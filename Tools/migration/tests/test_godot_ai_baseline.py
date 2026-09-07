import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from Tools.migration.godot_ai_baseline import validate_checkout


class GodotAiBaselineTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.checkout = self.root / "source"
        plugin = self.checkout / "plugin/addons/godot_ai/plugin.cfg"
        plugin.parent.mkdir(parents=True)
        plugin.write_text('version="3.2.5"\n', encoding="utf-8")
        (self.checkout / "LICENSE").write_text("MIT\n", encoding="utf-8")
        self.manifest = self.root / "godot-tooling.json"
        self.manifest.write_text(json.dumps({"godotAi": {
            "tag": "v3.2.5",
            "commit": "abc123",
            "licenseSha256": hashlib.sha256(b"MIT\n").hexdigest(),
        }}), encoding="utf-8")

    def tearDown(self):
        self.temporary.cleanup()

    @patch("Tools.migration.godot_ai_baseline.subprocess.check_output")
    def test_baseline_contract_is_manifest_driven_and_read_only(self, check_output):
        check_output.side_effect = ["abc123\n", "v3.2.5\n", "abc123\n", ""]
        result = validate_checkout(self.checkout, self.manifest)
        self.assertEqual(result["version"], "3.2.5")
        self.assertEqual(result["tag"], "v3.2.5")

    @patch("Tools.migration.godot_ai_baseline.subprocess.check_output")
    def test_dirty_checkout_is_rejected(self, check_output):
        check_output.side_effect = ["abc123\n", "v3.2.5\n", "abc123\n", " M plugin.cfg\n"]
        with self.assertRaisesRegex(RuntimeError, "dirty"):
            validate_checkout(self.checkout, self.manifest)

    @patch("Tools.migration.godot_ai_baseline.subprocess.check_output")
    def test_tag_and_plugin_version_must_agree(self, check_output):
        check_output.side_effect = ["abc123\n", "v3.2.5\n", "abc123\n", ""]
        (self.checkout / "plugin/addons/godot_ai/plugin.cfg").write_text(
            'version="3.2.4"\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(RuntimeError, "manifest/plugin version mismatch"):
            validate_checkout(self.checkout, self.manifest)


if __name__ == "__main__":
    unittest.main()
