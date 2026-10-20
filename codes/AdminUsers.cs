using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminUsers : WebPage
    {
        private const string AreaKey = "users";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Users | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminUsersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"au-panel\"><h1>No access</h1><p class=\"au-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("au-brand", Ui.Esc(site.Brand));
            response.SetElementContents("au-menu", await Auth.Menu("au", "AdminUsers", c));
            response.SetElementContents("au-who", Auth.Who("au", c));
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
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("au-main", NoAccess("users"));
                return response;
            }
            response.SetElementContents("au-main", PageHtml(await Store.LoadStaff()));
            return response;
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "open user", id, "Only managers can edit users.");
            }
            Staff? u = id == string.Empty ? null : (await Store.LoadStaff()).FirstOrDefault(x => x.Id == id);
            if (id != string.Empty && u == null)
            {
                response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                return response;
            }
            response.SetElementContents("au-mbody", FormHtml(u, c));
            response.ExecuteScript("AdminUsersJs.opened('" + (u == null ? "New user" : Ui.Js(u.Name)) + "');AdminUsersJs.managerToggle();");
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
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save user", id, "Only managers can edit users.");
            }
            string login = Ui.Clip(GetDataValue("login"), 30).ToLowerInvariant();
            string name = Ui.Clip(GetDataValue("name"), 80);
            string title = Ui.Clip(GetDataValue("title"), 50);
            bool active = GetDataValue("active") == "1";
            bool admin = GetDataValue("admin") == "1";
            string eLogin = string.Empty;
            string eName = name.Length < 2 || name.Length > 60 ? "Enter a name of 2 to 60 characters." : string.Empty;
            string eTitle = title.Length > 40 ? "Keep it under 40 characters." : string.Empty;
            string eSelf = string.Empty;
            string password = string.Empty;
            string shown;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = id == string.Empty ? null : staff.FirstOrDefault(x => x.Id == id);
                if (id != string.Empty && u == null)
                {
                    response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                    return response;
                }
                if (u == null)
                {
                    if (login.Length < 3 || !login.All(ch => ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '.' || ch == '_' || ch == '-'))
                    {
                        eLogin = "Use 3 to 30 letters, numbers, dots, dashes or underscores.";
                    }
                    else if (staff.Any(x => x.Login == login))
                    {
                        eLogin = "That user name is already taken.";
                    }
                }
                if (u != null && u.Id == c.U.Id && (!admin || !active))
                {
                    eSelf = "You cannot remove your own manager right or switch yourself off.";
                }
                if (u != null && u.Admin && u.Active && (!admin || !active) && !staff.Any(x => x.Id != u.Id && x.Admin && x.Active))
                {
                    eSelf = "There must be at least one active manager.";
                }
                response.SetElementContents("au-e-login", eLogin);
                response.SetElementContents("au-e-name", eName);
                response.SetElementContents("au-e-title", eTitle);
                response.SetElementContents("au-e-self", eSelf);
                if (eLogin + eName + eTitle + eSelf != string.Empty)
                {
                    return response;
                }
                bool isNew = u == null;
                if (u == null)
                {
                    u = new Staff { Id = "u-" + Access.NewToken().Substring(0, 10), Login = login };
                    password = Access.NewPassword();
                    u.Hash = Access.HashPassword(password);
                    u.MustChange = true;
                    staff.Add(u);
                }
                bool wasOn = u.Active;
                u.Name = name;
                u.Title = title;
                u.Active = active;
                u.Admin = admin;
                u.Perms = new Dictionary<string, string>();
                foreach (Area a in Access.Areas)
                {
                    string letters = (GetDataValue("p_" + a.Key + "_V") == "1" ? "V" : string.Empty) + (GetDataValue("p_" + a.Key + "_E") == "1" ? "E" : string.Empty) + (GetDataValue("p_" + a.Key + "_X") == "1" ? "X" : string.Empty);
                    string clean = Access.Clean(a, letters);
                    if (clean != string.Empty)
                    {
                        u.Perms[a.Key] = clean;
                    }
                }
                await Store.SaveStaff(staff);
                if (wasOn && !active)
                {
                    await Auth.Close(u.Id, string.Empty);
                }
                shown = isNew ? u.Login : string.Empty;
                id = u.Id;
            }
            await Store.Log(c.U.Id, password == string.Empty ? "edit user" : "add user", name, true);
            response.SetElementContents("au-list", ListHtml(await Store.LoadStaff()));
            if (password != string.Empty)
            {
                response.SetElementContents("au-mbody", SecretHtml(name, shown, password));
                response.ExecuteScript("AdminUsersJs.clean();AdminUsersJs.toast('User added');");
                return response;
            }
            response.ExecuteScript("AdminUsersJs.saved('User saved');");
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "reset password", id, "Only managers can reset passwords.");
            }
            string password = Access.NewPassword();
            string name;
            string login;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == id);
                if (u == null)
                {
                    response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                    return response;
                }
                u.Hash = Access.HashPassword(password);
                u.MustChange = true;
                name = u.Name;
                login = u.Login;
                await Store.SaveStaff(staff);
            }
            await Auth.Close(id, string.Empty);
            await Store.Log(c.U.Id, "reset password", name, true);
            response.SetElementContents("au-mbody", SecretHtml(name, login, password));
            response.ExecuteScript("AdminUsersJs.opened('Password reset');");
            if (id == c.U.Id)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
            }
            return response;
        }

        private static string SecretHtml(string name, string login, string password)
        {
            return "<div class=\"au-secret\"><b>" + Ui.Esc(name) + "</b><span>User name: <b>" + Ui.Esc(login) + "</b></span><span>Temporary password:</span><code>" + Ui.Esc(password) + "</code><span>Give it to them in person. It is shown only now. They choose their own password at their first sign-in.</span></div><div class=\"au-acts\"><button type=\"button\" class=\"au-btn\" onclick=\"AdminUsersJs.shut()\">Done</button></div>";
        }

        private static string PageHtml(List<Staff> staff)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"au-ph\"><div><h1>Users</h1><p>Who can sign in, and what each person can do.</p></div><button type=\"button\" class=\"au-btn\" onclick=\"AdminUsersJs.edit('')\">Add a user</button></div>");
            sb.Append("<div id=\"au-list\">" + ListHtml(staff) + "</div>");
            return sb.ToString();
        }

        private static string ListHtml(List<Staff> staff)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"au-wrapt\"><table class=\"au-table\"><thead><tr><th>Person</th><th>User name</th><th>Role</th><th>Status</th><th></th></tr></thead><tbody>");
            foreach (Staff u in staff.OrderByDescending(x => x.Admin).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                string areas = u.Admin ? "Everything" : string.Join(", ", Access.Areas.Where(a => Access.Level(u, a.Key).Length > 0).Select(a => a.Name));
                sb.Append("<tr><td><b>" + Ui.Esc(u.Name) + "</b><small>" + Ui.Esc(u.Title) + "</small></td><td>" + Ui.Esc(u.Login) + "</td>");
                sb.Append("<td>" + (u.Admin ? "Manager" : "Staff") + "<small>" + (areas == string.Empty ? "No areas yet" : Ui.Esc(areas)) + "</small></td>");
                sb.Append("<td><span class=\"au-pill " + (!u.Active ? "au-pill-off" : u.MustChange ? "au-pill-wait" : "au-pill-go") + "\">" + (!u.Active ? "Off" : u.MustChange ? "Must choose a password" : "Active") + "</span></td>");
                sb.Append("<td><div class=\"au-rowacts\"><button type=\"button\" class=\"au-btn au-btn-ghost au-btn-sm\" onclick=\"AdminUsersJs.edit('" + Ui.Js(u.Id) + "')\">Edit</button><button type=\"button\" class=\"au-btn au-btn-ghost au-btn-sm\" onclick=\"AdminUsersJs.reset('" + Ui.Js(u.Id) + "','" + Ui.Js(u.Name) + "')\">Reset password</button></div></td></tr>");
            }
            return sb.Append("</tbody></table></div>").ToString();
        }

        private static string FormHtml(Staff? u, Ctx c)
        {
            bool self = u != null && u.Id == c.U.Id;
            StringBuilder sb = new StringBuilder();
            sb.Append("<form class=\"au-stack\" id=\"au-form\" onsubmit=\"return AdminUsersJs.save('" + (u == null ? string.Empty : Ui.Js(u.Id)) + "')\" novalidate>");
            sb.Append("<div class=\"au-grid2\"><label class=\"au-field\" for=\"au-f-name\"><span>Name</span><input type=\"text\" id=\"au-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(u?.Name) + "\"><em class=\"au-err\" id=\"au-e-name\"></em></label>");
            sb.Append("<label class=\"au-field\" for=\"au-f-title\"><span>Job title</span><input type=\"text\" id=\"au-f-title\" data-k=\"title\" maxlength=\"40\" value=\"" + Ui.Esc(u?.Title) + "\"><em class=\"au-err\" id=\"au-e-title\"></em></label></div>");
            sb.Append("<label class=\"au-field\" for=\"au-f-login\"><span>User name</span><input type=\"text\" id=\"au-f-login\" data-k=\"login\" maxlength=\"30\" autocapitalize=\"none\" spellcheck=\"false\" value=\"" + Ui.Esc(u?.Login) + "\"" + (u == null ? string.Empty : " disabled") + "><small>" + (u == null ? "A temporary password is made when you save." : "The user name cannot be changed.") + "</small><em class=\"au-err\" id=\"au-e-login\"></em></label>");
            sb.Append("<div class=\"au-checks\"><label class=\"au-check\"><input type=\"checkbox\" id=\"au-f-active\" data-k=\"active\"" + (u == null || u.Active ? " checked" : string.Empty) + ">Can sign in</label>");
            sb.Append("<label class=\"au-check\"><input type=\"checkbox\" id=\"au-f-admin\" data-k=\"admin\" onchange=\"AdminUsersJs.managerToggle()\"" + (u != null && u.Admin ? " checked" : string.Empty) + ">Manager, with every right</label></div>");
            sb.Append("<em class=\"au-err\" id=\"au-e-self\"></em>");
            if (self)
            {
                sb.Append("<p class=\"au-hint\">This is you. You cannot switch yourself off or remove your own manager right.</p>");
            }
            sb.Append("<div class=\"au-permwrap\"><table class=\"au-perm\"><thead><tr><th>Area</th><th>View</th><th>Change</th><th>Extra</th></tr></thead><tbody>");
            foreach (Area a in Access.Areas)
            {
                string lv = u == null ? string.Empty : Access.Level(u, a.Key);
                if (a.AdminOnly)
                {
                    sb.Append("<tr><td>" + Ui.Esc(a.Name) + "<small>Managers only</small></td><td>-</td><td>-</td><td>-</td></tr>");
                    continue;
                }
                string v = "<input type=\"checkbox\" data-k=\"p_" + a.Key + "_V\" aria-label=\"View " + Ui.Esc(a.Name) + "\"" + (lv.Contains('V') ? " checked" : string.Empty) + ">";
                string e = a.NoEdit ? "-" : "<input type=\"checkbox\" data-k=\"p_" + a.Key + "_E\" aria-label=\"Change " + Ui.Esc(a.Name) + "\"" + (lv.Contains('E') ? " checked" : string.Empty) + ">";
                string x = a.NoEdit || a.Extra == string.Empty ? "-" : "<input type=\"checkbox\" data-k=\"p_" + a.Key + "_X\" aria-label=\"" + Ui.Esc(a.Extra) + " in " + Ui.Esc(a.Name) + "\"" + (lv.Contains('X') ? " checked" : string.Empty) + ">";
                sb.Append("<tr><td>" + Ui.Esc(a.Name) + (a.Extra == string.Empty ? string.Empty : "<small>Extra: " + Ui.Esc(a.Extra.ToLowerInvariant()) + "</small>") + "</td><td>" + v + "</td><td>" + e + "</td><td>" + x + "</td></tr>");
            }
            sb.Append("</tbody></table></div><p class=\"au-hint\">Extra needs Change. Everyone can see their own activity.</p>");
            sb.Append("<div class=\"au-acts\"><button type=\"submit\" class=\"au-btn\">Save user</button><button type=\"button\" class=\"au-btn au-btn-ghost\" onclick=\"AdminUsersJs.closeModal()\">Cancel</button></div></form>");
            return sb.ToString();
        }
    }
}
