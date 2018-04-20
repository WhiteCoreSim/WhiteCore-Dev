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

namespace WhiteCore.Modules.Web
{
    class PagesMigrator
    {
        static GridPage _rootPage;
        static GridPage _userPage;
        static GridPage _userTopPage;
        static GridPage _adminPage;
        public static readonly string Schema = "WebPages";
        public static readonly uint CurrentVersion = 16;

        static void InitializeDefaults ()
        {
            _rootPage = new GridPage ();
            _userPage = new GridPage ();
            _userTopPage = new GridPage ();
            _adminPage = new GridPage ();


/* original _rootPage options
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
                        MenuID = "region_map",
                        Location = "region_map.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuRegionMap",
                        MenuToolTip = "TooltipsMenuRegionMap"
                    }, 
                    new GridPage {
                        ShowInMenu = true,
                        MenuID = "region_list",
                        Location = "region_list.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuRegion",
                        MenuToolTip = "TooltipsMenuRegion"
                    }
                }
            });

            // events
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = false,
                MenuID = "events",
                Location = "events/events.html",
                MenuPosition = 3,
                MenuTitle = "MenuEvents",
                MenuToolTip = "TooltipsMenuEvents"
            });

            // classifieds
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = false,
                MenuID = "classifieds",
                Location = "classifieds.html",
                MenuPosition = 4,
                MenuTitle = "MenuClassifieds",
                MenuToolTip = "TooltipsMenuClassifieds"
            });

            // users
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "users",
                Location = "user_profile.html",
                MenuPosition = 5,
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
                        MenuID = "user_purchases",
                        Location = "user/user_purchases.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuMyPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_transactions",
                        Location = "user/user_transactions.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuMyTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_events",
                        Location = "user/events.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuMyEvents",
                        MenuToolTip = "TooltipsMenuEvents"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_regionmanager",
                        Location = "user/region_manager.html",
                        MenuPosition = 5,
                        MenuTitle = "MenuRegionManager",
                        MenuToolTip = "TooltipsMenuRegionManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_estatemanager",
                        Location = "user/estate_manager.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuEstateManager",
                        MenuToolTip = "TooltipsMenuEstateManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "change_user_information",
                        Location = "change_user_information.html",
                        MenuPosition = 7,
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
                MenuPosition = 6,
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


            // Management
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                AdminRequired = true,
                MenuID = "manager",
                Location = "admin/manager.html",
                MenuPosition = 7,
                MenuTitle = "MenuManager",
                MenuToolTip = "TooltipsMenuManager",
                Children = new List<GridPage> {
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
                        MenuID = "news_manager",
                        Location = "admin/news_manager.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuNewsManager",
                        MenuToolTip = "TooltipsMenuNewsManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "region_manager",
                        Location = "admin/region_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuRegionManager",
                        MenuToolTip = "TooltipsMenuRegionManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "estate_manager",
                        Location = "admin/estate_manager.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuEstateManager",
                        MenuToolTip = "TooltipsMenuEstateManager"
                    },
                    new GridPage {
                        ShowInMenu = false,
                        AdminRequired = true,
                        MenuID = "sim_console",
                        Location = "admin/sim_console.html",
                        MenuPosition = 5,
                        MenuTitle = "MenuManagerSimConsole",
                        MenuToolTip = "TooltipsMenuManagerSimConsole"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin_purchases",
                        Location = "admin/purchases.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin_abuse",
                        Location = "admin/abuse_list.html",
                        MenuPosition = 7,
                        MenuTitle = "MenuAbuse",
                        MenuToolTip = "TooltipsMenuAbuse"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin_transactions",
                        Location = "admin/transactions.html",
                        MenuPosition = 8,
                        MenuTitle = "MenuTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "Statistics",
                        Location = "admin/statistics.html",
                        MenuPosition = 9,
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
                MenuPosition = 8,
                MenuTitle = "MenuSettings",
                MenuToolTip = "TooltipsMenuSettingsManager",
                Children = new List<GridPage> {
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
                        MenuID = "settings_manager",
                        Location = "admin/settings_manager.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuSettingsManager",
                        MenuToolTip = "TooltipsMenuSettingsManager"
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
                        MenuID = "welcomescreen_manager",
                        Location = "admin/welcomescreen_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuWelcomeScreenManager",
                        MenuToolTip = "TooltipsMenuWelcomeScreenManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "factory_reset",
                        Location = "admin/factory_reset.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuFactoryReset",
                        MenuToolTip = "TooltipsMenuFactoryReset"
                    }
                }
            });


            // register
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedOutRequired = true,
                MenuID = "register",
                Location = "register.html",
                MenuPosition = 9,
                MenuTitle = "MenuRegister",
                MenuToolTip = "TooltipsMenuRegister"
            });

            // Login
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedOutRequired = true,
                MenuID = "login",
                Location = "login.html",
                MenuPosition = 10,
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

            // logout
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "logout",
                Location = "logout.html",
                MenuPosition = 10,
                MenuTitle = "MenuLogout",
                MenuToolTip = "TooltipsMenuLogout"
            });


            // these are non menu, individual pages that can be called
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = false,
                MenuID = "forgotpass",
                Location = "forgot_pass.html"
            });

            _rootPage.Children.Add (new GridPage {
                ShowInMenu = false,
                MenuID = "news_info",
                Location = "news.html"
            });

            // admin
            _rootPage.Children.Add (new GridPage {
                MenuID = "add_news",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/add_news.html"
            });
            _rootPage.Children.Add (new GridPage {
                MenuID = "edit_news",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/edit_news.html"
            });
            _rootPage.Children.Add (new GridPage {
                MenuID = "edit_user",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/edit_user.html"
            });

            _rootPage.Children.Add (new GridPage {
                MenuID = "abuse_report",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/abuse_report.html"
            });

            _rootPage.Children.Add (new GridPage {
                MenuID = "region_edit",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8,
                Location = "admin/region_edit.html"
            });

            _rootPage.Children.Add (new GridPage {
                MenuID = "estate_edit",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8,
                Location = "admin/estate_edit.html"
            });

            // user
            _rootPage.Children.Add (new GridPage {
                MenuID = "add_event",
                ShowInMenu = false,
                LoggedInRequired = true,
                AdminRequired = false,
                MenuPosition = 8,
                Location = "events/add_event.html"
            });
            _rootPage.Children.Add (new GridPage {
                MenuID = "edit_event",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8,
                Location = "user/edit_event.html"
            });
            _rootPage.Children.Add (new GridPage {
                MenuID = "user_regionedit",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8,
                Location = "user/region_edit.html"
            });
            _rootPage.Children.Add (new GridPage {
                MenuID = "user_estateedit",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8,
                Location = "user/estate_edit.html"
            });

 //end of original _rootPage options 
            */

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


            // Main menu options
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
            });

            // help
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "help",
                Location = "help.html",
                MenuPosition = 3,
                MenuTitle = "MenuHelp",
                MenuToolTip = "TooltipsMenuHelp",
            });

            // Non menu options
            // register
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedOutRequired = true,
                MenuID = "register",
                Location = "register.html",
                MenuPosition = 9,
                MenuTitle = "MenuRegister",
                MenuToolTip = "TooltipsMenuRegister"
            });

            // Login
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedOutRequired = true,
                MenuID = "login",
                Location = "login.html",
                MenuPosition = 10,
                MenuTitle = "MenuLogin",
                MenuToolTip = "TooltipsMenuLogin",
            });

            // logout
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "logout",
                Location = "logout.html",
                MenuPosition = 10,
                MenuTitle = "MenuLogout",
                MenuToolTip = "TooltipsMenuLogout"
            });

            // these are non menu, individual pages that can be called
            _rootPage.Children.Add (new GridPage {
                ShowInMenu = false,
                MenuID = "forgotpass",
                Location = "forgot_pass.html",
                MenuPosition = 10,
            });

            // User pages

            // users
            _userPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "account",
                Location = "",
                MenuPosition = 1,
                MenuTitle = "MenuUser",
                MenuToolTip = "TooltipsMenuUser",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_profile",
                        Location = "user_profile.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuUserProfile",
                        MenuToolTip = "TooltipsMenuProfile"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_purchases",
                        Location = "user/user_purchases.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuMyPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_transactions",
                        Location = "user/user_transactions.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuMyTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_contact",
                        Location = "user/contactinfo.html",
                        MenuPosition = 5,
                        MenuTitle = "MenuContactInfo",
                        MenuToolTip = "TooltipsMenuContactInfo"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_email",
                        Location = "user/emailaddress.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuEmail",
                        MenuToolTip = "TooltipsMenuEmail"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_password",
                        Location = "user/password.html",
                        MenuPosition = 7,
                        MenuTitle = "MenuChangeUserInformation",
                        MenuToolTip = "TooltipsMenuChangeUserInformation"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_partners",
                        Location = "user/partners.html",
                        MenuPosition = 7,
                        MenuTitle = "MenuPartners",
                        MenuToolTip = "TooltipsMenuPartners"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "delete_account",
                        Location = "user/deleteaccount.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuDeletAccount",
                        MenuToolTip = "TooltipsMenuDeleteAccount"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "change_user_information",
                        Location = "change_user_information.html",
                        MenuPosition = 7,
                        MenuTitle = "MenuChangeUserInformation",
                        MenuToolTip = "TooltipsMenuChangeUserInformation"
                    }
                }
            });

            // events
            _userPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = false,
                MenuID = "events",
                Location = "events/events.html",
                MenuPosition = 2,
                MenuTitle = "MenuEvents",
                MenuToolTip = "TooltipsMenuEvents",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_events",
                        Location = "user/events.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuMyEvents",
                        MenuToolTip = "TooltipsMenuEvents"
                    },
                }
            });

            // shopping
            _userPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "usershopping",
                Location = "",
                MenuPosition = 3,
                MenuTitle = "MenuShopping",
                MenuToolTip = "TooltipsMenuShopping",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = false,
                        MenuID = "classifieds",
                        Location = "classifieds.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuClassifieds",
                        MenuToolTip = "TooltipsMenuClassifieds"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = false,
                        MenuID = "marketplace",
                        Location = "marketplace.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuMarketplace",
                        MenuToolTip = "TooltipsMenuMarketplace"
                    },
                }
            });

            // land management
            _userPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "userlandmanage",
                Location = "userland.html",
                MenuPosition = 4,
                MenuTitle = "MenuUserLand",
                MenuToolTip = "TooltipsMenuUserLand",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_regionmanager",
                        Location = "user/region_manager.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuRegionManager",
                        MenuToolTip = "TooltipsMenuRegionManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user_estatemanager",
                        Location = "user/estate_manager.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuEstateManager",
                        MenuToolTip = "TooltipsMenuEstateManager"
                    },
                }
            });

            _userPage.Children.Add (new GridPage {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "userfriends",
                Location = "l",
                MenuPosition = 5,
                MenuTitle = "MenuUserFriends",
                MenuToolTip = "TooltipsMenuUserFriends",
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
                }
            });

            // User - non menu pages
            _userPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "add_event",
                Location = "events/add_event.html",
                MenuPosition = 8
            });
            _userPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "edit_event",
                Location = "user/edit_event.html",
                MenuPosition = 8
            });
            _userPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "user_regionedit",
                Location = "user/region_edit.html",
                MenuPosition = 8
            });
            _userPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "user_estateedit",
                Location = "user/estate_edit.html",
                MenuPosition = 8
            });

            // logout
/*            _userPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "logout",
                Location = "logout.html",
                MenuPosition = 10,
                MenuTitle = "MenuLogout",
                MenuToolTip = "TooltipsMenuLogout"
            });
*/
            // User top menu pages
            _userTopPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "home",
                Location = "userhome.html",
                MenuPosition = 0,
                MenuTitle = "MenuHome",
                MenuToolTip = "TooltipsMenuHome"
            });

            // news
            _userTopPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "news",
                Location = "news_list.html",
                MenuPosition = 1,
                MenuTitle = "MenuNews",
                MenuToolTip = "TooltipsMenuNews"
            });

            // world map
            _userTopPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "world",
                Location = "world.html",
                MenuPosition = 2,
                MenuTitle = "MenuWorld",
                MenuToolTip = "TooltipsMenuWorld",
            });

            // help
            _userTopPage.Children.Add (new GridPage {
                ShowInMenu = true,
                MenuID = "help",
                Location = "help.html",
                MenuPosition = 3,
                MenuTitle = "MenuHelp",
                MenuToolTip = "TooltipsMenuHelp",
            });

            // Non menu options
            // logout
            _userTopPage.Children.Add (new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "logout",
                Location = "logout.html",
                MenuPosition = 10,
                MenuTitle = "MenuLogout",
                MenuToolTip = "TooltipsMenuLogout"
            });


            // Admin menu pages
            // Management
            _adminPage.Children.Add (new GridPage {
                ShowInMenu = true,
                AdminRequired = true,
                MenuID = "manager",
                Location = "admin/manager.html",
                MenuPosition = 7,
                MenuTitle = "MenuManager",
                MenuToolTip = "TooltipsMenuManager",
                Children = new List<GridPage> {
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
                        MenuID = "news_manager",
                        Location = "admin/news_manager.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuNewsManager",
                        MenuToolTip = "TooltipsMenuNewsManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "region_manager",
                        Location = "admin/region_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuRegionManager",
                        MenuToolTip = "TooltipsMenuRegionManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "estate_manager",
                        Location = "admin/estate_manager.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuEstateManager",
                        MenuToolTip = "TooltipsMenuEstateManager"
                    },
                    new GridPage {
                        ShowInMenu = false,
                        AdminRequired = true,
                        MenuID = "sim_console",
                        Location = "admin/sim_console.html",
                        MenuPosition = 5,
                        MenuTitle = "MenuManagerSimConsole",
                        MenuToolTip = "TooltipsMenuManagerSimConsole"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin_purchases",
                        Location = "admin/purchases.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin_abuse",
                        Location = "admin/abuse_list.html",
                        MenuPosition = 7,
                        MenuTitle = "MenuAbuse",
                        MenuToolTip = "TooltipsMenuAbuse"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin_transactions",
                        Location = "admin/transactions.html",
                        MenuPosition = 8,
                        MenuTitle = "MenuTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "Statistics",
                        Location = "admin/statistics.html",
                        MenuPosition = 9,
                        MenuTitle = "MenuStatistics",
                        MenuToolTip = "TooltipsMenuStatistics"
                    }
                }
            });

            // admin settings
            _adminPage.Children.Add (new GridPage {
                ShowInMenu = true,
                AdminRequired = true,
                MenuID = "settings",
                Location = "admin/settings.html",
                MenuPosition = 8,
                MenuTitle = "MenuSettings",
                MenuToolTip = "TooltipsMenuSettingsManager",
                Children = new List<GridPage> {
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
                        MenuID = "settings_manager",
                        Location = "admin/settings_manager.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuSettingsManager",
                        MenuToolTip = "TooltipsMenuSettingsManager"
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
                        MenuID = "welcomescreen_manager",
                        Location = "admin/welcomescreen_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuWelcomeScreenManager",
                        MenuToolTip = "TooltipsMenuWelcomeScreenManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "factory_reset",
                        Location = "admin/factory_reset.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuFactoryReset",
                        MenuToolTip = "TooltipsMenuFactoryReset"
                    }
                }
            });
            // Admin - non menu pages
            _adminPage.Children.Add (new GridPage {
                MenuID = "add_news",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/add_news.html"
            });
            _adminPage.Children.Add (new GridPage {
                MenuID = "edit_news",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/edit_news.html"
            });
            _adminPage.Children.Add (new GridPage {
                MenuID = "edit_user",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/edit_user.html"
            });

            _adminPage.Children.Add (new GridPage {
                MenuID = "abuse_report",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8,
                Location = "admin/abuse_report.html"
            });

            _adminPage.Children.Add (new GridPage {
                MenuID = "region_edit",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8,
                Location = "admin/region_edit.html"
            });

            _adminPage.Children.Add (new GridPage {
                MenuID = "estate_edit",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8,
                Location = "admin/estate_edit.html"
            });
        }

        public static bool RequiresUpdate ()
        {
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            var version = generics.GetGeneric<OSDWrapper> (UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger () < CurrentVersion;
        }

        public static uint GetVersion ()
        {
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            var version = generics.GetGeneric<OSDWrapper> (UUID.Zero, Schema + "Version", "");
            return version == null ? 0 : (uint)version.Info.AsInteger ();
        }

        public static bool RequiresInitialUpdate ()
        {
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            var version = generics.GetGeneric<OSDWrapper> (UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger () < 1;
        }

        public static void ResetToDefaults ()
        {
            InitializeDefaults ();
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            //Remove all pages
            generics.RemoveGeneric (UUID.Zero, Schema);

            generics.AddGeneric (UUID.Zero, Schema, "Root", _rootPage.ToOSD ());
            generics.AddGeneric (UUID.Zero, Schema, "User", _userPage.ToOSD ());
            generics.AddGeneric (UUID.Zero, Schema, "UserTop", _userTopPage.ToOSD ());
            generics.AddGeneric (UUID.Zero, Schema, "Admin", _adminPage.ToOSD ());
            generics.AddGeneric (UUID.Zero, Schema + "Version", "", new OSDWrapper { Info = CurrentVersion }.ToOSD ());
        }

        public static bool CheckWhetherIgnoredVersionUpdate (uint version)
        {
            return version != CurrentVersion;
        }
    }
}
