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
    public IActionResult Dashboard(string tab = "")
    {
        // Nếu không có tab được chỉ định, chuyển hướng sang tab=revenue
        if (string.IsNullOrEmpty(tab))
        {
            return RedirectToAction("Dashboard", new { tab = "revenue" });
        }

        ViewBag.ActiveTab = tab.ToLower();
        return View("~/Views/Home/Dashboard.cshtml");
    }


    public async Task<IActionResult> ProductIndex()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "product" });

        var products = await _productRepository.GetAllAsync();
        return PartialView("_ProductListDashboard", products);
    }

    public async Task<IActionResult> CategoryIndex()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "category" });

        var categories = await _categoryRepository.GetAllAsync();
        return PartialView("_CategoryListDashboard", categories);
    }

    public async Task<IActionResult> BrandIndex()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "brand" });

        var brands = await _brandRepository.GetAllAsync();
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
            .Include(o => o.OrderDetails)
            .Include(o => o.ApplicationUser)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return PartialView("_OrderListDashboard", orders);
    }

    public async Task<IActionResult> UserIndex()
    {
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            return RedirectToAction("Dashboard", new { tab = "user" });

        var users = await _userManager.Users.ToListAsync();
        var model = new List<UserRoleViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            model.Add(new UserRoleViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                CurrentRoles = roles
            });
        }
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
                var usersAjax = await _userManager.Users.ToListAsync();
                var modelAjax = new List<UserRoleViewModel>();
                foreach (var userAjax in usersAjax)
                {
                    var rolesAjax = await _userManager.GetRolesAsync(userAjax);
                    modelAjax.Add(new UserRoleViewModel
                    {
                        UserId = userAjax.Id,
                        FullName = userAjax.FullName,
                        Email = userAjax.Email,
                        CurrentRoles = rolesAjax
                    });
                }
                return PartialView("_UserListDashboard", modelAjax);
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var usersAjax = await _userManager.Users.ToListAsync();
                var modelAjax = new List<UserRoleViewModel>();
                foreach (var userAjax in usersAjax)
                {
                    var rolesAjax = await _userManager.GetRolesAsync(userAjax);
                    modelAjax.Add(new UserRoleViewModel
                    {
                        UserId = userAjax.Id,
                        FullName = userAjax.FullName,
                        Email = userAjax.Email,
                        CurrentRoles = rolesAjax
                    });
                }
                return PartialView("_UserListDashboard", modelAjax);
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
            var users = await _userManager.Users.ToListAsync();
            var model = new List<UserRoleViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                model.Add(new UserRoleViewModel
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    CurrentRoles = roles
                });
            }
            return PartialView("_UserListDashboard", model);
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
                var usersAjax = await _userManager.Users.ToListAsync();
                var modelAjax = new List<UserRoleViewModel>();
                foreach (var userAjax in usersAjax)
                {
                    var rolesAjax = await _userManager.GetRolesAsync(userAjax);
                    modelAjax.Add(new UserRoleViewModel
                    {
                        UserId = userAjax.Id,
                        FullName = userAjax.FullName,
                        Email = userAjax.Email,
                        CurrentRoles = rolesAjax
                    });
                }
                return PartialView("_UserListDashboard", modelAjax);
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == userId)
        {
            TempData["ErrorMessage"] = "Bạn không thể tự xóa tài khoản của mình.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var usersAjax = await _userManager.Users.ToListAsync();
                var modelAjax = new List<UserRoleViewModel>();
                foreach (var userAjax in usersAjax)
                {
                    var rolesAjax = await _userManager.GetRolesAsync(userAjax);
                    modelAjax.Add(new UserRoleViewModel
                    {
                        UserId = userAjax.Id,
                        FullName = userAjax.FullName,
                        Email = userAjax.Email,
                        CurrentRoles = rolesAjax
                    });
                }
                return PartialView("_UserListDashboard", modelAjax);
            }
            return RedirectToAction("Dashboard", new { tab = "user" });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var usersAjax = await _userManager.Users.ToListAsync();
                var modelAjax = new List<UserRoleViewModel>();
                foreach (var userAjax in usersAjax)
                {
                    var rolesAjax = await _userManager.GetRolesAsync(userAjax);
                    modelAjax.Add(new UserRoleViewModel
                    {
                        UserId = userAjax.Id,
                        FullName = userAjax.FullName,
                        Email = userAjax.Email,
                        CurrentRoles = rolesAjax
                    });
                }
                return PartialView("_UserListDashboard", modelAjax);
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
            var users = await _userManager.Users.ToListAsync();
            var model = new List<UserRoleViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                model.Add(new UserRoleViewModel
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    CurrentRoles = roles
                });
            }
            return PartialView("_UserListDashboard", model);
        }
        return RedirectToAction("Dashboard", new { tab = "user" });
    }

    public async Task<IActionResult> Revenue()
    {
        // Tính tổng doanh thu chỉ tính các đơn hàng có trạng thái Hoàn Thành
        var totalRevenue = await _context.Orders
            .Where(o => o.OrderStatus == OrderStatus.HoanThanh) // Lọc các đơn hàng đã hoàn thành
            .SumAsync(o => o.TotalPrice);

        // Tính tổng số đơn hàng đã hoàn thành
        var totalOrders = await _context.Orders
            .Where(o => o.OrderStatus == OrderStatus.HoanThanh)
            .CountAsync(); // Lấy số lượng đơn hàng đã hoàn thành

        // Tính doanh thu trung bình mỗi đơn hàng
        var averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

        // Tính tổng số tiền giảm giá (nếu có)
        var totalDiscount = await _context.Promotions
            .Where(p => DateTime.Now >= p.StartDate && DateTime.Now <= p.EndDate)
            .SumAsync(p => p.DiscountPercent);

        // Tạo ViewModel và trả dữ liệu
        var revenueViewModel = new RevenueViewModel
        {
            TotalRevenue = totalRevenue,
            TotalOrders = totalOrders,  // Đây là tổng số đơn hàng
            AverageOrderValue = averageOrderValue,
            TotalDiscount = totalDiscount,
            DailyRevenueData = await GetDailyRevenue()  // Tính doanh thu theo ngày (nếu có)
        };

        return View(revenueViewModel);
    }

    public async Task<List<DailyRevenue>> GetDailyRevenue()
    {
        // Lấy dữ liệu doanh thu của từng ngày
        var dailyRevenue = await _context.Orders
            .Where(o => o.OrderStatus == OrderStatus.HoanThanh)  // Lọc các đơn hàng đã hoàn thành
            .GroupBy(o => o.OrderDate.Date)  // Nhóm theo ngày
            .Select(g => new DailyRevenue
            {
                Date = g.Key,
                TotalRevenue = g.Sum(o => o.TotalPrice)  // Tính tổng doanh thu theo ngày
            })
            .OrderBy(dr => dr.Date)  // Sắp xếp theo ngày
            .ToListAsync();

        return dailyRevenue;
    }

}


