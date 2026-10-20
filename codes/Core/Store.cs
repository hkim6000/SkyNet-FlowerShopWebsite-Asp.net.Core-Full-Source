using System.Text.Json;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public static class Store
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim LogGate = new SemaphoreSlim(1, 1);

        public static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string InitialFile => Path.Combine(Root, "appConfig", "initial-admin.txt");

        public static string ConfigFile => Path.Combine(Root, "appConfig", "application.cfg");

        public const string DemoId = "u-demo";

        private static DateTime configStamp = DateTime.MinValue;
        private static bool demoMode;

        public static bool Demo()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return false;
                }
                DateTime stamp = File.GetLastWriteTimeUtc(ConfigFile);
                if (stamp != configStamp)
                {
                    string line = File.ReadLines(ConfigFile).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("app.app.runmode", StringComparison.Ordinal)) ?? string.Empty;
                    string[] part = line.Split('|', 2);
                    demoMode = part.Length == 2 && part[1].Trim() == "0";
                    configStamp = stamp;
                }
                return demoMode;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public static async Task<Staff> DemoManager()
        {
            using (await Hold())
            {
                List<Staff> staff = await LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == DemoId);
                bool changed = false;
                if (u == null)
                {
                    u = new Staff { Id = DemoId, Login = "demo", Name = "Demo manager", Title = "Manager", Hash = Access.HashPassword(Access.NewPassword()) };
                    staff.Add(u);
                    changed = true;
                }
                if (!u.Admin || !u.Active || u.MustChange)
                {
                    u.Admin = true;
                    u.Active = true;
                    u.MustChange = false;
                    changed = true;
                }
                if (changed)
                {
                    await SaveStaff(staff);
                }
                return u;
            }
        }

        private sealed class Release : IDisposable
        {
            private bool done;

            public void Dispose()
            {
                if (!done)
                {
                    done = true;
                    Gate.Release();
                }
            }
        }

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Release();
        }

        public static async Task<T> Read<T>(string file, Func<T> empty)
        {
            if (!File.Exists(file))
            {
                return empty();
            }
            try
            {
                string json = await File.ReadAllTextAsync(file);
                return JsonSerializer.Deserialize<T>(json, Opts) ?? empty();
            }
            catch (JsonException)
            {
                return empty();
            }
        }

        public static async Task Write<T>(string file, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
            string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, Opts));
            File.Move(tmp, file, true);
        }

        public static async Task<SiteData> LoadSite()
        {
            SiteData site = await Read(Path.Combine(DataDir, "site.json"), () => new SiteData());
            Normalize(site);
            return site;
        }

        public static void Normalize(SiteData site)
        {
            foreach (Bouquet b in site.Bouquets)
            {
                if (b.Photos.Count == 0 && b.Image != string.Empty)
                {
                    b.Photos.Add(b.Image.Contains('/') ? b.Image : "images/bouquets/" + b.Image + ".webp");
                }
                b.Image = string.Empty;
            }
            foreach (Occasion o in site.Occasions)
            {
                if (o.Image != string.Empty && !o.Image.Contains('/'))
                {
                    o.Image = "images/occasions/" + o.Image + ".webp";
                }
            }
            if (site.Hero.Image == string.Empty)
            {
                site.Hero.Image = "images/hero.webp";
            }
            if (site.MaxVideoMb < 1 || site.MaxVideoMb > Media.CeilingMb)
            {
                site.MaxVideoMb = 30;
            }
            Blocks.Ensure(site);
        }

        public static Task SaveSite(SiteData site)
        {
            return Write(Path.Combine(DataDir, "site.json"), site);
        }

        public static Task<List<OrderRec>> LoadOrders()
        {
            return Read(Path.Combine(DataDir, "orders.json"), () => new List<OrderRec>());
        }

        public static Task SaveOrders(List<OrderRec> orders)
        {
            return Write(Path.Combine(DataDir, "orders.json"), orders);
        }

        public static Task<List<MessageRec>> LoadMessages()
        {
            return Read(Path.Combine(DataDir, "messages.json"), () => new List<MessageRec>());
        }

        public static Task SaveMessages(List<MessageRec> list)
        {
            return Write(Path.Combine(DataDir, "messages.json"), list);
        }

        public static Task<List<Staff>> LoadStaff()
        {
            return Read(Path.Combine(DataDir, "staff.json"), () => new List<Staff>());
        }

        public static Task SaveStaff(List<Staff> staff)
        {
            return Write(Path.Combine(DataDir, "staff.json"), staff);
        }

        public static Task<PayConf> LoadPay()
        {
            return Read(Path.Combine(DataDir, "payment.json"), () => new PayConf());
        }

        public static Task SavePay(PayConf conf)
        {
            return Write(Path.Combine(DataDir, "payment.json"), conf);
        }

        public static async Task Log(string userId, string action, string target, bool ok)
        {
            await LogGate.WaitAsync();
            try
            {
                string file = Path.Combine(DataDir, "activity.json");
                List<LogEntry> list = await Read(file, () => new List<LogEntry>());
                List<Staff> staff = await LoadStaff();
                string who = staff.FirstOrDefault(u => u.Id == userId)?.Name ?? userId;
                list.Add(new LogEntry { When = DateTime.UtcNow, UserId = userId, Who = who, Action = action, Target = target, Ok = ok });
                if (list.Count > 5000)
                {
                    list = list.Skip(list.Count - 5000).ToList();
                }
                await Write(file, list);
            }
            finally
            {
                LogGate.Release();
            }
        }

        public static Task<List<LogEntry>> LoadLog()
        {
            return Read(Path.Combine(DataDir, "activity.json"), () => new List<LogEntry>());
        }

        public static async Task EnsureFirstManager()
        {
            using (await Hold())
            {
                List<Staff> staff = await LoadStaff();
                if (staff.Any(u => u.Admin && u.Active && !u.MustChange && u.Hash != string.Empty))
                {
                    return;
                }
                if (File.Exists(InitialFile) && staff.Any(u => u.Admin && u.Active && u.MustChange))
                {
                    return;
                }
                string pass = Access.NewPassword();
                Staff? u = staff.FirstOrDefault(x => x.Login == "admin");
                if (u == null)
                {
                    u = new Staff { Id = "u-admin", Login = "admin", Name = "Administrator", Title = "Manager" };
                    staff.Insert(0, u);
                }
                u.Admin = true;
                u.Active = true;
                u.MustChange = true;
                u.Hash = Access.HashPassword(pass);
                await SaveStaff(staff);
                Directory.CreateDirectory(Path.GetDirectoryName(InitialFile) ?? Root);
                await File.WriteAllTextAsync(InitialFile, "user: admin" + Environment.NewLine + "password: " + pass + Environment.NewLine);
            }
        }

        public static string InitialPassword()
        {
            if (!File.Exists(InitialFile))
            {
                return string.Empty;
            }
            string line = File.ReadAllLines(InitialFile).FirstOrDefault(l => l.StartsWith("password: ", StringComparison.Ordinal)) ?? string.Empty;
            return line.Length > 10 ? line.Substring(10).Trim() : string.Empty;
        }
    }
}
