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

using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.DataService
{
    public class LocalEstateConnector : ConnectorBase, IEstateConnector
    {
        IGenericData GD;
        string m_estateSettingsTable = "estate_settings";
        string m_estateRegionsTable = "estate_regions";

        #region IEstateConnector Members
        public bool RemoteCalls()
        {
            return m_doRemoteCalls; 
        }

        public void Initialize(IGenericData GenericData, IConfigSource source, IRegistryCore registry,
                               string defaultConnectionString)
        {
            GD = GenericData;
            m_registry = registry;

            if (source.Configs[Name] != null)
                defaultConnectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);

            if (GD != null)
                GD.ConnectToDatabase(defaultConnectionString, "Estate",
                                     source.Configs["WhiteCoreConnectors"].GetBoolean("ValidateTables", true));

            Framework.Utilities.DataManager.RegisterPlugin(Name + "Local", this);

            if (source.Configs["WhiteCoreConnectors"].GetString("EstateConnector", "LocalConnector") == "LocalConnector")
            {
                Framework.Utilities.DataManager.RegisterPlugin(this);
            }
            Init(registry, Name);
        }

        public string Name
        {
            get { return "IEstateConnector"; }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public EstateSettings GetEstateSettings(UUID regionID)
        {
            object remoteValue = DoRemote(regionID);
            if (remoteValue != null || m_doRemoteOnly)
                return (EstateSettings)remoteValue;

            EstateSettings settings = new EstateSettings() {EstateID = 0};
            int estateID = GetEstateID(regionID);
            if (estateID == 0)
                return settings;
            settings = GetEstate(estateID);
            return settings;
        }

        public EstateSettings GetEstateSettings(int EstateID)
        {
            object remoteValue = DoRemote(EstateID);
            if (remoteValue != null || m_doRemoteOnly)
                return (EstateSettings)remoteValue;

            return GetEstate(EstateID);
        }

        public EstateSettings GetEstateSettings(string name)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateName"] = name;
            //            var EstateID = int.Parse (GD.Query (new string[1] { "EstateID" }, "estatesettings", filter, null, null, null) [0]);
            List<string> estate = GD.Query(new string[1] { "EstateID" }, m_estateSettingsTable, filter, null, null, null);

            if (estate.Count == 0)              // not found!!
                return null;

            int EstateID;
            if (!int.TryParse (estate[0], out EstateID))
                return null;
            
            return GetEstate (EstateID);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public int CreateNewEstate(EstateSettings es)
        {
            object remoteValue = DoRemote(es.ToOSD());
            if (remoteValue != null || m_doRemoteOnly)
                return (int) remoteValue;


            int estateID = GetEstate(es.EstateOwner, es.EstateName);
            if (estateID > 0)
            {
                return estateID;
            }

            // check for system or user estates
            if ((es.EstateOwner == (UUID) Constants.GovernorUUID))                  // Mainland?
            {
                es.EstateID = Constants.MainlandEstateID;
            } else if ( (es.EstateOwner == (UUID) Constants.RealEstateOwnerUUID) )  // System Estate?
            {
                es.EstateID = (uint) Constants.SystemEstateID;                       
            } else                                                                  // must be a new user estate then
                es.EstateID = GetNewEstateID();

            SaveEstateSettings(es, true);
            return (int) es.EstateID;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public int CreateNewEstate(EstateSettings es, UUID RegionID)
        {
            object remoteValue = DoRemote(es.ToOSD(), RegionID);
            if (remoteValue != null || m_doRemoteOnly)
                return (int) remoteValue;


            int estateID = GetEstate(es.EstateOwner, es.EstateName);
            if (estateID > 0)
            {
                if (LinkRegion(RegionID, estateID))
                    return estateID;
                return 0;
            }
            es.EstateID = GetNewEstateID();
            SaveEstateSettings(es, true);
            LinkRegion(RegionID, (int) es.EstateID);
            return (int) es.EstateID;
        }


        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void SaveEstateSettings(EstateSettings es)
        {
            object remoteValue = DoRemote(es.ToOSD());
            if (remoteValue != null || m_doRemoteOnly)
                return;

            SaveEstateSettings(es, false);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool LinkRegion(UUID regionID, int estateID)
        {
            object remoteValue = DoRemote(regionID, estateID);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            Dictionary<string, object> row = new Dictionary<string, object>(2);
            row["RegionID"] = regionID;
            row["EstateID"] = estateID;
            GD.Replace(m_estateRegionsTable, row);

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool DelinkRegion(UUID regionID)
        {
            object remoteValue = DoRemote(regionID);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["RegionID"] = regionID;
            GD.Delete(m_estateRegionsTable, filter);

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public bool DeleteEstate(int estateID)
        {
            object remoteValue = DoRemote(estateID);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateID"] = estateID;
            GD.Delete(m_estateRegionsTable, filter);
            GD.Delete(m_estateSettingsTable, filter);

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public bool EstateExists(string name)
        {
            object remoteValue = DoRemote(name);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateName"] = name;
            List<string> retVal = GD.Query(new string[1] { "EstateID" }, m_estateSettingsTable, filter, null, null, null);

            return retVal.Count > 0;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public int GetEstateID(string name)
        {
            object remoteValue = DoRemote(name);
            if (remoteValue != null || m_doRemoteOnly)
                return (int) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateName"] = name;
            List<string> retVal = GD.Query(new string[1] { "EstateID" }, m_estateSettingsTable, filter, null, null, null);

            if (retVal.Count > 0)
                return int.Parse(retVal[0]);        // return the EstateID
            return 0;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public int GetEstate(UUID ownerID, string name)
        {
            object remoteValue = DoRemote(ownerID, name);
            if (remoteValue != null || m_doRemoteOnly)
                return (int) remoteValue;
                
            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateName"] = name;
            filter.andFilters["EstateOwner"] = ownerID;

            List<string> retVal = GD.Query(new string[1] { "EstateID" }, m_estateSettingsTable, filter, null, null, null);

            if (retVal.Count > 0)
                return int.Parse(retVal[0]);
            return 0;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<UUID> GetRegions(int estateID)
        {
            object remoteValue = DoRemote(estateID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<UUID>) remoteValue;

            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateID"] = estateID;
            return
                GD.Query(new string[1] {"RegionID"}, m_estateRegionsTable, filter, null, null, null)
                  .ConvertAll(x => UUID.Parse(x));
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<string> GetEstates()
        {
            List<string> estates = GD.Query(new string[1] { "EstateName" }, m_estateSettingsTable, null, null, null, null);
            return estates;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<EstateSettings> GetEstates(UUID OwnerID)
        {
            return GetEstates(OwnerID, new Dictionary<string, bool>(0));
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<EstateSettings> GetEstates(UUID OwnerID, Dictionary<string, bool> boolFields)
        {
            object remoteValue = DoRemote(OwnerID, boolFields);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<EstateSettings>) remoteValue;

            List<EstateSettings> settings = new List<EstateSettings>();

            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateOwner"] = OwnerID;
            List<int> retVal =
                GD.Query(new string[1] { "EstateID" }, m_estateSettingsTable, filter, null, null, null)
                  .ConvertAll(x => int.Parse(x));
            foreach (int estateID in retVal)
            {
                bool Add = true;
                EstateSettings es = GetEstate(estateID);

                if (boolFields.Count > 0)
                {
                    OSDMap esmap = es.ToOSD();
                    foreach (KeyValuePair<string, bool> field in boolFields)
                    {
                        if (esmap.ContainsKey(field.Key) && esmap[field.Key].AsBoolean() != field.Value)
                        {
                            Add = false;
                            break;
                        }
                    }
                }

                if (Add)
                {
                    settings.Add(es);
                }
            }
            return settings;
        }

        #endregion

        #region Helpers

        public int GetEstateID(UUID regionID)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["RegionID"] = regionID;

            List<string> retVal = GD.Query(new string[1] {"EstateID"}, m_estateRegionsTable, filter, null, null, null);

            return (retVal.Count > 0) ? int.Parse(retVal[0]) : 0;
        }

        EstateSettings GetEstate(int estateID)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["EstateID"] = estateID;
            List<string> retVals = GD.Query(new string[1] { "*" }, m_estateSettingsTable, filter, null, null, null);
            EstateSettings settings = new EstateSettings
                                          {
                                              EstateID = 0
                                          };
            if (retVals.Count > 0)
                settings.FromOSD((OSDMap) OSDParser.DeserializeJson(retVals[4]));

            return settings;
        }

        uint GetNewEstateID()
        {
            List<string> QueryResults = GD.Query(new string[2]
                                                     {
                                                         "COUNT(EstateID)",
                                                         "MAX(EstateID)"
                                                     }, m_estateSettingsTable, null, null, null, null);
            if (uint.Parse (QueryResults [0]) > 0)
            {
                uint esID = uint.Parse (QueryResults [1]);
                if (esID > 99)                                 // Mainland is @#1, system estate is #10, user estates start at 100
                    return esID + 1;
            }
            return 100;
        }

        protected void SaveEstateSettings(EstateSettings es, bool doInsert)
        {
            Dictionary<string, object> values = new Dictionary<string, object>(5);
            values["EstateID"] = es.EstateID;
            values["EstateName"] = es.EstateName;
            values["EstateOwner"] = es.EstateOwner;
            values["ParentEstateID"] = es.ParentEstateID;
            values["Settings"] = OSDParser.SerializeJsonString(es.ToOSD());

            if (!doInsert)
            {
                QueryFilter filter = new QueryFilter();
                filter.andFilters["EstateID"] = es.EstateID;
                GD.Update(m_estateSettingsTable, values, null, filter, null, null);
            }
            else
            {
                GD.Insert(m_estateSettingsTable, values);
            }
        }

        #endregion
    }
}