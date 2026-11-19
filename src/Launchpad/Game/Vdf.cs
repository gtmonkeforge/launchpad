using System.Text.RegularExpressions;

namespace Launchpad.Game;

public static partial class Vdf
{
    public sealed class Node
    {
        public required string Key { get; init; }
        public string? Value { get; init; }
        public List<Node>? Children { get; init; }

        public int Start { get; init; }
        public int End { get; init; }

        public int Close { get; init; } = -1;

        public Node? Child(string key) => Children?.FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("""
        "(?:\\.|[^"\\])*"|[{}]|//[^\n]*
        """)]
    private static partial Regex Tokens();

    public static List<Node> Parse(string source)
    {
        List<Match> tokens = Tokens().Matches(source).Where(t => !t.Value.StartsWith("//", StringComparison.Ordinal)).ToList();
        var i = 0;

        string Decode(string quoted) => Regex.Replace(quoted[1..^1], """\\([\\"])""", "$1");

        List<Node> Group()
        {
            var nodes = new List<Node>();

            while (i < tokens.Count && tokens[i].Value != "}")
            {
                if (i + 1 >= tokens.Count)
                    throw new FormatException("Invalid Steam file");

                Match key = tokens[i++], value = tokens[i++];

                if (!key.Value.StartsWith('"'))
                    throw new FormatException("Invalid Steam file");

                if (value.Value == "{")
                {
                    List<Node> children = Group();

                    if (i >= tokens.Count || tokens[i].Value != "}")
                        throw new FormatException("Invalid Steam file");

                    Match close = tokens[i++];
                    nodes.Add(new Node { Key = Decode(key.Value), Children = children, Start = key.Index, End = close.Index + 1, Close = close.Index });
                }
                else if (value.Value.StartsWith('"'))
                {
                    nodes.Add(new Node { Key = Decode(key.Value), Value = Decode(value.Value), Start = value.Index, End = value.Index + value.Length });
                }
                else
                {
                    throw new FormatException("Invalid Steam file");
                }
            }

            return nodes;
        }

        return Group();
    }

    public static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static Node? Find(List<Node> roots, params string[] path)
    {
        Node? node = null;
        List<Node>? level = roots;

        foreach (var key in path)
        {
            node = level?.FirstOrDefault(n => n.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (node == null)
                return null;

            level = node.Children;
        }

        return node;
    }
}
