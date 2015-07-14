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


using System;
using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Chat
{
    public class OfflineMessageModule : INonSharedRegionModule
    {
        bool m_enabled;
        IScene m_Scene;
        IMessageTransferModule m_TransferModule;
        IOfflineMessagesConnector OfflineMessagesConnector;
        bool m_SendOfflineMessagesToEmail;

        Dictionary<UUID, List<GridInstantMessage>> m_offlineMessagesCache =
            new Dictionary<UUID, List<GridInstantMessage>>();

        public void Initialise(IConfigSource config)
        {
            IConfig cnf = config.Configs["Messaging"];
            if (cnf != null)
            {
                m_enabled = (cnf.GetString("OfflineMessageModule", Name) == Name);
                m_SendOfflineMessagesToEmail = cnf.GetBoolean("SendOfflineMessagesToEmail", m_SendOfflineMessagesToEmail);
            }
        }

        public void AddRegion(IScene scene)
        {
            if (!m_enabled)
                return;

            m_Scene = scene;

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
            scene.EventManager.OnCachedUserInfo += UpdateCachedInfo;
        }

        public void RegionLoaded(IScene scene)
        {
            if (!m_enabled)
                return;

            if (m_TransferModule == null)
            {
                OfflineMessagesConnector = Framework.Utilities.DataManager.RequestPlugin<IOfflineMessagesConnector>();
                m_TransferModule = scene.RequestModuleInterface<IMessageTransferModule>();
                if (m_TransferModule == null || OfflineMessagesConnector == null)
                {
                    scene.EventManager.OnNewClient -= OnNewClient;
                    scene.EventManager.OnClosingClient -= OnClosingClient;

                    m_enabled = false;
                    m_Scene = null;

                    MainConsole.Instance.Error(
                        "[OFFLINE MESSAGING] No message transfer module or OfflineMessagesConnector is enabled. Diabling offline messages");
                    return;
                }
                m_TransferModule.OnUndeliveredMessage += UndeliveredMessage;
            }
        }

        public void RemoveRegion(IScene scene)
        {
            if (!m_enabled)
                return;

            m_Scene = null;

            if (m_TransferModule != null)
            {
                scene.EventManager.OnNewClient -= OnNewClient;
                scene.EventManager.OnClosingClient -= OnClosingClient;
                scene.EventManager.OnCachedUserInfo -= UpdateCachedInfo;
                m_TransferModule.OnUndeliveredMessage -= UndeliveredMessage;
            }
        }

        public string Name
        {
            get { return "OfflineMessageModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        IClientAPI FindClient(UUID agentID)
        {
            IScenePresence presence = m_Scene.GetScenePresence(agentID);
            return (presence != null && !presence.IsChildAgent) ? presence.ControllingClient : null;
        }

        void UpdateCachedInfo(UUID agentID, CachedUserInfo info)
        {
            lock (m_offlineMessagesCache)
                m_offlineMessagesCache[agentID] = info.OfflineMessages;
        }

        void OnNewClient(IClientAPI client)
        {
            client.OnRetrieveInstantMessages += RetrieveInstantMessages;
        }

        void OnClosingClient(IClientAPI client)
        {
            client.OnRetrieveInstantMessages -= RetrieveInstantMessages;
        }

        void RetrieveInstantMessages(IClientAPI client)
        {
            if (OfflineMessagesConnector == null)
                return;

            List<GridInstantMessage> msglist;
            lock (m_offlineMessagesCache)
            {
                if (m_offlineMessagesCache.TryGetValue(client.AgentId, out msglist))
                    m_offlineMessagesCache.Remove(client.AgentId);
            }

            if (msglist == null)
                msglist = OfflineMessagesConnector.GetOfflineMessages(client.AgentId);
            msglist.Sort(delegate(GridInstantMessage a, GridInstantMessage b)
                { return a.Timestamp.CompareTo(b.Timestamp); 
                });

            foreach (GridInstantMessage IM in msglist)
            {
                // Send through scene event manager so all modules get a chance
                // to look at this message before it gets delivered.
                //
                // Needed for proper state management for stored group
                // invitations
                //
                IM.Offline = 1;
                m_Scene.EventManager.TriggerIncomingInstantMessage(IM);
            }
        }

        void UndeliveredMessage(GridInstantMessage im, string reason)
        {
            if (OfflineMessagesConnector == null || im == null)
                return;
            
            IClientAPI client = FindClient(im.FromAgentID);
            if ((client == null) && (im.Dialog != 32))
                return;
            
            if (!OfflineMessagesConnector.AddOfflineMessage(im))
            {
                if ((!im.FromGroup) && (reason != "User does not exist.") && (client != null))
                    client.SendInstantMessage(new GridInstantMessage()
                    {
                        FromAgentID = im.ToAgentID,
                        FromAgentName = "System",
                        ToAgentID = im.FromAgentID,
                        Dialog = (byte)InstantMessageDialog.MessageFromAgent,
                        Message = "User has too many IMs already, please try again later.",
                        Offline = 0,
                        RegionID = im.RegionID
                    });
                else if (client == null)
                    return;
            }
            else if ((im.Offline != 0) && (!im.FromGroup || im.FromGroup))
            {
                if (im.Dialog == 32) //Group notice
                {
                    IGroupsModule module = m_Scene.RequestModuleInterface<IGroupsModule>();
                    if (module != null)
                        im = module.BuildOfflineGroupNotice(im);

                    // TODO:  This drops (supposedly) group messages and the logic above is interesting!!
                    return;
                }

                if (client == null) return;

                IEmailModule emailModule = m_Scene.RequestModuleInterface<IEmailModule>();
                if (emailModule != null && m_SendOfflineMessagesToEmail)
                {
                    IUserProfileInfo profile =
                        Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>().GetUserProfile(im.ToAgentID);
                    if (profile != null && profile.IMViaEmail)
                    {
                        UserAccount account = m_Scene.UserAccountService.GetUserAccount(null, im.ToAgentID);
                        if (account != null && !string.IsNullOrEmpty(account.Email))
                        {
                            emailModule.SendEmail(UUID.Zero, account.Email,
                                                  string.Format("Offline Message from {0}", im.FromAgentName),
                                                  string.Format("Time: {0}\n",
                                                                Util.ToDateTime(im.Timestamp).ToShortDateString()) +
                                                  string.Format("From: {0}\n", im.FromAgentName) +
                                                  string.Format("Message: {0}\n", im.Message), m_Scene);
                        }
                    }
                }

                if (im.Dialog == (byte) InstantMessageDialog.MessageFromAgent && !im.FromGroup)
                {
                    client.SendInstantMessage(new GridInstantMessage()
                    {
                        FromAgentID = im.ToAgentID,
                        FromAgentName = "System",
                        ToAgentID = im.FromAgentID,
                        Dialog = (byte)InstantMessageDialog.MessageFromAgent,
                        Message = "Message saved, reason: " + reason,
                        Offline = 0,
                        RegionID = im.RegionID
                    });
                }

                if (im.Dialog == (byte) InstantMessageDialog.InventoryOffered)
                    client.SendAlertMessage("User is not online. Inventory has been saved");
            }
            else if (im.Offline == 0)
            {
                if (client == null) return;
                if (im.Dialog == (byte) InstantMessageDialog.MessageFromAgent && !im.FromGroup)
                {
                    client.SendInstantMessage(new GridInstantMessage()
                    {
                        FromAgentID = im.ToAgentID,
                        FromAgentName = "System",
                        ToAgentID = im.FromAgentID,
                        Dialog = (byte)InstantMessageDialog.MessageFromAgent,
                        Message = "Message saved, reason: " + reason,
                        Offline = 0,
                        RegionID = im.RegionID
                    });
                }

                if (im.Dialog == (byte) InstantMessageDialog.InventoryOffered)
                    client.SendAlertMessage("User not able to be found. Inventory has been saved");
            }
        }
    }
}