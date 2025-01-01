namespace PdfStreamingWatermarker.Functions.Services;

public interface IPdfWatermarkService
{
    Task WatermarkPdfAsync(Stream sourceStream, Stream outputStream, string watermarkText, CancellationToken cancellationToken);
} 