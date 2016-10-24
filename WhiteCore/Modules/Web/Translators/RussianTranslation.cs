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
            { "Accept", "Принять"},
            { "Save", "Сохранить"},
            { "Cancel", "Отмена"},
            { "FirstText", "В начало"},
            { "BackText", "Назад"},
            { "NextText", "Дольше"},
            { "LastText", "В конец"},
            { "CurrentPageText", "Текущая страница"},
            { "MoreInfoText", "Больше информации"},
            { "NoDetailsText", "Никаких подробностей не найдено..."},
            { "MoreInfo", "Больше информации"},
            { "Name", "Имя"},
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
            { "MaturityText", "Рейтинг Зрелости"},
            { "GeneralText", "общий"},
            { "MatureText", "зрелый"},
            { "AdultText", "для взрослых"},
            { "DateText", "Дата"},
            { "TimeText", "Время"},
            { "MinuteText", "минута"},
            { "MinutesText", "минут"},
            { "HourText", "час"},
            { "HoursText", "часов"},
            { "EditText", "Pедактировать"},
            { "EdittingText", "Редактируется"},

            // Status information
            { "GridStatus", "Статус Сети"},
            { "Online", "Онлайн"},
            { "Offline", "Офлайн"},
            { "TotalUserCount", "Всего Пользователей"},
            { "TotalRegionCount", "Всего Регионов"},
            { "UniqueVisitors", "За последние 30 дней"},
            { "OnlineNow", "В игре"},
            { "InterWorld", "Inter World (IWC)"},
            { "HyperGrid", "HyperGrid (HG)"},
            { "Voice", "Голосовоая связь"},
            { "Currency", "Валюта"},
            { "Disabled", "Откл."},
            { "Enabled", "Вкл."},
            { "News", "Новости"},
            { "Region", "Регион"},

            // User login
            { "Login", "Вход"},
            { "UserName", "Имя пользователя"},
            { "UserNameText", "Имя пользователя"},
            { "Password", "Пароль"},
            { "PasswordText", "Пароль"},
            { "PasswordConfirmation", "Подтверждение пароля"},
            { "ForgotPassword", "Забыли пароль?"},
            { "TypeUserNameToConfirm", "Пожалуйста, введите имя пользователя учетной записи, чтобы подтвердить её удаление."},

            // Special windows
            { "SpecialWindowTitleText", "Заголовок Окна Особой Информации"},
            { "SpecialWindowTextText", "Текст Окна Особой Информации"},
            { "SpecialWindowColorText", "Цвет Окна Особой Информации"},
            { "SpecialWindowStatusText", "Статус Окна Особой Информации"},
            { "WelcomeScreenManagerFor", "Управление Экраном Приветствия "},
            { "ChangesSavedSuccessfully", "Изменения сохранены успешно"},

            // User registration
            { "AvatarNameText", "Имя пользователя"},
            { "AvatarScopeText", "ID Аватара"},
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
            { "RegistrationsDisabled", "Регистрация в данный момент отключена, пожалуйста, попробуйте еще раз немного позже."},
            { "TermsOfServiceText", "Условия Пользования"},
            { "TermsOfServiceAccept", "Вы принимаете условия пользования описанные выше?"},
            { "AvatarNameError", "Вы не ввели имя аватара!"},
            { "AvatarPasswordError", "Поля пароля пустые или не совпадают!"},
            { "AvatarEmailError", "Адрес электронной почты необходим для восстановления пароля!"},
            { "AvatarNameSpacingError", "Имя аватар должно быть \"Имя Фамилия\"!"},

            // News
            { "OpenNewsManager", "Открыть управление новостями"},
            { "NewsManager", "Управление новостями"},
            { "EditNewsItem", "Редактировать новость"},
            { "AddNewsItem", "Добавить Новость"},
            { "DeleteNewsItem", "Удалить Новость"},
            { "NewsDateText", "Дата"},
            { "NewsTitleText", "Заголовок"},
            { "NewsItemTitle", "Заголовок Новости"},
            { "NewsItemText", "Текст Новости"},
            { "AddNewsText", "Добавить Новость"},
            { "DeleteNewsText", "Удалить Новость"},
            { "EditNewsText", "Редактировать Новость"},

            // User Profile
            { "UserProfileFor", "Профиль пользователя"},
            { "UsersGroupsText", "Мои группы"},
            { "GroupNameText", "Группы"},
            { "UsersPicksText", "Места"},
            { "ResidentSince", "Дата регистрации"},
            { "AccountType", "Тип Учетной Записи"},
            { "PartnersName", "Партнёр"},
            { "AboutMe", "Обо мне"},
            { "IsOnlineText", "Статус"},
            { "OnlineLocationText", "Локация"},
            { "Partner", "Нет партнёра"},
            { "Friends", "Друзья"},
            { "Nothing", "Нет информации"},
            { "ChangePass", "Ваш пароль изменен"},
            { "NoChangePass", "Не удалось изменить пароль, попробуйте еще раз позже"},

            // Region Information
            { "RegionInformationText", "Информация о регионе"},
            { "OwnerNameText", "Владелец"},
            { "RegionLocationText", "Координаты"},
            { "RegionSizeText", "Размер"},
            { "RegionNameText", "Название"},
            { "RegionTypeText", "Тип Региона"},
            { "RegionPresetTypeText", "Регион Предустановленная"},
            { "RegionDelayStartupText", "Задержка запуска скриптов"},
            { "RegionPresetText", "Регион Предустановленная"},
            { "RegionTerrainText", "Поверхность в Регионе"},
            { "ParcelsInRegionText", "Участки в Регионе"},
            { "ParcelNameText", "Название Участка"},
            { "ParcelOwnerText", "Имя Владельца Участка"},

            // Region List
            { "RegionInfoText", "Информация о регионе"},
            { "RegionListText", "Список регионов"},
            { "RegionLocXText", "Координат X"},
            { "RegionLocYText", "Координат Y"},
            { "SortByLocX", "Сортировать по координату X"},
            { "SortByLocY", "Сортировать по координату Y"},
            { "SortByName", "Сортировать по имени региона"},
            { "RegionMoreInfo", "Больше информации"},
            { "RegionMoreInfoTooltips", "Подробнее о"},
            { "OnlineUsersText", "Пользователи"},
            { "OnlineFriendsText", "Друзья в сети"},
            { "RegionOnlineText", "Статус"},
            { "RegionMaturityText", "Рейтинг Зрелости"},
            { "NumberOfUsersInRegionText", "Количество пользователей в регионе"},

            // Region manager
            { "AddRegionText", "Добавить область"},
            { "Mainland", "Материк"},
            { "Estate", "Имение"},
            { "FullRegion", "Полный Регион"},
            { "Homestead", "Родовое поместье"},
            { "Openspace", "Открытая местность"},
            { "Flatland", "Равнина"},
            { "Grassland", "Поле"},
            { "Island", "Остров"},
            { "Aquatic", "Океан"},
            { "Custom", "На заказ"},
            { "RegionPortText", "Порт"},
            { "RegionVisibilityText", "Видимый для соседей"},
            { "RegionInfiniteText", "Бесконечная область"},
            { "RegionCapacityText", "Вместимость"},
            { "NormalText", "Нормальный"},
            { "DelayedText", "Задерживается"},

            // Estate management
            {"AddEstateText", "Добавить Estate"},
            {"EstateText", "имущество"},
            {"EstatesText", "Estates"},
            {"PricePerMeterText", "Цена за квадратный метр"},
            {"PublicAccessText", "Публичный доступ"},
            {"AllowVoiceText", "Разрешить голос"},
            {"TaxFreeText", "Tax Free"},
            {"AllowDirectTeleportText", "Разрешить прямой телепортации"},

            // Menus
            { "MenuHome", "Главная"},
            { "MenuLogin", "Вход"},
            { "MenuLogout", "Выход"},
            { "MenuRegister", "Регистрация"},
            { "MenuForgotPass", "Забыли пароль?"},
            { "MenuNews", "Новости"},
            { "MenuWorld", "Мир"},
            { "MenuWorldMap", "Карта мира"},
            { "MenuRegion", "Список Регионов"},
            { "MenuUser", "Пользователи"},
            { "MenuOnlineUsers", "Пользователи в Сети"},
            { "MenuUserSearch", "Поиск"},
            { "MenuRegionSearch", "Поиск"},
            { "MenuChat", "Чат"},
            { "MenuHelp", "Помощь"},
            { "MenuViewerHelp", "Настройки и клиент программы"},
            { "MenuChangeUserInformation", "Изменить инфо пользователя"},
            { "MenuWelcomeScreenManager", "Управление Экраном Приветствия"},
            { "MenuNewsManager", "Управление Новостями"},
            { "MenuUserManager", "Пользователи"},
            { "MenuFactoryReset", "Сброс настроек на значения по умолчанию"},
            { "ResetMenuInfoText", "Сброс элементов меню к более обновленным значениям по умолчанию"},
            { "ResetSettingsInfoText", "Сброс настроек Web Интерфейса к более обновленным значениям по умолчанию"},
            { "MenuPageManager", "Управление Страницами"},
            { "MenuSettingsManager", "Настройки Web Интерфейса"},
            { "MenuManager", "Управление"},
            { "MenuSettings", "Настройки"},
            { "MenuRegionManager", "Управление Регионами"},
            { "MenuEstateManager", "менеджер по недвижимости"},
            { "MenuManagerSimConsole", "Консоль Симулятора"},
            { "MenuPurchases", "Покупки Игровой Валюты Пользователями"},
            { "MenuMyPurchases", "Мои покупки Игровой Валюты"},
            { "MenuTransactions", "Трансакции Пользователей"},
            { "MenuMyTransactions", "Мои Трансакции"},
            { "MenuClassifieds", "Объявления"},
            { "MenuMyClassifieds", "Мои Объявления"},
            { "MenuEvents", "Мероприятия"},
            { "MenuMyEvents", "Мои Мероприятия"},
            { "MenuStatistics", "Статистика"},
            { "MenuGridSettings", "Настрока Сети"},

            // Menu Tooltips
            { "TooltipsMenuHome", "Главная"},
            { "TooltipsMenuLogin", "Вход"},
            { "TooltipsMenuLogout", "Выход"},
            { "TooltipsMenuRegister", "Регистрация"},
            { "TooltipsMenuForgotPass", "Забыли пароль?"},
            { "TooltipsMenuNews", "Новости"},
            { "TooltipsMenuWorld", "Мир"},
            { "TooltipsMenuWorldMap", "Карта Мира"},
            { "TooltipsMenuUser", "Пользователь"},
            { "TooltipsMenuOnlineUsers", "Пользоваели в Сети"},
            { "TooltipsMenuUserSearch", "Поиск Пользователей"},
            { "TooltipsMenuRegionSearch", "Поиск Регинов"},
            { "TooltipsMenuChat", "Чат"},
            { "TooltipsMenuViewerHelp", "Помощь по настройке клиент-программы"},
            { "TooltipsMenuHelp", "Помошь"},
            { "TooltipsMenuChangeUserInformation", "Изменить Инфо Пользователя"},
            { "TooltipsMenuWelcomeScreenManager", "Управление Экраном Приветствия"},
            { "TooltipsMenuNewsManager", "Управление Новостями"},
            { "TooltipsMenuUserManager", "Управление Пользователями"},
            { "TooltipsMenuFactoryReset", "Сброс настроек на значения по умолчанию"},
            { "TooltipsMenuPageManager", "Управление Страницами"},
            { "TooltipsMenuSettingsManager", "Управление Настройками"},
            { "TooltipsMenuManager", "Административное Управление"},
            { "TooltipsMenuSettings", "Настройки Пользовательского Web Интерфейса"},
            { "TooltipsMenuRegionManager", "Создание/редактирование Региона"},
            { "TooltipsMenuEstateManager", "управление недвижимостью"},
            { "TooltipsMenuManagerSimConsole", "Online консоль симулятора"},
            { "TooltipsMenuPurchases", "Инфо о покупках игровой валюты"},
            { "TooltipsMenuTransactions", "Инфо о Трансакциях"},
            { "TooltipsMenuClassifieds", "Инфо об Объявлениях"},
            { "TooltipsMenuEvents", "Инфо о Мероприятиях"},
            { "TooltipsMenuStatistics", "Статистика Подключений"},
            { "TooltipsMenuGridSettings", "Настройка Сети"},

            // Menu Region box
            { "MenuRegionTitle", "Регион"},
            { "MenuParcelTitle", "Участки"},
            { "MenuOwnerTitle", "Владелец"},
            { "TooltipsMenuRegion", "Детали о Регионах"},
            { "TooltipsMenuParcel", "Детали об Участке"},
            { "TooltipsMenuOwner", "Владелец Имения"},

            // Menu Profile box
            { "MenuProfileTitle", "Профиль"},
            { "MenuGroupTitle", "Группы"},
            { "MenuPicksTitle", "Избранное"},
            { "MenuRegionsTitle", "Регионы"},
            { "TooltipsMenuProfile", "Профиль Пользователя"},
            { "TooltipsMenuGroups", "Группы Пользователя"},
            { "TooltipsMenuPicks", "Избранное Пользователя"},
            { "TooltipsMenuRegions", "Регионы Пользователя"},
            { "UserGroupNameText", "Группы Пользователя"},
            { "PickNameText", "Название Избранного"},
            { "PickRegionText", "Локация"},

            // Urls
            { "WelcomeScreen", "Экран Приветствия"},

            // Tooltips Urls
            { "TooltipsWelcomeScreen", "Экран Приветствия"},
            { "TooltipsWorldMap", "Карта Мира"},

            // Index
            { "HomeText", "Главная"},
            { "HomeTextWelcome", "Это наш Новый Виртуальный Мир! Присоединяйтесь к нам и сделайте отличие!"},
            { "HomeTextTips", "New presentations"},
            { "WelcomeToText", "Добро пожаловать в"},

            // World Map
            { "WorldMap", "Карта Мира"},
            { "WorldMapText", "На весь экран"},

            // Chat Page
            { "ChatText", "Служба Поддержки в Чате"},

            // Help Page
            { "HelpText", "Помощь"},
            { "HelpViewersConfigText", "Настройка Клиент-Программ"},

            // Logout
            { "Logout", "Выход"},
            { "LoggedOutSuccessfullyText", "Вы успешно вышли из системы."},

            // Change user information
            { "ChangeUserInformationText", "Изменить Инфо Пользователя"},
            { "ChangePasswordText", "Изменить пароль"},
            { "NewPasswordText", "Новый пароль"},
            { "NewPasswordConfirmationText", "Повторите пароль"},
            { "ChangeEmailText", "Изменть Email"},
            { "NewEmailText", "Новый Email адрес"},
            { "DeleteUserText", "Удалить аккаунт"},
            { "DeleteText", "Удалить"},
            { "DeleteUserInfoText",
               "Это удалит всю информацию о вас в сетке и удалит доступ к сервису. Если вы хотите продолжить, введите имя и пароль и нажмите кнопку Удалить."},
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
            { "AdminTempBanUserText", "Блокировка на время"},
            { "AdminTempBanUserInfoText", "Блокирует вход пользователя в течение заданного времени."},
            { "AdminBanUserText", "Бан навсегда"},
            { "AdminBanUserInfoText", "Блокирует вход пользователя в навсегда, пока он не будет снят Сотрудниками."},
            { "AdminUnbanUserText", "Разблокировать"},
            { "AdminUnbanUserInfoText", "Убирает временные и постоянные блокировки."},
            { "AdminLoginInAsUserText", "Войти как пользователь в 3D мир"},
            { "AdminLoginInAsUserInfoText",
                "Вы выйдете из вашей учетной записи Администратора и войдете в систему как этот пользователь."},
            { "BannedUntilText", "Пользователь заблокирован до:"},
            { "KickAUserText", "Выкинуть"},
            { "KickAUserInfoText", "Выкинуть пользователя из 3D мира (срабатывает в течение 30 секунд)"},
            { "KickMessageText", "Причина отключения"},
            { "KickUserText", "Отключение от 3D мира"},
            { "MessageAUserText", "Отправить сообщение"},
            { "MessageAUserInfoText", "Отправляет сообщение пользователю"},
            { "MessageUserText", "Отправить"},

            // Transactions
            { "TransactionsText", "Трансакции"},
            { "DateInfoText", "Выбрать период"},
            { "DateStartText", "От"},
            { "DateEndText", "До"},
            { "30daysPastText", "Предыдущие 30 Дней"},
            { "TransactionToAgentText", "К Пользователю"},
            { "TransactionFromAgentText", "От Пользователя"},
            { "TransactionDateText", "Дата"},
            { "TransactionDetailText", "Описание"},
            { "TransactionAmountText", "Сума"},
            { "TransactionBalanceText", "На Счете"},
            { "NoTransactionsText", "Траинсакции не нейдены..."},
            { "PurchasesText", "Покупки"},
            { "LoggedIPText", "IP адрес"},
            { "NoPurchasesText", "Покупок не найдено..."},
            { "PurchaseCostText", "Стоимость"},
       
            // Classifieds
            { "ClassifiedsText", "Объявления"},
            { "ClassifiedText", "Объявление"},
            { "ListedByText", "Внесены"},
            { "CreationDateText", "Добавлено"},
            { "ExpirationDateText", "Истекает" },
            { "DescriptionText", "Описание" },
            { "PriceOfListingText", "Цена"},

            // Classified categories
            { "CatAll", "Все"},
            { "CatSelect", ""},
            { "CatShopping", "Магазины"},
            { "CatLandRental", "Аренда Земли"},
            { "CatPropertyRental", "Аренда Собственности"},
            { "CatSpecialAttraction", "Специальные Предложения"},
            { "CatNewProducts", "Новые Продукты"},
            { "CatEmployment", "Трудоустройство"},
            { "CatWanted", "Срочно Нужны"},
            { "CatService", "Сервис"},
            { "CatPersonal", "Личные"},

            // Events
            { "EventsText", "Мероприятия"},
            { "EventNameText", "Мероприятие"},
            { "EventLocationText", "Где"},
            { "HostedByText","Организатор"},
            { "EventDateText", "Когда"},
            { "EventTimeInfoText", "Время мероприятия должно быть указано по локальному времени в 3д Мире"},
            { "CoverChargeText", "Входная плата"},
            { "DurationText", "Продолжительность"},
            { "AddEventText", "Добавить Мероприятие"},

            // Event categories
            { "CatDiscussion", "Обсуждение"},
            { "CatSports", "Спорт"},
            { "CatLiveMusic", "Живая Музыка"},
            { "CatCommercial", "Коммерчесое"},
            { "CatEntertainment", "Ночная жизнь/Развлечение"},
            { "CatGames", "Игры/Конкурсы"},
            { "CatPageants", "Pageants"},
            { "CatEducation", "Образование"},
            { "CatArtsCulture", "Исскуство/Культура"},
            { "CatCharitySupport", "Благотворительность/Группа Поддержки"},
            { "CatMiscellaneous", "Разное"},

            // Event lookup periods
            { "Next24Hours", "Следущие 24 часа"},
            { "Next10Hours", "Следущие 10 часов"},
            { "Next4Hours", "Следущие 4 часа"},
            { "Next2Hours", "Следущие 2 часа"},

            // Sim Console
            { "SimConsoleText", "Команда в Консоль симулятора"},
            { "SimCommandText", "Команда"},

            // Statistics
            { "StatisticsText", "Статистика"},
            { "ViewersText", " Клиент-программы:"},
            { "GPUText", "Графические карты:"},
            { "PerformanceText", "Производительность в среднем:"},
            { "FPSText", "Кадров в секунду"},
            { "RunTimeText", "Время работы"},
            { "RegionsVisitedText", "Посещеные Регионы"},
            { "MemoryUseageText", "Памяти использовалось"},
            { "PingTimeText", "Время пинга"},
            { "AgentsInViewText", "Агенты в поле зрения"},
            { "ClearStatsText", "Очистить Статистику"},

            // Abuse reports
            { "MenuAbuse", "Жалобы"},
            { "TooltipsMenuAbuse", "Жалобы пользователей"},
            { "AbuseReportText", "Жалоба"},
            { "AbuserNameText", "Обвиняемый"},
            { "AbuseReporterNameText", "Докладчик"},
            { "AssignedToText", "Назначена"},

            // Factory_reset
            { "FactoryReset", "Сброс настроек на значения по умолчанию"},
            { "ResetMenuText", "Сбросить настройки Меню на значения по умолчанию"},
            { "ResetSettingsText", "Сбросить настройки Пользовательского Web Интерфейса на значения по умолчанию"},
            { "Reset", "Сброс"},
            { "Settings", "Настройки"},
            { "Pages", "Страницы"},
            { "UpdateRequired", "требуется обновление"},
            { "DefaultsUpdated", "значения по умолчанию обновлены, перейдите в Сброс настроек на значения по усолчанию для обновления или в Управление Настройками для отключения этого предупреждения."},

            // Page_manager
            { "PageManager", "Управление Страницами"},
            { "SaveMenuItemChanges", "Сохранить Элемент Меню"},
            { "SelectItem", "Выбрать Элемент"},
            { "DeleteItem", "Удалить Элемент"},
            { "AddItem", "Добавить Элемент"},
            { "PageLocationText", "Локация Страницы"},
            { "PageIDText", "ID Страницы"},
            { "PagePositionText", "Расположение Страницы"},
            { "PageTooltipText", "Описание Страницы"},
            { "PageTitleText", "Заголовок Страницы"},
            { "RequiresLoginText", "Требуется Вход для Просмотра"},
            { "RequiresLogoutText", "Требуется Выход для Просмотра"},
            { "RequiresAdminText", "Требуется Администратор для просмотра"},
            { "RequiresAdminLevelText", "Требуется Уровень Администратора для Просмотра"},

            // Grid settings
            { "GridSettingsManager", "Управление Настройками Сети"},
            { "GridnameText", "Название Сети"},
            { "GridnickText", "Ник Сети"},
            { "WelcomeMessageText", "Сообщение Приветствие при Входе"},
            { "GovernorNameText", "Системный Губернатор"},
            { "MainlandEstateNameText", "Название Имения на Митерике"},
            { "RealEstateOwnerNameText", "Владелец Системного Имения"},
            { "SystemEstateNameText", "Название Системного Имения"},
            { "BankerNameText", "Системный банкир"},
            { "MarketPlaceOwnerNameText", "Системный владелец Торговой Площадки"},

            // Settings manager
            { "WebRegistrationText", "Web рагистрации разрешены"},
            { "GridCenterXText", "Координат X Центральной Локации в Сети"},
            { "GridCenterYText", "Координат Y Центральной Локации в Сети"},
            { "SettingsManager", "Управление Настройками"},
            { "IgnorePagesUpdatesText", "Игнорировать предупреждения об изменениях страниц до следующего обновления"},
            { "IgnoreSettingsUpdatesText", "Игнорировать предупреждения об изменениях настроек до следующего обновления"},
            { "HideLanguageBarText", "Спрятать Панель Выборя Языка"},
            { "HideStyleBarText", "Спрятать Панель Выбора Стилей"},
            { "HideSlideshowBarText", "Отключить Слайды"},
            { "LocalFrontPageText", "Локальный файл главной страницы"},
            { "LocalCSSText", "Локальный файл стилей CSS"},

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
            { "UserTypeText", "Тип Пользователя"},
            { "AdminUserTypeInfoText", "Тип Пльзователя (сейчас используется для переодических выплат)."},
            { "Guest", "Гость"},
            { "Resident", "Житель"},
            { "Member", "Представитель"},
            { "Contractor", "Контрактник"},
            { "Charter_Member", "Представитель Администрации"},

            // ColorBox
            { "ColorBoxImageText", "Изображение"},
            { "ColorBoxOfText", "пользователя"},
            { "ColorBoxPreviousText", "Вернуться"},
            { "ColorBoxNextText", "Далее"},
            { "ColorBoxCloseText", "Закрыть"},
            { "ColorBoxStartSlideshowText", "Запустить Слайдшоу"},
            { "ColorBoxStopSlideshowText", "Остановить Слайдшоу"},

            // Maintenance
            { "NoAccountFound", "Учетных записей не найдено"},
            { "DisplayInMenu", "Показывать в Меню"},
            { "ParentText", "Основное Меню"},
            { "CannotSetParentToChild", "Нелья поставить Основное Мено в Подменю"},
            { "TopLevel", "Верхний Уровень"},

            // Style Switcher
            { "styles1", "Default Minimalist"},
            { "styles2", "Light Degarde"},
            { "styles3", "Blue Night"},
            { "styles4", "Dark Degrade"},
            { "styles5", "Luminus"},

            { "StyleSwitcherStylesText", "Стили"},
            { "StyleSwitcherLanguagesText", "Языки"},
            { "StyleSwitcherChoiceText", "Выбор"},

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
