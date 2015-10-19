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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WhiteCore.Services.GenericServices
{
    public class GridServerInfoService : ConnectorBase, IGridServerInfoService, IService
    {
        protected Dictionary<string, List<string>> m_gridURIs = new Dictionary<string, List<string>>();
        protected bool m_remoteCalls = false, m_enabled = false;
        protected int m_defaultURICount = 11;

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig conf = config.Configs["GridServerInfoService"];
            if (conf == null || !conf.GetBoolean("Enabled"))
                return;
            m_enabled = true;
            registry.RegisterModuleInterface<IGridServerInfoService>(this);
            m_remoteCalls = conf.GetBoolean("DoRemote");
            m_defaultURICount = conf.GetInt("DefaultURICount", m_defaultURICount);
            Init(registry, GetType().Name);


            conf = config.Configs["Configuration"];
            if (conf == null)
                return;
            foreach (string key in conf.GetKeys())
                m_gridURIs.Add(key, Util.ConvertToList(conf.GetString(key).Replace("ServersHostname", MainServer.Instance.HostName), true));
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            if (!m_enabled) return;
            Dictionary<string, string> uris = new Dictionary<string, string>();
            foreach (ConnectorBase connector in ConnectorRegistry.ServerHandlerConnectors)
            {
                uris.Add(connector.ServerHandlerName, MainServer.Instance.FullHostName + ":" +
                    connector.ServerHandlerPort + connector.ServerHandlerPath);
            }
            new Timer(SendGridURIsAsync, uris, 3000, System.Threading.Timeout.Infinite);
        }

        private void SendGridURIsAsync(object state)
        {
            SendGridURIs((Dictionary<string, string>)state);
        }

        public List<string> GetGridURIs(string key)
        {
            if (!m_gridURIs.ContainsKey(key))
                return new List<string>();
            return m_gridURIs[key];
        }

        public string GetGridURI(string key)
        {
            if (!m_gridURIs.ContainsKey(key))
                return "";
            return m_gridURIs[key][0];
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public Dictionary<string, List<string>> RetrieveAllGridURIs(bool secure)
        {
            if (m_remoteCalls)
                return (Dictionary<string, List<string>>)base.DoRemoteCallGet(true, "ServerURI", secure);

            if (m_gridURIs.Count < m_defaultURICount)
            {
                MainConsole.Instance.WarnFormat("[GridServerInfoService]: Retrieve URIs failed, only had {0} of {1} URIs needed", m_gridURIs.Count, m_defaultURICount);
                return new Dictionary<string, List<string>>();
            }

            if (secure)
                return m_gridURIs;
            else
            {
                Dictionary<string, List<string>> uris = new Dictionary<string, List<string>>();
                foreach (KeyValuePair<string, List<string>> kvp in m_gridURIs)
                    uris.Add(kvp.Key, new List<string>(kvp.Value));
                return uris;
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public void SendGridURIs(Dictionary<string, string> uri)
        {
            if (m_remoteCalls)
            {
                base.DoRemoteCallPost(true, "ServerURI", uri);
                return;
            }


            MainConsole.Instance.InfoFormat("[GridServerInfoService]: Adding {0} uris", uri.Count);

            foreach (KeyValuePair<string, string> kvp in uri)
            {
                if (!m_gridURIs.ContainsKey(kvp.Key))
                    m_gridURIs.Add(kvp.Key, new List<string>());
                if(!m_gridURIs[kvp.Key].Contains(kvp.Value))
                    m_gridURIs[kvp.Key].Add(kvp.Value);
            }

            m_registry.RequestModuleInterface<IGridInfo>().UpdateGridInfo();
        }

        public void AddURI(string key, string value)
        {
            if (m_remoteCalls)
            {
                new Timer((o) => AddURIInternal(key, value), null, 2000, System.Threading.Timeout.Infinite);
                return;
            }

            AddURIInternal(key, value);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public void AddURIInternal(string key, string value)
        {
            if (m_remoteCalls)
            {
                base.DoRemoteCallPost(true, "ServerURI", key, value);
                return;
            }

            if (!m_gridURIs.ContainsKey(key))
                m_gridURIs.Add(key, new List<string>());
            m_gridURIs[key].Add(value);
            m_registry.RequestModuleInterface<IGridInfo>().UpdateGridInfo();

            MainConsole.Instance.InfoFormat("[GridServerInfoService]: Adding 1 uri");
        }
    }
}
