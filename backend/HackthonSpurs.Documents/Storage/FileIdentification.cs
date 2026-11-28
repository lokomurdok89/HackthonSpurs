using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace HackthonSpurs.Documents.Storage;

public static class FileIdentification
{
    private const string Pdf = "application/pdf";
    private const string Workbook = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Csv = "text/csv";

    public static string? Identify(string path, string declared)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[8];
        var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        string? actual;
        if (header[..length].StartsWith("%PDF-"u8))
            actual = length == 8 && char.IsAsciiDigit((char)header[5]) && header[6] == '.' && char.IsAsciiDigit((char)header[7]) ? Pdf : null;
        else if (header[..length].StartsWith("PK\x03\x04"u8))
            actual = IsWorkbook(stream) ? Workbook : null;
        else
            actual = IsCsv(stream, declared == Csv) ? Csv : null;
        return declared == "application/octet-stream" || declared == actual ? actual : null;
    }

    private static bool IsWorkbook(Stream stream)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > 4096) return false;
            var types = zip.GetEntry("[Content_Types].xml");
            if (types is null || types.Length > 262144 || zip.GetEntry("xl/workbook.xml") is null) return false;
            using var entry = types.Open();
            using var reader = XmlReader.Create(entry, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 262144
            });
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
            var root = XDocument.Load(reader).Root;
            return root?.Name == ns + "Types" && root.Elements(ns + "Override").Any(e =>
                (string?)e.Attribute("PartName") == "/xl/workbook.xml" &&
                (string?)e.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException) { return false; }
    }

    // Validate CSV syntax without retaining rows or evaluating cell contents.
    // A single-column file has no identifying delimiter: require explicit text/csv.
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
