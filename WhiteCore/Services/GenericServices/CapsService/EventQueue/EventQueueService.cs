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
using System.Net;
using System.Text;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services
{
    public class EventQueueService : ConnectorBase, IService, IEventQueueService
    {
        #region Declares

        protected ICapsService m_service;

        #endregion

        public virtual string Name {
            get { return GetType ().Name; }
        }

        #region IEventQueueService Members

        public virtual IEventQueueService InnerService {
            get { return this; }
        }

        public virtual bool Enqueue (OSD o, UUID agentID, UUID regionID)
        {
            return Enqueue (OSDParser.SerializeLLSDXmlString (o), agentID, regionID);
        }

        public virtual bool Enqueue (string o, UUID agentID, UUID regionID)
        {
            if (m_doRemoteCalls && m_doRemoteOnly) {
                Util.FireAndForget ((none) => {
                    EnqueueInternal (o, agentID, regionID);
                });
                return true;
            }

            // Find the CapsService for the user and enqueue the event
            IRegionClientCapsService service = GetRegionClientCapsService (agentID, regionID);
            if (service == null)
                return false;

            RegionClientEventQueueService eventQueueService = service.GetServiceConnectors ()
                                                                     .OfType<RegionClientEventQueueService> ()
                                                                     .FirstOrDefault ();
            if (eventQueueService == null)
                return false;

            OSD ev = OSDParser.DeserializeLLSDXml (o);
            return eventQueueService.Enqueue (ev);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual void EnqueueInternal (string o, UUID agentID, UUID regionID)
        {
            if (m_doRemoteCalls && m_doRemoteOnly)
                DoRemotePost (o, agentID, regionID);
            else
                Enqueue (o, agentID, regionID);
        }

        #endregion

        #region IService Members

        public virtual void Initialize (IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs ["Handlers"];
            if (handlerConfig.GetString ("EventQueueHandler", "") != Name)
                return;

            registry.RegisterModuleInterface<IEventQueueService> (this);
            Init (registry, Name);
        }

        public virtual void Start (IConfigSource config, IRegistryCore registry)
        {
            m_service = registry.RequestModuleInterface<ICapsService> ();
        }

        public virtual void FinishedStartup ()
        {
        }

        #endregion

        IRegionClientCapsService GetRegionClientCapsService (UUID agentID, UUID regionHandle)
        {
            IClientCapsService clientCaps = m_service.GetClientCapsService (agentID);
            if (clientCaps == null)
                return null;

            // If it doesn't exist, it will be null anyway, so we don't need to check anything else
            return clientCaps.GetCapsService (regionHandle);
        }

        #region EventQueue Message Enqueue

        public virtual void DisableSimulator (UUID avatarID, ulong regionHandle, UUID regionID)
        {
            OSD item = EventQueueHelper.DisableSimulator (regionHandle);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void EnableSimulator (ulong handle, byte [] iPAddress, int port,
                                             UUID avatarID,
                                             int regionSizeX, int regionSizeY, UUID regionID)
        {
            OSD item = EventQueueHelper.EnableSimulator (handle, iPAddress, port, regionSizeX, regionSizeY);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void ObjectPhysicsProperties (ISceneChildEntity [] entities, UUID avatarID, UUID regionID)
        {
            OSD item = EventQueueHelper.ObjectPhysicsProperties (entities);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void EstablishAgentCommunication (UUID avatarID, ulong regionHandle,
                                                         byte [] iPAddress, int port,
                                                         string capsUrl, int regionSizeX, int regionSizeY,
                                                         UUID regionID)
        {
            IPEndPoint endPoint = new IPEndPoint (new IPAddress (iPAddress), port);
            OSD item = EventQueueHelper.EstablishAgentCommunication (avatarID, regionHandle, endPoint.ToString (), capsUrl,
                           regionSizeX, regionSizeY);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void TeleportFinishEvent (ulong regionHandle, byte simAccess,
                                                 IPAddress address, int port, string capsURL,
                                                 uint locationID,
                                                 UUID avatarID, uint teleportFlags, int regionSizeX, int regionSizeY,
                                                 UUID regionID)
        {
            // Blank (for the CapsUrl) as we do not know what the CapsURL is on the sim side, it will be fixed when it reaches the grid server
            OSD item = EventQueueHelper.TeleportFinishEvent (regionHandle, simAccess, address, port,
                           locationID, capsURL, avatarID, teleportFlags, regionSizeX,
                           regionSizeY);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void CrossRegion (ulong handle, Vector3 pos, Vector3 lookAt,
                                         IPAddress address, int port, string capsURL,
                                         UUID avatarID, UUID sessionID, int regionSizeX, int regionSizeY,
                                         UUID regionID)
        {
            OSD item = EventQueueHelper.CrossRegion (handle, pos, lookAt, address, port,
                           capsURL, avatarID, sessionID, regionSizeX, regionSizeY);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void ChatterBoxSessionStartReply (string groupName, UUID groupID, UUID agentID, UUID regionID)
        {
            OSD Item = EventQueueHelper.ChatterBoxSessionStartReply (groupName, groupID);
            Enqueue (Item, agentID, regionID);
        }

        public virtual void ChatterboxInvitation (UUID sessionID, string sessionName,
                                                  UUID fromAgent, string message, UUID toAgent, string fromName,
                                                  byte dialog,
                                                  uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                                  uint ttl, UUID transactionID, bool fromGroup, byte [] binaryBucket,
                                                  UUID regionID)
        {
            OSD item = EventQueueHelper.ChatterboxInvitation (sessionID, sessionName, fromAgent, message, toAgent,
                           fromName, dialog,
                           timeStamp, offline, parentEstateID, position, ttl,
                           transactionID,
                           fromGroup, binaryBucket);
            Enqueue (item, toAgent, regionID);
            // MainConsole.Instance.InfoFormat("########### eq ChatterboxInvitation #############\n{0}", item);
        }

        public virtual void ChatterBoxSessionAgentListUpdates (UUID sessionID, UUID fromAgent, UUID toAgent,
                                                               bool canVoiceChat, bool isModerator, bool textMute,
                                                               UUID regionID)
        {
            OSD item = EventQueueHelper.ChatterBoxSessionAgentListUpdates (sessionID, fromAgent, canVoiceChat,
                           isModerator, textMute);
            Enqueue (item, toAgent, regionID);
            // MainConsole.Instance.InfoFormat("########### eq ChatterBoxSessionAgentListUpdates #############\n{0}", item);
        }

        public virtual void ChatterBoxSessionAgentListUpdates (UUID sessionID,
                                                               ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock
                                                                  [] messages, UUID toAgent, string transition,
                                                               UUID regionID)
        {
            OSD item = EventQueueHelper.ChatterBoxSessionAgentListUpdates (sessionID, messages, transition);
            Enqueue (item, toAgent, regionID);
            // MainConsole.Instance.InfoFormat("########### eq ChatterBoxSessionAgentListUpdates #############\n{0}", item);
        }

        public virtual void ParcelProperties (ParcelPropertiesMessage parcelPropertiesPacket, UUID avatarID, UUID regionID)
        {
            OSD item = EventQueueHelper.ParcelProperties (parcelPropertiesPacket);
            Enqueue (item, avatarID, regionID);
        }

        public void ParcelObjectOwnersReply (ParcelObjectOwnersReplyMessage parcelMessage, UUID agentID, UUID regionID)
        {
            OSD item = EventQueueHelper.ParcelObjectOwnersReply (parcelMessage);
            Enqueue (item, agentID, regionID);
        }

        public void LandStatReply (LandStatReplyMessage message, UUID agentID, UUID regionID)
        {
            OSD item = EventQueueHelper.LandStatReply (message);
            Enqueue (item, agentID, regionID);
        }

        public virtual void GroupMembership (AgentGroupDataUpdatePacket groupUpdate, UUID avatarID, UUID regionID)
        {
            OSD item = EventQueueHelper.GroupMembership (groupUpdate);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void QueryReply (PlacesReplyPacket groupUpdate, UUID avatarID, string [] info, UUID regionID)
        {
            OSD item = EventQueueHelper.PlacesQuery (groupUpdate, info);
            Enqueue (item, avatarID, regionID);
        }

        public virtual void ScriptRunningReply (UUID objectID, UUID itemID, bool running, bool mono,
                                                UUID avatarID, UUID regionID)
        {
            OSD Item = EventQueueHelper.ScriptRunningReplyEvent (objectID, itemID, running, true);
            Enqueue (Item, avatarID, regionID);
        }

        #endregion
    }

    public class RegionClientEventQueueService : ICapsServiceConnector
    {
        #region Declares

        readonly Queue<OSD> queue = new Queue<OSD> ();
        string m_capsPath;
        int m_ids;
        IRegionClientCapsService m_service;

        #endregion

        #region IInternalEventQueueService members

        #region Enqueue a message/Create/Remove handlers

        public void DumpEventQueue ()
        {
            lock (queue) {
                queue.Clear ();
            }
        }

        /// <summary>
        ///     Add the given event into the client's queue so that it is sent on the next
        /// </summary>
        /// <param name="ev"></param>
        /// <returns></returns>
        public bool Enqueue (OSD ev)
        {
            try {
                if (ev == null)
                    return false;

                lock (queue)
                    queue.Enqueue (ev);
            } catch (NullReferenceException e) {
                MainConsole.Instance.Error ("[Event queue] Caught exception: " + e);
                return false;
            }

            return true;
        }

        #endregion

        #region Process Get/Has events

        public bool HasEvents (UUID requestID, UUID agentID)
        {
            lock (queue) {
                return queue.Count > 0;
            }
        }

        public byte [] GetEvents (UUID requestID, UUID pAgentId, string req, OSHttpResponse response)
        {
            OSDMap events = new OSDMap ();
            try {
                OSDArray array = new OSDArray ();
                lock (queue) {
                    if (queue.Count == 0)
                        return NoEvents (requestID, pAgentId, response);

                    while (queue.Count > 0) {
                        array.Add (queue.Dequeue ());
                        m_ids++;
                    }
                }

                events.Add ("events", array);

                events.Add ("id", new OSDInteger (m_ids));
            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[Event queue]: Exception! " + ex);
            }

            response.StatusCode = 200;
            response.ContentType = "application/xml";

            return OSDParser.SerializeLLSDXmlBytes (events);
        }

        public byte [] NoEvents (UUID requestID, UUID agentID, OSHttpResponse response)
        {
            response.KeepAlive = false;
            response.ContentType = "text/plain";
            response.StatusCode = 502;

            return Encoding.UTF8.GetBytes ("Upstream error: ");
        }

        #endregion

        #endregion

        #region ICapsServiceConnector Members

        public void RegisterCaps (IRegionClientCapsService service)
        {
            m_service = service;

            const string capsBase = "/CAPS/EQG/";
            m_capsPath = capsBase + UUID.Random () + "/";

            // Register this as a caps handler
            m_service.AddStreamHandler ("EventQueueGet",
                new GenericStreamHandler ("POST", m_capsPath, (path, request, httpRequest, httpResponse) => new byte [0]));

            MainServer.Instance.AddPollServiceHTTPHandler (
                m_capsPath, new PollServiceEventArgs (null, HasEvents, GetEvents, NoEvents, m_service.AgentID));

            Random rnd = new Random (Environment.TickCount);
            m_ids = rnd.Next (30000000);
        }

        public void EnteringRegion ()
        {
            DumpEventQueue ();
        }

        public void DeregisterCaps ()
        {
            m_service.RemoveStreamHandler ("EventQueueGet", "POST", m_capsPath);
            MainServer.Instance.RemovePollServiceHTTPHandler ("POST", m_capsPath);
        }

        #endregion
    }
}