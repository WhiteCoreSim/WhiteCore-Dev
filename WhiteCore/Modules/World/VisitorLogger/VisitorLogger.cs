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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;


namespace WhiteCore.Modules.VisitorLogger
{
    /// <summary>
    ///     This module logs all visitors to the sim to a specified file
    /// </summary>
    public class VisitorLoggerModule : INonSharedRegionModule
    {
        #region Declares

        protected bool m_enabled = true;
        protected string m_fileName = "Visitors.log";
        protected Dictionary<UUID, DateTime> m_timesOfUsers = new Dictionary<UUID, DateTime> ();

        #endregion

        #region INonSharedRegionModule

        public void Initialise (IConfigSource source)
        {
            IConfig config = source.Configs ["VisitorLogModule"];
            if (config != null) {
                m_enabled = config.GetBoolean ("Enabled", m_enabled);
                m_fileName = config.GetString ("FileName", m_fileName);

                if (m_enabled && (m_fileName == ""))        // in case of default config
                    m_fileName = "Visitors.log";
            }
        }

        public void Close ()
        {
        }

        public void AddRegion (IScene scene)
        {
            if (m_enabled) {
                scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
                scene.EventManager.OnClosingClient += EventManager_OnClosingClient;
            }
        }

        public void RegionLoaded (IScene scene)
        {
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public string Name {
            get { return "VisitorLoggerModule"; }
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        void EventManager_OnClosingClient (IClientAPI client)
        {
            string logPath = MainConsole.Instance.LogPath;
            IScenePresence presence;
            if (client.Scene.TryGetScenePresence (client.AgentId, out presence) && !presence.IsChildAgent &&
                m_timesOfUsers.ContainsKey (client.AgentId)) {
                //Add the user
                FileStream stream = new FileStream (logPath + m_fileName, FileMode.OpenOrCreate);
                StreamWriter m_streamWriter = new StreamWriter (stream);
                try {
                    m_streamWriter.BaseStream.Position += m_streamWriter.BaseStream.Length;

                    string LineToWrite = DateTime.Now.ToShortDateString () + " " + DateTime.Now.ToLongTimeString () +
                                         " - " + client.Name + " left " +
                                         client.Scene.RegionInfo.RegionName + " after " +
                                         (DateTime.Now - m_timesOfUsers [client.AgentId]).Minutes + " minutes.";
                    m_timesOfUsers.Remove (presence.UUID);

                    m_streamWriter.WriteLine (LineToWrite);
                    m_streamWriter.Close ();
                } catch {
                    m_streamWriter.Close ();
                }
                stream.Close ();
            }
        }

        void OnMakeRootAgent (IScenePresence presence)
        {
            string logPath = MainConsole.Instance.LogPath;
            FileStream stream = new FileStream (logPath + m_fileName, FileMode.OpenOrCreate);
            StreamWriter m_streamWriter = new StreamWriter (stream);

            try {
                m_streamWriter.BaseStream.Position += m_streamWriter.BaseStream.Length;
                string lineToWrite;

                // are we just moving around...
                if (m_timesOfUsers.ContainsKey (presence.UUID)) {
                    lineToWrite = DateTime.Now.ToShortDateString () + " " + DateTime.Now.ToLongTimeString () +
                        " - " + presence.Name + " online for " +
                        (DateTime.Now - m_timesOfUsers [presence.UUID]).Minutes + " minutes.";

                    m_streamWriter.WriteLine (lineToWrite);
                } else {
                    // ..or first login
                    m_timesOfUsers [presence.UUID] = DateTime.Now;
                }

                lineToWrite = DateTime.Now.ToShortDateString () + " " + DateTime.Now.ToLongTimeString () + " - " +
                    presence.Name + " entered " +
                    presence.Scene.RegionInfo.RegionName + ".";

                m_streamWriter.WriteLine (lineToWrite);
                m_streamWriter.Close ();
            } catch {
                m_streamWriter.Close ();
            }
            stream.Close ();
        }

        #endregion
    }
}
