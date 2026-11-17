from __future__ import annotations

import re
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from validate_manual_qa_handoff import validate_manual_qa_handoff  # noqa: E402


SKILL = ".agents/skills/manual-qa-handoff/SKILL.md"
REFERENCE = ".agents/skills/manual-qa-handoff/references/output-contract.md"
METADATA = ".agents/skills/manual-qa-handoff/agents/openai.yaml"
LEDGER = ".agents/docs/manual-acceptance.md"
HISTORY = ".agents/docs/manual-acceptance-history.md"


class ManualQaHandoffPolicyTests(unittest.TestCase):
    def test_repository_contract_is_complete(self) -> None:
        root = Path(__file__).resolve().parents[2]
        self.assertEqual([], validate_manual_qa_handoff(root))

    def test_missing_stable_order_mapping_fails(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            ledger = root / LEDGER
            ledger.write_text(ledger.read_text(encoding="utf-8").replace("## Last Emitted Order", "## Removed"), encoding="utf-8")
            self.assertTrue(any("Last Emitted Order" in error for error in validate_manual_qa_handoff(root)))

    def test_automatic_gate_cannot_promote_manual_pass(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            skill = root / SKILL
            skill.write_text(skill.read_text(encoding="utf-8").replace(
                "观感、操作手感、难度体验和编辑器实际操作仍须用户明确说通过才能改为 `passed`。",
                "自动门禁通过后把人工项目改为 `passed`。"), encoding="utf-8")
            self.assertTrue(any("explicit user verdict" in error for error in validate_manual_qa_handoff(root)))

    def test_status_must_match_ledger_section(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            ledger = root / LEDGER
            mutated, replacements = re.subn(
                r"(?ms)(## Pending\b.*?^### MQA-[^\n]+\n\n- Status: `)pending(`)",
                r"\1passed\2",
                ledger.read_text(encoding="utf-8"), count=1)
            self.assertEqual(replacements, 1)
            ledger.write_text(mutated, encoding="utf-8")
            self.assertTrue(any("section" in error for error in validate_manual_qa_handoff(root)))

    def test_required_fields_must_be_nonempty(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            ledger = root / LEDGER
            ledger.write_text(ledger.read_text(encoding="utf-8").replace(
                "- Action: 现在不单独看。", "- Action: \n- Note: 现在不单独看。", 1), encoding="utf-8")
            self.assertTrue(any("Action" in error for error in validate_manual_qa_handoff(root)))

    def test_last_emitted_ordinals_must_be_contiguous(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            ledger = root / LEDGER
            text = ledger.read_text(encoding="utf-8")
            item_ids = re.findall(r"^### (MQA-[^\s]+)\s+—", text, flags=re.MULTILINE)
            self.assertGreaterEqual(len(item_ids), 2, "fixture must expose two QA items")
            order_section = f"## Last Emitted Order\n\n1. `{item_ids[0]}`\n1. `{item_ids[1]}`\n"
            text = re.sub(r"(?ms)^## Last Emitted Order\b.*\Z", order_section, text)
            ledger.write_text(text, encoding="utf-8")
            self.assertTrue(any("contiguous" in error for error in validate_manual_qa_handoff(root)))

    def test_implicit_invocation_must_be_real_yaml_value(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            metadata = root / METADATA
            metadata.write_text(metadata.read_text(encoding="utf-8").replace(
                "allow_implicit_invocation: true",
                "allow_implicit_invocation: false\n  # allow_implicit_invocation: true"), encoding="utf-8")
            self.assertTrue(any("implicit invocation" in error for error in validate_manual_qa_handoff(root)))

    def test_malformed_item_heading_cannot_be_ignored(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            ledger = root / LEDGER
            ledger.write_text(ledger.read_text(encoding="utf-8").replace(
                "### MQA-GODOT-SINGLE-RUN —", "### MQA-GODOT-SINGLE-RUN -", 1), encoding="utf-8")
            self.assertTrue(any("malformed item" in error for error in validate_manual_qa_handoff(root)))

    def test_malformed_order_line_cannot_be_ignored(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.copy_contract(root)
            ledger = root / LEDGER
            ledger.write_text(ledger.read_text(encoding="utf-8") + "\n10. MQA-GODOT-INVENTORY\n", encoding="utf-8")
            self.assertTrue(any("malformed entries" in error for error in validate_manual_qa_handoff(root)))

    def copy_contract(self, root: Path) -> None:
        source = Path(__file__).resolve().parents[2]
        for relative in (SKILL, REFERENCE, METADATA, LEDGER, HISTORY):
            target = root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text((source / relative).read_text(encoding="utf-8"), encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
