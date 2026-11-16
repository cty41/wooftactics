from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml


SKILL = Path(".agents/skills/manual-qa-handoff/SKILL.md")
REFERENCE = Path(".agents/skills/manual-qa-handoff/references/output-contract.md")
METADATA = Path(".agents/skills/manual-qa-handoff/agents/openai.yaml")
LEDGER = Path(".agents/docs/manual-acceptance.md")
HISTORY = Path(".agents/docs/manual-acceptance-history.md")
ALLOWED_STATUSES = {"pending", "passed", "failed", "deferred", "blocked"}
FULL_FIELDS = (
    "Status", "Source", "Action", "Expected", "Observe", "Preserve on failure",
    "Save boundary", "Automated evidence", "User verdict",
)
SHORT_FIELDS = ("Status", "看什么", "怎样算过", "User verdict")
SECTION_PATTERN = re.compile(
    r"^## (?P<section>Pending|Passed|Deferred or Blocked)\n(?P<body>.*?)(?=^## |\Z)",
    re.MULTILINE | re.DOTALL,
)
ITEM_PATTERN = re.compile(
    r"^### (?P<id>MQA-[A-Z0-9]+-[A-Z0-9]+(?:-[A-Z0-9]+)*)\s+—[^\n]+\n(?P<body>.*?)(?=^### |\Z)",
    re.MULTILINE | re.DOTALL,
)
EXPECTED_SECTIONS = {
    "pending": "Pending", "failed": "Pending", "passed": "Passed",
    "deferred": "Deferred or Blocked", "blocked": "Deferred or Blocked",
}


def collect_items(text: str) -> list[tuple[str, re.Match[str]]]:
    items: list[tuple[str, re.Match[str]]] = []
    for section_match in SECTION_PATTERN.finditer(text):
        items.extend(
            (section_match.group("section"), match)
            for match in ITEM_PATTERN.finditer(section_match.group("body"))
        )
    return items


def check_items(errors: list[str], items: list[tuple[str, re.Match[str]]], *, short_pending: bool) -> None:
    for section, match in items:
        item_id = match.group("id")
        body = match.group("body")
        status_match = re.search(r"^- Status: `([^`]+)`", body, re.MULTILINE)
        if status_match is None or status_match.group(1) not in ALLOWED_STATUSES:
            errors.append(f"{item_id} has a missing or invalid Status.")
            status = ""
        else:
            status = status_match.group(1)
            if EXPECTED_SECTIONS[status] != section:
                errors.append(f"{item_id} status does not match its ledger section.")
        fields = SHORT_FIELDS if short_pending and status in {"pending", "failed"} else FULL_FIELDS
        for field in fields:
            field_match = re.search(rf"^- {re.escape(field)}:[ \t]*(\S.*)$", body, re.MULTILINE)
            if field_match is None:
                errors.append(f"{item_id} is missing or has an empty field: {field}")


def validate_manual_qa_handoff(root: Path) -> list[str]:
    errors: list[str] = []
    paths = (SKILL, REFERENCE, METADATA, LEDGER, HISTORY)
    for relative in paths:
        if not (root / relative).is_file():
            errors.append(f"Missing manual QA contract file: {relative.as_posix()}")
    if errors:
        return errors

    skill = (root / SKILL).read_text(encoding="utf-8")
    reference = (root / REFERENCE).read_text(encoding="utf-8")
    metadata = (root / METADATA).read_text(encoding="utf-8")
    ledger = (root / LEDGER).read_text(encoding="utf-8")
    history = (root / HISTORY).read_text(encoding="utf-8")

    boundary = "观感、操作手感、难度体验和编辑器实际操作仍须用户明确说通过才能改为 `passed`。"
    if boundary not in skill or boundary not in reference:
        errors.append("Manual QA contract must keep experiential acceptance on an explicit user verdict.")
    factual = "预期结果已全部被测试断言的事实（页面文字、存档字段、导航落点）"
    if factual not in skill or factual not in reference:
        errors.append("Manual QA contract must allow fully asserted facts to close without a screenshot review.")
    fold = "新的人工边界默认写进 `MQA-GODOT-SINGLE-RUN`，不另开九字段条目。"
    if fold not in skill or fold not in reference:
        errors.append("Manual QA contract must fold new human boundaries into the current single run.")
    for token in ("人工验收", "manual QA", "code review", "自动门禁", "references/output-contract.md"):
        if token not in skill:
            errors.append(f"Skill trigger/workflow is missing required token: {token}")

    for token in ("本轮重点", "累计待验收", "无需重复人工验证", "环境与收尾", "Godot"):
        if token not in reference:
            errors.append(f"Output contract is missing required coverage: {token}")
    for status in sorted(ALLOWED_STATUSES):
        if f"`{status}`" not in reference:
            errors.append(f"Output contract is missing status definition: {status}")

    try:
        metadata_document = yaml.safe_load(metadata)
    except yaml.YAMLError as exception:
        errors.append(f"openai.yaml is invalid YAML: {exception}")
        metadata_document = {}
    if not isinstance(metadata_document, dict):
        metadata_document = {}
    policy = metadata_document.get("policy", {})
    interface = metadata_document.get("interface", {})
    if not isinstance(policy, dict) or policy.get("allow_implicit_invocation") is not True:
        errors.append("openai.yaml must enable implicit invocation with a real boolean value.")
    default_prompt = interface.get("default_prompt") if isinstance(interface, dict) else None
    if not isinstance(default_prompt, str) or "$manual-qa-handoff" not in default_prompt:
        errors.append("openai.yaml default_prompt must name $manual-qa-handoff.")

    if "## Last Emitted Order" not in ledger:
        errors.append("Ledger must contain ## Last Emitted Order for ordinal feedback mapping.")

    living_items = collect_items(ledger)
    history_items = collect_items(history)
    if not living_items:
        errors.append("Ledger must contain at least one stable MQA item.")
    ids = [match.group("id") for _, match in living_items + history_items]
    if len(ids) != len(set(ids)):
        errors.append("Ledger stable IDs must be unique.")
    if "MQA-GODOT-SINGLE-RUN" not in ids:
        errors.append("Ledger must keep MQA-GODOT-SINGLE-RUN as the current single-run check.")

    check_items(errors, living_items, short_pending=True)
    check_items(errors, history_items, short_pending=False)

    for label, text, found in (
        ("Ledger", ledger, living_items),
        ("History", history, history_items),
    ):
        raw_headings = re.findall(r"^###\s+MQA-.*$", text, re.MULTILINE)
        if len(raw_headings) != len(found):
            errors.append(f"{label} contains malformed item headings or items outside a status section.")

    order_section = ledger.partition("## Last Emitted Order")[2]
    order_entries = re.findall(r"^(\d+)\. `(MQA-[A-Z0-9]+-[A-Z0-9]+(?:-[A-Z0-9]+)*)`", order_section, re.MULTILINE)
    raw_order_lines = re.findall(r"^\d+\.\s+.*$", order_section, re.MULTILINE)
    if len(raw_order_lines) != len(order_entries):
        errors.append("Last Emitted Order contains malformed entries.")
    ordinals = [int(ordinal) for ordinal, _ in order_entries]
    ordered_ids = [item_id for _, item_id in order_entries]
    if not order_entries:
        errors.append("Last Emitted Order must contain at least one numbered stable ID.")
    elif ordinals != list(range(1, len(ordinals) + 1)):
        errors.append("Last Emitted Order ordinals must be unique and contiguous from 1.")
    unknown = sorted(set(ordered_ids) - set(ids))
    if unknown:
        errors.append(f"Last Emitted Order references unknown IDs: {', '.join(unknown)}")
    if len(ordered_ids) != len(set(ordered_ids)):
        errors.append("Last Emitted Order must not repeat stable IDs.")

    return errors


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    errors = validate_manual_qa_handoff(root)
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        return 1
    print("Manual QA handoff policy: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
