using System.Security.Cryptography;
using System.Text;
using Godot;
using Tactics.Application.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public interface IRunSaveFileSystem
{
    bool Exists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    void Delete(string path);
    void Move(string source, string destination);
}

public sealed class GodotRunSaveFileSystem : IRunSaveFileSystem
{
    public bool Exists(string path) => global::Godot.FileAccess.FileExists(path);

    public string ReadAllText(string path)
    {
        global::Godot.FileAccess? opened = global::Godot.FileAccess.Open(path, global::Godot.FileAccess.ModeFlags.Read);
        if (opened is null) throw new IOException($"Cannot open '{path}' for reading.");
        using global::Godot.FileAccess file = opened;
        return file.GetAsText();
    }

    public void WriteAllText(string path, string contents)
    {
        string absolute = ProjectSettings.GlobalizePath(path);
        string? directory = Path.GetDirectoryName(absolute);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException($"Save path '{path}' has no directory.");
        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (directoryError is not Error.Ok and not Error.AlreadyExists)
            throw new IOException($"Cannot create save directory: {directoryError}.");
        global::Godot.FileAccess? opened = global::Godot.FileAccess.Open(path, global::Godot.FileAccess.ModeFlags.Write);
        if (opened is null) throw new IOException($"Cannot open '{path}' for writing.");
        using global::Godot.FileAccess file = opened;
        file.StoreString(contents);
        file.Flush();
        if (file.GetError() != Error.Ok)
            throw new IOException($"Cannot flush '{path}': {file.GetError()}.");
    }

    public void Delete(string path)
    {
        if (!Exists(path)) return;
        Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok) throw new IOException($"Cannot delete '{path}': {error}.");
    }

    public void Move(string source, string destination)
    {
        Error error = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(source), ProjectSettings.GlobalizePath(destination));
        if (error != Error.Ok) throw new IOException($"Cannot move '{source}' to '{destination}': {error}.");
    }
}

public sealed class GodotRunSaveStore : IRunSaveStore
{
    public const string DefaultPath = "user://pure-run/save-v1.json";
    private readonly object _gate = new();
    private readonly IRunSaveFileSystem _files;
    private readonly string _main;
    private string Temp => _main + ".tmp";
    private string Backup => _main + ".bak";

    public GodotRunSaveStore(IRunSaveFileSystem? files = null, string path = DefaultPath)
    {
        _files = files ?? new GodotRunSaveFileSystem();
        _main = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Save path cannot be empty.", nameof(path)) : path;
    }

    public RunStoreResult Load()
    {
        lock (_gate) return LoadCore(repairMain: true);
    }

    public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
    {
        lock (_gate)
        {
            RunStoreResult current = LoadCore(repairMain: true);
            if (!current.Succeeded) return current;
            if (current.Snapshot!.Revision != expectedRevision)
                return new RunStoreResult(false, "save.stale_revision", current.Snapshot);
            if (snapshot.Revision <= expectedRevision)
                return new RunStoreResult(false, "save.non_increasing_revision", current.Snapshot);
            string encoded = RunSaveDocumentV11.Encode(snapshot);
            try
            {
                _files.WriteAllText(Temp, encoded);
                RequireValid(Temp, snapshot.Revision);
                if (_files.Exists(_main))
                {
                    string previous = _files.ReadAllText(_main);
                    _files.WriteAllText(Backup, previous);
                    RequireValid(Backup, expectedRevision);
                    _files.Delete(_main);
                }
                _files.Move(Temp, _main);
                PureRunSaveSnapshot saved = RequireValid(_main, snapshot.Revision);
                return new RunStoreResult(true, null, saved);
            }
            catch (Exception)
            {
                TryDelete(Temp);
                TryRestoreBackup();
                return new RunStoreResult(false, "save.write_failed", LoadCore(repairMain: false).Snapshot ?? current.Snapshot);
            }
        }
    }

    private RunStoreResult LoadCore(bool repairMain)
    {
        if (TryDecode(_main, out PureRunSaveSnapshot? main, out bool mainRequiresNewRun))
            return new RunStoreResult(true, mainRequiresNewRun ? "save.run_reset_for_v7" : null, main);
        bool mainExists = _files.Exists(_main);
        if (TryDecode(Backup, out PureRunSaveSnapshot? backup, out bool backupRequiresNewRun))
        {
            if (repairMain)
            {
                if (mainExists) Quarantine(_main);
                _files.WriteAllText(_main, _files.ReadAllText(Backup));
            }
            return new RunStoreResult(true, backupRequiresNewRun ? "save.recovered_from_backup_run_reset_for_v7" : "save.recovered_from_backup", backup);
        }
        bool backupExists = _files.Exists(Backup);
        if (!mainExists && !backupExists) return new RunStoreResult(true, null, new PureRunSaveSnapshot(0, null, null));
        if (repairMain)
        {
            if (mainExists) Quarantine(_main);
            if (backupExists) Quarantine(Backup);
        }
        return new RunStoreResult(false, "save.no_recoverable_document", null);
    }

    private bool TryDecode(string path, out PureRunSaveSnapshot? snapshot, out bool requiresNewRun)
    {
        snapshot = null;
        requiresNewRun = false;
        if (!_files.Exists(path)) return false;
        try
        {
            RunSaveDecodeResultV11 decoded = RunSaveDocumentV11.Decode(_files.ReadAllText(path));
            snapshot = decoded.Snapshot;
            requiresNewRun = decoded.RequiresNewRun;
            return decoded.Succeeded && snapshot is not null;
        }
        catch { return false; }
    }

    private PureRunSaveSnapshot RequireValid(string path, long revision)
    {
        if (!TryDecode(path, out PureRunSaveSnapshot? snapshot, out _) || snapshot!.Revision != revision)
            throw new IOException($"Save verification failed for '{path}'.");
        return snapshot;
    }

    private void Quarantine(string path)
    {
        string contents;
        try { contents = _files.ReadAllText(path); } catch { contents = path; }
        string prefix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents))).ToLowerInvariant()[..12];
        for (int ordinal = 1; ; ordinal++)
        {
            string target = $"{_main}.corrupt-{prefix}-{ordinal}";
            if (_files.Exists(target)) continue;
            _files.Move(path, target);
            return;
        }
    }

    private void TryRestoreBackup()
    {
        try
        {
            if (!_files.Exists(_main) && TryDecode(Backup, out _, out _)) _files.WriteAllText(_main, _files.ReadAllText(Backup));
        }
        catch { }
    }

    private void TryDelete(string path) { try { _files.Delete(path); } catch { } }
}
