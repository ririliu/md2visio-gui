using System.Text;
using System.Text.RegularExpressions;

namespace md2visio.mermaid.cmn
{
    internal static class MermaidBlockExtractor
    {
        private static readonly Regex BlockRegex = new(
            @"^\s*```+\s*mermaid\s*(?:\r?\n)(?<content>.*?)(?:\r?\n)```+",
            RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Dictionary<string, IReadOnlyList<string>> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<string> ExtractBlocks(string inputFile)
        {
            if (string.IsNullOrWhiteSpace(inputFile) || !File.Exists(inputFile))
            {
                return Array.Empty<string>();
            }

            if (Cache.TryGetValue(inputFile, out var cached))
            {
                return cached;
            }

            var content = File.ReadAllText(inputFile, Encoding.UTF8);
            var blocks = new List<string>();

            foreach (Match match in BlockRegex.Matches(content))
            {
                var block = match.Groups["content"].Value;
                blocks.Add(block.Replace("\r\n", "\n"));
            }

            Cache[inputFile] = blocks;
            return blocks;
        }

        public static string? TryGetBlock(string inputFile, int index)
        {
            var blocks = ExtractBlocks(inputFile);
            if (index < 0 || index >= blocks.Count)
            {
                return null;
            }

            return blocks[index];
        }
    }
}
