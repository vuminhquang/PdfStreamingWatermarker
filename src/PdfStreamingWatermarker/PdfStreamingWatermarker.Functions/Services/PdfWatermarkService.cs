using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Pdf.Extgstate;
using Microsoft.Extensions.Logging;
using PdfStreamingWatermarker.Functions.Infrastructure;

namespace PdfStreamingWatermarker.Functions.Services;

public class PdfWatermarkService : IPdfWatermarkService
{
    private readonly ILogger<PdfWatermarkService> _logger;
    private static readonly SemaphoreSlim _semaphore = new(8); // Limit concurrent operations

    public PdfWatermarkService(ILogger<PdfWatermarkService> logger)
    {
        _logger = logger;
    }

    public async Task WatermarkPdfAsync(
        Stream sourceStream, 
        Stream outputStream, 
        string watermarkText, 
        CancellationToken cancellationToken)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            var writerProperties = new WriterProperties()
                .SetCompressionLevel(0)
                .UseSmartMode()
                .SetFullCompressionMode(false);

            var asyncOutputStream = new AsyncOutputStream(outputStream);

            using var pdfReader = new PdfReader(sourceStream);
            await using var pdfWriter = new PdfWriter(asyncOutputStream, writerProperties);
            using var pdfDocument = new PdfDocument(pdfReader, pdfWriter);
            
            // Configure document for streaming
            pdfDocument.SetCloseWriter(false);
            pdfDocument.SetFlushUnusedObjects(true);

            // Create resources once
            var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var gState = new PdfExtGState().SetFillOpacity(0.3f);
            var watermark = new Paragraph(watermarkText)
                .SetFont(font)
                .SetFontSize(60)
                .SetFontColor(ColorConstants.LIGHT_GRAY);

            try
            {
                // Process pages one at a time
                var numberOfPages = pdfDocument.GetNumberOfPages();
                for (var i = 1; i <= numberOfPages; i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var pageSize = page.GetPageSize();
                    var canvas = new PdfCanvas(page);

                    // Add watermark
                    canvas.SaveState();
                    canvas.SetExtGState(gState);

                    var centerX = pageSize.GetWidth() / 2;
                    var centerY = pageSize.GetHeight() / 2;

                    var layoutCanvas = new Canvas(canvas, pageSize);
                    layoutCanvas.ShowTextAligned(watermark, 
                        centerX, 
                        centerY, 
                        i, 
                        TextAlignment.CENTER, 
                        VerticalAlignment.MIDDLE, 
                        (float)(Math.PI / 6));

                    layoutCanvas.Close();
                    canvas.RestoreState();
                    canvas.Release();

                    // Flush writer periodically instead of every page
                    if (i % 10 == 0 || i == numberOfPages)
                    {
                        pdfWriter.Flush();
                        await asyncOutputStream.FlushAsync(cancellationToken);
                    }
                }

                // Cleanup
                pdfDocument.Close();
                await asyncOutputStream.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error watermarking PDF");
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
} 