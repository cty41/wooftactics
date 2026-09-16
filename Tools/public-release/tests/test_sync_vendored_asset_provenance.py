import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "sync_vendored_asset_provenance.py"
SPEC = importlib.util.spec_from_file_location("sync_vendored_asset_provenance", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class VendoredAssetProvenanceTests(unittest.TestCase):
    def test_classifies_pinned_maliang_example_media(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            vendor = root / "Tools/vendor/maliang"
            poet = vendor / "examples/poet-cast/sample.png"
            synthetic = vendor / "examples/synthetic/success/delivery.png"
            poet.parent.mkdir(parents=True)
            synthetic.parent.mkdir(parents=True)
            poet.write_bytes(b"poet")
            synthetic.write_bytes(b"synthetic")
            subprocess.run(["git", "init", "-q"], cwd=vendor, check=True)
            subprocess.run(["git", "config", "user.name", "test"], cwd=vendor, check=True)
            subprocess.run(["git", "config", "user.email", "test@invalid"], cwd=vendor, check=True)
            subprocess.run(["git", "add", "."], cwd=vendor, check=True)
            subprocess.run(["git", "commit", "-qm", "fixture"], cwd=vendor, check=True)
            commit = subprocess.run(
                ["git", "rev-parse", "HEAD"], cwd=vendor, check=True,
                text=True, stdout=subprocess.PIPE,
            ).stdout.strip()
            adapter = root / "Tools/artworks/maliang.adapter.json"
            adapter.parent.mkdir(parents=True)
            adapter.write_text(json.dumps({"engine": {
                "submodulePath": "Tools/vendor/maliang", "commit": commit,
            }}), encoding="utf-8")

            entries = MODULE.expected_entries(root)

            self.assertEqual(2, len(entries))
            by_path = {entry["path"]: entry for entry in entries}
            self.assertEqual("CC-BY-4.0", by_path["Tools/vendor/maliang/examples/poet-cast/sample.png"]["license"])
            self.assertEqual("cty41", by_path["Tools/vendor/maliang/examples/poet-cast/sample.png"]["rightsHolder"])
            self.assertEqual("MIT", by_path["Tools/vendor/maliang/examples/synthetic/success/delivery.png"]["license"])
            self.assertEqual("MaLiang contributors", by_path["Tools/vendor/maliang/examples/synthetic/success/delivery.png"]["rightsHolder"])


if __name__ == "__main__":
    unittest.main()
