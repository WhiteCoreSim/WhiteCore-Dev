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
using System.Globalization;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using EventFlags = OpenMetaverse.DirectoryManager.EventFlags;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Services.DataService
{
    public class LocalDirectoryServiceConnector : ConnectorBase, IDirectoryServiceConnector
    {
        IGenericData genData;
        string m_userClassifiedsTable = "user_classifieds";
        string m_eventInfoTable = "event_information";
        string m_eventNotificationTable = "event_notifications";
        string m_SearchParcelTable = "search_parcel";

        #region IDirectoryServiceConnector Members

        public void Initialize (IGenericData genericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            genData = genericData;
            m_registry = simBase;

            if (source.Configs [Name] != null)
                defaultConnectionString = source.Configs [Name].GetString ("ConnectionString", defaultConnectionString);

            if (genData != null)
                genData.ConnectToDatabase (defaultConnectionString, "Directory",
                                     source.Configs ["WhiteCoreConnectors"].GetBoolean ("ValidateTables", true));

            Framework.Utilities.DataManager.RegisterPlugin (Name + "Local", this);

            if (source.Configs ["WhiteCoreConnectors"].GetString ("DirectoryServiceConnector", "LocalConnector") ==
                "LocalConnector") {
                Framework.Utilities.DataManager.RegisterPlugin (this);
            }

            Init (simBase, Name);
        }

        public string Name {
            get { return "IDirectoryServiceConnector"; }
        }

        public void Dispose ()
        {
        }

        #region Region

        /// <summary>
        ///     This also updates the parcel, not for just adding a new one
        /// </summary>
        /// <param name="parcels"></param>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void AddRegion (List<LandData> parcels)
        {
            if (m_doRemoteOnly) {
                DoRemote (parcels);
                return;
            }

            if (parcels.Count == 0)
                return;

            ClearRegion (parcels [0].RegionID);

            foreach (var parcel in parcels) {
                var orFilters = new Dictionary<string, object> ();
                orFilters.Add ("ParcelID", parcel.GlobalID);
                genData.Delete (m_SearchParcelTable, new QueryFilter { orFilters = orFilters });
            }

            List<object []> insertValues = parcels.Select (args => new List<object> {
                args.RegionID,
                args.GlobalID,
                args.LocalID,
                args.UserLocation.X,       // this is actually the landing position for teleporting
                args.UserLocation.Y,
                args.UserLocation.Z,
                args.Name,
                args.Description,
                args.Flags,
                args.Dwell,
                UUID.Zero,                 // infoUUID - not used?
                ((args.Flags & (uint) ParcelFlags.ForSale) == (uint) ParcelFlags.ForSale) ? 1 : 0,
                args.SalePrice,
                args.AuctionID,
                args.Area,
                0,                         // Estate ID - will be set later
                args.Maturity,
                args.OwnerID,
                args.GroupID,
                ((args.Flags & (uint) ParcelFlags.ShowDirectory) == (uint) ParcelFlags.ShowDirectory) ? 1 : 0,
                args.SnapshotID,
                OSDParser.SerializeLLSDXmlString(args.Bitmap),
                (int) args.Category,
                args.ScopeID,
                args.UserLookAt.X,
                args.UserLookAt.Y,
                args.UserLookAt.Z,
                (int) args.LandingType
            }).Select (Values => Values.ToArray ()).ToList ();

            genData.InsertMultiple (m_SearchParcelTable, insertValues);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void ClearRegion (UUID regionID)
        {
            if (m_doRemoteOnly) {
                DoRemote (regionID);
                return;
            }

            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["RegionID"] = regionID;
            genData.Delete (m_SearchParcelTable, filter);
        }

        #endregion

        #region Parcels

        static List<LandData> Query2LandData (List<string> query)
        {
            List<LandData> lands = new List<LandData> ();

            for (int i = 0; i < query.Count; i += 28) {
                LandData landData = new LandData ();

                landData.RegionID = UUID.Parse (query [i]);
                landData.GlobalID = UUID.Parse (query [i + 1]);
                landData.LocalID = int.Parse (query [i + 2]);

                // be aware of culture differences here...
                var posX = (float)Convert.ToDecimal (query [i + 3], Culture.NumberFormatInfo);
                var posY = (float)Convert.ToDecimal (query [i + 4], Culture.NumberFormatInfo);
                var posZ = (float)Convert.ToDecimal (query [i + 5], Culture.NumberFormatInfo);
                landData.UserLocation = new Vector3 (posX, posY, posZ);


                // UserLocation =
                //     new Vector3(float.Parse(Query[i + 3]), float.Parse(Query[i + 4]), float.Parse(Query[i + 5])),
                landData.Name = query [i + 6];
                landData.Description = query [i + 7];
                landData.Flags = uint.Parse (query [i + 8]);
                landData.Dwell = int.Parse (query [i + 9]);
                //landData.InfoUUID = UUID.Parse(Query[i + 10]);
                landData.SalePrice = int.Parse (query [i + 12]);
                landData.AuctionID = uint.Parse (query [i + 13]);
                landData.Area = int.Parse (query [i + 14]);
                landData.Maturity = int.Parse (query [i + 16]);
                landData.OwnerID = UUID.Parse (query [i + 17]);
                landData.GroupID = UUID.Parse (query [i + 18]);
                landData.SnapshotID = UUID.Parse (query [i + 20]);

                try {
                    landData.Bitmap = OSDParser.DeserializeLLSDXml (query [i + 21]);
                } catch {
                }

                // set some flags
                if (uint.Parse (query [i + 11]) != 0)
                    landData.Flags |= (uint)ParcelFlags.ForSale;

                if (uint.Parse (query [i + 19]) != 0)
                    landData.Flags |= (uint)ParcelFlags.ShowDirectory;

                landData.Category = (string.IsNullOrEmpty (query [i + 22]))
                                        ? ParcelCategory.None
                                        : (ParcelCategory)int.Parse (query [i + 22]);
                landData.ScopeID = UUID.Parse (query [i + 23]);

                // additional stuff 20190225 -greythane-
                var laposX = (float)Convert.ToDecimal (query [i + 24], Culture.NumberFormatInfo);
                var laposY = (float)Convert.ToDecimal (query [i + 25], Culture.NumberFormatInfo);
                var laposZ = (float)Convert.ToDecimal (query [i + 26], Culture.NumberFormatInfo);
                landData.UserLookAt = new Vector3 (laposX, laposY, laposZ);

                landData.LandingType = (byte)int.Parse (query [i + 27]);

                lands.Add (landData);
            }

            return lands;
        }

        /// <summary>
        ///     Gets a parcel from the search database by ParcelID (GlobalID)
        /// </summary>
        /// <param name="globalID"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public LandData GetParcelInfo (UUID globalID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (globalID);
                return remoteValue != null ? (LandData)remoteValue : null;
            }

            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["ParcelID"] = globalID;
            List<string> query = genData.Query (new [] { "*" }, m_SearchParcelTable, filter, null, null, null);

            // Cant find it, return
            if (query.Count == 0) {
                // Try fake parcel ID for compatibility reasons, 
                // given that they were saved into the database with 
                // classifieds and picks (plus it's not hard to keep this here).
                ulong RegionHandle = 0;
                uint X, Y, Z;
                Util.ParseFakeParcelID (globalID, out RegionHandle, out X, out Y, out Z);

                int regX, regY;
                Util.UlongToInts (RegionHandle, out regX, out regY);

                GridRegion r = m_registry.RequestModuleInterface<IGridService> ().GetRegionByPosition (null, regX, regY);
                return r == null ? null : GetParcelInfo (r.RegionID, (int)X, (int)Y);
            }

            LandData parcelLandData = null;
            List<LandData> lands = Query2LandData (query);
            if (parcelLandData == null && lands.Count != 0)
                parcelLandData = lands [0];
            
            return parcelLandData;
        }

        /// <summary>
        ///     Gets a parcel from the search database by region and location in the region
        /// </summary>
        /// <param name="regionID"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public LandData GetParcelInfo (UUID regionID, int x, int y)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (regionID, x, y);
                return remoteValue != null ? (LandData)remoteValue : null;
            }

            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["RegionID"] = regionID;
            List<string> query = genData.Query (new [] { "*" }, m_SearchParcelTable, filter, null, null, null);
            //Cant find it, return
            if (query.Count == 0)
                return null;

            LandData landData = null;
            List<LandData> lands = Query2LandData (query);

            GridRegion r = m_registry.RequestModuleInterface<IGridService> ().GetRegionByUUID (null, regionID);
            if (r == null)
                return null;

            bool [,] tempConvertMap = new bool [r.RegionSizeX / 4, r.RegionSizeX / 4];
            tempConvertMap.Initialize ();

            foreach (LandData land in lands.Where (land => land.Bitmap != null)) {
                ConvertBytesToLandBitmap (ref tempConvertMap, land.Bitmap, r.RegionSizeX);
                if (tempConvertMap [x / 4, y / 4]) {
                    landData = land;
                    break;
                }
            }
            if (landData == null && lands.Count != 0)
                landData = lands [0];

            return landData;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public LandData GetParcelInfo (UUID regionID, string parcelName)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (regionID, parcelName);
                return remoteValue != null ? (LandData)remoteValue : null;
            }

            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (regiondata != null) {
                GridRegion region = regiondata.Get (regionID, null);
                if (region != null) {
                    UUID parcelInfoID = UUID.Zero;
                    QueryFilter filter = new QueryFilter ();
                    filter.andFilters ["RegionID"] = regionID;
                    filter.andFilters ["Name"] = parcelName;

                    List<string> query = genData.Query (new [] { "ParcelID" }, m_SearchParcelTable, filter, null, 0, 1);

                    if (query.Count >= 1 && UUID.TryParse (query [0], out parcelInfoID)) {
                        return GetParcelInfo (parcelInfoID);
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Gets all parcels owned by the given user
        /// </summary>
        /// <param name="ownerID"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<ExtendedLandData> GetParcelByOwner (UUID ownerID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (ownerID);
                return remoteValue != null ? (List<ExtendedLandData>)remoteValue : new List<ExtendedLandData> ();
            }

            // NOTE: this does check for group deeded land as well, so this can check for that as well
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;

            Dictionary<string, bool> sort = new Dictionary<string, bool> (1);
            sort ["ParcelID"] = false;        // descending order

            List<string> query = genData.Query (new [] { "*" }, m_SearchParcelTable, filter, sort, null, null);

            return (query.Count != 0) ? LandDataToExtendedLandData (Query2LandData (query)) : new List<ExtendedLandData> ();
        }

        List<ExtendedLandData> LandDataToExtendedLandData (List<LandData> data)
        {
            return (from land in data
                    let region = m_registry.RequestModuleInterface<IGridService> ().GetRegionByUUID (null, land.RegionID)
                    where region != null
                    select new ExtendedLandData {
                        LandData = land,
                        RegionType = region.RegionType,
                        RegionTerrain = region.RegionTerrain,
                        RegionArea = region.RegionArea,
                        RegionName = region.RegionName,
                        GlobalPosX = region.RegionLocX + land.UserLocation.X,
                        GlobalPosY = region.RegionLocY + land.UserLocation.Y
                    }).ToList ();
        }

        static QueryFilter GetParcelsByRegionWhereClause (UUID regionID, UUID owner, ParcelFlags flags,
                                                         ParcelCategory category)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["RegionID"] = regionID;

            if (owner != UUID.Zero)
                filter.andFilters ["OwnerID"] = owner;

            if (flags != ParcelFlags.None)
                filter.andBitfieldAndFilters ["Flags"] = (uint)flags;

            if (category != ParcelCategory.Any)
                filter.andFilters ["Category"] = (int)category;

            return filter;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<LandData> GetParcelsByRegion (uint start, uint count, UUID regionID, UUID owner,
                                                 ParcelFlags flags, ParcelCategory category)
        {
            List<LandData> resp = new List<LandData> (0);
            if (count == 0)
                return resp;

            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (start, count, regionID, owner, flags, category);
                return remoteValue != null ? (List<LandData>)remoteValue : resp;
            }

            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (regiondata != null) {
                GridRegion region = regiondata.Get (regionID, null);
                if (region != null) {
                    QueryFilter filter = GetParcelsByRegionWhereClause (regionID, owner, flags, category);
                    Dictionary<string, bool> sort = new Dictionary<string, bool> (1);
                    sort ["OwnerID"] = false;
                    return Query2LandData (genData.Query (new [] { "*" }, m_SearchParcelTable, filter, sort, start, count));
                }
            }

            return resp;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public uint GetNumberOfParcelsByRegion (UUID RegionID, UUID owner, ParcelFlags flags, ParcelCategory category)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (RegionID, owner, flags, category);
                return remoteValue != null ? (uint)remoteValue : 0;
            }

            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (regiondata != null) {
                GridRegion region = regiondata.Get (RegionID, null);
                if (region != null) {
                    QueryFilter filter = GetParcelsByRegionWhereClause (RegionID, owner, flags, category);
                    return uint.Parse (genData.Query (new [] { "COUNT(ParcelID)" }, m_SearchParcelTable, filter, null, null, null) [0]);
                }
            }

            return 0;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<LandData> GetParcelsWithNameByRegion (uint start, uint count, UUID regionID, string name)
        {
            List<LandData> resp = new List<LandData> (0);
            if (count == 0)
                return resp;

            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (start, count, regionID, name);
                return remoteValue != null ? (List<LandData>)remoteValue : resp;
            }


            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (regiondata != null) {
                GridRegion region = regiondata.Get (regionID, null);
                if (region != null) {
                    QueryFilter filter = new QueryFilter ();
                    filter.andFilters ["RegionID"] = regionID;
                    filter.andFilters ["Name"] = name;

                    Dictionary<string, bool> sort = new Dictionary<string, bool> (1);
                    sort ["OwnerID"] = false;

                    return Query2LandData (genData.Query (new [] { "*" }, m_SearchParcelTable, filter, sort, start, count));
                }
            }

            return resp;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public uint GetNumberOfParcelsWithNameByRegion (UUID regionID, string name)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (regionID, name);
                return remoteValue != null ? (uint)remoteValue : 0;
            }

            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (regiondata != null) {
                GridRegion region = regiondata.Get (regionID, null);
                if (region != null) {
                    QueryFilter filter = new QueryFilter ();
                    filter.andFilters ["RegionID"] = regionID;
                    filter.andFilters ["Name"] = name;

                    return uint.Parse (genData.Query (new [] { "COUNT(ParcelID)" }, m_SearchParcelTable, filter, null, null, null) [0]);
                }
            }
            return 0;
        }

        /// <summary>
        ///     Searches for parcels around the grid
        /// </summary>
        /// <param name="queryText"></param>
        /// <param name="category"></param>
        /// <param name="startQuery"></param>
        /// <param name="Flags"> </param>
        /// <param name="scopeID"> </param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<DirPlacesReplyData> FindLand (string queryText, string category, int startQuery, uint Flags,
                                                 UUID scopeID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (queryText, category, startQuery, Flags, scopeID);
                return remoteValue != null ? (List<DirPlacesReplyData>)remoteValue : new List<DirPlacesReplyData> ();
            }

            QueryFilter filter = new QueryFilter ();
            Dictionary<string, bool> sort = new Dictionary<string, bool> ();

            // If they dwell sort flag is there, sort by dwell going down
            if ((Flags & (uint)DirectoryManager.DirFindFlags.DwellSort) == (uint)DirectoryManager.DirFindFlags.DwellSort)
                sort ["Dwell"] = false;

            if (scopeID != UUID.Zero)
                filter.andFilters ["ScopeID"] = scopeID;

            filter.orLikeFilters ["Name"] = "%" + queryText + "%";
            filter.orLikeFilters ["Description"] = "%" + queryText + "%";
            filter.andFilters ["ShowInSearch"] = 1;

            if (category != "-1")
                filter.andFilters ["Category"] = category;
            
            if ((Flags & (uint)DirectoryManager.DirFindFlags.AreaSort) == (uint)DirectoryManager.DirFindFlags.AreaSort)
                sort ["Area"] = false;
            
            if ((Flags & (uint)DirectoryManager.DirFindFlags.NameSort) == (uint)DirectoryManager.DirFindFlags.NameSort)
                sort ["Name"] = false;

            List<string> retVal = genData.Query (new []
                                               {
                                                   "ParcelID",
                                                   "Name",
                                                   "ForSale",
                                                   "Auction",
                                                   "Dwell",
                                                   "Flags"
                                               }, m_SearchParcelTable, filter, sort, (uint)startQuery, 50);

            if (retVal.Count == 0)
                return new List<DirPlacesReplyData> ();

            List<DirPlacesReplyData> data = new List<DirPlacesReplyData> ();
            for (int i = 0; i < retVal.Count; i += 6) {
                // Check to make sure we are sending the requested maturity levels
                if (
                    !((int.Parse (retVal [i + 5]) & (int)ParcelFlags.MaturePublish) == (int)ParcelFlags.MaturePublish &&
                      ((Flags & (uint)DirectoryManager.DirFindFlags.IncludeMature)) == 0)) {
                    
                    data.Add (new DirPlacesReplyData {
                        parcelID = new UUID (retVal [i]),
                        name = retVal [i + 1],
                        forSale = int.Parse (retVal [i + 2]) == 1,
                        auction = retVal [i + 3] == "0",            // Auction is stored as a 0 if there is no auction
                        dwell = float.Parse (retVal [i + 4])
                    });
                }
            }

            return data;
        }

        /// <summary>
        ///     Searches for parcels for sale around the grid
        /// </summary>
        /// <param name="searchType">2 = Auction only, 8 = For Sale - Mainland, 16 = For Sale - Estate, 4294967295 = All</param>
        /// <param name="price"></param>
        /// <param name="area"></param>
        /// <param name="startQuery"></param>
        /// <param name="flags"> </param>
        /// <param name="scopeID"> </param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<DirLandReplyData> FindLandForSale (string searchType, uint price, uint area, int startQuery,
                                                      uint flags, UUID scopeID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (searchType, price, area, startQuery, flags, scopeID);
                return remoteValue != null ? (List<DirLandReplyData>)remoteValue : new List<DirLandReplyData> ();
            }

            QueryFilter filter = new QueryFilter ();

            // Only parcels set for sale will be checked
            filter.andFilters ["ForSale"] = "1";
            if (scopeID != UUID.Zero)
                filter.andFilters ["ScopeID"] = scopeID;

            // They requested a sale price check
            if ((flags & (uint)DirectoryManager.DirFindFlags.LimitByPrice) == (uint)DirectoryManager.DirFindFlags.LimitByPrice)
                filter.andLessThanEqFilters ["SalePrice"] = (int)price;

            // They requested a 
            if ((flags & (uint)DirectoryManager.DirFindFlags.LimitByArea) == (uint)DirectoryManager.DirFindFlags.LimitByArea)
                filter.andGreaterThanEqFilters ["Area"] = (int)area;

            Dictionary<string, bool> sort = new Dictionary<string, bool> ();
            if ((flags & (uint)DirectoryManager.DirFindFlags.AreaSort) == (uint)DirectoryManager.DirFindFlags.AreaSort)
                sort ["Area"] = false;
            if ((flags & (uint)DirectoryManager.DirFindFlags.NameSort) == (uint)DirectoryManager.DirFindFlags.NameSort)
                sort ["Name"] = false;

            List<string> retVal = genData.Query (new []
                                               {
                                                   "ParcelID",
                                                   "Name",
                                                   "Auction",
                                                   "SalePrice",
                                                   "Area",
                                                   "Flags"
                                               }, m_SearchParcelTable, filter, sort, (uint)startQuery, 50);

            // if there are none, return
            if (retVal.Count == 0)
                return new List<DirLandReplyData> ();

            List<DirLandReplyData> data = new List<DirLandReplyData> ();
            for (int i = 0; i < retVal.Count; i += 6) {
                DirLandReplyData replyData = new DirLandReplyData {
                    forSale = true,
                    parcelID = new UUID (retVal [i]),
                    name = retVal [i + 1],
                    auction = (retVal [i + 2] != "0")
                };
                // If its an auction and we didn't request to see auctions, skip to the next and continue
                if ((flags & (uint)DirectoryManager.SearchTypeFlags.Auction) ==
                    (uint)DirectoryManager.SearchTypeFlags.Auction && !replyData.auction) {
                    continue;
                }

                replyData.salePrice = Convert.ToInt32 (retVal [i + 3]);
                replyData.actualArea = Convert.ToInt32 (retVal [i + 4]);

                // Check maturity levels depending on what flags the user has set
                // 0 flag is an override so that we can get all lands for sale, regardless of maturity
                if (flags == 0 ||
                    !((int.Parse (retVal [i + 5]) & (int)ParcelFlags.MaturePublish) == (int)ParcelFlags.MaturePublish &&
                      ((flags & (uint)DirectoryManager.DirFindFlags.IncludeMature)) == 0)) {
                    
                    data.Add (replyData);
                }
            }

            return data;
        }

        /// <summary>
        ///     Searches for parcels for sale around the grid
        /// </summary>
        /// <param name="searchType">2 = Auction only, 8 = For Sale - Mainland, 16 = For Sale - Estate, 4294967295 = All</param>
        /// <param name="price"></param>
        /// <param name="area"></param>
        /// <param name="startQuery"></param>
        /// <param name="flags"> </param>
        /// <param name="regionID"> </param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<DirLandReplyData> FindLandForSaleInRegion (string searchType, uint price, uint area, int startQuery,
                                                               uint flags, UUID regionID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (searchType, price, area, startQuery, flags, regionID);
                return remoteValue != null ? (List<DirLandReplyData>)remoteValue : new List<DirLandReplyData> ();
            }

            QueryFilter filter = new QueryFilter ();

            // Only parcels set for sale will be checked
            filter.andFilters ["ForSale"] = "1";
            filter.andFilters ["RegionID"] = regionID;

            // They requested a sale price check
            if ((flags & (uint)DirectoryManager.DirFindFlags.LimitByPrice) ==
                (uint)DirectoryManager.DirFindFlags.LimitByPrice) {
                filter.andLessThanEqFilters ["SalePrice"] = (int)price;
            }

            // They requested a 
            if ((flags & (uint)DirectoryManager.DirFindFlags.LimitByArea) ==
                (uint)DirectoryManager.DirFindFlags.LimitByArea) {
                filter.andGreaterThanEqFilters ["Area"] = (int)area;
            }
            Dictionary<string, bool> sort = new Dictionary<string, bool> ();
            if ((flags & (uint)DirectoryManager.DirFindFlags.AreaSort) == (uint)DirectoryManager.DirFindFlags.AreaSort)
                sort ["Area"] = false;
            
            if ((flags & (uint)DirectoryManager.DirFindFlags.NameSort) == (uint)DirectoryManager.DirFindFlags.NameSort)
                sort ["Name"] = false;
            
            // if ((queryFlags & (uint)DirectoryManager.DirFindFlags.PerMeterSort) == (uint)DirectoryManager.DirFindFlags.PerMeterSort)
            //    sort["Area"] = (queryFlags & (uint)DirectoryManager.DirFindFlags.SortAsc) == (uint)DirectoryManager.DirFindFlags.SortAsc);
            if ((flags & (uint)DirectoryManager.DirFindFlags.PricesSort) ==
                (uint)DirectoryManager.DirFindFlags.PricesSort)
                sort ["SalePrice"] = (flags & (uint)DirectoryManager.DirFindFlags.SortAsc) ==
                                    (uint)DirectoryManager.DirFindFlags.SortAsc;

            List<string> retVal = genData.Query (new []
                                               {
                                                   "ParcelID",
                                                   "Name",
                                                   "Auction",
                                                   "SalePrice",
                                                   "Area",
                                                   "Flags"
                                               }, m_SearchParcelTable, filter, sort, (uint)startQuery, 50);

            // if there are none, return
            if (retVal.Count == 0)
                return new List<DirLandReplyData> ();

            List<DirLandReplyData> data = new List<DirLandReplyData> ();
            for (int i = 0; i < retVal.Count; i += 6) {
                DirLandReplyData replyData = new DirLandReplyData {
                    forSale = true,
                    parcelID = new UUID (retVal [i]),
                    name = retVal [i + 1],
                    auction = (retVal [i + 2] != "0")
                };
                // If its an auction and we didn't request to see auctions, skip to the next and continue
                if ((flags & (uint)DirectoryManager.SearchTypeFlags.Auction) ==
                    (uint)DirectoryManager.SearchTypeFlags.Auction && !replyData.auction) {
                    continue;
                }

                replyData.salePrice = Convert.ToInt32 (retVal [i + 3]);
                replyData.actualArea = Convert.ToInt32 (retVal [i + 4]);

                // Check maturity levels depending on what flags the user has set
                // 0 flag is an override so that we can get all lands for sale, regardless of maturity
                if (flags == 0 ||
                    !((int.Parse (retVal [i + 5]) & (int)ParcelFlags.MaturePublish) == (int)ParcelFlags.MaturePublish &&
                      ((flags & (uint)DirectoryManager.DirFindFlags.IncludeMature)) == 0)) {
                    
                    data.Add (replyData);
                }
            }

            return data;
        }

        /// <summary>
        ///     Searches for the most popular places around the grid
        /// </summary>
        /// <param name="queryFlags"></param>
        /// <param name="scopeID"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<DirPopularReplyData> FindPopularPlaces (uint queryFlags, UUID scopeID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (queryFlags, scopeID);
                return remoteValue != null ? (List<DirPopularReplyData>)remoteValue : new List<DirPopularReplyData> ();
            }

            QueryFilter filter = new QueryFilter ();
            Dictionary<string, bool> sort = new Dictionary<string, bool> ();

            if ((queryFlags & (uint)DirectoryManager.DirFindFlags.AreaSort) == (uint)DirectoryManager.DirFindFlags.AreaSort)
                sort ["Area"] = false;
            else if ((queryFlags & (uint)DirectoryManager.DirFindFlags.NameSort) == (uint)DirectoryManager.DirFindFlags.NameSort)
                sort ["Name"] = false;
            // else if ((queryFlags & (uint)DirectoryManager.DirFindFlags.PerMeterSort) == (uint)DirectoryManager.DirFindFlags.PerMeterSort)
            //    sort["Area"] = (queryFlags & (uint)DirectoryManager.DirFindFlags.SortAsc) == (uint)DirectoryManager.DirFindFlags.SortAsc);
            // else if ((queryFlags & (uint)DirectoryManager.DirFindFlags.PricesSort) == (uint)DirectoryManager.DirFindFlags.PricesSort)
            //    sort["SalePrice"] = (queryFlags & (uint)DirectoryManager.DirFindFlags.SortAsc) == (uint)DirectoryManager.DirFindFlags.SortAsc;
            else
                sort ["Dwell"] = false;

            if (scopeID != UUID.Zero)
                filter.andFilters ["ScopeID"] = scopeID;
            
            List<string> retVal = genData.Query (new []
                                               {
                                                   "ParcelID",
                                                   "Name",
                                                   "Dwell",
                                                   "Flags"
                                               }, m_SearchParcelTable, filter, null, 0, 25);

            // if there are none, return
            if (retVal.Count == 0)
                return new List<DirPopularReplyData> ();

            List<DirPopularReplyData> data = new List<DirPopularReplyData> ();
            for (int i = 0; i < retVal.Count; i += 4) {
                // Check maturity levels depending on what flags the user has set
                // 0 flag is an override so that we can get all lands for sale, regardless of maturity
                if (queryFlags == 0 ||
                    !((int.Parse (retVal [i + 3]) & (int)ParcelFlags.MaturePublish) == (int)ParcelFlags.MaturePublish &&
                      ((queryFlags & (uint)DirectoryManager.DirFindFlags.IncludeMature)) == 0))

                    data.Add (new DirPopularReplyData {
                        ParcelID = new UUID (retVal [i]),
                        Name = retVal [i + 1],
                        Dwell = int.Parse (retVal [i + 2])
                    });
            }

            return data;
        }

        void ConvertBytesToLandBitmap (ref bool [,] tempConvertMap, byte [] bitmap, int sizeX)
        {
            try {
                int x = 0, y = 0, i = 0;
                int avg = (sizeX * sizeX / 128);
                for (i = 0; i < avg; i++) {
                    byte tempByte = bitmap [i];
                    int bitNum = 0;
                    for (bitNum = 0; bitNum < 8; bitNum++) {
                        bool bit = Convert.ToBoolean (Convert.ToByte (tempByte >> bitNum) & 1);
                        tempConvertMap [x, y] = bit;
                        x++;
                        if (x > (sizeX / 4) - 1) {
                            x = 0;
                            y++;
                        }
                    }
                }
            } catch {
            }
        }

        #endregion

        #region Classifieds

        /// <summary>
        ///     Searches for classifieds
        /// </summary>
        /// <param name="queryText"></param>
        /// <param name="category"></param>
        /// <param name="queryFlags"></param>
        /// <param name="startQuery"></param>
        /// <param name="scopeID"> </param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<DirClassifiedReplyData> FindClassifieds (string queryText, string category, uint queryFlags,
                                                            int startQuery, UUID scopeID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (queryText, category, queryFlags, startQuery, scopeID);
                return remoteValue != null ? (List<DirClassifiedReplyData>)remoteValue : new List<DirClassifiedReplyData> ();
            }

            QueryFilter filter = new QueryFilter ();

            filter.andLikeFilters ["Name"] = "%" + queryText + "%";
            if (int.Parse (category) != (int)DirectoryManager.ClassifiedCategories.Any) //Check the category
                filter.andFilters ["Category"] = category;
            if (scopeID != UUID.Zero)
                filter.andFilters ["ScopeID"] = scopeID;

            List<string> retVal = genData.Query (new [] { "*" }, m_userClassifiedsTable, filter, null, (uint)startQuery, 50);
            if (retVal.Count == 0)
                return new List<DirClassifiedReplyData> ();

            List<DirClassifiedReplyData> data = new List<DirClassifiedReplyData> ();
            for (int i = 0; i < retVal.Count; i += 9) {
                // Pull the classified out of OSD
                Classified classified = new Classified ();
                classified.FromOSD ((OSDMap)OSDParser.DeserializeJson (retVal [i + 6]));

                DirClassifiedReplyData replyData = new DirClassifiedReplyData {
                    classifiedFlags = classified.ClassifiedFlags,
                    classifiedID = classified.ClassifiedUUID,
                    creationDate = classified.CreationDate,
                    expirationDate = classified.ExpirationDate,
                    price = classified.PriceForListing,
                    name = classified.Name
                };
                // Check maturity levels
                var maturityquery = queryFlags & 0x4C;      // strip everything except what we want
                if (maturityquery == (uint)DirectoryManager.ClassifiedQueryFlags.All)
                    data.Add (replyData);
                else {
                    // var classifiedMaturity = replyData.classifiedFlags > 0 
                    //                                  ? replyData.classifiedFlags 
                    //                                  : (byte)DirectoryManager.ClassifiedQueryFlags.PG;
                    if ((maturityquery & replyData.classifiedFlags) != 0) // required rating  PG, Mature (Adult)
                        data.Add (replyData);
                }
            }

            return data;
        }

        /// <summary>
        /// Gets a list of all classifieds.
        /// </summary>
        /// <returns>The classifieds.</returns>
        /// <param name="category">Category.</param>
        /// <param name="classifiedFlags">Query flags.</param>
        public List<Classified> GetAllClassifieds (int category, uint classifiedFlags)
        {
            List<Classified> classifieds = new List<Classified> ();

            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (category, classifiedFlags);
                return remoteValue != null ? (List<Classified>)remoteValue : classifieds;
            }

            QueryFilter filter = new QueryFilter ();

            // filter.andLikeFilters ["Name"] = "%" + queryText + "%";
            if (category != (int)DirectoryManager.ClassifiedCategories.Any) // Check the category
                filter.andFilters ["Category"] = category.ToString ();
            // if (scopeID != UUID.Zero)
            //    filter.andFilters ["ScopeID"] = scopeID;

            List<string> retVal = genData.Query (new [] { "*" }, m_userClassifiedsTable, filter, null, null, null);

            if (retVal.Count != 0) {
                for (int i = 0; i < retVal.Count; i += 9) {
                    Classified classified = new Classified ();
                    // Pull the classified out of OSD
                    classified.FromOSD ((OSDMap)OSDParser.DeserializeJson (retVal [i + 6]));

                    // Check maturity levels
                    if (classifiedFlags != (uint)DirectoryManager.ClassifiedQueryFlags.All) {
                        if ((classifiedFlags & classified.ClassifiedFlags) != 0) // required rating All, PG, Mature ( Adult )
                            classifieds.Add (classified);
                    } else
                        // add all
                        classifieds.Add (classified);
                }
            }

            return classifieds;
        }

        /// <summary>
        ///     Gets all classifieds in the given region
        /// </summary>
        /// <param name="regionName"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<Classified> GetClassifiedsInRegion (string regionName)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (regionName);
                return remoteValue != null ? (List<Classified>)remoteValue : new List<Classified> ();
            }

            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["SimName"] = regionName;
            List<string> retVal = genData.Query (new [] { "*" }, m_userClassifiedsTable, filter, null, null, null);

            if (retVal.Count == 0)
                return new List<Classified> ();

            List<Classified> classifieds = new List<Classified> ();
            for (int i = 0; i < retVal.Count; i += 9) {
                Classified classified = new Classified ();
                // Pull the classified out of OSD
                classified.FromOSD ((OSDMap)OSDParser.DeserializeJson (retVal [i + 6]));
                classifieds.Add (classified);
            }

            return classifieds;
        }

        /// <summary>
        ///     Get a classified by its UUID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public Classified GetClassifiedByID (UUID id)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (id);
                return remoteValue != null ? (Classified)remoteValue : new Classified ();
            }

            QueryFilter filter = new QueryFilter ();
            Dictionary<string, object> where = new Dictionary<string, object> (1);
            where.Add ("ClassifiedUUID", id);
            filter.andFilters = where;

            List<string> retVal = genData.Query (new [] { "*" }, m_userClassifiedsTable, filter, null, null, null);

            if ((retVal == null) || (retVal.Count == 0)) return null;

            Classified classified = new Classified ();
            classified.FromOSD ((OSDMap)OSDParser.DeserializeJson (retVal [6]));

            return classified;
        }

        #endregion

        #region Events

        /// <summary>
        ///     Searches for events with the given parameters
        /// </summary>
        /// <param name="queryText"></param>
        /// <param name="eventFlags"></param>
        /// <param name="startQuery"></param>
        /// <param name="scopeID"> </param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<DirEventsReplyData> FindEvents (string queryText, uint eventFlags, int startQuery, UUID scopeID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (queryText, eventFlags, startQuery, scopeID);
                return remoteValue != null ? (List<DirEventsReplyData>)remoteValue : new List<DirEventsReplyData> ();
            }

            List<DirEventsReplyData> eventdata = new List<DirEventsReplyData> ();

            QueryFilter filter = new QueryFilter ();
            var queryList = queryText.Split ('|');

            string stringDay = queryList [0];
            if (stringDay == "u")               // "u" means search for events that are going on today
            {
                filter.andGreaterThanEqFilters ["UNIX_TIMESTAMP(date)"] = Util.ToUnixTime (DateTime.Today);
            } else {
                // Pull the day out then and search for that many days in the future/past
                int Day = int.Parse (stringDay);
                DateTime searchedDay = DateTime.Today.AddDays (Day);
                // We only look at one day at a time
                DateTime NextDay = searchedDay.AddDays (1);
                filter.andGreaterThanEqFilters ["UNIX_TIMESTAMP(date)"] = Util.ToUnixTime (searchedDay);
                filter.andLessThanEqFilters ["UNIX_TIMESTAMP(date)"] = Util.ToUnixTime (NextDay);
                filter.andLessThanEqFilters ["flags"] = (int)(eventFlags >> 24) & 0x7;
            }

            // do we have category?
            if (queryList [1] != "0")
                filter.andLikeFilters ["category"] = queryList [1];

            // do we have search parameters
            if (queryList.Length == 3)
                filter.andLikeFilters ["name"] = "%" + queryList [2] + "%";

            if (scopeID != UUID.Zero)
                filter.andFilters ["scopeID"] = scopeID;

            List<string> retVal = genData.Query (new []
                                               {
                                                   "EID",
                                                   "creator",
                                                   "date",
                                                   "maturity",
                                                   "flags",
                                                   "name",
                                               }, m_eventInfoTable, filter, null, (uint)startQuery, 50);

            if (retVal.Count > 0) {
                for (int i = 0; i < retVal.Count; i += 6) {
                    DirEventsReplyData replyData = new DirEventsReplyData {
                        eventID = Convert.ToUInt32 (retVal [i]),
                        ownerID = new UUID (retVal [i + 1]),
                        name = retVal [i + 5],
                    };
                    DateTime date = DateTime.Parse (retVal [i + 2]);
                    replyData.date = date.ToString (new DateTimeFormatInfo ());
                    replyData.unixTime = (uint)Util.ToUnixTime (date);
                    replyData.eventFlags = Convert.ToUInt32 (retVal [i + 3]) >> 1;  // convert event maturity back to EventData.maturity 0,1,2

                    // Check the maturity levels
                    var maturity = Convert.ToByte (retVal [i + 3]);     // db levels 1,2,4
                    uint reqMaturityflags = (eventFlags >> 24) & 0x7;

                    if ((maturity & reqMaturityflags) > 0)
                        eventdata.Add (replyData);
                }

            } else
                eventdata.Add (new DirEventsReplyData ());

            return eventdata;
        }



        public List<EventData> GetAllEvents (int queryHours, int category, int maturityLevel)
        {
            return GetEventsList (null, queryHours, category, maturityLevel);
        }

        public List<EventData> GetUserEvents (string userId, int queryHours, int category, int maturityLevel)
        {
            // The same as GetEventList but incuded for more intuitive calls and possible expansion 
            return GetEventsList (userId, queryHours, category, maturityLevel);
        }

        /// <summary>
        /// Gets the events list. WebUI and API
        /// </summary>
        /// <returns>The events list.</returns>
        /// <param name="userId">User identifier.</param>
        /// <param name="queryHours">Query hours.</param>
        /// <param name="category">Category.</param>
        /// <param name="maturityLevel">Maturity level.</param>
        public List<EventData> GetEventsList (string userId, int queryHours, int category, int maturityLevel)
        {
            List<EventData> retEvents = new List<EventData> ();

            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (queryHours, category, maturityLevel);
                return remoteValue != null ? (List<EventData>)remoteValue : retEvents;
            }

            QueryFilter filter = new QueryFilter ();
            if (userId != null)
                filter.andLikeFilters ["creator"] = userId;

            var starttime = DateTime.Now;
            var endTime = starttime.AddHours (queryHours);
            filter.andGreaterThanEqFilters ["UNIX_TIMESTAMP(date)"] = Util.ToUnixTime (starttime);
            filter.andLessThanEqFilters ["UNIX_TIMESTAMP(date)"] = Util.ToUnixTime (endTime);

            // maturity level
            filter.andLessThanEqFilters ["maturity"] = maturityLevel;

            if (category != (int)DirectoryManager.EventCategories.All)  // Check the category
                filter.andFilters ["category"] = category.ToString ();

            List<string> retVal = genData.Query (new [] { "*" }, m_eventInfoTable, filter, null, null, null);

            if (retVal.Count > 0) {
                List<EventData> allEvents = Query2EventData (retVal);

                // Check the maturity levels PG = 1, M = 2, A = 4
                foreach (EventData evnt in allEvents) {
                    if ((maturityLevel & evnt.maturity) != 0)   // required rating PG, Mature, Adult
                        retEvents.Add (evnt);
                }
            }

            return retEvents;
        }

        /// <summary>
        ///     Retrieves all events in the given region by their maturity level
        /// </summary>
        /// <param name="regionName"></param>
        /// <param name="maturity">Uses DirectoryManager.EventFlags to determine the maturity requested</param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<DirEventsReplyData> FindAllEventsInRegion (string regionName, int maturity)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (regionName, maturity);
                return remoteValue != null ? (List<DirEventsReplyData>)remoteValue : new List<DirEventsReplyData> ();
            }

            List<DirEventsReplyData> Data = new List<DirEventsReplyData> ();

            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (regiondata != null) {
                List<GridRegion> regions = regiondata.GetList (regionName, null, null, null);
                if (regions.Count > 0) {
                    QueryFilter filter = new QueryFilter ();
                    filter.andFilters ["region"] = regions [0].RegionID.ToString ();
                    filter.andFilters ["maturity"] = maturity;

                    List<string> retVal = genData.Query (new []
                                                       {
                                                           "EID",
                                                           "creator",
                                                           "date",
                                                           "maturity",
                                                           "flags",
                                                           "name"
                                                       }, m_eventInfoTable, filter, null, null, null);

                    if (retVal.Count > 0) {
                        for (int i = 0; i < retVal.Count; i += 6) {
                            DirEventsReplyData replyData = new DirEventsReplyData {
                                eventID = Convert.ToUInt32 (retVal [i]),
                                ownerID = new UUID (retVal [i + 1]),
                                name = retVal [i + 5],
                            };
                            DateTime date = DateTime.Parse (retVal [i + 2]);
                            replyData.date = date.ToString (new DateTimeFormatInfo ());
                            replyData.unixTime = (uint)Util.ToUnixTime (date);
                            //replyData.eventFlags = Convert.ToUInt32(retVal[i + 4]);
                            replyData.eventFlags = Convert.ToUInt32 (retVal [i + 3]) >> 1;   // maturity level

                            Data.Add (replyData);
                        }
                    }
                }
            }

            return Data;
        }

        static List<EventData> Query2EventData (List<string> retVal)
        {
            List<EventData> events = new List<EventData> ();
            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (retVal.Count % 16 != 0 || regiondata == null) {
                return events;
            }

            for (int i = 0; i < retVal.Count; i += 16) {
                EventData data = new EventData ();

                GridRegion region = regiondata.Get (UUID.Parse (retVal [2]), null);
                if (region == null) {
                    continue;
                }

                data.simName = region.RegionName;

                data.eventID = Convert.ToUInt32 (retVal [i]);
                data.creator = retVal [i + 1];
                data.regionId = retVal [1 + 2];
                data.parcelId = retVal [i + 3];

                //Parse the time out for the viewer
                DateTime date = DateTime.Parse (retVal [i + 4]);
                data.date = date.ToString (new DateTimeFormatInfo ());
                data.dateUTC = (uint)Util.ToUnixTime (date);

                data.cover = data.amount = Convert.ToUInt32 (retVal [i + 5]);
                data.maturity = Convert.ToInt32 (retVal [i + 6]);
                data.eventFlags = Convert.ToUInt32 (retVal [i + 7]);
                data.duration = Convert.ToUInt32 (retVal [i + 8]);

                // be aware of culture differences here...
                var posX = (float)Convert.ToDecimal (retVal [i + 9], Culture.NumberFormatInfo);
                var posY = (float)Convert.ToDecimal (retVal [1 + 10], Culture.NumberFormatInfo);
                var posZ = (float)Convert.ToDecimal (retVal [i + 11], Culture.NumberFormatInfo);
                data.regionPos = new Vector3 (posX, posY, posZ);

                data.globalPos = new Vector3 (
                    region.RegionLocX + data.regionPos.X,
                    region.RegionLocY + data.regionPos.Y,
                    region.RegionLocZ + data.regionPos.Z
                    );

                data.name = retVal [i + 12];
                data.description = retVal [i + 13];
                data.category = retVal [i + 14];

                events.Add (data);
            }

            return events;
        }

        /// <summary>
        ///     Gets more info about the event by the events unique event ID
        /// </summary>
        /// <param name="eventID"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public EventData GetEventInfo (uint eventID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (eventID);
                return remoteValue != null ? (EventData)remoteValue : new EventData ();
            }

            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["EID"] = eventID;
            List<string> retVal = genData.Query (new [] { "*" }, m_eventInfoTable, filter, null, null, null);

            return (retVal.Count == 0) ? null : Query2EventData (retVal) [0];
        }

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <returns>The event.</returns>
        /// <param name="creator">Creator.</param>
        /// <param name="regionID">Region identifier.</param>
        /// <param name="parcelID">Parcel identifier.</param>
        /// <param name="date">Date.</param>
        /// <param name="cover">Cover.</param>
        /// <param name="maturity">Maturity.</param>
        /// <param name="flags">Flags.</param>
        /// <param name="duration">Duration.</param>
        /// <param name="localPos">Local position.</param>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="category">Category.</param>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public EventData CreateEvent (UUID creator, UUID regionID, UUID parcelID, DateTime date, uint cover,
                                     EventFlags maturity, uint flags, uint duration, Vector3 localPos, string name,
                                     string description, string category)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (creator, regionID, parcelID, date, cover, maturity, flags, duration, localPos,
                                          name, description, category);
                return remoteValue != null ? (EventData)remoteValue : null;
            }

            IRegionData regiondata = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            if (regiondata == null)
                return null;

            GridRegion region = regiondata.Get (regionID, null);
            if (region == null)
                return null;

            // create the event
            EventData eventData = new EventData ();
            eventData.eventID = GetMaxEventID () + 1;
            eventData.creator = creator.ToString ();
            eventData.simName = region.RegionName;
            eventData.date = date.ToString (new DateTimeFormatInfo ());
            eventData.dateUTC = (uint)Util.ToUnixTime (date);
            eventData.amount = cover;
            eventData.cover = cover;
            eventData.maturity = (int)maturity;
            eventData.eventFlags = flags | (uint)maturity;
            eventData.duration = duration;
            eventData.globalPos = new Vector3 (
                region.RegionLocX + localPos.X,
                region.RegionLocY + localPos.Y,
                region.RegionLocZ + localPos.Z);
            eventData.regionPos = localPos;
            eventData.name = name;
            eventData.description = description;
            eventData.category = category;
            eventData.regionId = regionID.ToString ();
            eventData.parcelId = parcelID.ToString ();

            Dictionary<string, object> row = new Dictionary<string, object> (15);
            row ["EID"] = eventData.eventID;
            row ["creator"] = creator.ToString ();
            row ["region"] = regionID.ToString ();
            row ["parcel"] = parcelID.ToString ();
            row ["date"] = date.ToString ("s");
            row ["cover"] = eventData.cover;
            row ["maturity"] = Util.ConvertEventMaturityToDBMaturity (maturity);      // PG = 1, M == 2, A == 4
            row ["flags"] = flags;                   // region maturity flags
            row ["duration"] = duration;
            row ["localPosX"] = localPos.X;
            row ["localPosY"] = localPos.Y;
            row ["localPosZ"] = localPos.Z;
            row ["name"] = name;
            row ["description"] = description;
            row ["category"] = category;

            genData.Insert (m_eventInfoTable, row);

            return eventData;
        }


        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public bool UpdateAddEvent (EventData eventData)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (eventData);
                return (bool)remoteValue;
            }

            // delete this event it it exists 
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["EID"] = eventData.eventID;
            genData.Delete (m_eventInfoTable, filter);

            // add the event 
            Dictionary<string, object> row = new Dictionary<string, object> (15);
            row ["EID"] = GetMaxEventID () + 1;
            row ["creator"] = eventData.creator;
            row ["region"] = eventData.regionId;
            row ["parcel"] = eventData.parcelId;
            row ["date"] = eventData.date;
            row ["cover"] = eventData.cover;
            row ["maturity"] = Util.ConvertEventMaturityToDBMaturity ((EventFlags)eventData.maturity);      // PG = 1, M == 2, A == 4
            row ["flags"] = eventData.eventFlags;             // region maturity flags
            row ["duration"] = eventData.duration;
            row ["localPosX"] = eventData.regionPos.X;
            row ["localPosY"] = eventData.regionPos.Y;
            row ["localPosZ"] = eventData.regionPos.Z;
            row ["name"] = eventData.name;
            row ["description"] = eventData.description;
            row ["category"] = eventData.category;

            try {
                genData.Insert (m_eventInfoTable, row);
            } catch {
                return false;
            }

            // assume success if no error
            return true;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public bool DeleteEvent (string eventId)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (eventId);
                return (bool)remoteValue;
            }

            // delete this event it it exists 
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["EID"] = eventId;

            try {
                genData.Delete (m_eventInfoTable, filter);
            } catch {
                return false;
            }

            // assume success if no error
            return true;
        }

        // WebUI and API calls
        public List<EventData> GetEvents (uint start, uint count, Dictionary<string, bool> sort,
                                         Dictionary<string, object> filter)
        {
            return (count == 0)
                       ? new List<EventData> (0)
                       : Query2EventData (genData.Query (new [] { "*" }, m_eventInfoTable, 
                                                    new QueryFilter {andFilters = filter}, sort, start, count));
        }

        public uint GetNumberOfEvents (Dictionary<string, object> filter)
        {
            return uint.Parse (genData.Query (new []{"COUNT(EID)"},
                                         m_eventInfoTable,
                                         new QueryFilter {andFilters = filter}, null, null, null) [0]);
        }

        public uint GetMaxEventID ()
        {
            if (GetNumberOfEvents (new Dictionary<string, object> (0)) == 0) {
                return 0;
            }
            return uint.Parse (genData.Query (new [] { "MAX(EID)" }, m_eventInfoTable, null, null, null, null) [0]);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void AddEventNofication (UUID user, uint EventID)
        {
            if (m_doRemoteOnly) {
                DoRemotePost (user, EventID);
                return;
            }

            genData.Insert (m_eventNotificationTable, new object [2] { user.ToString (), EventID });
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void RemoveEventNofication (UUID user, uint eventID)
        {
            if (m_doRemoteOnly) {
                DoRemotePost (user, eventID);
                return;
            }

            QueryFilter filter = new QueryFilter ();
            filter.andFilters.Add ("UserID", user.ToString ());
            filter.andFilters.Add ("EventID", eventID);
            genData.Delete (m_eventNotificationTable, filter);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<EventData> GetEventNotifications (UUID user)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (user);
                return remoteValue != null ? (List<EventData>)remoteValue : new List<EventData> ();
            }

            QueryFilter filter = new QueryFilter ();
            filter.andFilters.Add ("UserID", user.ToString ());
            List<string> data = genData.Query (new string [1] { "EventID" }, m_eventNotificationTable, filter, null, null, null);
            List<EventData> events = new List<EventData> ();
            if (data.Count == 0)
                return events;

            foreach (string eventID in data)
                events.Add (GetEventInfo (uint.Parse (eventID)));

            return events;
        }

        #endregion

        #endregion
    }
}
