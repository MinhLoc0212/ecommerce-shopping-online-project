using System.Diagnostics;
using GearsHouse.Models;
using GearsHouse.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GearsHouse.Controllers
{
    public class HomeController : Controller
    {

        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public HomeController(IProductRepository productRepository, ICategoryRepository categoryRepository, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _userManager = userManager;
            _context = context;
        }

        


        public async Task<IActionResult> Index()
        {
            // Kiểm tra nếu user đã đăng nhập và là Admin thì chuyển hướng đến Dashboard
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Admin"))
                    {
                        return RedirectToAction("Dashboard", "Dashboard");
                    }
                }
            }
            var products = await _productRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categories;
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> FeaturedProducts(string mode = "best")
        {
            List<Product> products;

            if (string.Equals(mode, "best", StringComparison.OrdinalIgnoreCase))
            {
                var top = await _context.OrderDetails
                    .Include(od => od.Order)
                    .GroupBy(od => od.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        TotalSold = g.Sum(x => x.Quantity),
                        CompletedSold = g.Where(x => x.Order.OrderStatus == OrderStatus.HoanThanh).Sum(x => x.Quantity)
                    })
                    .OrderByDescending(x => x.CompletedSold)
                    .ThenByDescending(x => x.TotalSold)
                    .Take(8)
                    .ToListAsync();

                var ids = top.Select(x => x.ProductId).ToList();
                products = await _context.Products
                    .Where(p => ids.Contains(p.Id))
                    .ToListAsync();
                products = ids.Select(id => products.FirstOrDefault(p => p.Id == id)).Where(p => p != null).ToList()!;
            }
            else if (string.Equals(mode, "new", StringComparison.OrdinalIgnoreCase))
            {
                products = await _context.Products
                    .OrderByDescending(p => p.Id)
                    .Take(8)
                    .ToListAsync();
            }
            else if (string.Equals(mode, "sale", StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTime.Now;
                var promos = await _context.Promotions
                    .Where(p => now >= p.StartDate && now <= p.EndDate)
                    .OrderByDescending(p => p.DiscountPercent)
                    .ToListAsync();
                var ids = promos.Select(p => p.ProductId).Distinct().Take(8).ToList();
                products = await _context.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
                products = ids.Select(id => products.FirstOrDefault(p => p.Id == id)).Where(p => p != null).ToList()!;
            }
            else
            {
                products = await _context.Products
                    .OrderBy(p => p.Name)
                    .Take(8)
                    .ToListAsync();
            }

            var productIds = products.Select(p => p.Id).ToList();
            var reviews = await _context.Reviews
                .Where(r => productIds.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Average = g.Average(r => (double)r.Rating),
                    Count = g.Count()
                })
                .ToDictionaryAsync(k => k.ProductId, v => (v.Average, v.Count));

            var now2 = DateTime.Now;
            var activePromos = await _context.Promotions
                .Where(p => productIds.Contains(p.ProductId) && now2 >= p.StartDate && now2 <= p.EndDate)
                .ToListAsync();

            var result = products.Select(p =>
            {
                var promo = activePromos.FirstOrDefault(ap => ap.ProductId == p.Id);
                decimal? discounted = promo != null ? p.Price * (1 - promo.DiscountPercent / 100m) : (decimal?)null;
                var stat = reviews.ContainsKey(p.Id) ? reviews[p.Id] : (0.0, 0);
                return new
                {
                    id = p.Id,
                    name = p.Name,
                    imageUrl = p.ImageUrl,
                    price = p.Price,
                    discountedPrice = discounted,
                    discountPercent = promo?.DiscountPercent,
                    ratingAverage = stat.Item1,
                    ratingCount = stat.Item2,
                    quantity = p.Quantity
                };
            }).ToList();

            return Json(result);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public async Task<IActionResult> NextFortySale()
        {
            var now = DateTime.Now;
            var active = await _context.Promotions
                .Where(p => p.DiscountPercent == 40 && now >= p.StartDate && now <= p.EndDate)
                .OrderBy(p => p.EndDate)
                .FirstOrDefaultAsync();
            if (active != null)
            {
                return Json(new { target = active.EndDate, status = "active", id = active.Id });
            }
            var upcoming = await _context.Promotions
                .Where(p => p.DiscountPercent == 40 && p.StartDate > now)
                .OrderBy(p => p.StartDate)
                .FirstOrDefaultAsync();
            if (upcoming != null)
            {
                return Json(new { target = upcoming.StartDate, status = "upcoming", id = upcoming.Id });
            }
            return Json(new { target = (DateTime?)null, status = "none" });
        }

        [HttpGet]
        public async Task<IActionResult> TrendingProducts(int count = 12)
        {
            var now = DateTime.Now;
            var activePromos = await _context.Promotions
                .Where(p => now >= p.StartDate && now <= p.EndDate)
                .ToListAsync();
            var products = await _context.Products
                .OrderBy(p => Guid.NewGuid())
                .Take(count)
                .ToListAsync();
            var result = products.Select(p =>
            {
                var promo = activePromos.FirstOrDefault(ap => ap.ProductId == p.Id);
                decimal? discounted = promo != null ? p.Price * (1 - promo.DiscountPercent / 100m) : (decimal?)null;
                return new
                {
                    id = p.Id,
                    name = p.Name,
                    imageUrl = p.ImageUrl,
                    price = p.Price,
                    discountedPrice = discounted
                };
            }).ToList();
            return Json(result);
        }
    }
}
