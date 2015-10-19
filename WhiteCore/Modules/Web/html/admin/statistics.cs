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

using WhiteCore.Framework.Servers.HttpServer.Implementation;
using System.Collections.Generic;
using WhiteCore.Framework.DatabaseInterfaces;
using System.Linq;

namespace WhiteCore.Modules.Web
{
    public class UserStatisticsPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/admin/statistics.html"
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
            IUserStatsDataConnector dc = Framework.Utilities.DataManager.RequestPlugin<IUserStatsDataConnector>();
            var vars = new Dictionary<string, object>();

            if (dc == null)
            {
                response = "Sorry... Statistics information is not available";
                return null;

            }

            // Clear statistics
            if (requestParameters.ContainsKey("Submit") &&
                requestParameters["Submit"].ToString() == "SubmitClearStats")
            {
                dc.RemoveAllSessions();
                response = "Statistics cleared";
                return null;
            }

            // normal stats...
            var viewerList = new List<Dictionary<string, object>>();
            var client_viewers = dc.ViewerUsage ();
            foreach (var vclient in client_viewers)
                viewerList.Add( new Dictionary<string, object> {
                    { "ViewerName", vclient.Key },
                    { "ViewerCount", vclient.Value }
                });

            var gpuList = new List<Dictionary<string, object>>();
            gpuList.Add( new Dictionary<string, object> {
                {"GPUType","ATI"},
                {"GPUCount", dc.GetCount ("s_gpuvendor", new KeyValuePair<string, object> ("s_gpuvendor", "ATI"))} 
            });
            gpuList.Add( new Dictionary<string, object> {
                {"GPUType", "NVIDIA"},
                {"GPUCount", dc.GetCount ("s_gpuvendor", new KeyValuePair<string, object> ("s_gpuvendor", "NVIDIA"))}
            });
            gpuList.Add( new Dictionary<string, object> {
                {"GPUType", "Intel"},
                {"GPUCount", dc.GetCount ("s_gpuvendor", new KeyValuePair<string, object> ("s_gpuvendor", "Intel"))}
            });

            var fps = dc.Get ("fps").ConvertAll<float> ((s) => float.Parse (s));
            var runtime = dc.Get ("run_time").ConvertAll<float> ((s) => float.Parse (s));
            var visited = dc.Get ("regions_visited").ConvertAll<int> ((s) => int.Parse (s));
            var memoryUsage = dc.Get ("mem_use").ConvertAll<int> ((s) => int.Parse (s));
            var pingTime = dc.Get ("ping").ConvertAll<float> ((s) => float.Parse (s));
            var agentsInView = dc.Get ("agents_in_view").ConvertAll<int> ((s) => int.Parse (s));



            // data
            vars.Add("ViewersList",viewerList);
            vars.Add("GPUList",gpuList);
            vars.Add("FPS", fps.Average());
            vars.Add("RunTime", runtime.Average());
            vars.Add("RegionsVisited", visited.Average());
            vars.Add("MemoryUseage", memoryUsage.Average()/1000);
            vars.Add("PingTime", pingTime.Average());
            vars.Add("AgentsInView", agentsInView.Average());

            // labels
            vars.Add("StatisticsText", translator.GetTranslatedString("StatisticsText"));
            vars.Add("ViewersText", translator.GetTranslatedString("ViewersText"));
            vars.Add("GPUText", translator.GetTranslatedString("GPUText"));
            vars.Add("PerformanceText", translator.GetTranslatedString("PerformanceText"));
            vars.Add("FPSText",translator.GetTranslatedString("FPSText"));
            vars.Add("RunTimeText", translator.GetTranslatedString("RunTimeText"));
            vars.Add("RegionsVisitedText", translator.GetTranslatedString("RegionsVisitedText"));
            vars.Add("MemoryUseageText", translator.GetTranslatedString("MemoryUseageText"));
            vars.Add("PingTimeText", translator.GetTranslatedString("PingTimeText"));
            vars.Add("AgentsInViewText", translator.GetTranslatedString("AgentsInViewText"));

            vars.Add("ClearStatsText", translator.GetTranslatedString("ClearStatsText"));

                    
            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
