using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PdfStreamingWatermarker.Functions.Services;

namespace PdfStreamingWatermarker.Functions;

public class QueueWatermarkFunction
{
    private readonly IFileService _fileService;
    private readonly IPdfWatermarkService _watermarkService;
    private readonly ILogger<QueueWatermarkFunction> _logger;
    private readonly QueueServiceClient _queueServiceClient;

    public record WatermarkRequest(string Filename, string WatermarkText);

    public QueueWatermarkFunction(
        IFileService fileService,
        IPdfWatermarkService watermarkService,
        QueueServiceClient queueServiceClient,
        ILoggerFactory loggerFactory)
    {
        _fileService = fileService;
        _watermarkService = watermarkService;
        _queueServiceClient = queueServiceClient;
        _logger = loggerFactory.CreateLogger<QueueWatermarkFunction>();
    }

    [Function("ProcessWatermarkQueue")]
    public async Task Run(
        [QueueTrigger("watermark-queue")] string requestJson,
        FunctionContext context)
    {
        var request = JsonSerializer.Deserialize<WatermarkRequest>(requestJson);
        if (request == null)
        {
            _logger.LogError("Failed to deserialize queue message: {Message}", requestJson);
            return; // Don't retry invalid messages
        }

        try
        {
            var sourceStream = await _fileService.GetFileStreamAsync(
                request.Filename, 
                context.CancellationToken);

            if (sourceStream == null)
            {
                _logger.LogError("PDF file '{Filename}' not found", request.Filename);
                return;
            }

            // Create output blob name and stream
            var outputFilename = $"watermarked_{request.Filename}";
            using var outputStream = await _fileService.CreateFileStreamAsync(
                outputFilename, 
                context.CancellationToken);

            await _watermarkService.WatermarkPdfAsync(
                sourceStream,
                outputStream,
                request.WatermarkText,
                context.CancellationToken);

            _logger.LogInformation(
                "Successfully processed queued watermark for {Filename}", 
                request.Filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing queued watermark for {Filename}",
                request.Filename ?? "unknown");
            throw; // Retry queue processing
        }
    }
} 