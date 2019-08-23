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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services
{
    public class FriendsProcessing : IService
    {
        #region Declares

        protected IRegistryCore m_registry;

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup ()
        {
            // Also look for incoming messages to display
            m_registry.RequestModuleInterface<ISyncMessageRecievedService> ().OnMessageReceived += OnMessageReceived;
        }

        #endregion

        protected OSDMap OnMessageReceived (OSDMap message)
        {

            if (!message.ContainsKey ("Method"))
                return null;                        // nothing to do here...

            var method = message ["Method"].AsString();

            ISyncMessagePosterService asyncPost = m_registry.RequestModuleInterface<ISyncMessagePosterService> ();
            // We need to check and see if this is an AgentStatusChange
            if (method == "AgentStatusChange") {
                OSDMap innerMessage = (OSDMap)message ["Message"];
                // We got a message, now pass it on to the clients that need it
                UUID AgentID = innerMessage ["AgentID"].AsUUID ();
                UUID FriendToInformID = innerMessage ["FriendToInformID"].AsUUID ();
                bool NewStatus = innerMessage ["NewStatus"].AsBoolean ();

                // Do this since IFriendsModule is a scene module, not a ISimulationBase module (not interchangeable)
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                if (manager != null) {
                    foreach (IScene scene in manager.Scenes) {
                        if (scene.GetScenePresence (FriendToInformID) != null &&
                            !scene.GetScenePresence (FriendToInformID).IsChildAgent) {
                            IFriendsModule friendsModule = scene.RequestModuleInterface<IFriendsModule> ();
                            if (friendsModule != null) {
                                // Send the message
                                friendsModule.SendFriendsStatusMessage (FriendToInformID, new [] { AgentID }, NewStatus);
                            }
                        }
                    }
                }
            } else if (method == "AgentStatusChanges") {
                OSDMap innerMessage = (OSDMap)message ["Message"];
                // We got a message, now pass it on to the clients that need it
                List<UUID> AgentIDs = ((OSDArray)innerMessage ["AgentIDs"]).ConvertAll<UUID> ((o) => o);
                UUID FriendToInformID = innerMessage ["FriendToInformID"].AsUUID ();
                bool NewStatus = innerMessage ["NewStatus"].AsBoolean ();

                // Do this since IFriendsModule is a scene module, not a ISimulationBase module (not interchangeable)
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                if (manager != null) {
                    foreach (IScene scene in manager.Scenes) {
                        if (scene.GetScenePresence (FriendToInformID) != null &&
                            !scene.GetScenePresence (FriendToInformID).IsChildAgent) {
                            IFriendsModule friendsModule = scene.RequestModuleInterface<IFriendsModule> ();
                            if (friendsModule != null) {
                                // Send the message
                                friendsModule.SendFriendsStatusMessage (FriendToInformID, AgentIDs.ToArray (), NewStatus);
                            }
                        }
                    }
                }
            } else if (method == "FriendGrantRights") {
                OSDMap body = (OSDMap)message ["Message"];
                UUID targetID = body ["Target"].AsUUID ();
                IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
                UserInfo info;
                if (agentInfoService != null && (info = agentInfoService.GetUserInfo (targetID.ToString ())) != null &&
                    info.IsOnline) {

                    // Forward the message
                    asyncPost.Post (info.CurrentRegionURI, message);
                }
            } else if (method == "FriendshipOffered") {
                OSDMap body = (OSDMap)message ["Message"];
                UUID targetID = body ["Friend"].AsUUID ();
                IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
                UserInfo info;
                if (agentInfoService != null && 
                    (info = agentInfoService.GetUserInfo (targetID.ToString ())) != null &&
                    info.IsOnline) {

                    // Forward the message
                    asyncPost.Post (info.CurrentRegionURI, message);
                }
            } else if (method == "FriendTerminated") {
                OSDMap body = (OSDMap)message ["Message"];
                UUID targetID = body ["ExFriend"].AsUUID ();
                IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
                UserInfo info;
                if (agentInfoService != null && 
                    (info = agentInfoService.GetUserInfo (targetID.ToString ())) != null &&
                    info.IsOnline) {

                    // Forward the message
                    asyncPost.Post (info.CurrentRegionURI, message);
                }
            } else if (method == "FriendshipDenied") {
                OSDMap body = (OSDMap)message ["Message"];
                UUID targetID = body ["FriendID"].AsUUID ();
                IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
                UserInfo info;
                if (agentInfoService != null && 
                    (info = agentInfoService.GetUserInfo (targetID.ToString ())) != null &&
                    info.IsOnline) {

                    // Forward the message
                    asyncPost.Post (info.CurrentRegionURI, message);
                }
            } else if (method == "FriendshipApproved") {
                OSDMap body = (OSDMap)message ["Message"];
                UUID targetID = body ["FriendID"].AsUUID ();
                IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
                UserInfo info;
                if (agentInfoService != null && 
                    (info = agentInfoService.GetUserInfo (targetID.ToString ())) != null &&
                    info.IsOnline) {

                    // Forward the message
                    asyncPost.Post (info.CurrentRegionURI, message);
                }
            }
            return null;
        }
    }
}
