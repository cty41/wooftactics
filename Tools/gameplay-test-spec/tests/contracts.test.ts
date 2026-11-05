import assert from "node:assert/strict";
import test from "node:test";
import { compileScenarioDraft } from "../src/compiler.js";
import {
  buildRegistryContractCoverage,
  contractCoverageGateDiagnostics,
  evaluateContractCoverageGate,
  parseGameplayContracts,
  validateContractRegistry
} from "../src/contracts.js";
import { extractContractCandidates, generateScenarioDraft } from "../src/ollama.js";

const contract = `# Poison\n\n\`\`\`gameplay-contract
id: BUFF-POISON-DURATION-001
status: verified_current
statement: Poison reapplication adds three target actions without stacking damage.
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
dsl_support: partial
\`\`\`\n`;

test("parses explicit gameplay contracts and rejects duplicate registry IDs", () => {
  const parsed = parseGameplayContracts(contract, "poison.md");
  assert.equal(parsed.valid, true, JSON.stringify(parsed.diagnostics));
  assert.equal(parsed.contracts[0].id, "BUFF-POISON-DURATION-001");
  const registry = validateContractRegistry([{ path: "a.md", markdown: contract }, { path: "b.md", markdown: contract }]);
  assert.equal(registry.valid, false);
  assert.ok(registry.diagnostics.some(value => value.code === "ContractIdDuplicate"));
});

test("passes contract IDs from ScenarioDraft into a Godot plan", () => {
  const result = compileScenarioDraft({
    feature: "Battle", scenario: "PoisonDuration", contractIds: ["BUFF-POISON-DURATION-001"], tags: ["status"],
    requiredAdapters: ["Map", "Battle", "UI"], setup: [{
      kind: "loadValidatedCheckpoint",
      parameters: { id: "poison-duration", path: "Tests/checkpoints/poison-duration.json", semanticHash: "a".repeat(64) }
    }],
    actions: [{ kind: "endTurnOnlyUntilTerminal", parameters: {} }],
    assertions: [{ kind: "terminalSummaryOutcomeEquals", expected: "Victory", parameters: {} }], timeoutMs: 10000
  }, { runtime: "Godot" });
  assert.equal(result.valid, true, JSON.stringify(result.diagnostics));
  assert.deepEqual(result.plan?.contractIds, ["BUFF-POISON-DURATION-001"]);
});

test("accepts evidence-bound Ollama candidates and rejects remote hosts", async () => {
  const markdown = "毒素每次成功施加增加三个目标行动周期。";
  const fakeFetch: typeof fetch = async () => new Response(JSON.stringify({ message: { content: JSON.stringify({ candidates: [{
    suggestedId: "BUFF-POISON-DURATION-001", statement: markdown, quote: markdown, startLine: 1, endLine: 1, uncertainties: []
  }] }) } }), { status: 200, headers: { "content-type": "application/json" } });
  const result = await extractContractCandidates(markdown, { fetchImpl: fakeFetch });
  assert.equal(result.valid, true, JSON.stringify(result.diagnostics));
  const rejected = await extractContractCandidates(markdown, { host: "http://example.com:11434", fetchImpl: fakeFetch });
  assert.equal(rejected.valid, false);
  assert.equal(rejected.diagnostics[0].code, "OllamaRemoteHostRejected");
});

test("fails closed when Ollama invents evidence or an unsupported draft", async () => {
  const mismatchFetch: typeof fetch = async () => new Response(JSON.stringify({ message: { content: JSON.stringify({ candidates: [{
    suggestedId: "BUFF-POISON-DURATION-001", statement: "invented", quote: "not present", startLine: 1, endLine: 1, uncertainties: []
  }] }) } }), { status: 200 });
  const mismatch = await extractContractCandidates("actual", { fetchImpl: mismatchFetch });
  assert.equal(mismatch.valid, false);
  assert.equal(mismatch.diagnostics[0].code, "LlmEvidenceMismatch");

  const draftFetch: typeof fetch = async () => new Response(JSON.stringify({ message: { content: JSON.stringify({
    feature: "Battle", scenario: "Invented", contractIds: ["BUFF-POISON-DURATION-001"], tags: [], requiredAdapters: ["Battle"],
    setup: [], actions: [{ kind: "inventCapability", parameters: {} }], assertions: [{ kind: "battleIsActive", expected: true, parameters: {} }], timeoutMs: 10000
  }) } }), { status: 200 });
  const draft = await generateScenarioDraft("rule", "BUFF-POISON-DURATION-001", { fetchImpl: draftFetch });
  assert.equal(draft.valid, true, "schema-only proposal should be returned for deterministic semantic validation");
  const compiled = compileScenarioDraft(draft.value, { runtime: "Godot" });
  assert.equal(compiled.valid, false);
  assert.ok(compiled.diagnostics.some(value => value.code === "UnsupportedActionKind"));
});

test("contract coverage gate fails when a supported contract lacks ScenarioSpec", () => {
  const coverage = [
    { id: "BUFF-REFRESH-STRATEGY-001", status: "covered" as const },
    { id: "GRID-LOGIC-AUTHORITY-001", status: "missing-spec" as const }
  ];
  const gate = evaluateContractCoverageGate(true, coverage, new Set());
  assert.equal(gate.valid, false);
  assert.deepEqual(gate.uncoveredMissingSpec, ["GRID-LOGIC-AUTHORITY-001"]);
  assert.ok(contractCoverageGateDiagnostics(gate).some(value => value.code === "ContractCoverageMissingSpec"));
});

test("contract coverage gate accepts baseline allowlist entries for missing-spec contracts", () => {
  const coverage = [
    { id: "GRID-LOGIC-AUTHORITY-001", status: "missing-spec" as const },
    { id: "MOVE-TERRAIN-COST-001", status: "missing-spec" as const }
  ];
  const gate = evaluateContractCoverageGate(true, coverage, new Set(["GRID-LOGIC-AUTHORITY-001"]));
  assert.equal(gate.valid, false);
  assert.deepEqual(gate.uncoveredMissingSpec, ["MOVE-TERRAIN-COST-001"]);
  const allowedGate = evaluateContractCoverageGate(
    true,
    coverage,
    new Set(["GRID-LOGIC-AUTHORITY-001", "MOVE-TERRAIN-COST-001"])
  );
  assert.equal(allowedGate.valid, true);
  assert.deepEqual(allowedGate.staleAllowlistEntries, []);
});

test("buildRegistryContractCoverage preserves unsupported and failed statuses", () => {
  const registry = validateContractRegistry([{
    path: "sample.md",
    markdown: `# Sample\n\n\`\`\`gameplay-contract
id: GRID-LOGIC-AUTHORITY-001
status: verified_current
statement: Grid logic authority remains in Core.
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/GridTests.cs
dsl_support: supported
\`\`\`\n\n\`\`\`gameplay-contract
id: ATTR-VALUE-BOUNDS-001
status: verified_current
statement: Attribute bounds are enforced.
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/AttributeTests.cs
dsl_support: unsupported
\`\`\`\n`
  }]);
  const references = new Map<string, "covered" | "failed">([
    ["GRID-LOGIC-AUTHORITY-001", "failed"]
  ]);
  const coverage = buildRegistryContractCoverage(registry, references);
  assert.deepEqual(coverage, [
    { id: "GRID-LOGIC-AUTHORITY-001", status: "failed" },
    { id: "ATTR-VALUE-BOUNDS-001", status: "unsupported" }
  ]);
});
