using System.Text;
using System.Text.Json;

namespace InsightMail.API.Services
{
    public interface IGeminiClientService
    {
        Task<string> GenerateContentAsync(string prompt, int maxRetries = 4);
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

        public async Task<string> GenerateContentAsync(string prompt, int maxRetries = 4)
        {
            var apiKey = _config["GoogleGemini:ApiKey"];
            var model = _config["GoogleGemini:Model"] ?? "gemini-2.5-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var request = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };
            var json = JsonSerializer.Serialize(request);

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                var response = await _httpClient.PostAsync(
                    url, new StringContent(json, Encoding.UTF8, "application/json"));

                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseText);
                    return doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString() ?? "";
                }

                if ((int)response.StatusCode == 429 && attempt < maxRetries)
                {
                    // Try to read retryDelay from the response, fall back to exponential
                    var retryAfter = ParseRetryDelay(responseText)
                                     ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1) * 5);

                    _logger.LogWarning(
                        "Gemini 429 on attempt {Attempt}/{Max}. Retrying in {Delay}s...",
                        attempt + 1, maxRetries, retryAfter.TotalSeconds);

                    await Task.Delay(retryAfter);
                    continue;
                }

                _logger.LogError("Gemini API Error: {Body}", responseText);
                throw new Exception(responseText);
            }

            throw new Exception("Gemini API failed after max retries.");
        }

        private TimeSpan? ParseRetryDelay(string responseText)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var delayStr = doc.RootElement
                    .GetProperty("error")
                    .GetProperty("details")
                    .EnumerateArray()
                    .Where(d => d.TryGetProperty("retryDelay", out _))
                    .Select(d => d.GetProperty("retryDelay").GetString())
                    .FirstOrDefault();

                // Format is "8s" or "8.975s"
                if (delayStr != null && delayStr.EndsWith("s") &&
                    double.TryParse(delayStr.TrimEnd('s'), out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds + 1); // +1s buffer
                }
            }
            catch { /* ignore parse failures */ }
            return null;
        }
    }
}