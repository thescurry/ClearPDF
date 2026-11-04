using System.Text;

namespace ClearPDF.Mac.Tests;

internal static class MultiPagePdfWriter
{
    public static string Write(string path, int pageCount)
    {
        if (pageCount < 2)
            throw new ArgumentOutOfRangeException(nameof(pageCount));

        var fontId = 3 + (pageCount * 2);
        var bodies = new string[fontId];
        bodies[0] = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{3 + i} 0 R"));
        bodies[1] = $"2 0 obj\n<< /Type /Pages /Count {pageCount} /Kids [ {kids} ] >>\nendobj\n";

        for (var i = 0; i < pageCount; i++)
        {
            var pageId = 3 + i;
            var contentId = 3 + pageCount + i;
            bodies[pageId - 1] =
                $"{pageId} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [ 0 0 612 792 ] " +
                $"/Contents {contentId} 0 R /Resources << /Font << /F1 {fontId} 0 R >> >> >>\nendobj\n";

            var stream = $"BT /F1 24 Tf 72 720 Td (Page {i + 1}) Tj ET\n";
            bodies[contentId - 1] =
                $"{contentId} 0 obj\n<< /Length {stream.Length} >>\nstream\n{stream}endstream\nendobj\n";
        }

        bodies[fontId - 1] = $"{fontId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n";

        var header = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        var encoded = bodies.Select(b => Encoding.ASCII.GetBytes(b)).ToArray();
        var offsets = new int[bodies.Length];
        var cursor = header.Length;
        for (var i = 0; i < encoded.Length; i++)
        {
            offsets[i] = cursor;
            cursor += encoded[i].Length;
        }

        var xref = new StringBuilder();
        xref.Append("xref\n");
        xref.Append($"0 {bodies.Length + 1}\n");
        xref.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            xref.Append(offset.ToString("D10")).Append(" 00000 n \n");
        xref.Append("trailer\n");
        xref.Append($"<< /Size {bodies.Length + 1} /Root 1 0 R >>\n");
        xref.Append("startxref\n");
        xref.Append(cursor);
        xref.Append("\n%%EOF\n");

        using var streamOut = File.Create(path);
        streamOut.Write(header);
        foreach (var part in encoded)
            streamOut.Write(part);
        streamOut.Write(Encoding.ASCII.GetBytes(xref.ToString()));
        return path;
    }
}
