using GearsHouse.Models;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GearsHouse.Services
{
    public class MomoService
    {
        private readonly MomoSettings _settings;
        private readonly ILogger<MomoService> _logger;
        private readonly HttpClient _http;

        public MomoService(IOptions<MomoSettings> options, ILogger<MomoService> logger)
        {
            _settings = options.Value;
            _logger = logger;
            _http = new HttpClient();
        }

        public async Task<string> CreatePaymentUrlAsync(Order order, string returnUrl, string notifyUrl)
        {
            returnUrl = (returnUrl ?? string.Empty).Trim();
            notifyUrl = (notifyUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(returnUrl) || string.IsNullOrWhiteSpace(notifyUrl))
            {
                throw new ArgumentException("returnUrl/notifyUrl không hợp lệ");
            }

            var amount = ((long)Math.Round(order.TotalPrice, MidpointRounding.AwayFromZero)).ToString();
            var requestId = Guid.NewGuid().ToString();
            var orderInfo = $"Thanh toan don hang {order.Id}";
            var partnerOrderId = $"{order.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var extraData = ""; // Set extraData to empty string if not used, avoiding null issues

            // Build raw signature string using SortedDictionary to ensure correct order
            var rawData = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "partnerCode", _settings.PartnerCode },
                { "accessKey", _settings.AccessKey },
                { "requestId", requestId },
                { "amount", amount },
                { "orderId", partnerOrderId },
                { "orderInfo", orderInfo },
                { "redirectUrl", returnUrl },
                { "ipnUrl", notifyUrl },
                { "extraData", extraData },
                { "requestType", _settings.RequestType }
            };

            var raw = BuildRaw(rawData);
            var signature = HmacSha256(_settings.SecretKey, raw);
            
            _logger.LogInformation("MoMo Raw: {Raw}", raw);
            _logger.LogInformation("MoMo Signature: {Signature}", signature);

            // Construct payload
            var payload = new Dictionary<string, object>
            {
                { "partnerCode", _settings.PartnerCode },
                { "accessKey", _settings.AccessKey },
                { "requestId", requestId },
                { "amount", long.Parse(amount) }, // Send as number
                { "orderId", partnerOrderId },
                { "orderInfo", orderInfo },
                { "redirectUrl", returnUrl },
                { "ipnUrl", notifyUrl },
                { "extraData", extraData },
                { "requestType", _settings.RequestType },
                { "lang", "vi" },
                { "signature", signature }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(_settings.MomoApiUrl, content);
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("MoMo HTTP {Status} Body: {Body}", (int)resp.StatusCode, body);

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("resultCode", out var rc) && rc.GetInt32() != 0)
            {
                var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "";
                var localMsg = doc.RootElement.TryGetProperty("localMessage", out var lm) ? lm.GetString() : "";
                _logger.LogWarning("MoMo error resultCode={Code} message={Msg} localMessage={LocalMsg}", rc.GetInt32(), msg, localMsg);
                throw new InvalidOperationException($"MoMo Error: {msg}");
            }
            if (doc.RootElement.TryGetProperty("payUrl", out var payUrlElem))
            {
                return payUrlElem.GetString() ?? "";
            }

            _logger.LogWarning("MoMo response missing payUrl: {Body}", body);
            throw new InvalidOperationException("Không tạo được liên kết thanh toán MoMo.");
        }

        public bool ValidateSignature(IQueryCollection query)
        {
            var data = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in query)
            {
                var key = kv.Key;
                var value = kv.Value.ToString();
                if (key.Equals("signature", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(value)) data[key] = value;
            }
            var raw = BuildRaw(data);
            var expected = HmacSha256(_settings.SecretKey, raw);
            var actual = query["signature"].ToString();
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        public bool ValidateSignature(Dictionary<string, string> data)
        {
            var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in data)
            {
                if (string.Equals(kv.Key, "signature", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(kv.Value)) sorted[kv.Key] = kv.Value;
            }
            var raw = BuildRaw(sorted);
            var expected = HmacSha256(_settings.SecretKey, raw);
            var actual = data.TryGetValue("signature", out var sig) ? sig : "";
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSuccessResponse(IQueryCollection query)
        {
            var code = query["resultCode"].ToString();
            return code == "0";
        }

        public static bool IsSuccessResponse(Dictionary<string, string> data)
        {
            return data.TryGetValue("resultCode", out var code) && code == "0";
        }

        private static string BuildRaw(SortedDictionary<string, string> dict)
        {
            var sb = new StringBuilder();
            foreach (var kv in dict)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(kv.Key);
                sb.Append('=');
                sb.Append(kv.Value);
            }
            return sb.ToString();
        }

        private static string HmacSha256(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
