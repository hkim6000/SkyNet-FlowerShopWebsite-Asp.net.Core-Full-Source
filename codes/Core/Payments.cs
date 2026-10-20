using System.Globalization;
using System.Text.Json;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public static class Payments
    {
        private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("https://api.stripe.com/"), Timeout = TimeSpan.FromSeconds(20) };

        public static bool Ready(PayConf conf)
        {
            return conf.SecretKey.StartsWith("sk_", StringComparison.Ordinal) || conf.SecretKey.StartsWith("rk_", StringComparison.Ordinal);
        }

        private static string Cents(decimal v)
        {
            return ((long)decimal.Round(v * 100m, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        }

        private static async Task<(JsonDocument? doc, string error)> Send(PayConf conf, HttpMethod method, string path, List<KeyValuePair<string, string>>? form)
        {
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(method, path);
                req.Headers.Add("Authorization", "Bearer " + conf.SecretKey);
                if (form != null)
                {
                    req.Content = new FormUrlEncodedContent(form);
                }
                HttpResponseMessage res = await Http.SendAsync(req);
                string body = await res.Content.ReadAsStringAsync();
                JsonDocument doc = JsonDocument.Parse(body);
                if (!res.IsSuccessStatusCode)
                {
                    string msg = doc.RootElement.TryGetProperty("error", out JsonElement e) && e.TryGetProperty("message", out JsonElement m) ? m.GetString() ?? "Payment provider error." : "Payment provider error.";
                    return (null, msg);
                }
                return (doc, string.Empty);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
                return (null, "The payment service could not be reached. Please try again.");
            }
        }

        public static async Task<(string url, string id, string error)> CreateSession(PayConf conf, SiteData site, OrderRec o)
        {
            if (!Ready(conf))
            {
                return (string.Empty, string.Empty, "Card payment is not set up yet.");
            }
            string baseUrl = site.PublicUrl.Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
            {
                return (string.Empty, string.Empty, "The shop has not set its site address yet.");
            }
            string back = baseUrl + "/Pay?o=" + o.No + "&k=" + o.Token;
            List<KeyValuePair<string, string>> f = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("mode", "payment"),
                new KeyValuePair<string, string>("success_url", back + "&back=1"),
                new KeyValuePair<string, string>("cancel_url", back),
                new KeyValuePair<string, string>("client_reference_id", o.No),
                new KeyValuePair<string, string>("customer_email", o.Email),
                new KeyValuePair<string, string>("expires_at", DateTimeOffset.UtcNow.AddMinutes(31).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
            };
            int i = 0;
            void Add(string name, string description, decimal unit, int qty)
            {
                string p = "line_items[" + i + "]";
                f.Add(new KeyValuePair<string, string>(p + "[quantity]", qty.ToString(CultureInfo.InvariantCulture)));
                f.Add(new KeyValuePair<string, string>(p + "[price_data][currency]", conf.Currency));
                f.Add(new KeyValuePair<string, string>(p + "[price_data][unit_amount]", Cents(unit)));
                f.Add(new KeyValuePair<string, string>(p + "[price_data][product_data][name]", name));
                if (description.Length > 0)
                {
                    f.Add(new KeyValuePair<string, string>(p + "[price_data][product_data][description]", description));
                }
                i++;
            }
            foreach (OrderLine l in o.Lines)
            {
                Add(l.Title + " (" + l.SizeLabel + ")", string.Join(", ", l.Extras), l.Unit, l.Qty);
            }
            if (o.Fee > 0)
            {
                Add("Delivery", o.ZoneName, o.Fee, 1);
            }
            if (o.Tax > 0)
            {
                Add("Tax", string.Empty, o.Tax, 1);
            }
            (JsonDocument? doc, string error) = await Send(conf, HttpMethod.Post, "v1/checkout/sessions", f);
            if (doc == null)
            {
                return (string.Empty, string.Empty, error);
            }
            using (doc)
            {
                string url = doc.RootElement.TryGetProperty("url", out JsonElement u) ? u.GetString() ?? string.Empty : string.Empty;
                string id = doc.RootElement.TryGetProperty("id", out JsonElement s) ? s.GetString() ?? string.Empty : string.Empty;
                return (url, id, url.Length == 0 ? "The payment page could not be opened." : string.Empty);
            }
        }

        public static async Task<(bool paid, string intent, string error)> Check(PayConf conf, string sessionId)
        {
            if (!Ready(conf) || sessionId.Length == 0)
            {
                return (false, string.Empty, string.Empty);
            }
            (JsonDocument? doc, string error) = await Send(conf, HttpMethod.Get, "v1/checkout/sessions/" + Uri.EscapeDataString(sessionId), null);
            if (doc == null)
            {
                return (false, string.Empty, error);
            }
            using (doc)
            {
                bool paid = doc.RootElement.TryGetProperty("payment_status", out JsonElement p) && p.GetString() == "paid";
                string intent = doc.RootElement.TryGetProperty("payment_intent", out JsonElement i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? string.Empty : string.Empty;
                return (paid, intent, string.Empty);
            }
        }

        public static async Task<string> Refund(PayConf conf, string intent)
        {
            if (!Ready(conf) || intent.Length == 0)
            {
                return "No card payment is on record for this order. Refund it in your payment dashboard.";
            }
            (JsonDocument? doc, string error) = await Send(conf, HttpMethod.Post, "v1/refunds", new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("payment_intent", intent) });
            doc?.Dispose();
            return error;
        }
    }
}
