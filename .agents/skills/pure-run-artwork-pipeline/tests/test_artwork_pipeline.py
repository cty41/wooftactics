from __future__ import annotations

import argparse
import importlib.util
from concurrent.futures import ThreadPoolExecutor
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from PIL import Image, ImageDraw


SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "artwork_pipeline.py"
SPEC = importlib.util.spec_from_file_location("artwork_pipeline", SCRIPT)
pipeline = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = pipeline
SPEC.loader.exec_module(pipeline)
VALIDATOR_SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "validate_sprite_assets.py"
VALIDATOR_SPEC = importlib.util.spec_from_file_location("validate_sprite_assets", VALIDATOR_SCRIPT)
validator = importlib.util.module_from_spec(VALIDATOR_SPEC)
assert VALIDATOR_SPEC.loader
VALIDATOR_SPEC.loader.exec_module(validator)


class ArtworkPipelineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "Tools/public-release").mkdir(parents=True)
        (self.root / "Tools/public-release/asset-provenance.json").write_text(
            json.dumps({"schemaVersion": 1, "defaultAttribution": "cty41", "entries": []}), encoding="utf-8"
        )
        self.store = pipeline.Store(self.root)

    def tearDown(self):
        self.temp.cleanup()

    def test_retired_humanoid_amazon_family_is_hard_rejected(self):
        retired = self.root / "Tools/artworks/amazon/imgs/idle/dr/idle_f01.png"
        retired.parent.mkdir(parents=True)
        retired.write_bytes(b"historical")
        with self.assertRaisesRegex(pipeline.PipelineError, "retired asset family is forbidden"):
            self.store.relative(retired, must_exist=True)

    def test_index_review_history_is_immutable_and_does_not_rewrite_feedback(self):
        artifact = self.png("Tools/artworks/candidate.png")
        attempt = {"schemaVersion": 4, "attemptId": "job-review-a001", "jobId": "job-review", "state": "prepared",
                   "artifacts": {"prepared": {"path": self.store.relative(artifact), "sha256": pipeline.sha256_file(artifact)}}}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt, immutable=True)
        feedback = {"schemaVersion": 2, "feedbackId": "feedback-review", "attemptId": attempt["attemptId"],
                    "reviewer": "cty41", "authorType": "human", "verdict": "retry", "categories": ["topology"]}
        feedback_path = self.store.record("feedback", feedback["feedbackId"])
        pipeline.write_json_idempotent(feedback_path, feedback, immutable=True)
        original = feedback_path.read_bytes()

        index = pipeline.index_review_history(self.store, self.ns())

        self.assertEqual(1, index["sourceCount"])
        self.assertEqual(original, feedback_path.read_bytes())
        self.assertTrue(self.store.record("review-history-indexes", index["reviewHistoryIndexId"]).is_file())
        self.assertEqual("feedback-review", index["entries"][0]["feedbackId"])

    def _model_review_fixture(self, suffix="ok"):
        candidate = self.png(f"Tools/artworks/{suffix}-candidate.png")
        brief_path = self.root / f"Tools/artworks/{suffix}-brief.json"
        brief_path.parent.mkdir(parents=True, exist_ok=True)
        brief_path.write_text(json.dumps({"schemaVersion": 1, "briefId": f"brief-{suffix}"}), encoding="utf-8")
        contract = {
            "schemaVersion": 4, "contractId": f"contract-{suffix}",
            "briefSpec": {"briefId": f"brief-{suffix}", "path": self.store.relative(brief_path), "sha256": pipeline.sha256_file(brief_path)},
            "artDirectionSpec": {"profileId": "project"}, "acceptanceCaseIds": ["CASE-FAIL"],
        }
        pipeline.write_json_idempotent(self.store.record("contracts", contract["contractId"]), contract, immutable=True)
        job = {"schemaVersion": 4, "jobId": f"job-{suffix}", "contractId": contract["contractId"]}
        pipeline.write_json_idempotent(self.store.record("jobs", job["jobId"]), job, immutable=True)
        artifact = {"path": self.store.relative(candidate), "sha256": pipeline.sha256_file(candidate)}
        report = {"schemaVersion": 1, "attemptId": f"job-{suffix}-a001", "inputSha256": artifact["sha256"], "passed": True}
        report_path = self.store.record("reports", f"report-{suffix}")
        pipeline.write_json_idempotent(report_path, report, immutable=True)
        attempt = {"schemaVersion": 4, "attemptId": f"job-{suffix}-a001", "jobId": job["jobId"], "state": "review_pending",
                   "artifacts": {"calibrated": artifact}, "report": {"path": self.store.relative(report_path), "sha256": pipeline.sha256_file(report_path)}}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt, immutable=True)
        rule = {"schemaVersion": 1, "ruleId": "CAPSULE-NO-LIMBS", "version": 1,
                "scope": {"project": "pure-run"}, "authority": "hard", "autoRetryEligible": True,
                "requiredEvidenceRoles": ["CALIBRATED_256"], "sources": [{"type": "document", "id": "design"}],
                "positiveCaseIds": ["CASE-PASS"], "negativeCaseIds": ["CASE-FAIL"], "lifecycle": "active", "statement": "No limbs."}
        feedback = {"schemaVersion": 1, "feedbackId": f"feedback-{suffix}", "attemptId": attempt["attemptId"], "reviewer": "cty41", "verdict": "rejected"}
        pipeline.write_json_idempotent(self.store.record("feedback", feedback["feedbackId"]), feedback, immutable=True)
        case = {"schemaVersion": 1, "caseId": "CASE-FAIL", "version": 1, "ruleIds": ["CAPSULE-NO-LIMBS"],
                "polarity": "negative", "status": "active", "scope": {"project": "pure-run"},
                "artifact": artifact, "source": {"attemptId": attempt["attemptId"], "humanDecision": "rejected", "reviewer": "cty41"},
                "reviewOnly": True, "generationInputAllowed": False, "evidenceRegion": {"x": 0, "y": 0, "width": 1, "height": 1}}
        positive_case = {**case, "caseId": "CASE-PASS", "polarity": "positive",
                         "source": {**case["source"], "humanDecision": "approved"},
                         "reviewOnly": False, "generationInputAllowed": False}
        boundary_case = {**case, "caseId": "CASE-BOUNDARY", "polarity": "boundary"}
        compiled = pipeline.artwork_review.compile_review_policy(
            {"policyId": "policy", "version": 1}, [rule], [case, positive_case, boundary_case],
            {"project": "pure-run"}, acceptance_case_ids=["CASE-FAIL"])
        rule_record_id = f"rule-{suffix}"
        pipeline.write_json_idempotent(self.store.record("review-rules", rule_record_id),
                                       {"schemaVersion": 4, "reviewRuleRecordId": rule_record_id, "rule": rule}, immutable=True)
        case_record_id = f"case-{suffix}"
        pipeline.write_json_idempotent(self.store.record("review-cases", case_record_id),
                                       {"schemaVersion": 4, "reviewCaseRecordId": case_record_id, "case": case}, immutable=True)
        for label, extra_case, decision in (("positive", positive_case, "approved"), ("boundary", boundary_case, "rejected")):
            extra_id = f"case-{suffix}-{label}"
            pipeline.write_json_idempotent(self.store.record("review-cases", extra_id),
                {"schemaVersion": 4, "reviewCaseRecordId": extra_id, "case": extra_case}, immutable=True)
            extra_feedback = {"schemaVersion": 1, "feedbackId": f"feedback-{suffix}-{label}",
                              "attemptId": attempt["attemptId"], "reviewer": "cty41", "verdict": decision}
            pipeline.write_json_idempotent(self.store.record("feedback", extra_feedback["feedbackId"]), extra_feedback, immutable=True)
        policy_id = f"compiled-{suffix}"
        pipeline.write_json_idempotent(self.store.record("compiled-review-policies", policy_id),
                                       {"schemaVersion": 4, "compiledReviewPolicyRecordId": policy_id, "compiled": compiled}, immutable=True)
        packet = pipeline.create_model_review_packet(self.store, self.ns(
            attempt_id=attempt["attemptId"], compiled_review_policy_id=policy_id, required_model="reviewer-v1",
            frozen_invariant=["identity"], artifact=[], evidence=[f"CALIBRATED_256={candidate}"]))
        prompt = self.root / f"Tools/artworks/{suffix}-reviewer-prompt.txt"; prompt.write_text("review", encoding="utf-8")
        invocation = pipeline.begin_model_review(self.store, self.ns(
            attempt_id=attempt["attemptId"], packet_id=packet["packetId"], provider="test-provider", model="reviewer-v1",
            effort="medium", fresh_session_id=f"fresh-{suffix}", prompt_source=str(prompt), started_at="2026-09-04T10:00:00+00:00"))
        return attempt, packet, invocation, policy_id

    def test_prompt_only_comparison_cli_is_immutable_and_strictly_bound(self):
        anchor = self.root / "inputs/anchor.bin"; anchor.parent.mkdir(); anchor.write_bytes(b"anchor")
        contract = {"schemaVersion": 1, "contractId": "contract-comparison"}
        contract_path = self.store.record("contracts", contract["contractId"])
        pipeline.write_json_idempotent(contract_path, contract, immutable=True)
        base = {"schemaVersion": 1, "arm": "prompt-only", "contract": {"contractId": contract["contractId"], "sha256": pipeline.sha256_file(contract_path)},
                "anchors": [{"path": self.store.relative(anchor), "sha256": pipeline.sha256_file(anchor)}], "context": {"family": "poet"}, "imageGenBudget": 3,
                "rawCounts": [{"ruleId": "CAPSULE-NO-LIMBS", "context": {"pose": "idle"}, "knownHardErrorsReachedHuman": 1,
                "repeatedHardErrors": 1, "reviewerMisses": 0, "falsePositives": 0, "repeatedHumanFeedback": 1, "imageGenRounds": 3, "escalations": 0}]}
        reviewer = dict(base); reviewer["arm"] = "reviewer-closed-loop"; reviewer["rawCounts"] = [dict(base["rawCounts"][0], imageGenRounds=2)]
        source_dir = self.root / "inputs"; prompt_source = source_dir / "prompt-arm.json"; reviewer_source = source_dir / "reviewer-arm.json"
        prompt_source.write_text(json.dumps(base), encoding="utf-8"); reviewer_source.write_text(json.dumps(reviewer), encoding="utf-8")
        record = pipeline.record_prompt_only_comparison(self.store, self.ns(prompt_only_arm_source=str(prompt_source), reviewer_closed_loop_arm_source=str(reviewer_source)))
        self.assertTrue(self.store.record("prompt-only-comparisons", record["promptComparisonId"]).is_file())
        self.assertTrue(pipeline.strict_check(self.store, True)["ok"])
        record_path = self.store.record("prompt-only-comparisons", record["promptComparisonId"])
        tampered = pipeline.load_json(record_path); tampered["reviewerClosedLoop"]["imageGenBudget"] = 4
        record_path.write_text(json.dumps(tampered), encoding="utf-8")
        self.assertIn(f"prompt_only_comparison_invalid:{record['promptComparisonId']}", pipeline.strict_check(self.store, False)["issues"])

    def test_model_review_records_packet_invocation_and_valid_result(self):
        attempt, packet, invocation, policy_id = self._model_review_fixture()
        self.assertTrue(self.store.record("model-review-packets", packet["packetId"]).is_file())
        self.assertTrue(self.store.record("model-review-invocations", invocation["modelReviewInvocationId"]).is_file())
        raw = self.root / "Tools/artworks/ok-result.json"
        raw.write_text(json.dumps({"schemaVersion": 1, "packetId": packet["packetId"], "packetSha256": packet["sha256"],
                                   "decision": "pass_to_human", "summary": "Candidate is ready for human review.", "strengths": ["identity"],
                                   "defects": [], "frozenInvariants": ["identity"], "operations": []}), encoding="utf-8")
        result = pipeline.record_model_review(self.store, self.ns(invocation_id=invocation["modelReviewInvocationId"], compiled_review_policy_id=policy_id, raw_result=str(raw), unavailable_reason=None))
        self.assertEqual("model_reviewed", result["outcome"])
        self.assertEqual("model_reviewed", pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))["state"])
        self.assertEqual(pipeline.sha256_file(raw), result["rawResult"]["sha256"])

    def test_model_review_forbidden_authority_and_outside_evidence_require_human_review(self):
        for suffix, extra in (("approved", {"approved": True}), ("human", {"humanDecision": "passed"}),
                              ("score", {"aestheticScore": 10}), ("outside", None)):
            attempt, packet, invocation, policy_id = self._model_review_fixture(suffix)
            raw_payload = {"schemaVersion": 1, "packetId": packet["packetId"], "packetSha256": packet["sha256"],
                           "decision": "retry", "summary": "Needs correction.", "strengths": [],
                           "defects": [{"defectId": "D1", "ruleId": "CAPSULE-NO-LIMBS", "acceptanceCaseId": "CASE-FAIL", "severity": "hard", "certainty": "certain", "observed": "arm", "expected": "none", "evidenceRoles": ["CALIBRATED_256" if extra is not None else "RAW_CANDIDATE"], "region": {"x": 0, "y": 0, "width": 1, "height": 1}}],
                           "frozenInvariants": ["identity"], "operations": []}
            if extra:
                raw_payload.update(extra)
            raw = self.root / f"Tools/artworks/{suffix}-bad-result.json"; raw.write_text(json.dumps(raw_payload), encoding="utf-8")
            result = pipeline.record_model_review(self.store, self.ns(invocation_id=invocation["modelReviewInvocationId"], compiled_review_policy_id=policy_id, raw_result=str(raw), unavailable_reason=None))
            self.assertEqual("human_review_required", result["outcome"])
            self.assertIsNone(result["result"])
            self.assertEqual("human_review_required", pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))["state"])

    def test_historical_shadow_review_preserves_attempt_and_non_authoritative_failures(self):
        attempt, _packet, _invocation, policy_id = self._model_review_fixture("shadow")
        attempt_path = self.store.record("attempts", attempt["attemptId"])
        attempt["state"] = "promoted"; pipeline.write_json_idempotent(attempt_path, attempt)
        before = attempt_path.read_bytes()
        packet = pipeline.create_shadow_model_review_packet(self.store, self.ns(
            review_case_record_id="case-shadow", human_feedback_id="feedback-shadow", compiled_review_policy_id=policy_id,
            required_model="reviewer-v1", requested_effort="xhigh", frozen_invariant=["identity"], evidence=[f"CALIBRATED_256={self.root / 'Tools/artworks/shadow-candidate.png'}"]))
        self.assertTrue(packet["qualificationMode"])
        self.assertFalse(packet["countsTowardFormalAttemptBudget"]); self.assertFalse(packet["mayChangeAttemptState"])
        (self.root / "Tools/artworks/shadow-reviewer-prompt.txt").write_text("review", encoding="utf-8")
        invocation = pipeline.begin_model_review(self.store, self.ns(attempt_id=attempt["attemptId"], packet_id=packet["packetId"], provider="test-provider", model="reviewer-v1", effort="xhigh", fresh_session_id="shadow", prompt_source=str(self.root / "Tools/artworks/shadow-reviewer-prompt.txt"), started_at="2026-09-04T10:00:00+00:00"))
        result = pipeline.record_model_review(self.store, self.ns(invocation_id=invocation["modelReviewInvocationId"], compiled_review_policy_id=policy_id, raw_result=None, unavailable_reason="unavailable"))
        self.assertEqual("human_review_required", result["outcome"]); self.assertIsNone(result["result"])
        malformed = self.root / "Tools/artworks/shadow-malformed.json"; malformed.write_text("not-json", encoding="utf-8")
        second = pipeline.begin_model_review(self.store, self.ns(attempt_id=attempt["attemptId"], packet_id=packet["packetId"], provider="test-provider", model="reviewer-v1", effort="xhigh", fresh_session_id="shadow-malformed", prompt_source=str(self.root / "Tools/artworks/shadow-reviewer-prompt.txt"), started_at="2026-09-04T10:01:00+00:00"))
        malformed_result = pipeline.record_model_review(self.store, self.ns(invocation_id=second["modelReviewInvocationId"], compiled_review_policy_id=policy_id, raw_result=str(malformed), unavailable_reason=None))
        self.assertIsNone(malformed_result["result"]); self.assertIsNotNone(malformed_result["validationError"])
        self.assertEqual(before, attempt_path.read_bytes())
        with self.assertRaisesRegex(pipeline.PipelineError, "cannot be applied"):
            pipeline.apply_model_review(self.store, self.ns(model_review_result_record_id=result["modelReviewResultRecordId"], compiled_review_policy_id=policy_id, reviewer_prompt_id="reviewer-v1", case_set_version="cases-v1"))

    def _legacy_shadow_context(self, suffix, *, positive=False, schema_version=3):
        attempt, _packet, _invocation, policy_id = self._model_review_fixture(suffix)
        source_contract_path = self.store.record("contracts", f"contract-{suffix}")
        source_contract = pipeline.load_json(source_contract_path)
        context_contract = dict(source_contract, contractId=f"contract-{suffix}-review-context")
        pipeline.write_json_idempotent(self.store.record("contracts", context_contract["contractId"]), context_contract, immutable=True)
        legacy_contract = {"schemaVersion": schema_version, "contractId": source_contract["contractId"]}
        if schema_version == 3:
            legacy_contract["assetRole"] = "assembled_sprite"
        pipeline.write_json_idempotent(source_contract_path, legacy_contract)
        if positive:
            case_path = self.store.record("review-cases", f"case-{suffix}")
            case_record = pipeline.load_json(case_path)
            case_record["case"]["polarity"] = "positive"
            case_record["case"]["source"]["humanDecision"] = "approved"
            pipeline.write_json_idempotent(case_path, case_record)
            feedback_path = self.store.record("feedback", f"feedback-{suffix}")
            feedback = pipeline.load_json(feedback_path); feedback["verdict"] = "approved"
            pipeline.write_json_idempotent(feedback_path, feedback)
        args = self.ns(review_case_record_id=f"case-{suffix}", human_feedback_id=f"feedback-{suffix}",
                       compiled_review_policy_id=policy_id, required_model="reviewer-v1", requested_effort="high",
                       frozen_invariant=["identity"], evidence=[f"NEGATIVE_REVIEW_ONLY={self.root / f'Tools/artworks/{suffix}-candidate.png'}"],
                       review_context_contract_id=context_contract["contractId"], review_context_brief_source=None)
        return attempt, policy_id, args

    def test_legacy_positive_shadow_case_succeeds_with_explicit_review_context(self):
        for schema_version in (1, 2, 3):
            with self.subTest(schema_version=schema_version):
                suffix = f"legacy-positive-v{schema_version}"
                attempt, _policy_id, args = self._legacy_shadow_context(suffix, positive=True, schema_version=schema_version)
                before = self.store.record("attempts", attempt["attemptId"]).read_bytes()
                packet = pipeline.create_shadow_model_review_packet(self.store, args)
                self.assertEqual("REVIEW_CONTEXT_ONLY", packet["reviewContext"]["responsibility"])
                self.assertEqual("approved", packet["historicalProvenance"]["humanDecision"])
                self.assertEqual(before, self.store.record("attempts", attempt["attemptId"]).read_bytes())

    def test_legacy_shadow_case_without_explicit_override_fails(self):
        _attempt, _policy_id, args = self._legacy_shadow_context("legacy-missing")
        args.review_context_contract_id = None
        with self.assertRaisesRegex(pipeline.PipelineError, "explicit --review-context-contract-id"):
            pipeline.create_shadow_model_review_packet(self.store, args)

    def test_legacy_shadow_packet_separates_provenance_from_review_context(self):
        _attempt, _policy_id, args = self._legacy_shadow_context("legacy-separation")
        packet = pipeline.create_shadow_model_review_packet(self.store, args)
        self.assertEqual("contract-legacy-separation", packet["historicalProvenance"]["contract"]["contractId"])
        self.assertEqual("contract-legacy-separation-review-context", packet["reviewContext"]["contract"]["contractId"])
        self.assertNotEqual(packet["historicalProvenance"]["contract"], packet["reviewContext"]["contract"])
        self.assertIn("compiledPolicyRecord", packet["reviewContext"])

    def test_legacy_negative_case_artifact_and_evidence_remain_generation_isolated(self):
        _attempt, _policy_id, args = self._legacy_shadow_context("legacy-negative")
        packet = pipeline.create_shadow_model_review_packet(self.store, args)
        historical = packet["artifacts"][0]
        self.assertTrue(historical["reviewOnly"]); self.assertFalse(historical["generationInput"])
        self.assertFalse(packet["evidence"][0]["generationInput"])
        bad = dict(packet); bad["artifacts"] = [dict(historical, generationInput=True)]
        bad["packetId"] = pipeline.artwork_review.stable_id("model-review-packet", {k: v for k, v in bad.items() if k not in {"packetId", "sha256"}})
        bad["sha256"] = pipeline.hashlib.sha256(pipeline.artwork_review.canonical_bytes({k: v for k, v in bad.items() if k != "sha256"})).hexdigest()
        with self.assertRaisesRegex(pipeline.artwork_review.ReviewValidationError, "forbidden generation input"):
            pipeline.artwork_review.validate_model_review_packet(bad)

    def test_model_review_unavailable_result_is_preserved_for_human_review(self):
        attempt, _packet, invocation, policy_id = self._model_review_fixture("unavailable")
        result = pipeline.record_model_review(self.store, self.ns(
            invocation_id=invocation["modelReviewInvocationId"], compiled_review_policy_id=policy_id,
            raw_result=None, unavailable_reason="reviewer transport unavailable"))
        self.assertEqual("human_review_required", result["outcome"])
        self.assertIsNone(result["rawResult"])
        self.assertIsNone(result["result"])
        self.assertEqual("human_review_required", pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))["state"])

    def _record_retry_result(self, packet, invocation, policy_id, suffix):
        raw = self.root / f"Tools/artworks/{suffix}-retry-result.json"
        raw.write_text(json.dumps({"schemaVersion": 1, "packetId": packet["packetId"], "packetSha256": packet["sha256"],
            "decision": "retry", "summary": "Visible limb.", "strengths": [],
            "defects": [{"defectId": "D1", "ruleId": "CAPSULE-NO-LIMBS", "acceptanceCaseId": "CASE-FAIL", "severity": "hard", "certainty": "certain", "observed": "arm", "expected": "none", "evidenceRoles": ["CALIBRATED_256"], "region": {"x": 0, "y": 0, "width": 1, "height": 1}}],
            "frozenInvariants": ["identity"], "operations": [{"operation": "remove", "target": "limb", "instruction": "Remove limb.", "defectId": "D1"}]}), encoding="utf-8")
        return pipeline.record_model_review(self.store, self.ns(invocation_id=invocation["modelReviewInvocationId"], compiled_review_policy_id=policy_id, raw_result=str(raw), unavailable_reason=None))

    def _write_test_qualification(self, review_rule_record_id, policy_id, prompt):
        suffix = review_rule_record_id.removeprefix("rule-")
        rule = pipeline.load_json(self.store.record("review-rules", review_rule_record_id))["rule"]
        compiled = pipeline.load_json(self.store.record("compiled-review-policies", policy_id))["compiled"]
        contract_path = self.store.record("contracts", f"contract-{suffix}")
        candidate = self.root / f"Tools/artworks/{suffix}-candidate.png"
        anchor = {"path": self.store.relative(candidate), "sha256": pipeline.sha256_file(candidate)}
        context = {"compiledPolicyId": compiled["compiledPolicyId"], "caseSetVersion": "cases-v1"}
        counts = {"ruleId": rule["ruleId"], "context": {"pose": "idle"}, "knownHardErrorsReachedHuman": 1,
                  "repeatedHardErrors": 1, "reviewerMisses": 0, "falsePositives": 0,
                  "repeatedHumanFeedback": 1, "imageGenRounds": 1, "escalations": 0}
        base = {"schemaVersion": 1, "arm": "prompt-only",
                "contract": {"contractId": f"contract-{suffix}", "sha256": pipeline.sha256_file(contract_path)},
                "anchors": [anchor], "context": context, "imageGenBudget": 3, "rawCounts": [counts]}
        closed = {**base, "arm": "reviewer-closed-loop", "rawCounts": [{**counts, "knownHardErrorsReachedHuman": 0}]}
        proof_dir = self.root / "Tools/artworks/qualification-proof"; proof_dir.mkdir(parents=True, exist_ok=True)
        prompt_arm = proof_dir / f"{suffix}-prompt.json"; closed_arm = proof_dir / f"{suffix}-closed.json"
        prompt_arm.write_text(json.dumps(base), encoding="utf-8"); closed_arm.write_text(json.dumps(closed), encoding="utf-8")
        comparison = pipeline.record_prompt_only_comparison(self.store, self.ns(
            prompt_only_arm_source=str(prompt_arm), reviewer_closed_loop_arm_source=str(closed_arm)))
        audit_ids = []
        for label, case_id, feedback_id, decision in (
                ("negative", f"case-{suffix}", f"feedback-{suffix}", "retry"),
                ("positive", f"case-{suffix}-positive", f"feedback-{suffix}-positive", "pass_to_human"),
                ("boundary", f"case-{suffix}-boundary", f"feedback-{suffix}-boundary", "escalate_to_human")):
            packet = pipeline.create_shadow_model_review_packet(self.store, self.ns(
                review_case_record_id=case_id, human_feedback_id=feedback_id,
                compiled_review_policy_id=policy_id, required_model="reviewer-v1", requested_effort="medium",
                frozen_invariant=["identity"], evidence=[f"NEGATIVE_REVIEW_ONLY={candidate}"],
                review_context_contract_id=None, review_context_brief_source=None))
            invocation = pipeline.begin_model_review(self.store, self.ns(
                attempt_id=f"job-{suffix}-a001", packet_id=packet["packetId"], provider="test-provider",
                model="reviewer-v1", effort="medium", fresh_session_id=f"fresh-{suffix}-{label}",
                prompt_source=str(prompt), started_at="2026-09-04T10:00:00+00:00"))
            raw = proof_dir / f"{suffix}-{label}-result.json"
            defects = [{"defectId": "D1", "ruleId": rule["ruleId"], "acceptanceCaseId": "CASE-FAIL",
                        "severity": "hard", "certainty": "certain", "observed": "arm", "expected": "none",
                        "evidenceRoles": ["HISTORICAL_CASE_ARTIFACT"], "region": {"x": 0, "y": 0, "width": 1, "height": 1}}] if decision == "retry" else []
            operations = [{"operation": "remove", "target": "limb", "instruction": "Remove limb.", "defectId": "D1"}] if decision == "retry" else []
            raw.write_text(json.dumps({"schemaVersion": 1, "packetId": packet["packetId"], "packetSha256": packet["sha256"],
                "decision": decision, "summary": label, "strengths": [], "defects": defects,
                "frozenInvariants": ["identity"], "operations": operations}), encoding="utf-8")
            result = pipeline.record_model_review(self.store, self.ns(invocation_id=invocation["modelReviewInvocationId"],
                compiled_review_policy_id=policy_id, raw_result=str(raw), unavailable_reason=None))
            audit = pipeline.record_model_review_audit(self.store, self.ns(
                model_review_result_record_id=result["modelReviewResultRecordId"], reviewer="cty41",
                verdict="confirmed", finding=f"confirmed-{label}", audited_at="2026-09-04T10:30:00+00:00"))
            audit_ids.append(audit["modelReviewAuditId"])
        return pipeline.record_reviewer_qualification(self.store, self.ns(
            review_rule_record_id=review_rule_record_id, compiled_review_policy_id=policy_id,
            model="reviewer-v1", effort="medium", reviewer_prompt_id="reviewer-v1",
            reviewer_prompt_source=str(prompt), case_set_version="cases-v1", reviewer="cty41",
            prompt_only_comparison_id=comparison["promptComparisonId"], model_review_audit_id=audit_ids,
            qualified_at="2026-09-04T11:00:00+00:00"))

    def test_reviewer_qualification_requires_prompt_only_comparison(self):
        _attempt, _packet, _invocation, policy_id = self._model_review_fixture("qualification-audits")
        with self.assertRaisesRegex(pipeline.PipelineError, "prompt-only comparison"):
            pipeline.record_reviewer_qualification(self.store, self.ns(review_rule_record_id="rule-qualification-audits", compiled_review_policy_id=policy_id, model="reviewer-v1", effort="medium", reviewer_prompt_id="reviewer-v1", reviewer_prompt_source=str(self.root / "Tools/artworks/qualification-audits-reviewer-prompt.txt"), case_set_version="cases-v1", reviewer="cty41", qualified_at="2026-09-04T11:00:00+00:00"))

    def test_apply_model_review_pass_and_escalation_set_human_states(self):
        for suffix, model_decision, expected in (("apply-pass", "pass_to_human", "review_pending"), ("apply-escalate", "escalate_to_human", "human_review_required")):
            attempt, packet, invocation, policy_id = self._model_review_fixture(suffix)
            raw = self.root / f"Tools/artworks/{suffix}.json"
            raw.write_text(json.dumps({"schemaVersion": 1, "packetId": packet["packetId"], "packetSha256": packet["sha256"], "decision": model_decision, "summary": "handoff", "strengths": [], "defects": [], "frozenInvariants": ["identity"], "operations": []}), encoding="utf-8")
            result = pipeline.record_model_review(self.store, self.ns(invocation_id=invocation["modelReviewInvocationId"], compiled_review_policy_id=policy_id, raw_result=str(raw), unavailable_reason=None))
            applied = pipeline.apply_model_review(self.store, self.ns(model_review_result_record_id=result["modelReviewResultRecordId"], compiled_review_policy_id=policy_id, reviewer_prompt_id="reviewer-v1", case_set_version="cases-v1"))
            self.assertEqual(expected, applied["decision"]["action"])
            self.assertEqual(expected, pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))["state"])

    def test_apply_model_review_requires_exact_qualification_and_honors_suspension(self):
        attempt, packet, invocation, policy_id = self._model_review_fixture("qualified")
        result = self._record_retry_result(packet, invocation, policy_id, "qualified")
        unqualified = pipeline.apply_model_review(self.store, self.ns(model_review_result_record_id=result["modelReviewResultRecordId"], compiled_review_policy_id=policy_id, reviewer_prompt_id="reviewer-v1", case_set_version="cases-v1"))
        self.assertEqual("human_review_required", unqualified["decision"]["action"])
        attempt, packet, invocation, policy_id = self._model_review_fixture("suspended")
        result = self._record_retry_result(packet, invocation, policy_id, "suspended")
        qualification = self._write_test_qualification("rule-suspended", policy_id, self.root / "Tools/artworks/suspended-reviewer-prompt.txt")
        pipeline.suspend_reviewer_rule(self.store, self.ns(reviewer_qualification_id=qualification["reviewerQualificationId"], reviewer="cty41", reason="false positive", suspended_at="2026-09-04T12:00:00+00:00"))
        applied = pipeline.apply_model_review(self.store, self.ns(model_review_result_record_id=result["modelReviewResultRecordId"], compiled_review_policy_id=policy_id, reviewer_prompt_id="reviewer-v1", case_set_version="cases-v1"))
        self.assertEqual("human_review_required", applied["decision"]["action"])
        self.assertEqual("human_review_required", pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))["state"])

    def test_qualified_a001_creates_a002_with_next_generation_round(self):
        attempt, packet, invocation, policy_id = self._model_review_fixture("retry-child")
        result = self._record_retry_result(packet, invocation, policy_id, "retry-child")
        prompt = self.root / "Tools/artworks/retry-child-reviewer-prompt.txt"
        qualification = self._write_test_qualification("rule-retry-child", policy_id, prompt)
        self.assertEqual("cty41", qualification["reviewer"])
        applied = pipeline.apply_model_review(self.store, self.ns(model_review_result_record_id=result["modelReviewResultRecordId"], compiled_review_policy_id=policy_id, reviewer_prompt_id="reviewer-v1", case_set_version="cases-v1"))
        child = pipeline.load_json(self.store.record("attempts", applied["childAttemptId"]))
        self.assertEqual((2, 2, attempt["attemptId"]), (child["ordinal"], child["generationRound"], child["parentAttemptId"]))
        self.assertEqual("model_reviewed", pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))["state"])

    def test_review_experience_promotion_requires_cty41_and_never_mutates_rules(self):
        source = self.root / "Tools/artworks/experience.json"; source.parent.mkdir(parents=True, exist_ok=True)
        source.write_text(json.dumps({"schemaVersion": 1, "feedbackId": "feedback-1", "attemptId": "job-a001", "action": "draft-rule", "rationale": "recurring", "status": "proposed", "createdBy": "policy-curator", "ruleDraft": {"lifecycle": "draft", "autoRetryEligible": False}}), encoding="utf-8")
        candidate = pipeline.create_review_experience_candidate(self.store, self.ns(source=str(source)))
        with self.assertRaisesRegex(pipeline.PipelineError, "cty41"):
            pipeline.promote_review_experience(self.store, self.ns(review_experience_candidate_record_id=candidate["reviewExperienceCandidateRecordId"], reviewer="agent", reason="no", promoted_at="2026-09-04T12:00:00+00:00"))
        promotion = pipeline.promote_review_experience(self.store, self.ns(review_experience_candidate_record_id=candidate["reviewExperienceCandidateRecordId"], reviewer="cty41", reason="approved lesson", promoted_at="2026-09-04T12:00:00+00:00"))
        self.assertFalse(promotion["activeRulesModified"])
        self.assertFalse(any((self.store.pipeline / "review-rules").glob("*.json")))

    def test_technical_remediation_inherits_parent_generation_round(self):
        attempt, _packet, _invocation, _policy_id = self._model_review_fixture("technical-round")
        child = pipeline.retry(self.store, self.ns(job_id=attempt["jobId"], parent_attempt=attempt["attemptId"], feedback_id=None, technical_remediation=True))
        self.assertEqual(1, child["generationRound"])
        self.assertTrue(child["technicalRemediation"])

    def test_bound_input_hash_accepts_only_text_line_ending_normalization(self):
        prompt = self.root / "prompt.md"
        prompt.write_bytes(b"line one\nline two\n")
        expected_crlf = pipeline.hashlib.sha256(b"line one\r\nline two\r\n").hexdigest()
        self.assertTrue(pipeline.bound_input_hash_matches(prompt, expected_crlf))

        binary = self.root / "sprite.png"
        binary.write_bytes(b"line one\nline two\n")
        self.assertFalse(pipeline.bound_input_hash_matches(binary, expected_crlf))

    def test_migrate_ready_job_bindings_preserves_old_record_and_writes_receipt(self):
        prompt = self.root / "prompt.md"
        prompt.write_text("new prompt\n", encoding="utf-8")
        old_job = {
            "schemaVersion": 2,
            "jobId": "job-old",
            "state": "ready",
            "contractId": "contract-test",
            "contractSha256": "contract-sha",
            "prompt": {"path": "prompt.md", "sha256": pipeline.hashlib.sha256(b"old prompt\n").hexdigest()},
            "inputs": [],
            "target": {"direction": "down-right", "pose": "display"},
            "series": None,
            "conceptOnly": False,
            "contractRequirements": None,
            "requiresInvocation": False,
            "poseGuide": None,
            "localReferences": [],
        }
        old_path = self.store.record("jobs", "job-old")
        pipeline.write_json_idempotent(old_path, old_job, immutable=True)
        old_bytes = old_path.read_bytes()
        attempt = {"schemaVersion": 2, "attemptId": "job-old-a001", "jobId": "job-old", "state": "prepared"}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt, immutable=True)

        receipt = pipeline.migrate_ready_job_bindings(self.store, self.ns(
            job_id="job-old", reason="authorized prompt rebinding", authorized_by="direct-user-confirmation"))

        self.assertEqual(old_path.read_bytes(), old_bytes)
        self.assertNotEqual(receipt["newJobId"], "job-old")
        replacement = pipeline.load_json(self.store.record("jobs", receipt["newJobId"]))
        self.assertEqual(replacement["prompt"]["sha256"], pipeline.sha256_file(prompt))
        self.assertTrue(self.store.record("job-migrations", receipt["migrationId"]).is_file())
        self.assertEqual(receipt["historicalAttempts"][0]["attemptId"], "job-old-a001")

    def test_relicense_public_artifacts_records_cty41_decision_and_updates_manifest(self):
        asset = self.png("Tools/artworks/approved/relicensed.png")
        digest = pipeline.sha256_file(asset)
        manifest_path = self.root / "Tools/public-release/asset-provenance.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["entries"].append({
            "path": "Tools/artworks/approved/relicensed.png", "sha256": digest,
            "status": "approved", "rightsHolder": "cty41", "license": "project-owned",
            "provenance": "project-owned-gpt-generated",
        })
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

        receipt = pipeline.relicense_public_artifacts(self.store, self.ns(
            path=[str(asset)], from_license="project-owned", to_license="CC-BY-4.0",
            reviewer="cty41", reason="publish approved project-owned output",
            decided_at="2026-08-21T22:00:00+08:00"))

        updated = json.loads(manifest_path.read_text(encoding="utf-8"))
        self.assertEqual("CC-BY-4.0", updated["entries"][0]["license"])
        self.assertEqual(digest, receipt["artifacts"][0]["sha256"])
        self.assertTrue(self.store.record("license-receipts", receipt["licenseReceiptId"]).is_file())
        issues = []
        self.assertEqual({self.store.relative(asset)}, pipeline._validated_relicensed_paths(self.store, issues, {}))
        self.assertEqual([], issues)
        with self.assertRaisesRegex(pipeline.PipelineError, "not currently licensed"):
            pipeline.relicense_public_artifacts(self.store, self.ns(
                path=[str(asset)], from_license="project-owned", to_license="CC-BY-4.0",
                reviewer="cty41", reason="duplicate receipt", decided_at="2026-08-21T22:01:00+08:00"))
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer cty41"):
            pipeline.relicense_public_artifacts(self.store, self.ns(
                path=[str(asset)], from_license="project-owned", to_license="CC-BY-4.0",
                reviewer="codex", reason="not authorized", decided_at="2026-08-21T22:00:00+08:00"))
        receipt_path = self.store.record("license-receipts", receipt["licenseReceiptId"])
        malformed = dict(receipt)
        malformed["artifacts"] = ["bad"]
        receipt_path.write_text(json.dumps(malformed), encoding="utf-8")
        malformed_issues = []
        pipeline._validated_relicensed_paths(self.store, malformed_issues, {})
        self.assertEqual([f"license_receipt_invalid:{receipt_path.stem}"], malformed_issues)

    def test_relicense_reconciles_only_declared_existing_projection(self):
        asset = self.png("Tools/artworks/reviews/already-public.png")
        digest = pipeline.sha256_file(asset)
        manifest_path = self.root / "Tools/public-release/asset-provenance.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["entries"].append({
            "path": self.store.relative(asset), "sha256": digest, "status": "approved",
            "rightsHolder": "cty41", "license": "CC-BY-4.0", "provenance": "project-owned-supporting-derived",
        })
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        declaration_payload = {
            "artifact": {"path": self.store.relative(asset), "sha256": digest},
            "role": "historical-review-only", "note": "cty41 declaration", "reviewer": "cty41",
            "rights": {"rightsHolder": "cty41", "license": "project-owned",
                       "provenance": "cty41-direct-supporting-artifact-declaration"},
        }
        declaration_id = pipeline.stable_id("supporting-artifact", declaration_payload)
        pipeline.write_json_idempotent(self.store.record("supporting-artifacts", declaration_id),
            {"schemaVersion": 3, "supportingArtifactId": declaration_id, **declaration_payload}, immutable=True)
        args = self.ns(path=[str(asset)], from_license="project-owned", to_license="CC-BY-4.0",
                       reconcile_existing_projection=True, reviewer="cty41",
                       reason="reconcile explicitly authorized historical projection",
                       decided_at="2026-09-16T16:00:00+08:00")

        receipt = pipeline.relicense_public_artifacts(self.store, args)

        self.assertTrue(receipt["reconcilesExistingProjection"])
        issues = []
        self.assertEqual({self.store.relative(asset)}, pipeline._validated_relicensed_paths(self.store, issues, {}))
        self.assertEqual([], issues)
        with self.assertRaisesRegex(pipeline.PipelineError, "already covered"):
            pipeline.relicense_public_artifacts(self.store, args)

    def test_prune_missing_public_provenance_records_cty41_decision(self):
        existing = self.png("Tools/artworks/approved/existing.png")
        manifest_path = self.root / "Tools/public-release/asset-provenance.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["entries"].extend([
            {"path": "Tools/artworks/tmp/missing.png", "sha256": "1" * 64,
             "status": "approved", "rightsHolder": "cty41", "license": "CC-BY-4.0", "provenance": "test"},
            {"path": self.store.relative(existing), "sha256": pipeline.sha256_file(existing),
             "status": "approved", "rightsHolder": "cty41", "license": "CC-BY-4.0", "provenance": "test"},
        ])
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        args = self.ns(path=["Tools/artworks/tmp/missing.png"], reviewer="cty41",
                       reason="remove stale public manifest projection", decided_at="2026-09-16T14:52:37+08:00")

        receipt = pipeline.prune_missing_public_provenance(self.store, args)

        updated = json.loads(manifest_path.read_text(encoding="utf-8"))
        self.assertEqual([self.store.relative(existing)], [entry["path"] for entry in updated["entries"]])
        self.assertEqual("Tools/artworks/tmp/missing.png", receipt["removedEntries"][0]["path"])
        self.assertTrue(self.store.record("provenance-prunes", receipt["provenancePruneId"]).is_file())
        with self.assertRaisesRegex(pipeline.PipelineError, "requires reviewer cty41"):
            pipeline.prune_missing_public_provenance(self.store, self.ns(
                path=["Tools/artworks/tmp/other.png"], reviewer="agent", reason="unauthorized",
                decided_at="2026-09-16T14:52:37+08:00"))
        with self.assertRaisesRegex(pipeline.PipelineError, "existing artifact"):
            pipeline.prune_missing_public_provenance(self.store, self.ns(
                path=[str(existing)], reviewer="cty41", reason="must not remove existing",
                decided_at="2026-09-16T14:52:37+08:00"))

    def test_remediate_exact_chroma_artifacts_records_low_alpha_changes(self):
        candidate = self.png("Tools/artworks/equipment/candidates/item.png")
        with Image.open(candidate) as opened:
            image = opened.convert("RGBA")
        image.putpixel((12, 34), (0, 255, 0, 2))
        image.putpixel((56, 78), (255, 0, 255, 4))
        image.save(candidate, format="PNG", optimize=False, compress_level=9)
        master = self.root / "Tools/artworks/equipment/calibrated/item.png"
        master.parent.mkdir(parents=True)
        image.save(master, format="PNG", optimize=False, compress_level=9)
        before = pipeline.sha256_file(candidate)
        approval = {"schemaVersion": 2, "approvalId": "approval-chroma", "decision": "approved", "reviewer": "cty41"}
        pipeline.write_json_idempotent(self.store.record("approvals", "approval-chroma"), approval, immutable=True)
        attempt = {
            "schemaVersion": 2, "attemptId": "job-chroma-a001", "jobId": "job-chroma", "state": "promoted",
            "approvalId": "approval-chroma", "artifacts": {
                "prepared": {"path": self.store.relative(candidate), "sha256": before},
                "promoted": {"master": {"path": self.store.relative(master), "sha256": before}},
            },
        }
        pipeline.write_json_idempotent(self.store.record("attempts", "job-chroma-a001"), attempt, immutable=True)
        manifest_path = self.root / "Tools/public-release/asset-provenance.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        for path in (candidate, master):
            manifest["entries"].append({"path": self.store.relative(path), "sha256": before, "status": "approved",
                "rightsHolder": "cty41", "license": "project-owned", "provenance": "test"})
        missing_manifest = dict(manifest)
        missing_manifest["entries"] = manifest["entries"][:-1]
        manifest_path.write_text(json.dumps(missing_manifest), encoding="utf-8")
        args = self.ns(attempt_id="job-chroma-a001", path=[str(candidate), str(master)], reviewer="cty41",
                       reason="authorized exact chroma cleanup", authorized_at="2026-09-14T15:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "provenance entry is missing"):
            pipeline.remediate_exact_chroma_artifacts(self.store, args)
        self.assertEqual(before, pipeline.sha256_file(candidate))
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        args = self.ns(attempt_id="job-chroma-a001", path=[str(candidate), str(master)], reviewer="cty41",
                       reason="authorized exact chroma cleanup", authorized_at="2026-09-14T15:00:00+08:00")
        real_write = pipeline.write_json_idempotent

        def fail_manifest(path, value, immutable=False):
            if path == manifest_path:
                raise OSError("simulated manifest failure")
            return real_write(path, value, immutable=immutable)

        with mock.patch.object(pipeline, "write_json_idempotent", side_effect=fail_manifest):
            with self.assertRaisesRegex(pipeline.PipelineError, "transaction rolled back"):
                pipeline.remediate_exact_chroma_artifacts(self.store, args)
        self.assertEqual(before, pipeline.sha256_file(candidate))
        self.assertEqual(before, pipeline.sha256_file(master))
        self.assertEqual([], list((self.store.pipeline / "exact-chroma-remediations").glob("*.json")))

        receipt = pipeline.remediate_exact_chroma_artifacts(self.store, args)

        self.assertEqual(2, len(receipt["artifacts"]))
        for path in (candidate, master):
            with Image.open(path) as cleaned:
                self.assertEqual((0, 0, 0, 0), cleaned.getpixel((12, 34)))
                self.assertEqual((0, 0, 0, 0), cleaned.getpixel((56, 78)))
        updated = json.loads(manifest_path.read_text(encoding="utf-8"))
        self.assertTrue(all(entry["sha256"] != before for entry in updated["entries"]))
        self.assertTrue(self.store.record("exact-chroma-remediations", receipt["exactChromaRemediationId"]).is_file())
        issues = []
        remediations = pipeline._validated_exact_chroma_remediations(self.store, issues)
        self.assertEqual([], issues)
        self.assertTrue(pipeline._effective_artifact_binding_matches(
            self.store, attempt["artifacts"]["prepared"], remediations))
        receipt_path = self.store.record("exact-chroma-remediations", receipt["exactChromaRemediationId"])
        forged = json.loads(json.dumps(receipt))
        with Image.open(candidate) as opened:
            tampered = opened.copy()
        tampered.putpixel((1, 1), (1, 2, 3, 255))
        tampered.save(candidate, format="PNG", optimize=False, compress_level=9)
        next(item for item in forged["artifacts"] if item["path"] == self.store.relative(candidate))["afterSha256"] = pipeline.sha256_file(candidate)
        forged_payload = {key: value for key, value in forged.items()
                          if key not in {"schemaVersion", "exactChromaRemediationId"}}
        forged["exactChromaRemediationId"] = pipeline.stable_id("exact-chroma-remediation", forged_payload)
        receipt_path.unlink()
        receipt_path = self.store.record("exact-chroma-remediations", forged["exactChromaRemediationId"])
        pipeline.write_json_idempotent(receipt_path, forged, immutable=True)
        forged_issues = []
        pipeline._validated_exact_chroma_remediations(self.store, forged_issues)
        self.assertEqual([f"exact_chroma_remediation_invalid:{receipt_path.stem}"], forged_issues)
        malformed = dict(forged)
        malformed["artifacts"] = ["bad"]
        receipt_path.write_text(json.dumps(malformed), encoding="utf-8")
        malformed_issues = []
        pipeline._validated_exact_chroma_remediations(self.store, malformed_issues)
        self.assertEqual([f"exact_chroma_remediation_invalid:{receipt_path.stem}"], malformed_issues)

    def test_register_supporting_artifact_cannot_grant_rights_without_cty41_approval(self):
        guide = self.root / "Tools/artworks/doge/demonbound/pose-guide.svg"
        guide.parent.mkdir(parents=True)
        guide.write_text('<svg xmlns="http://www.w3.org/2000/svg"/>', encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "requires reviewer cty41"):
            pipeline.register_supporting_artifact(self.store, self.ns(
                path=str(guide), role="pose-guide", note="offline artwork support only",
                reviewer="agent", rights_approval_id="missing"))
        manifest = json.loads((self.root / "Tools/public-release/asset-provenance.json").read_text(encoding="utf-8"))
        self.assertEqual([], manifest["entries"])
        tutorial = self.root / "Tools/artworks/doge/demonbound/tutorial-card.json"
        tutorial.write_text('{"schemaVersion":1}\n', encoding="utf-8")
        pipeline.register_supporting_artifact(self.store, self.ns(
            path=str(tutorial), role="supporting-derived", note="public tutorial data",
            reviewer="cty41", rights_approval_id=None))
        manifest = json.loads((self.root / "Tools/public-release/asset-provenance.json").read_text(encoding="utf-8"))
        self.assertEqual("project-owned", manifest["entries"][0]["license"])
        self.assertEqual("Tools/artworks/doge/demonbound/tutorial-card.json", manifest["entries"][0]["path"])

    def png(self, rel: str, pear: bool = False, variant: int = 0) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (9, 8, 7, 0))
        draw = ImageDraw.Draw(image)
        draw.rectangle((106, 116, 149, 236), fill=(90, 80, 70, 255))
        draw.rectangle((104, 150, 107, 153), fill=(90, 80, 70, 255))
        draw.rectangle((148, 150, 151, 153), fill=(90, 80, 70, 255))
        if pear:
            draw.rectangle((96, 200, 159, 236), fill=(90, 80, 70, 255))
        if variant:
            draw.point((110 + variant, 120), fill=(90 + variant, 80, 70, 255))
        image.save(path)
        return path

    def mask(self, rel: str, pear: bool = False, contact: int = 4) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        draw.rectangle((108, 116, 147, 236), fill=pipeline.MASK_COLORS["core"])
        if pear:
            draw.rectangle((98, 198, 157, 236), fill=pipeline.MASK_COLORS["core"])
        draw.rectangle((104, 150, 107, 153), fill=pipeline.MASK_COLORS["near_hand"])
        draw.rectangle((148, 150, 151, 153), fill=pipeline.MASK_COLORS["far_hand"])
        if contact == 1:
            draw.rectangle((104, 150, 107, 153), fill=(0, 0, 0, 0))
            draw.point((107, 150), fill=pipeline.MASK_COLORS["near_hand"])
        draw.rectangle((108, 233, 115, 236), fill=pipeline.MASK_COLORS["near_foot"])
        draw.rectangle((140, 233, 147, 236), fill=pipeline.MASK_COLORS["far_foot"])
        image.save(path)
        return path

    def identity_mask(self, rel: str, diamond: bool = False) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        if diamond:
            draw.polygon(((128, 108), (136, 116), (128, 124), (120, 116)), fill=pipeline.IDENTITY_MASK_COLORS["forehead_blaze"])
        else:
            draw.rectangle((119, 108, 137, 116), fill=pipeline.IDENTITY_MASK_COLORS["forehead_blaze"])
        draw.rectangle((112, 82, 120, 102), fill=pipeline.IDENTITY_MASK_COLORS["alternate_ear"])
        draw.rectangle((128, 132, 147, 210), fill=pipeline.IDENTITY_MASK_COLORS["alternate_coat"])
        image.save(path)
        return path

    def ns(self, **values):
        return argparse.Namespace(**values)

    def art_direction_fixture(self):
        authored = self.root / "Tools/artworks/pure_run/art_direction"
        authored.mkdir(parents=True, exist_ok=True)
        sources = {
            "project.json": {"schemaVersion": 1, "profileKind": "project-art-direction", "profileId": "project-v1", "rules": []},
            "material.json": {"schemaVersion": 1, "profileKind": "material-language", "profileId": "material-v1", "materials": []},
            "actor.json": {"schemaVersion": 1, "profileKind": "family", "profileId": "actor-v1", "family": "actor", "rules": []},
            "brief.json": {"schemaVersion": 1, "briefKind": "asset", "briefId": "brief-v1", "family": "actor",
                           "requiredReviewPanels": ["candidate-master"], "acceptanceCases": [{"caseId": "CASE-001"}]},
        }
        paths = {}
        for name, value in sources.items():
            path = authored / name
            path.write_text(json.dumps(value), encoding="utf-8")
            paths[name] = path
        manifest = {
            "schemaVersion": 1, "manifestKind": "art-direction-manifest", "manifestId": "manifest-v1",
            "projectProfile": {"profileId": "project-v1", "path": self.store.relative(paths["project.json"]),
                               "sha256": pipeline.sha256_file(paths["project.json"])},
            "materialLanguage": {"profileId": "material-v1", "path": self.store.relative(paths["material.json"]),
                                 "sha256": pipeline.sha256_file(paths["material.json"])},
            "familyProfiles": [{"profileId": "actor-v1", "family": "actor", "path": self.store.relative(paths["actor.json"]),
                                "sha256": pipeline.sha256_file(paths["actor.json"])}],
            "briefTemplates": [],
        }
        manifest_path = authored / "manifest.json"
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        pipeline.register_art_direction_profile(self.store, self.ns(source=str(paths["project.json"])))
        pipeline.register_material_language(self.store, self.ns(source=str(paths["material.json"])))
        pipeline.register_family_profile(self.store, self.ns(source=str(paths["actor.json"])))
        pipeline.create_asset_brief(self.store, self.ns(source=str(paths["brief.json"])))
        pipeline.register_art_direction_manifest(self.store, self.ns(source=str(manifest_path)))
        return paths

    def contract_and_job(self):
        anchor = self.png("Tools/artworks/approved/anchor.png")
        anchor_mask = self.mask("Tools/artworks/masks/anchor.png")
        inventory = {"schemaVersion": 1, "assets": [{
            "path": "Tools/artworks/approved/anchor.png", "sha256": pipeline.sha256_file(anchor),
            "state": "legacy-approved", "lineage": None,
        }, {
            "path": "Tools/artworks/masks/anchor.png", "sha256": pipeline.sha256_file(anchor_mask),
            "state": "legacy-unresolved", "lineage": None,
        }]}
        pipeline.write_json_idempotent(self.store.pipeline / "legacy-assets.json", inventory)
        anchor_receipt = {
            "schemaVersion": 1, "approvalId": "approval-bootstrap", "attemptId": "bootstrap-anchor",
            "candidateSha256": pipeline.sha256_file(anchor), "maskSha256": pipeline.sha256_file(anchor_mask),
            "reviewer": "cty41", "decision": "approved", "reason": "fixture bootstrap",
            "decidedAt": "2026-08-18T00:00:00+08:00",
        }
        pipeline.write_json_idempotent(self.store.record("approvals", "approval-bootstrap"), anchor_receipt)
        prompt = self.root / "Tools/artworks/prompts/job.md"
        prompt.parent.mkdir(parents=True); prompt.write_text("fixed prompt", encoding="utf-8")
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="hero", approved_asset_id=None, kind="ground_character", direction="down-right", pose="idle",
            anchor=str(anchor), anchor_mask=str(anchor_mask), mask_required=True, no_arms=True,
            near_hand_side="left", far_hand_side="right",
            size_tolerance=3, center_tolerance=2,
            output_master="Tools/artworks/approved/hero.png", output_preview="Tools/artworks/approved/hero_128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
        job_args = self.ns(contract_id=contract["contractId"], prompt=str(prompt), input=[f"core_anchor={anchor}"])
        return contract, pipeline.create_job(self.store, job_args), job_args

    def bind_series(self, job_args, poses=None, limit=5):
        poses = poses or ["idle-dr", "idle-ul"]
        series = pipeline.create_series(self.store, self.ns(
            series_id="demonbound-series", asset_id="demonbound", pose=poses, max_unique_outputs=limit))
        job_args.series_id = series["seriesId"]
        job_args.pose_id = poses[0]
        return series, pipeline.create_job(self.store, job_args)

    def occlusion_contract_and_job(self, equipment_cap: float = 0.20):
        _, _, job_args = self.contract_and_job()
        anchor = self.root / "Tools/artworks/approved/anchor.png"
        anchor_mask = self.root / "Tools/artworks/masks/anchor.png"
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="hero-ul", approved_asset_id="hero-ul", kind="ground_character", direction="up-left", pose="idle",
            anchor=str(anchor), anchor_mask=str(anchor_mask), mask_required=True, no_arms=True,
            near_hand_side="left", far_hand_side="right", size_tolerance=3, center_tolerance=2,
            layer_rule=["near_hand=behind-core", "far_hand=behind-core", "equipment=behind-core"],
            visibility_cap=["near_hand=0.05", "far_hand=0.05", f"equipment={equipment_cap}"],
            output_master="Tools/artworks/approved/hero-ul.png",
            output_preview="Tools/artworks/approved/hero-ul-128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
        job_args.contract_id = contract["contractId"]
        job_args.series_id = None; job_args.pose_id = None
        return contract, pipeline.create_job(self.store, job_args)

    def occlusion_mask(self, rel: str, intrusion: bool = False) -> Path:
        path = self.mask(rel)
        image = Image.open(path).convert("RGBA")
        draw = ImageDraw.Draw(image)
        if intrusion:
            draw.rectangle((120, 160, 125, 190), fill=pipeline.MASK_COLORS["equipment"])
        else:
            draw.rectangle((100, 160, 107, 175), fill=pipeline.MASK_COLORS["equipment"])
        image.save(path)
        return path

    def process_series_attempt(self, job, variant=0, verdict="retry"):
        attempts = pipeline.list_attempts(self.store, job["jobId"])
        feedback_id = attempts[-1].get("feedbackId") if attempts else None
        attempt = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=attempts[-1]["attemptId"] if attempts else None,
            feedback_id=feedback_id))
        raw = self.png(f"incoming/{attempt['attemptId']}.png", variant=variant)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        mask = self.mask(f"incoming/{attempt['attemptId']}-mask.png")
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertTrue(report["passed"], report["issues"])
        pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        feedback = pipeline.record_feedback(self.store, self.ns(
            attempt_id=attempt["attemptId"], reviewer="cty41", verdict=verdict,
            strength=["contract geometry retained"], defect=[] if verdict == "selected" else ["needs another visual option"],
            next_prompt_delta="preserve geometry and vary art details", recorded_at="2026-08-18T00:00:00+08:00"))
        return attempt, feedback

    def process_core_size_failure(self, job, *, add_chroma=False, render_review=True):
        attempts = pipeline.list_attempts(self.store, job["jobId"])
        feedback_id = attempts[-1].get("feedbackId") if attempts else None
        attempt = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=attempts[-1]["attemptId"] if attempts else None,
            feedback_id=feedback_id))
        raw = self.png(f"incoming/{attempt['attemptId']}-wide.png")
        image = Image.open(raw).convert("RGBA")
        draw = ImageDraw.Draw(image)
        draw.rectangle((100, 116, 155, 236), fill=(90, 80, 70, 255))
        draw.rectangle((96, 150, 99, 153), fill=(90, 80, 70, 255))
        draw.rectangle((156, 150, 159, 153), fill=(90, 80, 70, 255))
        if add_chroma:
            draw.point((127, 140), fill=(0, 255, 0, 255))
        image.save(raw)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        mask_path = self.root / f"incoming/{attempt['attemptId']}-wide-mask.png"
        mask_path.parent.mkdir(parents=True, exist_ok=True)
        mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        mask_draw = ImageDraw.Draw(mask)
        mask_draw.rectangle((100, 116, 155, 236), fill=pipeline.MASK_COLORS["core"])
        mask_draw.rectangle((96, 150, 99, 153), fill=pipeline.MASK_COLORS["near_hand"])
        mask_draw.rectangle((156, 150, 159, 153), fill=pipeline.MASK_COLORS["far_hand"])
        mask_draw.rectangle((100, 233, 107, 236), fill=pipeline.MASK_COLORS["near_foot"])
        mask_draw.rectangle((148, 233, 155, 236), fill=pipeline.MASK_COLORS["far_foot"])
        mask.save(mask_path)
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask_path)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        if render_review:
            pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        feedback = pipeline.record_feedback(self.store, self.ns(
            attempt_id=attempt["attemptId"], reviewer="codex", verdict="technical_failed",
            strength=["visual candidate retained"], defect=["core width exceeds anchor"],
            next_prompt_delta="retain visual candidate", recorded_at="2026-08-18T00:00:00+08:00"))
        return attempt, report, feedback

    def v2_composition(self):
        anchor = self.png("Tools/artworks/approved/v2-anchor.png")
        spec_path = self.root / "Tools/artworks/specs/action.json"
        spec_path.parent.mkdir(parents=True, exist_ok=True)
        spec = {
            "canvas": [256, 256],
            "coreAxis": {"bottom": [127, 236], "top": [127, 116], "tiltDegrees": [-3, 3]},
            "footCenter": [127, 236],
            "weapon": {"hiddenGrip": [127, 175], "exitWindow": [150, 140, 175, 190],
                       "tipRegion": [175, 90, 230, 160], "maxGemAreaPx": 36},
            "forbiddenRegions": [{"name": "eyes", "rect": [105, 125, 150, 155]}],
            "equipmentState": {"scabbard": "absent"}
        }
        spec_path.write_text(json.dumps(spec), encoding="utf-8")
        return pipeline.create_composition(self.store, self.ns(
            asset_id="hero-action", spec=str(spec_path), anchor=str(anchor)))

    def v3_pose_proof_composition(self, gate="decision"):
        anchor = self.png("Tools/artworks/approved/v3-pose-anchor.png")
        example = Path(pipeline.pose_proof.__file__).resolve().parents[1] / "examples" / "pose-proof-cast-dr-v1.json"
        draft = json.loads(example.read_text(encoding="utf-8")); draft.pop("historicalEvidence", None)
        draft["assetId"] = f"hero-{gate}"
        card = pipeline.pose_proof.select_option(
            draft, "B", "cty41", "compact cast silhouette", {"A": "too open", "C": "too tall"},
            "2026-09-15T10:00:00+08:00")
        card_path = self.root / f"Tools/artworks/hero/pose-proofs/cast-{gate}.json"
        pipeline.pose_proof.write_card(card, card_path)
        spec = {
            "schemaVersion": 3, "poseProofContext": draft["visualMoment"], "canvas": [256, 256],
            "coreAxis": {"bottom": [127, 236], "top": [127, 116], "tiltDegrees": [-3, 3]},
            "footCenter": [127, 236],
            "weapon": {"hiddenGrip": [127, 175], "exitWindow": [150, 140, 175, 190],
                       "tipRegion": [175, 90, 230, 160], "maxGemAreaPx": 36},
            "forbiddenRegions": [], "equipmentState": {"scabbard": "present"},
        }
        if gate in {"decision", "both"}:
            spec["poseProofDecision"] = {"path": self.store.relative(card_path), "sha256": pipeline.sha256_file(card_path),
                                         "poseProofId": card["poseProofId"]}
        if gate in {"exemption", "both"}:
            contract_payload = {"assetId": f"hero-{gate}", "pose": "cast", "direction": "down-right"}
            source_contract_id = pipeline.stable_id("contract", contract_payload)
            source_contract_path = self.store.record("contracts", source_contract_id)
            pipeline.write_json_idempotent(source_contract_path, {"schemaVersion": 1, "contractId": source_contract_id,
                                                                   **contract_payload})
            job_payload = {"contractId": source_contract_id, "contractSha256": pipeline.sha256_file(source_contract_path),
                           "prompt": None, "inputs": [],
                           "target": {"direction": "down-right", "pose": "cast"}, "series": None,
                           "conceptOnly": False, "contractRequirements": None, "requiresInvocation": False,
                           "poseGuide": None, "localReferences": []}
            job_id = pipeline.stable_id("job", job_payload)
            pipeline.write_json_idempotent(self.store.record("jobs", job_id), {
                "schemaVersion": 1, "jobId": job_id, "state": "ready", **job_payload})
            attempt_id = f"fixture-{gate}"
            candidate = {"path": self.store.relative(anchor), "sha256": pipeline.sha256_file(anchor)}
            approval_payload = {"attemptId": attempt_id, "candidateSha256": candidate["sha256"], "maskSha256": None,
                                "reviewer": "cty41", "decision": "approved", "reason": "fixture approved pose",
                                "decidedAt": "2026-09-15T09:00:00+08:00"}
            approval_id = pipeline.stable_id("approval", approval_payload)
            approval_path = self.store.record("approvals", approval_id)
            approval = {"schemaVersion": 1, "approvalId": approval_id, **approval_payload}
            pipeline.write_json_idempotent(approval_path, approval)
            pipeline.write_json_idempotent(self.store.record("attempts", attempt_id), {
                "schemaVersion": 3, "attemptId": attempt_id, "state": "promoted", "approvalId": approval_id,
                "jobId": job_id, "artifacts": {"prepared": candidate}})
            exemption = pipeline.create_pose_proof_exemption(self.store, self.ns(
                asset_id=f"hero-{gate}", pose="cast", direction="down-right",
                consumer=draft["visualMoment"]["consumer"], phase=draft["visualMoment"]["phase"],
                sprite_role=draft["visualMoment"]["spriteRole"], category="existing-approved-pose",
                approval_id=[approval["approvalId"]], reviewer="cty41", reason="assembly retains an approved pose",
                decided_at="2026-09-15T10:00:00+08:00"))
            exemption_path = self.store.record("pose-proof-exemptions", exemption["exemptionId"])
            spec["poseProofExemption"] = {"path": self.store.relative(exemption_path),
                                           "sha256": pipeline.sha256_file(exemption_path),
                                           "exemptionId": exemption["exemptionId"]}
        spec_path = self.root / f"Tools/artworks/specs/v3-{gate}.json"
        spec_path.parent.mkdir(parents=True, exist_ok=True); spec_path.write_text(json.dumps(spec), encoding="utf-8")
        return pipeline.create_composition(self.store, self.ns(asset_id=f"hero-{gate}", spec=str(spec_path), anchor=str(anchor)))

    def action_contract_for_composition(self, composition):
        anchor = self.store.absolute(composition["anchor"]["path"])
        inventory_path = self.store.pipeline / "legacy-assets.json"
        inventory_path.write_text(json.dumps({"schemaVersion": 1, "assets": [{
            "path": composition["anchor"]["path"], "sha256": pipeline.sha256_file(anchor),
            "state": "legacy-approved", "lineage": None}]}), encoding="utf-8")
        return pipeline.create_contract(self.store, self.ns(
            asset_id=composition["assetId"], approved_asset_id=None, kind="action_pose", direction="down-right", pose="cast",
            anchor=str(anchor), anchor_mask=None, mask_required=False, no_arms=True,
            near_hand_side="left", far_hand_side="right", size_tolerance=6, center_tolerance=4,
            layer_rule=[], visibility_cap=[], composition_id=composition["compositionId"], identity_anchor_mask=None,
            forehead_blaze_min_iou=0.45, pose_reference=True,
            output_master="Tools/artworks/approved/hero-cast.png", output_preview="Tools/artworks/approved/hero-cast-128.png",
            rights_holder="cty41", license="project-owned", provenance="project-owned-gpt-generated",
            asset_role=None, component_kind=None, source_mode=None))

    def test_v1_and_v2_records_load_without_rewriting_v1(self):
        v1_path = self.store.record("contracts", "legacy")
        pipeline.write_json_idempotent(v1_path, {"schemaVersion": 1, "contractId": "legacy"})
        before = v1_path.read_bytes()
        self.assertEqual(1, pipeline.load_json(v1_path)["schemaVersion"])
        composition = self.v2_composition()
        self.assertEqual(2, composition["schemaVersion"])
        self.assertEqual(before, v1_path.read_bytes())

    def test_v3_pose_proof_decision_and_exemption_gate_action_contracts(self):
        decision = self.v3_pose_proof_composition("decision")
        self.assertEqual(3, decision["schemaVersion"])
        self.assertEqual("action_pose", self.action_contract_for_composition(decision)["kind"])
        exemption = self.v3_pose_proof_composition("exemption")
        self.assertEqual("action_pose", self.action_contract_for_composition(exemption)["kind"])
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly one"):
            self.v3_pose_proof_composition("both")
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly one"):
            self.v3_pose_proof_composition("missing")
        decision_spec = json.loads(self.store.absolute(decision["source"]["path"]).read_text(encoding="utf-8"))
        with self.assertRaisesRegex(pipeline.PipelineError, "asset"):
            pipeline.create_composition(self.store, self.ns(asset_id="other-asset", spec=decision["source"]["path"],
                                                            anchor=str(self.store.absolute(decision["anchor"]["path"]))))
        example = Path(pipeline.pose_proof.__file__).resolve().parents[1] / "examples" / "pose-proof-cast-dr-v1.json"
        draft = json.loads(example.read_text(encoding="utf-8")); draft.pop("historicalEvidence", None)
        draft["assetId"] = decision["assetId"]
        unauthorized = pipeline.pose_proof._core.select_option(
            draft, "B", "agent", "selected", {"A": "no", "C": "no"}, "2026-09-15T10:00:00+08:00")
        unauthorized_path = self.root / "Tools/artworks/hero/pose-proofs/cast-unauthorized.json"
        unauthorized_path.write_text(json.dumps(unauthorized), encoding="utf-8")
        unauthorized_spec = json.loads(json.dumps(decision_spec))
        unauthorized_spec["poseProofDecision"] = {
            "path": self.store.relative(unauthorized_path), "sha256": pipeline.sha256_file(unauthorized_path),
            "poseProofId": unauthorized["poseProofId"],
        }
        unauthorized_spec_path = self.root / "Tools/artworks/specs/v3-unauthorized-reviewer.json"
        unauthorized_spec_path.write_text(json.dumps(unauthorized_spec), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer cty41"):
            pipeline.create_composition(self.store, self.ns(
                asset_id=decision["assetId"], spec=str(unauthorized_spec_path),
                anchor=str(self.store.absolute(decision["anchor"]["path"]))))
        bare_exemption = json.loads(self.store.absolute(decision["source"]["path"]).read_text(encoding="utf-8"))
        bare_exemption.pop("poseProofDecision")
        bare_exemption["poseProofExemption"] = {"category": "existing-approved-pose", "reviewer": "cty41",
                                                  "reason": "bare image bypass", "decidedAt": "2026-09-15T10:00:00+08:00",
                                                  "evidence": [decision["anchor"]]}
        bare_path = self.root / "Tools/artworks/specs/v3-bare-exemption.json"
        bare_path.write_text(json.dumps(bare_exemption), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "binding is invalid"):
            pipeline.create_composition(self.store, self.ns(asset_id=decision["assetId"], spec=str(bare_path),
                                                            anchor=str(self.store.absolute(decision["anchor"]["path"]))))
        decision_spec["poseProofContext"]["phase"] = "wrong-phase"
        mismatch_path = self.root / "Tools/artworks/specs/v3-context-mismatch.json"
        mismatch_path.write_text(json.dumps(decision_spec), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "Visual Moment"):
            pipeline.create_composition(self.store, self.ns(asset_id=decision["assetId"], spec=str(mismatch_path),
                                                            anchor=str(self.store.absolute(decision["anchor"]["path"]))))
        card_path = self.root / decision["spec"]["poseProofDecision"]["path"]
        broken = json.loads(card_path.read_text(encoding="utf-8")); broken["selection"]["reason"] = "tampered"
        card_path.write_text(json.dumps(broken), encoding="utf-8")
        for fixture_attempt in (self.store.pipeline / "attempts").glob("fixture-*.json"):
            fixture_attempt.unlink()
        issues = pipeline.strict_check(self.store, False)["issues"]
        self.assertIn(f"composition_pose_proof_invalid:{decision['compositionId']}", issues)
        self.assertIn("pose_proof_card_invalid:Tools/artworks/hero/pose-proofs/cast-decision.json", issues)

    def test_new_v2_action_composition_cannot_bypass_pose_proof(self):
        composition = self.v2_composition()
        with self.assertRaisesRegex(pipeline.PipelineError, "schema v3 Pose Proof"):
            self.action_contract_for_composition(composition)
        grandfather = {"schemaVersion": 1, "cutoffRevision": "d48b70ba969386620c0671f5c8ff19ab59d2ab79",
                       "authorizedBy": "cty41", "decidedAt": "2026-09-15T10:00:00+08:00",
                       "reason": "fixture freezes a pre-gate composition", "entries": [{
                           "compositionId": composition["compositionId"],
                           "sha256": pipeline.sha256_file(self.store.record("compositions", composition["compositionId"]))}]}
        (self.store.pipeline / "pose-proof-grandfathers.json").write_text(json.dumps(grandfather), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "schema v3 Pose Proof"):
            self.action_contract_for_composition(composition)
        self.assertIn("pose_proof_grandfather_registry_invalid", pipeline.strict_check(self.store, False)["issues"])

    def test_immutable_json_publish_is_collision_safe_under_concurrency(self):
        path = self.root / "Tools/artworks/pipeline/pose-proof-exemptions/race.json"
        values = ({"schemaVersion": 1, "value": "A"}, {"schemaVersion": 1, "value": "B"})
        def publish(value):
            try:
                pipeline.write_json_idempotent(path, value, immutable=True); return "ok"
            except pipeline.PipelineError:
                return "collision"
        with ThreadPoolExecutor(max_workers=2) as pool:
            outcomes = list(pool.map(publish, values))
        self.assertEqual(["collision", "ok"], sorted(outcomes))
        self.assertIn(json.loads(path.read_text(encoding="utf-8")), values)

    def test_strict_reports_malformed_composition_without_traceback(self):
        malformed = self.store.record("compositions", "composition-malformed")
        malformed.parent.mkdir(parents=True, exist_ok=True); malformed.write_text("{", encoding="utf-8")
        contract_id = "contract-malformed-composition"
        pipeline.write_json_idempotent(self.store.record("contracts", contract_id), {
            "schemaVersion": 3, "contractId": contract_id, "kind": "action_pose", "assetId": "broken",
            "compositionSpec": {"compositionId": "composition-malformed", "sha256": pipeline.sha256_file(malformed)}})
        issues = pipeline.strict_check(self.store, False)["issues"]
        self.assertIn("composition_record_invalid:composition-malformed", issues)
        self.assertIn("contract_composition_invalid:contract-malformed-composition", issues)
        with self.assertRaisesRegex(pipeline.PipelineError, "composition_record_invalid:composition-malformed"):
            pipeline.strict_check(self.store, True)

    def test_pose_guide_is_deterministic_and_bound_to_composition(self):
        composition = self.v2_composition()
        args = self.ns(composition_id=composition["compositionId"], output="Tools/artworks/guides/action.png")
        first = pipeline.render_pose_guide(self.store, args)
        second = pipeline.render_pose_guide(self.store, args)
        self.assertEqual(first, second)
        self.assertEqual("supporting-derived", first["role"])

    def test_pose_guide_renders_validated_action_design(self):
        anchor = self.png("Tools/artworks/approved/action-anchor.png")
        spec_path = self.root / "Tools/artworks/specs/action-design.json"
        spec_path.parent.mkdir(parents=True, exist_ok=True)
        spec_path.write_text(json.dumps({
            "canvas": [256, 256],
            "coreAxis": {"bottom": [122, 232], "top": [148, 116], "tiltDegrees": [10, 18]},
            "footCenter": [128, 236],
            "weapon": {"hiddenGrip": [150, 158], "exitWindow": [140, 145, 165, 175], "tipRegion": [198, 202, 242, 238]},
            "forbiddenRegions": [],
            "equipmentState": {"scabbard": "present"},
            "actionDesign": {
                "silhouette": [[95, 216], [102, 145], [132, 112], [166, 145], [171, 205], [145, 229]],
                "lineOfAction": [[112, 224], [151, 119]],
                "centerOfMass": [137, 177],
                "pawContactZones": {
                    "nearHand": [[153, 154], [166, 158], [164, 171], [151, 168]],
                    "farHand": [[142, 158], [153, 160], [151, 172], [140, 169]],
                    "nearFoot": [[108, 225], [122, 224], [124, 236], [108, 236]],
                    "farFoot": [[137, 222], [151, 224], [153, 236], [138, 236]]
                },
                "compressionLine": [[105, 190], [137, 177], [164, 161]],
                "counterbalanceLine": [[120, 166], [91, 143], [72, 126]]
            }
        }), encoding="utf-8")
        composition = pipeline.create_composition(self.store, self.ns(
            asset_id="hero-action-design", spec=str(spec_path), anchor=str(anchor)))
        guide = pipeline.render_pose_guide(self.store, self.ns(
            composition_id=composition["compositionId"], output="Tools/artworks/guides/action-design.png"))
        rendered = Image.open(self.store.absolute(guide["artifact"]["path"])).convert("RGBA")
        self.assertGreater(rendered.getpixel((137, 177))[3], 0)
        self.assertGreater(rendered.getpixel((116, 233))[2], 150)
        detached = json.loads(spec_path.read_text(encoding="utf-8"))
        detached["weapon"]["hiddenGrip"] = None
        detached["forbiddenRegions"] = [[0, 0, 10, 10]]
        spec_path.write_text(json.dumps(detached), encoding="utf-8")
        detached_composition = pipeline.create_composition(self.store, self.ns(
            asset_id="detached-weapon-action-design", spec=str(spec_path), anchor=str(anchor)))
        detached_guide = pipeline.render_pose_guide(self.store, self.ns(
            composition_id=detached_composition["compositionId"], output="Tools/artworks/guides/detached-action-design.png"))
        self.assertTrue(self.store.absolute(detached_guide["artifact"]["path"]).exists())
        broken = json.loads(spec_path.read_text(encoding="utf-8"))
        broken["actionDesign"]["centerOfMass"] = [300, 177]
        spec_path.write_text(json.dumps(broken), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "invalid point"):
            pipeline.create_composition(self.store, self.ns(
                asset_id="broken-action-design", spec=str(spec_path), anchor=str(anchor)))

    def test_guard_gem_window_rejects_tip_pommel_missing_extra_and_oversized_gems(self):
        weapon = {
            "gemWindow": [122, 168, 132, 178],
            "forbiddenGemRegions": [[120, 76, 135, 101], [122, 184, 132, 198]],
            "maxGemAreaPx": 36,
        }
        self.assertEqual([], pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 170, 128, 173]}))
        self.assertIn("gem_missing", pipeline.composition_gem_issues(weapon, {}))
        self.assertIn("gem_outside_guard_window", pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 82, 128, 85]}))
        self.assertIn("gem_enters_forbidden_region", pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 82, 128, 85]}))
        self.assertIn("gem_outside_guard_window", pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 188, 128, 191]}))
        self.assertIn("extra_gem_present", pipeline.composition_gem_issues(weapon, {
            "gemRegion": [125, 170, 128, 173], "extraGemRegions": [[125, 82, 128, 85]],
        }))
        self.assertIn("gem_too_large", pipeline.composition_gem_issues(weapon, {"gemRegion": [122, 168, 128, 174]}))

    def test_blade_length_and_width_range_rejects_idle_ratio_drift(self):
        weapon = {"bladeCenterline": [123, 105, 132, 165], "minBladeWidthPx": 7, "maxBladeWidthPx": 9, "bladeLengthRangePx": [55, 65]}
        self.assertEqual([], pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 106, 131, 165]}))
        self.assertIn("weapon_blade_too_thin", pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 106, 129, 165]}))
        self.assertIn("weapon_blade_too_short", pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 120, 131, 160]}))
        self.assertIn("weapon_blade_too_long", pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 80, 131, 165]}))
        self.assertIn("weapon_blade_too_wide", pipeline.composition_blade_issues(weapon, {"weaponBlade": [120, 106, 131, 165]}))

    def test_eye_occlusion_requires_a_narrow_blade_to_overlap_both_inner_eyes(self):
        spec = {"eyeOcclusion": {"bladeOverlapsBothInnerEyes": True, "maxEyeCenterGapPx": 28}}
        regions = {"weaponBlade": [124, 125, 132, 150], "leftEyeRegion": [108, 128, 127, 146], "rightEyeRegion": [129, 128, 148, 146]}
        self.assertEqual([], pipeline.composition_eye_occlusion_issues(spec, regions))
        too_wide = dict(regions); too_wide["rightEyeRegion"] = [145, 128, 164, 146]
        self.assertIn("blade_does_not_occlude_both_inner_eyes", pipeline.composition_eye_occlusion_issues(spec, too_wide))
        self.assertIn("eye_center_gap_too_wide", pipeline.composition_eye_occlusion_issues(spec, too_wide))

    def test_generation_failure_does_not_create_raw_output(self):
        invocation = {"schemaVersion": 2, "invocationId": "generation-invocation-fixture",
                      "attemptId": "attempt-fixture", "state": "started"}
        pipeline.write_json_idempotent(self.store.record("generation-invocations", invocation["invocationId"]), invocation)
        failure = pipeline.record_generation_failure(self.store, self.ns(
            invocation_id=invocation["invocationId"], reason="delivery lost",
            failed_at="2026-08-18T00:00:00+08:00"))
        self.assertEqual("attempt-fixture", failure["attemptId"])
        self.assertFalse((self.store.pipeline / "attempts/attempt-fixture.json").exists())

    def test_feedback_v2_separates_author_and_backup_disposition(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/feedback-v2.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        feedback = pipeline.record_feedback(self.store, self.ns(
            attempt_id=attempt["attemptId"], reviewer="cty41", author_type="human",
            verdict="backup", category=["pose_axis"], strength=["viable fallback"],
            defect=["not selected"], frozen=["identity"], pending=["pose"],
            next_prompt_delta="", recorded_at="2026-08-18T00:00:00+08:00"))
        self.assertEqual("human", feedback["authorType"])
        self.assertEqual("backup", feedback["disposition"])

    def test_canonical_job_id_and_retry_numbering(self):
        _, job, args = self.contract_and_job()
        self.assertEqual(job, pipeline.create_job(self.store, args))
        first = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        second = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=first["attemptId"]))
        self.assertTrue(first["attemptId"].endswith("a001"))
        self.assertTrue(second["attemptId"].endswith("a002"))

    def test_path_escape_rejected(self):
        outside = self.root.parent / "outside.png"
        with self.assertRaises(pipeline.PipelineError):
            self.store.relative(outside)

    def test_bootstrap_anchor_receipt_binds_candidate_mask_and_review(self):
        self.contract_and_job()
        review = self.png("Tools/artworks/reviews/anchor-review.png")
        receipt = pipeline.approve_anchor(self.store, self.ns(
            candidate="Tools/artworks/approved/anchor.png", mask="Tools/artworks/masks/anchor.png",
            review=str(review), reviewer="cty41", reason="fixture review",
            decided_at="2026-08-18T00:00:00+08:00"))
        self.assertEqual(pipeline.sha256_file(review), receipt["reviewSha256"])
        self.assertTrue(pipeline.approved_mask_pair(
            self.store, receipt["candidateSha256"], receipt["maskSha256"]))
        self.assertEqual(
            {"path": receipt["maskPath"], "sha256": receipt["maskSha256"]},
            pipeline.approved_anchor_mask(self.store, receipt["candidateSha256"]))
        evidence = pipeline.core_size_exception_evidence(
            self.store,
            {"geometry": {"core": {"bbox": [100, 100, 149, 220]}}},
            {
                "anchor": {"path": receipt["candidatePath"], "sha256": receipt["candidateSha256"]},
                "tolerances": {"sizePx": 3},
            },
        )
        self.assertEqual([40, 121], evidence["anchorCoreSize"])
        self.assertEqual([10, 0], evidence["delta"])

    def test_anchor_core_bbox_accepts_approved_binary_mask(self):
        mask = Image.new("RGBA", (32, 32), (0, 0, 0, 255))
        ImageDraw.Draw(mask).ellipse((8, 6, 23, 25), fill=(255, 255, 255, 255))
        self.assertEqual((8, 6, 23, 25), pipeline.anchor_core_bbox(mask))

    def test_compile_prompt_uses_bat_invariants_for_tomb_maw_bat(self):
        self.contract_and_job()
        anchor = self.root / "Tools/artworks/approved/anchor.png"
        spec_path = self.root / "Tools/artworks/specs/bat-action.json"
        spec_path.parent.mkdir(parents=True)
        example = Path(pipeline.pose_proof.__file__).resolve().parents[1] / "examples" / "pose-proof-cast-dr-v1.json"
        draft = json.loads(example.read_text(encoding="utf-8")); draft.pop("historicalEvidence", None)
        draft.update({"assetId": "tomb-maw-bat-melee", "characterId": "tomb-maw-bat", "poseId": "melee"})
        card = pipeline.pose_proof.select_option(draft, "B", "cty41", "fixture pose", {"A": "no", "C": "no"},
                                                 "2026-09-15T10:00:00+08:00")
        card_path = self.root / "Tools/artworks/tomb-maw-bat/pose-proofs/melee.json"
        pipeline.pose_proof.write_card(card, card_path)
        spec_path.write_text(json.dumps({
            "schemaVersion": 3, "poseProofContext": draft["visualMoment"],
            "poseProofDecision": {"path": self.store.relative(card_path), "sha256": pipeline.sha256_file(card_path),
                                  "poseProofId": card["poseProofId"]},
            "canvas": [256, 256],
            "coreAxis": {"top": [128, 90], "bottom": [128, 180], "tiltDegrees": [-8, 8]},
            "footCenter": [128, 236],
            "weapon": {"hiddenGrip": [128, 145], "exitWindow": [100, 120, 156, 182], "tipRegion": [100, 130, 156, 190]},
            "forbiddenRegions": [],
            "equipmentState": {"scabbard": "absent", "staticEffects": "absent"},
        }), encoding="utf-8")
        composition = pipeline.create_composition(self.store, self.ns(
            asset_id="tomb-maw-bat-melee", spec=str(spec_path), anchor=str(anchor)))
        guide = pipeline.render_pose_guide(self.store, self.ns(
            composition_id=composition["compositionId"], output="Tools/artworks/reviews/bat-guide.png"))
        prompt = self.root / "Tools/artworks/prompts/bat.md"
        prompt.parent.mkdir(parents=True, exist_ok=True); prompt.write_text("bite", encoding="utf-8")
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="tomb-maw-bat-melee", approved_asset_id="tomb-maw-bat", kind="action_pose",
            direction="down-right", pose="melee", anchor=str(anchor), anchor_mask=None,
            mask_required=False, no_arms=False, near_hand_side=None, far_hand_side=None,
            size_tolerance=8, center_tolerance=2, layer_rule=[], visibility_cap=[],
            composition_id=composition["compositionId"], identity_anchor_mask=None,
            forehead_blaze_min_iou=0.45, pose_reference=True,
            output_master="Tools/artworks/approved/bat-melee.png",
            output_preview="Tools/artworks/approved/bat-melee_128.png", rights_holder="cty41",
            license="project-owned", provenance="project-owned-gpt-generated",
            asset_role=None, component_kind=None, source_mode=None))
        job = pipeline.create_job(self.store, self.ns(
            contract_id=contract["contractId"], prompt=str(prompt), input=[f"mother_anchor={anchor}"],
            pose_guide_id=guide["poseGuideId"], series_id=None, pose_id=None))
        compiled = pipeline.compile_prompt(self.store, self.ns(
            job_id=job["jobId"], pose_guide_id=guide["poseGuideId"], output="Tools/artworks/reviews/bat-prompt.md"))
        text = self.store.absolute(compiled["artifact"]["path"]).read_text(encoding="utf-8")
        self.assertIn("near-round spherical flying core", text)
        self.assertNotIn("exactly four paws", text)

    def test_v4_compile_prompt_uses_brief_invariants_and_role_bindings(self):
        composition = self.v2_composition()
        guide = pipeline.render_pose_guide(self.store, self.ns(
            composition_id=composition["compositionId"], output="Tools/artworks/reviews/v4-guide.png"))
        anchor = self.root / "Tools/artworks/approved/v2-anchor.png"
        prompt = self.root / "Tools/artworks/prompts/poet-v4.md"
        prompt.parent.mkdir(parents=True, exist_ok=True)
        prompt.write_text("one coherent poet action sprite", encoding="utf-8")
        brief_path = self.root / "Tools/artworks/briefs/poet-v4.json"
        brief_path.parent.mkdir(parents=True, exist_ok=True)
        role = "image-1-poet-identity-only"
        brief = {
            "schemaVersion": 1, "briefKind": "asset", "briefId": "poet-v4", "family": "actor",
            "purpose": "Show the poet committing to a melee impact.",
            "firstRead": ["poet", "impact"],
            "identityInvariants": ["approved five-red chow face", "exactly four attached paws", "no clothing or drinking vessel"],
            "forbidden": ["generic gray-white forehead blaze", "heterochromic ear"],
            "referenceResponsibilities": [{"role": role, "path": self.store.relative(anchor),
                "sha256": pipeline.sha256_file(anchor), "responsibility": "identity and volume only"}],
        }
        brief_path.write_text(json.dumps(brief), encoding="utf-8")
        contract_id = "contract-v4-prompt"
        pipeline.write_json_idempotent(self.store.record("contracts", contract_id), {
            "schemaVersion": 4, "contractId": contract_id,
            "compositionSpec": {"compositionId": composition["compositionId"]},
            "briefSpec": {"briefId": "poet-v4", "path": self.store.relative(brief_path), "sha256": pipeline.sha256_file(brief_path)},
        })
        job_id = "job-v4-prompt"
        pipeline.write_json_idempotent(self.store.record("jobs", job_id), {
            "schemaVersion": 4, "jobId": job_id, "contractId": contract_id,
            "prompt": {"path": self.store.relative(prompt), "sha256": pipeline.sha256_file(prompt)},
            "inputs": [{"role": role, "path": self.store.relative(anchor), "sha256": pipeline.sha256_file(anchor)}],
        })
        compiled = pipeline.compile_prompt(self.store, self.ns(
            job_id=job_id, pose_guide_id=guide["poseGuideId"], output="Tools/artworks/reviews/poet-v4-prompt.md"))
        text = self.store.absolute(compiled["artifact"]["path"]).read_text(encoding="utf-8")
        self.assertIn("approved five-red chow face", text)
        self.assertIn("identity and volume only", text)
        self.assertIn("generic gray-white forehead blaze", text)
        self.assertNotIn("half-body alternate coat color", text)

    def test_end_to_end_is_idempotent_and_promotes(self):
        _, job, _ = self.contract_and_job()
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [], "cases": []}), encoding="utf-8")
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/raw.png")
        ing = self.ns(attempt_id=attempt["attemptId"], source=str(raw))
        pipeline.ingest(self.store, ing); pipeline.ingest(self.store, ing)
        prep = self.ns(attempt_id=attempt["attemptId"], chroma=None)
        pipeline.prepare(self.store, prep); pipeline.prepare(self.store, prep)
        mask = self.mask("incoming/mask.png")
        attach = self.ns(attempt_id=attempt["attemptId"], mask=str(mask))
        pipeline.attach_mask(self.store, attach); pipeline.attach_mask(self.store, attach)
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertTrue(report["passed"], report["issues"])
        pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        approval = self.ns(attempt_id=attempt["attemptId"], reviewer="cty41", reason="fixture approved", decided_at="2026-08-18T00:00:00+08:00")
        pipeline.decide(self.store, approval, "approved"); pipeline.decide(self.store, approval, "approved")
        promoted = pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertEqual("promoted", promoted["state"])
        pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))
        cases = json.loads(cases_path.read_text(encoding="utf-8"))
        self.assertEqual([], cases["approved_assets"])  # a single direction is not a complete formal mother pair

    def test_action_identity_alias_does_not_replace_idle_casebook_mother(self):
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [{
            "id": "tomb-maw-bat", "down_right": "idle-dr.png", "up_left": "idle-ul.png"
        }], "cases": []}), encoding="utf-8")
        pipeline.update_approved_cases(self.store, {
            "assetId": "tomb-maw-bat-melee-bite-dr-v01",
            "approvedAssetId": "tomb-maw-bat",
            "direction": "down-right",
        }, "bite-dr.png")
        cases = json.loads(cases_path.read_text(encoding="utf-8"))
        self.assertEqual("idle-dr.png", cases["approved_assets"][0]["down_right"])

    def test_different_ingest_and_failed_promotion_are_rejected(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        first = self.png("incoming/first.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(first)))
        second = self.png("incoming/second.png", pear=True)
        with self.assertRaises(pipeline.PipelineError):
            pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(second)))

    def test_technical_remediation_reuses_exact_parent_generation(self):
        _, job, _ = self.contract_and_job()
        job_record = pipeline.load_json(self.store.record("jobs", job["jobId"]))
        job_record["requiresInvocation"] = True
        pipeline.write_json_idempotent(self.store.record("jobs", job["jobId"]), job_record)
        parent = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        invocation = {
            "schemaVersion": 2, "invocationId": "generation-invocation-fixture",
            "attemptId": parent["attemptId"], "state": "started",
        }
        pipeline.write_json_idempotent(
            self.store.record("generation-invocations", invocation["invocationId"]), invocation, immutable=True)
        raw = self.png("incoming/remediation.png")
        pipeline.ingest(self.store, self.ns(
            attempt_id=parent["attemptId"], source=str(raw), invocation_id=invocation["invocationId"]))
        parent = pipeline.load_json(self.store.record("attempts", parent["attemptId"]))
        child = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=parent["attemptId"], feedback_id=None,
            technical_remediation=True))
        remediated = pipeline.ingest(self.store, self.ns(
            attempt_id=child["attemptId"], source=str(raw), invocation_id=None))
        self.assertEqual(parent["generationInvocationId"], remediated["generationInvocationId"])
        self.assertEqual(parent["generationDeliveryId"], remediated["generationDeliveryId"])

    def test_no_arms_contract_rejects_forbidden_limb_labels(self):
        image = self.png("Tools/artworks/candidates/hero.png")
        mask = self.mask("Tools/artworks/masks/hero.png")
        with Image.open(mask) as source:
            edited = source.convert("RGBA")
        ImageDraw.Draw(edited).rectangle((100, 145, 103, 164), fill=pipeline.MASK_COLORS["near_arm"])
        edited.save(mask)
        attempt = {"artifacts": {"prepared": {"path": self.store.relative(image), "sha256": pipeline.sha256_file(image)},
                                 "mask": {"path": self.store.relative(mask), "sha256": pipeline.sha256_file(mask)}}}
        _, issues = pipeline.geometry_checks(self.store, {"maskRequired": True, "noArms": True, "kind": "action_pose",
                                                           "anchor": None, "handSides": {}, "compositionSpec": None}, attempt)
        self.assertIn("near_arm_forbidden", issues)

    def test_identity_mask_rejects_a_diamond_blaze(self):
        anchor = self.identity_mask("Tools/artworks/masks/idle-identity.png")
        candidate = self.identity_mask("Tools/artworks/masks/cast-identity.png", diamond=True)
        contract = {"identitySpec": {"anchorMaskPath": self.store.relative(anchor), "anchorMaskSha256": pipeline.sha256_file(anchor),
                                      "foreheadBlazeMinIou": 0.45, "foreheadBlazeAreaRatio": [0.65, 1.45]}}
        attempt = {"artifacts": {"identityMask": {"path": self.store.relative(candidate), "sha256": pipeline.sha256_file(candidate)}}}
        self.assertIn("forehead_blaze_shape_mismatch", pipeline.identity_mask_issues(self.store, contract, attempt))

    def test_identity_contract_requires_anchor_tile_compare_for_approval(self):
        with self.assertRaises(pipeline.PipelineError):
            pipeline.approval_review_hashes(self.store, {"artifacts": {"review": {}}}, {"identitySpec": {}})

    def test_prepare_parameter_mismatch_is_aborted_not_left_incomplete(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/prepare-mismatch.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None, chroma_tolerance=0))
        with self.assertRaises(pipeline.PipelineError):
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma="00ff00", chroma_tolerance=12))
        transaction = next((self.store.pipeline / "transactions").glob("*.json"))
        records = [json.loads(path.read_text(encoding="utf-8"))
                   for path in (self.store.pipeline / "transactions").glob("*.json")]
        self.assertIn("aborted", [record["state"] for record in records])
        self.assertNotEqual(transaction, None)
        with self.assertRaises(pipeline.PipelineError):
            pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))

    def test_gate_exception_approval_is_idempotent_selects_series_and_promotes(self):
        _, _, job_args = self.contract_and_job()
        _, job = self.bind_series(job_args)
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [], "cases": []}), encoding="utf-8")
        attempt, report, _ = self.process_core_size_failure(job)
        self.assertEqual(["core_size_out_of_tolerance"], report["issues"])
        args = self.ns(
            attempt_id=attempt["attemptId"], issue=["core_size_out_of_tolerance"], reviewer="cty41",
            reason="fixture accepts the known core width difference", decided_at="2026-08-18T01:00:00+08:00")
        receipt = pipeline.approve_exception(self.store, args)
        self.assertEqual(receipt, pipeline.approve_exception(self.store, args))
        self.assertEqual("gate-exception", receipt["approvalMode"])
        self.assertEqual([56, 121], receipt["waivedIssues"][0]["candidateCoreSize"])
        series = pipeline.load_json(self.store.record("series", "demonbound-series"))
        self.assertEqual(attempt["attemptId"], series["poses"][0]["selectedAttemptId"])
        self.assertEqual("approved", series["poses"][0]["state"])
        promoted = pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertEqual("promoted", promoted["state"])
        self.assertEqual(receipt, pipeline.approve_exception(self.store, args))
        managed, state_error = validator.promoted_state_machine_paths(self.root)
        self.assertIsNone(state_error)
        master = self.root / "Tools/artworks/approved/hero.png"
        preview = self.root / "Tools/artworks/approved/hero_128.png"
        reports = validator.validate_pair(
            master, preview, standard_height=122, baseline=236, preview_size=128,
            geometry_required=master.resolve() not in managed)
        self.assertFalse([report for report in reports if report["issues"]], reports)

    def test_gate_exception_rejects_nonwaivable_or_incomplete_failures(self):
        _, job, _ = self.contract_and_job()
        attempt, report, _ = self.process_core_size_failure(job, add_chroma=True)
        self.assertIn("core_size_out_of_tolerance", report["issues"])
        self.assertIn("exact_chroma_residue", report["issues"])
        args = self.ns(
            attempt_id=attempt["attemptId"], issue=["core_size_out_of_tolerance"], reviewer="cty41",
            reason="must not bypass chroma", decided_at="2026-08-18T01:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly match"):
            pipeline.approve_exception(self.store, args)
        args.issue = ["exact_chroma_residue"]
        with self.assertRaisesRegex(pipeline.PipelineError, "not waivable"):
            pipeline.approve_exception(self.store, args)

    def test_gate_exception_requires_cty41_complete_review_and_immutable_hashes(self):
        _, job, _ = self.contract_and_job()
        missing_review, _, _ = self.process_core_size_failure(job, render_review=False)
        args = self.ns(
            attempt_id=missing_review["attemptId"], issue=["core_size_out_of_tolerance"], reviewer="cty41",
            reason="fixture exception", decided_at="2026-08-18T01:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "review outputs"):
            pipeline.approve_exception(self.store, args)

        attempt, _, _ = self.process_core_size_failure(job)
        args.attempt_id = attempt["attemptId"]
        args.reviewer = "codex"
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer must be cty41"):
            pipeline.approve_exception(self.store, args)
        args.reviewer = "cty41"
        pipeline.approve_exception(self.store, args)
        record = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
        review_path = self.store.absolute(record["artifacts"]["review"]["preview128"]["path"])
        review_path.write_bytes(review_path.read_bytes() + b"tampered")
        with self.assertRaisesRegex(pipeline.PipelineError, "review artifact hash mismatch"):
            pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))

    def test_raw_tampering_and_approval_without_review_are_rejected(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/raw-tamper.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        attempt_record = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
        stored_raw = self.store.absolute(attempt_record["artifacts"]["raw"]["path"])
        stored_raw.write_bytes(stored_raw.read_bytes() + b"tampered")
        with self.assertRaisesRegex(pipeline.PipelineError, "raw artifact hash mismatch"):
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))

        second = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        clean = self.png("incoming/clean.png")
        pipeline.ingest(self.store, self.ns(attempt_id=second["attemptId"], source=str(clean)))
        pipeline.prepare(self.store, self.ns(attempt_id=second["attemptId"], chroma=None))
        mask = self.mask("incoming/clean-mask.png")
        pipeline.attach_mask(self.store, self.ns(attempt_id=second["attemptId"], mask=str(mask)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=second["attemptId"]))
        self.assertTrue(report["passed"], report["issues"])
        approval = self.ns(attempt_id=second["attemptId"], reviewer="cty41", reason="bypass", decided_at="2026-08-18T00:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "review outputs"):
            pipeline.decide(self.store, approval, "approved")

    def test_geometry_rejects_pear_and_missing_contact(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/pear.png", pear=True)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        mask = self.mask("incoming/pear-mask.png", pear=True, contact=1)
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertFalse(report["passed"])
        self.assertIn("core_lower_wider_than_middle", report["issues"])
        self.assertIn("near_hand_contact_lt_3", report["issues"])

    def test_technical_gate_rejects_chroma_and_transparent_rgb(self):
        path = self.png("bad.png")
        image = Image.open(path).convert("RGBA")
        image.putpixel((0, 0), (1, 2, 3, 0)); image.putpixel((10, 10), (0, 255, 0, 255)); image.save(path)
        _, issues = pipeline.inspect_technical(path, "ground_character")
        self.assertIn("transparent_rgb_nonzero", issues)
        self.assertIn("exact_chroma_residue", issues)

    def test_technical_gate_accepts_contract_declared_large_master(self):
        path = self.root / "large-master.png"
        Image.new("RGBA", (384, 384), (0, 0, 0, 0)).save(path)
        technical, issues = pipeline.inspect_technical(
            path, "projectile", expected_master_size=(384, 384))
        self.assertEqual([384, 384], technical["size"])
        self.assertNotIn("master_size_mismatch", issues)

        _, default_issues = pipeline.inspect_technical(path, "projectile")
        self.assertIn("master_size_mismatch", default_issues)

    def test_tile_placement_contract_and_review_metrics(self):
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="altar", approved_asset_id=None, kind="projectile", direction="down-right", pose="display",
            anchor=None, anchor_mask=None, mask_required=False, no_arms=False,
            near_hand_side=None, far_hand_side=None, size_tolerance=3, center_tolerance=2,
            master_width=384, master_height=384,
            footprint_width=2, footprint_height=2, display_scale=0.5,
            ground_anchor_x=192, ground_anchor_y=342, anchor_mode="contact_shape_center",
            output_master="Tools/artworks/approved/altar.png",
            output_preview="Tools/artworks/approved/altar_128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
        self.assertEqual([384, 384], contract["canvasSpec"]["masterSize"])
        self.assertEqual([2, 2], contract["tilePlacementSpec"]["footprintTiles"])

        image = Image.new("RGBA", (384, 384), (0, 0, 0, 0))
        review, metrics = pipeline.render_tile_placement_review(image, contract["tilePlacementSpec"])
        self.assertEqual([192, 192], metrics["displaySize"])
        self.assertEqual(metrics["logicalTileCenter"], metrics["anchorScreenPoint"])
        self.assertEqual(4, len(metrics["tileCenters"]))
        self.assertEqual([128, 224], metrics["logicalTileCenter"])
        self.assertEqual((256, 256), review.size)

    def test_tile_placement_contract_rejects_partial_or_out_of_canvas_values(self):
        common = dict(
            asset_id="bad", approved_asset_id=None, kind="projectile", direction="down-right", pose="display",
            anchor=None, anchor_mask=None, mask_required=False, no_arms=False,
            near_hand_side=None, far_hand_side=None, size_tolerance=3, center_tolerance=2,
            master_width=256, master_height=256, output_master="Tools/artworks/approved/bad.png",
            output_preview="Tools/artworks/approved/bad_128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated")
        with self.assertRaisesRegex(pipeline.PipelineError, "requires footprint"):
            pipeline.create_contract(self.store, self.ns(**common, footprint_width=1))
        with self.assertRaisesRegex(pipeline.PipelineError, "inside the master canvas"):
            pipeline.create_contract(self.store, self.ns(
                **common, footprint_width=1, footprint_height=1, display_scale=0.5,
                ground_anchor_x=128, ground_anchor_y=256, anchor_mode="contact_shape_center"))

    def test_target_orientation_places_asset_upper_right_facing_player(self):
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        spec = {
            "footprintTiles": [1, 1], "displayScale": 0.5,
            "groundAnchorPx": [128, 230], "anchorMode": "contact_shape_center",
            "boardRole": "target", "screenFacing": "down_left",
        }
        review, metrics = pipeline.render_tile_placement_review(image, spec)
        self.assertEqual("up_right", metrics["explorationDirection"])
        self.assertEqual("down_left", metrics["screenFacing"])
        self.assertGreater(metrics["logicalTileCenter"][0], metrics["playerReferenceTileCenter"][0])
        self.assertLess(metrics["logicalTileCenter"][1], metrics["playerReferenceTileCenter"][1])
        self.assertEqual(metrics["logicalTileCenter"], metrics["anchorScreenPoint"])
        self.assertEqual((320, 224), review.size)

    def test_finite_series_requires_feedback_and_rejects_sixth_unique_output(self):
        _, _, args = self.contract_and_job()
        _series, job = self.bind_series(args)
        first = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
        raw = self.png("incoming/series-first.png", variant=1)
        pipeline.ingest(self.store, self.ns(attempt_id=first["attemptId"], source=str(raw)))
        with self.assertRaisesRegex(pipeline.PipelineError, "feedback-id"):
            pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=first["attemptId"], feedback_id=None))
        pipeline.prepare(self.store, self.ns(attempt_id=first["attemptId"], chroma=None))
        pipeline.attach_mask(self.store, self.ns(attempt_id=first["attemptId"], mask=str(self.mask("incoming/series-first-mask.png"))))
        pipeline.validate_attempt(self.store, self.ns(attempt_id=first["attemptId"]))
        pipeline.render_review(self.store, self.ns(attempt_id=first["attemptId"]))
        previous = pipeline.record_feedback(self.store, self.ns(
            attempt_id=first["attemptId"], reviewer="cty41", verdict="retry", strength=[], defect=["v1"],
            next_prompt_delta="v2", recorded_at="2026-08-18T00:00:00+08:00"))
        for variant in range(2, 6):
            attempt = pipeline.retry(self.store, self.ns(
                job_id=job["jobId"], parent_attempt=None, feedback_id=previous["feedbackId"]))
            raw = self.png(f"incoming/series-{variant}.png", variant=variant)
            pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
            pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(self.mask(f"incoming/series-{variant}-mask.png"))))
            pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
            pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
            previous = pipeline.record_feedback(self.store, self.ns(
                attempt_id=attempt["attemptId"], reviewer="cty41", verdict="retry", strength=[], defect=[f"v{variant}"],
                next_prompt_delta=f"v{variant + 1}", recorded_at="2026-08-18T00:00:00+08:00"))
        sixth = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=previous["feedbackId"]))
        with self.assertRaisesRegex(pipeline.PipelineError, "output limit"):
            pipeline.ingest(self.store, self.ns(
                attempt_id=sixth["attemptId"], source=str(self.png("incoming/series-6.png", variant=6))))

    def test_unlimited_series_accepts_more_than_five_unique_outputs(self):
        _, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=None)
        self.assertIsNone(series["maxUniqueOutputs"])
        for variant in range(1, 7):
            self.process_series_attempt(job, variant=variant, verdict="retry")
        stored = pipeline.load_json(self.store.record("series", series["seriesId"]))
        pose = pipeline.series_pose(stored, "idle-dr")
        self.assertEqual(6, len(pipeline.unique_pose_hashes(self.store, pose)))

    def test_series_limit_can_be_removed_with_audited_change(self):
        _, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=1)
        _first, feedback = self.process_series_attempt(job, variant=1, verdict="retry")
        second = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=None, feedback_id=feedback["feedbackId"]))
        raw = self.png("incoming/series-after-unlimited.png", variant=2)
        with self.assertRaisesRegex(pipeline.PipelineError, "output limit"):
            pipeline.ingest(self.store, self.ns(attempt_id=second["attemptId"], source=str(raw)))
        change = pipeline.set_series_output_limit(self.store, self.ns(
            series_id=series["seriesId"], max_unique_outputs=None, unlimited=True,
            reviewer="cty41", reason="continue reviewed iteration",
            decided_at="2026-08-18T00:00:00+08:00"))
        self.assertIsNone(change["maxUniqueOutputs"])
        pipeline.ingest(self.store, self.ns(attempt_id=second["attemptId"], source=str(raw)))
        stored = pipeline.load_json(self.store.record("series", series["seriesId"]))
        self.assertIsNone(stored["maxUniqueOutputs"])
        self.assertIn(change["seriesLimitChangeId"], stored["limitChangeIds"])

    def test_removed_limit_reopens_exhausted_pose_without_rewriting_feedback(self):
        _, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=1)
        attempt, feedback = self.process_series_attempt(job, variant=1, verdict="exhausted")
        pipeline.set_series_output_limit(self.store, self.ns(
            series_id=series["seriesId"], max_unique_outputs=None, unlimited=True,
            reviewer="cty41", reason="continue reviewed iteration",
            decided_at="2026-08-18T00:00:00+08:00"))
        retried = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=attempt["attemptId"], feedback_id=feedback["feedbackId"]))
        self.assertEqual(feedback["feedbackId"], retried["retryFeedbackId"])
        stored = pipeline.load_json(self.store.record("series", series["seriesId"]))
        self.assertEqual("active", pipeline.series_pose(stored, "idle-dr")["state"])

    def test_provisional_anchor_makes_downstream_jobs_concept_only(self):
        contract, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=1)
        attempt, _ = self.process_series_attempt(job, variant=1, verdict="retry")
        pipeline.select_attempt(self.store, self.ns(attempt_id=attempt["attemptId"], provisional=True))
        pipeline.advance_series(self.store, self.ns(series_id=series["seriesId"]))
        next_contract = dict(contract)
        next_contract.pop("contractId")
        next_contract["outputs"] = {"master": "Tools/artworks/concepts/hero-ul.png", "preview": "Tools/artworks/concepts/hero-ul-128.png"}
        next_id = pipeline.contract_id(next_contract)
        pipeline.write_json_idempotent(self.store.record("contracts", next_id), {"schemaVersion": 1, "contractId": next_id, **next_contract}, immutable=True)
        next_args = self.ns(contract_id=next_id, prompt=args.prompt, input=args.input,
                            series_id=series["seriesId"], pose_id="idle-ul")
        downstream = pipeline.create_job(self.store, next_args)
        self.assertTrue(downstream["conceptOnly"])

    def test_nine_pose_series_end_to_end_without_imagegen(self):
        poses = ["idle-dr", "idle-ul", "melee-dr", "melee-ul", "cast-dr", "cast-ul", "hit-dr", "hit-ul", "death"]
        _contract, _, args = self.contract_and_job()
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [], "cases": []}), encoding="utf-8")
        series, first_job = self.bind_series(args, poses=poses)
        first_attempt, _ = self.process_series_attempt(first_job, verdict="selected")
        pipeline.select_attempt(self.store, self.ns(attempt_id=first_attempt["attemptId"], provisional=False))
        approval_args = self.ns(attempt_id=first_attempt["attemptId"], reviewer="cty41", reason="fixture idle anchor",
                                decided_at="2026-08-18T00:00:00+08:00")
        pipeline.decide(self.store, approval_args, "approved")
        promoted_idle = pipeline.promote(self.store, self.ns(attempt_id=first_attempt["attemptId"]))
        idle_master = promoted_idle["artifacts"]["promoted"]["master"]["path"]
        idle_mask = promoted_idle["artifacts"]["mask"]["path"]
        pipeline.advance_series(self.store, self.ns(series_id=series["seriesId"]))

        selected = []
        for index, pose in enumerate(poses[1:], start=1):
            direction = "up-left" if pose.endswith("ul") else "down-right"
            contract = pipeline.create_contract(self.store, self.ns(
                asset_id=f"hero-{pose}", approved_asset_id=f"hero-{pose}", kind="ground_character",
                direction=direction, pose=pose, anchor=idle_master, anchor_mask=idle_mask,
                mask_required=True, no_arms=True, near_hand_side="left", far_hand_side="right",
                size_tolerance=3, center_tolerance=2,
                output_master=f"Tools/artworks/approved/hero-{pose}.png",
                output_preview=f"Tools/artworks/approved/hero-{pose}-128.png",
                rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
            job = pipeline.create_job(self.store, self.ns(
                contract_id=contract["contractId"], prompt=args.prompt, input=[f"core_anchor={self.root / idle_master}"],
                series_id=series["seriesId"], pose_id=pose))
            attempt, _ = self.process_series_attempt(job, variant=index, verdict="selected")
            pipeline.select_attempt(self.store, self.ns(attempt_id=attempt["attemptId"], provisional=False))
            selected.append(attempt["attemptId"])
            pipeline.advance_series(self.store, self.ns(series_id=series["seriesId"]))

        for attempt_id in selected:
            decision = self.ns(attempt_id=attempt_id, reviewer="cty41", reason="fixture batch approval",
                               decided_at="2026-08-18T00:00:00+08:00")
            pipeline.decide(self.store, decision, "approved")
            pipeline.promote(self.store, self.ns(attempt_id=attempt_id))
        final_series = pipeline.load_json(self.store.record("series", series["seriesId"]))
        self.assertIsNone(final_series["currentPoseId"])
        self.assertTrue(all(pose["state"] == "promoted" for pose in final_series["poses"]))
        self.assertTrue(pipeline.strict_check(self.store, True)["ok"])

    def test_core_calibration_is_uniform_idempotent_and_256(self):
        _contract, job = self.occlusion_contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
        source = self.root / "incoming/large.png"; source.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (1254, 1254), (0, 0, 0, 0)); draw = ImageDraw.Draw(image)
        draw.rectangle((450, 400, 749, 999), fill=(90, 80, 70, 255))
        draw.rectangle((430, 650, 449, 680), fill=(90, 80, 70, 255)); draw.rectangle((750, 650, 769, 680), fill=(90, 80, 70, 255))
        draw.rectangle((450, 980, 520, 1020), fill=(90, 80, 70, 255)); draw.rectangle((679, 980, 749, 1020), fill=(90, 80, 70, 255))
        draw.rectangle((420, 600, 449, 760), fill=(90, 80, 70, 255)); image.save(source)
        mask_path = self.root / "incoming/large-mask.png"
        mask = Image.new("RGBA", (1254, 1254), (0, 0, 0, 0)); md = ImageDraw.Draw(mask)
        md.rectangle((450, 400, 749, 999), fill=pipeline.MASK_COLORS["core"])
        md.rectangle((430, 650, 449, 680), fill=pipeline.MASK_COLORS["near_hand"])
        md.rectangle((750, 650, 769, 680), fill=pipeline.MASK_COLORS["far_hand"])
        md.rectangle((450, 980, 520, 1020), fill=pipeline.MASK_COLORS["near_foot"])
        md.rectangle((679, 980, 749, 1020), fill=pipeline.MASK_COLORS["far_foot"])
        md.rectangle((420, 600, 449, 760), fill=pipeline.MASK_COLORS["equipment"]); mask.save(mask_path)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(source)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask_path)))
        first = pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
        second = pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertEqual(first["artifacts"]["calibrated"], second["artifacts"]["calibrated"])
        with Image.open(self.store.absolute(first["artifacts"]["calibrated"]["path"])) as calibrated:
            self.assertEqual((256, 256), calibrated.size)
        self.assertAlmostEqual(121 / 600, first["calibration"]["scale"])
        with Image.open(self.store.absolute(first["artifacts"]["calibratedMask"]["path"])) as calibrated_mask:
            box = pipeline.bbox_for(pipeline.pixel_data(calibrated_mask.convert("RGBA")), calibrated_mask.size, pipeline.MASK_COLORS["core"])
        self.assertGreater(box[2] - box[0] + 1, 55)  # uniform scaling keeps the wide source wide instead of stretching to the 40px anchor

    def test_chroma_tolerance_removes_generated_green_variation(self):
        source = self.root / "green-source.png"
        image = Image.new("RGBA", (4, 4), (24, 240, 24, 255)); image.putpixel((2, 2), (90, 80, 70, 255)); image.save(source)
        output = self.root / "green-prepared.png"
        pipeline.prepare_image(source, output, "00ff00", 48)
        with Image.open(output) as prepared:
            prepared = prepared.convert("RGBA")
            self.assertEqual((0, 0, 0, 0), prepared.getpixel((0, 0)))
            self.assertEqual((90, 80, 70, 255), prepared.getpixel((2, 2)))

    def test_resampled_chroma_cleanup_removes_both_reserved_key_colors(self):
        image = Image.new("RGBA", (3, 1), (0, 0, 0, 0))
        image.putdata([(0, 255, 0, 1), (255, 0, 255, 1), (120, 70, 140, 255)])

        cleaned = pipeline.clean_resampled_chroma(image, "00ff00", 48)

        self.assertEqual((0, 0, 0, 0), cleaned.getpixel((0, 0)))
        self.assertEqual((0, 0, 0, 0), cleaned.getpixel((1, 0)))
        self.assertEqual((120, 70, 140, 255), cleaned.getpixel((2, 0)))

    def test_behind_core_intrusion_is_rejected_and_outer_arcs_pass(self):
        _contract, job = self.occlusion_contract_and_job()
        for intrusion in (False, True):
            attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
            raw = self.png(f"incoming/occlusion-{intrusion}.png")
            if not intrusion:
                image = Image.open(raw).convert("RGBA"); ImageDraw.Draw(image).rectangle((100, 160, 107, 175), fill=(90, 80, 70, 255)); image.save(raw)
            else:
                image = Image.open(raw).convert("RGBA"); ImageDraw.Draw(image).rectangle((120, 160, 125, 190), fill=(90, 80, 70, 255)); image.save(raw)
            pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
            pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(self.occlusion_mask(f"incoming/occlusion-{intrusion}-mask.png", intrusion))))
            pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
            report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
            if intrusion:
                self.assertFalse(report["passed"])
                self.assertIn("equipment_intrudes_core", report["issues"])
                self.assertIn("core_row_disconnected", report["issues"])
            else:
                self.assertTrue(report["passed"], report["issues"])
                review = pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
                self.assertIn("depthReview", review["outputs"])
                record = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
                record["artifacts"]["review"].pop("depthReview")
                pipeline.save_attempt(self.store, record)
                with self.assertRaisesRegex(pipeline.PipelineError, "review outputs"):
                    pipeline.decide(self.store, self.ns(
                        attempt_id=attempt["attemptId"], reviewer="cty41", reason="missing depth review",
                        decided_at="2026-08-18T00:00:00+08:00"), "approved")

    def test_visibility_cap_and_missing_far_hand_are_rejected(self):
        _contract, job = self.occlusion_contract_and_job(equipment_cap=0.01)
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
        raw = self.png("incoming/visibility.png")
        image = Image.open(raw).convert("RGBA"); ImageDraw.Draw(image).rectangle((100, 160, 107, 175), fill=(90, 80, 70, 255)); image.save(raw)
        mask_path = self.occlusion_mask("incoming/visibility-mask.png")
        mask = Image.open(mask_path).convert("RGBA")
        pixels = mask.load()
        for y in range(mask.height):
            for x in range(mask.width):
                if pixels[x, y] == pipeline.MASK_COLORS["far_hand"]:
                    pixels[x, y] = (0, 0, 0, 0)
        mask.save(mask_path)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask_path)))
        pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertIn("far_hand_missing", report["issues"])
        self.assertIn("far_hand_missing_for_layer_rule", report["issues"])
        self.assertIn("equipment_visibility_cap_exceeded", report["issues"])

    def v3_contract(self, asset_id: str, role: str, component_kind: str | None, source_mode: str,
                    no_arms: bool = False):
        return pipeline.create_contract(self.store, self.ns(
            asset_id=asset_id, approved_asset_id=asset_id, kind="tile", direction="down-right", pose="cast",
            anchor=None, anchor_mask=None, mask_required=True, no_arms=no_arms,
            near_hand_side=None, far_hand_side=None, size_tolerance=3, center_tolerance=2,
            layer_rule=[], visibility_cap=[], composition_id=None, identity_anchor_mask=None,
            forehead_blaze_min_iou=0.45, pose_reference=False,
            output_master=f"Tools/artworks/approved/{asset_id}.png",
            output_preview=f"Tools/artworks/approved/{asset_id}-128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated-or-derived",
            asset_role=role, component_kind=component_kind, source_mode=source_mode))

    def approved_component(self, asset_id: str, kind: str, image: Image.Image, source_mode: str = "generated"):
        contract = self.v3_contract(asset_id, "component", kind, source_mode)
        path = self.root / f"Tools/artworks/components/{asset_id}.png"
        path.parent.mkdir(parents=True, exist_ok=True); image.save(path)
        artifact = {"path": self.store.relative(path), "sha256": pipeline.sha256_file(path)}
        label = "core" if kind == "body" else "equipment"
        if kind == "paw_overlay":
            label = "far_hand" if "far" in asset_id else "near_hand"
        if kind == "foot_overlay":
            label = "far_foot" if "far" in asset_id else "near_foot"
        mask = Image.new("RGBA", image.size, (0, 0, 0, 0))
        mask.paste(pipeline.MASK_COLORS[label], mask=image.getchannel("A"))
        mask_path = path.with_name(f"{path.stem}-mask.png"); mask.save(mask_path)
        mask_artifact = {"path": self.store.relative(mask_path), "sha256": pipeline.sha256_file(mask_path)}
        job_id = f"job-{asset_id}"
        pipeline.write_json_idempotent(self.store.record("jobs", job_id), {
            "schemaVersion": 3, "jobId": job_id, "state": "ready", "contractId": contract["contractId"],
            "contractSha256": pipeline.sha256_file(self.store.record("contracts", contract["contractId"])),
            "prompt": None, "inputs": [], "target": {"direction": "down-right", "pose": "cast"},
            "series": None, "conceptOnly": False, "contractRequirements": None,
            "requiresInvocation": False, "sourceMode": source_mode})
        attempt_id = f"{job_id}-a001"
        approval_id = f"approval-{asset_id}"
        pipeline.write_json_idempotent(self.store.record("attempts", attempt_id), {
            "schemaVersion": 3, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
            "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
            "technicalRemediation": False, "state": "approved",
            "artifacts": {"prepared": artifact, "mask": mask_artifact},
            "report": None, "approvalId": approval_id, "feedbackId": None})
        pipeline.write_json_idempotent(self.store.record("approvals", approval_id), {
            "schemaVersion": 3, "approvalId": approval_id, "attemptId": attempt_id,
            "candidateSha256": artifact["sha256"], "maskSha256": mask_artifact["sha256"],
            "reviewer": "cty41", "decision": "approved", "reason": "fixture",
            "decidedAt": "2026-08-19T00:00:00+08:00"})
        return attempt_id, contract, artifact

    def test_schema_v3_component_cannot_promote(self):
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(image).rectangle((100, 100, 140, 180), fill="red")
        attempt_id, _contract, artifact = self.approved_component("body-component", "body", image)
        with self.assertRaisesRegex(pipeline.PipelineError, "components cannot be promoted"):
            pipeline.promote(self.store, self.ns(attempt_id=attempt_id))

    def test_assembly_is_deterministic_and_rejects_unapproved_or_forbidden_transform(self):
        transparent = lambda: Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        body = transparent(); ImageDraw.Draw(body).rectangle((100, 80, 155, 230), fill=(90, 80, 70, 255))
        sword = transparent(); ImageDraw.Draw(sword).rectangle((126, 40, 130, 180), fill=(210, 210, 220, 255))
        far = transparent(); ImageDraw.Draw(far).ellipse((115, 160, 127, 172), fill=(255, 128, 0, 255))
        near = transparent(); ImageDraw.Draw(near).ellipse((130, 158, 142, 170), fill=(255, 128, 0, 255))
        far_foot = transparent(); ImageDraw.Draw(far_foot).ellipse((112, 220, 132, 240), fill=(200, 100, 0, 255))
        near_foot = transparent(); ImageDraw.Draw(near_foot).ellipse((130, 218, 152, 240), fill=(255, 128, 0, 255))
        components = [
            ("far_foot_overlay", *self.approved_component("assembly-far-foot-base", "foot_overlay", far_foot)),
            ("far_paw_overlay", *self.approved_component("assembly-far", "paw_overlay", far)),
            ("body", *self.approved_component("assembly-body", "body", body)),
            ("equipment", *self.approved_component("assembly-sword", "equipment", sword)),
            ("near_paw_overlay", *self.approved_component("assembly-near", "paw_overlay", near)),
            ("near_foot_overlay", *self.approved_component("assembly-near-foot-base", "foot_overlay", near_foot)),
        ]
        final_contract = self.v3_contract("assembled-cast", "assembled_sprite", None, "derived")
        spec = {
            "assetId": "assembled-cast", "contractId": final_contract["contractId"], "canvas": [256, 256],
            "layers": [{"role": role, "attemptId": attempt_id,
                        "transform": {"scalePercent": 100, "translate": [0, 0], "flipHorizontal": False}}
                       for role, attempt_id, _contract, _artifact in components],
        }
        spec_path = self.root / "assembly.json"; spec_path.write_text(json.dumps(spec), encoding="utf-8")
        assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        first = pipeline.render_assembly(self.store, self.ns(assembly_id=assembly["assemblyId"]))
        second = pipeline.render_assembly(self.store, self.ns(assembly_id=assembly["assemblyId"]))
        self.assertEqual(first["artifacts"]["prepared"], second["artifacts"]["prepared"])
        bad = json.loads(json.dumps(spec)); bad["layers"][1]["transform"]["rotation"] = 5
        spec_path.write_text(json.dumps(bad), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "only supports"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        unapproved = pipeline.load_json(self.store.record("attempts", components[1][1])); unapproved["state"] = "prepared"
        pipeline.write_json_idempotent(self.store.record("attempts", components[1][1]), unapproved)
        spec_path.write_text(json.dumps(spec), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "human approval"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))

    def test_derive_paw_overlay_uses_only_requested_semantic_label(self):
        body = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(body); draw.rectangle((20, 20, 29, 29), fill=(255, 128, 0, 255)); draw.rectangle((40, 20, 49, 29), fill=(255, 128, 0, 255))
        attempt_id, _contract, _artifact = self.approved_component("derive-body", "body", body)
        mask_path = self.root / "derive-mask.png"; mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); md = ImageDraw.Draw(mask)
        md.rectangle((20, 20, 29, 29), fill=pipeline.MASK_COLORS["near_hand"]); md.rectangle((40, 20, 49, 29), fill=pipeline.MASK_COLORS["far_hand"]); mask.save(mask_path)
        attempt = pipeline.load_json(self.store.record("attempts", attempt_id))
        attempt["artifacts"]["mask"] = {"path": self.store.relative(mask_path), "sha256": pipeline.sha256_file(mask_path)}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt_id), attempt)
        overlay_contract = self.v3_contract("near-overlay", "component", "paw_overlay", "derived")
        derived = pipeline.derive_component(self.store, self.ns(contract_id=overlay_contract["contractId"], source_attempt_id=attempt_id, label="near_hand"))
        output = Image.open(self.store.absolute(derived["artifacts"]["prepared"]["path"])).convert("RGBA")
        self.assertGreater(output.getpixel((24, 24))[3], 0)
        self.assertEqual(0, output.getpixel((44, 24))[3])

    def test_derive_body_and_equipment_partition_complete_pose_semantics(self):
        source = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(source)
        draw.rectangle((20, 20, 29, 29), fill=(80, 70, 60, 255))
        draw.rectangle((30, 20, 39, 29), fill=(120, 90, 70, 255))
        draw.rectangle((40, 20, 49, 29), fill=(220, 220, 230, 255))
        draw.rectangle((50, 20, 59, 29), fill=(255, 128, 0, 255))
        attempt_id, _contract, _artifact = self.approved_component("derive-complete", "body", source)
        mask_path = self.root / "derive-complete-mask.png"
        mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); md = ImageDraw.Draw(mask)
        md.rectangle((20, 20, 29, 29), fill=pipeline.MASK_COLORS["core"])
        md.rectangle((30, 20, 39, 29), fill=pipeline.MASK_COLORS["head_appendage"])
        md.rectangle((40, 20, 49, 29), fill=pipeline.MASK_COLORS["equipment"])
        md.rectangle((50, 20, 59, 29), fill=pipeline.MASK_COLORS["near_hand"])
        mask.save(mask_path)
        attempt = pipeline.load_json(self.store.record("attempts", attempt_id))
        attempt["artifacts"]["mask"] = {"path": self.store.relative(mask_path), "sha256": pipeline.sha256_file(mask_path)}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt_id), attempt)

        body_contract = self.v3_contract("derived-body", "component", "body", "derived")
        equipment_contract = self.v3_contract("derived-equipment", "component", "equipment", "derived")
        body = pipeline.derive_component(self.store, self.ns(
            contract_id=body_contract["contractId"], source_attempt_id=attempt_id, label="body"))
        equipment = pipeline.derive_component(self.store, self.ns(
            contract_id=equipment_contract["contractId"], source_attempt_id=attempt_id, label="equipment"))
        body_image = Image.open(self.store.absolute(body["artifacts"]["prepared"]["path"])).convert("RGBA")
        equipment_image = Image.open(self.store.absolute(equipment["artifacts"]["prepared"]["path"])).convert("RGBA")
        self.assertGreater(body_image.getpixel((24, 24))[3], 0)
        self.assertGreater(body_image.getpixel((34, 24))[3], 0)
        self.assertEqual(0, body_image.getpixel((44, 24))[3])
        self.assertGreater(equipment_image.getpixel((44, 24))[3], 0)
        self.assertEqual(0, equipment_image.getpixel((54, 24))[3])

    def test_derived_component_uses_passing_report_instead_of_human_approval(self):
        transparent = lambda: Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        source = transparent(); ImageDraw.Draw(source).ellipse((130, 158, 142, 170), fill=(255, 128, 0, 255))
        source_attempt_id, _contract, _artifact = self.approved_component("derive-near-source", "body", source)
        source_mask_path = self.root / "derive-near-source-mask.png"
        source_mask = transparent(); ImageDraw.Draw(source_mask).ellipse((130, 158, 142, 170), fill=pipeline.MASK_COLORS["near_hand"]); source_mask.save(source_mask_path)
        source_attempt = pipeline.load_json(self.store.record("attempts", source_attempt_id))
        source_attempt["artifacts"]["mask"] = {
            "path": self.store.relative(source_mask_path), "sha256": pipeline.sha256_file(source_mask_path)}
        pipeline.write_json_idempotent(self.store.record("attempts", source_attempt_id), source_attempt)
        derived_contract = self.v3_contract("derived-near-paw", "component", "paw_overlay", "derived")
        derived = pipeline.derive_component(self.store, self.ns(
            contract_id=derived_contract["contractId"], source_attempt_id=source_attempt_id, label="near_hand"))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=derived["attemptId"]))
        self.assertTrue(report["passed"])
        derived = pipeline.load_json(self.store.record("attempts", derived["attemptId"]))
        self.assertEqual("review_pending", derived["state"])
        self.assertIsNone(derived["approvalId"])

        body = transparent(); ImageDraw.Draw(body).rectangle((100, 80, 155, 230), fill=(90, 80, 70, 255))
        equipment = transparent(); ImageDraw.Draw(equipment).rectangle((126, 40, 130, 180), fill=(210, 210, 220, 255))
        far_hand = transparent(); ImageDraw.Draw(far_hand).ellipse((115, 160, 127, 172), fill=(220, 110, 0, 255))
        far_foot = transparent(); ImageDraw.Draw(far_foot).ellipse((112, 218, 132, 236), fill=(200, 100, 0, 255))
        near_foot = transparent(); ImageDraw.Draw(near_foot).ellipse((130, 218, 152, 236), fill=(255, 128, 0, 255))
        components = [
            ("far_foot_overlay", self.approved_component("derived-fixture-far-foot", "foot_overlay", far_foot)[0]),
            ("far_paw_overlay", self.approved_component("derived-fixture-far-paw", "paw_overlay", far_hand)[0]),
            ("body", self.approved_component("derived-fixture-body", "body", body)[0]),
            ("equipment", self.approved_component("derived-fixture-equipment", "equipment", equipment)[0]),
            ("near_paw_overlay", derived["attemptId"]),
            ("near_foot_overlay", self.approved_component("derived-fixture-near-foot", "foot_overlay", near_foot)[0]),
        ]
        final_contract = self.v3_contract("derived-assembly", "assembled_sprite", None, "derived")
        spec = {"assetId": "derived-assembly", "contractId": final_contract["contractId"], "canvas": [256, 256],
                "layers": [{"role": role, "attemptId": attempt_id,
                            "transform": {"scalePercent": 100, "translate": [0, 0], "flipHorizontal": False}}
                           for role, attempt_id in components]}
        spec_path = self.root / "derived-assembly.json"; spec_path.write_text(json.dumps(spec), encoding="utf-8")
        assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        self.assertEqual([role for role, _attempt_id in components], [layer["role"] for layer in assembly["layers"]])

    def test_foot_overlays_render_behind_body_in_canonical_order(self):
        transparent = lambda: Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        body = transparent(); ImageDraw.Draw(body).rectangle((100, 80, 155, 230), fill=(90, 80, 70, 255))
        far = transparent(); ImageDraw.Draw(far).ellipse((112, 218, 132, 236), fill=(200, 100, 0, 255))
        near = transparent(); ImageDraw.Draw(near).ellipse((130, 218, 152, 236), fill=(255, 128, 0, 255))
        sword = transparent(); ImageDraw.Draw(sword).rectangle((126, 40, 130, 180), fill=(210, 210, 220, 255))
        far_hand = transparent(); ImageDraw.Draw(far_hand).ellipse((115, 160, 127, 172), fill=(220, 110, 0, 255))
        near_hand = transparent(); ImageDraw.Draw(near_hand).ellipse((130, 158, 142, 170), fill=(255, 128, 0, 255))
        components = [
            ("far_foot_overlay", *self.approved_component("assembly-far-foot", "foot_overlay", far)),
            ("far_paw_overlay", *self.approved_component("assembly-foot-far-hand", "paw_overlay", far_hand)),
            ("body", *self.approved_component("assembly-foot-body", "body", body)),
            ("equipment", *self.approved_component("assembly-foot-sword", "equipment", sword)),
            ("near_paw_overlay", *self.approved_component("assembly-foot-near-hand", "paw_overlay", near_hand)),
            ("near_foot_overlay", *self.approved_component("assembly-near-foot", "foot_overlay", near)),
        ]
        final_contract = self.v3_contract("assembled-feet", "assembled_sprite", None, "derived", no_arms=True)
        spec = {"assetId": "assembled-feet", "contractId": final_contract["contractId"], "canvas": [256, 256],
                "layers": [{"role": role, "attemptId": attempt_id,
                            "transform": {"scalePercent": 100, "translate": [0, 0], "flipHorizontal": False}}
                           for role, attempt_id, _contract, _artifact in components]}
        spec_path = self.root / "feet-assembly.json"; spec_path.write_text(json.dumps(spec), encoding="utf-8")
        assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        rendered = pipeline.render_assembly(self.store, self.ns(assembly_id=assembly["assemblyId"]))
        output = Image.open(self.store.absolute(rendered["artifacts"]["prepared"]["path"])).convert("RGBA")
        self.assertEqual((90, 80, 70, 255), output.getpixel((120, 225)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=rendered["attemptId"]))
        self.assertFalse(any(issue.endswith("_contact_lt_3") for issue in report["issues"]))
        review = pipeline.render_review(self.store, self.ns(attempt_id=rendered["attemptId"]))
        layer_review_path = self.store.absolute(review["outputs"]["assemblyLayerReview"]["path"])
        with Image.open(layer_review_path) as layer_review:
            self.assertEqual((256 * 6, 512), layer_review.size)

        detached = json.loads(json.dumps(spec))
        detached["layers"][0]["transform"]["translate"] = [-80, 0]
        spec_path.write_text(json.dumps(detached), encoding="utf-8")
        detached_assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        detached_render = pipeline.render_assembly(self.store, self.ns(assembly_id=detached_assembly["assemblyId"]))
        detached_report = pipeline.validate_attempt(self.store, self.ns(attempt_id=detached_render["attemptId"]))
        self.assertIn("far_foot_contact_lt_3", detached_report["issues"])
        incomplete = json.loads(json.dumps(spec)); incomplete["layers"] = incomplete["layers"][:3]
        spec_path.write_text(json.dumps(incomplete), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly once"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        pose_depth = json.loads(json.dumps(spec))
        pose_depth["layers"] = [pose_depth["layers"][index] for index in (0, 5, 1, 4, 3, 2)]
        spec_path.write_text(json.dumps(pose_depth), encoding="utf-8")
        depth_assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        self.assertEqual(["far_foot_overlay", "near_foot_overlay", "far_paw_overlay",
                          "near_paw_overlay", "equipment", "body"],
                         [layer["role"] for layer in depth_assembly["layers"]])
        bad = json.loads(json.dumps(spec)); bad["layers"][0]["role"] = bad["layers"][1]["role"]
        spec_path.write_text(json.dumps(bad), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly once"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))

        far_attempt = pipeline.load_json(self.store.record("attempts", components[0][1]))
        far_mask_path = self.store.absolute(far_attempt["artifacts"]["mask"]["path"])
        contaminated = Image.open(far_mask_path).convert("RGBA")
        contaminated.putpixel((120, 225), pipeline.MASK_COLORS["near_foot"])
        contaminated.save(far_mask_path)
        spec_path.write_text(json.dumps(spec), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "foreign semantic label"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))

    def test_death_recipe_cli_is_retired_but_reviewed_import_promotes_exact_bytes(self):
        parser = pipeline.build_parser()
        self.assertNotIn("render-death-recipe", parser._subparsers._group_actions[0].choices)
        contract, _job = self.occlusion_contract_and_job()
        source = self.png("Tools/artworks/concepts/death-source.png")
        candidate_path = self.root / "Tools/artworks/candidates/death.png"; candidate_path.parent.mkdir(parents=True)
        candidate = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(candidate).ellipse((73, 81, 182, 174), fill=(80, 70, 65, 255)); candidate.save(candidate_path)
        preview_path = self.root / "Tools/artworks/candidates/death_128.png"
        preview = Image.new("RGBA", (128, 128), (0, 0, 0, 0)); ImageDraw.Draw(preview).ellipse((36, 40, 91, 87), fill=(80, 70, 65, 255)); preview.save(preview_path)
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(source), previous=str(candidate_path), reference=str(candidate_path),
            candidate=str(candidate_path), output="Tools/artworks/reviews/death-size.png"))
        adopted = pipeline.adopt_reviewed_sprite(self.store, self.ns(
            contract_id=contract["contractId"], source=str(source), candidate=str(candidate_path), preview=str(preview_path),
            size_comparison=comparison["artifact"]["path"], reviewer="cty41", reason="visual size accepted",
            accepted_at="2026-08-21T00:00:00+08:00"))
        attempt_id = adopted["attempt"]["attemptId"]
        self.assertIsNone(adopted["job"]["contractRequirements"])
        pipeline.decide(self.store, self.ns(attempt_id=attempt_id, reviewer="cty41", reason="visual size accepted",
                                             decided_at="2026-08-21T00:00:00+08:00"), "approved")
        master_output = self.store.absolute(contract["outputs"]["master"]); master_output.parent.mkdir(parents=True, exist_ok=True)
        preview_output = self.store.absolute(contract["outputs"]["preview"]); preview_output.parent.mkdir(parents=True, exist_ok=True)
        master_output.write_bytes(candidate_path.read_bytes()); preview_output.write_bytes(preview_path.read_bytes())
        promoted = pipeline.promote(self.store, self.ns(attempt_id=attempt_id))
        self.assertEqual(pipeline.sha256_file(candidate_path), promoted["artifacts"]["promoted"]["master"]["sha256"])
        self.assertEqual(pipeline.sha256_file(preview_path), promoted["artifacts"]["promoted"]["preview"]["sha256"])
        self.assertTrue(pipeline.strict_check(self.store, True)["ok"])

    def test_reviewed_import_rejects_non_human_or_off_center_candidate(self):
        contract, _job, _job_args = self.contract_and_job()
        source = self.png("Tools/artworks/concepts/source.png")
        candidate_path = self.root / "Tools/artworks/candidates/off-center.png"; candidate_path.parent.mkdir(parents=True)
        candidate = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(candidate).ellipse((10, 10, 80, 80), fill=(80, 70, 65, 255)); candidate.save(candidate_path)
        preview_path = self.root / "Tools/artworks/candidates/preview.png"
        Image.new("RGBA", (128, 128), (0, 0, 0, 0)).save(preview_path)
        review = self.root / "Tools/artworks/reviews/review.png"; review.parent.mkdir(parents=True); Image.new("RGBA", (576, 176), (32, 32, 32, 255)).save(review)
        args = self.ns(contract_id=contract["contractId"], source=str(source), candidate=str(candidate_path), preview=str(preview_path),
                       size_comparison=str(review), reviewer="agent", reason="invalid", accepted_at="2026-08-21T00:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer must be cty41"):
            pipeline.adopt_reviewed_sprite(self.store, args)
        args.reviewer = "cty41"
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(source), previous=str(candidate_path), reference=str(candidate_path), candidate=str(candidate_path),
            output="Tools/artworks/reviews/review.png"))
        args.size_comparison = comparison["artifact"]["path"]
        with self.assertRaisesRegex(pipeline.PipelineError, "centered or use the artwork baseline"):
            pipeline.adopt_reviewed_sprite(self.store, args)

    def test_normalize_reviewed_sprite_preserves_visible_pixels_and_clears_transparent_rgb(self):
        source = self.root / "Tools/artworks/candidates/source.png"; source.parent.mkdir(parents=True)
        image = Image.new("RGBA", (256, 256), (12, 34, 56, 0)); ImageDraw.Draw(image).ellipse((73, 81, 182, 174), fill=(80, 70, 65, 255)); image.save(source)
        result = pipeline.normalize_reviewed_sprite(self.store, self.ns(
            source=str(source), output="Tools/artworks/candidates/normalized.png",
            preview="Tools/artworks/candidates/normalized_128.png"))
        normalized = Image.open(self.store.absolute(result["artifacts"]["candidate"]["path"])).convert("RGBA")
        self.assertEqual((0, 0, 0, 0), normalized.getpixel((0, 0)))
        self.assertEqual(image.getpixel((128, 128)), normalized.getpixel((128, 128)))
        with Image.open(self.store.absolute(result["artifacts"]["preview"]["path"])) as preview:
            self.assertEqual((128, 128), preview.size)

    def test_equipment_style_candidate_uses_visible_crop_and_binds_passing_report(self):
        references = []
        for index in range(4):
            path = self.png(f"Tools/artworks/approved/style-{index}.png", variant=index)
            references.append({"role": f"anchor-{index}", "path": self.store.relative(path),
                               "sha256": pipeline.sha256_file(path)})
        profile_path = self.root / "Tools/artworks/equipment/style.json"
        profile_path.parent.mkdir(parents=True)
        profile_path.write_text(json.dumps({
            "schemaVersion": 1, "profileId": "test-flat-v1", "baseline": 236,
            "chroma": "00ff00", "chromaTolerance": 48,
            "processing": {"interiorPaletteColors": 8},
            "hardGates": {"maxInteriorColorBins": 40, "maxSmoothGradientRatio": 0.12},
            "references": references,
        }), encoding="utf-8")
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="styled-armor", approved_asset_id="styled-armor", kind="projectile",
            direction="down-right", pose="display", anchor=None, anchor_mask=None,
            mask_required=False, no_arms=False, near_hand_side=None, far_hand_side=None,
            size_tolerance=3, center_tolerance=2, layer_rule=[], visibility_cap=[],
            composition_id=None, identity_anchor_mask=None, forehead_blaze_min_iou=0.45,
            pose_reference=False, output_master="Tools/artworks/approved/styled-armor.png",
            output_preview="Tools/artworks/approved/styled-armor_128.png", rights_holder="cty41",
            license="project-owned", provenance="derived", asset_role=None, component_kind=None,
            source_mode=None, style_profile=str(profile_path), target_visible_height=108,
            visible_height_min=106, visible_height_max=110))
        source = self.root / "Tools/artworks/concepts/styled-source.png"
        source.parent.mkdir(parents=True)
        image = Image.new("RGBA", (400, 400), (0, 255, 0, 255))
        draw = ImageDraw.Draw(image)
        draw.rectangle((0, 0, 399, 399), fill=(35, 220, 25, 255))
        for y in range(80, 321):
            shade = 80 + (y - 80) // 4
            draw.rectangle((110, y, 289, y), fill=(shade, 65, 35, 255))
        image.save(source)
        report = pipeline.prepare_equipment_candidate(self.store, self.ns(
            contract_id=contract["contractId"], source=str(source),
            output="Tools/artworks/candidates/styled.png",
            preview="Tools/artworks/candidates/styled_128.png"))
        self.assertTrue(report["passed"], report["issues"])
        self.assertEqual(108, report["metrics"]["visibleSize"][1])
        self.assertLess(report["metrics"]["visibleSize"][0], report["metrics"]["visibleSize"][1])
        self.assertEqual(236, report["metrics"]["baseline"])
        candidate = self.store.absolute(report["candidate"]["path"])
        preview = self.store.absolute(report["preview"]["path"])
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(references[0]["path"]), previous=str(candidate), reference=str(candidate),
            candidate=str(candidate), output="Tools/artworks/reviews/styled-size.png"))
        adopted = pipeline.adopt_reviewed_sprite(self.store, self.ns(
            contract_id=contract["contractId"], source=str(source), candidate=str(candidate), preview=str(preview),
            size_comparison=comparison["artifact"]["path"], reviewer="cty41", reason="style accepted",
            accepted_at="2026-08-24T00:00:00+08:00"))
        bound_report = pipeline.load_json(self.store.absolute(adopted["attempt"]["report"]["path"]))
        self.assertEqual(report["styleReportId"],
                         Path(bound_report["styleReport"]["path"]).stem.replace("equipment-style-report-", "equipment-style-report-"))

    def test_styled_reviewed_import_rejects_candidate_without_style_report(self):
        reference = self.png("Tools/artworks/approved/style-anchor.png")
        profile_path = self.root / "Tools/artworks/equipment/style.json"
        profile_path.parent.mkdir(parents=True)
        profile_path.write_text(json.dumps({
            "schemaVersion": 1, "profileId": "test-flat-v1", "baseline": 236,
            "references": [{"role": "anchor", "path": self.store.relative(reference),
                            "sha256": pipeline.sha256_file(reference)}],
        }), encoding="utf-8")
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="unstyled-import", approved_asset_id="unstyled-import", kind="projectile",
            direction="down-right", pose="display", anchor=None, anchor_mask=None,
            mask_required=False, no_arms=False, near_hand_side=None, far_hand_side=None,
            size_tolerance=3, center_tolerance=2, layer_rule=[], visibility_cap=[],
            composition_id=None, identity_anchor_mask=None, forehead_blaze_min_iou=0.45,
            pose_reference=False, output_master="Tools/artworks/approved/unstyled.png",
            output_preview="Tools/artworks/approved/unstyled_128.png", rights_holder="cty41",
            license="project-owned", provenance="derived", asset_role=None, component_kind=None,
            source_mode=None, style_profile=str(profile_path), target_visible_height=108,
            visible_height_min=106, visible_height_max=110))
        candidate = self.png("Tools/artworks/candidates/manual.png")
        preview = self.root / "Tools/artworks/candidates/manual_128.png"
        pipeline.make_preview(Image.open(candidate).convert("RGBA")).save(preview)
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(reference), previous=str(candidate), reference=str(candidate), candidate=str(candidate),
            output="Tools/artworks/reviews/manual-size.png"))
        with self.assertRaisesRegex(pipeline.PipelineError, "matching equipment style report"):
            pipeline.adopt_reviewed_sprite(self.store, self.ns(
                contract_id=contract["contractId"], source=str(reference), candidate=str(candidate), preview=str(preview),
                size_comparison=comparison["artifact"]["path"], reviewer="cty41", reason="invalid bypass",
                accepted_at="2026-08-24T00:00:00+08:00"))

    def test_register_runtime_copy_requires_identical_approved_source(self):
        contract, _job, _job_args = self.contract_and_job()
        source = self.store.absolute(contract["anchor"]["path"])
        pipeline.register_public_artifacts(self.store, [contract["anchor"]], "project-owned-gpt-generated")
        manifest_path = self.root / "Tools/public-release/asset-provenance.json"
        manifest = pipeline.load_json(manifest_path)
        source_entry = next(entry for entry in manifest["entries"] if entry["path"] == contract["anchor"]["path"])
        source_entry["license"] = "project-owned"
        pipeline.write_json_idempotent(manifest_path, manifest)
        target = self.root / "godot/assets/units/hero.png"; target.parent.mkdir(parents=True); target.write_bytes(source.read_bytes())
        result = pipeline.register_runtime_copy(self.store, self.ns(source=str(source), target=str(target)))
        self.assertEqual(result["source"]["sha256"], result["target"]["sha256"])
        target_entry = next(entry for entry in pipeline.load_json(manifest_path)["entries"]
                            if entry["path"] == "godot/assets/units/hero.png")
        self.assertEqual(target_entry["license"], "project-owned")
        target_entry["license"] = "CC-BY-4.0"
        manifest = pipeline.load_json(manifest_path)
        next(entry for entry in manifest["entries"] if entry["path"] == target_entry["path"])["license"] = "CC-BY-4.0"
        pipeline.write_json_idempotent(manifest_path, manifest)
        pipeline.register_runtime_copy(self.store, self.ns(source=str(source), target=str(target)))
        normalized = next(entry for entry in pipeline.load_json(manifest_path)["entries"]
                          if entry["path"] == target_entry["path"])
        self.assertEqual(normalized["license"], "project-owned")
        Image.new("RGBA", (256, 256), (1, 2, 3, 255)).save(target)
        with self.assertRaisesRegex(pipeline.PipelineError, "byte-identical"):
            pipeline.register_runtime_copy(self.store, self.ns(source=str(source), target=str(target)))

    def test_reviewed_import_requires_native_rgba_and_matching_comparison_candidate(self):
        contract, _job, _job_args = self.contract_and_job()
        source = self.png("Tools/artworks/concepts/source.png")
        first = self.root / "Tools/artworks/candidates/first.png"; first.parent.mkdir(parents=True)
        second = self.root / "Tools/artworks/candidates/second.png"
        for path, color in ((first, (80, 70, 65, 255)), (second, (90, 80, 75, 255))):
            image = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(image).ellipse((73, 81, 182, 174), fill=color); image.save(path)
        preview = self.root / "Tools/artworks/candidates/preview.png"
        image = Image.new("RGBA", (128, 128), (0, 0, 0, 0)); ImageDraw.Draw(image).ellipse((36, 40, 91, 87), fill=(80, 70, 65, 255)); image.save(preview)
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(source), previous=str(first), reference=str(first), candidate=str(first),
            output="Tools/artworks/reviews/size.png"))
        args = self.ns(contract_id=contract["contractId"], source=str(source), candidate=str(second), preview=str(preview),
                       size_comparison=comparison["artifact"]["path"], reviewer="cty41", reason="test",
                       accepted_at="2026-08-21T00:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "Candidate does not match"):
            pipeline.adopt_reviewed_sprite(self.store, args)
        args.candidate = str(first)
        Image.new("RGB", (128, 128), (0, 0, 0)).save(preview)
        with self.assertRaisesRegex(pipeline.PipelineError, "must be 128x128 RGBA"):
            pipeline.adopt_reviewed_sprite(self.store, args)

    def equipment_contract_fixture(self):
        base = self.png("Tools/artworks/approved/base-anchor.png")
        category = self.png("Tools/artworks/approved/armor-anchor.png", variant=1)
        profile = self.root / "Tools/artworks/equipment/production-v2.json"
        profile.parent.mkdir(parents=True, exist_ok=True)
        profile.write_text(json.dumps({
            "schemaVersion": 2, "profileId": "test-equipment-v2", "baseline": 236,
            "chroma": "00ff00", "chromaTolerance": 48,
            "baseRules": ["flat style"], "negativeConstraints": ["no glossy lighting"],
            "hardGates": {"maxInteriorColorBins": 4, "maxSmoothGradientRatio": 0.0},
            "baseAnchors": [{"role": "base-style-anchor", "path": self.store.relative(base),
                             "sha256": pipeline.sha256_file(base)}],
            "categories": {"armor": {"rules": ["three-quarter view"], "anchors": [{
                "role": "armor-category-anchor", "path": self.store.relative(category),
                "sha256": pipeline.sha256_file(category)}]}},
        }), encoding="utf-8")
        contract = pipeline.create_equipment_contract(self.store, self.ns(
            asset_id="new-armor", approved_asset_id=None, category="armor", production_profile=str(profile),
            target_visible_height=108, visible_height_min=106, visible_height_max=110,
            master_width=256, master_height=256, size_tolerance=3, center_tolerance=2,
            output_master="Tools/artworks/approved/new-armor.png",
            output_preview="Tools/artworks/approved/new-armor_128.png", rights_holder="cty41",
            license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
        return contract, profile

    def test_recontract_equipment_report_preserves_master_and_supports_review(self):
        contract, _profile = self.equipment_contract_fixture()
        candidate_path = self.root / "Tools/artworks/candidates/exact-clean.png"
        candidate_path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        ImageDraw.Draw(image).rectangle((110, 129, 140, 236), fill=(120, 80, 60, 255))
        image.save(candidate_path)
        before = candidate_path.read_bytes()
        attempt = {"schemaVersion": 4, "attemptId": "job-exact-a001", "jobId": "job-exact", "state": "prepared",
                   "reviewedRecontractId": "recontract-test", "artifacts": {"prepared": {
                       "path": self.store.relative(candidate_path), "sha256": pipeline.sha256_file(candidate_path)}}}
        pipeline._bind_recontract_equipment_report(self.store, attempt, contract)
        self.assertEqual(before, candidate_path.read_bytes())
        self.assertTrue(pipeline.load_json(self.store.absolute(attempt["report"]["path"]))["passed"])
        with Image.open(self.store.absolute(attempt["artifacts"]["equipmentPreview"]["path"])) as preview:
            self.assertEqual(pipeline.clean_exact_chroma(pipeline.make_preview(image)).tobytes(), preview.tobytes())
        pipeline.write_json_idempotent(self.store.record("jobs", "job-exact"), {
            "schemaVersion": 4, "jobId": "job-exact", "contractId": contract["contractId"]})
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt)
        review = pipeline.render_equipment_review(self.store, self.ns(
            attempt_id=attempt["attemptId"], output="Tools/artworks/reviews/exact.png"))
        self.assertIn("equipmentPanel", review["outputs"])
        image.putpixel((110, 129), (0, 255, 0, 2)); image.save(candidate_path)
        with self.assertRaisesRegex(pipeline.PipelineError, "technical gate failed"):
            pipeline._bind_recontract_equipment_report(self.store, attempt, contract)

    def test_exact_chroma_cleanup_preserves_non_key_colors_and_alpha(self):
        pixels = [(0, 255, 0, 2), (0, 255, 0, 3), (255, 0, 255, 1),
                  (255, 0, 255, 255), (12, 34, 56, 0), (0, 254, 0, 2),
                  (254, 0, 255, 3), (100, 139, 100, 255), (10, 20, 30, 1)]
        image = Image.new("RGBA", (len(pixels), 1)); image.putdata(pixels)
        cleaned = pipeline.clean_exact_chroma(image)
        self.assertEqual([(0, 0, 0, 0)] * 5 + pixels[5:], pipeline.pixel_data(cleaned))
        self.assertEqual(pixels, pipeline.pixel_data(image))

    def test_equipment_lanczos_cleanup_removes_real_resampled_key_pixels(self):
        contract, _profile = self.equipment_contract_fixture()
        source = self.root / "Tools/artworks/concepts/resampled.png"
        source.parent.mkdir(parents=True)
        image = Image.new("RGBA", (160, 160))
        ImageDraw.Draw(image).ellipse((0, 0, 159, 159), fill=(100, 139, 100, 255))
        image.save(source)
        # This legal muted green survives the source keyer; Lanczos creates exact green at alpha=1.
        resized = image.resize((108, 108), Image.Resampling.LANCZOS)
        self.assertTrue(any(p[:3] == (0, 255, 0) and p[3] for p in pipeline.pixel_data(resized)))
        report = pipeline.prepare_equipment_candidate(self.store, self.ns(
            contract_id=contract["contractId"], source=str(source),
            output="Tools/artworks/candidates/resampled.png",
            preview="Tools/artworks/candidates/resampled_128.png", attempt_id=None))
        self.assertTrue(report["passed"], report["issues"])
        for key in ("candidate", "preview"):
            with Image.open(self.store.absolute(report[key]["path"])) as output:
                pixels = pipeline.pixel_data(output)
            self.assertFalse(any(p[3] and p[:3] in {(0, 255, 0), (255, 0, 255)} for p in pixels))
            self.assertFalse(any(not p[3] and any(p[:3]) for p in pixels))
            self.assertIn((100, 139, 100, 255), pixels)

    def test_technical_gate_rejects_low_alpha_exact_keys(self):
        path = self.png("Tools/artworks/candidates/low-alpha.png")
        for color in ((0, 255, 0), (255, 0, 255)):
            for alpha in (1, 2, 3, 255):
                with self.subTest(color=color, alpha=alpha):
                    image = Image.new("RGBA", (256, 256))
                    image.putpixel((128, 128), (*color, alpha)); image.save(path)
                    _, issues = pipeline.inspect_technical(path, "projectile")
                    self.assertIn("exact_chroma_residue", issues)

    def test_equipment_strict_rejects_actual_approved_master_and_preview_residue(self):
        self.test_equipment_approval_requires_matching_cty41_style_verdict()
        for name in ("approval.png", "approval_128.png"):
            path = self.root / "Tools/artworks/candidates" / name
            with Image.open(path) as opened:
                image = opened.convert("RGBA")
            image.putpixel((20, 20), (0, 255, 0, 2))
            image.putpixel((21, 20), (255, 0, 255, 3))
            image.putpixel((22, 20), (12, 34, 56, 0)); image.save(path)
            issues = pipeline.strict_check(self.store, False)["issues"]
            self.assertTrue(any(issue.startswith("equipment_exact_chroma_residue:") and issue.endswith(name) for issue in issues), issues)
            self.assertTrue(any(issue.startswith("equipment_transparent_rgb_nonzero:") and issue.endswith(name) for issue in issues), issues)
            with self.assertRaisesRegex(pipeline.PipelineError, "equipment_exact_chroma_residue"):
                pipeline.strict_check(self.store, True)

    def test_equipment_contract_binds_category_anchors_and_requires_invocation(self):
        contract, _profile = self.equipment_contract_fixture()
        self.assertTrue(contract["requiresInvocation"])
        self.assertEqual("armor", contract["equipmentProductionSpec"]["category"])
        self.assertEqual(2, len(contract["equipmentProductionSpec"]["anchors"]))
        category_anchor = self.store.absolute(contract["equipmentProductionSpec"]["anchors"][-1]["path"])
        Image.new("RGBA", (256, 256), (1, 2, 3, 255)).save(category_anchor)
        with self.assertRaisesRegex(pipeline.PipelineError, "anchor hash mismatch"):
            pipeline._equipment_style_profile(self.store, contract)

    def test_local_reference_descriptor_never_records_absolute_path_or_public_provenance(self):
        external = Path(self.temp.name).parent / f"third-party-{Path(self.temp.name).name}.png"
        Image.new("RGB", (8, 8), (1, 2, 3)).save(external)
        try:
            record = pipeline.register_local_reference(self.store, self.ns(
                path=str(external), source_label="Diablo II silhouette reference", role="shape_reference"))
            serialized = json.dumps(record)
            self.assertNotIn(str(external.parent), serialized)
            self.assertNotIn("path", record)
            manifest = json.loads((self.root / "Tools/public-release/asset-provenance.json").read_text(encoding="utf-8"))
            self.assertEqual([], manifest["entries"])
        finally:
            external.unlink(missing_ok=True)

    def test_equipment_prompt_uses_descriptors_and_preserve_processing_is_not_quantized(self):
        contract, _profile = self.equipment_contract_fixture()
        prompt = self.root / "Tools/artworks/prompts/equipment.md"
        prompt.parent.mkdir(parents=True); prompt.write_text("make leather armor", encoding="utf-8")
        local = self.root / "local-ref.png"; Image.new("RGB", (8, 8), (9, 8, 7)).save(local)
        descriptor = pipeline.register_local_reference(self.store, self.ns(
            path=str(local), source_label="local shape cue", role="shape_reference"))
        job = pipeline.create_job(self.store, self.ns(contract_id=contract["contractId"], prompt=str(prompt),
            input=[], pose_guide_id=None, series_id=None, pose_id=None,
            local_reference_id=[descriptor["localReferenceId"]]))
        compiled = pipeline.compile_equipment_prompt(self.store, self.ns(
            job_id=job["jobId"], output="Tools/artworks/pipeline/compiled/equipment.md"))
        text = self.store.absolute(compiled["artifact"]["path"]).read_text(encoding="utf-8")
        self.assertIn("local shape cue", text)
        self.store.record("local-references", descriptor["localReferenceId"]).unlink()
        with self.assertRaisesRegex(pipeline.PipelineError, "descriptor is missing"):
            pipeline.compile_equipment_prompt(self.store, self.ns(
                job_id=job["jobId"], output="Tools/artworks/pipeline/compiled/equipment-missing-ref.md"))
        source = self.root / "Tools/artworks/concepts/gradient.png"; source.parent.mkdir(parents=True)
        image = Image.new("RGBA", (160, 160), (0, 255, 0, 255)); draw = ImageDraw.Draw(image)
        for y in range(20, 140):
            draw.line((40, y, 119, y), fill=(y, 60, 40, 255))
        image.save(source)
        report = pipeline.prepare_equipment_candidate(self.store, self.ns(
            contract_id=contract["contractId"], source=str(source),
            output="Tools/artworks/candidates/armor.png", preview="Tools/artworks/candidates/armor_128.png",
            attempt_id=None))
        self.assertTrue(report["passed"])
        self.assertFalse(report["quantized"])
        self.assertIn("equipment_palette_complexity_review", report["advisories"])

    def test_new_equipment_contract_rejects_reviewed_import(self):
        contract, _profile = self.equipment_contract_fixture()
        with self.assertRaisesRegex(pipeline.PipelineError, "cannot use reviewed_import"):
            pipeline.adopt_reviewed_sprite(self.store, self.ns(
                contract_id=contract["contractId"], source="missing", candidate="missing", preview="missing",
                size_comparison="missing", reviewer="cty41", reason="bypass",
                accepted_at="2026-08-25T00:00:00+08:00"))

    def test_equipment_approval_requires_matching_cty41_style_verdict(self):
        contract, _profile = self.equipment_contract_fixture()
        prompt = self.root / "Tools/artworks/prompts/equipment-approval.md"
        prompt.parent.mkdir(parents=True); prompt.write_text("make armor", encoding="utf-8")
        job = pipeline.create_job(self.store, self.ns(contract_id=contract["contractId"], prompt=str(prompt),
            input=[], pose_guide_id=None, series_id=None, pose_id=None, local_reference_id=[]))
        compiled = pipeline.compile_equipment_prompt(self.store, self.ns(
            job_id=job["jobId"], output="Tools/artworks/pipeline/compiled/equipment-approval.md"))
        attempt = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=None, feedback_id=None, technical_remediation=False))
        invocation = pipeline.begin_generation(self.store, self.ns(
            attempt_id=attempt["attemptId"], compiled_prompt_id=compiled["compiledPromptId"],
            provider="fixture", started_at="2026-08-25T01:00:00+08:00"))
        source = self.root / "Tools/artworks/concepts/approved-flow.png"; source.parent.mkdir(parents=True)
        image = Image.new("RGBA", (160, 160), (0, 255, 0, 255)); ImageDraw.Draw(image).rectangle((40, 20, 119, 139), fill=(80, 60, 40, 255)); image.save(source)
        pipeline.ingest(self.store, self.ns(
            attempt_id=attempt["attemptId"], source=str(source), invocation_id=invocation["invocationId"]))
        swapped = self.root / "Tools/artworks/concepts/swapped-after-ingest.png"
        Image.new("RGBA", (160, 160), (70, 50, 30, 255)).save(swapped)
        with self.assertRaisesRegex(pipeline.PipelineError, "generation lineage"):
            pipeline.prepare_equipment_candidate(self.store, self.ns(
                contract_id=contract["contractId"], source=str(swapped), attempt_id=attempt["attemptId"],
                output="Tools/artworks/candidates/swapped.png", preview="Tools/artworks/candidates/swapped_128.png"))
        pipeline.prepare_equipment_candidate(self.store, self.ns(
            contract_id=contract["contractId"], source=str(source), attempt_id=attempt["attemptId"],
            output="Tools/artworks/candidates/approval.png", preview="Tools/artworks/candidates/approval_128.png"))
        pipeline.render_equipment_review(self.store, self.ns(
            attempt_id=attempt["attemptId"], output="Tools/artworks/reviews/equipment-panel.png"))
        decision = self.ns(attempt_id=attempt["attemptId"], reviewer="cty41", reason="accepted",
                           decided_at="2026-08-25T01:05:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "style verdict"):
            pipeline.decide(self.store, decision, "approved")
        pipeline.record_equipment_style_verdict(self.store, self.ns(
            attempt_id=attempt["attemptId"], reviewer="cty41", decision="approved", reason="style matches",
            decided_at="2026-08-25T01:04:00+08:00"))
        receipt = pipeline.decide(self.store, decision, "approved")
        self.assertEqual("approved", receipt["decision"])

    def test_art_direction_registry_is_idempotent_and_binds_schema_v4_contract(self):
        paths = self.art_direction_fixture()
        first = pipeline.register_family_profile(self.store, self.ns(source=str(paths["actor.json"])))
        second = pipeline.register_family_profile(self.store, self.ns(source=str(paths["actor.json"])))
        self.assertEqual(first, second)

        args = pipeline.build_parser().parse_args([
            "--root", str(self.root), "create-contract",
            "--asset-id", "actor-with-art-direction", "--kind", "ground_character",
            "--direction", "down-right", "--pose", "idle", "--no-mask-required",
            "--output-master", "Tools/artworks/output/actor.png",
            "--output-preview", "Tools/artworks/output/actor_128.png",
            "--family-profile-id", "actor-v1", "--brief-id", "brief-v1",
        ])
        contract = pipeline.run(args)
        self.assertEqual(4, contract["schemaVersion"])
        self.assertEqual("project-v1", contract["artDirectionSpec"]["profileId"])
        self.assertEqual("material-v1", contract["materialLanguageSpec"]["profileId"])
        self.assertEqual("actor", contract["familyProfileSpec"]["family"])
        self.assertEqual(["CASE-001"], contract["acceptanceCaseIds"])
        self.assertEqual([], pipeline.strict_check(self.store, False)["issues"])

    def test_art_direction_contract_requires_complete_binding_and_strict_reports_hash_drift(self):
        paths = self.art_direction_fixture()
        with self.assertRaisesRegex(pipeline.PipelineError, "both --family-profile-id and --brief-id"):
            args = pipeline.build_parser().parse_args([
                "--root", str(self.root), "create-contract",
                "--asset-id", "bad-binding", "--kind", "ground_character",
                "--direction", "down-right", "--pose", "idle", "--no-mask-required",
                "--output-master", "Tools/artworks/output/bad.png",
                "--output-preview", "Tools/artworks/output/bad_128.png",
                "--family-profile-id", "actor-v1",
            ])
            pipeline.run(args)
        family = json.loads(paths["actor.json"].read_text(encoding="utf-8"))
        family["rules"].append("drift")
        paths["actor.json"].write_text(json.dumps(family), encoding="utf-8")
        issues = pipeline.strict_check(self.store, False)["issues"]
        self.assertIn("art_direction_registry_hash:family-profiles:actor-v1", issues)

    def test_only_cty41_approved_anchor_can_bind_art_direction_contract(self):
        self.art_direction_fixture()
        candidate = self.png("Tools/artworks/candidates/anchor.png")
        review = self.png("Tools/artworks/reviews/anchor-board.png")
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer cty41"):
            pipeline.record_anchor_verdict(self.store, self.ns(
                candidate=str(candidate), family="actor", responsibility=["identity"], excluded_use=[],
                review=str(review), decision="approved-anchor", reviewer="agent", reason="invalid",
                decided_at="2026-09-03T12:00:00+08:00"))
        rejected = pipeline.record_anchor_verdict(self.store, self.ns(
            candidate=str(candidate), family="actor", responsibility=["identity"], excluded_use=[],
            review=str(review), decision="rejected-as-anchor", reviewer="cty41", reason="negative only",
            decided_at="2026-09-03T12:00:00+08:00"))
        values = [
            "--root", str(self.root), "create-contract", "--asset-id", "actor",
            "--kind", "ground_character", "--direction", "down-right", "--pose", "idle",
            "--no-mask-required", "--output-master", "Tools/artworks/output/actor.png",
            "--output-preview", "Tools/artworks/output/actor_128.png", "--family-profile-id", "actor-v1",
            "--brief-id", "brief-v1", "--anchor-verdict-id", rejected["anchorVerdictId"],
        ]
        with self.assertRaisesRegex(pipeline.PipelineError, "only approved-anchor"):
            pipeline.run(pipeline.build_parser().parse_args(values))

    def test_acceptance_cases_and_cty41_verdict_form_hash_bound_art_direction_gate(self):
        self.art_direction_fixture()
        contract = pipeline.run(pipeline.build_parser().parse_args([
            "--root", str(self.root), "create-contract", "--asset-id", "actor",
            "--kind", "ground_character", "--direction", "down-right", "--pose", "idle",
            "--no-mask-required", "--output-master", "Tools/artworks/output/actor.png",
            "--output-preview", "Tools/artworks/output/actor_128.png", "--family-profile-id", "actor-v1",
            "--brief-id", "brief-v1",
        ]))
        job = {"schemaVersion": 4, "jobId": "job-v4", "contractId": contract["contractId"], "state": "ready"}
        pipeline.write_json_idempotent(self.store.record("jobs", "job-v4"), job)
        candidate = self.png("Tools/artworks/pipeline/artifacts/job-v4/job-v4-a001/prepared.png")
        attempt = {"schemaVersion": 4, "attemptId": "job-v4-a001", "jobId": "job-v4", "state": "review_pending",
                   "artifacts": {"prepared": {"path": self.store.relative(candidate), "sha256": pipeline.sha256_file(candidate)}}}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt)
        panel = self.png("Tools/artworks/reviews/panel.png")
        pending = pipeline.record_acceptance_case_result(self.store, self.ns(
            attempt_id=attempt["attemptId"], case_id="CASE-001", evidence=[f"candidate={panel}"],
            automated_fact=["geometry passed"], automated_result="passed", human_check=["style pending"],
            human_decision="pending", reviewer="agent", reason="awaiting review",
            decided_at="2026-09-03T12:00:00+08:00"))
        self.assertEqual("pending", pending["humanDecision"])
        pending_review = pipeline.render_art_direction_review(self.store, self.ns(
            attempt_id=attempt["attemptId"], panel=[f"candidate-master={panel}"],
            output="Tools/artworks/reviews/art-direction-pending.png"))
        current = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
        with self.assertRaisesRegex(pipeline.PipelineError, "all required acceptance cases must pass"):
            pipeline._validate_art_direction_approval(self.store, current, contract)
        result = pipeline.record_acceptance_case_result(self.store, self.ns(
            attempt_id=attempt["attemptId"], case_id="CASE-001", evidence=[f"candidate={panel}"],
            automated_fact=["geometry passed"], automated_result="passed", human_check=["style reads"],
            human_decision="passed", reviewer="cty41", reason="accepted", decided_at="2026-09-03T12:01:00+08:00"))
        self.assertEqual("passed", result["humanDecision"])
        review = pipeline.render_art_direction_review(self.store, self.ns(
            attempt_id=attempt["attemptId"], panel=[f"candidate-master={panel}"],
            output="Tools/artworks/reviews/art-direction.png"))
        self.assertNotEqual(pending_review["artDirectionReviewId"], review["artDirectionReviewId"])
        current = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
        with self.assertRaisesRegex(pipeline.PipelineError, "verdict is required"):
            pipeline._validate_art_direction_approval(self.store, current, contract)
        verdict = pipeline.record_art_direction_verdict(self.store, self.ns(
            attempt_id=attempt["attemptId"], review_id=review["artDirectionReviewId"], decision="approved",
            accept_warning=[], reviewer="cty41", reason="all cases passed",
            decided_at="2026-09-03T12:02:00+08:00"))
        current = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
        self.assertEqual(verdict["artDirectionVerdictId"],
                         pipeline._validate_art_direction_approval(self.store, current, contract)["artDirectionVerdictId"])

    def test_resolve_prepare_transaction_commits_only_from_matching_artifact_evidence(self):
        prepared = self.png("Tools/artworks/pipeline/artifacts/job-recover/job-recover-a001/prepared.png")
        artifact = {"path": self.store.relative(prepared), "sha256": pipeline.sha256_file(prepared)}
        attempt = {"schemaVersion": 2, "attemptId": "job-recover-a001", "jobId": "job-recover",
                   "state": "prepared", "artifacts": {"prepared": artifact},
                   "preparation": {"chroma": "00ff00", "chromaTolerance": 42}}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt)
        payload = {"attemptId": attempt["attemptId"], "chroma": "0,255,0", "chromaTolerance": 42}
        transaction_id = pipeline.stable_id("transaction", {"operation": "prepare", **payload})
        pipeline.write_json_idempotent(self.store.record("transactions", transaction_id),
            {"schemaVersion": 2, "transactionId": transaction_id, "operation": "prepare", "payload": payload, "state": "started"})
        resolved = pipeline.resolve_transaction(self.store, self.ns(
            transaction_id=transaction_id, reviewer="cty41", reason="recover",
            decided_at="2026-09-08T12:00:00+00:00"))
        self.assertEqual("committed", resolved["state"])
        self.assertEqual(artifact, resolved["resolution"]["evidence"])
        original = pipeline.load_json(self.store.record("transactions", transaction_id))
        self.assertEqual("started", original["state"])
        repeated = pipeline.resolve_transaction(self.store, self.ns(
            transaction_id=transaction_id, reviewer="cty41", reason="again",
            decided_at="2026-09-08T12:01:00+00:00"))
        self.assertEqual(resolved["resolution"]["transactionResolutionId"],
                         repeated["resolution"]["transactionResolutionId"])

    def test_composition_hidden_tip_flag_must_be_boolean(self):
        anchor = self.png("Tools/artworks/anchor.png")
        spec_path = self.root / "Tools/artworks/composition.json"
        spec = {"canvas": [256, 256], "coreAxis": {}, "footCenter": {},
                "weapon": {"tipMayBeOccluded": "yes"}, "forbiddenRegions": [],
                "equipmentState": {"scabbard": "present"}}
        spec_path.write_text(json.dumps(spec), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "must be boolean"):
            pipeline.create_composition(self.store, self.ns(asset_id="actor", spec=str(spec_path), anchor=str(anchor)))
        spec["weapon"]["tipMayBeOccluded"] = True
        spec_path.write_text(json.dumps(spec), encoding="utf-8")
        self.assertTrue(pipeline.create_composition(self.store, self.ns(
            asset_id="actor", spec=str(spec_path), anchor=str(anchor)))["compositionId"])

    def test_reviewer_qualification_supersession_is_explicit_and_effective(self):
        proof = self.root / "Tools/artworks/proof.json"; proof.parent.mkdir(parents=True, exist_ok=True)
        proof.write_text(json.dumps({"schemaVersion": 4}), encoding="utf-8")
        binding = {"path": self.store.relative(proof), "sha256": pipeline.sha256_file(proof)}
        audit_bindings = []
        for index in range(3):
            audit_id = f"audit-{index}"; audit_path = self.root / f"Tools/artworks/{audit_id}.json"
            audit_path.write_text(json.dumps({"schemaVersion": 4, "modelReviewAuditId": audit_id,
                                              "reviewer": "cty41", "verdict": "confirmed", "finding": "confirmed"}), encoding="utf-8")
            audit_bindings.append({"modelReviewAuditId": audit_id, "path": self.store.relative(audit_path),
                                   "sha256": pipeline.sha256_file(audit_path)})
        common = {"state": "auto-retry-qualified", "reviewer": "cty41", "ruleId": "RULE", "ruleVersion": 1,
                  "model": "model", "effort": "medium", "reviewerPromptId": "prompt", "reviewerPromptSha256": "a" * 64,
                  "compiledPolicyId": "policy", "compiledPolicySha256": "b" * 64, "caseSetVersion": "cases",
                  "reviewRule": binding, "compiledPolicy": binding, "reviewerPrompt": binding}
        old_payload = {**common, "qualifiedAt": "2026-09-08T10:00:00+00:00"}
        old_id = pipeline.stable_id("reviewer-qualification", old_payload)
        old = {"schemaVersion": 4, "reviewerQualificationId": old_id, **old_payload}
        new_payload = {**common, "promptOnlyComparison": {"promptComparisonId": "comparison", **binding},
                       "qualificationAudits": audit_bindings,
                       "qualifiedAt": "2026-09-08T11:00:00+00:00"}
        new_id = pipeline.stable_id("reviewer-qualification", new_payload)
        new = {"schemaVersion": 4, "reviewerQualificationId": new_id, **new_payload}
        pipeline.write_json_idempotent(self.store.record("reviewer-qualifications", old_id), old, immutable=True)
        pipeline.write_json_idempotent(self.store.record("reviewer-qualifications", new_id), new, immutable=True)
        with self.assertRaisesRegex(pipeline.PipelineError, "valid prompt-only comparison"):
            pipeline.supersede_reviewer_qualification(self.store, self.ns(
                old_qualification_id=old_id, new_qualification_id=new_id, reviewer="cty41", reason="fake proof",
                superseded_at="2026-09-08T12:00:00+00:00"))

    def test_invalid_qualification_proof_fails_closed_at_runtime(self):
        _attempt, _packet, _invocation, policy_id = self._model_review_fixture("invalid-proof")
        qualification = self._write_test_qualification(
            "rule-invalid-proof", policy_id, self.root / "Tools/artworks/invalid-proof-reviewer-prompt.txt")
        audit_path = self.store.absolute(qualification["qualificationAudits"][0]["path"])
        audit = json.loads(audit_path.read_text(encoding="utf-8")); audit["verdict"] = "rejected"
        audit_path.write_text(json.dumps(audit), encoding="utf-8")
        self.assertNotIn(qualification["reviewerQualificationId"],
                         {item["reviewerQualificationId"] for item in pipeline._effective_qualifications(self.store)})

    def test_automatic_retry_child_creation_is_crash_idempotent(self):
        attempt, _packet, _invocation, _policy_id = self._model_review_fixture("retry-idempotent")
        prompt_delta = {"promptDeltaId": "delta", "operations": [], "promptDelta": []}

        first = pipeline._create_model_retry_attempt(self.store, attempt, prompt_delta)
        second = pipeline._create_model_retry_attempt(self.store, attempt, prompt_delta)

        self.assertEqual(first, second)
        self.assertEqual(f"{attempt['jobId']}-a002", first["attemptId"])
        self.assertEqual(2, first["generationRound"])
        self.assertEqual(2, len(pipeline.list_attempts(self.store, attempt["jobId"])))

    def test_automatic_retry_never_creates_attempt_a004(self):
        attempt, packet, invocation, policy_id = self._model_review_fixture("no-a004")
        result = self._record_retry_result(packet, invocation, policy_id, "no-a004")
        self._write_test_qualification("rule-no-a004", policy_id,
                                       self.root / "Tools/artworks/no-a004-reviewer-prompt.txt")
        for ordinal in (2, 3):
            extra = {**attempt, "attemptId": f"{attempt['jobId']}-a{ordinal:03d}", "ordinal": ordinal,
                     "generationRound": 1, "state": "ready", "modelReviewResultRecordId": None}
            pipeline.write_json_idempotent(self.store.record("attempts", extra["attemptId"]), extra, immutable=True)
        applied = pipeline.apply_model_review(self.store, self.ns(
            model_review_result_record_id=result["modelReviewResultRecordId"], compiled_review_policy_id=policy_id,
            reviewer_prompt_id="reviewer-v1", case_set_version="cases-v1"))
        self.assertEqual("human_review_required", applied["decision"]["action"])
        self.assertFalse(self.store.record("attempts", f"{attempt['jobId']}-a004").exists())

    def test_transaction_resolution_rejects_partial_conflicting_evidence(self):
        prepared = self.png("Tools/artworks/pipeline/artifacts/job-partial/job-partial-a001/prepared.png")
        attempt = {"schemaVersion": 2, "attemptId": "job-partial-a001", "jobId": "job-partial", "state": "prepared",
                   "artifacts": {"prepared": {"path": self.store.relative(prepared), "sha256": pipeline.sha256_file(prepared)}},
                   "preparation": {"chroma": "ff00ff", "chromaTolerance": 42}}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt)
        payload = {"attemptId": attempt["attemptId"], "chroma": "0,255,0", "chromaTolerance": 42}
        transaction_id = pipeline.stable_id("transaction", {"operation": "prepare", **payload})
        pipeline.write_json_idempotent(self.store.record("transactions", transaction_id),
            {"schemaVersion": 2, "transactionId": transaction_id, "operation": "prepare", "payload": payload, "state": "started"})
        with self.assertRaisesRegex(pipeline.PipelineError, "partial or conflicting"):
            pipeline.resolve_transaction(self.store, self.ns(transaction_id=transaction_id, reviewer="cty41",
                reason="do not guess", decided_at="2026-09-08T12:00:00+00:00"))
        self.assertFalse(any((self.store.pipeline / "transaction-resolutions").glob("*.json")))

    def exact_chroma_processing_fixture(self):
        source = self.root / "Tools/artworks/exact-source.png"
        source.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (16, 16), (80, 60, 40, 255))
        image.putpixel((2, 3), (0, 255, 0, 2)); image.putpixel((4, 5), (255, 0, 255, 3))
        image.putpixel((0, 0), (12, 34, 56, 0)); image.save(source)
        output = source.with_name("exact-output.png")
        image.putpixel((2, 3), (0, 0, 0, 0)); image.putpixel((4, 5), (0, 0, 0, 0)); image.save(output)
        attempt = {"attemptId": "job-exact-a001", "artifacts": {"prepared": pipeline._bound_artifact(self.store, str(source))}}
        candidate = pipeline._bound_artifact(self.store, str(output))
        processing = {"schemaVersion": 2, "operation": "deterministic-exact-chroma-pixel-cleanup",
                      "sourceAttemptId": attempt["attemptId"], "sourcePreparedSha256": pipeline.sha256_file(source),
                      "outputSha256": candidate["sha256"], "maxAlpha": 3,
                      "pixels": [{"x": 2, "y": 3, "rgba": [0, 255, 0, 2]},
                                 {"x": 4, "y": 5, "rgba": [255, 0, 255, 3]}]}
        return attempt, candidate, processing

    def test_exact_chroma_recontract_replays_only_two_bound_pixels(self):
        attempt, candidate, processing = self.exact_chroma_processing_fixture()
        pipeline._validate_recontract_processing(self.store, attempt, candidate, processing)
        output = self.store.absolute(candidate["path"])
        with Image.open(output) as opened:
            changed = opened.copy()
        changed.putpixel((0, 0), (0, 0, 0, 0)); changed.save(output)
        candidate["sha256"] = pipeline.sha256_file(output); processing["outputSha256"] = candidate["sha256"]
        with self.assertRaisesRegex(pipeline.PipelineError, "two-pixel replay"):
            pipeline._validate_recontract_processing(self.store, attempt, candidate, processing)

    def test_exact_chroma_recontract_rejects_unsafe_declarations(self):
        attempt, candidate, processing = self.exact_chroma_processing_fixture()
        mutations = [lambda p: p.update(maxAlpha=255), lambda p: p.update(maxAlpha=True),
                     lambda p: p.update(outputSha256="wrong"), lambda p: p.update(sourcePreparedSha256="wrong"),
                     lambda p: p.update(sourceAttemptId="wrong"), lambda p: p.update(extra="erase anything"),
                     lambda p: p["pixels"].pop(), lambda p: p["pixels"].append(p["pixels"][0]),
                     lambda p: p["pixels"].__setitem__(1, p["pixels"][0]),
                     lambda p: p["pixels"][0].update(x=-1), lambda p: p["pixels"][0].update(x=True),
                     lambda p: p["pixels"][0].update(rgba=[0, 254, 0, 2]),
                     lambda p: p["pixels"][0].update(rgba=[0, 255, 0, 0]),
                     lambda p: p["pixels"][0].update(rgba=[0, 255, 0, 4]),
                     lambda p: p["pixels"][0].update(rgba=[0, 255, 0, 3]),
                     lambda p: p["pixels"][0].update(x=6, y=6)]
        for index, mutate in enumerate(mutations):
            with self.subTest(index=index):
                invalid = json.loads(json.dumps(processing)); mutate(invalid)
                with self.assertRaises(pipeline.PipelineError):
                    pipeline._validate_recontract_processing(self.store, attempt, candidate, invalid)
        source = self.store.absolute(attempt["artifacts"]["prepared"]["path"])
        Image.new("RGBA", (16, 16)).save(source)
        with self.assertRaisesRegex(pipeline.PipelineError, "binding"):
            pipeline._validate_recontract_processing(self.store, attempt, candidate, processing)

    def test_recontract_processing_does_not_resurrect_invalid_strict_receipt(self):
        self.test_equipment_approval_requires_matching_cty41_style_verdict()
        source_path = next((self.store.pipeline / "attempts").glob("*.json"))
        source = pipeline.load_json(source_path)
        _fixture, candidate, processing = self.exact_chroma_processing_fixture()
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        ImageDraw.Draw(image).rectangle((110, 129, 140, 236), fill=(120, 80, 60, 255))
        image.putpixel((2, 3), (0, 255, 0, 2)); image.putpixel((4, 5), (255, 0, 255, 3))
        fixture_source = self.store.absolute(_fixture["artifacts"]["prepared"]["path"])
        image.save(fixture_source)
        _fixture["artifacts"]["prepared"]["sha256"] = pipeline.sha256_file(fixture_source)
        image.putpixel((2, 3), (0, 0, 0, 0)); image.putpixel((4, 5), (0, 0, 0, 0))
        image.save(self.store.absolute(candidate["path"]))
        candidate["sha256"] = pipeline.sha256_file(self.store.absolute(candidate["path"]))
        processing.update(sourcePreparedSha256=_fixture["artifacts"]["prepared"]["sha256"], outputSha256=candidate["sha256"])
        source["artifacts"]["prepared"] = _fixture["artifacts"]["prepared"]
        source["feedbackId"] = "feedback-selected"
        pipeline.write_json_idempotent(source_path, source)
        pipeline.write_json_idempotent(self.store.record("feedback", "feedback-selected"),
            {"schemaVersion": 2, "feedbackId": "feedback-selected", "authorType": "human", "verdict": "selected", "reviewer": "cty41"})
        processing.update(sourceAttemptId=source["attemptId"])
        processing_path = self.root / "Tools/artworks/exact-processing.json"
        pipeline.write_json_idempotent(processing_path, processing)
        job = pipeline.load_json(self.store.record("jobs", source["jobId"]))
        result = pipeline.recontract_reviewed_attempt(self.store, self.ns(
            source_attempt_id=source["attemptId"], contract_id=job["contractId"], candidate=candidate["path"],
            processing=str(processing_path), reviewer="cty41", reason="exact cleanup",
            accepted_at="2026-09-08T12:00:00+00:00"))
        receipt = result["receipt"]
        expected_issue = "trusted_artifact_record_invalid:reviewed-recontracts:" + receipt["reviewedRecontractId"]
        self.assertNotIn(expected_issue, pipeline.strict_check(self.store, False)["issues"])
        receipt["reviewer"] = "forged-reviewer"
        pipeline.write_json_idempotent(self.store.record("reviewed-recontracts", receipt["reviewedRecontractId"]), receipt)
        self.assertIn(expected_issue, pipeline.strict_check(self.store, False)["issues"])

    def test_reviewed_recontract_rejects_missing_generation_lineage(self):
        source = self.png("Tools/artworks/source.png")
        contract = {"schemaVersion": 4, "contractId": "contract-source", "direction": "up-left", "pose": "idle",
                    "rights": {"rightsHolder": "cty41", "license": "project-owned", "provenance": "test"}}
        target = {**contract, "contractId": "contract-target"}
        pipeline.write_json_idempotent(self.store.record("contracts", "contract-source"), contract)
        pipeline.write_json_idempotent(self.store.record("contracts", "contract-target"), target)
        job = {"schemaVersion": 4, "jobId": "job-source", "contractId": "contract-source"}
        pipeline.write_json_idempotent(self.store.record("jobs", "job-source"), job)
        feedback = {"schemaVersion": 2, "feedbackId": "feedback-selected", "authorType": "human",
                    "verdict": "selected", "reviewer": "cty41"}
        pipeline.write_json_idempotent(self.store.record("feedback", "feedback-selected"), feedback)
        attempt = {"schemaVersion": 4, "attemptId": "job-source-a001", "jobId": "job-source", "feedbackId": "feedback-selected",
                   "artifacts": {"raw": {"path": self.store.relative(source), "sha256": pipeline.sha256_file(source)},
                                 "prepared": {"path": self.store.relative(source), "sha256": pipeline.sha256_file(source)}}}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt["attemptId"]), attempt)
        with self.assertRaisesRegex(pipeline.PipelineError, "original generation invocation"):
            pipeline.recontract_reviewed_attempt(self.store, self.ns(source_attempt_id=attempt["attemptId"],
                contract_id="contract-target", candidate=None, processing=None, reviewer="cty41", reason="no lineage",
                accepted_at="2026-09-08T12:00:00+00:00"))

    def test_forged_attempt_id_cannot_register_arbitrary_artwork(self):
        artifact = self.png("Tools/artworks/arbitrary.png")
        contract = {"schemaVersion": 2, "contractId": "contract-real", "kind": "ground_character"}
        job = {"schemaVersion": 2, "jobId": "job-real", "contractId": "contract-real", "state": "ready"}
        pipeline.write_json_idempotent(self.store.record("contracts", "contract-real"), contract)
        pipeline.write_json_idempotent(self.store.record("jobs", "job-real"), job)
        forged = {"schemaVersion": 2, "attemptId": "forged-a001", "jobId": "job-real", "ordinal": 1,
                  "state": "ready", "artifacts": {"prepared": {"path": self.store.relative(artifact),
                                                                   "sha256": pipeline.sha256_file(artifact)}}}
        pipeline.write_json_idempotent(self.store.record("attempts", forged["attemptId"]), forged)
        self.assertIn("asset_unregistered:Tools/artworks/arbitrary.png",
                      pipeline.strict_check(self.store, False)["issues"])

    def test_pre_v3_component_migration_rejects_non_cty41_before_writing(self):
        with self.assertRaisesRegex(pipeline.PipelineError, "requires reviewer cty41"):
            pipeline.migrate_component(self.store, self.ns(
                contract_id="missing", source="missing", prepared="missing", processing="missing",
                reviewer="agent", reason="no", accepted_at="2026-09-08T12:00:00+00:00"))
        with self.assertRaisesRegex(pipeline.PipelineError, "is retired"):
            pipeline.migrate_component(self.store, self.ns(
                contract_id="missing", source="missing", prepared="missing", processing="missing",
                reviewer="cty41", reason="closed", accepted_at="2026-09-08T12:00:00+00:00"))


if __name__ == "__main__":
    unittest.main()
