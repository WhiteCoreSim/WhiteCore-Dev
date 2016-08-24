/*
 * Copyright (c) Contributors, http://whitecore-sim.org
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

namespace WhiteCore.Modules.Web.Translators
{
    public class DutchTranslation : ITranslator
    {
        public string LanguageName {
            get { return "nl"; }
        }

        public string GetTranslatedString (string key)
        {
            if (dictionary.ContainsKey (key))
                return dictionary [key];
            return ":" + key + ":";
        }

        readonly Dictionary<string, string> dictionary = new Dictionary<string, string> {
            // Generic
            { "No", "No"},
            { "Yes", "Yes"},
            { "Submit", "Verzenden"},
            { "Accept", "Accepteren"},
            { "Save", "Save"},
            { "FirstText", "Eerste"},
            { "BackText", "Terug"},
            { "NextText", "Volgende"},
            { "LastText", "Laatste"},
            { "CurrentPageText", "Current Page"},
            { "MoreInfoText", "Meer Info"},
            { "NoDetailsText", "Geen gegevens gevonden..."},
            { "MoreInfo", "More Informatie"},
            { "Name", "Naam"},
            { "ObjectNameText", "Voorwerp"},
            { "LocationText", "Plaats"},
            { "UUIDText", "UUID"},
            { "DetailsText", "Beschrijving"},
            { "NotesText", "Aantekeningen"},
            { "SaveUpdates", "Sia updates"},
            { "ActiveText", "Actief"},
            { "CheckedText", "Gecontroleerd"},
            { "CategoryText", "Categorie"},
            { "SummaryText", "Overzicht"},
            { "MaturityText", "Rijpheid"},
            { "DateText", "Datum"},
            { "TimeText", "Tijd"},
            { "MinuteText", "minuut"},
            { "MinutesText", "notulen"},
            { "HourText", "urr"},
            { "HoursText", "urr"},
            { "EdittingText", "Editing"},

            // Status information
            { "GridStatus", "Grid Status"},
            { "Online", "Online"},
            { "Offline", "Offline"},
            { "TotalUserCount", "Totale Gebruikers"},
            { "TotalRegionCount", "Totale Regios"},
            { "UniqueVisitors", "Unieke Bezeoeker per 30 dagen"},
            { "OnlineNow", "Nu online"},
            { "InterWorld", "Inter World (IWC)"},
            { "HyperGrid", "HyperGrid (HG)"},
            { "Voice", "Voice"},
            { "Currency", "Currency"},
            { "Disabled", "Uitgeschakeld"},
            { "Enabled", "Ingeschakeld"},
            { "News", "Nieuws"},
            { "Region", "Regio"},

            // User login
            { "Login", "Login"},
            { "UserName", "Gebruikersnaam"},
            { "UserNameText", "Gebruikersnaam"},
            { "Password", "Wachtwoord"},
            { "PasswordText", "Wachtwoord"},
            { "PasswordConfirmation", "Wachtwoord Bevestiging"},
            { "ForgotPassword", "Wachtwoord vergeten?"},
            { "TypeUserNameToConfirm", "Geef de gebruikersnaam van dit account in om te bevestigen dat je dit account wilt verwijderen"},

            // Special window
            { "SpecialWindowTitleText", "Special Info Window Titel"},
            { "SpecialWindowTextText", "Special Info Window Tekst"},
            { "SpecialWindowColorText", "Special Info Window Kleur"},
            { "SpecialWindowStatusText", "Special Info Window Status"},
            { "WelcomeScreenManagerFor", "Welkoms Scherm Manager voor"},
            { "ChangesSavedSuccessfully", "Wijzigingen succesvol opgeslagen"},

            // User registration
            { "AvatarNameText", "Avatar Naam"},
            { "AvatarScopeText", "Avatar Scope ID"},
            { "FirstNameText", "Uw Voornaam"},
            { "LastNameText", "Uw Achternaam"},
            { "UserAddressText", "Uw Adres"},
            { "UserZipText", "Uw Postcode"},
            { "UserCityText", "Uw Stad"},
            { "UserCountryText", "Uw Land"},
            { "UserDOBText", "Uw Geboortedatum (Maand Dag Jaar)"},
            { "UserEmailText", "Uw Email"},
            { "UserHomeRegionText", "Regione Home"},
            { "RegistrationText", "Avatar registratie"},
            { "RegistrationsDisabled", "Registraties zijn op dit moment gesloten, probeert u het later nog eens."},
            { "TermsOfServiceText", "Terms of Service"},
            { "TermsOfServiceAccept", "Accepteer u deze Terms of Service zoals boven beschreven?"},
            { "AvatarNameError", "Je hebt een avatar naam invoeren!"},
            { "AvatarPasswordError", "Wachtwoord is leeg of niet overeenkomen met!"},
            { "AvatarEmailError", "Een e-mailadres is vereist voor wachtwoord herstel! ('none' indien niet bekend)"},
            { "AvatarNameSpacingError", "Je avatar naam moet 'Voornaam Achternaam'!"},

            // News
            { "OpenNewsManager", "Open de Nieuws manager"},
            { "NewsManager", "Nieuws Manager"},
            { "EditNewsItem", "Bewerk nieuws"},
            { "AddNewsItem", "Voeg nieuw nieuws bericht toe"},
            { "DeleteNewsItem", "Verwijder nieuws item"},
            { "NewsDateText", "Nieuws Datum"},
            { "NewsTitleText", "Nieuws Titel"},
            { "NewsItemTitle", "Nieuws Item Titel"},
            { "NewsItemText", "Nieuws Item Tekst"},
            { "AddNewsText", "Nieuws toevoegen"},
            { "DeleteNewsText", "Verwijder Nieuws"},
            { "EditNewsText", "Bewerk Nieuws"},

            // User Profile
            { "UserProfileFor", "User Profiel Voor"},
            { "UsersGroupsText", "Groepen"},
            { "GroupNameText", "Groep"},
            { "UsersPicksText", "Picks for"},
            { "ResidentSince", "Resident Since"},
            { "AccountType", "Account Type"},
            { "PartnersName", "Partner's Naam"},
            { "AboutMe", "Over Mij"},
            { "IsOnlineText", "User Status"},
            { "OnlineLocationText", "User Locatie"},
            { "Partner", "Partner"},
            { "Friends", "Friends"},
            { "Nothing", "None"},
            { "ChangePass", "Verander wachtwoord"},
            { "NoChangePass", "Niet in staat om het wachtwoord te veranderen, probeer het later opnieuw"},

            // Region information
            { "RegionInformationText", "Region Informatie"},
            { "OwnerNameText", "Owner Naam"},
            { "RegionLocationText", "Region Locatie"},
            { "RegionSizeText", "Region Grootte"},
            { "RegionNameText", "Region Naam"},
            { "RegionTypeText", "Region Type"},
            { "RegionDelayStartupText", "Delay starting scripts"},
            { "RegionPresetText", "Region Preset"},
            { "RegionTerrainText", "Region Terrain"},
            { "ParcelsInRegionText", "Parcels In Region"},
            { "ParcelNameText", "Parcel Naam"},
            { "ParcelOwnerText", "Parcel Owner's Naam"},

            // Region List
            { "RegionInfoText", "Region Info"},
            { "RegionListText", "Region List"},
            { "RegionLocXText", "Region X"},
            { "RegionLocYText", "Region Y"},
            { "SortByLocX", "Sort By Region X"},
            { "SortByLocY", "Sort By Region Y"},
            { "SortByName", "Sort By Region Name"},
            { "RegionMoreInfo", "More Informatie"},
            { "RegionMoreInfoTooltips", "More info over"},
            { "OnlineUsersText", "Online Users"},
            { "OnlineFriendsText", "Online Friends"},
            { "RegionOnlineText", "Region Status"},
            { "RegionMaturityText", "Access Rating"},
            { "NumberOfUsersInRegionText", "Number of Users in region"},

            // Region manager
            { "Mainland", "Vasteland"},
            { "Estate", "Estate"},
            { "FullRegion", "Volledige Regio"},
            { "Homestead", "Homestead"},
            { "Openspace", "Openspace"},
            { "Flatland", "Flatland"},
            { "Grassland", "Grasland"},
            { "Island", "Island"},
            { "Aquatic", "Aquatische"},
            { "Custom", "Custom"},
            { "RegionPortText", "Regio-poort"},
            { "RegionVisibilityText", "Zichtbaar voor buren"},
            { "RegionInfiniteText", "Infinite Regio"},
            { "RegionCapacityText", "Regio object capaciteit"},

            // Menus
            { "MenuHome", "Home"},
            { "MenuLogin", "Login"},
            { "MenuLogout", "Logout"},
            { "MenuRegister", "Registeer"},
            { "MenuForgotPass", "Wachtwoord vergeten"},
            { "MenuNews", "Nieuws"},
            { "MenuWorld", "Wereld"},
            { "MenuWorldMap", "Wereld Map"},
            { "MenuRegion", "Region List"},
            { "MenuUser", "Gebruiker"},
            { "MenuOnlineUsers", "Online Gebruikers"},
            { "MenuUserSearch", "Zoek Gebruiker"},
            { "MenuRegionSearch", "Region Search"},
            { "MenuChat", "Chat"},
            { "MenuHelp", "Help"},
            { "MenuViewerHelp", "Viewer Help"},
            { "MenuChangeUserInformation", "Wijzig User Informatie"},
            { "MenuWelcomeScreenManager", "Welcome Screen Manager"},
            { "MenuNewsManager", "Nieuws Manager"},
            { "MenuUserManager", "User Manager"},
            { "MenuFactoryReset", "Factory Reset"},
            { "ResetMenuInfoText", "Reset de menu items terug naar de default waardes"},
            { "ResetSettingsInfoText", "Reset de Web Interface terug naar de default waardes"},
            { "MenuPageManager", "Page Manager"},
            { "MenuSettingsManager", "Settings Manager"},
            { "MenuManager", "Beheer"},
            { "MenuSettings", "Instellingen"},
            { "MenuRegionManager", "Regio Manager"},
            { "MenuManagerSimConsole", "Sim console"},
            { "MenuPurchases", "Gebruiker Aankopen"},
            { "MenuMyPurchases", "Mijn aankopen "},
            { "MenuTransactions", "Gebruiker Transacties"},
            { "MenuMyTransactions", "Mijn Transacties"},
            { "MenuClassifieds", "Advertenties"},
            { "MenuMyClassifieds", "Mijn Advertenties"},
            { "MenuEvents", "Evenementen"},
            { "MenuMyEvents", "Mijn evenementen"},
            { "MenuStatistics", "Viewer statistieken"},
            { "MenuGridSettings", "Grid instellingen"},

            // Menu Tooltips
            { "TooltipsMenuHome", "Home"},
            { "TooltipsMenuLogin", "Login"},
            { "TooltipsMenuLogout", "Logout"},
            { "TooltipsMenuRegister", "Registeer"},
            { "TooltipsMenuForgotPass", "Wachtwoord vergeten"},
            { "TooltipsMenuNews", "Nieuws"},
            { "TooltipsMenuWorld", "Wereld"},
            { "TooltipsMenuWorldMap", "Wereld Map"},
            { "TooltipsMenuUser", "User"},
            { "TooltipsMenuOnlineUsers", "Online Users"},
            { "TooltipsMenuUserSearch", "User Search"},
            { "TooltipsMenuRegionSearch", "Region Search"},
            { "TooltipsMenuChat", "Chat"},
            { "TooltipsMenuViewerHelp", "Viewer Help"},
            { "TooltipsMenuHelp", "Help"},
            { "TooltipsMenuChangeUserInformation", "Change User Information"},
            { "TooltipsMenuWelcomeScreenManager", "Welcome Screen Manager"},
            { "TooltipsMenuNewsManager", "Nieuws Manager"},
            { "TooltipsMenuUserManager", "User Manager"},
            { "TooltipsMenuFactoryReset", "Factory Reset"},
            { "TooltipsMenuPageManager", "Page Manager"},
            { "TooltipsMenuSettingsManager", "Settings Manager"},
            { "TooltipsMenuManager", "Admin Management"},
            { "TooltipsMenuSettings", "WebUI Instellingen"},
            { "TooltipsMenuRegionManager", "Regio maken / bewerken"},
            { "TooltipsMenuManagerSimConsole", "Online simulator console"},
            { "TooltipsMenuPurchases", "Aankoop informatie"},
            { "TooltipsMenuTransactions", "Transactie-informatie"},
            { "TooltipsMenuClassifieds", "Advertenties informatie"},
            { "TooltipsMenuEvents", "Evenement informatie"},
            { "TooltipsMenuStatistics", "Viewer statistieken"},
            { "TooltipsMenuGridSettings", "Grid instellingen"},

            // Menu Region
            { "MenuRegionTitle", "Region"},
            { "MenuParcelTitle", "Parcel"},
            { "MenuOwnerTitle", "Owner"},
            { "TooltipsMenuRegion", "Regio informatie"},
            { "TooltipsMenuParcel", "Regio Parcels"},
            { "TooltipsMenuOwner", "Estate Owner"},

            // Menu Profile
            { "MenuProfileTitle", "Profile"},
            { "MenuGroupTitle", "Group"},
            { "MenuPicksTitle", "Picks"},
            { "MenuRegionsTitle", "Regions"},
            { "TooltipsMenuProfile", "Gebruiker Profile"},
            { "TooltipsMenuGroups", "Gebruikersgroepen"},
            { "TooltipsMenuPicks", "Gebruiker Picks"},
            { "TooltipsMenuRegions", "Gebruiker regio"},
            { "UserGroupNameText", "Gebruikersgroep"},
            { "PickNameText", "Pick naam"},
            { "PickRegionText", "Locatie"},

            // Urls
            { "WelcomeScreen", "Welcome Screen"},

            // Tooltips Urls
            { "TooltipsWelcomeScreen", "Welcome Screen"},
            { "TooltipsWorldMap", "World Map"},

            // Index
            { "HomeText", "Home"},
            { "HomeTextWelcome", "This is our New Virtual World! Join us now, and make a difference!"},
            { "HomeTextTips", "New presentations"},
            { "WelcomeToText", "Welcome to"},

            // World Map
            { "WorldMap", "World Map"},
            { "WorldMapText", "Full Screen"},

            // Chat
            { "ChatText", "Chat Support"},

            // Help
            { "HelpText", "Help"},
            { "HelpViewersConfigText", "Viewer Configuratie"},

            // Logout
            { "Logout", "Logout"},
            { "LoggedOutSuccessfullyText", "You have been logged out successfully."},

            // Change user information page
            { "ChangeUserInformationText", "Change User Information"},
            { "ChangePasswordText", "Change Password"},
            { "NewPasswordText", "New Password"},
            { "NewPasswordConfirmationText", "New Password (Confirmation)"},
            { "ChangeEmailText", "Change Email Address"},
            { "NewEmailText", "New Email Address"},
            { "DeleteUserText", "Delete My Account"},
            { "DeleteText", "Delete"},
            { "DeleteUserInfoText",
                    "This will remove all information about you in the grid and remove your access to this service. If you wish to continue, enter your name and password and click Delete."},
            { "EditText", "Edit"},
            { "EditUserAccountText", "Edit User Account"},

            // Maintenance
            { "WebsiteDownInfoText", "Website is currently down, please try again soon."},
            { "WebsiteDownText", "Website offline"},

            // Http 404
            { "Error404Text", "Error code"},
            { "Error404InfoText", "404 Page Not Found"},
            { "HomePage404Text", "home page"},

            // Http 505
            { "Error505Text", "Error code"},
            { "Error505InfoText", "505 Internal Server Error"},
            { "HomePage505Text", "home page"},

            // User search
            { "Search", "Search"},
            { "SearchText", "Search"},
            { "SearchForUserText", "Search For A User"},
            { "UserSearchText", "User Search"},
            { "SearchResultForUserText", "Search Result For User"},

            // Region search
            { "SearchForRegionText", "Search For A Region"},
            { "RegionSearchText", "Region Search"},
            { "SearchResultForRegionText", "Search Result For Region"},

            // Edit user
            { "AdminDeleteUserText", "Delete User"},
            { "AdminDeleteUserInfoText", "This deletes the account and destroys all information associated with it."},
            { "BanText", "Ban"},
            { "UnbanText", "Unban"},
            { "AdminTempBanUserText", "Temp Ban User"},
            { "AdminTempBanUserInfoText", "This blocks the user from logging in for the set amount of time."},
            { "AdminBanUserText", "Ban User"},
            { "AdminBanUserInfoText", "This blocks the user from logging in until the user is unbanned."},
            { "AdminUnbanUserText", "Unban User"},
            { "AdminUnbanUserInfoText", "Removes temporary and permanent bans on the user."},
            { "AdminLoginInAsUserText", "Login as User"},
            { "AdminLoginInAsUserInfoText",
                    "You will be logged out of your admin account, and logged in as this user, and will see everything as they see it."},
            { "TimeUntilUnbannedText", "Time until user is unbanned"},
            { "BannedUntilText", "User banned until:"},
            { "KickAUserText", "Kick User"},
            { "KickAUserInfoText", "Kicks a user from the grid (logs them out within 30 seconds)"},
            { "KickMessageText", "Message To User"},
            { "KickUserText", "Kick User"},
            { "MessageAUserText", "Send User A Message"},
            { "MessageAUserInfoText", "Sends a user a blue-box message (will arrive within 30 seconds)"},
            { "MessageUserText", "Message User"},

            // Transactions
            { "TransactionsText", "Transacties"},
            { "DateInfoText", "Selecteer een datumbereik"},
            { "DateStartText", "Ingangsdatum"},
            { "DateEndText", "Eind"},
            { "30daysPastText", "Vorige 30 dagen"},
            { "TransactionToAgentText", "To User"},
            { "TransactionFromAgentText", "From User"},
            { "TransactionDateText", "Datum"},
            { "TransactionDetailText", "Beschrijving"},
            { "TransactionAmountText", "Bedrag"},
            { "TransactionBalanceText", "Balance"},
            { "NoTransactionsText", "Geen transacties gevonden..."},
            { "PurchasesText", "Aankopen"},
            { "LoggedIPText", "Gelogde IP-adres"},
            { "NoPurchasesText", "Geen aankopen gevonden..."},
            { "PurchaseCostText", "Kosten"},

            // Classifieds
            { "ClassifiedsText", "Advertenties"},
            { "ClassifiedText", "Advertentie"},
            { "ListedByText", "Aangeboden door"},
            { "CreationDateText", "Aanmaakdatum"},
            { "ExpirationDateText", "Uiterste houdbaarheidsdatum" },
            { "DescriptionText", "Beschrijving" },
            { "PriceOfListingText", "Prijs"},

            // Classified categories
            { "CatAll", "Alle"},
            { "CatSelect", ""},
            { "CatShopping", "Het winkelen"},
            { "CatLandRental", "Land Huur"},
            { "CatPropertyRental", "Eigendom Huur"},
            { "CatSpecialAttraction", "Bijzondere attractien"},
            { "CatNewProducts", "Nieuwe Producten"},
            { "CatEmployment", "Werk"},
            { "CatWanted", "Gezocht"},
            { "CatService", "Service"},
            { "CatPersonal", "Persoonlijk"},

            // Events
            { "EventsText", "Evenementen"},
            { "EventNameText", "Evenement"},
            { "EventLocationText", "Waar"},
            { "HostedByText","Gepresenteerd door"},
            { "EventDateText", "Wanneer"},
            { "EventTimeInfoText", "Event tijd moeten de lokale tijd (Server)"},
            { "CoverChargeText", "Dekking"},
            { "DurationText", "Duur"},
            { "AddEventText", "Voeg Event"},

            // Event categories
            { "CatDiscussion", "Discussie"},
            { "CatSports", "Sport"},
            { "CatLiveMusic", "Live muziek"},
            { "CatCommercial", "Commercieel"},
            { "CatEntertainment", "Nachtleven/Vermaak"},
            { "CatGames", "Spellen/Wedstrijden"},
            { "CatPageants", "Pageants"},
            { "CatEducation", "Onderwijs"},
            { "CatArtsCulture", "Arts/Cultuur"},
            { "CatCharitySupport", "Liefdadigheid/Steungroepp"},
            { "CatMiscellaneous", "Diversen"},

            // Event lookup periods
            { "Next24Hours", "Komende 24 uur"},
            { "Next10Hours", "Komende 10 uur"},
            { "Next4Hours", "Komende 4 uur"},
            { "Next2Hours", "Komende 2 uur"},

            // Sim Console
            { "SimConsoleText", "Sim Command Console"},
            { "SimCommandText", "Command"},

            // Statistics
            { "StatisticsText", "Viewer statistieken"},
            { "ViewersText", "Viewer gebruik"},
            { "GPUText", "Grafische kaarten"},
            { "PerformanceText", "Gemiddelden prestaties"},
            { "FPSText", "Frames / seconde"},
            { "RunTimeText", "Looptijd"},
            { "RegionsVisitedText", "Bezochte regio's"},
            { "MemoryUseageText", "Geheugen gebruik"},
            { "PingTimeText", "Ping tijd"},
            { "AgentsInViewText", "Agenten in het oog"},
            { "ClearStatsText", "Duidelijke statistieken over"},

            // Abuse reports
            { "MenuAbuse", "Misbruik Rapporten"},
            { "TooltipsMenuAbuse", "Gebruiker misbruil rapporten"},
            { "AbuseReportText", "Meld misbruikt"},
            { "AbuserNameText", "Abuser"},
            { "AbuseReporterNameText", "Verslaggever"},
            { "AssignedToText", "Toegewezen aan"},

            // Factory_reset
            { "FactoryReset", "Factory Reset"},
            { "ResetMenuText", "Reset Menu To Factory Defaults"},
            { "ResetSettingsText", "Reset Web Settings (Settings Manager page) To Factory Defaults"},
            { "Reset", "Reset"},
            { "Settings", "Settings"},
            { "Pages", "Pages"},
            { "DefaultsUpdated", "defaults updated, go to Factory Reset to update or Settings Manager to disable this warning."},

            // Page_manager
            { "PageManager", "Page Manager"},
            { "SaveMenuItemChanges", "Save Menu Item"},
            { "SelectItem", "Select Item"},
            { "DeleteItem", "Delete Item"},
            { "AddItem", "Add Item"},
            { "PageLocationText", "Page Location"},
            { "PageIDText", "Page ID"},
            { "PagePositionText", "Page Position"},
            { "PageTooltipText", "Page Tooltip"},
            { "PageTitleText", "Page Title"},
            { "RequiresLoginText", "Requires Login To View"},
            { "RequiresLogoutText", "Requires Logout To View"},
            { "RequiresAdminText", "Requires Admin To View"},
            { "RequiresAdminLevelText", "Required Admin Level To View"},

            // Grid settings
            { "GridSettingsManager", "Grid Settings Manager"},
            { "GridnameText", "Grid naam"},
            { "GridnickText", "Grid bijnaam"},
            { "WelcomeMessageText", "Login welkomstbericht "},
            { "GovernorNameText", "Systeem gouverneur"},
            { "MainlandEstateNameText", "Vasteland landgoed"},
            { "RealEstateOwnerNameText", "Systeem goed ownername"},
            { "SystemEstateNameText", "Naam Estate systeem"},
            { "BankerNameText", "Systeem bankierr"},
            { "MarketPlaceOwnerNameText", "Systeem marketplace eigenaar"},

            // Settings manager
            { "WebRegistrationText", "Web registrations allowed"},
            { "GridCenterXText", "Grid Center Location X"},
            { "GridCenterYText", "Grid Center Location Y"},
            { "SettingsManager", "Settings Manager"},
            { "IgnorePagesUpdatesText", "Ignore pages update warning until next update"},
            { "IgnoreSettingsUpdatesText", "Ignore settings update warning until next update"},
            { "HideLanguageBarText", "Verbergen taalkeuzemenu"},
            { "HideStyleBarText", "Verbergen stijl keuzebalk"},
            { "HideSlideshowBarText", "Verbergen slideshow bar"},
            { "LocalFrontPageText", "Lokale voorpagina"},
            { "LocalCSSText", "Lokale CSS stylesheet"},

            // Dates
            { "Sun", "Sun"},
            { "Mon", "Mon"},
            { "Tue", "Tue"},
            { "Wed", "Wed"},
            { "Thu", "Thu"},
            { "Fri", "Fri"},
            { "Sat", "Sat"},
            { "Sunday", "Sunday"},
            { "Monday", "Monday"},
            { "Tuesday", "Tuesday"},
            { "Wednesday", "Wednesday"},
            { "Thursday", "Thursday"},
            { "Friday", "Friday"},
            { "Saturday", "Saturday"},

            { "Jan_Short", "Jan"},
            { "Feb_Short", "Feb"},
            { "Mar_Short", "Mar"},
            { "Apr_Short", "Apr"},
            { "May_Short", "May"},
            { "Jun_Short", "Jun"},
            { "Jul_Short", "Jul"},
            { "Aug_Short", "Aug"},
            { "Sep_Short", "Sep"},
            { "Oct_Short", "Oct"},
            { "Nov_Short", "Nov"},
            { "Dec_Short", "Dec"},

            { "January", "January"},
            { "February", "February"},
            { "March", "March"},
            { "April", "April"},
            { "May", "May"},
            { "June", "June"},
            { "July", "July"},
            { "August", "August"},
            { "September", "September"},
            { "October", "October"},
            { "November", "November"},
            { "December", "December"},

            // User types
            { "UserTypeText", "Soort gebruiker"},
            { "AdminUserTypeInfoText", "Het type gebruiker (momenteel gebruikt voor periodieke betalingen stipendium)."},
            { "Guest", "Gast"},
            { "Resident", "Ingezetene"},
            { "Member", "Lid"},
            { "Contractor", "Aannemer"},
            { "Charter_Member", "Mede-oprichter"},

            // ColorBox
            { "ColorBoxImageText", "Image"},
            { "ColorBoxOfText", "of"},
            { "ColorBoxPreviousText", "Previous"},
            { "ColorBoxNextText", "Next"},
            { "ColorBoxCloseText", "Close"},
            { "ColorBoxStartSlideshowText", "Start Slide Show"},
            { "ColorBoxStopSlideshowText", "Stop Slide Show"},

            // Maintenance
            { "NoAccountFound", "Nog geen account gevonden"},
            { "DisplayInMenu", "Display in het menu"},
            { "ParentText", "Menu ouder"},
            { "CannotSetParentToChild", "Kan geen menu-item als een kind naar zichzelf."},
            { "TopLevel", "Top Level"},

            // Style Switcher
            { "styles1", "Default Minimalist"},
            { "styles2", "Light Degarde"},
            { "styles3", "Blue Night"},
            { "styles4", "Dark Degrade"},
            { "styles5", "Luminus"},

            { "StyleSwitcherStylesText", "Styles"},
            { "StyleSwitcherLanguagesText", "Languages"},
            { "StyleSwitcherChoiceText", "Choice"},

            // Language Switcher Tooltips
            { "en", "English"},
            { "fr", "Fran?ais"},
            { "de", "Deutsch"},
            { "it", "Italiano"},
            { "es", "Espa?ol"},
            { "nl", "Nederlands"},
            { "ru", "Русский"}

        };
    }
}
