using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
