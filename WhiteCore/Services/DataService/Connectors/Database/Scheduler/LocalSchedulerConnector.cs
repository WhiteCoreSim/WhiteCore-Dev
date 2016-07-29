﻿/*
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
using System.Data;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.DataService.Connectors.Database.Scheduler
{
    public class LocalSchedulerConnector : ISchedulerDataPlugin
    {
        IGenericData GD;

        readonly string[] theFields = new[]
        {
            "id", "fire_function", "fire_params", "run_once", "run_every",
            "runs_next", "keep_history", "require_reciept", "last_history_id",
            "create_time", "start_time", "run_every_type", "enabled",
            "schedule_for"
        };

        #region Implementation of IWhiteCoreDataPlugin

        /// <summary>
        ///     Returns the plugin name
        /// </summary>
        /// <returns></returns>
        public string Name
        {
            get { return "ISchedulerDataPlugin"; }
        }

        /// <summary>
        ///     Starts the database plugin, performs migrations if needed
        /// </summary>
        /// <param name="genericData">The Database Plugin</param>
        /// <param name="source">Config if more parameters are needed</param>
        /// <param name="simBase"></param>
        /// <param name="defaultConnectionString">The connection string to use</param>
        public void Initialize(IGenericData genericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            if (source.Configs["WhiteCoreConnectors"].GetString("SchedulerConnector", "LocalConnector") != "LocalConnector")
                return;

            if (source.Configs[Name] != null)
                defaultConnectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);
            if (genericData != null)
                genericData.ConnectToDatabase(defaultConnectionString, "Scheduler",
                                              source.Configs["WhiteCoreConnectors"].GetBoolean("ValidateTables", true));

            GD = genericData;
            Framework.Utilities.DataManager.RegisterPlugin(this);
        }

        #endregion

        #region Implementation of ISchedulerDataPlugin

        public string SchedulerSave(SchedulerItem itm)
        {
            object[] dbv = GetDBValues(itm);
            Dictionary<string, object> values = new Dictionary<string, object>(dbv.Length);
            int i = 0;
            foreach (object value in dbv) {
                values[theFields[i++]] = value;
            }
            if (SchedulerExist(itm.id)) {
                QueryFilter filter = new QueryFilter();
                filter.andFilters["id"] = itm.id;

                GD.Update("scheduler", values, null, filter, null, null);
            } else {
                GD.Insert("scheduler", values);
            }

            return itm.id;
        }

        public void SchedulerRemoveID(string id)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["id"] = id;
            GD.Delete("scheduler", filter);
        }

        public void SchedulerRemoveFunction(string identifier)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["fire_function"] = identifier;
            GD.Delete("scheduler", filter);
        }

        object[] GetDBValues(SchedulerItem itm)
        {
            return new object[]
                       {
                           itm.id,
                           itm.FireFunction,
                           itm.FireParams,
                           itm.RunOnce,
                           itm.RunEvery,
                           itm.TimeToRun,         // "run_next" field in db
                           itm.HistoryKeep,
                           itm.HistoryReceipt,
                           itm.HistoryLastID,
                           itm.CreateTime,
                           itm.StartTime,
                           (int) itm.RunEveryType,
                           itm.Enabled,
                           itm.ScheduleFor
                       };
        }

        public bool SchedulerExist(string id)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["id"] = id;

            return GD.Query(new string[] {"id"}, "scheduler", filter, null, null, null).Count >= 1;
        }


        public List<SchedulerItem> ToRun(DateTime timeToRun)
        {
            List<SchedulerItem> returnValue = new List<SchedulerItem>();
            DataReaderConnection dr = null;
            try {
                dr = GD.QueryData( 
                        // "WHERE enabled = 1 AND runs_next < '" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") +   // use local time for scheduling
                        "WHERE enabled = 1 AND runs_next <='" + timeToRun.ToString("yyyy-MM-dd HH:mm") +
                        "' ORDER BY runs_next desc", "scheduler", string.Join(", ", theFields));
                
                if (dr != null && dr.DataReader != null) {
                    while (dr.DataReader.Read()) {
                        returnValue.Add(LoadFromDataReader(dr.DataReader));
                    }
                }
            } catch{
            } finally {
                if (dr != null)
                    GD.CloseDatabase(dr);
            }

            return returnValue;
        }

        public SchedulerItem SaveHistory(SchedulerItem itm)
        {
            string his_id = UUID.Random().ToString();

            Dictionary<string, object> row = new Dictionary<string, object>(7);
            row["id"] = his_id;
            row["scheduler_id"] = itm.id;
            row["ran_time"] = DateTime.UtcNow;
            row["run_time"] = itm.TimeToRun;
            row["is_complete"] = 0;
            row["complete_time"] = DateTime.UtcNow;
            row["reciept"] = "";
            GD.Insert("scheduler_history", row);

            itm.HistoryLastID = his_id;
            return itm;
        }

        public SchedulerItem SaveHistoryComplete(SchedulerItem itm)
        {
            Dictionary<string, object> values = new Dictionary<string, object>(3);
            values["is_complete"] = true;
            values["complete_time"] = DateTime.UtcNow;
            values["reciept"] = "";

            QueryFilter filter = new QueryFilter();
            filter.andFilters["id"] = itm.HistoryLastID;

            GD.Update("scheduler_history", values, null, filter, null, null);

            return itm;
        }

        public void SaveHistoryCompleteReciept(string historyID, string reciept)
        {
            Dictionary<string, object> values = new Dictionary<string, object>(3);
            values["is_complete"] = 1;
            values["complete_time"] = DateTime.UtcNow;
            values["reciept"] = reciept;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["id"] = historyID;

            GD.Update("scheduler_history", values, null, filter, null, null);
        }

        public void HistoryDeleteOld(SchedulerItem itm)
        {
            if ((itm.id != "") && (itm.HistoryLastID != "")) {
                QueryFilter filter = new QueryFilter();
                filter.andNotFilters["id"] = itm.HistoryLastID;
                filter.andFilters["scheduler_id"] = itm.id;
                GD.Delete("scheduler_history", filter);
            }
        }

        public SchedulerItem Get(string id)
        {
            if (id != "") {
                QueryFilter filter = new QueryFilter();
                filter.andFilters["id"] = id;
                List<string> results = GD.Query(theFields, "scheduler", filter, null, null, null);
                return LoadFromList(results);
            }

            return null;
        }

        public SchedulerItem Get(string scheduleFor, string fireFunction)
        {
            if (scheduleFor != UUID.Zero.ToString()) {
                QueryFilter filter = new QueryFilter();
                filter.andFilters["schedule_for"] = scheduleFor;
                filter.andFilters["fire_function"] = fireFunction;
                List<string> results = GD.Query(theFields, "scheduler", filter, null, null, null);

                return LoadFromList(results);
            }

            return null;
        }

        public SchedulerItem GetFunctionItem(string fireFunction)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["fire_function"] = fireFunction;
            List<string> results = GD.Query(theFields, "scheduler", filter, null, null, null);

            if (results == null || results.Count == 0)
                return null;
            
            return LoadFromList(results);
        }

        SchedulerItem LoadFromDataReader(IDataReader dr)
        {
            return new SchedulerItem
                       {
                           id = dr["id"].ToString(),
                           FireFunction = dr["fire_function"].ToString(),
                           FireParams = dr["fire_params"].ToString(),
                           HistoryKeep = bool.Parse(dr["keep_history"].ToString()),
                           Enabled = bool.Parse(dr["enabled"].ToString()),
                           CreateTime = DateTime.Parse(dr["create_time"].ToString()),
                           HistoryLastID = dr["last_history_id"].ToString(),
                           TimeToRun = DateTime.Parse(dr["runs_next"].ToString()),
                           HistoryReceipt = bool.Parse(dr["require_reciept"].ToString()),
                           RunEvery = int.Parse(dr["run_every"].ToString()),
                           RunOnce = bool.Parse(dr["run_once"].ToString()),
                           RunEveryType = (RepeatType) int.Parse(dr["run_every_type"].ToString()),
                           StartTime = DateTime.Parse(dr["start_time"].ToString()),
                           ScheduleFor = UUID.Parse(dr["schedule_for"].ToString())
                       };
        }

        SchedulerItem LoadFromList(List<string> values)
        {
            if (values == null) return null;
            if (values.Count == 0) return null;

            return new SchedulerItem
                       {
                           id = values[0],
                           FireFunction = values[1],
                           FireParams = values[2],
                           RunOnce = bool.Parse(values[3]),
                           RunEvery = int.Parse(values[4]),
                           TimeToRun = DateTime.Parse(values[5]),
                           HistoryKeep = bool.Parse(values[6]),
                           HistoryReceipt = bool.Parse(values[7]),
                           HistoryLastID = values[8],
                           CreateTime = DateTime.Parse(values[9]),
                           StartTime = DateTime.Parse(values[10]),
                           RunEveryType = (RepeatType) int.Parse(values[11]),
                           Enabled = bool.Parse(values[12]),
                           ScheduleFor = UUID.Parse(values[13])
                       };
        }

        #endregion
    }
}
