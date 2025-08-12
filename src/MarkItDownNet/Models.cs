namespace MarkItDownNet;

/// <summary>Normalized bounding box.</summary>
public readonly record struct BoundingBox(double X, double Y, double Width, double Height)
{
    public static BoundingBox FromPdf(UglyToad.PdfPig.Core.PdfRectangle rect, double pageWidth, double pageHeight)
    {
        // PdfPig uses bottom-left origin; convert to top-left
        var x = rect.Left / pageWidth;
        var y = (pageHeight - rect.Top) / pageHeight;
        var w = rect.Width / pageWidth;
        var h = rect.Height / pageHeight;
        return new BoundingBox(x, y, w, h);
    }
}

/// <summary>Information about a page.</summary>
public record Page(int Number, double Width, double Height);

/// <summary>Line of text.</summary>
public record Line(int Page, string Text, BoundingBox BBox);

/// <summary>Single word.</summary>
public record Word(int Page, string Text, BoundingBox BBox);

/// <summary>Conversion result.</summary>
public record MarkItDownResult(
    string Markdown,
    IReadOnlyList<Page> Pages,
    IReadOnlyList<Line> Lines,
    IReadOnlyList<Word> Words);
