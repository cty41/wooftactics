import { z } from "zod";
import { ScenarioSpecSchema, ScenarioDraftSchema, type ExpectationDiagnostic, type ScenarioAssertion, type ScenarioSpec, type ScenarioStep, type ScenarioDraft } from "./schema.js";

const supportedSetupKinds = new Set([
  "loadValidatedCheckpoint",
  "createSkillTestWorld",
  "createSkillGraph",
  "createCell",
  "createUnit",
  "createSkillAbilityConfig",
  "createSkillAbility",
  "setTurnContext",
  "selectAbility",
  "bindBattleController",
  "createAiBrain",
  "useRealAssets",
  "loadSkillGraphAsset",
  "setRunSeed",
  "loadRoguelikeMap",
  "loadPureRunMap",
  "loadTestPartyConfig",
  "loadTestEncounterConfig",
  "setAdventureGold",
  "setRosterCharacterState",
  "addInventoryItem",
  "equipInventoryEquipmentToRosterCharacter",
  "initializePlayerInput"
]);

const supportedActionKinds = new Set([
  "endTurnOnlyUntilTerminal",
  "endTurnUntilPresentationNumber",
  "restartGodotMain",
  "setPresentationPaused",
  "setPresentationSpeed",
  "bindBattleController",
  "executeSkillGraph",
  "executeAbilityOnTarget",
  "executeAbilityOnCell",
  "advanceTurn",
  "endBattleWithResult",
  "executeAbility",
  "executeBattleSkillGraph",
  "moveUnit",
  "setUnitState",
  "addBuff",
  "executeAI",
  "createAiBrain",
  "setRunSeed",
  "enterNode",
  "triggerEvent",
  "completeNode",
  "setAdventureGold",
  "setRosterCharacterState",
  "addInventoryItem",
  "equipInventoryEquipmentToRosterCharacter",
  "applyRestSiteResult",
  "buyShopEquipment",
  "addConsumableInstance",
  "carryConsumableToRosterCharacter",
  "unloadRosterCharacterConsumable",
  "buyShopGood",
  "applyEventResult",
  "resolveNodeEventOption",
  "applyRestNodeTransaction",
  "buyShopGoodTransaction",
  "commitNodeTransaction",
  "reloadPureRunSession",
  "exercisePureRunSummaryAndDefeat",
  "captureActivePureRun",
  "beginBattleNode",
  "commitNaturalBattleVictory",
  "grantPureRunLevel",
  "commitNodeInteraction",
  "beginNodeTransaction",
  "commitNaturalBattleDefeat",
  "commitEventPartyDefeat",
  "useCarriedConsumable",
  "openUI",
  "closeUI",
  "clickElement",
  "setText",
  "setElementEnabled",
  "hoverElement",
  "rightClickElement",
  "pressKey",
  "configureLevelUpPanel",
  "refreshBattleActions",
  "refreshInventory",
  "waitForElement",
  "waitForMapReady",
  "spawnCorpse",
  "killUnit",
  "setBattleTestMode",
  "spawnInteractableCorpse",
  "consumeInteractableCorpseAt",
  "setUnitFacing",
  "initializeInitiativeOrder",
  "advanceInitiative",
  "tickUnitTurnStart",
  "tickUnitTurnEnd",
  "registerSummon",
  "beginOrderedTargetSelection",
  "selectOrderedTarget",
  "undoOrderedTargetSelection",
  "commitOrderedTargetSelection",
  "cancelOrderedTargetSelection",
  "bindPureRunAbilityToUnit",
  "dropAmazonSpear",
  "clickBattleUnit",
  "spawnBattleUnit",
  "restartBattle",
  "waitForBattleEnd",
  "initializePlayerInput",
  "movePointerToTarget",
  "clickPointerTarget",
  "rightClickPointerTarget",
  "pressInputKey",
  "waitForPlayerObservable",
  "waitForFrames",
  "playBattleThroughInput",
  "useBattleSkillThroughInput"
]);

const playerInputActionKinds = new Set([
  "initializePlayerInput",
  "movePointerToTarget",
  "clickPointerTarget",
  "rightClickPointerTarget",
  "pressInputKey",
  "waitForPlayerObservable",
  "waitForFrames",
  "playBattleThroughInput"
]);

const supportedPointerTargetKinds = new Set([
  "UiElement", "MapNode", "BattleUnit", "BattleCell",
  "AdventureCell", "AdventureActor", "AdventureObject", "AdventureExitApproach", "RouteNode"
]);
const supportedPlayerObservables = new Set([
  "uiElement",
  "uiVisible",
  "uiHidden",
  "mapReady",
  "battleReady",
  "humanTurn",
  "battleEnded",
  "adventureBoardReady",
  "adventureLeaderChanged",
  "adventureInteractionResolved",
  "exitCommitted",
  "eventBattleReady",
  "adventureSceneChanged",
]);

const supportedGraphKinds = new Set([
  "selfHeal",
  "singleTargetDamage",
  "invalidSelfHeal",
  "areaDamage",
  "knockback",
  "allyHeal",
  "applyBuff",
  "charge",
  "projectile"
]);

const supportedAssertionKinds = new Set([
  "inventoryProjectionEnteredBattle",
  "terminalSummaryOutcomeEquals",
  "activeRunExistsEquals",
  "presentationNumberEquals",
  "presentationNodeCountEquals",
  "productionSaveUnchanged",
  "checkpointRevisionEquals",
  "runtimeStateHashEquals",
  "demonboundCorruptionEquals",
  "battleSkillReceiptEquals",
  "demonboundPossessedEquals",
  "adventureActorCellEquals",
  "activeAdventureLeaderEquals",
  "runNodeLifecycleEquals",
  "immediateSuccessorNodeIdsEqual",
  "adventureObjectStateEquals",
  "storeOfferCountEquals",
  "storeSoldOfferCountEquals",
  "backpackContainsContentId",
  "eventResolutionEquals",
  "pendingBattleContextKindEquals",
  "escortStateEquals",
  "protectedNpcAliveEquals",
  "runSaveSchemaVersionEquals",
  "pendingPartyOrderEquals",
  "activePartyStartingSkillIdsEqual",
  "partyAllLivingAtFullResourcesEquals",
  "partyResourceSummaryEquals",
  "runtimeHasNoErrors",
  "executionStateEquals",
  "validationErrorCodeIncludes",
  "unitHealthEquals",
  "unitManaEquals",
  "unitHasBuff",
  "unitDoesNotHaveBuff",
  "unitBuffDurationEquals",
  "unitBuffCountEquals",
  "unitBuffIsUnique",
  "unitCellEquals",
  "unitCountInArea",
  "lastErrorContains",
  "stepMessageContains",
  "projectileLaunched",
  "projectileHitTarget",
  "projectileCompleted",
  "multiStageStateEquals",
  "battleIsActive",
  "currentRoundEquals",
  "unitAliveEquals",
  "battleResultEquals",
  "unitPositionEquals",
  "playerNumberEquals",
  "unitMaxHealthEquals",
  "unitCountEquals",
  "unitCanAct",
  "aiSelectedIntentTypeEquals",
  "aiCandidateCountEquals",
  "aiRuleFilteredCountEquals",
  "aiTurnSucceededEquals",
  "aiTurnAbilityEquals",
  "aiTurnDestinationEquals",
  "aiTurnTargetPointEquals",
  "aiTurnTargetCountEquals",
  "aiTurnUsedFallbackEquals",
  "aiTurnPatternStepEquals",
  "aiUsedAbilityEquals",
  "aiWasNoOpEquals",
  "unitPositionChangedSinceStep",
  "targetHealthChangedSinceStep",
  "decisionLogContains",
  "currentNodeEquals",
  "mapIsActive",
  "visitedNodeCountEquals",
  "battleVictoryCountEquals",
  "nodeTypeEquals",
  "nodeIsReachable",
  "nodeIsVisited",
  "runGoldEquals",
  "rosterCharacterHpEquals",
  "rosterCharacterMpEquals",
  "rosterCharacterDeadEquals",
  "rosterCharacterExperienceEquals",
  "rosterCharacterLevelEquals",
  "rosterCharacterHasSkillId",
  "rosterCharacterSkillLevelEquals",
  "pureRunSkillChoiceContains",
  "pureRunSkillChoicesAreMixed",
  "rosterCharacterEquipmentEquals",
  "rosterCharacterTotalAttributeEquals",
  "runtimeRosterCharacterHasPendingBuff",
  "rosterCharacterHasPendingBuff",
  "rosterCharacterPendingBuffHasIcon",
  "inventoryContains",
  "consumableCountEquals",
  "rosterCharacterCarriedConsumableEquals",
  "backpackConsumableCountEquals",
  "consumableInstanceExists",
  "shopGoodCountEquals",
  "shopConsumableCountAtLeast",
  "shopConsumableIdsUnique",
  "mysteryEventIdsUnique",
  "nodeEventIdEquals",
  "nodeTransactionPhaseEquals",
  "nodeTransactionRewardAppliedEquals",
  "transactionApplicationCountEquals",
  "nodeIsConsumed",
  "encounterRecipeContract",
  "monsterAiCatalogValid",
  "battleDefeatRewardsAreZero",
  "completedSummaryGoldEquals",
  "completedSummaryContainsItem",
  "completedSummaryOutcomeEquals",
  "completedSummaryNodesVisitedEquals",
  "completedSummaryEventsCompletedEquals",
  "unitCanReceiveHealingEquals",
  "elementVisible",
  "elementText",
  "elementEnabled",
  "elementExists",
  "cellIsBlocked",
  "unitOwnerEquals",
  "unitIsCorpse",
  "interactableCorpseExistsAt",
  "cellOccupiedByInteractable",
  "unitFacingEquals",
  "currentRoundOrderEquals",
  "unitStatusStacksEquals",
  "unitStatusRemainingActionsEquals",
  "summonOrderEquals",
  "summonCategoryEquals",
  "abilityAvailabilityEquals",
  "abilityAvailabilityReasonEquals",
  "actualSkillLevelEquals",
  "unitAbilityListEquals",
  "orderedTargetSelectionEquals",
  "selectionStageEquals",
  "spearHolderEquals",
  "spearCellEquals",
  "decoyRemainingActionsEquals",
  "aiTargetEquals",
  "elementClassContains",
  "elementClassContainsAny",
  "elementChildOrderEquals",
  "elementRectRelationEquals",
  "abilityCardAvailabilityEquals",
  "targetMarkerOrderEquals"
]);

const supportedFacingValues = new Set(["north", "east", "south", "west"]);

interface AliasState {
  graphs: Set<string>;
  cells: Set<string>;
  units: Set<string>;
  abilityConfigs: Set<string>;
  abilities: Set<string>;
  useRealAssets: boolean;
}

export interface ValidationResult {
  spec?: ScenarioSpec;
  diagnostics: ExpectationDiagnostic[];
  valid: boolean;
}

export function validateScenarioSpec(input: unknown): ValidationResult {
  const parsed = ScenarioSpecSchema.safeParse(input);
  if (!parsed.success) {
    return {
      valid: false,
      diagnostics: parsed.error.issues.map(issue => ({
        code: "SchemaValidationFailed",
        severity: "error",
        message: issue.message,
        path: issue.path.join(".")
      }))
    };
  }

  const spec = parsed.data;
  const diagnostics: ExpectationDiagnostic[] = [];
  const state = createAliasState();

  // Check if bindBattleController is in setup (units registered at runtime)
  const hasBindBattleController = spec.setup.some(s => s.kind === "bindBattleController");

  for (const step of spec.setup) {
    validateStepKind(step, supportedSetupKinds, "UnsupportedSetupKind", diagnostics);
    validateSetupStep(step, state, diagnostics);
  }

  for (const action of spec.actions) {
    validateStepKind(action, supportedActionKinds, "UnsupportedActionKind", diagnostics);
    validateActionStep(action, state, diagnostics, hasBindBattleController);
  }

  for (const assertion of spec.assertions) {
    validateAssertion(assertion, state, diagnostics, spec.requiredAdapters, hasBindBattleController);
  }

  if (!spec.requiredAdapters.includes("Skill") && !spec.requiredAdapters.includes("Battle") && !spec.requiredAdapters.includes("Map") && !spec.requiredAdapters.includes("UI") && !spec.requiredAdapters.includes("PlayerInput")) {
    diagnostics.push({
      code: "MissingAdapter",
      severity: "error",
      message: "Scenarios must include at least one supported adapter (Skill, Battle, Map, UI, or PlayerInput)."
    });
  }

  validateSemanticRules(spec, state, diagnostics);

  return {
    spec,
    diagnostics,
    valid: diagnostics.every(diagnostic => diagnostic.severity !== "error")
  };
}

export function validateScenarioDraft(draft: unknown): ValidationResult {
  const parsed = ScenarioDraftSchema.safeParse(draft);
  if (!parsed.success) {
    return {
      valid: false,
      diagnostics: parsed.error.issues.map(issue => ({
        code: "DraftSchemaValidationFailed",
        severity: "error",
        message: issue.message,
        path: issue.path.join(".")
      }))
    };
  }

  const draftData = parsed.data;
  const spec: ScenarioSpec = {
    feature: draftData.feature,
    scenario: draftData.scenario,
    contractIds: draftData.contractIds ?? [],
    tags: draftData.tags,
    requiredAdapters: draftData.requiredAdapters,
    setup: draftData.setup.map(s => ({ kind: s.kind, parameters: s.parameters })),
    actions: draftData.actions.map(a => ({ kind: a.kind, target: a.target, parameters: a.parameters })),
    assertions: draftData.assertions.map(a => ({ kind: a.kind, target: a.target, expected: a.expected, parameters: a.parameters })),
    timeoutMs: draftData.timeoutMs
  };

  return validateScenarioSpec(spec);
}

function createAliasState(): AliasState {
  return {
    graphs: new Set<string>(),
    cells: new Set<string>(),
    units: new Set<string>(),
    abilityConfigs: new Set<string>(),
    abilities: new Set<string>(),
    useRealAssets: false
  };
}

function validateStepKind(
  step: ScenarioStep,
  supportedKinds: Set<string>,
  code: string,
  diagnostics: ExpectationDiagnostic[]
): void {
  if (supportedKinds.has(step.kind)) {
    return;
  }

  diagnostics.push({
    code,
    severity: "error",
    message: `Unsupported ${step.id ? "step" : "item"} kind '${step.kind}'.`,
    path: step.id ?? step.kind
  });
}

function validateSetupStep(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  switch (step.kind) {
    case "loadValidatedCheckpoint":
      for (const key of ["id", "path", "semanticHash"] as const) requireStepStringParameter(step, key, diagnostics);
      if (typeof step.parameters.semanticHash === "string" && !/^[a-f0-9]{64}$/.test(step.parameters.semanticHash)) diagnostics.push({
        code: "InvalidCheckpointHash", severity: "error", message: "loadValidatedCheckpoint semanticHash must be lowercase SHA-256.", path: step.id ?? step.kind
      });
      break;
    case "createSkillTestWorld":
      resetAliasState(state);
      break;
    case "createSkillGraph":
      validateGraphStep(step, state, diagnostics);
      break;
    case "createCell":
      registerAlias(step, "alias", step.parameters.alias, state.cells, diagnostics, "MissingCellAlias");
      break;
    case "createUnit":
      validateUnitSetup(step, state, diagnostics);
      break;
    case "createSkillAbilityConfig":
      validateAbilityConfigSetup(step, state, diagnostics);
      break;
    case "createSkillAbility":
      validateAbilitySetup(step, state, diagnostics);
      break;
    case "setTurnContext":
      validateTurnContextSetup(step, state, diagnostics);
      break;
    case "selectAbility":
      validateSelectAbilitySetup(step, state, diagnostics);
      break;
    case "createAiBrain":
      // createAiBrain is a Battle setup action, no special validation needed
      break;
    case "useRealAssets":
      if (state.graphs.size > 0 || state.units.size > 0 || state.cells.size > 0) {
        diagnostics.push({
          code: "MixedAssetMode",
          severity: "error",
          message: "useRealAssets must be used before any createSkillGraph/createUnit/createCell setup.",
          path: step.id ?? step.kind
        });
      }
      state.useRealAssets = true;
      break;
    case "loadSkillGraphAsset":
      if (!state.useRealAssets) {
        diagnostics.push({
          code: "MissingRealAssetMode",
          severity: "error",
          message: "loadSkillGraphAsset requires useRealAssets to be called first.",
          path: step.id ?? step.kind
        });
      }
      registerAlias(step, "alias", step.parameters.alias, state.graphs, diagnostics, "MissingGraphAlias");
      break;
    case "loadRoguelikeMap":
    case "loadPureRunMap":
      // loadRoguelikeMap is a Map setup action, requires mapConfigPath parameter
      if (!getString(step.parameters.mapConfigPath)) {
        diagnostics.push({
          code: "MissingMapConfigPath",
          severity: "error",
          message: `${step.kind} requires a mapConfigPath parameter.`,
          path: step.id ?? step.kind
        });
      }
      if (step.parameters.strictAsset !== undefined && typeof step.parameters.strictAsset !== "boolean") {
        diagnostics.push({
          code: "InvalidStrictAsset",
          severity: "error",
          message: "loadRoguelikeMap strictAsset must be a boolean.",
          path: step.id ?? step.kind
        });
      }
      break;
    case "setRunSeed":
      validateRunSeed(step, diagnostics);
      break;
    case "loadTestPartyConfig":
      if (!getString(step.parameters.configPath)) {
        diagnostics.push({
          code: "MissingTestPartyConfigPath",
          severity: "error",
          message: "loadTestPartyConfig requires a configPath parameter.",
          path: step.id ?? step.kind
        });
      }
      if (!getString(step.parameters.spawnPointPrefix)) {
        diagnostics.push({
          code: "MissingSpawnPointPrefix",
          severity: "warning",
          message: "loadTestPartyConfig usually needs a spawnPointPrefix to resolve spawn points.",
          path: step.id ?? step.kind
        });
      }
      break;
    case "loadTestEncounterConfig":
      if (!getString(step.parameters.configPath)) {
        diagnostics.push({
          code: "MissingTestEncounterConfigPath",
          severity: "error",
          message: "loadTestEncounterConfig requires a configPath parameter.",
          path: step.id ?? step.kind
        });
      }
      if (!getString(step.parameters.spawnPointPrefix)) {
        diagnostics.push({
          code: "MissingSpawnPointPrefix",
          severity: "warning",
          message: "loadTestEncounterConfig usually needs a spawnPointPrefix to resolve spawn points.",
          path: step.id ?? step.kind
        });
      }
      break;
    default:
      break;
  }
}

function validateActionStep(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[], hasBindBattleController?: boolean): void {
  switch (step.kind) {
    case "endTurnOnlyUntilTerminal":
      if (step.parameters.maximumRounds !== undefined &&
          (!Number.isInteger(step.parameters.maximumRounds) || Number(step.parameters.maximumRounds) < 1 || Number(step.parameters.maximumRounds) > 80)) {
        diagnostics.push({ code: "InvalidMaximumRounds", severity: "error", message: "endTurnOnlyUntilTerminal maximumRounds must be an integer from 1 to 80.", path: step.id ?? step.kind });
      }
      break;
    case "restartGodotMain":
      break;
    case "setPresentationPaused":
      if (typeof step.parameters.paused !== "boolean") diagnostics.push({
        code: "InvalidPresentationPaused", severity: "error", message: "setPresentationPaused requires a boolean paused parameter.", path: step.id ?? step.kind
      });
      break;
    case "setPresentationSpeed":
      if (typeof step.parameters.speed !== "number" || ![0.5, 1, 2, 4].includes(step.parameters.speed)) diagnostics.push({
        code: "InvalidPresentationSpeed", severity: "error", message: "setPresentationSpeed requires speed 0.5, 1, 2, or 4.", path: step.id ?? step.kind
      });
      break;
    case "executeSkillGraph":
      validateExecuteSkillGraph(step, state, diagnostics);
      break;
    case "executeBattleSkillGraph":
      validateExecuteBattleSkillGraph(step, state, diagnostics, hasBindBattleController);
      break;
    case "executeAbilityOnTarget":
      validateExecuteAbilityOnTarget(step, state, diagnostics);
      break;
    case "executeAbilityOnCell":
      validateExecuteAbilityOnCell(step, state, diagnostics);
      break;
    case "setRunSeed":
      validateRunSeed(step, diagnostics);
      break;
    case "enterNode":
      validateEnterNode(step, diagnostics);
      break;
    case "triggerEvent":
      validateTriggerEvent(step, diagnostics);
      break;
    case "completeNode":
      // completeNode 不强制要求参数，会使用 currentNodeId
      break;

    case "openUI":
      validateOpenUI(step, diagnostics);
      break;
    case "closeUI":
      // closeUI 不强制要求参数，会关闭当前 UI
      break;
    case "clickElement":
    case "hoverElement":
    case "rightClickElement":
      validateClickElement(step, diagnostics);
      break;
    case "pressKey":
      validatePressKey(step, diagnostics);
      break;
    case "setText":
      validateSetText(step, diagnostics);
      break;
    case "setElementEnabled":
      validateSetElementEnabled(step, diagnostics);
      break;
    case "setUnitFacing":
      requireStepStringParameter(step, "unitAlias", diagnostics);
      requireStepStringParameter(step, "facing", diagnostics);
      if (getString(step.parameters.facing) &&
          !supportedFacingValues.has(getString(step.parameters.facing)!.toLowerCase())) {
        diagnostics.push({
          code: "InvalidFacing",
          severity: "error",
          message: "setUnitFacing facing must be North, East, South, or West.",
          path: step.id ?? step.kind
        });
      }
      break;
    case "tickUnitTurnStart":
    case "tickUnitTurnEnd":
      requireStepStringParameter(step, "unitAlias", diagnostics);
      break;
    case "registerSummon":
      requireStepStringParameter(step, "ownerAlias", diagnostics);
      requireStepStringParameter(step, "summonAlias", diagnostics);
      if (step.parameters.maximumActive !== undefined &&
          (!Number.isInteger(step.parameters.maximumActive) || Number(step.parameters.maximumActive) < 1)) {
        diagnostics.push({
          code: "InvalidMaximumActive",
          severity: "error",
          message: "registerSummon maximumActive must be a positive integer.",
          path: step.id ?? step.kind
        });
      }
      break;
    case "beginOrderedTargetSelection":
      if (!Number.isInteger(step.parameters.requiredCount) || Number(step.parameters.requiredCount) < 1) {
        diagnostics.push({
          code: "InvalidRequiredCount",
          severity: "error",
          message: "beginOrderedTargetSelection requires a positive integer requiredCount.",
          path: step.id ?? step.kind
        });
      }
      break;
    case "selectOrderedTarget":
      requireStepStringParameter(step, "targetAlias", diagnostics);
      break;
    case "movePointerToTarget":
    case "clickPointerTarget":
    case "rightClickPointerTarget":
      validatePlayerInputTarget(step, diagnostics);
      break;
    case "pressInputKey":
      requireStepStringParameter(step, "key", diagnostics);
      break;
    case "waitForPlayerObservable":
      validatePlayerObservable(step, diagnostics);
      break;
    case "playBattleThroughInput":
      if (step.parameters.maximumActions !== undefined &&
          (!Number.isInteger(step.parameters.maximumActions) || Number(step.parameters.maximumActions) < 1 || Number(step.parameters.maximumActions) > 100)) {
        diagnostics.push({
          code: "InvalidMaximumPlayerActions",
          severity: "error",
          message: "playBattleThroughInput maximumActions must be an integer from 1 to 100.",
          path: step.id ?? step.kind
        });
      }
      break;
    case "useBattleSkillThroughInput":
      requireStepStringParameter(step, "actorId", diagnostics);
      requireStepStringParameter(step, "skillId", diagnostics);
      if (step.parameters.maximumActions !== undefined &&
          (!Number.isInteger(step.parameters.maximumActions) || Number(step.parameters.maximumActions) < 1 || Number(step.parameters.maximumActions) > 100)) {
        diagnostics.push({ code: "InvalidMaximumPlayerActions", severity: "error",
          message: "useBattleSkillThroughInput maximumActions must be an integer from 1 to 100.", path: step.id ?? step.kind });
      }
      break;
    case "endTurnUntilPresentationNumber":
      if (typeof step.parameters.kind !== "string" || !["Normal", "Critical", "Heal", "Mana", "Miss"].includes(step.parameters.kind)) diagnostics.push({
        code: "InvalidPresentationNumberKind", severity: "error", message: "endTurnUntilPresentationNumber requires a supported kind.", path: step.id ?? step.kind
      });
      if (typeof step.parameters.maximumActions !== "number" || !Number.isInteger(step.parameters.maximumActions) || step.parameters.maximumActions < 1 || step.parameters.maximumActions > 100) diagnostics.push({
        code: "InvalidMaximumActions", severity: "error", message: "endTurnUntilPresentationNumber maximumActions must be an integer from 1 to 100.", path: step.id ?? step.kind
      });
      break;
    default:
      break;
  }
}

function validatePlayerInputTarget(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const targetKind = getString(step.parameters.targetKind) ?? "UiElement";
  if (!supportedPointerTargetKinds.has(targetKind)) {
    diagnostics.push({
      code: "InvalidPlayerInputTargetKind",
      severity: "error",
      message: `${step.kind} targetKind must be UiElement, MapNode, BattleUnit, BattleCell, AdventureCell, AdventureActor, AdventureObject, AdventureExitApproach, or RouteNode.`,
      path: step.id ?? step.kind
    });
    return;
  }

  const locator = step.target ?? getString(step.parameters.elementName) ?? getString(step.parameters.nodeId) ??
    getString(step.parameters.unitId) ?? getString(step.parameters.actorId) ??
    getString(step.parameters.objectId) ?? getString(step.parameters.routeNodeId) ?? getString(step.parameters.cell);
  if (!locator) {
    diagnostics.push({
      code: "MissingPlayerInputTarget",
      severity: "error",
      message: `${step.kind} requires a semantic target locator.`,
      path: step.id ?? step.kind
    });
  }
}

function validatePlayerObservable(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const observable = getString(step.parameters.observable);
  if (!observable || !supportedPlayerObservables.has(observable)) {
    diagnostics.push({
      code: "InvalidPlayerObservable",
      severity: "error",
      message: "waitForPlayerObservable observable must be a supported UI, battle, map, or adventure lifecycle value.",
      path: step.id ?? step.kind
    });
    return;
  }

  if (observable === "uiElement" && !(step.target ?? getString(step.parameters.elementName)))
    requireStepStringParameter(step, "elementName", diagnostics);
  if ((observable === "uiVisible" || observable === "uiHidden") && !getString(step.parameters.uiId))
    requireStepStringParameter(step, "uiId", diagnostics);
}

function validateRunSeed(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const seed = step.parameters.seed;
  if (typeof seed === "number" && Number.isInteger(seed)) {
    return;
  }

  diagnostics.push({
    code: "InvalidRunSeed",
    severity: "error",
    message: "setRunSeed requires an integer seed parameter.",
    path: step.id ?? step.kind
  });
}

function validateAssertion(assertion: ScenarioAssertion, state: AliasState, diagnostics: ExpectationDiagnostic[], requiredAdapters?: string[], hasBindBattleController?: boolean): void {
  if (!supportedAssertionKinds.has(assertion.kind)) {
    diagnostics.push({
      code: "UnsupportedAssertionKind",
      severity: "error",
      message: `Unsupported assertion kind '${assertion.kind}'.`,
      path: assertion.id ?? assertion.kind
    });
    return;
  }

  // If bindBattleController is in setup, units are registered at runtime
  // so we skip static unit alias checks
  const isBattleContext = hasBindBattleController || (requiredAdapters?.includes("Battle") && !requiredAdapters?.includes("Skill"));

  switch (assertion.kind) {
    case "terminalSummaryOutcomeEquals":
    case "presentationNumberEquals":
    case "runtimeStateHashEquals":
    case "adventureActorCellEquals":
    case "activeAdventureLeaderEquals":
    case "runNodeLifecycleEquals":
    case "adventureObjectStateEquals":
    case "eventResolutionEquals":
    case "pendingBattleContextKindEquals":
    case "escortStateEquals":
      requireStringExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "presentationNodeCountEquals":
    case "checkpointRevisionEquals":
    case "demonboundCorruptionEquals":
    case "storeOfferCountEquals":
    case "storeSoldOfferCountEquals":
    case "runSaveSchemaVersionEquals":
      requireIntegerExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "inventoryProjectionEnteredBattle":
    case "activeRunExistsEquals":
    case "productionSaveUnchanged":
    case "runtimeHasNoErrors":
    case "demonboundPossessedEquals":
    case "backpackContainsContentId":
    case "protectedNpcAliveEquals":
    case "partyAllLivingAtFullResourcesEquals":
      if (typeof assertion.expected !== "boolean") diagnostics.push({
        code: "InvalidAssertionExpectedType", severity: "error", message: `${assertion.kind} requires a boolean expected value.`, path: assertion.id ?? assertion.kind
      });
      break;
    case "immediateSuccessorNodeIdsEqual":
    case "pendingPartyOrderEquals":
    case "activePartyStartingSkillIdsEqual":
    case "partyResourceSummaryEquals":
      if (!Array.isArray(assertion.expected) || assertion.expected.some(value => typeof value !== "string")) diagnostics.push({
        code: "InvalidAssertionExpectedType", severity: "error", message: `${assertion.kind} requires a string-array expected value.`, path: assertion.id ?? assertion.kind
      });
      break;
    case "battleSkillReceiptEquals":
      if (assertion.expected === null || Array.isArray(assertion.expected) || typeof assertion.expected !== "object") diagnostics.push({
        code: "InvalidAssertionExpectedType", severity: "error", message: "battleSkillReceiptEquals requires an object expected value.", path: assertion.id ?? assertion.kind
      });
      break;
    case "executionStateEquals":
    case "validationErrorCodeIncludes":
    case "lastErrorContains":
    case "stepMessageContains":
    case "aiTurnAbilityEquals":
    case "aiTurnDestinationEquals":
    case "aiTurnTargetPointEquals":
    case "aiTurnPatternStepEquals":
    case "rosterCharacterHasSkillId":
    case "pureRunSkillChoiceContains":
    case "unitFacingEquals":
    case "summonCategoryEquals":
    case "abilityAvailabilityEquals":
    case "abilityAvailabilityReasonEquals":
    case "selectionStageEquals":
    case "spearHolderEquals":
    case "spearCellEquals":
    case "aiTargetEquals":
    case "elementClassContains":
    case "elementRectRelationEquals":
    case "abilityCardAvailabilityEquals":
    case "completedSummaryContainsItem":
    case "completedSummaryOutcomeEquals":
      requireStringExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "unitHealthEquals":
    case "unitManaEquals":
      requireNumberExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      if (!isBattleContext) requireKnownUnit(assertion, state, diagnostics);
      break;
    case "aiTurnTargetCountEquals":
    case "rosterCharacterLevelEquals":
    case "rosterCharacterSkillLevelEquals":
    case "backpackConsumableCountEquals":
    case "shopGoodCountEquals":
    case "shopConsumableCountAtLeast":
    case "unitStatusStacksEquals":
    case "unitStatusRemainingActionsEquals":
    case "actualSkillLevelEquals":
    case "decoyRemainingActionsEquals":
    case "transactionApplicationCountEquals":
    case "completedSummaryGoldEquals":
    case "completedSummaryNodesVisitedEquals":
    case "completedSummaryEventsCompletedEquals":
      requireIntegerExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "currentRoundOrderEquals":
    case "unitAbilityListEquals":
    case "orderedTargetSelectionEquals":
    case "summonOrderEquals":
    case "elementChildOrderEquals":
    case "targetMarkerOrderEquals":
    case "elementClassContainsAny":
      requireStringArrayExpected(assertion, diagnostics);
      break;
    case "aiTurnSucceededEquals":
    case "aiTurnUsedFallbackEquals":
    case "consumableInstanceExists":
    case "shopConsumableIdsUnique":
    case "mysteryEventIdsUnique":
    case "nodeTransactionRewardAppliedEquals":
    case "nodeIsConsumed":
    case "unitCanReceiveHealingEquals":
    case "pureRunSkillChoicesAreMixed":
    case "monsterAiCatalogValid":
    case "battleDefeatRewardsAreZero":
      if (typeof assertion.expected !== "boolean") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a boolean expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "unitHasBuff":
    case "unitDoesNotHaveBuff":
      if (!isBattleContext) requireKnownUnit(assertion, state, diagnostics);
      requireBuffName(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "unitBuffDurationEquals":
      if (!isBattleContext) requireKnownUnit(assertion, state, diagnostics);
      requireBuffName(assertion, diagnostics, "InvalidAssertionExpectedType");
      requireIntegerExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "unitCellEquals":
      if (!isBattleContext) requireKnownUnit(assertion, state, diagnostics);
      requireCellCoordinatesExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "unitAliveEquals":
      if (!isBattleContext) requireKnownUnit(assertion, state, diagnostics);
      if (typeof assertion.expected !== "boolean") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a boolean expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "battleIsActive":
    case "currentRoundEquals":
    case "battleResultEquals":
    case "unitPositionEquals":
    case "currentNodeEquals":
    case "mapIsActive":
    case "visitedNodeCountEquals":
    case "battleVictoryCountEquals":
    case "consumableCountEquals":
    case "nodeTypeEquals":
    case "nodeIsReachable":
    case "nodeIsVisited":
      break;
    case "elementVisible":
    case "elementEnabled":
    case "elementExists":
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingElementTarget",
          severity: "error",
          message: `${assertion.kind} requires a target element name.`,
          path: assertion.id ?? assertion.kind
        });
      }
      if (typeof assertion.expected !== "boolean") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a boolean expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "elementText":
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingElementTarget",
          severity: "error",
          message: `${assertion.kind} requires a target element name.`,
          path: assertion.id ?? assertion.kind
        });
      }
      if (typeof assertion.expected !== "string") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a string expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "encounterRecipeContract":
      if (!assertion.target || typeof assertion.expected !== "object" || assertion.expected === null || Array.isArray(assertion.expected)) {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: "encounterRecipeContract requires a target recipe and object expected value.",
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "cellIsBlocked":
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingCellTarget",
          severity: "error",
          message: `${assertion.kind} requires a target cell alias.`,
          path: assertion.id ?? assertion.kind
        });
      }
      if (typeof assertion.expected !== "boolean") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a boolean expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "unitOwnerEquals":
      if (!isBattleContext) requireKnownUnit(assertion, state, diagnostics);
      if (typeof assertion.expected !== "string") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a string expected value (owner unit alias).`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "unitIsCorpse":
      if (!isBattleContext) requireKnownUnit(assertion, state, diagnostics);
      if (typeof assertion.expected !== "boolean") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a boolean expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "interactableCorpseExistsAt":
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingCellTarget",
          severity: "error",
          message: `${assertion.kind} requires a target cell alias.`,
          path: assertion.id ?? assertion.kind
        });
      }
      if (typeof assertion.expected !== "boolean") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a boolean expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
    case "cellOccupiedByInteractable":
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingCellTarget",
          severity: "error",
          message: `${assertion.kind} requires a target cell alias.`,
          path: assertion.id ?? assertion.kind
        });
      }
      if (typeof assertion.expected !== "boolean") {
        diagnostics.push({
          code: "InvalidAssertionExpectedType",
          severity: "error",
          message: `${assertion.kind} requires a boolean expected value.`,
          path: assertion.id ?? assertion.kind
        });
      }
      break;
  }

  const unitTargetAssertions = new Set([
    "unitFacingEquals",
    "summonCategoryEquals",
    "unitAbilityListEquals",
    "summonOrderEquals",
    "unitStatusStacksEquals",
    "unitStatusRemainingActionsEquals",
    "actualSkillLevelEquals",
    "decoyRemainingActionsEquals"
  ]);
  if (unitTargetAssertions.has(assertion.kind) && !assertion.target) {
    diagnostics.push({
      code: "MissingUnitTarget",
      severity: "error",
      message: `${assertion.kind} requires a target unit alias.`,
      path: assertion.id ?? assertion.kind
    });
  }

  if ((assertion.kind === "unitStatusStacksEquals" || assertion.kind === "unitStatusRemainingActionsEquals") &&
      !getString(assertion.parameters.buffName)) {
    diagnostics.push({
      code: "MissingBuffName",
      severity: "error",
      message: `${assertion.kind} requires a buffName parameter.`,
      path: assertion.id ?? assertion.kind
    });
  }
  if (assertion.kind === "actualSkillLevelEquals" && !getString(assertion.parameters.skillId)) {
    diagnostics.push({
      code: "MissingSkillId",
      severity: "error",
      message: "actualSkillLevelEquals requires a skillId parameter.",
      path: assertion.id ?? assertion.kind
    });
  }

  const elementTargetAssertions = new Set([
    "elementClassContains",
    "elementClassContainsAny",
    "elementChildOrderEquals",
    "elementRectRelationEquals",
    "abilityCardAvailabilityEquals"
  ]);
  if (elementTargetAssertions.has(assertion.kind) && !assertion.target) {
    diagnostics.push({
      code: "MissingElementTarget",
      severity: "error",
      message: `${assertion.kind} requires a target element name.`,
      path: assertion.id ?? assertion.kind
    });
  }
  if (assertion.kind === "elementRectRelationEquals" &&
      !getString(assertion.parameters.otherElement) &&
      !getString(assertion.parameters.relativeTo)) {
    diagnostics.push({
      code: "MissingRelatedElement",
      severity: "error",
      message: "elementRectRelationEquals requires otherElement or relativeTo.",
      path: assertion.id ?? assertion.kind
    });
  }

  if ((assertion.kind === "abilityAvailabilityEquals" || assertion.kind === "abilityAvailabilityReasonEquals") &&
      !assertion.target && !getString(assertion.parameters.abilityAlias)) {
    diagnostics.push({
      code: "MissingAbilityTarget",
      severity: "error",
      message: `${assertion.kind} requires a target ability alias or abilityAlias parameter.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function validateGraphStep(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias) ?? "graph";
  if (!alias) {
    diagnostics.push({
      code: "MissingGraphAlias",
      severity: "error",
      message: "createSkillGraph requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const graphKind = getString(step.parameters.graphKind) ?? "";
  if (!supportedGraphKinds.has(graphKind)) {
    diagnostics.push({
      code: "UnsupportedGraphKind",
      severity: "error",
      message: `Unsupported skill graph kind '${graphKind}'.`,
      path: step.id ?? step.kind
    });
  }

  state.graphs.add(alias);
}

function validateUnitSetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias);
  if (!alias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "createUnit requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const cellAlias = getString(step.parameters.cellAlias);
  if (cellAlias && !state.cells.has(cellAlias)) {
    diagnostics.push({
      code: "UnknownCellAlias",
      severity: "error",
      message: `Cell alias '${cellAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  state.units.add(alias);
}

function validateAbilityConfigSetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias);
  if (!alias) {
    diagnostics.push({
      code: "MissingAbilityConfigAlias",
      severity: "error",
      message: "createSkillAbilityConfig requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const graphAlias = getString(step.parameters.graphAlias);
  if (!graphAlias) {
    diagnostics.push({
      code: "MissingGraphAlias",
      severity: "error",
      message: "createSkillAbilityConfig requires a graphAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.graphs.has(graphAlias)) {
    diagnostics.push({
      code: "UnknownGraphAlias",
      severity: "error",
      message: `Skill graph alias '${graphAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  state.abilityConfigs.add(alias);
}

function validateAbilitySetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias);
  if (!alias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "createSkillAbility requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const configAlias = getString(step.parameters.configAlias);
  if (!configAlias) {
    diagnostics.push({
      code: "MissingAbilityConfigAlias",
      severity: "error",
      message: "createSkillAbility requires a configAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.abilityConfigs.has(configAlias)) {
    diagnostics.push({
      code: "UnknownAbilityConfigAlias",
      severity: "error",
      message: `Ability config alias '${configAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const ownerAlias = getString(step.parameters.ownerAlias);
  if (!ownerAlias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "createSkillAbility requires an ownerAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.units.has(ownerAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Unit alias '${ownerAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  state.abilities.add(alias);
}

function validateTurnContextSetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const aliases = Array.isArray(step.parameters.playableUnitAliases)
    ? step.parameters.playableUnitAliases
    : [];

  for (const alias of aliases) {
    if (typeof alias !== "string" || !alias) {
      diagnostics.push({
        code: "InvalidPlayableUnitAlias",
        severity: "error",
        message: "playableUnitAliases must only contain unit alias strings.",
        path: step.id ?? step.kind
      });
      continue;
    }

    if (!state.units.has(alias)) {
      diagnostics.push({
        code: "UnknownUnitAlias",
        severity: "error",
        message: `Playable unit alias '${alias}' does not exist.`,
        path: step.id ?? step.kind
      });
    }
  }
}

function validateSelectAbilitySetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const abilityAlias = getString(step.parameters.abilityAlias);
  if (!abilityAlias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "selectAbility requires an abilityAlias.",
      path: step.id ?? step.kind
    });
    return;
  }

  if (!state.abilities.has(abilityAlias)) {
    diagnostics.push({
      code: "UnknownAbilityAlias",
      severity: "error",
      message: `Ability alias '${abilityAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }
}

function validateExecuteSkillGraph(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const graphAlias = getString(step.parameters.graphAlias) ?? "graph";
  if (!state.graphs.has(graphAlias)) {
    diagnostics.push({
      code: "UnknownGraphAlias",
      severity: "error",
      message: `Skill graph alias '${graphAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const casterAlias = getString(step.parameters.casterAlias);
  if (!casterAlias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "executeSkillGraph requires a casterAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.units.has(casterAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Caster alias '${casterAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const primaryTargetAlias = getString(step.parameters.primaryTargetAlias);
  if (primaryTargetAlias && !state.units.has(primaryTargetAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Primary target alias '${primaryTargetAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const targetPointAlias = getString(step.parameters.targetPointAlias);
  if (targetPointAlias && !state.cells.has(targetPointAlias) && !state.units.has(targetPointAlias)) {
    diagnostics.push({
      code: "UnknownCellAlias",
      severity: "error",
      message: `Target point alias '${targetPointAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }
}

function validateExecuteBattleSkillGraph(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[], hasBindBattleController?: boolean): void {
  const graphAlias = getString(step.parameters.graphAlias) ?? "graph";
  if (!state.graphs.has(graphAlias)) {
    diagnostics.push({
      code: "UnknownGraphAlias",
      severity: "error",
      message: `Skill graph alias '${graphAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  // Battle adapter registers units at runtime via bindBattleController,
  // so we don't check unit aliases statically for Battle actions
  const casterAlias = getString(step.parameters.casterAlias);
  if (!casterAlias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "executeBattleSkillGraph requires a casterAlias.",
      path: step.id ?? step.kind
    });
  }

  const targetAlias = getString(step.parameters.targetAlias);
  // Skip unit alias check - units are registered at runtime by bindBattleController

  const targetPointAlias = getString(step.parameters.targetPointAlias);
  // Skip cell alias check if bindBattleController is in setup (cells registered at runtime)
  if (targetPointAlias && !hasBindBattleController && !state.cells.has(targetPointAlias)) {
    diagnostics.push({
      code: "UnknownCellAlias",
      severity: "error",
      message: `Target point alias '${targetPointAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }
}

function validateEnterNode(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const nodeId = getString(step.parameters.nodeId);
  if (!nodeId) {
    diagnostics.push({
      code: "MissingNodeId",
      severity: "error",
      message: "enterNode requires a nodeId parameter.",
      path: step.id ?? step.kind
    });
  }
}

function validateTriggerEvent(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const eventId = getString(step.parameters.eventId);
  if (!eventId) {
    diagnostics.push({
      code: "MissingEventId",
      severity: "error",
      message: "triggerEvent requires an eventId parameter.",
      path: step.id ?? step.kind
    });
  }
}

function validateOpenUI(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const uiId = getString(step.parameters.uiId);
  if (!uiId) {
    diagnostics.push({
      code: "MissingUiId",
      severity: "error",
      message: "openUI requires a uiId parameter.",
      path: step.id ?? step.kind
    });
  }
}

function validateClickElement(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const elementName = getString(step.parameters.elementName);
  if (!elementName) {
    diagnostics.push({
      code: "MissingElementName",
      severity: "error",
      message: "clickElement requires an elementName parameter.",
      path: step.id ?? step.kind
    });
  }
}

function validatePressKey(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  requireStepStringParameter(step, "key", diagnostics);
}

function requireStepStringParameter(
  step: ScenarioStep,
  parameter: string,
  diagnostics: ExpectationDiagnostic[]
): void {
  if (!getString(step.parameters[parameter])) {
    diagnostics.push({
      code: "MissingActionParameter",
      severity: "error",
      message: `${step.kind} requires a ${parameter} parameter.`,
      path: step.id ?? step.kind
    });
  }
}

function validateSetText(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const elementName = getString(step.parameters.elementName);
  if (!elementName) {
    diagnostics.push({
      code: "MissingElementName",
      severity: "error",
      message: "setText requires an elementName parameter.",
      path: step.id ?? step.kind
    });
  }
  const text = getString(step.parameters.text);
  if (text === undefined) {
    diagnostics.push({
      code: "MissingText",
      severity: "error",
      message: "setText requires a text parameter.",
      path: step.id ?? step.kind
    });
  }
}

function validateSetElementEnabled(step: ScenarioStep, diagnostics: ExpectationDiagnostic[]): void {
  const elementName = getString(step.parameters.elementName);
  if (!elementName) {
    diagnostics.push({
      code: "MissingElementName",
      severity: "error",
      message: "setElementEnabled requires an elementName parameter.",
      path: step.id ?? step.kind
    });
  }
  const enabled = step.parameters.enabled;
  if (enabled === undefined || enabled === null) {
    diagnostics.push({
      code: "MissingEnabled",
      severity: "error",
      message: "setElementEnabled requires an enabled parameter.",
      path: step.id ?? step.kind
    });
  }
}

function validateExecuteAbilityOnTarget(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const abilityAlias = getString(step.parameters.abilityAlias);
  if (!abilityAlias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "executeAbilityOnTarget requires an abilityAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.abilities.has(abilityAlias)) {
    diagnostics.push({
      code: "UnknownAbilityAlias",
      severity: "error",
      message: `Ability alias '${abilityAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const targetAlias = getString(step.target) ?? getString(step.parameters.targetAlias);
  if (!targetAlias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "executeAbilityOnTarget requires a target alias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.units.has(targetAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Target alias '${targetAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }
}

function validateExecuteAbilityOnCell(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const abilityAlias = getString(step.parameters.abilityAlias);
  if (!abilityAlias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "executeAbilityOnCell requires an abilityAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.abilities.has(abilityAlias)) {
    diagnostics.push({
      code: "UnknownAbilityAlias",
      severity: "error",
      message: `Ability alias '${abilityAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const cellAlias = getString(step.target) ?? getString(step.parameters.cellAlias);
  if (cellAlias) {
    if (!state.cells.has(cellAlias)) {
      diagnostics.push({
        code: "UnknownCellAlias",
        severity: "error",
        message: `Cell alias '${cellAlias}' does not exist.`,
        path: step.id ?? step.kind
      });
    }
  }
}

function requireKnownUnit(assertion: ScenarioAssertion, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  if (!assertion.target) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: `${assertion.kind} requires a target unit alias.`,
      path: assertion.id ?? assertion.kind
    });
    return;
  }

  if (!state.units.has(assertion.target)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Unit alias '${assertion.target}' does not exist.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireStringExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  if (typeof assertion.expected !== "string" || !assertion.expected.trim()) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires a string expected value.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireNumberExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  if (typeof assertion.expected !== "number" || Number.isNaN(assertion.expected)) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires a numeric expected value.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireIntegerExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  if (typeof assertion.expected !== "number" || !Number.isInteger(assertion.expected)) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires an integer expected value.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireStringArrayExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[]): void {
  if (!Array.isArray(assertion.expected) || assertion.expected.some(value => typeof value !== "string")) {
    diagnostics.push({
      code: "InvalidAssertionExpectedType",
      severity: "error",
      message: `${assertion.kind} requires an array of string expected values.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireBuffName(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  const buffName = getString(assertion.parameters.buffName) ?? (typeof assertion.expected === "string" ? assertion.expected : "");
  if (!buffName) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires a buffName string.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireCellCoordinatesExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  const expected = assertion.expected;
  if (expected == null || typeof expected !== "object" || Array.isArray(expected)) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires { x, y } coordinates as expected value.`,
      path: assertion.id ?? assertion.kind
    });
    return;
  }

  const obj = expected as Record<string, unknown>;
  if (typeof obj.x !== "number" || typeof obj.y !== "number") {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires numeric x/y coordinates.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function getString(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function registerAlias(
  step: ScenarioStep,
  parameterName: string,
  value: unknown,
  aliasSet: Set<string>,
  diagnostics: ExpectationDiagnostic[],
  missingCode: string
): void {
  const alias = getString(value);
  if (!alias) {
    diagnostics.push({
      code: missingCode,
      severity: "error",
      message: `${step.kind} requires a ${parameterName}.`,
      path: step.id ?? step.kind
    });
    return;
  }

  aliasSet.add(alias);
}

function resetAliasState(state: AliasState): void {
  state.graphs.clear();
  state.cells.clear();
  state.units.clear();
  state.abilityConfigs.clear();
  state.abilities.clear();
}

function validateSemanticRules(spec: ScenarioSpec, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  if (spec.tags.includes("player-input-e2e")) {
    if (!spec.requiredAdapters.includes("PlayerInput")) {
      diagnostics.push({
        code: "MissingPlayerInputAdapter",
        severity: "error",
        message: "player-input-e2e scenarios must require the PlayerInput adapter."
      });
    }

    for (const setup of spec.setup) {
      if (setup.kind !== "initializePlayerInput" || (setup.adapter && setup.adapter !== "PlayerInput")) {
        diagnostics.push({
          code: "PlayerInputE2ESetupShortcut",
          severity: "error",
          message: `player-input-e2e setup cannot execute '${setup.kind}'. Only initializePlayerInput is allowed.`,
          path: setup.id ?? setup.kind
        });
      }
    }

    for (const action of spec.actions) {
      if (!playerInputActionKinds.has(action.kind) || (action.adapter && action.adapter !== "PlayerInput")) {
        diagnostics.push({
          code: "PlayerInputE2EActionShortcut",
          severity: "error",
          message: `player-input-e2e runtime action '${action.kind}' must execute through PlayerInput.`,
          path: action.id ?? action.kind
        });
      }
    }
  }

  const graphKinds = new Map<string, string>();
  for (const step of spec.setup) {
    if (step.kind === "createSkillGraph") {
      const alias = getString(step.parameters.alias) ?? "graph";
      const graphKind = getString(step.parameters.graphKind) ?? "";
      graphKinds.set(alias, graphKind);
    }
  }

  for (const action of spec.actions) {
    if (action.kind === "executeSkillGraph") {
      const graphAlias = getString(action.parameters.graphAlias) ?? "graph";
      const graphKind = graphKinds.get(graphAlias);

      if (graphKind === "areaDamage") {
        const targetPointAlias = getString(action.parameters.targetPointAlias);
        if (!targetPointAlias) {
          diagnostics.push({
            code: "MissingTargetPoint",
            severity: "error",
            message: "areaDamage graph requires a targetPointAlias in executeSkillGraph action.",
            path: action.id ?? action.kind
          });
        }
      }

      if (graphKind === "applyBuff") {
        const graphSetup = spec.setup.find(s =>
          s.kind === "createSkillGraph" &&
          (getString(s.parameters.alias) ?? "graph") === graphAlias
        );
        if (graphSetup) {
          const buffName = getString(graphSetup.parameters.buffName);
          const duration = graphSetup.parameters.duration;
          const selectionKind = getString(graphSetup.parameters.selectionKind);
          if (!buffName) {
            diagnostics.push({
              code: "MissingBuffName",
              severity: "warning",
              message: "applyBuff graph should specify a buffName parameter.",
              path: graphSetup.id ?? graphSetup.kind
            });
          }
          if (duration == null || (typeof duration === "number" && duration <= 0)) {
            diagnostics.push({
              code: "InvalidBuffDuration",
              severity: "warning",
              message: "applyBuff graph should specify a positive duration.",
              path: graphSetup.id ?? graphSetup.kind
            });
          }
          if (!selectionKind) {
            diagnostics.push({
              code: "MissingSelectionKind",
              severity: "warning",
              message: "applyBuff graph should specify a selectionKind (self/enemy/ally).",
              path: graphSetup.id ?? graphSetup.kind
            });
          }
        }
      }
    }
  }

  for (const assertion of spec.assertions) {
    if (assertion.kind === "unitBuffIsUnique" || assertion.kind === "unitBuffCountEquals") {
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingUnitAlias",
          severity: "error",
          message: `${assertion.kind} requires a target unit alias.`,
          path: assertion.id ?? assertion.kind
        });
      }
      const buffName = getString(assertion.parameters.buffName);
      if (!buffName && typeof assertion.expected !== "string") {
        diagnostics.push({
          code: "MissingBuffName",
          severity: "error",
          message: `${assertion.kind} requires a buffName parameter.`,
          path: assertion.id ?? assertion.kind
        });
      }
    }

    if (assertion.kind === "unitCountInArea") {
      const centerAlias = getString(assertion.parameters.centerAlias);
      const radius = assertion.parameters.radius;
      if (!centerAlias) {
        diagnostics.push({
          code: "MissingCenterAlias",
          severity: "error",
          message: "unitCountInArea requires a centerAlias parameter.",
          path: assertion.id ?? assertion.kind
        });
      }
      if (radius == null || typeof radius !== "number" || radius <= 0) {
        diagnostics.push({
          code: "InvalidRadius",
          severity: "error",
          message: "unitCountInArea requires a positive radius parameter.",
          path: assertion.id ?? assertion.kind
        });
      }
    }

    if (assertion.kind.startsWith("projectile")) {
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingProjectileTarget",
          severity: "error",
          message: `${assertion.kind} requires a target unit alias.`,
          path: assertion.id ?? assertion.kind
        });
      }
    }

    if (assertion.kind === "multiStageStateEquals") {
      const stageIndex = assertion.parameters.stageIndex;
      if (stageIndex == null || typeof stageIndex !== "number" || stageIndex < 0) {
        diagnostics.push({
          code: "InvalidStageIndex",
          severity: "error",
          message: "multiStageStateEquals requires a non-negative stageIndex parameter.",
          path: assertion.id ?? assertion.kind
        });
      }
    }
  }
}

export function formatZodError(error: z.ZodError): ExpectationDiagnostic[] {
  return error.issues.map(issue => ({
    code: "SchemaValidationFailed",
    severity: "error",
    message: issue.message,
    path: issue.path.join(".")
  }));
}
