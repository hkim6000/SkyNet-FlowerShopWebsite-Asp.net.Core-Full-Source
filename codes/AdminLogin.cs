using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminLogin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Staff sign in");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            bool demo = Store.Demo();
            if (!demo)
            {
                await Store.EnsureFirstManager();
            }
            SiteData site = await Store.LoadSite();
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_first}", demo ? DemoBox() : Local.Request() ? FirstBox(Store.InitialPassword()) : string.Empty)
                .Replace("{plhd_user}", demo ? "demo" : string.Empty)
                .Replace("{plhd_pass}", demo ? "demo1234" : string.Empty);
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("login"), 30).ToLowerInvariant();
            string secret = GetDataValue("secret") ?? string.Empty;
            if (Store.Demo())
            {
                Staff? real = (await Store.LoadStaff()).FirstOrDefault(x => x.Login == login && x.Active && !x.MustChange && x.Id != Store.DemoId);
                Staff who = real != null && secret.Length > 0 && Access.Verify(secret, real.Hash) ? real : await Store.DemoManager();
                string demoToken = await Auth.Open(who);
                await Store.Log(who.Id, "sign in", who.Login, true);
                response.ExecuteScript("AdminLoginJs.opened('" + demoToken + "');");
                return response;
            }
            if (Auth.Locked(login))
            {
                return Stop(response, "Too many attempts. Please wait a few minutes and try again.");
            }
            Staff? u = (await Store.LoadStaff()).FirstOrDefault(x => x.Login == login && x.Active);
            if (u == null || secret.Length == 0 || !Access.Verify(secret, u.Hash))
            {
                Auth.Fail(login);
                await Store.Log(login, "sign in", login, false);
                return Stop(response, "Wrong user name or password.");
            }
            Auth.Clear(login);
            if (u.MustChange)
            {
                response.ExecuteScript("AdminLoginJs.choose();");
                return response;
            }
            string token = await Auth.Open(u);
            await Store.Log(u.Id, "sign in", u.Login, true);
            response.ExecuteScript("AdminLoginJs.opened('" + token + "');");
            return response;
        }

        public async Task<ApiResponse> SetPassword()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("login"), 30).ToLowerInvariant();
            string secret = GetDataValue("secret") ?? string.Empty;
            string fresh = GetDataValue("fresh") ?? string.Empty;
            string again = GetDataValue("again") ?? string.Empty;
            if (Auth.Locked(login))
            {
                return Stop(response, "Too many attempts. Please wait a few minutes and try again.");
            }
            Staff? found = (await Store.LoadStaff()).FirstOrDefault(x => x.Login == login && x.Active && x.MustChange);
            if (found == null || !Access.Verify(secret, found.Hash))
            {
                Auth.Fail(login);
                return Stop(response, "The current password is not right.");
            }
            if (fresh.Length < 8 || fresh.Length > 100)
            {
                return Stop(response, "Use at least 8 characters for the new password.");
            }
            if (fresh == secret)
            {
                return Stop(response, "Choose a password different from the current one.");
            }
            if (fresh != again)
            {
                return Stop(response, "The two new passwords do not match.");
            }
            Staff? u;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                u = staff.FirstOrDefault(x => x.Id == found.Id);
                if (u == null)
                {
                    return Stop(response, "Please sign in again.");
                }
                u.Hash = Access.HashPassword(fresh);
                u.MustChange = false;
                await Store.SaveStaff(staff);
            }
            if (File.Exists(Store.InitialFile))
            {
                File.Delete(Store.InitialFile);
            }
            Auth.Clear(login);
            await Auth.Close(u.Id, string.Empty);
            string token = await Auth.Open(u);
            await Store.Log(u.Id, "new password", u.Login, true);
            response.ExecuteScript("AdminLoginJs.opened('" + token + "');");
            return response;
        }

        private static ApiResponse Stop(ApiResponse response, string message)
        {
            response.SetElementContents("lg-e-main", Ui.Esc(message));
            response.ExecuteScript("AdminLoginJs.failed();");
            return response;
        }

        private static string DemoBox()
        {
            return "<div class=\"lg-first\"><b>Demo</b><p>The sign-in is filled in for you. Any user name and password signs in as a demo manager with every right.</p></div>";
        }

        private static string FirstBox(string pass)
        {
            if (pass == string.Empty)
            {
                return string.Empty;
            }
            return "<div class=\"lg-first\"><b>First sign-in</b><dl><dt>User name</dt><dd>admin</dd><dt>Password</dt><dd>" + Ui.Esc(pass) + "</dd></dl><p>Shown only on this computer, until you choose your own password. Also in appConfig/initial-admin.txt.</p></div>";
        }
    }
}
