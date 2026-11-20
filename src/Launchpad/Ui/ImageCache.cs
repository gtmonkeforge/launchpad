using System.Collections.Concurrent;
using GoomeSharp;
using Launchpad.Forge;

namespace Launchpad.Ui;

public sealed class ImageCache(ForgeClient forge, Action changed)
{
    private readonly ConcurrentDictionary<string, Bitmap?> _ready = new();
    private readonly ConcurrentDictionary<string, byte[]> _arrived = new();
    private readonly ConcurrentDictionary<string, bool> _started = new();

    public Bitmap? Get(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (_ready.TryGetValue(path, out Bitmap? bitmap))
            return bitmap;

        if (_arrived.TryRemove(path, out var bytes))
        {
            try
            {
                bitmap = Bitmap.Load(bytes);
            }
            catch (GoomeException)
            {
                bitmap = null;
            }

            _ready[path] = bitmap;
            return bitmap;
        }

        if (_started.TryAdd(path, true))
            _ = Task.Run(() => Fetch(path));

        return null;
    }

    private async Task Fetch(string path)
    {
        try
        {
            using HttpResponseMessage response = await forge.SendAsync(HttpMethod.Get, path);
            response.EnsureSuccessStatusCode();
            _arrived[path] = await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception)
        {
            _ready[path] = null;
        }

        changed();
    }
}
