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
using OpenMetaverse;
using System.Collections.Generic;

namespace WhiteCore.Modules.Web
{
    internal class PagesMigrator
    {
        public static readonly string Schema = "WebPages";
        private static GridPage _rootPage;
        public static readonly uint CurrentVersion = 10;

        private static void InitializeDefaults()
        {
            _rootPage = new GridPage();

            // home
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "home",
                Location = "home.html",
                MenuPosition = 0,
                MenuTitle = "MenuHome",
                MenuToolTip = "TooltipsMenuHome"
            });

            // news
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "news",
                Location = "news_list.html",
                MenuPosition = 1,
                MenuTitle = "MenuNews",
                MenuToolTip = "TooltipsMenuNews"
            });

            // world map
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "world",
                Location = "world.html",
                MenuPosition = 2,
                MenuTitle = "MenuWorld",
                MenuToolTip = "TooltipsMenuWorld",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        MenuID = "world",
                        Location = "world.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuWorldMap",
                        MenuToolTip = "TooltipsMenuWorldMap"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        MenuID = "region_search",
                        Location = "region_search.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuRegionSearch",
                        MenuToolTip = "TooltipsMenuRegionSearch"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        MenuID = "region_list",
                        Location = "region_list.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuRegion",
                        MenuToolTip = "TooltipsMenuRegion"
                    }
                }
            });

            // users
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "users",
                Location = "user_profile.html",
                MenuPosition = 3,
                MenuTitle = "MenuUser",
                MenuToolTip = "TooltipsMenuUser",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "online_users",
                        Location = "online_users.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuOnlineUsers",
                        MenuToolTip = "TooltipsMenuOnlineUsers"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_search",
                        Location = "user_search.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuUserSearch",
                        MenuToolTip = "TooltipsMenuUserSearch"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "purchases",
                        Location = "user_purchases.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuMyPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "Transactions",
                        Location = "user_transactions.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuMyTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "change_user_information",
                        Location = "change_user_information.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuChangeUserInformation",
                        MenuToolTip = "TooltipsMenuChangeUserInformation"
                    }
                }
            });

            // help
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "help",
                Location = "help.html",
                MenuPosition = 5,
                MenuTitle = "MenuHelp",
                MenuToolTip = "TooltipsMenuHelp",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        MenuID = "help",
                        Location = "help.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuViewerHelp",
                        MenuToolTip = "TooltipsMenuViewerHelp"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        MenuID = "chat",
                        Location = "chat.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuChat",
                        MenuToolTip = "TooltipsMenuChat"
                    }
                }
            });

            // register
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "logout",
                Location = "logout.html",
                MenuPosition = 6,
                MenuTitle = "MenuLogout",
                MenuToolTip = "TooltipsMenuLogout"
            });

            // login
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedOutRequired = true,
                MenuID = "register",
                Location = "register.html",
                MenuPosition = 6,
                MenuTitle = "MenuRegister",
                MenuToolTip = "TooltipsMenuRegister"
            });

            // Logout
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedOutRequired = true,
                MenuID = "login",
                Location = "login.html",
                MenuPosition = 7,
                MenuTitle = "MenuLogin",
                MenuToolTip = "TooltipsMenuLogin",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        MenuID = "forgot_pass",
                        Location = "forgot_pass.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuForgotPass",
                        MenuToolTip = "TooltipsMenuForgotPass"
                    }
                }
            });

            // Management
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                AdminRequired = true,
                MenuID = "manager",
                Location = "admin/manager.html",
                MenuPosition = 8,
                MenuTitle = "MenuManager",
                MenuToolTip = "TooltipsMenuManager",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "news_manager",
                        Location = "admin/news_manager.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuNewsManager",
                        MenuToolTip = "TooltipsMenuNewsManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "user_search",
                        Location = "user_search.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuUserManager",
                        MenuToolTip = "TooltipsMenuUserManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "new_user",
                        Location = "register.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuRegister",
                        MenuToolTip = "TooltipsMenuRegister"
                    },
                    new GridPage {
                        ShowInMenu = false,
                        AdminRequired = true,
                        MenuID = "new_region",
                        Location = "admin/region_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuRegionManager",
                        MenuToolTip = "TooltipsMenuRegionManager"
                    },
                    new GridPage {
                        ShowInMenu = false,
                        AdminRequired = true,
                        MenuID = "sim_console",
                        Location = "admin/sim_console.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuManagerSimConsole",
                        MenuToolTip = "TooltipsMenuManagerSimConsole"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "Purchases_admin",
                        Location = "admin/purchases.html",
                        MenuPosition = 5,
                        MenuTitle = "MenuPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "Transactions_admin",
                        Location = "admin/transactions.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "Statistics",
                        Location = "admin/statistics.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuStatistics",
                        MenuToolTip = "TooltipsMenuStatistics"
                    }
                }
            });

            // admin settings
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                AdminRequired = true,
                MenuID = "manager",
                Location = "admin/settings.html",
                MenuPosition = 9,
                MenuTitle = "MenuSettings",
                MenuToolTip = "TooltipsMenuSettingsManager",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "factory_reset",
                        Location = "admin/factory_reset.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuFactoryReset",
                        MenuToolTip = "TooltipsMenuFactoryReset"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "page_manager",
                        Location = "admin/page_manager.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuPageManager",
                        MenuToolTip = "TooltipsMenuPageManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "settings_manager",
                        Location = "admin/settings_manager.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuSettingsManager",
                        MenuToolTip = "TooltipsMenuSettingsManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "gridsettings_manager",
                        Location = "admin/gridsettings_manager.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuGridSettings",
                        MenuToolTip = "TooltipsMenuGridSettings"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "welcomescreen_manager",
                        Location = "admin/welcomescreen_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuWelcomeScreenManager",
                        MenuToolTip = "TooltipsMenuWelcomeScreenManager"
                                                                
                    }
                }
            });


            _rootPage.Children.Add(new GridPage
                                       {
                                           MenuID = "add_news",
                                           ShowInMenu = false,
                                           AdminRequired = true,
                                           MenuPosition = 8,
                                           Location = "admin/add_news.html"
                                       });
            _rootPage.Children.Add(new GridPage
                                       {
                                           MenuID = "edit_news",
                                           ShowInMenu = false,
                                           AdminRequired = true,
                                           MenuPosition = 8,
                                           Location = "admin/edit_news.html"
                                       });
            _rootPage.Children.Add(new GridPage
                                       {
                                           MenuID = "edit_user",
                                           ShowInMenu = false,
                                           AdminRequired = true,
                                           MenuPosition = 8,
                                           Location = "admin/edit_user.html"
                                       });

            _rootPage.Children.Add(new GridPage
                                       {
                                           ShowInMenu = false,
                                           MenuID = "news_info",
                                           Location = "news.html"
                                       });

            //Things added, but not used
            /*pages.Add(new Dictionary<string, object> { { "MenuItemID", "tweets" }, 
                { "ShowInMenu", false },
                { "MenuItemLocation", "tweets.html" }, 
                { "MenuItemTitleHelp", translator.GetTranslatedString("TooltipsMenuTweets") },
                { "MenuItemTitle", translator.GetTranslatedString("MenuTweets") } });

            pages.Add(new Dictionary<string, object> { { "MenuItemID", "agent_info" }, 
                { "ShowInMenu", false },
                { "MenuItemLocation", "agent_info.html" }, 
                { "MenuItemTitleHelp", translator.GetTranslatedString("TooltipsMenuAgentInfo") },
                { "MenuItemTitle", translator.GetTranslatedString("MenuAgentInfo") } });

            pages.Add(new Dictionary<string, object> { { "MenuItemID", "region_info" }, 
                { "ShowInMenu", false },
                { "MenuItemLocation", "region_info.html" }, 
                { "MenuItemTitleHelp", translator.GetTranslatedString("TooltipsMenuRegionInfo") },
                { "MenuItemTitle", translator.GetTranslatedString("MenuRegionInfo") } });
            pages.Add(new Dictionary<string, object> { { "MenuItemID", "add_news" }, 
                { "ShowInMenu", false },
                { "MenuItemLocation", "admin/add_news.html" }, 
                { "MenuItemTitleHelp", translator.GetTranslatedString("TooltipsMenuNewsManager") },
                { "MenuItemTitle", translator.GetTranslatedString("MenuNewsManager") } });
            pages.Add(new Dictionary<string, object> { { "MenuItemID", "edit_news" }, 
                { "ShowInMenu", false },
                { "MenuItemLocation", "admin/edit_news.html" }, 
                { "MenuItemTitleHelp", translator.GetTranslatedString("TooltipsMenuNewsManager") },
                { "MenuItemTitle", translator.GetTranslatedString("MenuNewsManager") } });*/
        }

        public static bool RequiresUpdate()
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            OSDWrapper version = generics.GetGeneric<OSDWrapper>(UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger() < CurrentVersion;
        }

        public static uint GetVersion()
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            OSDWrapper version = generics.GetGeneric<OSDWrapper>(UUID.Zero, Schema + "Version", "");
            return version == null ? 0 : (uint) version.Info.AsInteger();
        }

        public static bool RequiresInitialUpdate()
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            OSDWrapper version = generics.GetGeneric<OSDWrapper>(UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger() < 1;
        }

        public static void ResetToDefaults()
        {
            InitializeDefaults();
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            //Remove all pages
            generics.RemoveGeneric(UUID.Zero, Schema);

            generics.AddGeneric(UUID.Zero, Schema, "Root", _rootPage.ToOSD());
            generics.AddGeneric(UUID.Zero, Schema + "Version", "", new OSDWrapper {Info = CurrentVersion}.ToOSD());
        }

        public static bool CheckWhetherIgnoredVersionUpdate(uint version)
        {
            return version != PagesMigrator.CurrentVersion;
        }
    }
}