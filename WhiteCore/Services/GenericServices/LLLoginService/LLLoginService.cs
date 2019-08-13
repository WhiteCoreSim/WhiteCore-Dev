/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using FriendInfo = WhiteCore.Framework.Services.FriendInfo;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using GridSettings = WhiteCore.Modules.Web.GridSettings;

namespace WhiteCore.Services
{
    public class LLLoginService : ILoginService, IService
    {
        static bool Initialized;

        // Global Textures
        const string sunTexture = "cce0f112-878f-4586-a2e2-a8f104bba271";
        const string cloudTexture = "dc4b9f0b-d008-45c6-96a4-01dd947ac621";
        const string moonTexture = "ec4b9f0b-d008-45c6-96a4-01dd947ac621";

        protected IUserAccountService m_UserAccountService;
        protected IAgentInfoService m_agentInfoService;
        protected IAuthenticationService m_AuthenticationService;
        protected IInventoryService m_InventoryService;
        protected IGridService m_GridService;
        protected ISimulationService m_SimulationService;
        protected ILibraryService m_LibraryService;
        protected IFriendsService m_FriendsService;
        protected IAvatarService m_AvatarService;
        protected IAssetService m_AssetService;
        protected ICapsService m_CapsService;
        protected IAvatarAppearanceArchiver m_ArchiveService;
        protected IRegistryCore m_registry;

        protected string m_DefaultRegionName;
        protected string m_WelcomeMessage;
        protected string m_WelcomeMessageURL;
        protected bool m_RequireInventory;
        protected int m_MinLoginLevel;
        protected bool m_AllowRemoteSetLoginLevel;

        protected IConfig m_loginServerConfig;
        protected IConfigSource m_config;
        protected bool m_AllowAnonymousLogin;
        protected bool m_AllowDuplicateLogin;
        protected string m_DefaultUserAvatarArchive = "DefaultAvatar.aa";
        protected string m_DefaultHomeRegion = "";
        protected Vector3 m_DefaultHomeRegionPos = new Vector3 ();
        protected ArrayList eventCategories = new ArrayList ();
        protected ArrayList classifiedCategories = new ArrayList ();
        protected List<ILoginModule> LoginModules = new List<ILoginModule> ();
        string m_forceUserToWearFolderName;
        string m_forceUserToWearFolderOwnerUUID;

        // 20190222 -greythane- for region logins
        readonly Dictionary<UUID, int> lastTelehub = new Dictionary<UUID, int> ();

        public int MinLoginLevel {
            get { return m_MinLoginLevel; }
        }

        public string WelcomeMessage {
            get { return m_WelcomeMessage; }
            set { m_WelcomeMessage = value; }
        }
        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            m_config = config;
            m_loginServerConfig = config.Configs ["LoginService"];
            IConfig handlersConfig = config.Configs ["Handlers"];
            if (handlersConfig == null || handlersConfig.GetString ("LoginHandler", "") != "LLLoginService")
                return;

            m_forceUserToWearFolderName = m_loginServerConfig.GetString ("forceUserToWearFolderName", "");
            m_forceUserToWearFolderOwnerUUID = m_loginServerConfig.GetString ("forceUserToWearFolderOwner", "");
            m_DefaultHomeRegion = m_loginServerConfig.GetString ("DefaultHomeRegion", "");
            m_DefaultHomeRegionPos = new Vector3 ();
            string defHomeRegPos = m_loginServerConfig.GetString ("DefaultHomeRegionPosition", "");
            if (defHomeRegPos != "") {
                string [] spl = defHomeRegPos.Replace (" ", "").Split (',');
                if (spl.Length == 2)
                    m_DefaultHomeRegionPos = new Vector3 (float.Parse (spl [0]), float.Parse (spl [1]), 25);
                else if (spl.Length == 3)
                    m_DefaultHomeRegionPos = new Vector3 (float.Parse (spl [0]), float.Parse (spl [1]), float.Parse (spl [2]));
            }
            m_DefaultUserAvatarArchive = m_loginServerConfig.GetString ("DefaultAvatarArchiveForNewUser",
                                                                       m_DefaultUserAvatarArchive);
            m_AllowAnonymousLogin = m_loginServerConfig.GetBoolean ("AllowAnonymousLogin", false);
            m_AllowDuplicateLogin = m_loginServerConfig.GetBoolean ("AllowDuplicateLogin", false);
            LLLoginResponseRegister.RegisterValue ("AllowFirstLife",
                                                  m_loginServerConfig.GetBoolean ("AllowFirstLifeInProfile", true)
                                                      ? "Y"
                                                      : "N");
            LLLoginResponseRegister.RegisterValue ("MaxAgentGroups", m_loginServerConfig.GetInt ("MaxAgentGroups", 100));
            LLLoginResponseRegister.RegisterValue ("VoiceServerType",
                                                  m_loginServerConfig.GetString ("VoiceServerType", "vivox"));
            ReadEventValues (m_loginServerConfig);
            ReadClassifiedValues (m_loginServerConfig);
            LLLoginResponseRegister.RegisterValue ("AllowExportPermission",
                                                  m_loginServerConfig.GetBoolean ("AllowUsageOfExportPermissions", true));

            m_DefaultRegionName = m_loginServerConfig.GetString ("DefaultRegion", string.Empty);
            m_WelcomeMessage = m_loginServerConfig.GetString ("WelcomeMessage", "");
            m_WelcomeMessage = m_WelcomeMessage.Replace ("\\n", "\n");
            m_WelcomeMessageURL = m_loginServerConfig.GetString ("CustomizedMessageURL", "");
            if (m_WelcomeMessageURL != "") {
                WebClient client = new WebClient ();
                try {
                    m_WelcomeMessage = client.DownloadString (m_WelcomeMessageURL);
                } catch {
                    MainConsole.Instance.Error ("[LLogin service]: Error obtaining welcome message from " + m_WelcomeMessageURL);
                }
                client.Dispose ();
            }
            // load web settings overrides (if any)
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
            var settings = generics.GetGeneric<GridSettings> (UUID.Zero, "GridSettings", "Settings");
            if (settings != null)
                m_WelcomeMessage = settings.WelcomeMessage;

            LLLoginResponseRegister.RegisterValue ("Message", m_WelcomeMessage);
            m_RequireInventory = m_loginServerConfig.GetBoolean ("RequireInventory", true);
            m_AllowRemoteSetLoginLevel = m_loginServerConfig.GetBoolean ("AllowRemoteSetLoginLevel", false);
            m_MinLoginLevel = m_loginServerConfig.GetInt ("MinLoginLevel", 0);
            LLLoginResponseRegister.RegisterValue ("SunTexture", m_loginServerConfig.GetString ("SunTexture", sunTexture));
            LLLoginResponseRegister.RegisterValue ("MoonTexture",
                                                  m_loginServerConfig.GetString ("MoonTexture", moonTexture));
            LLLoginResponseRegister.RegisterValue ("CloudTexture",
                                                  m_loginServerConfig.GetString ("CloudTexture", cloudTexture));


            registry.RegisterModuleInterface<ILoginService> (this);
            m_registry = registry;
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            m_UserAccountService = registry.RequestModuleInterface<IUserAccountService> ().InnerService;
            m_agentInfoService = registry.RequestModuleInterface<IAgentInfoService> ().InnerService;
            m_AuthenticationService = registry.RequestModuleInterface<IAuthenticationService> ();
            m_InventoryService = registry.RequestModuleInterface<IInventoryService> ();
            m_GridService = registry.RequestModuleInterface<IGridService> ();
            m_AvatarService = registry.RequestModuleInterface<IAvatarService> ().InnerService;
            m_FriendsService = registry.RequestModuleInterface<IFriendsService> ();
            m_SimulationService = registry.RequestModuleInterface<ISimulationService> ();
            m_AssetService = registry.RequestModuleInterface<IAssetService> ().InnerService;
            m_LibraryService = registry.RequestModuleInterface<ILibraryService> ();
            m_CapsService = registry.RequestModuleInterface<ICapsService> ();
            m_ArchiveService = registry.RequestModuleInterface<IAvatarAppearanceArchiver> ();

            if (!Initialized) {
                Initialized = true;
                RegisterCommands ();
            }

            LoginModules = WhiteCoreModuleLoader.PickupModules<ILoginModule> ();
            foreach (ILoginModule module in LoginModules) {
                module.Initialize (this, m_config, registry);
            }

            MainConsole.Instance.DebugFormat ("[LLogin service]: Starting...");
        }

        public void FinishedStartup ()
        {
        }

        public void ReadEventValues (IConfig config)
        {
            SetEventCategories ((int)DirectoryManager.EventCategories.Discussion, "Discussion");
            SetEventCategories ((int)DirectoryManager.EventCategories.Sports, "Sports");
            SetEventCategories ((int)DirectoryManager.EventCategories.LiveMusic, "Live Music");
            SetEventCategories ((int)DirectoryManager.EventCategories.Commercial, "Commercial");
            SetEventCategories ((int)DirectoryManager.EventCategories.Nightlife, "Nightlife/Entertainment");
            SetEventCategories ((int)DirectoryManager.EventCategories.Games, "Games/Contests");
            SetEventCategories ((int)DirectoryManager.EventCategories.Pageants, "Pageants");
            SetEventCategories ((int)DirectoryManager.EventCategories.Education, "Education");
            SetEventCategories ((int)DirectoryManager.EventCategories.Arts, "Arts and Culture");
            SetEventCategories ((int)DirectoryManager.EventCategories.Charity, "Charity/Support Groups");
            SetEventCategories ((int)DirectoryManager.EventCategories.Miscellaneous, "Miscellaneous");
        }

        public void ReadClassifiedValues (IConfig config)
        {
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.Shopping, "Shopping");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.LandRental, "Land Rental");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.PropertyRental, "Property Rental");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.SpecialAttraction, "Special Attraction");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.NewProducts, "New Products");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.Employment, "Employment");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.Wanted, "Wanted");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.Service, "Service");
            AddClassifiedCategory ((int)DirectoryManager.ClassifiedCategories.Personal, "Personal");
        }

        public void SetEventCategories (int value, string categoryName)
        {
            Hashtable hash = new Hashtable ();
            hash ["category_name"] = categoryName;
            hash ["category_id"] = value;
            eventCategories.Add (hash);
        }

        public void AddClassifiedCategory (int ID, string categoryName)
        {
            Hashtable hash = new Hashtable ();
            hash ["category_name"] = categoryName;
            hash ["category_id"] = ID;
            classifiedCategories.Add (hash);
        }

        public Hashtable SetLevel (string firstName, string lastName, string passwd, int level, IPEndPoint clientIP)
        {
            Hashtable response = new Hashtable ();
            response ["success"] = "false";

            if (!m_AllowRemoteSetLoginLevel)
                return response;

            try {
                UserAccount userAcct = m_UserAccountService.GetUserAccount (null, firstName, lastName);
                if (!userAcct.Valid) {
                    MainConsole.Instance.InfoFormat ("[LLogin service]: Set Level failed, user {0} {1} not found",
                                                    firstName, lastName);
                    return response;
                }

                if (userAcct.UserLevel < 200) {
                    MainConsole.Instance.InfoFormat ("[LLogin service]: Set Level failed, reason: user level too low");
                    return response;
                }

                //
                // Authenticate this user
                //
                // We don't support clear passwords here
                //
                string token = m_AuthenticationService.Authenticate (userAcct.PrincipalID, "UserAccount", passwd, 30);
                UUID secureSession = UUID.Zero;
                if ((token == string.Empty) || (token != string.Empty && !UUID.TryParse (token, out secureSession))) {
                    MainConsole.Instance.InfoFormat ("[LLogin service]: SetLevel failed, reason: authentication failed");
                    return response;
                }
            } catch (Exception e) {
                MainConsole.Instance.Error ("[LLogin service]: SetLevel failed, exception " + e);
                return response;
            }

            m_MinLoginLevel = level;
            MainConsole.Instance.InfoFormat ("[LLogin service]: Login level set to {0} by {1} {2}", level, firstName,
                                            lastName);

            response ["success"] = true;
            return response;
        }

        public bool VerifyClient (UUID AgentID, string name, string authType, string passwd)
        {
            MainConsole.Instance.InfoFormat ("[LLogin service]: Login verification request for {0}",
                                            AgentID == UUID.Zero
                                                ? name
                                                : AgentID.ToString ());

            //
            // Get the account and check that it exists
            //
            UserAccount userAcct = AgentID != UUID.Zero
                                      ? m_UserAccountService.GetUserAccount (null, AgentID)
                                      : m_UserAccountService.GetUserAccount (null, name);

            if (!userAcct.Valid) {
                return false;
            }

            IAgentInfo agent = null;
            IAgentConnector agentData = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector> ();
            if (agentData != null) {
                agent = agentData.GetAgent (userAcct.PrincipalID);
                if (agent == null) {
                    agentData.CreateNewAgent (userAcct.PrincipalID);
                    agent = agentData.GetAgent (userAcct.PrincipalID);
                }
            }

            foreach (ILoginModule module in LoginModules) {
                object data;
                if (module.Login (null, userAcct, agent, authType, passwd, out data) != null) {
                    return false;
                }
            }
            return true;
        }

        public LoginResponse Login (UUID AgentID, string Name, string authType, string passwd, string startLocation,
                                   string clientVersion, string channel, string mac, string id0, IPEndPoint clientIP,
                                   Hashtable requestData)
        {
            LoginResponse response;
            UUID session = UUID.Random ();
            UUID secureSession = UUID.Zero;

            // TODO: Make this check better
            //
            // Some TPV's now send their name in Channel instead of clientVersion 
            // while others send a Channel and a clientVersion.

            string realViewer;

            if (channel != "") {
                realViewer = channel + " " + clientVersion;
            } else {
                realViewer = clientVersion;
            }

            MainConsole.Instance.InfoFormat (
                "[LLogin service]: Login request for {0} from {1} with user agent {2} starting in {3}",
                Name, clientIP.Address, realViewer, startLocation);

            UserAccount userAcct = AgentID != UUID.Zero
                                      ? m_UserAccountService.GetUserAccount (null, AgentID)
                                      : m_UserAccountService.GetUserAccount (null, Name);

            // no user but allow anomymouse logins?
            if (!userAcct.Valid && m_AllowAnonymousLogin) {
                m_UserAccountService.CreateUser (Name, passwd.StartsWith ("$1$", StringComparison.Ordinal) ? passwd.Remove (0, 3) : passwd, "");
                userAcct = m_UserAccountService.GetUserAccount (null, Name);
            }
            if (!userAcct.Valid) {
                MainConsole.Instance.InfoFormat ("[LLogin service]: Login failed for user {0}: no account found", Name);
                return LLFailedLoginResponse.AccountProblem;
            }

            if (userAcct.UserLevel < 0) // No allowing anyone less than 0
            {
                MainConsole.Instance.InfoFormat (
                    "[LLogin service]: Login failed for user {0}, reason: user is banned",
                    userAcct.Name);
                return LLFailedLoginResponse.PermanentBannedProblem;
            }

            if (userAcct.UserLevel < m_MinLoginLevel) {
                MainConsole.Instance.InfoFormat (
                    "[LLogin service]: Login failed for user {1}, reason: login is blocked for user level {0}",
                    userAcct.UserLevel, userAcct.Name);
                return LLFailedLoginResponse.LoginBlockedProblem;
            }

            IAgentInfo agent = null;
            IAgentConnector agentData = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector> ();
            if (agentData != null) {
                agent = agentData.GetAgent (userAcct.PrincipalID);
                if (agent == null) {
                    agentData.CreateNewAgent (userAcct.PrincipalID);
                    agent = agentData.GetAgent (userAcct.PrincipalID);
                }
            } else {
                MainConsole.Instance.ErrorFormat ("[LLogin service]: Login failed for user {1}, reason: {0}",
                                                 userAcct.Name, "Unable to retrieve agen connector");
                return LLFailedLoginResponse.GridProblem;
            }

            requestData ["ip"] = clientIP.ToString ();
            foreach (ILoginModule module in LoginModules) {
                object data;
                if ((response = module.Login (requestData, userAcct, agent, authType, passwd, out data)) != null) {
                    MainConsole.Instance.InfoFormat (
                        "[LLogin service]: Login failed for user {1}, reason: {0}",
                        (data != null ? data.ToString () : (response is LLFailedLoginResponse) ? (response as LLFailedLoginResponse).Value : "Unknown"), userAcct.Name);
                    return response;
                }
                if (data != null)
                    secureSession = (UUID)data; // TODO: NEED TO FIND BETTER WAY TO GET THIS DATA
            }

            try {
                string DisplayName = userAcct.Name;
                AvatarAppearance avappearance = null;
                IProfileConnector profileData = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ();

                //
                // Get the user's inventory
                //
                if (m_RequireInventory && m_InventoryService == null) {
                    MainConsole.Instance.WarnFormat (
                        "[LLogin service]: Login failed for user {0}, reason: inventory service not set up",
                        userAcct.Name);
                    return LLFailedLoginResponse.InventoryProblem;
                }
                List<InventoryFolderBase> inventorySkel = m_InventoryService.GetInventorySkeleton (userAcct.PrincipalID);
                if (m_RequireInventory && ((inventorySkel == null) || (inventorySkel.Count == 0))) {
                    List<InventoryItemBase> defaultItems;
                    m_InventoryService.CreateUserInventory (userAcct.PrincipalID, m_DefaultUserAvatarArchive == "",
                                                           out defaultItems);
                    inventorySkel = m_InventoryService.GetInventorySkeleton (userAcct.PrincipalID);
                    if (m_RequireInventory && ((inventorySkel == null) || (inventorySkel.Count == 0))) {
                        MainConsole.Instance.InfoFormat (
                            "[LLogin service]: Login failed for user {0}, reason: unable to retrieve user inventory",
                            userAcct.Name);
                        return LLFailedLoginResponse.InventoryProblem;
                    }
                    if (defaultItems.Count > 0) {
                        avappearance = new AvatarAppearance (userAcct.PrincipalID);
                        avappearance.SetWearable ((int)WearableType.Shape,
                                                 new AvatarWearable (defaultItems [0].ID, defaultItems [0].AssetID));
                        avappearance.SetWearable ((int)WearableType.Skin,
                                                 new AvatarWearable (defaultItems [1].ID, defaultItems [1].AssetID));
                        avappearance.SetWearable ((int)WearableType.Hair,
                                                 new AvatarWearable (defaultItems [2].ID, defaultItems [2].AssetID));
                        avappearance.SetWearable ((int)WearableType.Eyes,
                                                 new AvatarWearable (defaultItems [3].ID, defaultItems [3].AssetID));
                        avappearance.SetWearable ((int)WearableType.Shirt,
                                                 new AvatarWearable (defaultItems [4].ID, defaultItems [4].AssetID));
                        avappearance.SetWearable ((int)WearableType.Pants,
                                                 new AvatarWearable (defaultItems [5].ID, defaultItems [5].AssetID));
                        m_AvatarService.SetAppearance (userAcct.PrincipalID, avappearance);
                    }
                }

                if (profileData != null) {
                    IUserProfileInfo UPI = profileData.GetUserProfile (userAcct.PrincipalID);
                    if (UPI == null) {
                        profileData.CreateNewProfile (userAcct.PrincipalID);
                        UPI = profileData.GetUserProfile (userAcct.PrincipalID);
                        UPI.AArchiveName = m_DefaultUserAvatarArchive;
                        UPI.IsNewUser = true;
                        //profileData.UpdateUserProfile(UPI); //It gets hit later by the next thing
                    }
                    //Find which is set, if any
                    string archiveName = (UPI.AArchiveName != "" && UPI.AArchiveName != " ")
                                             ? UPI.AArchiveName
                                             : m_DefaultUserAvatarArchive;
                    if (UPI.IsNewUser && archiveName != "") {
                        AvatarArchive arch = m_ArchiveService.LoadAvatarArchive (archiveName, userAcct.PrincipalID);
                        UPI.AArchiveName = "";
                        if (arch != null) {
                            avappearance = arch.Appearance;
                            m_AvatarService.SetAppearance (userAcct.PrincipalID, avappearance);
                            //Must reload this, as we created a new folder
                            inventorySkel = m_InventoryService.GetInventorySkeleton (userAcct.PrincipalID);
                        }
                    }
                    if (UPI.IsNewUser) {
                        UPI.IsNewUser = false;
                        profileData.UpdateUserProfile (UPI);
                    }
                    if (UPI.DisplayName != "")
                        DisplayName = UPI.DisplayName;
                }

                // Get active gestures
                List<InventoryItemBase> gestures = m_InventoryService.GetActiveGestures (userAcct.PrincipalID);
                //MainConsole.Instance.DebugFormat("[LLogin service]: {0} active gestures", gestures.Count);

                //Now get the logged in status, then below make sure to kill the previous agent if we crashed before
                UserInfo guinfo = m_agentInfoService.GetUserInfo (userAcct.PrincipalID.ToString ());
                //
                // Clear out any existing CAPS the user may have
                //
                if (m_CapsService != null) {
                    IAgentProcessing agentProcessor = m_registry.RequestModuleInterface<IAgentProcessing> ();
                    if (agentProcessor != null) {
                        IClientCapsService clientCaps = m_CapsService.GetClientCapsService (userAcct.PrincipalID);
                        if (clientCaps != null) {
                            IRegionClientCapsService rootRegionCaps = clientCaps.GetRootCapsService ();
                            if (rootRegionCaps != null)
                                agentProcessor.LogoutAgent (rootRegionCaps, !m_AllowDuplicateLogin);
                        }
                    } else
                        m_CapsService.RemoveCAPS (userAcct.PrincipalID);
                }

                //
                // Change Online status and get the home region
                //
                GridRegion home = null;
                if (guinfo != null && (guinfo.HomeRegionID != UUID.Zero) && m_GridService != null) {
                    home = m_GridService.GetRegionByUUID (userAcct.AllScopeIDs, guinfo.HomeRegionID);
                    if (home == null)       // the specified home region is not available?
                        guinfo = null;
                }

                if (guinfo == null || guinfo.HomeRegionID == UUID.Zero)     // Give them a default home and last
                {
                    bool positionSet = false;
                    string regionName = "none";

                    if (guinfo == null)
                        guinfo = new UserInfo { UserID = userAcct.PrincipalID.ToString () };

                    if (m_GridService != null) {
                        if (m_DefaultHomeRegion != "") {
                            GridRegion homeRegion = m_GridService.GetRegionByName (userAcct.AllScopeIDs, m_DefaultHomeRegion);
                            if (homeRegion != null)
                                guinfo.HomeRegionID = guinfo.CurrentRegionID = homeRegion.RegionID;
                            guinfo.HomePosition = guinfo.CurrentPosition = m_DefaultHomeRegionPos;
                            positionSet = true;
                            regionName = homeRegion.RegionName;
                        }

                        // if we have not found our home region...
                        if (guinfo.HomeRegionID == UUID.Zero) {
                            List<GridRegion> defaultRegions = m_GridService.GetDefaultRegions (userAcct.AllScopeIDs);
                            if (defaultRegions.Count > 0) {
                                guinfo.HomeRegionID = guinfo.CurrentRegionID = defaultRegions [0].RegionID;
                                regionName = defaultRegions [0].RegionName;
                            } else {
                                List<GridRegion> fallbackRegions = m_GridService.GetFallbackRegions (userAcct.AllScopeIDs, 0, 0);
                                if (fallbackRegions.Count > 0) {
                                    guinfo.HomeRegionID = guinfo.CurrentRegionID = fallbackRegions [0].RegionID;
                                    regionName = fallbackRegions [0].RegionName;
                                } else {
                                    List<GridRegion> safeRegions = m_GridService.GetSafeRegions (userAcct.AllScopeIDs, 0, 0);
                                    if (safeRegions.Count > 0) {
                                        guinfo.HomeRegionID = guinfo.CurrentRegionID = safeRegions [0].RegionID;
                                        regionName = safeRegions [0].RegionName;
                                    }
                                }
                            }
                        }
                    }

                    if (!positionSet) {
                        guinfo.CurrentPosition = guinfo.HomePosition = new Vector3 (128, 128, 25);
                        guinfo.HomeLookAt = guinfo.CurrentLookAt = new Vector3 (0, 0, 0);
                    }
                    m_agentInfoService.SetHomePosition (guinfo.UserID, guinfo.HomeRegionID,
                                                        guinfo.HomePosition, guinfo.HomeLookAt);

                    MainConsole.Instance.Info ("[LLLoginService]: User did not have a home, set to " +
                                              (guinfo.HomeRegionID == UUID.Zero ? "(no region found)" : regionName));
                }

                //
                // Find the destination region/grid
                //
                string where = string.Empty;
                Vector3 position = Vector3.Zero;
                Vector3 lookAt = Vector3.Zero;
                TeleportFlags tpFlags = TeleportFlags.ViaLogin;
                GridRegion destination = FindDestination (userAcct, guinfo, session, startLocation, home, out tpFlags,
                                                         out where, out position, out lookAt);
                if (destination == null) {
                    MainConsole.Instance.InfoFormat (
                        "[LLogin service]: Login failed for user {0}, reason: destination not found", userAcct.Name);
                    return LLFailedLoginResponse.DeadRegionProblem;
                }

                #region Appearance

                //
                // Get the avatar
                //
                if (m_AvatarService != null) {
                    bool loadedArchive;
                    avappearance = m_AvatarService.GetAndEnsureAppearance (userAcct.PrincipalID, m_DefaultUserAvatarArchive, out loadedArchive);
                    if (loadedArchive)
                        //Must reload this, as we created a new folder
                        inventorySkel = m_InventoryService.GetInventorySkeleton (userAcct.PrincipalID);
                } else
                    avappearance = new AvatarAppearance (userAcct.PrincipalID);

                if ((m_forceUserToWearFolderName != "") && (m_forceUserToWearFolderOwnerUUID.Length == 36)) {
                    UUID userThatOwnersFolder;
                    if (UUID.TryParse (m_forceUserToWearFolderOwnerUUID, out userThatOwnersFolder)) {
                        avappearance = WearFolder (avappearance, userAcct.PrincipalID, userThatOwnersFolder);
                    }
                }

                //Makes sure that all links are properly placed in the current outfit folder for v2 viewers
                FixCurrentOutFitFolder (userAcct.PrincipalID, ref avappearance);

                #endregion

                List<UUID> friendsToInform = new List<UUID> ();
                if (m_FriendsService != null)
                    friendsToInform = m_FriendsService.GetFriendOnlineStatuses (userAcct.PrincipalID, true);

                //
                // Instantiate/get the simulation interface and launch an agent at the destination
                //
                string reason = "", seedCap = "";
                AgentCircuitData aCircuit = LaunchAgentAtGrid (destination, tpFlags, userAcct, session,
                                                              secureSession, position, where,
                                                              clientIP, friendsToInform, out where, out reason, out seedCap,
                                                              out destination);

                if (aCircuit == null) {
                    MainConsole.Instance.InfoFormat ("[LLogin service]: Login failed for user {1}, reason: {0}", reason,
                                                    userAcct.Name);
                    return new LLFailedLoginResponse (LoginResponseEnum.InternalError, reason, false);
                }

                // Get Friends list 
                List<FriendInfo> friendsList = new List<FriendInfo> ();
                if (m_FriendsService != null)
                    friendsList = m_FriendsService.GetFriends (userAcct.PrincipalID);

                // Set them as logged in now, they are ready, and fire the logged in event now, as we're all done
                m_agentInfoService.SetLastPosition (userAcct.PrincipalID.ToString (), destination.RegionID, position,
                                                   lookAt, destination.ServerURI);
                m_agentInfoService.SetLoggedIn (userAcct.PrincipalID.ToString (), true, destination.RegionID,
                                               destination.ServerURI);
                m_agentInfoService.FireUserStatusChangeEvent (userAcct.PrincipalID.ToString (), true, destination.RegionID);

                //
                // Finally, fill out the response and return it
                //
                string MaturityRating = "A";
                string MaxMaturity = "A";
                if (agent != null) {
                    MaturityRating = agent.MaturityRating == 0
                                         ? "P"
                                         : agent.MaturityRating == 1 ? "M" : "A";
                    MaxMaturity = agent.MaxMaturity == 0
                                      ? "P"
                                      : agent.MaxMaturity == 1 ? "M" : "A";
                }


                ArrayList eventNotifications = new ArrayList ();
                BuildEventNotifications (userAcct.PrincipalID, ref eventNotifications);

                if (m_FriendsService != null)
                    m_FriendsService.SendFriendOnlineStatuses (userAcct.PrincipalID, true);

                response = new LLLoginResponse (userAcct, aCircuit, guinfo, destination, inventorySkel,
                                               friendsList.ToArray (), m_InventoryService, m_LibraryService,
                                               where, startLocation, position, lookAt, gestures, home, clientIP,
                                               MaxMaturity, MaturityRating,
                                               eventCategories, eventNotifications, classifiedCategories, seedCap,
                                               m_config, DisplayName, avappearance.Serial.ToString (), m_registry.RequestModuleInterface<IGridInfo> ());

                MainConsole.Instance.InfoFormat (
                    "[LLogin service]: All clear. Sending response to client for login to region " +
                    destination.RegionName + " at " + position + ".");

                return response;
            } catch (Exception e) {
                MainConsole.Instance.WarnFormat ("[LLogin service]: Exception processing login for {0} : {1}", Name, e);
                if (userAcct.Valid) {
                    // Revert their logged in status if we got that far
                    m_agentInfoService.SetLoggedIn (userAcct.PrincipalID.ToString (), false, UUID.Zero, "");
                }

                return LLFailedLoginResponse.InternalError;
            }
        }

        void BuildEventNotifications (UUID principalID, ref ArrayList eventNotifications)
        {
            IDirectoryServiceConnector dirService =
                Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
            if (dirService == null)
                return;
            List<EventData> events = dirService.GetEventNotifications (principalID);

            foreach (EventData ev in events) {
                Hashtable hash = new Hashtable ();
                hash ["event_id"] = ev.eventID;
                hash ["event_name"] = ev.name;
                hash ["event_desc"] = ev.description;
                hash ["event_date"] = ev.date;
                hash ["grid_x"] = ev.globalPos.X;
                hash ["grid_y"] = ev.globalPos.Y;
                hash ["x_region"] = ev.regionPos.X;
                hash ["y_region"] = ev.regionPos.Y;
                eventNotifications.Add (hash);
            }
        }

        protected void GetRegionLandingPoint (GridRegion region, out Vector3 landingPoint, out Vector3 lookAt)
        {
            // determine and return the region's landing point if set
            landingPoint = new Vector3 (128, 128, 25);      // assume the 'standard'
            lookAt = new Vector3 (0, 1, 0);

            if (region == null)
                return;

            // check if there is a telehub for this region
            var regionConnector = Framework.Utilities.DataManager.RequestPlugin<IRegionConnector> ();
            Telehub telehub = regionConnector.FindTelehub (region.RegionID, region.RegionHandle);
            if (telehub != null) {
                if (telehub.SpawnPos.Count == 0) {
                    landingPoint = new Vector3 (telehub.TelehubLocX, telehub.TelehubLocY, telehub.TelehubLocZ);
                    lookAt = new Vector3 (telehub.TelehubRotX, telehub.TelehubRotY, telehub.TelehubRotZ);

                    return;

                }
                int LastTelehubNum = 0;
                if (!lastTelehub.TryGetValue (region.RegionID, out LastTelehubNum))
                    LastTelehubNum = 0;
                landingPoint = telehub.SpawnPos [LastTelehubNum] +
                                        new Vector3 (telehub.TelehubLocX, telehub.TelehubLocY, telehub.TelehubLocZ);
                lookAt = telehub.SpawnPos [LastTelehubNum] +
                                        new Vector3 (telehub.TelehubRotX, telehub.TelehubRotY, telehub.TelehubRotZ);
                LastTelehubNum++;
                if (LastTelehubNum == telehub.SpawnPos.Count)
                    LastTelehubNum = 0;
                lastTelehub [region.RegionID] = LastTelehubNum;

                return;
            }

            // no telehub, check for a set landing point near the 'standard' - might need to modify this for multiple parcels - greythane-
            IDirectoryServiceConnector DSC = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
            if (DSC != null) {
                LandData landdata = DSC.GetParcelInfo (region.RegionID, (int)landingPoint.X, (int)landingPoint.Y);
                if (landdata != null) {
                    if ((landdata.LandingType == (int)LandingType.LandingPoint) && (landdata.UserLocation != Vector3.Zero)) {
                        // we have a landing point specified, use it
                        landingPoint = landdata.UserLocation;
                        lookAt = landdata.UserLookAt;
                    }
                }
            }
        }


        protected GridRegion FindDestination (UserAccount account, UserInfo pinfo, UUID sessionID, string startLocation,
                                             GridRegion home, out TeleportFlags tpFlags, out string where,
                                             out Vector3 position, out Vector3 lookAt)
        {
            where = "home";
            position = new Vector3 (128, 128, 25);
            lookAt = new Vector3 (0, 1, 0);
            tpFlags = TeleportFlags.ViaLogin;

            if (m_GridService == null)
                return null;

            if (startLocation.Equals ("home")) {
                tpFlags |= TeleportFlags.ViaLandmark;
                // logging into home region
                if (pinfo == null)
                    return null;

                GridRegion region = null;

                bool tryDefaults = false;

                if (home == null) {
                    MainConsole.Instance.WarnFormat (
                        "[LLogin service]: User {0} {1} tried to login to a 'home' start location but they have none set",
                        account.FirstName, account.LastName);

                    tryDefaults = true;
                } else {
                    region = home;

                    position = pinfo.HomePosition;
                    lookAt = pinfo.HomeLookAt;
                }

                if (tryDefaults) {
                    tpFlags &= ~TeleportFlags.ViaLandmark;
                    List<GridRegion> defaultRegions = m_GridService.GetDefaultRegions (account.AllScopeIDs);
                    if (defaultRegions.Count > 0) {
                        region = defaultRegions [0];
                        where = "safe";
                    } else {
                        // Try fo a fallback region
                        List<GridRegion> fallbackRegions = m_GridService.GetFallbackRegions (account.AllScopeIDs, 0, 0);
                        if (fallbackRegions.Count > 0) {
                            region = fallbackRegions [0];
                            where = "safe";
                        } else {
                            // Try to find any safe region
                            List<GridRegion> safeRegions = m_GridService.GetSafeRegions (account.AllScopeIDs, 0, 0);
                            if (safeRegions.Count > 0) {
                                region = safeRegions [0];
                                where = "safe";
                            } else {
                                MainConsole.Instance.WarnFormat (
                                    "[LLogin service]: User {0} {1} does not have a valid home and this grid does not have default locations. Attempting to find random region",
                                    account.FirstName, account.LastName);
                                List<GridRegion> allRegions = m_GridService.GetRegionsByName (account.AllScopeIDs, "", 0, 1);
                                if (allRegions.Count > 0) {
                                    region = allRegions [0];
                                    where = "safe";
                                }
                            }
                        }
                    }
                }

                // get landing point if set
                GetRegionLandingPoint (region, out position, out lookAt);
                return region;
            }

            if (startLocation.Equals ("last")) {
                tpFlags |= TeleportFlags.ViaLandmark;
                // logging into last visited region
                where = "last";

                if (pinfo == null)
                    return null;

                GridRegion region = null;

                if (pinfo.CurrentRegionID.Equals (UUID.Zero) ||
                    (region = m_GridService.GetRegionByUUID (account.AllScopeIDs, pinfo.CurrentRegionID)) == null) {
                    // Current region appears to be invalid...

                    tpFlags &= ~TeleportFlags.ViaLandmark;
                    List<GridRegion> fbRegions = m_GridService.GetDefaultRegions (account.AllScopeIDs);
                    if (fbRegions.Count > 0) {
                        region = fbRegions [0];
                        where = "safe";
                    } else {
                        fbRegions = m_GridService.GetFallbackRegions (account.AllScopeIDs, 0, 0);
                        if (fbRegions.Count > 0) {
                            region = fbRegions [0];
                            where = "safe";
                        } else {
                            fbRegions = m_GridService.GetSafeRegions (account.AllScopeIDs, 0, 0);
                            if (fbRegions.Count > 0) {
                                region = fbRegions [0];
                                where = "safe";
                            }
                        }
                    }

                    // get landing point if set
                    GetRegionLandingPoint (region, out position, out lookAt);
                    return region;
                }

                // we have our last region
                position = pinfo.CurrentPosition;
                if (position.X < 0)
                    position.X = 0;
                if (position.Y < 0)
                    position.Y = 0;
                if (position.Z < 0)
                    position.Z = 0;
                if (position.X > region.RegionSizeX)
                    position.X = region.RegionSizeX;
                if (position.Y > region.RegionSizeY)
                    position.Y = region.RegionSizeY;


                lookAt = pinfo.CurrentLookAt;

                return region;
            } else {
                // free uri form
                // e.g. New Moon&135&46  New Moon@whitecoregrid.org:8002&153&34
                where = "url";
                Regex reURI = new Regex (@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");
                Match uriMatch = reURI.Match (startLocation);
                position = new Vector3 (float.Parse (uriMatch.Groups ["x"].Value, Culture.NumberFormatInfo),
                                       float.Parse (uriMatch.Groups ["y"].Value, Culture.NumberFormatInfo),
                                       float.Parse (uriMatch.Groups ["z"].Value, Culture.NumberFormatInfo));

                string regionName = uriMatch.Groups ["region"].ToString ();
                if (!regionName.Contains ("@")) {
                    List<GridRegion> regions = m_GridService.GetRegionsByName (account.AllScopeIDs, regionName, 0, 1);
                    if (regions.Count > 0) {
                        // get landing point if set
                        GetRegionLandingPoint (regions [0], out position, out lookAt);
                        return regions [0];
                    }
                       
                    // could not locate requested region
                    MainConsole.Instance.InfoFormat (
                        "[LLogin service]: Got Custom Login URI {0}, can't locate region {1}. Trying defaults.",
                        startLocation, regionName);
                    
                    List<GridRegion> defaults = m_GridService.GetDefaultRegions (account.AllScopeIDs);
                    if (defaults.Count > 0) {
                        where = "safe";
                        GetRegionLandingPoint (defaults [0], out position, out lookAt);

                        return defaults [0];
                    }

                    List<GridRegion> fallbacks = m_GridService.GetFallbackRegions (account.AllScopeIDs, 0, 0);
                    if (fallbacks.Count > 0) {
                        where = "safe";
                        GetRegionLandingPoint (fallbacks [0], out position, out lookAt);
                        return fallbacks [0];
                    }

                    // Try to find any safe region
                    List<GridRegion> safeRegions = m_GridService.GetSafeRegions (account.AllScopeIDs, 0, 0);
                    if (safeRegions.Count > 0) {
                        where = "safe";
                        GetRegionLandingPoint (safeRegions [0], out position, out lookAt);
                        return safeRegions [0];
                    }
                    MainConsole.Instance.InfoFormat (
                        "[LLogin service]: Got Custom Login URI {0}, Grid does not have any available regions.",
                        startLocation);
                    return null;


                }

                //This is so that you can login to other grids via IWC (or HG), example"RegionTest@testingserver.com:8002". All this really needs to do is inform the other grid that we have a user who wants to connect. IWC allows users to login by default to other regions (without the host names), but if one is provided and we don't have a link, we need to create one here.
                string [] parts = regionName.Split (new char [] { '@' });
                if (parts.Length < 2) {
                    MainConsole.Instance.InfoFormat (
                        "[LLogin service]: Got Custom Login URI {0}, can't locate region {1}",
                        startLocation, regionName);
                    return null;
                }
                // Valid specification of a remote grid

                regionName = parts [0];
                // Try now that we removed the domain locator
                GridRegion region = m_GridService.GetRegionByName (account.AllScopeIDs, regionName);
                if (region != null && region.RegionName == regionName) {
                    // Make sure the region name is right too... it could just be a similar name
                    GetRegionLandingPoint (region, out position, out lookAt);
                    return region;
                }

                List<GridRegion> defaultRegions = m_GridService.GetDefaultRegions (account.AllScopeIDs);
                if (defaultRegions.Count > 0) {
                    where = "safe";
                    GetRegionLandingPoint (defaultRegions [0], out position, out lookAt);
                    return defaultRegions [0];
                } else {
                    List<GridRegion> fallbacks = m_GridService.GetFallbackRegions (account.AllScopeIDs, 0, 0);
                    if (fallbacks.Count > 0) {
                        where = "safe";
                        GetRegionLandingPoint (fallbacks [0], out position, out lookAt);
                        return fallbacks [0];
                    } else {
                        // Try to find any safe region
                        List<GridRegion> safeRegions = m_GridService.GetSafeRegions (account.AllScopeIDs, 0, 0);
                        if (safeRegions.Count > 0) {
                            where = "safe";
                            GetRegionLandingPoint (safeRegions [0], out position, out lookAt);
                            return safeRegions [0];
                        }
                        MainConsole.Instance.InfoFormat (
                            "[LLogin service]: Got Custom Login URI {0}, Grid does not have any available regions.",
                            startLocation);
                        return null;
                    }
                }
            }
        }

        protected AgentCircuitData LaunchAgentAtGrid (GridRegion destination, TeleportFlags tpFlags, UserAccount account,
                                                     UUID session, UUID secureSession, Vector3 position,
                                                     string currentWhere,
                                                     IPEndPoint clientIP, List<UUID> friendsToInform, out string where, out string reason,
                                                     out string seedCap, out GridRegion dest)
        {
            where = currentWhere;
            reason = string.Empty;
            uint circuitCode;
            AgentCircuitData aCircuit;
            dest = destination;

            #region Launch Agent

            circuitCode = (uint)Util.RandomClass.Next ();
            aCircuit = MakeAgent (destination, account, session, secureSession, circuitCode, position,
                                 clientIP);
            aCircuit.TeleportFlags = (uint)tpFlags;
            MainConsole.Instance.DebugFormat ("[LoginService]: Attempting to log {0} into {1} at {2}...", account.Name, destination.RegionName, destination.ServerURI);
            LoginAgentArgs args = m_registry.RequestModuleInterface<IAgentProcessing> ().LoginAgent (destination, aCircuit, friendsToInform);
            aCircuit.CachedUserInfo = args.CircuitData.CachedUserInfo;
            aCircuit.RegionUDPPort = args.CircuitData.RegionUDPPort;

            reason = args.Reason;
            seedCap = args.SeedCap;
            bool success = args.Success;
            if (!success && m_GridService != null) {
                MainConsole.Instance.DebugFormat ("[LoginService]: Failed to log {0} into {1} at {2}...", account.Name, destination.RegionName, destination.ServerURI);
                // Remove the landmark flag (landmark is used for ignoring the landing points in the region)
                aCircuit.TeleportFlags &= ~(uint)TeleportFlags.ViaLandmark;
                m_GridService.SetRegionUnsafe (destination.RegionID);

                // Make sure the client knows this isn't where they wanted to land
                where = "safe";

                // Try the default regions
                List<GridRegion> defaultRegions = m_GridService.GetDefaultRegions (account.AllScopeIDs);
                if (defaultRegions.Count > 0) {
                    success = TryFindGridRegionForAgentLogin (defaultRegions, account,
                                                              session, secureSession, circuitCode, position,
                                                              clientIP, aCircuit, friendsToInform,
                                                              out seedCap, out reason, out dest);
                }
                if (!success) {
                    // Try the fallback regions
                    List<GridRegion> fallbacks = m_GridService.GetFallbackRegions (account.AllScopeIDs,
                                                                                   destination.RegionLocX,
                                                                                   destination.RegionLocY);
                    if (fallbacks.Count > 0) {
                        success = TryFindGridRegionForAgentLogin (fallbacks, account,
                                                                  session, secureSession, circuitCode,
                                                                  position,
                                                                  clientIP, aCircuit, friendsToInform,
                                                                  out seedCap, out reason, out dest);
                    }
                    if (!success) {
                        // Try to find any safe region
                        List<GridRegion> safeRegions = m_GridService.GetSafeRegions (account.AllScopeIDs,
                                                                                     destination.RegionLocX,
                                                                                     destination.RegionLocY);
                        if (safeRegions.Count > 0) {
                            success = TryFindGridRegionForAgentLogin (safeRegions, account,
                                                                      session, secureSession, circuitCode,
                                                                      position, clientIP, aCircuit, friendsToInform,
                                                                      out seedCap, out reason, out dest);
                            if (!success)
                                reason = "No Region Found";
                        }
                    }
                }
            }

            #endregion

            if (success) {
                MainConsole.Instance.DebugFormat ("[LoginService]: Successfully logged {0} into {1} at {2}...", account.Name, destination.RegionName, destination.ServerURI);
                //Set the region to safe since we got there
                m_GridService.SetRegionSafe (destination.RegionID);
                return aCircuit;
            }
            return null;
        }

        protected bool TryFindGridRegionForAgentLogin (List<GridRegion> regions, UserAccount account,
                                                      UUID session, UUID secureSession,
                                                      uint circuitCode, Vector3 position,
                                                      IPEndPoint clientIP, AgentCircuitData aCircuit, List<UUID> friendsToInform,
                                                      out string seedCap, out string reason, out GridRegion destination)
        {
            LoginAgentArgs args = null;
            foreach (GridRegion r in regions) {
                if (r == null)
                    continue;
                MainConsole.Instance.DebugFormat ("[LoginService]: Attempting to log {0} into {1} at {2}...", account.Name, r.RegionName, r.ServerURI);
                args = m_registry.RequestModuleInterface<IAgentProcessing> ().
                                  LoginAgent (r, aCircuit, friendsToInform);
                if (args.Success) {
                    //aCircuit = MakeAgent(r, account, session, secureSession, circuitCode, position, clientIP);
                    MakeAgent (r, account, session, secureSession, circuitCode, position, clientIP);
                    destination = r;
                    reason = args.Reason;
                    seedCap = args.SeedCap;
                    return true;
                }
                m_GridService.SetRegionUnsafe (r.RegionID);
            }
            if (args != null) {
                seedCap = args.SeedCap;
                reason = args.Reason;
            } else {
                seedCap = "";
                reason = "";
            }
            destination = null;
            return false;
        }

        protected AgentCircuitData MakeAgent (GridRegion region, UserAccount account,
                                             UUID session, UUID secureSession, uint circuit,
                                             Vector3 position, IPEndPoint clientIP)
        {
            return new AgentCircuitData {
                AgentID = account.PrincipalID,
                IsChildAgent = false,
                CircuitCode = circuit,
                SecureSessionID = secureSession,
                SessionID = session,
                StartingPosition = position,
                IPAddress = clientIP.Address.ToString ()
            };
        }

        #region Console Commands

        protected void RegisterCommands ()
        {
            if (MainConsole.Instance == null)
                return;
            MainConsole.Instance.Commands.AddCommand (
                "login level",
                "login level <level>",
                "Set the minimum user level to log in",
                HandleLoginCommand, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "login reset",
                "login reset",
                "Reset the login level to allow all users",
                HandleLoginCommand, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "login text",
                "login text <text>",
                "Set the text users will see on login",
                HandleLoginCommand, false, true);
        }

        protected void HandleLoginCommand (IScene scene, string [] cmd)
        {
            string subcommand = cmd [1];

            switch (subcommand) {
            case "level":
                // Set the minimum level to allow login 
                // Useful to allow grid update without worrying about users.
                // or fixing critical issues
                //
                if (cmd.Length > 2)
                    int.TryParse (cmd [2], out m_MinLoginLevel);
                break;
            case "reset":
                m_MinLoginLevel = 0;
                break;
            case "text":
                if (cmd.Length > 2)
                    m_WelcomeMessage = cmd [2];
                break;
            }
        }

        #endregion

        #region Force Wear

        public AvatarAppearance WearFolder (AvatarAppearance avappearance, UUID user, UUID folderOwnerID)
        {
            InventoryFolderBase Folder2Wear = m_InventoryService.GetFolderByOwnerAndName (folderOwnerID, m_forceUserToWearFolderName);
            if (Folder2Wear != null) {
                List<InventoryItemBase> itemsInFolder = m_InventoryService.GetFolderItems (UUID.Zero, Folder2Wear.ID);

                InventoryFolderBase appearanceFolder = m_InventoryService.GetFolderForType (user, InventoryType.Wearable, FolderType.Clothing);
                InventoryFolderBase folderForAppearance = new InventoryFolderBase (UUID.Random (), "GridWear", user, -1,
                                                                                  appearanceFolder.ID, 1);
                List<InventoryFolderBase> userFolders = m_InventoryService.GetFolderFolders (user, appearanceFolder.ID);
                bool alreadyThere = false;
                List<UUID> items2RemoveFromAppearence = new List<UUID> ();
                List<UUID> toDelete = new List<UUID> ();

                foreach (InventoryFolderBase folder in userFolders) {
                    if (folder.Name == folderForAppearance.Name) {
                        List<InventoryItemBase> itemsInCurrentFolder = m_InventoryService.GetFolderItems (UUID.Zero, folder.ID);
                        foreach (InventoryItemBase itemBase in itemsInCurrentFolder) {
                            items2RemoveFromAppearence.Add (itemBase.AssetID);
                            items2RemoveFromAppearence.Add (itemBase.ID);
                            toDelete.Add (itemBase.ID);
                        }
                        folderForAppearance = folder;
                        alreadyThere = true;
                        m_InventoryService.DeleteItems (user, toDelete);
                        break;
                    }
                }


                if (!alreadyThere)
                    m_InventoryService.AddFolder (folderForAppearance);
                else {
                    // we have to remove all the old items if they are currently wearing them
                    for (int i = 0; i < avappearance.Wearables.Length; i++) {
                        AvatarWearable wearable = avappearance.Wearables [i];
                        for (int ii = 0; ii < wearable.Count; ii++) {
                            if (items2RemoveFromAppearence.Contains (wearable [ii].ItemID)) {
                                avappearance.Wearables [i] = AvatarWearable.DefaultWearables [i];
                                break;
                            }
                        }
                    }

                    List<AvatarAttachment> attachments = avappearance.GetAttachments ();
                    foreach (AvatarAttachment attachment in attachments) {
                        if ((items2RemoveFromAppearence.Contains (attachment.AssetID)) ||
                            (items2RemoveFromAppearence.Contains (attachment.ItemID))) {
                            avappearance.DetachAttachment (attachment.ItemID);
                        }
                    }
                }

                // ok, now we have a empty folder, lets add the items 
                foreach (InventoryItemBase itemBase in itemsInFolder) {
                    InventoryItemBase newcopy = m_InventoryService.InnerGiveInventoryItem (user, folderOwnerID, itemBase,
                                                                                          folderForAppearance.ID,
                                                                                          true, true);

                    if (newcopy.InvType == (int)InventoryType.Object) {
                        byte [] attobj = m_AssetService.GetData (newcopy.AssetID.ToString ());

                        if (attobj != null) {
                            string xmlData = Utils.BytesToString (attobj);
                            XmlDocument doc = new XmlDocument ();
                            try {
                                doc.LoadXml (xmlData);
                            } catch {
                                continue;
                            }

                            if (doc.FirstChild.OuterXml.StartsWith ("<groups>", StringComparison.Ordinal) ||
                                (doc.FirstChild.NextSibling != null &&
                                 doc.FirstChild.NextSibling.OuterXml.StartsWith ("<groups>", StringComparison.Ordinal)))
                                continue;

                            string xml;
                            if ((doc.FirstChild.NodeType == XmlNodeType.XmlDeclaration) &&
                                (doc.FirstChild.NextSibling != null))
                                xml = doc.FirstChild.NextSibling.OuterXml;
                            else
                                xml = doc.FirstChild.OuterXml;
                            doc.LoadXml (xml);

                            if (doc.DocumentElement == null) continue;

                            XmlNodeList xmlNodeList = doc.DocumentElement.SelectNodes ("//State");
                            int attchspot;
                            if ((xmlNodeList != null) && (int.TryParse (xmlNodeList [0].InnerText, out attchspot))) {
                                AvatarAttachment a = new AvatarAttachment (attchspot, newcopy.ID, newcopy.AssetID);
                                Dictionary<int, List<AvatarAttachment>> ac = avappearance.Attachments;

                                if (!ac.ContainsKey (attchspot))
                                    ac [attchspot] = new List<AvatarAttachment> ();

                                ac [attchspot].Add (a);
                                avappearance.Attachments = ac;
                            }
                        }
                    }
                    m_InventoryService.AddItem (newcopy);
                }
            }
            return avappearance;
        }

        public void FixCurrentOutFitFolder (UUID user, ref AvatarAppearance avappearance)
        {
            InventoryFolderBase CurrentOutFitFolder = m_InventoryService.GetFolderForType (user, 0, FolderType.CurrentOutfit);
            if (CurrentOutFitFolder == null) return;
            List<InventoryItemBase> ic = m_InventoryService.GetFolderItems (user, CurrentOutFitFolder.ID);
            List<UUID> brokenLinks = new List<UUID> ();
            List<UUID> OtherStuff = new List<UUID> ();
            foreach (var i in ic) {
                InventoryItemBase linkedItem;
                if ((linkedItem = m_InventoryService.GetItem (user, i.AssetID)) == null)
                    brokenLinks.Add (i.ID);
                else if (linkedItem.ID == AvatarWearable.DEFAULT_EYES_ITEM ||
                         linkedItem.ID == AvatarWearable.DEFAULT_SHAPE_ITEM ||
                         linkedItem.ID == AvatarWearable.DEFAULT_HAIR_ITEM ||
                         linkedItem.ID == AvatarWearable.DEFAULT_PANTS_ITEM ||
                         linkedItem.ID == AvatarWearable.DEFAULT_SHIRT_ITEM ||
                         linkedItem.ID == AvatarWearable.DEFAULT_SKIN_ITEM)
                    brokenLinks.Add (i.ID); //Default item link, needs removed
                else if (!OtherStuff.Contains (i.AssetID))
                    OtherStuff.Add (i.AssetID);
            }

            for (int i = 0; i < avappearance.Wearables.Length; i++) {
                AvatarWearable wearable = avappearance.Wearables [i];
                for (int ii = 0; ii < wearable.Count; ii++) {
                    if (!OtherStuff.Contains (wearable [ii].ItemID)) {
                        InventoryItemBase linkedItem2;
                        if ((linkedItem2 = m_InventoryService.GetItem (user, wearable [ii].ItemID)) != null) {
                            InventoryItemBase linkedItem3 = (InventoryItemBase)linkedItem2.Clone ();
                            linkedItem3.AssetID = linkedItem2.ID;
                            linkedItem3.AssetType = (int)AssetType.Link;
                            linkedItem3.ID = UUID.Random ();
                            linkedItem3.CurrentPermissions = linkedItem2.NextPermissions;
                            linkedItem3.EveryOnePermissions = linkedItem2.NextPermissions;
                            linkedItem3.Folder = CurrentOutFitFolder.ID;
                            m_InventoryService.AddItem (linkedItem3);
                        } else {
                            avappearance.Wearables [i] = AvatarWearable.DefaultWearables [i];
                        }
                    }
                }
            }

            List<UUID> items2UnAttach = new List<UUID> ();
            foreach (KeyValuePair<int, List<AvatarAttachment>> attachmentSpot in avappearance.Attachments) {
                foreach (AvatarAttachment attachment in attachmentSpot.Value) {
                    if (!OtherStuff.Contains (attachment.ItemID)) {
                        InventoryItemBase linkedItem2;
                        if ((linkedItem2 = m_InventoryService.GetItem (user, attachment.ItemID)) != null) {
                            InventoryItemBase linkedItem3 = (InventoryItemBase)linkedItem2.Clone ();
                            linkedItem3.AssetID = linkedItem2.ID;
                            linkedItem3.AssetType = (int)AssetType.Link;
                            linkedItem3.ID = UUID.Random ();
                            linkedItem3.CurrentPermissions = linkedItem2.NextPermissions;
                            linkedItem3.EveryOnePermissions = linkedItem2.NextPermissions;
                            linkedItem3.Folder = CurrentOutFitFolder.ID;
                            m_InventoryService.AddItem (linkedItem3);
                        } else
                            items2UnAttach.Add (attachment.ItemID);
                    }
                }
            }

            foreach (UUID uuid in items2UnAttach) {
                avappearance.DetachAttachment (uuid);
            }


            if (brokenLinks.Count != 0)
                m_InventoryService.DeleteItems (user, brokenLinks);
        }

        #endregion
    }
}
