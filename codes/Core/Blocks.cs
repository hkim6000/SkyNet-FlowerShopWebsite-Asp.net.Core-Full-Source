using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public sealed class PageSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Href { get; init; } = string.Empty;
        public string[] Data { get; init; } = Array.Empty<string>();
    }

    public sealed class PartSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Desc { get; init; } = string.Empty;
        public string Fields { get; init; } = string.Empty;
        public string ItemFields { get; init; } = string.Empty;
    }

    public static class Blocks
    {
        public static readonly PageSpec[] Pages =
        {
            new PageSpec { Key = "home", Name = "Home", Href = "Home", Data = new[] { "favourites", "occasions", "delivery" } },
            new PageSpec { Key = "shop", Name = "Shop", Href = "Shop", Data = new[] { "bouquetlist" } },
            new PageSpec { Key = "about", Name = "About", Href = "About", Data = Array.Empty<string>() },
            new PageSpec { Key = "track", Name = "Track an order", Href = "Track", Data = new[] { "trackform" } },
            new PageSpec { Key = "contact", Name = "Contact", Href = "Contact", Data = new[] { "contactform" } }
        };

        public static readonly PartSpec[] Types =
        {
            new PartSpec { Key = "hero", Name = "Page top", Desc = "Big title, text, buttons and a picture or video.", Fields = "kicker,title,text,media,buttons" },
            new PartSpec { Key = "text", Name = "Text", Desc = "A title and paragraphs.", Fields = "kicker,title,text" },
            new PartSpec { Key = "split", Name = "Picture and text", Desc = "A picture or video beside the text.", Fields = "kicker,title,text,media,button,flip" },
            new PartSpec { Key = "cards", Name = "Cards", Desc = "A row of cards with an icon, title, text and link.", Fields = "kicker,title,text,items", ItemFields = "icon,title,text,link" },
            new PartSpec { Key = "steps", Name = "Steps", Desc = "Numbered steps, for how ordering works.", Fields = "kicker,title,text,items", ItemFields = "title,text" },
            new PartSpec { Key = "stats", Name = "Numbers", Desc = "A row of big numbers with a label under each.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "boxes", Name = "Checklists", Desc = "Boxes with a title and one tick per line.", Fields = "kicker,title,items", ItemFields = "title,lines" },
            new PartSpec { Key = "people", Name = "People", Desc = "Portraits with a name and a role, for the team.", Fields = "kicker,title,text,items", ItemFields = "media,title,text" },
            new PartSpec { Key = "faq", Name = "Questions", Desc = "Questions that open to show the answer.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "quote", Name = "Quote", Desc = "A quote with a name and a small photo.", Fields = "quote,name,media" },
            new PartSpec { Key = "media", Name = "Picture or video", Desc = "One wide picture or video with a caption.", Fields = "media,caption" },
            new PartSpec { Key = "cta", Name = "Call to action", Desc = "A colored band with a title, text and buttons.", Fields = "kicker,title,text,buttons" }
        };

        public static readonly PartSpec[] Parts =
        {
            new PartSpec { Key = "favourites", Name = "Favourite bouquets", Desc = "Bouquets marked Show on the home page, with a link to the shop.", Fields = "kicker,title" },
            new PartSpec { Key = "occasions", Name = "Shop by occasion", Desc = "Every occasion with its photo. Change them on the Bouquets page.", Fields = "kicker,title" },
            new PartSpec { Key = "delivery", Name = "Delivery", Desc = "Zones, fees and the cut-off hour from Settings, with the three ordering steps.", Fields = "kicker,title,text" },
            new PartSpec { Key = "bouquetlist", Name = "Bouquet list", Desc = "Filters by occasion, price and sort, then every published bouquet.", Fields = "kicker,title,text" },
            new PartSpec { Key = "trackform", Name = "Track form", Desc = "Order number and email, then the order status.", Fields = "kicker,title,text" },
            new PartSpec { Key = "contactform", Name = "Contact details and form", Desc = "Address, phone, email and hours from Settings, beside the message form.", Fields = "kicker,title,text" }
        };

        public static readonly (string Key, string Name)[] Icons = { ("flower", "Flower"), ("leaf", "Leaf"), ("gift", "Gift"), ("truck", "Delivery van"), ("clock", "Clock"), ("pin", "Map pin"), ("cal", "Calendar"), ("heart", "Heart"), ("star", "Star"), ("users", "People"), ("check", "Tick"), ("phone", "Phone"), ("mail", "Email"), ("card", "Card") };

        private static readonly string[] Allowed = { "p", "br", "b", "i", "ul", "ol", "li", "a" };

        public static PageSpec? PageOf(string? key)
        {
            return Pages.FirstOrDefault(p => p.Key == (key ?? string.Empty).Trim().ToLowerInvariant());
        }

        public static PartSpec? Spec(Block b)
        {
            return b.Type == "data" ? Parts.FirstOrDefault(p => p.Key == b.Source) : Types.FirstOrDefault(t => t.Key == b.Type);
        }

        public static bool Has(PartSpec s, string field)
        {
            return ("," + s.Fields + ",").Contains("," + field + ",", StringComparison.Ordinal);
        }

        public static bool IsVideo(string src)
        {
            return src.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        public static string SafeLink(string? value)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Length == 0 || v.Length > 300)
            {
                return string.Empty;
            }
            if (Regex.IsMatch(v, "^https?://[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^mailto:[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^tel:[0-9+() .-]+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^#[A-Za-z0-9_-]+$") || Regex.IsMatch(v, "^[A-Za-z][A-Za-z0-9_-]*(\\?[A-Za-z0-9=&%._+-]*)?(#[A-Za-z0-9_-]+)?$"))
            {
                return v;
            }
            return string.Empty;
        }

        public static string Sanitize(string? html, int max)
        {
            string src = Regex.Replace(html ?? string.Empty, "<(script|style|template|iframe|object)[\\s\\S]*?</\\1\\s*>", string.Empty, RegexOptions.IgnoreCase);
            src = Regex.Replace(src, "<!--[\\s\\S]*?-->", string.Empty);
            StringBuilder sb = new StringBuilder();
            List<string> open = new List<string>();
            foreach (Match m in Regex.Matches(src, "<[^>]*>|[^<]+|<"))
            {
                string t = m.Value;
                if (!t.StartsWith('<') || t == "<")
                {
                    sb.Append(Ui.Esc(WebUtility.HtmlDecode(t)));
                    continue;
                }
                Match tag = Regex.Match(t, "^<\\s*(/?)\\s*([a-zA-Z][a-zA-Z0-9]*)([^>]*)>$");
                if (!tag.Success)
                {
                    continue;
                }
                bool close = tag.Groups[1].Value == "/";
                string name = tag.Groups[2].Value.ToLowerInvariant() switch
                {
                    "strong" => "b",
                    "em" => "i",
                    "div" => "p",
                    "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "p",
                    string n => n
                };
                if (!Allowed.Contains(name))
                {
                    continue;
                }
                if (name == "br")
                {
                    if (!close)
                    {
                        sb.Append("<br>");
                    }
                    continue;
                }
                if (close)
                {
                    int at = open.LastIndexOf(name);
                    if (at < 0)
                    {
                        continue;
                    }
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                    continue;
                }
                if (name == "p" && open.Contains("p"))
                {
                    int at = open.LastIndexOf("p");
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                }
                if (name == "a")
                {
                    Match h = Regex.Match(tag.Groups[3].Value, "href\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);
                    string href = h.Success ? SafeLink(WebUtility.HtmlDecode(h.Groups[2].Value + h.Groups[3].Value + h.Groups[4].Value)) : string.Empty;
                    if (href == string.Empty || open.Contains("a"))
                    {
                        continue;
                    }
                    bool outside = href.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                    sb.Append("<a href=\"" + Ui.Esc(href) + "\"" + (outside ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">");
                    open.Add("a");
                    continue;
                }
                sb.Append("<" + name + ">");
                open.Add(name);
            }
            for (int i = open.Count - 1; i >= 0; i--)
            {
                sb.Append("</" + open[i] + ">");
            }
            string outHtml = Regex.Replace(sb.ToString(), "<(p|b|i|li|ul|ol)>(\\s|<br>)*</\\1>", string.Empty);
            outHtml = Regex.Replace(outHtml, "(<br>\\s*){3,}", "<br><br>").Trim();
            return outHtml.Length > max ? string.Empty : outHtml;
        }

        public static string Plain(string? value, int max)
        {
            string v = Regex.Replace((value ?? string.Empty).Replace("\r", string.Empty), "[ \\t]+", " ");
            v = Regex.Replace(v, "\\n{3,}", "\n\n").Trim();
            return v.Length > max ? v.Substring(0, max).Trim() : v;
        }

        public static string NewId()
        {
            return "b" + Guid.NewGuid().ToString("N").Substring(0, 9);
        }

        public static (List<Block>? Blocks, string Error) Clean(List<MediaItem> library, PageSpec page, string? json)
        {
            List<Block>? raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<Block>>(json ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return (null, "The page could not be read. Reload and try again.");
            }
            if (raw == null || raw.Count == 0 || raw.Count > 40)
            {
                return (null, "A page needs between 1 and 40 blocks.");
            }
            HashSet<string> media = library.Select(m => m.Path).ToHashSet(StringComparer.Ordinal);
            HashSet<string> photos = library.Where(m => m.Kind == "photo").Select(m => m.Path).ToHashSet(StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<Block> list = new List<Block>();
            foreach (Block r in raw)
            {
                string type = r.Type ?? string.Empty;
                PartSpec? spec = type == "data" ? Parts.FirstOrDefault(p => p.Key == r.Source && page.Data.Contains(p.Key)) : Types.FirstOrDefault(t => t.Key == type);
                if (spec == null)
                {
                    return (null, "That page has a block it cannot use. Reload and try again.");
                }
                if (type == "data" && list.Any(x => x.Type == "data" && x.Source == spec.Key))
                {
                    return (null, spec.Name + " is on the page twice.");
                }
                string id = Regex.IsMatch(r.Id ?? string.Empty, "^b[a-z0-9]{3,12}$") && !ids.Contains(r.Id!) ? r.Id! : NewId();
                ids.Add(id);
                Block b = new Block { Id = id, Type = type, Source = type == "data" ? spec.Key : string.Empty, Hidden = r.Hidden };
                b.Kicker = Has(spec, "kicker") ? Plain(r.Kicker, 80) : string.Empty;
                b.Title = Has(spec, "title") || Has(spec, "name") ? Plain(r.Title, 140).Replace("\n", " ") : string.Empty;
                if (Has(spec, "text"))
                {
                    b.Text = Sanitize(r.Text, 6000);
                    if (b.Text == string.Empty && Regex.Replace(r.Text ?? string.Empty, "<[^>]*>|\\s|&nbsp;", string.Empty).Length > 0)
                    {
                        return (null, "The text in " + spec.Name + " is too long.");
                    }
                }
                else if (Has(spec, "quote") || Has(spec, "caption"))
                {
                    b.Text = Plain(r.Text, Has(spec, "quote") ? 600 : 240);
                }
                if (Has(spec, "media"))
                {
                    string m = (r.Media ?? string.Empty).Trim();
                    bool photoOnly = type == "quote";
                    if (m != string.Empty && !(photoOnly ? photos.Contains(m) : media.Contains(m)))
                    {
                        return (null, "A picture in " + spec.Name + " is no longer in the media library. Choose it again.");
                    }
                    b.Media = m;
                }
                if (Has(spec, "buttons") || Has(spec, "button"))
                {
                    b.Label = Plain(r.Label, 40).Replace("\n", " ");
                    b.Link = SafeLink(r.Link);
                    if (b.Label != string.Empty && b.Link == string.Empty)
                    {
                        return (null, "Check the link of the button “" + b.Label + "”. Use a page name like Shop, Contact or a full https:// address.");
                    }
                    if (Has(spec, "buttons"))
                    {
                        b.Label2 = Plain(r.Label2, 40).Replace("\n", " ");
                        b.Link2 = SafeLink(r.Link2);
                        if (b.Label2 != string.Empty && b.Link2 == string.Empty)
                        {
                            return (null, "Check the link of the button “" + b.Label2 + "”.");
                        }
                    }
                }
                b.Flip = Has(spec, "flip") && r.Flip;
                if (Has(spec, "items"))
                {
                    foreach (BlockItem it in (r.Items ?? new List<BlockItem>()).Take(12))
                    {
                        BlockItem c = new BlockItem { Title = Plain(it.Title, 120).Replace("\n", " "), Text = Plain(it.Text, 600) };
                        if (spec.ItemFields.Contains("icon"))
                        {
                            c.Icon = Icons.Any(x => x.Key == it.Icon) ? it.Icon : string.Empty;
                        }
                        if (spec.ItemFields.Contains("media"))
                        {
                            string m = (it.Media ?? string.Empty).Trim();
                            if (m != string.Empty && !photos.Contains(m))
                            {
                                return (null, "A photo in " + spec.Name + " is no longer in the media library. Choose it again.");
                            }
                            c.Media = m;
                        }
                        if (spec.ItemFields.Contains("link"))
                        {
                            c.Link = SafeLink(it.Link);
                            c.Label = c.Link != string.Empty ? Plain(it.Label, 40).Replace("\n", " ") : string.Empty;
                            if ((it.Link ?? string.Empty).Trim() != string.Empty && c.Link == string.Empty)
                            {
                                return (null, "Check the link on the card “" + c.Title + "”.");
                            }
                        }
                        if (c.Title != string.Empty || c.Text != string.Empty || c.Media != string.Empty)
                        {
                            b.Items.Add(c);
                        }
                    }
                }
                list.Add(b);
            }
            string? missing = page.Data.FirstOrDefault(k => !list.Any(x => x.Type == "data" && x.Source == k));
            if (missing != null)
            {
                return (null, (Parts.FirstOrDefault(p => p.Key == missing)?.Name ?? missing) + " must stay on the page. Hide it instead.");
            }
            return (list, string.Empty);
        }

        private static Block Data(string key, string kicker = "", string title = "", string text = "")
        {
            return new Block { Id = NewId(), Type = "data", Source = key, Kicker = kicker, Title = title, Text = text };
        }

        private static string Paras(IEnumerable<string> lines)
        {
            return string.Concat(lines.Where(l => l.Trim().Length > 0).Select(l => "<p>" + Ui.Esc(l.Trim()) + "</p>"));
        }

        private static string HeroImage(SiteData site)
        {
            return site.Hero.Image.Length > 0 ? site.Hero.Image : "images/hero.webp";
        }

        private static List<Block> Legacy(SiteData site)
        {
            List<Block> list = new List<Block>();
            IEnumerable<Block> old = site.Blocks.Count > 0 ? site.Blocks : new List<Block> { new Block { Type = "hero" }, new Block { Type = "promises" }, new Block { Type = "favourites", Title = "Our favourites" }, new Block { Type = "occasions", Title = "Shop by occasion" }, new Block { Type = "delivery", Eyebrow = "Delivery", Title = "We deliver across the city" } };
            foreach (Block o in old)
            {
                switch (o.Type)
                {
                    case "hero":
                        list.Add(new Block { Id = NewId(), Type = "hero", Hidden = !o.Visible, Kicker = site.Hero.Eyebrow, Title = site.Hero.Title.Length > 0 ? site.Hero.Title : "Fresh flowers, every morning", Text = site.Hero.Text.Length > 0 ? "<p>" + Ui.Esc(site.Hero.Text) + "</p>" : string.Empty, Media = HeroImage(site), Label = site.Hero.Button.Length > 0 ? site.Hero.Button : "Shop bouquets", Link = "Shop", Label2 = "Track an order", Link2 = "Track" });
                        break;
                    case "promises":
                        list.Add(new Block { Id = NewId(), Type = "cards", Hidden = !o.Visible, Items = site.Promises.Select(p => new BlockItem { Title = p.Title, Text = p.Text }).ToList() });
                        break;
                    case "favourites":
                        list.Add(new Block { Id = NewId(), Type = "data", Source = "favourites", Hidden = !o.Visible, Title = o.Title.Length > 0 ? o.Title : "Our favourites" });
                        break;
                    case "occasions":
                        list.Add(new Block { Id = NewId(), Type = "data", Source = "occasions", Hidden = !o.Visible, Title = o.Title.Length > 0 ? o.Title : "Shop by occasion" });
                        break;
                    case "delivery":
                        list.Add(new Block { Id = NewId(), Type = "data", Source = "delivery", Hidden = !o.Visible, Kicker = o.Eyebrow, Title = o.Title.Length > 0 ? o.Title : "We deliver across the city" });
                        break;
                    case "text":
                        list.Add(new Block { Id = NewId(), Type = "text", Hidden = !o.Visible, Kicker = o.Eyebrow, Title = o.Title, Text = Paras(o.Text.Replace("\r", string.Empty).Split("\n\n")) });
                        break;
                    case "photo":
                        list.Add(new Block { Id = NewId(), Type = "media", Hidden = !o.Visible, Media = o.Media, Text = o.Caption.Length > 0 ? o.Caption : o.Title });
                        break;
                    case "phototext":
                        list.Add(new Block { Id = NewId(), Type = "split", Hidden = !o.Visible, Kicker = o.Eyebrow, Title = o.Title, Text = Paras(o.Text.Replace("\r", string.Empty).Split("\n\n")), Media = o.Media, Flip = o.Side == "right" });
                        break;
                    case "video":
                        list.Add(new Block { Id = NewId(), Type = "media", Hidden = !o.Visible, Media = o.Media, Text = o.Caption.Length > 0 ? o.Caption : o.Title });
                        break;
                }
            }
            return list;
        }

        public static void Ensure(SiteData site)
        {
            bool fresh = site.Pages.Count == 0;
            foreach (PageSpec p in Pages)
            {
                if (!site.Pages.TryGetValue(p.Key, out PageDoc? doc) || doc == null)
                {
                    doc = new PageDoc();
                    site.Pages[p.Key] = doc;
                    if (fresh)
                    {
                        doc.Blocks = Seed(site, p.Key);
                    }
                }
                foreach (string k in p.Data)
                {
                    if (!doc.Blocks.Any(b => b.Type == "data" && b.Source == k))
                    {
                        doc.Blocks.Add(Data(k, string.Empty, Parts.First(x => x.Key == k).Name));
                    }
                }
            }
            if (fresh)
            {
                site.Blocks.Clear();
            }
        }

        private static List<Block> Seed(SiteData site, string key)
        {
            switch (key)
            {
                case "home":
                    return Legacy(site);
                case "shop":
                    return new List<Block> { Data("bouquetlist", "Shop", "Bouquets", "<p>Hand-tied from what is fresh today. Pick a size, add a vase or chocolates, choose your day.</p>") };
                case "about":
                    {
                        List<Block> list = new List<Block>();
                        list.Add(new Block { Id = NewId(), Type = "split", Kicker = "About us", Title = site.AboutTitle.Length > 0 ? site.AboutTitle : "A small studio with a big love of flowers", Text = (site.AboutLead.Length > 0 ? "<p><b>" + Ui.Esc(site.AboutLead) + "</b></p>" : string.Empty) + Paras(site.AboutParagraphs), Media = "images/about/workshop.webp", Label = "Shop bouquets", Link = "Shop" });
                        if (site.Care.Count > 0)
                        {
                            list.Add(new Block { Id = NewId(), Type = "steps", Title = "Making your flowers last", Items = site.Care.Select(c => new BlockItem { Title = c.Title, Text = c.Text }).ToList() });
                        }
                        list.Add(new Block { Id = NewId(), Type = "cta", Title = "Ready to order?", Text = "<p>Pick a day and a time slot, and we will have it ready.</p>", Label = "Shop bouquets", Link = "Shop", Label2 = "Get in touch", Link2 = "Contact" });
                        return list;
                    }
                case "track":
                    return new List<Block> { Data("trackform", "Track an order", "Where are my flowers?", "<p>Enter the order number from your confirmation and the email you used.</p>") };
                case "contact":
                    {
                        List<Block> list = new List<Block> { Data("contactform", "Contact", "Say hello", "<p>Questions about an order, a wedding or a big delivery? Send us a note and we will reply by email.</p>") };
                        if (site.Faqs.Count > 0)
                        {
                            list.Add(new Block { Id = NewId(), Type = "faq", Title = "Good to know", Items = site.Faqs.Select(f => new BlockItem { Title = f.Q, Text = f.A }).ToList() });
                        }
                        return list;
                    }
                default:
                    return new List<Block>();
            }
        }

        public static List<string> Uses(SiteData site, string src)
        {
            List<string> u = new List<string>();
            foreach (PageSpec p in Pages)
            {
                if (site.Pages.TryGetValue(p.Key, out PageDoc? doc) && doc.Blocks.Any(b => b.Media == src || b.Items.Any(i => i.Media == src)))
                {
                    u.Add("Page: " + p.Name);
                }
            }
            return u;
        }
    }
}
