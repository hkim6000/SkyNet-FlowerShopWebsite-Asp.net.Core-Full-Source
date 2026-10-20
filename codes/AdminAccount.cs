using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminAccount : WebPage
    {
                public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Account | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminAccountJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"aa-panel\"><h1>No access</h1><p class=\"aa-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("aa-brand", Ui.Esc(site.Brand));
            response.SetElementContents("aa-menu", await Auth.Menu("aa", "AdminAccount", c));
            response.SetElementContents("aa-who", Auth.Who("aa", c));
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
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"aa-ph\"><div><h1>My account</h1><p>" + Ui.Esc(c.U.Name) + " &middot; user name <b>" + Ui.Esc(c.U.Login) + "</b></p></div></div>");
            sb.Append("<div class=\"aa-grid2\"><form class=\"aa-panel aa-stack\" onsubmit=\"return AdminAccountJs.save()\" novalidate><h2>Change password</h2>");
            sb.Append("<label class=\"aa-field\" for=\"aa-cur\"><span>Current password</span><input type=\"password\" id=\"aa-cur\" autocomplete=\"current-password\" maxlength=\"100\"><em class=\"aa-err\" id=\"aa-e-cur\"></em></label>");
            sb.Append("<label class=\"aa-field\" for=\"aa-np\"><span>New password</span><input type=\"password\" id=\"aa-np\" autocomplete=\"new-password\" maxlength=\"100\"><small>At least 8 characters.</small><em class=\"aa-err\" id=\"aa-e-np\"></em></label>");
            sb.Append("<label class=\"aa-field\" for=\"aa-np2\"><span>New password again</span><input type=\"password\" id=\"aa-np2\" autocomplete=\"new-password\" maxlength=\"100\"><em class=\"aa-err\" id=\"aa-e-np2\"></em></label>");
            sb.Append("<div class=\"aa-acts\"><button type=\"submit\" class=\"aa-btn\">Change password</button></div></form>");
            sb.Append("<div class=\"aa-panel\"><h2>What you can do</h2><div class=\"aa-wrapt\"><table class=\"aa-table\" style=\"min-width:0\"><thead><tr><th>Area</th><th>Rights</th></tr></thead><tbody>");
            foreach (Area a in Access.Areas)
            {
                string lv = Access.Level(c.U, a.Key);
                string text = lv == string.Empty ? "None" : (lv.Contains('V') ? "View" : string.Empty) + (lv.Contains('E') ? ", change" : string.Empty) + (lv.Contains('X') ? ", " + a.Extra.ToLowerInvariant() : string.Empty);
                sb.Append("<tr><td>" + Ui.Esc(a.Name) + "</td><td>" + Ui.Esc(text) + "</td></tr>");
            }
            sb.Append("</tbody></table></div></div></div>");
            response.SetElementContents("aa-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string cur = GetDataValue("cur") ?? string.Empty;
            string np = GetDataValue("np") ?? string.Empty;
            string np2 = GetDataValue("np2") ?? string.Empty;
            string eCur = !Access.Verify(cur, c.U.Hash) ? "That is not your current password." : string.Empty;
            string eNp = np.Length < 8 ? "Use at least 8 characters." : np.Length > 100 ? "Use at most 100 characters." : np == cur ? "Choose a different password." : string.Empty;
            string eNp2 = np != np2 ? "The two passwords do not match." : string.Empty;
            response.SetElementContents("aa-e-cur", eCur);
            response.SetElementContents("aa-e-np", eNp);
            response.SetElementContents("aa-e-np2", eNp2);
            if (eCur + eNp + eNp2 != string.Empty)
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u == null)
                {
                    return response;
                }
                u.Hash = Access.HashPassword(np);
                await Store.SaveStaff(staff);
            }
            await Auth.Close(c.U.Id, c.Token);
            await Store.Log(c.U.Id, "password", c.U.Login, true);
            response.ExecuteScript("AdminAccountJs.saved();");
            return response;
        }
    }
}
