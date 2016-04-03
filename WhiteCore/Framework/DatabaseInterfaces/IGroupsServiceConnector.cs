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

using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.Services;

namespace WhiteCore.Framework.DatabaseInterfaces
{
    public interface IGroupsServiceConnector : IWhiteCoreDataPlugin
    {
        bool IsGroup (UUID groupID);

        void CreateGroup(UUID groupID, string name, string charter, bool showInList, UUID insigniaID, int membershipFee,
                         bool openEnrollment, bool allowPublish, bool maturePublish, UUID founderID, UUID ownerRoleID);

        void UpdateGroup(UUID requestingAgentID, UUID groupID, string charter, int showInList, UUID insigniaID,
                         int membershipFee, int openEnrollment, int allowPublish, int maturePublish);

        void UpdateGroupFounder(UUID groupID, UUID newOwner, bool keepOldOwnerInGroup);

        void AddGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject,
                            string message, UUID ItemID, int assetType, string itemName);

        bool EditGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID, string subject, string message);
        bool RemoveGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID);

        string SetAgentActiveGroup(UUID agentID, UUID groupID);
        UUID GetAgentActiveGroup(UUID requestingAgentID, UUID agentID);

        string SetAgentGroupSelectedRole(UUID agentID, UUID groupID, UUID roleID);

        void AddAgentToGroup(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID);
        bool RemoveAgentFromGroup(UUID requestingAgentID, UUID agentID, UUID groupID);

        void AddRoleToGroup(UUID requestingAgentID, UUID groupID, UUID roleID, string name, string description,
                            string roleTitle, ulong powers);

        void UpdateRole(UUID requestingAgentID, UUID groupID, UUID roleID, string name, string desc, string roleTitle,
                        ulong powers);

        void RemoveRoleFromGroup(UUID requestingAgentID, UUID roleID, UUID groupID);

        void AddAgentToRole(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID);
        void RemoveAgentFromRole(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID);

        void SetAgentGroupInfo(UUID requestingAgentID, UUID agentID, UUID groupID, int acceptNotices, int listInProfile);

        void AddAgentGroupInvite(UUID requestingAgentID, UUID inviteID, UUID groupID, UUID roleID, UUID agentID,
                                 string fromAgentName);

        void RemoveAgentInvite(UUID requestingAgentID, UUID inviteID);

        uint GetNumberOfGroupNotices(UUID requestingAgentID, UUID groupID);
        uint GetNumberOfGroupNotices(UUID requestingAgentID, List<UUID> groupIDList);

        uint GetNumberOfGroups(UUID requestingAgentID, Dictionary<string, bool> boolFields);

        List <UUID> GetAllGroups (UUID requestingAgentID);

        GroupRecord GetGroupRecord(UUID requestingAgentID, UUID groupID, string groupName);

        List<GroupRecord> GetGroupRecords(UUID requestingAgentID, uint start, uint count, Dictionary<string, bool> sort,
                                          Dictionary<string, bool> boolFields);

        List<GroupRecord> GetGroupRecords(UUID requestingAgentID, List<UUID> groupIDList);

        GroupProfileData GetMemberGroupProfile(UUID requestingAgentID, UUID groupID, UUID agentID);

        GroupMembershipData GetGroupMembershipData(UUID requestingAgentID, UUID groupID, UUID agentID);
        List<GroupMembershipData> GetAgentGroupMemberships(UUID requestingAgentID, UUID agentID);

        GroupInviteInfo GetAgentToGroupInvite(UUID requestingAgentID, UUID inviteID);
        List<GroupInviteInfo> GetGroupInvites(UUID requestingAgentID);

        GroupMembersData GetAgentGroupMemberData(UUID requestingAgentID, UUID groupID, UUID agentID);
        List<GroupMembersData> GetGroupMembers(UUID requestingAgentID, UUID groupID);

        // banned
        List<GroupBannedAgentsData> GetGroupBannedMembers(UUID requestingAgentID, UUID groupID);
        void AddGroupBannedAgent (UUID requestingAgentID, UUID groupID, List<UUID> bannedUserID);
        void RemoveGroupBannedAgent (UUID requestingAgentID, UUID groupID, List<UUID> bannedUserID);
        bool IsGroupBannedUser (UUID groupID, UUID agentID);
        GroupBannedAgentsData GetGroupBannedUser (UUID requestingAgentID, UUID groupID, UUID agentID);

        List<DirGroupsReplyData> FindGroups(UUID requestingAgentID, string search, uint? start, uint? count,
                                            uint queryflags);

        List<GroupRolesData> GetAgentGroupRoles(UUID requestingAgentID, UUID agentID, UUID groupID);
        List<GroupRolesData> GetGroupRoles(UUID requestingAgentID, UUID groupID);

        List<GroupRoleMembersData> GetGroupRoleMembers(UUID requestingAgentID, UUID groupID);

        GroupNoticeData GetGroupNoticeData(UUID requestingAgentID, UUID noticeID);
        GroupNoticeInfo GetGroupNotice(UUID requestingAgentID, UUID noticeID);

        List<GroupNoticeData> GetGroupNotices(UUID requestingAgentID, uint start, uint count, UUID groupID);
        List<GroupNoticeData> GetGroupNotices(UUID requestingAgentID, uint start, uint count, List<UUID> groupIDList);

        GroupProfileData GetGroupProfile(UUID requestingAgentID, UUID groupID);

        List<GroupTitlesData> GetGroupTitles(UUID requestingAgentID, UUID groupID);

        List<GroupProposalInfo> GetActiveProposals(UUID agentID, UUID groupID);
        List<GroupProposalInfo> GetInactiveProposals(UUID agentID, UUID groupID);
        void VoteOnActiveProposals(UUID agentID, UUID groupID, UUID proposalID, string vote);
        void AddGroupProposal(UUID agentID, GroupProposalInfo info);
    }

    /// <summary>
    ///     Internal class for chat sessions
    /// </summary>
    public class ChatSession
    {
        public List<ChatSessionMember> Members;
        public string Name;
        public UUID SessionID;
    }

    //Pulled from OpenMetaverse
    // Summary:
    //     Structure representing a member of a group chat session and their settings
    public class ChatSessionMember
    {
        // Summary:
        //     The OpenMetaverse.UUID of the Avatar
        public UUID AvatarKey;
        //
        // Summary:
        //     True if user has voice chat enabled
        public bool CanVoiceChat;

        /// <summary>
        ///     Whether the user has accepted being added to the group chat
        /// </summary>
        public bool HasBeenAdded;

        /// <summary>
        ///     Whether the user has asked to be removed from the chat
        /// </summary>
        public bool RequestedRemoval;

        //
        // Summary:
        //     True of Avatar has moderator abilities
        public bool IsModerator;
        //
        // Summary:
        //     True if a moderator has muted this avatars chat
        public bool MuteText;
        //
        // Summary:
        //     True if a moderator has muted this avatars voice
        public bool MuteVoice;
        //
        // Summary:
        //     True if they have been requested to join the session
    }

    public class GroupInviteInfo : IDataTransferable
    {
        public UUID AgentID = UUID.Zero;
        public string FromAgentName = "";
        public UUID GroupID = UUID.Zero;
        public UUID InviteID = UUID.Zero;
        public UUID RoleID = UUID.Zero;

        public GroupInviteInfo()
        {
        }

        public override OSDMap ToOSD()
        {
            OSDMap values = new OSDMap();
            values["GroupID"] = GroupID;
            values["RoleID"] = RoleID;
            values["AgentID"] = AgentID;
            values["InviteID"] = InviteID;
            values["FromAgentName"] = FromAgentName;
            return values;
        }

        public override void FromOSD(OSDMap values)
        {
            GroupID = values["GroupID"];
            RoleID = values["RoleID"];
            AgentID = values["AgentID"];
            InviteID = values["InviteID"];
            FromAgentName = values["FromAgentName"];
        }
    }

    public class GroupNoticeInfo : IDataTransferable
    {
        public byte[] BinaryBucket = new byte[0];
        public UUID GroupID = UUID.Zero;
        public string Message = string.Empty;
        public GroupNoticeData noticeData = new GroupNoticeData();

        public GroupNoticeInfo()
        {
        }

        public override OSDMap ToOSD()
        {
            OSDMap values = new OSDMap();
            values["noticeData"] = noticeData.ToOSD();
            values["GroupID"] = GroupID;
            values["Message"] = Message;
            values["BinaryBucket"] = BinaryBucket;
            return values;
        }

        public override void FromOSD(OSDMap values)
        {
            noticeData = new GroupNoticeData();
            noticeData.FromOSD((OSDMap)values["noticeData"]);
            GroupID = values["GroupID"];
            Message = values["Message"];
            BinaryBucket = values["BinaryBucket"];
        }
    }

    public class GroupProposalInfo : IDataTransferable
    {
        public int Duration;
        public UUID GroupID = UUID.Zero;
        public float Majority;
        public int Quorum;
        public UUID Session = UUID.Zero;
        public string Text = string.Empty;
        public UUID BallotInitiator = UUID.Zero;
        public DateTime Created = DateTime.Now;
        public DateTime Ending = DateTime.Now;
        public UUID VoteID = UUID.Random();

        /// <summary>
        ///     Only set when a user is calling to find out proposal info, it is what said user voted
        /// </summary>
        public string VoteCast = "";

        /// <summary>
        ///     The result of the proposal (success or failure)
        /// </summary>
        public bool Result = false;

        /// <summary>
        ///     The number of votes cast (so far if the proposal is still open)
        /// </summary>
        public int NumVotes = 0;

        /// <summary>
        ///     If this is false, the result of the proposal has not been calculated and should be when it is retrieved next
        /// </summary>
        public bool HasCalculatedResult = false;

        public override void FromOSD(OSDMap map)
        {
            GroupID = map["GroupID"].AsUUID();
            Duration = map["Duration"].AsInteger();
            Majority = (float)map["Majority"].AsReal();
            Text = map["Text"].AsString();
            Quorum = map["Quorum"].AsInteger();
            Session = map["Session"].AsUUID();
            BallotInitiator = map["BallotInitiator"];
            Created = map["Created"];
            Ending = map["Ending"];
            VoteID = map["VoteID"];
            VoteCast = map["VoteCast"];
            Result = map["Result"];
            NumVotes = map["NumVotes"];
            HasCalculatedResult = map["HasCalculatedResult"];
        }

        public override OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();
            map["GroupID"] = GroupID;
            map["Duration"] = Duration;
            map["Majority"] = Majority;
            map["Text"] = Text;
            map["Quorum"] = Quorum;
            map["Session"] = Session;
            map["BallotInitiator"] = BallotInitiator;
            map["Created"] = Created;
            map["Ending"] = Ending;
            map["VoteID"] = VoteID;
            map["VoteCast"] = VoteCast;
            map["Result"] = Result;
            map["NumVotes"] = NumVotes;
            map["HasCalculatedResult"] = HasCalculatedResult;
            return map;
        }
    }
}
