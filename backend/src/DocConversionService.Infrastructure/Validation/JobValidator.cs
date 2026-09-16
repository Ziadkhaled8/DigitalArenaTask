namespace DocConversionService.Infrastructure.Validation;

using System.Security.Cryptography;
using System.Text;
using DocConversionService.Domain.Interfaces;
using DocConversionService.Domain.Parsing;

public class JobValidator : IJobValidator
{
    public ValidationResult Validate(ParsedDocument original, IReadOnlyList<SplitPartResult> parts)
    {
        // 1. Part-sequence check
        if (parts.Count == 0)
            return new ValidationResult(false, "No output parts were produced.");

        var totalParts = parts[0].TotalParts;
        if (parts.Any(p => p.TotalParts != totalParts))
            return new ValidationResult(false, "Inconsistent TotalParts value across parts.");

        if (totalParts != parts.Count)
            return new ValidationResult(false, $"Expected {totalParts} parts but found {parts.Count}.");

        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].PartNumber != i + 1)
                return new ValidationResult(false, $"Part sequence gap: expected part {i + 1} but found part {parts[i].PartNumber}.");
        }

        // 2. Content-integrity check via canonical hash
        var originalHash = ComputeCanonicalHash(original.Elements);

        var allPartElements = parts
            .OrderBy(p => p.PartNumber)
            .SelectMany(p => p.Elements)
            .ToList();

        var reconstructedHash = ComputeCanonicalHash(allPartElements);

        if (originalHash != reconstructedHash)
            return new ValidationResult(false, "Content integrity check failed: canonical content hash mismatch between original and split parts.");

        return new ValidationResult(true);
    }

    private static string ComputeCanonicalHash(IEnumerable<DocumentElement> elements)
    {
        using var sha256 = SHA256.Create();
        using var stream = new MemoryStream();

        foreach (var element in elements)
        {
            byte[] data;
            switch (element)
            {
                case HeadingElement h:
                    data = Encoding.UTF8.GetBytes($"H{h.Level}:{h.Text}\n");
                    break;
                case ParagraphElement p:
                    data = Encoding.UTF8.GetBytes($"P:{p.Text}\n");
                    break;
                case ImageElement img:
                    // Hash image bytes to avoid putting full image data in the stream
                    var imgHash = Convert.ToHexString(SHA256.HashData(img.Bytes));
                    data = Encoding.UTF8.GetBytes($"I:{img.MimeType}:{imgHash}\n");
                    break;
                default:
                    continue;
            }
            stream.Write(data);
        }

        stream.Position = 0;
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
