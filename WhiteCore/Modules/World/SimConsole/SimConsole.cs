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
using System.IO;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Modules.SimConsole
{
    /// <summary>
    ///     This module allows for the console to be accessed in V2 viewers that support SimConsole
    ///     This will eventually be extended in Imprudence so that full console support can be added into the viewer (this module already supports the eventual extension)
    /// </summary>
    public class SimConsole : INonSharedRegionModule
    {
        enum Access
        {
            ReadWrite,
            Read,
            Write,
            None
        }

        #region Declares

        readonly Dictionary<UUID, Access> m_authorizedParticipants = new Dictionary<UUID, Access> ();
        IScene m_Scene;
        readonly Dictionary<string, Access> m_userKeys = new Dictionary<string, Access> ();
        readonly Dictionary<UUID, string> m_userLogLevel = new Dictionary<UUID, string> ();
        bool m_enabled;
        IEventQueueService msgQ;

        #endregion

        #region INonSharedRegionModule

        public void Initialise (IConfigSource source)
        {
            IConfig config = source.Configs ["SimConsole"];
            if (config != null) {
                m_enabled = config.GetBoolean ("Enabled", false);
                if (!m_enabled)
                    return;

                string User = config.GetString ("Users", "");
                string [] Users = User.Split (new string [] { "|" }, StringSplitOptions.RemoveEmptyEntries);

                MainConsole.OnIncomingLogWrite += IncomingLogWrite; //Get this hooked up

                // See who can access the console
                if (Users.Length > 0) {
                    for (int i = 0; i < Users.Length; i += 2) {
                        if (!m_userKeys.ContainsKey (Users [i]) && (i + 1) < Users.Length)                            m_userKeys.Add (Users [i], (Access)Enum.Parse (typeof (Access), Users [i + 1]));
                    }
                } else
                    MainConsole.Instance.Warn ("[SimConsole]: No SimConsole users specified. God access only.");
            }
        }

        public void AddRegion (IScene scene)
        {
            if (!m_enabled)
                return;

            m_Scene = scene;

            // set up event queue for messaging
            msgQ = m_Scene.RequestModuleInterface<IEventQueueService> ();
            if (msgQ == null) {
                m_enabled = false;      // no point if we cannot send messages
                MainConsole.Instance.Warn ("[Sim console]: Disabled as no message queue is available.");
                return;
            }

            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            scene.EventManager.OnMakeRootAgent += EventManager_OnMakeRootAgent;
            scene.EventManager.OnMakeChildAgent += EventManager_OnMakeChildAgent;

        }

        public void RegionLoaded (IScene scene)
        {
        }

        public void RemoveRegion (IScene scene)
        {
            scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            scene.EventManager.OnMakeRootAgent -= EventManager_OnMakeRootAgent;
            scene.EventManager.OnMakeChildAgent -= EventManager_OnMakeChildAgent;
            m_Scene = null;
        }

        public void Close ()
        {
        }

        public string Name {
            get { return "SimConsole"; }
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        #endregion

        #region CAPS

        public OSDMap OnRegisterCaps (UUID agentID, IHttpServer server)
        {
            HttpServerHandle consoleCaps = delegate (
                string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse) {
                    return SimConsoleAsyncResponder (agentID, request);
                };

            OSDMap retVal = new OSDMap ();
            retVal ["SimConsoleAsync"] = CapsUtil.CreateCAPS ("SimConsoleAsync", "");

            server.AddStreamHandler (new GenericStreamHandler ("POST", retVal ["SimConsoleAsync"], consoleCaps));

            return retVal;
        }

        //byte[] SimConsoleAsyncResponder(Stream request, UUID agentID)
        byte [] SimConsoleAsyncResponder (UUID agentID, Stream request)
        {
            IScenePresence SP = m_Scene.GetScenePresence (agentID);
            if (SP == null)
                return new byte [0]; //They don't exist

            OSD rm = OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));

            string message = rm.AsString ();

            //Is a god, or they authenticated to the server and have write access
            if (AuthenticateUser (SP, message) && CanWrite (SP.UUID))
                FireConsole (message);
            return OSDParser.SerializeLLSDXmlBytes ("");
        }

        void FireConsole (string message)
        {
            Util.FireAndForget (delegate { MainConsole.Instance.RunCommand (message); });
        }

        #endregion

        #region Authentication

        bool CanWrite (UUID AgentID)
        {
            if (m_authorizedParticipants.ContainsKey (AgentID)) {
                return m_authorizedParticipants [AgentID] == Access.Write
                       || m_authorizedParticipants [AgentID] == Access.ReadWrite;
            }
            return false;
        }

        bool CanRead (UUID AgentID)
        {
            if (m_authorizedParticipants.ContainsKey (AgentID)) {
                return m_authorizedParticipants [AgentID] == Access.Read
                       || m_authorizedParticipants [AgentID] == Access.ReadWrite;
            }
            return false;
        }

        bool AuthenticateUser (IScenePresence sp, string message)
        {
            if (m_authorizedParticipants.ContainsKey (sp.UUID)) {
                if (message == "")
                    return true; //Just checking whether it exists then
                
                bool firstLogin = false;
                if (!m_userLogLevel.ContainsKey (sp.UUID)) {
                    m_userLogLevel.Add (sp.UUID, "Info");
                    firstLogin = true;
                }
                return ParseMessage (sp, message, firstLogin);
            }

            if (m_userKeys.ContainsKey (sp.Name)) {
                m_authorizedParticipants.Add (sp.UUID, m_userKeys [sp.Name]);
                if (message == "")
                    return true; //Just checking whether it exists then
                
                return ParseMessage (sp, message, true);
            }
            return false;
        }

        bool ParseMessage (IScenePresence sp, string message, bool firstLogin)
        {
            if (firstLogin) {
                SendConsoleEventEQM (sp.UUID,
                                    "Welcome to the console, type /help for more information about viewer console commands");
            } else if (message.StartsWith ("/logout", StringComparison.Ordinal)) {
                m_authorizedParticipants.Remove (sp.UUID);
                SendConsoleEventEQM (sp.UUID, "Log out successful.");
                return false; //Don't execute the message anymore

            } else if (message.StartsWith ("/set log level", StringComparison.Ordinal)) {
                string [] words = message.Split (' ');
                if (words.Length == 4) {
                    m_userLogLevel [sp.UUID] = words [3];
                    SendConsoleEventEQM (sp.UUID, "Set log level successful.");
                } else
                    SendConsoleEventEQM (sp.UUID, "Set log level failed, please use a valid log level.");
                return false; //Don't execute the message anymore

            } else if (message.StartsWith ("/help", StringComparison.Ordinal)) {
                SendConsoleEventEQM (sp.UUID, "/logout - logout of the console.");
                SendConsoleEventEQM (sp.UUID, "/set log level - shows only certain messages to the viewer console.");
                SendConsoleEventEQM (sp.UUID, "/help - show this message again.");
                return false; //Don't execute the message anymore
            }
            return true;
        }

        #endregion

        void EventManager_OnMakeRootAgent (IScenePresence presence)
        {
            //See whether they are authenticated so that we can start sending them messages
            AuthenticateUser (presence, "");
        }

        void EventManager_OnMakeChildAgent (IScenePresence presence, GridRegion destination)
        {
            m_authorizedParticipants.Remove (presence.UUID);
            m_userLogLevel.Remove (presence.UUID);
        }

        public void IncomingLogWrite (string level, string text)
        {
            if (text == "")
                return;

            foreach (
                KeyValuePair<UUID, Access> kvp in
                    m_authorizedParticipants.Where (kvp => kvp.Value == Access.ReadWrite || kvp.Value == Access.Read)
                                            .Where (kvp => m_userLogLevel.ContainsKey (kvp.Key) &&
                                                    MainConsole.Instance.CompareLogLevels (m_userLogLevel [kvp.Key], level))) {
                //Send the EQM with the message to all people who have read access
                SendConsoleEventEQM (kvp.Key, text);
            }
        }

        /// <summary>
        ///     Send a console message to the viewer
        /// </summary>
        /// <param name="AgentID"></param>
        /// <param name="text"></param>
        void SendConsoleEventEQM (UUID AgentID, string text)
        {
            OSDMap item = new OSDMap {
                {"body", text},
                {"message", OSD.FromString("SimConsoleResponse")}
            };
            msgQ.Enqueue (item, AgentID, m_Scene.RegionInfo.RegionID);
        }
    }
}
