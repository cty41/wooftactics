import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from Tools.migration.godot_content_ownership import normalized_text_sha256, parse_catalog


ROOT = Path(__file__).resolve().parents[3]
RECEIPT = ROOT / "Tools/migration/manifest/ownership/godot-content-ownership-v1.json"


class GodotContentOwnershipTests(unittest.TestCase):
    def test_catalog_hash_is_stable_across_checkout_line_endings(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            lf = Path(directory) / "lf.tres"
            crlf = Path(directory) / "crlf.tres"
            lf.write_bytes(b'[gd_resource]\nvalue = "catalog"\n')
            crlf.write_bytes(b'[gd_resource]\r\nvalue = "catalog"\r\n')

            self.assertEqual(normalized_text_sha256(lf), normalized_text_sha256(crlf))

    def test_receipt_is_current_and_separates_manual_acceptance(self) -> None:
        subprocess.run(
            ["python", "Tools/migration/godot_content_ownership.py", "--check"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
        self.assertEqual("GodotOwned", receipt["ownership"])
        self.assertEqual(185, receipt["catalogCount"])
        self.assertEqual("pending_separate_quality_gate", receipt["manualAcceptance"])
        self.assertTrue(receipt["historicalExportReceiptsPreserved"])
        self.assertTrue(all(item["owner"] == "GodotOwned" for item in receipt["categories"]))

    def test_poet_catalog_inventory_is_godot_owned_without_claiming_manual_art_acceptance(self) -> None:
        poet_entries = {
            item["contentId"]: item for item in parse_catalog()
            if item["contentId"].startswith(("skill.poet.", "status.poet."))
            or item["contentId"] in {"unit.pure-run.poet", "unit.pure-run.poet-decoy"}
        }
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))

        self.assertEqual(19, len(poet_entries))
        self.assertEqual(15, sum(item["resourceType"] == "skill" for item in poet_entries.values()))
        self.assertEqual(2, sum(item["resourceType"] == "buff" for item in poet_entries.values()))
        self.assertEqual(2, sum(item["resourceType"] == "unit" for item in poet_entries.values()))
        self.assertEqual("GodotOwned", receipt["ownership"])
        self.assertEqual("pending_separate_quality_gate", receipt["manualAcceptance"])

    def test_historical_batches_remain_immutable_unity_owned_evidence(self) -> None:
        batches = ROOT / "Tools/migration/manifest/batches"
        documents = [json.loads(path.read_text(encoding="utf-8")) for path in batches.glob("*.json")]
        owners = {document.get("ownership", document.get("owner")) for document in documents}
        self.assertIn("UnityOwned", owners)
        self.assertNotIn("GodotOwned", owners)


if __name__ == "__main__":
    unittest.main()
