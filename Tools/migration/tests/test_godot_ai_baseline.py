import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from Tools.migration.godot_ai_baseline import load_expected, validate_checkout

COMMIT = "35da2c2fe1e2c20e11b5be01841e966951d6314b"
TAG = "v4.3.1"
PLUGIN_CFG = b'[plugin]\nversion="4.3.1"\n'


class GodotAiBaselineTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        plugin_dir = self.root / "plugin" / "addons" / "godot_ai"
        plugin_dir.mkdir(parents=True)
        self.plugin = plugin_dir / "plugin.cfg"
        self.plugin.write_bytes(PLUGIN_CFG)
        self.manifest = self.root / "manifest.json"
        self.manifest.write_text(
            json.dumps(
                {
                    "godotAi": {
                        "tag": TAG,
                        "commit": COMMIT,
                        "pluginCfgSha256": hashlib.sha256(PLUGIN_CFG).hexdigest(),
                    }
                }
            ),
            encoding="utf-8",
        )

    def _run(self, commit: str = COMMIT, tag: str = TAG) -> dict:
        with patch(
            "Tools.migration.godot_ai_baseline.subprocess.check_output",
            side_effect=[commit + "\n", tag + "\n"],
        ):
            return validate_checkout(self.root, self.manifest)

    def test_matching_checkout_passes(self) -> None:
        self.assertEqual(self._run()["tag"], TAG)

    def test_crlf_checkout_still_matches(self) -> None:
        self.plugin.write_bytes(PLUGIN_CFG.replace(b"\n", b"\r\n"))
        self.assertEqual(self._run()["commit"], COMMIT)

    def test_commit_mismatch_fails(self) -> None:
        with self.assertRaises(RuntimeError):
            self._run(commit="0" * 40)

    def test_tag_mismatch_fails(self) -> None:
        with self.assertRaises(RuntimeError):
            self._run(tag="v3.1.2")

    def test_plugin_digest_mismatch_fails(self) -> None:
        self.plugin.write_bytes(b'[plugin]\nversion="3.1.2"\n')
        with self.assertRaises(RuntimeError):
            self._run()

    def test_repository_manifest_is_the_pinned_baseline(self) -> None:
        expected = load_expected()
        self.assertEqual(expected["tag"], TAG)
        self.assertEqual(expected["commit"], COMMIT)


if __name__ == "__main__":
    unittest.main()
