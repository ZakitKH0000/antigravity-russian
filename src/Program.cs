using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
[assembly: AssemblyTitle("Antigravity Russian")]
[assembly: AssemblyDescription("Unofficial Russian interface patcher for Antigravity App")]
[assembly: AssemblyCompany("Antigravity Russian contributors")]
[assembly: AssemblyProduct("Antigravity Russian")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: ComVisible(false)]
namespace AntigravityRussian {
    static class Program {
        [STAThread] static void Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==2&&args[0]=="--finish-update") {
                try{UpdateService.Complete(args[1]);}catch(Exception ex){MessageBox.Show("Обновление не выполнено: "+ex.Message+"\nПредыдущая версия сохранена. Скачайте ZIP вручную из Releases.","Antigravity Russian",MessageBoxButtons.OK,MessageBoxIcon.Information);}return;
            }
            Application.Run(new MainForm());
        }
    }
}
