using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminShipping : WebPage
    {
        private const string AreaKey = "shipping";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Shipping | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminShippingJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminShippingJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"sp-panel\"><h1>No access</h1><p class=\"sp-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("sp-brand", Ui.Esc(site.Brand));
            response.SetElementContents("sp-menu", await Auth.Menu("sp", "AdminShipping", c));
            response.SetElementContents("sp-who", Auth.Who("sp", c));
        }

        private static readonly string[] Open = { "new", "preparing", "out", "ready" };

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
                response.SetElementContents("sp-main", NoAccess("shipping"));
                return response;
            }
            string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sp-ph\"><div><h1>Shipping</h1><p>Deliveries and pickups by day and time slot.</p></div></div>");
            sb.Append("<div class=\"sp-daybar\"><button type=\"button\" class=\"sp-btn sp-btn-ghost\" onclick=\"AdminShippingJs.shift(-1)\" aria-label=\"Previous day\">&lsaquo;</button><input type=\"date\" id=\"sp-day\" value=\"" + today + "\" onchange=\"AdminShippingJs.board()\" aria-label=\"Day\"><button type=\"button\" class=\"sp-btn sp-btn-ghost\" onclick=\"AdminShippingJs.shift(1)\" aria-label=\"Next day\">&rsaquo;</button><button type=\"button\" class=\"sp-btn sp-btn-ghost\" onclick=\"AdminShippingJs.today()\">Today</button>");
            sb.Append("<select id=\"sp-way\" onchange=\"AdminShippingJs.board()\" aria-label=\"Type\"><option value=\"\">Deliveries and pickups</option><option value=\"delivery\">Deliveries only</option><option value=\"pickup\">Pickups only</option></select></div>");
            sb.Append("<div id=\"sp-board\">" + BoardHtml(await Orders.LoadFresh(), site, c, today, string.Empty) + "</div>");
            response.SetElementContents("sp-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Board()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "view shipping", string.Empty, "You do not have access to shipping.");
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("sp-board", BoardHtml(await Orders.LoadFresh(), site, c, DayOf(GetDataValue("day")), Ui.Pick(GetDataValue("way"), new[] { "delivery", "pickup" })));
            return response;
        }

        public async Task<ApiResponse> Assign()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string no = Ui.Clip(GetDataValue("no"), 12).ToUpperInvariant();
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "assign courier", no, "You can look at shipping but not change it.");
            }
            SiteData site = await Store.LoadSite();
            string courier = Ui.Clip(GetDataValue("courier"), 60);
            string track = Ui.Clip(GetDataValue("track"), 80);
            if (courier != string.Empty && !site.Couriers.Contains(courier))
            {
                response.ExecuteScript("AdminShippingJs.toast('Choose a courier from the list.');");
                return response;
            }
            string error;
            using (await Store.Hold())
            {
                List<OrderRec> orders = await Store.LoadOrders();
                OrderRec? o = orders.FirstOrDefault(x => x.No == no);
                if (o == null)
                {
                    response.ExecuteScript("AdminShippingJs.toast('That order was not found.');");
                    return response;
                }
                error = Orders.Apply(site, o, "assign", courier, c.U.Name);
                if (error == string.Empty)
                {
                    o.Tracking = track;
                    await Store.SaveOrders(orders);
                }
            }
            if (error != string.Empty)
            {
                response.ExecuteScript("AdminShippingJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "assign courier", no + " " + courier, true);
            response.ExecuteScript("AdminShippingJs.toast('Courier saved');");
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
            string no = Ui.Clip(GetDataValue("no"), 12).ToUpperInvariant();
            string act = Ui.Pick(GetDataValue("act"), new[] { "dispatch", "ready", "deliver" });
            if (act == string.Empty || !Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, act == string.Empty ? "unknown" : act, no, "You are not allowed to send out or deliver orders.");
            }
            string error;
            List<OrderRec> orders;
            SiteData site = await Store.LoadSite();
            using (await Store.Hold())
            {
                orders = await Store.LoadOrders();
                OrderRec? o = orders.FirstOrDefault(x => x.No == no);
                if (o == null)
                {
                    response.ExecuteScript("AdminShippingJs.toast('That order was not found.');");
                    return response;
                }
                error = Orders.Apply(site, o, act, string.Empty, c.U.Name);
                if (error == string.Empty)
                {
                    await Store.SaveOrders(orders);
                }
            }
            if (error != string.Empty)
            {
                response.ExecuteScript("AdminShippingJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "order " + act, no, true);
            response.SetElementContents("sp-board", BoardHtml(orders, site, c, DayOf(GetDataValue("day")), Ui.Pick(GetDataValue("way"), new[] { "delivery", "pickup" })));
            response.SetElementContents("sp-menu", await Auth.Menu("sp", "AdminShipping", c));
            response.ExecuteScript("AdminShippingJs.toast('Order updated');");
            return response;
        }

        private static string DayOf(string? value)
        {
            string v = (value ?? string.Empty).Trim();
            return DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string BoardHtml(List<OrderRec> all, SiteData site, Ctx c, string day, string way)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            IEnumerable<OrderRec> scope = all;
            if (way == "delivery")
            {
                scope = scope.Where(o => !o.Pickup);
            }
            else if (way == "pickup")
            {
                scope = scope.Where(o => o.Pickup);
            }
            List<OrderRec> list = scope.Where(o => o.Date == day && o.Status != "cancelled" && o.Status != "awaiting").ToList();
            int waiting = scope.Count(o => o.Date == day && o.Status == "awaiting");
            StringBuilder sb = new StringBuilder();
            int dl = list.Count(o => !o.Pickup);
            sb.Append("<div class=\"sp-sum\">" + Ui.Esc(Ui.Day(day)) + ": " + Ui.Count(dl, "delivery", "deliveries") + ", " + Ui.Count(list.Count - dl, "pickup", "pickups") + (waiting > 0 ? ". " + Ui.Count(waiting, "order is", "orders are") + " waiting for payment and not shown." : string.Empty) + "</div>");
            List<OrderRec> late = day == today ? scope.Where(o => Open.Contains(o.Status) && string.CompareOrdinal(o.Date, today) < 0).OrderBy(o => o.Date).ToList() : new List<OrderRec>();
            if (late.Count > 0)
            {
                sb.Append("<section class=\"sp-sect sp-late\"><h2>Overdue <span>" + late.Count + "</span></h2><div class=\"sp-cards\">");
                foreach (OrderRec o in late)
                {
                    sb.Append(CardHtml(o, site, c, true));
                }
                sb.Append("</div></section>");
            }
            List<string> keys = site.Slots.Select(s => s.Key).ToList();
            foreach (string k in list.Select(o => o.Slot).Distinct().Where(k => !keys.Contains(k)))
            {
                keys.Add(k);
            }
            foreach (string k in keys)
            {
                List<OrderRec> group = list.Where(o => o.Slot == k).ToList();
                if (group.Count == 0)
                {
                    continue;
                }
                string label = site.Slots.FirstOrDefault(s => s.Key == k)?.Label ?? group[0].SlotLabel;
                sb.Append("<section class=\"sp-sect\"><h2>" + Ui.Esc(label) + " <span>" + group.Count + "</span></h2><div class=\"sp-cards\">");
                foreach (OrderRec o in group)
                {
                    sb.Append(CardHtml(o, site, c, false));
                }
                sb.Append("</div></section>");
            }
            if (list.Count == 0 && late.Count == 0)
            {
                sb.Append("<div class=\"sp-empty\">Nothing scheduled for this day.</div>");
            }
            return sb.ToString();
        }

        private static string CardHtml(OrderRec o, SiteData site, Ctx c, bool showDay)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool extra = Access.Can(c.U, AreaKey, 'X');
            string pillCls = o.Status == "new" ? " sp-pill-new" : o.Status == "delivered" ? string.Empty : " sp-pill-go";
            StringBuilder sb = new StringBuilder();
            sb.Append("<article class=\"sp-card\"><div class=\"sp-card-h\"><b>" + Ui.Esc(o.No) + "</b><span class=\"sp-pill" + pillCls + "\">" + Ui.Esc(Ui.StatusLabel(o.Status)) + "</span></div>");
            sb.Append("<p><b>" + Ui.Esc(o.Recipient) + "</b>" + (o.RecipientPhone == string.Empty ? string.Empty : " &middot; " + Ui.Esc(o.RecipientPhone)) + (showDay ? "<small> &middot; was due " + Ui.Esc(Ui.Day(o.Date)) + "</small>" : string.Empty) + "</p>");
            sb.Append("<p>" + (o.Pickup ? "Pickup at the studio" : Ui.Esc(o.Street) + ", " + Ui.Esc(o.City) + " " + Ui.Esc(o.Postcode) + "<small> &middot; " + Ui.Esc(o.ZoneName) + "</small>") + "</p>");
            sb.Append("<p><small>" + Ui.Esc(string.Join("; ", o.Lines.Select(l => l.Qty + " x " + l.Title + " (" + l.SizeLabel.Split('(')[0].Trim() + ")" + (l.Extras.Count > 0 ? " + " + string.Join(", ", l.Extras) : string.Empty)))) + "</small></p>");
            if (o.CardText != string.Empty)
            {
                sb.Append("<div class=\"sp-quote\">&ldquo;" + Ui.Esc(o.CardText) + "&rdquo;</div>");
            }
            if (o.Instructions != string.Empty)
            {
                sb.Append("<p><small>Note: " + Ui.Esc(o.Instructions) + "</small></p>");
            }
            if (o.PayStatus == "unpaid")
            {
                sb.Append("<p><small><b>Collect " + Ui.Money(o.Total) + "</b> on " + (o.Pickup ? "pickup" : "delivery") + "</small></p>");
            }
            bool closed = o.Status == "delivered";
            if (!o.Pickup && !closed)
            {
                StringBuilder opts = new StringBuilder("<option value=\"\">No courier</option>");
                foreach (string name in site.Couriers)
                {
                    opts.Append("<option" + (name == o.Courier ? " selected" : string.Empty) + ">" + Ui.Esc(name) + "</option>");
                }
                string dis = edit ? string.Empty : " disabled";
                sb.Append("<div class=\"sp-ship\"><select id=\"sp-c-" + Ui.Esc(o.No) + "\" aria-label=\"Courier\"" + dis + " onchange=\"AdminShippingJs.assign('" + Ui.Js(o.No) + "')\">" + opts + "</select>");
                sb.Append("<input type=\"text\" id=\"sp-t-" + Ui.Esc(o.No) + "\" value=\"" + Ui.Esc(o.Tracking) + "\" placeholder=\"Tracking or note\" maxlength=\"80\" aria-label=\"Tracking\"" + dis + " onchange=\"AdminShippingJs.assign('" + Ui.Js(o.No) + "')\"></div>");
            }
            if (extra)
            {
                sb.Append("<div class=\"sp-acts\">");
                if (!o.Pickup && (o.Status == "new" || o.Status == "preparing"))
                {
                    sb.Append("<button type=\"button\" class=\"sp-btn sp-btn-sm\" onclick=\"AdminShippingJs.mark('" + Ui.Js(o.No) + "','dispatch')\">Send out</button>");
                }
                if (o.Pickup && (o.Status == "new" || o.Status == "preparing"))
                {
                    sb.Append("<button type=\"button\" class=\"sp-btn sp-btn-sm\" onclick=\"AdminShippingJs.mark('" + Ui.Js(o.No) + "','ready')\">Ready for pickup</button>");
                }
                if (o.Status == "out" || o.Status == "ready")
                {
                    sb.Append("<button type=\"button\" class=\"sp-btn sp-btn-sm\" onclick=\"AdminShippingJs.mark('" + Ui.Js(o.No) + "','deliver')\">" + (o.Pickup ? "Handed over" : "Mark delivered") + "</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</article>");
            return sb.ToString();
        }
    }
}
