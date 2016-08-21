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

namespace WhiteCore.Modules.Web.Translators
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
                // Generic
                case "No": return "No";
                case "Yes": return "Si";
                case "Submit": return "Invia";
                case "Accept": return "Accetto";
                case "Save": return "Salva";
                case "FirstText": return "Primo";
                case "BackText": return "Precedente";
                case "NextText": return "Prossimo";
                case "LastText": return "Ultimo";
                case "CurrentPageText": return "Pagina corrente";
                case "MoreInfoText": return "Piu informazioni";
                case "NoDetailsText": return "Nessun dato trovato...";
            case "MoreInfo":
                return "Piu informazioni";

            case "ObjectNameText": return "Oggetto";
            case "LocationText": return "Posizione";
            case "UUIDText": return "UUID";
            case "DetailsText": return "Descrizione";
            case "NotesText": return "Note";
            case "SaveUpdates": return "Salva aggiornamenti";
            case "ActiveText": return "Attivo";
            case "CheckedText": return "Verificato";
            case "CategoryText": return "Categoria";
            case "SummaryText": return "Sommario";
                
                // Status information
                case "GridStatus": return "Stato della Grid";
                case "Online": return "Online";
                case "Offline": return "Offline";
                case "TotalUserCount": return "Utenti totali";
                case "TotalRegionCount": return "Regioni totali";
                case "UniqueVisitors": return "Visitatori unici ultimi 30 giorni";
                case "OnlineNow": return "Online Adesso";
                case "HyperGrid": return "HyperGrid (HG)";
                case "Voice": return "Voice";
                case "Currency": return "Valuta";
                case "Disabled": return "Disabilitato";
                case "Enabled": return "Abilitato";
                case "News": return " Notizie";
                case "Region": return "Regione";

                // User login
                case "Login": return "Login";
                case "UserName": 
                case "UserNameText": return "Nome Utente";
                case "Password":
                case "PasswordText": return "Password";
                case "PasswordConfirmation": return "Conferma Password";
                case "ForgotPassword": return "Password dimenticata?";

                // Special windows
                case "SpecialWindowTitleText": return "Titolo sezione Info Speciali";
                case "SpecialWindowTextText": return "Testo sezione Info Speciali";
                case "SpecialWindowColorText": return "Colore sezione Info Specialir";
                case "SpecialWindowStatusText": return "Stato sezione Info Speciali";
                case "WelcomeScreenManagerFor": return "Manager della pagina di benvenuto per";
                case "ChangesSavedSuccessfully": return "I cambiamenti saranno salvati in seguito";

                // User registration
                case "AvatarNameText": return "Nome Avatar";
                case "AvatarScopeText": return "Avatar Scope ID";
                case "FirstNameText": return "Il tuo Nome";
                case "LastNameText": return "Il tuo Cognome";
                case "UserAddressText": return "Il tuo indirizzo";
                case "UserZipText": return "Il tuo codice postale";
                case "UserCityText": return "La tua citta";
                case "UserCountryText": return "Il tuo paese";
                case "UserDOBText": return "La tua data di nascita (Mese Giorno Anno)";
                case "UserEmailText": return "La tua Email";
                case "UserHomeRegionText": return "Regione Home";
                case "RegistrationText": return "Registrazione Avatar";
                case "RegistrationsDisabled": return "La registrazione e attualmente disabilitata. Ti preghiamo di tornare su questa pagina piu tardi";
                case "TermsOfServiceText": return "Termini di Servizio";
                case "TermsOfServiceAccept": return "Accetti i Termini di Servizio descritti qui sopra?";
                case "AvatarNameError": return "Non è stato immesso un nome avatar!";
                case "AvatarPasswordError": return "La password è vuota o non corrispondenti!";
                case "AvatarEmailError": return "L'indirizzo email è necessario per il ripristino della password! ('none' se sconosciuta)";
                case "AvatarNameSpacingError": return "Il tuo nome avatar dovrebbe essere 'Nome Cognome'!";

                // news
                case "OpenNewsManager":
                    return "Apri il manager delle Notizie";
                case "NewsManager":
                    return "Manager delle Notizie";
                case "EditNewsItem":
                    return "Modifica le Notizie";
                case "AddNewsItem":
                    return "Aggiungi una nuova Notizia";
                case "DeleteNewsItem":
                    return "Elimina una Notizia";
                case "NewsDateText":
                    return "Data della Notizia";
                case "NewsTitleText":
                    return "Testo della Notizia";
                case "NewsItemTitle":
                    return "Titolo della Notizia";
                case "NewsItemText":
                    return "Testo della nuova Notizia";
                case "AddNewsText":
                    return "Aggiungi Notizia";
                case "DeleteNewsText":
                    return "Elimina Notizia";
                case "EditNewsText":
                    return "Modifica Notizia";

                // Users
                case "UserProfileFor":
                    return "Profilo Utente per";
                case "UsersGroupsText": return "Gruppi";
                case "GroupNameText": return "Gruppo";
                case "UsersPicksText": return "Preferiti";
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
                case "RegionMaturityText":
                    return "Livello di accesso";
                case "RegionTerrainText":
                    return "Terreno della sim";
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
                case "OnlineUsersText":
                    return "Utenti Online";
                case "RegionOnlineText":
                    return "Stato della Regione";
                case "NumberOfUsersInRegionText":
                    return "Numero di Utenti nella Regione";

                // Menu Buttons
                case "MenuHome": return "Home";
                case "MenuLogin": return "Entra";
                case "MenuLogout": return "Esci";
                case "MenuRegister": return "Registrati";
                case "MenuForgotPass": return "Password dimenticata";
                case "MenuNews": return "Notizie";
                case "MenuWorld": return "Mondo";
                case "MenuWorldMap": return "Mappa del Mondo";
                case "MenuRegion": return "Lista Regioni";
                case "MenuUser": return "Utente";
                case "MenuOnlineUsers": return "Utenti Online";
                case "MenuUserSearch": return "Ricerca Utenti";
                case "MenuRegionSearch": return "Ricerca Regione";
                case "MenuChat": return "Chat";
                case "MenuHelp": return "Aiuto";
                case "MenuChangeUserInformation": return "Cambia Informazioni Utente";
                case "MenuWelcomeScreenManager": return "Manager della pagina di benvenuto";
                case "MenuNewsManager": return "Manager delle Notizie";
                case "MenuUserManager": return "Manager degli Utenti";
                case "MenuFactoryReset": return "Reset impostazioni iniziali";
                case "ResetMenuInfoText": return "Resetta gli elementi del menu alle ultime impostazioni di default";
                case "ResetSettingsInfoText": return "Resetta gli elementi della interfaccia Web alle ultime impostazioni di default";
                case "MenuPageManager": return "Modifica delle Pagione";
                case "MenuSettingsManager": return "Modifica delle Impostazioni";
                case "MenuManager": return "Gestione";
                case "MenuSettings": return "Impostazioni";
                case "MenuRegionManager": return "Manager della Regione";
                case "MenuManagerSimConsole": return "Sim console";
                case "MenuPurchases": return "Acquisti degli utenti";
                case "MenuMyPurchases": return "I miei acquisti ";
                case "MenuTransactions": return "Operazioni utente";
                case "MenuMyTransactions": return "Le mie transazioni";
                case "MenuMyClassifieds": return "Le mie Inserzioni <NT>";                
                case "MenuStatistics": return "Statistiche Viewer";
                case "MenuGridSettings": return "Impostazioni della grid";

                // Tooltips Menu Buttons
                case "TooltipsMenuHome": return "Home";
                case "TooltipsMenuLogin": return "Entra";
                case "TooltipsMenuLogout": return "Esci";
                case "TooltipsMenuRegister": return "Registrati";
                case "TooltipsMenuForgotPass": return "Password dimenticata";
                case "TooltipsMenuNews": return "Notizie";
                case "TooltipsMenuWorld": return "Mondo";
                case "TooltipsMenuWorldMap": return "Mappa del Mondo";
                case "TooltipsMenuUser": return "Utente";
                case "TooltipsMenuOnlineUsers": return "Utenti Online";
                case "TooltipsMenuUserSearch": return "Ricerca Utenti";
                case "TooltipsMenuRegionSearch": return "Ricerca Regione";
                case "TooltipsMenuChat": return "Chat";
                case "TooltipsMenuHelp": return "Aiuto";
                case "TooltipsMenuChangeUserInformation": return "Modifica Impostazioni Utente";
                case "TooltipsMenuWelcomeScreenManager": return "Manager pagina di benvenuto";
                case "TooltipsMenuNewsManager": return "Manager delle Notizie";
                case "TooltipsMenuUserManager": return "Manager degli Utenti";
                case "TooltipsMenuFactoryReset": return "Reset alle impostazioni iniziali";
                case "TooltipsMenuPageManager": return "Manager delle Pagine";
                case "TooltipsMenuSettingsManager": return "Manager delle impostazioni";
                case "TooltipsMenuManager": return "Impostazioni Amministratore";
                case "TooltipsMenuSettings": return "Impostazioni WebUI";
                case "TooltipsMenuRegionManager": return "Regione creare / modificare";
                case "TooltipsMenuManagerSimConsole": return "Console della sim";
                case "TooltipsMenuPurchases": return "Informazioni Acquisto";
                case "TooltipsMenuTransactions": return "Informazioni sulle transazioni";
                case "TooltipsMenuStatistics": return "Statistiche Viewer";
                case "TooltipsMenuGridSettings": return "Impostazioni della grid";

                // Menu Region
                case "MenuRegionTitle": return "Regione";
                case "MenuParcelTitle": return "Terreno";
                case "MenuOwnerTitle": return "Proprietario";
                case "TooltipsMenuRegion": return "Dettagli Regione";
                case "TooltipsMenuParcel": return "Terreni nella Regione";
                case "TooltipsMenuOwner": return "Proprietari dei terreni";

                // Menu Profile
                case "MenuProfileTitle": return "Profilo";
                case "MenuGroupTitle": return "Gruppo";
                case "MenuPicksTitle": return "Preferiti";
                case "MenuRegionsTitle": return "Regioni";
                case "TooltipsMenuProfile": return "Profilo utente";
                case "TooltipsMenuGroups": return "Gruppi di utenti";
                case "TooltipsMenuPicks": return "Selezioni utente";
                case "TooltipsMenuRegions": return "Regioni utenti";
                case "UserGroupNameText": return "Gruppo utenti";
                case "PickNameText": return "Scegli il nome";
                case "PickRegionText": return "Posizione";
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
                    return "Gradiente Chiaro";
                case "styles3":
                    return "Blu notte";
                case "styles4":
                    return "Gradiente Scuro";
                case "styles5":
                    return "Luminoso";

                case "StyleSwitcherStylesText":
                    return "Stili";
                case "StyleSwitcherLanguagesText":
                    return "Lingua";
                case "StyleSwitcherChoiceText":
                    return "Seleziona";

                // Language Switcher Tooltips
                case "en":
                    return "English";
                case "fr":
                    return "Français";
                case "de":
                    return "Deutsch";
                case "it":
                    return "Italiano";
                case "es":
                    return "Español";
                case "nl":
                    return "Nederlands";
            case "ru":
                return "Русский";

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
                    return "Codice errore";
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
                    return "Questa operazione cancellera l Account ed eliminera tutti i dati associati ad esso.";
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
                    return "Questo Sblocca un utente finche non e sbloccato.";
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
                    return "Cancella elemento";
                case "AddItem":
                    return "Aggiungi elemento";
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
                case "RequiresLoginText":
                    return "E necessario essere connessi per vedere questo contenuto";
                case "RequiresLogoutText":
                    return "E necessario disconnettersi per vedere questo contenuto";
                case "RequiresAdminText":
                    return "E necessario essere Amministratori per vedere questo contenuto";

                // grid settings
                case "GridSettingsManager": return "Gestione impostazioni della grid ";
                case "GridnameText": return "Nome della grid ";
                case "GridnickText": return "Soprannome della grid ";
                case "WelcomeMessageText": return "Sezione messaggi di benvenuto ";
            case "GovernorNameText": return "Sistema di Governo";
            case "MainlandEstateNameText": return "Terraferma immobiliare";
            case "RealEstateOwnerNameText": return "Nome proprietario terreni del Governo";
            case "SystemEstateNameText": return "Nomi terreni del Governo";
            case "BankerNameText": return "Sistema bancario";
            case "MarketPlaceOwnerNameText": return "Proprietario del Marketplace";

            //settings manager page
            case "WebRegistrationText":
                    return "Registrazioni Web consentite";
                case "GridCenterXText":
                    return "Centro della Grid: Coordinate X";
                case "GridCenterYText":
                    return "Centro della Grid: Coordinate Y";
                case "SettingsManager":
                    return "Manager delle Impostazioni";
                case "IgnorePagesUpdatesText":
                    return "Ignora gli aggiornamenti fino al prossimo aggiornamento";
                case "IgnoreSettingsUpdatesText":
                    return "Ignora gli avvisi fino al prossimo aggiornamento";

                // Transactions
                case "TransactionsText": return "Operazioni";
                case "DateInfoText": return "Selezionare un intervallo di date";
                case "DateStartText": return "Data di Inizio";
                case "DateEndText": return "Data di Fine";
                case "30daysPastText": return "30 giorni precedenti";
                case "TransactionDateText": return "Data";
                case "TransactionDetailText": return "Descrizione";
                case "TransactionAmountText": return "Importo";
                case "TransactionBalanceText": return "Saldo";
                case "NoTransactionsText": return "Nessuna transazione trovata...";
                case "PurchasesText": return "Acquisti";
                case "LoggedIPText": return "Il tuo indirizzo IP";
                case "NoPurchasesText": return "Nessuna transazione trovata...";
                case "PurchaseCostText": return "Costo";
                
                // Classifieds
                case "ClassifiedsText": return "Inserzioni <NT>";

                // Sim Console
                case "SimConsoleText": return "Console di comando della Sim";
                case "SimCommandText": return "Comando";

                // statistics
                case "StatisticsText": return "Statistiche Viewer";
                case "ViewersText": return "Utilizzo Viewer";
                case "GPUText": return "Schede grafiche";
                case "PerformanceText": return "Medie prestazioni";
                case "FPSText": return "Fotogrammi / secondo";
                case "RunTimeText": return "Tempo di esecuzione";
                case "RegionsVisitedText": return "Regioni visitate";
                case "MemoryUseageText": return "Uso della memoria";
                case "PingTimeText": return "Tempo di Ping";
                case "AgentsInViewText": return "Agenti in vista";
                case "ClearStatsText": return "Cancella dati statistici";

                // abuse reports
            case "MenuAbuse": return "Denunce di Abuso";
            case "TooltipsMenuAbuse": return "Utente denunciato";
            case "AbuseReportText": return "Notifica di abuso";
            case "AbuserNameText": return "Accusato";
            case "AbuseReporterNameText": return "Accusatore";
            case "AssignedToText": return "Assegnato a";

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
                case "Resident":
                    return "Residente";
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
                    return "Menu principale";
                case "CannotSetParentToChild":
                    return "Impossibile impostare la voce di menu come derivato di se stesso.";
                case "TopLevel":
                    return "Livello superiore";
                case "HideLanguageBarText":
                    return "Nascondi la barra di selezione della lingua";
                case "HideStyleBarText":
                    return "Nascondi stile barra di selezione";
                case "HideSlideshowBarText":
                    return "Nascondi barra presentazione";
                case "LocalFrontPageText":
                    return "Prima pagina locale";
                case "LocalCSSText":
                    return "Foglio di stile CSS locale";

            }
            return "UNKNOWN CHARACTER";
        }
    }
}
