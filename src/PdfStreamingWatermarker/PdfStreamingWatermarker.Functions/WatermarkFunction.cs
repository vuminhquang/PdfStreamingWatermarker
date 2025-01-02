using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PdfStreamingWatermarker.Functions.Services;
using Azure.Storage.Queues;
using System.Text.Json;

namespace PdfStreamingWatermarker.Functions;

public class WatermarkFunction
{
    private readonly IFileService _fileService;
    private readonly IPdfWatermarkService _watermarkService;
    private readonly ILogger<WatermarkFunction> _logger;
    private readonly QueueServiceClient _queueServiceClient;
    private static readonly SemaphoreSlim _throttle = new(100); // Max concurrent requests

    public WatermarkFunction(
        IFileService fileService,
        IPdfWatermarkService watermarkService,
        QueueServiceClient queueServiceClient,
        ILoggerFactory loggerFactory)
    {
        _fileService = fileService;
        _watermarkService = watermarkService;
        _queueServiceClient = queueServiceClient;
        _logger = loggerFactory.CreateLogger<WatermarkFunction>();
    }

    [Function("WatermarkPdf")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
        string filename,
        string watermarkText)
    {
        if (!await _throttle.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            // Too busy - queue the request
            var queueClient = _queueServiceClient.GetQueueClient("watermark-queue");
            await queueClient.CreateIfNotExistsAsync();

            var request = new QueueWatermarkFunction.WatermarkRequest(filename, watermarkText);
            await queueClient.SendMessageAsync(JsonSerializer.Serialize(request));

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteStringAsync("Request queued for processing. Check watermarked_" + filename);
            return response;
        }

        _logger.LogInformation("Processing watermark request for {Filename}", filename);

        try
        {
            if (!await _fileService.FileExistsAsync(filename, req.FunctionContext.CancellationToken))
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"PDF file '{filename}' not found.");
                return notFound;
            }

            var sourceStream = await _fileService.GetFileStreamAsync(filename, req.FunctionContext.CancellationToken);
            if (sourceStream == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"PDF file '{filename}' could not be retrieved.");
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/pdf");
            response.Headers.Add("Content-Disposition", $"inline; filename=watermarked_{filename}");

            await _watermarkService.WatermarkPdfAsync(
                sourceStream, 
                response.Body, 
                watermarkText, 
                req.FunctionContext.CancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing watermark request for {Filename}", filename);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An error occurred while processing the PDF.");
            return error;
        }
        finally
        {
            _throttle.Release();
        }
    }
} 