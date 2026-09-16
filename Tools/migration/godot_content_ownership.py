"""Compile and validate the canonical Godot content ownership receipt."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CATEGORIES = ROOT / "Tools/migration/manifest/asset-categories.json"
CATALOG = ROOT / "godot/content/ContentCatalog.tres"
OUTPUT = ROOT / "Tools/migration/manifest/ownership/godot-content-ownership-v1.json"


def normalized_text_sha256(path: Path) -> str:
    """Hash tracked text independently of the checkout's line-ending policy."""
    normalized = path.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")
    return "sha256:" + hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def parse_catalog() -> list[dict[str, str]]:
    text = CATALOG.read_text(encoding="utf-8")
    blocks = text.split('[sub_resource type="Resource"')[1:]
    entries: list[dict[str, str]] = []
    for block in blocks:
        content = re.search(r'^ContentIdValue = "([^"]+)"', block, re.MULTILINE)
        if content is None:
            continue
        resource_type = re.search(r'^ResourceTypeIdValue = "([^"]+)"', block, re.MULTILINE)
        diagnostic = re.search(r'^DiagnosticPathValue = "([^"]+)"', block, re.MULTILINE)
        if resource_type is None or diagnostic is None:
            raise ValueError(f"Incomplete catalog entry: {content.group(1)}")
        entries.append(
            {
                "contentId": content.group(1),
                "resourceType": resource_type.group(1),
                "resourcePath": diagnostic.group(1),
            }
        )
    return entries


def compile_receipt() -> dict[str, object]:
    category_document = json.loads(CATEGORIES.read_text(encoding="utf-8"))
    categories = category_document["categories"]
    if any(category["owner"] != "GodotOwned" for category in categories):
        raise ValueError("Every current content category must be GodotOwned")
    entries = parse_catalog()
    if len(entries) != 185:
        raise ValueError(f"Canonical Catalog must contain 185 entries, got {len(entries)}")
    ids = [entry["contentId"] for entry in entries]
    if len(ids) != len(set(ids)):
        raise ValueError("Canonical Catalog contains duplicate ContentId values")
    for entry in entries:
        path = entry["resourcePath"]
        if not path.startswith("res://"):
            raise ValueError(f"Catalog path is not res:// based: {path}")
        local = ROOT / "godot" / path.removeprefix("res://")
        if not local.is_file():
            raise ValueError(f"Catalog resource is missing: {path}")
    type_counts = Counter(entry["resourceType"] for entry in entries)
    identity_payload = "\n".join(
        f'{entry["contentId"]}|{entry["resourceType"]}|{entry["resourcePath"]}'
        for entry in sorted(entries, key=lambda item: item["contentId"])
    ).encode("utf-8")
    return {
        "schemaVersion": 1,
        "receiptId": "godot-content-ownership-v1",
        "ownership": "GodotOwned",
        "catalogCount": len(entries),
        "catalogSha256": normalized_text_sha256(CATALOG),
        "catalogSemanticSha256": "sha256:" + hashlib.sha256(identity_payload).hexdigest(),
        "resourceTypeCounts": dict(sorted(type_counts.items())),
        "categories": categories,
        "supersedesGenerationState": sorted(
            path.name for path in (ROOT / "Tools/migration/manifest/state").glob("*.json")
        ),
        "historicalExportReceiptsPreserved": True,
        "manualAcceptance": "pending_separate_quality_gate",
        "payloadBoundary": {
            "audioPayloadIncluded": False,
            "thirdPartyUnityPayloadIncluded": False,
            "unityPlayerPrefsImportIncluded": False,
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    arguments = parser.parse_args()
    receipt = compile_receipt()
    payload = json.dumps(receipt, ensure_ascii=False, indent=2) + "\n"
    if arguments.check:
        if not OUTPUT.is_file() or OUTPUT.read_text(encoding="utf-8") != payload:
            raise SystemExit("Godot ownership receipt is stale")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_text(payload, encoding="utf-8", newline="\n")
    print(f'GODOT_CONTENT_OWNERSHIP_OK catalog={receipt["catalogCount"]}')


if __name__ == "__main__":
    main()
