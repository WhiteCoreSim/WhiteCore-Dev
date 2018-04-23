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
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class WorldMain : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/world.html"
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
            // worldview
            var webTextureService = webInterface.Registry.RequestModuleInterface<IWebHttpTextureService> ();

            var worldViewConfig =
	                webInterface.Registry.RequestModuleInterface<ISimulationBase> ().ConfigSource.Configs ["WorldViewModule"];
            bool worldViewEnabled = false;
            if (worldViewConfig != null)
                    worldViewEnabled = worldViewConfig.GetBoolean ("Enabled", true);

            // get region list
            List<Dictionary<string, object>> RegionListVars = new List<Dictionary<string, object>> ();
            var sortBy = new Dictionary<string, bool> ();
            sortBy.Add ("RegionName", true);
            var regionData = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            var regions = regionData.Get (0, RegionFlags.Hyperlink | RegionFlags.Foreign | RegionFlags.Hidden,
                                          null, 10, sortBy);
            foreach (var region in regions) {
                string info;
                info = (region.RegionArea < 1000000) ? region.RegionArea + " m2" : (region.RegionArea / 1000000) + " km2";
                info = info + ", " + region.RegionTerrain;

                var regionviewURL = "";
                if (webTextureService != null && worldViewEnabled && region.IsOnline)
                    regionviewURL = webTextureService.GetRegionWorldViewURL (region.RegionID);

                RegionListVars.Add (new Dictionary<string, object> {
                    { "RegionLocX", region.RegionLocX / Constants.RegionSize },
                    { "RegionLocY", region.RegionLocY / Constants.RegionSize },
                    { "RegionName", region.RegionName },
                    { "RegionInfo", info},
                    { "RegionStatus", region.IsOnline ? "Online" : "Offline"},
                    { "RegionID", region.RegionID },
                    { "RegionURI", region.RegionURI },
                    { "RegionWorldViewURL", regionviewURL}
                });
            }

            vars.Add ("RegionList", RegionListVars);
            vars.Add ("RegionText", translator.GetTranslatedString ("Region"));
            vars.Add ("MoreInfoText", translator.GetTranslatedString ("MoreInfoText"));

            vars.Add ("MainServerURL", webInterface.GridURL);
            vars.Add ("WorldMap", translator.GetTranslatedString ("WorldMap"));
            vars.Add ("WorldMapText", translator.GetTranslatedString ("WorldMapText"));

            var settings = webInterface.GetWebUISettings ();
            vars.Add ("MapCenterX", settings.MapCenter.X);
            vars.Add ("MapCenterY", settings.MapCenter.Y);

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
