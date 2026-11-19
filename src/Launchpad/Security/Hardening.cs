using System.Diagnostics;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Launchpad.Security;

public static partial class Hardening
{
    public static bool DebuggerAttached()
    {
        if (Debugger.IsAttached)
            return true;

        if (OperatingSystem.IsWindows())
        {
            if (IsDebuggerPresent())
                return true;

            var remote = false;
            return CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remote) && remote;
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/self/status"))
                {
                    if (line.StartsWith("TracerPid:", StringComparison.Ordinal))
                        return line[10..].Trim() != "0";
                }
            }
            catch (IOException)
            {
            }
        }

        return false;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsDebuggerPresent();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CheckRemoteDebuggerPresent(IntPtr process, [MarshalAs(UnmanagedType.Bool)] ref bool present);

    private static readonly HashSet<string> PinnedRoots = new(StringComparer.Ordinal)
    {
        "C5+lpZ7tcVwmwQIMcRtPbsQtWLABXhQzejna0wHFr8M=",
        "diGVwiVYbubAI3RW4hB9xU8e/CH2GnkuvVFZE8zmgzI=",
        "sCkq5UWXjg+7mKu9lMhhYF5bGLsy7VI/UNW3tccdR7w=",
    };

    public static bool ValidatePinned(X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors != SslPolicyErrors.None || certificate == null || chain == null)
            return false;

        foreach (X509ChainElement element in chain.ChainElements)
        {
            var pin = Convert.ToBase64String(SHA256.HashData(element.Certificate.PublicKey.ExportSubjectPublicKeyInfo()));

            if (PinnedRoots.Contains(pin))
                return true;
        }

        return false;
    }
}
