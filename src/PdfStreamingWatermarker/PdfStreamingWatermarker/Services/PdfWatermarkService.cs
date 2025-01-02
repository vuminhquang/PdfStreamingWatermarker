using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Pdf.Extgstate;
using PdfStreamingWatermarker.Infrastructure;

namespace PdfStreamingWatermarker.Services;

public class PdfWatermarkService : IPdfWatermarkService
{
    private readonly ILogger<PdfWatermarkService> _logger;

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
        using var scope = _logger.BeginScope("Watermarking PDF with text: {WatermarkText}", watermarkText);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var writerProperties = new WriterProperties()
                .SetCompressionLevel(0)
                .UseSmartMode();

            var asyncOutputStream = new AsyncOutputStream(outputStream);

            using var pdfReader = new PdfReader(sourceStream);
            await using var pdfWriter = new PdfWriter(asyncOutputStream, writerProperties);
            using var pdfDocument = new PdfDocument(pdfReader, pdfWriter);
            
            _logger.LogInformation("PDF opened. Total pages: {PageCount}", pdfDocument.GetNumberOfPages());
            
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

            var pageStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var numberOfPages = pdfDocument.GetNumberOfPages();

            for (var i = 1; i <= numberOfPages; i++)
            {
                var pageStart = pageStopwatch.ElapsedMilliseconds;
                
                var page = pdfDocument.GetPage(i);
                var pageSize = page.GetPageSize();
                var canvas = new PdfCanvas(page);

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

                if (i % 10 == 0 || i == numberOfPages)
                {
                    pdfWriter.Flush();
                    await asyncOutputStream.FlushAsync(cancellationToken);
                    _logger.LogInformation(
                        "Processed {PageCount}/{TotalPages} pages. Last batch took {BatchTime}ms", 
                        i, 
                        numberOfPages,
                        pageStopwatch.ElapsedMilliseconds);
                    pageStopwatch.Restart();
                }

                _logger.LogDebug(
                    "Page {PageNumber}/{TotalPages} processed in {PageTime}ms", 
                    i, 
                    numberOfPages,
                    pageStopwatch.ElapsedMilliseconds - pageStart);
            }

            pdfDocument.Close();
            await asyncOutputStream.FlushAsync(cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "PDF watermarking completed. Total time: {TotalTime}ms for {PageCount} pages", 
                stopwatch.ElapsedMilliseconds,
                numberOfPages);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Error watermarking PDF after {ElapsedTime}ms",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
} 