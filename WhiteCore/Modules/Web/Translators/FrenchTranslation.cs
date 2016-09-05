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
    public class FrenchTranslation : ITranslator
    {
        public string LanguageName {
            get { return "fr"; }
        }

        public string GetTranslatedString (string key)
        {
            if (dictionary.ContainsKey (key))
                return dictionary [key];
            return ":" + key + ":";
        }

        readonly Dictionary<string, string> dictionary = new Dictionary<string, string> {
            // Generic
            { "No", "Non"},
            { "Yes", "Oui"},
            { "Submit", "Envoyer"},
            { "Accept", "Accepter"},
            { "Save", "Sauver"},
            { "Cancel", "Annuler"},
            { "FirstText", "Premier"},
            { "BackText", "Précédent"},
            { "NextText", "Suivant"},
            { "LastText", "Dernier"},
            { "CurrentPageText", "Page actuelle"},
            { "MoreInfoText", "Plus d'informations"},
            { "NoDetailsText", "Pas de détails trouvés..."},
            { "MoreInfo", "Plus d'informations"},
            { "Name", "Name"},
            { "ObjectNameText", "Objet"},
            { "LocationText", "Emplacement"},
            { "UUIDText", "UUID"},
            { "DetailsText", "Description"},
            { "NotesText", "Remarques"},
            { "SaveUpdates", "Enregistrer les mises à jour"},
            { "ActiveText", "Actif"},
            { "CheckedText", "Vérifié"},
            { "CategoryText", "Catégorie"},
            { "SummaryText", "Résumé"},
            { "MaturityText", "Maturité"},
            { "DateText", "Date"},
            { "TimeText", "Temps"},
            { "MinuteText", "minute"},
            { "MinutesText", "minutes"},
            { "HourText", "heure"},
            { "HoursText", "heures"},
            { "EditText", "Modifier"},
            { "EdittingText", "Rédaction"},

            // Status information
            { "GridStatus", "Etat de la Grille"},
            { "Online", "En Ligne"},
            { "Offline", "Hors Ligne"},
            { "TotalUserCount", "Nombre total d'utilisateurs"},
            { "TotalRegionCount", "Nombre total de régions"},
            { "UniqueVisitors", "Visiteurs unique {30 jours)"},
            { "OnlineNow", "En ligne maintenant"},
            { "InterWorld", "Inter World (IWC)"},
            { "HyperGrid", "HyperGrid {HG)"},
            { "Voice", "Voix"},
            { "Currency", "Monnaie"},
            { "Disabled", "Désactivé"},
            { "Enabled", "Activé"},
            { "News", "Nouveautés"},
            { "Region", "Région"},

            // User login
            { "Login", "Connection"},
            { "UserName", "Nom d'utilisateur"},
            { "UserNameText", "Nom d'utilisateur"},
            { "Password", "Mot de passe"},
            { "PasswordText", "Mot de passe"},
            { "PasswordConfirmation", "Confirmer Mot de passe"},
            { "ForgotPassword", "Mot de passe oublié?"},
            { "TypeUserNameToConfirm", "S'il vous plaît entrez le nom d'utilisateur de ce compte pour confirmer que vous voulez supprimer ce compte"},

            // Special windows
            { "SpecialWindowTitleText", "Titre spécial de la fenêtre Info"},
            { "SpecialWindowTextText", "Texte spécial de la fenêtre Infos"},
            { "SpecialWindowColorText", "Couleur spécial de la fenêtre Infos"},
            { "SpecialWindowStatusText", "Status spécial de la fenêtre Infos"},
            { "WelcomeScreenManagerFor", "Welcome Screen Manager pour"},
            { "ChangesSavedSuccessfully", "Changements enregistrés avec succès"},

            // User registration
            { "AvatarNameText", "Nom de l'Avatar"},
            { "AvatarScopeText", "Scope ID de l'Avatar"},
            { "FirstNameText", "Votre Nom"},
            { "LastNameText", "Votre Prénom"},
            { "UserAddressText", "Votre Addresse"},
            { "UserZipText", "Votre Code Zip"},
            { "UserCityText", "Votre Ville"},
            { "UserCountryText", "Votre Pays"},
            { "UserDOBText", "Votre date d'anniversaire {Mois Jour Année)"},
            { "UserEmailText", "Votre Email"},
            { "UserHomeRegionText", "Accueil région"},
            { "RegistrationText", "Enregistrement de l'Avatar"},
            { "RegistrationsDisabled", "Les inscriptions sont actuellement désactivés, s'il vous plaît réessayez à nouveau dans quelques temps..."},
            { "TermsOfServiceText", "Conditions d'utilisation"},
            { "TermsOfServiceAccept", "Acceptez-vous les Conditions d'utilisation détaillés ci-dessus?"},
            { "AvatarNameError", "Vous n'avez pas saisi un nom d'avatar!"},
            { "AvatarPasswordError", "Mot de passe est vide ou ne correspondant pas!"},
            { "AvatarEmailError", "Une adresse e-mail est nécessaire pour la récupération de mot de passe! {'none' si inconnu"},
            { "AvatarNameSpacingError", "Votre nom d'avatar devrait être «Prénom Nom»!"},

            // News
            { "OpenNewsManager", "Ouvrez le Gestionnaire des News"},
            { "NewsManager", "Gestionnaire des News"},
            { "EditNewsItem", "Editer  un Article News"},
            { "AddNewsItem", "Ajouter une News"},
            { "DeleteNewsItem", "Effacer une News"},
            { "NewsDateText", "Date de la News"},
            { "NewsTitleText", "Title de la News"},
            { "NewsItemTitle", "Titre Article News"},
            { "NewsItemText", "Texte Article News"},
            { "AddNewsText", "Ajouter des News"},
            { "DeleteNewsText", "Effacer des News"},
            { "EditNewsText", "Editer des News"},

            // User profile
            { "UserProfileFor", "Profil Utilisateur pour"},
            { "UsersGroupsText", "Groupes Joints"},
            { "GroupNameText", "Groupe"},
            { "UsersPicksText", "Choix pour"},
            { "ResidentSince", "Resident depuis"},
            { "AccountType", "Type de compte"},
            { "PartnersName", "Nom des Partenaires"},
            { "AboutMe", "À propos de moi"},
            { "IsOnlineText", "Status de l'Utilisateur"},
            { "OnlineLocationText", "Emplacement actuelle de l'utilisateur"},
            { "Partner", "Partner"},
            { "Friends", "Friends"},
            { "Nothing", "Aucun"},
            { "ChangePass", "Changer le mot de passe"},
            { "NoChangePass", "Impossible de changer le mot de passe, s'il vous plaît réessayer plus tard"},

            // Region Information
            { "RegionInformationText", "Information sur la région"},
            { "OwnerNameText", "Nom du propriétaire"},
            { "RegionLocationText", "Emplacement de la Région"},
            { "RegionSizeText", "Taille de la Région"},
            { "RegionNameText", "Nom de la Région"},
            { "RegionTypeText", "Type de Région"},
            { "RegionPresetTypeText", "Région Preset"},
            { "RegionDelayStartupText", "Delay starting scripts"},
            { "RegionPresetText", "Region Preset"},
            { "RegionTerrainText", "Région Terrain"},
            { "ParcelsInRegionText", "Parcelles dans la Région"},
            { "ParcelNameText", "Nom de la Parcelle"},
            { "ParcelOwnerText", "Nom des Propriétaires de Parcelles"},

            // Region list
            { "RegionInfoText", "Information de la Région"},
            { "RegionListText", "Liste des Régions"},
            { "RegionLocXText", "Région X"},
            { "RegionLocYText", "Région Y"},
            { "SortByLocX", "Trié par Région X"},
            { "SortByLocY", "Trié par Région Y"},
            { "SortByName", "Trié par Nom de Région"},
            { "RegionMoreInfo", "Plus d'informations"},
            { "RegionMoreInfoTooltips", "Plus d'informations au sujet de"},
            { "OnlineUsersText", "Utilisateurs en Ligne"},
            { "OnlineFriendsText", "Online Friends"},
            { "RegionOnlineText", "Status de la Région"},
            { "RegionMaturityText", "Note d'accès"},
            { "NumberOfUsersInRegionText", "Nombre d'Utilisateurs dans la Région"},

            // Region manager
            { "AddRegionText", "Ajouter Région"},
            { "Mainland", "Territoire continental"},
            { "Estate", "Biens"},
            { "FullRegion", "Région complète"},
            { "Homestead", "Propriété"},
            { "Openspace", "Espace ouvert"},
            { "Flatland", "Terrain plat"},
            { "Grassland", "Prairie"},
            { "Island", "Île"},
            { "Aquatic", "Aquatique"},
            { "Custom", "Coutume"},
            { "RegionPortText", "Port Région"},
            { "RegionVisibilityText", "Visible aux voisins"},
            { "RegionInfiniteText", "Infini Région"},
            { "RegionCapacityText", "Capacité d'objet Région"},
            { "NormalText", "Ordinaire"},
            { "DelayedText", "Différé"},

            // Estate management
            {"AddEstateText", "Ajouter immobilier"},
            {"EstateText", "Biens"},
            {"EstatesText", "Estates"},
            {"PricePerMeterText", "Prix par mètre carrér"},
            {"PublicAccessText", "Accès publique"},
            {"AllowVoiceText", "Autoriser la voix"},
            {"TaxFreeText", "Tax Free"},
            {"AllowDirectTeleportText", "Autoriser téléporter directement"},

            // Menus
            { "MenuHome", "Accueil"},
            { "MenuLogin", "Connection"},
            { "MenuLogout", "Déconnexion"},
            { "MenuRegister", "Inscription"},
            { "MenuForgotPass", "Mot de Passe Oublié"},
            { "MenuNews", "News"},
            { "MenuWorld", "Monde"},
            { "MenuWorldMap", "Carte de Monde"},
            { "MenuRegion", "Liste des Régions"},
            { "MenuUser", "Utilisateurs"},
            { "MenuOnlineUsers", "Utilisateurs en Ligne"},
            { "MenuUserSearch", "Rechercher Utilisateur"},
            { "MenuRegionSearch", "Rechercher Région"},
            { "MenuChat", "Chat"},
            { "MenuHelp", "Aide"},
            { "MenuViewerHelp", "Viewer Help"},
            { "MenuChangeUserInformation", "Modifier Infos Utilisateur"},
            { "MenuWelcomeScreenManager", "Gestion Ecran Bienvenue"},
            { "MenuNewsManager", "Gestion des News"},
            { "MenuUserManager", "Gestion Utilisateurs"},
            { "MenuFactoryReset", "Réinitialiser"},
            { "ResetMenuInfoText", "Réinitialise les éléments de menu aux valeurs par défaut les plus à jour"},
            { "ResetSettingsInfoText", "Réinitialise les réglages de l'interface Web aux valeurs par défaut les plus à jour"},
            { "MenuPageManager", "Gestion des Pages"},
            { "MenuSettingsManager", "Gestion des paramètres"},
            { "MenuManager", "Gestion"},
            { "MenuSettings", "Paramètres"},
            { "MenuRegionManager", "Manager Région"},
            { "MenuEstateManager", "Gestionnaire immobilier"},
            { "MenuManagerSimConsole", "Simulateur Console"},
            { "MenuPurchases", "Achats de l'utilisateur"},
            { "MenuMyPurchases", "Mes Achats"},
            { "MenuTransactions", "Transactions de l'utilisateur"},
            { "MenuMyTransactions", "Mes Transactions"},
            { "MenuClassifieds", "Petites annonces"},
            { "MenuMyClassifieds", "Mes Petites annonces"},
            { "MenuEvents", "Événements"},
            { "MenuMyEvents", "Mes Événements"},
            { "MenuStatistics", "Statistiques Viewer"},
            { "MenuGridSettings", "Les paramètres de grille"},

            // Menu Tooltips
            { "TooltipsMenuHome", "Accueil"},
            { "TooltipsMenuLogin", "Connection"},
            { "TooltipsMenuLogout", "Déconnection"},
            { "TooltipsMenuRegister", "Inscription"},
            { "TooltipsMenuForgotPass", "Mot de Passe Oublié"},
            { "TooltipsMenuNews", "News"},
            { "TooltipsMenuWorld", "Monde"},
            { "TooltipsMenuWorldMap", "Carte du Monde"},
            { "TooltipsMenuUser", "Utilisateurs"},
            { "TooltipsMenuOnlineUsers", "Utilisateurs en ligne"},
            { "TooltipsMenuUserSearch", "Rechercher un Utilisateurs"},
            { "TooltipsMenuRegionSearch", "Rechercher un Région"},
            { "TooltipsMenuChat", "Chat"},
            { "TooltipsMenuViewerHelp", "Aide"},
            { "TooltipsMenuHelp", "Help"},
            { "TooltipsMenuChangeUserInformation", "Modifier les informations de l'utilisateur"},
            { "TooltipsMenuWelcomeScreenManager", "Gestionnaire de l'Ecran de Bienvenue"},
            { "TooltipsMenuNewsManager", "Gestionnaire des News"},
            { "TooltipsMenuUserManager", "Gestionnaire des Utilisateurs"},
            { "TooltipsMenuFactoryReset", "Réinitialiser"},
            { "TooltipsMenuPageManager", "Gestionnaire de Pages"},
            { "TooltipsMenuSettingsManager", "Gestionnaire de paramètres"},
            { "TooltipsMenuManager", "Gestion Administrative"},
            { "TooltipsMenuSettings", "WebUI Paramètres"},
            { "TooltipsMenuRegionManager", "Région créer / modifier"},
            { "TooltipsMenuEstateManager", "Gestionnaire immobilier"},
            { "TooltipsMenuManagerSimConsole", "Console de simulateur en ligne"},
            { "TooltipsMenuPurchases", "Informations d'achat"},
            { "TooltipsMenuTransactions", "Informations sur la transaction"},
            { "TooltipsMenuClassifieds", "Petites informations"},
            { "TooltipsMenuEvents", "Information sur l'événement"},
            { "TooltipsMenuStatistics", "Statistiques Viewer"},
            { "TooltipsMenuGridSettings", "Les paramètres de grille"},

            // Menu Region box
            { "MenuRegionTitle", "Région"},
            { "MenuParcelTitle", "Colis"},
            { "MenuOwnerTitle", "Propriétaire"},
            { "TooltipsMenuRegion", "Détails de la région"},
            { "TooltipsMenuParcel", "Colis Région"},
            { "TooltipsMenuOwner", "Immobilier Propriétaire"},

            // Menu Profile
            { "MenuProfileTitle", "Profil"},
            { "MenuGroupTitle", "Groupe"},
            { "MenuPicksTitle", "Picks"},
            { "MenuRegionsTitle", "Régions"},
            { "TooltipsMenuProfile", "Profil utilisateur"},
            { "TooltipsMenuGroups", "Groupes d'utilisateurs"},
            { "TooltipsMenuPicks", "Choix de l'utilisateur"},
            { "TooltipsMenuRegions", "Les régions de l'utilisateur"},
            { "UserGroupNameText", "Groupe d'utilisateurs"},
            { "PickNameText", "Choisissez nom"},
            { "PickRegionText", "Emplacement"},

            // Urls
            { "WelcomeScreen", "Ecran de Bienvenue"},

            // Tooltips Urls
            { "TooltipsWelcomeScreen", "Ecran de Bienvenue"},
            { "TooltipsWorldMap", "Carte du Monde"},

            // Index
            { "HomeText", "Accueil"},
            { "HomeTextWelcome",
                    "Ceci est notre Nouveau Monde Virtuel! Rejoignez-nous dés maintenant, et faites la différence!"},
            { "HomeTextTips", "Nouvelles présentations"},
            { "WelcomeToText", "Bienvenue"},

            // World Map Page
            { "WorldMap", "Carte du Monde"},
            { "WorldMapText", "Plein Ecran"},

            // Chat Page
            { "ChatText", "Chat de Support"},

            // Help Page
            { "HelpText", "Aide"},
            { "HelpViewersConfigText", "Configuration Viewer"},

            // Logout
            { "Logout", "Déconnection"},
            { "LoggedOutSuccessfullyText", "Vous avez été déconnecté avec succès."},
 
            // Change user information
            { "ChangeUserInformationText", "Modifier les informations de l'utilisateur"},
            { "ChangePasswordText", "Changer de Mot de Passe"},
            { "NewPasswordText", "Nouveau Mot de Passe"},
            { "NewPasswordConfirmationText", "Nouveau Mot de Passe {Confirmation)"},
            { "ChangeEmailText", "Changer d'Adresse Email"},
            { "NewEmailText", "Nouvelle Adresse Email"},
            { "DeleteUserText", "Effacer mon Compte"},
            { "DeleteText", "Effacer"},
            { "DeleteUserInfoText",
                    "Cela permettra d'éliminer toutes les informations vous concernant dans la grille et retirer votre accès à ce service. Si vous souhaitez continuer, saisissez votre nom et mot de passe et cliquez sur Supprimer."},
            { "EditText", "Editer"},
            { "EditUserAccountText", "Modifier un compte utilisateur"},

            // Maintenance 
            { "WebsiteDownInfoText", "Le Site Web est actuellement en panne, s'il vous plaît réessayez ultérieurement..."},
            { "WebsiteDownText", "Site Web Hors Ligne"},

            // Http 404
            { "Error404Text", "Code d'Erreur"},
            { "Error404InfoText", "404 La page n'a pu être trouvée"},
            { "HomePage404Text", "Page d'Accueil"},

            // Http 505
            { "Error505Text", "Code d'Erreur"},
            { "Error505InfoText", "505 Erreur Interne du Server"},
            { "HomePage505Text", "Page d'Accueil"},

            // User search
            { "Search", "Rechercher"},
            { "SearchText", "Rechercher"},
            { "SearchForUserText", "Recherche par Utilisateur"},
            { "UserSearchText", "Rechercher un Utilisateur"},
            { "SearchResultForUserText", "Résultat de la Recherche pour l'Utilisateur"},

            // Region_search
            { "SearchForRegionText", "Rechercher une Région"},
            { "RegionSearchText", "Rechercher une Région"},
            { "SearchResultForRegionText", "Résultat de recherche de la région"},

            // Edit user
            { "AdminDeleteUserText", "Supprimer l'utilisateur"},
            { "AdminDeleteUserInfoText", "Cette commande supprime le compte et détruit toutes les informations qui lui sont associés."},
            { "BanText", "Bannir"},
            { "UnbanText", "Débannir"},
            { "AdminTempBanUserText", "Temps de Bannissement"},
            { "AdminTempBanUserInfoText", "Cela empêche l'utilisateur de se connecter pour un laps de temps."},
            { "AdminBanUserText", "Bannir l'utilisateur"},
            { "AdminBanUserInfoText", "Cela empêche l'utilisateur de se connecter tant que l'interdiction n'est pas supprimée."},
            { "AdminUnbanUserText", "Débannir un Utilisateur"},
            { "AdminUnbanUserInfoText", "Supprime les interdictions temporaires et permanentes sur l'utilisateur."},
            { "AdminLoginInAsUserText", "Connectez-vous en tant qu'utilisateur"},
            { "AdminLoginInAsUserInfoText",
                    "Vous serez déconnecté de votre compte admin, et connecté en tant que cet utilisateur, et vous verrez tout comme ils le voient."},
            { "TimeUntilUnbannedText", "Temps jusqu'à la levée du Bannissement de l'utilisateur"},
            { "BannedUntilText", "L'utilisateur est interdit jusqu'à ce que"},
            { "KickAUserText", "Kicker l'utilisateur"},
            { "KickAUserInfoText", "Kicker l'utilisateur {L'utilisateur sera déconnecté dans les 30 secondes)"},
            { "KickMessageText", "Message à l'utilisateur"},
            { "KickUserText", "Kicker l'utilisateur"},
            { "MessageAUserText", "Send User A Message"},
            { "MessageAUserInfoText", "Sends a user a blue-box message (will arrive within 30 seconds)"},
            { "MessageUserText", "Message User"},

            // Transactions
            { "TransactionsText", "Transactions"},
            { "DateInfoText", "Sélectionnez une plage de dates"},
            { "DateStartText", "Commençant date"},
            { "DateEndText", "Date de fin"},
            { "30daysPastText", "30 jours précédents"},
            { "TransactionToAgentText", "To User"},
            { "TransactionFromAgentText", "From User"},
            { "TransactionDateText", "Date"},
            { "TransactionDetailText", "Description"},
            { "TransactionAmountText", "Amount"},
            { "TransactionBalanceText", "Balance"},
            { "NoTransactionsText", "Aucune transaction trouvé..."},
            { "PurchasesText", "Achats"},
            { "LoggedIPText", "Adresse IP enregistrée"},
            { "NoPurchasesText", "Aucun achat trouvés..."},
            { "PurchaseCostText", "Coût"},
            
            // Classifieds
            { "ClassifiedsText", "Annonce Catégorisée"},
            { "ClassifiedText", "Petites annonces"},
            { "ListedByText", "Répertorié par"},
            { "CreationDateText", "Date de l'affichage"},
            { "ExpirationDateText", "Date d'expiration" },
            { "DescriptionText", "La description" },
            { "PriceOfListingText", "Prix"},

            // Classified categories
            { "CatAll", "Tout"},
            { "CatSelect", ""},
            { "CatShopping", "Achats"},
            { "CatLandRental", "Location de terrains"},
            { "CatPropertyRental", "Location de la propriété"},
            { "CatSpecialAttraction", "Attraction spéciale"},
            { "CatNewProducts", "Nouveaux Produits"},
            { "CatEmployment", "Emploi"},
            { "CatWanted", "Voulait"},
            { "CatService", "Un service"},
            { "CatPersonal", "Personnel"},
           
            // Events
            { "EventsText", "Événements"},
            { "EventNameText", "Un Événement"},
            { "EventLocationText", "Où"},
            { "HostedByText","Hébergé par"},
            { "EventDateText", "Quand"},
            { "EventTimeInfoText", "Le temps de l'événement doit être heure locale"},
            { "CoverChargeText", "Couvert"},
            { "DurationText", "Durée"},
            { "AddEventText", "Ajouter un évènementt"},

            // Event categories
            { "CatDiscussion", "Discussion"},
            { "CatSports", "Des sports"},
            { "CatLiveMusic", "Musique live"},
            { "CatCommercial", "Commercial"},
            { "CatEntertainment", "Vie nocturne/Divertissement"},
            { "CatGames", "Des jeux/Concours"},
            { "CatPageants", "Pageants"},
            { "CatEducation", "Éducation"},
            { "CatArtsCulture", "Lettres/Culture"},
            { "CatCharitySupport", "Charité/Groupe de soutien"},
            { "CatMiscellaneous", "Divers"},

            // Event lookup periods
            { "Next24Hours", "Suivant 24 heures"},
            { "Next10Hours", "Suivant 10 heures"},
            { "Next4Hours", "Suivant 4 heures"},
            { "Next2Hours", "Suivant 2 heures"},

            // Sim Console
            { "SimConsoleText", "Sim Command Console"},
            { "SimCommandText", "Command"},

            // Statistics
            { "StatisticsText", "Statistiques Viewer"},
            { "ViewersText", "Utilisation Viewer"},
            { "GPUText", "Les cartes graphiques"},
            { "PerformanceText", "Moyennes de performance"},
            { "FPSText", "Images / seconde"},
            { "RunTimeText", "Durée"},
            { "RegionsVisitedText", "Régions visitées"},
            { "MemoryUseageText", "Utilisation de la mémoire"},
            { "PingTimeText", "Ping temps"},
            { "AgentsInViewText", "Agents en vue"},
            { "ClearStatsText", "Effacer les statistiques sur"},

            // Abuse reports
            { "MenuAbuse", "Abus Rapports"},
            { "TooltipsMenuAbuse", "Utilisateur abuse journaliste"},
            { "AbuseReportText", "Signaler un abus"},
            { "AbuserNameText", "Abuser"},
            { "AbuseReporterNameText", "Journaliste"},
            { "AssignedToText", "Assigné à"},

            // Factory_reset
            { "FactoryReset", "Réinitialiser"},
            { "ResetMenuText", "Réinitialiser les paramètres par défaut du menu"},
            { "ResetSettingsText", "Rétablir les paramètres Web {page Gestionnaire de paramètres) par défaut"},
            { "Reset", "Réinitialiser"},
            { "Settings", "Paramètres"},
            { "Pages", "Pages"},
            { "UpdateRequired", "mise à jour requise"},
            { "DefaultsUpdated",
                    "Mise à jour par défaut, rendez-vous sur \"Réinitialiseré\" ou \"Gestionnaire de paramètres\" pour désactiver cet avertissement."},

            // Page_manager
            { "PageManager", "Gestionnaire de Pages"},
            { "SaveMenuItemChanges", "Enregistrer un élément de menu"},
            { "SelectItem", "Selectionner un élément"},
            { "DeleteItem", "Effacer un élément"},
            { "AddItem", "Ajouter un élément"},
            { "PageLocationText", "Emplacement de la page"},
            { "PageIDText", "ID de la Page"},
            { "PagePositionText", "Position de la Page"},
            { "PageTooltipText", "Tooltip de la Page"},
            { "PageTitleText", "Titre de la Page"},
            { "RequiresLoginText", "Vous devez vous connecter pour voir"},
            { "RequiresLogoutText", "Vous devez vous déconnecter pour voir"},
            { "RequiresAdminText", "Vous devez vous connecter en temps qu'Admin pour voir"},
            { "RequiresAdminLevelText", "Required Admin Level To View"},

            // Grid settings
            { "GridSettingsManager", "Grille Settings Manager "},
            { "GridnameText", "Nom de Grille "},
            { "GridnickText", "Grille surnom "},
            { "WelcomeMessageText", "Connectez message de bienvenue "},
            { "GovernorNameText", "Gouverneur du système"},
            { "MainlandEstateNameText", "Succession continentale"},
            { "RealEstateOwnerNameText", "Ownername immobilier du système"},
            { "SystemEstateNameText", "Le nom du système de succession"},
            { "BankerNameText", "Banquier du système"},
            { "MarketPlaceOwnerNameText", "Propriétaire du marché du système"},

            // Settings manager page
            { "WebRegistrationText", "Enregistrements Web autorisés"},
            { "GridCenterXText", "Grille Location Centrer X"},
            { "GridCenterYText", "Grille Location Centrer Y"},
            { "SettingsManager", "Gestionnaire de paramètres"},
            { "IgnorePagesUpdatesText", "Ignorer les avertissements de mises à jour des pages jusqu'à la prochaine mise à jour"},
            { "IgnoreSettingsUpdatesText", "Ignorer les avertissements de mises à jour des paramètres jusqu'à la prochaine mise à jour"},
            { "HideLanguageBarText", "Masquer la barre de sélection de la langue"},
            { "HideStyleBarText", "Masquer la barre de sélection de style"},
            { "HideSlideshowBarText", "Masquer diaporama bar"},
            { "LocalFrontPageText", "Page d'accueil locale"},
            { "LocalCSSText", "CSS local feuille de style"},

            // Dates
            { "Sun", "Dim"},
            { "Mon", "Lun"},
            { "Tue", "Mar"},
            { "Wed", "Mer"},
            { "Thu", "Jeu"},
            { "Fri", "Ven"},
            { "Sat", "Sam"},
            { "Sunday", "Dimanche"},
            { "Monday", "Lundi"},
            { "Tuesday", "Mardi"},
            { "Wednesday", "Mercredi"},
            { "Thursday", "Jeudi"},
            { "Friday", "Vendredi"},
            { "Saturday", "Samedi"},

            { "Jan_Short", "Jan"},
            { "Feb_Short", "Fev"},
            { "Mar_Short", "Mar"},
            { "Apr_Short", "Avr"},
            { "May_Short", "Mai"},
            { "Jun_Short", "Jun"},
            { "Jul_Short", "Jui"},
            { "Aug_Short", "Aou"},
            { "Sep_Short", "Sep"},
            { "Oct_Short", "Oct"},
            { "Nov_Short", "Nov"},
            { "Dec_Short", "Dec"},

            { "January", "Janvier"},
            { "February", "Février"},
            { "March", "Mars"},
            { "April", "Avril"},
            { "May", "Mai"},
            { "June", "Juin"},
            { "July", "Juillet"},
            { "August", "Août"},
            { "September", "Septembre"},
            { "October", "Octobre"},
            { "November", "Novembre"},
            { "December", "Decembre"},

            // User types
            { "UserTypeText", "Type d'utilisateur"},
            { "AdminUserTypeInfoText", "Le type d'utilisateur {Actuellement utilisé pour les paiements allocations de formation périodiques)."},
            { "Guest", "Invité"},
            { "Resident", "Résident"},
            { "Member", "Membre"},
            { "Contractor", "Entrepreneur"},
            { "Charter_Member", "Membre de la Charte"},

            // ColorBox
            { "ColorBoxImageText", "Image"},
            { "ColorBoxOfText", "sur"},
            { "ColorBoxPreviousText", "Précédent"},
            { "ColorBoxNextText", "Suivant"},
            { "ColorBoxCloseText", "Fermer"},
            { "ColorBoxStartSlideshowText", "Démarrer Slide Show"},
            { "ColorBoxStopSlideshowText", "Arrêter Slide Show"},

            // Maintenance
            { "NoAccountFound", "Aucun compte trouvé"},
            { "DisplayInMenu", "Affichage dans le menu"},
            { "ParentText", "Menu Parent"},
            { "CannotSetParentToChild", "Vous ne pouvez pas définir de menu comme un enfant à se."},
            { "TopLevel", "Haut niveau"},

            // Style Switcher
            { "styles1", "Defaut Minimaliste"},
            { "styles2", "Dégardé Clair"},
            { "styles3", "Bleu Nuit"},
            { "styles4", "Dégradé Foncé"},
            { "styles5", "Luminus"},

            { "StyleSwitcherStylesText", "Styles"},
            { "StyleSwitcherLanguagesText", "Langages"},
            { "StyleSwitcherChoiceText", "Choix"},

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
