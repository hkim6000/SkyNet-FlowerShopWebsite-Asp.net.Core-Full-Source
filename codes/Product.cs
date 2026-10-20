using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class Product : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            string key = Ui.Clip(QueryValue("k"), 60).ToLowerInvariant();
            Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == key && x.Published);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            if (b == null)
            {
                HtmlDoc.SetTitle("Bouquet not found | " + site.Brand);
                HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                    .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_foot}", Ui.Foot("pd", site))
                    .Replace("{plhd_hidefound}", "hidden")
                    .Replace("{plhd_hidemissing}", string.Empty)
                    .Replace("{plhd_title}", string.Empty)
                    .Replace("{plhd_key}", string.Empty)
                    .Replace("{plhd_img}", "images/hero.webp")
                    .Replace("{plhd_thumbs}", string.Empty)
                    .Replace("{plhd_alt}", string.Empty)
                    .Replace("{plhd_occ}", string.Empty)
                    .Replace("{plhd_blurb}", string.Empty)
                    .Replace("{plhd_price}", string.Empty)
                    .Replace("{plhd_sizes}", string.Empty)
                    .Replace("{plhd_addons}", string.Empty)
                    .Replace("{plhd_stock}", string.Empty)
                    .Replace("{plhd_text}", string.Empty)
                    .Replace("{plhd_facts}", string.Empty)
                    .Replace("{plhd_related}", string.Empty);
                return;
            }
            HtmlDoc.SetTitle(b.Title + " | " + site.Brand);
            HtmlDoc.AddMetaElement("description", b.Blurb);
            string occ = string.Join(" / ", b.Occasions.Select(k => site.Occasions.FirstOrDefault(o => o.Key == k)?.Label ?? k));
            StringBuilder sizes = new StringBuilder();
            for (int i = 0; i < b.Sizes.Count; i++)
            {
                BouquetSize s = b.Sizes[i];
                sizes.Append("<label class=\"pd-size\"><input type=\"radio\" name=\"pd-size\" value=\"" + Ui.Esc(s.Key) + "\"" + (i == Math.Min(1, b.Sizes.Count - 1) ? " checked" : string.Empty) + "><i></i><span>" + Ui.Esc(s.Label) + "</span><em>" + Ui.Money(s.Price) + "</em></label>");
            }
            StringBuilder addons = new StringBuilder();
            foreach (Addon a in site.Addons)
            {
                addons.Append("<label class=\"pd-extra\"><input type=\"checkbox\" name=\"pd-extra\" value=\"" + Ui.Esc(a.Key) + "\"><i></i><span>" + Ui.Esc(a.Label) + "</span><em>+" + Ui.Money(a.Price) + "</em></label>");
            }
            List<Bouquet> related = site.Bouquets.Where(x => x.Published && x.Key != b.Key && x.Occasions.Intersect(b.Occasions).Any()).Take(4).ToList();
            if (related.Count < 4)
            {
                related.AddRange(site.Bouquets.Where(x => x.Published && x.Key != b.Key && !related.Contains(x)).Take(4 - related.Count));
            }
            StringBuilder cards = new StringBuilder();
            foreach (Bouquet r in related)
            {
                cards.Append(Ui.Card("pd", r));
            }
            string cutoff = DateTime.Today.AddHours(site.CutOffHour).ToString("h tt", CultureInfo.InvariantCulture).ToLowerInvariant();
            string facts = "<li>Hand-tied and wrapped in recycled kraft paper.</li><li>Order before " + cutoff + " for same-day delivery.</li><li>Seven-day freshness promise.</li>";
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_foot}", Ui.Foot("pd", site))
                .Replace("{plhd_hidefound}", string.Empty)
                .Replace("{plhd_hidemissing}", "hidden")
                .Replace("{plhd_title}", Ui.Esc(b.Title))
                .Replace("{plhd_key}", Ui.Esc(b.Key))
                .Replace("{plhd_img}", Ui.Esc(Ui.Photo(b)))
                .Replace("{plhd_thumbs}", Thumbs(b))
                .Replace("{plhd_alt}", Ui.Esc(b.Title + ": " + b.Blurb))
                .Replace("{plhd_occ}", Ui.Esc(occ))
                .Replace("{plhd_blurb}", Ui.Esc(b.Blurb))
                .Replace("{plhd_price}", Ui.Money(b.Sizes.Count > 1 ? b.Sizes[1].Price : Ui.FromPrice(b)))
                .Replace("{plhd_sizes}", sizes.ToString())
                .Replace("{plhd_addons}", addons.ToString())
                .Replace("{plhd_stock}", StockNote(site, b))
                .Replace("{plhd_text}", Ui.Esc(b.Text))
                .Replace("{plhd_facts}", facts)
                .Replace("{plhd_related}", cards.ToString());
        }

        private static string Thumbs(Bouquet b)
        {
            if (b.Photos.Count < 2)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder("<div class=\"pd-thumbs\" id=\"pd-thumbs\">");
            for (int i = 0; i < b.Photos.Count; i++)
            {
                bool video = b.Photos[i].EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
                sb.Append("<button type=\"button\" class=\"pd-th" + (i == 0 ? " pd-th-on" : string.Empty) + "\" data-src=\"" + Ui.Esc(b.Photos[i]) + "\" data-kind=\"" + (video ? "video" : "photo") + "\" aria-label=\"" + (video ? "Video " : "Photo ") + (i + 1) + " of " + b.Photos.Count + "\" aria-pressed=\"" + (i == 0 ? "true" : "false") + "\" onclick=\"ProductJs.photo(this)\">" + (video ? "<video src=\"" + Ui.Esc(b.Photos[i]) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><i class=\"pd-th-play\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"currentColor\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : "<img src=\"" + Ui.Esc(Media.Thumb(b.Photos[i])) + "\" alt=\"\" loading=\"lazy\">") + "</button>");
            }
            return sb.Append("</div>").ToString();
        }

        public async Task<ApiResponse> Quote()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == Ui.Clip(GetDataValue("item"), 60) && x.Published);
            if (b == null)
            {
                response.ExecuteScript("ProductJs.stock(false);");
                return response;
            }
            (CartIn? line, string error) = Build(site, b);
            if (line == null)
            {
                response.SetElementContents("pd-stock", Ui.Esc(error));
                response.ExecuteScript("ProductJs.stock(false);");
                return response;
            }
            PriceQuote q = Pricing.Price(site, new List<CartIn> { line }, true, string.Empty);
            decimal unit = q.Lines.Count > 0 ? q.Lines[0].Unit : 0;
            string each = line.Q > 1 ? "<small>" + Ui.Money(unit) + " each</small>" : string.Empty;
            response.SetElementContents("pd-price", Ui.Money(q.Subtotal) + each);
            response.SetElementContents("pd-stock", StockNote(site, b, line.Q));
            response.ExecuteScript("ProductJs.stock(" + (b.Stock >= line.Q ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> Add()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == Ui.Clip(GetDataValue("item"), 60) && x.Published);
            if (b == null)
            {
                response.SetElementContents("pd-stock", "This bouquet is no longer available.");
                return response;
            }
            (CartIn? line, string error) = Build(site, b);
            if (line == null)
            {
                response.SetElementContents("pd-stock", Ui.Esc(error));
                return response;
            }
            if (b.Stock < line.Q)
            {
                response.SetElementContents("pd-stock", b.Stock <= 0 ? "Sold out for now." : "Only " + b.Stock + " left.");
                return response;
            }
            string json = JsonSerializer.Serialize(new { k = line.K, s = line.S, a = line.A, q = line.Q });
            response.SetElementContents("pd-stock", string.Empty);
            response.ExecuteScript("ProductJs.added(" + json.Replace("<", "\\u003c") + ");");
            return response;
        }

        private (CartIn? line, string error) Build(SiteData site, Bouquet b)
        {
            BouquetSize? size = b.Sizes.FirstOrDefault(s => s.Key == Ui.Clip(GetDataValue("sz"), 30));
            if (size == null)
            {
                return (null, "Please choose a size.");
            }
            List<string> picked = new List<string>();
            foreach (string part in (GetDataValue("ex") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Take(6))
            {
                string k = part.Trim();
                if (site.Addons.Any(a => a.Key == k) && !picked.Contains(k))
                {
                    picked.Add(k);
                }
            }
            int qty = Ui.Bound(GetDataValue("qty"), 1, 20, 1);
            return (new CartIn { K = b.Key, S = size.Key, A = picked, Q = qty }, string.Empty);
        }

        private static string StockNote(SiteData site, Bouquet b, int qty = 1)
        {
            if (b.Stock <= 0)
            {
                return "Sold out for now. Check back tomorrow.";
            }
            if (b.Stock < qty)
            {
                return "Only " + b.Stock + " left.";
            }
            return b.Stock <= site.LowStock ? "Only " + b.Stock + " left today." : string.Empty;
        }

        public async Task<ApiResponse> Suggest()
        {
            ApiResponse response = new ApiResponse();
            string term = Ui.Clip(GetDataValue("typed"), 40);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("pd-sugg", Ui.Suggest("pd", site, term));
            response.ExecuteScript("ProductJs.openSugg();");
            return response;
        }
    }
}
