/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://opensimulator.org/
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
using System.Collections;
using System.Text;
using System.Threading;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Servers.HttpServer.Implementation;

namespace WhiteCore.Framework.Servers.HttpServer
{
    public class PollServiceRequestManager
    {
        static Queue m_requests = Queue.Synchronized (new Queue ());
        uint m_WorkerThreadCount = 0;
        Thread [] m_workerThreads;
        Thread m_watcherThread;
        PollServiceWorkerThread [] m_PollServiceWorkerThreads;
        volatile bool m_running = true;
        int m_pollTimeout;
        readonly object m_queueSync = new object ();

        public bool Started {
            get { return m_running; }
        }

        public PollServiceRequestManager (uint pWorkerThreadCount, int pTimeout)
        {
            m_WorkerThreadCount = pWorkerThreadCount;
            m_pollTimeout = pTimeout;
        }

        public void Start ()
        {
            m_running = true;
            m_workerThreads = new Thread [m_WorkerThreadCount];
            m_PollServiceWorkerThreads = new PollServiceWorkerThread [m_WorkerThreadCount];
            m_workerThreads = new Thread [m_WorkerThreadCount];

            //startup worker threads
            for (uint i = 0; i < m_WorkerThreadCount; i++) {
                m_PollServiceWorkerThreads [i] = new PollServiceWorkerThread (m_pollTimeout);
                m_PollServiceWorkerThreads [i].ReQueue += ReQueueEvent;

                m_workerThreads [i] = new Thread (m_PollServiceWorkerThreads [i].ThreadStart) { Name = string.Format ("PollServiceWorkerThread{0}", i) };
                m_workerThreads [i].Start ();
            }

            //start watcher threads
            m_watcherThread = new Thread (ThreadStart) { Name = "PollServiceWatcherThread" };
            m_watcherThread.Start ();
        }

        internal void ReQueueEvent (PollServiceHttpRequest req)
        {
            // Do accounting stuff here
            Enqueue (req);
        }

        public void Enqueue (PollServiceHttpRequest req)
        {
            lock (m_requests) {
                m_requests.Enqueue (req);
                lock (m_queueSync)
                    Monitor.Pulse (m_queueSync);
            }
        }

        public void ThreadStart ()
        {
            while (m_running) {
                if (!ProcessQueuedRequests ()) {
                    //lock(m_queueSync)
                    //    Monitor.Wait(m_queueSync);
                    Thread.Sleep (1000);
                }
            }
        }

        bool ProcessQueuedRequests ()
        {
            lock (m_requests) {
                if (m_requests.Count == 0)
                    return false;

                // MainConsole.Instance.DebugFormat("[POLL SERVICE REQUEST MANAGER]: Processing {0} requests", m_requests.Count);

                int reqperthread = (int)(m_requests.Count / m_WorkerThreadCount) + 1;

                // For Each WorkerThread
                for (int tc = 0; tc < m_WorkerThreadCount && m_requests.Count > 0; tc++) {
                    //Loop over number of requests each thread handles.
                    for (int i = 0; i < reqperthread && m_requests.Count > 0; i++) {
                        try {
                            m_PollServiceWorkerThreads [tc].Enqueue ((PollServiceHttpRequest)m_requests.Dequeue ());
                        } catch (InvalidOperationException) {
                            // The queue is empty, we did our calculations wrong!
                            return true;
                        }
                    }
                }
                return true;
            }
        }

        public void Stop ()
        {
            m_running = false;
            lock (m_requests) {

                foreach (object o in m_requests) {
                    PollServiceHttpRequest req = (PollServiceHttpRequest)o;
                    OSHttpResponse response = new OSHttpResponse (req.Context);

                    byte [] buffer = req.PollServiceArgs.NoEvents (req.RequestID, req.PollServiceArgs.Id, response);

                    req.Context.Response.SendChunked = false;
                    req.Context.Response.ContentEncoding = Encoding.UTF8;

                    try {
                        req.Context.Response.ContentLength64 = buffer.LongLength;
                        req.Context.Response.Close (buffer, true);
                    } catch (Exception ex) {
                        MainConsole.Instance.WarnFormat ("[Poll service worker thread]: Error: {0}", ex.ToString ());
                    }
                }

                m_requests.Clear ();
            }
            foreach (Thread t in m_workerThreads) {
                t.Abort ();
            }
        }
    }
}
