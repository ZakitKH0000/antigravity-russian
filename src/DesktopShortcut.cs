using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

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
        public bool Outdated { get { return IsInstalled && !string.Equals(Read(false),Target,StringComparison.OrdinalIgnoreCase); } }
        public bool Owned() { var d=Read(true); return d!=null && d.StartsWith(Marker,StringComparison.Ordinal); }

        public void Install() {
            if(File.Exists(Path) && !Owned()) throw new InvalidOperationException("На рабочем столе уже есть свой ярлык «"+System.IO.Path.GetFileNameWithoutExtension(FileName)+"». Русификатор его не меняет.");
            var link=(IShellLinkW)new ShellLink();
            try {
                link.SetPath(Target); link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(Target));
                link.SetDescription(Marker+" — перевод интерфейса Antigravity"); link.SetIconLocation(Target,0);
                ((IPersistFile)link).Save(Path,true);
            } finally { Marshal.ReleaseComObject(link); }
        }
        public void Remove() { if(IsInstalled) File.Delete(Path); }

        // The Unicode shell interface keeps the Cyrillic file name on any system code page.
        string Read(bool description) {
            if(!File.Exists(Path)) return null;
            var link=(IShellLinkW)new ShellLink();
            try {
                ((IPersistFile)link).Load(Path,0);
                var text=new StringBuilder(1024);
                if(description) link.GetDescription(text,text.Capacity); else link.GetPath(text,text.Capacity,IntPtr.Zero,4);
                return text.ToString();
            } catch(COMException) { return null; } catch(ArgumentException) { return null; }
            finally { Marshal.ReleaseComObject(link); }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,int size,IntPtr data,int flags);
            void GetIDList(out IntPtr list);
            void SetIDList(IntPtr list);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name,int size);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory,int size);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,int size);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int command);
            void SetShowCmd(int command);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int size,out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path,int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path,int reserved);
            void Resolve(IntPtr window,int flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
        }
    }
}
