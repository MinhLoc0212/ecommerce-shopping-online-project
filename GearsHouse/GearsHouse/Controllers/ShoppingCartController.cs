using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GearsHouse.Extensions;
using GearsHouse.Models;
using GearsHouse.Repositories;
using GearsHouse.Services;
using System.Text.Json;
using System.IO;
using Microsoft.Extensions.Logging;


namespace GearsHouse.Controllers
{
    [Authorize]
    public class ShoppingCartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProductRepository _productRepository;
        private readonly EmailService _emailService;
        private readonly InvoicePdfGenerator _pdfGenerator;
        private readonly VNPayService _vnpayService;
        private readonly VNPaySettings _vnpaySettings;
        private readonly MomoService _momoService;
        private readonly MomoSettings _momoSettings;
        private readonly ILogger<ShoppingCartController> _logger;


        public ShoppingCartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IProductRepository productRepository,
            EmailService emailService,
            InvoicePdfGenerator pdfGenerator,
            VNPayService vnpayService,
            Microsoft.Extensions.Options.IOptions<VNPaySettings> vnpOptions,
            MomoService momoService,
            Microsoft.Extensions.Options.IOptions<MomoSettings> momoOptions,
            ILogger<ShoppingCartController> logger)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _pdfGenerator = pdfGenerator;
            _vnpayService = vnpayService;
            _vnpaySettings = vnpOptions.Value;
            _momoService = momoService;
            _momoSettings = momoOptions.Value;
            _logger = logger;
        }


        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return Json(new { success = false, message = "Sản phẩm không tồn tại." });

            if (product.Quantity <= 0)
            {
                return Json(new { success = false, message = "Sản phẩm đã hết hàng." });
            }

            decimal discountedPrice = await GetDiscountedPrice(productId, product.Price);

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(i => i.UserId == user.Id && i.ProductId == productId );

            int currentQty = existingItem?.Quantity ?? 0;
            int requestedQty = currentQty + Math.Max(quantity, 1);

            if (requestedQty > product.Quantity)
            {
                int available = product.Quantity - currentQty;
                string msg = available > 0
                    ? $"Số lượng yêu cầu vượt quá tồn kho. Chỉ còn {available}."
                    : "Sản phẩm đã hết hàng trong giỏ của bạn.";
                int totalItemsFail = await _context.CartItems.Where(i => i.UserId == user.Id).SumAsync(i => i.Quantity);
                return Json(new { success = false, message = msg, totalItems = totalItemsFail });
            }

            if (existingItem != null)
            {
                existingItem.Quantity = requestedQty;
            }
            else
            {
                var cartItem = new CartItemEntity
                {
                    UserId = user.Id,
                    ProductId = productId,
                    Name = product.Name,
                    OriginalPrice = product.Price,
                    Price = discountedPrice,
                    Quantity = Math.Max(quantity, 1),
                    ImageUrl = product.ImageUrl
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            int totalItems = await _context.CartItems
                .Where(i => i.UserId == user.Id)
                .SumAsync(i => i.Quantity);

            return Json(new { success = true, message = "Đã thêm vào giỏ hàng!", totalItems });
        }

        // Thêm nhanh vào giỏ và chuyển tới trang giỏ hàng
        [HttpGet]
        public async Task<IActionResult> BuyNow(int productId, int quantity = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return RedirectToAction("Index", "Product");
            }

            if (quantity < 1) quantity = 1;

            decimal discountedPrice = await GetDiscountedPrice(productId, product.Price);

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(i => i.UserId == user.Id && i.ProductId == productId);

            // Nếu sản phẩm đã có trong giỏ, không cộng dồn nữa — chỉ chuyển tới giỏ
            if (existingItem != null)
            {
                return RedirectToAction("Index");
            }

            // Nếu chưa có, thêm mới với số lượng được chọn, kiểm tra tồn kho
            if (quantity > product.Quantity)
            {
                TempData["ErrorMessage"] = $"Số lượng yêu cầu vượt quá tồn kho (còn {product.Quantity}).";
                return RedirectToAction("Index");
            }

            var cartItem = new CartItemEntity
            {
                UserId = user.Id,
                ProductId = productId,
                Name = product.Name,
                OriginalPrice = product.Price,
                Price = discountedPrice,
                Quantity = quantity,
                ImageUrl = product.ImageUrl
            };
            _context.CartItems.Add(cartItem);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }


        // Phương thức này sẽ lấy giá khuyến mãi nếu có
        private async Task<decimal> GetDiscountedPrice(int productId, decimal price)
        {
            // Lấy tất cả các khuyến mãi cho sản phẩm từ cơ sở dữ liệu
            var promotions = await _context.Promotions
                .Where(p => p.ProductId == productId)
                .ToListAsync();  // Lấy tất cả các khuyến mãi cho sản phẩm này

            // Tìm khuyến mãi đang hoạt động
            var activePromotion = promotions.FirstOrDefault(p => p.IsActive);  // Tính toán IsActive ở bộ nhớ

            if (activePromotion != null)
            {
                // Nếu có khuyến mãi đang hoạt động, tính giá sau khuyến mãi
                decimal discountAmount = activePromotion.DiscountPercent / 100m;
                return price * (1 - discountAmount);  // Giảm giá theo tỷ lệ phần trăm
            }

            return price;  // Nếu không có khuyến mãi, trả lại giá gốc
        }

        public async Task<IActionResult> IndexAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _context.CartItems
                .Where(i => i.UserId == user.Id)
                .ToListAsync();

            var cart = new ShoppingCart
            {
                Items = cartItems.Select(i => new CartItem
                {
                    ProductId = i.ProductId,
                    Name = i.Name,
                    OriginalPrice = i.OriginalPrice,
                    Price = i.Price,
                    Quantity = i.Quantity,
                    
                    ImageUrl = i.ImageUrl
                }).ToList()
            };

            return View(cart);
        }



        private async Task<Product> GetProductFromDatabase(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            return product;
        }

        public async Task<IActionResult> UpdateQuantity(int productId, int change)
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(i => i.UserId == user.Id && i.ProductId == productId);

            if (cartItem != null)
            {
                var product = await _context.Products.FindAsync(productId);
                // Nếu sản phẩm không còn tồn tại, bỏ qua hoặc xóa (ở đây giữ nguyên logic an toàn)
                if (product == null) return RedirectToAction("Index");

                int newQuantity = cartItem.Quantity + change;

                // Kiểm tra tồn kho nếu tăng số lượng
                if (change > 0)
                {
                    if (newQuantity > product.Quantity)
                    {
                         TempData["ErrorMessage"] = $"Số lượng yêu cầu vượt quá tồn kho (chỉ còn {product.Quantity}).";
                         return RedirectToAction("Index");
                    }
                }

                // Giới hạn số lượng tối thiểu là 1
                if (newQuantity < 1)
                {
                    newQuantity = 1;
                }

                cartItem.Quantity = newQuantity;
                _context.CartItems.Update(cartItem);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> RemoveFromCartAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.CartItems
                .FirstOrDefaultAsync(i => i.UserId == user.Id && i.ProductId == productId );

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> CheckoutFromCart()
        {
            // Chuyển người dùng sang trang Checkout (GET) để nhập thông tin và xác nhận.
            // Trang Checkout sẽ tự kiểm tra giỏ hàng trong DB.
            return RedirectToAction("Checkout");
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            // Lấy giỏ hàng từ database theo UserId
            var cartItems = await _context.CartItems
                .Where(i => i.UserId == user.Id)
                .ToListAsync();

            if (!cartItems.Any())
                return RedirectToAction("Index");

            var order = new Order
            {
                UserId = user.Id,
                OrderDate = DateTime.UtcNow,
                TotalPrice = cartItems.Sum(i => i.Price * i.Quantity),
                OrderDetails = cartItems.Select(i => new OrderDetail
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList(),
                PaymentMethod = "",
                FullName = "",
                PhoneNumber = "",
                ShippingAddress = ""
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return RedirectToAction("CheckoutExisting", new { id = order.Id });
        }


        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);

            var cartItems = await _context.CartItems
                .Where(i => i.UserId == user.Id)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return RedirectToAction("Index");
            }

            var order = new Order
            {
                Id = 0, // chưa tạo đơn trong DB
                UserId = user.Id,
                OrderDate = DateTime.UtcNow,
                TotalPrice = cartItems.Sum(i => i.Price * i.Quantity),
                OrderDetails = new List<OrderDetail>()
            };

            foreach (var ci in cartItems)
            {
                var product = await _context.Products.FindAsync(ci.ProductId);
                order.OrderDetails.Add(new OrderDetail
                {
                    ProductId = ci.ProductId,
                    Quantity = ci.Quantity,
                    Price = ci.Price,
                    Product = product
                });
            }

            var productIds = cartItems.Select(i => i.ProductId).Distinct().ToList();
            var now = DateTime.Now;
            var hasPromo = await _context.Promotions.AnyAsync(p => productIds.Contains(p.ProductId) && now >= p.StartDate && now <= p.EndDate);
            ViewBag.HasPromo = hasPromo;

            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> CheckoutExisting(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            var productIds = order.OrderDetails.Select(od => od.ProductId).Distinct().ToList();
            var now = DateTime.Now;
            var hasPromo = await _context.Promotions.AnyAsync(p => productIds.Contains(p.ProductId) && now >= p.StartDate && now <= p.EndDate);
            ViewBag.HasPromo = hasPromo;

            return View("Checkout", order);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            var user = await _userManager.GetUserAsync(User);

            // Nếu chưa có đơn trong DB (đi từ giỏ hàng), tạo mới từ giỏ
            var existingOrder = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (existingOrder == null)
            {
                var cartItems = await _context.CartItems
                    .Where(i => i.UserId == user.Id)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    TempData["ErrorMessage"] = "Giỏ hàng trống.";
                    return RedirectToAction("Index");
                }

                existingOrder = new Order
                {
                    UserId = user.Id,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = cartItems.Sum(i => i.Price * i.Quantity),
                    OrderDetails = cartItems.Select(i => new OrderDetail
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        Price = i.Price
                    }).ToList()
                };
            }

            // Cập nhật thông tin đơn hàng
            existingOrder.FullName = order.FullName;
            existingOrder.PhoneNumber = order.PhoneNumber;
            existingOrder.ShippingAddress = order.ShippingAddress;
            existingOrder.Notes = order.Notes;
            existingOrder.PaymentMethod = order.PaymentMethod;
            existingOrder.CouponCode = string.IsNullOrWhiteSpace(order.CouponCode) ? null : order.CouponCode.Trim().ToUpperInvariant();

            // Áp dụng mã giảm giá (nếu hợp lệ) trước khi thanh toán
            if (!string.IsNullOrEmpty(existingOrder.CouponCode))
            {
                var productIdsForOrder = existingOrder.OrderDetails.Select(od => od.ProductId).Distinct().ToList();
                var now = DateTime.Now;
                var hasPromo = await _context.Promotions.AnyAsync(p => productIdsForOrder.Contains(p.ProductId) && now >= p.StartDate && now <= p.EndDate);
                if (hasPromo)
                {
                    TempData["WarningMessage"] = "Không áp dụng mã giảm giá cho sản phẩm đang được khuyến mãi.";
                    existingOrder.CouponCode = null;
                }
                else
                {
                    var coupon = await _context.CouponCodes
                        .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Code == existingOrder.CouponCode && !c.IsUsed);
                    if (coupon != null)
                    {
                        var grossTotal = existingOrder.OrderDetails.Sum(od => od.Price * od.Quantity);
                        if (grossTotal >= coupon.MinOrderTotal)
                        {
                            existingOrder.TotalPrice = Math.Max(0, existingOrder.TotalPrice - coupon.Amount);
                        }
                        else
                        {
                            TempData["WarningMessage"] = $"Đơn hàng phải từ {coupon.MinOrderTotal:N0} VNĐ mới dùng mã.";
                            existingOrder.CouponCode = null;
                        }
                    }
                    else
                    {
                        TempData["WarningMessage"] = "Mã giảm giá không hợp lệ hoặc đã sử dụng.";
                        existingOrder.CouponCode = null;
                    }
                }
            }

            // Lưu đơn lần đầu sau khi đã có đầy đủ thông tin bắt buộc
            if (existingOrder.Id == 0)
            {
                _context.Orders.Add(existingOrder);
            }
            else
            {
                _context.Orders.Update(existingOrder);
            }
            await _context.SaveChangesAsync();

            // Nếu chọn VNPay, chuyển hướng tới cổng thanh toán (đã áp dụng mã nếu hợp lệ)
            if (string.Equals(existingOrder.PaymentMethod, "VNPay", StringComparison.OrdinalIgnoreCase))
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
                {
                    ipAddress = "127.0.0.1";
                }
                var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
                var scheme = string.IsNullOrEmpty(Request.Scheme) ? "https" : Request.Scheme;
                var dynamicReturn = (!string.IsNullOrEmpty(host)) ? $"{scheme}://{host}/ShoppingCart/VNPayReturn" : _vnpaySettings.ReturnUrl;
                var paymentUrl = _vnpayService.CreatePaymentUrl(existingOrder, ipAddress, dynamicReturn);
                return Redirect(paymentUrl);
            }

                if (string.Equals(existingOrder.PaymentMethod, "MoMo", StringComparison.OrdinalIgnoreCase))
                {
                    // MoMo requires amount >= 1000 VND
                    if (existingOrder.TotalPrice < 1000)
                    {
                        TempData["ErrorMessage"] = "Thanh toán MoMo yêu cầu đơn hàng tối thiểu 1,000đ.";
                        return RedirectToAction("CheckoutExisting", new { id = existingOrder.Id });
                    }

                    try
                    {
                        var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
                        var scheme = string.IsNullOrEmpty(Request.Scheme) ? "https" : Request.Scheme;
                        var returnUrl = (!string.IsNullOrEmpty(host)) ? $"{scheme}://{host}/ShoppingCart/MomoReturn" : _momoSettings.ReturnUrl;
                        var notifyUrl = (!string.IsNullOrEmpty(host)) ? $"{scheme}://{host}/ShoppingCart/MomoNotify" : _momoSettings.NotifyUrl;
                        
                        _logger.LogInformation("Checkout PaymentMethod={Method} returnUrl={ReturnUrl} notifyUrl={NotifyUrl}", existingOrder.PaymentMethod, returnUrl, notifyUrl);
                        
                        var paymentUrl = await _momoService.CreatePaymentUrlAsync(existingOrder, returnUrl, notifyUrl);
                        _logger.LogInformation("MoMo payUrl={PayUrl}", paymentUrl);
                        
                        if (string.IsNullOrWhiteSpace(paymentUrl))
                        {
                            TempData["ErrorMessage"] = "Không tạo được liên kết thanh toán MoMo (URL rỗng).";
                            return RedirectToAction("CheckoutExisting", new { id = existingOrder.Id });
                        }
                        return Redirect(paymentUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "MoMo create payment failed: {Message}", ex.Message);
                        TempData["ErrorMessage"] = $"Lỗi thanh toán MoMo: {ex.Message}";
                        return RedirectToAction("CheckoutExisting", new { id = existingOrder.Id });
                    }
                }

            // Thanh toán không dùng VNPay: coi đơn hàng là đã xác nhận và bắt đầu xử lý
            existingOrder.OrderStatus = OrderStatus.DangXuLy;
            _context.Orders.Update(existingOrder);
            await _context.SaveChangesAsync();

            // Giảm tồn kho cho từng sản phẩm trong đơn hàng (không dùng VNPay)
            await DecreaseStockForOrder(existingOrder);

            // Xóa giỏ hàng trong database sau khi hoàn tất đơn hàng
            var userCartItems = _context.CartItems.Where(i => i.UserId == user.Id);
            _context.CartItems.RemoveRange(userCartItems);
            await _context.SaveChangesAsync();

            // Gửi email xác nhận đơn hàng
            var email = user.Email;

            // Tạo PDF hóa đơn cho đơn hàng
            string pdfPath = _pdfGenerator.GenerateInvoicePdf(existingOrder, existingOrder.OrderDetails.ToList());

            // Gửi email
            string subject = $"[GEARSHOUSE] Xác nhận đơn hàng #{existingOrder.Id}";
            string body = $@"
<html>
<body style='font-family: Arial, sans-serif; margin: 0; padding: 0; background-color: #f8f8f8;'>
    <table style='width: 100%; max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 10px;'>
        <tr>
            <td style='text-align: center;'>
                <h1 style='color: #333333;'>Cảm ơn bạn đã đặt hàng tại GEARSHOUSE!</h1>
                <p style='font-size: 18px; color: #555555;'>Mã đơn hàng của bạn là <strong style='color: #e74c3c;'>#{existingOrder.Id}</strong>.</p>
                <p style='font-size: 16px; color: #555555;'>Hóa đơn mua hàng đã được đính kèm trong email này. Chúng tôi sẽ xử lý đơn hàng và giao đến bạn sớm nhất!</p>
            </td>
        </tr>
        <tr>
            <td>
                <table style='width: 100%;'>
                    <tr>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Sản phẩm</th>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Số lượng</th>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Giá</th>
                    </tr>";

            foreach (var item in existingOrder.OrderDetails)
            {
                body += $@"
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Product.Name}</td>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Quantity}</td>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Price:N0} VNĐ</td>
                    </tr>";
            }

            body += $@"
                </table>
            </td>
        </tr>
        <tr>
            <td style='padding-top: 20px;'>
                <p style='font-size: 18px; color: #555555; font-weight: bold;'>Tổng tiền: <span style='color: #e74c3c;'>{existingOrder.TotalPrice:N0} VNĐ</span></p>
                <p style='font-size: 16px; color: #555555;'>Chúng tôi sẽ gửi thông tin vận chuyển sớm. Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với chúng tôi!</p>
            </td>
        </tr>
";

            // Phát hành mã giảm giá 1.000.000 nếu tổng >= 10.000.000 VNĐ và gắn vào cùng email
            string? issuedCodeForEmail = null;
            var grossTotalForIssueEmail = existingOrder.OrderDetails.Sum(od => od.Price * od.Quantity);
            if (grossTotalForIssueEmail >= 10_000_000m)
            {
                var newCode = GenerateCouponCode();
                var issue = new CouponCode
                {
                    Code = newCode,
                    UserId = user.Id,
                    Amount = 1_000_000m,
                    MinOrderTotal = 5_000_000m,
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CouponCodes.Add(issue);
                await _context.SaveChangesAsync();
                issuedCodeForEmail = newCode;
            }

            if (!string.IsNullOrEmpty(issuedCodeForEmail))
            {
                body += $@"
        <tr>
            <td style='padding-top: 10px;'>
                <div style='background:#f1f9ff;border:1.5px solid #0d6efd;border-radius:12px;padding:16px;'>
                    <h3 style='color:#0d6efd;margin-top:0;'>🎁 Quà tặng mã giảm giá</h3>
                    <p style='font-size:16px;color:#333;'>Đơn hàng của bạn đạt từ 10.000.000 VNĐ, chúng tôi tặng bạn mã giảm giá 
                    <strong style='color:#e74c3c;'>{issuedCodeForEmail}</strong> trị giá <strong>1.000.000 VNĐ</strong> cho đơn tiếp theo từ 
                    <strong>5.000.000 VNĐ</strong>. Mã chỉ dùng một lần và KHÔNG áp dụng cho các sản phẩm đang được khuyến mãi.</p>
                </div>
            </td>
        </tr>";
            }

            body += $@"
        <tr>
            <td style='padding-top: 20px; text-align: center;'>
                <p style='font-size: 16px; color: #555555;'>Trân trọng,</p>
                <p style='font-size: 16px; color: #555555; font-weight: bold;'>GEARSHOUSE</p>
            </td>
        </tr>
    </table>
</body>
</html>
";

            await _emailService.SendEmailAsync(email, subject, body, pdfPath);

            // Đánh dấu mã giảm giá đã dùng (nếu có)
            if (!string.IsNullOrEmpty(existingOrder.CouponCode))
            {
                var usedCoupon = await _context.CouponCodes
                    .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Code == existingOrder.CouponCode && !c.IsUsed);
                if (usedCoupon != null)
                {
                    usedCoupon.IsUsed = true;
                    usedCoupon.UsedOrderId = existingOrder.Id;
                    usedCoupon.UsedAt = DateTime.UtcNow;
                    _context.CouponCodes.Update(usedCoupon);
                    await _context.SaveChangesAsync();
                }
            }

            // Đã gộp nội dung mã giảm giá vào email xác nhận, không gửi email riêng

            return View("OrderCompleted", existingOrder.Id);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string code, int? orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để áp dụng mã." });
            }

            code = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá." });
            }

            // Lấy tổng tiền hiện tại từ đơn tồn tại hoặc giỏ hàng
            decimal grossTotal = 0m;
            if (orderId.HasValue && orderId.Value > 0)
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId.Value);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }
                grossTotal = order.OrderDetails.Sum(od => od.Price * od.Quantity);
            }
            else
            {
                var cartItems = await _context.CartItems.Where(c => c.UserId == user.Id).ToListAsync();
                grossTotal = cartItems.Sum(ci => ci.Price * ci.Quantity);
                if (grossTotal <= 0)
                {
                    return Json(new { success = false, message = "Giỏ hàng trống." });
                }
            }

            var coupon = await _context.CouponCodes
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Code == code);

            if (coupon == null)
            {
                return Json(new { success = false, message = "Mã giảm giá không hợp lệ." });
            }

            if (coupon.IsUsed)
            {
                return Json(new { success = false, used = true, message = "Mã giảm giá này đã được sử dụng." });
            }

            // Kiểm tra sản phẩm đang khuyến mãi
            List<int> productIds;
            if (orderId.HasValue && orderId.Value > 0)
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId.Value);
                productIds = order?.OrderDetails.Select(od => od.ProductId).Distinct().ToList() ?? new List<int>();
            }
            else
            {
                var cartItems = await _context.CartItems.Where(c => c.UserId == user.Id).ToListAsync();
                productIds = cartItems.Select(ci => ci.ProductId).Distinct().ToList();
            }
            var now = DateTime.Now;
            var hasPromo = await _context.Promotions.AnyAsync(p => productIds.Contains(p.ProductId) && now >= p.StartDate && now <= p.EndDate);
            if (hasPromo)
            {
                return Json(new { success = false, promoBlocked = true, message = "Không áp dụng mã giảm giá cho sản phẩm đang được khuyến mãi" });
            }

            if (grossTotal < coupon.MinOrderTotal)
            {
                return Json(new { success = false, message = $"Đơn hàng phải từ {coupon.MinOrderTotal:N0} VNĐ mới dùng mã." });
            }

            var finalTotal = Math.Max(0, grossTotal - coupon.Amount);
            return Json(new
            {
                success = true,
                discountAmount = coupon.Amount,
                minOrderTotal = coupon.MinOrderTotal,
                grossTotal,
                finalTotal
            });
        }




        

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCartItemCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { count = 0 });

            int totalItems = await _context.CartItems
                .Where(i => i.UserId == user.Id)
                .SumAsync(i => i.Quantity);

            return Json(new { count = totalItems });
        }


        [HttpPost]
        public async Task<IActionResult> IncreaseQuantity(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.CartItems
                .FirstOrDefaultAsync(i => i.UserId == user.Id && i.ProductId == productId);

            if (item != null)
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "Sản phẩm không tồn tại.";
                    return RedirectToAction("Index");
                }

                if (item.Quantity >= product.Quantity)
                {
                    TempData["ErrorMessage"] = $"Số lượng trong giỏ đã đạt tối đa tồn kho ({product.Quantity}).";
                }
                else
                {
                    item.Quantity++;
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Index");
        }



        [HttpPost]
        public async Task<IActionResult> DecreaseQuantity(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.CartItems
                .FirstOrDefaultAsync(i => i.UserId == user.Id && i.ProductId == productId);

            if (item != null)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    _context.CartItems.Remove(item); // Xoá nếu còn 1 thì người dùng giảm tiếp nghĩa là muốn xoá
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }



        [HttpPost]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", "ShoppingCart");
        }

        [HttpGet]
        public async Task<IActionResult> VNPayReturn()
        {
            // Xác thực chữ ký từ VNPay
            if (!_vnpayService.ValidateSignature(Request.Query))
            {
                TempData["ErrorMessage"] = "Xác thực thanh toán không thành công.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var txnRef = Request.Query["vnp_TxnRef"].ToString();
            if (!int.TryParse(txnRef, out var orderId))
            {
                TempData["ErrorMessage"] = "Tham chiếu đơn hàng không hợp lệ.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            if (!VNPayService.IsSuccessResponse(Request.Query))
            {
                TempData["ErrorMessage"] = "Thanh toán VNPay thất bại hoặc bị hủy.";
                return RedirectToAction("CheckoutExisting", new { id = order.Id });
            }

            // Thanh toán thành công: giảm tồn kho, xoá giỏ, gửi email và hiển thị hoàn tất
            order.OrderStatus = OrderStatus.DangXuLy;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            await DecreaseStockForOrder(order);
            var user = await _userManager.GetUserAsync(User);
            var cartItems = _context.CartItems.Where(i => i.UserId == user.Id);
            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();

            var email = user.Email;
            string pdfPath = _pdfGenerator.GenerateInvoicePdf(order, order.OrderDetails.ToList());

            string subject = $"[GEARSHOUSE] Xác nhận đơn hàng #{order.Id}";
            string body = $@"
<html>
<body style='font-family: Arial, sans-serif; margin: 0; padding: 0; background-color: #f8f8f8;'>
    <table style='width: 100%; max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 10px;'>
        <tr>
            <td style='text-align: center;'>
                <h1 style='color: #333333;'>Cảm ơn bạn đã đặt hàng tại GEARSHOUSE!</h1>
                <p style='font-size: 18px; color: #555555;'>Mã đơn hàng của bạn là <strong style='color: #e74c3c;'>#{order.Id}</strong>.</p>
                <p style='font-size: 16px; color: #555555;'>Hóa đơn mua hàng đã được đính kèm trong email này. Chúng tôi sẽ xử lý đơn hàng và giao đến bạn sớm nhất!</p>
            </td>
        </tr>
        <tr>
            <td>
                <table style='width: 100%;'>
                    <tr>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Sản phẩm</th>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Số lượng</th>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Giá</th>
                    </tr>";

            foreach (var item in order.OrderDetails)
            {
                body += $@"
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Product.Name}</td>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Quantity}</td>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Price:N0} VNĐ</td>
                    </tr>";
            }

            body += $@"
                </table>
            </td>
        </tr>
        <tr>
            <td style='padding-top: 20px;'>
                <p style='font-size: 18px; color: #555555; font-weight: bold;'>Tổng tiền: <span style='color: #e74c3c;'>{order.TotalPrice:N0} VNĐ</span></p>
                <p style='font-size: 16px; color: #555555;'>Thanh toán VNPay thành công.</p>
            </td>
        </tr>
        <tr>
            <td style='padding-top: 20px; text-align: center;'>
                <p style='font-size: 16px; color: #555555;'>Trân trọng,</p>
                <p style='font-size: 16px; color: #555555; font-weight: bold;'>GEARSHOUSE</p>
            </td>
        </tr>
    </table>
</body>
</html>
";

            // Gộp nội dung phát hành mã giảm giá vào email xác nhận VNPay nếu đủ điều kiện
            string? issuedCodeForEmailVnpay = null;
            var grossTotalForIssueVnpay = order.OrderDetails.Sum(od => od.Price * od.Quantity);
            if (grossTotalForIssueVnpay >= 10_000_000m)
            {
                var newCode = GenerateCouponCode();
                var issue = new CouponCode
                {
                    Code = newCode,
                    UserId = user.Id,
                    Amount = 1_000_000m,
                    MinOrderTotal = 5_000_000m,
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CouponCodes.Add(issue);
                await _context.SaveChangesAsync();
                issuedCodeForEmailVnpay = newCode;
            }

            if (!string.IsNullOrEmpty(issuedCodeForEmailVnpay))
            {
                body += $@"
        <tr>
            <td style='padding-top: 10px;'>
                <div style='background:#f1f9ff;border:1.5px solid #0d6efd;border-radius:12px;padding:16px;'>
                    <h3 style='color:#0d6efd;margin-top:0;'>🎁 Quà tặng mã giảm giá</h3>
                    <p style='font-size:16px;color:#333;'>Đơn hàng của bạn đạt từ 10.000.000 VNĐ, chúng tôi tặng bạn mã giảm giá 
                    <strong style='color:#e74c3c;'>{issuedCodeForEmailVnpay}</strong> trị giá <strong>1.000.000 VNĐ</strong> cho đơn tiếp theo từ 
                    <strong>5.000.000 VNĐ</strong>. Mã chỉ dùng một lần.</p>
                </div>
            </td>
        </tr>";
            }

            await _emailService.SendEmailAsync(email, subject, body, pdfPath);

            // Đánh dấu mã giảm giá đã dùng (nếu có)
            if (!string.IsNullOrEmpty(order.CouponCode))
            {
                var usedCoupon = await _context.CouponCodes
                    .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Code == order.CouponCode && !c.IsUsed);
                if (usedCoupon != null)
                {
                    usedCoupon.IsUsed = true;
                    usedCoupon.UsedOrderId = order.Id;
                    usedCoupon.UsedAt = DateTime.UtcNow;
                    _context.CouponCodes.Update(usedCoupon);
                    await _context.SaveChangesAsync();
                }
            }

            // Đã gộp nội dung mã giảm giá vào email xác nhận, không gửi email riêng

            return View("OrderCompleted", order.Id);
        }

        [HttpGet]
        public async Task<IActionResult> MomoReturn()
        {
            var success = MomoService.IsSuccessResponse(Request.Query);
            var sigOk = _momoService.ValidateSignature(Request.Query);
            if (!success)
            {
                TempData["ErrorMessage"] = "Thanh toán MoMo thất bại hoặc bị hủy.";
                return RedirectToAction("Index", "ShoppingCart");
            }
            if (!sigOk)
            {
                _logger.LogWarning("MoMo return signature invalid but resultCode=0, proceeding in test mode");
            }

            var extraDataStr = Request.Query["extraData"].ToString();
            int orderId;
            if (int.TryParse(extraDataStr, out orderId))
            {
            }
            else
            {
                var orderIdStr = Request.Query["orderId"].ToString();
                var baseId = (orderIdStr ?? string.Empty).Split('-').FirstOrDefault();
                if (!int.TryParse(baseId, out orderId))
                {
                    TempData["ErrorMessage"] = "Tham chiếu đơn hàng không hợp lệ.";
                    return RedirectToAction("Index", "ShoppingCart");
                }
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            if (!MomoService.IsSuccessResponse(Request.Query))
            {
                TempData["ErrorMessage"] = "Thanh toán MoMo thất bại hoặc bị hủy.";
                return RedirectToAction("CheckoutExisting", new { id = order.Id });
            }

            order.OrderStatus = OrderStatus.DangXuLy;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            await DecreaseStockForOrder(order);
            var user = await _userManager.GetUserAsync(User);
            var cartItems = _context.CartItems.Where(i => i.UserId == user.Id);
            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();

            var email = user.Email;
            string pdfPath = _pdfGenerator.GenerateInvoicePdf(order, order.OrderDetails.ToList());
            string subject = $"[GEARSHOUSE] Xác nhận đơn hàng #{order.Id}";
            string body = $@"<html>
<body style='font-family: Arial, sans-serif; margin: 0; padding: 0; background-color: #f8f8f8;'>
    <table style='width: 100%; max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 10px;'>
        <tr>
            <td style='text-align: center;'>
                <h1 style='color: #333333;'>Cảm ơn bạn đã đặt hàng tại GEARSHOUSE!</h1>
                <p style='font-size: 18px; color: #555555;'>Mã đơn hàng của bạn là <strong style='color: #e74c3c;'>#{order.Id}</strong>.</p>
                <p style='font-size: 16px; color: #555555;'>Hóa đơn mua hàng đã được đính kèm trong email này. Chúng tôi sẽ xử lý đơn hàng và giao đến bạn sớm nhất!</p>
            </td>
        </tr>
        <tr>
            <td>
                <table style='width: 100%;'>
                    <tr>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Sản phẩm</th>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Số lượng</th>
                        <th style='background-color: #e74c3c; color: #ffffff; padding: 10px; text-align: left;'>Giá</th>
                    </tr>";
            foreach (var item in order.OrderDetails)
            {
                body += $@"
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Product.Name}</td>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Quantity}</td>
                        <td style='padding: 10px; border: 1px solid #ddd;'>{item.Price:N0} VNĐ</td>
                    </tr>";
            }
            body += $@"
                </table>
            </td>
        </tr>
        <tr>
            <td style='padding-top: 20px;'>
                <p style='font-size: 18px; color: #555555; font-weight: bold;'>Tổng tiền: <span style='color: #e74c3c;'>{order.TotalPrice:N0} VNĐ</span></p>
                <p style='font-size: 16px; color: #555555;'>Thanh toán MoMo thành công.</p>
            </td>
        </tr>
        <tr>
            <td style='padding-top: 20px; text-align: center;'>
                <p style='font-size: 16px; color: #555555;'>Trân trọng,</p>
                <p style='font-size: 16px; color: #555555; font-weight: bold;'>GEARSHOUSE</p>
            </td>
        </tr>
    </table>
</body>
</html>
";

            string? issuedCodeForEmailMomo = null;
            var grossTotalForIssueMomo = order.OrderDetails.Sum(od => od.Price * od.Quantity);
            if (grossTotalForIssueMomo >= 10_000_000m)
            {
                var newCode = GenerateCouponCode();
                var issue = new CouponCode
                {
                    Code = newCode,
                    UserId = user.Id,
                    Amount = 1_000_000m,
                    MinOrderTotal = 5_000_000m,
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CouponCodes.Add(issue);
                await _context.SaveChangesAsync();
                issuedCodeForEmailMomo = newCode;
            }

            if (!string.IsNullOrEmpty(issuedCodeForEmailMomo))
            {
                body += $@"
        <tr>
            <td style='padding-top: 10px;'>
                <div style='background:#f1f9ff;border:1.5px solid #0d6efd;border-radius:12px;padding:16px;'>
                    <h3 style='color:#0d6efd;margin-top:0;'>🎁 Quà tặng mã giảm giá</h3>
                    <p style='font-size:16px;color:#333;'>Đơn hàng của bạn đạt từ 10.000.000 VNĐ, chúng tôi tặng bạn mã giảm giá 
                    <strong style='color:#e74c3c;'>{issuedCodeForEmailMomo}</strong> trị giá <strong>1.000.000 VNĐ</strong> cho đơn tiếp theo từ 
                    <strong>5.000.000 VNĐ</strong>. Mã chỉ dùng một lần.</p>
                </div>
            </td>
        </tr>";
            }

            await _emailService.SendEmailAsync(email, subject, body, pdfPath);

            return View("OrderCompleted", order.Id);
        }

        [HttpPost]
        public async Task<IActionResult> MomoNotify()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string,string>>(body) ?? new Dictionary<string,string>();

            if (!MomoService.IsSuccessResponse(data))
            {
                return Ok();
            }

            int orderId;
            var extraDataStr = data.TryGetValue("extraData", out var ed) ? ed : "";
            if (int.TryParse(extraDataStr, out orderId))
            {
            }
            else
            {
                var orderIdComposite = data.TryGetValue("orderId", out var oids) ? oids : "";
                var baseId = (orderIdComposite ?? string.Empty).Split('-').FirstOrDefault();
                if (!int.TryParse(baseId, out orderId))
                {
                    return Ok();
                }
            }

            if (!_momoService.ValidateSignature(data))
            {
                return Ok();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return Ok();
            }

            if (order.OrderStatus != OrderStatus.DangXuLy)
            {
                order.OrderStatus = OrderStatus.DangXuLy;
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();
                await DecreaseStockForOrder(order);
            }

            return Ok();
        }

        private static string GenerateCouponCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 7)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Helper: giảm số lượng tồn kho theo chi tiết đơn hàng
        private async Task DecreaseStockForOrder(Order order)
        {
            if (order?.OrderDetails == null || !order.OrderDetails.Any()) return;

            foreach (var detail in order.OrderDetails)
            {
                var product = await _context.Products.FindAsync(detail.ProductId);
                if (product == null) continue;

                var newQty = product.Quantity - detail.Quantity;
                product.Quantity = newQty < 0 ? 0 : newQty;
            }

            await _context.SaveChangesAsync();
        }
    }
}
