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

using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using OpenMetaverse;
using System.Collections.Generic;
using System.IO;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.Modules.Web
{
    public class RegionParcelsPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                {
                    "html/regionprofile/parcels.html"
                };
            }
        }

        public bool RequiresAuthentication
        {
            get { return false; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            // under development
            //response = "<h3>Sorry! This feature is not available yet<</h3><br /> Redirecting to main page" +
            //    "<script language=\"javascript\">" +
            //    "setTimeout(function() {window.location.href = \"index.html\";}, 3000);" +
            //    "</script>";
            //return null;
            
            response = null;
            var vars = new Dictionary<string, object>();
            if (httpRequest.Query.ContainsKey("regionid"))
            {
                var regionService = webInterface.Registry.RequestModuleInterface<IGridService> ();
                var region = regionService.GetRegionByUUID(null, UUID.Parse(httpRequest.Query["regionid"].ToString()));

                IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
                EstateSettings estate = estateConnector.GetEstateSettings(region.RegionID);

                vars.Add("RegionName", region.RegionName);
                vars.Add("OwnerUUID", estate.EstateOwner);
                var estateOwnerAccount = webInterface.Registry.RequestModuleInterface<IUserAccountService>().
                    GetUserAccount(null, estate.EstateOwner);
                vars.Add("OwnerName", estateOwnerAccount == null ? "No account found" : estateOwnerAccount.Name);
                vars.Add("RegionLocX", region.RegionLocX/Constants.RegionSize);
                vars.Add("RegionLocY", region.RegionLocY/Constants.RegionSize);
                vars.Add("RegionSizeX", region.RegionSizeX);
                vars.Add("RegionSizeY", region.RegionSizeY);
                vars.Add("RegionType", region.RegionType);
                vars.Add("RegionTerrain", region.RegionTerrain);
                vars.Add("RegionOnline",
                    (region.Flags & (int) RegionFlags.RegionOnline) ==
                    (int) RegionFlags.RegionOnline
                    ? translator.GetTranslatedString("Online")
                    : translator.GetTranslatedString("Offline"));

                IDirectoryServiceConnector directoryConnector =
                    Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector>();
                if (directoryConnector != null)
                {
                    List<LandData> data = directoryConnector.GetParcelsByRegion(0, 10, region.RegionID, UUID.Zero,
                        ParcelFlags.None, ParcelCategory.Any);
                    List<Dictionary<string, object>> parcels = new List<Dictionary<string, object>>();
                    string url = "../images/icons/no_parcel.jpg";

                    foreach (var p in data)
                    {
                        Dictionary<string, object> parcel = new Dictionary<string, object>();
                        parcel.Add("ParcelNameText", translator.GetTranslatedString("ParcelNameText"));
                        parcel.Add("ParcelOwnerText", translator.GetTranslatedString("ParcelOwnerText"));
                        parcel.Add("ParcelUUID", p.GlobalID);
                        parcel.Add("ParcelName", p.Name);
                        parcel.Add("ParcelOwnerUUID", p.OwnerID);
                        parcel.Add ("ParcelSnapshotURL", url);
                        IUserAccountService accountService =
                            webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                        if (accountService != null)
                        {
                            var account = accountService.GetUserAccount(null, p.OwnerID);
                            if (account == null)
                                parcel.Add("ParcelOwnerName", translator.GetTranslatedString("NoAccountFound"));
                            else
                                parcel.Add("ParcelOwnerName", account.Name);
                        }
                        parcels.Add(parcel);
                    }
                    vars.Add("ParcelInRegion", parcels);
                    vars.Add("NumberOfParcelsInRegion", parcels.Count);
                }
                IWebHttpTextureService webTextureService = webInterface.Registry.
                    RequestModuleInterface<IWebHttpTextureService>();
                if (webTextureService != null && region.TerrainMapImage != UUID.Zero)
                    vars.Add("RegionImageURL", webTextureService.GetTextureURL(region.TerrainMapImage));
                else
                    vars.Add("RegionImageURL", "../images/icons/no_terrain.jpg");

             /*   // Regionprofile Menus
                vars.Add("MenuRegionTitle", translator.GetTranslatedString("MenuRegionTitle"));
                vars.Add("TooltipsMenuRegion", translator.GetTranslatedString("TooltipsMenuRegion"));
                vars.Add("MenuParcelTitle", translator.GetTranslatedString("MenuParcelTitle"));
                vars.Add("TooltipsMenuParcel", translator.GetTranslatedString("TooltipsMenuParcel"));
                vars.Add("MenuOwnerTitle", translator.GetTranslatedString("MenuOwnerTitle"));
                vars.Add("TooltipsMenuOwner", translator.GetTranslatedString("TooltipsMenuOwner"));
                */
                vars.Add("RegionInformationText", translator.GetTranslatedString("RegionInformationText"));
                vars.Add("OwnerNameText", translator.GetTranslatedString("OwnerNameText"));
                vars.Add("RegionLocationText", translator.GetTranslatedString("RegionLocationText"));
                vars.Add("RegionSizeText", translator.GetTranslatedString("RegionSizeText"));
                vars.Add("RegionNameText", translator.GetTranslatedString("RegionNameText"));
                vars.Add("RegionTypeText", translator.GetTranslatedString("RegionTypeText"));
                vars.Add("RegionTerrainText", translator.GetTranslatedString("RegionTerrainText"));
                vars.Add("RegionInfoText", translator.GetTranslatedString("RegionInfoText"));
                vars.Add("RegionOnlineText", translator.GetTranslatedString("RegionOnlineText"));
                vars.Add("NumberOfUsersInRegionText", translator.GetTranslatedString("NumberOfUsersInRegionText"));
                vars.Add("ParcelsInRegionText", translator.GetTranslatedString("ParcelsInRegionText"));
                vars.Add ("MainServerURL", webInterface.GridURL);

            }

            return vars;

        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            httpResponse.ContentType = "text/html";
            text = File.ReadAllText("html/regionprofile/parcels.html");
            return true;
        }
    }
}