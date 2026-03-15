namespace GearsHouse.Models
{
    public class ChatDashboardViewModel
    {
        public ChatThread Thread { get; set; }
        public ApplicationUser Customer { get; set; }
        public int UnreadCount { get; set; }
    }
}
