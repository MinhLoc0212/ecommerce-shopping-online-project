using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GearsHouse.Extensions;
using GearsHouse.Models;
using GearsHouse.Repositories;
using GearsHouse.Services;


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
        


        public ShoppingCartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IProductRepository productRepository,
            EmailService emailService,
            InvoicePdfGenerator pdfGenerator,
            VNPayService vnpayService)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _pdfGenerator = pdfGenerator;
            _vnpayService = vnpayService;
            
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
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || !cart.Items.Any())
            {
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);

            var order = new Order
            {
                UserId = user.Id,
                OrderDate = DateTime.UtcNow,
                TotalPrice = cart.Items.Sum(i => i.Price * i.Quantity),
                OrderDetails = cart.Items.Select(i => new OrderDetail
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("Cart");

            return RedirectToAction("Checkout", new { orderId = order.Id });
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

            return RedirectToAction("Checkout", new { id = order.Id });
        }


        [HttpGet]
        public async Task<IActionResult> Checkout(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            var existingOrder = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (existingOrder == null)
                return NotFound();

            // Cập nhật thông tin đơn hàng
            existingOrder.FullName = order.FullName;
            existingOrder.PhoneNumber = order.PhoneNumber;
            existingOrder.ShippingAddress = order.ShippingAddress;
            existingOrder.Notes = order.Notes;
            existingOrder.PaymentMethod = order.PaymentMethod;

            _context.Orders.Update(existingOrder);
            await _context.SaveChangesAsync();

            // Nếu chọn VNPay, chuyển hướng tới cổng thanh toán
            if (string.Equals(existingOrder.PaymentMethod, "VNPay", StringComparison.OrdinalIgnoreCase))
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
                {
                    ipAddress = "127.0.0.1";
                }
                var returnUrl = $"{Request.Scheme}://{Request.Host}/ShoppingCart/VNPayReturn";
                var paymentUrl = _vnpayService.CreatePaymentUrl(existingOrder, ipAddress, returnUrl);
                return Redirect(paymentUrl);
            }

            // Giảm tồn kho cho từng sản phẩm trong đơn hàng (không dùng VNPay)
            await DecreaseStockForOrder(existingOrder);

            // Xóa giỏ hàng trong database sau khi hoàn tất đơn hàng
            var user = await _userManager.GetUserAsync(User);
            var cartItems = _context.CartItems.Where(i => i.UserId == user.Id);
            _context.CartItems.RemoveRange(cartItems);
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

            return View("OrderCompleted", existingOrder.Id);
        }




        

        [HttpGet]
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
                return RedirectToAction("Checkout", new { id = order.Id });
            }

            // Thanh toán thành công: giảm tồn kho, xoá giỏ, gửi email và hiển thị hoàn tất
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

            await _emailService.SendEmailAsync(email, subject, body, pdfPath);

            return View("OrderCompleted", order.Id);
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
