using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

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

        // Global rate limiter: max 4 requests per 65 seconds (safely under free tier limit of 5/min)
        private static readonly RateLimiter _rateLimiter = new TokenBucketRateLimiter(
            new TokenBucketRateLimiterOptions
            {
                TokenLimit = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(65),
                TokensPerPeriod = 4,
                AutoReplenishment = true
            });

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
            // Wait for a rate limit token before every call
            using var lease = await _rateLimiter.AcquireAsync(permitCount: 1);
            if (!lease.IsAcquired)
                throw new Exception("Gemini rate limiter queue is full. Try again later.");

            var apiKey = _config["GoogleGemini:ApiKey"];
            var model = _config["GoogleGemini:Model"] ?? "gemini-2.5-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var request = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };
            var json = JsonSerializer.Serialize(request);

            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
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

                if ((int)response.StatusCode == 429)
                {
                    var delay = ParseRetryDelay(responseText)
                                ?? TimeSpan.FromSeconds(Math.Pow(2, attempt) * 15);

                    _logger.LogWarning(
                        "Gemini 429 on attempt {Attempt}/{Max}. Retrying in {Delay}s...",
                        attempt, maxRetries, (int)delay.TotalSeconds);

                    if (attempt < maxRetries)
                        await Task.Delay(delay);
                    continue;
                }

                _logger.LogError("Gemini API error: {Body}", responseText);
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

                if (delayStr != null && delayStr.EndsWith("s") &&
                    double.TryParse(delayStr.TrimEnd('s'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var seconds))
                    return TimeSpan.FromSeconds(seconds + 2);
            }
            catch { }
            return null;
        }
    }
}