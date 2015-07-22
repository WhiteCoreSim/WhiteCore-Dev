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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Restart
{
    public class RestartModule : INonSharedRegionModule, IRestartModule
    {
        protected List<int> m_Alerts;
        protected Timer m_CountdownTimer;
        protected IDialogModule m_DialogModule;
        protected UUID m_Initiator;
        protected string m_Message;
        protected bool m_Notice;
        protected DateTime m_RestartBegin;
        protected IScene m_scene;
       
        string m_storeDirectory = "";

        #region INonSharedRegionModule Members

        public void Initialise (IConfigSource config)
        {
            IConfig rs_config = config.Configs ["FileBasedSimulationData"];
            if (rs_config != null)
            {
                m_storeDirectory = PathHelpers.ComputeFullPath (rs_config.GetString ("StoreBackupDirectory", m_storeDirectory));

                AddConsoleCommands ();
            }
        }

        public void AddRegion (IScene scene)
        {
            m_scene = scene;
            scene.RegisterModuleInterface<IRestartModule> (this);
        }

        public void RegionLoaded (IScene scene)
        {
            m_DialogModule = m_scene.RequestModuleInterface<IDialogModule> ();
            m_scene.EventManager.OnStartupFullyComplete += EventManager_OnStartupFullyComplete;
        }

        void EventManager_OnStartupFullyComplete (IScene scene, List<string> data)
        {
            DeserializeUsers (scene);
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public void Close ()
        {
        }

        public string Name
        {
            get { return "RestartModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return typeof(IRestartModule); }
        }


        #endregion

        #region IRestartModule Members

        /// <summary>
        /// Handles the help command.
        /// </summary>
        /// <param name="scene">Not used</param>
        /// <param name="cmd">Not used</param>
        void HandleHelp (IScene scene, string[] cmd)
        {
            MainConsole.Instance.Info (
                "restart all regions  [<time (in seconds)> [<message>]]\n" +
                "  Restart all simulator regions.\n Optionally delay <secs> displaying the <message> to users");

            MainConsole.Instance.Info (
                "restart region  [<time (in seconds)> [<message>]]\n" +
                "  Restart the currently selected region.\n Optionally delay <secs> displaying the <message> to users");

            MainConsole.Instance.Info (
                "restart region abort <message>\n" +
                "  Aborts a scheduled restart displaying the <message> to users");

        }

        /// <summary>
        /// Adds the console commands.
        /// </summary>
        void AddConsoleCommands ()
        {
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "restart all regions",
                    "restart all regions [<time (in seconds)> [message]]",
                    "Restart all simulator regions ",
                    HandleRegionRestart, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "restart region",
                    "restart region  [<time (in seconds)> [message]]",
                    "Restart the region",
                    HandleRegionRestart, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "restart region abort",
                    "restart region abort [message]",
                    "Abort the region restart",
                    HandleRegionRestartAbort, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "restart region help",
                    "restart region help",
                    "Help about the region restart command.",
                    HandleHelp, false, true);

            }
        }

        public TimeSpan TimeUntilRestart
        {
            get { return DateTime.Now - m_RestartBegin; }
        }

        public void ScheduleRestart (UUID initiator, string message, int[] alerts, bool notice)
        {
            if (alerts.Length == 0)
            {
                AbortRestart ("[Restart]: Region restart aborted");
                return;
            }

            if (m_CountdownTimer != null)
            {
                MainConsole.Instance.Warn ("[Restart]: Resetting the restart timer for new settings.");
                m_CountdownTimer.Stop ();
                m_CountdownTimer = null;
            }

            if (alerts == null)
            {
                RestartScene ();
                return;
            }

            m_Message = message;
            m_Initiator = initiator;
            m_Notice = notice;
            m_Alerts = new List<int> (alerts);
            m_Alerts.Sort ();
            m_Alerts.Reverse ();

            if (m_Alerts [0] == 0)
            {
                RestartScene ();
                return;
            }

            int nextInterval = DoOneNotice ();

            SetTimer (nextInterval);
        }

        public void AbortRestart (string message)
        {
            if (m_CountdownTimer != null)
            {
                m_CountdownTimer.Stop ();
                m_CountdownTimer = null;
                if (m_DialogModule != null && message != String.Empty)
                    m_DialogModule.SendGeneralAlert (message);

                MainConsole.Instance.Warn ("[Restart]: Region restart aborted");
            }
        }

        /// <summary>
        ///     This causes the region to restart immediately.
        /// </summary>
        public void RestartScene ()
        {

            MainConsole.Instance.Error ("[Restart]: Restarting " + m_scene.RegionInfo.RegionName);
            restartRegionSerialized (m_scene);
        }

        #endregion

        public int DoOneNotice ()
        {
            if (m_Alerts.Count == 0 || m_Alerts [0] == 0)
            {
                RestartScene ();
                return 0;
            }

            int nextAlert = 0;
            while (m_Alerts.Count > 1)
            {
                if (m_Alerts [1] == m_Alerts [0])
                {
                    m_Alerts.RemoveAt (0);
                    continue;
                }
                nextAlert = m_Alerts [1];
                break;
            }

            int currentAlert = m_Alerts [0];

            m_Alerts.RemoveAt (0);

            int minutes = currentAlert / 60;
            string currentAlertString = String.Empty;
            if (minutes > 0)
            {
                if (minutes == 1)
                    currentAlertString += "1 minute";
                else
                    currentAlertString += String.Format ("{0} minutes", minutes);
                if ((currentAlert % 60) != 0)
                    currentAlertString += " and ";
            }
            if ((currentAlert % 60) != 0)
            {
                int seconds = currentAlert % 60;
                if (seconds == 1)
                    currentAlertString += "1 second";
                else
                    currentAlertString += String.Format ("{0} seconds", seconds);
            }

            string msg = String.Format (m_Message, currentAlertString);

            if (m_DialogModule != null && msg != String.Empty)
            {
                if (m_Notice)
                    m_DialogModule.SendGeneralAlert (msg);
                else
                    m_DialogModule.SendNotificationToUsersInRegion (m_Initiator, "System", msg);
                MainConsole.Instance.WarnFormat ("[Region Restart]: {0} will restart in {1}",
                    m_scene.RegionInfo.RegionName, currentAlertString);
            }

            return currentAlert - nextAlert;
        }

        public void SetTimer (int intervalSeconds)
        {
            if (intervalSeconds == 0)
                return;
            m_CountdownTimer = new Timer { AutoReset = false, Interval = intervalSeconds * 1000 };
            m_CountdownTimer.Elapsed += OnTimer;
            m_CountdownTimer.Start ();
        }

        void OnTimer (object source, ElapsedEventArgs e)
        {
            int nextInterval = DoOneNotice ();

            SetTimer (nextInterval);
        }

        #region serialization

        void restartRegionSerialized (IScene scene)
        {
            var simBase = scene.RequestModuleInterface<ISimulationBase> ();
            var sceneManager = simBase.ApplicationRegistry.RequestModuleInterface<ISceneManager> ();

            SerializeUsers (scene);

            scene.CloseQuietly = true;
            sceneManager.RestartRegion (scene, false);

            DeserializeUsers (scene);

        }

        void SerializeUsers (IScene scene)
        {
            List<IScenePresence> sceneAgents = scene.GetScenePresences ();
            if (sceneAgents.Count == 0)
                return;
            
            var regionUsers = 0;
            OSDMap userMap = new OSDMap ();
            foreach (IScenePresence presence in sceneAgents)
            {
                OSDMap user = new OSDMap ();
                OSDMap remoteIP = new OSDMap ();

                remoteIP ["Address"] = presence.ControllingClient.RemoteEndPoint.Address.ToString ();
                remoteIP ["Port"] = presence.ControllingClient.RemoteEndPoint.Port;

                user ["RemoteEndPoint"] = remoteIP;
                user ["ClientInfo"] = presence.ControllingClient.RequestClientInfo ().ToOSD ();
                user ["Position"] = presence.AbsolutePosition;
                user ["IsFlying"] = presence.PhysicsActor.Flying;

                userMap [presence.UUID.ToString ()] = user;

                regionUsers++;
            }

            var regionName = scene.RegionInfo.RegionName;
            var sceneFile = BuildSaveFileName (regionName);

            File.WriteAllText (sceneFile, OSDParser.SerializeJsonString (userMap));
            MainConsole.Instance.InfoFormat ("[Restart]: {0} users saved for {1}", regionUsers, regionName);

        }

        void DeserializeUsers (IScene scene)
        {
            var regionName = scene.RegionInfo.RegionName;
            var readFile = BuildSaveFileName (regionName);
            if (!File.Exists (readFile))
                return;

            var regionUsers = 0;
            OSDMap sceneAgents = (OSDMap)OSDParser.DeserializeJson (File.ReadAllText (readFile));
            foreach (OSD o in sceneAgents.Values)
            {
                AgentCircuitData data = new AgentCircuitData ();
                OSDMap user = (OSDMap)o;

                data.FromOSD ((OSDMap)user ["ClientInfo"]);
                m_scene.AuthenticateHandler.AddNewCircuit (data.CircuitCode, data);
                OSDMap remoteIP = (OSDMap)user ["RemoteEndPoint"];
                IPEndPoint ep = new IPEndPoint (IPAddress.Parse (remoteIP ["Address"].AsString ()), remoteIP ["Port"].AsInteger ());

                m_scene.ClientServers [0].AddClient (data.CircuitCode, data.AgentID, data.SessionID, ep, data);
                IScenePresence sp = m_scene.GetScenePresence (data.AgentID);
                sp.MakeRootAgent (user ["Position"].AsVector3 (), user ["IsFlying"].AsBoolean (), true);
                sp.SceneViewer.SendPresenceFullUpdate (sp);

                regionUsers += 1;
            }

            File.Delete (readFile);
            MainConsole.Instance.InfoFormat ("[Restart]: {0} users loaded into {1}", regionUsers, regionName);
        }

        string BuildSaveFileName (string regionName)
        {
            var saveFile = regionName + ".siminfo";
            return Path.Combine (m_storeDirectory, saveFile);
        }

        #endregion

        #region console commands

        /// <summary>
        /// Handles the region restart command
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleRegionRestart (IScene scene, string[] args)
        {
            bool allRegions = false;
            int seconds = 0;
            string message = " will restart in {0}";
            string scnName = "";
            if (scene != null)
                scnName = scene.RegionInfo.RegionName;

            // check if this for all scenes
            if ((args.Length > 1) && (args [1].ToLower () == "all"))
            {
                allRegions = true;
                var newargs = new List<string> (args);
                newargs.RemoveAt (1);
                args = newargs.ToArray ();
                scnName = "all regions";
            }

            if (args.Length < 3)
            {
               
                if (MainConsole.Instance.Prompt ("[Restart]: Do you wish to restart " + scnName +
                    " immediately? (yes/no)", "no") != "yes")
                {
                    MainConsole.Instance.Info ("usage: region restart <time> [message]");
                    return;
                }
            }

            // do we have a time?   
            if (args.Length > 2)
            {
                if (!int.TryParse (args [2], out seconds))
                {
                    MainConsole.Instance.Error ("[Restart]: Unable to determine restart delay!");
                    return;
                }
            }

            // build message interval list
            List<int> times = new List<int> ();
            while (seconds > 0)
            {
                times.Add (seconds);
                if (seconds > 300)
                    seconds -= 120;
                else if (seconds > 30)
                    seconds -= 30;
                else
                    seconds -= 15;
            }
            times.Add (0);              // we should always have a 'zero' for the immediate request

            // have a message?
            if (args.Length > 3)
                message = Util.CombineParams (args, 4);   // assume everything else is the message

            if (!allRegions)
            {
                // we have a specified region
                IRestartModule restartModule = scene.RequestModuleInterface<IRestartModule> ();
                if (restartModule == null)
                {
                    MainConsole.Instance.Error ("[Restart]: Unable to locate restart module for " + scnName);
                    return;
                }
                restartModule.ScheduleRestart (UUID.Zero, scnName + message, times.ToArray (), true);
            } else
            {
                int offset = 0;
                // check for immediate restart
                if ((times.Count == 1) && (times [0] == 0))
                    times [0] = 2;                              // delay initial restart by 2 seconds

                foreach (IScene scn in MainConsole.Instance.ConsoleScenes)
                {
                    for (int i = 0; i < times.Count; i++)
                    {
                        times [i] = times [i] + 5;               // stagger each alert/restart by 5 seconds    
                    }   
 
                    scnName = scn.RegionInfo.RegionName;

                    IRestartModule sceneRestart = scn.RequestModuleInterface<IRestartModule> ();
                    if (sceneRestart == null)
                        MainConsole.Instance.Error ("[Restart]: Unable to locate restart module for " + scnName);
                    else
                    {
                        sceneRestart.ScheduleRestart (UUID.Zero, scnName + message, times.ToArray (), true);
                        offset++;
                    }
                }
            }
          
        }

        /// <summary>
        /// Handles the region restart abort command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleRegionRestartAbort (IScene scene, string[] args)
        {
            IRestartModule restartModule = scene.RequestModuleInterface<IRestartModule> ();
            if (restartModule == null)
            {
                MainConsole.Instance.Error ("[Restart]: Unable to locate restart module for this scene");
                return;
            }

            string msg = "Restart aborted";
            if (args.Length > 3)
                msg = Util.CombineParams (args, 4);   // assume everything else is the message
                    
            // are we aborting a scheduled restart?
            if (m_Alerts != null)
                AbortRestart (msg);
            else
                MainConsole.Instance.Info ("[Restart]: Abort ignored as no restart is in progress");

        }

        #endregion
    }
}