namespace GearsHouse.Models
{
    public class CartItemEntity
    {
        public int Id { get; set; }
        public string UserId { get; set; } // Liên kết với Identity User
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }       
        public string ImageUrl { get; set; }
    }
}
