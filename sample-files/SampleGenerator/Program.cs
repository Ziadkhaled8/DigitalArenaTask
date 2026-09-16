using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using System.Drawing;

var currentDir = Directory.GetCurrentDirectory();
var outputDir = Directory.Exists(Path.Combine(currentDir, "sample-files"))
    ? Path.Combine(currentDir, "sample-files")
    : Path.GetFullPath(Path.Combine(currentDir, ".."));

// 1. Text-only PDF
CreateTextOnlyPdf(Path.Combine(outputDir, "text-only.pdf"));

// 2. Text with images PDF (uses actual embedded bitmap images)
CreateTextWithImagesPdf(Path.Combine(outputDir, "text-with-images.pdf"));

// 3. Scanned image-only PDF (full-page bitmap, no text layer)
CreateScannedImageOnlyPdf(Path.Combine(outputDir, "scanned-image-only.pdf"));

Console.WriteLine("All sample PDFs created successfully!");

static void CreateTextOnlyPdf(string path)
{
    var document = new PdfDocument();
    document.Info.Title = "Text-Only Sample Document";
    document.Info.Author = "Sample Generator";

    // Page 1
    var page1 = document.AddPage();
    var gfx = XGraphics.FromPdfPage(page1);

    var titleFont = new XFont("Arial", 24, XFontStyle.Bold);
    var headingFont = new XFont("Arial", 18, XFontStyle.Bold);
    var bodyFont = new XFont("Arial", 12, XFontStyle.Regular);

    double y = 50;
    gfx.DrawString("Document Conversion Service", titleFont, XBrushes.Black, new XRect(50, y, page1.Width - 100, 40), XStringFormats.TopLeft);
    y += 50;

    gfx.DrawString("1. Introduction", headingFont, XBrushes.Black, new XRect(50, y, page1.Width - 100, 30), XStringFormats.TopLeft);
    y += 40;

    var paragraphs = new[]
    {
        "This document serves as a sample text-only PDF for testing the Document Conversion " +
        "and Splitting Service. It contains multiple paragraphs of text organized with headings " +
        "and body content to validate the parsing and conversion pipeline.",

        "The service is designed to accept PDF documents as input and convert them into " +
        "either HTML or DOCX format. The conversion process is rule-based and deterministic, " +
        "meaning text is carried across faithfully without any AI-based interpretation.",

        "When the converted output exceeds a configurable size limit (default: 2 MB), the " +
        "service automatically splits it into multiple ordered parts. Each part is clearly " +
        "sequenced so the full set can be reassembled downstream."
    };

    foreach (var para in paragraphs)
    {
        var tf = new XTextFormatter(gfx);
        var rect = new XRect(50, y, page1.Width - 100, 80);
        tf.DrawString(para, bodyFont, XBrushes.Black, rect, XStringFormats.TopLeft);
        y += 90;
    }

    gfx.DrawString("2. Architecture", headingFont, XBrushes.Black, new XRect(50, y, page1.Width - 100, 30), XStringFormats.TopLeft);
    y += 40;

    var archParagraphs = new[]
    {
        "The system follows Clean Architecture principles with four layers: Domain (entities, " +
        "enums, exceptions, interfaces), Application (orchestration, DTOs, queries), " +
        "Infrastructure (parsers, renderers, splitter, validator, storage, persistence), " +
        "and API (controllers, middleware, startup configuration).",

        "Dependencies flow inward: API references Infrastructure and Application, " +
        "Infrastructure implements interfaces defined in Domain. Domain has zero external " +
        "dependencies, keeping the business logic pure and testable."
    };

    foreach (var para in archParagraphs)
    {
        var tf = new XTextFormatter(gfx);
        var rect = new XRect(50, y, page1.Width - 100, 80);
        tf.DrawString(para, bodyFont, XBrushes.Black, rect, XStringFormats.TopLeft);
        y += 90;
    }

    // Page 2
    var page2 = document.AddPage();
    gfx = XGraphics.FromPdfPage(page2);
    y = 50;

    gfx.DrawString("3. Conversion Pipeline", headingFont, XBrushes.Black, new XRect(50, y, page2.Width - 100, 30), XStringFormats.TopLeft);
    y += 40;

    var pipelineParagraphs = new[]
    {
        "The conversion pipeline operates through several stages: document intake, parsing, " +
        "format routing, rendering, splitting (if needed), and validation. Each stage " +
        "transitions the job through well-defined status states.",

        "The format router determines which output formats are valid based on document " +
        "content. If the source PDF contains image XObjects, only HTML output is permitted. " +
        "DOCX requests for such documents are rejected at intake with a fail-fast approach.",

        "Splitting uses a sequential greedy accumulation algorithm. Elements are added to " +
        "the current part in document order, and the actual rendered size is measured after " +
        "each addition. Elements are never split or reordered.",

        "Post-split validation ensures content integrity by comparing a canonical hash of " +
        "the original document against the concatenated content of all parts. This approach " +
        "avoids the pitfall of comparing raw byte sizes, which would be invalid due to " +
        "per-part wrapper overhead differences."
    };

    foreach (var para in pipelineParagraphs)
    {
        var tf = new XTextFormatter(gfx);
        var rect = new XRect(50, y, page2.Width - 100, 80);
        tf.DrawString(para, bodyFont, XBrushes.Black, rect, XStringFormats.TopLeft);
        y += 90;
    }

    gfx.DrawString("4. Error Handling", headingFont, XBrushes.Black, new XRect(50, y, page2.Width - 100, 30), XStringFormats.TopLeft);
    y += 40;

    var errorParagraphs = new[]
    {
        "The service handles several types of exceptions: UnsupportedFormatException for " +
        "invalid format requests, CorruptedFileException for unreadable PDFs, " +
        "ScannedDocumentException for image-only documents, and EmptyDocumentException " +
        "for documents with no extractable content.",

        "All exceptions extend a common DocumentProcessingException base class with a " +
        "typed ErrorCode property, enabling the orchestrator to catch one type and route " +
        "appropriately. Errors are persisted as part of the job history."
    };

    foreach (var para in errorParagraphs)
    {
        var tf = new XTextFormatter(gfx);
        var rect = new XRect(50, y, page2.Width - 100, 80);
        tf.DrawString(para, bodyFont, XBrushes.Black, rect, XStringFormats.TopLeft);
        y += 90;
    }

    document.Save(path);
    Console.WriteLine($"Created: {Path.GetFileName(path)}");
}

static void CreateTextWithImagesPdf(string path)
{
    var document = new PdfDocument();
    document.Info.Title = "Text with Images Sample";

    var page = document.AddPage();
    var gfx = XGraphics.FromPdfPage(page);

    var titleFont = new XFont("Arial", 20, XFontStyle.Bold);
    var bodyFont = new XFont("Arial", 12, XFontStyle.Regular);

    double y = 50;
    gfx.DrawString("Report with Visual Elements", titleFont, XBrushes.Black, new XRect(50, y, page.Width - 100, 30), XStringFormats.TopLeft);
    y += 50;

    var tf = new XTextFormatter(gfx);
    tf.DrawString(
        "This document contains both text and embedded images to test the format " +
        "routing logic. Since it contains image XObjects, only HTML conversion should " +
        "be permitted. A DOCX conversion request should be rejected.",
        bodyFont, XBrushes.Black, new XRect(50, y, page.Width - 100, 60), XStringFormats.TopLeft);
    y += 80;

    // Create and embed an actual bitmap image (a colored logo)
    var logoImage = CreateBitmapImage(200, 100, (bmp) =>
    {
        for (int px = 0; px < bmp.Width; px++)
            for (int py = 0; py < bmp.Height; py++)
                bmp.SetPixel(px, py, System.Drawing.Color.FromArgb(41, 128, 185));
        // Draw white text area in center
        for (int px = 40; px < 160; px++)
            for (int py = 30; py < 70; py++)
                bmp.SetPixel(px, py, System.Drawing.Color.White);
    });
    gfx.DrawImage(logoImage, 50, y, 200, 100);
    y += 120;

    tf = new XTextFormatter(gfx);
    tf.DrawString(
        "The image above represents a company logo embedded in the PDF as a proper " +
        "XObject Image. The conversion service should detect this and route accordingly.",
        bodyFont, XBrushes.Black, new XRect(50, y, page.Width - 100, 60), XStringFormats.TopLeft);
    y += 80;

    // Create and embed a chart-like bitmap image
    var chartImage = CreateBitmapImage(400, 150, (bmp) =>
    {
        // Light gray background
        for (int px = 0; px < bmp.Width; px++)
            for (int py = 0; py < bmp.Height; py++)
                bmp.SetPixel(px, py, System.Drawing.Color.LightGray);

        // Draw colored bars
        var barColors = new[] {
            System.Drawing.Color.SteelBlue,
            System.Drawing.Color.Coral,
            System.Drawing.Color.MediumSeaGreen,
            System.Drawing.Color.Gold
        };
        int[] barHeights = { 120, 80, 100, 60 };
        for (int i = 0; i < 4; i++)
        {
            for (int px = 20 + i * 95; px < 20 + i * 95 + 75 && px < bmp.Width; px++)
                for (int py = 150 - barHeights[i]; py < 150 && py < bmp.Height; py++)
                    if (px >= 0 && py >= 0)
                        bmp.SetPixel(px, py, barColors[i]);
        }
    });
    gfx.DrawImage(chartImage, 50, y, 400, 150);
    y += 170;

    tf = new XTextFormatter(gfx);
    tf.DrawString(
        "The chart above shows quarterly performance data. This type of visual content " +
        "demonstrates why image preservation is important in document conversion.",
        bodyFont, XBrushes.Black, new XRect(50, y, page.Width - 100, 60), XStringFormats.TopLeft);

    document.Save(path);
    Console.WriteLine($"Created: {Path.GetFileName(path)}");
}

static void CreateScannedImageOnlyPdf(string path)
{
    var document = new PdfDocument();
    document.Info.Title = "Scanned Document";

    var page = document.AddPage();
    var gfx = XGraphics.FromPdfPage(page);

    // Create a full-page bitmap that simulates a scanned document
    // This embeds as a proper /XObject /Image with NO text layer
    int width = (int)page.Width;
    int height = (int)page.Height;
    var scannedImage = CreateBitmapImage(width, height, (bmp) =>
    {
        var rng = new Random(123);

        // Off-white background (simulating paper)
        for (int px = 0; px < bmp.Width; px++)
            for (int py = 0; py < bmp.Height; py++)
                bmp.SetPixel(px, py, System.Drawing.Color.FromArgb(248, 245, 240));

        // Draw "text-like" horizontal dark blocks to simulate scanned text
        double lineY = 80;
        for (int line = 0; line < 25; line++)
        {
            double lineWidth = 300 + rng.Next(200);
            double x = 60;
            while (x < 60 + lineWidth)
            {
                double wordWidth = 20 + rng.Next(60);
                for (int px = (int)x; px < (int)(x + wordWidth) && px < bmp.Width; px++)
                    for (int py = (int)lineY; py < (int)lineY + 8 && py < bmp.Height; py++)
                        if (px >= 0 && py >= 0)
                            bmp.SetPixel(px, py, System.Drawing.Color.FromArgb(40, 40, 40));
                x += wordWidth + 5 + rng.Next(8);
            }
            lineY += 20 + rng.Next(5);
        }

        // Draw a "signature" scribble at the bottom
        double sigX = 300;
        double sigY = bmp.Height - 120;
        for (int i = 0; i < 50; i++)
        {
            int nextX = (int)sigX + 2 + rng.Next(3);
            int nextY = (int)sigY + rng.Next(20) - 10;
            if (nextX >= 0 && nextX < bmp.Width && nextY >= 0 && nextY < bmp.Height)
                bmp.SetPixel(nextX, nextY, System.Drawing.Color.FromArgb(20, 20, 80));
            sigX = nextX;
            sigY = nextY;
        }
    });

    // Draw the full-page scanned image — no text layer, just an embedded image XObject
    gfx.DrawImage(scannedImage, 0, 0, page.Width, page.Height);

    document.Save(path);
    Console.WriteLine($"Created: {Path.GetFileName(path)}");
}

static XImage CreateBitmapImage(int width, int height, Action<Bitmap> drawAction)
{
    using var bitmap = new Bitmap(width, height);
    drawAction(bitmap);

    using var ms = new MemoryStream();
    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
    ms.Position = 0;

    return XImage.FromStream(() => new MemoryStream(ms.ToArray()));
}
