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
using System.IO;
using System.Linq;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Groups
{
    public class GroupsModule : INonSharedRegionModule, IGroupsModule
    {
        readonly Dictionary<UUID, GroupMembershipData> m_cachedGroupTitles =
            new Dictionary<UUID, GroupMembershipData> ();

        readonly Dictionary<UUID, List<GroupMembershipData>> m_cachedGroupMemberships =
            new Dictionary<UUID, List<GroupMembershipData>> ();

        IScene m_scene;

        // Configuration settings
        bool m_debugEnabled = true;
        IGroupsServiceConnector m_groupData;
        bool m_groupNoticesEnabled = true;
        bool m_groupsEnabled;
        IMessageTransferModule m_msgTransferModule;
        IInstantMessagingService m_imService;

        #region IGroupsModule Members

        public event NewGroupNotice OnNewGroupNotice;

        public bool IsGroup (UUID groupID)
        {
            return m_groupData.IsGroup (groupID);
        }

        public GroupRecord GetGroupRecord (UUID groupID)
        {
            return m_groupData.GetGroupRecord (UUID.Zero, groupID, null);
        }

        public GroupRecord GetGroupRecord (string name)
        {
            return m_groupData.GetGroupRecord (UUID.Zero, UUID.Zero, name);
        }

        public List<UUID> GetAllGroups (UUID requestingAgettID)
        {
            return m_groupData.GetAllGroups (requestingAgettID);
        }

        public void ActivateGroup (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            string title = m_groupData.SetAgentActiveGroup (GetRequestingAgentID (remoteClient), groupID);
            m_cachedGroupTitles.Remove (remoteClient.AgentId);
            // Changing active group changes title, active powers, all kinds of things
            // anyone who is in any region that can see this client, should probably be 
            // updated with new group info.  At a minimum, they should get ScenePresence
            // updated with new title.
            UpdateAllClientsWithGroupInfo (GetRequestingAgentID (remoteClient), title);
        }

        /// <summary>
        ///     Get the Role Titles for an Agent, for a specific group
        /// </summary>
        public List<GroupTitlesData> GroupTitlesRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            return m_groupData.GetGroupTitles (GetRequestingAgentID (remoteClient), groupID);
        }

        public List<GroupMembersData> GroupMembersRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);
            List<GroupMembersData> data = m_groupData.GetGroupMembers (GetRequestingAgentID (remoteClient), groupID);

            if (m_debugEnabled) {
                foreach (GroupMembersData member in data) {
                    MainConsole.Instance.DebugFormat ("[Groups]: Member({0}) - IsOwner({1})", member.AgentID,
                                                     member.IsOwner);
                }
            }

            return data;
        }

        public List<GroupRolesData> GroupRoleDataRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            return m_groupData.GetGroupRoles (GetRequestingAgentID (remoteClient), groupID);
        }

        public List<GroupRoleMembersData> GroupRoleMembersRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            List<GroupRoleMembersData> data = m_groupData.GetGroupRoleMembers (GetRequestingAgentID (remoteClient),
                                                                              groupID);

            if (m_debugEnabled) {
                foreach (GroupRoleMembersData member in data) {
                    MainConsole.Instance.DebugFormat ("[Groups]: member({0}) - Role({1})", member.MemberID, member.RoleID);
                }
            }
            return data;
        }

        public GroupProfileData GroupProfileRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            return m_groupData.GetGroupProfile (GetRequestingAgentID (remoteClient), groupID);
        }

        public List<GroupMembersData> GetGroupMembers (UUID requestingAgentID, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            return m_groupData.GetGroupMembers (requestingAgentID, groupID);
        }

        public GroupMembershipData [] GetMembershipData (UUID agentID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            return m_groupData.GetAgentGroupMemberships (UUID.Zero, agentID).ToArray ();
        }

        public GroupMembershipData GetMembershipData (UUID groupID, UUID agentID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called with groupID={1}, agentID={2}",
                    MethodBase.GetCurrentMethod ().Name, groupID, agentID);

            return AttemptFindGroupMembershipData (UUID.Zero, agentID, groupID);
        }

        public void UpdateGroupInfo (IClientAPI remoteClient, UUID groupID, string charter, bool showInList,
                                    UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish,
                                    bool maturePublish)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            // Note: Permissions checking for modification rights is handled by the Groups Server/Service
            m_groupData.UpdateGroup (GetRequestingAgentID (remoteClient), groupID, charter, showInList ? 1 : 0, insigniaID,
                membershipFee, openEnrollment ? 1 : 0, allowPublish ? 1 : 0, maturePublish ? 1 : 0);
            NullCacheInfos (groupID);
        }

        public void SetGroupAcceptNotices (IClientAPI remoteClient, UUID groupID, bool acceptNotices, bool listInProfile)
        {
            // Note: Permissions checking for modification rights is handled by the Groups Server/Service
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            m_groupData.SetAgentGroupInfo (GetRequestingAgentID (remoteClient), GetRequestingAgentID (remoteClient),
                                          groupID, acceptNotices ? 1 : 0, listInProfile ? 1 : 0);
            NullCacheInfos (remoteClient.AgentId, groupID);
        }

        void NullCacheInfos (UUID groupID)
        {
            foreach (UUID agentID in m_cachedGroupMemberships.Keys)
                NullCacheInfos (agentID, groupID);
        }

        void NullCacheInfos (UUID agentID, UUID groupID)
        {
            if (!m_cachedGroupMemberships.ContainsKey (agentID))
                return;
            m_cachedGroupMemberships [agentID].RemoveAll ((d) => d.GroupID == groupID);
        }

        public UUID CreateGroup (IClientAPI remoteClient, string name, string charter, bool showInList, UUID insigniaID,
                                int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            if (m_groupData.GetGroupRecord (GetRequestingAgentID (remoteClient), UUID.Zero, name) != null) {
                remoteClient.SendCreateGroupReply (UUID.Zero, false, "A group with the same name already exists.");
                return UUID.Zero;
            }
            // is there is a money module present ?
            IMoneyModule money = remoteClient.Scene.RequestModuleInterface<IMoneyModule> ();
            if (money != null) {
                try {
                    // do the transaction, that is if the agent has got sufficient funds
                    if (!money.Charge (GetRequestingAgentID (remoteClient), money.GroupCreationCharge, "Group Creation", TransactionType.GroupCreate)) {
                        remoteClient.SendCreateGroupReply (UUID.Zero, false,
                                                          "You have got insufficient funds to create a group.");
                        return UUID.Zero;
                    }
                } catch {
                    remoteClient.SendCreateGroupReply (UUID.Zero, false,
                        "A money related exception occurred, please contact your grid administrator.");
                    return UUID.Zero;
                }
            }
            UUID groupID = UUID.Random ();
            m_groupData.CreateGroup (groupID, name, charter, showInList,
                                                    insigniaID, membershipFee, openEnrollment, allowPublish,
                                                    maturePublish, GetRequestingAgentID (remoteClient), UUID.Random ());

            remoteClient.SendCreateGroupReply (groupID, true, "Group created successfully");
            m_cachedGroupTitles [remoteClient.AgentId] =
                AttemptFindGroupMembershipData (remoteClient.AgentId, remoteClient.AgentId, groupID);
            m_cachedGroupMemberships.Remove (remoteClient.AgentId);
            RemoveFromGroupPowersCache (remoteClient.AgentId, remoteClient.ActiveGroupId);
            // Update the founder with new group information.
            SendAgentGroupDataUpdate (remoteClient, GetRequestingAgentID (remoteClient));

            return groupID;
        }

        public GroupNoticeData [] GroupNoticesListRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            return m_groupData.GetGroupNotices (GetRequestingAgentID (remoteClient), 0, 0, groupID).ToArray ();
        }

        /// <summary>
        ///     Get the title of the agent's current role.
        /// </summary>
        public string GetGroupTitle (UUID avatarID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            //Check the cache first
            GroupMembershipData membership;
            if (m_cachedGroupTitles.ContainsKey (avatarID))
                membership = m_cachedGroupTitles [avatarID];
            else
                membership = m_groupData.GetGroupMembershipData (avatarID, UUID.Zero, avatarID);

            if (membership != null) {
                m_cachedGroupTitles [avatarID] = membership;
                return membership.GroupTitle;
            }
            m_cachedGroupTitles [avatarID] = null;
            return string.Empty;
        }

        /// <summary>
        ///     Change the current Active Group Role for Agent
        /// </summary>
        public void GroupTitleUpdate (IClientAPI remoteClient, UUID groupID, UUID titleRoleID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            string title = m_groupData.SetAgentGroupSelectedRole (GetRequestingAgentID (remoteClient), groupID, titleRoleID);
            m_cachedGroupTitles.Remove (remoteClient.AgentId);
            // TODO: Not sure what all is needed here, but if the active group role change is for the group
            // the client currently has set active, then we need to do a scene presence update too

            UpdateAllClientsWithGroupInfo (GetRequestingAgentID (remoteClient), title);
            NullCacheInfos (remoteClient.AgentId, groupID);
        }

        public void UpdateUsersForExternalRoleUpdate (UUID groupID, UUID roleID, UUID regionID)
        {
            foreach (IScenePresence sp in m_scene.GetScenePresences ()) {
                if (sp.ControllingClient.ActiveGroupId == groupID) {
                    m_cachedGroupTitles.Remove (sp.UUID); //Remove the old title
                    m_cachedGroupMemberships.Remove (sp.UUID);
                    UpdateAllClientsWithGroupInfo (sp.UUID, GetGroupTitle (sp.UUID));
                }
                //Remove their permissions too
                RemoveFromGroupPowersCache (sp.UUID, groupID);
            }
        }

        public void GroupRoleUpdate (IClientAPI remoteClient, UUID groupID, UUID roleID, string name, string description,
                                    string title, ulong powers, byte updateType)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            // Security Checks are handled in the Groups Service.

            switch ((GroupRoleUpdate)updateType) {
            case OpenMetaverse.GroupRoleUpdate.Create:
                m_groupData.AddRoleToGroup (GetRequestingAgentID (remoteClient), groupID, UUID.Random (), name,
                                         description, title, powers);
                break;

            case OpenMetaverse.GroupRoleUpdate.Delete:
                m_groupData.RemoveRoleFromGroup (GetRequestingAgentID (remoteClient), roleID, groupID);
                break;

            case OpenMetaverse.GroupRoleUpdate.UpdateAll:
            case OpenMetaverse.GroupRoleUpdate.UpdateData:
            case OpenMetaverse.GroupRoleUpdate.UpdatePowers:
                if (m_debugEnabled) {
                    GroupPowers gp = (GroupPowers)powers;
                    MainConsole.Instance.DebugFormat ("[Groups]: Role ({0}) updated with Powers ({1}) ({2})",
                                                        name, powers, gp);
                }
                m_groupData.UpdateRole (GetRequestingAgentID (remoteClient), groupID, roleID, name, description,
                                            title, powers);
                RemoveFromGroupPowersCache (groupID);
                break;

            case OpenMetaverse.GroupRoleUpdate.NoUpdate:
            default:
                // No Op
                break;
            }

            ISyncMessagePosterService amps = m_scene.RequestModuleInterface<ISyncMessagePosterService> ();
            if (amps != null) {
                OSDMap message = new OSDMap ();
                message ["Method"] = "FixGroupRoleTitles";
                message ["GroupID"] = groupID;
                message ["RoleID"] = roleID;
                message ["AgentID"] = remoteClient.AgentId;
                message ["Type"] = updateType;
                amps.PostToServer (message);
            }

            UpdateUsersForExternalRoleUpdate (groupID, roleID, remoteClient.Scene.RegionInfo.RegionID);
        }

        public void GroupRoleChanges (IClientAPI remoteClient, UUID groupID, UUID roleID, UUID memberID, uint changes)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            switch (changes) {
            case 0:
                // Add
                m_groupData.AddAgentToRole (GetRequestingAgentID (remoteClient), memberID, groupID, roleID);

                break;
            case 1:
                // Remove
                m_groupData.RemoveAgentFromRole (GetRequestingAgentID (remoteClient), memberID, groupID, roleID);

                break;
            default:
                MainConsole.Instance.ErrorFormat ("[Groups]: {0} does not understand changes == {1}",
                                                 MethodBase.GetCurrentMethod ().Name, changes);
                break;
            }

            // TODO: This update really should send out updates for everyone in the role that just got changed.
            SendAgentGroupDataUpdate (remoteClient, GetRequestingAgentID (remoteClient));
        }

        public void GroupNoticeRequest (IClientAPI remoteClient, UUID groupNoticeID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            GroupNoticeInfo data = m_groupData.GetGroupNotice (GetRequestingAgentID (remoteClient), groupNoticeID);

            if (data != null) {
                GridInstantMessage msg = BuildGroupNoticeIM (data, groupNoticeID, remoteClient.AgentId);
                OutgoingInstantMessage (msg, GetRequestingAgentID (remoteClient));
            }
        }

        public GridInstantMessage CreateGroupNoticeIM (UUID agentID, GroupNoticeInfo info, byte dialog)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            GridInstantMessage msg = new GridInstantMessage {
                ToAgentID = agentID,
                Dialog = dialog,
                FromGroup = true,
                Offline = 1,
                ParentEstateID = 0,
                Position = Vector3.Zero,
                RegionID = UUID.Zero,
                SessionID = UUID.Random ()
            };

            // msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupNotice;
            // Allow this message to be stored for offline use

            msg.FromAgentID = info.GroupID;
            msg.Timestamp = info.noticeData.Timestamp;
            msg.FromAgentName = info.noticeData.FromName;
            msg.Message = info.noticeData.Subject + "|" + info.Message;
            if (info.noticeData.HasAttachment) {
                msg.BinaryBucket = CreateBitBucketForGroupAttachment (info.noticeData, info.GroupID);
                //Save the sessionID for the callback by the client (reject or accept)
                //Only save if has attachment
                msg.SessionID = info.noticeData.ItemID;
            } else {
                byte [] bucket = new byte [19];
                bucket [0] = 0; //Attachment enabled == false so 0
                bucket [1] = 0; //No attachment, so no asset type
                info.GroupID.ToBytes (bucket, 2);
                bucket [18] = 0; //dunno
                msg.BinaryBucket = bucket;
            }

            return msg;
        }

        public void SendAgentGroupDataUpdate (IClientAPI remoteClient)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            // Send agent information about his groups
            SendAgentGroupDataUpdate (remoteClient, GetRequestingAgentID (remoteClient));
        }

        public void JoinGroupRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            var requestingAgentID = GetRequestingAgentID (remoteClient);

            GroupRecord record = m_groupData.GetGroupRecord (requestingAgentID, groupID, "");
            if (record != null) {
                // check if this user is banned
                var isBanned = m_groupData.IsGroupBannedUser (groupID, requestingAgentID);
                if (isBanned) {
                    remoteClient.SendJoinGroupReply (groupID, false);
                    return;
                }

                // invited users (not sure if this is needed really)
                var invites = m_groupData.GetGroupInvites (requestingAgentID);
                var invitedAgent = false;
                if (invites.Count > 0) {
                    foreach (var invite in invites) {
                        if (invite.GroupID == groupID)
                            invitedAgent = true;
                    }
                }

                // open enrolment or invited
                if (record.OpenEnrollment || invitedAgent) {
                    m_groupData.AddAgentToGroup (requestingAgentID, requestingAgentID,
                        groupID,
                        UUID.Zero);

                    m_cachedGroupMemberships.Remove (remoteClient.AgentId);
                    RemoveFromGroupPowersCache (remoteClient.AgentId, groupID);
                    remoteClient.SendJoinGroupReply (groupID, true);

                    ActivateGroup (remoteClient, groupID);

                    // Should this send updates to everyone in the group?
                    SendAgentGroupDataUpdate (remoteClient, requestingAgentID);

                    return;
                }

            }
            // unable to join the group
            remoteClient.SendJoinGroupReply (groupID, false);

        }

        public void LeaveGroupRequest (IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            if (
                !m_groupData.RemoveAgentFromGroup (GetRequestingAgentID (remoteClient), GetRequestingAgentID (remoteClient),
                                                  groupID))
                return;

            m_cachedGroupMemberships.Remove (remoteClient.AgentId);
            remoteClient.SendLeaveGroupReply (groupID, true);

            remoteClient.SendAgentDropGroup (groupID);
            RemoveFromGroupPowersCache (remoteClient.AgentId, groupID);

            if (remoteClient.ActiveGroupId == groupID)
                GroupTitleUpdate (remoteClient, UUID.Zero, UUID.Zero);

            SendAgentGroupDataUpdate (remoteClient, GetRequestingAgentID (remoteClient));

            if (m_imService != null) {
                // SL sends out notifications to the group messaging session that the person has left
                GridInstantMessage im = new GridInstantMessage {
                    FromAgentID = groupID,
                    Dialog = (byte)InstantMessageDialog.SessionSend,
                    BinaryBucket = new byte [0],
                    FromAgentName = "System",
                    FromGroup = true,
                    SessionID = groupID,
                    Message = remoteClient.Name + " has left the group.",
                    Offline = 1,
                    RegionID = remoteClient.Scene.RegionInfo.RegionID,
                    Timestamp = (uint)Util.UnixTimeSinceEpoch (),
                    ToAgentID = UUID.Zero
                };

                m_imService.EnsureSessionIsStarted (groupID);
                m_imService.SendChatToSession (UUID.Zero, im);
            }
        }

        public void EjectGroupMemberRequest (IClientAPI remoteClient, UUID groupID, UUID ejecteeID)
        {
            EjectGroupMember (remoteClient, GetRequestingAgentID (remoteClient), groupID, ejecteeID);
        }

        public void EjectGroupMember (IClientAPI remoteClient, UUID agentID, UUID groupID, UUID ejecteeID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);
            if (!m_groupData.RemoveAgentFromGroup (GetRequestingAgentID (remoteClient), ejecteeID, groupID))
                return;

            m_cachedGroupMemberships.Remove (ejecteeID);
            string agentName;
            RegionInfo regionInfo;

            // remoteClient provided or just agentID?
            if (remoteClient != null) {
                agentName = remoteClient.Name;
                regionInfo = remoteClient.Scene.RegionInfo;
                remoteClient.SendEjectGroupMemberReply (agentID, groupID, true);
            } else {
                IClientAPI client = GetActiveClient (agentID);
                if (client != null) {
                    agentName = client.Name;
                    regionInfo = client.Scene.RegionInfo;
                    client.SendEjectGroupMemberReply (agentID, groupID, true);
                } else {
                    regionInfo = m_scene.RegionInfo;
                    UserAccount acc = m_scene.UserAccountService.GetUserAccount (regionInfo.AllScopeIDs, agentID);
                    if (acc != null)
                        agentName = acc.FirstName + " " + acc.LastName;
                    else
                        agentName = "Unknown member";
                }
            }

            GroupRecord groupInfo = m_groupData.GetGroupRecord (GetRequestingAgentID (remoteClient), groupID, null);

            UserAccount account = m_scene.UserAccountService.GetUserAccount (regionInfo.AllScopeIDs, ejecteeID);

            if ((groupInfo == null) || (account == null))
                return;

            // Send Message to avatar being ejected from the group
            GridInstantMessage msg = new GridInstantMessage {
                SessionID = UUID.Zero,
                FromAgentID = UUID.Zero,
                ToAgentID = ejecteeID,
                Timestamp = 0,
                FromAgentName = "System",
                Message = string.Format ("You have been ejected from '{1}' by {0}.",
                                                               agentName, groupInfo.GroupName),
                Dialog = 210,
                FromGroup = false,
                Offline = 0,
                ParentEstateID = 0,
                Position = Vector3.Zero,
                RegionID = remoteClient.Scene.RegionInfo.RegionID,
                BinaryBucket = new byte [0]
            };

            OutgoingInstantMessage (msg, ejecteeID);

            //Do this here for local agents, otherwise it never gets done
            IClientAPI ejectee = GetActiveClient (ejecteeID);
            if (ejectee != null) {
                msg.Dialog = (byte)InstantMessageDialog.MessageFromAgent;
                OutgoingInstantMessage (msg, ejecteeID);
                ejectee.SendAgentDropGroup (groupID);
            }


            // Message to ejected person
            // Interop, received special 210 code for ejecting a group member
            // this only works within the comms servers domain, and won't work hypergrid

            m_cachedGroupTitles [ejecteeID] = null;
            RemoveFromGroupPowersCache (ejecteeID, groupID);
            UpdateAllClientsWithGroupInfo (ejecteeID, "");

            if (m_imService != null) {
                // SL sends out notifcations to the group messaging session that the person has left
                GridInstantMessage im = new GridInstantMessage {
                    FromAgentID = groupID,
                    Dialog = (byte)InstantMessageDialog.SessionSend,
                    BinaryBucket = new byte [0],
                    FromAgentName = "System",
                    FromGroup = true,
                    SessionID = groupID,
                    Message = account.Name + " has been ejected from the group by " + remoteClient.Name + ".",
                    Offline = 1,
                    RegionID = remoteClient.Scene.RegionInfo.RegionID,
                    Timestamp = (uint)Util.UnixTimeSinceEpoch (),
                    ToAgentID = UUID.Zero
                };

                m_imService.EnsureSessionIsStarted (groupID);
                m_imService.SendChatToSession (groupID, im);
            }
        }

        public void InviteGroupRequest (IClientAPI remoteClient, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            InviteGroup (remoteClient, GetRequestingAgentID (remoteClient), groupID, invitedAgentID, roleID);
        }

        public void InviteGroup (IClientAPI remoteClient, UUID agentID, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            string agentName;
            UUID agentUUID;
            RegionInfo regionInfo;
            // remoteClient provided or just agentID?
            if (remoteClient != null) {
                agentUUID = GetRequestingAgentID (remoteClient);
                agentName = remoteClient.Name;
                regionInfo = remoteClient.Scene.RegionInfo; 
            } else {
                agentUUID = agentID;
                IClientAPI client = GetActiveClient (agentID);
                if (client != null) {
                    agentName = client.Name;
                    regionInfo = client.Scene.RegionInfo;   
                } else {
                    regionInfo = m_scene.RegionInfo;
                    UserAccount account = m_scene.UserAccountService.GetUserAccount (regionInfo.AllScopeIDs, agentID);
                    if (account != null)
                        agentName = account.FirstName + " " + account.LastName;
                    else
                        agentName = "Unknown member";
                }
            }

            UUID InviteID = UUID.Random ();

            m_groupData.AddAgentGroupInvite (agentUUID, InviteID, groupID, roleID,
                                              invitedAgentID, agentName);

            // Check to see if the invite went through, if it did not then it's possible
            // the remoteClient did not validate or did not have permission to invite.
            GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite (invitedAgentID, InviteID);

            if (inviteInfo != null) {
                if (m_msgTransferModule != null) {
                    UUID inviteUUID = InviteID;

                    GridInstantMessage msg = new GridInstantMessage {
                        SessionID = inviteUUID,
                        FromAgentID = groupID,
                        ToAgentID = invitedAgentID,
                        Timestamp = 0,
                        FromAgentName = agentName
                    };
                    // msg.fromAgentID = GetRequestingAgentID(remoteClient).Guid;
                    // msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                    GroupRecord groupInfo = GetGroupRecord (groupID);
                    string MemberShipCost = ". There is no cost to join this group.";
                    if (groupInfo.MembershipFee != 0) {
                        MemberShipCost = ". To join, you must pay " + groupInfo.MembershipFee + ".";
                    }
                    msg.Message = string.Format ("{0} has invited you to join " + groupInfo.GroupName + MemberShipCost,
                                                agentName);
                    msg.Dialog = (byte)InstantMessageDialog.GroupInvitation;
                    msg.FromGroup = true;
                    msg.Offline = 0;
                    msg.ParentEstateID = 0;
                    msg.Position = Vector3.Zero;
                    msg.RegionID = regionInfo.RegionID;
                    msg.BinaryBucket = new byte [20];
                    OutgoingInstantMessage (msg, invitedAgentID);
                }
            }
        }

        public GridInstantMessage BuildOfflineGroupNotice (GridInstantMessage msg)
        {
            msg.Dialog = 211; //We set this so that it isn't taken the wrong way later
            return msg;
        }

        #endregion

        #region Client/Update Tools

        public void UpdateCachedData (UUID agentID, CachedUserInfo cachedInfo)
        {
            //Update the cache
            RemoveFromGroupPowersCache (agentID, UUID.Zero);
            m_cachedGroupTitles [agentID] = cachedInfo.ActiveGroup;
            m_cachedGroupMemberships [agentID] = cachedInfo.GroupMemberships;
        }

        /// <summary>
        ///     Try to find an active IClientAPI reference for agentID giving preference to root connections
        /// </summary>
        IClientAPI GetActiveClient (UUID agentID)
        {
            IClientAPI child = null;

            // Try root avatar first
            IScenePresence user;
            foreach (IScene scene in MainConsole.Instance.ConsoleScenes) {
                if (scene.TryGetScenePresence (agentID, out user)) {
                    if (!user.IsChildAgent)
                        return user.ControllingClient;

                    child = user.ControllingClient;
                }
            }

            // If we didn't find a root, then just return whichever child we found, or null if none
            return child;
        }

        /// <summary>
        ///     Send 'remoteClient' the group membership 'data' for agent 'dataForAgentID'.
        /// </summary>
        void SendGroupMembershipInfoViaCaps (IClientAPI remoteClient, UUID dataForAgentID,
                                                    GroupMembershipData [] data)
        {
            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            OSDArray AgentData = new OSDArray (1);
            OSDMap AgentDataMap = new OSDMap (1) { { "AgentID", OSD.FromUUID (dataForAgentID) } };
            AgentData.Add (AgentDataMap);


            OSDArray GroupData = new OSDArray (data.Length);
            OSDArray NewGroupData = new OSDArray (data.Length);

            foreach (GroupMembershipData membership in data) {
                if (GetRequestingAgentID (remoteClient) != dataForAgentID) {
                    if (!membership.ListInProfile) {
                        // If we're sending group info to remoteclient about another agent, 
                        // filter out groups the other agent doesn't want to share.
                        continue;
                    }
                }

                OSDMap GroupDataMap = new OSDMap (6);
                OSDMap NewGroupDataMap = new OSDMap (1);

                GroupDataMap.Add ("GroupID", OSD.FromUUID (membership.GroupID));
                GroupDataMap.Add ("GroupPowers", OSD.FromULong (membership.GroupPowers));
                GroupDataMap.Add ("AcceptNotices", OSD.FromBoolean (membership.AcceptNotices));
                GroupDataMap.Add ("GroupInsigniaID", OSD.FromUUID (membership.GroupPicture));
                GroupDataMap.Add ("Contribution", OSD.FromInteger (membership.Contribution));
                GroupDataMap.Add ("GroupName", OSD.FromString (membership.GroupName));
                NewGroupDataMap.Add ("ListInProfile", OSD.FromBoolean (membership.ListInProfile));

                GroupData.Add (GroupDataMap);
                NewGroupData.Add (NewGroupDataMap);
            }

            OSDMap llDataStruct = new OSDMap (3)
                                      {
                                          {"AgentData", AgentData},
                                          {"GroupData", GroupData},
                                          {"NewGroupData", NewGroupData}
                                      };

            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: {0}", OSDParser.SerializeJsonString (llDataStruct));

            IEventQueueService queue = remoteClient.Scene.RequestModuleInterface<IEventQueueService> ();

            if (queue != null)
                queue.Enqueue (BuildEvent ("AgentGroupDataUpdate", llDataStruct), GetRequestingAgentID (remoteClient),
                              remoteClient.Scene.RegionInfo.RegionID);
        }

        public OSD BuildEvent (string eventName, OSD eventBody)
        {
            OSDMap llsdEvent = new OSDMap (2) { { "body", eventBody }, { "message", new OSDString (eventName) } };

            return llsdEvent;
        }

        void SendScenePresenceUpdate (UUID agentID, string title)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: Updating scene title for {0} with title: {1}", agentID,
                                                 title);

            IScenePresence presence = m_scene.GetScenePresence (agentID);
            if (presence != null && !presence.IsChildAgent) {
                //Force send a full update
                foreach (
                    IScenePresence sp in
                        m_scene.GetScenePresences ()
                               .Where (sp => sp.SceneViewer.Culler.ShowEntityToClient (sp, presence, m_scene))) {
                    sp.ControllingClient.SendAvatarDataImmediate (presence);
                }
            }
        }

        /// <summary>
        ///     Send updates to all clients who might be interested in groups data for dataForClientID
        /// </summary>
        void UpdateAllClientsWithGroupInfo (UUID dataForAgentID, string activeGroupTitle)
        {
            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            // TODO: Probably isn't nessesary to update every client in every scene.
            // Need to examine client updates and do only what's nessesary.

            List<GroupMembershipData> membershipData = m_cachedGroupMemberships.ContainsKey (dataForAgentID)
                                                           ? m_cachedGroupMemberships [dataForAgentID]
                                                           : m_groupData.GetAgentGroupMemberships (dataForAgentID,
                                                                                                  dataForAgentID);

            m_scene.ForEachClient (delegate (IClientAPI client) {
                if (m_debugEnabled)
                    MainConsole.Instance.InfoFormat (
                        "[Groups]: SendAgentGroupDataUpdate called for {0}", client.Name);

                // TODO: All the client update functions need to be re-examined 
                // because most do too much and send too much stuff
                OnAgentDataUpdateRequest (client, dataForAgentID, UUID.Zero, false);

                GroupMembershipData [] membershipArray;
                if (client.AgentId != dataForAgentID) {
                    Predicate<GroupMembershipData> showInProfile =
                        membership => membership.ListInProfile;

                    membershipArray = membershipData.FindAll (showInProfile).ToArray ();
                } else
                    membershipArray = membershipData.ToArray ();

                SendGroupMembershipInfoViaCaps (client, dataForAgentID, membershipArray);
            });
            SendScenePresenceUpdate (dataForAgentID, activeGroupTitle);
        }

        /// <summary>
        ///     Update remoteClient with group information about dataForAgentID
        /// </summary>
        void SendAgentGroupDataUpdate (IClientAPI remoteClient, UUID dataForAgentID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: SendAgentGroupDataUpdate called for {0}", remoteClient.Name);

            // TODO: All the client update functions need to be re-examined 
            // because most do too much and send too much stuff
            OnAgentDataUpdateRequest (remoteClient, dataForAgentID, UUID.Zero);

            GroupMembershipData [] membershipArray = GetProfileListedGroupMemberships (remoteClient, dataForAgentID);
            SendGroupMembershipInfoViaCaps (remoteClient, dataForAgentID, membershipArray);
        }

        /// <summary>
        ///     Update remoteClient with group information about dataForAgentID
        /// </summary>
        void SendNewAgentGroupDataUpdate (IClientAPI remoteClient)
        {
            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: SendAgentGroupDataUpdate called for {0}", remoteClient.Name);

            // TODO: All the client update functions need to be re-examined 
            // because most do too much and send too much stuff
            OnAgentDataUpdateRequest (remoteClient, remoteClient.AgentId, UUID.Zero, false);

            GroupMembershipData [] membershipArray = GetProfileListedGroupMemberships (remoteClient, remoteClient.AgentId);
            SendGroupMembershipInfoViaCaps (remoteClient, remoteClient.AgentId, membershipArray);
        }

        /// <summary>
        ///     Get a list of groups memberships for the agent that are marked "ListInProfile"
        /// </summary>
        /// <param name="requestingClient"></param>
        /// <param name="dataForAgentID"></param>
        /// <returns></returns>
        GroupMembershipData [] GetProfileListedGroupMemberships (IClientAPI requestingClient, UUID dataForAgentID)
        {
            List<GroupMembershipData> membershipData = m_cachedGroupMemberships.ContainsKey (dataForAgentID)
                                                           ? m_cachedGroupMemberships [dataForAgentID]
                                                           : m_groupData.GetAgentGroupMemberships (
                                                               requestingClient.AgentId,
                                                               dataForAgentID);
            GroupMembershipData [] membershipArray;

            if (requestingClient.AgentId != dataForAgentID) {
                Predicate<GroupMembershipData> showInProfile =
                    membership => membership.ListInProfile;

                membershipArray = membershipData.FindAll (showInProfile).ToArray ();
            } else {
                membershipArray = membershipData.ToArray ();
            }

            if (m_debugEnabled) {
                MainConsole.Instance.InfoFormat ("[Groups]: Get group membership information for {0} requested by {1}",
                                                dataForAgentID,
                                                requestingClient.AgentId);
                foreach (GroupMembershipData membership in membershipArray) {
                    MainConsole.Instance.InfoFormat ("[Groups]: {0} :: {1} - {2} - {3}", dataForAgentID,
                                                    membership.GroupName,
                                                    membership.GroupTitle, membership.GroupPowers);
                }
            }

            return membershipArray;
        }

        #endregion

        #region IM Backed Processes

        public void NotifyChange (UUID groupID)
        {
            // Notify all group members of a change in group roles and/or
            // permissions
            //
        }

        void OutgoingInstantMessage (GridInstantMessage msg, UUID msgTo)
        {
            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            IClientAPI localClient = GetActiveClient (msgTo);
            if (localClient != null) {
                if (m_debugEnabled)
                    MainConsole.Instance.InfoFormat ("[Groups]: MsgTo ({0}) is local, delivering directly",
                                                    localClient.Name);
                localClient.SendInstantMessage (msg);
            } else {
                if (m_debugEnabled)
                    MainConsole.Instance.InfoFormat (
                        "[Groups]: MsgTo ({0}) is not local, delivering via TransferModule", msgTo);
                m_msgTransferModule.SendInstantMessage (msg);
            }
        }

        void OutgoingInstantMessage (GridInstantMessage msg, UUID msgTo, bool localOnly)
        {
            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            IClientAPI localClient = GetActiveClient (msgTo);
            if (localClient != null) {
                if (m_debugEnabled)
                    MainConsole.Instance.InfoFormat ("[Groups]: MsgTo ({0}) is local, delivering directly",
                                                    localClient.Name);
                localClient.SendInstantMessage (msg);
            } else if (!localOnly) {
                if (m_debugEnabled)
                    MainConsole.Instance.InfoFormat (
                        "[Groups]: MsgTo ({0}) is not local, delivering via TransferModule", msgTo);
                m_msgTransferModule.SendInstantMessage (msg);
            }
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise (IConfigSource config)
        {
            IConfig groupsConfig = config.Configs ["Groups"];

            if (groupsConfig == null) {
                // Do not run this module by default.
                return;
            }

            m_groupsEnabled = groupsConfig.GetBoolean ("Enabled", false);
            if (!m_groupsEnabled) {
                return;
            }

            if (groupsConfig.GetString ("Module", "Default") != Name) {
                m_groupsEnabled = false;
                return;
            }

            //MainConsole.Instance.InfoFormat("[Groups]: Initializing {0}", this.Name);

            m_groupNoticesEnabled = groupsConfig.GetBoolean ("NoticesEnabled", true);
            m_debugEnabled = groupsConfig.GetBoolean ("DebugEnabled", true);

        }

        public void AddRegion (IScene scene)
        {
            if (m_groupsEnabled)
                scene.RegisterModuleInterface<IGroupsModule> (this);
        }

        public void RegionLoaded (IScene scene)
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            m_groupData = Framework.Utilities.DataManager.RequestPlugin<IGroupsServiceConnector> ();

            // No Groups Service Connector, then nothing works...
            if (m_groupData == null) {
                m_groupsEnabled = false;
                MainConsole.Instance.Error ("[Groups]: Could not get IGroupsServicesConnector");
                Close ();
                return;
            }

            m_msgTransferModule = scene.RequestModuleInterface<IMessageTransferModule> ();
            m_imService = scene.RequestModuleInterface<IInstantMessagingService> ();

            // No message transfer module, no notices, group invites, rejects, ejects, etc
            if (m_msgTransferModule == null) {
                m_groupsEnabled = false;
                MainConsole.Instance.Error ("[Groups]: Could not get MessageTransferModule");
                Close ();
                return;
            }

            m_scene = scene;

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnClosingClient += OnClosingClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnClientLogin += EventManager_OnClientLogin;
            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            scene.EventManager.OnCachedUserInfo += UpdateCachedData;
            // The InstantMessageModule itself doesn't do this, 
            // so lets see if things explode if we don't do it
            // scene.EventManager.OnClientClosed += OnClientClosed;
        }

        public void RemoveRegion (IScene scene)
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            m_scene = null;

            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
            scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            scene.EventManager.OnClientLogin -= EventManager_OnClientLogin;
            scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            scene.EventManager.OnCachedUserInfo -= UpdateCachedData;
        }

        public void Close ()
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) MainConsole.Instance.Debug ("[Groups]: Shutting down Groups module.");
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public string Name {
            get { return "GroupsModule"; }
        }

        #endregion

        #region EventHandlers

        void OnNewClient (IClientAPI client)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            client.OnUUIDGroupNameRequest += HandleUUIDGroupNameRequest;
            client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
            client.OnDirFindQuery += OnDirFindQuery;
            client.OnRequestAvatarProperties += OnRequestAvatarProperties;
            client.OnGroupActiveProposalsRequest += GroupActiveProposalsRequest;
            client.OnGroupVoteHistoryRequest += GroupVoteHistoryRequest;
            client.OnGroupProposalBallotRequest += GroupProposalBallotRequest;

            // Used for Notices and Group Invites/Accept/Reject
            client.OnInstantMessage += OnInstantMessage;
        }

        protected void OnMakeRootAgent (IScenePresence sp)
        {
            // Send client their groups information.
            if (sp != null && !sp.IsChildAgent) {
                RemoveFromGroupPowersCache (sp.UUID, UUID.Zero);
                SendNewAgentGroupDataUpdate (sp.ControllingClient);
            }
        }

        void OnClosingClient (IClientAPI client)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            client.OnUUIDGroupNameRequest -= HandleUUIDGroupNameRequest;
            client.OnAgentDataUpdateRequest -= OnAgentDataUpdateRequest;
            client.OnDirFindQuery -= OnDirFindQuery;
            client.OnRequestAvatarProperties -= OnRequestAvatarProperties;

            // Used for Notices and Group Invites/Accept/Reject
            client.OnInstantMessage -= OnInstantMessage;

            //Remove them from the cache
            m_cachedGroupTitles.Remove (client.AgentId);
            RemoveFromGroupPowersCache (client.AgentId, UUID.Zero);
        }

        void GroupProposalBallotRequest (IClientAPI client, UUID agentID, UUID sessionID, UUID groupID,
                                                UUID proposalID, string vote)
        {
            m_groupData.VoteOnActiveProposals (agentID, groupID, proposalID, vote);
        }

        void GroupVoteHistoryRequest (IClientAPI client, UUID agentID, UUID sessionID, UUID groupID,
                                             UUID transactionID)
        {
            List<GroupProposalInfo> inactiveProposals = m_groupData.GetInactiveProposals (client.AgentId, groupID);
            foreach (GroupProposalInfo proposal in inactiveProposals) {
                GroupVoteHistoryItem [] votes = new GroupVoteHistoryItem [1];
                votes [0] = new GroupVoteHistoryItem ();
                votes [0].CandidateID = proposal.VoteID;
                votes [0].NumVotes = proposal.NumVotes;
                votes [0].VoteCast = proposal.Result ? "Yes" : "No";
                GroupVoteHistory history = new GroupVoteHistory ();
                history.EndDateTime = Util.BuildYMDDateString (proposal.Ending);
                history.Majority = proposal.Majority.ToString ();
                history.ProposalText = proposal.Text;
                history.Quorum = proposal.Quorum.ToString ();
                history.StartDateTime = Util.BuildYMDDateString (proposal.Created);
                history.VoteID = proposal.VoteID.ToString ();
                history.VoteInitiator = proposal.BallotInitiator.ToString ();
                history.VoteResult = proposal.Result ? "Success" : "Failure";
                history.VoteType = "Proposal"; //Must be set to this, or the viewer won't show it
                client.SendGroupVoteHistory (groupID, transactionID, history, votes);
            }
        }

        void GroupActiveProposalsRequest (IClientAPI client, UUID agentID, UUID sessionID, UUID groupID,
                                                 UUID transactionID)
        {
            List<GroupProposalInfo> activeProposals = m_groupData.GetActiveProposals (client.AgentId, groupID);
            GroupActiveProposals [] proposals = new GroupActiveProposals [activeProposals.Count];
            int i = 0;
            foreach (GroupProposalInfo proposal in activeProposals) {
                proposals [i] = new GroupActiveProposals ();
                proposals [i].ProposalText = proposal.Text;
                proposals [i].Majority = proposal.Majority.ToString ();
                proposals [i].Quorum = proposal.Quorum.ToString ();
                proposals [i].StartDateTime = Util.BuildYMDDateString (proposal.Created);
                proposals [i].TerseDateID = "";
                proposals [i].VoteID = proposal.VoteID.ToString ();
                proposals [i].VoteInitiator = proposal.BallotInitiator.ToString ();
                proposals [i].VoteAlreadyCast = proposal.VoteCast != "";
                proposals [i].VoteCast = proposal.VoteCast;
                proposals [i++].EndDateTime = Util.BuildYMDDateString (proposal.Ending);
            }
            client.SendGroupActiveProposals (groupID, transactionID, proposals);
        }

        byte [] GroupProposalBallot (string request, UUID agentID)
        {
            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml (request);

            UUID groupID = map ["group-id"].AsUUID ();
            UUID proposalID = map ["proposal-id"].AsUUID ();
            string vote = map ["vote"].AsString ();

            m_groupData.VoteOnActiveProposals (agentID, groupID, proposalID, vote);

            OSDMap resp = new OSDMap ();
            resp ["voted"] = OSD.FromBoolean (true);
            return OSDParser.SerializeLLSDXmlBytes (resp);
        }

        byte [] StartGroupProposal (string request, UUID agentID)
        {
            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml (request);

            int duration = map ["duration"].AsInteger ();
            UUID group = map ["group-id"].AsUUID ();
            double majority = map ["majority"].AsReal ();
            string text = map ["proposal-text"].AsString ();
            int quorum = map ["quorum"].AsInteger ();
            UUID session = map ["session-id"].AsUUID ();

            GroupProposalInfo info = new GroupProposalInfo {
                GroupID = group,
                Majority = (float)majority,
                Quorum = quorum,
                Session = session,
                Text = text,
                Duration = duration,
                BallotInitiator = agentID,
                Created = DateTime.Now,
                Ending = DateTime.Now.AddSeconds (duration),
                VoteID = UUID.Random ()
            };

            m_groupData.AddGroupProposal (agentID, info);

            OSDMap resp = new OSDMap ();
            resp ["voted"] = OSD.FromBoolean (true);
            return OSDParser.SerializeLLSDXmlBytes (resp);
        }

        void OnRequestAvatarProperties (IClientAPI remoteClient, UUID avatarID)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            //GroupMembershipData[] avatarGroups = m_groupData.GetAgentGroupMemberships(GetRequestingAgentID(remoteClient), avatarID).ToArray();
            GroupMembershipData [] avatarGroups = GetProfileListedGroupMemberships (remoteClient, avatarID);
            remoteClient.SendAvatarGroupsReply (avatarID, avatarGroups);
        }

        void EventManager_OnClientLogin (IClientAPI client)
        {
            if (client.Scene.GetScenePresence (client.AgentId).IsChildAgent)
                return;

            List<GroupInviteInfo> inviteInfo = m_groupData.GetGroupInvites (client.AgentId);

            if (inviteInfo.Count != 0) {
                foreach (GroupInviteInfo Invite in inviteInfo) {
                    if (m_msgTransferModule != null) {
                        UUID inviteUUID = Invite.InviteID;

                        GridInstantMessage msg = new GridInstantMessage {
                            SessionID = inviteUUID,
                            FromAgentID = Invite.GroupID,
                            ToAgentID = Invite.AgentID,
                            Timestamp = (uint)Util.UnixTimeSinceEpoch (),
                            FromAgentName = Invite.FromAgentName,
                            RegionID = client.Scene.RegionInfo.RegionID
                        };


                        GroupRecord groupInfo = GetGroupRecord (Invite.GroupID);
                        string MemberShipCost = ". There is no cost to join this group.";
                        if (groupInfo.MembershipFee != 0)
                            MemberShipCost = ". To join, you must pay " + groupInfo.MembershipFee + ".";

                        msg.Message =
                            string.Format ("{0} has invited you to join " + groupInfo.GroupName + MemberShipCost,
                                          Invite.FromAgentName);
                        msg.Dialog = (byte)InstantMessageDialog.GroupInvitation;
                        msg.FromGroup = true;
                        msg.Offline = 0;
                        msg.ParentEstateID = 0;
                        msg.Position = Vector3.Zero;
                        msg.RegionID = UUID.Zero;
                        msg.BinaryBucket = new byte [20];

                        OutgoingInstantMessage (msg, Invite.AgentID);
                    }
                }
            }
        }

        /*
         * This becomes very problematic in a shared module.  In a shared module you may have more then one
         * reference to IClientAPI's, one for 0 or 1 root connections, and 0 or more child connections.
         * The OnClientClosed event does not provide anything to indicate which one of those should be closed
         * nor does it provide what scene it was from so that the specific reference can be looked up.
         * The InstantMessageModule.cs does not currently worry about unregistering the handles, 
         * and it should be an issue, since it's the client that references us not the other way around
         * , so as long as we don't keep a reference to the client laying around, the client can still be GC'ed
        void OnClientClosed(UUID AgentId)
        {
            if (m_debugEnabled) MainConsole.Instance.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            lock (m_ActiveClients)
            {
                if (m_ActiveClients.ContainsKey(AgentId))
                {
                    IClientAPI client = m_ActiveClients[AgentId];
                    client.OnUUIDGroupNameRequest -= HandleUUIDGroupNameRequest;
                    client.OnAgentDataUpdateRequest -= OnAgentDataUpdateRequest;
                    client.OnDirFindQuery -= OnDirFindQuery;
                    client.OnInstantMessage -= OnInstantMessage;

                    m_ActiveClients.Remove(AgentId);
                }
                else
                {
                    if (m_debugEnabled) MainConsole.Instance.WarnFormat("[Groups]: Client closed that wasn't registered here.");
                }

                
            }
        }
        */

        void OnDirFindQuery (IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags,
                                    int queryStart)
        {
            if (((DirectoryManager.DirFindFlags)queryFlags & DirectoryManager.DirFindFlags.Groups) ==
                DirectoryManager.DirFindFlags.Groups) {
                if (m_debugEnabled)
                    MainConsole.Instance.DebugFormat (
                        "[Groups]: {0} called with queryText({1}) queryFlags({2}) queryStart({3})",
                        MethodBase.GetCurrentMethod ().Name, queryText, (DirectoryManager.DirFindFlags)queryFlags,
                        queryStart);

                remoteClient.SendDirGroupsReply (queryID,
                                                m_groupData.FindGroups (GetRequestingAgentID (remoteClient),
                                                                        queryText,
                                                                       (uint)queryStart,
                                                                        50,
                                                                        queryFlags).ToArray ());
            }
        }

        void OnAgentDataUpdateRequest (IClientAPI remoteClient, UUID dataForAgentID, UUID sessionID)
        {
            OnAgentDataUpdateRequest (remoteClient, dataForAgentID, sessionID, true);
        }

        void OnAgentDataUpdateRequest (IClientAPI remoteClient, UUID dataForAgentID, UUID sessionID, bool sendToAll)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            UUID activeGroupID = UUID.Zero;
            string activeGroupTitle = string.Empty;
            string activeGroupName = string.Empty;
            ulong activeGroupPowers = (ulong)GroupPowers.None;

            GroupMembershipData membership = m_cachedGroupTitles.ContainsKey (dataForAgentID)
                                                 ? m_cachedGroupTitles [dataForAgentID]
                                                 : m_groupData.GetGroupMembershipData (
                                                     GetRequestingAgentID (remoteClient),
                                                     UUID.Zero,
                                                     dataForAgentID);
            m_cachedGroupTitles [dataForAgentID] = membership;
            if (membership != null) {
                activeGroupID = membership.GroupID;
                activeGroupTitle = membership.GroupTitle;
                activeGroupPowers = membership.GroupPowers;
                activeGroupName = membership.GroupName;
            }

            //Gotta tell the client about their groups
            remoteClient.SendAgentDataUpdate (dataForAgentID, activeGroupID, remoteClient.Name, activeGroupPowers, activeGroupName,
                                             activeGroupTitle);

            if (sendToAll)
                SendScenePresenceUpdate (dataForAgentID, activeGroupTitle);
        }

        void HandleUUIDGroupNameRequest (UUID groupID, IClientAPI remoteClient)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            string GroupName;

            GroupRecord group = m_groupData.GetGroupRecord (GetRequestingAgentID (remoteClient), groupID, null);
            GroupName = group != null ? group.GroupName : "Unknown";

            remoteClient.SendGroupNameReply (groupID, GroupName);
        }

        void OnInstantMessage (IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled)
                MainConsole.Instance.DebugFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            // Group invitations
            if ((im.Dialog == (byte)InstantMessageDialog.GroupInvitationAccept) ||
                (im.Dialog == (byte)InstantMessageDialog.GroupInvitationDecline)) {
                UUID inviteID = im.SessionID;
                GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite (GetRequestingAgentID (remoteClient),
                                                                               inviteID);

                if (inviteInfo == null) {
                    if (m_debugEnabled)
                        MainConsole.Instance.WarnFormat (
                            "[Groups]: Received an Invite IM for an invite that does not exist {0}.",
                            inviteID);
                    return;
                }

                if (m_debugEnabled)
                    MainConsole.Instance.DebugFormat ("[Groups]: Invite is for Agent {0} to Group {1}.",
                                                     inviteInfo.AgentID,
                                                     inviteInfo.GroupID);

                UUID fromAgentID = im.FromAgentID;
                if ((inviteInfo != null) && (fromAgentID == inviteInfo.AgentID)) {
                    // Accept
                    if (im.Dialog == (byte)InstantMessageDialog.GroupInvitationAccept) {
                        if (m_debugEnabled)
                            MainConsole.Instance.DebugFormat ("[Groups]: Received an accept invite notice.");

                        // and the sessionid is the role
                        List<UUID> allScopeIDs;
                        if (remoteClient != null)
                            allScopeIDs = remoteClient.AllScopeIDs;
                        else
                            allScopeIDs = new List<UUID> ();
                        UserAccount account = m_scene.UserAccountService.GetUserAccount (allScopeIDs, inviteInfo.FromAgentName);
                        if (account != null) {
                            m_groupData.AddAgentToGroup (account.PrincipalID, inviteInfo.AgentID, inviteInfo.GroupID,
                                                        inviteInfo.RoleID);

                            GridInstantMessage msg = new GridInstantMessage {
                                SessionID = UUID.Zero,
                                FromAgentID = UUID.Zero,
                                ToAgentID = inviteInfo.AgentID,
                                Timestamp = (uint)Util.UnixTimeSinceEpoch (),
                                FromAgentName = "Groups",
                                Message = string.Format ("You have been added to the group."),
                                Dialog = (byte)InstantMessageDialog.MessageBox,
                                FromGroup = false,
                                Offline = 0,
                                ParentEstateID = 0,
                                Position = Vector3.Zero,
                                RegionID = UUID.Zero,
                                BinaryBucket = new byte [0]
                            };

                            OutgoingInstantMessage (msg, inviteInfo.AgentID);

                            GroupMembershipData gmd =
                                AttemptFindGroupMembershipData (inviteInfo.AgentID, inviteInfo.AgentID, inviteInfo.GroupID);
                            if (gmd != null) {
                                m_cachedGroupTitles [inviteInfo.AgentID] = gmd;
                                m_cachedGroupMemberships.Remove (GetRequestingAgentID (remoteClient));
                                RemoveFromGroupPowersCache (inviteInfo.AgentID, inviteInfo.GroupID);
                                UpdateAllClientsWithGroupInfo (inviteInfo.AgentID, gmd.GroupTitle);
                                if (remoteClient != null)
                                    SendAgentGroupDataUpdate (remoteClient);
                            }
                            m_groupData.RemoveAgentInvite (GetRequestingAgentID (remoteClient), inviteID);
                        }
                    }

                    // Reject
                    if (im.Dialog == (byte)InstantMessageDialog.GroupInvitationDecline) {
                        if (m_debugEnabled)
                            MainConsole.Instance.DebugFormat ("[Groups]: Received a reject invite notice.");
                        m_groupData.RemoveAgentInvite (GetRequestingAgentID (remoteClient), inviteID);
                    }
                    RemoveFromGroupPowersCache (GetRequestingAgentID (remoteClient), inviteInfo.GroupID);
                }
            }

            // Group notices
            switch (im.Dialog) {
            case (byte)InstantMessageDialog.GroupNotice: {
                    if (!m_groupNoticesEnabled)
                        return;

                    UUID GroupID = im.ToAgentID;
                    if (m_groupData.GetGroupRecord (GetRequestingAgentID (remoteClient), GroupID, null) != null) {
                        UUID NoticeID = UUID.Random ();
                        string Subject = im.Message.Substring (0, im.Message.IndexOf ('|'));
                        string Message = im.Message.Substring (Subject.Length + 1);

                        byte [] bucket;
                        UUID ItemID = UUID.Zero;
                        int GroupAssetType = 0;
                        string ItemName = "";

                        if ((im.BinaryBucket.Length == 1) && (im.BinaryBucket [0] == 0)) {
                            bucket = new byte [19];
                            bucket [0] = 0;
                            bucket [1] = 0;
                            GroupID.ToBytes (bucket, 2);
                            bucket [18] = 0;
                        } else {
                            //bucket = im.BinaryBucket;
                            string binBucket = Utils.BytesToString (im.BinaryBucket);
                            binBucket = binBucket.Remove (0, 14).Trim ();

                            OSDMap binBucketOSD = (OSDMap)OSDParser.DeserializeLLSDXml (binBucket);
                            if (binBucketOSD.ContainsKey ("item_id")) {
                                ItemID = binBucketOSD ["item_id"].AsUUID ();

                                InventoryItemBase item =
                                    m_scene.InventoryService.GetItem (GetRequestingAgentID (remoteClient), ItemID);
                                if (item != null) {
                                    GroupAssetType = item.AssetType;
                                    ItemName = item.Name;
                                } else
                                    ItemID = UUID.Zero;
                            }
                        }

                        m_groupData.AddGroupNotice (GetRequestingAgentID (remoteClient), GroupID, NoticeID,
                                                   im.FromAgentName, Subject, Message, ItemID, GroupAssetType, ItemName);
                        if (OnNewGroupNotice != null)
                            OnNewGroupNotice (GroupID, NoticeID);
                        GroupNoticeInfo notice = new GroupNoticeInfo () {
                            BinaryBucket = im.BinaryBucket,
                            GroupID = GroupID,
                            Message = Message,
                            noticeData = new GroupNoticeData {
                                AssetType = (byte)GroupAssetType,
                                FromName = im.FromAgentName,
                                GroupID = GroupID,
                                HasAttachment = ItemID != UUID.Zero,
                                ItemID = ItemID,
                                ItemName = ItemName,
                                NoticeID = NoticeID,
                                Subject = Subject,
                                Timestamp = im.Timestamp
                            }
                        };

                        if(remoteClient != null)
                            SendGroupNoticeToUsers (remoteClient, notice, false);
                    }
                }
                break;
            case (byte)InstantMessageDialog.GroupNoticeInventoryDeclined:
                break;
            case (byte)InstantMessageDialog.GroupNoticeInventoryAccepted: {
                    if (remoteClient != null) {
                        UUID FolderID = new UUID (im.BinaryBucket, 0);
                        remoteClient.Scene.InventoryService.GiveInventoryItemAsync (
                            remoteClient.AgentId,
                            UUID.Zero,
                            im.SessionID,
                            FolderID,
                            false,
                            (item) => { if (item != null) remoteClient.SendBulkUpdateInventory (item);}
                        );}
                }
                break;
            case 210: {
                    // This is sent from the region that the ejectee was ejected from
                    // if it's being delivered here, then the ejectee is here
                    // so we need to send local updates to the agent.

                    UUID ejecteeID = im.ToAgentID;

                    im.Dialog = (byte)InstantMessageDialog.MessageFromAgent;
                    OutgoingInstantMessage (im, ejecteeID);

                    IClientAPI ejectee = GetActiveClient (ejecteeID);
                    if (ejectee != null) {
                        UUID groupID = im.SessionID;
                        ejectee.SendAgentDropGroup (groupID);
                        if (ejectee.ActiveGroupId == groupID)
                            GroupTitleUpdate (ejectee, UUID.Zero, UUID.Zero);
                        RemoveFromGroupPowersCache (ejecteeID, groupID);
                    }
                }
                break;
            case 211: {
                    im.Dialog = (byte)InstantMessageDialog.GroupNotice;

                    //In offline group notices, imSessionID is replaced with the NoticeID so that we can rebuild the packet here
                    GroupNoticeInfo GND = m_groupData.GetGroupNotice (im.ToAgentID, im.SessionID);

                    if (GND != null) {
                        //Rebuild the binary bucket
                        if (GND.noticeData.HasAttachment) {
                            im.BinaryBucket = CreateBitBucketForGroupAttachment (GND.noticeData, GND.GroupID);
                            //Save the sessionID for the callback by the client (reject or accept)
                            //Only save if has attachment
                            im.SessionID = GND.noticeData.ItemID;
                        } else {
                            byte [] bucket = new byte [19];
                            bucket [0] = 0; //Attachment enabled == false so 0
                            bucket [1] = 0; //No attachment, so no asset type
                            GND.GroupID.ToBytes (bucket, 2);
                            bucket [18] = 0; //dunno
                            im.BinaryBucket = bucket;
                        }

                        OutgoingInstantMessage (im, im.ToAgentID);

                        //You MUST reset this, otherwise the client will get it twice,
                        // as it goes through OnGridInstantMessage
                        // which will check and then reresent the notice
                        im.Dialog = 211;
                    }
                }
                break;
            }
        }

        public void SendGroupNoticeToUsers (IClientAPI remoteClient, GroupNoticeInfo notice, bool localOnly)
        {
            // Send notice out to everyone that wants notices
            foreach (
                GroupMembersData member in
                    m_groupData.GetGroupMembers (GetRequestingAgentID (remoteClient), notice.GroupID)) {
                if (m_debugEnabled) {
                    UserAccount targetUser =
                        m_scene.UserAccountService.GetUserAccount (remoteClient.Scene.RegionInfo.AllScopeIDs,
                                                                  member.AgentID);
                    if (targetUser != null) {
                        MainConsole.Instance.DebugFormat (
                            "[Groups]: Prepping group notice {0} for agent: {1} who Accepts Notices ({2})",
                            notice.noticeData.NoticeID, targetUser.FirstName + " " + targetUser.LastName,
                            member.AcceptNotices);
                    } else {
                        MainConsole.Instance.DebugFormat (
                            "[Groups]: Prepping group notice {0} for agent: {1} who Accepts Notices ({2})",
                            notice.noticeData.NoticeID, member.AgentID, member.AcceptNotices);
                    }
                }

                if (member.AcceptNotices) {
                    // Build notice IIM
                    GridInstantMessage msg = CreateGroupNoticeIM (GetRequestingAgentID (remoteClient), notice,
                                                                 (byte)InstantMessageDialog.GroupNotice);

                    msg.ToAgentID = member.AgentID;
                    OutgoingInstantMessage (msg, member.AgentID, localOnly);
                }
            }
        }

        GroupMembershipData AttemptFindGroupMembershipData (UUID requestingAgentID, UUID agentID, UUID groupID)
        {
            if (m_cachedGroupMemberships.ContainsKey (agentID)) {
                foreach (
                    GroupMembershipData data in
                        from d in m_cachedGroupMemberships [agentID] where d.GroupID == groupID select d)
                    return data;
            }
            return m_groupData.GetGroupMembershipData (requestingAgentID, groupID, agentID);
        }

        void OnGridInstantMessage (GridInstantMessage msg)
        {
            if (m_debugEnabled)
                MainConsole.Instance.InfoFormat ("[Groups]: {0} called", MethodBase.GetCurrentMethod ().Name);

            // Trigger the above event handler
            OnInstantMessage(null, msg);         

            // If a message from a group arrives here, it may need to be forwarded to a local client
            if (msg.FromGroup) {
                switch (msg.Dialog) {
                case (byte)InstantMessageDialog.GroupInvitation:
                case (byte)InstantMessageDialog.GroupNotice:
                    UUID toAgentID = msg.ToAgentID;
                    IClientAPI localClient = GetActiveClient (toAgentID);
                    if (localClient != null) {
                        localClient.SendInstantMessage (msg);
                    }
                    break;
                }
            }
        }

        #endregion

        protected OSDMap OnRegisterCaps (UUID agentID, IHttpServer server)
        {
            OSDMap retVal = new OSDMap ();
            retVal ["GroupProposalBallot"] = CapsUtil.CreateCAPS ("GroupProposalBallot", "");

            server.AddStreamHandler (new GenericStreamHandler ("POST", retVal ["GroupProposalBallot"],
                                                             delegate (string path, Stream request,
                                                                      OSHttpRequest httpRequest,
                                                                      OSHttpResponse httpResponse) {
                                                                          return GroupProposalBallot (HttpServerHandlerHelpers.ReadString (request),
                                                                                                     agentID);
                                                                      }));
            retVal ["StartGroupProposal"] = CapsUtil.CreateCAPS ("StartGroupProposal", "");
            server.AddStreamHandler (new GenericStreamHandler ("POST", retVal ["StartGroupProposal"],
                                                             delegate (string path, Stream request,
                                                                      OSHttpRequest httpRequest,
                                                                      OSHttpResponse httpResponse) {
                                                                          return StartGroupProposal (HttpServerHandlerHelpers.ReadString (request),
                                                                                                    agentID);
                                                                      }));
            return retVal;
        }

        GridInstantMessage BuildGroupNoticeIM (GroupNoticeInfo data, UUID groupNoticeID, UUID AgentID)
        {
            GridInstantMessage msg = new GridInstantMessage {
                FromAgentID = data.GroupID,
                ToAgentID = AgentID,
                Timestamp = data.noticeData.Timestamp,
                FromAgentName = data.noticeData.FromName,
                Message = data.noticeData.Subject + "|" + data.Message,
                Dialog = (byte)InstantMessageDialog.GroupNoticeRequested,
                FromGroup = true,
                Offline = 1,
                ParentEstateID = 0,
                Position = Vector3.Zero,
                RegionID = UUID.Zero,
                SessionID = UUID.Random ()
            };

            //Allow offline

            if (data.noticeData.HasAttachment) {
                msg.BinaryBucket = CreateBitBucketForGroupAttachment (data.noticeData, data.GroupID);
                //Save the sessionID for the callback by the client (reject or accept)
                //Only save if has attachment
                msg.SessionID = data.noticeData.ItemID;
            } else {
                byte [] bucket = new byte [19];
                bucket [0] = 0; //Attachment enabled == false so 0
                bucket [1] = 0; //No attachment, so no asset type
                data.GroupID.ToBytes (bucket, 2);
                bucket [18] = 0; //dunno
                msg.BinaryBucket = bucket;
            }
            return msg;
        }

        byte [] CreateBitBucketForGroupAttachment (GroupNoticeData groupNoticeData, UUID groupID)
        {
            int i = 20;
            i += groupNoticeData.ItemName.Length;
            byte [] bitbucket = new byte [i];
            groupID.ToBytes (bitbucket, 2);
            byte [] name = Utils.StringToBytes (" " + groupNoticeData.ItemName);
            Array.ConstrainedCopy (name, 0, bitbucket, 18, name.Length);
            //Utils.Int16ToBytes((short)item.AssetType, bitbucket, 0);
            bitbucket [0] = 1; // 0 for no attachment, 1 for attachment
            bitbucket [1] = groupNoticeData.AssetType; // Asset type

            return bitbucket;
        }

        UUID GetRequestingAgentID (IClientAPI client)
        {
            UUID requestingAgentID = UUID.Zero;
            if (client != null) {
                requestingAgentID = client.AgentId;
            }
            return requestingAgentID;
        }

        #region Permissions

        /// <summary>
        ///     This caches the current group powers that the agent has
        ///     TKey 1 - UUID of the agent
        ///     TKey 2 - UUID of the group
        ///     TValue - Powers of the agent in the given group
        /// </summary>
        readonly Dictionary<UUID, Dictionary<UUID, ulong>> AgentGroupPowersCache =
            new Dictionary<UUID, Dictionary<UUID, ulong>> ();

        /// <summary>
        ///     WARNING: This is not the only place permissions are checked! They are checked in each of the connectors as well!
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="groupID"></param>
        /// <param name="permissions"></param>
        /// <returns></returns>
        public bool GroupPermissionCheck (UUID agentID, UUID groupID, GroupPowers permissions)
        {
            if (groupID == UUID.Zero)
                return false;

            if (agentID == UUID.Zero)
                return false;

            ulong ourPowers = 0;

            Dictionary<UUID, ulong> groupsCache;
            lock (AgentGroupPowersCache) {
                if (AgentGroupPowersCache.TryGetValue (agentID, out groupsCache)) {
                    if (groupsCache.ContainsKey (groupID)) {
                        ourPowers = groupsCache [groupID];
                        if (ourPowers == 1)
                            return false;
                        // 1 means not in the group or not found in the cache, so stop it here so that we don't check every time,
                        // and it can't be a permission, as its 0 then 2 in GroupPermissions
                    }
                }
            }
            //Ask the server as we don't know about this user
            if (ourPowers == 0) {
                GroupMembershipData GMD = AttemptFindGroupMembershipData (agentID, agentID, groupID);
                if (GMD == null) {
                    AddToGroupPowersCache (agentID, groupID, 1);
                    return false;
                }
                ourPowers = GMD.GroupPowers;
                //Add to the cache
                AddToGroupPowersCache (agentID, groupID, ourPowers);
            }

            //The user is the group, or it would have been weeded out earlier, so check whether we just need to know whether they are in the group
            if (permissions == GroupPowers.None)
                return true;

            if ((((GroupPowers)ourPowers) & permissions) != permissions)
                return false;

            return true;
        }

        void AddToGroupPowersCache (UUID agentID, UUID groupID, ulong powers)
        {
            lock (AgentGroupPowersCache) {
                Dictionary<UUID, ulong> agentGroups;
                if (!AgentGroupPowersCache.TryGetValue (agentID, out agentGroups))
                    agentGroups = new Dictionary<UUID, ulong> ();
                agentGroups [groupID] = powers;
                AgentGroupPowersCache [agentID] = agentGroups;
            }
        }

        void RemoveFromGroupPowersCache (UUID groupID)
        {
            lock (AgentGroupPowersCache) {
                foreach (Dictionary<UUID, ulong> grp in AgentGroupPowersCache.Values) {
                    grp.Remove (groupID);
                }
            }
        }

        void RemoveFromGroupPowersCache (UUID agentID, UUID groupID)
        {
            lock (AgentGroupPowersCache) {
                if (groupID == UUID.Zero) {
                    AgentGroupPowersCache.Remove (agentID);
                } else {
                    Dictionary<UUID, ulong> agentGroups;
                    if (AgentGroupPowersCache.TryGetValue (agentID, out agentGroups)) {
                        agentGroups.Remove (groupID);
                        AgentGroupPowersCache [agentID] = agentGroups;
                    }
                }
            }
        }

        #endregion
    }
}
