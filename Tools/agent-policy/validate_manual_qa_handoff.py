from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml


SKILL = Path(".agents/skills/manual-qa-handoff/SKILL.md")
REFERENCE = Path(".agents/skills/manual-qa-handoff/references/output-contract.md")
METADATA = Path(".agents/skills/manual-qa-handoff/agents/openai.yaml")
LEDGER = Path(".agents/docs/manual-acceptance.md")
ALLOWED_STATUSES = {"pending", "passed", "failed", "deferred", "blocked"}


def validate_manual_qa_handoff(root: Path) -> list[str]:
    errors: list[str] = []
    paths = (SKILL, REFERENCE, METADATA, LEDGER)
    for relative in paths:
        if not (root / relative).is_file():
            errors.append(f"Missing manual QA contract file: {relative.as_posix()}")
    if errors:
        return errors

    skill = (root / SKILL).read_text(encoding="utf-8")
    reference = (root / REFERENCE).read_text(encoding="utf-8")
    metadata = (root / METADATA).read_text(encoding="utf-8")
    ledger = (root / LEDGER).read_text(encoding="utf-8")

    boundary = "观感、操作手感、难度体验和编辑器实际操作仍须用户明确说通过才能改为 `passed`。"
    if boundary not in skill or boundary not in reference:
        errors.append("Manual QA contract must keep experiential acceptance on an explicit user verdict.")
    factual = "预期结果已全部被测试断言的事实（页面文字、存档字段、导航落点）"
    if factual not in skill or factual not in reference:
        errors.append("Manual QA contract must allow fully asserted facts to close without a screenshot review.")
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

    section_pattern = re.compile(r"^## (?P<section>Pending|Passed|Deferred or Blocked)\n(?P<body>.*?)(?=^## |\Z)", re.MULTILINE | re.DOTALL)
    item_pattern = re.compile(
        r"^### (?P<id>MQA-[A-Z0-9]+-[A-Z0-9]+(?:-[A-Z0-9]+)*)\s+—[^\n]+\n(?P<body>.*?)(?=^### |\Z)",
        re.MULTILINE | re.DOTALL,
    )
    items: list[tuple[str, re.Match[str]]] = []
    for section_match in section_pattern.finditer(ledger):
        items.extend((section_match.group("section"), match) for match in item_pattern.finditer(section_match.group("body")))
    ids = [match.group("id") for _, match in items]
    if not items:
        errors.append("Ledger must contain at least one stable MQA item.")
    if len(ids) != len(set(ids)):
        errors.append("Ledger stable IDs must be unique.")

    required_fields = (
        "Status", "Source", "Action", "Expected", "Observe", "Preserve on failure",
        "Save boundary", "Automated evidence", "User verdict",
    )
    expected_sections = {
        "pending": "Pending", "failed": "Pending", "passed": "Passed",
        "deferred": "Deferred or Blocked", "blocked": "Deferred or Blocked",
    }
    for section, match in items:
        item_id = match.group("id")
        body = match.group("body")
        status_match = re.search(r"^- Status: `([^`]+)`", body, re.MULTILINE)
        if status_match is None or status_match.group(1) not in ALLOWED_STATUSES:
            errors.append(f"{item_id} has a missing or invalid Status.")
        elif expected_sections[status_match.group(1)] != section:
            errors.append(f"{item_id} status does not match its ledger section.")
        for field in required_fields:
            field_match = re.search(rf"^- {re.escape(field)}:[ \t]*(\S.*)$", body, re.MULTILINE)
            if field_match is None:
                errors.append(f"{item_id} is missing or has an empty field: {field}")

    raw_headings = re.findall(r"^###\s+MQA-.*$", ledger, re.MULTILINE)
    if len(raw_headings) != len(items):
        errors.append("Ledger contains malformed item headings or items outside a status section.")

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
