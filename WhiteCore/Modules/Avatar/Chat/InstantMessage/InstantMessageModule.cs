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


using System;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;

namespace WhiteCore.Modules.Chat
{
    public class InstantMessageModule : INonSharedRegionModule
    {
        IScene m_Scene;

        IMessageTransferModule m_TransferModule;

        /// <value>
        ///     Is this module enabled?
        /// </value>
        bool m_enabled;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                m_enabled = (config.Configs["Messaging"].GetString("InstantMessageModule", Name) == Name);
            }
        }

        public void AddRegion(IScene scene)
        {
            if (!m_enabled)
                return;

            m_Scene = scene;
            scene.EventManager.OnNewClient += EventManager_OnNewClient;
            scene.EventManager.OnClosingClient += EventManager_OnClosingClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
        }

        public void RegionLoaded(IScene scene)
        {
            if (!m_enabled)
                return;

            if (m_TransferModule == null)
            {
                m_TransferModule =
                    scene.RequestModuleInterface<IMessageTransferModule>();

                if (m_TransferModule == null)
                {
                    MainConsole.Instance.Error("[INSTANT MESSAGE]: No message transfer module, IM will not work!");
                    scene.EventManager.OnNewClient -= EventManager_OnNewClient;
                    scene.EventManager.OnClosingClient -= EventManager_OnClosingClient;
                    scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;

                    m_Scene = null;
                    m_enabled = false;
                }
            }
        }

        public void RemoveRegion(IScene scene)
        {
            if (!m_enabled)
                return;

            m_Scene = null;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InstantMessageModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        void EventManager_OnClosingClient(IClientAPI client)
        {
            //client.OnInstantMessage -= OnInstantMessage;
        }

        void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
        }

        public void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            byte dialog = im.Dialog;

            if (dialog != (byte) InstantMessageDialog.MessageFromAgent
                && dialog != (byte) InstantMessageDialog.StartTyping
                && dialog != (byte) InstantMessageDialog.StopTyping
                && dialog != (byte) InstantMessageDialog.BusyAutoResponse
                && dialog != (byte) InstantMessageDialog.MessageFromObject)
            {
                return;
            }

            if (m_TransferModule != null)
            {
                if (client == null)
                {
                    UserAccount account = m_Scene.UserAccountService.GetUserAccount(m_Scene.RegionInfo.AllScopeIDs,
                                                                                    im.FromAgentID);
                    if (account != null)
                        im.FromAgentName = account.Name;
                    else
                        im.FromAgentName = im.FromAgentName + "(No account found for this user)";
                }
                else
                    im.FromAgentName = client.Name;

                m_TransferModule.SendInstantMessage(im);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="msg"></param>
        void OnGridInstantMessage(GridInstantMessage msg)
        {
            byte dialog = msg.Dialog;

            if (dialog != (byte) InstantMessageDialog.MessageFromAgent
                && dialog != (byte) InstantMessageDialog.StartTyping
                && dialog != (byte) InstantMessageDialog.StopTyping
                && dialog != (byte) InstantMessageDialog.MessageFromObject)
            {
                return;
            }

            if (m_TransferModule != null)
            {
                UserAccount account = m_Scene.UserAccountService.GetUserAccount(m_Scene.RegionInfo.AllScopeIDs,
                                                                                msg.FromAgentID);
                if (account != null)
                    msg.FromAgentName = account.Name;
                else
                    msg.FromAgentName = msg.FromAgentName + "(No account found for this user)";

                IScenePresence presence = null;
                if (m_Scene.TryGetScenePresence(msg.ToAgentID, out presence))
                {
                    presence.ControllingClient.SendInstantMessage(msg);
                    return;
                }
            }
        }
    }
}