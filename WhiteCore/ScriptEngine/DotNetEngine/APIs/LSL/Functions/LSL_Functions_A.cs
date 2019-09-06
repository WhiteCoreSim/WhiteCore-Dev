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
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.list;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {

        public void llAttachToAvatarTemp (int attachmentPoint)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity.RootChild.AttachmentPoint != 0)
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey (InventorySelf ()))
                    return;
                item = m_host.TaskInventory [InventorySelf ()];
            }

            if (item.PermsGranter != m_host.OwnerID)
                return;

            if ((item.NextPermissions & (uint)PermissionMask.Transfer) != (uint)PermissionMask.Transfer) {
                Error ("llAttachToAvatarTemp", "No permission to transfer");
                return;
            }

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) != 0) {
                AttachToAvatar (attachmentPoint, true);
            }
        }

        public void llAttachToAvatar (int attachmentPoint)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            if (m_host.ParentEntity.RootChild.AttachmentPoint != 0)
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey (InventorySelf ()))
                    return;
                item = m_host.TaskInventory [InventorySelf ()];
            }

            if (item.PermsGranter != m_host.OwnerID)
                return;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) != 0) {
                AttachToAvatar (attachmentPoint, false);
            }
        }

        /* The new / changed functions were tested with the following LSL script:

        default
        {
        state_entry()
            {
            rotation rot = llEuler2Rot(<0,70,0> * DEG_TO_RAD);

            llOwnerSay("to get here, we rotate over: "+ (string) llRot2Axis(rot));
            llOwnerSay("and we rotate for: "+ (llRot2Angle(rot) * RAD_TO_DEG));

            // convert back and forth between quaternion <-> vector and angle

            rotation newrot = llAxisAngle2Rot(llRot2Axis(rot),llRot2Angle(rot));

            llOwnerSay("Old rotation was: "+(string) rot);
            llOwnerSay("re-converted rotation is: "+(string) newrot);

            llSetRot(rot);  // to check the parameters in the prim
            }
        }
        */

        // Xantor 29/apr/2008
        // Returns rotation described by rotating angle radians about axis.
        // q = cos(a/2) + i (x * sin(a/2)) + j (y * sin(a/2)) + k (z * sin(a/2))
        public LSL_Rotation llAxisAngle2Rot (LSL_Vector axis, LSL_Float angle)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation ();


            double s = Math.Cos (angle * 0.5);
            double t = Math.Sin (angle * 0.5);
            double x = axis.x * t;
            double y = axis.y * t;
            double z = axis.z * t;

            return new LSL_Rotation (x, y, z, s);
        }

        public void llAllowInventoryDrop (int add)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            m_host.ParentEntity.RootChild.AllowedDrop = add != 0;

            // Update the object flags
            m_host.ParentEntity.RootChild.aggregateScriptEvents ();
        }

        public LSL_String llAvatarOnSitTarget ()
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return ScriptBaseClass.NULL_KEY;

            return m_host.SitTargetAvatar.Count > 0
                       ? new LSL_String (m_host.SitTargetAvatar [0].ToString ())
                       : ScriptBaseClass.NULL_KEY;
        }

        public LSL_Key llAvatarOnLinkSitTarget (LSL_Integer link)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return ScriptBaseClass.NULL_KEY;

            if (link == ScriptBaseClass.LINK_SET ||
                link == ScriptBaseClass.LINK_ALL_CHILDREN ||
                link == ScriptBaseClass.LINK_ALL_OTHERS ||
                link == 0)
                return ScriptBaseClass.NULL_KEY;

            var entities = GetLinkParts (link);
            return entities.Count == 0
                           ? ScriptBaseClass.NULL_KEY
                           : new LSL_String (entities [0].SitTargetAvatar.ToString ());
        }

        public DateTime llAddToLandPassList (string avatar, double hours)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule> ();
            if (parcelManagement != null) {
                LandData land =
                    parcelManagement.GetLandObject (m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).LandData;
                if (land.OwnerID == m_host.OwnerID) {
                    ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry ();
                    UUID key;
                    if (UUID.TryParse (avatar, out key)) {
                        entry.AgentID = key;
                        entry.Flags = AccessList.Access;
                        entry.Time = DateTime.Now.AddHours (hours);
                        land.ParcelAccessList.Add (entry);
                    }
                }
            }
            return PScriptSleep (m_sleepMsOnAddToLandPassList);
        }


        public DateTime llAddToLandBanList (string avatar, double hours)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule> ();
            if (parcelManagement != null) {
                LandData land =
                    parcelManagement.GetLandObject (m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y).LandData;
                if (land.OwnerID == m_host.OwnerID) {
                    ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry ();
                    UUID key;
                    if (UUID.TryParse (avatar, out key)) {
                        entry.AgentID = key;
                        entry.Flags = AccessList.Ban;
                        entry.Time = DateTime.Now.AddHours (hours);
                        land.ParcelAccessList.Add (entry);
                    }
                }
            }
            return PScriptSleep (m_sleepMsOnAddToLandBanList);
        }


    }
}