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
using WhiteCore.Framework.Utilities;

namespace WhiteCore.DataManager.Migration.Migrators.Inventory
{
    public class InventoryMigrator_5 : Migrator
    {
        public InventoryMigrator_5()
        {
            Version = new Version(0, 0, 5);
            MigrationName = "Inventory";

            Schema = new List<SchemaDefinition>();

            //
            // Change summary: April 1 2016
            //
            //   Change ID type fields to type UUID
            //

            AddSchema("inventory_folders", ColDefs(
                ColDef("folderID", ColumnTypes.UUID),
                ColDef("agentID", ColumnTypes.UUID),
                ColDef("parentFolderID", ColumnTypes.UUID),
                ColDef("folderName", ColumnTypes.String128),
                ColDef("type", ColumnTypes.Integer11),
                ColDef("version", ColumnTypes.Integer11)
            ), IndexDefs(
                IndexDef(new string[3] { "folderID", "agentID", "parentFolderID" },
                    IndexType.Primary)
            ));


            AddSchema("inventory_items", ColDefs(
                ColDef("assetID", ColumnTypes.UUID),
                ColDef("assetType", ColumnTypes.Integer11),
                ColDef("inventoryName", ColumnTypes.String128),
                ColDef("inventoryDescription", ColumnTypes.String128),
                ColDef("inventoryNextPermissions", ColumnTypes.Integer11),
                ColDef("inventoryCurrentPermissions", ColumnTypes.Integer11),
                ColDef("invType", ColumnTypes.Integer11),
                ColDef("creatorID", ColumnTypes.UUID),
                ColDef("inventoryBasePermissions", ColumnTypes.Integer11),
                ColDef("inventoryEveryOnePermissions", ColumnTypes.Integer11),
                ColDef("salePrice", ColumnTypes.Integer11),
                ColDef("saleType", ColumnTypes.Integer11),
                ColDef("creationDate", ColumnTypes.Integer11),
                ColDef("groupID", ColumnTypes.UUID),
                ColDef("groupOwned", ColumnTypes.Integer11),            // this probably should be a bool/int1
                ColDef("flags", ColumnTypes.Integer11),
                ColDef("inventoryID", ColumnTypes.UUID),
                ColDef("avatarID", ColumnTypes.UUID),
                ColDef("parentFolderID", ColumnTypes.UUID),
                ColDef("inventoryGroupPermissions", ColumnTypes.Integer11)
            ), IndexDefs(
                IndexDef(
                    new string[5]
                {
                    "assetType", "flags", "inventoryID", "avatarID",
                    "parentFolderID"
                }, IndexType.Primary),
                IndexDef(new string[2] { "parentFolderID", "avatarID" }, IndexType.Index),
                IndexDef(new string[2] { "avatarID", "assetType" }, IndexType.Index),
                IndexDef(new string[1] { "inventoryID" }, IndexType.Index),
                IndexDef(new string[2] { "assetID", "avatarID" }, IndexType.Index)
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
