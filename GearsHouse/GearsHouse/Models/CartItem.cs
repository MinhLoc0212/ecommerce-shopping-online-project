namespace GearsHouse.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal OriginalPrice { get; set; } // Giá gốc
        public decimal Price { get; set; }         // Giá sau khuyến mãi
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
    }
}
