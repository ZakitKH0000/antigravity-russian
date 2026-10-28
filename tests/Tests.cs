using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using AntigravityRussian;

static class Tests {
    static int count;
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); count++; Console.WriteLine("PASS "+message); }
    static void Fails(Action action,string message) { bool failed=false; try { action(); } catch { failed=true; } Assert(failed,message); }
    static readonly Action<string> Log=s=>{};
    static byte[] Bytes(string text) { return Encoding.UTF8.GetBytes(text); }
    static Dictionary<string,object> Dict(params object[] pairs) { var d=new Dictionary<string,object>(); for(int i=0;i<pairs.Length;i+=2) d.Add((string)pairs[i],pairs[i+1]); return d; }
    // Independent tiny ASAR fixture writer. No real Antigravity code or binaries are distributed.
    static void Fixture(string path,string version,bool changedPreload,bool nativeChanged) {
        var sources=new Dictionary<string,byte[]> {
            {"package.json",Bytes("{\"name\":\"antigravity\",\"productName\":\"Antigravity\",\"main\":\"dist/main.js\",\"version\":\""+version+"\"}")},
            {"untouched.txt",Bytes("fixture content remains identical: "+version)},
            {"dist/preload.js",Bytes("const e=require(\"electron\"); const "+(changedPreload?"changedBridge":"contextBridge")+"=e.contextBridge; const ipcRenderer=e.ipcRenderer; // fixture only\n")},
            {"dist/main.js",Bytes("const path=require('path'); const preload=path.join(__dirname,'preload.js'); const menu={label: 'New Window'}; // "+version+"\n")},
            {"dist/ipcHandlers.js",Bytes("function menu(item){return [{label: item.label ?? '',},{label: item.label ?? '',}];}")}
        };
        if(changedPreload) sources["dist/preload.js"]=Bytes(Encoding.UTF8.GetString(sources["dist/preload.js"]).Replace("contextBridge","changedBridge"));
        if(nativeChanged) sources["dist/ipcHandlers.js"]=Bytes("function menu(item){return [{label:(item.label ?? '')}];}");
        var files=new Dictionary<string,object>(); var dist=new Dictionary<string,object>(); files["dist"]=Dict("files",dist);
        files["unpacked.node"]=Dict("size",12,"unpacked",true); files["alias.txt"]=Dict("link","untouched.txt");
        using(var payload=new MemoryStream()) {
            foreach(var entry in sources) {
                var integrity=Dict("algorithm","SHA256","hash",AgRuArchive.Hash(entry.Value),"blockSize",4194304,"blocks",new[]{AgRuArchive.Hash(entry.Value)});
                var item=Dict("offset",payload.Position.ToString(),"size",entry.Value.Length,"integrity",integrity);
                if(entry.Key.StartsWith("dist/")) dist[entry.Key.Substring(5)]=item; else files[entry.Key]=item;
                payload.Write(entry.Value,0,entry.Value.Length);
            }
            var json=Bytes(new JavaScriptSerializer().Serialize(Dict("files",files))); int padding=(4-json.Length%4)%4;
            using(var stream=File.Create(path)) using(var writer=new BinaryWriter(stream)) {
                writer.Write(4); writer.Write(json.Length+padding+8); writer.Write(json.Length+padding+4); writer.Write(json.Length); writer.Write(json); writer.Write(new byte[padding]); writer.Write(payload.ToArray());
            }
        }
    }
    static void Fuse(string path,bool locked) { File.WriteAllBytes(path,Bytes("fixture-dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX"+"\x01\x09"+(locked?"101110011":"101100011")+new string('\0',32))); }
    static string Hash(string path) { return AgRuArchive.FileHash(path); }
    // A downloaded Antigravity update installs when the app quits and puts the English archive back.
    static void Updates(PatchService service,string install,string root,string target,string original) {
        string local=Path.Combine(root,"local"), pending=Path.Combine(local,"antigravity-updater","pending"), installer=Path.Combine(pending,"Antigravity-Setup.exe");
        Directory.CreateDirectory(pending); File.Copy(Path.Combine(Environment.SystemDirectory,"PING.EXE"),installer);
        File.SetLastWriteTimeUtc(installer,DateTime.UtcNow.AddMinutes(5));
        Assert(service.Inspect(install).UpdatePending,"Downloaded Antigravity update is detected");
        File.SetLastWriteTimeUtc(installer,DateTime.UtcNow.AddDays(-30));
        Assert(!service.Inspect(install).UpdatePending,"Installer older than the installation is not reported as pending");
        Assert(service.UpdateProcesses(install)==0,"No update installer reported while none runs");
        var running=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installer,"-n 30 127.0.0.1") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true });
        try {
            Assert(service.UpdateProcesses(install)==1,"Running update installer is detected");
            Fails(()=>service.Apply(install,Log),"Translation waits while an Antigravity update is being installed"); Assert(Hash(target)==original,"Installation is not modified during an update");
        } finally { if(!running.HasExited) running.Kill(); running.WaitForExit(); running.Dispose(); }
        string config=Path.Combine(install,"resources","app-update.yml"); File.WriteAllText(config,"provider: generic\nupdaterCacheDirName: 'other-updater'\n");
        Assert(AntigravityUpdater.Cache(install,local)==Path.Combine(local,"other-updater"),"Updater cache name is read from app-update.yml");
        File.Delete(config); File.Delete(installer);
    }
    static int Main() {
        string root=Path.Combine(Path.GetTempPath(),"AntigravityRussian-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            string install=Path.Combine(root,"app"), resources=Path.Combine(install,"resources"); Directory.CreateDirectory(resources);
            string target=Path.Combine(resources,"app.asar"), exe=Path.Combine(install,"Antigravity.exe"), state=Path.Combine(root,"state");
            Fixture(target,"1.0-test",false,false); Fuse(exe,false); string original=Hash(target), exeHash=Hash(exe);
            var service=new PatchService(state) { LocalData=Path.Combine(root,"local") }; var initial=service.Inspect(install);
            Assert(!initial.HasBackup&&!initial.IsRussian&&!Directory.Exists(state),"Inspect is read-only");
            var backed=service.Backup(install,Log); Assert(backed.HasBackup&&Hash(target)==original,"Backup does not modify installation");
            string backup=Path.Combine(backed.BackupDirectory,"original.asar"); Assert(Hash(backup)==original,"Original backup is byte exact");
            var applied=service.Apply(install,Log); string patched=Hash(target);
            Assert(applied.IsRussian&&patched!=original,"Apply installs translation");
            Assert(Hash(exe)==exeHash,"Executable and its fuses are unchanged");
            Assert(new AgRuArchive(target).Text("untouched.txt")=="fixture content remains identical: 1.0-test","Unmodified entry preserved");
            Assert(new AgRuArchive(target).Text("dist/main.js").Contains("Новое окно"),"Native display strings are valid Russian Unicode");
            service.Apply(install,Log); Assert(Hash(target)==patched,"Repeated apply is idempotent");
            service.Restore(install,Log); Assert(Hash(target)==original,"Restore returns exact original");
            service.Apply(install,Log); Assert(Hash(target)==patched,"Version-specific cached reapply works");
            File.AppendAllText(Path.Combine(applied.BackupDirectory,"russian.asar"),"damaged derived cache");
            service.Restore(install,Log); Assert(Hash(target)==original,"Damaged derived cache never blocks restore from valid original");
            service.Apply(install,Log); Assert(service.Inspect(install).IsRussian,"Damaged derived cache is rebuilt from verified original");
            service.Restore(install,Log);
            // A cache prepared by an earlier release must not reinstall its older translation.
            string cache=Path.Combine(applied.BackupDirectory,"russian.asar"), manifest=Path.Combine(applied.BackupDirectory,"manifest.json");
            File.Delete(cache); new AgRuArchive(backup).Build(cache,"// previous release overlay","{}","{}");
            var json=new JavaScriptSerializer(); var record=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest));
            record["PatchedSha256"]=Hash(cache); record["PayloadSha256"]=null; File.WriteAllText(manifest,json.Serialize(record));
            service.Apply(install,Log); Assert(Hash(target)==patched,"Cache from an earlier release is rebuilt with the current translation");
            Assert(!service.Inspect(install).TranslationOutdated&&PatchService.RunningProcesses(install)==0,"Current translation is not reported as outdated");
            record=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest)); record["PayloadSha256"]="previous-release"; File.WriteAllText(manifest,json.Serialize(record));
            Assert(service.Inspect(install).TranslationOutdated,"Installed translation of an earlier release is reported as outdated");
            var refreshed=service.UpdateTranslation(install,Log);
            Assert(refreshed.IsRussian&&!refreshed.TranslationOutdated&&Hash(target)==patched,"Update translation restores the original and applies the current dictionary");
            service.Restore(install,Log);
            // Same version number, different build: separate immutable backup.
            Fixture(target,"1.0-test",false,true); string other=Hash(target);
            var updatedBuild=service.Inspect(install); Assert(updatedBuild.EarlierBuild&&!updatedBuild.HasBackup,"New Antigravity build after a translated one is reported as updated");
            var second=service.Apply(install,Log);
            Assert(second.IsRussian&&second.BackupDirectory!=backed.BackupDirectory,"Different builds with same version have separate backups");
            Assert(second.Record.Notes.Any(n=>n.Contains("skipped")),"Changed optional native menu adapter is skipped and reported");
            service.Restore(install,Log); Assert(Hash(target)==other,"Second-build rollback does not downgrade to first build");
            Fixture(target,"2.0-test",false,false); string updated=Hash(target);
            service.Restore(install,Log); Assert(Hash(target)==updated,"Unrecorded update is never overwritten on restore");
            var third=service.Apply(install,Log); Assert(third.Version=="2.0-test"&&third.IsRussian,"Updated version is patched from its own source");
            service.Restore(install,Log); Assert(Hash(target)==updated,"Updated-version original restored exactly");
            // Electron prefers resources/app over app.asar: a translated archive would never be shown.
            string unpacked=Path.Combine(resources,"app"); Directory.CreateDirectory(unpacked); File.WriteAllText(Path.Combine(unpacked,"package.json"),"{}");
            Assert(service.Inspect(install).AppFolder,"Unpacked resources/app folder is detected");
            Fails(()=>service.Apply(install,Log),"Translation is refused while resources/app shadows the archive"); Assert(Hash(target)==updated,"Shadowed installation is not modified");
            Directory.Delete(unpacked,true);
            Updates(service,install,root,target,updated);
            File.AppendAllText(Path.Combine(third.BackupDirectory,"original.asar"),"corruption");
            Fails(()=>service.Apply(install,Log),"Corrupt backup is rejected"); Assert(Hash(target)==updated,"Corrupt backup never modifies installation");
            Fixture(target,"3.0-test",true,false); string unsupported=Hash(target);
            Fails(()=>service.Apply(install,Log),"Unknown preload architecture is rejected"); Assert(Hash(target)==unsupported,"Unsupported structure is not modified");
            Fixture(target,"4.0-test",false,false); string lockedHash=Hash(target); Fuse(exe,true);
            Fails(()=>service.Apply(install,Log),"Enabled Electron archive validation is respected"); Assert(Hash(target)==lockedHash,"Archive and executable remain untouched when validation is enabled");
            // Unknown pre-existing translation must never become the supposed original.
            Fuse(exe,false); var a=new AgRuArchive(target); string preexisting=Path.Combine(root,"preexisting.asar");
            a.Build(preexisting,"// fixture overlay", "{}", "{}"); File.Copy(preexisting,target,true); string unknown=Hash(target);
            Assert(service.Inspect(install).UnknownTranslation,"Unrecorded translation detected");
            Fails(()=>service.Backup(install,Log),"Unknown translation cannot be backed up as pristine original"); Assert(Hash(target)==unknown,"Unknown translation is not overwritten");
            // Independent instances must serialize mutation operations.
            using(var mutex=new System.Threading.Mutex(false,"Local\\AntigravityRuRecovery")) {
                bool holds=false;
                var thread=new System.Threading.Thread(()=>{ mutex.WaitOne(); holds=true; System.Threading.Thread.Sleep(1500); mutex.ReleaseMutex(); }); thread.Start();
                while(!holds) System.Threading.Thread.Sleep(10);
                Fails(()=>service.Restore(install,Log),"Concurrent patch operation is refused"); thread.Join();
            }
            FeatureTests.Run(root,Assert,Fails,install);
            Console.WriteLine("ALL "+count+" ASSERTIONS PASSED"); return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); Console.Error.WriteLine("Fixture directory retained: "+root); return 1; }
    }
}
