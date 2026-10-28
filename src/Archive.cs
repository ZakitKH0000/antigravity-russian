using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

// No external runtime or downloads. Used by Windows PowerShell / .NET Framework.
public sealed class AgRuArchive {
    static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 64000000, RecursionLimit = 100 }; }
    static Dictionary<string,object> Obj(object o) { return (Dictionary<string,object>)o; }
    public static string Hash(byte[] b) { using(var s=SHA256.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
    public static string FileHash(string path) { using(var f=File.OpenRead(path)) using(var s=SHA256.Create()) return BitConverter.ToString(s.ComputeHash(f)).Replace("-", "").ToLowerInvariant(); }
    static readonly UTF8Encoding Utf = new UTF8Encoding(false, true);
    readonly Dictionary<string,object> header;
    readonly Dictionary<string,Dictionary<string,object>> leaves = new Dictionary<string,Dictionary<string,object>>(StringComparer.Ordinal);
    readonly Dictionary<string,byte[]> data = new Dictionary<string,byte[]>(StringComparer.Ordinal);
    public readonly List<string> Notes = new List<string>();
    public string Version { get; private set; }
    public string Name { get; private set; }
    public string Product { get; private set; }
    public AgRuArchive(string path) {
        var raw=File.ReadAllBytes(path);
        if(raw.Length<16 || BitConverter.ToUInt32(raw,0)!=4) throw new Exception("Unsupported ASAR format.");
        long hs=BitConverter.ToUInt32(raw,4), ps=BitConverter.ToUInt32(raw,8), js=BitConverter.ToUInt32(raw,12);
        if(hs!=ps+4 || hs%4!=0 || js<2 || js>hs-8 || hs+8>raw.Length) throw new Exception("Invalid ASAR header.");
        header=Obj(Json().DeserializeObject(Utf.GetString(raw,16,(int)js)));
        Walk(header, "");
        foreach(var pair in leaves) {
            var item=pair.Value;
            if(!item.ContainsKey("offset")) continue;
            long offset=Convert.ToInt64(item["offset"]), size=Convert.ToInt64(item["size"]);
            if(offset<0 || size<0 || size>int.MaxValue || offset>raw.Length-hs-8-size) throw new Exception("Invalid ASAR entry bounds.");
            var content=new byte[(int)size]; Buffer.BlockCopy(raw,(int)(8+hs+offset),content,0,(int)size);
            data.Add(pair.Key,content);
            if(item.ContainsKey("integrity")) Verify(content,Obj(item["integrity"]));
        }
        if(data.ContainsKey("package.json")) {
            var pkg=Obj(Json().DeserializeObject(Text("package.json")));
            Version=pkg.ContainsKey("version") ? Convert.ToString(pkg["version"]) : "unknown";
            Name=pkg.ContainsKey("name") ? Convert.ToString(pkg["name"]) : "";
            Product=pkg.ContainsKey("productName") ? Convert.ToString(pkg["productName"]) : "";
            if(!pkg.ContainsKey("main") || Convert.ToString(pkg["main"])!="dist/main.js") Name="unsupported";
        }
    }
    void Walk(Dictionary<string,object> node,string prefix) {
        foreach(var pair in Obj(node["files"])) {
            if(pair.Key==".." || pair.Key=="." || pair.Key.Contains("/") || pair.Key.Contains("\\")) throw new Exception("Invalid entry name.");
            var item=Obj(pair.Value); string name=prefix+pair.Key;
            if(item.ContainsKey("files")) Walk(item,name+"/"); else leaves.Add(name,item);
        }
    }
    static void Verify(byte[] bytes,Dictionary<string,object> integrity) {
        if(Convert.ToString(integrity["algorithm"])!="SHA256" || Hash(bytes)!=Convert.ToString(integrity["hash"])) throw new Exception("ASAR integrity mismatch.");
        int block=Convert.ToInt32(integrity["blockSize"]);
        if(block<=0) throw new Exception("Invalid integrity block size.");
        var blocks=(object[])integrity["blocks"];
        if(blocks.Length!=(bytes.Length+(long)block-1)/block) throw new Exception("Invalid integrity block count.");
        for(int i=0;i<blocks.Length;i++) {
            int start=checked(i*block), length=Math.Min(block,bytes.Length-start);
            var part=new byte[length]; Buffer.BlockCopy(bytes,start,part,0,length);
            if(Hash(part)!=Convert.ToString(blocks[i])) throw new Exception("ASAR block mismatch.");
        }
    }
    public string Text(string name) { if(!data.ContainsKey(name)) throw new Exception("Missing packed entry: "+name); return Utf.GetString(data[name]); }
    public bool Has(string name) { return data.ContainsKey(name); }
    // Russian translations of this archive (ours and the earlier v3 script) touch only these scripts.
    static readonly string[] TranslatedEntries={"dist/preload.js","dist/ipcHandlers.js","dist/main.js","dist/tray.js","dist/updater.js"};
    // The archive is a small Electron shell, so the version alone does not pin a build. A translated archive must keep
    // every other entry byte for byte and must start its preload with the complete original preload (the overlay is appended).
    public bool DerivedFrom(AgRuArchive original) {
        if(Version!=original.Version || !original.Has("dist/preload.js") || !Has("dist/preload.js") || data.Count<original.data.Count || data.Count>original.data.Count+5) return false;
        foreach(var p in original.data) {
            byte[] mine;
            if(!data.TryGetValue(p.Key,out mine)) return false;
            if(mine.Length==p.Value.Length && mine.SequenceEqual(p.Value)) continue;
            if(!TranslatedEntries.Contains(p.Key)) return false;
        }
        byte[] before=original.data["dist/preload.js"], after=data["dist/preload.js"];
        return after.Length>before.Length && after.Take(before.Length).SequenceEqual(before);
    }
    public bool HasTranslation { get { return Has("dist/preload.js") && (Text("dist/preload.js").Contains("LOCAL RUSSIAN TRANSLATION") || Text("dist/preload.js").Contains("dataset.antigravityRu")); } }
    static int Count(string s,string match) { int n=0,p=0; while((p=s.IndexOf(match,p,StringComparison.Ordinal))>=0) { n++; p+=match.Length; } return n; }
    public void CheckCompatible() {
        if(Name!="antigravity" || Product!="Antigravity" || !Has("dist/preload.js") || !Has("dist/main.js")) throw new Exception("Unsupported Antigravity package structure.");
        string preload=Text("dist/preload.js");
        if(preload.Contains("LOCAL RUSSIAN TRANSLATION") || preload.Contains("dataset.antigravityRu") || preload.Contains("antigravity-ru-recovery")) throw new Exception("Unknown existing translation. Restore its original first.");
        if(!preload.Contains("require(\"electron\")") || !preload.Contains("contextBridge") || !preload.Contains("ipcRenderer")) throw new Exception("Preload architecture changed; manual adaptation required.");
        // Refuse a moved/renamed preload rather than patch an unused script.
        bool referenced=data.Where(p=>p.Key.EndsWith(".js") && p.Key.StartsWith("dist/",StringComparison.Ordinal) && p.Key!="dist/preload.js").Any(p=>Utf.GetString(p.Value).Contains("preload.js"));
        if(!referenced) throw new Exception("Preload location changed; manual adaptation required.");
    }
    public static void CheckExecutable(string path) {
        var bytes=File.ReadAllBytes(path); var sentinel=Encoding.ASCII.GetBytes("dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX"); int found=-1;
        for(int i=0;i<bytes.Length-sentinel.Length-11;i++) {
            if(bytes[i]!=sentinel[0]) continue; bool match=true;
            for(int j=1;j<sentinel.Length;j++) if(bytes[i+j]!=sentinel[j]) { match=false; break; }
            if(match) { if(found>=0) throw new Exception("Ambiguous Electron fuse metadata."); found=i; }
        }
        if(found<0 || bytes[found+sentinel.Length]!=1 || bytes[found+sentinel.Length+1]<5 || bytes[found+sentinel.Length+6]!=(byte)'0') throw new Exception("Archive signature validation or Electron fuse format changed. No patch allowed.");
    }
    public void Build(string destination,string overlay,string dictionaryJson,string rulesJson) {
        CheckCompatible();
        var changes=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        changes["dist/preload.js"]=Utf.GetBytes(Text("dist/preload.js")+"\n// BEGIN LOCAL RUSSIAN TRANSLATION antigravity-ru-recovery\n"+overlay+"\n// END LOCAL RUSSIAN TRANSLATION\n");
        if(Has("dist/ipcHandlers.js")) {
            string ipc=Text("dist/ipcHandlers.js"), token="label: item.label ?? '',";
            if(Count(ipc,token)==2 && !ipc.Contains("__agRuLabel")) {
                ipc=ipc.Replace(token,"label: __agRuLabel(item.label ?? ''),");
                ipc+="\nconst __agRuLabels = "+dictionaryJson+";\nfunction __agRuLabel(s) { return Object.prototype.hasOwnProperty.call(__agRuLabels,s) ? __agRuLabels[s] : s; }\n";
                changes["dist/ipcHandlers.js"]=Utf.GetBytes(ipc);
            } else Notes.Add("Native menu adapter skipped: source changed.");
        }
        foreach(var rule in Obj(Json().DeserializeObject(rulesJson))) {
            if(!Has(rule.Key)) { Notes.Add("Missing optional native labels: "+rule.Key); continue; }
            string text=changes.ContainsKey(rule.Key) ? Utf.GetString(changes[rule.Key]) : Text(rule.Key);
            foreach(object r in (object[])rule.Value) {
                var pair=(object[])r; string before=(string)pair[0], after=(string)pair[1];
                if(Count(text,before)==1) text=text.Replace(before,after); else Notes.Add("Native display label skipped: "+rule.Key+" / "+before);
            }
            if(text!=Text(rule.Key)) changes[rule.Key]=Utf.GetBytes(text);
        }
        using(var payload=new MemoryStream()) {
            foreach(var entry in leaves.Where(p=>p.Value.ContainsKey("offset")).OrderBy(p=>Convert.ToInt64(p.Value["offset"])).ToArray()) {
                byte[] content=changes.ContainsKey(entry.Key) ? changes[entry.Key] : data[entry.Key];
                entry.Value["offset"]=payload.Position.ToString(System.Globalization.CultureInfo.InvariantCulture); entry.Value["size"]=content.Length;
                if(changes.ContainsKey(entry.Key) && entry.Value.ContainsKey("integrity")) {
                    var integrity=Obj(entry.Value["integrity"]); int block=Convert.ToInt32(integrity["blockSize"]); var blocks=new List<string>();
                    for(int i=0;i<content.Length;i+=block) { var part=new byte[Math.Min(block,content.Length-i)]; Buffer.BlockCopy(content,i,part,0,part.Length); blocks.Add(Hash(part)); }
                    integrity["hash"]=Hash(content); integrity["blocks"]=blocks.ToArray();
                }
                payload.Write(content,0,content.Length);
            }
            byte[] json=Utf.GetBytes(Json().Serialize(header)); int padding=(4-json.Length%4)%4;
            using(var f=new FileStream(destination,FileMode.CreateNew,FileAccess.Write)) using(var w=new BinaryWriter(f)) {
                w.Write(4); w.Write(8+json.Length+padding); w.Write(4+json.Length+padding); w.Write(json.Length); w.Write(json); w.Write(new byte[padding]); payload.Position=0; payload.CopyTo(f);
            }
        }
        var check=new AgRuArchive(destination);
        if(!check.leaves.Keys.OrderBy(x=>x).SequenceEqual(leaves.Keys.OrderBy(x=>x))) throw new Exception("Archive entry list changed.");
        foreach(var p in data) if(!check.data[p.Key].SequenceEqual(changes.ContainsKey(p.Key) ? changes[p.Key] : p.Value)) throw new Exception("Archive verification failed: "+p.Key);
        Notes.Add("Verified changed entries: "+string.Join(", ",changes.Keys));
    }
}
