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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Modules;

namespace WhiteCore.Framework.Utilities
{
    public enum RepeatType
    {
        second = 1,
        minute = 2,
        hours = 3,
        days = 4,
        weeks = 5,
        months = 6,
        years = 7
    }

    public class SchedulerItem : IDataTransferable
    {
        public SchedulerItem()
        {
            SimpleInitialize();
        }

        /// <summary>
        /// Initializes a new instance of a SchedulerItem. 
        /// </summary>
        /// <param name="sName">Schedule name.</param>
        /// <param name="sParams">parameters.</param>
        /// <param name="runOnce">If set to <c>true</c> run once.</param>
        /// <param name="runSchedule">DateTime (utc) to run the schedule.</param>
        /// <param name="agentID">AgentID of the schedule.</param>
        public SchedulerItem(string sName, string sParams, bool runOnce, DateTime runSchedule, UUID agentID)
        {
            SimpleInitialize();
            FireFunction = sName;
            FireParams = sParams;
            RunOnce = runOnce;
            //RunEvery = runEvery;
            //RunEveryType = runEveryType;
            StartTime = DateTime.Now;                   // was UtcNow; but this is all relative to the server time not UTC
            TimeToRun = runSchedule;                    // dateTime to run this schedule
            CreateTime = DateTime.Now;                  // was UtcNow;
            ScheduleFor = agentID;
            Enabled = true;
        }


        void SimpleInitialize()
        {
            id = UUID.Random().ToString();
            RunOnce = true;
            RunEvery = 0;
            HistoryReceipt = false;
            FireFunction = string.Empty;
            FireParams = string.Empty;
            HistoryKeep = false;
            HistoryLastID = "";
            Enabled = false;
            ScheduleFor = UUID.Zero;
        }

        public string id { get; set; }

        public string HistoryLastID { get; set; }

        public DateTime TimeToRun { get; set; }

        public bool Enabled { get; set; }

        public bool HistoryKeep { get; set; }

        public string FireParams { get; set; }

        public string FireFunction { get; set; }

        public bool HistoryReceipt { get; set; }

        public bool RunOnce { get; set; }

        public int RunEvery { get; set; }

        public DateTime CreateTime { get; set; }

        public RepeatType RunEveryType { get; set; }

        public DateTime StartTime { get; set; }

        public UUID ScheduleFor { get; set; }

        public override OSDMap ToOSD()
        {
            OSDMap returnvalue = new OSDMap()
                                     {
                                         {"id", id},
                                         {"HistoryLastID", HistoryLastID},
                                         {"TimeToRun", TimeToRun},
                                         {"Enabled", Enabled},
                                         {"HistoryKeep", HistoryKeep},
                                         {"FireParams", FireParams},
                                         {"FireFunction", FireFunction},
                                         {"HistoryReceipt", HistoryReceipt},
                                         {"RunOnce", RunOnce},
                                         {"RunEvery", RunEvery},
                                         {"CreateTime", CreateTime},
                                         {"RunEveryType", (int) RunEveryType},
                                         {"StartTime", StartTime},
                                         {"ScheduleFor", ScheduleFor}
                                     };

            return returnvalue;
        }

        public override void FromOSD(OSDMap map)
        {
            id = map["id"].AsString();
            HistoryLastID = map["HistoryLastID"].AsString();
            TimeToRun = map["TimeToRun"].AsDate();
            Enabled = map["Enabled"].AsBoolean();
            HistoryKeep = map["HistoryKeep"].AsBoolean();
            FireParams = map["FireParams"].AsString();
            FireFunction = map["FireFunction"].AsString();
            HistoryReceipt = map["HistoryReceipt"].AsBoolean();
            RunOnce = map["RunOnce"].AsBoolean();
            RunEvery = map["RunEvery"].AsInteger();
            CreateTime = map["CreateTime"].AsDate();
            RunEveryType = (RepeatType) map["RunEveryType"].AsInteger();
            StartTime = map["StartTime"].AsDate();
            ScheduleFor = map["ScheduleFor"].AsUUID();
        }
    }

    public class SchedulerHistory
    {
        public SchedulerHistory()
        {
            id = 0;
            SchedulerItemID = 0;
            RanTime = 0;
            CompleteTime = 0;
            isComplete = false;
            Reciept = "";
            ErrorLog = "";
        }

        protected bool isComplete { get; set; }

        protected string ErrorLog { get; set; }

        protected string Reciept { get; set; }

        protected int CompleteTime { get; set; }

        protected int RanTime { get; set; }

        protected int SchedulerItemID { get; set; }

        protected int id { get; set; }
    }
}