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
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using Nini.Config;
using OpenMetaverse;


namespace WhiteCore.Modules.PhysicsState
{
    public class PhysicsStateModule : INonSharedRegionModule, IPhysicsStateModule
    {
        private readonly List<WorldPhysicsState> m_timeReversal = new List<WorldPhysicsState>();
        private bool m_isReversing;
        private bool m_isSavingRevertStates;
        private int m_lastRevertedTo = -100;
        private WorldPhysicsState m_lastWorldPhysicsState;
        private IScene m_scene;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(IScene scene)
        {
            scene.RegisterModuleInterface<IPhysicsStateModule>(this);
            m_scene = scene;
            Timer timeReversal = new Timer(250);
            timeReversal.Elapsed += timeReversal_Elapsed;
            timeReversal.Start();
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
            get { return "PhysicsState"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region IPhysicsStateModule Members

        public void SavePhysicsState()
        {
            m_lastWorldPhysicsState = m_isReversing ? null : MakePhysicsState();
        }

        public void ResetToLastSavedState()
        {
            if (m_lastWorldPhysicsState != null)
                m_lastWorldPhysicsState.Reload(m_scene, 1);
            m_lastWorldPhysicsState = null;
        }

        public void StartSavingPhysicsTimeReversalStates()
        {
            m_isSavingRevertStates = true;
        }

        public void StopSavingPhysicsTimeReversalStates()
        {
            m_isSavingRevertStates = false;
            m_timeReversal.Clear();
        }

        public void StartPhysicsTimeReversal()
        {
            m_lastRevertedTo = -100;
            m_isReversing = true;
            m_scene.RegionInfo.RegionSettings.DisablePhysics = true;
        }

        public void StopPhysicsTimeReversal()
        {
            m_lastRevertedTo = -100;
            m_scene.RegionInfo.RegionSettings.DisablePhysics = false;
            m_isReversing = false;
        }

        #endregion

        private WorldPhysicsState MakePhysicsState()
        {
            WorldPhysicsState state = new WorldPhysicsState();
            //Add all active objects in the scene
            foreach (PhysicsActor prm in m_scene.PhysicsScene.ActiveObjects)
            {
                state.AddPrim(prm);
            }

            foreach (IScenePresence sp in m_scene.GetScenePresences().Where(sp => !sp.IsChildAgent))
            {
                state.AddAvatar(sp.PhysicsActor);
            }

            return state;
        }

        private void timeReversal_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!m_isSavingRevertStates)
                return; //Only save if we are running this
            if (!m_isReversing) //Only save new states if we are going forward
                m_timeReversal.Add(MakePhysicsState());
            else
            {
                if (m_lastRevertedTo == -100)
                    m_lastRevertedTo = m_timeReversal.Count - 1;
                m_timeReversal[m_lastRevertedTo].Reload(m_scene, -1f); //Do the velocity in reverse with -1
                m_lastRevertedTo--;
                if (m_lastRevertedTo < 0)
                {
                    m_isSavingRevertStates = false;
                    m_lastRevertedTo = -100;
                    m_isReversing = false;
                    m_scene.StopPhysicsScene(); //Stop physics from moving too
                    m_scene.RegionInfo.RegionSettings.DisablePhysics = true; //Freeze the scene
                    m_timeReversal.Clear(); //Remove the states we have as well, we've played them
                }
            }
        }

        #region Nested type: WorldPhysicsState

        public class WorldPhysicsState
        {
            private readonly Dictionary<UUID, PhysicsState> m_activePrims = new Dictionary<UUID, PhysicsState>();

            public void AddPrim(PhysicsActor prm)
            {
                PhysicsState state = new PhysicsState
                                         {
                                             Position = prm.Position,
                                             AngularVelocity = prm.RotationalVelocity,
                                             LinearVelocity = prm.Velocity,
                                             Rotation = prm.Orientation
                                         };
                m_activePrims[prm.UUID] = state;
            }

            public void AddAvatar(PhysicsActor prm)
            {
                PhysicsState state = new PhysicsState
                                         {
                                             Position = prm.Position,
                                             AngularVelocity = prm.RotationalVelocity,
                                             LinearVelocity = prm.Velocity,
                                             Rotation = prm.Orientation
                                         };
                m_activePrims[prm.UUID] = state;
            }

            public void Reload(IScene scene, float direction)
            {
                foreach (KeyValuePair<UUID, PhysicsState> kvp in m_activePrims)
                {
                    ISceneChildEntity childPrim = scene.GetSceneObjectPart(kvp.Key);
                    if (childPrim != null && childPrim.PhysActor != null)
                        ResetPrim(childPrim.PhysActor, kvp.Value, direction);
                    else
                    {
                        IScenePresence sp = scene.GetScenePresence(kvp.Key);
                        if (sp != null)
                            ResetAvatar(sp.PhysicsActor, kvp.Value, direction);
                    }
                }
            }

            private void ResetPrim(PhysicsActor physicsObject, PhysicsState physicsState, float direction)
            {
                physicsObject.Position = physicsState.Position;
                physicsObject.Orientation = physicsState.Rotation;
                physicsObject.RotationalVelocity = physicsState.AngularVelocity*direction;
                physicsObject.Velocity = physicsState.LinearVelocity*direction;
                physicsObject.ForceSetVelocity(physicsState.LinearVelocity*direction);
                physicsObject.RequestPhysicsterseUpdate();
            }

            private void ResetAvatar(PhysicsActor physicsObject, PhysicsState physicsState, float direction)
            {
                physicsObject.Position = physicsState.Position;
                physicsObject.ForceSetPosition(physicsState.Position);
                physicsObject.Orientation = physicsState.Rotation;
                physicsObject.RotationalVelocity = physicsState.AngularVelocity*direction;
                physicsObject.Velocity = physicsState.LinearVelocity*direction;
                physicsObject.ForceSetVelocity(physicsState.LinearVelocity*direction);
                physicsObject.RequestPhysicsterseUpdate();
            }

            #region Nested type: PhysicsState

            public class PhysicsState
            {
                public Vector3 AngularVelocity;
                public Vector3 LinearVelocity;
                public Vector3 Position;
                public Quaternion Rotation;
            }

            #endregion
        }

        #endregion
    }
}