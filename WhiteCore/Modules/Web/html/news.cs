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


using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;

namespace WhiteCore.Modules.Web
{
    public class NewsPage : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/news.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return false; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();
            IGenericsConnector connector = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
            //GridNewsItem news = connector.GetGeneric<GridNewsItem> (UUID.Zero, "WebGridNews",
            //                                                       httpRequest.Query ["newsid"].ToString ());
 
            var newsItems = connector.GetGenerics<GridNewsItem> (UUID.Zero, "WebGridNews");
            if (newsItems.Count == 0)
                newsItems.Add (GridNewsItem.NoNewsItem);
            else
                vars.Add ("NewsList", newsItems.ConvertAll (item => item.ToDictionary ()));

            /*



            if (news != null) {
                vars.Add ("NewsTitle", news.Title);
                vars.Add ("NewsText", news.Text);
                vars.Add ("NewsID", news.ID.ToString ());
            } else {
                if (httpRequest.Query ["newsid"].ToString () == "-1") {
                    vars.Add ("NewsTitle", "No news to report");
                    vars.Add ("NewsText", "");
                } else {
                    vars.Add ("NewsTitle", "Invalid News Item");
                    vars.Add ("NewsText", "");
                }
                vars.Add ("NewsID", "-1");
            }
*/
            vars.Add ("News", translator.GetTranslatedString ("News"));
            vars.Add ("NewsItemTitle", translator.GetTranslatedString ("NewsItemTitle"));
            vars.Add ("NewsItemText", translator.GetTranslatedString ("NewsItemText"));
            vars.Add ("EditNewsText", translator.GetTranslatedString ("EditNewsText"));
            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
