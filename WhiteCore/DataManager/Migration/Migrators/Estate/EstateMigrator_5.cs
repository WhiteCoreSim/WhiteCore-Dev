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

namespace WhiteCore.DataManager.Migration.Migrators.Estate
{
    public class EstateMigrator_5 : Migrator
    {
        public EstateMigrator_5()
        {
            Version = new Version(0, 0, 5);
            MigrationName = "Estate";

            Schema = new List<SchemaDefinition>();

            //
            // Change summary:  April 1 2016
            //
            //   Change ID type fields to type UUID
            //

            AddSchema("estate_regions", ColDefs(
                ColDef("RegionID", ColumnTypes.UUID),
                ColDef("EstateID", ColumnTypes.Integer11)
            ), IndexDefs(
                IndexDef(new string[1] {"RegionID"}, IndexType.Primary),
                IndexDef(new string[1] {"EstateID"}, IndexType.Index)
            ));


            AddSchema("estate_settings", ColDefs(
                ColDef("EstateID", ColumnTypes.Integer11),
                ColDef("EstateName", ColumnTypes.String100),
                ColDef("EstateOwner", ColumnTypes.UUID),
                ColDef("ParentEstateID", ColumnTypes.Integer11),
                ColDef("Settings", ColumnTypes.Text)
            ), IndexDefs(
                IndexDef(new string[1] {"EstateID"}, IndexType.Primary),
                IndexDef(new string[1] {"EstateOwner"}, IndexType.Index),
                IndexDef(new string[2] {"EstateName", "EstateOwner"}, IndexType.Index)
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
