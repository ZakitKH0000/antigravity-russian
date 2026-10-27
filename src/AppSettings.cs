using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AntigravityRussian {
    // Preferences of the localizer itself, stored next to its backups.
    public sealed class AppSettings {
        public bool CheckUpdatesOnStart=true;
        public bool AgentRussian=true;
        public string LastUpdateCheckUtc;
        public static string FileFor(string dataRoot) { return Path.Combine(dataRoot,"settings.json"); }
        public static AppSettings Load(string dataRoot) {
            try {
                string file=FileFor(dataRoot);
                if(File.Exists(file)) return new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(file,Encoding.UTF8)) ?? new AppSettings();
            } catch(Exception) { /* Damaged preferences fall back to defaults; backups are unaffected. */ }
            return new AppSettings();
        }
        public void Save(string dataRoot) {
            Directory.CreateDirectory(dataRoot);
            string file=FileFor(dataRoot), temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));
                if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
            } finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        // At most one automatic GitHub request per day.
        public bool UpdateCheckDue(DateTime nowUtc) {
            DateTime last;
            return CheckUpdatesOnStart && (!DateTime.TryParse(LastUpdateCheckUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out last) || nowUtc-last.ToUniversalTime()>TimeSpan.FromHours(20));
        }
    }
}
