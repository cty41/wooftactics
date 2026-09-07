"""Validate the public-source and asset-provenance boundary.

The default report mode inventories excluded tracked files without failing so
the private development branch can converge incrementally. ``--candidate`` is
the hard publication gate: every excluded file must be absent and every media
file must have an approved, byte-matching provenance entry.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable


SECRET_PATTERNS = (
    re.compile(r"\bgh[pousr]_[A-Za-z0-9]{20,}\b"),
    re.compile(r"\bgithub_pat_[A-Za-z0-9_]{20,}\b"),
    re.compile(r"\bsk-[A-Za-z0-9]{20,}\b"),
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
)
LOCAL_USER_PATH = re.compile(
    r"(?i)\bC:[\\/]Users[\\/](?!(?:<user>|foo|example)(?:[\\/]))[^\\/\s\"']+"
)


@dataclass
class AuditResult:
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    tracked_files: int = 0
    approved_assets: int = 0

    @property
    def ok(self) -> bool:
        return not self.errors


def _matches(path: str, patterns: Iterable[str]) -> bool:
    normalized = path.replace("\\", "/")
    for pattern in patterns:
        normalized_pattern = pattern.replace("\\", "/")
        if fnmatch.fnmatchcase(normalized, normalized_pattern):
            return True
        if normalized_pattern.endswith("/**"):
            prefix = normalized_pattern[:-3]
            if normalized == prefix or normalized.startswith(prefix + "/"):
                return True
    return False


def _tracked_files(root: Path) -> list[str]:
    process = subprocess.run(
        ["git", "ls-files", "-z"],
        cwd=root,
        check=True,
        stdout=subprocess.PIPE,
    )
    return sorted(
        entry.decode("utf-8").replace("\\", "/")
        for entry in process.stdout.split(b"\0")
        if entry
    )


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _read_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def _is_probably_text(path: Path) -> bool:
    if not path.is_file() or path.stat().st_size > 2 * 1024 * 1024:
        return False
    try:
        path.read_text(encoding="utf-8")
        return True
    except (UnicodeDecodeError, OSError):
        return False


def _artwork_states(root: Path) -> dict[str, str]:
    pipeline = root / "Tools/artworks/pipeline"
    states: dict[str, str] = {}

    def record_artifacts(value: object, state: str, *, replace: bool = False) -> None:
        if isinstance(value, dict):
            path = value.get("path")
            if isinstance(path, str):
                normalized = path.replace("\\", "/")
                if replace:
                    states[normalized] = state
                else:
                    states.setdefault(normalized, state)
            for child in value.values():
                record_artifacts(child, state, replace=replace)
        elif isinstance(value, list):
            for child in value:
                record_artifacts(child, state, replace=replace)

    legacy = pipeline / "legacy-assets.json"
    if legacy.is_file():
        for asset in _read_json(legacy).get("assets", []):
            states[asset["path"].replace("\\", "/")] = asset.get("state", "")
    attempts = pipeline / "attempts"
    if attempts.is_dir():
        for attempt_path in attempts.glob("*.json"):
            attempt = _read_json(attempt_path)
            attempt_state = attempt.get("state", "")
            record_artifacts(attempt.get("artifacts", {}), f"attempt-{attempt_state}")
            if attempt_state == "promoted":
                record_artifacts(attempt.get("artifacts", {}).get("promoted", {}), "promoted", replace=True)
    for directory, state in (("pose-guides", "pose-guide"), ("supporting-artifacts", "supporting-artifact")):
        records = pipeline / directory
        if records.is_dir():
            for record_path in records.glob("*.json"):
                record_artifacts(_read_json(record_path), state)
    return states


def audit(root: Path, candidate: bool) -> AuditResult:
    result = AuditResult()
    policy_path = root / "Tools/public-release/public-source-policy.json"
    policy = _read_json(policy_path)
    manifest = _read_json(root / policy["assetManifest"])
    tracked = _tracked_files(root)
    tracked_set = set(tracked)
    result.tracked_files = len(tracked)

    for required in policy["requiredFiles"]:
        if required not in tracked_set and not (root / required).is_file():
            result.errors.append(f"required_file_missing:{required}")

    for prefix in policy["forbiddenPublicPrefixes"]:
        matches = [path for path in tracked if path.startswith(prefix)]
        if matches:
            result.errors.append(f"forbidden_prefix:{prefix}:{len(matches)}")

    excluded = [
        path for path in tracked
        if _matches(path, policy["excludedFromPublicRoot"])
    ]
    if excluded:
        message = f"excluded_files_tracked:{len(excluded)}"
        if candidate:
            result.errors.append(message)
        else:
            result.warnings.append(message)

    entries = manifest.get("entries", [])
    manifest_by_path = {entry["path"].replace("\\", "/"): entry for entry in entries}
    if len(manifest_by_path) != len(entries):
        result.errors.append("asset_manifest_duplicate_path")

    media_extensions = {value.lower() for value in policy["mediaExtensions"]}
    public_media = [
        path for path in tracked
        if Path(path).suffix.lower() in media_extensions
        and not _matches(path, policy["excludedFromPublicRoot"])
    ]
    artwork_states = _artwork_states(root)
    for path in public_media:
        entry = manifest_by_path.get(path)
        if entry is None:
            result.errors.append(f"asset_unregistered:{path}")
            continue
        if entry.get("status") != "approved":
            result.errors.append(f"asset_not_approved:{path}")
        if entry.get("license") not in policy["allowedLicenses"]:
            result.errors.append(f"asset_license_not_allowed:{path}")
        expected_hash = entry.get("sha256", "").lower()
        actual_hash = _sha256(root / path)
        if actual_hash != expected_hash:
            result.errors.append(f"asset_hash_mismatch:{path}")
        else:
            result.approved_assets += 1
        if path.startswith("Tools/artworks/") and not path.startswith("Tools/artworks/pipeline/"):
            state = artwork_states.get(path)
            if state is None:
                result.errors.append(f"artwork_state_unregistered:{path}")
            elif ("/approved/" in path or "/calibrated/" in path) and state not in {"legacy-approved", "promoted"}:
                result.errors.append(f"artwork_formal_state_invalid:{path}:{state}")

    for path in manifest_by_path:
        if path not in tracked_set and not (root / path).is_file():
            result.errors.append(f"asset_manifest_file_missing:{path}")

    for path in tracked:
        if _matches(path, policy["excludedFromPublicRoot"]):
            continue
        absolute = root / path
        if not _is_probably_text(absolute):
            continue
        content = absolute.read_text(encoding="utf-8")
        for pattern in SECRET_PATTERNS:
            if pattern.search(content):
                result.errors.append(f"secret_pattern:{path}:{pattern.pattern}")
        if candidate and LOCAL_USER_PATH.search(content):
            result.errors.append(f"local_user_path:{path}")

    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--candidate", action="store_true")
    args = parser.parse_args()

    root = args.root.resolve()
    result = audit(root, args.candidate)
    payload = {
        "schemaVersion": 1,
        "mode": "candidate" if args.candidate else "report",
        "status": "ok" if result.ok else "failed",
        "trackedFiles": result.tracked_files,
        "approvedAssets": result.approved_assets,
        "errors": result.errors,
        "warnings": result.warnings,
    }
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    return 0 if result.ok else 1


if __name__ == "__main__":
    sys.exit(main())
