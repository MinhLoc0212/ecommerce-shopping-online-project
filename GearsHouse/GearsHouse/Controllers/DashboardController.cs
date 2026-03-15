using GearsHouse.Models;
using GearsHouse.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public DashboardController(IProductRepository productRepository, ICategoryRepository categoryRepository, IBrandRepository brandRepository, ApplicationDbContext context
        , UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _brandRepository = brandRepository;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }
    public async Task<IActionResult> Dashboard(string tab = "")
    {
        // Nếu không có tab được chỉ định, chuyển hướng sang tab=revenue
        if (string.IsNullOrEmpty(tab))
        {
            return RedirectToAction("Dashboard", new { tab = "revenue" });
        }

        var user = await _userManager.GetUserAsync(User);
        ViewBag.UserName = user?.FullName ?? user?.UserName ?? "Admin";
        ViewBag.UserEmail = user?.Email;

        ViewBag.ActiveTab = tab.ToLower();
        return View("~/Views/Home/Dashboard.cshtml");
    }


    public async Task<IActionResult> ProductIndex(string search, int? categoryId, int? brandId, string status)
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "product" });

        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .AsNoTracking()
            .AsQueryable();

        // Filtering
        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(search) || (p.ProductInfo != null && p.ProductInfo.ToLower().Contains(search)));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (brandId.HasValue && brandId.Value > 0)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            if (status == "active")
                query = query.Where(p => p.Quantity > 0);
            else if (status == "out_of_stock")
                query = query.Where(p => p.Quantity <= 0);
        }

        var products = await query.ToListAsync();

        // Populate ViewBags
        ViewBag.Categories = await _categoryRepository.GetAllAsync();
        ViewBag.Brands = await _brandRepository.GetAllAsync();

        return PartialView("_ProductListDashboard", products);
    }

    public async Task<IActionResult> CategoryIndex(string search)
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "category" });

        var query = _context.Categories.Include(c => c.Products).AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(search));
        }

        var categories = await query.ToListAsync();
        return PartialView("_CategoryListDashboard", categories);
    }

    public async Task<IActionResult> BrandIndex(string search)
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "brand" });

        var query = _context.Brands.Include(b => b.Products).AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(search));
        }

        var brands = await query.ToListAsync();
        return PartialView("_BrandListDashboard", brands);
    }

    public async Task<IActionResult> PromotionIndex()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "promotion" });

        var promotions = await _context.Promotions.Include(p => p.Product).ToListAsync();
        return PartialView("_PromotionListDashboard", promotions);
    }

    public async Task<IActionResult> OrderIndex()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "order" });

        var orders = await _context.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return PartialView("_OrderListDashboard", orders);
    }

    public async Task<IActionResult> UserIndex()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "user" });

        var query = from user in _context.Users
                    join userRole in _context.UserRoles on user.Id equals userRole.UserId into ur
                    from userRole in ur.DefaultIfEmpty()
                    join role in _context.Roles on userRole.RoleId equals role.Id into r
                    from role in r.DefaultIfEmpty()
                    select new { User = user, RoleName = role.Name };

        var data = await query.AsNoTracking().ToListAsync();

        var model = data.GroupBy(x => x.User.Id)
                        .Select(g => new UserRoleViewModel
                        {
                            UserId = g.Key,
                            FullName = g.First().User.FullName,
                            Email = g.First().User.Email,
                            CurrentRoles = g.Where(x => x.RoleName != null).Select(x => x.RoleName).Distinct().ToList()
                        }).ToList();

        return PartialView("_UserListDashboard", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(string userId, string role)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            TempData["ErrorMessage"] = "Thiếu thông tin người dùng hoặc vai trò.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_UserListDashboard", await GetUsersWithRolesAsync());
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_UserListDashboard", await GetUsersWithRolesAsync());
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        // Đảm bảo role tồn tại
        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole(role));
        }

        // Xóa hết vai trò hiện tại và gán vai trò mới (chính sách 1 vai trò)
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        var addRes = await _userManager.AddToRoleAsync(user, role);
        if (addRes.Succeeded)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật vai trò người dùng thành '{role}'.";
        }
        else
        {
            TempData["ErrorMessage"] = "Cập nhật vai trò thất bại.";
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_UserListDashboard", await GetUsersWithRolesAsync());
        }
        return RedirectToAction("Dashboard", new { tab = "user" });
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["ErrorMessage"] = "Thiếu thông tin người dùng.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_UserListDashboard", await GetUsersWithRolesAsync());
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == userId)
        {
            TempData["ErrorMessage"] = "Bạn không thể tự xóa tài khoản của mình.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_UserListDashboard", await GetUsersWithRolesAsync());
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_UserListDashboard", await GetUsersWithRolesAsync());
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        var res = await _userManager.DeleteAsync(user);
        if (res.Succeeded)
        {
            TempData["SuccessMessage"] = "Đã xóa người dùng.";
        }
        else
        {
            TempData["ErrorMessage"] = "Xóa người dùng thất bại.";
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_UserListDashboard", await GetUsersWithRolesAsync());
        }
        return RedirectToAction("Dashboard", new { tab = "user" });
    }

    public async Task<IActionResult> Revenue()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "revenue" });

        var completedOrders = _context.Orders.AsNoTracking().Where(o => o.OrderStatus == OrderStatus.HoanThanh);

        // Tính tổng doanh thu chỉ tính các đơn hàng có trạng thái Hoàn Thành
        var totalRevenue = await completedOrders.SumAsync(o => o.TotalPrice);

        // Tính tổng số đơn hàng đã hoàn thành
        var totalOrders = await completedOrders.CountAsync();

        // Tính doanh thu trung bình mỗi đơn hàng
        var averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

        // Tính tổng số tiền giảm giá (nếu có)
        var totalDiscount = await _context.Promotions.AsNoTracking()
            .Where(p => DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate)
            .SumAsync(p => p.DiscountPercent);

        // Tính Top 4 danh mục bán chạy nhất
        var topCategories = await _context.OrderDetails.AsNoTracking()
            .Where(od => od.Order.OrderStatus == OrderStatus.HoanThanh)
            .GroupBy(od => od.Product.Category.Name)
            .Select(g => new
            {
                CategoryName = g.Key,
                TotalSold = g.Sum(od => od.Quantity)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(4)
            .ToListAsync();

        var totalItemsSold = topCategories.Sum(c => c.TotalSold);
        var topSellingCategories = topCategories.Select(c => new TopCategory
        {
            CategoryName = c.CategoryName,
            TotalSold = c.TotalSold,
            Percentage = totalItemsSold > 0 ? Math.Round((double)c.TotalSold / totalItemsSold * 100, 1) : 0
        }).ToList();

        // Tính Top 5 sản phẩm bán chạy nhất
        var topProductsQuery = await _context.OrderDetails.AsNoTracking()
            .Include(od => od.Product)
            .ThenInclude(p => p.Category)
            .Where(od => od.Order.OrderStatus == OrderStatus.HoanThanh)
            .GroupBy(od => od.ProductId)
            .Select(g => new
            {
                Product = g.First().Product,
                TotalSold = g.Sum(od => od.Quantity),
                TotalRevenue = g.Sum(od => od.Quantity * od.Price)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(5)
            .ToListAsync();

        var topSellingProducts = topProductsQuery.Select(p => new TopProduct
        {
            ProductName = p.Product.Name,
            ImageUrl = p.Product.ImageUrl,
            Price = p.Product.Price,
            CategoryName = p.Product.Category?.Name ?? "Unknown",
            TotalSold = p.TotalSold,
            TotalRevenue = p.TotalRevenue
        }).ToList();

        // Tạo ViewModel và trả dữ liệu
        var revenueViewModel = new RevenueViewModel
        {
            TotalRevenue = totalRevenue,
            TotalOrders = totalOrders,  // Đây là tổng số đơn hàng
            AverageOrderValue = averageOrderValue,
            TotalDiscount = totalDiscount,
            DailyRevenueData = await GetDailyRevenue(),  // Tính doanh thu theo ngày (nếu có)
            TopSellingCategories = topSellingCategories,
            TopSellingProducts = topSellingProducts
        };

        return View(revenueViewModel);
    }

    public async Task<List<DailyRevenue>> GetDailyRevenue()
    {
        var last30Days = DateTime.Now.AddDays(-30);
        // Lấy dữ liệu doanh thu của từng ngày (30 ngày gần nhất)
        var dailyRevenue = await _context.Orders.AsNoTracking()
            .Where(o => o.OrderStatus == OrderStatus.HoanThanh && o.OrderDate >= last30Days)
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new DailyRevenue
            {
                Date = g.Key,
                TotalRevenue = g.Sum(o => o.TotalPrice)
            })
            .OrderBy(dr => dr.Date)
            .ToListAsync();

        return dailyRevenue;
    }

    private async Task<List<UserRoleViewModel>> GetUsersWithRolesAsync()
    {
        var query = from user in _context.Users
                    join userRole in _context.UserRoles on user.Id equals userRole.UserId into ur
                    from userRole in ur.DefaultIfEmpty()
                    join role in _context.Roles on userRole.RoleId equals role.Id into r
                    from role in r.DefaultIfEmpty()
                    select new { User = user, RoleName = role.Name };

        var data = await query.AsNoTracking().ToListAsync();

        return data.GroupBy(x => x.User.Id)
            .Select(g => new UserRoleViewModel
            {
                UserId = g.Key,
                FullName = g.First().User.FullName,
                Email = g.First().User.Email,
                CurrentRoles = g.Where(x => x.RoleName != null).Select(x => x.RoleName).Distinct().ToList()
            }).ToList();
    }

}


