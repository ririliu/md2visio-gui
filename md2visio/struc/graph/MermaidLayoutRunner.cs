using md2visio.Api;
using md2visio.struc.figure;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace md2visio.struc.graph
{
    internal sealed class MermaidLayoutRunner
    {
        private const int DefaultTimeoutMs = 15000;
        private readonly Config _config;
        private readonly ConversionContext _context;

        public MermaidLayoutRunner(Config config, ConversionContext context)
        {
            _config = config;
            _context = context;
        }

        public GraphLayout? TryGetLayout(string mermaidSource)
        {
            if (!ShouldUseLayout())
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(mermaidSource))
            {
                _context.LogWarning("Mermaid 源文本为空，跳过 CLI 布局。");
                return null;
            }

            var cli = ResolveCliPath();
            if (string.IsNullOrWhiteSpace(cli))
            {
                _context.LogWarning("未配置 Mermaid CLI 路径，跳过 CLI 布局。");
                return null;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "md2visio-layout", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var inputPath = Path.Combine(tempDir, "diagram.mmd");
            var outputPath = Path.Combine(tempDir, "diagram.svg");
            var configPath = Path.Combine(tempDir, "config.json");

            try
            {
                File.WriteAllText(inputPath, mermaidSource, Encoding.UTF8);
                File.WriteAllText(configPath, BuildMermaidConfigJson(), new UTF8Encoding(false));

                if (!RunMermaidCli(cli, inputPath, outputPath, configPath))
                {
                    return null;
                }

                if (!File.Exists(outputPath))
                {
                    _context.LogWarning("Mermaid CLI 未生成 SVG 输出，跳过布局。");
                    return null;
                }

                var svgContent = File.ReadAllText(outputPath, Encoding.UTF8);
                return MermaidSvgLayoutParser.TryParse(svgContent, _context);
            }
            catch (Exception ex)
            {
                _context.LogWarning($"Mermaid CLI 布局失败: {ex.Message}");
                return null;
            }
            finally
            {
                if (_context.Debug)
                {
                    _context.LogInfo($"Mermaid CLI 临时目录保留: {tempDir}");
                }
                else
                {
                    TryCleanup(tempDir);
                }
            }
        }

        private bool ShouldUseLayout()
        {
            if (_config.GetBool("config.flowchart.useMermaidLayout", out bool enabled))
            {
                return enabled;
            }

            return false;
        }

        private string ResolveCliPath()
        {
            if (_config.GetString("config.flowchart.layoutCli", out string cli))
            {
                return cli;
            }

            return "mmdc";
        }

        private bool RunMermaidCli(string cli, string inputPath, string outputPath, string configPath)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = cli,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(inputPath) ?? string.Empty
            };
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(inputPath);
            process.StartInfo.ArgumentList.Add("-o");
            process.StartInfo.ArgumentList.Add(outputPath);
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add(configPath);

            process.Start();

            int timeout = DefaultTimeoutMs;
            if (_config.GetInt("config.flowchart.layoutTimeoutMs", out int configuredTimeout) && configuredTimeout > 0)
            {
                timeout = configuredTimeout;
            }

            if (!process.WaitForExit(timeout))
            {
                TryKill(process);
                _context.LogWarning("Mermaid CLI 超时，已终止布局进程。");
                return false;
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                _context.LogWarning($"Mermaid CLI 退出码 {process.ExitCode}: {stderr}".Trim());
                return false;
            }

            return true;
        }

        private string BuildMermaidConfigJson()
        {
            var flowchart = new Dictionary<string, object>();

            if (_config.GetString("config.flowchart.defaultRenderer", out string renderer))
            {
                flowchart["defaultRenderer"] = renderer;
            }
            if (_config.GetDouble("config.flowchart.nodeSpacing", out double nodeSpacing))
            {
                flowchart["nodeSpacing"] = nodeSpacing;
            }
            if (_config.GetDouble("config.flowchart.rankSpacing", out double rankSpacing))
            {
                flowchart["rankSpacing"] = rankSpacing;
            }
            if (_config.GetDouble("config.flowchart.diagramPadding", out double diagramPadding))
            {
                flowchart["diagramPadding"] = diagramPadding;
            }

            var mermaidConfig = new Dictionary<string, object>
            {
                ["flowchart"] = flowchart
            };

            if (_config.GetString("config.layout", out string layout))
            {
                mermaidConfig["layout"] = layout;
            }

            return JsonSerializer.Serialize(mermaidConfig, new JsonSerializerOptions { WriteIndented = true });
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }

        private static void TryCleanup(string tempDir)
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
            }
        }

        private static class MermaidSvgLayoutParser
        {
            public static GraphLayout? TryParse(string svgContent, ConversionContext context)
            {
                try
                {
                    var document = XDocument.Parse(svgContent);
                    var layout = new GraphLayout();

                    foreach (var group in document.Descendants().Where(e => e.Name.LocalName == "g"))
                    {
                        var className = group.Attribute("class")?.Value ?? string.Empty;
                        if (className.Contains("node") && !className.Contains("edge", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryReadLayoutNode(group, out var id, out var node))
                            {
                                layout.Nodes[GraphLayout.NormalizeId(id)] = node;
                            }
                        }
                        else if (className.Contains("cluster", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryReadLayoutNode(group, out var id, out var node))
                            {
                                layout.Subgraphs[GraphLayout.NormalizeId(id)] = node;
                            }
                        }
                    }

                    if (layout.Nodes.Count == 0)
                    {
                        context.LogWarning("Mermaid CLI 未输出节点坐标，跳过布局。");
                        return null;
                    }

                    return layout;
                }
                catch (Exception ex)
                {
                    context.LogWarning($"SVG 布局解析失败: {ex.Message}");
                    return null;
                }
            }

            private static bool TryReadLayoutNode(XElement group, out string id, out LayoutNode node)
            {
                id = group.Attribute("data-id")?.Value
                    ?? group.Attribute("id")?.Value
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(id))
                {
                    node = default;
                    return false;
                }

                if (!TryParseTranslate(group.Attribute("transform")?.Value, out double x, out double y))
                {
                    node = default;
                    return false;
                }

                TryParseSize(group, out double width, out double height);
                node = new LayoutNode(x, y, width, height);
                return true;
            }

            private static bool TryParseTranslate(string? transform, out double x, out double y)
            {
                x = 0;
                y = 0;
                if (string.IsNullOrWhiteSpace(transform)) return false;

                var match = Regex.Match(transform, @"translate\((?<x>[-\d\.]+)[ ,]+(?<y>[-\d\.]+)\)");
                if (!match.Success) return false;

                if (!double.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
                {
                    return false;
                }
                if (!double.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                {
                    return false;
                }

                return true;
            }

            private static void TryParseSize(XElement group, out double width, out double height)
            {
                width = 0;
                height = 0;

                var rect = group.Descendants().FirstOrDefault(e => e.Name.LocalName == "rect");
                if (rect != null)
                {
                    width = ParseLength(rect.Attribute("width")?.Value);
                    height = ParseLength(rect.Attribute("height")?.Value);
                    return;
                }

                var ellipse = group.Descendants().FirstOrDefault(e => e.Name.LocalName == "ellipse");
                if (ellipse != null)
                {
                    var rx = ParseLength(ellipse.Attribute("rx")?.Value);
                    var ry = ParseLength(ellipse.Attribute("ry")?.Value);
                    if (rx > 0) width = rx * 2;
                    if (ry > 0) height = ry * 2;
                }
            }

            private static double ParseLength(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return 0;
                var trimmed = value.Trim().TrimEnd('p', 'x');
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }

                return 0;
            }
        }
    }
}
