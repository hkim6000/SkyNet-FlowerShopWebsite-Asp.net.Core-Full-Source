using System.Text;
using FlowerShop.Core;
using FlowerShop.Models;
using SkyNet;

namespace FlowerShop.codes
{
    public class AdminMessages : WebPage
    {
        private const string AreaKey = "messages";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Messages | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminMessagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ms-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.SetElementContents("ms-who", Auth.Who("ms", c));
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
                response.SetElementContents("ms-main", "<div class=\"ms-panel\"><h1>No access</h1><p class=\"ms-hint\">You do not have access to messages. Ask a manager if you need it.</p></div>");
                return response;
            }
            List<MessageRec> messages = await Store.LoadMessages();
            response.SetElementContents("ms-main", "<div class=\"ms-ph\"><div><h1>Messages</h1><p>What customers sent from the Contact page.</p></div></div><div id=\"ms-list\">" + MessagesHtml(messages, Access.Can(c.U, AreaKey, 'E')) + "</div>");
            return response;
        }

        public async Task<ApiResponse> Mark()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 12);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "mark message", id, "You can look at messages but not change them.");
            }
            using (await Store.Hold())
            {
                List<MessageRec> list = await Store.LoadMessages();
                MessageRec? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.Read = GetDataValue("read") == "1";
                    await Store.SaveMessages(list);
                }
            }
            response.SetElementContents("ms-list", MessagesHtml(await Store.LoadMessages(), true));
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 12);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete message", id, "You can look at messages but not delete them.");
            }
            string who = string.Empty;
            using (await Store.Hold())
            {
                List<MessageRec> list = await Store.LoadMessages();
                MessageRec? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    who = m.Name;
                    list.Remove(m);
                    await Store.SaveMessages(list);
                }
            }
            await Store.Log(c.U.Id, "delete message", who, true);
            response.SetElementContents("ms-list", MessagesHtml(await Store.LoadMessages(), true));
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.toast('Message deleted');");
            return response;
        }

        private static string MessagesHtml(List<MessageRec> messages, bool edit)
        {
            if (messages.Count == 0)
            {
                return "<div class=\"ms-panel ms-empty\">No messages yet. They arrive from the Contact page.</div>";
            }
            string[] topics = { "order", "event", "delivery", "other" };
            string[] labels = { "An order", "A wedding or event", "Delivery", "Something else" };
            StringBuilder sb = new StringBuilder("<div class=\"ms-msgs\">");
            foreach (MessageRec m in messages.OrderByDescending(x => x.When).Take(100))
            {
                int t = Array.IndexOf(topics, m.Topic);
                sb.Append("<article class=\"ms-msg" + (m.Read ? string.Empty : " ms-unread") + "\"><div class=\"ms-msg-h\"><b>" + Ui.Esc(m.Name) + "</b><small>" + Ui.Esc(t >= 0 ? labels[t] : m.Topic) + " &middot; " + Ui.Esc(Ui.When(m.When)) + "</small></div>");
                sb.Append("<a href=\"mailto:" + Ui.Esc(m.Email) + "\">" + Ui.Esc(m.Email) + "</a><p>" + Ui.Esc(m.Text) + "</p>");
                if (edit)
                {
                    sb.Append("<div class=\"ms-acts\"><button type=\"button\" class=\"ms-btn ms-btn-ghost ms-btn-sm\" onclick=\"AdminMessagesJs.mark('" + Ui.Js(m.Id) + "','" + (m.Read ? "0" : "1") + "')\">" + (m.Read ? "Mark unread" : "Mark read") + "</button><button type=\"button\" class=\"ms-btn ms-btn-danger ms-btn-sm\" onclick=\"AdminMessagesJs.drop('" + Ui.Js(m.Id) + "','" + Ui.Js(m.Name) + "')\">Delete</button></div>");
                }
                sb.Append("</article>");
            }
            return sb.Append("</div>").ToString();
        }
    }
}
