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
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class UserRegionEditPage : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                {
                    "html/user/region_edit.html"
                };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
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
            var gridService = webInterface.Registry.RequestModuleInterface<IGridService> ();
            var user = Authenticator.GetAuthentication (httpRequest);
            vars.Add ("RegionServerURL", webInterface.GridURL); // This needs to be sorted out for grid regions

            #region EditRegion
            if (requestParameters.ContainsKey ("Submit")) {

                var regionServerURL = requestParameters ["RegionServerURL"].ToString ();
                // required
                if (regionServerURL == "") {
                    response = "<h3>" + translator.GetTranslatedString ("RegionServerURLError") + "</h3>";
                    return null;
                }

                var regionID = requestParameters ["RegionID"].ToString ();
                var regionName = requestParameters ["RegionName"].ToString ();
                //var OwnerUUID = requestParameters["OwnerUUID"].ToString();
                var regionLocX = requestParameters ["RegionLocX"].ToString ();
                var regionLocY = requestParameters ["RegionLocY"].ToString ();
                var regionSizeX = requestParameters ["RegionSizeX"].ToString ();
                var regionSizeY = requestParameters ["RegionSizeY"].ToString ();

                var regionType = requestParameters ["RegionType"].ToString ();
                var regionPresetType = requestParameters ["RegionPresetType"].ToString ();
                var regionTerrain = requestParameters ["RegionTerrain"].ToString ();

                var regionLoadTerrain = requestParameters.ContainsKey ("RegionLoadTerrain")
                    ? requestParameters ["RegionLoadTerrain"].ToString ()
                    : "";

                // a bit of idiot proofing
                if (regionName == "") {
                    response = "<h3>" + translator.GetTranslatedString ("RegionNameError") + "</h3>";
                    return null;
                }
                if ((regionLocX == "") || (regionLocY == "")) {
                    response = "<h3>" + translator.GetTranslatedString ("RegionLocationError") + "</h3>";
                    return null;
                }

                // so far so good...
                // build the new region details

                int RegionPort = int.Parse (requestParameters ["RegionPort"].ToString ());


                var newRegion = new RegionInfo ();
                if (regionID != "")
                    newRegion.RegionID = UUID.Parse (regionID);

                newRegion.RegionName = regionName;
                newRegion.RegionType = regionType;
                newRegion.RegionLocX = int.Parse (regionLocX);
                newRegion.RegionLocY = int.Parse (regionLocY);
                newRegion.RegionSizeX = int.Parse (regionSizeX);
                newRegion.RegionSizeY = int.Parse (regionSizeY);

                newRegion.RegionPort = RegionPort;
                newRegion.SeeIntoThisSimFromNeighbor = true;
                newRegion.InfiniteRegion = true;
                newRegion.ObjectCapacity = 50000;
                newRegion.Startup = StartupType.Normal;

                var regionPreset = regionPresetType.ToLower (); //SubString(0,1);
                if (regionPreset.StartsWith ("c", System.StringComparison.Ordinal)) {
                    newRegion.RegionPort = int.Parse (requestParameters ["RegionPort"].ToString ());
                    newRegion.SeeIntoThisSimFromNeighbor = (requestParameters ["RegionVisibility"].ToString ().ToLower () == "yes");
                    newRegion.InfiniteRegion = (requestParameters ["RegionInfinite"].ToString ().ToLower () == "yes");
                    newRegion.ObjectCapacity = int.Parse (requestParameters ["RegionCapacity"].ToString ());

                    string delayStartup = requestParameters ["RegionDelayStartup"].ToString ();
                    newRegion.Startup = delayStartup.StartsWith ("n", System.StringComparison.Ordinal) ? StartupType.Normal : StartupType.Medium;

                }

                if (regionPreset.StartsWith ("w", System.StringComparison.Ordinal)) {
                    // 'standard' setup
                    newRegion.RegionType = newRegion.RegionType + "Whitecore";
                    //info.RegionPort;            // use auto assigned port
                    newRegion.RegionTerrain = "Flatland";
                    newRegion.Startup = StartupType.Normal;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = true;
                    newRegion.ObjectCapacity = 50000;
                    newRegion.RegionPort = RegionPort;


                }
                if (regionPreset.StartsWith ("o", System.StringComparison.Ordinal)) {
                    // 'Openspace' setup
                    newRegion.RegionType = newRegion.RegionType + "Openspace";
                    //newRegion.RegionPort;            // use auto assigned port
                    if (regionTerrain.StartsWith ("a", System.StringComparison.Ordinal))
                        newRegion.RegionTerrain = "Aquatic";
                    else
                        newRegion.RegionTerrain = "Grassland";
                    newRegion.Startup = StartupType.Medium;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = true;
                    newRegion.ObjectCapacity = 750;
                    newRegion.RegionSettings.AgentLimit = 10;
                    newRegion.RegionSettings.AllowLandJoinDivide = false;
                    newRegion.RegionSettings.AllowLandResell = false;
                }
                if (regionPreset.StartsWith ("h", System.StringComparison.Ordinal)) {
                    // 'Homestead' setup
                    newRegion.RegionType = newRegion.RegionType + "Homestead";
                    //info.RegionPort;            // use auto assigned port
                    newRegion.RegionTerrain = "Homestead";
                    newRegion.Startup = StartupType.Medium;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = true;
                    newRegion.ObjectCapacity = 3750;
                    newRegion.RegionSettings.AgentLimit = 20;
                    newRegion.RegionSettings.AllowLandJoinDivide = false;
                    newRegion.RegionSettings.AllowLandResell = false;
                }

                if (regionPreset.StartsWith ("f", System.StringComparison.Ordinal)) {
                    // 'Full Region' setup
                    newRegion.RegionType = newRegion.RegionType + "Full Region";
                    //newRegion.RegionPort;            // use auto assigned port
                    newRegion.RegionTerrain = regionTerrain;
                    newRegion.Startup = StartupType.Normal;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = true;
                    newRegion.ObjectCapacity = 15000;
                    newRegion.RegionSettings.AgentLimit = 100;
                    if (newRegion.RegionType.StartsWith ("M", System.StringComparison.Ordinal))                           // defaults are 'true'
                    {
                        newRegion.RegionSettings.AllowLandJoinDivide = false;
                        newRegion.RegionSettings.AllowLandResell = false;
                    }
                }

                if (regionLoadTerrain.Length > 0) {
                    // we are loading terrain from a file... handled later
                    newRegion.RegionTerrain = "Custom";
                }

                // TODO: !!! Assumes everything is local for now !!!  
                if (requestParameters.ContainsKey ("NewRegion")) {
                    ISceneManager scenemanager = webInterface.Registry.RequestModuleInterface<ISceneManager> ();
                    if (scenemanager.CreateRegion (newRegion)) {
                        response = "<h3>Successfully created region</h3>" +
                            "<script language=\"javascript\">" +
                            "setTimeout(function() {window.location.href = \"/?page=region_manager\";}, 2000);" +
                            "</script>";
                        return null;
                    }
                    response = "<h3>Error creating this region.</h3>";
                    return null;

                    /* not required??
                    IGridRegisterModule gridRegister = webInterface.Registry.RequestModuleInterface<IGridRegisterModule> ();
                        if (gridRegister != null) {
                            if (gridRegister.RegisterRegionWithGrid (null, true, false, null)) {

                                response = "<h3>Successfully created region</h3>" +
                                    "<script language=\"javascript\">" +
                                    "setTimeout(function() {window.location.href = \"/?page=region_manager\";}, 2000);" +
                                    "</script>";
                                return null;
                            }
                        }

                        response = "<h3> Error registering region with grid</h3>";
                    } else
                        response = "<h3>Error creating this region.</h3>";
                    return null;
                    */
                }

                // TODO:  This will not work yet  :)
                // update region details
                var infoConnector = Framework.Utilities.DataManager.RequestPlugin<IRegionInfoConnector> ();
                if (infoConnector != null) {
                    infoConnector.UpdateRegionInfo (newRegion);

                    response = "<h3>Region details updated.</h3>" +
                    "<script language=\"javascript\">" +
                    "setTimeout(function() {window.location.href = \"/?page=region_manager\";}, 2000);" +
                    "</script>";
                } else
                    response = "<h3>Unable to update Region details!</h3>" +
                    "<script language=\"javascript\">" +
                    "setTimeout(function() {window.location.href = \"/?page=region_manager\";}, 2000);" +
                    "</script>";
                return null;

            }
            #endregion 

            #region EditRegion
            // we have or need data
            if (httpRequest.Query.ContainsKey ("regionid")) {
                var region = gridService.GetRegionByUUID (null, UUID.Parse (httpRequest.Query ["regionid"].ToString ()));

                vars.Add ("RegionID", region.RegionID.ToString ());
                vars.Add ("RegionName", region.RegionName);

                UserAccount estateOwnerAccount = null;
                var estateOwner = UUID.Zero;
                var estateId = -1;

                IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();
                if (estateConnector != null) {
                    EstateSettings estate = estateConnector.GetEstateSettings (region.RegionID);
                    if (estate != null) {
                        estateId = (int)estate.EstateID;
                        estateOwner = estate.EstateOwner;
                    }
                    var accountService = webInterface.Registry.RequestModuleInterface<IUserAccountService> ();
                    if (accountService != null)
                        estateOwnerAccount = accountService.GetUserAccount (null, estate.EstateOwner);
                }

                vars.Add ("EstateList", WebHelpers.EstateSelections (webInterface.Registry, estateOwner.ToString (), estateId));
                vars.Add ("OwnerUUID", region.EstateOwner);
                vars.Add ("OwnerName", estateOwnerAccount != null ? estateOwnerAccount.Name : "No account found");

                vars.Add ("RegionLocX", region.RegionLocX / Constants.RegionSize);
                vars.Add ("RegionLocY", region.RegionLocY / Constants.RegionSize);
                vars.Add ("RegionSizeX", region.RegionSizeX);
                vars.Add ("RegionSizeY", region.RegionSizeY);
                vars.Add ("RegionPort", region.InternalPort.ToString ());
                vars.Add ("RegionType", WebHelpers.RegionTypeArgs (translator, region.RegionType));
                vars.Add ("RegionPresetType", WebHelpers.RegionPresetArgs (translator, region.RegionType));
                vars.Add ("RegionTerrain", WebHelpers.RegionTerrainArgs (translator, region.RegionTerrain));

                // TODO:  This will not work yet  :)
                bool switches = false;
                var infoConnector = Framework.Utilities.DataManager.RequestPlugin<IRegionInfoConnector> ();
                if (infoConnector != null) {
                    var regionInfo = infoConnector.GetRegionInfo (region.RegionID);
                    if (regionInfo != null) {
                        vars.Add ("RegionCapacity", regionInfo.ObjectCapacity.ToString ());
                        vars.Add ("RegionVisibility", WebHelpers.YesNoSelection (translator, regionInfo.SeeIntoThisSimFromNeighbor));
                        vars.Add ("RegionInfinite", WebHelpers.YesNoSelection (translator, regionInfo.InfiniteRegion));
                        vars.Add ("RegionDelayStartup", WebHelpers.RegionStartupSelection (translator, regionInfo.Startup));
                    }
                }
                if (!switches) {
                    vars.Add ("RegionCapacity", "Unknown");
                    vars.Add ("RegionVisibility", WebHelpers.YesNoSelection (translator, true));
                    vars.Add ("RegionInfinite", WebHelpers.YesNoSelection (translator, true));
                    vars.Add ("RegionDelayStartup", WebHelpers.RegionStartupSelection (translator, StartupType.Normal)); // normal startup
                }


                //vars.Add ("RegionOnline",
                //    (region.Flags & (int)RegionFlags.RegionOnline) == (int)RegionFlags.RegionOnline
                //          ? translator.GetTranslatedString ("Online")
                //          : translator.GetTranslatedString ("Offline"));

                IWebHttpTextureService webTextureService = webInterface.Registry.
                    RequestModuleInterface<IWebHttpTextureService> ();
                if (webTextureService != null && region.TerrainMapImage != UUID.Zero)
                    vars.Add ("RegionImageURL", webTextureService.GetTextureURL (region.TerrainMapImage));
                else
                    vars.Add ("RegionImageURL", "images/icons/no_picture.jpg");
                vars.Add ("Submit", translator.GetTranslatedString ("SaveUpdates"));

            }
            #endregion

            vars.Add ("RegionPresets", WebHelpers.RegionSelections (webInterface.Registry));

            // Labels
            vars.Add ("UserName", user.Name);
            vars.Add ("RegionManagerText", translator.GetTranslatedString ("MenuRegionManager"));
            vars.Add ("RegionNameText", translator.GetTranslatedString ("RegionNameText"));
            vars.Add ("RegionLocationText", translator.GetTranslatedString ("RegionLocationText"));
            vars.Add ("RegionSizeText", translator.GetTranslatedString ("RegionSizeText"));
            vars.Add ("RegionTypeText", translator.GetTranslatedString ("RegionTypeText"));
            vars.Add ("RegionPresetText", translator.GetTranslatedString ("RegionPresetText"));
            vars.Add ("RegionTerrainText", translator.GetTranslatedString ("RegionTerrainText"));
            vars.Add ("EstateText", translator.GetTranslatedString ("EstateText"));
            vars.Add ("RegionPortText", translator.GetTranslatedString ("RegionPortText"));
            vars.Add ("RegionDelayStartupText", translator.GetTranslatedString ("RegionDelayStartupText"));
            vars.Add ("RegionVisibilityText", translator.GetTranslatedString ("RegionVisibilityText"));
            vars.Add ("RegionInfiniteText", translator.GetTranslatedString ("RegionInfiniteText"));
            vars.Add ("RegionCapacityText", translator.GetTranslatedString ("RegionCapacityText"));
            vars.Add ("Cancel", translator.GetTranslatedString ("Cancel"));
            vars.Add ("InfoMessage", "");

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
