#!/usr/bin/env python3
"""Pure Run authority adapter for the pinned MaLiang reviewer core."""
from __future__ import annotations

from typing import Any, Mapping, Sequence

from _maliang_adapter import activate

_CONFIG = activate()
from maliang_art import review as _core  # noqa: E402

for _name in dir(_core):
    if not _name.startswith("_"):
        globals()[_name] = getattr(_core, _name)

_AUTHORITY = _core.ReviewAuthority(_CONFIG["authority"]["reviewer"])
_RETIRED_PATH_PREFIXES = ("Tools/artworks/amazon/",)
_RETIRED_PATH_PREFIXES_CASEFOLD = tuple(prefix.casefold() for prefix in _RETIRED_PATH_PREFIXES)


def _reject_retired_paths(value: Any) -> None:
    if isinstance(value, Mapping):
        path = value.get("path")
        normalized = path.replace("\\", "/").casefold() if isinstance(path, str) else None
        if normalized is not None and normalized.startswith(_RETIRED_PATH_PREFIXES_CASEFOLD):
            raise _core.ReviewValidationError("artifact path references a retired asset family")
        for item in value.values():
            _reject_retired_paths(item)
    elif isinstance(value, (list, tuple)):
        for item in value:
            _reject_retired_paths(item)


def _call_with_project_paths(function: Any, *args: Any, **kwargs: Any) -> Any:
    _reject_retired_paths(args)
    _reject_retired_paths(kwargs)
    return function(*args, **kwargs)


def derive_review_history_index_entry(*args: Any, **kwargs: Any) -> dict[str, Any]:
    return _call_with_project_paths(_core.derive_review_history_index_entry, *args, **kwargs)


def validate_review_rule(rule: Mapping[str, Any]) -> dict[str, Any]:
    _reject_retired_paths(rule)
    return _core.validate_review_rule(rule)


def audit_case_fitness(case: Mapping[str, Any]) -> dict[str, Any]:
    _reject_retired_paths(case)
    return _core.audit_case_fitness(case, authority=_AUTHORITY)


def validate_review_case(case: Mapping[str, Any]) -> dict[str, Any]:
    _reject_retired_paths(case)
    source = case.get("source") if isinstance(case, Mapping) else None
    if isinstance(source, Mapping) and source.get("reviewer") != _AUTHORITY.reviewer:
        raise _core.ReviewValidationError(
            f"case source must be an explicit {_AUTHORITY.reviewer} decision"
        )
    if case.get("polarity") == "negative" and case.get("generationInputAllowed") is True:
        raise _core.ReviewValidationError("negative cases are forbidden as generation input")
    return _core.validate_review_case(case, authority=_AUTHORITY)


def compile_review_policy(
    project_policy: Mapping[str, Any], rules: Sequence[Mapping[str, Any]],
    cases: Sequence[Mapping[str, Any]], context: Mapping[str, Any], *,
    acceptance_case_ids: Sequence[str] = (), feedback_rule_ids: Sequence[str] = (),
) -> dict[str, Any]:
    _reject_retired_paths((project_policy, rules, cases, context))
    return _core.compile_review_policy(
        project_policy, rules, cases, context, authority=_AUTHORITY,
        acceptance_case_ids=acceptance_case_ids, feedback_rule_ids=feedback_rule_ids,
    )


def build_model_review_packet(*args: Any, **kwargs: Any) -> dict[str, Any]:
    return _call_with_project_paths(_core.build_model_review_packet, *args, **kwargs)


def build_shadow_model_review_packet(*args: Any, **kwargs: Any) -> dict[str, Any]:
    return _call_with_project_paths(_core.build_shadow_model_review_packet, *args, **kwargs)


def validate_model_review_packet(packet: Mapping[str, Any]) -> dict[str, Any]:
    _reject_retired_paths(packet)
    return _core.validate_model_review_packet(packet)


def validate_model_review_result(*args: Any, **kwargs: Any) -> dict[str, Any]:
    return _call_with_project_paths(_core.validate_model_review_result, *args, **kwargs)


def qualification_matches(qualification: Mapping[str, Any], **kwargs: Any) -> bool:
    _reject_retired_paths((qualification, kwargs))
    return _core.qualification_matches(qualification, authority=_AUTHORITY, **kwargs)


def evaluate_automatic_decision(result: Mapping[str, Any], **kwargs: Any) -> dict[str, Any]:
    _reject_retired_paths((result, kwargs))
    return _core.evaluate_automatic_decision(result, authority=_AUTHORITY, **kwargs)


def validate_experience_candidate(candidate: Mapping[str, Any]) -> dict[str, Any]:
    _reject_retired_paths(candidate)
    return _core.validate_experience_candidate(candidate, authority=_AUTHORITY)


def validate_prompt_only_comparison_arm(arm: Mapping[str, Any]) -> dict[str, Any]:
    _reject_retired_paths(arm)
    return _core.validate_prompt_only_comparison_arm(arm)


def validate_prompt_only_comparison(
    prompt_only_arm: Mapping[str, Any], reviewer_arm: Mapping[str, Any],
) -> dict[str, Any]:
    _reject_retired_paths((prompt_only_arm, reviewer_arm))
    return _core.validate_prompt_only_comparison(prompt_only_arm, reviewer_arm)


# Keep MaLiang's compatibility names inside the same project-policy wrappers.
derive_history_index_entry = derive_review_history_index_entry
case_fitness_audit = audit_case_fitness
compile_policy = compile_review_policy
validate_model_review_packet_payload = validate_model_review_packet
validate_model_review_result_payload = validate_model_review_result
evaluate_automatic_review_decision = evaluate_automatic_decision
