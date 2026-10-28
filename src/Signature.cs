using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace AntigravityRussian {
    // Installers are run only when Windows trusts their Authenticode signature and Google LLC signed them.
    public static class Signature {
        static readonly Guid GenericVerifyV2=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct FileInfo { public int Size; public string Path; public IntPtr Handle; public IntPtr Subject; }
        [StructLayout(LayoutKind.Sequential)]
        struct TrustData {
            public int Size; public IntPtr PolicyCallback, SipClientData; public int UiChoice, RevocationChecks, UnionChoice; public IntPtr File;
            public int StateAction; public IntPtr StateData, UrlReference; public int ProviderFlags, UiContext; public IntPtr SignatureSettings;
        }
        [DllImport("wintrust.dll",CharSet=CharSet.Unicode)] static extern int WinVerifyTrust(IntPtr window,[MarshalAs(UnmanagedType.LPStruct)] Guid action,ref TrustData data);

        public static bool Trusted(string path) {
            var file=new FileInfo { Size=Marshal.SizeOf(typeof(FileInfo)),Path=path };
            IntPtr filePtr=Marshal.AllocHGlobal(file.Size);
            try {
                Marshal.StructureToPtr(file,filePtr,false);
                // No UI, no revocation network calls; StateAction 0 verifies and releases at once.
                var data=new TrustData { Size=Marshal.SizeOf(typeof(TrustData)),UiChoice=2,RevocationChecks=0,UnionChoice=1,File=filePtr,ProviderFlags=0x1000 };
                return WinVerifyTrust(new IntPtr(-1),GenericVerifyV2,ref data)==0;
            } finally { Marshal.DestroyStructure(filePtr,typeof(FileInfo)); Marshal.FreeHGlobal(filePtr); }
        }
        public static bool SignedByGoogle(string path) {
            if(!Trusted(path)) return false;
            try { using(var cert=new X509Certificate2(X509Certificate.CreateFromSignedFile(path))) return cert.GetNameInfo(X509NameType.SimpleName,false)=="Google LLC" || cert.Subject.Contains("O=Google LLC"); }
            catch(System.Security.Cryptography.CryptographicException) { return false; }
        }
    }
}
