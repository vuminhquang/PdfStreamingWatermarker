using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Pdf.Extgstate;
using PdfStreamingWatermarker;
using PdfStreamingWatermarker.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<IFileService, ApiFileService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5194"); // Use your API base URL
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add static files middleware
app.UseStaticFiles();
app.UseHttpsRedirection();

// Base PDF endpoint - streaming version
app.MapMethods("/pdf/{filename}", new[] { "GET", "HEAD" }, async (string filename, HttpRequest request, HttpResponse response) =>
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filename);
    
    if (!File.Exists(filePath))
    {
        return Results.NotFound($"PDF file '{filename}' not found. Looked in: {filePath}");
    }

    response.Headers.Append("Content-Disposition", $"inline; filename={filename}");
    response.ContentType = "application/pdf";
    
    // Only send the file content for GET requests
    if (request.Method == "GET")
    {
        const int bufferSize = 8192; // 8KB buffer
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
        await fileStream.CopyToAsync(response.Body);
    }
    
    return Results.Empty;
});

// Watermark endpoint - streaming version
app.MapGet("/pdf/{filename}/watermark", async (
    string filename, 
    string watermarkText, 
    IFileService fileService,
    HttpResponse response, 
    CancellationToken cancellationToken) =>
{
    if (!await fileService.FileExistsAsync(filename, cancellationToken))
    {
        return Results.NotFound($"PDF file '{filename}' not found.");
    }

    var sourceStream = await fileService.GetFileStreamAsync(filename, cancellationToken);
    if (sourceStream == null)
    {
        return Results.NotFound($"PDF file '{filename}' could not be retrieved.");
    }

    response.Headers.Append("Content-Disposition", $"inline; filename=watermarked_{filename}");
    response.ContentType = "application/pdf";

    var writerProperties = new WriterProperties()
        .SetCompressionLevel(0) // Disable compression to reduce memory usage
        .UseSmartMode();

    var asyncOutputStream = new AsyncOutputStream(response.Body);

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
    
    return Results.Empty;
});

app.Run();