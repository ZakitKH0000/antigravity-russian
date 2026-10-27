using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
[assembly: AssemblyTitle("Antigravity Russian")]
[assembly: AssemblyDescription("Unofficial Russian interface patcher for Antigravity App")]
[assembly: AssemblyCompany("Antigravity Russian contributors")]
[assembly: AssemblyProduct("Antigravity Russian")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: ComVisible(false)]
namespace AntigravityRussian {
    static class Program {
        [STAThread] static void Main() {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
