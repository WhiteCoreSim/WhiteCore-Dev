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
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;

namespace WhiteCore.Modules.Web
{
    public class UserIndexMain : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/userindex.html",
                               "html/js/usermenu.js"
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
            response = null;
            var vars = new Dictionary<string, object> ();
            IWebHttpTextureService webhttpService =
	            webInterface.Registry.RequestModuleInterface<IWebHttpTextureService> ();

            #region Find pages

            var IsAdmin = Authenticator.CheckAdminAuthentication (httpRequest);

            var settings = webInterface.GetWebUISettings ();
            var userPage = webInterface.GetUserPages ();
            var userTopPage = webInterface.GetUserTopPages ();

            var mainmenu = webInterface.BuildPageMenus (userTopPage, httpRequest, translator);
            vars.Add ("MenuItems", mainmenu);

            var usermenu = webInterface.BuildPageMenus (userPage, httpRequest, translator);

            if (IsAdmin) {
                var adminPage = webInterface.GetAdminPages ();
                var adminmenu = webInterface.BuildPageMenus (adminPage, httpRequest, translator);
                usermenu.AddRange (adminmenu);
            }
            vars.Add ("UserMenuItems", usermenu);

            #endregion

            string picUrl = "../images/icons/no_avatar.jpg";
            UserAccount account = Authenticator.GetAuthentication (httpRequest);
            if (account == null)
                vars.Add ("UserName", "Unknown??");
            else {
                vars.Add ("UserName", account.Name);
                IUserProfileInfo profile = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ().
                GetUserProfile (account.PrincipalID);
                if (profile != null) {
                    vars.Add ("UserType", profile.MembershipGroup == "" ? "Resident" : profile.MembershipGroup);
                    if (webhttpService != null && profile.Image != UUID.Zero)
                        picUrl = webhttpService.GetTextureURL (profile.Image);
                } else {
                    vars.Add ("UserType", translator.GetTranslatedString ("Guest"));
                }
            }
            vars.Add ("UserAvatarURL", picUrl);

            // Tooltips Urls
            vars.Add ("TooltipsWelcomeScreen", translator.GetTranslatedString ("TooltipsWelcomeScreen"));
            vars.Add ("TooltipsWorldMap", translator.GetTranslatedString ("TooltipsWorldMap"));


            // Index Page
            vars.Add ("HomeText", translator.GetTranslatedString ("HomeText"));
            vars.Add ("HomeTextWelcome", translator.GetTranslatedString ("HomeTextWelcome"));
            vars.Add ("HomeTextTips", translator.GetTranslatedString ("HomeTextTips"));
            vars.Add ("WelcomeScreen", translator.GetTranslatedString ("WelcomeScreen"));
            vars.Add ("WelcomeToText", translator.GetTranslatedString ("WelcomeToText"));

            if (PagesMigrator.RequiresUpdate () &&
                PagesMigrator.CheckWhetherIgnoredVersionUpdate (settings.LastPagesVersionUpdateIgnored))
                vars.Add ("PagesUpdateRequired",
                         translator.GetTranslatedString ("Pages") + " " +
                         translator.GetTranslatedString ("DefaultsUpdated"));
            else
                vars.Add ("PagesUpdateRequired", "");
            if (SettingsMigrator.RequiresUpdate () &&
                SettingsMigrator.CheckWhetherIgnoredVersionUpdate (settings.LastSettingsVersionUpdateIgnored))
                vars.Add ("SettingsUpdateRequired",
                         translator.GetTranslatedString ("Settings") + " " +
                         translator.GetTranslatedString ("DefaultsUpdated"));
            else
                vars.Add ("SettingsUpdateRequired", "");

            // user news inclusion
            if (settings.LocalFrontPage == "") {
                vars.Add ("LocalPage", false);
                vars.Add ("LocalFrontPage", "");
            } else {
                vars.Add ("LocalPage", true);
                vars.Add ("LocalFrontPage", settings.LocalFrontPage);
            }

            // Language Switcher
            //vars.Add ("Languages", webInterface.AvailableLanguages ());
            //vars.Add ("ShowLanguageTranslatorBar", !settings.HideLanguageTranslatorBar);

            vars.Add ("Maintenance", false);
            vars.Add ("NoMaintenance", true);
            return vars;
        }

        /*string GetTranslatedString (ITranslator translator, string name, GridPage page, bool isTooltip)
        {
            string retVal = translator.GetTranslatedString (name);
            if (retVal == "UNKNOWN CHARACTER")
                return isTooltip ? page.MenuToolTip : page.MenuTitle;
            return retVal;
        } */

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
