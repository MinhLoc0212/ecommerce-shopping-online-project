namespace GearsHouse.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public int ThreadId { get; set; }
        public string SenderId { get; set; }
        public bool IsStaff { get; set; }
        public string Content { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ChatThread Thread { get; set; }
    }
}
