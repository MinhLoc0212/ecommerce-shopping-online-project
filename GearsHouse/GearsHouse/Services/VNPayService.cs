using System.Security.Cryptography;
using System.Text;
using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using GearsHouse.Models;

namespace GearsHouse.Services
{
    public class VNPayService
    {
        private readonly VNPaySettings _settings;
        private readonly ILogger<VNPayService> _logger;

        public VNPayService(IOptions<VNPaySettings> options, ILogger<VNPayService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public string CreatePaymentUrl(Order order, string ipAddress, string returnUrl)
        {
            var createDate = DateTime.Now;
            var expireDate = createDate.AddMinutes(15);

            // Sắp xếp theo thứ tự Ordinal giống demo VNPay
            var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = _settings.TmnCode,
                // Làm tròn số tiền nhân 100 để tránh sai số thập phân
                ["vnp_Amount"] = ((long)Math.Round(order.TotalPrice * 100m, MidpointRounding.AwayFromZero)).ToString(),
                ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = order.Id.ToString(),
                ["vnp_OrderInfo"] = $"Thanh toan don hang {order.Id}",
                ["vnp_OrderType"] = "other",
                ["vnp_ReturnUrl"] = returnUrl,
                ["vnp_IpAddr"] = ipAddress,
                ["vnp_Locale"] = "vn",
                ["vnp_CreateDate"] = createDate.ToString("yyyyMMddHHmmss"),
                ["vnp_ExpireDate"] = expireDate.ToString("yyyyMMddHHmmss")
            };

            // Cảnh báo nếu ReturnUrl là localhost/127.0.0.1 vì VNPay không thể tìm thấy website
            if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri))
            {
                var host = returnUri.Host;
                if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1")
                {
                    _logger.LogWarning("VNPay ReturnUrl đang sử dụng {Host}. VNPay yêu cầu domain public đã đăng ký; điều này có thể gây lỗi 'Không tìm thấy website'.", host);
                }
            }

            // Tạo chuỗi đã encode để ký (theo demo: UrlEncode key & value)
            var signData = BuildQueryEncoded(vnpParams);
            var secureHash = HmacSha512(_settings.HashSecret, signData);
            
            // Log để debug
            _logger.LogInformation("VNPay Signature Data: {SignData}", signData);
            _logger.LogInformation("VNPay Generated Hash: {SecureHash}", secureHash);
            
            // Tạo query string đầy đủ cho URL (đã encode)
            var fullQuery = BuildQueryEncoded(vnpParams);
            var url = $"{_settings.BaseUrl}?{fullQuery}&vnp_SecureHashType=HMACSHA512&vnp_SecureHash={secureHash}";
            _logger.LogInformation("VNPay Redirect URL: {Url}", url);
            return url;
        }

        public bool ValidateSignature(IQueryCollection query)
        {
            var data = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in query)
            {
                var key = kv.Key;
                var value = kv.Value.ToString();
                if (key == "vnp_SecureHash" || key == "vnp_SecureHashType") continue;
                if (!string.IsNullOrEmpty(value)) data[key] = value;
            }

            // Theo demo: xây chuỗi đã UrlEncode để đối chiếu chữ ký
            var raw = BuildQueryEncoded(data);
            var expected = HmacSha512(_settings.HashSecret, raw);
            var actual = query["vnp_SecureHash"].ToString();
            
            // Log để debug
            _logger.LogInformation("VNPay Validation Data: {Raw}", raw);
            _logger.LogInformation("VNPay Expected Hash: {Expected}", expected);
            _logger.LogInformation("VNPay Actual Hash: {Actual}", actual);
            
            var isValid = string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
            _logger.LogInformation("VNPay Signature Valid: {IsValid}", isValid);
            
            return isValid;
        }

        // Không cần normalize: sẽ encode lại bằng WebUtility.UrlEncode khi tạo chuỗi ký

        public static bool IsSuccessResponse(IQueryCollection query)
        {
            var code = query["vnp_ResponseCode"].ToString();
            return code == "00";
        }

        private static string BuildQueryEncoded(SortedDictionary<string, string> dict)
        {
            var sb = new StringBuilder();
            foreach (var kv in dict)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(WebUtility.UrlEncode(kv.Key));
                sb.Append('=');
                sb.Append(WebUtility.UrlEncode(kv.Value));
            }
            return sb.ToString();
        }

        private static string UrlEncodeVnp(string input)
        {
            if (input == null) return string.Empty;
            return WebUtility.UrlEncode(input);
        }

        private static string HmacSha512(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}