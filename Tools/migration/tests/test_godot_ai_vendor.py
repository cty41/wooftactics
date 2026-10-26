import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from Tools.migration.godot_ai_vendor import canonical_bytes, tree_digest, validate


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
            sample.write_bytes(b"[plugin]\r\nversion=\"4.3.1\"\r\n")
            windows = tree_digest(vendor)
            sample.write_bytes(b"[plugin]\nversion=\"4.3.1\"\n")
            self.assertEqual(windows, tree_digest(vendor))

    def test_validate_accepts_exact_manifest_and_rejects_drift(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            vendor = root / "godot/addons/godot_ai"
            manifest_dir = root / "Tools/migration/manifest"
            vendor.mkdir(parents=True)
            manifest_dir.mkdir(parents=True)
            (vendor / "plugin.cfg").write_text('version="4.3.1"\n', encoding="utf-8")
            (vendor / "LICENSE").write_text("MIT\n", encoding="utf-8")
            count, digest = tree_digest(vendor)
            policy = {
                "godotAi": {
                    "vendorPath": "godot/addons/godot_ai",
                    "vendorFileCount": count,
                    "vendorTreeSha256": digest,
                    "vendorPluginCfgSha256": hashlib.sha256(canonical_bytes(vendor / "plugin.cfg")).hexdigest(),
                    "vendorLicenseSha256": hashlib.sha256(canonical_bytes(vendor / "LICENSE")).hexdigest(),
                }
            }
            (manifest_dir / "godot-tooling.json").write_text(json.dumps(policy), encoding="utf-8")
            validate(root)
            (vendor / "plugin.cfg").write_text("drift", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "drifted"):
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
