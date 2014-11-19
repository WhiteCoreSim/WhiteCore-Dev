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
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using OpenMetaverse;
using System.Collections.Generic;

namespace WhiteCore.Modules.Web
{
    public class PageManagerPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/admin/page_manager.html"
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

            #region Find pages

            List<Dictionary<string, object>> pages = new List<Dictionary<string, object>>();

            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();
            GridPage rootPage = generics.GetGeneric<GridPage>(UUID.Zero, "WebPages", "Root");
            rootPage.Children.Sort((a, b) => a.MenuPosition.CompareTo(b.MenuPosition));
            List<GridPage> allPages = new List<GridPage>(rootPage.Children);
            foreach (GridPage page in rootPage.Children)
                allPages.AddRange(page.Children);
            allPages.RemoveAll((a) => !a.ShowInMenu);

            string MenuItem = requestParameters.ContainsKey("MenuItem")
                                  ? requestParameters["MenuItem"].ToString()
                                  : "";
            foreach (GridPage page in allPages)
            {
                pages.Add(new Dictionary<string, object>
                              {
                                  {"Value", page.Location},
                                  {"Name", page.Location},
                                  {
                                      "PageSelected", MenuItem == page.Location
                                                          ? "selected=\"selected\""
                                                          : ""
                                  }
                              });
            }
            vars.Add("PagesList", pages);

            #endregion

            if (requestParameters.ContainsKey("DeleteItem"))
            {
                rootPage.RemovePageByLocation(MenuItem, null);
                generics.AddGeneric(UUID.Zero, "WebPages", "Root", rootPage.ToOSD());
                response = "<h3>Successfully updated menu</h3>" +
                           "<script language=\"javascript\">" +
                           "setTimeout(function() {window.location.href = \"index.html\";}, 0);" +
                           "</script>";
                return null;
            }
            if (requestParameters.ContainsKey("AddItem"))
            {
                //generics.AddGeneric(UUID.Zero, "WebPages", "Root", rootPage.ToOSD());
                vars.Add("EdittingPageID", -2);
                vars.Add("DisplayEdit", true);
            }
            if (requestParameters.ContainsKey("SelectItem"))
            {
                GridPage page = rootPage.GetPageByLocation(MenuItem);

                vars.Add("PageTitle", page.MenuTitle);
                vars.Add("PageTooltip", page.MenuToolTip);
                vars.Add("PageID", page.MenuID);
                vars.Add("PagePosition", page.MenuPosition);
                vars.Add("PageLocation", page.Location);
                vars.Add("EdittingPageID", page.MenuID);
                vars.Add("RequiresLoginYes", page.LoggedInRequired ? "selected=\"selected\"" : "");
                vars.Add("RequiresLoginNo", !page.LoggedInRequired ? "selected=\"selected\"" : "");
                vars.Add("RequiresLogoutYes", page.LoggedOutRequired ? "selected=\"selected\"" : "");
                vars.Add("RequiresLogoutNo", !page.LoggedOutRequired ? "selected=\"selected\"" : "");
                vars.Add("RequiresAdminYes", page.AdminRequired ? "selected=\"selected\"" : "");
                vars.Add("RequiresAdminNo", !page.AdminRequired ? "selected=\"selected\"" : "");
                vars.Add("RequiresAdminLevel", page.AdminLevelRequired);
                vars.Add("DisplayInMenuYes", page.ShowInMenu ? "selected=\"selected\"" : "");
                vars.Add("DisplayInMenuNo", !page.ShowInMenu ? "selected=\"selected\"" : "");
                vars.Add("DisplayEdit", true);

                pages = new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                    {
                                        {"Value", "Top Level"},
                                        {"Name", translator.GetTranslatedString("TopLevel")},
                                        {"PageSelected", ""}
                                    }
                            };
                GridPage parent = rootPage.GetParent(page);
                foreach (GridPage p in allPages)
                {
                    pages.Add(new Dictionary<string, object>
                                  {
                                      {"Value", p.Location},
                                      {"Name", p.Location},
                                      {
                                          "PageSelected", parent.Location == p.Location
                                                              ? "selected=\"selected\""
                                                              : ""
                                      }
                                  });
                }
                vars.Add("ParentPagesList", pages);
            }

            if (requestParameters.ContainsKey("AddItem"))
            {
                vars.Add("PageTitle", "");
                vars.Add("PageTooltip", "");
                vars.Add("PageID", "");
                vars.Add("PagePosition", "");
                vars.Add("PageLocation", "");
                if (!vars.ContainsKey("EdittingPageID"))
                    vars.Add("EdittingPageID", "");
                vars.Add("RequiresLoginYes", "");
                vars.Add("RequiresLoginNo", "");
                vars.Add("RequiresLogoutYes", "");
                vars.Add("RequiresLogoutNo", "");
                vars.Add("RequiresAdminYes", "");
                vars.Add("RequiresAdminNo", "");
                vars.Add("RequiresAdminLevel", "1");

                pages = new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                    {
                                        {"Value", "Top Level"},
                                        {"Name", translator.GetTranslatedString("TopLevel")},
                                        {"PageSelected", ""}
                                    }
                            };
                foreach (GridPage p in allPages)
                {
                    pages.Add(new Dictionary<string, object>
                                  {
                                      {"Value", p.Location},
                                      {"Name", p.Location},
                                      {"PageSelected", ""}
                                  });
                }
                vars.Add("ParentPagesList", pages);
            }

            if (requestParameters.ContainsKey("SaveMenuItem"))
            {
                string edittingPageID = requestParameters["EdittingPageID"].ToString();
                string PageTitle = requestParameters["PageTitle"].ToString();
                string PageTooltip = requestParameters["PageTooltip"].ToString();
                string PagePosition = requestParameters["PagePosition"].ToString();
                string PageID = requestParameters["PageID"].ToString();
                string PageLocation = requestParameters["PageLocation"].ToString();
                bool RequiresLogin = bool.Parse(requestParameters["RequiresLogin"].ToString());
                bool RequiresLogout = bool.Parse(requestParameters["RequiresLogout"].ToString());
                bool RequiresAdmin = bool.Parse(requestParameters["RequiresAdmin"].ToString());
                bool DisplayInMenu = bool.Parse(requestParameters["DisplayInMenu"].ToString());
                string ParentMenuItem = requestParameters["ParentMenuItem"].ToString();
                int RequiredAdminLevel = int.Parse(requestParameters["RequiredAdminLevel"].ToString());
                GridPage page = rootPage.GetPage(edittingPageID);
                bool add = page == null;
                if (page == null)
                    page = new GridPage {MenuID = PageLocation, ShowInMenu = true};

                page.Location = PageLocation;
                page.MenuID = PageID;
                page.MenuPosition = int.Parse(PagePosition);
                page.MenuTitle = PageTitle;
                page.MenuToolTip = PageTooltip;
                page.LoggedInRequired = RequiresLogin;
                page.LoggedOutRequired = RequiresLogout;
                page.AdminRequired = RequiresAdmin;
                page.AdminLevelRequired = RequiredAdminLevel;
                page.ShowInMenu = DisplayInMenu;

                GridPage parent = rootPage.GetPageByLocation(ParentMenuItem);

                if (parent != page)
                {
                    if (!add)
                        rootPage.RemovePage(edittingPageID, page);

                    if (parent != null)
                        parent.Children.Add(page);
                    else //Top Level
                        rootPage.Children.Add(page);

                    response = "<h3>Successfully updated menu</h3>" +
                               "<script language=\"javascript\">" +
                               "setTimeout(function() {window.location.href = \"index.html\";}, 0);" +
                               "</script>";
                }
                else
                    response = "<h3>" + translator.GetTranslatedString("CannotSetParentToChild") + "</h3>";

                generics.AddGeneric(UUID.Zero, "WebPages", "Root", rootPage.ToOSD());
                return null;
            }

            vars.Add("PageTitleText", translator.GetTranslatedString("PageTitleText"));
            vars.Add("PageTooltipText", translator.GetTranslatedString("PageTooltipText"));
            vars.Add("PagePositionText", translator.GetTranslatedString("PagePositionText"));
            vars.Add("PageIDText", translator.GetTranslatedString("PageIDText"));
            vars.Add("PageLocationText", translator.GetTranslatedString("PageLocationText"));
            vars.Add("SaveMenuItemChanges", translator.GetTranslatedString("SaveMenuItemChanges"));
            vars.Add("RequiresLoginText", translator.GetTranslatedString("RequiresLoginText"));
            vars.Add("RequiresLogoutText", translator.GetTranslatedString("RequiresLogoutText"));
            vars.Add("RequiresAdminText", translator.GetTranslatedString("RequiresAdminText"));
            vars.Add("RequiresAdminLevelText", translator.GetTranslatedString("RequiresAdminLevelText"));
            vars.Add("DisplayInMenu", translator.GetTranslatedString("DisplayInMenu"));
            vars.Add("SelectItem", translator.GetTranslatedString("SelectItem"));
            vars.Add("DeleteItem", translator.GetTranslatedString("DeleteItem"));
            vars.Add("AddItem", translator.GetTranslatedString("AddItem"));
            vars.Add("PageManager", translator.GetTranslatedString("PageManager"));
            vars.Add("ParentText", translator.GetTranslatedString("ParentText"));
            vars.Add("Yes", translator.GetTranslatedString("Yes"));
            vars.Add("No", translator.GetTranslatedString("No"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}