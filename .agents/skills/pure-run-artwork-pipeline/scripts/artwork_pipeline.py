#!/usr/bin/env python3
"""Deterministic contract state machine for Pure Run artwork.

Image generation deliberately remains outside this program.  This CLI records
the immutable inputs and enforces every transition from ingestion to promotion.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageDraw

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))
import artwork_review


SCHEMA_VERSION = 3
ART_DIRECTION_SCHEMA_VERSION = 4
SUPPORTED_SCHEMA_VERSIONS = {1, 2, 3, 4}
PIPELINE_REL = Path("Tools/artworks/pipeline")
RETIRED_ASSET_PREFIXES = {
    "Tools/artworks/amazon/": "retirement-human-pixel-amazon-20260903",
}
KINDS = {"ground_character", "flying_character", "action_pose", "death_pose", "projectile", "tile"}
STATES = {
    "ready", "ingested", "prepared", "annotated", "calibrated", "review_pending",
    "model_review_pending", "model_reviewed", "human_review_required",
    "approved", "rejected", "promoted", "technical_failed",
}
SERIES_STATES = {"pending", "active", "review_pending", "approved", "promoted", "exhausted", "provisional"}
FEEDBACK_VERDICTS = {"selected", "backup", "retry", "technical_failed", "exhausted"}
FEEDBACK_CATEGORIES = {
    "identity", "core_geometry", "pose_axis", "gaze", "topology", "occlusion",
    "equipment_state", "equipment_scale", "chroma", "processing",
}
FORMAL_DIRS = {"approved", "calibrated"}
MASK_COLORS = {
    "core": (255, 0, 0, 255),
    "head_appendage": (255, 255, 0, 255),
    "near_hand": (0, 255, 0, 255),
    "far_hand": (0, 200, 0, 255),
    "near_foot": (0, 128, 255, 255),
    "far_foot": (0, 0, 255, 255),
    "equipment": (255, 0, 255, 255),
    "wings": (0, 255, 255, 255),
    "effect": (255, 128, 0, 255),
    # These labels are intentionally forbidden for capsule characters whose
    # four paws attach directly to the core.  They make an invented arm/leg
    # reviewable and mechanically rejectable rather than prompt-only.
    "near_arm": (128, 0, 255, 255),
    "far_arm": (96, 0, 192, 255),
    "near_leg": (128, 128, 0, 255),
    "far_leg": (96, 96, 0, 255),
}
IDENTITY_MASK_COLORS = {
    "forehead_blaze": (255, 255, 255, 255),
    "alternate_ear": (0, 255, 255, 255),
    "alternate_coat": (255, 128, 0, 255),
}
OCCLUSION_LABELS = {"near_hand", "far_hand", "equipment"}
WAIVABLE_GATE_ISSUES = {"core_size_out_of_tolerance"}
ASSET_ROLES = {"component", "assembled_sprite"}
COMPONENT_KINDS = {"body", "equipment", "paw_overlay", "foot_overlay", "death_expression_overlay"}
SOURCE_MODES = {"generated", "derived", "pre_v3_import"}
EQUIPMENT_CATEGORIES = {"weapon", "shield", "armor", "jewelry", "consumable"}
LOCAL_REFERENCE_ROLES = {"shape_reference", "style_reference", "material_reference"}
ASSEMBLY_CANONICAL_LAYER_ORDER = (
    "far_foot_overlay", "far_paw_overlay", "body", "equipment",
    "near_paw_overlay", "near_foot_overlay",
)
ASSEMBLY_LAYER_ROLES = set(ASSEMBLY_CANONICAL_LAYER_ORDER)


class PipelineError(RuntimeError):
    pass


def canonical_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def pretty_bytes(value: Any) -> bytes:
    # Record identity uses canonical_bytes(). Human-facing registries preserve
    # insertion order so appending one provenance item does not rewrite every
    # pre-existing object's field order.
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def bound_input_hash_matches(path: Path, expected: str | None) -> bool:
    """Keep binary inputs byte-exact while accepting text-only LF/CRLF checkout normalization."""
    if not expected or not path.is_file():
        return False
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() == expected:
        return True
    if path.suffix.lower() not in {".md", ".txt", ".json", ".yaml", ".yml"}:
        return False
    lf = raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    crlf = lf.replace(b"\n", b"\r\n")
    return expected in {hashlib.sha256(lf).hexdigest(), hashlib.sha256(crlf).hexdigest()}


def pixel_data(image: Image.Image) -> list[tuple[int, int, int, int]]:
    getter = getattr(image, "get_flattened_data", image.getdata)
    return list(getter())


def stable_id(prefix: str, payload: Any, length: int = 16) -> str:
    return f"{prefix}-{hashlib.sha256(canonical_bytes(payload)).hexdigest()[:length]}"


def write_json_idempotent(path: Path, value: Any, immutable: bool = False) -> bool:
    data = pretty_bytes(value)
    if path.exists():
        if path.read_bytes() == data:
            return False
        if immutable:
            raise PipelineError(f"immutable record differs: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp")
    temporary.write_bytes(data)
    os.replace(temporary, path)
    return True


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise PipelineError(f"cannot read JSON {path}: {exc}") from exc
    if not isinstance(value, dict) or value.get("schemaVersion") not in SUPPORTED_SCHEMA_VERSIONS:
        raise PipelineError(f"unsupported or missing schemaVersion in {path}")
    return value


@dataclass(frozen=True)
class Store:
    root: Path

    @property
    def pipeline(self) -> Path:
        return self.root / PIPELINE_REL

    def relative(self, path: Path | str, *, must_exist: bool = False) -> str:
        candidate = Path(path)
        if not candidate.is_absolute():
            candidate = self.root / candidate
        candidate = candidate.resolve()
        try:
            rel = candidate.relative_to(self.root.resolve())
        except ValueError as exc:
            raise PipelineError(f"path escapes repository root: {path}") from exc
        relative_path = rel.as_posix()
        retired = next((receipt for prefix, receipt in RETIRED_ASSET_PREFIXES.items()
                        if relative_path.startswith(prefix)), None)
        if retired:
            raise PipelineError(f"retired asset family is forbidden ({retired}): {relative_path}")
        if must_exist and not candidate.is_file():
            raise PipelineError(f"file does not exist: {relative_path}")
        return relative_path

    def absolute(self, rel: str, *, must_exist: bool = False) -> Path:
        self.relative(rel, must_exist=must_exist)
        return (self.root / rel).resolve()

    def record(self, group: str, record_id: str) -> Path:
        return self.pipeline / group / f"{record_id}.json"


def _source_json_artifact(store: Store, source: str) -> tuple[dict[str, Any], dict[str, str]]:
    rel = store.relative(source, must_exist=True)
    path = store.absolute(rel, must_exist=True)
    record = load_json(path)
    return record, {"path": rel, "sha256": sha256_file(path)}


def _register_art_direction_source(
        store: Store, args: argparse.Namespace, *, group: str, id_field: str,
        expected_field: str, expected_value: str, prefix: str) -> dict[str, Any]:
    source, artifact = _source_json_artifact(store, args.source)
    source_id = source.get(id_field)
    if not isinstance(source_id, str) or not source_id:
        raise PipelineError(f"art direction source must declare {id_field}")
    if source.get(expected_field) != expected_value:
        raise PipelineError(f"art direction source must declare {expected_field}={expected_value}")
    payload = {"sourceId": source_id, "sourceType": expected_value, "artifact": artifact}
    registry_id = stable_id(prefix, payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "registryId": registry_id, **payload}
    write_json_idempotent(store.record(group, source_id), record, immutable=True)
    return record


def register_art_direction_profile(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    return _register_art_direction_source(
        store, args, group="art-direction-profiles", id_field="profileId",
        expected_field="profileKind", expected_value="project-art-direction",
        prefix="art-direction-profile")


def register_family_profile(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    return _register_art_direction_source(
        store, args, group="family-profiles", id_field="profileId",
        expected_field="profileKind", expected_value="family", prefix="family-profile")


def register_material_language(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    return _register_art_direction_source(
        store, args, group="material-languages", id_field="profileId",
        expected_field="profileKind", expected_value="material-language",
        prefix="material-language")


def create_art_direction_brief(
        store: Store, args: argparse.Namespace, brief_kind: str) -> dict[str, Any]:
    return _register_art_direction_source(
        store, args, group="art-direction-briefs", id_field="briefId",
        expected_field="briefKind", expected_value=brief_kind,
        prefix=f"{brief_kind}-brief")


def create_asset_brief(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    return create_art_direction_brief(store, args, "asset")


def create_scene_brief(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    return create_art_direction_brief(store, args, "scene")


def _validate_art_direction_manifest_source(store: Store, manifest: dict[str, Any]) -> None:
    refs = [manifest.get("projectProfile"), manifest.get("materialLanguage")]
    refs.extend(manifest.get("familyProfiles", []))
    refs.extend(manifest.get("briefTemplates", []))
    for ref in refs:
        if not isinstance(ref, dict) or not ref.get("path") or not ref.get("sha256"):
            raise PipelineError("art direction manifest contains an invalid source reference")
        target = store.absolute(ref["path"], must_exist=True)
        if not bound_input_hash_matches(target, ref["sha256"]):
            raise PipelineError(f"art direction manifest source hash mismatch: {ref['path']}")


def register_art_direction_manifest(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source, artifact = _source_json_artifact(store, args.source)
    if source.get("manifestKind") != "art-direction-manifest" or not source.get("manifestId"):
        raise PipelineError("art direction manifest source is invalid")
    _validate_art_direction_manifest_source(store, source)
    payload = {"sourceId": source["manifestId"], "sourceType": "art-direction-manifest", "artifact": artifact}
    registry_id = stable_id("art-direction-manifest", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "registryId": registry_id, **payload}
    write_json_idempotent(store.record("art-direction-manifests", source["manifestId"]), record, immutable=True)
    pointer = {
        "schemaVersion": ART_DIRECTION_SCHEMA_VERSION,
        "activeArtDirectionManifestId": source["manifestId"],
        "registryId": registry_id,
        "artifact": artifact,
    }
    write_json_idempotent(store.pipeline / "active-art-direction-manifest.json", pointer)
    return record


def _load_registry_source(store: Store, group: str, source_id: str) -> tuple[dict[str, Any], dict[str, Any]]:
    registry_path = store.record(group, source_id)
    registry = load_json(registry_path)
    artifact = registry.get("artifact", {})
    target = store.absolute(artifact.get("path", ""), must_exist=True)
    if not bound_input_hash_matches(target, artifact.get("sha256")):
        raise PipelineError(f"registered art direction source hash mismatch: {source_id}")
    return registry, load_json(target)


def _resolve_art_direction_contract_specs(
        store: Store, args: argparse.Namespace) -> dict[str, Any] | None:
    family_profile_id = getattr(args, "family_profile_id", None)
    brief_id = getattr(args, "brief_id", None)
    if not family_profile_id and not brief_id:
        return None
    if not family_profile_id or not brief_id:
        raise PipelineError("art direction contracts require both --family-profile-id and --brief-id")
    active_path = store.pipeline / "active-art-direction-manifest.json"
    if not active_path.is_file():
        raise PipelineError("art direction contracts require an active registered manifest")
    active = load_json(active_path)
    manifest_id = active.get("activeArtDirectionManifestId")
    manifest_registry, manifest = _load_registry_source(store, "art-direction-manifests", manifest_id)
    if active.get("registryId") != manifest_registry.get("registryId"):
        raise PipelineError("active art direction manifest registry mismatch")
    project_id = manifest.get("projectProfile", {}).get("profileId")
    material_id = manifest.get("materialLanguage", {}).get("profileId")
    project_registry, project = _load_registry_source(store, "art-direction-profiles", project_id)
    material_registry, material = _load_registry_source(store, "material-languages", material_id)
    family_registry, family = _load_registry_source(store, "family-profiles", family_profile_id)
    brief_registry, brief = _load_registry_source(store, "art-direction-briefs", brief_id)
    family_refs = {item.get("profileId"): item for item in manifest.get("familyProfiles", [])}
    family_ref = family_refs.get(family_profile_id)
    if not family_ref or family_ref.get("sha256") != family_registry["artifact"]["sha256"]:
        raise PipelineError("family profile is not active in the current art direction manifest")
    if project.get("profileId") != project_id or material.get("profileId") != material_id:
        raise PipelineError("art direction registry source id mismatch")
    if brief.get("family") != family.get("family"):
        raise PipelineError("art direction brief family does not match family profile")
    anchor_verdict_ids = list(getattr(args, "anchor_verdict_id", []) or [])
    brief_reference_pairs = {
        (item.get("path"), item.get("sha256")) for item in brief.get("referenceResponsibilities", [])
        if isinstance(item, dict)
    }
    for verdict_id in anchor_verdict_ids:
        verdict = load_json(store.record("anchor-verdicts", verdict_id))
        if verdict.get("decision") != "approved-anchor":
            raise PipelineError("only approved-anchor verdicts may bind an art direction contract")
        candidate = verdict.get("candidate", {})
        if (verdict.get("family") != family.get("family")
                and (candidate.get("path"), candidate.get("sha256")) not in brief_reference_pairs):
            raise PipelineError("cross-family anchor must be an explicit hash-bound brief reference")
    return {
        "artDirectionSpec": {"profileId": project_id, **project_registry["artifact"]},
        "materialLanguageSpec": {"profileId": material_id, **material_registry["artifact"]},
        "familyProfileSpec": {"profileId": family_profile_id, "family": family["family"], **family_registry["artifact"]},
        "briefSpec": {"briefId": brief_id, "kind": brief["briefKind"], **brief_registry["artifact"]},
        "themeProfileSpec": None,
        "artDirectionManifestSpec": {"manifestId": manifest_id, **manifest_registry["artifact"]},
        "anchorVerdictIds": anchor_verdict_ids,
        "requiredReviewPanels": brief.get("requiredReviewPanels", []),
        "acceptanceCaseIds": [item if isinstance(item, str) else item.get("caseId") for item in brief.get("acceptanceCases", [])],
    }


def record_anchor_verdict(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if args.reviewer != "cty41":
        raise PipelineError("anchor verdict requires reviewer cty41")
    _iso_timestamp(args.decided_at, "--decided-at")
    candidate_rel = store.relative(args.candidate, must_exist=True)
    review_rel = store.relative(args.review, must_exist=True)
    payload = {
        "candidate": {"path": candidate_rel, "sha256": sha256_file(store.absolute(candidate_rel))},
        "family": args.family,
        "responsibilities": list(args.responsibility),
        "excludedUses": list(args.excluded_use),
        "review": {"path": review_rel, "sha256": sha256_file(store.absolute(review_rel))},
        "decision": args.decision,
        "reviewer": args.reviewer,
        "reason": args.reason,
        "decidedAt": args.decided_at,
    }
    verdict_id = stable_id("anchor-verdict", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "anchorVerdictId": verdict_id, **payload}
    write_json_idempotent(store.record("anchor-verdicts", verdict_id), record, immutable=True)
    return record


def _attempt_contract(store: Store, attempt: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any]]:
    job = load_json(store.record("jobs", attempt["jobId"]))
    return job, load_json(store.record("contracts", job["contractId"]))


def _parse_role_paths(store: Store, values: list[str], option: str) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    for value in values:
        try:
            role, raw_path = value.split("=", 1)
        except ValueError as exc:
            raise PipelineError(f"{option} must use ROLE=PATH") from exc
        if not role or role in result:
            raise PipelineError(f"{option} contains an empty or duplicate role: {role}")
        rel = store.relative(raw_path, must_exist=True)
        result[role] = {"path": rel, "sha256": sha256_file(store.absolute(rel))}
    return result


def record_acceptance_case_result(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    _job, contract = _attempt_contract(store, attempt)
    if contract.get("schemaVersion") != ART_DIRECTION_SCHEMA_VERSION:
        raise PipelineError("acceptance cases require an art direction contract")
    if args.case_id not in contract.get("acceptanceCaseIds", []):
        raise PipelineError("acceptance case is not required by the contract")
    if args.human_decision == "pending":
        if args.reviewer != "agent":
            raise PipelineError("pending acceptance case evidence must use reviewer agent")
    elif args.reviewer != "cty41":
        raise PipelineError("acceptance case human result requires reviewer cty41")
    _iso_timestamp(args.decided_at, "--decided-at")
    evidence = _parse_role_paths(store, list(args.evidence), "--evidence")
    candidate = candidate_artifact(attempt)
    payload = {
        "attemptId": attempt["attemptId"], "caseId": args.case_id,
        "candidate": candidate, "contractId": contract["contractId"],
        "contractSha256": sha256_file(store.record("contracts", contract["contractId"])),
        "evidence": evidence, "automatedFacts": list(args.automated_fact),
        "automatedResult": args.automated_result, "humanChecks": list(args.human_check),
        "humanDecision": args.human_decision, "reviewer": args.reviewer,
        "reason": args.reason, "decidedAt": args.decided_at,
    }
    result_id = stable_id("acceptance-case-result", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "acceptanceCaseResultId": result_id, **payload}
    write_json_idempotent(store.record("acceptance-case-results", result_id), record, immutable=True)
    attempt.setdefault("acceptanceCaseResultIds", [])
    if result_id not in attempt["acceptanceCaseResultIds"]:
        attempt["acceptanceCaseResultIds"].append(result_id)
        save_attempt(store, attempt)
    return record


def _latest_acceptance_results(store: Store, attempt: dict[str, Any]) -> dict[str, dict[str, Any]]:
    results: dict[str, dict[str, Any]] = {}
    for result_id in attempt.get("acceptanceCaseResultIds", []):
        result = load_json(store.record("acceptance-case-results", result_id))
        results[result["caseId"]] = result
    return results


def render_art_direction_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    _job, contract = _attempt_contract(store, attempt)
    if contract.get("schemaVersion") != ART_DIRECTION_SCHEMA_VERSION:
        raise PipelineError("art direction review requires a schema v4 contract")
    panels = _parse_role_paths(store, list(args.panel), "--panel")
    required = set(contract.get("requiredReviewPanels", []))
    if set(panels) != required:
        raise PipelineError(f"art direction review panels must exactly match: {sorted(required)}")
    results = _latest_acceptance_results(store, attempt)
    required_cases = set(contract.get("acceptanceCaseIds", []))
    if set(results) != required_cases:
        raise PipelineError(f"art direction review requires acceptance cases: {sorted(required_cases)}")
    columns = 4
    cell_width, cell_height = 300, 310
    rows = max(1, math.ceil(len(panels) / columns))
    overview = Image.new("RGBA", (columns * cell_width, rows * cell_height), (31, 33, 38, 255))
    draw = ImageDraw.Draw(overview)
    for index, (role, artifact) in enumerate(sorted(panels.items())):
        image = Image.open(store.absolute(artifact["path"], must_exist=True)).convert("RGBA")
        image.thumbnail((280, 260), Image.Resampling.NEAREST)
        x = (index % columns) * cell_width + (cell_width - image.width) // 2
        y = (index // columns) * cell_height + 28
        overview.alpha_composite(image, (x, y))
        draw.text(((index % columns) * cell_width + 12, (index // columns) * cell_height + 8), role, fill=(230, 190, 90, 255))
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel)
    output.parent.mkdir(parents=True, exist_ok=True)
    overview.save(output)
    candidate = candidate_artifact(attempt)
    payload = {
        "attemptId": attempt["attemptId"], "candidate": candidate,
        "contractId": contract["contractId"],
        "contractSha256": sha256_file(store.record("contracts", contract["contractId"])),
        "sourcePanels": panels,
        "acceptanceCaseResultIds": [results[case_id]["acceptanceCaseResultId"] for case_id in sorted(results)],
        "overview": {"path": output_rel, "sha256": sha256_file(output)},
    }
    review_id = stable_id("art-direction-review", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "artDirectionReviewId": review_id, **payload}
    write_json_idempotent(store.record("art-direction-reviews", review_id), record, immutable=True)
    previous_review_id = attempt.get("artDirectionReviewId")
    attempt.setdefault("artDirectionReviewIds", [])
    historical_ids = [
        item.get("artDirectionReviewId")
        for item in (load_json(path) for path in (store.pipeline / "art-direction-reviews").glob("*.json"))
        if item.get("attemptId") == attempt["attemptId"]
    ]
    for bound_review_id in [*historical_ids, previous_review_id, review_id]:
        if bound_review_id and bound_review_id not in attempt["artDirectionReviewIds"]:
            attempt["artDirectionReviewIds"].append(bound_review_id)
    attempt["artDirectionReviewId"] = review_id
    save_attempt(store, attempt)
    register_public_artifacts(store, [record["overview"]], provenance="review-derived")
    return record


def _validate_art_direction_approval(store: Store, attempt: dict[str, Any], contract: dict[str, Any]) -> dict[str, Any]:
    if contract.get("schemaVersion") != ART_DIRECTION_SCHEMA_VERSION:
        raise PipelineError("art direction approval validator requires schema v4")
    review_id = attempt.get("artDirectionReviewId")
    if not review_id:
        raise PipelineError("art direction review is required")
    review = load_json(store.record("art-direction-reviews", review_id))
    candidate = candidate_artifact(attempt)
    if review.get("candidate") != candidate or review.get("contractSha256") != sha256_file(store.record("contracts", contract["contractId"])):
        raise PipelineError("art direction review binding mismatch")
    for artifact in [*review.get("sourcePanels", {}).values(), review.get("overview", {})]:
        target = store.absolute(artifact.get("path", ""), must_exist=True)
        if sha256_file(target) != artifact.get("sha256"):
            raise PipelineError("art direction review artifact hash mismatch")
    results = _latest_acceptance_results(store, attempt)
    required_cases = set(contract.get("acceptanceCaseIds", []))
    if set(results) != required_cases or any(
            result.get("humanDecision") != "passed" or result.get("reviewer") != "cty41"
            or result.get("automatedResult") == "failed" for result in results.values()):
        raise PipelineError("all required acceptance cases must pass")
    verdict_id = attempt.get("artDirectionVerdictId")
    if not verdict_id:
        raise PipelineError("approved art direction verdict is required")
    verdict = load_json(store.record("art-direction-verdicts", verdict_id))
    if (verdict.get("decision") != "approved" or verdict.get("reviewer") != "cty41"
            or verdict.get("artDirectionReviewId") != review_id
            or verdict.get("candidate") != candidate):
        raise PipelineError("art direction verdict binding mismatch")
    return verdict


def record_art_direction_verdict(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if args.reviewer != "cty41":
        raise PipelineError("art direction verdict requires reviewer cty41")
    _iso_timestamp(args.decided_at, "--decided-at")
    attempt = load_json(store.record("attempts", args.attempt_id))
    review_id = attempt.get("artDirectionReviewId")
    if not review_id or review_id != args.review_id:
        raise PipelineError("art direction verdict requires the current bound review")
    review = load_json(store.record("art-direction-reviews", review_id))
    candidate = candidate_artifact(attempt)
    if review.get("candidate") != candidate:
        raise PipelineError("art direction review candidate mismatch")
    payload = {
        "attemptId": attempt["attemptId"], "candidate": candidate,
        "artDirectionReviewId": review_id,
        "reviewSha256": sha256_file(store.record("art-direction-reviews", review_id)),
        "decision": args.decision, "acceptedWarnings": list(args.accept_warning),
        "reviewer": args.reviewer, "reason": args.reason, "decidedAt": args.decided_at,
    }
    verdict_id = stable_id("art-direction-verdict", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "artDirectionVerdictId": verdict_id, **payload}
    write_json_idempotent(store.record("art-direction-verdicts", verdict_id), record, immutable=True)
    attempt.setdefault("artDirectionVerdictIds", [])
    if verdict_id not in attempt["artDirectionVerdictIds"]:
        attempt["artDirectionVerdictIds"].append(verdict_id)
    attempt["artDirectionVerdictId"] = verdict_id
    save_attempt(store, attempt)
    return record


def contract_id(payload: dict[str, Any]) -> str:
    return stable_id("contract", payload)


def registered_asset_state(store: Store, rel: str) -> str | None:
    legacy = store.pipeline / "legacy-assets.json"
    if legacy.is_file():
        for asset in load_json(legacy).get("assets", []):
            if asset.get("path") == rel:
                return asset.get("state")
    for path in (store.pipeline / "attempts").glob("*.json"):
        attempt = load_json(path)
        if attempt.get("state") != "promoted":
            continue
        if any(value.get("path") == rel for value in attempt.get("artifacts", {}).get("promoted", {}).values() if isinstance(value, dict)):
            return "promoted"
    return None


def approved_mask_pair(store: Store, candidate_hash: str, mask_hash: str) -> bool:
    for path in (store.pipeline / "approvals").glob("*.json"):
        receipt = load_json(path)
        if (receipt.get("decision") == "approved"
                and receipt.get("candidateSha256") == candidate_hash
                and receipt.get("maskSha256") == mask_hash):
            return True
    return False


def approved_anchor_mask(store: Store, candidate_hash: str) -> dict[str, str] | None:
    matches: dict[str, dict[str, str]] = {}
    for path in (store.pipeline / "approvals").glob("*.json"):
        receipt = load_json(path)
        mask_path = receipt.get("maskPath")
        mask_hash = receipt.get("maskSha256")
        if (receipt.get("decision") != "approved"
                or receipt.get("candidateSha256") != candidate_hash
                or not mask_path or not mask_hash):
            continue
        absolute = store.absolute(mask_path, must_exist=True)
        if sha256_file(absolute) != mask_hash:
            raise PipelineError("approved anchor mask hash mismatch")
        matches[mask_hash] = {"path": mask_path, "sha256": mask_hash}
    if len(matches) > 1:
        raise PipelineError("anchor has multiple approved masks; bind one explicitly in a new contract")
    return next(iter(matches.values()), None)


def required_review_keys(attempt: dict[str, Any], contract: dict[str, Any]) -> set[str]:
    if contract.get("equipmentProductionSpec"):
        return {"equipmentPanel"}
    if attempt.get("sourceMode") == "reviewed_import":
        return {"sizeComparison"}
    required = {"overlay", "preview128", "tile64x32"}
    if contract.get("identitySpec"):
        required.add("anchorTileCompare")
    if contract.get("occlusion"):
        required.add("depthReview")
    if contract.get("assetRole") == "assembled_sprite":
        required.add("assemblyLayerReview")
    return required


def render_equipment_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    spec = contract.get("equipmentProductionSpec")
    if not spec or attempt.get("state") != "prepared":
        raise PipelineError("equipment review requires a prepared equipment attempt")
    report = load_json(store.absolute(attempt["report"]["path"], must_exist=True))
    if not report.get("passed"):
        raise PipelineError("equipment technical gate did not pass")
    candidate = Image.open(store.absolute(candidate_artifact(attempt)["path"], must_exist=True)).convert("RGBA")
    previous = None
    if attempt.get("parentAttemptId"):
        parent = load_json(store.record("attempts", attempt["parentAttemptId"]))
        parent_artifact = candidate_artifact(parent)
        if parent_artifact:
            previous = Image.open(store.absolute(parent_artifact["path"], must_exist=True)).convert("RGBA")
    anchors = [Image.open(store.absolute(item["path"], must_exist=True)).convert("RGBA") for item in spec["anchors"]]
    cells = anchors + ([previous] if previous else []) + [candidate, make_preview(candidate)]
    panel = Image.new("RGBA", (max(1, len(cells)) * 272, 300), (28, 30, 34, 255))
    draw = ImageDraw.Draw(panel)
    labels = [item["role"] for item in spec["anchors"]] + (["previous"] if previous else []) + ["candidate", "preview128"]
    for index, (cell, label) in enumerate(zip(cells, labels)):
        thumb = cell.copy(); thumb.thumbnail((256, 256), Image.Resampling.LANCZOS)
        x = index * 272 + (272 - thumb.width) // 2
        panel.alpha_composite(thumb, (x, 8 + (256 - thumb.height) // 2))
        draw.text((index * 272 + 8, 276), label, fill=(240, 240, 240, 255))
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel); output.parent.mkdir(parents=True, exist_ok=True)
    panel.save(output, format="PNG", optimize=False, compress_level=9)
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    attempt["artifacts"]["review"] = {"equipmentPanel": artifact}
    transition(attempt, {"prepared"}, "review_pending")
    save_attempt(store, attempt)
    register_public_artifacts(store, [artifact], "project-owned-artwork-review")
    return {"schemaVersion": 2, "attemptId": attempt["attemptId"], "outputs": {"equipmentPanel": artifact}}


def record_equipment_style_verdict(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    if not contract.get("equipmentProductionSpec") or attempt.get("state") != "review_pending":
        raise PipelineError("style verdict requires equipment review_pending attempt")
    if args.reviewer != "cty41":
        raise PipelineError("equipment style verdict must be issued by cty41")
    _iso_timestamp(args.decided_at, "--decided-at")
    review_hashes = approval_review_hashes(store, attempt, contract)
    payload = {"attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt)["sha256"],
               "reviewSha256": review_hashes, "reviewer": args.reviewer,
               "decision": args.decision, "reason": args.reason, "decidedAt": args.decided_at}
    verdict_id = stable_id("equipment-style-verdict", payload)
    record = {"schemaVersion": 2, "equipmentStyleVerdictId": verdict_id, **payload}
    write_json_idempotent(store.record("equipment-style-verdicts", verdict_id), record, immutable=True)
    attempt["equipmentStyleVerdictId"] = verdict_id
    attempt.setdefault("equipmentStyleVerdictIds", [])
    if verdict_id not in attempt["equipmentStyleVerdictIds"]:
        attempt["equipmentStyleVerdictIds"].append(verdict_id)
    save_attempt(store, attempt)
    return record


def approval_review_hashes(store: Store, attempt: dict[str, Any], contract: dict[str, Any]) -> dict[str, str]:
    review = attempt.get("artifacts", {}).get("review")
    required = required_review_keys(attempt, contract)
    if not review or set(review) != required:
        raise PipelineError("approval requires deterministic review outputs")
    hashes: dict[str, str] = {}
    for key in sorted(review):
        artifact = review[key]
        review_path = store.absolute(artifact["path"], must_exist=True)
        if sha256_file(review_path) != artifact["sha256"]:
            raise PipelineError("review artifact hash mismatch")
        hashes[key] = artifact["sha256"]
    return hashes


def core_size_exception_evidence(store: Store, report: dict[str, Any], contract: dict[str, Any]) -> dict[str, Any]:
    core_box = report.get("geometry", {}).get("core", {}).get("bbox")
    anchor = contract.get("anchor") or {}
    anchor_mask_ref = None
    if anchor.get("maskPath"):
        anchor_mask_ref = {"path": anchor["maskPath"], "sha256": anchor.get("maskSha256")}
    elif anchor.get("sha256"):
        anchor_mask_ref = approved_anchor_mask(store, anchor["sha256"])
    if not core_box or not anchor_mask_ref:
        raise PipelineError("core size exception requires candidate and anchor core geometry")
    anchor_mask_path = store.absolute(anchor_mask_ref["path"], must_exist=True)
    if anchor_mask_ref.get("sha256") and sha256_file(anchor_mask_path) != anchor_mask_ref["sha256"]:
        raise PipelineError("approved anchor mask hash mismatch")
    anchor_mask = Image.open(anchor_mask_path).convert("RGBA")
    anchor_box = anchor_core_bbox(anchor_mask)
    if not anchor_box:
        raise PipelineError("core size exception requires an anchor core mask")
    candidate_size = [core_box[2] - core_box[0] + 1, core_box[3] - core_box[1] + 1]
    anchor_size = [anchor_box[2] - anchor_box[0] + 1, anchor_box[3] - anchor_box[1] + 1]
    return {
        "code": "core_size_out_of_tolerance",
        "candidateCoreSize": candidate_size,
        "anchorCoreSize": anchor_size,
        "delta": [candidate_size[0] - anchor_size[0], candidate_size[1] - anchor_size[1]],
        "tolerancePx": contract["tolerances"]["sizePx"],
    }


def gate_exception_evidence(store: Store, issues: list[str], report: dict[str, Any], contract: dict[str, Any]) -> list[dict[str, Any]]:
    requested = set(issues)
    if len(requested) != len(issues):
        raise PipelineError("exception issues must be unique")
    if not requested:
        raise PipelineError("exception approval requires at least one issue")
    unsupported = requested - WAIVABLE_GATE_ISSUES
    if unsupported:
        raise PipelineError(f"issue is not waivable: {sorted(unsupported)[0]}")
    report_issues = set(report.get("issues", []))
    if requested != report_issues:
        raise PipelineError("exception issues must exactly match the validation report")
    evidence = []
    for issue in sorted(requested):
        if issue == "core_size_out_of_tolerance":
            evidence.append(core_size_exception_evidence(store, report, contract))
    return evidence


def validate_exception_receipt(store: Store, attempt: dict[str, Any], receipt: dict[str, Any]) -> None:
    if receipt.get("approvalMode") != "gate-exception":
        raise PipelineError("approval is not a gate exception receipt")
    if receipt.get("reviewer") != "cty41":
        raise PipelineError("gate exception reviewer must be cty41")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract_path = store.record("contracts", job["contractId"])
    contract = load_json(contract_path)
    report_artifact = attempt.get("report") or {}
    report_path = store.absolute(report_artifact.get("path", ""), must_exist=True)
    if sha256_file(report_path) != report_artifact.get("sha256"):
        raise PipelineError("validation report hash mismatch")
    report = load_json(report_path)
    if report.get("passed"):
        raise PipelineError("passing reports must use standard approval")
    issue_codes = [item.get("code") for item in receipt.get("waivedIssues", [])]
    expected_evidence = gate_exception_evidence(store, issue_codes, report, contract)
    expected = {
        "candidateSha256": candidate_artifact(attempt).get("sha256"),
        "maskSha256": candidate_mask_artifact(attempt).get("sha256"),
        "reportSha256": report_artifact.get("sha256"),
        "contractId": job["contractId"],
        "contractSha256": sha256_file(contract_path),
        "reviewSha256": approval_review_hashes(store, attempt, contract),
        "waivedIssues": expected_evidence,
    }
    if contract.get("compositionSpec") and attempt.get("sourceMode") != "reviewed_import":
        annotation_id = attempt.get("annotationId")
        if not annotation_id:
            raise PipelineError("schema v2 gate exception requires annotations")
        expected["annotation"] = {"annotationId": annotation_id,
                                  "sha256": sha256_file(store.record("annotations", annotation_id))}
    for key, value in expected.items():
        if receipt.get(key) != value:
            raise PipelineError(f"gate exception receipt mismatch: {key}")


def approve_anchor(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    candidate_rel = store.relative(args.candidate, must_exist=True)
    mask_rel = store.relative(args.mask, must_exist=True)
    review_rel = store.relative(args.review, must_exist=True)
    if registered_asset_state(store, candidate_rel) not in {"legacy-approved", "promoted"}:
        raise PipelineError("bootstrap anchor candidate must be legacy-approved or promoted")
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    payload = {
        "attemptId": f"bootstrap:{candidate_rel}",
        "candidateSha256": sha256_file(store.absolute(candidate_rel)),
        "maskSha256": sha256_file(store.absolute(mask_rel)),
        "reviewSha256": sha256_file(store.absolute(review_rel)),
        "candidatePath": candidate_rel, "maskPath": mask_rel, "reviewPath": review_rel,
        "reviewer": args.reviewer, "decision": "approved", "reason": args.reason,
        "decidedAt": args.decided_at,
    }
    approval_id = stable_id("approval", payload)
    receipt = {"schemaVersion": 1, "approvalId": approval_id, **payload}
    write_json_idempotent(store.record("approvals", approval_id), receipt, immutable=True)
    return receipt


def series_pose(series: dict[str, Any], pose_id: str) -> dict[str, Any]:
    pose = next((item for item in series.get("poses", []) if item.get("poseId") == pose_id), None)
    if pose is None:
        raise PipelineError(f"series pose does not exist: {pose_id}")
    return pose


def save_series(store: Store, series: dict[str, Any]) -> None:
    write_json_idempotent(store.record("series", series["seriesId"]), series)


def create_series(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if not args.pose:
        raise PipelineError("create-series requires at least one --pose")
    if len(set(args.pose)) != len(args.pose):
        raise PipelineError("series pose IDs must be unique")
    if args.max_unique_outputs is not None and args.max_unique_outputs < 1:
        raise PipelineError("maxUniqueOutputs must be a positive integer or unlimited")
    record = {
        "schemaVersion": 1, "seriesId": args.series_id, "assetId": args.asset_id,
        "maxUniqueOutputs": args.max_unique_outputs, "currentPoseId": args.pose[0],
        "provisionalAnchorAttemptId": None,
        "poses": [{"poseId": value, "state": "active" if index == 0 else "pending",
                   "jobIds": [], "attemptIds": [], "selectedAttemptId": None}
                  for index, value in enumerate(args.pose)],
    }
    write_json_idempotent(store.record("series", args.series_id), record, immutable=True)
    return record


def set_series_output_limit(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    series = load_json(store.record("series", args.series_id))
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    if args.unlimited:
        new_limit = None
    else:
        new_limit = args.max_unique_outputs
        if new_limit is None or new_limit < 1:
            raise PipelineError("provide --unlimited or a positive --max-unique-outputs")
    previous_limit = series.get("maxUniqueOutputs")
    payload = {
        "seriesId": series["seriesId"], "previousMaxUniqueOutputs": previous_limit,
        "maxUniqueOutputs": new_limit, "reviewer": args.reviewer,
        "reason": args.reason, "decidedAt": args.decided_at,
    }
    change_id = stable_id("series-limit-change", payload)
    record = {"schemaVersion": 1, "seriesLimitChangeId": change_id, **payload}
    write_json_idempotent(store.record("series-limit-changes", change_id), record, immutable=True)
    series["maxUniqueOutputs"] = new_limit
    series.setdefault("limitChangeIds", [])
    if change_id not in series["limitChangeIds"]:
        series["limitChangeIds"].append(change_id)
        save_series(store, series)
    return record


def create_contract(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if args.kind not in KINDS:
        raise PipelineError(f"unsupported asset kind: {args.kind}")
    anchor = None
    if args.anchor:
        anchor_rel = store.relative(args.anchor, must_exist=True)
        if registered_asset_state(store, anchor_rel) not in {"legacy-approved", "promoted"}:
            raise PipelineError("core anchor must be legacy-approved or promoted")
        anchor = {"path": anchor_rel, "sha256": sha256_file(store.absolute(anchor_rel))}
        if args.anchor_mask:
            mask_rel = store.relative(args.anchor_mask, must_exist=True)
            mask_hash = sha256_file(store.absolute(mask_rel))
            if not approved_mask_pair(store, anchor["sha256"], mask_hash):
                raise PipelineError("core anchor mask requires an approved human receipt for the same candidate and mask hashes")
            anchor.update({"maskPath": mask_rel, "maskSha256": mask_hash})
    elif args.anchor_mask:
        raise PipelineError("--anchor-mask requires --anchor")
    layer_rules: dict[str, str] = {}
    for value in getattr(args, "layer_rule", []) or []:
        try:
            label, rule = value.split("=", 1)
        except ValueError as exc:
            raise PipelineError("--layer-rule must be LABEL=behind-core") from exc
        if label not in OCCLUSION_LABELS or rule != "behind-core":
            raise PipelineError(f"unsupported layer rule: {value}")
        if label in layer_rules:
            raise PipelineError(f"duplicate layer rule: {label}")
        layer_rules[label] = rule
    visibility_caps: dict[str, float] = {}
    for value in getattr(args, "visibility_cap", []) or []:
        try:
            label, raw_ratio = value.split("=", 1)
            ratio = float(raw_ratio)
        except (ValueError, TypeError) as exc:
            raise PipelineError("--visibility-cap must be LABEL=RATIO") from exc
        if label not in OCCLUSION_LABELS or not 0 < ratio <= 1:
            raise PipelineError(f"invalid visibility cap: {value}")
        if label in visibility_caps:
            raise PipelineError(f"duplicate visibility cap: {label}")
        visibility_caps[label] = ratio
    if set(visibility_caps) - set(layer_rules):
        raise PipelineError("visibility caps require a layer rule for the same label")
    occlusion = None
    if layer_rules:
        occlusion = {"layerRules": layer_rules, "visibilityCaps": visibility_caps}
    composition = None
    composition_id = getattr(args, "composition_id", None)
    if composition_id:
        composition_record = load_json(store.record("compositions", composition_id))
        composition = {
            "compositionId": composition_id,
            "sha256": sha256_file(store.record("compositions", composition_id)),
        }
    asset_role = getattr(args, "asset_role", None)
    component_kind = getattr(args, "component_kind", None)
    source_mode = getattr(args, "source_mode", None)
    if asset_role and asset_role not in ASSET_ROLES:
        raise PipelineError(f"unsupported asset role: {asset_role}")
    if asset_role == "component" and component_kind not in COMPONENT_KINDS:
        raise PipelineError("component contracts require --component-kind")
    if asset_role != "component" and component_kind:
        raise PipelineError("--component-kind is only valid for component contracts")
    if asset_role and source_mode not in SOURCE_MODES:
        raise PipelineError("schema v3 contracts require --source-mode")
    writes_v2 = hasattr(args, "composition_id")
    high_risk = (asset_role != "component"
                 and (args.kind in {"action_pose", "death_pose"} or bool(occlusion)
                      or bool(getattr(args, "pose_reference", False))))
    if writes_v2 and high_risk and not composition:
        raise PipelineError("high-risk schema v2 contract requires --composition-id")
    identity_spec = None
    identity_anchor_mask = getattr(args, "identity_anchor_mask", None)
    if identity_anchor_mask:
        if not anchor:
            raise PipelineError("identity anchor mask requires --anchor")
        identity_rel = store.relative(identity_anchor_mask, must_exist=True)
        identity_spec = {
            "anchorMaskPath": identity_rel,
            "anchorMaskSha256": sha256_file(store.absolute(identity_rel)),
            "foreheadBlazeMinIou": getattr(args, "forehead_blaze_min_iou", 0.45),
            "foreheadBlazeAreaRatio": [0.65, 1.45],
        }
    master_width = getattr(args, "master_width", 256)
    master_height = getattr(args, "master_height", 256)
    if master_width <= 0 or master_height <= 0:
        raise PipelineError("master dimensions must be positive")
    placement_values = {
        "footprint_width": getattr(args, "footprint_width", None),
        "footprint_height": getattr(args, "footprint_height", None),
        "display_scale": getattr(args, "display_scale", None),
        "ground_anchor_x": getattr(args, "ground_anchor_x", None),
        "ground_anchor_y": getattr(args, "ground_anchor_y", None),
        "anchor_mode": getattr(args, "anchor_mode", None),
    }
    requested_board_role = getattr(args, "board_role", None)
    requested_screen_facing = getattr(args, "screen_facing", None)
    if not any(value is not None for value in placement_values.values()) and (
            requested_board_role is not None or requested_screen_facing is not None):
        raise PipelineError("board orientation requires tile placement")
    tile_placement = None
    if any(value is not None for value in placement_values.values()):
        if any(value is None for value in placement_values.values()):
            raise PipelineError("tile placement requires footprint, display scale, ground anchor, and anchor mode")
        footprint_width = placement_values["footprint_width"]
        footprint_height = placement_values["footprint_height"]
        display_scale = placement_values["display_scale"]
        anchor_x = placement_values["ground_anchor_x"]
        anchor_y = placement_values["ground_anchor_y"]
        if footprint_width <= 0 or footprint_height <= 0:
            raise PipelineError("tile footprint dimensions must be positive")
        if display_scale <= 0:
            raise PipelineError("tile placement display scale must be positive")
        if not 0 <= anchor_x < master_width or not 0 <= anchor_y < master_height:
            raise PipelineError("tile placement ground anchor must be inside the master canvas")
        tile_placement = {
            "footprintTiles": [footprint_width, footprint_height],
            "displayScale": display_scale,
            "groundAnchorPx": [anchor_x, anchor_y],
            "anchorMode": placement_values["anchor_mode"],
        }
        board_role = requested_board_role
        screen_facing = requested_screen_facing
        if (board_role is None) != (screen_facing is None):
            raise PipelineError("tile placement board role and screen facing must be declared together")
        if board_role is not None:
            allowed_facing = {
                "target": {"down_left", "non_directional"},
                "player": {"up_right", "non_directional"},
                "neutral": {"non_directional"},
            }
            if screen_facing not in allowed_facing[board_role]:
                raise PipelineError(f"screen facing {screen_facing} is invalid for board role {board_role}")
            tile_placement.update({"boardRole": board_role, "screenFacing": screen_facing})
    style_spec = None
    style_profile_path = getattr(args, "style_profile", None)
    style_values = (
        getattr(args, "target_visible_height", None),
        getattr(args, "visible_height_min", None),
        getattr(args, "visible_height_max", None),
    )
    if style_profile_path or (any(value is not None for value in style_values)
                              and not getattr(args, "equipment_production_profile", None)):
        if not style_profile_path or any(value is None for value in style_values):
            raise PipelineError("equipment style requires profile, target height, and visible height range")
        target_height, minimum_height, maximum_height = style_values
        if not 0 < minimum_height <= target_height <= maximum_height <= master_height:
            raise PipelineError("invalid equipment visible height range")
        profile_rel = store.relative(style_profile_path, must_exist=True)
        profile = load_json(store.absolute(profile_rel))
        if profile.get("schemaVersion") != 1 or not profile.get("profileId"):
            raise PipelineError("equipment style profile must use schemaVersion 1 and declare profileId")
        for reference in profile.get("references", []):
            reference_path = store.absolute(reference["path"], must_exist=True)
            if sha256_file(reference_path) != reference.get("sha256"):
                raise PipelineError("equipment style reference hash mismatch")
        style_spec = {
            "profileId": profile["profileId"],
            "profilePath": profile_rel,
            "profileSha256": sha256_file(store.absolute(profile_rel)),
            "targetVisibleHeight": target_height,
            "visibleHeightRange": [minimum_height, maximum_height],
        }
    equipment_spec = None
    production_profile_path = getattr(args, "equipment_production_profile", None)
    equipment_category = getattr(args, "equipment_category", None)
    if production_profile_path or equipment_category:
        if not production_profile_path or equipment_category not in EQUIPMENT_CATEGORIES:
            raise PipelineError("equipment production requires a profile and supported category")
        profile_rel = store.relative(production_profile_path, must_exist=True)
        profile = load_json(store.absolute(profile_rel))
        if profile.get("schemaVersion") != 2 or not profile.get("profileId"):
            raise PipelineError("equipment production profile must use schemaVersion 2")
        category_profile = profile.get("categories", {}).get(equipment_category)
        if not category_profile:
            raise PipelineError("equipment category is missing from production profile")
        anchors = []
        for reference in profile.get("baseAnchors", []) + category_profile.get("anchors", []):
            target = store.absolute(reference["path"], must_exist=True)
            if sha256_file(target) != reference.get("sha256"):
                raise PipelineError("equipment production anchor hash mismatch")
            anchors.append(reference)
        target_height, minimum_height, maximum_height = style_values
        if any(value is None for value in (target_height, minimum_height, maximum_height)):
            raise PipelineError("equipment production requires target height and visible height range")
        if not 0 < minimum_height <= target_height <= maximum_height <= master_height:
            raise PipelineError("invalid equipment production visible height range")
        equipment_spec = {
            "profileId": profile["profileId"], "profilePath": profile_rel,
            "profileSha256": sha256_file(store.absolute(profile_rel)), "category": equipment_category,
            "anchors": anchors, "targetVisibleHeight": target_height,
            "visibleHeightRange": [minimum_height, maximum_height],
        }
    payload = {
        "assetId": args.asset_id,
        "approvedAssetId": args.approved_asset_id or args.asset_id,
        "kind": args.kind,
        "direction": args.direction,
        "pose": args.pose,
        "anchor": anchor,
        "maskRequired": bool(args.mask_required),
        "noArms": bool(args.no_arms),
        "handSides": {"near_hand": args.near_hand_side, "far_hand": args.far_hand_side},
        "occlusion": occlusion,
        "compositionSpec": composition,
        "identitySpec": identity_spec,
        "requiresInvocation": (bool(equipment_spec) or (writes_v2 and high_risk and source_mode != "derived")
                               or (asset_role == "component" and source_mode == "generated")),
        "canvasSpec": {"masterSize": [master_width, master_height]},
        "tilePlacementSpec": tile_placement,
        "styleSpec": style_spec,
        "equipmentProductionSpec": equipment_spec,
        "tolerances": {"sizePx": args.size_tolerance, "centerPx": args.center_tolerance},
        "outputs": {"master": store.relative(args.output_master), "preview": store.relative(args.output_preview)},
        "rights": {"rightsHolder": args.rights_holder, "license": args.license, "provenance": args.provenance},
    }
    if asset_role:
        payload.update({
            "assetRole": asset_role,
            "componentKind": component_kind,
            "sourceMode": source_mode,
            "runtimeEligible": asset_role == "assembled_sprite",
        })
    art_direction_specs = _resolve_art_direction_contract_specs(store, args)
    if art_direction_specs:
        payload.update(art_direction_specs)
    cid = contract_id(payload)
    version = ART_DIRECTION_SCHEMA_VERSION if art_direction_specs else (3 if asset_role else (2 if writes_v2 else 1))
    record = {"schemaVersion": version, "contractId": cid, **payload}
    write_json_idempotent(store.record("contracts", cid), record, immutable=True)
    return record


def create_equipment_contract(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    values = vars(args).copy()
    values.update({
        "kind": "tile", "direction": "non_directional", "pose": "inventory",
        "equipment_production_profile": args.production_profile,
        "equipment_category": args.category, "style_profile": None,
        "asset_role": None, "component_kind": None, "source_mode": None,
        "anchor": None, "anchor_mask": None, "mask_required": False, "no_arms": False,
        "near_hand_side": None, "far_hand_side": None,
        "composition_id": None,
    })
    return create_contract(store, argparse.Namespace(**values))


def _equipment_style_profile(store: Store, contract: dict[str, Any]) -> dict[str, Any]:
    spec = contract.get("equipmentProductionSpec") or contract.get("styleSpec")
    if not spec:
        raise PipelineError("operation requires a contract-bound equipment style profile")
    profile_path = store.absolute(spec["profilePath"], must_exist=True)
    if sha256_file(profile_path) != spec["profileSha256"]:
        raise PipelineError("equipment style profile hash mismatch")
    profile = load_json(profile_path)
    if profile.get("profileId") != spec["profileId"]:
        raise PipelineError("equipment style profile id mismatch")
    for reference in spec.get("anchors", []):
        if sha256_file(store.absolute(reference["path"], must_exist=True)) != reference["sha256"]:
            raise PipelineError("equipment production anchor hash mismatch")
    for reference in profile.get("references", []):
        if sha256_file(store.absolute(reference["path"], must_exist=True)) != reference["sha256"]:
            raise PipelineError("equipment style reference hash mismatch")
    return profile


def register_local_reference(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source = Path(args.path).resolve()
    if not source.is_file():
        raise PipelineError("local reference file does not exist")
    if args.role not in LOCAL_REFERENCE_ROLES:
        raise PipelineError("unsupported local reference role")
    payload = {"role": args.role, "sourceLabel": args.source_label,
               "fileName": source.name, "sha256": sha256_file(source),
               "localOnly": True, "publish": False}
    reference_id = stable_id("local-reference", payload)
    record = {"schemaVersion": 1, "localReferenceId": reference_id, **payload}
    write_json_idempotent(store.record("local-references", reference_id), record, immutable=True)
    return record


def _interior_style_metrics(image: Image.Image) -> dict[str, Any]:
    rgba = image.convert("RGBA")
    pixels = pixel_data(rgba)
    width, height = rgba.size
    interior: list[tuple[int, int, int]] = []
    smooth_pixels = 0
    scanned_pixels = 0
    for y in range(1, height - 1):
        row: list[tuple[int, tuple[int, int, int]]] = []
        for x in range(1, width - 1):
            index = y * width + x
            value = pixels[index]
            if value[3] == 255 and all(pixels[index + delta][3] == 255 for delta in (-1, 1, -width, width)):
                rgb = value[:3]
                interior.append(rgb)
                row.append((x, rgb))
        run = 1
        previous_sign = 0
        for index in range(1, len(row)):
            if row[index][0] != row[index - 1][0] + 1:
                run = 1
                previous_sign = 0
                continue
            left = row[index - 1][1]
            right = row[index][1]
            delta = round(sum(right) / 3) - round(sum(left) / 3)
            sign = 1 if 1 <= delta <= 8 else (-1 if -8 <= delta <= -1 else 0)
            if sign and sign == previous_sign:
                run += 1
            else:
                if run >= 8:
                    smooth_pixels += run
                run = 2 if sign else 1
            previous_sign = sign
            scanned_pixels += 1
        if run >= 8:
            smooth_pixels += run
    bins = {(red // 16, green // 16, blue // 16) for red, green, blue in interior}
    return {
        "interiorPixels": len(interior),
        "interiorColorBins": len(bins),
        "smoothGradientRatio": round(smooth_pixels / max(1, scanned_pixels), 6),
    }


def prepare_equipment_candidate(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    contract = load_json(store.record("contracts", args.contract_id))
    profile = _equipment_style_profile(store, contract)
    spec = contract.get("equipmentProductionSpec") or contract["styleSpec"]
    source = _bound_artifact(store, args.source)
    attempt = None
    attempt_id = getattr(args, "attempt_id", None)
    if attempt_id:
        attempt = load_json(store.record("attempts", attempt_id))
        job = load_json(store.record("jobs", attempt["jobId"]))
        if job["contractId"] != args.contract_id or attempt["state"] != "ingested":
            raise PipelineError("equipment candidate attempt does not match contract or ingested state")
        expected_source = (attempt.get("artifacts", {}).get("remediatedSource")
                           if attempt.get("technicalRemediation") else attempt.get("artifacts", {}).get("raw"))
        if not expected_source or source["sha256"] != expected_source.get("sha256"):
            raise PipelineError("equipment candidate source must match the attempt generation lineage")
    with Image.open(store.absolute(source["path"])) as opened:
        image = opened.convert("RGBA")
    chroma = profile.get("chroma", "00ff00").lstrip("#")
    tolerance = int(profile.get("chromaTolerance", 48))
    key = tuple(int(chroma[index:index + 2], 16) for index in (0, 2, 4))
    cleaned = []
    for red, green, blue, alpha in pixel_data(image):
        distance = (red - key[0]) ** 2 + (green - key[1]) ** 2 + (blue - key[2]) ** 2
        green_screen = green >= 120 and green >= red + 40 and green >= blue + 40
        cleaned.append((0, 0, 0, 0) if distance <= tolerance ** 2 or green_screen else (red, green, blue, alpha))
    image.putdata(cleaned)
    bbox = image.getchannel("A").getbbox()
    if not bbox:
        raise PipelineError("equipment source is empty after chroma removal")
    cropped = image.crop(bbox)
    target_height = spec["targetVisibleHeight"]
    target_width = max(1, round(cropped.width * target_height / cropped.height))
    resized = cropped.resize((target_width, target_height), Image.Resampling.LANCZOS)
    master_size = tuple(contract.get("canvasSpec", {}).get("masterSize", [256, 256]))
    canvas = Image.new("RGBA", master_size, (0, 0, 0, 0))
    baseline = int(profile.get("baseline", 236))
    x = round((master_size[0] - target_width) / 2)
    y = baseline - target_height + 1
    would_clip = x < 0 or y < 0 or x + target_width > master_size[0] or baseline >= master_size[1]
    canvas.alpha_composite(resized, (x, y))
    canvas = clean_exact_chroma(canvas)
    preview_image = clean_exact_chroma(make_preview(canvas))
    output_rel = store.relative(args.output)
    preview_rel = store.relative(args.preview)
    output_path = store.absolute(output_rel)
    preview_path = store.absolute(preview_rel)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, format="PNG", optimize=False, compress_level=9)
    preview_image.save(preview_path, format="PNG", optimize=False, compress_level=9)
    candidate = _bound_artifact(store, output_rel)
    preview = _bound_artifact(store, preview_rel)
    return _equipment_candidate_report(store, contract, source, candidate, preview, canvas, would_clip, attempt)


def _equipment_candidate_report(store: Store, contract: dict[str, Any], source: dict[str, str],
                                candidate: dict[str, str], preview: dict[str, str], canvas: Image.Image,
                                would_clip: bool = False, attempt: dict[str, Any] | None = None) -> dict[str, Any]:
    """Measure an existing master without resizing or rewriting its pixels."""
    profile = _equipment_style_profile(store, contract)
    spec = contract.get("equipmentProductionSpec") or contract["styleSpec"]
    baseline = int(profile.get("baseline", 236))
    visible = canvas.getchannel("A").getbbox()
    metrics = _interior_style_metrics(canvas)
    metrics.update({"visibleBbox": list(visible) if visible else None,
                    "visibleSize": [visible[2] - visible[0], visible[3] - visible[1]] if visible else None,
                    "baseline": visible[3] - 1 if visible else None})
    minimum_height, maximum_height = spec["visibleHeightRange"]
    limits = profile.get("hardGates", {})
    hard_issues = []
    if would_clip:
        hard_issues.append("equipment_canvas_clipped")
    advisories = []
    if not visible or not minimum_height <= metrics["visibleSize"][1] <= maximum_height:
        hard_issues.append("equipment_visible_height_out_of_range")
    if metrics["baseline"] != baseline:
        hard_issues.append("equipment_baseline_mismatch")
    if metrics["interiorColorBins"] > int(limits.get("maxInteriorColorBins", 40)):
        advisories.append("equipment_palette_complexity_review")
    if metrics["smoothGradientRatio"] > float(limits.get("maxSmoothGradientRatio", 0.12)):
        advisories.append("equipment_smooth_gradient_review")
    payload = {"contractId": contract["contractId"],
               "contractSha256": sha256_file(store.record("contracts", contract["contractId"])),
               "profile": {"path": spec["profilePath"], "sha256": spec["profileSha256"]},
               "source": source, "candidate": candidate, "preview": preview, "metrics": metrics,
               "processingMode": "preserve-fidelity", "quantized": False,
               "remediation": attempt.get("remediation") if attempt else None,
               "hardIssues": sorted(hard_issues), "advisories": sorted(advisories),
               "issues": sorted(hard_issues), "passed": not hard_issues}
    report_id = stable_id("equipment-style-report", payload)
    report = {"schemaVersion": 1, "styleReportId": report_id, **payload}
    write_json_idempotent(store.record("style-reports", report_id), report, immutable=True)
    register_public_artifacts(store, [candidate, preview], "project-owned-supporting-derived")
    if attempt:
        attempt["artifacts"]["prepared"] = candidate
        attempt["artifacts"]["equipmentPreview"] = preview
        attempt["report"] = {"path": store.relative(store.record("style-reports", report_id)),
                             "sha256": sha256_file(store.record("style-reports", report_id))}
        attempt["equipmentStyleReportId"] = report_id
        attempt["state"] = "prepared"
        save_attempt(store, attempt)
    return report


def remediate_equipment_candidate(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    parent = load_json(store.record("attempts", args.parent_attempt))
    job = load_json(store.record("jobs", parent["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    if not contract.get("equipmentProductionSpec"):
        raise PipelineError("equipment remediation requires a production contract")
    feedback = load_json(store.record("feedback", args.feedback_id))
    attempts = list_attempts(store, job["jobId"])
    if not attempts or attempts[-1]["attemptId"] != parent["attemptId"] or feedback.get("attemptId") != parent["attemptId"]:
        raise PipelineError("equipment remediation must branch from the latest feedback-bound attempt")
    raw = parent.get("artifacts", {}).get("raw")
    if not raw:
        raise PipelineError("equipment remediation parent has no raw artifact")
    raw_path = store.absolute(raw["path"], must_exist=True)
    if sha256_file(raw_path) != raw["sha256"]:
        raise PipelineError("equipment remediation parent raw hash mismatch")
    child = retry(store, argparse.Namespace(job_id=job["jobId"], parent_attempt=parent["attemptId"],
                                            feedback_id=args.feedback_id, technical_remediation=True))
    ingest(store, argparse.Namespace(attempt_id=child["attemptId"], source=raw["path"], invocation_id=None))
    profile = _equipment_style_profile(store, contract)
    source = Image.open(store.absolute(raw["path"], must_exist=True)).convert("RGBA")
    if args.mode == "palette":
        alpha = source.getchannel("A")
        colors = int(profile.get("remediation", {}).get("paletteColors", 24))
        source = source.quantize(colors=colors, method=Image.Quantize.FASTOCTREE).convert("RGBA")
        source.putalpha(alpha)
    elif args.mode == "alpha-islands":
        width, height = source.size
        opaque = {index for index, pixel in enumerate(pixel_data(source)) if pixel[3]}
        components: list[set[int]] = []
        while opaque:
            seed = opaque.pop()
            component = {seed}
            frontier = [seed]
            while frontier:
                index = frontier.pop()
                x, y = index % width, index // width
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    neighbor = ny * width + nx
                    if 0 <= nx < width and 0 <= ny < height and neighbor in opaque:
                        opaque.remove(neighbor)
                        component.add(neighbor)
                        frontier.append(neighbor)
            components.append(component)
        keep = max(components, key=len) if components else set()
        source.putdata([pixel if index in keep else (0, 0, 0, 0)
                        for index, pixel in enumerate(pixel_data(source))])
    source = normalize_transparent_rgb(source)
    derived = store.pipeline / "artifacts" / job["jobId"] / child["attemptId"] / f"remediated-{args.mode}.png"
    derived.parent.mkdir(parents=True, exist_ok=True)
    source.save(derived, format="PNG", optimize=False, compress_level=9)
    artifact = {"path": store.relative(derived), "sha256": sha256_file(derived)}
    register_public_artifacts(store, [artifact], "project-owned-derived-artwork")
    child = load_json(store.record("attempts", child["attemptId"]))
    child["artifacts"]["remediatedSource"] = artifact
    child["remediation"] = {"mode": args.mode, "parentAttemptId": parent["attemptId"],
                            "originalSha256": raw["sha256"], "derived": artifact}
    save_attempt(store, child)
    report = prepare_equipment_candidate(store, argparse.Namespace(
        contract_id=contract["contractId"], source=artifact["path"], output=args.output,
        preview=args.preview, attempt_id=child["attemptId"]))
    return {"schemaVersion": 2, "attemptId": child["attemptId"], "report": report}


def create_job(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    contract = load_json(store.record("contracts", args.contract_id))
    pose_guide = None
    pose_guide_id = getattr(args, "pose_guide_id", None)
    if contract.get("requiresInvocation") and contract.get("compositionSpec"):
        if not pose_guide_id:
            raise PipelineError("high-risk schema v2 job requires --pose-guide-id")
        guide_record = load_json(store.record("pose-guides", pose_guide_id))
        if guide_record.get("compositionId") != contract["compositionSpec"]["compositionId"]:
            raise PipelineError("pose guide does not match contract composition")
        pose_guide = {"poseGuideId": pose_guide_id,
                      "sha256": sha256_file(store.record("pose-guides", pose_guide_id))}
    series_binding = None
    series_id = getattr(args, "series_id", None)
    pose_id = getattr(args, "pose_id", None)
    if series_id or pose_id:
        if not series_id or not pose_id:
            raise PipelineError("--series-id and --pose-id must be provided together")
        series = load_json(store.record("series", series_id))
        pose = series_pose(series, pose_id)
        if pose["state"] not in {"active", "provisional"}:
            raise PipelineError("job can only be created for the active series pose")
        concept_only = bool(series.get("provisionalAnchorAttemptId") and pose_id != "idle-dr")
        if concept_only and any(part in FORMAL_DIRS for part in Path(contract["outputs"]["master"]).parts):
            raise PipelineError("provisional-anchor jobs must write to a non-formal concept path")
        series_binding = {"seriesId": series_id, "poseId": pose_id}
    else:
        concept_only = False
    inputs = []
    for role_path in args.input:
        try:
            role, raw_path = role_path.split("=", 1)
        except ValueError as exc:
            raise PipelineError("--input must be ROLE=PATH") from exc
        rel = store.relative(raw_path, must_exist=True)
        inputs.append({"role": role, "path": rel, "sha256": sha256_file(store.absolute(rel))})
    inputs.sort(key=lambda item: (item["role"], item["path"]))
    local_references = []
    for reference_id in getattr(args, "local_reference_id", []) or []:
        reference = load_json(store.record("local-references", reference_id))
        if not reference.get("localOnly") or reference.get("publish") is not False:
            raise PipelineError("invalid local reference descriptor")
        local_references.append({
            "localReferenceId": reference_id, "role": reference["role"],
            "sourceLabel": reference["sourceLabel"], "fileName": reference["fileName"],
            "sha256": reference["sha256"],
            "descriptorSha256": sha256_file(store.record("local-references", reference_id)),
        })
    local_references.sort(key=lambda item: item["localReferenceId"])
    for item in inputs:
        if "anchor" in item["role"].lower() or "mother" in item["role"].lower():
            anchor = contract.get("anchor")
            if not anchor or item["path"] != anchor["path"]:
                raise PipelineError("mother/anchor input must equal the contract core anchor")
    prompt_rel = store.relative(args.prompt, must_exist=True)
    requirements = None
    if contract.get("occlusion"):
        requirements = {
            "occlusion": contract["occlusion"],
            "imageGenDirective": "Draw behind-core equipment and both hand paws first, then draw the capsule body over their inner portions; only outer arcs may remain visible.",
        }
    payload = {
        "contractId": contract["contractId"],
        "contractSha256": sha256_file(store.record("contracts", args.contract_id)),
        "prompt": {"path": prompt_rel, "sha256": sha256_file(store.absolute(prompt_rel))},
        "inputs": inputs,
        "target": {"direction": contract["direction"], "pose": contract["pose"]},
        "series": series_binding,
        "conceptOnly": concept_only,
        "contractRequirements": requirements,
        "requiresInvocation": bool(contract.get("requiresInvocation")),
        "poseGuide": pose_guide,
        "localReferences": local_references,
    }
    jid = stable_id("job", payload)
    record = {"schemaVersion": contract.get("schemaVersion", 1), "jobId": jid, "state": "ready", **payload}
    write_json_idempotent(store.record("jobs", jid), record, immutable=True)
    packet = store.pipeline / "packets" / f"{jid}.json"
    write_json_idempotent(packet, record, immutable=True)
    if series_binding and jid not in pose["jobIds"]:
        pose["jobIds"].append(jid)
        save_series(store, series)
    return record


def migrate_ready_job_bindings(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Supersede an unused ready job after its bound authoring inputs changed."""
    old_path = store.record("jobs", args.job_id)
    old = load_json(old_path)
    attempts = list_attempts(store, args.job_id)
    if old.get("state") != "ready":
        raise PipelineError("only a ready job can migrate input bindings")
    if not args.reason.strip() or not args.authorized_by.strip():
        raise PipelineError("migration reason and authorizer cannot be empty")

    replacement = {key: value for key, value in old.items() if key not in {"schemaVersion", "jobId", "state"}}
    changes = []
    bindings = [replacement["prompt"], *replacement.get("inputs", [])]
    for bound in bindings:
        target = store.absolute(bound["path"])
        if not target.is_file():
            raise PipelineError(f"bound input is missing: {bound['path']}")
        current = sha256_file(target)
        if current != bound.get("sha256"):
            changes.append({"path": bound["path"], "previousSha256": bound.get("sha256"), "currentSha256": current})
            bound["sha256"] = current
    if not changes:
        raise PipelineError("job bindings are already current")

    new_job_id = stable_id("job", replacement)
    new_job = {"schemaVersion": old["schemaVersion"], "jobId": new_job_id, "state": "ready", **replacement}
    new_path = store.record("jobs", new_job_id)
    write_json_idempotent(new_path, new_job, immutable=True)
    write_json_idempotent(store.pipeline / "packets" / f"{new_job_id}.json", new_job, immutable=True)

    receipt_payload = {
        "oldJobId": old["jobId"],
        "oldJobSha256": sha256_file(old_path),
        "newJobId": new_job_id,
        "newJobSha256": sha256_file(new_path),
        "changes": changes,
        "historicalAttempts": [{
            "attemptId": attempt["attemptId"],
            "state": attempt["state"],
            "sha256": sha256_file(store.record("attempts", attempt["attemptId"])),
        } for attempt in attempts],
        "reason": args.reason.strip(),
        "authorizedBy": args.authorized_by.strip(),
    }
    migration_id = stable_id("job-migration", receipt_payload)
    receipt = {"schemaVersion": 2, "migrationId": migration_id, **receipt_payload}
    write_json_idempotent(store.record("job-migrations", migration_id), receipt, immutable=True)
    return receipt


def list_attempts(store: Store, job_id: str) -> list[dict[str, Any]]:
    result = []
    for path in sorted((store.pipeline / "attempts").glob(f"{job_id}-*.json")):
        result.append(load_json(path))
    return result


def retry(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    job = load_json(store.record("jobs", args.job_id))
    attempts = list_attempts(store, args.job_id)
    feedback = None
    contract = load_json(store.record("contracts", job["contractId"]))
    supplied_feedback_id = getattr(args, "feedback_id", None)
    supplied_parent_id = getattr(args, "parent_attempt", None)
    if supplied_feedback_id:
        feedback = load_json(store.record("feedback", supplied_feedback_id))
        if supplied_parent_id and (feedback.get("attemptId") != supplied_parent_id
                                  or load_json(store.record("attempts", supplied_parent_id)).get("feedbackId") != supplied_feedback_id):
            raise PipelineError("retry feedback must belong to the parent attempt")
    if attempts and (job.get("series") or contract.get("equipmentProductionSpec")):
        latest = attempts[-1]
        feedback_id = getattr(args, "feedback_id", None)
        if not feedback_id:
            raise PipelineError("retry requires --feedback-id for the previous attempt")
        feedback = load_json(store.record("feedback", feedback_id))
        if feedback.get("attemptId") != latest["attemptId"] or latest.get("feedbackId") != feedback_id:
            raise PipelineError("retry feedback must belong to the latest attempt")
        allowed_verdicts = {"retry", "technical_failed"}
        if getattr(args, "technical_remediation", False) and latest.get("state") == "technical_failed":
            allowed_verdicts.add("selected")
        if feedback.get("verdict") == "exhausted":
            binding = job["series"]
            series = load_json(store.record("series", binding["seriesId"]))
            if series.get("maxUniqueOutputs") is None:
                # An immutable exhausted receipt records the historical decision.
                # If the series limit is later explicitly removed, it may seed the
                # next retry without rewriting that receipt.
                allowed_verdicts.add("exhausted")
        if feedback.get("verdict") not in allowed_verdicts:
            raise PipelineError("selected feedback requires --technical-remediation on a technical failure")
    prompt_delta = feedback.get("nextPromptDelta") if feedback else None
    if feedback:
        addenda = []
        addenda_dir = store.pipeline / "feedback-addenda"
        if addenda_dir.is_dir():
            for addendum_path in addenda_dir.glob("*.json"):
                addendum = load_json(addendum_path)
                if addendum.get("parentFeedbackId") == feedback.get("feedbackId"):
                    addenda.append(addendum)
        if addenda:
            latest_addendum = max(addenda, key=lambda item: (item.get("recordedAt", ""), item.get("feedbackAddendumId", "")))
            if latest_addendum.get("disposition") == "retry" and latest_addendum.get("defects"):
                prompt_delta = "Human-directed retry: " + " ".join(latest_addendum["defects"])
    ordinal = len(attempts) + 1
    parent = args.parent_attempt
    parent_record = None
    if parent:
        parent_record = load_json(store.record("attempts", parent))
        if parent_record["jobId"] != args.job_id:
            raise PipelineError("parent attempt belongs to another job")
    technical_remediation = bool(getattr(args, "technical_remediation", False))
    if technical_remediation and parent_record is None:
        raise PipelineError("technical remediation requires a parent attempt")
    parent_round = parent_record.get("generationRound", parent_record.get("ordinal")) if parent_record else 0
    if parent_record is not None and parent_round is None:
        try:
            parent_round = int(parent_record["attemptId"].rsplit("-a", 1)[1])
        except (KeyError, ValueError, IndexError) as exc:
            raise PipelineError("cannot derive generation round from parent attempt") from exc
    generation_round = parent_round if technical_remediation and parent_record else parent_round + 1
    aid = f"{args.job_id}-a{ordinal:03d}"
    record = {"schemaVersion": job.get("schemaVersion", 1), "attemptId": aid, "jobId": args.job_id, "ordinal": ordinal,
              "generationRound": generation_round, "parentAttemptId": parent, "retryFeedbackId": getattr(args, "feedback_id", None), "promptDelta": prompt_delta,
              "technicalRemediation": technical_remediation,
              "state": "ready", "artifacts": {}, "report": None, "approvalId": None, "feedbackId": None}
    write_json_idempotent(store.record("attempts", aid), record, immutable=True)
    binding = job.get("series")
    if binding:
        series = load_json(store.record("series", binding["seriesId"]))
        pose = series_pose(series, binding["poseId"])
        if aid not in pose["attemptIds"]:
            pose["attemptIds"].append(aid)
            if pose["state"] == "exhausted" and series.get("maxUniqueOutputs") is None:
                pose["state"] = "active"
            save_series(store, series)
        packet = {"schemaVersion": 1, "attemptId": aid, "jobId": args.job_id,
                  "promptDelta": record["promptDelta"], "inputs": job["inputs"], "target": job["target"],
                  "conceptOnly": bool(job.get("conceptOnly")),
                  "contractRequirements": job.get("contractRequirements")}
        write_json_idempotent(store.pipeline / "packets" / f"{aid}.json", packet, immutable=True)
    return record


def index_review_history(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Create an immutable, read-only index over historical human feedback."""
    entries: list[dict[str, Any]] = []
    feedback_dir = store.pipeline / "feedback"
    for feedback_path in sorted(feedback_dir.glob("*.json")):
        feedback = load_json(feedback_path)
        attempt = None
        attempt_id = feedback.get("attemptId")
        if isinstance(attempt_id, str):
            candidate = store.record("attempts", attempt_id)
            if candidate.is_file():
                attempt = load_json(candidate)
        entry = artwork_review.derive_review_history_index_entry(feedback, attempt=attempt)
        entry["feedback"] = {"path": store.relative(feedback_path), "sha256": sha256_file(feedback_path)}
        entry["attemptRecord"] = ({"path": store.relative(store.record("attempts", attempt["attemptId"])),
                                   "sha256": sha256_file(store.record("attempts", attempt["attemptId"]))}
                                  if attempt else None)
        entries.append(entry)
    payload = {"entries": entries, "sourceCount": len(entries)}
    index_id = stable_id("review-history-index", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewHistoryIndexId": index_id, **payload}
    write_json_idempotent(store.record("review-history-indexes", index_id), record, immutable=True)
    return record


def audit_review_case(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Audit a prospective case source without mutating historical pipeline records."""
    source, artifact = _source_json_artifact(store, args.source)
    audit = artwork_review.audit_case_fitness(source)
    payload = {"source": artifact, "caseId": source.get("caseId"), "audit": audit}
    audit_id = stable_id("case-fitness-audit", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "caseFitnessAuditId": audit_id, **payload}
    write_json_idempotent(store.record("case-fitness-audits", audit_id), record, immutable=True)
    return record


def register_project_review_policy(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source, artifact = _source_json_artifact(store, args.source)
    policy_id, version = source.get("policyId"), source.get("version")
    if not isinstance(policy_id, str) or not policy_id or not isinstance(version, int) or isinstance(version, bool) or version < 1:
        raise PipelineError("project review policy source requires policyId and positive integer version")
    payload = {"policyId": policy_id, "version": version, "source": artifact}
    record_id = stable_id("project-review-policy", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "projectReviewPolicyId": record_id, **payload}
    write_json_idempotent(store.record("project-review-policies", record_id), record, immutable=True)
    return record


def create_review_rule(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source, artifact = _source_json_artifact(store, args.source)
    try:
        rule = artwork_review.validate_review_rule(source)
    except artwork_review.ReviewValidationError as exc:
        raise PipelineError(f"invalid review rule source: {exc}") from exc
    payload = {"rule": rule, "source": artifact}
    record_id = stable_id("review-rule", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewRuleRecordId": record_id, **payload}
    write_json_idempotent(store.record("review-rules", record_id), record, immutable=True)
    return record


def create_review_case(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source, artifact = _source_json_artifact(store, args.source)
    try:
        case = artwork_review.validate_review_case(source)
    except artwork_review.ReviewValidationError as exc:
        raise PipelineError(f"invalid review case source: {exc}") from exc
    audit = artwork_review.audit_case_fitness(source)
    if audit["status"] not in {"active", "shadow-only"}:
        raise PipelineError(f"review case fitness is {audit['status']}, not registrable")
    payload = {"case": case, "source": artifact, "fitness": audit}
    record_id = stable_id("review-case", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewCaseRecordId": record_id, **payload}
    write_json_idempotent(store.record("review-cases", record_id), record, immutable=True)
    return record


def compile_review_policy_record(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    policy_path = store.record("project-review-policies", args.project_review_policy_id)
    policy = load_json(policy_path)
    context, context_artifact = _source_json_artifact(store, args.context)
    rules = [load_json(path)["rule"] for path in sorted((store.pipeline / "review-rules").glob("*.json"))]
    cases = [load_json(path)["case"] for path in sorted((store.pipeline / "review-cases").glob("*.json"))]
    try:
        compiled = artwork_review.compile_review_policy(
            {"policyId": policy["policyId"], "version": policy["version"]}, rules, cases, context,
            acceptance_case_ids=list(args.acceptance_case_id), feedback_rule_ids=list(args.feedback_rule_id))
    except artwork_review.ReviewValidationError as exc:
        raise PipelineError(f"cannot compile review policy: {exc}") from exc
    payload = {"compiled": compiled,
               "projectReviewPolicy": {"path": store.relative(policy_path), "sha256": sha256_file(policy_path)},
               "context": context_artifact}
    record_id = stable_id("compiled-review-policy-record", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "compiledReviewPolicyRecordId": record_id, **payload}
    write_json_idempotent(store.record("compiled-review-policies", record_id), record, immutable=True)
    return record


def _model_review_packet_artifacts(attempt: dict[str, Any], caller_artifacts: dict[str, dict[str, str]]) -> dict[str, dict[str, str]]:
    """Bind every currently available candidate artifact under stable packet roles."""
    artifacts = {f"CANDIDATE_{role.upper()}": artifact
                 for role, artifact in sorted(attempt.get("artifacts", {}).items())
                 if isinstance(artifact, dict) and {"path", "sha256"} <= set(artifact)}
    candidate = candidate_artifact(attempt)
    if not candidate:
        raise PipelineError("model review requires a current candidate artifact")
    artifacts["CURRENT_CANDIDATE"] = candidate
    for role, artifact in caller_artifacts.items():
        if role in artifacts:
            raise PipelineError(f"model review artifact role conflicts with candidate binding: {role}")
        artifacts[role] = artifact
    return artifacts


def create_model_review_packet(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Freeze a passing candidate, compiled policy, and review evidence for one model call."""
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt.get("state") != "review_pending":
        raise PipelineError("model review packet requires a review_pending attempt")
    report_ref = attempt.get("report") or {}
    if not report_ref:
        raise PipelineError("model review packet requires a deterministic validation report")
    report_path = store.absolute(report_ref.get("path", ""), must_exist=True)
    if sha256_file(report_path) != report_ref.get("sha256"):
        raise PipelineError("deterministic validation report hash mismatch")
    report = load_json(report_path)
    candidate = candidate_artifact(attempt)
    if not report.get("passed") or report.get("inputSha256") != candidate.get("sha256"):
        raise PipelineError("model review packet requires a passing report for the current candidate")
    job, contract = _attempt_contract(store, attempt)
    brief_ref = contract.get("briefSpec")
    if not brief_ref or not contract.get("artDirectionSpec"):
        raise PipelineError("model review is enabled only for art direction contracts")
    brief_path = store.absolute(brief_ref.get("path", ""), must_exist=True)
    if not bound_input_hash_matches(brief_path, brief_ref.get("sha256")):
        raise PipelineError("contract brief hash mismatch")
    brief = load_json(brief_path)
    compiled_record = load_json(store.record("compiled-review-policies", args.compiled_review_policy_id))
    compiled = compiled_record.get("compiled")
    if not isinstance(compiled, dict):
        raise PipelineError("compiled review policy record is invalid")
    caller_artifacts = _parse_role_paths(store, list(args.artifact), "--artifact")
    evidence = []
    for role, artifact in _parse_role_paths(store, list(args.evidence), "--evidence").items():
        evidence.append({"role": role, **artifact})
    try:
        packet = artwork_review.build_model_review_packet(
            attempt=attempt,
            contract={"contractId": contract["contractId"], "sha256": sha256_file(store.record("contracts", contract["contractId"]))},
            brief={"briefId": brief["briefId"], "sha256": sha256_file(brief_path)},
            compiled_policy=compiled,
            artifacts=_model_review_packet_artifacts(attempt, caller_artifacts), evidence=evidence,
            acceptance_cases=[{"caseId": case_id} for case_id in contract.get("acceptanceCaseIds", [])],
            feedback=[], frozen_invariants=list(args.frozen_invariant), required_model=args.required_model,
        )
    except (KeyError, artwork_review.ReviewValidationError) as exc:
        raise PipelineError(f"cannot create model review packet: {exc}") from exc
    write_json_idempotent(store.record("model-review-packets", packet["packetId"]), packet, immutable=True)
    attempt["modelReviewPacketId"] = packet["packetId"]
    attempt["modelReviewPacketSha256"] = sha256_file(store.record("model-review-packets", packet["packetId"]))
    transition(attempt, {"review_pending"}, "model_review_pending")
    save_attempt(store, attempt)
    return packet


def create_shadow_model_review_packet(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Freeze one registered historical human case for non-authoritative Shadow review."""
    case_record_path = store.record("review-cases", args.review_case_record_id)
    case_record = load_json(case_record_path)
    case = case_record.get("case")
    if not isinstance(case, dict):
        raise PipelineError("review case record is invalid")
    try:
        case = artwork_review.validate_review_case(case)
    except artwork_review.ReviewValidationError as exc:
        raise PipelineError(f"invalid review case: {exc}") from exc
    if case.get("generationInputAllowed"):
        raise PipelineError("shadow review case cannot allow generation input")
    if case.get("containsRetiredAsset") or case["artifact"]["path"].startswith("Tools/artworks/amazon/"):
        raise PipelineError("shadow review case references a retired artifact")
    if not case.get("evidenceRegion"):
        raise PipelineError("shadow review case requires an evidence region")
    attempt = load_json(store.record("attempts", case["source"]["attemptId"]))
    job, source_contract = _attempt_contract(store, attempt)
    context_contract_id = getattr(args, "review_context_contract_id", None)
    context_brief_source = getattr(args, "review_context_brief_source", None)
    if source_contract.get("schemaVersion") in {1, 2, 3} and not context_contract_id:
        raise PipelineError("legacy shadow cases require an explicit --review-context-contract-id override")
    if context_brief_source and not context_contract_id:
        raise PipelineError("--review-context-brief-source requires --review-context-contract-id")
    contract = source_contract if not context_contract_id else load_json(store.record("contracts", context_contract_id))
    if contract.get("schemaVersion") != ART_DIRECTION_SCHEMA_VERSION:
        raise PipelineError("shadow review context contract must use schema v4")
    brief_ref = contract.get("briefSpec")
    if not brief_ref or not contract.get("artDirectionSpec"):
        raise PipelineError("shadow model review is enabled only for art direction contracts")
    brief_path = store.absolute(context_brief_source or brief_ref.get("path", ""), must_exist=True)
    if not bound_input_hash_matches(brief_path, brief_ref.get("sha256")):
        raise PipelineError("review context brief hash mismatch")
    brief = load_json(brief_path)
    if brief.get("briefId") != brief_ref.get("briefId"):
        raise PipelineError("review context brief id does not match contract binding")
    feedback_path = store.record("feedback", args.human_feedback_id)
    feedback = load_json(feedback_path)
    if feedback.get("attemptId") != attempt.get("attemptId") or feedback.get("reviewer") != "cty41":
        raise PipelineError("shadow human feedback must bind the case historical cty41 attempt")
    compiled_record_path = store.record("compiled-review-policies", args.compiled_review_policy_id)
    compiled_record = load_json(compiled_record_path)
    compiled = compiled_record.get("compiled")
    if not isinstance(compiled, dict) or case["caseId"] not in {item.get("caseId") for item in compiled.get("cases", [])}:
        raise PipelineError("compiled review policy does not bind the shadow review case")
    artifact_path = store.absolute(case["artifact"]["path"], must_exist=True)
    if sha256_file(artifact_path) != case["artifact"]["sha256"]:
        raise PipelineError("shadow review case artifact hash mismatch")
    evidence = [{"role": role, **artifact} for role, artifact in _parse_role_paths(store, list(args.evidence), "--evidence").items()]
    if not evidence:
        raise PipelineError("shadow model review requires bound reviewer evidence")
    try:
        packet = artwork_review.build_shadow_model_review_packet(
            attempt=attempt, contract={"contractId": contract["contractId"], "sha256": sha256_file(store.record("contracts", contract["contractId"]))},
            brief={"briefId": brief_ref["briefId"], "sha256": sha256_file(brief_path)}, compiled_policy=compiled,
            historical_contract={"contractId": source_contract["contractId"], "sha256": sha256_file(store.record("contracts", source_contract["contractId"]))},
            compiled_policy_record={"compiledPolicyId": args.compiled_review_policy_id, "sha256": sha256_file(compiled_record_path)},
            case_binding={"reviewCaseRecord": {"reviewCaseRecordId": case_record["reviewCaseRecordId"], "sha256": sha256_file(case_record_path)},
                          "caseId": case["caseId"], "artifact": case["artifact"], "humanDecision": case["source"]["humanDecision"],
                          "humanFeedback": {"feedbackId": feedback["feedbackId"], "sha256": sha256_file(feedback_path)}, "evidenceRegion": case["evidenceRegion"]},
            evidence=evidence, frozen_invariants=list(args.frozen_invariant), required_model=args.required_model,
            requested_effort=args.requested_effort)
    except (KeyError, artwork_review.ReviewValidationError) as exc:
        raise PipelineError(f"cannot create shadow model review packet: {exc}") from exc
    write_json_idempotent(store.record("model-review-packets", packet["packetId"]), packet, immutable=True)
    return packet


def begin_model_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Record an external model-review invocation without making any model call."""
    attempt = load_json(store.record("attempts", args.attempt_id))
    packet = load_json(store.record("model-review-packets", args.packet_id))
    try:
        packet = artwork_review.validate_model_review_packet(packet)
    except artwork_review.ReviewValidationError as exc:
        raise PipelineError(f"invalid model review packet: {exc}") from exc
    shadow = packet.get("qualificationMode") is True
    if packet["attemptId"] != attempt["attemptId"] or (not shadow and attempt.get("modelReviewPacketId") != packet["packetId"]):
        raise PipelineError("model review packet does not match the current attempt")
    if not shadow and attempt.get("state") != "model_review_pending":
        raise PipelineError("model review can only begin for a model_review_pending attempt")
    if args.model != packet["requiredModel"] or args.effort != packet["reasoningEffort"]:
        raise PipelineError("actual model and effort must match the packet requirement")
    _iso_timestamp(args.started_at, "--started-at")
    prompt_source = _bound_artifact(store, args.prompt_source)
    if not args.provider.strip() or not args.fresh_session_id.strip():
        raise PipelineError("provider and fresh session id are required")
    payload = {"attemptId": attempt["attemptId"], "packetId": packet["packetId"], "packetSha256": packet["sha256"],
               "qualificationMode": shadow, "provider": args.provider, "model": args.model, "effort": args.effort,
               "freshSession": True, "freshSessionId": args.fresh_session_id,
               "promptSource": prompt_source, "startedAt": args.started_at}
    invocation_id = stable_id("model-review-invocation", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "modelReviewInvocationId": invocation_id, "state": "started", **payload}
    write_json_idempotent(store.record("model-review-invocations", invocation_id), record, immutable=True)
    if not shadow:
        attempt["modelReviewInvocationId"] = invocation_id
        save_attempt(store, attempt)
    return record


def record_model_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Bind raw external output and validate it, or preserve an unavailable review for a human."""
    invocation = load_json(store.record("model-review-invocations", args.invocation_id))
    attempt = load_json(store.record("attempts", invocation["attemptId"]))
    packet = load_json(store.record("model-review-packets", invocation["packetId"]))
    shadow = invocation.get("qualificationMode") is True
    if shadow != (packet.get("qualificationMode") is True):
        raise PipelineError("model review invocation qualification mode does not match packet")
    if not shadow and (attempt.get("state") != "model_review_pending" or attempt.get("modelReviewInvocationId") != invocation["modelReviewInvocationId"]):
        raise PipelineError("model review invocation is not current for a pending attempt")
    compiled_record = load_json(store.record("compiled-review-policies", args.compiled_review_policy_id))
    compiled = compiled_record.get("compiled")
    if not isinstance(compiled, dict):
        raise PipelineError("compiled review policy record is invalid")
    raw_result = _bound_artifact(store, args.raw_result) if args.raw_result else None
    validation_error = None
    validated_result = None
    if raw_result:
        try:
            raw_payload = json.loads(store.absolute(raw_result["path"], must_exist=True).read_text(encoding="utf-8"))
            validated_result = artwork_review.validate_model_review_result(raw_payload, packet, compiled)
        except (OSError, json.JSONDecodeError, artwork_review.ReviewValidationError) as exc:
            validation_error = str(exc)
    else:
        validation_error = args.unavailable_reason
    if not raw_result and not validation_error:
        raise PipelineError("record model review requires --raw-result or --unavailable-reason")
    payload = {"invocationId": invocation["modelReviewInvocationId"], "attemptId": attempt["attemptId"],
               "packetId": packet.get("packetId"), "packetSha256": packet.get("sha256"), "qualificationMode": shadow,
               "provider": invocation["provider"], "model": invocation["model"], "effort": invocation["effort"],
               "freshSession": invocation["freshSession"], "freshSessionId": invocation["freshSessionId"],
               "promptSource": invocation["promptSource"],
               "rawResult": raw_result, "result": validated_result,
               "outcome": "model_reviewed" if validated_result else "human_review_required",
               "validationError": validation_error}
    result_id = stable_id("model-review-result-record", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "modelReviewResultRecordId": result_id, **payload}
    write_json_idempotent(store.record("model-review-results", result_id), record, immutable=True)
    if not shadow:
        attempt["modelReviewResultRecordId"] = result_id
        attempt["state"] = "model_reviewed" if validated_result else "human_review_required"
        save_attempt(store, attempt)
    return record


def _artifact_binding_matches(store: Store, artifact: Any) -> bool:
    if not isinstance(artifact, dict) or not artifact.get("path") or not artifact.get("sha256"):
        return False
    try:
        target = store.absolute(artifact["path"])
        return target.is_file() and sha256_file(target) == artifact["sha256"]
    except (PipelineError, OSError):
        return False


def _reviewer_qualification_payload(record: dict[str, Any]) -> dict[str, Any]:
    fields = ["state", "reviewer", "ruleId", "ruleVersion", "model", "effort", "reviewerPromptId",
              "reviewerPromptSha256", "compiledPolicyId", "compiledPolicySha256", "caseSetVersion",
              "reviewRule", "compiledPolicy", "reviewerPrompt"]
    fields.extend(key for key in ("promptOnlyComparison", "qualificationAudits") if key in record)
    fields.append("qualifiedAt")
    return {key: record.get(key) for key in fields}


def _audit_is_confirmed(audit: dict[str, Any]) -> bool:
    if audit.get("verdict") is not None:
        return audit.get("verdict") == "confirmed"
    return audit.get("finding") in {
        "matches-human-negative-retry", "matches-approved-positive-no-false-retry",
        "correctly-escalates-occluded-boundary",
    }


def _reviewer_qualification_is_valid(store: Store, record: dict[str, Any], *, allow_legacy: bool = False) -> bool:
    qualification_id = record.get("reviewerQualificationId")
    if not qualification_id or stable_id("reviewer-qualification", _reviewer_qualification_payload(record)) != qualification_id:
        return False
    if record.get("reviewer") != "cty41" or not all(_artifact_binding_matches(store, record.get(key))
                                                       for key in ("reviewRule", "compiledPolicy", "reviewerPrompt")):
        return False
    comparison = record.get("promptOnlyComparison")
    audits = record.get("qualificationAudits", [])
    if not comparison or not audits:
        return allow_legacy
    if not _artifact_binding_matches(store, comparison) or len(audits) < 3 or len({item.get("modelReviewAuditId") for item in audits}) != len(audits):
        return False
    try:
        rule_record = load_json(store.absolute(record["reviewRule"]["path"])); rule = artwork_review.validate_review_rule(rule_record.get("rule", {}))
        compiled_record = load_json(store.absolute(record["compiledPolicy"]["path"])); compiled = compiled_record.get("compiled", {})
        comparison_record = load_json(store.absolute(comparison["path"]))
        validated_comparison = artwork_review.validate_prompt_only_comparison(
            comparison_record.get("promptOnly", {}), comparison_record.get("reviewerClosedLoop", {}))
        context = validated_comparison["promptOnly"].get("context", {})
        if (record.get("ruleId") != rule.get("ruleId") or record.get("ruleVersion") != rule.get("version")
                or record.get("reviewerPromptSha256") != record["reviewerPrompt"].get("sha256")
                or record.get("compiledPolicyId") != compiled.get("compiledPolicyId")
                or record.get("compiledPolicySha256") != compiled.get("sha256")
                or not (comparison.get("promptComparisonId") == comparison_record.get("promptComparisonId") == validated_comparison.get("promptComparisonId"))
                or context.get("compiledPolicyId") != record.get("compiledPolicyId")
                or context.get("caseSetVersion") != record.get("caseSetVersion")
                or validated_comparison["reviewerClosedLoop"].get("context") != context
                or not any(row.get("ruleId") == rule.get("ruleId") for row in validated_comparison["reviewerClosedLoop"].get("rawCounts", []))
                or not any(item.get("ruleId") == rule.get("ruleId") and item.get("version") == rule.get("version") for item in compiled.get("rules", []))):
            return False
        cases_by_id = {case.get("caseId"): case for case in compiled.get("cases", [])}
        expected_decision = {"negative": "retry", "positive": "pass_to_human", "boundary": "escalate_to_human"}
        seen_polarities = set()
        for binding in audits:
            if not _artifact_binding_matches(store, binding):
                return False
            audit = load_json(store.absolute(binding["path"]))
            result_path = store.record("model-review-results", audit.get("modelReviewResultRecordId", "")); result_record = load_json(result_path)
            packet_path = store.record("model-review-packets", result_record.get("packetId", "")); packet = load_json(packet_path)
            invocation_path = store.record("model-review-invocations", result_record.get("invocationId", "")); invocation = load_json(invocation_path)
            case = cases_by_id.get(packet.get("caseBinding", {}).get("caseId")); polarity = case.get("polarity") if case else None
            invocation_payload = {key: invocation.get(key) for key in ("attemptId", "packetId", "packetSha256", "qualificationMode",
                                                                         "provider", "model", "effort", "freshSession",
                                                                         "freshSessionId", "promptSource", "startedAt")}
            result_payload = {key: result_record.get(key) for key in ("invocationId", "attemptId", "packetId", "packetSha256",
                                                                       "provider", "model", "effort", "freshSession", "freshSessionId",
                                                                       "promptSource", "rawResult", "result", "outcome", "validationError")}
            if "qualificationMode" in result_record:
                result_payload["qualificationMode"] = result_record["qualificationMode"]
            nested_result_valid = artwork_review.validate_model_review_result(result_record.get("result", {}), packet, compiled)
            if (audit.get("modelReviewAuditId") != binding.get("modelReviewAuditId") or audit.get("reviewer") != "cty41"
                    or not _audit_is_confirmed(audit) or not _artifact_binding_matches(store, audit.get("modelReviewResult"))
                    or audit["modelReviewResult"].get("path") != store.relative(result_path)
                    or not result_record.get("qualificationMode") or not packet.get("qualificationMode")
                    or artwork_review.validate_model_review_packet(packet).get("packetId") != packet.get("packetId")
                    or stable_id("model-review-invocation", invocation_payload) != invocation.get("modelReviewInvocationId")
                    or stable_id("model-review-result-record", result_payload) != result_record.get("modelReviewResultRecordId")
                    or not _artifact_binding_matches(store, invocation.get("promptSource"))
                    or invocation.get("packetId") != packet.get("packetId") or invocation.get("packetSha256") != packet.get("sha256")
                    or invocation.get("model") != record.get("model") or invocation.get("effort") != record.get("effort")
                    or invocation.get("promptSource") != result_record.get("promptSource")
                    or packet.get("requiredModel") != record.get("model") or packet.get("reasoningEffort") != record.get("effort")
                    or result_record.get("packetSha256") != packet.get("sha256") or nested_result_valid != result_record.get("result")
                    or result_record.get("model") != record.get("model") or result_record.get("effort") != record.get("effort")
                    or result_record.get("outcome") != "model_reviewed" or result_record.get("validationError") is not None
                    or result_record.get("promptSource", {}).get("sha256") != record.get("reviewerPromptSha256")
                    or packet.get("compiledPolicy", {}).get("compiledPolicyId") != record.get("compiledPolicyId")
                    or polarity not in expected_decision
                    or result_record.get("result", {}).get("decision") != expected_decision[polarity]):
                return False
            seen_polarities.add(polarity)
        return seen_polarities == set(expected_decision)
    except (PipelineError, artwork_review.ReviewValidationError, OSError, KeyError, TypeError, json.JSONDecodeError):
        return False


def record_reviewer_qualification(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Let cty41 immutably qualify one exact rule/invocation configuration."""
    if args.reviewer != "cty41":
        raise PipelineError("reviewer qualification requires reviewer cty41")
    rule_record = load_json(store.record("review-rules", args.review_rule_record_id))
    rule = rule_record.get("rule")
    if not isinstance(rule, dict):
        raise PipelineError("review rule record is invalid")
    compiled_record = load_json(store.record("compiled-review-policies", args.compiled_review_policy_id))
    compiled = compiled_record.get("compiled")
    if not isinstance(compiled, dict):
        raise PipelineError("compiled review policy record is invalid")
    if not any(item.get("ruleId") == rule.get("ruleId") and item.get("version") == rule.get("version") for item in compiled.get("rules", [])):
        raise PipelineError("qualification rule is not bound by compiled policy")
    prompt_source = _bound_artifact(store, args.reviewer_prompt_source)
    comparison_id = getattr(args, "prompt_only_comparison_id", None)
    if not comparison_id:
        raise PipelineError("qualification requires a prompt-only comparison")
    comparison_path = store.record("prompt-only-comparisons", comparison_id)
    comparison = load_json(comparison_path)
    comparison_context = comparison.get("promptOnly", {}).get("context", {})
    if (comparison_context.get("compiledPolicyId") != compiled.get("compiledPolicyId")
            or comparison_context.get("caseSetVersion") != args.case_set_version
            or comparison.get("reviewerClosedLoop", {}).get("context") != comparison_context):
        raise PipelineError("qualification prompt-only comparison does not match policy and case set")
    comparison_rows = comparison.get("reviewerClosedLoop", {}).get("rawCounts", [])
    if not any(row.get("ruleId") == rule.get("ruleId") for row in comparison_rows):
        raise PipelineError("qualification prompt-only comparison does not cover rule")
    expected_decision = {"negative": "retry", "positive": "pass_to_human", "boundary": "escalate_to_human"}
    cases_by_id = {case["caseId"]: case for case in compiled.get("cases", [])}
    audit_bindings = []
    seen_polarities = set()
    audit_ids = getattr(args, "model_review_audit_id", None)
    if not audit_ids:
        raise PipelineError("qualification requires audited negative, positive, and boundary cases")
    for audit_id in audit_ids:
        audit_path = store.record("model-review-audits", audit_id)
        audit = load_json(audit_path)
        result_path = store.record("model-review-results", audit["modelReviewResultRecordId"])
        result_record = load_json(result_path)
        packet = load_json(store.record("model-review-packets", result_record["packetId"]))
        case_id = packet.get("caseBinding", {}).get("caseId")
        case = cases_by_id.get(case_id)
        polarity = case.get("polarity") if case else None
        if (audit.get("reviewer") != "cty41" or not _audit_is_confirmed(audit) or not result_record.get("qualificationMode")
                or result_record.get("model") != args.model or result_record.get("effort") != args.effort
                or result_record.get("promptSource", {}).get("sha256") != prompt_source["sha256"]
                or packet.get("compiledPolicy", {}).get("compiledPolicyId") != compiled.get("compiledPolicyId")
                or polarity not in expected_decision
                or result_record.get("result", {}).get("decision") != expected_decision[polarity]):
            raise PipelineError("qualification audit does not prove the required model/effort/policy case decision")
        seen_polarities.add(polarity)
        audit_bindings.append({"modelReviewAuditId": audit_id, "path": store.relative(audit_path), "sha256": sha256_file(audit_path)})
    if seen_polarities != set(expected_decision):
        raise PipelineError("qualification requires audited negative, positive, and boundary cases")
    _iso_timestamp(args.qualified_at, "--qualified-at")
    payload = {
        "state": "auto-retry-qualified", "reviewer": "cty41", "ruleId": rule["ruleId"], "ruleVersion": rule["version"],
        "model": args.model, "effort": args.effort, "reviewerPromptId": args.reviewer_prompt_id,
        "reviewerPromptSha256": prompt_source["sha256"], "compiledPolicyId": compiled["compiledPolicyId"],
        "compiledPolicySha256": compiled["sha256"], "caseSetVersion": args.case_set_version,
        "reviewRule": {"path": store.relative(store.record("review-rules", args.review_rule_record_id)), "sha256": sha256_file(store.record("review-rules", args.review_rule_record_id))},
        "compiledPolicy": {"path": store.relative(store.record("compiled-review-policies", args.compiled_review_policy_id)), "sha256": sha256_file(store.record("compiled-review-policies", args.compiled_review_policy_id))},
        "reviewerPrompt": prompt_source,
        "promptOnlyComparison": {"promptComparisonId": comparison["promptComparisonId"], "path": store.relative(comparison_path), "sha256": sha256_file(comparison_path)},
        "qualificationAudits": sorted(audit_bindings, key=lambda item: item["modelReviewAuditId"]), "qualifiedAt": args.qualified_at,
    }
    qualification_id = stable_id("reviewer-qualification", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewerQualificationId": qualification_id, **payload}
    write_json_idempotent(store.record("reviewer-qualifications", qualification_id), record, immutable=True)
    return record


def supersede_reviewer_qualification(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Supersede one immutable qualification with a stronger compatible qualification."""
    if args.reviewer != "cty41":
        raise PipelineError("reviewer qualification supersession requires reviewer cty41")
    old_path = store.record("reviewer-qualifications", args.old_qualification_id)
    new_path = store.record("reviewer-qualifications", args.new_qualification_id)
    old, new = load_json(old_path), load_json(new_path)
    if old["reviewerQualificationId"] == new["reviewerQualificationId"]:
        raise PipelineError("qualification cannot supersede itself")
    identity = ("ruleId", "ruleVersion", "model", "effort", "reviewerPromptId", "reviewerPromptSha256",
                "compiledPolicyId", "compiledPolicySha256", "caseSetVersion")
    if any(old.get(field) != new.get(field) for field in identity):
        raise PipelineError("qualification supersession requires the same rule, model, effort, prompt, policy, and case set")
    if not _reviewer_qualification_is_valid(store, old, allow_legacy=True):
        raise PipelineError("old qualification identity or bindings are invalid")
    if not _reviewer_qualification_is_valid(store, new):
        raise PipelineError("replacement qualification must contain valid prompt-only comparison and confirmed audited cases")
    if datetime.fromisoformat(new["qualifiedAt"]) <= datetime.fromisoformat(old["qualifiedAt"]):
        raise PipelineError("replacement qualification must be newer than the old qualification")
    for existing_path in (store.pipeline / "reviewer-qualification-supersessions").glob("*.json"):
        existing = load_json(existing_path)
        if existing.get("oldQualificationId") == old["reviewerQualificationId"]:
            if existing.get("newQualificationId") == new["reviewerQualificationId"]:
                return existing
            raise PipelineError("old qualification already has a different successor")
        if existing.get("oldQualificationId") == new["reviewerQualificationId"] and existing.get("newQualificationId") == old["reviewerQualificationId"]:
            raise PipelineError("qualification supersession cycle is forbidden")
    _iso_timestamp(args.superseded_at, "--superseded-at")
    payload = {
        "oldQualificationId": old["reviewerQualificationId"],
        "oldQualification": {"path": store.relative(old_path), "sha256": sha256_file(old_path)},
        "newQualificationId": new["reviewerQualificationId"],
        "newQualification": {"path": store.relative(new_path), "sha256": sha256_file(new_path)},
        "reviewer": "cty41", "reason": args.reason, "supersededAt": args.superseded_at,
    }
    supersession_id = stable_id("reviewer-qualification-supersession", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION,
              "reviewerQualificationSupersessionId": supersession_id, **payload}
    write_json_idempotent(store.record("reviewer-qualification-supersessions", supersession_id), record, immutable=True)
    return record


def suspend_reviewer_rule(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Append a cty41 suspension; qualifications themselves remain immutable."""
    if args.reviewer != "cty41":
        raise PipelineError("reviewer rule suspension requires reviewer cty41")
    qualification = load_json(store.record("reviewer-qualifications", args.reviewer_qualification_id))
    _iso_timestamp(args.suspended_at, "--suspended-at")
    payload = {"reviewerQualificationId": qualification["reviewerQualificationId"], "ruleId": qualification["ruleId"],
               "ruleVersion": qualification["ruleVersion"], "reviewer": "cty41", "reason": args.reason,
               "suspendedAt": args.suspended_at}
    suspension_id = stable_id("reviewer-rule-suspension", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewerRuleSuspensionId": suspension_id, **payload}
    write_json_idempotent(store.record("reviewer-rule-suspensions", suspension_id), record, immutable=True)
    return record


def record_model_review_audit(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Preserve a human audit of a model result without changing its outcome."""
    if args.reviewer != "cty41":
        raise PipelineError("model review audit requires reviewer cty41")
    result_path = store.record("model-review-results", args.model_review_result_record_id)
    result = load_json(result_path)
    _iso_timestamp(args.audited_at, "--audited-at")
    verdict = getattr(args, "verdict", None)
    if verdict not in {"confirmed", "rejected"}:
        raise PipelineError("model review audit requires verdict confirmed or rejected")
    payload = {"modelReviewResultRecordId": result["modelReviewResultRecordId"],
               "modelReviewResult": {"path": store.relative(result_path), "sha256": sha256_file(result_path)},
               "reviewer": "cty41", "verdict": verdict, "finding": args.finding, "auditedAt": args.audited_at}
    audit_id = stable_id("model-review-audit", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "modelReviewAuditId": audit_id, **payload}
    write_json_idempotent(store.record("model-review-audits", audit_id), record, immutable=True)
    return record


def _effective_qualifications(store: Store) -> list[dict[str, Any]]:
    suspended = {load_json(path).get("reviewerQualificationId") for path in (store.pipeline / "reviewer-rule-suspensions").glob("*.json")}
    qualification_records = {record.get("reviewerQualificationId"): record for record in
                             (load_json(path) for path in sorted((store.pipeline / "reviewer-qualifications").glob("*.json")))}
    superseded: dict[str, str] = {}
    for path in sorted((store.pipeline / "reviewer-qualification-supersessions").glob("*.json")):
        record = load_json(path)
        old_id, new_id = record.get("oldQualificationId"), record.get("newQualificationId")
        payload = {key: record.get(key) for key in ("oldQualificationId", "oldQualification", "newQualificationId",
                                                    "newQualification", "reviewer", "reason", "supersededAt")}
        old, new = qualification_records.get(old_id), qualification_records.get(new_id)
        identity = ("ruleId", "ruleVersion", "model", "effort", "reviewerPromptId", "reviewerPromptSha256",
                    "compiledPolicyId", "compiledPolicySha256", "caseSetVersion")
        if (not old or not new or old_id in superseded or record.get("reviewer") != "cty41"
                or stable_id("reviewer-qualification-supersession", payload) != record.get("reviewerQualificationSupersessionId")
                or any(old.get(field) != new.get(field) for field in identity)
                or not _reviewer_qualification_is_valid(store, new)):
            continue
        cursor = new_id
        cyclic = False
        while cursor in superseded:
            cursor = superseded[cursor]
            if cursor == old_id:
                cyclic = True
                break
        if not cyclic:
            superseded[old_id] = new_id
    result = []
    for qualification_id, qualification in qualification_records.items():
        if not _reviewer_qualification_is_valid(store, qualification, allow_legacy=qualification_id in superseded):
            continue
        if qualification_id in suspended:
            qualification = {**qualification, "suspendedAt": "recorded-by-suspension"}
        if qualification_id in superseded:
            qualification = {**qualification, "supersededBy": superseded[qualification_id]}
        result.append(qualification)
    return result


def _create_model_retry_attempt(store: Store, attempt: dict[str, Any], prompt_delta: dict[str, Any]) -> dict[str, Any]:
    """Create the only automatic nontechnical child; never rewrites its parent."""
    generation_round = attempt.get("generationRound", attempt.get("ordinal"))
    if generation_round is None:
        try:
            generation_round = int(attempt["attemptId"].rsplit("-a", 1)[1])
        except (KeyError, ValueError, IndexError) as exc:
            raise PipelineError("cannot derive generation round from attempt") from exc
    if isinstance(generation_round, bool) or not isinstance(generation_round, int) or generation_round >= 3:
        raise PipelineError("automatic generation budget exhausted; cannot create a004")
    attempts = list_attempts(store, attempt["jobId"])
    ordinal = len(attempts) + 1
    if ordinal > 3:
        raise PipelineError("automatic retry cannot create attempt a004 or later; escalate to human review")
    child = {"schemaVersion": attempt.get("schemaVersion", ART_DIRECTION_SCHEMA_VERSION),
             "attemptId": f"{attempt['jobId']}-a{ordinal:03d}", "jobId": attempt["jobId"], "ordinal": ordinal,
             "generationRound": generation_round + 1, "parentAttemptId": attempt["attemptId"],
             "retryFeedbackId": None, "promptDelta": prompt_delta, "technicalRemediation": False,
             "state": "ready", "artifacts": {}, "report": None, "approvalId": None, "feedbackId": None,
             "modelReviewParentResultRecordId": attempt.get("modelReviewResultRecordId")}
    write_json_idempotent(store.record("attempts", child["attemptId"]), child, immutable=True)
    return child


def apply_model_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Apply a validated review exactly once using bound invocation metadata."""
    result_record = load_json(store.record("model-review-results", args.model_review_result_record_id))
    if result_record.get("qualificationMode") is True:
        raise PipelineError("shadow model review results cannot be applied to attempt state or retries")
    result = result_record.get("result")
    if not isinstance(result, dict):
        raise PipelineError("model review result is not validated")
    invocation = load_json(store.record("model-review-invocations", result_record["invocationId"]))
    attempt = load_json(store.record("attempts", result_record["attemptId"]))
    if attempt.get("state") != "model_reviewed" or attempt.get("modelReviewResultRecordId") != result_record["modelReviewResultRecordId"]:
        raise PipelineError("model review result is not current for a reviewed attempt")
    packet = load_json(store.record("model-review-packets", result_record["packetId"]))
    compiled_record = load_json(store.record("compiled-review-policies", args.compiled_review_policy_id))
    compiled = compiled_record.get("compiled")
    if not isinstance(compiled, dict):
        raise PipelineError("compiled review policy record is invalid")
    if result_record.get("packetSha256") != packet.get("sha256") or invocation.get("packetSha256") != packet.get("sha256"):
        raise PipelineError("result or invocation packet binding mismatch")
    if invocation.get("model") != packet.get("requiredModel") or invocation.get("effort") != packet.get("reasoningEffort"):
        raise PipelineError("invocation metadata does not exactly match packet")
    for application_path in (store.pipeline / "model-review-applications").glob("*.json"):
        existing = load_json(application_path)
        if existing.get("modelReviewResultRecordId") == result_record["modelReviewResultRecordId"]:
            return existing
    decision = artwork_review.evaluate_automatic_decision(
        result, packet=packet, compiled_policy=compiled, qualifications=_effective_qualifications(store),
        model=invocation["model"], reviewer_prompt_id=args.reviewer_prompt_id,
        reviewer_prompt_sha256=invocation["promptSource"]["sha256"], case_set_version=args.case_set_version)
    if decision["action"] == "automatic_retry" and len(list_attempts(store, attempt["jobId"])) >= 3:
        decision = {"action": "human_review_required", "automaticRetry": False,
                    "reason": "automatic_retry_would_create_a004"}
    payload = {"modelReviewResultRecordId": result_record["modelReviewResultRecordId"], "attemptId": attempt["attemptId"],
               "decision": decision, "invocation": {"modelReviewInvocationId": invocation["modelReviewInvocationId"],
               "packetId": invocation["packetId"], "packetSha256": invocation["packetSha256"], "model": invocation["model"], "effort": invocation["effort"], "promptSource": invocation["promptSource"]}}
    if decision["action"] == "automatic_retry":
        child = _create_model_retry_attempt(store, attempt, decision["promptDelta"])
        payload["childAttemptId"] = child["attemptId"]
    apply_id = stable_id("model-review-application", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "modelReviewApplicationId": apply_id, **payload}
    write_json_idempotent(store.record("model-review-applications", apply_id), record, immutable=True)
    if decision["action"] != "automatic_retry":
        attempt["state"] = decision["action"]
        save_attempt(store, attempt)
    return record


def create_review_experience_candidate(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source, artifact = _source_json_artifact(store, args.source)
    try:
        candidate = artwork_review.validate_experience_candidate(source)
    except artwork_review.ReviewValidationError as exc:
        raise PipelineError(f"invalid review experience candidate: {exc}") from exc
    payload = {"candidate": candidate, "source": artifact}
    record_id = stable_id("review-experience-candidate", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewExperienceCandidateRecordId": record_id, **payload}
    write_json_idempotent(store.record("review-experience-candidates", record_id), record, immutable=True)
    return record


def promote_review_experience(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if args.reviewer != "cty41":
        raise PipelineError("review experience promotion requires reviewer cty41")
    candidate_path = store.record("review-experience-candidates", args.review_experience_candidate_record_id)
    candidate_record = load_json(candidate_path)
    candidate = candidate_record.get("candidate")
    if not isinstance(candidate, dict) or candidate.get("status") != "proposed":
        raise PipelineError("only proposed review experience candidates can be promoted")
    _iso_timestamp(args.promoted_at, "--promoted-at")
    payload = {"reviewExperienceCandidateRecordId": candidate_record["reviewExperienceCandidateRecordId"],
               "candidate": {"path": store.relative(candidate_path), "sha256": sha256_file(candidate_path)},
               "reviewer": "cty41", "reason": args.reason, "promotedAt": args.promoted_at,
               "activeRulesModified": False}
    promotion_id = stable_id("review-experience-promotion", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewExperiencePromotionId": promotion_id, **payload}
    write_json_idempotent(store.record("review-experience-promotions", promotion_id), record, immutable=True)
    return record


def record_prompt_only_comparison(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Freeze a like-for-like prompt-only baseline and Reviewer-loop raw-count comparison."""
    prompt_only, prompt_only_source = _source_json_artifact(store, args.prompt_only_arm_source)
    reviewer, reviewer_source = _source_json_artifact(store, args.reviewer_closed_loop_arm_source)
    try:
        comparison = artwork_review.validate_prompt_only_comparison(prompt_only, reviewer)
    except artwork_review.ReviewValidationError as exc:
        raise PipelineError(f"invalid prompt-only comparison: {exc}") from exc
    payload = {"promptOnly": comparison["promptOnly"], "reviewerClosedLoop": comparison["reviewerClosedLoop"],
               "sources": {"promptOnly": prompt_only_source, "reviewerClosedLoop": reviewer_source}}
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "promptComparisonId": comparison["promptComparisonId"], **payload}
    write_json_idempotent(store.record("prompt-only-comparisons", comparison["promptComparisonId"]), record, immutable=True)
    return record


def save_attempt(store: Store, attempt: dict[str, Any]) -> None:
    if attempt.get("state") not in STATES:
        raise PipelineError(f"invalid attempt state: {attempt.get('state')}")
    write_json_idempotent(store.record("attempts", attempt["attemptId"]), attempt)


def transition(attempt: dict[str, Any], expected: Iterable[str], target: str) -> None:
    if attempt["state"] == target:
        return
    if attempt["state"] not in set(expected):
        raise PipelineError(f"illegal transition {attempt['state']} -> {target}")
    attempt["state"] = target


def copy_bound(store: Store, source: str, destination: str) -> dict[str, str]:
    src_rel = store.relative(source, must_exist=True)
    dst_rel = store.relative(destination)
    src = store.absolute(src_rel)
    dst = store.absolute(dst_rel)
    digest = sha256_file(src)
    if dst.exists():
        if sha256_file(dst) != digest:
            raise PipelineError(f"destination exists with different bytes: {dst_rel}")
    else:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dst)
    return {"path": dst_rel, "sha256": digest}


def transaction_record(store: Store, operation: str, payload: dict[str, Any], state: str) -> tuple[str, dict[str, Any]]:
    transaction_id = stable_id("transaction", {"operation": operation, **payload})
    path = store.record("transactions", transaction_id)
    if path.is_file():
        record = load_json(path)
        if record.get("operation") != operation or record.get("payload") != payload:
            raise PipelineError("transaction identity collision")
    else:
        record = {"schemaVersion": 2, "transactionId": transaction_id,
                  "operation": operation, "payload": payload, "state": "started"}
    record["state"] = state
    write_json_idempotent(path, record)
    return transaction_id, record


def resolve_transaction(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Resolve a stranded transaction from hash-bound operation evidence."""
    if args.reviewer != "cty41":
        raise PipelineError("transaction resolution requires reviewer cty41")
    _iso_timestamp(args.decided_at, "--decided-at")
    path = store.record("transactions", args.transaction_id)
    record = load_json(path)
    if record.get("state") != "started":
        raise PipelineError("only a started transaction can receive an immutable resolution")
    for existing_path in (store.pipeline / "transaction-resolutions").glob("*.json"):
        existing = load_json(existing_path)
        if existing.get("transactionId") == record.get("transactionId"):
            return {**record, "state": existing["outcome"], "resolution": existing}
    operation, payload = record.get("operation"), record.get("payload", {})
    if operation != "prepare":
        raise PipelineError("automatic transaction resolution currently supports prepare only")
    attempt_path = store.record("attempts", payload.get("attemptId", ""))
    if not attempt_path.is_file():
        raise PipelineError("transaction attempt does not exist")
    attempt = load_json(attempt_path)
    prepared = attempt.get("artifacts", {}).get("prepared")
    preparation = attempt.get("preparation", {})
    def canonical_chroma(value: Any) -> str:
        text = str(value).strip().lower().replace("#", "")
        if "," in text:
            try:
                channels = [int(part.strip()) for part in text.split(",")]
            except ValueError as exc:
                raise PipelineError("transaction chroma metadata is invalid") from exc
            if len(channels) != 3 or any(channel < 0 or channel > 255 for channel in channels):
                raise PipelineError("transaction chroma metadata is invalid")
            return "".join(f"{channel:02x}" for channel in channels)
        return text
    expected_chroma = canonical_chroma(payload.get("chroma", ""))
    actual_chroma = canonical_chroma(preparation.get("chroma", ""))
    prepared_path = store.absolute(prepared.get("path", "")) if prepared else None
    artifact_present = bool(prepared or preparation)
    committed = bool(prepared and prepared_path and prepared_path.is_file()
                     and sha256_file(prepared_path) == prepared.get("sha256")
                     and expected_chroma == actual_chroma
                     and payload.get("chromaTolerance") == preparation.get("chromaTolerance"))
    if artifact_present and not committed:
        raise PipelineError("transaction has partial or conflicting preparation evidence; human investigation required")
    outcome = "committed" if committed else "aborted"
    resolution_payload = {"transactionId": record["transactionId"],
                          "transaction": {"path": store.relative(path), "sha256": sha256_file(path)},
                          "operation": operation, "payload": payload, "outcome": outcome,
                          "reviewer": "cty41", "reason": args.reason, "decidedAt": args.decided_at,
                          "evidence": prepared if committed else None}
    resolution_id = stable_id("transaction-resolution", resolution_payload)
    resolution = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION,
                  "transactionResolutionId": resolution_id, **resolution_payload}
    write_json_idempotent(store.record("transaction-resolutions", resolution_id), resolution, immutable=True)
    return {**record, "state": outcome, "resolution": resolution}


def ingest(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    source_rel = store.relative(args.source, must_exist=True)
    digest = sha256_file(store.absolute(source_rel))
    transaction_payload = {"attemptId": attempt["attemptId"], "sourceSha256": digest,
                           "invocationId": getattr(args, "invocation_id", None)}
    transaction_id, _ = transaction_record(store, "ingest", transaction_payload, "started")
    existing = attempt["artifacts"].get("raw")
    if existing:
        if existing["sha256"] != digest:
            raise PipelineError("attempt already contains a different ImageGen output")
        transaction_record(store, "ingest", transaction_payload, "committed")
        return attempt
    if attempt["state"] != "ready":
        raise PipelineError("ingest requires ready attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    invocation_id = getattr(args, "invocation_id", None)
    inherited_generation = None
    if job.get("requiresInvocation"):
        if attempt.get("technicalRemediation") and not invocation_id:
            parent_id = attempt.get("parentAttemptId")
            if not parent_id:
                raise PipelineError("technical remediation requires a parent attempt")
            parent = load_json(store.record("attempts", parent_id))
            parent_raw = parent.get("artifacts", {}).get("raw")
            if not parent_raw or parent_raw.get("sha256") != digest:
                raise PipelineError("technical remediation must reuse the exact parent ImageGen output")
            if not parent.get("generationInvocationId") or not parent.get("generationDeliveryId"):
                raise PipelineError("technical remediation parent is missing generation provenance")
            inherited_generation = {
                "generationInvocationId": parent["generationInvocationId"],
                "generationDeliveryId": parent["generationDeliveryId"],
            }
        elif not invocation_id:
            raise PipelineError("schema v2 ingest requires --invocation-id")
        else:
            invocation = load_json(store.record("generation-invocations", invocation_id))
            if invocation.get("attemptId") != attempt["attemptId"] or invocation.get("state") != "started":
                raise PipelineError("generation invocation does not match ready attempt")
    binding = job.get("series")
    if binding:
        series = load_json(store.record("series", binding["seriesId"]))
        pose = series_pose(series, binding["poseId"])
        unique_hashes = set()
        for attempt_id in pose["attemptIds"]:
            candidate = load_json(store.record("attempts", attempt_id))
            raw_hash = candidate.get("artifacts", {}).get("raw", {}).get("sha256")
            if raw_hash:
                unique_hashes.add(raw_hash)
        limit = series.get("maxUniqueOutputs")
        if limit is not None and digest not in unique_hashes and len(unique_hashes) >= limit:
            raise PipelineError(f"pose {pose['poseId']} already reached its unique ImageGen output limit")
    suffix = store.absolute(source_rel).suffix.lower() or ".png"
    dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / f"raw{suffix}"
    source_artifact = {"path": source_rel, "sha256": digest}
    attempt["artifacts"]["source"] = source_artifact
    attempt["artifacts"]["raw"] = copy_bound(store, source_rel, store.relative(dst))
    if invocation_id:
        delivery_payload = {"invocationId": invocation_id, "attemptId": attempt["attemptId"], "rawSha256": digest}
        delivery_id = stable_id("generation-delivery", delivery_payload)
        delivery = {"schemaVersion": 2, "generationDeliveryId": delivery_id, **delivery_payload}
        write_json_idempotent(store.record("generation-deliveries", delivery_id), delivery, immutable=True)
        attempt["generationInvocationId"] = invocation_id
        attempt["generationDeliveryId"] = delivery_id
    elif inherited_generation:
        attempt.update(inherited_generation)
    register_public_artifacts(store, [source_artifact, attempt["artifacts"]["raw"]], "project-owned-gpt-generated")
    transition(attempt, {"ready"}, "ingested")
    save_attempt(store, attempt)
    transaction_record(store, "ingest", transaction_payload, "committed")
    return attempt


def prepare_image(source: Path, destination: Path, chroma: str | None, chroma_tolerance: int = 0) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = pixel_data(image)
    key = None
    if chroma:
        value = chroma.lstrip("#")
        if len(value) != 6:
            raise PipelineError("--chroma must be RRGGBB")
        key = tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))
    if not 0 <= chroma_tolerance <= 255:
        raise PipelineError("--chroma-tolerance must be between 0 and 255")
    cleaned = []
    for red, green, blue, alpha in pixels:
        distance_squared = sum((value - target) ** 2 for value, target in zip((red, green, blue), key)) if key else None
        if key and distance_squared is not None and distance_squared <= chroma_tolerance ** 2:
            cleaned.append((0, 0, 0, 0))
        elif alpha == 0:
            cleaned.append((0, 0, 0, 0))
        else:
            cleaned.append((red, green, blue, alpha))
    image.putdata(cleaned)
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination, format="PNG", optimize=False, compress_level=9)


def clean_exact_chroma(image: Image.Image) -> Image.Image:
    """Remove reserved exact keys after resampling, without widening color tolerance."""
    result = image.convert("RGBA")
    result.putdata([(0, 0, 0, 0) if not alpha or (red, green, blue) in {(0, 255, 0), (255, 0, 255)}
                    else (red, green, blue, alpha)
                    for red, green, blue, alpha in pixel_data(result)])
    return result


def clean_resampled_chroma(image: Image.Image, chroma: str | None, tolerance: int) -> Image.Image:
    if not chroma:
        return normalize_transparent_rgb(image)
    value = chroma.lstrip("#")
    key = tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))
    cleaned = []
    for red, green, blue, alpha in pixel_data(image.convert("RGBA")):
        distance_squared = sum((channel - target) ** 2 for channel, target in zip((red, green, blue), key))
        reserved_key_residue = (
            (green > 180 and green > red + 80 and green > blue + 80)
            or (red > 180 and blue > 180 and red > green + 80 and blue > green + 80)
        )
        if distance_squared <= tolerance ** 2 or reserved_key_residue:
            cleaned.append((0, 0, 0, 0))
        elif alpha == 0:
            cleaned.append((0, 0, 0, 0))
        else:
            cleaned.append((red, green, blue, alpha))
    result = Image.new("RGBA", image.size)
    result.putdata(cleaned)
    return result


def prepare(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    transaction_payload = {"attemptId": attempt["attemptId"], "chroma": args.chroma,
                           "chromaTolerance": getattr(args, "chroma_tolerance", 0)}
    transaction_record(store, "prepare", transaction_payload, "started")
    preparation = {"chroma": args.chroma.lower().lstrip("#") if args.chroma else None,
                   "chromaTolerance": getattr(args, "chroma_tolerance", 0)}
    existing = attempt["artifacts"].get("prepared")
    if existing:
        if sha256_file(store.absolute(existing["path"], must_exist=True)) != existing["sha256"]:
            transaction_record(store, "prepare", transaction_payload, "aborted")
            raise PipelineError("prepared artifact hash mismatch")
        recorded = attempt.get("preparation")
        if recorded is not None and recorded != preparation:
            # This is an intentional no-op rejection, not an interrupted
            # transaction.  Preserve its audit record without blocking strict
            # checks for the otherwise valid attempt.
            transaction_record(store, "prepare", transaction_payload, "aborted")
            raise PipelineError("attempt already uses different preparation parameters")
        transaction_record(store, "prepare", transaction_payload, "committed")
        return attempt
    if attempt["state"] != "ingested":
        raise PipelineError("prepare requires ingested attempt")
    raw = store.absolute(attempt["artifacts"]["raw"]["path"], must_exist=True)
    if sha256_file(raw) != attempt["artifacts"]["raw"]["sha256"]:
        raise PipelineError("raw artifact hash mismatch")
    dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "prepared.png"
    prepare_image(raw, dst, args.chroma, preparation["chromaTolerance"])
    attempt["artifacts"]["prepared"] = {"path": store.relative(dst), "sha256": sha256_file(dst)}
    attempt["preparation"] = preparation
    register_public_artifacts(store, [attempt["artifacts"]["prepared"]], "project-owned-derived-artwork")
    transition(attempt, {"ingested"}, "prepared")
    save_attempt(store, attempt)
    transaction_record(store, "prepare", transaction_payload, "committed")
    return attempt


def attach_mask(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    prepared = store.absolute(attempt["artifacts"].get("prepared", {}).get("path", ""), must_exist=True)
    mask_rel = store.relative(args.mask, must_exist=True)
    mask = store.absolute(mask_rel)
    with Image.open(prepared) as base, Image.open(mask) as overlay:
        if base.size != overlay.size:
            raise PipelineError("semantic mask must use the prepared image coordinate system")
    digest = sha256_file(mask)
    existing = attempt["artifacts"].get("mask")
    if existing and existing["sha256"] != digest:
        raise PipelineError("attempt already contains a different semantic mask")
    if not existing:
        if attempt["state"] != "prepared":
            raise PipelineError("attach-mask requires prepared attempt")
        dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "mask.png"
        attempt["artifacts"]["maskSource"] = {"path": mask_rel, "sha256": digest}
        attempt["artifacts"]["mask"] = copy_bound(store, mask_rel, store.relative(dst))
        register_public_artifacts(store, [attempt["artifacts"]["maskSource"], attempt["artifacts"]["mask"]], "project-owned-semantic-mask")
        transition(attempt, {"prepared"}, "annotated")
        save_attempt(store, attempt)
    elif not attempt["artifacts"].get("maskSource"):
        attempt["artifacts"]["maskSource"] = {"path": mask_rel, "sha256": digest}
        register_public_artifacts(store, [attempt["artifacts"]["maskSource"]], "project-owned-semantic-mask")
        save_attempt(store, attempt)
    return attempt


def attach_identity_mask(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Bind an identity-only mask after core calibration.

    Identity markings deliberately use a separate flat mask so that a forehead
    blaze never punches a hole through the core geometry mask.
    """
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    if not contract.get("identitySpec"):
        raise PipelineError("identity mask requires an identity-spec contract")
    candidate = store.absolute(candidate_artifact(attempt).get("path", ""), must_exist=True)
    mask_rel = store.relative(args.mask, must_exist=True)
    mask = store.absolute(mask_rel)
    with Image.open(candidate) as image, Image.open(mask) as identity:
        if image.size != identity.size:
            raise PipelineError("identity mask must use the calibrated candidate coordinate system")
    digest = sha256_file(mask)
    existing = attempt["artifacts"].get("identityMask")
    if existing and existing["sha256"] != digest:
        raise PipelineError("attempt already contains a different identity mask")
    if not existing:
        dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "identity-mask.png"
        attempt["artifacts"]["identityMaskSource"] = {"path": mask_rel, "sha256": digest}
        attempt["artifacts"]["identityMask"] = copy_bound(store, mask_rel, store.relative(dst))
        register_public_artifacts(store, [attempt["artifacts"]["identityMaskSource"], attempt["artifacts"]["identityMask"]],
                                  "project-owned-identity-mask")
        save_attempt(store, attempt)
    return attempt


def candidate_artifact(attempt: dict[str, Any]) -> dict[str, str]:
    return attempt.get("artifacts", {}).get("calibrated") or attempt.get("artifacts", {}).get("prepared", {})


def candidate_mask_artifact(attempt: dict[str, Any]) -> dict[str, str]:
    return attempt.get("artifacts", {}).get("calibratedMask") or attempt.get("artifacts", {}).get("mask", {})


def normalize_transparent_rgb(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    rgba.putdata([(0, 0, 0, 0) if alpha == 0 else (red, green, blue, alpha)
                  for red, green, blue, alpha in pixel_data(rgba)])
    return rgba


def calibrate_core(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    existing_image = attempt.get("artifacts", {}).get("calibrated")
    existing_mask = attempt.get("artifacts", {}).get("calibratedMask")
    if existing_image or existing_mask:
        if not existing_image or not existing_mask:
            raise PipelineError("calibration artifacts are incomplete")
        for artifact in (existing_image, existing_mask):
            if sha256_file(store.absolute(artifact["path"], must_exist=True)) != artifact["sha256"]:
                raise PipelineError("calibration artifact hash mismatch")
        return attempt
    if attempt["state"] != "annotated":
        raise PipelineError("calibrate-core requires annotated attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    anchor = contract.get("anchor") or {}
    anchor_mask_artifact = ({"path": anchor["maskPath"], "sha256": anchor["maskSha256"]}
                            if anchor.get("maskPath") else approved_anchor_mask(store, anchor.get("sha256", "")))
    if not anchor_mask_artifact:
        raise PipelineError("calibrate-core requires an approved anchor mask")
    source_image = Image.open(store.absolute(attempt["artifacts"]["prepared"]["path"], must_exist=True)).convert("RGBA")
    source_mask = Image.open(store.absolute(attempt["artifacts"]["mask"]["path"], must_exist=True)).convert("RGBA")
    anchor_mask = Image.open(store.absolute(anchor_mask_artifact["path"], must_exist=True)).convert("RGBA")
    source_box = bbox_for(pixel_data(source_mask), source_mask.size, MASK_COLORS["core"])
    anchor_box = anchor_core_bbox(anchor_mask)
    if not source_box or not anchor_box:
        raise PipelineError("source and anchor masks must both contain a core region")
    source_height = source_box[3] - source_box[1] + 1
    anchor_height = anchor_box[3] - anchor_box[1] + 1
    scale = anchor_height / source_height
    scaled_size = (max(1, round(source_image.width * scale)), max(1, round(source_image.height * scale)))
    scaled_image = source_image.resize(scaled_size, Image.Resampling.LANCZOS)
    scaled_mask = source_mask.resize(scaled_size, Image.Resampling.NEAREST)
    source_center = ((source_box[0] + source_box[2]) / 2, (source_box[1] + source_box[3]) / 2)
    anchor_center = ((anchor_box[0] + anchor_box[2]) / 2, (anchor_box[1] + anchor_box[3]) / 2)
    offset = (round(anchor_center[0] - source_center[0] * scale),
              round(anchor_center[1] - source_center[1] * scale))
    if contract["kind"] in {"ground_character", "action_pose"}:
        scaled_pixels = pixel_data(scaled_mask)
        foot_boxes = [bbox_for(scaled_pixels, scaled_mask.size, MASK_COLORS[label]) for label in ("near_foot", "far_foot")]
        foot_bottoms = [box[3] for box in foot_boxes if box]
        if foot_bottoms:
            offset = (offset[0], 236 - max(foot_bottoms))
    output_image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    output_mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    output_image.alpha_composite(scaled_image, offset)
    output_mask.alpha_composite(scaled_mask, offset)
    preparation = attempt.get("preparation", {})
    output_image = clean_resampled_chroma(output_image, preparation.get("chroma"), preparation.get("chromaTolerance", 0))
    out_dir = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"]
    image_path = out_dir / "calibrated.png"
    mask_path = out_dir / "calibrated-mask.png"
    output_image.save(image_path, format="PNG", optimize=False, compress_level=9)
    output_mask.save(mask_path, format="PNG", optimize=False, compress_level=9)
    attempt["artifacts"]["calibrated"] = {"path": store.relative(image_path), "sha256": sha256_file(image_path)}
    attempt["artifacts"]["calibratedMask"] = {"path": store.relative(mask_path), "sha256": sha256_file(mask_path)}
    attempt["calibration"] = {"method": "uniform-core-height", "scale": scale, "offset": list(offset),
                              "sourceCoreBbox": list(source_box), "anchorCoreBbox": list(anchor_box),
                              "anchorMask": anchor_mask_artifact}
    register_public_artifacts(store, [attempt["artifacts"]["calibrated"], attempt["artifacts"]["calibratedMask"]],
                              "project-owned-deterministically-core-calibrated-artwork")
    transition(attempt, {"annotated"}, "calibrated")
    save_attempt(store, attempt)
    return attempt


def bbox_for(pixels: list[tuple[int, int, int, int]], size: tuple[int, int], color: tuple[int, int, int, int]) -> tuple[int, int, int, int] | None:
    width, _ = size
    points = [(index % width, index // width) for index, value in enumerate(pixels) if value == color]
    if not points:
        return None
    xs, ys = zip(*points)
    return min(xs), min(ys), max(xs), max(ys)


def anchor_core_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    """Read either a semantic red core mask or an approved legacy binary core mask."""
    rgba = image.convert("RGBA")
    pixels = pixel_data(rgba)
    semantic = bbox_for(pixels, rgba.size, MASK_COLORS["core"])
    if semantic:
        return semantic
    opaque_colors = {value for value in pixels if value[3]}
    binary_colors = {(0, 0, 0, 255), (255, 255, 255, 255)}
    if opaque_colors and opaque_colors <= binary_colors and (255, 255, 255, 255) in opaque_colors:
        return bbox_for(pixels, rgba.size, (255, 255, 255, 255))
    return None


def inspect_technical(path: Path, kind: str, *, require_master_canvas: bool = True,
                      expected_master_size: tuple[int, int] = (256, 256)) -> tuple[dict[str, Any], list[str]]:
    image = Image.open(path).convert("RGBA")
    pixels = pixel_data(image)
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    issues = []
    if require_master_canvas and image.size != expected_master_size:
        issues.append("master_size_mismatch")
    max_x, max_y = image.width - 1, image.height - 1
    if require_master_canvas and any(image.getpixel(point)[3] != 0 for point in ((0, 0), (max_x, 0), (0, max_y), (max_x, max_y))):
        issues.append("corner_not_transparent")
    if any(a == 0 and (r or g or b) for r, g, b, a in pixels):
        issues.append("transparent_rgb_nonzero")
    if any(a and ((r, g, b) == (0, 255, 0) or (r, g, b) == (255, 0, 255)) for r, g, b, a in pixels):
        issues.append("exact_chroma_residue")
    if any(a and (abs(r) <= 12 and abs(g - 255) <= 12 and abs(b) <= 12 or abs(r - 255) <= 12 and abs(g) <= 12 and abs(b - 255) <= 12)
           for r, g, b, a in pixels):
        issues.append("chroma_fringe")
    if bbox is None:
        issues.append("empty_alpha")
    return {"size": list(image.size), "alphaBbox": list(bbox) if bbox else None}, issues


def composition_gem_issues(weapon: dict[str, Any], regions: dict[str, Any]) -> list[str]:
    """Validate the manually annotated unique gem against a v2 composition spec."""
    gem_window = weapon.get("gemWindow")
    if not gem_window:
        return []
    gem = regions.get("gemRegion")
    if not gem:
        return ["gem_missing"]
    def overlap(a: list[int], b: list[int]) -> bool:
        return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])
    issues = []
    gem_area = max(0, gem[2] - gem[0] + 1) * max(0, gem[3] - gem[1] + 1)
    if gem_area > weapon.get("maxGemAreaPx", gem_area):
        issues.append("gem_too_large")
    if not overlap(gem, gem_window):
        issues.append("gem_outside_guard_window")
    for forbidden in weapon.get("forbiddenGemRegions", []):
        if overlap(gem, forbidden):
            issues.append("gem_enters_forbidden_region")
    if regions.get("extraGemRegions"):
        issues.append("extra_gem_present")
    return issues


def composition_blade_issues(weapon: dict[str, Any], regions: dict[str, Any]) -> list[str]:
    """Validate a high-risk sword's annotated dimensions against its composition."""
    blade = regions.get("weaponBlade")
    if not blade:
        return ["weapon_blade_annotation_missing"] if weapon.get("bladeCenterline") else []
    width = blade[2] - blade[0] + 1
    height = blade[3] - blade[1] + 1
    issues = []
    if width < weapon.get("minBladeWidthPx", width):
        issues.append("weapon_blade_too_thin")
    if width > weapon.get("maxBladeWidthPx", width):
        issues.append("weapon_blade_too_wide")
    length_range = weapon.get("bladeLengthRangePx")
    if length_range:
        length = max(width, height)
        if length < length_range[0]:
            issues.append("weapon_blade_too_short")
        if length > length_range[1]:
            issues.append("weapon_blade_too_long")
    return issues


def composition_eye_occlusion_issues(spec: dict[str, Any], regions: dict[str, Any]) -> list[str]:
    eye_rule = spec.get("eyeOcclusion")
    if not eye_rule:
        return []
    blade = regions.get("weaponBlade")
    left_eye, right_eye = regions.get("leftEyeRegion"), regions.get("rightEyeRegion")
    if not blade or not left_eye or not right_eye:
        return ["eye_occlusion_annotations_missing"]
    def overlap(a: list[int], b: list[int]) -> bool:
        return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])
    issues = []
    if eye_rule.get("bladeOverlapsBothInnerEyes") and (not overlap(blade, left_eye) or not overlap(blade, right_eye)):
        issues.append("blade_does_not_occlude_both_inner_eyes")
    left_center, right_center = (left_eye[0] + left_eye[2]) / 2, (right_eye[0] + right_eye[2]) / 2
    if right_center - left_center > eye_rule.get("maxEyeCenterGapPx", float("inf")):
        issues.append("eye_center_gap_too_wide")
    return issues


def identity_mask_issues(store: Store, contract: dict[str, Any], attempt: dict[str, Any]) -> list[str]:
    spec = contract.get("identitySpec")
    if not spec:
        return []
    artifact = attempt.get("artifacts", {}).get("identityMask")
    if not artifact:
        return ["identity_mask_missing"]
    anchor_path = store.absolute(spec["anchorMaskPath"], must_exist=True)
    candidate_path = store.absolute(artifact["path"], must_exist=True)
    if sha256_file(anchor_path) != spec["anchorMaskSha256"]:
        return ["identity_anchor_mask_hash_mismatch"]
    anchor = Image.open(anchor_path).convert("RGBA")
    candidate = Image.open(candidate_path).convert("RGBA")
    if anchor.size != candidate.size:
        return ["identity_mask_size_mismatch"]
    issues: list[str] = []
    for label, color in IDENTITY_MASK_COLORS.items():
        anchor_points = {index for index, value in enumerate(pixel_data(anchor)) if value == color}
        candidate_points = {index for index, value in enumerate(pixel_data(candidate)) if value == color}
        if not anchor_points:
            issues.append(f"identity_anchor_{label}_missing")
            continue
        if not candidate_points:
            issues.append(f"identity_{label}_missing")
            continue
        if label != "forehead_blaze":
            continue
        union = anchor_points | candidate_points
        iou = len(anchor_points & candidate_points) / max(1, len(union))
        ratio = len(candidate_points) / len(anchor_points)
        if iou < spec.get("foreheadBlazeMinIou", 0.45):
            issues.append("forehead_blaze_shape_mismatch")
        minimum, maximum = spec.get("foreheadBlazeAreaRatio", [0.65, 1.45])
        if not minimum <= ratio <= maximum:
            issues.append("forehead_blaze_area_out_of_range")
    return issues


def geometry_checks(store: Store, contract: dict[str, Any], attempt: dict[str, Any]) -> tuple[dict[str, Any], list[str]]:
    mask_artifact = candidate_mask_artifact(attempt)
    if contract["maskRequired"] and not mask_artifact:
        return {}, ["semantic_mask_missing"]
    if not mask_artifact:
        return {}, []
    mask = Image.open(store.absolute(mask_artifact["path"], must_exist=True)).convert("RGBA")
    pixels = pixel_data(mask)
    candidate = Image.open(store.absolute(candidate_artifact(attempt)["path"], must_exist=True)).convert("RGBA")
    if candidate.size != mask.size:
        return {}, ["semantic_mask_size_mismatch"]
    candidate_pixels = pixel_data(candidate)
    boxes = {label: bbox_for(pixels, mask.size, color) for label, color in MASK_COLORS.items()}
    issues = []
    allowed = set(MASK_COLORS.values()) | {(0, 0, 0, 0)}
    if any(value not in allowed for value in pixels):
        issues.append("semantic_mask_unknown_color")
    if any(a == 0 and (r or g or b) for r, g, b, a in pixels):
        issues.append("semantic_mask_transparent_rgb_nonzero")
    if any(mask_value[3] and not candidate_value[3] for mask_value, candidate_value in zip(pixels, candidate_pixels)):
        issues.append("semantic_mask_outside_subject")
    core = boxes["core"]
    if core is None:
        issues.append("core_missing")
    anchor = contract.get("anchor") or {}
    anchor_mask_artifact = ({"path": anchor["maskPath"], "sha256": anchor["maskSha256"]}
                            if anchor.get("maskPath") else approved_anchor_mask(store, anchor.get("sha256", "")))
    if contract.get("anchor") and not anchor_mask_artifact:
        issues.append("anchor_mask_missing")
    if contract["noArms"]:
        for label in ("near_arm", "far_arm", "near_leg", "far_leg"):
            if boxes[label] is not None:
                issues.append(f"{label}_forbidden")
        for label in ("near_hand", "far_hand", "near_foot", "far_foot"):
            if boxes[label] is None:
                issues.append(f"{label}_missing")
        if core:
            width = mask.width
            core_points = {i for i, value in enumerate(pixels) if value == MASK_COLORS["core"]}
            equipment_points = {i for i, value in enumerate(pixels) if value == MASK_COLORS["equipment"]}
            for label in ("near_hand", "far_hand", "near_foot", "far_foot"):
                part_points = {i for i, value in enumerate(pixels) if value == MASK_COLORS[label]}
                # A hand in a component assembly may be physically separated from
                # the visible core by the held equipment drawn between them.  The
                # equipment is then the stable contact surface; feet must still
                # contact the core directly.
                contact_points = core_points
                if contract.get("assetRole") == "assembled_sprite" and label in {"near_hand", "far_hand"}:
                    contact_points = core_points | equipment_points
                contacts = 0
                for index in part_points:
                    x, y = index % width, index // width
                    if any((ny * width + nx) in contact_points for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)) if 0 <= nx < width and 0 <= ny < mask.height):
                        contacts += 1
                if part_points and contacts < 3:
                    issues.append(f"{label}_contact_lt_3")
    if contract["kind"] in {"ground_character", "action_pose"}:
        foot_bottoms = [boxes[label][3] for label in ("near_foot", "far_foot") if boxes[label]]
        # The semantic foot mask is the deterministic virtual ground contact.
        # Full-sprite alpha can extend several rows lower because of antialiasing
        # or rear equipment, neither of which changes the character baseline.
        if foot_bottoms and max(foot_bottoms) not in {235, 236, 237}:
            issues.append("baseline_not_236")
    metrics: dict[str, Any] = {"boxes": {key: list(value) if value else None for key, value in boxes.items()}}
    if core:
        left, top, right, bottom = core
        foot_tops = [boxes[label][1] for label in ("near_foot", "far_foot") if boxes[label]]
        uninterrupted_bottom = min(foot_tops) - 1 if foot_tops else bottom
        row_widths = []
        core_rows: dict[int, list[int]] = {}
        for y in range(top, bottom + 1):
            xs = [x for x in range(mask.width) if pixels[y * mask.width + x] == MASK_COLORS["core"]]
            core_rows[y] = xs
            row_widths.append(max(xs) - min(xs) + 1 if xs else 0)
            # Feet legitimately occlude the lower capsule. Continuity is a
            # core-hull rule, so rows in the foot-overlap band are excluded.
            allowed_occluders = ("near_hand", "far_hand") if contract.get("assetRole") == "component" else ()
            if contract.get("assetRole") == "assembled_sprite":
                allowed_occluders = ("equipment", "near_hand", "far_hand")
            occluded_row = any(boxes[label] and boxes[label][1] <= y <= boxes[label][3] for label in allowed_occluders)
            if y <= uninterrupted_bottom and xs and len(xs) != max(xs) - min(xs) + 1 and not occluded_row:
                issues.append("core_row_disconnected")
        shape_row_widths = row_widths
        # Front-layer equipment legitimately replaces visible core labels in an
        # assembled mask.  The bound approved anchor remains the authoritative
        # immutable body shape for the three-band capsule check.
        if contract.get("assetRole") == "assembled_sprite" and anchor_mask_artifact:
            with Image.open(store.absolute(anchor_mask_artifact["path"], must_exist=True)) as anchor_source:
                anchor_shape_mask = anchor_source.convert("RGBA")
            anchor_shape_pixels = pixel_data(anchor_shape_mask)
            anchor_shape_box = anchor_core_bbox(anchor_shape_mask)
            if anchor_shape_box:
                _anchor_left, anchor_top, _anchor_right, anchor_bottom = anchor_shape_box
                shape_row_widths = []
                for y in range(anchor_top, anchor_bottom + 1):
                    xs = [x for x in range(anchor_shape_mask.width)
                          if anchor_shape_pixels[y * anchor_shape_mask.width + x] == MASK_COLORS["core"]]
                    shape_row_widths.append(max(xs) - min(xs) + 1 if xs else 0)
        height = len(shape_row_widths)
        bands = [shape_row_widths[:max(1, height // 3)],
                 shape_row_widths[height // 3:max(height // 3 + 1, 2 * height // 3)],
                 shape_row_widths[2 * height // 3:]]
        widths = [max(band or [0]) for band in bands]
        metrics["core"] = {"bbox": [left, top, right, bottom], "center": [(left + right) / 2, (top + bottom) / 2], "bandMaxWidths": widths}
        if widths[2] > widths[1]:
            issues.append("core_lower_wider_than_middle")
        core_center_x = (left + right) / 2
        for label, side in contract.get("handSides", {}).items():
            box = boxes.get(label)
            if box and side and contract.get("assetRole") != "component":
                hand_center_x = (box[0] + box[2]) / 2
                if (side == "left" and hand_center_x >= core_center_x) or (side == "right" and hand_center_x <= core_center_x):
                    issues.append(f"{label}_wrong_side")
        if anchor and anchor_mask_artifact:
            anchor_mask = Image.open(store.absolute(anchor_mask_artifact["path"], must_exist=True)).convert("RGBA")
            anchor_pixels = pixel_data(anchor_mask)
            anchor_box = anchor_core_bbox(anchor_mask)
            if anchor_box:
                tol_size = contract["tolerances"]["sizePx"]
                tol_center = contract["tolerances"]["centerPx"]
                aw, ah = anchor_box[2] - anchor_box[0] + 1, anchor_box[3] - anchor_box[1] + 1
                cw, ch = right - left + 1, bottom - top + 1
                if abs(aw - cw) > tol_size or abs(ah - ch) > tol_size:
                    issues.append("core_size_out_of_tolerance")
                if abs((anchor_box[0] + anchor_box[2] - left - right) / 2) > tol_center or abs((anchor_box[1] + anchor_box[3] - top - bottom) / 2) > tol_center:
                    issues.append("core_center_out_of_tolerance")
                occlusion = contract.get("occlusion") or {}
                core_count = sum(1 for value in pixels if value == MASK_COLORS["core"])
                anchor_core_points = {index for index, value in enumerate(anchor_pixels) if value == MASK_COLORS["core"]}
                intrusion_counts: dict[str, int] = {}
                visible_ratios: dict[str, float] = {}
                for label, rule in occlusion.get("layerRules", {}).items():
                    label_points = {index for index, value in enumerate(pixels) if value == MASK_COLORS[label]}
                    if not label_points:
                        issues.append(f"{label}_missing_for_layer_rule")
                    if rule == "behind-core":
                        intrusion = 0
                        for index in label_points:
                            x, y = index % mask.width, index // mask.width
                            row = core_rows.get(y, [])
                            if row and min(row) < x < max(row):
                                intrusion += 1
                        intrusion_counts[label] = intrusion
                        if intrusion:
                            issues.append(f"{label}_intrudes_core")
                    ratio = len(label_points) / max(1, core_count)
                    visible_ratios[label] = ratio
                    cap = occlusion.get("visibilityCaps", {}).get(label)
                    if cap is not None and ratio > cap:
                        issues.append(f"{label}_visibility_cap_exceeded")
                metrics["occlusion"] = {"intrusionPixels": intrusion_counts, "visibleAreaRatios": visible_ratios}
        composition_ref = contract.get("compositionSpec")
        if composition_ref:
            composition = load_json(store.record("compositions", composition_ref["compositionId"]))
            spec = composition["spec"]
            annotation_id = attempt.get("annotationId")
            if not annotation_id and not spec.get("bodyLayer"):
                issues.append("annotations_missing")
            elif annotation_id:
                annotations = load_json(store.record("annotations", annotation_id))
                top_xs = core_rows.get(top, [])
                bottom_xs = core_rows.get(uninterrupted_bottom, [])
                if top_xs and bottom_xs:
                    dx = ((min(top_xs) + max(top_xs)) - (min(bottom_xs) + max(bottom_xs))) / 2
                    dy = max(1, uninterrupted_bottom - top)
                    import math
                    angle = math.degrees(math.atan2(dx, dy))
                    metrics.setdefault("composition", {})["coreTiltDegrees"] = angle
                    minimum, maximum = spec["coreAxis"]["tiltDegrees"]
                    if not minimum <= angle <= maximum:
                        issues.append("pose_axis_out_of_range")
                regions = annotations["regions"]
                def overlap(a: list[int], b: list[int]) -> bool:
                    return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])
                if spec.get("bodyLayer"):
                    if any(regions.get(label) for label in ("weaponBlade", "guardRegion", "scabbard", "staticEffect")):
                        issues.append("body_layer_contains_weapon_or_effect_annotation")
                else:
                    exit_box = regions["weaponExit"]
                    if not overlap(exit_box, spec["weapon"]["exitWindow"]):
                        issues.append("weapon_exit_outside_window")
                    weapon_tip = regions["weaponTip"]
                    if weapon_tip is None:
                        if not spec["weapon"].get("tipMayBeOccluded"):
                            issues.append("weapon_tip_annotation_missing")
                    elif not overlap(weapon_tip, spec["weapon"]["tipRegion"]):
                        issues.append("weapon_tip_outside_region")
                blade_corridor = spec["weapon"].get("bladeCenterline")
                if blade_corridor and not spec.get("bodyLayer"):
                    blade = regions.get("weaponBlade")
                    if not blade:
                        issues.append("weapon_blade_annotation_missing")
                    else:
                        blade_width = blade[2] - blade[0] + 1
                        blade_height = blade[3] - blade[1] + 1
                        metrics.setdefault("composition", {})["bladeWidthPx"] = blade_width
                        metrics.setdefault("composition", {})["bladeLengthPx"] = max(blade_width, blade_height)
                        issues.extend(composition_blade_issues(spec["weapon"], regions))
                        if not overlap(blade, blade_corridor):
                            issues.append("weapon_blade_outside_centerline")
                issues.extend(composition_eye_occlusion_issues(spec, regions))
                guard_window = spec["weapon"].get("guardWindow")
                if guard_window and not spec.get("bodyLayer"):
                    guard = regions.get("guardRegion")
                    if not guard:
                        issues.append("guard_annotation_missing")
                    elif not overlap(guard, guard_window):
                        issues.append("guard_outside_window")
                equipment_box = boxes.get("equipment")
                if equipment_box:
                    for forbidden in spec["forbiddenRegions"]:
                        if overlap(list(equipment_box), forbidden["rect"]):
                            issues.append(f"equipment_enters_forbidden_{forbidden['name']}")
                if not spec.get("bodyLayer"):
                    issues.extend(composition_gem_issues(spec["weapon"], regions))
                    if spec["equipmentState"].get("scabbard") == "absent" and regions.get("scabbard"):
                        issues.append("scabbard_present")
                    if spec["equipmentState"].get("staticEffects") == "absent" and regions.get("staticEffect"):
                        issues.append("static_effect_present")
    issues.extend(identity_mask_issues(store, contract, attempt))
    return metrics, issues


def validate_attempt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] in {"review_pending", "technical_failed"} and attempt.get("report"):
        report = load_json(store.absolute(attempt["report"]["path"], must_exist=True))
        if sha256_file(store.absolute(attempt["report"]["path"])) != attempt["report"]["sha256"]:
            raise PipelineError("validation report hash mismatch")
        return report
    if attempt["state"] not in {"prepared", "annotated", "calibrated"}:
        raise PipelineError("validate requires prepared, annotated, or calibrated attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    if contract.get("occlusion") and not attempt.get("artifacts", {}).get("calibrated"):
        raise PipelineError("occlusion contracts require calibrate-core before validation")
    candidate = candidate_artifact(attempt)
    prepared = store.absolute(candidate["path"], must_exist=True)
    technical, issues = inspect_technical(
        prepared, contract["kind"], require_master_canvas=contract.get("assetRole") != "component",
        expected_master_size=tuple(contract.get("canvasSpec", {}).get("masterSize", [256, 256])))
    if contract.get("assetRole") == "component" and contract.get("componentKind") != "body":
        mask_artifact = candidate_mask_artifact(attempt)
        geometry = {"componentKind": contract.get("componentKind")}
        geometry_issues = []
        if contract.get("maskRequired") and not mask_artifact:
            geometry_issues.append("component_mask_missing")
        elif mask_artifact:
            with Image.open(store.absolute(mask_artifact["path"], must_exist=True)) as mask_source, Image.open(prepared) as prepared_source:
                mask_image = mask_source.convert("RGBA")
                prepared_size = prepared_source.size
            if mask_image.size != prepared_size:
                geometry_issues.append("component_mask_size_mismatch")
            if not any(pixel[3] for pixel in pixel_data(mask_image)):
                geometry_issues.append("component_mask_empty")
    else:
        geometry, geometry_issues = geometry_checks(store, contract, attempt)
    issues.extend(geometry_issues)
    report = {"schemaVersion": 1, "attemptId": attempt["attemptId"], "inputSha256": sha256_file(prepared),
              "maskSha256": candidate_mask_artifact(attempt).get("sha256"), "technical": technical,
              "geometry": geometry, "issues": sorted(set(issues)), "passed": not issues}
    report_id = stable_id("report", report)
    report_path = store.pipeline / "reports" / f"{report_id}.json"
    write_json_idempotent(report_path, report, immutable=True)
    attempt["report"] = {"path": store.relative(report_path), "sha256": sha256_file(report_path)}
    attempt["state"] = "review_pending" if report["passed"] else "technical_failed"
    save_attempt(store, attempt)
    return report


def make_preview(master: Image.Image) -> Image.Image:
    preview = master.resize((128, 128), Image.Resampling.LANCZOS).convert("RGBA")
    preview.putdata([(0, 0, 0, 0) if alpha < 8 else (red, green, blue, alpha)
                     for red, green, blue, alpha in pixel_data(preview)])
    return preview


def render_tile_placement_review(prepared: Image.Image, spec: dict[str, Any]) -> tuple[Image.Image, dict[str, Any]]:
    tile_width, tile_height = 64, 32
    footprint_width, footprint_height = spec["footprintTiles"]
    scale = float(spec["displayScale"])
    anchor_x, anchor_y = spec["groundAnchorPx"]
    scaled_size = (round(prepared.width * scale), round(prepared.height * scale))
    scaled = prepared.resize(scaled_size, Image.Resampling.LANCZOS)
    board_role = spec.get("boardRole")
    screen_facing = spec.get("screenFacing")
    contextual = board_role in {"player", "target"}
    canvas_width = max(320 if contextual else 256, scaled_size[0] + (128 if contextual else tile_width))
    canvas_height = max(224 if contextual else 160, scaled_size[1] + (96 if contextual else tile_height * 2))
    if board_role == "target":
        front_center = (canvas_width // 2 + 48, canvas_height - 80)
        reference_center = (front_center[0] - 96, front_center[1] + 48)
    elif board_role == "player":
        front_center = (canvas_width // 2 - 48, canvas_height - 32)
        reference_center = (front_center[0] + 96, front_center[1] - 48)
    else:
        front_center = (canvas_width // 2, canvas_height - tile_height)
        reference_center = None
    scaled_anchor = (round(anchor_x * scale), round(anchor_y * scale))
    sprite_origin = (front_center[0] - scaled_anchor[0], front_center[1] - scaled_anchor[1])
    review = Image.new("RGBA", (canvas_width, canvas_height), (36, 36, 42, 255))
    draw = ImageDraw.Draw(review)
    if reference_center is not None:
        draw.polygon(((reference_center[0] - tile_width // 2, reference_center[1]),
                      (reference_center[0], reference_center[1] - tile_height // 2),
                      (reference_center[0] + tile_width // 2, reference_center[1]),
                      (reference_center[0], reference_center[1] + tile_height // 2)),
                     fill=(76, 62, 46, 255), outline=(226, 181, 92, 255), width=2)
        start, end = (reference_center, front_center) if board_role == "target" else (front_center, reference_center)
        draw.line((*start, *end), fill=(226, 181, 92, 255), width=2)
    tile_centers = []
    for row in range(footprint_height):
        for column in range(footprint_width):
            center = (
                front_center[0] + (column - row - (footprint_width - footprint_height)) * tile_width // 2,
                front_center[1] - ((footprint_width - 1 - column) + (footprint_height - 1 - row)) * tile_height // 2,
            )
            tile_centers.append(list(center))
            draw.polygon(((center[0] - tile_width // 2, center[1]),
                          (center[0], center[1] - tile_height // 2),
                          (center[0] + tile_width // 2, center[1]),
                          (center[0], center[1] + tile_height // 2)),
                         fill=(53, 66, 82, 255), outline=(157, 180, 204, 255), width=2)
    review.alpha_composite(scaled, sprite_origin)
    draw.line((front_center[0] - 5, front_center[1], front_center[0] + 5, front_center[1]),
              fill=(255, 80, 80, 255), width=1)
    draw.line((front_center[0], front_center[1] - 5, front_center[0], front_center[1] + 5),
              fill=(255, 80, 80, 255), width=1)
    metrics = {
        "masterSize": list(prepared.size),
        "displayScale": scale,
        "displaySize": list(scaled_size),
        "footprintTiles": [footprint_width, footprint_height],
        "tileCenters": tile_centers,
        "logicalTileCenter": list(front_center),
        "groundAnchorPx": [anchor_x, anchor_y],
        "scaledGroundAnchorPx": list(scaled_anchor),
        "spriteOrigin": list(sprite_origin),
        "anchorScreenPoint": [sprite_origin[0] + scaled_anchor[0], sprite_origin[1] + scaled_anchor[1]],
        "anchorMode": spec["anchorMode"],
        "boardRole": board_role,
        "screenFacing": screen_facing,
        "playerReferenceTileCenter": list(reference_center) if board_role == "target" else None,
        "targetReferenceTileCenter": list(reference_center) if board_role == "player" else list(front_center) if board_role == "target" else None,
        "explorationDirection": "up_right" if contextual else None,
    }
    return review, metrics


def render_anchor_tile_compare(prepared: Image.Image, anchor: Image.Image,
                               identity_spec: dict[str, Any] | None, store: Store) -> Image.Image:
    """Show the formal Idle and candidate on identical 64x32 isometric tiles."""
    image = Image.new("RGBA", (384, 160), (36, 36, 42, 255))
    draw = ImageDraw.Draw(image)
    for center_x in (96, 288):
        draw.polygon(((center_x - 32, 144), (center_x, 128), (center_x + 32, 144), (center_x, 159)),
                     fill=(94, 96, 104, 255), outline=(160, 162, 170, 255))
        draw.line(((center_x, 124), (center_x, 159)), fill=(80, 210, 255, 220), width=1)
    image.alpha_composite(make_preview(anchor), (32, 26))
    image.alpha_composite(make_preview(prepared), (224, 26))
    draw.text((42, 6), "Idle DR", fill=(230, 230, 235, 255))
    draw.text((226, 6), "Candidate", fill=(230, 230, 235, 255))
    if identity_spec:
        identity = Image.open(store.absolute(identity_spec["anchorMaskPath"], must_exist=True)).convert("RGBA")
        box = bbox_for(pixel_data(identity), identity.size, IDENTITY_MASK_COLORS["forehead_blaze"])
        if box:
            scaled = tuple(round(value / 2) for value in box)
            draw.rectangle((32 + scaled[0], 26 + scaled[1], 32 + scaled[2], 26 + scaled[3]), outline=(255, 255, 255, 220), width=1)
            draw.rectangle((224 + scaled[0], 26 + scaled[1], 224 + scaled[2], 26 + scaled[3]), outline=(255, 255, 255, 220), width=1)
    return image


def render_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] not in {"review_pending", "technical_failed", "approved", "rejected", "promoted"}:
        raise PipelineError("render-review requires a completed validation")
    prepared = Image.open(store.absolute(candidate_artifact(attempt)["path"], must_exist=True)).convert("RGBA")
    mask_artifact = candidate_mask_artifact(attempt)
    overlay = prepared.copy()
    if mask_artifact:
        mask = Image.open(store.absolute(mask_artifact["path"], must_exist=True)).convert("RGBA")
        tint = Image.new("RGBA", mask.size, (0, 0, 0, 0))
        tint.putdata([(r, g, b, 96 if a else 0) for r, g, b, a in pixel_data(mask)])
        overlay = Image.alpha_composite(overlay, tint)
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    anchor_art = contract.get("anchor")
    panel_width, panel_height = prepared.size
    review = Image.new(
        "RGBA", (panel_width * (3 if anchor_art else 2), panel_height), (36, 36, 42, 255))
    review.alpha_composite(prepared, (0, 0))
    review.alpha_composite(overlay, (panel_width, 0))
    if anchor_art:
        anchor = Image.open(store.absolute(anchor_art["path"], must_exist=True)).convert("RGBA")
        review.alpha_composite(anchor, (panel_width * 2, 0))
    preview = make_preview(prepared)
    placement_metrics = None
    if contract.get("tilePlacementSpec"):
        tile, placement_metrics = render_tile_placement_review(prepared, contract["tilePlacementSpec"])
    else:
        tile = Image.new("RGBA", (256, 160), (36, 36, 42, 255))
        tile_draw = ImageDraw.Draw(tile)
        tile_draw.polygon(((96, 144), (128, 128), (160, 144), (128, 159)), fill=(94, 96, 104, 255), outline=(160, 162, 170, 255))
        tile.alpha_composite(preview, (64, 26))  # preview anchor (64,118) -> tile center (128,144)
    out_dir = store.pipeline / "reviews" / attempt["attemptId"]
    out_dir.mkdir(parents=True, exist_ok=True)
    review_images = [("overlay", review), ("preview128", preview), ("tile64x32", tile)]
    if contract.get("identitySpec"):
        anchor = Image.open(store.absolute(contract["anchor"]["path"], must_exist=True)).convert("RGBA")
        review_images.append(("anchorTileCompare", render_anchor_tile_compare(prepared, anchor, contract["identitySpec"], store)))
    if contract.get("occlusion"):
        depth = Image.new("RGBA", (256, 256), (36, 36, 42, 255))
        depth.alpha_composite(prepared)
        anchor_mask = Image.open(store.absolute(contract["anchor"]["maskPath"], must_exist=True)).convert("RGBA")
        anchor_pixels = pixel_data(anchor_mask)
        mask_pixels = pixel_data(mask)
        depth_tint = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        colored = []
        behind_colors = {MASK_COLORS[label] for label in contract["occlusion"]["layerRules"]}
        for anchor_value, mask_value in zip(anchor_pixels, mask_pixels):
            if mask_value in behind_colors:
                colored.append((255, 48, 48, 180) if anchor_value == MASK_COLORS["core"] else (48, 220, 96, 150))
            elif anchor_value == MASK_COLORS["core"]:
                colored.append((64, 128, 255, 42))
            else:
                colored.append((0, 0, 0, 0))
        depth_tint.putdata(colored)
        depth = Image.alpha_composite(depth, depth_tint)
        review_images.append(("depthReview", depth))
    if contract.get("assetRole") == "assembled_sprite":
        assembly_id = attempt.get("assemblyId")
        if not assembly_id:
            raise PipelineError("assembled sprite review requires assembly binding")
        assembly = load_json(store.record("assemblies", assembly_id))
        layers = assembly["layers"]
        layer_review = Image.new("RGBA", (256 * len(layers), 512), (36, 36, 42, 255))
        cumulative = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        layer_draw = ImageDraw.Draw(layer_review)
        for index, layer in enumerate(layers):
            component = Image.open(store.absolute(layer["artifact"]["path"], must_exist=True)).convert("RGBA")
            rendered_component = apply_assembly_transform(component, layer["transform"])
            offset = tuple(layer["transform"]["translate"])
            layer_review.alpha_composite(rendered_component, (index * 256 + offset[0], offset[1]))
            cumulative.alpha_composite(rendered_component, offset)
            layer_review.alpha_composite(cumulative, (index * 256, 256))
            layer_draw.text((index * 256 + 6, 6), f"layer: {layer['role']}", fill=(235, 235, 240, 255))
            layer_draw.text((index * 256 + 6, 262), f"cumulative: {index + 1}/{len(layers)}", fill=(235, 235, 240, 255))
        review_images.append(("assemblyLayerReview", layer_review))
    outputs = {}
    for name, image in review_images:
        path = out_dir / f"{name}.png"
        if path.exists():
            before = sha256_file(path)
            tmp = out_dir / f".{name}.tmp.png"
            image.save(tmp, format="PNG", optimize=False, compress_level=9)
            if sha256_file(tmp) != before:
                tmp.unlink()
                raise PipelineError(f"review output is not deterministic: {path}")
            tmp.unlink()
        else:
            image.save(path, format="PNG", optimize=False, compress_level=9)
        outputs[name] = {"path": store.relative(path), "sha256": sha256_file(path)}
    if placement_metrics is not None:
        metrics_path = out_dir / "tilePlacementMetrics.json"
        write_json_idempotent(metrics_path, {"schemaVersion": 1, "attemptId": attempt["attemptId"], **placement_metrics})
        attempt["artifacts"]["tilePlacementMetrics"] = {
            "path": store.relative(metrics_path), "sha256": sha256_file(metrics_path)}
    attempt["artifacts"]["review"] = outputs
    register_public_artifacts(store, list(outputs.values()), "project-owned-artwork-review")
    save_attempt(store, attempt)
    return {"schemaVersion": 1, "attemptId": attempt["attemptId"], "outputs": outputs}


def decide(store: Store, args: argparse.Namespace, decision: str) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    binding = job.get("series")
    bound_series = bound_pose = None
    if binding:
        bound_series = load_json(store.record("series", binding["seriesId"]))
        bound_pose = series_pose(bound_series, binding["poseId"])
        if job.get("conceptOnly") or bound_pose["state"] == "provisional" or bound_series.get("provisionalAnchorAttemptId") == attempt["attemptId"]:
            raise PipelineError("provisional series artwork cannot be approved or rejected as formal art")
    target = "approved" if decision == "approved" else "rejected"
    if attempt["state"] == target and attempt.get("approvalId"):
        existing = load_json(store.record("approvals", attempt["approvalId"]))
        expected = {"reviewer": args.reviewer, "decision": decision, "reason": args.reason, "decidedAt": args.decided_at}
        if any(existing.get(key) != value for key, value in expected.items()):
            raise PipelineError("attempt already has a different approval receipt")
        return existing
    if attempt["state"] != "review_pending":
        raise PipelineError("approval decision requires review_pending attempt")
    if contract.get("schemaVersion") in {2, 3, ART_DIRECTION_SCHEMA_VERSION} and args.reviewer != "cty41":
        raise PipelineError("schema v2/v3/v4 formal approval must be issued by cty41")
    art_direction_verdict = None
    if decision == "approved" and contract.get("schemaVersion") == ART_DIRECTION_SCHEMA_VERSION:
        art_direction_verdict = _validate_art_direction_approval(store, attempt, contract)
    elif decision == "approved" and contract.get("equipmentProductionSpec"):
        verdict_id = attempt.get("equipmentStyleVerdictId")
        if not verdict_id:
            raise PipelineError("equipment approval requires a cty41 style verdict")
        verdict = load_json(store.record("equipment-style-verdicts", verdict_id))
        current_reviews = approval_review_hashes(store, attempt, contract)
        if (verdict.get("decision") != "approved" or verdict.get("reviewer") != "cty41"
                or verdict.get("candidateSha256") != candidate_artifact(attempt)["sha256"]
                or verdict.get("reviewSha256") != current_reviews):
            raise PipelineError("equipment style verdict does not match candidate and review")
    annotation = None
    composition_requires_annotations = False
    if contract.get("compositionSpec"):
        composition = load_json(store.record("compositions", contract["compositionSpec"]["compositionId"]))
        composition_requires_annotations = not composition.get("spec", {}).get("bodyLayer", False)
    if composition_requires_annotations and attempt.get("sourceMode") != "reviewed_import":
        annotation_id = attempt.get("annotationId")
        if not annotation_id:
            raise PipelineError("high-risk schema v2 approval requires annotations")
        annotation_path = store.record("annotations", annotation_id)
        annotation = {"annotationId": annotation_id, "sha256": sha256_file(annotation_path)}
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    report = load_json(store.absolute(attempt["report"]["path"], must_exist=True))
    if not report["passed"]:
        raise PipelineError("technical gate did not pass")
    review_hashes = approval_review_hashes(store, attempt, contract)
    receipt_payload = {
        "attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt)["sha256"],
        "maskSha256": candidate_mask_artifact(attempt).get("sha256"), "reviewer": args.reviewer,
        "reviewSha256": review_hashes,
        "decision": decision, "reason": args.reason, "decidedAt": args.decided_at,
        "annotation": annotation, "reportSha256": attempt["report"]["sha256"],
    }
    if art_direction_verdict:
        receipt_payload["artDirectionVerdict"] = {
            "artDirectionVerdictId": art_direction_verdict["artDirectionVerdictId"],
            "sha256": sha256_file(store.record("art-direction-verdicts", art_direction_verdict["artDirectionVerdictId"])),
        }
    approval_id = stable_id("approval", receipt_payload)
    receipt = {"schemaVersion": contract.get("schemaVersion", 1), "approvalId": approval_id, **receipt_payload}
    write_json_idempotent(store.record("approvals", approval_id), receipt, immutable=True)
    attempt["approvalId"] = approval_id
    transition(attempt, {"review_pending"}, target)
    save_attempt(store, attempt)
    if decision == "approved" and bound_series and bound_pose:
        bound_pose["state"] = "approved"
        save_series(store, bound_series)
    return receipt


def approve_exception(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract_path = store.record("contracts", job["contractId"])
    contract = load_json(contract_path)
    if args.reviewer != "cty41":
        raise PipelineError("gate exception reviewer must be cty41")
    if not args.reason.strip():
        raise PipelineError("gate exception reason must not be empty")
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    if not attempt.get("feedbackId"):
        raise PipelineError("gate exception approval requires recorded feedback")

    binding = job.get("series")
    bound_series = bound_pose = None
    if binding:
        bound_series = load_json(store.record("series", binding["seriesId"]))
        bound_pose = series_pose(bound_series, binding["poseId"])
        if job.get("conceptOnly") or bound_pose["state"] == "provisional" or bound_series.get("provisionalAnchorAttemptId") == attempt["attemptId"]:
            raise PipelineError("provisional series artwork cannot receive a gate exception approval")
        selected = bound_pose.get("selectedAttemptId")
        if selected and selected != attempt["attemptId"]:
            raise PipelineError("pose already has a different selected attempt")

    existing_id = attempt.get("approvalId")
    if attempt["state"] in {"approved", "promoted"} and existing_id:
        existing = load_json(store.record("approvals", existing_id))
        validate_exception_receipt(store, attempt, existing)
        expected = {
            "reviewer": args.reviewer, "reason": args.reason, "decidedAt": args.decided_at,
            "waivedIssues": gate_exception_evidence(
                store, args.issue,
                load_json(store.absolute(attempt["report"]["path"], must_exist=True)), contract),
        }
        if any(existing.get(key) != value for key, value in expected.items()):
            raise PipelineError("attempt already has a different gate exception approval receipt")
        return existing
    if attempt["state"] != "technical_failed":
        raise PipelineError("gate exception approval requires technical_failed attempt")

    report_path = store.absolute(attempt["report"]["path"], must_exist=True)
    if sha256_file(report_path) != attempt["report"]["sha256"]:
        raise PipelineError("validation report hash mismatch")
    report = load_json(report_path)
    if report.get("passed"):
        raise PipelineError("passing reports must use standard approval")
    waived_issues = gate_exception_evidence(store, args.issue, report, contract)
    review_hashes = approval_review_hashes(store, attempt, contract)
    annotation = None
    if contract.get("compositionSpec"):
        annotation_id = attempt.get("annotationId")
        if not annotation_id:
            raise PipelineError("schema v2 gate exception requires annotations")
        annotation = {"annotationId": annotation_id,
                      "sha256": sha256_file(store.record("annotations", annotation_id))}
    receipt_payload = {
        "attemptId": attempt["attemptId"],
        "candidateSha256": candidate_artifact(attempt)["sha256"],
        "maskSha256": candidate_mask_artifact(attempt).get("sha256"),
        "reportSha256": attempt["report"]["sha256"],
        "contractId": job["contractId"],
        "contractSha256": sha256_file(contract_path),
        "reviewSha256": review_hashes,
        "annotation": annotation,
        "reviewer": args.reviewer,
        "decision": "approved",
        "approvalMode": "gate-exception",
        "waivedIssues": waived_issues,
        "reason": args.reason,
        "decidedAt": args.decided_at,
    }
    approval_id = stable_id("approval", receipt_payload)
    receipt = {"schemaVersion": contract.get("schemaVersion", 1), "approvalId": approval_id, **receipt_payload}
    write_json_idempotent(store.record("approvals", approval_id), receipt, immutable=True)
    attempt["approvalId"] = approval_id
    transition(attempt, {"technical_failed"}, "approved")
    save_attempt(store, attempt)
    if bound_series and bound_pose:
        bound_pose["selectedAttemptId"] = attempt["attemptId"]
        bound_pose["state"] = "approved"
        save_series(store, bound_series)
    return receipt


def record_feedback(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] not in {"prepared", "annotated", "calibrated", "review_pending", "technical_failed"}:
        raise PipelineError("feedback requires a processed or validated attempt")
    if args.verdict not in FEEDBACK_VERDICTS:
        raise PipelineError(f"unsupported feedback verdict: {args.verdict}")
    payload = {
        "attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt).get("sha256"),
        "reviewer": args.reviewer, "verdict": args.verdict, "strengths": args.strength,
        "defects": args.defect, "nextPromptDelta": args.next_prompt_delta or "", "recordedAt": args.recorded_at,
    }
    author_type = getattr(args, "author_type", None)
    categories = getattr(args, "category", []) or []
    frozen = getattr(args, "frozen", []) or []
    pending = getattr(args, "pending", []) or []
    if author_type:
        if author_type not in {"agent", "human"}:
            raise PipelineError("feedback author type must be agent or human")
        if set(categories) - FEEDBACK_CATEGORIES:
            raise PipelineError("feedback contains unsupported defect category")
        payload.update({"authorType": author_type, "categories": categories,
                        "disposition": args.verdict, "frozenInvariants": frozen,
                        "pendingFixes": pending or list(args.defect)})
    try:
        recorded_at = datetime.fromisoformat(args.recorded_at)
    except ValueError as exc:
        raise PipelineError("--recorded-at must be an ISO-8601 timestamp") from exc
    if recorded_at.tzinfo is None:
        raise PipelineError("--recorded-at must include a timezone offset")
    feedback_id = stable_id("feedback", payload)
    record = {"schemaVersion": 2 if author_type else 1, "feedbackId": feedback_id, **payload}
    existing_id = attempt.get("feedbackId")
    if existing_id and existing_id != feedback_id:
        raise PipelineError("attempt already has different immutable feedback")
    write_json_idempotent(store.record("feedback", feedback_id), record, immutable=True)
    if not existing_id:
        attempt["feedbackId"] = feedback_id
        save_attempt(store, attempt)
    return record


def record_feedback_addendum(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    feedback = load_json(store.record("feedback", args.feedback_id))
    attempt = load_json(store.record("attempts", feedback["attemptId"]))
    try:
        recorded_at = datetime.fromisoformat(args.recorded_at)
    except ValueError as exc:
        raise PipelineError("--recorded-at must be an ISO-8601 timestamp") from exc
    if recorded_at.tzinfo is None:
        raise PipelineError("--recorded-at must include a timezone offset")
    disposition = getattr(args, "disposition", None)
    if not args.defect and not disposition:
        raise PipelineError("feedback addendum requires a defect or disposition")
    if disposition and disposition not in FEEDBACK_VERDICTS:
        raise PipelineError("feedback addendum disposition is invalid")
    payload = {"attemptId": attempt["attemptId"], "parentFeedbackId": feedback["feedbackId"],
               "reviewer": args.reviewer, "defects": args.defect, "recordedAt": args.recorded_at,
               "authorType": getattr(args, "author_type", None), "disposition": disposition}
    addendum_id = stable_id("feedback-addendum", payload)
    record = {"schemaVersion": 2 if disposition or getattr(args, "author_type", None) else 1,
              "feedbackAddendumId": addendum_id, **payload}
    write_json_idempotent(store.record("feedback-addenda", addendum_id), record, immutable=True)
    attempt.setdefault("feedbackAddendumIds", [])
    if addendum_id not in attempt["feedbackAddendumIds"]:
        attempt["feedbackAddendumIds"].append(addendum_id)
        save_attempt(store, attempt)
    return record


def attempt_binding(store: Store, attempt: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    job = load_json(store.record("jobs", attempt["jobId"]))
    binding = job.get("series")
    if not binding:
        raise PipelineError("attempt is not bound to a series")
    series = load_json(store.record("series", binding["seriesId"]))
    pose = series_pose(series, binding["poseId"])
    return job, series, pose


def unique_pose_hashes(store: Store, pose: dict[str, Any]) -> set[str]:
    hashes = set()
    for attempt_id in pose["attemptIds"]:
        attempt = load_json(store.record("attempts", attempt_id))
        digest = attempt.get("artifacts", {}).get("raw", {}).get("sha256")
        if digest:
            hashes.add(digest)
    return hashes


def select_attempt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    _job, series, pose = attempt_binding(store, attempt)
    feedback_id = attempt.get("feedbackId")
    if not feedback_id:
        raise PipelineError("selection requires recorded feedback")
    feedback = load_json(store.record("feedback", feedback_id))
    if args.provisional:
        limit = series.get("maxUniqueOutputs")
        if limit is None or pose["poseId"] != "idle-dr" or len(unique_pose_hashes(store, pose)) != limit:
            raise PipelineError("provisional anchor is only allowed for exhausted idle-dr")
        pose["state"] = "provisional"
        series["provisionalAnchorAttemptId"] = attempt["attemptId"]
    else:
        if attempt["state"] != "review_pending" or feedback["verdict"] != "selected":
            raise PipelineError("normal selection requires review_pending and selected feedback")
        pose["state"] = "review_pending"
    existing = pose.get("selectedAttemptId")
    if existing and existing != attempt["attemptId"]:
        raise PipelineError("pose already has a different selected attempt")
    pose["selectedAttemptId"] = attempt["attemptId"]
    save_series(store, series)
    return series


def advance_series(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    series = load_json(store.record("series", args.series_id))
    current = series_pose(series, series["currentPoseId"])
    if current["state"] == "active":
        limit = series.get("maxUniqueOutputs")
        if limit is None:
            raise PipelineError("unlimited active pose must be selected, approved, promoted, or explicitly rejected before advancing")
        hashes = unique_pose_hashes(store, current)
        feedback_complete = all(load_json(store.record("attempts", aid)).get("feedbackId") for aid in current["attemptIds"] if load_json(store.record("attempts", aid)).get("artifacts", {}).get("raw"))
        if len(hashes) != limit or not feedback_complete:
            raise PipelineError("active pose can only exhaust after every unique output has feedback")
        current["state"] = "exhausted"
    if current["poseId"] == "idle-dr" and current["state"] not in {"promoted", "provisional"}:
        raise PipelineError("idle-dr must be promoted or explicitly provisional before advancing")
    if current["state"] not in {"review_pending", "approved", "promoted", "exhausted", "provisional"}:
        raise PipelineError("current pose is not ready to advance")
    index = series["poses"].index(current)
    if index + 1 >= len(series["poses"]):
        series["currentPoseId"] = None
        save_series(store, series)
        return series
    next_pose = series["poses"][index + 1]
    if next_pose["state"] == "pending":
        next_pose["state"] = "active"
    series["currentPoseId"] = next_pose["poseId"]
    save_series(store, series)
    return series


def update_provenance(store: Store, paths: list[dict[str, str]], contract: dict[str, Any]) -> None:
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    manifest = load_json(manifest_path)
    by_path = {entry["path"]: entry for entry in manifest["entries"]}
    for artifact in paths:
        entry = {"path": artifact["path"], "sha256": artifact["sha256"], "status": "approved", **contract["rights"]}
        existing = by_path.get(artifact["path"])
        if existing and existing != entry:
            supporting_upgrade = (
                existing.get("sha256") == entry["sha256"]
                and existing.get("status") == "approved"
                and existing.get("provenance") == "project-owned-supporting-derived"
            )
            if not supporting_upgrade:
                raise PipelineError(f"conflicting provenance entry: {artifact['path']}")
            existing.clear()
            existing.update(entry)
        if not existing:
            manifest["entries"].append(entry)
            by_path[artifact["path"]] = entry
    write_json_idempotent(manifest_path, manifest)


def register_public_artifacts(store: Store, paths: list[dict[str, str]], provenance: str) -> None:
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    if not manifest_path.is_file():
        return
    manifest = load_json(manifest_path)
    by_path = {entry["path"]: entry for entry in manifest["entries"]}
    for artifact in paths:
        existing = by_path.get(artifact["path"])
        if existing:
            expected = {"sha256": artifact["sha256"], "status": "approved", "rightsHolder": "cty41", "license": "CC-BY-4.0"}
            if any(existing.get(key) != value for key, value in expected.items()):
                raise PipelineError(f"conflicting provenance entry: {artifact['path']}")
            continue
        entry = {
            "path": artifact["path"], "sha256": artifact["sha256"], "status": "approved",
            "rightsHolder": "cty41", "license": "CC-BY-4.0", "provenance": provenance,
        }
        manifest["entries"].append(entry)
        by_path[artifact["path"]] = entry
    write_json_idempotent(manifest_path, manifest)


def sync_attempt_provenance(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Synchronize hash-bound attempt PNGs using the attempt contract's existing rights."""
    if args.reviewer != "cty41":
        raise PipelineError("attempt provenance synchronization requires reviewer cty41")
    _iso_timestamp(args.synced_at, "--synced-at")
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    if not contract.get("rights"):
        raise PipelineError("attempt contract has no rights binding")
    artifacts = []
    for artifact in attempt.get("artifacts", {}).values():
        values = artifact.values() if isinstance(artifact, dict) and "path" not in artifact else [artifact]
        for value in values:
            if (isinstance(value, dict) and str(value.get("path", "")).lower().endswith(".png")
                    and str(value.get("path", "")).startswith("Tools/artworks/pipeline/")):
                target = store.absolute(value["path"])
                if not target.is_file() or sha256_file(target) != value.get("sha256"):
                    raise PipelineError("attempt provenance artifact hash mismatch")
                artifacts.append({"path": value["path"], "sha256": value["sha256"]})
    artifacts = sorted({item["path"]: item for item in artifacts}.values(), key=lambda item: item["path"])
    if not artifacts:
        raise PipelineError("attempt has no PNG artifacts to synchronize")
    payload = {"attemptId": attempt["attemptId"], "jobId": job["jobId"], "contractId": contract["contractId"],
               "artifacts": artifacts, "rights": contract["rights"], "reviewer": "cty41",
               "reason": args.reason, "syncedAt": args.synced_at}
    sync_id = stable_id("attempt-provenance-sync", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "attemptProvenanceSyncId": sync_id, **payload}
    update_provenance(store, artifacts, contract)
    write_json_idempotent(store.record("attempt-provenance-syncs", sync_id), record, immutable=True)
    return record


def invalidate_attempt_provenance_sync(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if args.reviewer != "cty41":
        raise PipelineError("attempt provenance sync invalidation requires reviewer cty41")
    _iso_timestamp(args.invalidated_at, "--invalidated-at")
    sync_path = store.record("attempt-provenance-syncs", args.attempt_provenance_sync_id)
    sync = load_json(sync_path)
    invalid = any(not str(item.get("path", "")).startswith("Tools/artworks/pipeline/")
                  for item in sync.get("artifacts", []))
    if not invalid:
        raise PipelineError("only a structurally invalid historical provenance sync may be invalidated")
    payload = {"attemptProvenanceSyncId": sync["attemptProvenanceSyncId"],
               "attemptProvenanceSync": {"path": store.relative(sync_path), "sha256": sha256_file(sync_path)},
               "reviewer": "cty41", "reason": args.reason, "invalidatedAt": args.invalidated_at}
    invalidation_id = stable_id("attempt-provenance-sync-invalidation", payload)
    record = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION,
              "attemptProvenanceSyncInvalidationId": invalidation_id, **payload}
    write_json_idempotent(store.record("attempt-provenance-sync-invalidations", invalidation_id), record, immutable=True)
    return record


def relicense_public_artifacts(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    _iso_timestamp(args.decided_at, "--decided-at")
    if args.reviewer != "cty41":
        raise PipelineError("public artifact relicensing requires reviewer cty41")
    if args.from_license != "project-owned" or args.to_license != "CC-BY-4.0":
        raise PipelineError("only project-owned to CC-BY-4.0 relicensing is supported")
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    manifest = load_json(manifest_path)
    by_path = {entry["path"]: entry for entry in manifest["entries"]}
    artifacts: list[dict[str, str]] = []
    for value in sorted(set(args.path)):
        rel = store.relative(value, must_exist=True)
        entry = by_path.get(rel)
        if entry is None:
            raise PipelineError(f"provenance entry is missing: {rel}")
        digest = sha256_file(store.absolute(rel))
        if entry.get("sha256") != digest:
            raise PipelineError(f"provenance hash mismatch: {rel}")
        if entry.get("status") != "approved" or entry.get("rightsHolder") != "cty41":
            raise PipelineError(f"artifact is not approved project-owned work: {rel}")
        if entry.get("license") not in {args.from_license, args.to_license}:
            raise PipelineError(f"artifact has unexpected license: {rel}")
        artifacts.append({"path": rel, "sha256": digest})
    payload = {
        "artifacts": artifacts,
        "fromLicense": args.from_license,
        "toLicense": args.to_license,
        "reviewer": args.reviewer,
        "reason": args.reason,
        "decidedAt": args.decided_at,
    }
    receipt_id = stable_id("public-artifact-license", payload)
    receipt = {"schemaVersion": 1, "licenseReceiptId": receipt_id, **payload}
    write_json_idempotent(store.record("license-receipts", receipt_id), receipt, immutable=True)
    for artifact in artifacts:
        by_path[artifact["path"]]["license"] = args.to_license
    write_json_idempotent(manifest_path, manifest)
    return receipt


def _iso_timestamp(value: str, option: str) -> None:
    try:
        parsed = datetime.fromisoformat(value)
    except ValueError as exc:
        raise PipelineError(f"{option} must be an ISO-8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise PipelineError(f"{option} must include a timezone offset")


def create_composition(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    spec_rel = store.relative(args.spec, must_exist=True)
    try:
        spec = json.loads(store.absolute(spec_rel).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise PipelineError(f"cannot read composition spec: {exc}") from exc
    required = {"canvas", "coreAxis", "footCenter", "weapon", "forbiddenRegions", "equipmentState"}
    if not isinstance(spec, dict) or required - set(spec):
        raise PipelineError("composition spec is missing required v2 fields")
    if spec["equipmentState"].get("scabbard") not in {"present", "absent", "optional"}:
        raise PipelineError("composition equipmentState.scabbard is invalid")
    if "tipMayBeOccluded" in spec["weapon"] and not isinstance(spec["weapon"]["tipMayBeOccluded"], bool):
        raise PipelineError("composition weapon.tipMayBeOccluded must be boolean")
    anchor_rel = store.relative(args.anchor, must_exist=True)
    payload = {
        "assetId": args.asset_id,
        "spec": spec,
        "source": {"path": spec_rel, "sha256": sha256_file(store.absolute(spec_rel))},
        "anchor": {"path": anchor_rel, "sha256": sha256_file(store.absolute(anchor_rel))},
    }
    composition_id = stable_id("composition", payload)
    record = {"schemaVersion": 2, "compositionId": composition_id, **payload}
    write_json_idempotent(store.record("compositions", composition_id), record, immutable=True)
    return record


def render_pose_guide(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    composition_path = store.record("compositions", args.composition_id)
    composition = load_json(composition_path)
    spec = composition["spec"]
    width, height = spec.get("canvas", [256, 256])
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    axis = spec["coreAxis"]
    draw.line([tuple(axis["bottom"]), tuple(axis["top"])], fill=(0, 220, 255, 255), width=3)
    if spec.get("coreBbox"):
        draw.rounded_rectangle(tuple(spec["coreBbox"]), radius=18, outline=(0, 220, 255, 220), width=3)
    foot = spec["footCenter"]
    draw.ellipse((foot[0] - 3, foot[1] - 3, foot[0] + 3, foot[1] + 3), fill=(255, 220, 0, 255))
    weapon = spec["weapon"]
    grip = weapon["hiddenGrip"]
    draw.ellipse((grip[0] - 3, grip[1] - 3, grip[0] + 3, grip[1] + 3), fill=(255, 0, 255, 255))
    for key, color in (("exitWindow", (0, 255, 0, 150)), ("tipRegion", (255, 128, 0, 150))):
        draw.rectangle(tuple(weapon[key]), outline=color, width=2)
    if weapon.get("guardWindow"):
        draw.rectangle(tuple(weapon["guardWindow"]), outline=(255, 255, 0, 180), width=2)
    if weapon.get("bladeCenterline"):
        draw.rectangle(tuple(weapon["bladeCenterline"]), outline=(120, 140, 255, 180), width=1)
    if weapon.get("screenAxis"):
        draw.line([tuple(point) for point in weapon["screenAxis"]], fill=(255, 255, 255, 240), width=3)
    if spec.get("renderForbiddenRegions", True):
        for region in spec["forbiddenRegions"]:
            draw.rectangle(tuple(region["rect"]), outline=(255, 0, 0, 220), fill=(255, 0, 0, 40), width=2)
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(f".{output.name}.tmp")
    image.save(temporary, format="PNG", optimize=False, compress_level=9)
    os.replace(temporary, output)
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    payload = {
        "compositionId": composition["compositionId"],
        "compositionSha256": sha256_file(composition_path),
        "anchorSha256": composition["anchor"]["sha256"],
        "artifact": artifact,
        "role": "supporting-derived",
    }
    guide_id = stable_id("pose-guide", payload)
    record = {"schemaVersion": 2, "poseGuideId": guide_id, **payload}
    write_json_idempotent(store.record("pose-guides", guide_id), record, immutable=True)
    register_public_artifacts(store, [artifact], "project-owned-supporting-derived")
    return record


def compile_prompt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    job = load_json(store.record("jobs", args.job_id))
    contract = load_json(store.record("contracts", job["contractId"]))
    if contract.get("schemaVersion") not in {2, 3, 4}:
        raise PipelineError("compile-prompt requires schema v2, v3, or v4 contract")
    composition_ref = contract.get("compositionSpec")
    if not composition_ref:
        raise PipelineError("schema v2 high-risk prompt requires composition spec")
    composition = load_json(store.record("compositions", composition_ref["compositionId"]))
    guide = load_json(store.record("pose-guides", args.pose_guide_id))
    if guide.get("compositionId") != composition["compositionId"]:
        raise PipelineError("pose guide does not match composition")
    unresolved = []
    for attempt in list_attempts(store, job["jobId"]):
        feedback_id = attempt.get("feedbackId")
        if feedback_id:
            feedback = load_json(store.record("feedback", feedback_id))
            unresolved.extend(feedback.get("pendingFixes", feedback.get("defects", [])))
    approved_asset_id = contract.get("approvedAssetId")
    anchor_path = (contract.get("anchor") or {}).get("path", "")
    is_tomb_maw_bat = approved_asset_id == "tomb-maw-bat" or "tomb_maw_bat" in anchor_path
    if contract.get("componentKind") == "death_expression_overlay":
        invariants = [
            "transparent expression overlay only", "exactly two compact crossed-eye marks",
            "no face, coat, ears, mouth, collar, paws, equipment, effects, text, or watermark",
        ]
    elif is_tomb_maw_bat:
        invariants = [
            "near-round spherical flying core locked to the approved bat anchor",
            "exactly two pointed ears and exactly two membrane wings attached to the core",
            "no paws, arms, legs, humanoid torso, or tail",
            "dark plum body, red wing membranes, yellow eyes, and ivory fangs",
            "preserve the approved hover height and virtual tile landing axis",
        ]
    else:
        invariants = [
            "equal-width rigid capsule body", "exactly four paws directly attached to the body",
            "no arms and no legs between paws and body",
            "gray-white forehead blaze and heterochromic ear", "half-body alternate coat color",
        ]
    if composition["spec"]["equipmentState"].get("scabbard") == "absent":
        invariants.append("no scabbard anywhere")
    sections = [
        "# Deterministic ImageGen Task Packet",
        "## Frozen invariants\n" + "\n".join(f"- {item}" for item in invariants),
        "## Reference responsibilities\n" + "\n".join(f"- {item['role']}: {item['path']} @ {item['sha256']}" for item in job["inputs"]),
        "## Composition\n```json\n" + json.dumps(composition["spec"], ensure_ascii=False, sort_keys=True, indent=2) + "\n```",
        "## Unresolved fixes\n" + ("\n".join(f"- {item}" for item in unresolved) or "- none"),
        "## Base prompt\n" + store.absolute(job["prompt"]["path"]).read_text(encoding="utf-8"),
    ]
    data = ("\n\n".join(sections) + "\n").encode("utf-8")
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(f".{output.name}.tmp")
    temporary.write_bytes(data)
    os.replace(temporary, output)
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    payload = {"jobId": job["jobId"], "poseGuideId": guide["poseGuideId"], "artifact": artifact}
    prompt_id = stable_id("compiled-prompt", payload)
    record = {"schemaVersion": 2, "compiledPromptId": prompt_id, **payload}
    write_json_idempotent(store.record("compiled-prompts", prompt_id), record, immutable=True)
    return record


def compile_equipment_prompt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    job = load_json(store.record("jobs", args.job_id))
    _validate_job_local_references(store, job)
    contract = load_json(store.record("contracts", job["contractId"]))
    spec = contract.get("equipmentProductionSpec")
    if not spec:
        raise PipelineError("compile-equipment-prompt requires an equipment production contract")
    profile = _equipment_style_profile(store, contract)
    category = profile["categories"][spec["category"]]
    unresolved = []
    for attempt in list_attempts(store, job["jobId"]):
        if attempt.get("feedbackId"):
            feedback = load_json(store.record("feedback", attempt["feedbackId"]))
            unresolved.extend(feedback.get("pendingFixes", feedback.get("defects", [])))
    reference_lines = [f"- {item['role']}: {item['path']} @ {item['sha256']}" for item in spec["anchors"]]
    reference_lines.extend(
        f"- local {item['role']}: {item['sourceLabel']} ({item['fileName']}) @ {item['sha256']}"
        for item in job.get("localReferences", []))
    sections = [
        "# Pure Run Equipment ImageGen Task Packet",
        f"## Category\n- {spec['category']}",
        "## Frozen base style\n" + "\n".join(f"- {item}" for item in profile.get("baseRules", [])),
        "## Category rules\n" + "\n".join(f"- {item}" for item in category.get("rules", [])),
        "## Reference responsibilities\n" + ("\n".join(reference_lines) or "- none"),
        "## Negative constraints\n" + "\n".join(f"- {item}" for item in profile.get("negativeConstraints", [])),
        "## Feedback delta\n" + ("\n".join(f"- {item}" for item in unresolved) or "- none"),
        "## Base prompt\n" + store.absolute(job["prompt"]["path"]).read_text(encoding="utf-8"),
    ]
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(("\n\n".join(sections) + "\n").encode("utf-8"))
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    payload = {"jobId": job["jobId"], "equipmentProfileSha256": spec["profileSha256"], "artifact": artifact}
    prompt_id = stable_id("compiled-prompt", payload)
    record = {"schemaVersion": 2, "compiledPromptId": prompt_id, **payload}
    write_json_idempotent(store.record("compiled-prompts", prompt_id), record, immutable=True)
    return record


def _validate_job_local_references(store: Store, job: dict[str, Any]) -> None:
    for binding in job.get("localReferences", []):
        path = store.record("local-references", binding.get("localReferenceId", ""))
        if not path.is_file() or sha256_file(path) != binding.get("descriptorSha256"):
            raise PipelineError("job local reference descriptor is missing or changed")
        descriptor = load_json(path)
        if (descriptor.get("sha256") != binding.get("sha256")
                or descriptor.get("role") != binding.get("role")
                or descriptor.get("sourceLabel") != binding.get("sourceLabel")):
            raise PipelineError("job local reference binding mismatch")


def begin_generation(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] != "ready":
        raise PipelineError("generation can only begin for a ready attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    _validate_job_local_references(store, job)
    compiled = load_json(store.record("compiled-prompts", args.compiled_prompt_id))
    if compiled["jobId"] != attempt["jobId"]:
        raise PipelineError("compiled prompt belongs to another job")
    _iso_timestamp(args.started_at, "--started-at")
    payload = {
        "attemptId": attempt["attemptId"], "compiledPromptId": compiled["compiledPromptId"],
        "compiledPromptSha256": sha256_file(store.record("compiled-prompts", compiled["compiledPromptId"])),
        "provider": args.provider, "startedAt": args.started_at,
    }
    invocation_id = stable_id("generation-invocation", payload)
    record = {"schemaVersion": 2, "invocationId": invocation_id, "state": "started", **payload}
    write_json_idempotent(store.record("generation-invocations", invocation_id), record, immutable=True)
    return record


def record_generation_failure(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    invocation = load_json(store.record("generation-invocations", args.invocation_id))
    _iso_timestamp(args.failed_at, "--failed-at")
    payload = {"invocationId": invocation["invocationId"], "attemptId": invocation["attemptId"],
               "reason": args.reason, "failedAt": args.failed_at}
    failure_id = stable_id("generation-failure", payload)
    record = {"schemaVersion": 2, "generationFailureId": failure_id, **payload}
    write_json_idempotent(store.record("generation-failures", failure_id), record, immutable=True)
    return record


def attach_annotations(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] not in {"prepared", "annotated", "calibrated"}:
        raise PipelineError("annotations require a prepared attempt")
    annotation_rel = store.relative(args.annotations, must_exist=True)
    annotations = load_json(store.absolute(annotation_rel))
    required = {"eyeRegion", "weaponExit", "weaponTip", "gemRegion"}
    if required - set(annotations.get("regions", {})):
        raise PipelineError("annotations are missing required high-risk regions")
    mask = candidate_mask_artifact(attempt)
    payload = {"attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt)["sha256"],
               "maskSha256": mask["sha256"], "source": {"path": annotation_rel, "sha256": sha256_file(store.absolute(annotation_rel))},
               "regions": annotations["regions"]}
    annotation_id = stable_id("annotations", payload)
    record = {"schemaVersion": 2, "annotationId": annotation_id, **payload}
    write_json_idempotent(store.record("annotations", annotation_id), record, immutable=True)
    attempt["annotationId"] = annotation_id
    if attempt["state"] == "prepared":
        attempt["state"] = "annotated"
    save_attempt(store, attempt)
    return record


def record_advisory_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    _iso_timestamp(args.recorded_at, "--recorded-at")
    payload = {"attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt)["sha256"],
               "reviewer": args.reviewer, "risks": args.risk, "recordedAt": args.recorded_at,
               "nonBinding": True}
    advisory_id = stable_id("advisory-review", payload)
    record = {"schemaVersion": 2, "advisoryReviewId": advisory_id, **payload}
    write_json_idempotent(store.record("advisory-reviews", advisory_id), record, immutable=True)
    return record


def register_supporting_artifact(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if getattr(args, "reviewer", None) != "cty41":
        raise PipelineError("supporting artifact registration requires reviewer cty41")
    if args.role not in {"review-derived", "historical-review-only", "pose-guide", "semantic-mask", "supporting-derived"}:
        raise PipelineError("unsupported supporting artifact role")
    rights = {"rightsHolder": "cty41", "license": "project-owned",
              "provenance": "cty41-direct-supporting-artifact-declaration"}
    rel = store.relative(args.path, must_exist=True)
    if not rel.startswith("Tools/artworks/") or rel.startswith("Tools/artworks/pipeline/"):
        raise PipelineError("supporting artifact must be a non-pipeline Tools/artworks file")
    artifact = {"path": rel, "sha256": sha256_file(store.absolute(rel))}
    payload = {"artifact": artifact, "role": args.role, "note": args.note, "reviewer": "cty41",
               "rights": rights}
    record_id = stable_id("supporting-artifact", payload)
    record = {"schemaVersion": 3, "supportingArtifactId": record_id, **payload}
    write_json_idempotent(store.record("supporting-artifacts", record_id), record, immutable=True)
    if Path(rel).suffix.lower() in {".png", ".svg"}:
        update_provenance(store, [artifact], {"rights": rights})
    return record


def _bound_artifact(store: Store, path: str) -> dict[str, str]:
    rel = store.relative(path, must_exist=True)
    return {"path": rel, "sha256": sha256_file(store.absolute(rel))}


def _visible_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.getchannel("A").getbbox()


def _sprite_import_geometry(image: Image.Image, expected_size: tuple[int, int], label: str) -> dict[str, Any]:
    if image.mode != "RGBA" or image.size != expected_size:
        raise PipelineError(f"{label} must be {expected_size[0]}x{expected_size[1]} RGBA")
    corners = [image.getpixel(point)[3] for point in ((0, 0), (image.width - 1, 0), (0, image.height - 1), (image.width - 1, image.height - 1))]
    if any(corners):
        raise PipelineError(f"{label} corners must be transparent")
    if any(pixel[3] and pixel[:3] in {(0, 255, 0), (255, 0, 255)} for pixel in pixel_data(image)):
        raise PipelineError(f"{label} contains exact chroma residue")
    bbox = _visible_bbox(image)
    if not bbox:
        raise PipelineError(f"{label} is empty")
    center = [(bbox[0] + bbox[2] - 1) / 2, (bbox[1] + bbox[3] - 1) / 2]
    expected_center = [(image.width - 1) / 2, (image.height - 1) / 2]
    centered = all(abs(center[index] - expected_center[index]) <= 0.5 for index in range(2))
    expected_baseline = 236 if expected_size == (256, 256) else 118
    baseline_tolerance = 2 if expected_size == (256, 256) else 1
    if not centered and abs((bbox[3] - 1) - expected_baseline) > baseline_tolerance:
        raise PipelineError(f"{label} must be centered or use the artwork baseline")
    return {"bbox": list(bbox), "bboxSize": [bbox[2] - bbox[0], bbox[3] - bbox[1]], "center": center}


def normalize_reviewed_sprite(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source = _bound_artifact(store, args.source)
    with Image.open(store.absolute(source["path"])) as opened:
        if opened.mode != "RGBA" or opened.size != (256, 256):
            raise PipelineError("reviewed source must be 256x256 RGBA")
        normalized = normalize_transparent_rgb(opened.copy())
    output_rel = store.relative(args.output)
    preview_rel = store.relative(args.preview)
    output = store.absolute(output_rel); preview = store.absolute(preview_rel)
    output.parent.mkdir(parents=True, exist_ok=True); preview.parent.mkdir(parents=True, exist_ok=True)
    normalized.save(output, format="PNG", optimize=False, compress_level=9)
    make_preview(normalized).save(preview, format="PNG", optimize=False, compress_level=9)
    artifacts = {"candidate": _bound_artifact(store, output_rel), "preview": _bound_artifact(store, preview_rel)}
    register_public_artifacts(store, list(artifacts.values()), "project-owned-supporting-derived")
    return {"schemaVersion": 1, "source": source, "artifacts": artifacts}


def register_runtime_copy(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source = _bound_artifact(store, args.source)
    target = _bound_artifact(store, args.target)
    if source["sha256"] != target["sha256"]:
        raise PipelineError("runtime copy must be byte-identical to its approved source")
    manifest = load_json(store.root / "Tools/public-release/asset-provenance.json")
    source_entry = next((entry for entry in manifest["entries"] if entry.get("path") == source["path"]), None)
    if not source_entry or source_entry.get("status") != "approved":
        raise PipelineError("runtime copy source must have approved public provenance")
    register_public_artifacts(store, [target], "project-owned-migrated-runtime-art")
    return {"schemaVersion": 1, "source": source, "target": target}


def render_size_comparison(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    inputs = [("Identity", args.identity), ("Previous", args.previous), ("Pose reference", args.reference), ("Candidate", args.candidate)]
    bound = []
    panels = []
    measurements = []
    for label, value in inputs:
        artifact = _bound_artifact(store, value)
        image = Image.open(store.absolute(artifact["path"])).convert("RGBA")
        preview = make_preview(image) if image.size == (256, 256) else image.resize((128, 128), Image.Resampling.LANCZOS)
        panel = Image.new("RGBA", (144, 176), (32, 32, 32, 255))
        panel.alpha_composite(preview, (8, 8))
        ImageDraw.Draw(panel).text((8, 144), label, fill=(255, 255, 255, 255))
        panels.append(panel)
        bbox = _visible_bbox(preview)
        measurements.append({"label": label, "bbox": list(bbox) if bbox else None,
                             "bboxSize": [bbox[2] - bbox[0], bbox[3] - bbox[1]] if bbox else None})
        bound.append({"label": label, "artifact": artifact})
    review = Image.new("RGBA", (144 * len(panels), 176), (32, 32, 32, 255))
    for index, panel in enumerate(panels):
        review.alpha_composite(panel, (144 * index, 0))
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel); output.parent.mkdir(parents=True, exist_ok=True)
    review.save(output, format="PNG", optimize=False, compress_level=9)
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    payload = {"inputs": bound, "measurements": measurements, "artifact": artifact}
    comparison_id = stable_id("size-comparison", payload)
    record = {"schemaVersion": 1, "sizeComparisonId": comparison_id, **payload}
    write_json_idempotent(store.record("size-comparisons", comparison_id), record, immutable=True)
    register_public_artifacts(store, [artifact], "project-owned-artwork-review")
    return record


def adopt_reviewed_sprite(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    contract_path = store.record("contracts", args.contract_id)
    contract = load_json(contract_path)
    if contract.get("equipmentProductionSpec"):
        raise PipelineError("new equipment contracts cannot use reviewed_import")
    if contract.get("styleSpec"):
        _equipment_style_profile(store, contract)
    if contract.get("assetRole") == "component" or contract.get("runtimeEligible") is False:
        raise PipelineError("reviewed imports must be complete Sprite assets")
    if args.reviewer != "cty41":
        raise PipelineError("reviewed import reviewer must be cty41")
    try:
        accepted_at = datetime.fromisoformat(args.accepted_at)
    except ValueError as exc:
        raise PipelineError("--accepted-at must be an ISO-8601 timestamp") from exc
    if accepted_at.tzinfo is None:
        raise PipelineError("--accepted-at must include a timezone offset")
    source = _bound_artifact(store, args.source)
    candidate = _bound_artifact(store, args.candidate)
    preview = _bound_artifact(store, args.preview)
    comparison = _bound_artifact(store, args.size_comparison)
    style_report_artifact = None
    if contract.get("styleSpec"):
        matches = []
        for path in (store.pipeline / "style-reports").glob("*.json"):
            report = load_json(path)
            if (report.get("contractId") == args.contract_id
                    and report.get("candidate") == candidate
                    and report.get("preview") == preview):
                matches.append(report)
        if len(matches) != 1 or not matches[0].get("passed"):
            raise PipelineError("reviewed import requires one passing matching equipment style report")
        style_path = store.record("style-reports", matches[0]["styleReportId"])
        style_report_artifact = {"path": store.relative(style_path), "sha256": sha256_file(style_path)}
    comparison_records = [
        load_json(path) for path in (store.pipeline / "size-comparisons").glob("*.json")
        if load_json(path).get("artifact") == comparison
    ]
    if len(comparison_records) != 1:
        raise PipelineError("reviewed import requires one matching immutable size-comparison record")
    comparison_candidate = next(
        (item.get("artifact") for item in comparison_records[0].get("inputs", []) if item.get("label") == "Candidate"), None)
    if comparison_candidate != candidate:
        raise PipelineError("size comparison Candidate does not match the reviewed import candidate")
    with Image.open(store.absolute(candidate["path"])) as candidate_source:
        candidate_geometry = _sprite_import_geometry(candidate_source.copy(), (256, 256), "candidate")
    with Image.open(store.absolute(preview["path"])) as preview_source:
        preview_geometry = _sprite_import_geometry(preview_source.copy(), (128, 128), "preview")
    payload = {"contractId": args.contract_id, "source": source, "candidate": candidate, "preview": preview,
               "sizeComparison": comparison, "reviewer": args.reviewer, "reason": args.reason,
               "acceptedAt": args.accepted_at, "sourceMode": "reviewed_import"}
    adoption_id = stable_id("reviewed-import", payload)
    adoption = {"schemaVersion": 2, "reviewedImportId": adoption_id, **payload}
    write_json_idempotent(store.record("reviewed-imports", adoption_id), adoption, immutable=True)
    job_payload = {"contractId": args.contract_id, "reviewedImportId": adoption_id, "sourceMode": "reviewed_import"}
    job_id = stable_id("job", job_payload)
    job = {"schemaVersion": 2, "jobId": job_id, "state": "ready", "contractId": args.contract_id,
           "contractSha256": sha256_file(contract_path), "prompt": None,
           "inputs": [source, candidate, preview, comparison], "target": {"direction": contract["direction"], "pose": contract["pose"]},
           "series": None, "conceptOnly": False, "contractRequirements": None,
           "requiresInvocation": False, "sourceMode": "reviewed_import", "reviewedImportId": adoption_id}
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    report_payload = {"passed": True, "issues": [], "candidateGeometry": candidate_geometry,
                      "previewGeometry": preview_geometry, "reviewedImportId": adoption_id}
    if style_report_artifact:
        report_payload["styleReport"] = style_report_artifact
    report_id = stable_id("report", report_payload)
    report_path = store.record("reports", report_id)
    write_json_idempotent(report_path, {"schemaVersion": 2, "reportId": report_id, **report_payload}, immutable=True)
    attempt_id = f"{job_id}-a001"
    attempt = {"schemaVersion": 2, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
               "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
               "technicalRemediation": False, "state": "review_pending", "sourceMode": "reviewed_import",
               "reviewedImportId": adoption_id,
               "artifacts": {"source": source, "prepared": candidate, "importedPreview": preview,
                             "review": {"sizeComparison": comparison}},
               "report": {"path": store.relative(report_path), "sha256": sha256_file(report_path)},
               "approvalId": None, "feedbackId": None}
    write_json_idempotent(store.record("attempts", attempt_id), attempt)
    return {"schemaVersion": 2, "adoption": adoption, "job": job, "attempt": attempt}


def _validated_generation_lineage(store: Store, attempt: dict[str, Any]) -> dict[str, dict[str, str]]:
    invocation_id, delivery_id = attempt.get("generationInvocationId"), attempt.get("generationDeliveryId")
    if not invocation_id or not delivery_id:
        raise PipelineError("source attempt requires its original generation invocation and delivery")
    invocation_path = store.record("generation-invocations", invocation_id)
    delivery_path = store.record("generation-deliveries", delivery_id)
    if not invocation_path.is_file() or not delivery_path.is_file():
        raise PipelineError("source generation invocation or delivery is missing")
    invocation, delivery = load_json(invocation_path), load_json(delivery_path)
    invocation_payload = {key: invocation.get(key) for key in ("attemptId", "compiledPromptId", "compiledPromptSha256", "provider", "startedAt")}
    delivery_payload = {key: delivery.get(key) for key in ("invocationId", "attemptId", "rawSha256")}
    raw = attempt.get("artifacts", {}).get("raw")
    if (stable_id("generation-invocation", invocation_payload) != invocation_id
            or stable_id("generation-delivery", delivery_payload) != delivery_id
            or invocation.get("attemptId") != attempt.get("attemptId")
            or delivery.get("invocationId") != invocation_id or delivery.get("attemptId") != attempt.get("attemptId")
            or not _artifact_binding_matches(store, raw) or delivery.get("rawSha256") != raw.get("sha256")):
        raise PipelineError("source generation invocation/delivery lineage is invalid")
    return {
        "sourceAttempt": {"path": store.relative(store.record("attempts", attempt["attemptId"])),
                          "sha256": sha256_file(store.record("attempts", attempt["attemptId"]))},
        "sourceJob": {"path": store.relative(store.record("jobs", attempt["jobId"])),
                      "sha256": sha256_file(store.record("jobs", attempt["jobId"]))},
        "generationInvocation": {"path": store.relative(invocation_path), "sha256": sha256_file(invocation_path)},
        "generationDelivery": {"path": store.relative(delivery_path), "sha256": sha256_file(delivery_path)},
    }


def _validate_recontract_processing(store: Store, attempt: dict[str, Any], candidate: dict[str, str], processing: Any) -> None:
    if isinstance(processing, dict) and processing.get("schemaVersion") == 2:
        required = {"schemaVersion", "operation", "sourceAttemptId", "sourcePreparedSha256",
                    "outputSha256", "maxAlpha", "pixels"}
        source = attempt.get("artifacts", {}).get("prepared", {})
        if (set(processing) != required
                or processing.get("operation") != "deterministic-exact-chroma-pixel-cleanup"
                or processing.get("sourceAttemptId") != attempt.get("attemptId")
                or processing.get("sourcePreparedSha256") != source.get("sha256")
                or processing.get("outputSha256") != candidate.get("sha256")
                or not _artifact_binding_matches(store, source)
                or not _artifact_binding_matches(store, candidate)):
            raise PipelineError("exact chroma processing schema or source/output binding is invalid")
        max_alpha, changes = processing["maxAlpha"], processing["pixels"]
        if type(max_alpha) is not int or not 1 <= max_alpha <= 3 or not isinstance(changes, list) or len(changes) != 2:
            raise PipelineError("exact chroma processing requires two pixels and maxAlpha between 1 and 3")
        with Image.open(store.absolute(source["path"])) as original, Image.open(store.absolute(candidate["path"])) as output:
            if original.mode != "RGBA" or output.mode != "RGBA" or original.size != output.size:
                raise PipelineError("exact chroma processing requires equal native RGBA dimensions")
            expected = original.copy()
            seen = set()
            for change in changes:
                if not isinstance(change, dict) or set(change) != {"x", "y", "rgba"}:
                    raise PipelineError("exact chroma processing pixel declaration is invalid")
                x, y, rgba = change["x"], change["y"], change["rgba"]
                if (type(x) is not int or type(y) is not int
                        or not 0 <= x < original.width or not 0 <= y < original.height
                        or (x, y) in seen or not isinstance(rgba, list) or len(rgba) != 4
                        or any(type(channel) is not int for channel in rgba)
                        or tuple(rgba[:3]) not in {(0, 255, 0), (255, 0, 255)}
                        or not 0 < rgba[3] <= max_alpha
                        or original.getpixel((x, y)) != tuple(rgba)):
                    raise PipelineError("exact chroma processing may only clear distinct bound low-alpha exact key pixels")
                seen.add((x, y))
                expected.putpixel((x, y), (0, 0, 0, 0))
            if pixel_data(expected) != pixel_data(output):
                raise PipelineError("exact chroma candidate is not the declared two-pixel replay output")
        return
    if not isinstance(processing, dict) or processing.get("schemaVersion") != 1:
        raise PipelineError("reviewed recontract processing must use supported schemaVersion 1 or 2")
    if (processing.get("operation") != "deterministic-green-fringe-cleanup"
            or processing.get("sourceAttemptId") != attempt.get("attemptId")
            or processing.get("sourcePreparedSha256") != attempt.get("artifacts", {}).get("prepared", {}).get("sha256")
            or processing.get("transparentRule") != "alpha=0 becomes transparent black; otherwise green>=64 and green>red+15 and green>blue+15 becomes transparent black"
            or processing.get("decontaminationRule") != "remaining green channel is capped at max(red,blue)+4"
            or processing.get("geometryChange") != "none other than removal of chroma/fringe pixels"):
        raise PipelineError("reviewed recontract processing schema or source binding is invalid")
    source_path = store.absolute(attempt["artifacts"]["prepared"]["path"])
    candidate_path = store.absolute(candidate["path"])
    with Image.open(source_path) as source_opened, Image.open(candidate_path) as candidate_opened:
        source_image, candidate_image = source_opened.convert("RGBA"), candidate_opened.convert("RGBA")
        if source_image.size != candidate_image.size:
            raise PipelineError("reviewed recontract processing changed image dimensions")
        expected = []
        for red, green, blue, alpha in pixel_data(source_image):
            if alpha == 0 or (green >= 64 and green > red + 15 and green > blue + 15):
                expected.append((0, 0, 0, 0))
            else:
                expected.append((red, min(green, max(red, blue) + 4), blue, alpha))
        if expected != pixel_data(candidate_image):
            raise PipelineError("reviewed recontract candidate is not the declared deterministic processing output")


def _bind_recontract_equipment_report(store: Store, attempt: dict[str, Any], contract: dict[str, Any]) -> None:
    """Measure an exact processed master without resizing or rewriting its pixels."""
    spec = contract["equipmentProductionSpec"]
    profile = _equipment_style_profile(store, contract)
    candidate = attempt["artifacts"]["prepared"]
    candidate_path = store.absolute(candidate["path"], must_exist=True)
    technical, hard_issues = inspect_technical(candidate_path, contract["kind"],
        expected_master_size=tuple(contract.get("canvasSpec", {}).get("masterSize", [256, 256])))
    with Image.open(candidate_path) as opened:
        image = opened.convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    metrics = _interior_style_metrics(image)
    metrics.update({"visibleBbox": list(bbox) if bbox else None,
                    "visibleSize": [bbox[2] - bbox[0], bbox[3] - bbox[1]] if bbox else None,
                    "baseline": bbox[3] - 1 if bbox else None})
    minimum, maximum = spec["visibleHeightRange"]
    if not bbox or not minimum <= bbox[3] - bbox[1] <= maximum:
        hard_issues.append("equipment_visible_height_out_of_range")
    if not bbox or bbox[3] - 1 != int(profile.get("baseline", 236)):
        hard_issues.append("equipment_baseline_mismatch")
    if hard_issues:
        raise PipelineError("recontract equipment technical gate failed: " + ", ".join(sorted(set(hard_issues))))
    preview_image = clean_exact_chroma(make_preview(image))
    preview_path = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "equipment-preview.png"
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    if preview_path.exists():
        with Image.open(preview_path) as existing:
            if existing.mode != "RGBA" or existing.size != preview_image.size or existing.tobytes() != preview_image.tobytes():
                raise PipelineError("recontract equipment preview collision")
    else:
        preview_image.save(preview_path, format="PNG", optimize=False, compress_level=9)
    preview = _bound_artifact(store, str(preview_path))
    _, preview_issues = inspect_technical(preview_path, contract["kind"], require_master_canvas=False)
    if preview_issues:
        raise PipelineError("recontract equipment preview technical gate failed: " + ", ".join(preview_issues))
    limits = profile.get("hardGates", {})
    advisories = []
    if metrics["interiorColorBins"] > int(limits.get("maxInteriorColorBins", 40)):
        advisories.append("equipment_palette_complexity_review")
    if metrics["smoothGradientRatio"] > float(limits.get("maxSmoothGradientRatio", 0.12)):
        advisories.append("equipment_smooth_gradient_review")
    payload = {"contractId": contract["contractId"],
               "contractSha256": sha256_file(store.record("contracts", contract["contractId"])),
               "profile": {"path": spec["profilePath"], "sha256": spec["profileSha256"]},
               "source": candidate, "candidate": candidate, "preview": preview, "metrics": metrics,
               "processingMode": "exact-reviewed-recontract-no-resize", "quantized": False,
               "reviewedRecontractId": attempt["reviewedRecontractId"], "technical": technical,
               "hardIssues": [], "advisories": advisories, "issues": [], "passed": True}
    report_id = stable_id("equipment-style-report", payload)
    report_path = store.record("style-reports", report_id)
    write_json_idempotent(report_path, {"schemaVersion": 1, "styleReportId": report_id, **payload}, immutable=True)
    update_provenance(store, [candidate, preview], contract)
    attempt["artifacts"]["equipmentPreview"] = preview
    attempt["equipmentStyleReportId"] = report_id
    attempt["report"] = {"path": store.relative(report_path), "sha256": sha256_file(report_path)}


def recontract_reviewed_attempt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    source_attempt = load_json(store.record("attempts", args.source_attempt_id))
    source_job = load_json(store.record("jobs", source_attempt["jobId"]))
    target_contract_path = store.record("contracts", args.contract_id)
    target_contract = load_json(target_contract_path)
    if args.reviewer != "cty41":
        raise PipelineError("reviewed recontract reviewer must be cty41")
    _iso_timestamp(args.accepted_at, "--accepted-at")
    feedback_id = source_attempt.get("feedbackId")
    if not feedback_id:
        raise PipelineError("reviewed recontract requires human-selected source feedback")
    feedback = load_json(store.record("feedback", feedback_id))
    if feedback.get("authorType") != "human" or feedback.get("verdict") != "selected" or feedback.get("reviewer") != "cty41":
        raise PipelineError("reviewed recontract source must be explicitly selected by cty41")
    source_lineage = _validated_generation_lineage(store, source_attempt)
    candidate_override = getattr(args, "candidate", None)
    candidate = (_bound_artifact(store, candidate_override) if candidate_override
                 else source_attempt.get("artifacts", {}).get("prepared"))
    if not candidate or not _artifact_binding_matches(store, candidate):
        raise PipelineError("reviewed recontract requires a hash-bound prepared source candidate")
    processing = None
    if candidate_override:
        processing_path = getattr(args, "processing", None)
        if not processing_path:
            raise PipelineError("reviewed recontract candidate override requires --processing")
        processing_rel = store.relative(processing_path, must_exist=True)
        parameters = load_json(store.absolute(processing_rel))
        _validate_recontract_processing(store, source_attempt, candidate, parameters)
        processing = {"path": processing_rel, "sha256": sha256_file(store.absolute(processing_rel)),
                      "parameters": parameters}
    payload = {
        "sourceAttemptId": source_attempt["attemptId"],
        "sourceJobId": source_job["jobId"],
        "sourceContractId": source_job["contractId"],
        "generationInvocationId": source_attempt.get("generationInvocationId"),
        "generationDeliveryId": source_attempt.get("generationDeliveryId"),
        "sourceLineage": source_lineage,
        "candidate": candidate,
        "processing": processing,
        "targetContractId": args.contract_id,
        "targetContractSha256": sha256_file(target_contract_path),
        "reviewer": args.reviewer,
        "reason": args.reason,
        "acceptedAt": args.accepted_at,
    }
    receipt_id = stable_id("reviewed-recontract", payload)
    receipt = {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "reviewedRecontractId": receipt_id, **payload}
    job_payload = {"contractId": args.contract_id, "reviewedRecontractId": receipt_id, "sourceMode": "reviewed_recontract"}
    job_id = stable_id("job", job_payload)
    job = {
        "schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "jobId": job_id, "state": "ready",
        "contractId": args.contract_id, "contractSha256": sha256_file(target_contract_path),
        "prompt": None, "inputs": [candidate],
        "target": {"direction": target_contract["direction"], "pose": target_contract["pose"]},
        "series": None, "conceptOnly": False, "contractRequirements": None,
        "requiresInvocation": False, "sourceMode": "reviewed_recontract", "reviewedRecontractId": receipt_id,
    }
    attempt_id = f"{job_id}-a001"
    artifacts = {key: value for key, value in source_attempt.get("artifacts", {}).items()
                 if key in {"source", "raw"}}
    artifacts["prepared"] = candidate
    attempt = {
        "schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "attemptId": attempt_id, "jobId": job_id,
        "ordinal": 1, "generationRound": source_attempt.get("generationRound"),
        "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
        "technicalRemediation": False, "state": "prepared", "sourceMode": "reviewed_recontract",
        "reviewedRecontractId": receipt_id, "sourceAttemptId": source_attempt["attemptId"],
        "generationInvocationId": source_attempt.get("generationInvocationId"),
        "generationDeliveryId": source_attempt.get("generationDeliveryId"),
        "artifacts": artifacts, "calibration": None,
        "report": None, "approvalId": None, "feedbackId": None,
    }
    if target_contract.get("equipmentProductionSpec"):
        _bind_recontract_equipment_report(store, attempt, target_contract)
    write_json_idempotent(store.record("reviewed-recontracts", receipt_id), receipt, immutable=True)
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    write_json_idempotent(store.record("attempts", attempt_id), attempt, immutable=True)
    return {"schemaVersion": ART_DIRECTION_SCHEMA_VERSION, "receipt": receipt, "job": job, "attempt": attempt}


def _component_contract(store: Store, contract_id_value: str, kind: str | None = None) -> dict[str, Any]:
    contract = load_json(store.record("contracts", contract_id_value))
    if contract.get("schemaVersion") not in {3, ART_DIRECTION_SCHEMA_VERSION} or contract.get("assetRole") != "component":
        raise PipelineError("operation requires a schema v3/v4 component contract")
    if kind and contract.get("componentKind") != kind:
        raise PipelineError(f"component contract must have kind {kind}")
    return contract


def migrate_component(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if args.reviewer != "cty41":
        raise PipelineError("pre-v3 component migration requires reviewer cty41")
    raise PipelineError("migrate-component is retired after the audited pre-v3 migration closeout; use generated or derived components")


def derive_component(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    label_kind = {
        "near_hand": "paw_overlay", "far_hand": "paw_overlay",
        "near_foot": "foot_overlay", "far_foot": "foot_overlay",
        "body": "body", "equipment": "equipment",
    }
    if args.label not in label_kind:
        raise PipelineError("unsupported derived component semantic label")
    contract = _component_contract(store, args.contract_id, label_kind[args.label])
    if contract.get("sourceMode") != "derived":
        raise PipelineError("derive-component requires sourceMode derived")
    source_attempt = load_json(store.record("attempts", args.source_attempt_id))
    if source_attempt.get("state") not in {"approved", "promoted"}:
        raise PipelineError("derived components require an approved source pose")
    source_job = load_json(store.record("jobs", source_attempt["jobId"]))
    source_contract = load_json(store.record("contracts", source_job["contractId"]))
    if source_contract.get("assetRole") == "component":
        if source_contract.get("componentKind") != "body":
            raise PipelineError("derived overlays require a body component or complete pose source")
    elif source_contract.get("kind") not in {"ground_character", "action_pose"}:
        raise PipelineError("derived overlays require a body component or complete pose source")
    source_artifact = candidate_artifact(source_attempt)
    mask_artifact = candidate_mask_artifact(source_attempt)
    if not mask_artifact:
        raise PipelineError("derived overlay requires an approved source semantic mask")
    image = Image.open(store.absolute(source_artifact["path"], must_exist=True)).convert("RGBA")
    mask = Image.open(store.absolute(mask_artifact["path"], must_exist=True)).convert("RGBA")
    if image.size != mask.size:
        raise PipelineError("source component and semantic mask sizes differ")
    selected_labels = {"core", "head_appendage"} if args.label == "body" else {args.label}
    selected_colors = {MASK_COLORS[label] for label in selected_labels}
    derived = Image.new("RGBA", image.size, (0, 0, 0, 0))
    derived.putdata([pixel if mask_pixel in selected_colors else (0, 0, 0, 0)
                     for pixel, mask_pixel in zip(pixel_data(image), pixel_data(mask))])
    derived_mask = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    derived_mask.putdata([mask_pixel if mask_pixel in selected_colors else (0, 0, 0, 0)
                          for mask_pixel in pixel_data(mask)])
    payload = {
        "contractId": args.contract_id, "sourceAttemptId": args.source_attempt_id,
        "sourceCandidateSha256": source_artifact["sha256"], "sourceMaskSha256": mask_artifact["sha256"],
        "label": args.label,
    }
    derivation_id = stable_id("component-derivation", payload)
    job_id = stable_id("job", payload)
    attempt_id = f"{job_id}-a001"
    output = store.pipeline / "artifacts" / job_id / attempt_id / "prepared.png"
    mask_output = store.pipeline / "artifacts" / job_id / attempt_id / "mask.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    temp = output.with_name(".prepared.tmp.png")
    derived.save(temp, format="PNG", optimize=False, compress_level=9)
    if output.exists() and sha256_file(output) != sha256_file(temp):
        temp.unlink()
        raise PipelineError("derived component output is not deterministic")
    if output.exists():
        temp.unlink()
    else:
        os.replace(temp, output)
    artifact = {"path": store.relative(output), "sha256": sha256_file(output)}
    mask_temp = mask_output.with_name(".mask.tmp.png")
    derived_mask.save(mask_temp, format="PNG", optimize=False, compress_level=9)
    if mask_output.exists() and sha256_file(mask_output) != sha256_file(mask_temp):
        mask_temp.unlink()
        raise PipelineError("derived component mask is not deterministic")
    if mask_output.exists():
        mask_temp.unlink()
    else:
        os.replace(mask_temp, mask_output)
    mask_output_artifact = {"path": store.relative(mask_output), "sha256": sha256_file(mask_output)}
    receipt = {"schemaVersion": 3, "componentDerivationId": derivation_id, **payload,
               "artifact": artifact, "maskArtifact": mask_output_artifact}
    write_json_idempotent(store.record("component-derivations", derivation_id), receipt, immutable=True)
    job = {
        "schemaVersion": 3, "jobId": job_id, "state": "ready", "contractId": args.contract_id,
        "contractSha256": sha256_file(store.record("contracts", args.contract_id)), "prompt": None,
        "inputs": [source_artifact, mask_artifact], "target": {"direction": contract["direction"], "pose": contract["pose"]},
        "series": None, "conceptOnly": False, "contractRequirements": None,
        "requiresInvocation": False, "sourceMode": "derived",
    }
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    attempt = {
        "schemaVersion": 3, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
        "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
        "technicalRemediation": False, "state": "annotated",
        "artifacts": {"prepared": artifact, "mask": mask_output_artifact},
        "report": None, "approvalId": None, "feedbackId": None,
        "componentDerivationId": derivation_id, "sourceMode": "derived",
    }
    write_json_idempotent(store.record("attempts", attempt_id), attempt)
    register_public_artifacts(store, [artifact, mask_output_artifact], "project-owned-supporting-derived")
    return attempt


def apply_assembly_transform(image: Image.Image, transform: dict[str, Any]) -> Image.Image:
    scale = transform["scalePercent"]
    width = max(1, round(image.width * scale / 100))
    height = max(1, round(image.height * scale / 100))
    result = image.resize((width, height), Image.Resampling.LANCZOS) if scale != 100 else image.copy()
    if transform["flipHorizontal"]:
        result = result.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return normalize_transparent_rgb(result)


def apply_assembly_mask_transform(image: Image.Image, transform: dict[str, Any]) -> Image.Image:
    """Apply the assembly transform without inventing interpolated semantic labels."""
    scale = transform["scalePercent"]
    width = max(1, round(image.width * scale / 100))
    height = max(1, round(image.height * scale / 100))
    result = image.resize((width, height), Image.Resampling.NEAREST) if scale != 100 else image.copy()
    if transform["flipHorizontal"]:
        result = result.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return normalize_transparent_rgb(result.convert("RGBA"))


def _mask_geometry(mask: Image.Image, color: tuple[int, int, int, int]) -> dict[str, float]:
    points = [(index % mask.width, index // mask.width)
              for index, pixel in enumerate(pixel_data(mask)) if pixel == color]
    if len(points) < 4:
        raise PipelineError("death geometry mask requires at least four core pixels")
    cx = sum(point[0] for point in points) / len(points)
    cy = sum(point[1] for point in points) / len(points)
    xx = sum((x - cx) ** 2 for x, _y in points) / len(points)
    yy = sum((y - cy) ** 2 for _x, y in points) / len(points)
    xy = sum((x - cx) * (y - cy) for x, y in points) / len(points)
    angle = 0.5 * math.atan2(2 * xy, xx - yy)
    ux, uy = math.cos(angle), math.sin(angle)
    vx, vy = -uy, ux
    major_values = [(x - cx) * ux + (y - cy) * uy for x, y in points]
    minor_values = [(x - cx) * vx + (y - cy) * vy for x, y in points]
    major = max(major_values) - min(major_values) + 1
    minor = max(minor_values) - min(minor_values) + 1
    if major < minor:
        major, minor = minor, major
        angle += math.pi / 2
    while angle >= math.pi / 2:
        angle -= math.pi
    while angle < -math.pi / 2:
        angle += math.pi
    return {"centerX": cx, "centerY": cy, "angleRadians": angle,
            "angleDegrees": math.degrees(angle), "major": major, "minor": minor,
            "ratio": major / minor}


def _affine_inverse_coefficients(matrix: tuple[float, float, float, float],
                                 source_center: tuple[float, float],
                                 target_center: tuple[float, float]) -> tuple[float, ...]:
    a, b, c, d = matrix
    determinant = a * d - b * c
    if abs(determinant) < 1e-8:
        raise PipelineError("death geometry transform is singular")
    ia, ib, ic, id_ = d / determinant, -b / determinant, -c / determinant, a / determinant
    sx, sy = source_center; tx, ty = target_center
    return ia, ib, sx - ia * tx - ib * ty, ic, id_, sy - ic * tx - id_ * ty


def _semantic_layer(image: Image.Image, mask: Image.Image, labels: set[str]) -> Image.Image:
    colors = {MASK_COLORS[label] for label in labels}
    result = Image.new("RGBA", image.size, (0, 0, 0, 0))
    result.putdata([pixel if mask_pixel in colors else (0, 0, 0, 0)
                    for pixel, mask_pixel in zip(pixel_data(image), pixel_data(mask))])
    return result


def _transform_semantic_layer(image: Image.Image, mask: Image.Image, labels: set[str],
                              matrix: tuple[float, float, float, float],
                              source_center: tuple[float, float], target_center: tuple[float, float]) -> tuple[Image.Image, Image.Image]:
    coefficients = _affine_inverse_coefficients(matrix, source_center, target_center)
    layer = _semantic_layer(image, mask, labels)
    label_layer = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    allowed = {MASK_COLORS[label] for label in labels}
    label_layer.putdata([value if value in allowed else (0, 0, 0, 0) for value in pixel_data(mask)])
    transformed = layer.transform(image.size, Image.Transform.AFFINE, coefficients, Image.Resampling.BICUBIC)
    transformed_mask = label_layer.transform(mask.size, Image.Transform.AFFINE, coefficients, Image.Resampling.NEAREST)
    return normalize_transparent_rgb(transformed), normalize_transparent_rgb(transformed_mask)


def render_death_recipe(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    spec_rel = store.relative(args.spec, must_exist=True)
    spec = json.loads(store.absolute(spec_rel).read_text(encoding="utf-8"))
    required = {"schemaVersion", "assetId", "sourceImage", "sourceMask", "referenceMask",
                "output", "reviewOutput", "angleToleranceDegrees", "ratioTolerance"}
    optional = {"expressionOverlay", "eyeRegions", "axisPolicy"}
    if not isinstance(spec, dict) or set(spec) - optional != required or spec["schemaVersion"] != 1:
        raise PipelineError("death recipe requires schemaVersion 1 and the canonical geometry fields")
    source_rel = store.relative(spec["sourceImage"], must_exist=True)
    source_mask_rel = store.relative(spec["sourceMask"], must_exist=True)
    reference_mask_rel = store.relative(spec["referenceMask"], must_exist=True)
    source = Image.open(store.absolute(source_rel)).convert("RGBA")
    source_mask = Image.open(store.absolute(source_mask_rel)).convert("RGBA")
    reference_mask = Image.open(store.absolute(reference_mask_rel)).convert("RGBA")
    if source.size != (256, 256) or source_mask.size != source.size or reference_mask.size != source.size:
        raise PipelineError("death recipe inputs must be matching 256x256 images")
    source_geometry = _mask_geometry(source_mask, MASK_COLORS["core"])
    reference_geometry = _mask_geometry(reference_mask, MASK_COLORS["core"])
    source_angle = source_geometry["angleRadians"]; target_angle = reference_geometry["angleRadians"]
    target_ratio = reference_geometry["ratio"]
    if spec.get("axisPolicy", "legacy-major-only") == "legacy-major-only":
        major_scale = min(1.0, target_ratio / source_geometry["ratio"])
        minor_scale = 1.0
    elif target_ratio >= source_geometry["ratio"]:
        major_scale = 1.0
        minor_scale = source_geometry["ratio"] / target_ratio
    elif spec.get("axisPolicy") == "toward-reference-no-expand":
        major_scale = target_ratio / source_geometry["ratio"]
        minor_scale = 1.0
    else:
        raise PipelineError("death recipe axisPolicy must be toward-reference-no-expand")
    cos_s, sin_s = math.cos(source_angle), math.sin(source_angle)
    cos_t, sin_t = math.cos(target_angle), math.sin(target_angle)
    # R(target) * diag(major_scale, minor_scale) * R(-source).  Only the
    # axis that moves the ratio toward the approved reference may shrink;
    # death shaping never expands either local axis.
    matrix = (
        cos_t * major_scale * cos_s + sin_t * minor_scale * sin_s,
        cos_t * major_scale * sin_s - sin_t * minor_scale * cos_s,
        sin_t * major_scale * cos_s - cos_t * minor_scale * sin_s,
        sin_t * major_scale * sin_s + cos_t * minor_scale * cos_s,
    )
    source_center = (source_geometry["centerX"], source_geometry["centerY"])
    target_center = source_center
    core_image, core_mask = _transform_semantic_layer(source, source_mask, {"core"}, matrix, source_center, target_center)
    delta = target_angle - source_angle; rotation = (math.cos(delta), -math.sin(delta), math.sin(delta), math.cos(delta))
    attachments_image, attachments_mask = _transform_semantic_layer(
        source, source_mask, {"head_appendage", "near_hand", "far_hand", "near_foot", "far_foot"},
        rotation, source_center, target_center)
    equipment_image = _semantic_layer(source, source_mask, {"equipment"})
    equipment_mask = Image.new("RGBA", source.size, (0, 0, 0, 0))
    equipment_mask.putdata([value if value == MASK_COLORS["equipment"] else (0, 0, 0, 0)
                            for value in pixel_data(source_mask)])
    canvas = Image.new("RGBA", source.size, (0, 0, 0, 0)); mask_canvas = canvas.copy()
    for layer in (core_image, attachments_image, equipment_image): canvas.alpha_composite(layer)
    for layer in (core_mask, attachments_mask, equipment_mask): mask_canvas.alpha_composite(layer)
    expression_artifact = None
    expression = None
    if spec.get("expressionOverlay"):
        expression_rel = store.relative(spec["expressionOverlay"], must_exist=True)
        expression = Image.open(store.absolute(expression_rel)).convert("RGBA")
        if expression.size != source.size:
            raise PipelineError("death expression overlay must be 256x256")
        regions = spec.get("eyeRegions") or []
        if not regions or any(not isinstance(region, list) or len(region) != 4 for region in regions):
            raise PipelineError("death expression overlay requires eyeRegions rectangles")
        for index, pixel in enumerate(pixel_data(expression)):
            if not pixel[3]: continue
            x, y = index % expression.width, index // expression.width
            if not any(x0 <= x <= x1 and y0 <= y <= y1 for x0, y0, x1, y1 in regions):
                raise PipelineError("death expression overlay has opaque pixels outside eyeRegions")
        expression_artifact = {"path": expression_rel, "sha256": sha256_file(store.absolute(expression_rel))}
    alpha_box = canvas.getbbox()
    if not alpha_box:
        raise PipelineError("death recipe produced an empty image")
    cx = (alpha_box[0] + alpha_box[2]) // 2; cy = (alpha_box[1] + alpha_box[3]) // 2
    shift = (128 - cx, 128 - cy)
    # Recompose without wrap-around; alpha_composite clips at the canvas edge.
    composed = Image.alpha_composite(Image.alpha_composite(core_image, attachments_image), equipment_image)
    canvas = Image.new("RGBA", source.size, (0, 0, 0, 0)); canvas.alpha_composite(composed, shift)
    if expression is not None:
        # Expression overlays are authored against the final centered face and
        # therefore compose after geometry centering.
        canvas.alpha_composite(expression)
    mask_centered = Image.new("RGBA", source.size, (0, 0, 0, 0)); mask_centered.alpha_composite(mask_canvas, shift)
    output_rel = store.relative(spec["output"]); review_rel = store.relative(spec["reviewOutput"])
    output_path = store.absolute(output_rel); review_path = store.absolute(review_rel)
    output_path.parent.mkdir(parents=True, exist_ok=True); review_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, format="PNG", optimize=False, compress_level=9)
    result_geometry = _mask_geometry(mask_centered, MASK_COLORS["core"])
    angle_error = abs(result_geometry["angleDegrees"] - reference_geometry["angleDegrees"])
    ratio_error = abs(result_geometry["ratio"] - reference_geometry["ratio"]) / reference_geometry["ratio"]
    report = {"angleErrorDegrees": angle_error, "ratioError": ratio_error,
              "passed": angle_error <= spec["angleToleranceDegrees"] and ratio_error <= spec["ratioTolerance"]}
    review = Image.new("RGBA", (768, 256), (0, 0, 0, 0))
    reference_visual = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); reference_visual.putalpha(reference_mask.getchannel("A"))
    review.alpha_composite(source, (0, 0)); review.alpha_composite(reference_visual, (256, 0)); review.alpha_composite(canvas, (512, 0))
    review.save(review_path, format="PNG", optimize=False, compress_level=9)
    artifact = lambda rel: {"path": rel, "sha256": sha256_file(store.absolute(rel))}
    payload = {"schemaVersion": 1, "assetId": spec["assetId"], "spec": artifact(spec_rel),
               "source": artifact(source_rel), "sourceMask": artifact(source_mask_rel),
               "referenceMask": artifact(reference_mask_rel), "expressionOverlay": expression_artifact,
               "sourceGeometry": source_geometry, "referenceGeometry": reference_geometry,
               "resultGeometry": result_geometry, "shift": list(shift), "report": report,
               "artifacts": {"prepared": artifact(output_rel), "review": artifact(review_rel)}}
    recipe_id = stable_id("death-recipe", payload)
    payload["deathRecipeId"] = recipe_id
    write_json_idempotent(store.record("death-recipes", recipe_id), payload, immutable=True)
    register_supporting_artifact(store, argparse.Namespace(
        path=output_rel, role="death-recipe-candidate",
        note=f"Deterministic death recipe output {recipe_id}; candidate only, not human-approved or promoted."))
    register_supporting_artifact(store, argparse.Namespace(
        path=review_rel, role="death-recipe-review",
        note=f"Deterministic source/reference/result review for {recipe_id}."))
    return payload


def _validated_assembly_spec(store: Store, spec_path: str) -> tuple[dict[str, Any], dict[str, Any]]:
    raw = json.loads(store.absolute(store.relative(spec_path, must_exist=True)).read_text(encoding="utf-8"))
    if not isinstance(raw, dict) or set(raw) != {"assetId", "contractId", "canvas", "layers"}:
        raise PipelineError("assembly spec requires exactly assetId, contractId, canvas, and layers")
    contract = load_json(store.record("contracts", raw["contractId"]))
    if contract.get("schemaVersion") not in {3, ART_DIRECTION_SCHEMA_VERSION} or contract.get("assetRole") != "assembled_sprite":
        raise PipelineError("assembly requires a schema v3/v4 assembled_sprite contract")
    if raw["assetId"] != contract["assetId"]:
        raise PipelineError("assembly assetId must match contract")
    if raw["canvas"] != [256, 256]:
        raise PipelineError("schema v3 sprite assemblies require a 256x256 canvas")
    roles = [layer.get("role") for layer in raw["layers"]]
    if len(roles) != len(ASSEMBLY_LAYER_ROLES) or set(roles) != ASSEMBLY_LAYER_ROLES:
        raise PipelineError("assembly requires each of the six component roles exactly once")
    normalized_layers = []
    for layer in raw["layers"]:
        if set(layer) != {"role", "attemptId", "transform"} or layer["role"] not in ASSEMBLY_LAYER_ROLES:
            raise PipelineError("invalid assembly layer")
        transform = layer["transform"]
        if set(transform) != {"scalePercent", "translate", "flipHorizontal"}:
            raise PipelineError("assembly transform only supports scalePercent, translate, and flipHorizontal")
        if not isinstance(transform["scalePercent"], int) or isinstance(transform["scalePercent"], bool) or not 1 <= transform["scalePercent"] <= 400:
            raise PipelineError("assembly scalePercent must be an integer from 1 to 400")
        if (not isinstance(transform["translate"], list) or len(transform["translate"]) != 2
                or any(not isinstance(value, int) or isinstance(value, bool) for value in transform["translate"])):
            raise PipelineError("assembly translate must contain two integers")
        if not isinstance(transform["flipHorizontal"], bool):
            raise PipelineError("assembly flipHorizontal must be boolean")
        attempt = load_json(store.record("attempts", layer["attemptId"]))
        job = load_json(store.record("jobs", attempt["jobId"]))
        component_contract = _component_contract(store, job["contractId"])
        if layer["role"].endswith("paw_overlay"):
            expected_kind = "paw_overlay"
        elif layer["role"].endswith("foot_overlay"):
            expected_kind = "foot_overlay"
        else:
            expected_kind = layer["role"]
        if component_contract.get("componentKind") != expected_kind:
            raise PipelineError("assembly layer role does not match component kind")
        artifact = candidate_artifact(attempt)
        mask_artifact = candidate_mask_artifact(attempt)
        if not mask_artifact:
            raise PipelineError("assembly components require semantic masks")
        with Image.open(store.absolute(artifact["path"], must_exist=True)) as image_source, Image.open(
                store.absolute(mask_artifact["path"], must_exist=True)) as mask_source:
            component_image = image_source.convert("RGBA")
            component_mask = mask_source.convert("RGBA")
        if component_image.size != component_mask.size:
            raise PipelineError("assembly component and semantic mask sizes differ")
        expected_labels = {
            "far_foot_overlay": {"far_foot"},
            "far_paw_overlay": {"far_hand"},
            "body": {"core", "head_appendage"},
            "equipment": {"equipment"},
            "near_paw_overlay": {"near_hand"},
            "near_foot_overlay": {"near_foot"},
        }[layer["role"]]
        allowed_colors = {MASK_COLORS[label] for label in expected_labels}
        for subject_pixel, mask_pixel in zip(pixel_data(component_image), pixel_data(component_mask)):
            if mask_pixel[3] and mask_pixel not in allowed_colors:
                raise PipelineError(f"assembly layer {layer['role']} contains a foreign semantic label")
            if subject_pixel[3] and mask_pixel not in allowed_colors:
                raise PipelineError(f"assembly layer {layer['role']} contains unlabelled subject pixels")
        source_mode = component_contract.get("sourceMode")
        if source_mode == "generated":
            if attempt.get("state") not in {"approved", "promoted"} or not attempt.get("approvalId"):
                raise PipelineError("generated assembly components require human approval")
            approval = load_json(store.record("approvals", attempt["approvalId"]))
            if (approval.get("decision") != "approved" or approval.get("reviewer") != "cty41"
                    or approval.get("attemptId") != attempt["attemptId"]
                    or approval.get("candidateSha256") != artifact.get("sha256")
                    or approval.get("maskSha256") != mask_artifact.get("sha256")):
                raise PipelineError("generated component approval receipt mismatch")
        elif source_mode in {"derived", "pre_v3_import"}:
            report_artifact = attempt.get("report") or {}
            report_path = store.absolute(report_artifact.get("path", ""), must_exist=True)
            if sha256_file(report_path) != report_artifact.get("sha256") or not load_json(report_path).get("passed"):
                raise PipelineError(f"{source_mode} assembly components require a passing validation report")
            if attempt.get("state") not in {"review_pending", "approved", "promoted"}:
                raise PipelineError(f"{source_mode} assembly component is not review-ready")
            receipt_group = "component-derivations" if source_mode == "derived" else "component-migrations"
            receipt_key = "componentDerivationId" if source_mode == "derived" else "componentMigrationId"
            receipt_id = attempt.get(receipt_key)
            if not receipt_id:
                raise PipelineError(f"{source_mode} assembly component is missing its source receipt")
            receipt = load_json(store.record(receipt_group, receipt_id))
            receipt_artifact = receipt.get("artifact") if source_mode == "derived" else receipt.get("prepared")
            if (receipt.get("contractId") != component_contract["contractId"]
                    or not receipt_artifact or receipt_artifact.get("sha256") != artifact.get("sha256")):
                raise PipelineError(f"{source_mode} component source receipt mismatch")
        else:
            raise PipelineError("assembly component has unsupported source mode")
        normalized_layers.append({**layer, "artifact": artifact, "componentContractId": component_contract["contractId"]})
    normalized = {"assetId": raw["assetId"], "contractId": raw["contractId"], "canvas": raw["canvas"], "layers": normalized_layers}
    return normalized, contract


def create_assembly(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    normalized, _contract = _validated_assembly_spec(store, args.spec)
    assembly_id = stable_id("assembly", normalized)
    record = {"schemaVersion": 3, "assemblyId": assembly_id, **normalized}
    write_json_idempotent(store.record("assemblies", assembly_id), record, immutable=True)
    return record


def render_assembly(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    assembly = load_json(store.record("assemblies", args.assembly_id))
    contract = load_json(store.record("contracts", assembly["contractId"]))
    job_payload = {"assemblyId": args.assembly_id, "contractId": assembly["contractId"], "rendererVersion": 4}
    job_id = stable_id("job", job_payload)
    attempt_id = f"{job_id}-a001"
    attempt_path = store.record("attempts", attempt_id)
    if attempt_path.is_file():
        return load_json(attempt_path)
    canvas = Image.new("RGBA", tuple(assembly["canvas"]), (0, 0, 0, 0))
    mask_canvas = Image.new("RGBA", tuple(assembly["canvas"]), (0, 0, 0, 0))
    for layer in assembly["layers"]:
        source_path = store.absolute(layer["artifact"]["path"], must_exist=True)
        if sha256_file(source_path) != layer["artifact"]["sha256"]:
            raise PipelineError("assembly component hash drift")
        rendered = apply_assembly_transform(Image.open(source_path).convert("RGBA"), layer["transform"])
        canvas.alpha_composite(rendered, tuple(layer["transform"]["translate"]))
        layer_attempt = load_json(store.record("attempts", layer["attemptId"]))
        mask_artifact = candidate_mask_artifact(layer_attempt)
        mask_path = store.absolute(mask_artifact["path"], must_exist=True)
        if sha256_file(mask_path) != mask_artifact["sha256"]:
            raise PipelineError("assembly component mask hash drift")
        rendered_mask = apply_assembly_mask_transform(Image.open(mask_path), layer["transform"])
        mask_canvas.alpha_composite(rendered_mask, tuple(layer["transform"]["translate"]))
    # Resampling a keyed component can reintroduce subpixel green on the edge.
    # Exact/fuzzy chroma is forbidden by the sprite contract, so normalize it
    # deterministically after the full layer stack has been composed.
    canvas = clean_resampled_chroma(canvas, "#00ff00", 12)
    mask_pixels = []
    for mask_pixel, subject_pixel in zip(pixel_data(mask_canvas), pixel_data(canvas)):
        subject_alpha = subject_pixel[3]
        mask_pixels.append(mask_pixel if subject_alpha else (0, 0, 0, 0))
    mask_canvas.putdata(mask_pixels)
    mask_canvas = normalize_transparent_rgb(mask_canvas)
    output = store.pipeline / "artifacts" / job_id / attempt_id / "prepared.png"
    mask_output = output.with_name("mask.png")
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, format="PNG", optimize=False, compress_level=9)
    mask_canvas.save(mask_output, format="PNG", optimize=False, compress_level=9)
    artifact = {"path": store.relative(output), "sha256": sha256_file(output)}
    mask_output_artifact = {"path": store.relative(mask_output), "sha256": sha256_file(mask_output)}
    receipt_payload = {
        "assemblyId": args.assembly_id,
        "rendererVersion": 4,
        "assemblySha256": sha256_file(store.record("assemblies", args.assembly_id)),
        "output": artifact,
        "maskOutput": mask_output_artifact,
    }
    receipt_id = stable_id("assembly-render", receipt_payload)
    receipt = {"schemaVersion": 3, "assemblyRenderId": receipt_id, **receipt_payload}
    write_json_idempotent(store.record("assembly-renders", receipt_id), receipt, immutable=True)
    job = {
        "schemaVersion": 3, "jobId": job_id, "state": "ready", "contractId": contract["contractId"],
        "contractSha256": sha256_file(store.record("contracts", contract["contractId"])), "prompt": None,
        "inputs": [layer["artifact"] for layer in assembly["layers"]],
        "target": {"direction": contract["direction"], "pose": contract["pose"]}, "series": None,
        "conceptOnly": False, "contractRequirements": None, "requiresInvocation": False, "sourceMode": "derived",
    }
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    attempt = {
        "schemaVersion": 3, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
        "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
        "technicalRemediation": False, "state": "annotated",
        "artifacts": {"prepared": artifact, "mask": mask_output_artifact},
        "report": None, "approvalId": None, "feedbackId": None,
        "assemblyId": args.assembly_id, "assemblyRenderId": receipt_id, "sourceMode": "derived",
    }
    write_json_idempotent(attempt_path, attempt)
    register_public_artifacts(store, [artifact, mask_output_artifact], "project-owned-supporting-derived")
    return attempt


def update_approved_cases(store: Store, contract: dict[str, Any], master_path: str) -> None:
    cases_path = store.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
    if not cases_path.is_file():
        return
    # Identity aliases on action/death contracts must not replace Idle direction mothers.
    if contract.get("approvedAssetId") and contract["approvedAssetId"] != contract["assetId"]:
        return
    cases = json.loads(cases_path.read_text(encoding="utf-8"))
    direction_key = contract["direction"].replace("-", "_")
    if direction_key not in {"down_right", "up_left"}:
        return
    asset_id = contract.get("approvedAssetId", contract["assetId"])
    directions: dict[str, str] = {}
    for attempt_path in (store.pipeline / "attempts").glob("*.json"):
        attempt = load_json(attempt_path)
        if attempt.get("state") != "promoted":
            continue
        job = load_json(store.record("jobs", attempt["jobId"]))
        promoted_contract = load_json(store.record("contracts", job["contractId"]))
        if promoted_contract.get("approvedAssetId", promoted_contract["assetId"]) != asset_id:
            continue
        key = promoted_contract["direction"].replace("-", "_")
        promoted_master = attempt.get("artifacts", {}).get("promoted", {}).get("master", {}).get("path")
        if key in {"down_right", "up_left"} and promoted_master:
            directions[key] = promoted_master
    directions[direction_key] = master_path
    entry = next((item for item in cases.get("approved_assets", []) if item.get("id") == asset_id), None)
    if set(directions) != {"down_right", "up_left"}:
        if entry is not None:
            cases["approved_assets"].remove(entry)
        write_json_idempotent(cases_path, cases)
        return
    if entry is None:
        entry = {"id": asset_id}
        cases.setdefault("approved_assets", []).append(entry)
    for key, path in directions.items():
        existing = entry.get(key)
        if existing and existing != path:
            raise PipelineError(f"approved mother list already has a different {key} path for {asset_id}")
        entry[key] = path
    write_json_idempotent(cases_path, cases)


def promote(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] == "promoted":
        return attempt
    if attempt["state"] != "approved":
        raise PipelineError("promote requires approved attempt")
    approval = load_json(store.record("approvals", attempt["approvalId"]))
    candidate = candidate_artifact(attempt)
    if approval["candidateSha256"] != candidate["sha256"]:
        raise PipelineError("approval no longer matches candidate")
    if approval.get("approvalMode") == "gate-exception":
        validate_exception_receipt(store, attempt, approval)
    job = load_json(store.record("jobs", attempt["jobId"]))
    binding = job.get("series")
    bound_series = bound_pose = None
    if binding:
        bound_series = load_json(store.record("series", binding["seriesId"]))
        bound_pose = series_pose(bound_series, binding["poseId"])
        if job.get("conceptOnly") or bound_pose["state"] == "provisional" or bound_series.get("provisionalAnchorAttemptId") == attempt["attemptId"]:
            raise PipelineError("provisional series artwork cannot be promoted")
    contract = load_json(store.record("contracts", job["contractId"]))
    if contract.get("schemaVersion") == ART_DIRECTION_SCHEMA_VERSION:
        _validate_art_direction_approval(store, attempt, contract)
    elif contract.get("equipmentProductionSpec"):
        verdict_id = attempt.get("equipmentStyleVerdictId")
        verdict = load_json(store.record("equipment-style-verdicts", verdict_id or "")) if verdict_id else None
        if not verdict or verdict.get("decision") != "approved" or verdict.get("candidateSha256") != candidate["sha256"]:
            raise PipelineError("equipment promotion requires matching approved style verdict")
        if verdict.get("reviewSha256") != approval_review_hashes(store, attempt, contract):
            raise PipelineError("equipment style verdict review hash mismatch")
    if contract.get("assetRole") == "component" or contract.get("runtimeEligible") is False:
        raise PipelineError("approved components cannot be promoted as runtime sprites")
    master = copy_bound(store, candidate["path"], contract["outputs"]["master"])
    preview_path = store.absolute(contract["outputs"]["preview"])
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    if attempt.get("sourceMode") == "reviewed_import":
        imported_preview = attempt.get("artifacts", {}).get("importedPreview")
        if not imported_preview:
            raise PipelineError("reviewed import is missing its approved preview")
        preview_artifact = copy_bound(store, imported_preview["path"], contract["outputs"]["preview"])
    else:
        master_image = Image.open(store.absolute(master["path"])).convert("RGBA")
        preview = make_preview(master_image)
        if preview_path.exists():
            temp = preview_path.with_suffix(".candidate.png")
            preview.save(temp, format="PNG", optimize=False, compress_level=9)
            if sha256_file(temp) != sha256_file(preview_path):
                temp.unlink()
                raise PipelineError("promotion preview exists with different bytes")
            temp.unlink()
        else:
            preview.save(preview_path, format="PNG", optimize=False, compress_level=9)
        preview_artifact = {"path": store.relative(preview_path), "sha256": sha256_file(preview_path)}
    update_provenance(store, [master, preview_artifact], contract)
    update_approved_cases(store, contract, master["path"])
    attempt["artifacts"]["promoted"] = {"master": master, "preview": preview_artifact}
    transition(attempt, {"approved"}, "promoted")
    save_attempt(store, attempt)
    if bound_series and bound_pose:
        bound_pose["state"] = "promoted"
        save_series(store, bound_series)
    return attempt


def refresh_promoted_preview(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt.get("state") != "promoted":
        raise PipelineError("refresh-promoted-preview requires a promoted attempt")
    promoted = attempt.get("artifacts", {}).get("promoted", {})
    master_artifact = promoted.get("master")
    preview_artifact = promoted.get("preview")
    if not master_artifact or not preview_artifact:
        raise PipelineError("promoted attempt is missing master or preview")
    master_path = store.absolute(master_artifact["path"], must_exist=True)
    if sha256_file(master_path) != master_artifact["sha256"]:
        raise PipelineError("promoted master hash mismatch")
    preview_path = store.absolute(preview_artifact["path"], must_exist=True)
    with Image.open(master_path) as master_image:
        regenerated = make_preview(master_image.convert("RGBA"))
    temporary = preview_path.with_suffix(".refresh.png")
    regenerated.save(temporary, format="PNG", optimize=False, compress_level=9)
    if sha256_file(temporary) != sha256_file(preview_path):
        temporary.replace(preview_path)
    else:
        temporary.unlink()
    new_hash = sha256_file(preview_path)
    preview_artifact["sha256"] = new_hash
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    manifest = load_json(manifest_path)
    entry = next((item for item in manifest.get("entries", []) if item.get("path") == preview_artifact["path"]), None)
    if not entry:
        raise PipelineError("promoted preview provenance entry is missing")
    entry["sha256"] = new_hash
    write_json_idempotent(manifest_path, manifest)
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    update_approved_cases(store, contract, master_artifact["path"])
    save_attempt(store, attempt)
    return attempt


def inventory_state(rel: str) -> str:
    parts = set(Path(rel).parts)
    if "rejected" in parts or "superseded" in parts:
        return "legacy-rejected"
    if parts & FORMAL_DIRS:
        return "legacy-approved"
    return "legacy-unresolved"


def migrate_legacy(store: Store, _args: argparse.Namespace) -> dict[str, Any]:
    provenance_path = store.root / "Tools/public-release/asset-provenance.json"
    provenance = load_json(provenance_path) if provenance_path.exists() else {"entries": []}
    approved_hashes = {entry["path"]: entry["sha256"] for entry in provenance.get("entries", []) if entry.get("status") == "approved"}
    entries = []
    for path in sorted((store.root / "Tools/artworks").rglob("*.png")):
        if store.pipeline in path.parents:
            continue
        rel = store.relative(path)
        state = inventory_state(rel)
        digest = sha256_file(path)
        if state == "legacy-approved" and approved_hashes.get(rel) != digest:
            state = "legacy-unresolved"
        entries.append({"path": rel, "sha256": digest, "state": state, "lineage": None,
                        "mayBeAnchor": False, "note": "directory is historical state evidence only"})
    record = {"schemaVersion": 1, "assets": entries}
    write_json_idempotent(store.pipeline / "legacy-assets.json", record)
    provenance_entries = provenance.get("entries", [])
    by_path = {entry["path"]: entry for entry in provenance_entries}
    for path in sorted((store.root / "Tools/artworks").rglob("*.png")):
        rel = store.relative(path)
        if rel in by_path:
            continue
        entry = {
            "path": rel, "sha256": sha256_file(path), "status": "approved",
            "rightsHolder": "cty41", "license": "CC-BY-4.0",
            "provenance": "project-owned-gpt-generated-or-derived",
        }
        by_path[rel] = entry
        provenance_entries.append(entry)
    provenance["entries"] = provenance_entries
    write_json_idempotent(provenance_path, provenance)
    return record


def _strict_art_direction_sources(store: Store, issues: list[str]) -> None:
    specs = (
        ("art-direction-manifests", "art-direction-manifest"),
        ("art-direction-profiles", "project-art-direction"),
        ("family-profiles", "family"),
        ("material-languages", "material-language"),
        ("art-direction-briefs", None),
    )
    for group, expected_type in specs:
        for path in sorted((store.pipeline / group).glob("*.json")):
            record = load_json(path)
            source_id = record.get("sourceId")
            artifact = record.get("artifact", {})
            if path.stem != source_id or record.get("schemaVersion") != ART_DIRECTION_SCHEMA_VERSION:
                issues.append(f"art_direction_registry_shape:{group}:{path.stem}")
                continue
            target = store.absolute(artifact.get("path", ""))
            if not target.is_file() or not bound_input_hash_matches(target, artifact.get("sha256")):
                issues.append(f"art_direction_registry_hash:{group}:{source_id}")
                continue
            source = load_json(target)
            source_type = record.get("sourceType")
            if expected_type is not None and source_type != expected_type:
                issues.append(f"art_direction_registry_type:{group}:{source_id}")
            if group == "art-direction-profiles" and source.get("profileId") != source_id:
                issues.append(f"art_direction_registry_source_id:{group}:{source_id}")
            elif group in {"family-profiles", "material-languages"} and source.get("profileId") != source_id:
                issues.append(f"art_direction_registry_source_id:{group}:{source_id}")
            elif group == "art-direction-manifests" and source.get("manifestId") != source_id:
                issues.append(f"art_direction_registry_source_id:{group}:{source_id}")
            elif group == "art-direction-briefs" and source.get("briefId") != source_id:
                issues.append(f"art_direction_registry_source_id:{group}:{source_id}")
            payload = {"sourceId": source_id, "sourceType": source_type, "artifact": artifact}
            prefix = {
                "art-direction-manifest": "art-direction-manifest",
                "project-art-direction": "art-direction-profile",
                "family": "family-profile",
                "material-language": "material-language",
                "asset": "asset-brief",
                "scene": "scene-brief",
            }.get(source_type)
            if not prefix or stable_id(prefix, payload) != record.get("registryId"):
                issues.append(f"art_direction_registry_identity:{group}:{source_id}")
    active_path = store.pipeline / "active-art-direction-manifest.json"
    if active_path.is_file():
        active = load_json(active_path)
        manifest_id = active.get("activeArtDirectionManifestId")
        manifest_path = store.record("art-direction-manifests", manifest_id or "")
        if not manifest_path.is_file():
            issues.append(f"active_art_direction_manifest_missing:{manifest_id}")
        else:
            manifest = load_json(manifest_path)
            if manifest.get("registryId") != active.get("registryId") or manifest.get("artifact") != active.get("artifact"):
                issues.append(f"active_art_direction_manifest_binding:{manifest_id}")


def _strict_art_direction_evidence(store: Store, issues: list[str]) -> None:
    for path in sorted((store.pipeline / "anchor-verdicts").glob("*.json")):
        record = load_json(path)
        payload = {key: record[key] for key in (
            "candidate", "family", "responsibilities", "excludedUses", "review",
            "decision", "reviewer", "reason", "decidedAt")}
        expected = stable_id("anchor-verdict", payload)
        if path.stem != record.get("anchorVerdictId") or expected != record.get("anchorVerdictId"):
            issues.append(f"anchor_verdict_identity:{path.stem}")
        if record.get("reviewer") != "cty41" or record.get("decision") not in {
                "approved-anchor", "rejected-as-anchor", "pending-more-evidence"}:
            issues.append(f"anchor_verdict_authority:{path.stem}")
        for artifact in (record.get("candidate", {}), record.get("review", {})):
            target = store.absolute(artifact.get("path", ""))
            if not target.is_file() or sha256_file(target) != artifact.get("sha256"):
                issues.append(f"anchor_verdict_hash:{path.stem}")
    for path in sorted((store.pipeline / "acceptance-case-results").glob("*.json")):
        record = load_json(path)
        payload = {key: record[key] for key in (
            "attemptId", "caseId", "candidate", "contractId", "contractSha256", "evidence",
            "automatedFacts", "automatedResult", "humanChecks", "humanDecision", "reviewer",
            "reason", "decidedAt")}
        expected = stable_id("acceptance-case-result", payload)
        if path.stem != record.get("acceptanceCaseResultId") or expected != record.get("acceptanceCaseResultId"):
            issues.append(f"acceptance_case_identity:{path.stem}")
        attempt_path = store.record("attempts", record.get("attemptId", ""))
        if not attempt_path.is_file():
            issues.append(f"acceptance_case_attempt:{path.stem}")
            continue
        attempt = load_json(attempt_path)
        if record.get("acceptanceCaseResultId") not in attempt.get("acceptanceCaseResultIds", []):
            issues.append(f"acceptance_case_backlink:{path.stem}")
        if record.get("candidate") != candidate_artifact(attempt):
            issues.append(f"acceptance_case_candidate:{path.stem}")
        decision = record.get("humanDecision")
        if (decision == "pending" and record.get("reviewer") != "agent") or (
                decision in {"passed", "failed"} and record.get("reviewer") != "cty41") or decision not in {
                "pending", "passed", "failed"}:
            issues.append(f"acceptance_case_authority:{path.stem}")
        for artifact in record.get("evidence", {}).values():
            target = store.absolute(artifact.get("path", ""))
            if not target.is_file() or sha256_file(target) != artifact.get("sha256"):
                issues.append(f"acceptance_case_evidence_hash:{path.stem}")
    for path in sorted((store.pipeline / "art-direction-reviews").glob("*.json")):
        record = load_json(path)
        payload = {key: record[key] for key in (
            "attemptId", "candidate", "contractId", "contractSha256", "sourcePanels",
            "acceptanceCaseResultIds", "overview")}
        expected = stable_id("art-direction-review", payload)
        if path.stem != record.get("artDirectionReviewId") or expected != record.get("artDirectionReviewId"):
            issues.append(f"art_direction_review_identity:{path.stem}")
        attempt_path = store.record("attempts", record.get("attemptId", ""))
        if not attempt_path.is_file():
            issues.append(f"art_direction_review_attempt:{path.stem}")
            continue
        attempt = load_json(attempt_path)
        current_review_id = attempt.get("artDirectionReviewId")
        historical_review_ids = attempt.get("artDirectionReviewIds", [])
        if (historical_review_ids and current_review_id != record.get("artDirectionReviewId")
                and record.get("artDirectionReviewId") not in historical_review_ids):
            issues.append(f"art_direction_review_backlink:{path.stem}")
        for artifact in [*record.get("sourcePanels", {}).values(), record.get("overview", {})]:
            target = store.absolute(artifact.get("path", ""))
            if not target.is_file() or sha256_file(target) != artifact.get("sha256"):
                issues.append(f"art_direction_review_hash:{path.stem}")
    for path in sorted((store.pipeline / "art-direction-verdicts").glob("*.json")):
        record = load_json(path)
        payload = {key: record[key] for key in (
            "attemptId", "candidate", "artDirectionReviewId", "reviewSha256", "decision",
            "acceptedWarnings", "reviewer", "reason", "decidedAt")}
        expected = stable_id("art-direction-verdict", payload)
        if path.stem != record.get("artDirectionVerdictId") or expected != record.get("artDirectionVerdictId"):
            issues.append(f"art_direction_verdict_identity:{path.stem}")
        if record.get("reviewer") != "cty41" or record.get("decision") not in {"approved", "retry"}:
            issues.append(f"art_direction_verdict_authority:{path.stem}")
        review_path = store.record("art-direction-reviews", record.get("artDirectionReviewId", ""))
        if not review_path.is_file() or sha256_file(review_path) != record.get("reviewSha256"):
            issues.append(f"art_direction_verdict_review:{path.stem}")
        attempt_path = store.record("attempts", record.get("attemptId", ""))
        if attempt_path.is_file():
            attempt = load_json(attempt_path)
            if record.get("artDirectionVerdictId") not in attempt.get("artDirectionVerdictIds", []):
                issues.append(f"art_direction_verdict_backlink:{path.stem}")


def _strict_reviewer_records(store: Store, issues: list[str]) -> None:
    """Verify immutable Reviewer chains only when the new policy records exist."""
    def bound(artifact: dict[str, Any]) -> bool:
        try:
            target = store.absolute(artifact.get("path", ""))
            return target.is_file() and sha256_file(target) == artifact.get("sha256")
        except (PipelineError, OSError):
            return False

    for group, field, validator, prefix in (("review-rules", "rule", artwork_review.validate_review_rule, "review-rule"),
                                            ("review-cases", "case", artwork_review.validate_review_case, "review-case")):
        for path in sorted((store.pipeline / group).glob("*.json")):
            record = load_json(path)
            try:
                value = validator(record.get(field, {}))
                payload = {field: value, "source": record.get("source")}
                if group == "review-cases":
                    payload["fitness"] = record.get("fitness")
                if not bound(record.get("source", {})) or stable_id(prefix, payload) != path.stem:
                    raise artwork_review.ReviewValidationError("binding")
            except artwork_review.ReviewValidationError:
                issue = "review_case_negative_role" if group == "review-cases" else "review_rule_invalid"
                issues.append(f"{issue}:{path.stem}")
    superseded_qualifications = {}
    for path in sorted((store.pipeline / "reviewer-qualification-supersessions").glob("*.json")):
        record = load_json(path)
        payload = {key: record.get(key) for key in ("oldQualificationId", "oldQualification", "newQualificationId", "newQualification", "reviewer", "reason", "supersededAt")}
        old_id, new_id = record.get("oldQualificationId"), record.get("newQualificationId")
        old_path = store.record("reviewer-qualifications", old_id or ""); new_path = store.record("reviewer-qualifications", new_id or "")
        identity = ("ruleId", "ruleVersion", "model", "effort", "reviewerPromptId", "reviewerPromptSha256",
                    "compiledPolicyId", "compiledPolicySha256", "caseSetVersion")
        old = load_json(old_path) if old_path.is_file() else {}; new = load_json(new_path) if new_path.is_file() else {}
        if (record.get("reviewerQualificationSupersessionId") != path.stem
                or stable_id("reviewer-qualification-supersession", payload) != path.stem
                or record.get("reviewer") != "cty41" or not bound(record.get("oldQualification", {}))
                or not bound(record.get("newQualification", {})) or old_id in superseded_qualifications
                or any(old.get(field) != new.get(field) for field in identity)
                or not _reviewer_qualification_is_valid(store, old, allow_legacy=True)
                or not _reviewer_qualification_is_valid(store, new)
                or datetime.fromisoformat(new.get("qualifiedAt")) <= datetime.fromisoformat(old.get("qualifiedAt"))):
            issues.append(f"reviewer_qualification_supersession_invalid:{path.stem}")
        else:
            superseded_qualifications[old_id] = new_id
    for path in sorted((store.pipeline / "reviewer-qualifications").glob("*.json")):
        record = load_json(path)
        fields = ["state", "reviewer", "ruleId", "ruleVersion", "model", "effort", "reviewerPromptId", "reviewerPromptSha256", "compiledPolicyId", "compiledPolicySha256", "caseSetVersion", "reviewRule", "compiledPolicy", "reviewerPrompt"]
        fields.extend(key for key in ("promptOnlyComparison", "qualificationAudits") if key in record)
        fields.append("qualifiedAt")
        payload = {key: record.get(key) for key in fields}
        rule_path = store.absolute(record.get("reviewRule", {}).get("path", "")); policy_path = store.absolute(record.get("compiledPolicy", {}).get("path", ""))
        modern_proof = bool(record.get("promptOnlyComparison")) and bool(record.get("qualificationAudits"))
        proof_bound = (not modern_proof and path.stem in superseded_qualifications) or (
            modern_proof and bound(record["promptOnlyComparison"])
            and all(bound(item) for item in record.get("qualificationAudits", [])))
        if (record.get("reviewerQualificationId") != path.stem or stable_id("reviewer-qualification", payload) != path.stem
                or record.get("reviewer") != "cty41" or not bound(record.get("reviewRule", {})) or not bound(record.get("compiledPolicy", {}))
                or not bound(record.get("reviewerPrompt", {})) or not proof_bound
                or not rule_path.is_file() or not policy_path.is_file()):
            issues.append(f"reviewer_qualification_invalid:{path.stem}")
    for path in sorted((store.pipeline / "reviewer-rule-suspensions").glob("*.json")):
        record = load_json(path); payload = {key: record.get(key) for key in ("reviewerQualificationId", "ruleId", "ruleVersion", "reviewer", "reason", "suspendedAt")}
        if (record.get("reviewerRuleSuspensionId") != path.stem or stable_id("reviewer-rule-suspension", payload) != path.stem
                or record.get("reviewer") != "cty41" or not store.record("reviewer-qualifications", record.get("reviewerQualificationId", "")).is_file()):
            issues.append(f"reviewer_rule_suspension_invalid:{path.stem}")
    packets: dict[str, dict[str, Any]] = {}
    for path in sorted((store.pipeline / "model-review-packets").glob("*.json")):
        packet = load_json(path); packets[path.stem] = packet
        try:
            artwork_review.validate_model_review_packet(packet)
        except artwork_review.ReviewValidationError:
            issues.append(f"model_review_packet_invalid:{path.stem}")
            continue
        attempt_path = store.record("attempts", packet.get("attemptId", ""))
        if not attempt_path.is_file():
            issues.append(f"model_review_packet_backlink:{path.stem}")
        elif not packet.get("qualificationMode") and load_json(attempt_path).get("modelReviewPacketId") != packet.get("packetId"):
            issues.append(f"model_review_packet_backlink:{path.stem}")
        for artifact in [*packet.get("artifacts", []), *packet.get("evidence", [])]:
            if not bound(artifact):
                issues.append(f"model_review_packet_hash:{path.stem}")
                break
    invocations: dict[str, dict[str, Any]] = {}
    for path in sorted((store.pipeline / "model-review-invocations").glob("*.json")):
        record = load_json(path); invocations[path.stem] = record
        payload = {key: record.get(key) for key in ("attemptId", "packetId", "packetSha256", "provider", "model", "effort", "freshSession", "freshSessionId", "promptSource", "startedAt")}
        if "qualificationMode" in record: payload["qualificationMode"] = record["qualificationMode"]
        if record.get("state") != "started" or record.get("modelReviewInvocationId") != path.stem or stable_id("model-review-invocation", payload) != path.stem:
            issues.append(f"model_review_invocation_identity:{path.stem}")
        packet = packets.get(record.get("packetId"))
        attempt_path = store.record("attempts", record.get("attemptId", ""))
        if (not packet or packet.get("sha256") != record.get("packetSha256") or packet.get("attemptId") != record.get("attemptId")
                or bool(record.get("qualificationMode")) != bool(packet.get("qualificationMode"))
                or not attempt_path.is_file() or (not record.get("qualificationMode") and load_json(attempt_path).get("modelReviewInvocationId") != path.stem)):
            issues.append(f"model_review_invocation_backlink:{path.stem}")
        if not bound(record.get("promptSource", {})):
            issues.append(f"model_review_invocation_prompt_hash:{path.stem}")
    results: dict[str, dict[str, Any]] = {}
    for path in sorted((store.pipeline / "model-review-results").glob("*.json")):
        record = load_json(path); results[path.stem] = record
        payload = {key: record.get(key) for key in ("invocationId", "attemptId", "packetId", "packetSha256", "provider", "model", "effort", "freshSession", "freshSessionId", "promptSource", "rawResult", "result", "outcome", "validationError")}
        if "qualificationMode" in record: payload["qualificationMode"] = record["qualificationMode"]
        if record.get("modelReviewResultRecordId") != path.stem or stable_id("model-review-result-record", payload) != path.stem:
            issues.append(f"model_review_result_identity:{path.stem}")
        invocation = invocations.get(record.get("invocationId")); packet = packets.get(record.get("packetId"))
        attempt_path = store.record("attempts", record.get("attemptId", ""))
        if (not invocation or not packet or invocation.get("packetId") != record.get("packetId")
                or invocation.get("packetSha256") != record.get("packetSha256")
                or bool(record.get("qualificationMode")) != bool(invocation.get("qualificationMode"))
                or not attempt_path.is_file() or (not record.get("qualificationMode") and load_json(attempt_path).get("modelReviewResultRecordId") != path.stem)):
            issues.append(f"model_review_result_backlink:{path.stem}")
        if record.get("rawResult") and not bound(record["rawResult"]):
            issues.append(f"model_review_result_raw_hash:{path.stem}")
        if record.get("result"):
            try:
                policy_id = packet.get("compiledPolicy", {}).get("compiledPolicyId") if packet else None
                matching = [load_json(candidate).get("compiled") for candidate in (store.pipeline / "compiled-review-policies").glob("*.json")]
                policy = next((item for item in matching if isinstance(item, dict) and item.get("compiledPolicyId") == policy_id), None)
                artwork_review.validate_model_review_result(record["result"], packet, policy or {})
            except (artwork_review.ReviewValidationError, TypeError):
                issues.append(f"model_review_result_authority:{path.stem}")
    for path in sorted((store.pipeline / "model-review-applications").glob("*.json")):
        record = load_json(path); result = results.get(record.get("modelReviewResultRecordId"))
        payload = {key: record.get(key) for key in ("modelReviewResultRecordId", "attemptId", "decision", "invocation")}
        if "childAttemptId" in record:
            payload["childAttemptId"] = record["childAttemptId"]
        if record.get("modelReviewApplicationId") != path.stem or stable_id("model-review-application", payload) != path.stem:
            issues.append(f"model_review_application_identity:{path.stem}")
        if not result or result.get("attemptId") != record.get("attemptId"):
            issues.append(f"model_review_application_backlink:{path.stem}")
        if record.get("decision", {}).get("action") == "automatic_retry":
            child_path = store.record("attempts", record.get("childAttemptId", ""))
            parent_path = store.record("attempts", record.get("attemptId", ""))
            if not child_path.is_file() or not parent_path.is_file():
                issues.append(f"model_review_application_child:{path.stem}")
            else:
                parent, child = load_json(parent_path), load_json(child_path)
                if (child.get("generationRound", 99) > 3 or child.get("ordinal", 99) > 3
                        or parent.get("generationRound", parent.get("ordinal", 99)) >= 3):
                    issues.append(f"model_review_a003_automatic_a004:{path.stem}")
                invocation = invocations.get(result.get("invocationId"), {}) if result else {}
                packet = packets.get(result.get("packetId"), {}) if result else {}
                policy_id = packet.get("compiledPolicy", {}).get("compiledPolicyId")
                policy = next((load_json(item).get("compiled") for item in (store.pipeline / "compiled-review-policies").glob("*.json")
                               if load_json(item).get("compiled", {}).get("compiledPolicyId") == policy_id), {})
                qualifications = _effective_qualifications(store)
                for defect in result.get("result", {}).get("defects", []) if result else []:
                    rule = next((item for item in policy.get("rules", []) if item.get("ruleId") == defect.get("ruleId")), {})
                    if not any(qualification.get("state") == "auto-retry-qualified" and qualification.get("reviewer") == "cty41"
                               and qualification.get("ruleId") == rule.get("ruleId") and qualification.get("ruleVersion") == rule.get("version")
                               and qualification.get("model") == invocation.get("model") and qualification.get("effort") == packet.get("reasoningEffort")
                               and qualification.get("reviewerPromptSha256") == invocation.get("promptSource", {}).get("sha256")
                               and qualification.get("compiledPolicyId") == policy.get("compiledPolicyId")
                               and qualification.get("compiledPolicySha256") == policy.get("sha256")
                               and not qualification.get("suspendedAt") and not qualification.get("supersededBy")
                               for qualification in qualifications):
                        issues.append(f"reviewer_qualification_invalid_use:{path.stem}:{defect.get('ruleId')}")
    for path in sorted((store.pipeline / "prompt-only-comparisons").glob("*.json")):
        record = load_json(path)
        try:
            expected = artwork_review.validate_prompt_only_comparison(record.get("promptOnly", {}), record.get("reviewerClosedLoop", {}))
            if record.get("promptComparisonId") != path.stem or expected["promptComparisonId"] != path.stem:
                raise artwork_review.ReviewValidationError("identity")
            sources = record.get("sources", {})
            if set(sources) != {"promptOnly", "reviewerClosedLoop"} or not all(bound(item) for item in sources.values()):
                raise artwork_review.ReviewValidationError("source hash")
            for key, source in sources.items():
                if load_json(store.absolute(source["path"], must_exist=True)) != record[{"promptOnly": "promptOnly", "reviewerClosedLoop": "reviewerClosedLoop"}[key]]:
                    raise artwork_review.ReviewValidationError("source payload")
            contract = expected["promptOnly"]["contract"]
            contract_path = store.record("contracts", contract["contractId"])
            if not contract_path.is_file() or sha256_file(contract_path) != contract["sha256"] or any(not bound(anchor) for anchor in expected["promptOnly"]["anchors"]):
                raise artwork_review.ReviewValidationError("bindings")
        except (artwork_review.ReviewValidationError, PipelineError, OSError, KeyError, TypeError, json.JSONDecodeError):
            issues.append(f"prompt_only_comparison_invalid:{path.stem}")


def strict_check(store: Store, strict: bool) -> dict[str, Any]:
    issues = []
    _strict_art_direction_sources(store, issues)
    _strict_reviewer_records(store, issues)
    _strict_art_direction_evidence(store, issues)
    invalidated_syncs = set()
    for invalidation_path in (store.pipeline / "attempt-provenance-sync-invalidations").glob("*.json"):
        invalidation = load_json(invalidation_path)
        payload = {key: invalidation.get(key) for key in ("attemptProvenanceSyncId", "attemptProvenanceSync", "reviewer", "reason", "invalidatedAt")}
        if (invalidation.get("attemptProvenanceSyncInvalidationId") != invalidation_path.stem
                or stable_id("attempt-provenance-sync-invalidation", payload) != invalidation_path.stem
                or invalidation.get("reviewer") != "cty41" or not _artifact_binding_matches(store, invalidation.get("attemptProvenanceSync"))):
            issues.append(f"attempt_provenance_sync_invalidation_invalid:{invalidation_path.stem}")
        else:
            invalidated_syncs.add(invalidation.get("attemptProvenanceSyncId"))
    for sync_path in (store.pipeline / "attempt-provenance-syncs").glob("*.json"):
        sync = load_json(sync_path)
        if sync.get("attemptProvenanceSyncId") in invalidated_syncs:
            continue
        payload = {key: sync.get(key) for key in ("attemptId", "jobId", "contractId", "artifacts", "rights", "reviewer", "reason", "syncedAt")}
        attempt_path = store.record("attempts", sync.get("attemptId", "")); job_path = store.record("jobs", sync.get("jobId", "")); contract_path = store.record("contracts", sync.get("contractId", ""))
        valid = (sync.get("attemptProvenanceSyncId") == sync_path.stem == stable_id("attempt-provenance-sync", payload)
                 and sync.get("reviewer") == "cty41" and attempt_path.is_file() and job_path.is_file() and contract_path.is_file()
                 and all(str(item.get("path", "")).startswith("Tools/artworks/pipeline/") and _artifact_binding_matches(store, item)
                         for item in sync.get("artifacts", [])))
        if valid:
            attempt, job, contract = load_json(attempt_path), load_json(job_path), load_json(contract_path)
            attempt_artifacts = {item.get("path"): item.get("sha256") for artifact in attempt.get("artifacts", {}).values()
                                 for item in (artifact.values() if isinstance(artifact, dict) and "path" not in artifact else [artifact])
                                 if isinstance(item, dict)}
            eligible = sorted(({"path": path_value, "sha256": sha_value} for path_value, sha_value in attempt_artifacts.items()
                               if str(path_value).startswith("Tools/artworks/pipeline/") and str(path_value).lower().endswith(".png")),
                              key=lambda item: item["path"])
            manifest = load_json(store.root / "Tools/public-release/asset-provenance.json")
            manifest_by_path = {item.get("path"): item for item in manifest.get("entries", [])}
            valid = (job.get("jobId") == sync.get("jobId") and job.get("contractId") == sync.get("contractId")
                     and contract.get("rights") == sync.get("rights") and bool(eligible)
                     and sync.get("artifacts") == eligible
                     and all(manifest_by_path.get(item["path"]) == {"path": item["path"], "sha256": item["sha256"],
                                                                  "status": "approved", **sync["rights"]}
                             for item in eligible))
        if not valid:
            issues.append(f"attempt_provenance_sync_invalid:{sync_path.stem}")
    inventory_path = store.pipeline / "legacy-assets.json"
    inventory = load_json(inventory_path) if inventory_path.exists() else {"assets": []}
    inventory_by_path = {item["path"]: item for item in inventory.get("assets", [])}
    for rel, item in inventory_by_path.items():
        path = store.absolute(rel)
        if not path.exists():
            issues.append(f"inventory_missing:{rel}")
        elif sha256_file(path) != item["sha256"]:
            issues.append(f"inventory_hash:{rel}")
    current = {store.relative(path) for path in (store.root / "Tools/artworks").rglob("*.png") if store.pipeline not in path.parents}
    registered_paths = set()
    for group in ("pose-guides", "supporting-artifacts"):
        for record_path in (store.pipeline / group).glob("*.json"):
            record = load_json(record_path)
            artifact = record.get("artifact", {})
            artifact_exists = isinstance(artifact, dict) and artifact.get("path") and store.absolute(artifact["path"]).is_file()
            valid = _artifact_binding_matches(store, artifact)
            legacy_missing = False
            if group == "supporting-artifacts":
                payload = {key: value for key, value in record.items() if key not in {"schemaVersion", "supportingArtifactId"}}
                valid = valid and record.get("supportingArtifactId") == record_path.stem == stable_id("supporting-artifact", payload)
                if record.get("schemaVersion") == 3:
                    valid = (valid and record.get("reviewer") == "cty41"
                             and record.get("rights") == {"rightsHolder": "cty41", "license": "project-owned",
                                                          "provenance": "cty41-direct-supporting-artifact-declaration"})
                elif record.get("schemaVersion") == 2 and not artifact_exists:
                    legacy_missing = True
                elif record.get("schemaVersion") != 2:
                    valid = False
            if valid and artifact.get("path"):
                registered_paths.add(artifact["path"])
            elif group == "supporting-artifacts" and not legacy_missing:
                issues.append(f"supporting_artifact_invalid:{record_path.stem}")

    def collect_bound_pngs(value: Any) -> None:
        if isinstance(value, dict):
            if isinstance(value.get("path"), str) and value["path"].lower().endswith(".png"):
                try:
                    target = store.absolute(value["path"])
                    if target.is_file() and sha256_file(target) == value.get("sha256"):
                        registered_paths.add(value["path"])
                except (PipelineError, OSError):
                    pass
            else:
                for child in value.values():
                    collect_bound_pngs(child)
        elif isinstance(value, list):
            for child in value:
                collect_bound_pngs(child)

    trusted_artifact_fields = {
        "component-migrations": ("source", "prepared", "mask"),
        "reviewed-recontracts": ("source", "candidate", "mask"),
        "model-review-packets": ("artifacts", "evidence"),
        "acceptance-case-results": ("evidence",),
        "art-direction-reviews": ("overview", "sourcePanels"),
        "size-comparisons": ("artifact", "inputs"),
    }
    trusted_identity = {
        "component-migrations": ("componentMigrationId", "component-migration"),
        "reviewed-recontracts": ("reviewedRecontractId", "reviewed-recontract"),
        "acceptance-case-results": ("acceptanceCaseResultId", "acceptance-case-result"),
        "art-direction-reviews": ("artDirectionReviewId", "art-direction-review"),
        "size-comparisons": ("sizeComparisonId", "size-comparison"),
    }
    for group, fields in trusted_artifact_fields.items():
        for record_path in (store.pipeline / group).glob("*.json"):
            record = load_json(record_path)
            valid = False
            try:
                if group == "jobs":
                    valid = (record.get("jobId") == record_path.stem
                             and store.record("contracts", record.get("contractId", "")).is_file()
                             and all(_artifact_binding_matches(store, item) for item in record.get("inputs", [])))
                elif group == "model-review-packets":
                    valid = artwork_review.validate_model_review_packet(record).get("packetId") == record_path.stem
                else:
                    identity_key, prefix = trusted_identity[group]
                    payload = {key: value for key, value in record.items() if key not in {"schemaVersion", identity_key}}
                    valid = record.get(identity_key) == record_path.stem == stable_id(prefix, payload)
                if valid and group == "component-migrations":
                    contract_path = store.record("contracts", record.get("contractId", ""))
                    processing = record.get("processing")
                    parameters = processing.get("parameters") if isinstance(processing, dict) and processing.get("path") else processing
                    valid = (record.get("reviewer") == "cty41" and record.get("invocationStatus") == "missing-pre-v3"
                             and contract_path.is_file() and sha256_file(contract_path) == record.get("contractSha256")
                             and all(_artifact_binding_matches(store, record.get(key)) for key in ("source", "prepared"))
                             and (record.get("mask") is None or _artifact_binding_matches(store, record.get("mask")))
                             and isinstance(parameters, dict) and bool(parameters.get("operation")))
                    if valid and isinstance(processing, dict) and processing.get("path"):
                        valid = (_artifact_binding_matches(store, processing) and load_json(store.absolute(processing["path"])) == parameters
                                 and str(parameters.get("operation", "")).startswith("deterministic-")
                                 and parameters.get("runtimeEligible") is False and parameters.get("selectedBy") == "cty41")
                if valid and group == "reviewed-recontracts":
                    source_attempt = load_json(store.record("attempts", record.get("sourceAttemptId", "")))
                    lineage = _validated_generation_lineage(store, source_attempt)
                    source_job = load_json(store.record("jobs", source_attempt.get("jobId", "")))
                    target_contract_path = store.record("contracts", record.get("targetContractId", ""))
                    feedback_path = store.record("feedback", source_attempt.get("feedbackId", ""))
                    feedback = load_json(feedback_path) if feedback_path.is_file() else {}
                    valid = (record.get("reviewer") == "cty41" and target_contract_path.is_file()
                             and sha256_file(target_contract_path) == record.get("targetContractSha256")
                             and record.get("sourceJobId") == source_job.get("jobId")
                             and record.get("sourceContractId") == source_job.get("contractId")
                             and feedback.get("authorType") == "human" and feedback.get("verdict") == "selected"
                             and feedback.get("reviewer") == "cty41"
                             and (record.get("sourceLineage") is None or record.get("sourceLineage") == lineage))
                    if record.get("processing"):
                        processing = record["processing"]
                        valid = (valid and _artifact_binding_matches(store, processing)
                                 and processing.get("parameters") == load_json(store.absolute(processing["path"])))
                        if valid:
                            _validate_recontract_processing(store, source_attempt, record["candidate"], processing["parameters"])
                    valid = valid and _artifact_binding_matches(store, record.get("candidate"))
                    derived_jobs = [load_json(item) for item in (store.pipeline / "jobs").glob("*.json")
                                    if load_json(item).get("reviewedRecontractId") == record.get("reviewedRecontractId")]
                    derived_attempts = [load_json(item) for item in (store.pipeline / "attempts").glob("*.json")
                                        if load_json(item).get("reviewedRecontractId") == record.get("reviewedRecontractId")]
                    valid = (valid and len(derived_jobs) == 1 and len(derived_attempts) == 1
                             and derived_jobs[0].get("contractId") == record.get("targetContractId")
                             and derived_attempts[0].get("jobId") == derived_jobs[0].get("jobId")
                             and derived_attempts[0].get("sourceAttemptId") == record.get("sourceAttemptId")
                             and derived_attempts[0].get("generationInvocationId") == record.get("generationInvocationId")
                             and derived_attempts[0].get("generationDeliveryId") == record.get("generationDeliveryId"))
            except (PipelineError, artwork_review.ReviewValidationError, OSError, KeyError, TypeError, json.JSONDecodeError):
                valid = False
            if valid:
                for field in fields:
                    collect_bound_pngs(record.get(field))
            else:
                issues.append(f"trusted_artifact_record_invalid:{group}:{record_path.stem}")
    for path in sorted((store.pipeline / "attempts").glob("*.json")):
        attempt = load_json(path)
        attempt_valid_for_registration = attempt.get("attemptId") == path.stem
        try:
            ordinal_from_id = int(path.stem.rsplit("-a", 1)[1])
            attempt_valid_for_registration = (attempt_valid_for_registration and attempt.get("ordinal") == ordinal_from_id
                                              and attempt.get("attemptId") == f"{attempt.get('jobId')}-a{ordinal_from_id:03d}")
        except (ValueError, IndexError):
            attempt_valid_for_registration = False
        if attempt.get("state") not in STATES:
            issues.append(f"attempt_state:{path.name}")
            attempt_valid_for_registration = False
        if bool(attempt.get("artifacts", {}).get("calibrated")) != bool(attempt.get("artifacts", {}).get("calibratedMask")):
            issues.append(f"attempt_calibration_pair:{attempt.get('attemptId')}")
        for artifact in attempt.get("artifacts", {}).values():
            values = artifact.values() if isinstance(artifact, dict) and "path" not in artifact else [artifact]
            for value in values:
                if isinstance(value, dict) and value.get("path"):
                    target = store.absolute(value["path"])
                    if not target.exists() or sha256_file(target) != value.get("sha256"):
                        issues.append(f"artifact_hash:{attempt['attemptId']}:{value.get('path')}")
                        attempt_valid_for_registration = False
        if not store.record("jobs", attempt.get("jobId", "")).is_file():
            issues.append(f"attempt_job_missing:{attempt.get('attemptId')}")
            attempt_valid_for_registration = False
        else:
            job_record = load_json(store.record("jobs", attempt["jobId"]))
            contract_record = load_json(store.record("contracts", job_record["contractId"]))
            if (contract_record.get("equipmentProductionSpec") or contract_record.get("styleSpec")) and attempt.get("state") in {"approved", "promoted"}:
                artifacts = attempt.get("artifacts", {})
                outputs = [candidate_artifact(attempt), artifacts.get("equipmentPreview"),
                           artifacts.get("review", {}).get("preview128"),
                           *artifacts.get("promoted", {}).values()]
                for output in outputs:
                    if not output or not store.absolute(output["path"]).is_file():
                        continue  # Missing files remain errors in the binding checks above.
                    _, pixel_issues = inspect_technical(store.absolute(output["path"]), contract_record["kind"],
                                                        require_master_canvas=False)
                    for issue in pixel_issues:
                        if issue in {"exact_chroma_residue", "transparent_rgb_nonzero"}:
                            issues.append(f"equipment_{issue}:{attempt['attemptId']}:{output['path']}")
            if contract_record.get("assetRole") == "component" and attempt.get("state") == "promoted":
                issues.append(f"component_promoted:{attempt.get('attemptId')}")
        if attempt.get("state") in {"approved", "rejected", "promoted"} and not attempt.get("approvalId"):
            issues.append(f"attempt_approval_missing:{attempt.get('attemptId')}")
        approval_id = attempt.get("approvalId")
        if approval_id:
            approval_path = store.record("approvals", approval_id)
            if not approval_path.is_file():
                issues.append(f"attempt_approval_record_missing:{attempt.get('attemptId')}")
            else:
                receipt = load_json(approval_path)
                expected_decision = "rejected" if attempt.get("state") == "rejected" else "approved"
                if receipt.get("decision") != expected_decision:
                    issues.append(f"attempt_approval_decision:{attempt.get('attemptId')}")
                approval_mode = receipt.get("approvalMode", "standard")
                if approval_mode not in {"standard", "gate-exception"}:
                    issues.append(f"attempt_approval_mode:{attempt.get('attemptId')}")
                if approval_mode == "gate-exception":
                    try:
                        validate_exception_receipt(store, attempt, receipt)
                    except PipelineError:
                        issues.append(f"attempt_gate_exception_invalid:{attempt.get('attemptId')}")
                elif attempt.get("report"):
                    report_path = store.absolute(attempt["report"]["path"], must_exist=True)
                    if not load_json(report_path).get("passed"):
                        issues.append(f"attempt_standard_approval_failed_report:{attempt.get('attemptId')}")
                prepared = candidate_artifact(attempt)
                mask = candidate_mask_artifact(attempt)
                if receipt.get("candidateSha256") != prepared.get("sha256") or receipt.get("maskSha256") != mask.get("sha256"):
                    issues.append(f"attempt_approval_hash:{attempt.get('attemptId')}")
                job = load_json(store.record("jobs", attempt["jobId"]))
                contract = load_json(store.record("contracts", job["contractId"]))
                required_reviews = required_review_keys(attempt, contract)
                if set(attempt.get("artifacts", {}).get("review", {})) != required_reviews:
                    issues.append(f"attempt_review_set:{attempt.get('attemptId')}")
                receipt_reviews = receipt.get("reviewSha256")
                current_reviews = {key: value.get("sha256") for key, value in sorted(attempt.get("artifacts", {}).get("review", {}).items())}
                if receipt_reviews is not None and receipt_reviews != current_reviews:
                    issues.append(f"attempt_review_receipt_hash:{attempt.get('attemptId')}")
        if attempt_valid_for_registration:
            for artifact in attempt.get("artifacts", {}).values():
                values = artifact.values() if isinstance(artifact, dict) and "path" not in artifact else [artifact]
                registered_paths.update(value["path"] for value in values
                                        if isinstance(value, dict) and value.get("path"))
    missing = sorted(current - set(inventory_by_path) - registered_paths)
    issues.extend(f"asset_unregistered:{path}" for path in missing)
    superseded_jobs = set()
    for path in sorted((store.pipeline / "job-migrations").glob("*.json")):
        migration = load_json(path)
        old_path = store.record("jobs", migration.get("oldJobId", ""))
        new_path = store.record("jobs", migration.get("newJobId", ""))
        migration_id = migration.get("migrationId")
        migration_payload = {key: value for key, value in migration.items() if key not in {"schemaVersion", "migrationId"}}
        if stable_id("job-migration", migration_payload) != migration_id:
            issues.append(f"job_migration_identity:{migration_id}")
            continue
        if not old_path.is_file() or sha256_file(old_path) != migration.get("oldJobSha256"):
            issues.append(f"job_migration_old:{migration_id}")
            continue
        if not new_path.is_file() or sha256_file(new_path) != migration.get("newJobSha256"):
            issues.append(f"job_migration_new:{migration_id}")
            continue
        if not migration.get("changes") or not migration.get("reason") or not migration.get("authorizedBy"):
            issues.append(f"job_migration_evidence:{migration_id}")
            continue
        historical_attempts_valid = all(
            store.record("attempts", bound.get("attemptId", "")).is_file() and
            sha256_file(store.record("attempts", bound.get("attemptId", ""))) == bound.get("sha256")
            for bound in migration.get("historicalAttempts", [])
        )
        if not historical_attempts_valid:
            issues.append(f"job_migration_attempt:{migration_id}")
            continue
        superseded_jobs.add(migration["oldJobId"])

    retired_historical_jobs = set()
    retirement_dir = store.root / "Tools/artworks/pure_run/art_direction/references"
    for retirement_path in retirement_dir.glob("retired_*.json"):
        retirement = load_json(retirement_path)
        retired_historical_jobs.update(
            Path(value).stem for value in retirement.get("preservedHistoricalPipelineRecords", [])
            if value.startswith("Tools/artworks/pipeline/jobs/")
        )

    for path in sorted((store.pipeline / "jobs").glob("*.json")):
        job = load_json(path)
        if job.get("jobId") in superseded_jobs:
            continue
        contract_path = store.record("contracts", job.get("contractId", ""))
        if not contract_path.is_file():
            issues.append(f"job_contract_missing:{job.get('jobId')}")
        elif sha256_file(contract_path) != job.get("contractSha256"):
            issues.append(f"job_contract_hash:{job.get('jobId')}")
        else:
            contract = load_json(contract_path)
            expected_requirements = None if job.get("sourceMode") == "reviewed_recontract" or not contract.get("occlusion") else {
                "occlusion": contract["occlusion"],
                "imageGenDirective": "Draw behind-core equipment and both hand paws first, then draw the capsule body over their inner portions; only outer arcs may remain visible.",
            }
            if job.get("contractRequirements") != expected_requirements:
                issues.append(f"job_contract_requirements:{job.get('jobId')}")
        if job.get("jobId") not in retired_historical_jobs:
            for bound in ([job["prompt"]] if isinstance(job.get("prompt"), dict) else []) + job.get("inputs", []):
                target = store.absolute(bound.get("path", ""))
                if not bound_input_hash_matches(target, bound.get("sha256")):
                    issues.append(f"job_input_hash:{job.get('jobId')}:{bound.get('path')}")
        try:
            _validate_job_local_references(store, job)
        except PipelineError:
            issues.append(f"job_local_reference:{job.get('jobId')}")
        if job.get("conceptOnly"):
            for attempt in list_attempts(store, job["jobId"]):
                if attempt.get("state") in {"approved", "promoted"}:
                    issues.append(f"concept_only_formal:{attempt.get('attemptId')}")
    for path in sorted((store.pipeline / "contracts").glob("*.json")):
        contract = load_json(path)
        equipment_spec = contract.get("equipmentProductionSpec")
        if equipment_spec:
            if equipment_spec.get("category") not in EQUIPMENT_CATEGORIES or not contract.get("requiresInvocation"):
                issues.append(f"contract_equipment_shape:{contract.get('contractId')}")
            profile_path = store.absolute(equipment_spec.get("profilePath", ""))
            if not profile_path.is_file() or sha256_file(profile_path) != equipment_spec.get("profileSha256"):
                issues.append(f"contract_equipment_profile_hash:{contract.get('contractId')}")
            for anchor_ref in equipment_spec.get("anchors", []):
                target = store.absolute(anchor_ref.get("path", ""))
                if not target.is_file() or sha256_file(target) != anchor_ref.get("sha256"):
                    issues.append(f"contract_equipment_anchor_hash:{contract.get('contractId')}")
        if contract.get("schemaVersion") == ART_DIRECTION_SCHEMA_VERSION:
            required_specs = ("artDirectionManifestSpec", "artDirectionSpec", "materialLanguageSpec",
                              "familyProfileSpec", "briefSpec")
            if any(not contract.get(key) for key in required_specs):
                issues.append(f"contract_art_direction_binding_missing:{contract.get('contractId')}")
            for key in required_specs:
                spec = contract.get(key, {})
                target = store.absolute(spec.get("path", ""))
                if not target.is_file() or not bound_input_hash_matches(target, spec.get("sha256")):
                    issues.append(f"contract_art_direction_hash:{contract.get('contractId')}:{key}")
            for verdict_id in contract.get("anchorVerdictIds", []):
                verdict_path = store.record("anchor-verdicts", verdict_id)
                if not verdict_path.is_file():
                    issues.append(f"contract_anchor_verdict_missing:{contract.get('contractId')}:{verdict_id}")
                elif load_json(verdict_path).get("decision") != "approved-anchor":
                    issues.append(f"contract_anchor_verdict_invalid:{contract.get('contractId')}:{verdict_id}")
        role = contract.get("assetRole")
        if contract.get("schemaVersion") in {3, ART_DIRECTION_SCHEMA_VERSION} and role:
            if role not in ASSET_ROLES:
                issues.append(f"contract_asset_role:{contract.get('contractId')}")
            if role == "component":
                if contract.get("componentKind") not in COMPONENT_KINDS or contract.get("runtimeEligible") is not False:
                    issues.append(f"contract_component_shape:{contract.get('contractId')}")
            elif contract.get("componentKind") is not None or contract.get("runtimeEligible") is not True:
                issues.append(f"contract_assembled_shape:{contract.get('contractId')}")
            if contract.get("sourceMode") not in SOURCE_MODES:
                issues.append(f"contract_source_mode:{contract.get('contractId')}")
        if (contract.get("schemaVersion") == 2 and contract.get("requiresInvocation")
                and not contract.get("equipmentProductionSpec")):
            composition_ref = contract.get("compositionSpec")
            if not composition_ref:
                issues.append(f"contract_composition_missing:{contract.get('contractId')}")
            else:
                target = store.record("compositions", composition_ref.get("compositionId", ""))
                if not target.is_file() or sha256_file(target) != composition_ref.get("sha256"):
                    issues.append(f"contract_composition_hash:{contract.get('contractId')}")
        occlusion = contract.get("occlusion")
        if occlusion:
            if set(occlusion.get("layerRules", {})) - OCCLUSION_LABELS:
                issues.append(f"contract_occlusion_label:{contract.get('contractId')}")
            if any(rule != "behind-core" for rule in occlusion.get("layerRules", {}).values()):
                issues.append(f"contract_occlusion_rule:{contract.get('contractId')}")
            if set(occlusion.get("visibilityCaps", {})) - set(occlusion.get("layerRules", {})):
                issues.append(f"contract_visibility_cap_label:{contract.get('contractId')}")
        anchor = contract.get("anchor")
        if anchor:
            target = store.absolute(anchor.get("path", ""))
            if not target.is_file() or sha256_file(target) != anchor.get("sha256"):
                issues.append(f"contract_anchor_hash:{contract.get('contractId')}")
            mask_path = anchor.get("maskPath")
            if mask_path:
                mask = store.absolute(mask_path)
                if not mask.is_file() or sha256_file(mask) != anchor.get("maskSha256"):
                    issues.append(f"contract_anchor_mask_hash:{contract.get('contractId')}")
    for path in sorted((store.pipeline / "feedback").glob("*.json")):
        feedback = load_json(path)
        if feedback.get("schemaVersion") == 2:
            if feedback.get("authorType") not in {"agent", "human"}:
                issues.append(f"feedback_author_type:{feedback.get('feedbackId')}")
            if set(feedback.get("categories", [])) - FEEDBACK_CATEGORIES:
                issues.append(f"feedback_category:{feedback.get('feedbackId')}")
            if feedback.get("disposition") not in FEEDBACK_VERDICTS:
                issues.append(f"feedback_disposition:{feedback.get('feedbackId')}")
        attempt_path = store.record("attempts", feedback.get("attemptId", ""))
        if not attempt_path.is_file():
            issues.append(f"feedback_attempt_missing:{feedback.get('feedbackId')}")
            continue
        attempt = load_json(attempt_path)
        if attempt.get("feedbackId") != feedback.get("feedbackId"):
            issues.append(f"feedback_backlink:{feedback.get('feedbackId')}")
        if feedback.get("candidateSha256") != candidate_artifact(attempt).get("sha256"):
            remediated = any(
                child.get("technicalRemediation") and child.get("parentAttemptId") == attempt.get("attemptId")
                and child.get("artifacts", {}).get("raw", {}).get("sha256") == attempt.get("artifacts", {}).get("raw", {}).get("sha256")
                for child in (load_json(item) for item in (store.pipeline / "attempts").glob("*.json"))
            )
            if not remediated:
                issues.append(f"feedback_candidate_hash:{feedback.get('feedbackId')}")
    for path in sorted((store.pipeline / "local-references").glob("*.json")):
        reference = load_json(path)
        if (reference.get("role") not in LOCAL_REFERENCE_ROLES or reference.get("localOnly") is not True
                or reference.get("publish") is not False or "path" in reference):
            issues.append(f"local_reference_shape:{reference.get('localReferenceId')}")
    for path in sorted((store.pipeline / "equipment-style-verdicts").glob("*.json")):
        verdict = load_json(path)
        attempt_path = store.record("attempts", verdict.get("attemptId", ""))
        if not attempt_path.is_file():
            issues.append(f"equipment_style_verdict_attempt:{verdict.get('equipmentStyleVerdictId')}")
            continue
        attempt = load_json(attempt_path)
        if verdict.get("equipmentStyleVerdictId") not in attempt.get(
                "equipmentStyleVerdictIds", [attempt.get("equipmentStyleVerdictId")]):
            issues.append(f"equipment_style_verdict_backlink:{verdict.get('equipmentStyleVerdictId')}")
    for path in sorted((store.pipeline / "feedback-addenda").glob("*.json")):
        addendum = load_json(path)
        feedback_path = store.record("feedback", addendum.get("parentFeedbackId", ""))
        attempt_path = store.record("attempts", addendum.get("attemptId", ""))
        if not feedback_path.is_file() or not attempt_path.is_file():
            issues.append(f"feedback_addendum_parent:{addendum.get('feedbackAddendumId')}")
            continue
        feedback = load_json(feedback_path)
        attempt = load_json(attempt_path)
        if feedback.get("attemptId") != attempt.get("attemptId"):
            issues.append(f"feedback_addendum_attempt:{addendum.get('feedbackAddendumId')}")
        if addendum.get("feedbackAddendumId") not in attempt.get("feedbackAddendumIds", []):
            issues.append(f"feedback_addendum_backlink:{addendum.get('feedbackAddendumId')}")
    for path in sorted((store.pipeline / "series").glob("*.json")):
        series = load_json(path)
        limit = series.get("maxUniqueOutputs")
        if limit is not None and (not isinstance(limit, int) or isinstance(limit, bool) or limit < 1):
            issues.append(f"series_limit:{series.get('seriesId')}")
        seen_attempts = set()
        for pose in series.get("poses", []):
            if pose.get("state") not in SERIES_STATES:
                issues.append(f"series_pose_state:{series.get('seriesId')}:{pose.get('poseId')}")
            if limit is not None and len(unique_pose_hashes(store, pose)) > limit:
                issues.append(f"series_pose_limit:{series.get('seriesId')}:{pose.get('poseId')}")
            for attempt_id in pose.get("attemptIds", []):
                if attempt_id in seen_attempts:
                    issues.append(f"series_attempt_duplicate:{series.get('seriesId')}:{attempt_id}")
                seen_attempts.add(attempt_id)
                attempt = load_json(store.record("attempts", attempt_id))
                if (attempt.get("artifacts", {}).get("raw") and not attempt.get("feedbackId")
                        and not attempt.get("technicalRemediation")):
                    issues.append(f"series_feedback_missing:{series.get('seriesId')}:{attempt_id}")
            selected = pose.get("selectedAttemptId")
            if selected and selected not in pose.get("attemptIds", []):
                issues.append(f"series_selection_foreign:{series.get('seriesId')}:{pose.get('poseId')}")
            if pose.get("state") == "provisional" and pose.get("poseId") != "idle-dr":
                issues.append(f"series_provisional_pose:{series.get('seriesId')}:{pose.get('poseId')}")
        if series.get("provisionalAnchorAttemptId"):
            for pose in series.get("poses", [])[1:]:
                if pose.get("state") in {"approved", "promoted"}:
                    issues.append(f"series_provisional_downstream_formal:{series.get('seriesId')}:{pose.get('poseId')}")
        for change_id in series.get("limitChangeIds", []):
            change_path = store.record("series-limit-changes", change_id)
            if not change_path.is_file():
                issues.append(f"series_limit_change_missing:{series.get('seriesId')}:{change_id}")
                continue
            change = load_json(change_path)
            if change.get("seriesId") != series.get("seriesId"):
                issues.append(f"series_limit_change_series:{series.get('seriesId')}:{change_id}")
        if series.get("limitChangeIds"):
            latest = load_json(store.record("series-limit-changes", series["limitChangeIds"][-1]))
            if latest.get("maxUniqueOutputs") != limit:
                issues.append(f"series_limit_change_backlink:{series.get('seriesId')}")
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    if manifest_path.is_file():
        manifest = load_json(manifest_path)
        manifest_by_path = {item["path"]: item for item in manifest.get("entries", [])}
        for png in sorted(store.pipeline.rglob("*.png")):
            rel = store.relative(png)
            entry = manifest_by_path.get(rel)
            if not entry:
                issues.append(f"pipeline_png_provenance_missing:{rel}")
            elif entry.get("sha256") != sha256_file(png):
                issues.append(f"pipeline_png_provenance_hash:{rel}")
    for path in sorted((store.pipeline / "generation-deliveries").glob("*.json")):
        delivery = load_json(path)
        invocation = store.record("generation-invocations", delivery.get("invocationId", ""))
        attempt = store.record("attempts", delivery.get("attemptId", ""))
        if not invocation.is_file() or not attempt.is_file():
            issues.append(f"generation_delivery_parent:{delivery.get('generationDeliveryId')}")
            continue
        attempt_record = load_json(attempt)
        if attempt_record.get("artifacts", {}).get("raw", {}).get("sha256") != delivery.get("rawSha256"):
            issues.append(f"generation_delivery_hash:{delivery.get('generationDeliveryId')}")
    transaction_resolutions: dict[str, dict[str, Any]] = {}
    for resolution_path in sorted((store.pipeline / "transaction-resolutions").glob("*.json")):
        resolution = load_json(resolution_path)
        payload = {key: resolution.get(key) for key in ("transactionId", "transaction", "operation", "payload", "outcome", "reviewer", "reason", "decidedAt", "evidence")}
        valid = (resolution.get("transactionResolutionId") == resolution_path.stem == stable_id("transaction-resolution", payload)
                 and resolution.get("reviewer") == "cty41" and resolution.get("outcome") in {"committed", "aborted"}
                 and _artifact_binding_matches(store, resolution.get("transaction")))
        if valid:
            transaction = load_json(store.absolute(resolution["transaction"]["path"]))
            valid = (resolution.get("operation") == transaction.get("operation")
                     and resolution.get("payload") == transaction.get("payload"))
            if valid and resolution.get("outcome") == "committed":
                attempt_path = store.record("attempts", resolution.get("payload", {}).get("attemptId", ""))
                valid = attempt_path.is_file() and _artifact_binding_matches(store, resolution.get("evidence"))
                if valid:
                    attempt = load_json(attempt_path); prepared = attempt.get("artifacts", {}).get("prepared")
                    preparation = attempt.get("preparation", {})
                    expected_chroma = str(resolution["payload"].get("chroma", "")).replace("#", "").lower()
                    if "," in expected_chroma:
                        try:
                            expected_chroma = "".join(f"{int(value.strip()):02x}" for value in expected_chroma.split(","))
                        except ValueError:
                            valid = False
                    valid = (valid and prepared == resolution.get("evidence")
                             and preparation.get("chroma") == expected_chroma
                             and preparation.get("chromaTolerance") == resolution["payload"].get("chromaTolerance"))
            elif valid:
                valid = resolution.get("evidence") is None
        if not valid or resolution.get("transactionId") in transaction_resolutions:
            issues.append(f"transaction_resolution_invalid:{resolution_path.stem}")
        else:
            transaction_resolutions[resolution["transactionId"]] = resolution
    for path in sorted((store.pipeline / "transactions").glob("*.json")):
        transaction = load_json(path)
        expected_id = stable_id("transaction", {"operation": transaction.get("operation"), **transaction.get("payload", {})})
        if transaction.get("transactionId") != path.stem or expected_id != path.stem:
            issues.append(f"transaction_identity:{path.stem}")
        effective_terminal = transaction.get("state") in {"committed", "aborted"} or transaction.get("transactionId") in transaction_resolutions
        if not effective_terminal:
            issues.append(f"transaction_incomplete:{transaction.get('transactionId')}")
        legacy_resolution = transaction.get("resolution")
        if legacy_resolution and (legacy_resolution.get("reviewer") != "cty41"
                                  or transaction.get("state") not in {"committed", "aborted"}
                                  or (legacy_resolution.get("evidence") and not _artifact_binding_matches(store, legacy_resolution["evidence"]))):
            issues.append(f"transaction_resolution_invalid:{path.stem}")
    for path in sorted((store.pipeline / "assemblies").glob("*.json")):
        assembly = load_json(path)
        if assembly.get("schemaVersion") != 3:
            issues.append(f"assembly_schema:{assembly.get('assemblyId')}")
            continue
        expected = stable_id("assembly", {key: assembly[key] for key in ("assetId", "contractId", "canvas", "layers")})
        if expected != assembly.get("assemblyId"):
            issues.append(f"assembly_identity:{assembly.get('assemblyId')}")
        for layer in assembly.get("layers", []):
            artifact = layer.get("artifact", {})
            target = store.absolute(artifact.get("path", ""))
            if not target.is_file() or sha256_file(target) != artifact.get("sha256"):
                issues.append(f"assembly_layer_hash:{assembly.get('assemblyId')}:{layer.get('role')}")
    for path in sorted((store.pipeline / "approvals").glob("*.json")):
        approval = load_json(path)
        attempt_path = store.record("attempts", approval.get("attemptId", ""))
        if not attempt_path.is_file():
            continue
        attempt = load_json(attempt_path)
        job = load_json(store.record("jobs", attempt["jobId"]))
        contract = load_json(store.record("contracts", job["contractId"]))
        if contract.get("schemaVersion") == 2:
            if approval.get("reviewer") != "cty41":
                issues.append(f"approval_not_human:{approval.get('approvalId')}")
            if contract.get("compositionSpec") and attempt.get("sourceMode") != "reviewed_import" and not approval.get("annotation"):
                issues.append(f"approval_annotations_missing:{approval.get('approvalId')}")
            elif contract.get("compositionSpec") and attempt.get("sourceMode") != "reviewed_import":
                annotation = approval["annotation"]
                annotation_path = store.record("annotations", annotation.get("annotationId", ""))
                if not annotation_path.is_file() or sha256_file(annotation_path) != annotation.get("sha256"):
                    issues.append(f"approval_annotations_hash:{approval.get('approvalId')}")
    result = {"schemaVersion": 1, "strict": strict, "inventoryCount": len(inventory_by_path), "issues": sorted(issues), "ok": not issues}
    if strict and issues:
        raise PipelineError("strict check failed:\n" + "\n".join(issues))
    return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="repository root")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("migrate-legacy")
    for command_name in (
            "register-art-direction-manifest", "register-art-direction-profile",
            "register-family-profile", "register-material-language",
            "create-asset-brief", "create-scene-brief"):
        source_command = commands.add_parser(command_name)
        source_command.add_argument("--source", required=True)
    anchor = commands.add_parser("approve-anchor")
    anchor.add_argument("--candidate", required=True); anchor.add_argument("--mask", required=True)
    anchor.add_argument("--review", required=True); anchor.add_argument("--reviewer", required=True)
    anchor.add_argument("--reason", required=True); anchor.add_argument("--decided-at", required=True)
    series = commands.add_parser("create-series")
    series.add_argument("--series-id", required=True); series.add_argument("--asset-id", required=True)
    series.add_argument("--pose", action="append", required=True)
    series.add_argument("--max-unique-outputs", type=int)
    limit = commands.add_parser("set-series-output-limit")
    limit.add_argument("--series-id", required=True)
    limit_group = limit.add_mutually_exclusive_group(required=True)
    limit_group.add_argument("--max-unique-outputs", type=int)
    limit_group.add_argument("--unlimited", action="store_true")
    limit.add_argument("--reviewer", required=True); limit.add_argument("--reason", required=True)
    limit.add_argument("--decided-at", required=True)
    create = commands.add_parser("create-contract")
    create.add_argument("--asset-id", required=True); create.add_argument("--approved-asset-id"); create.add_argument("--kind", required=True)
    create.add_argument("--direction", required=True); create.add_argument("--pose", required=True)
    create.add_argument("--anchor"); create.add_argument("--anchor-mask")
    create.add_argument("--mask-required", action=argparse.BooleanOptionalAction, default=True)
    create.add_argument("--no-arms", action="store_true"); create.add_argument("--size-tolerance", type=int, default=3)
    create.add_argument("--near-hand-side", choices=("left", "right")); create.add_argument("--far-hand-side", choices=("left", "right"))
    create.add_argument("--center-tolerance", type=int, default=2); create.add_argument("--output-master", required=True)
    create.add_argument("--master-width", type=int, default=256)
    create.add_argument("--master-height", type=int, default=256)
    create.add_argument("--footprint-width", type=int)
    create.add_argument("--footprint-height", type=int)
    create.add_argument("--display-scale", type=float)
    create.add_argument("--ground-anchor-x", type=float)
    create.add_argument("--ground-anchor-y", type=float)
    create.add_argument("--anchor-mode", choices=("contact_shape_center", "virtual_ground_point", "visual_bounds_center", "texture_center"))
    create.add_argument("--board-role", choices=("player", "target", "neutral"))
    create.add_argument("--screen-facing", choices=("up_right", "down_left", "non_directional"))
    create.add_argument("--layer-rule", action="append", default=[])
    create.add_argument("--visibility-cap", action="append", default=[])
    create.add_argument("--composition-id")
    create.add_argument("--identity-anchor-mask")
    create.add_argument("--forehead-blaze-min-iou", type=float, default=0.45)
    create.add_argument("--pose-reference", action="store_true")
    create.add_argument("--output-preview", required=True); create.add_argument("--rights-holder", default="cty41")
    create.add_argument("--license", default="CC-BY-4.0"); create.add_argument("--provenance", default="project-owned-gpt-generated")
    create.add_argument("--asset-role", choices=sorted(ASSET_ROLES))
    create.add_argument("--component-kind", choices=sorted(COMPONENT_KINDS))
    create.add_argument("--source-mode", choices=sorted(SOURCE_MODES))
    create.add_argument("--style-profile")
    create.add_argument("--family-profile-id")
    create.add_argument("--brief-id")
    create.add_argument("--anchor-verdict-id", action="append", default=[])
    create.add_argument("--target-visible-height", type=int)
    create.add_argument("--visible-height-min", type=int)
    create.add_argument("--visible-height-max", type=int)
    equipment_contract = commands.add_parser("create-equipment-contract")
    equipment_contract.add_argument("--asset-id", required=True); equipment_contract.add_argument("--approved-asset-id")
    equipment_contract.add_argument("--category", choices=sorted(EQUIPMENT_CATEGORIES), required=True)
    equipment_contract.add_argument("--production-profile", required=True)
    equipment_contract.add_argument("--target-visible-height", type=int, required=True)
    equipment_contract.add_argument("--visible-height-min", type=int, required=True)
    equipment_contract.add_argument("--visible-height-max", type=int, required=True)
    equipment_contract.add_argument("--master-width", type=int, default=256); equipment_contract.add_argument("--master-height", type=int, default=256)
    equipment_contract.add_argument("--size-tolerance", type=int, default=3); equipment_contract.add_argument("--center-tolerance", type=int, default=2)
    equipment_contract.add_argument("--output-master", required=True); equipment_contract.add_argument("--output-preview", required=True)
    equipment_contract.add_argument("--rights-holder", default="cty41"); equipment_contract.add_argument("--license", default="CC-BY-4.0")
    equipment_contract.add_argument("--provenance", default="project-owned-gpt-generated")
    equipment_contract.add_argument("--family-profile-id")
    equipment_contract.add_argument("--brief-id")
    equipment_contract.add_argument("--anchor-verdict-id", action="append", default=[])
    local_ref = commands.add_parser("register-local-reference")
    local_ref.add_argument("--path", required=True); local_ref.add_argument("--source-label", required=True)
    local_ref.add_argument("--role", choices=sorted(LOCAL_REFERENCE_ROLES), required=True)
    anchor_verdict = commands.add_parser("record-anchor-verdict")
    anchor_verdict.add_argument("--candidate", required=True); anchor_verdict.add_argument("--family", required=True)
    anchor_verdict.add_argument("--responsibility", action="append", required=True)
    anchor_verdict.add_argument("--excluded-use", action="append", default=[]); anchor_verdict.add_argument("--review", required=True)
    anchor_verdict.add_argument("--decision", choices=("approved-anchor", "rejected-as-anchor", "pending-more-evidence"), required=True)
    anchor_verdict.add_argument("--reviewer", required=True); anchor_verdict.add_argument("--reason", required=True)
    anchor_verdict.add_argument("--decided-at", required=True)
    acceptance = commands.add_parser("record-acceptance-case-result")
    acceptance.add_argument("--attempt-id", required=True); acceptance.add_argument("--case-id", required=True)
    acceptance.add_argument("--evidence", action="append", required=True)
    acceptance.add_argument("--automated-fact", action="append", default=[])
    acceptance.add_argument("--automated-result", choices=("passed", "warning", "failed", "not-applicable"), required=True)
    acceptance.add_argument("--human-check", action="append", default=[])
    acceptance.add_argument("--human-decision", choices=("pending", "passed", "failed"), required=True)
    acceptance.add_argument("--reviewer", required=True); acceptance.add_argument("--reason", required=True)
    acceptance.add_argument("--decided-at", required=True)
    art_review = commands.add_parser("render-art-direction-review")
    art_review.add_argument("--attempt-id", required=True); art_review.add_argument("--panel", action="append", required=True)
    art_review.add_argument("--output", required=True)
    art_verdict = commands.add_parser("record-art-direction-verdict")
    art_verdict.add_argument("--attempt-id", required=True); art_verdict.add_argument("--review-id", required=True)
    art_verdict.add_argument("--decision", choices=("approved", "retry"), required=True)
    art_verdict.add_argument("--accept-warning", action="append", default=[])
    art_verdict.add_argument("--reviewer", required=True); art_verdict.add_argument("--reason", required=True)
    art_verdict.add_argument("--decided-at", required=True)
    job = commands.add_parser("create-job"); job.add_argument("--contract-id", required=True)
    job.add_argument("--prompt", required=True); job.add_argument("--input", action="append", default=[])
    job.add_argument("--pose-guide-id")
    job.add_argument("--local-reference-id", action="append", default=[])
    job.add_argument("--series-id"); job.add_argument("--pose-id")
    migrate_job = commands.add_parser("migrate-ready-job-bindings")
    migrate_job.add_argument("--job-id", required=True); migrate_job.add_argument("--reason", required=True)
    migrate_job.add_argument("--authorized-by", required=True)
    retry_p = commands.add_parser("retry"); retry_p.add_argument("--job-id", required=True); retry_p.add_argument("--parent-attempt")
    retry_p.add_argument("--feedback-id")
    retry_p.add_argument("--technical-remediation", action="store_true")
    ingest_p = commands.add_parser("ingest"); ingest_p.add_argument("--attempt-id", required=True); ingest_p.add_argument("--source", required=True)
    ingest_p.add_argument("--invocation-id")
    prepare_p = commands.add_parser("prepare"); prepare_p.add_argument("--attempt-id", required=True); prepare_p.add_argument("--chroma")
    prepare_p.add_argument("--chroma-tolerance", type=int, default=0)
    mask_p = commands.add_parser("attach-mask"); mask_p.add_argument("--attempt-id", required=True); mask_p.add_argument("--mask", required=True)
    identity_mask_p = commands.add_parser("attach-identity-mask"); identity_mask_p.add_argument("--attempt-id", required=True); identity_mask_p.add_argument("--mask", required=True)
    calibrate_p = commands.add_parser("calibrate-core"); calibrate_p.add_argument("--attempt-id", required=True)
    validate_p = commands.add_parser("validate"); validate_p.add_argument("--attempt-id", required=True)
    review_p = commands.add_parser("render-review"); review_p.add_argument("--attempt-id", required=True)
    feedback = commands.add_parser("record-feedback"); feedback.add_argument("--attempt-id", required=True)
    feedback.add_argument("--reviewer", required=True); feedback.add_argument("--verdict", choices=sorted(FEEDBACK_VERDICTS), required=True)
    feedback.add_argument("--strength", action="append", default=[]); feedback.add_argument("--defect", action="append", default=[])
    feedback.add_argument("--next-prompt-delta"); feedback.add_argument("--recorded-at", required=True)
    feedback.add_argument("--author-type", choices=("agent", "human")); feedback.add_argument("--category", action="append", default=[])
    feedback.add_argument("--frozen", action="append", default=[]); feedback.add_argument("--pending", action="append", default=[])
    addendum = commands.add_parser("record-feedback-addendum"); addendum.add_argument("--feedback-id", required=True)
    addendum.add_argument("--reviewer", required=True); addendum.add_argument("--defect", action="append", default=[])
    addendum.add_argument("--author-type", choices=("agent", "human")); addendum.add_argument("--disposition", choices=sorted(FEEDBACK_VERDICTS))
    addendum.add_argument("--recorded-at", required=True)
    select = commands.add_parser("select-attempt"); select.add_argument("--attempt-id", required=True)
    select.add_argument("--provisional", action="store_true")
    advance = commands.add_parser("advance-series"); advance.add_argument("--series-id", required=True)
    for name in ("approve", "reject"):
        decision = commands.add_parser(name); decision.add_argument("--attempt-id", required=True)
        decision.add_argument("--reviewer", required=True); decision.add_argument("--reason", required=True)
        decision.add_argument("--decided-at", required=True, help="explicit ISO-8601 timestamp")
    exception = commands.add_parser("approve-exception"); exception.add_argument("--attempt-id", required=True)
    exception.add_argument("--issue", action="append", required=True)
    exception.add_argument("--reviewer", required=True); exception.add_argument("--reason", required=True)
    exception.add_argument("--decided-at", required=True, help="explicit ISO-8601 timestamp")
    promote_p = commands.add_parser("promote"); promote_p.add_argument("--attempt-id", required=True)
    refresh_p = commands.add_parser("refresh-promoted-preview"); refresh_p.add_argument("--attempt-id", required=True)
    composition = commands.add_parser("create-composition"); composition.add_argument("--asset-id", required=True)
    composition.add_argument("--spec", required=True); composition.add_argument("--anchor", required=True)
    guide = commands.add_parser("render-pose-guide"); guide.add_argument("--composition-id", required=True); guide.add_argument("--output", required=True)
    compiled = commands.add_parser("compile-prompt"); compiled.add_argument("--job-id", required=True)
    compiled.add_argument("--pose-guide-id", required=True); compiled.add_argument("--output", required=True)
    equipment_compiled = commands.add_parser("compile-equipment-prompt")
    equipment_compiled.add_argument("--job-id", required=True); equipment_compiled.add_argument("--output", required=True)
    begin = commands.add_parser("begin-generation"); begin.add_argument("--attempt-id", required=True)
    begin.add_argument("--compiled-prompt-id", required=True); begin.add_argument("--provider", default="openai-gpt-image")
    begin.add_argument("--started-at", required=True)
    failure = commands.add_parser("record-generation-failure"); failure.add_argument("--invocation-id", required=True)
    failure.add_argument("--reason", required=True); failure.add_argument("--failed-at", required=True)
    annotations = commands.add_parser("attach-annotations"); annotations.add_argument("--attempt-id", required=True)
    annotations.add_argument("--annotations", required=True)
    advisory = commands.add_parser("record-advisory-review"); advisory.add_argument("--attempt-id", required=True)
    advisory.add_argument("--reviewer", required=True); advisory.add_argument("--risk", action="append", required=True)
    advisory.add_argument("--recorded-at", required=True)
    commands.add_parser("index-review-history")
    case_audit = commands.add_parser("audit-review-case"); case_audit.add_argument("--source", required=True)
    review_policy = commands.add_parser("register-project-review-policy"); review_policy.add_argument("--source", required=True)
    review_rule = commands.add_parser("create-review-rule"); review_rule.add_argument("--source", required=True)
    review_case = commands.add_parser("create-review-case"); review_case.add_argument("--source", required=True)
    compiled_review = commands.add_parser("compile-review-policy")
    compiled_review.add_argument("--project-review-policy-id", required=True); compiled_review.add_argument("--context", required=True)
    compiled_review.add_argument("--acceptance-case-id", action="append", default=[])
    compiled_review.add_argument("--feedback-rule-id", action="append", default=[])
    model_packet = commands.add_parser("create-model-review-packet")
    model_packet.add_argument("--attempt-id", required=True); model_packet.add_argument("--compiled-review-policy-id", required=True)
    model_packet.add_argument("--required-model", required=True); model_packet.add_argument("--frozen-invariant", action="append", default=[])
    model_packet.add_argument("--artifact", action="append", default=[]); model_packet.add_argument("--evidence", action="append", default=[])
    shadow_packet = commands.add_parser("create-shadow-model-review-packet")
    shadow_packet.add_argument("--review-case-record-id", required=True); shadow_packet.add_argument("--human-feedback-id", required=True)
    shadow_packet.add_argument("--compiled-review-policy-id", required=True); shadow_packet.add_argument("--required-model", required=True)
    shadow_packet.add_argument("--requested-effort", choices=("medium", "high", "xhigh"), required=True)
    shadow_packet.add_argument("--review-context-contract-id", help="schema-v4 contract used only for historical case rule evaluation")
    shadow_packet.add_argument("--review-context-brief-source", help="hash-matching brief source used only for historical case rule evaluation")
    shadow_packet.add_argument("--frozen-invariant", action="append", default=[]); shadow_packet.add_argument("--evidence", action="append", default=[])
    model_begin = commands.add_parser("begin-model-review")
    model_begin.add_argument("--attempt-id", required=True); model_begin.add_argument("--packet-id", required=True)
    model_begin.add_argument("--provider", required=True); model_begin.add_argument("--model", required=True); model_begin.add_argument("--effort", required=True)
    model_begin.add_argument("--fresh-session-id", required=True); model_begin.add_argument("--prompt-source", required=True); model_begin.add_argument("--started-at", required=True)
    model_record = commands.add_parser("record-model-review")
    model_record.add_argument("--invocation-id", required=True); model_record.add_argument("--compiled-review-policy-id", required=True)
    model_result = model_record.add_mutually_exclusive_group(required=True)
    model_result.add_argument("--raw-result"); model_result.add_argument("--unavailable-reason")
    qualification = commands.add_parser("record-reviewer-qualification")
    qualification.add_argument("--review-rule-record-id", required=True); qualification.add_argument("--compiled-review-policy-id", required=True)
    qualification.add_argument("--model", required=True); qualification.add_argument("--effort", choices=("medium", "high", "xhigh"), required=True)
    qualification.add_argument("--reviewer-prompt-id", required=True); qualification.add_argument("--reviewer-prompt-source", required=True)
    qualification.add_argument("--case-set-version", required=True); qualification.add_argument("--prompt-only-comparison-id", required=True)
    qualification.add_argument("--model-review-audit-id", action="append", required=True)
    qualification.add_argument("--reviewer", required=True); qualification.add_argument("--qualified-at", required=True)
    supersession = commands.add_parser("supersede-reviewer-qualification")
    supersession.add_argument("--old-qualification-id", required=True); supersession.add_argument("--new-qualification-id", required=True)
    supersession.add_argument("--reviewer", required=True); supersession.add_argument("--reason", required=True)
    supersession.add_argument("--superseded-at", required=True)
    suspension = commands.add_parser("suspend-reviewer-rule")
    suspension.add_argument("--reviewer-qualification-id", required=True); suspension.add_argument("--reviewer", required=True)
    suspension.add_argument("--reason", required=True); suspension.add_argument("--suspended-at", required=True)
    audit = commands.add_parser("record-model-review-audit")
    audit.add_argument("--model-review-result-record-id", required=True); audit.add_argument("--reviewer", required=True)
    audit.add_argument("--verdict", choices=("confirmed", "rejected"), required=True)
    audit.add_argument("--finding", required=True); audit.add_argument("--audited-at", required=True)
    apply_review = commands.add_parser("apply-model-review")
    apply_review.add_argument("--model-review-result-record-id", required=True); apply_review.add_argument("--compiled-review-policy-id", required=True)
    apply_review.add_argument("--reviewer-prompt-id", required=True); apply_review.add_argument("--case-set-version", required=True)
    experience = commands.add_parser("create-review-experience-candidate"); experience.add_argument("--source", required=True)
    promote_experience = commands.add_parser("promote-review-experience")
    promote_experience.add_argument("--review-experience-candidate-record-id", required=True); promote_experience.add_argument("--reviewer", required=True)
    promote_experience.add_argument("--reason", required=True); promote_experience.add_argument("--promoted-at", required=True)
    prompt_comparison = commands.add_parser("record-prompt-only-comparison")
    prompt_comparison.add_argument("--prompt-only-arm-source", required=True)
    prompt_comparison.add_argument("--reviewer-closed-loop-arm-source", required=True)
    supporting = commands.add_parser("register-supporting-artifact"); supporting.add_argument("--path", required=True)
    supporting.add_argument("--role", default="supporting-derived"); supporting.add_argument("--note", required=True)
    supporting.add_argument("--reviewer", required=True)
    comparison_p = commands.add_parser("render-size-comparison")
    comparison_p.add_argument("--identity", required=True); comparison_p.add_argument("--previous", required=True)
    comparison_p.add_argument("--reference", required=True); comparison_p.add_argument("--candidate", required=True)
    comparison_p.add_argument("--output", required=True)
    normalize_import_p = commands.add_parser("normalize-reviewed-sprite")
    normalize_import_p.add_argument("--source", required=True); normalize_import_p.add_argument("--output", required=True)
    normalize_import_p.add_argument("--preview", required=True)
    equipment_candidate_p = commands.add_parser("prepare-equipment-candidate")
    equipment_candidate_p.add_argument("--contract-id", required=True)
    equipment_candidate_p.add_argument("--source", required=True)
    equipment_candidate_p.add_argument("--output", required=True)
    equipment_candidate_p.add_argument("--preview", required=True)
    equipment_candidate_p.add_argument("--attempt-id")
    equipment_remediation = commands.add_parser("remediate-equipment-candidate")
    equipment_remediation.add_argument("--parent-attempt", required=True); equipment_remediation.add_argument("--feedback-id", required=True)
    equipment_remediation.add_argument("--mode", choices=("palette", "transparent-rgb", "alpha-islands"), required=True)
    equipment_remediation.add_argument("--output", required=True); equipment_remediation.add_argument("--preview", required=True)
    equipment_review = commands.add_parser("render-equipment-review")
    equipment_review.add_argument("--attempt-id", required=True); equipment_review.add_argument("--output", required=True)
    style_verdict = commands.add_parser("record-equipment-style-verdict")
    style_verdict.add_argument("--attempt-id", required=True); style_verdict.add_argument("--reviewer", required=True)
    style_verdict.add_argument("--decision", choices=("approved", "retry"), required=True)
    style_verdict.add_argument("--reason", required=True); style_verdict.add_argument("--decided-at", required=True)
    runtime_copy_p = commands.add_parser("register-runtime-copy")
    runtime_copy_p.add_argument("--source", required=True); runtime_copy_p.add_argument("--target", required=True)
    sync_provenance_p = commands.add_parser("sync-attempt-provenance")
    sync_provenance_p.add_argument("--attempt-id", required=True); sync_provenance_p.add_argument("--reviewer", required=True)
    sync_provenance_p.add_argument("--reason", required=True); sync_provenance_p.add_argument("--synced-at", required=True)
    invalidate_sync_p = commands.add_parser("invalidate-attempt-provenance-sync")
    invalidate_sync_p.add_argument("--attempt-provenance-sync-id", required=True); invalidate_sync_p.add_argument("--reviewer", required=True)
    invalidate_sync_p.add_argument("--reason", required=True); invalidate_sync_p.add_argument("--invalidated-at", required=True)
    relicense_p = commands.add_parser("relicense-public-artifact")
    relicense_p.add_argument("--path", action="append", required=True)
    relicense_p.add_argument("--from-license", default="project-owned")
    relicense_p.add_argument("--to-license", default="CC-BY-4.0")
    relicense_p.add_argument("--reviewer", required=True); relicense_p.add_argument("--reason", required=True)
    relicense_p.add_argument("--decided-at", required=True)
    adopt_p = commands.add_parser("adopt-reviewed-sprite")
    adopt_p.add_argument("--contract-id", required=True); adopt_p.add_argument("--source", required=True)
    adopt_p.add_argument("--candidate", required=True); adopt_p.add_argument("--preview", required=True)
    adopt_p.add_argument("--size-comparison", required=True); adopt_p.add_argument("--reviewer", required=True)
    adopt_p.add_argument("--reason", required=True); adopt_p.add_argument("--accepted-at", required=True)
    recontract_p = commands.add_parser("recontract-reviewed-attempt")
    recontract_p.add_argument("--source-attempt-id", required=True); recontract_p.add_argument("--contract-id", required=True)
    recontract_p.add_argument("--candidate"); recontract_p.add_argument("--processing")
    recontract_p.add_argument("--reviewer", required=True); recontract_p.add_argument("--reason", required=True)
    recontract_p.add_argument("--accepted-at", required=True)
    migrate_component_p = commands.add_parser("migrate-component")
    migrate_component_p.add_argument("--contract-id", required=True); migrate_component_p.add_argument("--source", required=True)
    migrate_component_p.add_argument("--prepared", required=True); migrate_component_p.add_argument("--processing", required=True)
    migrate_component_p.add_argument("--reviewer", required=True); migrate_component_p.add_argument("--reason", required=True)
    migrate_component_p.add_argument("--accepted-at", required=True)
    derive_component_p = commands.add_parser("derive-component")
    derive_component_p.add_argument("--contract-id", required=True); derive_component_p.add_argument("--source-attempt-id", required=True)
    derive_component_p.add_argument(
        "--label",
        choices=("near_hand", "far_hand", "near_foot", "far_foot", "body", "equipment"),
        required=True,
    )
    assembly_p = commands.add_parser("create-assembly"); assembly_p.add_argument("--spec", required=True)
    render_assembly_p = commands.add_parser("render-assembly"); render_assembly_p.add_argument("--assembly-id", required=True)
    resolve_transaction_p = commands.add_parser("resolve-transaction")
    resolve_transaction_p.add_argument("--transaction-id", required=True); resolve_transaction_p.add_argument("--reviewer", required=True)
    resolve_transaction_p.add_argument("--reason", required=True); resolve_transaction_p.add_argument("--decided-at", required=True)
    check_p = commands.add_parser("check"); check_p.add_argument("--strict", action="store_true")
    return parser


def run(args: argparse.Namespace) -> dict[str, Any]:
    store = Store(Path(args.root).resolve())
    handlers = {
        "migrate-legacy": migrate_legacy,
        "register-art-direction-manifest": register_art_direction_manifest,
        "register-art-direction-profile": register_art_direction_profile,
        "register-family-profile": register_family_profile,
        "register-material-language": register_material_language,
        "create-asset-brief": create_asset_brief,
        "create-scene-brief": create_scene_brief,
        "approve-anchor": approve_anchor, "create-series": create_series,
        "set-series-output-limit": set_series_output_limit,
        "create-contract": create_contract, "create-equipment-contract": create_equipment_contract,
        "register-local-reference": register_local_reference,
        "record-anchor-verdict": record_anchor_verdict,
        "record-acceptance-case-result": record_acceptance_case_result,
        "render-art-direction-review": render_art_direction_review,
        "record-art-direction-verdict": record_art_direction_verdict,
        "create-job": create_job,
        "migrate-ready-job-bindings": migrate_ready_job_bindings,
        "retry": retry, "ingest": ingest, "prepare": prepare, "attach-mask": attach_mask,
        "attach-identity-mask": attach_identity_mask,
        "calibrate-core": calibrate_core,
        "validate": validate_attempt, "render-review": render_review, "record-feedback": record_feedback,
        "record-feedback-addendum": record_feedback_addendum,
        "select-attempt": select_attempt, "advance-series": advance_series,
        "approve": lambda s, a: decide(s, a, "approved"),
        "reject": lambda s, a: decide(s, a, "rejected"),
        "approve-exception": approve_exception, "promote": promote,
        "refresh-promoted-preview": refresh_promoted_preview,
        "create-composition": create_composition, "render-pose-guide": render_pose_guide,
        "compile-prompt": compile_prompt, "begin-generation": begin_generation,
        "compile-equipment-prompt": compile_equipment_prompt,
        "record-generation-failure": record_generation_failure,
        "attach-annotations": attach_annotations, "record-advisory-review": record_advisory_review,
        "index-review-history": index_review_history, "audit-review-case": audit_review_case,
        "register-project-review-policy": register_project_review_policy,
        "create-review-rule": create_review_rule, "create-review-case": create_review_case,
        "compile-review-policy": compile_review_policy_record,
        "create-model-review-packet": create_model_review_packet,
        "create-shadow-model-review-packet": create_shadow_model_review_packet,
        "begin-model-review": begin_model_review,
        "record-model-review": record_model_review,
        "record-reviewer-qualification": record_reviewer_qualification,
        "supersede-reviewer-qualification": supersede_reviewer_qualification,
        "suspend-reviewer-rule": suspend_reviewer_rule,
        "record-model-review-audit": record_model_review_audit,
        "apply-model-review": apply_model_review,
        "create-review-experience-candidate": create_review_experience_candidate,
        "promote-review-experience": promote_review_experience,
        "record-prompt-only-comparison": record_prompt_only_comparison,
        "register-supporting-artifact": register_supporting_artifact,
        "render-size-comparison": render_size_comparison,
        "normalize-reviewed-sprite": normalize_reviewed_sprite,
        "prepare-equipment-candidate": prepare_equipment_candidate,
        "remediate-equipment-candidate": remediate_equipment_candidate,
        "render-equipment-review": render_equipment_review,
        "record-equipment-style-verdict": record_equipment_style_verdict,
        "register-runtime-copy": register_runtime_copy,
        "sync-attempt-provenance": sync_attempt_provenance,
        "invalidate-attempt-provenance-sync": invalidate_attempt_provenance_sync,
        "relicense-public-artifact": relicense_public_artifacts,
        "adopt-reviewed-sprite": adopt_reviewed_sprite,
        "recontract-reviewed-attempt": recontract_reviewed_attempt,
        "migrate-component": migrate_component, "derive-component": derive_component,
        "create-assembly": create_assembly, "render-assembly": render_assembly,
        "resolve-transaction": resolve_transaction,
        "check": lambda s, a: strict_check(s, a.strict),
    }
    return handlers[args.command](store, args)


def main() -> int:
    try:
        result = run(build_parser().parse_args())
        print(json.dumps(result, ensure_ascii=False, sort_keys=True, indent=2))
        return 0
    except PipelineError as exc:
        print(f"artwork-pipeline: error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
