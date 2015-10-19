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

/* Revised Aug, Sept 2009 by Kitto Flora. ODEDynamics.cs replaces
 * ODEVehicleSettings.cs. It and ODEPrim.cs are re-organised:
 * ODEPrim.cs contains methods dealing with Prim editing, Prim
 * characteristics and Kinetic motion.
 * ODEDynamics.cs contains methods dealing with Prim Physical motion
 * (dynamics) and the associated settings. Old Linear and angular
 * motors for dynamic motion have been replace with  MoveLinear()
 * and MoveAngular(); 'Physical' is used only to switch ODE dynamic
 * simualtion on/off; VEHICAL_TYPE_NONE/VEHICAL_TYPE_<other> is to
 * switch between 'VEHICLE' parameter use and general dynamics
 * settings use.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using WhiteCore.Framework.Physics;
using OpenMetaverse;

//using Ode.NET;

namespace WhiteCore.Physics.OpenDynamicsEngine
{
    public class WhiteCoreODEDynamics
    {
        public float Mass;
        private int frcount; // Used to limit dynamics debug output to
        // every 100th frame

        // private OdeScene m_parentScene = null;
        private Vector3 m_BlockingEndPoint = Vector3.Zero;
        private Quaternion m_RollreferenceFrame = Quaternion.Identity;
        private float m_VehicleBuoyancy; //KF: m_VehicleBuoyancy is set by VEHICLE_BUOYANCY for a vehicle.
        private float m_VhoverEfficiency;
        private float m_VhoverHeight;
        private float m_VhoverTargetHeight = -1.0f; // if <0 then no hover, else its the current target height
        private float m_VhoverTimescale;
        // Linear properties
        //       private Vector3 m_lastVertAttractor = Vector3.Zero;             // what VA was last applied to body

        //Deflection properties
        private float m_angularDeflectionEfficiency;
        private float m_angularDeflectionTimescale;
        private Vector3 m_angularFrictionTimescale = Vector3.Zero; // body angular velocity  decay rate

        private float m_angularMotorDecayTimescale; // motor angular velocity decay rate
        private Vector3 m_angularMotorDirection = Vector3.Zero; // angular velocity requested by LSL motor
        private float m_angularMotorTimescale; // motor angular velocity ramp up rate
        private Vector3 m_angularMotorVelocity = Vector3.Zero; // current angular motor velocity


        //Banking properties
        private float m_bankingEfficiency;
        private float m_bankingMix;
        private float m_bankingTimescale;
        private IntPtr m_body = IntPtr.Zero;
        private bool m_enabled;
        private VehicleFlag m_flags = 0; // Boolean settings:
        private List<Vector3> m_forcelist = new List<Vector3>();
        private Vector3 m_lastAngVelocity = Vector3.Zero;
        private Vector3 m_lastAngularVelocity = Vector3.Zero; // what was last applied to body
        private Vector3 m_lastLinearVelocityVector = Vector3.Zero;
        private Vector3 m_lastPositionVector = Vector3.Zero;
        private Vector3 m_lastVelocity = Vector3.Zero;
        private Vector3 m_lastposChange = Vector3.Zero;
        private float m_linearDeflectionEfficiency;
        private float m_linearDeflectionTimescale;
        private Vector3 m_linearFrictionTimescale = Vector3.Zero;
        private float m_linearMotorDecayTimescale;
        private Vector3 m_linearMotorDirection = Vector3.Zero; // velocity requested by LSL, decayed by time
        private Vector3 m_linearMotorDirectionLASTSET = Vector3.Zero; // velocity requested by LSL
        private Vector3 m_linearMotorOffset = Vector3.Zero;
        private float m_linearMotorTimescale;
        //private bool m_linearZeroFlag;
        private Vector3 m_newVelocity = Vector3.Zero; // velocity applied to body
        private Quaternion m_referenceFrame = Quaternion.Identity; // Axis modifier
        private Vehicle m_type = Vehicle.TYPE_NONE; // If a 'VEHICLE', and what kind
        private Quaternion m_userLookAt = Quaternion.Identity;

        //Hover and Buoyancy properties
        // Modifies gravity. Slider between -1 (double-gravity) and 1 (full anti-gravity)
        // KF: So far I have found no good method to combine a script-requested .Z velocity and gravity.
        // Therefore only m_VehicleBuoyancy=1 (0g) will use the script-requested .Z velocity.

        //Attractor properties
        private float m_verticalAttractionEfficiency = 1.0f; // damped
        private float m_verticalAttractionTimescale = 500f; // Timescale > 300  means no vert attractor.


        public Vehicle Type
        {
            get { return m_type; }
        }

        public IntPtr Body
        {
            get { return m_body; }
        }

        internal void ProcessFloatVehicleParam(Vehicle pParam, float pValue, float timestep)
        {
            switch (pParam)
            {
                case Vehicle.ANGULAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_angularDeflectionEfficiency = pValue;
                    break;
                case Vehicle.ANGULAR_DEFLECTION_TIMESCALE:
                    if (pValue <= timestep)
                        m_angularDeflectionTimescale = timestep;
                    else
                        m_angularDeflectionTimescale = pValue/timestep;
                    break;
                case Vehicle.ANGULAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue <= timestep)
                        m_angularMotorDecayTimescale = timestep;
                    else
                        m_angularMotorDecayTimescale = pValue/timestep;
                    break;
                case Vehicle.ANGULAR_MOTOR_TIMESCALE:
                    if (pValue <= timestep)
                        m_angularMotorTimescale = timestep;
                    else
                        m_angularMotorTimescale = pValue/timestep;
                    break;
                case Vehicle.BANKING_EFFICIENCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_bankingEfficiency = pValue;
                    break;
                case Vehicle.BANKING_MIX:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_bankingMix = pValue;
                    break;
                case Vehicle.BANKING_TIMESCALE:
                    if (pValue <= timestep)
                        m_bankingTimescale = timestep;
                    else
                        m_bankingTimescale = pValue/timestep;
                    break;
                case Vehicle.BUOYANCY:
                    if (pValue < -1f) pValue = -1f;
                    if (pValue > 1f) pValue = 1f;
                    m_VehicleBuoyancy = pValue;
                    break;
                case Vehicle.HOVER_EFFICIENCY:
                    if (pValue < 0f) pValue = 0f;
                    if (pValue > 1f) pValue = 1f;
                    m_VhoverEfficiency = pValue;
                    break;
                case Vehicle.HOVER_HEIGHT:
                    m_VhoverHeight = pValue;
                    break;
                case Vehicle.HOVER_TIMESCALE:
                    if (pValue <= timestep)
                        m_VhoverTimescale = timestep;
                    else
                        m_VhoverTimescale = pValue/timestep;
                    break;
                case Vehicle.LINEAR_DEFLECTION_EFFICIENCY:
                    if (pValue < 0.01f) pValue = 0.01f;
                    m_linearDeflectionEfficiency = pValue;
                    break;
                case Vehicle.LINEAR_DEFLECTION_TIMESCALE:
                    if (pValue <= timestep)
                        m_linearDeflectionTimescale = timestep;
                    else
                        m_linearDeflectionTimescale = pValue/timestep;
                    break;
                case Vehicle.LINEAR_MOTOR_DECAY_TIMESCALE:
                    if (pValue > 120)
                        m_linearMotorDecayTimescale = 120/timestep;
                    else if (pValue <= timestep)
                        m_linearMotorDecayTimescale = timestep;
                    else
                        m_linearMotorDecayTimescale = pValue/timestep;
                    break;
                case Vehicle.LINEAR_MOTOR_TIMESCALE:
                    if (pValue <= timestep)
                        m_linearMotorTimescale = timestep;
                    else
                        m_linearMotorTimescale = pValue/timestep;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_EFFICIENCY:
                    if (pValue < 0.1f) pValue = 0.1f; // Less goes unstable
                    if (pValue > 1.0f) pValue = 1.0f;
                    m_verticalAttractionEfficiency = pValue;
                    break;
                case Vehicle.VERTICAL_ATTRACTION_TIMESCALE:
                    if (pValue <= timestep)
                        m_verticalAttractionTimescale = timestep;
                    else
                        m_verticalAttractionTimescale = pValue/timestep;
                    break;

                    // These are vector properties but the engine lets you use a single float value to
                    // set all of the components to the same value
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue <= timestep)
                        pValue = timestep;
                    m_angularFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_angularMotorApply = 100;
                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue <= timestep)
                        pValue = timestep;
                    m_linearFrictionTimescale = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = new Vector3(pValue, pValue, pValue);
                    m_linearMotorDirectionLASTSET = new Vector3(pValue, pValue, pValue);
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue, pValue, pValue);
                    break;
            }
        }

//end ProcessFloatVehicleParam

        //All parts hooked up
        internal void ProcessVectorVehicleParam(Vehicle pParam, Vector3 pValue, float timestep)
        {
            switch (pParam)
            {
                case Vehicle.ANGULAR_FRICTION_TIMESCALE:
                    if (pValue.X < timestep)
                        pValue.X = timestep;
                    if (pValue.Y < timestep)
                        pValue.Y = timestep;
                    if (pValue.Z < timestep)
                        pValue.Z = timestep;
                    m_angularFrictionTimescale = new Vector3(pValue.X/timestep, pValue.Y/timestep, pValue.Z/timestep);
                    break;
                case Vehicle.ANGULAR_MOTOR_DIRECTION:
                    m_angularMotorDirection = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    // Limit requested angular speed to 2 rps= 4 pi rads/sec
                    if (m_angularMotorDirection.X > 12.56f) m_angularMotorDirection.X = 12.56f;
                    else if (m_angularMotorDirection.X < -12.56f) m_angularMotorDirection.X = -12.56f;
                    if (m_angularMotorDirection.Y > 12.56f) m_angularMotorDirection.Y = 12.56f;
                    else if (m_angularMotorDirection.Y < -12.56f) m_angularMotorDirection.Y = -12.56f;
                    if (m_angularMotorDirection.Z > 12.56f) m_angularMotorDirection.Z = 12.56f;
                    else if (m_angularMotorDirection.Z < -12.56f) m_angularMotorDirection.Z = -12.56f;

                    break;
                case Vehicle.LINEAR_FRICTION_TIMESCALE:
                    if (pValue.X < timestep)
                        pValue.X = timestep;
                    if (pValue.Y < timestep)
                        pValue.Y = timestep;
                    if (pValue.Z < timestep)
                        pValue.Z = timestep;
                    m_linearFrictionTimescale = new Vector3(pValue.X/timestep, pValue.Y/timestep, pValue.Z/timestep);
                    break;
                case Vehicle.LINEAR_MOTOR_DIRECTION:
                    m_linearMotorDirection = pValue;
                    m_linearMotorDirectionLASTSET = m_linearMotorDirection;
                    break;
                case Vehicle.LINEAR_MOTOR_OFFSET:
                    m_linearMotorOffset = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
                case Vehicle.BLOCK_EXIT:
                    m_BlockingEndPoint = new Vector3(pValue.X, pValue.Y, pValue.Z);
                    break;
            }
        }

//end ProcessVectorVehicleParam

        //All parts hooked up
        internal void ProcessRotationVehicleParam(Vehicle pParam, Quaternion pValue)
        {
            switch (pParam)
            {
                case Vehicle.REFERENCE_FRAME:
                    m_referenceFrame = pValue;
                    break;
                case Vehicle.ROLL_FRAME:
                    m_RollreferenceFrame = pValue;
                    break;
            }
        }

//end ProcessRotationVehicleParam

        internal void ProcessVehicleFlags(int pParam, bool remove)
        {
            VehicleFlag param = (VehicleFlag) pParam;
            if (remove)
            {
                if (pParam == -1)
                    m_flags = 0;
                else
                    m_flags &= ~param;
            }
            else if (pParam == -1)
                m_flags = 0;
            else
                m_flags |= param;
        }

//end ProcessVehicleFlags

        internal void ProcessTypeChange(WhiteCoreODEPrim parent, Vehicle pType, float timestep)
        {
            // Set Defaults For Type
            m_type = pType;
            switch (pType)
            {
                case Vehicle.TYPE_NONE:
                    m_linearFrictionTimescale = new Vector3(1000/timestep, 1000/timestep, 1000/timestep);
                    m_angularFrictionTimescale = new Vector3(1000/timestep, 1000/timestep, 1000/timestep);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1000/timestep;
                    m_linearMotorDecayTimescale = 1000/timestep;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1000/timestep;
                    m_angularMotorDecayTimescale = 120/timestep;
                    m_VhoverHeight = 0;
                    m_VhoverTimescale = 1000/timestep;
                    m_VehicleBuoyancy = 0;

                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1/timestep;

                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 1000/timestep;

                    m_bankingEfficiency = 0;
                    m_bankingMix = 1;
                    m_bankingTimescale = 1000/timestep;

                    m_flags = 0;
                    m_referenceFrame = Quaternion.Identity;
                    break;

                case Vehicle.TYPE_SLED:
                    m_linearFrictionTimescale = new Vector3(30/timestep, 1/timestep, 1000/timestep);

                    m_angularFrictionTimescale = new Vector3(1000/timestep, 1000/timestep, 1000/timestep);

                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1000/timestep;
                    m_linearMotorDecayTimescale = 120/timestep;

                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1000/timestep;
                    m_angularMotorDecayTimescale = 120/timestep;

                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 10;
                    m_VhoverTimescale = 10/timestep;
                    m_VehicleBuoyancy = 0;

                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 1/timestep;

                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 1000/timestep;

                    m_bankingEfficiency = 0;
                    m_bankingMix = 1;
                    m_bankingTimescale = 10/timestep;

                    m_referenceFrame = Quaternion.Identity;
                    m_flags &=
                        ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                          VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.LIMIT_MOTOR_UP);
                    break;
                case Vehicle.TYPE_CAR:
                    m_linearFrictionTimescale = new Vector3(100/timestep, 2/timestep, 1000/timestep);
                    m_angularFrictionTimescale = new Vector3(1000/timestep, 1000/timestep, 1000/timestep);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 1/timestep;
                    m_linearMotorDecayTimescale = 60/timestep;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 1/timestep;
                    m_angularMotorDecayTimescale = 0.8f/timestep;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0;
                    m_VhoverTimescale = 1000/timestep;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 1;
                    m_linearDeflectionTimescale = 2/timestep;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 10/timestep;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 10f/timestep;
                    m_bankingEfficiency = -0.2f;
                    m_bankingMix = 1;
                    m_bankingTimescale = 1/timestep;
                    m_referenceFrame = Quaternion.Identity;
                    m_flags &=
                        ~(VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                          VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_ROLL_ONLY |
                                VehicleFlag.LIMIT_MOTOR_UP | VehicleFlag.HOVER_UP_ONLY);
                    break;
                case Vehicle.TYPE_BOAT:
                    m_linearFrictionTimescale = new Vector3(10/timestep, 3/timestep, 2/timestep);
                    m_angularFrictionTimescale = new Vector3(10/timestep, 10/timestep, 10/timestep);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5/timestep;
                    m_linearMotorDecayTimescale = 60/timestep;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4/timestep;
                    m_angularMotorDecayTimescale = 4/timestep;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 2/timestep;
                    m_VehicleBuoyancy = 1;
                    m_linearDeflectionEfficiency = 0.5f;
                    m_linearDeflectionTimescale = 3/timestep;
                    m_angularDeflectionEfficiency = 0.5f;
                    m_angularDeflectionTimescale = 5/timestep;
                    m_verticalAttractionEfficiency = 0.5f;
                    m_verticalAttractionTimescale = 5f/timestep;
                    m_bankingEfficiency = -0.3f;
                    m_bankingMix = 0.8f;
                    m_bankingTimescale = 1/timestep;
                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                                 VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.NO_DEFLECTION_UP |
                                VehicleFlag.LIMIT_MOTOR_UP |
                                VehicleFlag.HOVER_WATER_ONLY);
                    break;
                case Vehicle.TYPE_AIRPLANE:
                    m_linearFrictionTimescale = new Vector3(200/timestep, 10/timestep, 5/timestep);
                    m_angularFrictionTimescale = new Vector3(20/timestep, 20/timestep, 20/timestep);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 2/timestep;
                    m_linearMotorDecayTimescale = 60/timestep;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 4/timestep;
                    m_angularMotorDecayTimescale = 4/timestep;
                    m_VhoverHeight = 0;
                    m_VhoverEfficiency = 0.5f;
                    m_VhoverTimescale = 1000/timestep;
                    m_VehicleBuoyancy = 0;
                    m_linearDeflectionEfficiency = 0.5f;
                    m_linearDeflectionTimescale = 3/timestep;
                    m_angularDeflectionEfficiency = 1;
                    m_angularDeflectionTimescale = 2/timestep;
                    m_verticalAttractionEfficiency = 0.9f;
                    m_verticalAttractionTimescale = 2f/timestep;
                    m_bankingEfficiency = 1;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 2;
                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP |
                                 VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                                 VehicleFlag.HOVER_GLOBAL_HEIGHT | VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY);
                    break;
                case Vehicle.TYPE_BALLOON:
                    m_linearFrictionTimescale = new Vector3(5/timestep, 5/timestep, 5/timestep);
                    m_angularFrictionTimescale = new Vector3(10/timestep, 10/timestep, 10/timestep);
                    m_linearMotorDirection = Vector3.Zero;
                    m_linearMotorTimescale = 5/timestep;
                    m_linearMotorDecayTimescale = 60/timestep;
                    m_angularMotorDirection = Vector3.Zero;
                    m_angularMotorTimescale = 6/timestep;
                    m_angularMotorDecayTimescale = 10/timestep;
                    m_VhoverHeight = 5;
                    m_VhoverEfficiency = 0.8f;
                    m_VhoverTimescale = 10/timestep;
                    m_VehicleBuoyancy = 1;
                    m_linearDeflectionEfficiency = 0;
                    m_linearDeflectionTimescale = 5/timestep;
                    m_angularDeflectionEfficiency = 0;
                    m_angularDeflectionTimescale = 5/timestep;
                    m_verticalAttractionEfficiency = 1f;
                    m_verticalAttractionTimescale = 100f/timestep;
                    m_bankingEfficiency = 0;
                    m_bankingMix = 0.7f;
                    m_bankingTimescale = 5/timestep;
                    m_referenceFrame = Quaternion.Identity;
                    m_flags &= ~(VehicleFlag.NO_DEFLECTION_UP | VehicleFlag.LIMIT_MOTOR_UP |
                                 VehicleFlag.HOVER_WATER_ONLY | VehicleFlag.HOVER_TERRAIN_ONLY |
                                 VehicleFlag.HOVER_UP_ONLY);
                    m_flags |= (VehicleFlag.LIMIT_ROLL_ONLY | VehicleFlag.HOVER_GLOBAL_HEIGHT);
                    break;
            }
        }

//end SetDefaultsForType

        internal void Enable(IntPtr pBody, WhiteCoreODEPrim parent, WhiteCoreODEPhysicsScene pParentScene)
        {
            if (m_enabled)
                return;
            if (pBody == IntPtr.Zero || m_type == Vehicle.TYPE_NONE)
                return;
            m_body = pBody;
            d.BodySetGravityMode(Body, true);
            m_enabled = true;
            m_lastLinearVelocityVector = parent.Velocity;
            m_lastPositionVector = parent.Position;
            m_lastAngularVelocity = parent.RotationalVelocity;
            parent.ThrottleUpdates = false;
            GetMass(pBody);
        }

        internal void Disable(WhiteCoreODEPrim parent)
        {
            if (!m_enabled || m_type == Vehicle.TYPE_NONE)
                return;
            m_enabled = false;
            parent.ThrottleUpdates = true;
            parent.ForceSetVelocity(Vector3.Zero);
            parent.ForceSetRotVelocity(Vector3.Zero);
            parent.ForceSetPosition(parent.Position);
            d.BodySetGravityMode(Body, false);
            m_body = IntPtr.Zero;
            m_linearMotorDirection = Vector3.Zero;
            m_linearMotorDirectionLASTSET = Vector3.Zero;
            m_angularMotorDirection = Vector3.Zero;
        }

        internal void GetMass(IntPtr pBody)
        {
            d.Mass mass;
            d.BodyGetMass(pBody, out mass);

            Mass = mass.mass;
        }


        internal void Step(IntPtr pBody, float pTimestep, WhiteCoreODEPhysicsScene pParentScene, WhiteCoreODEPrim parent)
        {
            m_body = pBody;
            if (pBody == IntPtr.Zero || m_type == Vehicle.TYPE_NONE)
                return;
            if (Mass == 0)
                GetMass(pBody);
            if (Mass == 0)
                return; //No noMass vehicles...
            if (!d.BodyIsEnabled(Body))
                d.BodyEnable(Body);

            frcount++; // used to limit debug comment output
            if (frcount > 100)
                frcount = 0;

            // scale time so parameters work as before
            // until we scale then acording to ode step time

            MoveLinear(pTimestep, pParentScene, parent);
            MoveAngular(pTimestep, pParentScene, parent);
            LimitRotation(pTimestep);
        }

        // end Step

        private void MoveLinear(float pTimestep, WhiteCoreODEPhysicsScene _pParentScene, WhiteCoreODEPrim parent)
        {
            bool ishovering = false;
            bool bypass_buoyancy = false;
            d.Vector3 dpos = d.BodyGetPosition(Body);
            d.Vector3 dvel_now = d.BodyGetLinearVel(Body);
            d.Quaternion drotq_now = d.BodyGetQuaternion(Body);

            Vector3 pos = new Vector3(dpos.X, dpos.Y, dpos.Z);
            Vector3 vel_now = new Vector3(dvel_now.X, dvel_now.Y, dvel_now.Z);
            Quaternion rotq = new Quaternion(drotq_now.X, drotq_now.Y, drotq_now.Z, drotq_now.W);
            rotq *= m_referenceFrame; //add reference rotation to rotq
            Quaternion irotq = new Quaternion(-rotq.X, -rotq.Y, -rotq.Z, rotq.W);

            m_newVelocity = Vector3.Zero;

            if (!(m_lastPositionVector.X == 0 &&
                  m_lastPositionVector.Y == 0 &&
                  m_lastPositionVector.Z == 0))
            {
                ///Only do this if we have a last position
                m_lastposChange.X = pos.X - m_lastPositionVector.X;
                m_lastposChange.Y = pos.Y - m_lastPositionVector.Y;
                m_lastposChange.Z = pos.Z - m_lastPositionVector.Z;
            }

            #region Blocking Change

            if (m_BlockingEndPoint != Vector3.Zero)
            {
                bool needUpdateBody = false;
                if(pos.X >= (m_BlockingEndPoint.X - 1)) {
                    pos.X -= m_lastposChange.X + 1;
                    needUpdateBody = true;
                }
                if(pos.Y >= (m_BlockingEndPoint.Y - 1)) {
                    pos.Y -= m_lastposChange.Y + 1;
                    needUpdateBody = true;
                }
                if(pos.Z >= (m_BlockingEndPoint.Z - 1)) {
                    pos.Z -= m_lastposChange.Z + 1;
                    needUpdateBody = true;
                }
                if(pos.X <= 0) {
                    pos.X += m_lastposChange.X + 1;
                    needUpdateBody = true;
                }
                if(pos.Y <= 0) {
                    pos.Y += m_lastposChange.Y + 1;
                    needUpdateBody = true;
                }
                if(needUpdateBody)
                    d.BodySetPosition(Body, pos.X, pos.Y, pos.Z);
            }

            #endregion

            #region Terrain checks

            float terrainHeight = _pParentScene.GetTerrainHeightAtXY(pos.X, pos.Y);
            if(pos.Z < terrainHeight - 5) {
                pos.Z = terrainHeight + 2;
                m_lastPositionVector = pos;
                d.BodySetPosition(Body, pos.X, pos.Y, pos.Z);
            }
            else if(pos.Z < terrainHeight) {
                m_newVelocity.Z += 1;
            }

            #endregion

            #region Hover

            Vector3 hovervel = Vector3.Zero;
            if(m_VhoverTimescale * pTimestep <= 300.0f && m_VhoverHeight > 0.0f) {
                ishovering = true;

                if ((m_flags & VehicleFlag.HOVER_WATER_ONLY) != 0) {
                    m_VhoverTargetHeight = (float) _pParentScene.GetWaterLevel(pos.X, pos.Y) + 0.3f + m_VhoverHeight;
                }
                else if ((m_flags & VehicleFlag.HOVER_TERRAIN_ONLY) != 0) {
                    m_VhoverTargetHeight = _pParentScene.GetTerrainHeightAtXY(pos.X, pos.Y) + m_VhoverHeight;
                }
                else if ((m_flags & VehicleFlag.HOVER_GLOBAL_HEIGHT) != 0) {
                    m_VhoverTargetHeight = m_VhoverHeight;
                }
                else {
                    float waterlevel = (float)_pParentScene.GetWaterLevel(pos.X, pos.Y) + 0.3f;
                    float terrainlevel = (float)_pParentScene.GetTerrainHeightAtXY(pos.X, pos.Y);
                    if(waterlevel > terrainlevel) {
                        m_VhoverTargetHeight = waterlevel + m_VhoverHeight;
                    }
                    else {
                        m_VhoverTargetHeight = terrainlevel + m_VhoverHeight;
                    }
                }

                float tempHoverHeight = m_VhoverTargetHeight;
                if((m_flags & VehicleFlag.HOVER_UP_ONLY) != 0) {
                    // If body is aready heigher, use its height as target height
                    if (pos.Z > tempHoverHeight) {
                        tempHoverHeight = pos.Z;
                        bypass_buoyancy = true; //emulate sl bug
                    }
                }
                if((m_flags & VehicleFlag.LOCK_HOVER_HEIGHT) != 0) {
                    if((pos.Z - tempHoverHeight) > .2 || (pos.Z - tempHoverHeight) < -.2) {
                        float h = tempHoverHeight;
                        float groundHeight = _pParentScene.GetTerrainHeightAtXY(pos.X, pos.Y);
                        if(groundHeight >= tempHoverHeight) 
                            h = groundHeight;

                        d.BodySetPosition(Body, pos.X, pos.Y, h);
                    }
                }
                else {
                    hovervel.Z -= ((dvel_now.Z * 0.1f * m_VhoverEfficiency) + (pos.Z - tempHoverHeight)) / m_VhoverTimescale;
                    hovervel.Z *= 7.0f * (1.0f + m_VhoverEfficiency);

                    if(hovervel.Z > 50.0f) hovervel.Z = 50.0f;
                    if(hovervel.Z < -50.0f) hovervel.Z = -50.0f;
                }
            }

            #endregion

            #region limitations

            //limit maximum velocity
            if(vel_now.LengthSquared() > 1e6f) {
                vel_now /= vel_now.Length();
                vel_now *= 1000f;
                d.BodySetLinearVel(Body, vel_now.X, vel_now.Y, vel_now.Z);
            }

            //block movement in x and y when low velocity
            bool enable_ode_gravity = true;
            if(vel_now.LengthSquared() < 0.02f) {
                d.BodySetLinearVel(Body, 0.0f, 0.0f, 0.0f);
                vel_now = Vector3.Zero;
                if(parent.LinkSetIsColliding)
                    enable_ode_gravity = false;
            }

            #endregion

            #region Linear motors

            //cancel directions of linear friction for certain vehicles without having effect on ode gravity
            Vector3 vt_vel_now = vel_now;
            bool no_grav_calc = false;
            if((Type != Vehicle.TYPE_AIRPLANE && Type != Vehicle.TYPE_BALLOON) && m_VehicleBuoyancy != 1.0f) {
                vt_vel_now.Z = 0.0f;
                no_grav_calc = true;
            }

            if(!bypass_buoyancy) {
                //apply additional gravity force over ode gravity
                if(m_VehicleBuoyancy == 1.0f) enable_ode_gravity = false;
                else if(m_VehicleBuoyancy != 0.0f && enable_ode_gravity) {
                    float grav = _pParentScene.gravityz * parent.GravityMultiplier * -m_VehicleBuoyancy;
                    m_newVelocity.Z += grav * Mass;
                }
            }

            //set ode gravity
            d.BodySetGravityMode(Body, enable_ode_gravity);

            //add default linear friction (mimic sl friction as much as possible)
            float initialFriction = 0.055f;
            float defaultFriction = 180f;

            Vector3 friction = Vector3.Zero;
            if(parent.LinkSetIsColliding || ishovering) {
                if(vt_vel_now.X > 0.0f) friction.X += initialFriction;
                if(vt_vel_now.Y > 0.0f) friction.Y += initialFriction;
                if(vt_vel_now.Z > 0.0f) friction.Z += initialFriction;
                if(vt_vel_now.X < 0.0f) friction.X -= initialFriction;
                if(vt_vel_now.Y < 0.0f) friction.Y -= initialFriction;
                if(vt_vel_now.Z < 0.0f) friction.Z -= initialFriction;
                friction += vt_vel_now / defaultFriction;
                friction *= irotq;
            }

            //world -> body orientation
            vel_now *= irotq;
            vt_vel_now *= irotq;

            //add linear friction
            if(vt_vel_now.X > 0.0f)
                friction.X += vt_vel_now.X * vt_vel_now.X / m_linearFrictionTimescale.X;
            else
                friction.X -= vt_vel_now.X * vt_vel_now.X / m_linearFrictionTimescale.X;

            if(vt_vel_now.Y > 0.0f)
                friction.Y += vt_vel_now.Y * vt_vel_now.Y / m_linearFrictionTimescale.Y;
            else
                friction.Y -= vt_vel_now.Y * vt_vel_now.Y / m_linearFrictionTimescale.Y;

            if(vt_vel_now.Z > 0.0f)
                friction.Z += vt_vel_now.Z * vt_vel_now.Z / m_linearFrictionTimescale.Z;
            else
                friction.Z -= vt_vel_now.Z * vt_vel_now.Z / m_linearFrictionTimescale.Z;
	    
            friction /= 1.35f; //1.5f;

            //add linear forces
            //not the best solution, but it is really close to sl motor velocity, and just works
            Vector3 motorVelocity = (m_linearMotorDirection * 3.0f - vel_now) / m_linearMotorTimescale / 5.0f;  //2.8f;
            Vector3 motorfrictionamp = new Vector3(4.0f, 4.0f, 4.0f);
            Vector3 motorfrictionstart = new Vector3(1.0f, 1.0f, 1.0f);
            motorVelocity *= motorfrictionstart + motorfrictionamp / (m_linearFrictionTimescale * pTimestep);
            float addVel = 0.15f;
            if(motorVelocity.LengthSquared() > 0.01f) {
                if(motorVelocity.X > 0.0f) motorVelocity.X += addVel;
                if(motorVelocity.Y > 0.0f) motorVelocity.Y += addVel;
                if(motorVelocity.Z > 0.0f) motorVelocity.Z += addVel;
                if(motorVelocity.X < 0.0f) motorVelocity.X -= addVel;
                if(motorVelocity.Y < 0.0f) motorVelocity.Y -= addVel;
                if(motorVelocity.Z < 0.0f) motorVelocity.Z -= addVel;
            }

            //free run
            if(vel_now.X > m_linearMotorDirection.X && m_linearMotorDirection.X >= 0.0f) motorVelocity.X = 0.0f;
            if(vel_now.Y > m_linearMotorDirection.Y && m_linearMotorDirection.Y >= 0.0f) motorVelocity.Y = 0.0f;
            if(vel_now.Z > m_linearMotorDirection.Z && m_linearMotorDirection.Z >= 0.0f) motorVelocity.Z = 0.0f;

            if(vel_now.X < m_linearMotorDirection.X && m_linearMotorDirection.X <= 0.0f) motorVelocity.X = 0.0f;
            if(vel_now.Y < m_linearMotorDirection.Y && m_linearMotorDirection.Y <= 0.0f) motorVelocity.Y = 0.0f;
            if(vel_now.Z < m_linearMotorDirection.Z && m_linearMotorDirection.Z <= 0.0f) motorVelocity.Z = 0.0f;

            //decay linear motor
            m_linearMotorDirection *= (1.0f - 1.0f/m_linearMotorDecayTimescale);

            #endregion

            #region Deflection

            //does only deflect on x axis from world orientation with z axis rotated to body
            //it is easier to filter out gravity deflection for vehicles(car) without rotation problems
            Quaternion irotq_z = irotq;
            irotq_z.X = 0.0f;
            irotq_z.Y = 0.0f;
            float mag = (float)Math.Sqrt(irotq_z.W * irotq_z.W + irotq_z.Z * irotq_z.Z); //normalize
            irotq_z.W /= mag;
            irotq_z.Z /= mag;

            Vector3 vel_defl = new Vector3(dvel_now.X, dvel_now.Y, dvel_now.Z);
            vel_defl *= irotq_z;
            if(no_grav_calc) {
                vel_defl.Z = 0.0f;
                if(!parent.LinkSetIsColliding) vel_defl.Y = 0.0f;
            }

            Vector3 deflection = vel_defl / m_linearDeflectionTimescale * m_linearDeflectionEfficiency * 100.0f;
            
			float deflectionLengthY = Math.Abs(deflection.Y);
            float deflectionLengthX = Math.Abs(deflection.X);

            deflection.Z = 0.0f;
            if((m_flags & (VehicleFlag.NO_DEFLECTION_UP)) == 0) {
                deflection.Z = deflectionLengthX;
                deflection.X = -deflection.X;
            }

            if(vel_defl.X < 0.0f) deflection.X = -deflectionLengthY;
            else if(vel_defl.X >= 0.0f) deflection.X = deflectionLengthY;
            deflection.Y = -deflection.Y;

            irotq_z.W = -irotq_z.W;
            deflection *= irotq_z;

            #endregion

            #region Deal with tainted forces

            Vector3 TaintedForce = new Vector3();
            if(m_forcelist.Count != 0) {
                try {
                    TaintedForce = m_forcelist.Aggregate(TaintedForce, (current, t) => current + (t));
                }
                catch(IndexOutOfRangeException) {
                    TaintedForce = Vector3.Zero;
                }
                catch(ArgumentOutOfRangeException) {
                    TaintedForce = Vector3.Zero;
                }
                m_forcelist = new List<Vector3>();
            }

            #endregion

            #region Add Forces

            //add forces
            m_newVelocity -= (friction *= Mass / pTimestep);
            m_newVelocity += TaintedForce;
            motorVelocity *= Mass / pTimestep;

            #endregion

            #region No X,Y,Z

            if((m_flags & (VehicleFlag.NO_X)) != 0)
                m_newVelocity.X = -vel_now.X * Mass / pTimestep;
            if((m_flags & (VehicleFlag.NO_Y)) != 0)
                m_newVelocity.Y = -vel_now.Y * Mass / pTimestep;
            if((m_flags & (VehicleFlag.NO_Z)) != 0)
                m_newVelocity.Z = -vel_now.Z * Mass / pTimestep;

            #endregion

    	    m_newVelocity *= rotq;
            m_newVelocity += (hovervel *= Mass / pTimestep);
	    
            if(parent.LinkSetIsColliding || Type == Vehicle.TYPE_AIRPLANE || Type == Vehicle.TYPE_BALLOON || ishovering) {
                m_newVelocity += deflection;

                motorVelocity *= rotq;

                if((m_flags & (VehicleFlag.LIMIT_MOTOR_UP)) != 0 && motorVelocity.Z > 0.0f) motorVelocity.Z = 0.0f;
                m_newVelocity += motorVelocity;
            }

            d.BodyAddForce(Body, m_newVelocity.X, m_newVelocity.Y, m_newVelocity.Z);

        }

        private void MoveAngular(float pTimestep, WhiteCoreODEPhysicsScene _pParentScene, WhiteCoreODEPrim parent)
        {
            bool ishovering = false;
            d.Vector3 d_angularVelocity = d.BodyGetAngularVel(Body);
            d.Vector3 d_lin_vel_now = d.BodyGetLinearVel(Body);
            d.Quaternion drotq = d.BodyGetQuaternion(Body);
            Quaternion rotq = new Quaternion(drotq.X, drotq.Y, drotq.Z, drotq.W);
            rotq *= m_referenceFrame; //add reference rotation to rotq
            Quaternion irotq = new Quaternion(-rotq.X, -rotq.Y, -rotq.Z, rotq.W);
            Vector3 angularVelocity = new Vector3(d_angularVelocity.X, d_angularVelocity.Y, d_angularVelocity.Z);
            Vector3 linearVelocity = new Vector3(d_lin_vel_now.X, d_lin_vel_now.Y, d_lin_vel_now.Z);

            Vector3 friction = Vector3.Zero;
            Vector3 vertattr = Vector3.Zero;
            Vector3 deflection = Vector3.Zero;
            Vector3 banking = Vector3.Zero;

            //limit maximum rotation speed
            if(angularVelocity.LengthSquared() > 1e3f) {
                angularVelocity = Vector3.Zero;
                d.BodySetAngularVel(Body, angularVelocity.X, angularVelocity.Y, angularVelocity.Z);
            }

            angularVelocity *= irotq; //world to body orientation

            if(m_VhoverTimescale * pTimestep <= 300.0f && m_VhoverHeight > 0.0f) ishovering = true;

            #region Angular motor
            Vector3 motorDirection = Vector3.Zero;

            if(Type == Vehicle.TYPE_BOAT) {
                //keep z flat for boats, no sidediving lol
                Vector3 tmp = new Vector3(0.0f, 0.0f, m_angularMotorDirection.Z);
                m_angularMotorDirection.Z = 0.0f;
                m_angularMotorDirection += tmp * irotq;
            }

            if(parent.LinkSetIsColliding || Type == Vehicle.TYPE_AIRPLANE || Type == Vehicle.TYPE_BALLOON || ishovering){
                motorDirection = m_angularMotorDirection * 0.34f; //0.3f;
            }

            m_angularMotorVelocity.X = (motorDirection.X - angularVelocity.X) / m_angularMotorTimescale;
            m_angularMotorVelocity.Y = (motorDirection.Y - angularVelocity.Y) / m_angularMotorTimescale;
            m_angularMotorVelocity.Z = (motorDirection.Z - angularVelocity.Z) / m_angularMotorTimescale;
            m_angularMotorDirection *= (1.0f - 1.0f/m_angularMotorDecayTimescale);

            if(m_angularMotorDirection.LengthSquared() > 0.0f)
            {
                if(angularVelocity.X > m_angularMotorDirection.X && m_angularMotorDirection.X >= 0.0f) m_angularMotorVelocity.X = 0.0f;
                if(angularVelocity.Y > m_angularMotorDirection.Y && m_angularMotorDirection.Y >= 0.0f) m_angularMotorVelocity.Y = 0.0f;
                if(angularVelocity.Z > m_angularMotorDirection.Z && m_angularMotorDirection.Z >= 0.0f) m_angularMotorVelocity.Z = 0.0f;

                if(angularVelocity.X < m_angularMotorDirection.X && m_angularMotorDirection.X <= 0.0f) m_angularMotorVelocity.X = 0.0f;
                if(angularVelocity.Y < m_angularMotorDirection.Y && m_angularMotorDirection.Y <= 0.0f) m_angularMotorVelocity.Y = 0.0f;
                if(angularVelocity.Z < m_angularMotorDirection.Z && m_angularMotorDirection.Z <= 0.0f) m_angularMotorVelocity.Z = 0.0f;
            }

            #endregion

            #region friction

            float initialFriction = 0.0001f;
            if(angularVelocity.X > initialFriction) friction.X += initialFriction;
    	    if(angularVelocity.Y > initialFriction) friction.Y += initialFriction;
    	    if(angularVelocity.Z > initialFriction) friction.Z += initialFriction;
            if(angularVelocity.X < -initialFriction) friction.X -= initialFriction;
            if(angularVelocity.Y < -initialFriction) friction.Y -= initialFriction;
            if(angularVelocity.Z < -initialFriction) friction.Z -= initialFriction;

            if(angularVelocity.X > 0.0f)
                friction.X += angularVelocity.X * angularVelocity.X / m_angularFrictionTimescale.X;
            else
                friction.X -= angularVelocity.X * angularVelocity.X / m_angularFrictionTimescale.X;
            
            if(angularVelocity.Y > 0.0f)
                friction.Y += angularVelocity.Y * angularVelocity.Y / m_angularFrictionTimescale.Y;
            else
                friction.Y -= angularVelocity.Y * angularVelocity.Y / m_angularFrictionTimescale.Y;

            if(angularVelocity.Z > 0.0f)
                friction.Z += angularVelocity.Z * angularVelocity.Z / m_angularFrictionTimescale.Z;
            else
                friction.Z -= angularVelocity.Z * angularVelocity.Z / m_angularFrictionTimescale.Z;


            if(Math.Abs(m_angularMotorDirection.X) > 0.01f) friction.X = 0.0f;
            if(Math.Abs(m_angularMotorDirection.Y) > 0.01f) friction.Y = 0.0f;
            if(Math.Abs(m_angularMotorDirection.Z) > 0.01f) friction.Z = 0.0f;

            #endregion

            #region Vertical attraction

            if(m_verticalAttractionTimescale < 300)
            {
                float VAservo = 38.0f / m_verticalAttractionTimescale;
                if(Type == Vehicle.TYPE_CAR) VAservo = 10.0f / m_verticalAttractionTimescale;

                Vector3 verterr = new Vector3(0.0f, 0.0f, 1.0f);
                verterr *= rotq;
                vertattr.X = verterr.Y;
                vertattr.Y = -verterr.X;
                vertattr.Z = 0.0f;

                vertattr *= irotq;

                //when upsidedown prefer x rotation of body, to keep forward movement direction the same
                if(verterr.Z < 0.0f) {
                    vertattr.Y = -vertattr.Y * 2.0f;
                    if(vertattr.X < 0.0f) vertattr.X = -2.0f - vertattr.X;
                    else vertattr.X = 2.0f - vertattr.X;
                }

                vertattr *= VAservo;

                vertattr.X += (vertattr.X - angularVelocity.X) * (0.004f * m_verticalAttractionEfficiency + 0.0001f);
                vertattr.Y += (vertattr.Y - angularVelocity.Y) * (0.004f * m_verticalAttractionEfficiency + 0.0001f);

                if((m_flags & (VehicleFlag.LIMIT_ROLL_ONLY)) != 0) vertattr.Y = 0.0f;
            }

            #endregion

            #region deflection
            //rotates body to direction of movement (linearMovement vector)
			/* temporary disabled due to instabilities, needs to be rewritten
            if(m_angularDeflectionTimescale < 300)
            {
                float Dservo = 0.05f * m_angularDeflectionTimescale * m_angularDeflectionEfficiency;
                float mag = (float)linearVelocity.LengthSquared();
                if(mag > 0.01f) {
                    linearVelocity.Y = -linearVelocity.Y;
                    linearVelocity *= rotq;

                    mag = (float)Math.Sqrt(mag);

                    linearVelocity.Y /= mag;
                    linearVelocity.Z /= mag;

                    deflection.Y = -linearVelocity.Z;
                    deflection.Z = -linearVelocity.Y;

                    deflection *= Dservo;
                }
            }
			*/
            #endregion

            #region banking

            if(m_verticalAttractionTimescale < 300 && m_bankingEfficiency > 0) { //vertical attraction must be enabled		
                float mag = (float)(linearVelocity.X * linearVelocity.X + linearVelocity.Y * linearVelocity.Y);
                if(mag > 0.01f) {
                    mag = (float)Math.Sqrt(mag);
                    if(mag > 20.0f) mag = 1.0f;
                    else mag /= 20.0f;
                }
                else mag = 0.0f;

                float b_static = -m_angularMotorDirection.X * 0.12f * (1.0f - m_bankingMix);
                float b_dynamic = -m_angularMotorDirection.X * 0.12f * mag * m_bankingMix;

                banking.Z = (b_static + b_dynamic - d_angularVelocity.Z) / m_bankingTimescale * m_bankingEfficiency;
            }

            #endregion

            m_lastAngularVelocity = angularVelocity;
            
            if(parent.LinkSetIsColliding || Type == Vehicle.TYPE_AIRPLANE || Type == Vehicle.TYPE_BALLOON || ishovering) {		
                angularVelocity += deflection;
                angularVelocity -= friction;
            }
            else {
                banking = Vector3.Zero;
            }

            angularVelocity += m_angularMotorVelocity;
            angularVelocity += vertattr;
            angularVelocity *= rotq;
            angularVelocity += banking;

            if(angularVelocity.LengthSquared() < 1e-5f) {
                angularVelocity = Vector3.Zero;
                d.BodySetAngularVel(Body, 0, 0, 0);
                m_angularZeroFlag = true;
            }
            else {
                d.BodySetAngularVel(Body, angularVelocity.X, angularVelocity.Y, angularVelocity.Z);
                m_angularZeroFlag = false;
            }
        }

        private Vector3 ToEuler(Quaternion m_lastCameraRotation)
        {
            Quaternion t = new Quaternion(m_lastCameraRotation.X*m_lastCameraRotation.X,
                                          m_lastCameraRotation.Y*m_lastCameraRotation.Y,
                                          m_lastCameraRotation.Z*m_lastCameraRotation.Z,
                                          m_lastCameraRotation.W*m_lastCameraRotation.W);
            double m = (m_lastCameraRotation.X + m_lastCameraRotation.Y + m_lastCameraRotation.Z +
                        m_lastCameraRotation.W);
            if (m == 0) return Vector3.Zero;
            double n = 2*(m_lastCameraRotation.Y*m_lastCameraRotation.W + m_lastCameraRotation.X*m_lastCameraRotation.Y);
            double p = m*m - n*n;
            if (p > 0)
                return
                    new Vector3(
                        (float)
                        NormalizeAngle(
                            Math.Atan2(
                                2.0*
                                (m_lastCameraRotation.X*m_lastCameraRotation.W -
                                 m_lastCameraRotation.Y*m_lastCameraRotation.Z), (-t.X - t.Y + t.Z + t.W))),
                        (float) NormalizeAngle(Math.Atan2(n, Math.Sqrt(p))),
                        (float)
                        NormalizeAngle(
                            Math.Atan2(
                                2.0*
                                (m_lastCameraRotation.Z*m_lastCameraRotation.W -
                                 m_lastCameraRotation.X*m_lastCameraRotation.Y), (t.X - t.Y - t.Z + t.W))));
            else if (n > 0)
                return new Vector3(0, (float) (Math.PI*0.5),
                                   (float)
                                   NormalizeAngle(
                                       Math.Atan2(
                                           (m_lastCameraRotation.Z*m_lastCameraRotation.W +
                                            m_lastCameraRotation.X*m_lastCameraRotation.Y), 0.5 - t.X - t.Z)));
            else
                return new Vector3(0, (float) (-Math.PI*0.5),
                                   (float)
                                   NormalizeAngle(
                                       Math.Atan2(
                                           (m_lastCameraRotation.Z*m_lastCameraRotation.W +
                                            m_lastCameraRotation.X*m_lastCameraRotation.Y), 0.5 - t.X - t.Z)));
        }

        protected double NormalizeAngle(double angle)
        {
            if (angle > -Math.PI && angle < Math.PI)
                return angle;

            int numPis = (int) (Math.PI/angle);
            double remainder = angle - Math.PI*numPis;
            if (numPis%2 == 1)
                return Math.PI - angle;
            return remainder;
        }

        //end MoveAngular

        internal void LimitRotation(float timestep)
        {
            if (m_RollreferenceFrame != Quaternion.Identity || (m_flags & VehicleFlag.LOCK_ROTATION) != 0)
            {
                d.Quaternion rot = d.BodyGetQuaternion(Body);
                d.Quaternion m_rot = rot;
                if (rot.X >= m_RollreferenceFrame.X)
                    m_rot.X = rot.X - (m_RollreferenceFrame.X/2);

                if (rot.Y >= m_RollreferenceFrame.Y)
                    m_rot.Y = rot.Y - (m_RollreferenceFrame.Y/2);

                if (rot.X <= -m_RollreferenceFrame.X)
                    m_rot.X = rot.X + (m_RollreferenceFrame.X/2);

                if (rot.Y <= -m_RollreferenceFrame.Y)
                    m_rot.Y = rot.Y + (m_RollreferenceFrame.Y/2);

                if ((m_flags & VehicleFlag.LOCK_ROTATION) != 0)
                {
                    m_rot.X = 0;
                    m_rot.Y = 0;
                }

                if (m_rot.X != rot.X || m_rot.Y != rot.Y || m_rot.Z != rot.Z)
                    d.BodySetQuaternion(Body, ref m_rot);
            }
        }

        internal void ProcessSetCameraPos(Quaternion CameraRotation)
        {
            //m_referenceFrame -= m_lastCameraRotation;
            //m_referenceFrame += CameraRotation;
            m_userLookAt = CameraRotation;
        }

        internal void ProcessForceTaint(Vector3 force)
        {
            m_forcelist.Add(force);
        }

        public Quaternion llRotBetween(Vector3 a, Vector3 b)
        {
            Quaternion rotBetween;
            // Check for zero vectors. If either is zero, return zero rotation. Otherwise,
            // continue calculation.
            if (a == Vector3.Zero || b == Vector3.Zero)
            {
                rotBetween = Quaternion.Identity;
            }
            else
            {
                a.Normalize();
                b.Normalize();
                double dotProduct = (a.X*b.X) + (a.Y*b.Y) + (a.Z*b.Z);
                // There are two degenerate cases possible. These are for vectors 180 or
                // 0 degrees apart. These have to be detected and handled individually.
                //
                // Check for vectors 180 degrees apart.
                // A dot product of -1 would mean the angle between vectors is 180 degrees.
                if (dotProduct < -0.9999999f)
                {
                    // First assume X axis is orthogonal to the vectors.
                    Vector3 orthoVector = new Vector3(1.0f, 0.0f, 0.0f);
                    orthoVector = orthoVector - a*(a.X/(a.X*a.X) + (a.Y*a.Y) + (a.Z*a.Z));
                    // Check for near zero vector. A very small non-zero number here will create
                    // a rotation in an undesired direction.
                    rotBetween = Math.Sqrt(orthoVector.X*orthoVector.X + orthoVector.Y*orthoVector.Y +
                                           orthoVector.Z*orthoVector.Z) > 0.0001
                                     ? new Quaternion(orthoVector.X, orthoVector.Y, orthoVector.Z, 0.0f)
                                     : new Quaternion(0.0f, 0.0f, 1.0f, 0.0f);
                }
                    // Check for parallel vectors.
                    // A dot product of 1 would mean the angle between vectors is 0 degrees.
                else if (dotProduct > 0.9999999f)
                {
                    // Set zero rotation.
                    rotBetween = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
                }
                else
                {
                    // All special checks have been performed so get the axis of rotation.
                    Vector3 crossProduct = new Vector3
                        (
                        a.Y*b.Z - a.Z*b.Y,
                        a.Z*b.X - a.X*b.Z,
                        a.X*b.Y - a.Y*b.X
                        );
                    // Quarternion s value is the length of the unit vector + dot product.
                    double qs = 1.0 + dotProduct;
                    rotBetween = new Quaternion(crossProduct.X, crossProduct.Y, crossProduct.Z, (float) qs);
                    // Normalize the rotation.
                    double mag =
                        Math.Sqrt(rotBetween.X*rotBetween.X + rotBetween.Y*rotBetween.Y + rotBetween.Z*rotBetween.Z +
                                  rotBetween.W*rotBetween.W);
                    // We shouldn't have to worry about a divide by zero here. The qs value will be
                    // non-zero because we already know if we're here, then the dotProduct is not -1 so
                    // qs will not be zero. Also, we've already handled the input vectors being zero so the
                    // crossProduct vector should also not be zero.
                    rotBetween.X = (float) (rotBetween.X/mag);
                    rotBetween.Y = (float) (rotBetween.Y/mag);
                    rotBetween.Z = (float) (rotBetween.Z/mag);
                    rotBetween.W = (float) (rotBetween.W/mag);
                    // Check for undefined values and set zero rotation if any found. This code might not actually be required
                    // any longer since zero vectors are checked for at the top.
                    if (Double.IsNaN(rotBetween.X) || Double.IsNaN(rotBetween.Y) || Double.IsNaN(rotBetween.Y) ||
                        Double.IsNaN(rotBetween.W))
                    {
                        rotBetween = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
                    }
                }
            }
            return rotBetween;
        }


        public int m_angularMotorApply { get; set; }

        public bool m_angularZeroFlag { get; set; }
    }
}