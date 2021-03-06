﻿/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
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
using WhiteCore.DataManager.Migration;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.DataManager.Migration.Migrators.Currency
{
    public class CurrencyMigrator_5 : Migrator
    {
        public CurrencyMigrator_5()
        {
            Version = new Version(0, 0, 5);
            MigrationName = "SimpleCurrency";

            Schema = new List<SchemaDefinition>();

            //
            // Change summary: 19 May 2018
            //
            //   Change 2 columns in the group_currency table to fix
            //   a bug with the group currency not being stored correctly
            //

            AddSchema("user_currency", ColDefs(
                ColDef("PrincipalID", ColumnTypes.UUID),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("LandInUse", ColumnTypes.Integer30),
                ColDef("Tier", ColumnTypes.Integer30),
                ColDef("IsGroup", ColumnTypes.TinyInt1),            // this will be deprecated
                ColDef("StipendsBalance", ColumnTypes.Integer11)),
                IndexDefs(
                    IndexDef(new string[1] {"PrincipalID"}, IndexType.Primary)
                ));

            // Currency Transaction Logs
            AddSchema("user_currency_history", ColDefs(
                ColDef("TransactionID", ColumnTypes.UUID),
                ColDef("Description", ColumnTypes.String128),
                ColDef("FromPrincipalID", ColumnTypes.UUID),
                ColDef("FromName", ColumnTypes.String128),
                ColDef("ToPrincipalID", ColumnTypes.UUID),
                ColDef("ToName", ColumnTypes.String128),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("TransType", ColumnTypes.Integer11),
                ColDef("Created", ColumnTypes.Integer30),
                ColDef("ToBalance", ColumnTypes.Integer30),
                ColDef("FromBalance", ColumnTypes.Integer30),
                ColDef("FromObjectName", ColumnTypes.String50),
                ColDef("ToObjectName", ColumnTypes.String50),
                ColDef("RegionID", ColumnTypes.UUID)),
                IndexDefs(
                    IndexDef(new string[1] { "TransactionID" }, IndexType.Primary)
                ));

            // user purchases
            AddSchema("user_purchased", ColDefs(
                ColDef("PurchaseID", ColumnTypes.UUID),
                ColDef("PrincipalID", ColumnTypes.UUID),
                ColDef("IP", ColumnTypes.String64),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("RealAmount", ColumnTypes.Integer30),
                ColDef("Created", ColumnTypes.Integer30),
                ColDef("Updated", ColumnTypes.Integer30)),
                IndexDefs(
                    IndexDef(new string[1] { "PurchaseID" }, IndexType.Primary)
                ));

            // Group currency
            AddSchema("group_currency", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("Balance", ColumnTypes.Integer30),
                ColDef("GroupFee", ColumnTypes.Integer30),
                ColDef("LandFee", ColumnTypes.Integer30),
                ColDef("ObjectFee", ColumnTypes.Integer30),
                ColDef("ParcelDirectoryFee", ColumnTypes.Integer30),
                ColDef("TotalTierCredits", ColumnTypes.Integer30),  // Changed from TierCredits
                ColDef("TotalTierDebit", ColumnTypes.Integer30)),   // Changed from TierDebit
                IndexDefs(
                    IndexDef(new string[1] {"GroupID"}, IndexType.Primary)
                ));

            // Currency Transaction Logs
            AddSchema("group_currency_history", ColDefs(
                ColDef("TransactionID", ColumnTypes.UUID),
                ColDef("Description", ColumnTypes.String128),
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("GroupName", ColumnTypes.String128),
                ColDef("AgentID", ColumnTypes.UUID),
                ColDef("AgentName", ColumnTypes.String128),
                ColDef("Amount", ColumnTypes.Integer30),
                ColDef("TransType", ColumnTypes.Integer11),
                ColDef("Created", ColumnTypes.Integer30),
                ColDef("GroupBalance", ColumnTypes.Integer30),
                ColDef("AgentBalance", ColumnTypes.Integer30),
                ColDef("FromObjectName", ColumnTypes.String50),
                ColDef("ToObjectName", ColumnTypes.String50),
                ColDef("RegionID", ColumnTypes.UUID)),
                IndexDefs(
                    IndexDef(new string[1] { "TransactionID" }, IndexType.Primary)
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

        public override void FinishedMigration(IDataConnector genericData)
        {
        }
    }
}