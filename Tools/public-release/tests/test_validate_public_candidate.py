import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "validate_public_candidate.py"
SPEC = importlib.util.spec_from_file_location("validate_public_candidate", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
audit = MODULE.audit


class PublicCandidateAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        (self.root / "Tools/public-release").mkdir(parents=True)
        for required in (
            "LICENSE", "NOTICE", "ASSET_LICENSE.md", "THIRD_PARTY_NOTICES.md",
            "TRADEMARKS.md", "CONTRIBUTING.md", "SECURITY.md",
        ):
            (self.root / required).write_text("test\n", encoding="utf-8")
        (self.root / "asset.png").write_bytes(b"asset")
        self.hash = hashlib.sha256(b"asset").hexdigest()
        self._write_policy()
        self._write_manifest(self.hash)
        subprocess.run(["git", "add", "."], cwd=self.root, check=True)

    def tearDown(self):
        self.temp.cleanup()

    def _write_policy(self):
        policy = {
            "schemaVersion": 1,
            "defaultTextLicense": "Apache-2.0",
            "assetManifest": "Tools/public-release/asset-provenance.json",
            "requiredFiles": [
                "LICENSE", "NOTICE", "ASSET_LICENSE.md", "THIRD_PARTY_NOTICES.md",
                "TRADEMARKS.md", "CONTRIBUTING.md", "SECURITY.md",
            ],
            "mediaExtensions": [".png"],
            "excludedFromPublicRoot": ["private/**"],
            "forbiddenPublicPrefixes": ["Assets/"],
            "allowedLicenses": ["CC-BY-4.0"],
        }
        (self.root / "Tools/public-release/public-source-policy.json").write_text(
            json.dumps(policy), encoding="utf-8")

    def _write_manifest(self, digest):
        manifest = {
            "schemaVersion": 1,
            "defaultAttribution": "cty41",
            "entries": [{
                "path": "asset.png",
                "sha256": digest,
                "status": "approved",
                "rightsHolder": "cty41",
                "license": "CC-BY-4.0",
                "provenance": "test",
            }],
        }
        (self.root / "Tools/public-release/asset-provenance.json").write_text(
            json.dumps(manifest), encoding="utf-8")

    def test_candidate_accepts_registered_asset(self):
        result = audit(self.root, candidate=True)
        self.assertTrue(result.ok, result.errors)
        self.assertEqual(1, result.approved_assets)

    def test_candidate_allows_documented_placeholder_user_paths(self):
        sample = self.root / "sample.md"
        sample.write_text(r"Examples: C:\Users\foo\uvx.exe and C:\Users\<user>\uvx.exe", encoding="utf-8")
        subprocess.run(["git", "add", "sample.md"], cwd=self.root, check=True)
        result = audit(self.root, candidate=True)
        self.assertTrue(result.ok, result.errors)

    def test_candidate_rejects_concrete_local_user_path(self):
        sample = self.root / "sample.md"
        sample.write_text("C:\\Users\\" + "alice\\secret.txt", encoding="utf-8")
        subprocess.run(["git", "add", "sample.md"], cwd=self.root, check=True)
        result = audit(self.root, candidate=True)
        self.assertIn("local_user_path:sample.md", result.errors)

    def test_candidate_rejects_hash_drift(self):
        self._write_manifest("0" * 64)
        result = audit(self.root, candidate=True)
        self.assertIn("asset_hash_mismatch:asset.png", result.errors)

    def test_report_warns_but_candidate_rejects_excluded_file(self):
        (self.root / "private").mkdir()
        (self.root / "private/internal.txt").write_text("internal", encoding="utf-8")
        subprocess.run(["git", "add", "private/internal.txt"], cwd=self.root, check=True)
        report = audit(self.root, candidate=False)
        candidate = audit(self.root, candidate=True)
        self.assertIn("excluded_files_tracked:1", report.warnings)
        self.assertIn("excluded_files_tracked:1", candidate.errors)

    def test_artwork_requires_state_inventory_and_formal_state(self):
        artwork = self.root / "Tools/artworks/doge/calibrated/hero.png"
        artwork.parent.mkdir(parents=True)
        artwork.write_bytes(b"hero")
        manifest = json.loads((self.root / "Tools/public-release/asset-provenance.json").read_text(encoding="utf-8"))
        manifest["entries"].append({
            "path": "Tools/artworks/doge/calibrated/hero.png",
            "sha256": hashlib.sha256(b"hero").hexdigest(), "status": "approved",
            "rightsHolder": "cty41", "license": "CC-BY-4.0", "provenance": "test",
        })
        (self.root / "Tools/public-release/asset-provenance.json").write_text(json.dumps(manifest), encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=self.root, check=True)
        missing = audit(self.root, candidate=True)
        self.assertIn("artwork_state_unregistered:Tools/artworks/doge/calibrated/hero.png", missing.errors)

        pipeline = self.root / "Tools/artworks/pipeline"
        pipeline.mkdir(parents=True)
        inventory = {"schemaVersion": 1, "assets": [{
            "path": "Tools/artworks/doge/calibrated/hero.png", "sha256": hashlib.sha256(b"hero").hexdigest(),
            "state": "legacy-unresolved", "lineage": None,
        }]}
        (pipeline / "legacy-assets.json").write_text(json.dumps(inventory), encoding="utf-8")
        invalid = audit(self.root, candidate=True)
        self.assertIn("artwork_formal_state_invalid:Tools/artworks/doge/calibrated/hero.png:legacy-unresolved", invalid.errors)

        inventory["assets"][0]["state"] = "legacy-approved"
        (pipeline / "legacy-assets.json").write_text(json.dumps(inventory), encoding="utf-8")
        accepted = audit(self.root, candidate=True)
        self.assertFalse(any(error.startswith("artwork_") for error in accepted.errors), accepted.errors)

    def test_candidate_accepts_attempt_and_supporting_artifact_inventory(self):
        pipeline = self.root / "Tools/artworks/pipeline"
        attempts = pipeline / "attempts"
        supporting = pipeline / "supporting-artifacts"
        attempts.mkdir(parents=True)
        supporting.mkdir(parents=True)
        paths = [
            "Tools/artworks/doge/concepts/hero-source.png",
            "Tools/artworks/doge/reviews/hero-review.png",
        ]
        manifest = json.loads((self.root / "Tools/public-release/asset-provenance.json").read_text(encoding="utf-8"))
        for path in paths:
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(path.encode("utf-8"))
            manifest["entries"].append({
                "path": path, "sha256": hashlib.sha256(path.encode("utf-8")).hexdigest(),
                "status": "approved", "rightsHolder": "cty41", "license": "CC-BY-4.0",
                "provenance": "test",
            })
        (self.root / "Tools/public-release/asset-provenance.json").write_text(json.dumps(manifest), encoding="utf-8")
        (attempts / "attempt.json").write_text(json.dumps({
            "state": "validated", "artifacts": {"source": {"path": paths[0]}},
        }), encoding="utf-8")
        (supporting / "review.json").write_text(json.dumps({
            "artifact": {"path": paths[1]}, "role": "review",
        }), encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=self.root, check=True)

        result = audit(self.root, candidate=True)

        self.assertFalse(any(error.startswith("artwork_state_unregistered:") for error in result.errors), result.errors)


if __name__ == "__main__":
    unittest.main()
