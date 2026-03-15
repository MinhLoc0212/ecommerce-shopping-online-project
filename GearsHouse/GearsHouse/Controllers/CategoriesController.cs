using Microsoft.AspNetCore.Mvc;
using GearsHouse.Models;
using GearsHouse.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GearsHouse.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;

        // Constructor
        public CategoriesController(ICategoryRepository categoryRepository, ApplicationDbContext context)
        {
            _categoryRepository = categoryRepository;
            _context = context;
        }

        // GET: /Categories/Index
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return View(categories);
        }

        // GET: /Categories/Add
        [Authorize(Roles = "Admin")]
        public IActionResult AddCategory()
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("AddCategory");
            }
            return View();
        }

        // POST: /Categories/Add
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddCategory(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ViewBag.Message = "Tên danh mục không được để trống!";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView("AddCategory", category);
                }
                return View(category);
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Danh mục đã được thêm thành công!";

            // Lưu lại URL của trang quản lý danh mục
            TempData["RedirectUrl"] = Url.Action("CategoryIndex", "Dashboard");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var categories = await _categoryRepository.GetAllAsync();
                return PartialView("~/Views/Dashboard/_CategoryListDashboard.cshtml", categories);
            }
            return RedirectToAction("Dashboard", "Dashboard", new { tab = "category" }); // Quay lại Dashboard
        }



        // GET: /Categories/Update/{id}
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("UpdateCategory", category);
            }
            return View(category);
        }

        // POST: /Categories/Update/{id}
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory(int id, Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Danh mục đã được cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _categoryRepository.Exists(category.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var categories = await _categoryRepository.GetAllAsync();
                    return PartialView("~/Views/Dashboard/_CategoryListDashboard.cshtml", categories);
                }
                return RedirectToAction("Dashboard", "Dashboard", new { tab = "category" });
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("UpdateCategory", category);
            }
            return View(category);
        }


        // GET: /Categories/Delete/{id}
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }


        [HttpPost, ActionName("DeleteCategoryConfirmed")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategoryConfirmed(int id)
        {
            // Kiểm tra xem có sản phẩm nào thuộc danh mục này không
            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);

            if (hasProducts)
            {
                TempData["ErrorMessage"] = "Không thể xóa danh mục này vì còn sản phẩm liên quan.";
            }
            else
            {
                var category = await _categoryRepository.GetByIdAsync(id);
                if (category != null)
                {
                    await _categoryRepository.DeleteAsync(id);
                    TempData["SuccessMessage"] = "Danh mục đã được xóa thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy danh mục để xóa!";
                }
            }

            return RedirectToAction("Dashboard", "Dashboard", new { tab = "category" });
        }


    }
}
