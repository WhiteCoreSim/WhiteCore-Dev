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
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using DataPlugins = WhiteCore.Framework.Utilities.DataManager;

namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        #region Parcels

        static OSDMap LandData2WebOSD (LandData parcel)
        {
            var parcelOSD = parcel.ToOSD ();
            parcelOSD ["GenericData"] = parcelOSD.ContainsKey ("GenericData") 
                                                 ? (parcelOSD ["GenericData"].Type == OSDType.Map 
                                                    ? parcelOSD ["GenericData"] 
                                                    : (OSDMap)OSDParser.DeserializeLLSDXml (parcelOSD ["GenericData"].ToString ())) 
                                                 : new OSDMap ();
            parcelOSD ["Bitmap"] = OSD.FromBinary (parcelOSD ["Bitmap"]).ToString ();
            return parcelOSD;
        }

        OSDMap GetParcelsByRegion (OSDMap map)
        {
            var resp = new OSDMap ();
            resp ["Parcels"] = new OSDArray ();
            resp ["Total"] = OSD.FromInteger (0);

            var directory = DataPlugins.RequestPlugin<IDirectoryServiceConnector> ();

            if (directory != null && map.ContainsKey ("Region") == true) {
                UUID regionID = UUID.Parse (map ["Region"]);
                // not used? // UUID scopeID = map.ContainsKey ("ScopeID") ? UUID.Parse (map ["ScopeID"].ToString ()) : UUID.Zero;
                UUID owner = map.ContainsKey ("Owner") ? UUID.Parse (map ["Owner"].ToString ()) : UUID.Zero;
                uint start = map.ContainsKey ("Start") ? uint.Parse (map ["Start"].ToString ()) : 0;
                uint count = map.ContainsKey ("Count") ? uint.Parse (map ["Count"].ToString ()) : 10;
                ParcelFlags flags = map.ContainsKey ("Flags") ? (ParcelFlags)int.Parse (map ["Flags"].ToString ()) : ParcelFlags.None;
                ParcelCategory category = map.ContainsKey ("Category") ? (ParcelCategory)uint.Parse (map ["Flags"].ToString ()) : ParcelCategory.Any;
                uint total = directory.GetNumberOfParcelsByRegion (regionID, owner, flags, category);

                if (total > 0) {
                    resp ["Total"] = OSD.FromInteger ((int)total);
                    if (count == 0) {
                        return resp;
                    }
                    List<LandData> regionParcels = directory.GetParcelsByRegion (start, count, regionID, owner, flags, category);
                    OSDArray parcels = new OSDArray (regionParcels.Count);
                    regionParcels.ForEach (delegate (LandData parcel) {
                        parcels.Add (LandData2WebOSD (parcel));
                    });
                    resp ["Parcels"] = parcels;
                }
            }

            return resp;
        }

        OSDMap GetParcel (OSDMap map)
        {
            OSDMap resp = new OSDMap ();

            UUID regionID = map.ContainsKey ("RegionID") ? UUID.Parse (map ["RegionID"].ToString ()) : UUID.Zero;
            // not used?? // UUID scopeID = map.ContainsKey ("ScopeID") ? UUID.Parse (map ["ScopeID"].ToString ()) : UUID.Zero;
            UUID parcelID = map.ContainsKey ("ParcelInfoUUID") ? UUID.Parse (map ["ParcelInfoUUID"].ToString ()) : UUID.Zero;
            string parcelName = map.ContainsKey ("Parcel") ? map ["Parcel"].ToString ().Trim () : string.Empty;

            var directory = DataPlugins.RequestPlugin<IDirectoryServiceConnector> ();

            if (directory != null && (parcelID != UUID.Zero || (regionID != UUID.Zero && parcelName != string.Empty))) {
                LandData parcel = null;

                if (parcelID != UUID.Zero) {
                    parcel = directory.GetParcelInfo (parcelID);
                } else if (regionID != UUID.Zero && parcelName != string.Empty) {
                    parcel = directory.GetParcelInfo (regionID, parcelName);
                }

                if (parcel != null) {
                    resp ["Parcel"] = LandData2WebOSD (parcel);
                }
            }

            return resp;
        }

        #endregion
	}
}
