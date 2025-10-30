using GearsHouse.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GearsHouse.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(ReviewInputModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["ReviewErrors"] = "Bạn cần đăng nhập để gửi đánh giá.";
                return RedirectToAction("Display", "Product", new { id = input.ProductId });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["ReviewErrors"] = string.Join("; ", errors);
                return RedirectToAction("Display", "Product", new { id = input.ProductId });
            }

            var review = new Review
            {
                ProductId = input.ProductId,
                Rating = input.Rating,
                Comment = input.Comment,
                UserId = user.Id,
                UserName = user.UserName,
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Cảm ơn bạn đã đánh giá sản phẩm!";
            return RedirectToAction("Display", "Product", new { id = input.ProductId });
        }


    }
}
