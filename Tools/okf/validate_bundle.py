#!/usr/bin/env python3
"""Validate the Tactics OKF bundle and its project-specific profile."""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from datetime import date, datetime
from pathlib import Path
from urllib.parse import unquote, urlsplit

import yaml


RESERVED_NAMES = {"index.md", "log.md"}
RESERVED_PREFIXES = ("log-archive-",)


def _is_reserved(name: str) -> bool:
    return name in RESERVED_NAMES or name.startswith(RESERVED_PREFIXES)


CATALOG_MAP_NAME = "catalog-scopes.yaml"
REQUIRED_FIELDS = ("type", "title", "description", "timestamp")
VALID_STATUSES = {"draft", "active", "superseded", "archived"}
IMPLEMENTATION_TYPES = {
    "Project Architecture",
    "Game System",
    "Development Plan",
    "Operational Playbook",
    "Test Evidence",
}
LINK_PATTERN = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")
LOG_DATE_PATTERN = re.compile(r"^## (\d{4}-\d{2}-\d{2})\s*$", re.MULTILINE)


@dataclass(frozen=True)
class MarkdownDocument:
    path: Path
    frontmatter: dict[str, object]
    body: str
    has_frontmatter: bool


def _relative(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return str(path)


def _parse_document(path: Path, bundle_root: Path) -> tuple[MarkdownDocument | None, list[str]]:
    errors: list[str] = []
    rel = _relative(path, bundle_root)
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        return None, [f"{rel}: 文件不是有效 UTF-8：{exc}"]

    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return MarkdownDocument(path, {}, text, False), errors

    end_index = next((index for index in range(1, len(lines)) if lines[index].strip() == "---"), None)
    if end_index is None:
        return None, [f"{rel}: frontmatter 缺少结束分隔符"]

    yaml_text = "\n".join(lines[1:end_index])
    try:
        frontmatter = yaml.safe_load(yaml_text) or {}
    except yaml.YAMLError as exc:
        return None, [f"{rel}: YAML 解析失败：{exc}"]

    if not isinstance(frontmatter, dict):
        return None, [f"{rel}: frontmatter 必须是 YAML mapping"]

    body = "\n".join(lines[end_index + 1 :]).lstrip("\n")
    return MarkdownDocument(path, frontmatter, body, True), errors


def _timestamp_is_valid(value: object) -> bool:
    if isinstance(value, (datetime, date)):
        return True
    if not isinstance(value, str) or not value.strip():
        return False
    try:
        datetime.fromisoformat(value.strip().replace("Z", "+00:00"))
    except ValueError:
        return False
    return True


def _resolve_internal_link(source: Path, raw_target: str, bundle_root: Path) -> Path | None:
    target = raw_target.strip().strip("<>")
    if not target or target.startswith("#"):
        return None

    split = urlsplit(target)
    if split.scheme or split.netloc:
        return None

    path_text = unquote(split.path)
    if not path_text:
        return None

    if path_text.startswith("/"):
        resolved = bundle_root / path_text.lstrip("/")
    else:
        resolved = source.parent / path_text

    if path_text.endswith("/"):
        resolved = resolved / "index.md"
    return resolved.resolve()


def _linked_targets(document: MarkdownDocument, bundle_root: Path) -> set[Path]:
    targets: set[Path] = set()
    for match in LINK_PATTERN.finditer(document.body):
        target = _resolve_internal_link(document.path, match.group(1), bundle_root)
        if target is not None:
            targets.add(target)
    return targets


def _validate_concept(
    document: MarkdownDocument,
    bundle_root: Path,
    repo_root: Path,
    profile_version: str,
    allowed_missing_repo_prefixes: tuple[str, ...],
) -> list[str]:
    errors: list[str] = []
    rel = _relative(document.path, bundle_root)
    if not document.has_frontmatter:
        return [f"{rel}: OKF 概念缺少 YAML frontmatter"]

    for field in REQUIRED_FIELDS:
        value = document.frontmatter.get(field)
        if value is None or (isinstance(value, str) and not value.strip()):
            errors.append(f"{rel}: 缺少 Tactics Profile 必填字段 {field}")

    if "timestamp" in document.frontmatter and not _timestamp_is_valid(document.frontmatter["timestamp"]):
        errors.append(f"{rel}: timestamp 不是有效 ISO 8601 时间")

    status = document.frontmatter.get("status")
    if status not in VALID_STATUSES:
        errors.append(f"{rel}: status 必须是 {sorted(VALID_STATUSES)} 之一")

    concept_type = document.frontmatter.get("type")
    if concept_type in IMPLEMENTATION_TYPES:
        repo_paths = document.frontmatter.get("repo_paths")
        if not isinstance(repo_paths, list) or not repo_paths:
            errors.append(f"{rel}: {concept_type} 必须包含非空 repo_paths")
        else:
            for raw_path in repo_paths:
                if not isinstance(raw_path, str) or not raw_path.strip():
                    errors.append(f"{rel}: repo_paths 只能包含非空字符串")
                    continue
                normalized_path = raw_path.replace("\\", "/").strip("/")
                missing_is_allowed = any(
                    normalized_path == prefix or normalized_path.startswith(f"{prefix}/")
                    for prefix in allowed_missing_repo_prefixes
                )
                if not (repo_root / raw_path).exists() and not missing_is_allowed:
                    errors.append(f"{rel}: repo_path 不存在：{raw_path}")
        if profile_version == "0.1":
            revision = document.frontmatter.get("verified_revision")
            if not isinstance(revision, str) or not revision.strip():
                errors.append(f"{rel}: {concept_type} 必须包含 verified_revision")
        else:
            fingerprint = document.frontmatter.get("source_fingerprint")
            if not isinstance(fingerprint, str) or not re.fullmatch(r"sha256:[0-9a-f]{64}", fingerprint):
                errors.append(f"{rel}: {concept_type} 必须包含有效 source_fingerprint")

    if status == "superseded":
        replacement = document.frontmatter.get("superseded_by")
        if not isinstance(replacement, str) or not replacement.strip():
            errors.append(f"{rel}: superseded 概念必须包含 superseded_by")
        else:
            replacement_path = replacement if replacement.endswith(".md") else f"{replacement}.md"
            target = (bundle_root / replacement_path.lstrip("/")).resolve()
            if not target.exists():
                errors.append(f"{rel}: superseded_by 目标不存在：{replacement}")

    return errors


def _validate_catalog_map(
    bundle_root: Path,
    documents: dict[Path, MarkdownDocument],
    implementation_scopes: dict[str, Path],
) -> list[str]:
    errors: list[str] = []
    map_path = bundle_root / CATALOG_MAP_NAME
    if not map_path.is_file():
        return [f"缺少 {CATALOG_MAP_NAME}"]

    try:
        payload = yaml.safe_load(map_path.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as exc:
        return [f"{CATALOG_MAP_NAME}: YAML 解析失败：{exc}"]

    if not isinstance(payload, dict):
        return [f"{CATALOG_MAP_NAME}: 根节点必须是 YAML mapping"]
    if payload.get("version") != 1:
        errors.append(f"{CATALOG_MAP_NAME}: version 必须是 1")

    for field in ("tracked_roots", "ignored_paths"):
        value = payload.get(field)
        if not isinstance(value, list) or any(not isinstance(item, str) or not item.strip() for item in value):
            errors.append(f"{CATALOG_MAP_NAME}: {field} 必须是字符串列表")

    scopes = payload.get("scopes")
    if not isinstance(scopes, dict):
        return errors + [f"{CATALOG_MAP_NAME}: scopes 必须是 YAML mapping"]

    mapped_scopes: set[str] = set()
    for raw_scope, raw_config in scopes.items():
        if not isinstance(raw_scope, str) or not raw_scope.strip() or not isinstance(raw_config, dict):
            errors.append(f"{CATALOG_MAP_NAME}: scope 配置无效：{raw_scope!r}")
            continue
        scope = raw_scope.strip()
        mapped_scopes.add(scope)
        concept = raw_config.get("concept")
        patterns = raw_config.get("paths")
        if not isinstance(concept, str) or not concept.strip():
            errors.append(f"{CATALOG_MAP_NAME}: {scope}.concept 必须是非空字符串")
        else:
            concept_path = (bundle_root / concept).resolve()
            document = documents.get(concept_path)
            if document is None:
                errors.append(f"{CATALOG_MAP_NAME}: {scope}.concept 不存在：{concept}")
            elif document.frontmatter.get("catalog_scope") != scope:
                errors.append(f"{CATALOG_MAP_NAME}: {scope}.concept 的 catalog_scope 不匹配")
        if not isinstance(patterns, list) or not patterns:
            errors.append(f"{CATALOG_MAP_NAME}: {scope}.paths 必须是非空字符串列表")
        elif any(not isinstance(pattern, str) or not pattern.strip() for pattern in patterns):
            errors.append(f"{CATALOG_MAP_NAME}: {scope}.paths 只能包含非空字符串")

    for scope, concept_path in implementation_scopes.items():
        if scope not in mapped_scopes:
            errors.append(
                f"{_relative(concept_path, bundle_root)}: 实现型概念未在 {CATALOG_MAP_NAME} 中登记"
            )
    return errors


def validate_bundle(
    bundle_root: Path,
    repo_root: Path,
    allowed_missing_repo_prefixes: tuple[str, ...] = (),
) -> list[str]:
    bundle_root = bundle_root.resolve()
    repo_root = repo_root.resolve()
    errors: list[str] = []

    if not bundle_root.is_dir():
        return [f"Bundle 目录不存在：{bundle_root}"]

    documents: dict[Path, MarkdownDocument] = {}
    for path in sorted(bundle_root.rglob("*.md")):
        document, parse_errors = _parse_document(path, bundle_root)
        errors.extend(parse_errors)
        if document is not None:
            documents[path.resolve()] = document

    root_index_path = (bundle_root / "index.md").resolve()
    root_log_path = (bundle_root / "log.md").resolve()
    if root_index_path not in documents:
        errors.append("根 index.md 不存在")
    if root_log_path not in documents:
        errors.append("根 log.md 不存在")

    root_document = documents.get(root_index_path)
    profile_version = str(root_document.frontmatter.get("tactics_profile")) if root_document else ""
    active_scopes: dict[str, str] = {}
    implementation_scopes: dict[str, Path] = {}
    for path, document in documents.items():
        if not _is_reserved(path.name):
            errors.extend(
                _validate_concept(
                    document,
                    bundle_root,
                    repo_root,
                    profile_version,
                    allowed_missing_repo_prefixes,
                )
            )
            status = document.frontmatter.get("status")
            scope = document.frontmatter.get("catalog_scope")
            if status == "active" and isinstance(scope, str) and scope.strip():
                if scope in active_scopes:
                    errors.append(
                        f"{_relative(path, bundle_root)}: active catalog_scope '{scope}' "
                        f"与 {active_scopes[scope]} 重复"
                    )
                else:
                    active_scopes[scope] = _relative(path, bundle_root)
                    if document.frontmatter.get("type") in IMPLEMENTATION_TYPES:
                        implementation_scopes[scope] = path
        elif path.name == "index.md":
            if path == root_index_path:
                if not document.has_frontmatter:
                    errors.append("根 index.md 必须声明 okf_version 和 tactics_profile")
                else:
                    if str(document.frontmatter.get("okf_version")) != "0.1":
                        errors.append("根 index.md 的 okf_version 必须是 0.1")
                    if str(document.frontmatter.get("tactics_profile")) not in {"0.1", "0.2"}:
                        errors.append("根 index.md 的 tactics_profile 必须是 0.1 或 0.2")
            elif document.has_frontmatter:
                errors.append(f"{_relative(path, bundle_root)}: 子目录 index.md 不应包含 frontmatter")
        elif document.has_frontmatter:
            errors.append(f"{_relative(path, bundle_root)}: log.md 不应包含 frontmatter")

    for path, document in documents.items():
        for target in _linked_targets(document, bundle_root):
            try:
                target.relative_to(bundle_root)
            except ValueError:
                errors.append(
                    f"{_relative(path, bundle_root)}: 内部链接越出 bundle：{_relative(target, repo_root)}"
                )
                continue
            if not target.exists():
                errors.append(
                    f"{_relative(path, bundle_root)}: 内部链接目标不存在：{_relative(target, bundle_root)}"
                )

    for path, document in documents.items():
        if _is_reserved(path.name):
            continue
        parent_index = (path.parent / "index.md").resolve()
        index_document = documents.get(parent_index)
        if index_document is None:
            errors.append(f"{_relative(path, bundle_root)}: 所在目录缺少 index.md")
        elif path not in _linked_targets(index_document, bundle_root):
            errors.append(f"{_relative(path, bundle_root)}: 未被所在目录 index.md 收录")

    indexed_directories = {path.parent.resolve() for path in documents if path.name == "index.md"}
    for directory in sorted(indexed_directories):
        if directory == bundle_root:
            continue
        parent_index = (directory.parent / "index.md").resolve()
        index_document = documents.get(parent_index)
        child_index = (directory / "index.md").resolve()
        if index_document is None or child_index not in _linked_targets(index_document, bundle_root):
            errors.append(f"{_relative(directory, bundle_root)}/index.md: 未被父级 index.md 收录")

    log_document = documents.get(root_log_path)
    if log_document is not None:
        headings = LOG_DATE_PATTERN.findall(log_document.body)
        parsed_dates = [date.fromisoformat(value) for value in headings]
        if parsed_dates != sorted(parsed_dates, reverse=True):
            errors.append("根 log.md 日期必须按从新到旧排列")

    if (bundle_root / CATALOG_MAP_NAME).is_file():
        errors.extend(_validate_catalog_map(bundle_root, documents, implementation_scopes))

    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description="校验 Tactics OKF v0.1 bundle 与 Tactics Profile")
    parser.add_argument("--bundle", type=Path, default=Path(".agents/knowledge"))
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument(
        "--allow-missing-repo-prefix",
        action="append",
        default=[],
        help="Allow missing repo_paths under an intentionally excluded repository prefix.",
    )
    args = parser.parse_args()

    allowed_prefixes = tuple(
        prefix.replace("\\", "/").strip("/")
        for prefix in args.allow_missing_repo_prefix
        if prefix.strip("/\\")
    )
    errors = validate_bundle(args.bundle, args.repo_root, allowed_prefixes)
    if errors:
        print(f"OKF_CHECK_FAILED errors={len(errors)}")
        for error in errors:
            print(f"- {error}")
        return 1

    concept_count = sum(
        1
        for path in args.bundle.rglob("*.md")
        if not _is_reserved(path.name)
    )
    print(f"OKF_CHECK_OK concepts={concept_count} bundle={args.bundle.as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
