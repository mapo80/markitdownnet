using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

static class ReportHtml
{
    public static string Build(List<BenchResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");
        sb.AppendLine("<h2>Timing</h2><table border='1'><tr><th>Mode</th><th>avg±std (ms)</th><th>p50</th><th>p90</th><th>p95</th></tr>");
        foreach (var r in results)
            sb.AppendLine($"<tr><td>{r.Mode}</td><td>{r.AvgMs:F1}±{r.StdMs:F1}</td><td>{r.P50Ms:F1}</td><td>{r.P90Ms:F1}</td><td>{r.P95Ms:F1}</td></tr>");
        sb.AppendLine("</table>");
        var pre = results.FirstOrDefault(r=>r.Mode=="pre");
        var post1S = results.FirstOrDefault(r=>r.Mode=="post-1S");
        var post2 = results.FirstOrDefault(r=>r.Mode=="post-2");
        var pyHot = results.FirstOrDefault(r=>r.Mode=="python-hot");
        var pyCold = results.FirstOrDefault(r=>r.Mode=="python-cold");
        if (post1S!=null && pre!=null)
        {
            var delta = (post1S.AvgMs - pre.AvgMs)/pre.AvgMs*100.0;
            sb.AppendLine($"<p>post-1S vs pre: {delta:F1}%</p>");
        }
        if (post2!=null && post1S!=null)
        {
            var delta = (post2.AvgMs - post1S.AvgMs)/post1S.AvgMs*100.0;
            sb.AppendLine($"<p>post-2 vs post-1S: {delta:F1}%</p>");
        }
        if (pyHot!=null && pyCold!=null)
        {
            var delta = (pyHot.AvgMs - pyCold.AvgMs)/pyCold.AvgMs*100.0;
            sb.AppendLine($"<p>python-hot vs python-cold: {delta:F1}%</p>");
        }
        if (pyHot!=null && post2!=null)
        {
            var delta = (post2.AvgMs - pyHot.AvgMs)/pyHot.AvgMs*100.0;
            sb.AppendLine($"<p>post-2 vs python-hot: {delta:F1}%</p>");
        }
        if (pyHot!=null)
        {
            bool refHasTable = pyHot.Similarity?.Tables > 0;
            if (refHasTable)
            {
                sb.AppendLine("<h2>Quality vs python-hot</h2><table border='1'><tr><th>Mode</th><th>CER</th><th>Token-F1</th><th>line_F1</th><th>tables_count</th><th>line_count</th><th>list_items</th><th>max_list_depth</th><th>table_cell_F1</th></tr>");
                foreach(var r in results.Where(r=>r.Mode=="pre" || r.Mode=="post-1S" || r.Mode=="post-2"))
                {
                    var s=r.Similarity;
                    if (s!=null)
                    {
                        var f1 = s.TableCellF1.HasValue ? s.TableCellF1.Value.ToString("F3") : "—";
                        sb.AppendLine($"<tr><td>{r.Mode}</td><td>{s.Cer:F3}</td><td>{s.F1:F3}</td><td>{s.LineF1:F3}</td><td>{s.Tables}</td><td>{s.LineCount}</td><td>{s.ListItems}</td><td>{s.MaxListDepth}</td><td>{f1}</td></tr>");
                    }
                }
                sb.AppendLine("</table>");
            }
            else
            {
                sb.AppendLine("<h2>Quality vs python-hot</h2><table border='1'><tr><th>Mode</th><th>CER</th><th>Token-F1</th><th>line_F1</th><th>tables_count</th><th>line_count</th><th>list_items</th><th>max_list_depth</th><th>pipes_lines</th><th>median_pipes</th><th>max_pipes</th></tr>");
                foreach(var r in results.Where(r=>r.Mode=="pre" || r.Mode=="post-1S" || r.Mode=="post-2"))
                {
                    var s=r.Similarity;
                    if (s!=null)
                        sb.AppendLine($"<tr><td>{r.Mode}</td><td>{s.Cer:F3}</td><td>{s.F1:F3}</td><td>{s.LineF1:F3}</td><td>{s.Tables}</td><td>{s.LineCount}</td><td>{s.ListItems}</td><td>{s.MaxListDepth}</td><td>{s.PipeLines}</td><td>{s.MedianPipesPerLine:F1}</td><td>{s.MaxPipesPerLine}</td></tr>");
                }
                sb.AppendLine("</table>");
            }
            var pyNorm = File.ReadAllText(pyHot.NormOutput);
            var preNorm = pre?.NormOutput;
            var post1SNorm = post1S?.NormOutput;
            var post2Norm = post2?.NormOutput;
            if (preNorm!=null)
            {
                sb.AppendLine("<h3>pre vs python-hot</h3><table border='1'><tr><td><pre>");
                sb.Append(WebUtility.HtmlEncode(File.ReadAllText(preNorm)));
                sb.AppendLine("</pre></td><td><pre>");
                sb.Append(WebUtility.HtmlEncode(pyNorm));
                sb.AppendLine("</pre></td></tr></table>");
            }
            if (post1SNorm!=null)
            {
                sb.AppendLine("<h3>post-1S vs python-hot</h3><table border='1'><tr><td><pre>");
                sb.Append(WebUtility.HtmlEncode(File.ReadAllText(post1SNorm)));
                sb.AppendLine("</pre></td><td><pre>");
                sb.Append(WebUtility.HtmlEncode(pyNorm));
                sb.AppendLine("</pre></td></tr></table>");
            }
            if (post2Norm!=null)
            {
                sb.AppendLine("<h3>post-2 vs python-hot</h3><table border='1'><tr><td><pre>");
                sb.Append(WebUtility.HtmlEncode(File.ReadAllText(post2Norm)));
                sb.AppendLine("</pre></td><td><pre>");
                sb.Append(WebUtility.HtmlEncode(pyNorm));
                sb.AppendLine("</pre></td></tr></table>");
            }
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
