using System.Text.RegularExpressions;

namespace Launchpad.Game;

public static partial class Proton
{
    [GeneratedRegex("""(?:^|\s)WINEDLLOVERRIDES=(?:"([^"\n]*)"|'([^'\n]*)'|([^\s"']+))""")]
    private static partial Regex Assignment();

    [GeneratedRegex(@"(?:^|\s)WINEDLLOVERRIDES=")]
    private static partial Regex AnyAssignment();

    private static bool Targets(string entry, string proxy) =>
        entry.Split('=')[0].Split(',').Any(name => Regex.Replace(name.Trim(), @"\.dll$", "", RegexOptions.IgnoreCase).Equals(proxy, StringComparison.OrdinalIgnoreCase));

    public static string LaunchOptions(string options, string proxy)
    {
        Match? assignment = Assignment().Matches(options).LastOrDefault();

        if (assignment != null)
        {
            var value = assignment.Groups[1].Success ? assignment.Groups[1].Value : assignment.Groups[2].Success ? assignment.Groups[2].Value : assignment.Groups[3].Value;
            var entries = value.Split(';');
            var existing = entries.Where(e => Targets(e, proxy)).LastOrDefault();

            if (existing != null && Regex.IsMatch((existing.Split('=').ElementAtOrDefault(1) ?? "").Split(',')[0].Trim(), "^(n|native)$", RegexOptions.IgnoreCase))
                return options;

            IEnumerable<string> kept = entries.SelectMany(entry =>
            {
                if (!Targets(entry, proxy))
                    return [entry];

                var parts = entry.Split('=', 2);
                var others = parts[0].Split(',').Where(name => !Regex.Replace(name.Trim(), @"\.dll$", "", RegexOptions.IgnoreCase).Equals(proxy, StringComparison.OrdinalIgnoreCase)).ToArray();
                return others.Length > 0 ? [string.Join(',', others) + "=" + (parts.ElementAtOrDefault(1) ?? "")] : Array.Empty<string>();
            }).Where(entry => entry.Length > 0);

            var updated = string.Join(';', kept.Append(proxy + "=n,b"));
            var start = assignment.Index + assignment.Value.IndexOf("WINEDLLOVERRIDES=", StringComparison.Ordinal);
            var end = assignment.Index + assignment.Length;
            return options[..start] + $"WINEDLLOVERRIDES=\"{updated}\"" + options[end..];
        }

        if (AnyAssignment().IsMatch(options))
            throw new FormatException("Gorilla Tag's Steam launch options have an invalid WINEDLLOVERRIDES value. Fix it in Steam, then try again.");

        var command = options.Contains("%command%", StringComparison.Ordinal) ? options : "%command%" + (options.Length > 0 ? " " + options : "");
        return $"WINEDLLOVERRIDES=\"{proxy}=n,b\" {command}";
    }

    public static string PatchLocalConfig(string source, string proxy)
    {
        List<Vdf.Node> roots = Vdf.Parse(source);
        Vdf.Node apps = Vdf.Find(roots, "UserLocalConfigStore", "Software", "Valve", "Steam", "apps")
            ?? throw new FormatException("Steam settings not found. Launch Gorilla Tag once through Steam, then try again.");

        Vdf.Node app = apps.Child(GameLocator.SteamAppId) is { Children: not null, Close: >= 0 } found
            ? found
            : throw new FormatException("Launch Gorilla Tag once through Steam, then try again.");

        Vdf.Node? launch = app.Child("LaunchOptions");

        if (launch != null)
        {
            var updated = LaunchOptions(launch.Value ?? "", proxy);
            return updated == launch.Value ? source : source[..launch.Start] + Vdf.Quote(updated) + source[launch.End..];
        }

        return source[..app.Close] + $"\n\"LaunchOptions\" {Vdf.Quote(LaunchOptions("", proxy))}\n" + source[app.Close..];
    }

    public sealed record Result(int Accounts, int Updated);

    public static Result Configure(IEnumerable<string> steamRoots, string proxy)
    {
        int accounts = 0, updated = 0;

        foreach (var root in steamRoots)
        {
            var userdata = Path.Combine(root, "userdata");

            if (!Directory.Exists(userdata))
                continue;

            foreach (var account in Directory.GetDirectories(userdata))
            {
                var file = Path.Combine(account, "config", "localconfig.vdf");

                if (!File.Exists(file))
                    continue;

                var source = File.ReadAllText(file);

                if (!source.Contains($"\"{GameLocator.SteamAppId}\"", StringComparison.Ordinal))
                    continue;

                var patched = PatchLocalConfig(source, proxy);

                if (patched != source)
                {
                    var backup = file + ".launchpad-backup";

                    if (!File.Exists(backup))
                        File.WriteAllText(backup, source);

                    var temp = file + ".launchpad-tmp";
                    File.WriteAllText(temp, patched);
                    File.Move(temp, file, true);
                    updated++;
                }

                accounts++;
            }
        }

        if (accounts == 0)
            throw new FormatException("Launch Gorilla Tag once through Steam, then try again.");

        return new Result(accounts, updated);
    }
}
