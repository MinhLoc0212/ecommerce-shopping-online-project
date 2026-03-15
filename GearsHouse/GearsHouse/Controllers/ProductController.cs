using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using GearsHouse.Extensions;
using System.Data;
using GearsHouse.Models;
using GearsHouse.Repositories;

namespace GearsHouse.Controllers
{

    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBrandRepository _brandRepository;
        private readonly ApplicationDbContext _context;
        public ProductController(IProductRepository productRepository, ICategoryRepository categoryRepository, IBrandRepository brandRepository, ApplicationDbContext context)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _brandRepository = brandRepository;
            _context = context;
        }


        public async Task<IActionResult> Index(int? categoryId, int? brandId, string keyword)
        {
            var products = await _productRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();
                products = products.Where(p => p.Name.ToLower().Contains(keyword) || (p.ProductInfo != null && p.ProductInfo.ToLower().Contains(keyword)));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId.Value).ToList();
                var category = await _categoryRepository.GetByIdAsync(categoryId.Value);
                ViewBag.SelectedCategory = category?.Name;
            }
            else
            {
                ViewBag.SelectedCategory = "Tất cả sản phẩm";
            }



            if (brandId.HasValue)
            {
                products = products.Where(p => p.BrandId == brandId).ToList();
                ViewBag.SelectedBrand = (await _brandRepository.GetByIdAsync(brandId.Value))?.Name;
            }

            // Lấy danh sách khuyến mãi đang hoạt động
            var promotions = _context.Promotions
                .Where(p => DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate)
                .ToList();

            // Tính toán giá sau khi áp dụng khuyến mãi
            var productDiscounts = new Dictionary<int, (decimal newPrice, int discountPercent)>();

            foreach (var product in products)
            {
                var promo = promotions.FirstOrDefault(p => p.ProductId == product.Id);
                if (promo != null)
                {
                    decimal discountedPrice = product.Price * (1 - promo.DiscountPercent / 100m);
                    productDiscounts[product.Id] = (discountedPrice, promo.DiscountPercent);
                }
            }

            // Gửi dictionary xuống view để xử lý hiển thị
            ViewBag.ProductDiscounts = productDiscounts;

            // Lấy thống kê đánh giá (Review Stats)
            var productIds = products.Select(p => p.Id).ToList();
            var reviewStats = await _context.Reviews
                .Where(r => productIds.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new { 
                    ProductId = g.Key, 
                    Count = g.Count(), 
                    Average = g.Average(r => (double)r.Rating) 
                })
                .ToDictionaryAsync(k => k.ProductId, v => (v.Average, v.Count));
            
            ViewBag.ReviewStats = reviewStats;

            return View(products);
        }


        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add()
        {
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            ViewBag.Brands = new SelectList(await _context.Brands.ToListAsync(), "BrandId", "Name");
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Add");
            }
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add(Product product, IFormFile imageUrl, List<IFormFile> galleryImages)
        {
            if (ModelState.IsValid)
            {
                if (imageUrl != null)
                {
                    product.ImageUrl = await SaveImage(imageUrl);
                }

                await _productRepository.AddAsync(product);
                // Lưu danh sách ảnh phụ nếu có
                if (galleryImages != null && galleryImages.Count > 0)
                {
                    foreach (var img in galleryImages)
                    {
                        if (img != null && img.Length > 0)
                        {
                            var url = await SaveImage(img);
                            _context.ProductImages.Add(new ProductImage
                            {
                                Url = url,
                                ProductId = product.Id
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }
                TempData["SuccessMessage"] = "Sản phẩm đã được thêm thành công!";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Sản phẩm đã được thêm thành công!" });
                }
                return RedirectToAction("Dashboard", "Dashboard", new { tab = "product" });
            }

            var categories = await _categoryRepository.GetAllAsync();
            var brands = await _brandRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            ViewBag.Brands = new SelectList(brands, "BrandId", "Name");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Add", product);
            }
            return View(product);
        }




        [Authorize(Roles = "Admin")]
        private async Task<string> SaveImage(IFormFile image)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "image");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
            var savePath = Path.Combine(uploadsDir, fileName);
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/image/" + fileName;
        }

        public async Task<IActionResult> Display(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Lấy danh sách khuyến mãi đang hoạt động
            var promotions = _context.Promotions
                .Where(p => DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate && p.ProductId == id)
                .ToList();

            // Tính toán giá sau khi áp dụng khuyến mãi
            decimal? discountedPrice = null;
            int? discountPercent = null;

            if (promotions.Any())
            {
                var promo = promotions.First(); // Giả sử chỉ có một khuyến mãi đang áp dụng
                discountedPrice = product.Price * (1 - promo.DiscountPercent / 100m);
                discountPercent = promo.DiscountPercent;
            }

            ViewBag.ProductDiscounts = discountedPrice.HasValue ? discountedPrice.Value : (decimal?)null;
            ViewBag.DiscountPercent = discountPercent;

            //  Thêm đoạn này để lấy đánh giá sản phẩm
            var reviews = await _context.Reviews
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            ViewBag.Reviews = reviews;

            // Lưu lịch sử xem sản phẩm theo tài khoản (Session theo UserId)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionKey = $"RECENTLY_VIEWED_{userId}";

                var viewedIds = HttpContext.Session.GetObjectFromJson<List<int>>(sessionKey) ?? new List<int>();
                // Đưa sản phẩm hiện tại lên đầu, loại bỏ trùng
                viewedIds.RemoveAll(pid => pid == product.Id);
                viewedIds.Insert(0, product.Id);
                // Giới hạn 50 sản phẩm đã xem gần đây
                if (viewedIds.Count > 50)
                {
                    viewedIds = viewedIds.Take(50).ToList();
                }
                HttpContext.Session.SetObjectAsJson(sessionKey, viewedIds);

                // Tải danh sách sản phẩm đã xem (không gồm sản phẩm hiện tại) để hiển thị
                var recentIds = viewedIds.Where(pid => pid != product.Id).ToList();
                if (recentIds.Any())
                {
                    var recentProducts = await _context.Products
                        .Where(p => recentIds.Contains(p.Id))
                        .ToListAsync();
                    // Sắp xếp theo thứ tự trong recentIds
                    ViewBag.RecentlyViewed = recentIds
                        .Select(id2 => recentProducts.FirstOrDefault(p => p.Id == id2))
                        .Where(p => p != null)
                        .ToList();
                }
            }

            // Lấy danh sách sản phẩm tương tự (cùng danh mục)
            var relatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(10)
                .ToListAsync();
            ViewBag.RelatedProducts = relatedProducts;

            return View(product);
        }


        //Hiển thị form cập nhật sản phẩm
       
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
        
            var brands = await _brandRepository.GetAllAsync();
            ViewBag.Brands = new SelectList(brands, "BrandId", "Name", product.BrandId);
        
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Update", product);
            }
            return View(product);
        }

        // POST: Xử lý lưu chỉnh sửa
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageUrl, List<IFormFile> galleryImages)
        {
            // Loại bỏ validation cho ImageUrl để không gây lỗi khi không có ảnh
            ModelState.Remove("ImageUrl");

            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);

                // Tách kiểm tra thay đổi sản phẩm và thêm ảnh phụ
                bool isProductChanged = false;
                bool hasNewGalleryImages = galleryImages != null && galleryImages.Any(img => img != null && img.Length > 0);

                // Kiểm tra nếu tên sản phẩm, giá, danh mục, thương hiệu hoặc số lượng có thay đổi không
                if (existingProduct.Name != product.Name) isProductChanged = true;
                if (existingProduct.Price != product.Price) isProductChanged = true;
                if (existingProduct.CategoryId != product.CategoryId) isProductChanged = true;
                if (!string.IsNullOrEmpty(existingProduct.ProductInfo) != !string.IsNullOrEmpty(product.ProductInfo)) isProductChanged = true;
                if (!string.IsNullOrEmpty(existingProduct.TechnicalSpecs) != !string.IsNullOrEmpty(product.TechnicalSpecs)) isProductChanged = true;
                if (existingProduct.BrandId != product.BrandId) isProductChanged = true;
                if (existingProduct.Quantity != product.Quantity) isProductChanged = true;
                // Nếu có upload ảnh chính mới thì cũng xem như có thay đổi
                if (imageUrl != null) isProductChanged = true;

                // Cập nhật thông tin sản phẩm nếu có thay đổi ở các trường
                if (isProductChanged)
                {
                    if (imageUrl != null)
                    {
                        product.ImageUrl = await SaveImage(imageUrl);
                    }
                    else
                    {
                        product.ImageUrl = existingProduct.ImageUrl;
                    }

                    existingProduct.Name = product.Name;
                    existingProduct.Price = product.Price;
                    existingProduct.ProductInfo = product.ProductInfo;
                    existingProduct.TechnicalSpecs = product.TechnicalSpecs;
                    existingProduct.CategoryId = product.CategoryId;
                    existingProduct.BrandId = product.BrandId;
                    existingProduct.Quantity = product.Quantity;
                    existingProduct.ImageUrl = product.ImageUrl;

                    await _productRepository.UpdateAsync(existingProduct);
                }

                // Thêm ảnh phụ nếu có, ngay cả khi không thay đổi trường sản phẩm
                if (hasNewGalleryImages)
                {
                    foreach (var img in galleryImages)
                    {
                        if (img != null && img.Length > 0)
                        {
                            var url = await SaveImage(img);
                            _context.ProductImages.Add(new ProductImage
                            {
                                Url = url,
                                ProductId = existingProduct.Id
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Thông báo: thành công nếu có bất kỳ thay đổi nào (trường hoặc ảnh phụ)
                if (isProductChanged || hasNewGalleryImages)
                {
                    TempData["SuccessMessage"] = "Sản phẩm đã được cập nhật thành công!";
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var products = await _productRepository.GetAllAsync();
                    return PartialView("~/Views/Dashboard/_ProductListDashboard.cshtml", products);
                }
                // Quay lại Dashboard
                return RedirectToAction("Dashboard", "Dashboard", new { tab = "product" });
            }

            // Nếu ModelState không hợp lệ, trả về view để chỉnh sửa
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            var brands = await _brandRepository.GetAllAsync();
            ViewBag.Brands = new SelectList(brands, "BrandId", "Name");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Update", product);
            }
            return View(product);
        }

        //Hiển thị form xác nhận xóa sản phẩm
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productRepository.DeleteAsync(id);
            TempData["SuccessMessage"] = "Sản phẩm đã được xóa thành công!";
            return RedirectToAction("Dashboard", "Dashboard", new { tab = "product" });
        }

        public async Task<IActionResult> Filter(string keyword, int? categoryId, int? brandId, decimal? minPrice, decimal? maxPrice, string sort)
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                // Align search logic with Index action
                keyword = keyword.ToLower();
                products = products.Where(p => p.Name.ToLower().Contains(keyword) || (p.ProductInfo != null && p.ProductInfo.ToLower().Contains(keyword)));
            }

            if (categoryId.HasValue && categoryId.Value != 0)
                products = products.Where(p => p.CategoryId == categoryId.Value);

            if (brandId.HasValue && brandId.Value != 0)
                products = products.Where(p => p.BrandId == brandId.Value);

            if (minPrice.HasValue)
                products = products.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                products = products.Where(p => p.Price <= maxPrice.Value);

            // Sorting logic
            switch (sort)
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price);
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price);
                    break;
                case "newest":
                    products = products.OrderByDescending(p => p.Id); // Assuming higher ID means newer
                    break;
                default:
                    // Default sorting (e.g., by name or relevance)
                    products = products.OrderBy(p => p.Name);
                    break;
            }

            var filteredProducts = await products.ToListAsync();

            // Lấy danh sách khuyến mãi đang hoạt động
            var promotions = _context.Promotions
                .Where(p => DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate)
                .ToList();

            // Tính toán giá sau khi áp dụng khuyến mãi
            var productDiscounts = new Dictionary<int, (decimal newPrice, int discountPercent)>();

            foreach (var product in filteredProducts)
            {
                var promo = promotions.FirstOrDefault(p => p.ProductId == product.Id);
                if (promo != null)
                {
                    decimal discountedPrice = product.Price * (1 - promo.DiscountPercent / 100m);
                    productDiscounts[product.Id] = (discountedPrice, promo.DiscountPercent);
                }
            }

            // Gửi dictionary xuống view để xử lý hiển thị
            ViewBag.ProductDiscounts = productDiscounts;

            return PartialView("_ProductList", filteredProducts);
        }

        [HttpGet]
        public async Task<IActionResult> Suggest(string keyword, int take = 6)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Json(Array.Empty<object>());
            }

            var suggestions = await _context.Products
                .Where(p => EF.Functions.Like(p.Name, "%" + keyword + "%"))
                .OrderBy(p => p.Name)
                .Take(take)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    imageUrl = p.ImageUrl,
                    price = p.Price
                })
                .ToListAsync();

            return Json(suggestions);
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProductImage(int id, int productId)
        {
            var image = await _context.ProductImages.FirstOrDefaultAsync(pi => pi.Id == id && pi.ProductId == productId);
            if (image != null)
            {
                _context.ProductImages.Remove(image);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Ảnh phụ đã được xóa thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy ảnh để xóa!";
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var product = await _productRepository.GetByIdAsync(productId);
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
                var brands = await _brandRepository.GetAllAsync();
                ViewBag.Brands = new SelectList(brands, "BrandId", "Name", product.BrandId);
                return PartialView("Update", product);
            }

            return RedirectToAction("Update", new { id = productId });
        }

        

    }
}
