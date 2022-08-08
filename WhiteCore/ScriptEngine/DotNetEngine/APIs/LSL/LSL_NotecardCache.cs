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
    public class NotecardCache
    {
        protected class Notecard
        {
            public string[] text;
            public DateTime lastRef;
        }

        protected static Dictionary<UUID, Notecard> m_Notecards =
            new Dictionary<UUID, Notecard>();

        public static void Cache(UUID assetID, string text) {
            CacheCheck();

            lock (m_Notecards) {
                if (m_Notecards.ContainsKey(assetID))
                    return;

                Notecard nc = new Notecard { lastRef = DateTime.Now, text = SLUtil.ParseNotecardToList(text).ToArray() };
                m_Notecards[assetID] = nc;
            }
        }

        public static bool IsCached(UUID assetID) {
            lock (m_Notecards) {
                return m_Notecards.ContainsKey(assetID);
            }
        }

        public static int GetLines(UUID assetID) {
            if (!IsCached(assetID))
                return -1;

            lock (m_Notecards) {
                m_Notecards[assetID].lastRef = DateTime.Now;
                return m_Notecards[assetID].text.Length;
            }
        }

        /// <summary>
        /// Get a notecard line.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="lineNumber">Lines start at index 0</param>
        /// <returns></returns>
        public static string GetLine(UUID assetID, int lineNumber) {
            if (lineNumber < 0)
                return "";

            if (!IsCached(assetID))
                return "";

            lock (m_Notecards) {
                m_Notecards[assetID].lastRef = DateTime.Now;

                if (lineNumber >= m_Notecards[assetID].text.Length)
                    return "\n\n\n";

                string data = m_Notecards[assetID].text[lineNumber];

                return data;
            }
        }

        /// <summary>
        /// Get a notecard line.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="lineNumber">Lines start at index 0</param>
        /// <param name="maxLength">
        /// Maximum length of the returned line.
        /// </param>
        /// <returns>
        /// If the line length is longer than <paramref name="maxLength"/>,
        /// the return string will be truncated.
        /// </returns>
        public static string GetLine(UUID assetID, int lineNumber, int maxLength) {
            string line = GetLine(assetID, lineNumber);

            if (line.Length > maxLength)
                line = line.Substring(0, maxLength);

            return line;
        }

        public static void CacheCheck() {
            lock (m_Notecards) {
                foreach (UUID key in new List<UUID>(m_Notecards.Keys)) {
                    Notecard nc = m_Notecards[key];
                    if (nc.lastRef.AddSeconds(30) < DateTime.Now)
                        m_Notecards.Remove(key);
                }
            }
        }
    }
}
