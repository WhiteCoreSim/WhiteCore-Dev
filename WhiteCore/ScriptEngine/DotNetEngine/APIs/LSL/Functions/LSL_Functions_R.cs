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
        /// <summary>
        ///     Reset the named script. The script must be present
        ///     in the same prim.
        /// </summary>
        public void llResetScript() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_UrlModule != null) {
                m_UrlModule.ScriptRemoved(m_itemID);
            }
            m_ScriptEngine.ResetScript(m_host.UUID, m_itemID, true);
        }

        public void llResetOtherScript(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            UUID item;
            if ((item = ScriptByName(name)) != UUID.Zero)
                m_ScriptEngine.ResetScript(m_host.UUID, item, false);
            else
                Error("llResetOtherScript", "Can't find script '" + name + "'");
        }


        public void llRegionSay(int channelID, string text) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            if (channelID == 0) //0 isn't normally allowed, so check against a higher threat level
                if (!ScriptProtection.CheckThreatLevel(ThreatLevel.Moderate, "LSL", m_host, "LSL", m_itemID))
                    return;

            IChatModule chatModule = World.RequestModuleInterface<IChatModule>();
            if (chatModule != null)
                chatModule.SimChat(text, ChatTypeEnum.Region, channelID,
                                   m_host.ParentEntity.RootChild.AbsolutePosition, m_host.Name, m_host.UUID, false,
                                   World);

            if (m_comms != null)
                m_comms.DeliverMessage(ChatTypeEnum.Region, channelID, m_host.Name, m_host.UUID, text);
        }

        public void llRegionSayTo(LSL_Key toID, int channelID, string text) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            IChatModule chatModule = World.RequestModuleInterface<IChatModule>();

            if (text.Length > 1023)
                text = text.Substring(0, 1023);
            if (channelID == 0) {
                IScenePresence presence = World.GetScenePresence(UUID.Parse(toID.m_string));
                if (presence != null) {
                    if (chatModule != null)
                        chatModule.TrySendChatMessage(presence, m_host.AbsolutePosition,
                                                      m_host.UUID, m_host.Name, ChatTypeEnum.Say, text,
                                                      ChatSourceType.Object, 10000);
                }
            }

            if (m_comms != null)
                m_comms.DeliverMessage(ChatTypeEnum.Region, channelID, m_host.Name, m_host.UUID,
                                       UUID.Parse(toID.m_string), text);
        }

        public DateTime llRotateTexture(double rotation, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            RotateTexture(m_host, rotation, face);
            return PScriptSleep(m_sleepMsOnRotateTexture);
        }

        public LSL_Integer llRotTarget(LSL_Rotation rot, double error) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            return
                m_host.registerRotTargetWaypoint(
                    new Quaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s), (float)error);
        }

        public void llRotTargetRemove(int number) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.unregisterRotTargetWaypoint(number);
        }

        public void llResetTime() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_timer = Util.GetTimeStampMS();
        }


        public DateTime llRezAtRoot(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param) {
            return llRezPrim(inventory, pos, vel, rot, param, true, true, true, true);
        }

        /// <summary>
        ///     This isn't really an LSL function, just a way to merge llRezAtRoot and llRezObject into one
        /// </summary>
        /// <param name="inventory"></param>
        /// <param name="pos"></param>
        /// <param name="vel"></param>
        /// <param name="rot"></param>
        /// <param name="param"></param>
        /// <param name="isRezAtRoot"></param>
        /// <param name="setDieAtEdge"></param>
        /// <param name="checkPos"></param>
        /// <returns></returns>
        public DateTime llRezPrim(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot,
                                  int param, bool isRezAtRoot, bool setDieAtEdge, bool checkPos) {
            return llRezPrim(inventory, pos, vel, rot, param, isRezAtRoot, false, setDieAtEdge, checkPos);
        }

        /// <summary>
        ///     This isn't really an LSL function, just a way to merge llRezAtRoot and llRezObject into one
        /// </summary>
        /// <param name="inventory"></param>
        /// <param name="pos"></param>
        /// <param name="vel"></param>
        /// <param name="rot"></param>
        /// <param name="param"></param>
        /// <param name="isRezAtRoot"></param>
        /// <param name="doRecoil"></param>
        /// <param name="setDieAtEdge"></param>
        /// <param name="checkPos"></param>
        /// <returns></returns>
        public DateTime llRezPrim(string inventory, LSL_Vector pos, LSL_Vector vel,
                                  LSL_Rotation rot, int param, bool isRezAtRoot, bool doRecoil,
                                  bool setDieAtEdge, bool checkPos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.Low, "llRezPrim", m_host, "LSL", m_itemID))
                return DateTime.Now;

            if (m_ScriptEngine.Config.GetBoolean("AllowllRezObject", true)) {
                if (double.IsNaN(rot.x) || double.IsNaN(rot.y) || double.IsNaN(rot.z) || double.IsNaN(rot.s))
                    return DateTime.Now;
                if (checkPos) {
                    float dist = (float)llVecDist(llGetPos(), pos);

                    if (dist > m_ScriptDistanceFactor * 10.0f)
                        return DateTime.Now;
                }

                TaskInventoryDictionary partInventory = (TaskInventoryDictionary)m_host.TaskInventory.Clone();

                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in partInventory) {
                    if (inv.Value.Name == inventory) {
                        // make sure we're an object.
                        if (inv.Value.InvType != (int)InventoryType.Object) {
                            llSay(0, "Unable to create requested object. Object is missing from database.");
                            return DateTime.Now;
                        }

                        Vector3 llpos = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);
                        Vector3 llvel = new Vector3((float)vel.x, (float)vel.y, (float)vel.z);

                        ISceneEntity new_group = RezObject(m_host, inv.Value, llpos, Rot2Quaternion(rot), llvel, param,
                                                           m_host.UUID, isRezAtRoot);
                        if (new_group == null)
                            continue;

                        new_group.OnFinishedPhysicalRepresentationBuilding +=
                            delegate () {
                                //Do this after the physics engine has built the prim
                                float groupmass = new_group.GetMass();
                                //Recoil to the av
                                if (m_host.IsAttachment &&
                                    doRecoil &&
                                    (new_group.RootChild.Flags & PrimFlags.Physics) == PrimFlags.Physics) {
                                    IScenePresence SP = m_host.ParentEntity.Scene.GetScenePresence(m_host.OwnerID);
                                    if (SP != null) {
                                        //Push the av backwards (For every action, there is an equal, but opposite reaction)
                                        Vector3 impulse = llvel * groupmass;
                                        impulse.X = impulse.X < 1 ? impulse.X : impulse.X > -1 ? impulse.X : -1;
                                        impulse.Y = impulse.Y < 1 ? impulse.Y : impulse.Y > -1 ? impulse.Y : -1;
                                        impulse.Z = impulse.Z < 1 ? impulse.Z : impulse.Z > -1 ? impulse.Z : -1;
                                        SP.PushForce(impulse);
                                    }
                                }
                            };

                        // If there was an unknown error.
                        if (new_group.RootChild == null)
                            continue;

                        // objects rezzed with this method are die_at_edge by default.
                        if (setDieAtEdge)
                            new_group.RootChild.SetDieAtEdge(true);

                        // Variable script delay? (see (http://wiki.secondlife.com/wiki/LSL_Delay)
                        return PScriptSleep(m_sleepMsOnRezAtRoot);
                    }
                }

                llSay(0, "Could not find object " + inventory);
            }
            return DateTime.Now;
        }

        public DateTime llRezObject(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param) {
            return llRezPrim(inventory, pos, vel, rot, param, false, true, true, true);
        }

        public void llRotLookAt(LSL_Rotation target, double strength, double damping) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            Quaternion rot = new Quaternion((float)target.x, (float)target.y, (float)target.z, (float)target.s);
            m_host.RotLookAt(rot, (float)strength, (float)damping);
        }

        public void llReleaseControls() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
                    return;
                item = m_host.TaskInventory[InventorySelf()];
            }


            if (item.PermsGranter != UUID.Zero) {
                IScenePresence presence = World.GetScenePresence(item.PermsGranter);

                if (presence != null) {
                    if ((item.PermsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0) {
                        // Unregister controls from Presence
                        IScriptControllerModule m = presence.RequestModuleInterface<IScriptControllerModule>();
                        if (m != null)
                            m.UnRegisterControlEventsToScript(m_localID, m_itemID);
                        // Remove Take Control permission.
                        item.PermsMask &= ~ScriptBaseClass.PERMISSION_TAKE_CONTROLS;
                    }
                }
            }
        }

        public void llReleaseURL(string url) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_UrlModule != null)
                m_UrlModule.ReleaseURL(url);
        }

        public void llReleaseCamera(string avatar) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            Deprecated("llReleaseCamera", "Use llClearCameraParams instead");
        }

        public void llRequestPermissions(string agent, int perm) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            UUID agentID = new UUID();

            if (!UUID.TryParse(agent, out agentID))
                return;

            UUID invItemID = InventorySelf();

            if (invItemID == UUID.Zero)
                return; // Not in a prim? How??

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                item = m_host.TaskInventory[invItemID];
            }

            if (agentID == UUID.Zero || perm == 0) // Releasing permissions
            {
                llReleaseControls();

                item.PermsGranter = UUID.Zero;
                item.PermsMask = 0;

                m_ScriptEngine.PostScriptEvent(
                    m_itemID,
                    m_host.UUID,
                    new EventParams("run_time_permissions",
                                    new object[] { new LSL_Integer(0) },
                                    new DetectParams[0]),
                    EventPriority.FirstStart
                );

                return;
            }

            if (item.PermsGranter != agentID || (perm & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) == 0)
                llReleaseControls();


            if (m_host.ParentEntity.IsAttachment && (UUID)agent == m_host.ParentEntity.RootChild.AttachedAvatar) {
                // When attached, certain permissions are implicit if requested from owner
                int implicitPerms = ScriptBaseClass.PERMISSION_TAKE_CONTROLS |
                                    ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION |
                                    ScriptBaseClass.PERMISSION_CONTROL_CAMERA |
                                    ScriptBaseClass.PERMISSION_ATTACH |
                                    ScriptBaseClass.PERMISSION_TRACK_CAMERA;

                if ((perm & (~implicitPerms)) == 0) // Requested only implicit perms
                {
                    lock (m_host.TaskInventory) {
                        m_host.TaskInventory[invItemID].PermsGranter = agentID;
                        m_host.TaskInventory[invItemID].PermsMask = perm;
                    }

                    m_ScriptEngine.PostScriptEvent(
                        m_itemID,
                        m_host.UUID,
                        new EventParams("run_time_permissions",
                                        new object[] { new LSL_Integer(perm) },
                                        new DetectParams[0]),
                        EventPriority.FirstStart
                    );

                    return;
                }
            } else if (m_host.ParentEntity.SitTargetAvatar.Contains(agentID)) // Sitting avatar
              {
                // When agent is sitting, certain permissions are implicit if requested from sitting agent
                int implicitPerms = ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION |
                                    ScriptBaseClass.PERMISSION_CONTROL_CAMERA |
                                    ScriptBaseClass.PERMISSION_TRACK_CAMERA |
                                    ScriptBaseClass.PERMISSION_TAKE_CONTROLS;

                if ((perm & (~implicitPerms)) == 0) // Requested only implicit perms
                {
                    lock (m_host.TaskInventory) {
                        m_host.TaskInventory[invItemID].PermsGranter = agentID;
                        m_host.TaskInventory[invItemID].PermsMask = perm;
                    }

                    m_ScriptEngine.PostScriptEvent(
                        m_itemID,
                        m_host.UUID,
                        new EventParams("run_time_permissions",
                                        new object[] { new LSL_Integer(perm) },
                                        new DetectParams[0]),
                        EventPriority.FirstStart
                    );

                    return;
                }
            }

            IScenePresence presence = World.GetScenePresence(agentID);

            if (presence != null) {
                string ownerName = "";
                IScenePresence ownerPresence = World.GetScenePresence(m_host.ParentEntity.RootChild.OwnerID);
                ownerName = ownerPresence == null ? resolveName(m_host.OwnerID) : ownerPresence.Name;

                // If permissions are being requested from an NPC bot and were not implicitly granted above then
                // auto grant all requested permissions if the script is owned by the NPC or the NPCs owner
                var botMgr = World.RequestModuleInterface<IBotManager>();
                if (botMgr != null && botMgr.IsNpcAgent(agentID)) {
                    if (botMgr.CheckPermission(agentID, m_host.OwnerID)) {

                        lock (m_host.TaskInventory) {
                            m_host.TaskInventory[invItemID].PermsGranter = agentID;
                            m_host.TaskInventory[invItemID].PermsMask = 0;
                        }

                        m_ScriptEngine.PostScriptEvent(
                            m_itemID,
                            m_host.UUID,
                            new EventParams(
                                "run_time_permissions",
                                new object[] { new LSL_Integer(perm) },
                                new DetectParams[0]),
                                EventPriority.FirstStart
                        );
                    }
                    // it is an NPC, exit even if the permissions werent granted above, they are not going to answer
                    // the question!
                    return;
                }

                if (ownerName == string.Empty)
                    ownerName = "(hippos)";

                if (!m_waitingForScriptAnswer) {
                    lock (m_host.TaskInventory) {
                        m_host.TaskInventory[invItemID].PermsGranter = agentID;
                        m_host.TaskInventory[invItemID].PermsMask = 0;
                    }

                    presence.ControllingClient.OnScriptAnswer += handleScriptAnswer;
                    m_waitingForScriptAnswer = true;
                }

                presence.ControllingClient.SendScriptQuestion(
                    m_host.UUID, m_host.ParentEntity.RootChild.Name, ownerName, invItemID, perm);

                return;
            }

            // Requested agent is not in range, refuse perms
            m_ScriptEngine.PostScriptEvent(
                m_itemID,
                m_host.UUID,
                new EventParams("run_time_permissions",
                                new object[] { new LSL_Integer(0) },
                                new DetectParams[0]),
                EventPriority.FirstStart
         );
        }

        public void llRemoveInventory(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            lock (m_host.TaskInventory) {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values) {
                    if (item.Name == name) {
                        if (item.ItemID == m_itemID)
                            throw new ScriptDeleteException();
                        m_host.Inventory.RemoveInventoryItem(item.ItemID);
                        return;
                    }
                }
            }
        }

        public LSL_Key llRequestAgentData(string id, int data) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            UUID uuid = (UUID)id;
            UserInfo pinfo = null;
            UserAccount userAcct;

            UserInfoCacheEntry ce;
            if (!m_userInfoCache.TryGetValue(uuid, out ce)) {
                userAcct = World.UserAccountService.GetUserAccount(World.RegionInfo.AllScopeIDs, uuid);
                if (!userAcct.Valid) {
                    m_userInfoCache[uuid] = null; // Cache negative
                    return UUID.Zero.ToString();
                }

                ce = new UserInfoCacheEntry { time = Util.EnvironmentTickCount(), account = userAcct };
                pinfo = World.RequestModuleInterface<IAgentInfoService>().GetUserInfo(uuid.ToString());
                ce.pinfo = pinfo;
                m_userInfoCache[uuid] = ce;
            } else {
                if (ce == null) {
                    return UUID.Zero.ToString();
                }
                userAcct = ce.account;
                pinfo = ce.pinfo;
            }

            if (Util.EnvironmentTickCount() < ce.time || (Util.EnvironmentTickCount() - ce.time) >= 20000) {
                ce.time = Util.EnvironmentTickCount();
                ce.pinfo = World.RequestModuleInterface<IAgentInfoService>().GetUserInfo(uuid.ToString());
                pinfo = ce.pinfo;
            }

            string reply = string.Empty;

            switch (data) {
                case 1: // DATA_ONLINE (0|1)
                    if (pinfo != null && pinfo.IsOnline)
                        reply = "1";
                    else
                        reply = "0";
                    break;
                case 2: // DATA_NAME (First Last)
                    reply = userAcct.Name;
                    break;
                case 3: // DATA_BORN (YYYY-MM-DD)
                    DateTime born = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                    born = born.AddSeconds(userAcct.Created);
                    reply = born.ToString("yyyy-MM-dd");
                    break;
                case 4: // DATA_RATING (0,0,0,0,0,0)
                    reply = "0,0,0,0,0,0";
                    break;
                case 8: // DATA_PAYINFO (0|1|2|3)
                    if ((userAcct.UserFlags & ScriptBaseClass.PAYMENT_INFO_ON_FILE) ==
                        ScriptBaseClass.PAYMENT_INFO_ON_FILE)
                        reply = ScriptBaseClass.PAYMENT_INFO_ON_FILE.ToString();
                    if ((userAcct.UserFlags & ScriptBaseClass.PAYMENT_INFO_USED) == ScriptBaseClass.PAYMENT_INFO_USED)
                        reply = ScriptBaseClass.PAYMENT_INFO_USED.ToString();
                    reply = "0";
                    break;
                default:
                    return UUID.Zero.ToString(); // Raise no event
            }

            UUID rq = UUID.Random();

            DataserverPlugin dataserverPlugin = (DataserverPlugin)m_ScriptEngine.GetScriptPlugin("Dataserver");
            UUID tid = dataserverPlugin.RegisterRequest(m_host.UUID,
                                                        m_itemID, rq.ToString());

            dataserverPlugin.AddReply(rq.ToString(), reply, 100);

            PScriptSleep(m_sleepMsOnRequestAgentData);
            return tid.ToString();
        }

        public LSL_Key llRequestInventoryData(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            TaskInventoryDictionary itemDictionary = (TaskInventoryDictionary)m_host.TaskInventory.Clone();

            foreach (TaskInventoryItem item in itemDictionary.Values) {
                if (item.Type == 3 && item.Name == name) {
                    UUID rq = UUID.Random();
                    DataserverPlugin dataserverPlugin = (DataserverPlugin)m_ScriptEngine.GetScriptPlugin("Dataserver");

                    UUID tid = dataserverPlugin.RegisterRequest(m_host.UUID,
                                                                m_itemID, rq.ToString());

                    Vector3 region = new Vector3(
                        World.RegionInfo.RegionLocX,
                        World.RegionInfo.RegionLocY,
                        0);

                    World.AssetService.Get(item.AssetID.ToString(), this,
                                           delegate (string i, object sender, AssetBase a) {
                                               if (a != null) {
                                                   AssetLandmark lm = new AssetLandmark(a);

                                                   float rx = (uint)(lm.RegionHandle >> 32);
                                                   float ry = (uint)lm.RegionHandle;
                                                   region = lm.Position + new Vector3(rx, ry, 0) - region;

                                                   string reply = region.ToString();
                                                   dataserverPlugin.AddReply(rq.ToString(),
                                                                             reply, 1000);
                                               }
                                           });

                    PScriptSleep(m_sleepMsOnRequestInventoryData);
                    return tid.ToString();
                }
            }
            PScriptSleep(m_sleepMsOnRequestInventoryData);
            return string.Empty;
        }

        public void llRemoveVehicleFlags(int flags) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity != null) {
                if (!m_host.ParentEntity.IsDeleted) {
                    m_host.ParentEntity.RootChild.SetVehicleFlags(flags, true);
                }
            }
        }

        /// <summary>
        ///     This is a deprecated function so this just replicates the result of
        ///     invoking it in SL
        /// </summary>
        public DateTime llRemoteLoadScript(string target, string name, int running, int start_param) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            // Report an error as it does in SL
            Deprecated("llRemoteLoadScript", "Use llRemoteLoadScriptPin instead");
            return PScriptSleep(m_sleepMsOnRemoteLoadScript);
        }

        public DateTime llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            bool found = false;
            UUID destId = UUID.Zero;
            UUID srcId = UUID.Zero;

            if (!UUID.TryParse(target, out destId)) {
                Error("llRemoteLoadScriptPin", "Can't parse key '" + target + "'");
                return DateTime.Now;
            }

            // target must be a different prim than the one containing the script
            if (m_host.UUID == destId) {
                return DateTime.Now;
            }

            // copy the first script found with this inventory name
            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == name) {
                        // make sure the object is a script
                        if (10 == inv.Value.Type) {
                            found = true;
                            srcId = inv.Key;
                            break;
                        }
                    }
                }
            }

            if (!found) {
                Error("llRemoteLoadScriptPin", "Can't find script '" + name + "'");
                return DateTime.Now;
            }

            // the rest of the permission checks are done in RezScript, so check the pin there as well
            ILLClientInventory inventoryModule = World.RequestModuleInterface<ILLClientInventory>();
            if (inventoryModule != null)
                inventoryModule.RezScript(srcId, m_host, destId, pin, running, start_param);
            // this will cause the delay even if the script pin or permissions were wrong - seems ok
            return PScriptSleep(m_sleepMsOnRemoteLoadScriptPin);
        }

        public DateTime llRemoteDataReply(string channel, string message_id, string sdata, int idata) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IXMLRPC xmlrpcMod = World.RequestModuleInterface<IXMLRPC>();
            xmlrpcMod.RemoteDataReply(channel, message_id, sdata, idata);
            return PScriptSleep(m_sleepMsOnRemoteDataReply);
        }

        public void llRemoteDataSetRegion() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            Deprecated("llRemoteDataSetRegion", "Use llOpenRemoteDataChannel instead");
        }

        public LSL_String llRequestSecureURL() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            if (m_UrlModule != null)
                return m_UrlModule.RequestSecureURL(m_ScriptEngine.ScriptModule, m_host, m_itemID).ToString();
            return UUID.Zero.ToString();
        }


        public LSL_Key llRequestSimulatorData(string simulator, int data) {
            UUID tid = UUID.Zero;

            try {
                if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                    return "";

                string reply = string.Empty;

                GridRegion info = World.RegionInfo.RegionName == simulator
                                      ? new GridRegion(World.RegionInfo)
                                      : World.GridService.GetRegionByName(World.RegionInfo.AllScopeIDs, simulator);


                switch (data) {
                    case 5: // DATA_SIM_POS
                        if (info == null)
                            break;

                        reply = new LSL_Vector(
                            info.RegionLocX,
                            info.RegionLocY,
                            0).ToString();
                        break;
                    case 6: // DATA_SIM_STATUS
                        if (info != null) {
                            reply = (info.Flags & (int)RegionFlags.RegionOnline) != 0 ? "up" : "down";
                        }
                        //if() starting
                        //if() stopping
                        //if() crashed
                        else
                            reply = "unknown";
                        break;
                    case 7: // DATA_SIM_RATING
                        if (info == null)
                            break;

                        uint access = Util.ConvertAccessLevelToMaturity(info.Access);
                        if (access == 0)
                            reply = "PG";
                        else if (access == 1)
                            reply = "MATURE";
                        else if (access == 2)
                            reply = "ADULT";
                        else
                            reply = "UNKNOWN";
                        break;
                    case 128:
                        try {
                            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.High, "llRequestSimulatorData", m_host,
                                                                   "LSL", m_itemID))
                                return "";

                            reply = "WhiteCore";
                        } catch {
                            reply = "";
                        }
                        break;
                }
                if (reply != "") {
                    UUID rq = UUID.Random();

                    DataserverPlugin dataserverPlugin = (DataserverPlugin)m_ScriptEngine.GetScriptPlugin("Dataserver");

                    tid = dataserverPlugin.RegisterRequest(m_host.UUID, m_itemID, rq.ToString());

                    dataserverPlugin.AddReply(rq.ToString(), reply, 1000);
                }
            } catch {
            }

            PScriptSleep(m_sleepMsOnRequestSimulatorData);
            return tid.ToString();
        }

        public LSL_String llRequestURL() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            if (m_UrlModule != null)
                return m_UrlModule.RequestURL(m_ScriptEngine.ScriptModule, m_host, m_itemID).ToString();
            return UUID.Zero.ToString();
        }


        /// <summary>
        ///     The SL implementation shouts an error, it is deprecated
        ///     This duplicates SL
        /// </summary>
        public DateTime llRefreshPrimURL() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            Deprecated("llRefreshPrimURL");
            return PScriptSleep(m_sleepMsOnRefreshPrimURL);
        }


        public DateTime llRemoveFromLandPassList(string avatar) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                LandData land =
                    parcelManagement.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).LandData;
                if (land.OwnerID == m_host.OwnerID) {
                    UUID key;
                    if (UUID.TryParse(avatar, out key)) {
                        foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList) {
                            if (entry.AgentID == key && entry.Flags == AccessList.Access) {
                                land.ParcelAccessList.Remove(entry);
                                break;
                            }
                        }
                    }
                }
            }
            return PScriptSleep(m_sleepMsOnRemoveFromLandPassList);
        }

        public DateTime llRemoveFromLandBanList(string avatar) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                LandData land =
                    parcelManagement.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).LandData;
                if (land.OwnerID == m_host.OwnerID) {
                    UUID key;
                    if (UUID.TryParse(avatar, out key)) {
                        foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList) {
                            if (entry.AgentID == key && entry.Flags == AccessList.Ban) {
                                land.ParcelAccessList.Remove(entry);
                                break;
                            }
                        }
                    }
                }
            }
            return PScriptSleep(m_sleepMsOnRemoveFromLandBanList);
        }


        public DateTime llResetLandBanList() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                LandData land =
                    parcelManagement.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).LandData;
                if (land.OwnerID == m_host.OwnerID) {
                    foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList) {
                        if (entry.Flags == AccessList.Ban) {
                            land.ParcelAccessList.Remove(entry);
                        }
                    }
                }
            }
            return PScriptSleep(m_sleepMsOnResetLandBanList);
        }

        public DateTime llResetLandPassList() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                LandData land =
                    parcelManagement.GetLandObject(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).LandData;
                if (land.OwnerID == m_host.OwnerID) {
                    foreach (ParcelManager.ParcelAccessEntry entry in land.ParcelAccessList) {
                        if (entry.Flags == AccessList.Access) {
                            land.ParcelAccessList.Remove(entry);
                        }
                    }
                }
            }
            return PScriptSleep(m_sleepMsOnResetLandPassList);
        }


        public LSL_Key llRequestUsername(LSL_Key uuid) {
            UUID userID = UUID.Zero;

            if (!UUID.TryParse(uuid, out userID)) {
                // => complain loudly, as specified by the LSL docs
                Error("llRequestUsername", "Failed to parse uuid for avatar.");

                return UUID.Zero.ToString();
            }

            DataserverPlugin dataserverPlugin = (DataserverPlugin)m_ScriptEngine.GetScriptPlugin("Dataserver");
            UUID tid = dataserverPlugin.RegisterRequest(m_host.UUID, m_itemID, uuid.ToString());

            Util.FireAndForget(delegate {
                string name = "";
                UserAccount userAcct =
                    World.UserAccountService.GetUserAccount(World.RegionInfo.AllScopeIDs, userID);
                name = userAcct.Name;
                dataserverPlugin.AddReply(uuid.ToString(), name, 100);
            });

            PScriptSleep(m_sleepMsOnRequestUserName);
            return tid.ToString();
        }

        public LSL_Key llRequestDisplayName(LSL_Key uuid) {
            UUID userID = UUID.Zero;

            if (!UUID.TryParse(uuid, out userID)) {
                // => complain loudly, as specified by the LSL docs
                Error("llRequestDisplayName", "Failed to parse uuid for avatar.");

                return UUID.Zero.ToString();
            }

            DataserverPlugin dataserverPlugin = (DataserverPlugin)m_ScriptEngine.GetScriptPlugin("Dataserver");
            UUID tid = dataserverPlugin.RegisterRequest(m_host.UUID, m_itemID, uuid.ToString());

            Util.FireAndForget(delegate {
                string name = "";
                IProfileConnector connector =
                    Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>();
                if (connector != null) {
                    IUserProfileInfo info = connector.GetUserProfile(userID);
                    if (info != null)
                        name = info.DisplayName;
                }
                dataserverPlugin.AddReply(uuid.ToString(),
                                          name, 100);
            });

            PScriptSleep(m_sleepMsOnRequestUserName);
            return tid.ToString();
        }

    }
}
