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

using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Modules.Search
{
    public class WhiteCoreSearchModule : INonSharedRegionModule
    {
        #region Declares

        private IScene m_Scene;
        private IGroupsModule GroupsModule;
        private IProfileConnector ProfileFrontend;
        private IDirectoryServiceConnector DirectoryService;
        private bool m_SearchEnabled;

        #endregion

        #region Client

        public void NewClient(IClientAPI client)
        {
            // Subscribe to messages
            client.OnDirPlacesQuery += DirPlacesQuery;
            client.OnDirFindQuery += DirFindQuery;
            client.OnDirPopularQuery += DirPopularQuery;
            client.OnDirLandQuery += DirLandQuery;
            client.OnDirClassifiedQuery += DirClassifiedQuery;
            // Response after Directory Queries
            client.OnEventInfoRequest += EventInfoRequest;
            client.OnMapItemRequest += HandleMapItemRequest;
            client.OnPlacesQuery += OnPlacesQueryRequest;
            client.OnAvatarPickerRequest += ProcessAvatarPickerRequest;
            client.OnEventNotificationAddRequest += client_OnEventNotificationAddRequest;
            client.OnEventNotificationRemoveRequest += client_OnEventNotificationRemoveRequest;
        }

        private void OnClosingClient(IClientAPI client)
        {
            client.OnDirPlacesQuery -= DirPlacesQuery;
            client.OnDirFindQuery -= DirFindQuery;
            client.OnDirPopularQuery -= DirPopularQuery;
            client.OnDirLandQuery -= DirLandQuery;
            client.OnDirClassifiedQuery -= DirClassifiedQuery;
            // Response after Directory Queries
            client.OnEventInfoRequest -= EventInfoRequest;
            client.OnMapItemRequest -= HandleMapItemRequest;
            client.OnPlacesQuery -= OnPlacesQueryRequest;
            client.OnAvatarPickerRequest -= ProcessAvatarPickerRequest;
            client.OnEventNotificationAddRequest -= client_OnEventNotificationAddRequest;
            client.OnEventNotificationRemoveRequest -= client_OnEventNotificationRemoveRequest;
        }

        #endregion

        #region Search Module

        #region Delegates

        public delegate void SendPacket<T>(T[] data);

        #endregion

        /// <summary>
        ///     Parcel request
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="queryID"></param>
        /// <param name="queryText">The thing to search for</param>
        /// <param name="queryFlags"></param>
        /// <param name="category"></param>
        /// <param name="simName"></param>
        /// <param name="queryStart"></param>
        protected void DirPlacesQuery(IClientAPI remoteClient, UUID queryID,
                                      string queryText, int queryFlags, int category, string simName,
                                      int queryStart)
        {
            List<DirPlacesReplyData> ReturnValues =
                DirectoryService.FindLand(queryText.Trim(), category.ToString(), queryStart,
                                          (uint) queryFlags, remoteClient.ScopeID);

            SplitPackets(ReturnValues,
                         data => remoteClient.SendDirPlacesReply(queryID, data));
        }
        
        /// <summary>
        ///     Popular places based on traffic
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="queryID"></param>
        /// <param name="queryFlags"></param>
        public void DirPopularQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags)
        {
            List<DirPopularReplyData> ReturnValues =
                DirectoryService.FindPopularPlaces(queryFlags, remoteClient.ScopeID);

            remoteClient.SendDirPopularReply(queryID, ReturnValues.ToArray());
        }

        /// <summary>
        ///     Land for sale request
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="queryID"></param>
        /// <param name="queryFlags"></param>
        /// <param name="searchType"></param>
        /// <param name="price"></param>
        /// <param name="area"></param>
        /// <param name="queryStart"></param>
        public void DirLandQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags, uint searchType, uint price,
                                 uint area, int queryStart)
        {
            List<DirLandReplyData> ReturnValues =
                new List<DirLandReplyData>(DirectoryService.FindLandForSale(searchType.ToString(), price, area,
                                                                            queryStart, queryFlags, remoteClient.ScopeID));

            SplitPackets(ReturnValues,
                         data => remoteClient.SendDirLandReply(queryID, data));
        }

        /// <summary>
        ///     Finds either people or events
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="queryID">Just a UUID to send back to the client</param>
        /// <param name="queryText">The term to search for</param>
        /// <param name="queryFlags">Flags like maturity, etc</param>
        /// <param name="queryStart">Where in the search should we start? 0, 10, 20, etc</param>
        public void DirFindQuery(IClientAPI remoteClient, UUID queryID,
                                 string queryText, uint queryFlags, int queryStart)
        {
            if ((queryFlags & 1) != 0) //People query
            {
                DirPeopleQuery(remoteClient, queryID, queryText, queryFlags,
                               queryStart);
            }
            else if ((queryFlags & 32) != 0) //Events query
            {
                DirEventsQuery(remoteClient, queryID, queryText, queryFlags,
                               queryStart);
            }
        }

        //TODO: Flagged to optimize
        public void DirPeopleQuery(IClientAPI remoteClient, UUID queryID,
                                   string queryText, uint queryFlags, int queryStart)
        {
            //Find the user accounts
            List<UserAccount> accounts = m_Scene.UserAccountService.GetUserAccounts(remoteClient.AllScopeIDs,
                                                                                    queryText.Trim());
            List<DirPeopleReplyData> ReturnValues =
                new List<DirPeopleReplyData>();

            foreach (UserAccount item in accounts)
            {
                //This is really bad, we should not be searching for all of these people again in the Profile service
                IUserProfileInfo UserProfile = ProfileFrontend.GetUserProfile(item.PrincipalID);
                if (UserProfile == null)
                {
                    DirPeopleReplyData person = new DirPeopleReplyData
                                                    {
                                                        agentID = item.PrincipalID,
                                                        firstName = item.FirstName,
                                                        lastName = item.LastName
                                                    };
                    if (GroupsModule == null)
                        person.group = "";
                    else
                    {
                        person.group = "";
                        GroupMembershipData[] memberships = GroupsModule.GetMembershipData(item.PrincipalID);
                        foreach (GroupMembershipData membership in memberships.Where(membership => membership.Active))
                        {
                            person.group = membership.GroupName;
                        }
                    }
                    //Then we have to pull the GUI to see if the user is online or not
                    UserInfo Pinfo =
                        m_Scene.RequestModuleInterface<IAgentInfoService>().GetUserInfo(item.PrincipalID.ToString());
                    if (Pinfo != null && Pinfo.IsOnline) //If it is null, they are offline
                        person.online = true;
                    person.reputation = 0;
                    ReturnValues.Add(person);
                }
                else if (UserProfile.AllowPublish) //Check whether they want to be in search or not
                {
                    DirPeopleReplyData person = new DirPeopleReplyData
                                                    {
                                                        agentID = item.PrincipalID,
                                                        firstName = item.FirstName,
                                                        lastName = item.LastName
                                                    };
                    if (GroupsModule == null)
                        person.group = "";
                    else
                    {
                        person.group = "";
                        //Check what group they have set
                        GroupMembershipData[] memberships = GroupsModule.GetMembershipData(item.PrincipalID);
                        foreach (GroupMembershipData membership in memberships.Where(membership => membership.Active))
                        {
                            person.group = membership.GroupName;
                        }
                    }
                    //Then we have to pull the GUI to see if the user is online or not
                    UserInfo Pinfo =
                        m_Scene.RequestModuleInterface<IAgentInfoService>().GetUserInfo(item.PrincipalID.ToString());
                    if (Pinfo != null && Pinfo.IsOnline)
                        person.online = true;
                    person.reputation = 0;
                    ReturnValues.Add(person);
                }
            }

            SplitPackets(ReturnValues,
                         data => remoteClient.SendDirPeopleReply(queryID, data));
        }

        /// <summary>
        ///     Find Events
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="queryID"></param>
        /// <param name="queryText"></param>
        /// <param name="queryFlags"></param>
        /// <param name="queryStart"></param>
        public void DirEventsQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags,
                                   int queryStart)
        {
            List<DirEventsReplyData> ReturnValues =
                new List<DirEventsReplyData>(DirectoryService.FindEvents(queryText.Trim(), queryFlags, queryStart,
                                                                         remoteClient.ScopeID));

            SplitPackets(ReturnValues, data => remoteClient.SendDirEventsReply(queryID, data));
        }
        
        /// <summary>
        ///     Find Classifieds
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="queryID"></param>
        /// <param name="queryText"></param>
        /// <param name="queryFlags"></param>
        /// <param name="category"></param>
        /// <param name="queryStart"></param>
        public void DirClassifiedQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags,
                                       uint category, int queryStart)
        {
            List<DirClassifiedReplyData> ReturnValues =
                new List<DirClassifiedReplyData>(DirectoryService.FindClassifieds(queryText.Trim(), category.ToString(),
                                                                                  queryFlags, queryStart,
                                                                                  remoteClient.ScopeID));

            SplitPackets(ReturnValues,
                         data => remoteClient.SendDirClassifiedReply(queryID, data));
        }

        public void SplitPackets<T>(List<T> packets, SendPacket<T> send)
        {
            if (packets.Count == 0)
            {
                send(new T[0]);
                return;
            }
            int i = 0;
            while (i < packets.Count)
            {
                int count = Math.Min(10, packets.Count);
                //Split into sets of 10 packets
                T[] data = packets.GetRange(i, count).ToArray();
                i += count;
                if (data.Length != 0)
                    send(data);
            }
        }

        /// <summary>
        ///     Tell the client about Event X
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="queryEventID">ID of the event</param>
        public void EventInfoRequest(IClientAPI remoteClient, uint queryEventID)
        {
            //Find the event
            EventData data = DirectoryService.GetEventInfo(queryEventID);
            if (data == null)
                return;
            //Send the event
            remoteClient.SendEventInfoReply(data);
        }

        public virtual void HandleMapItemRequest(IClientAPI remoteClient, uint flags,
                                                 uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            //All the parts are in for this, except for popular places and those are not in as they are not reqested anymore.

            List<mapItemReply> mapitems = new List<mapItemReply>();
            mapItemReply mapitem = new mapItemReply();
            uint xstart = 0;
            uint ystart = 0;
            Utils.LongToUInts(remoteClient.Scene.RegionInfo.RegionHandle, out xstart, out ystart);
            GridRegion GR = null;

            GR = regionhandle == 0
                     ? new GridRegion(remoteClient.Scene.RegionInfo)
                     : m_Scene.GridService.GetRegionByPosition(remoteClient.AllScopeIDs, (int) xstart, (int) ystart);
            if (GR == null)
            {
                //No region???
                return;
            }

            #region Telehub

            if (itemtype == (uint) GridItemType.Telehub)
            {
                IRegionConnector GF = Framework.Utilities.DataManager.RequestPlugin<IRegionConnector>();
                if (GF == null)
                    return;

                int tc = Environment.TickCount;
                //Find the telehub
                Telehub telehub = GF.FindTelehub(GR.RegionID, GR.RegionHandle);
                if (telehub != null)
                {
                    mapitem = new mapItemReply
                                  {
                                      x = (uint) (GR.RegionLocX + telehub.TelehubLocX),
                                      y = (uint) (GR.RegionLocY + telehub.TelehubLocY),
                                      id = GR.RegionID,
                                      name = Util.Md5Hash(GR.RegionName + tc.ToString()),
                                      Extra = 1,
                                      Extra2 = 0
                                  };
                    //The position is in GLOBAL coordinates (in meters)
                    //This is how the name is sent, go figure
                    //Not sure, but this is what gets sent

                    mapitems.Add(mapitem);
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                    mapitems.Clear();
                }
            }

            #endregion

            #region Land for sale

            //PG land that is for sale
            if (itemtype == (uint) GridItemType.LandForSale)
            {
                if (DirectoryService == null)
                    return;
                //Find all the land, use "0" for the flags so we get all land for sale, no price or area checking
                List<DirLandReplyData> Landdata = DirectoryService.FindLandForSaleInRegion("0", uint.MaxValue, 0, 0, 0,
                                                                                           GR.RegionID);

                int locX = 0;
                int locY = 0;
                foreach (DirLandReplyData landDir in Landdata)
                {
                    if (landDir == null)
                        continue;
                    LandData landdata = DirectoryService.GetParcelInfo(landDir.parcelID);
                    if (landdata == null || landdata.Maturity != 0)
                        continue; //Not a PG land 
                    if (m_Scene.RegionInfo.RegionID == landdata.RegionID)
                    {
                        //Global coords, so add the meters
                        locX = m_Scene.RegionInfo.RegionLocX;
                        locY = m_Scene.RegionInfo.RegionLocY;
                    }
                    else
                    {
                        //Ask the grid service for the coordinates if the region is not local
                        GridRegion r = m_Scene.GridService.GetRegionByUUID(remoteClient.AllScopeIDs, landdata.RegionID);
                        if (r != null)
                        {
                            locX = r.RegionLocX;
                            locY = r.RegionLocY;
                        }
                    }
                    if (locY == 0 && locX == 0) //Couldn't find the region, don't send
                        continue;

                    mapitem = new mapItemReply
                                  {
                                      x = (uint) (locX + landdata.UserLocation.X),
                                      y = (uint) (locY + landdata.UserLocation.Y),
                                      id = landDir.parcelID,
                                      name = landDir.name,
                                      Extra = landDir.actualArea,
                                      Extra2 = landDir.salePrice
                                  };
                    //Global coords, so make sure its in meters
                    mapitems.Add(mapitem);
                }
                //Send all the map items
                if (mapitems.Count != 0)
                {
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                    mapitems.Clear();
                }
            }

            //Adult or mature land that is for sale
            if (itemtype == (uint) GridItemType.AdultLandForSale)
            {
                if (DirectoryService == null)
                    return;
                //Find all the land, use "0" for the flags so we get all land for sale, no price or area checking
                List<DirLandReplyData> Landdata = DirectoryService.FindLandForSale("0", uint.MaxValue, 0, 0, 0,
                                                                                   remoteClient.ScopeID);

                int locX = 0;
                int locY = 0;
                foreach (DirLandReplyData landDir in Landdata)
                {
                    LandData landdata = DirectoryService.GetParcelInfo(landDir.parcelID);
                    if (landdata == null || landdata.Maturity == 0)
                        continue; //Its PG

                    if (m_Scene.RegionInfo.RegionID == landdata.RegionID)
                    {
                        locX = m_Scene.RegionInfo.RegionLocX;
                        locY = m_Scene.RegionInfo.RegionLocY;
                    }
                    else
                    {
                        //Ask the grid service for the coordinates if the region is not local
                        GridRegion r = m_Scene.GridService.GetRegionByUUID(remoteClient.AllScopeIDs, landdata.RegionID);
                        if (r != null)
                        {
                            locX = r.RegionLocX;
                            locY = r.RegionLocY;
                        }
                    }
                    if (locY == 0 && locX == 0) //Couldn't find the region, don't send
                        continue;

                    mapitem = new mapItemReply
                                  {
                                      x = (uint) (locX + landdata.UserLocation.X),
                                      y = (uint) (locY + landdata.UserLocation.Y),
                                      id = landDir.parcelID,
                                      name = landDir.name,
                                      Extra = landDir.actualArea,
                                      Extra2 = landDir.salePrice
                                  };
                    //Global coords, so make sure its in meters

                    mapitems.Add(mapitem);
                }
                //Send the results if we have any
                if (mapitems.Count != 0)
                {
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                    mapitems.Clear();
                }
            }

            #endregion

            #region Events

            if (itemtype == (uint) GridItemType.PgEvent ||
                itemtype == (uint) GridItemType.MatureEvent ||
                itemtype == (uint) GridItemType.AdultEvent)
            {
                if (DirectoryService == null)
                    return;

                //Find the maturity level
                int maturity = itemtype == (uint) GridItemType.PgEvent
                                   ? (int) DirectoryManager.EventFlags.PG
                                   : (itemtype == (uint) GridItemType.MatureEvent)
                                         ? (int) DirectoryManager.EventFlags.Mature
                                         : (int) DirectoryManager.EventFlags.Adult;

                //Gets all the events occurring in the given region by maturity level
                List<DirEventsReplyData> Eventdata = DirectoryService.FindAllEventsInRegion(GR.RegionName, maturity);

                foreach (DirEventsReplyData eventData in Eventdata)
                {
                    //Get more info on the event
                    EventData eventdata = DirectoryService.GetEventInfo(eventData.eventID);
                    if (eventdata == null)
                        continue; //Can't do anything about it
                    Vector3 globalPos = eventdata.globalPos;
                    mapitem = new mapItemReply
                                  {
                                      x = (uint) globalPos.X,
                                      y = (uint) globalPos.Y,
                                      id = UUID.Random(),
                                      name = eventData.name,
                                      Extra = (int) eventdata.dateUTC,
                                      Extra2 = (int) eventdata.eventID
                                  };

                    //Use global position plus half the region so that it doesn't always appear in the bottom corner

                    mapitems.Add(mapitem);
                }
                //Send if we have any
                if (mapitems.Count != 0)
                {
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                    mapitems.Clear();
                }
            }

            #endregion

            #region Classified

            if (itemtype == (uint) GridItemType.Classified)
            {
                if (DirectoryService == null)
                    return;
                //Get all the classifieds in this region
                List<Classified> Classifieds = DirectoryService.GetClassifiedsInRegion(GR.RegionName);
                foreach (Classified classified in Classifieds)
                {
                    //Get the region so we have its position
                    GridRegion region = m_Scene.GridService.GetRegionByName(remoteClient.AllScopeIDs, classified.SimName);

                    mapitem = new mapItemReply
                                  {
                                      x = (uint)
                                          (region.RegionLocX + classified.GlobalPos.X +
                                           (remoteClient.Scene.RegionInfo.RegionSizeX/2)),
                                      y = (uint)
                                          (region.RegionLocY + classified.GlobalPos.Y +
                                           (remoteClient.Scene.RegionInfo.RegionSizeY/2)),
                                      id = classified.CreatorUUID,
                                      name = classified.Name,
                                      Extra = 0,
                                      Extra2 = 0
                                  };

                    //Use global position plus half the sim so that all classifieds are not in the bottom corner

                    mapitems.Add(mapitem);
                }
                //Send the events, if we have any
                if (mapitems.Count != 0)
                {
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                    mapitems.Clear();
                }
            }

            #endregion
        }

        public void OnPlacesQueryRequest(UUID QueryID, UUID TransactionID, string QueryText, uint QueryFlags,
                                         byte Category, string SimName, IClientAPI client)
        {
            if (QueryFlags == 64) //Agent Owned
            {
                //Get all the parcels
                client.SendPlacesQuery(DirectoryService.GetParcelByOwner(client.AgentId).ToArray(), QueryID,
                                       TransactionID);
            }
            if (QueryFlags == 256) //Group Owned
            {
                //Find all the group owned land
                List<ExtendedLandData> parcels = DirectoryService.GetParcelByOwner(QueryID);

                //Send if we have any parcels
                if (parcels.Count != 0)
                    client.SendPlacesQuery(parcels.ToArray(), QueryID, TransactionID);
            }
        }

        public void ProcessAvatarPickerRequest(IClientAPI client, UUID avatarID, UUID RequestID, string query)
        {
            IScene scene = client.Scene;
            List<UserAccount> accounts = scene.UserAccountService.GetUserAccounts(scene.RegionInfo.AllScopeIDs, query);

            if (accounts == null)
                accounts = new List<UserAccount>(0);

            AvatarPickerReplyPacket replyPacket =
                (AvatarPickerReplyPacket) PacketPool.Instance.GetPacket(PacketType.AvatarPickerReply);
            // TODO: don't create new blocks if recycling an old packet

            AvatarPickerReplyPacket.DataBlock[] searchData =
                new AvatarPickerReplyPacket.DataBlock[accounts.Count];
            AvatarPickerReplyPacket.AgentDataBlock agentData = new AvatarPickerReplyPacket.AgentDataBlock
                                                                   {AgentID = avatarID, QueryID = RequestID};

            replyPacket.AgentData = agentData;

            int i = 0;
            foreach (UserAccount item in accounts)
            {
                UUID translatedIDtem = item.PrincipalID;
                searchData[i] = new AvatarPickerReplyPacket.DataBlock
                                    {
                                        AvatarID = translatedIDtem,
                                        FirstName = Utils.StringToBytes(item.FirstName),
                                        LastName = Utils.StringToBytes(item.LastName)
                                    };
                i++;
            }
            if (accounts.Count == 0)
            {
                searchData = new AvatarPickerReplyPacket.DataBlock[0];
            }
            replyPacket.Data = searchData;

            AvatarPickerReplyAgentDataArgs agent_data = new AvatarPickerReplyAgentDataArgs
                                                            {
                                                                AgentID = replyPacket.AgentData.AgentID,
                                                                QueryID = replyPacket.AgentData.QueryID
                                                            };

            List<AvatarPickerReplyDataArgs> data_args = new List<AvatarPickerReplyDataArgs>();
            for (i = 0; i < replyPacket.Data.Length; i++)
            {
                AvatarPickerReplyDataArgs data_arg = new AvatarPickerReplyDataArgs
                                                         {
                                                             AvatarID = replyPacket.Data[i].AvatarID,
                                                             FirstName = replyPacket.Data[i].FirstName,
                                                             LastName = replyPacket.Data[i].LastName
                                                         };
                data_args.Add(data_arg);
            }
            client.SendAvatarPickerReply(agent_data, data_args);
        }

        private void client_OnEventNotificationRemoveRequest(uint EventID, IClientAPI client)
        {
            DirectoryService.RemoveEventNofication(client.AgentId, EventID);
        }

        private void client_OnEventNotificationAddRequest(uint EventID, IClientAPI client)
        {
            DirectoryService.AddEventNofication(client.AgentId, EventID);
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            IConfig searchConfig = config.Configs["Search"];
            if (searchConfig != null) //Check whether we are enabled
                if (searchConfig.GetString("SearchModule", Name) == Name)
                    m_SearchEnabled = true;
        }

        public void AddRegion(IScene scene)
        {
            if (!m_SearchEnabled)
                return;

            m_Scene = scene;
            scene.EventManager.OnNewClient += NewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
        }

        public void RemoveRegion(IScene scene)
        {
            if (!m_SearchEnabled)
                return;

            m_Scene = null;
            scene.EventManager.OnNewClient -= NewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
        }

        public void RegionLoaded(IScene scene)
        {
            if (!m_SearchEnabled)
                return;
            //Pull in the services we need
            ProfileFrontend = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>();
            DirectoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector>();
            GroupsModule = scene.RequestModuleInterface<IGroupsModule>();
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "WhiteCoreSearchModule"; }
        }

        #endregion
    }
}