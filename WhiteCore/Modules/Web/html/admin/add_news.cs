/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */


using System;
using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;

namespace WhiteCore.Modules.Web
{
    public class AddNewPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/admin/add_news.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return true; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return true; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();
            if (requestParameters.ContainsKey("Submit"))
            {
                string title = requestParameters["NewsTitle"].ToString();
                string text = requestParameters["NewsText"].ToString();
                IGenericsConnector connector = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();
                GridNewsItem item = new GridNewsItem {Text = text, Time = DateTime.Now, Title = title};
                item.ID = connector.GetGenericCount(UUID.Zero, "WebGridNews") + 1;
                connector.AddGeneric(UUID.Zero, "WebGridNews", item.ID.ToString(), item.ToOSD());
                response = "<h3>News item added successfully, redirecting to main page</h3>" +
                           "<script language=\"javascript\">" +
                           "setTimeout(function() {window.location.href = \"index.html?page=news_manager\";}, 0);" +
                           "</script>";
                return null;
            }

            vars.Add("NewsItemTitle", translator.GetTranslatedString("NewsItemTitle"));
            vars.Add("NewsItemText", translator.GetTranslatedString("NewsItemText"));
            vars.Add("AddNewsText", translator.GetTranslatedString("AddNewsText"));
            vars.Add("Submit", translator.GetTranslatedString("Submit"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}