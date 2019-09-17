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


using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services.DataService
{
    public class LocalAbuseReportsConnector : IAbuseReportsConnector
    {
        IGenericData genData;
        string m_abuseReportsTable = "abusereports";
        bool m_enabled;

        #region IAbuseReportsConnector Members

        public void Initialize (IGenericData GenericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            genData = GenericData;

            if (source.Configs [Name] != null)
                defaultConnectionString = source.Configs [Name].GetString ("ConnectionString", defaultConnectionString);

            if (genData != null) {
                genData.ConnectToDatabase (defaultConnectionString, "AbuseReports",
                    source.Configs ["WhiteCoreConnectors"].GetBoolean ("ValidateTables", true));


                Framework.Utilities.DataManager.RegisterPlugin (Name + "Local", this);
                if (source.Configs ["WhiteCoreConnectors"].GetString ("AbuseReportsConnector", "LocalConnector") ==
                    "LocalConnector") {
                    m_enabled = true;
                    Framework.Utilities.DataManager.RegisterPlugin (this);
                }
            }
        }

        public string Name {
            get { return "IAbuseReportsConnector"; }
        }

        public bool Enabled ()
        {
            return m_enabled;
        }

        /// <summary>
        /// Gets the number of Abuse reports.
        /// </summary>
        /// <returns>The report count.</returns>
        public int AbuseReportCount ()
        {
            QueryFilter filter = new QueryFilter ();
            var reports = genData.Query (new string [1] { "count(*)" }, m_abuseReportsTable, filter, null, null, null);
            if ((reports == null) || (reports.Count == 0))
                return 0;

            return int.Parse (reports [0]);

        }

        /// <summary>
        ///     Gets the abuse report associated with the number and uses the pass to authenticate.
        /// </summary>
        /// <param name="Number"></param>
        /// <param name="Password"></param>
        /// <returns></returns>
        public AbuseReport GetAbuseReport (int Number, string Password)
        {
            return GetAbuseReport (Number);
        }

        /// <summary>
        ///     Gets the abuse report associated with the number without authentication
        /// </summary>
        /// <param name="Number"></param>
        /// <returns></returns>
        public AbuseReport GetAbuseReport (int Number)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["Number"] = Number;
            List<string> reports = genData.Query (new string [] { "*" }, m_abuseReportsTable, filter, null, null, null);

            return (reports.Count == 0)
                       ? null
                       : new AbuseReport {
                           Category = reports [0],
                           ReporterName = reports [1],
                           ObjectName = reports [2],
                           ObjectUUID = new UUID (reports [3]),
                           AbuserName = reports [4],
                           AbuseLocation = reports [5],
                           AbuseDetails = reports [6],
                           ObjectPosition = reports [7],
                           RegionName = reports [8],
                           ScreenshotID = new UUID (reports [9]),
                           AbuseSummary = reports [10],
                           Number = int.Parse (reports [11]),
                           AssignedTo = reports [12],
                           Active = int.Parse (reports [13]) == 1,
                           Checked = int.Parse (reports [14]) == 1,
                           Notes = reports [15]
                       };
        }

        public List<AbuseReport> GetAbuseReports (int start, int count, bool active)
        {
            List<AbuseReport> rv = new List<AbuseReport> ();
            QueryFilter filter = new QueryFilter ();
            filter.andGreaterThanEqFilters ["CAST(number AS UNSIGNED)"] = start;
            filter.andFilters ["Active"] = active ? 1 : 0;
            List<string> query = genData.Query (new string [1] { "*" }, m_abuseReportsTable, filter, null, null, null);
            if (query.Count % 16 != 0) {
                return rv;
            }
            try {
                for (int i = 0; i < query.Count; i += 16) {
                    AbuseReport report = new AbuseReport {
                        Category = query [i + 0],
                        ReporterName = query [i + 1],
                        ObjectName = query [i + 2],
                        ObjectUUID = new UUID (query [i + 3]),
                        AbuserName = query [i + 4],
                        AbuseLocation = query [i + 5],
                        AbuseDetails = query [i + 6],
                        ObjectPosition = query [i + 7],
                        RegionName = query [i + 8],
                        ScreenshotID = new UUID (query [i + 9]),
                        AbuseSummary = query [i + 10],
                        Number = int.Parse (query [i + 11]),
                        AssignedTo = query [i + 12],
                        Active = int.Parse (query [i + 13]) == 1,
                        Checked = int.Parse (query [i + 14]) == 1,
                        Notes = query [i + 15]
                    };
                    rv.Add (report);
                }
            } catch {
            }
            return rv;
        }

        /// <summary>
        ///     Adds a new abuse report to the database
        /// </summary>
        /// <param name="report"></param>
        public void AddAbuseReport (AbuseReport report)
        {
            List<object> InsertValues = new List<object> {
                report.Category.ToString (),
                report.ReporterName,
                report.ObjectName,
                report.ObjectUUID,
                report.AbuserName,
                report.AbuseLocation,
                report.AbuseDetails,
                report.ObjectPosition,
                report.RegionName,
                report.ScreenshotID,
                report.AbuseSummary
            };

            Dictionary<string, bool> sort = new Dictionary<string, bool> (1);
            sort ["Number"] = false;

            //We do not trust the number sent by the region. Always find it ourselves
            List<string> values = genData.Query (new string [1] { "Number" }, m_abuseReportsTable, null, sort, null, null);
            report.Number = values.Count == 0 ? 0 : int.Parse (values [0]);

            report.Number++;

            InsertValues.Add (report.Number);

            InsertValues.Add (report.AssignedTo);
            InsertValues.Add (report.Active ? 1 : 0);
            InsertValues.Add (report.Checked ? 1 : 0);
            InsertValues.Add (report.Notes);

            genData.Insert (m_abuseReportsTable, InsertValues.ToArray ());
        }

        /// <summary>
        ///     Updates an abuse report and authenticates with the password.
        /// </summary>
        /// <param name="report"></param>
        /// <param name="Password"></param>
        public void UpdateAbuseReport (AbuseReport report, string Password)
        {
            UpdateAbuseReport (report);
        }

        /// <summary>
        ///     Updates an abuse report without authentication
        /// </summary>
        /// <param name="report"></param>
        public void UpdateAbuseReport (AbuseReport report)
        {
            Dictionary<string, object> row = new Dictionary<string, object> (16);
            //This is update, so we trust the number as it should know the number it's updating now.
            row ["Category"] = report.Category.ToString ();
            row ["ReporterName"] = report.ReporterName;
            row ["ObjectName"] = report.ObjectName;
            row ["ObjectUUID"] = report.ObjectUUID;
            row ["AbuserName"] = report.AbuserName;
            row ["AbuseLocation"] = report.AbuseLocation;
            row ["AbuseDetails"] = report.AbuseDetails;
            row ["ObjectPosition"] = report.ObjectPosition;
            row ["RegionName"] = report.RegionName;
            row ["ScreenshotID"] = report.ScreenshotID;
            row ["AbuseSummary"] = report.AbuseSummary;
            row ["Number"] = report.Number;
            row ["AssignedTo"] = report.AssignedTo;
            row ["Active"] = report.Active ? 1 : 0;
            row ["Checked"] = report.Checked ? 1 : 0;
            row ["Notes"] = report.Notes;

            genData.Replace (m_abuseReportsTable, row);
        }

        #endregion

        public void Dispose ()
        {
        }
    }
}
