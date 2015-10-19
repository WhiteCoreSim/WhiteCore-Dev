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

using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Services.GenericServices.CapsService
{
    public class ExternalCapsHandler : ConnectorBase, IExternalCapsHandler, IService
    {
        private List<string> m_allowedCapsModules = new List<string>();
        private Dictionary<UUID, List<IExternalCapsRequestHandler>> m_caps =
            new Dictionary<UUID, List<IExternalCapsRequestHandler>>();
        private ISyncMessagePosterService m_syncPoster;
        private List<string> m_servers = new List<string>();

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            IConfig externalConfig = config.Configs["ExternalCaps"];
            if (externalConfig == null) return;
            m_allowedCapsModules = Util.ConvertToList(externalConfig.GetString("CapsHandlers"), true);
            
            ISyncMessageRecievedService service = registry.RequestModuleInterface<ISyncMessageRecievedService>();
            service.OnMessageReceived += service_OnMessageReceived;
            m_syncPoster = registry.RequestModuleInterface<ISyncMessagePosterService>();
            m_registry = registry;
            registry.RegisterModuleInterface<IExternalCapsHandler>(this);

            Init(registry, GetType().Name);
        }

        public void FinishedStartup()
        {
        }

        public OSDMap GetExternalCaps(UUID agentID, GridRegion region)
        {
            if (m_registry == null) return new OSDMap();
            OSDMap resp = new OSDMap();
            if (m_registry.RequestModuleInterface<IGridServerInfoService>() != null)
            {
                m_servers = m_registry.RequestModuleInterface<IGridServerInfoService>().GetGridURIs("SyncMessageServerURI");
                OSDMap req = new OSDMap();
                req["AgentID"] = agentID;
                req["Region"] = region.ToOSD();
                req["Method"] = "GetCaps";

                List<ManualResetEvent> events = new List<ManualResetEvent>();
                foreach (string uri in m_servers.Where((u)=>(!u.Contains(MainServer.Instance.Port.ToString()))))
                {
                    ManualResetEvent even = new ManualResetEvent(false);
                    m_syncPoster.Get(uri, req, (r) =>
                    {
                        if (r == null)
                            return;
                        foreach (KeyValuePair<string, OSD> kvp in r)
                            resp.Add(kvp.Key, kvp.Value);
                        even.Set();
                    });
                    events.Add(even);
                }
                if(events.Count > 0)
                    ManualResetEvent.WaitAll(events.ToArray());
            }
            foreach (var h in GetHandlers(agentID, region.RegionID))
            {
                if (m_allowedCapsModules.Contains(h.Name))
                    h.IncomingCapsRequest(agentID, region, m_registry.RequestModuleInterface<ISimulationBase>(), ref resp);
            }
            return resp;
        }

        public void RemoveExternalCaps(UUID agentID, GridRegion region)
        {
            OSDMap req = new OSDMap();
            req["AgentID"] = agentID;
            req["Region"] = region.ToOSD();
            req["Method"] = "RemoveCaps";

            foreach (string uri in m_servers)
                m_syncPoster.Post(uri, req);

            foreach (var h in GetHandlers(agentID, region.RegionID))
            {
                if (m_allowedCapsModules.Contains(h.Name))
                    h.IncomingCapsDestruction();
            }
        }

        OSDMap service_OnMessageReceived(OSDMap message)
        {
            string method = message["Method"];
            if (method != "GetCaps" && method != "RemoveCaps")
                return null;
            UUID AgentID = message["AgentID"];
            GridRegion region = new GridRegion();
            region.FromOSD((OSDMap)message["Region"]);
            OSDMap map = new OSDMap();
            switch (method)
            {
                case "GetCaps":
                    foreach (var h in GetHandlers(AgentID, region.RegionID))
                    {
                        if (m_allowedCapsModules.Contains(h.Name))
                            h.IncomingCapsRequest(AgentID, region, m_registry.RequestModuleInterface<ISimulationBase>(), ref map);
                    }
                    return map;
                case "RemoveCaps":
                    foreach (var h in GetHandlers(AgentID, region.RegionID))
                    {
                        if (m_allowedCapsModules.Contains(h.Name))
                            h.IncomingCapsDestruction();
                    }
                    return map;
            }
            return null;
        }

        private List<IExternalCapsRequestHandler> GetHandlers(UUID agentID, UUID regionID)
        {
            lock (m_caps)
            {
                List<IExternalCapsRequestHandler> caps;
                if (!m_caps.TryGetValue(agentID ^ regionID, out caps))
                {
                    caps = WhiteCore.Framework.ModuleLoader.WhiteCoreModuleLoader.PickupModules<IExternalCapsRequestHandler>();
                    m_caps.Add(agentID ^ regionID, caps);
                }
                return caps;
            }
        }
    }
}
