namespace DocConversionService.Domain.Parsing;

public abstract class DocumentElement { }

public class HeadingElement : DocumentElement
{
    public string Text { get; }
    public int Level { get; }

    public HeadingElement(string text, int level)
    {
        Text = text;
        Level = Math.Clamp(level, 1, 6);
    }
}

public class ParagraphElement : DocumentElement
{
    public string Text { get; }

    public ParagraphElement(string text)
    {
        Text = text;
    }
}

public class ImageElement : DocumentElement
{
    public byte[] Bytes { get; }
    public string MimeType { get; }
    public int PageIndex { get; }

    public ImageElement(byte[] bytes, string mimeType, int pageIndex)
    {
        Bytes = bytes;
        MimeType = mimeType;
        PageIndex = pageIndex;
    }
}
