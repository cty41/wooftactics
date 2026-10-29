import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "check_maliang_drift.py"
SPEC = importlib.util.spec_from_file_location("check_maliang_drift", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)

SKILL_RELATIVE = "skills/maliang-art/SKILL.md"


def _git(repo: Path, *arguments: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *arguments], check=True,
        text=True, stdout=subprocess.PIPE,
    ).stdout.strip()


class MaLiangDriftTests(unittest.TestCase):
    def _fixture(self, root: Path) -> tuple[Path, Path]:
        """Build an upstream repo plus a clone standing in for the submodule."""
        upstream = root / "upstream"
        skill = upstream / SKILL_RELATIVE
        skill.parent.mkdir(parents=True)
        skill.write_text("baseline\n", encoding="utf-8")
        _git(upstream, "init", "-q", "-b", "main")
        _git(upstream, "config", "user.name", "test")
        _git(upstream, "config", "user.email", "test@invalid")
        _git(upstream, "add", ".")
        _git(upstream, "commit", "-qm", "baseline")

        vendor = root / "Tools/vendor/maliang"
        vendor.parent.mkdir(parents=True)
        subprocess.run(
            ["git", "clone", "-q", str(upstream), str(vendor)], check=True,
        )
        _git(vendor, "config", "user.name", "test")
        _git(vendor, "config", "user.email", "test@invalid")
        return upstream, vendor

    def _write_adapter(self, root: Path, vendor: Path, *, skill_hash: str | None = None) -> None:
        import hashlib

        if skill_hash is None:
            skill_hash = hashlib.sha256((vendor / SKILL_RELATIVE).read_bytes()).hexdigest()
        adapter = root / MODULE.ADAPTER_PATH
        adapter.parent.mkdir(parents=True, exist_ok=True)
        adapter.write_text(json.dumps({
            "schemaVersion": 1,
            "engine": {
                "submodulePath": "Tools/vendor/maliang",
                "commit": _git(vendor, "rev-parse", "HEAD"),
                "defaultBranch": "main",
            },
            "upstreamSkill": {
                "path": SKILL_RELATIVE,
                "sha256": skill_hash,
                "localAdaptation": ".agents/skills/pure-run-artwork-pipeline/SKILL.md",
            },
        }), encoding="utf-8")

    def test_reports_no_drift_when_aligned(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _upstream, vendor = self._fixture(root)
            self._write_adapter(root, vendor)

            self.assertEqual(0, self._run(root))
            self.assertIn("已对齐", self._last_output)

    def _run(self, root: Path, *extra: str) -> int:
        result = subprocess.run(
            ["py", str(MODULE_PATH), "--root", str(root), *extra],
            text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        )
        self._last_output = result.stdout
        return result.returncode

    def test_reports_upstream_commits_behind(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            upstream, vendor = self._fixture(root)
            self._write_adapter(root, vendor)
            (upstream / "note.md").write_text("new\n", encoding="utf-8")
            _git(upstream, "add", ".")
            _git(upstream, "commit", "-qm", "upstream change")
            _git(vendor, "fetch", "-q", "origin", "main")

            self.assertEqual(1, self._run(root))
            self.assertIn("落后 origin/main 1 个提交", self._last_output)

    def test_reports_uncommitted_local_changes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _upstream, vendor = self._fixture(root)
            self._write_adapter(root, vendor)
            (vendor / "src-local.py").write_text("local\n", encoding="utf-8")

            self.assertEqual(1, self._run(root))
            self.assertIn("未回推的本地改动", self._last_output)
            self.assertIn("src-local.py", self._last_output)

    def test_reports_upstream_skill_hash_change(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _upstream, vendor = self._fixture(root)
            self._write_adapter(root, vendor, skill_hash="0" * 64)

            self.assertEqual(1, self._run(root))
            self.assertIn("上游 skill 已更新", self._last_output)

    def test_reports_unknown_when_remote_ref_missing(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _upstream, vendor = self._fixture(root)
            self._write_adapter(root, vendor)
            _git(vendor, "update-ref", "-d", "refs/remotes/origin/main")

            self.assertEqual(1, self._run(root))
            self.assertIn("上游状态未知", self._last_output)


if __name__ == "__main__":
    unittest.main()
