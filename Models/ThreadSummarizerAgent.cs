using System.Text;
using System.Text.Json;
using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;

namespace InsightMail.Services
{
    public class ThreadSummarizerAgent
    {
        private readonly IGeminiClientService _gemini;
        private readonly ThreadChunker _chunker;
        private readonly ILogger<ThreadSummarizerAgent> _logger;

        public ThreadSummarizerAgent(
            IGeminiClientService gemini,
            ThreadChunker chunker,
            ILogger<ThreadSummarizerAgent> logger)
        {
            _gemini = gemini;
            _chunker = chunker;
            _logger = logger;
        }

        public async Task<ThreadSummary> SummarizeThreadAsync(List<Email> emails)
        {
            // Step 1: Order chronologically
            var ordered = emails.OrderBy(e => e.ReceivedDate).ToList();

            // Step 2: Chunk
            var chunks = _chunker.ChunkThread(ordered);
            _logger.LogInformation("Thread split into {Count} chunks", chunks.Count);

            // Step 3: Summarize each chunk in parallel
            var chunkSummaries = await SummarizeChunksAsync(chunks);

            // Step 4: Synthesize final summary
            var summary = await SynthesizeFinalSummaryAsync(chunkSummaries);

            // Step 5: Extract structured data in parallel
            await EnrichSummaryAsync(summary, ordered);

            // Step 6: Score confidence
            summary.OriginalEmailCount = ordered.Count;
            summary.ChunkCount = chunks.Count;
            summary.ConfidenceScore = CalculateConfidence(summary);
            summary.ThreadSubject = ordered.FirstOrDefault()?.Subject ?? "";
            summary.EmailIds = ordered.Select(e => e.Id).ToList();

            return summary;
        }

        // ── Chunk summarization ──────────────────────────────────────────

        private async Task<List<ChunkSummary>> SummarizeChunksAsync(
            List<EmailChunk> chunks)
        {
            var tasks = chunks.Select(c => SummarizeChunkAsync(c));
            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        private async Task<ChunkSummary> SummarizeChunkAsync(EmailChunk chunk)
        {
            var emailsText = FormatEmailsForPrompt(chunk.Emails);

            var prompt = $@"
You are summarizing a portion of an email thread.
This is chunk {chunk.ChunkIndex + 1} of the thread.

Emails (chronological):
{emailsText}

Create a concise summary (150-200 words) covering:
1. Main topics discussed
2. Any decisions made
3. Questions raised
4. Action items mentioned
5. Key participants and their positions

Be factual. Include dates when relevant. Do not invent information.";

            var response = await _gemini.GenerateContentAsync(prompt);

            return new ChunkSummary
            {
                ChunkIndex = chunk.ChunkIndex,
                Summary = response,
                EmailCount = chunk.Emails.Count,
                DateRange = $"{chunk.Emails.First().ReceivedDate:MMM dd} - " +
                             $"{chunk.Emails.Last().ReceivedDate:MMM dd}"
            };
        }

        // ── Final synthesis ──────────────────────────────────────────────

        private async Task<ThreadSummary> SynthesizeFinalSummaryAsync(
            List<ChunkSummary> chunkSummaries)
        {
            var chunksText = FormatChunkSummariesForPrompt(chunkSummaries);

            var prompt = $@"
You are creating an executive summary of an email thread.
The thread has been broken into {chunkSummaries.Count} parts:

{chunksText}

Create a comprehensive executive summary with these sections:

1. EXECUTIVE SUMMARY (2-3 sentences)
   - What is this thread about?
   - What is the current status?
   - What is the key outcome or next step?

2. KEY DECISIONS (bullet list)
   - Decisions made, who made them, date if mentioned

3. OPEN QUESTIONS (bullet list)
   - Unresolved issues awaiting answers

4. ACTION ITEMS (bullet list)
   - Tasks, who is responsible, deadlines if mentioned

5. TIMELINE OF KEY EVENTS (chronological)
   - Major developments with dates

Be specific and factual. If a section has no items, write 'None identified'.";

            var response = await _gemini.GenerateContentAsync(prompt);

            return new ThreadSummary
            {
                ExecutiveSummaryText = response,
                GeneratedDate = DateTime.UtcNow
            };
        }

        // ── Structured extraction ────────────────────────────────────────

        private async Task EnrichSummaryAsync(ThreadSummary summary, List<Email> emails)
        {
            // Max 2 concurrent Gemini calls to stay within rate limits
            var semaphore = new SemaphoreSlim(2, 2);

            async Task<T> Throttled<T>(Func<Task<T>> taskFactory)
            {
                await semaphore.WaitAsync();
                try { return await taskFactory(); }
                finally { semaphore.Release(); }
            }

            var decisionsTask = Throttled(() => ExtractDecisionsAsync(summary.ExecutiveSummaryText));
            var questionsTask = Throttled(() => ExtractQuestionsAsync(summary.ExecutiveSummaryText));
            var actionsTask = Throttled(() => ExtractActionItemsAsync(summary.ExecutiveSummaryText));
            var participantsTask = Throttled(() => AnalyzeParticipantsAsync(emails));
            var timelineTask = Throttled(() => ExtractTimelineAsync(summary.ExecutiveSummaryText));

            await Task.WhenAll(decisionsTask, questionsTask, actionsTask, participantsTask, timelineTask);

            summary.KeyDecisions = decisionsTask.Result;
            summary.OpenQuestions = questionsTask.Result;
            summary.ActionItems = actionsTask.Result;
            summary.Participants = participantsTask.Result;
            summary.Timeline = timelineTask.Result;
        }

        private async Task<List<Decision>> ExtractDecisionsAsync(string text)
        {
            var prompt = $@"
Extract all decisions from this email thread summary.
Summary: {text}

Return ONLY a JSON array, no markdown, no explanation:
[{{""decision"":""text"",""decidedBy"":""name or null"",""date"":""date or null"",""conditions"":""any or null""}}]
If none, return: []";

            var raw = await _gemini.GenerateContentAsync(prompt);
            return ParseJson<List<Decision>>(raw) ?? new();
        }

        private async Task<List<OpenQuestion>> ExtractQuestionsAsync(string text)
        {
            var prompt = $@"
Extract all open/unresolved questions from this email thread summary.
Summary: {text}

Return ONLY a JSON array, no markdown, no explanation:
[{{""question"":""text"",""raisedBy"":""name or null"",""importance"":""why or null"",""status"":""status or null""}}]
If none, return: []";

            var raw = await _gemini.GenerateContentAsync(prompt);
            return ParseJson<List<OpenQuestion>>(raw) ?? new();
        }

        private async Task<List<ThreadActionItem>> ExtractActionItemsAsync(string text)
        {
            var prompt = $@"
Extract all action items from this email thread summary.
Summary: {text}

Return ONLY a JSON array, no markdown, no explanation:
[{{""task"":""text"",""assignedTo"":""name or null"",""deadline"":""date or null"",""priority"":""High/Medium/Low""}}]
If none, return: []";

            var raw = await _gemini.GenerateContentAsync(prompt);
            return ParseJson<List<ThreadActionItem>>(raw) ?? new();
        }

        private async Task<List<Participant>> AnalyzeParticipantsAsync(
            List<Email> emails)
        {
            var map = new Dictionary<string, ParticipantInfo>();

            foreach (var email in emails)
            {
                if (!map.ContainsKey(email.Sender))
                    map[email.Sender] = new ParticipantInfo
                    {
                        Email = email.Sender,
                        Name = ExtractName(email.Sender),
                        FirstParticipation = email.ReceivedDate,
                        LastParticipation = email.ReceivedDate
                    };

                map[email.Sender].EmailCount++;
                map[email.Sender].LastParticipation = email.ReceivedDate;
            }

            var participantList = string.Join("\n", map.Values
                .OrderByDescending(p => p.EmailCount)
                .Select(p => $"- {p.Name} ({p.Email}): {p.EmailCount} emails"));

            var emailsText = FormatEmailsForPrompt(emails.Take(10).ToList());

            var prompt = $@"
Analyze participant roles in this email thread.

Participants:
{participantList}

Thread sample:
{emailsText}

Return ONLY a JSON array, no markdown, no explanation:
[{{""name"":""name"",""email"":""email"",""role"":""Decision Maker/Contributor/Observer"",""position"":""supporting/opposing/neutral/unclear"",""keyContributions"":""brief summary""}}]";

            var raw = await _gemini.GenerateContentAsync(prompt);
            return ParseJson<List<Participant>>(raw) ?? new();
        }

        private async Task<List<TimelineEvent>> ExtractTimelineAsync(string text)
        {
            var prompt = $@"
Create a chronological timeline of key events from this email thread summary.
Summary: {text}

Return ONLY a JSON array ordered by date, no markdown, no explanation:
[{{""date"":""date or Unknown"",""event"":""what happened"",""significance"":""why it matters""}}]
If none, return: []";

            var raw = await _gemini.GenerateContentAsync(prompt);
            return ParseJson<List<TimelineEvent>>(raw) ?? new();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private string FormatEmailsForPrompt(List<Email> emails)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < emails.Count; i++)
            {
                var e = emails[i];
                sb.AppendLine($"--- Email {i + 1} ---");
                sb.AppendLine($"Date: {e.ReceivedDate:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"From: {e.Sender}");
                sb.AppendLine($"Subject: {e.Subject}");
                sb.AppendLine($"Content: {e.Body}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string FormatChunkSummariesForPrompt(List<ChunkSummary> summaries)
        {
            var sb = new StringBuilder();
            foreach (var c in summaries)
            {
                sb.AppendLine($"=== Part {c.ChunkIndex + 1} ({c.DateRange}) ===");
                sb.AppendLine(c.Summary);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string ExtractName(string email) =>
            email.Contains('@') ? email.Split('@')[0] : email;

        private T? ParseJson<T>(string raw)
        {
            try
            {
                var clean = raw.Replace("```json", "").Replace("```", "").Trim();
                return JsonSerializer.Deserialize<T>(clean,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JSON parse failed: {Msg}", ex.Message);
                return default;
            }
        }

        private decimal CalculateConfidence(ThreadSummary summary)
        {
            decimal score = 100m;
            if (summary.OpenQuestions.Count > 5) score -= 10m;
            if (!summary.KeyDecisions.Any()) score -= 15m;
            if (summary.Participants.Count < 2) score -= 10m;
            if (summary.Timeline.Count < 2 &&
                summary.OriginalEmailCount > 10) score -= 10m;
            return Math.Max(0, score);
        }
    }
}