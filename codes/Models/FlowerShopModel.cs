namespace FlowerShop.Models
{
    public class SiteData
    {
        public string Brand { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Hours { get; set; } = string.Empty;
        public string PublicUrl { get; set; } = string.Empty;
        public HeroText Hero { get; set; } = new HeroText();
        public List<Promise> Promises { get; set; } = new List<Promise>();
        public string AboutTitle { get; set; } = string.Empty;
        public string AboutLead { get; set; } = string.Empty;
        public List<string> AboutParagraphs { get; set; } = new List<string>();
        public List<Promise> Care { get; set; } = new List<Promise>();
        public List<Occasion> Occasions { get; set; } = new List<Occasion>();
        public List<Bouquet> Bouquets { get; set; } = new List<Bouquet>();
        public List<Faq> Faqs { get; set; } = new List<Faq>();
        public List<Addon> Addons { get; set; } = new List<Addon>();
        public List<Zone> Zones { get; set; } = new List<Zone>();
        public List<Slot> Slots { get; set; } = new List<Slot>();
        public List<string> Couriers { get; set; } = new List<string>();
        public List<string> Blackout { get; set; } = new List<string>();
        public List<int> ClosedDays { get; set; } = new List<int>();
        public decimal TaxPercent { get; set; }
        public int CutOffHour { get; set; } = 14;
        public int MaxDaysAhead { get; set; } = 30;
        public bool AcceptCard { get; set; } = true;
        public bool AcceptLater { get; set; } = true;
        public int LowStock { get; set; } = 3;
        public int MaxVideoMb { get; set; } = 30;
        public List<Block> Blocks { get; set; } = new List<Block>();
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
    }

    public class HeroText
    {
        public string Eyebrow { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Button { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
    }

    public class PageDoc
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
        public List<PageVersion> Versions { get; set; } = new List<PageVersion>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PageVersion
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public string Extra { get; set; } = string.Empty;
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
        public bool Visible { get; set; } = true;
        public string Eyebrow { get; set; } = string.Empty;
        public string Poster { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Name { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime Added { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class Promise
    {
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class Occasion
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
    }

    public class Bouquet
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Colors { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public List<string> Photos { get; set; } = new List<string>();
        public List<string> Occasions { get; set; } = new List<string>();
        public List<BouquetSize> Sizes { get; set; } = new List<BouquetSize>();
        public int Stock { get; set; }
        public bool Published { get; set; } = true;
        public bool Featured { get; set; }
        public int Added { get; set; }
    }

    public class BouquetSize
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class Addon
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class Zone
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Fee { get; set; }
        public decimal FreeOver { get; set; }
        public List<string> Prefixes { get; set; } = new List<string>();
    }

    public class Slot
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class Faq
    {
        public string Q { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
    }

    public class CartIn
    {
        public string K { get; set; } = string.Empty;
        public string S { get; set; } = string.Empty;
        public List<string> A { get; set; } = new List<string>();
        public int Q { get; set; }
    }

    public class OrderLine
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SizeKey { get; set; } = string.Empty;
        public string SizeLabel { get; set; } = string.Empty;
        public decimal Unit { get; set; }
        public int Qty { get; set; }
        public List<string> Extras { get; set; } = new List<string>();
        public string Image { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Problem { get; set; } = string.Empty;
    }

    public class OrderNote
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Internal { get; set; }
    }

    public class OrderRec
    {
        public string No { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public bool Pickup { get; set; }
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Postcode { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public string SlotLabel { get; set; } = string.Empty;
        public string CardText { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
        public decimal Subtotal { get; set; }
        public decimal Fee { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string PayMethod { get; set; } = "later";
        public string PayStatus { get; set; } = "unpaid";
        public string Session { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public string Status { get; set; } = "new";
        public string Courier { get; set; } = string.Empty;
        public string Tracking { get; set; } = string.Empty;
        public List<OrderNote> Notes { get; set; } = new List<OrderNote>();
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public bool MustChange { get; set; }
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class LogEntry
    {
        public DateTime When { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Who { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class MessageRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public bool Read { get; set; }
    }

    public class PayConf
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Currency { get; set; } = "usd";
    }

    public class SessionRec
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
    }
}
