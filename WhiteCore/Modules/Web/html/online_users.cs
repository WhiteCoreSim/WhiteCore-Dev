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

using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using OpenMetaverse;
using System.Collections.Generic;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class OnlineUsersPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/online_users.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return false; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();
            var usersList = new List<Dictionary<string, object>>();
            var libraryOwner = new UUID(Constants.LibraryOwner);
			var realestateOwner = new UUID(Constants.RealEstateOwnerUUID);

            uint amountPerQuery = 10;
            int start = httpRequest.Query.ContainsKey("Start") ? int.Parse(httpRequest.Query["Start"].ToString()) : 0;
            uint count = Framework.Utilities.DataManager.RequestPlugin<IAgentInfoConnector>().RecentlyOnline(5*60, true);
            int maxPages = (int) (count/amountPerQuery) - 1;

            if (start == -1)
                start = (int) (maxPages < 0 ? 0 : maxPages);

            vars.Add("CurrentPage", start);
            vars.Add("NextOne", start + 1 > maxPages ? start : start + 1);
            vars.Add("BackOne", start - 1 < 0 ? 0 : start - 1);

             var activeUsers = Framework.Utilities.DataManager.RequestPlugin<IAgentInfoConnector>()
                                   .RecentlyOnline(5*60, true, new Dictionary<string, bool>(), (uint) start,
                                                   amountPerQuery);
            if (activeUsers.Count > 0)
            {
                IUserAccountService accountService = webInterface.Registry.RequestModuleInterface<IUserAccountService> ();
                IGridService gridService = webInterface.Registry.RequestModuleInterface<IGridService> ();
                foreach (var user in activeUsers)
                {
					if ( (UUID.Parse (user.UserID) == libraryOwner) ||
						 (UUID.Parse (user.UserID) == realestateOwner) 
					    )
                        continue;

                    var region = gridService.GetRegionByUUID (null, user.CurrentRegionID);
                    var account = accountService.GetUserAccount (region.AllScopeIDs, UUID.Parse (user.UserID));
                    if (account != null && region != null)
                    {
                        usersList.Add (new Dictionary<string, object> {
                            { "UserName", account.Name },
                            { "UserRegion", region.RegionName },
                            { "UserLocation",  user.CurrentPosition },
                            { "UserID", user.UserID },
                            { "UserRegionID", region.RegionID }
                        });
                    }
                }
            }
            else
            {
                usersList.Add(
                    new Dictionary<string, object>
                {
                    {"UserName", ""},
                    {"UserRegion", ""},
                    {"UserLocation", "No users are currently logged in"},
                    {"UserID", ""},
                    {"UserRegionID", ""}
                });
            }

            if (requestParameters.ContainsKey("Order"))
            {
                if (requestParameters["Order"].ToString() == "RegionName")
                    usersList.Sort((a, b) => a["UserRegion"].ToString().CompareTo(b["UserRegion"].ToString()));
                if (requestParameters["Order"].ToString() == "UserName")
                    usersList.Sort((a, b) => a["UserName"].ToString().CompareTo(b["UserName"].ToString()));
            }


            vars.Add("UsersOnlineList", usersList);
            vars.Add("OnlineUsersText", translator.GetTranslatedString("OnlineUsersText"));
            vars.Add("UserNameText", translator.GetTranslatedString("UserNameText"));
            vars.Add("OnlineLocationText", translator.GetTranslatedString("OnlineLocationText"));
            vars.Add("RegionNameText", translator.GetTranslatedString("RegionNameText"));
            vars.Add("MoreInfoText", translator.GetTranslatedString("MoreInfoText"));

            vars.Add("FirstText", translator.GetTranslatedString("FirstText"));
            vars.Add("BackText", translator.GetTranslatedString("BackText"));
            vars.Add("NextText", translator.GetTranslatedString("NextText"));
            vars.Add("LastText", translator.GetTranslatedString("LastText"));
            vars.Add("CurrentPageText", translator.GetTranslatedString("CurrentPageText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}