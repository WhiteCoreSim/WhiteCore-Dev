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
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class UserRegionManagerPage : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/user/region_manager.html"
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

            var regionListVars = new List<Dictionary<string, object>> ();
            var user = Authenticator.GetAuthentication (httpRequest);


            var regionData = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            var regions = regionData.GetOwnerRegions (user.PrincipalID);
            if (regions.Count > 0) {
                foreach (var region in regions) {
                    string info;
                    info = (region.RegionArea < 1000000) ? region.RegionArea + " m2" : (region.RegionArea / 1000000) + " km2";
                    info = info + ", " + region.RegionTerrain;

                    regionListVars.Add (new Dictionary<string, object> {
                        { "RegionLocX", region.RegionLocX / Constants.RegionSize },
                        { "RegionLocY", region.RegionLocY / Constants.RegionSize },
                        { "RegionName", region.RegionName },
                        { "RegionInfo", info},
                        { "RegionStatus", WebHelpers.YesNo(translator, region.IsOnline)},
                        { "RegionID", region.RegionID },
                        { "RegionURI", region.RegionURI }
                    });
                }
            } else {
                regionListVars.Add (new Dictionary<string, object> {
                    { "RegionLocX", "" },
                    { "RegionLocY", "" },
                    { "RegionName", "" },
                    { "RegionInfo", translator.GetTranslatedString("NoDetailsText")},
                    { "RegionStatus", ""},
                    { "RegionID", "" },
                    { "RegionURI", "" }
                });

            }

            vars.Add ("RegionList", regionListVars);

            // labels
            vars.Add ("UserName", user.Name);
            vars.Add ("RegionsText", translator.GetTranslatedString ("MenuRegionsTitle"));
            vars.Add ("AddRegionText", translator.GetTranslatedString ("AddRegionText"));
            vars.Add ("EditRegionText", translator.GetTranslatedString ("EditText"));
            vars.Add ("RegionListText", translator.GetTranslatedString ("RegionListText"));
            vars.Add ("RegionText", translator.GetTranslatedString ("Region"));


            vars.Add ("RegionNameText", translator.GetTranslatedString ("RegionNameText"));
            vars.Add ("RegionLocXText", translator.GetTranslatedString ("RegionLocXText"));
            vars.Add ("RegionLocYText", translator.GetTranslatedString ("RegionLocYText"));
            vars.Add ("RegionOnlineText", translator.GetTranslatedString ("Online"));
            //vars.Add ("MainServerURL", webInterface.GridURL);

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
