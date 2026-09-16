"""Load the pinned MaLiang source and Pure Run adapter policy."""
from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path
from typing import Any

# Keep flattened release-candidate snapshots byte-identical across repeated imports.
sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[4]
CONFIG_PATH = ROOT / "Tools" / "artworks" / "maliang.adapter.json"


def load_config() -> dict[str, Any]:
    config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    if config.get("schemaVersion") != 1:
        raise RuntimeError("unsupported MaLiang adapter schemaVersion")
    engine = config.get("engine")
    if not isinstance(engine, dict) or engine.get("submodulePath") != "Tools/vendor/maliang":
        raise RuntimeError("invalid MaLiang adapter engine configuration")
    commit = engine.get("commit")
    if not isinstance(commit, str) or len(commit) != 40 or any(char not in "0123456789abcdef" for char in commit):
        raise RuntimeError("invalid MaLiang pinned commit")
    return config


def _git(repo: Path, *arguments: str) -> bytes:
    result = subprocess.run(
        ["git", "-C", str(repo), *arguments],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(f"unable to verify pinned MaLiang checkout: {detail}")
    return result.stdout


def _verify_expanded_snapshot(repo: Path, expected_commit: str) -> None:
    manifest_path = ROOT / "rc-source-manifest.json"
    if not manifest_path.is_file():
        raise RuntimeError("flattened MaLiang checkout lacks the RC source manifest")
    manifest_blob = _git(ROOT, "rev-parse", "HEAD:rc-source-manifest.json").decode("ascii").strip()
    manifest_data = manifest_path.read_bytes()
    actual_manifest_blob = hashlib.sha1(
        f"blob {len(manifest_data)}\0".encode("ascii") + manifest_data
    ).hexdigest()
    if actual_manifest_blob != manifest_blob:
        raise RuntimeError("RC source manifest differs from its staging commit")
    manifest = json.loads(manifest_data.decode("utf-8-sig"))
    if manifest.get("boundary") != "public-source-byte-identical-v1":
        raise RuntimeError("flattened MaLiang checkout has an invalid RC source boundary")
    submodule_path = repo.relative_to(ROOT).as_posix()
    links = {item.get("path"): item.get("commit") for item in manifest.get("expandedGitlinks", [])}
    if links.get(submodule_path) != expected_commit:
        raise RuntimeError(f"flattened MaLiang checkout is not bound to pinned commit {expected_commit}")
    prefix = f"{submodule_path}/"
    entries = {
        item.get("path"): item for item in manifest.get("files", [])
        if isinstance(item, dict) and str(item.get("path", "")).startswith(prefix)
    }
    actual_paths = {
        path.relative_to(ROOT).as_posix() for path in repo.rglob("*") if path.is_file()
    }
    if actual_paths != set(entries):
        raise RuntimeError("flattened MaLiang checkout file set differs from the RC source manifest")
    for relative, item in entries.items():
        if (item.get("sourceRepositoryCommit") != expected_commit
                or not isinstance(item.get("sourceObject"), str)
                or len(item["sourceObject"]) != 40
                or item.get("sourceSha256") != item.get("stagedSha256")):
            raise RuntimeError(f"flattened MaLiang provenance is invalid: {relative}")
        actual_sha256 = hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()
        if actual_sha256 != item.get("stagedSha256"):
            raise RuntimeError(f"flattened MaLiang file differs from staged provenance: {relative}")


def _verify_pinned_checkout(repo: Path, expected_commit: str) -> None:
    git_root = Path(_git(repo, "rev-parse", "--show-toplevel").decode("utf-8").strip()).resolve()
    if git_root != repo.resolve():
        if git_root != ROOT.resolve():
            raise RuntimeError(f"MaLiang checkout resolves through an unexpected repository: {git_root}")
        _verify_expanded_snapshot(repo, expected_commit)
        return
    head = _git(repo, "rev-parse", "HEAD").decode("ascii").strip()
    if head != expected_commit:
        raise RuntimeError(f"MaLiang checkout is not at pinned commit {expected_commit}: {head}")
    tree = _git(repo, "ls-tree", "-r", "-z", head)
    for raw_entry in tree.split(b"\0"):
        if not raw_entry:
            continue
        metadata, raw_path = raw_entry.split(b"\t", 1)
        _mode, object_type, expected_blob = metadata.decode("ascii").split()
        if object_type != "blob":
            continue
        relative = raw_path.decode("utf-8")
        path = repo / relative
        if not path.is_file():
            raise RuntimeError(f"pinned MaLiang file is missing: {relative}")
        data = path.read_bytes()
        actual_blob = hashlib.sha1(f"blob {len(data)}\0".encode("ascii") + data).hexdigest()
        if actual_blob != expected_blob:
            raise RuntimeError(f"pinned MaLiang file differs from commit: {relative}")
    untracked = _git(repo, "ls-files", "--others", "--exclude-standard", "-z")
    if untracked:
        relative = untracked.split(b"\0", 1)[0].decode("utf-8", errors="replace")
        raise RuntimeError(f"pinned MaLiang checkout contains untracked content: {relative}")


def activate() -> dict[str, Any]:
    config = load_config()
    checkout = ROOT / config["engine"]["submodulePath"]
    _verify_pinned_checkout(checkout, config["engine"]["commit"])
    source = checkout / "src"
    if not (source / "maliang_art" / "__init__.py").is_file():
        raise RuntimeError("pinned MaLiang submodule is missing; run git submodule update --init")
    source_text = str(source)
    if source_text not in sys.path:
        sys.path.insert(0, source_text)
    import maliang_art
    imported = Path(maliang_art.__file__).resolve()
    if not imported.is_relative_to(source.resolve()):
        raise RuntimeError(f"MaLiang resolved outside the pinned submodule: {imported}")
    if maliang_art.__version__ != config["engine"]["version"]:
        raise RuntimeError(
            f"MaLiang adapter requires {config['engine']['version']}, found {maliang_art.__version__}"
        )
    return config
