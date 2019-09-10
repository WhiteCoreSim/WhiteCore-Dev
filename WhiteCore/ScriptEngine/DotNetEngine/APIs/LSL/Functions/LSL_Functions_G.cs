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

        public LSL_Integer llGetScriptState(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            UUID item;
            if ((item = ScriptByName(name)) != UUID.Zero) {
                return m_ScriptEngine.GetScriptRunningState(item) ? 1 : 0;
            }

            Error("llGetScriptState", "Can't find script '" + name + "'");

            // If we didn't find it, then it's safe to
            // assume it is not running.

            return 0;
        }

        public LSL_Key llGenerateKey() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Key();

            return UUID.Random().ToString();
        }

        public LSL_Integer llGetStatus(int status) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            if (status == ScriptBaseClass.STATUS_PHYSICS) {
                return (m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) == (uint)PrimFlags.Physics
                           ? new LSL_Integer(1)
                           : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_PHANTOM) {
                return (m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) == (uint)PrimFlags.Phantom
                           ? new LSL_Integer(1)
                           : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_CAST_SHADOWS) {
                if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.CastShadows) == (uint)PrimFlags.CastShadows)
                    return new LSL_Integer(1);
                return new LSL_Integer(0);
            }
            if (status == ScriptBaseClass.STATUS_BLOCK_GRAB) {
                return m_host.GetBlockGrab(false) ? new LSL_Integer(1) : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT) {
                return m_host.GetBlockGrab(true) ? new LSL_Integer(1) : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_DIE_AT_EDGE) {
                return m_host.GetDieAtEdge() ? new LSL_Integer(1) : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_RETURN_AT_EDGE) {
                return m_host.GetReturnAtEdge() ? new LSL_Integer(1) : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_ROTATE_X) {
                return m_host.GetAxisRotation(2) == 2 ? new LSL_Integer(1) : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_ROTATE_Y) {
                return m_host.GetAxisRotation(4) == 4 ? new LSL_Integer(1) : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_ROTATE_Z) {
                return m_host.GetAxisRotation(8) == 8 ? new LSL_Integer(1) : new LSL_Integer(0);
            }

            if (status == ScriptBaseClass.STATUS_SANDBOX) {
                return m_host.GetStatusSandbox() ? new LSL_Integer(1) : new LSL_Integer(0);
            }
            return new LSL_Integer(0);
        }

        public LSL_Vector llGetScale() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();
            Vector3 tmp = m_host.Scale;
            return new LSL_Vector(tmp.X, tmp.Y, tmp.Z);
        }

        public LSL_Float llGetAlpha(int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();


            return GetAlpha(m_host, face);
        }

        public LSL_Vector llGetColor(int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            return GetColor(m_host, face);
        }

        public LSL_String llGetTexture(int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();

            return GetTexture(m_host, face);
        }

        public LSL_Vector llGetPos() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            Vector3 pos = m_host.GetWorldPosition();
            return new LSL_Vector(pos.X, pos.Y, pos.Z);
        }

        public LSL_Vector llGetLocalPos() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();
            return GetLocalPos(m_host);
        }

        public LSL_Float llGetTimeOfDay() // this is not sl compatible see wiki
        {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            return (DateTime.Now.TimeOfDay.TotalMilliseconds / 1000) % (3600 * 4);
        }

        public LSL_Float llGetWallclock() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            return DateTime.Now.TimeOfDay.TotalSeconds;
        }

        public LSL_Float llGetTime() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            double ScriptTime = Util.GetTimeStampMS() - m_timer;
            return (ScriptTime / 1000.0);
        }

        public LSL_Float llGetAndResetTime() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            double now = Util.GetTimeStampMS();
            double ScriptTime = now - m_timer;
            m_timer = now;
            return (ScriptTime / 1000.0);
        }


        /// <summary>
        ///     Return a portion of the designated string bounded by
        ///     inclusive indices (start and end). As usual, the negative
        ///     indices, and the tolerance for out-of-bound values, makes
        ///     this more complicated than it might otherwise seem.
        /// </summary>
        public LSL_String llGetSubString(string src, int start, int end) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();


            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.

            if (start < 0) {
                start = src.Length + start;
            }
            if (end < 0) {
                end = src.Length + end;
            }

            // Conventional substring
            if (start <= end) {
                // Implies both bounds are out-of-range.
                if (end < 0 || start >= src.Length) {
                    return string.Empty;
                }
                // If end is positive, then it directly
                // corresponds to the lengt of the substring
                // needed (plus one of course). BUT, it
                // must be within bounds.
                if (end >= src.Length) {
                    end = src.Length - 1;
                }

                if (start < 0) {
                    return src.Substring(0, end + 1);
                }
                // Both indices are positive
                return src.Substring(start, (end + 1) - start);
            }

            // Inverted substring (end < start)
            // Implies both indices are below the
            // lower bound. In the inverted case, that
            // means the entire string will be returned
            // unchanged.
            if (start < 0) {
                return src;
            }
            // If both indices are greater than the upper
            // bound the result may seem initially counter
            // intuitive.
            if (end >= src.Length) {
                return src;
            }

            if (end < 0) {
                return start < src.Length ? src.Substring(start) : string.Empty;
            }
            if (start < src.Length) {
                return src.Substring(0, end + 1) + src.Substring(start);
            }
            return src.Substring(0, end + 1);
        }


        public LSL_Integer llGiveMoney(string destination, int amount) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();
            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero)
                return 0;


            TaskInventoryItem item = m_host.TaskInventory[invItemID];

            if (item.PermsGranter == UUID.Zero)
                return 0;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_DEBIT) == 0) {
                Error("llGiveMoney", "No permissions to give money");
                return 0;
            }

            UUID toID = new UUID();

            if (!UUID.TryParse(destination, out toID)) {
                Error("llGiveMoney", "Bad key in llGiveMoney");
                return 0;
            }

            IMoneyModule money = World.RequestModuleInterface<IMoneyModule>();

            if (money == null) {
                NotImplemented("llGiveMoney");
                return 0;
            }

            bool result = money.ObjectGiveMoney(
                m_host.ParentEntity.UUID, m_host.Name, m_host.OwnerID, toID, amount);

            if (result)
                return 1;

            return 0;
        }


        public LSL_String llGetOwner() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();


            return m_host.OwnerID.ToString();
        }

        public void llGetNextEmail(string address, string subject) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            IEmailModule emailModule = World.RequestModuleInterface<IEmailModule>();
            if (emailModule == null) {
                Error("llGetNextEmail", "Email module not configured");
                return;
            }

            emailModule.GetNextEmailAsync(
                m_host.UUID,
                address,
                subject,
                email =>
                {
                    if (email == null)
                        return;

                    m_ScriptEngine.PostScriptEvent(
                        m_itemID, m_host.UUID, "email", new object[] {
                            new LSL_String(email.time),
                            new LSL_String(email.sender),
                            new LSL_String(email.subject),
                            new LSL_String(email.message),
                            new LSL_Integer(email.numLeft)
                        }
                    );
                },
                World);
        }

        public LSL_String llGetKey() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            return m_host.UUID.ToString();
        }

        public LSL_Integer llGetStartParameter() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            return m_ScriptEngine.GetStartParameter(m_itemID, m_host.UUID);
        }

        public void llGodLikeRezObject(string inventory, LSL_Vector pos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_ScriptEngine.Config.GetBoolean("AllowGodFunctions", false)) {
                if (World.Permissions.CanRunConsoleCommand(m_host.OwnerID)) {
                    byte[] asset = World.AssetService.GetData(inventory);
                    if (asset == null)
                        return;

                    ISceneEntity group
                        = SceneEntitySerializer.SceneObjectSerializer.FromOriginalXmlFormat(UUID.Zero,
                                                                                            Utils.BytesToString(asset),
                                                                                            World);
                    if (group == null)
                        return;

                    group.IsDeleted = false;
                    foreach (ISceneChildEntity part in group.ChildrenEntities())
                        part.IsLoading = false;

                    group.OwnerID = m_host.OwnerID;

                    group.RootChild.AddFlag(PrimFlags.CreateSelected);
                    // If we're rezzing an attachment then don't ask AddNewSceneObject() to update the client since
                    // we'll be doing that later on.  Scheduling more than one full update during the attachment
                    // process causes some clients to fail to display the attachment properly.
                    World.SceneGraph.AddPrimToScene(group);

                    // if attachment we set it's asset id so object updates can reflect that
                    // if not, we set it's position in world.
                    group.AbsolutePosition = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);

                    IScenePresence SP = World.GetScenePresence(m_host.OwnerID);
                    if (SP != null)
                        group.SetGroup(m_host.GroupID, SP.UUID, false);

                    if (group.RootChild.Shape.PCode == (byte)PCode.Prim)
                        group.ClearPartAttachmentData();

                    // Fire on_rez
                    group.CreateScriptInstances(0, true, StateSource.ScriptedRez, UUID.Zero, false);
                    group.ScheduleGroupUpdate(PrimUpdateFlags.ForcedFullUpdate);
                }
            }
        }

        public LSL_String llGetPermissionsKey() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            lock (m_host.TaskInventory) {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values) {
                    if (item.Type == 10 && item.ItemID == m_itemID) {
                        return item.PermsGranter.ToString();
                    }
                }
            }

            return UUID.Zero.ToString();
        }

        public LSL_Integer llGetPermissions() {
            lock (m_host.TaskInventory) {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values) {
                    if (item.Type == 10 && item.ItemID == m_itemID) {
                        int perms = item.PermsMask;
                        if (m_automaticLinkPermission)
                            perms |= ScriptBaseClass.PERMISSION_CHANGE_LINKS;
                        return perms;
                    }
                }
            }

            return 0;
        }

        public LSL_Integer llGetLinkNumber() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;


            if (m_host.ParentEntity.ChildrenEntities().Count > 1) {
                return m_host.LinkNum;
            }
            return 0;
        }

        public LSL_String llGetLinkKey(int linknum) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            IEntity target = m_host.ParentEntity.GetLinkNumPart(linknum);
            if (target != null) {
                return target.UUID.ToString();
            }
            return UUID.Zero.ToString();
        }

        /// <summary>
        ///     The rules governing the returned name are not simple. The only
        ///     time a blank name is returned is if the target prim has a blank
        ///     name. If no prim with the given link number can be found then
        ///     usually NULL_KEY is returned but there are exceptions.
        ///     In a single unlinked prim, A call with 0 returns the name, all
        ///     other values for link number return NULL_KEY
        ///     In link sets it is more complicated.
        ///     If the script is in the root prim:-
        ///     A zero link number returns NULL_KEY.
        ///     Positive link numbers return the name of the prim, or NULL_KEY
        ///     if a prim does not exist at that position.
        ///     Negative link numbers return the name of the first child prim.
        ///     If the script is in a child prim:-
        ///     Link numbers 0 or 1 return the name of the root prim.
        ///     Positive link numbers return the name of the prim or NULL_KEY
        ///     if a prim does not exist at that position.
        ///     Negative numbers return the name of the root prim.
        ///     References
        ///     http://lslwiki.net/lslwiki/wakka.php?wakka=llGetLinkName
        ///     Mentions NULL_KEY being returned
        ///     http://wiki.secondlife.com/wiki/LlGetLinkName
        ///     Mentions using the LINK_* constants, some of which are negative
        /// </summary>
        public LSL_String llGetLinkName(int linknum) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            // simplest case, this prims link number
            if (linknum == m_host.LinkNum)
                return m_host.Name;

            // Single prim
            if (m_host.LinkNum == 0) {
                if (linknum == 1)
                    return m_host.Name;
                IEntity entity = m_host.ParentEntity.GetLinkNumPart(linknum);
                if (entity != null)
                    return entity.Name;
                return UUID.Zero.ToString();
            }
            // Link set
            IEntity part = null;
            part = m_host.LinkNum == 1
                       ? m_host.ParentEntity.GetLinkNumPart(linknum < 0 ? 2 : linknum)
                       : m_host.ParentEntity.GetLinkNumPart(linknum < 2 ? 1 : linknum);
            if (part != null)
                return part.Name;
            return UUID.Zero.ToString();
        }

        public LSL_Integer llGetInventoryNumber(int type) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            int count = 0;

            lock (m_host.TaskInventory) {
                count += m_host.TaskInventory.Values.Count(item => item.Type == type || type == -1);
            }

            return count;
        }

        public LSL_String llGetInventoryName(int type, int number) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            ArrayList keys = new ArrayList();

            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Type == type || type == -1) {
                        keys.Add(inv.Value.Name);
                    }
                }
            }

            if (keys.Count == 0)
                return string.Empty;

            keys.Sort();
            if (keys.Count > number) {
                return (string)keys[number];
            }
            return string.Empty;
        }

        public DateTime llGiveInventory(string destination, string inventory) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            bool found = false;
            UUID destId = UUID.Zero;
            UUID objId = UUID.Zero;
            int assetType = 0;
            string objName = string.Empty;

            if (!UUID.TryParse(destination, out destId)) {
                Error("llGiveInventory", "Could not parse key " + destination);
                return DateTime.Now;
            }

            // move the first object found with this inventory name
            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == inventory) {
                        found = true;
                        objId = inv.Key;
                        assetType = inv.Value.Type;
                        objName = inv.Value.Name;
                        break;
                    }
                }
            }

            if (!found) {
                Error("llGiveInventory", "Can't find inventory object '" + inventory + "'");
            }

            // check if destination is an avatar
            if (World.GetScenePresence(destId) != null ||
                m_host.ParentEntity.Scene.RequestModuleInterface<IAgentInfoService>().GetUserInfo(destId.ToString()) !=
                null) {
                // destination is an avatar
                InventoryItemBase agentItem = null;
                ILLClientInventory inventoryModule = World.RequestModuleInterface<ILLClientInventory>();
                if (inventoryModule != null)
                    agentItem = inventoryModule.MoveTaskInventoryItemToUserInventory(destId, UUID.Zero, m_host, objId,
                                                                                     false);

                if (agentItem == null)
                    return DateTime.Now;

                byte[] bucket = new byte[17];
                bucket[0] = (byte)assetType;
                byte[] objBytes = agentItem.ID.GetBytes();
                Array.Copy(objBytes, 0, bucket, 1, 16);

                GridInstantMessage msg = new GridInstantMessage()
                {
                    FromAgentID = m_host.UUID,
                    FromAgentName = m_host.Name + ", an object owned by " +
                                                                              resolveName(m_host.OwnerID) + ",",
                    ToAgentID = destId,
                    Dialog = (byte)InstantMessageDialog.InventoryOffered,
                    Message = objName + "'\n'" + m_host.Name + "' is located at " + m_host.AbsolutePosition +
                                                           " in '" + World.RegionInfo.RegionName,
                    SessionID = agentItem.ID,
                    Offline = 1,
                    Position = m_host.AbsolutePosition,
                    BinaryBucket = bucket,
                    RegionID = m_host.ParentEntity.Scene.RegionInfo.RegionID
                };

                if (m_TransferModule != null)
                    m_TransferModule.SendInstantMessage(msg);
            } else {
                // destination is an object
                ILLClientInventory inventoryModule = World.RequestModuleInterface<ILLClientInventory>();
                if (inventoryModule != null)
                    inventoryModule.MoveTaskInventoryItemToObject(destId, m_host, objId);
            }
            return PScriptSleep(m_sleepMsOnGiveInventory);
        }

        public LSL_String llGetScriptName() {
            string result = string.Empty;

            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            lock (m_host.TaskInventory) {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values) {
                    if (item.Type == 10 && item.ItemID == m_itemID) {
                        result = item.Name ?? string.Empty;
                        break;
                    }
                }
            }

            return result;
        }

        public LSL_Integer llGetNumberOfSides() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;


            return GetNumberOfSides(m_host);
        }

        public LSL_String llGetInventoryKey(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();


            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == name) {
                        return (inv.Value.CurrentPermissions &
                                (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify)) ==
                               (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify)
                                   ? inv.Value.AssetID.ToString()
                                   : UUID.Zero.ToString();
                    }
                }
            }

            return UUID.Zero.ToString();
        }

        public LSL_Vector llGetTextureOffset(int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            return GetTextureOffset(m_host, face);
        }

        public LSL_Vector llGetTextureScale(int side) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            Primitive.TextureEntry tex = m_host.Shape.Textures;
            LSL_Vector scale;
            if (side == -1) {
                side = 0;
            }
            scale.x = tex.GetFace((uint)side).RepeatU;
            scale.y = tex.GetFace((uint)side).RepeatV;
            scale.z = 0.0;
            return scale;
        }

        public LSL_Float llGetTextureRot(int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            return GetTextureRot(m_host, face);
        }

        public LSL_String llGetOwnerKey(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            UUID key = new UUID();
            if (UUID.TryParse(id, out key)) {
                try {
                    ISceneChildEntity obj = World.GetSceneObjectPart(key);
                    if (obj == null)
                        return id; // the key is for an agent so just return the key
                    return obj.OwnerID.ToString();
                } catch (KeyNotFoundException) {
                    return id; // The Object/Agent not in the region so just return the key
                }
            }
            return UUID.Zero.ToString();
        }

        public LSL_Integer llGetListLength(LSL_List src) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;


            if (src == new LSL_List(new object[0])) {
                return 0;
            }
            return src.Length;
        }

        public LSL_Integer llGetListEntryType(LSL_List src, int index) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            if (index < 0) {
                index = src.Length + index;
            }
            if (index >= src.Length) {
                return 0;
            }

            if (src.Data[index] is LSL_Integer || src.Data[index] is int)
                return ScriptBaseClass.TYPE_INTEGER;
            if (src.Data[index] is LSL_Float || src.Data[index] is float || src.Data[index] is double)
                return ScriptBaseClass.TYPE_FLOAT;
            if (src.Data[index] is LSL_String || src.Data[index] is string) {
                UUID tuuid;
                if (UUID.TryParse(src.Data[index].ToString(), out tuuid)) {
                    return ScriptBaseClass.TYPE_KEY;
                }
                return ScriptBaseClass.TYPE_STRING;
            }
            if (src.Data[index] is LSL_Vector)
                return ScriptBaseClass.TYPE_VECTOR;
            if (src.Data[index] is LSL_Rotation)
                return ScriptBaseClass.TYPE_ROTATION;
            if (src.Data[index] is LSL_List)
                return 7; //Extension of LSL by us
            return ScriptBaseClass.TYPE_INVALID;
        }

        public LSL_Integer llGetRegionAgentCount() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            IEntityCountModule entityCountModule = World.RequestModuleInterface<IEntityCountModule>();
            if (entityCountModule != null)
                return new LSL_Integer(entityCountModule.RootAgents);

            return new LSL_Integer(0);
        }

        public LSL_Vector llGetRegionCorner() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            return new LSL_Vector(World.RegionInfo.RegionLocX, World.RegionInfo.RegionLocY, 0);
        }

        public LSL_String llGetObjectName() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            return m_host.Name ?? string.Empty;
        }

        public LSL_String llGetDate() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            DateTime date = DateTime.Now.ToUniversalTime();
            string result = date.ToString("yyyy-MM-dd");
            return result;
        }

        /// <summary>
        ///     Fully implemented
        /// </summary>
        public LSL_Integer llGetAgentInfo(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;


            UUID key = new UUID();
            if (!UUID.TryParse(id, out key))
                return 0;

            int flags = 0;

            IScenePresence agent = World.GetScenePresence(key);
            if (agent == null)
                return 0;

            if (agent.IsChildAgent)
                return 0; // Fail if they are not in the same region

            // note: in OpenSim, sitting seems to cancel AGENT_ALWAYS_RUN, unlike SL
            if (agent.SetAlwaysRun)
                flags |= ScriptBaseClass.AGENT_ALWAYS_RUN;

            IAttachmentsModule attachMod = World.RequestModuleInterface<IAttachmentsModule>();
            if (attachMod != null) {
                ISceneEntity[] att = attachMod.GetAttachmentsForAvatar(agent.UUID);
                if (att.Length > 0) {
                    flags |= ScriptBaseClass.AGENT_ATTACHMENTS;
                    if (att.Where(gobj => gobj != null).Any(gobj => gobj.RootChild.Inventory.ContainsScripts())) {
                        flags |= ScriptBaseClass.AGENT_SCRIPTED;
                    }
                }
            }

            if ((agent.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY) != 0) {
                flags |= ScriptBaseClass.AGENT_FLYING;
                flags |= ScriptBaseClass.AGENT_IN_AIR;
                // flying always implies in-air, even if colliding with e.g. a wall
            }

            if ((agent.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AWAY) != 0)
                flags |= ScriptBaseClass.AGENT_AWAY;

            // seems to get unset, even if in mouselook, when avatar is sitting on a prim???
            if ((agent.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0)
                flags |= ScriptBaseClass.AGENT_MOUSELOOK;

            if ((agent.State & (byte)AgentState.Typing) != 0)
                flags |= ScriptBaseClass.AGENT_TYPING;

            if (agent.IsBusy)
                flags |= ScriptBaseClass.AGENT_BUSY;

            string agentMovementAnimation = agent.Animator.CurrentMovementAnimation;

            if (agentMovementAnimation == "CROUCH")
                flags |= ScriptBaseClass.AGENT_CROUCHING;

            if (agentMovementAnimation == "WALK" || agentMovementAnimation == "CROUCHWALK")
                flags |= ScriptBaseClass.AGENT_WALKING;

            // not colliding implies in air. Note: flying also implies in-air, even if colliding (see above)

            // note: AGENT_IN_AIR and AGENT_WALKING seem to be mutually exclusive states in SL.

            // note: this may need some tweaking when walking downhill. you "fall down" for a brief instant
            // and don't collide when walking downhill, which instantly registers as in-air, briefly. should
            // there be some minimum non-collision threshold time before claiming the avatar is in-air?
            if ((flags & ScriptBaseClass.AGENT_WALKING) == 0 &&
                agent.PhysicsActor != null &&
                !agent.PhysicsActor.IsColliding) {
                flags |= ScriptBaseClass.AGENT_IN_AIR;
            }

            if (agent.ParentID != UUID.Zero) {
                flags |= ScriptBaseClass.AGENT_ON_OBJECT;
                flags |= ScriptBaseClass.AGENT_SITTING;
            }

            if (agent.Animator.Animations.ImplicitDefaultAnimation.AnimID
                == AnimationSet.Animations.AnimsUUID["SIT_GROUND_CONSTRAINED"]) {
                flags |= ScriptBaseClass.AGENT_SITTING;
            }

            return flags;
        }

        public LSL_String llGetAgentLanguage(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            IAgentConnector agentFrontend = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector>();
            if (agentFrontend == null)
                return "en-us";
            IAgentInfo agent = agentFrontend.GetAgent(new UUID(id));
            if (agent == null)
                return "en-us";
            if (agent.LanguageIsPublic)
                return agent.Language;
            return "en-us";
        }

        /// <summary>
        ///     http://wiki.secondlife.com/wiki/LlGetAgentList
        ///     The list of options is currently not used in SL
        ///     scope is one of:-
        ///     AGENT_LIST_REGION - all in the region
        ///     AGENT_LIST_PARCEL - all in the same parcel as the scripted object
        ///     AGENT_LIST_PARCEL_OWNER - all in any parcel owned by the owner of the
        ///     current parcel.
        /// </summary>
        public LSL_List llGetAgentList(LSL_Integer scope, LSL_List options) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            // the constants are 1, 2 and 4 so bits are being set, but you
            // get an error "INVALID_SCOPE" if it is anything but 1, 2 and 4
            bool regionWide = scope == ScriptBaseClass.AGENT_LIST_REGION;
            bool parcelOwned = scope == ScriptBaseClass.AGENT_LIST_PARCEL_OWNER;
            bool parcel = scope == ScriptBaseClass.AGENT_LIST_PARCEL;
            LSL_List result = new LSL_List();

            if (!regionWide && !parcelOwned && !parcel) {
                result.Add("INVALID_SCOPE");
                return result;
            }

            Vector3 pos;
            UUID id = UUID.Zero;

            if (parcel || parcelOwned) {
                pos = m_host.GetWorldPosition();
                IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
                ILandObject land = parcelManagement.GetLandObject(pos.X, pos.Y);
                if (land == null) {
                    id = UUID.Zero;
                } else {
                    if (parcelOwned) {
                        id = land.LandData.OwnerID;
                    } else {
                        id = land.LandData.GlobalID;
                    }
                }
            }

            World.ForEachScenePresence(
                delegate (IScenePresence ssp) {
                    // Gods are not listed in SL
                    if (!ssp.IsDeleted && FloatAlmostEqual(ssp.GodLevel, 0.0) && !ssp.IsChildAgent) {
                        if (!regionWide) {
                            pos = ssp.AbsolutePosition;
                            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
                            ILandObject land = parcelManagement.GetLandObject(pos.X, pos.Y);
                            if (land != null) {
                                if (parcelOwned && land.LandData.OwnerID == id || parcel && land.LandData.GlobalID == id) {
                                    result.Add(ssp.UUID.ToString());
                                }
                            }
                        } else {
                            result.Add(ssp.UUID.ToString());
                        }
                    }
                    // Maximum of 100 results
                    if (result.Length > 99) {
                        return;
                    }
                }
            );

            return result;
        }

        public LSL_String llGetDisplayName(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            UUID key = new UUID();
            if (UUID.TryParse(id, out key)) {
                IScenePresence presence = World.GetScenePresence(key);

                if (presence != null) {
                    IProfileConnector connector = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>();
                    if (connector != null)
                        return connector.GetUserProfile(presence.UUID).DisplayName;
                }
            }
            return string.Empty;
        }

        public LSL_String llGetUsername(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            UUID key = new UUID();
            if (UUID.TryParse(id, out key)) {
                IScenePresence presence = World.GetScenePresence(key);

                if (presence != null)
                    return presence.Name;
            }
            return string.Empty;
        }

        public LSL_String llGetLandOwnerAt(LSL_Vector pos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                ILandObject land = parcelManagement.GetLandObject((float)pos.x, (float)pos.y);
                if (land != null)
                    return land.LandData.OwnerID.ToString();
            }
            return UUID.Zero.ToString();
        }

        /// <summary>
        ///     According to http://lslwiki.net/lslwiki/wakka.php?wakka=llGetAgentSize
        ///     only the height of avatars vary and that says:
        ///     Width (x) and depth (y) are constant. (0.45m and 0.6m respectively).
        /// </summary>
        public LSL_Vector llGetAgentSize(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            IScenePresence avatar = World.GetScenePresence((UUID)id);
            LSL_Vector agentSize;
            if (avatar == null || avatar.IsChildAgent) // Fail if not in the same region
            {
                agentSize = ScriptBaseClass.ZERO_VECTOR;
            } else {
                IAvatarAppearanceModule appearance = avatar.RequestModuleInterface<IAvatarAppearanceModule>();
                agentSize = appearance != null
                                ? new LSL_Vector(0.45, 0.6, appearance.Appearance.AvatarHeight)
                                : ScriptBaseClass.ZERO_VECTOR;
            }
            return agentSize;
        }


        public LSL_Integer llGetAttached() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            return m_host.ParentEntity.RootChild.AttachmentPoint;
        }

        public LSL_List llGetAttachedList(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            IScenePresence av = World.GetScenePresence((UUID)id);

            if (av == null || av.IsDeleted)
                return new LSL_List("NOT FOUND");
            if (av.IsChildAgent || av.IsInTransit)
                return new LSL_List("NOT ON REGION");

            LSL_List AttachmentsList = new LSL_List();

            IAttachmentsModule attachMod = World.RequestModuleInterface<IAttachmentsModule>();
            if (attachMod != null) {
                ISceneEntity[] Attachments = attachMod.GetAttachmentsForAvatar(av.UUID);
                foreach (ISceneEntity Attachment in Attachments) {
                    AttachmentsList.Add(new LSL_Key(Attachment.UUID.ToString()));
                }
            }
            return AttachmentsList;
        }

        public LSL_Integer llGetFreeMemory() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            // Make scripts designed for Mono happy
            return 65536;
        }

        public LSL_Integer llGetMemoryLimit() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            // Make scripts designed for Mono happy
            return 65536;
        }

        public LSL_Integer llGetSPMaxMemory() {
            //TODO: Not implemented!
            return 0;
        }

        public LSL_Integer llGetUsedMemory() {
            //TODO: Not implemented!
            return 0;
        }


        public LSL_String llGetRegionName() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();
            return World.RegionInfo.RegionName;
        }

        public LSL_Float llGetRegionTimeDilation() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            return World.TimeDilation;
        }

        /// <summary>
        ///     Returns the value reported in the client Statistics window
        /// </summary>
        public LSL_Float llGetRegionFPS() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            ISimFrameMonitor reporter = World.RequestModuleInterface<IMonitorModule>().GetMonitor<ISimFrameMonitor>(World);
            if (reporter != null)
                return reporter.LastReportedSimFPS;
            return 0;
        }



        public void llGiveInventoryList(string destination, string category, LSL_List inventory) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            UUID destID;
            if (!UUID.TryParse(destination, out destID))
                return;

            List<UUID> itemList = new List<UUID>();

            foreach (object item in inventory.Data) {
                UUID itemID;
                if (UUID.TryParse(item.ToString(), out itemID)) {
                    itemList.Add(itemID);
                } else {
                    itemID = GetTaskInventoryItem(item.ToString());
                    if (itemID != UUID.Zero)
                        itemList.Add(itemID);
                }
            }

            if (itemList.Count == 0)
                return;
            UUID folderID = UUID.Zero;
            ILLClientInventory inventoryModule = World.RequestModuleInterface<ILLClientInventory>();
            if (inventoryModule != null)
                folderID = inventoryModule.MoveTaskInventoryItemsToUserInventory(destID, category, m_host, itemList);

            if (folderID == UUID.Zero)
                return;

            byte[] bucket = new byte[17];
            bucket[0] = (byte)AssetType.Folder;
            byte[] objBytes = folderID.GetBytes();
            Array.Copy(objBytes, 0, bucket, 1, 16);

            GridInstantMessage msg = new GridInstantMessage()
            {
                FromAgentID = m_host.UUID,
                FromAgentName = m_host.Name + ", an object owned by " +
                                                                         resolveName(m_host.OwnerID) + ",",
                ToAgentID = destID,
                Dialog = (byte)InstantMessageDialog.InventoryOffered,
                Message = category + "\n" + m_host.Name + " is located at " +
                                                                   World.RegionInfo.RegionName + " " +
                                                                   m_host.AbsolutePosition,
                SessionID = folderID,
                Offline = 1,
                Position = m_host.AbsolutePosition,
                BinaryBucket = bucket,
                RegionID = m_host.ParentEntity.Scene.RegionInfo.RegionID
            };

            if (m_TransferModule != null)
                m_TransferModule.SendInstantMessage(msg);
        }

        public LSL_Integer llGetLinkNumberOfSides(int LinkNum) {
            List<ISceneChildEntity> Parts = GetLinkParts(LinkNum);
            int faces = Parts.Sum(part => GetNumberOfSides(part));
            return new LSL_Integer(faces);
        }

        public LSL_List llGetAnimationList(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();


            LSL_List l = new LSL_List();
            IScenePresence av = World.GetScenePresence((UUID)id);
            if (av == null || av.IsChildAgent) // only if in the region
                return l;
            UUID[] anims = av.Animator.GetAnimationArray();
            foreach (UUID foo in anims)
                l.Add(new LSL_Key(foo.ToString()));
            return l;
        }

        public LSL_Vector llGetRootPosition() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            return new LSL_Vector(m_host.ParentEntity.AbsolutePosition.X, m_host.ParentEntity.AbsolutePosition.Y,
                                  m_host.ParentEntity.AbsolutePosition.Z);
        }

        /// <summary>
        ///     http://lslwiki.net/lslwiki/wakka.php?wakka=llGetRot
        ///     http://lslwiki.net/lslwiki/wakka.php?wakka=ChildRotation
        ///     Also tested in sl in regards to the behaviour in attachments/mouselook
        ///     In the root prim:-
        ///     Returns the object rotation if not attached
        ///     Returns the avatars rotation if attached
        ///     Returns the camera rotation if attached and the avatar is in mouselook
        /// </summary>
        public LSL_Rotation llGetRootRotation() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation();

            Quaternion q;
            if (m_host.ParentEntity.RootChild.AttachmentPoint != 0) {
                IScenePresence avatar = World.GetScenePresence(m_host.AttachedAvatar);
                if (avatar != null)
                    q = (avatar.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0
                            ? avatar.CameraRotation
                            : avatar.Rotation;
                else
                    q = m_host.ParentEntity.GroupRotation; // Likely never get here but just in case
            } else
                q = m_host.ParentEntity.GroupRotation; // just the group rotation
            return new LSL_Rotation(q.X, q.Y, q.Z, q.W);
        }

        public LSL_String llGetObjectDesc() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            return m_host.Description ?? string.Empty;
        }

        public LSL_String llGetCreator() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            return m_host.CreatorID.ToString();
        }

        public LSL_String llGetTimestamp() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            return DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        }

        public LSL_Integer llGetNumberOfPrims() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            int avatarCount = m_host.ParentEntity.SitTargetAvatar.Count;

            return m_host.ParentEntity.PrimCount + avatarCount;
        }



        public LSL_List llGetPrimitiveParams(LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            return GetLinkPrimitiveParams(m_host, rules, m_allowOpenSimParams);
        }

        public LSL_List llGetLinkPrimitiveParams(int linknumber, LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();


            List<ISceneChildEntity> parts = GetLinkParts(linknumber);

            LSL_List res = new LSL_List();

            return parts.Select(part => GetLinkPrimitiveParams(part, rules, m_allowOpenSimParams))
                        .Aggregate(res, (current, partRes) => current + partRes);
        }


        public LSL_Float llGetGMTclock() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float();

            return DateTime.UtcNow.TimeOfDay.TotalSeconds;
        }

        public LSL_String llGetHTTPHeader(LSL_Key request_id, string header) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            if (m_UrlModule != null)
                return m_UrlModule.GetHttpHeader(request_id, header);
            return string.Empty;
        }


        public LSL_String llGetSimulatorHostname() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            IUrlModule UrlModule = World.RequestModuleInterface<IUrlModule>();
            return UrlModule.ExternalHostNameForLSL;
        }

        public LSL_Integer llGetObjectPermMask(int mask) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();


            int permmask = 0;

            if (mask == ScriptBaseClass.MASK_BASE) //0
            {
                permmask = (int)m_host.BaseMask;
            } else if (mask == ScriptBaseClass.MASK_OWNER) //1
              {
                permmask = (int)m_host.OwnerMask;
            } else if (mask == ScriptBaseClass.MASK_GROUP) //2
              {
                permmask = (int)m_host.GroupMask;
            } else if (mask == ScriptBaseClass.MASK_EVERYONE) //3
              {
                permmask = (int)m_host.EveryoneMask;
            } else if (mask == ScriptBaseClass.MASK_NEXT) //4
              {
                permmask = (int)m_host.NextOwnerMask;
            }

            return permmask;
        }

        public LSL_Integer llGetInventoryPermMask(string item, int mask) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;


            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == item) {
                        switch (mask) {
                            case 0:
                                return (int)inv.Value.BasePermissions;
                            case 1:
                                return (int)inv.Value.CurrentPermissions;
                            case 2:
                                return (int)inv.Value.GroupPermissions;
                            case 3:
                                return (int)inv.Value.EveryonePermissions;
                            case 4:
                                return (int)inv.Value.NextPermissions;
                        }
                    }
                }
            }

            return -1;
        }

        public LSL_String llGetInventoryCreator(string item) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == item) {
                        return inv.Value.CreatorID.ToString();
                    }
                }
            }

            Error("llGetInventoryCreator", "Can't find item '" + item + "'");

            return string.Empty;
        }


        public LSL_String llGetEnv(LSL_String name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            if (name == "sim_channel")
                return "WhiteCore-Sim Server";
            if (name == "sim_version")
                return World.RequestModuleInterface<ISimulationBase>().Version;
            if (name == "frame_number")
                return new LSL_String(World.Frame.ToString());
            if (name == "region_idle")
                return new LSL_String("1");
            if (name == "dynamic_pathfinding")
                return new LSL_String("disabled");
            if (name == "estate_id")
                return new LSL_String(World.RegionInfo.EstateSettings.EstateID.ToString());
            if (name == "region_max_prims")
                return World.RegionInfo.ObjectCapacity.ToString();

            return string.Empty;
        }

        public LSL_List llGetPrimMediaParams(LSL_Integer face, LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();
            PScriptSleep(m_sleepMsOnGetPrimMediaParams);

            // LSL Spec http://wiki.secondlife.com/wiki/LlGetPrimMediaParams says to fail silently if face is invalid
            // TODO: Need to correctly handle case where a face has no media (which gives back an empty list).
            // Assuming silently fail means give back an empty list.  Ideally, need to check this.
            if (face < 0 || face > m_host.GetNumberOfSides() - 1)
                return new LSL_List();
            return GetPrimMediaParams(m_host, face, rules);
        }

        public LSL_List llGetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            //PScriptSleep(m_sleepMsOnGetLinkMedia);
            List<ISceneChildEntity> entities = GetLinkParts(link);
            if (entities.Count == 0 || face < 0 || face > entities[0].GetNumberOfSides() - 1)
                return new LSL_List();
            LSL_List res = new LSL_List();

            return entities.Select(part => GetPrimMediaParams(part, face, rules)).Aggregate(res,
                                                                                            (current, partRes) =>
                                                                                            current + partRes);
        }

        public LSL_Integer llGetInventoryType(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;


            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == name) {
                        return inv.Value.Type;
                    }
                }
            }

            return -1;
        }


        public LSL_Vector llGetCameraPos() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector();

            UUID invItemID = InventorySelf();

            if (invItemID == UUID.Zero)
                return new LSL_Vector();

            lock (m_host.TaskInventory) {
                if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero)
                    return new LSL_Vector();

                if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0) {
                    Error("llGetCameraPos", "No permissions to track the camera");
                    return new LSL_Vector();
                }
            }

            IScenePresence presence = World.GetScenePresence(m_host.OwnerID);
            if (presence != null) {
                LSL_Vector pos = new LSL_Vector(presence.CameraPosition.X, presence.CameraPosition.Y,
                                                presence.CameraPosition.Z);
                return pos;
            }
            return new LSL_Vector();
        }

        public LSL_Rotation llGetCameraRot() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation();

            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero)
                return new LSL_Rotation();

            lock (m_host.TaskInventory) {
                if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero)
                    return new LSL_Rotation();

                if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0) {
                    Error("llGetCameraRot", "No permissions to track the camera");
                    return new LSL_Rotation();
                }
            }

            IScenePresence presence = World.GetScenePresence(m_host.OwnerID);
            if (presence != null) {
                return new LSL_Rotation(presence.CameraRotation.X, presence.CameraRotation.Y, presence.CameraRotation.Z,
                                        presence.CameraRotation.W);
            }

            return new LSL_Rotation();
        }


        public LSL_Integer llGetUnixTime() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            return Util.UnixTimeSinceEpoch();
        }

        public LSL_Integer llGetParcelFlags(LSL_Vector pos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                return (int)parcelManagement.GetLandObject((float)pos.x, (float)pos.y).LandData.Flags;
            }
            return 0;
        }

        public LSL_Integer llGetRegionFlags() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
            if (estate == null)
                return 67108864;
            return (int)estate.GetRegionFlags();
        }


        public LSL_Integer llGetParcelPrimCount(LSL_Vector pos, int category, int sim_wide) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                LandData land = parcelManagement.GetLandObject((float)pos.x, (float)pos.y).LandData;

                if (land == null) {
                    return 0;
                }
                IPrimCountModule primCountsModule = World.RequestModuleInterface<IPrimCountModule>();
                if (primCountsModule != null) {
                    IPrimCounts primCounts = primCountsModule.GetPrimCounts(land.GlobalID);
                    if (sim_wide != 0) {
                        if (category == 0) {
                            return primCounts.Simulator;
                        }
                        return 0;
                    }
                    switch (category) {
                        case 0:
                            return primCounts.Total; //land.
                        case 1:
                            return primCounts.Owner;
                        case 2:
                            return primCounts.Group;
                        case 3:
                            return primCounts.Others;
                        case 4:
                            return primCounts.Selected;
                        case 5:
                            return primCounts.Temporary; //land.
                    }
                }
            }
            return 0;
        }

        public LSL_List llGetParcelPrimOwners(LSL_Vector pos) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            LSL_List ret = new LSL_List();
            if (parcelManagement != null) {
                ILandObject land = parcelManagement.GetLandObject((float)pos.x, (float)pos.y);
                if (land != null) {
                    IPrimCountModule primCountModule = World.RequestModuleInterface<IPrimCountModule>();
                    if (primCountModule != null) {
                        IPrimCounts primCounts = primCountModule.GetPrimCounts(land.LandData.GlobalID);
                        foreach (KeyValuePair<UUID, int> detectedParams in primCounts.GetAllUserCounts()) {
                            ret.Add(new LSL_String(detectedParams.Key.ToString()));
                            ret.Add(new LSL_Integer(detectedParams.Value));
                        }
                    }
                }
            }
            PScriptSleep(m_sleepMsOnGetParcelPrimOwners);
            return ret;
        }

        public LSL_Integer llGetObjectPrimCount(string object_id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            UUID id;
            if (!UUID.TryParse(object_id, out id)) {
                return 0;
            }
            ISceneChildEntity part = World.GetSceneObjectPart(id);
            if (part == null) {
                return 0;
            }
            return part.ParentEntity.PrimCount;
        }

        public LSL_Integer llGetParcelMaxPrims(LSL_Vector pos, int sim_wide) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                IPrimCountModule primCount = World.RequestModuleInterface<IPrimCountModule>();
                ILandObject land = parcelManagement.GetLandObject((float)pos.x, (float)pos.y);
                return primCount.GetParcelMaxPrimCount(land);
            }
            return 0;
        }

        public LSL_List llGetParcelDetails(LSL_Vector pos, LSL_List param) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            LSL_List ret = new LSL_List();
            if (parcelManagement != null) {
                LandData land = parcelManagement.GetLandObject((float)pos.x, (float)pos.y).LandData;
                if (land == null) {
                    return new LSL_List(0);
                }
                foreach (object o in param.Data) {
                    if ((LSL_Integer)o == ScriptBaseClass.PARCEL_DETAILS_NAME)
                        ret.Add(new LSL_String(land.Name));
                    else if ((LSL_Integer)o == ScriptBaseClass.PARCEL_DETAILS_DESC)
                        ret.Add(new LSL_String(land.Description));
                    else if ((LSL_Integer)o == ScriptBaseClass.PARCEL_DETAILS_OWNER)
                        ret.Add(new LSL_Key(land.OwnerID.ToString()));
                    else if ((LSL_Integer)o == ScriptBaseClass.PARCEL_DETAILS_GROUP)
                        ret.Add(new LSL_Key(land.GroupID.ToString()));
                    else if ((LSL_Integer)o == ScriptBaseClass.PARCEL_DETAILS_AREA)
                        ret.Add(new LSL_Integer(land.Area));
                    else if ((LSL_Integer)o == ScriptBaseClass.PARCEL_DETAILS_ID)
                        ret.Add(new LSL_Key(land.GlobalID.ToString()));
                    else if ((LSL_Integer)o == ScriptBaseClass.PARCEL_DETAILS_PRIVACY)
                        ret.Add(new LSL_Integer(land.Private ? 1 : 0));
                    else
                        ret.Add(new LSL_Integer(0));
                }
            }
            return ret;
        }

        public LSL_List llGetObjectDetails(string id, LSL_List args) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            LSL_List ret = new LSL_List();
            UUID key = new UUID();
            if (UUID.TryParse(id, out key)) {
                IScenePresence av = World.GetScenePresence(key);

                if (av != null) {
                    foreach (object o in args.Data) {
                        if ((LSL_Integer)o == ScriptBaseClass.OBJECT_NAME) {
                            ret.Add(new LSL_String(av.Name));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_DESC) {
                            ret.Add(new LSL_String(""));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_POS) {
                            Vector3 tmp = av.AbsolutePosition;
                            ret.Add(new LSL_Vector(tmp.X, tmp.Y, tmp.Z));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_ROT) {
                            Quaternion rtmp = av.Rotation;
                            ret.Add(new LSL_Rotation(rtmp.X, rtmp.Y, rtmp.Z, rtmp.W));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_VELOCITY) {
                            Vector3 tmp = av.Velocity;
                            ret.Add(new LSL_Vector(tmp.X, tmp.Y, tmp.Z));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_OWNER) {
                            ret.Add(id);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_GROUP) {
                            ret.Add(UUID.Zero.ToString());
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CREATOR) {
                            ret.Add(UUID.Zero.ToString());
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_RUNNING_SCRIPT_COUNT) {
                            IScriptModule[] modules = World.RequestModuleInterfaces<IScriptModule>();
                            int activeScripts = modules.Sum(mod => mod.GetActiveScripts(av));
                            ret.Add(activeScripts);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TOTAL_SCRIPT_COUNT) {
                            IScriptModule[] modules = World.RequestModuleInterfaces<IScriptModule>();
                            int totalScripts = modules.Sum(mod => mod.GetTotalScripts(av));
                            ret.Add(totalScripts);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SCRIPT_MEMORY) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SCRIPT_TIME) {
                            IScriptModule[] modules = World.RequestModuleInterfaces<IScriptModule>();
                            int scriptTime = modules.Sum(mod => mod.GetScriptTime(m_itemID));
                            ret.Add(scriptTime);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PRIM_EQUIVALENCE) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SERVER_COST) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_STREAMING_COST) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PHYSICS_COST) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CHARACTER_TIME) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_ROOT) {
                            ret.Add(av.Sitting ? av.SittingOnUUID : av.UUID);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_ATTACHED_POINT) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PATHFINDING_TYPE) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PHYSICS) {
                            ret.Add(0);
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PHANTOM) {
                            ret.Add(0);
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TEMP_ON_REZ) {
                            ret.Add(0);
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_RENDER_WEIGHT) {
                            ret.Add(-1);
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_HOVER_HEIGHT) {
                            ret.Add(new LSL_Float(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_LAST_OWNER_ID) {
                            ret.Add(ScriptBaseClass.NULL_KEY);
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CLICK_ACTION) {
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_OMEGA) {
                            ret.Add(new LSL_Vector(Vector3.Zero));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PRIM_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TOTAL_INVENTORY_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_GROUP_TAG) {
                            // Return empty string for now, need a proper check
                            ret.Add(new LSL_String(String.Empty));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TEMP_ATTACHED) {
                            ret.Add(new LSL_Integer(0));
                            break;
                        }
                          // Added Sep 2017 from Constants
                          else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CREATION_TIME) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SELECT_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SIT_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else {
                            ret.Add(ScriptBaseClass.OBJECT_UNKNOWN_DETAIL);
                        }
                    }
                    return ret;
                }
                ISceneChildEntity obj = World.GetSceneObjectPart(key);
                if (obj != null) {
                    foreach (object o in args.Data) {
                        if ((LSL_Integer)o == ScriptBaseClass.OBJECT_NAME) {
                            ret.Add(new LSL_String(obj.Name));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_DESC) {
                            ret.Add(new LSL_String(obj.Description));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_POS) {
                            Vector3 tmp = obj.AbsolutePosition;
                            ret.Add(new LSL_Vector(tmp.X, tmp.Y, tmp.Z));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_ROT) {
                            Quaternion rtmp = obj.GetRotationOffset();
                            ret.Add(new LSL_Rotation(rtmp.X, rtmp.Y, rtmp.Z, rtmp.W));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_VELOCITY) {
                            Vector3 tmp = obj.Velocity;
                            ret.Add(new LSL_Vector(tmp.X, tmp.Y, tmp.Z));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_OWNER) {
                            ret.Add(new LSL_Key(obj.OwnerID.ToString()));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_GROUP) {
                            ret.Add(new LSL_Key(obj.GroupID.ToString()));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CREATOR) {
                            ret.Add(new LSL_Key(obj.CreatorID.ToString()));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_RUNNING_SCRIPT_COUNT) {
                            IScriptModule[] modules = World.RequestModuleInterfaces<IScriptModule>();
                            int activeScripts = modules.Sum(mod => mod.GetActiveScripts(obj));
                            ret.Add(new LSL_Integer(activeScripts));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TOTAL_SCRIPT_COUNT) {
                            IScriptModule[] modules = World.RequestModuleInterfaces<IScriptModule>();
                            int totalScripts = modules.Sum(mod => mod.GetTotalScripts(obj));
                            ret.Add(new LSL_Integer(totalScripts));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SCRIPT_MEMORY) {
                            ret.Add(new LSL_Integer(0));
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CHARACTER_TIME) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_ROOT) {
                            ret.Add(obj.ParentEntity.RootChild.UUID);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_ATTACHED_POINT) {
                            ret.Add(obj.ParentEntity.RootChild.AttachmentPoint);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PATHFINDING_TYPE) {
                            ret.Add(0);
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PHYSICS) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PHANTOM) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TEMP_ON_REZ) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_RENDER_WEIGHT) {
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_HOVER_HEIGHT) {
                            ret.Add(new LSL_Float(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_LAST_OWNER_ID) {
                            ret.Add(new LSL_Key(obj.LastOwnerID.ToString()));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CLICK_ACTION) {
                            ret.Add(new LSL_Integer(obj.ClickAction));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_OMEGA) {
                            ret.Add(new LSL_Vector(obj.AngularVelocity));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_PRIM_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TOTAL_INVENTORY_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_GROUP_TAG) {
                            // Return empty string for now, need a proper check
                            ret.Add(new LSL_String(String.Empty));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_TEMP_ATTACHED) {
                            // Return 0 for now, needs a proper check
                            ret.Add(new LSL_Integer(0));
                            break;
                        }
                          // Added Sep 2017 from Constants
                          else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_CREATION_TIME) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SELECT_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else if ((LSL_Integer)o == ScriptBaseClass.OBJECT_SIT_COUNT) {
                            // Return 0 for now, needs a proper check    
                            ret.Add(new LSL_Integer(0));
                            break;
                        } else {
                            ret.Add(ScriptBaseClass.OBJECT_UNKNOWN_DETAIL);
                        }
                    }
                    return ret;
                }
            }
            return new LSL_List();
        }

        public LSL_Key llGetNumberOfNotecardLines(string name) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            TaskInventoryDictionary itemsDictionary = (TaskInventoryDictionary)m_host.TaskInventory.Clone();

            UUID assetID = UUID.Zero;

            if (!UUID.TryParse(name, out assetID)) {
                foreach (TaskInventoryItem item in itemsDictionary.Values) {
                    if (item.Type == 7 && item.Name == name) {
                        assetID = item.AssetID;
                        break;
                    }
                }
            }

            if (assetID == UUID.Zero) {
                // => complain loudly, as specified by the LSL docs
                Error("llGetNumberOfNotecardLines", "Can't find notecard '" + name + "'");

                return UUID.Zero.ToString();
            }

            // was: UUID tid = tid = m_ScriptEngine.
            UUID rq = UUID.Random();
            DataserverPlugin dataserverPlugin = (DataserverPlugin)m_ScriptEngine.GetScriptPlugin("Dataserver");
            UUID tid = dataserverPlugin.RegisterRequest(m_host.UUID, m_itemID, rq.ToString());

            if (NotecardCache.IsCached(assetID)) {
                dataserverPlugin.AddReply(rq.ToString(),
                                          NotecardCache.GetLines(assetID).ToString(), 100);
                PScriptSleep(m_sleepMsOnGetNumberOfNotecardLines);
                return tid.ToString();
            }

            WithNotecard(assetID, delegate (UUID id, AssetBase a) {
                if (a == null || a.Type != 7) {
                    Error("llGetNumberOfNotecardLines", "Can't find notecard '" + name + "'");
                    tid = UUID.Zero;
                } else {
                    UTF8Encoding enc =
                        new UTF8Encoding();
                    string data = enc.GetString(a.Data);
                    NotecardCache.Cache(id, data);
                    dataserverPlugin.AddReply(rq.ToString(),
                                              NotecardCache.GetLines(id).ToString(), 100);
                }
            });

            PScriptSleep(m_sleepMsOnGetNumberOfNotecardLines);
            return tid.ToString();
        }


        public LSL_Key llGetNotecardLine(string name, int line) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            TaskInventoryDictionary itemsDictionary = (TaskInventoryDictionary)m_host.TaskInventory.Clone();

            UUID assetID = UUID.Zero;

            if (!UUID.TryParse(name, out assetID)) {
                foreach (TaskInventoryItem item in itemsDictionary.Values) {
                    if (item.Type == 7 && item.Name == name) {
                        assetID = item.AssetID;
                        break;
                    }
                }
            }

            if (assetID == UUID.Zero) {
                // => complain loudly, as specified by the LSL docs
                Error("llGetNotecardLine", "Notecard '" + name + "' could not be found.");

                return UUID.Zero.ToString();
            }

            // was: UUID tid = tid = m_ScriptEngine.
            UUID rq = UUID.Random();
            DataserverPlugin dataserverPlugin = (DataserverPlugin)m_ScriptEngine.GetScriptPlugin("Dataserver");
            UUID tid = dataserverPlugin.RegisterRequest(m_host.UUID, m_itemID, rq.ToString());

            if (NotecardCache.IsCached(assetID)) {
                dataserverPlugin.AddReply(rq.ToString(),
                                          NotecardCache.GetLine(assetID, line, m_notecardLineReadCharsMax), 100);
                PScriptSleep(m_sleepMsOnGetNotecardLine);
                return tid.ToString();
            }

            WithNotecard(assetID, delegate (UUID id, AssetBase a) {
                if (a == null || a.Type != 7) {
                    Error("llGetNotecardLine", "Notecard '" + name + "' could not be found.");
                } else {
                    UTF8Encoding enc =
                        new UTF8Encoding();
                    string data = enc.GetString(a.Data);
                    NotecardCache.Cache(id, data);
                    dataserverPlugin.AddReply(rq.ToString(),
                                              NotecardCache.GetLine(id, line,
                                                                    m_notecardLineReadCharsMax),
                                              100);
                }
            });

            PScriptSleep(m_sleepMsOnGetNotecardLine);
            return tid.ToString();
        }


        public LSL_Float llGetSimStats(LSL_Integer statType) {
            LSL_Float retVal = 0;
            if (statType == ScriptBaseClass.SIM_STAT_PCT_CHARS_STEPPED) {
                //TODO: Not implemented
                retVal = 0;
            }
            return retVal;
        }





    }
}
