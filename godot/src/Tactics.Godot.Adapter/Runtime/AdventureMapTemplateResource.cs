using Godot;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
[GlobalClass]
public partial class AdventureMapTemplateResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string BoardContentIdValue { get; set; } = string.Empty;
    [Export] public int Width { get; set; } = 10;
    [Export] public int Height { get; set; } = 10;
    [Export] public string BlockedCellsValue { get; set; } = string.Empty;
    [Export] public string ObjectsValue { get; set; } = string.Empty;
    [Export] public string ActorsValue { get; set; } = string.Empty;
    [Export] public Vector2I BoardEntryCell { get; set; }
    [Export] public Vector2I BoardExitCell { get; set; }
    [Export] public string CandidateSlotsValue { get; set; } = string.Empty;
    [Export] public string PartyEntrySlotsValue { get; set; } = string.Empty;
    [Export] public string PlayerBattleSlotsValue { get; set; } = string.Empty;
    [Export] public string EnemyBattleSlotsValue { get; set; } = string.Empty;
    [Export] public string EntriesValue { get; set; } = string.Empty;
    [Export] public string ExitsValue { get; set; } = string.Empty;
    [Export] public string ConnectionAnchorsValue { get; set; } = string.Empty;
    [Export] public string CameraFocusAnchorValue { get; set; } = string.Empty;
    [Export] public string AtlasBoundsAnchorValue { get; set; } = string.Empty;
    [Export] public string[] StateLayerIds { get; set; } = Array.Empty<string>();
    [Export] public string TerrainCellsValue { get; set; } = string.Empty;
    [Export] public string DecorationCellsValue { get; set; } = string.Empty;
    [Export] public string MaskCellsValue { get; set; } = string.Empty;
    [Export] public TileSet? TileSet { get; set; }

    public AdventureMapTemplateDefinition ToCoreDefinition()
    {
        if (SchemaVersion != 1) throw new InvalidOperationException("Unsupported adventure map template schema.");
        var board = new AdventureBoardDefinition(new ContentId(BoardContentIdValue), Width, Height,
            ParseCells(BlockedCellsValue), ParseObjects(ObjectsValue), ParseActors(ActorsValue),
            Point(BoardEntryCell), Point(BoardExitCell));
        var definition = new AdventureMapTemplateDefinition(new ContentId(ContentIdValue), board,
            ParseSlots(CandidateSlotsValue), ParseSlots(PartyEntrySlotsValue), ParseSlots(PlayerBattleSlotsValue),
            ParseSlots(EnemyBattleSlotsValue), ParseAnchors(EntriesValue), ParseExits(ExitsValue),
            ParseAnchors(ConnectionAnchorsValue), ParseAnchor(CameraFocusAnchorValue),
            ParseAnchor(AtlasBoundsAnchorValue), StateLayerIds);
        definition.Validate();
        if (TileSet is null) throw new InvalidOperationException("Adventure map template requires a generated TileSet.");
        return definition;
    }

    public IReadOnlyList<GridPoint> TerrainCells => ParseCells(TerrainCellsValue);
    public IReadOnlyList<GridPoint> DecorationCells => ParseCells(DecorationCellsValue);
    public IReadOnlyList<GridPoint> MaskCells => ParseCells(MaskCellsValue);

    private static GridPoint Point(Vector2I value) => new(value.X, value.Y);
    private static GridPoint[] ParseCells(string value) => Parts(value).Select(ParseCell).ToArray();
    private static AdventureMapSlot[] ParseSlots(string value) => Parts(value).Select(item =>
    {
        string[] fields = item.Split('@'); return new AdventureMapSlot(fields[0], ParseCell(fields[1]));
    }).ToArray();
    private static AdventureMapAnchor[] ParseAnchors(string value) => Parts(value).Select(ParseAnchor).ToArray();
    private static AdventureMapAnchor ParseAnchor(string value)
    {
        string[] fields = value.Split('@'); return new AdventureMapAnchor(fields[0], ParseCell(fields[1]));
    }
    private static AdventureMapExitAnchor[] ParseExits(string value) => Parts(value).Select(item =>
    {
        string[] fields = item.Split('@'); return new AdventureMapExitAnchor(fields[0], ParseCell(fields[1]), fields[2], fields[3]);
    }).ToArray();
    private static AdventureActorPlacement[] ParseActors(string value) => Parts(value).Select(item =>
    {
        string[] fields = item.Split('@'); return new AdventureActorPlacement(fields[0], ParseCell(fields[1]));
    }).ToArray();
    private static AdventureBoardObject[] ParseObjects(string value) => Parts(value).Select(item =>
    {
        string[] fields = item.Split('@');
        return new AdventureBoardObject(fields[0], Enum.Parse<AdventureObjectKind>(fields[1]), ParseCell(fields[2]),
            bool.Parse(fields[3]), bool.Parse(fields[4]), Empty(fields[5]), bool.Parse(fields[6]));
    }).ToArray();
    private static string? Empty(string value) => value.Length == 0 ? null : value;
    private static string[] Parts(string value) => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static GridPoint ParseCell(string value)
    {
        string[] fields = value.Split(',');
        if (fields.Length != 2 || !int.TryParse(fields[0], out int x) || !int.TryParse(fields[1], out int y))
            throw new ArgumentException($"Invalid map cell '{value}'.");
        return new GridPoint(x, y);
    }
}
