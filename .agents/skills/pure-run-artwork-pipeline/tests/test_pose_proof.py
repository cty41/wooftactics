from __future__ import annotations

import copy
import hashlib
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "pose_proof.py"
SPEC = importlib.util.spec_from_file_location("pose_proof", SCRIPT)
pose_proof = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = pose_proof
SPEC.loader.exec_module(pose_proof)
EXAMPLE = Path(__file__).resolve().parents[1] / "examples" / "pose-proof-cast-dr-v1.json"


class PoseProofTests(unittest.TestCase):
    def setUp(self):
        self.draft = json.loads(EXAMPLE.read_text(encoding="utf-8"))

    def test_example_renders_deterministically_with_fixed_palette(self):
        first = pose_proof.render_board(self.draft)
        second = pose_proof.render_board(copy.deepcopy(self.draft))
        self.assertEqual(first.size, (900, 410))
        self.assertEqual(hashlib.sha256(first.tobytes()).hexdigest(), hashlib.sha256(second.tobytes()).hexdigest())
        colors = set(first.get_flattened_data())
        self.assertIn(pose_proof.ORANGE, colors)
        self.assertIn(pose_proof.BACKGROUND, colors)
        self.assertNotIn((0, 255, 0, 255), colors)
        preview = pose_proof.render_preview(self.draft["options"][1])
        self.assertEqual((128, 128), preview.size)

    def test_accepts_one_to_four_options_and_rejects_invalid_shapes(self):
        for count in range(1, 5):
            draft = copy.deepcopy(self.draft)
            templates = self.draft["options"]
            draft["options"] = [copy.deepcopy(templates[index % len(templates)]) for index in range(count)]
            for index, option in enumerate(draft["options"]):
                option["optionId"] = chr(ord("A") + index)
            self.assertEqual(count, len(pose_proof.validate_draft(draft)["options"]))
        for count in (0, 5):
            draft = copy.deepcopy(self.draft)
            draft["options"] = (draft["options"] * 2)[:count]
            with self.assertRaisesRegex(pose_proof.PoseProofError, "1-4"):
                pose_proof.validate_draft(draft)
        duplicate = copy.deepcopy(self.draft)
        duplicate["options"][1]["optionId"] = "A"
        with self.assertRaisesRegex(pose_proof.PoseProofError, "unique"):
            pose_proof.validate_draft(duplicate)
        traversal = copy.deepcopy(self.draft)
        traversal["options"][0]["optionId"] = "../../escaped"
        with self.assertRaisesRegex(pose_proof.PoseProofError, "safe slugs"):
            pose_proof.validate_draft(traversal)
        outside = copy.deepcopy(self.draft)
        outside["options"][0]["head"]["center"] = [300, 1]
        with self.assertRaisesRegex(pose_proof.PoseProofError, "outside"):
            pose_proof.validate_draft(outside)
        unknown = copy.deepcopy(self.draft)
        unknown["identityColor"] = "red"
        with self.assertRaisesRegex(pose_proof.PoseProofError, "unknown"):
            pose_proof.validate_draft(unknown)

    def test_selection_keeps_only_selected_geometry_and_rejection_summaries(self):
        card = pose_proof.select_option(
            self.draft, "B", "cty41", "best compact cast silhouette",
            {"A": "too directional", "C": "too tall"}, "2026-09-15T10:00:00+08:00")
        self.assertEqual("B", card["selectedOption"]["optionId"])
        self.assertNotIn("options", card)
        self.assertEqual(["A", "C"], [item["optionId"] for item in card["selection"]["rejectedOptions"]])
        self.assertEqual(card["poseProofId"], pose_proof.select_option(
            self.draft, "B", "cty41", "best compact cast silhouette",
            {"A": "too directional", "C": "too tall"}, "2026-09-15T10:00:00+08:00")["poseProofId"])

    def test_selection_rejects_missing_authority_reason_and_option(self):
        common = {"draft": self.draft, "option_id": "B", "reviewer": "cty41", "reason": "selected",
                  "rejected_reasons": {"A": "no", "C": "no"}, "selected_at": "2026-09-15T10:00:00+08:00"}
        for field, value, message in (("reviewer", "agent", "reviewer cty41"), ("reason", "", "requires a reason"),
                                      ("option_id", "Z", "does not exist"), ("selected_at", "2026-09-15", "timezone")):
            args = dict(common); args[field] = value
            with self.assertRaisesRegex(pose_proof.PoseProofError, message):
                pose_proof.select_option(**args)
        args = dict(common); args["rejected_reasons"] = {"A": "no"}
        with self.assertRaisesRegex(pose_proof.PoseProofError, "every unselected"):
            pose_proof.select_option(**args)

    def test_write_card_is_idempotent_and_collision_safe(self):
        card = pose_proof.select_option(self.draft, "B", "cty41", "selected", {"A": "no", "C": "no"},
                                        "2026-09-15T10:00:00+08:00")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "card.json"
            pose_proof.write_card(card, path)
            pose_proof.write_card(card, path)
            altered = dict(card); altered["direction"] = "up-left"
            with self.assertRaisesRegex(pose_proof.PoseProofError, "selection is invalid|identity is invalid|collision"):
                pose_proof.write_card(altered, path)

    def test_concurrent_different_cards_never_overwrite_the_winner(self):
        first = pose_proof.select_option(self.draft, "B", "cty41", "selected B", {"A": "no", "C": "no"},
                                         "2026-09-15T10:00:00+08:00")
        second = pose_proof.select_option(self.draft, "A", "cty41", "selected A", {"B": "no", "C": "no"},
                                          "2026-09-15T10:00:01+08:00")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "card.json"
            def publish(card):
                try:
                    pose_proof.write_card(card, path); return "ok"
                except pose_proof.PoseProofError:
                    return "collision"
            with ThreadPoolExecutor(max_workers=2) as pool:
                outcomes = list(pool.map(publish, (first, second)))
            self.assertEqual(["collision", "ok"], sorted(outcomes))
            self.assertIn(json.loads(path.read_text(encoding="utf-8")), (first, second))


if __name__ == "__main__":
    unittest.main()
