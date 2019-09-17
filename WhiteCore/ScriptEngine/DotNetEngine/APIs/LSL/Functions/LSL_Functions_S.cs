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
        public void llSetScriptState(string name, int run) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            // These functions are supposed to be robust,
            // so get the state one step at a time.
            UUID item;
            if ((item = ScriptByName(name)) != UUID.Zero) {
                m_ScriptEngine.SetScriptRunningState(item, run == 1);
            } else {
                Error("llSetScriptState", "Can't find script '" + name + "'");
            }
        }

        public void llSay(int channelID, object m_text) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            string text = m_text.ToString();

            if (m_scriptConsoleChannelEnabled && (channelID == m_scriptConsoleChannel)) {
                MainConsole.Instance.Debug(text);
            } else {
                if (text.Length > 1023)
                    text = text.Substring(0, 1023);

                IChatModule chatModule = World.RequestModuleInterface<IChatModule>();
                if (chatModule != null)
                    chatModule.SimChat(text, ChatTypeEnum.Say, channelID,
                                       m_host.ParentEntity.RootChild.AbsolutePosition, m_host.Name, m_host.UUID, false,
                                       World);

                if (m_comms != null)
                    m_comms.DeliverMessage(ChatTypeEnum.Say, channelID, m_host.Name, m_host.UUID, text);
            }
        }

        public void llShout(int channelID, string text) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            IChatModule chatModule = World.RequestModuleInterface<IChatModule>();
            if (chatModule != null)
                chatModule.SimChat(text, ChatTypeEnum.Shout, channelID,
                                   m_host.ParentEntity.RootChild.AbsolutePosition, m_host.Name, m_host.UUID, true, World);

            if (m_comms != null)
                m_comms.DeliverMessage(ChatTypeEnum.Shout, channelID, m_host.Name, m_host.UUID, text);
        }


        public void llSensor(string name, string id, int type, double range, double arc) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            UUID keyID = UUID.Zero;
            UUID.TryParse(id, out keyID);
            SensorRepeatPlugin sensorPlugin = (SensorRepeatPlugin)m_ScriptEngine.GetScriptPlugin("SensorRepeat");
            sensorPlugin.SenseOnce(m_host.UUID, m_itemID, name, keyID, type, range, arc, m_host);
        }

        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            UUID keyID = UUID.Zero;
            UUID.TryParse(id, out keyID);

            SensorRepeatPlugin sensorPlugin = (SensorRepeatPlugin)m_ScriptEngine.GetScriptPlugin("SensorRepeat");
            sensorPlugin.SetSenseRepeatEvent(m_host.UUID, m_itemID, name, keyID, type, range, arc, rate, m_host);
        }

        public void llSensorRemove() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            SensorRepeatPlugin sensorPlugin = (SensorRepeatPlugin)m_ScriptEngine.GetScriptPlugin("SensorRepeat");
            sensorPlugin.RemoveScript(m_host.UUID, m_itemID);
        }

        public void llSetStatus(int status, int value) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            int statusrotationaxis = 0;

            if ((status & ScriptBaseClass.STATUS_PHYSICS) == ScriptBaseClass.STATUS_PHYSICS) {
                if (value != 0) {
                    ISceneEntity group = m_host.ParentEntity;
                    if (group == null)
                        return;

                    bool allow = !(from part in @group.ChildrenEntities()
                                   let WSModule = @group.Scene.RequestModuleInterface<IOpenRegionSettingsModule>()
                                   where WSModule != null && !FloatAlmostEqual(WSModule.MaximumPhysPrimScale, -1)
                                   let tmp = part.Scale
                                   where
                                       tmp.X > WSModule.MaximumPhysPrimScale || tmp.Y > WSModule.MaximumPhysPrimScale ||
                                       tmp.Z > WSModule.MaximumPhysPrimScale
                                   select WSModule).Any();

                    if (!allow)
                        return;
                    m_host.ParentEntity.ScriptSetPhysicsStatus(true);
                } else {
                    m_host.ParentEntity.ScriptSetPhysicsStatus(false);
                }
            }

            if ((status & ScriptBaseClass.STATUS_PHANTOM) == ScriptBaseClass.STATUS_PHANTOM) {
                m_host.ScriptSetPhantomStatus(value != 0);
            }

            if ((status & ScriptBaseClass.STATUS_CAST_SHADOWS) == ScriptBaseClass.STATUS_CAST_SHADOWS) {
                m_host.AddFlag(PrimFlags.CastShadows);
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_X) == ScriptBaseClass.STATUS_ROTATE_X) {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_X;
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_Y) == ScriptBaseClass.STATUS_ROTATE_Y) {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_Y;
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_Z) == ScriptBaseClass.STATUS_ROTATE_Z) {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_Z;
            }

            if ((status & ScriptBaseClass.STATUS_BLOCK_GRAB) == ScriptBaseClass.STATUS_BLOCK_GRAB) {
                m_host.SetBlockGrab(value != 0, false);
            }

            if ((status & ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT) == ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT) {
                m_host.SetBlockGrab(value != 0, true);
            }

            if ((status & ScriptBaseClass.STATUS_DIE_AT_EDGE) == ScriptBaseClass.STATUS_DIE_AT_EDGE) {
                m_host.SetDieAtEdge(value != 0);
            }

            if ((status & ScriptBaseClass.STATUS_RETURN_AT_EDGE) == ScriptBaseClass.STATUS_RETURN_AT_EDGE) {
                m_host.SetReturnAtEdge(value != 0);
            }

            if ((status & ScriptBaseClass.STATUS_SANDBOX) == ScriptBaseClass.STATUS_SANDBOX) {
                m_host.SetStatusSandbox(value != 0);
            }

            if (statusrotationaxis != 0) {
                m_host.SetAxisRotation(statusrotationaxis, value);
            }
        }

        public void llSetScale(LSL_Vector scale) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            SetScale(m_host, scale);
        }

        public void llSetClickAction(int action) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.ClickAction = (byte)action;
            m_host.ScheduleUpdate(PrimUpdateFlags.FindBest);
        }

        public void llSetColor(LSL_Vector color, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.SetFaceColor(new Vector3((float)color.x, (float)color.y, (float)color.z), face);
        }

        public void llSetAlpha(double alpha, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            SetAlpha(m_host, alpha, face);
        }


        public void llSetLinkAlpha(int linknumber, double alpha, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            List<ISceneChildEntity> parts = GetLinkParts(linknumber);

            foreach (ISceneChildEntity part in parts)
                SetAlpha(part, alpha, face);
        }

        public DateTime llSetTexture(string texture, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            SetTexture(m_host, texture, face);
            return PScriptSleep(m_sleepMsOnSetTexture);
        }

        public DateTime llSetLinkTexture(int linknumber, string texture, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;


            List<ISceneChildEntity> parts = GetLinkParts(linknumber);

            foreach (ISceneChildEntity part in parts)
                SetTexture(part, texture, face);

            return PScriptSleep(m_sleepMsOnSetLinkTexture);
        }

        public DateTime llScaleTexture(double u, double v, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;


            ScaleTexture(m_host, u, v, face);
            return PScriptSleep(m_sleepMsOnScaleTexture);
        }

        public DateTime llSetPos(LSL_Vector pos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;


            SetPos(m_host, pos, true);

            return PScriptSleep(m_sleepMsOnSetPos);
        }

        public LSL_Integer llSetRegionPos(LSL_Vector pos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return ScriptBaseClass.FALSE;


            SetPos(m_host, pos, false);

            return ScriptBaseClass.TRUE;
        }

        public DateTime llSetRot(LSL_Rotation rot) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;


            // try to let this work as in SL...
            SetLinkRot(m_host, rot);
            return PScriptSleep(m_sleepMsOnSetRot);
        }

        public DateTime llSetLocalRot(LSL_Rotation rot) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            SetRot(m_host, Rot2Quaternion(rot));
            return PScriptSleep(m_sleepMsOnSetLocalRot);
        }




        public void llStopLookAt() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.StopLookAt();
        }

        public void llSetTimerEvent(double sec) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            if (!FloatAlmostEqual(sec, 0.0) && sec < m_MinTimerInterval)
                sec = m_MinTimerInterval;

            // Setting timer repeat
            TimerPlugin timerPlugin = (TimerPlugin)m_ScriptEngine.GetScriptPlugin("Timer");
            timerPlugin.SetTimerEvent(m_host.UUID, m_itemID, sec);
        }

        public virtual DateTime llSleep(double sec) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            return PScriptSleep((int)(sec * 1000));
        }

        public void llSetBuoyancy(double buoyancy) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    m_host.ParentEntity.RootChild.SetBuoyancy((float)buoyancy);
                }
            }
        }

        /// <summary>
        ///     Attempt to clamp the object on the Z axis at the given height over tau seconds.
        /// </summary>
        /// <param name="height">Height to hover.  Height of zero disables hover.</param>
        /// <param name="water">False if height is calculated just from ground, otherwise uses ground or water depending on whichever is higher</param>
        /// <param name="tau">Number of seconds over which to reach target</param>
        public void llSetHoverHeight(double height, int water, double tau) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.PhysActor != null) {
                PIDHoverType hoverType = PIDHoverType.Ground;
                if (water != 0) {
                    hoverType = PIDHoverType.GroundAndWater;
                }

                m_host.SetHoverHeight((float)height, hoverType, (float)tau);
            }
        }

        public void llStopHover() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.PhysActor != null) {
                m_host.SetHoverHeight(0f, PIDHoverType.Ground, 0f);
            }
        }


        public LSL_Integer llStringLength(string str) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            if (str.Length > 0) {
                return str.Length;
            }
            return 0;
        }



        public void llSetLinkColor(int linknumber, LSL_Vector color, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            List<ISceneChildEntity> parts = GetLinkParts(linknumber);

            foreach (ISceneChildEntity part in parts)
                part.SetFaceColor(new Vector3((float)color.x, (float)color.y, (float)color.z), face);
        }

        public void llSetText(string text, LSL_Vector color, LSL_Float alpha) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            Vector3 av3 = new Vector3(Util.Clip((float)color.x, 0.0f, 1.0f),
                                      Util.Clip((float)color.y, 0.0f, 1.0f),
                                      Util.Clip((float)color.z, 0.0f, 1.0f));
            m_host.SetText(text.Length > 254 ? text.Remove(254) : text, av3, Util.Clip((float)alpha, 0.0f, 1.0f));
            //m_host.ParentGroup.HasGroupChanged = true;
            //m_host.ParentGroup.ScheduleGroupForFul;
        }

        public void llSetDamage(double damage) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.ParentEntity.Damage = (float)damage;

            ICombatModule combatModule = World.RequestModuleInterface<ICombatModule>();
            if (combatModule != null)
                combatModule.AddDamageToPrim(m_host.ParentEntity);
        }

        public LSL_Integer llSubStringIndex(string source, string pattern) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            return source.IndexOf(pattern, StringComparison.Ordinal);
        }

        public void llSetObjectName(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.Name = name ?? string.Empty;
        }

        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            SetTextureAnim(m_host, mode, face, sizex, sizey, start, length, rate);
        }

        public void llSetLinkTextureAnim(int linknumber, int mode, int face, int sizex, int sizey, double start,
                                         double length, double rate) {
            List<ISceneChildEntity> parts = GetLinkParts(linknumber);

            foreach (var part in parts) {
                SetTextureAnim(part, mode, face, sizex, sizey, start, length, rate);
            }
        }

        public LSL_Integer llSameGroup(string agent) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            UUID agentId = new UUID();
            if (!UUID.TryParse(agent, out agentId))
                return new LSL_Integer(0);
            IScenePresence presence = World.GetScenePresence(agentId);
            if (presence == null || presence.IsChildAgent) // Return flase for child agents
                return new LSL_Integer(0);
            IClientAPI client = presence.ControllingClient;
            if (m_host.GroupID == client.ActiveGroupId)
                return new LSL_Integer(1);
            return new LSL_Integer(0);
        }

        public LSL_Integer llSetMemoryLimit(LSL_Integer limit) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            // Make scripts designed for Mono happy
            return 65536;
        }

        public void llScriptProfiler(LSL_Integer profilerFlags) {
            //TODO: We don't support this, notted
        }

        public void llSetVehicleType(int type) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    m_host.ParentEntity.RootChild.SetVehicleType(type);
                }
            }
        }

        public void llSetVehicleFloatParam(int param, LSL_Float value) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    m_host.ParentEntity.RootChild.SetVehicleFloatParam(param, (float)value);
                }
            }
        }

        public void llSetVehicleVectorParam(int param, LSL_Vector vec) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    m_host.ParentEntity.RootChild.SetVehicleVectorParam(param,
                                                                        new Vector3((float)vec.x, (float)vec.y,
                                                                                    (float)vec.z));
                }
            }
        }

        public void llSetVehicleRotationParam(int param, LSL_Rotation rot) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    m_host.ParentEntity.RootChild.SetVehicleRotationParam(param, Rot2Quaternion(rot));
                }
            }
        }

        public void llSetVehicleFlags(int flags) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    m_host.ParentEntity.RootChild.SetVehicleFlags(flags, false);
                }
            }
        }

        public void llSitTarget(LSL_Vector offset, LSL_Rotation rot) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            SitTarget(m_host, offset, rot);
        }

        public void llSetTouchText(string text) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.TouchName = text;
        }

        public void llSetSitText(string text) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.SitName = text;
        }

        public void llSetLinkCamera(LSL_Integer link, LSL_Vector eye, LSL_Vector at) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            List<ISceneChildEntity> entities = GetLinkParts(link);
            if (entities.Count > 0) {
                entities[0].CameraEyeOffset = new Vector3((float)eye.x, (float)eye.y, (float)eye.z);
                entities[0].CameraAtOffset = new Vector3((float)at.x, (float)at.y, (float)at.z);
            }
        }

        public void llSetCameraEyeOffset(LSL_Vector offset) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.CameraEyeOffset = new Vector3((float)offset.x, (float)offset.y, (float)offset.z);
        }

        public void llSetCameraAtOffset(LSL_Vector offset) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.CameraAtOffset = new Vector3((float)offset.x, (float)offset.y, (float)offset.z);
        }

        public LSL_Integer llScriptDanger(LSL_Vector pos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            bool result = m_ScriptEngine.PipeEventsForScript(m_host,
                                                             new Vector3((float)pos.x, (float)pos.y, (float)pos.z));
            if (result) {
                return 1;
            }
            return 0;
        }

        public void llSetRemoteScriptAccessPin(int pin) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.ScriptAccessPin = pin;
        }

        public LSL_Key llSendRemoteData(string channel, string dest, int idata, string sdata) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            IXMLRPC xmlrpcMod = World.RequestModuleInterface<IXMLRPC>();
            ScriptSleep(m_sleepMsOnSendRemoteData);
            return (xmlrpcMod.SendRemoteData(m_host.UUID, m_itemID, channel, dest, idata, sdata)).ToString();
        }

        public LSL_String llSHA1String(string src) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            return Util.SHA1Hash(src).ToLower();
        }

        public void llSetPrimitiveParams(LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            SetPrimParams(m_host, rules, m_allowOpenSimParams);
            PScriptSleep(m_sleepMsOnSetPrimitiveParams);
        }

        public void llSetLinkPrimitiveParams(int linknumber, LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            List<IEntity> parts = GetLinkPartsAndEntities(linknumber);

            foreach (IEntity part in parts)
                SetPrimParams(part, rules, m_allowOpenSimParams);

            PScriptSleep(m_sleepMsOnSetLinkPrimitiveParams);
        }

        public void llSetLinkPrimitiveParamsFast(int linknumber, LSL_List rules) {
            List<ISceneChildEntity> parts = GetLinkParts(linknumber);

            foreach (ISceneChildEntity part in parts)
                SetPrimParams(part, rules, m_allowOpenSimParams);
        }

        public LSL_String llStringToBase64(string str) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            try {
                byte[] encData_byte;
                encData_byte = Util.UTF8.GetBytes(str);
                string encodedData = Convert.ToBase64String(encData_byte);
                return encodedData;
            } catch {
                Error("llBase64ToString", "Error encoding string");
                return string.Empty;
            }
        }

        public DateTime llSetParcelMusicURL(string url) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                ILandObject land = parcelManagement.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

                if (land == null)
                    return DateTime.Now;

                if (!World.Permissions.CanEditParcel(m_host.OwnerID, land))
                    return DateTime.Now;

                land.SetMusicUrl(url);
            }

            return PScriptSleep(m_sleepMsOnSetParcelMusicURL);
        }

        public void llSetObjectDesc(string desc) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.Description = desc ?? string.Empty;
        }

        public void llSetPhysicsMaterial(LSL_Integer bits, LSL_Float density, LSL_Float friction, LSL_Float restitution,
                                 LSL_Float gravityMultiplier) {
            ObjectFlagUpdatePacket.ExtraPhysicsBlock[] blocks = new ObjectFlagUpdatePacket.ExtraPhysicsBlock[1];
            blocks[0] = new ObjectFlagUpdatePacket.ExtraPhysicsBlock();
            if ((bits & ScriptBaseClass.DENSITY) == ScriptBaseClass.DENSITY)
                m_host.Density = (float)density;
            else
                blocks[0].Density = m_host.Density;

            if ((bits & ScriptBaseClass.FRICTION) == ScriptBaseClass.FRICTION)
                m_host.Friction = (float)friction;
            else
                blocks[0].Friction = m_host.Friction;

            if ((bits & ScriptBaseClass.RESTITUTION) == ScriptBaseClass.RESTITUTION)
                m_host.Restitution = (float)restitution;
            else
                blocks[0].Restitution = m_host.Restitution;

            if ((bits & ScriptBaseClass.GRAVITY_MULTIPLIER) == ScriptBaseClass.GRAVITY_MULTIPLIER)
                m_host.GravityMultiplier = (float)gravityMultiplier;
            else
                blocks[0].GravityMultiplier = m_host.GravityMultiplier;

            bool UsePhysics = ((m_host.Flags & PrimFlags.Physics) != 0);
            bool IsTemporary = ((m_host.Flags & PrimFlags.TemporaryOnRez) != 0);
            bool IsPhantom = ((m_host.Flags & PrimFlags.Phantom) != 0);
            bool IsVolumeDetect = m_host.VolumeDetectActive;
            blocks[0].PhysicsShapeType = m_host.PhysicsType;
            if (m_host.UpdatePrimFlags(UsePhysics, IsTemporary, IsPhantom, IsVolumeDetect, blocks))
                m_host.ParentEntity.RebuildPhysicalRepresentation(true, null);
        }

        public void llSetObjectPermMask(int mask, int value) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            if (m_ScriptEngine.Config.GetBoolean("AllowGodFunctions", false)) {
                if (World.Permissions.CanRunConsoleCommand(m_host.OwnerID)) {
                    if (mask == ScriptBaseClass.MASK_BASE) //0
                    {
                        m_host.BaseMask = (uint)value;
                    } else if (mask == ScriptBaseClass.MASK_OWNER) //1
                      {
                        m_host.OwnerMask = (uint)value;
                    } else if (mask == ScriptBaseClass.MASK_GROUP) //2
                      {
                        m_host.GroupMask = (uint)value;
                    } else if (mask == ScriptBaseClass.MASK_EVERYONE) //3
                      {
                        m_host.EveryoneMask = (uint)value;
                    } else if (mask == ScriptBaseClass.MASK_NEXT) //4
                      {
                        m_host.NextOwnerMask = (uint)value;
                    }
                }
            }
        }

        public void llSetInventoryPermMask(string item, int mask, int value) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_ScriptEngine.Config.GetBoolean("AllowGodFunctions", false)) {
                if (World.Permissions.CanRunConsoleCommand(m_host.OwnerID)) {
                    lock (m_host.TaskInventory) {
                        foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                            if (inv.Value.Name == item) {
                                switch (mask) {
                                    case 0:
                                        inv.Value.BasePermissions = (uint)value;
                                        break;
                                    case 1:
                                        inv.Value.CurrentPermissions = (uint)value;
                                        break;
                                    case 2:
                                        inv.Value.GroupPermissions = (uint)value;
                                        break;
                                    case 3:
                                        inv.Value.EveryonePermissions = (uint)value;
                                        break;
                                    case 4:
                                        inv.Value.NextPermissions = (uint)value;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public LSL_Integer llSetPrimMediaParams(LSL_Integer face, LSL_List rules) {
            PScriptSleep(m_sleepMsOnSetPrimMediaParams);

            // LSL Spec http://wiki.secondlife.com/wiki/LlSetPrimMediaParams says to fail silently if face is invalid
            // Assuming silently fail means sending back STATUS_OK.  Ideally, need to check this.
            // Don't perform the media check directly
            if (face < 0 || face > m_host.GetNumberOfSides() - 1)
                return ScriptBaseClass.STATUS_OK;
            return SetPrimMediaParams(m_host, face, rules);
        }

        public LSL_Integer llSetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules) {
            //PScriptSleep(m_sleepMsOnSetLinkMedia);

            // LSL Spec http://wiki.secondlife.com/wiki/LlSetPrimMediaParams says to fail silently if face is invalid
            // Assuming silently fail means sending back STATUS_OK.  Ideally, need to check this.
            // Don't perform the media check directly
            List<ISceneChildEntity> entities = GetLinkParts(link);
            if (entities.Count == 0 || face < 0 || face > entities[0].GetNumberOfSides() - 1)
                return ScriptBaseClass.STATUS_OK;
            foreach (ISceneChildEntity child in entities)
                SetPrimMediaParams(child, face, rules);
            return ScriptBaseClass.STATUS_OK;
        }


        public void llSetPayPrice(int price, LSL_List quick_pay_buttons) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.ParentEntity.RootChild.PayPrice[0] = price;

            if (quick_pay_buttons.Data.Length > 0)
                m_host.ParentEntity.RootChild.PayPrice[1] = (LSL_Integer)quick_pay_buttons.Data[0];
            else
                m_host.ParentEntity.RootChild.PayPrice[1] = (LSL_Integer)(-2);
            if (quick_pay_buttons.Data.Length > 1)
                m_host.ParentEntity.RootChild.PayPrice[2] = (LSL_Integer)quick_pay_buttons.Data[1];
            else
                m_host.ParentEntity.RootChild.PayPrice[2] = (LSL_Integer)(-2);
            if (quick_pay_buttons.Data.Length > 2)
                m_host.ParentEntity.RootChild.PayPrice[3] = (LSL_Integer)quick_pay_buttons.Data[2];
            else
                m_host.ParentEntity.RootChild.PayPrice[3] = (LSL_Integer)(-2);
            if (quick_pay_buttons.Data.Length > 3)
                m_host.ParentEntity.RootChild.PayPrice[4] = (LSL_Integer)quick_pay_buttons.Data[3];
            else
                m_host.ParentEntity.RootChild.PayPrice[4] = (LSL_Integer)(-2);
        }


        /// <summary>
        ///     The SL implementation does nothing, it is deprecated
        ///     This duplicates SL
        /// </summary>
        public DateTime llSetPrimURL(string url) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;
            Deprecated("llSetPrimURL", "Use llSetPrimMediaParams instead");
            return PScriptSleep(m_sleepMsOnSetPrimURL);
        }


        public void llSetCameraParams(LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            // our key in the object we are in
            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero) return;

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if (objectID == UUID.Zero) return;

            UUID agentID;
            lock (m_host.TaskInventory) {
                // we need the permission first, to know which avatar we want to set the camera for
                agentID = m_host.TaskInventory[invItemID].PermsGranter;

                if (agentID == UUID.Zero) return;
                if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0)
                    return;
            }

            IScenePresence presence = World.GetScenePresence(agentID);

            // we are not interested in child-agents
            if (presence.IsChildAgent) return;

            SortedDictionary<int, float> parameters = new SortedDictionary<int, float>();
            object[] data = rules.Data;
            for (int i = 0; i < data.Length; ++i) {
                int type = Convert.ToInt32(data[i++].ToString());
                if (i >= data.Length) break; // odd number of entries => ignore the last

                // some special cases: Vector parameters are split into 3 float parameters (with type+1, type+2, type+3)
                if (type == ScriptBaseClass.CAMERA_FOCUS ||
                    type == ScriptBaseClass.CAMERA_FOCUS_OFFSET ||
                    type == ScriptBaseClass.CAMERA_POSITION) {
                    LSL_Vector v = (LSL_Vector)data[i];
                    parameters.Add(type + 1, (float)v.x);
                    parameters.Add(type + 2, (float)v.y);
                    parameters.Add(type + 3, (float)v.z);
                } else {
                    if (data[i] is LSL_Float)
                        parameters.Add(type, (float)((LSL_Float)data[i]).value);
                    else if (data[i] is LSL_Integer)
                        parameters.Add(type, ((LSL_Integer)data[i]).value);
                    else parameters.Add(type, Convert.ToSingle(data[i]));
                }
            }
            if (parameters.Count > 0) presence.ControllingClient.SendSetFollowCamProperties(objectID, parameters);
        }


        public LSL_String llStringTrim(string src, int type) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            if (type == (int)ScriptBaseClass.STRING_TRIM_HEAD) {
                return src.TrimStart();
            }
            if (type == (int)ScriptBaseClass.STRING_TRIM_TAIL) {
                return src.TrimEnd();
            }
            if (type == (int)ScriptBaseClass.STRING_TRIM) {
                return src.Trim();
            }
            return src;
        }

        public void llSetKeyframedMotion(LSL_List keyframes, LSL_List options) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            if (!m_host.IsRoot) {
                Error("llSetKeyframedMotion", "Must be used in the root object!");
                return;
            }
            KeyframeAnimation.Data dataType = KeyframeAnimation.Data.Both;
            KeyframeAnimation.Modes currentMode = KeyframeAnimation.Modes.Forward;
            for (int i = 0; i < options.Length; i += 2) {
                LSL_Integer option = options.GetLSLIntegerItem(i);
                LSL_Integer value = options.GetLSLIntegerItem(i + 1);
                if (option == ScriptBaseClass.KFM_COMMAND) {
                    m_host.ParentEntity.AddKeyframedMotion(null, (KeyframeAnimation.Commands)value.value);
                    break; //Its supposed to be the only option in the list
                }
                if (option == ScriptBaseClass.KFM_MODE) {
                    currentMode = (KeyframeAnimation.Modes)value.value;
                } else if (option == ScriptBaseClass.KFM_DATA) {
                    dataType = (KeyframeAnimation.Data)value.value;
                }
            }
            List<Vector3> positions = new List<Vector3>();
            List<Quaternion> rotations = new List<Quaternion>();
            List<float> times = new List<float>();
            for (int i = 0; i < keyframes.Length; i += (dataType == KeyframeAnimation.Data.Both ? 3 : 2)) {
                if (dataType == KeyframeAnimation.Data.Both ||
                    dataType == KeyframeAnimation.Data.Translation) {
                    LSL_Vector pos = keyframes.GetVector3Item(i);
                    positions.Add(pos.ToVector3());
                }
                if (dataType == KeyframeAnimation.Data.Both ||
                    dataType == KeyframeAnimation.Data.Rotation) {
                    LSL_Rotation rot = keyframes.GetQuaternionItem(i + (dataType == KeyframeAnimation.Data.Both ? 1 : 0));
                    Quaternion quat = rot.ToQuaternion();
                    quat.Normalize();
                    rotations.Add(quat);
                }
                LSL_Float time = keyframes.GetLSLFloatItem(i + (dataType == KeyframeAnimation.Data.Both ? 2 : 1));
                times.Add((float)time);
            }
            KeyframeAnimation animation = new KeyframeAnimation
            {
                CurrentMode = currentMode,
                PositionList = positions.ToArray(),
                RotationList = rotations.ToArray(),
                TimeList = times.ToArray(),
                CurrentAnimationPosition = 0,
                InitialPosition = m_host.AbsolutePosition,
                InitialRotation = m_host.GetRotationOffset()
            };
            m_host.ParentEntity.AddKeyframedMotion(animation, KeyframeAnimation.Commands.Play);
        }




    }
}
