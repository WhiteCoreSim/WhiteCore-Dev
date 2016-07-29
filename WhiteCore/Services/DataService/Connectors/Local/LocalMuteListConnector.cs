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
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.DataService
{
    public class LocalMuteListConnector : ConnectorBase, IMuteListConnector
    {
        IGenericData GD;

        #region IMuteListConnector Members

        public void Initialize (IGenericData GenericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            GD = GenericData;

            if (source.Configs [Name] != null)
                defaultConnectionString = source.Configs [Name].GetString ("ConnectionString", defaultConnectionString);

            if (GD != null)
                GD.ConnectToDatabase (defaultConnectionString, "Generics",
                                     source.Configs ["WhiteCoreConnectors"].GetBoolean ("ValidateTables", true));

            Framework.Utilities.DataManager.RegisterPlugin (Name + "Local", this);

            if (source.Configs ["WhiteCoreConnectors"].GetString ("MuteListConnector", "LocalConnector") == "LocalConnector") {
                Framework.Utilities.DataManager.RegisterPlugin (this);
            }
            Init (simBase, Name);
        }

        public string Name {
            get { return "IMuteListConnector"; }
        }

        /// <summary>
        ///     Gets the full mute list for the given agent.
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<MuteList> GetMuteList (UUID AgentID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (AgentID);
                return remoteValue != null ? (List<MuteList>)remoteValue : new List<MuteList> ();
            }

            return GenericUtils.GetGenerics<MuteList> (AgentID, "MuteList", GD);
        }

        /// <summary>
        ///     Updates or adds a mute for the given agent
        /// </summary>
        /// <param name="mute"></param>
        /// <param name="AgentID"></param>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void UpdateMute (MuteList mute, UUID AgentID)
        {
            if (m_doRemoteOnly) {
                DoRemote (mute, AgentID);
                return;
            }

            GenericUtils.AddGeneric (AgentID, "MuteList", mute.MuteID.ToString (), mute.ToOSD (), GD);
        }

        /// <summary>
        ///     Deletes a mute for the given agent
        /// </summary>
        /// <param name="muteID"></param>
        /// <param name="AgentID"></param>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public void DeleteMute (UUID muteID, UUID AgentID)
        {
            if (m_doRemoteOnly) {
                DoRemote (muteID, AgentID);
                return;
            }

            GenericUtils.RemoveGenericByKeyAndType (AgentID, "MuteList", muteID.ToString (), GD);
        }

        /// <summary>
        ///     Checks to see if PossibleMuteID is muted by AgentID
        /// </summary>
        /// <param name="AgentID"></param>
        /// <param name="PossibleMuteID"></param>
        /// <returns></returns>
        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public bool IsMuted (UUID AgentID, UUID PossibleMuteID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (AgentID, PossibleMuteID);
                return remoteValue != null && (bool)remoteValue;
            }

            return GenericUtils.GetGeneric<MuteList> (AgentID, "MuteList", PossibleMuteID.ToString (), GD) != null;
        }

        #endregion

        public void Dispose ()
        {
        }
    }
}