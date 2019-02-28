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
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.ClientStack
{
    public delegate bool PacketMethod (IClientAPI simClient, Packet packet);

    /// <summary>
    ///     Handles new client connections
    ///     Constructor takes a single Packet and authenticates everything
    /// </summary>
    public sealed partial class LLClientView : IClientAPI
    {
        /// <value>
        ///     Debug packet level.  See OpenSim.RegisterConsoleCommands() for more details.
        /// </value>
        int m_debugPacketLevel;
        List<string> m_debugPackets = new List<string> ();
        List<string> m_debugRemovePackets = new List<string> ();

        readonly bool m_allowUDPInv;

        #region Events

        public event BinaryGenericMessage OnBinaryGenericMessage;
        public event Action<IClientAPI> OnLogout;
        public event ObjectPermissions OnObjectPermissions;
        public event Action<IClientAPI> OnConnectionClosed;
        public event ViewerEffectEventHandler OnViewerEffect;
        public event ImprovedInstantMessage OnInstantMessage;
        public event PreSendImprovedInstantMessage OnPreSendInstantMessage;
        public event ChatMessage OnChatFromClient;
        public event RezRestoreToWorld OnRezRestoreToWorld;
        public event RezObject OnRezObject;
        public event DeRezObject OnDeRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall1 OnRequestWearables;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event UUIDNameRequest OnDetachAttachmentIntoInv;
        public event ObjectAttach OnObjectAttach;
        public event ObjectDeselect OnObjectDetach;
        public event ObjectDrop OnObjectDrop;
        public event GenericCall1 OnCompleteMovementToRegion;
        public event UpdateAgent OnAgentUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event StartAnim OnStartAnim;
        public event StopAnim OnStopAnim;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event LinkObjects OnLinkObjects;
        public event DelinkObjects OnDelinkObjects;
        public event GrabObject OnGrabObject;
        public event DeGrabObject OnDeGrabObject;
        public event SpinStart OnSpinStart;
        public event SpinStop OnSpinStop;
        public event ObjectDuplicate OnObjectDuplicate;
        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
        public event MoveObject OnGrabUpdate;
        public event SpinObject OnSpinUpdate;
        public event AddNewPrim OnAddPrim;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectRequest OnObjectRequest;
        public event ObjectSelect OnObjectSelect;
        public event ObjectDeselect OnObjectDeselect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event GenericCall7 OnObjectClickAction;
        public event GenericCall7 OnObjectMaterial;
        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdateVectorWithUpdate OnUpdatePrimGroupPosition;
        public event UpdateVectorWithUpdate OnUpdatePrimSinglePosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event UpdateVector OnUpdatePrimGroupScale;

#pragma warning disable 67

        public event StatusChange OnChildAgentStatus;
        public event GenericMessage OnGenericMessage;
        public event BuyObjectInventory OnBuyObjectInventory;
        public event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture;

#pragma warning restore 67

        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;
        public event FetchInventory OnAgentDataUpdateRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;
        public event CreateNewInventoryItem OnCreateNewInventoryItem;
        public event LinkInventoryItem OnLinkInventoryItem;
        public event CreateInventoryFolder OnCreateNewInventoryFolder;
        public event UpdateInventoryFolder OnUpdateInventoryFolder;
        public event MoveInventoryFolder OnMoveInventoryFolder;
        public event FetchInventoryDescendents OnFetchInventoryDescendents;
        public event PurgeInventoryDescendents OnPurgeInventoryDescendents;
        public event FetchInventory OnFetchInventory;
        public event RequestTaskInventory OnRequestTaskInventory;
        public event UpdateInventoryItem OnUpdateInventoryItem;
        public event ChangeInventoryItemFlags OnChangeInventoryItemFlags;
        public event CopyInventoryItem OnCopyInventoryItem;
        public event MoveInventoryItem OnMoveInventoryItem;
        public event RemoveInventoryItem OnRemoveInventoryItem;
        public event RemoveInventoryFolder OnRemoveInventoryFolder;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event AbortXfer OnAbortXfer;
        public event RequestTerrain OnRequestTerrain;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event MoveTaskInventory OnMoveTaskItem;
        public event RemoveTaskInventory OnRemoveTaskItem;
        public event UUIDNameRequest OnNameFromUUIDRequest;
        public event ParcelAccessListRequest OnParcelAccessListRequest;
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event ParcelAbandonRequest OnParcelAbandonRequest;
        public event ParcelGodForceOwner OnParcelGodForceOwner;
        public event ParcelReclaim OnParcelReclaim;
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        public event ParcelReturnObjectsRequest OnParcelDisableObjectsRequest;
        public event VelocityInterpolateChangeRequest OnVelocityInterpolateChangeRequest;
        public event ParcelDeedToGroup OnParcelDeedToGroup;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;
        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;
        public event GrantUserFriendRights OnGrantUserRights;
        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event ParcelBuy OnParcelBuy;
        public event UUIDNameRequest OnTeleportHomeRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;
        public event ScriptAnswer OnScriptAnswer;
        public event RequestPayPrice OnRequestPayPrice;
        public event ObjectSaleInfo OnObjectSaleInfo;
        public event ObjectBuy OnObjectBuy;
        public event AgentSit OnUndo;
        public event AgentSit OnRedo;
        public event LandUndo OnLandUndo;
        public event ForceReleaseControls OnForceReleaseControls;
        public event GodLandStatRequest OnLandStatRequest;
        public event RequestObjectPropertiesFamily OnObjectGroupRequest;
        public event DetailedEstateDataRequest OnDetailedEstateDataRequest;
        public event SetEstateFlagsRequest OnSetEstateFlagsRequest;
        public event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture;
        public event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights;
        public event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest;
        public event SetRegionTerrainSettings OnSetRegionTerrainSettings;
        public event BakeTerrain OnBakeTerrain;
        public event RequestTerrain OnUploadTerrain;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event EstateRestartSimRequest OnEstateRestartSimRequest;
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest;
        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event UpdateVector OnAutoPilotGo;
        public event ActivateGesture OnActivateGesture;
        public event DeactivateGesture OnDeactivateGesture;
        public event ObjectOwner OnObjectOwner;
        public event DirPlacesQuery OnDirPlacesQuery;
        public event DirFindQuery OnDirFindQuery;
        public event DirLandQuery OnDirLandQuery;
        public event DirPopularQuery OnDirPopularQuery;
        public event DirClassifiedQuery OnDirClassifiedQuery;
        public event EventInfoRequest OnEventInfoRequest;
        public event ParcelSetOtherCleanTime OnParcelSetOtherCleanTime;
        public event MapItemRequest OnMapItemRequest;
        public event OfferCallingCard OnOfferCallingCard;
        public event AcceptCallingCard OnAcceptCallingCard;
        public event DeclineCallingCard OnDeclineCallingCard;
        public event SoundTrigger OnSoundTrigger;
        public event StartLure OnStartLure;
        public event TeleportLureRequest OnTeleportLureRequest;
        public event NetworkStats OnNetworkStatsUpdate;
        public event ClassifiedInfoRequest OnClassifiedInfoRequest;
        public event ClassifiedInfoUpdate OnClassifiedInfoUpdate;
        public event ClassifiedDelete OnClassifiedDelete;
        public event ClassifiedDelete OnClassifiedGodDelete;
        public event EventNotificationAddRequest OnEventNotificationAddRequest;
        public event EventNotificationRemoveRequest OnEventNotificationRemoveRequest;
        public event EventGodDelete OnEventGodDelete;
        public event ParcelDwellRequest OnParcelDwellRequest;
        public event UserInfoRequest OnUserInfoRequest;
        public event UpdateUserInfo OnUpdateUserInfo;
        public event RetrieveInstantMessages OnRetrieveInstantMessages;
        public event PickDelete OnPickDelete;
        public event PickGodDelete OnPickGodDelete;
        public event PickInfoUpdate OnPickInfoUpdate;
        public event AvatarNotesUpdate OnAvatarNotesUpdate;
        public event MuteListRequest OnMuteListRequest;
        public event AvatarInterestUpdate OnAvatarInterestUpdate;
        public event PlacesQuery OnPlacesQuery;
        public event AgentFOV OnAgentFOV;
        public event FindAgentUpdate OnFindAgent;
        public event TrackAgentUpdate OnTrackAgent;
        public event NewUserReport OnUserReport;
        public event SaveStateHandler OnSaveState;
        public event GroupAccountSummaryRequest OnGroupAccountSummaryRequest;
        public event GroupAccountDetailsRequest OnGroupAccountDetailsRequest;
        public event GroupAccountTransactionsRequest OnGroupAccountTransactionsRequest;
        public event FreezeUserUpdate OnParcelFreezeUser;
        public event EjectUserUpdate OnParcelEjectUser;
        public event ParcelBuyPass OnParcelBuyPass;
        public event ParcelGodMark OnParcelGodMark;
        public event GroupActiveProposalsRequest OnGroupActiveProposalsRequest;
        public event GroupVoteHistoryRequest OnGroupVoteHistoryRequest;
        public event SimWideDeletesDelegate OnSimWideDeletes;
        public event SendPostcard OnSendPostcard;
        public event TeleportCancel OnTeleportCancel;
        public event MuteListEntryUpdate OnUpdateMuteListEntry;
        public event MuteListEntryRemove OnRemoveMuteListEntry;
        public event GodlikeMessage OnGodlikeMessage;
        public event GodUpdateRegionInfoUpdate OnGodUpdateRegionInfoUpdate;
        public event GodlikeMessage OnEstateTelehubRequest;
        public event ViewerStartAuction OnViewerStartAuction;
        public event GroupProposalBallotRequest OnGroupProposalBallotRequest;
        public event AgentCachedTextureRequest OnAgentCachedTextureRequest;

        #endregion Events

        #region Enums

        public enum TransferPacketStatus
        {
            MorePacketsToCome = 0,
            Done = 1,
            AssetSkip = 2,
            AssetAbort = 3,
            AssetRequestFailed = -1,
            AssetUnknownSource = -2, // Equivalent of a 404
            InsufficientPermissions = -3
        }

        #endregion

        #region Class Members

        // LLClientView Only
        public delegate void BinaryGenericMessage (object sender, string method, byte [] [] args);

        /// <summary>
        ///     Used to adjust Sun Orbit values so Linden based viewers properly position sun
        /// </summary>
        const float m_sunPainDaHalfOrbitalCutoff = 4.712388980384689858f;

        static readonly Dictionary<PacketType, PacketMethod> PacketHandlers =
            new Dictionary<PacketType, PacketMethod> (); //Global/static handlers for all clients

        readonly LLUDPServer m_udpServer;
        readonly LLUDPClient m_udpClient;
        readonly UUID m_sessionId;
        readonly UUID m_secureSessionId;
        readonly UUID m_agentId;
        readonly uint m_circuitCode;
        readonly byte [] m_channelVersion = Utils.EmptyBytes;
        readonly Dictionary<string, UUID> m_defaultAnimations = new Dictionary<string, UUID> ();
        readonly IGroupsModule m_GroupsModule;

        int m_cachedTextureSerial;

        /// <value>
        ///     Maintain a record of all the objects killed.  This allows us to stop an update being sent from the
        ///     thread servicing the m_primFullUpdates queue after a kill.  If this happens the object persists as an
        ///     ownerless phantom.
        ///     All manipulation of this set has to occur under an m_entityUpdates.SyncRoot lock
        /// </value>
        //protected HashSet<uint> m_killRecord = new HashSet<uint>();
        //        protected HashSet<uint> m_attachmentsSent;
        int m_animationSequenceNumber = 1;

        bool m_SendLogoutPacketWhenClosing = true;
        AgentUpdateArgs lastarg;
        bool m_IsActive = true;

        readonly Dictionary<PacketType, PacketProcessor> m_packetHandlers =
            new Dictionary<PacketType, PacketProcessor> ();

        readonly Dictionary<string, GenericMessage> m_genericPacketHandlers =
            new Dictionary<string, GenericMessage> ();

        //PauPaw:Local Generic Message handlers

        readonly IScene m_scene;
        readonly LLImageManager m_imageManager;
        readonly string m_Name;
        readonly EndPoint m_userEndPoint;
        UUID m_activeGroupID;
        string m_activeGroupName = string.Empty;
        ulong m_activeGroupPowers;
        uint m_agentFOVCounter;

        readonly IAssetService m_assetService;
        // ReSharper disable ConvertToConstant.Local
        bool m_checkPackets = true;
        // ReSharper restore ConvertToConstant.Local

        #endregion Class Members

        #region Properties

        public LLUDPClient UDPClient {
            get { return m_udpClient; }
        }

        public IPEndPoint RemoteEndPoint {
            get { return m_udpClient.RemoteEndPoint; }
        }

        public UUID SecureSessionId {
            get { return m_secureSessionId; }
        }

        public IScene Scene {
            get { return m_scene; }
        }

        public UUID SessionId {
            get { return m_sessionId; }
        }

        public Vector3 StartPos { get; set; }

        public UUID AgentId {
            get { return m_agentId; }
        }

        public UUID ScopeID { get; set; }

        public List<UUID> AllScopeIDs { get; set; }

        public UUID ActiveGroupId {
            get { return m_activeGroupID; }
        }

        public string ActiveGroupName {
            get { return m_activeGroupName; }
        }

        public ulong ActiveGroupPowers {
            get { return m_activeGroupPowers; }
        }

        /// <summary>
        ///     Full name of the client (first name and last name)
        /// </summary>
        public string Name {
            get { return m_Name; }
        }

        public uint CircuitCode {
            get { return m_circuitCode; }
        }

        public int NextAnimationSequenceNumber {
            get { return m_animationSequenceNumber; }
        }

        public bool IsActive {
            get { return m_IsActive; }
            set { m_IsActive = value; }
        }

        public bool IsLoggingOut { get; set; }

        public bool SendLogoutPacketWhenClosing {
            set { m_SendLogoutPacketWhenClosing = value; }
        }

        #endregion Properties

        #region debug
        long startMem;

        #endregion

        /// <summary>
        ///     Constructor
        /// </summary>
        public LLClientView (EndPoint remoteEP, IScene scene, LLUDPServer udpServer, LLUDPClient udpClient,
                            AgentCircuitData sessionInfo,
                            UUID agentId, UUID sessionId, uint circuitCode)
        {
            startMem = GC.GetTotalMemory (false);

            InitDefaultAnimations ();

            m_scene = scene;

            IConfig advancedConfig = m_scene.Config.Configs ["ClientStack.LindenUDP"];
            if (advancedConfig != null)
                m_allowUDPInv = advancedConfig.GetBoolean ("AllowUDPInventory", m_allowUDPInv);

            //m_killRecord = new HashSet<uint>();
            //            m_attachmentsSent = new HashSet<uint>();

            m_assetService = m_scene.RequestModuleInterface<IAssetService> ();
            m_GroupsModule = scene.RequestModuleInterface<IGroupsModule> ();
            m_imageManager = new LLImageManager (this, m_assetService, Scene.RequestModuleInterface<IJ2KDecoder> ());
            ISimulationBase simulationBase = m_scene.RequestModuleInterface<ISimulationBase> ();
            if (simulationBase != null)
                m_channelVersion = Util.StringToBytes256 (simulationBase.Version);
            m_agentId = agentId;
            m_sessionId = sessionId;
            m_secureSessionId = sessionInfo.SecureSessionID;
            m_circuitCode = circuitCode;
            m_userEndPoint = remoteEP;
            UserAccount account = m_scene.UserAccountService.GetUserAccount (m_scene.RegionInfo.AllScopeIDs, m_agentId);
            if (account != null)
                m_Name = account.Name;

            StartPos = sessionInfo.StartingPosition;

            m_udpServer = udpServer;
            m_udpClient = udpClient;
            m_udpClient.OnQueueEmpty += HandleQueueEmpty;
            m_udpClient.OnPacketStats += PopulateStats;

            RegisterLocalPacketHandlers ();
        }

        public void Reset ()
        {
            lastarg = null;
            //Reset the killObjectUpdate packet stats
            //m_killRecord = new HashSet<uint>();
        }

        public void SetDebugPacketLevel (int newDebug)
        {
            m_debugPacketLevel = newDebug;
        }

        public void SetDebugPacketName (string packetName, bool remove)
        {
            if (remove) {
                m_debugRemovePackets.Add (packetName);
                m_debugPackets.Remove (packetName);
            } else {
                m_debugPackets.Add (packetName);
                m_debugRemovePackets.Remove (packetName);
            }
        }

        #region Client Methods

        public void Stop ()
        {
            // Send the STOP packet NOW, otherwise it doesn't get out in time
            var disable = (DisableSimulatorPacket)PacketPool.Instance.GetPacket (PacketType.DisableSimulator);
            OutPacket (disable, ThrottleOutPacketType.Immediate);
        }

        /// <summary>
        ///     Shut down the client view
        /// </summary>
        public void Close (bool forceClose)
        {
            //MainConsole.Instance.DebugFormat(
            //    "[Client]: Close has been called for {0} attached to scene {1}",
            //    Name, m_scene.RegionInfo.RegionName);

            if (forceClose && !IsLoggingOut) //Don't send it to clients that are logging out
            {
                // Send the STOP packet NOW, otherwise it doesn't get out in time
                var disable = (DisableSimulatorPacket)PacketPool.Instance.GetPacket (PacketType.DisableSimulator);
                OutPacket (disable, ThrottleOutPacketType.Immediate);
            }

            IsActive = false;

            // Shutdown the image manager
            if (m_imageManager != null)
                m_imageManager.Close ();

            // Fire the callback for this connection closing
            if (OnConnectionClosed != null)
                OnConnectionClosed (this);

            // Flush all of the packets out of the UDP server for this client
            if (m_udpServer != null) {
                m_udpServer.Flush (m_udpClient);
                m_udpServer.RemoveClient (this);
            }

            // Disable UDP handling for this client
            m_udpClient.OnQueueEmpty -= HandleQueueEmpty;
            m_udpClient.OnPacketStats -= PopulateStats;
            m_udpClient.Shutdown ();

            // 03122016 Fly-man-
            // Turning this on to check why regions are taking
            // up so much memory after a user exits the region
            MainConsole.Instance.DebugFormat ("[Client] Memory on initiate {0} mBytes", startMem / 1000000);
            var endMem = GC.GetTotalMemory (true);
            MainConsole.Instance.DebugFormat ("[Client] Memory on close {0} mBytes", endMem / 1000000);
            MainConsole.Instance.DebugFormat ("[Client] Memory released {0} mBytes", (startMem - endMem) / 1000000);
        }

        public void Kick (string message)
        {
            if (!ChildAgentStatus ()) {
                var kupack = (KickUserPacket)PacketPool.Instance.GetPacket (PacketType.KickUser);
                kupack.UserInfo.AgentID = AgentId;
                kupack.UserInfo.SessionID = SessionId;
                kupack.TargetBlock.TargetIP = 0;
                kupack.TargetBlock.TargetPort = 0;
                kupack.UserInfo.Reason = Util.StringToBytes256 (message);
                OutPacket (kupack, ThrottleOutPacketType.OutBand);
                // You must sleep here or users get no message!
                Thread.Sleep (500);
            }
        }

        #endregion Client Methods

        #region IClientCore

        readonly Dictionary<Type, object> m_clientInterfaces = new Dictionary<Type, object> ();

        /// <summary>
        ///     Register an interface on this client, should only be called in the constructor.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="iface"></param>
        void RegisterInterface<T> (T iface)
        {
            lock (m_clientInterfaces) {
                if (!m_clientInterfaces.ContainsKey (typeof (T))) {
                    m_clientInterfaces.Add (typeof (T), iface);
                }
            }
        }

        public bool TryGet<T> (out T iface)
        {
            if (m_clientInterfaces.ContainsKey (typeof (T))) {
                iface = (T)m_clientInterfaces [typeof (T)];
                return true;
            }
            iface = default (T);
            return false;
        }

        public T Get<T> ()
        {
            return (T)m_clientInterfaces [typeof (T)];
        }

        #endregion


        /// <summary>
        ///     Calculate the number of packets required to send the asset to the client.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        static int CalculateNumPackets (byte [] data)
        {
            const uint m_maxPacketSize = 1024;

            int numPackets = 1;

            if (data == null)
                return 0;

            if (data.LongLength > m_maxPacketSize) {
                // over max number of bytes so split up file
                long restData = data.LongLength - m_maxPacketSize;
                int restPackets = (int)((restData + m_maxPacketSize - 1) / m_maxPacketSize);
                numPackets += restPackets;
            }

            return numPackets;
        }

        #region IClientIPEndpoint Members

        public IPAddress EndPoint {
            get {
                if (m_userEndPoint is IPEndPoint) {
                    IPEndPoint ep = (IPEndPoint)m_userEndPoint;

                    return ep.Address;
                }
                return null;
            }
        }

        #endregion

        public EndPoint GetClientEP ()
        {
            return m_userEndPoint;
        }

        /// <summary>
        ///     Handler called when we receive a logout packet.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="pack"></param>
        /// <returns></returns>
        bool HandleLogout (IClientAPI client, Packet pack)
        {
            if (pack.Type == PacketType.LogoutRequest) {
                if (((LogoutRequestPacket)pack).AgentData.SessionID != SessionId) return false;
            }

            return Logout (client);
        }

        /// <summary>
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        bool Logout (IClientAPI client)
        {
            //MainConsole.Instance.InfoFormat("[Client]: Got a logout request for {0} in {1}", Name, Scene.RegionInfo.RegionName);

            Action<IClientAPI> handlerLogout = OnLogout;

            if (handlerLogout != null) {
                handlerLogout (client);
            }

            return true;
        }

    }
}
