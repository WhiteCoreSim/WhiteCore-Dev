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
using System.Timers;
using OpenMetaverse;

namespace WhiteCore.Framework.Utilities
{
    public sealed class TimedSaving<T> : IDisposable
    {
        public delegate void TimeElapsed(UUID agentID, T data);

        private readonly Dictionary<UUID, T> _saveQueueData = new Dictionary<UUID, T>();
        private readonly Dictionary<UUID, long> _queue = new Dictionary<UUID, long>();

        private readonly Timer _updateTimer = new Timer();
        private const int _checkTime = 500; // milliseconds to wait between checks for updates
        private int _sendtime = 2;
        private TimeElapsed _arg;

        public void Start(int secondsToWait, TimeElapsed args)
        {
            _arg = args;
            _sendtime = secondsToWait;
            _updateTimer.Enabled = false;
            _updateTimer.AutoReset = true;
            _updateTimer.Interval = _checkTime; // 500 milliseconds wait to start async ops
            _updateTimer.Elapsed += timer_elapsed;
        }

        public void Add(UUID agentid)
        {
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(_sendtime*1000*10000);
            lock (_queue)
            {
                _queue[agentid] = timestamp;
                _updateTimer.Start();
            }
        }

        public void Add(UUID agentid, T data)
        {
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(_sendtime*1000*10000);
            lock (_queue)
            {
                _queue[agentid] = timestamp;
                _saveQueueData[agentid] = data;
                _updateTimer.Start();
            }
        }

        private void timer_elapsed(object sender, EventArgs ea)
        {
            long now = DateTime.Now.Ticks;

            Dictionary<UUID, long> sends;
            lock (_queue)
                sends = new Dictionary<UUID, long>(_queue);

            foreach (KeyValuePair<UUID, long> kvp in sends)
            {
                if (kvp.Value < now)
                {
                    T data = default(T);
                    lock (_saveQueueData)
                        if (_saveQueueData.TryGetValue(kvp.Key, out data))
                            _saveQueueData.Remove(kvp.Key);
                    Util.FireAndForget(delegate { _arg(kvp.Key, data); });
                    lock (_queue)
                        _queue.Remove(kvp.Key);
                }
            }
        }

        public void Dispose()
        {
            _updateTimer.Close();
        }
    }

    public sealed class ListCombiningTimedSaving<T> : IDisposable
    {
        public delegate void TimeElapsed(UUID agentID, List<T> data);

        private readonly Dictionary<UUID, List<T>> _saveQueueData = new Dictionary<UUID, List<T>>();
        private readonly Dictionary<UUID, long> _queue = new Dictionary<UUID, long>();

        private readonly Timer _updateTimer = new Timer();
        private const int _checkTime = 500; // milliseconds to wait between checks for updates
        private double _sendtime = 3;
        private TimeElapsed _arg;

        public void Start(double secondsToWait, TimeElapsed args)
        {
            _arg = args;
            _sendtime = secondsToWait;
            _updateTimer.Enabled = false;
            _updateTimer.AutoReset = true;
            _updateTimer.Interval = _checkTime; // 500 milliseconds wait to start async ops
            _updateTimer.Elapsed += timer_elapsed;
        }

        public void Add(UUID agentid)
        {
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(_sendtime*1000*10000);
            lock (_queue)
            {
                _queue[agentid] = timestamp;
                _updateTimer.Start();
            }
        }

        public void Add(UUID agentid, List<T> data)
        {
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(_sendtime*1000*10000);
            lock (_queue)
            {
                _queue[agentid] = timestamp;
                if (!_saveQueueData.ContainsKey(agentid))
                    _saveQueueData.Add(agentid, new List<T>());
                _saveQueueData[agentid].AddRange(data);
                _updateTimer.Start();
            }
        }

        public void Add(UUID agentid, T data)
        {
            long timestamp = DateTime.Now.Ticks + Convert.ToInt64(_sendtime*1000*10000);
            lock (_queue)
            {
                _queue[agentid] = timestamp;
                if (!_saveQueueData.ContainsKey(agentid))
                    _saveQueueData.Add(agentid, new List<T>());
                _saveQueueData[agentid].Add(data);
                _updateTimer.Start();
            }
        }

        private void timer_elapsed(object sender, EventArgs ea)
        {
            long now = DateTime.Now.Ticks;

            Dictionary<UUID, long> sends;
            lock (_queue)
                sends = new Dictionary<UUID, long>(_queue);

            foreach (KeyValuePair<UUID, long> kvp in sends)
            {
                if (kvp.Value < now)
                {
                    List<T> data = new List<T>();
                    lock (_saveQueueData)
                        if (_saveQueueData.TryGetValue(kvp.Key, out data))
                            _saveQueueData.Remove(kvp.Key);
                    Util.FireAndForget(delegate { _arg(kvp.Key, data); });
                    lock (_queue)
                        _queue.Remove(kvp.Key);
                }
            }
        }

        public void Dispose()
        {
            _updateTimer.Close();
        }
    }
}