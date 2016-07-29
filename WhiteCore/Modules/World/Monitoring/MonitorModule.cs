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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using WhiteCore.Modules.Monitoring.Alerts;
using WhiteCore.Modules.Monitoring.Monitors;
using ThreadState = System.Diagnostics.ThreadState;
using Timer = System.Timers.Timer;

namespace WhiteCore.Modules.Monitoring
{
    public class MonitorModule : IApplicationPlugin, IMonitorModule
    {
        #region Declares

        protected MonitorRegistry m_registry;
        protected Dictionary<UUID, MonitorRegistry> m_regionRegistry = new Dictionary<UUID, MonitorRegistry> ();
        protected ISimulationBase m_simulationBase;

        #region Enums

        public enum Stats : uint
        {
            TimeDilation,
            FPS,
            PhysFPS,
            AgentUpdates,
            FrameMS,
            NetMS,
            SimOtherMS,
            SimPhysicsMS,
            AgentMS,
            ImagesMS,
            ScriptMS,
            TotalObjects,
            ActiveObjects,
            NumAgentMain,
            NumAgentChild,
            NumScriptActive,
            LSLIPS, //Lines per second
            InPPS, //Packets per second
            OutPPS, //Packets per second
            PendingDownloads,
            PendingUploads,
            VirtualSizeKB,
            ResidentSizeKB,
            PendingLocalUploads,
            TotalUnackedBytes,
            PhysicsPinnedTasks,
            PhysicsLODTasks,
            SimPhysicsStepMS,
            SimPhysicsShape,
            SimPhysicsOtherMS,
            SimPhysicsMemory,
            ScriptEPS, //Events per second
            SimSpareTime,
            SimSleepTime,
            IOPumpTime
        }

        #endregion

        #region Events

        public event SendStatResult OnSendStatsResult;

        #endregion

        public sealed class MonitorRegistry : IDisposable
        {
            #region Declares

            private readonly float [] lastReportedSimStats = new float [35];
            private readonly MonitorModule m_module;
            private readonly Timer m_report = new Timer ();
            private Dictionary<string, IAlert> m_alerts = new Dictionary<string, IAlert> ();
            private IScene m_currentScene;
            //The estate module to pull out the region flags
            private IEstateModule m_estateModule;
            private Dictionary<string, IMonitor> m_monitors = new Dictionary<string, IMonitor> ();
            private float statsUpdateFactor = 2;
            private int statsUpdatesEveryMS = 2000;

            /// <summary>
            ///     The last reported stats for this region
            /// </summary>
            public float [] LastReportedSimStats {
                get { return lastReportedSimStats; }
            }

            #endregion

            #region Constructor

            /// <summary>
            ///     Constructor, set the MonitorModule ref up
            /// </summary>
            /// <param name="module"></param>
            public MonitorRegistry (MonitorModule module)
            {
                m_module = module;
                for (int i = 0; i < sb.Length; i++) {
                    sb [i] = new SimStatsPacket.StatBlock ();
                }

                //As we aren't a scene, we add all of the monitors that do not need the scene and run for the entire instance
                AddDefaultMonitors ();
            }

            #endregion

            #region Add Region

            /// <summary>
            ///     Set the scene for this instance, add the HTTP handler for the monitor stats,
            ///     add all the given monitors and alerts, and start the stats heartbeat.
            /// </summary>
            /// <param name="scene"></param>
            public void AddScene (IScene scene)
            {
                if (scene != null) {
                    m_currentScene = scene;
                    //Add the HTTP handler
                    MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("GET",
                                                                                "/monitorstats/" +
                                                                                scene.RegionInfo.RegionID + "/",
                                                                                StatsPage));
                    //Add all of the region monitors
                    AddRegionMonitors (scene);

                    //Set the correct update time
                    SetUpdateMS (2000);
                    //Start the stats heartbeat
                    m_report.AutoReset = false;
                    m_report.Interval = statsUpdatesEveryMS;
                    m_report.Elapsed += statsHeartBeat;
                    m_report.Enabled = true;
                }
            }

            public void Close ()
            {
                if (m_currentScene != null) {
                    //Kill the stats heartbeat and http handler
                    m_report.Stop ();
                    MainServer.Instance.RemoveStreamHandler ("GET", "/monitorstats/" + m_currentScene.RegionInfo.RegionID + "/");
                }
                //Remove all monitors/alerts
                m_alerts.Clear ();
                m_monitors.Clear ();
            }

            #endregion

            #region Default

            /// <summary>
            ///     Add the monitors that are for the entire instance
            /// </summary>
            private void AddDefaultMonitors ()
            {
                AddMonitor (new AssetMonitor ());
                AddMonitor (new GCMemoryMonitor ());
                AddMonitor (new LoginMonitor ());
                AddMonitor (new PWSMemoryMonitor ());
                AddMonitor (new ThreadCountMonitor ());
            }

            /// <summary>
            ///     Add the monitors that are for each scene
            /// </summary>
            /// <param name="scene"></param>
            private void AddRegionMonitors (IScene scene)
            {
                AddMonitor (new AgentCountMonitor (scene));
                AddMonitor (new AgentUpdateMonitor (scene));
                AddMonitor (new ChildAgentCountMonitor (scene));
                AddMonitor (new ImageFrameTimeMonitor (scene));
                AddMonitor (new LastFrameTimeMonitor (scene));
                AddMonitor (new NetworkMonitor (scene));
                AddMonitor (new ObjectCountMonitor (scene));
                AddMonitor (new OtherFrameMonitor (scene));
                AddMonitor (new ObjectUpdateMonitor (scene));
                AddMonitor (new PhysicsFrameMonitor (scene));
                AddMonitor (new PhysicsUpdateFrameMonitor (scene));
                AddMonitor (new PhysicsSyncFrameMonitor (scene));
                AddMonitor (new ScriptCountMonitor (scene));
                AddMonitor (new ScriptFrameTimeMonitor (scene));
                AddMonitor (new SimFrameMonitor (scene));
                AddMonitor (new SleepFrameMonitor (scene));
                AddMonitor (new TimeDilationMonitor (scene));
                AddMonitor (new TotalFrameMonitor (scene));

                AddAlert (new DeadlockAlert (GetMonitor<ILastFrameTimeMonitor> ()));
            }

            #endregion

            #region Add/Remove/Get Monitor and Alerts

            /// <summary>
            ///     Add a new monitor to the monitor list
            /// </summary>
            /// <param name="monitor"></param>
            public void AddMonitor (IMonitor monitor)
            {
                if (monitor.GetInterfaceName () == "")
                    m_monitors.Add (monitor.GetName (), monitor);
                else
                    m_monitors.Add (monitor.GetInterfaceName (), monitor);
            }

            /// <summary>
            ///     Add a new alert
            /// </summary>
            /// <param name="alert"></param>
            public void AddAlert (IAlert alert)
            {
                alert.OnTriggerAlert += OnTriggerAlert;
                m_alerts.Add (alert.GetName (), alert);
            }

            /// <summary>
            ///     Remove a known monitor from the list
            /// </summary>
            /// <param name="Name"></param>
            public void RemoveMonitor (string Name)
            {
                m_monitors.Remove (Name);
            }

            /// <summary>
            ///     Remove a known alert from the list
            /// </summary>
            /// <param name="Name"></param>
            public void RemoveAlert (string Name)
            {
                m_alerts.Remove (Name);
            }

            /// <summary>
            ///     Get a known monitor from the list, if not known, it will return null
            /// </summary>
            /// <returns></returns>
            public T GetMonitor<T> () where T : IMonitor
            {
                IMonitor m;
                if (m_monitors.TryGetValue (typeof (T).Name, out m))
                    return (T)m;
                return default (T);
            }

            /// <summary>
            ///     Gets a known alert from the list, if not known, it will return null
            /// </summary>
            /// <param name="Name"></param>
            /// <returns></returns>
            public IAlert GetAlert (string Name)
            {
                if (m_alerts.ContainsKey (Name))
                    return m_alerts [Name];
                return null;
            }

            #endregion

            #region Trigger Alert

            /// <summary>
            ///     This occurs when the alert has been triggered and it alerts the console about it
            /// </summary>
            /// <param name="reporter"></param>
            /// <param name="reason"></param>
            /// <param name="fatal"></param>
            private void OnTriggerAlert (Type reporter, string reason, bool fatal)
            {
                string regionName = m_currentScene != null ? " for " + m_currentScene.RegionInfo.RegionName : "";
                MainConsole.Instance.Error ("[Monitor] " + reporter.Name + regionName + " reports " + reason +
                                           " (Fatal: " + fatal + ")");
            }

            #endregion

            #region Report

            /// <summary>
            ///     Create a string that has the info from all monitors that we know of
            /// </summary>
            /// <returns></returns>
            public string Report ()
            {
                string report = "";
                foreach (IMonitor monitor in m_monitors.Values) {
                    string regionName = m_currentScene != null ? m_currentScene.RegionInfo.RegionName + ": " : "";
                    report += regionName + monitor.GetName () + " reports " + monitor.GetFriendlyValue () + "\n";
                }
                return report;
            }

            #endregion

            #region HTTP Stats page

            /// <summary>
            ///     Return a hashable of the monitors, or just the ones requested
            /// </summary>
            /// <param name="path"></param>
            /// <param name="request"></param>
            /// <param name="httpRequest"></param>
            /// <param name="httpResponse"></param>
            /// <returns></returns>
            public byte [] StatsPage (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                // If request was for a specific monitor
                // e.g. url/?monitor=Monitor.Name
                if (httpRequest.QueryString.Get ("monitor") != null) {
                    string monID = httpRequest.QueryString.Get ("monitor");

                    foreach (IMonitor monitor in m_monitors.Values) {
                        string elemName = monitor.ToString ();
                        if (elemName.StartsWith (monitor.GetType ().Namespace, StringComparison.Ordinal))
                            elemName = elemName.Substring (monitor.GetType ().Namespace.Length + 1);
                        if (elemName == monID || monitor.ToString () == monID) {
                            httpResponse.ContentType = "text/plain";
                            return Encoding.UTF8.GetBytes (monitor.GetValue ().ToString ());
                        }
                    }

                    // No monitor with that name
                    httpResponse.ContentType = "text/plain";
                    return Encoding.UTF8.GetBytes ("No such monitor");
                }

                string xml = "<data>";
                foreach (IMonitor monitor in m_monitors.Values) {
                    string elemName = monitor.ToString ();
                    if (elemName.StartsWith (monitor.GetType ().Namespace, StringComparison.Ordinal))
                        elemName = elemName.Substring (monitor.GetType ().Namespace.Length + 1);

                    xml += "<" + elemName + ">" + monitor.GetValue () + "</" + elemName + ">";
                }
                xml += "</data>";

                httpResponse.ContentType = "text/xml";
                return Encoding.UTF8.GetBytes (xml);
            }

            #endregion

            #region Stats Heartbeat

            private readonly SimStatsPacket.StatBlock [] sb = new SimStatsPacket.StatBlock [35];
            private SimStatsPacket.RegionBlock rb;

            private void buildInitialRegionBlock ()
            {
                rb = new SimStatsPacket.RegionBlock ();
                uint regionFlags = 0;

                try {
                    if (m_estateModule == null)
                        m_estateModule = m_currentScene.RequestModuleInterface<IEstateModule> ();
                    regionFlags = m_estateModule != null ? (uint)m_estateModule.GetRegionFlags () : 0;
                } catch (Exception) {
                    // leave region flags at 0
                }
                rb.ObjectCapacity = (uint)m_currentScene.RegionInfo.ObjectCapacity;
                rb.RegionFlags = regionFlags;
                rb.RegionX = (uint)m_currentScene.RegionInfo.RegionLocX / Constants.RegionSize;
                rb.RegionY = (uint)m_currentScene.RegionInfo.RegionLocY / Constants.RegionSize;
            }

            /// <summary>
            ///     This is called by a timer and makes a SimStats class of the current stats that we have in this simulator.
            ///     It then sends the packet to the client and triggers the events to tell followers about the updated stats
            ///     and updates the LastSet* values for monitors.
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void statsHeartBeat (object sender, EventArgs e)
            {
                if (m_currentScene.PhysicsScene == null)
                    return;
                m_report.Stop ();
                if (rb == null)
                    buildInitialRegionBlock ();

                // Know what's not thread safe in Mono... modifying timers.
                lock (m_report) {
                    ISimFrameMonitor simFrameMonitor = GetMonitor<ISimFrameMonitor> ();
                    ITimeDilationMonitor timeDilationMonitor = GetMonitor<ITimeDilationMonitor> ();
                    ITotalFrameTimeMonitor totalFrameMonitor = GetMonitor<ITotalFrameTimeMonitor> ();
                    ISleepFrameMonitor sleepFrameMonitor = GetMonitor<ISleepFrameMonitor> ();
                    IOtherFrameMonitor otherFrameMonitor = GetMonitor<IOtherFrameMonitor> ();
                    IPhysicsFrameMonitor physicsFrameMonitor = GetMonitor<IPhysicsFrameMonitor> ();
                    IPhysicsSyncFrameMonitor physicsSyncFrameMonitor = GetMonitor<IPhysicsSyncFrameMonitor> ();
                    IPhysicsUpdateFrameMonitor physicsTimeFrameMonitor = GetMonitor<IPhysicsUpdateFrameMonitor> ();
                    IAgentUpdateMonitor agentUpdateFrameMonitor = GetMonitor<IAgentUpdateMonitor> ();
                    INetworkMonitor networkMonitor = GetMonitor<INetworkMonitor> ();
                    IImageFrameTimeMonitor imagesMonitor = GetMonitor<IImageFrameTimeMonitor> ();
                    IScriptFrameTimeMonitor scriptMonitor = GetMonitor<IScriptFrameTimeMonitor> ();
                    IScriptCountMonitor totalScriptMonitor = GetMonitor<IScriptCountMonitor> ();

                    #region various statistic googly moogly

                    float simfps = simFrameMonitor.SimFPS / statsUpdateFactor;
                    // save the reported value so there is something available for llGetRegionFPS 
                    simFrameMonitor.LastReportedSimFPS = simfps;

                    float physfps = physicsFrameMonitor.PhysicsFPS / statsUpdateFactor;
                    physicsFrameMonitor.LastReportedPhysicsFPS = physfps;
                    //Update the time dilation with the newest physicsFPS
                    timeDilationMonitor.SetPhysicsFPS (physfps);

                    #endregion

                    #region Add the stats packets

                    //Some info on this packet http://wiki.secondlife.com/wiki/Statistics_Bar_Guide

                    sb [0].StatID = (uint)Stats.TimeDilation;
                    sb [0].StatValue = (float)timeDilationMonitor.GetValue ();

                    sb [1].StatID = (uint)Stats.FPS;
                    sb [1].StatValue = simfps;

                    float realsimfps = simfps * 2;

                    sb [2].StatID = (uint)Stats.PhysFPS;
                    sb [2].StatValue = physfps;

                    sb [3].StatID = (uint)Stats.AgentUpdates;
                    sb [3].StatValue = (agentUpdateFrameMonitor.AgentUpdates / realsimfps);

                    sb [4].StatID = (uint)Stats.FrameMS;
                    float TotalFrames = (float)(totalFrameMonitor.GetValue () / realsimfps);
                    sb [4].StatValue = TotalFrames;

                    sb [5].StatID = (uint)Stats.NetMS;
                    sb [5].StatValue = 0; //TODO: Implement this

                    sb [6].StatID = (uint)Stats.SimOtherMS;
                    float otherMS = (float)(otherFrameMonitor.GetValue () / realsimfps);
                    sb [6].StatValue = otherMS;

                    sb [7].StatID = (uint)Stats.SimPhysicsMS;
                    float PhysicsMS = (float)(physicsTimeFrameMonitor.GetValue () / realsimfps);
                    sb [7].StatValue = PhysicsMS;

                    sb [8].StatID = (uint)Stats.AgentMS;
                    sb [8].StatValue = (agentUpdateFrameMonitor.AgentFrameTime / realsimfps);

                    sb [9].StatID = (uint)Stats.ImagesMS;
                    float imageMS = (float)(imagesMonitor.GetValue () / realsimfps);
                    sb [9].StatValue = imageMS;

                    sb [10].StatID = (uint)Stats.ScriptMS;
                    float ScriptMS = (float)(scriptMonitor.GetValue () / realsimfps);
                    sb [10].StatValue = ScriptMS;

                    sb [11].StatID = (uint)Stats.TotalObjects;
                    sb [12].StatID = (uint)Stats.ActiveObjects;
                    sb [13].StatID = (uint)Stats.NumAgentMain;
                    sb [14].StatID = (uint)Stats.NumAgentChild;

                    IEntityCountModule entityCountModule = m_currentScene.RequestModuleInterface<IEntityCountModule> ();
                    if (entityCountModule != null) {
                        sb [11].StatValue = entityCountModule.Objects;

                        sb [12].StatValue = entityCountModule.ActiveObjects;

                        sb [13].StatValue = entityCountModule.RootAgents;

                        sb [14].StatValue = entityCountModule.ChildAgents;
                    }

                    sb [15].StatID = (uint)Stats.NumScriptActive;
                    sb [15].StatValue = totalScriptMonitor.ActiveScripts;

                    sb [16].StatID = (uint)Stats.LSLIPS;
                    sb [16].StatValue = 0; //This isn't used anymore, and has been superseded by LSLEPS

                    sb [17].StatID = (uint)Stats.InPPS;
                    sb [17].StatValue = (networkMonitor.InPacketsPerSecond / statsUpdateFactor);

                    sb [18].StatID = (uint)Stats.OutPPS;
                    sb [18].StatValue = (networkMonitor.OutPacketsPerSecond / statsUpdateFactor);

                    sb [19].StatID = (uint)Stats.PendingDownloads;
                    sb [19].StatValue = (networkMonitor.PendingDownloads);

                    sb [20].StatID = (uint)Stats.PendingUploads;
                    sb [20].StatValue = (networkMonitor.PendingUploads);

                    //21 and 22 are forced to the GC memory as they WILL make memory usage go up rapidly otherwise!
                    sb [21].StatID = (uint)Stats.VirtualSizeKB;
                    sb [21].StatValue = GC.GetTotalMemory (false) / 1024;
                    // System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024);

                    sb [22].StatID = (uint)Stats.ResidentSizeKB;
                    sb [22].StatValue = GC.GetTotalMemory (false) / 1024;
                    //(float)System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / (1024);

                    sb [23].StatID = (uint)Stats.PendingLocalUploads;
                    sb [23].StatValue = (networkMonitor.PendingUploads / statsUpdateFactor);

                    sb [24].StatID = (uint)Stats.TotalUnackedBytes;
                    sb [24].StatValue = (networkMonitor.UnackedBytes);

                    sb [25].StatID = (uint)Stats.PhysicsPinnedTasks;
                    sb [25].StatValue = 0;

                    sb [26].StatID = (uint)Stats.PhysicsLODTasks;
                    sb [26].StatValue = 0;

                    sb [27].StatID = (uint)Stats.SimPhysicsStepMS;
                    sb [27].StatValue = m_currentScene.PhysicsScene.StepTime;

                    sb [28].StatID = (uint)Stats.SimPhysicsShape;
                    sb [28].StatValue = 0;

                    sb [29].StatID = (uint)Stats.SimPhysicsOtherMS;
                    sb [29].StatValue = (float)(physicsSyncFrameMonitor.GetValue () / realsimfps);

                    sb [30].StatID = (uint)Stats.SimPhysicsMemory;
                    sb [30].StatValue = 0;

                    sb [31].StatID = (uint)Stats.ScriptEPS;
                    sb [31].StatValue = totalScriptMonitor.ScriptEPS / statsUpdateFactor;

                    sb [32].StatID = (uint)Stats.SimSpareTime;
                    //Spare time is the total time minus the stats that are in the same category in the client
                    // It is the sleep time, physics step, update physics shape, physics other, and pumpI0.
                    // Note: take out agent Update and script time for now, as they are not a part of the heartbeat right now and will mess this calc up
                    float SpareTime = TotalFrames - (
                                                        /*NetMS + */ PhysicsMS + otherMS + imageMS);
                    //                         + /*(agentUpdateFrameMonitor.AgentFrameTime / statsUpdateFactor) +*/
                    //                        (imagesMonitor.GetValue() / statsUpdateFactor) /* + ScriptMS*/));

                    sb [32].StatValue = SpareTime;

                    sb [33].StatID = (uint)Stats.SimSleepTime;
                    sb [33].StatValue = (float)(sleepFrameMonitor.GetValue () / realsimfps);

                    sb [34].StatID = (uint)Stats.IOPumpTime;
                    sb [34].StatValue = 0; //TODO: implement this

                    #endregion

                    for (int i = 0; i < sb.Length; i++) {
                        if (float.IsInfinity (sb [i].StatValue) ||
                            float.IsNaN (sb [i].StatValue))
                            sb [i].StatValue = 0; //Don't send huge values
                        lastReportedSimStats [i] = sb [i].StatValue;
                    }

                    SimStats simStats
                        = new SimStats (rb, sb, m_currentScene.RegionInfo.RegionID);

                    //Fire the event and tell followers about the new stats
                    m_module.SendStatsResults (simStats);

                    //Tell all the scene presences about the new stats
                    foreach (IScenePresence agent in m_currentScene.GetScenePresences ().Where (agent => !agent.IsChildAgent)) {
                        agent.ControllingClient.SendSimStats (simStats);
                    }

                    //Now fix any values that require resetting
                    ResetValues ();
                }
                m_report.Start ();
            }

            /// <summary>
            ///     Reset the values of the stats that require resetting (ones that use += and not =)
            /// </summary>
            public void ResetValues ()
            {
                foreach (IMonitor m in m_monitors.Values) {
                    m.ResetStats ();
                }
            }

            /// <summary>
            ///     Set the correct update time for the timer
            /// </summary>
            /// <param name="ms"></param>
            public void SetUpdateMS (int ms)
            {
                statsUpdatesEveryMS = ms;

                lock (m_report) {
                    statsUpdateFactor = (statsUpdatesEveryMS / 1000);
                    m_report.Interval = statsUpdatesEveryMS;
                }
            }

            #endregion

            #region IDisposable

            public void Dispose ()
            {
                m_report.Close ();
            }

            #endregion
        }

        #endregion

        #region Get

        /// <summary>
        ///     Get a monitor from the given Scene (RegionID or "" for the base instance) 
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public T GetMonitor<T> (IScene scene) where T : IMonitor
        {
            if (scene == null)
                return m_registry.GetMonitor<T> ();
            
            lock (m_regionRegistry)
                return m_regionRegistry [scene.RegionInfo.RegionID].GetMonitor<T> ();
        }

        /// <summary>
        ///     Get the latest region stats for the given regionID
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public float [] GetRegionStats (IScene scene)
        {
            lock (m_regionRegistry)
                return m_regionRegistry [scene.RegionInfo.RegionID].LastReportedSimStats;
        }

        #endregion

        #region Console

        /// <summary>
        ///     This shows stats for ALL regions in the instance
        /// </summary>
        /// <param name="args"></param>
        protected void DebugMonitors (IScene scene, string [] args)
        {
            //Dump all monitors to the console
            if (scene != null) {
                lock (m_regionRegistry)
                    MainConsole.Instance.Info ("[Stats] " + m_regionRegistry [scene.RegionInfo.RegionID].Report ());
            } else
                MainConsole.Instance.Info ("[Stats] " + m_registry.Report ());
            MainConsole.Instance.Info ("");
        }

        /// <summary>
        ///     This shows the stats for the given region
        /// </summary>
        /// <param name="args"></param>
        protected void DebugMonitorsInCurrentRegion (IScene scene, string [] args)
        {
            ISceneManager manager = m_simulationBase.ApplicationRegistry.RequestModuleInterface<ISceneManager> ();
            if (manager != null) {
                if (scene != null) {
                    lock (m_regionRegistry)
                        MainConsole.Instance.Info ("[Stats] " + m_regionRegistry [scene.RegionInfo.RegionID].Report ());
                } else
                    MainConsole.Instance.Info ("[Stats] " + m_registry.Report ());
                MainConsole.Instance.Info ("");
            } else {
                //Dump all the monitors to the console
                DebugMonitors (scene, args);
            }
        }

        protected void HandleShowThreads (IScene scene, string [] cmd)
        {
            if (Util.IsLinux)
                MainConsole.Instance.Warn ("Thread process statistics may not be fully implemented with Mono");

            MainConsole.Instance.Info (GetThreadsReport ());
        }

        protected void HandleShowUptime (IScene scene, string [] cmd)
        {
            MainConsole.Instance.Info (GetUptimeReport ());
        }

        protected void HandleShowStats (IScene scene, string [] cmd)
        {
            DebugMonitorsInCurrentRegion (scene, cmd);
        }

        protected void HandleShowQueues (IScene scene, string [] cmd)
        {
            List<string> args = new List<string> (cmd);
            args.RemoveAt (0);
            string [] showParams = args.ToArray ();
            MainConsole.Instance.Info (GetQueuesReport (showParams));
        }

        /// <summary>
        ///     Print UDP Queue data for each client
        /// </summary>
        /// <returns></returns>
        protected string GetQueuesReport (string [] showParams)
        {
            StringBuilder report = new StringBuilder ();

            int columnPadding = 2;
            int maxNameLength = 18;
            int maxRegionNameLength = 14;
            int maxTypeLength = 4;
            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding +
                                        maxTypeLength + columnPadding;

            report.AppendFormat ("{0,-" + maxNameLength + "}{1,-" + columnPadding + "}", "User", "");
            report.AppendFormat ("{0,-" + maxRegionNameLength + "}{1,-" + columnPadding + "}", "Region", "");
            report.AppendFormat ("{0,-" + maxTypeLength + "}{1,-" + columnPadding + "}", "Type", "");

            report.AppendFormat (
                "{0,9} {1,9} {2,9} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7} {10,7}\n",
                "Packets",
                "Packets",
                "Packets",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes");

            report.AppendFormat ("{0,-" + totalInfoFieldsLength + "}", "");
            report.AppendFormat (
                "{0,9} {1,9} {2,9} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7} {10,7}\n",
                "Out",
                "In",
                "Unacked",
                "Resend",
                "Land",
                "Wind",
                "Cloud",
                "Task",
                "Texture",
                "Asset",
                "State");

            return report.ToString ();
        }

        #endregion

        #region Diagonistics

        /// <summary>
        ///     Print statistics to the logfile, if they are active
        /// </summary>
        private void LogDiagnostics (object source, ElapsedEventArgs e)
        {
            MainConsole.Instance.Debug (LogDiagnostics ());
        }

        public string LogDiagnostics ()
        {
            StringBuilder sb = new StringBuilder ("DIAGNOSTICS\n\n");
            sb.Append (GetUptimeReport ());

            sb.Append (m_registry.Report ());

            sb.Append (Environment.NewLine);
            sb.Append (GetThreadsReport ());

            return sb.ToString ();
        }

        public ProcessThreadCollection GetThreads ()
        {
            Process thisProc = Process.GetCurrentProcess ();
            return thisProc.Threads;
        }

        /// <summary>
        ///     Get a report about the registered threads in this server.
        /// </summary>
        public string GetThreadsReport ()
        {
            StringBuilder sb = new StringBuilder ();

            ProcessThreadCollection threads = GetThreads ();
            if (threads == null) {
                sb.Append ("WhiteCore thread tracking is only enabled in DEBUG mode.");
            } else {
                sb.Append (threads.Count + " threads are being tracked:" + Environment.NewLine);
                foreach (ProcessThread t in threads) {
                    if (t != null) {
                        sb.Append ("ID: " + t.Id + ", TotalProcessorTime: " + t.TotalProcessorTime + ", TimeRunning: " +
                            (DateTime.Now - t.StartTime) + ", Pri: " + t.CurrentPriority + ", State: " + t.ThreadState);
                        if (t.ThreadState == ThreadState.Wait)
                            sb.Append (", Reason: " + t.WaitReason + Environment.NewLine);
                        else
                            sb.Append (Environment.NewLine);
                    }
                }
            }
            int workers = 0, ports = 0, maxWorkers = 0, maxPorts = 0;
            ThreadPool.GetAvailableThreads (out workers, out ports);
            ThreadPool.GetMaxThreads (out maxWorkers, out maxPorts);

            sb.Append (Environment.NewLine + "*** ThreadPool threads ***" + Environment.NewLine);
            sb.Append ("workers: " + (maxWorkers - workers) + " (" + maxWorkers + "); ports: " + (maxPorts - ports) +
                      " (" + maxPorts + ")" + Environment.NewLine);

            return sb.ToString ();
        }

        /// <summary>
        ///     Return a report about the uptime of this server
        /// </summary>
        /// <returns></returns>
        public string GetUptimeReport ()
        {
            StringBuilder sb = new StringBuilder (String.Format ("Time now is {0}\n", DateTime.Now));
            sb.Append (string.Format ("Server has been running since {0}, {1}\n", m_simulationBase.StartupTime.DayOfWeek,
                                    m_simulationBase.StartupTime));
            sb.Append (string.Format ("That is an elapsed time of {0}\n", DateTime.Now - m_simulationBase.StartupTime));

            return sb.ToString ();
        }

        #endregion

        #region IApplicationPlugin Members

        public void PreStartup (ISimulationBase simBase)
        {
        }

        Timer PeriodicDiagnosticsTimer;
        public void Initialize (ISimulationBase simulationBase)
        {
            m_simulationBase = simulationBase;

            PeriodicDiagnosticsTimer = new Timer (60 * 60 * 1000); // One hour
            PeriodicDiagnosticsTimer.Elapsed += LogDiagnostics;
            PeriodicDiagnosticsTimer.Enabled = true;
            PeriodicDiagnosticsTimer.Start ();

            if (MainConsole.Instance != null) {
                MainConsole.Instance.Commands.AddCommand (
                    "show threads",
                    "show threads",
                    "List tracked threads",
                    HandleShowThreads, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "show uptime",
                    "show uptime",
                    "Show server startup time and uptime",
                    HandleShowUptime, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "show queues",
                    "show queues [full]",
                    "Shows the queues for the given agent (if full is given as a parameter, child agents are displayed as well)",
                    HandleShowQueues, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "show stats",
                    "show stats",
                    "Show statistical information for this server",
                    HandleShowStats, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "stats report",
                    "stats report",
                    "Returns a variety of statistics about the current region and/or simulator",
                    DebugMonitors, false, true);
            }

            m_simulationBase.ApplicationRegistry.RegisterModuleInterface<IMonitorModule> (this);
            m_registry = new MonitorRegistry (this);
        }

        public void ReloadConfiguration (IConfigSource config)
        {
        }

        public void PostInitialise ()
        {
            ISceneManager manager = m_simulationBase.ApplicationRegistry.RequestModuleInterface<ISceneManager> ();
            if (manager != null) {
                manager.OnAddedScene += OnAddedScene;
                manager.OnCloseScene += OnCloseScene;
            }
        }

        public void Start ()
        {
        }

        public void PostStart ()
        {
        }

        public string Name {
            get { return "StatsModule"; }
        }

        public void Close ()
        {
            PeriodicDiagnosticsTimer.Close ();
        }

        #endregion

        #region Event Handlers

        public void SendStatsResults (SimStats simStats)
        {
            SendStatResult handlerSendStatResult = OnSendStatsResult;
            if (handlerSendStatResult != null) {
                handlerSendStatResult (simStats);
            }
        }

        #endregion

        public void OnAddedScene (IScene scene)
        {
            //Register all the commands for this region
            MonitorRegistry reg = new MonitorRegistry (this);
            reg.AddScene (scene);
            lock (m_regionRegistry)
                m_regionRegistry [scene.RegionInfo.RegionID] = reg;
            scene.RegisterModuleInterface<IMonitorModule> (this);
        }

        public void OnCloseScene (IScene scene)
        {
            lock (m_regionRegistry) {
                m_regionRegistry [scene.RegionInfo.RegionID].Close ();
                m_regionRegistry.Remove (scene.RegionInfo.RegionID);
            }
        }

        public void Dispose ()
        {
        }
    }
}
