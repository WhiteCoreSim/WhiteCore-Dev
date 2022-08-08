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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;

namespace WhiteCore.Modules.Web
{
    public class UserHomeMain : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new[]
                           {
                               "html/userhome.html",
                               "html/user/userhome.html"
                          };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            IWebHttpTextureService webhttpService =  webInterface.Registry.RequestModuleInterface<IWebHttpTextureService>();
            response = null;
            var vars = new Dictionary<string, object> ();

            //var settings = webInterface.GetWebUISettings ();
            // user setup news inclusion
            //if (settings.LocalUserFrontPage == "") {
            //    vars.Add ("LocalPage", false);
            //    vars.Add ("LocalFrontPage", "");
            //} else {
            //    vars.Add ("LocalPage", true);
            //    vars.Add ("LocalFrontPage", settings.LocalUserFrontPage);
            //}

            UserAccount userAcct = Authenticator.GetAuthentication(httpRequest);
            if (!userAcct.Valid)
                return vars;

            // build the dashboard info
            vars.Add("UserName", userAcct.Name);
            vars.Add("ResidentSince", translator.GetTranslatedString("ResidentSince"));
            vars.Add("UserBorn", Util.ToDateTime(userAcct.Created).ToString("ddd, dd MMM yyyy"));

            IUserProfileInfo profile = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>().
                GetUserProfile(userAcct.PrincipalID);
            string picUrl = "../static/icons/no_avatar.jpg";
            if (profile != null) {
                vars.Add("UserType", profile.MembershipGroup == "" ? "Resident" : profile.MembershipGroup);
                if (webhttpService != null && profile.Image != UUID.Zero)
                    picUrl = webhttpService.GetTextureURL(profile.Image);
            } else {
                // no profile yet
                vars.Add("UserType", translator.GetTranslatedString("Guest"));
            }
            vars.Add("UserPictureURL", picUrl);

            var infoService = webInterface.Registry.RequestModuleInterface<IAgentInfoService>();
            var userInfo = infoService.GetUserInfo(userAcct.PrincipalID.ToString());
            var gridService = webInterface.Registry.RequestModuleInterface<IGridService>();
            var homeRegion = userInfo.HomeRegionID;

            vars.Add("UserLastLogin", userInfo.LastLogin.ToString("ddd, dd MMM yyyy h:mm tt"));
            vars.Add("UserHomeRegion", gridService.GetRegionByUUID(null, homeRegion).RegionName);
            vars.Add("LastLoginText", translator.GetTranslatedString("LastText") + " " + translator.GetTranslatedString("Login"));
            vars.Add("HomeText", translator.GetTranslatedString("HomeText"));

            // build a friends list for the user
            /*  maybe not required here ??
             *  
            var friendsService = webInterface.Registry.RequestModuleInterface<IFriendsService>();
            var userFriendsList = new List<UUID>();
            var friendsList = new List<Dictionary<string, object>>();

            if (friendsService != null) {
                var friends = friendsService.GetFriends(userAcct.PrincipalID);
                foreach (var friend in friends) {
                    UUID friendID;
                    UUID.TryParse(friend.Friend, out friendID);

                    if ((friendID != UUID.Zero) && (friendID != userAcct.PrincipalID))       // our own id is returned??
                        userFriendsList.Add(friendID);
                }
            }

            if (userFriendsList.Count > 0) {
                vars.Add("HaveFriendData", true);
                vars.Add("NoFriendData", false);

                var accountService = webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                var gridService = webInterface.Registry.RequestModuleInterface<IGridService>();
                var agentInfo = Framework.Utilities.DataManager.RequestPlugin<IAgentInfoConnector>();
                var activeUsers = agentInfo.CurrentlyOnline(0, new Dictionary<string, bool>());

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
                    var friendAcct = accountService.GetUserAccount(null, userfriendID);
                    if (friendAcct.Valid) {
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
                } else {
                    vars.Add("HaveFriendData", false);
                    vars.Add("NoFriendData", true);
                }
            } 
            */

            // user groups
            vars.Add("UsersGroupsText", translator.GetTranslatedString("UsersGroupsText"));

            IGroupsServiceConnector groupsConnector =
                Framework.Utilities.DataManager.RequestPlugin<IGroupsServiceConnector>();
            List<Dictionary<string, object>> groups = new List<Dictionary<string, object>>();

            int groupCount = 0;
            if (groupsConnector != null) {
                var groupsIn = groupsConnector.GetAgentGroupMemberships(userAcct.PrincipalID, userAcct.PrincipalID);
                if (groupsIn != null) {
                    groupCount = groups.Count;
                    foreach (var grp in groupsIn) {
                        var grpData = groupsConnector.GetGroupProfile(userAcct.PrincipalID, grp.GroupID);
                        string url = "../static/icons/no_groups.jpg";
                        if (grpData != null) {
                            if (webhttpService != null && grpData.InsigniaID != UUID.Zero)
                                url = webhttpService.GetTextureURL(grpData.InsigniaID);
                            groups.Add(new Dictionary<string, object> {
                            { "GroupPictureURL", url },
                            { "GroupName", grp.GroupName }
                        });
                        }
                    }
                }


                if (groups.Count == 0) {
                    groups.Add(new Dictionary<string, object> {
                        { "GroupPictureURL", "../static/icons/no_groups.jpg" },
                        { "GroupName", "None yet" }
                    });
                }
            }

            // bank balance
            IConfig gridInfo = webInterface.Registry.RequestModuleInterface<ISimulationBase>().ConfigSource.Configs["GridInfoService"];
            var inWorldCurrency = gridInfo.GetString("CurrencySymbol", string.Empty) + " ";
            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule>();
            if (moneyModule != null) {
                var userCurrency = moneyModule.Balance(userAcct.PrincipalID);
                vars.Add("UserBalance", inWorldCurrency + " " + userCurrency.ToString());
            } else {
                vars.Add("UserBalance", inWorldCurrency + " 0.00");
            }

            // some base text
            vars.Add("DashboardNameText", translator.GetTranslatedString("DashboardNameText"));
            vars.Add("AccountType", translator.GetTranslatedString("AccountType"));
            vars.Add("BankBalanceText", translator.GetTranslatedString("BankBalanceText"));
    
            vars.Add("GroupNameText", translator.GetTranslatedString("GroupNameText"));
            vars.Add("Groups", groups);
            vars.Add("GroupsJoined", groupCount);


            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
