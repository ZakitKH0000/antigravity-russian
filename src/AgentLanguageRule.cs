using System;
using System.IO;
using System.Text;

namespace AntigravityRussian {
    // Global Antigravity rule asking the agent to talk to the user in Russian.
    // Lives in the global customizations root (~/.gemini/config/rules); only our own file is ever changed.
    public sealed class AgentLanguageRule {
        const string Marker="<!-- antigravity-russian:agent-language -->";
        public const string FileName="antigravity-russian.md";
        public static readonly string Text=
            "---\ntrigger: always_on\nglob:\ndescription: Antigravity Russian — язык общения агента\n---\n"+Marker+"\n"+
            "Общайся с пользователем на русском языке: ответы, вопросы и варианты ответов, планы, названия и описания шагов и задач, отчёты, "+
            "сообщения о ходе работы и рассуждения, которые видит пользователь. Пометки вариантов тоже пиши по-русски, например «(рекомендуется)» вместо «(Recommended)».\n"+
            "Код, команды, пути, имена файлов, идентификаторы, сообщения об ошибках инструментов и цитаты оставляй в оригинале.\n"+
            "Если пользователь явно попросит другой язык, используй его.\n";
        public readonly string Path;
        public AgentLanguageRule(string homeDirectory) { Path=System.IO.Path.Combine(homeDirectory,".gemini","config","rules",FileName); }
        // Same home directory as Antigravity's language server (Go os.UserHomeDir reads USERPROFILE on Windows).
        public AgentLanguageRule() : this(Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) { }
        public bool IsInstalled { get { return Owned(); } }
        // A file with our name but without our marker belongs to the user and is left alone.
        bool Owned() {
            try { return File.Exists(Path) && File.ReadAllText(Path,Encoding.UTF8).Contains(Marker); } catch(IOException) { return false; } catch(UnauthorizedAccessException) { return false; }
        }
        public void Install() {
            if(File.Exists(Path) && !Owned()) throw new InvalidOperationException("Файл правила "+FileName+" уже создан вручную. Русификатор его не меняет.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temp=Path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllText(temp,Text,new UTF8Encoding(false));
                if(File.Exists(Path)) File.Replace(temp,Path,null); else File.Move(temp,Path);
            } finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public void Remove() { if(Owned()) File.Delete(Path); }
    }
}
