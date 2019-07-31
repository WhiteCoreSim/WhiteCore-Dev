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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Web;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services
{
    public class DisplayNamesCAPS : ICapsServiceConnector
    {
        List<string> bannedNames = new List<string> ();
        IEventQueueService m_eventQueue;
        IProfileConnector m_profileConnector;
        IRegionClientCapsService m_service;
        IUserAccountService m_userService;

        double m_update_days = 0;

        #region ICapsServiceConnector Members

        public void RegisterCaps (IRegionClientCapsService service)
        {
            var cfgservice = service.ClientCaps.Registry.RequestModuleInterface<ISimulationBase> ();
            var displayNamesConfig = cfgservice.ConfigSource.Configs ["DisplayNames"];
            if (displayNamesConfig != null) {
                if (!displayNamesConfig.GetBoolean ("Enabled", true))
                    return;

                string bannedNamesString = displayNamesConfig.GetString ("BannedUserNames", "");
                if (bannedNamesString != "")
                    bannedNames = new List<string> (bannedNamesString.Split (','));

                m_update_days = displayNamesConfig.GetDouble ("UpdateDays", m_update_days);

            }
            m_service = service;
            m_profileConnector = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ();
            m_eventQueue = service.Registry.RequestModuleInterface<IEventQueueService> ();
            m_userService = service.Registry.RequestModuleInterface<IUserAccountService> ();

            string post = CapsUtil.CreateCAPS ("SetDisplayName", "");
            service.AddStreamHandler ("SetDisplayName", new GenericStreamHandler ("POST", post, ProcessSetDisplayName));

            post = CapsUtil.CreateCAPS ("GetDisplayNames", "");
            service.AddStreamHandler ("GetDisplayNames", new GenericStreamHandler ("GET", post, ProcessGetDisplayName));
        }

        public void EnteringRegion ()
        {
        }

        public void DeregisterCaps ()
        {
            if (m_service == null)          // If display names aren't enabled
                return;

            m_service.RemoveStreamHandler ("SetDisplayName", "POST");
            m_service.RemoveStreamHandler ("GetDisplayNames", "GET");
        }

        #endregion

        #region Caps Messages

        /// <summary>
        ///     Set the display name for the given user
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <returns></returns>
        byte [] ProcessSetDisplayName (string path, Stream request,
                                     OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            try {
                OSDMap rm = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
                OSDArray display_name = (OSDArray)rm ["display_name"];
                string oldDisplayName = display_name [0].AsString ();
                string newDisplayName = display_name [1].AsString ();

                //Check to see if their name contains a banned character
                if (bannedNames.Select (bannedUserName => bannedUserName.Replace (" ", ""))
                               .Any (BannedUserName => newDisplayName.ToLower ().Contains (BannedUserName.ToLower ()))) {
                    newDisplayName = m_service.ClientCaps.AccountInfo.Name;
                }

                IUserProfileInfo info = m_profileConnector.GetUserProfile (m_service.AgentID);
                if (info == null) {
                    //m_avatar.ControllingClient.SendAlertMessage ("You cannot update your display name currently as your profile cannot be found.");
                } else {
                    //Set the name
                    info.DisplayName = newDisplayName;
                    info.DisplayNameUpdated = DateTime.UtcNow;
                    m_profileConnector.UpdateUserProfile (info);

                    var nextUpdate = info.DisplayNameUpdated.AddDays (m_update_days);

                    //One for us
                    DisplayNameUpdate (newDisplayName, oldDisplayName, m_service.ClientCaps.AccountInfo, m_service.AgentID, nextUpdate);

                    foreach (
                        IRegionClientCapsService avatar in
                            m_service.RegionCaps.GetClients ().Where (avatar => avatar.AgentID != m_service.AgentID)) {
                        //Update all others
                        DisplayNameUpdate (newDisplayName, oldDisplayName, m_service.ClientCaps.AccountInfo, avatar.AgentID, nextUpdate);
                    }
                    //The reply
                    SetDisplayNameReply (newDisplayName, oldDisplayName, m_service.ClientCaps.AccountInfo, nextUpdate);

                }
            } catch {
                // nothing to do 
            }

            return MainServer.BlankResponse;
        }

        /// <summary>
        ///     Get the user's display name, currently not used?
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <returns></returns>
        byte [] ProcessGetDisplayName (string path, Stream request,
                                     OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            //I've never seen this come in, so for now... do nothing
            NameValueCollection query = HttpUtility.ParseQueryString (httpRequest.Url.Query);
            string [] ids = query.GetValues ("ids");
            string username = query.GetOne ("username");

            OSDMap map = new OSDMap ();
            OSDArray agents = new OSDArray ();
            OSDArray bad_ids = new OSDArray ();
            OSDArray bad_usernames = new OSDArray ();

            if (ids != null) {
                foreach (string id in ids) {
                    UserAccount userAcct = m_userService.GetUserAccount (m_service.ClientCaps.AccountInfo.AllScopeIDs,
                                                                       UUID.Parse (id));
                    if (userAcct.Valid) {
                        IUserProfileInfo info =
                            Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ()
                                  .GetUserProfile (userAcct.PrincipalID);
                        if (info != null)
                            PackUserInfo (info, userAcct, ref agents);
                        else
                            PackUserInfo (new IUserProfileInfo (), userAcct, ref agents);
                        //else //Technically is right, but needs to be packed no matter what for OS based grids
                        //    bad_ids.Add (id);
                    }
                }
            } else if (username != null) {
                UserAccount userAcct = m_userService.GetUserAccount (m_service.ClientCaps.AccountInfo.AllScopeIDs,
                                                                   username.Replace ('.', ' '));
                if (userAcct.Valid) {
                    var info = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ().GetUserProfile (userAcct.PrincipalID);
                    if (info != null)
                        PackUserInfo (info, userAcct, ref agents);
                    else
                        bad_usernames.Add (username);
                }
            }

            map ["agents"] = agents;
            map ["bad_ids"] = bad_ids;
            map ["bad_usernames"] = bad_usernames;

            return OSDParser.SerializeLLSDXmlBytes (map);
        }

        void PackUserInfo (IUserProfileInfo info, UserAccount account, ref OSDArray agents)
        {
            OSDMap agentMap = new OSDMap ();
            agentMap ["username"] = account.Name;
            agentMap ["display_name"] = (info == null || info.DisplayName == "") ? account.Name : info.DisplayName;
            agentMap ["legacy_first_name"] = account.FirstName;
            agentMap ["legacy_last_name"] = account.LastName;
            agentMap ["id"] = account.PrincipalID;
            agentMap ["is_display_name_default"] = isDefaultDisplayName (account.FirstName, account.LastName, account.Name,
                                                                       info == null ? account.Name : info.DisplayName);
            if (info != null) {
                if (m_update_days > 0)
                    agentMap ["display_name_next_update"] = OSD.FromDate (info.DisplayNameUpdated.AddDays (m_update_days));
                else
                    agentMap ["display_name_next_update"] = OSD.FromDate (info.DisplayNameUpdated);
            }

            agents.Add (agentMap);
        }

        #region Event Queue

        /// <summary>
        ///     Send the user a display name update
        /// </summary>
        /// <param name="newDisplayName"></param>
        /// <param name="oldDisplayName"></param>
        /// <param name="infoFromAv"></param>
        /// <param name="toAgentID"></param>
        void DisplayNameUpdate (string newDisplayName, string oldDisplayName, UserAccount infoFromAv,
                               UUID toAgentID, DateTime nextUpdate)
        {
            if (m_eventQueue != null) {
                //If the DisplayName is blank, the client refuses to do anything, so we send the name by default
                if (newDisplayName == "")
                    newDisplayName = infoFromAv.Name;

                bool isDefaultName = isDefaultDisplayName (infoFromAv.FirstName, infoFromAv.LastName, infoFromAv.Name,
                                                          newDisplayName);

                OSD item = DisplayNameUpdate (newDisplayName, oldDisplayName, infoFromAv.PrincipalID, isDefaultName,
                                             infoFromAv.FirstName, infoFromAv.LastName,
                                             infoFromAv.FirstName + "." + infoFromAv.LastName,
                                             nextUpdate);
                m_eventQueue.Enqueue (item, toAgentID, m_service.Region.RegionID);
            }
        }

        static bool isDefaultDisplayName (string first, string last, string name, string displayName)
        {
            if (displayName == name)
                return true;
            return displayName == first + "." + last;
        }

        /// <summary>
        ///     Reply to the set display name reply
        /// </summary>
        /// <param name="newDisplayName"></param>
        /// <param name="oldDisplayName"></param>
        /// <param name="mAvatar"></param>
        public void SetDisplayNameReply (string newDisplayName, string oldDisplayName, UserAccount mAvatar, DateTime nextUpdate)
        {
            if (m_eventQueue != null) {
                bool isDefaultName = isDefaultDisplayName (mAvatar.FirstName, mAvatar.LastName, mAvatar.Name,
                                                          newDisplayName);

                OSD item = DisplayNameReply (newDisplayName, oldDisplayName, mAvatar.PrincipalID, isDefaultName,
                                            mAvatar.FirstName, mAvatar.LastName,
                                            mAvatar.FirstName + "." + mAvatar.LastName,
                                            nextUpdate);
                m_eventQueue.Enqueue (item, mAvatar.PrincipalID, m_service.Region.RegionID);
            }
        }

        /// <summary>
        ///     Tell the user about an update
        /// </summary>
        /// <param name="newDisplayName"></param>
        /// <param name="oldDisplayName"></param>
        /// <param name="iD"></param>
        /// <param name="isDefault"></param>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="account"></param>
        /// <returns></returns>

        public OSD DisplayNameUpdate (string newDisplayName, string oldDisplayName, UUID iD, bool isDefault, string first,
                                     string last, string account, DateTime nextUpdate)
        {
            OSDMap nameReply = new OSDMap { { "message", OSD.FromString ("DisplayNameUpdate") } };

            OSDMap body = new OSDMap ();

            OSDMap agentData = new OSDMap ();
            agentData ["display_name"] = OSD.FromString (newDisplayName);
            agentData ["id"] = OSD.FromUUID (iD);
            agentData ["is_display_name_default"] = OSD.FromBoolean (isDefault);
            agentData ["legacy_first_name"] = OSD.FromString (first);
            agentData ["legacy_last_name"] = OSD.FromString (last);
            agentData ["username"] = OSD.FromString (account);
            agentData ["display_name_next_update"] = OSD.FromDate (nextUpdate);

            body.Add ("agent", agentData);
            body.Add ("agent_id", OSD.FromUUID (iD));
            body.Add ("old_display_name", OSD.FromString (oldDisplayName));

            nameReply.Add ("body", body);

            return nameReply;
        }

        /// <summary>
        ///     Send back a user's display name
        /// </summary>
        /// <param name="newDisplayName"></param>
        /// <param name="oldDisplayName"></param>
        /// <param name="iD"></param>
        /// <param name="isDefault"></param>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="account"></param>
        /// <returns></returns>
        OSD DisplayNameReply (string newDisplayName, string oldDisplayName, UUID iD, bool isDefault, string first,
                                    string last, string account, DateTime nextUpdate)
        {
            OSDMap nameReply = new OSDMap ();

            OSDMap body = new OSDMap ();
            OSDMap content = new OSDMap ();
            OSDMap agentData = new OSDMap ();

            content.Add ("display_name", OSD.FromString (newDisplayName));
            content.Add ("display_name_next_update", OSD.FromDate (nextUpdate));
            content.Add ("id", OSD.FromUUID (iD));
            content.Add ("is_display_name_default", OSD.FromBoolean (isDefault));
            content.Add ("legacy_first_name", OSD.FromString (first));
            content.Add ("legacy_last_name", OSD.FromString (last));
            content.Add ("username", OSD.FromString (account));

            body.Add ("content", content);
            body.Add ("agent", agentData);
            //body.Add ("old_display_name", OSD.FromString (oldDisplayName));
            body.Add ("reason", OSD.FromString ("OK"));
            body.Add ("status", OSD.FromInteger (200));

            nameReply.Add ("body", body);
            nameReply.Add ("message", OSD.FromString ("SetDisplayNameReply"));

            return nameReply;
        }

        #endregion

        #endregion
    }
}
