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

namespace WhiteCore.DataManager.Migration.Migrators.Agent
{
    public class AgentMigrator_7 : Migrator
    {
        public AgentMigrator_7()
        {
            Version = new Version(0, 0, 7);
            MigrationName = "Agent";

            Schema = new List<SchemaDefinition>();

            //
            // Change summary:  April 1 2016
            //
            //   Change ID type fields to type UUID
            //

            AddSchema("user_profile", ColDefs(
                ColDef("ID", ColumnTypes.UUID),
                ColDef("Key", ColumnTypes.String50),
                ColDef("Value", ColumnTypes.Text)
            ), IndexDefs(
                IndexDef(new string[2] { "ID", "Key" }, IndexType.Primary)
            ));

            AddSchema("user_classifieds", ColDefs(
                ColDef("Name", ColumnTypes.String50),
                ColDef("Category", ColumnTypes.String50),
                ColDef("SimName", ColumnTypes.String50),
                ColDef("OwnerUUID", ColumnTypes.UUID),
                new ColumnDefinition
            {
                Name = "ScopeID",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.UUID,
                    defaultValue = OpenMetaverse.UUID.Zero.ToString()
                }
            },
                ColDef("ClassifiedUUID", ColumnTypes.UUID),
                ColDef("Classified", ColumnTypes.String8196),
                new ColumnDefinition
            {
                Name = "Price",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.Integer,
                    Size = 11,
                    defaultValue = "0"
                }
            },
                new ColumnDefinition
            {
                Name = "Keyword",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.String,
                    Size = 512,
                    defaultValue = ""
                }
            }
            ), IndexDefs(
                IndexDef(new string[1] { "ClassifiedUUID" }, IndexType.Primary),
                IndexDef(new string[2] { "Name", "Category" }, IndexType.Index),
                IndexDef(new string[1] { "OwnerUUID" }, IndexType.Index),
                IndexDef(new string[1] { "Keyword" }, IndexType.Index, 255)
            ));

            AddSchema("user_picks", ColDefs(
                ColDef("Name", ColumnTypes.String50),
                ColDef("SimName", ColumnTypes.String50),
                ColDef("OwnerUUID", ColumnTypes.UUID),
                ColDef("PickUUID", ColumnTypes.UUID),
                ColDef("Pick", ColumnTypes.String8196)
            ), IndexDefs(
                IndexDef(new string[1] { "PickUUID" }, IndexType.Primary),
                IndexDef(new string[1] { "OwnerUUID" }, IndexType.Index)
            ));

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
