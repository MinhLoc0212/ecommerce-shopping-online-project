using System.ComponentModel.DataAnnotations;

namespace GearsHouse.Models
{
    public class Brand
    {
        [Key]
        public int BrandId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }

        // Danh sách sản phẩm thuộc thương hiệu này
        public ICollection<Product>? Products { get; set; }
    }
}
