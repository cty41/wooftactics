#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Godot.Adapter.Runtime;
namespace Tactics.Godot.Adapter.Editor;
public static class LayerFourAssetFactory
{
    public const string BatchId="pure-run-layer4-map-nodes-v1"; private const string Root="res://content/layer4"; private const string Global="res://content/ContentCatalog.tres";
    public static void Build()
    {
        string repo=Directory.GetParent(Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://"))))!.FullName;
        using JsonDocument doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(repo,"Tools","migration","out",BatchId+".draft.json")));
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Root)); var entries=new List<GodotResourceEntry>();var targets=new List<string>();
        Add(doc.RootElement.GetProperty("map").GetProperty("contentId").GetString()!,"run-map",doc.RootElement.GetProperty("map"));
        Add(doc.RootElement.GetProperty("encounter").GetProperty("contentId").GetString()!,"encounter",doc.RootElement.GetProperty("encounter"));
        Add(doc.RootElement.GetProperty("rest").GetProperty("contentId").GetString()!,"rest",doc.RootElement.GetProperty("rest"));
        Add(doc.RootElement.GetProperty("store").GetProperty("contentId").GetString()!,"store",doc.RootElement.GetProperty("store"));
        foreach(JsonElement e in doc.RootElement.GetProperty("events").EnumerateArray())Add(e.GetProperty("contentId").GetString()!,"event",e);
        GodotResourceCatalog old=ResourceLoader.Load<GodotResourceCatalog>(Global,string.Empty,ResourceLoader.CacheMode.Ignore)!;
        // The legacy batch still owns its frozen map evidence, while the ownership-closure batch owns the
        // authoritative editable map. Re-running this exporter must not replace that canonical catalog entry.
        bool hasAuthoritativeMap=old.Entries.Any(e=>e.ContentIdValue=="treasure.pure-run.standard-v1");
        GodotResourceEntry[] catalogEntries=entries.Where(e=>!hasAuthoritativeMap||e.ResourceTypeIdValue!="run-map").ToArray();
        var all=old.Entries.Where(e=>catalogEntries.All(n=>n.ContentIdValue!=e.ContentIdValue)).Select(Copy).Concat(catalogEntries).OrderBy(e=>e.ContentIdValue,StringComparer.Ordinal).ToArray();if(all.Length is not (108 or 109 or 114 or 115 or 116 or 119 or 131 or 141 or 142 or 143 or 185))throw new InvalidOperationException($"Unsupported canonical Catalog count: {all.Length}");var catalog=new GodotResourceCatalog{Entries=all};Save(catalog,Global);catalog.Validate();
        string ledger=Path.Combine(repo,"Tools","migration","manifest","state",BatchId+".json");File.WriteAllText(ledger,JsonSerializer.Serialize(new{schemaVersion=1,batchId=BatchId,state="Generated",ownership="UnityOwned",artifacts=targets.Order().Select(p=>new{resourcePath=p,targetHash="sha256:"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath(p)))).ToLowerInvariant()})},new JsonSerializerOptions{WriteIndented=true})+"\n",new UTF8Encoding(false));
        void Add(string id,string kind,JsonElement payload){string path=Root+"/"+id.Replace('.','_').Replace('-','_')+".tres";var r=new PureRunLayerFourResource{ContentIdValue=id,KindValue=kind,PayloadJson=payload.GetRawText()};Save(r,path);targets.Add(path);entries.Add(new GodotResourceEntry{ContentIdValue=id,ResourceTypeIdValue=kind,ResourceUidValue=ResourceUid.IdToText(Uid(path)),DiagnosticPathValue=path,SchemaVersion=1,ReferenceContentIds=Array.Empty<string>()});}
    }
    private static GodotResourceEntry Copy(GodotResourceEntry e)=>new(){ContentIdValue=e.ContentIdValue,ResourceTypeIdValue=e.ResourceTypeIdValue,ResourceUidValue=e.ResourceUidValue,DiagnosticPathValue=e.DiagnosticPathValue,SchemaVersion=e.SchemaVersion,ReferenceContentIds=e.ReferenceContentIds};
    private static long Uid(string p){string text=ResourceUid.PathToUid(p);long uid=text.StartsWith("uid://",StringComparison.Ordinal)?ResourceUid.TextToId(text):ResourceUid.CreateIdForPath(p);if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,p);return uid;}
    private static void Save(Resource r,string p)=>DeterministicResourceSaver.Save(r,p,Uid(p));
}
#endif
