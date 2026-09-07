import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "validate_third_party_notices.py"
SPEC = importlib.util.spec_from_file_location("validate_third_party_notices", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class ThirdPartyNoticeTests(unittest.TestCase):
    def test_notice_must_match_manifest_version_and_commit(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = root / "Tools/migration/manifest/godot-tooling.json"
            manifest.parent.mkdir(parents=True)
            manifest.write_text(json.dumps({"godotAi": {
                "tag": "v3.2.5", "commit": "43369d39abcdef"
            }}), encoding="utf-8")
            notice = root / "THIRD_PARTY_NOTICES.md"
            notice.write_text(
                "| Godot AI EditorPlugin | 3.2.5 (`43369d39`) | MIT | Editor tooling |\n",
                encoding="utf-8",
            )
            MODULE.validate(root)
            notice.write_text(
                "| Godot AI EditorPlugin | 3.1.2 (`678b16a6`) | MIT | Editor tooling |\n",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "stale"):
                MODULE.validate(root)


if __name__ == "__main__":
    unittest.main()
