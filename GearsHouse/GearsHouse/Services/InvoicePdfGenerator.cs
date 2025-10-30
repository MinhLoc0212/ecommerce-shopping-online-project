using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing.Layout;
using System.IO;
using GearsHouse.Models;

namespace GearsHouse.Services
{
    public class InvoicePdfGenerator
    {
        public string GenerateInvoicePdf(Order order, List<OrderDetail> details)
        {
            string fileName = $"invoice_order_{order.Id}.pdf";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "invoices", fileName);

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var tf = new XTextFormatter(gfx);

            var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 12, XFontStyle.Regular);
            var detailFont = new XFont("Arial", 10, XFontStyle.Regular);
            var boldFont = new XFont("Arial", 10, XFontStyle.Bold);
            int y = 40;

            // Title
            gfx.DrawString("HÓA ĐƠN ĐẶT HÀNG", titleFont, XBrushes.Black, new XRect(0, y, page.Width, 20), XStringFormats.Center);
            y += 40;

            // Customer Information
            gfx.DrawString($"Mã đơn hàng: {order.Id}", headerFont, XBrushes.Black, 40, y); y += 20;
            gfx.DrawString($"Khách hàng: {order.FullName}", headerFont, XBrushes.Black, 40, y); y += 20;
            gfx.DrawString($"Số điện thoại: {order.PhoneNumber}", headerFont, XBrushes.Black, 40, y); y += 20;
            gfx.DrawString($"Địa chỉ: {order.ShippingAddress}", headerFont, XBrushes.Black, 40, y); y += 20;
            gfx.DrawString($"Ghi chú: {order.Notes}", headerFont, XBrushes.Black, 40, y); y += 30;

            // Products List Header
            gfx.DrawString("Danh sách sản phẩm:", boldFont, XBrushes.Black, 40, y); y += 10;

            // Table layout
            int x1 = 40;   // Tên sản phẩm
            int x2 = 250;  // Số lượng
            int x3 = 340;  // Giá
            int x4 = 500;  // Kết thúc bảng
            int rowHeight = 25;

            // Vẽ tiêu đề cột
            gfx.DrawRectangle(XPens.Black, x1, y, x4 - x1, rowHeight);
            gfx.DrawLine(XPens.Black, x2, y, x2, y + rowHeight);
            gfx.DrawLine(XPens.Black, x3, y, x3, y + rowHeight);

            tf.DrawString("Tên sản phẩm", boldFont, XBrushes.Black, new XRect(x1 + 5, y + 5, x2 - x1 - 10, rowHeight));
            tf.DrawString("Số lượng", boldFont, XBrushes.Black, new XRect(x2 + 5, y + 5, x3 - x2 - 10, rowHeight));
            tf.DrawString("Giá", boldFont, XBrushes.Black, new XRect(x3 + 5, y + 5, x4 - x3 - 10, rowHeight));

            y += rowHeight;

            // Vẽ từng dòng dữ liệu
            foreach (var item in details)
            {
                gfx.DrawRectangle(XPens.Black, x1, y, x4 - x1, rowHeight);
                gfx.DrawLine(XPens.Black, x2, y, x2, y + rowHeight);
                gfx.DrawLine(XPens.Black, x3, y, x3, y + rowHeight);

                tf.DrawString(item.Product.Name, detailFont, XBrushes.Black, new XRect(x1 + 5, y + 5, x2 - x1 - 10, rowHeight));
                tf.DrawString(item.Quantity.ToString(), detailFont, XBrushes.Black, new XRect(x2 + 5, y + 5, x3 - x2 - 10, rowHeight));
                tf.DrawString($"{item.Price:N0} VNĐ", detailFont, XBrushes.Black, new XRect(x3 + 5, y + 5, x4 - x3 - 10, rowHeight));

                y += rowHeight;
            }

            // Tổng tiền
            y += 10;
            gfx.DrawString($"Tổng tiền: {order.TotalPrice:N0} VNĐ", boldFont, XBrushes.Black, x1, y);

            // Lưu file PDF
            document.Save(filePath);
            return filePath;
        }
    }
}
