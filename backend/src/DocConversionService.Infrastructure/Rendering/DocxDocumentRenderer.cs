namespace DocConversionService.Infrastructure.Rendering;

using DocConversionService.Domain.Enums;
using DocConversionService.Domain.Interfaces;
using DocConversionService.Domain.Parsing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;

public class DocxDocumentRenderer : IDocumentRenderer
{
    public OutputFormat Format => OutputFormat.Docx;

    public byte[] Render(IReadOnlyList<DocumentElement> elements)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            // Add styles
            AddStyles(mainPart);

            foreach (var element in elements)
            {
                switch (element)
                {
                    case HeadingElement heading:
                        body.Append(CreateHeadingParagraph(heading));
                        break;

                    case ParagraphElement paragraph:
                        body.Append(CreateTextParagraph(paragraph.Text));
                        break;

                    case ImageElement image:
                        var imagePart = mainPart.AddImagePart(GetImagePartType(image.MimeType));
                        using (var imgStream = new MemoryStream(image.Bytes))
                        {
                            imagePart.FeedData(imgStream);
                        }
                        var relationshipId = mainPart.GetIdOfPart(imagePart);
                        body.Append(CreateImageParagraph(relationshipId));
                        break;
                }
            }

            mainPart.Document.Append(body);
        }

        return stream.ToArray();
    }

    private static void AddStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Heading 1 style
        styles.Append(new Style(
            new StyleName { Val = "heading 1" },
            new StyleParagraphProperties(
                new SpacingBetweenLines { Before = "240", After = "120" }),
            new StyleRunProperties(
                new Bold(),
                new FontSize { Val = "48" }) // 24pt
        ) { Type = StyleValues.Paragraph, StyleId = "Heading1" });

        // Heading 2 style
        styles.Append(new Style(
            new StyleName { Val = "heading 2" },
            new StyleParagraphProperties(
                new SpacingBetweenLines { Before = "200", After = "100" }),
            new StyleRunProperties(
                new Bold(),
                new FontSize { Val = "36" }) // 18pt
        ) { Type = StyleValues.Paragraph, StyleId = "Heading2" });

        stylesPart.Styles = styles;
    }

    private static Paragraph CreateHeadingParagraph(HeadingElement heading)
    {
        var styleId = heading.Level == 1 ? "Heading1" : "Heading2";
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            new Run(new Text(heading.Text) { Space = SpaceProcessingModeValues.Preserve })
        );
    }

    private static Paragraph CreateTextParagraph(string text)
    {
        return new Paragraph(
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
        );
    }

    private static Paragraph CreateImageParagraph(string relationshipId)
    {
        long cx = 5000000; // ~5.2 inches
        long cy = 3000000; // ~3.1 inches

        var element = new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = (UInt32Value)1U, Name = "Image" },
            new A.Graphic(
                new A.GraphicData(
                    new Pic.Picture(
                        new Pic.NonVisualPictureProperties(
                            new Pic.NonVisualDrawingProperties { Id = 0U, Name = "image.png" },
                            new Pic.NonVisualPictureDrawingProperties()),
                        new Pic.BlipFill(
                            new A.Blip { Embed = relationshipId },
                            new A.Stretch(new A.FillRectangle())),
                        new Pic.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0, Y = 0 },
                                new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList())
                            { Preset = A.ShapeTypeValues.Rectangle })
                    )
                ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
            )
        ) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U };

        return new Paragraph(
            new Run(new Drawing(element))
        );
    }

    private static PartTypeInfo GetImagePartType(string mimeType) => mimeType.ToLower() switch
    {
        "image/png" => ImagePartType.Png,
        "image/jpeg" or "image/jpg" => ImagePartType.Jpeg,
        "image/gif" => ImagePartType.Gif,
        "image/bmp" => ImagePartType.Bmp,
        _ => ImagePartType.Png
    };
}
