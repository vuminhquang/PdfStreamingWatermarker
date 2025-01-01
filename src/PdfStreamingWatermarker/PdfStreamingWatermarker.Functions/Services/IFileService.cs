namespace PdfStreamingWatermarker.Functions.Services;

public interface IFileService
{
    Task<Stream?> GetFileStreamAsync(string filename, CancellationToken cancellationToken);
    Task<bool> FileExistsAsync(string filename, CancellationToken cancellationToken);
} 