using InsightMail.API.Models;
using InsightMail.Models;

namespace InsightMail.Services
{
    public class ThreadChunker
    {
        private const int TARGET_CHUNK_SIZE = 3000;
        private const int MAX_EMAILS_PER_CHUNK = 7;

        public List<EmailChunk> ChunkThread(List<Email> thread)
        {
            var chunks = new List<EmailChunk>();
            var sortedEmails = thread
                .OrderBy(e => e.ReceivedDate)
                .ToList();

            int i = 0;
            while (i < sortedEmails.Count)
            {
                var chunk = new EmailChunk { ChunkIndex = chunks.Count };
                var tokenCount = 0;

                while (i < sortedEmails.Count &&
                       chunk.Emails.Count < MAX_EMAILS_PER_CHUNK &&
                       tokenCount < TARGET_CHUNK_SIZE)
                {
                    chunk.Emails.Add(sortedEmails[i]);
                    tokenCount += EstimateTokens(sortedEmails[i]);
                    i++;
                }

                chunks.Add(chunk);

                // Overlap: include last email of this chunk in next chunk
                if (i < sortedEmails.Count && chunk.Emails.Count > 1)
                    i--;
            }

            return chunks;
        }

        private int EstimateTokens(Email email)
        {
            var words = (email.Subject?.Split(' ').Length ?? 0) +
                        (email.Body?.Split(' ').Length ?? 0);
            return (int)(words / 0.75);
        }
    }
}