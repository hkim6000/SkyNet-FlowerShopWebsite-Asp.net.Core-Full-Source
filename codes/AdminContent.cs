using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminContent : WebPage
    {
        private const string AreaKey = "content";
        private const int MaxCustom = 30;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Content | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminContentJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminContentJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ac-panel\"><h1>No access</h1><p class=\"ac-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ac-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ac-menu", await Auth.Menu("ac", "AdminContent", c));
            response.SetElementContents("ac-who", Auth.Who("ac", c));
        }

        private static readonly string[] Tabs = { "home", "about", "faq", "occasions", "contact", "messages" };
        private static readonly string[] Builtin = { "hero", "promises", "favourites", "occasions", "delivery" };
        private static readonly string[] Custom = { "text", "photo", "phototext", "video" };

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
                response.SetElementContents("ac-main", NoAccess("content"));
                return response;
            }
            response.SetElementContents("ac-main", await PageHtml(c, site, "home"));
            response.ExecuteScript("AdminContentJs.shown('home');");
            return response;
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
                return await Refuse(response, c, "open content", string.Empty, "You do not have access to content.");
            }
            string tab = Ui.Pick(GetDataValue("tab"), Tabs);
            tab = tab == string.Empty ? "home" : tab;
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ac-main", await PageHtml(c, site, tab));
            response.ExecuteScript("AdminContentJs.shown('" + tab + "');");
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
            string tab = Ui.Pick(GetDataValue("tab"), Tabs);
            if (!Access.Can(c.U, AreaKey, 'E') || tab == "messages" || tab == "home" || tab == string.Empty)
            {
                return await Refuse(response, c, "save content", tab, "You can look at content but not change it.");
            }
            Dictionary<string, string> errors = new Dictionary<string, string>();
            List<MediaItem> media = await Media.Load();
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                switch (tab)
                {
                    case "about":
                        site.AboutTitle = Need("atitle", 80, 3, errors);
                        site.AboutLead = Need("alead", 240, 10, errors);
                        List<string> paras = (GetDataValue("paras") ?? string.Empty).Replace("\r", string.Empty).Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                        if (paras.Count == 0 || paras.Count > 6 || paras.Any(x => x.Length > 700))
                        {
                            errors["paras"] = "Write 1 to 6 paragraphs of up to 700 characters, with a blank line between them.";
                        }
                        else
                        {
                            site.AboutParagraphs = paras;
                        }
                        List<Promise> tips = new List<Promise>();
                        foreach (string line in (GetDataValue("tips") ?? string.Empty).Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] part = line.Split('|', 2);
                            if (part.Length == 2 && part[0].Trim().Length > 1 && part[1].Trim().Length > 4 && part[0].Trim().Length <= 40 && part[1].Trim().Length <= 160)
                            {
                                tips.Add(new Promise { Title = part[0].Trim(), Text = part[1].Trim() });
                            }
                            else if (line.Trim().Length > 0)
                            {
                                errors["tips"] = "Write each tip as: Title | text (title up to 40, text up to 160 characters).";
                            }
                        }
                        if (!errors.ContainsKey("tips"))
                        {
                            site.Care = tips.Take(6).ToList();
                        }
                        break;
                    case "faq":
                        List<Faq> faqs = new List<Faq>();
                        foreach (string line in (GetDataValue("faqs") ?? string.Empty).Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] part = line.Split('|', 2);
                            if (part.Length == 2 && part[0].Trim().Length > 4 && part[1].Trim().Length > 4 && part[0].Trim().Length <= 120 && part[1].Trim().Length <= 400)
                            {
                                faqs.Add(new Faq { Q = part[0].Trim(), A = part[1].Trim() });
                            }
                            else if (line.Trim().Length > 0)
                            {
                                errors["faqs"] = "Write each question as: Question | answer (question up to 120, answer up to 400 characters).";
                            }
                        }
                        if (!errors.ContainsKey("faqs"))
                        {
                            site.Faqs = faqs.Take(12).ToList();
                        }
                        break;
                    case "occasions":
                        for (int i = 0; i < site.Occasions.Count; i++)
                        {
                            site.Occasions[i].Label = Need("ol" + i, 30, 2, errors);
                            site.Occasions[i].Blurb = Need("ob" + i, 80, 3, errors);
                            string img = Ui.Clip(GetDataValue("oi" + i), 200);
                            if (Media.Find(media, img, "photo") == null)
                            {
                                errors["oi" + i] = "Choose a photo from the library.";
                            }
                            else
                            {
                                site.Occasions[i].Image = img;
                            }
                        }
                        break;
                    case "contact":
                        site.Brand = Need("brand", 40, 2, errors);
                        site.Tagline = Need("tagline", 100, 5, errors);
                        site.Phone = Need("phone", 30, 5, errors);
                        site.Email = Need("email", 80, 5, errors);
                        if (!errors.ContainsKey("email") && !Ui.IsEmail(site.Email))
                        {
                            errors["email"] = "Enter a valid email address.";
                        }
                        site.Address = Need("address", 120, 5, errors);
                        site.Hours = Need("hours", 120, 3, errors);
                        break;
                }
                foreach (string k in KeysFor(tab, site))
                {
                    response.SetElementContents("ac-e-" + k, errors.TryGetValue(k, out string? m) ? m : string.Empty);
                }
                if (errors.Count > 0)
                {
                    return response;
                }
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "edit content", tab, true);
            response.ExecuteScript("AdminContentJs.saved();");
            return response;
        }

        public async Task<ApiResponse> EditBlock()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            string type = Ui.Pick(GetDataValue("type"), Custom);
            if (!Access.Can(c.U, AreaKey, 'V') || (id == string.Empty && !Access.Can(c.U, AreaKey, 'E')))
            {
                return await Refuse(response, c, "open block", id, "You can look at the home page but not add blocks.");
            }
            SiteData site = await Store.LoadSite();
            Block? b = id == string.Empty ? null : site.Blocks.FirstOrDefault(x => x.Id == id);
            if (id == string.Empty && type == string.Empty || id != string.Empty && b == null)
            {
                response.ExecuteScript("AdminContentJs.toast('That block was not found.');");
                return response;
            }
            Block shown = b ?? new Block { Type = type, Side = "left" };
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("ac-mbody", BlockForm(site, media, shown, id, Access.Can(c.U, AreaKey, 'E')));
            response.ExecuteScript("AdminContentJs.opened('" + Ui.Js((b == null ? "New block: " : "Edit block: ") + Media.TypeName(shown.Type)) + "');");
            return response;
        }

        public async Task<ApiResponse> SaveBlock()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save block", id, "You can look at the home page but not change it.");
            }
            string newType = Ui.Pick(GetDataValue("type"), Custom);
            List<MediaItem> media = await Media.Load();
            Dictionary<string, string> errors = new Dictionary<string, string>();
            string label;
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Block? b = id == string.Empty ? null : site.Blocks.FirstOrDefault(x => x.Id == id);
                if (id != string.Empty && b == null || id == string.Empty && newType == string.Empty)
                {
                    response.ExecuteScript("AdminContentJs.toast('That block was not found.');");
                    return response;
                }
                if (b == null && site.Blocks.Count(x => Custom.Contains(x.Type)) >= MaxCustom)
                {
                    response.ExecuteScript("AdminContentJs.toast('The home page has " + MaxCustom + " added blocks already. Delete one first.');");
                    return response;
                }
                Block work = b ?? new Block { Id = "b" + Media.NewId().Substring(0, 8), Type = newType };
                switch (work.Type)
                {
                    case "hero":
                        site.Hero.Eyebrow = Need("eyebrow", 60, 0, errors);
                        site.Hero.Title = Need("title", 70, 3, errors);
                        site.Hero.Text = Need("text", 240, 10, errors);
                        site.Hero.Button = Need("button", 30, 2, errors);
                        site.Hero.Image = PathOf("media", "photo", true, media, errors, site.Hero.Image);
                        break;
                    case "promises":
                        for (int i = 0; i < 3; i++)
                        {
                            while (site.Promises.Count <= i)
                            {
                                site.Promises.Add(new Promise());
                            }
                            site.Promises[i].Title = Need("p" + i + "t", 40, 2, errors);
                            site.Promises[i].Text = Need("p" + i + "x", 140, 5, errors);
                        }
                        break;
                    case "favourites":
                    case "occasions":
                        work.Title = Need("title", 60, 2, errors);
                        break;
                    case "delivery":
                        work.Eyebrow = Need("eyebrow", 40, 0, errors);
                        work.Title = Need("title", 70, 2, errors);
                        break;
                    case "text":
                        work.Eyebrow = Need("eyebrow", 40, 0, errors);
                        work.Title = Need("title", 90, 0, errors);
                        work.Text = Need("text", 3000, 0, errors);
                        if (!errors.ContainsKey("title") && !errors.ContainsKey("text") && work.Title.Length == 0 && work.Text.Length == 0)
                        {
                            errors["text"] = "Write a heading or some text.";
                        }
                        break;
                    case "photo":
                        work.Media = PathOf("media", "photo", true, media, errors, work.Media);
                        work.Title = Need("title", 90, 0, errors);
                        work.Caption = Need("caption", 160, 0, errors);
                        break;
                    case "phototext":
                        work.Media = PathOf("media", "photo", true, media, errors, work.Media);
                        work.Eyebrow = Need("eyebrow", 40, 0, errors);
                        work.Title = Need("title", 90, 2, errors);
                        work.Text = Need("text", 2000, 0, errors);
                        work.Side = Ui.Pick(GetDataValue("side"), new[] { "left", "right" }) == "right" ? "right" : "left";
                        break;
                    case "video":
                        work.Media = PathOf("media", "video", true, media, errors, work.Media);
                        work.Poster = PathOf("poster", "photo", false, media, errors, work.Poster);
                        work.Title = Need("title", 90, 0, errors);
                        work.Caption = Need("caption", 160, 0, errors);
                        break;
                }
                foreach (string k in new[] { "eyebrow", "title", "text", "button", "media", "poster", "caption", "p0t", "p0x", "p1t", "p1x", "p2t", "p2x" })
                {
                    response.SetElementContents("ac-e-" + k, errors.TryGetValue(k, out string? m) ? m : string.Empty);
                }
                if (errors.Count > 0)
                {
                    return response;
                }
                if (b == null)
                {
                    site.Blocks.Add(work);
                }
                label = Label(site, work);
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, id == string.Empty ? "add home block" : "edit home block", label, true);
            response.SetElementContents("ac-blocks", BlocksHtml(site, Access.Can(c.U, AreaKey, 'E')));
            response.ExecuteScript("AdminContentJs.blockSaved();");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "move block", id, "You can look at the home page but not change it.");
            }
            int dir = GetDataValue("dir") == "up" ? -1 : 1;
            SiteData site;
            string label = string.Empty;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                int at = site.Blocks.FindIndex(x => x.Id == id);
                int to = at + dir;
                if (at >= 0 && to >= 0 && to < site.Blocks.Count)
                {
                    Block b = site.Blocks[at];
                    site.Blocks[at] = site.Blocks[to];
                    site.Blocks[to] = b;
                    label = Label(site, b);
                    await Store.SaveSite(site);
                }
            }
            if (label.Length > 0)
            {
                await Store.Log(c.U.Id, "move home block", label, true);
            }
            response.SetElementContents("ac-blocks", BlocksHtml(site, true));
            response.ExecuteScript("AdminContentJs.moved('" + Ui.Js(id) + "','" + (dir < 0 ? "up" : "down") + "');");
            return response;
        }

        public async Task<ApiResponse> Order()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "reorder blocks", string.Empty, "You can look at the home page but not change it.");
            }
            List<string> ids = (GetDataValue("ids") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => Ui.Clip(x, 20)).Distinct().ToList();
            SiteData site;
            bool changed = false;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                if (ids.Count == site.Blocks.Count && site.Blocks.All(b => ids.Contains(b.Id)))
                {
                    List<Block> sorted = ids.Select(i => site.Blocks.First(b => b.Id == i)).ToList();
                    changed = !sorted.SequenceEqual(site.Blocks);
                    site.Blocks = sorted;
                    if (changed)
                    {
                        await Store.SaveSite(site);
                    }
                }
            }
            if (changed)
            {
                await Store.Log(c.U.Id, "reorder home blocks", string.Empty, true);
            }
            response.SetElementContents("ac-blocks", BlocksHtml(site, true));
            response.ExecuteScript("AdminContentJs.toast('" + (changed ? "New order saved. The home page shows it now." : "The order did not change.") + "');");
            return response;
        }

        public async Task<ApiResponse> Toggle()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "show or hide block", id, "You can look at the home page but not change it.");
            }
            SiteData site;
            Block? b;
            string label = string.Empty;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                b = site.Blocks.FirstOrDefault(x => x.Id == id);
                if (b != null)
                {
                    b.Visible = GetDataValue("to") == "1";
                    label = Label(site, b);
                    await Store.SaveSite(site);
                }
            }
            if (b == null)
            {
                response.ExecuteScript("AdminContentJs.toast('That block was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, b.Visible ? "show home block" : "hide home block", label, true);
            response.SetElementContents("ac-blocks", BlocksHtml(site, true));
            response.ExecuteScript("AdminContentJs.toast('" + (b.Visible ? "The block is on the home page again." : "The block is hidden. It stays saved here.") + "');");
            return response;
        }

        public async Task<ApiResponse> DropBlock()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete block", id, "You can look at the home page but not change it.");
            }
            SiteData site;
            string label = string.Empty;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Block? b = site.Blocks.FirstOrDefault(x => x.Id == id);
                if (b != null && Custom.Contains(b.Type))
                {
                    label = Label(site, b);
                    site.Blocks.Remove(b);
                    await Store.SaveSite(site);
                }
            }
            if (label.Length == 0)
            {
                response.ExecuteScript("AdminContentJs.toast('Only blocks you added can be deleted. Hide the others instead.');");
                return response;
            }
            await Store.Log(c.U.Id, "delete home block", label, true);
            response.SetElementContents("ac-blocks", BlocksHtml(site, true));
            response.ExecuteScript("AdminContentJs.toast('Block deleted');");
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
                response.ExecuteScript("AdminContentJs.uploaded();");
                return await Refuse(response, c, "upload media", string.Empty, "You can look at content but not change it.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.SetElementContents("ac-pkmsg", "Choose one or more files first.");
                response.ExecuteScript("AdminContentJs.uploaded();");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            string kind = Kind(GetDataValue("pk"));
            response.SetElementContents("ac-pkgrid", PickGrid(media, kind == string.Empty ? "photo" : kind, Ui.Clip(GetDataValue("cur"), 200)));
            response.SetElementContents("ac-pkmsg", Ui.Esc(((added > 0 ? Ui.Count(added, "file", "files") + " added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()));
            response.ExecuteScript("AdminContentJs.uploaded();");
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
                return await Refuse(response, c, "choose media", string.Empty, "You can look at content but not change it.");
            }
            string kind = Kind(GetDataValue("pk"));
            kind = kind == string.Empty ? "photo" : kind;
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("ac-pkbody", PickHtml(site, media, kind, Ui.Clip(GetDataValue("cur"), 200)));
            response.ExecuteScript("AdminContentJs.picking('" + (kind == "video" ? "Choose a video" : "Choose a photo") + "');");
            return response;
        }

        public async Task<ApiResponse> Mark()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 12);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "mark message", id, "You can look at messages but not change them.");
            }
            using (await Store.Hold())
            {
                List<MessageRec> list = await Store.LoadMessages();
                MessageRec? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.Read = GetDataValue("read") == "1";
                    await Store.SaveMessages(list);
                }
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ac-main", await PageHtml(c, site, "messages"));
            response.SetElementContents("ac-menu", await Auth.Menu("ac", "AdminContent", c));
            response.ExecuteScript("AdminContentJs.shown('messages');");
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 12);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete message", id, "You can look at messages but not delete them.");
            }
            string who = string.Empty;
            using (await Store.Hold())
            {
                List<MessageRec> list = await Store.LoadMessages();
                MessageRec? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    who = m.Name;
                    list.Remove(m);
                    await Store.SaveMessages(list);
                }
            }
            await Store.Log(c.U.Id, "delete message", who, true);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ac-main", await PageHtml(c, site, "messages"));
            response.SetElementContents("ac-menu", await Auth.Menu("ac", "AdminContent", c));
            response.ExecuteScript("AdminContentJs.shown('messages');AdminContentJs.toast('Message deleted');");
            return response;
        }

        private string Need(string key, int max, int min, Dictionary<string, string> errors)
        {
            string v = (GetDataValue(key) ?? string.Empty).Trim();
            if (v.Length < min || v.Length > max)
            {
                errors[key] = min == 0 ? "Keep it under " + max + " characters." : "Enter " + min + " to " + max + " characters.";
            }
            return v;
        }

        private string PathOf(string key, string kind, bool required, List<MediaItem> media, Dictionary<string, string> errors, string old)
        {
            string v = Ui.Clip(GetDataValue(key), 200);
            if (v.Length == 0 && !required)
            {
                return string.Empty;
            }
            if (Media.Find(media, v, kind) == null)
            {
                errors[key] = kind == "video" ? "Choose a video from the library." : "Choose a photo from the library.";
                return old;
            }
            return v;
        }

        private static string Kind(string? v)
        {
            string k = (v ?? string.Empty).Trim();
            return k == "photo" || k == "video" ? k : string.Empty;
        }

        private static IEnumerable<string> KeysFor(string tab, SiteData site)
        {
            switch (tab)
            {
                case "about":
                    return new[] { "atitle", "alead", "paras", "tips" };
                case "faq":
                    return new[] { "faqs" };
                case "occasions":
                    return Enumerable.Range(0, site.Occasions.Count).SelectMany(i => new[] { "ol" + i, "ob" + i, "oi" + i });
                default:
                    return new[] { "brand", "tagline", "phone", "email", "address", "hours" };
            }
        }

        private static string Field(string key, string label, string value, int max, bool area, string hint = "", bool disabled = false)
        {
            string dis = disabled ? " disabled" : string.Empty;
            string control = area
                ? "<textarea id=\"ac-f-" + key + "\" data-k=\"" + key + "\" rows=\"" + (max > 400 ? 8 : 3) + "\" maxlength=\"" + max + "\"" + dis + ">" + Ui.Esc(value) + "</textarea>"
                : "<input type=\"text\" id=\"ac-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + dis + ">";
            return "<label class=\"ac-field\" for=\"ac-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + control + (hint == string.Empty ? string.Empty : "<small>" + Ui.Esc(hint) + "</small>") + "<em class=\"ac-err\" id=\"ac-e-" + key + "\"></em></label>";
        }

        private static string Preview(List<MediaItem> media, string path, string kind)
        {
            if (path.Length == 0)
            {
                return "<span class=\"ac-mf-none\">" + (kind == "video" ? "No video chosen" : "No photo chosen") + "</span>";
            }
            MediaItem? m = Media.Find(media, path, string.Empty);
            if (m != null && m.Kind == "video")
            {
                return "<video src=\"" + Ui.Esc(path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video>";
            }
            return "<img src=\"" + Ui.Esc(m != null && m.Thumb.Length > 0 ? m.Thumb : path) + "\" alt=\"\">";
        }

        private static string MediaField(List<MediaItem> media, string key, string label, string path, string kind, bool required, bool edit)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ac-field ac-mf\"><span>" + Ui.Esc(label) + "</span><div class=\"ac-mf-row\"><div class=\"ac-mf-prev\" id=\"ac-mfp-" + key + "\">" + Preview(media, path, kind) + "</div>");
            sb.Append("<input type=\"hidden\" id=\"ac-f-" + key + "\" data-k=\"" + key + "\" value=\"" + Ui.Esc(path) + "\">");
            if (edit)
            {
                sb.Append("<div class=\"ac-mf-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost ac-btn-sm\" onclick=\"AdminContentJs.pick('" + key + "','" + kind + "')\">" + (kind == "video" ? "Choose video" : "Choose photo") + "</button>");
                if (!required)
                {
                    sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-ghost ac-btn-sm\" onclick=\"AdminContentJs.unpick('" + key + "','" + kind + "')\">Remove</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div><em class=\"ac-err\" id=\"ac-e-" + key + "\"></em></div>");
            return sb.ToString();
        }

        private static string Label(SiteData site, Block b)
        {
            switch (b.Type)
            {
                case "hero":
                    return "Hero: " + site.Hero.Title;
                case "promises":
                    return "Promises";
                default:
                    return Media.TypeName(b.Type) + (b.Title.Length > 0 ? ": " + b.Title : string.Empty);
            }
        }

        private static string Summary(SiteData site, Block b)
        {
            string s;
            switch (b.Type)
            {
                case "hero":
                    s = site.Hero.Title;
                    break;
                case "promises":
                    s = string.Join(" · ", site.Promises.Select(p => p.Title));
                    break;
                case "favourites":
                    s = b.Title + " (bouquets marked Show on the home page)";
                    break;
                case "text":
                    s = b.Title.Length > 0 ? b.Title : b.Text;
                    break;
                case "photo":
                case "video":
                    s = b.Title.Length > 0 ? b.Title : b.Caption.Length > 0 ? b.Caption : Path.GetFileName(b.Media);
                    break;
                default:
                    s = b.Title;
                    break;
            }
            return s.Length > 90 ? s.Substring(0, 90) + "…" : s;
        }

        private static string BlocksHtml(SiteData site, bool edit)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < site.Blocks.Count; i++)
            {
                Block b = site.Blocks[i];
                string id = Ui.Js(b.Id);
                bool custom = Custom.Contains(b.Type);
                sb.Append("<li class=\"ac-blk" + (b.Visible ? string.Empty : " ac-blk-off") + "\" data-id=\"" + Ui.Esc(b.Id) + "\"" + (edit ? " draggable=\"true\"" : string.Empty) + ">");
                if (edit)
                {
                    sb.Append("<span class=\"ac-grip\" title=\"Drag to move\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></span>");
                }
                sb.Append("<span class=\"ac-blk-n\">" + (i + 1) + "</span><div class=\"ac-blk-t\"><b>" + Ui.Esc(Media.TypeName(b.Type)) + (b.Visible ? string.Empty : " <span class=\"ac-pill ac-pill-off\">Hidden</span>") + "</b><small>" + Ui.Esc(Summary(site, b)) + "</small></div><div class=\"ac-blk-a\">");
                if (edit)
                {
                    sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-ghost ac-btn-sm ac-sq\" id=\"ac-up-" + Ui.Esc(b.Id) + "\" aria-label=\"Move up\" title=\"Move up\" onclick=\"AdminContentJs.move('" + id + "','up')\"" + (i == 0 ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button>");
                    sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-ghost ac-btn-sm ac-sq\" id=\"ac-dn-" + Ui.Esc(b.Id) + "\" aria-label=\"Move down\" title=\"Move down\" onclick=\"AdminContentJs.move('" + id + "','down')\"" + (i == site.Blocks.Count - 1 ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button>");
                    sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-ghost ac-btn-sm\" onclick=\"AdminContentJs.toggle('" + id + "','" + (b.Visible ? "0" : "1") + "','" + Ui.Js(Media.TypeName(b.Type)) + "')\">" + (b.Visible ? "Hide" : "Show") + "</button>");
                }
                sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-ghost ac-btn-sm\" onclick=\"AdminContentJs.editBlock('" + id + "','')\">" + (edit ? "Edit" : "View") + "</button>");
                if (edit && custom)
                {
                    sb.Append("<button type=\"button\" class=\"ac-btn ac-btn-danger ac-btn-sm\" onclick=\"AdminContentJs.dropBlock('" + id + "','" + Ui.Js(Media.TypeName(b.Type)) + "')\">Delete</button>");
                }
                sb.Append("</div></li>");
            }
            return sb.ToString();
        }

        private static string BlockForm(SiteData site, List<MediaItem> media, Block b, string id, bool edit)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<form class=\"ac-stack\" id=\"ac-bform\" onsubmit=\"return AdminContentJs.saveBlock('" + Ui.Js(id) + "','" + Ui.Js(id == string.Empty ? b.Type : string.Empty) + "')\" novalidate>");
            bool d = !edit;
            switch (b.Type)
            {
                case "hero":
                    sb.Append(MediaField(media, "media", "Background photo", site.Hero.Image, "photo", true, edit));
                    sb.Append(Field("eyebrow", "Small line above the headline", site.Hero.Eyebrow, 60, false, string.Empty, d));
                    sb.Append(Field("title", "Headline", site.Hero.Title, 70, false, string.Empty, d));
                    sb.Append(Field("text", "Introduction", site.Hero.Text, 240, true, string.Empty, d));
                    sb.Append(Field("button", "Button label", site.Hero.Button, 30, false, "The button opens the shop.", d));
                    break;
                case "promises":
                    for (int i = 0; i < 3; i++)
                    {
                        Promise p = i < site.Promises.Count ? site.Promises[i] : new Promise();
                        sb.Append("<h3 class=\"ac-sub\">Promise " + (i + 1) + "</h3><div class=\"ac-rowtwo\">" + Field("p" + i + "t", "Title", p.Title, 40, false, string.Empty, d) + Field("p" + i + "x", "Text", p.Text, 140, false, string.Empty, d) + "</div>");
                    }
                    break;
                case "favourites":
                    sb.Append(Field("title", "Heading", b.Title, 60, false, "Bouquets marked Show on the home page appear here. Set that on the Bouquets page.", d));
                    break;
                case "occasions":
                    sb.Append(Field("title", "Heading", b.Title, 60, false, "Change the occasion names and photos on the Occasions tab.", d));
                    break;
                case "delivery":
                    sb.Append(Field("eyebrow", "Small line above the heading", b.Eyebrow, 40, false, string.Empty, d));
                    sb.Append(Field("title", "Heading", b.Title, 70, false, "Zones, fees and the cut-off time come from Settings.", d));
                    break;
                case "text":
                    sb.Append(Field("eyebrow", "Small line above the heading (optional)", b.Eyebrow, 40, false, string.Empty, d));
                    sb.Append(Field("title", "Heading", b.Title, 90, false, string.Empty, d));
                    sb.Append(Field("text", "Text", b.Text, 3000, true, "Leave a blank line between paragraphs.", d));
                    break;
                case "photo":
                    sb.Append(MediaField(media, "media", "Photo", b.Media, "photo", true, edit));
                    sb.Append(Field("title", "Heading (optional)", b.Title, 90, false, string.Empty, d));
                    sb.Append(Field("caption", "Caption (optional)", b.Caption, 160, false, string.Empty, d));
                    break;
                case "phototext":
                    sb.Append(MediaField(media, "media", "Photo", b.Media, "photo", true, edit));
                    sb.Append("<fieldset class=\"ac-fieldset\"><legend>Photo side</legend><div class=\"ac-radios\"><label class=\"ac-check\"><input type=\"radio\" name=\"ac-side\" value=\"left\"" + (b.Side != "right" ? " checked" : string.Empty) + (d ? " disabled" : string.Empty) + ">Photo on the left</label><label class=\"ac-check\"><input type=\"radio\" name=\"ac-side\" value=\"right\"" + (b.Side == "right" ? " checked" : string.Empty) + (d ? " disabled" : string.Empty) + ">Photo on the right</label></div></fieldset>");
                    sb.Append(Field("eyebrow", "Small line above the heading (optional)", b.Eyebrow, 40, false, string.Empty, d));
                    sb.Append(Field("title", "Heading", b.Title, 90, false, string.Empty, d));
                    sb.Append(Field("text", "Text", b.Text, 2000, true, "Leave a blank line between paragraphs.", d));
                    break;
                case "video":
                    sb.Append(MediaField(media, "media", "Video (MP4)", b.Media, "video", true, edit));
                    sb.Append(MediaField(media, "poster", "Cover photo (optional)", b.Poster, "photo", false, edit));
                    sb.Append(Field("title", "Heading (optional)", b.Title, 90, false, string.Empty, d));
                    sb.Append(Field("caption", "Caption (optional)", b.Caption, 160, false, string.Empty, d));
                    break;
            }
            sb.Append("<div class=\"ac-acts\">" + (edit ? "<button type=\"submit\" class=\"ac-btn\">" + (id == string.Empty ? "Add block" : "Save block") + "</button>" : string.Empty) + "<button type=\"button\" class=\"ac-btn ac-btn-ghost\" onclick=\"AdminContentJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button></div></form>");
            return sb.ToString();
        }

        private static string PickGrid(List<MediaItem> media, string kind, string current)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == kind).OrderByDescending(m => m.Added).Take(400).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ac-empty\">" + (kind == "video" ? "No videos yet. Upload one above." : "No photos yet. Upload one above.") + "</div>";
            }
            StringBuilder sb = new StringBuilder();
            foreach (MediaItem m in rows)
            {
                string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
                sb.Append("<button type=\"button\" class=\"ac-pi" + (m.Path == current ? " ac-pi-on" : string.Empty) + "\" data-p=\"" + Ui.Esc(m.Path) + "\" data-t=\"" + Ui.Esc(thumb) + "\" data-kind=\"" + m.Kind + "\" data-n=\"" + Ui.Esc((m.Name + " " + m.Alt).ToLowerInvariant()) + "\" onclick=\"AdminContentJs.choose(this)\" title=\"" + Ui.Esc(m.Name) + "\">");
                sb.Append(m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video>" : "<img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\">");
                sb.Append("<span>" + Ui.Esc(m.Name) + "</span></button>");
            }
            return sb.ToString();
        }

        private static string PickHtml(SiteData site, List<MediaItem> media, string kind, string current)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ac-pktools\"><input type=\"search\" id=\"ac-pkq\" placeholder=\"Search the library\" maxlength=\"40\" oninput=\"AdminContentJs.pkfilter()\" aria-label=\"Search the library\">");
            sb.Append("<label class=\"ac-btn ac-btn-ghost ac-upbtn\"><input type=\"file\" id=\"ac-pkfile\" multiple accept=\"" + (kind == "video" ? "video/mp4" : "image/jpeg,image/png,image/webp,image/gif") + "\" onchange=\"AdminContentJs.upload()\">Upload new</label></div>");
            sb.Append("<p class=\"ac-hint\">" + (kind == "video" ? "MP4 videos up to " + site.MaxVideoMb + " MB." : "JPG, PNG, WebP or GIF up to " + Media.PhotoMb + " MB.") + "</p><p class=\"ac-upmsg\" id=\"ac-pkmsg\" role=\"status\"></p>");
            sb.Append("<input type=\"hidden\" id=\"ac-pkkind\" value=\"" + kind + "\"><div class=\"ac-pkgrid\" id=\"ac-pkgrid\">" + PickGrid(media, kind, current) + "</div>");
            return sb.ToString();
        }

        private async Task<string> PageHtml(Ctx c, SiteData site, string tab)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            List<MessageRec> messages = await Store.LoadMessages();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ac-ph\"><div><h1>Content</h1><p>The words and pictures on the website. Changes show up straight away.</p></div></div><div class=\"ac-tabs\" id=\"ac-tabs\">");
            string[,] names = { { "home", "Home page" }, { "about", "About" }, { "faq", "Questions" }, { "occasions", "Occasions" }, { "contact", "Shop details" }, { "messages", "Messages" } };
            for (int i = 0; i < names.GetLength(0); i++)
            {
                int unread = names[i, 0] == "messages" ? messages.Count(m => !m.Read) : 0;
                sb.Append("<button type=\"button\" class=\"ac-tab\" data-t=\"" + names[i, 0] + "\" onclick=\"AdminContentJs.show('" + names[i, 0] + "')\">" + names[i, 1] + (unread > 0 ? "<i class=\"ac-badge\">" + unread + "</i>" : string.Empty) + "</button>");
            }
            sb.Append("</div>");
            if (tab == "messages")
            {
                sb.Append(MessagesHtml(messages, edit));
                return sb.ToString();
            }
            if (tab == "home")
            {
                sb.Append("<section class=\"ac-panel\"><div class=\"ac-blhead\"><div><h2>Home page blocks</h2><p class=\"ac-hint\">The home page shows these blocks from top to bottom." + (edit ? " Drag a block or use the arrows to move it. Hidden blocks stay saved." : string.Empty) + "</p></div>");
                if (edit)
                {
                    sb.Append("<div class=\"ac-addblk\"><select id=\"ac-addtype\" aria-label=\"Block type\"><option value=\"text\">Text</option><option value=\"photo\">Photo</option><option value=\"phototext\">Photo and text</option><option value=\"video\">Video</option></select><button type=\"button\" class=\"ac-btn\" onclick=\"AdminContentJs.addBlock()\">Add block</button></div>");
                }
                sb.Append("</div><ol class=\"ac-blocks\" id=\"ac-blocks\">" + BlocksHtml(site, edit) + "</ol></section>");
                return sb.ToString();
            }
            sb.Append("<form class=\"ac-panel ac-form\" id=\"ac-form\" onsubmit=\"return AdminContentJs.save()\" novalidate>");
            switch (tab)
            {
                case "about":
                    sb.Append(Field("atitle", "Heading", site.AboutTitle, 80, false));
                    sb.Append(Field("alead", "Opening line", site.AboutLead, 240, true));
                    sb.Append(Field("paras", "Story", string.Join("\n\n", site.AboutParagraphs), 2400, true, "Leave a blank line between paragraphs."));
                    sb.Append(Field("tips", "Flower care tips", string.Join("\n", site.Care.Select(t => t.Title + " | " + t.Text)), 1200, true, "One tip per line: Title | text"));
                    break;
                case "faq":
                    sb.Append(Field("faqs", "Questions and answers", string.Join("\n", site.Faqs.Select(f => f.Q + " | " + f.A)), 6000, true, "One per line: Question | answer. They appear on the Contact page."));
                    break;
                case "occasions":
                    {
                        List<MediaItem> media = await Media.Load();
                        for (int i = 0; i < site.Occasions.Count; i++)
                        {
                            sb.Append("<div class=\"ac-occrow\">" + MediaField(media, "oi" + i, "Photo", site.Occasions[i].Image, "photo", true, edit) + "<div class=\"ac-stack\">" + Field("ol" + i, "Occasion name", site.Occasions[i].Label, 30, false) + Field("ob" + i, "One-line description", site.Occasions[i].Blurb, 80, false) + "</div></div>");
                        }
                    }
                    break;
                default:
                    sb.Append(Field("brand", "Shop name", site.Brand, 40, false));
                    sb.Append(Field("tagline", "Tagline", site.Tagline, 100, false));
                    sb.Append("<div class=\"ac-rowtwo\">" + Field("phone", "Phone", site.Phone, 30, false) + Field("email", "Email", site.Email, 80, false) + "</div>");
                    sb.Append(Field("address", "Studio address", site.Address, 120, false));
                    sb.Append(Field("hours", "Opening hours", site.Hours, 120, false));
                    break;
            }
            sb.Append(edit ? "<div class=\"ac-acts\"><button type=\"submit\" class=\"ac-btn\">Save changes</button></div>" : "<p class=\"ac-hint\">You have view-only access to content.</p>");
            sb.Append("</form>");
            return sb.ToString();
        }

        private static string MessagesHtml(List<MessageRec> messages, bool edit)
        {
            if (messages.Count == 0)
            {
                return "<div class=\"ac-panel ac-empty\">No messages yet. They arrive from the Contact page.</div>";
            }
            string[] topics = { "order", "event", "delivery", "other" };
            string[] labels = { "An order", "A wedding or event", "Delivery", "Something else" };
            StringBuilder sb = new StringBuilder("<div class=\"ac-msgs\">");
            foreach (MessageRec m in messages.OrderByDescending(x => x.When).Take(100))
            {
                int t = Array.IndexOf(topics, m.Topic);
                sb.Append("<article class=\"ac-msg" + (m.Read ? string.Empty : " ac-unread") + "\"><div class=\"ac-msg-h\"><b>" + Ui.Esc(m.Name) + "</b><small>" + Ui.Esc(t >= 0 ? labels[t] : m.Topic) + " &middot; " + Ui.Esc(Ui.When(m.When)) + "</small></div>");
                sb.Append("<a href=\"mailto:" + Ui.Esc(m.Email) + "\">" + Ui.Esc(m.Email) + "</a><p>" + Ui.Esc(m.Text) + "</p>");
                if (edit)
                {
                    sb.Append("<div class=\"ac-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost ac-btn-sm\" onclick=\"AdminContentJs.mark('" + Ui.Js(m.Id) + "','" + (m.Read ? "0" : "1") + "')\">" + (m.Read ? "Mark unread" : "Mark read") + "</button><button type=\"button\" class=\"ac-btn ac-btn-danger ac-btn-sm\" onclick=\"AdminContentJs.drop('" + Ui.Js(m.Id) + "','" + Ui.Js(m.Name) + "')\">Delete</button></div>");
                }
                sb.Append("</article>");
            }
            return sb.Append("</div>").ToString();
        }
    }
}
