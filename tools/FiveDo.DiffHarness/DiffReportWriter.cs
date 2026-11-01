using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FiveDo.DiffHarness;

/// <summary>Writes JSON, Markdown, and HTML differential reports.</summary>
internal static class DiffReportWriter
{
    /// <summary>Writes every report format.</summary>
    /// <param name="outputDirectory">Destination directory.</param>
    /// <param name="report">Report to write.</param>
    /// <param name="name">File-name stem.</param>
    /// <returns>The JSON report path.</returns>
    public static string WriteAll(string outputDirectory, DiffReport report, string name)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, name + ".json");
        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(report, DiffJsonContext.Default.DiffReport),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(outputDirectory, name + ".md"),
            BuildMarkdown(report),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(outputDirectory, name + ".html"),
            BuildHtml(report),
            Encoding.UTF8);
        return jsonPath;
    }

    /// <summary>Builds the Markdown report.</summary>
    public static string BuildMarkdown(DiffReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var text = new StringBuilder();
        text.Append("# 5DO Differential Report - ").AppendLine(report.ManifestName);
        text.AppendLine();
        text.Append("Generated: ").AppendLine(report.GeneratedUtc);
        text.Append("Manifest: `").Append(report.ManifestPath).AppendLine("`");
        text.Append("Commit: `").Append(report.Provenance.GitCommit ?? "unknown").AppendLine("`");
        text.Append("CLI: `").Append(report.Provenance.CliPath ?? "unknown").AppendLine("`");
        text.Append("OperaCore: `").Append(report.Provenance.OperaCorePath ?? "unknown")
            .Append("` (").Append(report.Provenance.OperaCoreMd5 ?? "no md5").AppendLine(")");
        text.AppendLine();
        text.AppendLine("## Summary");
        text.AppendLine();
        text.AppendLine("| Result | Count |");
        text.AppendLine("|---|---:|");
        text.Append("| Match | ").Append(report.Summary.Matched).AppendLine(" |");
        text.Append("| Allowed divergence | ").Append(report.Summary.Allowed).AppendLine(" |");
        text.Append("| Diverged | ").Append(report.Summary.Diverged).AppendLine(" |");
        text.Append("| Frame offset | ").Append(report.Summary.Offset).AppendLine(" |");
        text.Append("| Error | ").Append(report.Summary.Errors).AppendLine(" |");
        text.Append("| Skipped | ").Append(report.Summary.Skipped).AppendLine(" |");
        text.Append("| **Verdict** | **").Append(report.Summary.Passed ? "PASS" : "FAIL").AppendLine("** |");
        text.AppendLine();
        text.AppendLine("| Title | Status | Frames | Checkpoints | Allowed fields | Diverged fields | PC delta | Error |");
        text.AppendLine("|---|---|---:|---:|---:|---:|---:|---|");
        foreach (var title in report.Titles)
        {
            text.Append("| ").Append(EscapeMarkdown(title.Name))
                .Append(" | ").Append(title.Status)
                .Append(" | ").Append(title.Frames.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(title.Checkpoints.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(title.AllowedCount.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(title.DivergedCount.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(title.MaxPcDelta?.ToString(CultureInfo.InvariantCulture) ?? "-")
                .Append(" | ").Append(EscapeMarkdown(title.Error ?? string.Empty))
                .AppendLine(" |");
        }

        var details = report.Titles
            .Where(title => title.Status is "Diverged" or "Allowed" or "Offset" or "Error")
            .ToArray();
        if (details.Length > 0)
        {
            text.AppendLine();
            text.AppendLine("## Details");
            foreach (var title in details)
            {
                text.AppendLine();
                text.Append("### ").AppendLine(title.Name);
                if (title.Error is not null)
                {
                    text.AppendLine();
                    text.AppendLine("```");
                    text.AppendLine(title.Error);
                    text.AppendLine("```");
                }

                foreach (var checkpoint in title.Checkpoints)
                {
                    var fields = checkpoint.Fields
                        .Where(field => !field.Match || field.AllowedReason is not null)
                        .ToArray();
                    if (fields.Length == 0)
                    {
                        continue;
                    }

                    text.AppendLine();
                    text.Append("Frame ").Append(checkpoint.Frame).AppendLine(":");
                    if (checkpoint.FiveDoPc is not null || checkpoint.OperaPc is not null)
                    {
                        text.Append("PC: ")
                            .Append(checkpoint.FiveDoPc is { } fiveDoPc ? "0x" + fiveDoPc.ToString("x8", CultureInfo.InvariantCulture) : "-")
                            .Append(" / ")
                            .Append(checkpoint.OperaPc is { } operaPc ? "0x" + operaPc.ToString("x8", CultureInfo.InvariantCulture) : "-");
                        if (checkpoint.PcDelta is { } delta)
                        {
                            text.Append(" (delta ").Append(delta).Append(')');
                        }

                        text.AppendLine();
                    }

                    if (checkpoint.FiveDoImage is not null || checkpoint.OperaImage is not null)
                    {
                        text.Append("Images: ");
                        if (checkpoint.FiveDoImage is not null)
                        {
                            text.Append("[5DO](").Append(FileUri(checkpoint.FiveDoImage)).Append(") ");
                        }

                        if (checkpoint.OperaImage is not null)
                        {
                            text.Append("[Opera](").Append(FileUri(checkpoint.OperaImage)).Append(')');
                        }

                        text.AppendLine();
                    }

                    text.AppendLine();
                    text.AppendLine("| Field | 5DO | Opera | Result |");
                    text.AppendLine("|---|---|---|---|");
                    foreach (var field in fields)
                    {
                        var result = field.Match
                            ? "match"
                            : field.AllowedReason is not null
                                ? "allowed: " + field.AllowedReason
                                : field.Diagnostic ?? "diverged";
                        text.Append("| ").Append(field.Field)
                            .Append(" | ").Append(field.FiveDo ?? "-")
                            .Append(" | ").Append(field.Opera ?? "-")
                            .Append(" | ").Append(EscapeMarkdown(result))
                            .AppendLine(" |");
                    }
                }
            }
        }

        return text.ToString();
    }

    /// <summary>Builds the HTML report.</summary>
    public static string BuildHtml(DiffReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.Append("<title>5DO Differential - ").Append(WebUtility.HtmlEncode(report.ManifestName))
            .AppendLine("</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font:14px/1.5 system-ui,sans-serif;margin:2rem;color:#1d1d1f;background:#fafafa}");
        html.AppendLine("h1{font-size:1.5rem;margin:0 0 .25rem}h2{margin-top:2rem;font-size:1.15rem}");
        html.AppendLine(".meta{color:#666;font-size:.85rem;margin-bottom:1.5rem}");
        html.AppendLine(".cards{display:flex;gap:1rem;flex-wrap:wrap;margin:1rem 0}");
        html.AppendLine(".card{background:#fff;border:1px solid #ddd;border-radius:8px;padding:.75rem 1rem;min-width:8rem}");
        html.AppendLine(".card b{display:block;font-size:1.4rem}");
        html.AppendLine("table{border-collapse:collapse;width:100%;background:#fff;margin:.5rem 0 1rem}");
        html.AppendLine("th,td{border:1px solid #ddd;padding:.4rem .55rem;text-align:left;vertical-align:top}");
        html.AppendLine("th{background:#f0f0f0;font-weight:600}td.num{text-align:right;font-variant-numeric:tabular-nums}");
        html.AppendLine(".Match{color:#137333;font-weight:600}.Allowed{color:#8a5a00;font-weight:600}");
        html.AppendLine(".Diverged{color:#b3261e;font-weight:600}.Error{color:#b3261e;font-weight:600}");
        html.AppendLine(".Offset{color:#8a5a00;font-weight:600}");
        html.AppendLine(".pass{background:#e6f4ea;border-color:#b7e1c1}.fail{background:#fce8e6;border-color:#f4c7c3}");
        html.AppendLine("code{font-family:ui-monospace,Consolas,monospace;font-size:.85em}");
        html.AppendLine("</style></head><body>");
        html.Append("<h1>5DO Differential - ").Append(WebUtility.HtmlEncode(report.ManifestName)).AppendLine("</h1>");
        html.Append("<div class=\"meta\">Generated ").Append(WebUtility.HtmlEncode(report.GeneratedUtc))
            .Append(" &middot; commit <code>").Append(WebUtility.HtmlEncode(report.Provenance.GitCommit ?? "unknown"))
            .Append("</code> &middot; manifest <code>").Append(WebUtility.HtmlEncode(report.ManifestPath))
            .Append("</code><br>CLI <code>").Append(WebUtility.HtmlEncode(report.Provenance.CliPath ?? "unknown"))
            .Append("</code> &middot; OperaCore <code>").Append(WebUtility.HtmlEncode(report.Provenance.OperaCorePath ?? "unknown"))
            .Append("</code> (").Append(WebUtility.HtmlEncode(report.Provenance.OperaCoreMd5 ?? "no md5"))
            .AppendLine(")</div>");

        html.AppendLine("<div class=\"cards\">");
        foreach ((var label, var value) in new[]
        {
            ("Match", report.Summary.Matched),
            ("Allowed", report.Summary.Allowed),
            ("Diverged", report.Summary.Diverged),
            ("Frame offset", report.Summary.Offset),
            ("Error", report.Summary.Errors),
            ("Checkpoints", report.Summary.Checkpoints),
        })
        {
            html.Append("<div class=\"card\"><b>").Append(value).Append("</b>")
                .Append(WebUtility.HtmlEncode(label)).AppendLine("</div>");
        }

        html.Append("<div class=\"card ").Append(report.Summary.Passed ? "pass" : "fail")
            .Append("\"><b>").Append(report.Summary.Passed ? "PASS" : "FAIL")
            .AppendLine("</b>verdict</div></div>");

        html.AppendLine("<h2>Titles</h2><table><thead><tr><th>Title</th><th>Status</th><th>Frames</th>"
            + "<th>Checkpoints</th><th>Allowed fields</th><th>Diverged fields</th><th>PC delta</th><th>Error</th></tr></thead><tbody>");
        foreach (var title in report.Titles)
        {
            html.Append("<tr><td>").Append(WebUtility.HtmlEncode(title.Name))
                .Append("</td><td class=\"").Append(WebUtility.HtmlEncode(title.Status)).Append("\">")
                .Append(WebUtility.HtmlEncode(title.Status))
                .Append("</td><td class=\"num\">").Append(title.Frames)
                .Append("</td><td class=\"num\">").Append(title.Checkpoints.Length)
                .Append("</td><td class=\"num\">").Append(title.AllowedCount)
                .Append("</td><td class=\"num\">").Append(title.DivergedCount)
                .Append("</td><td class=\"num\">").Append(title.MaxPcDelta?.ToString(CultureInfo.InvariantCulture) ?? "-")
                .Append("</td><td>").Append(WebUtility.HtmlEncode(title.Error ?? string.Empty))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");

        var details = report.Titles
            .Where(title => title.Status is "Diverged" or "Allowed" or "Offset" or "Error")
            .ToArray();
        if (details.Length > 0)
        {
            html.AppendLine("<h2>Details</h2>");
            foreach (var title in details)
            {
                html.Append("<h3>").Append(WebUtility.HtmlEncode(title.Name)).AppendLine("</h3>");
                if (title.Error is not null)
                {
                    html.Append("<pre>").Append(WebUtility.HtmlEncode(title.Error)).AppendLine("</pre>");
                }

                foreach (var checkpoint in title.Checkpoints)
                {
                    var fields = checkpoint.Fields
                        .Where(field => !field.Match || field.AllowedReason is not null)
                        .ToArray();
                    if (fields.Length == 0)
                    {
                        continue;
                    }

                    html.Append("<p><strong>Frame ").Append(checkpoint.Frame).AppendLine("</strong></p>");
                    if (checkpoint.FiveDoPc is not null || checkpoint.OperaPc is not null)
                    {
                        html.Append("<p>PC: <code>")
                            .Append(WebUtility.HtmlEncode(
                                checkpoint.FiveDoPc is { } fiveDoPc
                                    ? "0x" + fiveDoPc.ToString("x8", CultureInfo.InvariantCulture)
                                    : "-"))
                            .Append("</code> / <code>")
                            .Append(WebUtility.HtmlEncode(
                                checkpoint.OperaPc is { } operaPc
                                    ? "0x" + operaPc.ToString("x8", CultureInfo.InvariantCulture)
                                    : "-"))
                            .Append("</code>");
                        if (checkpoint.PcDelta is { } delta)
                        {
                            html.Append(" (delta ").Append(delta).Append(')');
                        }

                        html.AppendLine("</p>");
                    }

                    if (checkpoint.FiveDoImage is not null || checkpoint.OperaImage is not null)
                    {
                        html.Append("<p>Images: ");
                        if (checkpoint.FiveDoImage is not null)
                        {
                            html.Append("<a href=\"").Append(WebUtility.HtmlEncode(FileUri(checkpoint.FiveDoImage)))
                                .Append("\">5DO</a> ");
                        }

                        if (checkpoint.OperaImage is not null)
                        {
                            html.Append("<a href=\"").Append(WebUtility.HtmlEncode(FileUri(checkpoint.OperaImage)))
                                .Append("\">Opera</a>");
                        }

                        html.AppendLine("</p>");
                    }

                    html.AppendLine("<table><thead><tr><th>Field</th><th>5DO</th><th>Opera</th><th>Result</th></tr></thead><tbody>");
                    foreach (var field in fields)
                    {
                        var result = field.Match
                            ? "match"
                            : field.AllowedReason is not null
                                ? "allowed: " + field.AllowedReason
                                : field.Diagnostic ?? "diverged";
                        html.Append("<tr><td>").Append(WebUtility.HtmlEncode(field.Field))
                            .Append("</td><td><code>").Append(WebUtility.HtmlEncode(field.FiveDo ?? "-"))
                            .Append("</code></td><td><code>").Append(WebUtility.HtmlEncode(field.Opera ?? "-"))
                            .Append("</code></td><td>").Append(WebUtility.HtmlEncode(result))
                            .AppendLine("</td></tr>");
                    }

                    html.AppendLine("</tbody></table>");
                }
            }
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string FileUri(string path)
    {
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }
}
