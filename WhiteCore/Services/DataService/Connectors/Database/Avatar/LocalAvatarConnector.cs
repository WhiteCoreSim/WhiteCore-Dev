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


using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services.DataService
{
    public class LocalAvatarConnector : IAvatarData
    {
        IGenericData m_GD;
        string m_realm = "user_appearance";
        readonly object m_lock = new object ();

        #region IAvatarData Members

        public void Initialize (IGenericData genericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            if (source.Configs ["WhiteCoreConnectors"].GetString ("AvatarConnector", "LocalConnector") != "LocalConnector")
                return;

            m_GD = genericData;

            string connectionString = defaultConnectionString;
            if (source.Configs [Name] != null)
                connectionString = source.Configs [Name].GetString ("ConnectionString", defaultConnectionString);

            if (m_GD != null)
                m_GD.ConnectToDatabase (connectionString, "Avatars",
                                     source.Configs ["WhiteCoreConnectors"].GetBoolean ("ValidateTables", true));

            Framework.Utilities.DataManager.RegisterPlugin (this);
        }

        public string Name {
            get { return "IAvatarData"; }
        }

        #endregion

        public void Dispose ()
        {
        }

        public AvatarAppearance Get (UUID principalID)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["PrincipalID"] = principalID;
            List<string> data;
            lock (m_lock) {
                data = m_GD.Query (new string [] { "Appearance" }, m_realm, filter, null, null, null);
            }
            if (data.Count == 0)
                return null;

            AvatarAppearance appearance = new AvatarAppearance ();
            appearance.FromOSD ((OSDMap)OSDParser.DeserializeJson (data [0]));

            return appearance;
        }

        public bool Store (UUID principalID, AvatarAppearance data)
        {
            lock (m_lock) {
                QueryFilter filter = new QueryFilter ();
                filter.andFilters ["PrincipalID"] = principalID;
                Dictionary<string, object> values = new Dictionary<string, object> ();
                values.Add ("PrincipalID", principalID);
                values.Add ("Appearance", OSDParser.SerializeJsonString (data.ToOSD ()));
                m_GD.Replace (m_realm, values);
            }

            return true;
        }

        public bool Delete (UUID principalID)
        {
            lock (m_lock) {
                QueryFilter filter = new QueryFilter ();
                filter.andFilters ["PrincipalID"] = principalID;

                return m_GD.Delete (m_realm, filter);
            }
        }
    }
}
