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
    public class SpanishTranslation : ITranslator
    {
        public string LanguageName
        {
            get { return "es"; }
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
            { "Yes", "Sí"},
            { "Submit", "Enviar"},
            { "Accept", "Aceptar"},
            { "Save", "Guardar"},
            { "FirstText", "Primera"},
            { "BackText", "Volver"},
            { "NextText", "Siguiente"},
            { "LastText", "Última"},
            { "CurrentPageText", "La página actual"},
            { "MoreInfoText", "Más info"},
            { "NoDetailsText", "No se encontraron datos..."},
            { "MoreInfo", "More Information"},
            { "Name", "Name"},
            { "ObjectNameText", "Objeto"},
            { "LocationText", "Ubicación"},
            { "UUIDText", "UUID"},
            { "DetailsText", "Descripción"},
            { "NotesText", "Notas"},
            { "SaveUpdates", "Guardar cambios"},
            { "ActiveText", "Activo"},
            { "CheckedText", "Comprobado"},
            { "CategoryText", "Categoria"},
            { "SummaryText", "Resumen"},
            { "MaturityText", "Maturity"},
            { "DateText", "Date"},
            { "TimeText", "Time"},
            { "MinuteText", "minute"},
            { "MinutesText", "minutes"},
            { "HourText", "hour"},
            { "HoursText", "hours"},
            { "EdittingText", "Editing"},

            // Status information
            { "GridStatus", "Estato de la Grid"},
            { "Online", "En Linea"},
            { "Offline", "Offline"},
            { "TotalUserCount", "total de Usuarios"},
            { "TotalRegionCount", "Cuenta Total Región"},
            { "UniqueVisitors", "Visitantes únicos últimos 30 días"},
            { "OnlineNow", "En línea ahora"},
            { "InterWorld", "Inter World (IWC)"},
            { "HyperGrid", "HyperGrid (HG)"},
            { "Voice", "Voz"},
            { "Currency", "Moneda"},
            { "Disabled", "Discapacitado"},
            { "Enabled", "Habilitado"},
            { "News", "Nuevas"},
            { "Region", "Región"},

            // User login
            { "Login", "Iniciar sesión"},
            { "UserName", "Nombre de usuario"},
            { "UserNameText", "Nombre de usuario"},
            { "Password", "Contraseña"},
            { "PasswordText", "Contraseña"},
            { "PasswordConfirmation", "Password Confirmation"},
            { "ForgotPassword", "¿Olvidó su contraseña?"},
            { "TypeUserNameToConfirm", "Please type the username of this account to confirm you want to delete this account"},

            // Special windows
            { "SpecialWindowTitleText", "Special Info Window Title"},
            { "SpecialWindowTextText", "Special Info Window Text"},
            { "SpecialWindowColorText", "Special Info Window Color"},
            { "SpecialWindowStatusText", "Special Info Window Status"},
            { "WelcomeScreenManagerFor", "Welcome Screen Manager For"},
            { "ChangesSavedSuccessfully", "Changes Saved Successfully"},

            // User registration
            { "AvatarNameText", "Avatar Name"},
            { "AvatarScopeText", "Avatar Scope ID"},
            { "FirstNameText", "Your First Name"},
            { "LastNameText", "Your Last Name"},
            { "UserAddressText", "Your Address"},
            { "UserZipText", "Your Zip Code"},
            { "UserCityText", "Your City"},
            { "UserCountryText", "Your Country"},
            { "UserDOBText", "Your Date Of Birth (Month Day Year)"},
            { "UserEmailText", "Your Email"},
            { "UserHomeRegionText", "Región Home"},
            { "RegistrationText", "Avatar registration"},
            { "RegistrationsDisabled", "Registrations are currently disabled, please try again soon."},
            { "TermsOfServiceText", "Terms of Service"},
            { "TermsOfServiceAccept", "Do you accept the Terms of Service as detailed above?"},
            { "AvatarNameError", "No ha introducido el nombre de avatar!"},
            { "AvatarPasswordError", "Contraseña está vacío o que no coincida con!"},
            { "AvatarEmailError", "Se requiere una dirección de correo electrónico para recuperar la contraseña! ('none' si no se conoce)"},
            { "AvatarNameSpacingError", "Su nombre avatar debe ser 'Nombre Apellido'!"},

            // News
            { "OpenNewsManager", "Open the news manager"},
            { "NewsManager", "News Manager"},
            { "EditNewsItem", "Edit news item"},
            { "AddNewsItem", "Add new news item"},
            { "DeleteNewsItem", "Delete news item"},
            { "NewsDateText", "News Date"},
            { "NewsTitleText", "News Title"},
            { "NewsItemTitle", "News Item Title"},
            { "NewsItemText", "News Item Text"},
            { "AddNewsText", "Add News"},
            { "DeleteNewsText", "Delete News"},
            { "EditNewsText", "Edit News"},

            // User Profile
            { "UserProfileFor", "User Profile For"},
            { "UsersGroupsText", "Grupos unidos"},
            { "GroupNameText", "Grupo"},
            { "UsersPicksText", "Selecciones para"},
            { "ResidentSince", "Resident Since"},
            { "AccountType", "Account Type"},
            { "PartnersName", "Partner's Name"},
            { "AboutMe", "About Me"},
            { "IsOnlineText", "User Status"},
            { "OnlineLocationText", "User Location"},
            { "Partner", "Partner"},
            { "Friends", "Friends"},
            { "Nothing", "None"},
            { "ChangePass", "Change password"},
            { "NoChangePass", "Unable to change the password, please try again later"},

            // Region Information
            { "RegionInformationText", "Region Information"},
            { "OwnerNameText", "Owner Name"},
            { "RegionLocationText", "Region Location"},
            { "RegionSizeText", "Region Size"},
            { "RegionNameText", "Region Name"},
            { "RegionTypeText", "Region Type"},
            { "RegionDelayStartupText", "Delay starting scripts"},
            { "RegionPresetText", "Region Preset"},
            { "RegionTerrainText", "Región Terreno"},
            { "ParcelsInRegionText", "Parcels In Region"},
            { "ParcelNameText", "Parcel Name"},
            { "ParcelOwnerText", "Parcel Owner's Name"},

            // Region List
            { "RegionInfoText", "Region Info"},
            { "RegionListText", "Region List"},
            { "RegionLocXText", "Region X"},
            { "RegionLocYText", "Region Y"},
            { "SortByLocX", "Sort By Region X"},
            { "SortByLocY", "Sort By Region Y"},
            { "SortByName", "Sort By Region Name"},
            { "RegionMoreInfo", "More Information"},
            { "RegionMoreInfoTooltips", "More info about"},
            { "OnlineUsersText", "Online Users"},
            { "OnlineFriendsText", "Online Friends"},
            { "RegionOnlineText", "Region Status"},
            { "RegionMaturityText", "Access Rating"},
            { "NumberOfUsersInRegionText", "Number of Users in region"},

            // Region manager
            { "Mainland", "Mainland"},
            { "Estate", "Estate"},
            { "FullRegion", "Full Region"},
            { "Homestead", "Homestead"},
            { "Openspace", "Openspace"},
            { "Flatland", "Flatland"},
            { "Grassland", "Grassland"},
            { "Island", "Island"},
            { "Aquatic", "Aquatic"},
            { "Custom", "Custom"},
            { "RegionPortText", "Region Port"},
            { "RegionVisibilityText", "Visible to neighbours"},
            { "RegionInfiniteText", "Infinite Region"},
            { "RegionCapacityText", "Region object capacity"},

            // Menus
            { "MenuHome", "Inicio"},
            { "MenuLogin", "Iniciar sesión"},
            { "MenuLogout", "Salir"},
            { "MenuRegister", "Registro"},
            { "MenuForgotPass", "Olvidó su contraseña?"},
            { "MenuNews", "Noticias"},
            { "MenuWorld", "Mundial"},
            { "MenuWorldMap", "Mapa del Mundo"},
            { "MenuRegion", "lista Región"},
            { "MenuUser", "User"},
            { "MenuOnlineUsers", "Online Users"},
            { "MenuUserSearch", "User Search"},
            { "MenuRegionSearch", "Region Search"},
            { "MenuChat", "Chat"},
            { "MenuHelp", "Help"},
            { "MenuViewerHelp", "Viewer Help"},
            { "MenuChangeUserInformation", "Change User Information"},
            { "MenuWelcomeScreenManager", "Welcome Screen Manager"},
            { "MenuNewsManager", "News Manager"},
            { "MenuUserManager", "User Manager"},
            { "MenuFactoryReset", "Factory Reset"},
            { "ResetMenuInfoText", "Resets the menu items back to the most updated defaults"},
            { "ResetSettingsInfoText", "Resets the Web Interface settings back to the most updated defaults"},
            { "MenuPageManager", "Page Manager"},
            { "MenuSettingsManager", "Settings Manager"},
            { "MenuManager", "Manejo"},
            { "MenuSettings", "Ajustes"},
            { "MenuRegionManager", "Gerente de Región"},
            { "MenuManagerSimConsole", "Simulador de consola"},
            { "MenuPurchases", "Usuario compra"},
            { "MenuMyPurchases", "Mis Compras"},
            { "MenuTransactions", "Transacciones de usuario"},
            { "MenuMyTransactions", "Mis Transacciones"},
            { "MenuClassifieds", "Classifieds"},
            { "MenuMyClassifieds", "Mis Classifieds <NT>"},
            { "MenuEvents", "Events"},
            { "MenuMyEvents", "My Events"},
            { "MenuStatistics", "Visor de estadísticas"},
            { "MenuGridSettings", "Configuración de la cuadrícula"},

            // Menu Tooltips
            { "TooltipsMenuHome", "Inicio"},
            { "TooltipsMenuLogin", "Iniciar sesión"},
            { "TooltipsMenuLogout", "Salir"},
            { "TooltipsMenuRegister", "Registro"},
            { "TooltipsMenuForgotPass", "Olvidó su contraseña?"},
            { "TooltipsMenuNews", "Noticias"},
            { "TooltipsMenuWorld", "Mundial"},
            { "TooltipsMenuWorldMap", "Mapa del Mundo"},
            { "TooltipsMenuUser", "User"},
            { "TooltipsMenuOnlineUsers", "Online Users"},
            { "TooltipsMenuUserSearch", "User Search"},
            { "TooltipsMenuRegionSearch", "Region Search"},
            { "TooltipsMenuChat", "Chat"},
            { "TooltipsMenuViewerHelp", "Viewer Help"},
            { "TooltipsMenuHelp", "Help"},
            { "TooltipsMenuChangeUserInformation", "Change User Information"},
            { "TooltipsMenuWelcomeScreenManager", "Welcome Screen Manager"},
            { "TooltipsMenuNewsManager", "News Manager"},
            { "TooltipsMenuUserManager", "User Manager"},
            { "TooltipsMenuFactoryReset", "Factory Reset"},
            { "TooltipsMenuPageManager", "Page Manager"},
            { "TooltipsMenuSettingsManager", "Settings Manager"},
            { "TooltipsMenuManager", "Admin Management"},
            { "TooltipsMenuSettings", "WebUI Ajustes"},
            { "TooltipsMenuRegionManager", "Región crear / editar"},
            { "TooltipsMenuManagerSimConsole", "Consola Simulador Online"},
            { "TooltipsMenuPurchases", "Información Realizar compra"},
            { "TooltipsMenuTransactions", "La información de transacciones"},
            { "TooltipsMenuClassifieds", "Classifieds information"},
            { "TooltipsMenuEvents", "Event information"},
            { "TooltipsMenuStatistics", "Visor de estadísticas"},
            { "TooltipsMenuGridSettings", "Configuración de la cuadrícula"},

            // Menu Region box
            { "MenuRegionTitle", "Región"},
            { "MenuParcelTitle", "Parcel"},
            { "MenuOwnerTitle", "Owner"},
            { "TooltipsMenuRegion", "Detalles Región"},
            { "TooltipsMenuParcel", "Parcelas Región"},
            { "TooltipsMenuOwner", "Propietario Estate"},

            // Menu Profile box
            { "MenuProfileTitle", "Profil"},
            { "MenuGroupTitle", "Group"},
            { "MenuPicksTitle", "Picks"},
            { "MenuRegionsTitle", "Regiones"},
            { "TooltipsMenuProfile", "Perfil del usuario"},
            { "TooltipsMenuGroups", "Grupos de usuarios"},
            { "TooltipsMenuPicks", "Selecciones de usuario"},
            { "TooltipsMenuRegions", "Regiones usuario"},
            { "UserGroupNameText", "Grupo de usuarios"},
            { "PickNameText", "Escoja el nombre"},
            { "PickRegionText", "Ubicación"},

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
            { "HelpText", "Ayudar"},
            { "HelpViewersConfigText", "Configuración del Visor"},

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
            { "KickAUserText", "Kick A User (Logs them out within 30 seconds)"},
            { "KickAUserInfoText", "Kicks a user from the grid (logs them out within 30 seconds)"},
            { "KickMessageText", "Message To User"},
            { "KickUserText", "Kick User"},
            { "MessageAUserText", "Send User A Message"},
            { "MessageAUserInfoText", "Sends a user a blue-box message (will arrive within 30 seconds)"},
            { "MessageUserText", "Message User"},

            // Transactions
            { "TransactionsText", "Transacciones"},
            { "DateInfoText", "Seleccione un rango de fechas"},
            { "DateStartText", "Fecha comenzando"},
            { "DateEndText", "Fecha de clausura"},
            { "30daysPastText", "Últimos 30 días"},
            { "TransactionToAgentText", "To User"},
            { "TransactionFromAgentText", "From User"},
            { "TransactionDateText", "Fecha"},
            { "TransactionDetailText", "Descripción"},
            { "TransactionAmountText", "Importe"},
            { "TransactionBalanceText", "Saldo"},
            { "NoTransactionsText", "No hay transacciones encontraron..."},
            { "PurchasesText", "Las compras"},
            { "LoggedIPText", "Dirección IP registrada"},
            { "NoPurchasesText", "No se encontraron compras..."},
            { "PurchaseCostText", "Costo"},
            
            // Classifieds
            { "ClassifiedsText", "Anuncio Breve"},
            { "ClassifiedText", "Classified"},
            { "ListedByText", " Listed by"},
            { "CreationDateText", "Added"},
            { "ExpirationDateText", "Expiration" },
            { "DescriptionText", "Description" },
            { "PriceOfListingText", "Price"},

            // Classified categories
            { "CatAll", "All"},
            { "CatSelect", ""},
            { "CatShopping", "Shopping"},
            { "CatLandRental", "Land Rental"},
            { "CatPropertyRental", "Property Rental"},
            { "CatSpecialAttraction", "Special Attraction"},
            { "CatNewProducts", "New Products"},
            { "CatEmployment", "Employment"},
            { "CatWanted", "Wanted"},
            { "CatService", "Service"},
            { "CatPersonal", "Personal"},
           
            // Events
            { "EventsText", "Events"},
            { "EventNameText", "Event"},
            { "EventLocationText", "Where"},
            { "HostedByText","Hosted by"},
            { "EventDateText", "When"},
            { "EventTimeInfoText", "Event time should be local time (Server)"},
            { "CoverChargeText", "Cover charge"},
            { "DurationText", "Duration"},
            { "AddEventText", "Add event"},

            // Event categories
            { "CatDiscussion", "Discussion"},
            { "CatSports", "Sports"},
            { "CatLiveMusic", "Live Music"},
            { "CatCommercial", "Commercial"},
            { "CatEntertainment", "Nightlife/Entertainment"},
            { "CatGames", "Games/Contests"},
            { "CatPageants", "Pageants"},
            { "CatEducation", "Education"},
            { "CatArtsCulture", "Arts/Culture"},
            { "CatCharitySupport", "Charity/Support Group"},
            { "CatMiscellaneous", "Miscellaneous"},

            // Event lookup periods
            { "Next24Hours", "Next 24 hours"},
            { "Next10Hours", "Next 10 hours"},
            { "Next4Hours", "Next 4 hours"},
            { "Next2Hours", "Next 2 hours"},

            // Sim Console
            { "SimConsoleText", "Sim Command Console"},
            { "SimCommandText", "Command"},

            // Statistics
            { "StatisticsText", "Visor de estadísticas"},
            { "ViewersText", "Uso del Visor"},
            { "GPUText", "Tarjetas gráficas"},
            { "PerformanceText", "Promedios de rendimiento "},
            { "FPSText", "Cuadros / segundo"},
            { "RunTimeText", "El tiempo de ejecución"},
            { "RegionsVisitedText", "Regiones visitadas"},
            { "MemoryUseageText", "Uso de la memoria"},
            { "PingTimeText", "Tiempo Ping"},
            { "AgentsInViewText", "Agentes de vista"},
            { "ClearStatsText", "Borrar datos estadísticas"},

            // Abuse reports
            { "MenuAbuse", "Abuso Informes"},
            { "TooltipsMenuAbuse", "Usuario abuso informes"},
            { "AbuseReportText", "Reportar abuso"},
            { "AbuserNameText", "Abusador"},
            { "AbuseReporterNameText", "Reportero"},
            { "AssignedToText", "Asignado a"},
                
            // Factory reset
            { "FactoryReset", "Factory Reset"},
            { "ResetMenuText", "Reset Menu To Factory Defaults"},
            { "ResetSettingsText", "Reset Web Settings (Settings Manager page) To Factory Defaults"},
            { "Reset", "Reset"},
            { "Settings", "Settings"},
            { "Pages", "Pages"},
            { "DefaultsUpdated",
                    "defaults updated, go to Factory Reset to update or Settings Manager to disable this warning."},

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
            { "RequiresAdminLevelText", "Requires Admin To View"},

            // Grid settings
            { "GridSettingsManager", "Rejilla Settings Manager "},
            { "GridnameText", "Nombre de cuadrícula "},
            { "GridnickText", "Apodo Cuadrícula "},
            { "WelcomeMessageText", "Ingresa mensajes de bienvenida "},
            { "GovernorNameText", "Gobernador del sistema"},
            { "MainlandEstateNameText", "Raíces continental"},
            { "RealEstateOwnerNameText", "Sistema de nombre de propietario de la finca"},
            { "SystemEstateNameText", "Nombre raíces Sistema"},
            { "BankerNameText", "Banquero sistema"},
            { "MarketPlaceOwnerNameText", "Propietario del sistema de mercado"},

            // Settings manager
            { "WebRegistrationText", "Registros Web permitidas"},
            { "GridCenterXText", "Grid Center Location X"},
            { "GridCenterYText", "Grid Center Location Y"},
            { "SettingsManager", "Settings Manager"},
            { "IgnorePagesUpdatesText", "Ignore pages update warning until next update"},
            { "IgnoreSettingsUpdatesText", "Ignore settings update warning until next update"},
            { "HideLanguageBarText", "Barra de selección de idioma Ocultar"},
            { "HideStyleBarText", "Ocultar barra de selección de estilo"},
            { "HideSlideshowBarText", "Bar diapositivas Ocultar"},
            { "LocalFrontPageText", "Primera página Local"},
            { "LocalCSSText", "Hoja de estilos CSS Local"},

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
            { "UserTypeText", "Tipo de usuario"},
            { "AdminUserTypeInfoText", "El tipo de usuario (en la actualidad se utiliza para el pago de estipendios periódicos)."},
            { "Guest", "Invitado"},
            { "Resident", "Residente"},
            { "Member", "Miembro"},
            { "Contractor", "Contratista"},
            { "Charter_Member", "Miembro fundador"},

            // ColorBox
            { "ColorBoxImageText", "Image"},
            { "ColorBoxOfText", "of"},
            { "ColorBoxPreviousText", "Previous"},
            { "ColorBoxNextText", "Next"},
            { "ColorBoxCloseText", "Close"},
            { "ColorBoxStartSlideshowText", "Start Slide Show"},
            { "ColorBoxStopSlideshowText", "Stop Slide Show"},

            // Maintenance
            { "NoAccountFound", "No se han encontrado cuenta"},
            { "DisplayInMenu", "Pantalla en el menú"},
            { "ParentText", "Menú principalt"},
            { "CannotSetParentToChild", "No se puede establecer elemento de menú como un niño a sí mismo."},
            { "TopLevel", "Nivel superior"},

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
            { "fr", "Français"},
            { "de", "Deutsch"},
            { "it", "Italiano"},
            { "es", "Español"},
            { "nl", "Nederlands"},
            { "ru", "Русский"}

        };
    }
}
