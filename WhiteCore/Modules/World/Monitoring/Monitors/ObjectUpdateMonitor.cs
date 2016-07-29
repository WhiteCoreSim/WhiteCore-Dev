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


using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;

namespace WhiteCore.Modules.Monitoring.Monitors
{
    public class ObjectUpdateMonitor : IObjectUpdateMonitor
    {
        #region Declares
        readonly object _primLock = new object ();

        float LastPrimsLimited;
        volatile float primsLimited;

        public float PrimsLimited {
            get {
                lock (_primLock)
                    return LastPrimsLimited;
            }
        }

        #endregion

        #region Constructor

        public ObjectUpdateMonitor (IScene scene)
        {
        }

        #endregion

        #region Implementation of IMonitor

        public double GetValue ()
        {
            lock (_primLock)
                return LastPrimsLimited / 10;
        }

        public string GetName ()
        {
            return "PrimUpdates";
        }

        public string GetInterfaceName ()
        {
            return "IObjectUpdateMonitor";
        }

        public string GetFriendlyValue ()
        {
            return GetValue () + " prim updates limited/second";
        }

        #endregion

        #region Other Methods

        #region IMonitor Members

        public void ResetStats ()
        {
            lock (_primLock) {
                LastPrimsLimited = primsLimited;
                primsLimited = 0;
            }
        }

        #endregion

        #region IObjectUpdateMonitor Members

        public void AddLimitedPrims (int prims)
        {
            lock (_primLock)
                primsLimited += prims;
        }

        #endregion

        #endregion
    }
}
