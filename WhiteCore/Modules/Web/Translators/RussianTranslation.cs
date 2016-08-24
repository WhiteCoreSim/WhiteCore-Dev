/*
 * Copyright (c) Contributors, http//whitecore-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Virtual Universe Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES},
 * LOSS OF USE, DATA, OR PROFITS}, OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;

namespace WhiteCore.Modules.Web.Translators
{
    public class RussianTranslation : ITranslator
    {
        public string LanguageName {
            get { return "ru"; }
        }


        public string GetTranslatedString (string key)
        {
            if (dictionary.ContainsKey (key))
                return dictionary [key];
            return ":" + key + ":";
        }

        readonly Dictionary<string, string> dictionary = new Dictionary<string, string> {
            // general
            { "No", "Нет"},
            { "Yes", "Да"},
            { "Submit", "Отправить"},
            { "Accept", "Nринимать"},
            { "Save", "Сохранить"},
            { "FirstText", "Nервый"},
            { "BackText", "Назад"},
            { "NextText", "Следующий"},
            { "LastText", "Nоследний"},
            { "CurrentPageText", "Current Page"},
            { "MoreInfoText", "Больше информации"},
            { "NoDetailsText", "Никаких подробностей не найдено..."},
            { "MoreInfo", "More Information"},
            { "Name", "имя"},
            { "ObjectNameText", "Oбъект"},
            { "LocationText", "Регион"},
            { "UUIDText", "UUID"},
            { "DetailsText", "Описание"},
            { "NotesText", "Заметки"},
            { "SaveUpdates", "Сохранить"},
            { "ActiveText", "Aктивный"},
            { "CheckedText", "Рассмотренные"},
            { "CategoryText", "Категория"},
            { "SummaryText", "Резюме"},
            { "MaturityText", "Maturity"},
            { "DateText", "Date"},
            { "TimeText", "Time"},
            { "MinuteText", "minute"},
            { "MinutesText", "minutes"},
            { "HourText", "hour"},
            { "HoursText", "hours"},
            { "EdittingText", "Editing"},

            // Status information
            { "GridStatus", "Статус"},
            { "Online", "В 3D Мире"},
            { "Offline", "Нет в Сети"},
            { "TotalUserCount", "Пользователей"},
            { "TotalRegionCount", "Total Region Count"},
            { "UniqueVisitors", "За последние 30 дней"},
            { "OnlineNow", "В игре"},
            { "InterWorld", "Inter World (IWC)"},
            { "HyperGrid", "HyperGrid (HG)"},
            { "Voice", "Голосовой"},
            { "Currency", "Currency"},
            { "Disabled", "Disabled"},
            { "Enabled", "Enabled"},
            { "News", "Новости"},
            { "Region", "Регион"},

            // User login
            { "Login", "Вход"},
            { "UserName", "Имя пользователя"},
            { "UserNameText", "Имя пользователя"},
            { "Password", "Пароль"},
            { "PasswordText", "Пароль"},
            { "PasswordConfirmation", "Password Confirmation"},
            { "ForgotPassword", "Забыли пароль?"},
            { "TypeUserNameToConfirm", "Пожалуйста, введите имя пользователя этой учетной записи, чтобы подтвердить, что вы хотите удалить эту учетную запись"},

            // Special windows
            { "SpecialWindowTitleText", "Special Info Window Title"},
            { "SpecialWindowTextText", "Special Info Window Text"},
            { "SpecialWindowColorText", "Special Info Window Color"},
            { "SpecialWindowStatusText", "Special Info Window Status"},
            { "WelcomeScreenManagerFor", "Welcome Screen Manager For"},
            { "ChangesSavedSuccessfully", "Changes Saved Successfully"},

            // User registration
            { "AvatarNameText", "Имя пользователя"},
            { "AvatarScopeText", "Avatar Scope ID"},
            { "FirstNameText", "Имя"},
            { "LastNameText", "Фамилия"},
            { "UserAddressText", "Адрес"},
            { "UserZipText", "Почтовый Индекс"},
            { "UserCityText", "Город"},
            { "UserCountryText", "Страна"},
            { "UserDOBText", "Дата рождения"},
            { "UserEmailText", "Email"},
            { "UserHomeRegionText", "Выберите Регион"},
            { "RegistrationText", "Регистрация"},
            { "RegistrationsDisabled", "Регистрация в данный момент отключена, пожалуйста, попробуйте еще раз в ближайшее время."},
            { "TermsOfServiceText", "Условия Пользования"},
            { "TermsOfServiceAccept", "Вы принимаете условия пользования, как описано выше?"},
            { "AvatarNameError", "Вы не ввели имя аватара!"},
            { "AvatarPasswordError", "Поле пароля пустое или не совпадает!"},
            { "AvatarEmailError", "Адрес электронной почты необходим для восстановления пароля!"},
            { "AvatarNameSpacingError", "Имя аватар должно быть \"Имя Фамилия\"!"},

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
            { "UserProfileFor", "Профиль пользователя"},
            { "UsersGroupsText", "Мои группы"},
            { "GroupNameText", "Группы"},
            { "UsersPicksText", "Места"},
            { "ResidentSince", "Дата регистрации"},
            { "AccountType", "Должность"},
            { "PartnersName", "Партнёр"},
            { "AboutMe", "Обо мне"},
            { "IsOnlineText", "Статус"},
            { "OnlineLocationText", "Локация"},
            { "Partner", "Нет партнёра"},
            { "Friends", "Friends"},
            { "Nothing", "Нет информации"},
            { "ChangePass", "Ваш пароль изменен"},
            { "NoChangePass", "Не удалось изменить пароль, попробуйте еще раз позже"},

            // Region Information
            { "RegionInformationText", "Информация об регионе"},
            { "OwnerNameText", "Владелец"},
            { "RegionLocationText", "Координаты"},
            { "RegionSizeText", "Размер"},
            { "RegionNameText", "Название"},
            { "RegionTypeText", "Region Type"},
            { "RegionDelayStartupText", "Delay starting scripts"},
            { "RegionPresetText", "Region Preset"},
            { "RegionTerrainText", "Region Terrain"},
            { "ParcelsInRegionText", "Parcels In Region"},
            { "ParcelNameText", "Parcel Name"},
            { "ParcelOwnerText", "Parcel Owner's Name"},

            // Region List
            { "RegionInfoText", "Информация об регионе"},
            { "RegionListText", "Список регионов"},
            { "RegionLocXText", "Позиция X"},
            { "RegionLocYText", "Позиция Y"},
            { "SortByLocX", "Сортировать по позиции X"},
            { "SortByLocY", "Сортировать по позиции Y"},
            { "SortByName", "Сортировать по имени региона"},
            { "RegionMoreInfo", "Больще информации"},
            { "RegionMoreInfoTooltips", "Подробнее о"},
            { "OnlineUsersText", "Пользователи"},
            { "OnlineFriendsText", "Online Friends"},
            { "RegionOnlineText", "Статус"},
            { "RegionMaturityText", "Открыть Рейтинг"},
            { "NumberOfUsersInRegionText", "Количество пользователей в регионе"},

            // Region manager
            { "Mainland", "Материк"},
            { "Estate", "Имущество"},
            { "FullRegion", "Полный Регион"},
            { "Homestead", "Homestead"},
            { "Openspace", "Космос"},
            { "Flatland", "Равнина"},
            { "Grassland", "Поле"},
            { "Island", "Остров"},
            { "Aquatic", "Океан"},
            { "Custom", "На заказ"},
            { "RegionPortText", "Порт"},
            { "RegionVisibilityText", "Видимый для соседей"},
            { "RegionInfiniteText", "Бесконечная область"},
            { "RegionCapacityText", "Region object capacity"},

            // Menus
            { "MenuHome", "Домой"},
            { "MenuLogin", "Вход"},
            { "MenuLogout", "Выход"},
            { "MenuRegister", "Регистрация"},
            { "MenuForgotPass", "Забыли пароль"},
            { "MenuNews", "Новости"},
            { "MenuWorld", "Мир"},
            { "MenuWorldMap", "Карта мира"},
            { "MenuRegion", "Список локаций"},
            { "MenuUser", "Пользователи"},
            { "MenuOnlineUsers", "Онлайн пользователи"},
            { "MenuUserSearch", "Поиск"},
            { "MenuRegionSearch", "Поиск"},
            { "MenuChat", "Чат"},
            { "MenuHelp", "Помощь"},
            { "MenuViewerHelp", "Скачать"},
            { "MenuChangeUserInformation", "Change User Information"},
            { "MenuWelcomeScreenManager", "Welcome Screen Manager"},
            { "MenuNewsManager", "Новости"},
            { "MenuUserManager", "Пользователи"},
            { "MenuFactoryReset", "Сброс"},
            { "ResetMenuInfoText", "Resets the menu items back to the most updated defaults"},
            { "ResetSettingsInfoText", "Resets the Web Interface settings back to the most updated defaults"},
            { "MenuPageManager", "Страницы"},
            { "MenuSettingsManager", "Настройки сайта"},
            { "MenuManager", "Management"},
            { "MenuSettings", "Настройки"},
            { "MenuRegionManager", "Region Manager"},
            { "MenuManagerSimConsole", "Simulator console"},
            { "MenuPurchases", "User Purchases"},
            { "MenuMyPurchases", "My Purchases"},
            { "MenuTransactions", "User Transactions"},
            { "MenuMyTransactions", "Трансакции"},
            { "MenuClassifieds", "Classifieds"},
            { "MenuMyClassifieds", "My Classifieds"},
            { "MenuEvents", "Events"},
            { "MenuMyEvents", "My Events"},
            { "MenuStatistics", "Клиенты"},
            { "MenuGridSettings", "Сервер"},

            // Menu Tooltips
            { "TooltipsMenuHome", "Home"},
            { "TooltipsMenuLogin", "Login"},
            { "TooltipsMenuLogout", "Logout"},
            { "TooltipsMenuRegister", "Register"},
            { "TooltipsMenuForgotPass", "Forgot Password"},
            { "TooltipsMenuNews", "News"},
            { "TooltipsMenuWorld", "World"},
            { "TooltipsMenuWorldMap", "World Map"},
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
            { "TooltipsMenuSettings", "WebUI Settings"},
            { "TooltipsMenuRegionManager", "Region create/edit"},
            { "TooltipsMenuManagerSimConsole", "Online simulator console"},
            { "TooltipsMenuPurchases", "Purchase information"},
            { "TooltipsMenuTransactions", "Transaction information"},
            { "TooltipsMenuClassifieds", "Classifieds information"},
            { "TooltipsMenuEvents", "Event information"},
            { "TooltipsMenuStatistics", "Viewer Statistics"},
            { "TooltipsMenuGridSettings", "Grid settings"},

            // Menu Region box
            { "MenuRegionTitle", "Регион"},
            { "MenuParcelTitle", "Parcels"},
            { "MenuOwnerTitle", "Владелец"},
            { "TooltipsMenuRegion", "Region Details"},
            { "TooltipsMenuParcel", "Region Parcels"},
            { "TooltipsMenuOwner", "Estate Owner"},

            // Menu Profile box
            { "MenuProfileTitle", "Profile"},
            { "MenuGroupTitle", "Groups"},
            { "MenuPicksTitle", "Picks"},
            { "MenuRegionsTitle", "Regions"},
            { "TooltipsMenuProfile", "User Profile"},
            { "TooltipsMenuGroups", "User Groups"},
            { "TooltipsMenuPicks", "User Selections"},
            { "TooltipsMenuRegions", "User Regions"},
            { "UserGroupNameText", "User group"},
            { "PickNameText", "Pick name"},
            { "PickRegionText", "Location"},

            // Urls
            { "WelcomeScreen", "Welcome Screen"},

            // Tooltips Urls
            { "TooltipsWelcomeScreen", "Welcome Screen"},
            { "TooltipsWorldMap", "World Map"},

            // Index
            { "HomeText", "Главная"},
            { "HomeTextWelcome", "This is our New Virtual World! Join us now, and make a difference!"},
            { "HomeTextTips", "New presentations"},
            { "WelcomeToText", "Welcome to"},

            // World Map
            { "WorldMap", "Карта мира"},
            { "WorldMapText", "На весь экран"},

            // Chat Page
            { "ChatText", "Chat Support"},

            // Help Page
            { "HelpText", "Скачать"},
            { "HelpViewersConfigText", "Viewer Configuration"},
            /*{ "AlchemyViewer", "Alchemy Viewer"},
            { "AngstromViewer", "Angstrom Viewer"},
            { "AstraViewer", "Astra Viewer"},
            { "FirestormViewer", "Firestorm Viewer"},
            { "KokuaViewer", "Kokua Viewer"},
            { "ImprudenceViewer", "Imprudence Viewer"},
            { "PhoenixViewer", "Phoenix Viewer"},
            { "SingularityViewer", "Singularity Viewer"},
            { "VoodooViewer", "Voodoo Viewer"},
            { "ZenViewer", "Zen Viewer"},*/

            // Logout
            { "Logout", "Выход"},
            { "LoggedOutSuccessfullyText", "Вы успешно вышли из системы."},

            // Change user information
            { "ChangeUserInformationText", "Настройки"},
            { "ChangePasswordText", "Изменить пароль"},
            { "NewPasswordText", "Новый пароль"},
            { "NewPasswordConfirmationText", "Повторите пароль"},
            { "ChangeEmailText", "Изменть Email"},
            { "NewEmailText", "Новый Email адрес"},
            { "DeleteUserText", "Удалить аккаунт"},
            { "DeleteText", "Удалить"},
            { "DeleteUserInfoText",
               "Вы уверены, что хотите удалить свой аккаунт в ReaL Life 3D без возможности восстановления. Если вы хотите продолжить, введите свое имя и пароль и нажмите кнопку Удалить."},
            { "EditText", "Редактировать"},
            { "EditUserAccountText", "Редактировать аккаунт пользователя"},

            // Maintenance
            { "WebsiteDownInfoText", "Сайт на данный момент не работает, пожалуйста, попробуйте еще раз в ближайшее время."},
            { "WebsiteDownText", "Сайт отключен на технические работы"},

            // Http 404
            { "Error404Text", "Ошибка"},
            { "Error404InfoText", "404 страница не существует"},
            { "HomePage404Text", "Главная страница"},

            // Http 505
            { "Error505Text", "Ошибка"},
            { "Error505InfoText", "505 Внутренняя Ошибка Сервера"},
            { "HomePage505Text", "Главная страница"},

            // User search
            { "Search", "Поиск"},
            { "SearchText", "Поиск"},
            { "SearchForUserText", "Поиск пользователя"},
            { "UserSearchText", "Поиск пользователя"},
            { "SearchResultForUserText", "Результат поиска"},

            // Region search
            { "SearchForRegionText", "Поиск Региона"},
            { "RegionSearchText", "Поиск Региона"},
            { "SearchResultForRegionText", "Результат поиска"},

            // Edit user
            { "AdminDeleteUserText", "Удалить пользователя"},
            { "AdminDeleteUserInfoText", "Удалить аккаунт и всю связанную с ним информацию."},
            { "BanText", "Заблокировать"},
            { "UnbanText", "Разблокировать"},
            { "AdminTempBanUserText", "Бан на время"},
            { "AdminTempBanUserInfoText", "Блокирует вход пользователя в течение заданного времени."},
            { "AdminBanUserText", "Бан навсегда"},
            { "AdminBanUserInfoText", "Блокирует вход пользователя в навсегда, пока он не будет снят Сотрудниками."},
            { "AdminUnbanUserText", "Разблокировать"},
            { "AdminUnbanUserInfoText", "Removes temporary and permanent bans on the user."},
            { "AdminLoginInAsUserText", "Вход пользователем в 3D мир"},
            { "AdminLoginInAsUserInfoText",
                "Вы выйдете из вашей учетной записи Сотрудника и войдете в систему как этот пользователь."},
            { "BannedUntilText", "User banned until"},
            { "KickAUserText", "Выкинуть"},
            { "KickAUserInfoText", "Выкинуть пользователя с 3D мира (срабатывает в течение 30 секунд)"},
            { "KickMessageText", "Причина отключения"},
            { "KickUserText", "Отключение от 3D мира"},
            { "MessageAUserText", "Отправить сообщение"},
            { "MessageAUserInfoText", "Отправляет сообщение пользователю"},
            { "MessageUserText", "Отправить"},

            // Transactions
            { "TransactionsText", "Transactions"},
            { "DateInfoText", "Select a date range"},
            { "DateStartText", "Commencing Date"},
            { "DateEndText", "Ending Date"},
            { "30daysPastText", "Previous 30 days"},
            { "TransactionToAgentText", "To User"},
            { "TransactionFromAgentText", "From User"},
            { "TransactionDateText", "Date"},
            { "TransactionDetailText", "Description"},
            { "TransactionAmountText", "Amount"},
            { "TransactionBalanceText", "Balance"},
            { "NoTransactionsText", "No transactions found..."},
            { "PurchasesText", "Purchases"},
            { "LoggedIPText", "Logged IP address"},
            { "NoPurchasesText", "No purchases found..."},
            { "PurchaseCostText", "Cost"},
       
            // Classifieds
            { "ClassifiedsText", "Объявления"},
            { "ClassifiedText", "Classified"},
            { "ListedByText", "Внесены"},
            { "CreationDateText", "Указанной даты"},
            { "ExpirationDateText", "Срок годности" },
            { "DescriptionText", "Описание" },
            { "PriceOfListingText", "Цена"},
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
            { "HostedByText","Сделано"},
            { "EventDateText", "начинающегося"},
            { "EventTimeInfoText", "Event time should be local time (Server)"},
            { "CoverChargeText", "входная плата"},
            { "Duration", "продолжительность"},
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
            { "StatisticsText", "Viewer statistics"},
            { "ViewersText", "Viewer usage"},
            { "GPUText", "Graphics cards"},
            { "PerformanceText", "Performance averages"},
            { "FPSText", "Frames / second"},
            { "RunTimeText", "Run time"},
            { "RegionsVisitedText", "Regions visited"},
            { "MemoryUseageText", "Memory use"},
            { "PingTimeText", "Ping time"},
            { "AgentsInViewText", "Agents in view"},
            { "ClearStatsText", "Clear statistics data"},

            // Abuse reports
            { "MenuAbuse", "Abuse Reports"},
            { "TooltipsMenuAbuse", "User abuse reports"},
            { "AbuseReportText", "Abuse Report"},
            { "AbuserNameText", "Abuser"},
            { "AbuseReporterNameText", "Reporter"},
            { "AssignedToText", "Assigned to"},

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
            { "GridnameText", "Grid name"},
            { "GridnickText", "Grid nickname"},
            { "WelcomeMessageText", "Login welcome message"},
            { "GovernorNameText", "System Governor"},
            { "MainlandEstateNameText", "Mainland estate"},
            { "RealEstateOwnerNameText", "System estate owner"},
            { "SystemEstateNameText", "System estate"},
            { "BankerNameText", "System banker"},
            { "MarketPlaceOwnerNameText", "System marketplace owner"},

            // Settings manager
            { "WebRegistrationText", "Web registrations allowed"},
            { "GridCenterXText", "Grid Center Location X"},
            { "GridCenterYText", "Grid Center Location Y"},
            { "SettingsManager", "Settings Manager"},
            { "IgnorePagesUpdatesText", "Ignore pages update warning until next update"},
            { "IgnoreSettingsUpdatesText", "Ignore settings update warning until next update"},
            { "HideLanguageBarText", "Hide Language Selection Bar"},
            { "HideStyleBarText", "Hide Style Selection Bar"},
            { "HideSlideshowBarText", "Hide Slideshow Bar"},
            { "LocalFrontPageText", "Local front page"},
            { "LocalCSSText", "Local CSS stylesheet"},

            // Dates
            { "Sun", "Вс"},
            { "Mon", "Пн"},
            { "Tue", "Вт"},
            { "Wed", "Ср"},
            { "Thu", "Чт"},
            { "Fri", "Пт"},
            { "Sat", "Сб"},
            { "Sunday", "Воскресенье"},
            { "Monday", "Понедельник"},
            { "Tuesday", "Вторник"},
            { "Wednesday", "Среда"},
            { "Thursday", "Четверг"},
            { "Friday", "Пятница"},
            { "Saturday", "Суббота"},

            { "Jan_Short", "Янв"},
            { "Feb_Short", "Фев"},
            { "Mar_Short", "Март"},
            { "Apr_Short", "Апр"},
            { "May_Short", "Май"},
            { "Jun_Short", "Июнь"},
            { "Jul_Short", "Июль"},
            { "Aug_Short", "Авг"},
            { "Sep_Short", "Сен"},
            { "Oct_Short", "Окт"},
            { "Nov_Short", "Ноя"},
            { "Dec_Short", "Дек"},

            { "January", "Январь"},
            { "February", "Февраль"},
            { "March", "Март"},
            { "April", "Апрель"},
            { "May", "Май"},
            { "June", "Июнь"},
            { "July", "Июль"},
            { "August", "Август"},
            { "September", "Сентябрь"},
            { "October", "Октябрь"},
            { "November", "Ноябрь"},
            { "December", "Декабрь"},

            // User types
            { "UserTypeText", "User type"},
            { "AdminUserTypeInfoText", "The type of user (Currently used for periodical stipend payments)."},
            { "Guest", "Гость"},
            { "Resident", "Resident"},
            { "Member", "Member"},
            { "Contractor", "Contractor"},
            { "Charter_Member", "Charter Member"},

            // ColorBox
            { "ColorBoxImageText", "Image"},
            { "ColorBoxOfText", "of"},
            { "ColorBoxPreviousText", "Previous"},
            { "ColorBoxNextText", "Далее"},
            { "ColorBoxCloseText", "Закрыть"},
            { "ColorBoxStartSlideshowText", "Start Slide Show"},
            { "ColorBoxStopSlideshowText", "Stop Slide Show"},

            // Maintenance
            { "NoAccountFound", "No account found"},
            { "DisplayInMenu", "Display In Menu"},
            { "ParentText", "Menu Parent"},
            { "CannotSetParentToChild", "Cannot set menu item as a child to itself."},
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
            { "fr", "Français"},
            { "de", "Deutsch"},
            { "it", "Italiano"},
            { "es", "Español"},
            { "nl", "Nederlands"},
            { "ru", "Русский"},

        };

    }
}
