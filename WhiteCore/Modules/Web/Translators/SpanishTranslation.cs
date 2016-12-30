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
        public string LanguageName {
            get { return "es"; }
        }

        public string FullLanguageName {
            get { return "Spanish"; }
        }

        public string GetTranslatedString (string key)
        {
            if (dictionary.ContainsKey (key))
                return dictionary [key];
            return ":" + key + ":";
        }

        Dictionary<string, string> dictionary = new Dictionary<string, string> {
            // Generic
            { "No", "No"},
            { "Yes", "Sí"},
            { "Submit", "Enviar"},
            { "Accept", "Aceptar"},
            { "Save", "Guardar"},
            { "Cancel", "Cancelar"},
            { "FirstText", "Primera"},
            { "BackText", "Volver"},
            { "NextText", "Siguiente"},
            { "LastText", "Última"},
            { "CurrentPageText", "La página actual"},
            { "MoreInfoText", "Más info"},
            { "NoDetailsText", "No se encontraron datos..."},
            { "MoreInfo", "More Information"},
            { "Name", "Nombre"},
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
            { "MaturityText", "Madurez"},
            { "GeneralText", "General"},
            { "MatureText", "Maduro"},
            { "AdultText", "Adulto"},
            { "DateText", "Fecha"},
            { "TimeText", "Hora"},
            { "MinuteText", "minuto"},
            { "MinutesText", "minutos"},
            { "HourText", "hora"},
            { "HoursText", "horas"},
            { "EditText", "Editar"},
            { "EdittingText", "Edición"},

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
            { "RegionPresetTypeText", "Región de preajuste"},
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
            { "AddRegionText", "Añadir Región"},
            { "Mainland", "Continente"},
            { "Estate", "Inmuebles"},
            { "FullRegion", "Región llena"},
            { "Homestead", "Granja"},
            { "Openspace", "Espacio abierto"},
            { "Flatland", "Terreno plano"},
            { "Grassland", "Pradera"},
            { "Island", "Isla"},
            { "Aquatic", "Acuático"},
            { "Custom", "Personalizado"},
            { "RegionPortText", "Región puerto"},
            { "RegionVisibilityText", "Visibles a los vecinos"},
            { "RegionInfiniteText", "Región infinita"},
            { "RegionCapacityText", "la capacidad objeto de región"},
            { "NormalText", "Normal"},
            { "DelayedText", "Retrasado"},

            // Estate management
            {"AddEstateText", "Añadir Raíces"},
            {"EstateText", "Inmuebles"},
            {"EstatesText", "Estates"},
            {"PricePerMeterText", "Precio por metro cuadrado"},
            {"PublicAccessText", "Acceso público"},
            {"AllowVoiceText", "Permitir que la voz"},
            {"TaxFreeText", "Libre de impuestos"},
            {"AllowDirectTeleportText", "Permitir teletransporte directa"},

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
            { "MenuEstateManager", "Administrador de la finca"},
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
            { "TooltipsMenuEstateManager", "Administración de inmuebles"},
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
            { "WelcomeScreen", "Pantalla de bienvenida"},

            // Tooltips Urls
            { "TooltipsWelcomeScreen", "Pantalla de bienvenida"},
            { "TooltipsWorldMap", "Mapa del mundo"},

            // Index
            { "HomeText", "Casa"},
            { "HomeTextWelcome", "Este es nuestro nuevo mundo virtual! Únete a nosotros, y hacer una diferencia!"},
            { "HomeTextTips", "Nuevas presentaciones"},
            { "WelcomeToText", "Bienvenido a"},

            // World Map
            { "WorldMap", "Mapa del mundo"},
            { "WorldMapText", "Pantalla completa"},

            // Chat
            { "ChatText", "Chat de soporte"},

            // Help
            { "HelpText", "Ayudar"},
            { "HelpViewersConfigText", "Configuración del Visor"},

            // Logout
            { "Logout", "Cerrar sesión"},
            { "LoggedOutSuccessfullyText", "Se le ha cerrado la sesión con éxito."},

            // Change user information page
            { "ChangeUserInformationText", "Información de cambio de usuario"},
            { "ChangePasswordText", "Cambia la contraseña"},
            { "NewPasswordText", "Nueva contraseña"},
            { "NewPasswordConfirmationText", "Nueva contraseña (Confirmación)"},
            { "ChangeEmailText", "Cambiar dirección de correo electrónico"},
            { "NewEmailText", "Nueva dirección de correo electrónico"},
            { "DeleteUserText", "Borrar mi cuenta"},
            { "DeleteText", "DeleBorrarte"},
            { "DeleteUserInfoText",
                    "Esto eliminará toda la información acerca de usted en la red y eliminar su acceso a este servicio. Si desea continuar, introduzca su nombre y contraseña y haga clic en Eliminar."},
            { "EditUserAccountText", "Editar cuenta de usuario"},

            // Maintenance
            { "WebsiteDownInfoText", "Página web está actualmente abajo, por favor, inténtelo de nuevo pronto."},
            { "WebsiteDownText", "Sitio web offline"},

            // Http 404
            { "Error404Text", "Código de error"},
            { "Error404InfoText", "404 Página no encontrada"},
            { "HomePage404Text", "página de inicio"},

            // Http 505
            { "Error505Text", "Código de error"},
            { "Error505InfoText", "505 Error de servidor interno"},
            { "HomePage505Text", "página de inicio"},

            // User search
            { "Search", "Buscar"},
            { "SearchText", "Buscar"},
            { "SearchForUserText", "Buscar un usuario"},
            { "UserSearchText", "la búsqueda de usuarios"},
            { "SearchResultForUserText", "Resultados de la búsqueda para los usuarios"},

            // Region search
            { "SearchForRegionText", "Search For A Region"},
            { "RegionSearchText", "Región búsqueda"},
            { "SearchResultForRegionText", "Resultados de la búsqueda por región"},

            // Edit user
            { "AdminDeleteUserText", "Borrar usuario"},
            { "AdminDeleteUserInfoText", "Esto elimina la cuenta y destruye toda la información asociada a ella."},
            { "BanText", "Prohibición"},
            { "UnbanText", "Eliminar la prohibición"},
            { "AdminTempBanUserText", "Usuario temp Ban"},
            { "AdminTempBanUserInfoText", "Esto bloqueó el usuario inicie sesión en la cantidad fija de tiempo."},
            { "AdminBanUserText", "Ban usuario"},
            { "AdminBanUserInfoText", "Esto bloquea el usuario inicie sesión en hasta que se prohibió el usuario."},
            { "AdminUnbanUserText", "Desbloquear el acceso de usuarios"},
            { "AdminUnbanUserInfoText", "Elimina las prohibiciones temporales y permanentes en el usuario."},
            { "AdminLoginInAsUserText", "Entrar como usuario"},
            { "AdminLoginInAsUserInfoText",
                    "Se cerrará la sesión de su cuenta de administrador, y ha iniciado sesión como este usuario, y verá todo como lo ven."},
            { "TimeUntilUnbannedText", "Tiempo hasta que el usuario está prohibida"},
            { "BannedUntilText", "Usuario prohibido hasta"},
            { "KickAUserText", "Expulsar a un usuario (los registra en el plazo de 30 segundos)"},
            { "KickAUserInfoText", "Expulsa a un usuario de la red (los registra en el plazo de 30 segundos)"},
            { "KickMessageText", "Mensaje para el usuario"},
            { "KickUserText", "Tiro de usuario"},
            { "MessageAUserText", "Enviar un mensaje"},
            { "MessageAUserInfoText", "Envía a un usuario un mensaje azul-box (llegará dentro de 30 segundos)"},
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
            { "ClassifiedsText", "Clasificados"},
            { "ClassifiedText", "Clasificado"},
            { "ListedByText", "Anuncio por"},
            { "CreationDateText", "Adicional"},
            { "ExpirationDateText", "Vencimiento" },
            { "DescriptionText", "Descripción" },
            { "PriceOfListingText", "Precio"},

            // Classified categories
            { "CatAll", "Todas"},
            { "CatSelect", ""},
            { "CatShopping", "Compras"},
            { "CatLandRental", "Arrendamiento de tierras"},
            { "CatPropertyRental", "Casa en alquiler"},
            { "CatSpecialAttraction", "Atracción especial"},
            { "CatNewProducts", "Nuevos productos"},
            { "CatEmployment", "Empleo"},
            { "CatWanted", "Querido"},
            { "CatService", "Servicio"},
            { "CatPersonal", "Personal"},
           
            // Events
            { "EventsText", "Eventos"},
            { "EventNameText", "Evento"},
            { "EventLocationText", "Dónde"},
            { "HostedByText","Alojado por"},
            { "EventDateText", "Cuando"},
            { "EventTimeInfoText", "Hora del evento debe ser la hora local"},
            { "CoverChargeText", "Cover"},
            { "DurationText", "Duración"},
            { "AddEventText", "Añadir evento"},

            // Event categories
            { "CatDiscussion", "Discusión"},
            { "CatSports", "Deportes"},
            { "CatLiveMusic", "Música en vivo"},
            { "CatCommercial", "Comercial"},
            { "CatEntertainment", "La vida nocturna/Entretenimiento"},
            { "CatGames", "Juegos/Concursos"},
            { "CatPageants", "los concursos"},
            { "CatEducation", "Educación"},
            { "CatArtsCulture", "Letras/Cultura"},
            { "CatCharitySupport", "Caridad/Grupo de apoyo"},
            { "CatMiscellaneous", "Diverso"},

            // Event lookup periods
            { "Next24Hours", "Próximas 24 horas"},
            { "Next10Hours", "Próximas 10 horas"},
            { "Next4Hours", "Próximas 4 horas"},
            { "Next2Hours", "Próximas 2 horas"},

            // Sim Console
            { "SimConsoleText", "Sim Mando Consola"},
            { "SimCommandText", "Mando"},

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
            { "ResetMenuText", "Menú restablecer los valores de fábrica"},
            { "ResetSettingsText", "Restablecer configuración Web (página Administrador de configuración) Para los valores de fábrica"},
            { "Reset", "Reiniciar"},
            { "Settings", "Ajustes"},
            { "Pages", "Páginas"},
            { "UpdateRequired", "actualización necesaria"},
            { "DefaultsUpdated",
                    "por defecto actualizados, van a valores de fábrica de actualizar o Administrador de configuración para desactivar esta advertencia."},

            // Page_manager
            { "PageManager", "Administrador de la página"},
            { "SaveMenuItemChanges", "Guardar del Menú"},
            { "SelectItem", "Seleccione un artículo"},
            { "DeleteItem", "Eliminar elemento"},
            { "AddItem", "Añadir artículo"},
            { "PageLocationText", "Ubicación de página"},
            { "PageIDText", "Página ID"},
            { "PagePositionText", "Página Posición"},
            { "PageTooltipText", "Página Tooltip"},
            { "PageTitleText", "Página Título"},
            { "RequiresLoginText", "Requiere Login para ver"},
            { "RequiresLogoutText", "Requiere Salir para ver"},
            { "RequiresAdminText", "Requiere de administración para ver"},
            { "RequiresAdminLevelText", "Requiere de administración para ver"},

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

            { "January", "Enero"},
            { "February", "Febrero"},
            { "March", "Marzo"},
            { "April", "Abril"},
            { "May", "Mayo"},
            { "June", "Junio"},
            { "July", "Julio"},
            { "August", "Agosto"},
            { "September", "Septiembre"},
            { "October", "Octubre"},
            { "November", "Noviembre"},
            { "December", "Diciembre"},

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
