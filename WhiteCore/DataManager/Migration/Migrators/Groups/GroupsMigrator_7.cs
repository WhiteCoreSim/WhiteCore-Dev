﻿/*
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
using WhiteCore.Framework.Utilities;

namespace WhiteCore.DataManager.Migration.Migrators.Groups
{
    public class GroupsMigrator_7 : Migrator
    {
        public GroupsMigrator_7()
        {
            Version = new Version(0, 0, 7);
            MigrationName = "Groups";

            Schema = new List<SchemaDefinition>();

            //
            // Change summary: April 1 2016
            //
            //   Change ID type fields to type UUID
            //

            AddSchema("group_agent", ColDefs(
                ColDef("AgentID", ColumnTypes.UUID),
                ColDef("ActiveGroupID", ColumnTypes.UUID)
            ), IndexDefs(
                IndexDef(new string[1] {"AgentID"}, IndexType.Primary)
            ));

            AddSchema("group_data", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("Name", ColumnTypes.String50),
                ColDef("Charter", ColumnTypes.Text),
                ColDef("InsigniaID", ColumnTypes.UUID),
                ColDef("FounderID", ColumnTypes.UUID),
                ColDef("MembershipFee", ColumnTypes.String50),
                ColDef("OpenEnrollment", ColumnTypes.String50),
                ColDef("ShowInList", ColumnTypes.String50),
                ColDef("AllowPublish", ColumnTypes.String50),
                ColDef("MaturePublish", ColumnTypes.String50),
                ColDef("OwnerRoleID", ColumnTypes.UUID)
            ), IndexDefs(
                IndexDef(new string[1] {"GroupID"}, IndexType.Primary),
                IndexDef(new string[1] {"Name"}, IndexType.Unique)
            ));

            AddSchema("group_invite", ColDefs(
                ColDef("InviteID", ColumnTypes.UUID),
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("RoleID", ColumnTypes.UUID),
                ColDef("AgentID", ColumnTypes.UUID),
                ColDef("TMStamp", ColumnTypes.String50),
                ColDef("FromAgentName", ColumnTypes.String50)
            ), IndexDefs(
                IndexDef(new string[4] {"InviteID", "GroupID", "RoleID", "AgentID"},
                    IndexType.Primary),
                IndexDef(new string[2] {"AgentID", "InviteID"}, IndexType.Index)
            ));

            AddSchema("group_membership", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("AgentID", ColumnTypes.UUID),
                ColDef("SelectedRoleID", ColumnTypes.UUID),
                ColDef("Contribution", ColumnTypes.String45),
                ColDef("ListInProfile", ColumnTypes.String45),
                ColDef("AcceptNotices", ColumnTypes.String45)
            ), IndexDefs(
                IndexDef(new string[2] {"GroupID", "AgentID"}, IndexType.Primary),
                IndexDef(new string[1] {"AgentID"}, IndexType.Index)
            ));

            AddSchema("group_notice", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("NoticeID", ColumnTypes.UUID),
                ColDef("Timestamp", ColumnTypes.String50),
                ColDef("FromName", ColumnTypes.String255),
                ColDef("Subject", ColumnTypes.String255),
                ColDef("Message", ColumnTypes.Text),
                ColDef("HasAttachment", ColumnTypes.String50),
                ColDef("ItemID", ColumnTypes.UUID),
                ColDef("AssetType", ColumnTypes.String50),
                ColDef("ItemName", ColumnTypes.String50)
            ), IndexDefs(
                IndexDef(new string[3] {"GroupID", "NoticeID", "Timestamp"},
                    IndexType.Primary)
            ));

            AddSchema("group_role_membership", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("RoleID", ColumnTypes.UUID),
                ColDef("AgentID", ColumnTypes.UUID)
            ), IndexDefs(
                IndexDef(new string[3] {"GroupID", "RoleID", "AgentID"},
                    IndexType.Primary),
                IndexDef(new string[2] {"AgentID", "GroupID"}, IndexType.Index)
            ));

            AddSchema("group_roles", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("RoleID", ColumnTypes.UUID),
                ColDef("Name", ColumnTypes.String255),
                ColDef("Description", ColumnTypes.String255),
                ColDef("Title", ColumnTypes.String255),
                ColDef("Powers", ColumnTypes.String50)
            ), IndexDefs(
                IndexDef(new string[2] {"GroupID", "RoleID"}, IndexType.Primary),
                IndexDef(new string[1] {"RoleID"}, IndexType.Index)
            ));

            // TODO:  Group proposals & votes are saved as generic data currently.
            //        Should these be dropped or the appropriate functions re-worked to use these tables??
            AddSchema("group_proposals", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("Duration", ColumnTypes.Integer11),
                ColDef("Majority", ColumnTypes.Float),
                ColDef("Text", ColumnTypes.Text),
                ColDef("Quorum", ColumnTypes.Integer11),
                ColDef("Session", ColumnTypes.UUID),
                ColDef("BallotInitiator", ColumnTypes.UUID),
                ColDef("Created", ColumnTypes.DateTime),
                ColDef("Ending", ColumnTypes.DateTime),
                ColDef("VoteID", ColumnTypes.UUID)));

            AddSchema("group_proposals_votes", ColDefs(
                ColDef("ProposalID", ColumnTypes.UUID),
                ColDef("UserID", ColumnTypes.UUID),
                ColDef("Vote", ColumnTypes.String10)));

            AddSchema("group_bans", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("AgentID", ColumnTypes.UUID),
                ColDef("BanTime", ColumnTypes.DateTime)));
        }

        protected override void DoCreateDefaults(IDataConnector genericData)
        {
            EnsureAllTablesInSchemaExist(genericData);
        }

        protected override bool DoValidate(IDataConnector genericData)
        {
            return TestThatAllTablesValidate(genericData);
        }

        protected override void DoMigrate(IDataConnector genericData)
        {
            DoCreateDefaults(genericData);
        }

        protected override void DoPrepareRestorePoint(IDataConnector genericData)
        {
            CopyAllTablesToTempVersions(genericData);
        }
    }
}
