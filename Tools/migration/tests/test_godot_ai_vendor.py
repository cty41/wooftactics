import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from Tools.migration.godot_ai_baseline import canonical_bytes
from Tools.migration.godot_ai_vendor import tree_digest, validate


class GodotAiVendorTests(unittest.TestCase):
    def test_tree_digest_is_path_and_content_sensitive(self):
        with tempfile.TemporaryDirectory() as temporary:
            vendor = Path(temporary)
            (vendor / "nested").mkdir()
            (vendor / "nested/file.txt").write_text("one", encoding="utf-8")
            first = tree_digest(vendor)
            (vendor / "nested/file.txt").write_text("two", encoding="utf-8")
            self.assertNotEqual(first, tree_digest(vendor))

    def test_tree_digest_normalizes_text_line_endings(self):
        with tempfile.TemporaryDirectory() as temporary:
            vendor = Path(temporary)
            sample = vendor / "plugin.cfg"
            sample.write_bytes(b"[plugin]\r\nversion=\"3.2.5\"\r\n")
            windows = tree_digest(vendor)
            sample.write_bytes(b"[plugin]\nversion=\"3.2.5\"\n")
            self.assertEqual(windows, tree_digest(vendor))

    def test_validate_accepts_minimal_manifest_and_rejects_drift(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            vendor = root / "godot/addons/godot_ai"
            manifest_dir = root / "Tools/migration/manifest"
            vendor.mkdir(parents=True)
            manifest_dir.mkdir(parents=True)
            (vendor / "plugin.cfg").write_text('version="3.2.5"\n', encoding="utf-8")
            (vendor / "LICENSE").write_text("MIT\n", encoding="utf-8")
            _, digest = tree_digest(vendor)
            policy = {"godotAi": {
                "tag": "v3.2.5",
                "commit": "abc123",
                "vendorPath": "godot/addons/godot_ai",
                "vendorTreeSha256": digest,
                "licenseSha256": hashlib.sha256(canonical_bytes(vendor / "LICENSE")).hexdigest(),
            }}
            (manifest_dir / "godot-tooling.json").write_text(json.dumps(policy), encoding="utf-8")
            result = validate(root)
            self.assertEqual(result["version"], "3.2.5")
            (vendor / "plugin.cfg").write_text("drift", encoding="utf-8")
            with self.assertRaisesRegex((ValueError, RuntimeError), "version|drifted"):
                validate(root)

    def test_vendor_path_cannot_escape_repository(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest_dir = root / "Tools/migration/manifest"
            manifest_dir.mkdir(parents=True)
            (manifest_dir / "godot-tooling.json").write_text(json.dumps({"godotAi": {
                "tag": "v3.2.5", "commit": "abc", "vendorPath": "../outside",
                "vendorTreeSha256": "x", "licenseSha256": "y",
            }}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "inside repository"):
                validate(root)


class GodotAiRepositoryVendorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]

    def test_repository_vendor_matches_manifest_and_is_enabled(self):
        validate(self.root)
        project = (self.root / "godot/project.godot").read_text(encoding="utf-8")
        self.assertIn('_mcp_game_helper="*res://addons/godot_ai/runtime/game_helper.gd"', project)
        self.assertIn('"res://addons/godot_ai/plugin.cfg"', project)

    def test_vendor_is_public_source_but_excluded_from_runtime_export(self):
        policy = json.loads(
            (self.root / "Tools/public-release/public-source-policy.json").read_text(encoding="utf-8")
        )
        self.assertNotIn("godot/addons/godot_ai/**", policy["excludedFromPublicRoot"])
        export = (self.root / "godot/export_presets.cfg").read_text(encoding="utf-8")
        self.assertIn("addons/godot_ai/*", export)


if __name__ == "__main__":
    unittest.main()
