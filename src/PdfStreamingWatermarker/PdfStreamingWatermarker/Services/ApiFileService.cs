namespace PdfStreamingWatermarker.Services;

public class ApiFileService : IFileService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiFileService> _logger;

    public ApiFileService(HttpClient httpClient, ILogger<ApiFileService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Stream?> GetFileStreamAsync(string filename, CancellationToken cancellationToken)
    {
        try
        {
            var requestUrl = $"/pdf/{filename}";
            _logger.LogInformation("Getting file stream from: {Url}", requestUrl);
            
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            _logger.LogInformation("Get file stream response: {StatusCode}", response.StatusCode);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("File {Filename} not found", filename);
                return null;
            }

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file stream for {Filename}", filename);
            return null;
        }
    }

    public async Task<bool> FileExistsAsync(string filename, CancellationToken cancellationToken)
    {
        try
        {
            var requestUrl = $"/pdf/{filename}";
            _logger.LogInformation("Checking file existence at: {Url}", requestUrl);
            
            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, requestUrl), 
                cancellationToken);
                
            _logger.LogInformation("File exists response: {StatusCode}", response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence for {Filename}", filename);
            return false;
        }
    }
} 