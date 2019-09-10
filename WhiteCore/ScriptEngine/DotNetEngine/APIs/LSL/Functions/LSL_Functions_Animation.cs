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
        public LSL_String llGetAnimation(string id) {
            // This should only return a value if the avatar is in the same region
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            UUID avatar = (UUID)id;
            IScenePresence presence = World.GetScenePresence(avatar);
            if (presence == null)
                return "";

            if (m_host.ParentEntity.Scene.RegionInfo.RegionHandle == presence.Scene.RegionInfo.RegionHandle) {
                Dictionary<UUID, string> animationstateNames = AnimationSet.Animations.AnimStateNames;
                AnimationSet currentAnims = presence.Animator.Animations;
                string currentAnimationState = string.Empty;
                if (animationstateNames.TryGetValue(currentAnims.ImplicitDefaultAnimation.AnimID,
                                                    out currentAnimationState))
                    return currentAnimationState;
            }

            return string.Empty;
        }



        public void llSetAnimationOverride(LSL_String anim_state, LSL_String anim) {
            //anim_state - animation state to be overriden
            //anim       - an animation in the prim's inventory or the name of the built-in animation

            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero)
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
                    return;
                item = m_host.TaskInventory[InventorySelf()];
            }

            if (item.PermsGranter == UUID.Zero)
                return;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS) != 0) {
                IScenePresence presence = World.GetScenePresence(item.PermsGranter);

                if (presence != null) {
                    // Do NOT try to parse UUID, animations cannot be triggered by ID
                    UUID animID = KeyOrName(anim, AssetType.Animation, false);

                    presence.Animator.SetDefaultAnimationOverride(anim_state, animID, anim);
                }
            }
        }

        public void llResetAnimationOverride(LSL_String anim_state) {
            //anim_state - animation state to be reset

            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero)
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
                    return;
                item = m_host.TaskInventory[InventorySelf()];
            }

            if (item.PermsGranter == UUID.Zero)
                return;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS) != 0) {
                IScenePresence presence = World.GetScenePresence(item.PermsGranter);

                if (presence != null) {
                    presence.Animator.ResetDefaultAnimationOverride(anim_state);
                }
            }
        }

        public LSL_String llGetAnimationOverride(string anim_state) {
            //anim_state - animation state to be reset

            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero)
                return "";

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
                    return "";
                item = m_host.TaskInventory[InventorySelf()];
            }

            if (item.PermsGranter == UUID.Zero)
                return "";

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0 ||
                (item.PermsMask & ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS) != 0) {
                IScenePresence presence = World.GetScenePresence(item.PermsGranter);

                if (presence != null) {
                    return new LSL_String(presence.Animator.GetDefaultAnimationOverride(anim_state));
                }
            }
            return "";
        }

        public void llStartAnimation(string anim) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero)
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
                    return;
                item = m_host.TaskInventory[InventorySelf()];
            }

            if (item.PermsGranter == UUID.Zero)
                return;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0) {
                IScenePresence presence = World.GetScenePresence(item.PermsGranter);

                if (presence != null) {
                    // Do NOT try to parse UUID, animations cannot be triggered by ID
                    UUID animID = KeyOrName(anim, AssetType.Animation, false);
                    if (animID == UUID.Zero) {
                        bool RetVal = presence.Animator.AddAnimation(anim, m_host.UUID);
                        if (!RetVal) {
                            IChatModule chatModule = World.RequestModuleInterface<IChatModule>();
                            if (chatModule != null)
                                chatModule.SimChat("Could not find animation '" + anim + "'.",
                                                    ChatTypeEnum.DebugChannel, 2147483647, m_host.AbsolutePosition,
                                                    m_host.Name, m_host.UUID, false, World);
                        }
                    } else
                        presence.Animator.AddAnimation(animID, m_host.UUID);
                }
            }
        }

        public void llStopAnimation(string anim) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            UUID invItemID = InventorySelf();
            if (invItemID == UUID.Zero)
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
                    return;
                item = m_host.TaskInventory[InventorySelf()];
            }

            if (item.PermsGranter == UUID.Zero)
                return;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0) {
                UUID animID = new UUID();

                if (!UUID.TryParse(anim, out animID)) {
                    animID = InventoryKey(anim, AssetType.Animation, false);
                }

                IScenePresence presence = World.GetScenePresence(item.PermsGranter);

                if (presence != null) {
                    if (animID == UUID.Zero) {
                        if (UUID.TryParse(anim, out animID))
                            presence.Animator.RemoveAnimation(animID);
                        else {
                            bool RetVal = presence.Animator.RemoveAnimation(anim);
                            if (!RetVal) {
                                IChatModule chatModule = World.RequestModuleInterface<IChatModule>();
                                if (chatModule != null)
                                    chatModule.SimChat("Could not find animation '" + anim + "'.",
                                                       ChatTypeEnum.DebugChannel, 2147483647, m_host.AbsolutePosition,
                                                       m_host.Name, m_host.UUID, false, World);
                            }
                        }
                    } else
                        presence.Animator.RemoveAnimation(animID);
                }
            }
        }


    }
}
