using GearsHouse.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using GearsHouse.Models;

public class UuDaiController : Controller
{
    private static readonly List<UuDai> uuDaiList = new List<UuDai>
    {
        new UuDai { Id = 1, TieuDe = "Ưu đãi TT1", MoTa = "Chi tiết ưu đãi thanh toán 1", DuongDanAnh = "/image/tt1.webp" },
        new UuDai { Id = 2, TieuDe = "Ưu đãi TT2", MoTa = "Chi tiết ưu đãi thanh toán 2", DuongDanAnh = "/image/tt2.webp" },
        new UuDai { Id = 3, TieuDe = "Ưu đãi TT3", MoTa = "Chi tiết ưu đãi thanh toán 3", DuongDanAnh = "/image/tt3.webp" },
        new UuDai { Id = 4, TieuDe = "Ưu đãi TT4", MoTa = "Chi tiết ưu đãi thanh toán 4", DuongDanAnh = "/image/tt4.webp" }
    };

    public IActionResult ChiTiet(int id)
    {
        var uuDai = uuDaiList.FirstOrDefault(u => u.Id == id);
        if (uuDai == null)
            return NotFound();

        return View(uuDai);
    }
}
