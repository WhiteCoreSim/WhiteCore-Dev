/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org/, http://opensimulator.org/
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

using System.Text;
using OMV = OpenMetaverse;

namespace WhiteCore.Physics.BulletSPlugin
{
    // When a child is linked, the relationship position of the child to the parent
    //    is remembered so the child's world position can be recomputed when it is
    //    removed from the linkset.
    sealed class BSLinksetCompoundInfo : BSLinksetInfo
    {
        public int Index;
        public OMV.Vector3 OffsetFromRoot;
        public OMV.Vector3 OffsetFromCenterOfMass;
        public OMV.Quaternion OffsetRot;

        public BSLinksetCompoundInfo(int indx, OMV.Vector3 p, OMV.Quaternion r)
        {
            Index = indx;
            OffsetFromRoot = p;
            OffsetFromCenterOfMass = p;
            OffsetRot = r;
        }

        // 'centerDisplacement' is the distance from the root the the center-of-mass (Bullet 'zero' of the shape)
        public BSLinksetCompoundInfo(int indx, BSPrimLinkable root, BSPrimLinkable child, OMV.Vector3 centerDisplacement)
        {
            // Each child position and rotation is given relative to the center-of-mass.
            OMV.Quaternion invRootOrientation = OMV.Quaternion.Inverse(root.RawOrientation);
            OMV.Vector3 displacementFromRoot = (child.RawPosition - root.RawPosition) * invRootOrientation;
            OMV.Vector3 displacementFromCOM = displacementFromRoot - centerDisplacement;
            OMV.Quaternion displacementRot = child.RawOrientation * invRootOrientation;

            // Save relative position for recomputing child's world position after moving linkset.
            Index = indx;
            OffsetFromRoot = displacementFromRoot;
            OffsetFromCenterOfMass = displacementFromCOM;
            OffsetRot = displacementRot;
        }

        public override void Clear()
        {
            Index = 0;
            OffsetFromRoot = OMV.Vector3.Zero;
            OffsetFromCenterOfMass = OMV.Vector3.Zero;
            OffsetRot = OMV.Quaternion.Identity;
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append("<i=");
            buff.Append(Index.ToString());
            buff.Append(",p=");
            buff.Append(OffsetFromRoot.ToString());
            buff.Append(",m=");
            buff.Append(OffsetFromCenterOfMass.ToString());
            buff.Append(",r=");
            buff.Append(OffsetRot.ToString());
            buff.Append(">");
            return buff.ToString();
        }
    };

    public sealed class BSLinksetCompound : BSLinkset
    {
#pragma warning disable 414
        static string LogHeader = "[BULLETSIM LINKSET COMPOUND]";
#pragma warning restore 414

        public BSLinksetCompound(BSScene scene, BSPrimLinkable parent)
            : base(scene, parent)
        {
            LinksetImpl = LinksetImplementation.Compound;
        }

        // TODO!!! public override void SetPhysicalFriction
        // TODO!!! public override void SetPhysicalRestitution
        // TODO!!! public override void SetPhysicalGravity
        // TODO!!! public override void ComputeAndSetLocalInertia
        // TODO!!! public override void SetPhysicalCollisionFlags
        // TODO!!! public override void AddToPhysicalCollisionFlags
        // TODO!!! public override void RemoveFromPhysicalCollisionFlags

        // For compound implimented linksets, if there are children, use compound shape for the root.
        public override BSPhysicsShapeType PreferredPhysicalShape(BSPrimLinkable requestor)
        {
            // Returning 'unknown' means we don't have a preference.
            BSPhysicsShapeType ret = BSPhysicsShapeType.SHAPE_UNKNOWN;
            if (IsRoot(requestor) && HasAnyChildren)
            {
                ret = BSPhysicsShapeType.SHAPE_COMPOUND;
            }
            // DetailLog("{0},BSLinksetCompound.PreferredPhysicalShape,call,shape={1}", LinksetRoot.LocalID, ret);
            return ret;
        }

        // When physical properties are changed the linkset needs to recalculate
        //   its internal properties.
        public override void Refresh(BSPrimLinkable requestor)
        {
            // Something changed so do the rebulding thing
            ScheduleRebuild(requestor);
            base.Refresh(requestor);
        }

        // Schedule a refresh to happen after all the other taint processing.
        protected override void ScheduleRebuild(BSPrimLinkable requestor)
        {
            // When rebuilding, it is possible to set properties that would normally require a rebuild.
            //    If already rebuilding, don't request another rebuild.
            //    If a linkset with just a root prim (simple non-linked prim) don't bother rebuilding.
            lock (m_linksetActivityLock)
            {
                if (!RebuildScheduled && !Rebuilding && HasAnyChildren)
                {
                    InternalScheduleRebuild(requestor);
                }
            }
        }

        // must be called with m_linksetActivityLock or race conditions will haunt you.
        void InternalScheduleRebuild(BSPrimLinkable requestor)
        {
            DetailLog("{0},BSLinksetCompound.InternalScheduleRebuild,,rebuilding={1},hasChildren={2}", requestor.LocalID,
                Rebuilding, HasAnyChildren);
            RebuildScheduled = true;
            PhysicsScene.PostTaintObject("BSLinksetCompound.ScheduleRebuild", LinksetRoot.LocalID, delegate()
            {
                if (HasAnyChildren)
                {
                    if (AllPartsComplete)
                    {
                        RecomputeLinksetCompound();
                    }
                    else
                    {
                        DetailLog(
                            "{0},BSLinksetCompound.InternalScheduleRebuild,,rescheduling because not all children complete",
                            requestor.LocalID);
                        InternalScheduleRebuild(requestor);
                    }
                }
                RebuildScheduled = false;
            });
        }

        // The object is going dynamic (physical). Do any setup necessary for a dynamic linkset.
        // Only the state of the passed object can be modified. The rest of the linkset
        //     has not yet been fully constructed.
        // Return 'true' if any properties updated on the passed object.
        // Called at taint-time!
        public override bool MakeDynamic(BSPrimLinkable child)
        {
            bool ret = false;
            DetailLog("{0},BSLinksetCompound.MakeDynamic,call,IsRoot={1}", child.LocalID, IsRoot(child));
            if (IsRoot(child))
            {
                // The root is going dynamic. Rebuild the linkset so parts and mass get computed properly.
                Refresh(LinksetRoot);
            }
            return ret;
        }

        // The object is going static (non-physical). Do any setup necessary for a static linkset.
        // Return 'true' if any properties updated on the passed object.
        // This doesn't normally happen -- WhiteCore removes the objects from the physical
        //     world if it is a static linkset.
        // Called at taint-time!
        public override bool MakeStatic(BSPrimLinkable child)
        {
            bool ret = false;
            DetailLog("{0},BSLinksetCompound.MakeStatic,call,IsRoot={1}", child.LocalID, IsRoot(child));
            child.ClearDisplacement();
            if (IsRoot(child))
            {
                Refresh(LinksetRoot);
            }
            return ret;
        }

        // 'physicalUpdate' is true if these changes came directly from the physics engine. Don't need to rebuild then.
        // Called at taint-time.
        public override void UpdateProperties(UpdatedProperties whichUpdated, BSPrimLinkable updated)
        {
            if (!LinksetRoot.IsPhysicallyActive)
            {
                // No reason to do this physical stuff for static linksets.
                DetailLog("{0},BSLinksetCompound.UpdateProperties,notPhysical", LinksetRoot.LocalID);
                return;
            }
            // The user moving a child around requires the rebuilding of the linkset compound shape
            // One problem is this happens when a border is crossed -- the simulator implementation
            //    stores the position into the group which causes the move of the object
            //    but it also means all the child positions get updated.
            //    What would cause an unnecessary rebuild so we make sure the linkset is in a
            //    region before bothering to do a rebuild.
            if (!IsRoot(updated) && PhysicsScene.TerrainManager.IsWithinKnownTerrain(LinksetRoot.RawPosition))
            {
                // If a child of the linkset is updating only the position or rotation, that can be done
                //    without rebuilding the linkset.
                // If a handle for the child can be fetch, we update the child here. If a rebuild was
                //    scheduled by someone else, the rebuild will just replace this setting.
                bool updatedChild = false;

                // Anything other than updating position or orientation usually means a physical update
                //     and that is caused by us updating the object.
                if ((whichUpdated & ~(UpdatedProperties.Position | UpdatedProperties.Orientation)) == 0)
                {
                    // Find the physical instance of the child 
                    if (!RebuildScheduled && !LinksetRoot.IsIncomplete && LinksetRoot.PhysShape.HasPhysicalShape &&
                        PhysicsScene.PE.IsCompound(LinksetRoot.PhysShape.physShapeInfo))
                    {
                        // It is possible that the linkset is still under construction and the child is not yet
                        //    inserted into the compound shape. A rebuild of the linkset in a pre-step action will
                        //    build the whole thing with the new position or rotation.
                        // The index must be checked because Bullet references the child array but does no validity
                        //    checking of the child index passed.
                        int numLinksetChildren =
                            PhysicsScene.PE.GetNumberOfCompoundChildren(LinksetRoot.PhysShape.physShapeInfo);
                        if (updated.LinksetChildIndex < numLinksetChildren)
                        {
                            BulletShape linksetChildShape =
                                PhysicsScene.PE.GetChildShapeFromCompoundShapeIndex(LinksetRoot.PhysShape.physShapeInfo,
                                    updated.LinksetChildIndex);
                            if (linksetChildShape.HasPhysicalShape)
                            {
                                // Found the child shape within the compound shape
                                PhysicsScene.PE.UpdateChildTransform(LinksetRoot.PhysShape.physShapeInfo,
                                    updated.LinksetChildIndex,
                                    updated.RawPosition - LinksetRoot.RawPosition,
                                    updated.RawOrientation*OMV.Quaternion.Inverse(LinksetRoot.RawOrientation),
                                    true /* shouldRecalculateLocalAabb */);
                                updatedChild = true;
                                DetailLog(
                                    "{0},BSLinksetCompound.UpdateProperties,changeChildPosRot,whichUpdated={1},pos={2},rot={3}",
                                    updated.LocalID, whichUpdated, updated.RawPosition, updated.RawOrientation);
                            }
                            else // DEBUG DEBUG
                            {
                                // DEBUG DEBUG
                                DetailLog(
                                    "{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild,noChildShape,shape={1}",
                                    updated.LocalID, linksetChildShape);
                            } // DEBUG DEBUG
                        }
                        else // DEBUG DEBUG
                        {
                            // DEBUG DEBUG
                            // the child is not yet in the compound shape. This is non-fatal.
                            DetailLog(
                                "{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild,childNotInCompoundShape,numChildren={1},index={2}",
                                updated.LocalID, numLinksetChildren, updated.LinksetChildIndex);
                        } // DEBUG DEBUG
                    }
                    else // DEBUG DEBUG
                    {
                        // DEBUG DEBUG
                        DetailLog("{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild,noBodyOrNotCompound",
                            updated.LocalID);
                    } // DEBUG DEBUG

                    if (!updatedChild)
                    {
                        // If couldn't do the individual child, the linkset needs a rebuild to incorporate the new child info.
                        // Note: there are several ways through this code that will not update the child if
                        //    the linkset is being rebuilt. In this case, scheduling a rebuild is a NOOP since
                        //    there will already be a rebuild scheduled.
                        DetailLog(
                            "{0},BSLinksetCompound.UpdateProperties,couldNotUpdateChild.schedulingRebuild,whichUpdated={1}",
                            updated.LocalID, whichUpdated);
                        Refresh(updated);
                    }
                }
            }
        }

        // TODO!!! rename to RemoveDependencies!
        // Routine called when rebuilding the body of some member of the linkset.
        // Since we don't keep in world relationships, do nothing unless it's a child changing.
        // Returns 'true' of something was actually removed and would need restoring
        // Called at taint-time!!
        public override bool RemoveBodyDependencies(BSPrimLinkable child)
        {
            bool ret = false;

            DetailLog("{0},BSLinksetCompound.RemoveBodyDependencies,refreshIfChild,rID={1},rBody={2},isRoot={3}",
                child.LocalID, LinksetRoot.LocalID, LinksetRoot.PhysBody, IsRoot(child));

            Refresh(child);

            return ret;
        }

        // When the linkset is built, the child shape is added to the compound shape relative to the
        //    root shape. The linkset then moves around but this does not move the actual child
        //    prim. The child prim's location must be recomputed based on the location of the root shape.
        void RecomputeChildWorldPosition(BSPrimLinkable child, bool inTaintTime)
        {
            // For the moment (20130201), disable this computation (converting the child physical addr back to
            //    a region address) until we have a good handle on center-of-mass offsets and what the physics
            //    engine moving a child actually means.
            // The simulator keeps track of where children should be as the linkset moves. Setting
            //    the pos/rot here does not effect that knowledge as there is no good way for the
            //    physics engine to send the simulator an update for a child.

            /*
            BSLinksetCompoundInfo lci = child.LinksetInfo as BSLinksetCompoundInfo;
            if (lci != null)
            {
                if (inTaintTime)
                {
                    OMV.Vector3 oldPos = child.RawPosition;
                    child.ForcePosition = LinksetRoot.RawPosition + lci.OffsetFromRoot;
                    child.ForceOrientation = LinksetRoot.RawOrientation * lci.OffsetRot;
                    DetailLog("{0},BSLinksetCompound.RecomputeChildWorldPosition,oldPos={1},lci={2},newPos={3}",
                                                child.LocalID, oldPos, lci, child.RawPosition);
                }
                else
                {
                    // TaintedObject is not used here so the raw position is set now and not at taint-time.
                    child.Position = LinksetRoot.RawPosition + lci.OffsetFromRoot;
                    child.Orientation = LinksetRoot.RawOrientation * lci.OffsetRot;
                }
            }
            else
            {
                // This happens when children have been added to the linkset but the linkset
                //     has not been constructed yet. So like, at taint time, adding children to a linkset
                //     and then changing properties of the children (makePhysical, for instance)
                //     but the post-print action of actually rebuilding the linkset has not yet happened.
                // PhysicsScene.Logger.WarnFormat("{0} Restoring linkset child position failed because of no relative position computed. ID={1}",
                //                                 LogHeader, child.LocalID);
                DetailLog("{0},BSLinksetCompound.recomputeChildWorldPosition,noRelativePositonInfo", child.LocalID);
            }
            */
        }

        // ================================================================

        // Add a new child to the linkset.
        // Called while LinkActivity is locked.
        protected override void AddChildToLinkset(BSPrimLinkable child)
        {
            if (!HasChild(child))
            {
                m_children.Add(child, new BSLinkInfo(child));

                DetailLog("{0},BSLinksetCompound.AddChildToLinkset,call,child={1}", LinksetRoot.LocalID, child.LocalID);

                // Rebuild the compound shape with the new child shape included
                Refresh(child);

            }
            return;
        }

        // Remove the specified child from the linkset.
        // Safe to call even if the child is not really in the linkset.
        protected override void RemoveChildFromLinkset(BSPrimLinkable child, bool inTaintTime)
        {
            child.ClearDisplacement();

            if (m_children.Remove(child))
            {
                DetailLog("{0},BSLinksetCompound.RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                    child.LocalID,
                    LinksetRoot.LocalID, LinksetRoot.PhysBody.AddrString,
                    child.LocalID, child.PhysBody.AddrString);

                // Cause the child's body to be rebuilt and thus restored to normal operation
                child.ForceBodyShapeRebuild(inTaintTime);

                if (!HasAnyChildren)
                {
                    // The linkset is now empty. The root needs rebuilding.
                    LinksetRoot.ForceBodyShapeRebuild(inTaintTime);
                }
                else
                {
                    // Rebuild the compound shape with the child removed
                    Refresh(LinksetRoot);
                }
            }
            return;
        }

        // Called before the simulation step to make sure the compound based linkset
        //    is all initialized.
        // Constraint linksets are rebuilt every time.
        // Note that this works for rebuilding just the root after a linkset is taken apart.
        // Called at taint time!!
        bool UseBulletSimRootOffsetHack = false;
            // Attempt to have Bullet track the coords of root compound shape

        void RecomputeLinksetCompound()
        {
            try
            {
                // Suppress rebuilding while rebuilding. (We know rebuilding is on only one thread.)
                Rebuilding = true;

                // No matter what is being done, force the root prim's PhysBody and PhysShape to get set
                //     to what they should be as if the root was not in a linkset.
                // Not that bad since we only get into this routine if there are children in the linkset and
                //     something has been updated/changed.
                // Have to do the rebuild before checking for physical because this might be a linkset
                //     being destructed and going non-physical.
                LinksetRoot.ForceBodyShapeRebuild(true);

                // There is no reason to build all this physical stuff for a non-physical or empty linkset.
                if (!LinksetRoot.IsPhysicallyActive || !HasAnyChildren)
                {
                    DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,notPhysicalOrNoChildren",
                        LinksetRoot.LocalID);
                    return; // Note the 'finally' clause at the botton which will get executed.
                }

                // Get a new compound shape to build the linkset shape in.
                BSShape linksetShape = BSShapeCompound.GetReference(PhysicsScene);

                // Compute a displacement for each component so it is relative to the center-of-mass.
                // Bullet presumes an object's origin (relative <0,0,0> is its center-of-mass
                OMV.Vector3 centerOfMassW = ComputeLinksetCenterOfMass();

                OMV.Quaternion invRootOrientation =
                    OMV.Quaternion.Normalize(OMV.Quaternion.Inverse(LinksetRoot.RawOrientation));
                OMV.Vector3 origRootPosition = LinksetRoot.RawPosition;

                // 'centerDisplacementV' is the vehicle relative distance from the simulator root position to the center-of-mass
                OMV.Vector3 centerDisplacementV = (centerOfMassW - LinksetRoot.RawPosition)*invRootOrientation;
                if (UseBulletSimRootOffsetHack || !BSParam.LinksetOffsetCenterOfMass)
                {
                    // Zero everything if center-of-mass displacement is not being done.
                    centerDisplacementV = OMV.Vector3.Zero;
                    LinksetRoot.ClearDisplacement();
                }
                else
                {
                    // The actual center-of-mass could have been set by the user.
                    centerDisplacementV = LinksetRoot.SetEffectiveCenterOfMassDisplacement(centerDisplacementV);
                }

                DetailLog("{0},BSLinksetCompound.RecumputeLinksetCompound,COM,rootPos={1},com={2},comDisp={3}",
                    LinksetRoot.LocalID, origRootPosition, centerOfMassW, centerDisplacementV);

                // Add the shapes of all the components of the linkset
                int memberIndex = 1;
                ForEachMember((cPrim) =>
                {
                    if (IsRoot(cPrim))
                    {
                        // Root shape is always index zero.
                        cPrim.LinksetChildIndex = 0;
                    }
                    else
                    {
                        cPrim.LinksetChildIndex = memberIndex;
                        memberIndex++;
                    }

                    // Get a reference to the shape of the child for adding of that shape to the linkset compound shape
                    BSShape childShape = cPrim.PhysShape.GetReference(PhysicsScene, cPrim);

                    // Offset the child shape from the center-of-mass and rotate it to root relative.
                    OMV.Vector3 offsetPos = (cPrim.RawPosition - origRootPosition)*invRootOrientation -
                                            centerDisplacementV;
                    OMV.Quaternion offsetRot = OMV.Quaternion.Normalize(cPrim.RawOrientation)*invRootOrientation;

                    // Add the child shape to the compound shape being build
                    if (childShape.physShapeInfo.HasPhysicalShape)
                    {
                        PhysicsScene.PE.AddChildShapeToCompoundShape(linksetShape.physShapeInfo,
                            childShape.physShapeInfo, offsetPos, offsetRot);
                        DetailLog(
                            "{0},BSLinksetCompound.RecomputeLinksetCompound,addChild,indx={1},cShape={2},offPos={3},offRot={4}",
                            LinksetRoot.LocalID, cPrim.LinksetChildIndex, childShape, offsetPos, offsetRot);

                        // Since we are borrowing the shape of the child, disable the original child body
                        if (!IsRoot(cPrim))
                        {
                            PhysicsScene.PE.AddToCollisionFlags(cPrim.PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);
                            PhysicsScene.PE.ForceActivationState(cPrim.PhysBody, ActivationState.DISABLE_SIMULATION);

                            // We don't want collision from the old linkset children.
                            PhysicsScene.PE.RemoveFromCollisionFlags(cPrim.PhysBody,
                                CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
                            cPrim.PhysBody.collisionType = CollisionType.LinksetChild;
                        }
                    }
                    else
                    {
                        // The linkset must be in an intermediate state where all the children have not yet
                        //    been constructed. This sometimes happens on startup when everything is getting
                        //    built and some shapes have to wait for assets to be read in.
                        // Just skip this linkset for the moment and cause the shape to be rebuilt next tick.
                        // One problem might be that the shape is broken somehow and it never becomes completely
                        //    available. This might cause the rebuild to happen over and over.
                        InternalScheduleRebuild(LinksetRoot);
                        DetailLog(
                            "{0},BSLinksetCompound.RecomputeLinksetCompound,addChildWithNoShape,indx={1},cShape={2},offPos={3},offRot={4}",
                            LinksetRoot.LocalID, cPrim.LinksetChildIndex, childShape, offsetPos, offsetRot);
                        // Output an annoying warning. It should only happen once but if it keeps coming out,
                        //    the user knows there is something wrong and will report it.
                        PhysicsScene.Logger.WarnFormat(
                            "{0} Linkset rebuild warning. If this happens more than one or two times, please report in the issue tracker",
                            LogHeader);
                        PhysicsScene.Logger.WarnFormat("{0} pName={1}, childIdx={2}, shape={3}",
                            LogHeader, LinksetRoot.Name, cPrim.LinksetChildIndex, childShape);

                        // This causes the loop to bail on building the rest of this linkset.
                        // The rebuild operation will fix it up next tick or declare the object unbuildable.
                        return true;
                    }
                    return false; // 'false' says to move anto the nex child in the list
                });

                // Replace the root shape with the built compound shape.
                // Object removed and added to world to get collision cache rebuilt for new shape.
                LinksetRoot.PhysShape.Dereference(PhysicsScene);
                LinksetRoot.PhysShape = linksetShape;
                PhysicsScene.PE.RemoveObjectFromWorld(PhysicsScene.World, LinksetRoot.PhysBody);
                PhysicsScene.PE.SetCollisionShape(PhysicsScene.World, LinksetRoot.PhysBody, linksetShape.physShapeInfo);
                PhysicsScene.PE.AddObjectToWorld(PhysicsScene.World, LinksetRoot.PhysBody);
                DetailLog("{0},BSLinksetCompound.RecomputeLinksetCompound,addBody,body={1},shape={2}",
                    LinksetRoot.LocalID, LinksetRoot.PhysBody, linksetShape);

                // With all of the linkset packed into the root prim, it has the mass of everyone.
                LinksetMass = ComputeLinksetMass();
                LinksetRoot.UpdatePhysicalMassProperties(LinksetMass, true);
                if (UseBulletSimRootOffsetHack)
                {
                    // Enable the physical position updator to return the position and rotation of the root shape.
                    // This enables a feature in the C++ code to return the world coordinates of the first shape in the
                    //      compound shape. This aleviates the need to offset the returned physical position by the
                    //      center-of-mass offset.
                    // TODO: either debug this feature or remove it.
                    PhysicsScene.PE.AddToCollisionFlags(LinksetRoot.PhysBody,
                        CollisionFlags.BS_RETURN_ROOT_COMPOUND_SHAPE);
                }
            }
            finally
            {
                Rebuilding = false;
            }

            // See that the Aabb surround the new shape
            PhysicsScene.PE.RecalculateCompoundShapeLocalAabb(LinksetRoot.PhysShape.physShapeInfo);
        }
    }
}
