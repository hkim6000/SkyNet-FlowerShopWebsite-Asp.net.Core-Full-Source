using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminOrders : WebPage
    {
        private const string AreaKey = "orders";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Orders | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOrdersJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminOrdersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ao-panel\"><h1>No access</h1><p class=\"ao-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ao-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ao-menu", await Auth.Menu("ao", "AdminOrders", c));
            response.SetElementContents("ao-who", Auth.Who("ao", c));
        }

        private const int Size = 12;
        private static readonly string[] Acts = { "prepare", "paid", "cancel", "refund", "note" };

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
                response.SetElementContents("ao-main", NoAccess("orders"));
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ao-ph\"><div><h1>Orders</h1><p>Every order placed on the website.</p></div></div>");
            sb.Append("<div class=\"ao-tools\"><input type=\"search\" id=\"ao-term\" placeholder=\"Search number, name or email\" maxlength=\"40\" oninput=\"AdminOrdersJs.type()\" aria-label=\"Search orders\">");
            sb.Append("<select id=\"ao-st\" onchange=\"AdminOrdersJs.list(1)\" aria-label=\"Status\"><option value=\"\">All statuses</option><option value=\"open\">Open orders</option><option value=\"awaiting\">Waiting for payment</option><option value=\"new\">New</option><option value=\"preparing\">Being arranged</option><option value=\"out\">Out for delivery</option><option value=\"ready\">Ready for pickup</option><option value=\"delivered\">Delivered</option><option value=\"cancelled\">Cancelled</option></select>");
            sb.Append("<select id=\"ao-py\" onchange=\"AdminOrdersJs.list(1)\" aria-label=\"Payment\"><option value=\"\">Any payment</option><option value=\"paid\">Paid</option><option value=\"unpaid\">Not paid yet</option><option value=\"refunded\">Refunded</option></select></div>");
            sb.Append("<div id=\"ao-list\">" + ListHtml(await Orders.LoadFresh(), string.Empty, string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("ao-main", sb.ToString());
            return response;
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
                return await Refuse(response, c, "list orders", string.Empty, "You do not have access to orders.");
            }
            response.SetElementContents("ao-list", ListHtml(await Orders.LoadFresh(), Ui.Clip(GetDataValue("term"), 40), Ui.Clip(GetDataValue("st"), 12), Ui.Clip(GetDataValue("py"), 12), Ui.Bound(GetDataValue("pg"), 1, 100000, 1)));
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open order", string.Empty, "You do not have access to orders.");
            }
            string no = Ui.Clip(GetDataValue("no"), 12).ToUpperInvariant();
            OrderRec? o = (await Orders.LoadFresh()).FirstOrDefault(x => x.No == no);
            if (o == null)
            {
                response.ExecuteScript("AdminOrdersJs.toast('That order was not found.');");
                return response;
            }
            response.SetElementContents("ao-mbody", DetailHtml(c, o));
            response.ExecuteScript("AdminOrdersJs.opened('" + Ui.Js(o.No) + "');");
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string no = Ui.Clip(GetDataValue("no"), 12).ToUpperInvariant();
            string act = Ui.Pick(GetDataValue("act"), Acts);
            string why = Ui.Clip(GetDataValue("why"), 300);
            char need = act == "cancel" || act == "refund" ? 'X' : 'E';
            if (act == string.Empty || !Access.Can(c.U, AreaKey, need))
            {
                return await Refuse(response, c, act == string.Empty ? "unknown" : act, no, need == 'X' ? "You are not allowed to cancel or refund orders." : "You can look at orders but not change them.");
            }
            if (act == "refund")
            {
                OrderRec? before = (await Orders.LoadFresh()).FirstOrDefault(x => x.No == no);
                if (before == null || before.PayStatus != "paid")
                {
                    response.ExecuteScript("AdminOrdersJs.toast('Only paid orders can be refunded.');");
                    return response;
                }
                if (before.PayMethod == "card")
                {
                    string refundError = await Payments.Refund(await Store.LoadPay(), before.Intent);
                    if (refundError != string.Empty)
                    {
                        response.ExecuteScript("AdminOrdersJs.toast('" + Ui.Js(refundError) + "');");
                        return response;
                    }
                }
            }
            string error;
            OrderRec? o;
            List<OrderRec> orders;
            using (await Store.Hold())
            {
                orders = await Store.LoadOrders();
                SiteData site = await Store.LoadSite();
                o = orders.FirstOrDefault(x => x.No == no);
                if (o == null)
                {
                    response.ExecuteScript("AdminOrdersJs.toast('That order was not found.');");
                    return response;
                }
                error = Orders.Apply(site, o, act, why, c.U.Name);
                if (error == string.Empty)
                {
                    await Store.SaveOrders(orders);
                    await Store.SaveSite(site);
                }
            }
            if (error != string.Empty)
            {
                response.ExecuteScript("AdminOrdersJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "order " + act, no + " " + o.Name, true);
            response.SetElementContents("ao-mbody", DetailHtml(c, o));
            response.SetElementContents("ao-list", ListHtml(orders, Ui.Clip(GetDataValue("term"), 40), Ui.Clip(GetDataValue("st"), 12), Ui.Clip(GetDataValue("py"), 12), Ui.Bound(GetDataValue("pg"), 1, 100000, 1)));
            response.SetElementContents("ao-menu", await Auth.Menu("ao", "AdminOrders", c));
            response.ExecuteScript("AdminOrdersJs.done('" + (act == "note" ? "Note added" : "Order updated") + "');");
            return response;
        }

        private static string Pill(string status)
        {
            string cls = status == "new" ? " ao-pill-new" : status == "awaiting" ? " ao-pill-wait" : status == "cancelled" ? " ao-pill-off" : status == "delivered" ? string.Empty : " ao-pill-go";
            return "<span class=\"ao-pill" + cls + "\">" + Ui.Esc(Ui.StatusLabel(status)) + "</span>";
        }

        private static string PayPill(OrderRec o)
        {
            string cls = o.PayStatus == "paid" ? " ao-pill-go" : o.PayStatus == "refunded" ? " ao-pill-off" : " ao-pill-wait";
            return "<span class=\"ao-pill" + cls + "\">" + Ui.Esc(Ui.PayLabel(o)) + "</span>";
        }

        private static string ListHtml(List<OrderRec> all, string term, string st, string py, int pg)
        {
            IEnumerable<OrderRec> q = all.OrderByDescending(o => o.Created);
            if (st == "open")
            {
                q = q.Where(o => Orders.Open.Contains(o.Status));
            }
            else if (st != string.Empty)
            {
                q = q.Where(o => o.Status == st);
            }
            if (py != string.Empty)
            {
                q = q.Where(o => o.PayStatus == py);
            }
            if (term != string.Empty)
            {
                q = q.Where(o => Ui.Has(o.No, term) || Ui.Has(o.Name, term) || Ui.Has(o.Email, term) || Ui.Has(o.Recipient, term));
            }
            List<OrderRec> rows = q.ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ao-empty\">" + (all.Count == 0 ? "No orders yet. They will appear here as customers check out." : "No orders match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            pg = Math.Min(pg, pages);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ao-wrapt\"><table class=\"ao-table\"><thead><tr><th>Order</th><th>Customer</th><th>For</th><th class=\"ao-num\">Total</th><th>Status</th><th>Payment</th></tr></thead><tbody>");
            foreach (OrderRec o in rows.Skip((pg - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"ao-row\" onclick=\"AdminOrdersJs.open('" + Ui.Js(o.No) + "')\"><td><button type=\"button\" class=\"ao-link\">" + Ui.Esc(o.No) + "</button><small>" + Ui.Esc(Ui.When(o.Created)) + "</small></td>");
                sb.Append("<td>" + Ui.Esc(o.Name) + "<small>" + Ui.Esc(o.Email) + "</small></td>");
                sb.Append("<td>" + Ui.Esc(Ui.Day(o.Date)) + "<small>" + (o.Pickup ? "Pickup" : "Delivery") + ", " + Ui.Esc(o.SlotLabel) + "</small></td>");
                sb.Append("<td class=\"ao-num\">" + Ui.Money(o.Total) + "</td><td>" + Pill(o.Status) + "</td><td>" + PayPill(o) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append("<div class=\"ao-pager\"><span>Page " + pg + " of " + pages + " &middot; " + Ui.Count(rows.Count, "order", "orders") + "</span><div>");
            sb.Append("<button type=\"button\" class=\"ao-btn ao-btn-ghost ao-btn-sm\"" + (pg <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminOrdersJs.list(" + (pg - 1) + ")\">Previous</button>");
            sb.Append("<button type=\"button\" class=\"ao-btn ao-btn-ghost ao-btn-sm\"" + (pg >= pages ? " disabled" : string.Empty) + " onclick=\"AdminOrdersJs.list(" + (pg + 1) + ")\">Next</button></div></div>");
            return sb.ToString();
        }

        private static string DetailHtml(Ctx c, OrderRec o)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ao-top2\">" + Pill(o.Status) + PayPill(o) + "<small>Placed " + Ui.Esc(Ui.When(o.Created)) + "</small></div>");
            sb.Append("<div class=\"ao-two\"><div><h3>Customer</h3><dl class=\"ao-kv\"><dt>Name</dt><dd>" + Ui.Esc(o.Name) + "</dd><dt>Email</dt><dd><a href=\"mailto:" + Ui.Esc(o.Email) + "\">" + Ui.Esc(o.Email) + "</a></dd><dt>Phone</dt><dd>" + Ui.Esc(o.Phone) + "</dd></dl></div>");
            sb.Append("<div><h3>" + (o.Pickup ? "Pickup" : "Delivery") + "</h3><dl class=\"ao-kv\"><dt>When</dt><dd>" + Ui.Esc(Ui.Day(o.Date)) + ", " + Ui.Esc(o.SlotLabel) + "</dd>");
            if (!o.Pickup)
            {
                sb.Append("<dt>Recipient</dt><dd>" + Ui.Esc(o.Recipient) + (o.RecipientPhone == string.Empty ? string.Empty : ", " + Ui.Esc(o.RecipientPhone)) + "</dd><dt>Address</dt><dd>" + Ui.Esc(o.Street) + ", " + Ui.Esc(o.City) + " " + Ui.Esc(o.Postcode) + "</dd><dt>Zone</dt><dd>" + Ui.Esc(o.ZoneName) + "</dd>");
                sb.Append("<dt>Courier</dt><dd>" + (o.Courier == string.Empty ? "Not assigned" : Ui.Esc(o.Courier) + (o.Tracking == string.Empty ? string.Empty : ", " + Ui.Esc(o.Tracking))) + "</dd>");
            }
            if (o.CardText != string.Empty)
            {
                sb.Append("<dt>Card message</dt><dd>&ldquo;" + Ui.Esc(o.CardText) + "&rdquo;</dd>");
            }
            if (o.Instructions != string.Empty)
            {
                sb.Append("<dt>Notes</dt><dd>" + Ui.Esc(o.Instructions) + "</dd>");
            }
            sb.Append("</dl></div></div><h3>Items</h3><div class=\"ao-items\">");
            foreach (OrderLine l in o.Lines)
            {
                sb.Append("<div><span>" + l.Qty + " x " + Ui.Esc(l.Title) + "<small>" + Ui.Esc(l.SizeLabel) + (l.Extras.Count > 0 ? " + " + Ui.Esc(string.Join(", ", l.Extras)) : string.Empty) + "</small></span><b>" + Ui.Money(l.Total) + "</b></div>");
            }
            if (!o.Pickup)
            {
                sb.Append("<div><span>Delivery</span><b>" + (o.Fee == 0 ? "Free" : Ui.Money(o.Fee)) + "</b></div>");
            }
            if (o.Tax > 0)
            {
                sb.Append("<div><span>Tax</span><b>" + Ui.Money(o.Tax) + "</b></div>");
            }
            sb.Append("<div class=\"ao-tot\"><span>Total</span><b>" + Ui.Money(o.Total) + "</b></div></div>");
            sb.Append("<h3 class=\"ao-mt\">History</h3><ul class=\"ao-hist\">");
            foreach (OrderNote n in o.Notes.OrderByDescending(x => x.When))
            {
                sb.Append("<li" + (n.Internal ? " class=\"ao-inner\"" : string.Empty) + "><small>" + Ui.Esc(Ui.When(n.When)) + " &middot; " + Ui.Esc(n.By) + (n.Internal ? " &middot; internal note" : string.Empty) + "</small>" + Ui.Esc(n.Text) + "</li>");
            }
            sb.Append("</ul>");
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool extra = Access.Can(c.U, AreaKey, 'X');
            sb.Append("<div class=\"ao-acts\">");
            if (edit && o.Status == "new")
            {
                sb.Append("<button type=\"button\" class=\"ao-btn\" onclick=\"AdminOrdersJs.act('" + Ui.Js(o.No) + "','prepare')\">Start arranging</button>");
            }
            if (edit && o.PayMethod == "later" && o.PayStatus == "unpaid" && o.Status != "cancelled")
            {
                sb.Append("<button type=\"button\" class=\"ao-btn ao-btn-ghost\" onclick=\"AdminOrdersJs.act('" + Ui.Js(o.No) + "','paid')\">Mark as paid</button>");
            }
            if (extra && o.Status != "cancelled" && o.Status != "delivered")
            {
                sb.Append("<button type=\"button\" class=\"ao-btn ao-btn-danger\" onclick=\"AdminOrdersJs.act('" + Ui.Js(o.No) + "','cancel')\">Cancel order</button>");
            }
            if (extra && o.PayStatus == "paid")
            {
                sb.Append("<button type=\"button\" class=\"ao-btn ao-btn-danger\" onclick=\"AdminOrdersJs.act('" + Ui.Js(o.No) + "','refund')\">Refund payment</button>");
            }
            if (Access.Can(c.U, "shipping", 'V') && o.Status != "cancelled" && o.Status != "awaiting")
            {
                sb.Append("<a class=\"ao-btn ao-btn-ghost\" href=\"AdminShipping\">Open shipping board</a>");
            }
            sb.Append("</div>");
            if (edit)
            {
                sb.Append("<div class=\"ao-noteform\"><label for=\"ao-note\">Internal note, not shown to the customer</label><textarea id=\"ao-note\" maxlength=\"300\"></textarea><div><button type=\"button\" class=\"ao-btn ao-btn-ghost ao-btn-sm\" onclick=\"AdminOrdersJs.addNote('" + Ui.Js(o.No) + "')\">Add note</button></div></div>");
            }
            if (!edit && !extra)
            {
                sb.Append("<p class=\"ao-hint\">You have view-only access to orders.</p>");
            }
            return sb.ToString();
        }
    }
}
