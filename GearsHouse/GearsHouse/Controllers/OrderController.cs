using GearsHouse.Extensions;
//using WebBanHang.Data;
using GearsHouse.Models;
using GearsHouse.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize] // Chỉ cho phép người dùng đã đăng nhập truy cập
public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;
    
    public OrderController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentOrdersJson()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Json(new List<object>());

        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                id = o.Id,
                date = o.OrderDate.ToString("dd/MM/yyyy"),
                total = o.TotalPrice.ToString("C0"),
                status = o.OrderStatus.ToString(),
                itemCount = o.OrderDetails.Sum(od => od.Quantity)
            })
            .ToListAsync();

        return Json(orders);
    }

    public async Task<IActionResult> Tracking()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var originalOrder = await _context.Orders
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (originalOrder == null)
        {
            return NotFound();
        }

        var outOfStockProducts = new List<string>();
        int addedOrUpdatedCount = 0;

        foreach (var od in originalOrder.OrderDetails)
        {
            var product = await _context.Products.FindAsync(od.ProductId);
            if (product == null) continue; // bỏ qua sản phẩm không còn tồn tại

            // Nếu hết hàng, bỏ qua
            if (product.Quantity <= 0)
            {
                outOfStockProducts.Add(product.Name);
                continue;
            }

            // Áp dụng giá hiện hành (có/không khuyến mãi)
            var discountedPrice = await GetDiscountedPrice(product.Id, product.Price);

            // Tìm cart item hiện có
            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == product.Id);

            int desiredQty = od.Quantity + (existingItem?.Quantity ?? 0);
            // Giới hạn theo tồn kho
            int finalQty = Math.Min(desiredQty, product.Quantity);
            if (finalQty <= 0) continue;

            if (existingItem != null)
            {
                var previousQty = existingItem.Quantity;
                existingItem.Quantity = finalQty;
                existingItem.OriginalPrice = product.Price;
                existingItem.Price = discountedPrice;
                if (finalQty > previousQty)
                {
                    addedOrUpdatedCount++;
                }
            }
            else
            {
                var cartItem = new CartItemEntity
                {
                    UserId = userId,
                    ProductId = product.Id,
                    Name = product.Name,
                    OriginalPrice = product.Price,
                    Price = discountedPrice,
                    Quantity = Math.Max(1, Math.Min(od.Quantity, product.Quantity)),
                    ImageUrl = product.ImageUrl
                };
                _context.CartItems.Add(cartItem);
                addedOrUpdatedCount++;
            }
        }

        await _context.SaveChangesAsync();

        if (addedOrUpdatedCount > 0)
        {
            TempData["SuccessMessage"] = "Sản phẩm từ đơn hàng đã được thêm vào giỏ.";
            if (outOfStockProducts.Any())
            {
                TempData["WarningMessage"] = "Các sản phẩm sau đã hết hàng và không thể thêm: " + string.Join(", ", outOfStockProducts);
            }
        }
        else if (outOfStockProducts.Any())
        {
            TempData["WarningMessage"] = "Các sản phẩm sau đã hết hàng và không thể thêm: " + string.Join(", ", outOfStockProducts);
        }
        return RedirectToAction("Index", "ShoppingCart");
    }

    // Lấy giá sau khuyến mãi (nếu có) cho sản phẩm
    private async Task<decimal> GetDiscountedPrice(int productId, decimal price)
    {
        var promotions = await _context.Promotions
            .Where(p => p.ProductId == productId)
            .ToListAsync();

        var activePromotion = promotions.FirstOrDefault(p => p.IsActive);
        if (activePromotion != null)
        {
            decimal discountAmount = activePromotion.DiscountPercent / 100m;
            return price * (1 - discountAmount);
        }

        return price;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus orderStatus)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        order.OrderStatus = orderStatus;
        await _context.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var orders = await _context.Orders
                .Include(o => o.ApplicationUser)
                .Include(o => o.OrderDetails)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return PartialView("~/Views/Dashboard/_OrderListDashboard.cshtml", orders);
        }

        return RedirectToAction("Dashboard", "Dashboard", new { tab = "order" });
    }

    // Action để hiển thị danh sách đơn hàng cho admin
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> OrderManage()
    {
        var orders = await _context.Orders
            .Include(o => o.ApplicationUser)
            .Include(o => o.OrderDetails)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> OrderDetail(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product) 
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView(order);
        }

        return View(order);
    }
}
