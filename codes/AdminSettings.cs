using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Settings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ae-panel\"><h1>No access</h1><p class=\"ae-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ae-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ae-menu", await Auth.Menu("ae", "AdminSettings", c));
            response.SetElementContents("ae-who", Auth.Who("ae", c));
        }

        private static readonly string[] Days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Shell(response, c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("ae-main", NoAccess("settings"));
                return response;
            }
            PayConf conf = await Store.LoadPay();
            response.SetElementContents("ae-main", FormHtml(site, conf));
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save settings", string.Empty, "Only managers can change settings.");
            }
            Dictionary<string, string> e = new Dictionary<string, string>();
            bool cutOk = int.TryParse(Raw("cutoff"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cut) && cut >= 0 && cut <= 23;
            bool aheadOk = int.TryParse(Raw("ahead"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ahead) && ahead >= 1 && ahead <= 60;
            bool lowOk = int.TryParse(Raw("low"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int low) && low >= 0 && low <= 50;
            bool taxOk = decimal.TryParse(Raw("tax"), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal tax) && tax >= 0 && tax <= 30;
            string brand = Raw("brand");
            string tagline = Raw("tagline");
            string phone = Raw("phone");
            string email = Raw("email");
            string address = Raw("address");
            string hours = Raw("hours");
            if (brand.Length < 2 || brand.Length > 40)
            {
                e["brand"] = "Enter 2 to 40 characters.";
            }
            if (tagline.Length < 5 || tagline.Length > 100)
            {
                e["tagline"] = "Enter 5 to 100 characters.";
            }
            if (phone.Length < 5 || phone.Length > 30)
            {
                e["phone"] = "Enter 5 to 30 characters.";
            }
            if (!Ui.IsEmail(email))
            {
                e["email"] = "Enter a valid email address.";
            }
            if (address.Length < 5 || address.Length > 120)
            {
                e["address"] = "Enter 5 to 120 characters.";
            }
            if (hours.Length < 3 || hours.Length > 120)
            {
                e["hours"] = "Enter 3 to 120 characters.";
            }
            bool videoOk = int.TryParse(Raw("videomb"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int videoMb) && videoMb >= 1 && videoMb <= Media.CeilingMb;
            if (!videoOk)
            {
                e["videomb"] = "Enter a whole number of megabytes from 1 to " + Media.CeilingMb + ".";
            }
            if (!cutOk)
            {
                e["cutoff"] = "Enter an hour from 0 to 23.";
            }
            if (!aheadOk)
            {
                e["ahead"] = "Enter a number of days from 1 to 60.";
            }
            if (!lowOk)
            {
                e["low"] = "Enter a number from 0 to 50.";
            }
            if (!taxOk)
            {
                e["tax"] = "Enter a percentage from 0 to 30.";
            }
            List<int> closed = new List<int>();
            for (int d = 0; d < 7; d++)
            {
                if (Raw("d" + d) == "1")
                {
                    closed.Add(d);
                }
            }
            if (closed.Count == 7)
            {
                e["days"] = "Leave at least one day open.";
            }
            List<string> blackout = new List<string>();
            foreach (string line in Lines("blackout"))
            {
                if (DateTime.TryParseExact(line, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day))
                {
                    blackout.Add(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
                else
                {
                    e["blackout"] = "Write each date as YYYY-MM-DD, one per line.";
                }
            }
            List<Slot> slots = new List<Slot>();
            foreach (string line in Lines("slots").Take(8))
            {
                string key = Ui.Slug(line.Split(',')[0]);
                string unique = key.Length == 0 ? "slot" : key;
                int n = 2;
                while (slots.Any(s => s.Key == unique))
                {
                    unique = key + n++;
                }
                if (line.Length > 50)
                {
                    e["slots"] = "Keep each time slot under 50 characters.";
                }
                slots.Add(new Slot { Key = unique, Label = line });
            }
            if (slots.Count == 0)
            {
                e["slots"] = "Add at least one time slot.";
            }
            List<Zone> zones = new List<Zone>();
            foreach (string line in Lines("zones").Take(8))
            {
                string[] p = line.Split('|').Select(x => x.Trim()).ToArray();
                bool feeOk = p.Length == 4 && decimal.TryParse(p[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal fee) && fee >= 0 && fee <= 500;
                bool freeOk = p.Length == 4 && decimal.TryParse(p[2], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal free) && free >= 0 && free <= 5000;
                List<string> prefixes = p.Length == 4 ? p[3].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToUpperInvariant().Replace(" ", string.Empty)).Where(x => x.Length >= 2 && x.Length <= 8).ToList() : new List<string>();
                if (p.Length != 4 || p[0].Length < 2 || p[0].Length > 40 || !feeOk || !freeOk || prefixes.Count == 0)
                {
                    e["zones"] = "Write each zone as: Name | fee | free delivery over | postcode starts (comma separated).";
                    continue;
                }
                string key = Ui.Slug(p[0]);
                string unique = key.Length == 0 ? "zone" : key;
                int n = 2;
                while (zones.Any(z => z.Key == unique))
                {
                    unique = key + n++;
                }
                zones.Add(new Zone { Key = unique, Name = p[0], Fee = Pricing.Round(decimal.Parse(p[1], CultureInfo.InvariantCulture)), FreeOver = Pricing.Round(decimal.Parse(p[2], CultureInfo.InvariantCulture)), Prefixes = prefixes });
            }
            if (zones.Count == 0 && !e.ContainsKey("zones"))
            {
                e["zones"] = "Add at least one delivery zone.";
            }
            List<Addon> addons = new List<Addon>();
            foreach (string line in Lines("addons").Take(8))
            {
                string[] p = line.Split('|').Select(x => x.Trim()).ToArray();
                if (p.Length != 2 || p[0].Length < 2 || p[0].Length > 40 || !decimal.TryParse(p[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price) || price < 0 || price > 500)
                {
                    e["addons"] = "Write each extra as: Name | price.";
                    continue;
                }
                string key = Ui.Slug(p[0]);
                string unique = key.Length == 0 ? "extra" : key;
                int n = 2;
                while (addons.Any(a => a.Key == unique))
                {
                    unique = key + n++;
                }
                addons.Add(new Addon { Key = unique, Label = p[0], Price = Pricing.Round(price) });
            }
            List<string> couriers = Lines("couriers").Where(x => x.Length <= 40).Distinct().Take(10).ToList();
            bool card = Raw("card") == "1";
            bool later = Raw("later") == "1";
            string url = Raw("url");
            string key2 = (GetDataValue("paykey") ?? string.Empty).Trim();
            bool clear = Raw("payclear") == "1";
            string cur = Raw("cur").ToLowerInvariant();
            if (!card && !later)
            {
                e["pay"] = "Turn on at least one way to pay.";
            }
            if (url.Length > 0 && (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.Length > 200 || url.Contains(' ')))
            {
                e["url"] = "Enter the full address, starting with https://";
            }
            if (cur.Length != 3 || !cur.All(ch => ch >= 'a' && ch <= 'z'))
            {
                e["cur"] = "Enter a three-letter currency code, such as usd.";
            }
            if (key2.Length > 0 && !(key2.StartsWith("sk_", StringComparison.Ordinal) || key2.StartsWith("rk_", StringComparison.Ordinal)) || key2.Length > 200)
            {
                e["paykey"] = "A secret key starts with sk_ or rk_.";
            }
            PayConf conf = await Store.LoadPay();
            string finalKey = clear ? string.Empty : key2.Length > 0 ? key2 : conf.SecretKey;
            if (card && Payments.Ready(new PayConf { SecretKey = finalKey }) && url.Length == 0)
            {
                e["url"] = "Enter the site address so customers can return from the payment page.";
            }
            foreach (string k in new[] { "cutoff", "ahead", "low", "tax", "days", "blackout", "slots", "zones", "addons", "pay", "url", "cur", "paykey", "videomb", "brand", "tagline", "phone", "email", "address", "hours" })
            {
                response.SetElementContents("ae-e-" + k, e.TryGetValue(k, out string? m) ? m : string.Empty);
            }
            if (e.Count > 0)
            {
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                site.CutOffHour = cut;
                site.MaxDaysAhead = ahead;
                site.LowStock = low;
                site.TaxPercent = tax;
                site.ClosedDays = closed;
                site.Blackout = blackout.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
                site.Slots = slots;
                site.Zones = zones;
                site.Addons = addons;
                site.Couriers = couriers;
                site.AcceptCard = card;
                site.AcceptLater = later;
                site.PublicUrl = url;
                site.MaxVideoMb = videoMb;
                site.Brand = brand;
                site.Tagline = tagline;
                site.Phone = phone;
                site.Email = email;
                site.Address = address;
                site.Hours = hours;
                await Store.SaveSite(site);
                conf.SecretKey = finalKey;
                conf.Currency = cur;
                await Store.SavePay(conf);
            }
            await Store.Log(c.U.Id, "save settings", string.Empty, true);
            response.ExecuteScript("AdminSettingsJs.saved('" + Ui.Js(KeyStatus(conf)) + "');");
            return response;
        }

        private string Raw(string key)
        {
            return (GetDataValue(key) ?? string.Empty).Trim();
        }

        private IEnumerable<string> Lines(string key)
        {
            return (GetDataValue(key) ?? string.Empty).Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);
        }

        private static string KeyStatus(PayConf conf)
        {
            if (!Payments.Ready(conf))
            {
                return "No payment key saved. Customers can only choose to pay on delivery or pickup.";
            }
            return "Key saved: " + conf.SecretKey.Substring(0, 3) + "..." + conf.SecretKey.Substring(conf.SecretKey.Length - 4);
        }

        private static string Text(string key, string label, string value, int max)
        {
            return "<label class=\"ae-field\" for=\"ae-f-" + key + "\"><span>" + Ui.Esc(label) + "</span><input type=\"text\" id=\"ae-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"><em class=\"ae-err\" id=\"ae-e-" + key + "\"></em></label>";
        }

        private static string Num(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormHtml(SiteData site, PayConf conf)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ae-ph\"><div><h1>Settings</h1><p>Ordering rules, delivery, tax and payment. Managers only.</p></div></div>");
            sb.Append("<form class=\"ae-form\" id=\"ae-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"ae-panel ae-stack\"><h2>Shop details</h2><div class=\"ae-grid2\">" + Text("brand", "Shop name", site.Brand, 40) + Text("tagline", "Tagline", site.Tagline, 100) + "</div><div class=\"ae-grid2\">" + Text("phone", "Phone", site.Phone, 30) + Text("email", "Email", site.Email, 80) + "</div>" + Text("address", "Studio address", site.Address, 120) + Text("hours", "Opening hours", site.Hours, 120) + "<p class=\"ae-hint\">Shown in the footer, on the Contact page and wherever {name} is used on a page.</p></section>");
            sb.Append("<section class=\"ae-panel ae-stack\"><h2>Ordering</h2><div class=\"ae-grid3\">");
            sb.Append("<label class=\"ae-field\" for=\"ae-cutoff\"><span>Same-day cut-off hour</span><input type=\"number\" id=\"ae-cutoff\" data-k=\"cutoff\" min=\"0\" max=\"23\" value=\"" + site.CutOffHour + "\"><small>Orders placed after this hour (0 to 23) start tomorrow.</small><em class=\"ae-err\" id=\"ae-e-cutoff\"></em></label>");
            sb.Append("<label class=\"ae-field\" for=\"ae-ahead\"><span>Book ahead, days</span><input type=\"number\" id=\"ae-ahead\" data-k=\"ahead\" min=\"1\" max=\"60\" value=\"" + site.MaxDaysAhead + "\"><em class=\"ae-err\" id=\"ae-e-ahead\"></em></label>");
            sb.Append("<label class=\"ae-field\" for=\"ae-low\"><span>Low stock warning at</span><input type=\"number\" id=\"ae-low\" data-k=\"low\" min=\"0\" max=\"50\" value=\"" + site.LowStock + "\"><em class=\"ae-err\" id=\"ae-e-low\"></em></label></div>");
            sb.Append("<fieldset class=\"ae-fieldset\"><legend>Closed on</legend><div class=\"ae-days\">");
            for (int d = 0; d < 7; d++)
            {
                sb.Append("<label class=\"ae-check\"><input type=\"checkbox\" data-k=\"d" + d + "\"" + (site.ClosedDays.Contains(d) ? " checked" : string.Empty) + ">" + Days[d] + "</label>");
            }
            sb.Append("</div><em class=\"ae-err\" id=\"ae-e-days\"></em></fieldset>");
            sb.Append("<label class=\"ae-field\" for=\"ae-blackout\"><span>Closed dates (holidays)</span><textarea id=\"ae-blackout\" data-k=\"blackout\" rows=\"3\" placeholder=\"2026-12-25\">" + Ui.Esc(string.Join("\n", site.Blackout)) + "</textarea><small>One date per line, as YYYY-MM-DD.</small><em class=\"ae-err\" id=\"ae-e-blackout\"></em></label>");
            sb.Append("<label class=\"ae-field\" for=\"ae-slots\"><span>Time slots</span><textarea id=\"ae-slots\" data-k=\"slots\" rows=\"4\">" + Ui.Esc(string.Join("\n", site.Slots.Select(s => s.Label))) + "</textarea><small>One slot per line.</small><em class=\"ae-err\" id=\"ae-e-slots\"></em></label></section>");
            sb.Append("<section class=\"ae-panel ae-stack\"><h2>Delivery and extras</h2>");
            sb.Append("<label class=\"ae-field\" for=\"ae-zones\"><span>Delivery zones</span><textarea id=\"ae-zones\" data-k=\"zones\" rows=\"4\">" + Ui.Esc(string.Join("\n", site.Zones.Select(z => z.Name + " | " + Num(z.Fee) + " | " + Num(z.FreeOver) + " | " + string.Join(", ", z.Prefixes)))) + "</textarea><small>Name | fee | free delivery over (0 for never) | postcodes that start with, separated by commas.</small><em class=\"ae-err\" id=\"ae-e-zones\"></em></label>");
            sb.Append("<label class=\"ae-field\" for=\"ae-addons\"><span>Extras customers can add</span><textarea id=\"ae-addons\" data-k=\"addons\" rows=\"4\">" + Ui.Esc(string.Join("\n", site.Addons.Select(a => a.Label + " | " + Num(a.Price)))) + "</textarea><small>Name | price.</small><em class=\"ae-err\" id=\"ae-e-addons\"></em></label>");
            sb.Append("<label class=\"ae-field\" for=\"ae-couriers\"><span>Couriers</span><textarea id=\"ae-couriers\" data-k=\"couriers\" rows=\"3\">" + Ui.Esc(string.Join("\n", site.Couriers)) + "</textarea><small>One per line. Staff pick from this list on the shipping board.</small></label>");
            sb.Append("<label class=\"ae-field\" for=\"ae-tax\"><span>Sales tax, percent</span><input type=\"text\" inputmode=\"decimal\" id=\"ae-tax\" data-k=\"tax\" maxlength=\"6\" value=\"" + Num(site.TaxPercent) + "\"><small>Added to the items and delivery fee at checkout.</small><em class=\"ae-err\" id=\"ae-e-tax\"></em></label></section>");
            sb.Append("<section class=\"ae-panel ae-stack\"><h2>Media library</h2><div class=\"ae-grid3\"><label class=\"ae-field\" for=\"ae-videomb\"><span>Largest video upload, MB</span><input type=\"number\" id=\"ae-videomb\" data-k=\"videomb\" min=\"1\" max=\"" + Media.CeilingMb + "\" value=\"" + site.MaxVideoMb + "\"><small>MP4 files only, 1 to " + Media.CeilingMb + ". Photos are limited to " + Media.PhotoMb + " MB.</small><em class=\"ae-err\" id=\"ae-e-videomb\"></em></label></div></section>");
            sb.Append("<section class=\"ae-panel ae-stack\"><h2>Payment</h2>");
            sb.Append("<label class=\"ae-check\"><input type=\"checkbox\" data-k=\"card\"" + (site.AcceptCard ? " checked" : string.Empty) + ">Let customers pay now by card (Stripe Checkout)</label>");
            sb.Append("<label class=\"ae-check\"><input type=\"checkbox\" data-k=\"later\"" + (site.AcceptLater ? " checked" : string.Empty) + ">Let customers pay on delivery or pickup</label><em class=\"ae-err\" id=\"ae-e-pay\"></em>");
            sb.Append("<div class=\"ae-secret\" id=\"ae-paystate\">" + Ui.Esc(KeyStatus(conf)) + "</div>");
            sb.Append("<label class=\"ae-field\" for=\"ae-paykey\"><span>Stripe secret key</span><input type=\"password\" id=\"ae-paykey\" data-k=\"paykey\" autocomplete=\"off\" maxlength=\"200\" placeholder=\"sk_live_... or sk_test_...\"><small>Leave empty to keep the saved key. The key is stored on the server and never shown again.</small><em class=\"ae-err\" id=\"ae-e-paykey\"></em></label>");
            sb.Append("<label class=\"ae-check\"><input type=\"checkbox\" id=\"ae-payclear\" data-k=\"payclear\">Remove the saved key</label>");
            sb.Append("<div class=\"ae-grid2\"><label class=\"ae-field\" for=\"ae-cur\"><span>Currency code</span><input type=\"text\" id=\"ae-cur\" data-k=\"cur\" maxlength=\"3\" value=\"" + Ui.Esc(conf.Currency) + "\"><small>Prices on the site show a dollar sign; set the same currency here.</small><em class=\"ae-err\" id=\"ae-e-cur\"></em></label>");
            sb.Append("<label class=\"ae-field\" for=\"ae-url\"><span>Public site address</span><input type=\"text\" id=\"ae-url\" data-k=\"url\" maxlength=\"200\" placeholder=\"https://www.example.com\" value=\"" + Ui.Esc(site.PublicUrl) + "\"><small>Where customers return after paying. Required for card payments.</small><em class=\"ae-err\" id=\"ae-e-url\"></em></label></div></section>");
            sb.Append("<div class=\"ae-acts\"><button type=\"submit\" class=\"ae-btn\">Save settings</button></div></form>");
            return sb.ToString();
        }
    }
}
