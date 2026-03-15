namespace GearsHouse.Models
{
    public class RevenueViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal TotalDiscount { get; set; }
        public List<DailyRevenue> DailyRevenueData { get; set; }
        public List<TopCategory> TopSellingCategories { get; set; }
        public List<TopProduct> TopSellingProducts { get; set; }
    }
    public class DailyRevenue
    {
        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
    }
    public class TopCategory
    {
        public string CategoryName { get; set; }
        public int TotalSold { get; set; }
        public double Percentage { get; set; }
    }
    public class TopProduct
    {
        public string ProductName { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; }
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public string CategoryName { get; set; }
    }
}
