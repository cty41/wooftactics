#!/usr/bin/env python3
"""Pure Run compatibility launcher for the pinned MaLiang Pose Proof core."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from _maliang_adapter import activate

_CONFIG = activate()
from maliang_art import core as _maliang_core  # noqa: E402
from maliang_art import pose as _core  # noqa: E402

for _name in dir(_core):
    if not _name.startswith("_"):
        globals()[_name] = getattr(_core, _name)

PoseProofError = _core.PoseProofError
_PROJECT_CANVAS = list(_CONFIG["poseProof"]["canvas"])
_PROJECT_PREVIEW = list(_CONFIG["poseProof"]["preview"])
_PROJECT_REVIEWER = _CONFIG["authority"]["reviewer"]


def _require_project_canvas(value: dict[str, Any]) -> dict[str, Any]:
    if value.get("canvas") != _PROJECT_CANVAS:
        raise PoseProofError(f"Pure Run pose proof canvas must be {_PROJECT_CANVAS}")
    return value


def validate_draft(draft: Any, *, root: str | Path | None = None,
                   verify: bool = False) -> dict[str, Any]:
    return _require_project_canvas(_core.validate_draft(draft, root=root, verify=verify))


def validate_card(card: Any, *, root: str | Path | None = None,
                  verify: bool = False) -> dict[str, Any]:
    value = _require_project_canvas(_core.validate_card(card, root=root, verify=verify))
    if value["selection"]["reviewer"] != _PROJECT_REVIEWER:
        raise PoseProofError(f"pose proof selection requires reviewer {_PROJECT_REVIEWER}")
    return value


def load_draft(path: str | Path) -> dict[str, Any]:
    return validate_draft(_core.load_draft(path))


def render_board(draft: dict[str, Any]):
    return _core.render_board(validate_draft(draft))


def select_option(draft: dict[str, Any], option_id: str, reviewer: str, reason: str,
                  rejected_reasons: dict[str, str], selected_at: str) -> dict[str, Any]:
    """Apply Pure Run's configured human authority before generic selection."""
    validate_draft(draft)
    if reviewer != _PROJECT_REVIEWER:
        raise PoseProofError(f"pose proof selection requires reviewer {_PROJECT_REVIEWER}")
    return validate_card(_core.select_option(
        draft, option_id, reviewer, reason, rejected_reasons, selected_at,
    ))


def render_preview(option: dict[str, Any], canvas: list[int] | tuple[int, int] | None = None,
                   size: list[int] | tuple[int, int] | None = None):
    """Preserve the project-local one-argument API and 128px preview rule."""
    canvas = _PROJECT_CANVAS if canvas is None else list(canvas)
    size = _PROJECT_PREVIEW if size is None else list(size)
    if canvas != _PROJECT_CANVAS or size != _PROJECT_PREVIEW:
        raise PoseProofError(
            f"Pure Run pose preview requires canvas {_PROJECT_CANVAS} and size {_PROJECT_PREVIEW}"
        )
    return _core.render_preview(option, canvas, size)


def write_card(card: dict[str, Any], path: str | Path) -> Path:
    """Preserve legacy pretty JSON bytes through MaLiang's safe publisher."""
    validate_card(card)
    target = Path(path)
    absolute = target.absolute()
    encoded = (json.dumps(card, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    try:
        _maliang_core.write_immutable_bytes(absolute.parent, absolute.name, encoded)
    except _maliang_core.MaLiangError as exc:
        raise PoseProofError(f"pose proof card collision: {target}") from exc
    return target


def main() -> int:
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    render = commands.add_parser("render-options")
    render.add_argument("--spec", required=True); render.add_argument("--output", required=True)
    render.add_argument("--preview-dir")
    select = commands.add_parser("select-option")
    select.add_argument("--spec", required=True); select.add_argument("--option-id", required=True)
    select.add_argument("--output-card", required=True); select.add_argument("--reviewer", required=True)
    select.add_argument("--reason", required=True); select.add_argument("--rejected-reason", action="append", default=[])
    select.add_argument("--selected-at", required=True); select.add_argument("--preview-output")
    args = parser.parse_args()
    try:
        draft = load_draft(Path(args.spec))
        if args.command == "render-options":
            _core.save_png(render_board(draft), Path(args.output))
            if args.preview_dir:
                preview_dir = Path(args.preview_dir).resolve()
                for option in draft["options"]:
                    target = (preview_dir / f"{option['optionId']}.png").resolve()
                    if not target.is_relative_to(preview_dir):
                        raise PoseProofError("preview path escapes preview-dir")
                    _core.save_png(render_preview(option, draft["canvas"]), target)
        else:
            card = select_option(
                draft, args.option_id, args.reviewer, args.reason,
                _core.parse_rejected(args.rejected_reason), args.selected_at,
            )
            write_card(card, Path(args.output_card))
            if args.preview_output:
                _core.save_png(render_preview(card["selectedOption"], card["canvas"]), Path(args.preview_output))
            print(json.dumps(card, ensure_ascii=False, indent=2))
        return 0
    except PoseProofError as exc:
        parser.error(str(exc))
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
