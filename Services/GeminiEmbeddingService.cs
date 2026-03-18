namespace InsightMail.Services
{
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;

    namespace InsightMail.Services
    {
        public class GeminiEmbeddingService : IEmbeddingService
        {
            private readonly string _apiKey;
            private readonly HttpClient _httpClient;
            private const string EmbeddingModel = "gemini-embedding-2-preview";

            public GeminiEmbeddingService(IConfiguration configuration, HttpClient httpClient)
            {
                _apiKey = configuration["GoogleGemini:ApiKey"]!;
                _httpClient = httpClient;
            }

            public async Task<float[]> GenerateEmbeddingAsync(string text)
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{EmbeddingModel}:embedContent?key={_apiKey}";

                var requestBody = new
                {
                    content = new
                    {
                        parts = new[]
        {
            new { text = text }
        }
                    },
                    outputDimensionality = 1024 // MUST be here (top level)
                };

                var requestJson = JsonSerializer.Serialize(requestBody);

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Embedding API failed: {error}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseJson);

                var embedding = doc.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values")
                    .EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToArray();
                Console.WriteLine($"Embedding size: {embedding.Length}");
                return embedding;
            }
        }
    }
}
