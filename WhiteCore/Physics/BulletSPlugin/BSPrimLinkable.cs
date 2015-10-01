/*
 * Copyright (c) Contributors, http://opensimulator.org/, http://whitecore-sim.org
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Linq;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.SceneInfo;
using OMV = OpenMetaverse;

namespace WhiteCore.Physics.BulletSPlugin
{
    public class BSPrimLinkable : BSPrimDisplaced
    {
        public BSLinkset Linkset { get; set; }
        // The index of this child prim.
        public int LinksetChildIndex { get; set; }

        public BSLinksetInfo LinksetInfo { get; set; }

        public BSPrimLinkable(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
            OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical, int material, float friction,
            float restitution, float gravityMultiplier, float density)
            : base(localID, primName, parent_scene, pos, size, rotation, pbs, pisPhysical)
        {
            Linkset = BSLinkset.Factory(PhysicsScene, this);

            PhysicsScene.TaintedObject("BSPrimLinksetCompound.Refresh", delegate()
            {
                SetMaterial(material);
                Friction = friction;
                Restitution = restitution;
                GravityMultiplier = gravityMultiplier;
                Density = density;
                Linkset.Refresh(this);
            });
        }

        public override void Destroy()
        {
            if (!Linkset.LinksetRoot.BlockPhysicalReconstruction)
                //If we are disabled, this entire linkset is being removed, so allow it to happen
                Linkset = Linkset.RemoveMeFromLinkset(this);
            base.Destroy();
        }

        public override BSPhysicsShapeType PreferredPhysicalShape
        {
            get { return Linkset.PreferredPhysicalShape(this); }
        }

        public override void link(PhysicsActor obj)
        {
            BSPrimLinkable parent = obj as BSPrimLinkable;
            if (parent != null)
            {
                BSPhysObject parentBefore = Linkset.LinksetRoot;
                int childrenBefore = Linkset.NumberOfChildren;

                Linkset = parent.Linkset.AddMeToLinkset(this);

                DetailLog(
                    "{0},BSPrimLinkset.link,call,parentBefore={1}, childrenBefore=={2}, parentAfter={3}, childrenAfter={4}",
                    LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
            }
            return;
        }

        public override void linkGroupToThis(PhysicsActor[] objs)
        {
            Linkset.AddGroupToLinkset(objs.Cast<BSPrimLinkable>().ToArray());

            DetailLog("{0},BSPrimLinkset.linkGroupToThis,call, childrenAdded={1}, childrenAfter={2}",
                LocalID, objs.Length, Linkset.NumberOfChildren);
        }

        public override void delink()
        {
            // TODO: decide if this parent checking needs to happen at taint time
            // Race condition here: if link() and delink() in same simulation tick, the delink will not happen

            BSPhysObject parentBefore = Linkset.LinksetRoot;
            int childrenBefore = Linkset.NumberOfChildren;

            Linkset = Linkset.RemoveMeFromLinkset(this);

            DetailLog(
                "{0},BSPrimLinkset.delink,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}, ",
                LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
            return;
        }

        // When simulator changes position, this might be moving a child of the linkset.
        public override OMV.Vector3 Position
        {
            get { return base.Position; }
            set
            {
                base.Position = value;
                PhysicsScene.TaintedObject("BSPrimLinkset.setPosition",
                    delegate() { Linkset.UpdateProperties(UpdatedProperties.Position, this); });
            }
        }

        // When simulator changes orientation, this might be moving a child of the linkset.
        public override OMV.Quaternion Orientation
        {
            get { return base.Orientation; }
            set
            {
                base.Orientation = value;
                PhysicsScene.TaintedObject("BSPrimLinkset.setOrientation",
                    delegate() { Linkset.UpdateProperties(UpdatedProperties.Orientation, this); });
            }
        }

        public override float TotalMass
        {
            get { return Linkset.LinksetMass; }
        }

    public override OMV.Vector3 CenterOfMass
    {
        get { return Linkset.CenterOfMass; }
    }

    public OMV.Vector3 GeometricCenter   // was override
    {
        get { return Linkset.GeometricCenter; }
    }
        public override void UpdatePhysicalParameters()
        {
            base.UpdatePhysicalParameters();
            // Recompute any linkset parameters.
            // When going from non-physical to physical, this re-enables the constraints that
            //     had been automatically disabled when the mass was set to zero.
            // For compound based linksets, this enables and disables interactions of the children.
            if (Linkset != null) // null can happen during initialization
                Linkset.Refresh(this);
        }

        protected override void MakeDynamic(bool makeStatic)
        {
            base.MakeDynamic(makeStatic);
            if (Linkset != null)    // null can happen during initialization
            {
                if (makeStatic)
                    Linkset.MakeStatic(this);
                else
                    Linkset.MakeDynamic(this);
            }
        }

        // Body is being taken apart. Remove physical dependencies and schedule a rebuild.
        protected override void RemoveBodyDependencies()
        {
            Linkset.RemoveBodyDependencies(this);
            base.RemoveBodyDependencies();
        }

        public override void UpdateProperties(EntityProperties entprop)
        {
            if (Linkset.IsRoot(this))
            {
                // Properties are only updated for the roots of a linkset.
                // TODO: this will have to change when linksets are articulated.
                base.UpdateProperties(entprop);
            }
            /*
        else
        {
            // For debugging, report the movement of children
            DetailLog("{0},BSPrim.UpdateProperties,child,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, entprop.Position, entprop.Rotation, entprop.Velocity,
                    entprop.Acceleration, entprop.RotationalVelocity);
        }
             */
            // The linkset might like to know about changing locations
            Linkset.UpdateProperties(UpdatedProperties.EntPropUpdates, this);
        }

        public override bool Collide(uint collidingWith, BSPhysObject collidee,
            OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
        {
            // prims in the same linkset cannot collide with each other
            BSPrimLinkable convCollidee = collidee as BSPrimLinkable;
            if (convCollidee != null && (this.Linkset.LinksetID == convCollidee.Linkset.LinksetID))
            {
                return false;
            }
            return base.Collide(collidingWith, collidee, contactPoint, contactNormal, pentrationDepth);
        }



    // A linkset reports any collision on any part of the linkset.
    public long SomeCollisionSimulationStep = 0;
    public override bool IsColliding
    {
        get
        {
            return (SomeCollisionSimulationStep == PhysicsScene.SimulationStep) || base.IsColliding;
        }
        set
        {
            if (value)
                    SomeCollisionSimulationStep = PhysicsScene.SimulationStep;
            else
                SomeCollisionSimulationStep = 0;

                base.IsColliding = value;
        }
    }

 /* not sure what this is for yet - 20150925 -
        // Convert the existing linkset of this prim into a new type.
    public bool ConvertLinkset(BSLinkset.LinksetImplementation newType)
    {
        bool ret = false;
        if (LinksetType != newType)
        {
            DetailLog("{0},BSPrimLinkable.ConvertLinkset,oldT={1},newT={2}", LocalID, LinksetType, newType);

            // Set the implementation type first so the call to BSLinkset.Factory gets the new type.
            this.LinksetType = newType;

            BSLinkset oldLinkset = this.Linkset;
            BSLinkset newLinkset = BSLinkset.Factory(PhysScene, this);

            this.Linkset = newLinkset;

            // Pick up any physical dependencies this linkset might have in the physics engine.
            oldLinkset.RemoveDependencies(this);

            // Create a list of the children (mainly because can't interate through a list that's changing)
            List<BSPrimLinkable> children = new List<BSPrimLinkable>();
            oldLinkset.ForEachMember((child) =>
            {
                if (!oldLinkset.IsRoot(child))
                    children.Add(child);
                return false;   // 'false' says to continue to next member
            });

            // Remove the children from the old linkset and add to the new (will be a new instance from the factory)
            foreach (BSPrimLinkable child in children)
            {
                oldLinkset.RemoveMeFromLinkset(child, true);
            }
            foreach (BSPrimLinkable child in children)
            {
                newLinkset.AddMeToLinkset(child);
                child.Linkset = newLinkset;
            }

            // Force the shape and linkset to get reconstructed
            newLinkset.Refresh(this);
            this.ForceBodyShapeRebuild(true/);
        }
        return ret;
    }
*/
        /* not ported yet - 20150925
        #region Extension
    public override object Extension(string pFunct, params object[] pParams)
    {
        DetailLog("{0} BSPrimLinkable.Extension,op={1},nParam={2}", LocalID, pFunct, pParams.Length);
        object ret = null;
        switch (pFunct)
        {
            // physGetLinksetType();
            // pParams = [ BSPhysObject root, null ]
            case ExtendedPhysics.PhysFunctGetLinksetType:
            {
                ret = (object)LinksetType;
                DetailLog("{0},BSPrimLinkable.Extension.physGetLinksetType,type={1}", LocalID, ret);
                break;
            }
            // physSetLinksetType(type);
            // pParams = [ BSPhysObject root, null, integer type ]
            case ExtendedPhysics.PhysFunctSetLinksetType:
            {
                if (pParams.Length > 2)
                {
                    BSLinkset.LinksetImplementation linksetType = (BSLinkset.LinksetImplementation)pParams[2];
                    if (Linkset.IsRoot(this))
                    {
                            PhysicsScene.TaintedObject(LocalID, "BSPrim.PhysFunctSetLinksetType", delegate()
                        {
                            // Cause the linkset type to change
                            DetailLog("{0},BSPrimLinkable.Extension.physSetLinksetType, oldType={1},newType={2}",
                                                LocalID, Linkset.LinksetImpl, linksetType);
                            ConvertLinkset(linksetType);
                        });
                    }
                    ret = (object)(int)linksetType;
                }
                break;
            }
            // physChangeLinkType(linknum, typeCode);
            // pParams = [ BSPhysObject root, BSPhysObject child, integer linkType ]
            case ExtendedPhysics.PhysFunctChangeLinkType:
            {
                ret = Linkset.Extension(pFunct, pParams);
                break;
            }
            // physGetLinkType(linknum);
            // pParams = [ BSPhysObject root, BSPhysObject child ]
            case ExtendedPhysics.PhysFunctGetLinkType:
            {
                ret = Linkset.Extension(pFunct, pParams);
                break;
            }
            // physChangeLinkParams(linknum, [code, value, code, value, ...]);
            // pParams = [ BSPhysObject root, BSPhysObject child, object[] [ string op, object opParam, string op, object opParam, ... ] ]
            case ExtendedPhysics.PhysFunctChangeLinkParams:
            {
                ret = Linkset.Extension(pFunct, pParams);
                break;
            }
            default:
                ret = base.Extension(pFunct, pParams);
                break;
        }
        return ret;
    }
    #endregion  // Extension
    */
}
}
