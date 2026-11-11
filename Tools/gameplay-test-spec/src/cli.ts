#!/usr/bin/env node
import { Command } from "commander";
import { readFile, writeFile, readdir, stat } from "node:fs/promises";
import { join, basename } from "node:path";
import { compileScenarioSpec, compileScenarioDraft } from "./compiler.js";
import { formatGameplayTestDocument, parseGameplayTestDocument } from "./frontmatter.js";
import { generateScenarioSpec, generateSkillGraphSpec, generateSkillGraphSpecFromAnswers, generateGameplayTestFromSpec, type SkillDesignAnswers } from "./generator.js";
import { validateScenarioSpec, validateScenarioDraft } from "./validator.js";
import { compileAuthoringSpec } from "./authoring/compiler.js";
import {
  buildRegistryContractCoverage,
  contractCoverageGateDiagnostics,
  evaluateContractCoverageGate,
  loadAllowMissingSpecAllowlist,
  parseGameplayContracts,
  validateContractRegistry
} from "./contracts.js";
import { extractContractCandidates, generateScenarioDraft, type LlmProviderId, type StructuredLlmProvider } from "./llm.js";
import { loadConfiguredProvider } from "./provider-config.js";
import { createOllamaProvider } from "./providers/ollama-provider.js";
import { doctorOpenCodeGo } from "./providers/opencode-go-provider.js";
import type { ExpectationDiagnostic } from "./schema.js";

const program = new Command();

program.command("validate-authoring-spec")
  .description("Validate an AuthoringAssetSpecV1 JSON document without writing resources")
  .requiredOption("-s, --spec <path>", "input authoring spec JSON")
  .action(async options => {
    const result = compileAuthoringSpec(JSON.parse(await readFile(options.spec, "utf8")));
    printJson({ valid: result.valid, diagnostics: result.diagnostics });
    if (!result.valid) process.exitCode = 1;
  });

program.command("compile-authoring-spec")
  .description("Compile AuthoringAssetSpecV1 JSON to MCP batch arguments")
  .requiredOption("-s, --spec <path>", "input authoring spec JSON")
  .requiredOption("-o, --out <path>", "output compiled authoring batch JSON")
  .action(async options => {
    const result = compileAuthoringSpec(JSON.parse(await readFile(options.spec, "utf8")));
    if (!result.valid || !result.batch) { printJson(result); process.exitCode = 1; return; }
    await writeFile(options.out, `${JSON.stringify(result.batch, null, 2)}\n`, "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .name("gameplay-test-spec")
  .description("Generate, validate, and compile gameplay test specs.")
  .version("0.1.0");

program.command("validate-contracts")
  .description("Validate explicit gameplay-contract blocks in a Markdown document or directory")
  .requiredOption("-d, --doc <path>", "input design document or directory")
  .action(async options => {
    const input = await stat(options.doc);
    const result = input.isDirectory()
      ? validateContractRegistry(await Promise.all((await findMarkdownFiles(options.doc)).map(async path => ({
          path,
          markdown: await readFile(path, "utf8")
        }))))
      : parseGameplayContracts(await readFile(options.doc, "utf8"), options.doc);
    printJson(result);
    process.exitCode = result.valid ? 0 : 1;
  });

program.command("extract-contracts")
  .description("Use the configured LLM provider to propose evidence-bound gameplay contracts")
  .requiredOption("-d, --doc <path>", "input design document")
  .option("--provider <provider>", "provider override: opencode-go or ollama")
  .option("--host <url>", "explicit Ollama loopback host")
  .option("--model <name>", "explicit Ollama model")
  .option("--timeout-ms <milliseconds>", "explicit Ollama request timeout")
  .option("-o, --out <path>", "optional candidate JSON output")
  .action(async options => {
    const resolved = await resolveCliProvider(options);
    if (!resolved.provider) { printJson({ valid: false, diagnostics: resolved.diagnostics }); process.exitCode = 1; return; }
    const result = await extractContractCandidates(await readFile(options.doc, "utf8"), resolved.provider);
    if (options.out && result.valid) await writeFile(options.out, `${JSON.stringify(result.value, null, 2)}\n`, "utf8");
    printJson(options.out && result.valid ? { ...result, out: options.out } : result);
    process.exitCode = result.valid ? 0 : 1;
  });

program.command("generate-drafts")
  .description("Use the configured LLM provider to propose a ScenarioDraft for one explicit contract")
  .requiredOption("-d, --doc <path>", "input design document")
  .requiredOption("-c, --contract <id>", "contract ID")
  .requiredOption("-o, --out <path>", "candidate ScenarioDraft JSON output")
  .option("--provider <provider>", "provider override: opencode-go or ollama")
  .option("--host <url>", "explicit Ollama loopback host")
  .option("--model <name>", "explicit Ollama model")
  .option("--timeout-ms <milliseconds>", "explicit Ollama request timeout")
  .action(async options => {
    const markdown = await readFile(options.doc, "utf8");
    const parsed = parseGameplayContracts(markdown, options.doc);
    const contract = parsed.contracts.find(value => value.id === options.contract);
    if (!parsed.valid || !contract) {
      const diagnostics = [...parsed.diagnostics];
      if (!contract) {
        diagnostics.push({
          code: "ContractNotFound",
          severity: "error" as const,
          message: `Contract '${options.contract}' was not found.`
        });
      }
      printJson({ valid: false, diagnostics });
      process.exitCode = 1;
      return;
    }
    const resolved = await resolveCliProvider(options);
    if (!resolved.provider) { printJson({ valid: false, diagnostics: resolved.diagnostics }); process.exitCode = 1; return; }
    const result = await generateScenarioDraft(contract.statement, contract.id, resolved.provider);
    if (result.valid && result.value) await writeFile(options.out, `${JSON.stringify(result.value, null, 2)}\n`, "utf8");
    printJson(result.valid ? { ...result, out: options.out } : result);
    process.exitCode = result.valid ? 0 : 1;
  });

program.command("provider-doctor")
  .description("Validate OpenCode Go configuration, model discovery, and JSON output without sending project content")
  .action(async () => {
    const loaded = await loadConfiguredProvider("opencode-go");
    if (!loaded.openCodeOptions) {
      printJson({ valid: false, diagnostics: loaded.diagnostics, paths: loaded.paths });
      process.exitCode = 1;
      return;
    }
    const result = await doctorOpenCodeGo(loaded.openCodeOptions);
    printJson({ ...result, paths: loaded.paths });
    process.exitCode = result.valid ? 0 : 1;
  });

program.command("contract-coverage")
  .description("Report deterministic contract coverage from design documents and gameplay specs")
  .requiredOption("--docs <path>", "directory containing contract Markdown documents")
  .requiredOption("--specs <path>", "directory containing *.gameplay-test.md files")
  .option("--allow-missing-spec <path>", "JSON baseline allowlist for supported contracts without ScenarioSpec")
  .action(async options => {
    const documents = await Promise.all((await findMarkdownFiles(options.docs)).map(async path => ({ path, markdown: await readFile(path, "utf8") })));
    const registry = validateContractRegistry(documents);
    const references = new Map<string, "covered" | "failed">();
    for (const file of await findGameplayTestFiles(options.specs)) {
      const doc = parseGameplayTestDocument(await readFile(file, "utf8"));
      const validation = validateScenarioSpec(doc.frontmatter);
      const ids = (doc.frontmatter as { contractIds?: string[] }).contractIds ?? [];
      for (const id of ids) if (references.get(id) !== "failed") references.set(id, validation.valid ? "covered" : "failed");
    }
    const coverage = buildRegistryContractCoverage(registry, references);
    const allowedMissingSpecIds = options.allowMissingSpec
      ? await loadAllowMissingSpecAllowlist(options.allowMissingSpec)
      : new Set<string>();
    const gate = evaluateContractCoverageGate(registry.valid, coverage, allowedMissingSpecIds);
    const diagnostics = [
      ...registry.diagnostics,
      ...contractCoverageGateDiagnostics(gate)
    ];
    printJson({
      valid: gate.valid,
      diagnostics,
      coverage,
      gate: {
        uncoveredMissingSpec: gate.uncoveredMissingSpec,
        staleAllowlistEntries: gate.staleAllowlistEntries,
        failedContractIds: gate.failedContractIds
      }
    });
    process.exitCode = gate.valid ? 0 : 1;
  });

program
  .command("generate-spec")
  .description("Generate spec from natural language (legacy helper)")
  .requiredOption("-t, --text <text>", "natural language expectation text")
  .requiredOption("-o, --out <path>", "output *.gameplay-test.md path")
  .action(async options => {
    const result = generateScenarioSpec(options.text);
    if (!result.spec || result.needsClarification) {
      printJson(result);
      process.exitCode = 1;
      return;
    }

    await writeFile(options.out, formatGameplayTestDocument(result.spec), "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .command("validate-spec")
  .description("Validate a ScenarioSpec from *.gameplay-test.md")
  .requiredOption("-s, --spec <path>", "input *.gameplay-test.md path")
  .action(async options => {
    const markdown = await readFile(options.spec, "utf8");
    const doc = parseGameplayTestDocument(markdown);
    const result = validateScenarioSpec(doc.frontmatter);
    printJson(result);
    process.exitCode = result.valid ? 0 : 1;
  });

program
  .command("validate-draft")
  .description("Validate a ScenarioDraft JSON file")
  .requiredOption("-d, --draft <path>", "input *.json draft path")
  .action(async options => {
    const content = await readFile(options.draft, "utf8");
    const draft = JSON.parse(content);
    const result = validateScenarioDraft(draft);
    printJson(result);
    process.exitCode = result.valid ? 0 : 1;
  });

program
  .command("compile-spec")
  .description("Compile ScenarioSpec to ExecutableScenarioPlan")
  .requiredOption("-s, --spec <path>", "input *.gameplay-test.md path")
  .requiredOption("-o, --out <path>", "output *.plan.json path")
  .option("--runtime <runtime>", "runtime target: godot (unity is frozen compatibility only)", "godot")
  .action(async options => {
    const markdown = await readFile(options.spec, "utf8");
    const doc = parseGameplayTestDocument(markdown);
    const runtime = parseRuntime(options.runtime);
    const result = compileScenarioSpec(doc.frontmatter, { runtime });
    if (!result.valid || !result.plan) {
      printJson(result);
      process.exitCode = 1;
      return;
    }

    await writeFile(options.out, `${JSON.stringify(result.plan, null, 2)}\n`, "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .command("compile-draft")
  .description("Compile ScenarioDraft JSON to ExecutableScenarioPlan")
  .requiredOption("-d, --draft <path>", "input *.json draft path")
  .requiredOption("-o, --out <path>", "output *.plan.json path")
  .option("--runtime <runtime>", "runtime target: godot (unity is frozen compatibility only)", "godot")
  .action(async options => {
    const content = await readFile(options.draft, "utf8");
    const draft = JSON.parse(content);
    const result = compileScenarioDraft(draft, { runtime: parseRuntime(options.runtime) });
    if (!result.valid || !result.plan) {
      printJson(result);
      process.exitCode = 1;
      return;
    }

    await writeFile(options.out, `${JSON.stringify(result.plan, null, 2)}\n`, "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .command("generate-skill-graph-spec")
  .description("Generate SkillGraphSpec JSON from natural language description")
  .requiredOption("-t, --text <text>", "natural language skill description")
  .option("-o, --out <path>", "output JSON path (default: stdout)")
  .action(async options => {
    const result = generateSkillGraphSpec(options.text);
    if (options.out && result.spec) {
      await writeFile(options.out, `${JSON.stringify(result.spec, null, 2)}\n`, "utf8");
      printJson({ ok: true, out: options.out, needsClarification: result.needsClarification, questionsToAsk: result.questionsToAsk });
    } else {
      printJson(result);
    }
    process.exitCode = result.needsClarification ? 1 : 0;
  });

program
  .command("generate-skill-graph-spec-answers")
  .description("Generate SkillGraphSpec JSON from structured answers JSON")
  .requiredOption("-a, --answers <path>", "input answers JSON path")
  .option("-o, --out <path>", "output JSON path (default: stdout)")
  .action(async options => {
    const content = await readFile(options.answers, "utf8");
    const answers: SkillDesignAnswers = JSON.parse(content);
    const spec = generateSkillGraphSpecFromAnswers(answers);
    if (options.out) {
      await writeFile(options.out, `${JSON.stringify(spec, null, 2)}\n`, "utf8");
      printJson({ ok: true, out: options.out });
    } else {
      printJson(spec);
    }
  });

program
  .command("generate-test-from-spec")
  .description("Generate gameplay-test.md from SkillGraphSpec JSON")
  .requiredOption("-s, --spec <path>", "input SkillGraphSpec JSON path")
  .requiredOption("-o, --out <path>", "output *.gameplay-test.md path")
  .action(async options => {
    const content = await readFile(options.spec, "utf8");
    const spec = JSON.parse(content);
    const scenarioSpec = generateGameplayTestFromSpec(spec);
    const validation = validateScenarioSpec(scenarioSpec);
    if (!validation.valid) {
      printJson({ ok: false, diagnostics: validation.diagnostics });
      process.exitCode = 1;
      return;
    }
    const markdown = formatGameplayTestDocument(scenarioSpec);
    await writeFile(options.out, markdown, "utf8");
    printJson({ ok: true, out: options.out, scenario: scenarioSpec.scenario, graphKind: scenarioSpec.setup.find((s: any) => s.kind === "createSkillGraph")?.parameters?.graphKind });
  });

// Batch commands
program
  .command("batch-compile")
  .description("Batch compile all *.gameplay-test.md files in a directory")
  .requiredOption("-d, --dir <path>", "input directory containing *.gameplay-test.md files")
  .option("-o, --out <path>", "output directory for *.plan.json files (default: same as input)")
  .option("--filter-adapter <adapter>", "filter by adapter (e.g., Battle, Skill)")
  .option("--filter-tag <tag>", "filter by tag")
  .option("--filter-feature <feature>", "filter by feature")
  .option("--filter-scenario <scenario>", "filter by scenario name")
  .option("--runtime <runtime>", "runtime target: godot (unity is frozen compatibility only)", "godot")
  .action(async options => {
    const inputDir = options.dir;
    const outDir = options.out || inputDir;
    const runtime = parseRuntime(options.runtime);
    const files = await findGameplayTestFiles(inputDir);
    
    const summary = {
      total: 0,
      compiled: 0,
      failed: 0,
      skipped: 0,
      failures: [] as Array<{ file: string; diagnostics: any[] }>
    };

    for (const file of files) {
      summary.total++;
      try {
        const markdown = await readFile(file, "utf8");
        const doc = parseGameplayTestDocument(markdown);
        const spec = doc.frontmatter as any;

        // Apply filters
        if (options.filterAdapter && !spec.requiredAdapters?.includes(options.filterAdapter)) {
          summary.skipped++;
          continue;
        }
        if (options.filterTag && !spec.tags?.includes(options.filterTag)) {
          summary.skipped++;
          continue;
        }
        if (options.filterFeature && spec.feature !== options.filterFeature) {
          summary.skipped++;
          continue;
        }
        if (options.filterScenario && spec.scenario !== options.filterScenario) {
          summary.skipped++;
          continue;
        }

        const result = compileScenarioSpec(spec, { runtime });
        if (result.valid && result.plan) {
          const outPath = join(outDir, basename(file, ".gameplay-test.md") + ".plan.json");
          await writeFile(outPath, `${JSON.stringify(result.plan, null, 2)}\n`, "utf8");
          summary.compiled++;
        } else {
          summary.failed++;
          summary.failures.push({
            file: basename(file),
            diagnostics: result.diagnostics
          });
        }
      } catch (error) {
        summary.failed++;
        summary.failures.push({
          file: basename(file),
          diagnostics: [{ code: "FileError", severity: "error", message: String(error) }]
        });
      }
    }

    printJson(summary);
    process.exitCode = summary.failed > 0 ? 1 : 0;
  });

program
  .command("batch-validate")
  .description("Batch validate all *.gameplay-test.md files in a directory")
  .requiredOption("-d, --dir <path>", "input directory containing *.gameplay-test.md files")
  .option("--filter-adapter <adapter>", "filter by adapter (e.g., Battle, Skill)")
  .option("--filter-tag <tag>", "filter by tag")
  .option("--filter-feature <feature>", "filter by feature")
  .option("--filter-scenario <scenario>", "filter by scenario name")
  .action(async options => {
    const inputDir = options.dir;
    const files = await findGameplayTestFiles(inputDir);
    
    const summary = {
      total: 0,
      valid: 0,
      invalid: 0,
      skipped: 0,
      failures: [] as Array<{ file: string; diagnostics: any[] }>
    };

    for (const file of files) {
      summary.total++;
      try {
        const markdown = await readFile(file, "utf8");
        const doc = parseGameplayTestDocument(markdown);
        const spec = doc.frontmatter as any;

        // Apply filters
        if (options.filterAdapter && !spec.requiredAdapters?.includes(options.filterAdapter)) {
          summary.skipped++;
          continue;
        }
        if (options.filterTag && !spec.tags?.includes(options.filterTag)) {
          summary.skipped++;
          continue;
        }
        if (options.filterFeature && spec.feature !== options.filterFeature) {
          summary.skipped++;
          continue;
        }
        if (options.filterScenario && spec.scenario !== options.filterScenario) {
          summary.skipped++;
          continue;
        }

        const result = validateScenarioSpec(spec);
        if (result.valid) {
          summary.valid++;
        } else {
          summary.invalid++;
          summary.failures.push({
            file: basename(file),
            diagnostics: result.diagnostics
          });
        }
      } catch (error) {
        summary.invalid++;
        summary.failures.push({
          file: basename(file),
          diagnostics: [{ code: "FileError", severity: "error", message: String(error) }]
        });
      }
    }

    printJson(summary);
    process.exitCode = summary.invalid > 0 ? 1 : 0;
  });

async function findGameplayTestFiles(dir: string): Promise<string[]> {
  const files: string[] = [];
  const entries = await readdir(dir, { withFileTypes: true });
  
  for (const entry of entries) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      const subFiles = await findGameplayTestFiles(fullPath);
      files.push(...subFiles);
    } else if (entry.name.endsWith(".gameplay-test.md")) {
      files.push(fullPath);
    }
  }
  
  return files;
}

program.parseAsync().catch(error => {
  printJson({
    ok: false,
    diagnostics: [{
      code: "UnhandledCliError",
      severity: "error",
      message: error instanceof Error ? error.message : String(error)
    }]
  });
  process.exitCode = 1;
});

function printJson(value: unknown): void {
  console.log(JSON.stringify(value, null, 2));
}

async function findMarkdownFiles(dir: string): Promise<string[]> {
  const files: string[] = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) files.push(...await findMarkdownFiles(fullPath));
    else if (entry.name.endsWith(".md")) files.push(fullPath);
  }
  return files;
}

function parseRuntime(value: string): "Unity" | "Godot" {
  const normalized = value.toLowerCase();
  if (normalized === "unity") return "Unity";
  if (normalized === "godot") return "Godot";
  throw new Error(`Unknown runtime '${value}'. Expected unity or godot.`);
}

function parsePositiveInteger(value: string, optionName: string): number {
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed <= 0) {
    throw new Error(`--${optionName} must be a positive integer.`);
  }
  return parsed;
}

async function resolveCliProvider(options: {
  provider?: string;
  host?: string;
  model?: string;
  timeoutMs?: string;
}): Promise<{ provider?: StructuredLlmProvider; diagnostics: ExpectationDiagnostic[] }> {
  const provider = parseProvider(options.provider);
  if (provider === "ollama" && (options.host || options.model || options.timeoutMs)) {
    return {
      provider: createOllamaProvider({
        host: options.host,
        model: options.model,
        timeoutMs: options.timeoutMs ? parsePositiveInteger(options.timeoutMs, "timeout-ms") : undefined
      }),
      diagnostics: []
    };
  }
  const loaded = await loadConfiguredProvider(provider);
  return { provider: loaded.provider, diagnostics: loaded.diagnostics };
}

function parseProvider(value?: string): LlmProviderId | undefined {
  if (value === undefined) return undefined;
  if (value === "opencode-go" || value === "ollama") return value;
  throw new Error(`Unknown provider '${value}'. Expected opencode-go or ollama.`);
}
