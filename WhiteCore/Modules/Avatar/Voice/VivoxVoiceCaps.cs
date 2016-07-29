﻿/*
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;


namespace WhiteCore.Modules
{
    public class VivoxVoiceService : IVoiceService, IService
    {
        // channel distance model values
        public const int CHAN_DIST_NONE     = 0; // no attenuation
        public const int CHAN_DIST_INVERSE  = 1; // inverse distance attenuation
        public const int CHAN_DIST_LINEAR   = 2; // linear attenuation
        public const int CHAN_DIST_EXPONENT = 3; // exponential attenuation
        public const int CHAN_DIST_DEFAULT  = CHAN_DIST_LINEAR;

        // channel type values
        public static readonly string CHAN_TYPE_POSITIONAL   = "positional";
        public static readonly string CHAN_TYPE_CHANNEL      = "channel";
        public static readonly string CHAN_TYPE_DEFAULT      = CHAN_TYPE_POSITIONAL;

        // channel mode values
        public static readonly string CHAN_MODE_OPEN         = "open";
        public static readonly string CHAN_MODE_LECTURE      = "lecture";
        public static readonly string CHAN_MODE_PRESENTATION = "presentation";
        public static readonly string CHAN_MODE_AUDITORIUM   = "auditorium";
        public static readonly string CHAN_MODE_DEFAULT      = CHAN_MODE_OPEN;

        // unconstrained default values
        public const double CHAN_ROLL_OFF_DEFAULT           = 2.0; // rate of attenuation
        public const double CHAN_ROLL_OFF_MIN               = 1.0;
        public const double CHAN_ROLL_OFF_MAX               = 4.0;
        public const int CHAN_MAX_RANGE_DEFAULT             = 80; // distance at which channel is silent
        public const int CHAN_MAX_RANGE_MIN                 = 0;
        public const int CHAN_MAX_RANGE_MAX                 = 160;
        public const int CHAN_CLAMPING_DISTANCE_DEFAULT     = 10; // distance before attenuation applies
        public const int CHAN_CLAMPING_DISTANCE_MIN         = 0;
        public const int CHAN_CLAMPING_DISTANCE_MAX         = 160;

        static readonly Object vlock = new Object();

        // Control info, e.g. vivox server, admin user, admin password
        static bool m_adminConnected;

        static string m_vivoxServer;
        static string m_vivoxSipUri;
        static string m_vivoxVoiceAccountApi;
        static string m_vivoxAdminUser;
        static string m_vivoxAdminPassword;
        static string m_authToken = String.Empty;

        static int m_vivoxChannelDistanceModel;
        static double m_vivoxChannelRollOff;
        static int m_vivoxChannelMaximumRange;
        static string m_vivoxChannelMode;
        static string m_vivoxChannelType;
        static int m_vivoxChannelClampingDistance;

        static Dictionary<string, string> m_parents = new Dictionary<string, string>();
        static bool m_dumpXml;
        protected IRegistryCore m_registry;

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            
            IConfig voiceconfig = config.Configs["Voice"];
            if (voiceconfig == null)
                return;

            const string voiceModule = "VivoxVoice";
            if (voiceconfig.GetString("Module", voiceModule) != voiceModule)
                return;

            IConfig vivoxConfig = config.Configs["VivoxVoice"];

            if (vivoxConfig == null)
                return;

            // save for later
            m_registry = registry;

            // we need to know if the service is local 
            IConfig wcconf = config.Configs["WhiteCoreConnectors"];
            if (wcconf == null)
                return;                             // something major if we don't have this!!
            if (wcconf.GetBoolean("DoRemoteCalls",false))
                return;

            MainConsole.Instance.InfoFormat("[VivoxVoice] Using Vivox for voice communications");

            // This is a local service, either grid server or standalone
            // (region servers do not require the admin configuration)
            // get and initialise admin configuration for control
            try
            {
                // retrieve configuration variables
                m_vivoxServer = vivoxConfig.GetString("vivox_server", String.Empty);
                m_vivoxSipUri = vivoxConfig.GetString("vivox_sip_uri", String.Empty);
                m_vivoxAdminUser = vivoxConfig.GetString("vivox_admin_user", String.Empty);
                m_vivoxAdminPassword = vivoxConfig.GetString("vivox_admin_password", String.Empty);

                m_vivoxChannelDistanceModel = vivoxConfig.GetInt("vivox_channel_distance_model", CHAN_DIST_DEFAULT);
                m_vivoxChannelRollOff = vivoxConfig.GetDouble("vivox_channel_roll_off", CHAN_ROLL_OFF_DEFAULT);
                m_vivoxChannelMaximumRange = vivoxConfig.GetInt("vivox_channel_max_range", CHAN_MAX_RANGE_DEFAULT);
                m_vivoxChannelMode = vivoxConfig.GetString("vivox_channel_mode", CHAN_MODE_DEFAULT).ToLower();
                m_vivoxChannelType = vivoxConfig.GetString("vivox_channel_type", CHAN_TYPE_DEFAULT).ToLower();
                m_vivoxChannelClampingDistance = vivoxConfig.GetInt("vivox_channel_clamping_distance",
                                                                    CHAN_CLAMPING_DISTANCE_DEFAULT);
                m_dumpXml = vivoxConfig.GetBoolean("dump_xml", false);

                // Validate against constraints and default if necessary
                if (m_vivoxChannelRollOff < CHAN_ROLL_OFF_MIN || m_vivoxChannelRollOff > CHAN_ROLL_OFF_MAX)
                {
                    MainConsole.Instance.WarnFormat("[VivoxVoice] Invalid value for roll off ({0}), reset to {1}.",
                                                    m_vivoxChannelRollOff, CHAN_ROLL_OFF_DEFAULT);
                    m_vivoxChannelRollOff = CHAN_ROLL_OFF_DEFAULT;
                }

                if (m_vivoxChannelMaximumRange < CHAN_MAX_RANGE_MIN || m_vivoxChannelMaximumRange > CHAN_MAX_RANGE_MAX)
                {
                    MainConsole.Instance.WarnFormat("[VivoxVoice] Invalid value for maximum range ({0}), reset to {1}.",
                                                    m_vivoxChannelMaximumRange, CHAN_MAX_RANGE_DEFAULT);
                    m_vivoxChannelMaximumRange = CHAN_MAX_RANGE_DEFAULT;
                }

                if (m_vivoxChannelClampingDistance < CHAN_CLAMPING_DISTANCE_MIN ||
                    m_vivoxChannelClampingDistance > CHAN_CLAMPING_DISTANCE_MAX)
                {
                    MainConsole.Instance.WarnFormat(
                        "[VivoxVoice] Invalid value for clamping distance ({0}), reset to {1}.",
                        m_vivoxChannelClampingDistance, CHAN_CLAMPING_DISTANCE_DEFAULT);
                    m_vivoxChannelClampingDistance = CHAN_CLAMPING_DISTANCE_DEFAULT;
                }

                switch (m_vivoxChannelMode)
                {
                    case "open":
                        break;
                    case "lecture":
                        break;
                    case "presentation":
                        break;
                    case "auditorium":
                        break;
                    default:
                        MainConsole.Instance.WarnFormat(
                            "[VivoxVoice] Invalid value for channel mode ({0}), reset to {1}.",
                            m_vivoxChannelMode, CHAN_MODE_DEFAULT);
                        m_vivoxChannelMode = CHAN_MODE_DEFAULT;
                        break;
                }

                switch (m_vivoxChannelType)
                {
                    case "positional":
                        break;
                    case "channel":
                        break;
                    default:
                        MainConsole.Instance.WarnFormat(
                            "[VivoxVoice] Invalid value for channel type ({0}), reset to {1}.",
                            m_vivoxChannelType, CHAN_TYPE_DEFAULT);
                        m_vivoxChannelType = CHAN_TYPE_DEFAULT;
                        break;
                }

                m_vivoxVoiceAccountApi = String.Format("http://{0}/api2", m_vivoxServer);

                // Admin interface required values
                if (String.IsNullOrEmpty(m_vivoxServer) ||
                    String.IsNullOrEmpty(m_vivoxSipUri) ||
                    String.IsNullOrEmpty(m_vivoxAdminUser) ||
                    String.IsNullOrEmpty(m_vivoxAdminPassword))
                {
                    MainConsole.Instance.Error("[VivoxVoice] plugin has wrong configuration");
                    MainConsole.Instance.Info("[VivoxVoice] plugin disabled: incomplete configuration");
                    return;
                }

                MainConsole.Instance.InfoFormat("[VivoxVoice] using vivox server {0}", m_vivoxServer);

                // Get admin rights and cleanup any residual channel definition
                DoAdminLogin();
                 
                // if we get here then all is well
                MainConsole.Instance.Info("[VivoxVoice]: plugin enabled");

                registry.RegisterModuleInterface<IVoiceService>(this);
                
             }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[VivoxVoice] plugin initialization failed: {0}", e);
            }
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            //if (m_pluginEnabled)
            //    VivoxLogout();
        }

        #region IVoiceModule Members

        public void VoiceAccountRequest(IRegionClientCapsService regionClient, out string agentname, out string password,
                                        out string vivoxSipUri, out string vivoxVoiceAccountApi)
        {
            vivoxSipUri = m_vivoxSipUri;
            vivoxVoiceAccountApi = m_vivoxVoiceAccountApi;

            bool retry = false;
            agentname = "x" + Convert.ToBase64String(regionClient.AgentID.GetBytes());
            password = new UUID(Guid.NewGuid()).ToString().Replace('-', 'Z').Substring(0, 16);
            string code;

            agentname = agentname.Replace('+', '-').Replace('/', '_');

            do
            {
                XmlElement resp = VivoxGetAccountInfo(agentname);

                if (XmlFind(resp, "response.level0.status", out code))
                {
                    if (code != "OK")
                    {
                        if (XmlFind(resp, "response.level0.body.code", out code))
                        {
                            // If the request was recognized, then this should be set to something
                            switch (code)
                            {
                                case "201": // Account expired
                                    MainConsole.Instance.ErrorFormat(
                                        "[VivoxVoice]: avatar \"{0}\": Get account information failed : expired credentials",
                                        regionClient.ClientCaps.AccountInfo.Name);
                                    m_adminConnected = false;
                                    retry = DoAdminLogin();
                                    break;

                                case "202": // Missing credentials
                                    MainConsole.Instance.ErrorFormat(
                                        "[VivoxVoice]: avatar \"{0}\": Get account information failed : missing credentials",
                                        regionClient.ClientCaps.AccountInfo.Name);
                                    break;

                                case "212": // Not authorized
                                    MainConsole.Instance.ErrorFormat(
                                        "[VivoxVoice]: avatar \"{0}\": Get account information failed : not authorized",
                                        regionClient.ClientCaps.AccountInfo.Name);
                                    break;

                                case "300": // Required parameter missing
                                    MainConsole.Instance.ErrorFormat(
                                        "[VivoxVoice]: avatar \"{0}\": Get account information failed : parameter missing",
                                        regionClient.ClientCaps.AccountInfo.Name);
                                    break;

                                case "403": // Account does not exist
                                    resp = VivoxCreateAccount(agentname, password);
                                    // Note: This REALLY MUST BE status. Create Account does not return code.
                                    if (XmlFind(resp, "response.level0.status", out code))
                                    {
                                        switch (code)
                                        {
                                            case "201": // Account expired
                                                MainConsole.Instance.ErrorFormat(
                                                    "[VivoxVoice]: avatar \"{0}\": Create account information failed : expired credentials",
                                                    regionClient.ClientCaps.AccountInfo.Name);
                                                m_adminConnected = false;
                                                retry = DoAdminLogin();
                                                break;

                                            case "202": // Missing credentials
                                                MainConsole.Instance.ErrorFormat(
                                                    "[VivoxVoice]: avatar \"{0}\": Create account information failed : missing credentials",
                                                    regionClient.ClientCaps.AccountInfo.Name);
                                                break;

                                            case "212": // Not authorized
                                                MainConsole.Instance.ErrorFormat(
                                                    "[VivoxVoice]: avatar \"{0}\": Create account information failed : not authorized",
                                                    regionClient.ClientCaps.AccountInfo.Name);
                                                break;

                                            case "300": // Required parameter missing
                                                MainConsole.Instance.ErrorFormat(
                                                    "[VivoxVoice]: avatar \"{0}\": Create account information failed : parameter missing",
                                                    regionClient.ClientCaps.AccountInfo.Name);
                                                break;

                                            case "400": // Create failed
                                                MainConsole.Instance.ErrorFormat(
                                                    "[VivoxVoice]: avatar \"{0}\": Create account information failed : create failed",
                                                    regionClient.ClientCaps.AccountInfo.Name);
                                                break;
                                        }
                                    }
                                    break;

                                case "404": // Failed to retrieve account
                                    MainConsole.Instance.ErrorFormat(
                                        "[VivoxVoice]: avatar \"{0}\": Get account information failed : retrieve failed",
                                        regionClient.ClientCaps.AccountInfo.Name);
                                    // [AMW] Sleep and retry for a fixed period? Or just abandon?
                                    break;
                            }
                        }
                    }
                }
            } while (retry);

            if (code != "OK")
            {
                MainConsole.Instance.DebugFormat(
                    "[VivoxVoice][PROVISIONVOICE]: Get Account Request failed for \"{0}\"",
                    regionClient.ClientCaps.AccountInfo.Name);
                throw new Exception("Unable to execute request");
            }

            // Unconditionally change the password on each request
            VivoxPassword(agentname, password);
        }

        public void ParcelVoiceRequest(IRegionClientCapsService regionClient, out string channel_uri, out int localID)
        {
            channel_uri = "";
            localID = 0;
            IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService>();
            UserInfo user = agentInfoService.GetUserInfo(regionClient.AgentID.ToString());
            if (user == null || !user.IsOnline)
                return;

            bool success;
            UUID parcelID;
            string parcelName, ParentID;
            uint parcelFlags;
            GetParcelChannelInfo(regionClient.AgentID, regionClient.Region, user.CurrentRegionURI, out success, out parcelID, out parcelName,
                                 out localID, out parcelFlags, out ParentID);
            if (success)
                channel_uri = RegionGetOrCreateChannel(user.CurrentRegionID, regionClient.Region.RegionName, parcelID,
                                                       parcelName, localID, parcelFlags, ParentID);

        }

        public void GetParcelChannelInfo(UUID avatarID, Framework.Services.GridRegion region, string URL,
                                         out bool success, out UUID parcelID, out string parcelName, out int localID,
                                         out uint parcelFlags, out string ParentID)
        {
            ISyncMessagePosterService syncPoster = m_registry.RequestModuleInterface<ISyncMessagePosterService>();
            OSDMap request = new OSDMap();
            request["AvatarID"] = avatarID;
            request["Method"] = "GetParcelChannelInfo";
            request ["RegionName"] = region.RegionName;
            OSDMap response = null;
            syncPoster.Get(URL, request, resp => { response = resp; });
            while (response == null)
                Thread.Sleep (5);

            success = response["Success"];
            bool noAgents = response ["NoAgent"];
            if (!success || noAgents)
            {
                // parcel is not voice enabled or there are no agents here
                parcelID = UUID.Zero;
                parcelName = "";
                localID = 0;
                parcelFlags = 0;
                ParentID = "";
            } else
            {
                // set parcel details
                parcelID = response ["ParcelID"];
                parcelName = response ["ParcelName"];
                localID = response ["LocalID"];
                parcelFlags = response ["ParcelFlags"];
                ParentID = GetParentIDForRegion (region);
            }
        }

        string GetParentIDForRegion(Framework.Services.GridRegion region)
        {
            lock (vlock)
            {
                string sceneUUID = region.RegionID.ToString();
                lock (m_parents)
                    if (m_parents.ContainsKey(sceneUUID))
                        return m_parents[sceneUUID];

                string channelId;

                string sceneName = region.RegionName;

                // Make sure that all local channels are deleted.
                // So we have to search for the children, and then do an
                // iteration over the set of children identified.
                // This assumes that there is just one directory per
                // region.

                if (VivoxTryGetDirectory(sceneUUID + "D", out channelId))
                {
                    MainConsole.Instance.DebugFormat(
                        "[VivoxVoice]: region {0}: uuid {1}: located directory id {2}",
                        sceneName, sceneUUID, channelId);

                    XmlElement children = VivoxListChildren(channelId);
                    string count;

                    if ( XmlFind(children, "response.level0.channel-search.count", out count) )
                    {
                        int cnum = Convert.ToInt32(count);
                        for (int i = 0; i < cnum; i++)
                        {
                            string id;
                            if (XmlFind(children,
                                        "response.level0.channel-search.channels.channels.level4.id",
                                        i, out id))
                            {
                                if (!IsOK(VivoxDeleteChannel(channelId, id)))
                                    MainConsole.Instance.WarnFormat(
                                        "[VivoxVoice] Channel delete failed {0}:{1}:{2}",
                                        i, channelId, id);
                            }
                        }
                    }
                }
                else
                {
                    if ( !VivoxTryCreateDirectory(sceneUUID + "D", sceneName, out channelId) )
                    {
                        MainConsole.Instance.WarnFormat(
                            "[VivoxVoice] Create failed <{0}:{1}:{2}>",
                            "*", sceneUUID, sceneName);
                        channelId = String.Empty;
                    }
                }


                // Create a dictionary entry unconditionally. This eliminates the
                // need to check for a parent in the core code. The end result is
                // the same, if the parent table entry is an empty string, then
                // region channels will be created as first-level channels.

                lock (m_parents)
                    if (!m_parents.ContainsKey(sceneUUID))
                        m_parents.Add(sceneUUID, channelId);
                return channelId;
            }
        }

         string RegionGetOrCreateChannel(UUID regionID, string regionName, UUID parcelID, string parcelName,
                                                int localID, uint parcelFlags, string voiceParentID)
        {
            string channelUri;
            string channelId;

            string landUUID;
            string landName;

            // Create parcel voice channel. If no parcel exists, then the voice channel ID is the same
            // as the directory ID. Otherwise, it reflects the parcel's ID.

            if (localID != 1 && (parcelFlags & (uint) ParcelFlags.UseEstateVoiceChan) == 0)
            {
                landName = string.Format("{0}:{1}", regionName, parcelName);
                landUUID = parcelID.ToString();
                MainConsole.Instance.TraceFormat(
                    "[VivoxVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}",
                    landName, localID, landUUID);
            }
            else
            {
                landName = string.Format("{0}:{1}", regionName, "Full");        // 20160505 -greythane - was regionName:regionName 
                landUUID = regionID.ToString();
                MainConsole.Instance.TraceFormat(
                    "[VivoxVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}",
                    landName, localID, landUUID);
            }

            lock (vlock)
            {
                // Added by Adam to help debug channel not available errors.
                if (VivoxTryGetChannel(voiceParentID, landUUID, out channelId, out channelUri))
                    MainConsole.Instance.DebugFormat("[VivoxVoice] Found existing channel at " + channelUri);
                else if (VivoxTryCreateChannel(voiceParentID, landUUID, landName, out channelUri))
                    MainConsole.Instance.InfoFormat("[VivoxVoice] Created new channel at {0} for {1}", channelUri, regionName);
                else
                    throw new Exception("vivox channel uri not available");

                MainConsole.Instance.TraceFormat(
                    "[VivoxVoice]: Region:Parcel \"{0}\": parent channel id {1}: retrieved parcel channel_uri {2} ",
                    landName, voiceParentID, channelUri);
            }

            return channelUri;
        }

        public OSDMap GroupConferenceCallRequest(IRegionClientCapsService caps, UUID sessionid)
        {
            OSDMap map = new OSDMap();
            map["session_id"] = sessionid;
            OSDMap voice_credentials = new OSDMap();

            string channelID = "Conff" + sessionid;
            string channelUri, parentID;
            lock (vlock)
            {
                if (!VivoxTryCreateDirectory("Server" + sessionid + "D", sessionid.ToString(), out parentID))
                {
                    VivoxTryGetDirectory("Server" + sessionid + "D", out parentID);
                    //parentID = String.Empty;
                }
                // Added by Adam to help debug channel not availible errors.
                if (VivoxTryGetChannel(parentID, channelID, out channelID, out channelUri))
                    MainConsole.Instance.DebugFormat("[VivoxVoice] Found existing channel at " + channelUri);
                else if (VivoxTryCreateChannel(parentID, "Conff" + sessionid, "Conff" + sessionid,
                                               out channelUri))
                    MainConsole.Instance.DebugFormat("[VivoxVoice] Created new channel at " + channelUri);
                else
                    throw new Exception("vivox channel uri not available");

                MainConsole.Instance.TraceFormat("[VivoxVoice]: Conference \"{0}\": retrieved parcel channel_uri {1} ",
                                                 channelID, channelUri);
            }
            voice_credentials["channel_uri"] = channelUri;
            voice_credentials["channel_credentials"] = "";
            map["voice_credentials"] = voice_credentials;

            // <llsd><map>
            //       <key>session-id</key><string>c0da7611-9405-e3a4-0172-c36a1120c77a</string>
            //       <key>voice_credentials</key><map>
            //           <key>channel_credentials</key><string>rh1iIIiT2v+ebJjRI+klpFHjFmo</string>
            //           <key>channel_uri</key><string>sip:confctl-12574742@bhr.vivox.com</string>
            //       </map>
            // </map></llsd>
            return map;
        }

        #endregion

        #region Vivox Calls

        static readonly string m_vivoxLoginPath = "http://{0}/api2/viv_signin.php?userid={1}&pwd={2}";

        /// <summary>
        ///     Perform administrative login for Vivox.
        ///     Returns a hash table containing values returned from the request.
        /// </summary>
        XmlElement VivoxLogin(string name, string password)
        {
            string requrl = String.Format(m_vivoxLoginPath, m_vivoxServer, name, password);
            return VivoxCall(requrl, false);
        }


        static readonly string m_vivoxLogoutPath = "http://{0}/api2/viv_signout.php?auth_token={1}";

        /// <summary>
        ///     Perform administrative logout for Vivox.
        /// </summary>
        XmlElement VivoxLogout()
        {
            string requrl = String.Format(m_vivoxLogoutPath, m_vivoxServer, m_authToken);
            return VivoxCall(requrl, false);
        }


        static readonly string m_vivoxGetAccountPath =
            "http://{0}/api2/viv_get_acct.php?auth_token={1}&user_name={2}";

        /// <summary>
        ///     Retrieve account information for the specified user.
        ///     Returns a hash table containing values returned from the request.
        /// </summary>
        XmlElement VivoxGetAccountInfo(string user)
        {
            string requrl = String.Format(m_vivoxGetAccountPath, m_vivoxServer, m_authToken, user);
            return VivoxCall(requrl, true);
        }


        static readonly string m_vivoxNewAccountPath =
            "http://{0}/api2/viv_adm_acct_new.php?username={1}&pwd={2}&auth_token={3}";

        /// <summary>
        ///     Creates a new account.
        ///     For now we supply the minimum set of values, which
        ///     is user name and password. We *can* supply a lot more
        ///     demographic data.
        /// </summary>
        XmlElement VivoxCreateAccount(string user, string password)
        {
            string requrl = String.Format(m_vivoxNewAccountPath, m_vivoxServer, user, password, m_authToken);
            return VivoxCall(requrl, true);
        }


        static readonly string m_vivoxPasswordPath =
            "http://{0}/api2/viv_adm_password.php?user_name={1}&new_pwd={2}&auth_token={3}";

        /// <summary>
        ///     Change the user's password.
        /// </summary>
        XmlElement VivoxPassword(string user, string password)
        {
            string requrl = String.Format(m_vivoxPasswordPath, m_vivoxServer, user, password, m_authToken);
            return VivoxCall(requrl, true);
        }


        static readonly string m_vivoxChannelPath =
            "http://{0}/api2/viv_chan_mod.php?mode={1}&chan_name={2}&auth_token={3}";

        /// <summary>
        ///     Create a channel.
        ///     Once again, there a multitude of options possible. In the simplest case
        ///     we specify only the name and get a non-persistent cannel in return. Non
        ///     persistent means that the channel gets deleted if no-one uses it for
        ///     5 hours. To accommodate future requirements, it may be a good idea to
        ///     initially create channels under the umbrella of a parent ID based upon
        ///     the region name. That way we have a context for side channels, if those
        ///     are required in a later phase.
        ///     In this case the call handles parent and description as optional values.
        /// </summary>
        bool VivoxTryCreateChannel(string parent, string channelId, string description, out string channelUri)
        {
            string requrl = String.Format(m_vivoxChannelPath, m_vivoxServer, "create", channelId, m_authToken);

            if (!string.IsNullOrEmpty(parent))
            {
                requrl = String.Format("{0}&chan_parent={1}", requrl, parent);
            }
            if (!string.IsNullOrEmpty(description))
            {
                requrl = String.Format("{0}&chan_desc={1}", requrl, description);
            }

            requrl = String.Format("{0}&chan_type={1}", requrl, m_vivoxChannelType);
            requrl = String.Format("{0}&chan_mode={1}", requrl, m_vivoxChannelMode);
            requrl = String.Format("{0}&chan_roll_off={1}", requrl, m_vivoxChannelRollOff);
            requrl = String.Format("{0}&chan_dist_model={1}", requrl, m_vivoxChannelDistanceModel);
            requrl = String.Format("{0}&chan_max_range={1}", requrl, m_vivoxChannelMaximumRange);
            requrl = String.Format("{0}&chan_clamping_distance={1}", requrl, m_vivoxChannelClampingDistance);

            XmlElement resp = VivoxCall(requrl, true);
            if (XmlFind(resp, "response.level0.body.chan_uri", out channelUri))
                return true;

            channelUri = String.Empty;
            return false;
        }

        /// <summary>
        ///     Create a directory.
        ///     Create a channel with an unconditional type of "dir" (indicating directory).
        ///     This is used to create an arbitrary name tree for partitioning of the
        ///     channel name space.
        ///     The parent and description are optional values.
        /// </summary>
        bool VivoxTryCreateDirectory(string dirId, string description, out string channelId)
        {
            string requrl = String.Format(m_vivoxChannelPath, m_vivoxServer, "create", dirId, m_authToken);

            // if (parent != null && parent != String.Empty)
            // {
            //     requrl = String.Format("{0}&chan_parent={1}", requrl, parent);
            // }

            if (!string.IsNullOrEmpty(description))
            {
                requrl = String.Format("{0}&chan_desc={1}", requrl, description);
            }
            requrl = String.Format("{0}&chan_type={1}", requrl, "dir");

            XmlElement resp = VivoxCall(requrl, true);
            if (IsOK(resp) && XmlFind(resp, "response.level0.body.chan_id", out channelId))
                return true;

            channelId = String.Empty;
            return false;
        }

        static readonly string m_vivoxChannelSearchPath =
            "http://{0}/api2/viv_chan_search.php?cond_channame={1}&auth_token={2}";

        /// <summary>
        ///     Retrieve a channel.
        ///     Once again, there a multitude of options possible. In the simplest case
        ///     we specify only the name and get a non-persistent cannel in return. Non
        ///     persistent means that the channel gets deleted if no-one uses it for
        ///     5 hours. To accommodate future requirements, it may be a good idea to
        ///     initially create channels under the umbrella of a parent ID based upon
        ///     the region name. That way we have a context for side channels, if those
        ///     are required in a later phase.
        ///     In this case the call handles parent and description as optional values.
        /// </summary>
        bool VivoxTryGetChannel(string channelParent, string channelName,
                                        out string channelId, out string channelUri)
        {
            string count;

            string requrl = String.Format(m_vivoxChannelSearchPath, m_vivoxServer, channelName, m_authToken);
            XmlElement resp = VivoxCall(requrl, true);

            if (XmlFind(resp, "response.level0.channel-search.count", out count))
            {
                int channels = Convert.ToInt32(count);

                // Bug in Vivox Server r2978 where count returns 0
                // Found by Adam
                if (channels == 0)
                {
                    for (int j = 0; j < 100; j++)
                    {
                        string tmpId;
                        if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.id", j, out tmpId))
                            break;

                        channels = j + 1;
                    }
                }

                for (int i = 0; i < channels; i++)
                {
                    string name;
                    string id;
                    string type;
                    string uri;
                    string parent;

                    // skip if not a channel
                    if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.type", i, out type) ||
                        (type != "channel" && type != "positional_M"))
                    {
                        MainConsole.Instance.Debug("[VivoxVoice] Skipping Channel " + i + " as it's not a channel.");
                        continue;
                    }

                    // skip if not the name we are looking for
                    if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.name", i, out name) ||
                        name != channelName)
                    {
                        MainConsole.Instance.Debug("[VivoxVoice] Skipping Channel " + i + " as it has no name.");
                        continue;
                    }

                    // skip if parent does not match
                    if (channelParent != null &&
                        !XmlFind(resp, "response.level0.channel-search.channels.channels.level4.parent", i, out parent))
                    {
                        MainConsole.Instance.Debug("[VivoxVoice] Skipping Channel " + i + "/" + name +
                                                   " as it's parent doesn't match");
                        continue;
                    }

                    // skip if no channel id available
                    if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.id", i, out id))
                    {
                        MainConsole.Instance.Debug("[VivoxVoice] Skipping Channel " + i + "/" + name +
                                                   " as it has no channel ID");
                        continue;
                    }

                    // skip if no channel uri available
                    if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.uri", i, out uri))
                    {
                        MainConsole.Instance.Debug("[VivoxVoice] Skipping Channel " + i + "/" + name +
                                                   " as it has no channel URI");
                        continue;
                    }

                    channelId = id;
                    channelUri = uri;

                    return true;
                }
            }
            else
            {
                MainConsole.Instance.Debug("[VivoxVoice] No count element?");
            }

            channelId = String.Empty;
            channelUri = String.Empty;

            // Useful incase something goes wrong.
            //MainConsole.Instance.Debug("[VivoxVoice] Could not find channel in XMLRESP: " + resp.InnerXml);

            return false;
        }

        bool VivoxTryGetDirectory(string directoryName, out string directoryId)
        {
            string count;

            string requrl = String.Format(m_vivoxChannelSearchPath, m_vivoxServer, directoryName, m_authToken);
            XmlElement resp = VivoxCall(requrl, true);

            if (XmlFind(resp, "response.level0.channel-search.count", out count))
            {
                int channels = Convert.ToInt32(count);
                for (int i = 0; i < channels; i++)
                {
                    string name;
                    string id;
                    string type;

                    // skip if not a directory
                    if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.type", i, out type) ||
                        type != "dir")
                        continue;

                    // skip if not the name we are looking for
                    if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.name", i, out name) ||
                        name != directoryName)
                        continue;

                    // skip if no channel id available
                    if (!XmlFind(resp, "response.level0.channel-search.channels.channels.level4.id", i, out id))
                        continue;

                    directoryId = id;
                    return true;
                }
            }

            directoryId = String.Empty;
            return false;
        }

        // static readonly string m_vivoxChannelById = "http://{0}/api2/viv_chan_mod.php?mode={1}&chan_id={2}&auth_token={3}";

        // XmlElement VivoxGetChannelById(string parent, string channelid)
        // {
        //     string requrl = String.Format(m_vivoxChannelById, m_vivoxServer, "get", channelid, m_authToken);

        //     if (parent != null && parent != String.Empty)
        //         return VivoxGetChild(parent, channelid);
        //     else
        //         return VivoxCall(requrl, true);
        // }

        /// <summary>
        ///     Delete a channel.
        ///     Once again, there a multitude of options possible. In the simplest case
        ///     we specify only the name and get a non-persistent cannel in return. Non
        ///     persistent means that the channel gets deleted if no-one uses it for
        ///     5 hours. To accommodate future requirements, it may be a good idea to
        ///     initially create channels under the umbrella of a parent ID based upon
        ///     the region name. That way we have a context for side channels, if those
        ///     are required in a later phase.
        ///     In this case the call handles parent and description as optional values.
        /// </summary>
        static readonly string m_vivoxChannelDel =
            "http://{0}/api2/viv_chan_mod.php?mode={1}&chan_id={2}&auth_token={3}";

        XmlElement VivoxDeleteChannel(string parent, string channelid)
        {
            string requrl = String.Format(m_vivoxChannelDel, m_vivoxServer, "delete", channelid, m_authToken);
            if (!string.IsNullOrEmpty(parent))
            {
                requrl = String.Format("{0}&chan_parent={1}", requrl, parent);
            }
            return VivoxCall(requrl, true);
        }

        /// <summary>
        ///     Return information on channels in the given directory
        /// </summary>
        static readonly string m_vivoxChannelSearch =
            "http://{0}/api2/viv_chan_search.php?&cond_chanparent={1}&auth_token={2}";

        XmlElement VivoxListChildren(string channelid)
        {
            string requrl = String.Format(m_vivoxChannelSearch, m_vivoxServer, channelid, m_authToken);
            return VivoxCall(requrl, true);
        }

        // XmlElement VivoxGetChild(string parent, string child)
        // {

        //     XmlElement children = VivoxListChildren(parent);
        //     string count;

        //    if (XmlFind(children, "response.level0.channel-search.count", out count))
        //     {
        //         int cnum = Convert.ToInt32(count);
        //         for (int i = 0; i < cnum; i++)
        //         {
        //             string name;
        //             string id;
        //             if (XmlFind(children, "response.level0.channel-search.channels.channels.level4.name", i, out name))
        //             {
        //                 if (name == child)
        //                 {
        //                    if (XmlFind(children, "response.level0.channel-search.channels.channels.level4.id", i, out id))
        //                     {
        //                         return VivoxGetChannelById(null, id);
        //                     }
        //                 }
        //             }
        //         }
        //     }

        //     // One we *know* does not exist.
        //     return VivoxGetChannel(null, Guid.NewGuid().ToString());

        // }

        /// <summary>
        ///     This method handles the WEB side of making a request over the
        ///     Vivox interface. The returned values are transferred to a hash
        ///     table which is returned as the result.
        ///     The outcome of the call can be determined by examining the
        ///     status value in the hash table.
        /// </summary>
        XmlElement VivoxCall(string requrl, bool admin)
        {
            XmlDocument doc;

            // If this is an admin call, and admin is not connected,
            // and the admin id cannot be connected, then fail.
            if (admin && !m_adminConnected && !DoAdminLogin())
                return null;

            doc = new XmlDocument();

            try
            {
                // Otherwise prepare the request
                MainConsole.Instance.TraceFormat("[VivoxVoice] Sending request <{0}>", requrl);

                HttpWebRequest req = (HttpWebRequest) WebRequest.Create(requrl);
                HttpWebResponse rsp;

                // We are sending just parameters, no content
                req.ContentLength = 0;

                // Send request and retrieve the response
                rsp = (HttpWebResponse) req.GetResponse();
                XmlTextReader rdr = null;
                try {
                    rdr = new XmlTextReader (rsp.GetResponseStream ());
                    doc.Load (rdr);
                    rdr.Close ();
                } catch {
                    if (rdr != null)
                        rdr.Close ();
                }
                rsp.Close ();
                
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[VivoxVoice] Error in admin call : {0}", e.Message);
            }

            // If we're debugging server responses, dump the whole
            // load now
            if (m_dumpXml) XmlScanl(doc.DocumentElement, 0);

            return doc.DocumentElement;
        }

        /// <summary>
        ///     Just say if it worked.
        /// </summary>
        bool IsOK(XmlElement resp)
        {
            string status;
            XmlFind(resp, "response.level0.status", out status);
            return (status == "OK");
        }

        /// <summary>
        ///     Login has been factored in this way because it gets called
        ///     from several places in the module, and we want it to work
        ///     the same way each time.
        /// </summary>
        bool DoAdminLogin()
        {
            MainConsole.Instance.Debug("[VivoxVoice] Establishing admin connection");

            lock (vlock)
            {
                if (!m_adminConnected)
                {
                    string status;
                    XmlElement resp;

                    resp = VivoxLogin(m_vivoxAdminUser, m_vivoxAdminPassword);

                    if (XmlFind(resp, "response.level0.body.status", out status))
                    {
                        if (status == "Ok")
                        {
                            MainConsole.Instance.Info("[VivoxVoice] Admin connection established");
                            if (XmlFind(resp, "response.level0.body.auth_token", out m_authToken))
                            {
                                if (m_dumpXml)
                                    MainConsole.Instance.TraceFormat("[VivoxVoice] Auth Token <{0}>",
                                                                     m_authToken);
                                m_adminConnected = true;
                            }
                        }
                        else
                        {
                            MainConsole.Instance.WarnFormat("[VivoxVoice] Admin connection failed, status = {0}",
                                                            status);
                        }
                    }
                }
            }

            return m_adminConnected;
        }

        /// <summary>
        ///     The XmlScan routine is provided to aid in the
        ///     reverse engineering of incompletely
        ///     documented packets returned by the Vivox
        ///     voice server. It is only called if the
        ///     m_dumpXml switch is set.
        /// </summary>
        void XmlScanl(XmlElement e, int index)
        {
            if (e.HasChildNodes)
            {
                MainConsole.Instance.TraceFormat("<{0}>".PadLeft(index + 5), e.Name);
                XmlNodeList children = e.ChildNodes;
                foreach (XmlNode node in children)
                    switch (node.NodeType)
                    {
                        case XmlNodeType.Element:
                            XmlScanl((XmlElement) node, index + 1);
                            break;
                        case XmlNodeType.Text:
                            MainConsole.Instance.DebugFormat("\"{0}\"".PadLeft(index + 5), node.Value);
                            break;
                    }
                MainConsole.Instance.TraceFormat("</{0}>".PadLeft(index + 6), e.Name);
            }
            else
            {
                MainConsole.Instance.TraceFormat("<{0}/>".PadLeft(index + 6), e.Name);
            }
        }

        static readonly char[] C_POINT = {'.'};

        /// <summary>
        ///     The Find method is passed an element whose
        ///     inner text is scanned in an attempt to match
        ///     the name hierarchy passed in the 'tag' parameter.
        ///     If the whole hierarchy is resolved, the InnerText
        ///     value at that point is returned. Note that this
        ///     may itself be a sub-hierarchy of the entire
        ///     document. The function returns a Boolean indicator
        ///     of the search's success. The search is performed
        ///     by the recursive Search method.
        /// </summary>
        bool XmlFind(XmlElement root, string tag, int nth, out string result)
        {
            if (root == null || tag == null || tag == String.Empty)
            {
                result = String.Empty;
                return false;
            }
            return XmlSearch(root, tag.Split(C_POINT), 0, ref nth, out result);
        }

        bool XmlFind(XmlElement root, string tag, out string result)
        {
            int nth = 0;
            if (root == null || tag == null || tag == String.Empty)
            {
                result = String.Empty;
                return false;
            }
            return XmlSearch(root, tag.Split(C_POINT), 0, ref nth, out result);
        }

        /// <summary>
        ///     XmlSearch is initially called by XmlFind, and then
        ///     recursively called by itself until the document
        ///     supplied to XmlFind is either exhausted or the name hierarchy
        ///     is matched.
        ///     If the hierarchy is matched, the value is returned in
        ///     result, and true returned as the function's
        ///     value. Otherwise the result is set to the empty string and
        ///     false is returned.
        /// </summary>
        bool XmlSearch(XmlElement e, string[] tags, int index, ref int nth, out string result)
        {
            if (index == tags.Length || e.Name != tags[index])
            {
                result = String.Empty;
                return false;
            }

            if (tags.Length - index == 1)
            {
                if (nth == 0)
                {
                    result = e.InnerText;
                    return true;
                }

                nth--;
                result = String.Empty;
                return false;
            }

            if (e.HasChildNodes)
            {
                XmlNodeList children = e.ChildNodes;
                foreach (XmlNode node in children)
                {
                    if (node.NodeType == XmlNodeType.Element)
                    {
                        if (XmlSearch((XmlElement) node, tags, index + 1, ref nth, out result))
                            return true;
                    }
                }
            }

            result = String.Empty;
            return false;
        }

        #endregion
    }

    public class VivoxVoiceCAPS : ICapsServiceConnector
    {
        protected IRegionClientCapsService m_service;
        protected IVoiceService m_voiceModule;

        public void RegisterCaps(IRegionClientCapsService service)
        {
            m_service = service;
            m_voiceModule = service.Registry.RequestModuleInterface<IVoiceService>();

            if (m_voiceModule != null)
            {
                service.AddStreamHandler("ProvisionVoiceAccountRequest",
                                         new GenericStreamHandler("POST",
                                                                  service.CreateCAPS("ProvisionVoiceAccountRequest", ""),
                                                                  ProvisionVoiceAccountRequest));
                service.AddStreamHandler("ParcelVoiceInfoRequest",
                                         new GenericStreamHandler("POST",
                                                                  service.CreateCAPS("ParcelVoiceInfoRequest", ""),
                                                                  ParcelVoiceInfoRequest));
            }
        }

        public void EnteringRegion()
        {
        }

        public void DeregisterCaps()
        {
            m_service.RemoveStreamHandler("ProvisionVoiceAccountRequest", "POST");
            m_service.RemoveStreamHandler("ParcelVoiceInfoRequest", "POST");
        }

        #region Incoming voice caps

        public byte[] ProvisionVoiceAccountRequest(string path, Stream request, OSHttpRequest httpRequest,
                                                   OSHttpResponse httpResponse)
        {
            try
            {
                string agentname, password, m_vivoxSipUri, m_vivoxVoiceAccountApi;
                m_voiceModule.VoiceAccountRequest(m_service, out agentname, out password, out m_vivoxSipUri,
                                                  out m_vivoxVoiceAccountApi);

                OSDMap map = new OSDMap();
                map["username"] = agentname;
                map["password"] = password;
                map["voice_sip_uri_hostname"] = m_vivoxSipUri;
                map["voice_account_server_name"] = m_vivoxVoiceAccountApi;

                MainConsole.Instance.DebugFormat("[VivoxVoice][PROVISIONVOICE]: avatar \"{0}\" added",
                                                 m_service.ClientCaps.AccountInfo.Name);

                return OSDParser.SerializeLLSDXmlBytes(map);
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[VivoxVoice][PROVISIONVOICE]: : {0}, retry later", e);
                return Encoding.UTF8.GetBytes("<llsd><undef /></llsd>");
            }
        }

        public byte[] ParcelVoiceInfoRequest(string path, Stream request, OSHttpRequest httpRequest,
                                             OSHttpResponse httpResponse)
        {
            // - check whether we have a region channel in our cache
            // - if not:
            //       create it and cache it
            // - send it to the client
            // - send channel_uri: as "sip:regionID@m_sipDomain"
            try
            {
                string channel_uri;
                int localID;

                m_voiceModule.ParcelVoiceRequest(m_service, out channel_uri, out localID);

                // fill in our response to the client
                OSDMap map = new OSDMap();
                map["region_name"] = m_service.Region.RegionName;
                map["parcel_local_id"] = localID;
                map["voice_credentials"] = new OSDMap();
                ((OSDMap) map["voice_credentials"])["channel_uri"] = channel_uri;

                MainConsole.Instance.DebugFormat(
                    "[VivoxVoice][PARCELVOICE]: region \"{0}\": Parcel ({1}): avatar \"{2}\"",
                    m_service.Region.RegionName, localID, m_service.ClientCaps.AccountInfo.Name);
                return OSDParser.SerializeLLSDXmlBytes(map);
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat(
                    "[VivoxVoice][PARCELVOICE]: region \"{0}\": avatar \"{1}\": {2}, retry later",
                    m_service.Region.RegionName, m_service.ClientCaps.AccountInfo.Name, e);

                return Encoding.UTF8.GetBytes("<llsd><undef /></llsd>");
            }
        }

        #endregion
    }
}