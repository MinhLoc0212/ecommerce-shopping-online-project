using System.ComponentModel.DataAnnotations;

namespace GearsHouse.Models
{
    public class CouponCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(7, MinimumLength = 7)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Số tiền giảm: 1,000,000 VNĐ
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; } = 1_000_000m;

        // Áp dụng cho đơn có tổng tiền >= 5,000,000 VNĐ
        [Range(0, double.MaxValue)]
        public decimal MinOrderTotal { get; set; } = 5_000_000m;

        public bool IsUsed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? UsedOrderId { get; set; }
        public DateTime? UsedAt { get; set; }
    }
}