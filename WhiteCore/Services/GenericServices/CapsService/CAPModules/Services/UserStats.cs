﻿/*
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


using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nini.Config;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services
{
    public class StatsModule : ICapsServiceConnector
    {
        IRegionClientCapsService m_service;

        /// <summary>
        ///     Callback for a viewerstats cap
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <returns></returns>
        public byte[] ViewerStatsReport (string path, Stream request, OSHttpRequest httpRequest,
                                        OSHttpResponse httpResponse)
        {
            IUserStatsDataConnector dataConnector =
                Framework.Utilities.DataManager.RequestPlugin<IUserStatsDataConnector> ();

            OpenMetaverse.Messages.Linden.ViewerStatsMessage vsm =
                new OpenMetaverse.Messages.Linden.ViewerStatsMessage ();
            vsm.Deserialize ((OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request)));
            dataConnector.UpdateUserStats (vsm, m_service.AgentID, m_service.Region.RegionID);

            return MainServer.BlankResponse;
        }

        public void RegisterCaps (IRegionClientCapsService service)
        {
            m_service = service;
            service.AddStreamHandler ("ViewerStats",
                new GenericStreamHandler ("POST", service.CreateCAPS ("ViewerStats", ""), ViewerStatsReport));
        }

        public void DeregisterCaps ()
        {
            m_service.RemoveStreamHandler ("ViewerStats", "POST");
        }

        public void EnteringRegion ()
        {
        }
    }

    public class StatMetrics : IService
    {
        IUserStatsDataConnector statsData;

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup ()
        {
            statsData = Framework.Utilities.DataManager.RequestPlugin<IUserStatsDataConnector> ();

            if (statsData != null)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "user metrics", 
                    "user metrics", 
                    "Gives metrics on users", 
                    Metrics, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "clear user metrics",
                    "clear user metrics",
                    "Clear all saved user metrics", 
                    ClearMetrics, false, true);
            }
        }

        public void ClearMetrics (IScene scene, string[] cmd)
        {
            if (statsData != null)
                statsData.RemoveAllSessions ();
        }

        public void Metrics (IScene scene, string[] cmd)
        {
            if (statsData != null)
            {
                MainConsole.Instance.Info ("");
                var client_viewers = statsData.ViewerUsage ();
                MainConsole.Instance.Info ("Viewer usage:");
                foreach (var vclient in client_viewers)
                    MainConsole.Instance.CleanInfo (vclient.Key + ": " + vclient.Value);


                MainConsole.Instance.CleanInfo ("");
                MainConsole.Instance.CleanInfo ("Graphics cards:");
                MainConsole.Instance.CleanInfo (
                    string.Format (
                        "Graphic cards: {0} logins have used ATI, {1} logins have used NVIDIA, {2} logins have used Intel graphics",
                        statsData.GetCount ("s_gpuvendor", new KeyValuePair<string, object> ("s_gpuvendor", "ATI")),
                        statsData.GetCount ("s_gpuvendor", new KeyValuePair<string, object> ("s_gpuvendor", "NVIDIA")),
                        statsData.GetCount ("s_gpuvendor", new KeyValuePair<string, object> ("s_gpuvendor", "Intel"))));

                MainConsole.Instance.CleanInfo ("");
                MainConsole.Instance.CleanInfo ("Performance:");
                List<float> fps = statsData.Get ("fps").ConvertAll<float> ((s) => float.Parse (s));
                if (fps.Count > 0)
                    MainConsole.Instance.CleanInfo (string.Format ("Average fps: {0}", fps.Average ()));

                List<float> run_time = statsData.Get ("run_time").ConvertAll<float> ((s) => float.Parse (s));
                if (run_time.Count > 0)
                    MainConsole.Instance.CleanInfo (string.Format ("Average viewer run time: {0}", run_time.Average ()));

                List<int> regions_visited = statsData.Get ("regions_visited").ConvertAll<int> ((s) => int.Parse (s));
                if (regions_visited.Count > 0)
                    MainConsole.Instance.CleanInfo (string.Format ("Average regions visited: {0}", regions_visited.Average ()));

                List<int> mem_use = statsData.Get ("mem_use").ConvertAll<int> ((s) => int.Parse (s));
                if (mem_use.Count > 0)
                    MainConsole.Instance.CleanInfo (string.Format ("Average viewer memory use: {0} mb", mem_use.Average () / 1000));

                List<float> ping = statsData.Get ("ping").ConvertAll<float> ((s) => float.Parse (s));
                if (ping.Count > 0)
                    MainConsole.Instance.CleanInfo (string.Format ("Average ping: {0}", ping.Average ()));

                List<int> agents_in_view = statsData.Get ("agents_in_view").ConvertAll<int> ((s) => int.Parse (s));
                if (agents_in_view.Count > 0)
                    MainConsole.Instance.CleanInfo (string.Format ("Average agents in view: {0}", agents_in_view.Average ()));

                MainConsole.Instance.CleanInfo ("");

            }
        }
    }
}