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

namespace WhiteCore.DataManager.Migration.Migrators.Directory
{
    public class DirectoryMigrator_13 : Migrator
    {
        public DirectoryMigrator_13()
        {
            Version = new Version(0, 0, 13);
            MigrationName = "Directory";

            Schema = new List<SchemaDefinition>();

            //
            // Change summary:  April 1 2016
            //
            //   Change ID type fields to type UUID
            //

            AddSchema("search_parcel", ColDefs(
                ColDef("RegionID", ColumnTypes.UUID),
                ColDef("ParcelID", ColumnTypes.UUID),
                ColDef("LocalID", ColumnTypes.String50),
                ColDef("LandingX", ColumnTypes.String50),
                ColDef("LandingY", ColumnTypes.String50),
                ColDef("LandingZ", ColumnTypes.String50),
                ColDef("Name", ColumnTypes.String50),
                ColDef("Description", ColumnTypes.String255),
                ColDef("Flags", ColumnTypes.String50),
                ColDef("Dwell", ColumnTypes.String50),
                ColDef("InfoUUID", ColumnTypes.UUID),
                ColDef("ForSale", ColumnTypes.String50),
                ColDef("SalePrice", ColumnTypes.String50),
                ColDef("Auction", ColumnTypes.String50),
                ColDef("Area", ColumnTypes.String50),
                ColDef("EstateID", ColumnTypes.String50),
                ColDef("Maturity", ColumnTypes.String50),
                ColDef("OwnerID", ColumnTypes.UUID),
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("ShowInSearch", ColumnTypes.String50),
                ColDef("SnapshotID", ColumnTypes.UUID),
                ColDef("Bitmap", ColumnTypes.LongText),
                ColDef("Category", ColumnTypes.String50),
                new ColumnDefinition
            {
                Name = "ScopeID",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.UUID,
                    defaultValue = OpenMetaverse.UUID.Zero.ToString()
                }
            }
            ), IndexDefs(
                IndexDef(new string[1] { "ParcelID" }, IndexType.Primary),
                IndexDef(new string[4] { "RegionID", "OwnerID", "Flags", "Category" },
                    IndexType.Index),
                IndexDef(new string[2] { "RegionID", "Name" }, IndexType.Index),
                IndexDef(new string[1] { "OwnerID" }, IndexType.Index),
                IndexDef(new string[3] { "ForSale", "SalePrice", "Area" }, IndexType.Index)
            ));


            AddSchema("event_information", ColDefs(
                ColDef("EID", ColumnTypes.Integer11),
                new ColumnDefinition
            {
                Name = "creator",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.UUID
                }
            },
                new ColumnDefinition
            {
                Name = "region",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.UUID
                }
            },
                new ColumnDefinition
            {
                Name = "parcel",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.UUID
                }
            },
                ColDef("date", ColumnTypes.DateTime),
                ColDef("cover", ColumnTypes.Integer11),
                ColDef("maturity", ColumnTypes.TinyInt4),
                ColDef("flags", ColumnTypes.Integer11),
                ColDef("duration", ColumnTypes.Integer11),
                ColDef("localPosX", ColumnTypes.Float),
                ColDef("localPosY", ColumnTypes.Float),
                ColDef("localPosZ", ColumnTypes.Float),
                ColDef("name", ColumnTypes.String50),
                ColDef("description", ColumnTypes.String255),
                ColDef("category", ColumnTypes.String50),
                new ColumnDefinition
            {
                Name = "scopeID",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.UUID,
                    defaultValue = OpenMetaverse.UUID.Zero.ToString()
                }
            }
            ), IndexDefs(
                IndexDef(new string[1] { "EID" }, IndexType.Primary),
                IndexDef(new string[1] { "name" }, IndexType.Index),
                IndexDef(new string[2] { "date", "flags" }, IndexType.Index),
                IndexDef(new string[2] { "region", "maturity" }, IndexType.Index)
            ));

            AddSchema("event_notifications", ColDefs(
                new ColumnDefinition
            {
                Name = "UserID",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.UUID
                }
            },
                ColDef("EventID", ColumnTypes.Integer11)
            ), IndexDefs(
                IndexDef(new string[1] { "UserID" }, IndexType.Primary)
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
