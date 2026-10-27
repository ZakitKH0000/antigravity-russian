using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO.Compression;
using System.Web.Script.Serialization;

namespace AntigravityRussian {
    public sealed class ReleaseInfo {
        public string Tag,Version,PageUrl,ArchiveUrl,Sha256;
        public long Size;
    }
    public sealed class UpdatePlan {
        public int ParentPid;
        public string Target,PreviousSha256,NewSha256,ConfigSha256,Version;
    }
    public static class UpdateService {
        public const string Api="https://api.github.com/repos/ZakitKH0000/antigravity-russian/releases/latest";
        public static string UpdatesRoot { get { return Path.Combine(PatchService.DefaultDataRoot,"updates"); } }
        static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength=1048576 }; }
        static string Str(Dictionary<string,object> obj,string key) { object value; return obj.TryGetValue(key,out value)?Convert.ToString(value):""; }
        static bool Flag(Dictionary<string,object> obj,string key) { object value; return obj.TryGetValue(key,out value)&&value is bool&&(bool)value; }
        public static bool IsNewer(string remote,Version current) {
            Version v; return Version.TryParse(remote,out v) && new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision))>new Version(current.Major,current.Minor,Math.Max(0,current.Build),Math.Max(0,current.Revision));
        }
        public static ReleaseInfo ParseRelease(string text) {
            var obj=Json().DeserializeObject(text) as Dictionary<string,object>;
            if(obj==null||Flag(obj,"draft")||Flag(obj,"prerelease"))throw new InvalidDataException("Нужен опубликованный стабильный релиз.");
            string tag=Str(obj,"tag_name"), version=tag.StartsWith("v")?tag.Substring(1):tag;
            Version parsed; if(!Version.TryParse(version,out parsed)||parsed.Build<0||version!=parsed.ToString(3))throw new InvalidDataException("Неподдерживаемый номер релиза.");
            string page=AppInfo.Repository+"/releases/tag/"+tag, name="AntigravityRussian-"+version+"-win-x64.zip";
            if(Str(obj,"html_url")!=page)throw new InvalidDataException("Релиз принадлежит другому репозиторию.");
            object list; if(!obj.TryGetValue("assets",out list)||!(list is object[]))throw new InvalidDataException("В релизе нет файлов.");
            var assets=((object[])list).Cast<Dictionary<string,object>>().Where(a=>Str(a,"name")==name).ToArray();
            if(assets.Length!=1)throw new InvalidDataException("Не найден единственный ZIP для Windows x64.");
            var asset=assets[0]; string url=AppInfo.Repository+"/releases/download/"+tag+"/"+name, digest=Str(asset,"digest");
            long size; if(Str(asset,"browser_download_url")!=url || !long.TryParse(Str(asset,"size"),out size)||size<=0||size>33554432)throw new InvalidDataException("Неподдерживаемый адрес или размер обновления.");
            if(!System.Text.RegularExpressions.Regex.IsMatch(digest,"^sha256:[0-9a-fA-F]{64}$"))throw new InvalidDataException("GitHub не предоставил SHA-256 файла. Откройте Releases и обновитесь вручную.");
            return new ReleaseInfo {Tag=tag,Version=version,PageUrl=page,ArchiveUrl=url,Sha256=digest.Substring(7).ToLowerInvariant(),Size=size};
        }
        static void Download(string url,Stream destination,long maximum,Action<long> progress) {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url); request.UserAgent="AntigravityRussian/"+AppInfo.VersionText;
            request.Accept="application/vnd.github+json"; request.Timeout=20000; request.ReadWriteTimeout=20000;
            using(var response=(HttpWebResponse)request.GetResponse()) {
                var final=response.ResponseUri;
                if(final.Scheme!="https"||!new[]{"api.github.com","github.com","release-assets.githubusercontent.com","objects.githubusercontent.com"}.Contains(final.Host))throw new InvalidDataException("Недопустимое перенаправление загрузки.");
                if(response.ContentLength>maximum)throw new InvalidDataException("Слишком большой файл обновления.");
                using(var input=response.GetResponseStream()) {
                    byte[] buffer=new byte[65536]; int read; long total=0;
                    while((read=input.Read(buffer,0,buffer.Length))>0) { total+=read; if(total>maximum)throw new InvalidDataException("Превышен размер загрузки."); destination.Write(buffer,0,read); if(progress!=null)progress(total); }
                }
            }
        }
        public static ReleaseInfo FetchLatest() {
            using(var stream=new MemoryStream()) { Download(Api,stream,1048576,null); return ParseRelease(Encoding.UTF8.GetString(stream.ToArray())); }
        }
        public static void ExtractVerified(string zip,string destination,ReleaseInfo release) {
            if(AgRuArchive.FileHash(zip)!=release.Sha256)throw new InvalidDataException("SHA-256 обновления не совпал. Установка отменена.");
            Directory.CreateDirectory(destination);
            var allowed=new HashSet<string>(new[]{"AntigravityRussian.exe","AntigravityRussian.exe.config","README.md","LICENSE","CHANGELOG.md","QUICKSTART.md"},StringComparer.Ordinal);
            var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long size=0;
            using(var archive=ZipFile.OpenRead(zip)) {
                foreach(var entry in archive.Entries) {
                    if(!allowed.Contains(entry.FullName)||!found.Add(entry.FullName)||entry.Length>16777216)throw new InvalidDataException("Неожиданное содержимое ZIP обновления.");
                    size+=entry.Length; if(size>33554432)throw new InvalidDataException("Распакованное обновление слишком большое.");
                }
                if(!found.Contains("AntigravityRussian.exe")||!found.Contains("AntigravityRussian.exe.config"))throw new InvalidDataException("В обновлении нет приложения или конфигурации.");
                foreach(var entry in archive.Entries)entry.ExtractToFile(Path.Combine(destination,entry.FullName));
            }
            var assembly=AssemblyName.GetAssemblyName(Path.Combine(destination,"AntigravityRussian.exe"));
            if(assembly.Name!="AntigravityRussian"||assembly.Version.ToString(3)!=release.Version)throw new InvalidDataException("Версия EXE не совпала с релизом.");
        }
        public static string Stage(ReleaseInfo release,Action<string> log) {
            string folder=Path.Combine(UpdatesRoot,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string zip=Path.Combine(folder,"download.zip");
            log("Загружаем русификатор "+release.Version+" с GitHub…");
            using(var output=File.Create(zip))Download(release.ArchiveUrl,output,33554432,null);
            if(new FileInfo(zip).Length!=release.Size)throw new InvalidDataException("Размер обновления не совпал с релизом.");
            ExtractVerified(zip,folder,release);
            string source=Path.Combine(folder,"AntigravityRussian.exe"), sourceConfig=source+".config";
            File.Copy(source,Path.Combine(folder,"Updater.exe")); File.Copy(sourceConfig,Path.Combine(folder,"Updater.exe.config"));
            string target=Assembly.GetExecutingAssembly().Location;
            var plan=new UpdatePlan { ParentPid=Process.GetCurrentProcess().Id,Target=target,PreviousSha256=AgRuArchive.FileHash(target),NewSha256=AgRuArchive.FileHash(source),ConfigSha256=AgRuArchive.FileHash(sourceConfig),Version=release.Version };
            string path=Path.Combine(folder,"plan.json"); File.WriteAllText(path,Json().Serialize(plan),new UTF8Encoding(false));
            return path;
        }
        public static void StartInstaller(string plan) {
            var start=new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(plan),"Updater.exe"),"--finish-update \""+plan+"\"") { UseShellExecute=true };
            Process.Start(start);
        }
        // Testable installer core: all paths and hashes revalidated after the original process exits.
        public static void InstallPlan(string planFile,string approvedRoot,bool restart) {
            string folder=Path.GetDirectoryName(Path.GetFullPath(planFile)); Guid id;
            if(!string.Equals(Path.GetDirectoryName(folder),Path.GetFullPath(approvedRoot).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(folder),"N",out id))throw new InvalidDataException("План обновления находится вне хранилища.");
            var plan=Json().Deserialize<UpdatePlan>(File.ReadAllText(planFile,Encoding.UTF8));
            if(plan==null||!Path.IsPathRooted(plan.Target)||!plan.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Некорректная цель обновления.");
            string target=Path.GetFullPath(plan.Target), source=Path.Combine(folder,"AntigravityRussian.exe"), config=source+".config";
            if(target.StartsWith("\\\\")||target.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Нельзя обновлять сетевой или вспомогательный процесс.");
            if(AssemblyName.GetAssemblyName(source).Name!="AntigravityRussian"||AssemblyName.GetAssemblyName(source).Version.ToString(3)!=plan.Version||AgRuArchive.FileHash(source)!=plan.NewSha256||AgRuArchive.FileHash(config)!=plan.ConfigSha256)throw new InvalidDataException("Подготовленное обновление повреждено.");
            if(plan.ParentPid>0) {
                Process parent=null; try { parent=Process.GetProcessById(plan.ParentPid); } catch(ArgumentException) { }
                if(parent!=null)using(parent) {
                    try { if(!parent.HasExited && !string.Equals(parent.MainModule.FileName,target,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Родительский процесс не совпал."); }
                    catch(InvalidOperationException) { if(!parent.HasExited)throw; }
                    if(!parent.WaitForExit(30000))throw new IOException("Русификатор не закрылся. Повторите обновление.");
                }
            }
            if(AgRuArchive.FileHash(target)!=plan.PreviousSha256||AssemblyName.GetAssemblyName(target).Name!="AntigravityRussian")throw new IOException("Файл русификатора изменился. Замена отменена.");
            string suffix=".update-"+id.ToString("N"), temp=target+suffix, configTarget=target+".config", configTemp=configTarget+suffix, backup=target+suffix+".previous", configBackup=configTarget+suffix+".previous";
            bool replaced=false, configReplaced=false, hadConfig=File.Exists(configTarget);
            try {
                File.Copy(source,temp); File.Copy(config,configTemp);
                if(AgRuArchive.FileHash(temp)!=plan.NewSha256||AgRuArchive.FileHash(configTemp)!=plan.ConfigSha256)throw new IOException("Проверка копирования обновления не прошла.");
                File.Replace(temp,target,backup); replaced=true;
                if(hadConfig)File.Replace(configTemp,configTarget,configBackup); else File.Move(configTemp,configTarget); configReplaced=true;
                if(AgRuArchive.FileHash(target)!=plan.NewSha256||AgRuArchive.FileHash(configTarget)!=plan.ConfigSha256)throw new IOException("Проверка установленного русификатора не прошла.");
                if(restart)Process.Start(new ProcessStartInfo(target) {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(target)});
            } catch {
                if(configReplaced) { if(hadConfig)File.Replace(configBackup,configTarget,null); else File.Delete(configTarget); }
                if(replaced && File.Exists(backup))File.Replace(backup,target,null);
                throw;
            } finally { if(File.Exists(temp))File.Delete(temp); if(File.Exists(configTemp))File.Delete(configTemp); }
            // Preserve the previous executable in the user update cache; no Antigravity data is touched.
            try { if(File.Exists(backup)) { File.Copy(backup,Path.Combine(folder,"previous.exe"),true); File.Delete(backup); } if(File.Exists(configBackup)) { File.Copy(configBackup,Path.Combine(folder,"previous.exe.config"),true); File.Delete(configBackup); } } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
        public static void Complete(string planFile) {
            string own=Assembly.GetExecutingAssembly().Location;
            if(!string.Equals(Path.GetDirectoryName(own),Path.GetDirectoryName(Path.GetFullPath(planFile)),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Неверный вспомогательный процесс.");
            var plan=Json().Deserialize<UpdatePlan>(File.ReadAllText(planFile,Encoding.UTF8));
            if(AgRuArchive.FileHash(own)!=plan.NewSha256)throw new InvalidDataException("Вспомогательный процесс повреждён.");
            InstallPlan(planFile,UpdatesRoot,true);
        }
    }
}
