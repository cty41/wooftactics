#!/usr/bin/env python3
"""Validate .agents/compose/spec/*.md feature-document structure without external packages.

Rules (see .agents/compose/spec/review-nits-to-gates.md S2.1):
A1 frontmatter has non-empty feature/status/updated/source; status in designed|in-progress|delivered.
A2 status delivered => Report non-empty with "What was built" and "Verification".
A3 status designed|in-progress => Report empty (template: fill only at finalize).
A4 status delivered => every Tasks item checked.
A5 Tasks acceptance text carries no placeholder (TBD/TODO/完成实现/添加适当).
A6 covers:/depends: references resolve to headings and task ids in the same document.
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

REQUIRED_FIELDS = {"feature", "status", "updated", "source"}
ALLOWED_STATUS = {"designed", "in-progress", "delivered"}
PLACEHOLDER_PATTERN = re.compile(r"TBD|TODO|完成实现|添加适当")
TASK_PATTERN = re.compile(r"^- \[(?P<mark>[ xX])\] (?P<id>T\d+):", re.MULTILINE)
UNCHECKED_LINE_PATTERN = re.compile(r"^- \[ \]")
COVERS_PATTERN = re.compile(r"covers:\s*((?:S\d+(?:\.\d+)?)(?:\s*,\s*S\d+(?:\.\d+)?)*)")
DEPENDS_PATTERN = re.compile(r"depends:\s*((?:T\d+)(?:\s*,\s*T\d+)*)")
SECTION_ANCHOR_PATTERN = re.compile(r"^#{2,3} \[?(?P<anchor>S\d+(?:\.\d+)?)\]?", re.MULTILINE)


def parse_frontmatter(text: str) -> dict[str, str]:
    match = re.match(r"^---\s*\n(.*?)\n---\s*\n", text, re.DOTALL)
    if not match:
        raise ValueError("missing YAML frontmatter")

    fields: dict[str, str] = {}
    for line in match.group(1).splitlines():
        if not line.strip():
            continue
        key, separator, value = line.partition(":")
        if not separator:
            raise ValueError(f"invalid frontmatter line: {line}")
        fields[key.strip()] = value.strip().strip('"').strip("'")
    return fields


def extract_section(text: str, title: str) -> str:
    match = re.search(
        rf"^## {re.escape(title)}\s*\n(.*?)(?=^## |\Z)",
        text,
        re.MULTILINE | re.DOTALL,
    )
    return match.group(1) if match else ""


def validate_spec(path: Path) -> list[str]:
    errors: list[str] = []
    text = path.read_text(encoding="utf-8")
    try:
        fields = parse_frontmatter(text)
    except ValueError as error:
        return [f"{path}: {error}"]

    missing = sorted(REQUIRED_FIELDS - fields.keys())
    if missing:
        errors.append(f"{path}: missing fields: {', '.join(missing)}")
    for name in sorted(REQUIRED_FIELDS & fields.keys()):
        if not fields[name]:
            errors.append(f"{path}: empty field '{name}'")

    status = fields.get("status", "")
    if status and status not in ALLOWED_STATUS:
        errors.append(f"{path}: invalid status '{status}'")

    report = extract_section(text, "Report")
    report_body = report.strip()
    if status == "delivered":
        if not report_body:
            errors.append(f"{path}: delivered spec must fill Report")
        else:
            for marker in ("What was built", "Verification"):
                if marker not in report_body:
                    errors.append(f"{path}: Report missing '{marker}'")
    elif status in ALLOWED_STATUS and report_body:
        errors.append(f"{path}: Report must stay empty until delivered")

    tasks_section = extract_section(text, "Tasks")
    task_marks = {match.group("id"): match.group("mark") for match in TASK_PATTERN.finditer(tasks_section)}
    if tasks_section.strip() and not task_marks:
        errors.append(f"{path}: Tasks section has no parseable task items")
    if status == "delivered":
        for task_id, mark in sorted(task_marks.items()):
            if mark.strip().lower() != "x":
                errors.append(f"{path}: delivered spec has unchecked task '{task_id}'")
        for line in tasks_section.splitlines():
            if UNCHECKED_LINE_PATTERN.match(line) and not TASK_PATTERN.match(line):
                errors.append(f"{path}: delivered spec has unchecked task item: {line.strip()[:60]}")

    for line in tasks_section.splitlines():
        if PLACEHOLDER_PATTERN.search(line):
            errors.append(f"{path}: placeholder text in Tasks: {line.strip()[:60]}")

    anchors = set(SECTION_ANCHOR_PATTERN.findall(text))
    for raw in COVERS_PATTERN.findall(text):
        for anchor in (part.strip() for part in raw.split(",")):
            if anchor and anchor not in anchors:
                errors.append(f"{path}: covers '{anchor}' has no matching section heading")
    for raw in DEPENDS_PATTERN.findall(text):
        for task_id in (part.strip() for part in raw.split(",")):
            if task_id and task_id not in task_marks:
                errors.append(f"{path}: depends '{task_id}' has no matching task id")
    return errors


def validate_root(root: Path) -> list[str]:
    errors: list[str] = []
    for path in sorted(root.glob("*.md")):
        errors.extend(validate_spec(path))
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(".agents/compose/spec"),
        help="compose spec directory",
    )
    args = parser.parse_args()

    errors = validate_root(args.root)
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        return 1
    count = len(list(args.root.glob("*.md")))
    print(f"Compose spec validation passed: {count} spec(s) checked.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
