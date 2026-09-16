namespace DocConversionService.Application.Configuration;

public class ConversionSettings
{
    public long MaxPartSizeBytes { get; set; } = 2 * 1024 * 1024; // 2 MB
}
