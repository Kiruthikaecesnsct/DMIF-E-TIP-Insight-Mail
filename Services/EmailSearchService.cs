using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;

namespace InsightMail.Services
{
    public class EmailSearchService
    {
        private readonly IEmbeddingService _embedding;
        private readonly IEmailRepository _repository;

        public EmailSearchService(
            IEmbeddingService embedding,
            IEmailRepository repository)
        {
            _embedding = embedding;
            _repository = repository;
        }

        public async Task<List<SearchResult>> SearchAsync(string query, int limit = 10)
        {
            // 1. Generate embedding
            var queryEmbedding = await _embedding.GenerateEmbeddingAsync(
                $"performance review self assessment {query}");

            Console.WriteLine($"Query embedding size: {queryEmbedding.Length}");

            // 2. Call repository (must return SearchResult)
            var results = await _repository.VectorSearchAsync(queryEmbedding, limit);

            return results;
        }
    }
}