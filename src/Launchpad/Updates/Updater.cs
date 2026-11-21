using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Updates;

// Keeps Launchpad up to date from the Launchpad server (LaunchpadServer.Root).
//
// On start and every few hours it asks for the newest release for this platform. The answer is a manifest signed with
// the offline release key (scripts/release.cs); only a manifest that verifies against ReleaseKey below, names a newer
// version, and lists a file for this platform counts. The installer is downloaded in the background and checked
// against the manifest's size and SHA-256 before Launchpad offers it. It's installed when the person chooses
// "Restart to update", or quietly when they close Launchpad.
//
// Builds that aren't installed in the usual place (running from bin/, or an unpacked tarball elsewhere) still hear
// about updates but leave installing to the person.
public sealed class Updater : IDisposable
{
    // The public half of the release key. Replacing it means older installs stop accepting updates.
    public const string ReleaseKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAELbXqvFoRCSXf7bhHCVQMNa0mXpm4GpyuVfRnpYrUYh++/n/gCKRTCal6ZkN/Z5wXg2VjRRH5kwd7gZDrLkoXcQ==";

    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(4);

    private readonly HttpClient _http;
    private readonly Uri _server;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly string _folder = Path.Combine(AppPaths.Root, "updates");

    private UpdateState _state;
    private string? _installer;

    public Updater(HttpMessageHandler? handler = null)
    {
        _server = LaunchpadServer.Root;
        _http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 3, UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All }, true)
        {
            Timeout = TimeSpan.FromMinutes(20),
        };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MonkeForge-Launchpad", Current.ToString(3)));
        _state = new UpdateState("idle", Current.ToString(3), null, null, null, null, CanInstall, null);
    }

    public static Version Current { get; } = typeof(Updater).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);

    public static string Os => OperatingSystem.IsWindows() ? "windows" : "linux";
    public static string Arch => System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";

    // state: idle, checking, current, downloading, ready, error. ready means an installer is downloaded and verified.
    public sealed record UpdateState(string State, string Current, string? Version, string? Notes, string? PublishedAt, int? Progress, bool CanInstall, string? Error);

    public UpdateState State
    {
        get
        {
            lock (_stateGate)
                return _state;
        }
    }

    public event Action<UpdateState>? Changed;

    private void Set(UpdateState state)
    {
        lock (_stateGate)
            _state = state;

        Changed?.Invoke(state);
    }

    // Where the installers put Launchpad. Only there does it replace itself.
    public static bool CanInstall
    {
        get
        {
            var exe = Environment.ProcessPath;

            if (exe == null || Environment.GetEnvironmentVariable("LAUNCHPAD_NO_SELF_UPDATE") == "1")
                return false;

            var folder = Path.GetDirectoryName(Path.GetFullPath(exe)) ?? "";

            if (OperatingSystem.IsWindows())
            {
                var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MonkeForge Launchpad");
                return string.Equals(folder.TrimEnd('\\'), expected, StringComparison.OrdinalIgnoreCase);
            }

            var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return folder.TrimEnd('/') == Path.Combine(data, "monkeforge-launchpad");
        }
    }

    public void Start(Func<bool> enabled, CancellationToken ct)
    {
        CleanOldDownloads();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);

                while (!ct.IsCancellationRequested)
                {
                    if (enabled())
                        await CheckAsync(true, ct);

                    await Task.Delay(CheckEvery, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, ct);
    }

    // Checks for a newer release and, when this install can update itself, downloads it. Background checks keep quiet
    // about network problems; a check the person asked for reports them.
    public async Task<UpdateState> CheckAsync(bool background, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(background ? TimeSpan.Zero : System.Threading.Timeout.InfiniteTimeSpan, ct))
            return State;

        try
        {
            if (State.State == "ready")
                return State;

            UpdateState previous = State;

            if (!background)
                Set(previous with { State = "checking", Error = null });

            (ReleaseManifest Manifest, ReleaseFile File)? found;

            try
            {
                found = await LatestAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UpdateException && !ct.IsCancellationRequested)
            {
                var message = ex is UpdateException ? ex.Message : "The update server couldn’t be reached. Check your internet connection.";
                Set(background ? previous : previous with { State = "error", Error = message });
                return State;
            }

            if (found is not { } update)
            {
                Set(new UpdateState("current", Current.ToString(3), null, null, null, null, CanInstall, null));
                return State;
            }

            ReleaseManifest manifest = update.Manifest;
            var available = new UpdateState("available", Current.ToString(3), manifest.Version, manifest.Notes, manifest.PublishedAt, null, CanInstall, null);

            if (!CanInstall)
            {
                Set(available);
                return State;
            }

            try
            {
                _installer = await DownloadAsync(manifest, update.File, available, ct);
                Set(available with { State = "ready", Progress = 100 });
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or UpdateException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                Set(available with { State = "error", Error = ex is UpdateException ? ex.Message : "The update couldn’t be downloaded. Launchpad will try again later." });
            }

            return State;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class UpdateException(string message) : Exception(message);

    private async Task<(ReleaseManifest, ReleaseFile)?> LatestAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.GetAsync(new Uri(_server, $"/api/v1/updates/latest?os={Os}&arch={Arch}&version={Current.ToString(3)}"), ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new UpdateException($"The update server returned {(int)response.StatusCode}. Try again later.");

        JsonNode? body;

        try
        {
            body = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (System.Text.Json.JsonException)
        {
            throw new UpdateException("The update server sent a response Launchpad couldn’t read.");
        }

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(body.Str("manifest") ?? "");
        }
        catch (FormatException)
        {
            throw new UpdateException("The update server sent a response Launchpad couldn’t read.");
        }

        if (!ReleaseManifest.VerifySignature(bytes, body.Str("signature"), ReleaseKey))
            throw new UpdateException("The update server offered an update that isn’t signed by MonkeForge, so Launchpad ignored it.");

        ReleaseManifest manifest;

        try
        {
            manifest = ReleaseManifest.Parse(bytes);
        }
        catch (FormatException)
        {
            throw new UpdateException("The update server offered an update Launchpad couldn’t read.");
        }

        ReleaseFile? file = manifest.FileFor(Os, Arch);
        return manifest.ParsedVersion > Current && file != null ? (manifest, file) : null;
    }

    private async Task<string> DownloadAsync(ReleaseManifest manifest, ReleaseFile file, UpdateState progressState, CancellationToken ct)
    {
        Directory.CreateDirectory(_folder);
        var target = Path.Combine(_folder, file.Name);

        if (File.Exists(target) && new FileInfo(target).Length == file.Size && await HashAsync(target, ct) == file.Sha256)
            return target;

        var partial = target + ".part";
        Set(progressState with { State = "downloading", Progress = 0 });

        using (HttpResponseMessage response = await _http.GetAsync(new Uri(_server, $"/download/{Uri.EscapeDataString(manifest.Version)}/{Uri.EscapeDataString(file.Name)}"), HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (!response.IsSuccessStatusCode)
                throw new UpdateException($"The update couldn’t be downloaded (the server returned {(int)response.StatusCode}). Launchpad will try again later.");

            await using Stream input = await response.Content.ReadAsStreamAsync(ct);
            await using FileStream output = File.Create(partial);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;
            var lastPercent = -1;
            int read;

            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                total += read;

                if (total > file.Size)
                    throw new UpdateException("The update download was larger than expected, so Launchpad discarded it.");

                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);

                var percent = (int)(total * 100 / file.Size);

                if (percent != lastPercent && percent % 5 == 0)
                {
                    lastPercent = percent;
                    Set(progressState with { State = "downloading", Progress = percent });
                }
            }

            if (total != file.Size || Convert.ToHexStringLower(hash.GetHashAndReset()) != file.Sha256)
            {
                output.Close();
                File.Delete(partial);
                throw new UpdateException("The update download didn’t match its signature, so Launchpad discarded it.");
            }
        }

        File.Move(partial, target, true);
        return target;
    }

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }

    private void CleanOldDownloads()
    {
        try
        {
            if (!Directory.Exists(_folder))
                return;

            foreach (var entry in Directory.EnumerateFileSystemEntries(_folder))
            {
                if (File.Exists(entry))
                    File.Delete(entry);
                else
                    Directory.Delete(entry, true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Starts the installer in a helper that waits for this process to exit first, so files aren't in use and the
    // single-instance lock is free when the new version opens. Returns false when there's nothing to install.
    public bool LaunchInstaller(bool relaunch)
    {
        string? installer;

        lock (_stateGate)
            installer = _state.State == "ready" ? _installer : null;

        if (installer == null || !File.Exists(installer) || !CanInstall)
            return false;

        var start = OperatingSystem.IsWindows() ? WindowsInstaller(installer, relaunch) : LinuxInstaller(installer, relaunch);

        if (start == null)
            return false;

        start.Environment["LP_PID"] = Environment.ProcessId.ToString();
        start.Environment["LP_EXE"] = Environment.ProcessPath ?? "";
        start.Environment["LP_RELAUNCH"] = relaunch ? "1" : "0";

        try
        {
            using Process? _ = Process.Start(start);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static ProcessStartInfo WindowsInstaller(string msi, bool relaunch)
    {
        // The MSI is per-user with MajorUpgrade, so it upgrades in place without an admin prompt. /passive shows a
        // small progress bar when the person asked for the update; on exit it runs silently.
        const string Script = """
            $ErrorActionPreference = 'SilentlyContinue'
            Wait-Process -Id ([int]$env:LP_PID) -Timeout 60
            $ui = if ($env:LP_RELAUNCH -eq '1') { '/passive' } else { '/quiet' }
            $p = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList @('/i', ('"' + $env:LP_MSI + '"'), $ui, '/norestart') -Wait -PassThru
            if ($env:LP_RELAUNCH -eq '1' -and ($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010)) { Start-Process -FilePath $env:LP_EXE }
            Remove-Item -LiteralPath $env:LP_MSI -Force
            """;

        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(Script)) })
            start.ArgumentList.Add(arg);

        start.Environment["LP_MSI"] = msi;
        return start;
    }

    private ProcessStartInfo? LinuxInstaller(string archive, bool relaunch)
    {
        // Unpack now, while Launchpad can still report a problem; install.sh then swaps the app folder after exit.
        var unpacked = Path.Combine(_folder, "unpacked");

        try
        {
            if (Directory.Exists(unpacked))
                Directory.Delete(unpacked, true);

            Directory.CreateDirectory(unpacked);

            using (FileStream file = File.OpenRead(archive))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                TarFile.ExtractToDirectory(gzip, unpacked, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }

        var installScript = Directory.EnumerateFiles(unpacked, "install.sh", SearchOption.AllDirectories).FirstOrDefault();

        if (installScript == null)
            return null;

        const string Script = """
            while kill -0 "$LP_PID" 2>/dev/null; do sleep 0.2; done
            if bash "$LP_INSTALL" >/dev/null 2>&1 && [ "$LP_RELAUNCH" = 1 ]; then
                nohup "$LP_EXE" >/dev/null 2>&1 &
            fi
            rm -rf -- "$LP_UPDATES"
            """;

        var start = new ProcessStartInfo("/usr/bin/env") { UseShellExecute = false };

        foreach (var arg in new[] { "setsid", "-f", "bash", "-c", Script })
            start.ArgumentList.Add(arg);

        start.Environment["LP_INSTALL"] = installScript;
        start.Environment["LP_UPDATES"] = _folder;
        return start;
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}
