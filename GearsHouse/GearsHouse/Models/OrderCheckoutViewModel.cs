namespace GearsHouse.Models
{
    public class OrderCheckoutViewModel
    {
        public Order Order { get; set; }
        public List<CartItem> CartItems { get; set; } = new List<CartItem>();
        public decimal TotalAmount => CartItems.Sum(item => item.Price * item.Quantity);
    }
}
