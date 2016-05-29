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
    public class SimFrameMonitor : ISimFrameMonitor
    {
        #region Declares
        readonly object _simLock = new object ();

        // saved last reported value so there is something available for llGetRegionFPS 
        volatile float lastReportedSimFPS;
        volatile float simFPS;

        public float LastReportedSimFPS {
            get {
                lock (_simLock)
                    return lastReportedSimFPS;
            }
            set {
                lock (_simLock)
                    lastReportedSimFPS = value;
            }
        }

        public float SimFPS {
            get { lock (_simLock) return simFPS; }
        }

        #endregion

        #region Constructor

        public SimFrameMonitor (IScene scene)
        {
        }

        #endregion

        #region Implementation of IMonitor

        public double GetValue ()
        {
            lock (_simLock)
                return LastReportedSimFPS;
        }

        public string GetName ()
        {
            return "SimFrameStats";
        }

        public string GetInterfaceName ()
        {
            return "ISimFrameMonitor";
        }

        public string GetFriendlyValue ()
        {
            return GetValue () + " frames/second";
        }

        #endregion

        #region Other Methods

        #region IMonitor Members

        public void ResetStats ()
        {
            lock (_simLock)
                simFPS = 0;
        }

        #endregion

        #region ISimFrameMonitor Members

        public void AddFPS (int fps)
        {
            lock (_simLock)
                simFPS += fps;
        }

        #endregion

        #endregion
    }
}
