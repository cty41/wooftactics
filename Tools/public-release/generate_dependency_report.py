#!/usr/bin/env python3
"""Generate a deterministic dependency inventory for the public source tree."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def collect_nuget(root: Path) -> list[dict[str, str]]:
    dependencies: set[tuple[str, str]] = set()
    for lock_path in sorted(root.glob("**/packages.lock.json")):
        if any(part in {"bin", "obj", ".godot"} for part in lock_path.parts):
            continue
        document = json.loads(lock_path.read_text(encoding="utf-8"))
        for framework in document.get("dependencies", {}).values():
            for name, details in framework.items():
                version = str(details.get("resolved", details.get("requested", "unknown")))
                dependencies.add((name, version))
    return [
        {"ecosystem": "NuGet", "name": name, "version": version}
        for name, version in sorted(dependencies, key=lambda item: (item[0].lower(), item[1]))
    ]


def collect_npm(root: Path) -> list[dict[str, str]]:
    lock_path = root / "Tools" / "gameplay-test-spec" / "package-lock.json"
    if not lock_path.is_file():
        return []
    document = json.loads(lock_path.read_text(encoding="utf-8"))
    dependencies: set[tuple[str, str]] = set()
    for path, details in document.get("packages", {}).items():
        if not path.startswith("node_modules/") or "/node_modules/" in path:
            continue
        name = path.removeprefix("node_modules/")
        dependencies.add((name, str(details.get("version", "unknown"))))
    return [
        {"ecosystem": "npm", "name": name, "version": version}
        for name, version in sorted(dependencies, key=lambda item: (item[0].lower(), item[1]))
    ]


def collect_vendored(root: Path) -> list[dict[str, str]]:
    dependencies: list[dict[str, str]] = []

    manifest_path = root / "Tools" / "migration" / "manifest" / "godot-tooling.json"
    if manifest_path.is_file():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        godot_ai = manifest.get("godotAi", {})
        vendor_path = root / str(godot_ai.get("vendorPath", ""))
        if vendor_path.is_dir():
            dependencies.append({
                "ecosystem": "Vendored",
                "name": "godot-ai",
                "version": str(godot_ai.get("tag", "unknown")).removeprefix("v"),
            })

    adapter_path = root / "Tools" / "artworks" / "maliang.adapter.json"
    if adapter_path.is_file():
        adapter = json.loads(adapter_path.read_text(encoding="utf-8"))
        engine = adapter.get("engine", {})
        vendor_path = root / str(engine.get("submodulePath", ""))
        if vendor_path.is_dir():
            dependencies.append({
                "ecosystem": "Vendored",
                "name": "maliang-game-art",
                "version": str(engine.get("version", "unknown")),
            })

    return dependencies


def build_report(root: Path) -> dict[str, object]:
    dependencies = collect_nuget(root) + collect_npm(root) + collect_vendored(root)
    dependencies.sort(key=lambda item: (item["ecosystem"], item["name"].lower(), item["version"]))
    return {
        "schemaVersion": 1,
        "documentType": "tactics-public-dependency-inventory",
        "dependencyCount": len(dependencies),
        "dependencies": dependencies,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    output = (args.output or root / "Tools" / "public-release" / "dependency-inventory.json").resolve()
    rendered = json.dumps(build_report(root), ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if not output.is_file() or output.read_text(encoding="utf-8") != rendered:
            print(f"Dependency inventory is stale: {output}")
            return 1
        print(f"Dependency inventory is current: {output}")
        return 0
    output.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"Wrote dependency inventory: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
