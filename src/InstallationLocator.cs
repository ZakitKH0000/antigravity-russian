using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace AntigravityRussian {
    public static class InstallationLocator {
        public static string DirectoryFromCommand(string command) {
            if(string.IsNullOrWhiteSpace(command))return null;
            string s=Environment.ExpandEnvironmentVariables(command.Trim()), file;
            if(s.StartsWith("\"")) { int end=s.IndexOf('"',1); if(end<0)return null; file=s.Substring(1,end-1); }
            else { int end=s.IndexOf(".exe",StringComparison.OrdinalIgnoreCase); if(end<0)return null; file=s.Substring(0,end+4); }
            if(!string.Equals(Path.GetFileName(file),"Antigravity.exe",StringComparison.OrdinalIgnoreCase))return null;
            try { return Path.GetDirectoryName(Path.GetFullPath(file)); } catch { return null; }
        }
        public static string FirstSupported(IEnumerable<string> candidates) {
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string candidate in candidates) {
                try {
                    if(string.IsNullOrWhiteSpace(candidate))continue;
                    string path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate)).TrimEnd(Path.DirectorySeparatorChar);
                    if(path.StartsWith("\\\\") || !seen.Add(path) || !File.Exists(Path.Combine(path,"Antigravity.exe")))continue;
                    string file=Path.Combine(path,"resources","app.asar"); if(!File.Exists(file))continue;
                    var archive=new AgRuArchive(file);
                    if(archive.Name=="antigravity"&&archive.Product=="Antigravity")return path;
                } catch { /* Stale paths, unavailable processes and unsupported editions are skipped. */ }
            }
            return null;
        }
        public static string Find() {
            var candidates=new List<string>();
            string saved=Path.Combine(PatchService.DefaultDataRoot,"installation.txt");
            try { if(File.Exists(saved))candidates.Add(File.ReadAllText(saved).Trim()); } catch(IOException) { } catch(UnauthorizedAccessException) { }
            foreach(var p in Process.GetProcessesByName("Antigravity"))using(p) { try { candidates.Add(Path.GetDirectoryName(p.MainModule.FileName)); } catch { } }
            foreach(var basis in new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}) {
                candidates.Add(Path.Combine(basis,"Programs","antigravity")); candidates.Add(Path.Combine(basis,"antigravity"));
                foreach(string parent in new[]{basis,Path.Combine(basis,"Programs")})try {
                    if(Directory.Exists(parent))foreach(string dir in Directory.EnumerateDirectories(parent,"*antigravity*",SearchOption.TopDirectoryOnly))candidates.Add(dir);
                } catch(IOException) { } catch(UnauthorizedAccessException) { }
            }
            foreach(RegistryHive hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32})try {
                using(var root=RegistryKey.OpenBaseKey(hive,view)) {
                    using(var app=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\Antigravity.exe"))if(app!=null)candidates.Add(DirectoryFromCommand(Convert.ToString(app.GetValue(null))));
                    using(var uninstall=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))if(uninstall!=null)foreach(string name in uninstall.GetSubKeyNames())using(var key=uninstall.OpenSubKey(name)) {
                        if(key==null || Convert.ToString(key.GetValue("DisplayName")).IndexOf("Antigravity",StringComparison.OrdinalIgnoreCase)<0)continue;
                        candidates.Add(Convert.ToString(key.GetValue("InstallLocation"))); candidates.Add(DirectoryFromCommand(Convert.ToString(key.GetValue("DisplayIcon"))));
                    }
                }
            } catch(System.Security.SecurityException) { } catch(UnauthorizedAccessException) { } catch(IOException) { }
            return FirstSupported(candidates);
        }
    }
}
