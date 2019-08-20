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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.DataService
{
    public class LocalUserInfoConnector : IAgentInfoConnector
    {
        IGenericData GD;
        protected bool m_allowDuplicatePresences = true;
        protected bool m_checkLastSeen = true;
        string m_userInfoTable = "user_info";

        #region IAgentInfoConnector Members

        public void Initialize (IGenericData GenericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            if (source.Configs ["WhiteCoreConnectors"].GetString ("UserInfoConnector", "LocalConnector") == "LocalConnector") {
                GD = GenericData;

                string connectionString = defaultConnectionString;
                if (source.Configs [Name] != null) {
                    connectionString = source.Configs [Name].GetString ("ConnectionString", defaultConnectionString);

                    m_allowDuplicatePresences =
                        source.Configs [Name].GetBoolean ("AllowDuplicatePresences", m_allowDuplicatePresences);
                    m_checkLastSeen =
                        source.Configs [Name].GetBoolean ("CheckLastSeen", m_checkLastSeen);
                }
                if (GD != null)
                    GD.ConnectToDatabase (connectionString, "UserInfo",
                                         source.Configs ["WhiteCoreConnectors"].GetBoolean ("ValidateTables", true));

                Framework.Utilities.DataManager.RegisterPlugin (this);
            }
        }

        public string Name {
            get { return "IAgentInfoConnector"; }
        }

        public bool Set (UserInfo info)
        {
            object [] values = new object [14];
            values [0] = info.UserID;
            values [1] = info.CurrentRegionID;
            values [2] = Util.ToUnixTime (DateTime.Now.ToUniversalTime ());
            //Convert to binary so that it can be converted easily
            values [3] = info.IsOnline ? 1 : 0;
            values [4] = Util.ToUnixTime (info.LastLogin);
            values [5] = Util.ToUnixTime (info.LastLogout);
            values [6] = OSDParser.SerializeJsonString (info.Info);
            values [7] = info.CurrentRegionID.ToString ();
            values [8] = info.CurrentPosition.ToString ();
            values [9] = info.CurrentLookAt.ToString ();
            values [10] = info.HomeRegionID.ToString ();
            values [11] = info.HomePosition.ToString ();
            values [12] = info.HomeLookAt.ToString ();
            values [13] = info.CurrentRegionURI;

            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["UserID"] = info.UserID;
            GD.Delete (m_userInfoTable, filter);

            return GD.Insert (m_userInfoTable, values);
        }

        public void Update (string userID, Dictionary<string, object> values)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["UserID"] = userID;

            GD.Update (m_userInfoTable, values, null, filter, null, null);
        }

        public void SetLastPosition (string userID, UUID regionID, string regionURI, Vector3 lastPosition,
                                    Vector3 lastLookAt)
        {
            Dictionary<string, object> values = new Dictionary<string, object> (5);
            values ["CurrentRegionID"] = regionID;
            values ["CurrentRegionURI"] = regionURI;
            values ["CurrentPosition"] = lastPosition;
            values ["CurrentLookat"] = lastLookAt;
            values ["LastSeen"] = Util.ToUnixTime (DateTime.Now.ToUniversalTime ());

            Update (userID, values);
        }

        public void SetHomePosition (string userID, UUID regionID, Vector3 Position, Vector3 lookAt)
        {
            Dictionary<string, object> values = new Dictionary<string, object> (4);
            values ["HomeRegionID"] = regionID;
            values ["LastSeen"] = Util.ToUnixTime (DateTime.Now.ToUniversalTime ());
            values ["HomePosition"] = Position;
            values ["HomeLookat"] = lookAt;

            Update (userID, values);
        }

        static List<UserInfo> ParseQuery (List<string> query)
        {
            List<UserInfo> users = new List<UserInfo> ();

            if (query.Count % 14 == 0) {
                for (int i = 0; i < query.Count; i += 14) {
                    UserInfo user = new UserInfo {
                        UserID = query [i],
                        CurrentRegionID = UUID.Parse (query [i + 1]),
                        IsOnline = query [i + 3] == "1",
                        LastLogin = Util.ToDateTime (int.Parse (query [i + 4])),
                        LastLogout = Util.ToDateTime (int.Parse (query [i + 5])),
                        Info = (OSDMap)OSDParser.DeserializeJson (query [i + 6])
                    };
                    try {
                        user.CurrentRegionID = UUID.Parse (query [i + 7]);
                        if (query [i + 8] != "")
                            user.CurrentPosition = Vector3.Parse (query [i + 8]);
                        if (query [i + 9] != "")
                            user.CurrentLookAt = Vector3.Parse (query [i + 9]);
                        user.HomeRegionID = UUID.Parse (query [i + 10]);
                        if (query [i + 11] != "")
                            user.HomePosition = Vector3.Parse (query [i + 11]);
                        if (query [i + 12] != "")
                            user.HomeLookAt = Vector3.Parse (query [i + 12]);
                        user.CurrentRegionURI = query [i + 13];
                    } catch {
                    }

                    users.Add (user);
                }
            }

            return users;
        }

        public List<UserInfo> GetByCurrentRegion (string regionID)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["CurrentRegionID"] = regionID;
            filter.andFilters ["IsOnline"] = "1";
            List<string> query = GD.Query (new string [1] { "*" }, m_userInfoTable, filter, null, null, null);

            if (query.Count == 0)
                return new List<UserInfo> ();

            return ParseQuery (query);
        }

        public UserInfo Get (string userID, bool checkOnlineStatus, out bool onlineStatusChanged)
        {
            onlineStatusChanged = false;

            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["UserID"] = userID;
            List<string> query = GD.Query (new string [1] { "*" }, m_userInfoTable, filter, null, null, null);

            if (query.Count == 0) {
                return null;
            }
            UserInfo user = ParseQuery (query) [0];

            // Check LastSeen
            DateTime timeLastSeen = Util.ToDateTime (int.Parse (query [2]));
            DateTime timeNow = DateTime.Now.ToUniversalTime ();
            if (checkOnlineStatus && m_checkLastSeen && user.IsOnline && (timeLastSeen.AddHours (1) < timeNow)) {
                MainConsole.Instance.Warn ("[UserInfoService]: Found a user (" + user.UserID +
                                          ") that was not seen within the last hour " +
                                          "(since " + timeLastSeen.ToLocalTime ().ToString () + ", time elapsed " +
                                          (timeNow - timeLastSeen).Days + " days, " + (timeNow - timeLastSeen).Hours +
                                          " hours)! Logging them out.");
                user.IsOnline = false;
                Set (user);
                onlineStatusChanged = true;
            }
            return user;
        }

        public uint RecentlyOnline (uint secondsAgo, bool stillOnline)
        {
            // Beware!! login times are UTC!
            int now = Util.ToUnixTime (DateTime.Now.ToUniversalTime ()) - (int)secondsAgo;

            QueryFilter filter = new QueryFilter ();
            filter.orGreaterThanEqFilters ["LastLogin"] = now;
            filter.orGreaterThanEqFilters ["LastSeen"] = now;
            if (stillOnline) {
                // filter.andGreaterThanFilters["LastLogout"] = now;
                filter.andFilters ["IsOnline"] = "1";
            }

            List<string> userCount = GD.Query (new string [1] { "COUNT(UserID)" }, m_userInfoTable, filter, null, null, null);
            return uint.Parse (userCount [0]);
        }

        public uint OnlineUsers (uint secondsAgo)
        {
            QueryFilter filter = new QueryFilter ();
            if (secondsAgo > 0) {
                // Beware!! login times are UTC!
                int now = Util.ToUnixTime (DateTime.Now.ToUniversalTime ()) - (int)secondsAgo;

                filter.orGreaterThanEqFilters ["LastLogin"] = now;
                filter.orGreaterThanEqFilters ["LastSeen"] = now;
                // filter.andGreaterThanFilters["LastLogout"] = now;
            }
            filter.andFilters ["IsOnline"] = "1";

            List<string> userCount = GD.Query (new string [1] { "COUNT(UserID)" }, m_userInfoTable, filter, null, null, null);

            return uint.Parse (userCount [0]);
        }

        public List<UserInfo> RecentlyOnline (uint secondsAgo, bool stillOnline, Dictionary<string, bool> sort)
        {
            // Beware!! login times are UTC!
            int now = Util.ToUnixTime (DateTime.Now.ToUniversalTime ()) - (int)secondsAgo;

            QueryFilter filter = new QueryFilter ();
            filter.orGreaterThanEqFilters ["LastLogin"] = now;
            filter.orGreaterThanEqFilters ["LastSeen"] = now;
            if (stillOnline) {
                // filter.andGreaterThanFilters["LastLogout"] = now;
                filter.andFilters ["IsOnline"] = "1";
            }

            List<string> query = GD.Query (new string [] { "*" }, m_userInfoTable, filter, sort, null, null);

            return ParseQuery (query);
        }

        public List<UserInfo> CurrentlyOnline (uint secondsAgo, Dictionary<string, bool> sort)
        {

            QueryFilter filter = new QueryFilter ();
            if (secondsAgo > 0) {
                // Beware!! login times are UTC!
                int now = Util.ToUnixTime (DateTime.Now.ToUniversalTime ());
                now -= (int)secondsAgo;

                filter.orGreaterThanEqFilters ["LastLogin"] = now;
                filter.orGreaterThanEqFilters ["LastSeen"] = now;
            }

            // online only please...
            filter.andFilters ["IsOnline"] = "1";

            List<string> query = GD.Query (new string [] { "*" }, m_userInfoTable, filter, sort, null, null);

            return ParseQuery (query);
        }

        #endregion

        public void Dispose ()
        {
        }
    }
}
