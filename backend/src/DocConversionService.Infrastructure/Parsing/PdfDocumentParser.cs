namespace DocConversionService.Infrastructure.Parsing;

using DocConversionService.Application.Interfaces;
using DocConversionService.Domain.Exceptions;
using DocConversionService.Domain.Parsing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

public class PdfDocumentParser : IDocumentParser
{
    public ParsedDocument Parse(byte[] pdfBytes)
    {
        PdfDocument pdfDocument;
        try
        {
            pdfDocument = PdfDocument.Open(pdfBytes);
        }
        catch (Exception ex)
        {
            throw new CorruptedFileException("The PDF file could not be opened. It may be corrupted or not a valid PDF.", ex);
        }

        var elements = new List<DocumentElement>();
        var allWords = new List<Word>();
        bool hasAnyImages = false;
        bool hasAnyText = false;

        try
        {
            using (pdfDocument)
            {
                // First pass: collect all words to compute median font size
                foreach (var page in pdfDocument.GetPages())
                {
                    try
                    {
                        var words = page.GetWords().ToList();
                        allWords.AddRange(words);
                    }
                    catch (Exception ex)
                    {
                        throw new CorruptedFileException(
                            $"Failed to parse page {page.Number}. The page may be corrupted.", ex);
                    }
                }

                double medianFontSize = 12; // default fallback
                if (allWords.Count > 0)
                {
                    var fontSizes = allWords.Select(w => w.Letters.FirstOrDefault()?.FontSize ?? 12)
                        .OrderBy(s => s).ToList();
                    medianFontSize = fontSizes[fontSizes.Count / 2];
                }

                double headingThreshold = medianFontSize * 1.3;

                // Second pass: extract elements per page
                int pageIndex = 0;
                foreach (var page in pdfDocument.GetPages())
                {
                    var pageWords = page.GetWords().ToList();

                    if (pageWords.Count > 0)
                        hasAnyText = true;

                    // Group words into lines by Y position
                    var lines = GroupWordsIntoLines(pageWords);

                    // Group lines into paragraphs
                    var paragraphs = GroupLinesIntoParagraphs(lines);

                    foreach (var paragraph in paragraphs)
                    {
                        var text = string.Join(" ", paragraph.SelectMany(line => line.Select(w => w.Text)));
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        // Check if this is a heading based on average font size
                        var avgFontSize = paragraph
                            .SelectMany(l => l)
                            .SelectMany(w => w.Letters)
                            .Select(l => l.FontSize)
                            .DefaultIfEmpty(medianFontSize)
                            .Average();

                        if (avgFontSize >= headingThreshold)
                        {
                            int level = avgFontSize >= medianFontSize * 2 ? 1 : 2;
                            elements.Add(new HeadingElement(text.Trim(), level));
                        }
                        else
                        {
                            elements.Add(new ParagraphElement(text.Trim()));
                        }
                    }

                    // Extract images
                    try
                    {
                        var images = page.GetImages().ToList();
                        foreach (var image in images)
                        {
                            hasAnyImages = true;
                            byte[] imageBytes;
                            try
                            {
                                imageBytes = image.RawBytes.ToArray();
                            }
                            catch
                            {
                                // If we can't get raw bytes, try TryGetPng
                                if (image.TryGetPng(out var pngBytes))
                                    imageBytes = pngBytes;
                                else
                                    continue; // skip unextractable images
                            }

                            if (imageBytes.Length > 0)
                            {
                                elements.Add(new ImageElement(imageBytes, "image/png", pageIndex));
                            }
                        }
                    }
                    catch
                    {
                        // Images extraction failed — continue without images for this page
                    }

                    pageIndex++;
                }
            }
        }
        catch (DocumentProcessingException)
        {
            throw; // re-throw our typed exceptions
        }
        catch (Exception ex)
        {
            throw new CorruptedFileException("Failed to parse the PDF document.", ex);
        }

        // Check for scanned document (images but no text)
        if (!hasAnyText && hasAnyImages)
        {
            throw new ScannedDocumentException(
                "The document appears to be a scanned image with no extractable text layer. " +
                "Rule-based conversion requires a text layer.");
        }

        // Check for empty document
        if (elements.Count == 0)
        {
            throw new EmptyDocumentException("The document contains no extractable content.");
        }

        return new ParsedDocument(elements);
    }

    private static List<List<Word>> GroupWordsIntoLines(List<Word> words)
    {
        if (words.Count == 0) return new List<List<Word>>();

        var sorted = words.OrderByDescending(w => w.BoundingBox.Bottom)
                         .ThenBy(w => w.BoundingBox.Left)
                         .ToList();

        var lines = new List<List<Word>>();
        var currentLine = new List<Word> { sorted[0] };
        double currentY = sorted[0].BoundingBox.Bottom;

        for (int i = 1; i < sorted.Count; i++)
        {
            double wordY = sorted[i].BoundingBox.Bottom;
            if (Math.Abs(wordY - currentY) < 5) // Same line threshold
            {
                currentLine.Add(sorted[i]);
            }
            else
            {
                lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());
                currentLine = new List<Word> { sorted[i] };
                currentY = wordY;
            }
        }
        lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());

        return lines;
    }

    private static List<List<List<Word>>> GroupLinesIntoParagraphs(List<List<Word>> lines)
    {
        if (lines.Count == 0) return new List<List<List<Word>>>();

        var paragraphs = new List<List<List<Word>>>();
        var currentParagraph = new List<List<Word>> { lines[0] };

        for (int i = 1; i < lines.Count; i++)
        {
            double prevBottom = lines[i - 1].Min(w => w.BoundingBox.Bottom);
            double currTop = lines[i].Max(w => w.BoundingBox.Top);
            double gap = prevBottom - currTop;

            // Get average line height for comparison
            double lineHeight = lines[i - 1].Average(w => w.BoundingBox.Height);

            if (gap > lineHeight * 1.5) // New paragraph if gap > 1.5x line height
            {
                paragraphs.Add(currentParagraph);
                currentParagraph = new List<List<Word>> { lines[i] };
            }
            else
            {
                currentParagraph.Add(lines[i]);
            }
        }
        paragraphs.Add(currentParagraph);

        return paragraphs;
    }
}
