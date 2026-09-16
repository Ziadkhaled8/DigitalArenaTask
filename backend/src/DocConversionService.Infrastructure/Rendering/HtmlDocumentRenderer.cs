namespace DocConversionService.Infrastructure.Rendering;

using System.Text;
using DocConversionService.Domain.Enums;
using DocConversionService.Domain.Interfaces;
using DocConversionService.Domain.Parsing;

public class HtmlDocumentRenderer : IDocumentRenderer
{
    public OutputFormat Format => OutputFormat.Html;

    public byte[] Render(IReadOnlyList<DocumentElement> elements)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Converted Document</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 2em; line-height: 1.6; }");
        sb.AppendLine("    img { max-width: 100%; height: auto; display: block; margin: 1em 0; }");
        sb.AppendLine("    h1 { font-size: 2em; margin-top: 1em; }");
        sb.AppendLine("    h2 { font-size: 1.5em; margin-top: 0.8em; }");
        sb.AppendLine("    p { margin: 0.5em 0; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        foreach (var element in elements)
        {
            switch (element)
            {
                case HeadingElement heading:
                    var tag = $"h{heading.Level}";
                    sb.AppendLine($"  <{tag}>{System.Net.WebUtility.HtmlEncode(heading.Text)}</{tag}>");
                    break;

                case ParagraphElement paragraph:
                    sb.AppendLine($"  <p>{System.Net.WebUtility.HtmlEncode(paragraph.Text)}</p>");
                    break;

                case ImageElement image:
                    var base64 = Convert.ToBase64String(image.Bytes);
                    sb.AppendLine($"  <img src=\"data:{image.MimeType};base64,{base64}\" alt=\"Document image\">");
                    break;
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
