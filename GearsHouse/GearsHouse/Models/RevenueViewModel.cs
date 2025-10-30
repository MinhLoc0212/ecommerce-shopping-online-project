namespace GearsHouse.Models
{
    public class RevenueViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal TotalDiscount { get; set; }
        public List<DailyRevenue> DailyRevenueData { get; set; }
    }
    public class DailyRevenue
    {
        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
