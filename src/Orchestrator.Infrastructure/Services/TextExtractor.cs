using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Orchestrator.Infrastructure.Services;

public interface ITextExtractor
{
    string Extract(byte[] bytes, string? mimeType, string? fileName);
}

public class TextExtractor : ITextExtractor
{
    public string Extract(byte[] bytes, string? mimeType, string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        if (mimeType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true
            || ext is ".txt" or ".md" or ".csv")
        {
            return Encoding.UTF8.GetString(bytes);
        }

        if (mimeType == "application/pdf" || ext == ".pdf")
        {
            return ExtractPdf(bytes);
        }

        if (mimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" || ext == ".docx")
        {
            return ExtractDocx(bytes);
        }

        // Fallback: treat as UTF-8 text
        return Encoding.UTF8.GetString(bytes);
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
            sb.AppendLine(para.InnerText);
        return sb.ToString();
    }
}
