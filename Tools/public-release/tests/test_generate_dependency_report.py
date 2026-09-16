import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "generate_dependency_report.py"
SPEC = importlib.util.spec_from_file_location("generate_dependency_report", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class DependencyInventoryTests(unittest.TestCase):
    def test_report_is_deterministic_and_deduplicated(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            lock = {
                "dependencies": {
                    "net9.0": {
                        "Example.Package": {"resolved": "1.2.3"},
                    }
                }
            }
            for directory in ("one", "two"):
                (root / directory).mkdir()
                (root / directory / "packages.lock.json").write_text(
                    json.dumps(lock), encoding="utf-8")
            first = MODULE.build_report(root)
            second = MODULE.build_report(root)
            self.assertEqual(first, second)
            self.assertEqual(1, first["dependencyCount"])
            self.assertEqual("Example.Package", first["dependencies"][0]["name"])

    def test_report_includes_manifest_pinned_vendors(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = root / "Tools/migration/manifest"
            adapter = root / "Tools/artworks"
            godot_ai_vendor = root / "godot/addons/godot_ai"
            maliang_vendor = root / "Tools/vendor/maliang"
            manifest.mkdir(parents=True)
            adapter.mkdir(parents=True)
            godot_ai_vendor.mkdir(parents=True)
            maliang_vendor.mkdir(parents=True)
            (manifest / "godot-tooling.json").write_text(
                json.dumps({"godotAi": {"tag": "v3.1.2", "vendorPath": "godot/addons/godot_ai"}}),
                encoding="utf-8",
            )
            (adapter / "maliang.adapter.json").write_text(
                json.dumps({"engine": {"version": "0.1.0", "submodulePath": "Tools/vendor/maliang"}}),
                encoding="utf-8",
            )
            report = MODULE.build_report(root)
            self.assertIn(
                {"ecosystem": "Vendored", "name": "godot-ai", "version": "3.1.2"},
                report["dependencies"],
            )
            self.assertIn(
                {"ecosystem": "Vendored", "name": "maliang-game-art", "version": "0.1.0"},
                report["dependencies"],
            )


if __name__ == "__main__":
    unittest.main()
