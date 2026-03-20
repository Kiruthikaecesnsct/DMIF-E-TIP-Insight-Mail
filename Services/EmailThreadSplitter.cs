using InsightMail.API.Models;

namespace InsightMail.Services
{
    public class EmailThreadSplitter
    {
        // Patterns that indicate a quoted/previous message starts
        private static readonly string[] SeparatorPatterns = new[]
        {
            "--------------------------------------------------",
            "________________________________",
            "From:",
            "-----Original Message-----",
            "-----Forwarded Message-----"
        };

        public List<Email> SplitThread(Email originalEmail)
        {
            var emails = new List<Email>();
            var body = originalEmail.Body ?? "";

            // Find all separator positions
            var splits = FindSplitPositions(body);

            if (splits.Count == 0)
            {
                // No thread history — single email
                emails.Add(originalEmail);
                return emails;
            }

            // First email is the top-level message (before first separator)
            var topBody = body.Substring(0, splits[0]).Trim();
            if (!string.IsNullOrWhiteSpace(topBody))
            {
                emails.Add(new Email
                {
                    Id = Guid.NewGuid().ToString(),
                    Sender = originalEmail.Sender,
                    Recipients = originalEmail.Recipients,
                    Subject = originalEmail.Subject,
                    Body = topBody,
                    ReceivedDate = originalEmail.ReceivedDate,
                    UploadedDate = originalEmail.UploadedDate,
                    ThreadId = originalEmail.ThreadId,
                    InReplyTo = originalEmail.InReplyTo
                });
            }

            // Parse each quoted block as a separate email
            for (int i = 0; i < splits.Count; i++)
            {
                var start = splits[i];
                var end = i + 1 < splits.Count ? splits[i + 1] : body.Length;
                var block = body.Substring(start, end - start).Trim();

                var parsed = ParseQuotedBlock(block, originalEmail);
                if (parsed != null)
                    emails.Add(parsed);
            }

            // Reverse so oldest email is first
            emails.Reverse();
            return emails;
        }

        private List<int> FindSplitPositions(string body)
        {
            var positions = new List<int>();
            var lines = body.Split('\n');
            var charPos = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                bool isSeparator = SeparatorPatterns.Any(p =>
                    trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                // Also detect "From: Name <email>" pattern mid-body
                bool isFromLine = trimmed.StartsWith("From:",
                    StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains("@");

                if (isSeparator || isFromLine)
                {
                    // Only add if not already near a previous split
                    if (!positions.Any() || charPos - positions.Last() > 50)
                        positions.Add(charPos);
                }

                charPos += line.Length + 1; // +1 for \n
            }

            return positions;
        }

        private Email? ParseQuotedBlock(string block, Email originalEmail)
        {
            // Try to extract From, Sent/Date, To, Subject from the block header
            var sender = ExtractField(block, "From:");
            var dateStr = ExtractField(block, "Sent:")
                           ?? ExtractField(block, "Date:");
            var subject = ExtractField(block, "Subject:");

            // Get the body — everything after the header lines
            var bodyStart = FindBodyStart(block);
            var body = bodyStart >= 0
                ? block.Substring(bodyStart).Trim()
                : block.Trim();

            if (string.IsNullOrWhiteSpace(body)) return null;

            // Parse date
            DateTime receivedDate = originalEmail.ReceivedDate.AddHours(-1);
            if (!string.IsNullOrEmpty(dateStr))
                DateTime.TryParse(dateStr, out receivedDate);

            return new Email
            {
                Id = Guid.NewGuid().ToString(),
                Sender = sender ?? "unknown@unknown.com",
                Recipients = originalEmail.Recipients,
                Subject = subject ?? originalEmail.Subject,
                Body = body,
                ReceivedDate = receivedDate,
                UploadedDate = originalEmail.UploadedDate,
                ThreadId = originalEmail.ThreadId,
                InReplyTo = originalEmail.InReplyTo
            };
        }

        private string? ExtractField(string block, string fieldName)
        {
            var lines = block.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(fieldName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(fieldName.Length).Trim();
                }
            }
            return null;
        }

        private int FindBodyStart(string block)
        {
            // Body starts after the blank line following headers
            var headerFields = new[] { "From:", "Sent:", "Date:", "To:",
                                       "Subject:", "Cc:", "---------" };
            var lines = block.Split('\n');
            bool inHeader = true;

            int charPos = 0;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (inHeader)
                {
                    bool isHeaderLine = headerFields.Any(h =>
                        trimmed.StartsWith(h,
                            StringComparison.OrdinalIgnoreCase)) ||
                        trimmed.StartsWith("---");

                    if (!isHeaderLine && !string.IsNullOrWhiteSpace(trimmed))
                        return charPos;
                }

                charPos += line.Length + 1;
            }

            return -1;
        }
    }
}