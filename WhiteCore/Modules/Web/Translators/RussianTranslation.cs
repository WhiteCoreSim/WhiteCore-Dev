/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://virtual-planets.org/,  http://reallife3d.ru/
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
	public class RussianTranslation : ITranslator
	{
		public string LanguageName {
			get { return "ru"; }
		}

		public string GetTranslatedString (string key)
		{
			switch (key) {
			// Generic
			case "No":
				return "Нет";
			case "Yes":
				return "Да";
			case "Submit":
				return "Отправить";
			case "Accept":
				return "Accept";
			case "Save":
				return "Сохранить";
			case "FirstText":
				return "First";
			case "BackText":
				return "Назад";
			case "NextText":
				return "Следующий";
			case "LastText":
				return "Last";
			case "CurrentPageText":
				return "Current Page";
			case "MoreInfoText":
				return "Больше информации";
			case "NoDetailsText":
				return "No details found...";
			case "ObjectNameText":
				return "Object";
			case "LocationText":
				return "Регион";
			case "UUIDText":
				return "UUID";
			case "DetailsText":
				return "Description";
			case "NotesText":
				return "Notes";
			case "SaveUpdates":
				return "Сохранить";
			case "ActiveText":
				return "Active";
			case "CheckedText":
				return "Checked";
			case "CategoryText":
				return "Категория";
			case "SummaryText":
				return "Summary";
                
			// Status information
			case "GridStatus":
				return "Статус";
			case "Online":
				return "В 3D Мире";
			case "Offline":
				return "Нет в Сети";
			case "TotalUserCount":
				return "Пользователей";
			case "TotalRegionCount":
				return "Total Region Count";
			case "UniqueVisitors":
				return "За последние 30 дней";
			case "OnlineNow":
				return "В игре";
			case "HyperGrid":
				return "HyperGrid (HG)";
			case "Voice":
				return "Голосовой";
			case "Currency":
				return "Currency";
			case "Disabled":
				return "Disabled";
			case "Enabled":
				return "Enabled";
			case "News":
				return "Новости";
			case "Region":
				return "Регион";

			// User login
			case "Login":
				return "Вход";
			case "UserName":
			case "UserNameText":
				return "Имя пользователя";
			case "Password":
			case "PasswordText":
				return "Пароль";
			case "PasswordConfirmation":
				return "Password Confirmation";
			case "ForgotPassword":
				return "Забыли пароль?";
			case "TypeUserNameToConfirm":
				return "Пожалуйста, введите имя пользователя этой учетной записи, чтобы подтвердить, что вы хотите удалить эту учетную запись";

			// Special windows
			case "SpecialWindowTitleText":
				return "Special Info Window Title";
			case "SpecialWindowTextText":
				return "Special Info Window Text";
			case "SpecialWindowColorText":
				return "Special Info Window Color";
			case "SpecialWindowStatusText":
				return "Special Info Window Status";
			case "WelcomeScreenManagerFor":
				return "Welcome Screen Manager For";
			case "ChangesSavedSuccessfully":
				return "Changes Saved Successfully";

			// User registration
			case "AvatarNameText":
				return "Имя пользователя";
			case "AvatarScopeText":
				return "Avatar Scope ID";
			case "FirstNameText":
				return "Имя";
			case "LastNameText":
				return "Фамилия";
			case "UserAddressText":
				return "Адрес";
			case "UserZipText":
				return "Почтовый Индекс";
			case "UserCityText":
				return "Город";
			case "UserCountryText":
				return "Страна";
			case "UserDOBText":
				return "Дата рождения";
			case "UserEmailText":
				return "Email";
			case "UserHomeRegionText":
				return "Выберите Регион";
			case "RegistrationText":
				return "Регистрация";
			case "RegistrationsDisabled":
				return "Регистрация в данный момент отключена, пожалуйста, попробуйте еще раз в ближайшее время.";
			case "TermsOfServiceText":
				return "Условия Пользования";
			case "TermsOfServiceAccept":
				return "Вы принимаете условия пользования, как описано выше?";
			case "AvatarNameError":
				return "Вы не ввели имя аватара!";
			case "AvatarPasswordError":
				return "Поле пароля пустое или не совпадает!";
			case "AvatarEmailError":
				return "Адрес электронной почты необходим для восстановления пароля!";
			case "AvatarNameSpacingError":
				return "Имя аватар должно быть \"Имя Фамилия\"!";

			// news
			case "OpenNewsManager":
				return "Open the news manager";
			case "NewsManager":
				return "News Manager";
			case "EditNewsItem":
				return "Edit news item";
			case "AddNewsItem":
				return "Add new news item";
			case "DeleteNewsItem":
				return "Delete news item";
			case "NewsDateText":
				return "News Date";
			case "NewsTitleText":
				return "News Title";
			case "NewsItemTitle":
				return "News Item Title";
			case "NewsItemText":
				return "News Item Text";
			case "AddNewsText":
				return "Add News";
			case "DeleteNewsText":
				return "Delete News";
			case "EditNewsText":
				return "Edit News";

			// User Profile
			case "UserProfileFor":
				return "Профиль пользователя";
			case "UsersGroupsText":
				return "Мои группы";
			case "GroupNameText":
				return "Группы";
			case "UsersPicksText":
				return "Места";
			case "ResidentSince":
				return "Дата регистрации";
			case "AccountType":
				return "Должность";
			case "PartnersName":
				return "Партнёр";
			case "AboutMe":
				return "Обо мне";
			case "IsOnlineText":
				return "Статус";
			case "OnlineLocationText":
				return "Локация";
			case "Partner":
				return "Нет партнёра";
			case "Nothing":
				return "Нет информации";
			case "ChangePass":
				return "Ваш пароль изменен";
			case "NoChangePass":
				return "Не удалось изменить пароль, попробуйте еще раз позже";

			// Region Information
			case "RegionInformationText":
				return "Информация об регионе";
			case "OwnerNameText":
				return "Владелец";
			case "RegionLocationText":
				return "Координаты";
			case "RegionSizeText":
				return "Размер";
			case "RegionNameText":
				return "Название";
			case "RegionTypeText":
				return "Region Type";
			case "RegionDelayStartupText":
				return "Delay starting scripts";
			case "RegionPresetText":
				return "Region Preset";
			case "RegionTerrainText":
				return "Region Terrain";
			case "ParcelsInRegionText":
				return "Parcels In Region";
			case "ParcelNameText":
				return "Parcel Name";
			case "ParcelOwnerText":
				return "Parcel Owner's Name";

			// Region List
			case "RegionInfoText":
				return "Информация об регионе";
			case "RegionListText":
				return "Список регионов";
			case "RegionLocXText":
				return "Позиция X";
			case "RegionLocYText":
				return "Позиция Y";
			case "SortByLocX":
				return "Сортировать по позиции X";
			case "SortByLocY":
				return "Сортировать по позиции Y";
			case "SortByName":
				return "Сортировать по имени региона";
			case "RegionMoreInfo":
				return "Больще информации";
			case "RegionMoreInfoTooltips":
				return "Подробнее о";
			case "OnlineUsersText":
				return "Пользователи";
			case "RegionOnlineText":
				return "Статус";
			case "RegionMaturityText":
				return "Открыть Рейтинг";
			case "NumberOfUsersInRegionText":
				return "Количество пользователей в регионе";

			// Region manager
			case "Mainland":
				return "Материк";
			case "Estate":
				return "Имущество";
			case "FullRegion":
				return "Полный Регион";
			case "Homestead":
				return "Homestead";
			case "Openspace":
				return "Космос";
			case "Flatland":
				return "Равнина";
			case "Grassland":
				return "Поле";
			case "Island":
				return "Остров";
			case "Aquatic":
				return "Океан";
			case "Custom":
				return "На заказ";
			case "RegionPortText":
				return "Порт";
			case "RegionVisibilityText":
				return "Видимый для соседей";
			case "RegionInfiniteText":
				return "Бесконечная область";
			case "RegionCapacityText":
				return "Region object capacity";

			// Main Menu Buttons
			case "MenuHome":
				return "Домой";
			case "MenuLogin":
				return "Вход";
			case "MenuLogout":
				return "Выход";
			case "MenuRegister":
				return "Регистрация";
			case "MenuForgotPass":
				return "Забыли пароль";
			case "MenuNews":
				return "Новости";
			case "MenuWorld":
				return "Мир";
			case "MenuWorldMap":
				return "Карта мира";
			case "MenuRegion":
				return "Список локаций";
			case "MenuUser":
				return "Пользователи";
			case "MenuOnlineUsers":
				return "Онлайн пользователи";
			case "MenuUserSearch":
				return "Поиск";
			case "MenuRegionSearch":
				return "Поиск";
			case "MenuChat":
				return "Чат";
			case "MenuHelp":
				return "Помощь";
			case "MenuViewerHelp":
				return "Скачать";
			case "MenuChangeUserInformation":
				return "Change User Information";
			case "MenuWelcomeScreenManager":
				return "Welcome Screen Manager";
			case "MenuNewsManager":
				return "Новости";
			case "MenuUserManager":
				return "Пользователи";
			case "MenuFactoryReset":
				return "Сброс";
			case "ResetMenuInfoText":
				return "Resets the menu items back to the most updated defaults";
			case "ResetSettingsInfoText":
				return "Resets the Web Interface settings back to the most updated defaults";
			case "MenuPageManager":
				return "Страницы";
			case "MenuSettingsManager":
				return "Настройки сайта";
			case "MenuManager":
				return "Management";
			case "MenuSettings":
				return "Настройки";
			case "MenuRegionManager":
				return "Region Manager";
			case "MenuManagerSimConsole":
				return "Simulator console";
			case "MenuPurchases":
				return "User Purchases";
			case "MenuMyPurchases":
				return "My Purchases";
			case "MenuTransactions":
				return "User Transactions";
			case "MenuMyTransactions":
				return "Трансакции";
			case "MenuStatistics":
				return "Клиенты";
			case "MenuGridSettings":
				return "Сервер";

			// Main Menu Tooltips
			case "TooltipsMenuHome":
				return "Home";
			case "TooltipsMenuLogin":
				return "Login";
			case "TooltipsMenuLogout":
				return "Logout";
			case "TooltipsMenuRegister":
				return "Register";
			case "TooltipsMenuForgotPass":
				return "Forgot Password";
			case "TooltipsMenuNews":
				return "News";
			case "TooltipsMenuWorld":
				return "World";
			case "TooltipsMenuWorldMap":
				return "World Map";
			case "TooltipsMenuUser":
				return "User";
			case "TooltipsMenuOnlineUsers":
				return "Online Users";
			case "TooltipsMenuUserSearch":
				return "User Search";
			case "TooltipsMenuRegionSearch":
				return "Region Search";
			case "TooltipsMenuChat":
				return "Chat";
			case "TooltipsMenuViewerHelp":
				return "Viewer Help";
			case "TooltipsMenuHelp":
				return "Help";
			case "TooltipsMenuChangeUserInformation":
				return "Change User Information";
			case "TooltipsMenuWelcomeScreenManager":
				return "Welcome Screen Manager";
			case "TooltipsMenuNewsManager":
				return "News Manager";
			case "TooltipsMenuUserManager":
				return "User Manager";
			case "TooltipsMenuFactoryReset":
				return "Factory Reset";
			case "TooltipsMenuPageManager":
				return "Page Manager";
			case "TooltipsMenuSettingsManager":
				return "Settings Manager";
			case "TooltipsMenuManager":
				return "Admin Management";
			case "TooltipsMenuSettings":
				return "WebUI Settings";
			case "TooltipsMenuRegionManager":
				return "Region create/edit";
			case "TooltipsMenuManagerSimConsole":
				return "Online simulator console";
			case "TooltipsMenuPurchases":
				return "Purchase information";
			case "TooltipsMenuTransactions":
				return "Transaction information";
			case "TooltipsMenuStatistics":
				return "Viewer Statistics";
			case "TooltipsMenuGridSettings":
				return "Grid settings";

			// Menu Region box
			case "MenuRegionTitle":
				return "Регион";
			case "MenuParcelTitle":
				return "Parcels";
			case "MenuOwnerTitle":
				return "Владелец";
			case "TooltipsMenuRegion":
				return "Region Details";
			case "TooltipsMenuParcel":
				return "Region Parcels";
			case "TooltipsMenuOwner":
				return "Estate Owner";

			// Menu Profile Box
			case "MenuProfileTitle":
				return "Profile";
			case "MenuGroupTitle":
				return "Groups";
			case "MenuPicksTitle":
				return "Picks";
			case "MenuRegionsTitle":
				return "Regions";
			case "TooltipsMenuProfile":
				return "User Profile";
			case "TooltipsMenuGroups":
				return "User Groups";
			case "TooltipsMenuPicks":
				return "User Selections";
			case "TooltipsMenuRegions":
				return "User Regions";
			case "UserGroupNameText":
				return "User group";
			case "PickNameText":
				return "Pick name";
			case "PickRegionText":
				return "Location";

			// Urls
			case "WelcomeScreen":
				return "Welcome Screen";

			// Tooltips Urls
			case "TooltipsWelcomeScreen":
				return "Welcome Screen";
			case "TooltipsWorldMap":
				return "World Map";

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
				return "Styles";
			case "StyleSwitcherLanguagesText":
				return "Languages";
			case "StyleSwitcherChoiceText":
				return "Choice";

			// Language Switcher Tooltips
			case "ru":
				return "Русский";
			case "en":
				return "English";
			case "de":
				return "German";
			case "it":
				return "Italian";
			case "es":
				return "Spanish";
			case "nl":
				return "Dutch";

			// Index Page
			case "HomeText":
				return "Главная";
			case "HomeTextWelcome":
				return "This is our New Virtual World! Join us now, and make a difference!";
			case "HomeTextTips":
				return "New presentations";
			case "WelcomeToText":
				return "Welcome to";

			// World Map Page
			case "WorldMap":
				return "Карта мира";
			case "WorldMapText":
				return "На весь экран";

			// Chat Page
			case "ChatText":
				return "Chat Support";

			// Help Page
			case "HelpText":
				return "Скачать";
			case "HelpViewersConfigText":
				return "Viewer Configuration";
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
			case "Logout":
				return "Выход";
			case "LoggedOutSuccessfullyText":
				return "Вы успешно вышли из системы.";

			//Change user information page
			case "ChangeUserInformationText":
				return "Настройки";
			case "ChangePasswordText":
				return "Изменить пароль";
			case "NewPasswordText":
				return "Новый пароль";
			case "NewPasswordConfirmationText":
				return "Повторите пароль";
			case "ChangeEmailText":
				return "Изменть Email";
			case "NewEmailText":
				return "Новый Email адрес";
			case "DeleteUserText":
				return "Удалить аккаунт";
			case "DeleteText":
				return "Удалить";
			case "DeleteUserInfoText":
				return
               "Вы уверены, что хотите удалить свой аккаунт в ReaL Life 3D без возможности восстановления. Если вы хотите продолжить, введите свое имя и пароль и нажмите кнопку Удалить.";
			case "EditText":
				return "Редактировать";
			case "EditUserAccountText":
				return "Редактировать аккаунт пользователя";

			//Maintenance page
			case "WebsiteDownInfoText":
				return "Сайт на данный момент не работает, пожалуйста, попробуйте еще раз в ближайшее время.";
			case "WebsiteDownText":
				return "Сайт отключен на технические работы";

			//http_404 page
			case "Error404Text":
				return "Ошибка";
			case "Error404InfoText":
				return "404 страница не существует";
			case "HomePage404Text":
				return "Главная страница";

			//http_505 page
			case "Error505Text":
				return "Ошибка";
			case "Error505InfoText":
				return "505 Внутренняя Ошибка Сервера";
			case "HomePage505Text":
				return "Главная страница";

			//user_search page
			case "Search":
				return "Поиск";
			case "SearchText":
				return "Поиск";
			case "SearchForUserText":
				return "Поиск пользователя";
			case "UserSearchText":
				return "Поиск пользователя";
			case "SearchResultForUserText":
				return "Результат поиска";

			//region_search page
			case "SearchForRegionText":
				return "Поиск Региона";
			case "RegionSearchText":
				return "Поиск Региона";
			case "SearchResultForRegionText":
				return "Результат поиска";

			//Edit user page
			case "AdminDeleteUserText":
				return "Удалить пользователя";
			case "AdminDeleteUserInfoText":
				return "Удалить аккаунт и всю связанную с ним информацию.";
			case "BanText":
				return "Заблокировать";
			case "UnbanText":
				return "Разблокировать";
			case "AdminTempBanUserText":
				return "Бан на время";
			case "AdminTempBanUserInfoText":
				return "Блокирует вход пользователя в течение заданного времени.";
			case "AdminBanUserText":
				return "Бан навсегда";
			case "AdminBanUserInfoText":
				return "Блокирует вход пользователя в навсегда, пока он не будет снят Сотрудниками.";
			case "AdminUnbanUserText":
				return "Разблокировать";
			case "AdminUnbanUserInfoText":
				return "Removes temporary and permanent bans on the user.";
			case "AdminLoginInAsUserText":
				return "Вход пользователем в 3D мир";
			case "AdminLoginInAsUserInfoText":
				return
                       "Вы выйдете из вашей учетной записи Сотрудника и войдете в систему как этот пользователь.";
			case "TimeUntilUnbannedText":
				return "Время Блокировки";
			case "DaysText":
				return "Дней";
			case "HoursText":
				return "Часов";
			case "MinutesText":
				return "Минут";
			case "EdittingText":
				return "Editing";
			case "BannedUntilText":
				return "User banned until:";
			case "KickAUserText":
				return "Выкинуть";
			case "KickAUserInfoText":
				return "Выкинуть пользователя с 3D мира (срабатывает в течение 30 секунд)";
			case "KickMessageText":
				return "Причина отключения";
			case "KickUserText":
				return "Отключение от 3D мира";
			case "MessageAUserText":
				return "Отправить сообщение";
			case "MessageAUserInfoText":
				return "Отправляет сообщение пользователю";
			case "MessageUserText":
				return "Отправить";

			// Transactions
			case "TransactionsText":
				return "Transactions";
			case "DateInfoText":
				return "Select a date range";
			case "DateStartText":
				return "Commencing Date";
			case "DateEndText":
				return "Ending Date";
			case "30daysPastText":
				return "Previous 30 days";
			case "TransactionToAgentText":
				return "To User";
			case "TransactionFromAgentText":
				return "From User";
			case "TransactionDateText":
				return "Date";
			case "TransactionDetailText":
				return "Description";
			case "TransactionAmountText":
				return "Amount";
			case "TransactionBalanceText":
				return "Balance";
			case "NoTransactionsText":
				return "No transactions found...";
			case "PurchasesText":
				return "Purchases";
			case "LoggedIPText":
				return "Logged IP address";
			case "NoPurchasesText":
				return "No purchases found...";
			case "PurchaseCostText":
				return "Cost";

			// Sim Console
			case "SimConsoleText":
				return "Sim Command Console";
			case "SimCommandText":
				return "Command";

			//factory_reset
			case "FactoryReset":
				return "Factory Reset";
			case "ResetMenuText":
				return "Reset Menu To Factory Defaults";
			case "ResetSettingsText":
				return "Reset Web Settings (Settings Manager page) To Factory Defaults";
			case "Reset":
				return "Reset";
			case "Settings":
				return "Settings";
			case "Pages":
				return "Pages";
			case "DefaultsUpdated":
				return "defaults updated, go to Factory Reset to update or Settings Manager to disable this warning.";

			//page_manager
			case "PageManager":
				return "Page Manager";
			case "SaveMenuItemChanges":
				return "Save Menu Item";
			case "SelectItem":
				return "Select Item";
			case "DeleteItem":
				return "Delete Item";
			case "AddItem":
				return "Add Item";
			case "PageLocationText":
				return "Page Location";
			case "PageIDText":
				return "Page ID";
			case "PagePositionText":
				return "Page Position";
			case "PageTooltipText":
				return "Page Tooltip";
			case "PageTitleText":
				return "Page Title";
			case "RequiresLoginText":
				return "Requires Login To View";
			case "RequiresLogoutText":
				return "Requires Logout To View";
			case "RequiresAdminText":
				return "Requires Admin To View";
			case "RequiresAdminLevelText":
				return "Required Admin Level To View";

			// grid settings
			case "GridSettingsManager":
				return "Grid Settings Manager";
			case "GridnameText":
				return "Grid name";
			case "GridnickText":
				return "Grid nickname";
			case "WelcomeMessageText":
				return "Login welcome message";
			case "SystemEstateNameText":
				return "System estate name";
			case "SystemEstateOwnerText":
				return "System estate owner name";

			//settings manager page
			case "WebRegistrationText":
				return "Web registrations allowed";
			case "GridCenterXText":
				return "Grid Center Location X";
			case "GridCenterYText":
				return "Grid Center Location Y";
			case "SettingsManager":
				return "Settings Manager";
			case "IgnorePagesUpdatesText":
				return "Ignore pages update warning until next update";
			case "IgnoreSettingsUpdatesText":
				return "Ignore settings update warning until next update";
			case "HideLanguageBarText":
				return "Hide Language Selection Bar";
			case "HideStyleBarText":
				return "Hide Style Selection Bar";
			case "HideSlideshowBarText":
				return "Hide Slideshow Bar";
			case "LocalFrontPageText":
				return "Local front page";
			case "LocalCSSText":
				return "Local CSS stylesheet";

			// statistics
			case "StatisticsText":
				return "Viewer statistics";
			case "ViewersText":
				return "Viewer usage";
			case "GPUText":
				return "Graphics cards";
			case "PerformanceText":
				return "Performance averages";
			case "FPSText":
				return "Frames / second";
			case "RunTimeText":
				return "Run time";
			case "RegionsVisitedText":
				return "Regions visited";
			case "MemoryUseageText":
				return "Memory use";
			case "PingTimeText":
				return "Ping time";
			case "AgentsInViewText":
				return "Agents in view";
			case "ClearStatsText":
				return "Clear statistics data";

			// abuse reports
			case "MenuAbuse":
				return "Abuse Reports";
			case "TooltipsMenuAbuse":
				return "User abuse reports";
			case "AbuseReportText":
				return "Abuse Report";
			case "AbuserNameText":
				return "Abuser";
			case "AbuseReporterNameText":
				return "Reporter";
			case "AssignedToText":
				return "Assigned to";

			//Times
			case "Sun":
				return "Вс";
			case "Mon":
				return "Пн";
			case "Tue":
				return "Вт";
			case "Wed":
				return "Ср";
			case "Thu":
				return "Чт";
			case "Fri":
				return "Пт";
			case "Sat":
				return "Сб";
			case "Sunday":
				return "Воскресенье";
			case "Monday":
				return "Понедельник";
			case "Tuesday":
				return "Вторник";
			case "Wednesday":
				return "Среда";
			case "Thursday":
				return "Четверг";
			case "Friday":
				return "Пятница";
			case "Saturday":
				return "Суббота";

			case "Jan_Short":
				return "Янв";
			case "Feb_Short":
				return "Фев";
			case "Mar_Short":
				return "Март";
			case "Apr_Short":
				return "Апр";
			case "May_Short":
				return "Май";
			case "Jun_Short":
				return "Июнь";
			case "Jul_Short":
				return "Июль";
			case "Aug_Short":
				return "Авг";
			case "Sep_Short":
				return "Сен";
			case "Oct_Short":
				return "Окт";
			case "Nov_Short":
				return "Ноя";
			case "Dec_Short":
				return "Дек";

			case "January":
				return "Январь";
			case "February":
				return "Февраль";
			case "March":
				return "Март";
			case "April":
				return "Апрель";
			case "May":
				return "Май";
			case "June":
				return "Июнь";
			case "July":
				return "Июль";
			case "August":
				return "Август";
			case "September":
				return "Сентябрь";
			case "October":
				return "Октябрь";
			case "November":
				return "Ноябрь";
			case "December":
				return "Декабрь";

			// User types
			case "UserTypeText":
				return "User type";
			case "AdminUserTypeInfoText":
				return "The type of user (Currently used for periodical stipend payments).";
			case "Guest":
				return "Гость";
			case "Resident":
				return "Resident";
			case "Member":
				return "Member";
			case "Contractor":
				return "Contractor";
			case "Charter_Member":
				return "Charter Member";

			// ColorBox
			case "ColorBoxImageText":
				return "Image";
			case "ColorBoxOfText":
				return "of";
			case "ColorBoxPreviousText":
				return "Previous";
			case "ColorBoxNextText":
				return "Далее";
			case "ColorBoxCloseText":
				return "Закрыть";
			case "ColorBoxStartSlideshowText":
				return "Start Slide Show";
			case "ColorBoxStopSlideshowText":
				return "Stop Slide Show";

			// Maintenance
			case "NoAccountFound":
				return "No account found";
			case "DisplayInMenu":
				return "Display In Menu";
			case "ParentText":
				return "Menu Parent";
			case "CannotSetParentToChild":
				return "Cannot set menu item as a child to itself.";
			case "TopLevel":
				return "Top Level";
			}
			return "UNKNOWN CHARACTER";
		}
	}
}