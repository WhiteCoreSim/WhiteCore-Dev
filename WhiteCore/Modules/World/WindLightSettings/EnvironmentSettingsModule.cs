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

using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.IO;

namespace WhiteCore.Modules
{
    public class EnvironmentSettingsModule : IEnvironmentSettingsModule, INonSharedRegionModule
    {
        private IScene m_scene;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(IScene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IEnvironmentSettingsModule>(this);
            scene.EventManager.OnRegisterCaps += EventManager_OnRegisterCaps;
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
            get { return "EnvironmentSettingsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private OSDMap EventManager_OnRegisterCaps(UUID agentID, IHttpServer server)
        {
            OSDMap retVal = new OSDMap();
            retVal["EnvironmentSettings"] = CapsUtil.CreateCAPS("EnvironmentSettings", "");
            //Sets the Windlight settings
            server.AddStreamHandler(new GenericStreamHandler("POST", retVal["EnvironmentSettings"],
                                                             delegate(string path, Stream request,
                                                                      OSHttpRequest httpRequest,
                                                                      OSHttpResponse httpResponse)
                                                                 { return SetEnvironment(request, agentID); }));
            //Sets the Windlight settings
            server.AddStreamHandler(new GenericStreamHandler("GET", retVal["EnvironmentSettings"],
                                                             delegate(string path, Stream request,
                                                                      OSHttpRequest httpRequest,
                                                                      OSHttpResponse httpResponse)
                                                                 { return EnvironmentSettings(agentID); }));
            return retVal;
        }

        private byte[] SetEnvironment(Stream request, UUID agentID)
        {
            IScenePresence SP = m_scene.GetScenePresence(agentID);
            if (SP == null)
                return new byte[0]; //They don't exist
            bool success = false;
            string fail_reason = "";
            if (SP.Scene.Permissions.CanIssueEstateCommand(agentID, false))
            {
                m_scene.RegionInfo.EnvironmentSettings = OSDParser.DeserializeLLSDXml(HttpServerHandlerHelpers.ReadFully(request));
                success = true;

                //Tell everyone about the changes
                TriggerWindlightUpdate(1);
            }
            else
            {
                fail_reason = "You don't have permissions to set the Windlight settings here.";
                SP.ControllingClient.SendAlertMessage(
                    "You don't have the correct permissions to set the Windlight Settings");
            }
            OSDMap result = new OSDMap()
                                {
                                    new KeyValuePair<string, OSD>("success", success),
                                    new KeyValuePair<string, OSD>("regionID", SP.Scene.RegionInfo.RegionID)
                                };
            if (fail_reason != "")
                result["fail_reason"] = fail_reason;

            return OSDParser.SerializeLLSDXmlBytes(result);
        }

        private byte[] EnvironmentSettings(UUID agentID)
        {
            IScenePresence SP = m_scene.GetScenePresence(agentID);
            if (SP == null)
                return new byte[0]; //They don't exist

            if (m_scene.RegionInfo.EnvironmentSettings != null)
                return OSDParser.SerializeLLSDXmlBytes(m_scene.RegionInfo.EnvironmentSettings);
            return new byte[0];
        }

        public void TriggerWindlightUpdate(int interpolate)
        {
            foreach (IScenePresence presence in m_scene.GetScenePresences())
            {
                OSD item = BuildEQM(interpolate);
                IEventQueueService eq = presence.Scene.RequestModuleInterface<IEventQueueService>();
                if (eq != null)
                    eq.Enqueue(item, presence.UUID, presence.Scene.RegionInfo.RegionID);
            }
        }

        private OSD BuildEQM(int interpolate)
        {
            OSDMap map = new OSDMap();

            OSDMap body = new OSDMap();

            body.Add("Interpolate", interpolate);

            map.Add("body", body);
            map.Add("message", OSD.FromString("WindLightRefresh"));
            return map;
        }

        public WindlightDayCycle GetCurrentDayCycle()
        {
            if (m_scene.RegionInfo.EnvironmentSettings != null)
            {
                WindlightDayCycle cycle = new WindlightDayCycle();
                cycle.FromOSD(m_scene.RegionInfo.EnvironmentSettings);
                return cycle;
            }
            return null;
        }

        public void SetDayCycle(WindlightDayCycle cycle)
        {
            m_scene.RegionInfo.EnvironmentSettings = cycle.ToOSD();
        }
    }
}