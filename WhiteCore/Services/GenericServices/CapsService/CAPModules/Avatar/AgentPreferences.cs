/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
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

using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services
{
    public class AgentPreferencesCAPS : ICapsServiceConnector
    {
        IRegionClientCapsService m_service;

        #region ICapsServiceConnector implementation

        public void RegisterCaps (IRegionClientCapsService service)
        {
            m_service = service;

            HttpServerHandle method = delegate (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse) {
                return ProcessUpdateAgentPreferences (request, m_service.AgentID);
            };

            service.AddStreamHandler ("AgentPreferences",
                new GenericStreamHandler ("POST", service.CreateCAPS ("AgentPreferences", ""), method));

            service.AddStreamHandler ("UpdateAgentLanguage",
                new GenericStreamHandler ("POST", service.CreateCAPS ("UpdateAgentLanguage", ""), method));

            service.AddStreamHandler ("UpdateAgentInformation",
                new GenericStreamHandler ("POST", service.CreateCAPS ("UpdateAgentInformation", ""), method));
        }

        public void DeregisterCaps ()
        {
            m_service.RemoveStreamHandler ("AgentPreferences", "POST");
            m_service.RemoveStreamHandler ("UpdateAgentLanguage", "POST");
            m_service.RemoveStreamHandler ("UpdateAgentInformation", "POST");
        }

        public void EnteringRegion ()
        {
        }

        #endregion

        byte [] ProcessUpdateAgentPreferences (Stream request, UUID agentID)
        {
            OSDMap rm = OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request)) as OSDMap;
            if (rm == null)
                return MainServer.BadRequest;

            IAgentConnector data = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector> ();
            if (data != null) {
                IAgentInfo agent = data.GetAgent (agentID);
                if (agent == null)
                    return MainServer.BadRequest;

                // Access preferences ?
                if (rm.ContainsKey ("access_prefs")) {
                    OSDMap accessPrefs = (OSDMap)rm ["access_prefs"];
                    string Level = accessPrefs ["max"].AsString ();
                    int maxLevel = 0;
                    if (Level == "PG")
                        maxLevel = 0;
                    if (Level == "M")
                        maxLevel = 1;
                    if (Level == "A")
                        maxLevel = 2;
                    agent.MaturityRating = maxLevel;
                }
                // Next permissions
                if (rm.ContainsKey ("default_object_perm_masks")) {
                    OSDMap permsMap = (OSDMap)rm ["default_object_perm_masks"];
                    agent.PermEveryone = permsMap ["Everyone"].AsInteger ();
                    agent.PermGroup = permsMap ["Group"].AsInteger ();
                    agent.PermNextOwner = permsMap ["NextOwner"].AsInteger ();
                }
                // Hoverheight
                if (rm.ContainsKey ("hover_height")) {
                    agent.HoverHeight = rm ["hover_height"].AsReal ();
                }
                // Language
                if (rm.ContainsKey ("language")) {
                    agent.Language = rm ["language"].AsString ();
                }
                // Show Language to others / objects
                if (rm.ContainsKey ("language_is_public")) {
                    agent.LanguageIsPublic = rm ["language_is_public"].AsBoolean ();
                }
                data.UpdateAgent (agent);

                // Build a response that can be send back to the viewer
                OSDMap resp = new OSDMap ();
                OSDMap respAccessPrefs = new OSDMap ();
                respAccessPrefs ["max"] = Utilities.GetMaxMaturity (agent.MaxMaturity);
                resp ["access_prefs"] = respAccessPrefs;

                OSDMap respDefaultPerms = new OSDMap ();
                respDefaultPerms ["Everyone"] = agent.PermEveryone;
                respDefaultPerms ["Group"] = agent.PermGroup;
                respDefaultPerms ["NextOwner"] = agent.PermNextOwner;
                resp ["default_object_perm_masks"] = respDefaultPerms;

                var acctsvc = m_service.Registry.RequestModuleInterface<IUserAccountService> ();
                int usrLevel = 0;
                if (acctsvc != null) {
                    UserAccount usrAcct = acctsvc.GetUserAccount (null, agentID);
                    usrLevel = usrAcct.UserLevel;
                }
                resp ["god_level"] = usrLevel;

                resp ["hover_height"] = agent.HoverHeight;
                resp ["language"] = agent.Language;
                resp ["language_is_public"] = agent.LanguageIsPublic;

                return OSDParser.SerializeLLSDXmlBytes (resp);
            }
            return MainServer.BlankResponse;
        }
    }
}