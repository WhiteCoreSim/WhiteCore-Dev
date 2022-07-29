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
        static GridPage _modalPage;
        public static readonly string Schema = "WebPages";
        public static readonly uint CurrentVersion = 17;

        static void InitializeDefaults() {
            _rootPage = new GridPage();
            _userPage = new GridPage();
            _userTopPage = new GridPage();
            _adminPage = new GridPage();
            _modalPage = new GridPage();

            // Main menu options
            // home
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "home",
                Location = "home.html",
                MenuPosition = 0,
                MenuTitle = "MenuHome",
                MenuToolTip = "TooltipsMenuHome"
            });

            // world map
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "world",
                Location = "world.html",
                MenuPosition = 1,
                MenuTitle = "MenuWorld",
                MenuToolTip = "TooltipsMenuWorld",
            });

            // news
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "news",
                Location = "news.html",
                MenuPosition = 2,
                MenuTitle = "MenuNews",
                MenuToolTip = "TooltipsMenuNews"
            });

            // events
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                LoggedInRequired = false,
                MenuID = "events",
                Location = "events/events.html",
                MenuPosition = 3,
                MenuTitle = "MenuEvents",
                MenuToolTip = "TooltipsMenuEvents"
            });

            // classifieds
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                LoggedInRequired = false,
                MenuID = "classifieds",
                Location = "classifieds/classifieds.html",
                MenuPosition = 4,
                MenuTitle = "MenuClassifieds",
               MenuToolTip = "TooltipsMenuClassifieds"
            });

            // help
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "help",
                Location = "help.html",
                MenuPosition = 5,
                MenuTitle = "MenuHelp",
                MenuToolTip = "TooltipsMenuHelp",
            });

            // Non menu options
            // register
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedOutRequired = true,
                MenuID = "register",
                Location = "register.html",
                MenuPosition = 9,
                MenuTitle = "MenuRegister",
                MenuToolTip = "TooltipsMenuRegister"
            });

            // Login
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedOutRequired = true,
                MenuID = "login",
                Location = "login.html",
                MenuPosition = 10,
                MenuTitle = "MenuLogin",
                MenuToolTip = "TooltipsMenuLogin",
            });

            // logout
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "logout",
                Location = "logout.html",
                MenuPosition = 10,
                MenuTitle = "MenuLogout",
                MenuToolTip = "TooltipsMenuLogout"
            });

            // forgot password
            _rootPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                MenuID = "forgotpass",
                Location = "forgot_pass.html",
                MenuPosition = 10,
            });

            // Modal views - main and user

            // region/userprofile info from world map
            _modalPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                MenuID = "regionprofile-modal_profile",
                Location = "regionprofile/modal_profile.html",
                MenuPosition = 1
            });
            _modalPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                MenuID = "regionprofile-modal_parcels",
                Location = "regionprofile/modal_parcels.html",
                MenuPosition = 1
            });

            _modalPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                MenuID = "webprofile-modal_profile",
                Location = "webprofile/modal_profile.html",
                MenuPosition = 1
            });
            _modalPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                MenuID = "webprofile-modal_groups",
                Location = "webprofile/modal_groups.html",
                MenuPosition = 1
            });
            _modalPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                MenuID = "webprofile-modal_picks",
                Location = "webprofile/modal_picks.html",
                MenuPosition = 1
            });
            _modalPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                MenuID = "webprofile-modal_regions",
                Location = "webprofile/modal_regions.html",
                MenuPosition = 1
            });
            _modalPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "irc_chat",
                Location = "irc_chat.html",
                MenuPosition = 1,
            });

            // User pages

            // users
            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "account",
                Location = "user/profile.html",
                MenuPosition = 1,
                MenuTitle = "MenuUser",
                MenuToolTip = "TooltipsMenuUser",
                Children = new List<GridPage> {
                    /* new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-profile",
                        Location = "user/profile.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuUserProfile",
                        MenuToolTip = "TooltipsMenuProfile"
                    }, */
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-purchases",
                        Location = "user/purchases.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuMyPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-transactions",
                        Location = "user/transactions.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuMyTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-contact",
                        Location = "user/contact.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuContactInfo",
                        MenuToolTip = "TooltipsMenuContactInfo"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-email",
                        Location = "user/email.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuEmail",
                        MenuToolTip = "TooltipsMenuEmail"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-partners",
                        Location = "user/partnership.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuPartners",
                        MenuToolTip = "TooltipsMenuPartners"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-password",
                        Location = "user/password.html",
                        MenuPosition = 5,
                        MenuTitle = "MenuChangeUserPassword",
                        MenuToolTip = "TooltipsMenuChangeUserPassword"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-deleteaccount",
                        Location = "user/deleteaccount.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuDeleteAccount",
                        MenuToolTip = "TooltipsMenuDeleteAccount"
                    } /*,
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "change_user_information",
                        Location = "user/update_user.html",
                        MenuPosition = 8,
                        MenuTitle = "MenuChangeUserInformation",
                        MenuToolTip = "TooltipsMenuChangeUserInformation"
                    }*/
                }
            });

            // events
            _userPage.Children.Add(new GridPage
            {
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
                        MenuID = "user-events",
                        Location = "user/events.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuMyEvents",
                        MenuToolTip = "TooltipsMenuEvents"
                    },
                }
            });

            // shopping
            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "usershopping",
                Location = "classifieds/classifieds.html",
                MenuPosition = 3,
                MenuTitle = "MenuShopping",
                MenuToolTip = "TooltipsMenuShopping",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = false,
                        MenuID = "user-classifieds",
                        Location = "user/classifieds.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuClassifieds",
                        MenuToolTip = "TooltipsMenuClassifieds"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = false,
                        MenuID = "marketplace",
                        Location = "classifieds/marketplace.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuMarketplace",
                        MenuToolTip = "TooltipsMenuMarketplace"
                    },
                }
            });

            // land management
            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "userlandmanage",
                Location = "user/region_manager.html",
                MenuPosition = 4,
                MenuTitle = "MenuUserLand",
                MenuToolTip = "TooltipsMenuUserLand",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-buyland",
                        Location = "user/buyland.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuUserBuyLand",
                        MenuToolTip = "TooltipsMenuUserBuyLand"
                    },
                   new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-groupland",
                        Location = "user/groupland.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuUserGroupLand",
                        MenuToolTip = "TooltipsMenuUserGroupLand"
                    },
                     new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-landfees",
                        Location = "user/landfees.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuUserLandfees",
                        MenuToolTip = "TooltipsMenuUserLandfees"
                    },
                   new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-mainland",
                        Location = "user/mainland.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuUserMainland",
                        MenuToolTip = "TooltipsMenuUserMainland"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        LoggedInRequired = true,
                        MenuID = "user-estate_manager",
                        Location = "user/estate_manager.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuEstateManager",
                        MenuToolTip = "TooltipsMenuEstateManager"
                    },
                }
            });

            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                LoggedInRequired = true,
                MenuID = "user-friends",
                Location = "user/friends.html",
                MenuPosition = 5,
                MenuTitle = "MenuUserFriends",
                MenuToolTip = "TooltipsMenuUserFriends",
                /*  Should these be here??
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
                }*/
            });

            // User - non menu pages
            _userPage.Children.Add(new GridPage {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "user-userhome",
                Location = "user/userhome.html",
                MenuPosition = 8
            });

            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "classifieds-add_classified",
                Location = "classifieds/add_classified.html",
                MenuPosition = 8
            });

            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "events-add_event",
                Location = "events/add_event.html",
                MenuPosition = 8
            });
            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "user-edit_event",
                Location = "user/edit_event.html",
                MenuPosition = 8
            });
            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "user-region_edit",
                Location = "user/region_edit.html",
                MenuPosition = 8
            });
            _userPage.Children.Add(new GridPage
            {
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuID = "user-estate_edit",
                Location = "user/estate_edit.html",
                MenuPosition = 8
            });

            // User top menu pages
            _userTopPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "userhome",
                Location = "userhome.html",
                MenuPosition = 0,
                MenuTitle = "MenuHome",
                MenuToolTip = "TooltipsMenuHome"
            });

            // news
            _userTopPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "news",
                Location = "news.html",
                MenuPosition = 1,
                MenuTitle = "MenuNews",
                MenuToolTip = "TooltipsMenuNews"
            });

            // world map
            _userTopPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "world",
                Location = "world.html",
                MenuPosition = 2,
                MenuTitle = "MenuWorld",
                MenuToolTip = "TooltipsMenuWorld",
            });

            // help
            _userTopPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                MenuID = "help",
                Location = "help.html",
                MenuPosition = 3,
                MenuTitle = "MenuHelp",
                MenuToolTip = "TooltipsMenuHelp",
            });

            // Non menu options
            // logout
            _userTopPage.Children.Add(new GridPage
            {
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
            _adminPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                AdminRequired = true,
                MenuID = "admin-manager",
                Location = "admin/user_manager.html",
                MenuPosition = 7,
                MenuTitle = "MenuManager",
                MenuToolTip = "TooltipsMenuManager",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-register",
                        Location = "admin/user_register.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuRegister",
                        MenuToolTip = "TooltipsMenuRegister"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-user_manager",
                        Location = "admin/user_manager.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuUserManager",
                        MenuToolTip = "TooltipsMenuUserManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-news_manager",
                        Location = "admin/news_manager.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuNewsManager",
                        MenuToolTip = "TooltipsMenuNewsManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-region_manager",
                        Location = "admin/region_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuRegionManager",
                        MenuToolTip = "TooltipsMenuRegionManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-estate_manager",
                        Location = "admin/estate_manager.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuEstateManager",
                        MenuToolTip = "TooltipsMenuEstateManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-abuse_manager",
                        Location = "admin/abuse_manager.html",
                        MenuPosition = 5,
                        MenuTitle = "MenuAbuse",
                        MenuToolTip = "TooltipsMenuAbuse"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-purchases",
                        Location = "admin/purchases.html",
                        MenuPosition = 6,
                        MenuTitle = "MenuPurchases",
                        MenuToolTip = "TooltipsMenuPurchases"

                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-transactions",
                        Location = "admin/transactions.html",
                        MenuPosition = 7,
                        MenuTitle = "MenuTransactions",
                        MenuToolTip = "TooltipsMenuTransactions"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-statistics",
                        Location = "admin/statistics.html",
                        MenuPosition = 8,
                        MenuTitle = "MenuStatistics",
                        MenuToolTip = "TooltipsMenuStatistics"
                    },
                    /*new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-sim_console",
                        Location = "admin/sim_console.html",
                        MenuPosition = 9,
                        MenuTitle = "MenuManagerSimConsole",
                        MenuToolTip = "TooltipsMenuManagerSimConsole"
                    }*/
                }
            });

            // admin settings
            _adminPage.Children.Add(new GridPage
            {
                ShowInMenu = true,
                AdminRequired = true,
                MenuID = "admin-settings",
                Location = "admin/settings_manager.html",
                MenuPosition = 8,
                MenuTitle = "MenuSettings",
                MenuToolTip = "TooltipsMenuSettingsManager",
                Children = new List<GridPage> {
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-gridsettings_manager",
                        Location = "admin/gridsettings_manager.html",
                        MenuPosition = 0,
                        MenuTitle = "MenuGridSettings",
                        MenuToolTip = "TooltipsMenuGridSettings"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-settings_manager",
                        Location = "admin/settings_manager.html",
                        MenuPosition = 1,
                        MenuTitle = "MenuSettingsManager",
                        MenuToolTip = "TooltipsMenuSettingsManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-page_manager",
                        Location = "admin/page_manager.html",
                        MenuPosition = 2,
                        MenuTitle = "MenuPageManager",
                       MenuToolTip = "TooltipsMenuPageManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-welcomescreen_manager",
                        Location = "admin/welcomescreen_manager.html",
                        MenuPosition = 3,
                        MenuTitle = "MenuWelcomeScreenManager",
                        MenuToolTip = "TooltipsMenuWelcomeScreenManager"
                    },
                    new GridPage {
                        ShowInMenu = true,
                        AdminRequired = true,
                        MenuID = "admin-factory_reset",
                        Location = "admin/factory_reset.html",
                        MenuPosition = 4,
                        MenuTitle = "MenuFactoryReset",
                        MenuToolTip = "TooltipsMenuFactoryReset"
                    }
                }
            });

            // Admin - non menu pages
            _adminPage.Children.Add(new GridPage
            {
                MenuID = "admin-news_add",
                Location = "admin/news_add.html",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8
            });
            _adminPage.Children.Add(new GridPage
            {
                MenuID = "admin-news_edit",
                Location = "admin/news_edit.html",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8
            });
            _adminPage.Children.Add(new GridPage
            {
                MenuID = "admin-user_edit",
                Location = "admin/user_edit.html",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8
            });
            _adminPage.Children.Add(new GridPage
            {
                MenuID = "admin-user_password",
                Location = "admin/user_password.html",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8
            });

            _adminPage.Children.Add(new GridPage
            {
                MenuID = "admin-abuse_report",
                Location = "admin/abuse_report.html",
                ShowInMenu = false,
                AdminRequired = true,
                MenuPosition = 8
            });

            _adminPage.Children.Add(new GridPage
            {
                MenuID = "admin-region_edit",
                Location = "admin/region_edit.html",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8
            });

            _adminPage.Children.Add(new GridPage
            {
                MenuID = "admin-estate_edit",
                Location = "admin/estate_edit.html",
                ShowInMenu = false,
                LoggedInRequired = true,
                MenuPosition = 8
            });
        }

        public static bool RequiresUpdate() {
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            var version = generics.GetGeneric<OSDWrapper>(UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger() < CurrentVersion;
        }

        public static uint GetVersion() {
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            var version = generics.GetGeneric<OSDWrapper>(UUID.Zero, Schema + "Version", "");
            return version == null ? 0 : (uint)version.Info.AsInteger();
        }

        public static bool RequiresInitialUpdate() {
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            var version = generics.GetGeneric<OSDWrapper>(UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger() < 1;
        }

        public static void ResetToDefaults() {
            InitializeDefaults();
            var generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();

            //Remove all pages
            generics.RemoveGeneric(UUID.Zero, Schema);

            generics.AddGeneric(UUID.Zero, Schema, "Root", _rootPage.ToOSD());
            generics.AddGeneric(UUID.Zero, Schema, "User", _userPage.ToOSD());
            generics.AddGeneric(UUID.Zero, Schema, "UserTop", _userTopPage.ToOSD());
            generics.AddGeneric(UUID.Zero, Schema, "Modal", _modalPage.ToOSD());
            generics.AddGeneric(UUID.Zero, Schema, "Admin", _adminPage.ToOSD());
            generics.AddGeneric(UUID.Zero, Schema + "Version", "", new OSDWrapper { Info = CurrentVersion }.ToOSD());
        }

        public static bool CheckWhetherIgnoredVersionUpdate(uint version) {
            return version != CurrentVersion;
        }
    }
}
