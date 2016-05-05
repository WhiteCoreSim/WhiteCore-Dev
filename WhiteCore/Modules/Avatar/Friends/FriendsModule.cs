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


using System;
using System.Collections.Generic;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Other;
using FriendInfo = WhiteCore.Framework.Services.FriendInfo;

namespace WhiteCore.Modules.Friends
{
    public class FriendsModule : INonSharedRegionModule, IFriendsModule
    {
        protected Dictionary<UUID, List<FriendInfo>> m_Friends =
            new Dictionary<UUID, List<FriendInfo>> ();
        protected Dictionary<UUID, List<UUID>> m_FriendOnlineStatuses =
            new Dictionary<UUID, List<UUID>> ();

        protected IScene m_scene;
        public bool m_enabled = true;
        protected bool m_firstStart = true;

        protected IFriendsService FriendsService
        {
            get { return m_scene.RequestModuleInterface<IFriendsService> (); }
        }

        protected IGridService GridService
        {
            get { return m_scene.GridService; }
        }

        public IUserAccountService UserAccountService
        {
            get { return m_scene.UserAccountService; }
        }

        public ISyncMessagePosterService SyncMessagePosterService
        {
            get { return m_scene.RequestModuleInterface<ISyncMessagePosterService> (); }
        }

        public ISyncMessageRecievedService AsyncMessageRecievedService
        {
            get { return m_scene.RequestModuleInterface<ISyncMessageRecievedService> (); }
        }

        #region IFriendsModule Members

        public int GetFriendPerms (UUID principalID, UUID friendID)
        {
            FriendInfo[] friends = GetFriends (principalID);
            foreach (FriendInfo fi in friends.Where(fi => fi.Friend == friendID.ToString()))
            {
                return fi.TheirFlags;
            }

            return -1;
        }

        public void SendFriendsStatusMessage (UUID FriendToInformID, UUID[] userIDs, bool online)
        {
            // Try local
            IClientAPI friendClient = LocateClientObject (FriendToInformID);
            if (friendClient != null)
            {
                MainConsole.Instance.InfoFormat ("[FriendsModule]: Local Status Notify {0} that {1} users are {2}", FriendToInformID, userIDs.Length, online);
                // the  friend in this sim as root agent
                if (online)
                    friendClient.SendAgentOnline (userIDs);
                else
                    friendClient.SendAgentOffline (userIDs);
                // we're done
                return;
            }

            MainConsole.Instance.ErrorFormat ("[FriendsModule]: Could not send status update to non-existent client {0}.", 
                FriendToInformID);

        }

        public FriendInfo[] GetFriends (UUID agentID)
        {
            List<FriendInfo> friends = new List<FriendInfo> ();
            lock (m_Friends)
            {
                if (m_Friends.TryGetValue (agentID, out friends))
                    return friends.ToArray ();
            }

            return new FriendInfo[0];
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise (IConfigSource config)
        {
        }

        public void Close ()
        {
        }

        public void AddRegion (IScene scene)
        {
            if (!m_enabled)
                return;

            m_scene = scene;
            scene.RegisterModuleInterface<IFriendsModule> (this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
            scene.EventManager.OnCachedUserInfo += UpdateCachedInfo;
            scene.EventManager.OnMakeRootAgent += MakeRootAgent;
        }

        public void RegionLoaded (IScene scene)
        {
            if (m_firstStart)
                AsyncMessageRecievedService.OnMessageReceived += OnMessageReceived;
            m_firstStart = false;
        }

        public void RemoveRegion (IScene scene)
        {
            if (!m_enabled)
                return;

            m_scene = null;
            scene.UnregisterModuleInterface<IFriendsModule> (this);

            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
            scene.EventManager.OnCachedUserInfo -= UpdateCachedInfo;
            scene.EventManager.OnMakeRootAgent -= MakeRootAgent;
        }

        public string Name
        {
            get { return "FriendsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        void UpdateCachedInfo (UUID agentID, CachedUserInfo info)
        {
            lock (m_FriendOnlineStatuses)
            {
                if (info.FriendOnlineStatuses.Count > 0)
                    m_FriendOnlineStatuses [agentID] = info.FriendOnlineStatuses;
                else
                    m_FriendOnlineStatuses.Remove (agentID);
            }
            lock (m_Friends)
                m_Friends [agentID] = info.Friends;
        }

        void MakeRootAgent (IScenePresence presence)
        {
            lock (m_FriendOnlineStatuses)
            {
                List<UUID> friendOnlineStatuses;
                if (m_FriendOnlineStatuses.TryGetValue (presence.UUID, out friendOnlineStatuses))
                {
                    SendFriendsStatusMessage (presence.UUID, friendOnlineStatuses.ToArray (), true);
                    m_FriendOnlineStatuses.Remove (presence.UUID);
                }
            }
        }

        protected OSDMap OnMessageReceived (OSDMap message)
        {
            if (!message.ContainsKey ("Method"))
                return null;
            if (message ["Method"] == "FriendGrantRights")
            {
                UUID Requester = message ["Requester"].AsUUID ();
                UUID Target = message ["Target"].AsUUID ();
                int MyFlags = message ["MyFlags"].AsInteger ();
                int Rights = message ["Rights"].AsInteger ();
                LocalGrantRights (Requester, Target, MyFlags, Rights);
            } else if (message ["Method"] == "FriendTerminated")
            {
                UUID Requester = message ["Requester"].AsUUID ();
                UUID ExFriend = message ["ExFriend"].AsUUID ();
                LocalFriendshipTerminated (ExFriend, Requester);
            } else if (message ["Method"] == "FriendshipOffered")
            {
                //UUID Requester = message["Requester"].AsUUID();
                UUID Friend = message ["Friend"].AsUUID ();
                GridInstantMessage im = new GridInstantMessage ();
                im.FromOSD ((OSDMap)message ["Message"]);
                LocalFriendshipOffered (Friend, im);
            } else if (message ["Method"] == "FriendshipDenied")
            {
                UUID Requester = message ["Requester"].AsUUID ();
                string ClientName = message ["ClientName"].AsString ();
                UUID FriendID = message ["FriendID"].AsUUID ();
                LocalFriendshipDenied (Requester, ClientName, FriendID);
            } else if (message ["Method"] == "FriendshipApproved")
            {
                UUID Requester = message ["Requester"].AsUUID ();
                string ClientName = message ["ClientName"].AsString ();
                UUID FriendID = message ["FriendID"].AsUUID ();
                LocalFriendshipApproved (Requester, ClientName, null, FriendID);
            }
            return null;
        }

        void OnClosingClient (IClientAPI client)
        {
            client.OnInstantMessage -= OnInstantMessage;
            client.OnApproveFriendRequest -= OnApproveFriendRequest;
            client.OnDenyFriendRequest -= OnDenyFriendRequest;
            client.OnTerminateFriendship -= OnTerminateFriendship;
            client.OnGrantUserRights -= OnGrantUserRights;
        }

        void OnNewClient (IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
            client.OnApproveFriendRequest += OnApproveFriendRequest;
            client.OnDenyFriendRequest += OnDenyFriendRequest;
            client.OnTerminateFriendship += OnTerminateFriendship;
            client.OnGrantUserRights += OnGrantUserRights;
            OfflineFriendRequest (client);
        }

        /// <summary>
        ///     Find the client for a ID
        /// </summary>
        public IClientAPI LocateClientObject (UUID agentID)
        {
            IScenePresence presence;
            foreach (IScene scene in MainConsole.Instance.ConsoleScenes)
            {
                presence = scene.GetScenePresence (agentID);
                if (presence != null)
                    return presence.ControllingClient;
            }
            return null;
        }

        void OnInstantMessage (IClientAPI client, GridInstantMessage im)
        {
            if ((InstantMessageDialog)im.Dialog == InstantMessageDialog.FriendshipOffered)
            {
                // we got a friendship offer
                UUID principalID = im.FromAgentID;
                UUID friendID = im.ToAgentID;

                //Can't trust the incoming name for friend offers, so we have to find it ourselves.
                UserAccount sender = m_scene.UserAccountService.GetUserAccount (m_scene.RegionInfo.AllScopeIDs,
                                         principalID);
                im.FromAgentName = sender.Name;
                UserAccount reciever = m_scene.UserAccountService.GetUserAccount (m_scene.RegionInfo.AllScopeIDs,
                                           friendID);

                MainConsole.Instance.DebugFormat ("[FRIENDS]: {0} offered friendship to {1}", sender.Name, reciever.Name);
                // This user wants to be friends with the other user.
                // Let's add the relation backwards, in case the other is not online
                FriendsService.StoreFriend (friendID, principalID.ToString (), 0);

                // Now let's ask the other user to be friends with this user
                ForwardFriendshipOffer (principalID, friendID, im);
            }
        }

        void ForwardFriendshipOffer (UUID agentID, UUID friendID, GridInstantMessage im)
        {
            // !!!!!!!! This is a hack so that we don't have to keep state (transactionID/imSessionID)
            // We stick this agent's ID as imSession, so that it's directly available on the receiving end
            im.SessionID = im.FromAgentID;

            // Try the local sim
            UserAccount account = UserAccountService.GetUserAccount (m_scene.RegionInfo.AllScopeIDs, agentID);
            im.FromAgentName = (account == null) ? "Unknown" : account.Name;

            if (LocalFriendshipOffered (friendID, im))
                return;

            // The prospective friend is not here [as root]. Let's4 forward.
            SyncMessagePosterService.PostToServer (SyncMessageHelper.FriendshipOffered (agentID, friendID, im,
                m_scene.RegionInfo.RegionID));
            // If the prospective friend is not online, they will get the message upon login.
        }

        void OnApproveFriendRequest (IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            MainConsole.Instance.DebugFormat ("[FRIENDS]: {0} accepted friendship from {1}", agentID, friendID);

            FriendsService.StoreFriend (agentID, friendID.ToString (), 1);
            FriendsService.StoreFriend (friendID, agentID.ToString (), 1);

            // Update the local cache
            UpdateFriendsCache (agentID);

            //
            // Notify the friend
            //

            //
            // Send calling card to the local user
            //

            ICallingCardModule ccmodule = client.Scene.RequestModuleInterface<ICallingCardModule> ();
            if (ccmodule != null)
            {
                UserAccount account = client.Scene.UserAccountService.GetUserAccount (client.AllScopeIDs, friendID);
                UUID folderID =
                    client.Scene.InventoryService.GetFolderForType (agentID, InventoryType.Unknown, FolderType.CallingCard).ID;
                if (account != null)
                    ccmodule.CreateCallingCard (client, friendID, folderID, account.Name);
            }

            // Try Local
            if (LocalFriendshipApproved (agentID, client.Name, client, friendID))
                return;
            SyncMessagePosterService.PostToServer (SyncMessageHelper.FriendshipApproved (
                agentID, client.Name, friendID, m_scene.RegionInfo.RegionID));
        }

        void OnDenyFriendRequest (IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            MainConsole.Instance.DebugFormat ("[FRIENDS]: {0} denied friendship to {1}", agentID, friendID);


            var friendRequests = FriendsService.GetFriendsRequest (agentID);
            if (friendRequests == null)
                return;

            FriendInfo [] friends = friendRequests.ToArray ();
            foreach (FriendInfo fi in friends)
            {
                if (fi.MyFlags == 0)
                {
                    UUID fromAgentID;
                    if (!UUID.TryParse (fi.Friend, out fromAgentID))
                        continue;
                    if (fromAgentID == friendID) //Get those pesky HG travelers as well
                        FriendsService.Delete (agentID, fi.Friend);
                }
            }
            FriendsService.Delete (friendID, agentID.ToString ());

            //
            // Notify the friend
            //

            // Try local
            if (LocalFriendshipDenied (agentID, client.Name, friendID))
                return;
            SyncMessagePosterService.PostToServer (SyncMessageHelper.FriendshipDenied (
                agentID, client.Name, friendID, m_scene.RegionInfo.RegionID));
        }

        void OnTerminateFriendship (IClientAPI client, UUID agentID, UUID exfriendID)
        {
            FriendsService.Delete (agentID, exfriendID.ToString ());
            FriendsService.Delete (exfriendID, agentID.ToString ());

            // Update local cache
            UpdateFriendsCache (agentID);

            client.SendTerminateFriend (exfriendID);

            //
            // Notify the friend
            //

            // Try local
            if (LocalFriendshipTerminated (exfriendID, agentID))
                return;

            SyncMessagePosterService.PostToServer (SyncMessageHelper.FriendTerminated (
                agentID, exfriendID, m_scene.RegionInfo.RegionID));
        }

        void OnGrantUserRights (IClientAPI remoteClient, UUID requester, UUID target, int rights)
        {
            FriendInfo[] friends = GetFriends (remoteClient.AgentId);
            if (friends.Length == 0)
                return;

            MainConsole.Instance.DebugFormat ("[FRIENDS MODULE]: User {0} changing rights to {1} for friend {2}",
                requester, rights,
                target);

            // Let's find the friend in this user's friend list
            FriendInfo friend = null;
            foreach (FriendInfo fi in friends.Where(fi => fi.Friend == target.ToString()))
            {
                friend = fi;
            }

            if (friend != null) // Found it
            {
                // Store it on the DB
                FriendsService.StoreFriend (requester, target.ToString (), rights);

                // Store it in the local cache
                int myFlags = friend.MyFlags;
                friend.MyFlags = rights;

                // Always send this back to the original client
                remoteClient.SendChangeUserRights (requester, target, rights);

                //
                // Notify the friend
                //


                // Try local
                if (!LocalGrantRights (requester, target, myFlags, rights))
                {
                    SyncMessagePosterService.PostToServer (SyncMessageHelper.FriendGrantRights (
                        requester, target, myFlags, rights, m_scene.RegionInfo.RegionID));
                }
            }
        }

        public void OfflineFriendRequest (IClientAPI client)
        {
            UUID agentID = client.AgentId;
            var friendRequests = FriendsService.GetFriendsRequest (agentID);
            if (friendRequests == null)
                return;

            FriendInfo [] friends = friendRequests.ToArray ();

            GridInstantMessage im = new GridInstantMessage () {
                ToAgentID = agentID,
                Dialog = (byte)InstantMessageDialog.FriendshipOffered,
                Message = "Will you be my friend?",
                Offline = 1,
                RegionID = client.Scene.RegionInfo.RegionID
            };

            foreach (FriendInfo fi in friends) {
                if (fi.MyFlags == 0) {
                    UUID fromAgentID;
                    if (!UUID.TryParse (fi.Friend, out fromAgentID))
                        continue;

                    UserAccount account = m_scene.UserAccountService.GetUserAccount (
                                              client.Scene.RegionInfo.AllScopeIDs, fromAgentID);
                    im.FromAgentID = fromAgentID;
                    if (account != null)
                        im.FromAgentName = account.Name;
                    im.Offline = 1;
                    im.SessionID = im.FromAgentID;

                    LocalFriendshipOffered (agentID, im);
                }
            }

        }

        void UpdateFriendsCache (UUID agentID)
        {
            lock (m_Friends)
                m_Friends [agentID] = FriendsService.GetFriends (agentID);
        }

        #region Local

        public bool LocalFriendshipOffered (UUID toID, GridInstantMessage im)
        {
            IClientAPI friendClient = LocateClientObject (toID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                friendClient.SendInstantMessage (im);
                // we're done
                return true;
            }
            return false;
        }

        public bool LocalFriendshipApproved (UUID userID, string name, IClientAPI us, UUID friendID)
        {
            IClientAPI friendClient = LocateClientObject (friendID);
            if (friendClient != null)
            {
                //They are online, send the online message
                if (us != null) {
                    us.SendAgentOnline (new [] { friendID });

                    // the prospective friend in this sim as root agent
                    GridInstantMessage im = new GridInstantMessage () {
                        FromAgentID = userID,
                        FromAgentName = name,
                        ToAgentID = friendID,
                        Dialog = (byte)InstantMessageDialog.FriendshipAccepted,
                        Message = userID.ToString (),
                        Offline = 0,
                        RegionID = us.Scene.RegionInfo.RegionID
                    };
                    friendClient.SendInstantMessage (im);
                }
                // Update the local cache
                UpdateFriendsCache (friendID);


                //
                // put a calling card into the inventory of the friend
                //
                ICallingCardModule ccmodule = friendClient.Scene.RequestModuleInterface<ICallingCardModule> ();
                if (ccmodule != null)
                {
                    UserAccount account = friendClient.Scene.UserAccountService.GetUserAccount (friendClient.AllScopeIDs,
                                              userID);
                    UUID folderID =
                        friendClient.Scene.InventoryService.GetFolderForType (friendID, InventoryType.Unknown, FolderType.CallingCard).ID;
                    ccmodule.CreateCallingCard (friendClient, userID, folderID, account.Name);
                }
                // we're done
                return true;
            }

            return false;
        }

        public bool LocalFriendshipDenied (UUID userID, string userName, UUID friendID)
        {
            IClientAPI friendClient = LocateClientObject (friendID);
            if (friendClient != null)
            {
                // the prospective friend in this sim as root agent
                GridInstantMessage im = new GridInstantMessage () {
                    FromAgentID = userID,
                    FromAgentName = userName,
                    ToAgentID = friendID,
                    Dialog = (byte)InstantMessageDialog.FriendshipDeclined,
                    Message = userID.ToString (),
                    Offline = 0,
                    RegionID = friendClient.Scene.RegionInfo.RegionID
                };
                friendClient.SendInstantMessage (im);
                // we're done
                return true;
            }

            return false;
        }

        public bool LocalFriendshipTerminated (UUID exfriendID, UUID terminatingUser)
        {
            IClientAPI friendClient = LocateClientObject (exfriendID);
            if (friendClient != null)
            {
                // update local cache
                UpdateFriendsCache (exfriendID);
                // the friend in this sim as root agent
                // you do NOT send the friend his uuid...  /me sighs...    - Revolution
                friendClient.SendTerminateFriend (terminatingUser);
                return true;
            }

            return false;
        }

        public bool LocalGrantRights (UUID userID, UUID friendID, int userFlags, int rights)
        {
            IClientAPI friendClient = LocateClientObject (friendID);
            if (friendClient != null)
            {
                bool onlineBitChanged = ((rights ^ userFlags) & (int)FriendRights.CanSeeOnline) != 0;
                if (onlineBitChanged)
                {
                    if ((rights & (int)FriendRights.CanSeeOnline) == 1)
                        friendClient.SendAgentOnline (new[] { new UUID (userID) });
                    else
                        friendClient.SendAgentOffline (new[] { new UUID (userID) });
                } else
                {
                    bool canEditObjectsChanged = ((rights ^ userFlags) & (int)FriendRights.CanModifyObjects) != 0;
                    if (canEditObjectsChanged)
                        friendClient.SendChangeUserRights (userID, friendID, rights);
                }

                // Update local cache
                FriendInfo[] friends = GetFriends (friendID);
                foreach (FriendInfo finfo in friends.Where(finfo => finfo.Friend == userID.ToString()))
                {
                    finfo.TheirFlags = rights;
                }
                friends = GetFriends (userID);
                foreach (FriendInfo finfo in friends.Where(finfo => finfo.Friend == friendID.ToString()))
                {
                    finfo.MyFlags = rights;
                }
                //Add primFlag updates for all the prims in the sim with the owner, so that the new permissions are set up correctly
                IScenePresence friendSP = friendClient.Scene.GetScenePresence (friendClient.AgentId);
                foreach (
                    ISceneEntity entity in
                        friendClient.Scene.Entities.GetEntities().Where(entity => entity.OwnerID == userID))
                {
                    entity.ScheduleGroupUpdateToAvatar (friendSP, PrimUpdateFlags.PrimFlags);
                }

                return true;
            }

            return false;
        }

        #endregion
    }
}