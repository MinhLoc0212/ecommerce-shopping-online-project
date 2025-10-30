using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GearsHouse.Models;

namespace GearsHouse.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PromotionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromotionController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var promotions = await _context.Promotions.Include(p => p.Product).ToListAsync();
            return View(promotions);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Create");
            }
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Promotion promotion)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView("Create", promotion);
                }
                return View(promotion);
            }

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Khuyến mãi đã được tạo thành công!";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var promotions = await _context.Promotions.Include(p => p.Product).ToListAsync();
                return PartialView("~/Views/Dashboard/_PromotionListDashboard.cshtml", promotions);
            }
            return RedirectToAction("Dashboard", "Dashboard", new { tab = "promotion" });
        }

        public async Task<IActionResult> Update(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null) return NotFound();
            ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", promotion.ProductId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Update", promotion);
            }
            return View(promotion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, Promotion promotion)
        {
            if (id != promotion.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(promotion);
                await _context.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var promotions = await _context.Promotions.Include(p => p.Product).ToListAsync();
                    return PartialView("~/Views/Dashboard/_PromotionListDashboard.cshtml", promotions);
                }
                return RedirectToAction("Dashboard", "Dashboard", new { tab = "promotion" });
            }
            ViewBag.Products = new SelectList(await _context.Products.ToListAsync(), "Id", "Name", promotion.ProductId);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("Update", promotion);
            }
            return View(promotion);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var promotion = await _context.Promotions.Include(p => p.Product).FirstOrDefaultAsync(p => p.Id == id);
            if (promotion == null) return NotFound();
            return View(promotion);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion != null)
            {
                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Dashboard", "Dashboard", new { tab = "promotion" });
        }
    }
}
