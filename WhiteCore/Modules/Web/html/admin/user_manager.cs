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
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class UserManagerPage : IWebInterfacePage
    {
        public string[] FilePath {
            get {
                return new[]
                           {
                               "html/admin/user_manager.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return true; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response) {
            response = null;
            var vars = new Dictionary<string, object>();
            var usersList = new List<Dictionary<string, object>>();
            var IsAdmin = Authenticator.CheckAdminAuthentication(httpRequest);

            string noDetails = translator.GetTranslatedString("NoDetailsText");
            var webhttpService = webInterface.Registry.RequestModuleInterface<IWebHttpTextureService>();
            var profileservice = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>();
            var accountService = webInterface.Registry.RequestModuleInterface<IUserAccountService>();
            var agentInfoService = webInterface.Registry.RequestModuleInterface<IAgentInfoService>();
            var regionService = webInterface.Registry.RequestModuleInterface<IGridService>();

            // get all users
            var users = accountService.GetUserAccounts(null, "*", null, null);

            if (users.Count == 0) {
                usersList.Add(new Dictionary<string, object> {
                        { "UserName", translator.GetTranslatedString ("NoDetailsText") },
                        { "UserID", "" },
                        { "UserType", "" },
                        { "UserPictureURL", ""},
                        { "UserRegion", ""},
                        { "Position", ""},
                        { "UserDisplayName", "" },
                        { "IsOnline", ""}
                    });
            } else {

                noDetails = "";
                var nopicUrl = "../static/icons/no_avatar.jpg";

                foreach (var user in users) {
                    //if (user.PrincipleID == adminuser.PrincipalID)
                    //    continue;

                    if (Utilities.IsSystemUser(user.PrincipalID))
                        continue;

                    var picUrl = nopicUrl;
                    var userType = "Resident";
                    var userHome = "";
                    var userRegion = "";
                    var userDisplayName = "";
                    var userPosition = "";
                    bool isOnline = false;

                    var profile = profileservice.GetUserProfile(user.PrincipalID);
                    if (profile != null)
                    {
                        userType = profile.MembershipGroup == "" ? "Resident" : profile.MembershipGroup;
                        userDisplayName = profile.DisplayName;
                        if (userDisplayName != "")
                            userDisplayName = "(" + userDisplayName + ")";

                        if (webhttpService != null && profile.Image != UUID.Zero)
                            picUrl = webhttpService.GetTextureURL(profile.Image);
                    }

                    if (agentInfoService != null) {
                        var userInfo = agentInfoService.GetUserInfo(user.PrincipalID.ToString());
                        if (userInfo != null)
                        {
                            isOnline = userInfo.IsOnline;
                            UUID userRegionID = userInfo.CurrentRegionID;
                            UUID userHomeRegionID = userInfo.HomeRegionID;

                            if (isOnline)
                            {
                                Vector3 position = userInfo.CurrentPosition;
                                userPosition = Math.Truncate(position.X).ToString() + ", " + Math.Truncate(position.Y).ToString();
                            }
                            if (regionService != null)
                            {
                                var hregion = regionService.GetRegionByUUID(null, userHomeRegionID);
                                if (hregion != null)
                                    userHome = "(H) " + hregion.RegionName;
                                var cregion = regionService.GetRegionByUUID(null, userRegionID);
                                if (cregion != null)
                                    userRegion = cregion.RegionName;
                            }
                        }
                    }
                    usersList.Add(new Dictionary<string, object> {
                        { "UserName", user.Name },
                        { "UserID", user.PrincipalID },
                        { "UserType", userType },
                        { "UserPictureURL", picUrl},
                        { "UserRegion", isOnline ? userRegion : userHome },
                        { "Position", userPosition.ToString()},
                        { "UserDisplayName", userDisplayName },
                        { "IsOnline", isOnline ? "Yes" : "No" }

                    });
                }
            }

            vars.Add("CanEdit", true);      // maybe not needed? - possible look only access??
            vars.Add("NoDetailsText", noDetails);
            vars.Add("UsersList", usersList);
            vars.Add("TotalUserCountText", translator.GetTranslatedString("TotalUserCount"));
            vars.Add("TotalUsersCount", usersList.Count - 1);
            vars.Add("UserNameText", translator.GetTranslatedString("UserNameText"));
            vars.Add("UserHomeRegionText", translator.GetTranslatedString("UserHomeRegionText"));
            vars.Add("UserTypeText", translator.GetTranslatedString("UserTypeText"));
            vars.Add("LocationText", translator.GetTranslatedString("Location"));
            vars.Add("RegionText", translator.GetTranslatedString("Region"));
            vars.Add("OnlineText", translator.GetTranslatedString("Online"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text) {
            text = "";
            return false;
        }
    }
}
