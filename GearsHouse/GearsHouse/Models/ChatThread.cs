namespace GearsHouse.Models
{
    public class ChatThread
    {
        public int Id { get; set; }
        public string CustomerId { get; set; }
        public string? StaffId { get; set; }
        public bool IsClosed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
