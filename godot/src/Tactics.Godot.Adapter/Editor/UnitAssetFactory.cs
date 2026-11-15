#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Application.Units;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Generates the frozen Pure Run Unit resources and scenes exclusively through ResourceSaver.
/// </summary>
public static class UnitAssetFactory
{
    private const string BatchId = "pure-run-units-v1";
    private const string DefaultRoot = "res://content/units";
    private const string ActorPath = DefaultRoot + "/UnitActor.tscn";
    private const string CatalogPath = DefaultRoot + "/ContentCatalog.tres";
    private const string GalleryPath = DefaultRoot + "/UnitGallery.tscn";
    private const string SpawnFixturePath = DefaultRoot + "/UnitSpawnFixture.tscn";

    public static void Build(string? draftPath = null, string root = DefaultRoot)
    {
        if (root != DefaultRoot)
            throw new ArgumentException($"Unit assets must use the canonical root '{DefaultRoot}'.", nameof(root));
        string projectPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repositoryPath = Directory.GetParent(projectPath)?.FullName
            ?? throw new InvalidOperationException("Cannot resolve migration repository root.");
        draftPath ??= Path.Combine(
            repositoryPath,
            "Tools",
            "migration",
            "out",
            "pure-run-units-v1.draft.json");
        UnitMigrationDraft draft = UnitMigrationDraft.Load(draftPath);
        IReadOnlyDictionary<ContentId, CompiledUnitDefinition> definitions =
            draft.CompileApplicationDefinitions();
        if (definitions.Count != 12)
            throw new InvalidOperationException("Pure Run Unit draft must compile exactly 12 definitions.");
        Shader goatTintShader = ResourceLoader.Load<Shader>(
            draft.TintContract.GodotShaderPath,
            string.Empty,
            ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical Goat body tint shader cannot be loaded.");

        EnsureDirectory(root);
        string ledgerPath = Path.Combine(
            repositoryPath,
            "Tools",
            "migration",
            "manifest",
            "state",
            $"{BatchId}.json");
        string[] unitPaths = draft.Units
            .Select(unit => ResourcePath(unit.ContentId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] targetPaths = unitPaths
            .Append(ActorPath)
            .Append(CatalogPath)
            .Append(GalleryPath)
            .Append(SpawnFixturePath)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var transaction = new ResourceSaveTransaction(ledgerPath, draft.Source);
        transaction.Preflight(targetPaths);

        try
        {
            SaveActorScene(ActorPath);
            PackedScene actorScene = ResourceLoader.Load<PackedScene>(
                ActorPath,
                string.Empty,
                ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException("Generated Unit actor scene cannot be loaded.");

            var savedDefinitions = new List<UnitDefinitionResource>();
            foreach (UnitDraftRecord unit in draft.Units.OrderBy(item => item.ContentId, StringComparer.Ordinal))
            {
                string resourcePath = ResourcePath(unit.ContentId);
                var resource = LoadOrCreate<UnitDefinitionResource>(resourcePath);
                Populate(
                    resource,
                    unit,
                    draft.ActorContentId,
                    draft.SpriteContract,
                    actorScene,
                    goatTintShader);
                ApplyAttributeBalanceOverrides(resource);
                resource.ToCoreDefinition();
                SaveResource(resource, resourcePath);
                savedDefinitions.Add(resource);
            }

            var catalog = LoadOrCreate<GodotResourceCatalog>(CatalogPath);
            catalog.Entries = savedDefinitions
                .Select(resource => Entry(
                    resource.ContentIdValue,
                    "unit",
                    ResourcePath(resource.ContentIdValue),
                    draft.ActorContentId))
                .Append(Entry(draft.ActorContentId, "packed-scene", ActorPath))
                .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
                .ToArray();
            SaveResource(catalog, CatalogPath);
            catalog.Validate();
            UnitBatchValidator.Validate(catalog);

            SaveScene<GodotUnitGallery>(GalleryPath, "UnitGallery", node => node.Catalog = catalog);
            SaveScene<GodotUnitSpawnFixture>(
                SpawnFixturePath,
                "UnitSpawnFixture",
                node => node.Catalog = catalog);

            var semantics = draft.Units.ToDictionary(
                unit => ResourcePath(unit.ContentId),
                unit => JsonSerializer.SerializeToElement(new
                {
                    unit,
                    spriteContract = draft.SpriteContract
                }),
                StringComparer.Ordinal);
            semantics[ActorPath] = JsonSerializer.SerializeToElement(new
            {
                contentId = draft.ActorContentId,
                rootType = nameof(GodotUnitActor),
                children = new[] { "Shadow", "Body" },
                contract = "cardinal-dr-ul-body-only-mirroring-v2",
                directionMapping = new
                {
                    south = "down-right",
                    north = "up-left",
                    east = "up-left+flip-x",
                    west = "down-right+flip-x"
                },
                tintContract = draft.TintContract.Id,
                spriteContract = draft.SpriteContract.Id
            });
            semantics[CatalogPath] = JsonSerializer.SerializeToElement(
                catalog.Entries.Select(entry => entry.ContentIdValue).Order(StringComparer.Ordinal).ToArray());
            semantics[GalleryPath] = JsonSerializer.SerializeToElement(new
            {
                unitCount = 12,
                columns = 4,
                controls = new[]
                {
                    "1=south", "2=north", "3=east", "4=west", "d=death", "t=goat-tint", "r=reset"
                },
                reset = "south+living+goat-tint-enabled",
                canvas = new
                {
                    width = UnitPreviewLayout.CanvasWidth,
                    height = UnitPreviewLayout.CanvasHeight,
                    contract = UnitPreviewLayout.CanvasContract
                },
                layoutContract = GodotUnitGallery.LayoutContract,
                actorScale = GodotUnitGallery.ActorScale,
                firstColumnGroundX = GodotUnitGallery.FirstColumnGroundX,
                firstRowGroundY = GodotUnitGallery.FirstRowGroundY,
                columnSpacing = GodotUnitGallery.ColumnSpacing,
                rowSpacing = GodotUnitGallery.RowSpacing,
                labelOffsetX = GodotUnitGallery.LabelOffsetX,
                labelOffsetY = GodotUnitGallery.LabelOffsetY,
                labelSize = new { width = GodotUnitGallery.LabelWidth, height = GodotUnitGallery.LabelHeight },
                fontSize = GodotUnitGallery.PreviewFontSize,
                background = GodotUnitGallery.PreviewBackgroundColor.ToHtml(),
                fixture = nameof(GodotUnitGallery)
            });
            semantics[SpawnFixturePath] = JsonSerializer.SerializeToElement(new
            {
                unitCount = 12,
                board = new { width = 10, height = 10 },
                spawnCells = GodotUnitSpawnFixture.FixedSpawnCells,
                canvas = new
                {
                    width = UnitPreviewLayout.CanvasWidth,
                    height = UnitPreviewLayout.CanvasHeight,
                    contract = UnitPreviewLayout.CanvasContract
                },
                layoutContract = GodotUnitSpawnFixture.LayoutContract,
                gridOrigin = new
                {
                    x = GodotUnitSpawnFixture.GridOrigin.X,
                    y = GodotUnitSpawnFixture.GridOrigin.Y
                },
                cellSize = GodotUnitSpawnFixture.CellSize,
                actorScale = GodotUnitSpawnFixture.ActorScale,
                viewportSafeInset = GodotUnitSpawnFixture.ViewportSafeInset,
                boardSafeInset = GodotUnitSpawnFixture.BoardSafeInset,
                overflowPolicy = GodotUnitSpawnFixture.OverflowPolicy,
                background = GodotUnitSpawnFixture.PreviewBackgroundColor.ToHtml(),
                grid = GodotUnitSpawnFixture.PreviewGridColor.ToHtml(),
                fixture = nameof(GodotUnitSpawnFixture),
                identityContract = "definition-id-separated-from-instance-id"
            });
            transaction.Commit(targetPaths, semantics);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void Populate(
        UnitDefinitionResource resource,
        UnitDraftRecord unit,
        string actorContentId,
        UnitSpriteContract spriteContract,
        PackedScene actorScene,
        Shader goatTintShader)
    {
        UnitDefinitionDraft compiledDraft = unit.ToApplicationDraft(actorContentId, spriteContract);
        resource.SchemaVersion = compiledDraft.SchemaVersion;
        resource.ContentIdValue = compiledDraft.ContentId;
        resource.SourceId = compiledDraft.SourceId;
        resource.DisplayName = compiledDraft.DisplayName;
        resource.Category = unit.Category;
        resource.FamilyId = compiledDraft.FamilyId;
        resource.RoleId = compiledDraft.RoleId;
        resource.Strength = compiledDraft.Strength;
        resource.Agility = compiledDraft.Agility;
        resource.Constitution = compiledDraft.Constitution;
        resource.Intelligence = compiledDraft.Intelligence;
        resource.Charisma = compiledDraft.Charisma;
        resource.Luck = compiledDraft.Luck;
        resource.Speed = compiledDraft.Speed;
        resource.MaxHealth = compiledDraft.MaxHealth;
        resource.MaxMana = compiledDraft.MaxMana;
        resource.StartingMana = compiledDraft.StartingMana;
        resource.MoveRange = compiledDraft.MoveRange;
        resource.Initiative = compiledDraft.Initiative;
        resource.AttackRange = compiledDraft.AttackRange;
        resource.AttackFactor = compiledDraft.AttackFactor;
        resource.DefenceFactor = compiledDraft.DefenceFactor;
        resource.MovementKindValue = compiledDraft.MovementKind;
        resource.CanProduceCorpse = compiledDraft.CanProduceCorpse;
        resource.ActorContentIdValue = compiledDraft.ActorContentId;
        resource.ActorScene = actorScene;
        resource.DownRightTexture = LoadTexture(compiledDraft.DownRightTexture);
        resource.UpLeftTexture = LoadTexture(compiledDraft.UpLeftTexture);
        resource.UnarmedDownRightTexture = compiledDraft.UnarmedDownRightTexture is null ? null : LoadTexture(compiledDraft.UnarmedDownRightTexture);
        resource.UnarmedUpLeftTexture = compiledDraft.UnarmedUpLeftTexture is null ? null : LoadTexture(compiledDraft.UnarmedUpLeftTexture);
        resource.DeathTexture = compiledDraft.DeathTexture is null
            ? null
            : LoadTexture(compiledDraft.DeathTexture);
        ApplyPlayerActionPoses(resource);
        resource.ShadowTexture = LoadTexture(compiledDraft.ShadowTexture);
        resource.DownRightBodyOffset = SpriteOffset(
            resource.DownRightTexture,
            compiledDraft.DownRightPivotX,
            compiledDraft.DownRightPivotY);
        resource.UpLeftBodyOffset = SpriteOffset(
            resource.UpLeftTexture,
            compiledDraft.UpLeftPivotX,
            compiledDraft.UpLeftPivotY);
        resource.DeathBodyOffset = resource.DeathTexture is null
            ? Vector2.Zero
            : SpriteOffset(resource.DeathTexture, compiledDraft.DeathPivotX, compiledDraft.DeathPivotY);
        resource.ShadowOffset = new Vector2(
            compiledDraft.ShadowOffsetX * compiledDraft.BodyPixelsPerUnit,
            -compiledDraft.ShadowOffsetY * compiledDraft.BodyPixelsPerUnit);
        float shadowScale = compiledDraft.BodyPixelsPerUnit /
            (float)compiledDraft.ShadowPixelsPerUnit * compiledDraft.ShadowScale;
        resource.ShadowScale = new Vector2(shadowScale, shadowScale);
        resource.ShadowOpacity = compiledDraft.ShadowOpacity;
        resource.BodyTint = new Color(
            compiledDraft.BodyTintRed,
            compiledDraft.BodyTintGreen,
            compiledDraft.BodyTintBlue,
            compiledDraft.BodyTintAlpha);
        resource.BodyTintModeValue = compiledDraft.BodyTintMode;
        resource.BaseBodyColor = new Color(
            compiledDraft.BaseBodyColorRed,
            compiledDraft.BaseBodyColorGreen,
            compiledDraft.BaseBodyColorBlue,
            compiledDraft.BaseBodyColorAlpha);
        if (compiledDraft.BodyTintMode == UnitBodyTintModes.GoatBodyMaskV1)
        {
            ShaderMaterial material = resource.BodyTintMaterial ?? new ShaderMaterial();
            material.Shader = goatTintShader;
            material.SetShaderParameter("body_tint", resource.BodyTint);
            material.SetShaderParameter("base_body_color", resource.BaseBodyColor);
            resource.BodyTintMaterial = material;
        }
        else
        {
            resource.BodyTintMaterial = null;
        }
        resource.DeferredDependencies = compiledDraft.DeferredDependencies
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static Texture2D LoadTexture(string path) => ResourceLoader.Load<Texture2D>(
        path,
        string.Empty,
        ResourceLoader.CacheMode.Ignore)
        ?? throw new InvalidOperationException($"Cannot load migrated Unit texture '{path}'.");

    public static void UpgradeExistingAttributeBalance(string root = DefaultRoot)
    {
        if (root != DefaultRoot)
            throw new ArgumentException($"Unit assets must use the canonical root '{DefaultRoot}'.", nameof(root));

        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath,
            string.Empty,
            ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical Unit Catalog cannot be loaded.");
        foreach (GodotResourceEntry entry in catalog.Entries
                     .Where(item => item.ResourceTypeIdValue == "unit")
                     .OrderBy(item => item.ContentIdValue, StringComparer.Ordinal))
        {
            UnitDefinitionResource resource = ResourceLoader.Load<UnitDefinitionResource>(
                entry.DiagnosticPathValue,
                string.Empty,
                ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Unit Resource '{entry.DiagnosticPathValue}' cannot be loaded.");
            bool primaryChanged = ApplyPrimaryAttribute(resource);
            if (resource.DerivedStatModeValue == "explicit")
            {
                resource.ToCoreDefinition();
                if (primaryChanged)
                    SaveResource(resource, entry.DiagnosticPathValue);
                continue;
            }

            ApplyAttributeBalanceOverrides(resource);
            resource.ToCoreDefinition();
            SaveResource(resource, entry.DiagnosticPathValue);
        }

        // Demonbound and Poet are generated by their own factories and live outside the unit catalog root.
        foreach (string classPath in new[] { "res://content/demonbound/PureRunDemonbound.tres", "res://content/poet/PureRunPoet.tres" })
        {
            UnitDefinitionResource classResource = ResourceLoader.Load<UnitDefinitionResource>(
                classPath, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Class Unit Resource '{classPath}' cannot be loaded.");
            if (!ApplyPrimaryAttribute(classResource)) continue;
            classResource.ToCoreDefinition();
            SaveResource(classResource, classPath);
        }

        UnitBatchValidator.Validate(catalog);
    }

    /// <summary>
    /// Writes the class primary attribute from <see cref="PrimaryAttributeRules"/> (keyed by the player-class
    /// content id) so authored data and combat rules cannot disagree. Returns true when the value changed.
    /// </summary>
    public static bool ApplyPrimaryAttribute(UnitDefinitionResource resource)
    {
        SkillRole role = resource.ContentIdValue switch
        {
            "unit.pure-run.mage" => SkillRole.Mage,
            "unit.pure-run.necromancer" => SkillRole.Necromancer,
            "unit.pure-run.demonbound" => SkillRole.Demonbound,
            "unit.pure-run.poet" => SkillRole.Poet,
            "unit.pure-run.amazon" => SkillRole.Amazon,
            _ => SkillRole.Any
        };
        string value = PrimaryAttributeRules.For(role).ToString();
        if (resource.PrimaryAttributeValue == value)
            return false;
        resource.PrimaryAttributeValue = value;
        return true;
    }

    private static void ApplyAttributeBalanceOverrides(UnitDefinitionResource resource)
    {
        (UnitAttributes Attributes, int MovementTrait) target = resource.ContentIdValue switch
        {
            "unit.pure-run.mage" => (new UnitAttributes(4, 5, 3, 6, 6, 6), 0),
            "unit.pure-run.necromancer" => (new UnitAttributes(5, 3, 6, 6, 6, 4), 0),
            "unit.pure-run.amazon" => (new UnitAttributes(5, 5, 5, 5, 5, 5), 0),
            "unit.pure-run.skeleton-warrior" or "unit.pure-run.skeleton-mage" =>
                (new UnitAttributes(4, 4, 2, 2, 2, 2), -1),
            "unit.pure-run.fire-demon" => (new UnitAttributes(5, 4, 3, 5, 5, 5), -1),
            "unit.pure-run.goat-charger" or "unit.pure-run.goat-elite-charger" =>
                (WithAgility(resource, 8), 0),
            "unit.pure-run.goat-ranged" => (WithAgility(resource, 12), 0),
            "unit.pure-run.goat-support" => (WithAgility(resource, 8), 0),
            "unit.pure-run.goat-aoe" or "unit.pure-run.goat-elite-poison-caster" =>
                (WithAgility(resource, 6), -1),
            _ => (new UnitAttributes(resource.Strength, resource.Agility, resource.Constitution,
                resource.Intelligence, resource.Charisma, resource.Luck), 0)
        };
        resource.Strength = target.Attributes.Strength;
        resource.Agility = target.Attributes.Agility;
        resource.Constitution = target.Attributes.Constitution;
        resource.Intelligence = target.Attributes.Intelligence;
        resource.Charisma = target.Attributes.Charisma;
        resource.Luck = target.Attributes.Luck;
        resource.MovementTraitModifier = target.MovementTrait;
        UnitDerivedStats derived = UnitDerivedStatRules.Calculate(target.Attributes, target.MovementTrait);
        resource.MaxHealth = derived.MaxHealth;
        resource.MaxMana = derived.MaxMana;
        resource.StartingMana = derived.StartingMana;
        resource.MoveRange = derived.MoveRange;
        resource.Initiative = derived.Initiative;
        resource.DerivedStatModeValue = "frozen-formula";
    }

    private static UnitAttributes WithAgility(UnitDefinitionResource resource, int agility) => new(
        resource.Strength, agility, resource.Constitution, resource.Intelligence, resource.Charisma, resource.Luck);

    private static void ApplyPlayerActionPoses(UnitDefinitionResource resource)
    {
        string? prefix = resource.ContentIdValue switch
        {
            "unit.pure-run.mage" => "doge_mage",
            "unit.pure-run.necromancer" => "doge_necromancer",
            "unit.pure-run.amazon" => "doge_hunter",
            _ => null
        };
        resource.MeleeDownRightTexture = resource.MeleeUpLeftTexture = null;
        resource.RangedDownRightTexture = resource.RangedUpLeftTexture = null;
        resource.CastDownRightTexture = resource.CastUpLeftTexture = null;
        resource.HitDownRightTexture = resource.HitUpLeftTexture = null;
        if (prefix is null) return;
        Texture2D Pose(string suffix) => LoadTexture($"res://assets/units/actions/{prefix}_{suffix}.png");
        resource.CastDownRightTexture = Pose("cast_dr");
        resource.CastUpLeftTexture = Pose("cast_ul");
        resource.HitDownRightTexture = Pose("hit_dr");
        resource.HitUpLeftTexture = Pose("hit_ul");
        if (resource.ContentIdValue == "unit.pure-run.amazon")
        {
            resource.MeleeDownRightTexture = Pose("melee_attack_dr");
            resource.MeleeUpLeftTexture = Pose("melee_attack_ul");
            resource.RangedDownRightTexture = resource.MeleeDownRightTexture;
            resource.RangedUpLeftTexture = resource.MeleeUpLeftTexture;
        }
    }

    private static Vector2 SpriteOffset(Texture2D texture, float pivotX, float pivotY) => new(
        texture.GetWidth() * (0.5f - pivotX),
        texture.GetHeight() * (pivotY - 0.5f));

    private static void SaveActorScene(string path)
    {
        PackedScene packedScene = File.Exists(ProjectSettings.GlobalizePath(path))
            ? ResourceLoader.Load<PackedScene>(path, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Existing scene '{path}' cannot be loaded.")
            : new PackedScene();
        GodotUnitActor root;
        if (packedScene.CanInstantiate())
        {
            Node instance = packedScene.Instantiate();
            root = instance as GodotUnitActor ?? throw new InvalidOperationException(
                $"Existing scene '{path}' does not instantiate {nameof(GodotUnitActor)}.");
            if (root.GetChildCount() != 2 || root.Shadow is null || root.Body is null)
            {
                root.Free();
                throw new InvalidOperationException("Existing Unit actor scene has an unexpected node layout.");
            }
        }
        else
        {
            root = new GodotUnitActor();
            var shadow = new Sprite2D { Name = "Shadow" };
            var body = new Sprite2D { Name = "Body" };
            root.AddChild(shadow);
            root.AddChild(body);
            shadow.Owner = root;
            body.Owner = root;
            root.Shadow = shadow;
            root.Body = body;
        }

        root.Name = "UnitActor";
        root.Shadow!.Name = "Shadow";
        root.Shadow.Centered = true;
        root.Shadow.ZIndex = 0;
        root.Shadow.FlipH = false;
        root.Shadow.Modulate = Colors.White;
        root.Shadow.Scale = Vector2.One;
        root.Body!.Name = "Body";
        root.Body.Centered = true;
        root.Body.Offset = Vector2.Zero;
        root.Body.ZIndex = 1;
        root.Body.FlipH = false;
        Error packError = packedScene.Pack(root);
        root.Free();
        if (packError != Error.Ok)
            throw new InvalidOperationException($"Cannot pack '{path}': {packError}");
        SaveResource(packedScene, path);
    }

    private static void SaveScene<T>(string path, string name, Action<T> configure)
        where T : Node, new()
    {
        PackedScene packedScene = File.Exists(ProjectSettings.GlobalizePath(path))
            ? ResourceLoader.Load<PackedScene>(path, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Existing scene '{path}' cannot be loaded.")
            : new PackedScene();
        T candidate = new();
        bool nativeRootCompatible = packedScene.CanInstantiate() &&
            packedScene.GetState().GetNodeType(0) == candidate.GetClass();
        T root;
        if (nativeRootCompatible)
        {
            candidate.Free();
            Node instance = packedScene.Instantiate();
            if (instance is T typed)
            {
                root = typed;
            }
            else
            {
                // The generator owns these ledger-protected targets. Rebuild the root when
                // an intentional C# base-node migration makes the old serialized root incompatible.
                instance.Free();
                root = new T();
            }
        }
        else
        {
            root = candidate;
        }
        root.Name = name;
        configure(root);
        Error packError = packedScene.Pack(root);
        root.Free();
        if (packError != Error.Ok)
            throw new InvalidOperationException($"Cannot pack '{path}': {packError}");
        SaveResource(packedScene, path);
    }

    private static T LoadOrCreate<T>(string path) where T : Resource, new() =>
        File.Exists(ProjectSettings.GlobalizePath(path))
            ? ResourceLoader.Load<T>(path, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException(
                    $"Existing resource '{path}' could not be loaded as {typeof(T).Name}.")
            : new T();

    private static GodotResourceEntry Entry(
        string contentId,
        string resourceTypeId,
        string path,
        params string[] references)
    {
        long uid = UidForPath(path);
        if (uid == ResourceUid.InvalidId)
            throw new InvalidOperationException($"Saved Unit resource '{path}' has no UID.");
        return new GodotResourceEntry
        {
            ContentIdValue = contentId,
            ResourceTypeIdValue = resourceTypeId,
            ResourceUidValue = ResourceUid.IdToText(uid),
            DiagnosticPathValue = path,
            SchemaVersion = 1,
            ReferenceContentIds = references.Order(StringComparer.Ordinal).ToArray()
        };
    }

    private static string ResourcePath(string contentId)
    {
        const string prefix = "unit.pure-run.";
        if (!contentId.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected Pure Run Unit ContentId '{contentId}'.");
        string name = string.Concat(contentId[prefix.Length..].Split('-').Select(segment =>
            char.ToUpperInvariant(segment[0]) + segment[1..]));
        return $"{DefaultRoot}/PureRun{name}.tres";
    }

    private static void EnsureDirectory(string resourceDirectory)
    {
        Error error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(resourceDirectory));
        if (error != Error.Ok && error != Error.AlreadyExists)
            throw new InvalidOperationException($"Cannot create '{resourceDirectory}': {error}");
    }

    private static void SaveResource(Resource resource, string path)
    {
        long existingUid = UidForPath(path);
        long uid = existingUid != ResourceUid.InvalidId
            ? existingUid
            : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid))
            ResourceUid.AddId(uid, path);
        else if (!string.Equals(ResourceUid.GetIdPath(uid), path, StringComparison.Ordinal))
            throw new InvalidOperationException($"Resource UID collision for '{path}'.");
        DeterministicResourceSaver.Save(resource,path,uid);
    }

    private static long UidForPath(string path)
    {
        string uidText = ResourceUid.PathToUid(path);
        return uidText.StartsWith("uid://", StringComparison.Ordinal)
            ? ResourceUid.TextToId(uidText)
            : ResourceUid.InvalidId;
    }

    private sealed class ResourceSaveTransaction
    {
        private readonly string _ledgerPath;
        private readonly UnitDraftSource _source;
        private readonly Dictionary<string, byte[]?> _backups = new(StringComparer.OrdinalIgnoreCase);

        public ResourceSaveTransaction(string ledgerPath, UnitDraftSource source)
        {
            _ledgerPath = ledgerPath;
            _source = source;
        }

        public void Preflight(IEnumerable<string> resourcePaths)
        {
            string[] paths = resourcePaths.ToArray();
            foreach (string resourcePath in paths)
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                _backups[absolutePath] = File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : null;
            }
            _backups[_ledgerPath] = File.Exists(_ledgerPath) ? File.ReadAllBytes(_ledgerPath) : null;

            if (!File.Exists(_ledgerPath))
            {
                string? unmanaged = paths.FirstOrDefault(path =>
                    File.Exists(ProjectSettings.GlobalizePath(path)));
                if (unmanaged is not null)
                {
                    throw new InvalidOperationException(
                        $"Unit target exists without a migration ledger: {unmanaged}");
                }
                return;
            }

            using JsonDocument previousLedger = JsonDocument.Parse(File.ReadAllText(_ledgerPath));
            JsonElement root = previousLedger.RootElement;
            if (root.GetProperty("batchId").GetString() != BatchId ||
                root.GetProperty("source").GetProperty("exportHash").GetString() != _source.ExportHash)
            {
                throw new InvalidOperationException("Pure Run Unit migration ledger source binding changed.");
            }
            JsonElement[] recordedArtifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
            if (recordedArtifacts.Length != paths.Length)
                throw new InvalidOperationException("Pure Run Unit migration ledger artifact count changed.");
            var expectedHashes = recordedArtifacts.ToDictionary(
                item => item.GetProperty("resourcePath").GetString()!,
                item => item.GetProperty("targetHash").GetString()!,
                StringComparer.Ordinal);
            foreach (JsonElement artifact in recordedArtifacts)
            {
                string resourcePath = artifact.GetProperty("resourcePath").GetString()!;
                long uid = ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!);
                if (!ResourceUid.HasId(uid))
                    ResourceUid.AddId(uid, resourcePath);
                else if (!string.Equals(ResourceUid.GetIdPath(uid), resourcePath, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Recorded UID now maps to another resource: {resourcePath}");
            }
            foreach (string resourcePath in paths)
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                if (!expectedHashes.TryGetValue(resourcePath, out string? expectedHash) ||
                    !File.Exists(absolutePath) || Hash(File.ReadAllBytes(absolutePath)) != expectedHash)
                {
                    throw new InvalidOperationException(
                        $"Generated Unit target changed after the last migration: {resourcePath}");
                }
            }
        }

        public void Commit(
            IEnumerable<string> resourcePaths,
            IReadOnlyDictionary<string, JsonElement> semanticModels)
        {
            var artifacts = resourcePaths.Order(StringComparer.Ordinal).Select(resourcePath =>
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                if (!File.Exists(absolutePath))
                    throw new InvalidOperationException($"Generated Unit target is missing: {resourcePath}");
                long uid = UidForPath(resourcePath);
                if (uid == ResourceUid.InvalidId)
                    throw new InvalidOperationException($"Generated Unit target has no UID: {resourcePath}");
                return new
                {
                    resourcePath,
                    resourceUid = ResourceUid.IdToText(uid),
                    targetHash = Hash(File.ReadAllBytes(absolutePath)),
                    semanticHash = Hash(Encoding.UTF8.GetBytes(semanticModels[resourcePath].GetRawText()))
                };
            }).ToArray();
            var ledger = new
            {
                schemaVersion = 1,
                batchId = BatchId,
                source = new
                {
                    sourceTag = _source.SourceTag,
                    sourceCommit = _source.SourceCommit,
                    unityVersion = _source.UnityVersion,
                    exporterVersion = _source.ExporterVersion,
                    exportHash = _source.ExportHash,
                    derivedContract = _source.DerivedContract
                },
                artifacts
            };
            string payload = JsonSerializer.Serialize(
                ledger,
                new JsonSerializerOptions { WriteIndented = true }) + "\n";
            Directory.CreateDirectory(Path.GetDirectoryName(_ledgerPath)!);
            string temporaryPath = _ledgerPath + ".tmp";
            File.WriteAllText(temporaryPath, payload, new UTF8Encoding(false));
            File.Move(temporaryPath, _ledgerPath, overwrite: true);
        }

        public void Rollback()
        {
            foreach ((string path, byte[]? payload) in _backups.Reverse())
            {
                if (payload is null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, payload);
            }
        }

        private static string Hash(byte[] payload) =>
            "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}
#endif
