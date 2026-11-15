"""Worktree-scoped lock compatible with GodotDevSession mutex naming."""

from __future__ import annotations

import hashlib
import os
from dataclasses import dataclass


@dataclass(frozen=True)
class WorktreeIdentity:
    repo_root: str
    key: str
    user_directory_name: str


@dataclass
class WorktreeLock:
    path: str
    _handle: object

    def release(self) -> None:
        if self._handle is None:
            return
        handle = self._handle
        self._handle = None
        if os.name == "nt":
            import msvcrt

            try:
                handle.seek(0)
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            finally:
                handle.close()
            return
        import fcntl

        try:
            fcntl.flock(handle.fileno(), fcntl.LOCK_UN)
        finally:
            handle.close()


def worktree_lock_key(repo_root: str) -> str:
    resolved = os.path.abspath(repo_root).rstrip(os.sep)
    digest = hashlib.sha256(resolved.lower().encode("utf-8")).hexdigest()
    return digest[:24]


def worktree_identity(repo_root: str) -> WorktreeIdentity:
    resolved = os.path.abspath(repo_root).rstrip(os.sep)
    key = worktree_lock_key(repo_root)
    return WorktreeIdentity(
        repo_root=resolved,
        key=key,
        user_directory_name=f"TacticsGodotDev/{key}",
    )


def acquire_worktree_lock(repo_root: str) -> WorktreeLock:
    key = worktree_lock_key(repo_root)
    lock_dir = os.environ.get("TMPDIR") or os.environ.get("TEMP") or "/tmp"
    lock_path = os.path.join(lock_dir, f"TacticsGodotDev-{key}.lock")
    handle = open(lock_path, "a+", encoding="utf-8")
    try:
        if os.name == "nt":
            import msvcrt

            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl

            fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    except (BlockingIOError, OSError) as exc:
        handle.close()
        raise RuntimeError(
            f"Another build, verification, or Editor launch owns this worktree: {os.path.abspath(repo_root)}"
        ) from exc
    handle.seek(0)
    handle.truncate()
    handle.write(str(os.getpid()))
    handle.flush()
    return WorktreeLock(path=lock_path, _handle=handle)
