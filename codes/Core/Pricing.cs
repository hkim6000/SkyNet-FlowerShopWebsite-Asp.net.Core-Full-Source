using System.Globalization;
using System.Text.Json;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public class PriceQuote
    {
        public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
        public decimal Subtotal { get; set; }
        public decimal Fee { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string ZoneName { get; set; } = string.Empty;
        public string FeeNote { get; set; } = string.Empty;
        public bool Ok { get; set; }
    }

    public static class Pricing
    {
        public static List<CartIn> Parse(string? json)
        {
            string text = (json ?? string.Empty).Trim();
            if (text.Length == 0 || text.Length > 6000)
            {
                return new List<CartIn>();
            }
            try
            {
                List<CartIn> list = JsonSerializer.Deserialize<List<CartIn>>(text, Store.Opts) ?? new List<CartIn>();
                return list.Take(30).ToList();
            }
            catch (JsonException)
            {
                return new List<CartIn>();
            }
        }

        public static decimal Round(decimal v)
        {
            return decimal.Round(v, 2, MidpointRounding.AwayFromZero);
        }

        public static Zone? ZoneFor(SiteData site, string postcode)
        {
            string pc = postcode.Replace(" ", string.Empty).ToUpperInvariant();
            if (pc.Length < 3)
            {
                return null;
            }
            return site.Zones.FirstOrDefault(z => z.Prefixes.Any(x => x.Trim().Length > 0 && pc.StartsWith(x.Replace(" ", string.Empty).ToUpperInvariant(), StringComparison.Ordinal)));
        }

        public static PriceQuote Price(SiteData site, List<CartIn> cart, bool pickup, string postcode)
        {
            PriceQuote q = new PriceQuote { Ok = cart.Count > 0 };
            Dictionary<string, int> used = new Dictionary<string, int>();
            foreach (CartIn c in cart)
            {
                Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == c.K);
                OrderLine line = new OrderLine { Key = c.K ?? string.Empty, Qty = Math.Max(1, Math.Min(20, c.Q)) };
                if (b == null || !b.Published)
                {
                    line.Title = "Unavailable bouquet";
                    line.Problem = "This bouquet is no longer available.";
                    q.Ok = false;
                    q.Lines.Add(line);
                    continue;
                }
                line.Title = b.Title;
                line.Image = Media.Thumb(Ui.Photo(b));
                BouquetSize? size = b.Sizes.FirstOrDefault(s => s.Key == c.S);
                if (size == null)
                {
                    line.Problem = "Please choose a size.";
                    q.Ok = false;
                    q.Lines.Add(line);
                    continue;
                }
                line.SizeKey = size.Key;
                line.SizeLabel = size.Label;
                decimal unit = size.Price;
                foreach (string ak in (c.A ?? new List<string>()).Distinct().Take(6))
                {
                    Addon? a = site.Addons.FirstOrDefault(x => x.Key == ak);
                    if (a != null)
                    {
                        unit += a.Price;
                        line.Extras.Add(a.Label);
                    }
                }
                line.Unit = unit;
                used[b.Key] = (used.TryGetValue(b.Key, out int n) ? n : 0) + line.Qty;
                if (used[b.Key] > b.Stock)
                {
                    line.Problem = b.Stock <= 0 ? "Sold out for now." : "Only " + b.Stock + " left.";
                    q.Ok = false;
                }
                line.Total = Round(unit * line.Qty);
                q.Subtotal += line.Total;
                q.Lines.Add(line);
            }
            if (!pickup)
            {
                Zone? z = ZoneFor(site, postcode);
                if (z == null)
                {
                    q.FeeNote = postcode.Trim().Length == 0 ? "Enter the postcode to see the delivery fee." : "We do not deliver to this postcode yet. Choose pickup instead.";
                }
                else
                {
                    q.ZoneName = z.Name;
                    q.Fee = z.FreeOver > 0 && q.Subtotal >= z.FreeOver ? 0 : z.Fee;
                    q.FeeNote = q.Fee == 0 ? "Free delivery to " + z.Name : z.Name;
                }
            }
            q.Tax = Round((q.Subtotal + q.Fee) * site.TaxPercent / 100m);
            q.Total = q.Subtotal + q.Fee + q.Tax;
            return q;
        }

        public static bool IsOpen(SiteData site, DateTime day)
        {
            return !site.ClosedDays.Contains((int)day.DayOfWeek) && !site.Blackout.Contains(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        public static List<DateTime> OpenDates(SiteData site, DateTime now)
        {
            List<DateTime> list = new List<DateTime>();
            DateTime first = now.Hour < site.CutOffHour ? now.Date : now.Date.AddDays(1);
            for (int i = 0; i <= Math.Min(60, Math.Max(1, site.MaxDaysAhead)) && list.Count < 21; i++)
            {
                DateTime d = first.AddDays(i);
                if (IsOpen(site, d))
                {
                    list.Add(d);
                }
            }
            return list;
        }

        public static string DateError(SiteData site, string date, string slot, DateTime now)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
            {
                return "Please choose a date.";
            }
            if (!OpenDates(site, now).Any(x => x.Date == d.Date))
            {
                return "We cannot serve that date. Please pick another.";
            }
            return string.Empty;
        }

        public static string SlotError(SiteData site, string slot)
        {
            return site.Slots.Any(s => s.Key == slot) ? string.Empty : "Please choose a time slot.";
        }
    }
}
