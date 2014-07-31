﻿namespace WhiteCore.Modules.Web.Translators
{
    public class GermanTranslation : ITranslator
    {
        public string LanguageName
        {
            get { return "de"; }
        }

        public string GetTranslatedString(string key)
        {
            switch (key)
            {
                case "GridStatus":
                    return "Grid Status";
                case "Online":
                    return "Online";
                case "Offline":
                    return "Offline";
                case "TotalUserCount":
                    return "Einwohner";
                case "TotalRegionCount":
                    return "Regionen";
                case "UniqueVisitors":
                    return "Aktive Nutzer letzten 30 Tage";
                case "OnlineNow":
                    return "Jetzt Online";
                case "HyperGrid":
                    return "HyperGrid (HG)";
                case "Voice":
                    return "Stimme";
                case "Currency":
                    return "Devisen";
                case "Disabled":
                    return "Deaktiviert";
                case "Enabled":
                    return "Aktiviert";
                case "News":
                    return "Nachrichten";
                case "Region":
                    return "Region";
                case "Login":
                    return "Einloggen";
                case "UserName":
                    return "Nutzername";
                case "UserNameText":
                    return "Nutzername";
                case "Password":
                    return "Password";
                case "PasswordText":
                    return "Password";
                case "PasswordConfirmation":
                    return "Password Confirmation";
                case "ForgotPassword":
                    return "Passwort vergessen?";
                case "Submit":
                    return "Einreichen";


                case "SpecialWindowTitleText":
                    return "Spezieller Title des Info Fensters";
                case "SpecialWindowTextText":
                    return "Spezieller Text des Info Fensters";
                case "SpecialWindowColorText":
                    return "Spezielle Farbe des Info Fensters";
                case "SpecialWindowStatusText":
                    return "Spezieller Status des Info Fensters";
                case "WelcomeScreenManagerFor":
                    return "Willkommen Ansichtsmanager für";
                case "ChangesSavedSuccessfully":
                    return "Änderungen erfolgreich gespeichert";


                case "AvatarNameText":
                    return "Avatar Name";
                case "AvatarScopeText":
                    return "Avatar Scope ID";
                case "FirstNameText":
                    return "Dein Vorname";
                case "LastNameText":
                    return "Dein Nachname";
                case "UserAddressText":
                    return "Deine Adresse";
                case "UserZipText":
                    return "Deine Postleitzahl";
                case "UserCityText":
                    return "Deine Stadt";
                case "UserCountryText":
                    return "Dein Land";
                case "UserDOBText":
                    return "Dein Geburtsdatum (Monat Tag Jahr)";
                case "UserEmailText":
                    return "Dein Email";
                case "RegistrationText":
                    return "Avatar Registrierung";
                case "RegistrationsDisabled":
                    return "Registrationen sind zur Zeit leider nicht möglich, bitte versuche es später erneut.";
                case "TermsOfServiceText":
                    return "Nutzungsbedingungen";
                case "TermsOfServiceAccept":
                    return "akzeptieren Sie die Nutzungsbedinungen, wie oben beschrieben?";
                case "AvatarNameError":
                    return "Sie haben keinen Avatar Namen!";
                case "AvatarPasswordError":
                    return "Passwort darf nicht leer sein!";
                case "AvatarEmailError":
                return "Eine E-Mail Adresse ist für die Passwort Wiederherstellung erforderlich! ('none', wenn unbekannt)";

                    // news
                case "OpenNewsManager":
                    return "Öffne den Nachrichten Manager";
                case "NewsManager":
                    return "Nachrichten Manager";
                case "EditNewsItem":
                    return "Bearbeite Nachrichten";
                case "AddNewsItem":
                    return "Neue Nachrichten hinzufügen";
                case "DeleteNewsItem":
                    return "Nachrichten löschen";
                case "NewsDateText":
                    return "Nachrichten Datum";
                case "NewsTitleText":
                    return "Nachrichten Titel Text";
                case "NewsItemTitle":
                    return "Nachrichten Titel";
                case "NewsItemText":
                    return "Nachrichten Text";
                case "AddNewsText":
                    return "Nachrichten hinzufügen";
                case "DeleteNewsText":
                    return "Nachrichten Löschen";
                case "EditNewsText":
                    return "Nachrichten bearbeiten";
                case "UserProfileFor":
                    return "Benutzerprofil für";
                case "ResidentSince":
                    return "Einwohner seit";
                case "AccountType":
                    return "Kontotyp";
                case "PartnersName":
                    return "Name des Partners";
                case "AboutMe":
                    return "über mich";
                case "IsOnlineText":
                    return "User Status";
                case "OnlineLocationText":
                    return "User Location";

                case "RegionInformationText":
                    return "Region Information";
                case "OwnerNameText":
                    return "Eigentümer";
                case "RegionLocationText":
                    return "Region Lage";
                case "RegionSizeText":
                    return "Region Größe";
                case "RegionNameText":
                    return "Region Name";
                case "RegionTypeText":
                    return "Region Art";
            case "RegionTerrainText":
                return "Region Terrain";
                case "ParcelsInRegionText":
                    return "Parzellen In der Region";
                case "ParcelNameText":
                    return "Parzellen Name";
                case "ParcelOwnerText":
                    return "Parzellen Eigentümer";

                    // Region Page
                case "RegionInfoText":
                    return "Region Info";
                case "RegionListText":
                    return "Region Liste";
                case "RegionLocXText":
                    return "Region X";
                case "RegionLocYText":
                    return "Region Y";
                case "SortByLocX":
                    return "nach RegionX sortieren";
                case "SortByLocY":
                    return "nach RegionY sortieren";
                case "SortByName":
                    return "nach Regionsnamen sortieren";
                case "RegionMoreInfo":
                    return "Mehr Informationen";
                case "RegionMoreInfoTooltips":
                    return "Mehr Informationen über";
                case "FirstText":
                    return "Erste";
                case "BackText":
                    return "Zurück";
                case "NextText":
                    return "Vor";
                case "LastText":
                    return "Letzte";
                case "CurrentPageText":
                    return "Aktuelle Seite";
                case "MoreInfoText":
                    return "Mehr Informationen";
                case "OnlineUsersText":
                    return "Online Benutzer";
                case "RegionOnlineText":
                    return "Region Status";
                case "NumberOfUsersInRegionText":
                    return "Anzahl der Benutzer in Region";

                    // Menu Buttons
                case "MenuHome":
                    return "Home";
                case "MenuLogin":
                    return "Login";
                case "MenuLogout":
                    return "Logout";
                case "MenuRegister":
                    return "Registrieren";
                case "MenuForgotPass":
                    return "Passwort vergessen";
                case "MenuNews":
                    return "Nachrichten";
                case "MenuWorld":
                    return "Welt";
                case "MenuWorldMap":
                    return "Weltkarte";
                case "MenuRegion":
                    return "Regionsliste";
                case "MenuUser":
                    return "Benutzer";
                case "MenuOnlineUsers":
                    return "Benutzer online";
                case "MenuUserSearch":
                    return "Benutzersuche";
                case "MenuRegionSearch":
                    return "Regionssuche";
                case "MenuChat":
                    return "Chat";
                case "MenuHelp":
                    return "Hilfe";
                case "MenuChangeUserInformation":
                    return "Ändere Benutzer Informationen";
                case "MenuWelcomeScreenManager":
                    return "Willkommen Screen Manager";
                case "MenuNewsManager":
                    return "Nachrichten Manager";
                case "MenuUserManager":
                    return "Benutzer Manager";
                case "MenuFactoryReset":
                    return "Zurücksetzen";
                case "ResetMenuInfoText":
                    return "Setzt das Menü auf die meist üblichen Standardwerte zurück";
                case "ResetSettingsInfoText":
                    return "Setzt die Webinterface Einstellungen auf die meist üblichen Standardwerte zurück";
                case "MenuPageManager":
                    return "Seitenmanager";
                case "MenuSettingsManager":
                    return "Einstellungsmanager";
                case "MenuManager":
                    return "Admin";

                    // Tooltips Menu Buttons
                case "TooltipsMenuHome":
                    return "Home";
                case "TooltipsMenuLogin":
                    return "Login";
                case "TooltipsMenuLogout":
                    return "Logout";
                case "TooltipsMenuRegister":
                    return "Register";
                case "TooltipsMenuForgotPass":
                    return "Passwort vergessen";
                case "TooltipsMenuNews":
                    return "Nachrichten";
                case "TooltipsMenuWorld":
                    return "Welt";
                case "TooltipsMenuWorldMap":
                    return "Weltkarte";
                case "TooltipsMenuRegion":
                    return "Regions Liste";
                case "TooltipsMenuUser":
                    return "Benutzer";
                case "TooltipsMenuOnlineUsers":
                    return "Benutzer Online";
                case "TooltipsMenuUserSearch":
                    return "Benutzersuche";
                case "TooltipsMenuRegionSearch":
                    return "Regionssuche";
                case "TooltipsMenuChat":
                    return "Chat";
                case "TooltipsMenuHelp":
                    return "Hilfe";
                case "TooltipsMenuChangeUserInformation":
                    return "Benutzerinformationen ändern";
                case "TooltipsMenuWelcomeScreenManager":
                    return "Willkommen Screen Manager";
                case "TooltipsMenuNewsManager":
                    return "Nachrichten Manager";
                case "TooltipsMenuUserManager":
                    return "Benutzermanager";
                case "TooltipsMenuFactoryReset":
                    return "Zurücksetzen";
                case "TooltipsMenuPageManager":
                    return "Seitenmanager";
                case "TooltipsMenuSettingsManager":
                    return "Einstellungsmanager";
                case "TooltipsMenuManager":
                    return "Admin Management";

                    // Menu Region
                case "MenuRegionTitle":
                    return "Regionen";
                case "MenuParcelTitle":
                    return "Parzelle";
                case "MenuOwnerTitle":
                    return "Owner";

                    // Menu Profile
                case "MenuProfileTitle":
                    return "Profil";
                case "MenuGroupTitle":
                    return "Gruppe";
                case "MenuPicksTitle":
                    return "Auswahl";

                    // Urls
                case "WelcomeScreen":
                    return "Willkommen Screen";

                    // Tooltips Urls
                case "TooltipsWelcomeScreen":
                    return "Willkommen Screen";
                case "TooltipsWorldMap":
                    return "Weltkarte";

                    // Style Switcher
                case "styles1":
                    return "Default Minimalist";
                case "styles2":
                    return "Light Degarde";
                case "styles3":
                    return "Blue Night";
                case "styles4":
                    return "Dark Degrade";
                case "styles5":

                    return "Luminus";
                case "StyleSwitcherStylesText":
                    return "Arten";
                case "StyleSwitcherLanguagesText":
                    return "Sprachen";
                case "StyleSwitcherChoiceText":
                    return "Auswahl";

                    // Language Switcher Tooltips
            case "en": 
                return "Englisch";
            case "fr": 
                return "Französisch";
            case "de": 
                return "Deutsch";
            case "it": 
                return "Italienisch";
            case "es": 
                return "Spanisch";
            case "nl": 
                return "Holländisch";

                    // Index Page
                case "HomeText":
                    return "Home";
                case "HomeTextWelcome":
                    return "Hier ist unsere neue virtuelle Welt! besuche uns jetzt und erkenne den Unterschied!";
                case "HomeTextTips":
                    return "Neue Präsentationen";
                case "WelcomeToText":
                    return "Willkommen bei";

                    // World Map Page
                case "WorldMap":
                    return "Weltkarte";
                case "WorldMapText":
                    return "Vollbild";

                    // Chat Page
                case "ChatText":
                    return "Chat";

                    // Help Page
                case "HelpText":
                    return "Hilfe";
                case "HelpViewersConfigText":
                    return "Viewer Konfiguration";
                case "AngstromViewer":
                    return "Angstrom Viewer";
                case "AstraViewer":
                    return "Astra Viewer";
                case "FirestormViewer":
                    return "Firestorm Viewer";
                case "KokuaViewer":
                    return "Kokua Viewer";
                case "ImprudenceViewer":
                    return "Imprudence Viewer";
                case "PhoenixViewer":
                    return "Phoenix Viewer";
                case "SingularityViewer":
                    return "Singularity Viewer";
                case "VoodooViewer":
                    return "Voodoo Viewer";
                case "ZenViewer":
                    return "Zen Viewer";

                    //Logout page
                case "LoggedOutSuccessfullyText":
                    return "Du hast dich erfolgreich abgemeldet.";
                case "Logout":
                    return "Logout";

                    //Change user information page
                case "ChangeUserInformationText":
                    return "Ändere Benutzer Informationen";
                case "ChangePasswordText":
                    return "Passwort ändern";
                case "NewPasswordText":
                    return "Neues Passwort";
                case "NewPasswordConfirmationText":
                    return "Neues Passwort bestätigen";
                case "ChangeEmailText":
                    return "Ändere Email Adresse";
                case "NewEmailText":
                    return "Neue Email Adresse";
                case "DeleteUserText":
                    return "Meinen Account löschen";
                case "DeleteText":
                    return "löschen";
                case "DeleteUserInfoText":
                    return
                        "Das wird alle Informationen im Grid über dich und deinen Zugang zu diesem Dienst entfernen. Um dies abzuschließen gebe hier deinen Benutzernamen und Passwort ein und drücke auf löschen.";
                case "EditText":
                    return "bearbeiten";
                case "EditUserAccountText":
                    return "bearbeite Benutzer Account";

                    //Maintenance page
                case "WebsiteDownInfoText":
                    return "Die Website ist zur Zeit nicht erreichbar, bitte versuche es später noch einmal.";
                case "WebsiteDownText":
                    return "Website offline";

                    //http_404 page
                case "Error404Text":
                    return "Error code";
                case "Error404InfoText":
                    return "404 Seite nicht gefunden";
                case "HomePage404Text":
                    return "home seite";

                    //http_505 page
                case "Error505Text":
                    return "Error code";
                case "Error505InfoText":
                    return "505 interner Server Fehler";
                case "HomePage505Text":
                    return "home seite";

                    //user_search page
                case "Search":
                    return "Suche";
                case "SearchText":
                    return "Suche";
                case "SearchForUserText":
                    return "Suche nach einen Benutzer";
                case "UserSearchText":
                    return "Benutzersuche";
                case "SearchResultForUserText":
                    return "Suchergebnis für Benutzer";

                    //region_search page
                case "SearchForRegionText":
                    return "Suche nach einer Region";
                case "RegionSearchText":
                    return "Region Suche";
                case "SearchResultForRegionText":
                    return "Suchergebnis für Region";

                    //Edit user page
                case "AdminDeleteUserText":
                    return "Benutzer löschen";
                case "AdminDeleteUserInfoText":
                    return "Dies löscht den Account und zerstört alle damit verbundenen Daten.";
                case "BanText":
                    return "Sperren";
                case "UnbanText":
                    return "entsperren";
                case "AdminTempBanUserText":
                    return "vorübergehender Benutzer Ban";
                case "AdminTempBanUserInfoText":
                    return "Dies verhindert, daß sich der Benutzer für eine bestimmte Zeit anmelden kann.";
                case "AdminBanUserText":
                    return "Benutzer sperren";
                case "AdminBanUserInfoText":
                    return "Dies verhindert, daß sich der Benutzer bis er entsperrt wurde nicht anmelden kann.";
                case "AdminUnbanUserText":
                    return "Benutzer entsperren";
                case "AdminUnbanUserInfoText":
                    return "entferne temporäre und ständige sperren des Benutzers.";
                case "AdminLoginInAsUserText":
                    return "als Benutzer anmelden";
                case "AdminLoginInAsUserInfoText":
                    return
                         "Dein Admin Account wird nun abgemeldet, dieser Benutzer wird angemeldet und du wirst sehen was sie sehen.";
                case "TimeUntilUnbannedText":
                    return "Zeit bis benutzer entsperrt wird";
                case "DaysText":
                    return "Tage";
                case "HoursText":
                    return "Stunden";
                case "MinutesText":
                    return "Minuten";
                case "EdittingText":
                    return "Bearbeiten";
                case "BannedUntilText":
                    return "Benutzer gesperrt bis:";
                case "KickAUserText":
                    return "Kick einen Benutzer (wird innerhalb von 30 Sekunden abgemeldet)";
                case "KickMessageText":
                    return "Message an Benutzer";
                case "KickUserText":
                    return "Kick Benutzer";

                    //factory_reset
                case "FactoryReset":
                    return "Zurücksetzen";
                case "ResetMenuText":
                    return "Menü zum Zurücksetzen auf Werkseinstellungen";
                case "ResetSettingsText":
                    return "Zurücksetzen der Webeinstellungen (Einstellungsmanager Seite) zu den Werkseinstellungen";
                case "Reset":
                    return "Reset";
                case "Settings":
                    return "Einstellungen";
                case "Pages":
                    return "Seiten";
                case "DefaultsUpdated":
                    return
                        "Standardwerte aktualisiert, gehe zu den Werkseinstellungen um sie zu aktualisieren oder Einstellungsmanager um diese Warnung zu deaktivieren.";

                    //page_manager
                case "PageManager":
                    return "Seitenmanager";
                case "SaveMenuItemChanges":
                    return "Speichere Menüpunkt";
                case "SelectItem":
                    return "Punkt Auswählen";
                case "DeleteItem":
                    return "Punkt löschen";
                case "AddItem":
                    return "Punkt hinzufügen";
                case "PageLocationText":
                    return "Seitenlage";
                case "PageIDText":
                    return "Seiten ID";
                case "PagePositionText":
                    return "Seitenposition";
                case "PageTooltipText":
                    return "Seiten Tooltip";
                case "PageTitleText":
                    return "Seitentitel";
                case "No":
                    return "nein";
                case "Yes":
                    return "ja";
                case "RequiresLoginText":
                    return "Benötigt Anmeldung für Anzeige";
                case "RequiresLogoutText":
                    return "Benötigt Abmeldung für Anzeige";
                case "RequiresAdminText":
                    return "Benötigt Adminrechte für Anzeige";

                    //settings manager page
                case "Save":
                    return "Speichern";
                case "WebRegistrationText":
                    return "Web-Registrierungen erlaubt";
                case "GridCenterXText":
                    return "Grid Center Location X";
                case "GridCenterYText":
                    return "Grid Center Location Y";
                case "GoogleMapAPIKeyText":
                    return "Google Maps API Key";
                case "GoogleMapAPIKeyHelpText":
                    return "The google maps v2 api key erstellt";
                case "SettingsManager":
                    return "Settingsmanager";
                case "IgnorePagesUpdatesText":
                    return "Ignoriere Seitenupdate Warnung bis zum nächsten Update";
                case "IgnoreSettingsUpdatesText":
                    return "Ignoriere Einstellungsupdate Warnung bis zum nächsten Update";

                    //Times
                case "Sun":
                    return "Son";
                case "Mon":
                    return "Mon";
                case "Tue":
                    return "Die";
                case "Wed":
                    return "Mi";
                case "Thu":
                    return "Do";
                case "Fri":
                    return "Fr";
                case "Sat":
                    return "Sa";
                case "Sunday":
                    return "Sonntag";
                case "Monday":
                    return "Montag";
                case "Tuesday":
                    return "Dienstag";
                case "Wednesday":
                    return "Mittwoch";
                case "Thursday":
                    return "Donnerstag";
                case "Friday":
                    return "Freitag";
                case "Saturday":
                    return "Samstag";

                case "Jan_Short":
                    return "Jan";
                case "Feb_Short":
                    return "Feb";
                case "Mar_Short":
                    return "Mär";
                case "Apr_Short":
                    return "Apr";
                case "May_Short":
                    return "Mai";
                case "Jun_Short":
                    return "Jun";
                case "Jul_Short":
                    return "Jul";
                case "Aug_Short":
                    return "Aug";
                case "Sep_Short":
                    return "Sep";
                case "Oct_Short":
                    return "Okt";
                case "Nov_Short":
                    return "Nov";
                case "Dec_Short":
                    return "Dez";

                case "January":
                    return "Januar";
                case "February":
                    return "Februar";
                case "March":
                    return "März";
                case "April":
                    return "April";
                case "May":
                    return "Mai";
                case "June":
                    return "Juni";
                case "July":
                    return "Juli";
                case "August":
                    return "August";
                case "September":
                    return "September";
                case "October":
                    return "Oktober";
                case "November":
                    return "November";
                case "December":
                    return "Dezember";

                // User types
            case "UserTypeText":
                return "Benutzertyp";
            case "AdminUserTypeInfoText":
                return "Der Typ des Benutzers (Derzeit für die regelmäßige Stipendium Zahlungen verwendet wird).";
            case "Guest":
                return "Gast";
            case  "Resident":
                return "Einwohner";
            case "Member":
                return "Mitglied";
            case "Contractor":
                return "Auftragnehmer";
            case "Charter_Member":
                return "Charter-Mitglied";

                    // ColorBox
                case "ColorBoxImageText":
                    return "Image";
                case "ColorBoxOfText":
                    return "von";
                case "ColorBoxPreviousText":
                    return "vorig";
                case "ColorBoxNextText":
                    return "nächste";
                case "ColorBoxCloseText":
                    return "schließen";
                case "ColorBoxStartSlideshowText":
                    return "Starte Slide Show";
                case "ColorBoxStopSlideshowText":
                    return "Stope Slide Show";

                // Maintenance
            case "NoAccountFound":
                return "Kein Konto gefunden";
            case "DisplayInMenu":
                return "Anzeige im Menü";
            case "ParentText":
                return "Menü Mutter";
            case "CannotSetParentToChild":
                return "Kann nicht Menüpunkt als Kind selbst festgelegt.";
            case "TopLevel":
                return "Erste Ebene";
            case "HideLanguageBarText":
                return "Sprachauswahlleiste ausblenden";
            case "HideStyleBarText":
                return "Stil Auswahlleiste ausblenden";

            }
            return "UNKNOWN CHARACTER";
        }
    }
}