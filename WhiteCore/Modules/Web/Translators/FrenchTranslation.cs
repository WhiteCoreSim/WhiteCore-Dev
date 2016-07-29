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
    public class FrenchTranslation : ITranslator
    {
        public string LanguageName
        {
            get { return "fr"; }
        }

        public string GetTranslatedString(string key)
        {
            switch (key)
            {
                // Generic
                case "No": return "Non";
                case "Yes": return "Oui";
                case "FirstText": return "Premier";
                case "Submit": return "Envoyer";
                case "Accept": return "Accepter";
                case "Save": return "Sauver";
                case "BackText": return "Précédent";
                case "NextText": return "Suivant";
                case "LastText": return "Dernier";
                case "CurrentPageText": return "Page actuelle";
                case "MoreInfoText": return "Plus d'informations";
                case "NoDetailsText": return "Pas de détails trouvés...";
            case "MoreInfo":
                return "Plus d'informations";

            case "ObjectNameText": return "Objet";
            case "LocationText": return "Emplacement";
            case "UUIDText": return "UUID";
            case "DetailsText": return "Description";
            case "NotesText": return "Remarques";
            case "SaveUpdates": return "Enregistrer les mises à jour";
            case "ActiveText": return "Actif";
            case "CheckedText": return "Vérifié";
            case "CategoryText": return "Catégorie";
            case "SummaryText": return "Résumé";
                
                //Status information
                case "GridStatus": return "Etat de la Grille";
                case "Online": return "En Ligne";
                case "Offline": return "Hors Ligne";
                case "TotalUserCount": return "Nombre total d'utilisateurs";
                case "TotalRegionCount": return "Nombre total de régions";
                case "UniqueVisitors": return "Visiteurs unique (30 jours)";
                case "OnlineNow": return "En ligne maintenant";
                case "HyperGrid": return "HyperGrid (HG)";
                case "Voice": return "Voix";
                case "Currency": return "Monnaie";
                case "Disabled": return "Désactivé";
                case "Enabled": return "Activé";
                case "News": return "Nouveautés";
                case "Region": return "Région";

                //User login
                case "Login": return "Connection";
                case "UserName": return "Nom d'utilisateur";
                case "UserNameText": return "Nom d'utilisateur";
                case "Password": return "Mot de passe";
                case "PasswordText": return "Mot de passe";
                case "PasswordConfirmation": return "Confirmer Mot de passe";
                case "ForgotPassword": return "Mot de passe oublié?";

                // Special windows
                case "SpecialWindowTitleText": return "Titre spécial de la fenêtre Info";
                case "SpecialWindowTextText": return "Texte spécial de la fenêtre Infos";
                case "SpecialWindowColorText": return "Couleur spécial de la fenêtre Infos";
                case "SpecialWindowStatusText": return "Status spécial de la fenêtre Infos";
                case "WelcomeScreenManagerFor": return "Welcome Screen Manager pour";
                case "ChangesSavedSuccessfully": return "Changements enregistrés avec succès";

                // User registration
                case "AvatarNameText": return "Nom de l'Avatar";
                case "AvatarScopeText": return "Scope ID de l'Avatar";
                case "FirstNameText": return "Votre Nom";
                case "LastNameText": return "Votre Prénom";
                case "UserAddressText": return "Votre Addresse";
                case "UserZipText": return "Votre Code Zip";
                case "UserCityText": return "Votre Ville";
                case "UserCountryText": return "Votre Pays";
                case "UserDOBText": return "Votre date d'anniversaire (Mois Jour Année)";
                case "UserEmailText": return "Votre Email";
                case "UserHomeRegionText": return "Accueil région";
                case "RegistrationText": return "Enregistrement de l'Avatar";
                case "RegistrationsDisabled": return "Les inscriptions sont actuellement désactivés, s'il vous plaît réessayez à nouveau dans quelques temps...";
                case "TermsOfServiceText": return "Conditions d'utilisation";
                case "TermsOfServiceAccept": return "Acceptez-vous les Conditions d'utilisation détaillés ci-dessus?";
                case "AvatarNameError": return "Vous n'avez pas saisi un nom d'avatar!";
                case "AvatarPasswordError": return "Mot de passe est vide ou ne correspondant pas!";
                case "AvatarEmailError": return "Une adresse e-mail est nécessaire pour la récupération de mot de passe! ('none' si inconnu";
                case "AvatarNameSpacingError": return "Votre nom d'avatar devrait être «Prénom Nom»!";

                // news
                case "OpenNewsManager": return "Ouvrez le Gestionnaire des News";
                case "NewsManager": return "Gestionnaire des News";
                case "EditNewsItem": return "Editer  un Article News";
                case "AddNewsItem": return "Ajouter une News";
                case "DeleteNewsItem": return "Effacer une News";
                case "NewsDateText": return "Date de la News";
                case "NewsTitleText": return "Title de la News";
                case "NewsItemTitle": return "Titre Article News";
                case "NewsItemText": return "Texte Article News";
                case "AddNewsText": return "Ajouter des News";
                case "DeleteNewsText": return "Effacer des News";
                case "EditNewsText": return "Editer des News";

                // Users
                case "UserProfileFor": return "Profil Utilisateur pour";
                case "UsersGroupsText": return "Groupes Joints";
                case "GroupNameText": return "Groupe";
                case "UsersPicksText": return "Choix pour";
                case "ResidentSince":
                    return "Resident depuis";
                case "AccountType":
                    return "Type de compte";
                case "PartnersName":
                    return "Nom des Partenaires";
                case "AboutMe":
                    return "À propos de moi";
                case "IsOnlineText":
                    return "Status de l'Utilisateur";
                case "OnlineLocationText":
                    return "Emplacement actuelle de l'utilisateur";

                case "RegionInformationText":
                    return "Information sur la région";
                case "OwnerNameText":
                    return "Nom du propriétaire";
                case "RegionLocationText":
                    return "Emplacement de la Région";
                case "RegionSizeText":
                    return "Taille de la Région";
                case "RegionNameText":
                    return "Nom de la Région";
                case "RegionTypeText":
                    return "Type de Région";
                case "RegionTerrainText":
                    return "Région Terrain";
                case "ParcelsInRegionText":
                    return "Parcelles dans la Région";
                case "ParcelNameText":
                    return "Nom de la Parcelle";
                case "ParcelOwnerText":
                    return "Nom des Propriétaires de Parcelles";

                // Region Page
                case "RegionInfoText":
                    return "Information de la Région";
                case "RegionListText":
                    return "Liste des Régions";
                case "RegionLocXText":
                    return "Région X";
                case "RegionLocYText":
                    return "Région Y";
                case "RegionMaturityText":
                    return "Access Rating";
                case "SortByLocX":
                    return "Trié par Région X";
                case "SortByLocY":
                    return "Trié par Région Y";
                case "SortByName":
                    return "Trié par Nom de Région";
                case "RegionMoreInfo":
                    return "Plus d'informations";
                case "RegionMoreInfoTooltips":
                    return "Plus d'informations au sujet de";
                case "OnlineUsersText":
                    return "Utilisateurs en Ligne";
                case "RegionOnlineText":
                    return "Status de la Région";
                case "NumberOfUsersInRegionText":
                    return "Nombre d'Utilisateurs dans la Région";

                // Menu Buttons
                case "MenuHome": return "Accueil";
                case "MenuLogin": return "Connection";
                case "MenuLogout": return "Déconnexion";
                case "MenuRegister": return "Inscription";
                case "MenuForgotPass": return "Mot de Passe Oublié";
                case "MenuNews": return "News";
                case "MenuWorld": return "Monde";
                case "MenuWorldMap": return "Carte de Monde";
                case "MenuRegion": return "Liste des Régions";
                case "MenuUser": return "Utilisateurs";
                case "MenuOnlineUsers": return "Utilisateurs en Ligne";
                case "MenuUserSearch": return "Rechercher Utilisateur";
                case "MenuRegionSearch": return "Rechercher Région";
                case "MenuChat": return "Chat";
                case "MenuHelp": return "Aide";
                case "MenuChangeUserInformation": return "Modifier Infos Utilisateur";
                case "MenuWelcomeScreenManager": return "Gestion Ecran Bienvenue";
                case "MenuNewsManager": return "Gestion des News";
                case "MenuUserManager": return "Gestion Utilisateurs";
                case "MenuFactoryReset": return "Réinitialiser";
                case "ResetMenuInfoText": return "Réinitialise les éléments de menu aux valeurs par défaut les plus à jour";
                case "ResetSettingsInfoText": return "Réinitialise les réglages de l'interface Web aux valeurs par défaut les plus à jour";
                case "MenuPageManager": return "Gestion des Pages";
                case "MenuSettingsManager": return "Gestion des paramètres";
                case "MenuManager": return "Gestion";
                case "MenuSettings": return "Paramètres";
                case "MenuRegionManager": return "Manager Région";
                case "MenuManagerSimConsole": return "Simulateur Console";
                case "MenuPurchases": return "Achats de l'utilisateur";
                case "MenuMyPurchases": return "Mes Achats";
                case "MenuTransactions": return "Transactions de l'utilisateur";
                case "MenuMyTransactions": return "Mes Transactions";
                case "MenuMyClassifieds": return "Mes Classifieds <NT>";                
                case "MenuStatistics": return "Statistiques Viewer";
                case "MenuGridSettings": return "Les paramètres de grille";
                
                // Tooltips Menu Buttons
                case "TooltipsMenuHome": return "Accueil";
                case "TooltipsMenuLogin": return "Connection";
                case "TooltipsMenuLogout": return "Déconnection";
                case "TooltipsMenuRegister": return "Inscription";
                case "TooltipsMenuForgotPass": return "Mot de Passe Oublié";
                case "TooltipsMenuNews": return "News";
                case "TooltipsMenuWorld": return "Monde";
                case "TooltipsMenuWorldMap": return "Carte du Monde";
                case "TooltipsMenuUser": return "Utilisateurs";
                case "TooltipsMenuOnlineUsers": return "Utilisateurs en ligne";
                case "TooltipsMenuUserSearch": return "Rechercher un Utilisateurs";
                case "TooltipsMenuRegionSearch": return "Rechercher un Région";
                case "TooltipsMenuChat": return "Chat";
                case "TooltipsMenuHelp": return "Aide";
                case "TooltipsMenuChangeUserInformation": return "Modifier les informations de l'utilisateur";
                case "TooltipsMenuWelcomeScreenManager": return "Gestionnaire de l'Ecran de Bienvenue";
                case "TooltipsMenuNewsManager": return "Gestionnaire des News";
                case "TooltipsMenuUserManager": return "Gestionnaire des Utilisateurs";
                case "TooltipsMenuFactoryReset": return "Réinitialiser";
                case "TooltipsMenuPageManager": return "Gestionnaire de Pages";
                case "TooltipsMenuSettingsManager": return "Gestionnaire de paramètres";
                case "TooltipsMenuManager": return "Gestion Administrative";
                case "TooltipsMenuSettings": return "WebUI Paramètres";
                case "TooltipsMenuRegionManager": return "Région créer / modifier";
                case "TooltipsMenuManagerSimConsole": return "Console de simulateur en ligne";
                case "TooltipsMenuPurchases": return "Informations d'achat";
                case "TooltipsMenuTransactions": return "Informations sur la transaction";
                case "TooltipsMenuStatistics": return "Statistiques Viewer";
                case "TooltipsMenuGridSettings": return "Les paramètres de grille";
                
                // Menu Region
                case "MenuRegionTitle": return "Région";
                case "MenuParcelTitle": return "Colis";
                case "MenuOwnerTitle": return "Propriétaire";
                case "TooltipsMenuRegion": return "Détails de la région";
                case "TooltipsMenuParcel": return "Colis Région";
                case "TooltipsMenuOwner": return "Immobilier Propriétaire";

                // Menu Profile
                case "MenuProfileTitle": return "Profil";
                case "MenuGroupTitle": return "Groupe";
                case "MenuPicksTitle": return "Picks";
                case "MenuRegionsTitle": return "Régions";
                case "TooltipsMenuProfile": return "Profil utilisateur";
                case "TooltipsMenuGroups": return "Groupes d'utilisateurs";
                case "TooltipsMenuPicks": return "Choix de l'utilisateur";
                case "TooltipsMenuRegions": return "Les régions de l'utilisateur";
                case "UserGroupNameText": return "Groupe d'utilisateurs";
                case "PickNameText": return "Choisissez nom";
                case "PickRegionText": return "Emplacement";

                // Urls
                case "WelcomeScreen":
                    return "Ecran de Bienvenue";

                // Tooltips Urls
                case "TooltipsWelcomeScreen":
                    return "Ecran de Bienvenue";
                case "TooltipsWorldMap":
                    return "Carte du Monde";

                // Style Switcher
                case "styles1":
                    return "Defaut Minimaliste";
                case "styles2":
                    return "Dégardé Clair";
                case "styles3":
                    return "Bleu Nuit";
                case "styles4":
                    return "Dégradé Foncé";
                case "styles5":
                    return "Luminus";

                case "StyleSwitcherStylesText":
                    return "Styles";
                case "StyleSwitcherLanguagesText":
                    return "Langages";
                case "StyleSwitcherChoiceText":
                    return "Choix";

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
                    return "Accueil";
                case "HomeTextWelcome":
                    return
                        "Ceci est notre Nouveau Monde Virtuel! Rejoignez-nous dés maintenant, et faites la différence!";
                case "HomeTextTips":
                    return "Nouvelles présentations";
                case "WelcomeToText":
                    return "Bienvenue";

                // World Map Page
                case "WorldMap":
                    return "Carte du Monde";
                case "WorldMapText":
                    return "Plein Ecran";

                // Chat Page
                case "ChatText":
                    return "Chat de Support";

                // Help Page
                case "HelpText":
                    return "Aide";
                case "HelpViewersConfigText":
                    return "Configuration Viewer";
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
                    return "Vous avez été déconnecté avec succès.";
                case "Logout":
                    return "Déconnection";

                //Change user information page
                case "ChangeUserInformationText":
                    return "Modifier les informations de l'utilisateur";
                case "ChangePasswordText":
                    return "Changer de Mot de Passe";
                case "NewPasswordText":
                    return "Nouveau Mot de Passe";
                case "NewPasswordConfirmationText":
                    return "Nouveau Mot de Passe (Confirmation)";
                case "ChangeEmailText":
                    return "Changer d'Adresse Email";
                case "NewEmailText":
                    return "Nouvelle Adresse Email";
                case "DeleteUserText":
                    return "Effacer mon Compte";
                case "DeleteText":
                    return "Effacer";
                case "DeleteUserInfoText":
                    return
                        "Cela permettra d'éliminer toutes les informations vous concernant dans la grille et retirer votre accès à ce service. Si vous souhaitez continuer, saisissez votre nom et mot de passe et cliquez sur Supprimer.";
                case "EditText":
                    return "Editer";
                case "EditUserAccountText":
                    return "Modifier un compte utilisateur";

                //Maintenance page
                case "WebsiteDownInfoText":
                    return "Le Site Web est actuellement en panne, s'il vous plaît réessayez ultérieurement...";
                case "WebsiteDownText":
                    return "Site Web Hors Ligne";

                //http_404 page
                case "Error404Text":
                    return "Code d'Erreur";
                case "Error404InfoText":
                    return "404 La page n'a pu être trouvée";
                case "HomePage404Text":
                    return "Page d'Accueil";

                //http_505 page
                case "Error505Text":
                    return "Code d'Erreur";
                case "Error505InfoText":
                    return "505 Erreur Interne du Server";
                case "HomePage505Text":
                    return "Page d'Accueil";

                //user_search page
                case "Search":
                    return "Rechercher";
                case "SearchText":
                    return "Rechercher";
                case "SearchForUserText":
                    return "Recherche par Utilisateur";
                case "UserSearchText":
                    return "Rechercher un Utilisateur";
                case "SearchResultForUserText":
                    return "Résultat de la Recherche pour l'Utilisateur";

                //region_search page
                case "SearchForRegionText":
                    return "Rechercher une Région";
                case "RegionSearchText":
                    return "Rechercher une Région";
                case "SearchResultForRegionText":
                    return "Résultat de recherche de la région";

                //Edit user page
                case "AdminDeleteUserText":
                    return "Supprimer l'utilisateur";
                case "AdminDeleteUserInfoText":
                    return "Cette commande supprime le compte et détruit toutes les informations qui lui sont associés.";
                case "BanText":
                    return "Bannir";
                case "UnbanText":
                    return "Débannir";
                case "AdminTempBanUserText":
                    return "Temps de Bannissement";
                case "AdminTempBanUserInfoText":
                    return "Cela empêche l'utilisateur de se connecter pour un laps de temps.";
                case "AdminBanUserText":
                    return "Bannir l'utilisateur";
                case "AdminBanUserInfoText":
                    return "Cela empêche l'utilisateur de se connecter tant que l'interdiction n'est pas supprimée.";
                case "AdminUnbanUserText":
                    return "Débannir un Utilisateur";
                case "AdminUnbanUserInfoText":
                    return "Supprime les interdictions temporaires et permanentes sur l'utilisateur.";
                case "AdminLoginInAsUserText":
                    return "Connectez-vous en tant qu'utilisateur";
                case "AdminLoginInAsUserInfoText":
                    return
                        "Vous serez déconnecté de votre compte admin, et connecté en tant que cet utilisateur, et vous verrez tout comme ils le voient.";
                case "TimeUntilUnbannedText":
                    return "Temps jusqu'à la levée du Bannissement de l'utilisateur";
                case "DaysText":
                    return "Jours";
                case "HoursText":
                    return "Heures";
                case "MinutesText":
                    return "Minutes";
                case "EdittingText":
                    return "Edition";
                case "BannedUntilText":
                    return "L'utilisateur est interdit jusqu'à ce que";
                case "KickAUserText":
                    return "Kicker l'utilisateur (L'utilisateur sera déconnecté dans les 30 secondes)";
                case "KickMessageText":
                    return "Message à l'utilisateur";
                case "KickUserText":
                    return "Kicker l'utilisateur";

                //factory_reset
                case "FactoryReset":
                    return "Réinitialiser";
                case "ResetMenuText":
                    return "Réinitialiser les paramètres par défaut du menu";
                case "ResetSettingsText":
                    return "Rétablir les paramètres Web (page Gestionnaire de paramètres) par défaut";
                case "Reset":
                    return "Réinitialiser";
                case "Settings":
                    return "Paramètres";
                case "Pages":
                    return "Pages";
                case "DefaultsUpdated":
                    return
                        "Mise à jour par défaut, rendez-vous sur \"Réinitialiseré\" ou \"Gestionnaire de paramètres\" pour désactiver cet avertissement.";

                //page_manager
                case "PageManager":
                    return "Gestionnaire de Pages";
                case "SaveMenuItemChanges":
                    return "Enregistrer un élément de menu";
                case "SelectItem":
                    return "Selectionner un élément";
                case "DeleteItem":
                    return "Effacer un élément";
                case "AddItem":
                    return "Ajouter un élément";
                case "PageLocationText":
                    return "Emplacement de la page";
                case "PageIDText":
                    return "ID de la Page";
                case "PagePositionText":
                    return "Position de la Page";
                case "PageTooltipText":
                    return "Tooltip de la Page";
                case "PageTitleText":
                    return "Titre de la Page";
                case "RequiresLoginText":
                    return "Vous devez vous connecter pour voir";
                case "RequiresLogoutText":
                    return "Vous devez vous déconnecter pour voir";
                case "RequiresAdminText":
                    return "Vous devez vous connecter en temps qu'Admin pour voir";

                // grid settings
                case "GridSettingsManager": return "Grille Settings Manager ";
                case "GridnameText": return "Nom de Grille ";
                case "GridnickText": return "Grille surnom ";
                case "WelcomeMessageText": return "Connectez message de bienvenue ";
            case "GovernorNameText": return "Gouverneur du système";
            case "MainlandEstateNameText": return "Succession continentale";
            case "RealEstateOwnerNameText": return "Ownername immobilier du système";
            case "SystemEstateNameText": return "Le nom du système de succession";
            case "BankerNameText": return "Banquier du système";
            case "MarketPlaceOwnerNameText": return "Propriétaire du marché du système";

            //settings manager page
            case "WebRegistrationText":
                    return "Enregistrements Web autorisés";
                case "GridCenterXText":
                    return "Grille Location Centrer X";
                case "GridCenterYText":
                    return "Grille Location Centrer Y";
                case "GoogleMapAPIKeyText":
                    return "Google Maps API Key";
                case "GoogleMapAPIKeyHelpText":
                    return "Générer la Google Maps API KEY v2 ici";
                case "SettingsManager":
                    return "Gestionnaire de paramètres";
                case "IgnorePagesUpdatesText":
                    return "Ignorer les avertissements de mises à jour des pages jusqu'à la prochaine mise à jour";
                case "IgnoreSettingsUpdatesText":
                    return "Ignorer les avertissements de mises à jour des paramètres jusqu'à la prochaine mise à jour";

                // Transactions
                case "TransactionsText": return "Transactions";
                case "DateInfoText": return "Sélectionnez une plage de dates";
                case "DateStartText": return "Commençant date";
                case "DateEndText": return "Date de fin";
                case "30daysPastText": return "30 jours précédents";
                case "TransactionDateText": return "Date";
                case "TransactionDetailText": return "Description";
                case "TransactionAmountText": return "Amount";
                case "TransactionBalanceText": return "Balance";
                case "NoTransactionsText": return "Aucune transaction trouvé...";
                case "PurchasesText": return "Achats";
                case "LoggedIPText": return "Adresse IP enregistrée";
                case "NoPurchasesText": return "Aucun achat trouvés...";
                case "PurchaseCostText": return "Coût";
                
                // Classifieds
                case "ClassifiedsText": return "Annonce Catégorisée";

                // Sim Console
                case "SimConsoleText": return "Sim Command Console";
                case "SimCommandText": return "Command";

                // statistics
                case "StatisticsText": return "Statistiques Viewer";
                case "ViewersText": return "Utilisation Viewer";
                case "GPUText": return "Les cartes graphiques";
                case "PerformanceText": return "Moyennes de performance";
                case "FPSText": return "Images / seconde";
                case "RunTimeText": return "Durée";
                case "RegionsVisitedText": return "Régions visitées";
                case "MemoryUseageText": return "Utilisation de la mémoire";
                case "PingTimeText": return "Ping temps";
                case "AgentsInViewText": return "Agents en vue";
                case "ClearStatsText": return "Effacer les statistiques sur";

                // abuse reports
            case "MenuAbuse": return "Abus Rapports";
            case "TooltipsMenuAbuse": return "Utilisateur abuse journaliste";
            case "AbuseReportText": return "Signaler un abus";
            case "AbuserNameText": return "Abuser";
            case "AbuseReporterNameText": return "Journaliste";
            case "AssignedToText": return "Assigné à";

                //Times
                case "Sun":
                    return "Dim";
                case "Mon":
                    return "Lun";
                case "Tue":
                    return "Mar";
                case "Wed":
                    return "Mer";
                case "Thu":
                    return "Jeu";
                case "Fri":
                    return "Ven";
                case "Sat":
                    return "Sam";
                case "Sunday":
                    return "Dimanche";
                case "Monday":
                    return "Lundi";
                case "Tuesday":
                    return "Mardi";
                case "Wednesday":
                    return "Mercredi";
                case "Thursday":
                    return "Jeudi";
                case "Friday":
                    return "Vendredi";
                case "Saturday":
                    return "Samedi";

                case "Jan_Short":
                    return "Jan";
                case "Feb_Short":
                    return "Fev";
                case "Mar_Short":
                    return "Mar";
                case "Apr_Short":
                    return "Avr";
                case "May_Short":
                    return "Mai";
                case "Jun_Short":
                    return "Jun";
                case "Jul_Short":
                    return "Jui";
                case "Aug_Short":
                    return "Aou";
                case "Sep_Short":
                    return "Sep";
                case "Oct_Short":
                    return "Oct";
                case "Nov_Short":
                    return "Nov";
                case "Dec_Short":
                    return "Dec";

                case "January":
                    return "Janvier";
                case "February":
                    return "Février";
                case "March":
                    return "Mars";
                case "April":
                    return "Avril";
                case "May":
                    return "Mai";
                case "June":
                    return "Juin";
                case "July":
                    return "Juillet";
                case "August":
                    return "Août";
                case "September":
                    return "Septembre";
                case "October":
                    return "Octobre";
                case "November":
                    return "Novembre";
                case "December":
                    return "Decembre";

                // User types
                case "UserTypeText":
                    return "Type d'utilisateur";
                case "AdminUserTypeInfoText":
                    return "Le type d'utilisateur (Actuellement utilisé pour les paiements allocations de formation périodiques).";
                case "Guest":
                    return "Invité";
                case "Resident":
                    return "Résident";
                case "Member":
                    return "Membre";
                case "Contractor":
                    return "Entrepreneur";
                case "Charter_Member":
                    return "Membre de la Charte";

                // ColorBox
                case "ColorBoxImageText":
                    return "Image";
                case "ColorBoxOfText":
                    return "sur";
                case "ColorBoxPreviousText":
                    return "Précédent";
                case "ColorBoxNextText":
                    return "Suivant";
                case "ColorBoxCloseText":
                    return "Fermer";
                case "ColorBoxStartSlideshowText":
                    return "Démarrer Slide Show";
                case "ColorBoxStopSlideshowText":
                    return "Arrêter Slide Show";

                // Maintenance
                case "NoAccountFound":
                    return "Aucun compte trouvé";
                case "DisplayInMenu":
                    return "Affichage dans le menu";
                case "ParentText":
                    return "Menu Parent";
                case "CannotSetParentToChild":
                    return "Vous ne pouvez pas définir de menu comme un enfant à se.";
                case "TopLevel":
                    return "Haut niveau";
                case "HideLanguageBarText":
                    return "Masquer la barre de sélection de la langue";
                case "HideStyleBarText":
                    return "Masquer la barre de sélection de style";
                case "HideSlideshowBarText":
                    return "Masquer diaporama bar";
                case "LocalFrontPageText":
                    return "Page d'accueil locale";
                case "LocalCSSText":
                    return "CSS local feuille de style";

            }
            return "UNKNOWN CHARACTER";
        }
    }
}
