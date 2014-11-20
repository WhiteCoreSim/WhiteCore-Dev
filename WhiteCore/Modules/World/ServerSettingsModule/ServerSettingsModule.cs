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

using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WhiteCore.Modules
{
    public class ServerSettingsModule : INonSharedRegionModule, IServerSettings
    {
        private List<ServerSetting> m_settings = new List<ServerSetting>();

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(IScene scene)
        {
            scene.RegisterModuleInterface<IServerSettings>(this);
            scene.EventManager.OnRegisterCaps += EventManager_OnRegisterCaps;
        }

        private OSDMap EventManager_OnRegisterCaps(UUID agentID, IHttpServer httpServer)
        {
            OSDMap map = new OSDMap();

            map["ServerFeatures"] = CapsUtil.CreateCAPS("ServerFeatures", "");
            httpServer.AddStreamHandler(new GenericStreamHandler("POST", map["ServerFeatures"],
                                                                 delegate(string path, Stream request,
                                                                          OSHttpRequest httpRequest,
                                                                          OSHttpResponse httpResponse)
                                                                     { return SetServerFeature(request, agentID); }));
            httpServer.AddStreamHandler(new GenericStreamHandler("GET", map["ServerFeatures"],
                                                                 delegate(string path, Stream request,
                                                                          OSHttpRequest httpRequest,
                                                                          OSHttpResponse httpResponse)
                                                                     { return GetServerFeature(request, agentID); }));

            return map;
        }

        private byte[] SetServerFeature(Stream request, UUID agentID)
        {
            return new byte[0];
        }

        private byte[] GetServerFeature(Stream request, UUID agentID)
        {
            return Encoding.UTF8.GetBytes(BuildSettingsXML());
        }

        public void RegionLoaded(IScene scene)
        {
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "ServerSettingsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string BuildSettingsXML()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("<?xml version=\"1.0\" ?>");
            builder.AppendLine("<llsd>");
            builder.AppendLine("<map>");

            foreach (ServerSetting setting in m_settings)
            {
                builder.Append("<key>");
                builder.Append(setting.Name);
                builder.AppendLine("</key>");
                builder.AppendLine("<map>");
                builder.AppendLine("<key>Comment</key>");
                builder.Append("<string>");
                builder.Append(setting.Comment);
                builder.AppendLine("</string>");
                builder.AppendLine("<key>Type</key>");
                builder.Append("<string>");
                builder.Append(setting.Type);
                builder.AppendLine("</string>");
                builder.AppendLine("<key>Value</key>");
                builder.Append(setting.GetValue());
                builder.AppendLine("</map>");
            }

            builder.AppendLine("</map>");
            builder.AppendLine("</llsd>");

            return builder.ToString();
        }

        public void RegisterSetting(ServerSetting setting)
        {
            m_settings.Add(setting);
        }

        public void UnregisterSetting(ServerSetting setting)
        {
            m_settings.RemoveAll(s => s.Name == setting.Name);
        }
    }
}