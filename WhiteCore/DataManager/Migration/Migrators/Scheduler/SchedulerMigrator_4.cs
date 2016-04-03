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

namespace WhiteCore.DataManager.Migration.Migrators.Scheduler
{
    public class SchedulerMigrator_4 : Migrator
    {
        public SchedulerMigrator_4()
        {
            Version = new Version(0, 0, 4);
            MigrationName = "Scheduler";

            Schema = new List<SchemaDefinition>();

            //
            // Change summary: April 1 2016
            //
            //   Change ID type fields to type UUID
            //

            AddSchema("scheduler", ColDefs(
                ColDef("id", ColumnTypes.UUID),
                ColDef("fire_function", ColumnTypes.String128),
                ColDef("fire_params", ColumnTypes.String1024),
                ColDef("run_once", ColumnTypes.TinyInt1),
                ColDef("run_every", ColumnTypes.Integer30),
                ColDef("runs_next", ColumnTypes.DateTime),
                ColDef("keep_history", ColumnTypes.TinyInt1),
                ColDef("require_reciept", ColumnTypes.TinyInt1),
                ColDef("last_history_id", ColumnTypes.UUID),
                ColDef("create_time", ColumnTypes.DateTime),
                ColDef("start_time", ColumnTypes.DateTime),
                ColDef("run_every_type", ColumnTypes.Integer30),
                ColDef("enabled", ColumnTypes.TinyInt1),
                new ColumnDefinition
            {
                Name = "schedule_for",
                Type = new ColumnTypeDef
                {
                    Type = ColumnType.String,
                    Size = 36,
                    defaultValue = OpenMetaverse.UUID.Zero.ToString()
                }
            }
            ), IndexDefs(
                IndexDef(new[] {"id"}, IndexType.Primary),
                IndexDef(new[] {"runs_next", "enabled"}, IndexType.Index),
                IndexDef(new[] {"schedule_for", "fire_function"}, IndexType.Index)
            ));

            AddSchema("scheduler_history", ColDefs(
                ColDef("id", ColumnTypes.UUID),
                ColDef("scheduler_id", ColumnTypes.UUID),
                ColDef("ran_time", ColumnTypes.DateTime),
                ColDef("run_time", ColumnTypes.DateTime),
                ColDef("reciept", ColumnTypes.String1024),
                ColDef("is_complete", ColumnTypes.TinyInt1),
                ColDef("complete_time", ColumnTypes.DateTime)
            ), IndexDefs(
                IndexDef(new string[2] {"id", "scheduler_id"}, IndexType.Primary)
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
