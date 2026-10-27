using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
[assembly: AssemblyTitle("Antigravity Russian")]
[assembly: AssemblyDescription("Unofficial Russian interface patcher for Antigravity App")]
[assembly: AssemblyCompany("Antigravity Russian contributors")]
[assembly: AssemblyProduct("Antigravity Russian")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: ComVisible(false)]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]
namespace AntigravityRussian {
    static class Program {
        [STAThread] static void Main(string[] args) {
            UseEmbeddedConfiguration();
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==2&&args[0]=="--finish-update") {
                try{UpdateService.Complete(args[1]);}catch(Exception ex){MessageBox.Show("Обновление не выполнено: "+ex.Message+"\nПредыдущая версия сохранена. Скачайте новую версию вручную из Releases.","Antigravity Russian",MessageBoxButtons.OK,MessageBoxIcon.Information);}return;
            }
            Application.Run(new MainForm());
        }
        // A single downloaded EXE has no .config beside it: use the embedded one (WinForms high-DPI mode) before any configuration is read.
        static void UseEmbeddedConfiguration() {
            try {
                if(File.Exists(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile))return;
                byte[] config; using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("app.config")) { if(s==null)return; config=new byte[s.Length]; s.Read(config,0,config.Length); }
                string folder=Path.Combine(Path.GetTempPath(),"AntigravityRussian"), file=Path.Combine(folder,"AntigravityRussian-"+AppInfo.VersionText+".config");
                Directory.CreateDirectory(folder);
                if(!File.Exists(file)||!AgRuArchive.Hash(File.ReadAllBytes(file)).Equals(AgRuArchive.Hash(config)))File.WriteAllBytes(file,config);
                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE",file);
            } catch(Exception) { /* Defaults still work; only fine high-DPI scaling may differ. */ }
        }
    }
}
