using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AntigravityRussian {
    // Desktop shortcut that opens this localizer. Only a shortcut carrying our marker is changed or removed.
    public sealed class DesktopShortcut {
        public const string FileName="Русификатор Antigravity.lnk";
        public const string Marker="Antigravity Russian";
        public readonly string Path, Target;
        public DesktopShortcut(string desktop,string target) { Path=System.IO.Path.Combine(desktop,FileName); Target=System.IO.Path.GetFullPath(target); }
        public DesktopShortcut() : this(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),System.Windows.Forms.Application.ExecutablePath) { }

        public bool IsInstalled { get { return File.Exists(Path) && Owned(); } }
        // Shortcut made by us that points to an older location of the EXE.
        public bool Outdated { get { return IsInstalled && !string.Equals(Read("TargetPath"),Target,StringComparison.OrdinalIgnoreCase); } }
        public bool Owned() { var d=Read("Description"); return d!=null && d.StartsWith(Marker,StringComparison.Ordinal); }

        public void Install() {
            if(File.Exists(Path) && !Owned()) throw new InvalidOperationException("На рабочем столе уже есть свой ярлык «"+System.IO.Path.GetFileNameWithoutExtension(FileName)+"». Русификатор его не меняет.");
            Use(link=> {
                Set(link,"TargetPath",Target); Set(link,"WorkingDirectory",System.IO.Path.GetDirectoryName(Target));
                Set(link,"Description",Marker+" — перевод интерфейса Antigravity"); Set(link,"IconLocation",Target+",0");
                link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null); return null;
            });
        }
        public void Remove() { if(IsInstalled) File.Delete(Path); }

        string Read(string property) {
            if(!File.Exists(Path)) return null;
            try { return Use(link=>link.GetType().InvokeMember(property,BindingFlags.GetProperty,null,link,null) as string); }
            catch(TargetInvocationException) { return null; } catch(COMException) { return null; }
        }
        static void Set(object link,string property,string value) { link.GetType().InvokeMember(property,BindingFlags.SetProperty,null,link,new object[]{value}); }
        // WScript.Shell is part of every Windows installation; late binding avoids an interop assembly.
        string Use(Func<object,string> action) {
            object shell=null, link=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
                link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Path});
                return action(link);
            } finally {
                if(link!=null) Marshal.ReleaseComObject(link);
                if(shell!=null) Marshal.ReleaseComObject(shell);
            }
        }
    }
}
