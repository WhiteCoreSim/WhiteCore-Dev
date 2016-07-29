﻿/*
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
    public class RegionInfoOldPage : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/region_info.html"
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
                IAgentInfoService agentInfoService = webInterface.Registry.RequestModuleInterface<IAgentInfoService> ();
                IUserAccountService userService = webInterface.Registry.RequestModuleInterface<IUserAccountService> ();

                var regionService = webInterface.Registry.RequestModuleInterface<IGridService> ();
                var region = regionService.GetRegionByUUID (null, UUID.Parse (httpRequest.Query ["regionid"].ToString ()));

                IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();
                var ownerUUID = UUID.Zero;
                var ownerName = "Unknown";
                if (estateConnector != null) {
                    EstateSettings estate = estateConnector.GetEstateSettings (region.RegionID);
                    if (estate != null) {
                        ownerUUID = estate.EstateOwner;
                        UserAccount estateOwnerAccount = null;
                        if (userService != null)
                            estateOwnerAccount = userService.GetUserAccount (null, estate.EstateOwner);
                        ownerName = estateOwnerAccount == null ? "No account found" : estateOwnerAccount.Name;
                    }
                }
                vars.Add ("OwnerUUID", ownerUUID);
                vars.Add ("OwnerName", ownerName);

                vars.Add ("RegionName", region.RegionName);
                vars.Add ("RegionLocX", region.RegionLocX / Constants.RegionSize);
                vars.Add ("RegionLocY", region.RegionLocY / Constants.RegionSize);
                vars.Add ("RegionSizeX", region.RegionSizeX);
                vars.Add ("RegionSizeY", region.RegionSizeY);
                vars.Add ("RegionType", region.RegionType);
                vars.Add ("RegionTerrain", region.RegionTerrain);
                vars.Add ("RegionOnline",
                         (region.Flags & (int)RegionFlags.RegionOnline) ==
                         (int)RegionFlags.RegionOnline
                             ? translator.GetTranslatedString ("Online")
                             : translator.GetTranslatedString ("Offline"));

                if (agentInfoService != null) {
                    List<UserInfo> usersInRegion = agentInfoService.GetUserInfos (region.RegionID);
                    if (usersInRegion == null) {
                        vars.Add ("NumberOfUsersInRegion", 0);
                        vars.Add ("UsersInRegion", new List<Dictionary<string, object>> ());
                    } else {
                        vars.Add ("NumberOfUsersInRegion", usersInRegion.Count);
                        List<Dictionary<string, object>> users = new List<Dictionary<string, object>> ();
                        foreach (var client in usersInRegion) {
                            if (userService != null) {
                                UserAccount account = userService.GetUserAccount (null, client.UserID);
                                if (account != null) {

                                    Dictionary<string, object> user = new Dictionary<string, object> ();
                                    user.Add ("UserNameText", translator.GetTranslatedString ("UserNameText"));
                                    user.Add ("UserUUID", client.UserID);
                                    user.Add ("UserName", account.Name);
                                    users.Add (user);
                                }
                            }
                        }
                        vars.Add ("UsersInRegion", users);
                    }
                } else {
                    vars.Add ("NumberOfUsersInRegion", 0);
                    vars.Add ("UsersInRegion", new List<Dictionary<string, object>> ());
                }
                IDirectoryServiceConnector directoryConnector =
                    Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();

                if (directoryConnector != null) {
                    List<LandData> data = directoryConnector.GetParcelsByRegion (0, 10, region.RegionID, UUID.Zero,
                                                                                ParcelFlags.None, ParcelCategory.Any);
                    List<Dictionary<string, object>> parcels = new List<Dictionary<string, object>> ();
                    if (data != null) {
                        foreach (var p in data) {
                            Dictionary<string, object> parcel = new Dictionary<string, object> ();
                            parcel.Add ("ParcelNameText", translator.GetTranslatedString ("ParcelNameText"));
                            parcel.Add ("ParcelOwnerText", translator.GetTranslatedString ("ParcelOwnerText"));
                            parcel.Add ("ParcelUUID", p.GlobalID);
                            parcel.Add ("ParcelName", p.Name);
                            parcel.Add ("ParcelOwnerUUID", p.OwnerID);
                            if (userService != null) {
                                var account = userService.GetUserAccount (null, p.OwnerID);
                                if (account != null)
                                    parcel.Add ("ParcelOwnerName", account.Name);
                                else
                                    parcel.Add ("ParcelOwnerName", translator.GetTranslatedString ("NoAccountFound"));
                            }
                            parcels.Add (parcel);
                        }
                    }
                    vars.Add ("ParcelInRegion", parcels);
                    vars.Add ("NumberOfParcelsInRegion", parcels.Count);
                }
                IWebHttpTextureService webTextureService = webInterface.Registry.
                                                                        RequestModuleInterface<IWebHttpTextureService> ();
                if (webTextureService != null && region.TerrainMapImage != UUID.Zero)
                    vars.Add ("RegionImageURL", webTextureService.GetTextureURL (region.TerrainMapImage));
                else
                    vars.Add ("RegionImageURL", "images/icons/no_picture.jpg");

                // Menu Region
                vars.Add ("MenuRegionTitle", translator.GetTranslatedString ("MenuRegionTitle"));
                vars.Add ("MenuParcelTitle", translator.GetTranslatedString ("MenuParcelTitle"));
                vars.Add ("MenuOwnerTitle", translator.GetTranslatedString ("MenuOwnerTitle"));

                vars.Add ("RegionInformationText", translator.GetTranslatedString ("RegionInformationText"));
                vars.Add ("OwnerNameText", translator.GetTranslatedString ("OwnerNameText"));
                vars.Add ("RegionLocationText", translator.GetTranslatedString ("RegionLocationText"));
                vars.Add ("RegionSizeText", translator.GetTranslatedString ("RegionSizeText"));
                vars.Add ("RegionNameText", translator.GetTranslatedString ("RegionNameText"));
                vars.Add ("RegionTypeText", translator.GetTranslatedString ("RegionTypeText"));
                vars.Add ("RegionTerrainText", translator.GetTranslatedString ("RegionTerrainText"));
                vars.Add ("RegionInfoText", translator.GetTranslatedString ("RegionInfoText"));
                vars.Add ("RegionOnlineText", translator.GetTranslatedString ("RegionOnlineText"));
                vars.Add ("NumberOfUsersInRegionText", translator.GetTranslatedString ("NumberOfUsersInRegionText"));
                vars.Add ("ParcelsInRegionText", translator.GetTranslatedString ("ParcelsInRegionText"));

                // Style Switcher
                vars.Add ("styles1", translator.GetTranslatedString ("styles1"));
                vars.Add ("styles2", translator.GetTranslatedString ("styles2"));
                vars.Add ("styles3", translator.GetTranslatedString ("styles3"));
                vars.Add ("styles4", translator.GetTranslatedString ("styles4"));
                vars.Add ("styles5", translator.GetTranslatedString ("styles5"));

                vars.Add ("StyleSwitcherStylesText", translator.GetTranslatedString ("StyleSwitcherStylesText"));
                vars.Add ("StyleSwitcherLanguagesText", translator.GetTranslatedString ("StyleSwitcherLanguagesText"));
                vars.Add ("StyleSwitcherChoiceText", translator.GetTranslatedString ("StyleSwitcherChoiceText"));

                // Language Switcher
                vars.Add ("en", translator.GetTranslatedString ("en"));
                vars.Add ("fr", translator.GetTranslatedString ("fr"));
                vars.Add ("de", translator.GetTranslatedString ("de"));
                vars.Add ("it", translator.GetTranslatedString ("it"));
                vars.Add ("es", translator.GetTranslatedString ("es"));
                vars.Add ("nl", translator.GetTranslatedString ("nl"));

                var settings = webInterface.GetWebUISettings ();
                vars.Add ("ShowLanguageTranslatorBar", !settings.HideLanguageTranslatorBar);
                vars.Add ("ShowStyleBar", !settings.HideStyleBar);

            }

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}