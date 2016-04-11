/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://opensimulator.org/, http://whitecore-sim.org
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
    public sealed class BSLinksetConstraints : BSLinkset
    {
        // private static string LogHeader = "[BULLETSIM LINKSET CONSTRAINTS]";

        public class BSLinkInfoConstraint : BSLinkInfo
        {
            public ConstraintType constraintType;
            public BSConstraint constraint;

            public OMV.Vector3 frameInAloc;
            public OMV.Quaternion frameInArot;
            public OMV.Vector3 frameInBloc;
            public OMV.Quaternion frameInBrot;
            public bool useLinearReferenceFrameA;

            public BSLinkInfoConstraint(BSPrimLinkable pMember) : base(pMember)
            {
                constraint = null;
                ResetLink();
                member.PhysicsScene.DetailLog("{0},BSLinkInfoConstraint.creation", member.LocalID);
            }
        }
        
        public BSLinksetConstraints(BSScene scene, BSPrimLinkable parent) 
            : base(scene, parent)
        {
            LinksetImpl = LinksetImplementation.Constraint;
        }

        // When physical properties are changed the linkset needs to recalculate
        //   its internal properties.
        // This is queued in the 'post taint' queue so the
        //   refresh will happen once after all the other taints are applied.
        public override void Refresh(BSPrimLinkable requestor)
        {
            ScheduleRebuild(requestor);
            base.Refresh(requestor);
        }

        // The object is going dynamic (physical). Do any setup necessary
        //     for a dynamic linkset.
        // Only the state of the passed object can be modified. The rest of the linkset
        //     has not yet been fully constructed.
        // Return 'true' if any properties updated on the passed object.
        // Called at taint-time!
        public override bool MakeDynamic(BSPrimLinkable child)
        {
            // What is done for each object in BSPrim is what we want.
            return false;
        }

        // The object is going static (non-physical). Do any setup necessary for a static linkset.
        // Return 'true' if any properties updated on the passed object.
        // This doesn't normally happen -- WhiteCore removes the objects from the physical
        //     world if it is a static linkset.
        // Called at taint-time!
        public override bool MakeStatic(BSPrimLinkable child)
        {
            // What is done for each object in BSPrim is what we want.
            return false;
        }

        // Called at taint-time!!
        public override void UpdateProperties(UpdatedProperties whichUpdated, BSPrimLinkable pObj)
        {
            // Nothing to do for constraints on property updates
        }

        // TODO!!! Rename to RemoveDependencies
        // Routine called when rebuilding the body of some member of the linkset.
        // Destroy all the constraints have have been made to root and set
        //     up to rebuild the constraints before the next simulation step.
        // Returns 'true' of something was actually removed and would need restoring
        // Called at taint-time!!
        public override bool RemoveBodyDependencies(BSPrimLinkable child)
        {
            bool ret = false;

            DetailLog("{0},BSLinksetConstraint.RemoveBodyDependencies,removeChildrenForRoot,rID={1},rBody={2}",
                child.LocalID, LinksetRoot.LocalID, LinksetRoot.PhysBody.AddrString);

            lock (m_linksetActivityLock)
            {
                // Just undo all the constraints for this linkset. Rebuild at the end of the step.
                ret = PhysicallyUnlinkAllChildrenFromRoot(LinksetRoot);
                // Cause the constraints, et al to be rebuilt before the next simulation step.
                Refresh(LinksetRoot);
            }
            return ret;
        }

        // ================================================================

        // Add a new child to the linkset.
        // Called while LinkActivity is locked.
        protected override void AddChildToLinkset(BSPrimLinkable child)
        {
            if (!HasChild(child))
            {
                m_children.Add(child, new BSLinkInfoConstraint(child));
                
                DetailLog("{0},BSLinksetConstraints.AddChildToLinkset,call,child={1}", LinksetRoot.LocalID,
                    child.LocalID);

                // Cause constraints and assorted properties to be recomputed before the next simulation step.
                Refresh(LinksetRoot);
            }
            return;
        }

        protected override void ScheduleRebuild(BSPrimLinkable requestor)
        {
            Refresh(requestor);
        }

        // Remove the specified child from the linkset.
        // Safe to call even if the child is not really in my linkset.
        protected override void RemoveChildFromLinkset(BSPrimLinkable child, bool inTaintTime)
        {
            if (m_children.Remove(child))
            {
                BSPrimLinkable rootx = LinksetRoot; // capture the root and body as of now
                BSPrimLinkable childx = child;

                DetailLog("{0},BSLinksetConstraints.RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                    childx.LocalID,
                    rootx.LocalID, rootx.PhysBody.AddrString,
                    childx.LocalID, childx.PhysBody.AddrString);

                PhysicsScene.TaintedObject(inTaintTime,"BSLinksetConstraints.RemoveChildFromLinkset",
                    delegate() { PhysicallyUnlinkAChildFromRoot(rootx, childx); });
                // See that the linkset parameters are recomputed at the end of the taint time.
                Refresh(LinksetRoot);
            }
            else
            {
                // Non-fatal occurance.
                // PhysicsScene.Logger.ErrorFormat("{0}: Asked to remove child from linkset that was not in linkset", LogHeader);
            }
            return;
        }

        // Create a constraint between me (root of linkset) and the passed prim (the child).
        // Called at taint time!
        void PhysicallyLinkAChildToRoot(BSPrimLinkable rootPrim, BSPrimLinkable childPrim)
        {
            // Don't build the constraint when asked. Put it off until just before the simulation step.
            Refresh(rootPrim);
        }

        // Create a static constraint between the two passed objects
        BSConstraint BuildConstraint(BSPrimLinkable rootPrim, BSLinkInfo li)
        {
            BSLinkInfoConstraint linkInfo = li as BSLinkInfoConstraint;
            if (linkInfo == null) return null;

            // Zero motion for children so they don't interpolate
            li.member.ZeroMotion(true);

            BSConstraint constrain = null;

            switch (linkInfo.constraintType)
            {
                case ConstraintType.BS_FIXED_CONSTRAINT_TYPE:
                case ConstraintType.D6_CONSTRAINT_TYPE:
                    // Relative position normalaized to the root prim
                    // Essentually a vector pointing from center of rootPrim to center of li.member
                    OMV.Vector3 childRelativePosition = linkInfo.member.Position - rootPrim.Position;

                    // real world coordinate of midpoint between the two objects
                    OMV.Vector3 midPoint = rootPrim.Position + (childRelativePosition/2);
                    DetailLog(
                        "{0},BSLinksetConstraint.BuildConstraint,6Dof,rBody={1},cBody={2},rLoc={3},cLoc={4},midLoc={5}",
                        rootPrim.LocalID, rootPrim.PhysBody, linkInfo.member.PhysBody, rootPrim.Position,
                        linkInfo.member.Position, midPoint);

                    // create a constraint that allows no freedom of movement between the two objects
                    // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818

                    constrain = new BSConstraint6Dof(PhysicsScene.World, rootPrim.PhysBody, linkInfo.member.PhysBody,
                        midPoint, true, true);
                    /* NOTE: below is an attempt to build constraint with full frame computation, etc.
                     *     Using the midpoint is easier since it lets the Bullet code manipulate the transforms
                     *     of the objects.
                     * Code left for future programmers.
                    // ==================================================================================
                    // relative position normalized to the root prim
                    OMV.Quaternion invThisOrientation = OMV.Quaternion.Inverse(rootPrim.Orientation);
                    OMV.Vector3 childRelativePosition = (liConstraint.member.Position - rootPrim.Position) * invThisOrientation;

            
                    // relative rotation of the child to the parent
                    OMV.Quaternion childRelativeRotation = invThisOrientation * childPrim.Orientation;
                    OMV.Quaternion inverseChildRelativeRotation = OMV.Quaternion.Inverse(childRelativeRotation);

                    DetailLog("{0},BSLinksetConstraint.PhysicallyLinkAChildToRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, childPrim.LocalID);
                    BS6DofConstraint constrain = new BS6DofConstraint(
                                    PhysicsScene.World, rootPrim.Body, childPrim.Body,
                                    OMV.Vector3.Zero,
                                    OMV.Quaternion.Inverse(rootPrim.Orientation),
                                    OMV.Vector3.Zero,
                                    OMV.Quaternion.Inverse(childPrim.Orientation),
                                    true,
                                    true
                                    );
                    // ==================================================================================
                    */
                            break;
                case ConstraintType.D6_SPRING_CONSTRAINT_TYPE:
                    constrain = new BSConstraintSpring(PhysicsScene.World, rootPrim.PhysBody, linkInfo.member.PhysBody,
                                    linkInfo.frameInAloc, linkInfo.frameInArot, linkInfo.frameInBloc, linkInfo.frameInBrot,
                                    linkInfo.useLinearReferenceFrameA,
                                    true /*disableCollisionsBetweenLinkedBodies*/);
                    DetailLog("{0},BSLinksetConstraint.BuildConstraint,spring,root={1},rBody={2},child={3},cBody={4},rLoc={5},cLoc={6}",
                                                    rootPrim.LocalID,
                                                    rootPrim.LocalID, rootPrim.PhysBody.AddrString,
                                                    linkInfo.member.LocalID, linkInfo.member.PhysBody.AddrString,
                                                    rootPrim.Position, linkInfo.member.Position);
                    break;
                default:
                    break;
            }

            linkInfo.SetLinkParameters(constrain);

            PhysicsScene.Constraints.AddConstraint(constrain);

            return constrain;
        }

        // Remove linkage between the linkset root and a particular child
        // The root and child bodies are passed in because we need to remove the constraint between
        //      the bodies that were present at unlink time.
        // Called at taint time!
        bool PhysicallyUnlinkAChildFromRoot(BSPrimLinkable rootPrim, BSPrimLinkable childPrim)
        {
            bool ret = false;
            DetailLog(
                "{0},BSLinksetConstraint.PhysicallyUnlinkAChildFromRoot,taint,root={1},rBody={2},child={3},cBody={4}",
                rootPrim.LocalID,
                rootPrim.LocalID, rootPrim.PhysBody.AddrString,
                childPrim.LocalID, childPrim.PhysBody.AddrString);

            // Find the constraint for this link and get rid of it from the overall collection and from my list
            if (PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.PhysBody, childPrim.PhysBody))
            {
                // Make the child refresh its location
                PhysicsScene.PE.PushUpdate(childPrim.PhysBody);
                ret = true;
            }

            return ret;
        }

        // Remove linkage between myself and any possible children I might have.
        // Returns 'true' of any constraints were destroyed.
        // Called at taint time!
        bool PhysicallyUnlinkAllChildrenFromRoot(BSPrimLinkable rootPrim)
        {
            DetailLog("{0},BSLinksetConstraint.PhysicallyUnlinkAllChildren,taint", rootPrim.LocalID);

            return PhysicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.PhysBody);
        }

        // Call each of the constraints that make up this linkset and recompute the
        //    various transforms and variables. Create constraints of not created yet.
        // Called before the simulation step to make sure the constraint based linkset
        //    is all initialized.
        // Called at taint time!!
        void RecomputeLinksetConstraints()
        {
            float linksetMass = LinksetMass;
            LinksetRoot.UpdatePhysicalMassProperties(linksetMass, true);

            DetailLog("{0},BSLinksetConstraint.RecomputeLinksetConstraints,set,rBody={1},linksetMass={2}",
                LinksetRoot.LocalID, LinksetRoot.PhysBody.AddrString, linksetMass);

            try
            {
                Rebuilding = true;

                // There is no reason to build all this physical stuff for a non-physical linkset.
                if (!LinksetRoot.IsPhysicallyActive || !HasAnyChildren)
                {
                    DetailLog("{0},BSLinksetConstraint.RecomputeLinksetCompound,notPhysicalOrNoChildren",
                        LinksetRoot.LocalID);
                    return; // Note the 'finally' clause at the botton which will get executed.
                }

                ForEachLinkInfo((li) =>
                {
                    // A child in the linkset physically shows the mass of the whole linkset.
                    // This allows Bullet to apply enough force on the child to move the whole linkset.
                    // (Also do the mass stuff before recomputing the constraint so mass is not zero.)
                    li.member.UpdatePhysicalMassProperties(linksetMass, true);

                    BSConstraint constrain;
                    if (
                        !PhysicsScene.Constraints.TryGetConstraint(LinksetRoot.PhysBody, li.member.PhysBody,
                            out constrain))
                    {
                        // If constraint doesn't exist yet, create it.
                        constrain = BuildConstraint(LinksetRoot, li);
                    }
                    li.SetLinkParameters(constrain);
                    constrain.RecomputeConstraintVariables(linksetMass);

                    // PhysicScene.PE.DumpConstraint(PhysicsScene.World, constrain.Constraint);
                    return false; // 'false' says to keep processing other members
                });
            }
            finally
            {
                Rebuilding = false;
            }
        }
    }
}
