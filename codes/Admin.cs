using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class Admin : WebPage
    {
                public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Dashboard | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"db-panel\"><h1>No access</h1><p class=\"db-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-brand", Ui.Esc(site.Brand));
            response.SetElementContents("db-menu", await Auth.Menu("db", "Admin", c));
            response.SetElementContents("db-who", Auth.Who("db", c));
        }

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
            bool seeOrders = Access.Can(c.U, "orders", 'V');
            bool seeShip = Access.Can(c.U, "shipping", 'V');
            bool seeProducts = Access.Can(c.U, "products", 'V');
            bool seeContent = Access.Can(c.U, "messages", 'V');
            List<OrderRec> orders = await Orders.LoadFresh();
            List<MessageRec> messages = await Store.LoadMessages();
            string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"db-ph\"><div><h1>Hello, " + Ui.Esc(c.U.Name.Split(' ')[0]) + "</h1><p>" + DateTime.Now.ToString("dddd, MMMM d", CultureInfo.InvariantCulture) + "</p></div></div>");
            sb.Append("<div class=\"db-tiles\">");
            if (seeOrders)
            {
                int fresh = orders.Count(o => o.Status == "new");
                decimal sales = orders.Where(o => o.PayStatus == "paid" && o.Created > DateTime.UtcNow.AddDays(-7)).Sum(o => o.Total);
                sb.Append(Tile("AdminOrders", "New orders", fresh.ToString(), fresh > 0));
                sb.Append(Tile("AdminOrders", "Waiting for payment", orders.Count(o => o.Status == "awaiting").ToString(), false));
                sb.Append(Tile("AdminOrders", "Paid sales, last 7 days", Ui.Money(sales), false));
            }
            if (seeShip)
            {
                int due = orders.Count(o => (o.Status == "new" || o.Status == "preparing" || o.Status == "out" || o.Status == "ready") && o.Date == today);
                int late = orders.Count(o => (o.Status == "new" || o.Status == "preparing" || o.Status == "out" || o.Status == "ready") && string.CompareOrdinal(o.Date, today) < 0);
                sb.Append(Tile("AdminShipping", "To deliver or hand over today", due.ToString(), due > 0));
                if (late > 0)
                {
                    sb.Append(Tile("AdminShipping", "Overdue", late.ToString(), true));
                }
            }
            List<Bouquet> low = site.Bouquets.Where(b => b.Published && b.Stock <= site.LowStock).OrderBy(b => b.Stock).ToList();
            if (seeProducts)
            {
                sb.Append(Tile("AdminProducts", "Running low", low.Count.ToString(), low.Count > 0));
            }
            int unread = messages.Count(m => !m.Read);
            if (seeContent)
            {
                sb.Append(Tile("AdminMessages", "Unread messages", unread.ToString(), unread > 0));
            }
            sb.Append("</div>");
            sb.Append("<div class=\"db-cols\">");
            if (seeOrders)
            {
                List<OrderRec> fresh = orders.Where(o => o.Status == "new").OrderBy(o => o.Created).Take(6).ToList();
                sb.Append("<div class=\"db-panel\"><h2>New orders</h2>");
                if (fresh.Count == 0)
                {
                    sb.Append("<p class=\"db-hint\">No new orders right now.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"db-list\">");
                    foreach (OrderRec o in fresh)
                    {
                        sb.Append("<li><span><b>" + Ui.Esc(o.No) + "</b> " + Ui.Esc(o.Name) + "<small>" + (o.Pickup ? "Pickup " : "Delivery ") + Ui.Esc(Ui.Day(o.Date)) + ", " + Ui.Esc(o.SlotLabel) + "</small></span><b>" + Ui.Money(o.Total) + "</b></li>");
                    }
                    sb.Append("</ul>");
                }
                sb.Append("<div class=\"db-acts\"><a class=\"db-btn db-btn-ghost\" href=\"AdminOrders\">All orders</a></div></div>");
            }
            if (seeProducts)
            {
                sb.Append("<div class=\"db-panel\"><h2>Running low</h2>");
                if (low.Count == 0)
                {
                    sb.Append("<p class=\"db-hint\">Every bouquet is well stocked.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"db-list\">");
                    foreach (Bouquet b in low.Take(8))
                    {
                        sb.Append("<li><span>" + Ui.Esc(b.Title) + "<small>" + (b.Stock <= 0 ? "Sold out" : Ui.Count(b.Stock, "bouquet left", "bouquets left")) + "</small></span></li>");
                    }
                    sb.Append("</ul>");
                }
                sb.Append("<div class=\"db-acts\"><a class=\"db-btn db-btn-ghost\" href=\"AdminProducts\">Manage bouquets</a></div></div>");
            }
            sb.Append("</div>");
            if (!seeOrders && !seeShip && !seeProducts && !seeContent)
            {
                sb.Append("<div class=\"db-panel\"><p class=\"db-hint\">Use the menu to open the areas you work in.</p></div>");
            }
            response.SetElementContents("db-main", sb.ToString());
            return response;
        }

        private static string Tile(string page, string label, string value, bool hot)
        {
            return "<a class=\"db-tile" + (hot ? " db-tile-hot" : string.Empty) + "\" href=\"" + page + "\"><small>" + Ui.Esc(label) + "</small><b>" + Ui.Esc(value) + "</b></a>";
        }
    }
}
