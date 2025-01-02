using Azure.Storage.Blobs;
using PdfStreamingWatermarker.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure file services based on environment
if (builder.Environment.IsProduction())
{
    // Azure Blob Storage in production
    var connectionString = builder.Configuration["AzureStorage:ConnectionString"];
    builder.Services.AddSingleton(x => new BlobServiceClient(connectionString));
    builder.Services.AddScoped<IFileService, AzureBlobFileService>();
}
else
{
    // API service in development
    builder.Services.AddHttpClient<IFileService, ApiFileService>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ApiService:BaseUrl"] ?? "http://localhost:5194");
    });
}

// Add service registration
builder.Services.AddScoped<IPdfWatermarkService, PdfWatermarkService>();

// Add after builder creation
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure logging levels
builder.Services.Configure<LoggerFilterOptions>(options =>
{
    options.AddFilter("PdfStreamingWatermarker.Services", LogLevel.Debug);
    options.AddFilter("Microsoft", LogLevel.Warning);
    options.AddFilter("System", LogLevel.Warning);
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
app.MapMethods("/pdf/{filename}", ["GET", "HEAD"], async (string filename, HttpRequest request, HttpResponse response) =>
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
    IPdfWatermarkService watermarkService,
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

    await watermarkService.WatermarkPdfAsync(sourceStream, response.Body, watermarkText, cancellationToken);
    
    return Results.Empty;
});

app.Run();