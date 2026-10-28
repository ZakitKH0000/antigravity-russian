using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AntigravityRussian {
    // Antigravity updates itself with electron-updater: the installer downloaded into its cache runs
    // when Antigravity quits and puts the English app.asar of the new version in place.
    public static class AntigravityUpdater {
        public static string Cache(string directory,string localData) {
            string name="antigravity-updater";
            try {
                foreach(string line in File.ReadAllLines(Path.Combine(directory,"resources","app-update.yml"))) {
                    var m=Regex.Match(line,@"^\s*updaterCacheDirName:\s*['""]?([\w.-]+)");
                    if(m.Success) name=m.Groups[1].Value;
                }
            } catch(IOException) { } catch(UnauthorizedAccessException) { }
            return Path.Combine(localData,name);
        }
        // A downloaded update waits here until Antigravity is closed. The cache may outlive the installation, so
        // only an installer downloaded after the last install counts. The installer keeps build timestamps of files,
        // so the install time is when Antigravity.exe and the folder entries were created; the localizer touches neither.
        public static bool Pending(string cache,string exe) { return PendingInstaller(cache,exe)!=null; }
        public static string PendingInstaller(string cache,string exe) {
            string pending=Path.Combine(cache,"pending");
            try {
                DateTime created=File.GetCreationTimeUtc(exe), folder=Directory.GetLastWriteTimeUtc(Path.GetDirectoryName(exe)), installed=created>folder?created:folder;
                if(!Directory.Exists(pending)) return null;
                return Directory.EnumerateFiles(pending,"*.exe").Where(f=>File.GetLastWriteTimeUtc(f)>installed).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            } catch(IOException) { return null; } catch(UnauthorizedAccessException) { return null; }
        }

        // Feed of the official installer: the same one Antigravity's own updater reads (app-update.yml url + Windows channel).
        public sealed class Release { public string Version, Url, Sha512; public long Size; }
        public static Release Latest(string directory) {
            string feed=null, config=Path.Combine(directory,"resources","app-update.yml");
            if(File.Exists(config)) foreach(string line in File.ReadAllLines(config)) {
                var m=Regex.Match(line,@"^\s*url:\s*['""]?(https://[^'""\s]+)"); if(m.Success) feed=m.Groups[1].Value;
            }
            Uri uri;
            if(feed==null || !Uri.TryCreate(feed.TrimEnd('/')+"/latest-x64-win.yml",UriKind.Absolute,out uri) || uri.Scheme!="https" || !uri.Host.EndsWith(".run.app",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Не найден адрес официальных обновлений Antigravity.");
            string text;
            using(var buffer=new MemoryStream()) { Download(uri.AbsoluteUri,buffer,1048576,null); text=System.Text.Encoding.UTF8.GetString(buffer.ToArray()); }
            var release=new Release();
            foreach(string line in text.Split('\n')) {
                var m=Regex.Match(line,@"^\s*(?:-\s*)?(version|url|sha512|size):\s*['""]?([^'""\s]+)");
                if(!m.Success) continue;
                string key=m.Groups[1].Value, value=m.Groups[2].Value;
                if(key=="version" && release.Version==null) release.Version=value;
                else if(key=="url" && release.Url==null) release.Url=value;
                else if(key=="sha512" && release.Sha512==null) release.Sha512=value;
                else if(key=="size" && release.Size==0) long.TryParse(value,out release.Size);
            }
            Uri file;
            if(release.Version==null || release.Sha512==null || release.Size<=0 || release.Url==null || !Uri.TryCreate(release.Url,UriKind.Absolute,out file) || !OfficialHost(file))
                throw new InvalidDataException("Сервер обновлений Antigravity вернул неожиданный ответ.");
            return release;
        }
        static bool OfficialHost(Uri uri) {
            return uri.Scheme=="https" && ((uri.Host=="storage.googleapis.com" && uri.AbsolutePath.StartsWith("/antigravity-public/",StringComparison.Ordinal)) || uri.Host=="dl.google.com");
        }
        static void Download(string url,Stream destination,long maximum,Action<long> progress) {
            System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
            var request=(System.Net.HttpWebRequest)System.Net.WebRequest.Create(url); request.UserAgent="AntigravityRussian/"+AppInfo.VersionText;
            request.Timeout=30000; request.ReadWriteTimeout=30000;
            using(var response=(System.Net.HttpWebResponse)request.GetResponse()) {
                var final=response.ResponseUri;
                if(final.Scheme!="https" || (!OfficialHost(final) && !final.Host.EndsWith(".run.app",StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Недопустимое перенаправление загрузки.");
                if(response.ContentLength>maximum) throw new InvalidDataException("Слишком большой файл.");
                using(var input=response.GetResponseStream()) {
                    byte[] buffer=new byte[65536]; int read; long total=0;
                    while((read=input.Read(buffer,0,buffer.Length))>0) { total+=read; if(total>maximum) throw new InvalidDataException("Превышен размер загрузки."); destination.Write(buffer,0,read); if(progress!=null) progress(total); }
                }
            }
        }
        public static string Sha512(string path) { using(var f=File.OpenRead(path)) using(var s=System.Security.Cryptography.SHA512.Create()) return Convert.ToBase64String(s.ComputeHash(f)); }
        // Downloads into the folder, reusing a complete earlier download; size and SHA-512 come from the official feed.
        public static string Fetch(Release release,string folder,Action<string> log) {
            Directory.CreateDirectory(folder);
            string target=Path.Combine(folder,"Antigravity-x64-"+Regex.Replace(release.Version,@"[^\w.-]","")+".exe"), part=target+".part";
            if(File.Exists(target) && new FileInfo(target).Length==release.Size && Sha512(target)==release.Sha512) return target;
            if(File.Exists(target)) File.Delete(target);
            log("Скачиваем официальный установщик Antigravity "+release.Version+" ("+(release.Size/1048576)+" МБ)…");
            int reported=0;
            try {
                using(var output=new FileStream(part,FileMode.Create,FileAccess.Write)) Download(release.Url,output,release.Size,total=> { int step=(int)(total*10/release.Size); if(step>reported) { reported=step; log("Загружено "+step*10+"%"); } });
                if(new FileInfo(part).Length!=release.Size || Sha512(part)!=release.Sha512) throw new InvalidDataException("Контрольная сумма установщика Antigravity не совпала.");
                File.Move(part,target);
            } finally { if(File.Exists(part)) File.Delete(part); }
            return target;
        }
        // Runs a verified Google installer silently and waits for it; "--updated" is what Antigravity passes for its own updates.
        public static void Install(string installer,string cache,Action<string> log) {
            if(!Signature.SignedByGoogle(installer)) throw new InvalidOperationException("Установщик Antigravity не подписан Google — запуск отменён.");
            log("Устанавливаем Antigravity — это займёт до пары минут…");
            using(var process=Process.Start(new ProcessStartInfo(installer,"--updated /S") { UseShellExecute=false,CreateNoWindow=true })) {
                if(!process.WaitForExit(600000)) throw new TimeoutException("Установка Antigravity идёт дольше 10 минут. Дождитесь её окончания и повторите.");
                if(process.ExitCode!=0) throw new InvalidOperationException("Установщик Antigravity завершился с кодом "+process.ExitCode+".");
            }
            var until=DateTime.UtcNow.AddMinutes(5);
            while(Running(cache)>0 && DateTime.UtcNow<until) System.Threading.Thread.Sleep(1000);
            log("Antigravity установлен. Если вы пользуетесь анлокером confeden/Antigravity, запустите его снова — он подготовит обновлённые файлы.");
        }
        // Installer processes started from the cache; unreadable processes belong to other users or services.
        public static int Running(string cache) {
            string prefix=Path.GetFullPath(cache).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar; int count=0;
            foreach(var process in Process.GetProcesses()) using(process) {
                try { if(process.MainModule.FileName.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) count++; }
                catch(System.ComponentModel.Win32Exception) { } catch(InvalidOperationException) { } catch(NotSupportedException) { }
            }
            return count;
        }
    }
}
