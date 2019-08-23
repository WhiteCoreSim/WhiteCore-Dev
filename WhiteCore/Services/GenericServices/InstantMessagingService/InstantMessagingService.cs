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

using System.Collections.Generic;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using ChatSessionMember = WhiteCore.Framework.DatabaseInterfaces.ChatSessionMember;

namespace WhiteCore.Services
{
    public class InstantMessagingService : ConnectorBase, IService, IInstantMessagingService
    {
        #region Declares

        IEventQueueService m_eventQueueService;
        IGroupsServiceConnector m_groupData;
        readonly Dictionary<UUID, ChatSession> chatSessions = new Dictionary<UUID, ChatSession> ();

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            registry.RegisterModuleInterface<IInstantMessagingService> (this);

            Init (registry, "InstantMessagingService", "", "/im/", "InstantMessageServerURI");
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup ()
        {
            m_eventQueueService = m_registry.RequestModuleInterface<IEventQueueService> ();
            ISyncMessageRecievedService syncRecievedService = m_registry.RequestModuleInterface<ISyncMessageRecievedService> ();
            if (syncRecievedService != null)
                syncRecievedService.OnMessageReceived += syncRecievedService_OnMessageReceived;
            m_groupData = Framework.Utilities.DataManager.RequestPlugin<IGroupsServiceConnector> ();
            m_registry.RequestModuleInterface<ISimulationBase> ().EventManager.RegisterEventHandler ("UserStatusChange",
                                                                                                   OnGenericEvent);
        }

        #endregion

        #region Region-side message sending

        OSDMap syncRecievedService_OnMessageReceived (OSDMap message)
        {
            string method = message ["Method"];
            if (method == "SendInstantMessages") {
                List<GridInstantMessage> messages =
                    ((OSDArray)message ["Messages"]).ConvertAll<GridInstantMessage> ((o) => {
                        GridInstantMessage im = new GridInstantMessage ();
                        im.FromOSD ((OSDMap)o);
                        return im;
                    });
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                if (manager != null) {
                    foreach (GridInstantMessage im in messages) {
                        Framework.PresenceInfo.IScenePresence userPresence;

                        foreach (IScene scene in manager.Scenes) {
                            userPresence = scene.GetScenePresence (im.ToAgentID);

                            //AR: Do not fire for child agents or group messages are sent for every region
                            if (userPresence != null && userPresence.IsChildAgent == false) {
                                IMessageTransferModule messageTransfer = scene.RequestModuleInterface<IMessageTransferModule> ();
                                if (messageTransfer != null) {
                                    messageTransfer.SendInstantMessage (im);
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        #endregion

        #region User Status Change

        protected object OnGenericEvent (string functionName, object parameters)
        {
            if (functionName == "UserStatusChange") {
                // A user has logged in or out... we need to update friends lists across the grid

                object [] info = (object [])parameters;
                UUID us = UUID.Parse (info [0].ToString ());
                bool isOnline = bool.Parse (info [1].ToString ());

                if (!isOnline) {
                    // If they are going offline, actually remove from from all group chats so that the next time they log in, they will be re-added
                    foreach (GroupMembershipData gmd in m_groupData.GetAgentGroupMemberships (us, us)) {
                        ChatSessionMember member = FindMember (us, gmd.GroupID);
                        if (member != null) {
                            member.HasBeenAdded = false;
                            member.RequestedRemoval = false;
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region IInstantMessagingService Members

        public string ChatSessionRequest (IRegionClientCapsService caps, OSDMap req)
        {
            string method = req ["method"].AsString ();
            UUID sessionID = UUID.Parse (req ["session-id"].AsString ());

            switch (method) {
            case "start conference": {
                    if (SessionExists (sessionID))
                        return ""; // No duplicate sessions

                    // Create the session.
                    CreateSession (new ChatSession {
                        Members = new List<ChatSessionMember> (),
                        SessionID = sessionID,
                        Name = caps.ClientCaps.AccountInfo.Name + " Conference"
                    });

                    OSDArray parameters = (OSDArray)req ["params"];
                    // Add other invited members.
                    foreach (OSD param in parameters) {
                        AddDefaultPermsMemberToSession (param.AsUUID (), sessionID);
                    }

                    // Add us to the session!
                    AddMemberToGroup (new ChatSessionMember {
                        AvatarKey = caps.AgentID,
                        CanVoiceChat = true,
                        IsModerator = true,
                        MuteText = false,
                        MuteVoice = false,
                        HasBeenAdded = true,
                        RequestedRemoval = false
                    }, sessionID);


                    //Inform us about our room
                    ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block =
                        new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock {
                            AgentID = caps.AgentID,
                            CanVoiceChat = true,
                            IsModerator = true,
                            MuteText = false,
                            MuteVoice = false,
                            Transition = "ENTER"
                        };
                    m_eventQueueService.ChatterBoxSessionAgentListUpdates (sessionID, new [] { block }, caps.AgentID,
                                                                          "ENTER", caps.RegionID);

                    ChatterBoxSessionStartReplyMessage cs = new ChatterBoxSessionStartReplyMessage {
                        VoiceEnabled = true,
                        TempSessionID = sessionID,
                        Type = 1,
                        Success = true,
                        SessionID = sessionID,
                        SessionName = caps.ClientCaps.AccountInfo.Name + " Conference",
                        ModeratedVoice = true
                    };
                    return OSDParser.SerializeLLSDXmlString (cs.Serialize ());
                }

            case "accept invitation": {
                    // They would like added to the group conversation
                    var usAgents = new List<ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock> ();
                    var notUsAgents = new List<ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock> ();

                    ChatSession session = GetSession (sessionID);
                    if (session != null) {
                        ChatSessionMember thismember = FindMember (caps.AgentID, sessionID);
                        if (thismember != null) {

                            // Tell all the other members about the incoming member
                            foreach (ChatSessionMember sessionMember in session.Members) {
                                ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block =
                                    new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock {
                                        AgentID = sessionMember.AvatarKey,
                                        CanVoiceChat = sessionMember.CanVoiceChat,
                                        IsModerator = sessionMember.IsModerator,
                                        MuteText = sessionMember.MuteText,
                                        MuteVoice = sessionMember.MuteVoice,
                                        Transition = "ENTER"
                                    };
                                if (sessionMember.AvatarKey == thismember.AvatarKey) {
                                    usAgents.Add (block);
                                    notUsAgents.Add (block);
                                } else {
                                    if (sessionMember.HasBeenAdded)
                                        // Don't add not joined yet agents. They don't want to be here.
                                        notUsAgents.Add (block);
                                }
                            }
                            thismember.HasBeenAdded = true;
                            foreach (ChatSessionMember member in session.Members) {
                                if (member.HasBeenAdded) //Only send to those in the group
                                {
                                    UUID regionID = findRegionID (member.AvatarKey);
                                    if (regionID != UUID.Zero) {
                                        m_eventQueueService.ChatterBoxSessionAgentListUpdates (
                                            session.SessionID,
                                            member.AvatarKey == thismember.AvatarKey ? notUsAgents.ToArray () : usAgents.ToArray (),
                                            member.AvatarKey, "ENTER",
                                            regionID);
                                    }
                                }
                            }
                            return "Accepted";
                        }
                    }

                    return "";  // no session exists? ... or cannot find member
                }

            case "mute update": {
                    // Check if the user is a moderator
                    if (!CheckModeratorPermission (caps.AgentID, sessionID))
                        return "";

                    OSDMap parameters = (OSDMap)req ["params"];
                    UUID agentID = parameters ["agent_id"].AsUUID ();
                    OSDMap muteInfoMap = (OSDMap)parameters ["mute_info"];

                    ChatSessionMember thismember = FindMember (agentID, sessionID);
                    if (thismember == null)
                        return "";

                    if (muteInfoMap.ContainsKey ("text"))
                        thismember.MuteText = muteInfoMap ["text"].AsBoolean ();
                    if (muteInfoMap.ContainsKey ("voice"))
                        thismember.MuteVoice = muteInfoMap ["voice"].AsBoolean ();

                    ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block =
                        new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock {
                            AgentID = thismember.AvatarKey,
                            CanVoiceChat = thismember.CanVoiceChat,
                            IsModerator = thismember.IsModerator,
                            MuteText = thismember.MuteText,
                            MuteVoice = thismember.MuteVoice,
                            Transition = "ENTER"
                        };

                    ChatSession session = GetSession (sessionID);
                    // Send an update to all users so that they show the correct permissions
                    foreach (ChatSessionMember member in session.Members) {
                        if (member.HasBeenAdded) //Only send to those in the group
                        {
                            UUID regionID = findRegionID (member.AvatarKey);
                            if (regionID != UUID.Zero) {
                                m_eventQueueService.ChatterBoxSessionAgentListUpdates (
                                    sessionID, new [] { block },member.AvatarKey, "", regionID);
                            }
                        }
                    }

                    return "Accepted";
                }

            case "call": {
                    // Implement voice chat for conferences...

                    IVoiceService voiceService = m_registry.RequestModuleInterface<IVoiceService> ();
                    if (voiceService == null)
                        return "";

                    OSDMap resp = voiceService.GroupConferenceCallRequest (caps, sessionID);
                    return OSDParser.SerializeLLSDXmlString (resp);
                }
            default:
                MainConsole.Instance.Warn ("ChatSessionRequest : " + method);
                return "";
            }
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void EnsureSessionIsStarted (UUID groupID)
        {
            if (m_doRemoteOnly) {
                DoRemoteCallPost (true, "InstantMessageServerURI", groupID);
                return;
            }

            if (!SessionExists (groupID)) {
                GroupRecord groupInfo = m_groupData.GetGroupRecord (UUID.Zero, groupID, null);

                CreateSession (new ChatSession {
                    Members = new List<ChatSessionMember> (),
                    SessionID = groupID,
                    Name = groupInfo.GroupName
                });

                foreach (
                    GroupMembersData gmd in
                        m_groupData.GetGroupMembers (UUID.Zero, groupID)
                                   .Where ( gmd =>
                                       (gmd.AgentPowers & (ulong)GroupPowers.JoinChat) == (ulong)GroupPowers.JoinChat)
                    ) {
                    AddMemberToGroup (new ChatSessionMember {
                        AvatarKey = gmd.AgentID,
                        CanVoiceChat = false,
                        IsModerator = GroupPermissionCheck (gmd.AgentID, groupID, GroupPowers.ModerateChat),
                        MuteText = false,
                        MuteVoice = false,
                        HasBeenAdded = false
                    }, groupID);
                }
            }
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void CreateGroupChat (UUID agentID, GridInstantMessage im)
        {
            if (m_doRemoteOnly) {
                DoRemoteCallPost (true, "InstantMessageServerURI", agentID, im);
                return;
            }

            UUID groupSessionID = im.SessionID;

            GroupRecord groupInfo = m_groupData.GetGroupRecord (agentID, groupSessionID, null);

            if (groupInfo != null) {
                if (!GroupPermissionCheck (agentID, groupSessionID, GroupPowers.JoinChat))
                    return;     // They have to be able to join to create a group chat
                
                // Create the session.
                if (!SessionExists (groupSessionID)) {
                    CreateSession (new ChatSession {
                        Members = new List<ChatSessionMember> (),
                        SessionID = groupSessionID,
                        Name = groupInfo.GroupName
                    });
                    AddMemberToGroup (new ChatSessionMember {
                        AvatarKey = agentID,
                        CanVoiceChat = false,
                        IsModerator = GroupPermissionCheck (agentID, groupSessionID, GroupPowers.ModerateChat),
                        MuteText = false,
                        MuteVoice = false,
                        HasBeenAdded = true
                    }, groupSessionID);

                    foreach (
                        GroupMembersData gmd in
                            m_groupData.GetGroupMembers (agentID, groupSessionID)
                                       .Where (gmd => gmd.AgentID != agentID)
                                       .Where ( gmd =>
                                           (gmd.AgentPowers & (ulong)GroupPowers.JoinChat) == (ulong)GroupPowers.JoinChat)) 
                    {
                        AddMemberToGroup (new ChatSessionMember {
                            AvatarKey = gmd.AgentID,
                            CanVoiceChat = false,
                            IsModerator = GroupPermissionCheck (gmd.AgentID, groupSessionID, GroupPowers.ModerateChat),
                            MuteText = false,
                            MuteVoice = false,
                            HasBeenAdded = false
                        }, groupSessionID);
                    }
                    // Tell us that it was made successfully
                    m_eventQueueService.ChatterBoxSessionStartReply (groupInfo.GroupName, groupSessionID,
                                                                    agentID, findRegionID (agentID));
                } else {
                    ChatSession thisSession = GetSession (groupSessionID);
                    // A session already exists
                    // Add us
                    AddMemberToGroup (new ChatSessionMember {
                        AvatarKey = agentID,
                        CanVoiceChat = false,
                        IsModerator = GroupPermissionCheck (agentID, groupSessionID, GroupPowers.ModerateChat),
                        MuteText = false,
                        MuteVoice = false,
                        HasBeenAdded = true
                    }, groupSessionID);

                    // Tell us that we entered successfully
                    m_eventQueueService.ChatterBoxSessionStartReply (groupInfo.GroupName, groupSessionID,
                                                                    agentID, findRegionID (agentID));
                    
                    var usAgents = new List<ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock> ();
                    var notUsAgents = new List<ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock> ();

                    foreach (ChatSessionMember sessionMember in thisSession.Members) {
                        ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block =
                            new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock {
                                AgentID = sessionMember.AvatarKey,
                                CanVoiceChat = sessionMember.CanVoiceChat,
                                IsModerator = sessionMember.IsModerator,
                                MuteText = sessionMember.MuteText,
                                MuteVoice = sessionMember.MuteVoice,
                                Transition = "ENTER"
                            };
                        if (agentID == sessionMember.AvatarKey)
                            usAgents.Add (block);
                        if (sessionMember.HasBeenAdded)
                            // Don't add not joined yet agents. They don't want to be here.
                            notUsAgents.Add (block);
                    }
                    foreach (ChatSessionMember member in thisSession.Members) {
                        if (member.HasBeenAdded)    // Only send to those in the group
                        {
                            UUID regionID = findRegionID (member.AvatarKey);
                            if (regionID != UUID.Zero) {
                                if (member.AvatarKey == agentID) {
                                    // Tell 'us' about all the other agents in the group
                                    m_eventQueueService.ChatterBoxSessionAgentListUpdates (
                                        groupSessionID, notUsAgents.ToArray (), member.AvatarKey, "ENTER", regionID);
                                } else {
                                    // Tell 'other' agents about the new agent ('us')
                                    m_eventQueueService.ChatterBoxSessionAgentListUpdates (
                                        groupSessionID, usAgents.ToArray (), member.AvatarKey, "ENTER", regionID);
                                }
                            }
                        }
                    }
                }

                ChatSessionMember agentMember = FindMember (agentID, groupSessionID);
                if (agentMember != null) {
                    // Tell us that we entered
                    var ourblock =
                        new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock {
                            AgentID = agentID,
                            CanVoiceChat = agentMember.CanVoiceChat,
                            IsModerator = agentMember.IsModerator,
                            MuteText = agentMember.MuteText,
                            MuteVoice = agentMember.MuteVoice,
                            Transition = "ENTER"
                        };
                    m_eventQueueService.ChatterBoxSessionAgentListUpdates (
                        groupSessionID, new [] { ourblock }, agentID, "ENTER", findRegionID (agentID));
                }
            }
        }

        /// <summary>
        ///     Remove the member from this session
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="im"></param>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void DropMemberFromSession (UUID agentID, GridInstantMessage im)
        {
            if (m_doRemoteOnly) {
                DoRemoteCallPost (true, "InstantMessageServerURI", agentID, im);
                return;
            }

            ChatSession session;
            chatSessions.TryGetValue (im.SessionID, out session);
            if (session == null)
                return;
            
            ChatSessionMember member = null;
            foreach (ChatSessionMember testmember in
                     session.Members.Where (testmember => testmember.AvatarKey == im.FromAgentID))
                member = testmember;

            if (member == null)
                return;

            member.HasBeenAdded = false;
            member.RequestedRemoval = true;

            if (session.Members.Count (mem => mem.HasBeenAdded) == 0) //If a member hasn't been added, kill this anyway
            {
                chatSessions.Remove (session.SessionID);
                return;
            }

            ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block =
                new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock {
                    AgentID = member.AvatarKey,
                    CanVoiceChat = member.CanVoiceChat,
                    IsModerator = member.IsModerator,
                    MuteText = member.MuteText,
                    MuteVoice = member.MuteVoice,
                    Transition = "LEAVE"
                };
            foreach (ChatSessionMember sessionMember in session.Members) {
                if (sessionMember.HasBeenAdded) //Only send to those in the group
                {
                    UUID regionID = findRegionID (sessionMember.AvatarKey);
                    if (regionID != UUID.Zero) {
                        m_eventQueueService.ChatterBoxSessionAgentListUpdates (
                            session.SessionID, new [] { block }, sessionMember.AvatarKey, "LEAVE", regionID);
                    }
                }
            }
        }

        /// <summary>
        ///     Send chat to all the members of this friend conference
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="im"></param>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void SendChatToSession (UUID agentID, GridInstantMessage im)
        {
            if (m_doRemoteOnly) {
                DoRemoteCallPost (true, "InstantMessageServerURI", agentID, im);
                return;
            }

            Util.FireAndForget ((o) => {
                ChatSession session;
                chatSessions.TryGetValue (im.SessionID, out session);
                if (session == null)
                    return;

                if (agentID != UUID.Zero)   // Not system
                {
                    ChatSessionMember sender = FindMember (agentID, im.SessionID);
                    if (sender.MuteText)
                        return;             // They have been admin muted, don't allow them to send anything
                }

                var messagesToSend = new Dictionary<string, List<GridInstantMessage>> ();
                foreach (ChatSessionMember member in session.Members) {
                    if (member.HasBeenAdded) {
                        im.ToAgentID = member.AvatarKey;
                        im.BinaryBucket = Utils.StringToBytes (session.Name);
                        im.RegionID = UUID.Zero;
                        im.ParentEstateID = 0;
                        im.Offline = 0;
                        GridInstantMessage message = new GridInstantMessage ();
                        message.FromOSD (im.ToOSD ());
                        // im.timestamp = 0;
                        string uri = FindRegionURI (member.AvatarKey);
                        if (uri != "")  // Check if they are online
                        {
                            // Bulk send all of the instant messages to the same region, so that we don't send them one-by-one over and over
                            if (messagesToSend.ContainsKey (uri))
                                messagesToSend [uri].Add (message);
                            else
                                messagesToSend.Add (uri, new List<GridInstantMessage> { message });
                        }
                    } else if (!member.RequestedRemoval)                      // If they're requested to leave, don't recontact them
                      {
                        UUID regionID = findRegionID (member.AvatarKey);
                        if (regionID != UUID.Zero) {
                            im.ToAgentID = member.AvatarKey;
                            m_eventQueueService.ChatterboxInvitation (
                                session.SessionID,
                                session.Name,
                                im.FromAgentID,
                                im.Message,
                                im.ToAgentID,
                                im.FromAgentName,
                                im.Dialog,
                                im.Timestamp,
                                im.Offline == 1,
                                (int)im.ParentEstateID,
                                im.Position,
                                1,
                                im.SessionID,
                                false,
                                Utils.StringToBytes (session.Name),
                                regionID
                                );
                        }
                    }
                }
                foreach (KeyValuePair<string, List<GridInstantMessage>> kvp in messagesToSend) {
                    SendInstantMessages (kvp.Key, kvp.Value);
                }
            });
        }

        #endregion

        #region Session Caching

        bool SessionExists (UUID groupID)
        {
            return chatSessions.ContainsKey (groupID);
        }

        bool GroupPermissionCheck (UUID agentID, UUID groupID, GroupPowers groupPowers)
        {
            GroupMembershipData grpMD = m_groupData.GetGroupMembershipData (agentID, groupID, agentID);
            if (grpMD == null) 
                return false;
            
            return (grpMD.GroupPowers & (ulong)groupPowers) == (ulong)groupPowers;
        }

        void SendInstantMessages (string uri, List<GridInstantMessage> ims)
        {
            ISyncMessagePosterService syncMessagePoster = m_registry.RequestModuleInterface<ISyncMessagePosterService> ();
            if (syncMessagePoster != null) {
                OSDMap map = new OSDMap ();
                map ["Method"] = "SendInstantMessages";
                map ["Messages"] = ims.ToOSDArray ();
                syncMessagePoster.Post (uri, map);
            }
        }

        UUID findRegionID (UUID agentID)
        {
            IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
            UserInfo user = agentInfoService.GetUserInfo (agentID.ToString ());
            return (user != null && user.IsOnline) ? user.CurrentRegionID : UUID.Zero;
        }

        string FindRegionURI (UUID agentID)
        {
            IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
            UserInfo user = agentInfoService.GetUserInfo (agentID.ToString ());
            return (user != null && user.IsOnline) ? user.CurrentRegionURI : "";
        }

        /// <summary>
        ///     Find the member from X sessionID
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        ChatSessionMember FindMember (UUID agentID, UUID sessionID)
        {
            ChatSession session;
            chatSessions.TryGetValue (sessionID, out session);
            if (session == null)
                return null;
            
            ChatSessionMember thismember = new ChatSessionMember { AvatarKey = UUID.Zero };
            foreach (ChatSessionMember testmember in session.Members.Where (testmember => testmember.AvatarKey == agentID)) {
                thismember = testmember;
            }
            return thismember;
        }

        /// <summary>
        ///     Check whether the user has moderator permissions
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        bool CheckModeratorPermission (UUID agentID, UUID sessionID)
        {
            ChatSession session;
            chatSessions.TryGetValue (sessionID, out session);
            if (session == null)
                return false;
            ChatSessionMember thismember = new ChatSessionMember { AvatarKey = UUID.Zero };
            foreach (ChatSessionMember testmember in session.Members.Where (testmember => testmember.AvatarKey == agentID)) {
                thismember = testmember;
            }
            if (thismember == null)
                return false;
            return thismember.IsModerator;
        }

        /// <summary>
        ///     Add this member to the friend conference
        /// </summary>
        /// <param name="member"></param>
        /// <param name="sessionID"></param>
        void AddMemberToGroup (ChatSessionMember member, UUID sessionID)
        {
            ChatSession session;
            chatSessions.TryGetValue (sessionID, out session);
            ChatSessionMember oldMember;
            if ((oldMember = session.Members.Find (mem => mem.AvatarKey == member.AvatarKey)) != null) {
                oldMember.HasBeenAdded = true;
                oldMember.RequestedRemoval = false;
            } else
                session.Members.Add (member);
        }

        /// <summary>
        ///     Create a new friend conference session
        /// </summary>
        /// <param name="session"></param>
        void CreateSession (ChatSession session)
        {
            chatSessions.Add (session.SessionID, session);
        }

        /// <summary>
        ///     Get a session by a user's sessionID
        /// </summary>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        ChatSession GetSession (UUID sessionID)
        {
            ChatSession session;
            chatSessions.TryGetValue (sessionID, out session);
            return session;
        }

        /// <summary>
        ///     Add the agent to the in-memory session lists and give them the default permissions
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="sessionID"></param>
        void AddDefaultPermsMemberToSession (UUID agentID, UUID sessionID)
        {
            ChatSession session;
            chatSessions.TryGetValue (sessionID, out session);
            ChatSessionMember member = new ChatSessionMember {
                AvatarKey = agentID,
                CanVoiceChat = true,
                IsModerator = false,
                MuteText = false,
                MuteVoice = false,
                HasBeenAdded = false
            };
            session.Members.Add (member);
        }

        #endregion
    }
}
