using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
namespace AntigravityRussian {
    public sealed class DiagnosticReport {
        readonly List<string> events=new List<string>();
        public void Record(string operation,Exception error) {
            // Only allowlisted event IDs and exception type names. Never copy raw messages or UI logs.
            string code=new[]{"inspect","backup","apply","restore","import","discover","update","launch","rule","shortcut"}.Contains(operation)?operation:"operation";
            events.Add(DateTime.UtcNow.ToString("HH:mm:ss")+"Z "+code+" / "+(error==null?"OK":error.GetType().Name+" 0x"+error.HResult.ToString("X8")));
            if(events.Count>12)events.RemoveAt(0);
        }
        public string Build(Installation installation) {
            var text=new StringBuilder();
            text.AppendLine("Antigravity Russian — отчёт разработчику");
            text.AppendLine("Русификатор: "+AppInfo.VersionText);
            text.AppendLine("Время UTC: "+DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            text.AppendLine("Windows: "+Environment.OSVersion.Version+" / "+(Environment.Is64BitOperatingSystem?"x64":"x86"));
            text.AppendLine("Установка найдена: "+(installation==null?"нет":"да"));
            if(installation!=null) {
                // The version is read from an external archive: do not transmit arbitrary package strings.
                System.Version version; text.AppendLine("Antigravity: "+(System.Version.TryParse(installation.Version,out version)?version.ToString():"неизвестная версия"));
                text.AppendLine("Русский интерфейс: "+installation.IsRussian);
                text.AppendLine("Резервная копия: "+installation.HasBackup);
                text.AppendLine("Неизвестная русификация: "+installation.UnknownTranslation);
                text.AppendLine("Папка resources\\app: "+installation.AppFolder);
                text.AppendLine("Скачано обновление Antigravity: "+installation.UpdatePending);
            }
            text.AppendLine("Последние действия:"); foreach(string e in events)text.AppendLine(e);
            text.AppendLine("Без переписки, токенов, имён пользователей, путей и содержимого файлов.");
            return text.ToString();
        }
        public static string TelegramDraft(string report) { return AppInfo.Telegram+"?text="+Uri.EscapeDataString(report); }
    }
}
