from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from validate_compose_spec import validate_root, validate_spec  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[2]
REAL_SPEC_ROOT = REPO_ROOT / "docs" / "compose" / "spec"

DESIGNED_SPEC = """---
feature: sample-feature
status: designed
updated: 2026-11-16
source: feat/sample
---

# Sample Feature

## Report

## [S1] Problem
Something to fix.

## [S2] Design

### S2.1 Contract
Behavior.

## Tasks

- [ ] T1: build it — acceptance: observable result (covers: S2.1)
- [ ] T2: verify it — acceptance: observable result (depends: T1)
"""


def delivered_variant() -> str:
    return DESIGNED_SPEC.replace(
        "status: designed", "status: delivered", 1
    ).replace(
        "## Report\n",
        "## Report\n\n**What was built** — the thing.\n\n**Verification** — command passed.\n",
        1,
    ).replace("- [ ]", "- [x]")


class ComposeSpecPolicyTests(unittest.TestCase):
    def write_spec(self, root: Path, text: str) -> Path:
        path = root / "sample-feature.md"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def validate(self, text: str) -> list[str]:
        with tempfile.TemporaryDirectory() as directory:
            return validate_spec(self.write_spec(Path(directory), text))

    def test_designed_spec_passes(self) -> None:
        self.assertEqual([], self.validate(DESIGNED_SPEC))

    def test_delivered_spec_passes(self) -> None:
        self.assertEqual([], self.validate(delivered_variant()))

    def test_missing_frontmatter_fails(self) -> None:
        errors = self.validate("# No frontmatter\n")
        self.assertTrue(any("missing YAML frontmatter" in error for error in errors))

    def test_missing_or_empty_field_fails(self) -> None:
        errors = self.validate(DESIGNED_SPEC.replace("source: feat/sample\n", ""))
        self.assertTrue(any("missing fields: source" in error for error in errors))
        errors = self.validate(DESIGNED_SPEC.replace("updated: 2026-11-16", "updated:"))
        self.assertTrue(any("empty field 'updated'" in error for error in errors))

    def test_invalid_status_fails(self) -> None:
        errors = self.validate(DESIGNED_SPEC.replace("status: designed", "status: shipped"))
        self.assertTrue(any("invalid status 'shipped'" in error for error in errors))

    def test_delivered_requires_report(self) -> None:
        errors = self.validate(DESIGNED_SPEC.replace("status: designed", "status: delivered"))
        self.assertTrue(any("must fill Report" in error for error in errors))

    def test_delivered_report_requires_both_markers(self) -> None:
        half = delivered_variant().replace("**Verification** — command passed.\n", "")
        errors = self.validate(half)
        self.assertTrue(any("Report missing 'Verification'" in error for error in errors))

    def test_undelivered_report_must_stay_empty(self) -> None:
        filled = DESIGNED_SPEC.replace(
            "## Report\n", "## Report\n\n**What was built** — early draft.\n", 1
        )
        errors = self.validate(filled)
        self.assertTrue(any("must stay empty until delivered" in error for error in errors))

    def test_delivered_requires_checked_tasks(self) -> None:
        stale = delivered_variant().replace("- [x] T2:", "- [ ] T2:", 1)
        errors = self.validate(stale)
        self.assertTrue(any("unchecked task 'T2'" in error for error in errors))

    def test_delivered_report_only_spec_passes(self) -> None:
        body = delivered_variant()
        start = body.index("## Tasks")
        end = body.index("## Journey log") if "## Journey log" in body else len(body)
        report_only = body[:start] + body[end:]
        self.assertEqual([], self.validate(report_only))

    def test_delivered_unchecked_box_without_id_fails(self) -> None:
        mixed = delivered_variant().replace(
            "- [x] T1: build it", "- [ ] write docs\n- [x] T1: build it", 1
        )
        errors = self.validate(mixed)
        self.assertTrue(any("unchecked task item" in error for error in errors))

    def test_uppercase_checked_marker_passes(self) -> None:
        upper = delivered_variant().replace("- [x] T1:", "- [X] T1:", 1)
        self.assertEqual([], self.validate(upper))

    def test_malformed_tasks_section_fails(self) -> None:
        broken = DESIGNED_SPEC.replace("- [ ] T1:", "- no marker T1:", 1).replace(
            "- [ ] T2:", "- no marker T2:", 1
        )
        errors = self.validate(broken)
        self.assertTrue(any("no parseable task items" in error for error in errors))

    def test_placeholder_text_fails(self) -> None:
        errors = self.validate(DESIGNED_SPEC.replace("observable result", "TBD result"))
        self.assertTrue(any("placeholder text in Tasks" in error for error in errors))

    def test_covers_anchor_must_exist(self) -> None:
        errors = self.validate(DESIGNED_SPEC.replace("covers: S2.1", "covers: S9.9"))
        self.assertTrue(any("covers 'S9.9' has no matching section heading" in error for error in errors))

    def test_depends_target_must_exist(self) -> None:
        errors = self.validate(DESIGNED_SPEC.replace("depends: T1", "depends: T9"))
        self.assertTrue(any("depends 'T9' has no matching task id" in error for error in errors))

    def test_real_tree_specs_pass(self) -> None:
        self.assertEqual([], validate_root(REAL_SPEC_ROOT))


if __name__ == "__main__":
    unittest.main()
