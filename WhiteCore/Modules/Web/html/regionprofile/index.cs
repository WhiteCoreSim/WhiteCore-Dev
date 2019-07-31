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
using System.IO;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;


namespace WhiteCore.Modules.Web
{
    public class RegionInfoPage : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                {
                    "html/regionprofile/index.html",
                    "html/regionprofile/base.html",
                    "html/regionprofile/"
                };
            }
        }

        public bool RequiresAuthentication {
            get { return false; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();
            if (httpRequest.Query.ContainsKey ("regionid")) {
                var regionService = webInterface.Registry.RequestModuleInterface<IGridService> ();
                var region = regionService.GetRegionByUUID (null, UUID.Parse (httpRequest.Query ["regionid"].ToString ()));

                IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();
                EstateSettings estate = null;
                if (estateConnector != null)
                    estate = estateConnector.GetRegionEstateSettings (region.RegionID);
                if (estate != null) {
                    vars.Add ("OwnerUUID", estate.EstateOwner);
                    var estateOwnerAccount = webInterface.Registry.RequestModuleInterface<IUserAccountService> ().
                                                GetUserAccount (null, estate.EstateOwner);
                    vars.Add ("OwnerName", estateOwnerAccount.Valid ? estateOwnerAccount.Name : "No account found");
                } else {
                    vars.Add ("OwnerUUID", "Unknown");
                    vars.Add ("OwnerName", "Unknown");
                }

                vars.Add ("RegionName", region.RegionName);
                vars.Add ("RegionLocX", region.RegionLocX / Constants.RegionSize);
                vars.Add ("RegionLocY", region.RegionLocY / Constants.RegionSize);
                vars.Add ("RegionSizeX", region.RegionSizeX);
                vars.Add ("RegionSizeY", region.RegionSizeY);
                vars.Add ("RegionType", region.RegionType);
                vars.Add ("RegionTerrain", region.RegionTerrain);
                vars.Add ("RegionMaturity", Utilities.GetRegionMaturity (region.Access));

                bool regionIsOnline = (region.Flags & (int)RegionFlags.RegionOnline) == (int)RegionFlags.RegionOnline;
                vars.Add ("RegionOnline",
                    regionIsOnline
                    ? translator.GetTranslatedString ("Online")
                    : translator.GetTranslatedString ("Offline"));

                IAgentInfoService agentInfoService = webInterface.Registry.RequestModuleInterface<IAgentInfoService> ();
                IUserAccountService userService = webInterface.Registry.RequestModuleInterface<IUserAccountService> ();
                if (agentInfoService != null) {
                    List<UserInfo> usersInRegion = agentInfoService.GetUserInfos (region.RegionID);
                    vars.Add ("NumberOfUsersInRegion", usersInRegion != null ? usersInRegion.Count : 0);
                    List<Dictionary<string, object>> users = new List<Dictionary<string, object>> ();
                    if (userService != null) {
                        foreach (var client in usersInRegion) {
                            UserAccount userAcct = userService.GetUserAccount (null, (UUID)client.UserID);
                            if (!userAcct.Valid)    // ?? maybe we should just show as 'Unknown' -greythane- 20190730
                                continue;
                            Dictionary<string, object> user = new Dictionary<string, object> ();
                            user.Add ("UserNameText", translator.GetTranslatedString ("UserNameText"));
                            user.Add ("UserUUID", client.UserID);
                            user.Add ("UserName", userAcct.Name);
                            users.Add (user);
                        }
                    }
                    vars.Add ("UsersInRegion", users);
                } else {
                    vars.Add ("NumberOfUsersInRegion", 0);
                    vars.Add ("UsersInRegion", new List<Dictionary<string, object>> ());
                }
                IDirectoryServiceConnector directoryConnector =
                    Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
                if (directoryConnector != null) {
                    List<LandData> parcelData = directoryConnector.GetParcelsByRegion (0, 10, region.RegionID, UUID.Zero,
                        ParcelFlags.None, ParcelCategory.Any);
                    /*List<Dictionary<string, object>> parcels = new List<Dictionary<string, object>>();
                    foreach (var p in parcelData)
                     {
                        Dictionary<string, object> parcel = new Dictionary<string, object>();
                        parcel.Add("ParcelNameText", translator.GetTranslatedString("ParcelNameText"));
                        parcel.Add("ParcelOwnerText", translator.GetTranslatedString("ParcelOwnerText"));
                        parcel.Add("ParcelUUID", p.GlobalID);
                        parcel.Add("ParcelName", p.Name);
                        parcel.Add("ParcelOwnerUUID", p.OwnerID);
                        IUserAccountService accountService =
                            webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                        if (accountService != null)
                        {
                            var ownerAcct = accountService.GetUserAccount(null, p.OwnerID);
                            if (!ownerAcct.Valid)
                                parcel.Add("ParcelOwnerName", translator.GetTranslatedString("NoAccountFound"));
                            else
                                parcel.Add("ParcelOwnerName", ownerAcct.Name);
                        }
                        parcels.Add(parcel);
                    }

                    vars.Add("ParcelInRegion", parcels);
*/
                    if (parcelData != null)
                        vars.Add ("NumberOfParcelsInRegion", parcelData.Count);
                    else
                        vars.Add ("NumberOfParcelsInRegion", 0);
                }
                IWebHttpTextureService webTextureService = webInterface.Registry.RequestModuleInterface<IWebHttpTextureService> ();
                var regionMapURL = "../images/icons/no_terrain.jpg";

                if (webTextureService != null && region.TerrainMapImage != UUID.Zero)
                    regionMapURL = webTextureService.GetTextureURL (region.TerrainMapImage);

                vars.Add ("RegionImageURL", regionMapURL);

                // worldview
                IConfig worldViewConfig =
                    webInterface.Registry.RequestModuleInterface<ISimulationBase> ().ConfigSource.Configs ["WorldViewModule"];
                bool worldViewEnabled = false;
                if (worldViewConfig != null)
                    worldViewEnabled = worldViewConfig.GetBoolean ("Enabled", true);

                if (webTextureService != null && worldViewEnabled && regionIsOnline)
                    vars.Add ("RegionWorldViewURL", webTextureService.GetRegionWorldViewURL (region.RegionID));
                else
                    vars.Add ("RegionWorldViewURL", regionMapURL);

                // Menu Region
                vars.Add ("MenuRegionTitle", translator.GetTranslatedString ("MenuRegionTitle"));
                vars.Add ("TooltipsMenuRegion", translator.GetTranslatedString ("TooltipsMenuRegion"));
                vars.Add ("MenuParcelTitle", translator.GetTranslatedString ("MenuParcelTitle"));
                vars.Add ("TooltipsMenuParcel", translator.GetTranslatedString ("TooltipsMenuParcel"));
                vars.Add ("MenuOwnerTitle", translator.GetTranslatedString ("MenuOwnerTitle"));
                vars.Add ("TooltipsMenuOwner", translator.GetTranslatedString ("TooltipsMenuOwner"));


                vars.Add ("RegionInformationText", translator.GetTranslatedString ("RegionInformationText"));
                vars.Add ("OwnerNameText", translator.GetTranslatedString ("OwnerNameText"));
                vars.Add ("RegionLocationText", translator.GetTranslatedString ("RegionLocationText"));
                vars.Add ("RegionSizeText", translator.GetTranslatedString ("RegionSizeText"));
                vars.Add ("RegionNameText", translator.GetTranslatedString ("RegionNameText"));
                vars.Add ("RegionTypeText", translator.GetTranslatedString ("RegionTypeText"));
                vars.Add ("RegionMaturityText", translator.GetTranslatedString ("RegionMaturityText"));
                vars.Add ("RegionTerrainText", translator.GetTranslatedString ("RegionTerrainText"));
                vars.Add ("RegionInfoText", translator.GetTranslatedString ("RegionInfoText"));
                vars.Add ("RegionOnlineText", translator.GetTranslatedString ("RegionOnlineText"));
                vars.Add ("NumberOfUsersInRegionText", translator.GetTranslatedString ("NumberOfUsersInRegionText"));
                vars.Add ("ParcelsInRegionText", translator.GetTranslatedString ("ParcelsInRegionText"));
                vars.Add ("MainServerURL", webInterface.GridURL);

            }

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            httpResponse.ContentType = "text/html";
            text = File.ReadAllText ("html/regionprofile/index.html");
            return true;
        }
    }
}