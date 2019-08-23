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
using System.Linq;
using System.Net;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;
using FriendInfo = WhiteCore.Framework.Services.FriendInfo;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Services
{
    /// <summary>
    ///     A class to handle LL login response.
    /// </summary>
    public class LLLoginResponse : LoginResponse
    {
        readonly ArrayList classifiedCategories;
        readonly ArrayList eventCategories;
        readonly ArrayList eventNotifications;
        readonly ArrayList initialOutfit;
        readonly ArrayList loginFlags;
        readonly IConfigSource m_source;
        readonly ArrayList tutorial = new ArrayList();
        readonly ArrayList uiConfig;
        readonly Hashtable uiConfigHash;
        ArrayList activeGestures;

        string agentAccess;
        string agentAccessMax;
        string agentRegionAccess;
        int aoTransition;
        int agentFlags;
        UUID agentID;
        ArrayList agentInventory;

        // Login
        string firstname;
        string home;
        ArrayList inventoryRoot;
        string lastname;
        string login;
        Hashtable loginFlagsHash;
        string lookAt;
        string cOFVersion;

        BuddyList m_buddyList;
        string seedCapability;
        string startLocation;
        string udpBlackList;
        IGridInfo m_gridInfo;

        public LLLoginResponse()
        {
            login = "true";
            ErrorMessage = "";
            ErrorReason = LoginResponseEnum.OK;
            loginFlags = new ArrayList();
            eventCategories = new ArrayList();
            eventNotifications = new ArrayList();
            uiConfig = new ArrayList();
            classifiedCategories = new ArrayList();

            uiConfigHash = new Hashtable();

            inventoryRoot = new ArrayList();
            initialOutfit = new ArrayList();
            agentInventory = new ArrayList();
            activeGestures = new ArrayList();

            SetDefaultValues();
        }

        public LLLoginResponse(UserAccount account, AgentCircuitData aCircuit, Framework.Services.UserInfo pinfo,
                               GridRegion destination, List<InventoryFolderBase> invSkel, FriendInfo[] friendsList,
                               IInventoryService invService, ILibraryService libService,
                               string where, string startlocation, Vector3 position, Vector3 lookAt,
                               List<InventoryItemBase> gestures,
                               GridRegion home, IPEndPoint clientIP, string adultMax, string adultRating,
                               ArrayList eventValues, ArrayList eventNotificationValues, ArrayList classifiedValues,
                               string seedCap, IConfigSource source,
                               string displayName, string cofversion, IGridInfo info)
            : this()
        {
            m_source = source;
            m_gridInfo = info;
            SeedCapability = seedCap;

            FillOutInventoryData(invSkel, libService, invService);

            FillOutActiveGestures(gestures);

            CircuitCode = (int) aCircuit.CircuitCode;
            Lastname = account.LastName;
            Firstname = account.FirstName;
            this.DisplayName = displayName;
            AgentID = account.PrincipalID;
            SessionID = aCircuit.SessionID;
            SecureSessionID = aCircuit.SecureSessionID;
            BuddList = ConvertFriendListItem(friendsList);
            StartLocation = where;
            AgentAccessMax = adultMax;
            AgentAccess = adultRating;
            AgentRegionAccess = AgentRegionAccess;
            AOTransition = AOTransition;
            AgentFlag = AgentFlag;
            eventCategories = eventValues;
            eventNotifications = eventNotificationValues;
            classifiedCategories = classifiedValues;
            cOFVersion = cofversion;

            FillOutHomeData(pinfo, home);
            LookAt = string.Format("[r{0},r{1},r{2}]", lookAt.X, lookAt.Y, lookAt.Z);

            FillOutRegionData(aCircuit, destination);
            login = "true";
            ErrorMessage = "";
            ErrorReason = LoginResponseEnum.OK;
        }

        #region FillOutData

        void FillOutInventoryData(List<InventoryFolderBase> invSkel, ILibraryService libService,
                                          IInventoryService invService)
        {
            InventoryData inventData = null;

            try
            {
                inventData = GetInventorySkeleton(libService, invService, invSkel);
            }
            catch (Exception e)
            {
                MainConsole.Instance.WarnFormat(
                    "[LLogin service]: Error processing inventory skeleton of agent {0} - {1}",
                    agentID, e);

                // ignore and continue
            }

            if (inventData != null)
            {
                ArrayList AgentInventoryArray = inventData.InventoryArray;

                Hashtable InventoryRootHash = new Hashtable();
                InventoryRootHash["folder_id"] = inventData.RootFolderID.ToString();
                InventoryRoot = new ArrayList {InventoryRootHash};
                InventorySkeleton = AgentInventoryArray;
            }

            // Inventory Library Section
            if (libService != null &&
                (InventoryLibraryOwner == null || InventoryLibRoot == null || InventoryLibRoot == null))
            {
                InventoryLibrary = new ArrayList();
                InventoryLibraryOwner = new ArrayList();
                InventoryLibRoot = new ArrayList();

                InventoryLibraryOwner = GetLibraryOwner(libService);
                InventoryLibrary = GetInventoryLibrary(libService, invService);
                Hashtable InventoryLibRootHash = new Hashtable();
                InventoryLibRootHash["folder_id"] = "00000112-000f-0000-0000-000100bba000";
                InventoryLibRoot.Add(InventoryLibRootHash);
            }
        }

        void FillOutActiveGestures(List<InventoryItemBase> gestures)
        {
            ArrayList list = new ArrayList();
            if (gestures != null)
            {
                foreach (InventoryItemBase gesture in gestures)
                {
                    Hashtable item = new Hashtable();
                    item["item_id"] = gesture.ID.ToString();
                    item["asset_id"] = gesture.AssetID.ToString();
                    list.Add(item);
                }
            }
            ActiveGestures = list;
        }

        void FillOutHomeData(Framework.Services.UserInfo pinfo, GridRegion homeRegion)
        {
            // TODO: The region start positions should be retrieved from the SimulationBase MapCenterX/MapCenterY
            // This is a fallback setting as the user's home region should have been set on login
            int x = Constants.DEFAULT_REGIONSTART_X * Constants.RegionSize;
            int y = Constants.DEFAULT_REGIONSTART_Y * Constants.RegionSize;
            if (homeRegion != null) {
                x = homeRegion.RegionLocX;
                y = homeRegion.RegionLocY;
            }

            Home = string.Format(
                "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                x,
                y,
                pinfo.HomePosition.X, pinfo.HomePosition.Y, pinfo.HomePosition.Z,
                pinfo.HomeLookAt.X, pinfo.HomeLookAt.Y, pinfo.HomeLookAt.Z);
        }

        void FillOutRegionData(AgentCircuitData circuitData, GridRegion destination)
        {
            IPEndPoint endPoint = destination.ExternalEndPoint;
            //We don't need this anymore, we set this from what we get from the region
            //endPoint = Util.ResolveAddressForClient (endPoint, circuitData.ClientIPEndPoint);
            SimAddress = endPoint.Address.ToString();
            SimPort = (uint) circuitData.RegionUDPPort;
            RegionX = (uint) destination.RegionLocX;
            RegionY = (uint) destination.RegionLocY;
            RegionSizeX = destination.RegionSizeX;
            RegionSizeY = destination.RegionSizeY;
        }

        void SetDefaultValues()
        {
            DST = TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.Now) ? "Y" : "N";
            StipendSinceLogin = "N";
            Gendered = "Y";
            EverLoggedIn = "Y";
            login = "false";
            firstname = "Test";
            lastname = "User";
            agentAccess = "M";
            agentAccessMax = "A";
            agentRegionAccess = "A";
            startLocation = "last";
            aoTransition = 0;
            agentFlags = 0;
            udpBlackList = "EnableSimulator,TeleportFinish,CrossedRegion,OpenCircuit";

            ErrorMessage = "You have entered an invalid name/password combination.  Check Caps/lock.";
            ErrorReason = LoginResponseEnum.PasswordIncorrect;
            SessionID = UUID.Random();
            SecureSessionID = UUID.Random();
            AgentID = UUID.Random();

            Hashtable InitialOutfitHash = new Hashtable();
            InitialOutfitHash["folder_name"] = "Nightclub Female";
            InitialOutfitHash["gender"] = "female";
            initialOutfit.Add(InitialOutfitHash);
        }

        #endregion

        #region To***

        public override Hashtable ToHashtable()
        {
            try
            {
                Hashtable responseData = new Hashtable();

                loginFlagsHash = new Hashtable();
                loginFlagsHash["daylight_savings"] = DST;
                loginFlagsHash["stipend_since_login"] = StipendSinceLogin;
                loginFlagsHash["gendered"] = Gendered;
                loginFlagsHash["ever_logged_in"] = EverLoggedIn;
                loginFlags.Add(loginFlagsHash);

                responseData["first_name"] = Firstname;
                responseData["last_name"] = Lastname;
                responseData["display_name"] = DisplayName;
                responseData["agent_access"] = agentAccess;
                responseData["agent_access_max"] = agentAccessMax;
                responseData["agent_region_access"] = agentRegionAccess;
                responseData["udp_blacklist"] = udpBlackList;

                if (AllowFirstLife != null)
                    uiConfigHash["allow_first_life"] = AllowFirstLife;
                uiConfig.Add(uiConfigHash);

                responseData["sim_port"] = (int) SimPort;
                responseData["sim_ip"] = SimAddress;
                responseData["http_port"] = (int) SimHttpPort;

                responseData["agent_id"] = AgentID.ToString();
                responseData["session_id"] = SessionID.ToString();
                responseData["secure_session_id"] = SecureSessionID.ToString();
                responseData["circuit_code"] = CircuitCode;
                responseData["seconds_since_epoch"] = (int) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                responseData["login-flags"] = loginFlags;
                responseData["seed_capability"] = seedCapability;

                responseData["event_categories"] = eventCategories;
                responseData["event_notifications"] = eventNotifications; // Fly-Man- This is the Notifications of Events that you "subscribed" to
                responseData["classified_categories"] = classifiedCategories;
                responseData["ui-config"] = uiConfig;
                responseData["export"] = AllowExportPermission ? "flag" : "";
                responseData["ao_transition"] = aoTransition;
                responseData["agent_flags"] = agentFlags;

                if (agentInventory != null)
                {
                    responseData["inventory-skeleton"] = agentInventory;
                    responseData["inventory-root"] = inventoryRoot;
                }
                responseData["inventory-skel-lib"] = InventoryLibrary;
                responseData["inventory-lib-root"] = InventoryLibRoot;
                responseData["gestures"] = activeGestures;
                responseData["inventory-lib-owner"] = InventoryLibraryOwner;
                responseData["initial-outfit"] = initialOutfit;

                Hashtable tutorialHash = new Hashtable();
                tutorialHash["tutorial_url"] = TutorialURL;

                if (TutorialURL != "")
                    tutorialHash["use_tutorial"] = "Y";
                else
                    tutorialHash["use_tutorial"] = "";
                tutorial.Add(tutorialHash);

                responseData["tutorial_setting"] = tutorial;
                responseData["start_location"] = startLocation;
                responseData["home"] = home;
                responseData["look_at"] = lookAt;
                responseData["message"] = Message;
                responseData["region_x"] = (int) (RegionX);
                responseData["region_y"] = (int) (RegionY);
                responseData["region_size_x"] = (RegionSizeX);
                responseData["region_size_y"] = (RegionSizeY);
                responseData["cof_version"] = cOFVersion;
                
                #region Global Textures

                ArrayList globalTextures = new ArrayList();
                Hashtable globalTexturesHash = new Hashtable();
                globalTexturesHash["sun_texture_id"] = SunTexture;
                globalTexturesHash["cloud_texture_id"] = CloudTexture;
                globalTexturesHash["moon_texture_id"] = MoonTexture;
                globalTextures.Add(globalTexturesHash);
                responseData["global-textures"] = globalTextures;

                #endregion Global Textures

                if (SearchURL != string.Empty)
                    responseData["search"] = SearchURL;

                if (MapTileURL != string.Empty)
                    responseData["map-server-url"] = MapTileURL;

                if (AgentAppearanceURL != string.Empty)
                    responseData["agent_appearance_service"] = AgentAppearanceURL;

                if (WebProfileURL != string.Empty)
                    responseData["web_profile_url"] = WebProfileURL;

                if (HelpURL != string.Empty)
                    responseData["help_url_format"] = HelpURL;

                if (SnapshotConfigURL != string.Empty)
                    responseData["snapshot_config_url"] = SnapshotConfigURL;

                if (OpenIDURL != string.Empty)
                    responseData["openid_url"] = OpenIDURL;

                if (DestinationURL != string.Empty)
                    responseData["destination_guide_url"] = DestinationURL;

                if (MarketPlaceURL != string.Empty)
                    responseData["marketplace_url"] = MarketPlaceURL;

                if (MaxAgentGroups != 0)
                    responseData["max-agent-groups"] = MaxAgentGroups;
                else
                    responseData["max-agent-groups"] = 100;

                //Makes viewers crash...
                if (VoiceServerType != string.Empty)
                {
                    Hashtable voice_config = new Hashtable();
                    voice_config["VoiceServerType"] = VoiceServerType;
                    ArrayList list = new ArrayList {voice_config};
                    responseData["voice-config"] = list;
                }

                if (m_buddyList != null)
                {
                    responseData["buddy-list"] = m_buddyList.ToArray();
                }
                if (m_source != null)
                {
                    // we're mapping GridInfoService keys to 
                    // the ones expected by known viewers.
                    // hippo, imprudence, phoenix are known to work
                    IConfig gridInfo = m_source.Configs["GridInfoService"];
                    if (gridInfo.GetBoolean("SendGridInfoToViewerOnLogin", false))
                    {
                        string tmp;
                        tmp = gridInfo.GetString("gridname", string.Empty);
                        if (tmp != string.Empty) responseData["gridname"] = tmp;
                        tmp = gridInfo.GetString("login", string.Empty);
                        if (tmp != string.Empty) responseData["loginuri"] = tmp;

                        // alternate keys of the same thing. (note careful not to overwrite responsedata["welcome"]
                        tmp = gridInfo.GetString("loginpage", string.Empty);
                        if (tmp != string.Empty) responseData["loginpage"] = tmp;
                        tmp = gridInfo.GetString("welcome", string.Empty);
                        if (tmp != string.Empty) responseData["loginpage"] = tmp;

                        // alternate keys of the same thing.
                        tmp = gridInfo.GetString("economy", string.Empty);
                        if (tmp != string.Empty) responseData["economy"] = tmp;
                        tmp = gridInfo.GetString("helperuri", string.Empty);
                        if (tmp != string.Empty) responseData["helperuri"] = tmp;
                        
                        // Some viewers recognize these values already
                        // ...but broadcasting them won't make older viewer crash
                        tmp = gridInfo.GetString("destination", string.Empty);
                        if (tmp != string.Empty) responseData["destination"] = tmp;
                        tmp = gridInfo.GetString("marketplace", string.Empty);
                        if (tmp != string.Empty) responseData["marketplace"] = tmp;

                        tmp = gridInfo.GetString("about", string.Empty);
                        if (tmp != string.Empty) responseData["about"] = tmp;
                        tmp = gridInfo.GetString("help", string.Empty);
                        if (tmp != string.Empty) responseData["help"] = tmp;
                        tmp = gridInfo.GetString("register", string.Empty);
                        if (tmp != string.Empty) responseData["register"] = tmp;
                        tmp = gridInfo.GetString("password", string.Empty);
                        if (tmp != string.Empty) responseData["password"] = tmp;
                        tmp = gridInfo.GetString("CurrencySymbol", string.Empty);
                        if (tmp != string.Empty) responseData["currency"] = tmp;
                        tmp = gridInfo.GetString("RealCurrencySymbol", string.Empty);
                        if (tmp != string.Empty) responseData["real_currency"] = tmp;
                        tmp = gridInfo.GetString("DirectoryFee", string.Empty);
                        if (tmp != string.Empty) responseData["directory_fee"] = tmp;
                        tmp = gridInfo.GetString("MaxGroups", string.Empty);
                        if (tmp != string.Empty) responseData["max_groups"] = tmp;
                    }
                }

                responseData["login"] = "true";

                return responseData;
            }
            catch (Exception e)
            {
                MainConsole.Instance.Warn("[LLogin service]: Error creating Hashtable Response: " + e);

                return LLFailedLoginResponse.InternalError.ToHashtable();
            }
        }

        public void AddToUIConfig(string itemName, string item)
        {
            uiConfigHash[itemName] = item;
        }

        static BuddyList ConvertFriendListItem(FriendInfo[] friendsList)
        {
            BuddyList buddylistreturn = new BuddyList();
            foreach (BuddyList.BuddyInfo buddyitem in from finfo in friendsList
                                                      where finfo.TheirFlags != -1
                                                      select new BuddyList.BuddyInfo(finfo.Friend)
                                                                 {
                                                                     BuddyID = finfo.Friend,
                                                                     BuddyRightsHave = finfo.TheirFlags,
                                                                     BuddyRightsGiven = finfo.MyFlags
                                                                 })
            {
                buddylistreturn.AddNewBuddy(buddyitem);
            }
            return buddylistreturn;
        }

        InventoryData GetInventorySkeleton(ILibraryService library, IInventoryService inventoryService,
                                                   List<InventoryFolderBase> folders)
        {
            UUID rootID = UUID.Zero;
            ArrayList agentInventoryArray = new ArrayList();
            Hashtable tempHash;
            foreach (InventoryFolderBase InvFolder in folders)
            {
                if (InvFolder.ParentID == UUID.Zero && InvFolder.Name == InventoryFolderBase.ROOT_FOLDER_NAME)
                    rootID = InvFolder.ID;
                tempHash = new Hashtable();
                tempHash["name"] = InvFolder.Name;
                tempHash["parent_id"] = InvFolder.ParentID.ToString();
                tempHash["version"] = (int) InvFolder.Version;
                tempHash["type_default"] = (int) InvFolder.Type;
                tempHash["folder_id"] = InvFolder.ID.ToString();
                agentInventoryArray.Add(tempHash);
            }
            return new InventoryData(agentInventoryArray, rootID);
        }

        /// <summary>
        ///     Converts the inventory library skeleton into the form required by the RPC request.
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetInventoryLibrary(ILibraryService library, IInventoryService inventoryService)
        {
            ArrayList agentInventoryArray = new ArrayList();
            List<InventoryFolderBase> rootFolders = inventoryService.GetRootFolders(library.LibraryOwnerUUID);
            Hashtable rootHash = new Hashtable();
            rootHash["name"] = library.LibraryName;
            rootHash["parent_id"] = UUID.Zero.ToString();
            rootHash["version"] = 1;
            rootHash["type_default"] = 8;
            rootHash["folder_id"] = library.LibraryRootFolderID.ToString();
            agentInventoryArray.Add(rootHash);

            List<UUID> rootFolderUUIDs =
                (from rootFolder in rootFolders 
                 where rootFolder.Name != InventoryFolderBase.ROOT_FOLDER_NAME 
                 select rootFolder.ID).ToList();

            if (rootFolderUUIDs.Count != 0)
            {
                foreach (UUID rootfolderID in rootFolderUUIDs)
                {
                    TraverseFolder(library.LibraryOwnerUUID, rootfolderID, inventoryService, library, true,
                                   ref agentInventoryArray);
                }
            }
            return agentInventoryArray;
        }

        void TraverseFolder(UUID agentIDreq, UUID folderID, IInventoryService invService, ILibraryService library,
                                    bool rootFolder, ref ArrayList table)
        {
            List<InventoryFolderBase> folders = invService.GetFolderFolders(agentIDreq, folderID);
            foreach (InventoryFolderBase folder in folders)
            {
                Hashtable tempHash = new Hashtable();
                tempHash["name"] = folder.Name;
                if (rootFolder)
                    tempHash["parent_id"] = library.LibraryRootFolderID.ToString();
                else
                    tempHash["parent_id"] = folder.ParentID.ToString();
                tempHash["version"] = 1;
                tempHash["type_default"] = 9;
                tempHash["folder_id"] = folder.ID.ToString();
                table.Add(tempHash);
                TraverseFolder(agentIDreq, folder.ID, invService, library, false, ref table);
            }
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        protected virtual ArrayList GetLibraryOwner(ILibraryService libService)
        {
            // for now create random inventory library owner
            Hashtable tempHash = new Hashtable();
            tempHash["agent_id"] = libService.LibraryOwnerUUID.ToString(); // libFolder.Owner
            ArrayList inventoryLibOwner = new ArrayList {tempHash};
            return inventoryLibOwner;
        }

        public class InventoryData
        {
            public ArrayList InventoryArray;
            public UUID RootFolderID = UUID.Zero;

            public InventoryData(ArrayList invList, UUID rootID)
            {
                InventoryArray = invList;
                RootFolderID = rootID;
            }
        }

        #endregion

        #region Properties

        public static ArrayList InventoryLibrary;

        public static ArrayList InventoryLibraryOwner;

        public static ArrayList InventoryLibRoot;

        public string Login
        {
            get { return login; }
            set { login = value; }
        }

        public string DST { get; set; }

        public string StipendSinceLogin { get; set; }

        public string Gendered { get; set; }

        public string EverLoggedIn { get; set; }

        public uint SimPort { get; set; }

        public uint SimHttpPort { get; set; }

        public string SimAddress { get; set; }

        public UUID AgentID
        {
            get { return agentID; }
            set { agentID = value; }
        }

        public UUID SessionID { get; set; }

        public UUID SecureSessionID { get; set; }

        public int CircuitCode { get; set; }

        public uint RegionX { get; set; }

        public uint RegionY { get; set; }

        public int RegionSizeX { get; set; }

        public int RegionSizeY { get; set; }

        public string Firstname
        {
            get { return firstname; }
            set { firstname = value; }
        }

        public string DisplayName { get; set; }

        public string Lastname
        {
            get { return lastname; }
            set { lastname = value; }
        }

        public string AgentAccess
        {
            get { return agentAccess; }
            set { agentAccess = value; }
        }

        public string AgentAccessMax
        {
            get { return agentAccessMax; }
            set { agentAccessMax = value; }
        }
        
        public string AgentRegionAccess
        {
        	get { return agentRegionAccess; }
        	set { agentRegionAccess = value; }
        }

        public string StartLocation
        {
            get { return startLocation; }
            set { startLocation = value; }
        }

        public string LookAt
        {
            get { return lookAt; }
            set { lookAt = value; }
        }

        public string SeedCapability
        {
            get { return seedCapability; }
            set { seedCapability = value; }
        }
        
        public int AOTransition
        {
        	get { return aoTransition; }
        	set { aoTransition = value; }
        }
        
        public int AgentFlag
        {
        	get { return agentFlags; }
        	set { agentFlags = value; }
        }

        public string ErrorReason { get; set; }

        public string ErrorMessage { get; set; }

        public ArrayList InventoryRoot
        {
            get { return inventoryRoot; }
            set { inventoryRoot = value; }
        }

        public ArrayList InventorySkeleton
        {
            get { return agentInventory; }
            set { agentInventory = value; }
        }

        public ArrayList ActiveGestures
        {
            get { return activeGestures; }
            set { activeGestures = value; }
        }

        public string Home
        {
            get { return home; }
            set { home = value; }
        }

        public string SunTexture
        {
            get { return (string) LLLoginResponseRegister.GetValue("SunTexture"); }
        }

        public string CloudTexture
        {
            get { return (string) LLLoginResponseRegister.GetValue("CloudTexture"); }
        }

        public string MoonTexture
        {
            get { return (string) LLLoginResponseRegister.GetValue("MoonTexture"); }
        }

        public string AllowFirstLife
        {
            get { return (string) LLLoginResponseRegister.GetValue("AllowFirstLife"); }
        }

        public bool AllowExportPermission
        {
            get { return (bool) LLLoginResponseRegister.GetValue("AllowExportPermission"); }
        }

        public string OpenIDURL
        {
            get { return ""; }
        }

        public string SnapshotConfigURL
        {
            get { return m_gridInfo.GridSnapshotConfigURI; }
        }

        public string HelpURL
        {
            get { return m_gridInfo.GridHelpURI; }
        }

        public int MaxAgentGroups
        {
            get { return (int) LLLoginResponseRegister.GetValue("MaxAgentGroups"); }
        }

        public string VoiceServerType
        {
            get { return (string) LLLoginResponseRegister.GetValue("VoiceServerType"); }
        }

        public string TutorialURL
        {
            get { return m_gridInfo.GridTutorialURI; }
        }

        public string MapTileURL
        {
            get { return m_gridInfo.GridMapTileURI; }
        }

        public string AgentAppearanceURL
        {
            get { return m_gridInfo.AgentAppearanceURI; }
        }

        public string SearchURL
        {
            get { return m_gridInfo.GridSearchURI; }
        }

        public string WebProfileURL
        {
            get { return m_gridInfo.GridWebProfileURI; }
        }

        public string DestinationURL
        {
            get { return m_gridInfo.GridDestinationURI; }
        }

        public string MarketPlaceURL
        {
            get { return m_gridInfo.GridMarketplaceURI; }
        }

        public string Message
        {
            get
            {
                string retVal = (string) LLLoginResponseRegister.GetValue("Message");
                if (retVal.Contains ("<USERNAME>"))
                {
                    retVal = DisplayName != "" 
                        ? retVal.Replace ("<USERNAME>", DisplayName) 
                        : retVal.Replace ("<USERNAME>", firstname + " " + lastname);
                }
                return retVal;
            }
        }

        public BuddyList BuddList
        {
            get { return m_buddyList; }
            set { m_buddyList = value; }
        }

        #endregion

        #region Nested type: BuddyList

        public class BuddyList
        {
            public List<BuddyInfo> Buddies = new List<BuddyInfo>();

            public void AddNewBuddy(BuddyInfo buddy)
            {
                if (!Buddies.Contains(buddy))
                {
                    Buddies.Add(buddy);
                }
            }

            public ArrayList ToArray()
            {
                ArrayList buddyArray = new ArrayList();
                foreach (BuddyInfo buddy in Buddies)
                {
                    buddyArray.Add(buddy.ToHashTable());
                }
                return buddyArray;
            }

            #region Nested type: BuddyInfo

            public class BuddyInfo
            {
                public string BuddyID;
                public int BuddyRightsGiven = 1;
                public int BuddyRightsHave = 1;

                public BuddyInfo(string buddyID)
                {
                    BuddyID = buddyID;
                }

                public BuddyInfo(UUID buddyID)
                {
                    BuddyID = buddyID.ToString();
                }

                public Hashtable ToHashTable()
                {
                    Hashtable hTable = new Hashtable();
                    hTable["buddy_rights_has"] = BuddyRightsHave;
                    hTable["buddy_rights_given"] = BuddyRightsGiven;
                    hTable["buddy_id"] = BuddyID;
                    return hTable;
                }
            }

            #endregion
        }

        #endregion

        #region Nested type: UserInfo
        // This UserInfo class is not used - greythane - 20190822
        // the Framework.Services.UserInfo class has more details and used
        /*
        public class UserInfo
        {
            public string firstname;
            public Vector3 homelookat;
            public Vector3 homepos;
            public ulong homeregionhandle;
            public string lastname;
        }
        */
        #endregion
    }

    /// <summary>
    ///     A generic kvp register so that we can store values for multiple LLLoginResponses
    /// </summary>
    public class LLLoginResponseRegister
    {
        static readonly Dictionary<string, object> m_values = new Dictionary<string, object>();

        public static void RegisterValue(string key, object value)
        {
            m_values[key] = value;
        }

        public static object GetValue(string key)
        {
            if (m_values.ContainsKey(key))
                return m_values[key];
            return null;
        }
    }
}
