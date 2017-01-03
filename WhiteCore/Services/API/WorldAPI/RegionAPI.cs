/*
 * Copyright (c) Contributors, http://whitecore-sim.org/
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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using DataPlugins = WhiteCore.Framework.Utilities.DataManager;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {

        #region Regions

        OSDMap GetRegions (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            RegionFlags type = map.Keys.Contains ("RegionFlags") ? (RegionFlags)map ["RegionFlags"].AsInteger () : RegionFlags.RegionOnline;
            int start = map.Keys.Contains ("Start") ? map ["Start"].AsInteger () : 0;
            if (start < 0) {
                start = 0;
            }
            int count = map.Keys.Contains ("Count") ? map ["Count"].AsInteger () : 10;
            if (count < 0) {
                count = 1;
            }

            var regiondata = DataPlugins.RequestPlugin<IRegionData> ();

            Dictionary<string, bool> sort = new Dictionary<string, bool> ();

            string [] supportedSort = {
                "SortRegionName",
                "SortLocX",
                "SortLocY"
            };

            foreach (string sortable in supportedSort) {
                if (map.ContainsKey (sortable)) {
                    sort [sortable.Substring (4)] = map [sortable].AsBoolean ();
                }
            }

            List<GridRegion> regions = regiondata.Get (type, sort);
            OSDArray Regions = new OSDArray ();
            if (start < regions.Count) {
                int i = 0;
                int j = regions.Count <= (start + count) ? regions.Count : (start + count);
                for (i = start; i < j; ++i) {
                    Regions.Add (regions [i].ToOSD ());
                }
            }
            resp ["Start"] = OSD.FromInteger (start);
            resp ["Count"] = OSD.FromInteger (count);
            resp ["Total"] = OSD.FromInteger (regions.Count);
            resp ["Regions"] = Regions;
            return resp;
        }

        OSDMap GetRegion (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            IRegionData regiondata = DataPlugins.RequestPlugin<IRegionData> ();
            if (regiondata != null && (map.ContainsKey ("RegionID") || map.ContainsKey ("Region"))) {
                string regionName = map.ContainsKey ("Region") ? map ["Region"].ToString ().Trim () : "";
                UUID regionID = map.ContainsKey ("RegionID") ? UUID.Parse (map ["RegionID"].ToString ()) : UUID.Zero;
                UUID scopeID = map.ContainsKey ("ScopeID") ? UUID.Parse (map ["ScopeID"].ToString ()) : UUID.Zero;
                GridRegion region = null;
                if (regionID != UUID.Zero) {
                    region = regiondata.Get (regionID, null);
                } else if (regionName != string.Empty) {
                    region = regiondata.Get (regionName, null, null, null) [0];
                }
                if (region != null) {
                    resp ["Region"] = region.ToOSD ();
                }
            }
            return resp;
        }

        #endregion

    }
}
