using Microsoft.AspNetCore.Mvc;
using GearsHouse.Models;
using GearsHouse.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GearsHouse.Controllers
{
    public class BrandController : Controller
    {
        private readonly IBrandRepository _brandRepository;
        private readonly ApplicationDbContext _context;

        public BrandController(IBrandRepository brandRepository, ApplicationDbContext context)
        {
            _brandRepository = brandRepository;
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult AddBrand()
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("AddBrand");
            }
            return View();
        }

        // Thêm thương hiệu
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddBrand(Brand brand, IFormFile? LogoFile)
        {
            if (ModelState.IsValid)
            {
                if (LogoFile != null && LogoFile.Length > 0)
                {
                    // Handle file upload
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "brands");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + LogoFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await LogoFile.CopyToAsync(fileStream);
                    }
                    
                    brand.LogoUrl = "/images/brands/" + uniqueFileName;
                }

                await _brandRepository.AddAsync(brand);
                TempData["SuccessMessage"] = "Thương hiệu đã được thêm thành công!";

                // Lưu lại URL của trang quản lý thương hiệu
                TempData["RedirectUrl"] = Url.Action("BrandIndex", "Dashboard");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    // Trả về JSON để client biết đã thành công và reload lại danh sách
                    return Json(new { success = true, message = "Thương hiệu đã được thêm thành công!" });
                }

                return RedirectToAction("Dashboard", "Dashboard", new { tab = "brand" }); // Quay lại Dashboard
            }

            // Nếu không hợp lệ, trở lại với View hiện tại để người dùng sửa
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("AddBrand", brand);
            }
            return View(brand);
        }


        // Hiển thị form sửa thương hiệu
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBrand(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("UpdateBrand", brand);
            }
            return View(brand);
        }

        // Xử lý lưu thông tin sửa thương hiệu
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBrand(int id, Brand brand)
        {
            if (id != brand.BrandId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(brand);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Thương hiệu đã được cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BrandExists(brand.BrandId))
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
                    var brands = await _brandRepository.GetAllAsync();
                    return PartialView("~/Views/Dashboard/_BrandListDashboard.cshtml", brands);
                }
                return RedirectToAction("Dashboard", "Dashboard", new { tab = "brand" });
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("UpdateBrand", brand);
            }
            return View(brand);
        }


        // Kiểm tra xem thương hiệu có tồn tại trong CSDL không
        private bool BrandExists(int id)
        {
            return _context.Brands.Any(e => e.BrandId == id);
        }

        // Xử lý xóa thương hiệu
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBrand(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                return NotFound();
            }

            return View(brand);
        }

        [HttpPost, ActionName("DeleteBrandConfirmed")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBrandConfirmed(int id)
        {
            // Kiểm tra xem có sản phẩm nào thuộc thương hiệu này không
            bool hasProducts = await _context.Products.AnyAsync(p => p.BrandId == id);

            if (hasProducts)
            {
                TempData["ErrorMessage"] = "Không thể xóa thương hiệu này vì còn sản phẩm liên quan.";
            }
            else
            {
                var brand = await _context.Brands.FindAsync(id);
                if (brand != null)
                {
                    _context.Brands.Remove(brand);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Thương hiệu đã được xóa thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thương hiệu để xóa!";
                }
            }

            return RedirectToAction("Dashboard", "Dashboard", new { tab = "brand" });
        }



    }
}
