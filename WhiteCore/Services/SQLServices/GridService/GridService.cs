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
using System.Collections.Generic;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Other;
using WhiteCore.Framework.Utilities;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.Services.SQLServices.GridService
{
    public class GridService : ConnectorBase, IGridService, IService
    {
        #region Declares

        protected bool m_AllowDuplicateNames;
        protected bool m_AllowNewRegistrations = true;
        protected bool m_AllowNewRegistrationsWithPass;
        protected string m_RegisterRegionPassword = "";
        protected IRegionData m_Database;
        protected bool m_DisableRegistrations;
        protected bool m_UseSessionID = true;
        protected IConfigSource m_config;
        protected int m_maxRegionSize = Constants.MaxRegionSize;
        protected int m_cachedMaxRegionSize;
        protected int m_cachedRegionViewSize;
        protected IAgentInfoService m_agentInfoService;
        protected ISyncMessagePosterService m_syncPosterService;
        protected IGridServerInfoService m_gridServerInfo;

        struct NeighborLocation
        {
            public UUID RegionID;
            public int RegionLocX;
            public int RegionLocY;

            public override bool Equals (object obj)
            {
                if (obj is NeighborLocation)
                {
                    NeighborLocation loc = (NeighborLocation)obj;
                    return loc.RegionID == RegionID &&
                    loc.RegionLocX == RegionLocX &&
                    loc.RegionLocY == RegionLocY;
                }
                return false;
            }

            public static bool operator == (NeighborLocation a, NeighborLocation b)
            {
                // 20160407 - greythane - Null checks are invalid as NeighbourLocation is never null
                /*
                // If both are null, or both are same instance, return true.
                if (Object.ReferenceEquals (a, b))
                    return true;

                // If one is null, but not both, return false.
                if (((object)a == null) || ((object)b == null))
                    return false;
                */
                // Return true if the fields match:
                return a.Equals (b);
            }

            public static bool operator != (NeighborLocation a, NeighborLocation b)
            {
                return !(a == b);
            }

            public override int GetHashCode ()
            {
                string idStr = RegionID.ToString ();
                int hash = idStr.GetHashCode ();

                hash = (hash * 3) + (RegionLocX * 5) + (RegionLocY * 7);
	
                return hash;
            }

        }

        class NeighborLocationEqualityComparer : IEqualityComparer<NeighborLocation>
        {
            public bool Equals (NeighborLocation b1, NeighborLocation b2)
            {
                return b1 == b2;
            }

            public int GetHashCode (NeighborLocation bx)
            {
                return bx.GetHashCode ();
            }

        }

        readonly Dictionary<NeighborLocation, List<GridRegion>> m_KnownNeighbors = 
            new Dictionary<NeighborLocation, List<GridRegion>> (new NeighborLocationEqualityComparer ());

        #endregion

        #region IService Members

        public virtual void Initialize (IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs ["Handlers"];
            if (handlerConfig.GetString ("GridHandler", "") != Name)
                return;

            //MainConsole.Instance.DebugFormat("[Grid service]: Starting...");
            Configure (config, registry);
        }

        public virtual void Configure (IConfigSource config, IRegistryCore registry)
        {
            m_config = config;
            IConfig gridConfig = config.Configs ["GridService"];
            if (gridConfig != null)
            {
                m_DisableRegistrations = gridConfig.GetBoolean ("DisableRegistrations", m_DisableRegistrations);
                m_AllowNewRegistrations = gridConfig.GetBoolean ("AllowNewRegistrations", m_AllowNewRegistrations);
                m_AllowNewRegistrationsWithPass = gridConfig.GetBoolean ("AllowNewRegistrationsWithPass",
                    m_AllowNewRegistrationsWithPass);
                m_RegisterRegionPassword =
                    Util.Md5Hash (gridConfig.GetString ("RegisterRegionPassword", m_RegisterRegionPassword));
                m_maxRegionSize = gridConfig.GetInt ("MaxRegionSize", m_maxRegionSize);
                m_cachedRegionViewSize = gridConfig.GetInt ("RegionSightSize", m_cachedRegionViewSize);
                m_UseSessionID = !gridConfig.GetBoolean ("DisableSessionID", !m_UseSessionID);
                m_AllowDuplicateNames = gridConfig.GetBoolean ("AllowDuplicateNames", m_AllowDuplicateNames);
            }

            registry.RegisterModuleInterface<IGridService> (this);
            Init (registry, Name, serverPath: "/grid/", serverHandlerName: "GridServerURI");

            if (IsLocalConnector && (MainConsole.Instance != null))
            {
                MainConsole.Instance.Commands.AddCommand (
                    "show region",
                    "show region [Region name]",
                    "Show details on a region",
                    HandleShowRegion, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set region flags",
                    "set region flags [Region name] [flags]",
                    "Set database flags for region",
                    HandleSetFlags, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set region scope",
                    "set region scope [Region name] [UUID]",
                    "Set database scope for region",
                    HandleSetScope, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "grid clear all regions",
                    "grid clear all regions",
                    "Clears all regions from the database",
                    HandleClearAllRegions, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "grid clear down regions",
                    "grid clear down regions",
                    "Clears all regions that are offline from the database",
                    HandleClearAllDownRegions, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "grid clear region",
                    "grid clear region [RegionName]",
                    "Clears the regions with the given name from the database",
                    HandleClearRegion, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "grid enable region registration",
                    "grid enable region registration",
                    "Allows new regions to be registered with the grid",
                    HandleRegionRegistration, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "grid disable region registration",
                    "grid disable region registration",
                    "Disallows new regions to be registered with the grid",
                    HandleRegionRegistration, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "show full grid",
                    "show full grid",
                    "Show details of the grid regions",
                    HandleShowFullGrid, false, true);
            }

        }

        public virtual void Start (IConfigSource config, IRegistryCore registry)
        {
            m_Database = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            m_agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
            m_syncPosterService = m_registry.RequestModuleInterface<ISyncMessagePosterService> ();
        }

        public virtual void FinishedStartup ()
        {
            m_gridServerInfo = m_registry.RequestModuleInterface<IGridServerInfoService> ();

        }

        public virtual string Name
        {
            get { return GetType ().Name; }
        }

        #endregion

        #region IGridService Members

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual int GetMaxRegionSize ()
        {
            if (m_cachedMaxRegionSize != 0)
                return m_cachedMaxRegionSize;

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI");
                int rval = remoteValue != null ? (int) remoteValue : 0;
                m_cachedMaxRegionSize = rval == 0 ? Constants.MaxRegionSize : rval;
                return m_cachedMaxRegionSize;
            }

            return m_maxRegionSize == 0 ? Constants.MaxRegionSize : m_maxRegionSize;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual int GetRegionViewSize ()
        {
            if (m_cachedRegionViewSize != 0)
                return m_cachedRegionViewSize;

            m_cachedRegionViewSize = 1;

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI");
                m_cachedRegionViewSize = remoteValue != null ? (int)remoteValue : 1;
            }
            return m_cachedRegionViewSize;
        }

        public virtual IGridService InnerService
        {
            get { return this; }
        }

        /// <summary>
        ///     Gets the default regions that people land in if they have no other region to enter
        /// </summary>
        /// <param name="scopeIDs"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual List<GridRegion> GetDefaultRegions (List<UUID> scopeIDs)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs);
                return remoteValue != null ? (List<GridRegion>)remoteValue : new List<GridRegion> ();
            }

            List<GridRegion> regions = m_Database.GetDefaultRegions (scopeIDs);

            List<GridRegion> ret = regions.Where (r => (r.Flags & (int)RegionFlags.RegionOnline) != 0).ToList ();

            MainConsole.Instance.DebugFormat ("[Grid service]: GetDefaultRegions returning {0} regions", ret.Count);
            return ret;
        }

        /// <summary>
        ///     Attempts to find regions that are good for the agent to login to if the default and fallback regions are down.
        /// </summary>
        /// <param name="scopeIDs"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual List<GridRegion> GetSafeRegions (List<UUID> scopeIDs, int x, int y)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, x, y);
                return remoteValue != null ? (List<GridRegion>)remoteValue : new List<GridRegion> ();
            }

            return m_Database.GetSafeRegions (scopeIDs, x, y);
        }

        /// <summary>
        ///     Tells the grid server that this region is not able to be connected to.
        ///     This updates the down flag in the map and blocks it from becoming a 'safe' region fallback
        ///     Only called by LLLoginService
        /// </summary>
        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual void SetRegionUnsafe (UUID id)
        {
            /*object remoteValue = DoRemoteByURL("GridServerURI", id);
            if (remoteValue != null || m_doRemoteOnly)
                return;*/

            GridRegion data = m_Database.Get (id, null);
            if (data == null)
                return;
            if ((data.Flags & (int)RegionFlags.Safe) == (int)RegionFlags.Safe)
                data.Flags &= ~(int)RegionFlags.Safe; //Remove only the safe var the first time
            else if ((data.Flags & (int)RegionFlags.RegionOnline) == (int)RegionFlags.RegionOnline)
                data.Flags &= ~(int)RegionFlags.RegionOnline; //Remove online the second time it fails
            m_Database.Store (data);
        }

        /// <summary>
        ///     Tells the grid server that this region is able to be connected to.
        ///     This updates the down flag in the map and allows it to become a 'safe' region fallback
        ///     Only called by LLLoginService
        /// </summary>
        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual void SetRegionSafe (UUID id)
        {
            /*object remoteValue = DoRemoteByURL("GridServerURI", id);
            if (remoteValue != null || m_doRemoteOnly)
                return;*/

            GridRegion data = m_Database.Get (id, null);
            if (data == null)
                return;
            if ((data.Flags & (int)RegionFlags.Safe) == 0)
                data.Flags |= (int)RegionFlags.Safe;
            else if ((data.Flags & (int)RegionFlags.RegionOnline) == 0)
                data.Flags |= (int)RegionFlags.RegionOnline;
            m_Database.Store (data);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual List<GridRegion> GetFallbackRegions (List<UUID> scopeIDs, int x, int y)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, x, y);
                return remoteValue != null ? (List<GridRegion>)remoteValue : new List<GridRegion> ();
            }

            List<GridRegion> regions = m_Database.GetFallbackRegions (scopeIDs, x, y);

            List<GridRegion> ret = regions.Where (r => (r.Flags & (int)RegionFlags.RegionOnline) != 0).ToList ();

            MainConsole.Instance.DebugFormat ("[Grid service]: Fallback returned {0} regions", ret.Count);
            return ret;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual int GetRegionFlags (List<UUID> scopeIDs, UUID regionID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, regionID);
                return remoteValue != null ? (int)remoteValue : -1;
            }

            GridRegion region = m_Database.Get (regionID, scopeIDs);

            //MainConsole.Instance.DebugFormat("[Grid service]: Request for flags of {0}: {1}", regionID, flags);
            return region != null ? region.Flags : -1;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual multipleMapItemReply GetMapItems (List<UUID> scopeIDs, ulong regionHandle,
                                                        GridItemType gridItemType)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, regionHandle, gridItemType);
                return remoteValue != null ? (multipleMapItemReply)remoteValue : new multipleMapItemReply ();
            }

            multipleMapItemReply allItems = new multipleMapItemReply ();
            if (gridItemType == GridItemType.AgentLocations) //Grid server only cares about agent locations
            {
                int X, Y;
                Util.UlongToInts (regionHandle, out X, out Y);
                //Get the items and send them back
                allItems.items [regionHandle] = GetItems (scopeIDs, X, Y, regionHandle);
            }
            return allItems;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.None)]
        public virtual RegisterRegion RegisterRegion (GridRegion regionInfos, UUID oldSessionID, string password,
                                                     int majorProtocolVersion, int minorProtocolVersion)
        {
            RegisterRegion rr;
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", regionInfos, oldSessionID, password,
                                     majorProtocolVersion, minorProtocolVersion);
                if (remoteValue != null)
                    return (RegisterRegion)remoteValue;
            
                rr = new RegisterRegion { Error = "Could not reach the grid service." };
                return rr;
            }

            if (majorProtocolVersion < ProtocolVersion.MINIMUM_MAJOR_PROTOCOL_VERSION ||
                minorProtocolVersion < ProtocolVersion.MINIMUM_MINOR_PROTOCOL_VERSION)
            {
                return new RegisterRegion {
                    Error = "You need to update your version of WhiteCore, the protocol version is too low to connect to this server."
                };
            }

            if (m_DisableRegistrations)
                return new RegisterRegion { Error = "Registrations are disabled." };

            UUID NeedToDeletePreviousRegion = UUID.Zero;

            //Get the range of this so that we get the full count and make sure that we are not overlapping smaller regions
            List<GridRegion> regions = m_Database.Get (regionInfos.RegionLocX - GetMaxRegionSize (),
                                           regionInfos.RegionLocY - GetMaxRegionSize (),
                                           regionInfos.RegionLocX + regionInfos.RegionSizeX - 1,
                                           regionInfos.RegionLocY + regionInfos.RegionSizeY - 1,
                                           null);
            if (regions.Any (r => (r.RegionLocX >= regionInfos.RegionLocX &&
                r.RegionLocX < regionInfos.RegionLocX + regionInfos.RegionSizeX) &&
                (r.RegionLocY >= regionInfos.RegionLocY &&
                r.RegionLocY < regionInfos.RegionLocY + regionInfos.RegionSizeY) &&
                r.RegionID != regionInfos.RegionID))
            {
                MainConsole.Instance.WarnFormat (
                    "[Grid service]: Region {0} tried to register in coordinates {1}, {2} which are already in use in scope {3}.",
                    regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY, regionInfos.ScopeID);
                return new RegisterRegion { Error = "Region overlaps another region" };
            }

            GridRegion region = m_Database.Get (regionInfos.RegionID, null);

            if (region != null)
            {
                //If we already have a session, we need to check it
                if (!VerifyRegionSessionID (region, oldSessionID))
                {
                    MainConsole.Instance.WarnFormat (
                        "[Grid service]: Region {0} called register, but the sessionID they provided is wrong!",
                        region.RegionName);
                    return new RegisterRegion { Error = "Wrong Session ID" };
                }
            }

            if ((!m_AllowNewRegistrations && region == null) && (!m_AllowNewRegistrationsWithPass))
            {
                MainConsole.Instance.WarnFormat (
                    "[Grid service]: Region {0} tried to register but registrations are disabled.",
                    regionInfos.RegionName);
                return new RegisterRegion { Error = "Registrations are disabled." };
            }

            if (region == null && m_AllowNewRegistrationsWithPass && password != m_RegisterRegionPassword)
            {
                MainConsole.Instance.WarnFormat (
                    "[Grid service]: Region {0} tried to register but passwords didn't match.", regionInfos.RegionName);
                // don't want to leak info so just tell them its disabled
                return new RegisterRegion { Error = "Registrations are disabled." };
            }

            if (m_maxRegionSize != 0 &&
                (regionInfos.RegionSizeX > m_maxRegionSize || regionInfos.RegionSizeY > m_maxRegionSize))
            {
                //Too big... kick it out
                MainConsole.Instance.WarnFormat (
                    "[Grid service]: Region {0} tried to register with too large of a size {1},{2}.",
                    regionInfos.RegionName, regionInfos.RegionSizeX, regionInfos.RegionSizeY);
                return new RegisterRegion { Error = "Region is too large, reduce its size." };
            }

            if ((region != null) && (region.RegionID != regionInfos.RegionID))
            {
                MainConsole.Instance.WarnFormat (
                    "[Grid service]: Region {0} tried to register in coordinates {1}, {2} which are already in use in scope {3}.",
                    regionInfos.RegionName,
                    regionInfos.RegionLocX / Constants.RegionSize,
                    regionInfos.RegionLocY / Constants.RegionSize,
                    regionInfos.ScopeID);
                return new RegisterRegion { Error = "Region overlaps another region" };
            }

            if ((region != null) && (region.RegionID == regionInfos.RegionID) &&
                ((region.RegionLocX != regionInfos.RegionLocX) || (region.RegionLocY != regionInfos.RegionLocY)))
            {
                if ((region.Flags & (int)RegionFlags.NoMove) != 0)
                    return new RegisterRegion {
                        Error = "Can't move this region," + region.RegionLocX / Constants.RegionSize +
                            "," + region.RegionLocY / Constants.RegionSize
                    };

                // Region reregistering in other coordinates. Delete the old entry
                MainConsole.Instance.DebugFormat (
                    "[Grid service]: Region {0} ({1}) was previously registered at {2}, {3}. Deleting old entry.",
                    regionInfos.RegionName,
                    regionInfos.RegionID,
                    regionInfos.RegionLocX / Constants.RegionSize,
                    regionInfos.RegionLocY / Constants.RegionSize
                );

                NeedToDeletePreviousRegion = regionInfos.RegionID;
            }

            if (region != null)
            {
                // There is a preexisting record
                //
                // Get it's flags
                //
                RegionFlags rflags = (RegionFlags)region.Flags;

                // Is this a reservation?
                //
                if ((rflags & RegionFlags.Reservation) != 0)
                {
                    // Regions reserved for the null key cannot be taken.
                    if (region.SessionID == UUID.Zero)
                        return new RegisterRegion { Error = "Region location is reserved" };

                    // Treat it as an auth request
                    //
                    // NOTE: Fudging the flags value here, so these flags
                    //       should not be used elsewhere. Don't optimize
                    //       this with the later retrieval of the same flags!
                    rflags |= RegionFlags.Authenticate;
                }
            }

            if (!m_AllowDuplicateNames)
            {
                List<GridRegion> dupe = m_Database.Get (regionInfos.RegionName, null, null, null);
                if (dupe != null && dupe.Count > 0)
                {
                    if (dupe.Any (d => d.RegionID != regionInfos.RegionID))
                    {
                        MainConsole.Instance.WarnFormat (
                            "[Grid service]: Region {0} tried to register duplicate name with ID {1}.",
                            regionInfos.RegionName, regionInfos.RegionID);
                        return new RegisterRegion { Error = "Duplicate region name" };
                    }
                }
            }

            if (region != null)
            {
                //If we are locked out, we can't come in
                if ((region.Flags & (int)RegionFlags.LockedOut) != 0)
                    return new RegisterRegion { Error = "Region locked out" };

                //Remove the reservation if we are there now
                region.Flags &= ~(int)RegionFlags.Reservation;

                regionInfos.Flags = region.Flags; // Preserve flags
                //Preserve scopeIDs
                regionInfos.AllScopeIDs = region.AllScopeIDs;
                regionInfos.ScopeID = region.ScopeID;
            } else
            {
                //Regions do not get to set flags, so wipe them
                regionInfos.Flags = 0;
                //See if we are in the configuration anywhere and have flags set

                IConfig gridConfig = m_config.Configs ["GridService"];
                if ((gridConfig != null) && regionInfos.RegionName != string.Empty)
                {
                    int newFlags = 0;
                    string regionName = regionInfos.RegionName.Trim ().Replace (' ', '_');
                    newFlags = ParseFlags (newFlags, gridConfig.GetString ("DefaultRegionFlags", string.Empty));
                    newFlags = ParseFlags (newFlags, gridConfig.GetString ("Region_" + regionName, string.Empty));
                    newFlags = ParseFlags (newFlags, gridConfig.GetString ("Region_" + regionInfos.RegionHandle, string.Empty));
                    regionInfos.Flags = newFlags;
                }
            }

            //Set these so that we can make sure the region is online later
            regionInfos.Flags |= (int)RegionFlags.RegionOnline;
            regionInfos.Flags |= (int)RegionFlags.Safe;
            regionInfos.LastSeen = Util.UnixTimeSinceEpoch ();

            //Update the sessionID, use the old so that we don't generate a bunch of these
            UUID SessionID = oldSessionID == UUID.Zero ? UUID.Random () : oldSessionID;
            regionInfos.SessionID = SessionID;

            // Everything is ok, let's register
            try
            {
                if (NeedToDeletePreviousRegion != UUID.Zero)
                    m_Database.Delete (NeedToDeletePreviousRegion);

                if (m_Database.Store (regionInfos))
                {
                    //Get the neighbors for them
                    List<GridRegion> neighbors = GetNeighbors (null, regionInfos);
                    FixNeighbors (regionInfos, neighbors, false);

                    MainConsole.Instance.InfoFormat ("[Grid service]: Region {0} registered successfully at {1}, {2}",
                        regionInfos.RegionName,
                        regionInfos.RegionLocX / Constants.RegionSize,
                        regionInfos.RegionLocY/ Constants.RegionSize);

                    Dictionary<string, List<string>> uris = m_gridServerInfo == null ? null : m_gridServerInfo.RetrieveAllGridURIs (false);
                    if (uris != null && uris.Count == 0)    //We don't have all of them yet
                        return new RegisterRegion { Error = "Grid is not fully ready yet, please try again shortly" };
                    return new RegisterRegion {
                        Error = "",
                        Neighbors = neighbors,
                        RegionFlags = regionInfos.Flags,
                        SessionID = SessionID,
                        Region = regionInfos,
                        URIs = m_gridServerInfo == null ? null : m_gridServerInfo.RetrieveAllGridURIs (false)
                    };
                }
            } catch (Exception e)
            {
                MainConsole.Instance.WarnFormat ("[Grid service]: Database exception: {0}", e);
            }

            return new RegisterRegion { Error = "Failed to save region into the database." };
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual string UpdateMap (GridRegion gregion, bool online)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", gregion, online);
                return remoteValue != null ? (string)remoteValue : string.Empty;
            }

            GridRegion region = m_Database.Get (gregion.RegionID, null);
            if (region != null)
            {
                if (!VerifyRegionSessionID (region, gregion.SessionID))
                {
                    MainConsole.Instance.Warn (
                        "[Grid service]: Region called UpdateMap, but provided incorrect SessionID! Possible attempt to disable a region!!");
                    return "Wrong Session ID";
                }

                MainConsole.Instance.DebugFormat ("[Grid service]: Region {0} updated its map", gregion.RegionID);

                m_Database.Delete (gregion.RegionID);

                if (online)
                {
                    region.Flags |= (int)RegionFlags.RegionOnline;
                    region.Flags |= (int)RegionFlags.Safe;
                } else
                {
                    region.Flags &= ~(int)RegionFlags.RegionOnline;
                    region.Flags &= ~(int)RegionFlags.Safe;
                }

                region.TerrainImage = gregion.TerrainImage;
                region.TerrainMapImage = gregion.TerrainMapImage;
                region.SessionID = gregion.SessionID;
                region.AllScopeIDs = gregion.AllScopeIDs;
                region.ScopeID = gregion.ScopeID;
                //Update all of these as well, as they are able to be set by the region owner
                region.EstateOwner = gregion.EstateOwner;
                region.Access = gregion.Access;
                region.ExternalHostName = gregion.ExternalHostName;
                region.HttpPort = gregion.HttpPort;
                region.RegionName = gregion.RegionName;
                region.RegionType = gregion.RegionType;
                region.RegionTerrain = gregion.RegionTerrain;
                region.RegionArea = gregion.RegionArea;

                try
                {
                    region.LastSeen = Util.UnixTimeSinceEpoch ();
                    m_Database.Store (region);
                    FixNeighbors (region, GetNeighbors (null, region), false);
                } catch (Exception e)
                {
                    MainConsole.Instance.DebugFormat ("[Grid service]: Database exception: {0}", e);
                }
            }

            return string.Empty;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual bool DeregisterRegion (GridRegion gregion)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", gregion);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            GridRegion region = m_Database.Get (gregion.RegionID, null);
            if (region == null)
                return false;

            if (!VerifyRegionSessionID (region, gregion.SessionID))
            {
                MainConsole.Instance.Warn (
                    "[Grid service]: Region called deregister, but provided incorrect SessionID! Possible attempt to disable a region!!");
                return false;
            }

            MainConsole.Instance.InfoFormat ("[Grid service]: Region {0} at position {1}, {2} deregistered",
                gregion.RegionID,
                gregion.RegionLocX / Constants.RegionSize,
                gregion.RegionLocY / Constants.RegionSize
            );

            FixNeighbors (region, GetNeighbors (null, gregion), true);

            return m_Database.Delete (gregion.RegionID);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual GridRegion GetRegionByUUID (List<UUID> scopeIDs, UUID regionID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, regionID);
                return remoteValue != null ? (GridRegion)remoteValue : new GridRegion ();
            }

            return m_Database.Get (regionID, scopeIDs);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual GridRegion GetRegionByPosition (List<UUID> scopeIDs, int x, int y)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, x, y);
                return remoteValue != null ? (GridRegion)remoteValue : new GridRegion ();
            }

            return m_Database.GetZero (x, y, scopeIDs);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual GridRegion GetRegionByName (List<UUID> scopeIDs, string regionName)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, regionName);
                return remoteValue != null ? (GridRegion)remoteValue : null;
            }

            // viewers send # as a wildcard
            if (regionName.EndsWith ("#", StringComparison.Ordinal))
                regionName = regionName.TrimEnd ('#');

            List<GridRegion> rdatas = m_Database.Get (regionName + "%", scopeIDs, 0, 1);
            if ((rdatas != null) && (rdatas.Count > 0))
            {
                //Sort to find the region with the exact name that was given
                rdatas.Sort (new RegionDataComparison (regionName));
                //Results are backwards... so it needs reversed
                rdatas.Reverse ();
                return rdatas [0];
            }

            return null;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual List<GridRegion> GetRegionsByName (List<UUID> scopeIDs, string name, uint? start, uint? count)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, name, start, count);
                return remoteValue != null ? (List<GridRegion>)remoteValue : new List<GridRegion> ();
            }

            // viewers send # as a wildcard
            if (name.EndsWith ("#", StringComparison.Ordinal))
                name = name.TrimEnd ('#');

            List<GridRegion> rdatas = m_Database.Get (name + "%", scopeIDs, start, count);

            if (rdatas != null)
            {
                //Sort to find the region with the exact name that was given
                rdatas.Sort (new RegionDataComparison (name));
                //Results are backwards... so it needs reversed
                rdatas.Reverse ();
                return rdatas;
            }

            // nothing found here
            return new List<GridRegion> ();
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual uint GetRegionsByNameCount (List<UUID> scopeIDs, string name)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, name);
                return remoteValue != null ? (uint)remoteValue : 0;
            }

            // viewers send # as a wildcard
            if (name.EndsWith ("#", StringComparison.Ordinal))
                name = name.TrimEnd ('#');

            return m_Database.GetCount (name + "%", scopeIDs);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual List<GridRegion> GetRegionRange (List<UUID> scopeIDs, int xmin, int xmax, int ymin, int ymax)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, xmin, xmax, ymin, ymax);
                return remoteValue != null ? (List<GridRegion>)remoteValue : new List<GridRegion> ();
            }

            return m_Database.Get (xmin, ymin, xmax, ymax, scopeIDs);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual List<GridRegion> GetRegionRange (List<UUID> scopeIDs, float centerX, float centerY,
                                                       uint squareRangeFromCenterInMeters)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", scopeIDs, centerX, centerY,
                                     squareRangeFromCenterInMeters);
                return remoteValue != null ? (List<GridRegion>)remoteValue : new List<GridRegion> ();
            }
             
            return m_Database.Get (scopeIDs, UUID.Zero, centerX, centerY, squareRangeFromCenterInMeters);
        }

        /// <summary>
        ///     Get the cached list of neighbors or a new list
        /// </summary>
        /// <param name="scopeIDs"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual List<GridRegion> GetNeighbors (List<UUID> scopeIDs, GridRegion region)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("GridServerURI", region);
                return remoteValue != null ? (List<GridRegion>)remoteValue : new List<GridRegion> ();
            }

            NeighborLocation currentLoc = BuildNeighborLocation (region);
            //List<GridRegion> neighbors = m_KnownNeighbors.FirstOrDefault((loc)=>loc.Key == currentLoc).Value;
            List<GridRegion> neighbors;
            //if (neighbors == null)
            if (!m_KnownNeighbors.TryGetValue (currentLoc, out neighbors))
            {
                neighbors = FindNewNeighbors (region);
                m_KnownNeighbors [BuildNeighborLocation (region)] = neighbors;
            }
            GridRegion[] regions = new GridRegion[neighbors.Count];
            neighbors.CopyTo (regions);
            return AllScopeIDImpl.CheckScopeIDs (scopeIDs, new List<GridRegion> (regions));
        }

        NeighborLocation BuildNeighborLocation (GridRegion reg)
        {
            return new NeighborLocation () {
                RegionID = reg.RegionID,
                RegionLocX = reg.RegionLocX,
                RegionLocY = reg.RegionLocY
            };
        }

        #endregion

        #region Console Members

        void HandleClearAllRegions (IScene scene, string[] cmd)
        {
            //Delete everything... give no criteria to just do 'delete from gridregions'
            m_Database.DeleteAll (new[] { "1" }, new object[] { 1 });
            MainConsole.Instance.Warn ("[GridService]: Cleared all regions");
        }

        void HandleClearRegion (IScene scene, string[] cmd)
        {
            if (cmd.Length <= 3)
            {
                MainConsole.Instance.Warn ("Wrong syntax, please check the help function and try again");
                return;
            }

            string regionName = Util.CombineParams (cmd, 3);
            GridRegion r = GetRegionByName (null, regionName);
            if (r == null)
            {
                MainConsole.Instance.Warn ("[GridService]: Region was not found");
                return;
            }
            m_Database.Delete (r.RegionID);
            MainConsole.Instance.Warn ("[GridService]: Region was removed");
        }

        void HandleRegionRegistration (IScene scene, string[] cmd)
        {
            bool enabled = cmd [1] == "enable";
            m_AllowNewRegistrations = enabled;
            IConfig gridConfig = m_config.Configs ["GridService"];
            if (gridConfig != null)
                gridConfig.Set ("AllowNewRegistrations", enabled);
            MainConsole.Instance.Info ("[GridService]: Registrations have been " + (enabled ? "enabled" : "disabled") +
            " for new regions");
        }

        void HandleClearAllDownRegions (IScene scene, string[] cmd)
        {
            //Delete any flags with (Flags & 254) == 254
            m_Database.DeleteAll (new[] { "Flags" }, new object[] { 0 });
            MainConsole.Instance.Warn ("[GridService]: Cleared all down regions");
        }

        void HandleShowRegion (IScene scene, string[] cmd)
        {
            if (cmd.Length < 3)
            {
                MainConsole.Instance.Info ("Syntax: show region <region name>");
                return;
            }
            string regionname = cmd [2];
            if (cmd.Length > 3)
            {
                for (int ii = 3; ii < cmd.Length; ii++)
                {
                    regionname += " " + cmd [ii];
                }
            }


            List<GridRegion> regions = GetRegionsByName (null, regionname, null, null);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Info ("Region not found");
                return;
            }

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            // not yet  // IRegionInfoConnector regionService = m_registry.RequestModuleInterface<IRegionInfoConnector>();

            foreach (GridRegion r in regions)
            {
                RegionFlags flags = (RegionFlags)Convert.ToInt32 (r.Flags);
                int RegionPosX = r.RegionLocX / Constants.RegionSize;
                int RegionPosY = r.RegionLocY / Constants.RegionSize;

                UserAccount account = accountService.GetUserAccount (null, r.EstateOwner);

                MainConsole.Instance.Info (
                    "-------------------------------------------------------------------------------");
                MainConsole.Instance.Info ("Region Name      : " + r.RegionName);
                MainConsole.Instance.Info ("Region Maturity  : " + Utilities.GetRegionMaturity(r.Access));
                MainConsole.Instance.Info ("Region UUID      : " + r.RegionID);
                MainConsole.Instance.Info ("Region ScopeID   : " + r.ScopeID);
                MainConsole.Instance.Info ("Region Location  : " + string.Format ("{0},{1}", RegionPosX, RegionPosY));
                MainConsole.Instance.Info ("Region Size      : " + string.Format ("{0} x {1}", r.RegionSizeX, r.RegionSizeY));
                MainConsole.Instance.Info ("Region URI       : " + r.RegionURI);	
                MainConsole.Instance.Info ("Map tile UUID    : " + r.TerrainMapImage);
                MainConsole.Instance.Info ("Region Owner     : " + account.Name + " [" + r.EstateOwner + "]");
                MainConsole.Instance.Info ("Region Flags     : " + flags);
                //MainConsole.Instance.Info ("Gridserver URI    : " + r.ServerURI);				
                MainConsole.Instance.Info ("");
                MainConsole.Instance.Info ("========== Extended Region Information ==========");
                MainConsole.Instance.Info ("");
                MainConsole.Instance.Info ("Region Type      : " + r.RegionType);
                MainConsole.Instance.Info ("Region Terrain   : " + r.RegionTerrain);
                MainConsole.Instance.Info ("Region Online    : " + r.IsOnline);
                MainConsole.Instance.Info ("Region Last Seen : " + Utils.UnixTimeToDateTime(r.LastSeen));
                MainConsole.Instance.Info ("Region Area Size : " + r.RegionArea);

                /* Not yet
                var ri = regionService.GetRegionInfo (r.RegionID);
                MainConsole.Instance.CleanInfo ("Startup      : {0}" + string.Format( ri.Startup == StartupType.Normal ? "Normal" : "Delayed"));                  
                MainConsole.Instance.CleanInfo ("See into     : {0}" + string.Format( ri.SeeIntoThisSimFromNeighbor ? "yes" : "No"));             
                MainConsole.Instance.CleanInfo ("Inifinite    : {0}" + string.Format( ri.InfiniteRegion ? "Yes" : "No"));                   
                MainConsole.Instance.CleanInfo ("Capacity     : {0}" + ri.ObjectCapacity);                   
                MainConsole.Instance.CleanInfo ("Agent max    : {0}" + ri.RegionSettings.AgentLimit);
                MainConsole.Instance.CleanInfo ("Allow divide : {0}" + string.Format( ri.RegionSettings.AllowLandJoinDivide ? "Yes" : "No"));
                MainConsole.Instance.CleanInfo ("Allow resale : {0}" + string.Format( ri.RegionSettings.AllowLandResell ? "Yes" : "No"));
                */
                MainConsole.Instance.Info (
                    "-------------------------------------------------------------------------------");
                MainConsole.Instance.CleanInfo (string.Empty);
            }
        }


        void HandleShowFullGrid (IScene scene, string[] cmd)
        {

            List<GridRegion> regions = GetRegionsByName (null, "", null, null);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Info ("There does not appear to be any registered regions?");
                return;
            }

            int mainland = 0;
            float mainlandArea = 0;
            int estates = 0;
            float estateArea = 0;
            int iwcRegions = 0;
            int hgRegions = 0;
            int offLine = 0;

            string regionInfo;

            regionInfo = string.Format ("{0, -20}", "Region");
            regionInfo += string.Format ("{0, -12}", "Location");
            regionInfo += string.Format ("{0, -14}", "Size");
            regionInfo += string.Format ("{0, -12}", "Area");
            regionInfo += string.Format ("{0, -26}", "Type");
            regionInfo += string.Format ("{0, -10}", "Online");
            regionInfo += string.Format ("{0, -6}", "IWC/HG");

            MainConsole.Instance.CleanInfo (regionInfo);

            MainConsole.Instance.CleanInfo (
                "----------------------------------------------------------------------------------------------------");


            foreach (GridRegion region in regions)
            {
                string rType = region.RegionType;
                if (rType.StartsWith ("M", StringComparison.Ordinal))
                {
                    mainland++;
                    mainlandArea = mainlandArea + region.RegionArea;
                }

                if (rType.StartsWith ("E", StringComparison.Ordinal))
                {
                    estates++;
                    estateArea = estateArea + region.RegionArea;
                }

                if (!region.IsOnline)
                    offLine++;
                if (region.IsHgRegion)
                    hgRegions++;
                if (region.IsForeign)
                    iwcRegions++;

                // TODO ... change hardcoded field sizes to public constants
                regionInfo = string.Format ("{0, -20}", region.RegionName);
                regionInfo += string.Format ("{0, -12}", region.RegionLocX / Constants.RegionSize + "," + region.RegionLocY / Constants.RegionSize);
                regionInfo += string.Format ("{0, -14}", region.RegionSizeX + "x" + region.RegionSizeY);
                regionInfo += string.Format ("{0, -12}", region.RegionArea < 1000000 ? region.RegionArea + " m2" : (region.RegionArea / 1000000) + " km2");
                regionInfo += string.Format ("{0, -26}", region.RegionType);
                regionInfo += string.Format ("{0, -10}", region.IsOnline ? "yes" : "no");
                regionInfo += string.Format ("{0, -6}", (region.IsHgRegion || region.IsForeign) ? "yes" : "no");

                MainConsole.Instance.CleanInfo (regionInfo);
            }
            MainConsole.Instance.CleanInfo ("");
            MainConsole.Instance.CleanInfo (
                "----------------------------------------------------------------------------------------------------");
            MainConsole.Instance.CleanInfo ("Mainland: " + mainland + " regions with an area of " + (mainlandArea / 1000000) + " km2");
            MainConsole.Instance.CleanInfo ("Estates : " + estates + " regions with an area of " + (estateArea / 1000000) + " km2");
            MainConsole.Instance.CleanInfo ("Total   : " + (mainland + estates) + " regions with an area of " + ((mainlandArea + estateArea) / 1000000) + " km2");
            MainConsole.Instance.CleanInfo ("Offline : " + offLine);
            MainConsole.Instance.CleanInfo ("IWC/HG  : " + (hgRegions + iwcRegions));
            MainConsole.Instance.CleanInfo (string.Empty);
            MainConsole.Instance.CleanInfo (
                "----------------------------------------------------------------------------------------------------");
            MainConsole.Instance.CleanInfo ("");
        }

        int ParseFlags (int prev, string flags)
        {
            RegionFlags f = (RegionFlags)prev;

            string[] parts = flags.Split (new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in parts)
            {
                try
                {
                    int val;
                    if (p.StartsWith ("+", StringComparison.Ordinal))
                    {
                        val = (int)Enum.Parse (typeof(RegionFlags), p.Substring (1));
                        f |= (RegionFlags)val;
                    } else if (p.StartsWith ("-", StringComparison.Ordinal))
                    {
                        val = (int)Enum.Parse (typeof(RegionFlags), p.Substring (1));
                        f &= ~(RegionFlags)val;
                    } else
                    {
                        val = (int)Enum.Parse (typeof(RegionFlags), p);
                        f |= (RegionFlags)val;
                    }
                } catch (Exception)
                {
                    MainConsole.Instance.Info ("Error in flag specification: " + p);
                }
            }

            return (int)f;
        }

        void HandleSetFlags (IScene scene, string[] cmd)
        {
            if (cmd.Length < 5)
            {
                MainConsole.Instance.Info ("Syntax: set region flags <region name> <flags>");
                return;
            }

            string regionname = cmd [3];
            if (cmd.Length > 5)
            {
                for (int ii = 4; ii < cmd.Length - 1; ii++)
                {
                    regionname += " " + cmd [ii];
                }
            }
            List<GridRegion> regions = m_Database.Get (regionname, null, null, null);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Info ("Region not found");
                return;
            }

            foreach (GridRegion r in regions)
            {
                int flags = r.Flags;
                flags = ParseFlags (flags, cmd [cmd.Length - 1]);
                r.Flags = flags;
                RegionFlags f = (RegionFlags)flags;

                MainConsole.Instance.Info (String.Format ("Set region {0} to {1}", r.RegionName, f));
                m_Database.Store (r);
            }
        }

        void HandleSetScope (IScene scene, string[] cmd)
        {
            if (cmd.Length < 5)
            {
                MainConsole.Instance.Info ("Syntax: set region scope <region name> <UUID>");
                return;
            }

            string regionname = cmd [3];
            if (cmd.Length > 5)
            {
                for (int ii = 4; ii < cmd.Length - 1; ii++)
                {
                    regionname += " " + cmd [ii];
                }
            }
            List<GridRegion> regions = m_Database.Get (regionname, null, null, null);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Info ("Region not found");
                return;
            }

            foreach (GridRegion r in regions)
            {
                r.ScopeID = UUID.Parse (cmd [cmd.Length - 1]);

                MainConsole.Instance.Info (string.Format ("Set region {0} to {1}", r.RegionName, r.ScopeID));
                m_Database.Store (r);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        ///     Normalize the current float to the nearest block of 5 meters
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        float NormalizePosition (float number)
        {
            try
            {
                if (float.IsNaN (number))
                    return 0;
                if (float.IsInfinity (number))
                    return 0;
                if (number < 0)
                    number = 0;
                double n = Math.Round (number, 0); //Remove the decimal
                string Number = n.ToString (); //Round the last

                string first = Number.Remove (Number.Length - 1);
                if (first == "")
                    return 0;
                int FirstNumber = 0;
                FirstNumber = first.StartsWith (".", StringComparison.Ordinal) ? 0 : int.Parse (first);

                string endNumber = Number.Remove (0, Number.Length - 1);
                if (endNumber == "")
                    return 0;
                float EndNumber = float.Parse (endNumber);
                if (EndNumber < 2.5f)
                    EndNumber = 0;
                else if (EndNumber > 7.5)
                {
                    EndNumber = 0;
                    FirstNumber++;
                } else
                    EndNumber = 5;
                return float.Parse (FirstNumber + EndNumber.ToString ());
            } catch (Exception ex)
            {
                MainConsole.Instance.Error ("[GridService]: Error in NormalizePosition " + ex);
            }
            return 0;
        }

        /// <summary>
        ///     Get all agent locations for the given region
        /// </summary>
        /// <param name="scopeIDs"></param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        List<mapItemReply> GetItems (List<UUID> scopeIDs, int X, int Y, ulong regionHandle)
        {
            List<mapItemReply> mapItems = new List<mapItemReply> ();
            GridRegion region = GetRegionByPosition (scopeIDs, X, Y);
            //if the region is down or doesn't exist, don't check it
            if (region == null)
                return new List<mapItemReply> ();

            Dictionary<Vector3, int> Positions = new Dictionary<Vector3, int> ();
            //Get a list of all the clients in the region and add them
            List<UserInfo> userInfos = m_agentInfoService.GetUserInfos (region.RegionID);
            if (userInfos != null)
            {
                foreach (UserInfo userInfo in userInfos)
                {
                    //Normalize the positions to 5 meter blocks so that agents stack instead of cover up each other
                    Vector3 position = new Vector3 (NormalizePosition (userInfo.CurrentPosition.X),
                                       NormalizePosition (userInfo.CurrentPosition.Y), 0);
                    int Number = 0;
                    //Find the number of agents currently at this position
                    if (!Positions.TryGetValue (position, out Number))
                        Number = 0;
                    Number++;
                    Positions [position] = Number;
                }
            }

            //Build the mapItemReply blocks
            mapItems = Positions.Select (position => new mapItemReply {
                x = (uint) (region.RegionLocX + position.Key.X),
                y = (uint) (region.RegionLocY + position.Key.Y),
                id = UUID.Zero,
                name = Util.Md5Hash (region.RegionName + Environment.TickCount),
                Extra = position.Value,
                Extra2 = 0
            }).ToList ();

            //If there are no agents, we send one blank one to the client
            if (mapItems.Count == 0)
            {
                mapItemReply mapitem = new mapItemReply {
                    x = (uint)(region.RegionLocX + 1),
                    y = (uint)(region.RegionLocY + 1),
                    id = UUID.Zero,
                    name = Util.Md5Hash (region.RegionName + Environment.TickCount),
                    Extra = 0,
                    Extra2 = 0
                };
                mapItems.Add (mapitem);
            }
            return mapItems;
        }

        void FixNeighbors (GridRegion regionInfos, List<GridRegion> neighbors, bool down)
        {
            foreach (GridRegion r in neighbors)
            {
                NeighborLocation currentLoc = BuildNeighborLocation (r);
                if (m_KnownNeighbors.ContainsKey (currentLoc))
                {
                    //Add/Remove them to/from the list
                    if (down)
                        m_KnownNeighbors [currentLoc].Remove (regionInfos);
                    else if (m_KnownNeighbors [currentLoc].Find ( delegate(GridRegion rr)
                    {
                        if (rr.RegionID == regionInfos.RegionID)
                            return true;
                        return false;
                    }) == null)
                        m_KnownNeighbors [currentLoc].Add (regionInfos);
                }

                if (m_syncPosterService != null)
                    m_syncPosterService.Post (r.ServerURI,
                        SyncMessageHelper.NeighborChange (r.RegionID, regionInfos.RegionID, down));
            }

            if (down)
            {
                List<NeighborLocation> locs = m_KnownNeighbors.Keys.Where ((l) => l.RegionID == regionInfos.RegionID).ToList ();
                foreach (NeighborLocation l in locs)
                    m_KnownNeighbors.Remove (l);
            }
        }

        /// <summary>
        ///     Get all the neighboring regions of the given region
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        protected virtual List<GridRegion> FindNewNeighbors (GridRegion region)
        {
            int startX = (region.RegionLocX - 8192); //Give 8192 by default so that we pick up neighbors next to us
            int startY = (region.RegionLocY - 8192);
            if (GetMaxRegionSize () != 0)
            {
                startX = (region.RegionLocX - GetMaxRegionSize ());
                startY = (region.RegionLocY - GetMaxRegionSize ());
            }

            //-1 so that we don't get size (256) + viewsize (256) and get a region two 256 blocks over
            int endX = (region.RegionLocX + GetRegionViewSize () + region.RegionSizeX - 1);
            int endY = (region.RegionLocY + GetRegionViewSize () + region.RegionSizeY - 1);

            List<GridRegion> neighbors = GetRegionRange (null, startX, endX, startY, endY);

            neighbors.RemoveAll (delegate(GridRegion r)
            {
                if (r.RegionID == region.RegionID)
                    return true;

                if (r.RegionLocX + r.RegionSizeX - 1 < (region.RegionLocX - GetRegionViewSize ()) ||
                    r.RegionLocY + r.RegionSizeY - 1 < (region.RegionLocY - GetRegionViewSize ()))
                    //Check for regions outside of the boundary (created above when checking for large regions next to us)
                    return true;

                return false;
            });
            return neighbors;
        }

        public virtual bool VerifyRegionSessionID (GridRegion r, UUID SessionID)
        {
            if (m_UseSessionID && r.SessionID != SessionID)
                return false;
            return true;
        }

        public class RegionDataComparison : IComparer<GridRegion>
        {
            readonly string RegionName;

            public RegionDataComparison (string regionName)
            {
                RegionName = regionName;
            }

            #region IComparer<GridRegion> Members

            int IComparer<GridRegion>.Compare (GridRegion x, GridRegion y)
            {
                if (x.RegionName == RegionName)
                    return 1;
                if (y.RegionName == RegionName)
                    return -1;
                return 0;
            }

            #endregion
        }

        #endregion
    }
}
