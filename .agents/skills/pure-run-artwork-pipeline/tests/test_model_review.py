from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from copy import deepcopy
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "artwork_review.py"
SPEC = importlib.util.spec_from_file_location("artwork_review", SCRIPT)
review = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = review
SPEC.loader.exec_module(review)

SHA = "a" * 64
SHA2 = "b" * 64


class ModelReviewCoreTests(unittest.TestCase):
    def rule(self, **changes):
        value = {
            "schemaVersion": 1, "ruleId": "CAPSULE-NO-LIMBS", "version": 1,
            "scope": {"project": "pure-run", "family": "poet", "topology": "capsule"},
            "authority": "hard", "autoRetryEligible": True,
            "requiredEvidenceRoles": ["CALIBRATED_256", "SEMANTIC_MASK_OVERLAY"],
            "sources": [{"type": "document", "id": "design-v1"}],
            "positiveCaseIds": ["CASE-PASS"], "negativeCaseIds": ["CASE-FAIL"],
            "lifecycle": "active", "statement": "No arm or leg segments.",
        }
        value.update(changes)
        return value

    def case(self, case_id="CASE-FAIL", polarity="negative", **changes):
        value = {
            "schemaVersion": 1, "caseId": case_id, "version": 1,
            "ruleIds": ["CAPSULE-NO-LIMBS"], "polarity": polarity, "status": "active",
            "scope": {"project": "pure-run", "family": "poet"},
            "artifact": {"path": f"Tools/artworks/cases/{case_id}.png", "sha256": SHA},
            "source": {"attemptId": "job-old-a001", "humanDecision": "rejected" if polarity == "negative" else "approved", "reviewer": "cty41"},
            "reviewOnly": polarity == "negative", "generationInputAllowed": polarity == "positive",
        }
        value.update(changes)
        return value

    def policy(self):
        return review.compile_review_policy(
            {"policyId": "pure-run-policy", "version": 1}, [self.rule()],
            [self.case(), self.case("CASE-PASS", "positive")],
            {"project": "pure-run", "family": "poet", "topology": "capsule", "direction": "up-left", "pose": "idle"},
            acceptance_case_ids=["CASE-FAIL"], feedback_rule_ids=["CAPSULE-NO-LIMBS"],
        )

    def packet(self):
        return review.build_model_review_packet(
            attempt={"attemptId": "job-poet-a001"}, contract={"contractId": "contract-1", "sha256": SHA},
            brief={"briefId": "brief-1", "sha256": SHA2}, compiled_policy=self.policy(),
            artifacts={"calibrated": {"path": "Tools/artworks/candidate.png", "sha256": SHA}},
            evidence=[
                {"role": "CALIBRATED_256", "path": "Tools/artworks/candidate.png", "sha256": SHA},
                {"role": "SEMANTIC_MASK_OVERLAY", "path": "Tools/artworks/mask-overlay.png", "sha256": SHA2},
                {"role": "NEGATIVE_REVIEW_ONLY", "path": "Tools/artworks/cases/bad.png", "sha256": SHA, "generationInput": False},
            ], acceptance_cases=[{"caseId": "CASE-FAIL"}], feedback=[],
            frozen_invariants=["native up-left", "identity"], required_model="gpt-5.6-sol",
        )

    def result(self, packet=None, decision="retry"):
        packet = packet or self.packet()
        return {
            "schemaVersion": 1, "packetId": packet["packetId"], "packetSha256": packet["sha256"],
            "decision": decision, "summary": "Visible limb violates capsule topology.", "strengths": ["native view"],
            "defects": [{"defectId": "D1", "ruleId": "CAPSULE-NO-LIMBS", "acceptanceCaseId": "CASE-FAIL",
                         "severity": "hard", "certainty": "certain", "observed": "An arm segment is visible.",
                         "expected": "Paws attach directly.", "evidenceRoles": ["CALIBRATED_256", "SEMANTIC_MASK_OVERLAY"],
                         "region": {"x": .1, "y": .2, "width": .3, "height": .4}}] if decision == "retry" else [],
            "frozenInvariants": list(packet["frozenInvariants"]),
            "operations": [{"operation": "remove", "target": "arm segment", "instruction": "Remove every arm segment.", "defectId": "D1"}] if decision == "retry" else [],
        }

    def qualification(self, packet=None, policy=None):
        packet = packet or self.packet(); policy = policy or self.policy()
        value = {"state": "auto-retry-qualified", "reviewer": "cty41", "ruleId": "CAPSULE-NO-LIMBS", "ruleVersion": 1,
                 "model": "gpt-5.6-sol", "effort": packet["reasoningEffort"], "reviewerPromptId": "reviewer-v1",
                 "reviewerPromptSha256": SHA, "compiledPolicyId": policy["compiledPolicyId"],
                 "compiledPolicySha256": policy["sha256"], "caseSetVersion": "cases-v1",
                 "promptOnlyComparison": {"promptComparisonId": "comparison-1", "path": "comparison.json", "sha256": SHA}}
        value["reviewerQualificationId"] = review.stable_id("reviewer-qualification", value)
        return value

    def test_stable_id_and_history_derivation_are_deterministic_json(self):
        feedback = {"feedbackId": "feedback-1", "attemptId": "job-a001", "categories": ["topology"], "authorType": "human"}
        first = review.derive_review_history_index_entry(feedback, attempt={"attemptId": "job-a001", "jobId": "job"}, artifact={"path": "Tools/artworks/x.png", "sha256": SHA})
        second = review.derive_review_history_index_entry(feedback, attempt={"attemptId": "job-a001", "jobId": "job"}, artifact={"sha256": SHA, "path": "Tools/artworks/x.png"})
        self.assertEqual(first, second); json.dumps(first)
        with self.assertRaisesRegex(review.ReviewValidationError, "does not match"):
            review.derive_review_history_index_entry(feedback, attempt={"attemptId": "other"})

    def test_case_fitness_audit_classifies_active_split_historical_and_invalid(self):
        base = {"polarity": "negative", "artifact": {"path": "Tools/artworks/x.png", "sha256": SHA},
                "source": {"attemptId": "a", "humanDecision": "rejected", "reviewer": "cty41"},
                "scope": {"project": "pure-run"}, "defectIds": ["D1"], "observableAtReviewSize": True,
                "reviewOnly": True, "hashVerified": True, "artifactExists": True}
        self.assertEqual("active", review.audit_case_fitness(base)["status"])
        split = deepcopy(base); split["defectIds"] = ["D1", "D2"]
        self.assertEqual("needs-splitting", review.audit_case_fitness(split)["status"])
        historical = deepcopy(base); historical["observableAtReviewSize"] = False
        self.assertEqual("historical-only", review.audit_case_fitness(historical)["status"])
        retired = deepcopy(base); retired["containsRetiredAsset"] = True
        self.assertEqual("invalid", review.audit_case_fitness(retired)["status"])

    def test_rule_authority_and_sources_are_strict(self):
        self.assertEqual("hard", review.validate_review_rule(self.rule())["authority"])
        with self.assertRaisesRegex(review.ReviewValidationError, "only hard"):
            review.validate_review_rule(self.rule(authority="advisory"))
        with self.assertRaisesRegex(review.ReviewValidationError, "unknown fields"):
            review.validate_review_rule(self.rule(secretOverride=True))

    def test_negative_case_is_never_generation_input_and_requires_cty41(self):
        review.validate_review_case(self.case())
        with self.assertRaisesRegex(review.ReviewValidationError, "forbidden"):
            review.validate_review_case(self.case(generationInputAllowed=True))
        bad = self.case(); bad["source"]["reviewer"] = "model"
        with self.assertRaisesRegex(review.ReviewValidationError, "cty41"):
            review.validate_review_case(bad)

    def test_scope_matching_and_policy_selection_are_deterministic(self):
        self.assertTrue(review.scope_matches({"family": ["poet", "doge"], "direction": "*"}, {"family": "poet", "direction": "up-left"}))
        self.assertFalse(review.scope_matches({"family": "bat"}, {"family": "poet"}))
        first = self.policy(); second = self.policy()
        self.assertEqual(first["compiledPolicyId"], second["compiledPolicyId"])
        self.assertEqual(["CASE-FAIL", "CASE-PASS"], [c["caseId"] for c in first["cases"]])

    def test_policy_fails_conflicting_versions_and_missing_acceptance_case(self):
        with self.assertRaisesRegex(review.ReviewValidationError, "conflicting"):
            review.compile_review_policy({"policyId": "p", "version": 1}, [self.rule(), self.rule(version=2)], [], {"project": "pure-run", "family": "poet", "topology": "capsule"})
        with self.assertRaisesRegex(review.ReviewValidationError, "missing or inapplicable"):
            review.compile_review_policy({"policyId": "p", "version": 1}, [self.rule()], [], {"project": "pure-run", "family": "poet", "topology": "capsule"}, acceptance_case_ids=["MISSING"])

    def test_effort_maps_only_nontechnical_three_round_budget(self):
        self.assertEqual(["medium", "high", "xhigh"], [review.review_effort(i) for i in (1, 2, 3)])
        self.assertEqual("high", review.review_effort("job-x-a002"))
        for kwargs in ({"qualification_mode": True}, {"technical_remediation": True}):
            with self.assertRaises(review.ReviewValidationError): review.review_effort(1, **kwargs)
        with self.assertRaises(review.ReviewValidationError): review.review_effort(4)

    def test_shadow_packet_supports_only_three_explicit_efforts_and_is_non_authoritative(self):
        case = self.case(evidenceRegion={"x": 0, "y": 0, "width": 1, "height": 1})
        binding = {"reviewCaseRecord": {"reviewCaseRecordId": "case-record-1", "sha256": SHA2}, "caseId": case["caseId"],
                   "artifact": case["artifact"], "humanDecision": "rejected", "humanFeedback": {"feedbackId": "feedback-1", "sha256": SHA2}, "evidenceRegion": case["evidenceRegion"]}
        for effort in ("medium", "high", "xhigh"):
            packet = review.build_shadow_model_review_packet(attempt={"attemptId": "job-old-a001"}, contract={"contractId": "contract-1", "sha256": SHA}, brief={"briefId": "brief-1", "sha256": SHA2}, compiled_policy=self.policy(), case_binding=binding, evidence=[{"role": "NEGATIVE_REVIEW_ONLY", "path": "Tools/artworks/cases/bad.png", "sha256": SHA, "generationInput": False}], frozen_invariants=["identity"], required_model="m", requested_effort=effort)
            self.assertTrue(packet["qualificationMode"]); self.assertFalse(packet["mayChangeAttemptState"])
            self.assertEqual(effort, packet["reasoningEffort"])
        with self.assertRaisesRegex(review.ReviewValidationError, "medium, high, or xhigh"):
            review.build_shadow_model_review_packet(attempt={"attemptId": "job-old-a001"}, contract={"contractId": "contract-1", "sha256": SHA}, brief={"briefId": "brief-1", "sha256": SHA2}, compiled_policy=self.policy(), case_binding=binding, evidence=[], frozen_invariants=[], required_model="m", requested_effort="low")

    def test_packet_builds_hash_bound_payload_with_fixed_permissions(self):
        packet = self.packet(); self.assertEqual("medium", packet["reasoningEffort"])
        self.assertFalse(packet["permissions"]["mayApprove"]); json.dumps(packet)
        self.assertEqual(packet, review.validate_model_review_packet(packet))

    def test_packet_rejects_hash_tamper_elevated_authority_and_negative_input(self):
        packet = self.packet(); packet["attemptId"] = "tampered-a001"
        with self.assertRaisesRegex(review.ReviewValidationError, "packetId"):
            review.validate_model_review_packet(packet)
        packet = self.packet(); packet["permissions"]["mayApprove"] = True
        with self.assertRaisesRegex(review.ReviewValidationError, "authority"):
            review.validate_model_review_packet(packet)
        with self.assertRaisesRegex(review.ReviewValidationError, "negative"):
            review.build_model_review_packet(attempt={"attemptId": "job-a001"}, contract={"contractId": "c", "sha256": SHA}, brief={"briefId": "b", "sha256": SHA}, compiled_policy=self.policy(), artifacts={"raw": {"path": "x.png", "sha256": SHA}}, evidence=[{"role": "NEGATIVE_REVIEW_ONLY", "path": "bad.png", "sha256": SHA, "generationInput": True}], acceptance_cases=[], feedback=[], frozen_invariants=[], required_model="m")

    def test_packet_rejects_absolute_escaping_and_retired_paths(self):
        for path in ("C:/secret.png", "../secret.png", "Tools/artworks/amazon/old.png"):
            with self.assertRaises(review.ReviewValidationError):
                review.build_model_review_packet(attempt={"attemptId": "job-a001"}, contract={"contractId": "c", "sha256": SHA}, brief={"briefId": "b", "sha256": SHA}, compiled_policy=self.policy(), artifacts={"raw": {"path": path, "sha256": SHA}}, evidence=[], acceptance_cases=[], feedback=[], frozen_invariants=[], required_model="m")

    def test_strict_result_happy_path_and_canonical_result_id(self):
        packet = self.packet(); result = review.validate_model_review_result(self.result(packet), packet, self.policy())
        self.assertTrue(result["resultId"].startswith("model-review-result-")); json.dumps(result)

    def test_result_rejects_approval_score_human_claim_and_packet_outside_evidence(self):
        packet = self.packet()
        for field, value in (("approved", True), ("aestheticScore", 9), ("humanDecision", "passed")):
            bad = self.result(packet); bad[field] = value
            with self.assertRaisesRegex(review.ReviewValidationError, "unknown fields"):
                review.validate_model_review_result(bad, packet, self.policy())
        bad = self.result(packet); bad["defects"][0]["evidenceRoles"] = ["RAW_CANDIDATE"]
        with self.assertRaisesRegex(review.ReviewValidationError, "outside"):
            review.validate_model_review_result(bad, packet, self.policy())

    def test_result_rejects_unbound_rule_case_and_frozen_invariant_mutation(self):
        packet = self.packet()
        for field, value in (("ruleId", "UNKNOWN"), ("acceptanceCaseId", "UNKNOWN")):
            bad = self.result(packet); bad["defects"][0][field] = value
            with self.assertRaises(review.ReviewValidationError): review.validate_model_review_result(bad, packet, self.policy())
        bad = self.result(packet); bad["frozenInvariants"] = []
        with self.assertRaisesRegex(review.ReviewValidationError, "frozen"):
            review.validate_model_review_result(bad, packet, self.policy())

    def test_controlled_operations_reject_free_text_actions_and_frozen_changes(self):
        with self.assertRaisesRegex(review.ReviewValidationError, "uncontrolled"):
            review.validate_controlled_operations([{"operation": "rewrite_brief", "target": "brief", "instruction": "change it", "defectId": "D1"}], frozen_invariants=[], defect_ids={"D1"})
        with self.assertRaisesRegex(review.ReviewValidationError, "frozen"):
            review.validate_controlled_operations([{"operation": "adjust", "target": "identity", "instruction": "change face", "defectId": "D1"}], frozen_invariants=["identity"], defect_ids={"D1"})
        with self.assertRaisesRegex(review.ReviewValidationError, "approved positive"):
            review.validate_controlled_operations([{"operation": "restore_from_anchor", "target": "core", "instruction": "restore", "defectId": "D1", "anchorRole": "NEGATIVE_REVIEW_ONLY"}], frozen_invariants=[], defect_ids={"D1"})

    def test_controlled_compilation_has_stable_order_and_id(self):
        ops = [{"operation": "hide", "target": "sheath", "instruction": "Hide lower sheath.", "defectId": "D1"}, {"operation": "remove", "target": "limbs", "instruction": "Remove limbs.", "defectId": "D1"}]
        compiled = review.compile_controlled_operations(ops, frozen_invariants=[], defect_ids={"D1"})
        self.assertEqual("REMOVE limbs: Remove limbs.", compiled["promptDelta"][0])
        self.assertEqual(compiled, review.compile_controlled_operations(list(reversed(ops)), frozen_invariants=[], defect_ids={"D1"}))

    def test_qualified_hard_retry_advances_round(self):
        packet = self.packet(); policy = self.policy(); qualification = self.qualification(packet, policy)
        decision = review.evaluate_automatic_decision(self.result(packet), packet=packet, compiled_policy=policy,
            qualifications=[qualification], model="gpt-5.6-sol", reviewer_prompt_id="reviewer-v1", reviewer_prompt_sha256=SHA, case_set_version="cases-v1")
        self.assertEqual({"action": "automatic_retry", "automaticRetry": True, "nextRound": 2, "nextEffort": "high"}, {k: decision[k] for k in ("action", "automaticRetry", "nextRound", "nextEffort")})

    def test_unqualified_or_drifted_rule_cannot_auto_retry(self):
        packet = self.packet(); policy = self.policy(); qualification = self.qualification(packet, policy)
        for field, value in (("state", "shadow"), ("model", "other"), ("reviewerPromptSha256", SHA2), ("compiledPolicySha256", SHA2)):
            bad = deepcopy(qualification); bad[field] = value
            decision = review.evaluate_automatic_decision(self.result(packet), packet=packet, compiled_policy=policy, qualifications=[bad], model="gpt-5.6-sol", reviewer_prompt_id="reviewer-v1", reviewer_prompt_sha256=SHA, case_set_version="cases-v1")
            self.assertEqual("human_review_required", decision["action"])

    def test_a003_never_creates_a004_and_pass_or_escalation_stops(self):
        packet = self.packet(); packet["reviewRound"] = 3; packet["reasoningEffort"] = "xhigh"
        payload = {k: v for k, v in packet.items() if k not in {"packetId", "sha256"}}
        packet["packetId"] = review.stable_id("model-review-packet", payload)
        packet["sha256"] = __import__("hashlib").sha256(review.canonical_bytes({k: v for k, v in packet.items() if k != "sha256"})).hexdigest()
        decision = review.evaluate_automatic_decision(self.result(packet), packet=packet, compiled_policy=self.policy(), qualifications=[], model="gpt-5.6-sol", reviewer_prompt_id="reviewer-v1", reviewer_prompt_sha256=SHA, case_set_version="cases-v1")
        self.assertEqual("automatic_generation_budget_exhausted", decision["reason"])
        for model_decision, action in (("pass_to_human", "review_pending"), ("escalate_to_human", "human_review_required")):
            p = self.packet(); result = self.result(p, model_decision)
            evaluated = review.evaluate_automatic_decision(result, packet=p, compiled_policy=self.policy(), qualifications=[], model="m", reviewer_prompt_id="p", reviewer_prompt_sha256=SHA, case_set_version="v")
            self.assertEqual(action, evaluated["action"])

    def test_prompt_only_comparison_freezes_like_for_like_raw_counts(self):
        arm = {"schemaVersion": 1, "arm": "prompt-only", "contract": {"contractId": "contract-1", "sha256": SHA},
               "anchors": [{"path": "Tools/artworks/anchor.png", "sha256": SHA2}], "context": {"family": "poet", "pose": "idle"},
               "imageGenBudget": 3, "rawCounts": [{"ruleId": "CAPSULE-NO-LIMBS", "context": {"direction": "up-left"},
               "knownHardErrorsReachedHuman": 1, "repeatedHardErrors": 1, "reviewerMisses": 0, "falsePositives": 0,
               "repeatedHumanFeedback": 1, "imageGenRounds": 3, "escalations": 0}]}
        reviewer_arm = deepcopy(arm); reviewer_arm["arm"] = "reviewer-closed-loop"; reviewer_arm["rawCounts"][0]["imageGenRounds"] = 2
        comparison = review.validate_prompt_only_comparison(arm, reviewer_arm)
        self.assertTrue(comparison["promptComparisonId"].startswith("prompt-only-comparison-")); json.dumps(comparison)
        self.assertEqual(comparison, review.validate_prompt_only_comparison(arm, reviewer_arm))

    def test_legacy_shadow_packet_is_read_only_and_must_match_registered_case(self):
        case = self.case(evidenceRegion={"x": 0, "y": 0, "width": 1, "height": 1})
        binding = {"reviewCaseRecord": {"reviewCaseRecordId": "case-record-1", "sha256": SHA2},
                   "caseId": case["caseId"], "artifact": case["artifact"], "humanDecision": "rejected",
                   "humanFeedback": {"feedbackId": "feedback-1", "sha256": SHA2},
                   "evidenceRegion": case["evidenceRegion"]}
        packet = review.build_shadow_model_review_packet(
            attempt={"attemptId": "job-old-a001"}, contract={"contractId": "contract-1", "sha256": SHA},
            brief={"briefId": "brief-1", "sha256": SHA2}, compiled_policy=self.policy(), case_binding=binding,
            evidence=[{"role": "NEGATIVE_REVIEW_ONLY", "path": "Tools/artworks/cases/bad.png", "sha256": SHA, "generationInput": False}],
            frozen_invariants=["identity"], required_model="m", requested_effort="medium")
        packet.pop("reviewContext"); packet.pop("historicalProvenance")
        packet["artifacts"][0].pop("reviewOnly"); packet["artifacts"][0].pop("generationInput")
        payload = {k: v for k, v in packet.items() if k not in {"packetId", "sha256"}}
        packet["packetId"] = review.stable_id("model-review-packet", payload)
        packet["sha256"] = __import__("hashlib").sha256(review.canonical_bytes({k: v for k, v in packet.items() if k != "sha256"})).hexdigest()
        self.assertEqual(packet, review.validate_model_review_packet(packet))
        packet["artifacts"].append({"role": "RAW_CANDIDATE", "path": "Tools/artworks/extra.png", "sha256": SHA})
        payload = {k: v for k, v in packet.items() if k not in {"packetId", "sha256"}}
        packet["packetId"] = review.stable_id("model-review-packet", payload)
        packet["sha256"] = __import__("hashlib").sha256(review.canonical_bytes({k: v for k, v in packet.items() if k != "sha256"})).hexdigest()
        with self.assertRaisesRegex(review.ReviewValidationError, "historical case artifact"):
            review.validate_model_review_packet(packet)

    def test_automatic_decision_preserves_all_effort_qualifications_for_one_rule(self):
        policy = self.policy(); packet = self.packet()
        qualifications = []
        for effort in ("medium", "high", "xhigh"):
            effort_packet = {**packet, "reasoningEffort": effort}
            qualification = self.qualification(effort_packet, policy)
            qualifications.append(qualification)
        decision = review.evaluate_automatic_decision(
            self.result(packet), packet=packet, compiled_policy=policy, qualifications=qualifications,
            model="gpt-5.6-sol", reviewer_prompt_id="reviewer-v1", reviewer_prompt_sha256=SHA,
            case_set_version="cases-v1")
        self.assertEqual("automatic_retry", decision["action"])
        self.assertEqual([qualifications[0]["reviewerQualificationId"]], decision["qualificationIds"])

    def test_prompt_only_comparison_rejects_scores_and_mismatched_arms(self):
        arm = {"arm": "prompt-only", "contract": {"contractId": "contract-1", "sha256": SHA},
               "anchors": [{"path": "Tools/artworks/anchor.png", "sha256": SHA2}], "context": {"family": "poet"}, "imageGenBudget": 3,
               "rawCounts": [{"ruleId": "RULE", "context": {"pose": "idle"}, "knownHardErrorsReachedHuman": 0, "repeatedHardErrors": 0,
               "reviewerMisses": 0, "falsePositives": 0, "repeatedHumanFeedback": 0, "imageGenRounds": 1, "escalations": 0}]}
        reviewer_arm = deepcopy(arm); reviewer_arm["arm"] = "reviewer-closed-loop"
        scored = deepcopy(arm); scored["aestheticScore"] = 8
        with self.assertRaisesRegex(review.ReviewValidationError, "unknown fields"):
            review.validate_prompt_only_comparison(scored, reviewer_arm)
        mismatched = deepcopy(reviewer_arm); mismatched["imageGenBudget"] = 4
        with self.assertRaisesRegex(review.ReviewValidationError, "mismatched imageGenBudget"):
            review.validate_prompt_only_comparison(arm, mismatched)

    def test_experience_candidates_cannot_self_promote_or_autoqualify_rules(self):
        candidate = {"feedbackId": "f1", "attemptId": "job-a001", "action": "draft-rule", "rationale": "new recurring issue", "status": "proposed", "createdBy": "policy-curator", "ruleDraft": {"lifecycle": "draft", "autoRetryEligible": False}}
        validated = review.validate_experience_candidate(candidate)
        self.assertTrue(validated["lessonCandidateId"].startswith("review-lesson-candidate-")); json.dumps(validated)
        bad = deepcopy(candidate); bad["promoted"] = True; bad["promotedBy"] = "model"
        with self.assertRaisesRegex(review.ReviewValidationError, "self-promote"):
            review.validate_experience_candidate(bad)
        bad = deepcopy(candidate); bad["ruleDraft"]["autoRetryEligible"] = True
        with self.assertRaisesRegex(review.ReviewValidationError, "draft and unqualified"):
            review.validate_experience_candidate(bad)


if __name__ == "__main__":
    unittest.main()
