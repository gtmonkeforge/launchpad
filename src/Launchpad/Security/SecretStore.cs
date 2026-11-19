using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Launchpad.Infrastructure;

namespace Launchpad.Security;

public interface ISecretStore
{
    byte[]? Get(string name);
    void Set(string name, byte[] value);
    void Delete(string name);

    string Description { get; }
    bool Protected { get; }
}

public static class SecretStore
{
    public static ISecretStore Create()
    {
        if (OperatingSystem.IsWindows())
            return new DpapiSecretStore(AppPaths.Secrets);

        if (KeyringSecretStore.Available())
            return new KeyringSecretStore();

        return new FileSecretStore(AppPaths.Secrets);
    }

    internal static string FileName(string directory, string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9._-]{1,120}$"))
            throw new ArgumentException("Invalid secret name.", nameof(name));

        return Path.Combine(directory, name + ".bin");
    }
}

[SupportedOSPlatform("windows")]
internal sealed class DpapiSecretStore(string directory) : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("monkeforge-launchpad-v2");

    public string Description => "Windows Data Protection (DPAPI)";
    public bool Protected => true;

    public byte[]? Get(string name)
    {
        var path = SecretStore.FileName(directory, name);

        if (!File.Exists(path))
            return null;

        try
        {
            return ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            File.Delete(path);
            return null;
        }
    }

    public void Set(string name, byte[] value)
    {
        Directory.CreateDirectory(directory);
        var path = SecretStore.FileName(directory, name);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, ProtectedData.Protect(value, Entropy, DataProtectionScope.CurrentUser));
        File.Move(temp, path, true);
    }

    public void Delete(string name) => SafeFile.Delete(SecretStore.FileName(directory, name));
}

[UnsupportedOSPlatform("windows")]
internal sealed class KeyringSecretStore : ISecretStore
{
    private const string Service = "org.monkeforge.launchpad";

    public string Description => "your desktop keyring (Secret Service)";
    public bool Protected => true;

    public static bool Available()
    {
        try
        {
            (var code, _) = Run(["lookup", "service", Service, "key", "probe"], null, 5000);
            return code is 0 or 1;
        }
        catch
        {
            return false;
        }
    }

    public byte[]? Get(string name)
    {
        (var code, var output) = Run(["lookup", "service", Service, "key", name], null, 15000);

        if (code != 0 || output.Length == 0)
            return null;

        try
        {
            return Convert.FromBase64String(output.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public void Set(string name, byte[] value)
    {
        (var code, _) = Run(["store", "--label=MonkeForge Launchpad (" + name + ")", "service", Service, "key", name], Convert.ToBase64String(value), 30000);

        if (code != 0)
            throw new UserFacingException("Your desktop keyring refused to store Launchpad's sign-in. Unlock the keyring and try again.");
    }

    public void Delete(string name) => Run(["clear", "service", Service, "key", name], null, 15000);

    private static (int Code, string Output) Run(string[] args, string? input, int timeout)
    {
        var start = new ProcessStartInfo("secret-tool") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false };

        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("secret-tool did not start");

        if (input != null)
            process.StandardInput.Write(input);

        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(timeout))
        {
            process.Kill(true);
            throw new TimeoutException("The keyring did not respond.");
        }

        return (process.ExitCode, output.Result);
    }
}

internal sealed class FileSecretStore(string directory) : ISecretStore
{
    public string Description => "an owner-only file (no desktop keyring was found)";
    public bool Protected => false;

    public byte[]? Get(string name)
    {
        var path = SecretStore.FileName(directory, name);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void Set(string name, byte[] value)
    {
        Directory.CreateDirectory(directory);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var path = SecretStore.FileName(directory, name);
        var temp = path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };

        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var stream = new FileStream(temp, options))
            stream.Write(value);

        File.Move(temp, path, true);
    }

    public void Delete(string name) => SafeFile.Delete(SecretStore.FileName(directory, name));
}

public sealed class MemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, byte[]> _items = [];
    public string Description => "memory";
    public bool Protected => true;
    public byte[]? Get(string name) => _items.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, byte[] value) => _items[name] = value;
    public void Delete(string name) => _items.Remove(name);
}
