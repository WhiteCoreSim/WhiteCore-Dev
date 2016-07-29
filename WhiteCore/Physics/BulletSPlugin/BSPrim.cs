﻿/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://virtualnexus.eu/, http://aurora-sim.org/, http://opensimulator.org/
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

using System;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;
using OMV = OpenMetaverse;

namespace WhiteCore.Physics.BulletSPlugin
{
    [Serializable]
    public class BSPrim : BSPhysObject
    {
        static readonly string LogHeader = "[BULLETS PRIM]";

        // _size is what the user passed. Scale is what we pass to the physics engine with the mesh.
        OMV.Vector3 _size; // the multiplier for each mesh dimension as passed by the user


        int _physicsActorType;
        bool _isSelected;
        bool _isVolumeDetect;
        bool _grabbed;
        bool _kinematic;

        // _position is what the simulator thinks the positions of the prim is.
        OMV.Vector3 _position;
        float _mass; // the mass of this object
        OMV.Vector3 _acceleration;
        OMV.Quaternion _orientation;
        bool _isPhysical;
        bool _flying;
        bool _setAlwaysRun;
        bool _throttleUpdates;
        bool _floatOnWater;
        OMV.Vector3 _rotationalVelocity;
        float _buoyancy;

        int CrossingFailures { get; set; }

        public BSDynamics VehicleActor;
        public const string VehicleActorName = "BasicVehicle";

        public const string HoverActorName = "HoverActor";
        public const String LockedAxisActorName = "BSPrim.LockedAxis";
        public const string MoveToTargetActorName = "MoveToTargetActor";
        public const string SetForceActorName = "SetForceActor";
        public const string SetTorqueActorName = "SetTorqueActor";

        public BSPrim(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
            OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
            : base(parent_scene, localID, primName, "BSPrim")
        {
            // MainConsole.Instance.DebugFormat("{0}: BSPrim creation of {1}, id={2}", LogHeader, primName, localID);
            _physicsActorType = (int)ActorTypes.Prim;
            _position = pos;
            _size = size;
            Scale = size; // prims are the size the user wants them to be (different for BSCharactes).
            _orientation = rotation;
            _buoyancy = 0f;
            RawVelocity = OMV.Vector3.Zero;
            _rotationalVelocity = OMV.Vector3.Zero;
            BaseShape = pbs;
            _isPhysical = pisPhysical;
            _isVolumeDetect = false;

            // Add a dynamic vehicle to our set of actors that can move this prim.
	          VehicleActor = new BSDynamics(PhysicsScene, this, VehicleActorName);
            PhysicalActors.Add(VehicleActorName, VehicleActor);
            //PhysicalActors.Add(VehicleActorName, new BSDynamics(PhysicsScene, this, VehicleActorName));

            _mass = CalculateMass();

            // DetailLog("{0},BSPrim.constructor,call", LocalID);
            // do the actual object creation at taint time
            PhysicsScene.TaintedObject(LocalID, "BSPrim.create", delegate()
            {
                // Make sure the object is being created with some sanity.
                ExtremeSanityCheck(true /* inTaintTime */);

                CreateGeomAndObject(true);

                CurrentCollisionFlags = PhysicsScene.PE.GetCollisionFlags(PhysBody);

                IsInitialized = true;
            });
        }

        // called when this prim is being destroyed and we should free all the resources
        public override void Destroy()
        {
            // MainConsole.Instance.DebugFormat("{0}: Destroy, id={1}", LogHeader, LocalID);
            IsInitialized = false;

            base.Destroy();

            // Undo any vehicle properties
            this.VehicleType = (int)Vehicle.TYPE_NONE;

            PhysicsScene.TaintedObject(LocalID, "BSPrim.Destroy", delegate()
            {
                DetailLog("{0},BSPrim.Destroy,taint,", LocalID);
                // If there are physical body and shape, release my use of same.
                PhysicsScene.Shapes.DereferenceBody(PhysBody, null);
                PhysBody.Clear();
                PhysShape.Dereference(PhysicsScene);
                PhysShape = new BSShapeNull();
            });
        }

        public override bool IsIncomplete
        {
            get { return ShapeRebuildScheduled; }
        }

        // 'true' if this obejct's shape is in need of a rebuild and a rebuild has been queued.
        // The prim is still available but its underlying shape will change soon.
        // This is protected by a 'lock(this)'.
        public bool ShapeRebuildScheduled { get; protected set; }

        public override OMV.Vector3 Size
        {
            get { return _size; }
            set
            {
                // We presume the scale and size are the same. If scale must be changed for
                //     the physical shape, that is done when the geometry is built.
                _size = value;
                Scale = _size;
                ForceBodyShapeRebuild(false);
            }
        }

        public override PrimitiveBaseShape Shape
        {
            set
            {
                BaseShape = value;
                DetailLog("{0},BSPrim.changeShape,pbs={1}", LocalID, BSScene.PrimitiveBaseShapeToString(BaseShape));
                PrimAssetState = PrimAssetCondition.Unknown;
                ForceBodyShapeRebuild(false);
            }
        }

        // 'unknown' says to choose the best type
        public override BSPhysicsShapeType PreferredPhysicalShape
        {
            get { return BSPhysicsShapeType.SHAPE_UNKNOWN; }
        }

        public override bool ForceBodyShapeRebuild(bool inTaintTime)
        {
            if (inTaintTime)
            {
                _mass = CalculateMass(); // changing the shape changes the mass
                CreateGeomAndObject(true);
            }
            else
            {
                lock (this)
                {
                    // If a rebuild is not already in the queue
                    if (!ShapeRebuildScheduled)
                    {
                        // Remember that a rebuild is queued -- this is used to flag an imcomplete object
                        ShapeRebuildScheduled = true;
                        PhysicsScene.TaintedObject(LocalID, "BSPrim.ForceBodyShapeRebuild", delegate()
                        {
                            _mass = CalculateMass(); // changing the shape changes the mass
                            CreateGeomAndObject(true);
                            ShapeRebuildScheduled = false;
                        });
                    }
                }


            }
            return true;
        }

        public override bool Grabbed {
            set { _grabbed = value; }
        }
        public override bool Selected
        {
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    PhysicsScene.TaintedObject(LocalID, "BSPrim.setSelected", delegate()
                    {
                        DetailLog("{0},BSPrim.selected,taint,selected={1}", LocalID, _isSelected);
                        SetObjectDynamic(false);
                   //SelectObject(_isSelected);   //??

                    });
                }
            }
        }

        protected virtual void SelectObject(bool val)
        {
            if (!val)
            {
                //Don't make objects phantom when selecting
                //PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);
                //Reenable collision events
                if (SubscribedEvents())
                    EnableCollisions(true);
                PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.ACTIVE_TAG);

                // Don't force activation so setting of DISABLE_SIMULATION can stay if used.
                PhysicsScene.PE.Activate(PhysBody, false);
            }
            else
            {
                //Don't make objects phantom when selecting
                //PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);
                PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.DISABLE_SIMULATION);
                //Disable collision events
                EnableCollisions(false);
            }
        }

        public override bool IsSelected
        {
            get { return _isSelected; }
        }

        public override void CrossingFailure()
        {
            CrossingFailures++;
            if (CrossingFailures > BSParam.CrossingFailuresBeforeOutOfBounds)
            {
                RaiseOutOfBounds(RawPosition);
            }
            else if (CrossingFailures == BSParam.CrossingFailuresBeforeOutOfBounds)
            {
                MainConsole.Instance.WarnFormat("{0} Too many crossing failures for {1}", LogHeader, Name);
            }
            return;
        }

        // link me to the specified parent
        public override void Link(PhysicsActor obj)
        {
        }

        public override void LinkGroupToThis(PhysicsActor[] objs)
        {
        }

        // delink me from my linkset
        public override void Delink()
        {
        }

        // Set motion values to zero.
        // Do it to the properties so the values get set in the physics engine.
        // Push the setting of the values to the viewer.
        // Called at taint time!
        public override void ZeroMotion(bool inTaintTime)
        {
            RawVelocity = OMV.Vector3.Zero;
            _acceleration = OMV.Vector3.Zero;
            _rotationalVelocity = OMV.Vector3.Zero;

            // Zero some other properties in the physics engine
            PhysicsScene.TaintedObject(inTaintTime, "BSPrim.ZeroMotion", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                    PhysicsScene.PE.ClearAllForces(PhysBody);
            });
        }

        public override void ZeroAngularMotion(bool inTaintTime)
        {
            _rotationalVelocity = OMV.Vector3.Zero;
            // Zero some other properties in the physics engine
            PhysicsScene.TaintedObject(inTaintTime, "BSPrim.ZeroMotion", delegate()
            {
                // DetailLog("{0},BSPrim.ZeroAngularMotion,call,rotVel={1}", LocalID, _rotationalVelocity);
                if (PhysBody.HasPhysicalBody)
                {
                    PhysicsScene.PE.SetInterpolationAngularVelocity(PhysBody, _rotationalVelocity);
                    PhysicsScene.PE.SetAngularVelocity(PhysBody, _rotationalVelocity);
                }
            });
        }

        public override void LockAngularMotion(OMV.Vector3 axis)
        {
            DetailLog("{0},BSPrim.LockAngularMotion,call,axis={1}", LocalID, axis);

            ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR, 0f, 0f);
            if (axis.X != 1)
            {
                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_X, 0f, 0f);
            }
            if (axis.Y != 1)
            {
                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_ANGULAR_Y, 0f, 0f);
            }
            if (axis.Z != 1)
            {
                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_ANGULAR_Z, 0f, 0f);
            }

            InitializeAxisActor();
            return;
        }

        public override OMV.Vector3 RawPosition
        {
            get { return _position; }
            set { _position = value; }
        }

        public override OMV.Vector3 Position
        {
            get
            {
                // don't do the GetObjectPosition for root elements because this function is called a zillion times.
                // _position = ForcePosition;
                return _position;
            }
            set
            {
                // If the position must be forced into the physics engine, use ForcePosition.
                // All positions are given in world positions.
                if (_position == value)
                {
                    DetailLog("{0},BSPrim.setPosition,call,positionNotChanging,pos={1},orient={2}", LocalID, _position,
                        _orientation);
                    return;
                }
                _position = value;
                PositionSanityCheck(false);

                PhysicsScene.TaintedObject(LocalID, "BSPrim.setPosition", delegate()
                {
                    DetailLog("{0},BSPrim.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                    ForcePosition = _position;
                });
            }
        }

        // NOTE: overloaded by BSPrimDisplaced to handle offset for center-of-gravity.
        public override OMV.Vector3 ForcePosition
        {
            get
            {
                _position = PhysicsScene.PE.GetPosition(PhysBody);
                return _position;
            }
            set
            {
                _position = value;
                if (PhysBody.HasPhysicalBody)
                {
                    bool selected = IsSelected;
                    if (selected)
                    {
                        _isSelected = false;
                        SelectObject(_isSelected);
                    }
                    PhysicsScene.PE.SetTranslation(PhysBody, _position, _orientation);
                    ActivateIfPhysical(false);
                    if (selected)
                    {
                        if (IsPhysicallyActive && PhysBody.collisionType != CollisionType.Static)
                            PhysicsScene.PE.UpdateSingleAabb(PhysicsScene.World, PhysBody);
                        _isSelected = true;
                        SelectObject(_isSelected);
                    }
                }
            }
        }

        // Check that the current position is sane and, if not, modify the position to make it so.
        // Check for being below terrain and being out of bounds.
        // Returns 'true' of the position was made sane by some action.
        bool PositionSanityCheck(bool inTaintTime)
        {
            bool ret = false;

            // We don't care where non-physical items are placed
            if (!IsPhysicallyActive)
                return ret;

            if (!PhysicsScene.TerrainManager.IsWithinKnownTerrain(RawPosition))
            {
                // The physical object is out of the known/simulated area.
                // Upper levels of code will handle the transition to other areas so, for
                //     the time, we just ignore the position.
                return ret;
            }

            float terrainHeight = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(RawPosition);
            OMV.Vector3 upForce = OMV.Vector3.Zero;
            float approxSize = Math.Max(Size.X, Math.Max(Size.Y, Size.Z));
            if ((RawPosition.Z + approxSize / 2f) < terrainHeight)
            {
                DetailLog("{0},BSPrim.PositionAdjustUnderGround,call,pos={1},terrain={2}", LocalID, RawPosition,
                    terrainHeight);
                float targetHeight = terrainHeight + (Size.Z / 2f);
                // If the object is below ground it just has to be moved up because pushing will
                //     not get it through the terrain
                //_position = new OMV.Vector3(_position.X, _position.Y, targetHeight);
                _position.Z = targetHeight;
                if (inTaintTime)
                {
                    ForcePosition = _position;
                }
                // If we are throwing the object around, zero its other forces
                ZeroMotion(inTaintTime);
                ret = true;
            }

            if ((CurrentCollisionFlags & CollisionFlags.BS_FLOATS_ON_WATER) != 0)
            {
                float waterHeight = PhysicsScene.TerrainManager.GetWaterLevelAtXYZ(_position);
                // TODO: a floating motor so object will bob in the water
                if (Math.Abs(RawPosition.Z - waterHeight) > 0.1f)
                {
                    // Upforce proportional to the distance away from the water. Correct the error in 1 sec.
                    upForce.Z = (waterHeight - RawPosition.Z) * 1f;

                    // Apply upforce and overcome gravity.
                    OMV.Vector3 correctionForce = upForce - PhysicsScene.DefaultGravity;
                    DetailLog("{0},BSPrim.PositionSanityCheck,applyForce,pos={1},upForce={2},correctionForce={3}",
                        LocalID, _position, upForce, correctionForce);
                    AddForce(correctionForce, false, inTaintTime);
                    ret = true;
                }
            }

            return ret;
        }

        // Occasionally things will fly off and really get lost.
        // Find the wanderers and bring them back.
        // Return 'true' if some parameter need some sanity.
        bool ExtremeSanityCheck(bool inTaintTime)
        {
            bool ret = false;

            // There have been instances of objects getting thrown way out of bounds and crashing
            //    the border crossing code.
//            uint wayOutThere = Constants.RegionSize * Constants.RegionSize;
//            if (_position.X < -Constants.RegionSize || _position.X > wayOutThere
//                || _position.Y < -Constants.RegionSize || _position.Y > wayOutThere
//                || _position.Z < -Constants.RegionSize || _position.Z > wayOutThere)
//            {
            int wayOutThere = 10000;
            int wayUnderThere = -10000;
            if (_position.X < wayUnderThere || _position.X > wayOutThere
                || _position.Y < wayUnderThere || _position.Y > wayOutThere
                || _position.Z < wayUnderThere || _position.Z > wayOutThere)
            {
                _position = new OMV.Vector3(10, 10, 50);
                ZeroMotion(inTaintTime);
                ret = true;
            }
//            if (RawVelocity.LengthSquared() > BSParam.MaxLinearVelocity)
            if (RawVelocity.LengthSquared() > BSParam.MaxLinearVelocitySquared)
            {
                RawVelocity = Util.ClampV(RawVelocity, BSParam.MaxLinearVelocity);
                ret = true;
            }
            if (_rotationalVelocity.LengthSquared() > BSParam.MaxAngularVelocitySquared)
            {
                _rotationalVelocity = Util.ClampV(_rotationalVelocity, BSParam.MaxAngularVelocity);
                ret = true;
            }

            return ret;
        }

        // Return the effective mass of the object.
        // The definition of this call is to return the mass of the prim.
        // If the simulator cares about the mass of the linkset, it will sum it itself.
        public override float Mass
        {
            get { return _mass; }
        }

        // TotalMass returns the mass of the large object the prim may be in (overridden by linkset code)
        public virtual float TotalMass
        {
            get { return _mass; }
        }

        // used when we only want this prim's mass and not the linkset thing
        public override float RawMass
        {
            get { return _mass; }
        }

        // Set the physical mass to the passed mass.
        // Note that this does not change _mass!
        public override void UpdatePhysicalMassProperties(float physMass, bool inWorld)
        {
            if (PhysBody.HasPhysicalBody && PhysShape.HasPhysicalShape)
            {
                if (IsStatic)
                {
                    PhysicsScene.PE.SetGravity(PhysBody, PhysicsScene.DefaultGravity);
                    Inertia = OMV.Vector3.Zero;
                    PhysicsScene.PE.SetMassProps(PhysBody, 0.1f, Inertia);    // 20160601 - greythane - was 0f for mass which will always error
                    PhysicsScene.PE.UpdateInertiaTensor(PhysBody);
                }
                else
                {
                    if (inWorld)
                    {
                        // Changing interesting properties doesn't change proxy and collision cache
                        //    information. The Bullet solution is to re-add the object to the world
                        //    after parameters are changed.
                        PhysicsScene.PE.RemoveObjectFromWorld(PhysicsScene.World, PhysBody);
                    }

                    // The computation of mass props requires gravity to be set on the object.
                    Gravity = ComputeGravity(Buoyancy);
                    PhysicsScene.PE.SetGravity(PhysBody, Gravity);

                    Inertia = PhysicsScene.PE.CalculateLocalInertia(PhysShape.physShapeInfo, physMass);
                    PhysicsScene.PE.SetMassProps(PhysBody, physMass, Inertia);
                    PhysicsScene.PE.UpdateInertiaTensor(PhysBody);

                    DetailLog("{0},BSPrim.UpdateMassProperties,mass={1},localInertia={2},grav={3},inWorld={4}",
                        LocalID, physMass, Inertia, Gravity, inWorld);

                    if (inWorld)
                    {
                        AddObjectToPhysicalWorld();
                    }
                }
            }
        }

        // Return what gravity should be set to this very moment
        public OMV.Vector3 ComputeGravity(float buoyancy)
        {
            OMV.Vector3 ret = PhysicsScene.DefaultGravity;

            if (!IsStatic)
            {
                ret *= (1f - buoyancy);
                ret *= GravityMultiplier;
            }

            return ret;
        }

        // Is this used?
        public override OMV.Vector3 CenterOfMass
        {
            get { return RawPosition; }
        }

        public override OMV.Vector3 Force
        {
            get { return RawForce; }
            set
            {
                RawForce = value;
                EnableActor(RawForce != OMV.Vector3.Zero, SetForceActorName,
                    delegate() { return new BSActorSetForce(PhysicsScene, this, SetForceActorName); });
            }
        }


        public override int VehicleType
        {
            get
            {
                return (int)VehicleActor.Type;
            }
            set
            {
                Vehicle type = (Vehicle)value;

                PhysicsScene.TaintedObject (LocalID,"setVehicleType", delegate() {
                    // Vehicle code changes the parameters for this vehicle type.
                    VehicleActor.ProcessTypeChange (type);
                    ActivateIfPhysical (false);
                });
            }
        }

        public override void VehicleFloatParam (int param, float value)
        {
            PhysicsScene.TaintedObject(LocalID, "BSPrim.VehicleFloatParam", delegate() {
                VehicleActor.ProcessFloatVehicleParam ((Vehicle)param, value);
                ActivateIfPhysical (false);
            });
        }

        // override for vector parameters
        public override void VehicleVectorParam(int param, OMV.Vector3 value)
        {
            PhysicsScene.TaintedObject(LocalID, "BSPrim.VehicleVectorParam", delegate()
            {
                VehicleActor.ProcessVectorVehicleParam((Vehicle)param, value);
                ActivateIfPhysical(false);
            });
        }

        public override void VehicleRotationParam(int param, OMV.Quaternion rotation)
        {
            PhysicsScene.TaintedObject(LocalID, "BSPrim.VehicleRotationParam", delegate()
            {
                VehicleActor.ProcessRotationVehicleParam((Vehicle)param, rotation);
                ActivateIfPhysical(false);
            });
        }
        public override void VehicleFlags(int param, bool remove)
        {
            PhysicsScene.TaintedObject(LocalID, "BSPrim.VehicleFlags", delegate()
            {
                VehicleActor.ProcessVehicleFlags(param, remove);
            });
        }

        //TODO!!!!   -greythane- 20151006 - use the VolumneDetect below, to directly set rather than the OS implementation
        //
        // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more info
        //public override void SetVolumeDetect(int param)

        public override bool VolumeDetect
        {
            get { return _isVolumeDetect; }
            set
            {
                if (_isVolumeDetect != value)
                {
                    _isVolumeDetect = value;
                    PhysicsScene.TaintedObject(LocalID, "BSPrim.SetVolumeDetect", delegate()
                    {
                        // DetailLog("{0},setVolumeDetect,taint,volDetect={1}", LocalID, _isVolumeDetect);
                        SetObjectDynamic(true);
                        ZeroMotion(true);
                    });
                }
            }
        }

       public override bool IsVolumeDetect
       {
           get { return _isVolumeDetect; }
       }

       public override void SetMaterial(int material, float friction, float restitution, float gravityMultiplier,
            float density)
        {
            base.SetMaterial(material);
            base.Friction = friction;
            base.Restitution = restitution;
            base.GravityMultiplier = gravityMultiplier;
            base.Density = density;
            PhysicsScene.TaintedObject(LocalID, "BSPrim.SetMaterial", delegate() { UpdatePhysicalParameters(); });
        }

    public override float Friction
    {
        get { return base.Friction; }
        set
        {
            if (base.Friction != value)
            {
                base.Friction = value;
                PhysicsScene.TaintedObject(LocalID, "BSPrim.setFriction", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }
    public override float Restitution
    {
        get { return base.Restitution; }
        set
        {
            if (base.Restitution != value)
            {
                base.Restitution = value;
                PhysicsScene.TaintedObject(LocalID, "BSPrim.setRestitution", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }
    // The simulator/viewer keeps density as 100kg/m3.
    // Remember to use BSParam.DensityScaleFactor to create the physical density.
    public override float Density
    {
        get { return base.Density; }
        set
        {
            if (base.Density != value)
            {
                base.Density = value;
                PhysicsScene.TaintedObject(LocalID, "BSPrim.setDensity", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }
    public override float GravityMultiplier   // maybe should be GravityMultiplier ??
    {
            get { return base.GravityMultiplier; }
        set
        {
                if (base.GravityMultiplier != value)
            {
                    base.GravityMultiplier = value;
                    PhysicsScene.TaintedObject(LocalID, "BSPrim.setGravityMultiplier", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }

        public override OMV.Vector3 Velocity
        {
            get { return RawVelocity; }
            set
            {
                RawVelocity = value;
                PhysicsScene.TaintedObject(LocalID, "BSPrim.setVelocity", delegate()
                {
                    // DetailLog("{0},BSPrim.SetVelocity,taint,vel={1}", LocalID, RawVelocity);
                    ForceVelocity = RawVelocity;
                });
            }
        }

        public override OMV.Vector3 ForceVelocity
        {
            get { return RawVelocity; }
            set
            {
                PhysicsScene.AssertInTaintTime("BSPrim.ForceVelocity");

                RawVelocity = Util.ClampV(value, BSParam.MaxLinearVelocity);
                if (PhysBody.HasPhysicalBody)
                {
                    DetailLog("{0},BSPrim.ForceVelocity,taint,vel={1}", LocalID, RawVelocity);
                    PhysicsScene.PE.SetLinearVelocity(PhysBody, RawVelocity);
                    ActivateIfPhysical(false);
                }
            }
        }

        public override OMV.Vector3 Torque
        {
            get { return RawTorque; }
            set
            {
                RawTorque = value;
                EnableActor(RawTorque != OMV.Vector3.Zero, SetTorqueActorName,
                    delegate() { return new BSActorSetTorque(PhysicsScene, this, SetTorqueActorName); });
                // DetailLog("{0},BSPrim.SetTorque,call,torque={1}", LocalID, _torque);

            // This appears to cause crashes => OS import
                // Call update so actor Refresh() is called to start things off
            //PhysicsScene.TaintedObject("BSPrim.setTorque", delegate()
            //{
            //    UpdatePhysicalParameters();
            //});
            }
        }

        public override OMV.Vector3 Acceleration
        {
            get { return _acceleration; }
            set { _acceleration = value; }
        }

        public override OMV.Quaternion RawOrientation
        {
            get { return _orientation; }
            set { _orientation = value; }
        }

        public override OMV.Quaternion Orientation
        {
            get { return _orientation; }
            set
            {
                if (_orientation == value)
                    return;
                _orientation = value;

                PhysicsScene.TaintedObject(LocalID,"BSPrim.setOrientation", delegate() { ForceOrientation = RawOrientation; });
            }
        }

        // Go directly to Bullet to get/set the value.
        public override OMV.Quaternion ForceOrientation
        {
            get
            {
                _orientation = PhysicsScene.PE.GetOrientation(PhysBody);
                return _orientation;
            }
            set
            {
                _orientation = value;
                if (PhysBody.HasPhysicalBody)
                    PhysicsScene.PE.SetTranslation(PhysBody, _position, _orientation);
            }
        }

        // we are a prim here
        public override int PhysicsActorType
        {
            get { return _physicsActorType; }
            set { _physicsActorType = value; }
        }

        public override bool IsPhysical
        {
            get { return _isPhysical; }
            set
            {
                if (_isPhysical != value)
                {
                    _isPhysical = value;
                    PhysicsScene.TaintedObject(LocalID,"BSPrim.setIsPhysical", delegate()
                    {
                        DetailLog("{0},setIsPhysical,taint,isPhys={1}", LocalID, _isPhysical);
                        SetObjectDynamic(true);
                        // whether phys-to-static or static-to-phys, the object is not moving.
                        ZeroMotion(true);
                    });
                }
            }
        }

        // An object is static (does not move) if selected or not physical
        public override bool IsStatic
        {
            get { return /*_isSelected ||*/ !IsPhysical; }
        }

        // An object is solid if it's not phantom and if it's not doing VolumeDetect
        public override bool IsSolid
        {
            get { return !IsPhantom && !_isVolumeDetect; }
        }

        // The object is moving and is actively being dynamic in the physical world
        public override bool IsPhysicallyActive
        {
            get { return !_isSelected && IsPhysical; }
        }

        // Make gravity work if the object is physical and not selected
        // Called at taint-time!!
        void SetObjectDynamic(bool forceRebuild)
        {
            // Recreate the physical object if necessary
            CreateGeomAndObject(forceRebuild);
        }

        // Convert the simulator's physical properties into settings on BulletSim objects.
        // There are four flags we're interested in:
        //     IsStatic: Object does not move, otherwise the object has mass and moves
        //     isSolid: other objects bounce off of this object
        //     isVolumeDetect: other objects pass through but can generate collisions
        //     collisionEvents: whether this object returns collision events
        public virtual void UpdatePhysicalParameters()
        {
            if (!PhysBody.HasPhysicalBody)
            {
                // This would only happen if updates are called for during initialization when the body is not set up yet.
                DetailLog("{0},BSPrim.UpdatePhysicalParameters,taint,calledWithNoPhysBody", LocalID);
                return;
            }

            // Mangling all the physical properties requires the object not be in the physical world.
            // This is a NOOP if the object is not in the world (BulletSim and Bullet ignore objects not found).
            PhysicsScene.PE.RemoveObjectFromWorld(PhysicsScene.World, PhysBody);

            // Set up the object physicalness (does gravity and collisions move this object)
            MakeDynamic(IsStatic);

            // Update vehicle specific parameters (after MakeDynamic() so can change physical parameters)
            PhysicalActors.Refresh();

            // Arrange for collision events if the simulator wants them
            EnableCollisions(SubscribedEvents());

            // Make solid or not (do things bounce off or pass through this object).
            MakeSolid(IsSolid);

            AddObjectToPhysicalWorld();

            //Force selection properties
            SelectObject(IsSelected);

            // Rebuild its shape
            PhysicsScene.PE.UpdateSingleAabb(PhysicsScene.World, PhysBody);

            DetailLog(
                "{0},BSPrim.UpdatePhysicalParameters,taintExit,static={1},solid={2},mass={3},collide={4},cf={5:X},cType={6},body={7},shape={8},pos={9}",
                LocalID, IsStatic, IsSolid, Mass, SubscribedEvents(), CurrentCollisionFlags, PhysBody.collisionType,
                PhysBody, PhysShape, PhysicsScene.PE.GetPosition(PhysBody));
        }

        // "Making dynamic" means changing to and from static.
        // When static, gravity does not effect the object and it is fixed in space.
        // When dynamic, the object can fall and be pushed by others.
        // This is independent of its 'solidness' which controls what passes through
        //    this object and what interacts with it.
        protected virtual void MakeDynamic(bool makeStatic)
        {
            if (makeStatic)
            {
                // Become a Bullet 'static' object type
                CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_STATIC_OBJECT);
                // Stop all movement
                ZeroMotion(true);

                // Set various physical properties so other object interact properly
                PhysicsScene.PE.SetFriction(PhysBody, Friction);
                PhysicsScene.PE.SetRestitution(PhysBody, Restitution);
                PhysicsScene.PE.SetContactProcessingThreshold(PhysBody, BSParam.ContactProcessingThreshold);

                // Mass is zero which disables a bunch of physics stuff in Bullet
                UpdatePhysicalMassProperties(0f, false);
                // Set collision detection parameters
                if (BSParam.CcdMotionThreshold > 0f)
                {
                    PhysicsScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
                    PhysicsScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
                }

                // The activation state is 'disabled' so Bullet will not try to act on it.
                // PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.DISABLE_SIMULATION);
                // Start it out sleeping and physical actions could wake it up.
                PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.ISLAND_SLEEPING);

                // This collides like a static object
                PhysBody.collisionType = CollisionType.Static;
            }
            else
            {
                // Not a Bullet static object
                CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody,
                    CollisionFlags.CF_STATIC_OBJECT);

                // Set various physical properties so other object interact properly
                PhysicsScene.PE.SetFriction(PhysBody, Friction);
                PhysicsScene.PE.SetRestitution(PhysBody, Restitution);
                // DetailLog("{0},BSPrim.MakeDynamic,frict={1},rest={2}", LocalID, Friction, Restitution);

                // per http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=3382
                // Since this can be called multiple times, only zero forces when becoming physical
                // PhysicsScene.PE.ClearAllForces(BSBody);

                // For good measure, make sure the transform is set through to the motion state
                ForcePosition = _position;
                ForceVelocity = RawVelocity;
                ForceRotationalVelocity = _rotationalVelocity;

                // A dynamic object has mass
                UpdatePhysicalMassProperties(RawMass, false);

                // Set collision detection parameters
                if (BSParam.CcdMotionThreshold > 0f)
                {
                    PhysicsScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
                    PhysicsScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
                }

                // Various values for simulation limits
                PhysicsScene.PE.SetDamping(PhysBody, BSParam.LinearDamping, BSParam.AngularDamping);
                PhysicsScene.PE.SetDeactivationTime(PhysBody, BSParam.DeactivationTime);
                PhysicsScene.PE.SetSleepingThresholds(PhysBody, BSParam.LinearSleepingThreshold,
                    BSParam.AngularSleepingThreshold);
                PhysicsScene.PE.SetContactProcessingThreshold(PhysBody, BSParam.ContactProcessingThreshold);

                // This collides like an object.
                PhysBody.collisionType = CollisionType.Dynamic;

                // Force activation of the object so Bullet will act on it.
                // Must do the ForceActivationState2() to overcome the DISABLE_SIMULATION from static objects.
                PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.ACTIVE_TAG);
            }
        }

        // "Making solid" means that other object will not pass through this object.
        // To make transparent, we create a Bullet ghost object.
        // Note: This expects to be called from the UpdatePhysicalParameters() routine as
        //     the functions after this one set up the state of a possibly newly created collision body.
        void MakeSolid(bool makeSolid)
        {
            CollisionObjectTypes bodyType = (CollisionObjectTypes)PhysicsScene.PE.GetBodyType(PhysBody);
            if (makeSolid)
            {
                // Verify the previous code created the correct shape for this type of thing.
                if ((bodyType & CollisionObjectTypes.CO_RIGID_BODY) == 0)
                {
                    MainConsole.Instance.ErrorFormat(
                        "{0} MakeSolid: physical body of wrong type for solidity. id={1}, type={2}", LogHeader, LocalID,
                        bodyType);
                }
                CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody,
                    CollisionFlags.CF_NO_CONTACT_RESPONSE);
            }
            else
            {
                if ((bodyType & CollisionObjectTypes.CO_GHOST_OBJECT) == 0)
                {
                    MainConsole.Instance.ErrorFormat(
                        "{0} MakeSolid: physical body of wrong type for non-solidness. id={1}, type={2}", LogHeader,
                        LocalID, bodyType);
                }
                CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody,
                    CollisionFlags.CF_NO_CONTACT_RESPONSE);

                // Change collision info from a static object to a ghosty collision object
                PhysBody.collisionType = CollisionType.VolumeDetect;
            }
        }

        // Turn on or off the flag controlling whether collision events are returned to the simulator.
        void EnableCollisions(bool wantsCollisionEvents)
        {
            if (wantsCollisionEvents)
            {
                CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody,
                    CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
            }
            else
            {
                CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody,
                    CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
            }
        }

        // Add me to the physical world.
        // Object MUST NOT already be in the world.
        // This routine exists because some assorted properties get mangled by adding to the world.
        internal void AddObjectToPhysicalWorld()
        {
            if (PhysBody.HasPhysicalBody)
            {
                PhysicsScene.PE.AddObjectToWorld(PhysicsScene.World, PhysBody);
            }
            else
            {
                MainConsole.Instance.ErrorFormat("{0} Attempt to add physical object without body. id={1}", LogHeader,
                    LocalID);
                DetailLog("{0},BSPrim.AddObjectToPhysicalWorld,addObjectWithoutBody,cType={1}", LocalID,
                    PhysBody.collisionType);
            }
        }

        // prims don't fly
        public override bool Flying
        {
            get { return _flying; }
            set { _flying = value; }
        }

        public override bool SetAlwaysRun
        {
            get { return _setAlwaysRun; }
            set { _setAlwaysRun = value; }
        }

        public override bool ThrottleUpdates
        {
            get { return _throttleUpdates; }
            set { _throttleUpdates = value; }
        }

        public bool IsPhantom
        {
            get
            {
                // SceneObjectPart removes phantom objects from the physics scene
                // so, although we could implement touching and such, we never
                // are invoked as a phantom object
                return false;
            }
        }

        public override bool FloatOnWater
        {
            set
            {
                _floatOnWater = value;
                PhysicsScene.TaintedObject(LocalID,"BSPrim.setFloatOnWater", delegate()
                {
                    if (_floatOnWater)
                        CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody,
                            CollisionFlags.BS_FLOATS_ON_WATER);
                    else
                        CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody,
                            CollisionFlags.BS_FLOATS_ON_WATER);
                });
            }
        }

        public override OMV.Vector3 RotationalVelocity
        {
            get { return _rotationalVelocity; }
            set
            {
                _rotationalVelocity = value;
                Util.ClampV(_rotationalVelocity, BSParam.MaxAngularVelocity);
                // MainConsole.Instance.DebugFormat("{0}: RotationalVelocity={1}", LogHeader, _rotationalVelocity);
                PhysicsScene.TaintedObject(LocalID,"BSPrim.setRotationalVelocity",
                    delegate() { ForceRotationalVelocity = _rotationalVelocity; });
            }
        }

        public override OMV.Vector3 ForceRotationalVelocity
        {
            get { return _rotationalVelocity; }
            set
            {
                _rotationalVelocity = Util.ClampV(value, BSParam.MaxAngularVelocity);
                if (PhysBody.HasPhysicalBody)
                {
                    DetailLog("{0},BSPrim.ForceRotationalVel,taint,rotvel={1}", LocalID, _rotationalVelocity);
                    PhysicsScene.PE.SetAngularVelocity(PhysBody, _rotationalVelocity);
                    // PhysicsScene.PE.SetInterpolationAngularVelocity(PhysBody, _rotationalVelocity);
                    ActivateIfPhysical(false);
                }
            }
        }

      public override bool Kinematic
      {
          get { return _kinematic; }
          set { _kinematic = value; }
      }

        public override float Buoyancy
        {
            get { return _buoyancy; }
            set {
                _buoyancy = value;
                PhysicsScene.TaintedObject(LocalID,"BSPrim.setBuoyancy", delegate() { ForceBuoyancy = _buoyancy; });
            }
        }

        public override float ForceBuoyancy
        {
            get { return _buoyancy; }
            set {
                _buoyancy = value;
                // DetailLog("{0},BSPrim.setForceBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                // Force the recalculation of the various inertia,etc variables in the object
                UpdatePhysicalMassProperties(RawMass, true);
                DetailLog("{0},BSPrim.ForceBuoyancy,buoy={1},mass={2},grav={3}", LocalID, _buoyancy, RawMass, Gravity);
                ActivateIfPhysical(false);
            }
        }

        /*
        // all the PID stuff appears to have been moved to SceneObjectPat..UpdateLookAt()
        public override bool PIDActive {
            get
            {
                return MoveToTargetActive;
            }
            set {
                base.MoveToTargetActive = value;
                EnableActor(MoveToTargetActive, MoveToTargetActorName, delegate()
                {
                     return new BSActorMoveToTarget(PhysicsScene, this, MoveToTargetActorName);
                });

                // Call update so actor Refresh() is called to start things off
                PhysicsScene.TaintedObject( "BSPrim.PIDActive", delegate()
                {
                    UpdatePhysicalParameters();
                });
             }
        }

        public override OMV.Vector3 PIDTarget
        {
            set
            {
                base.PIDTarget = value;
                BSActor actor;
                if (PhysicalActors.TryGetActor(MoveToTargetActorName, out actor))
                {
                    // if the actor exists, tell it to refresh its values.
                    actor.Refresh();
                }
                
            }
        }
        // Used for llSetHoverHeight and maybe vehicle height
        // Hover Height will override MoveTo target's Z
        public override bool PIDHoverActive {
            set {
                base.HoverActive = value;
                EnableActor(HoverActive, HoverActorName, delegate()
                {
                    return new BSActorHover(PhysicsScene, this, HoverActorName);
                });
                // Call update so actor Refresh() is called to start things off
                PhysicsScene.TaintedObject("BSPrim.PIDHoverActive", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
*/
        public override void AddForce(OMV.Vector3 force, bool pushforce)
        {
            // Per documentation, max force is limited.
            OMV.Vector3 addForce = Util.ClampV(force, BSParam.MaxAddForceMagnitude);

            // Since this force is being applied in only one step, make this a force per second.
            addForce /= PhysicsScene.LastTimeStep;
            AddForce(addForce, pushforce, false /* inTaintTime */);
        }

        // Applying a force just adds this to the total force on the object.
        // This added force will only last the next simulation tick.
        public void AddForce(OMV.Vector3 force, bool pushforce, bool inTaintTime)
        {
            // for an object, doesn't matter if force is a pushforce or not
            if (IsPhysicallyActive)
            {
                if (force.IsFinite())
                {
                    // DetailLog("{0},BSPrim.addForce,call,force={1}", LocalID, addForce);

                    OMV.Vector3 addForce = force;
                    PhysicsScene.TaintedObject(inTaintTime, "BSPrim.AddForce", delegate()
                    {
                        // Bullet adds this central force to the total force for this tick
                        DetailLog("{0},BSPrim.addForce,taint,force={1}", LocalID, addForce);
                        if (PhysBody.HasPhysicalBody)
                        {
                            PhysicsScene.PE.ApplyCentralForce(PhysBody, addForce);
                            ActivateIfPhysical(false);
                        }
                    });
                }
                else
                {
                    MainConsole.Instance.WarnFormat("{0}: AddForce: Got a NaN force applied to a prim. LocalID={1}",
                        LogHeader, LocalID);
                    return;
                }
            }
        }

        public void AddForceImpulse(OMV.Vector3 impulse, bool pushforce, bool inTaintTime)
        {
            // for an object, doesn't matter if force is a pushforce or not
            if (!IsPhysicallyActive)
            {
                if (impulse.IsFinite())
                {
                    OMV.Vector3 addImpulse = Util.ClampV(impulse, BSParam.MaxAddForceMagnitude);
                    // DetailLog("{0},BSPrim.addForceImpulse,call,impulse={1}", LocalID, impulse);

                    PhysicsScene.TaintedObject(inTaintTime, "BSPrim.AddImpulse", delegate()
                    {
                        // Bullet adds this impulse immediately to the velocity
                        DetailLog("{0},BSPrim.addForceImpulse,taint,impulseforce={1}", LocalID, addImpulse);
                        if (PhysBody.HasPhysicalBody)
                        {
                            PhysicsScene.PE.ApplyCentralImpulse(PhysBody, addImpulse);
                            ActivateIfPhysical(false);
                        }
                    });
                }
                else
                {
                    MainConsole.Instance.WarnFormat(
                        "{0}: AddForceImpulse: Got a NaN impulse applied to a prim. LocalID={1}", LogHeader, LocalID);
                    return;
                }
            }
        }

        // BSPhysObject.AddAngularForce()
        public override void AddAngularForce(OMV.Vector3 force, bool pushforce, bool inTaintTime)
        {
            if (force.IsFinite())
            {
                OMV.Vector3 angForce = force;
                PhysicsScene.TaintedObject(inTaintTime, "BSPrim.AddAngularForce", delegate()
                {
                    if (PhysBody.HasPhysicalBody)
                    {
                        DetailLog("{0},BSPrim.AddAngularForce,taint,angForce={1}", LocalID, angForce);
                        PhysicsScene.PE.ApplyTorque(PhysBody, angForce);
                        ActivateIfPhysical(false);
                    }
                });
            }
            else
            {
                MainConsole.Instance.WarnFormat("{0}: Got a NaN force applied to a prim. LocalID={1}", LogHeader,
                    LocalID);
                return;
            }
        }

        // A torque impulse.
        // ApplyTorqueImpulse adds torque directly to the angularVelocity.
        // AddAngularForce accumulates the force and applied it to the angular velocity all at once.
        // Computed as: angularVelocity += impulse * inertia;
        public void ApplyTorqueImpulse(OMV.Vector3 impulse, bool inTaintTime)
        {
            OMV.Vector3 applyImpulse = impulse;
            PhysicsScene.TaintedObject(inTaintTime, "BSPrim.ApplyTorqueImpulse", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                {
                    PhysicsScene.PE.ApplyTorqueImpulse(PhysBody, applyImpulse);
                    ActivateIfPhysical(false);
                }
            });
        }

        #region Mass Calculation

        float CalculateMass()
        {
            float volume = _size.X * _size.Y * _size.Z; // default
            float tmp;

            float returnMass = 0;
            float hollowAmount = (float)BaseShape.ProfileHollow * 2.0e-5f;
            float hollowVolume = hollowAmount * hollowAmount;

            switch (BaseShape.ProfileShape)
            {
                case ProfileShape.Square:
                    // default box

                    if (BaseShape.PathCurve == (byte)Extrusion.Straight)
                    {
                        if (hollowAmount > 0.0)
                        {
                            switch (BaseShape.HollowShape)
                            {
                                case HollowShape.Square:
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Circle:

                                    hollowVolume *= 0.78539816339f;
                                    break;

                                case HollowShape.Triangle:

                                    hollowVolume *= (0.5f * .5f);
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    else if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                    {
                        //a tube

                        volume *= 0.78539816339e-2f * (float)(200 - BaseShape.PathScaleX);
                        tmp = 1.0f - 2.0e-2f * (float)(200 - BaseShape.PathScaleY);
                        volume -= volume * (tmp * tmp);

                        if (hollowAmount > 0.0)
                        {
                            hollowVolume *= hollowAmount;

                            switch (BaseShape.HollowShape)
                            {
                                case HollowShape.Square:
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Circle:
                                    hollowVolume *= 0.78539816339f;
                                    
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= 0.5f * 0.5f;
                                    break;
                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    break;

                case ProfileShape.Circle:

                    if (BaseShape.PathCurve == (byte)Extrusion.Straight)
                    {
                        volume *= 0.78539816339f; // elipse base

                        if (hollowAmount > 0.0)
                        {
                            switch (BaseShape.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Circle:
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.5f * 2.5984480504799f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= .5f * 1.27323954473516f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    else if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.61685027506808491367715568749226e-2f * (float)(200 - BaseShape.PathScaleX);
                        tmp = 1.0f - .02f * (float)(200 - BaseShape.PathScaleY);
                        volume *= (1.0f - tmp * tmp);

                        if (hollowAmount > 0.0)
                        {
                            // calculate the hollow volume by it's shape compared to the prim shape
                            hollowVolume *= hollowAmount;

                            switch (BaseShape.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Circle:
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.5f * 2.5984480504799f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= .5f * 1.27323954473516f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

                case ProfileShape.HalfCircle:
                    if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.52359877559829887307710723054658f;
                    }
                    break;

                case ProfileShape.EquilateralTriangle:

                    if (BaseShape.PathCurve == (byte)Extrusion.Straight)
                    {
                        volume *= 0.32475953f;

                        if (hollowAmount > 0.0)
                        {
                            // calculate the hollow volume by it's shape compared to the prim shape
                            switch (BaseShape.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Triangle:
                                    hollowVolume *= .25f;
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.499849f * 3.07920140172638f;
                                    break;

                                case HollowShape.Circle:
                                    // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                    // Cyllinder hollow volume calculation

                                    hollowVolume *= 0.1963495f * 3.07920140172638f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    else if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.32475953f;
                        volume *= 0.01f * (float)(200 - BaseShape.PathScaleX);
                        tmp = 1.0f - .02f * (float)(200 - BaseShape.PathScaleY);
                        volume *= (1.0f - tmp * tmp);

                        if (hollowAmount > 0.0)
                        {
                            hollowVolume *= hollowAmount;

                            switch (BaseShape.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Triangle:
                                    hollowVolume *= .25f;
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.499849f * 3.07920140172638f;
                                    break;

                                case HollowShape.Circle:

                                    hollowVolume *= 0.1963495f * 3.07920140172638f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

                default:
                    break;
            }


            float taperX1;
            float taperY1;
            float taperX;
            float taperY;
            float pathBegin;
            float pathEnd;
            float profileBegin;
            float profileEnd;

            if (BaseShape.PathCurve == (byte)Extrusion.Straight || BaseShape.PathCurve == (byte)Extrusion.Flexible)
            {
                taperX1 = BaseShape.PathScaleX * 0.01f;
                if (taperX1 > 1.0f)
                    taperX1 = 2.0f - taperX1;
                taperX = 1.0f - taperX1;

                taperY1 = BaseShape.PathScaleY * 0.01f;
                if (taperY1 > 1.0f)
                    taperY1 = 2.0f - taperY1;
                taperY = 1.0f - taperY1;
            }
            else
            {
                taperX = BaseShape.PathTaperX * 0.01f;
                if (taperX < 0.0f)
                    taperX = -taperX;
                taperX1 = 1.0f - taperX;

                taperY = BaseShape.PathTaperY * 0.01f;
                if (taperY < 0.0f)
                    taperY = -taperY;
                taperY1 = 1.0f - taperY;
            }


            volume *= (taperX1 * taperY1 + 0.5f * (taperX1 * taperY + taperX * taperY1) + 0.3333333333f * taperX * taperY);

            pathBegin = (float)BaseShape.PathBegin * 2.0e-5f;
            pathEnd = 1.0f - (float)BaseShape.PathEnd * 2.0e-5f;
            volume *= (pathEnd - pathBegin);

            // this is crude aproximation
            profileBegin = (float)BaseShape.ProfileBegin * 2.0e-5f;
            profileEnd = 1.0f - (float)BaseShape.ProfileEnd * 2.0e-5f;
            volume *= (profileEnd - profileBegin);

            returnMass = Density * BSParam.DensityScaleFactor * volume;

            returnMass = Util.Clamp(returnMass, BSParam.MinimumObjectMass, BSParam.MaximumObjectMass);
            // DetailLog("{0},BSPrim.CalculateMass,den={1},vol={2},mass={3}", LocalID, Density, volume, returnMass);

            return returnMass;
        } // end CalculateMass

        #endregion Mass Calculation

        // Rebuild the geometry and object.
        // This is called when the shape changes so we need to recreate the mesh/hull.
        // Called at taint-time!!!
        public void CreateGeomAndObject(bool forceRebuild)
        {
            // Create the correct physical representation for this type of object.
            // Updates base.PhysBody and base.PhysShape with the new information.
            // Ignore 'forceRebuild'. 'GetBodyAndShape' makes the right choices and changes of necessary.
            PhysicsScene.Shapes.GetBodyAndShape(false /*forceRebuild */, PhysicsScene.World, this, delegate(BulletBody pBody, BulletShape pShape)
            {
                // Called if the current prim body is about to be destroyed.
                // Remove all the physical dependencies on the old body.
                // (Maybe someday make the changing of BSShape an event to be subscribed to by BSLinkset, ...)
                RemoveBodyDependencies();
            });

            // Make sure the properties are set on the new object
            UpdatePhysicalParameters();
            return;
        }

        // Called at taint-time
        protected virtual void RemoveBodyDependencies()
        {
            PhysicalActors.RemoveBodyDependencies();
        }

        #region Extension
        public override object Extension(string pFunct, params object[] pParams)
        {
            DetailLog("{0} BSPrim.Extension,op={1}", LocalID, pFunct);
            object ret;
            switch (pFunct)
            {
                case ExtendedPhysics.PhysFunctAxisLockLimits:
                    ret = SetAxisLockLimitsExtension(pParams);
                    break;
                default:
                    ret = base.Extension(pFunct, pParams);
                    break;
            }
            return ret;
        }

        void InitializeAxisActor()
        {
            EnableActor(LockedAngularAxis != LockedAxisFree || LockedLinearAxis != LockedAxisFree,
                                        LockedAxisActorName, delegate()
            {
                return new BSActorLockAxis(PhysicsScene, this, LockedAxisActorName);
            });

            // Update parameters so the new actor's Refresh() action is called at the right time.
            PhysicsScene.TaintedObject(LocalID, "BSPrim.LockAxis", delegate()
            {
                UpdatePhysicalParameters();
            });
        }

        // Passed an array of an array of parameters, set the axis locking.
        // This expects an int (PHYS_AXIS_*) followed by none or two limit floats
        //    followed by another int and floats, etc.
        object SetAxisLockLimitsExtension(object[] pParams)
        {
            DetailLog("{0} SetAxisLockLimitsExtension. parmlen={1}", LocalID, pParams.GetLength(0));
            object ret = null;
            try
            {
                if (pParams.GetLength(0) > 1)
                {
                    int index = 2;
                    while (index < pParams.GetLength(0))
                    {
                        var funct = pParams[index];
                        DetailLog("{0} SetAxisLockLimitsExtension. op={1}, index={2}", LocalID, funct, index);
                        if (funct is Int32 || funct is Int64)
                        {
                            switch ((int)funct)
                            {
                                // Those that take no parameters
                                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR:
                                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_X:
                                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Y:
                                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Z:
                                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR:
                                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_X:
                                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Y:
                                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Z:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_X:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Y:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Z:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_X:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Y:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Z:
                                case ExtendedPhysics.PHYS_AXIS_UNLOCK:
                                    ApplyAxisLimits((int)funct, 0f, 0f);
                                    index += 1;
                                    break;
                                // Those that take two parameters (the limits)
                                case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_X:
                                case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Y:
                                case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Z:
                                case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_X:
                                case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Y:
                                case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Z:
                                    ApplyAxisLimits((int)funct, (float)pParams[index + 1], (float)pParams[index + 2]);
                                    index += 3;
                                    break;
                                default:
                                    MainConsole.Instance.WarnFormat("{0} SetSxisLockLimitsExtension. Unknown op={1}", LogHeader, funct);
                                    index += 1;
                                    break;
                            }
                        }
                    }
                    InitializeAxisActor();
                    ret = (object)index;
                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.WarnFormat("{0} SetSxisLockLimitsExtension exception in object {1}: {2}", LogHeader, Name, e);
                ret = null;
            }
            return ret;    // not implemented yet
        }

            // Set the locking parameters.
            // If an axis is locked, the limits for the axis are set to zero,
            // If the axis is being constrained, the high and low value are passed and set.
            // When done here, LockedXXXAxis flags are set and LockedXXXAxixLow/High are set to the range.
        protected void ApplyAxisLimits(int funct, float low, float high)
        {
            DetailLog("{0} ApplyAxisLimits. op={1}, low={2}, high={3}", LocalID, funct, low, high);
            float linearMax = 23000f;
            float angularMax = (float)Math.PI;

            switch (funct)
            {
                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR:
                    LockedLinearAxis = new OMV.Vector3(LockedAxis, LockedAxis, LockedAxis);
                    LockedLinearAxisLow = OMV.Vector3.Zero;
                    LockedLinearAxisHigh = OMV.Vector3.Zero;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_X:
                    LockedLinearAxis.X = LockedAxis;
                    LockedLinearAxisLow.X = 0f;
                    LockedLinearAxisHigh.X = 0f;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_X:
                    LockedLinearAxis.X = LockedAxis;
                    LockedLinearAxisLow.X = Util.Clip(low, -linearMax, linearMax);
                    LockedLinearAxisHigh.X = Util.Clip(high, -linearMax, linearMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Y:
                    LockedLinearAxis.Y = LockedAxis;
                    LockedLinearAxisLow.Y = 0f;
                    LockedLinearAxisHigh.Y = 0f;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Y:
                    LockedLinearAxis.Y = LockedAxis;
                    LockedLinearAxisLow.Y = Util.Clip(low, -linearMax, linearMax);
                    LockedLinearAxisHigh.Y = Util.Clip(high, -linearMax, linearMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Z:
                    LockedLinearAxis.Z = LockedAxis;
                    LockedLinearAxisLow.Z = 0f;
                    LockedLinearAxisHigh.Z = 0f;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Z:
                    LockedLinearAxis.Z = LockedAxis;
                    LockedLinearAxisLow.Z = Util.Clip(low, -linearMax, linearMax);
                    LockedLinearAxisHigh.Z = Util.Clip(high, -linearMax, linearMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR:
                    LockedAngularAxis = new OMV.Vector3(LockedAxis, LockedAxis, LockedAxis);
                    LockedAngularAxisLow = OMV.Vector3.Zero;
                    LockedAngularAxisHigh = OMV.Vector3.Zero;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_X:
                    LockedAngularAxis.X = LockedAxis;
                    LockedAngularAxisLow.X = 0;
                    LockedAngularAxisHigh.X = 0;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_X:
                    LockedAngularAxis.X = LockedAxis;
                    LockedAngularAxisLow.X = Util.Clip(low, -angularMax, angularMax);
                    LockedAngularAxisHigh.X = Util.Clip(high, -angularMax, angularMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Y:
                    LockedAngularAxis.Y = LockedAxis;
                    LockedAngularAxisLow.Y = 0;
                    LockedAngularAxisHigh.Y = 0;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Y:
                    LockedAngularAxis.Y = LockedAxis;
                    LockedAngularAxisLow.Y = Util.Clip(low, -angularMax, angularMax);
                    LockedAngularAxisHigh.Y = Util.Clip(high, -angularMax, angularMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Z:
                    LockedAngularAxis.Z = LockedAxis;
                    LockedAngularAxisLow.Z = 0;
                    LockedAngularAxisHigh.Z = 0;
                    break;
                case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Z:
                    LockedAngularAxis.Z = LockedAxis;
                    LockedAngularAxisLow.Z = Util.Clip(low, -angularMax, angularMax);
                    LockedAngularAxisHigh.Z = Util.Clip(high, -angularMax, angularMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR:
                    LockedLinearAxis = LockedAxisFree;
                    LockedLinearAxisLow = new OMV.Vector3(-linearMax, -linearMax, -linearMax);
                    LockedLinearAxisHigh = new OMV.Vector3(linearMax, linearMax, linearMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_X:
                    LockedLinearAxis.X = FreeAxis;
                    LockedLinearAxisLow.X = -linearMax;
                    LockedLinearAxisHigh.X = linearMax;
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Y:
                    LockedLinearAxis.Y = FreeAxis;
                    LockedLinearAxisLow.Y = -linearMax;
                    LockedLinearAxisHigh.Y = linearMax;
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Z:
                    LockedLinearAxis.Z = FreeAxis;
                    LockedLinearAxisLow.Z = -linearMax;
                    LockedLinearAxisHigh.Z = linearMax;
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR:
                    LockedAngularAxis = LockedAxisFree;
                    LockedAngularAxisLow = new OMV.Vector3(-angularMax, -angularMax, -angularMax);
                    LockedAngularAxisHigh = new OMV.Vector3(angularMax, angularMax, angularMax);
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_X:
                    LockedAngularAxis.X = FreeAxis;
                    LockedAngularAxisLow.X = -angularMax;
                    LockedAngularAxisHigh.X = angularMax;
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Y:
                    LockedAngularAxis.Y = FreeAxis;
                    LockedAngularAxisLow.Y = -angularMax;
                    LockedAngularAxisHigh.Y = angularMax;
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Z:
                    LockedAngularAxis.Z = FreeAxis;
                    LockedAngularAxisLow.Z = -angularMax;
                    LockedAngularAxisHigh.Z = angularMax;
                    break;
                case ExtendedPhysics.PHYS_AXIS_UNLOCK:
                    ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR, 0f, 0f);
                    ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR, 0f, 0f);
                    break;
                default:
                    break;
            }
            return;
        }
        #endregion  // Extension

        // The physics engine says that properties have updated. Update same and inform
        // the world that things have changed.
        // NOTE: BSPrim.UpdateProperties is overloaded by BSPrimLinkable which modifies updates from root and children prims.
        // NOTE: BSPrim.UpdateProperties is overloaded by BSPrimDisplaced which handles mapping physical position to simulator position.
        public override void UpdateProperties(EntityProperties entprop)
        {
            // Let anyone (like the actors) modify the updated properties before they are pushed into the object and the simulator.
            TriggerPreUpdatePropertyAction(ref entprop);

            // DetailLog("{0},BSPrim.UpdateProperties,entry,entprop={1}", LocalID, entprop);   // DEBUG DEBUG

            // Assign directly to the local variables so the normal set actions do not happen
            _position = entprop.Position;
            _orientation = entprop.Rotation;

            bool terseUpdate = false;

            if (entprop.Velocity != OMV.Vector3.Zero && entprop.Velocity.ApproxEquals(OMV.Vector3.Zero, 0.01f) &&
                Velocity != OMV.Vector3.Zero)
            {
                entprop.Velocity = OMV.Vector3.Zero;
                entprop.Acceleration = OMV.Vector3.Zero;
                entprop.RotationalVelocity = OMV.Vector3.Zero;
                Velocity = RawVelocity = OMV.Vector3.Zero;
                ZeroMotion(true);
                terseUpdate = true;
            }

            // DEBUG DEBUG DEBUG -- smooth velocity changes a bit. The simulator seems to be
            //    very sensitive to velocity changes.
            if (entprop.Velocity == OMV.Vector3.Zero ||
                (VehicleType != 0 /*&& !entprop.Velocity.ApproxEquals(RawVelocity, 0.01f)*/) ||
                !entprop.Velocity.ApproxEquals(RawVelocity, BSParam.UpdateVelocityChangeThreshold / 2f))
            {
                terseUpdate = true;
                RawVelocity = entprop.Velocity;
            }
            _acceleration = entprop.Acceleration;
            _rotationalVelocity = entprop.RotationalVelocity;

            // DetailLog("{0},BSPrim.UpdateProperties,afterAssign,entprop={1}", LocalID, entprop);   // DEBUG DEBUG

            // The sanity check can change the velocity and/or position.
            if (PositionSanityCheck(true /* inTaintTime */))
            {
                entprop.Position = _position;
                entprop.Velocity = RawVelocity;
                entprop.RotationalVelocity = _rotationalVelocity;
                entprop.Acceleration = _acceleration;
            }

            // 20131224 not used        OMV.Vector3 direction = OMV.Vector3.UnitX * _orientation;   // DEBUG DEBUG DEBUG
            //DetailLog("{0},BSPrim.UpdateProperties,call,entProp={1},dir={2}", LocalID, entprop, direction);

            // remember the current and last set values
            LastEntityProperties = CurrentEntityProperties;
            CurrentEntityProperties = entprop;

            if (terseUpdate)
                RequestPhysicsterseUpdate ();
            /*
            else
            {
                // For debugging, report the movement of children
                DetailLog("{0},BSPrim.UpdateProperties,child,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                        LocalID, entprop.Position, entprop.Rotation, entprop.Velocity,
                        entprop.Acceleration, entprop.RotationalVelocity);
            }
            */
         }


        // High performance detailed logging routine used by the physical objects.
        protected new void DetailLog(string msg, params Object[] args)
        {
            // => new integration of logging here! <=
            //if (PhysicsScene.PhysicsLogging.Enabled)
            // commented out by fine (spam at the console)
            // PhysicsScene.DetailLog(msg, args);
            // commented out by fine (spam at the console)
            // WhiteCore.Framework.ConsoleFramework.MainConsole.Instance.InfoFormat("[BulletPrim]: " + msg, args);
        }
    }
}
