using System.Text;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public sealed class Ctx
    {
        public Staff U { get; init; } = new Staff();
        public string Token { get; init; } = string.Empty;
    }

    public static class Auth
    {
        private static readonly SemaphoreSlim SessGate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, List<DateTime>> Fails = new Dictionary<string, List<DateTime>>();
        private static readonly object FailLock = new object();

        private static string SessionFile => Path.Combine(Store.Root, "temp", "admin", "sessions.json");

        public static bool Locked(string login)
        {
            lock (FailLock)
            {
                if (!Fails.TryGetValue(login, out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        public static void Fail(string login)
        {
            lock (FailLock)
            {
                if (!Fails.TryGetValue(login, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Fails[login] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public static void Clear(string login)
        {
            lock (FailLock)
            {
                Fails.Remove(login);
            }
        }

        public static async Task<string> Open(Staff u)
        {
            await SessGate.WaitAsync();
            try
            {
                List<SessionRec> list = await Store.Read(SessionFile, () => new List<SessionRec>());
                list.RemoveAll(s => s.Expires < DateTime.UtcNow);
                string token = Access.NewToken();
                list.Add(new SessionRec { Token = token, UserId = u.Id, Expires = DateTime.UtcNow.AddHours(8) });
                await Store.Write(SessionFile, list);
                return token;
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task Close(string userId, string keepToken)
        {
            await SessGate.WaitAsync();
            try
            {
                List<SessionRec> list = await Store.Read(SessionFile, () => new List<SessionRec>());
                list.RemoveAll(s => s.UserId == userId && s.Token != keepToken);
                await Store.Write(SessionFile, list);
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task<Ctx?> Check(string? token)
        {
            string t = (token ?? string.Empty).Trim();
            if (t.Length != 32)
            {
                return null;
            }
            await SessGate.WaitAsync();
            SessionRec? s;
            try
            {
                List<SessionRec> list = await Store.Read(SessionFile, () => new List<SessionRec>());
                s = list.FirstOrDefault(x => x.Token == t && x.Expires > DateTime.UtcNow);
                if (s != null && s.Expires < DateTime.UtcNow.AddHours(7.5))
                {
                    s.Expires = DateTime.UtcNow.AddHours(8);
                    await Store.Write(SessionFile, list);
                }
            }
            finally
            {
                SessGate.Release();
            }
            if (s == null)
            {
                return null;
            }
            Staff? u = (await Store.LoadStaff()).FirstOrDefault(x => x.Id == s.UserId);
            if (u == null || !u.Active || u.MustChange || u.Id == Store.DemoId && !Store.Demo())
            {
                return null;
            }
            return new Ctx { U = u, Token = t };
        }

        public static async Task<string> Menu(string p, string current, Ctx c)
        {
            List<OrderRec> orders = await Orders.LoadFresh();
            List<MessageRec> messages = await Store.LoadMessages();
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            int fresh = orders.Count(o => o.Status == "new");
            int due = orders.Count(o => (o.Status == "new" || o.Status == "preparing" || o.Status == "out" || o.Status == "ready") && string.CompareOrdinal(o.Date, today) <= 0);
            int unread = messages.Count(m => !m.Read);
            StringBuilder sb = new StringBuilder();
            sb.Append(Item(p, current, "Admin", "Dashboard", 0));
            foreach (Area a in Access.Areas)
            {
                if (a.Key == "activity")
                {
                    sb.Append(Item(p, current, a.Page, Access.Level(c.U, a.Key).Length > 0 ? a.Name : "My activity", 0));
                    continue;
                }
                if (Access.Level(c.U, a.Key).Length == 0)
                {
                    continue;
                }
                int badge = a.Key == "orders" ? fresh : a.Key == "shipping" ? due : a.Key == "messages" ? unread : 0;
                sb.Append(Item(p, current, a.Page, a.Name, badge));
            }
            sb.Append(Item(p, current, "AdminAccount", "My account", 0));
            return sb.ToString();
        }

        private static string Item(string p, string current, string page, string label, int badge)
        {
            return "<a class=\"" + p + "-mi" + (page == current ? " " + p + "-on" : string.Empty) + "\" href=\"" + page + "\"><span>" + Ui.Esc(label) + "</span>" + (badge > 0 ? "<i class=\"" + p + "-badge\">" + badge + "</i>" : string.Empty) + "</a>";
        }

        public static string Who(string p, Ctx c)
        {
            return "<b class=\"" + p + "-who-n\">" + Ui.Esc(c.U.Name) + "</b><small>" + Ui.Esc(c.U.Admin ? "Manager" : c.U.Title) + "</small>";
        }
    }
}
