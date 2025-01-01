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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Add new PDF streaming endpoint
app.MapGet("/pdf/{filename}", async (string filename, HttpResponse response) =>
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filename);
    
    if (!File.Exists(filePath))
    {
        return Results.NotFound($"PDF file '{filename}' not found.");
    }

    response.Headers.Append("Content-Disposition", $"inline; filename={filename}");
    response.ContentType = "application/pdf";
    
    await using var stream = File.OpenRead(filePath);
    await stream.CopyToAsync(response.Body);
    
    return Results.Empty;
})
    .WithName("GetPdf")
    .WithOpenApi();

app.MapGet("/pdf/{filename}/watermark", async (string filename, string watermarkText, HttpResponse response, CancellationToken cancellationToken) =>
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filename);
    
    if (!File.Exists(filePath))
    {
        return Results.NotFound($"PDF file '{filename}' not found.");
    }

    response.Headers.Append("Content-Disposition", $"inline; filename=watermarked_{filename}");
    response.ContentType = "application/pdf";

    var writerProperties = new WriterProperties()
        .SetFullCompressionMode(false)
        .UseSmartMode();

    var asyncOutputStream = new AsyncOutputStream(response.Body);

    await using var sourceStream = File.OpenRead(filePath);
    using var pdfReader = new PdfReader(sourceStream);
    await using var pdfWriter = new PdfWriter(asyncOutputStream, writerProperties);
    using var pdfDocument = new PdfDocument(pdfReader, pdfWriter);
    using var document = new Document(pdfDocument);

    pdfDocument.SetCloseWriter(false);
    pdfDocument.SetFlushUnusedObjects(true);

    var numberOfPages = pdfDocument.GetNumberOfPages();
    var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

    for (var i = 1; i <= numberOfPages; i++)
    {
        var page = pdfDocument.GetPage(i);
        var pageSize = page.GetPageSize();
        var canvas = new PdfCanvas(page);

        // Create a new content stream for the watermark
        canvas.SaveState();

        // Set transparency
        var gState = new PdfExtGState().SetFillOpacity(0.3f);
        canvas.SetExtGState(gState);

        // Create the watermark text
        var watermark = new Paragraph(watermarkText)
            .SetFont(font)
            .SetFontSize(60)
            .SetFontColor(ColorConstants.LIGHT_GRAY);

        // Calculate center position
        var centerX = pageSize.GetWidth() / 2;
        var centerY = pageSize.GetHeight() / 2;

        // Create the layout canvas
        var layoutCanvas = new Canvas(canvas, pageSize);
        
        // Position and rotate the watermark
        layoutCanvas
            .ShowTextAligned(watermark, 
                centerX, 
                centerY, 
                i, 
                TextAlignment.CENTER, 
                VerticalAlignment.MIDDLE, 
                (float)(Math.PI / 6));

        // Cleanup
        layoutCanvas.Close();
        canvas.RestoreState();
        canvas.Release();
        
        // Flush the page
        page.Flush();

        if (i % 10 != 0) continue;
        pdfWriter.Flush();
        await asyncOutputStream.FlushAsync(cancellationToken);
    }

    document.Close();
    
    return Results.Empty;
})
    .WithName("GetWatermarkedPdf")
    .WithOpenApi();

app.Run();