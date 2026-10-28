using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace AntigravityRussian {
    // The owner chose one-click russification: Antigravity is closed by the localizer, without a question.
    public static class AntigravityProcesses {
        // Processes started from the installation folder: windows, tray, language server and helpers.
        static List<Process> Find(string directory) {
            string prefix=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar; var found=new List<Process>();
            foreach(var process in Process.GetProcesses()) {
                bool ours=false;
                try { ours=process.MainModule.FileName.StartsWith(prefix,StringComparison.OrdinalIgnoreCase); }
                catch(System.ComponentModel.Win32Exception) { } catch(InvalidOperationException) { } catch(NotSupportedException) { }
                if(ours) found.Add(process); else process.Dispose();
            }
            return found;
        }
        static int Count(string directory) { var found=Find(directory); foreach(var p in found) p.Dispose(); return found.Count; }
        static void Wait(IEnumerable<Process> processes,int milliseconds) {
            var until=DateTime.UtcNow.AddMilliseconds(milliseconds);
            foreach(var p in processes) {
                int left=(int)Math.Max(0,(until-DateTime.UtcNow).TotalMilliseconds);
                try { p.WaitForExit(left); } catch(System.ComponentModel.Win32Exception) { } catch(InvalidOperationException) { }
            }
        }
        static bool Exited(Process p) { try { return p.HasExited; } catch(InvalidOperationException) { return true; } catch(System.ComponentModel.Win32Exception) { return false; } }

        // Asks the windows to close first, then ends what is left (Antigravity hides in the tray on close).
        public static bool Close(string directory,Action<string> log) {
            var running=Find(directory);
            if(running.Count==0) return false;
            try {
                log("Закрываем Antigravity, включая значок в трее…");
                foreach(var p in running) { try { p.CloseMainWindow(); } catch(InvalidOperationException) { } }
                Wait(running,5000);
                foreach(var p in running.Where(p=>!Exited(p))) { try { p.Kill(); } catch(System.ComponentModel.Win32Exception) { } catch(InvalidOperationException) { } }
                Wait(running,10000);
            } finally { foreach(var p in running) p.Dispose(); }
            int left=Count(directory);
            if(left>0) throw new InvalidOperationException("Не удалось закрыть Antigravity: осталось процессов — "+left+". Закройте его вручную, включая значок в трее, и повторите.");
            log("Antigravity закрыт.");
            return true;
        }
    }
}
