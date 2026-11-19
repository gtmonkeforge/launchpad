using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Launchpad.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static async Task<T> ReadFileAsync<T>(string path, Func<T> fallback)
    {
        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options) ?? fallback();
        }
        catch (FileNotFoundException)
        {
            return fallback();
        }
        catch (DirectoryNotFoundException)
        {
            return fallback();
        }
        catch (JsonException)
        {
            throw new UserFacingException($"Could not read {path}. Repair or delete the file, then try again.");
        }
    }

    public static async Task WriteFileAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";

        await using (FileStream stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, value, Options);

        File.Move(temp, path, true);
    }

    public static string? Str(this JsonNode? node, string key)
    {
        return node is JsonObject obj && obj[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }

    public static bool Bool(this JsonNode? node, string key)
    {
        return node is JsonObject obj && obj[key] is JsonValue value && value.TryGetValue(out bool flag) && flag;
    }

    public static long Long(this JsonNode? node, string key)
    {
        return node is JsonObject obj && obj[key] is JsonValue value && value.TryGetValue(out long number) ? number : 0;
    }
}

public class UserFacingException(string message, Exception? inner = null) : Exception(message, inner);
