using System.Text.RegularExpressions;

namespace md2visio.struc.graph
{
    internal sealed class GraphLayout
    {
        public Dictionary<string, LayoutNode> Nodes { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, LayoutNode> Subgraphs { get; } = new(StringComparer.Ordinal);

        public bool TryGetNode(string id, out LayoutNode node)
        {
            var normalized = NormalizeId(id);
            if (Nodes.TryGetValue(normalized, out node))
            {
                return true;
            }

            return false;
        }

        public bool TryGetNode(GNode node, out LayoutNode layout)
        {
            if (TryGetNode(node.ID, out layout)) return true;
            if (!string.IsNullOrWhiteSpace(node.Label) && TryGetNode(node.Label, out layout)) return true;
            return false;
        }

        public bool TryGetSubgraph(GSubgraph subgraph, out LayoutNode layout)
        {
            if (TryGetNode(subgraph.ID, out layout)) return true;
            if (!string.IsNullOrWhiteSpace(subgraph.Label) && TryGetNode(subgraph.Label, out layout)) return true;
            return false;
        }

        public bool CoversNodes(IEnumerable<GNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!TryGetNode(node, out _))
                {
                    Console.WriteLine($"[WARN] Mermaid CLI 布局缺少节点坐标: id='{node.ID}', label='{node.Label}'");
                    return false;
                }
            }
            return true;
        }

        public static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;

            var trimmed = id.Trim();
            var normalized = Regex.Replace(trimmed, @"^(flowchart|graph)[-_]", string.Empty, RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"-\d+$", string.Empty);
            return normalized;
        }
    }

    internal readonly record struct LayoutNode(double X, double Y, double Width, double Height);
}
