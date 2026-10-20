using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminProducts : WebPage
    {
        private const string AreaKey = "products";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Products | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminProductsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminProductsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ap-panel\"><h1>No access</h1><p class=\"ap-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ap-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ap-menu", await Auth.Menu("ap", "AdminProducts", c));
            response.SetElementContents("ap-who", Auth.Who("ap", c));
        }

        private const int Size = 10;

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
                response.SetElementContents("ap-main", NoAccess("bouquets"));
                return response;
            }
            response.SetElementContents("ap-main", GetDataValue("tab") == "occasions" ? OccasionsHtml(site, c) : BouquetsHtml(site, c));
            return response;
        }

        private static string TabsHtml(string cur)
        {
            return "<div class=\"ap-tabs\"><button type=\"button\" class=\"ap-tab" + (cur == "bouquets" ? " ap-act" : string.Empty) + "\" onclick=\"AdminProductsJs.tab('bouquets')\">Bouquets</button><button type=\"button\" class=\"ap-tab" + (cur == "occasions" ? " ap-act" : string.Empty) + "\" onclick=\"AdminProductsJs.tab('occasions')\">Occasions</button></div>";
        }

        public async Task<ApiResponse> Tab()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open bouquets", string.Empty, "You do not have access to bouquets.");
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ap-main", GetDataValue("tab") == "occasions" ? OccasionsHtml(site, c) : BouquetsHtml(site, c));
            return response;
        }

        private static string OccasionsHtml(SiteData site, Ctx c)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-ph\"><div><h1>Occasions</h1><p>The occasions customers shop by, with a photo and a short line each.</p></div></div>" + TabsHtml("occasions"));
            sb.Append("<form class=\"ap-panel ap-stack\" id=\"ap-oform\" onsubmit=\"return AdminProductsJs.saveOccasions()\" novalidate>");
            for (int i = 0; i < site.Occasions.Count; i++)
            {
                Occasion o = site.Occasions[i];
                sb.Append("<div class=\"ap-occrow\"><div class=\"ap-field\"><span>Photo</span><div class=\"ap-opic\" id=\"ap-opic-" + i + "\"><img src=\"" + Ui.Esc(Media.Thumb(o.Image)) + "\" alt=\"\"></div><input type=\"hidden\" id=\"ap-f-oi" + i + "\" data-k=\"oi" + i + "\" value=\"" + Ui.Esc(o.Image) + "\">" + (edit ? "<div><button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" onclick=\"AdminProductsJs.pickOne('oi" + i + "')\">Choose photo</button></div>" : string.Empty) + "<em class=\"ap-err\" id=\"ap-e-oi" + i + "\"></em></div><div class=\"ap-stack\"><label class=\"ap-field\" for=\"ap-f-ol" + i + "\"><span>Occasion name</span><input type=\"text\" id=\"ap-f-ol" + i + "\" data-k=\"ol" + i + "\" maxlength=\"30\" value=\"" + Ui.Esc(o.Label) + "\"" + (edit ? string.Empty : " disabled") + "><em class=\"ap-err\" id=\"ap-e-ol" + i + "\"></em></label><label class=\"ap-field\" for=\"ap-f-ob" + i + "\"><span>One-line description</span><input type=\"text\" id=\"ap-f-ob" + i + "\" data-k=\"ob" + i + "\" maxlength=\"80\" value=\"" + Ui.Esc(o.Blurb) + "\"" + (edit ? string.Empty : " disabled") + "><em class=\"ap-err\" id=\"ap-e-ob" + i + "\"></em></label></div></div>");
            }
            sb.Append(edit ? "<div class=\"ap-acts\"><button type=\"submit\" class=\"ap-btn\">Save occasions</button></div>" : "<p class=\"ap-hint\">You have view-only access.</p>");
            sb.Append("</form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> SaveOccasions()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save occasions", string.Empty, "You can look at occasions but not change them.");
            }
            List<MediaItem> media = await Media.Load();
            Dictionary<string, string> errors = new Dictionary<string, string>();
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                for (int i = 0; i < site.Occasions.Count; i++)
                {
                    string label = Ui.Clip(GetDataValue("ol" + i), 30);
                    string blurb = Ui.Clip(GetDataValue("ob" + i), 80);
                    string img = Ui.Clip(GetDataValue("oi" + i), 200);
                    if (label.Length < 2)
                    {
                        errors["ol" + i] = "Enter 2 to 30 characters.";
                    }
                    if (blurb.Length < 3)
                    {
                        errors["ob" + i] = "Enter 3 to 80 characters.";
                    }
                    if (Media.Find(media, img, "photo") == null)
                    {
                        errors["oi" + i] = "Choose a photo from the library.";
                    }
                    response.SetElementContents("ap-e-ol" + i, errors.TryGetValue("ol" + i, out string? e1) ? e1 : string.Empty);
                    response.SetElementContents("ap-e-ob" + i, errors.TryGetValue("ob" + i, out string? e2) ? e2 : string.Empty);
                    response.SetElementContents("ap-e-oi" + i, errors.TryGetValue("oi" + i, out string? e3) ? e3 : string.Empty);
                    if (errors.Count == 0)
                    {
                        site.Occasions[i].Label = label;
                        site.Occasions[i].Blurb = blurb;
                        site.Occasions[i].Image = img;
                    }
                }
                if (errors.Count > 0)
                {
                    return response;
                }
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "edit occasions", string.Empty, true);
            response.ExecuteScript("AdminProductsJs.toast('Saved. The website shows it now.');");
            return response;
        }

        private static string BouquetsHtml(SiteData site, Ctx c)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-ph\"><div><h1>Bouquets</h1><p>What the shop sells, with sizes, prices and stock.</p></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"ap-btn\" onclick=\"AdminProductsJs.edit('')\">Add a bouquet</button>");
            }
            sb.Append("</div>" + TabsHtml("bouquets") + "<div class=\"ap-tools\"><input type=\"search\" id=\"ap-term\" placeholder=\"Search bouquets\" maxlength=\"40\" oninput=\"AdminProductsJs.type()\" aria-label=\"Search bouquets\">");
            sb.Append("<select id=\"ap-st\" onchange=\"AdminProductsJs.list(1)\" aria-label=\"State\"><option value=\"\">All bouquets</option><option value=\"live\">Published</option><option value=\"hidden\">Hidden</option><option value=\"low\">Low or sold out</option></select>");
            sb.Append("<select id=\"ap-occ\" onchange=\"AdminProductsJs.list(1)\" aria-label=\"Occasion\"><option value=\"\">Any occasion</option>");
            foreach (Occasion o in site.Occasions)
            {
                sb.Append("<option value=\"" + Ui.Esc(o.Key) + "\">" + Ui.Esc(o.Label) + "</option>");
            }
            sb.Append("</select></div><div id=\"ap-list\">" + ListHtml(site, c, string.Empty, string.Empty, string.Empty, 1) + "</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "list bouquets", string.Empty, "You do not have access to bouquets.");
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ap-list", ListHtml(site, c, Ui.Clip(GetDataValue("term"), 40), Ui.Clip(GetDataValue("st"), 8), Ui.Clip(GetDataValue("occ"), 30), Ui.Bound(GetDataValue("pg"), 1, 100000, 1)));
            return response;
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Access.Can(c.U, AreaKey, 'V') || (id == string.Empty && !Access.Can(c.U, AreaKey, 'E')))
            {
                return await Refuse(response, c, "open bouquet", id, "You can look at bouquets but not add them.");
            }
            SiteData site = await Store.LoadSite();
            Bouquet? b = id == string.Empty ? null : site.Bouquets.FirstOrDefault(x => x.Key == id);
            if (id != string.Empty && b == null)
            {
                response.ExecuteScript("AdminProductsJs.toast('That bouquet was not found.');");
                return response;
            }
            response.SetElementContents("ap-mbody", FormHtml(site, c, b));
            response.ExecuteScript("AdminProductsJs.opened('" + (b == null ? "New bouquet" : Ui.Js(b.Title)) + "');");
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
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save bouquet", id, "You can look at bouquets but not change them.");
            }
            SiteData site = await Store.LoadSite();
            string title = Ui.Clip(GetDataValue("title"), 80);
            string blurb = Ui.Clip(GetDataValue("blurb"), 160);
            string text = Ui.Clip(GetDataValue("text"), 700);
            string colors = Ui.Clip(GetDataValue("colors"), 80);
            List<MediaItem> media = await Media.Load();
            List<string> photos = (GetDataValue("photos") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => Ui.Clip(x, 200)).Distinct().ToList();
            List<string> occs = (GetDataValue("occs") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => site.Occasions.Any(o => o.Key == x)).Distinct().ToList();
            Bouquet? old = id == string.Empty ? null : site.Bouquets.FirstOrDefault(x => x.Key == id);
            List<BouquetSize> sizes = new List<BouquetSize>();
            string eSizes = ParseSizes(GetDataValue("sizes"), old, sizes);
            bool stockOk = int.TryParse((GetDataValue("stock") ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int stock) && stock >= 0 && stock <= 999;
            string eTitle = title.Length < 2 || title.Length > 60 ? "Enter a name of 2 to 60 characters." : string.Empty;
            string eBlurb = blurb.Length < 3 ? "Add a short line about the bouquet." : blurb.Length > 100 ? "Keep it under 100 characters." : string.Empty;
            string eText = text.Length > 600 ? "Keep it under 600 characters." : string.Empty;
            string eImage = photos.Count == 0 ? "Choose at least one photo." : photos.Count > MaxPhotos ? "Choose up to " + MaxPhotos + " photos." : photos.Any(x => Media.Find(media, x, string.Empty) == null) ? "One of the files is no longer in the library. Choose again." : Media.Find(media, photos[0], "photo") == null ? "The first item must be a photo. It is used on cards and in the cart." : string.Empty;
            string eStock = stockOk ? string.Empty : "Enter a whole number from 0 to 999.";
            response.SetElementContents("ap-e-title", eTitle);
            response.SetElementContents("ap-e-blurb", eBlurb);
            response.SetElementContents("ap-e-text", eText);
            response.SetElementContents("ap-e-photos", eImage);
            response.SetElementContents("ap-e-stock", eStock);
            response.SetElementContents("ap-e-sizes", eSizes);
            if (id != string.Empty && old == null)
            {
                response.ExecuteScript("AdminProductsJs.toast('That bouquet was not found.');");
                return response;
            }
            if (eTitle + eBlurb + eText + eImage + eStock + eSizes != string.Empty)
            {
                return response;
            }
            bool featured = GetDataValue("featured") == "1";
            bool canPublish = Access.Can(c.U, AreaKey, 'X');
            bool published = GetDataValue("published") == "1";
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Bouquet? b = id == string.Empty ? null : site.Bouquets.FirstOrDefault(x => x.Key == id);
                if (b == null)
                {
                    string key = Ui.Slug(title);
                    key = key.Length == 0 ? "bouquet" : key.Length > 40 ? key.Substring(0, 40) : key;
                    string unique = key;
                    int n = 2;
                    while (site.Bouquets.Any(x => x.Key == unique))
                    {
                        unique = key + "-" + n++;
                    }
                    b = new Bouquet { Key = unique, Added = 0, Published = canPublish && published };
                    site.Bouquets.Add(b);
                }
                else if (canPublish)
                {
                    b.Published = published;
                }
                b.Title = title;
                b.Blurb = blurb;
                b.Text = text;
                b.Colors = colors;
                b.Photos = photos;
                b.Occasions = occs;
                b.Sizes = sizes;
                b.Stock = stock;
                b.Featured = featured;
                await Store.SaveSite(site);
                id = b.Key;
            }
            await Store.Log(c.U.Id, "save bouquet", title, true);
            response.SetElementContents("ap-list", ListHtml(site, c, Ui.Clip(GetDataValue("term"), 40), Ui.Clip(GetDataValue("st"), 8), Ui.Clip(GetDataValue("occ"), 30), Ui.Bound(GetDataValue("pg"), 1, 100000, 1)));
            response.SetElementContents("ap-menu", await Auth.Menu("ap", "AdminProducts", c));
            response.ExecuteScript("AdminProductsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Publish()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "publish bouquet", id, "You are not allowed to publish or hide bouquets.");
            }
            bool to = GetDataValue("to") == "1";
            SiteData site;
            string title;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == id);
                if (b == null)
                {
                    response.ExecuteScript("AdminProductsJs.toast('That bouquet was not found.');");
                    return response;
                }
                b.Published = to;
                title = b.Title;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, to ? "publish bouquet" : "hide bouquet", title, true);
            response.SetElementContents("ap-list", ListHtml(site, c, Ui.Clip(GetDataValue("term"), 40), Ui.Clip(GetDataValue("st"), 8), Ui.Clip(GetDataValue("occ"), 30), Ui.Bound(GetDataValue("pg"), 1, 100000, 1)));
            response.ExecuteScript("AdminProductsJs.done('" + (to ? "Published" : "Hidden") + "');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "delete bouquet", id, "You are not allowed to delete bouquets.");
            }
            SiteData site;
            string title;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == id);
                if (b == null)
                {
                    response.ExecuteScript("AdminProductsJs.toast('That bouquet was not found.');");
                    return response;
                }
                title = b.Title;
                site.Bouquets.Remove(b);
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "delete bouquet", title, true);
            response.SetElementContents("ap-list", ListHtml(site, c, Ui.Clip(GetDataValue("term"), 40), Ui.Clip(GetDataValue("st"), 8), Ui.Clip(GetDataValue("occ"), 30), Ui.Bound(GetDataValue("pg"), 1, 100000, 1)));
            response.ExecuteScript("AdminProductsJs.done('Bouquet deleted');");
            return response;
        }

        public async Task<ApiResponse> Pick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "choose photos", string.Empty, "You can look at bouquets but not change them.");
            }
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-pktools\"><input type=\"search\" id=\"ap-pkq\" placeholder=\"Search the library\" maxlength=\"40\" oninput=\"AdminProductsJs.pkfilter()\" aria-label=\"Search the library\">");
            sb.Append("<label class=\"ap-btn ap-btn-ghost ap-upbtn\"><input type=\"file\" id=\"ap-pkfile\" multiple accept=\"" + Media.Accept + "\" onchange=\"AdminProductsJs.upload()\">Upload new</label></div>");
            sb.Append("<p class=\"ap-hint\">Click photos and videos in the order you want them. The first one must be a photo; it is the main picture. Up to " + MaxPhotos + ". Photos up to " + Media.PhotoMb + " MB, MP4 up to " + site.MaxVideoMb + " MB.</p><p class=\"ap-upmsg\" id=\"ap-pkmsg\" role=\"status\"></p>");
            sb.Append("<div class=\"ap-pkgrid\" id=\"ap-pkgrid\">" + PickGrid(media) + "</div>");
            sb.Append("<div class=\"ap-acts ap-pkacts\"><button type=\"button\" class=\"ap-btn\" onclick=\"AdminProductsJs.usePicked()\">Use these</button><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminProductsJs.closePick()\">Cancel</button><span class=\"ap-hint\" id=\"ap-pkcount\"></span></div>");
            response.SetElementContents("ap-pkbody", sb.ToString());
            response.ExecuteScript("AdminProductsJs.picking();");
            return response;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                response.ExecuteScript("AdminProductsJs.uploaded();");
                return await Refuse(response, c, "upload media", string.Empty, "You can look at bouquets but not change them.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.SetElementContents("ap-pkmsg", "Choose one or more photos or videos first.");
                response.ExecuteScript("AdminProductsJs.uploaded();");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("ap-pkgrid", PickGrid(media));
            response.SetElementContents("ap-pkmsg", Ui.Esc(((added > 0 ? Ui.Count(added, "file", "files") + " added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()));
            response.ExecuteScript("AdminProductsJs.uploaded();");
            return response;
        }

        private const int MaxPhotos = 6;

        private static string PickGrid(List<MediaItem> media)
        {
            List<MediaItem> rows = media.OrderByDescending(m => m.Added).Take(400).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ap-empty\">Nothing in the library yet. Upload a photo or video above.</div>";
            }
            StringBuilder sb = new StringBuilder();
            foreach (MediaItem m in rows)
            {
                string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
                sb.Append("<button type=\"button\" class=\"ap-pi\" data-p=\"" + Ui.Esc(m.Path) + "\" data-t=\"" + Ui.Esc(thumb) + "\" data-n=\"" + Ui.Esc((m.Name + " " + m.Alt).ToLowerInvariant()) + "\" data-kind=\"" + m.Kind + "\" onclick=\"AdminProductsJs.choose(this)\" title=\"" + Ui.Esc(m.Name) + "\">" + (m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><i class=\"ap-pi-play\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"currentColor\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : "<img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\">") + "<i class=\"ap-pi-n\"></i><span>" + Ui.Esc(m.Name) + "</span></button>");
            }
            return sb.ToString();
        }

        private static string PhotoStrip(List<string> photos, bool edit)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < photos.Count; i++)
            {
                bool video = photos[i].EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
                string thumb = Media.Thumb(photos[i]);
                sb.Append("<figure class=\"ap-pic\" data-p=\"" + Ui.Esc(photos[i]) + "\" data-t=\"" + Ui.Esc(thumb) + "\" data-kind=\"" + (video ? "video" : "photo") + "\">" + (video ? "<video src=\"" + Ui.Esc(photos[i]) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><i class=\"ap-pi-play\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"currentColor\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : "<img src=\"" + Ui.Esc(thumb) + "\" alt=\"\">") + (i == 0 ? "<figcaption>Main</figcaption>" : string.Empty));
                if (edit)
                {
                    sb.Append("<div class=\"ap-pic-b\"><button type=\"button\" aria-label=\"Move left\" onclick=\"AdminProductsJs.shift(" + i + ",-1)\"" + (i == 0 ? " disabled" : string.Empty) + ">&lsaquo;</button><button type=\"button\" aria-label=\"Move right\" onclick=\"AdminProductsJs.shift(" + i + ",1)\"" + (i == photos.Count - 1 ? " disabled" : string.Empty) + ">&rsaquo;</button><button type=\"button\" aria-label=\"Remove\" onclick=\"AdminProductsJs.unpick(" + i + ")\">&times;</button></div>");
                }
                sb.Append("</figure>");
            }
            if (photos.Count == 0)
            {
                sb.Append("<span class=\"ap-pic-none\">Nothing chosen yet.</span>");
            }
            return sb.ToString();
        }

        private static string ParseSizes(string? json, Bouquet? old, List<BouquetSize> into)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
                int i = 0;
                foreach (JsonElement row in doc.RootElement.EnumerateArray().Take(6))
                {
                    string label = Ui.Clip(row.TryGetProperty("l", out JsonElement l) ? l.GetString() : string.Empty, 60);
                    string price = (row.TryGetProperty("p", out JsonElement p) ? p.GetString() : string.Empty) ?? string.Empty;
                    if (label == string.Empty && price == string.Empty)
                    {
                        i++;
                        continue;
                    }
                    if (label == string.Empty || !decimal.TryParse(price, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) || value <= 0 || value > 9999)
                    {
                        return "Each size needs a name and a price above zero.";
                    }
                    string key = old != null && i < old.Sizes.Count ? old.Sizes[i].Key : Ui.Slug(label.Split('(')[0]);
                    string unique = key.Length == 0 ? "size" : key;
                    int n = 2;
                    while (into.Any(s => s.Key == unique))
                    {
                        unique = key + n++;
                    }
                    into.Add(new BouquetSize { Key = unique, Label = label, Price = Pricing.Round(value) });
                    i++;
                }
            }
            catch (JsonException)
            {
                return "The sizes could not be read.";
            }
            return into.Count == 0 ? "Add at least one size with a price." : string.Empty;
        }

        private static string ListHtml(SiteData site, Ctx c, string term, string st, string occ, int pg)
        {
            IEnumerable<Bouquet> q = site.Bouquets.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase);
            if (st == "live")
            {
                q = q.Where(b => b.Published);
            }
            else if (st == "hidden")
            {
                q = q.Where(b => !b.Published);
            }
            else if (st == "low")
            {
                q = q.Where(b => b.Stock <= site.LowStock);
            }
            if (occ != string.Empty)
            {
                q = q.Where(b => b.Occasions.Contains(occ));
            }
            if (term != string.Empty)
            {
                q = q.Where(b => Ui.Has(b.Title, term) || Ui.Has(b.Blurb, term) || Ui.Has(b.Colors, term));
            }
            List<Bouquet> rows = q.ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ap-empty\">" + (site.Bouquets.Count == 0 ? "No bouquets yet." : "No bouquets match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            pg = Math.Min(pg, pages);
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool extra = Access.Can(c.U, AreaKey, 'X');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-wrapt\"><table class=\"ap-table\"><thead><tr><th>Bouquet</th><th>Occasions</th><th class=\"ap-num\">From</th><th class=\"ap-num\">Stock</th><th>Status</th><th></th></tr></thead><tbody>");
            foreach (Bouquet b in rows.Skip((pg - 1) * Size).Take(Size))
            {
                string occText = string.Join(", ", b.Occasions.Select(k => site.Occasions.FirstOrDefault(o => o.Key == k)?.Label ?? k));
                string stock = b.Stock <= 0 ? "<span class=\"ap-pill ap-pill-bad\">Sold out</span>" : b.Stock <= site.LowStock ? "<span class=\"ap-pill ap-pill-wait\">" + b.Stock + " left</span>" : b.Stock.ToString();
                string key = Ui.Js(b.Key);
                string title = Ui.Js(b.Title);
                sb.Append("<tr><td><div class=\"ap-cellt\"><img class=\"ap-thumb\" src=\"" + Ui.Esc(Media.Thumb(Ui.Photo(b))) + "\" alt=\"\" loading=\"lazy\"><span><b>" + Ui.Esc(b.Title) + "</b><small>" + Ui.Esc(b.Blurb) + "</small></span></div></td>");
                sb.Append("<td>" + Ui.Esc(occText) + "</td><td class=\"ap-num\">" + Ui.Money(Ui.FromPrice(b)) + "</td><td class=\"ap-num\">" + stock + "</td>");
                sb.Append("<td><span class=\"ap-pill " + (b.Published ? "ap-pill-go" : "ap-pill-off") + "\">" + (b.Published ? "Published" : "Hidden") + "</span>" + (b.Featured ? " <span class=\"ap-pill\">Featured</span>" : string.Empty) + "</td><td><div class=\"ap-rowacts\">");
                sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" onclick=\"AdminProductsJs.edit('" + key + "')\">" + (edit ? "Edit" : "View") + "</button>");
                if (extra)
                {
                    sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" onclick=\"AdminProductsJs.publish('" + key + "','" + (b.Published ? "0" : "1") + "','" + title + "')\">" + (b.Published ? "Hide" : "Publish") + "</button>");
                    sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-danger ap-btn-sm\" onclick=\"AdminProductsJs.remove('" + key + "','" + title + "')\">Delete</button>");
                }
                sb.Append("</div></td></tr>");
            }
            sb.Append("</tbody></table></div><div class=\"ap-pager\"><span>Page " + pg + " of " + pages + " &middot; " + Ui.Count(rows.Count, "bouquet", "bouquets") + "</span><div>");
            sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\"" + (pg <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminProductsJs.list(" + (pg - 1) + ")\">Previous</button>");
            sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\"" + (pg >= pages ? " disabled" : string.Empty) + " onclick=\"AdminProductsJs.list(" + (pg + 1) + ")\">Next</button></div></div>");
            return sb.ToString();
        }

        private static string FormHtml(SiteData site, Ctx c, Bouquet? b)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool extra = Access.Can(c.U, AreaKey, 'X');
            string dis = edit ? string.Empty : " disabled";
            List<string> photos = b?.Photos ?? new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<form class=\"ap-stack\" id=\"ap-form\" onsubmit=\"return AdminProductsJs.save('" + (b == null ? string.Empty : Ui.Js(b.Key)) + "')\" novalidate>");
            sb.Append("<label class=\"ap-field\" for=\"ap-f-title\"><span>Name</span><input type=\"text\" id=\"ap-f-title\" data-k=\"title\" maxlength=\"60\" value=\"" + Ui.Esc(b?.Title) + "\"" + dis + "><em class=\"ap-err\" id=\"ap-e-title\"></em></label>");
            sb.Append("<label class=\"ap-field\" for=\"ap-f-blurb\"><span>Short line, shown on cards</span><input type=\"text\" id=\"ap-f-blurb\" data-k=\"blurb\" maxlength=\"100\" value=\"" + Ui.Esc(b?.Blurb) + "\"" + dis + "><em class=\"ap-err\" id=\"ap-e-blurb\"></em></label>");
            sb.Append("<label class=\"ap-field\" for=\"ap-f-text\"><span>Description</span><textarea id=\"ap-f-text\" data-k=\"text\" rows=\"4\" maxlength=\"600\"" + dis + ">" + Ui.Esc(b?.Text) + "</textarea><em class=\"ap-err\" id=\"ap-e-text\"></em></label>");
            sb.Append("<label class=\"ap-field\" for=\"ap-f-colors\"><span>Colours, used by search</span><input type=\"text\" id=\"ap-f-colors\" data-k=\"colors\" maxlength=\"80\" placeholder=\"pink, blush, rose\" value=\"" + Ui.Esc(b?.Colors) + "\"" + dis + "></label>");
            sb.Append("<div class=\"ap-field\"><span>Photos and videos</span><div class=\"ap-photos\" id=\"ap-photos\">" + PhotoStrip(photos, edit) + "</div><input type=\"hidden\" id=\"ap-f-photos\" data-k=\"photos\" value=\"" + Ui.Esc(string.Join("|", photos)) + "\">");
            if (edit)
            {
                sb.Append("<div><button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" onclick=\"AdminProductsJs.pickPhotos()\">Choose photos or videos</button></div><small>From the media library, or upload new ones there. The first item must be a photo; it is shown on cards and in the cart. Up to " + MaxPhotos + ".</small>");
            }
            sb.Append("<em class=\"ap-err\" id=\"ap-e-photos\"></em></div>");
            sb.Append("<fieldset class=\"ap-fieldset\"><legend>Occasions</legend><div class=\"ap-checks\">");
            foreach (Occasion o in site.Occasions)
            {
                sb.Append("<label class=\"ap-check\"><input type=\"checkbox\" name=\"ap-occ\" value=\"" + Ui.Esc(o.Key) + "\"" + (b != null && b.Occasions.Contains(o.Key) ? " checked" : string.Empty) + dis + ">" + Ui.Esc(o.Label) + "</label>");
            }
            sb.Append("</div></fieldset>");
            sb.Append("<fieldset class=\"ap-fieldset\"><legend>Sizes and prices</legend><div class=\"ap-sizes\">");
            for (int i = 0; i < 4; i++)
            {
                BouquetSize? s = b != null && i < b.Sizes.Count ? b.Sizes[i] : null;
                sb.Append("<div class=\"ap-sizerow\"><input type=\"text\" maxlength=\"60\" placeholder=\"" + (i == 0 ? "Small (about 12 stems)" : "Size name") + "\" aria-label=\"Size " + (i + 1) + " name\" value=\"" + Ui.Esc(s?.Label) + "\"" + dis + "><input type=\"text\" inputmode=\"decimal\" maxlength=\"8\" placeholder=\"Price\" aria-label=\"Size " + (i + 1) + " price\" value=\"" + (s == null ? string.Empty : s.Price.ToString("0.##", CultureInfo.InvariantCulture)) + "\"" + dis + "></div>");
            }
            sb.Append("</div><em class=\"ap-err\" id=\"ap-e-sizes\"></em></fieldset>");
            sb.Append("<div class=\"ap-grid2\"><label class=\"ap-field\" for=\"ap-f-stock\"><span>Stock available</span><input type=\"number\" id=\"ap-f-stock\" data-k=\"stock\" min=\"0\" max=\"999\" value=\"" + (b == null ? 10 : b.Stock) + "\"" + dis + "><small>Goes down with every order and back up when one is cancelled.</small><em class=\"ap-err\" id=\"ap-e-stock\"></em></label>");
            sb.Append("<div class=\"ap-stack\"><label class=\"ap-check\"><input type=\"checkbox\" data-k=\"featured\"" + (b != null && b.Featured ? " checked" : string.Empty) + dis + ">Show on the home page</label>");
            sb.Append("<label class=\"ap-check\"><input type=\"checkbox\" data-k=\"published\"" + (b != null && b.Published ? " checked" : string.Empty) + (extra ? string.Empty : " disabled") + ">Published in the shop</label>");
            sb.Append(extra ? string.Empty : "<small class=\"ap-hint\">Only people who can publish may change this.</small>");
            sb.Append("</div></div>");
            sb.Append("<div class=\"ap-acts\">" + (edit ? "<button type=\"submit\" class=\"ap-btn\">Save bouquet</button>" : string.Empty) + "<button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminProductsJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button></div></form>");
            return sb.ToString();
        }
    }
}
