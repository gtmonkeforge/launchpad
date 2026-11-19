#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MonkeForge.Injector;

public static class Wire
{
    public const int Version = 1;
    public const string ConfigFolder = ".launchpad";
    public const string ConfigFile = "injector.txt";
    public const int MaxFieldBytes = 8 * 1024;
    public const int MaxBodyBytes = 64 * 1024 * 1024;

    public const byte End = 0;
    public const byte Mod = 1;
    public const byte Error = 2;
    public const byte Hello = 3;

    public static string ConfigPath(string gameRoot) => Path.Combine(Path.Combine(gameRoot, ConfigFolder), ConfigFile);

    public static string EncodeFields(IDictionary<string, string> fields)
    {
        var text = new StringBuilder();

        foreach (KeyValuePair<string, string> field in fields)
        {
            if (field.Key.Length == 0 || field.Key.IndexOfAny(new[] { '=', '\n', '\r' }) >= 0)
                throw new ArgumentException("Invalid field name: " + field.Key);

            text.Append(field.Key).Append('=').Append(Uri.EscapeDataString(field.Value ?? "")).Append('\n');
        }

        return text.ToString();
    }

    public static Dictionary<string, string> DecodeFields(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in text.Split('\n'))
        {
            var split = line.IndexOf('=');

            if (split <= 0)
                continue;

            fields[line.Substring(0, split)] = Uri.UnescapeDataString(line.Substring(split + 1).TrimEnd('\r'));
        }

        return fields;
    }

    public static void WriteFrame(Stream stream, byte type, IDictionary<string, string> fields, byte[] body = null)
    {
        var encoded = Encoding.UTF8.GetBytes(EncodeFields(fields ?? new Dictionary<string, string>()));

        if (encoded.Length > MaxFieldBytes)
            throw new InvalidDataException("Injector frame fields are too large.");

        if (type == Mod && (body == null || body.Length > MaxBodyBytes))
            throw new InvalidDataException("Injector mod frame has an invalid body.");

        var head = new byte[3];
        head[0] = type;
        head[1] = (byte)(encoded.Length >> 8);
        head[2] = (byte)encoded.Length;
        stream.Write(head, 0, head.Length);
        stream.Write(encoded, 0, encoded.Length);

        if (type == Mod)
        {
            var length = new byte[] { (byte)(body.Length >> 24), (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length };
            stream.Write(length, 0, length.Length);
            stream.Write(body, 0, body.Length);
        }

        stream.Flush();
    }

    public sealed class Frame
    {
        public byte Type;
        public Dictionary<string, string> Fields;
        public byte[] Body;

        public string Field(string key) => Fields != null && Fields.TryGetValue(key, out var value) ? value : "";
    }

    public static Frame ReadFrame(Stream stream)
    {
        var head = ReadExactly(stream, 3);

        if (head[0] > Hello)
            throw new InvalidDataException("Unknown injector frame type.");

        var fieldLength = (head[1] << 8) | head[2];

        if (fieldLength > MaxFieldBytes)
            throw new InvalidDataException("Injector frame fields are too large.");

        var frame = new Frame
        {
            Type = head[0],
            Fields = DecodeFields(Encoding.UTF8.GetString(ReadExactly(stream, fieldLength))),
        };

        if (frame.Type == Mod)
        {
            var length = ReadExactly(stream, 4);
            var bodyLength = ((long)length[0] << 24) | ((long)length[1] << 16) | ((long)length[2] << 8) | length[3];

            if (bodyLength <= 0 || bodyLength > MaxBodyBytes)
                throw new InvalidDataException("Injector mod frame has an invalid body.");

            frame.Body = ReadExactly(stream, (int)bodyLength);
        }

        return frame;
    }

    private static byte[] ReadExactly(Stream stream, int count)
    {
        var buffer = new byte[count];
        var offset = 0;

        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);

            if (read <= 0)
                throw new EndOfStreamException("Launchpad closed the injector connection.");

            offset += read;
        }

        return buffer;
    }
}
