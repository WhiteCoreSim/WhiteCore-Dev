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
using WhiteCore.Framework.Utilities;
using WhiteCore.Framework.SceneInfo;
using OpenMetaverse;
using System.Collections.Generic;
using System.IO;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;


namespace WhiteCore.Modules.Web
{
    public class RegionManagerPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                {
                    "html/admin/region_manager.html"
                    //"html/regionprofile/base.html",
                    //"html/regionprofile/"
                };
            }
        }

        public bool RequiresAuthentication
        {
            get { return true; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return true; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();
            var gridService = webInterface.Registry.RequestModuleInterface<IGridService> ();


            if (requestParameters.ContainsKey("Submit"))
            {

                string RegionServerURL = requestParameters["RegionServerURL"].ToString();
                // required
                if (RegionServerURL == "")  {
                    response = "<h3>" + translator.GetTranslatedString ("RegionServerURLError") + "</h3>";   
                    return null;
                }

                string RegionName = requestParameters["RegionName"].ToString();
                //string OwnerUUID = requestParameters["OwnerUUID"].ToString();
                string RegionLocX = requestParameters["RegionLocX"].ToString();
                string RegionLocY = requestParameters["RegionLocY"].ToString();
                string RegionSizeX = requestParameters["RegionSizeX"].ToString();
                string RegionSizeY = requestParameters["RegionSizeY"].ToString();

                string RegionType = requestParameters["RegionType"].ToString();
                string RegionPresetType = requestParameters["RegionPresetType"].ToString();
                string RegionTerrain = requestParameters["RegionTerrain"].ToString();

                string RegionLoadTerrain = requestParameters.ContainsKey("RegionLoadTerrain")
                    ? requestParameters["RegionLoadTerrain"].ToString()
                    : "";
                //bool ToSAccept = requestParameters.ContainsKey("ToSAccept") &&
                //    requestParameters["ToSAccept"].ToString() == "Accepted";

               // string UserType = requestParameters.ContainsKey("UserType")         // only admins can set membership
               //     ? requestParameters ["UserType"].ToString ()
               //     : "Resident";


                // a bit of idiot proofing
                if (RegionName == "")  {
                    response = "<h3>" + translator.GetTranslatedString ("RegionNameError") + "</h3>";   
                    return null;
                }
                if ( (RegionLocX == "") || (RegionLocY == "") )
                {
                    response = "<h3>" + translator.GetTranslatedString ("RegionLocationError") + "</h3>";   
                    return null;
                } 

                // so far so good...
                // build the new region details
                int RegionPort = int.Parse (requestParameters ["RegionPort"].ToString ());

                var newRegion = new RegionInfo();

                newRegion.RegionName = RegionName;
                newRegion.RegionType = RegionType;
                newRegion.RegionLocX = int.Parse (RegionLocX);
                newRegion.RegionLocY = int.Parse (RegionLocY);
                newRegion.RegionSizeX = int.Parse (RegionSizeX);
                newRegion.RegionSizeY = int.Parse (RegionSizeY);

                newRegion.RegionPort = RegionPort;
                newRegion.SeeIntoThisSimFromNeighbor = true;
                newRegion.InfiniteRegion = false;
                newRegion.ObjectCapacity = 50000;
                newRegion.Startup = StartupType.Normal;

                var regionPreset = RegionPresetType.ToLower ();
                if (regionPreset.StartsWith ("c"))
                {
                    newRegion.RegionPort = int.Parse( requestParameters["RegionPort"].ToString() );
                    newRegion.SeeIntoThisSimFromNeighbor = (requestParameters["RegionVisibility"].ToString().ToLower() == "yes");
                    newRegion.InfiniteRegion = (requestParameters["RegionInfinite"].ToString().ToLower() == "yes");
                    newRegion.ObjectCapacity = int.Parse( requestParameters["RegionCapacity"].ToString() );

                    string delayStartup = requestParameters["RegionDelayStartup"].ToString();
                    newRegion.Startup = delayStartup.StartsWith ("n") ? StartupType.Normal : StartupType.Medium;

                }

                if (regionPreset.StartsWith("w"))
                {
                    // 'standard' setup
                    newRegion.RegionType = newRegion.RegionType + "Whitecore";                   
                    //info.RegionPort;            // use auto assigned port
                    newRegion.RegionTerrain = "Flatland";
                    newRegion.Startup = StartupType.Normal;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = false;
                    newRegion.ObjectCapacity = 50000;
                    newRegion.RegionPort = RegionPort;
 

                }
                if (regionPreset.StartsWith("o"))       
                {
                    // 'Openspace' setup
                    newRegion.RegionType = newRegion.RegionType + "Openspace";                   
                    //newRegion.RegionPort;            // use auto assigned port
                    if (RegionTerrain.StartsWith("a"))
                        newRegion.RegionTerrain = "Aquatic";
                    else
                        newRegion.RegionTerrain = "Grassland";
                    newRegion.Startup = StartupType.Medium;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = false;
                    newRegion.ObjectCapacity = 750;
                    newRegion.RegionSettings.AgentLimit = 10;
                    newRegion.RegionSettings.AllowLandJoinDivide = false;
                    newRegion.RegionSettings.AllowLandResell = false;
                }
                if (regionPreset.StartsWith("h"))       
                {
                    // 'Homestead' setup
                    newRegion.RegionType = newRegion.RegionType + "Homestead";                   
                    //info.RegionPort;            // use auto assigned port
                    newRegion.RegionTerrain = "Homestead";
                    newRegion.Startup = StartupType.Medium;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = false;
                    newRegion.ObjectCapacity = 3750;
                    newRegion.RegionSettings.AgentLimit = 20;
                    newRegion.RegionSettings.AllowLandJoinDivide = false;
                    newRegion.RegionSettings.AllowLandResell = false;
                }

                if (regionPreset.StartsWith("f"))       
                {
                    // 'Full Region' setup
                    newRegion.RegionType = newRegion.RegionType + "Full Region";                   
                    //newRegion.RegionPort;            // use auto assigned port
                    newRegion.RegionTerrain = RegionTerrain;
                    newRegion.Startup = StartupType.Normal;
                    newRegion.SeeIntoThisSimFromNeighbor = true;
                    newRegion.InfiniteRegion = false;
                    newRegion.ObjectCapacity = 15000;
                    newRegion.RegionSettings.AgentLimit = 100;
                    if (newRegion.RegionType.StartsWith ("M"))                           // defaults are 'true'
                    {
                        newRegion.RegionSettings.AllowLandJoinDivide = false;
                        newRegion.RegionSettings.AllowLandResell = false;
                    }
                }

                if (RegionLoadTerrain.Length > 0)
                {
                    // we are loading terrain from a file... handled later
                    newRegion.RegionTerrain = "Custom";
                }

                /* Disabled as this is a worl=k in progress and will break with the current scenemanager (Dec 5 - greythane-
                // TODO: !!! Assumes everything is local for now !!!               
                ISceneManager scenemanager = webInterface.Registry.RequestModuleInterface<ISceneManager> ();
                if (scenemanager.CreateRegion(newRegion))
                {   
                    IGridRegisterModule gridRegister = webInterface.Registry.RequestModuleInterface<IGridRegisterModule>();
                    if( gridRegister.RegisterRegionWithGrid(null, true, false, null)) 
                    {
 
                        response = "<h3>Successfully created region, redirecting to main page</h3>" +
                            "<script language=\"javascript\">" +
                            "setTimeout(function() {window.location.href = \"index.html\";}, 3000);" +
                            "</script>";
                    }
                    else
//                        response = "<h3>" + error + "</h3>";
                            response = "<h3> Error registering region with grid</h3>";
                }
                else
*/
                response = "<h3>Error creating this region.</h3>";
                return null;
            }

            // we have or need data
            if (httpRequest.Query.ContainsKey ("regionid"))
            {
                var region = gridService.GetRegionByUUID (null, UUID.Parse (httpRequest.Query ["regionid"].ToString ()));

                IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();
                EstateSettings estate = estateConnector.GetEstateSettings (region.RegionID);

                vars.Add ("RegionName", region.RegionName);
                vars.Add ("OwnerUUID", estate.EstateOwner);

                var estateOwnerAccount = webInterface.Registry.RequestModuleInterface<IUserAccountService> ().
                    GetUserAccount (null, estate.EstateOwner);
                vars.Add ("OwnerName", estateOwnerAccount == null ? "No account found" : estateOwnerAccount.Name);
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

                IWebHttpTextureService webTextureService = webInterface.Registry.
                    RequestModuleInterface<IWebHttpTextureService> ();
                if (webTextureService != null && region.TerrainMapImage != UUID.Zero)
                    vars.Add ("RegionImageURL", webTextureService.GetTextureURL (region.TerrainMapImage));
                else
                    vars.Add ("RegionImageURL", "images/icons/no_picture.jpg");
            } 
            else
            {
                // default values

                // check for user name seed
                string[] m_regionNameSeed = null;
                /*IConfig regionConfig =
                    webInterface.Registry.RequestModuleInterface<ISimulationBase>().ConfigSource.Configs["LoginService"];

                 if (loginServerConfig != null)
                {
                    string userNameSeed = loginServerConfig.GetString ("UserNameSeed", "");
                    if (userNameSeed != "")
                        m_userNameSeed = userNameSeed.Split (',');
                }
*/
                Utilities.MarkovNameGenerator rNames = new Utilities.MarkovNameGenerator();
                string regionName = rNames.FirstName (m_regionNameSeed == null ? Utilities.RegionNames: m_regionNameSeed, 3,7);
                vars.Add ("RegionName", regionName);

                //var scenemanager = webInterface.Registry.RequestModuleInterface<ISceneManager> ();
                var gconnector = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();
                var settings = gconnector.GetGeneric<WebUISettings>(UUID.Zero, "WebUISettings", "Settings");

                // get some current details
                //List<GridRegion> regions = gridService.GetRegionsByName(null, "", null,null);

//                var currentInfo = scenemanager.FindCurrentRegionInfo ();
                Dictionary<string, int> currentInfo = null;
                if (currentInfo != null)
                {
                    vars.Add ("RegionLocX", currentInfo ["minX"] > 0 ? currentInfo ["minX"] : settings.MapCenter.X);
                    vars.Add ("RegionLocY", currentInfo ["minY"] > 0 ? currentInfo ["minY"] : settings.MapCenter.Y);
                    vars.Add("RegionPort", currentInfo ["port"] > 0 ? currentInfo ["port"] + 1 : 9000);
                } else
                {
                    vars.Add ("RegionLocX", settings.MapCenter.X);
                    vars.Add ("RegionLocY", settings.MapCenter.Y);
                    vars.Add("RegionPort", 9000);

                }

                   
                vars.Add ("RegionSizeX", Constants.RegionSize);
                vars.Add ("RegionSizeY", Constants.RegionSize);
                vars.Add ("RegionType", webInterface.RegionTypeArgs(translator));
                vars.Add ("RegionPresetType", webInterface.RegionPresetArgs(translator));
                vars.Add ("RegionTerrain", webInterface.RegionTerrainArgs(translator));
              
            }

                // Labels
                //vars.Add ("RegionInformationText", translator.GetTranslatedString ("RegionInformationText"));
            vars.Add ("RegionNameText", translator.GetTranslatedString ("RegionNameText"));
            vars.Add ("RegionLocationText", translator.GetTranslatedString ("RegionLocationText"));
            vars.Add ("RegionSizeText", translator.GetTranslatedString ("RegionSizeText"));
            vars.Add ("RegionTypeText", translator.GetTranslatedString ("RegionTypeText"));
            vars.Add ("RegionPresetText", translator.GetTranslatedString ("RegionPresetText"));
            vars.Add ("RegionTerrainText", translator.GetTranslatedString ("RegionTerrainText"));
            vars.Add ("OwnerNameText", translator.GetTranslatedString ("OwnerNameText"));
            vars.Add ("RegionPortText", translator.GetTranslatedString ("RegionPortText"));
            vars.Add ("RegionDelayStartupText", translator.GetTranslatedString ("RegionDelayStartupText"));
            vars.Add ("RegionVisibilityText", translator.GetTranslatedString ("RegionVisibilityText"));
            vars.Add ("RegionInfiniteText", translator.GetTranslatedString ("RegionInfiniteText"));
            vars.Add ("RegionCapacityText", translator.GetTranslatedString ("RegionCapacityText"));
            vars.Add ("Yes", translator.GetTranslatedString ("Yes"));
            vars.Add ("No", translator.GetTranslatedString ("No"));
            vars.Add("Accept", translator.GetTranslatedString("Accept"));
            vars.Add("Submit", translator.GetTranslatedString("Submit"));
            vars.Add("SubmitURL", "home.html");
            vars.Add("ErrorMessage", "");


         

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}