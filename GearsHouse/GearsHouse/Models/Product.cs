using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GearsHouse.Models
{
    public class Product
    {
        public int Id { get; set; }
        [Required, StringLength(100)]
        public string Name { get; set; }
        [Range(1, 999999999)]
        public decimal Price { get; set; }
        public string? ProductInfo { get; set; }
        public string? TechnicalSpecs { get; set; }
        public string? ImageUrl { get; set; }
        public List<ProductImage>? Images { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        [Required]
        public int BrandId { get; set; }
        public Brand? Brand { get; set; }

        public int Quantity { get; set; }

    }
}
