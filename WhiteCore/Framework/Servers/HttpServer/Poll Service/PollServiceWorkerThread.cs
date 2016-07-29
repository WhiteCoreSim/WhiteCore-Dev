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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Framework.Servers.HttpServer
{
    public delegate void ReQueuePollServiceItem(PollServiceHttpRequest req);

    public class PollServiceWorkerThread
    {
        public event ReQueuePollServiceItem ReQueue;

        readonly BlockingQueue<PollServiceHttpRequest> m_request;
        bool m_running = true;
        int m_timeout = 250;

        public PollServiceWorkerThread(int pTimeout)
        {
            m_request = new BlockingQueue<PollServiceHttpRequest>();
            m_timeout = pTimeout;
        }

        public void ThreadStart()
        {
            Run();
        }

        public void Run()
        {
            while (m_running)
            {
                PollServiceHttpRequest req = m_request.Dequeue();

                try
                {
                    byte[] buffer = null;
                    if (req.PollServiceArgs.HasEvents(req.RequestID, req.PollServiceArgs.Id))
                    {
                        StreamReader str;
                        try
                        {
                            str = new StreamReader(req.Context.Request.InputStream);
                        }
                        catch (ArgumentException)
                        {
                            // Stream was not readable means a child agent
                            // was closed due to logout, leaving the
                            // Event Queue request orphaned.
                            continue;
                        }

                        OSHttpResponse response = new OSHttpResponse(req.Context);

                        buffer = req.PollServiceArgs.GetEvents(req.RequestID, req.PollServiceArgs.Id,
                                                                               str.ReadToEnd(), response);
                    }
                    else
                    {
                        if ((Environment.TickCount - req.RequestTime) > m_timeout)
                        {
                            OSHttpResponse response = new OSHttpResponse(req.Context);

                            buffer = req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id, response);
                        }
                        else
                        {
                            ReQueuePollServiceItem reQueueItem = ReQueue;
                            if (reQueueItem != null)
                                reQueueItem(req);
                        }
                    }
                    if (buffer != null)
                    {
                        req.Context.Response.ContentEncoding = Encoding.UTF8;

                        try
                        {
                            if (req.Context.Request.ProtocolVersion.Minor == 0)
                            {
                                //HTTP 1.0... no chunking
                                req.Context.Response.ContentLength64 = buffer.Length;
                                using (Stream stream = req.Context.Response.OutputStream)
                                {
                                    HttpServerHandlerHelpers.WriteNonChunked(stream, buffer);
                                }
                            }
                            else
                            {
                                req.Context.Response.SendChunked = true;
                                using (Stream stream = req.Context.Response.OutputStream)
                                {
                                    HttpServerHandlerHelpers.WriteChunked(stream, buffer);
                                }
                            }
                            req.Context.Response.Close();
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is HttpListenerException) ||
                                !HttpListenerManager.IGNORE_ERROR_CODES.Contains(((HttpListenerException)ex).ErrorCode))
                                MainConsole.Instance.WarnFormat("[Poll service worker thread]: Failed to write all data to the stream: {0}", ex.ToString());
                        }
                    }
                }
                catch (Exception e)
                {
                    MainConsole.Instance.ErrorFormat("Exception in poll service thread: {0}", e.ToString());
                }
            }
        }

        internal void Enqueue(PollServiceHttpRequest pPollServiceHttpRequest)
        {
            m_request.Enqueue(pPollServiceHttpRequest);
        }
    }
}
