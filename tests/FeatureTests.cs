using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.IO.Compression;
using System.Web.Script.Serialization;
using AntigravityRussian;
static class FeatureTests {
    public static void Run(string root,Action<bool,string> check,Action<Action,string> fails,string install) {
        check(InstallationLocator.DirectoryFromCommand("\"C:\\Apps with spaces\\Antigravity.exe\",0")==@"C:\Apps with spaces","Quoted registry icon path parsed");
        check(InstallationLocator.DirectoryFromCommand(@"C:\Apps with spaces\Antigravity.exe,0")==@"C:\Apps with spaces","Unquoted registry icon path parsed");
        check(InstallationLocator.DirectoryFromCommand(@"C:\Apps\Other.exe")==null,"Unrelated executable excluded from discovery");
        check(InstallationLocator.FirstSupported(new[]{Path.Combine(root,"missing"),install})==install,"Discovery falls back from stale path to supported installation");
        var diagnostic=new DiagnosticReport(); diagnostic.Record("apply",new IOException(@"secret token at C:\Users\Private\file.txt"));
        string report=diagnostic.Build(new Installation {Version="private-token",HasBackup=true});
        check(!report.Contains("Private")&&!report.Contains("token")&&!report.Contains("file.txt"),"Report excludes error messages, private paths and untrusted version strings");
        check(report.Contains("IOException")&&report.Contains("apply"),"Report preserves useful allowlisted error code");
        string draft=DiagnosticReport.TelegramDraft(report);
        check(draft.StartsWith("https://t.me/zak0407?text=")&&Uri.UnescapeDataString(draft.Substring(draft.IndexOf('=')+1))==report,"Telegram draft targets developer and encodes exact preview");
        check(UpdateService.IsNewer("1.2.0",new Version(1,1,0,0))&&!UpdateService.IsNewer("1.1.0",new Version(1,1,0,0)),"Stable release version comparison handles assembly revision");
        string version=AppInfo.VersionText, tag="v"+version, url=AppInfo.Repository+"/releases/download/"+tag+"/AntigravityRussian-"+version+"-win-x64.zip";
        var asset=new Dictionary<string,object>{{"name","AntigravityRussian-"+version+"-win-x64.zip"},{"size",123},{"digest","sha256:"+new string('a',64)},{"browser_download_url",url}};
        var metadata=new Dictionary<string,object>{{"tag_name",tag},{"draft",false},{"prerelease",false},{"html_url",AppInfo.Repository+"/releases/tag/"+tag},{"assets",new object[]{asset}}};
        var json=new JavaScriptSerializer(); var parsed=UpdateService.ParseRelease(json.Serialize(metadata));
        check(parsed.Version==version&&parsed.Sha256==new string('a',64),"Release metadata pinned to expected repository, asset and SHA-256");
        asset["browser_download_url"]="https://example.org/malware.zip";fails(()=>UpdateService.ParseRelease(json.Serialize(metadata)),"Foreign update URL rejected");asset["browser_download_url"]=url;
        metadata["prerelease"]=true;fails(()=>UpdateService.ParseRelease(json.Serialize(metadata)),"Prerelease rejected for stable updates");metadata["prerelease"]=false;
        asset["digest"]="";fails(()=>UpdateService.ParseRelease(json.Serialize(metadata)),"Missing GitHub digest rejected");
        string zip=Path.Combine(root,"release.zip"), exe=typeof(PatchService).Assembly.Location;
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)) {archive.CreateEntryFromFile(exe,"AntigravityRussian.exe");var c=archive.CreateEntry("AntigravityRussian.exe.config");using(var w=new StreamWriter(c.Open()))w.Write("<configuration><startup><supportedRuntime version=\"v4.0\"/></startup></configuration>");}
        var release=new ReleaseInfo {Version=version,Sha256=AgRuArchive.FileHash(zip)};
        string updates=Path.Combine(root,"updates"), stage=Path.Combine(updates,Guid.NewGuid().ToString("N"));
        UpdateService.ExtractVerified(zip,stage,release);check(File.Exists(Path.Combine(stage,"AntigravityRussian.exe")),"Verified release extracted and assembly version checked");
        release.Sha256=new string('0',64);fails(()=>UpdateService.ExtractVerified(zip,Path.Combine(root,"bad"),release),"Tampered ZIP rejected before extraction");
        string evil=Path.Combine(root,"traversal.zip");using(var a=ZipFile.Open(evil,ZipArchiveMode.Create)){a.CreateEntry("../outside.exe");}
        release.Sha256=AgRuArchive.FileHash(evil);fails(()=>UpdateService.ExtractVerified(evil,Path.Combine(root,"evil-stage"),release),"ZIP path traversal rejected");check(!File.Exists(Path.Combine(root,"outside.exe")),"Traversal writes no outside file");
        string portable=Path.Combine(root,"portable");Directory.CreateDirectory(portable);string target=Path.Combine(portable,"AntigravityRussian.exe");File.Copy(exe,target);File.AppendAllText(target,"old fixture overlay");File.WriteAllText(target+".config","<configuration/>");
        string oldHash=AgRuArchive.FileHash(target), oldConfig=AgRuArchive.FileHash(target+".config");
        var plan=new UpdatePlan {ParentPid=0,Target=target,PreviousSha256=oldHash,NewSha256=AgRuArchive.FileHash(Path.Combine(stage,"AntigravityRussian.exe")),ConfigSha256=AgRuArchive.FileHash(Path.Combine(stage,"AntigravityRussian.exe.config")),Version=version};
        string planPath=Path.Combine(stage,"plan.json");File.WriteAllText(planPath,json.Serialize(plan));
        using(var held=new FileStream(target+".config",FileMode.Open,FileAccess.Read,FileShare.Read))fails(()=>UpdateService.InstallPlan(planPath,updates,false),"Locked configuration aborts update");
        check(AgRuArchive.FileHash(target)==oldHash&&AgRuArchive.FileHash(target+".config")==oldConfig,"Failed self-update rolls back original executable and configuration");
        UpdateService.InstallPlan(planPath,updates,false);
        check(AgRuArchive.FileHash(target)==plan.NewSha256&&AgRuArchive.FileHash(target+".config")==plan.ConfigSha256,"Self-update core atomically installs verified executable and config");
        check(AgRuArchive.FileHash(Path.Combine(stage,"previous.exe"))==oldHash,"Previous patcher executable retained for recovery");
        fails(()=>UpdateService.InstallPlan(planPath,updates,false),"Changed target checksum prevents stale update plan reuse");
        fails(()=>UpdateService.InstallPlan(planPath,root,false),"Update plan outside approved root rejected");
    }
}
