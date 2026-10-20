using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminActivity : WebPage
    {
        private const string AreaKey = "activity";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Activity | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminActivityJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminActivityJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"av-panel\"><h1>No access</h1><p class=\"av-hint\">You do not have access to " + Ui.Esc(what) + ". Ask a manager if you need it.</p></div>";
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("av-brand", Ui.Esc(site.Brand));
            response.SetElementContents("av-menu", await Auth.Menu("av", "AdminActivity", c));
            response.SetElementContents("av-who", Auth.Who("av", c));
        }

        private const int Size = 25;

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
            bool all = Access.Can(c.U, AreaKey, 'V');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"av-ph\"><div><h1>" + (all ? "Activity log" : "My activity") + "</h1><p>" + (all ? "Who changed what, and any refused requests." : "What you have done in the staff area.") + "</p></div></div>");
            sb.Append("<div class=\"av-tools\"><input type=\"search\" id=\"av-term\" placeholder=\"Search people, actions, orders\" maxlength=\"40\" oninput=\"AdminActivityJs.type()\" aria-label=\"Search\"><select id=\"av-res\" onchange=\"AdminActivityJs.list(1)\" aria-label=\"Result\"><option value=\"\">All results</option><option value=\"ok\">Done</option><option value=\"no\">Refused</option></select></div>");
            sb.Append("<div id=\"av-list\">" + ListHtml(await Store.LoadLog(), c, string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("av-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string term = Ui.Clip(GetDataValue("term"), 40);
            string res = Ui.Pick(GetDataValue("res"), new[] { "ok", "no" });
            int pg = Ui.Bound(GetDataValue("pg"), 1, 100000, 1);
            response.SetElementContents("av-list", ListHtml(await Store.LoadLog(), c, term, res, pg));
            return response;
        }

        private static string ListHtml(List<LogEntry> log, Ctx c, string term, string res, int pg)
        {
            IEnumerable<LogEntry> q = log.OrderByDescending(l => l.When);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                q = q.Where(l => l.UserId == c.U.Id);
            }
            if (res == "ok")
            {
                q = q.Where(l => l.Ok);
            }
            else if (res == "no")
            {
                q = q.Where(l => !l.Ok);
            }
            if (term != string.Empty)
            {
                q = q.Where(l => Ui.Has(l.Who, term) || Ui.Has(l.Action, term) || Ui.Has(l.Target, term));
            }
            List<LogEntry> all = q.ToList();
            int pages = Math.Max(1, (all.Count + Size - 1) / Size);
            pg = Math.Min(pg, pages);
            if (all.Count == 0)
            {
                return "<div class=\"av-empty\">Nothing logged yet.</div>";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"av-wrapt\"><table class=\"av-table\"><thead><tr><th>When</th><th>Who</th><th>Action</th><th>Target</th><th>Result</th></tr></thead><tbody>");
            foreach (LogEntry l in all.Skip((pg - 1) * Size).Take(Size))
            {
                sb.Append("<tr><td>" + Ui.Esc(Ui.When(l.When)) + "</td><td>" + Ui.Esc(l.Who) + "</td><td>" + Ui.Esc(l.Action) + "</td><td>" + Ui.Esc(l.Target) + "</td><td><span class=\"av-pill " + (l.Ok ? "av-pill-go" : "av-pill-bad") + "\">" + (l.Ok ? "Done" : "Refused") + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append("<div class=\"av-pager\"><span>Page " + pg + " of " + pages + " &middot; " + Ui.Count(all.Count, "entry", "entries") + "</span><div>");
            sb.Append("<button type=\"button\" class=\"av-btn av-btn-ghost av-btn-sm\"" + (pg <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminActivityJs.list(" + (pg - 1) + ")\">Previous</button>");
            sb.Append("<button type=\"button\" class=\"av-btn av-btn-ghost av-btn-sm\"" + (pg >= pages ? " disabled" : string.Empty) + " onclick=\"AdminActivityJs.list(" + (pg + 1) + ")\">Next</button>");
            sb.Append("</div></div>");
            return sb.ToString();
        }
    }
}
