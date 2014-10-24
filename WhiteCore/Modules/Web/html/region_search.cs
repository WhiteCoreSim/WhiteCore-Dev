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
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class RegionSearchPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/region_search.html"
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

            uint amountPerQuery = 10;
            string noDetails = translator.GetTranslatedString ("NoDetailsText");

            if (requestParameters.ContainsKey("Submit"))
            {

                IGridService gridService = webInterface.Registry.RequestModuleInterface<IGridService>();
                string regionname = requestParameters["regionname"].ToString();
                int start = httpRequest.Query.ContainsKey("Start")
                                ? int.Parse(httpRequest.Query["Start"].ToString())
                                : 0;
                uint count = gridService.GetRegionsByNameCount(null, regionname);
                int maxPages = (int) (count/amountPerQuery) - 1;

                if (start == -1)
                    start = (int) (maxPages < 0 ? 0 : maxPages);

                vars.Add("CurrentPage", start);
                vars.Add("NextOne", start + 1 > maxPages ? start : start + 1);
                vars.Add("BackOne", start - 1 < 0 ? 0 : start - 1);

                var regions = gridService.GetRegionsByName(null, regionname, (uint) start, amountPerQuery);
                if (regions != null)
                {
                    noDetails = "";

                    foreach (var region in regions)
                    {
                        string info;
                        info = (region.RegionArea < 1000000) ? region.RegionArea + " m2" : (region.RegionArea / 1000000) + " km2";
                        info = info + ", " + region.RegionTerrain;

                        regionslist.Add (new Dictionary<string, object> {
                            { "RegionName", region.RegionName },
                            { "RegionLocX", region.RegionLocX / Constants.RegionSize },
                            { "RegionLocY", region.RegionLocY / Constants.RegionSize },
                            { "RegionInfo", info },
                            { "RegionStatus", region.IsOnline ? "yes" : "no" },
                            { "RegionID", region.RegionID }
                        });
                    }
                }

            }
            else
            {
                vars.Add("CurrentPage", 0);
                vars.Add("NextOne", 0);
                vars.Add("BackOne", 0);
            }

            vars.Add("NoDetailsText", noDetails);
            vars.Add ("RegionsList", regionslist);
            vars.Add ("RegionSearchText", translator.GetTranslatedString ("RegionSearchText"));
            vars.Add ("SearchForRegionText", translator.GetTranslatedString ("SearchForRegionText"));
            vars.Add ("RegionNameText", translator.GetTranslatedString ("RegionNameText"));
            vars.Add ("RegionLocXText", translator.GetTranslatedString ("RegionLocXText"));
            vars.Add ("RegionLocYText", translator.GetTranslatedString ("RegionLocYText"));
            vars.Add ("RegionOnlineText", translator.GetTranslatedString ("Online"));

            vars.Add ("Search", translator.GetTranslatedString ("Search"));

            vars.Add ("FirstText", translator.GetTranslatedString ("FirstText"));
            vars.Add ("BackText", translator.GetTranslatedString ("BackText"));
            vars.Add ("NextText", translator.GetTranslatedString ("NextText"));
            vars.Add ("LastText", translator.GetTranslatedString ("LastText"));
            vars.Add ("CurrentPageText", translator.GetTranslatedString ("CurrentPageText"));

            vars.Add ("SearchResultForRegionText", translator.GetTranslatedString ("SearchResultForRegionText"));
            vars.Add ("RegionMoreInfo", translator.GetTranslatedString ("RegionMoreInfo"));
            vars.Add ("MoreInfoText", translator.GetTranslatedString ("MoreInfoText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}