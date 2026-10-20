using System.Globalization;
using System.Net;
using System.Text;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public static class Ui
    {
        public static string Esc(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        public static string Js(string? value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("<", "\\u003c").Replace("\r", " ").Replace("\n", " ");
        }

        public static string Money(decimal v)
        {
            return "$" + v.ToString("#,##0.00", CultureInfo.InvariantCulture);
        }

        public static string Clip(string? value, int max)
        {
            string q = (value ?? string.Empty).Trim();
            return q.Length > max ? q.Substring(0, max) : q;
        }

        public static string Pick(string? value, IEnumerable<string> allowed)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return allowed.Contains(v) ? v : string.Empty;
        }

        public static int Bound(string? value, int min, int max, int fallback)
        {
            return int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? Math.Max(min, Math.Min(max, n)) : fallback;
        }

        public static bool Has(string? text, string q)
        {
            return q.Length > 0 && (text ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        public static string Count(int n, string one, string many)
        {
            return n + " " + (n == 1 ? one : many);
        }

        public static string Slug(string text)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9')
                {
                    sb.Append(c);
                }
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                {
                    sb.Append('-');
                }
            }
            return sb.ToString().Trim('-');
        }

        public static string When(DateTime utc)
        {
            return utc.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture);
        }

        public static string Day(string date)
        {
            return DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.ToString("ddd, MMM d", CultureInfo.InvariantCulture) : date;
        }

        public static decimal FromPrice(Bouquet b)
        {
            return b.Sizes.Count == 0 ? 0 : b.Sizes.Min(s => s.Price);
        }

        public static string Photo(Bouquet b)
        {
            return b.Photos.Count > 0 ? b.Photos[0] : "images/hero.webp";
        }

        public static bool IsNew(Bouquet b)
        {
            return b.Added >= -30;
        }

        public static string StatusLabel(string status)
        {
            switch (status)
            {
                case "awaiting": return "Waiting for payment";
                case "new": return "New";
                case "preparing": return "Being arranged";
                case "out": return "Out for delivery";
                case "ready": return "Ready for pickup";
                case "delivered": return "Delivered";
                case "cancelled": return "Cancelled";
                default: return status;
            }
        }

        public static string PayLabel(OrderRec o)
        {
            if (o.PayStatus == "refunded")
            {
                return "Refunded";
            }
            if (o.PayStatus == "paid")
            {
                return o.PayMethod == "card" ? "Paid by card" : "Paid";
            }
            return o.PayMethod == "card" ? "Card payment pending" : "Pay on " + (o.Pickup ? "pickup" : "delivery");
        }

        public static string Logo(string p, string brand)
        {
            return "<a class=\"" + p + "-logo\" href=\"Home\" aria-label=\"" + Esc(brand) + " home\"><svg viewBox=\"0 0 40 40\" width=\"34\" height=\"34\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"10\" fill=\"#B8445F\"/><g fill=\"#FBF7F2\"><circle cx=\"20\" cy=\"11\" r=\"5\"/><circle cx=\"29\" cy=\"18\" r=\"5\"/><circle cx=\"26\" cy=\"28\" r=\"5\"/><circle cx=\"14\" cy=\"28\" r=\"5\"/><circle cx=\"11\" cy=\"18\" r=\"5\"/></g><circle cx=\"20\" cy=\"20\" r=\"4\" fill=\"#E9C46A\"/></svg><span>" + Esc(brand) + "</span></a>";
        }

        public static string Card(string p, Bouquet b)
        {
            bool gone = b.Stock <= 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("<a class=\"" + p + "-card\" href=\"Product?k=" + Esc(b.Key) + "\"><span class=\"" + p + "-card-img\"><img src=\"" + Esc(Media.Thumb(Photo(b))) + "\" alt=\"" + Esc(b.Title) + "\" loading=\"lazy\">");
            if (gone)
            {
                sb.Append("<i class=\"" + p + "-tag " + p + "-tag-out\">Sold out today</i>");
            }
            else if (IsNew(b))
            {
                sb.Append("<i class=\"" + p + "-tag\">New</i>");
            }
            sb.Append("</span><b>" + Esc(b.Title) + "</b><small>" + Esc(b.Blurb) + "</small><em>From " + Money(FromPrice(b)) + "</em></a>");
            return sb.ToString();
        }

        public static string Suggest(string p, SiteData site, string term)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Bouquet b in site.Bouquets.Where(x => x.Published && (Has(x.Title, term) || Has(x.Colors, term) || Has(x.Blurb, term))).Take(6))
            {
                sb.Append("<a class=\"" + p + "-sg\" href=\"Product?k=" + Esc(b.Key) + "\"><img src=\"" + Esc(Media.Thumb(Photo(b))) + "\" alt=\"\"><span><b>" + Esc(b.Title) + "</b><small>From " + Money(FromPrice(b)) + "</small></span></a>");
            }
            if (sb.Length == 0)
            {
                return "<div class=\"" + p + "-sg-none\">No bouquets match &ldquo;" + Esc(term) + "&rdquo;</div>";
            }
            sb.Append("<a class=\"" + p + "-sg-all\" href=\"Shop?term=" + WebUtility.UrlEncode(term) + "\">See all results</a>");
            return sb.ToString();
        }

        public static string Foot(string p, SiteData site)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"" + p + "-foot-in\"><div class=\"" + p + "-fbrand\">" + Logo(p, site.Brand) + "<p>" + Esc(site.Tagline) + "</p></div>");
            sb.Append("<div class=\"" + p + "-fcol\"><b>Shop</b><a href=\"Shop\">All bouquets</a>");
            foreach (Occasion o in site.Occasions)
            {
                sb.Append("<a href=\"Shop?o=" + Esc(o.Key) + "\">" + Esc(o.Label) + "</a>");
            }
            sb.Append("</div><div class=\"" + p + "-fcol\"><b>Help</b><a href=\"Track\">Track an order</a><a href=\"About\">About us</a><a href=\"Contact\">Contact and FAQ</a><a href=\"AdminLogin\">Staff sign in</a></div>");
            sb.Append("<div class=\"" + p + "-fcol\"><b>Visit</b><span>" + Esc(site.Address) + "</span><span>" + Esc(site.Hours) + "</span><a href=\"mailto:" + Esc(site.Email) + "\">" + Esc(site.Email) + "</a><span>" + Esc(site.Phone) + "</span></div></div>");
            sb.Append("<div class=\"" + p + "-foot-b\">&copy; " + DateTime.Now.Year + " " + Esc(site.Brand) + "</div>");
            return sb.ToString();
        }
    }
}
