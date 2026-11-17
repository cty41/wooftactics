#!/usr/bin/env python3
"""Report drift between the pinned MaLiang submodule, its upstream, and its skill baseline.

Reports only. Bumping the pin is always a human decision.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


ADAPTER_PATH = "Tools/artworks/maliang.adapter.json"


def git(repo: Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(repo), *arguments],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "git command failed")
    return result.stdout.strip()


def check_upstream(vendor: Path, pinned: str, branch: str, fetch: bool) -> list[str]:
    if fetch:
        try:
            git(vendor, "fetch", "origin", branch)
        except RuntimeError as error:
            return [f"上游检查失败：无法 fetch origin/{branch}：{error}"]
    remote = f"refs/remotes/origin/{branch}"
    try:
        head = git(vendor, "rev-parse", remote)
    except RuntimeError:
        return [
            f"上游状态未知：本地没有 {remote}。用 --fetch 联网刷新后重跑，"
            "不要把「未知」当成「已对齐」。"
        ]
    if head == pinned:
        return []
    behind = git(vendor, "rev-list", "--count", f"{pinned}..{head}")
    ahead = git(vendor, "rev-list", "--count", f"{head}..{pinned}")
    if behind == "0":
        return [
            f"固定提交不在 origin/{branch} 上（领先 {ahead} 个提交）：{pinned[:8]}。"
            "确认该提交是否已被上游合并或 rebase 掉。"
        ]
    log = git(vendor, "log", "--oneline", "--no-decorate", "-10", f"{pinned}..{head}")
    lines = [f"上游已更新：固定提交落后 origin/{branch} {behind} 个提交。"]
    lines.append(f"  固定：{pinned}")
    lines.append(f"  上游：{head}")
    lines.extend(f"  {entry}" for entry in log.splitlines())
    lines.append(f"  升级后需同步 {ADAPTER_PATH}、THIRD_PARTY_NOTICES.md 与 vendored asset provenance。")
    return lines


def check_local(vendor: Path) -> list[str]:
    dirty = git(vendor, "status", "--porcelain")
    if not dirty:
        return []
    lines = [
        "子模块内存在未回推的本地改动。这些文件属于 MaLiang 仓库，"
        "应先回推上游并重新 pin，不能只留在本仓："
    ]
    lines.extend(f"  {entry}" for entry in dirty.splitlines())
    return lines


def check_skill(root: Path, vendor: Path, config: dict) -> list[str]:
    declared = config.get("upstreamSkill")
    if not isinstance(declared, dict):
        return ["Skill 基线未声明：adapter 缺少 upstreamSkill，无法检测上游 skill 漂移。"]
    skill = vendor / declared["path"]
    if not skill.is_file():
        return [f"上游 skill 文件缺失：{declared['path']}。"]
    actual = hashlib.sha256(skill.read_bytes()).hexdigest()
    if actual == declared["sha256"]:
        return []
    return [
        f"上游 skill 已更新：{declared['path']}",
        f"  基线：{declared['sha256']}",
        f"  当前：{actual}",
        f"  请人工评估本仓适配版 {declared['localAdaptation']} 是否需要同步，"
        f"确认后把新哈希写回 {ADAPTER_PATH}。",
    ]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--fetch", action="store_true", help="联网刷新 origin（默认离线）")
    args = parser.parse_args()
    root = args.root.resolve()

    config = json.loads((root / ADAPTER_PATH).read_text(encoding="utf-8"))
    engine = config["engine"]
    vendor = root / engine["submodulePath"]
    pinned = engine["commit"]
    branch = engine.get("defaultBranch", "main")

    if not (vendor / ".git").exists():
        print(f"子模块未初始化：{engine['submodulePath']}")
        print("  运行 git submodule update --init 后重跑。")
        return 1

    actual = git(vendor, "rev-parse", "HEAD")
    reports: list[str] = []
    if actual != pinned:
        reports.append(
            f"子模块不在固定提交上：期望 {pinned}，实际 {actual}。"
            "美术管线适配器会因此硬失败。"
        )
    reports.extend(check_local(vendor))
    reports.extend(check_upstream(vendor, pinned, branch, args.fetch))
    reports.extend(check_skill(root, vendor, config))

    if not reports:
        print(f"MaLiang 已对齐：{pinned[:8]} == origin/{branch}，无本地改动，skill 基线一致。")
        return 0
    for line in reports:
        print(line)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
