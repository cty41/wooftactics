import hashlib
import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "_maliang_adapter.py"
SPEC = importlib.util.spec_from_file_location("maliang_adapter", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


def git(repo: Path, *arguments: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *arguments],
        check=True,
        text=True,
        stdout=subprocess.PIPE,
    ).stdout.strip()


class PinnedMaliangAdapterTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.repo = Path(self.temporary.name)
        git(self.repo, "init", "-q")
        git(self.repo, "config", "user.name", "test")
        git(self.repo, "config", "user.email", "test@invalid")
        git(self.repo, "config", "core.autocrlf", "false")
        package = self.repo / "src" / "maliang_art"
        package.mkdir(parents=True)
        self.source = package / "__init__.py"
        self.source.write_text('__version__ = "0.1.0"\n', encoding="utf-8")
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "initial")
        self.commit = git(self.repo, "rev-parse", "HEAD")

    def tearDown(self):
        self.temporary.cleanup()

    def test_accepts_exact_clean_checkout(self):
        MODULE._verify_pinned_checkout(self.repo, self.commit)

    def test_rejects_same_version_at_different_commit(self):
        self.source.write_text('__version__ = "0.1.0"\n# weaker implementation\n', encoding="utf-8")
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "different implementation")

        with self.assertRaisesRegex(RuntimeError, "not at pinned commit"):
            MODULE._verify_pinned_checkout(self.repo, self.commit)

    def test_rejects_dirty_bytes_hidden_from_status_policy(self):
        self.source.write_text('__version__ = "0.1.0"\n# dirty implementation\n', encoding="utf-8")

        with self.assertRaisesRegex(RuntimeError, "differs from commit"):
            MODULE._verify_pinned_checkout(self.repo, self.commit)

    def test_rejects_untracked_runtime_content(self):
        (self.source.parent / "injected.py").write_text("ENABLED = True\n", encoding="utf-8")

        with self.assertRaisesRegex(RuntimeError, "untracked content"):
            MODULE._verify_pinned_checkout(self.repo, self.commit)

    def test_accepts_only_manifest_bound_flattened_rc_snapshot(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            checkout = root / "Tools" / "vendor" / "maliang"
            checkout.mkdir(parents=True)
            payload = checkout / "payload.txt"
            payload.write_bytes(b"pinned payload")
            digest = hashlib.sha256(payload.read_bytes()).hexdigest()
            pinned_commit = "a" * 40
            manifest = {
                "schemaVersion": 1, "sourceCommit": "c" * 40,
                "boundary": "public-source-byte-identical-v1",
                "expandedGitlinks": [{"path": "Tools/vendor/maliang", "commit": pinned_commit}],
                "files": [{
                    "path": "Tools/vendor/maliang/payload.txt", "sourceSha256": digest,
                    "stagedSha256": digest, "sourceRepositoryCommit": pinned_commit,
                    "sourceObject": "b" * 40,
                }],
            }
            (root / "rc-source-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
            git(root, "init", "-q"); git(root, "config", "user.name", "test")
            git(root, "config", "user.email", "test@invalid"); git(root, "add", ".")
            git(root, "commit", "-qm", "RC source snapshot")
            previous_root = MODULE.ROOT
            MODULE.ROOT = root
            try:
                MODULE._verify_pinned_checkout(checkout, pinned_commit)
                payload.write_bytes(b"tampered payload")
                with self.assertRaisesRegex(RuntimeError, "differs from staged provenance"):
                    MODULE._verify_pinned_checkout(checkout, pinned_commit)
            finally:
                MODULE.ROOT = previous_root


if __name__ == "__main__":
    unittest.main()
