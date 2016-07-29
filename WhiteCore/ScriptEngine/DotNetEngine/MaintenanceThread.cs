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

//#define Debug

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.ScriptEngine.DotNetEngine
{
    public class MaintenanceThread
    {
        #region Declares
        EventWaitHandle wh = new AutoResetEvent(false);

        const int EMPTY_WORK_KILL_THREAD_TIME = 250;
        readonly EventManager EventManager;

        /// <summary>
        ///     Queue that handles the loading and unloading of scripts
        /// </summary>
        readonly StartPerformanceQueue LUQueue = new StartPerformanceQueue();

        readonly ConcurrentQueue<QueueItemStruct> ScriptEvents = new ConcurrentQueue<QueueItemStruct>();

        readonly PriorityQueue<QueueItemStruct, long> SleepingScriptEvents =
            new PriorityQueue<QueueItemStruct, long>(10, DateTimeComparer);

        readonly ScriptEngine m_ScriptEngine;
        float EventPerformance = 0.1f;
        bool FiredStartupEvent;
        DateTime NextSleepersTest = DateTime.Now;

        int SleepingScriptEventCount;
        int m_CheckingEvents;
        int m_CheckingSleepers;

        public long CmdHandlerQueueIsRunning;
        public bool EventProcessorIsRunning;
        public bool RunInMainProcessingThread;
        public bool ScriptChangeIsRunning;
        public WhiteCoreThreadPool cmdThreadpool;
        public int MaxScriptThreads = 1;
        public bool m_Started;
        public WhiteCoreThreadPool scriptChangeThreadpool;
        public WhiteCoreThreadPool scriptThreadpool;

        public bool Started
        {
            get { return m_Started; }
            set
            {
                m_Started = true;

                scriptChangeThreadpool.QueueEvent(ScriptChangeQueue, 2);
                //Start the queue because it can't start itself
                cmdThreadpool.ClearEvents();
                cmdThreadpool.QueueEvent(CmdHandlerQueue, 2);
            }
        }

        static int DateTimeComparer(long a, long b)
        {
            return b.CompareTo(a);
        }

        #endregion

        #region Constructor

        public MaintenanceThread(ScriptEngine Engine)
        {
            m_ScriptEngine = Engine;
            EventManager = Engine.EventManager;

            RunInMainProcessingThread = Engine.Config.GetBoolean("RunInMainProcessingThread", false);

            RunInMainProcessingThread = false; // temporary false until code is fix to work with true

            //There IS a reason we start this, even if RunInMain is enabled
            // If this isn't enabled, we run into issues with the CmdHandlerQueue,
            // as it always must be async, so we must run the pool anyway
            WhiteCoreThreadPoolStartInfo info = new WhiteCoreThreadPoolStartInfo
                                                 {
                                                     priority = ThreadPriority.Normal,
                                                     Threads = 1,
                                                     MaxSleepTime = Engine.Config.GetInt("SleepTime", 100),
                                                     SleepIncrementTime = Engine.Config.GetInt("SleepIncrementTime", 1),
                                                     Name = "Script Cmd Thread Pools"
                                                 };
            cmdThreadpool = new WhiteCoreThreadPool(info);
            
            WhiteCoreThreadPoolStartInfo scinfo = new WhiteCoreThreadPoolStartInfo
                                                 {
                                                     priority = ThreadPriority.Normal,
                                                     Threads = 1,
                                                     MaxSleepTime = Engine.Config.GetInt("SleepTime", 100),
                                                     SleepIncrementTime = Engine.Config.GetInt("SleepIncrementTime", 1),
                                                     Name = "Script Loading Thread Pools"
                                                 };
        
            scriptChangeThreadpool = new WhiteCoreThreadPool(scinfo);

            MaxScriptThreads = Engine.Config.GetInt("MaxScriptThreads", 100); // leave control threads out of user option
            WhiteCoreThreadPoolStartInfo sinfo = new WhiteCoreThreadPoolStartInfo
                                                  {
                                                      priority = ThreadPriority.Normal,
                                                      Threads = MaxScriptThreads,
                                                      MaxSleepTime = Engine.Config.GetInt("SleepTime", 100),
                                                      SleepIncrementTime = Engine.Config.GetInt("SleepIncrementTime", 1),
                                                      KillThreadAfterQueueClear = true,
                                                      Name = "Script Event Thread Pools"
                                                  };
            scriptThreadpool = new WhiteCoreThreadPool(sinfo);

            AppDomain.CurrentDomain.AssemblyResolve += m_ScriptEngine.AssemblyResolver.OnAssemblyResolve;
        }

        #endregion

        #region Loops

        /// <summary>
        ///     This loop deals with starting and stopping scripts
        /// </summary>
        /// <returns></returns>
        public void ScriptChangeQueue()
        {
            if (m_ScriptEngine.Scene == null)
                return;

            IMonitorModule module = m_ScriptEngine.Scene.RequestModuleInterface<IMonitorModule>();
            int StartTime = Util.EnvironmentTickCount();

            if (!Started) //Break early
                return;

            if (m_ScriptEngine.ConsoleDisabled || m_ScriptEngine.Disabled || !m_ScriptEngine.Scene.ShouldRunHeartbeat)
                return;

            ScriptChangeIsRunning = true;

            object oitems;
            bool broken = false;
            for (int i = 0; i < 5; i++)
            {
                if (LUQueue.GetNext(out oitems))
                    StartScripts(oitems as LUStruct[]);
                else
                {
                    //None left, stop looping
                    broken = true;
                    break;
                }
            }
            if (!broken)
            {
                scriptChangeThreadpool.QueueEvent(ScriptChangeQueue, 2); //Requeue us, still more to do
                return;
            }

            if (!FiredStartupEvent)
            {
                //If we are empty, we are all done with script startup and can tell the region that we are all done
                if (LUQueue.Count() == 0)
                {
                    FiredStartupEvent = true;
                    m_ScriptEngine.Scene.EventManager.TriggerEmptyScriptCompileQueue(m_ScriptEngine.ScriptFailCount,
                                                                                     m_ScriptEngine.ScriptErrorMessages);

                    m_ScriptEngine.Scene.EventManager.TriggerModuleFinishedStartup("ScriptEngine", new List<string>
                                                                                                       {
                                                                                                           m_ScriptEngine.ScriptFailCount.ToString(),
                                                                                                           m_ScriptEngine.ScriptErrorMessages
                                                                                                       });
                        //Tell that we are done
                }
            }
            ScriptChangeIsRunning = false;
            Thread.Sleep(20);

            if (module != null)
            {
                IScriptFrameTimeMonitor scriptMonitor = module.GetMonitor<IScriptFrameTimeMonitor>(m_ScriptEngine.Scene);
                if (scriptMonitor != null)
                    scriptMonitor.AddTime(Util.EnvironmentTickCountSubtract(StartTime));
            }
        }

        public void StartScripts(LUStruct[] items)
        {
            List<LUStruct> NeedsFired = new List<LUStruct>();
            foreach (LUStruct item in items)
            {
                if (m_ScriptEngine.ConsoleDisabled || m_ScriptEngine.Disabled || m_ScriptEngine.Scene == null ||
                    !m_ScriptEngine.Scene.ShouldRunHeartbeat)
                    break;

                if (item.Action == LUType.Unload)
                {
                    //Close
                    item.ID.CloseAndDispose(true);
                }
                else if (item.Action == LUType.Load ||
                         item.Action == LUType.Reupload)
                {
                    try
                    {
                        //Start
                        if (item.ID.Start(item))
                            NeedsFired.Add(item);
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.Error("[" + m_ScriptEngine.ScriptEngineName + "]: LEAKED COMPILE ERROR: " +
                                                   ex);
                    }
                }
            }
            foreach (LUStruct item in NeedsFired)
            {
                //Fire the events afterward so that they all start at the same time
                item.ID.FireEvents();
            }
        }

        public void CmdHandlerQueue()
        {
            if (m_ScriptEngine.Scene == null)
            {
                Interlocked.Exchange(ref CmdHandlerQueueIsRunning, 0);
                return;
            }
            Interlocked.Exchange(ref CmdHandlerQueueIsRunning, 1);
            IMonitorModule module = m_ScriptEngine.Scene.RequestModuleInterface<IMonitorModule>();
            int StartTime = Util.EnvironmentTickCount();

            if (!Started) //Break early
            {
                Interlocked.Exchange(ref CmdHandlerQueueIsRunning, 0);
                return;
            }

            if (m_ScriptEngine.ConsoleDisabled || m_ScriptEngine.Disabled || !m_ScriptEngine.Scene.ShouldRunHeartbeat)
            {
                Interlocked.Exchange(ref CmdHandlerQueueIsRunning, 0);
                return;
            }

            //Check timers, etc
            bool didAnything = false;
            try
            {
                didAnything = m_ScriptEngine.DoOneScriptPluginPass();
            }
            catch (Exception ex)
            {
                MainConsole.Instance.WarnFormat("[{0}]: Error in CmdHandlerPass, {1}", m_ScriptEngine.ScriptEngineName,
                                                ex);
            }

            if (module != null)
            {
                IScriptFrameTimeMonitor scriptMonitor = module.GetMonitor<IScriptFrameTimeMonitor>(m_ScriptEngine.Scene);
                if (scriptMonitor != null)
                    scriptMonitor.AddTime(Util.EnvironmentTickCountSubtract(StartTime));
            }

            if (didAnything) //If we did something, run us again soon
                cmdThreadpool.QueueEvent(CmdHandlerQueue, 2);
            else
                Interlocked.Exchange(ref CmdHandlerQueueIsRunning, 0);
        }

        #endregion

        #region Add

        public void AddScriptChange(LUStruct[] items, LoadPriority priority)
        {
            if (RunInMainProcessingThread)
                StartScripts(items);
            else
            {
                LUQueue.Add(items, priority);
                if (!ScriptChangeIsRunning)
                    StartThread("Change");
            }
        }

        #endregion

        #region Remove

        public void RemoveState(ScriptData ID)
        {
            m_ScriptEngine.StateSave.DeleteFrom(ID);
        }

        #endregion

        #region Start thread

        public void Stop()
        {
            scriptThreadpool.Restart();
            scriptChangeThreadpool.Restart();
            cmdThreadpool.Restart();
            LUQueue.Clear();
            QueueItemStruct itm;
			while (ScriptEvents.TryDequeue(out itm)) {}
            lock(SleepingScriptEvents)
                SleepingScriptEvents.Clear();
        }

        public void Stats()
        {
#pragma warning disable 612, 618
            foreach (Thread t in scriptThreadpool.GetThreads())
            {
                if (t != null)
                {
                    //t.Suspend();
                    wh.WaitOne();
                    System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(t, true);
                    //t.Resume();
                    wh.Set ();
                    MainConsole.Instance.Debug("Thread " + t.Name);
                    MainConsole.Instance.Debug(trace.GetFrames());
                }
            }
            foreach (Thread t in scriptChangeThreadpool.GetThreads())
            {
                if (t != null)
                {
                    //t.Suspend();
                    wh.WaitOne ();
                    System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(t, true);
                    //t.Resume();
                    wh.Set ();
                    MainConsole.Instance.Debug("Thread " + t.Name);
                    MainConsole.Instance.Debug(trace.GetFrames());
                }
            }
            foreach (Thread t in cmdThreadpool.GetThreads())
            {
                if (t != null)
                {
                    //t.Suspend();
                    wh.WaitOne();
                    System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(t, true);
                    //t.Resume();
                    wh.Set();
                    MainConsole.Instance.Debug("Thread " + t.Name);
                    MainConsole.Instance.Debug(trace.GetFrames());
                }
            }
#pragma warning restore 612, 618
        }

        /// <summary>
        ///     Queue the event loop given by thread
        /// </summary>
        /// <param name="thread"></param>
        void StartThread(string thread)
        {
            if (thread == "Change")
            {
                scriptChangeThreadpool.QueueEvent(ScriptChangeQueue, 2);
            }
            else if (thread == "CmdHandlerQueue" && Interlocked.Read(ref CmdHandlerQueueIsRunning) == 0)
            {
                cmdThreadpool.ClearEvents();
                cmdThreadpool.QueueEvent(CmdHandlerQueue, 2);
            }
        }


        /// <summary>
        ///     Makes sure that all the threads that need to be running are running and starts them if they need to be running
        /// </summary>
        public void PokeThreads(UUID itemID)
        {
            if (itemID != UUID.Zero)
            {
                ScriptData script = ScriptEngine.ScriptProtection.GetScript(itemID);
                if (script != null && script.Script != null)
                    script.Script.NeedsStateSaved = true;
            }
            if (LUQueue.Count() > 0 && !ScriptChangeIsRunning)
                StartThread("Change");
            if (Interlocked.Read(ref CmdHandlerQueueIsRunning) == 0)
                StartThread("CmdHandlerQueue");
        }

        public void DisableThreads()
        {
            Interlocked.Exchange(ref CmdHandlerQueueIsRunning, 0);
            EventProcessorIsRunning = false;
            ScriptChangeIsRunning = false;
            cmdThreadpool.ClearEvents();
            scriptChangeThreadpool.ClearEvents();
            scriptThreadpool.ClearEvents();
        }

        #endregion

        #region Scripts events scheduler control

        public void RemoveFromEventSchQueue(ScriptData ID, bool abortcur)
        {
            if (ID == null)
                return;

            //Ignore any events to be added after this
            ID.IgnoreNew = true;
            //Clear out the old events
            Interlocked.Increment(ref ID.VersionID);
        }

        public void SetEventSchSetIgnoreNew(ScriptData ID, bool yes)
        {
            if (ID == null)
                return;
            ID.IgnoreNew = yes;
        }

        public void AddEventSchQueue(ScriptData ID, string FunctionName, DetectParams[] qParams, EventPriority priority,
                                     params object[] param)
        {
            QueueItemStruct QIS = new QueueItemStruct
            {
                EventsProcData = new ScriptEventsProcData(),
                ID = ID,
                functionName = FunctionName,
                llDetectParams = qParams,
                param = param,
                VersionID = Interlocked.Read(ref ID.VersionID),
                State = ID.State,
                CurrentlyAt = null
            };

            if (ID.Script == null || ID.IgnoreNew)
                return;

            if (!ID.SetEventParams(QIS)) // check events delay rules
                return;

            ScriptEvents.Enqueue(QIS);

            long threadCount = Interlocked.Read(ref scriptThreadpool.nthreads);
            if (threadCount == 0 || threadCount < (ScriptEvents.Count + (SleepingScriptEventCount/2))*EventPerformance)
            {
                scriptThreadpool.QueueEvent(eventLoop, 2);
            }
        }

        public bool AddEventSchQIS(QueueItemStruct QIS, EventPriority priority)
        {
            if (QIS.ID == null || QIS.ID.IgnoreNew)
            {
                EventManager.EventComplete(QIS);
                return false;
            }

            if (QIS.ID.Script == null)
            {
                QIS.ID.CheckAddEventToQueue(QIS);
                return false;
            }

            if (!QIS.ID.SetEventParams(QIS)) // check events delay rules
            {
                EventManager.EventComplete(QIS);
                return false;
            }

            QIS.CurrentlyAt = null;

            if (priority == EventPriority.Suspended || priority == EventPriority.Continued)
            {
                lock (SleepingScriptEvents)
                {
                    long time = priority == EventPriority.Suspended
                                    ? DateTime.Now.AddMilliseconds(10).Ticks
                                    : DateTime.Now.Ticks;
                    //Let it sleep for 10ms so that other scripts can process before it, any repeating plugins ought to use this
                    SleepingScriptEvents.Enqueue(QIS, time);
                    SleepingScriptEventCount++;
#if Debug
                MainConsole.Instance.Warn (ScriptEventCount + ", " + QIS.functionName);
#endif
                }
            }
            else
                ScriptEvents.Enqueue(QIS);

            long threadCount = Interlocked.Read(ref scriptThreadpool.nthreads);
            if (threadCount == 0 || threadCount < (ScriptEvents.Count + (SleepingScriptEventCount/2))*EventPerformance)
            {
                scriptThreadpool.QueueEvent(eventLoop, 2);
            }
            return true;
        }

        public void eventLoop()
        {
            int numberOfEmptyWork = 0;
            while (!m_ScriptEngine.ConsoleDisabled && !m_ScriptEngine.Disabled &&
                   m_ScriptEngine.Scene.ShouldRunHeartbeat) {
                //int numScriptsProcessed = 0;
                int numSleepScriptsProcessed = 0;
                //const int minNumScriptsToProcess = 1;
                //processMoreScripts:
                QueueItemStruct QIS = new QueueItemStruct ();
                bool found = false;

                //Check whether it is time, and then do the thread safety piece
                bool checkTime;
                lock (SleepingScriptEvents)
                    checkTime = Interlocked.CompareExchange (ref m_CheckingSleepers, 1, 0) == 0;
                if (checkTime) {
                    lock (SleepingScriptEvents) {
                    restart:
                        if (SleepingScriptEvents.Count > 0) {
                            QIS = SleepingScriptEvents.Dequeue ().Value;
                            found = true;
                            if (QIS.RunningNumber > 2 && SleepingScriptEventCount > 0 &&
                                numSleepScriptsProcessed < SleepingScriptEventCount) {
                                QIS.RunningNumber = 1;
                                SleepingScriptEvents.Enqueue (QIS, QIS.EventsProcData.TimeCheck.Ticks);
                                numSleepScriptsProcessed++; found = false;
                                found = false;
                                goto restart;
                            }
                        }
                    }
                    if (found) {
                        bool expired;
                        lock (SleepingScriptEvents)
                            expired = QIS.EventsProcData.TimeCheck.Ticks < DateTime.Now.Ticks;

                        if (expired) {
                            DateTime NextTime = DateTime.MaxValue;
                            lock (SleepingScriptEvents) {
                                if (SleepingScriptEvents.Count > 0)
                                    NextTime = SleepingScriptEvents.Peek ().Value.EventsProcData.TimeCheck;
                                //Now add in the next sleep time
                                NextSleepersTest = NextTime;

                                //All done
                                Interlocked.Exchange (ref m_CheckingSleepers, 0);
                            }

                            //Execute the event
                            EventSchExec (QIS);
                            lock (SleepingScriptEvents)
                                SleepingScriptEventCount--;
                            //numScriptsProcessed++;
                        } else {
                            lock (SleepingScriptEvents) {
                                NextSleepersTest = QIS.EventsProcData.TimeCheck;
                                SleepingScriptEvents.Enqueue (QIS, QIS.EventsProcData.TimeCheck.Ticks);
                                //All done
                                Interlocked.Exchange (ref m_CheckingSleepers, 0);
                            }
                        }
                    } else //No more left, don't check again
                      {
                        lock (SleepingScriptEvents) {
                            NextSleepersTest = DateTime.MaxValue;
                            //All done
                            Interlocked.Exchange (ref m_CheckingSleepers, 0);
                        }
                    }
                }
                int timeToSleep = 5;
                //If we can, get the next event
                if (Interlocked.CompareExchange (ref m_CheckingEvents, 1, 0) == 0) {
                    if (ScriptEvents.TryDequeue (out QIS)) {
                        Interlocked.Exchange (ref m_CheckingEvents, 0);
#if Debug
                        MainConsole.Instance.Warn(QIS.functionName + "," + ScriptEvents.Count);
#endif
                        EventSchExec (QIS);
                        //numScriptsProcessed++;
                    } else
                        Interlocked.Exchange (ref m_CheckingEvents, 0);
                }
                //Process a bunch each time
                //if (ScriptEventCount > 0 && numScriptsProcessed < minNumScriptsToProcess)
                //    goto processMoreScripts;

                lock (SleepingScriptEvents) {
                    if (ScriptEvents.Count == 0 && NextSleepersTest.Ticks != DateTime.MaxValue.Ticks)
                        timeToSleep = (int)(NextSleepersTest - DateTime.Now).TotalMilliseconds;
                }
                if (timeToSleep < 5)
                    timeToSleep = 5;
                if (timeToSleep > 50)
                    timeToSleep = 50;

                if (SleepingScriptEventCount == 0 && ScriptEvents.Count == 0)
                {
                    numberOfEmptyWork++;
                    if (numberOfEmptyWork > EMPTY_WORK_KILL_THREAD_TIME)
                        //Don't break immediately, otherwise we have to wait to spawn more threads
                    {
                        break; //No more events, end
                    }
                    else if (numberOfEmptyWork > EMPTY_WORK_KILL_THREAD_TIME/20)
                        timeToSleep += 10;
                }
                else if (Interlocked.Read(ref scriptThreadpool.nthreads) >
                         (ScriptEvents.Count + (int) ((SleepingScriptEventCount/2f + 0.5f))) ||
                         Interlocked.Read(ref scriptThreadpool.nthreads) > MaxScriptThreads)
                {
                    numberOfEmptyWork++;
                    if (numberOfEmptyWork > (EMPTY_WORK_KILL_THREAD_TIME/2)) //Don't break immediately
                    {
                        break; //Too many threads, kill some off
                    }
                    else if (numberOfEmptyWork > EMPTY_WORK_KILL_THREAD_TIME/20)
                        timeToSleep += 5;
                }
                else
                    numberOfEmptyWork /= 2; //Cut it down, but don't zero it out, as this may just be one event
#if Debug
                MainConsole.Instance.Warn ("Sleep: " + timeToSleep);
#endif
                Interlocked.Increment(ref scriptThreadpool.nSleepingthreads);
                Thread.Sleep(timeToSleep);
                Interlocked.Decrement(ref scriptThreadpool.nSleepingthreads);
            }
        }

        public void EventSchExec(QueueItemStruct QIS)
        {
            if (QIS.ID == null || QIS.ID.Script == null)
                return;

            if (!QIS.ID.Running)
            {
                //do only state_entry and on_rez
                if (!(QIS.functionName == "state_entry" || QIS.functionName == "on_rez"))
                {
                    return;
                }
            }

            //Check the versionID so that we can kill events
            if (QIS.functionName != "link_message" &&
                QIS.VersionID != Interlocked.Read(ref QIS.ID.VersionID))
            {
                MainConsole.Instance.DebugFormat("[WDNE]: Found bad version ID in queue, resetting, {0} to {1}",
                                                QIS.VersionID, Interlocked.Read(ref QIS.ID.VersionID));
                MainConsole.Instance.DebugFormat("[WDNE]:     Function: '{0}' in region {1}",
					QIS.functionName == "" ? QIS.functionName : "unknown",
                    QIS.ID.Part.ParentEntity.Scene.RegionInfo.RegionName);
                //return;
            }

            if(MainConsole.Instance.IsTraceEnabled)
                MainConsole.Instance.TraceFormat("[WDNE]: Running Event {0} in object {1} in region {2}",
                                           QIS.functionName, QIS.ID.Part,
                                           QIS.ID.Part.ParentEntity.Scene.RegionInfo.RegionName);
            if (!EventSchProcessQIS(ref QIS)) //Execute the event
            {
                //All done
                lock (SleepingScriptEvents) 
                    QIS.EventsProcData.State = ScriptEventsState.Idle;
            }
            else
            {
                if (QIS.CurrentlyAt.SleepTo.Ticks != 0)
                {
                    lock(SleepingScriptEvents) {
                        QIS.EventsProcData.TimeCheck = QIS.CurrentlyAt.SleepTo;
                        QIS.EventsProcData.State = ScriptEventsState.Sleep;
                    
                        //If it is greater, we need to check sooner for this one
                        if (NextSleepersTest.Ticks > QIS.CurrentlyAt.SleepTo.Ticks)
                            NextSleepersTest = QIS.CurrentlyAt.SleepTo;
                    
                        SleepingScriptEvents.Enqueue(QIS, QIS.CurrentlyAt.SleepTo.Ticks);
                        SleepingScriptEventCount++;
                    }
                }
                else
                {
                    lock (SleepingScriptEvents) {
                        QIS.EventsProcData.State = ScriptEventsState.Running;
                        ScriptEvents.Enqueue (QIS);
                    }
                }
            }
        }

        public bool EventSchProcessQIS(ref QueueItemStruct QIS)
        {
            try
            {
                Exception ex = null;
                EnumeratorInfo Running = QIS.ID.Script.ExecuteEvent(QIS.State,
                                                                    QIS.functionName,
                                                                    QIS.param, QIS.CurrentlyAt, out ex);

                if (ex != null)
                {
                    //Check exceptions, some are ours to deal with, and others are to be logged
                    if (ex.Message.Contains("SelfDeleteException"))
                    {
                        if (QIS.ID.Part != null && QIS.ID.Part.ParentEntity != null)
                        {
                            IBackupModule backup =
                                QIS.ID.Part.ParentEntity.Scene.RequestModuleInterface<IBackupModule>();
                            if (backup != null)
                                backup.DeleteSceneObjects(
                                    new ISceneEntity[1] {QIS.ID.Part.ParentEntity}, true, true);
                        }
                    }
                    else if (ex.Message.Contains("ScriptDeleteException"))
                    {
                        if (QIS.ID.Part != null && QIS.ID.Part.ParentEntity != null)
                            QIS.ID.Part.Inventory.RemoveInventoryItem(QIS.ID.ItemID);
                    }
                        //Log it for the user
                    else if (!(ex.Message.Contains("EventAbortException")) &&
                             !(ex.Message.Contains("MinEventDelayException")))
                        QIS.ID.DisplayUserNotification(ex.ToString(), "executing", false, true);
                    EventManager.EventComplete(QIS);
                    return false;
                }
                else if (Running != null)
                {
                    //Did not finish so requeue it
                    QIS.CurrentlyAt = Running;
                    QIS.RunningNumber++;
                    return true; //Do the return... otherwise we open the queue for this event back up
                }
            }
            catch (Exception ex)
            {
                //Error, tell the user
                QIS.ID.DisplayUserNotification(ex.ToString(), "executing", false, true);
            }
            //Tell the event manager about it so that the events will be removed from the queue
            EventManager.EventComplete(QIS);
            return false;
        }

        #endregion
    }
}
