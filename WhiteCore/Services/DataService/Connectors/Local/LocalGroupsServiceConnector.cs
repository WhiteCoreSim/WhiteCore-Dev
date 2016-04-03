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
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.DataService
{
    public class LocalGroupsServiceConnector : ConnectorBase, IGroupsServiceConnector
    {
        #region Declares
        const string _DATAREALM = "group_data";
        const string _NOTICEREALM = "group_notice";
        const string _MEMBERSHIPREALM = "group_membership";
        const string _MEMBERSHIPROLEREALM = "group_role_membership";
        const string _AGENTREALM = "group_agent";
        const string _ROLEREALM = "group_roles";
        const string _INVITEREALM = "group_invite";
        const string _BANREALM = "group_bans";

        IGenericData GD;
        List<UUID> agentsCanBypassGroupNoticePermsCheck = new List<UUID>();

        // system groups
        string realEstateGroupName = Constants.RealEstateGroupName;

        #endregion

        #region IWhiteCoreDataPlugin members

        public void Initialize(IGenericData genericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            GD = genericData;

            if (source.Configs[Name] != null)
                defaultConnectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);

            if (source.Configs["Groups"] != null)
            {
                agentsCanBypassGroupNoticePermsCheck =
                    Util.ConvertToList(source.Configs["Groups"].GetString("AgentsCanBypassGroupNoticePermsCheck", ""), true)
                        .ConvertAll(x => new UUID(x));
            }

            if (GD != null)
                GD.ConnectToDatabase (defaultConnectionString, "Groups",
                    source.Configs ["WhiteCoreConnectors"].GetBoolean ("ValidateTables", true));

            Framework.Utilities.DataManager.RegisterPlugin (Name + "Local", this);

            if (source.Configs ["WhiteCoreConnectors"].GetString ("GroupsConnector", "LocalConnector") == "LocalConnector")
                Framework.Utilities.DataManager.RegisterPlugin (this);

            IConfig grpConfig = source.Configs["SystemUserService"];
            if (grpConfig != null)
                realEstateGroupName = grpConfig.GetString("RealEstateGroupName", realEstateGroupName);

            Init (simBase, Name);

            // check for system groups if we are local
            if (IsLocalConnector)
            {
                VerifySystemGroup ( 
                    realEstateGroupName, 
                    (UUID) Constants.RealEstateGroupUUID,
                    (UUID) Constants.RealEstateOwnerUUID,
                    "RealEstate maintenance group"
                );
            }
        }

        public string Name
        {
            get { return "IGroupsServiceConnector"; }
        }

        /// <summary>
        /// Verify existence of and create a system group if required.
        /// </summary>
        /// <returns><c>true</c>, if system group was created, <c>false</c> otherwise.</returns>
        /// <param name="grpName">Group name.</param>
        /// <param name="grpUUID">Group UUID.</param>
        /// <param name="grpOwnerUUID">Group owner UUID.</param>
        /// <param name="grpCharter">Group charter.</param>
        void VerifySystemGroup(string grpName, UUID grpUUID, UUID grpOwnerUUID, string grpCharter)
        {
            GroupRecord grpRec;
            grpRec = GetGroupRecord (UUID.Zero, grpUUID, grpName);
            if (grpRec == null)
            {
                MainConsole.Instance.WarnFormat ("Creating System Group '{0}'", grpName);

                CreateGroup (
                    grpUUID,                                            // Group UUID
                    grpName,                                            // Name
                    grpCharter,                                         // Charter / description
                    false,                                              // Show in list
                    UUID.Zero, 0, false, false, false,                  // Insignia UUID, Membership fee, Open Enrollment, Allow publishing, Mature
                    grpOwnerUUID,                                       // founder UUID
                    UUID.Random ());                                    // owner role UUID
            } else
            {
                if (grpRec.FounderID != grpOwnerUUID)
                    UpdateGroupFounder (grpUUID, grpOwnerUUID, false);
            }
        }

        #endregion

        #region IGroupsServiceConnector Members

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool IsGroup(UUID groupID)
        {
            object remoteValue = DoRemote(groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (bool)remoteValue;

            QueryFilter filter = new QueryFilter();

            if (groupID != UUID.Zero)
            {
                filter.andFilters ["GroupID"] = groupID;
                List<string> groupsData = GD.Query (new[] { "GroupID" }, _DATAREALM, filter, null, null, null);

                return (groupsData.Count != 0);
            }

            return false;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void CreateGroup(UUID groupID, string name, string charter, bool showInList, UUID insigniaID,
                                int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish,
                                UUID founderID, UUID ownerRoleID)
        {
            object remoteValue = DoRemote(groupID, name, charter, showInList, insigniaID, membershipFee, openEnrollment,
                                          allowPublish, maturePublish, founderID, ownerRoleID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            const ulong EveryonePowers = (ulong)(
                GroupPowers.Accountable |
                GroupPowers.AllowSetHome |
                GroupPowers.AllowVoiceChat |
                GroupPowers.JoinChat |
                GroupPowers.ReceiveNotices |
                GroupPowers.StartProposal |
                GroupPowers.VoteOnProposal
            );
           
           const ulong OfficersPowers = (ulong) (
                (GroupPowers) EveryonePowers |
                GroupPowers.AllowFly |
                GroupPowers.AllowLandmark |
                GroupPowers.AllowRez |  
                GroupPowers.AssignMemberLimited |
                GroupPowers.ChangeIdentity | 
                GroupPowers.ChangeMedia |
                GroupPowers.ChangeOptions |
                GroupPowers.DeedObject |
                GroupPowers.Eject |
                GroupPowers.FindPlaces |
                GroupPowers.Invite |
                GroupPowers.LandChangeIdentity |
                GroupPowers.LandDeed |
                GroupPowers.LandDivideJoin |
                GroupPowers.LandEdit |
                GroupPowers.LandEjectAndFreeze |
                GroupPowers.LandGardening |
                GroupPowers.LandManageAllowed |
                GroupPowers.LandManageBanned |
                GroupPowers.LandManagePasses |
                GroupPowers.LandOptions |
                GroupPowers.LandRelease |
                GroupPowers.LandSetSale |
                GroupPowers.MemberVisible |
                GroupPowers.ModerateChat |
                GroupPowers.ObjectManipulate |
                GroupPowers.ObjectSetForSale |
                GroupPowers.ReturnGroupOwned |
                GroupPowers.ReturnGroupSet |
                GroupPowers.ReturnNonGroup |
                GroupPowers.RoleProperties |
                GroupPowers.SendNotices |
                GroupPowers.SetLandingPoint
             );

            const ulong OwnersPowers = (ulong) (
                (GroupPowers) OfficersPowers |
                GroupPowers.AllowEditLand |
                GroupPowers.AssignMember |
                GroupPowers.ChangeActions|
                GroupPowers.CreateRole |
                GroupPowers.DeleteRole |
                GroupPowers.ExperienceAdmin |
                GroupPowers.ExperienceCreator |
                GroupPowers.GroupBanAccess |
                GroupPowers.HostEvent |
                GroupPowers.RemoveMember
            );

                
            Dictionary<string, object> row = new Dictionary<string, object>(11);
            row["GroupID"] = groupID;
            row["Name"] = name;
            row["Charter"] = charter ?? "";
            row["InsigniaID"] = insigniaID;
            row["FounderID"] = founderID;
            row["MembershipFee"] = membershipFee;
            row["OpenEnrollment"] = openEnrollment ? 1 : 0;
            row["ShowInList"] = showInList ? 1 : 0;
            row["AllowPublish"] = allowPublish ? 1 : 0;
            row["MaturePublish"] = maturePublish ? 1 : 0;
            row["OwnerRoleID"] = ownerRoleID;

            GD.Insert(_DATAREALM, row);

            // const ulong EveryonePowers = 8796495740928;             // >> 0x80018010000
            //
            // 03-07-2015 Fly-Man- Removed this part in favor of using the real values
            //
            
            //Add everyone role to group
            AddRoleToGroup(founderID, groupID, UUID.Zero, "Everyone", "Everyone in the group is in the everyone role.",
                           "Member of " + name, EveryonePowers);

            // const ulong OfficersPowers = 436506116225230;           // >> 0x 18cfffffff8ce
            //
            // 03-07-2015 Fly-Man- Removed this part in favor of using the real values
            //

            UUID officersRole = UUID.Random();
            //Add officers role to group
            AddRoleToGroup(founderID, groupID, officersRole, "Officers",
                           "The officers of the group, with more powers than regular members.", "Officer of " + name,
                           OfficersPowers);

            // replaced with above //const ulong OwnerPowers = 18446744073709551615;
            // this is the uint maxvalue
            // the default above is : 349644697632766 >>  0x13dfffffffffe
           
            //Add owner role to group
            AddRoleToGroup(founderID, groupID, ownerRoleID, "Owners", "Owners of " + name, "Owner of " + name,
                           OwnersPowers);

            //Add owner to the group as owner
            AddAgentToGroup(founderID, founderID, groupID, ownerRoleID);
            AddAgentToRole(founderID, founderID, groupID, officersRole);

            SetAgentGroupSelectedRole(founderID, groupID, ownerRoleID);

            SetAgentActiveGroup(founderID, groupID);
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public void UpdateGroupFounder(UUID groupID, UUID newOwner, bool keepOldOwnerInGroup)
        {
            /*object remoteValue = DoRemote(groupID, newOwner, keepOldOwnerInGroup);
            if (remoteValue != null || m_doRemoteOnly)
                return;*/

            GroupRecord record = GetGroupRecord(UUID.Zero, groupID, "");
            bool newUserExists = GetAgentGroupMemberData(newOwner, groupID, newOwner) != null;

            Dictionary<string, object> values = new Dictionary<string, object>(1);
            values["FounderID"] = newOwner;
            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;

            GD.Update(_DATAREALM, values, null, filter, null, null);

            if (!newUserExists)
                AddAgentToGroup(newOwner, newOwner, groupID, record.OwnerRoleID);
            else
                AddAgentToRole(newOwner, newOwner, groupID, record.OwnerRoleID);

            if (!keepOldOwnerInGroup)
                RemoveAgentFromGroup(newOwner, record.FounderID, groupID);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void UpdateGroup(UUID requestingAgentID, UUID groupID, string charter, int showInList, UUID insigniaID,
                                int membershipFee, int openEnrollment, int allowPublish, int maturePublish)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, charter, showInList, insigniaID, membershipFee,
                                          openEnrollment, allowPublish, maturePublish);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID,
                                      (ulong) (GroupPowers.ChangeOptions | GroupPowers.ChangeIdentity)))
            {
                Dictionary<string, object> values = new Dictionary<string, object>(6);
                values["Charter"] = charter;
                values["InsigniaID"] = insigniaID;
                values["MembershipFee"] = membershipFee;
                values["OpenEnrollment"] = openEnrollment;
                values["ShowInList"] = showInList;
                values["AllowPublish"] = allowPublish;
                values["MaturePublish"] = maturePublish;

                QueryFilter filter = new QueryFilter();
                filter.andFilters["GroupID"] = groupID;

                GD.Update(_DATAREALM, values, null, filter, null, null);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void AddGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject,
                                   string message, UUID itemID, int assetType, string itemName)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, noticeID, fromName, subject, message, itemID,
                                          assetType, itemName);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.SendNotices))
            {
                Dictionary<string, object> row = new Dictionary<string, object>(10);
                row["GroupID"] = groupID;
                row["NoticeID"] = noticeID == UUID.Zero ? UUID.Random() : noticeID;
                row["Timestamp"] = ((uint) Util.UnixTimeSinceEpoch());
                row["FromName"] = fromName;
                row["Subject"] = subject;
                row["Message"] = message;
                row["HasAttachment"] = (itemID != UUID.Zero) ? 1 : 0;
                row["ItemID"] = itemID;
                row["AssetType"] = assetType;
                row["ItemName"] = itemName == null ? "" : itemName;

                GD.Insert(_NOTICEREALM, row);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public bool EditGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID, string subject, string message)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, noticeID, subject, message);
            if (remoteValue != null || m_doRemoteOnly)
            {
                return (bool) remoteValue;
            }

            if (!agentsCanBypassGroupNoticePermsCheck.Contains(requestingAgentID) &&
                !CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.SendNotices))
            {
                MainConsole.Instance.TraceFormat("Permission check failed when trying to edit group notice {0}.",
                                                 noticeID);
                return false;
            }

            GroupNoticeInfo GNI = GetGroupNotice(requestingAgentID, noticeID);
            if (GNI == null)
            {
                MainConsole.Instance.TraceFormat("Could not find group notice {0}", noticeID);
                return false;
            }
            else if (GNI.GroupID != groupID)
            {
                MainConsole.Instance.TraceFormat("Group notice {0} group ID {1} does not match supplied group ID {2}",
                                                 noticeID, GNI.GroupID, groupID);
                return false;
            }
            else if (subject.Trim() == string.Empty || message.Trim() == string.Empty)
            {
                MainConsole.Instance.TraceFormat("Could not edit group notice {0}, message or subject was empty",
                                                 noticeID);
                return false;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            filter.andFilters["NoticeID"] = noticeID;

            Dictionary<string, object> update = new Dictionary<string, object>(2);
            update["Subject"] = subject.Trim();
            update["Message"] = message.Trim();

            return GD.Update(_NOTICEREALM, update, null, filter, null, null);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public bool RemoveGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, noticeID);
            if (remoteValue != null || m_doRemoteOnly)
            {
                return (bool) remoteValue;
            }

            if (!agentsCanBypassGroupNoticePermsCheck.Contains(requestingAgentID) &&
                !CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.SendNotices))
            {
                MainConsole.Instance.TraceFormat("Permission check failed when trying to edit group notice {0}.",
                                                 noticeID);
                return false;
            }

            GroupNoticeInfo GNI = GetGroupNotice(requestingAgentID, noticeID);
            if (GNI == null)
            {
                MainConsole.Instance.TraceFormat("Could not find group notice {0}", noticeID);
                return false;
            }
            else if (GNI.GroupID != groupID)
            {
                MainConsole.Instance.TraceFormat("Group notice {0} group ID {1} does not match supplied group ID {2}",
                                                 noticeID, GNI.GroupID, groupID);
                return false;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            filter.andFilters["NoticeID"] = noticeID;

            return GD.Delete(_NOTICEREALM, filter);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public string SetAgentActiveGroup(UUID agentID, UUID groupID)
        {
            object remoteValue = DoRemote(agentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (string) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["AgentID"] = agentID;
            if (GD.Query(new[] {"*"}, _AGENTREALM, filter, null, null, null).Count != 0)
            {
                Dictionary<string, object> values = new Dictionary<string, object>(1);
                values["ActiveGroupID"] = groupID;

                GD.Update(_AGENTREALM, values, null, filter, null, null);
            }
            else
            {
                Dictionary<string, object> row = new Dictionary<string, object>(2);
                row["AgentID"] = agentID;
                row["ActiveGroupID"] = groupID;
                GD.Insert(_AGENTREALM, row);
            }
            GroupMembersData gdata = GetAgentGroupMemberData(agentID, groupID, agentID);
            return gdata == null ? "" : gdata.Title;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public UUID GetAgentActiveGroup(UUID requestingAgentID, UUID agentID)
        {
            object remoteValue = DoRemote(requestingAgentID, agentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (UUID) remoteValue; // note: this is bad, you can't cast a null object to a UUID

            QueryFilter filter = new QueryFilter();
            filter.andFilters["AgentID"] = agentID;
            List<string> groups = GD.Query(new string[1] {"ActiveGroupID"}, _AGENTREALM, filter, null, null, null);

            return (groups.Count != 0) ? UUID.Parse(groups[0]) : UUID.Zero;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public string SetAgentGroupSelectedRole(UUID agentID, UUID groupID, UUID roleID)
        {
            object remoteValue = DoRemote(agentID, groupID, roleID);
            if (remoteValue != null || m_doRemoteOnly)
                return (string) remoteValue;

            Dictionary<string, object> values = new Dictionary<string, object>(1);
            values["SelectedRoleID"] = roleID;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["AgentID"] = agentID;
            filter.andFilters["GroupID"] = groupID;

            GD.Update(_MEMBERSHIPREALM, values, null, filter, null, null);

            GroupMembersData gdata = GetAgentGroupMemberData(agentID, groupID, agentID);
            return gdata == null ? "" : gdata.Title;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void AddAgentToGroup(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID)
        {
            object remoteValue = DoRemote(requestingAgentID, agentID, groupID, roleID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            Dictionary<string, object> where = new Dictionary<string, object>(2);
            where["AgentID"] = agentID;
            where["GroupID"] = groupID;

            if (GD.Query(new[] {"*"}, _MEMBERSHIPREALM, 
                new QueryFilter {andFilters = where}, null, null, null).Count != 0)
            {
                MainConsole.Instance.Error("[AGM]: Agent " + agentID + " is already in " + groupID);
                return;
            }
            Dictionary<string, object> row = new Dictionary<string, object>(6);
            row["GroupID"] = groupID;
            row["AgentID"] = agentID;
            row["SelectedRoleID"] = roleID;
            row["Contribution"] = 0;
            row["ListInProfile"] = 1;
            row["AcceptNotices"] = 1;
            GD.Insert(_MEMBERSHIPREALM, row);

            // Make sure they're in the Everyone role
            AddAgentToRole(requestingAgentID, agentID, groupID, UUID.Zero);
            // Make sure they're in specified role, if they were invited
            if (roleID != UUID.Zero)
                AddAgentToRole(requestingAgentID, agentID, groupID, roleID);
            //Set the role they were invited to as their selected role
            SetAgentGroupSelectedRole(agentID, groupID, roleID);
            SetAgentActiveGroup(agentID, groupID);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool RemoveAgentFromGroup(UUID requestingAgentID, UUID agentID, UUID groupID)
        {
            //Allow kicking yourself
            object remoteValue = DoRemote(requestingAgentID, agentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue != null && (bool) remoteValue;

            if ((CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.RemoveMember)) ||
                (requestingAgentID == agentID))
            {
                QueryFilter filter = new QueryFilter();
                filter.andFilters["AgentID"] = agentID;
                filter.andFilters["ActiveGroupID"] = groupID;

                Dictionary<string, object> values = new Dictionary<string, object>(1);
                values["ActiveGroupID"] = UUID.Zero;

                // 1. If group is agent's active group, change active group to uuidZero
                GD.Update(_AGENTREALM, values, null, filter, null, null);

                filter.andFilters.Remove("ActiveGroupID");
                filter.andFilters["GroupID"] = groupID;

                // 2. Remove Agent from all of the group roles
                GD.Delete(_MEMBERSHIPROLEREALM, filter);

                // 3. Remove Agent from the groups
                GD.Delete(_MEMBERSHIPREALM, filter);

                return true;
            }
            return false;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void AddRoleToGroup(UUID requestingAgentID, UUID groupID, UUID roleID, string nameOf, string description,
                                   string title, ulong powers)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, roleID, nameOf, description, title, powers);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.CreateRole))
            {
                Dictionary<string, object> row = new Dictionary<string, object>(6);
                row["GroupID"] = groupID;
                row["RoleID"] = roleID;
                row["Name"] = nameOf;
                row["Description"] = description != null ? description : "";
                row["Title"] = title;
                row["Powers"] = powers.ToString();
                GD.Insert(_ROLEREALM, row);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void UpdateRole(UUID requestingAgentID, UUID groupID, UUID roleID, string nameOf, string desc,
                               string roleTitle, ulong powers)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, roleID, nameOf, desc, roleTitle, powers);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.RoleProperties))
            {
                Dictionary<string, object> values = new Dictionary<string, object>();
                values["RoleID"] = roleID;
                if (nameOf != null)
                {
                    values["Name"] = nameOf;
                }
                if (desc != null)
                    values["Description"] = desc;

                if (roleTitle != null)
                {
                    values["Title"] = roleTitle;
                }
                values["Powers"] = powers.ToString();

                QueryFilter filter = new QueryFilter();
                filter.andFilters["GroupID"] = groupID;
                filter.andFilters["RoleID"] = roleID;

                GD.Update(_ROLEREALM, values, null, filter, null, null);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void RemoveRoleFromGroup(UUID requestingAgentID, UUID roleID, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, roleID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.DeleteRole))
            {
                Dictionary<string, object> values = new Dictionary<string, object>(1);
                values["SelectedRoleID"] = UUID.Zero;

                QueryFilter ufilter = new QueryFilter();
                ufilter.andFilters["GroupID"] = groupID;
                ufilter.andFilters["SelectedRoleID"] = roleID;

                QueryFilter dfilter = new QueryFilter();
                dfilter.andFilters["GroupID"] = groupID;
                dfilter.andFilters["RoleID"] = roleID;

                GD.Delete(_MEMBERSHIPROLEREALM, dfilter);
                GD.Update(_MEMBERSHIPREALM, values, null, ufilter, null, null);
                GD.Delete(_ROLEREALM, dfilter);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void AddAgentToRole(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID)
        {
            object remoteValue = DoRemote(requestingAgentID, agentID, groupID, roleID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (!CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.AssignMember))
            {
                //This isn't an open and shut case, they could be setting the agent to their role, which would allow for AssignMemberLimited
                if (!CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.AssignMemberLimited))
                {
                    GroupProfileData profile = GetGroupProfile(requestingAgentID, groupID);
                    if (profile == null || !profile.OpenEnrollment || roleID != UUID.Zero) //For open enrollment adding
                    {
                        MainConsole.Instance.Warn("[AGM]: User " + requestingAgentID + " attempted to add user " +
                                                  agentID +
                                                  " to group " + groupID + ", but did not have permissions to do so!");
                        return;
                    }
                }
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            filter.andFilters["RoleID"] = roleID;
            filter.andFilters["AgentID"] = agentID;
            //Make sure they aren't already in this role
            if (
                uint.Parse(GD.Query(new[] {"COUNT(AgentID)"}, _MEMBERSHIPROLEREALM, filter, null, null, null)[0]) ==
                0)
            {
                Dictionary<string, object> row = new Dictionary<string, object>(3);
                row["GroupID"] = groupID;
                row["RoleID"] = roleID;
                row["AgentID"] = agentID;
                GD.Insert(_MEMBERSHIPROLEREALM, row);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void RemoveAgentFromRole(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID)
        {
            object remoteValue = DoRemote(requestingAgentID, agentID, groupID, roleID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.AssignMember))
            {
                Dictionary<string, object> values = new Dictionary<string, object>(1);
                values["SelectedRoleID"] = UUID.Zero;

                QueryFilter filter = new QueryFilter();
                filter.andFilters["AgentID"] = agentID;
                filter.andFilters["GroupID"] = groupID;
                filter.andFilters["SelectedRoleID"] = roleID;

                GD.Update(_MEMBERSHIPREALM, values, null, filter, null, null);

                filter.andFilters.Remove("SelectedRoleID");
                filter.andFilters["RoleID"] = roleID;
                GD.Delete(_MEMBERSHIPROLEREALM, filter);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void SetAgentGroupInfo(UUID requestingAgentID, UUID agentID, UUID groupID, int acceptNotices,
                                      int listInProfile)
        {
            object remoteValue = DoRemote(requestingAgentID, agentID, groupID, acceptNotices, listInProfile);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (!CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.ChangeIdentity))
            {
                return;
            }

            Dictionary<string, object> values = new Dictionary<string, object>(3);
            values["AgentID"] = agentID;
            values["AcceptNotices"] = acceptNotices;
            values["ListInProfile"] = listInProfile;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;     // these were reversed - 20160314 -greythane-
            filter.andFilters["AgentID"] = agentID;

            GD.Update(_MEMBERSHIPREALM, values, null, filter, null, null);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void AddAgentGroupInvite(UUID requestingAgentID, UUID inviteID, UUID groupID, UUID roleID, UUID agentID,
                                        string fromAgentName)
        {
            object remoteValue = DoRemote(requestingAgentID, inviteID, groupID, roleID, agentID, fromAgentName);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.Invite))
            {
                QueryFilter filter = new QueryFilter();
                filter.andFilters["AgentID"] = agentID;
                filter.andFilters["GroupID"] = groupID;
                GD.Delete(_INVITEREALM, filter);

                Dictionary<string, object> row = new Dictionary<string, object>(6);
                row["InviteID"] = inviteID;
                row["GroupID"] = groupID;
                row["RoleID"] = roleID;
                row["AgentID"] = agentID;
                row["TMStamp"] = Util.UnixTimeSinceEpoch();
                row["FromAgentName"] = fromAgentName;
                GD.Insert(_INVITEREALM, row);
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void RemoveAgentInvite(UUID requestingAgentID, UUID inviteID)
        {
            object remoteValue = DoRemote(requestingAgentID, inviteID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["InviteID"] = inviteID;
            GD.Delete(_INVITEREALM, filter);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void AddGroupProposal(UUID agentID, GroupProposalInfo info)
        {
            object remoteValue = DoRemote(agentID, info);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(agentID, info.GroupID, (ulong) GroupPowers.StartProposal))
                GenericUtils.AddGeneric(info.GroupID, "Proposal", info.VoteID.ToString(), info.ToOSD(), GD);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupProposalInfo> GetActiveProposals(UUID agentID, UUID groupID)
        {
            object remoteValue = DoRemote(agentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupProposalInfo>) remoteValue;

            if (!CheckGroupPermissions(agentID, groupID, (ulong) GroupPowers.VoteOnProposal))
                return new List<GroupProposalInfo>();

            List<GroupProposalInfo> proposals = GenericUtils.GetGenerics<GroupProposalInfo>(groupID, "Proposal", GD);
            proposals = (from p in proposals where p.Ending > DateTime.Now select p).ToList();
            foreach (GroupProposalInfo p in proposals)
                p.VoteCast = GetHasVoted(agentID, p);

            return proposals; //Return only ones that are still running
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupProposalInfo> GetInactiveProposals(UUID agentID, UUID groupID)
        {
            object remoteValue = DoRemote(agentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupProposalInfo>) remoteValue;

            if (!CheckGroupPermissions(agentID, groupID, (ulong) GroupPowers.VoteOnProposal))
                return new List<GroupProposalInfo>();

            List<GroupProposalInfo> proposals = GenericUtils.GetGenerics<GroupProposalInfo>(groupID, "Proposal", GD);
            proposals = (from p in proposals where p.Ending < DateTime.Now select p).ToList();
            List<GroupProposalInfo> proposalsNeedingResults =
                (from p in proposals where !p.HasCalculatedResult select p).ToList();
            foreach (GroupProposalInfo p in proposalsNeedingResults)
            {
                List<OpenMetaverse.StructuredData.OSDMap> maps = GenericUtils.GetGenerics(p.GroupID, p.VoteID.ToString(),
                                                                                          GD);
                int yes = 0;
                int no = 0;
                foreach (OpenMetaverse.StructuredData.OSDMap vote in maps)
                {
                    if (vote["Vote"].AsString().ToLower() == "yes")
                        yes++;
                    else if (vote["Vote"].AsString().ToLower() == "no")
                        no++;
                }
                if (yes + no < p.Quorum)
                    p.Result = false;
                /*if (yes > no)
                    p.Result = true;
                else
                    p.Result = false;*/
                p.HasCalculatedResult = true;
                GenericUtils.AddGeneric(p.GroupID, "Proposal", p.VoteID.ToString(), p.ToOSD(), GD);
            }
            foreach (GroupProposalInfo p in proposals)
                p.VoteCast = GetHasVoted(agentID, p);

            return proposals; //Return only ones that are still running
        }

        string GetHasVoted(UUID agentID, GroupProposalInfo p)
        {
            OpenMetaverse.StructuredData.OSDMap map = GenericUtils.GetGeneric(p.GroupID, p.VoteID.ToString(),
                                                                              agentID.ToString(), GD);
            if (map != null)
                return map["Vote"];
            return "";
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void VoteOnActiveProposals(UUID agentID, UUID groupID, UUID proposalID, string vote)
        {
            object remoteValue = DoRemote(agentID, groupID, proposalID, vote);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (!CheckGroupPermissions(agentID, groupID, (ulong) GroupPowers.VoteOnProposal))
                return;

            OpenMetaverse.StructuredData.OSDMap map = new OpenMetaverse.StructuredData.OSDMap();
            map["Vote"] = vote;
            GenericUtils.AddGeneric(groupID, proposalID.ToString(), agentID.ToString(), map, GD);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public uint GetNumberOfGroupNotices(UUID requestingAgentID, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (uint) remoteValue; // note: this is bad, you can't cast a null object to a uint

            List<UUID> GroupIDs = new List<UUID> {groupID};
            return GetNumberOfGroupNotices(requestingAgentID, GroupIDs);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public uint GetNumberOfGroupNotices(UUID requestingAgentID, List<UUID> groupIDList)
        {
            object remoteValue = DoRemote(requestingAgentID, groupIDList);
            if (remoteValue != null || m_doRemoteOnly)
                return (uint) remoteValue; // note: this is bad, you can't cast a null object to a uint

            bool had = groupIDList.Count > 0;

            List<UUID> groupIDs = new List<UUID>();
            if (!agentsCanBypassGroupNoticePermsCheck.Contains(requestingAgentID))
            {
                groupIDs.AddRange(
                    groupIDs.Where(
                        GroupID => CheckGroupPermissions(requestingAgentID, GroupID, (ulong) GroupPowers.ReceiveNotices)));
            }
            else
            {
                groupIDs = groupIDList;
            }

            if (had && groupIDs.Count == 0)
            {
                return 0;
            }

            QueryFilter filter = new QueryFilter();
            List<object> filterGroupIDs = new List<object>(groupIDs.Count);
            filterGroupIDs.AddRange(groupIDs.Cast<object>());
            if (filterGroupIDs.Count > 0)
            {
                filter.orMultiFilters["GroupID"] = filterGroupIDs;
            }

            return uint.Parse(GD.Query(new[] {"COUNT(NoticeID)"}, _NOTICEREALM, filter, null, null, null)[0]);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public uint GetNumberOfGroups(UUID requestingAgentID, Dictionary<string, bool> boolFields)
        {
            object remoteValue = DoRemote(requestingAgentID, boolFields);
            if (remoteValue != null || m_doRemoteOnly)
                return (uint) remoteValue; // note: this is bad, you can't cast a null object to a uint

            QueryFilter filter = new QueryFilter();

            string[] BoolFields = {"OpenEnrollment", "ShowInList", "AllowPublish", "MaturePublish"};
            foreach (string field in BoolFields)
            {
                if (boolFields.ContainsKey(field))
                {
                    filter.andFilters[field] = boolFields[field] ? "1" : "0";
                }
            }

            return uint.Parse(GD.Query(new[] {"COUNT(GroupID)"}, _DATAREALM, filter, null, null, null)[0]);
        }

        static GroupRecord GroupRecordQueryResult2GroupRecord(List<String> result)
        {
            return new GroupRecord
                       {
                           GroupID = UUID.Parse(result[0]),
                           GroupName = result[1],
                           Charter = result[2],
                           GroupPicture = UUID.Parse(result[3]),
                           FounderID = UUID.Parse(result[4]),
                           MembershipFee = int.Parse(result[5]),
                           OpenEnrollment = int.Parse(result[6]) == 1,
                           ShowInList = int.Parse(result[7]) == 1,
                           AllowPublish = int.Parse(result[8]) == 1,
                           MaturePublish = int.Parse(result[9]) == 1,
                           OwnerRoleID = UUID.Parse(result[10])
                       };
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List <UUID> GetAllGroups(UUID requestingAgentID)
        {
            object remoteValue = DoRemote(requestingAgentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<UUID>) remoteValue;

            // maybe check for system user??
            if (!Utilities.IsSystemUser(requestingAgentID))
                return new List<UUID>();

            QueryFilter filter = new QueryFilter();
            List<string> groupsData = GD.Query(new[] { "GroupID" }, _DATAREALM, filter, null, null, null);

            if (groupsData == null)
                return new List <UUID>();

            List <UUID> groupIDs = new List <UUID>();
            foreach (var id in groupsData)
                groupIDs.Add((UUID) id);

            return groupIDs;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupRecord GetGroupRecord(UUID requestingAgentID, UUID groupID, string groupName)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, groupName);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupRecord) remoteValue;

            QueryFilter filter = new QueryFilter();

            if (groupID != UUID.Zero)
            {
                filter.andFilters["GroupID"] = groupID;
            }
            if (!string.IsNullOrEmpty(groupName))
            {
                filter.andFilters["Name"] = groupName;
            }
            if (filter.Count == 0)
            {
                return null;
            }
            List<string> osgroupsData = GD.Query(new[]
                                                       {
                                                           "GroupID",
                                                           "Name",
                                                           "Charter",
                                                           "InsigniaID",
                                                           "FounderID",
                                                           "MembershipFee",
                                                           "OpenEnrollment",
                                                           "ShowInList",
                                                           "AllowPublish",
                                                           "MaturePublish",
                                                           "OwnerRoleID"
            }, _DATAREALM, filter, null, null, null);
            return (osgroupsData.Count == 0) ? null : GroupRecordQueryResult2GroupRecord(osgroupsData);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupRecord> GetGroupRecords(UUID requestingAgentID, uint start, uint count,
                                                 Dictionary<string, bool> sort, Dictionary<string, bool> boolFields)
        {
            object remoteValue = DoRemote(requestingAgentID, start, count, boolFields);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupRecord>) remoteValue;

            string[] sortAndBool = {"OpenEnrollment", "MaturePublish"};
            string[] BoolFields = {"OpenEnrollment", "ShowInList", "AllowPublish", "MaturePublish"};

            foreach (string field in sortAndBool)
            {
                if (boolFields.ContainsKey(field) && sort.ContainsKey(field))
                {
                    sort.Remove(field);
                }
            }

            QueryFilter filter = new QueryFilter();

            foreach (string field in BoolFields)
            {
                if (boolFields.ContainsKey(field))
                {
                    filter.andFilters[field] = boolFields[field] ? "1" : "0";
                }
            }

            List<GroupRecord> Reply = new List<GroupRecord>();

            List<string> osgroupsData = GD.Query(new[]
                                                       {
                                                           "GroupID",
                                                           "Name",
                                                           "Charter",
                                                           "InsigniaID",
                                                           "FounderID",
                                                           "MembershipFee",
                                                           "OpenEnrollment",
                                                           "ShowInList",
                                                           "AllowPublish",
                                                           "MaturePublish",
                                                           "OwnerRoleID"
            }, _DATAREALM, filter, sort, start, count);

            if (osgroupsData.Count < 11)
            {
                return Reply;
            }
            for (int i = 0; i < osgroupsData.Count; i += 11)
            {
                Reply.Add(GroupRecordQueryResult2GroupRecord(osgroupsData.GetRange(i, 11)));
            }
            return Reply;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupRecord> GetGroupRecords(UUID requestingAgentID, List<UUID> groupIDList)
        {
            object remoteValue = DoRemote(requestingAgentID, groupIDList);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupRecord>) remoteValue;

            List<GroupRecord> Reply = new List<GroupRecord>(0);
            if (groupIDList.Count <= 0)
            {
                return Reply;
            }

            QueryFilter filter = new QueryFilter();
            filter.orMultiFilters["GroupID"] = new List<object>();
            foreach (UUID groupID in groupIDList)
            {
                filter.orMultiFilters["GroupID"].Add(groupID);
            }

            List<string> osgroupsData = GD.Query(new[]
                                                       {
                                                           "GroupID",
                                                           "Name",
                                                           "Charter",
                                                           "InsigniaID",
                                                           "FounderID",
                                                           "MembershipFee",
                                                           "OpenEnrollment",
                                                           "ShowInList",
                                                           "AllowPublish",
                                                           "MaturePublish",
                                                           "OwnerRoleID"
            }, _DATAREALM, filter, null, null, null);

            if (osgroupsData.Count < 11)
            {
                return Reply;
            }
            for (int i = 0; i < osgroupsData.Count; i += 11)
            {
                Reply.Add(GroupRecordQueryResult2GroupRecord(osgroupsData.GetRange(i, 11)));
            }
            return Reply;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupProfileData GetMemberGroupProfile(UUID requestingAgentID, UUID groupID, UUID agentID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, agentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupProfileData) remoteValue;

            if (!CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.MemberVisible))
                return new GroupProfileData();

            GroupProfileData GPD = new GroupProfileData();
            GroupRecord record = GetGroupRecord(requestingAgentID, groupID, null);

            QueryFilter filter1 = new QueryFilter();
            filter1.andFilters["GroupID"] = agentID; // yes these look the wrong way around
            filter1.andFilters["AgentID"] = groupID; // but they were like that when I got here! ~ SignpostMarv

            QueryFilter filter2 = new QueryFilter();
            filter2.andFilters["GroupID"] = groupID;

            List<string> Membership = GD.Query(new[]
                                                     {
                                                         "Contribution",
                                                         "ListInProfile",
                                                         "SelectedRoleID"
            }, _MEMBERSHIPREALM, filter1, null, null, null);

            int GroupMemCount =
                int.Parse(GD.Query(new[] {"COUNT(AgentID)"}, _MEMBERSHIPREALM, filter2, null, null, null)[0]);

            int GroupRoleCount = int.Parse(GD.Query(new[] {"COUNT(RoleID)"}, _ROLEREALM, filter2, null, null, null)[0]);

            QueryFilter filter3 = new QueryFilter();
            filter3.andFilters["RoleID"] = Membership[2];
            List<string> GroupRole = GD.Query(new[]
                                                    {
                                                        "Name",
                                                        "Powers"
            }, _ROLEREALM, filter3, null, null, null);

            GPD.AllowPublish = record.AllowPublish;
            GPD.Charter = record.Charter;
            GPD.FounderID = record.FounderID;
            GPD.GroupID = record.GroupID;
            GPD.GroupMembershipCount = GroupMemCount;
            GPD.GroupRolesCount = GroupRoleCount;
            GPD.InsigniaID = record.GroupPicture;
            GPD.MaturePublish = record.MaturePublish;
            GPD.MembershipFee = record.MembershipFee;
            GPD.MemberTitle = GroupRole[0];
            GPD.Money = 0;

            GPD.Name = record.GroupName;
            GPD.OpenEnrollment = record.OpenEnrollment;
            GPD.OwnerRole = record.OwnerRoleID;
            GPD.PowersMask = ulong.Parse(GroupRole[1]);
            GPD.ShowInList = int.Parse(Membership[2]) == 1;

            return GPD;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupMembershipData GetGroupMembershipData(UUID requestingAgentID, UUID groupID, UUID agentID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, agentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupMembershipData) remoteValue;

            if (groupID == UUID.Zero)
                groupID = GetAgentActiveGroup(requestingAgentID, agentID);
            if (groupID == UUID.Zero)
                return null;

            QueryTables tables = new QueryTables();
            tables.AddTable(_DATAREALM, "osg");
            tables.AddTable(_MEMBERSHIPREALM, "osgm", JoinType.Inner, new[,] {{"osg.GroupID", "osgm.GroupID"}});
            tables.AddTable(_ROLEREALM, "osr", JoinType.Inner,
                            new[,] {{"osgm.SelectedRoleID", "osr.RoleID"}, {"osr.GroupID", "osg.GroupID"}});

            QueryFilter filter = new QueryFilter();
            filter.andFilters["osg.GroupID"] = groupID;
            filter.andFilters["osgm.AgentID"] = agentID;

            string[] fields = new[]
                                  {
                                      "osgm.AcceptNotices",
                                      "osgm.Contribution",
                                      "osgm.ListInProfile",
                                      "osgm.SelectedRoleID",
                                      "osr.Title",
                                      "osr.Powers",
                                      "osg.AllowPublish",
                                      "osg.Charter",
                                      "osg.FounderID",
                                      "osg.Name",
                                      "osg.InsigniaID",
                                      "osg.MaturePublish",
                                      "osg.MembershipFee",
                                      "osg.OpenEnrollment",
                                      "osg.ShowInList"
                                  };
            List<string> Membership = GD.Query(fields, tables, filter, null, null, null);

            if (fields.Length != Membership.Count)
                return null;

            GroupMembershipData GMD = new GroupMembershipData
                                          {
                                              AcceptNotices = int.Parse(Membership[0]) == 1,
                                              Active = true, //TODO: Figure out what this is and its effects if false
                                              ActiveRole = UUID.Parse(Membership[3]),
                                              AllowPublish = int.Parse(Membership[6]) == 1,
                                              Charter = Membership[7],
                                              Contribution = int.Parse(Membership[1]),
                                              FounderID = UUID.Parse(Membership[8]),
                                              GroupID = groupID,
                                              GroupName = Membership[9],
                                              GroupPicture = UUID.Parse(Membership[10]),
                                              GroupPowers = ulong.Parse(Membership[5]),
                                              GroupTitle = Membership[4],
                                              ListInProfile = int.Parse(Membership[2]) == 1,
                                              MaturePublish = int.Parse(Membership[11]) == 1,
                                              MembershipFee = int.Parse(Membership[12]),
                                              OpenEnrollment = int.Parse(Membership[13]) == 1,
                                              ShowInList = int.Parse(Membership[14]) == 1
                                          };


            return GMD;
        }


        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupTitlesData> GetGroupTitles(UUID requestingAgentID, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupTitlesData>) remoteValue;

            QueryTables tables = new QueryTables();
            tables.AddTable(_MEMBERSHIPREALM, "osgm");
            tables.AddTable(_MEMBERSHIPROLEREALM, "osgrm", JoinType.Inner,
                            new[,] {{"osgm.AgentID", "osgrm.AgentID"}, {"osgm.GroupID", "osgrm.GroupID"}});
            tables.AddTable(_ROLEREALM, "osr", JoinType.Inner,
                            new[,] {{"osgrm.RoleID", "osr.RoleID"}, {"osgm.GroupID", "osr.GroupID"}});


            QueryFilter filter = new QueryFilter();
            filter.andFilters["osgm.AgentID"] = requestingAgentID;
            filter.andFilters["osgm.GroupID"] = groupID;

            List<string> Membership = GD.Query(new[]
                                                     {
                                                         "osgm.SelectedRoleID",
                                                         "osgrm.RoleID",
                                                         "osr.Name"
                                                     }, tables, filter, null, null, null);


            List<GroupTitlesData> titles = new List<GroupTitlesData>();
            for (int loop = 0; loop < Membership.Count(); loop += 3)
            {
                titles.Add(new GroupTitlesData
                               {
                                   Name = Membership[loop + 2],
                                   UUID = UUID.Parse(Membership[loop + 1]),
                                   Selected = Membership[loop + 0] == Membership[loop + 1]
                               });
            }
            return titles;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupMembershipData> GetAgentGroupMemberships(UUID requestingAgentID, UUID AgentID)
        {
            object remoteValue = DoRemote(requestingAgentID, AgentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupMembershipData>) remoteValue;

            QueryTables tables = new QueryTables();
            tables.AddTable(_DATAREALM, "osg");
            tables.AddTable(_MEMBERSHIPREALM, "osgm", JoinType.Inner, new[,] {{"osg.GroupID", "osgm.GroupID"}});
            tables.AddTable(_ROLEREALM, "osr", JoinType.Inner, new[,] {{"osgm.SelectedRoleID", "osr.RoleID"}});

            QueryFilter filter = new QueryFilter();
            filter.andFilters["osgm.AgentID"] = AgentID;

            string[] fields = new[]
                                  {
                                      "osgm.AcceptNotices",
                                      "osgm.Contribution",
                                      "osgm.ListInProfile",
                                      "osgm.SelectedRoleID",
                                      "osr.Title",
                                      "osr.Powers",
                                      "osg.AllowPublish",
                                      "osg.Charter",
                                      "osg.FounderID",
                                      "osg.Name",
                                      "osg.InsigniaID",
                                      "osg.MaturePublish",
                                      "osg.MembershipFee",
                                      "osg.OpenEnrollment",
                                      "osg.ShowInList",
                                      "osg.GroupID"
                                  };
            List<string> Membership = GD.Query(fields, tables, filter, null, null, null);
            List<GroupMembershipData> results = new List<GroupMembershipData>();
            for (int loop = 0; loop < Membership.Count; loop += fields.Length)
            {
                results.Add(new GroupMembershipData
                                {
                                    AcceptNotices = int.Parse(Membership[loop + 0]) == 1,
                                    Active = true,
                                    //TODO: Figure out what this is and its effects if false
                                    ActiveRole = UUID.Parse(Membership[loop + 3]),
                                    AllowPublish = int.Parse(Membership[loop + 6]) == 1,
                                    Charter = Membership[loop + 7],
                                    Contribution = int.Parse(Membership[loop + 1]),
                                    FounderID = UUID.Parse(Membership[loop + 8]),
                                    GroupID = UUID.Parse(Membership[loop + 15]),
                                    GroupName = Membership[loop + 9],
                                    GroupPicture = UUID.Parse(Membership[loop + 10]),
                                    GroupPowers = ulong.Parse(Membership[loop + 5]),
                                    GroupTitle = Membership[loop + 4],
                                    ListInProfile = int.Parse(Membership[loop + 2]) == 1,
                                    MaturePublish = int.Parse(Membership[loop + 11]) == 1,
                                    MembershipFee = int.Parse(Membership[loop + 12]),
                                    OpenEnrollment = int.Parse(Membership[loop + 13]) == 1,
                                    ShowInList = int.Parse(Membership[loop + 14]) == 1
                                });
            }
            return results;
        }



        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupInviteInfo GetAgentToGroupInvite(UUID requestingAgentID, UUID inviteID)
        {
            object remoteValue = DoRemote(requestingAgentID, inviteID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupInviteInfo) remoteValue;

            GroupInviteInfo invite = new GroupInviteInfo();

            Dictionary<string, object> where = new Dictionary<string, object>(2);
            where["AgentID"] = requestingAgentID;
            where["InviteID"] = inviteID;

            List<string> groupInvite = GD.Query(new[] {"*"}, _INVITEREALM,
                new QueryFilter{ andFilters = where }, null, null, null);

            if (groupInvite.Count == 0)
            {
                return null;
            }
            invite.AgentID = UUID.Parse(groupInvite[3]);
            invite.GroupID = UUID.Parse(groupInvite[1]);
            invite.InviteID = UUID.Parse(groupInvite[0]);
            invite.RoleID = UUID.Parse(groupInvite[2]);
            invite.FromAgentName = groupInvite[5];

            return invite;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupInviteInfo> GetGroupInvites(UUID requestingAgentID)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["AgentID"] = requestingAgentID;

            object remoteValue = DoRemote(requestingAgentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupInviteInfo>) remoteValue;

            List<string> groupInvite = GD.Query(new[] {"*"}, _INVITEREALM, filter, null, null, null);

            List<GroupInviteInfo> invites = new List<GroupInviteInfo>();

            for (int i = 0; i < groupInvite.Count; i += 6)
            {
                invites.Add(new GroupInviteInfo
                                {
                                    AgentID = UUID.Parse(groupInvite[i + 3]),
                                    GroupID = UUID.Parse(groupInvite[i + 1]),
                                    InviteID = UUID.Parse(groupInvite[i]),
                                    RoleID = UUID.Parse(groupInvite[i + 2]),
                                    FromAgentName = groupInvite[i + 5]
                                });
            }

            return invites;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupMembersData GetAgentGroupMemberData(UUID requestingAgentID, UUID groupID, UUID agentID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, agentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupMembersData) remoteValue;


            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            filter.andFilters["AgentID"] = agentID;

            List<string> Membership = GD.Query(new string[4]
                                                     {
                                                         "AcceptNotices",
                                                         "Contribution",
                                                         "ListInProfile",
                                                         "SelectedRoleID"
            }, _MEMBERSHIPREALM, filter, null, null, null);

            if (Membership.Count != 4)
            {
                return null;
            }

            filter.andFilters.Remove("AgentID");
            filter.andFilters["RoleID"] = Membership[3];

            List<string> GroupRole = GD.Query(new string[2]
                                                    {
                                                        "Title",
                                                        "Powers"
            }, _ROLEREALM, filter, null, null, null);

            if (GroupRole.Count != 2)
            {
                return null;
            }

            filter.andFilters.Remove("RoleID");

            List<string> OwnerRoleID = GD.Query(new string[1]
                                                      {
                                                          "OwnerRoleID"
            }, _DATAREALM, filter, null, null, null);

            filter.andFilters["RoleID"] = OwnerRoleID[0];
            filter.andFilters["AgentID"] = agentID;

            bool IsOwner = uint.Parse(GD.Query(new string[1]
                                                     {
                                                         "COUNT(AgentID)"
            }, _MEMBERSHIPROLEREALM, filter, null, null, null)[0]) == 1;

            return new GroupMembersData
                       {
                           AcceptNotices = (Membership[0]) == "1",
                           AgentID = agentID,
                           Contribution = int.Parse(Membership[1]),
                           IsOwner = IsOwner,
                           ListInProfile = (Membership[2]) == "1",
                           AgentPowers = ulong.Parse(GroupRole[1]),
                           Title = GroupRole[0],
                           OnlineStatus = "(Online)"
                       };
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupMembersData> GetGroupMembers(UUID requestingAgentID, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupMembersData>) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            List<string> Agents = GD.Query(new[] {"AgentID"}, _MEMBERSHIPREALM, filter, null, null, null);

            List<GroupMembersData> list = new List<GroupMembersData>();
            foreach (string agent in Agents)
            {
                GroupMembersData d = GetAgentGroupMemberData(requestingAgentID, groupID, UUID.Parse(agent));
                if (d == null) continue;
                UserInfo info =
                    m_registry.RequestModuleInterface<IAgentInfoService>().GetUserInfo(
                        d.AgentID.ToString());
                if (info != null && !info.IsOnline)
                    d.OnlineStatus = info.LastLogin.ToShortDateString();
                else if (info == null)
                    d.OnlineStatus = "Unknown";
                else
                    d.OnlineStatus = "Online";
                list.Add(d);
            }
            return list;
        }


        // Banned users
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupBannedAgentsData> GetGroupBannedMembers(UUID requestingAgentID, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupBannedAgentsData>) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            List<string> bannedAgents = GD.Query(new[] {"AgentID, BanTime"}, _BANREALM, filter, null, null, null);

            var userList = new List<GroupBannedAgentsData>();
            if (bannedAgents.Count == 0)
            {
                return userList;
            }

            for (int i = 0; i < bannedAgents.Count; i += 2)
            {
                GroupBannedAgentsData banUser = new GroupBannedAgentsData();
                banUser.AgentID = UUID.Parse (bannedAgents [i]);
                banUser.BanDate = DateTime.Parse(bannedAgents [i+1]).ToLocalTime();    

                userList.Add(banUser);
            }
            return userList;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void AddGroupBannedAgent(UUID requestingAgentID, UUID groupID, List<UUID> bannedUserID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, bannedUserID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.GroupBanAccess))
            {
                foreach (UUID userID in bannedUserID)
                {
                    Dictionary<string, object> row = new Dictionary<string, object> (3);
                    row ["GroupID"] = groupID;
                    row ["AgentID"] = userID;
                    row ["BanTime"] = DateTime.UtcNow; 

                    GD.Insert (_BANREALM, row);
                }
            }
        }


        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void RemoveGroupBannedAgent(UUID requestingAgentID, UUID groupID, List<UUID> bannedUserID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, bannedUserID);
            if (remoteValue != null || m_doRemoteOnly)
                return;

            if (CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.GroupBanAccess))
            {
                foreach (UUID userID in bannedUserID)
                {
                    QueryFilter filter = new QueryFilter ();
                    filter.andFilters ["GroupID"] = groupID;
                    filter.andFilters ["AgentID"] = userID;

                    GD.Delete (_BANREALM, filter);
                }
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupBannedAgentsData GetGroupBannedUser(UUID requestingAgentID, UUID groupID, UUID agentID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID, agentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupBannedAgentsData) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            filter.andFilters ["AgentID"] = agentID;

            List<string>  bUser = GD.Query(new[] {"AgentID, BanTime"}, _BANREALM, filter, null, null, null);

            GroupBannedAgentsData bannedUser = new GroupBannedAgentsData ();
            if (bUser.Count > 0)
            {
                bannedUser.AgentID = UUID.Parse (bUser [0]);
                bannedUser.BanDate = Util.ToDateTime (int.Parse (bUser [1]));   
            }
                            
            return bannedUser;
        }


        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool IsGroupBannedUser(UUID groupID, UUID agentID)
        {
            object remoteValue = DoRemote(groupID, agentID);
            if (remoteValue != null || m_doRemoteOnly)
                return (bool) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;
            filter.andFilters ["AgentID"] = agentID;

            List<string>  banned = GD.Query(new[] {"AgentID"}, _BANREALM, filter, null, null, null);
            bool isBanned = (banned.Count > 0);        // true if found (banned)

            return isBanned;
        }

        // Search
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<DirGroupsReplyData> FindGroups(UUID requestingAgentID, string search, uint? start, uint? count,
                                                   uint queryflags)
        {
            object remoteValue = DoRemote(requestingAgentID, search, start, count, queryflags);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<DirGroupsReplyData>) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andLikeFilters["Name"] = "%" + search + "%";

            List<string> retVal = GD.Query(new[]
                                                 {
                                                     "GroupID",
                                                     "Name",
                                                     "ShowInList",
                                                     "AllowPublish",
                                                     "MaturePublish"
            }, _DATAREALM, filter, null, start, count);

            List<DirGroupsReplyData> Reply = new List<DirGroupsReplyData>();

            for (int i = 0; i < retVal.Count; i += 5)
            {
                if (retVal[i + 2] == "0") // (ShowInList param) They don't want to be shown in search.. respect this
                {
                    continue;
                }

                if ((queryflags & (uint) DirectoryManager.DirFindFlags.IncludeMature) !=
                    (uint) DirectoryManager.DirFindFlags.IncludeMature)
                {
                    if (retVal[i + 4] == "1") // (MaturePublish param) Check for pg,mature
                    {
                        continue;
                    }
                }

                DirGroupsReplyData dirgroup = new DirGroupsReplyData
                                                  {
                                                      groupID = UUID.Parse(retVal[i]),
                                                      groupName = retVal[i + 1]
                                                  };
                filter = new QueryFilter();
                filter.andFilters["GroupID"] = dirgroup.groupID;
                dirgroup.members =
                    int.Parse(GD.Query(new[] {"COUNT(AgentID)"}, _MEMBERSHIPREALM, filter, null, null, null)[0]);

                Reply.Add(dirgroup);
            }
            return Reply;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupRolesData> GetAgentGroupRoles(UUID requestingAgentID, UUID agentID, UUID groupID)
        {
            // I couldn't actually get this function to call when testing changes
            object remoteValue = DoRemote(requestingAgentID, agentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupRolesData>) remoteValue;

            //No permissions check necessary, we are checking only roles that they are in, so if they arn't in the group, that isn't a problem

            QueryTables tables = new QueryTables();
            tables.AddTable(_MEMBERSHIPROLEREALM, "osgm");
            tables.AddTable(_ROLEREALM, "osr", JoinType.Inner, new[,] {{"osgm.RoleID", "osr.RoleID"}});

            QueryFilter filter = new QueryFilter();
            filter.andFilters["osgm.AgentID"] = agentID;
            filter.andFilters["osgm.GroupID"] = groupID;

            string[] fields = new[]
                                  {
                                      "osr.Name",
                                      "osr.Description",
                                      "osr.Title",
                                      "osr.Powers",
                                      "osr.RoleID"
                                  };
            List<string> Roles = GD.Query(fields, tables, filter, null, null, null);

            filter = new QueryFilter();

            List<GroupRolesData> RolesData = new List<GroupRolesData>();

            for (int loop = 0; loop < Roles.Count; loop += fields.Length)
            {
                RolesData.Add(new GroupRolesData
                                  {
                                      RoleID = UUID.Parse(Roles[loop + 4]),
                                      Name = Roles[loop + 0],
                                      Description = Roles[loop + 1],
                                      Powers = ulong.Parse(Roles[loop + 3]),
                                      Title = Roles[loop + 2]
                                  });
            }

            return RolesData;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupRolesData> GetGroupRoles(UUID requestingAgentID, UUID groupID)
        {
            // Can't use joins here without a group by as well
            object remoteValue = DoRemote(requestingAgentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupRolesData>) remoteValue;

            if (!CheckGroupPermissions(requestingAgentID, groupID, (ulong) GroupPowers.None))
            {
                return new List<GroupRolesData>(0);
            }

            List<GroupRolesData> GroupRoles = new List<GroupRolesData>();

            QueryFilter rolesFilter = new QueryFilter();
            rolesFilter.andFilters["GroupID"] = groupID;
            List<string> Roles = GD.Query(new[]
                                                {
                                                    "Name",
                                                    "Description",
                                                    "Title",
                                                    "Powers",
                                                    "RoleID"
            }, _ROLEREALM, rolesFilter, null, null, null);

            QueryFilter filter = new QueryFilter();
            filter.andFilters["GroupID"] = groupID;

            for (int i = 0; i < Roles.Count; i += 5)
            {
                filter.andFilters["RoleID"] = UUID.Parse(Roles[i + 4]);
                int Count =
                    int.Parse(GD.Query(new[] {"COUNT(AgentID)"}, _MEMBERSHIPROLEREALM, filter, null, null, null)[0]);

                GroupRoles.Add(new GroupRolesData
                                   {
                                       Members = Count,
                                       RoleID = UUID.Parse(Roles[i + 4]),
                                       Name = Roles[i + 0],
                                       Description = Roles[i + 1],
                                       Powers = ulong.Parse(Roles[i + 3]),
                                       Title = Roles[i + 2]
                                   });
            }
            return GroupRoles;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupRoleMembersData> GetGroupRoleMembers(UUID requestingAgentID, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupRoleMembersData>) remoteValue;

            List<GroupRoleMembersData> RoleMembers = new List<GroupRoleMembersData>();

            QueryTables tables = new QueryTables();
            tables.AddTable(_MEMBERSHIPROLEREALM, "osgrm");
            tables.AddTable(_ROLEREALM, "osr", JoinType.Inner, new[,] {{"osr.RoleID", "osgrm.RoleID"}});

            QueryFilter filter = new QueryFilter();
            filter.andFilters["osgrm.GroupID"] = groupID;
            string[] fields = new[]
                                  {
                                      "osgrm.RoleID",
                                      "osgrm.AgentID",
                                      "osr.Powers"
                                  };
            List<string> Roles = GD.Query(fields, tables, filter, null, null, null);

            GroupMembersData GMD = GetAgentGroupMemberData(requestingAgentID, groupID, requestingAgentID);
            const long canViewMemebersBit = 140737488355328L;
            for (int i = 0; i < Roles.Count; i += fields.Length)
            {
                GroupRoleMembersData RoleMember = new GroupRoleMembersData
                                                      {
                                                          RoleID = UUID.Parse(Roles[i]),
                                                          MemberID = UUID.Parse(Roles[i + 1])
                                                      };

                // if they are a member, they can see everyone, otherwise, only the roles that are supposed to be shown
                if (GMD != null ||
                    ((long.Parse(Roles[i + 2]) & canViewMemebersBit) == canViewMemebersBit ||
                     RoleMember.MemberID == requestingAgentID))
                    RoleMembers.Add(RoleMember);
            }

            return RoleMembers;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupNoticeData GetGroupNoticeData(UUID requestingAgentID, UUID noticeID)
        {
            object remoteValue = DoRemote(requestingAgentID, noticeID);
            if (remoteValue != null || m_doRemoteOnly)
            {
                return (GroupNoticeData) remoteValue;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["NoticeID"] = noticeID;
            string[] fields = new string[9]
                                  {
                                      "GroupID",
                                      "Timestamp",
                                      "FromName",
                                      "Subject",
                                      "ItemID",
                                      "HasAttachment",
                                      "Message",
                                      "AssetType",
                                      "ItemName"
                                  };
            List<string> notice = GD.Query(fields, _NOTICEREALM, filter, null, null, null);

            if (notice.Count != fields.Length)
            {
                return null;
            }

            GroupNoticeData GND = new GroupNoticeData
                                      {
                                          GroupID = UUID.Parse(notice[0]),
                                          NoticeID = noticeID,
                                          Timestamp = uint.Parse(notice[1]),
                                          FromName = notice[2],
                                          Subject = notice[3],
                                          HasAttachment = int.Parse(notice[5]) == 1
                                      };
            if (GND.HasAttachment)
            {
                GND.ItemID = UUID.Parse(notice[4]);
                GND.AssetType = (byte) int.Parse(notice[7]);
                GND.ItemName = notice[8];
            }

            return GND;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupNoticeInfo GetGroupNotice(UUID requestingAgentID, UUID noticeID)
        {
            object remoteValue = DoRemote(requestingAgentID, noticeID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupNoticeInfo) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["NoticeID"] = noticeID;
            string[] fields = new string[9]
                                  {
                                      "GroupID",
                                      "Timestamp",
                                      "FromName",
                                      "Subject",
                                      "ItemID",
                                      "HasAttachment",
                                      "Message",
                                      "AssetType",
                                      "ItemName"
                                  };
            List<string> notice = GD.Query(fields, _NOTICEREALM, filter, null, null, null);

            if (notice.Count != fields.Length)
            {
                return null;
            }

            GroupNoticeData GND = new GroupNoticeData
                                      {
                                          NoticeID = noticeID,
                                          Timestamp = uint.Parse(notice[1]),
                                          FromName = notice[2],
                                          Subject = notice[3],
                                          HasAttachment = int.Parse(notice[5]) == 1
                                      };
            if (GND.HasAttachment)
            {
                GND.ItemID = UUID.Parse(notice[4]);
                GND.AssetType = (byte) int.Parse(notice[7]);
                GND.ItemName = notice[8];
            }

            GroupNoticeInfo info = new GroupNoticeInfo
                                       {
                                           BinaryBucket = new byte[0],
                                           GroupID = UUID.Parse(notice[0]),
                                           Message = notice[6],
                                           noticeData = GND
                                       };

            return (!agentsCanBypassGroupNoticePermsCheck.Contains(requestingAgentID) &&
                    !CheckGroupPermissions(requestingAgentID, info.GroupID, (ulong) GroupPowers.ReceiveNotices))
                       ? null
                       : info;
        }

        static GroupNoticeData GroupNoticeQueryResult2GroupNoticeData(List<string> result)
        {
            GroupNoticeData GND = new GroupNoticeData
                                      {
                                          GroupID = UUID.Parse(result[0]),
                                          NoticeID = UUID.Parse(result[6]),
                                          Timestamp = uint.Parse(result[1]),
                                          FromName = result[2],
                                          Subject = result[3],
                                          HasAttachment = int.Parse(result[5]) == 1
                                      };
            if (GND.HasAttachment)
            {
                GND.ItemID = UUID.Parse(result[4]);
                GND.AssetType = (byte) int.Parse(result[8]);
                GND.ItemName = result[9];
            }
            return GND;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupNoticeData> GetGroupNotices(UUID requestingAgentID, uint start, uint count, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, start, count, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupNoticeData>) remoteValue;

            return GetGroupNotices(requestingAgentID, start, count, new List<UUID>(new[] {groupID}));
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupNoticeData> GetGroupNotices(UUID requestingAgentID, uint start, uint count, List<UUID> groupIDList)
        {
            object remoteValue = DoRemote(requestingAgentID, start, count, groupIDList);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupNoticeData>) remoteValue;

            List<UUID> groupIDs = new List<UUID>();
            if (!agentsCanBypassGroupNoticePermsCheck.Contains(requestingAgentID))
            {
                groupIDs.AddRange(
                    groupIDList.Where(
                        GroupID => CheckGroupPermissions(requestingAgentID, GroupID, (ulong) GroupPowers.ReceiveNotices)));
            }
            else
            {
                groupIDs = groupIDList;
            }

            List<GroupNoticeData> AllNotices = new List<GroupNoticeData>();
            if (groupIDs.Count > 0)
            {
                QueryFilter filter = new QueryFilter();
                filter.orMultiFilters["GroupID"] = new List<object>(groupIDs.Count);
                foreach (UUID groupID in groupIDs)
                {
                    filter.orMultiFilters["GroupID"].Add(groupID);
                }

                Dictionary<string, bool> sort = new Dictionary<string, bool>(1);
                sort["Timestamp"] = false;

                uint? s = null;
                if (start != 0)
                    s = start;
                uint? c = null;
                if (count != 0)
                    c = count;

                List<string> notice = GD.Query(new[]
                                                     {
                                                         "GroupID",
                                                         "Timestamp",
                                                         "FromName",
                                                         "Subject",
                                                         "ItemID",
                                                         "HasAttachment",
                                                         "NoticeID",
                                                         "Message",
                                                         "AssetType",
                                                         "ItemName"
                }, _NOTICEREALM, filter, sort, s, c);

                for (int i = 0; i < notice.Count; i += 10)
                {
                    AllNotices.Add(GroupNoticeQueryResult2GroupNoticeData(notice.GetRange(i, 10)));
                }
            }
            return AllNotices;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupProfileData GetGroupProfile(UUID requestingAgentID, UUID groupID)
        {
            object remoteValue = DoRemote(requestingAgentID, groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupProfileData) remoteValue;

            GroupProfileData profile = new GroupProfileData();

            GroupRecord groupInfo = GetGroupRecord(requestingAgentID, groupID, null);
            if (groupInfo != null)
            {
                profile.AllowPublish = groupInfo.AllowPublish;
                profile.Charter = groupInfo.Charter;
                profile.FounderID = groupInfo.FounderID;
                profile.GroupID = groupID;
                profile.GroupMembershipCount =
                    GetGroupMembers(requestingAgentID, groupID).Count;
                profile.GroupRolesCount = GetGroupRoles(requestingAgentID, groupID).Count;
                profile.InsigniaID = groupInfo.GroupPicture;
                profile.MaturePublish = groupInfo.MaturePublish;
                profile.MembershipFee = groupInfo.MembershipFee;
                profile.Money = 0; // TODO: Get this from the currency server?
                profile.Name = groupInfo.GroupName;
                profile.OpenEnrollment = groupInfo.OpenEnrollment;
                profile.OwnerRole = groupInfo.OwnerRoleID;
                profile.ShowInList = groupInfo.ShowInList;
            }

            GroupMembershipData memberInfo = GetGroupMembershipData(requestingAgentID,
                                                                    groupID,
                                                                    requestingAgentID);
            if (memberInfo != null)
            {
                profile.MemberTitle = memberInfo.GroupTitle;
                profile.PowersMask = memberInfo.GroupPowers;
            }

            return profile;
        }

        #endregion

        public void Dispose()
        {
        }

        public bool CheckGroupPermissions(UUID agentID, UUID groupID, ulong permissions)
        {
            if (groupID == UUID.Zero)
                return false;

            if (agentID == UUID.Zero)
                return false;

            GroupMembersData GMD = GetAgentGroupMemberData(agentID, groupID, agentID);
            GroupRecord record = GetGroupRecord(agentID, groupID, null);
            if (permissions == 0)
            {
                if (GMD != null || record.FounderID == agentID || record.OpenEnrollment)
                    return true;
                return false;
            }

            if (record != null && record.FounderID == agentID)
                return true;

            if (GMD == null)
                return false;

            if ((GMD.AgentPowers & permissions) != permissions)
                return false;

            return true;
        }
    }
}
