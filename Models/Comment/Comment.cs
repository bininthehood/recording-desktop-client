namespace RecordClient.Models.Comment
{
    public class Comment
    {
        public string Text { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;
        public string Display => $"[{Time:HH:mm:ss}] {Text}";
    }
}
