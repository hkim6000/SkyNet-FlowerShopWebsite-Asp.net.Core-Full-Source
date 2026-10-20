using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class Cart : WebPage
    {
        private static readonly string[] Ways = { "delivery", "pickup" };

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            PayConf conf = await Store.LoadPay();
            HtmlDoc.SetTitle("Your cart | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            DateTime now = DateTime.Now;
            StringBuilder dates = new StringBuilder();
            foreach (DateTime d in Pricing.OpenDates(site, now))
            {
                string label = d.Date == now.Date ? "Today, " : d.Date == now.Date.AddDays(1) ? "Tomorrow, " : string.Empty;
                dates.Append("<option value=\"" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\">" + label + d.ToString("ddd, MMM d", CultureInfo.InvariantCulture) + "</option>");
            }
            if (dates.Length == 0)
            {
                dates.Append("<option value=\"\">No dates available</option>");
            }
            StringBuilder slots = new StringBuilder();
            foreach (Slot s in site.Slots)
            {
                slots.Append("<option value=\"" + Ui.Esc(s.Key) + "\">" + Ui.Esc(s.Label) + "</option>");
            }
            bool card = site.AcceptCard && Payments.Ready(conf);
            StringBuilder pay = new StringBuilder();
            bool first = true;
            if (card)
            {
                pay.Append("<label class=\"ct-mode\"><input type=\"radio\" name=\"ct-pay\" value=\"card\" checked><span><b>Pay now by card</b><small>Secure checkout on our payment partner's page</small></span></label>");
                first = false;
            }
            if (site.AcceptLater)
            {
                pay.Append("<label class=\"ct-mode\"><input type=\"radio\" name=\"ct-pay\" value=\"later\"" + (first ? " checked" : string.Empty) + "><span><b>Pay on delivery or pickup</b><small>Cash or card when your flowers arrive</small></span></label>");
            }
            if (pay.Length == 0)
            {
                pay.Append("<p>Online ordering is paused. Please call " + Ui.Esc(site.Phone) + " to order.</p>");
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_foot}", Ui.Foot("ct", site))
                .Replace("{plhd_pickup}", "Collect at " + Ui.Esc(site.Address))
                .Replace("{plhd_dates}", dates.ToString())
                .Replace("{plhd_slots}", slots.ToString())
                .Replace("{plhd_pay}", pay.ToString())
                .Replace("{plhd_fine}", "Prices are recalculated by our shop when you place the order. We never see or store your card number.");
        }

        public async Task<ApiResponse> Price()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            List<CartIn> cart = Pricing.Parse(GetDataValue("cart"));
            bool pickup = Ui.Pick(GetDataValue("way"), Ways) == "pickup";
            PriceQuote q = Pricing.Price(site, cart, pickup, Ui.Clip(GetDataValue("zipcode"), 10));
            response.SetElementContents("ct-lines", LinesHtml(q));
            response.SetElementContents("ct-totals", TotalsHtml(q, pickup));
            response.ExecuteScript("CartJs.priced(" + (q.Ok && (pickup || q.ZoneName != string.Empty) ? "true" : "false") + "," + cart.Count + ");");
            return response;
        }

        public async Task<ApiResponse> Place()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            PayConf conf = await Store.LoadPay();
            List<CartIn> cart = Pricing.Parse(GetDataValue("cart"));
            bool pickup = Ui.Pick(GetDataValue("way"), Ways) == "pickup";
            string name = Ui.Clip(GetDataValue("name"), 80);
            string email = Ui.Clip(GetDataValue("email"), 100);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string rname = Ui.Clip(GetDataValue("rname"), 80);
            string rphone = Ui.Clip(GetDataValue("rphone"), 30);
            string street = Ui.Clip(GetDataValue("street"), 120);
            string city = Ui.Clip(GetDataValue("city"), 80);
            string zip = Ui.Clip(GetDataValue("zipcode"), 10);
            string day = Ui.Clip(GetDataValue("day"), 12);
            string slot = Ui.Pick(GetDataValue("slot"), site.Slots.Select(s => s.Key));
            string card = Ui.Clip(GetDataValue("card"), 200);
            string instr = Ui.Clip(GetDataValue("instr"), 200);
            bool cardOk = site.AcceptCard && Payments.Ready(conf);
            string pay = Ui.Pick(GetDataValue("pay"), new[] { "card", "later" });
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 || name.Length > 60 ? "Please enter your name." : string.Empty,
                ["email"] = !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty,
                ["phone"] = PhoneDigits(phone) < 7 || PhoneDigits(phone) > 15 ? "Please enter a phone number we can reach." : string.Empty,
                ["rname"] = !pickup && (rname.Length < 2 || rname.Length > 60) ? "Please enter who the flowers are for." : string.Empty,
                ["rphone"] = rphone.Length > 0 && (PhoneDigits(rphone) < 7 || PhoneDigits(rphone) > 15) ? "That phone number does not look right." : string.Empty,
                ["street"] = !pickup && street.Length < 5 ? "Please enter the street address." : string.Empty,
                ["city"] = !pickup && city.Length < 2 ? "Please enter the city." : string.Empty,
                ["zip"] = !pickup && Pricing.ZoneFor(site, zip) == null ? "We do not deliver to this postcode yet. Choose pickup, or call us." : string.Empty,
                ["date"] = Pricing.DateError(site, day, slot, DateTime.Now),
                ["slot"] = Pricing.SlotError(site, slot),
                ["pay"] = pay == string.Empty || (pay == "card" && !cardOk) || (pay == "later" && !site.AcceptLater) ? "Please choose how you will pay." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ct-e-" + kv.Key, kv.Value);
            }
            string cardErr = card.Length > 200 ? "Please keep the card message under 200 characters." : string.Empty;
            response.SetElementContents("ct-e-msg", cardErr);
            if (cart.Count == 0)
            {
                response.SetElementContents("ct-e-form", "Your cart is empty.");
                return response;
            }
            if (e.Values.Any(v => v != string.Empty) || cardErr != string.Empty)
            {
                response.SetElementContents("ct-e-form", "Please check the highlighted fields.");
                return response;
            }
            OrderRec draft = new OrderRec
            {
                Name = name,
                Email = email,
                Phone = phone,
                Recipient = pickup ? name : rname,
                RecipientPhone = pickup ? phone : rphone,
                Pickup = pickup,
                Street = pickup ? string.Empty : street,
                City = pickup ? string.Empty : city,
                Postcode = pickup ? string.Empty : zip.ToUpperInvariant(),
                Date = day,
                Slot = slot,
                CardText = card,
                Instructions = instr,
                PayMethod = pay
            };
            (OrderRec? order, string error) = await Orders.Create(draft, cart, zip);
            if (order == null)
            {
                response.SetElementContents("ct-e-form", Ui.Esc(error));
                response.ExecuteScript("CartJs.reprice();");
                return response;
            }
            response.SetElementContents("ct-e-form", string.Empty);
            response.ExecuteScript("CartJs.placed('Pay?o=" + order.No + "&k=" + order.Token + "');");
            return response;
        }

        public async Task<ApiResponse> Suggest()
        {
            ApiResponse response = new ApiResponse();
            string term = Ui.Clip(GetDataValue("typed"), 40);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ct-sugg", Ui.Suggest("ct", site, term));
            response.ExecuteScript("CartJs.openSugg();");
            return response;
        }

        private static int PhoneDigits(string v)
        {
            return v.Count(char.IsDigit);
        }

        private static string LinesHtml(PriceQuote q)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < q.Lines.Count; i++)
            {
                OrderLine l = q.Lines[i];
                string detail = l.SizeLabel + (l.Extras.Count > 0 ? " + " + string.Join(", ", l.Extras) : string.Empty);
                sb.Append("<div class=\"ct-line\"><img src=\"" + (l.Image == string.Empty ? "images/hero.webp" : Ui.Esc(l.Image)) + "\" alt=\"\">");
                sb.Append("<div class=\"ct-line-t\"><b>" + Ui.Esc(l.Title) + "</b><small>" + Ui.Esc(detail) + "</small>" + (l.Problem == string.Empty ? string.Empty : "<em class=\"ct-problem\">" + Ui.Esc(l.Problem) + "</em>") + "</div>");
                sb.Append("<div class=\"ct-qty\"><button type=\"button\" aria-label=\"Fewer\" onclick=\"CartJs.step(" + i + ",-1)\">&minus;</button><span>" + l.Qty + "</span><button type=\"button\" aria-label=\"More\" onclick=\"CartJs.step(" + i + ",1)\">+</button></div>");
                sb.Append("<b class=\"ct-lt\">" + (l.Total > 0 ? Ui.Money(l.Total) : string.Empty) + "</b>");
                sb.Append("<span class=\"ct-x\"><button type=\"button\" class=\"ct-drop\" aria-label=\"Remove " + Ui.Esc(l.Title) + "\" onclick=\"CartJs.drop(" + i + ")\">&times;</button></span></div>");
            }
            return sb.ToString();
        }

        private static string TotalsHtml(PriceQuote q, bool pickup)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div><span>Items</span><b>" + Ui.Money(q.Subtotal) + "</b></div>");
            if (pickup)
            {
                sb.Append("<div><span>Pickup</span><b>Free</b></div>");
            }
            else
            {
                sb.Append("<div><span>Delivery<small>" + Ui.Esc(q.FeeNote) + "</small></span><b>" + (q.ZoneName == string.Empty ? "-" : q.Fee == 0 ? "Free" : Ui.Money(q.Fee)) + "</b></div>");
            }
            if (q.Tax > 0)
            {
                sb.Append("<div><span>Tax</span><b>" + Ui.Money(q.Tax) + "</b></div>");
            }
            sb.Append("<div class=\"ct-grand\"><span>Total</span><b>" + Ui.Money(q.Total) + "</b></div>");
            return sb.ToString();
        }
    }
}
