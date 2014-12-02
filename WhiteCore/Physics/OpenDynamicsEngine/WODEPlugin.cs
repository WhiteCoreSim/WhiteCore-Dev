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

//#define USE_DRAWSTUFF

using System;
using WhiteCore.Framework.Physics;

//using Ode.NET;
//#if USE_DRAWSTUFF
//using Drawstuff.NET;
//#endif 

namespace WhiteCore.Physics.OpenDynamicsEngine
{
    /// <summary>
    ///     ODE plugin
    /// </summary>
    public class WhiteCoreODEPlugin : IPhysicsPlugin
    {
        private static bool m_initialized;
        private WhiteCoreODEPhysicsScene _mScene;
        private static object m_lock = new object();

        #region IPhysicsPlugin Members

        public bool Init()
        {
            return true;
        }

        public PhysicsScene GetScene()
        {
            lock (m_lock)
            {
                if (_mScene == null)
                {
                    if (!m_initialized) //Only initialize ode once!
                    {
                        // Initializing ODE only when a scene is created allows alternative ODE plugins to co-habit (according to
                        // http://opensimulator.org/mantis/view.php?id=2750).
                        d.InitODE();
                        m_initialized = true;
                    }

                    _mScene = new WhiteCoreODEPhysicsScene();
                }
            }

            return _mScene;
        }

        public string GetName()
        {
            return "OpenDynamicsEngine";
        }

        public void Dispose()
        {
        }

        #endregion
    }

    /// <summary>
    ///     Various properties that ODE uses for AMotors but isn't exposed in ODE.NET so we must define them ourselves.
    /// </summary>
    public enum dParam
    {
        LowStop = 0,
        HiStop = 1,
        Vel = 2,
        FMax = 3,
        FudgeFactor = 4,
        Bounce = 5,
        CFM = 6,
        StopERP = 7,
        StopCFM = 8,
        LoStop2 = 256,
        HiStop2 = 257,
        Vel2 = 258,
        FMax2 = 259,
        StopERP2 = 7 + 256,
        StopCFM2 = 8 + 256,
        LoStop3 = 512,
        HiStop3 = 513,
        Vel3 = 514,
        FMax3 = 515,
        StopERP3 = 7 + 512,
        StopCFM3 = 8 + 512
    }

    /// <summary>
    ///     Collision flags
    /// </summary>
    [Flags]
    public enum CollisionCategories
    {
        Disabled = 0,
        Geom = 0x00000001,
        Body = 0x00000002,
        Space = 0x00000004,
        Character = 0x00000008,
        Land = 0x00000010,
        Water = 0x00000020,
        Wind = 0x00000040,
        Sensor = 0x00000080,
        Selected = 0x00000100
    }

    /// <summary>
    ///     Material type for a primitive
    /// </summary>
    public enum Material
    {
        /// <summary>
        /// </summary>
        Stone = 0,

        /// <summary>
        /// </summary>
        Metal = 1,

        /// <summary>
        /// </summary>
        Glass = 2,

        /// <summary>
        /// </summary>
        Wood = 3,

        /// <summary>
        /// </summary>
        Flesh = 4,

        /// <summary>
        /// </summary>
        Plastic = 5,

        /// <summary>
        /// </summary>
        Rubber = 6
    }
}