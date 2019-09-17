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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using WhiteCore.ScriptEngine.DotNetEngine.Plugins;
using WhiteCore.ScriptEngine.DotNetEngine.Runtime;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using LSL_Float = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger;
using LSL_Key = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.List;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {
        public DateTime llMakeExplosion(int particles, double scale, double vel, double lifetime, double arc,
                        string texture, LSL_Vector offset) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            Deprecated("llMakeExplosion", "Use llParticleSystem instead");
            return PScriptSleep(m_sleepMsOnMakeExplosion);
        }

        public DateTime llMakeFountain(int particles, double scale, double vel, double lifetime, double arc, int bounce,
                                       string texture, LSL_Vector offset, double bounce_offset) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            Deprecated("llMakeFountain", "Use llParticleSystem instead");
            return PScriptSleep(m_sleepMsOnMakeFountain);
        }

        public DateTime llMakeSmoke(int particles, double scale, double vel, double lifetime, double arc, string texture,
                                    LSL_Vector offset) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            Deprecated("llMakeSmoke", "Use llParticleSystem instead");
            return PScriptSleep(m_sleepMsOnMakeSmoke);
        }

        public DateTime llMakeFire(int particles, double scale, double vel, double lifetime, double arc, string texture,
                                   LSL_Vector offset) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            Deprecated("llMakeFire", "Use llParticleSystem instead");
            return PScriptSleep(m_sleepMsOnMakeFire);
        }

        public void llApplyImpulse(LSL_Vector force, int local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            //No energy force yet
            Vector3 v = new Vector3((float)force.x, (float)force.y, (float)force.z);
            float len = v.Length();
            if (len > 20000.0f) {
                //                v.Normalize();
                v = v * 20000.0f / len;
            }
            m_host.ApplyImpulse(v, local != 0);
        }

        public void llApplyRotationalImpulse(LSL_Vector force, int local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.ApplyAngularImpulse(new Vector3((float)force.x, (float)force.y, (float)force.z), local != 0);
        }

        public void llCollisionFilter(string name, string id, int accept) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.CollisionFilter.Clear();
            m_host.CollisionFilter.Add(accept, id ?? name);
        }

        public void llCollisionSound(string impact_sound, double impact_volume) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.CollisionSound = KeyOrName(impact_sound, AssetType.Sound, true);
            m_host.CollisionSoundVolume = (float)impact_volume;
        }

        public void llCollisionSprite(string impact_sprite) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            // Since this is broken in SL, we can do this however we want, until they fix it.
            m_host.CollisionSprite = UUID.Parse(impact_sprite);
        }

        public LSL_List llCastRay(LSL_Vector start, LSL_Vector end, LSL_List options) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            LSL_List list = new LSL_List();

            Vector3 rayStart = start.ToVector3();
            Vector3 rayEnd = end.ToVector3();
            Vector3 dir = rayEnd - rayStart;

            float dist = Vector3.Mag(dir);

            int count = 1;
            bool detectPhantom = false;
            int dataFlags = 0;
            int rejectTypes = 0;

            for (int i = 0; i < options.Length; i += 2) {
                if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_MAX_HITS)
                    count = options.GetLSLIntegerItem(i + 1);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_DETECT_PHANTOM)
                    detectPhantom = (options.GetLSLIntegerItem(i + 1) > 0);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_DATA_FLAGS)
                    dataFlags = options.GetLSLIntegerItem(i + 1);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_REJECT_TYPES)
                    rejectTypes = options.GetLSLIntegerItem(i + 1);
            }

            if (count > 16)
                count = 16;
            else if (count <= 0) {
                Error("llCastRay", "You must request at least one result from llCastRay.");
                return new LSL_List();
            }

            List<ContactResult> results = new List<ContactResult>();

            bool checkTerrain = !((rejectTypes & ScriptBaseClass.RC_REJECT_LAND) == ScriptBaseClass.RC_REJECT_LAND);
            bool checkAgents = !((rejectTypes & ScriptBaseClass.RC_REJECT_AGENTS) == ScriptBaseClass.RC_REJECT_AGENTS);
            bool checkNonPhysical = !((rejectTypes & ScriptBaseClass.RC_REJECT_NONPHYSICAL) == ScriptBaseClass.RC_REJECT_NONPHYSICAL);
            bool checkPhysical = !((rejectTypes & ScriptBaseClass.RC_REJECT_PHYSICAL) == ScriptBaseClass.RC_REJECT_PHYSICAL);


            if (checkAgents) {
                ContactResult[] agentHits = AvatarIntersection(rayStart, rayEnd);
                foreach (ContactResult r in agentHits)
                    results.Add(r);
            }

            if (checkPhysical || checkNonPhysical || detectPhantom) {
                ContactResult[] objectHits = ObjectIntersection(rayStart, rayEnd, checkPhysical, checkNonPhysical, detectPhantom, count + 2);
                for (int iter = 0; iter < objectHits.Length; iter++) {
                    // Redistance the Depth because the Scene RayCaster returns distance from center to make the rezzing code simpler.
                    objectHits[iter].Depth = Vector3.Distance(objectHits[iter].Pos, rayStart);
                    results.Add(objectHits[iter]);
                }
            }

            if (checkTerrain) {
                ContactResult? groundContact = GroundIntersection(rayStart, rayEnd);
                if (groundContact != null)
                    results.Add((ContactResult)groundContact);
            }

            results.Sort(delegate (ContactResult a, ContactResult b) {
                return a.Depth.CompareTo(b.Depth);
            });

            int values = 0;
            ISceneEntity thisgrp = m_host.ParentEntity;

            foreach (ContactResult result in results) {
                if (result.Depth > dist)
                    continue;

                // physics ray can return colisions with host prim
                // this is supposed to happen
                if (m_host.LocalId == result.ConsumerID)
                    continue;

                if (!checkTerrain && result.ConsumerID == 0)
                    continue; //Terrain

                UUID itemID = UUID.Zero;
                int linkNum = 0;

                ISceneChildEntity part = World.GetSceneObjectPart(result.ConsumerID);
                // It's a prim!
                if (part != null) {
                    // dont detect members of same object ???
                    if (part.ParentEntity == thisgrp)
                        continue;

                    if ((dataFlags & ScriptBaseClass.RC_GET_ROOT_KEY) == ScriptBaseClass.RC_GET_ROOT_KEY)
                        itemID = part.ParentEntity.UUID;
                    else
                        itemID = part.UUID;

                    linkNum = part.LinkNum;
                } else {
                    IScenePresence sp = World.GetScenePresence(result.ConsumerID);
                    /// It it a boy? a girl?
                    if (sp != null)
                        itemID = sp.UUID;
                }

                list.Add(new LSL_String(itemID.ToString()));

                if ((dataFlags & ScriptBaseClass.RC_GET_LINK_NUM) == ScriptBaseClass.RC_GET_LINK_NUM)
                    list.Add(new LSL_Integer(linkNum));

                list.Add(new LSL_Vector(result.Pos));

                if ((dataFlags & ScriptBaseClass.RC_GET_NORMAL) == ScriptBaseClass.RC_GET_NORMAL) {
                    Vector3 norm = result.Normal * -1;
                    list.Add(new LSL_Vector(norm));
                }

                values++;
                if (values >= count)
                    break;
            }

            list.Add(new LSL_Integer(values));

            return list;
        }

        /// <summary>
        ///     See http://lslwiki.net/lslwiki/wakka.php?wakka=ChildRotation
        /// </summary>
        public LSL_Rotation llGetRot() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation();
            // unlinked or root prim then use llRootRotation
            // see llRootRotaion for references.
            if (m_host.LinkNum == 0 || m_host.LinkNum == 1) {
                return llGetRootRotation();
            }

            Quaternion q = m_host.GetWorldRotation();
            return new LSL_Rotation(q.X, q.Y, q.Z, q.W);
        }

        public LSL_Rotation llGetLocalRot() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation();

            return new LSL_Rotation(m_host.GetRotationOffset().X, m_host.GetRotationOffset().Y,
                                    m_host.GetRotationOffset().Z, m_host.GetRotationOffset().W);
        }

        public LSL_Vector llGetForce() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();
            LSL_Vector force = new LSL_Vector(0.0, 0.0, 0.0);


            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    Vector3 tmpForce = m_host.ParentEntity.RootChild.GetForce();
                    force.x = tmpForce.X;
                    force.y = tmpForce.Y;
                    force.z = tmpForce.Z;
                }
            }

            return force;
        }

        public LSL_Vector llGetTorque() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            Vector3 torque = m_host.ParentEntity.GetTorque();
            return new LSL_Vector(torque.X, torque.Y, torque.Z);
        }

        public LSL_Vector llGetVel() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();
            Vector3 tmp = m_host.IsAttachment
                              ? m_host.ParentEntity.Scene.GetScenePresence(m_host.AttachedAvatar).Velocity
                              : m_host.Velocity;
            return new LSL_Vector(tmp.X, tmp.Y, tmp.Z);
        }

        public LSL_Vector llGetAccel() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();
            Vector3 tmp = m_host.Acceleration;
            return new LSL_Vector(tmp.X, tmp.Y, tmp.Z);
        }

        public LSL_Vector llGetOmega() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();
            Vector3 tmp = m_host.AngularVelocity;
            return new LSL_Vector(tmp.X, tmp.Y, tmp.Z);
        }

        public LSL_Float llGetObjectMass(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            UUID key = new UUID();
            if (UUID.TryParse(id, out key)) {
                try {
                    ISceneChildEntity obj = World.GetSceneObjectPart(key);
                    if (obj != null)
                        return obj.GetMass();
                    // the object is null so the key is for an avatar
                    IScenePresence avatar = World.GetScenePresence(key);
                    if (avatar != null) {
                        if (avatar.IsChildAgent)
                            // reference http://www.lslwiki.net/lslwiki/wakka.php?wakka=llGetObjectMass
                            // child agents have a mass of 1.0
                            return 1;

                        return avatar.PhysicsActor.Mass;
                    }
                } catch (KeyNotFoundException) {
                    return 0; // The Object/Agent not in the region so just return zero
                }
            }
            return 0;
        }

        public LSL_Float llGetMassMKS() {
            return llGetMass() * 100;
        }

        public LSL_Float llGetMass() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();
            if (m_host.IsAttachment) {
                IScenePresence SP = m_host.ParentEntity.Scene.GetScenePresence(m_host.OwnerID);
                return SP != null ? SP.PhysicsActor.Mass : 0.0;
            }
            return m_host.GetMass();
        }

        public LSL_Vector llGetCenterOfMass() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            Vector3 center = m_host.GetGeometricCenter();
            return new LSL_Vector(center.X, center.Y, center.Z);
        }

        /// <summary>
        ///     A partial implementation.
        ///     http://lslwiki.net/lslwiki/wakka.php?wakka=llGetBoundingBox
        ///     So far only valid for standing/flying/ground sitting avatars and single prim objects.
        ///     If the object has multiple prims and/or a sitting avatar then the bounding
        ///     box is for the root prim only.
        /// </summary>
        public LSL_List llGetBoundingBox(string obj) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            UUID objID = UUID.Zero;
            LSL_List result = new LSL_List();
            if (!UUID.TryParse(obj, out objID)) {
                result.Add(new LSL_Vector());
                result.Add(new LSL_Vector());
                return result;
            }
            IScenePresence presence = World.GetScenePresence(objID);
            if (presence != null) {
                if (presence.ParentID == UUID.Zero) // not sat on an object
                {
                    LSL_Vector lower = new LSL_Vector();
                    LSL_Vector upper = new LSL_Vector();
                    if (presence.Animator.Animations.ImplicitDefaultAnimation.AnimID
                        == AnimationSet.Animations.AnimsUUID["SIT_GROUND_CONSTRAINED"]) {
                        // This is for ground sitting avatars
                        IAvatarAppearanceModule appearance = presence.RequestModuleInterface<IAvatarAppearanceModule>();
                        if (appearance != null) {
                            float height = appearance.Appearance.AvatarHeight / 2.66666667f;
                            lower = new LSL_Vector(-0.3375f, -0.45f, height * -1.0f);
                            upper = new LSL_Vector(0.3375f, 0.45f, 0.0f);
                        }
                    } else {
                        // This is for standing/flying avatars
                        IAvatarAppearanceModule appearance = presence.RequestModuleInterface<IAvatarAppearanceModule>();
                        if (appearance != null) {
                            float height = appearance.Appearance.AvatarHeight / 2.0f;
                            lower = new LSL_Vector(-0.225f, -0.3f, height * -1.0f);
                            upper = new LSL_Vector(0.225f, 0.3f, height + 0.05f);
                        }
                    }
                    result.Add(lower);
                    result.Add(upper);
                    return result;
                }
                // sitting on an object so we need the bounding box of that
                // which should include the avatar so set the UUID to the
                // UUID of the object the avatar is sat on and allow it to fall through
                // to processing an object
                ISceneChildEntity p = World.GetSceneObjectPart(presence.ParentID);
                objID = p.UUID;
            }
            ISceneChildEntity part = World.GetSceneObjectPart(objID);
            // Currently only works for single prims without a sitting avatar
            if (part != null) {
                Vector3 halfSize = part.Scale * 0.5f;
                LSL_Vector lower = new LSL_Vector(halfSize.X * -1.0f, halfSize.Y * -1.0f, halfSize.Z * -1.0f);
                LSL_Vector upper = new LSL_Vector(halfSize.X, halfSize.Y, halfSize.Z);
                result.Add(lower);
                result.Add(upper);
                return result;
            }

            // Not found so return empty values
            result.Add(new LSL_Vector());
            result.Add(new LSL_Vector());
            return result;
        }

        public LSL_Vector llGetGeometricCenter() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            Vector3 MinPos = new Vector3(100000, 100000, 100000);
            Vector3 MaxPos = new Vector3(-100000, -100000, -100000);
            foreach (ISceneChildEntity child in m_host.ParentEntity.ChildrenEntities()) {
                Vector3 tmp = child.AbsolutePosition;
                if (tmp.X < MinPos.X)
                    MinPos.X = tmp.X;
                if (tmp.Y < MinPos.Y)
                    MinPos.Y = tmp.Y;
                if (tmp.Z < MinPos.Z)
                    MinPos.Z = tmp.Z;

                if (tmp.X > MaxPos.X)
                    MaxPos.X = tmp.X;
                if (tmp.Y > MaxPos.Y)
                    MaxPos.Y = tmp.Y;
                if (tmp.Z > MaxPos.Z)
                    MaxPos.Z = tmp.Z;
            }
            Vector3 GroupAvg = ((MaxPos + MinPos) / 2);
            return new LSL_Vector(GroupAvg.X, GroupAvg.Y, GroupAvg.Z);

            //Just plain wrong!
            //return new LSL_Vector(m_host.GetGeometricCenter().X, m_host.GetGeometricCenter().Y, m_host.GetGeometr).Z);
        }

        public LSL_List llGetPhysicsMaterial() {
            var result = new LSL_List();

            result.Add(new LSL_Float(m_host.GravityMultiplier));
            result.Add(new LSL_Float(m_host.Restitution));
            result.Add(new LSL_Float(m_host.Friction));
            result.Add(new LSL_Float(m_host.Density));

            return result;
        }

        public LSL_Float llGetEnergy() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            return 1.0f;
        }

        public void llSetTorque(LSL_Vector torque, int local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.SetAngularImpulse(new Vector3((float)torque.x, (float)torque.y, (float)torque.z), local != 0);
        }

        public void llSetForceAndTorque(LSL_Vector force, LSL_Vector torque, int local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            llSetForce(force, local);
            llSetTorque(torque, local);
        }

        public void llSetVelocity(LSL_Vector force, LSL_Integer local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            Vector3 velocity = new Vector3((float)force.x, (float)force.y, (float)force.z);
            if (local == 1) {
                Quaternion grot = m_host.GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = velocity;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                velocity = newimpulse;
            }

            if (m_host.ParentEntity.RootChild.PhysActor != null)
                m_host.ParentEntity.RootChild.PhysActor.Velocity = velocity;
        }

        public void llSetAngularVelocity(LSL_Vector force, LSL_Integer local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            Vector3 rotvelocity = new Vector3((float)force.x, (float)force.y, (float)force.z);
            if (local == 1) {
                Quaternion grot = m_host.GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = rotvelocity;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                rotvelocity = newimpulse;
            }

            if (m_host.ParentEntity.RootChild.PhysActor != null)
                m_host.ParentEntity.RootChild.PhysActor.RotationalVelocity = rotvelocity;
        }

        public void llSetForce(LSL_Vector force, int local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    if (local != 0)
                        force *= llGetRot();

                    m_host.ParentEntity.RootChild.SetForce(new Vector3((float)force.x, (float)force.y, (float)force.z));
                }
            }
        }



    }
}
