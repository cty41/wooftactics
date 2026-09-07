import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from Tools.migration.godot_ai_baseline import canonical_bytes
from Tools.migration.refresh_godot_ai_vendor import refresh, render_notice


class GodotAiRefreshTests(unittest.TestCase):
    def test_notice_update_is_exact_and_preserves_other_rows(self):
        source = (
            "| Component | Version range or locked version | License | Use |\n"
            "|---|---:|---|---|\n"
            "| Godot AI EditorPlugin | 3.1.2 (`678b16a6`) | MIT | Editor tooling |\n"
            "| Other | 1.0 | MIT | Runtime |\n"
        )
        rendered = render_notice(source, "3.2.5", "43369d39abcdef")
        self.assertIn("| Godot AI EditorPlugin | 3.2.5 (`43369d39`) | MIT | Editor tooling |", rendered)
        self.assertIn("| Other | 1.0 | MIT | Runtime |", rendered)

    def test_notice_requires_one_managed_row(self):
        with self.assertRaisesRegex(ValueError, "exactly one"):
            render_notice("no table", "3.2.5", "43369d39")

    @patch("Tools.migration.refresh_godot_ai_vendor.subprocess.run")
    @patch("Tools.migration.refresh_godot_ai_vendor.inspect_source")
    def test_refresh_updates_vendor_manifest_and_notice_together(self, inspect_source, run):
        inspect_source.return_value = {
            "tag": "v3.2.5", "commit": "43369d39abcdef", "version": "3.2.5",
            "licenseSha256": hashlib.sha256(b"MIT\n").hexdigest(),
        }
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "repo"
            source = Path(temporary) / "source"
            plugin = source / "plugin/addons/godot_ai"
            plugin.mkdir(parents=True)
            (plugin / "plugin.cfg").write_text('version="3.2.5"\n', encoding="utf-8")
            (source / "LICENSE").write_text("MIT\n", encoding="utf-8")
            vendor = root / "godot/addons/godot_ai"
            vendor.mkdir(parents=True)
            (vendor / "old.txt").write_text("old", encoding="utf-8")
            manifest_path = root / "Tools/migration/manifest/godot-tooling.json"
            manifest_path.parent.mkdir(parents=True)
            manifest_path.write_text(json.dumps({"godotAi": {
                "tag": "v3.1.2", "commit": "old", "vendorPath": "godot/addons/godot_ai",
                "vendorTreeSha256": "old", "licenseSha256": "old", "vendorFileCount": 1,
            }}), encoding="utf-8")
            inventory = root / "Tools/public-release/dependency-inventory.json"
            inventory.parent.mkdir(parents=True)
            inventory.write_text("{}\n", encoding="utf-8")
            (root / "Tools/public-release/generate_dependency_report.py").write_text("", encoding="utf-8")
            (root / "THIRD_PARTY_NOTICES.md").write_text(
                "| Godot AI EditorPlugin | 3.1.2 (`old`) | MIT | Editor tooling |\n",
                encoding="utf-8",
            )

            result = refresh(root, source, "v3.2.5")

            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            self.assertEqual(manifest["godotAi"]["tag"], "v3.2.5")
            self.assertNotIn("vendorFileCount", manifest["godotAi"])
            self.assertEqual(result["version"], "3.2.5")
            self.assertFalse((vendor / "old.txt").exists())
            self.assertEqual(
                hashlib.sha256(canonical_bytes(vendor / "LICENSE")).hexdigest(),
                manifest["godotAi"]["licenseSha256"],
            )
            self.assertIn("3.2.5 (`43369d39`)", (root / "THIRD_PARTY_NOTICES.md").read_text(encoding="utf-8"))
            run.assert_called_once()


if __name__ == "__main__":
    unittest.main()
