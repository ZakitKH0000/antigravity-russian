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
        public static bool Pending(string cache,string exe) {
            string pending=Path.Combine(cache,"pending");
            try {
                DateTime created=File.GetCreationTimeUtc(exe), folder=Directory.GetLastWriteTimeUtc(Path.GetDirectoryName(exe)), installed=created>folder?created:folder;
                return Directory.Exists(pending) && Directory.EnumerateFiles(pending,"*.exe").Any(f=>File.GetLastWriteTimeUtc(f)>installed);
            } catch(IOException) { return false; } catch(UnauthorizedAccessException) { return false; }
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
