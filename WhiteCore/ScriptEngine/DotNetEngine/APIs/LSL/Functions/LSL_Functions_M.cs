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


        public void llMinEventDelay(double delay) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_ScriptEngine.SetMinEventDelay(m_itemID, m_host.UUID, delay);
        }

        public void llMessageLinked(int linknumber, int num, string msg, string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            List<ISceneChildEntity> parts = GetLinkParts(linknumber);

            foreach (ISceneChildEntity part in parts) {
                int linkNumber = m_host.LinkNum;
                if (m_host.ParentEntity.ChildrenEntities().Count == 1)
                    linkNumber = 0;

                object[] resobj = { new LSL_Integer(linkNumber),
                                    new LSL_Integer(num),
                                    new LSL_String(msg),
                                    new LSL_String(id)
                                  };

                m_ScriptEngine.PostObjectEvent(part.UUID, "link_message", resobj);
            }
        }

        public LSL_String llMD5String(string src, int nonce) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();

            return Util.Md5Hash(string.Format("{0}:{1}", src, nonce));
        }


        public DateTime llMapDestination(string simname, LSL_Vector pos, LSL_Vector lookAt) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            UUID avatarID = m_host.OwnerID;
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_host.UUID, m_itemID, 0);
            // only works on the first detected avatar
            //This only works in touch events or if the item is attached to the avatar
            if (detectedParams == null && !m_host.IsAttachment) return DateTime.Now;

            if (detectedParams != null)
                avatarID = detectedParams.Key;

            IScenePresence avatar = World.GetScenePresence(avatarID);
            if (avatar != null) {
                IMuteListModule module = m_host.ParentEntity.Scene.RequestModuleInterface<IMuteListModule>();
                if (module != null) {
                    bool cached = false; //Unneeded
                    if (module.GetMutes(avatar.UUID, out cached).Any(mute => mute.MuteID == m_host.OwnerID)) {
                        return DateTime.Now; //If the avatar is muted, they don't get any contact from the muted av
                    }
                }
                avatar.ControllingClient.SendScriptTeleportRequest(m_host.Name, simname,
                                                                   new Vector3((float)pos.x, (float)pos.y,
                                                                               (float)pos.z),
                                                                   new Vector3((float)lookAt.x, (float)lookAt.y,
                                                                               (float)lookAt.z));
            }
            return PScriptSleep(m_sleepMsOnMapDestination);
        }

        public LSL_Integer llManageEstateAccess(LSL_Integer action, LSL_String avatar) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return LSL_Integer.FALSE;
            if (World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false)) {
                if (action == ScriptBaseClass.ESTATE_ACCESS_ALLOWED_AGENT_ADD)
                    World.RegionInfo.EstateSettings.AddEstateUser(UUID.Parse(avatar));
                else if (action == ScriptBaseClass.ESTATE_ACCESS_ALLOWED_AGENT_REMOVE)
                    World.RegionInfo.EstateSettings.RemoveEstateUser(UUID.Parse(avatar));
                else if (action == ScriptBaseClass.ESTATE_ACCESS_ALLOWED_GROUP_ADD)
                    World.RegionInfo.EstateSettings.AddEstateGroup(UUID.Parse(avatar));
                else if (action == ScriptBaseClass.ESTATE_ACCESS_ALLOWED_GROUP_REMOVE)
                    World.RegionInfo.EstateSettings.RemoveEstateGroup(UUID.Parse(avatar));
                else if (action == ScriptBaseClass.ESTATE_ACCESS_BANNED_AGENT_ADD)
                    World.RegionInfo.EstateSettings.AddBan(new EstateBan
                    {
                        EstateID = World.RegionInfo.EstateSettings.EstateID,
                        BannedUserID = UUID.Parse(avatar)
                    });
                else if (action == ScriptBaseClass.ESTATE_ACCESS_BANNED_AGENT_REMOVE)
                    World.RegionInfo.EstateSettings.RemoveBan(UUID.Parse(avatar));
                return LSL_Integer.TRUE;
            } else
                Error("llManageEstateAccess", "llManageEstateAccess object owner must manage estate.");
            return LSL_Integer.FALSE;
        }

    }
}
