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
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Chat
{
    public class ChatModule : INonSharedRegionModule, IChatModule, IMuteListModule
    {
        const int DEBUG_CHANNEL = 2147483647;
        const int DEFAULT_CHANNEL = 0;
        readonly Dictionary<UUID, MuteList []> MuteListCache = new Dictionary<UUID, MuteList []> ();
        IScene m_Scene;

        IMuteListConnector MuteListConnector;
        IInstantMessagingService m_imService;
        internal IConfig m_config;

        bool m_enabled = true;
        float m_maxChatDistance = 100;
        int m_saydistance = 30;
        int m_shoutdistance = 256;
        bool m_useMuteListModule = true;
        int m_whisperdistance = 10;

        public float MaxChatDistance {
            get { return m_maxChatDistance; }
            set { m_maxChatDistance = value; }
        }

        #region IChatModule Members

        public int SayDistance {
            get { return m_saydistance; }
            set { m_saydistance = value; }
        }

        public int ShoutDistance {
            get { return m_shoutdistance; }
            set { m_shoutdistance = value; }
        }

        public int WhisperDistance {
            get { return m_whisperdistance; }
            set { m_whisperdistance = value; }
        }

        public IConfig Config {
            get { return m_config; }
        }

        /// <summary>
        ///     Send the message from the prim to the avatars in the regions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="c"></param>
        public virtual void OnChatFromWorld (object sender, OSChatMessage c)
        {
            // early return if not on public or debug channel
            if (c.Channel != DEFAULT_CHANNEL && c.Channel != DEBUG_CHANNEL)
                return;

            if (c.Range > m_maxChatDistance) //Check for max distance
                c.Range = m_maxChatDistance;

            DeliverChatToAvatars (ChatSourceType.Object, c);
        }

        public void SimChat (string message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                            UUID fromID, bool fromAgent, bool broadcast, float range, UUID toAgentID, IScene scene)
        {
            OSChatMessage args = new OSChatMessage {
                Message = message,
                Channel = channel,
                Type = type,
                Position = fromPos,
                Range = range,
                SenderUUID = fromID,
                Scene = scene,
                ToAgentID = toAgentID
            };


            if (fromAgent) {
                IScenePresence user = scene.GetScenePresence (fromID);
                if (user != null)
                    args.Sender = user.ControllingClient;
            } else {
                args.SenderObject = scene.GetSceneObjectPart (fromID);
            }

            args.From = fromName;
            //args.

            if (broadcast) {
                OnChatBroadcast (scene, args);
                scene.EventManager.TriggerOnChatBroadcast (scene, args);
            } else {
                OnChatFromWorld (scene, args);
                scene.EventManager.TriggerOnChatFromWorld (scene, args);
            }
        }

        public void SimChat (string message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                            UUID fromID, bool fromAgent, IScene scene)
        {
            SimChat (message, type, channel, fromPos, fromName, fromID, fromAgent, false, -1, UUID.Zero, scene);
        }

        /// <summary>
        ///     Say this message directly to a single person
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="channel"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        /// <param name="fromAgent"></param>
        /// <param name="toAgentID"></param>
        /// <param name="scene"></param>
        public void SimChatBroadcast (string message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                                     UUID fromAgentID, bool fromAgent, UUID toAgentID, IScene scene)
        {
            SimChat (message, type, channel, fromPos, fromName, fromAgentID, fromAgent, true, -1, toAgentID, scene);
        }

        public virtual void DeliverChatToAvatars (ChatSourceType sourceType, OSChatMessage c)
        {
            string fromName = c.From;
            UUID fromID = UUID.Zero;
            string message = c.Message;
            IScene scene = c.Scene;
            Vector3 fromPos = c.Position;

            if (c.Channel == DEBUG_CHANNEL)
                c.Type = ChatTypeEnum.DebugChannel;

            IScenePresence avatar = (scene != null && c.Sender != null)
                                        ? scene.GetScenePresence (c.Sender.AgentId)
                                        : null;
            switch (sourceType) {
            case ChatSourceType.Agent:
                if (scene != null) {
                    if (avatar != null && message == "") {
                        fromPos = avatar.AbsolutePosition;
                        fromName = avatar.Name;
                        fromID = c.Sender.AgentId;
                        //Always send this so it fires on typing start and end
                        IAttachmentsModule attMod = scene.RequestModuleInterface<IAttachmentsModule> ();
                        if (attMod != null)
                            attMod.SendScriptEventToAttachments (avatar.UUID, "changed", new object [] { Changed.STATE });
                    } else
                        fromID = c.SenderUUID;
                } else
                    fromID = c.SenderUUID;
                break;

            case ChatSourceType.Object:
                fromID = c.SenderUUID;

                break;
            }


            // from below it appears that if the source is an agent then do not send messge??
            if (sourceType == ChatSourceType.Agent)
                return;

            if (message.Length >= 1000) // libomv limit
                message = message.Substring (0, 1000);

            // determine who should receive the message
            var presences = m_Scene.GetScenePresences ();
            var fromRegionPos = fromPos;

            foreach (IScenePresence presence in presences) {
                if (presence.IsChildAgent)
                    continue;

                // check presence distances
                var toRegionPos = presence.AbsolutePosition;
                var dis = (int)Util.GetDistanceTo (toRegionPos, fromRegionPos);

                if (c.Type == ChatTypeEnum.Custom && dis > c.Range)                 // further than the defined custom range
                    continue;

                if (c.Type == ChatTypeEnum.Shout && dis > m_shoutdistance)          // too far for shouting
                    continue;

                if (c.Type == ChatTypeEnum.Say && dis > m_saydistance)              // too far for normal chat
                    continue;

                if (c.Type == ChatTypeEnum.Whisper && dis > m_whisperdistance)      // too far out for whisper
                    continue;

                if (avatar != null) {
                    if (avatar.CurrentParcelUUID != presence.CurrentParcelUUID)     // not in the same parcel
                        continue;

                    // If both are not in the same proviate parcel, don't send the chat message
                    if (!(avatar.CurrentParcel.LandData.Private && presence.CurrentParcel.LandData.Private))
                        continue;
                }

                // this one is good to go....
                TrySendChatMessage (presence, fromPos, fromID, fromName, c.Type, message, sourceType,
                        c.Range);
            }
            /* previous for reference - remove when verified - greythane -
            foreach (IScenePresence presence in from presence in m_Scene.GetScenePresences()
                                                where !presence.IsChildAgent
                                                let fromRegionPos = fromPos
                                                let toRegionPos = presence.AbsolutePosition
                                                let dis = (int) Util.GetDistanceTo(toRegionPos, fromRegionPos)
                                                where
                                                    (c.Type != ChatTypeEnum.Whisper || dis <= m_whisperdistance) &&
                                                    (c.Type != ChatTypeEnum.Say || dis <= m_saydistance) &&
                                                    (c.Type != ChatTypeEnum.Shout || dis <= m_shoutdistance) &&
                                                    (c.Type != ChatTypeEnum.Custom || dis <= c.Range)
                                                where
                                                    sourceType != ChatSourceType.Agent || avatar == null ||
                                                    avatar.CurrentParcel == null ||
                                                    (avatar.CurrentParcelUUID == presence.CurrentParcelUUID ||
                                                     (!avatar.CurrentParcel.LandData.Private &&
                                                      !presence.CurrentParcel.LandData.Private))
                                                select presence)
            {
                //If one of them is in a private parcel, and the other isn't in the same parcel, don't send the chat message
                TrySendChatMessage (presence, fromPos, fromID, fromName, c.Type, message, sourceType,
                    c.Range);
            }
            */
        }

        public virtual void TrySendChatMessage (IScenePresence presence, Vector3 fromPos,
                                               UUID fromAgentID, string fromName, ChatTypeEnum type,
                                               string message, ChatSourceType src, float range)
        {
            if (type == ChatTypeEnum.Custom) {
                int dis = (int)Util.GetDistanceTo (fromPos, presence.AbsolutePosition);
                //Set the best fitting setting for custom
                if (dis < m_whisperdistance)
                    type = ChatTypeEnum.Whisper;
                else if (dis > m_saydistance)
                    type = ChatTypeEnum.Shout;
                else if (dis > m_whisperdistance && dis < m_saydistance)
                    type = ChatTypeEnum.Say;
            }

            presence.ControllingClient.SendChatMessage (message, (byte)type, fromPos, fromName,
                fromAgentID, (byte)src, (byte)ChatAudibleLevel.Fully);
        }

        #endregion

        #region IChatModule

        public List<IChatPlugin> AllChatPlugins = new List<IChatPlugin> ();
        public Dictionary<string, IChatPlugin> ChatPlugins = new Dictionary<string, IChatPlugin> ();

        public void RegisterChatPlugin (string main, IChatPlugin plugin)
        {
            if (!ChatPlugins.ContainsKey (main))
                ChatPlugins.Add (main, plugin);
        }

        #endregion

        #region IMuteListModule Members

        /// <summary>
        ///     Get all the mutes from the database
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="cached"></param>
        /// <returns></returns>
        public MuteList [] GetMutes (UUID agentID, out bool cached)
        {
            cached = false;
            MuteList [] muteList = new MuteList [0];
            if (MuteListConnector == null)
                return muteList;
            lock (MuteListCache) {
                if (!MuteListCache.TryGetValue (agentID, out muteList)) {
                    muteList = MuteListConnector.GetMuteList (agentID).ToArray ();
                    MuteListCache.Add (agentID, muteList);
                } else
                    cached = true;
            }

            return muteList;
        }

        void UpdateCachedInfo (UUID agentID, CachedUserInfo info)
        {
            lock (MuteListCache)
                MuteListCache [agentID] = info.MuteList.ToArray ();
        }

        /// <summary>
        ///     Update the mute in the database
        /// </summary>
        /// <param name="muteID"></param>
        /// <param name="muteName"></param>
        /// <param name="flags"></param>
        /// <param name="agentID"></param>
        public void UpdateMuteList (UUID muteID, string muteName, int flags, UUID agentID)
        {
            if (muteID == UUID.Zero)
                return;
            MuteList Mute = new MuteList {
                MuteID = muteID,
                MuteName = muteName,
                MuteType = flags.ToString ()
            };
            MuteListConnector.UpdateMute (Mute, agentID);
            lock (MuteListCache)
                MuteListCache.Remove (agentID);
        }

        /// <summary>
        ///     Remove the given mute from the user's mute list in the database
        /// </summary>
        /// <param name="muteID"></param>
        /// <param name="muteName"></param>
        /// <param name="agentID"></param>
        public void RemoveMute (UUID muteID, string muteName, UUID agentID)
        {
            //Gets sent if a mute is not selected.
            if (muteID != UUID.Zero) {
                MuteListConnector.DeleteMute (muteID, agentID);
                lock (MuteListCache)
                    MuteListCache.Remove (agentID);
            }
        }

        #endregion

        #region INonSharedRegionModule Members

        public virtual void Initialise (IConfigSource config)
        {
            m_config = config.Configs ["Chat"];

            if (null == m_config) {
                MainConsole.Instance.Info ("[CHAT]: no config found, plugin disabled");
                m_enabled = false;
                return;
            }

            if (!m_config.GetBoolean ("enabled", true)) {
                MainConsole.Instance.Info ("[CHAT]: plugin disabled by configuration");
                m_enabled = false;
                return;
            }

            m_whisperdistance = m_config.GetInt ("whisper_distance", m_whisperdistance);
            m_saydistance = m_config.GetInt ("say_distance", m_saydistance);
            m_shoutdistance = m_config.GetInt ("shout_distance", m_shoutdistance);
            m_maxChatDistance = m_config.GetFloat ("max_chat_distance", m_maxChatDistance);

            var msgConfig = config.Configs ["Messaging"];
            m_useMuteListModule = (msgConfig.GetString ("MuteListModule", "ChatModule") == Name);
        }

        public virtual void AddRegion (IScene scene)
        {
            if (!m_enabled)
                return;

            m_Scene = scene;
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
            scene.EventManager.OnCachedUserInfo += UpdateCachedInfo;

            scene.RegisterModuleInterface<IMuteListModule> (this);
            scene.RegisterModuleInterface<IChatModule> (this);
            FindChatPlugins ();
            MainConsole.Instance.DebugFormat ("[CHAT]: Initialized for {0} w:{1} s:{2} S:{3}",
                scene.RegionInfo.RegionName, m_whisperdistance, m_saydistance, m_shoutdistance);
        }

        public virtual void RegionLoaded (IScene scene)
        {
            if (!m_enabled)
                return;

            if (m_useMuteListModule)
                MuteListConnector = Framework.Utilities.DataManager.RequestPlugin<IMuteListConnector> ();

            m_imService = scene.RequestModuleInterface<IInstantMessagingService> ();
        }

        public virtual void RemoveRegion (IScene scene)
        {
            if (!m_enabled)
                return;

            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
            scene.EventManager.OnCachedUserInfo -= UpdateCachedInfo;

            m_Scene = null;
            scene.UnregisterModuleInterface<IMuteListModule> (this);
            scene.UnregisterModuleInterface<IChatModule> (this);
        }

        public virtual void Close ()
        {
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public virtual string Name {
            get { return "ChatModule"; }
        }

        #endregion

        void FindChatPlugins ()
        {
            AllChatPlugins = WhiteCoreModuleLoader.PickupModules<IChatPlugin> ();
            foreach (IChatPlugin plugin in AllChatPlugins) {
                plugin.Initialize (this);
            }
        }

        void OnClosingClient (IClientAPI client)
        {
            client.OnChatFromClient -= OnChatFromClient;
            client.OnMuteListRequest -= OnMuteListRequest;
            client.OnUpdateMuteListEntry -= OnMuteListUpdate;
            client.OnRemoveMuteListEntry -= OnMuteListRemove;
            client.OnInstantMessage -= OnInstantMessage;
            //Tell all client plugins that the user left
            foreach (IChatPlugin plugin in AllChatPlugins) {
                plugin.OnClosingClient (client.AgentId, client.Scene);
            }
        }

        public virtual void OnNewClient (IClientAPI client)
        {
            client.OnChatFromClient += OnChatFromClient;
            client.OnMuteListRequest += OnMuteListRequest;
            client.OnUpdateMuteListEntry += OnMuteListUpdate;
            client.OnRemoveMuteListEntry += OnMuteListRemove;
            client.OnInstantMessage += OnInstantMessage;

            //Tell all the chat plugins about the new user
            foreach (IChatPlugin plugin in AllChatPlugins) {
                plugin.OnNewClient (client);
            }
        }

        /// <summary>
        ///     Set the correct position for the chat message
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        protected OSChatMessage FixPositionOfChatMessage (OSChatMessage c)
        {
            IScenePresence avatar;
            if ((avatar = c.Scene.GetScenePresence (c.Sender.AgentId)) != null)
                c.Position = avatar.AbsolutePosition;

            return c;
        }

        /// <summary>
        ///     New chat message from the client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="c"></param>
        protected virtual void OnChatFromClient (IClientAPI sender, OSChatMessage c)
        {
            c = FixPositionOfChatMessage (c);

            // redistribute to interested subscribers
            if (c.Message != "")
                c.Scene.EventManager.TriggerOnChatFromClient (sender, c);

            // early return if not on public or debug channel
            if (c.Channel != DEFAULT_CHANNEL && c.Channel != DEBUG_CHANNEL)
                return;

            // sanity check:
            if (c.Sender == null) {
                MainConsole.Instance.ErrorFormat ("[CHAT] OnChatFromClient from {0} has empty Sender field!", sender);
                return;
            }

            //If the message is not blank, tell the plugins about it
            if (c.Message != "") {
                foreach (
                    string pluginMain in
                        ChatPlugins.Keys.Where (
                            pluginMain => pluginMain == "all" || c.Message.StartsWith (pluginMain + ".", StringComparison.Ordinal))) {
                    IChatPlugin plugin;
                    ChatPlugins.TryGetValue (pluginMain, out plugin);
                    //If it returns false, stop the message from being sent
                    if (!plugin.OnNewChatMessageFromWorld (c, out c))
                        return;
                }
            }
            string Name2 = "";
            if (sender != null) {
                Name2 = (sender).Name;
            }
            c.From = Name2;

            DeliverChatToAvatars (ChatSourceType.Agent, c);
        }

        protected virtual void OnChatBroadcast (object sender, OSChatMessage c)
        {
            // unless the chat to be broadcast is of type Region, we
            // drop it if its channel is neither 0 nor DEBUG_CHANNEL
            if (c.Channel != DEFAULT_CHANNEL && c.Channel != DEBUG_CHANNEL && c.Type != ChatTypeEnum.Region)
                return;

            ChatTypeEnum cType = c.Type;
            if (c.Channel == DEBUG_CHANNEL)
                cType = ChatTypeEnum.DebugChannel;

            if (c.Range > m_maxChatDistance)
                c.Range = m_maxChatDistance;

            if (cType == ChatTypeEnum.SayTo)
                //Change to something client can understand as SayTo doesn't exist except on the server
                cType = ChatTypeEnum.Owner;

            if (cType == ChatTypeEnum.Region)
                cType = ChatTypeEnum.Say;

            if (c.Message.Length > 1100)
                c.Message = c.Message.Substring (0, 1000);

            // broadcast chat works by redistributing every incoming chat
            // message to each avatar in the scene.
            string fromName = c.From;

            UUID fromID = UUID.Zero;
            ChatSourceType sourceType = ChatSourceType.Object;
            if (null != c.Sender) {
                IScenePresence avatar = c.Scene.GetScenePresence (c.Sender.AgentId);
                fromID = c.Sender.AgentId;
                fromName = avatar.Name;
                sourceType = ChatSourceType.Agent;
            }

            // MainConsole.Instance.DebugFormat("[CHAT] Broadcast: fromID {0} fromName {1}, cType {2}, sType {3}", fromID, fromName, cType, sourceType);

            c.Scene.ForEachScenePresence (
                delegate (IScenePresence presence) {
                    // ignore chat from child agents
                    if (presence.IsChildAgent)
                        return;

                    IClientAPI client = presence.ControllingClient;

                    // don't forward SayOwner chat from objects to
                    // non-owner agents
                    if ((c.Type == ChatTypeEnum.Owner) &&
                            (null != c.SenderObject) &&
                            (c.SenderObject.OwnerID != client.AgentId))
                        return;

                    // don't forward SayTo chat from objects to
                    // non-targeted agents
                    if ((c.Type == ChatTypeEnum.SayTo) &&
                            (c.ToAgentID != client.AgentId))
                        return;

                    bool cached;
                    MuteList [] mutes = GetMutes (client.AgentId, out cached);
                    foreach (MuteList m in mutes)
                        if (m.MuteID == c.SenderUUID ||
                                (c.SenderObject != null && m.MuteID == c.SenderObject.ParentEntity.UUID))
                            return;

                    client.SendChatMessage (
                        c.Message,
                        (byte)cType,
                        new Vector3 (client.Scene.RegionInfo.RegionSizeX * 0.5f,
                        client.Scene.RegionInfo.RegionSizeY * 0.5f, 30),
                        fromName,
                        fromID,
                        (byte)sourceType,
                        (byte)ChatAudibleLevel.Fully);
                });
        }


        /// <summary>
        ///     Get all the mutes the client has set
        /// </summary>
        /// <param name="client"></param>
        /// <param name="crc"></param>
        void OnMuteListRequest (IClientAPI client, uint crc)
        {
            if (!m_useMuteListModule)
                return;
            //Sends the name of the file being sent by the xfer module DO NOT EDIT!!!
            string filename = "mutes" + client.AgentId;
            byte [] fileData = new byte [0];
            string invString = "";
            int i = 0;
            bool cached;
            MuteList [] muteList = GetMutes (client.AgentId, out cached);
            if (muteList == null)
                return;
            /*if (cached)
            {
                client.SendUseCachedMuteList();
                return;
            }*/

            Dictionary<UUID, bool> cache = new Dictionary<UUID, bool> ();
            foreach (MuteList mute in muteList) {
                cache [mute.MuteID] = true;
                invString += (mute.MuteType + " " + mute.MuteID + " " + mute.MuteName + " |\n");
                i++;
            }

            if (invString != "")
                invString = invString.Remove (invString.Length - 3, 3);

            fileData = Utils.StringToBytes (invString);
            IXfer xfer = client.Scene.RequestModuleInterface<IXfer> ();
            if (xfer != null) {
                xfer.AddNewFile (filename, fileData);
                client.SendMuteListUpdate (filename);
            }
        }

        /// <summary>
        ///     Update the mute (from the client)
        /// </summary>
        /// <param name="client"></param>
        /// <param name="muteID"></param>
        /// <param name="muteName"></param>
        /// <param name="flags"></param>
        /// <param name="agentID"></param>
        void OnMuteListUpdate (IClientAPI client, UUID muteID, string muteName, int flags, UUID agentID)
        {
            if (!m_useMuteListModule)
                return;
            UpdateMuteList (muteID, muteName, flags, client.AgentId);
            OnMuteListRequest (client, 0);
        }

        /// <summary>
        ///     Remove the mute (from the client)
        /// </summary>
        /// <param name="client"></param>
        /// <param name="muteID"></param>
        /// <param name="muteName"></param>
        /// <param name="agentID"></param>
        void OnMuteListRemove (IClientAPI client, UUID muteID, string muteName, UUID agentID)
        {
            if (!m_useMuteListModule)
                return;
            RemoveMute (muteID, muteName, client.AgentId);
            OnMuteListRequest (client, 0);
        }

        /// <summary>
        ///     Find the presence from all the known sims
        /// </summary>
        /// <param name="avID"></param>
        /// <returns></returns>
        public IScenePresence FindScenePresence (UUID avID)
        {
            return m_Scene.GetScenePresence (avID);
        }

        /// <summary>
        ///     If its a message we deal with, pull it from the client here
        /// </summary>
        /// <param name="client"></param>
        /// <param name="im"></param>
        void OnInstantMessage (IClientAPI client, GridInstantMessage im)
        {
            byte dialog = im.Dialog;
            switch (dialog) {
            case (byte)InstantMessageDialog.SessionGroupStart:
                m_imService.CreateGroupChat (client.AgentId, im);
                break;
            case (byte)InstantMessageDialog.SessionSend:
                m_imService.SendChatToSession (client.AgentId, im);
                break;
            case (byte)InstantMessageDialog.SessionDrop:
                m_imService.DropMemberFromSession (client.AgentId, im);
                break;
            }
        }
    }
}