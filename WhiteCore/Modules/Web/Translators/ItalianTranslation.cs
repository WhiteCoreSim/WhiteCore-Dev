﻿namespace WhiteCore.Modules.Web.Translators
{
    public class ItalianTranslation : ITranslator
    {
        public string LanguageName
        {
            get { return "it"; }
        }

        public string GetTranslatedString(string key)
        {
            switch (key)
            {
                case "GridStatus":
                    return "Stato della Grid";
                case "Online":
                    return "Online";
                case "Offline":
                    return "Offline";
                case "TotalUserCount":
                    return "Utenti totali";
                case "TotalRegionCount":
                    return "Regioni totali";
                case "UniqueVisitors":
                    return "Visitatori unici ultimi 30 giorni";
                case "OnlineNow":
                    return "Online Adesso";
                case "HyperGrid":
                    return "HyperGrid (HG)";
                case "Voice":
                    return "Voice";
                case "Currency":
                    return "Valuta";
                case "Disabled":
                    return "Disabilitato";
                case "Enabled":
                    return "Abilitato";
                case "News":
                    return "News";
                case "Region":
                    return "Regione";
                case "Login":
                    return "Login";
                case "UserName":
                case "UserNameText":
                    return "Nome Utente";
                case "Password":
                case "PasswordText":
                    return "Password";
                case "PasswordConfirmation":
                    return "Conferma Password";
                case "ForgotPassword":
                    return "Password dimenticata?";
                case "Submit":
                    return "Invia";


                case "SpecialWindowTitleText":
                    return "Titolo sezione Info Speciali";
                case "SpecialWindowTextText":
                    return "Testo sezione Info Speciali";
                case "SpecialWindowColorText":
                    return "Colore sezione Info Specialir";
                case "SpecialWindowStatusText":
                    return "Stato sezione Info Speciali";
                case "WelcomeScreenManagerFor":
                    return "Manager della pagina di benvenuto per";
                case "ChangesSavedSuccessfully":
                    return "I cambiamenti saranno salvati in seguito";


                case "AvatarNameText":
                    return "Nome Avatar";
                case "AvatarScopeText":
                    return "Avatar Scope ID";
                case "FirstNameText":
                    return "Il tuo Nome";
                case "LastNameText":
                    return "Il tuo Cognome";
                case "UserAddressText":
                    return "Il tuo indirizzo";
                case "UserZipText":
                    return "Il tuo codice postale";
                case "UserCityText":
                    return "La tua citta";
                case "UserCountryText":
                    return "Il tuo paese";
                case "UserDOBText":
                    return "La tua data di nascita (Mese Giorno Anno)";
                case "UserEmailText":
                    return "La tua Email";
                case "RegistrationText":
                    return "Registrazione Avatar";
                case "RegistrationsDisabled":
                    return
                        "La registrazione e attualmente disabilitata. Ti preghiamo di tornare su questa pagina piu tardi";
                case "TermsOfServiceText":
                    return "Termini di Servizio";
                case "TermsOfServiceAccept":
                    return "Accetti i Termini di Servizio descritti qui sopra?";
                case "AvatarNameError":
                    return "Non è stato immesso un nome di avatar!";
                case "AvatarPasswordError":
                    return "La password è vuota o non corrispondenti!";
                case "AvatarEmailError":
                return "L'indirizzo email è necessario per il ripristino della password! ('none' se sconosciuta)";

                    // news
                case "OpenNewsManager":
                    return "Apri il manager delle News";
                case "NewsManager":
                    return "Manager delle News";
                case "EditNewsItem":
                    return "Modifica le News";
                case "AddNewsItem":
                    return "Aggiungi una nuova News";
                case "DeleteNewsItem":
                    return "Elimina una News";
                case "NewsDateText":
                    return "Data della News";
                case "NewsTitleText":
                    return "Testo della News";
                case "NewsItemTitle":
                    return "Titolo della News";
                case "NewsItemText":
                    return "Testo della nuova News";
                case "AddNewsText":
                    return "Aggiungi News";
                case "DeleteNewsText":
                    return "Elimina News";
                case "EditNewsText":
                    return "Modifica News";
                case "UserProfileFor":
                    return "Profilo Utente per";
                case "ResidentSince":
                    return "Residente dal";
                case "AccountType":
                    return "Tipo di Account";
                case "PartnersName":
                    return "Nome del Partner";
                case "AboutMe":
                    return "Il mio Profilo";
                case "IsOnlineText":
                    return "Stato dell utente";
                case "OnlineLocationText":
                    return "Posizione dell Utente";

                case "RegionInformationText":
                    return "Informazioni sulla Regione";
                case "OwnerNameText":
                    return "Proprietario";
                case "RegionLocationText":
                    return "Posizione della Regione";
                case "RegionSizeText":
                    return "Dimensione della Regione";
                case "RegionNameText":
                    return "Nome della Regione";
                case "RegionTypeText":
                    return "Tipo della Regione";
            case "RegionTerrainText":
                return "Regione Terrain";
                case "ParcelsInRegionText":
                    return "Terreni della Regione";
                case "ParcelNameText":
                    return "Nome del Terreno";
                case "ParcelOwnerText":
                    return "Proprietario del Terreno";

                    // Region Page
                case "RegionInfoText":
                    return "Info Regione";
                case "RegionListText":
                    return "Lista Regioni";
                case "RegionLocXText":
                    return "Coordinate X della Regione";
                case "RegionLocYText":
                    return "Coordinate Y della Regione";
                case "SortByLocX":
                    return "Ordina secondo la coordinata X";
                case "SortByLocY":
                    return "Ordina secondo la coordinata Y";
                case "SortByName":
                    return "Ordina secondo il nome della Regione";
                case "RegionMoreInfo":
                    return "Piu informazioni";
                case "RegionMoreInfoTooltips":
                    return "Piu informazioni su";
                case "FirstText":
                    return "Primo";
                case "BackText":
                    return "Precedente";
                case "NextText":
                    return "Prossimo";
                case "LastText":
                    return "Ultimo";
                case "CurrentPageText":
                    return "Pagina corrente";
                case "MoreInfoText":
                    return "Piu informazioni";
                case "OnlineUsersText":
                    return "Utenti Online";
                case "RegionOnlineText":
                    return "Stato della Regione";
                case "NumberOfUsersInRegionText":
                    return "Numero di Utenti nella Regione";

                    // Menu Buttons
                case "MenuHome":
                    return "Home";
                case "MenuLogin":
                    return "Entra";
                case "MenuLogout":
                    return "Esci";
                case "MenuRegister":
                    return "Registrati";
                case "MenuForgotPass":
                    return "Password dimenticata";
                case "MenuNews":
                    return "News";
                case "MenuWorld":
                    return "Mondo";
                case "MenuWorldMap":
                    return "Mappa del Mondo";
                case "MenuRegion":
                    return "Lista Regioni";
                case "MenuUser":
                    return "Utente";
                case "MenuOnlineUsers":
                    return "Utenti Online";
                case "MenuUserSearch":
                    return "Ricerca Utenti";
                case "MenuRegionSearch":
                    return "Ricerca Regione";
                case "MenuChat":
                    return "Chat";
                case "MenuHelp":
                    return "Aiuto";
                case "MenuChangeUserInformation":
                    return "Cambia Informazioni Utente";
                case "MenuWelcomeScreenManager":
                    return "Manager dello pagina di benvenuto";
                case "MenuNewsManager":
                    return "Manager delle News";
                case "MenuUserManager":
                    return "Manager degli Utenti";
                case "MenuFactoryReset":
                    return "Reset impostazioni iniziali";
                case "ResetMenuInfoText":
                    return "Resetta gli elementi del menu alle ultime impostazioni di default";
                case "ResetSettingsInfoText":
                    return "Resetta gli elementi della interfaccia Web alle ultime impostazioni di default";
                case "MenuPageManager":
                    return "Modifica delle Pagione";
                case "MenuSettingsManager":
                    return "Modifica delle Impostazioni";
                case "MenuManager":
                    return "Admin";

                    // Tooltips Menu Buttons
                case "TooltipsMenuHome":
                    return "Home";
                case "TooltipsMenuLogin":
                    return "Entra";
                case "TooltipsMenuLogout":
                    return "Esci";
                case "TooltipsMenuRegister":
                    return "Registrati";
                case "TooltipsMenuForgotPass":
                    return "Password dimenticata";
                case "TooltipsMenuNews":
                    return "News";
                case "TooltipsMenuWorld":
                    return "Mondo";
                case "TooltipsMenuWorldMap":
                    return "Mappa del Mondo";
                case "TooltipsMenuRegion":
                    return "Lista Regioni";
                case "TooltipsMenuUser":
                    return "Utente";
                case "TooltipsMenuOnlineUsers":
                    return "Utenti Online";
                case "TooltipsMenuUserSearch":
                    return "Ricerca Utenti";
                case "TooltipsMenuRegionSearch":
                    return "Ricerca Regione";
                case "TooltipsMenuChat":
                    return "Chat";
                case "TooltipsMenuHelp":
                    return "Aiuto";
                case "TooltipsMenuChangeUserInformation":
                    return "Modifica Impostazioni Utente";
                case "TooltipsMenuWelcomeScreenManager":
                    return "Manager pagina di benvenuto";
                case "TooltipsMenuNewsManager":
                    return "Manager delle News";
                case "TooltipsMenuUserManager":
                    return "Manager degli Utenti";
                case "TooltipsMenuFactoryReset":
                    return "Reset alle impostazioni iniziali";
                case "TooltipsMenuPageManager":
                    return "Manager delle Pagine";
                case "TooltipsMenuSettingsManager":
                    return "Manager delle impostazioni";
                case "TooltipsMenuManager":
                    return "Impostaioni Amministratore";

                    // Menu Region
                case "MenuRegionTitle":
                    return "Regione";
                case "MenuParcelTitle":
                    return "Terreno";
                case "MenuOwnerTitle":
                    return "Proprietario";

                    // Menu Profile
                case "MenuProfileTitle":
                    return "Profilo";
                case "MenuGroupTitle":
                    return "Gruppo";
                case "MenuPicksTitle":
                    return "Preferiti";

                    // Urls
                case "WelcomeScreen":
                    return "Pagina di Benvenuto";

                    // Tooltips Urls
                case "TooltipsWelcomeScreen":
                    return "Pagina di Benvenuto";
                case "TooltipsWorldMap":
                    return "Mappa del Mondo";

                    // Style Switcher
                case "styles1":
                    return "Default Minimalista";
                case "styles2":
                    return "Light Degarde";
                case "styles3":
                    return "Blue Night";
                case "styles4":
                    return "Dark Degrade";
                case "styles5":
                    return "Luminus";

                case "StyleSwitcherStylesText":
                    return "Stili";
                case "StyleSwitcherLanguagesText":
                    return "Lingua";
                case "StyleSwitcherChoiceText":
                    return "Seleziona";

                    // Language Switcher Tooltips
            case "en": 
                return "Inglese";
            case "fr":
                return "Francese";
            case "de":
                return "Tedesco";
            case "it":
                return "Italiano";
            case "es":
                return "Spagnolo";
            case "nl":
                return "Olandese";

                    // Index Page
                case "HomeText":
                    return "Home";
                case "HomeTextWelcome":
                    return "Questo e il nostro nuovo mondo virtuale! Registrati gratis e fai la differenza!";
                case "HomeTextTips":
                    return "Nuove presentazioni";
                case "WelcomeToText":
                    return "Benvenuto a";

                    // World Map Page
                case "WorldMap":
                    return "Mappa del mondo";
                case "WorldMapText":
                    return "Schermo intero";

                    // Chat Page
                case "ChatText":
                    return "Chat di Supporto";

                    // Help Page
                case "HelpText":
                    return "Aiuto";
                case "HelpViewersConfigText":
                    return "Configurazione Viewer";
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
                    return "Sei stato disconnesso.";
                case "Logout":
                    return "Disconnetti";

                    //Change user information page
                case "ChangeUserInformationText":
                    return "Modifica informazioni utente";
                case "ChangePasswordText":
                    return "Modifica Password";
                case "NewPasswordText":
                    return "Nuova Password";
                case "NewPasswordConfirmationText":
                    return "Nuova Password (Conferma)";
                case "ChangeEmailText":
                    return "Mofifica indirizzo Email";
                case "NewEmailText":
                    return "Nuovo indirizzo Email";
                case "DeleteUserText":
                    return "Cancella il mio Account";
                case "DeleteText":
                    return "Cancella";
                case "DeleteUserInfoText":
                    return
                        "Questa operazione eliminera i tuoi dati nella grid e non ti consentira di accedere di nuovo. Se davvero vuoi continuare inserisci di nuovo la tua password e clicca su Cancella.";
                case "EditText":
                    return "Modifica";
                case "EditUserAccountText":
                    return "Modifica Account Utente";

                    //Maintenance page
                case "WebsiteDownInfoText":
                    return "Il sito web e attualmente offline, ti preghiamo di provare piu tardi.";
                case "WebsiteDownText":
                    return "Il sito web e offline";

                    //http_404 page
                case "Error404Text":
                    return "Codice Errore";
                case "Error404InfoText":
                    return "404 Pagina non trovata";
                case "HomePage404Text":
                    return "home page";

                    //http_505 page
                case "Error505Text":
                    return "Ccodice errore";
                case "Error505InfoText":
                    return "505 Errore interno del server";
                case "HomePage505Text":
                    return "home page";

                    //user_search page
                case "Search":
                    return "Cerca";
                case "SearchText":
                    return "Cerca";
                case "SearchForUserText":
                    return "Cerca un utente";
                case "UserSearchText":
                    return "Ricerca Utente";
                case "SearchResultForUserText":
                    return "Risultati della ricerca Utente";

                    //region_search page
                case "SearchForRegionText":
                    return "Cerca una Regione";
                case "RegionSearchText":
                    return "Cerca Regione";
                case "SearchResultForRegionText":
                    return "Risultati della ricerca per Regione";

                    //Edit user page
                case "AdminDeleteUserText":
                    return "Elimina Utente";
                case "AdminDeleteUserInfoText":
                    return "Questa operazione cancellera l Accaount ed eliminera tutti i dati associati ad esso.";
                case "BanText":
                    return "Vieta Accesso";
                case "UnbanText":
                    return "Consenti Accesso";
                case "AdminTempBanUserText":
                    return "Vieta temporaneamente Accesso";
                case "AdminTempBanUserInfoText":
                    return "Questo blocca  un utente per il periodo di tempo desiderato.";
                case "AdminBanUserText":
                    return "Sblocca Utente";
                case "AdminBanUserInfoText":
                    return "Questo Sblocca u utente finche non e sbloccato.";
                case "AdminUnbanUserText":
                    return "Sblocca Utente";
                case "AdminUnbanUserInfoText":
                    return "Rimuove tutti i blocchi temporanei e definitivi su un utente.";
                case "AdminLoginInAsUserText":
                    return "Accedi come utente";
                case "AdminLoginInAsUserInfoText":
                    return
                        "Verrai disconnesso dal tuo Account amministratore e ti connetterai come Utente e vedrai ogni cosa come la vedono gli Utenti normali.";
                case "TimeUntilUnbannedText":
                    return "Tempo mancante allo sblocco utente";
                case "DaysText":
                    return "Giorni";
                case "HoursText":
                    return "Ore";
                case "MinutesText":
                    return "Minuti";
                case "EdittingText":
                    return "Modifica";
                case "BannedUntilText":
                    return "Utente bloccato fino a";
                case "KickAUserText":
                    return "Espelli un utente (disconnetti un utente entro i prossimi 30 secondi)";
                case "KickMessageText":
                    return "invia un messaggio ad un utente";
                case "KickUserText":
                    return "Espelli un utente";

                    //factory_reset
                case "FactoryReset":
                    return "Reset ad impostazioni iniziali";
                case "ResetMenuText":
                    return "Reset del menu alle impostazioni iniziali";
                case "ResetSettingsText":
                    return
                        "Reset delle impostazioni Web (pagina delle impostazioni Amministratore) alle impostazioni iniziali";
                case "Reset":
                    return "Reset";
                case "Settings":
                    return "Impostazioni";
                case "Pages":
                    return "Pagine";
                case "DefaultsUpdated":
                    return
                        "impostazioni di default aggiornate, vai alla pagina di reset delle impostazioni iniziali od alla sezione Amministrazione per eliminare questo messaggio.";

                    //page_manager
                case "PageManager":
                    return "Manager della Pagina";
                case "SaveMenuItemChanges":
                    return "Salva elemento del menu";
                case "SelectItem":
                    return "Seleziona un elemento";
                case "DeleteItem":
                    return "Delete Item";
                case "AddItem":
                    return "Add Item";
                case "PageLocationText":
                    return "Posizione della Pagina";
                case "PageIDText":
                    return "ID della Pagina";
                case "PagePositionText":
                    return "Posizione della Pagina";
                case "PageTooltipText":
                    return "Suggerimenti per la Pagina";
                case "PageTitleText":
                    return "Titolo della Pagina";
                case "No":
                    return "No";
                case "Yes":
                    return "Si";
                case "RequiresLoginText":
                    return "E necessario essere connessi per vedere questo contenuto";
                case "RequiresLogoutText":
                    return "E necessario disconnettersi per vedere questo contenuto";
                case "RequiresAdminText":
                    return "E necessario essere Amministratori per vedere questo contenuto";

                    //settings manager page
                case "Save":
                    return "Salva";
                case "WebRegistrationText":
                    return "Immatricolazioni Web consentiti";
                case "GridCenterXText":
                    return "Centro della Grid Coordinate X";
                case "GridCenterYText":
                    return "Centro della Grid Coordinate Y";
                case "SettingsManager":
                    return "Manager delle Impostazioni";
                case "IgnorePagesUpdatesText":
                    return "Ignora gli aggiornamenti fino al prossimo update";
                case "IgnoreSettingsUpdatesText":
                    return "Ignora gli avvisi fino al prossimo update";

                    //Times
                case "Sun":
                    return "Dom";
                case "Mon":
                    return "Lun";
                case "Tue":
                    return "Mar";
                case "Wed":
                    return "Mer";
                case "Thu":
                    return "Gio";
                case "Fri":
                    return "Ven";
                case "Sat":
                    return "Sab";
                case "Sunday":
                    return "Domenica";
                case "Monday":
                    return "Lunedi";
                case "Tuesday":
                    return "Martedi";
                case "Wednesday":
                    return "Mercoledi";
                case "Thursday":
                    return "Giovedi";
                case "Friday":
                    return "Venerdi";
                case "Saturday":
                    return "Sabato";

                case "Jan_Short":
                    return "Gen";
                case "Feb_Short":
                    return "Feb";
                case "Mar_Short":
                    return "Mar";
                case "Apr_Short":
                    return "Apr";
                case "May_Short":
                    return "Mag";
                case "Jun_Short":
                    return "Giu";
                case "Jul_Short":
                    return "Lug";
                case "Aug_Short":
                    return "Ago";
                case "Sep_Short":
                    return "Set";
                case "Oct_Short":
                    return "Ott";
                case "Nov_Short":
                    return "Nov";
                case "Dec_Short":
                    return "Dic";

                case "January":
                    return "Gennaio";
                case "February":
                    return "Febbraio";
                case "March":
                    return "Marzo";
                case "April":
                    return "Aprile";
                case "May":
                    return "Maggio";
                case "June":
                    return "Giugno";
                case "July":
                    return "Luglio";
                case "August":
                    return "Agosto";
                case "September":
                    return "Settembre";
                case "October":
                    return "Ottobre";
                case "November":
                    return "Novembre";
                case "December":
                    return "Dicembre";

                // User types
            case "UserTypeText":
                return "Tipo di utente";
            case "AdminUserTypeInfoText":
                return "Il tipo di utente (Attualmente utilizzato per i pagamenti periodici stipendio).";
            case "Guest":
                return "Ospite";
            case  "Resident":
                return "Resident";
            case "Member":
                return "Membro";
            case "Contractor":
                return "Imprenditore";
            case "Charter_Member":
                return "Socio fondatore";

                    // ColorBox
                case "ColorBoxImageText":
                    return "Immagine";
                case "ColorBoxOfText":
                    return "di";
                case "ColorBoxPreviousText":
                    return "Precedente";
                case "ColorBoxNextText":
                    return "Successivo";
                case "ColorBoxCloseText":
                    return "Chiudi";
                case "ColorBoxStartSlideshowText":
                    return "Presentazione immagini";
                case "ColorBoxStopSlideshowText":
                    return "Ferma la presentazione";

            // Maintenance
            case "NoAccountFound":
                return "Nessun account trovato";
            case "DisplayInMenu":
                return "Visualizzazione nel menu";
            case "ParentText":
                return "Genitore Menu";
            case "CannotSetParentToChild":
                return "Impossibile impostare la voce di menu come un bambino a se stesso.";
            case "TopLevel":
                return "Livello superiore";
            case "HideLanguageBarText":
                return "Nascondi la barra di selezione della lingua";
            case "HideStyleBarText":
                return "Nascondi stile barra di selezione";

            }
            return "UNKNOWN CHARACTER";
        }
    }
}
