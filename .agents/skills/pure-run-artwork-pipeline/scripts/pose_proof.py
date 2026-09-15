#!/usr/bin/env python3
"""Pure Run compatibility launcher for the pinned MaLiang Pose Proof core."""
from __future__ import annotations

import argparse
import json
import os
import tempfile
from pathlib import Path
from typing import Any

from _maliang_adapter import activate

_CONFIG = activate()
from maliang_art import pose as _core  # noqa: E402

for _name in dir(_core):
    if not _name.startswith("_"):
        globals()[_name] = getattr(_core, _name)

PoseProofError = _core.PoseProofError


def select_option(draft: dict[str, Any], option_id: str, reviewer: str, reason: str,
                  rejected_reasons: dict[str, str], selected_at: str) -> dict[str, Any]:
    """Apply Pure Run's configured human authority before generic selection."""
    if reviewer != _CONFIG["authority"]["reviewer"]:
        raise PoseProofError(f"pose proof selection requires reviewer {_CONFIG['authority']['reviewer']}")
    return _core.select_option(draft, option_id, reviewer, reason, rejected_reasons, selected_at)


def render_preview(option: dict[str, Any], canvas: list[int] | tuple[int, int] | None = None,
                   size: list[int] | tuple[int, int] | None = None):
    """Preserve the project-local one-argument API and 128px preview rule."""
    canvas = canvas or _CONFIG["poseProof"]["canvas"]
    size = size or _CONFIG["poseProof"]["preview"]
    return _core.render_preview(option, canvas, size)


def write_card(card: dict[str, Any], path: str | Path) -> Path:
    """Preserve legacy pretty JSON bytes while enforcing MaLiang validation."""
    _core.validate_card(card)
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(card, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    if target.exists():
        if target.read_bytes() != encoded:
            raise PoseProofError(f"pose proof card collision: {target}")
        return target
    handle, temporary_name = tempfile.mkstemp(prefix=f".{target.name}.", suffix=".tmp", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        try:
            os.link(temporary, target)
        except FileExistsError:
            if target.read_bytes() != encoded:
                raise PoseProofError(f"pose proof card collision: {target}")
    finally:
        temporary.unlink(missing_ok=True)
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
        draft = _core.load_draft(Path(args.spec))
        if args.command == "render-options":
            _core.save_png(_core.render_board(draft), Path(args.output))
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
