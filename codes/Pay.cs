using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class Pay : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Your order | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string no = Ui.Clip(QueryValue("o"), 12).ToUpperInvariant();
            string tk = Ui.Clip(QueryValue("k"), 40);
            OrderRec? o = await Find(no, tk);
            if (o != null && QueryValue("back") == "1" && o.Status == "awaiting")
            {
                await Confirm(o.No, tk);
                o = await Find(no, tk);
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_foot}", Ui.Foot("py", site))
                .Replace("{plhd_order}", o == null ? MissingHtml() : OrderHtml(site, o));
        }

        public async Task<ApiResponse> Start()
        {
            ApiResponse response = new ApiResponse();
            string no = Ui.Clip(GetDataValue("no"), 12).ToUpperInvariant();
            string tk = Ui.Clip(GetDataValue("tk"), 40);
            OrderRec? o = await Find(no, tk);
            if (o == null || o.Status != "awaiting" || o.PayMethod != "card")
            {
                response.SetElementContents("py-msg", "This order is not waiting for a card payment.");
                response.ExecuteScript("PayJs.failed();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            PayConf conf = await Store.LoadPay();
            (string url, string id, string error) = await Payments.CreateSession(conf, site, o);
            if (error != string.Empty)
            {
                response.SetElementContents("py-msg", Ui.Esc(error));
                response.ExecuteScript("PayJs.failed();");
                return response;
            }
            using (await Store.Hold())
            {
                List<OrderRec> orders = await Store.LoadOrders();
                OrderRec? mine = orders.FirstOrDefault(x => x.No == o.No);
                if (mine != null)
                {
                    mine.Session = id;
                    await Store.SaveOrders(orders);
                }
            }
            response.ExecuteScript("PayJs.go('" + Ui.Js(url) + "');");
            return response;
        }

        public async Task<ApiResponse> Check()
        {
            ApiResponse response = new ApiResponse();
            string no = Ui.Clip(GetDataValue("no"), 12).ToUpperInvariant();
            string tk = Ui.Clip(GetDataValue("tk"), 40);
            string message = await Confirm(no, tk);
            if (message == string.Empty)
            {
                response.ExecuteScript("PayJs.reload();");
                return response;
            }
            response.SetElementContents("py-msg", Ui.Esc(message));
            return response;
        }

        public async Task<ApiResponse> Suggest()
        {
            ApiResponse response = new ApiResponse();
            string term = Ui.Clip(GetDataValue("typed"), 40);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("py-sugg", Ui.Suggest("py", site, term));
            response.ExecuteScript("PayJs.openSugg();");
            return response;
        }

        private static async Task<OrderRec?> Find(string no, string tk)
        {
            if (no.Length == 0 || tk.Length == 0)
            {
                return null;
            }
            List<OrderRec> orders = await Orders.LoadFresh();
            return orders.FirstOrDefault(x => x.No == no && x.Token == tk);
        }

        private static async Task<string> Confirm(string no, string tk)
        {
            OrderRec? o = await Find(no, tk);
            if (o == null || o.Status != "awaiting")
            {
                return o != null && o.PayStatus == "paid" ? string.Empty : "We could not confirm a payment for this order yet.";
            }
            PayConf conf = await Store.LoadPay();
            (bool paid, string intent, string error) = await Payments.Check(conf, o.Session);
            if (error != string.Empty)
            {
                return error;
            }
            if (!paid)
            {
                return "The payment has not come through yet. If you just paid, wait a moment and check again.";
            }
            using (await Store.Hold())
            {
                List<OrderRec> orders = await Store.LoadOrders();
                OrderRec? mine = orders.FirstOrDefault(x => x.No == o.No);
                if (mine != null)
                {
                    Orders.Paid(mine, intent);
                    await Store.SaveOrders(orders);
                }
            }
            return string.Empty;
        }

        private static string MissingHtml()
        {
            return "<div class=\"py-card\"><h1>We could not find that order</h1><p>The link may be incomplete. You can look up an order with its number and your email.</p><div class=\"py-act\"><a class=\"py-btn\" href=\"Track\">Track an order</a><a class=\"py-btn py-btn-ghost\" href=\"Shop\">Keep shopping</a></div></div>";
        }

        private static string OrderHtml(SiteData site, OrderRec o)
        {
            StringBuilder sb = new StringBuilder();
            bool wait = o.Status == "awaiting";
            bool bad = o.Status == "cancelled";
            string title = wait ? "Almost there" : bad ? "This order was cancelled" : "Thank you, " + Ui.Esc(o.Name.Split(' ')[0]);
            string line = wait ? "Complete your card payment to confirm the order. We hold your flowers for a short time." : bad ? "If you paid, we will refund you. Contact us if you have questions." : o.PayMethod == "later" && o.PayStatus == "unpaid" ? "Your order is confirmed. You pay when " + (o.Pickup ? "you collect it." : "it arrives.") : "Your order is confirmed. We will arrange it by hand and send it on its way.";
            sb.Append("<div class=\"py-card\" id=\"py-order\" data-no=\"" + Ui.Esc(o.No) + "\" data-tk=\"" + Ui.Esc(o.Token) + "\">");
            sb.Append("<span class=\"py-badge" + (wait ? " py-badge-wait" : bad ? " py-badge-bad" : string.Empty) + "\">" + Ui.Esc(Ui.StatusLabel(o.Status)) + "</span>");
            sb.Append("<h1>" + title + "</h1><p>" + line + "</p>");
            sb.Append("<div class=\"py-facts\"><div><small>Order number</small><b>" + Ui.Esc(o.No) + "</b></div>");
            sb.Append("<div><small>" + (o.Pickup ? "Pickup" : "Delivery") + "</small>" + Ui.Esc(Ui.Day(o.Date)) + ", " + Ui.Esc(o.SlotLabel) + "</div>");
            sb.Append("<div><small>" + (o.Pickup ? "Collect at" : "Deliver to") + "</small>" + (o.Pickup ? Ui.Esc(site.Address) : Ui.Esc(o.Recipient) + ", " + Ui.Esc(o.Street) + ", " + Ui.Esc(o.City) + " " + Ui.Esc(o.Postcode)) + "</div>");
            sb.Append("<div><small>Payment</small>" + Ui.Esc(Ui.PayLabel(o)) + "</div></div>");
            sb.Append("<div class=\"py-lines\">");
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
            sb.Append("<div class=\"py-total\"><span>Total</span><b>" + Ui.Money(o.Total) + "</b></div></div>");
            sb.Append("<div class=\"py-msg\" id=\"py-msg\" role=\"alert\"></div><div class=\"py-act\">");
            if (wait && o.PayMethod == "card")
            {
                sb.Append("<button type=\"button\" class=\"py-btn\" id=\"py-go\" onclick=\"PayJs.start()\">Pay " + Ui.Money(o.Total) + " now</button>");
                sb.Append("<button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"PayJs.check()\">I have paid, check again</button>");
            }
            else
            {
                sb.Append("<a class=\"py-btn\" href=\"Track\">Track this order</a><a class=\"py-btn py-btn-ghost\" href=\"Shop\">Keep shopping</a>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }
    }
}
