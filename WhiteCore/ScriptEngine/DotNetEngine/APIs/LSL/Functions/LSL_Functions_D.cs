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
        public LSL_String llDetectedName (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return string.Empty;
            return detectedParams.Name;
        }

        public LSL_String llDetectedKey (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return string.Empty;
            return detectedParams.Key.ToString ();
        }

        public LSL_String llDetectedOwner (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return string.Empty;
            return detectedParams.Owner.ToString ();
        }

        public LSL_Integer llDetectedType (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return 0;
            return new LSL_Integer (detectedParams.Type);
        }

        public LSL_Vector llDetectedPos (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return new LSL_Vector ();
            return detectedParams.Position;
        }

        public LSL_Vector llDetectedVel (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return new LSL_Vector ();
            return detectedParams.Velocity;
        }

        public LSL_Vector llDetectedGrab (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams parms = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (parms == null)
                return new LSL_Vector (0, 0, 0);

            return parms.OffsetPos;
        }

        public LSL_Rotation llDetectedRot (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return new LSL_Rotation ();
            return detectedParams.Rotation;
        }

        public LSL_Integer llDetectedGroup (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (detectedParams == null)
                return new LSL_Integer (0);
            if (m_host.GroupID == detectedParams.Group)
                return new LSL_Integer (1);
            return new LSL_Integer (0);
        }

        public LSL_Integer llDetectedLinkNumber (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            DetectParams parms = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, number);
            if (parms == null)
                return new LSL_Integer (0);

            return new LSL_Integer (parms.LinkNum);
        }

        /// <summary>
        ///     See http://wiki.secondlife.com/wiki/LlDetectedTouchBinormal for details
        /// </summary>
        public LSL_Vector llDetectedTouchBinormal (int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, index);
            if (detectedParams == null)
                return new LSL_Vector ();
            return detectedParams.TouchBinormal;
        }

        /// <summary>
        ///     See http://wiki.secondlife.com/wiki/LlDetectedTouchFace for details
        /// </summary>
        public LSL_Integer llDetectedTouchFace (int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, index);
            if (detectedParams == null)
                return new LSL_Integer (-1);
            return new LSL_Integer (detectedParams.TouchFace);
        }

        /// <summary>
        ///     See http://wiki.secondlife.com/wiki/LlDetectedTouchNormal for details
        /// </summary>
        public LSL_Vector llDetectedTouchNormal (int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, index);
            if (detectedParams == null)
                return new LSL_Vector ();
            return detectedParams.TouchNormal;
        }

        /// <summary>
        ///     See http://wiki.secondlife.com/wiki/LlDetectedTouchPos for details
        /// </summary>
        public LSL_Vector llDetectedTouchPos (int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, index);
            if (detectedParams == null)
                return new LSL_Vector ();
            return detectedParams.TouchPos;
        }

        /// <summary>
        ///     See http://wiki.secondlife.com/wiki/LlDetectedTouchST for details
        /// </summary>
        public LSL_Vector llDetectedTouchST (int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, index);
            if (detectedParams == null)
                return new LSL_Vector (-1.0, -1.0, 0.0);
            return detectedParams.TouchST;
        }

        /// <summary>
        ///     See http://wiki.secondlife.com/wiki/LlDetectedTouchUV for details
        /// </summary>
        public LSL_Vector llDetectedTouchUV (int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            DetectParams detectedParams = m_ScriptEngine.GetDetectParams (m_host.UUID, m_itemID, index);
            if (detectedParams == null)
                return new LSL_Vector (-1.0, -1.0, 0.0);
            return detectedParams.TouchUV;
        }

        public virtual void llDie ()
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            throw new SelfDeleteException ();
        }

        /// <summary>
        ///     Delete substring removes the specified substring bounded
        ///     by the inclusive indices start and end. Indices may be
        ///     negative (indicating end-relative) and may be inverted,
        ///     i.e. end < start. />
        /// </summary>
        public LSL_String llDeleteSubString (string src, int start, int end)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String ();


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
            // Conventionally delimited substring
            if (start <= end) {
                // If both bounds are outside of the existing
                // string, then return unchanges.
                if (end < 0 || start >= src.Length) {
                    return src;
                }
                // At least one bound is in-range, so we
                // need to clip the out-of-bound argument.
                if (start < 0) {
                    start = 0;
                }

                if (end >= src.Length) {
                    end = src.Length - 1;
                }

                return src.Remove (start, end - start + 1);
            }
            // Inverted substring
            // In this case, out of bounds means that
            // the existing string is part of the cut.
            if (start < 0 || end >= src.Length) {
                return string.Empty;
            }

            if (end > 0) {
                if (start < src.Length) {
                    return src.Remove (start).Remove (0, end + 1);
                }
                return src.Remove (0, end + 1);
            }
            if (start < src.Length) {
                return src.Remove (start);
            }
            return src;
        }

        public void llDetachFromAvatar ()
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.ParentEntity.RootChild.AttachmentPoint == 0)
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey (InventorySelf ()))
                    return;
                item = m_host.TaskInventory [InventorySelf ()];
            }

            if (item.PermsGranter != m_host.OwnerID)
                return;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) != 0)
                DetachFromAvatar ();
        }

        public LSL_List llDeleteSubList (LSL_List src, int start, int end)
        {
            return src.DeleteSublist (start, end);
        }

        public LSL_String llDumpList2String (LSL_List src, string seperator)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            if (src.Length == 0) {
                return string.Empty;
            }

            string ret = src.Data.Aggregate ("", (current, o) => current + (o + seperator));

            ret = ret.Substring (0, ret.Length - seperator.Length);
            return ret;
        }

        public DateTime llDialog (string avatar, string message, LSL_List buttons, int chat_channel)
        {
            IDialogModule dm = World.RequestModuleInterface<IDialogModule> ();

            if (dm == null)
                return DateTime.Now;

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            UUID av = new UUID ();
            if (!UUID.TryParse (avatar, out av)) {
                //Silently accepted in in SL NOTE: it does sleep though!
                //LSLError("First parameter to llDialog needs to be a key");
                return PScriptSleep (m_sleepMsOnDialog);
            }
            if (buttons.Length > 12) {
                Error ("llDialog", "No more than 12 buttons can be shown");
                return DateTime.Now;
            }
            string [] buts = new string [buttons.Length];
            for (int i = 0; i < buttons.Length; i++) {
                if (buttons.Data [i].ToString () == string.Empty) {
                    Error ("llDialog", "Button label cannot be blank");
                    return DateTime.Now;
                }
                if (buttons.Data [i].ToString ().Length > 24) {
                    Error ("llDialog", "Button label cannot be longer than 24 characters");
                    return DateTime.Now;
                }
                buts [i] = buttons.Data [i].ToString ();
            }
            if (buts.Length == 0)
                buts = new [] { "OK" };

            dm.SendDialogToUser (
                av, m_host.Name, m_host.UUID, m_host.OwnerID,
                message, new UUID ("00000000-0000-2222-3333-100000001000"), chat_channel, buts);

            return PScriptSleep (m_sleepMsOnDialog);
        }


        public void llDeleteCharacter ()
        {
            IBotManager botManager = World.RequestModuleInterface<IBotManager> ();
            if (botManager != null)
                botManager.RemoveCharacter (m_host.ParentEntity.UUID);
        }



    }
}
