using System.Text;
using System.Text.Json;

namespace InsightMail.API.Services
{
    public interface IGeminiClientService
    {
        Task<string> GenerateContentAsync(string prompt);
    }

    public class GeminiClientService : IGeminiClientService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<GeminiClientService> _logger;

        public GeminiClientService(
            HttpClient httpClient,
            IConfiguration config,
            ILogger<GeminiClientService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GenerateContentAsync(string prompt)
        {
            try
            {
                var apiKey = _config["GoogleGemini:ApiKey"];
                var model = _config["GoogleGemini:Model"] ?? "gemini-2.5-flash";

                var url =
                    $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(request);

                var response = await _httpClient.PostAsync(
                    url,
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API Error: {0}", responseText);
                    throw new Exception(responseText);
                }

                using var doc = JsonDocument.Parse(responseText);

                var result = doc
                    .RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return result ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API error");
                throw;
            }
        }
    }
}