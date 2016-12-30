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
 * LOSS OF USE, DATA, OR PROFITS, OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;

namespace WhiteCore.Modules.Web.Translators
{
    public class GermanTranslation : ITranslator
    {
        public string LanguageName {
            get { return "de"; }
        }

        public string FullLanguageName {
            get { return "Deutsch"; }
        }

        public string GetTranslatedString (string key)
        {
            if (dictionary.ContainsKey (key))
                return dictionary [key];
            return ":" + key + ":";
        }

        Dictionary<string, string> dictionary = new Dictionary<string, string> {
            // Generic
            {"No", "nein"},
            {"Yes", "ja"},
            {"Submit", "Einreichen"},
            {"Accept", "Akzeptieren"},
            {"Save", "Speichern"},
            { "Cancel", "Stornieren"},
            {"FirstText", "Erste"},
            {"BackText", "ZurÃ¼ck"},
            {"NextText", "Vor"},
            {"LastText", "Letzte"},
            {"CurrentPageText", "Aktuelle Seite"},
            {"MoreInfoText", "Mehr Informationen"},
            {"NoDetailsText", "Keine Angaben gefunden..."},
            {"MoreInfo", "Informationen"},
            { "Name", "Name"},
            {"ObjectNameText", "Objekt"},
            {"LocationText", "Ort"},
            {"UUIDText", "UUID"},
            {"DetailsText", "Beschreibung"},
            {"NotesText", "Aufzeichnungen"},
            {"SaveUpdates", "Sparen Sie Aktuelles"},
            {"ActiveText", "Activ"},
            {"CheckedText", "GeprÃ¼ft"},
            {"CategoryText", "Kategorie"},
            {"SummaryText", "Zusammenfassung"},
            { "MaturityText", "Maturity"},
            { "GeneralText", "General"},
            { "MatureText", "Reifen"},
            { "AdultText", "Erwachsene"},
            { "DateText", "Datum"},
            { "TimeText", "Zeit"},
            { "MinuteText", "minute"},
            { "MinutesText", "minuten"},
            { "HourText", "stunde"},
            { "HoursText", "stunden"},
            { "EditText", "Bearbeiten"},
            { "EdittingText", "Bearbeiten"},

            // Status information
            {"GridStatus", "Grid Status"},
            {"Online", "Online"},
            {"Offline", "Offline"},
            {"TotalUserCount", "Einwohner"},
            {"TotalRegionCount", "Regionen"},
            {"UniqueVisitors", "Aktive Nutzer letzten 30 Tage"},
            {"OnlineNow", "Jetzt Online"},
            { "InterWorld", "Inter World (IWC)"},
            {"HyperGrid", "HyperGrid (HG)"},
            {"Voice", "Stimme"},
            {"Currency", "Geld"},
            {"Disabled", "Deaktiviert"},
            {"Enabled", "Aktiviert"},
            {"News", "Nachrichten"},
            {"Region", "Region"},

            // User login
            {"Login", "Einloggen"},
            {"UserName", "Nutzername"},
            {"UserNameText", "Nutzername"},
            {"Password", "Passwort"},
            {"PasswordText", "Passwort"},
            {"PasswordConfirmation", "Passwort BestÃ¤tigung"},
            {"ForgotPassword", "Passwort vergessen?"},
            { "TypeUserNameToConfirm", "Bitte schreibe den Namen des Benutzers wenn du ihn wirklich lÃ¶schen willst"},

            // Special windows
            {"SpecialWindowTitleText", "Spezieller Title des Info Fensters"},
            {"SpecialWindowTextText", "Spezieller Text des Info Fensters"},
            {"SpecialWindowColorText", "Spezielle Farbe des Info Fensters"},
            {"SpecialWindowStatusText", "Spezieller Status des Info Fensters"},
            {"WelcomeScreenManagerFor", "Willkommen Ansichtsmanager fÃ¼r"},
            {"ChangesSavedSuccessfully", "Ãnderungen erfolgreich gespeichert"},

            // User registration
            {"AvatarNameText", "Avatar Name"},
            {"AvatarScopeText", "Avatar Scope ID"},
            {"FirstNameText", "Dein Vorname"},
            {"LastNameText", "Dein Nachname"},
            {"UserAddressText", "Deine Adresse"},
            {"UserZipText", "Deine Postleitzahl"},
            {"UserCityText", "Deine Stadt"},
            {"UserCountryText", "Dein Land"},
            {"UserDOBText", "Dein Geburtsdatum (Monat Tag Jahr)"},
            {"UserEmailText", "Dein Email"},
            {"UserHomeRegionText", "Heimatregion"},
            {"RegistrationText", "Avatar Registrierung"},
            {"RegistrationsDisabled", "Registrationen sind zur Zeit leider nicht mÃ¶glich, bitte versuche es spÃ¤ter erneut."},
            {"TermsOfServiceText", "Nutzungsbedingungen"},
            {"TermsOfServiceAccept", "akzeptieren Sie die Nutzungsbedinungen, wie oben beschrieben?"},
            {"AvatarNameError", "Sie haben keinen Avatar Namen!"},
            {"AvatarPasswordError", "Passwort darf nicht leer sein!"},
            {"AvatarEmailError", "Eine E-Mail Adresse ist fÃ¼r die Passwort Wiederherstellung erforderlich! ('none', wenn unbekannt)"},
            {"AvatarNameSpacingError", "Ihr Avatar Name sollte 'Vorname Nachname' sein!"},

            // News
            {"OpenNewsManager", "Ãffne den Nachrichten Manager"},
            {"NewsManager", "Nachrichten Manager"},
            {"EditNewsItem", "Bearbeite Nachrichten"},
            {"AddNewsItem", "Neue Nachrichten hinzufÃ¼gen"},
            {"DeleteNewsItem", "Nachrichten lÃ¶schen"},
            {"NewsDateText", "Nachrichten Datum"},
            {"NewsTitleText", "Nachrichten Titel Text"},
            {"NewsItemTitle", "Nachrichten Titel"},
            {"NewsItemText", "Nachrichten Text"},
            {"AddNewsText", "Nachrichten hinzufÃ¼gen"},
            {"DeleteNewsText", "Nachrichten LÃ¶schen"},
            {"EditNewsText", "Nachrichten bearbeiten"},

            // User profile
            {"UserProfileFor", "Benutzerprofil fÃ¼r"},
            {"UsersGroupsText", "Gruppe beigetreten"},
            {"GroupNameText", "Gruppe"},
            {"UsersPicksText", "Tipps fÃ¼r die"},
            {"ResidentSince", "Einwohner seit"},
            {"AccountType", "Kontotyp"},
            {"PartnersName", "Name des Partners"},
            {"AboutMe", "Ã¼ber mich"},
            {"IsOnlineText", "User Status"},
            {"OnlineLocationText", "User Location"},
            { "Partner", "Partner"},
            { "Friends", "Friends"},
            { "Nothing", "None"},
            { "ChangePass", "Change password"},
            { "NoChangePass", "Unable to change the password, please try again later"},

            // Region Information
            {"RegionInformationText", "Region Information"},
            {"OwnerNameText", "EigentÃ¼mer"},
            {"RegionLocationText", "Region Lage"},
            {"RegionSizeText", "Region GrÃ¶Ãe"},
            {"RegionNameText", "Region Name"},
            {"RegionTypeText", "Region Art"},
            { "RegionPresetTypeText", "Region Type"},
            { "RegionDelayStartupText", "Delay starting scripts"},
            { "RegionPresetText", "Region Preset"},
            {"RegionTerrainText", "Region Terrain"},
            {"ParcelsInRegionText", "Parzellen In der Region"},
            {"ParcelNameText", "Parzellen Name"},
            {"ParcelOwnerText", "Parzellen EigentÃ¼mer"},

            // Region list
            {"RegionInfoText", "Region Info"},
            {"RegionListText", "Region Liste"},
            {"RegionLocXText", "Region X"},
            {"RegionLocYText", "Region Y"},
            {"SortByLocX", "nach RegionX sortieren"},
            {"SortByLocY", "nach RegionY sortieren"},
            {"SortByName", "nach Regionsnamen sortieren"},
            {"RegionMoreInfo", "Mehr Informationen"},
            {"RegionMoreInfoTooltips", "Mehr Informationen Ã¼ber"},
            {"OnlineUsersText", "Online Benutzer"},
            { "OnlineFriendsText", "Online Friends"},
            {"RegionOnlineText", "Region Status"},
            {"RegionMaturityText", "Access Rating"},
            {"NumberOfUsersInRegionText", "Anzahl der Benutzer in Region"},

            // Region manager
            { "AddRegionText", "In Region"},
            { "Mainland", "Festland"},
            { "Estate", "Gut"},
            { "FullRegion", "Voll Region"},
            { "Homestead", "Heimstätte"},
            { "Openspace", "Freifläche"},
            { "Flatland", "Flachland"},
            { "Grassland", "Wiese"},
            { "Island", "Insel"},
            { "Aquatic", "Wasser"},
            { "Custom", "Brauch"},
            { "RegionPortText", "Region Port"},
            { "RegionVisibilityText", "Sichtbar zu Nachbarn"},
            { "RegionInfiniteText", "Unendliche Region"},
            { "RegionCapacityText", "Region Prims"},
            { "NormalText", "Normal"},
            { "DelayedText", "Verspätet"},

            // Estate management
            {"AddEstateText", "In Immobilien"},
            {"EstateText", "Gut"},
            {"EstatesText", "Ländereien"},
            {"PricePerMeterText", "Preis pro Quadratmeter"},
            {"PublicAccessText", "Öffentlicher Zugang"},
            {"AllowVoiceText", "Lassen Stimme"},
            {"TaxFreeText", "Steuerfrei"},
            {"AllowDirectTeleportText", "Lassen Sie direkte teleporting"},

            // Menus
            {"MenuHome", "Startseite"},
            {"MenuLogin", "Login"},
            {"MenuLogout", "Logout"},
            {"MenuRegister", "Registrieren"},
            {"MenuForgotPass", "Passwort vergessen"},
            {"MenuNews", "Nachrichten"},
            {"MenuWorld", "Welt"},
            {"MenuWorldMap", "Weltkarte"},
            {"MenuRegion", "Regionsliste"},
            {"MenuUser", "Benutzer"},
            {"MenuOnlineUsers", "Benutzer online"},
            {"MenuUserSearch", "Benutzersuche"},
            {"MenuRegionSearch", "Regionssuche"},
            {"MenuChat", "Chat"},
            {"MenuHelp", "Hilfe"},
            { "MenuViewerHelp", "Viewer Hilfe"},
            {"MenuChangeUserInformation", "Ãndere Benutzer Informationen"},
            {"MenuWelcomeScreenManager", "Willkommens Bildschirm Manager"},
            {"MenuNewsManager", "Nachrichten Manager"},
            {"MenuUserManager", "Benutzer Manager"},
            {"MenuFactoryReset", "ZurÃ¼cksetzen"},
            {"ResetMenuInfoText", "Setzt das MenÃ¼ auf die meist Ã¼blichen Standardwerte zurÃ¼ck"},
            {"ResetSettingsInfoText", "Setzt die Webinterface Einstellungen auf die meist Ã¼blichen Standardwerte zurÃ¼ck"},
            {"MenuPageManager", "Seitenmanager"},
            {"MenuSettingsManager", "Einstellungsmanager"},
            {"MenuManager", "Management"},
            {"MenuSettings", "Einstellungen"},
            {"MenuRegionManager", "Region-Manager"},
            { "MenuEstateManager", "Estate Manager"},
            {"MenuManagerSimConsole", "Simulator-Konsole"},
            {"MenuPurchases", "Benutzer EinkÃ¤ufe"},
            {"MenuMyPurchases", "Meine KÃ¤ufe"},
            {"MenuTransactions", "Benutzertransaktionen"},
            {"MenuMyTransactions", "Meine VorgÃ¤nge"},
            { "MenuClassifieds", "Classifieds"},
            {"MenuMyClassifieds", "Meine Classifieds <NT>"},
            { "MenuEvents", "Events"},
            { "MenuMyEvents", "My Events"},
            {"MenuStatistics", "Viewer-Statistiken"},
            {"MenuGridSettings", "Grid-Einstellungen"},

            // Menu Tooltips
            {"TooltipsMenuHome", "Startseite"},
            {"TooltipsMenuLogin", "Login"},
            {"TooltipsMenuLogout", "Logout"},
            {"TooltipsMenuRegister", "Register"},
            {"TooltipsMenuForgotPass", "Passwort vergessen"},
            {"TooltipsMenuNews", "Nachrichten"},
            {"TooltipsMenuWorld", "Welt"},
            {"TooltipsMenuWorldMap", "Weltkarte"},
            {"TooltipsMenuUser", "Benutzer"},
            {"TooltipsMenuOnlineUsers", "Benutzer Online"},
            {"TooltipsMenuUserSearch", "Benutzersuche"},
            {"TooltipsMenuRegionSearch", "Regionssuche"},
            {"TooltipsMenuChat", "Chat"},
            {"TooltipsMenuViewerHelp", "Hilfe"},
            { "TooltipsMenuHelp", "Help"},
            {"TooltipsMenuChangeUserInformation", "Benutzerinformationen Ã¤ndern"},
            {"TooltipsMenuWelcomeScreenManager", "Willkommen Screen Manager"},
            {"TooltipsMenuNewsManager", "Nachrichten Manager"},
            {"TooltipsMenuUserManager", "Benutzermanager"},
            {"TooltipsMenuFactoryReset", "ZurÃ¼cksetzen"},
            {"TooltipsMenuPageManager", "Seitenmanager"},
            {"TooltipsMenuSettingsManager", "Einstellungsmanager"},
            {"TooltipsMenuManager", "Admin Management"},
            {"TooltipsMenuSettings", "WebUI Einstellungen"},
            {"TooltipsMenuRegionManager", "Region erstellen / bearbeiten"},
            { "TooltipsMenuEstateManager", "Estate management"},
            {"TooltipsMenuManagerSimConsole", "Online-Simulator-Konsole"},
            {"TooltipsMenuPurchases", "Kaufinformationen"},
            {"TooltipsMenuTransactions", "Transaktionsinformationen"},
            { "TooltipsMenuClassifieds", "Classifieds information"},
            { "TooltipsMenuEvents", "Event information"},
            {"TooltipsMenuStatistics", "Viewer-Statistiken"},
            {"TooltipsMenuGridSettings", "Grid-Einstellungen"},

            // Menu Region box
            {"MenuRegionTitle", "Regionen"},
            {"MenuParcelTitle", "Parzelle"},
            {"MenuOwnerTitle", "Besitzer"},
            {"TooltipsMenuRegion", "Regions Liste"},
            {"TooltipsMenuParcel", "Region Pakete"},
            {"TooltipsMenuOwner", "Immobilienbesitzer"},

            // Menu Profile box
            {"MenuProfileTitle", "Profil"},
            {"MenuGroupTitle", "Gruppe"},
            {"MenuPicksTitle", "Auswahl"},
            {"MenuRegionsTitle", "Regionen"},
            {"TooltipsMenuProfile", "Benutzerprofil"},
            {"TooltipsMenuGroups", "Benutzergruppen"},
            {"TooltipsMenuPicks", "Benutzer Picks"},
            {"TooltipsMenuRegions", "Benutzer Regionen"},
            {"UserGroupNameText", "Benutzergruppe"},
            {"PickNameText", "WÃ¤hlen Sie Namen"},
            {"PickRegionText", "Lage"},

            // Urls
            {"WelcomeScreen", "Start Bildschirm"},

            // Tooltips Urls
            {"TooltipsWelcomeScreen", "Willkommen Screen"},
            {"TooltipsWorldMap", "Weltkarte"},

            // Index
            {"HomeText", "Startseite"},
            {"HomeTextWelcome", "Dies ist unsere neue virtuelle Welt! besuche uns jetzt und erkenne den Unterschied!"},
            {"HomeTextTips", "Neue PrÃ¤sentationen"},
            {"WelcomeToText", "Willkommen bei"},

            // World Map
            {"WorldMap", "Weltkarte"},
            {"WorldMapText", "Vollbild"},

            // Chat
            {"ChatText", "Chat"},

            // Help
            {"HelpText", "Hilfe"},
            {"HelpViewersConfigText", "Viewer Konfiguration"},

            //Logout
            {"Logout", "Logout"},
            {"LoggedOutSuccessfullyText", "Du hast dich erfolgreich abgemeldet."},

            //Change user information page
            {"ChangeUserInformationText", "Ãndere Benutzer Informationen"},
            {"ChangePasswordText", "Passwort Ã¤ndern"},
            {"NewPasswordText", "Neues Passwort"},
            {"NewPasswordConfirmationText", "Neues Passwort bestÃ¤tigen"},
            {"ChangeEmailText", "Ãndere Email Adresse"},
            {"NewEmailText", "Neue Email Adresse"},
            {"DeleteUserText", "Meinen Account lÃ¶schen"},
            {"DeleteText", "lÃ¶schen"},
            {"DeleteUserInfoText",
                    "Das wird alle Informationen im Grid Ã¼ber dich und deinen Zugang zu diesem Dienst entfernen. Um dies abzuschlieÃen gebe hier deinen Benutzernamen und Passwort ein und drÃ¼cke auf lÃ¶schen."},
            {"EditUserAccountText", "bearbeite Benutzer Account"},

            // Maintenance
            {"WebsiteDownInfoText", "Die Website ist zur Zeit nicht erreichbar, bitte versuche es spÃ¤ter noch einmal."},
            {"WebsiteDownText", "Website offline"},

            // Http 404
            {"Error404Text", "Error code"},
            {"Error404InfoText", "404 Seite nicht gefunden"},
            {"HomePage404Text", "home seite"},

            // Http 505
            {"Error505Text", "Error code"},
            {"Error505InfoText", "505 interner Server Fehler"},
            {"HomePage505Text", "Startseite"},

            // User search
            {"Search", "Suche"},
            {"SearchText", "Suche"},
            {"SearchForUserText", "Suche nach einen Benutzer"},
            {"UserSearchText", "Benutzersuche"},
            {"SearchResultForUserText", "Suchergebnis fÃ¼r Benutzer"},

            // Region_search
            {"SearchForRegionText", "Suche nach einer Region"},
            {"RegionSearchText", "Region Suche"},
            {"SearchResultForRegionText", "Suchergebnis fÃ¼r Region"},

            // Edit user
            {"AdminDeleteUserText", "Benutzer lÃ¶schen"},
            {"AdminDeleteUserInfoText", "Dies lÃ¶scht den Account und zerstÃ¶rt alle damit verbundenen Daten."},
            {"BanText", "Sperren"},
            {"UnbanText", "entsperren"},
            {"AdminTempBanUserText", "vorÃ¼bergehender Benutzer Ban"},
            {"AdminTempBanUserInfoText", "Dies verhindert, daÃ sich der Benutzer fÃ¼r eine bestimmte Zeit anmelden kann."},
            {"AdminBanUserText", "Benutzer sperren"},
            {"AdminBanUserInfoText", "Dies verhindert, daÃ sich der Benutzer bis er entsperrt wurde nicht anmelden kann."},
            {"AdminUnbanUserText", "Benutzer entsperren"},
            {"AdminUnbanUserInfoText", "entferne temporÃ¤re und stÃ¤ndige sperren des Benutzers."},
            {"AdminLoginInAsUserText", "als Benutzer anmelden"},
            {"AdminLoginInAsUserInfoText",
                     "Dein Admin Account wird nun abgemeldet, dieser Benutzer wird angemeldet und du wirst sehen was sie sehen."},
            {"TimeUntilUnbannedText", "Zeit bis benutzer entsperrt wird"},
            {"BannedUntilText", "Benutzer gesperrt bis:"},
            { "KickAUserText", "Kick User"},
            {"KickAUserInfoText", "Kick einen Benutzer (wird innerhalb von 30 Sekunden abgemeldet)"},
            {"KickMessageText", "Message an Benutzer"},
            {"KickUserText", "Kick Benutzer"},
            { "MessageAUserText", "Send User A Message"},
            { "MessageAUserInfoText", "Sendet eine Blaue Box an einen Benutzer (erscheint innerhalb 30 Sekunden)"},
            { "MessageUserText", "Message User"},

            // Transactions
            {"TransactionsText", "Transaktionen"},
            {"DateInfoText", "WÃ¤hlen Sie einen Datumsbereich"},
            {"DateStartText", "Beginn Datum"},
            {"DateEndText", "Ende"},
            {"30daysPastText", "Letzten 30 Tagen"},
            { "TransactionToAgentText", "Zu Benutzer"},
            { "TransactionFromAgentText", "Von Benutzer"},
            {"TransactionDateText", "Datum"},
            {"TransactionDetailText", "Beschreibung"},
            {"TransactionAmountText", "Menge"},
            {"TransactionBalanceText", "Guthaben"},
            {"NoTransactionsText", "Keine Transaktionen gefunden..."},
            {"PurchasesText", "EinkÃ¤ufe"},
            {"LoggedIPText", "Gespeichert IP-Adresse"},
            {"NoPurchasesText", "Keine EinkÃ¤ufe gefunden..."},
            {"PurchaseCostText", "Kosten"},
            
            // Classifieds
            {"ClassifiedsText", "Rubriksanzeige"},
            { "ClassifiedText", "Classified"},
            { "ListedByText", " Eingetragen von"},
            { "CreationDateText", "Erstellungsdatun"},
            { "ExpirationDateText", "LÃ¤uft aus am" },
            { "DescriptionText", "Beschreibung" },
            { "PriceOfListingText", "Kosten"},

            // Classified categories
            { "CatAll", "All"},
            { "CatSelect", ""},
            { "CatShopping", "Shopping"},
            { "CatLandRental", "Land Miete"},
            { "CatPropertyRental", "Eigentums Miete"},
            { "CatSpecialAttraction", "Besondere Attraktion"},
            { "CatNewProducts", "Neue Produkte"},
            { "CatEmployment", "BeschÃ¤ftigung"},
            { "CatWanted", "Gesucht"},
            { "CatService", "Service"},
            { "CatPersonal", "PersÃ¶nlich"},
           
            // Events
            { "EventsText", "Events"},
            { "EventNameText", "Event"},
            { "EventLocationText", "Wo"},
            { "HostedByText","Angeboten von"},
            { "EventDateText", "Wann"},
            { "EventTimeInfoText", "Event sollte mit Server Zeit Ã¼bereinstimmen"},
            { "CoverChargeText", "EintrittsgebÃ¼hr"},
            { "DurationText", "Dauer"},
            { "AddEventText", "Event HinzufÃ¼gen"},

            // Event categories
            { "CatDiscussion", "GesprÃ¤che"},
            { "CatSports", "Sport"},
            { "CatLiveMusic", "Live Musik"},
            { "CatCommercial", "GeschÃ¤fftlich"},
            { "CatEntertainment", "Nightlife/Entertainment"},
            { "CatGames", "Spiele/Wettbewerbe"},
            { "CatPageants", "Prunk"},
            { "CatEducation", "Ausbildung"},
            { "CatArtsCulture", "Kunst/Kultue"},
            { "CatCharitySupport", "WohltÃ¤tigkeit/UnterstÃ¼tzungs Gruppe"},
            { "CatMiscellaneous", "Verschiedenes"},

            // Event lookup periods
            { "Next24Hours", "NÃ¤chsten 24 Stunden"},
            { "Next10Hours", "NÃ¤chsten 12 Stunden"},
            { "Next4Hours", "N#chsten 4 Stunden"},
            { "Next2Hours", "NÃ¤chsten 2 Stunden"},

            // Sim Console
            {"SimConsoleText", "Sim Befehls Konsole"},
            {"SimCommandText", "Befehl"},

            // Statistics
            {"StatisticsText", "Viewer-Statistiken"},
            {"ViewersText", "Viewer-Nutzung"},
            {"GPUText", "Grafikkarten"},
            {"PerformanceText", "Leistungsmittelwerte"},
            {"FPSText", "Bilder / s"},
            {"RunTimeText", "Laufzeit"},
            {"RegionsVisitedText", "Regionen besucht"},
            {"MemoryUseageText", "Speichernutzung"},
            {"PingTimeText", "Ping-Zeit"},
            {"AgentsInViewText", "Agents im Blick"},
            {"ClearStatsText", "Klare Statistiken Ã¼ber"},

            // Abuse reports
            {"MenuAbuse", "Missbrauch Berichte"},
            {"TooltipsMenuAbuse", "Benutzer abuser berichte"},
            {"AbuseReportText", "Missbrauch"},
            {"AbuserNameText", "Abuser"},
            {"AbuseReporterNameText", "Reporter"},
            {"AssignedToText", "Zugewiesen"},

            // Factory_reset
            {"FactoryReset", "ZurÃ¼cksetzen"},
            {"ResetMenuText", "MenÃ¼ zum ZurÃ¼cksetzen auf Werkseinstellungen"},
            {"ResetSettingsText", "ZurÃ¼cksetzen der Webeinstellungen (Einstellungsmanager Seite) zu den Werkseinstellungen"},
            {"Reset", "Reset"},
            {"Settings", "Einstellungen"},
            {"Pages", "Seiten"},
            { "UpdateRequired", "update erforderlich"},
            {"DefaultsUpdated",
                    "Standardwerte aktualisiert, gehe zu den Werkseinstellungen um sie zu aktualisieren oder Einstellungsmanager um diese Warnung zu deaktivieren."},

            // Page_manager
            {"PageManager", "Seitenmanager"},
            {"SaveMenuItemChanges", "Speichere MenÃ¼punkt"},
            {"SelectItem", "Punkt AuswÃ¤hlen"},
            {"DeleteItem", "Punkt lÃ¶schen"},
            {"AddItem", "Punkt hinzufÃ¼gen"},
            {"PageLocationText", "Seitenlage"},
            {"PageIDText", "Seiten ID"},
            {"PagePositionText", "Seitenposition"},
            {"PageTooltipText", "Seiten Tooltip"},
            {"PageTitleText", "Seitentitel"},
            {"RequiresLoginText", "BenÃ¶tigt Anmeldung fÃ¼r Anzeige"},
            {"RequiresLogoutText", "BenÃ¶tigt Abmeldung fÃ¼r Anzeige"},
            { "RequiresAdminText", "Requires Admin To View"},
            {"RequiresAdminLevelText", "BenÃ¶tigt Adminrechte fÃ¼r Anzeige"},

            // Grid settings
            {"GridSettingsManager", "Grid Einstellungs Manager"},
            {"GridnameText", "Grid Namen "},
            {"GridnickText", "Grid Spitznamen "},
            {"WelcomeMessageText", "Login Willkommen Nachrichten "},
            {"GovernorNameText", "System Gouverneur"},
            {"MainlandEstateNameText", "Festland Immobilien"},
            {"RealEstateOwnerNameText", "System Gutsbesitzer Namen"},
            {"SystemEstateNameText", "System Immobilienname"},
            {"BankerNameText", "System banker"},
            {"MarketPlaceOwnerNameText", "System Marktplatz Besitzer"},

            // Settings manager
            {"WebRegistrationText", "Web-Registrierungen erlaubt"},
            {"GridCenterXText", "Grid Center Location X"},
            {"GridCenterYText", "Grid Center Location Y"},
            {"SettingsManager", "Settingsmanager"},
            {"IgnorePagesUpdatesText", "Ignoriere Seitenupdate Warnung bis zum nÃ¤chsten Update"},
            {"IgnoreSettingsUpdatesText", "Ignoriere Einstellungsupdate Warnung bis zum nÃ¤chsten Update"},
            {"HideLanguageBarText", "Sprachauswahlleiste ausblenden"},
            {"HideStyleBarText", "Stil Auswahlleiste ausblenden"},
            {"HideSlideshowBarText", "Ausblenden Diashow bar"},
            {"LocalFrontPageText", "Lokale Titelseite"},
            {"LocalCSSText", "Lokale CSS-Stylesheet"},

            // Dates
            {"Sun", "Son"},
            {"Mon", "Mon"},
            {"Tue", "Die"},
            {"Wed", "Mi"},
            {"Thu", "Do"},
            {"Fri", "Fr"},
            {"Sat", "Sa"},
            {"Sunday", "Sonntag"},
            {"Monday", "Montag"},
            {"Tuesday", "Dienstag"},
            {"Wednesday", "Mittwoch"},
            {"Thursday", "Donnerstag"},
            {"Friday", "Freitag"},
            {"Saturday", "Samstag"},

            {"Jan_Short", "Jan"},
            {"Feb_Short", "Feb"},
            {"Mar_Short", "MÃ¤r"},
            {"Apr_Short", "Apr"},
            {"May_Short", "Mai"},
            {"Jun_Short", "Jun"},
            {"Jul_Short", "Jul"},
            {"Aug_Short", "Aug"},
            {"Sep_Short", "Sep"},
            {"Oct_Short", "Okt"},
            {"Nov_Short", "Nov"},
            {"Dec_Short", "Dez"},

            {"January", "Januar"},
            {"February", "Februar"},
            {"March", "MÃ¤rz"},
            {"April", "April"},
            {"May", "Mai"},
            {"June", "Juni"},
            {"July", "Juli"},
            {"August", "August"},
            {"September", "September"},
            {"October", "Oktober"},
            {"November", "November"},
            {"December", "Dezember"},

            // User types
            {"UserTypeText", "Benutzertyp"},
            {"AdminUserTypeInfoText", "Der Typ des Benutzers (Derzeit fÃ¼r die regelmÃ¤Ãige Stipendium Zahlungen verwendet wird)."},
            {"Guest", "Gast"},
            {"Resident", "Einwohner"},
            {"Member", "Mitglied"},
            {"Contractor", "Auftragnehmer"},
            {"Charter_Member", "Charter-Mitglied"},

            // ColorBox
            {"ColorBoxImageText", "Image"},
            {"ColorBoxOfText", "von"},
            {"ColorBoxPreviousText", "vorig"},
            {"ColorBoxNextText", "nÃ¤chste"},
            {"ColorBoxCloseText", "schlieÃen"},
            {"ColorBoxStartSlideshowText", "Starte Slide Show"},
            {"ColorBoxStopSlideshowText", "Stope Slide Show"},

            // Maintenance
            {"NoAccountFound", "Kein Konto gefunden"},
            {"DisplayInMenu", "Anzeige im MenÃ¼"},
            {"ParentText", "HauptmenÃ¼"},
            {"CannotSetParentToChild", "Kann MenÃ¼punkt nicht als UntermenÃ¼ festlegen."},
            {"TopLevel", "Erste Ebene"},

            // Style Switcher
            {"styles1", "Default Minimalist"},
            {"styles2", "Light Degarde"},
            {"styles3", "Blue Night"},
            {"styles4", "Dark Degrade"},
            {"styles5", "Luminus"},
            {"StyleSwitcherStylesText", "Arten"},
            {"StyleSwitcherLanguagesText", "Sprachen"},
            {"StyleSwitcherChoiceText", "Auswahl"},

            // Language Switcher Tooltips
            { "en", "English"},
            { "fr", "Fran?ais"},
            { "de", "Deutsch"},
            { "it", "Italiano"},
            { "es", "Espa?ol"},
            { "nl", "Nederlands"},
            { "ru", "§²§å§ã§ã§Ü§Ú§Û"}
        };

        public void Serialize (string basePath)
        {
            TranslatorSerialization.Serialize (basePath, FullLanguageName, LanguageName, dictionary);
        }

        public void Deserialize (string basePath)
        {
            var newdict = TranslatorSerialization.Deserialize (basePath, LanguageName);
            if (newdict.Count > 0)
                dictionary = newdict;
        }
    }
}
