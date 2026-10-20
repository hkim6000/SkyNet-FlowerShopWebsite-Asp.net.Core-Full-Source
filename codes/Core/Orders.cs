using System.Security.Cryptography;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public static class Orders
    {
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static readonly string[] Open = { "awaiting", "new", "preparing", "out", "ready" };

        public static string NewCode(int length)
        {
            char[] c = new char[length];
            for (int i = 0; i < length; i++)
            {
                c[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
            }
            return new string(c);
        }

        public static async Task<List<OrderRec>> LoadFresh()
        {
            List<OrderRec> orders = await Store.LoadOrders();
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-45);
            if (!orders.Any(o => o.Status == "awaiting" && o.Created < cutoff))
            {
                return orders;
            }
            using (await Store.Hold())
            {
                orders = await Store.LoadOrders();
                SiteData site = await Store.LoadSite();
                foreach (OrderRec o in orders.Where(x => x.Status == "awaiting" && x.Created < cutoff))
                {
                    Cancel(site, o, "system", "Payment was not completed in time.");
                }
                await Store.SaveOrders(orders);
                await Store.SaveSite(site);
            }
            return orders;
        }

        public static async Task<(OrderRec? order, string error)> Create(OrderRec draft, List<CartIn> cart, string postcode)
        {
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                PriceQuote q = Pricing.Price(site, cart, draft.Pickup, postcode);
                if (!q.Ok)
                {
                    return (null, "Some items in your cart changed. Please review the cart and try again.");
                }
                if (!draft.Pickup && q.ZoneName == string.Empty)
                {
                    return (null, "We do not deliver to that postcode yet.");
                }
                List<OrderRec> orders = await Store.LoadOrders();
                string no;
                do
                {
                    no = "PB-" + NewCode(6);
                }
                while (orders.Any(o => o.No == no));
                draft.No = no;
                draft.Token = Access.NewToken();
                draft.Created = DateTime.UtcNow;
                draft.Lines = q.Lines;
                draft.Subtotal = q.Subtotal;
                draft.Fee = q.Fee;
                draft.Tax = q.Tax;
                draft.Total = q.Total;
                draft.ZoneName = q.ZoneName;
                draft.SlotLabel = site.Slots.FirstOrDefault(s => s.Key == draft.Slot)?.Label ?? draft.Slot;
                draft.Status = draft.PayMethod == "card" ? "awaiting" : "new";
                draft.PayStatus = "unpaid";
                draft.Notes.Add(new OrderNote { When = DateTime.UtcNow, By = "customer", Text = draft.PayMethod == "card" ? "Order placed, waiting for card payment." : "Order placed, pay on " + (draft.Pickup ? "pickup" : "delivery") + "." });
                foreach (OrderLine l in q.Lines)
                {
                    Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == l.Key);
                    if (b != null)
                    {
                        b.Stock = Math.Max(0, b.Stock - l.Qty);
                    }
                }
                orders.Add(draft);
                await Store.SaveOrders(orders);
                await Store.SaveSite(site);
                return (draft, string.Empty);
            }
        }

        public static void Cancel(SiteData site, OrderRec o, string by, string why)
        {
            if (o.Status == "cancelled" || o.Status == "delivered")
            {
                return;
            }
            foreach (OrderLine l in o.Lines)
            {
                Bouquet? b = site.Bouquets.FirstOrDefault(x => x.Key == l.Key);
                if (b != null)
                {
                    b.Stock += l.Qty;
                }
            }
            o.Status = "cancelled";
            o.Notes.Add(new OrderNote { When = DateTime.UtcNow, By = by, Text = "Cancelled." + (why == string.Empty ? string.Empty : " " + why) + (o.PayStatus == "paid" ? " Paid by card: refund needed." : string.Empty) });
        }

        public static string Apply(SiteData site, OrderRec o, string act, string arg, string by)
        {
            bool live = o.Status != "cancelled" && o.Status != "delivered";
            switch (act)
            {
                case "prepare":
                    if (o.Status != "new")
                    {
                        return "Only new orders can be started.";
                    }
                    o.Status = "preparing";
                    Note(o, by, "Started arranging.");
                    return string.Empty;
                case "ready":
                    if (!o.Pickup || (o.Status != "new" && o.Status != "preparing"))
                    {
                        return "This order cannot be marked ready.";
                    }
                    o.Status = "ready";
                    Note(o, by, "Ready for pickup.");
                    return string.Empty;
                case "dispatch":
                    if (o.Pickup || (o.Status != "new" && o.Status != "preparing"))
                    {
                        return "This order cannot be sent out.";
                    }
                    if (o.Courier == string.Empty)
                    {
                        return "Choose a courier first.";
                    }
                    o.Status = "out";
                    Note(o, by, "Out for delivery with " + o.Courier + ".");
                    return string.Empty;
                case "deliver":
                    if (o.Status != "out" && o.Status != "ready")
                    {
                        return "This order is not out for delivery or ready yet.";
                    }
                    o.Status = "delivered";
                    if (o.PayStatus == "unpaid" && o.PayMethod == "later")
                    {
                        o.PayStatus = "paid";
                    }
                    Note(o, by, o.Pickup ? "Picked up." : "Delivered.");
                    return string.Empty;
                case "cancel":
                    if (!live)
                    {
                        return "This order is already closed.";
                    }
                    Cancel(site, o, by, Ui.Clip(arg, 200));
                    return string.Empty;
                case "paid":
                    if (o.PayMethod != "later" || o.PayStatus != "unpaid" || o.Status == "cancelled")
                    {
                        return "Payment cannot be marked on this order.";
                    }
                    o.PayStatus = "paid";
                    Note(o, by, "Payment received.");
                    return string.Empty;
                case "refund":
                    if (o.PayStatus != "paid")
                    {
                        return "Only paid orders can be refunded.";
                    }
                    o.PayStatus = "refunded";
                    Note(o, by, "Refunded " + Ui.Money(o.Total) + ".");
                    return string.Empty;
                case "assign":
                    if (o.Pickup || !live)
                    {
                        return "A courier cannot be set on this order.";
                    }
                    o.Courier = Ui.Clip(arg, 60);
                    return string.Empty;
                case "note":
                    string text = Ui.Clip(arg, 300);
                    if (text.Length < 2)
                    {
                        return "Please write a note.";
                    }
                    o.Notes.Add(new OrderNote { When = DateTime.UtcNow, By = by, Text = text, Internal = true });
                    return string.Empty;
                default:
                    return "Unknown action.";
            }
        }

        public static void Note(OrderRec o, string by, string text)
        {
            o.Notes.Add(new OrderNote { When = DateTime.UtcNow, By = by, Text = text });
        }

        public static void Paid(OrderRec o, string intent)
        {
            if (o.PayStatus == "paid" || o.PayStatus == "refunded")
            {
                return;
            }
            o.PayStatus = "paid";
            o.Intent = intent;
            if (o.Status == "awaiting")
            {
                o.Status = "new";
            }
            Note(o, "stripe", "Card payment received.");
        }
    }
}
