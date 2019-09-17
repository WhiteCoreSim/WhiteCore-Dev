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
        public DateTime llOffsetTexture(double u, double v, int face) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            OffsetTexture(m_host, u, v, face);
            return PScriptSleep(m_sleepMsOnOffsetTexture);
        }

        public LSL_Integer llOverMyLand(string id) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            UUID key = new UUID();
            if (UUID.TryParse(id, out key)) {
                try {
                    IScenePresence presence = World.GetScenePresence(key);
                    IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
                    if (presence != null) {     // object is an avatar

                        if (parcelManagement != null) {
                            if (m_host.OwnerID == parcelManagement.GetLandObject(presence.AbsolutePosition.X,
                                                                                  presence.AbsolutePosition.Y).LandData.OwnerID)
                                return 1;
                        }
                    } else {                    // object is not an avatar
                        ISceneChildEntity obj = World.GetSceneObjectPart(key);
                        if (obj != null && parcelManagement != null) {
                            if (m_host.OwnerID == parcelManagement.GetLandObject(obj.AbsolutePosition.X,
                                                                                  obj.AbsolutePosition.Y).LandData.OwnerID)
                                return 1;
                        }
                    }
                } catch (NullReferenceException) {
                    // lots of places to get nulls
                    // eg, presence.AbsolutePosition
                    return 0;
                }
            }
            return 0;
        }

        public DateTime llOpenRemoteDataChannel() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IXMLRPC xmlrpcMod = World.RequestModuleInterface<IXMLRPC>();
            if (xmlrpcMod.IsEnabled()) {
                UUID channelID = xmlrpcMod.OpenXMLRPCChannel(m_host.UUID, m_itemID, UUID.Zero);
                IXmlRpcRouter xmlRpcRouter = World.RequestModuleInterface<IXmlRpcRouter>();
                if (xmlRpcRouter != null) {
                    string hostName = MainServer.Instance.HostName;
                    string protocol = MainServer.Instance.Secure ? "https://" : "http://";

                    xmlRpcRouter.RegisterNewReceiver(
                        m_ScriptEngine.ScriptModule,
                        channelID,
                        m_host.UUID,
                        m_itemID,
                        string.Format("{0}{1}:{2}/", protocol, hostName, xmlrpcMod.Port)
                    );
                }
                object[] resobj = {
                    new LSL_Integer(1),
                    new LSL_String(channelID.ToString()),
                    new LSL_String(UUID.Zero.ToString()),
                    new LSL_String(string.Empty),
                    new LSL_Integer(0),
                    new LSL_String(string.Empty)
                };
                m_ScriptEngine.PostScriptEvent(
                    m_itemID,
                    m_host.UUID,
                    new EventParams("remote_data", resobj, new DetectParams[0])
                    , EventPriority.FirstStart
                );
            }
            return PScriptSleep(m_sleepMsOnOpenRemoteDataChannel);
        }

        public void llOwnerSay(string msg) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            IChatModule chatModule = World.RequestModuleInterface<IChatModule>();
            if (chatModule != null)
                chatModule.SimChatBroadcast(msg, ChatTypeEnum.Owner, 0,
                                            m_host.AbsolutePosition, m_host.Name, m_host.UUID, false, UUID.Zero, World);
        }


    }
}
