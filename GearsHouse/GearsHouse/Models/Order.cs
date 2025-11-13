using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GearsHouse.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string FullName { get; set; } // Họ tên



        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; } // Số điện thoại

        public string UserId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public decimal TotalPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        public string ShippingAddress { get; set; }

        public string? Notes { get; set; }

        // Mã giảm giá áp dụng cho đơn (tuỳ chọn)
        public string? CouponCode { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public string PaymentMethod { get; set; } // "COD" hoặc "Online"

        [ForeignKey("UserId")]
        [ValidateNever]
        public ApplicationUser ApplicationUser { get; set; }

        public List<OrderDetail> OrderDetails { get; set; }

        public OrderStatus OrderStatus { get; set; } = OrderStatus.ChoXacNhan;
    }

    public enum OrderStatus
    {
        [Display(Name = "Chờ Xác Nhận")]
        ChoXacNhan = 0,    // 0

        [Display(Name = "Đang Xử Lý")]
        DangXuLy = 1,      // 1

        [Display(Name = "Đang Giao Hàng")]
        DangGiaoHang = 2,  // 2

        [Display(Name = "Hoàn Thành")]
        HoanThanh = 3,     // 3

        [Display(Name = "Đã Hủy")]
        DaHuy = 4          // 4
    }
}
