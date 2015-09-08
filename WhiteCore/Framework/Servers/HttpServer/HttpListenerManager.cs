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
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using WhiteCore.Framework.ConsoleFramework;

namespace WhiteCore.Framework.Servers.HttpServer
{
    public sealed class HttpListenerManager : IDisposable
    {
        //Error codes to ignore if something goes wrong
        // 1229 - An operation was attempted on a nonexistent network connection
        // 995  - The I/O operation has been aborted because of either a thread exit or an application request.
        public static readonly int[] IGNORE_ERROR_CODES = new int[3] { 64, 1229, 995 };
        public event Action<HttpListenerContext> ProcessRequest;

        ConcurrentQueue<HttpListenerContext> _queue;
        ManualResetEvent _newQueueItem = new ManualResetEvent(false), _listenForNextRequest = new ManualResetEvent(false);
        readonly HttpListener _listener;
        readonly Thread _listenerThread;
        readonly Thread[] _workers;
        bool _isSecure;
        bool _isRunning;
        int _lockedQueue;

        public HttpListenerManager(uint maxThreads, bool isSecure)
        {
            _workers = new Thread[maxThreads];
            _queue = new ConcurrentQueue<HttpListenerContext>();
            _listener = new HttpListener();
#if LINUX
            _listener.IgnoreWriteExceptions = true;
#endif
            _listenerThread = new Thread(HandleRequests);
            _isSecure = isSecure;
        }

        public void Start(uint port)
        {
            _isRunning = true;
            _listener.Prefixes.Add(String.Format(@"http{0}://+:{1}/", _isSecure ? "s" : "", port));
            _listener.Start();
            _listenerThread.Start();

            for (int i = 0; i < _workers.Length; i++)
            {
                _workers[i] = new Thread(Worker);
                _workers[i].Start();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void Stop()
        {
            if (!_isRunning)
                return;
            _isRunning = false;
            _listener.Stop();
            _listenForNextRequest.Set();
            _listenerThread.Join();
            _newQueueItem.Set();
            foreach (Thread worker in _workers)
                worker.Join();
            _listener.Close();
        }

#if true //LINUX

        void HandleRequests()
        {
            while (_listener.IsListening)
            {
                _listener.BeginGetContext(ListenerCallback, null);
                _listenForNextRequest.WaitOne();
                _listenForNextRequest.Reset();
            }
        }

        void ListenerCallback(IAsyncResult result)
        {
            HttpListenerContext context = null;

            try
            {
                if(_listener.IsListening)
                    context = _listener.EndGetContext(result);
            }
            catch (Exception ex)
            {
                MainConsole.Instance.ErrorFormat("[HttpListenerManager]: Exception occurred: {0}", ex.ToString());
                return;
            }
            finally
            {
                _listenForNextRequest.Set();
            }
            if (context == null)
                return;
            _queue.Enqueue(context);
            _newQueueItem.Set();
        }

#else

        private void HandleRequests()
        {
            while (_listener.IsListening)
            {
                var context = _listener.BeginGetContext(ContextReady, null);

                if (0 == WaitHandle.WaitAny(new[] {_stop, context.AsyncWaitHandle}))
                    return;
            }
        }

        private void ContextReady(IAsyncResult ar)
        {
            try
            {
                if (!_listener.IsListening) return;
                _queue.Enqueue(_listener.EndGetContext(ar));
                _newQueueItem.Set();
            }
            catch
            {
                return;
            }
        }

#endif

        void Worker()
        {
            while ((_queue.Count > 0 || _newQueueItem.WaitOne()) && _listener.IsListening)
            {
                _newQueueItem.Reset();
                HttpListenerContext context = null;
                if (Interlocked.CompareExchange(ref _lockedQueue, 1, 0) == 0)
                {
                    _queue.TryDequeue(out context);
                    //All done
                    Interlocked.Exchange(ref _lockedQueue, 0);
                }
                try
                {
                    if(context != null)
                        ProcessRequest(context);
                }
                catch (Exception e)
                {
                    MainConsole.Instance.ErrorFormat("[HttpListenerManager]: Exception occurred: {0}", e.ToString());
                }
            }
        }
    }
}
