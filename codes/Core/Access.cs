using System.Security.Cryptography;
using FlowerShop.Models;

namespace FlowerShop.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "orders", Name = "Orders", Page = "AdminOrders", Extra = "Cancel and refund" },
            new Area { Key = "shipping", Name = "Shipping", Page = "AdminShipping", Extra = "Dispatch and deliver" },
            new Area { Key = "products", Name = "Bouquets", Page = "AdminProducts", Extra = "Publish and delete" },
            new Area { Key = "pages", Name = "Pages", Page = "AdminPages" },
            new Area { Key = "media", Name = "Media library", Page = "AdminMedia", Extra = "Delete files" },
            new Area { Key = "messages", Name = "Messages", Page = "AdminMessages" },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", NoEdit = true },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", AdminOnly = true },
            new Area { Key = "users", Name = "Users", Page = "AdminUsers", AdminOnly = true }
        };

        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static Area? Find(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Level(Staff user, string key)
        {
            Area? a = Find(key);
            if (a == null || !user.Active)
            {
                return string.Empty;
            }
            if (user.Admin)
            {
                return a.NoEdit ? "V" : "VEX";
            }
            if (a.AdminOnly)
            {
                return string.Empty;
            }
            return Clean(a, user.Perms.TryGetValue(key, out string? p) ? p : string.Empty);
        }

        public static bool Can(Staff user, string key, char right)
        {
            return Level(user, key).IndexOf(right) >= 0;
        }

        public static string Clean(Area a, string letters)
        {
            if (a.AdminOnly || letters.IndexOf('V') < 0)
            {
                return string.Empty;
            }
            bool e = !a.NoEdit && letters.IndexOf('E') >= 0;
            bool x = e && a.Extra != string.Empty && letters.IndexOf('X') >= 0;
            return "V" + (e ? "E" : string.Empty) + (x ? "X" : string.Empty);
        }

        public static string NewPassword()
        {
            char[] c = new char[14];
            for (int i = 0; i < 14; i++)
            {
                c[i] = i % 5 == 4 ? '-' : Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
            }
            return new string(c, 0, 14);
        }

        public static string NewToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            string[] p = stored.Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2" || !int.TryParse(p[1], out int rounds))
            {
                return false;
            }
            byte[] salt = Convert.FromBase64String(p[2]);
            byte[] want = Convert.FromBase64String(p[3]);
            byte[] got = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, want.Length);
            return CryptographicOperations.FixedTimeEquals(got, want);
        }
    }
}
