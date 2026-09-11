using Godot;
using Tactics.Core.Battle;
using Tactics.Core.Runtime;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Minimal runtime entry point for the migration project. Gameplay remains in Core.
/// </summary>
public partial class TacticsMigrationRoot : Node
{
    private GodotPlayableRunTestContext? _testContext;
    public GodotPlayableRunMain? PlayableRun { get; private set; }

    public void ConfigureTestContext(GodotPlayableRunTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsInsideTree()) throw new InvalidOperationException("Test context must be configured before Main.tscn enters the tree.");
        _testContext = context;
    }

    public override void _Ready()
    {
        string[] commandLine = OS.GetCmdlineArgs()
            .Concat(OS.GetCmdlineUserArgs())
            .ToArray();
        if (commandLine.Contains("--validate-poison-spear"))
        {
            ValidatePoisonSpear();
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--play-poison-spear"))
        {
            _ = PlayPoisonSpearAsync();
            return;
        }
        if (commandLine.Contains("--validate-units"))
        {
            ValidateUnits();
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--validate-buffs-items"))
        {
            ValidateBuffsItems();
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--validate-starting-skills"))
        {
            ValidateStartingSkills();
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--validate-ai-encounters"))
        {
            ValidateAiEncounters();
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--validate-run-persistence"))
        {
            ValidateRunPersistence();
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--validate-playable-run-ui"))
        {
            var playable = new GodotPlayableRunMain();
            AddChild(playable);
            if (!playable.IsReadyForInput)
                throw new InvalidOperationException(
                    $"Playable Run UI failed its startup contract: {playable.StartupContractSummary}.");
            GD.Print($"Playable Run UI validation OK: canvas=1600x900, contract={playable.StartupContractSummary}, map=ready");
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--play-unit-gallery"))
        {
            PlayUnitGallery();
            return;
        }
        if (commandLine.Contains("--capture-unit-gallery"))
        {
            CaptureUnitGallery();
            GetTree().Quit();
            return;
        }
        if (commandLine.Contains("--capture-unit-spawn"))
        {
            CaptureUnitSpawnFixture();
            GetTree().Quit();
            return;
        }
        PlayableRun = new GodotPlayableRunMain();
        if (_testContext is not null) PlayableRun.ConfigureTestContext(_testContext);
        AddChild(PlayableRun);
        GD.Print("Tactics Godot playable run UI ready");
    }

    private static void ValidatePoisonSpear()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/poison_spear/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Poison Spear ContentCatalog is missing.");
        PoisonSpearSliceValidation validation = PoisonSpearSliceValidator.Validate(catalog);
        GD.Print($"Poison Spear validation OK: entries={validation.CatalogEntryCount}, damage={validation.Action.Damage}, poisonTurns={validation.Action.PoisonTurns}, presentationNodes={validation.Presentation.Nodes.Count}");
    }

    private async Task PlayPoisonSpearAsync()
    {
        try
        {
            var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/poison_spear/ContentCatalog.tres")
                ?? throw new InvalidOperationException("Poison Spear ContentCatalog is missing.");
            PoisonSpearSliceValidation validation = PoisonSpearSliceValidator.Validate(catalog);
            var presentation = catalog.TryGet("presentation.poison-spear.lv1", out Resource? resource)
                ? resource as PoisonSpearPresentationResource
                : null;
            if (presentation is null)
                throw new InvalidOperationException("Poison Spear presentation is missing from the catalog.");

            var player = new PoisonSpearPresentationPlayer();
            AddChild(player);
            using var scope = new BattleRuntimeScope();
            await player.Start(new Vector2(15f, 15f), new Vector2(35f, 25f), presentation, scope);
            await scope.WhenIdleAsync();
            GD.Print($"Poison Spear presentation OK: actionDamage={validation.Action.Damage}, nodes={validation.Presentation.Nodes.Count}");
            player.QueueFree();
        }
        catch (Exception exception)
        {
            GD.PushError($"Poison Spear presentation failed: {exception}");
            GetTree().Quit(1);
            return;
        }

        GetTree().Quit();
    }

    private static void ValidateUnits()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/units/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Pure Run Unit ContentCatalog is missing.");
        UnitBatchValidation validation = UnitBatchValidator.Validate(catalog);
        var fixtureScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitSpawnFixture.tscn")
            ?? throw new InvalidOperationException("Pure Run Unit spawn fixture is missing.");
        Node fixtureInstance = fixtureScene.Instantiate();
        if (fixtureInstance is not GodotUnitSpawnFixture fixture)
        {
            fixtureInstance.Free();
            throw new InvalidOperationException("Pure Run Unit spawn fixture has the wrong root type.");
        }
        IReadOnlyList<BattleUnitState> states = fixture.CreateStates();
        fixture.Free();
        var galleryScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitGallery.tscn")
            ?? throw new InvalidOperationException("Pure Run Unit gallery is missing.");
        Node galleryInstance = galleryScene.Instantiate();
        bool validGallery = galleryInstance is GodotUnitGallery;
        galleryInstance.Free();
        if (!validGallery || states.Count != 13)
            throw new InvalidOperationException("Pure Run Unit validation fixtures are incomplete.");
        GD.Print(
            $"Pure Run Unit validation OK: entries={validation.CatalogEntryCount}, " +
            $"units={validation.UnitCount}, states={states.Count}");
    }

    private static void ValidateBuffsItems()
    {
        var batchCatalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/buffs_items/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Pure Run Buff/Item ContentCatalog is missing.");
        var globalCatalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Canonical global ContentCatalog is missing.");
        BuffItemBatchValidation validation = BuffItemBatchValidator.Validate(batchCatalog, globalCatalog);
        GD.Print(
            $"Pure Run Buff/Item validation OK: entries={validation.BatchCatalogEntryCount}, " +
            $"global={validation.GlobalCatalogEntryCount}, statuses={validation.StatusCount}, " +
            $"consumables={validation.ConsumableCount}, equipment={validation.EquipmentCount}");
    }

    private static void ValidateStartingSkills()
    {
        var batchCatalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/skills/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Starting-skill ContentCatalog is missing.");
        var globalCatalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Canonical global ContentCatalog is missing.");
        StartingSkillBatchValidation validation = StartingSkillBatchValidator.Validate(batchCatalog, globalCatalog);
        var fixtureScene = ResourceLoader.Load<PackedScene>("res://content/skills/SkillFixture.tscn")
            ?? throw new InvalidOperationException("Starting-skill fixture is missing.");
        Node fixture = fixtureScene.Instantiate();
        bool validFixture = fixture is GodotStartingSkillFixture;
        fixture.Free();
        if (!validFixture)
            throw new InvalidOperationException("Starting-skill fixture has the wrong root type.");
        GD.Print($"Starting-skill validation OK: entries={validation.BatchCount}, global={validation.GlobalCount}, generated={validation.GeneratedCount}");
    }

    private static void ValidateAiEncounters()
    {
        var batch=ResourceLoader.Load<GodotResourceCatalog>("res://content/ai_encounters/ContentCatalog.tres")??throw new InvalidOperationException("AI/Encounter Catalog is missing.");
        var global=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")??throw new InvalidOperationException("Canonical Catalog is missing.");
        AiEncounterBatchValidation validation=AiEncounterBatchValidator.Validate(batch,global);
        PackedScene scene=ResourceLoader.Load<PackedScene>("res://content/ai_encounters/AiEncounterFixture.tscn")??throw new InvalidOperationException("AI/Encounter Fixture is missing.");Node fixture=scene.Instantiate();bool valid=fixture is GodotAiEncounterFixture;if(fixture is GodotAiEncounterFixture gameplay){gameplay._Ready();AiFixtureTurnResult single=gameplay.ExecuteSingleTurn();gameplay.ResetCurrentScenario();AiFixtureRoundResult round=gameplay.ExecuteCurrentRound();if(single.ActorId!="fixture.enemy.0"||round.Turns.Count!=3||round.RoundAfter!=round.RoundBefore+1||round.HitCommandLimit)throw new InvalidOperationException("AI/Encounter Fixture single-turn or full-round execution is invalid.");}fixture.Free();if(!valid)throw new InvalidOperationException("AI/Encounter Fixture root type is invalid.");
        GD.Print($"AI/Encounter validation OK: entries={validation.BatchCount}, global={validation.GlobalCount}, skills={validation.Skills}, ai={validation.Ai}, layouts={validation.Layouts}, encounters={validation.Encounters}");
    }

    private static void ValidateRunPersistence()
    {
        var batch=ResourceLoader.Load<GodotResourceCatalog>("res://content/runs/ContentCatalog.tres")??throw new InvalidOperationException("Pure Run persistence Catalog is missing.");
        var global=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")??throw new InvalidOperationException("Canonical Catalog is missing.");
        var scene=ResourceLoader.Load<PackedScene>("res://content/runs/RunPersistenceFixture.tscn")??throw new InvalidOperationException("Pure Run persistence Fixture is missing.");
        RunPersistenceBatchValidation validation=RunPersistenceBatchValidator.Validate(batch,global,scene);
        GD.Print($"Pure Run persistence validation OK: entries={validation.BatchCount}, global={validation.GlobalCount}, resumes={validation.Fixture.Resumes}, backup={validation.Fixture.BackupRecovered}");
    }

    private void PlayUnitGallery()
    {
        var galleryScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitGallery.tscn")
            ?? throw new InvalidOperationException("Pure Run Unit gallery is missing.");
        Node gallery = galleryScene.Instantiate();
        AddChild(gallery);
        GD.Print("Pure Run Unit gallery ready: units=12");
    }

    private static void CaptureUnitGallery()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/units/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Pure Run Unit ContentCatalog is missing.");
        UnitBatchValidator.Validate(catalog);

        Image canvas = Image.CreateEmpty(
            UnitPreviewLayout.CanvasWidth,
            UnitPreviewLayout.CanvasHeight,
            false,
            Image.Format.Rgba8);
        canvas.Fill(GodotUnitGallery.PreviewBackgroundColor);
        GodotResourceEntry[] entries = catalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "unit")
            .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < entries.Length; index++)
        {
            if (!catalog.TryGet(entries[index].ContentIdValue, out Resource? loaded) ||
                loaded is not UnitDefinitionResource definition)
            {
                throw new InvalidOperationException(
                    $"Unit gallery capture cannot load '{entries[index].ContentIdValue}'.");
            }

            Vector2 groundPosition = GodotUnitGallery.GetActorGroundPosition(index);
            var center = new Vector2I(
                Mathf.RoundToInt(groundPosition.X),
                Mathf.RoundToInt(groundPosition.Y));
            CompositeUnit(
                canvas,
                definition,
                GodotUnitFacing.South,
                false,
                center,
                GodotUnitGallery.ActorScale);
        }

        string outputPath = ProjectSettings.GlobalizePath(
            "res://../Tools/migration/out/pure-run-units-v1-gallery.png");
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new InvalidOperationException("Unit gallery capture path has no parent directory.");
        Directory.CreateDirectory(outputDirectory);

        Error saveError = canvas.SavePng(outputPath);
        if (saveError != Error.Ok)
            throw new InvalidOperationException($"Unit gallery capture failed with Godot error {saveError}.");
        GD.Print($"Pure Run Unit gallery captured: {outputPath}");
    }

    private static void CaptureUnitSpawnFixture()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/units/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Pure Run Unit ContentCatalog is missing.");
        var fixtureScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitSpawnFixture.tscn")
            ?? throw new InvalidOperationException("Pure Run Unit spawn fixture is missing.");
        Node fixtureInstance = fixtureScene.Instantiate();
        if (fixtureInstance is not GodotUnitSpawnFixture fixture)
        {
            fixtureInstance.Free();
            throw new InvalidOperationException("Pure Run Unit spawn fixture has the wrong root type.");
        }
        IReadOnlyList<BattleUnitState> states = fixture.CreateStates();
        fixture.Free();

        Image canvas = Image.CreateEmpty(
            UnitPreviewLayout.CanvasWidth,
            UnitPreviewLayout.CanvasHeight,
            false,
            Image.Format.Rgba8);
        canvas.Fill(GodotUnitSpawnFixture.PreviewBackgroundColor);
        int originX = Mathf.RoundToInt(GodotUnitSpawnFixture.GridOrigin.X);
        int originY = Mathf.RoundToInt(GodotUnitSpawnFixture.GridOrigin.Y);
        int cellSize = Mathf.RoundToInt(GodotUnitSpawnFixture.CellSize);
        for (int line = 0; line <= GodotUnitSpawnFixture.GridSize; line++)
        {
            int offset = line * cellSize;
            canvas.FillRect(
                new Rect2I(
                    originX + offset,
                    originY,
                    1,
                    cellSize * GodotUnitSpawnFixture.GridSize + 1),
                GodotUnitSpawnFixture.PreviewGridColor);
            canvas.FillRect(
                new Rect2I(
                    originX,
                    originY + offset,
                    cellSize * GodotUnitSpawnFixture.GridSize + 1,
                    1),
                GodotUnitSpawnFixture.PreviewGridColor);
        }

        foreach ((BattleUnitState state, int index) in states.Select((state, index) => (state, index)))
        {
            if (!catalog.TryGet(state.Unit.DefinitionId.Value, out Resource? loaded) ||
                loaded is not UnitDefinitionResource definition)
            {
                throw new InvalidOperationException(
                    $"Unit spawn capture cannot load '{state.Unit.DefinitionId.Value}'.");
            }
            Vector2 cellCenter = GodotUnitSpawnFixture.GetCellCenter(state.Unit.Position);
            var center = new Vector2I(
                Mathf.RoundToInt(cellCenter.X),
                Mathf.RoundToInt(cellCenter.Y));
            CompositeUnit(
                canvas,
                definition,
                (GodotUnitFacing)(index % 4),
                false,
                center,
                GodotUnitSpawnFixture.ActorScale);
        }

        string outputPath = ProjectSettings.GlobalizePath(
            "res://../Tools/migration/out/pure-run-units-v1-spawn.png");
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new InvalidOperationException("Unit spawn capture path has no parent directory.");
        Directory.CreateDirectory(outputDirectory);
        Error saveError = canvas.SavePng(outputPath);
        if (saveError != Error.Ok)
            throw new InvalidOperationException($"Unit spawn capture failed with Godot error {saveError}.");
        GD.Print($"Pure Run Unit spawn fixture captured: {outputPath}");
    }

    private static void CompositeUnit(
        Image canvas,
        UnitDefinitionResource definition,
        GodotUnitFacing facing,
        bool showDeath,
        Vector2I center,
        float scale)
    {
        bool usesUpLeft = facing is GodotUnitFacing.North or GodotUnitFacing.East;
        Texture2D bodyTexture = showDeath
            ? definition.DeathTexture!
            : usesUpLeft
                ? definition.UpLeftTexture!
                : definition.DownRightTexture!;
        Image body = GodotUnitTintReference.CopyTextureImage(bodyTexture);
        if (!showDeath && (facing == GodotUnitFacing.East || facing == GodotUnitFacing.West))
            body.FlipX();
        ResizeForCapture(body, scale);
        GodotUnitTintReference.Apply(
            body,
            definition.BodyTintModeValue,
            definition.BodyTint,
            definition.BaseBodyColor);

        Image shadow = GodotUnitTintReference.CopyTextureImage(definition.ShadowTexture!);
        ApplyOpacity(shadow, definition.ShadowOpacity);
        ResizeForCapture(shadow, scale * definition.ShadowScale.X);
        var shadowCenter = center + new Vector2I(
            Mathf.RoundToInt(definition.ShadowOffset.X * scale),
            Mathf.RoundToInt(definition.ShadowOffset.Y * scale));
        Vector2 bodyOffset = showDeath
            ? definition.DeathBodyOffset
            : usesUpLeft
                ? definition.UpLeftBodyOffset
                : definition.DownRightBodyOffset;
        if (!showDeath && (facing == GodotUnitFacing.East || facing == GodotUnitFacing.West))
            bodyOffset.X = -bodyOffset.X;
        var bodyCenter = center + new Vector2I(
            Mathf.RoundToInt(bodyOffset.X * scale),
            Mathf.RoundToInt(bodyOffset.Y * scale));
        BlendCentered(canvas, shadow, shadowCenter);
        BlendCentered(canvas, body, bodyCenter);
    }

    private static void ApplyOpacity(Image image, float opacity)
    {
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color pixel = image.GetPixel(x, y);
                pixel.A *= opacity;
                image.SetPixel(x, y, pixel);
            }
        }
    }

    private static void ResizeForCapture(Image image, float scale)
    {
        image.Resize(
            Math.Max(1, Mathf.RoundToInt(image.GetWidth() * scale)),
            Math.Max(1, Mathf.RoundToInt(image.GetHeight() * scale)),
            Image.Interpolation.Lanczos);
    }

    private static void BlendCentered(Image canvas, Image source, Vector2I center)
    {
        Vector2I destination = center - source.GetSize() / 2;
        canvas.BlendRect(source, new Rect2I(Vector2I.Zero, source.GetSize()), destination);
    }
}
