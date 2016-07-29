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
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services
{
    public class GridWideMessageModule : IService, IGridWideMessageModule
    {
        #region Declares

        protected IRegistryCore m_registry;
        protected ISyncMessagePosterService m_messagePost;
        protected ICapsService m_capsService;

        #endregion

        #region IGridWideMessageModule Members

        public void KickUser (UUID avatarID, string message)
        {
            //Get required interfaces
            IClientCapsService client = m_capsService.GetClientCapsService (avatarID);
            if (client != null) {
                IRegionClientCapsService regionClient = client.GetRootCapsService ();
                if (regionClient != null) {
                    //Send the message to the client
                    m_messagePost.Get (regionClient.Region.ServerURI,
                                      BuildRequest ("KickUserMessage", message, regionClient.AgentID.ToString ()),
                                      (resp) => {
                                          IAgentProcessing agentProcessor =
                                              m_registry.RequestModuleInterface<IAgentProcessing> ();
                                          if (agentProcessor != null)
                                              agentProcessor.LogoutAgent (regionClient, true);
                                          MainConsole.Instance.Info ("User has been kicked.");
                                      });

                    return;
                }
            }
            MainConsole.Instance.Info ("Could not find user to send message to.");
        }

        public void MessageUser (UUID avatarID, string message)
        {
            //Get required interfaces
            IClientCapsService client = m_capsService.GetClientCapsService (avatarID);
            if (client != null) {
                IRegionClientCapsService regionClient = client.GetRootCapsService ();
                if (regionClient != null) {
                    //Send the message to the client
                    m_messagePost.Post (regionClient.Region.ServerURI,
                                       BuildRequest ("GridWideMessage", message, regionClient.AgentID.ToString ()));
                    MainConsole.Instance.Info ("Message sent to the user.");
                    return;
                }
            }
            MainConsole.Instance.Info ("Could not find user to send message to.");
        }

        public void SendAlert (string message)
        {
            //Get required interfaces
            List<IClientCapsService> clients = m_capsService.GetClientsCapsServices ();

            //Go through all clients, and send the message async to all agents that are root
            foreach (
                IRegionClientCapsService regionClient in
                    from client in clients
                    from regionClient in client.GetCapsServices ()
                    where regionClient.RootAgent
                    select regionClient)
            {
                MainConsole.Instance.Debug ("[GridWideMessageModule]: Informed " + regionClient.ClientCaps.AccountInfo.Name);
                //Send the message to the client
                m_messagePost.Post (regionClient.Region.ServerURI,
                                   BuildRequest ("GridWideMessage", message, regionClient.AgentID.ToString ()));
            }
            MainConsole.Instance.Info ("[GridWideMessageModule]: Sent alert, will be delivered across the grid shortly.");
        }

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            registry.RegisterModuleInterface<IGridWideMessageModule> (this);
            IConfig handlersConfig = config.Configs ["Handlers"];
            if (handlersConfig == null)
                return;
            if (handlersConfig.GetString ("GridWideMessage", "") != "GridWideMessageModule")
                return;


            if (MainConsole.Instance != null) {
                MainConsole.Instance.Commands.AddCommand (
                    "grid send alert",
                    "grid send alert <message>",
                    "Sends a message to all users in the grid",
                    SendGridAlert, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "grid send message",
                    "grid send message <first> <last> <message>",
                    "Sends a message to a user in the grid",
                    SendGridMessage, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "grid kick user",
                    "grid kick user <first> <last> <message>",
                    "Kicks a user from the grid",
                    KickUserMessage, false, true);
            }
        }

        public void FinishedStartup ()
        {
            //Also look for incoming messages to display
            m_messagePost = m_registry.RequestModuleInterface<ISyncMessagePosterService> ();
            m_capsService = m_registry.RequestModuleInterface<ICapsService> ();
            m_registry.RequestModuleInterface<ISyncMessageRecievedService> ().OnMessageReceived += OnMessageReceived;
        }

        #endregion

        #region Commands

        protected void SendGridAlert (IScene scene, string [] cmd)
        {
            string message;
            if (cmd.Length > 3)
                message = CombineParams (cmd, 3);
            else
                message = MainConsole.Instance.Prompt ("Message to send?", "");
            if (message == "")
                return;

            SendAlert (message);
        }

        protected void SendGridMessage (IScene scene, string [] cmd)
        {
            string user;
            string message;

            if (cmd.Length >= 4)
                user = CombineParams (cmd, 3, 5);
            else
                user = MainConsole.Instance.Prompt ("User name? (First Last)", "");
            if (user == "")
                return;

            if (cmd.Length > 5)
                message = CombineParams (cmd, 5);
            else
                message = MainConsole.Instance.Prompt ("Message to send?", "");
            if (message == "")
                return;


            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount account = userService.GetUserAccount (null, user.Split (' ') [0], user.Split (' ') [1]);
            if (account == null) {
                MainConsole.Instance.Info ("User does not exist.");
                return;
            }
            MessageUser (account.PrincipalID, message);
        }

        protected void KickUserMessage (IScene scene, string [] cmd)
        {
            //Combine the parameters and figure out the message
            string user = CombineParams (cmd, 3, 5);
            if (user.EndsWith (" ", System.StringComparison.Ordinal))
                user = user.Remove (user.Length - 1);
            string message = CombineParams (cmd, 5);
            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount account = userService.GetUserAccount (null, user);
            if (account == null) {
                MainConsole.Instance.Info ("User does not exist.");
                return;
            }

            KickUser (account.PrincipalID, message);
        }

        string CombineParams (string [] commandParams, int pos)
        {
            string result = string.Empty;
            for (int i = pos; i < commandParams.Length; i++) {
                result += commandParams [i] + " ";
            }

            return result;
        }

        string CombineParams (string [] commandParams, int pos, int end)
        {
            string result = string.Empty;
            for (int i = pos; i < commandParams.Length && i < end; i++) {
                result += commandParams [i] + " ";
            }

            return result;
        }

        OSDMap BuildRequest (string name, string value, string user)
        {
            OSDMap map = new OSDMap ();

            map ["Method"] = name;
            map ["Value"] = value;
            map ["User"] = user;

            return map;
        }

        #endregion

        #region Message Received

        protected OSDMap OnMessageReceived (OSDMap message)
        {
            if (message.ContainsKey ("Method") && message ["Method"] == "GridWideMessage") {
                //We got a message, now display it
                string user = message ["User"].AsString ();
                string value = message ["Value"].AsString ();

                //Get the Scene registry since IDialogModule is a region module, and isn't in the ISimulationBase registry
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                if (manager != null) {
                    foreach (IScene scene in manager.Scenes) {
                        IScenePresence sp = null;
                        if (scene.TryGetScenePresence (UUID.Parse (user), out sp) && !sp.IsChildAgent) {
                            IDialogModule dialogModule = scene.RequestModuleInterface<IDialogModule> ();
                            if (dialogModule != null) {
                                //Send the message to the user now
                                dialogModule.SendAlertToUser (UUID.Parse (user), value);
                            }
                        }
                    }
                }
            } else if (message.ContainsKey ("Method") && message ["Method"] == "KickUserMessage") {
                //We got a message, now display it
                string user = message ["User"].AsString ();
                string value = message ["Value"].AsString ();

                //Get the Scene registry since IDialogModule is a region module, and isn't in the ISimulationBase registry
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                if (manager != null) {
                    foreach (IScene scene in manager.Scenes) {
                        IScenePresence sp = null;
                        if (scene.TryGetScenePresence (UUID.Parse (user), out sp)) {
                            sp.ControllingClient.Kick (value == "" ? "The WhiteCore Grid Manager kicked you out." : value);
                            IEntityTransferModule transferModule = scene.RequestModuleInterface<IEntityTransferModule> ();
                            if (transferModule != null)
                                transferModule.IncomingCloseAgent (scene, sp.UUID);
                        }
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
