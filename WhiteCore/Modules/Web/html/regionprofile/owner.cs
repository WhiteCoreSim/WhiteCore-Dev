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

using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using OpenMetaverse;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WhiteCore.Modules.Web
{
    public class RegionOwnerPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                {
                    "html/regionprofile/owner.html"
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

            string username = filename.Split('/').LastOrDefault();
            UserAccount account = null;
            if (httpRequest.Query.ContainsKey("userid"))
            {
                string userid = httpRequest.Query["userid"].ToString();

                account = webInterface.Registry.RequestModuleInterface<IUserAccountService>().
                                       GetUserAccount(null, UUID.Parse(userid));
            }
            else if (httpRequest.Query.ContainsKey("name"))
            {
                string name = httpRequest.Query.ContainsKey("name") ? httpRequest.Query["name"].ToString() : username;
                name = name.Replace('.', ' ');
                name = name.Replace("%20", " ");
                account = webInterface.Registry.RequestModuleInterface<IUserAccountService>().
                                       GetUserAccount(null, name);
            }
            else
            {
                username = username.Replace("%20", " ");
                webInterface.Redirect(httpResponse, "/regionprofile/?name=" + username);
                return vars;
            }

            if (account == null)
                return vars;

            var libraryOwner = new UUID(Constants.LibraryOwner);
            var realestateOwner = new UUID(Constants.RealEstateOwnerUUID);

            if ( (account.PrincipalID == libraryOwner) || (account.PrincipalID == realestateOwner) )
                return vars;

            vars.Add("UserName", account.Name);
            //  TODO: User Profile inworld shows this as the standard mm/dd/yyyy
            //  Do we want this to be localised into the users Localisation or keep it as standard ?
            //
            //  vars.Add("UserBorn", Culture.LocaleDate(Util.ToDateTime(account.Created)));
            vars.Add("UserBorn", Util.ToDateTime(account.Created).ToShortDateString());

            IUserProfileInfo profile = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>().
                                              GetUserProfile(account.PrincipalID);
            vars.Add("UserType", profile.MembershipGroup == "" ? "Resident" : profile.MembershipGroup);
            if (profile != null)
            {
                if (profile.Partner != UUID.Zero)
                {
                    account = webInterface.Registry.RequestModuleInterface<IUserAccountService>().
                                           GetUserAccount(null, profile.Partner);
                    vars.Add("UserPartner", account.Name);
                }
                else
                    vars.Add("UserPartner", "No partner");
                vars.Add("UserAboutMe", profile.AboutText == "" ? "Nothing here" : profile.AboutText);
                string url = "../images/icons/no_picture.jpg";
                IWebHttpTextureService webhttpService =
                    webInterface.Registry.RequestModuleInterface<IWebHttpTextureService>();
                if (webhttpService != null && profile.Image != UUID.Zero)
                    url = webhttpService.GetTextureURL(profile.Image);
                vars.Add("UserPictureURL", url);
            }
            UserAccount ourAccount = Authenticator.GetAuthentication(httpRequest);
            if (ourAccount != null)
            {
                IFriendsService friendsService = webInterface.Registry.RequestModuleInterface<IFriendsService>();
                var friends = friendsService.GetFriends(account.PrincipalID);
                UUID friendID = UUID.Zero;
                if (friends.Any(f => UUID.TryParse(f.Friend, out friendID) && friendID == ourAccount.PrincipalID))
                {
                    IAgentInfoService agentInfoService =
                        webInterface.Registry.RequestModuleInterface<IAgentInfoService>();
                    IGridService gridService = webInterface.Registry.RequestModuleInterface<IGridService>();
                    UserInfo ourInfo = agentInfoService.GetUserInfo(account.PrincipalID.ToString());
                    if (ourInfo != null && ourInfo.IsOnline)
                        vars.Add("OnlineLocation", gridService.GetRegionByUUID(null, ourInfo.CurrentRegionID).RegionName);
                    vars.Add("UserIsOnline", ourInfo != null && ourInfo.IsOnline);
                    vars.Add("IsOnline",
                             ourInfo != null && ourInfo.IsOnline
                                 ? translator.GetTranslatedString("Online")
                                 : translator.GetTranslatedString("Offline"));
                }
                else
                {
                    vars.Add("OnlineLocation", "");
                    vars.Add("UserIsOnline", false);
                    vars.Add("IsOnline", translator.GetTranslatedString("Offline"));
                }
            }
            else
            {
                vars.Add("OnlineLocation", "");
                vars.Add("UserIsOnline", false);
                vars.Add("IsOnline", translator.GetTranslatedString("Offline"));
            }

            // Menu Region
            vars.Add("MenuRegionTitle", translator.GetTranslatedString("MenuRegionTitle"));
            vars.Add("MenuParcelTitle", translator.GetTranslatedString("MenuParcelTitle"));
            vars.Add("MenuOwnerTitle", translator.GetTranslatedString("MenuOwnerTitle"));

            vars.Add("UserProfileFor", translator.GetTranslatedString("UserProfileFor"));
            vars.Add("ResidentSince", translator.GetTranslatedString("ResidentSince"));
            vars.Add("AccountType", translator.GetTranslatedString("AccountType"));
            vars.Add("PartnersName", translator.GetTranslatedString("PartnersName"));
            vars.Add("AboutMe", translator.GetTranslatedString("AboutMe"));
            vars.Add("IsOnlineText", translator.GetTranslatedString("IsOnlineText"));
            vars.Add("OnlineLocationText", translator.GetTranslatedString("OnlineLocationText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            httpResponse.ContentType = "text/html";
            text = File.ReadAllText("html/regionprofile/index.html");
            return true;
        }
    }
}