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
    public class RegionBoxPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/welcomescreen/region_box.html",
                               "html/region_list.html"
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

            List<Dictionary<string, object>> RegionListVars = new List<Dictionary<string, object>>();
            var sortBy = new Dictionary<string, bool>();
            if (httpRequest.Query.ContainsKey("region"))
                sortBy.Add(httpRequest.Query["region"].ToString(), true);
            else if (httpRequest.Query.ContainsKey("Order"))
                sortBy.Add(httpRequest.Query["Order"].ToString(), true);

            uint amountPerQuery = 50;
            int start = httpRequest.Query.ContainsKey("Start") ? int.Parse(httpRequest.Query["Start"].ToString()) : 0;
            uint count = Framework.Utilities.DataManager.RequestPlugin<IRegionData>().Count((RegionFlags) 0,
                                                                                    RegionFlags.Hyperlink |
                                                                                    RegionFlags.Foreign |
                                                                                    RegionFlags.Hidden);
            int maxPages = (int) (count/amountPerQuery) - 1;

            if (start == -1)
                start = (int) (maxPages < 0 ? 0 : maxPages);

            vars.Add("CurrentPage", start);
            vars.Add("NextOne", start + 1 > maxPages ? start : start + 1);
            vars.Add("BackOne", start - 1 < 0 ? 0 : start - 1);

            var regions = Framework.Utilities.DataManager.RequestPlugin<IRegionData>().Get((RegionFlags) 0,
                                                                                   RegionFlags.Hyperlink |
                                                                                   RegionFlags.Foreign |
                                                                                   RegionFlags.Hidden,
                                                                                   (uint) (start*amountPerQuery),
                                                                                   amountPerQuery, sortBy);
            foreach (var region in regions)
            {
                string info;
                info = (region.RegionArea < 1000000) ? region.RegionArea + " m2" : (region.RegionArea / 1000000) + " km2";
                info = info + ", " +region.RegionTerrain;

                RegionListVars.Add (new Dictionary<string, object> {
                    { "RegionLocX", region.RegionLocX / Constants.RegionSize },
                    { "RegionLocY", region.RegionLocY / Constants.RegionSize },
                    { "RegionName", region.RegionName },
                    { "RegionInfo", info},
                    { "RegionStatus", region.IsOnline ? "yes" : "no"},
                    { "RegionID", region.RegionID },
                    { "RegionURI", region.RegionURI }
                });
            }

            vars.Add("RegionList", RegionListVars);
            vars.Add("RegionText", translator.GetTranslatedString("Region"));


            vars.Add("RegionNameText", translator.GetTranslatedString("RegionNameText"));
            vars.Add("RegionLocXText", translator.GetTranslatedString("RegionLocXText"));
            vars.Add("RegionLocYText", translator.GetTranslatedString("RegionLocYText"));
            vars.Add ("RegionOnlineText", translator.GetTranslatedString ("Online"));
            vars.Add("SortByLocX", translator.GetTranslatedString("SortByLocX"));
            vars.Add("SortByLocY", translator.GetTranslatedString("SortByLocY"));
            vars.Add("SortByName", translator.GetTranslatedString("SortByName"));
            vars.Add("RegionListText", translator.GetTranslatedString("RegionListText"));
            vars.Add("FirstText", translator.GetTranslatedString("FirstText"));
            vars.Add("BackText", translator.GetTranslatedString("BackText"));
            vars.Add("NextText", translator.GetTranslatedString("NextText"));
            vars.Add("LastText", translator.GetTranslatedString("LastText"));
            vars.Add("CurrentPageText", translator.GetTranslatedString("CurrentPageText"));
            vars.Add("MoreInfoText", translator.GetTranslatedString("MoreInfoText"));
            vars.Add("RegionMoreInfo", translator.GetTranslatedString("RegionMoreInfo"));
            vars.Add ("MainServerURL", webInterface.GridURL);

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}