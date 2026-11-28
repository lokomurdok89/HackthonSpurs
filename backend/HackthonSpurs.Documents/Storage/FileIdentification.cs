using HackthonSpurs.Resources;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace HackthonSpurs.Documents.Storage;

public static class FileIdentification
{
    private const string Pdf = Media.Pdf;
    private const string Workbook = Media.Workbook;
    private const string Csv = Media.Csv;

    /// <summary>Determine an allowed media type from the staged bytes and processing mode.</summary>
    /// <param name="path">Private staged file path.</param>
    /// <param name="declared">Media type supplied by the uploader.</param>
    /// <param name="spur">Whether the SPUR image lane is selected.</param>
    public static string? Identify(string path, string declared, bool spur = false)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[8];
        var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        if (spur && (declared is Media.Png or Media.Jpeg or Media.Binary))
        {
            if (declared != Media.Jpeg && header[..length].StartsWith((ReadOnlySpan<byte>)[137, 80, 78, 71, 13, 10, 26, 10])) return Media.Png;
            if (declared != Media.Png && header[..length].StartsWith((ReadOnlySpan<byte>)[255, 216, 255])) return Media.Jpeg;
        }
        stream.Position = 0;
        string? actual;
        if (header[..length].StartsWith("%PDF-"u8))
            actual = length == 8 && char.IsAsciiDigit((char)header[5]) && header[6] == '.' && char.IsAsciiDigit((char)header[7]) ? Pdf : null;
        else if (header[..length].StartsWith("PK\x03\x04"u8))
            actual = IdentifyOffice(stream, spur);
        else if (spur && declared == Media.Plain)
            actual = IsPlainText(stream) ? declared : null;
        else
            actual = IsCsv(stream, declared == Csv) ? Csv : null;
        return declared == Media.Binary || declared == actual ? actual : null;
    }

    /// <summary>Check the ZIP entries and content types required for an XLSX workbook.</summary>
    /// <param name="stream">Input positioned at the start of the file.</param>
    /// <param name="spur">Also admit Word and PowerPoint for rendering.</param>
    private static string? IdentifyOffice(Stream stream, bool spur)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > Policy.MaxZipEntries || spur && (zip.Entries.Sum(e => (decimal)e.Length) > Policy.MaxExpandedBytes ||
                zip.Entries.Any(e => e.FullName.EndsWith(FileFormat.OfficeMacro, StringComparison.OrdinalIgnoreCase)))) return null;
            var types = zip.GetEntry(FileFormat.OfficeManifest);
            if (types is null || types.Length > Policy.MaxContentTypes) return null;
            using var entry = types.Open();
            using var reader = XmlReader.Create(entry, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = Policy.MaxContentTypes
            });
            XNamespace ns = FileFormat.ContentTypesNamespace;
            var root = XDocument.Load(reader).Root;
            if (root?.Name != ns + FileFormat.ContentTypes) return null;
            foreach (var (part, media) in new[] { (FileFormat.WorkbookPart, Workbook), (FileFormat.WordPart, OcrFormats.Word),
                (FileFormat.SlidesPart, OcrFormats.Slides) })
            {
                if (!spur && media != Workbook) continue;
                if (zip.GetEntry(part) != null && root.Elements(ns + FileFormat.Override).Any(e =>
                    (string?)e.Attribute(FileFormat.PartName) == "/" + part &&
                    (string?)e.Attribute(FileFormat.ContentType) == media + FileFormat.MainXml)) return media;
            }
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException) { return null; }
    }

    /// <summary>Recognize UTF-8 printable text without treating it as a formula or markup.</summary>
    /// <param name="stream">Original positioned at its start.</param>
    private static bool IsPlainText(Stream stream)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        try
        {
            int c;
            while ((c = reader.Read()) >= 0)
                if (char.IsControl((char)c) && c is not ('\r' or '\n' or '\t' or '\f')) return false;
            return true;
        }
        catch (DecoderFallbackException) { return false; }
    }

    // Validate CSV syntax without retaining rows or evaluating cell contents.
    // A single-column file has no identifying delimiter: require explicit text/csv.
    /// <summary>Validate UTF-8 CSV syntax without evaluating or retaining cell contents.</summary>
    /// <param name="stream">Input positioned at the start of the file.</param>
    /// <param name="explicitCsv">Whether the uploader declared text/csv.</param>
    private static bool IsCsv(Stream stream, bool explicitCsv)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        bool quoted = false, closed = false, fieldStart = true, comma = false, content = false;
        try
        {
            if (reader.Peek() == 0xfeff) reader.Read();
            int c;
            while ((c = reader.Read()) >= 0)
            {
                if (char.IsControl((char)c) && c is not ('\r' or '\n' or '\t')) return false;
                if (!char.IsWhiteSpace((char)c)) content = true;
                if (quoted)
                {
                    if (c != '"') continue;
                    if (reader.Peek() == '"') { reader.Read(); continue; }
                    quoted = false; closed = true;
                }
                else if (c is ',' or '\r' or '\n')
                {
                    comma |= c == ',';
                    fieldStart = true; closed = false;
                }
                else if (closed) return false;
                else if (c == '"' && fieldStart) { quoted = true; fieldStart = false; }
                else if (c == '"') return false;
                else fieldStart = false;
            }
            return content && !quoted && (comma || explicitCsv);
        }
        catch (DecoderFallbackException) { return false; }
    }
}
