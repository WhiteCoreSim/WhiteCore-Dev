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



        public DateTime llCreateLink (string target, int parent)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            UUID invItemID = InventorySelf ();
            UUID targetID;

            if (!UUID.TryParse (target, out targetID))
                return DateTime.Now;

            TaskInventoryItem item;
            lock (m_host.TaskInventory) {
                item = m_host.TaskInventory [invItemID];
            }

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_CHANGE_LINKS) == 0
                && !m_automaticLinkPermission) {
                Error ("llCreateLink", "PERMISSION_CHANGE_LINKS permission not set");
                return DateTime.Now;
            }

            IClientAPI client = null;
            IScenePresence sp = World.GetScenePresence (item.PermsGranter);
            if (sp != null)
                client = sp.ControllingClient;

            ISceneChildEntity targetPart = World.GetSceneObjectPart (targetID);

            if (targetPart.ParentEntity.RootChild.AttachmentPoint != 0)
                return DateTime.Now;
            // Fail silently if attached
            ISceneEntity parentPrim = null;
            ISceneEntity childPrim = null;
            if (parent != 0) {
                parentPrim = m_host.ParentEntity;
                childPrim = targetPart.ParentEntity;
            } else {
                parentPrim = targetPart.ParentEntity;
                childPrim = m_host.ParentEntity;
            }
            //                byte uf = childPrim.RootPart.UpdateFlag;
            parentPrim.LinkToGroup (childPrim);
            //                if (uf != (Byte)0)
            //                    parent.RootPart.UpdateFlag = uf;

            parentPrim.TriggerScriptChangedEvent (Changed.LINK);
            parentPrim.RootChild.CreateSelected = true;
            parentPrim.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);

            if (client != null)
                parentPrim.GetProperties (client);

            return PScriptSleep (m_sleepMsOnCreateLink);
        }


        /// <summary>
        ///     The supplied string is scanned for commas
        ///     and converted into a list. Commas are only
        ///     effective if they are encountered outside
        ///     of &apos;&lt;&apos; &apos;&gt;&apos; delimiters. Any whitespace
        ///     before or after an element is trimmed.
        /// </summary>
        public LSL_List llCSV2List (string src)
        {
            LSL_List result = new LSL_List ();
            int parens = 0;
            int start = 0;
            int length = 0;

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List ();


            for (int i = 0; i < src.Length; i++) {
                switch (src [i]) {
                case '<':
                    parens++;
                    length++;
                    break;
                case '>':
                    if (parens > 0)
                        parens--;
                    length++;
                    break;
                case ',':
                    if (parens == 0) {
                        result.Add (new LSL_String (src.Substring (start, length).Trim ()));
                        start += length + 1;
                        length = 0;
                    } else {
                        length++;
                    }
                    break;
                default:
                    length++;
                    break;
                }
            }

            result.Add (new LSL_String (src.Substring (start, length).Trim ()));

            return result;
        }

        public DateTime llCloseRemoteDataChannel (object _channel)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            IXMLRPC xmlrpcMod = World.RequestModuleInterface<IXMLRPC> ();
            xmlrpcMod.CloseXMLRPCChannel (UUID.Parse (_channel.ToString ()));
            return PScriptSleep (m_sleepMsOnCloseRemoteDataChannel);
        }

        public LSL_Integer llClearPrimMedia (LSL_Integer face)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;
            PScriptSleep (m_sleepMsOnClearPrimMedia);

            ClearPrimMedia (m_host, face);

            return ScriptBaseClass.STATUS_OK;
        }

        public LSL_Integer llClearLinkMedia (LSL_Integer link, LSL_Integer face)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;
            //PScriptSleep(m_sleepMsOnClearLinkMedia);

            List<ISceneChildEntity> entities = GetLinkParts (link);
            if (entities.Count == 0 || face < 0 || face > entities [0].GetNumberOfSides () - 1)
                return ScriptBaseClass.STATUS_OK;

            foreach (ISceneChildEntity child in entities)
                ClearPrimMedia (child, face);

            return ScriptBaseClass.STATUS_OK;
        }


        public void llClearCameraParams ()
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            // our key in the object we are in
            UUID invItemID = InventorySelf ();
            if (invItemID == UUID.Zero) return;

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if (objectID == UUID.Zero) return;

            // we need the permission first, to know which avatar we want to clear the camera for
            UUID agentID;
            lock (m_host.TaskInventory) {
                agentID = m_host.TaskInventory [invItemID].PermsGranter;
                if (agentID == UUID.Zero) return;
                if ((m_host.TaskInventory [invItemID].PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0)
                    return;
            }

            IScenePresence presence = World.GetScenePresence (agentID);

            // we are not interested in child-agents
            if (presence.IsChildAgent) return;

            presence.ControllingClient.SendClearFollowCamProperties (objectID);
        }


        public void llCreateCharacter (LSL_List options)
        {
            IBotManager botManager = World.RequestModuleInterface<IBotManager> ();
            if (botManager != null) {
                botManager.CreateCharacter (m_host.ParentEntity.UUID, World);
                llUpdateCharacter (options);
            }
        }



    }
}
