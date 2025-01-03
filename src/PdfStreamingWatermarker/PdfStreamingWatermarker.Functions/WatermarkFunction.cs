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
    private static readonly SemaphoreSlim _throttle = new(100); // Max concurrent requests

    public WatermarkFunction(
        IFileService fileService,
        IPdfWatermarkService watermarkService,
        ILoggerFactory loggerFactory)
    {
        _fileService = fileService;
        _watermarkService = watermarkService;
        _logger = loggerFactory.CreateLogger<WatermarkFunction>();
    }

    [Function("WatermarkPdf")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
        string filename,
        string watermarkText)
    {
        _logger.LogInformation("Processing watermark request for {Filename}", filename);

        try
        {
            // Check if file exists
            if (!await _fileService.FileExistsAsync(filename, req.FunctionContext.CancellationToken))
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"PDF file '{filename}' not found.");
                return notFound;
            }

            // Get source stream
            var sourceStream = await _fileService.GetFileStreamAsync(filename, req.FunctionContext.CancellationToken);
            if (sourceStream == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"PDF file '{filename}' could not be retrieved.");
                return notFound;
            }

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/pdf");
            response.Headers.Add("Content-Disposition", $"inline; filename=watermarked_{filename}");

            // Process watermark - will wait if system is busy
            await _throttle.WaitAsync(req.FunctionContext.CancellationToken);
            try
            {
                await _watermarkService.WatermarkPdfAsync(
                    sourceStream, 
                    response.Body, 
                    watermarkText, 
                    req.FunctionContext.CancellationToken);
            }
            finally
            {
                _throttle.Release();
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing watermark request for {Filename}", filename);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An error occurred while processing the PDF.");
            return error;
        }
    }
} 