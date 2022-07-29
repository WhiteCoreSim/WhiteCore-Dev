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
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class UserFriendsPage : IWebInterfacePage
    {
        public string[] FilePath {
            get {
                return new[]
                           {
                               "html/user/friends.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response) {
            response = null;
            var vars = new Dictionary<string, object>();
            var friendsList = new List<Dictionary<string, object>>();
            var agentInfo = Framework.Utilities.DataManager.RequestPlugin<IAgentInfoConnector>();

            var IsAdmin = Authenticator.CheckAdminAuthentication(httpRequest);

            //var activeUsers = agentInfo.RecentlyOnline(15*60, true, new Dictionary<string, bool>());  // active in the last 15 minutes
            var activeUsers = agentInfo.CurrentlyOnline(0, new Dictionary<string, bool>());

            // build a friends list for the user
            var userFriendsList = new List<UUID>();

            var ourAccount = Authenticator.GetAuthentication(httpRequest);
            if (ourAccount.Valid) {
                var friendsService = webInterface.Registry.RequestModuleInterface<IFriendsService>();
                if (friendsService != null) {
                    var friends = friendsService.GetFriends(ourAccount.PrincipalID);
                    foreach (var friend in friends) {
                        UUID friendID;
                        UUID.TryParse(friend.Friend, out friendID);

                        if ((friendID != UUID.Zero) && (friendID != ourAccount.PrincipalID))       // our own id is returned??
                            userFriendsList.Add(friendID);
                    }
                }

                var accountService = webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                var gridService = webInterface.Registry.RequestModuleInterface<IGridService>();

                if (userFriendsList.Count > 0) {
                    vars.Add("HaveData", true);
                    vars.Add("NoData", false);

                    foreach (var userfriendID in userFriendsList) {
                        var isonline = false;
                        var friendPosition = Vector3.Zero;
                        var regionName = "Offline";
                        var regionID = "";

                        foreach (var online_user in activeUsers) {
                            if (online_user.UserID == userfriendID.ToString()) {
                                isonline = true;
                                friendPosition = online_user.CurrentPosition;
                                var region = gridService.GetRegionByUUID(null, online_user.CurrentRegionID);
                                if (region != null) {
                                    regionName = region.RegionName;
                                    regionID = region.RegionID.ToString();
                                }
                                break;
                            }
                        }

                        // add details
                        var userAcct = accountService.GetUserAccount(null, userfriendID);
                        if (userAcct.Valid) {
                            friendsList.Add(new Dictionary<string, object> {
                                { "UserName", userAcct.Name },
                                { "UserRegion", regionName },
                                { "UserLocation",  friendPosition.ToString() },
                                { "UserID", userfriendID },
                                { "UserRegionID", regionID },
                                { "IsOnline", isonline },
                                {"HopUrl", webInterface.HopVectorUrl(regionName, friendPosition)}
                            });
                        }
                    }
                }
            }

            if (friendsList.Count == 0) {
                vars.Add("HaveData", false);
                vars.Add("NoData", true);
                /*
                friendsList.Add(
                    new Dictionary<string, object>
                {
                    {"UserName", ""},
                    {"UserRegion", "No friends found"},
                    {"UserLocation", ""},
                    {"UserID", ""},
                    {"UserRegionID", ""},
                    {"IsOnline", false }
                });*/
            }

            if (requestParameters.ContainsKey("Order")) {
                if (requestParameters["Order"].ToString() == "RegionName")
                    friendsList.Sort((a, b) => string.Compare(a["UserRegion"].ToString(), b["UserRegion"].ToString(), System.StringComparison.Ordinal));
                if (requestParameters["Order"].ToString() == "UserName")
                    friendsList.Sort((a, b) => string.Compare(a["UserName"].ToString(), b["UserName"].ToString(), System.StringComparison.Ordinal));
            }

            vars.Add("UserName", ourAccount.Name);
            vars.Add("UserFriendsText", translator.GetTranslatedString("Friends"));
            vars.Add("UserFriendsList", friendsList);
            vars.Add("UserNameText", translator.GetTranslatedString("UserNameText"));
            vars.Add("OnlineLocationText", translator.GetTranslatedString("OnlineLocationText"));
            vars.Add("RegionNameText", translator.GetTranslatedString("RegionNameText"));
            vars.Add("MoreInfoText", translator.GetTranslatedString("MoreInfoText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text) {
            text = "";
            return false;
        }
    }
}
