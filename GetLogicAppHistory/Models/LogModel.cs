namespace LoggerWithOtelExporter.Models
{
    internal class LogModel
    {
        public int attempt { get; set; }
        public string? entityType { get; set; }
        public string? source { get; set; }
        public string? target { get; set; }
        public string? BusinessObjectId { get; set; }
        public string? Response { get; set; }
        public int? ResponseCode { get; set; }
        public bool Success { get; set; }
        public string? category { get; set; }
    }
}
