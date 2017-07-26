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
using WhiteCore.Framework.Servers.HttpServer.Implementation;

namespace WhiteCore.Modules.Web
{
    public class IndexMain : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/index.html",
                               "html/js/menu.js"
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

            #region Find pages

            List<Dictionary<string, object>> pages = new List<Dictionary<string, object>>();

            var settings = webInterface.GetWebUISettings();
            var rootPage = webInterface.GetGridPages();

            rootPage.Children.Sort((a, b) => a.MenuPosition.CompareTo(b.MenuPosition));

            foreach (GridPage page in rootPage.Children)
            {
                if (page.LoggedOutRequired && Authenticator.CheckAuthentication(httpRequest))
                    continue;
                if (page.LoggedInRequired && !Authenticator.CheckAuthentication(httpRequest))
                    continue;
                if (page.AdminRequired && !Authenticator.CheckAdminAuthentication(httpRequest, page.AdminLevelRequired))
                    continue;

                List<Dictionary<string, object>> childPages = new List<Dictionary<string, object>>();
                page.Children.Sort((a, b) => a.MenuPosition.CompareTo(b.MenuPosition));
                foreach (GridPage childPage in page.Children)
                {
                    if (childPage.LoggedOutRequired && Authenticator.CheckAuthentication(httpRequest))
                        continue;
                    if (childPage.LoggedInRequired && !Authenticator.CheckAuthentication(httpRequest))
                        continue;
                    if (childPage.AdminRequired &&
                        !Authenticator.CheckAdminAuthentication(httpRequest, childPage.AdminLevelRequired))
                        continue;

                    childPages.Add(new Dictionary<string, object>
                                       {
                                           {"ChildMenuItemID", childPage.MenuID},
                                           {"ChildShowInMenu", childPage.ShowInMenu},
                                           {"ChildMenuItemLocation", childPage.Location},
                                           {
                                               "ChildMenuItemTitleHelp",
                                               GetTranslatedString(translator, childPage.MenuToolTip, childPage, true)
                                           },
                                           {
                                               "ChildMenuItemTitle",
                                               GetTranslatedString(translator, childPage.MenuTitle, childPage, false)
                                           }
                                       });

                    //Add one for menu.js
                    pages.Add(new Dictionary<string, object>
                                  {
                                      {"MenuItemID", childPage.MenuID},
                                      {"ShowInMenu", false},
                                      {"MenuItemLocation", childPage.Location}
                                  });
                }

                pages.Add(new Dictionary<string, object>
                              {
                                  {"MenuItemID", page.MenuID},
                                  {"ShowInMenu", page.ShowInMenu},
                                  {"HasChildren", page.Children.Count > 0},
                                  {"ChildrenMenuItems", childPages},
                                  {"MenuItemLocation", page.Location},
                                  {"MenuItemTitleHelp", GetTranslatedString(translator, page.MenuToolTip, page, true)},
                        {"MenuItemTitle", GetTranslatedString(translator, page.MenuTitle, page, false)},
                        {"MenuItemToolTip", GetTranslatedString(translator, page.MenuToolTip, page, true)}
                              });
            }
            vars.Add("MenuItems", pages);

            #endregion

            // Tooltips Urls
            vars.Add("TooltipsWelcomeScreen", translator.GetTranslatedString("TooltipsWelcomeScreen"));
            vars.Add("TooltipsWorldMap", translator.GetTranslatedString("TooltipsWorldMap"));


            // Index Page
            vars.Add("HomeText", translator.GetTranslatedString("HomeText"));
            vars.Add("HomeTextWelcome", translator.GetTranslatedString("HomeTextWelcome"));
            vars.Add("HomeTextTips", translator.GetTranslatedString("HomeTextTips"));
            vars.Add("WelcomeScreen", translator.GetTranslatedString("WelcomeScreen"));
            vars.Add("WelcomeToText", translator.GetTranslatedString("WelcomeToText"));

            if (PagesMigrator.RequiresUpdate() &&
                PagesMigrator.CheckWhetherIgnoredVersionUpdate(settings.LastPagesVersionUpdateIgnored))
                vars.Add("PagesUpdateRequired",
                         translator.GetTranslatedString("Pages") + " " +
                         translator.GetTranslatedString("DefaultsUpdated"));
            else
                vars.Add("PagesUpdateRequired", "");
            if (SettingsMigrator.RequiresUpdate() &&
                SettingsMigrator.CheckWhetherIgnoredVersionUpdate(settings.LastSettingsVersionUpdateIgnored))
                vars.Add("SettingsUpdateRequired",
                         translator.GetTranslatedString("Settings") + " " +
                         translator.GetTranslatedString("DefaultsUpdated"));
            else
                vars.Add("SettingsUpdateRequired", "");

            // user news inclusion
            if (settings.LocalFrontPage == "") {
                vars.Add ("LocalPage", false);
                vars.Add ("LocalFrontPage", "");
            } else {
                vars.Add ("LocalPage", true);
                vars.Add ("LocalFrontPage", settings.LocalFrontPage);
            }

            // Language Switcher
            vars.Add ("Languages", webInterface.AvailableLanguages());
            vars.Add("ShowLanguageTranslatorBar", !settings.HideLanguageTranslatorBar);

            vars.Add("Maintenance", false);
            vars.Add("NoMaintenance", true);
            return vars;
        }

        string GetTranslatedString(ITranslator translator, string name, GridPage page, bool isTooltip)
        {
            string retVal = translator.GetTranslatedString(name);
            if (retVal == "UNKNOWN CHARACTER")
                return isTooltip ? page.MenuToolTip : page.MenuTitle;
            return retVal;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
