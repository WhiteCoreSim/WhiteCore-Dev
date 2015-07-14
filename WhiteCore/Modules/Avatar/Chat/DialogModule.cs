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
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Chat
{
    /// <summary>
    ///     This dialog module has support for mute lists
    /// </summary>
    public class DialogModule : INonSharedRegionModule, IDialogModule
    {
        protected bool m_enabled;
        protected IMuteListModule m_muteListModule;
        protected IScene m_scene;

        #region IDialogModule Members

        public void SendAlertToUser (IClientAPI client, string message)
        {
            SendAlertToUser (client, message, false);
        }

        public void SendAlertToUser (IClientAPI client, string message, bool modal)
        {
            client.SendAgentAlertMessage (message, modal);
        }

        public void SendAlertToUser (UUID agentID, string message)
        {
            SendAlertToUser (agentID, message, false);
        }

        public void SendAlertToUser (UUID agentID, string message, bool modal)
        {
            IScenePresence sp = m_scene.GetScenePresence (agentID);

            if (sp != null && !sp.IsChildAgent)
                sp.ControllingClient.SendAgentAlertMessage (message, modal);
        }

        public void SendAlertToUser (string Name, string message, bool modal)
        {
            IScenePresence presence = m_scene.SceneGraph.GetScenePresence (Name);
            if (presence != null && !presence.IsChildAgent)
                presence.ControllingClient.SendAgentAlertMessage (message, modal);
        }

        public void SendGeneralAlert (string message)
        {
            m_scene.ForEachScenePresence (delegate(IScenePresence presence)
            {
                if (!presence.IsChildAgent)
                    presence.ControllingClient.SendAlertMessage (message);
            });
        }

        public void SendDialogToUser (
            UUID avatarID, string objectName, UUID objectID, UUID ownerID,
            string message, UUID textureID, int ch, string[] buttonlabels)
        {
            UserAccount account = m_scene.UserAccountService.GetUserAccount (m_scene.RegionInfo.AllScopeIDs, ownerID);
            string ownerFirstName, ownerLastName;
            if (account != null)
            {
                ownerFirstName = account.FirstName;
                ownerLastName = account.LastName;
            } else
            {
                ownerFirstName = "(unknown";
                ownerLastName = "user)";
            }

            //If the user is muted, we do NOT send them dialog boxes
            if (m_muteListModule != null)
            {
                bool cached; // Not used but needed for call

                if (m_muteListModule.GetMutes (avatarID, out cached).Any (mute => mute.MuteID == ownerID))
                {
                    return;
                }
            }

            IScenePresence sp = m_scene.GetScenePresence (avatarID);
            if (sp != null && !sp.IsChildAgent)
                sp.ControllingClient.SendDialog (
                    objectName,
                    objectID, 
                    ownerID, 
                    ownerFirstName, 
                    ownerLastName, 
                    message,
                    textureID,
                    ch, 
                    buttonlabels);
        }

        public void SendUrlToUser (
            UUID avatarID, string objectName, UUID objectID, UUID ownerID, bool groupOwned, string message, string url)
        {
            IScenePresence sp = m_scene.GetScenePresence (avatarID);

            //If the user is muted, do NOT send them URL boxes
            if (m_muteListModule != null)
            {
                bool cached; // Not used but needed for call
                if (m_muteListModule.GetMutes (avatarID, out cached).Any (mute => mute.MuteID == ownerID))
                {
                    return;
                }
            }

            if (sp != null && !sp.IsChildAgent)
                sp.ControllingClient.SendLoadURL (objectName, objectID, ownerID, groupOwned, message, url);
        }

        public void SendTextBoxToUser (UUID avatarID, string message, int chatChannel, string name, UUID objectID,
                                      UUID ownerID)
        {
            IScenePresence sp = m_scene.GetScenePresence (avatarID);

            if (sp != null && !sp.IsChildAgent)
            {
                UserAccount account = m_scene.UserAccountService.GetUserAccount (m_scene.RegionInfo.AllScopeIDs, ownerID);
                string ownerFirstName, ownerLastName;

                if (account != null)
                {
                    ownerFirstName = account.FirstName;
                    ownerLastName = account.LastName;
                } else
                {
                    if (name != "")
                    {
                        ownerFirstName = name;
                        ownerLastName = "";
                    } else
                    {
                        ownerFirstName = "(unknown";
                        ownerLastName = "user)";
                    }
                }

                //If the user is muted, do not send the text box
                if (m_muteListModule != null)
                {
                    bool cached; // Not used but needed for call

                    if (m_muteListModule.GetMutes (avatarID, out cached).Any (mute => mute.MuteID == ownerID))
                    {
                        return;
                    }
                }
                sp.ControllingClient.SendTextBoxRequest (message, chatChannel, name, ownerFirstName, ownerLastName,
                    ownerID, objectID);
            }
        }

        public void SendNotificationToUsersInRegion (
            UUID fromAvatarID, string fromAvatarName, string message)
        {
            m_scene.ForEachScenePresence (delegate(IScenePresence presence)
            {
                if (!presence.IsChildAgent)
                    presence.ControllingClient.SendBlueBoxMessage (
                        fromAvatarID,
                        fromAvatarName,
                        message);
            });
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise (IConfigSource config)
        {
            IConfig m_config = config.Configs ["Dialog"];
            if (m_config != null)
            {
                m_enabled = m_config.GetString ("DialogModule", Name) == Name;
            }
        }

        public void AddRegion (IScene scene)
        {
            if (!m_enabled)
                return;
            
            m_scene = scene;
            m_scene.RegisterModuleInterface<IDialogModule> (this);
            m_scene.EventManager.OnPermissionError += SendAlertToUser;

            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "alert user", 
                    "alert user <<first last> message>", 
                    "Send an alert to a user in the current region", 
                    HandleAlertConsoleCommand, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "alert general", 
                    "alert general <message>", 
                    "Send an alert to everyone in the current region", 
                    HandleAlertConsoleCommand, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "alert broadcast", 
                    "alert broadcast <message>", 
                    "Send an alert to everyone logged in", 
                    HandleAlertConsoleCommand, false, true);
                
            }
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public void RegionLoaded (IScene scene)
        {
            m_muteListModule = m_scene.RequestModuleInterface<IMuteListModule> ();
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close ()
        {
        }

        public string Name
        {
            get { return "DialogModule"; }
        }

        #endregion

        /// <summary>
        ///     Handle an alert command from the console.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleAlertConsoleCommand (IScene scene, string[] cmdparams)
        {
            string message = "";
            string userName = "";
            string cmdType = cmdparams [1].ToLower ();
 
            if (cmdType.StartsWith ("g") || cmdType.StartsWith ("b")  )
            {
                // general
                if (cmdparams.Length > 2)
                    message = Util.CombineParams (cmdparams, 2);
                else
                    message = MainConsole.Instance.Prompt ("Message to send?", "");
                if (message == "")
                    return;

                if (cmdType.StartsWith ("g"))
                {
                    MainConsole.Instance.InfoFormat ("[DIALOG]: Sending general alert in region {0} with message '{1}'",
                        scene.RegionInfo.RegionName, message);

                    // send the message
                    scene.ForEachScenePresence (delegate(IScenePresence sp) {
                        if (!sp.IsChildAgent)
                            sp.ControllingClient.SendAlertMessage (message);
                    });
                } else
                {

                    MainConsole.Instance.InfoFormat ("[DIALOG]: Sending broadcast alert to all regions with message '{0}'",  message);

                    // broadcst the message
                    foreach (IScene scn in MainConsole.Instance.ConsoleScenes)
                    {
                        scn.ForEachScenePresence (delegate(IScenePresence sp) {
                            if (!sp.IsChildAgent)
                                sp.ControllingClient.SendAlertMessage (message);
                        });
                    }
                }
                return;
            }
                
            // user alert
            if (cmdparams.Length >= 4)
                userName = cmdparams [2] + " " + cmdparams [3];
            else
                userName = MainConsole.Instance.Prompt ("User name? (First Last)", "");
            if (userName == "")
                return;
            
            if (cmdparams.Length > 4)
                message = Util.CombineParams (cmdparams, 4);
            else
                message = MainConsole.Instance.Prompt ("Message to send?", "");
            if (message == "")
                return;
                       

            MainConsole.Instance.InfoFormat ("[DIALOG]: Sending alert in region {0} to {1} with message '{2}'",
                scene.RegionInfo.RegionName, userName, message);

            // send the message to the user
            IScenePresence spc = scene.SceneGraph.GetScenePresence (userName);
            if (spc != null && !spc.IsChildAgent)
                spc.ControllingClient.SendAgentAlertMessage (message, false);

        }
    }
}