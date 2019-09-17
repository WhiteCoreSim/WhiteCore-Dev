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
        ///     Insert string inserts the specified string identified by src
        ///     at the index indicated by index. Index may be negative, in
        ///     which case it is end-relative. The index may exceed either
        ///     string bound, with the result being a concatenation.
        /// </summary>
        public LSL_String llInsertString(string dest, int index, string src) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();


            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.
            if (index < 0) {
                index = dest.Length + index;

                // Negative now means it is less than the lower
                // bound of the string.

                if (index < 0) {
                    return src + dest;
                }
            }

            if (index >= dest.Length) {
                return dest + src;
            }

            // The index is in bounds.
            // In this case the index refers to the index that will
            // be assigned to the first character of the inserted string.
            // So unlike the other string operations, we do not add one
            // to get the correct string length.
            return dest.Substring(0, index) + src + dest.Substring(index);
        }

        public DateTime llInstantMessage(string user, string message) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;


            // We may be able to use ClientView.SendInstantMessage here, but we need a client instance.
            // InstantMessageModule.OnInstantMessage searches through a list of scenes for a client matching the toAgent,
            // but I don't think we have a list of scenes available from here.
            // (We also don't want to duplicate the code in OnInstantMessage if we can avoid it.)

            UUID friendTransactionID = UUID.Random();

            GridInstantMessage msg = new GridInstantMessage
            {
                FromAgentID = m_host.UUID,
                ToAgentID = UUID.Parse(user),
                SessionID = friendTransactionID,
                FromAgentName = m_host.Name,
                RegionID = m_host.ParentEntity.Scene.RegionInfo.RegionID
            };

            // This is the item we're mucking with here

            // Cap the message length at 1024.
            if (message != null && message.Length > 1024)
                msg.Message = message.Substring(0, 1024);
            else
                msg.Message = message;

            msg.Dialog = (byte)InstantMessageDialog.MessageFromObject;
            msg.FromGroup = false;
            msg.Offline = 0;
            msg.ParentEstateID = 0;
            msg.Position = m_host.AbsolutePosition;
            msg.RegionID = World.RegionInfo.RegionID;
            msg.BinaryBucket
                = Util.StringToBytes256(
                    "{0}/{1}/{2}/{3}",
                    World.RegionInfo.RegionName,
                    (int)Math.Floor(m_host.AbsolutePosition.X),
                    (int)Math.Floor(m_host.AbsolutePosition.Y),
                    (int)Math.Floor(m_host.AbsolutePosition.Z));

            if (m_TransferModule != null) {
                m_TransferModule.SendInstantMessage(msg);
            }
            return PScriptSleep(m_sleepMsOnInstantMessage);
        }
    }
}
