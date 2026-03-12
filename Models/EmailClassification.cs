namespace InsightMail.Models
{
    public class EmailClassification
    {
        public string Category { get; set; } = "";
        public int Priority { get; set; }
        public string Reasoning { get; set; } = "";
        public double Confidence { get; set; }
    }
}
