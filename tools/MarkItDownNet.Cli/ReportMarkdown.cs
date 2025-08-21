using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

static class ReportMarkdown
{
    public static string Build(List<BenchResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Timing summary (md_ms)");
        sb.AppendLine("| mode | avg md_ms | std md_ms |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var r in results)
            sb.AppendLine($"| {r.Mode} | {r.AvgMs:F1} | {r.StdMs:F1} |");

        var pre = results.FirstOrDefault(r=>r.Mode=="pre");
        var post1S = results.FirstOrDefault(r=>r.Mode=="post-1S");
        var post2 = results.FirstOrDefault(r=>r.Mode=="post-2");
        var pyHot = results.FirstOrDefault(r=>r.Mode=="python-hot");
        if (pre!=null && post1S!=null)
        {
            var delta = (post1S.AvgMs - pre.AvgMs) / pre.AvgMs * 100.0;
            sb.AppendLine($"\npost-1S vs pre: {delta:F1}%");
        }
        if (post2!=null && post1S!=null)
        {
            var delta = (post2.AvgMs - post1S.AvgMs) / post1S.AvgMs * 100.0;
            sb.AppendLine($"\npost-2 vs post-1S: {delta:F1}%");
        }
        if (pyHot!=null && post2!=null)
        {
            var delta = (post2.AvgMs - pyHot.AvgMs) / pyHot.AvgMs * 100.0;
            sb.AppendLine($"\npost-2 vs python-hot: {delta:F1}%");
        }

        sb.AppendLine("\n## Quality vs python-hot");
        if (pyHot!=null && pyHot.Similarity?.Tables > 0)
        {
            sb.AppendLine("| mode | CER | Token-F1 | line_F1 | tables_count | line_count | list_items | table_cell_F1 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var r in results.Where(r=>r.Mode=="pre" || r.Mode=="post-1S" || r.Mode=="post-2"))
            {
                var s = r.Similarity;
                if (s != null)
                {
                    var f1 = s.TableCellF1.HasValue ? s.TableCellF1.Value.ToString("F3") : "—";
                    sb.AppendLine($"| {r.Mode} | {s.Cer:F3} | {s.F1:F3} | {s.LineF1:F3} | {s.Tables} | {s.LineCount} | {s.ListItems} | {f1} |");
                }
            }
        }
        else if (pyHot!=null)
        {
            sb.AppendLine("| mode | CER | Token-F1 | line_F1 | tables_count | line_count | list_items | pipes_lines | median_pipes | max_pipes |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var r in results.Where(r=>r.Mode=="pre" || r.Mode=="post-1S" || r.Mode=="post-2"))
            {
                var s = r.Similarity;
                if (s != null)
                    sb.AppendLine($"| {r.Mode} | {s.Cer:F3} | {s.F1:F3} | {s.LineF1:F3} | {s.Tables} | {s.LineCount} | {s.ListItems} | {s.PipeLines} | {s.MedianPipesPerLine:F1} | {s.MaxPipesPerLine} |");
            }
        }

        if (pyHot!=null)
        {
            var counts = results.Where(r=>r.Mode=="pre" || r.Mode=="post-1S" || r.Mode=="post-2" || r.Mode=="python-hot")
                .ToDictionary(r=>r.Mode, r=>r.Similarity?.Tables ?? 0);
            if (counts.Values.All(c=>c==0))
            {
                sb.AppendLine("\n**Tables:** none detected in this sample (all modes).");
            }
            else
            {
                var list = string.Join(", ", counts.Select(kv=>$"{kv.Key}={kv.Value}"));
                var tcF1 = results.FirstOrDefault(r=>r.Mode=="post-2")?.Similarity?.TableCellF1;
                if ((pyHot.Similarity?.Tables ?? 0) > 0 && tcF1 != null)
                    sb.AppendLine($"\n**Tables:** {list} (table_cell_F1(post-2)={tcF1:F3})");
                else
                    sb.AppendLine($"\n**Tables:** {list}");
            }

            sb.AppendLine("\n### Observations");
            var preS = results.FirstOrDefault(r=>r.Mode=="pre")?.Similarity;
            var post2S = results.FirstOrDefault(r=>r.Mode=="post-2")?.Similarity;
            var post1SSim = results.FirstOrDefault(r=>r.Mode=="post-1S")?.Similarity;
            if (preS!=null && post2S!=null)
            {
                sb.AppendLine($"- CER pre {preS.Cer:F3} vs post-2 {post2S.Cer:F3}");
                if (post1SSim!=null)
                    sb.AppendLine($"- line_F1 post-2 {post2S.LineF1:F3} vs post-1S {post1SSim.LineF1:F3}");
                var preAvg = results.First(r=>r.Mode=="pre").AvgMs;
                var postAvg = results.First(r=>r.Mode=="post-2").AvgMs;
                sb.AppendLine($"- post-2 overhead vs pre {((postAvg-preAvg)/preAvg*100):F1}%");
            }
        }
        return sb.ToString();
    }
}
