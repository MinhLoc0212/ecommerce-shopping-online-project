using GearsHouse.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

public class UuDaiSinhVienController : Controller
{
    private static readonly List<UuDai> uuDaiSVList = new List<UuDai>
    {
        new UuDai { Id = 1, TieuDe = "Ưu đãi SV1", MoTa = "Chi tiết ưu đãi sinh viên 1", DuongDanAnh = "/image/sv1.webp" },
        new UuDai { Id = 2, TieuDe = "Ưu đãi SV2", MoTa = "Chi tiết ưu đãi sinh viên 2", DuongDanAnh = "/image/sv2.webp" },
        new UuDai { Id = 3, TieuDe = "Ưu đãi SV3", MoTa = "Chi tiết ưu đãi sinh viên 3", DuongDanAnh = "/image/sv3.webp" },
        new UuDai { Id = 4, TieuDe = "Ưu đãi SV4", MoTa = "Chi tiết ưu đãi sinh viên 4", DuongDanAnh = "/image/sv4.webp" }
    };

    public IActionResult ChiTiet(int id)
    {
        var uuDai = uuDaiSVList.FirstOrDefault(u => u.Id == id);
        if (uuDai == null)
            return NotFound();

        return View(uuDai);
    }
}
