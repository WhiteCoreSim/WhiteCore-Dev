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
using System.Linq;
using System.Net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;


namespace WhiteCore.Services
{
    public class EventQueueHelper
    {
        static byte[] ulongToByteArray (ulong uLongValue)
        {
            // Reverse endianness of RegionHandle
            return new[] {
                (byte)((uLongValue >> 56) % 256),
                (byte)((uLongValue >> 48) % 256),
                (byte)((uLongValue >> 40) % 256),
                (byte)((uLongValue >> 32) % 256),
                (byte)((uLongValue >> 24) % 256),
                (byte)((uLongValue >> 16) % 256),
                (byte)((uLongValue >> 8) % 256),
                (byte)(uLongValue % 256)
            };
        }

        static byte[] uintToByteArray (uint uIntValue)
        {
            byte[] resultbytes = Utils.UIntToBytes (uIntValue);
            if (BitConverter.IsLittleEndian)
                Array.Reverse (resultbytes);

            return resultbytes;
        }

        public static OSD buildEvent (string eventName, OSD eventBody)
        {
            OSDMap llsdEvent = new OSDMap (2) { { "body", eventBody }, { "message", new OSDString (eventName) } };

            return llsdEvent;
        }

        public static OSD EnableSimulator (ulong handle, byte[] ipAddress, int port, int regionSizeX, int regionSizeY)
        {
            OSDMap llsdSimInfo = new OSDMap (3) {
                { "Handle", new OSDBinary (ulongToByteArray (handle)) },
                { "IP", new OSDBinary (ipAddress) },
                { "Port", new OSDInteger (port) },
                { "RegionSizeX", OSD.FromUInteger ((uint)regionSizeX) },
                { "RegionSizeY", OSD.FromUInteger ((uint)regionSizeY) }
            };

            OSDArray arr = new OSDArray (1) { llsdSimInfo };
            OSDMap llsdBody = new OSDMap (1) { { "SimulatorInfo", arr } };

            return buildEvent ("EnableSimulator", llsdBody);
        }

        public static OSD ObjectPhysicsProperties (ISceneChildEntity[] entities)
        {
            ObjectPhysicsPropertiesMessage message = new ObjectPhysicsPropertiesMessage ();
            int i = entities.Count (entity => entity != null);

            message.ObjectPhysicsProperties = new Primitive.PhysicsProperties[i];
            i = 0;

            foreach (ISceneChildEntity entity in entities.Where(entity => entity != null))
            {
                message.ObjectPhysicsProperties [i] = new Primitive.PhysicsProperties {
                    Density = entity.Density,
                    Friction = entity.Friction,
                    GravityMultiplier = entity.GravityMultiplier,
                    LocalID = entity.LocalId,
                    PhysicsShapeType = (PhysicsShapeType)entity.PhysicsType,
                    Restitution = entity.Restitution
                };
                i++;
            }

            OSDMap oMap = new OSDMap { { "message", OSD.FromString ("ObjectPhysicsProperties") } };
            OSD message_body = message.Serialize ();
            oMap.Add ("body", message_body);

            return oMap;
        }

        public static OSD DisableSimulator (ulong handle)
        {
            OSDMap llsdBody = new OSDMap (1);
            return buildEvent ("DisableSimulator", llsdBody);
        }

        public static OSD CrossRegion (ulong handle, Vector3 pos, Vector3 lookAt,
                                       IPAddress address, int port,
                                       string capsURL, UUID agentID, UUID sessionID, int regionSizeX, int regionSizeY)
        {
            OSDArray lookAtArr = new OSDArray (3)
                                     { OSD.FromReal (lookAt.X), OSD.FromReal (lookAt.Y), OSD.FromReal (lookAt.Z) };

            OSDArray positionArr = new OSDArray (3) {
                OSD.FromReal (pos.X),
                OSD.FromReal (pos.Y),
                OSD.FromReal (pos.Z)
            };

            OSDMap infoMap = new OSDMap (2) { { "LookAt", lookAtArr }, { "Position", positionArr } };

            OSDArray infoArr = new OSDArray (1) { infoMap };

            OSDMap agentDataMap = new OSDMap (2) {
                { "AgentID", OSD.FromUUID (agentID) }, 
                { "SessionID", OSD.FromUUID (sessionID) }
            };

            OSDArray agentDataArr = new OSDArray (1) { agentDataMap };

            OSDMap regionDataMap = new OSDMap (4) {
                { "RegionHandle", OSD.FromBinary (ulongToByteArray (handle)) },
                { "SeedCapability", OSD.FromString (capsURL) },
                { "SimIP", OSD.FromBinary (address.GetAddressBytes ()) },
                { "SimPort", OSD.FromInteger (port) },
                { "RegionSizeX", OSD.FromUInteger ((uint)regionSizeX) },
                { "RegionSizeY", OSD.FromUInteger ((uint)regionSizeY) }
            };

            OSDArray regionDataArr = new OSDArray (1) { regionDataMap };

            OSDMap llsdBody = new OSDMap (3) {
                { "Info", infoArr },
                { "AgentData", agentDataArr },
                { "RegionData", regionDataArr }
            };

            return buildEvent ("CrossedRegion", llsdBody);
        }

        public static OSD TeleportFinishEvent (ulong regionHandle, byte simAccess, IPAddress address, int port,
                                               uint locationID, string capsURL, UUID agentID, uint teleportFlags,
                                               int regionSizeX, int regionSizeY)
        {
            OSDMap info = new OSDMap {
                { "AgentID", OSD.FromUUID (agentID) },
                { "LocationID", OSD.FromBinary (uintToByteArray (locationID)) },
                { "RegionHandle", OSD.FromBinary (ulongToByteArray (regionHandle)) },
                { "SeedCapability", OSD.FromString (capsURL) },
                { "SimAccess", OSD.FromInteger (simAccess) },
                { "SimIP", OSD.FromBinary (address.GetAddressBytes ()) },
                { "SimPort", OSD.FromInteger (port) },
                { "TeleportFlags", OSD.FromBinary (uintToByteArray (teleportFlags)) },
                { "RegionSizeX", OSD.FromUInteger ((uint)regionSizeX) },
                { "RegionSizeY", OSD.FromUInteger ((uint)regionSizeY) }
            };

            OSDArray infoArr = new OSDArray { info };

            OSDMap body = new OSDMap { { "Info", infoArr } };

            return buildEvent ("TeleportFinish", body);
        }

        public static OSD ScriptRunningReplyEvent (UUID objectID, UUID itemID, bool running, bool mono)
        {
            OSDMap script = new OSDMap {
                { "ObjectID", OSD.FromUUID (objectID) },
                { "ItemID", OSD.FromUUID (itemID) },
                { "Running", OSD.FromBoolean (running) },
                { "Mono", OSD.FromBoolean (mono) }
            };

            OSDArray scriptArr = new OSDArray { script };

            OSDMap body = new OSDMap { { "Script", scriptArr } };

            return buildEvent ("ScriptRunningReply", body);
        }

        public static OSD EstablishAgentCommunication (UUID agentID, ulong regionhandle, string simIpAndPort,
                                                       string seedcap, int regionSizeX, int regionSizeY)
        {
            OSDMap body = new OSDMap (3) {
                { "agent-id", new OSDUUID (agentID) },
                { "sim-ip-and-port", new OSDString (simIpAndPort) },
                { "seed-capability", new OSDString (seedcap) },
                { "region-handle", OSD.FromULong (regionhandle) },
                { "region-size-x", OSD.FromInteger (regionSizeX) },
                { "region-size-y", OSD.FromInteger (regionSizeY) }
            };

            return buildEvent ("EstablishAgentCommunication", body);
        }

        public static OSD AgentParams (UUID agentID, bool checkEstate, int godLevel, bool limitedToEstate)
        {
            OSDMap body = new OSDMap (4) {
                { "agent_id", new OSDUUID (agentID) },
                { "check_estate", new OSDInteger (checkEstate ? 1 : 0) },
                { "god_level", new OSDInteger (godLevel) },
                { "limited_to_estate", new OSDInteger (limitedToEstate ? 1 : 0) }
            };

            return body;
        }

        public static OSD InstantMessageParams (UUID fromAgent, string message, UUID toAgent,
                                                string fromName, byte dialog, uint timeStamp, bool offline,
                                                int parentEstateID,
                                                Vector3 position, uint ttl, UUID transactionID, bool fromGroup,
                                                byte[] binaryBucket)
        {
            OSDMap messageParams = new OSDMap (15) { { "type", new OSDInteger (dialog) } };

            OSDArray positionArray = new OSDArray (3) {
                OSD.FromReal (position.X),
                OSD.FromReal (position.Y),
                OSD.FromReal (position.Z)
            };
            messageParams.Add ("position", positionArray);

            messageParams.Add ("region_id", new OSDUUID (UUID.Zero));
            messageParams.Add ("to_id", new OSDUUID (toAgent));
            messageParams.Add ("source", new OSDInteger (0));

            OSDMap data = new OSDMap (1) { { "binary_bucket", OSD.FromBinary (binaryBucket) } };
            messageParams.Add ("data", data);
            messageParams.Add ("message", new OSDString (message));
            messageParams.Add ("id", new OSDUUID (transactionID));
            messageParams.Add ("from_name", new OSDString (fromName));
            messageParams.Add ("timestamp", new OSDInteger ((int)timeStamp));
            messageParams.Add ("offline", new OSDInteger (offline ? 1 : 0));
            messageParams.Add ("parent_estate_id", new OSDInteger (parentEstateID));
            messageParams.Add ("ttl", new OSDInteger ((int)ttl));
            messageParams.Add ("from_id", new OSDUUID (fromAgent));
            messageParams.Add ("from_group", new OSDInteger (fromGroup ? 1 : 0));

            return messageParams;
        }

        public static OSD InstantMessage (UUID fromAgent, string message, UUID toAgent,
                                          string fromName, byte dialog, uint timeStamp, bool offline, int parentEstateID,
                                          Vector3 position, uint ttl, UUID transactionID, bool fromGroup,
                                          byte[] binaryBucket,
                                          bool checkEstate, int godLevel, bool limitedToEstate)
        {
            OSDMap im = new OSDMap (2) { 
                { "message_params", InstantMessageParams (
                        fromAgent, message, toAgent,
                        fromName, dialog, timeStamp, offline,
                        parentEstateID,
                        position, ttl, transactionID, fromGroup,
                        binaryBucket)
                },
                { "agent_params", AgentParams (fromAgent, checkEstate, godLevel, limitedToEstate) }
            };

            return im;
        }

        public static OSD ChatterBoxSessionStartReply (string groupName, UUID groupID)
        {
            OSDMap moderatedMap = new OSDMap (4) { { "voice", OSD.FromBoolean (false) } };

            OSDMap sessionMap = new OSDMap (4) {
                { "moderated_mode", moderatedMap },
                { "session_name", OSD.FromString (groupName) },
                { "type", OSD.FromInteger (0) },
                { "voice_enabled", OSD.FromBoolean (false) }
            };

            OSDMap bodyMap = new OSDMap (4) {
                { "session_id", OSD.FromUUID (groupID) },
                { "temp_session_id", OSD.FromUUID (groupID) },
                { "success", OSD.FromBoolean (true) },
                { "session_info", sessionMap }
            };

            return buildEvent ("ChatterBoxSessionStartReply", bodyMap);
        }

        public static OSD ChatterboxInvitation (UUID sessionID, string sessionName,
                                                UUID fromAgent, string message, UUID toAgent, string fromName,
                                                byte dialog,
                                                uint timeStamp, bool offline, int parentEstateID, Vector3 position,
                                                uint ttl, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            OSDMap body = new OSDMap (5) {
                { "session_id", new OSDUUID (sessionID) },
                { "from_name", new OSDString (fromName) },
                { "session_name", new OSDString (sessionName) },
                { "from_id", new OSDUUID (fromAgent) },
                { "instantmessage", InstantMessage (
                        fromAgent, message, toAgent,
                        fromName, dialog, timeStamp, offline,
                        parentEstateID, position,
                        ttl, transactionID, fromGroup, binaryBucket, true,
                        0, true)
                }
            };

            OSDMap chatterboxInvitation = new OSDMap (2) {
                { "message", new OSDString ("ChatterBoxInvitation") },
                { "body", body }
            };
            return chatterboxInvitation;
        }

        public static OSD ChatterBoxSessionAgentListUpdates (UUID sessionID, UUID agentID, 
                                                             bool canVoiceChat, bool isModerator, bool textMute)
        {
            OSDMap body = new OSDMap ();
            OSDMap agentUpdates = new OSDMap ();
            OSDMap infoDetail = new OSDMap ();
            OSDMap mutes = new OSDMap { { "text", OSD.FromBoolean (textMute) } };

            infoDetail.Add ("can_voice_chat", OSD.FromBoolean (canVoiceChat));
            infoDetail.Add ("is_moderator", OSD.FromBoolean (isModerator));
            infoDetail.Add ("mutes", mutes);
            OSDMap info = new OSDMap { { "info", infoDetail } };
            agentUpdates.Add (agentID.ToString (), info);
            body.Add ("agent_updates", agentUpdates);
            body.Add ("session_id", OSD.FromUUID (sessionID));
            body.Add ("updates", new OSD ());

            OSDMap chatterBoxSessionAgentListUpdates = new OSDMap {
                { "message",OSD.FromString ("ChatterBoxSessionAgentListUpdates") },
                { "body", body }
            };

            return chatterBoxSessionAgentListUpdates;
        }

        internal static OSD ChatterBoxSessionAgentListUpdates (UUID sessionID,
                                                               ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock [] agentUpdatesBlock, 
                                                               string transition)
        {
            OSDMap body = new OSDMap ();
            OSDMap agentUpdates = new OSDMap ();
            OSDMap infoDetail = new OSDMap ();
            OSDMap mutes = new OSDMap ();

            foreach (ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block in agentUpdatesBlock)
            {
                infoDetail = new OSDMap ();
                mutes = new OSDMap
                            { { "text", OSD.FromBoolean (block.MuteText) }, { "voice", OSD.FromBoolean (block.MuteVoice) } };
                infoDetail.Add ("can_voice_chat", OSD.FromBoolean (block.CanVoiceChat));
                infoDetail.Add ("is_moderator", OSD.FromBoolean (block.IsModerator));
                infoDetail.Add ("mutes", mutes);
                OSDMap info = new OSDMap { { "info", infoDetail } };
                if (transition != string.Empty)
                    info.Add ("transition", OSD.FromString (transition));
                agentUpdates.Add (block.AgentID.ToString (), info);
            }
            body.Add ("agent_updates", agentUpdates);
            body.Add ("session_id", OSD.FromUUID (sessionID));
            body.Add ("updates", new OSD ());

            OSDMap chatterBoxSessionAgentListUpdates = new OSDMap {
                { "message",OSD.FromString ("ChatterBoxSessionAgentListUpdates") },
                { "body", body }
            };

            return chatterBoxSessionAgentListUpdates;
        }

        public static OSD GroupMembership (AgentGroupDataUpdatePacket groupUpdatePacket)
        {
            OSDMap groupUpdate = new OSDMap { { "message", OSD.FromString ("AgentGroupDataUpdate") } };

            OSDMap body = new OSDMap ();
            OSDArray agentData = new OSDArray ();
            OSDMap agentDataMap = new OSDMap { { "AgentID", OSD.FromUUID (groupUpdatePacket.AgentData.AgentID) } };
            agentData.Add (agentDataMap);
            body.Add ("AgentData", agentData);

            OSDArray groupData = new OSDArray ();

            foreach (OSDMap groupDataMap in 
                groupUpdatePacket.GroupData.Select(groupDataBlock => new OSDMap {
                    {"ListInProfile", OSD.FromBoolean(false)},
                    {"GroupID", OSD.FromUUID(groupDataBlock.GroupID)},
                    {"GroupInsigniaID", OSD.FromUUID(groupDataBlock.GroupInsigniaID)},
                    { "Contribution", OSD.FromInteger(groupDataBlock.Contribution)},
                    {"GroupPowers",OSD.FromBinary(ulongToByteArray(groupDataBlock.GroupPowers))},
                    {"GroupName",OSD.FromString(Utils.BytesToString(groupDataBlock.GroupName))},
                    {"AcceptNotices",OSD.FromBoolean(groupDataBlock.AcceptNotices)}
                }))
            {
                groupData.Add (groupDataMap);
            }

            body.Add ("GroupData", groupData);
            groupUpdate.Add ("body", body);

            return groupUpdate;
        }

        public static OSD PlacesQuery (PlacesReplyPacket placesReplyPkt, string[] regionType)
        {
            PlacesReplyMessage message = new PlacesReplyMessage ();
            message.AgentID = placesReplyPkt.AgentData.AgentID;
            message.QueryID = placesReplyPkt.AgentData.QueryID;
            message.TransactionID = placesReplyPkt.TransactionData.TransactionID;
            message.QueryDataBlocks = new PlacesReplyMessage.QueryData[placesReplyPkt.QueryData.Length];
            OSDMap placesReply = new OSDMap { { "message", OSD.FromString ("PlacesReplyMessage") } };

            int i = 0;
            foreach (PlacesReplyPacket.QueryDataBlock groupDataBlock in placesReplyPkt.QueryData)
            {
                message.QueryDataBlocks [i] = new PlacesReplyMessage.QueryData ();
                message.QueryDataBlocks [i].ActualArea = groupDataBlock.ActualArea;
                message.QueryDataBlocks [i].BillableArea = groupDataBlock.BillableArea;
                message.QueryDataBlocks [i].Description = Utils.BytesToString (groupDataBlock.Desc);
                message.QueryDataBlocks [i].Dwell = groupDataBlock.Dwell;
                message.QueryDataBlocks [i].Flags = groupDataBlock.Flags;
                message.QueryDataBlocks [i].GlobalX = groupDataBlock.GlobalX;
                message.QueryDataBlocks [i].GlobalY = groupDataBlock.GlobalY;
                message.QueryDataBlocks [i].GlobalZ = groupDataBlock.GlobalZ;
                message.QueryDataBlocks [i].Name = Utils.BytesToString (groupDataBlock.Name);
                message.QueryDataBlocks [i].OwnerID = groupDataBlock.OwnerID;
                message.QueryDataBlocks [i].SimName = Utils.BytesToString (groupDataBlock.SimName);
                message.QueryDataBlocks [i].SnapShotID = groupDataBlock.SnapshotID;
                message.QueryDataBlocks [i].ProductSku = regionType [i];
                message.QueryDataBlocks [i].Price = groupDataBlock.Price;

                i++;
            }

            OSDMap map = message.Serialize ();
            placesReply ["body"] = map;
            return placesReply;
        }

        public static OSD ParcelProperties (ParcelPropertiesMessage parcelPropertiesMessage)
        {
            OSDMap message = new OSDMap { { "message", OSD.FromString ("ParcelProperties") } };
            OSD message_body = parcelPropertiesMessage.Serialize ();
            message.Add ("body", message_body);
            return message;
        }

        public static OSD ParcelObjectOwnersReply (ParcelObjectOwnersReplyMessage parcelPropertiesMessage)
        {
            OSDMap message = new OSDMap { { "message", OSD.FromString ("ParcelObjectOwnersReply") } };
            OSD message_body = parcelPropertiesMessage.Serialize ();
            if (((OSDMap)message_body).ContainsKey ("DataExtended"))
            {
                OSDArray m = (OSDArray)((OSDMap)message_body) ["DataExtended"];
                int num = 0;
                foreach (OSDMap innerMap in m.Cast<OSDMap>())
                {
                    innerMap ["TimeStamp"] =
                        OSD.FromUInteger ((uint)Util.ToUnixTime (parcelPropertiesMessage.PrimOwnersBlock [num].TimeStamp));
                    num++;
                }
            }
            message.Add ("body", message_body);
            return message;
        }

        public static OSD LandStatReply (LandStatReplyMessage statReplyMessage)
        {
            OSDMap message = new OSDMap { { "message", OSD.FromString ("LandStatReply") } };
            OSD message_body = statReplyMessage.Serialize ();
            OSDArray m = (OSDArray)((OSDMap)message_body) ["DataExtended"];
            int num = 0;
            foreach (OSDMap innerMap in m.Cast<OSDMap>())
            {
                innerMap ["TimeStamp"] =
                    OSD.FromUInteger ((uint)Util.ToUnixTime (statReplyMessage.ReportDataBlocks [num].TimeStamp));
                num++;
            }
            message.Add ("body", message_body);
            return message;
        }
    }
}