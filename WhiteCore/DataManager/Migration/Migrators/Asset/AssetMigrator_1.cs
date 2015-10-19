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
using WhiteCore.Framework.Utilities;

namespace WhiteCore.DataManager.Migration.Migrators.Asset
{
    public class AssetMigrator_1 : Migrator
    {
        public AssetMigrator_1()
        {
            Version = new Version(0, 0, 1);
            MigrationName = "Asset";

            schema = new List<SchemaDefinition>();

            AddSchema("lslgenericdata", ColDefs(
                ColDef("Token", ColumnTypes.String50),
                ColDef("KeySetting", ColumnTypes.String50),
                ColDef("ValueSetting", ColumnTypes.String50)
                                            ), IndexDefs(
                                                IndexDef(new string[2] {"Token", "KeySetting"}, IndexType.Primary)
                                                   ));

            renameColumns.Add("UUID", "id");
            renameColumns.Add("Name", "name");
            renameColumns.Add("Description", "description");
            renameColumns.Add("Type", "assetType");
            renameColumns.Add("Local", "local");
            renameColumns.Add("Temporary", "temporary");
            renameColumns.Add("CreatorID", "creatorID");
            renameColumns.Add("Data", "data");

            AddSchema("assets", ColDefs(
                ColDef("id", ColumnTypes.Char36),
                ColDef("name", ColumnTypes.String64),
                ColDef("description", ColumnTypes.String64),
                ColDef("assetType", ColumnTypes.TinyInt4),
                ColDef("local", ColumnTypes.TinyInt1),
                ColDef("temporary", ColumnTypes.TinyInt1),
                ColDef("asset_flags", ColumnTypes.String45),
                ColDef("creatorID", ColumnTypes.String36),
                ColDef("data", ColumnTypes.LongBlob),
                ColDef("create_time", ColumnTypes.Integer11),
                ColDef("access_time", ColumnTypes.Integer11)
                                    ), IndexDefs(
                                        IndexDef(new string[1] {"id"}, IndexType.Primary)
                                           ));

            RemoveSchema("assetblob");
            RemoveSchema("assettext");
            RemoveSchema("assetmesh");
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