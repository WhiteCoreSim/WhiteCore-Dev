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
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class AgentRegionsPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/webprofile/regions.html"
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
            response = null;
            var vars = new Dictionary<string, object>();
            var regionslist = new List<Dictionary<string, object>>();
            UserAccount account = null;

            // future use // uint amountPerQuery = 10;
            string noDetails = translator.GetTranslatedString ("NoDetailsText");

            if (httpRequest.Query.ContainsKey("userid"))
            {
                List <UUID> scopeUUID = new List<UUID>();
                string userid = httpRequest.Query["userid"].ToString();
                UUID userUUID = UUID.Parse (userid);
                scopeUUID.Add (userUUID);
                  
                account = webInterface.Registry.RequestModuleInterface<IUserAccountService>().
                    GetUserAccount(null, userUUID);

                IGridService gridService = webInterface.Registry.RequestModuleInterface<IGridService>();
                IWebHttpTextureService webTextureService = webInterface.Registry.RequestModuleInterface<IWebHttpTextureService>();

                var regions = gridService.GetRegionsByName(scopeUUID, "", null, null);
                // TODO: Searching using the user UUID scope does not appear to work  -greythane- 20141020
                if (regions != null)
                {
                    noDetails = "";

                    foreach (var region in regions)
                    {
                        if (region.EstateOwner != userUUID)         // not this one...
                            continue;

                        string info;
                        info = (region.RegionArea < 1000000) ? region.RegionArea + " m2" : (region.RegionArea / 1000000) + " km2";
                        info = info + ", " + region.RegionTerrain;

                        var regionData = new Dictionary<string, object> ();
                        regionData.Add("RegionName", region.RegionName);
                        regionData.Add("RegionLocX", region.RegionLocX / Constants.RegionSize);
                        regionData.Add("RegionLocY", region.RegionLocY / Constants.RegionSize);
                        regionData.Add("RegionInfo", info);
                        regionData.Add("RegionStatus", region.IsOnline ? "yes" : "no");
                        regionData.Add("RegionID", region.RegionID);

                        if (webTextureService != null && region.TerrainMapImage != UUID.Zero)
                            regionData.Add("RegionImageURL", webTextureService.GetTextureURL(region.TerrainMapImage));
                        else
                            regionData.Add("RegionImageURL", "../images/icons/no_terrain.jpg");

                        regionslist.Add (regionData);
                    }
                } 
            }

            // provide something..
            if (regionslist.Count == 0)
            {
                regionslist.Add (new Dictionary<string, object> {
                    {"RegionName", translator.GetTranslatedString ("NoDetailsText")},
                    {"RegionLocX", ""},
                    {"RegionLocY", ""},
                    {"RegionInfo", ""},
                    {"RegionStatus", ""},
                    {"RegionID", ""},
                    {"RegionImageURL", "../images/icons/no_terrain.jpg"}
                    });
             }

            vars.Add("NoDetailsText", noDetails);
            if (account != null)
                vars.Add ("UserName", account.Name);
            else
                vars.Add ("UserName", "");
            
            vars.Add ("RegionListText", translator.GetTranslatedString ("RegionListText"));
            vars.Add ("RegionList", regionslist);
            vars.Add ("RegionNameText", translator.GetTranslatedString ("RegionNameText"));
            vars.Add ("RegionText", translator.GetTranslatedString ("Region"));
            vars.Add ("RegionLocXText", translator.GetTranslatedString ("RegionLocXText"));
            vars.Add ("RegionLocYText", translator.GetTranslatedString ("RegionLocYText"));
            vars.Add ("RegionOnlineText", translator.GetTranslatedString ("Online"));
            vars.Add ("RegionMoreInfo", translator.GetTranslatedString ("RegionMoreInfo"));
            vars.Add ("MoreInfoText", translator.GetTranslatedString ("MoreInfoText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            httpResponse.ContentType = "text/html";
            //text = "";
            text = File.ReadAllText("html/webprofile/index.html");
                      return false;
        }
    }
}