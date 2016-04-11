/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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

using OMV = OpenMetaverse;

namespace WhiteCore.Physics.BulletSPlugin
{
    public class BSActorLockAxis : BSActor
    {
        BSConstraint LockAxisConstraint;
        bool HaveRegisteredForBeforeStepCallback = false;

        // The lock access flags (which axises were locked) when the constraint was built.
        // Used to see if locking has changed since when the constraint was built.
        OMV.Vector3 LockAxisLinearFlags;
        OMV.Vector3 LockAxisAngularFlags;

        public BSActorLockAxis (BSScene physicsScene, BSPhysObject pObj, string actorName)
            : base (physicsScene, pObj, actorName)
        {
            m_physicsScene.DetailLog ("{0},BSActorLockAxis,constructor", m_controllingPrim.LocalID);
            LockAxisConstraint = null;
        }

        // BSActor.isActive
        public override bool isActive
        {
            get { return Enabled && m_controllingPrim.IsPhysicallyActive; }
        }

        // Release any connections and resources used by the actor.
        // BSActor.Dispose()
        public override void Dispose ()
        {
            RemoveAxisLockConstraint ();
        }

        // Called when physical parameters (properties set in Bullet) need to be re-applied.
        // Called at taint-time.
        // BSActor.Refresh()
        public override void Refresh ()
        {
            // If all the axis are free, we don't need to exist
            // Refresh() only turns off. Enabling is done by Initialize AxisActor()
            //      whenever parameters are changed.
            //      This leaves 'enable' free to turn off an actor when it is not wanted to run.
            if (m_controllingPrim.LockedAngularAxis == m_controllingPrim.LockedAxisFree
                && m_controllingPrim.LockedLinearAxis == m_controllingPrim.LockedAxisFree)
            {
                Enabled = false;
            }

            // If the object is physically active, add the axis locking constraint
            if (isActive)
            {
                RegisterForBeforeStepCallback ();
            } else
            {
                RemoveAxisLockConstraint ();
                UnRegisterForBeforeStepCallback ();
            }
        }

        // The object's physical representation is being rebuilt so pick up any physical dependencies (constraints, ...).
        //     Register a prestep action to restore physical requirements before the next simulation step.
        // Called at taint-time.
        // BSActor.RemoveBodyDependencies()
        public override void RemoveBodyDependencies ()
        {
            if (LockAxisConstraint != null)
            {
                // If a constraint is set up, remove it from the physical scene
                RemoveAxisLockConstraint ();
                // Schedule a call before the next simulation step to restore the constraint.
                m_physicsScene.PostTaintObject ("BSActorLockAxis:" + ActorName, m_controllingPrim.LocalID,
                    delegate() {
                        Refresh ();
                    });
            }
        }

        void RegisterForBeforeStepCallback ()
        {
            if (!HaveRegisteredForBeforeStepCallback)
            {
                m_physicsScene.BeforeStep += PhysicsScene_BeforeStep;
                HaveRegisteredForBeforeStepCallback = true;
            }
        }

        void UnRegisterForBeforeStepCallback ()
        {
            if (HaveRegisteredForBeforeStepCallback)
            {
                m_physicsScene.BeforeStep -= PhysicsScene_BeforeStep;
                HaveRegisteredForBeforeStepCallback = false;
            }
        }

        void PhysicsScene_BeforeStep (float timestep)
        {
            // If all the axis are free, we don't need to exist
            if (m_controllingPrim.LockedAngularAxis == m_controllingPrim.LockedAxisFree &&
                m_controllingPrim.LockedLinearAxis == m_controllingPrim.LockedAxisFree)
            {
                Enabled = false;
            }

            // If the object is physically active, add the axis locking constraint
            if (isActive)
            {
                // Check to see if the locking parameters have changed
                if (m_controllingPrim.LockedLinearAxis != this.LockAxisLinearFlags ||
                    m_controllingPrim.LockedAngularAxis != this.LockAxisAngularFlags)
                {
                    // The locking has changed. Remove the old constraint and build a new one
                    RemoveAxisLockConstraint ();
                }
                AddAxisLockConstraint ();
            } else
            {
                RemoveAxisLockConstraint ();
            }
        }

        // Note that this relies on being called at TaintTime
        void AddAxisLockConstraint ()
        {
            if (LockAxisConstraint == null)
            {
                // Lock that axis by creating a 6DOF constraint that has one end in the world and
                //    the other in the object.
                // http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?p=20817
                // http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?p=26380

                // Remove any existing axis constraint (just to be sure)
                RemoveAxisLockConstraint ();

                BSConstraint6Dof axisConstrainer = new BSConstraint6Dof (m_physicsScene.World, m_controllingPrim.PhysBody,
                                                       OMV.Vector3.Zero, OMV.Quaternion.Identity,
                                                       false /* useLinearReferenceFrameB */, true /* disableCollisionsBetweenLinkedBodies */);
                LockAxisConstraint = axisConstrainer;
                m_physicsScene.Constraints.AddConstraint (LockAxisConstraint);

                // Remember the clocking being inforce so we can notice if they have changed
                LockAxisLinearFlags = m_controllingPrim.LockedLinearAxis;
                LockAxisAngularFlags = m_controllingPrim.LockedAngularAxis;

                // The constraint is tied to the world and oriented to the prim.
                if (
                    !axisConstrainer.SetLinearLimits (m_controllingPrim.LockedLinearAxisLow,
                        m_controllingPrim.LockedLinearAxisHigh))
                {
                    m_physicsScene.DetailLog ("{0},BSActorLockAxis.AddAxisLockConstraint,failedSetLinearLimits",
                        m_controllingPrim.LocalID);
                }

                if (
                    !axisConstrainer.SetAngularLimits (m_controllingPrim.LockedAngularAxisLow,
                        m_controllingPrim.LockedAngularAxisHigh))
                {
                    m_physicsScene.DetailLog ("{0},BSActorLockAxis.AddAxisLockConstraint,failedSetAngularLimits", m_controllingPrim.LocalID);
                }

                m_physicsScene.DetailLog (
                    "{0},BSActorLockAxis.AddAxisLockConstraint,create,linLow={1},linHi={2},angLow={3},angHi={4}",
                    m_controllingPrim.LocalID, m_controllingPrim.LockedLinearAxisLow,
                    m_controllingPrim.LockedLinearAxisHigh, m_controllingPrim.LockedAngularAxisLow,
                    m_controllingPrim.LockedAngularAxisHigh);

                // Constants from one of the posts mentioned above and used in Bullet's ConstraintDemo.
                axisConstrainer.TranslationalLimitMotor (true /* enable */, 5.0f, 0.1f);
                axisConstrainer.RecomputeConstraintVariables (m_controllingPrim.RawMass);
                RegisterForBeforeStepCallback ();
            }
        }

        void RemoveAxisLockConstraint ()
        {
            UnRegisterForBeforeStepCallback ();
            if (LockAxisConstraint != null)
            {
                m_physicsScene.Constraints.RemoveAndDestroyConstraint (LockAxisConstraint);
                LockAxisConstraint = null;
                m_physicsScene.DetailLog ("{0},BSActorLockAxis.RemoveAxisLockConstraint,destroyingConstraint",
                    m_controllingPrim.LocalID);
            }
        }
    }
}
