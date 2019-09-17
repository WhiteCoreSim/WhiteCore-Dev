/*
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
using WhiteCore.Framework.Utilities;

namespace WhiteCore.DataManager.Migration.Migrators.Currency
{
    public class CurrencyMigrator_6 : Migrator
    {
        public CurrencyMigrator_6 ()
        {
            Version = new Version (0, 0, 6);
            MigrationName = "SimpleCurrency";

            Schema = new List<SchemaDefinition> ();

            //
            // Change summary: 19 May 2018
            //   Change 2 columns in the group_currency table to fix
            //   a bug with the group currency not being stored correctly
            // Updated 13 Nov 2018
            //   Change all integer fields to Integer11 rather than the strange Integer30
            // 

            AddSchema ("user_currency", ColDefs (
                ColDef ("PrincipalID", ColumnTypes.UUID),
                ColDef ("Amount", ColumnTypes.Integer11),
                ColDef ("LandInUse", ColumnTypes.Integer11),
                ColDef ("Tier", ColumnTypes.Integer11),
                ColDef ("IsGroup", ColumnTypes.TinyInt1),            // this will be deprecated
                new ColumnDefinition {
                    Name = "StipendsBalance",
                    Type = new ColumnTypeDef {
                        Type = ColumnType.Integer,
                        Size = 11,
                        defaultValue = "0"
                    }
                }
            ),
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
                ColDef("Amount", ColumnTypes.Integer11),
                ColDef("TransType", ColumnTypes.Integer11),
                ColDef("Created", ColumnTypes.Integer11),
                ColDef("ToBalance", ColumnTypes.Integer11),
                ColDef("FromBalance", ColumnTypes.Integer11),
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
                ColDef("Amount", ColumnTypes.Integer11),
                ColDef("RealAmount", ColumnTypes.Integer11),
                ColDef("Created", ColumnTypes.Integer11),
                ColDef("Updated", ColumnTypes.Integer11)),
                IndexDefs(
                    IndexDef(new string[1] { "PurchaseID" }, IndexType.Primary)
                ));

            // Group currency 
            RenameColumns.Add("TierCredits", "TotalTierCredits");
            RenameColumns.Add("TierDebits", "TotalTierDebits");

            AddSchema("group_currency", ColDefs(
                ColDef("GroupID", ColumnTypes.UUID),
                ColDef("Balance", ColumnTypes.Integer11),
                ColDef("GroupFee", ColumnTypes.Integer11),
                ColDef("LandFee", ColumnTypes.Integer11),
                ColDef("ObjectFee", ColumnTypes.Integer11),
                ColDef("ParcelDirectoryFee", ColumnTypes.Integer11),
                ColDef("TotalTierCredits", ColumnTypes.Integer11),   // Changed from TierCredits
                ColDef("TotalTierDebits", ColumnTypes.Integer11)),   // Changed from TierDebits
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
                ColDef("Amount", ColumnTypes.Integer11),
                ColDef("TransType", ColumnTypes.Integer11),
                ColDef("Created", ColumnTypes.Integer11),
                ColDef("GroupBalance", ColumnTypes.Integer11),
                ColDef("AgentBalance", ColumnTypes.Integer11),
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