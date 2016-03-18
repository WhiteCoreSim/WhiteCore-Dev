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
using System.Text;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Voice
{
    public class GenericVoiceModule : INonSharedRegionModule
    {
        bool m_enabled;
        IScene m_scene;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            IConfig voiceconfig = config.Configs["Voice"];
            if (voiceconfig == null)
                return;
            
            const string voiceModule = "GenericVoice";
            if (voiceconfig.GetString("Module", voiceModule) != voiceModule)
                return;

            // We are using generic voice calls to keep the viewer happy
            m_enabled = true;

        }

        public void AddRegion(IScene scene)
        {
            if (m_enabled)
            {
                scene.EventManager.OnRegisterCaps +=
                    (agentID, server) => OnRegisterCaps(scene, agentID, server);
            }
            m_scene = scene;
            ISyncMessageRecievedService syncRecievedService =
                m_scene.RequestModuleInterface<ISyncMessageRecievedService>();
            if (syncRecievedService != null)
                syncRecievedService.OnMessageReceived += syncRecievedService_OnMessageReceived;
        }

        // Called to indicate that all loadable modules have now been added
        public void RegionLoaded(IScene scene)
        {
            // Do nothing.
        }

        // Called to indicate that the region is going away.
        public void RemoveRegion(IScene scene)
        {
        }

        public void Close()
        {
            // Do nothing.
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "GenericVoiceModule"; }
        }

        #endregion

        // OnRegisterCaps is invoked via the scene.EventManager
        // every time WhiteCore hands out capabilities to a client
        // (login, region crossing). We contribute two capabilities to
        // the set of capabilities handed back to the client:
        // ProvisionVoiceAccountRequest and ParcelVoiceInfoRequest.
        // 
        // ProvisionVoiceAccountRequest allows the client to obtain
        // the voice account credentials for the avatar it is
        // controlling (e.g., user name, password, etc).
        // 
        // ParcelVoiceInfoRequest is invoked whenever the client
        // changes from one region or parcel to another.
        //
        // Note that OnRegisterCaps is called here via a closure
        // delegate containing the scene of the respective region (see
        // Initialise()).
        public OSDMap OnRegisterCaps(IScene scene, UUID agentID, IHttpServer caps)
        {
            OSDMap retVal = new OSDMap();
            retVal["ProvisionVoiceAccountRequest"] = CapsUtil.CreateCAPS("ProvisionVoiceAccountRequest", "");
            caps.AddStreamHandler(new GenericStreamHandler("POST", retVal["ProvisionVoiceAccountRequest"],
                                                           (path, request, httpRequest, httpResponse) =>
                                                           ProvisionVoiceAccountRequest(scene, agentID)));
            retVal["ParcelVoiceInfoRequest"] = CapsUtil.CreateCAPS("ParcelVoiceInfoRequest", "");
            caps.AddStreamHandler(new GenericStreamHandler("POST", retVal["ParcelVoiceInfoRequest"],
                                                           (path, request, httpRequest, httpResponse) =>
                                                           ParcelVoiceInfoRequest(scene, agentID)));

            return retVal;
        }

        /// Callback for a client request for Voice Account Details.
        public byte[] ProvisionVoiceAccountRequest(IScene scene, UUID agentID)
        {
            try
            {
                OSDMap response = new OSDMap();
                response["username"] = "";
                response["password"] = "";
                response["voice_sip_uri_hostname"] = "";
                response["voice_account_server_name"] = "";

                return OSDParser.SerializeLLSDXmlBytes(response);
            }
            catch (Exception)
            {
                return Encoding.UTF8.GetBytes("<llsd><undef /></llsd>");
            }
        }

        /// Callback for a client request for ParcelVoiceInfo
        public byte[] ParcelVoiceInfoRequest(IScene scene, UUID agentID)
        {
            OSDMap response = new OSDMap();
            response["region_name"] = scene.RegionInfo.RegionName;
            response["parcel_local_id"] = 0;
            response["voice_credentials"] = new OSDMap();
            ((OSDMap) response["voice_credentials"])["channel_uri"] = "";
            return OSDParser.SerializeLLSDXmlBytes(response);
        }



        #region Region-side message sending

        OSDMap syncRecievedService_OnMessageReceived(OSDMap message)
        {
            string method = message["Method"];
            if (method == "GetParcelChannelInfo")
            {
                IScenePresence avatar = m_scene.GetScenePresence(message["AvatarID"].AsUUID());

                string regionName = message ["RegionName"];

                if (m_scene.RegionInfo.RegionName != regionName)
                    return null;                                            // not for the required region!!


                bool success = true;
                bool noAgent = false;
                // get channel_uri: check first whether estate
                // settings allow voice, then whether parcel allows
                // voice, if all do retrieve or obtain the parcel
                // voice channel
                if (!m_scene.RegionInfo.EstateSettings.AllowVoice)
                {
                    MainConsole.Instance.DebugFormat(
                        "[Voice]: region \"{0}\": voice not enabled in estate settings",
                        m_scene.RegionInfo.RegionName);
                    success = false;
                }
                if (avatar == null || avatar.CurrentParcel == null)
                {
                    noAgent = true;
                    success = false;
                } else if ((avatar.CurrentParcel.LandData.Flags & (uint)ParcelFlags.AllowVoiceChat) == 0)
                {
                    MainConsole.Instance.DebugFormat(
                       "[Voice]: region \"{0}\": Parcel \"{1}\" ({2}): avatar \"{3}\": voice not enabled for parcel",
                        m_scene.RegionInfo.RegionName, avatar.CurrentParcel.LandData.Name,
                        avatar.CurrentParcel.LandData.LocalID, avatar.Name);
                    success = false;
                }
                else
                {
                    MainConsole.Instance.DebugFormat(
                        "[Voice]: region \"{0}\": voice enabled in estate settings, creating parcel voice",
                        m_scene.RegionInfo.RegionName);
                    //success = true;
                }


                OSDMap map = new OSDMap();
                map ["Method"] = method;
                map["Success"] = success;
                map["NoAgent"] = noAgent;
                if (success)
                {
                    map["ParcelID"] = avatar.CurrentParcel.LandData.GlobalID;
                    map["ParcelName"] = avatar.CurrentParcel.LandData.Name;
                    map["LocalID"] = avatar.CurrentParcel.LandData.LocalID;
                    map["ParcelFlags"] = avatar.CurrentParcel.LandData.Flags;
                }
                return map;
            }
            return null;
        }

        #endregion
    }
}