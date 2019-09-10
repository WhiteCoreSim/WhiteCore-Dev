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
        public void llUnSit(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            UUID key = new UUID();
            if (UUID.TryParse(id, out key)) {
                IScenePresence av = World.GetScenePresence(key);

                if (av != null) {
                    if (m_host.ParentEntity.SitTargetAvatar.Contains(key)) {
                        // if the avatar is sitting on this object, then
                        // we can unsit them.  We don't want random scripts unsitting random people
                        // Lets avoid the popcorn avatar scenario.
                        av.StandUp();
                    } else {
                        // If the object owner also owns the parcel
                        // or
                        // if the land is group owned and the object is group owned by the same group
                        // or
                        // if the object is owned by a person with estate access.

                        IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
                        if (parcelManagement != null) {
                            ILandObject parcel = parcelManagement.GetLandObject(av.AbsolutePosition.X,
                                                                                av.AbsolutePosition.Y);
                            if (parcel != null) {
                                if (m_host.OwnerID == parcel.LandData.OwnerID ||
                                    (m_host.OwnerID == m_host.GroupID && m_host.GroupID == parcel.LandData.GroupID
                                     && parcel.LandData.IsGroupOwned) || World.Permissions.IsGod(m_host.OwnerID)) {
                                    av.StandUp();
                                }
                            }
                        }
                    }
                }
            }
        }



        public void llUpdateCharacter(LSL_List options) {
            IBotManager botManager = World.RequestModuleInterface<IBotManager>();
            if (botManager != null) {
                IBotController controller = botManager.GetCharacterManager(m_host.ParentEntity.UUID);
                if (controller == null)
                    return;         // nothing to controll :(

                for (int i = 0; i < options.Length; i += 2) {
                    LSL_Integer opt = options.GetLSLIntegerItem(i);
                    LSL_Float value = options.GetLSLFloatItem(i + 1);
                    if (opt == ScriptBaseClass.CHARACTER_DESIRED_SPEED)
                        controller.SetSpeedModifier((float)value.value);
                    else if (opt == ScriptBaseClass.CHARACTER_RADIUS) {
                    } else if (opt == ScriptBaseClass.CHARACTER_LENGTH) {
                    } else if (opt == ScriptBaseClass.CHARACTER_ORIENTATION) {
                    } else if (opt == ScriptBaseClass.CHARACTER_AVOIDANCE_MODE) {
                    } else if (opt == ScriptBaseClass.CHARACTER_TYPE) {
                    } else if (opt == ScriptBaseClass.TRAVERSAL_TYPE) {
                    } else if (opt == ScriptBaseClass.CHARACTER_MAX_ACCEL) {
                    } else if (opt == ScriptBaseClass.CHARACTER_MAX_DECEL) {
                    } else if (opt == ScriptBaseClass.CHARACTER_MAX_TURN_RADIUS) {
                    } else if (opt == ScriptBaseClass.CHARACTER_DESIRED_TURN_SPEED) {
                    } else if (opt == ScriptBaseClass.CHARACTER_MAX_SPEED) {
                    } else if (opt == ScriptBaseClass.CHARACTER_ACCOUNT_FOR_SKIPPED_FRAMES) {
                    } else if (opt == ScriptBaseClass.CHARACTER_STAY_WITHIN_PARCEL) {
                    }
                }
            }
        }


    }
}
