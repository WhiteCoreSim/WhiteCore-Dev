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
        #region Added functions

        public LSL_Float llGetMaxScaleFactor() {
            NotImplemented("llGetMaxScaleFactor", "Not implemented at this moment");
            return 1.0f;
        }

        public LSL_Float llGetMinScaleFactor() {
            NotImplemented("llGetMinScaleFactor", "Not implemented at this moment");
            return 1.0f;
        }

        public LSL_List llGetStaticPath(LSL_Vector start, LSL_Vector end, LSL_Float radius, LSL_List parameters) {
            NotImplemented("llGetStaticPath", "Not implemented at this moment");
            LSL_List empty = new LSL_List();
            return empty;
        }

        public LSL_Integer llReturnObjectsByID(LSL_List objects) {
            NotImplemented("llReturnObjectsByID", "Not implemented at this moment");
            return 0;
        }

        public LSL_Integer llReturnObjectsByOwner(LSL_Key owner, LSL_Integer scope) {
            NotImplemented("llReturnObjectsByOwner", "Not implemented at this moment");
            return 0;
        }

        public LSL_Integer llScaleByFactor(LSL_Float scaling_factor) {
            NotImplemented("llScaleByFactor", "Not implemented at this moment");
            return 0;
        }

        public LSL_String llXorBase64(LSL_String str1, LSL_String str2) {
            NotImplemented("llXorBase64", "Not implemented at this moment");
            return string.Empty;
        }
        #endregion

        #region Added functions for Experiences
        public LSL_Integer llAgentInExperience(LSL_Key agent) {
            NotImplemented("llAgentInExperience", "Not implemented at this moment");
            return 0;
        }

        public void llClearExperiencePermissions(LSL_Key agent) {
            NotImplemented("llClearExperiencePermissions", "Not implemented at this moment");
        }

        public LSL_Key llCreateKeyValue(LSL_String key, LSL_String value) {
            NotImplemented("llClearExperiencePermissions", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llDataSizeKeyValue() {
            NotImplemented("llDataSizeKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llDeleteKeyValue(LSL_String key) {
            NotImplemented("llDeleteKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_List llGetExperienceDetails(LSL_Key experience_id) {
            NotImplemented("llGetExperienceDetails", "Not implemented at this moment");
            return new LSL_List();
        }

        public LSL_String llGetExperienceErrorMessage(LSL_Integer value) {
            NotImplemented("llGetExperienceDetails", "Not implemented at this moment");
            return String.Empty;
        }

        public LSL_List llGetExperienceList(LSL_Key agent) {
            NotImplemented("llGetExperienceDetails", "Function was deprecated");
            return new LSL_List();
        }

        public LSL_Key llKeyCountKeyValue() {
            NotImplemented("llKeyCountKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llKeysKeyValue(LSL_Integer first, LSL_Integer count) {
            NotImplemented("llKeysKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llReadKeyValue(LSL_String key) {
            NotImplemented("llReadKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public void llRequestExperiencePermissions(LSL_Key agent, LSL_String name) {
            NotImplemented("llRequestExperiencePermissions", "Not implemented at this moment");
        }

        public LSL_Integer llSitOnLink(LSL_Key agent_id, LSL_Integer link) {
            NotImplemented("llSitOnLink", "Not implemented at this moment");
            return 0;
        }

        public LSL_Key llUpdateKeyValue(LSL_Key key, LSL_String value, LSL_Integer check, LSL_String original_value) {
            NotImplemented("llUpdateKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }
        #endregion
    }
}
