using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

namespace AntigravityRussian {
    public sealed class BackupRecord {
        public int Schema = 1;
        public string Version;
        public string OriginalSha256;
        public string PatchedSha256;
        public string PayloadSha256;
        public string CreatedUtc;
        public string[] Notes = new string[0];
    }
    public sealed class Installation {
        public string Directory, ArchivePath, ExePath, Version, Hash, BackupDirectory;
        public bool IsRussian, HasBackup, UnknownTranslation;
        // Installed translation was built by another release; EarlierBuild: another Antigravity build was translated here.
        public bool TranslationOutdated, EarlierBuild;
        // AppFolder: Electron loads resources/app before app.asar, so a translated archive is never shown.
        // UpdatePending: Antigravity downloaded an update that replaces app.asar with English when it quits.
        public bool AppFolder, UpdatePending;
        public string UpdaterCache;
        public int Records;
        public BackupRecord Record;
        public string Status { get { return UnknownTranslation ? "Обнаружена другая русификация" : AppFolder ? "Перевод не виден: мешает папка resources\\app" : TranslationOutdated ? "Доступен новый перевод" : IsRussian ? "Русский интерфейс включён" : EarlierBuild ? "Antigravity обновился — включите русский снова" : "Английский интерфейс — можно русифицировать"; } }
    }
    public sealed class PatchService {
        public static readonly string DefaultDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityRussian");
        public readonly string DataRoot;
        // Where Antigravity keeps downloaded updates; tests point it at a temporary folder.
        public string LocalData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public const string AppFolderHint="Antigravity загружает интерфейс из папки resources\\app, а не из app.asar, поэтому перевод не будет виден. Такую папку создают распаковщики, например анлокеры для старых версий Antigravity. Русификатор её не меняет.";
        static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength=64000000 }; }
        public PatchService(string dataRoot) { DataRoot=Path.GetFullPath(dataRoot); }
        public PatchService() : this(DefaultDataRoot) { }
        public static string InstallKey(string directory) { return AgRuArchive.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())).Substring(0,24); }
        string Records(string directory) { return Path.Combine(DataRoot,"backups",InstallKey(directory)); }
        static string Payload(string name) {
            using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload."+name)) {
                if(s==null) throw new InvalidOperationException("Отсутствует встроенный словарь: "+name);
                using(var r=new StreamReader(s,Encoding.UTF8,true)) return r.ReadToEnd();
            }
        }
        // Identifies the embedded translation, so a cache built by another release is never reused.
        static string payloadHash;
        static string PayloadHash() { return payloadHash ?? (payloadHash=AgRuArchive.Hash(Encoding.UTF8.GetBytes(Payload("localize-ui.js")+"\0"+Payload("dictionary.json")+"\0"+Payload("native-rules.json")))); }
        public static string FindInstallation() {
            return InstallationLocator.Find();
        }
        public void SaveSelection(string directory) {
            System.IO.Directory.CreateDirectory(DataRoot);
            File.WriteAllText(Path.Combine(DataRoot,"installation.txt"),Path.GetFullPath(directory),new UTF8Encoding(false));
        }
        public Installation Inspect(string directory) {
            if(string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Выберите файл Antigravity.exe установленного приложения.");
            directory=Path.GetFullPath(directory);
            string archive=Path.Combine(directory,"resources","app.asar"), exe=Path.Combine(directory,"Antigravity.exe");
            if(!File.Exists(archive) || !File.Exists(exe)) throw new InvalidOperationException("В выбранной папке нет Antigravity.exe и resources/app.asar.");
            var a=new AgRuArchive(archive);
            if(a.Name!="antigravity" || a.Product!="Antigravity") throw new InvalidOperationException("Эта установка не поддерживается. Нужен Antigravity App с Electron-архивом app.asar.");
            var result=new Installation { Directory=directory, ArchivePath=archive, ExePath=exe, Version=a.Version, Hash=AgRuArchive.FileHash(archive),
                AppFolder=File.Exists(Path.Combine(directory,"resources","app","package.json")), UpdaterCache=AntigravityUpdater.Cache(directory,LocalData) };
            result.UpdatePending=AntigravityUpdater.Pending(result.UpdaterCache,exe);
            string records=Records(directory);
            bool otherRecords=false;
            if(System.IO.Directory.Exists(records)) foreach(string recordRoot in System.IO.Directory.GetDirectories(records)) {
                string folder=Path.GetFileName(recordRoot), manifest=Path.Combine(recordRoot,"manifest.json");
                if(folder.Length!=64 || folder.Any(c=>!"0123456789abcdef".Contains(c)) || !File.Exists(manifest)) continue;
                var record=Json().Deserialize<BackupRecord>(File.ReadAllText(manifest,Encoding.UTF8));
                if(record==null || record.Schema!=1 || record.OriginalSha256!=folder) throw new InvalidOperationException("Повреждена запись резервной копии: "+recordRoot);
                result.Records++;
                if(result.Hash!=record.OriginalSha256 && result.Hash!=record.PatchedSha256) { otherRecords=true; continue; }
                ValidateRecord(recordRoot,record);
                if(record.Version!=a.Version) throw new InvalidOperationException("Версия резервной копии не совпала с приложением.");
                result.Record=record; result.BackupDirectory=recordRoot; result.HasBackup=true;
                result.IsRussian=result.Hash==record.PatchedSha256;
                break;
            }
            result.UnknownTranslation=a.HasTranslation && !result.IsRussian;
            result.TranslationOutdated=result.IsRussian && result.Record.PayloadSha256!=PayloadHash();
            result.EarlierBuild=!result.HasBackup && !result.UnknownTranslation && otherRecords;
            return result;
        }
        static void ValidateRecord(string path,BackupRecord record,bool requirePatch=false) {
            if(AgRuArchive.FileHash(Path.Combine(path,"original.asar"))!=record.OriginalSha256) throw new InvalidOperationException("Резервная копия повреждена. Файлы приложения не менялись.");
            if(requirePatch && record.PatchedSha256!=null && AgRuArchive.FileHash(Path.Combine(path,"russian.asar"))!=record.PatchedSha256) throw new InvalidOperationException("Подготовленный русский архив повреждён. Файлы приложения не менялись.");
        }
        static void WriteManifest(string directory,BackupRecord record) {
            string path=Path.Combine(directory,"manifest.json"), temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllText(temp,Json().Serialize(record),new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            } finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        // Processes of this installation that keep app.asar in use; unreadable ones count as running.
        public static int RunningProcesses(string directory) {
            string exe=Path.Combine(Path.GetFullPath(directory),"Antigravity.exe"); int count=0;
            foreach(var process in Process.GetProcessesByName("Antigravity")) using(process) {
                try { if(string.Equals(process.MainModule.FileName,exe,StringComparison.OrdinalIgnoreCase)) count++; }
                catch(System.ComponentModel.Win32Exception) { count++; }
                catch(InvalidOperationException) { if(!process.HasExited) count++; }
            }
            return count;
        }
        // Installer of an Antigravity update that is replacing the files right now.
        public int UpdateProcesses(string directory) { return AntigravityUpdater.Running(AntigravityUpdater.Cache(Path.GetFullPath(directory),LocalData)); }
        static void EnsureClosed(Installation info) {
            if(AntigravityUpdater.Running(info.UpdaterCache)>0) throw new InvalidOperationException("Сейчас устанавливается обновление Antigravity. Дождитесь окончания установки и повторите действие.");
            foreach(var process in Process.GetProcessesByName("Antigravity")) using(process) {
                try {
                    if(string.Equals(process.MainModule.FileName,info.ExePath,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Закройте Antigravity, включая значок в трее, и повторите действие. Принудительное завершение не выполняется.");
                } catch(System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Не удалось проверить процесс Antigravity. Полностью закройте приложение."); }
                catch(InvalidOperationException) { if(!process.HasExited) throw; }
            }
        }
        T Locked<T>(Func<T> action) {
            using(var mutex=new Mutex(false,"Local\\AntigravityRuRecovery")) {
                bool acquired=false;
                try { try { acquired=mutex.WaitOne(0); } catch(AbandonedMutexException) { acquired=true; }
                    if(!acquired) throw new InvalidOperationException("Другая операция русификации уже выполняется.");
                    return action();
                } finally { if(acquired) mutex.ReleaseMutex(); }
            }
        }
        public Installation Backup(string directory,Action<string> log) { return Locked(()=>BackupCore(directory,log)); }
        Installation BackupCore(string directory,Action<string> log) {
            var info=Inspect(directory);
            if(info.HasBackup) { log("Резервная копия проверена: "+info.BackupDirectory); return info; }
            var archive=new AgRuArchive(info.ArchivePath); archive.CheckCompatible();
            // Copying a running app is read-only; recheck the hash to detect concurrent updates.
            string records=Records(directory); System.IO.Directory.CreateDirectory(records);
            string destination=Path.Combine(records,info.Hash), temp=Path.Combine(records,"pending-"+Guid.NewGuid().ToString("N"));
            if(System.IO.Directory.Exists(destination)) throw new InvalidOperationException("Найдена неполная копия этого архива. Проверьте папку резервных копий.");
            System.IO.Directory.CreateDirectory(temp);
            try {
                string original=Path.Combine(temp,"original.asar"); File.Copy(info.ArchivePath,original);
                if(AgRuArchive.FileHash(original)!=info.Hash || AgRuArchive.FileHash(info.ArchivePath)!=info.Hash) throw new InvalidOperationException("Приложение обновилось во время копирования. Повторите проверку.");
                WriteManifest(temp,new BackupRecord { Version=info.Version,OriginalSha256=info.Hash,CreatedUtc=DateTime.UtcNow.ToString("o") });
                System.IO.Directory.Move(temp,destination);
            } finally {
                // Only remove our own incomplete files; never recurse into a backup tree.
                if(System.IO.Directory.Exists(temp)) { foreach(string f in new[]{"original.asar","manifest.json"}) if(File.Exists(Path.Combine(temp,f))) File.Delete(Path.Combine(temp,f)); if(!System.IO.Directory.EnumerateFileSystemEntries(temp).Any()) System.IO.Directory.Delete(temp); }
            }
            log("Создана и проверена резервная копия версии "+info.Version+".");
            return Inspect(directory);
        }
        public Installation Apply(string directory,Action<string> log) { return Locked(()=>ApplyCore(directory,log)); }
        // Replaces a translation built by an earlier release: verified original first, then the current dictionary.
        public Installation UpdateTranslation(string directory,Action<string> log) { return Locked(()=> {
            var info=Inspect(directory);
            if(!info.TranslationOutdated) return ApplyCore(directory,log);
            RestoreCore(directory,log);
            return ApplyCore(directory,log);
        }); }
        Installation ApplyCore(string directory,Action<string> log) {
            var info=Inspect(directory);
            if(info.IsRussian) { log("Русский интерфейс уже установлен."); return info; }
            if(info.AppFolder) throw new InvalidOperationException(AppFolderHint+" Файлы не изменены.");
            EnsureClosed(info); AgRuArchive.CheckExecutable(info.ExePath);
            string exeHash=AgRuArchive.FileHash(info.ExePath);
            if(!info.HasBackup) info=BackupCore(directory,log);
            if(info.UnknownTranslation) throw new InvalidOperationException("Сначала восстановите оригинал неизвестного русификатора.");
            string patched=Path.Combine(info.BackupDirectory,"russian.asar");
            string payload=PayloadHash();
            if(info.Record.PatchedSha256==null || info.Record.PayloadSha256!=payload || !File.Exists(patched) || AgRuArchive.FileHash(patched)!=info.Record.PatchedSha256) {
                string temp=Path.Combine(info.BackupDirectory,"build-"+Guid.NewGuid().ToString("N")+".asar");
                try {
                    var original=new AgRuArchive(Path.Combine(info.BackupDirectory,"original.asar"));
                    original.Build(temp,Payload("localize-ui.js"),Payload("dictionary.json"),Payload("native-rules.json"));
                    // The regional unlocker unpacks app.asar unless dist/main.js keeps the v2.4+ shell markers.
                    if(RegionalUnlocker.KeepsDesktopShape(original.Text("dist/main.js")) && !RegionalUnlocker.KeepsDesktopShape(new AgRuArchive(temp).Text("dist/main.js")))
                        throw new InvalidOperationException("Перевод изменил бы признаки, по которым анлокер confeden распознаёт Antigravity. Русификация отменена, файлы не изменены.");
                    if(File.Exists(patched)) File.Delete(patched); // Derived cache only; original is immutable.
                    File.Move(temp,patched);
                    info.Record.PatchedSha256=AgRuArchive.FileHash(patched); info.Record.PayloadSha256=payload; info.Record.Notes=original.Notes.ToArray();
                    WriteManifest(info.BackupDirectory,info.Record);
                } finally { if(File.Exists(temp)) File.Delete(temp); }
            }
            Replace(info,patched,info.Record.PatchedSha256,exeHash);
            log("Русификация установлена. Можно запускать Antigravity.");
            foreach(string note in info.Record.Notes.Where(n=>n.Contains("skipped") || n.Contains("Missing"))) log("Часть системных подписей новой версии пропущена: "+note);
            return Inspect(directory);
        }
        public Installation Restore(string directory,Action<string> log) { return Locked(()=>RestoreCore(directory,log)); }
        Installation RestoreCore(string directory,Action<string> log) {
            var info=Inspect(directory);
            if(!info.IsRussian) { log("Архив этой установки не изменён данным русификатором. Замена не требуется."); return info; }
            EnsureClosed(info);
            Replace(info,Path.Combine(info.BackupDirectory,"original.asar"),info.Record.OriginalSha256,AgRuArchive.FileHash(info.ExePath));
            log("Оригинал версии "+info.Version+" восстановлен без понижения версии.");
            return Inspect(directory);
        }
        static void Replace(Installation info,string source,string expected,string exeHash) {
            string temp=info.ArchivePath+".ru-"+Guid.NewGuid().ToString("N")+".tmp", rollback=temp+".rollback";
            try {
                File.Copy(source,temp); if(AgRuArchive.FileHash(temp)!=expected) throw new InvalidOperationException("Контрольная сумма подготовленного архива не совпала.");
                EnsureClosed(info);
                if(AgRuArchive.FileHash(info.ArchivePath)!=info.Hash || AgRuArchive.FileHash(info.ExePath)!=exeHash) throw new InvalidOperationException("Antigravity обновился во время подготовки. Замена отменена.");
                File.Replace(temp,info.ArchivePath,rollback);
                if(AgRuArchive.FileHash(info.ArchivePath)!=expected) {
                    File.Replace(rollback,info.ArchivePath,null);
                    throw new InvalidOperationException("Проверка результата не прошла; предыдущий файл восстановлен.");
                }
            } finally {
                if(File.Exists(temp)) File.Delete(temp);
                if(File.Exists(rollback) && new[]{expected,info.Hash}.Contains(AgRuArchive.FileHash(info.ArchivePath))) File.Delete(rollback);
            }
        }
        public void Launch(string directory) { var info=Inspect(directory); Process.Start(new ProcessStartInfo(info.ExePath) { WorkingDirectory=info.Directory,UseShellExecute=true }); }
        // Explicit import for people migrating from the earlier script bundle. Does not install a patch.
        public Installation ImportBackup(string directory,string originalPath,Action<string> log) { return Locked(()=> {
            var installed=Inspect(directory); var original=new AgRuArchive(originalPath); original.CheckCompatible();
            if(original.Version!=installed.Version) throw new InvalidOperationException("Версии выбранной копии и установленного приложения различаются.");
            if(!installed.UnknownTranslation) throw new InvalidOperationException("Импорт нужен только для ранее установленной русификации без записи отката.");
            // Import is limited to the exact previously audited v3 build; never trust an arbitrary matching version string.
            if(AgRuArchive.FileHash(originalPath)!="8ce27ff7c2defa10ed1ed2817288a61c546690f4cae1b1a8c91d4e69e0851e4d" || installed.Hash!="17f45c8bf992e1a15c555bc0911ff9a85a30d89fc60380c100d6fa56ca705e4e") throw new InvalidOperationException("Эта пара архивов не входит в проверенную миграцию v3. Восстановите оригинал прежним инструментом.");
            string records=Records(directory); System.IO.Directory.CreateDirectory(records);
            string originalHash=AgRuArchive.FileHash(originalPath), dest=Path.Combine(records,originalHash), pending=Path.Combine(records,"pending-"+Guid.NewGuid().ToString("N"));
            if(System.IO.Directory.Exists(dest)) throw new InvalidOperationException("Запись для этой сборки уже существует.");
            System.IO.Directory.CreateDirectory(pending);
            File.Copy(originalPath,Path.Combine(pending,"original.asar")); File.Copy(installed.ArchivePath,Path.Combine(pending,"russian.asar"));
            var record=new BackupRecord { Version=installed.Version,OriginalSha256=originalHash,PatchedSha256=installed.Hash,CreatedUtc=DateTime.UtcNow.ToString("o"),Notes=new[]{"Imported audited v3 backup"} };
            ValidateRecord(pending,record,true); WriteManifest(pending,record); System.IO.Directory.Move(pending,dest);
            log("Резервная копия прежней русификации импортирована; установка не менялась."); return Inspect(directory);
        }); }
    }
}
