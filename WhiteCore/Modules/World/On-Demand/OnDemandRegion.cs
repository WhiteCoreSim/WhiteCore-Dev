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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;


namespace WhiteCore.Modules.OnDemand
{
    /// <summary>
    ///     Some notes on this module, this module just modifies when/where the startup code is executed
    ///     This module has a few different settings for the region to startup with,
    ///     Soft, Medium, and Normal (no change)
    ///     -- Soft --
    ///     Disables the heartbeats (not scripts, as its instance-wide)
    ///     Only loads land and parcels, no prims
    ///     -- Medium --
    ///     Same as Soft, except it loads prims (same as normal, but no threads)
    ///     -- Normal --
    ///     Same as always
    /// </summary>
    public class OnDemandRegionModule : INonSharedRegionModule
    {
        #region Declares

        readonly List<UUID> m_zombieAgents = new List<UUID>();
        bool m_isRunning;
        bool m_isShuttingDown;
        bool m_isStartingUp;
        IScene m_scene;

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(IScene scene)
        {
            if (scene.RegionInfo.Startup != StartupType.Normal)
            {
                m_scene = scene;
                //Disable the heartbeat for this region
                scene.ShouldRunHeartbeat = false;

                scene.EventManager.OnRemovePresence += OnRemovePresence;
                scene.WhiteCoreEventManager.RegisterEventHandler("NewUserConnection", OnGenericEvent);
                scene.WhiteCoreEventManager.RegisterEventHandler("AgentIsAZombie", OnGenericEvent);
            }
        }

        public void RegionLoaded(IScene scene)
        {
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "OnDemandRegionModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region Private Events

        object OnGenericEvent(string FunctionName, object parameters)
        {
            if (FunctionName == "NewUserConnection")
            {
                if (!m_isRunning)
                {
                    m_isRunning = true;
                    if (m_scene.RegionInfo.Startup == StartupType.Medium)
                    {
                        m_scene.WhiteCoreEventManager.FireGenericEventHandler("MediumStartup", m_scene);
                        MediumStartup();
                    }
                }
            }
            else if (FunctionName == "AgentIsAZombie")
                m_zombieAgents.Add((UUID) parameters);
            return null;
        }

        void OnRemovePresence(IScenePresence presence)
        {
            if (m_scene.GetScenePresences().Count == 1) //This presence hasn't been removed yet, so we check against one
            {
                if (m_zombieAgents.Contains(presence.UUID))
                {
                    m_zombieAgents.Remove(presence.UUID);
                    return; //It'll be reading an agent, don't kill the sim immediately
                }
                //If all clients are out of the region, we can close it again
                if (m_scene.RegionInfo.Startup == StartupType.Medium)
                {
                    m_scene.WhiteCoreEventManager.FireGenericEventHandler("MediumShutdown", m_scene);
                    MediumShutdown();
                }
                m_isRunning = false;
            }
        }

        #endregion

        #region Private Shutdown Methods

        void MediumShutdown()
        {
            //Only shut down one at a time
            if (m_isShuttingDown)
                return;
            m_isShuttingDown = true;
            GenericShutdown();
            m_isShuttingDown = false;
        }

        /// <summary>
        ///     This shuts down the heartbeats so that everything is dead again
        /// </summary>
        void GenericShutdown()
        {
            //After the next iteration, the threads will kill themselves
            m_scene.ShouldRunHeartbeat = false;
        }

        #endregion

        #region Private Startup Methods

        /// <summary>
        ///     We've already loaded prims/parcels/land earlier,
        ///     we don't have anything else to load,
        ///     so we just need to get the heartbeats back on track
        /// </summary>
        void MediumStartup()
        {
            //Only start up one at a time
            if (m_isStartingUp)
                return;
            m_isStartingUp = true;

            GenericStartup();

            m_isStartingUp = false;
        }

        /// <summary>
        ///     This sets up the heartbeats so that they are running again, which is needed
        /// </summary>
        void GenericStartup()
        {
            m_scene.ShouldRunHeartbeat = true;
            m_scene.StartHeartbeat();
        }

        #endregion
    }
}