using System.Reflection;
namespace AntigravityRussian {
    public static class AppInfo {
        public const string Repository="https://github.com/ZakitKH0000/antigravity-russian";
        public const string RegionalProject="https://github.com/confeden/Antigravity";
        public const string Telegram="https://t.me/zak0407";
        public static System.Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public static string VersionText { get { return Version.ToString(3); } }
    }
}
