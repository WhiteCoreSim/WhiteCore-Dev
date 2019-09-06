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
        public void llBreakLink (int linknum)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            UUID invItemID = InventorySelf ();

            lock (m_host.TaskInventory) {
                if ((m_host.TaskInventory [invItemID].PermsMask & ScriptBaseClass.PERMISSION_CHANGE_LINKS) == 0
                    && !m_automaticLinkPermission) {
                    Error ("llBreakLink", "PERMISSION_CHANGE_LINKS permission not set");
                    return;
                }
            }

            if (linknum < ScriptBaseClass.LINK_THIS)
                return;

            ISceneEntity parentPrim = m_host.ParentEntity;

            if (parentPrim.RootChild.AttachmentPoint != 0)
                return; // Fail silently if attached
            ISceneChildEntity childPrim = null;

            if (linknum == ScriptBaseClass.LINK_ROOT) {
            } else if (linknum == ScriptBaseClass.LINK_SET ||
                       ScriptBaseClass.LINK_ALL_OTHERS ||
                       ScriptBaseClass.LINK_ALL_CHILDREN ||
                       ScriptBaseClass.LINK_THIS) {
                foreach (ISceneChildEntity part in parentPrim.ChildrenEntities ()) {
                    if (part.UUID != m_host.UUID) {
                        childPrim = part;
                        break;
                    }
                }
            } else {
                IEntity target = m_host.ParentEntity.GetLinkNumPart (linknum);
                if (target is ISceneChildEntity) {
                    childPrim = target as ISceneChildEntity;
                } else
                    return;
                if (childPrim.UUID == m_host.UUID)
                    childPrim = null;
            }

            if (linknum == ScriptBaseClass.LINK_ROOT) {
                // Restructuring Multiple Prims.
                List<ISceneChildEntity> parts = new List<ISceneChildEntity> (parentPrim.ChildrenEntities ());
                parts.Remove (parentPrim.RootChild);
                foreach (ISceneChildEntity part in parts) {
                    parentPrim.DelinkFromGroup (part, true);
                }
                parentPrim.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);
                parentPrim.TriggerScriptChangedEvent (Changed.LINK);

                if (parts.Count > 0) {
                    ISceneChildEntity newRoot = parts [0];
                    parts.Remove (newRoot);
                    foreach (ISceneChildEntity part in parts) {
                        newRoot.ParentEntity.LinkToGroup (part.ParentEntity);
                    }
                    newRoot.ParentEntity.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);
                }
            } else {
                if (childPrim == null)
                    return;

                parentPrim.DelinkFromGroup (childPrim, true);
                childPrim.ParentEntity.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);
                parentPrim.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);
                parentPrim.TriggerScriptChangedEvent (Changed.LINK);
            }
        }

        public void llBreakAllLinks ()
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            ISceneEntity parentPrim = m_host.ParentEntity;
            if (parentPrim.RootChild.AttachmentPoint != 0)
                return; // Fail silently if attached

            List<ISceneChildEntity> parts = new List<ISceneChildEntity> (parentPrim.ChildrenEntities ());
            parts.Remove (parentPrim.RootChild);

            foreach (ISceneChildEntity part in parts) {
                parentPrim.DelinkFromGroup (part, true);
                parentPrim.TriggerScriptChangedEvent (Changed.LINK);
                part.ParentEntity.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);
            }
            parentPrim.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);
        }

        public LSL_String llBase64ToString (string str)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            try {
                byte [] b = Convert.FromBase64String (str);
                return Encoding.UTF8.GetString (b);
            } catch {
                Error ("llBase64ToString", "Error decoding string");
                return string.Empty;
            }
        }

    }
}
