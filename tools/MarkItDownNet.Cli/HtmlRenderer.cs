using System.Text;
using System.Text.Json;
using System.IO;

namespace MarkItDownNet.Cli;

public static class HtmlRenderer
{
    public static string Build(object runConfig, Dictionary<string, DatasetAgg> byDataset, DatasetAgg global, List<MdFileResult> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>MD Parity Report</h1>");
        sb.AppendLine("<h2>Run config</h2>");
        sb.AppendLine($"<pre>{JsonSerializer.Serialize(runConfig)}</pre>");
        sb.AppendLine("<h2>Text metrics</h2>");
        sb.AppendLine("<table><tr><th>scope</th><th>CER</th><th>Token-F1</th><th>Line-F1</th><th>n_files</th></tr>");
        sb.AppendLine($"<tr><td>global</td><td>{global.Cer:F3}</td><td>{global.TokenF1:F3}</td><td>{global.LineF1:F3}</td><td>{global.n_files}</td></tr>");
        foreach (var kv in byDataset)
        {
            var a = kv.Value;
            sb.AppendLine($"<tr><td>{kv.Key}</td><td>{a.Cer:F3}</td><td>{a.TokenF1:F3}</td><td>{a.LineF1:F3}</td><td>{a.n_files}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine("<h2>Structure metrics</h2>");
        sb.AppendLine("<table><tr><th>scope</th><th>H1</th><th>H2</th><th>H3</th><th>list_items</th><th>max_list_depth</th><th>tables_count</th><th>pipes_lines_avg</th></tr>");
        sb.AppendLine($"<tr><td>global</td><td>{global.H1}</td><td>{global.H2}</td><td>{global.H3}</td><td>{global.ListItems}</td><td>{global.MaxListDepth}</td><td>{global.Tables}</td><td>{global.PipeLinesAvg:F1}</td></tr>");
        foreach (var kv in byDataset)
        {
            var a = kv.Value;
            sb.AppendLine($"<tr><td>{kv.Key}</td><td>{a.H1}</td><td>{a.H2}</td><td>{a.H3}</td><td>{a.ListItems}</td><td>{a.MaxListDepth}</td><td>{a.Tables}</td><td>{a.PipeLinesAvg:F1}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine("<h2>Sample diffs (worst 3 / best 3)</h2>");
        sb.AppendLine("<ol>");
        foreach (var f in files.OrderBy(x => x.metrics.text.token_f1).Take(3))
            sb.AppendLine($"<li>worst {f.dataset}/{Path.GetFileName(f.paths.ref_md)} <a href='{f.paths.ref_md}'>ref</a> <a href='{f.paths.hyp_md}'>hyp</a></li>");
        foreach (var f in files.OrderByDescending(x => x.metrics.text.token_f1).Take(3))
            sb.AppendLine($"<li>best {f.dataset}/{Path.GetFileName(f.paths.ref_md)} <a href='{f.paths.ref_md}'>ref</a> <a href='{f.paths.hyp_md}'>hyp</a></li>");
        sb.AppendLine("</ol>");
        return sb.ToString();
    }
}
