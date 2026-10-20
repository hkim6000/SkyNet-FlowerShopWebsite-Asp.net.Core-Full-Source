using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class Shop : WebPage
    {
        private static readonly string[] Prices = { "u50", "u75", "o75" };
        private static readonly string[] Sorts = { "new", "low", "high", "name" };

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Bouquets | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Browse hand-tied bouquets by occasion, price and size.");
            string occ = Ui.Pick(QueryValue("o"), site.Occasions.Select(o => o.Key));
            string price = Ui.Pick(QueryValue("p"), Prices);
            string sort = Ui.Pick(QueryValue("s"), Sorts);
            string find = Ui.Clip(QueryValue("term"), 40);
            if (sort == string.Empty)
            {
                sort = "new";
            }
            List<MediaItem> media = await Media.Load();
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_blocks}", PageHtml(site, media, "shop"))
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_foot}", Ui.Foot("sh", site))
                .Replace("{plhd_occ}", Ui.Esc(occ))
                .Replace("{plhd_find}", Ui.Esc(find))
                .Replace("{plhd_chips}", Chips(site, occ))
                .Replace("{plhd_priceopts}", PriceOptions(price))
                .Replace("{plhd_sortopts}", SortOptions(sort))
                .Replace("{plhd_count}", CountLine(Pick(site, occ, price, sort, find).Count, find))
                .Replace("{plhd_grid}", Grid(Pick(site, occ, price, sort, find)));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            string occ = Ui.Pick(GetDataValue("occ"), site.Occasions.Select(o => o.Key));
            string price = Ui.Pick(GetDataValue("pr"), Prices);
            string sort = Ui.Pick(GetDataValue("ord"), Sorts);
            string find = Ui.Clip(GetDataValue("find"), 40);
            if (sort == string.Empty)
            {
                sort = "new";
            }
            List<Bouquet> list = Pick(site, occ, price, sort, find);
            response.SetElementContents("sh-chips", Chips(site, occ));
            response.SetElementContents("sh-count", CountLine(list.Count, find));
            response.SetElementContents("sh-grid", Grid(list));
            response.ExecuteScript("ShopJs.filtered('" + Ui.Js(occ) + "','" + Ui.Js(price) + "','" + Ui.Js(sort) + "','" + Ui.Js(find) + "');");
            return response;
        }

        public async Task<ApiResponse> Suggest()
        {
            ApiResponse response = new ApiResponse();
            string term = Ui.Clip(GetDataValue("typed"), 40);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("sh-sugg", Ui.Suggest("sh", site, term));
            response.ExecuteScript("ShopJs.openSugg();");
            return response;
        }

        private static List<Bouquet> Pick(SiteData site, string occ, string price, string sort, string find)
        {
            IEnumerable<Bouquet> q = site.Bouquets.Where(b => b.Published);
            if (occ != string.Empty)
            {
                q = q.Where(b => b.Occasions.Contains(occ));
            }
            if (price == "u50")
            {
                q = q.Where(b => Ui.FromPrice(b) < 50);
            }
            else if (price == "u75")
            {
                q = q.Where(b => Ui.FromPrice(b) >= 50 && Ui.FromPrice(b) <= 75);
            }
            else if (price == "o75")
            {
                q = q.Where(b => Ui.FromPrice(b) > 75);
            }
            if (find != string.Empty)
            {
                q = q.Where(b => Ui.Has(b.Title, find) || Ui.Has(b.Blurb, find) || Ui.Has(b.Colors, find) || Ui.Has(b.Text, find));
            }
            switch (sort)
            {
                case "low": return q.OrderBy(b => Ui.FromPrice(b)).ToList();
                case "high": return q.OrderByDescending(b => Ui.FromPrice(b)).ToList();
                case "name": return q.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase).ToList();
                default: return q.OrderByDescending(b => b.Added).ToList();
            }
        }

        private static string Chips(SiteData site, string occ)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<button type=\"button\" class=\"sh-chip" + (occ == string.Empty ? " sh-act" : string.Empty) + "\" onclick=\"ShopJs.pick('')\">All</button>");
            foreach (Occasion o in site.Occasions)
            {
                sb.Append("<button type=\"button\" class=\"sh-chip" + (occ == o.Key ? " sh-act" : string.Empty) + "\" onclick=\"ShopJs.pick('" + Ui.Js(o.Key) + "')\">" + Ui.Esc(o.Label) + "</button>");
            }
            return sb.ToString();
        }

        private static string PriceOptions(string price)
        {
            string[,] o = { { "", "Any price" }, { "u50", "Under $50" }, { "u75", "$50 to $75" }, { "o75", "Over $75" } };
            return Options(o, price);
        }

        private static string SortOptions(string sort)
        {
            string[,] o = { { "new", "Newest" }, { "low", "Price, low to high" }, { "high", "Price, high to low" }, { "name", "Name" } };
            return Options(o, sort);
        }

        private static string Options(string[,] o, string current)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.GetLength(0); i++)
            {
                sb.Append("<option value=\"" + o[i, 0] + "\"" + (o[i, 0] == current ? " selected" : string.Empty) + ">" + o[i, 1] + "</option>");
            }
            return sb.ToString();
        }

        private static string CountLine(int n, string find)
        {
            string text = Ui.Count(n, "bouquet", "bouquets");
            return find == string.Empty ? text : text + " for &ldquo;" + Ui.Esc(find) + "&rdquo;<a href=\"Shop\">Clear search</a>";
        }

        private static string Grid(List<Bouquet> list)
        {
            if (list.Count == 0)
            {
                return "<div class=\"sh-empty\">No bouquets match. <a href=\"Shop\">Show all bouquets</a></div>";
            }
            StringBuilder sb = new StringBuilder();
            foreach (Bouquet b in list)
            {
                sb.Append(Ui.Card("sh", b));
            }
            return sb.ToString();
        }

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "flower" => a + "<circle cx=\"12\" cy=\"12\" r=\"2.5\"/><path d=\"M12 3.5v4M12 16.5v4M3.5 12h4M16.5 12h4M6 6l2.8 2.8M15.2 15.2L18 18M18 6l-2.8 2.8M8.8 15.2L6 18\"/></svg>",
                "leaf" => a + "<path d=\"M5 19c0-8 5-13 14-13-0 9-5 14-14 13zM5 19c3-4 6-7 10-9\"/></svg>",
                "gift" => a + "<rect x=\"3.5\" y=\"9\" width=\"17\" height=\"11\" rx=\"2\"/><path d=\"M3.5 13h17M12 9v11M8.5 9c-2 0-3-1-3-2.5S7 4 8.5 5.5 12 9 12 9M15.5 9c2 0 3-1 3-2.5S17 4 15.5 5.5 12 9 12 9\"/></svg>",
                "truck" => a + "<path d=\"M3 7h11v9H3zM14 10h4l3 3v3h-7z\"/><circle cx=\"7\" cy=\"17.5\" r=\"1.8\"/><circle cx=\"17\" cy=\"17.5\" r=\"1.8\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "pin" => a + "<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "cal" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "heart" => a + "<path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg>",
                "star" => a + "<path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg>",
                "users" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            if (Blocks.IsVideo(src))
            {
                return "<video src=\"" + Ui.Esc(src) + "\" playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return "<img src=\"" + Ui.Esc(src) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"sh-btn" + (main ? string.Empty : " sh-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"sh-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"sh-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"sh-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"sh-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "sh-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"sh-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"sh-wrap sh-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            return "<section class=\"sh-bhero" + (mv == string.Empty ? " sh-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"sh-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"sh-wrap sh-bhero-in\"><div class=\"sh-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"sh-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "sh-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"sh-bsplit" + (b.Flip ? " sh-bflip" : string.Empty) + (b.Media == string.Empty ? " sh-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"sh-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"sh-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"sh-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"sh-bcards sh-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"sh-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"sh-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"sh-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"sh-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"sh-bsteps sh-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"sh-bstats sh-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Initials(string name)
        {
            string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"sh-bpeople sh-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"sh-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"sh-bnoface\">" + Ui.Esc(Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"sh-bboxes sh-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"sh-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"sh-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>" + Icon("check") + "<span>" + Ui.Esc(Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"sh-bfaq\">" + Head(site, b, false) + "<div class=\"sh-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"sh-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"sh-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"sh-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"sh-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "sh-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(SiteData site, List<MediaItem> media, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(site, media, b, first),
                    "text" => Sec(b, "<div class=\"sh-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(site, media, b),
                    "cards" => Cards(site, b),
                    "steps" => Steps(site, b),
                    "stats" => StatRow(site, b),
                    "people" => People(site, media, b),
                    "boxes" => Boxes(site, b),
                    "faq" => Faq(site, b),
                    "quote" => Quote(site, media, b),
                    "media" => Wide(site, media, b),
                    "cta" => Cta(site, b),
                    "data" => DataPart(site, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            return sb.ToString();
        }

        private static string DataPart(SiteData site, Block b, bool first)
        {
            if (b.Source != "bouquetlist")
            {
                return string.Empty;
            }
            return Sec(b, Head(site, b, true, first ? "h1" : "h2") + "<input type=\"hidden\" id=\"sh-occ\" value=\"{plhd_occ}\"><input type=\"hidden\" id=\"sh-find\" value=\"{plhd_find}\"><div class=\"sh-bar\"><div class=\"sh-chips\" id=\"sh-chips\">{plhd_chips}</div><div class=\"sh-tools\"><label class=\"sh-sort\"><span>Price</span><select id=\"sh-price\" onchange=\"ShopJs.filter()\">{plhd_priceopts}</select></label><label class=\"sh-sort\"><span>Sort</span><select id=\"sh-sort\" onchange=\"ShopJs.filter()\">{plhd_sortopts}</select></label></div></div><div class=\"sh-count\" id=\"sh-count\">{plhd_count}</div><div class=\"sh-grid\" id=\"sh-grid\">{plhd_grid}</div>");
        }

    }
}
