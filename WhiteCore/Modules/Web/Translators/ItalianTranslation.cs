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
    public class ItalianTranslation : ITranslator
    {
        public string LanguageName {
            get { return "it"; }
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
            { "Yes", "Si"},
            { "Submit", "Invia"},
            { "Accept", "Accetto"},
            { "Save", "Salva"},
            { "Cancel", "Annulla"},
            { "FirstText", "Primo"},
            { "BackText", "Precedente"},
            { "NextText", "Prossimo"},
            { "LastText", "Ultimo"},
            { "CurrentPageText", "Pagina corrente"},
            { "MoreInfoText", "Piu informazioni"},
            { "NoDetailsText", "Nessun dato trovato..."},
            { "MoreInfo", "Piu informazioni"},
            { "Name", "Nome"},
            { "ObjectNameText", "Oggetto"},
            { "LocationText", "Posizione"},
            { "UUIDText", "UUID"},
            { "DetailsText", "Descrizione"},
            { "NotesText", "Nota"},
            { "SaveUpdates", "Salva aggiornamenti"},
            { "ActiveText", "Attivo"},
            { "CheckedText", "Verificato"},
            { "CategoryText", "Categoria"},
            { "SummaryText", "Sommario"},
            { "MaturityText", "Scadenza"},
            { "DateText", "Data"},
            { "TimeText", "Tempo"},
            { "MinuteText", "minuto"},
            { "MinutesText", "minuti"},
            { "HourText", "ora"},
            { "HoursText", "ore"},
            { "EditText", "Modifica"},
            { "EdittingText", "La modifica"},

            // Status information
            { "GridStatus", "Stato della Grid"},
            { "Online", "Online"},
            { "Offline", "Offline"},
            { "TotalUserCount", "Utenti totali"},
            { "TotalRegionCount", "Regioni totali"},
            { "UniqueVisitors", "Visitatori unici ultimi 30 giorni"},
            { "OnlineNow", "Online Adesso"},
            { "InterWorld", "Inter World (IWC)"},
            { "HyperGrid", "HyperGrid (HG)"},
            { "Voice", "Voice"},
            { "Currency", "Valuta"},
            { "Disabled", "Disabilitato"},
            { "Enabled", "Abilitato"},
            { "News", " Notizie"},
            { "Region", "Regione"},

            // User login
            { "Login", "Login"},
            { "UserName", "Nome Utente"},
            { "UserNameText", "Nome Utente"},
            { "Password", "Password"},
            { "PasswordText", "Password"},
            { "PasswordConfirmation", "Conferma Password"},
            { "ForgotPassword", "Password dimenticata?"},
            { "TypeUserNameToConfirm", "Si prega di inserire il nome utente di questo account per confermare che si desidera eliminare questo account"},

            // Special windows
            { "SpecialWindowTitleText", "Titolo sezione Info Speciali"},
            { "SpecialWindowTextText", "Testo sezione Info Speciali"},
            { "SpecialWindowColorText", "Colore sezione Info Specialir"},
            { "SpecialWindowStatusText", "Stato sezione Info Speciali"},
            { "WelcomeScreenManagerFor", "Manager della pagina di benvenuto per"},
            { "ChangesSavedSuccessfully", "I cambiamenti saranno salvati in seguito"},

            // User registration
            { "AvatarNameText", "Nome Avatar"},
            { "AvatarScopeText", "Avatar Scope ID"},
            { "FirstNameText", "Il tuo Nome"},
            { "LastNameText", "Il tuo Cognome"},
            { "UserAddressText", "Il tuo indirizzo"},
            { "UserZipText", "Il tuo codice postale"},
            { "UserCityText", "La tua citta"},
            { "UserCountryText", "Il tuo paese"},
            { "UserDOBText", "La tua data di nascita (Mese Giorno Anno)"},
            { "UserEmailText", "La tua Email"},
            { "UserHomeRegionText", "Regione Home"},
            { "RegistrationText", "Registrazione Avatar"},
            { "RegistrationsDisabled", "La registrazione e attualmente disabilitata. Ti preghiamo di tornare su questa pagina piu tardi"},
            { "TermsOfServiceText", "Termini di Servizio"},
            { "TermsOfServiceAccept", "Accetti i Termini di Servizio descritti qui sopra?"},
            { "AvatarNameError", "Non è stato immesso un nome avatar!"},
            { "AvatarPasswordError", "La password è vuota o non corrispondenti!"},
            { "AvatarEmailError", "L'indirizzo email è necessario per il ripristino della password! ('none' se sconosciuta)"},
            { "AvatarNameSpacingError", "Il tuo nome avatar dovrebbe essere 'Nome Cognome'!"},

            // News
            { "OpenNewsManager", "Apri il manager delle Notizie"},
            { "NewsManager", "Manager delle Notizie"},
            { "EditNewsItem", "Modifica le Notizie"},
            { "AddNewsItem", "Aggiungi una nuova Notizia"},
            { "DeleteNewsItem", "Elimina una Notizia"},
            { "NewsDateText", "Data della Notizia"},
            { "NewsTitleText", "Testo della Notizia"},
            { "NewsItemTitle", "Titolo della Notizia"},
            { "NewsItemText", "Testo della nuova Notizia"},
            { "AddNewsText", "Aggiungi Notizia"},
            { "DeleteNewsText", "Elimina Notizia"},
            { "EditNewsText", "Modifica Notizia"},

            // User Profile
            { "UserProfileFor", "Profilo Utente per"},
            { "UsersGroupsText", "Gruppi"},
            { "GroupNameText", "Gruppo"},
            { "UsersPicksText", "Preferiti"},
            { "ResidentSince", "Residente dal"},
            { "AccountType", "Tipo di Account"},
            { "PartnersName", "Nome del Partner"},
            { "AboutMe", "Il mio Profilo"},
            { "IsOnlineText", "Stato dell utente"},
            { "OnlineLocationText", "Posizione dell Utente"},
            { "Partner", "Compagno"},
            { "Friends", "Amici"},
            { "Nothing", "Nessuna"},
            { "ChangePass", "Cambia la password"},
            { "NoChangePass", "Impossibile cambiare la password, si prega di riprovare più tardi"},

            // Region Information
            { "RegionInformationText", "Informazioni sulla Regione"},
            { "OwnerNameText", "Proprietario"},
            { "RegionLocationText", "Posizione della Regione"},
            { "RegionSizeText", "Dimensione della Regione"},
            { "RegionNameText", "Nome della Regione"},
            { "RegionTypeText", "Tipo della Regione"},
            { "RegionPresetTypeText", "Regione Preset"},
            { "RegionDelayStartupText", "Delay starting scripts"},
            { "RegionPresetText", "Region Preset"},
            { "RegionTerrainText", "Terreno della sim"},
            { "ParcelsInRegionText", "Terreni della Regione"},
            { "ParcelNameText", "Nome del Terreno"},
            { "ParcelOwnerText", "Proprietario del Terreno"},

            // Region List
            { "RegionInfoText", "Info Regione"},
            { "RegionListText", "Lista Regioni"},
            { "RegionLocXText", "Coordinate X della Regione"},
            { "RegionLocYText", "Coordinate Y della Regione"},
            { "SortByLocX", "Ordina secondo la coordinata X"},
            { "SortByLocY", "Ordina secondo la coordinata Y"},
            { "SortByName", "Ordina secondo il nome della Regione"},
            { "RegionMoreInfo", "Piu informazioni"},
            { "RegionMoreInfoTooltips", "Piu informazioni su"},
            { "OnlineUsersText", "Utenti Online"},
            { "OnlineFriendsText", "Amici Online"},
            { "RegionOnlineText", "Stato della Regione"},
            { "RegionMaturityText", "Livello di accesso"},
            { "NumberOfUsersInRegionText", "Numero di Utenti nella Regione"},

            // Region manager
            { "AddRegionText", "Aggiungere Regione"},
            { "Mainland", "Terraferma"},
            { "Estate", "Tenuta"},
            { "FullRegion", "Regione ricca"},
            { "Homestead", "Fattoria"},
            { "Openspace", "Spazio aperto"},
            { "Flatland", "Pianura"},
            { "Grassland", "Prateria"},
            { "Island", "Isola"},
            { "Aquatic", "Acquatico"},
            { "Custom", "Custom"},
            { "RegionPortText", "Porto regione"},
            { "RegionVisibilityText", "Visibile a vicini di casa"},
            { "RegionInfiniteText", "Regione Infinite"},
            { "RegionCapacityText", "Capacità oggetto Regione"},
            { "NormalText", "Normale"},
            { "DelayedText", "Ritardato"},

            // Estate management
            {"AddEstateText", "Aggiungere immobiliare"},
            {"EstateText", "Tenuta"},
            {"EstatesText", "Estates"},
            {"PricePerMeterText", "Prezzo al metro quadrato"},
            {"PublicAccessText", "Accesso pubblico"},
            {"AllowVoiceText", "Consentire voce"},
            {"TaxFreeText", "Senza tasse"},
            {"AllowDirectTeleportText", "Consentire teletrasporto diretta"},

            // Menus
            { "MenuHome", "Home"},
            { "MenuLogin", "Entra"},
            { "MenuLogout", "Esci"},
            { "MenuRegister", "Registrati"},
            { "MenuForgotPass", "Password dimenticata"},
            { "MenuNews", "Notizie"},
            { "MenuWorld", "Mondo"},
            { "MenuWorldMap", "Mappa del Mondo"},
            { "MenuRegion", "Lista Regioni"},
            { "MenuUser", "Utente"},
            { "MenuOnlineUsers", "Utenti Online"},
            { "MenuUserSearch", "Ricerca Utenti"},
            { "MenuRegionSearch", "Ricerca Regione"},
            { "MenuChat", "Chat"},
            { "MenuHelp", "Aiuto"},
            { "MenuViewerHelp", "Viewer Help"},
            { "MenuChangeUserInformation", "Cambia Informazioni Utente"},
            { "MenuWelcomeScreenManager", "Manager della pagina di benvenuto"},
            { "MenuNewsManager", "Manager delle Notizie"},
            { "MenuUserManager", "Manager degli Utenti"},
            { "MenuFactoryReset", "Reset impostazioni iniziali"},
            { "ResetMenuInfoText", "Resetta gli elementi del menu alle ultime impostazioni di default"},
            { "ResetSettingsInfoText", "Resetta gli elementi della interfaccia Web alle ultime impostazioni di default"},
            { "MenuPageManager", "Modifica delle Pagione"},
            { "MenuSettingsManager", "Modifica delle Impostazioni"},
            { "MenuManager", "Gestione"},
            { "MenuSettings", "Impostazioni"},
            { "MenuRegionManager", "Manager della Regione"},
            { "MenuEstateManager", "Direttore immobiliare"},
            { "MenuManagerSimConsole", "Sim console"},
            { "MenuPurchases", "Acquisti degli utenti"},
            { "MenuMyPurchases", "I miei acquisti "},
            { "MenuTransactions", "Operazioni utente"},
            { "MenuMyTransactions", "Le mie transazioni"},
            { "MenuClassifieds", "Classifieds"},
            { "MenuMyClassifieds", "Le mie Inserzioni <NT>"},
            { "MenuEvents", "Events"},
            { "MenuMyEvents", "My Events"},
            { "MenuStatistics", "Statistiche Viewer"},
            { "MenuGridSettings", "Impostazioni della grid"},

            // Menu Tooltips
            { "TooltipsMenuHome", "Home"},
            { "TooltipsMenuLogin", "Entra"},
            { "TooltipsMenuLogout", "Esci"},
            { "TooltipsMenuRegister", "Registrati"},
            { "TooltipsMenuForgotPass", "Password dimenticata"},
            { "TooltipsMenuNews", "Notizie"},
            { "TooltipsMenuWorld", "Mondo"},
            { "TooltipsMenuWorldMap", "Mappa del Mondo"},
            { "TooltipsMenuUser", "Utente"},
            { "TooltipsMenuOnlineUsers", "Utenti Online"},
            { "TooltipsMenuUserSearch", "Ricerca Utenti"},
            { "TooltipsMenuRegionSearch", "Ricerca Regione"},
            { "TooltipsMenuChat", "Chat"},
            { "TooltipsMenuViewerHelp", "Aiuto"},
            { "TooltipsMenuHelp", "Aiuto"},
            { "TooltipsMenuChangeUserInformation", "Modifica Impostazioni Utente"},
            { "TooltipsMenuWelcomeScreenManager", "Manager pagina di benvenuto"},
            { "TooltipsMenuNewsManager", "Manager delle Notizie"},
            { "TooltipsMenuUserManager", "Manager degli Utenti"},
            { "TooltipsMenuFactoryReset", "Reset alle impostazioni iniziali"},
            { "TooltipsMenuPageManager", "Manager delle Pagine"},
            { "TooltipsMenuSettingsManager", "Manager delle impostazioni"},
            { "TooltipsMenuManager", "Impostazioni Amministratore"},
            { "TooltipsMenuSettings", "Impostazioni WebUI"},
            { "TooltipsMenuRegionManager", "Regione creare / modificare"},
            { "TooltipsMenuEstateManager", "Gestione immobiliare"},
            { "TooltipsMenuManagerSimConsole", "Console della sim"},
            { "TooltipsMenuPurchases", "Informazioni Acquisto"},
            { "TooltipsMenuTransactions", "Informazioni sulle transazioni"},
            { "TooltipsMenuClassifieds", "Classifieds information"},
            { "TooltipsMenuEvents", "Event information"},
            { "TooltipsMenuStatistics", "Statistiche Viewer"},
            { "TooltipsMenuGridSettings", "Impostazioni della grid"},

            // Menu Region box
            { "MenuRegionTitle", "Regione"},
            { "MenuParcelTitle", "Terreno"},
            { "MenuOwnerTitle", "Proprietario"},
            { "TooltipsMenuRegion", "Dettagli Regione"},
            { "TooltipsMenuParcel", "Terreni nella Regione"},
            { "TooltipsMenuOwner", "Proprietari dei terreni"},

            // Menu Profile box
            { "MenuProfileTitle", "Profilo"},
            { "MenuGroupTitle", "Gruppo"},
            { "MenuPicksTitle", "Preferiti"},
            { "MenuRegionsTitle", "Regioni"},
            { "TooltipsMenuProfile", "Profilo utente"},
            { "TooltipsMenuGroups", "Gruppi di utenti"},
            { "TooltipsMenuPicks", "Selezioni utente"},
            { "TooltipsMenuRegions", "Regioni utenti"},
            { "UserGroupNameText", "Gruppo utenti"},
            { "PickNameText", "Scegli il nome"},
            { "PickRegionText", "Posizione"},

            // Urls
            { "WelcomeScreen", "Pagina di Benvenuto"},

            // Tooltips Urls
            { "TooltipsWelcomeScreen", "Pagina di Benvenuto"},
            { "TooltipsWorldMap", "Mappa del Mondo"},

            // Index
            { "HomeText", "Home"},
            { "HomeTextWelcome", "Questo e il nostro nuovo mondo virtuale! Registrati gratis e fai la differenza!"},
            { "HomeTextTips", "Nuove presentazioni"},
            { "WelcomeToText", "Benvenuto a"},

            // World Map
            { "WorldMap", "Mappa del mondo"},
            { "WorldMapText", "Schermo intero"},

            // Chat
            { "ChatText", "Chat di Supporto"},

            // Help
            { "HelpText", "Aiuto"},
            { "HelpViewersConfigText", "Configurazione Viewer"},

            // Logout
            { "Logout", "Disconnetti"},
            { "LoggedOutSuccessfullyText", "Sei stato disconnesso."},

            // Change user information page
            { "ChangeUserInformationText", "Modifica informazioni utente"},
            { "ChangePasswordText", "Modifica Password"},
            { "NewPasswordText", "Nuova Password"},
            { "NewPasswordConfirmationText", "Nuova Password (Conferma)"},
            { "ChangeEmailText", "Mofifica indirizzo Email"},
            { "NewEmailText", "Nuovo indirizzo Email"},
            { "DeleteUserText", "Cancella il mio Account"},
            { "DeleteText", "Cancella"},
            { "DeleteUserInfoText",
                    "Questa operazione eliminera i tuoi dati nella grid e non ti consentira di accedere di nuovo. Se davvero vuoi continuare inserisci di nuovo la tua password e clicca su Cancella."},
            { "EditUserAccountText", "Modifica Account Utente"},

            // Maintenance
            { "WebsiteDownInfoText", "Il sito web e attualmente offline, ti preghiamo di provare piu tardi."},
            { "WebsiteDownText", "Il sito web e offline"},

            // Http 404
            { "Error404Text", "Codice Errore"},
            { "Error404InfoText", "404 Pagina non trovata"},
            { "HomePage404Text", "home page"},

            // Http 505
            { "Error505Text", "Codice errore"},
            { "Error505InfoText", "505 Errore interno del server"},
            { "HomePage505Text", "home page"},

            // User search
            { "Search", "Cerca"},
            { "SearchText", "Cerca"},
            { "SearchForUserText", "Cerca un utente"},
            { "UserSearchText", "Ricerca Utente"},
            { "SearchResultForUserText", "Risultati della ricerca Utente"},

            // Region search
            { "SearchForRegionText", "Cerca una Regione"},
            { "RegionSearchText", "Cerca Regione"},
            { "SearchResultForRegionText", "Risultati della ricerca per Regione"},

            // Edit user
            { "AdminDeleteUserText", "Elimina Utente"},
            { "AdminDeleteUserInfoText", "Questa operazione cancellera l Account ed eliminera tutti i dati associati ad esso."},
            { "BanText", "Vieta Accesso"},
            { "UnbanText", "Consenti Accesso"},
            { "AdminTempBanUserText", "Vieta temporaneamente Accesso"},
            { "AdminTempBanUserInfoText", "Questo blocca  un utente per il periodo di tempo desiderato."},
            { "AdminBanUserText", "Sblocca Utente"},
            { "AdminBanUserInfoText", "Questo Sblocca un utente finche non e sbloccato."},
            { "AdminUnbanUserText", "Sblocca Utente"},
            { "AdminUnbanUserInfoText", "Rimuove tutti i blocchi temporanei e definitivi su un utente."},
            { "AdminLoginInAsUserText", "Accedi come utente"},
            { "AdminLoginInAsUserInfoText",
                    "Verrai disconnesso dal tuo Account amministratore e ti connetterai come Utente e vedrai ogni cosa come la vedono gli Utenti normali."},
            { "TimeUntilUnbannedText", "Tempo mancante allo sblocco utente"},
            { "BannedUntilText", "Utente bloccato fino a"},
            { "KickAUserText", "Espelli un utente (disconnetti un utente entro i prossimi 30 secondi)"},
            { "KickAUserInfoText", "Kicks a user from the grid (logs them out within 30 seconds)"},
            { "KickMessageText", "invia un messaggio ad un utente"},
            { "KickUserText", "Espelli un utente"},
            { "MessageAUserText", "Send User A Message"},
            { "MessageAUserInfoText", "Sends a user a blue-box message (will arrive within 30 seconds)"},
            { "MessageUserText", "Message User"},

            // Transactions
            { "TransactionsText", "Operazioni"},
            { "DateInfoText", "Selezionare un intervallo di date"},
            { "DateStartText", "Data di Inizio"},
            { "DateEndText", "Data di Fine"},
            { "30daysPastText", "30 giorni precedenti"},
            { "TransactionToAgentText", "To User"},
            { "TransactionFromAgentText", "From User"},
            { "TransactionDateText", "Data"},
            { "TransactionDetailText", "Descrizione"},
            { "TransactionAmountText", "Importo"},
            { "TransactionBalanceText", "Saldo"},
            { "NoTransactionsText", "Nessuna transazione trovata..."},
            { "PurchasesText", "Acquisti"},
            { "LoggedIPText", "Il tuo indirizzo IP"},
            { "NoPurchasesText", "Nessuna transazione trovata..."},
            { "PurchaseCostText", "Costo"},

            // Classifieds
            { "ClassifiedsText", "Inserzioni <NT>"},
            { "ClassifiedText", "Inserzioni <NT>"},
            { "ListedByText", " Listed by"},
            { "CreationDateText", "Creation date"},
            { "ExpirationDateText", "Expiration date" },
            { "DescriptionText", "Description" },
            { "PriceOfListingText", "Price"},

            // Classified categories
            { "CatAll", "Tutti"},
            { "CatSelect", ""},
            { "CatShopping", "Shopping"},
            { "CatLandRental", "Noleggio Terra"},
            { "CatPropertyRental", "Affitto di proprietà"},
            { "CatSpecialAttraction", "Attrazione speciale"},
            { "CatNewProducts", "Nuovi Prodotti"},
            { "CatEmployment", "Occupazione"},
            { "CatWanted", "Ricercato"},
            { "CatService", "Servizio"},
            { "CatPersonal", "Personale"},
           
            // Events
            { "EventsText", "Eventi"},
            { "EventNameText", "Evento"},
            { "EventLocationText", "Dove"},
            { "HostedByText","Ospitato da"},
            { "EventDateText", "quando"},
            { "EventTimeInfoText", "Tempo evento dovrebbe essere ora locale"},
            { "CoverChargeText", "Coperto"},
            { "DurationText", "Durata"},
            { "AddEventText", "Aggiungi evento"},

            // Event categories
            { "CatDiscussion", "Discussione"},
            { "CatSports", "Gli sport"},
            { "CatLiveMusic", "Musica dal vivo"},
            { "CatCommercial", "Commerciale"},
            { "CatEntertainment", "Vita notturna/Divertimento"},
            { "CatGames", "I giochi/Concorsi"},
            { "CatPageants", "Spettacoli"},
            { "CatEducation", "Educazione"},
            { "CatArtsCulture", "Arts/Cultura"},
            { "CatCharitySupport", "Carità/Gruppo di supporto"},
            { "CatMiscellaneous", "Miscellaneo"},

            // Event lookup periods
            { "Next24Hours", "Prossime 24 ore"},
            { "Next10Hours", "Prossime 10 ore"},
            { "Next4Hours", "Prossime 4 ore"},
            { "Next2Hours", "Prossime 2 ore"},

            // Sim Console
            { "SimConsoleText", "Console di comando della Sim"},
            { "SimCommandText", "Comando"},

            // statistics
            { "StatisticsText", "Statistiche Viewer"},
            { "ViewersText", "Usage Viewer"},
            { "GPUText", "Schede grafiche"},
            { "PerformanceText", "Medie prestazioni"},
            { "FPSText", "Fotogrammi / secondo"},
            { "RunTimeText", "Tempo di esecuzione"},
            { "RegionsVisitedText", "Regioni visitate"},
            { "MemoryUseageText", "Uso della memoria"},
            { "PingTimeText", "Tempo di Ping"},
            { "AgentsInViewText", "Agenti in vista"},
            { "ClearStatsText", "Cancella dati statistici"},

            // Abuse reports
            { "MenuAbuse", "Denunce di Abuso"},
            { "TooltipsMenuAbuse", "Utente denunciato"},
            { "AbuseReportText", "Notifica di abuso"},
            { "AbuserNameText", "Accusato"},
            { "AbuseReporterNameText", "Accusatore"},
            { "AssignedToText", "Assegnato a"},

             // Factory_reset
            { "FactoryReset", "Reset ad impostazioni iniziali"},
            { "ResetMenuText", "Reset del menu alle impostazioni iniziali"},
            { "ResetSettingsText",
                    "Reset delle impostazioni Web (pagina delle impostazioni Amministratore) alle impostazioni iniziali"},
            { "Reset", "Reset"},
            { "Settings", "Impostazioni"},
            { "Pages", "Pagine"},
            { "UpdateRequired", "aggiornamento richiesto"},
            { "DefaultsUpdated",
                    "impostazioni di default aggiornate, vai alla pagina di reset delle impostazioni iniziali od alla sezione Amministrazione per eliminare questo messaggio."},

            // Page_manager
            { "PageManager", "Manager della Pagina"},
            { "SaveMenuItemChanges", "Salva elemento del menu"},
            { "SelectItem", "Seleziona un elemento"},
            { "DeleteItem", "Cancella elemento"},
            { "AddItem", "Aggiungi elemento"},
            { "PageLocationText", "Posizione della Pagina"},
            { "PageIDText", "ID della Pagina"},
            { "PagePositionText", "Posizione della Pagina"},
            { "PageTooltipText", "Suggerimenti per la Pagina"},
            { "PageTitleText", "Titolo della Pagina"},
            { "RequiresLoginText", "E necessario essere connessi per vedere questo contenuto"},
            { "RequiresLogoutText", "E necessario disconnettersi per vedere questo contenuto"},
            { "RequiresAdminText", "E necessario essere Amministratori per vedere questo contenuto"},
            { "RequiresAdminLevelText", "Required Admin Level To View"},

            // Grid settings
            { "GridSettingsManager", "Gestione impostazioni della grid "},
            { "GridnameText", "Nome della grid "},
            { "GridnickText", "Soprannome della grid "},
            { "WelcomeMessageText", "Sezione messaggi di benvenuto "},
            { "GovernorNameText", "Sistema di Governo"},
            { "MainlandEstateNameText", "Terraferma immobiliare"},
            { "RealEstateOwnerNameText", "Nome proprietario terreni del Governo"},
            { "SystemEstateNameText", "Nomi terreni del Governo"},
            { "BankerNameText", "Sistema bancario"},
            { "MarketPlaceOwnerNameText", "Proprietario del Marketplace"},

            // Settings manager
            { "WebRegistrationText", "Registrazioni Web consentite"},
            { "GridCenterXText", "Centro della Grid: Coordinate X"},
            { "GridCenterYText", "Centro della Grid: Coordinate Y"},
            { "SettingsManager", "Manager delle Impostazioni"},
            { "IgnorePagesUpdatesText", "Ignora gli aggiornamenti fino al prossimo aggiornamento"},
            { "IgnoreSettingsUpdatesText", "Ignora gli avvisi fino al prossimo aggiornamento"},
            { "HideLanguageBarText", "Nascondi la barra di selezione della lingua"},
            { "HideStyleBarText", "Nascondi stile barra di selezione"},
            { "HideSlideshowBarText", "Nascondi barra presentazione"},
            { "LocalFrontPageText", "Prima pagina locale"},
            { "LocalCSSText", "Foglio di stile CSS locale"},

            // Dates
            { "Sun", "Dom"},
            { "Mon", "Lun"},
            { "Tue", "Mar"},
            { "Wed", "Mer"},
            { "Thu", "Gio"},
            { "Fri", "Ven"},
            { "Sat", "Sab"},
            { "Sunday", "Domenica"},
            { "Monday", "Lunedi"},
            { "Tuesday", "Martedi"},
            { "Wednesday", "Mercoledi"},
            { "Thursday", "Giovedi"},
            { "Friday", "Venerdi"},
            { "Saturday", "Sabato"},

            { "Jan_Short", "Gen"},
            { "Feb_Short", "Feb"},
            { "Mar_Short", "Mar"},
            { "Apr_Short", "Apr"},
            { "May_Short", "Mag"},
            { "Jun_Short", "Giu"},
            { "Jul_Short", "Lug"},
            { "Aug_Short", "Ago"},
            { "Sep_Short", "Set"},
            { "Oct_Short", "Ott"},
            { "Nov_Short", "Nov"},
            { "Dec_Short", "Dic"},

            { "January", "Gennaio"},
            { "February", "Febbraio"},
            { "March", "Marzo"},
            { "April", "Aprile"},
            { "May", "Maggio"},
            { "June", "Giugno"},
            { "July", "Luglio"},
            { "August", "Agosto"},
            { "September", "Settembre"},
            { "October", "Ottobre"},
            { "November", "Novembre"},
            { "December", "Dicembre"},

            // User types
            { "UserTypeText", "Tipo di utente"},
            { "AdminUserTypeInfoText", "Il tipo di utente (Attualmente utilizzato per i pagamenti periodici stipendio)."},
            { "Guest", "Ospite"},
            { "Resident", "Residente"},
            { "Member", "Membro"},
            { "Contractor", "Imprenditore"},
            { "Charter_Member", "Socio fondatore"},

            // ColorBox
            { "ColorBoxImageText", "Immagine"},
            { "ColorBoxOfText", "di"},
            { "ColorBoxPreviousText", "Precedente"},
            { "ColorBoxNextText", "Successivo"},
            { "ColorBoxCloseText", "Chiudi"},
            { "ColorBoxStartSlideshowText", "Presentazione immagini"},
            { "ColorBoxStopSlideshowText", "Ferma la presentazione"},

            // Maintenance
            { "NoAccountFound", "Nessun account trovato"},
            { "DisplayInMenu", "Visualizzazione nel menu"},
            { "ParentText", "Menu principale"},
            { "CannotSetParentToChild", "Impossibile impostare la voce di menu come derivato di se stesso."},
            { "TopLevel", "Livello superiore"},

            // Style Switcher
            { "styles1", "Default Minimalista"},
            { "styles2", "Gradiente Chiaro"},
            { "styles3", "Blu notte"},
            { "styles4", "Gradiente Scuro"},
            { "styles5", "Luminoso"},

            { "StyleSwitcherStylesText", "Stili"},
            { "StyleSwitcherLanguagesText", "Lingua"},
            { "StyleSwitcherChoiceText", "Seleziona"},

            // Language Switcher Tooltips
            { "en", "English"},
            { "fr", "Français"},
            { "de", "Deutsch"},
            { "it", "Italiano"},
            { "es", "Español"},
            { "nl", "Nederlands"},
            { "ru", "Русский"}

        };
    }
}
