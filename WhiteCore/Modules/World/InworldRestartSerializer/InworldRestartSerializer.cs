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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace WhiteCore.Modules
{
    public class InworldRestartSerializer : INonSharedRegionModule
    {
        private IScene m_scene;
        private string m_fileName = "sceneagents";
        private string m_storeDirectory = "";

        public string Name
        {
            get { return "InworldRestartSerializer"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["FileBasedSimulationData"];
            if (config != null)
                m_storeDirectory = PathHelpers.ComputeFullPath(config.GetString("StoreBackupDirectory", m_storeDirectory));
            config = source.Configs["Startup"];
            if (config != null)
                m_fileName = config.GetString("RegionDataFileName", m_fileName);
            MainConsole.Instance.Commands.AddCommand("quit serialized", 
                                                     "quit serialized", 
                                                     "Closes the scene and saves all agents", 
                                                     quitSerialized, true, false);
        }

        public void AddRegion(IScene scene)
        {
        }

        public void RegionLoaded(IScene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnStartupFullyComplete += EventManager_OnStartupFullyComplete;
        }

        void EventManager_OnStartupFullyComplete(IScene scene, List<string> data)
        {
            DeserializeUsers();
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void Close()
        {
        }

        private void quitSerialized(IScene scene, string[] args)
        {
            SerializeUsers(scene);
            scene.CloseQuietly = true;
            scene.Close(false);

            scene.RequestModuleInterface<ISimulationBase>().Shutdown(true);
        }

        private void SerializeUsers(IScene scene)
        {
            OSDMap userMap = new OSDMap();
            foreach (IScenePresence presence in scene.GetScenePresences())
            {
                OSDMap user = new OSDMap();
                OSDMap remoteIP = new OSDMap();
                remoteIP["Address"] = presence.ControllingClient.RemoteEndPoint.Address.ToString();
                remoteIP["Port"] = presence.ControllingClient.RemoteEndPoint.Port;
                user["RemoteEndPoint"] = remoteIP;
                user["ClientInfo"] = presence.ControllingClient.RequestClientInfo().ToOSD();
                user["Position"] = presence.AbsolutePosition;
                user["IsFlying"] = presence.PhysicsActor.Flying;

                userMap[presence.UUID.ToString()] = user;
            }


            File.WriteAllText(BuildSaveFileName(), OSDParser.SerializeJsonString(userMap));
        }

        private void DeserializeUsers()
        {
            if (!File.Exists(BuildSaveFileName()))
                return;
            foreach (OSD o in ((OSDMap)OSDParser.DeserializeJson(File.ReadAllText(BuildSaveFileName()))).Values)
            {
                AgentCircuitData data = new AgentCircuitData();
                OSDMap user = (OSDMap)o;
                data.FromOSD((OSDMap)user["ClientInfo"]);
                m_scene.AuthenticateHandler.AddNewCircuit(data.CircuitCode, data);
                OSDMap remoteIP = (OSDMap)user["RemoteEndPoint"];
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(remoteIP["Address"].AsString()), remoteIP["Port"].AsInteger());
                m_scene.ClientServers[0].AddClient(data.CircuitCode, data.AgentID, data.SessionID, ep, data);
                IScenePresence sp = m_scene.GetScenePresence(data.AgentID);
                sp.MakeRootAgent(user["Position"].AsVector3(), user["IsFlying"].AsBoolean(), true);
                sp.SceneViewer.SendPresenceFullUpdate(sp);
            }

            File.Delete(BuildSaveFileName());
        }

        private string BuildSaveFileName()
        {
            return (m_storeDirectory == "" || m_storeDirectory == "/")
                       ? m_fileName + ".siminfo"
                       : Path.Combine(m_storeDirectory, m_fileName + ".siminfo");
        }
    }
}
