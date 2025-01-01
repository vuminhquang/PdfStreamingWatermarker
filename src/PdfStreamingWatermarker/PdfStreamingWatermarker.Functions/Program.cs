using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PdfStreamingWatermarker.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Add Azure Blob Storage
        var connectionString = context.Configuration["AzureStorage:ConnectionString"];
        services.AddSingleton(x => new BlobServiceClient(connectionString));
        
        // Register services
        services.AddScoped<IFileService, AzureBlobFileService>();
        services.AddScoped<IPdfWatermarkService, PdfWatermarkService>();
    })
    .Build();

await host.RunAsync(); 