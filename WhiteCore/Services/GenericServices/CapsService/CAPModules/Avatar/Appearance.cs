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
using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using Encoder = System.Drawing.Imaging.Encoder;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Services
{
    public class AppearanceCAPS : IExternalCapsRequestHandler
    {
        protected ISyncMessagePosterService m_syncMessage;
        protected IAgentAppearanceService m_appearanceService;
        protected UUID m_agentID;
        protected GridRegion m_region;
        protected string m_uri;

        public string Name { get { return GetType ().Name; } }

        public void IncomingCapsRequest (UUID agentID, GridRegion region, ISimulationBase simbase, ref OSDMap capURLs)
        {
            m_syncMessage = simbase.ApplicationRegistry.RequestModuleInterface<ISyncMessagePosterService> ();
            m_appearanceService = simbase.ApplicationRegistry.RequestModuleInterface<IAgentAppearanceService> ();
            m_region = region;
            m_agentID = agentID;

            if (m_appearanceService == null)
                return;//Can't bake!
            
            m_uri = "/CAPS/UpdateAvatarAppearance/" + UUID.Random () + "/";
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", m_uri, UpdateAvatarAppearance));
            capURLs ["UpdateAvatarAppearance"] = MainServer.Instance.ServerURI + m_uri;
        }

        public void IncomingCapsDestruction ()
        {
            MainServer.Instance.RemoveStreamHandler ("POST", m_uri);
        }

        #region Server Side Baked Textures

        byte[] UpdateAvatarAppearance (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
            try
            {
                OSDMap rm = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
                int cof_version = rm ["cof_version"].AsInteger ();

                bool success = false;
                string error = "";
                AvatarAppearance appearance = m_appearanceService.BakeAppearance (m_agentID, cof_version);

                OSDMap map = new OSDMap ();
                if (appearance == null)
                {
                    map ["success"] = false;
                    map ["error"] = "Wrong COF";
                    map ["agent_id"] = m_agentID;
                    return OSDParser.SerializeLLSDXmlBytes (map);
                }

                OSDMap uaamap = new OSDMap ();
                uaamap ["Method"] = "UpdateAvatarAppearance";
                uaamap ["AgentID"] = m_agentID;
                uaamap ["Appearance"] = appearance.ToOSD ();
                m_syncMessage.Post (m_region.ServerURI, uaamap);
                success = true;

                map ["success"] = success;
                map ["error"] = error;
                map ["agent_id"] = m_agentID;
                /*map["avatar_scale"] = appearance.AvatarHeight;
                map["textures"] = newBakeIDs.ToOSDArray();
                OSDArray visualParams = new OSDArray();
                foreach(byte b in appearance.VisualParams)
                    visualParams.Add((int)b);
                map["visual_params"] = visualParams;*/
                return OSDParser.SerializeLLSDXmlBytes (map);

            } catch (Exception e)
            {
                MainConsole.Instance.Error ("[CAPS]: " + e);
            }

            return null;
        }

        #endregion
    }
}