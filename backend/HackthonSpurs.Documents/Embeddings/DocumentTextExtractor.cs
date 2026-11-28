using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace HackthonSpurs.Documents.Embeddings;

public sealed record ExtractedChunk(string Locator, string Text);

public static class DocumentTextExtractor
{
    private const int MaxChunkChars = 1000;
    private const int ChunkOverlapChars = 150;
    private const int RowsPerChunk = 25;
    private const int MinNativePdfPageChars = 20;

    public static async Task<IReadOnlyList<ExtractedChunk>> ExtractAsync(
        string path,
        string mediaType,
        SpurVisionOcrClient? visionOcr,
        CancellationToken ct)
    {
        if (!File.Exists(path))
            return [];

        return mediaType switch
        {
            "application/pdf" => await ExtractPdfAsync(path, visionOcr, ct),
            "text/csv" => ExtractCsv(path),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ExtractXlsx(path),
            "image/png" or "image/jpeg" => await ExtractImageAsync(path, mediaType, visionOcr, ct),
            _ => []
        };
    }

    private static async Task<List<ExtractedChunk>> ExtractPdfAsync(
        string path,
        SpurVisionOcrClient? visionOcr,
        CancellationToken ct)
    {
        var chunks = new List<ExtractedChunk>();
        var scannedPages = new List<int>();

        using (var document = PdfDocument.Open(path))
        {
            foreach (var page in document.GetPages())
            {
                var text = NormalizeText(ExtractPdfPageText(page));
                if (text.Length >= MinNativePdfPageChars)
                {
                    AddSplitChunks(chunks, $"page {page.Number}", text);
                }
                else
                {
                    scannedPages.Add(page.Number);
                }
            }
        }

        if (scannedPages.Count > 0 && visionOcr is { IsConfigured: true })
        {
            foreach (var pageNumber in scannedPages)
            {
                ct.ThrowIfCancellationRequested();
                var pngBytes = await RenderPdfPageToPngAsync(path, pageNumber, ct);
                if (pngBytes is null || pngBytes.Length == 0)
                    continue;

                var ocrText = NormalizeText(await visionOcr.ExtractTextFromImageAsync(pngBytes, "image/png", ct));
                if (!string.IsNullOrWhiteSpace(ocrText))
                    AddSplitChunks(chunks, $"page {pageNumber} (vision OCR)", ocrText);
            }
        }

        return chunks;
    }

    private static string ExtractPdfPageText(Page page)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .ToList();

        if (words.Count == 0)
            return page.Text ?? string.Empty;

        // PDF coordinates have Y=0 at the bottom of the page: sort top-to-bottom, then left-to-right.
        var ordered = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var lines = new List<List<Word>>();
        foreach (var word in ordered)
        {
            var baseline = word.BoundingBox.Bottom;
            var tolerance = Math.Max(3.5, word.BoundingBox.Height * 0.55);
            var line = lines.FirstOrDefault(l => Math.Abs(l[0].BoundingBox.Bottom - baseline) <= tolerance);
            if (line is null)
                lines.Add([word]);
            else
                line.Add(word);
        }

        var sb = new StringBuilder();
        foreach (var line in lines.OrderByDescending(l => l.Average(w => w.BoundingBox.Bottom)))
        {
            line.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
            for (var i = 0; i < line.Count; i++)
            {
                if (i > 0)
                {
                    var gap = line[i].BoundingBox.Left - line[i - 1].BoundingBox.Right;
                    sb.Append(gap > 18 ? " | " : " ");
                }
                sb.Append(line[i].Text);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static async Task<List<ExtractedChunk>> ExtractImageAsync(
        string path,
        string mediaType,
        SpurVisionOcrClient? visionOcr,
        CancellationToken ct)
    {
        var chunks = new List<ExtractedChunk>();
        if (visionOcr is not { IsConfigured: true })
            return chunks;

        var bytes = await File.ReadAllBytesAsync(path, ct);
        var ocrText = NormalizeText(await visionOcr.ExtractTextFromImageAsync(bytes, mediaType, ct));
        if (!string.IsNullOrWhiteSpace(ocrText))
            AddSplitChunks(chunks, "image (vision OCR)", ocrText);

        return chunks;
    }

    private static async Task<byte[]?> RenderPdfPageToPngAsync(string pdfPath, int pageNumber, CancellationToken ct)
    {
        var prefix = Path.Combine(Path.GetTempPath(), $"spurs-ocr-{Guid.NewGuid():N}");
        var pngPath = prefix + ".png";
        try
        {
            var psi = new ProcessStartInfo("pdftoppm")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-png");
            psi.ArgumentList.Add("-r");
            psi.ArgumentList.Add("150");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(pageNumber.ToString());
            psi.ArgumentList.Add("-l");
            psi.ArgumentList.Add(pageNumber.ToString());
            psi.ArgumentList.Add("-singlefile");
            psi.ArgumentList.Add(pdfPath);
            psi.ArgumentList.Add(prefix);

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0 || !File.Exists(pngPath))
                return null;

            return await File.ReadAllBytesAsync(pngPath, ct);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return null;
        }
        finally
        {
            if (File.Exists(pngPath))
                File.Delete(pngPath);
        }
    }

    private static void AddSplitChunks(List<ExtractedChunk> target, string baseLocator, string text)
    {
        var parts = SplitText(text, MaxChunkChars, ChunkOverlapChars);
        for (var i = 0; i < parts.Count; i++)
        {
            var locator = parts.Count == 1
                ? baseLocator
                : $"{baseLocator} (part {i + 1})";
            target.Add(new ExtractedChunk(locator, parts[i]));
        }
    }

    private static List<ExtractedChunk> ExtractCsv(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return [];

        var header = lines[0];
        if (lines.Count == 1)
            return [new ExtractedChunk("row 1", header)];

        var chunks = new List<ExtractedChunk>();
        for (var i = 1; i < lines.Count; i += RowsPerChunk)
        {
            var end = Math.Min(i + RowsPerChunk - 1, lines.Count - 1);
            var sb = new StringBuilder();
            sb.AppendLine(header);
            for (var r = i; r <= end; r++)
                sb.AppendLine(lines[r]);

            chunks.Add(new ExtractedChunk($"rows {i + 1}-{end + 1}", sb.ToString().Trim()));
        }
        return chunks;
    }

    private static List<ExtractedChunk> ExtractXlsx(string path)
    {
        var chunks = new List<ExtractedChunk>();
        using var zip = ZipFile.OpenRead(path);

        var sharedStrings = ReadSharedStrings(zip);
        var sheets = ResolveSheets(zip);

        foreach (var (name, entryPath) in sheets)
        {
            var entry = zip.GetEntry(entryPath);
            if (entry is null) continue;

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var rows = new List<(int RowNum, string Line)>();
            var rowIndex = 0;
            foreach (var row in doc.Descendants(ns + "row"))
            {
                rowIndex++;
                var rowAttr = row.Attribute("r")?.Value;
                var rowNum = int.TryParse(rowAttr, out var parsed) ? parsed : rowIndex;

                var values = new List<string>();
                foreach (var cell in row.Elements(ns + "c"))
                {
                    var cellValue = ReadCellValue(cell, ns, sharedStrings);
                    if (!string.IsNullOrWhiteSpace(cellValue))
                        values.Add(cellValue);
                }

                if (values.Count > 0)
                    rows.Add((rowNum, string.Join(" | ", values)));
            }

            if (rows.Count == 0) continue;

            var header = rows[0].Line;
            if (rows.Count == 1)
            {
                chunks.Add(new ExtractedChunk($"sheet {name} · row {rows[0].RowNum}", header));
                continue;
            }

            for (var i = 1; i < rows.Count; i += RowsPerChunk)
            {
                var end = Math.Min(i + RowsPerChunk - 1, rows.Count - 1);
                var sb = new StringBuilder();
                sb.AppendLine($"Sheet: {name}");
                sb.AppendLine(header);
                for (var r = i; r <= end; r++)
                    sb.AppendLine($"Row {rows[r].RowNum}: {rows[r].Line}");

                chunks.Add(new ExtractedChunk(
                    $"sheet {name} · rows {rows[i].RowNum}-{rows[end].RowNum}",
                    sb.ToString().Trim()));
            }
        }

        return chunks;
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var list = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return list;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        foreach (var si in doc.Descendants(ns + "si"))
        {
            var text = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
            list.Add(text);
        }
        return list;
    }

    private static List<(string Name, string EntryPath)> ResolveSheets(ZipArchive zip)
    {
        var result = new List<(string Name, string EntryPath)>();
        var wbEntry = zip.GetEntry("xl/workbook.xml");
        var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (wbEntry is null || relsEntry is null)
        {
            foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                result.Add((Path.GetFileNameWithoutExtension(e.Name), e.FullName));
            return result;
        }

        var relMap = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var relStream = relsEntry.Open())
        {
            var relDoc = XDocument.Load(relStream);
            foreach (var rel in relDoc.Descendants().Where(e => e.Name.LocalName == "Relationship"))
            {
                var id = rel.Attribute("Id")?.Value;
                var target = rel.Attribute("Target")?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                {
                    var normalized = target.TrimStart('/');
                    if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                        normalized = "xl/" + normalized;
                    relMap[id] = normalized;
                }
            }
        }

        using (var wbStream = wbEntry.Open())
        {
            var wbDoc = XDocument.Load(wbStream);
            XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            foreach (var sheet in wbDoc.Descendants().Where(e => e.Name.LocalName == "sheet"))
            {
                var name = sheet.Attribute("name")?.Value ?? "Sheet";
                var rId = sheet.Attribute(rNs + "id")?.Value;
                if (!string.IsNullOrEmpty(rId) && relMap.TryGetValue(rId, out var target))
                    result.Add((name, target));
            }
        }

        return result;
    }

    private static string ReadCellValue(XElement cell, XNamespace ns, List<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
            return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value)).Trim();

        var raw = cell.Element(ns + "v")?.Value?.Trim() ?? "";
        if (type == "s" && int.TryParse(raw, out var idx) && idx >= 0 && idx < sharedStrings.Count)
            return sharedStrings[idx].Trim();

        return raw;
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder(input.Length);
        var blankStreak = 0;

        foreach (var rawLine in lines)
        {
            var cleaned = CollapseHorizontalWhitespace(rawLine);
            if (cleaned.Length == 0)
            {
                blankStreak++;
                if (sb.Length > 0 && blankStreak == 1)
                    sb.Append('\n');
                continue;
            }
            blankStreak = 0;
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(cleaned);
        }

        return sb.ToString().Trim();
    }

    private static string CollapseHorizontalWhitespace(string line)
    {
        var sb = new StringBuilder(line.Length);
        var prevSpace = false;
        foreach (var ch in line)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    private static List<string> SplitText(string text, int maxChars, int overlap)
    {
        if (text.Length <= maxChars)
            return [text];

        var parts = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(maxChars, text.Length - start);
            if (start + length < text.Length)
            {
                var lastSpace = text.LastIndexOf(' ', start + length - 1, Math.Min(200, length));
                if (lastSpace > start + maxChars / 2)
                    length = lastSpace - start;
            }

            parts.Add(text.Substring(start, length).Trim());
            if (start + length >= text.Length)
                break;

            start = Math.Max(start + 1, start + length - overlap);
        }
        return parts;
    }
}
