using System;
using System.Net.Sockets;

namespace AntigravityRussian {
    // Coexistence with confeden/Antigravity (regional unlocker). It patches only language_server/agy and routes
    // the Language Server through AG_LS_PROXY; it unpacks app.asar only when dist/main.js does not look like the
    // v2.4+ shell. Nothing here changes network settings: the checks only read.
    public static class RegionalUnlocker {
        // Same predicate as the unlocker's is_new_desktop_architecture (src/patch_ide.rs).
        public static bool KeepsDesktopShape(string mainJs) {
            if(mainJs==null) return false;
            bool modular=mainJs.Contains("./languageServer") && mainJs.Contains("./ipcHandlers");
            bool legacyAuth=mainJs.Contains("_handleAuthErrorResponse") || mainJs.Contains("getUserStatus") || mainJs.Contains("\"AUTH_SUCCESS\"") || mainJs.Contains("\"SET_INELIGIBLE\"");
            return modular && !legacyAuth;
        }
        // A configured local unlocker proxy that does not answer: Antigravity sign-in and chat fail until it runs.
        public static string ProxyProblem() {
            string value=Environment.GetEnvironmentVariable("AG_LS_PROXY") ?? Environment.GetEnvironmentVariable("AG_LS_PROXY",EnvironmentVariableTarget.User);
            Uri uri;
            if(string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(),UriKind.Absolute,out uri) || !uri.IsLoopback) return null;
            try {
                using(var client=new TcpClient()) {
                    var attempt=client.BeginConnect(uri.Host,uri.Port,null,null);
                    if(attempt.AsyncWaitHandle.WaitOne(700) && client.Connected) { client.EndConnect(attempt); return null; }
                }
            } catch(SocketException) { }
            return "Служба анлокера confeden не отвечает на порту "+uri.Port+": Antigravity не сможет войти в аккаунт и ответить в чате. Запустите анлокер (от имени администратора), затем откройте Antigravity.";
        }
    }
}
