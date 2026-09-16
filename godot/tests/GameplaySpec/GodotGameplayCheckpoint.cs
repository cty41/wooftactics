using System.Security.Cryptography;
using System.Text;
using Godot;
using Tactics.Application.Runs;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Tests.GameplaySpec;

public sealed class ValidatedGodotRunCheckpoint
{
    private ValidatedGodotRunCheckpoint(string id, string path, string semanticHash, PureRunSaveSnapshot snapshot)
    {
        Id = id;
        Path = path;
        SemanticHash = semanticHash;
        Snapshot = snapshot;
    }

    public string Id { get; }
    public string Path { get; }
    public string SemanticHash { get; }
    public PureRunSaveSnapshot Snapshot { get; }

    public static ValidatedGodotRunCheckpoint Create(string id, string path, PureRunSaveSnapshot snapshot)
    {
        string encoded = RunSaveDocumentV11.Encode(snapshot);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(encoded))).ToLowerInvariant();
        return new ValidatedGodotRunCheckpoint(id, path, hash, snapshot);
    }

    public bool Verify() => string.Equals(SemanticHash,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RunSaveDocumentV11.Encode(Snapshot)))).ToLowerInvariant(),
        StringComparison.Ordinal);
}

public sealed class GodotGameplayIsolatedRunStore : IRunSaveStore
{
    private readonly GodotRunSaveStore _inner;
    private readonly string _directory;
    public string SavePath { get; }

    public GodotGameplayIsolatedRunStore(string scenarioName, string attemptId, PureRunSaveSnapshot? initial)
    {
        string scenario = Sanitize(scenarioName);
        string attempt = Sanitize(attemptId);
        _directory = $"user://qa-runner/{scenario}/{attempt}";
        if (!_directory.StartsWith("user://qa-runner/", StringComparison.Ordinal))
            throw new InvalidOperationException("QA save isolation escaped its root.");
        SavePath = _directory + "/save-v5.json";
        _inner = new GodotRunSaveStore(path: SavePath);
        if (initial is not null)
        {
            string absolute = ProjectSettings.GlobalizePath(SavePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, RunSaveDocumentV10.Encode(initial), Encoding.UTF8);
            RunStoreResult seed = _inner.Load();
            if (!seed.Succeeded || seed.Snapshot?.Revision != initial.Revision)
                throw new InvalidOperationException("Unable to seed isolated checkpoint: " + seed.ErrorCode);
        }
    }

    public RunStoreResult Load() => _inner.Load();
    public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision) => _inner.Save(snapshot, expectedRevision);

    public int Cleanup()
    {
        string absolute = ProjectSettings.GlobalizePath(_directory);
        if (!Directory.Exists(absolute)) return 0;
        string qaRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("user://qa-runner"));
        string resolved = Path.GetFullPath(absolute);
        if (!resolved.StartsWith(qaRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to clean outside the QA runner root.");
        int files = Directory.GetFiles(resolved, "*", SearchOption.AllDirectories).Length;
        Directory.Delete(resolved, true);
        return files;
    }

    private static string Sanitize(string value)
    {
        string result = new(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray());
        return string.IsNullOrWhiteSpace(result) ? throw new ArgumentException("Isolation identity is empty.") : result;
    }
}
