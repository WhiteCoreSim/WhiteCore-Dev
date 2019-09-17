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
using System.Net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.ClientStack
{
    //public delegate bool PacketMethod (IClientAPI simClient, Packet packet);

    /// <summary>
    ///     Handles new client connections
    ///     Constructor takes a single Packet and authenticates everything
    /// </summary>
    public sealed partial class LLClientView : IClientAPI
    {


        #region Scene/Avatar to Client

        public void SendRegionHandshake (RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            var handshake = (RegionHandshakePacket)PacketPool.Instance.GetPacket (PacketType.RegionHandshake);
            handshake.RegionInfo = new RegionHandshakePacket.RegionInfoBlock {
                BillableFactor = args.billableFactor,
                IsEstateManager = args.isEstateManager,
                TerrainHeightRange00 = args.terrainHeightRange0,
                TerrainHeightRange01 = args.terrainHeightRange1,
                TerrainHeightRange10 = args.terrainHeightRange2,
                TerrainHeightRange11 = args.terrainHeightRange3,
                TerrainStartHeight00 = args.terrainStartHeight0,
                TerrainStartHeight01 = args.terrainStartHeight1,
                TerrainStartHeight10 = args.terrainStartHeight2,
                TerrainStartHeight11 = args.terrainStartHeight3,
                SimAccess = args.simAccess,
                WaterHeight = args.waterHeight,
                RegionFlags = (uint)args.regionFlags,
                SimName = Util.StringToBytes256 (args.regionName),
                SimOwner = args.SimOwner,
                TerrainBase0 = args.terrainBase0,
                TerrainBase1 = args.terrainBase1,
                TerrainBase2 = args.terrainBase2,
                TerrainBase3 = args.terrainBase3,
                TerrainDetail0 = args.terrainDetail0,
                TerrainDetail1 = args.terrainDetail1,
                TerrainDetail2 = args.terrainDetail2,
                TerrainDetail3 = args.terrainDetail3,
                CacheID = UUID.Random ()
            };

            //I guess this is for the client to remember an old setting?
            handshake.RegionInfo2 = new RegionHandshakePacket.RegionInfo2Block { RegionID = regionInfo.RegionID };
            handshake.RegionInfo4 = new RegionHandshakePacket.RegionInfo4Block [1]
                                        {
                                            new RegionHandshakePacket.RegionInfo4Block
                                                {
                                                    RegionFlagsExtended = args.regionFlags,
                                                    RegionProtocols = (ulong) RegionProtocols.None // RegionProtocols.AgentAppearanceService
                                                }
                                        };
            handshake.RegionInfo3 = new RegionHandshakePacket.RegionInfo3Block {
                CPUClassID = 9,
                CPURatio = 1,
                ColoName = Utils.EmptyBytes,
                ProductName = Util.StringToBytes256 (regionInfo.RegionType),
                ProductSKU = Utils.EmptyBytes
            };


            OutPacket (handshake, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// </summary>
        public void MoveAgentIntoRegion (RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            var mov = (AgentMovementCompletePacket)PacketPool.Instance.GetPacket (PacketType.AgentMovementComplete);
            mov.SimData.ChannelVersion = m_channelVersion;
            mov.AgentData.SessionID = m_sessionId;
            mov.AgentData.AgentID = AgentId;
            mov.Data.RegionHandle = regInfo.RegionHandle;
            mov.Data.Timestamp = (uint)Util.UnixTimeSinceEpoch ();
            mov.Data.Position = pos;
            mov.Data.LookAt = look;

            // Hack to get this out immediately and skip the throttles
            OutPacket (mov, ThrottleOutPacketType.OutBand);
        }

        public void SendChatMessage (string message, byte type, Vector3 fromPos, string fromName,
                                    UUID fromAgentID, byte source, byte audible)
        {
            var reply = (ChatFromSimulatorPacket)PacketPool.Instance.GetPacket (PacketType.ChatFromSimulator);
            reply.ChatData.Audible = audible;
            reply.ChatData.Message = Util.StringToBytes1024 (message);
            reply.ChatData.ChatType = type;
            reply.ChatData.SourceType = source;
            reply.ChatData.Position = fromPos;
            reply.ChatData.FromName = Util.StringToBytes256 (fromName);
            reply.ChatData.OwnerID = fromAgentID;
            reply.ChatData.SourceID = fromAgentID;

            //Don't split me up!
            reply.HasVariableBlocks = false;
            // Hack to get this out immediately and skip throttles
            OutPacket (reply, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendTelehubInfo (Vector3 TelehubPos, Quaternion TelehubRot, List<Vector3> SpawnPoint,
                                     UUID ObjectID, string nameT)
        {
            var packet = (TelehubInfoPacket)PacketPool.Instance.GetPacket (PacketType.TelehubInfo);
            packet.SpawnPointBlock = new TelehubInfoPacket.SpawnPointBlockBlock [SpawnPoint.Count];
            int i = 0;
            foreach (Vector3 pos in SpawnPoint) {
                packet.SpawnPointBlock [i] = new TelehubInfoPacket.SpawnPointBlockBlock { SpawnPointPos = pos };
                i++;
            }
            packet.TelehubBlock.ObjectID = ObjectID;
            packet.TelehubBlock.ObjectName = Utils.StringToBytes (nameT);
            packet.TelehubBlock.TelehubPos = TelehubPos;
            packet.TelehubBlock.TelehubRot = TelehubRot;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        /// <summary>
        ///     Send an instant message to this client
        /// </summary>
        //
        // Don't remove transaction ID! Groups and item gives need to set it!
        public void SendInstantMessage (GridInstantMessage im)
        {
            if (m_scene.Permissions.CanInstantMessage (im.FromAgentID, im.ToAgentID)) {
                var msg = (ImprovedInstantMessagePacket)PacketPool.Instance.GetPacket (PacketType.ImprovedInstantMessage);

                msg.AgentData.AgentID = im.FromAgentID;
                msg.AgentData.SessionID = UUID.Zero;
                msg.MessageBlock.FromAgentName = Util.StringToBytes256 (im.FromAgentName);
                msg.MessageBlock.Dialog = im.Dialog;
                msg.MessageBlock.FromGroup = im.FromGroup;
                if (im.SessionID == UUID.Zero)
                    msg.MessageBlock.ID = im.FromAgentID ^ im.ToAgentID;
                else
                    msg.MessageBlock.ID = im.SessionID;
                msg.MessageBlock.Offline = im.Offline;
                msg.MessageBlock.ParentEstateID = 0;
                msg.MessageBlock.Position = im.Position;
                msg.MessageBlock.RegionID = im.RegionID;
                msg.MessageBlock.Timestamp = im.Timestamp;
                msg.MessageBlock.ToAgentID = im.ToAgentID;
                msg.MessageBlock.Message = Util.StringToBytes1024 (im.Message);
                msg.MessageBlock.BinaryBucket = im.BinaryBucket;

                OutPacket (msg, ThrottleOutPacketType.AvatarInfo);
            }
        }

        public void SendGenericMessage (string method, List<string> message)
        {
            List<byte []> convertedmessage =
                message.ConvertAll (delegate (string item) { return Util.StringToBytes256 (item); });
            SendGenericMessage (method, convertedmessage);
        }

        public void SendGenericMessage (string method, List<byte []> message)
        {
            var gmp = new GenericMessagePacket {
                MethodData = { Method = Util.StringToBytes256 (method) },
                ParamList = new GenericMessagePacket.ParamListBlock [message.Count]
            };
            int i = 0;
            foreach (byte [] val in message) {
                gmp.ParamList [i] = new GenericMessagePacket.ParamListBlock ();
                gmp.ParamList [i++].Parameter = val;
            }

            OutPacket (gmp, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendGroupActiveProposals (UUID groupID, UUID transactionID, GroupActiveProposals [] Proposals)
        {
            var GAPIRP = new GroupActiveProposalItemReplyPacket {
                AgentData = { AgentID = AgentId, GroupID = groupID },
                TransactionData = {
                    TransactionID = transactionID,
                    TotalNumItems = (uint) Proposals.Length },
                ProposalData = new GroupActiveProposalItemReplyPacket.ProposalDataBlock [Proposals.Length]
            };

            int i = 0;
            foreach (
                GroupActiveProposalItemReplyPacket.ProposalDataBlock ProposalData in
                    Proposals.Select (Proposal => new GroupActiveProposalItemReplyPacket.ProposalDataBlock {
                        VoteCast = Utils.StringToBytes ("false"),
                        VoteID = new UUID (Proposal.VoteID),
                        VoteInitiator = new UUID (Proposal.VoteInitiator),
                        Majority = Convert.ToInt32 (Proposal.Majority),
                        Quorum = Convert.ToInt32 (Proposal.Quorum),
                        TerseDateID = Utils.StringToBytes (Proposal.TerseDateID),
                        StartDateTime = Utils.StringToBytes (Proposal.StartDateTime),
                        EndDateTime = Utils.StringToBytes (Proposal.EndDateTime),
                        ProposalText = Utils.StringToBytes (Proposal.ProposalText),
                        AlreadyVoted = false
                    })) {
                GAPIRP.ProposalData [i] = ProposalData;
                i++;
            }
            OutPacket (GAPIRP, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendGroupVoteHistory (UUID groupID, UUID transactionID, GroupVoteHistory Vote,
                                          GroupVoteHistoryItem [] VoteItems)
        {
            GroupVoteHistoryItemReplyPacket GVHIRP = new GroupVoteHistoryItemReplyPacket {
                AgentData = { AgentID = AgentId, GroupID = groupID },
                TransactionData = { TransactionID = transactionID,
                                    TotalNumItems = (uint) VoteItems.Length },
                HistoryItemData = { VoteID = new UUID(Vote.VoteID),
                                    VoteInitiator = new UUID(Vote.VoteInitiator),
                                    Majority = Convert.ToInt32(Vote.Majority),
                                    Quorum = Convert.ToInt32(Vote.Quorum),
                                    TerseDateID = Utils.StringToBytes(Vote.TerseDateID),
                                    StartDateTime = Utils.StringToBytes(Vote.StartDateTime),
                                    EndDateTime = Utils.StringToBytes(Vote.EndDateTime),
                                    VoteType = Utils.StringToBytes(Vote.VoteType),
                                    VoteResult = Utils.StringToBytes(Vote.VoteResult),
                                    ProposalText = Utils.StringToBytes(Vote.ProposalText) }
            };


            int i = 0;
            GVHIRP.VoteItem = new GroupVoteHistoryItemReplyPacket.VoteItemBlock [VoteItems.Length];

            foreach (
                GroupVoteHistoryItemReplyPacket.VoteItemBlock VoteItem in
                    VoteItems.Select (item => new GroupVoteHistoryItemReplyPacket.VoteItemBlock {
                        CandidateID = item.CandidateID,
                        NumVotes = item.NumVotes,
                        VoteCast = Utils.StringToBytes (item.VoteCast)
                    })) {
                GVHIRP.VoteItem [i] = VoteItem;
                i++;
            }
            OutPacket (GVHIRP, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendGroupAccountingDetails (IClientAPI sender, UUID groupID, UUID transactionID, UUID sessionID,
                                               int amt, int currentInterval, int interval, string startDate,
                                               GroupAccountHistory [] history)
        {
            GroupAccountDetailsReplyPacket GADRP = new GroupAccountDetailsReplyPacket {
                AgentData = new GroupAccountDetailsReplyPacket.AgentDataBlock { AgentID = sender.AgentId, GroupID = groupID },
                HistoryData = new GroupAccountDetailsReplyPacket.HistoryDataBlock [history.Length]
            };
            int i = 0;
            foreach (GroupAccountHistory h in history) {
                GroupAccountDetailsReplyPacket.HistoryDataBlock History =
                    new GroupAccountDetailsReplyPacket.HistoryDataBlock ();

                History.Amount = h.Amount;
                History.Description = Utils.StringToBytes (h.Description);

                GADRP.HistoryData [i++] = History;
            }
            GADRP.MoneyData = new GroupAccountDetailsReplyPacket.MoneyDataBlock {
                CurrentInterval = currentInterval,
                IntervalDays = interval,
                RequestID = transactionID,
                StartDate = Utils.StringToBytes (startDate)
            };
            OutPacket (GADRP, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendGroupAccountingSummary (IClientAPI sender, UUID groupID, UUID requestID, int moneyAmt,
                                                int totalTierDebits, int totalTierCredits,
                                                string startDate, int currentInterval,
                                                int intervalLength,
                                                string taxDate, string lastTaxDate, int parcelDirectoryFee,
                                                int landTaxFee, int groupTaxFee, int objectTaxFee)
        {
            GroupAccountSummaryReplyPacket GASRP =
                (GroupAccountSummaryReplyPacket)PacketPool.Instance.GetPacket (
                    PacketType.GroupAccountSummaryReply);

            GASRP.AgentData = new GroupAccountSummaryReplyPacket.AgentDataBlock { AgentID = sender.AgentId, GroupID = groupID };
            GASRP.MoneyData = new GroupAccountSummaryReplyPacket.MoneyDataBlock {
                Balance = moneyAmt,
                TotalCredits = totalTierCredits,
                TotalDebits = totalTierDebits,
                StartDate = Utils.StringToBytes (startDate + '\n'),
                CurrentInterval = currentInterval,
                GroupTaxCurrent = groupTaxFee,
                GroupTaxEstimate = groupTaxFee,
                IntervalDays = intervalLength,
                LandTaxCurrent = landTaxFee,
                LandTaxEstimate = landTaxFee,
                LastTaxDate = Utils.StringToBytes (lastTaxDate),
                LightTaxCurrent = 0,
                TaxDate = Utils.StringToBytes (taxDate),
                RequestID = requestID,
                ParcelDirFeeEstimate = parcelDirectoryFee,
                ParcelDirFeeCurrent = parcelDirectoryFee,
                ObjectTaxEstimate = objectTaxFee,
                NonExemptMembers = 0,
                ObjectTaxCurrent = objectTaxFee,
                LightTaxEstimate = 0
            };
            OutPacket (GASRP, ThrottleOutPacketType.Asset);
        }

        public void SendGroupTransactionsSummaryDetails (IClientAPI sender, UUID groupID, UUID transactionID,
                                                         UUID sessionID, int currentInterval, int intervalDays,
                                                         string startingDate, GroupAccountHistory [] history)
        {
            GroupAccountTransactionsReplyPacket GATRP =
                (GroupAccountTransactionsReplyPacket)PacketPool.Instance.GetPacket (
                    PacketType.GroupAccountTransactionsReply);

            GATRP.AgentData = new GroupAccountTransactionsReplyPacket.AgentDataBlock { AgentID = sender.AgentId, GroupID = groupID };
            GATRP.MoneyData = new GroupAccountTransactionsReplyPacket.MoneyDataBlock {
                CurrentInterval = currentInterval,
                IntervalDays = intervalDays,
                RequestID = transactionID,
                StartDate = Utils.StringToBytes (startingDate)
            };
            GATRP.HistoryData = new GroupAccountTransactionsReplyPacket.HistoryDataBlock [history.Length];
            int i = 0;
            foreach (GroupAccountHistory h in history) {
                GroupAccountTransactionsReplyPacket.HistoryDataBlock History =
                    new GroupAccountTransactionsReplyPacket.HistoryDataBlock {
                        Amount = h.Amount,
                        Item = Utils.StringToBytes (h.Description),
                        Time = Utils.StringToBytes (h.TimeString),
                        Type = 0,
                        User = Utils.StringToBytes (h.UserCausingCharge)
                    };
                GATRP.HistoryData [i++] = History;
            }
            OutPacket (GATRP, ThrottleOutPacketType.Asset);
        }

        /// <summary>
        ///     Send the region heightmap to the client
        /// </summary>
        /// <param name="map">heightmap</param>
        public void SendLayerData (short [] map)
        {
            DoSendLayerData (map);
            m_udpServer.FireAndForget (DoSendLayerData, map);
        }

        /// <summary>
        ///     Send terrain layer information to the client.
        /// </summary>
        /// <param name="o"></param>
        void DoSendLayerData (object o)
        {
            short [] map = (short [])o;
            try {
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize; y++) {
                    for (int x = 0; x < m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize; x += 4) {
                        SendLayerPacket (map, y, x);
                        //Thread.Sleep(35);
                    }
                }
            } catch (Exception e) {
                MainConsole.Instance.Warn ("[Client]: ClientView.API.cs: SendLayerData() - Failed with exception " + e);
            }
        }

        /// <summary>
        ///     Sends a set of four patches (x, x+1, ..., x+3) to the client
        /// </summary>
        /// <param name="map">heightmap</param>
        /// <param name="x">X coordinate for patches 0..12</param>
        /// <param name="y">Y coordinate for patches 0..15</param>
        public void SendLayerPacket (short [] map, int y, int x)
        {
            int [] xs = { x + 0, x + 1, x + 2, x + 3 };
            int [] ys = { y, y, y, y };

            try {
                byte type = (byte)TerrainPatch.LayerType.Land;
                if (m_scene.RegionInfo.RegionSizeX > Constants.RegionSize ||
                    m_scene.RegionInfo.RegionSizeY > Constants.RegionSize) {
                    type++;
                }
                LayerDataPacket layerpack = WhiteCoreTerrainCompressor.CreateLandPacket (map, xs, ys, type,
                                                                                         m_scene.RegionInfo.RegionSizeX,
                                                                                         m_scene.RegionInfo.RegionSizeY);
                layerpack.Header.Zerocoded = true;
                layerpack.Header.Reliable = true;

                if (layerpack.Length > 1000) {
                    // Oversize packet was created
                    for (int xa = 0; xa < 4; xa++) {
                        // Send oversize packet in individual patches
                        SendLayerData (x + xa, y, map);
                    }
                } else {
                    OutPacket (layerpack, ThrottleOutPacketType.Land);
                }
            } catch (OverflowException) {
                for (int xa = 0; xa < 4; xa++) {
                    // Send oversize packet in individual patches
                    SendLayerData (x + xa, y, map);
                }
            } catch (IndexOutOfRangeException) {
                for (int xa = 0; xa < 4; xa++) {
                    // Bad terrain, send individual chunks
                    SendLayerData (x + xa, y, map);
                }
            }
        }

        /// <summary>
        ///     Sends a specified patch to a client
        /// </summary>
        /// <param name="px">Patch coordinate (x) 0..regionSize/16</param>
        /// <param name="py">Patch coordinate (y) 0..regionSize/16</param>
        /// <param name="map">heightmap</param>
        public void SendLayerData (int px, int py, short [] map)
        {
            try {
                int [] x = { px };
                int [] y = { py };

                byte type = (byte)TerrainPatch.LayerType.Land;
                if (m_scene.RegionInfo.RegionSizeX > Constants.RegionSize ||
                    m_scene.RegionInfo.RegionSizeY > Constants.RegionSize) {
                    type++;
                }
                LayerDataPacket layerpack = WhiteCoreTerrainCompressor.CreateLandPacket (map, x, y, type,
                                                                                         m_scene.RegionInfo.RegionSizeX,
                                                                                         m_scene.RegionInfo.RegionSizeY);

                OutPacket (layerpack, ThrottleOutPacketType.Land);
            } catch (Exception e) {
                MainConsole.Instance.ErrorFormat ("[Client]: SendLayerData() Failed with exception: " + e);
            }
        }

        /// <summary>
        ///     Sends a specified patch to a client
        /// </summary>
        /// <param name="x">Patch coordinates (x) 0..regionSize/16</param>
        /// <param name="y">Patch coordinates (y) 0..regionSize/16</param>
        /// <param name="map">heightmap</param>
        /// <param name="layertype"></param>
        public void SendLayerData (int [] x, int [] y, short [] map, TerrainPatch.LayerType layertype)
        {
            const int MaxPatches = 10;
            byte type = (byte)layertype;
            if (m_scene.RegionInfo.RegionSizeX > Constants.RegionSize ||
                m_scene.RegionInfo.RegionSizeY > Constants.RegionSize) {
                if (layertype == TerrainPatch.LayerType.Land || layertype == TerrainPatch.LayerType.Water)
                    type++;
                else
                    type += 2;
            }
            //Only send 10 at a time
            for (int i = 0; i < x.Length; i += MaxPatches) {
                int Size = (x.Length - i) - 10 > 0 ? 10 : (x.Length - i);
                try {
                    //Find the size for the array
                    int [] xTemp = new int [Size];
                    int [] yTemp = new int [Size];

                    //Copy the arrays
                    Array.Copy (x, i, xTemp, 0, Size);
                    Array.Copy (y, i, yTemp, 0, Size);

                    //Build the packet
                    LayerDataPacket layerpack = WhiteCoreTerrainCompressor.CreateLandPacket (map, xTemp, yTemp, type,
                                                                                             m_scene.RegionInfo.RegionSizeX,
                                                                                             m_scene.RegionInfo.RegionSizeY);

                    layerpack.Header.Zerocoded = true;
                    layerpack.Header.Reliable = true;

                    if (layerpack.Length > 1000) {
                        // Oversize packet was created
                        for (int xa = 0; xa < Size; xa++) {
                            // Send oversize packet in individual patches
                            //
                            SendLayerData (x [i + xa], y [i + xa], map);
                        }
                    } else {
                        OutPacket (layerpack, ThrottleOutPacketType.Land);
                    }
                } catch (OverflowException) {
                    for (int xa = 0; xa < Size; xa++) {
                        // Send oversize packet in individual patches
                        SendLayerData (x [i + xa], y [i + xa], map);
                    }
                } catch (IndexOutOfRangeException) {
                    for (int xa = 0; xa < Size; xa++) {
                        // Bad terrain, send individual chunks
                        SendLayerData (x [i + xa], y [i + xa], map);
                    }
                }
            }
        }

        /// <summary>
        ///     Send the wind matrix to the client
        /// </summary>
        /// <param name="windSpeeds">16x16 array of wind speeds</param>
        public void SendWindData (Vector2 [] windSpeeds)
        {
            m_udpServer.FireAndForget (DoSendWindData, windSpeeds);
        }

        /// <summary>
        ///     Send the cloud matrix to the client
        /// </summary>
        /// <param name="cloudDensity">16x16 array of cloud densities</param>
        public void SendCloudData (float [] cloudDensity)
        {
            m_udpServer.FireAndForget (DoSendCloudData, cloudDensity);
        }

        /// <summary>
        ///     Send wind layer information to the client.
        /// </summary>
        /// <param name="o"></param>
        void DoSendWindData (object o)
        {
            Vector2 [] windSpeeds = (Vector2 [])o;
            TerrainPatch [] patches = new TerrainPatch [2];
            patches [0] = new TerrainPatch { Data = new float [16 * 16] };
            patches [1] = new TerrainPatch { Data = new float [16 * 16] };


            //            for (int y = 0; y < 16*16; y+=16)
            //            {
            for (int x = 0; x < 16 * 16; x++) {
                patches [0].Data [x] = windSpeeds [x].X;
                patches [1].Data [x] = windSpeeds [x].Y;
            }
            //            }
            byte type = (byte)TerrainPatch.LayerType.Wind;
            if (m_scene.RegionInfo.RegionSizeX > Constants.RegionSize ||
                m_scene.RegionInfo.RegionSizeY > Constants.RegionSize) {
                type += 2;
            }
            LayerDataPacket layerpack = WhiteCoreTerrainCompressor.CreateLayerDataPacket (patches, type,
                                                                                          m_scene.RegionInfo.RegionSizeX,
                                                                                          m_scene.RegionInfo.RegionSizeY);
            layerpack.Header.Zerocoded = true;
            OutPacket (layerpack, ThrottleOutPacketType.Wind);
        }

        /// <summary>
        ///     Send cloud layer information to the client.
        /// </summary>
        /// <param name="o"></param>
        void DoSendCloudData (object o)
        {
            float [] cloudCover = (float [])o;
            TerrainPatch [] patches = new TerrainPatch [1];
            patches [0] = new TerrainPatch { Data = new float [16 * 16] };

            //            for (int y = 0; y < 16*16; y+=16)
            //{
            for (int x = 0; x < 16 * 16; x++) {
                patches [0].Data [x] = cloudCover [x];
            }
            //}

            byte type = (byte)TerrainPatch.LayerType.Cloud;
            if (m_scene.RegionInfo.RegionSizeX > Constants.RegionSize ||
                m_scene.RegionInfo.RegionSizeY > Constants.RegionSize) {
                type += 2;
            }
            LayerDataPacket layerpack = WhiteCoreTerrainCompressor.CreateLayerDataPacket (patches, type,
                                                                                          m_scene.RegionInfo.RegionSizeX,
                                                                                          m_scene.RegionInfo.RegionSizeY);
            layerpack.Header.Zerocoded = true;
            OutPacket (layerpack, ThrottleOutPacketType.Cloud);
        }

        public AgentCircuitData RequestClientInfo ()
        {
            AgentCircuitData agentData = new AgentCircuitData {
                IsChildAgent = false,
                AgentID = AgentId,
                SessionID = m_sessionId,
                SecureSessionID = SecureSessionId,
                CircuitCode = m_circuitCode
            };

            AgentCircuitData currentAgentCircuit = m_udpServer.m_circuitManager.GetAgentCircuitData (AgentId);
            if (currentAgentCircuit != null)
                agentData.IPAddress = currentAgentCircuit.IPAddress;

            return agentData;
        }

        internal void SendMapBlockSplit (List<MapBlockData> mapBlocks, uint flag)
        {
            MapBlockReplyPacket mapReply = (MapBlockReplyPacket)PacketPool.Instance.GetPacket (PacketType.MapBlockReply);
            // TODO: don't create new blocks if recycling an old packet

            MapBlockData [] mapBlocks2 = mapBlocks.ToArray ();

            mapReply.AgentData.AgentID = AgentId;
            mapReply.Data = new MapBlockReplyPacket.DataBlock [mapBlocks2.Length];
            mapReply.Size = new MapBlockReplyPacket.SizeBlock [mapBlocks2.Length];
            mapReply.AgentData.Flags = flag;

            for (int i = 0; i < mapBlocks2.Length; i++) {
                mapReply.Data [i] = new MapBlockReplyPacket.DataBlock { MapImageID = mapBlocks2 [i].MapImageID, X = mapBlocks2 [i].X, Y = mapBlocks2 [i].Y };
                mapReply.Size [i] = new MapBlockReplyPacket.SizeBlock { SizeX = mapBlocks2 [i].SizeX, SizeY = mapBlocks2 [i].SizeY };
                mapReply.Data [i].WaterHeight = mapBlocks2 [i].WaterHeight;
                mapReply.Data [i].Name = Utils.StringToBytes (mapBlocks2 [i].Name);
                mapReply.Data [i].RegionFlags = mapBlocks2 [i].RegionFlags;
                mapReply.Data [i].Access = mapBlocks2 [i].Access;
                mapReply.Data [i].Agents = mapBlocks2 [i].Agents;
            }
            OutPacket (mapReply, ThrottleOutPacketType.Land);
        }

        public void SendMapBlock (List<MapBlockData> mapBlocks, uint flag)
        {
            MapBlockData [] mapBlocks2 = mapBlocks.ToArray ();

            const int maxsend = 10;

            //int packets = Math.Ceiling(mapBlocks2.Length / maxsend);

            List<MapBlockData> sendingBlocks = new List<MapBlockData> ();

            for (int i = 0; i < mapBlocks2.Length; i++) {
                sendingBlocks.Add (mapBlocks2 [i]);
                if (((i + 1) == mapBlocks2.Length) || (((i + 1) % maxsend) == 0)) {
                    SendMapBlockSplit (sendingBlocks, flag);
                    sendingBlocks = new List<MapBlockData> ();
                }
            }
        }

        public void SendLocalTeleport (Vector3 position, Vector3 lookAt, uint flags)
        {
            TeleportLocalPacket tpLocal = (TeleportLocalPacket)PacketPool.Instance.GetPacket (PacketType.TeleportLocal);
            tpLocal.Info.AgentID = AgentId;
            tpLocal.Info.TeleportFlags = flags;
            tpLocal.Info.LocationID = 2;
            tpLocal.Info.LookAt = lookAt;
            tpLocal.Info.Position = position;

            // Hack to get this out immediately and skip throttles
            OutPacket (tpLocal, ThrottleOutPacketType.OutBand);
        }

        public void SendRegionTeleport (ulong regionHandle, byte simAccess, IPEndPoint newRegionEndPoint,
                                       uint locationID,
                                       uint flags, string capsURL)
        {
            //TeleportFinishPacket teleport = (TeleportFinishPacket)PacketPool.Instance.GetPacket(PacketType.TeleportFinish);

            TeleportFinishPacket teleport = new TeleportFinishPacket {
                Info = { AgentID = AgentId,
                         RegionHandle = regionHandle,
                         SimAccess = simAccess,
                         SeedCapability = Util.StringToBytes256(capsURL) }
            };


            IPAddress oIP = newRegionEndPoint.Address;
            byte [] byteIP = oIP.GetAddressBytes ();
            uint ip = (uint)byteIP [3] << 24;
            ip += (uint)byteIP [2] << 16;
            ip += (uint)byteIP [1] << 8;
            ip += byteIP [0];

            teleport.Info.SimIP = ip;
            teleport.Info.SimPort = (ushort)newRegionEndPoint.Port;
            teleport.Info.LocationID = 4;
            teleport.Info.TeleportFlags = 1 << 4;

            // Hack to get this out immediately and skip throttles.
            OutPacket (teleport, ThrottleOutPacketType.OutBand);
        }

        /// <summary>
        ///     Inform the client that a teleport attempt has failed
        /// </summary>
        public void SendTeleportFailed (string reason)
        {
            TeleportFailedPacket tpFailed =
                (TeleportFailedPacket)PacketPool.Instance.GetPacket (PacketType.TeleportFailed);
            tpFailed.Info.AgentID = AgentId;
            tpFailed.Info.Reason = Util.StringToBytes256 (reason);
            tpFailed.AlertInfo = new TeleportFailedPacket.AlertInfoBlock [0];

            // Hack to get this out immediately and skip throttles
            OutPacket (tpFailed, ThrottleOutPacketType.OutBand);
        }

        /// <summary>
        /// </summary>
        public void SendTeleportStart (uint flags)
        {
            TeleportStartPacket tpStart = (TeleportStartPacket)PacketPool.Instance.GetPacket (PacketType.TeleportStart);
            //TeleportStartPacket tpStart = new TeleportStartPacket();
            tpStart.Info.TeleportFlags = flags; //16; // Teleport via location

            // Hack to get this out immediately and skip throttles
            OutPacket (tpStart, ThrottleOutPacketType.OutBand);
        }

        public void SendTeleportProgress (uint flags, string message)
        {
            TeleportProgressPacket tpProgress =
                (TeleportProgressPacket)PacketPool.Instance.GetPacket (PacketType.TeleportProgress);
            tpProgress.AgentData.AgentID = AgentId;
            tpProgress.Info.TeleportFlags = flags;
            tpProgress.Info.Message = Util.StringToBytes256 (message);

            // Hack to get this out immediately and skip throttles
            OutPacket (tpProgress, ThrottleOutPacketType.OutBand);
        }

        public void SendMoneyBalance (UUID transaction, bool success, byte [] description, int balance)
        {
            MoneyBalanceReplyPacket money =
                (MoneyBalanceReplyPacket)PacketPool.Instance.GetPacket (PacketType.MoneyBalanceReply);
            money.MoneyData.AgentID = AgentId;
            money.MoneyData.TransactionID = transaction;
            money.MoneyData.TransactionSuccess = success;
            money.MoneyData.Description = description;
            money.MoneyData.MoneyBalance = balance;
            money.TransactionInfo.ItemDescription = Util.StringToBytes256 ("NONE");
            OutPacket (money, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendPayPrice (UUID objectID, int [] payPrice)
        {
            if (payPrice [0] == 0 &&
                payPrice [1] == 0 &&
                payPrice [2] == 0 &&
                payPrice [3] == 0 &&
                payPrice [4] == 0)
                return;

            PayPriceReplyPacket payPriceReply =
                (PayPriceReplyPacket)PacketPool.Instance.GetPacket (PacketType.PayPriceReply);
            payPriceReply.ObjectData.ObjectID = objectID;
            payPriceReply.ObjectData.DefaultPayPrice = payPrice [0];

            payPriceReply.ButtonData = new PayPriceReplyPacket.ButtonDataBlock [4];
            payPriceReply.ButtonData [0] = new PayPriceReplyPacket.ButtonDataBlock { PayButton = payPrice [1] };
            payPriceReply.ButtonData [1] = new PayPriceReplyPacket.ButtonDataBlock { PayButton = payPrice [2] };
            payPriceReply.ButtonData [2] = new PayPriceReplyPacket.ButtonDataBlock { PayButton = payPrice [3] };
            payPriceReply.ButtonData [3] = new PayPriceReplyPacket.ButtonDataBlock { PayButton = payPrice [4] };

            OutPacket (payPriceReply, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendPlacesQuery (ExtendedLandData [] LandData, UUID queryID, UUID transactionID)
        {
            PlacesReplyPacket PlacesReply = new PlacesReplyPacket ();

            PlacesReplyPacket.QueryDataBlock [] Query = new PlacesReplyPacket.QueryDataBlock [LandData.Length + 1];

            // Since we don't have Membership we should send an empty QueryData block 
            // here to keep the viewer happy

            PlacesReplyPacket.QueryDataBlock MembershipBlock = new PlacesReplyPacket.QueryDataBlock {
                ActualArea = 0,
                BillableArea = 0,
                Desc = Utils.StringToBytes (""),
                Dwell = 0,
                Flags = 0,
                GlobalX = 0,
                GlobalY = 0,
                GlobalZ = 0,
                Name = Utils.StringToBytes (""),
                OwnerID = UUID.Zero,
                Price = 0,
                SimName = Utils.StringToBytes (""),
                SnapshotID = UUID.Zero
            };
            Query [0] = MembershipBlock;

            //Note: Nothing is ever done with this?????
            int totalarea = 0;
            List<string> RegionTypes = new List<string> ();
            for (int i = 0; i < LandData.Length; i++) {
                PlacesReplyPacket.QueryDataBlock QueryBlock = new PlacesReplyPacket.QueryDataBlock {
                    ActualArea = LandData [i].LandData.Area,
                    BillableArea = LandData [i].LandData.Area,
                    Desc = Utils.StringToBytes (LandData [i].LandData.Description),
                    Dwell = LandData [i].LandData.Dwell,
                    Flags = 0,
                    GlobalX = LandData [i].GlobalPosX,
                    GlobalY = LandData [i].GlobalPosY,
                    GlobalZ = 0,
                    Name = Utils.StringToBytes (LandData [i].LandData.Name),
                    OwnerID = LandData [i].LandData.OwnerID,
                    Price = LandData [i].LandData.SalePrice,
                    SimName = Utils.StringToBytes (LandData [i].RegionName),
                    SnapshotID = LandData [i].LandData.SnapshotID
                };
                Query [i + 1] = QueryBlock;
                totalarea += LandData [i].LandData.Area;
                RegionTypes.Add (LandData [i].RegionType);
            }
            PlacesReply.QueryData = Query;
            PlacesReply.AgentData = new PlacesReplyPacket.AgentDataBlock { AgentID = AgentId, QueryID = queryID };
            PlacesReply.TransactionData.TransactionID = transactionID;
            try {
                OutPacket (PlacesReply, ThrottleOutPacketType.AvatarInfo);
                //Disabled for now... it doesn't seem to work right...
                /*IEventQueueService eq = Scene.RequestModuleInterface<IEventQueueService>();
                if (eq != null)
                {
                    eq.QueryReply(PlacesReply, AgentId, RegionTypes.ToArray(), Scene.RegionInfo.RegionHandle);
                }*/
            } catch (Exception ex) {
                MainConsole.Instance.Error ("[Clcient]: Unable to send group membership data via eventqueue - exception: " + ex);
                MainConsole.Instance.Warn ("[Client]: Sending places query data via UDP");
                OutPacket (PlacesReply, ThrottleOutPacketType.AvatarInfo);
            }
        }

        public void SendStartPingCheck (byte seq)
        {
            StartPingCheckPacket pc = (StartPingCheckPacket)PacketPool.Instance.GetPacket (PacketType.StartPingCheck);
            pc.Header.Reliable = false;

            pc.PingID.PingID = seq;
            // We *could* get OldestUnacked, but it would hurt performance and not provide any benefit
            pc.PingID.OldestUnacked = 0;

            OutPacket (pc, ThrottleOutPacketType.OutBand);
        }

        public void SendKillObject (ulong regionHandle, IEntity [] entities)
        {
            if (entities.Length == 0)
                return; //........... why!

            //            MainConsole.Instance.DebugFormat("[Client]: Sending KillObjectPacket to {0} for {1} in {2}", Name, localID, regionHandle);

            KillObjectPacket kill = (KillObjectPacket)PacketPool.Instance.GetPacket (PacketType.KillObject);
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock [entities.Length];
            int i = 0;
            bool brokenUpdate = false;

            foreach (IEntity entity in entities) {
                if (entity == null) {
                    brokenUpdate = true;
                    continue;
                }

                /*if ((entity is SceneObjectPart &&
                    ((SceneObjectPart)entity).IsAttachment) ||
                    (entity is SceneObjectGroup &&
                    ((SceneObjectGroup)entity).RootPart.IsAttachment))
                {
                    // Do nothing
                }
                else if(entity is SceneObjectPart)
                    m_killRecord.Add(entity.LocalId);*/
                KillObjectPacket.ObjectDataBlock block = new KillObjectPacket.ObjectDataBlock { ID = entity.LocalId };
                kill.ObjectData [i] = block;
                i++;
            }
            //If the # of entities is not correct, we have to rebuild the entire packet
            if (brokenUpdate) {
                int count = kill.ObjectData.Count (block => block != null);
                i = 0;
                KillObjectPacket.ObjectDataBlock [] bk = new KillObjectPacket.ObjectDataBlock [count];

                foreach (KillObjectPacket.ObjectDataBlock block in kill.ObjectData.Where (block => block != null)) {
                    bk [i] = block;
                    i++;
                }
                kill.ObjectData = bk;
            }

            kill.Header.Reliable = true;
            kill.Header.Zerocoded = true;

            OutPacket (kill, ThrottleOutPacketType.Task);
        }

        public void SendKillObject (ulong regionHandle, uint [] entities)
        {
            if (entities.Length == 0)
                return; //........... why!

            //            MainConsole.Instance.DebugFormat("[Client]: Sending KillObjectPacket to {0} for {1} in {2}", Name, localID, regionHandle);

            KillObjectPacket kill = (KillObjectPacket)PacketPool.Instance.GetPacket (PacketType.KillObject);
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock [entities.Length];
            int i = 0;

            foreach (
                KillObjectPacket.ObjectDataBlock block in
                    entities.Select (entity => new KillObjectPacket.ObjectDataBlock { ID = entity })) {
                kill.ObjectData [i] = block;
                i++;
            }

            kill.Header.Reliable = true;
            kill.Header.Zerocoded = true;

            OutPacket (kill, ThrottleOutPacketType.Task);
        }

        /// <summary>
        ///     Send information about the items contained in a folder to the client.
        ///     XXX This method needs some refactoring loving
        /// </summary>
        /// <param name="ownerID">The owner of the folder</param>
        /// <param name="folderID">The id of the folder</param>
        /// <param name="items">The items contained in the folder identified by folderID</param>
        /// <param name="folders"></param>
        /// <param name="version"></param>
        /// <param name="fetchFolders">Do we need to send folder information?</param>
        /// <param name="fetchItems">Do we need to send item information?</param>
        public void SendInventoryFolderDetails (UUID ownerID, UUID folderID, List<InventoryItemBase> items,
                                               List<InventoryFolderBase> folders, int version,
                                               bool fetchFolders, bool fetchItems)
        {
            // An inventory descendents packet consists of a single agent section and an inventory details
            // section for each inventory item.  The size of each inventory item is approximately 550 bytes.
            // In theory, UDP has a maximum packet size of 64k, so it should be possible to send descendent
            // packets containing metadata for in excess of 100 items.  But in practice, there may be other
            // factors (e.g. firewalls) restraining the maximum UDP packet size.  See,
            //
            // http://opensimulator.org/mantis/view.php?id=226
            //
            // for one example of this kind of thing.  In fact, the Linden servers appear to only send about
            // 6 to 7 items at a time, so let's stick with 6
            const int MAX_ITEMS_PER_PACKET = 5;
            const int MAX_FOLDERS_PER_PACKET = 6;

            if (items == null || folders == null)
                return; //This DOES happen when things time out!!

            int totalItems = fetchItems ? items.Count : 0;
            int totalFolders = fetchFolders ? folders.Count : 0;
            int itemsSent = 0;
            int foldersSent = 0;
            int foldersToSend = 0;
            int itemsToSend = 0;

            InventoryDescendentsPacket currentPacket = null;

            // Handle empty folders
            //
            if (totalItems == 0 && totalFolders == 0)
                currentPacket = CreateInventoryDescendentsPacket (ownerID, folderID, version,
                                                                  items.Count + folders.Count,
                                                                  0, 0);

            // To preserve SL compatibility, we will NOT combine folders and items in one packet
            //
            while (itemsSent < totalItems || foldersSent < totalFolders) {
                if (currentPacket == null) // Start a new packet
                {
                    foldersToSend = totalFolders - foldersSent;
                    if (foldersToSend > MAX_FOLDERS_PER_PACKET)
                        foldersToSend = MAX_FOLDERS_PER_PACKET;

                    if (foldersToSend == 0) {
                        itemsToSend = totalItems - itemsSent;
                        if (itemsToSend > MAX_ITEMS_PER_PACKET)
                            itemsToSend = MAX_ITEMS_PER_PACKET;
                    }

                    currentPacket = CreateInventoryDescendentsPacket (ownerID,
                                                                      folderID, version,
                                                                      items.Count + folders.Count,
                                                                      foldersToSend,
                                                                      itemsToSend);
                }

                if (foldersToSend-- > 0)
                    currentPacket.FolderData [foldersSent % MAX_FOLDERS_PER_PACKET] =
                        CreateFolderDataBlock (folders [foldersSent++]);
                else if (itemsToSend-- > 0)
                    currentPacket.ItemData [itemsSent % MAX_ITEMS_PER_PACKET] = CreateItemDataBlock (items [itemsSent++]);
                else {
                    OutPacket (currentPacket, ThrottleOutPacketType.Asset, false, null);
                    currentPacket = null;
                }
            }

            if (currentPacket != null)
                OutPacket (currentPacket, ThrottleOutPacketType.Asset, false, null);
        }

        InventoryDescendentsPacket.FolderDataBlock CreateFolderDataBlock (InventoryFolderBase folder)
        {
            InventoryDescendentsPacket.FolderDataBlock newBlock = new InventoryDescendentsPacket.FolderDataBlock {
                FolderID = folder.ID,
                Name = Util.StringToBytes256 (folder.Name),
                ParentID = folder.ParentID,
                Type = (sbyte)folder.Type
            };

            return newBlock;
        }

        InventoryDescendentsPacket.ItemDataBlock CreateItemDataBlock (InventoryItemBase item)
        {
            InventoryDescendentsPacket.ItemDataBlock newBlock = new InventoryDescendentsPacket.ItemDataBlock {
                ItemID = item.ID,
                AssetID = item.AssetID,
                CreatorID = item.CreatorIdAsUuid,
                BaseMask = item.BasePermissions,
                Description = Util.StringToBytes256 (item.Description),
                EveryoneMask = item.EveryOnePermissions,
                OwnerMask = item.CurrentPermissions,
                FolderID = item.Folder,
                InvType = (sbyte)item.InvType,
                Name = Util.StringToBytes256 (item.Name),
                NextOwnerMask = item.NextPermissions,
                OwnerID = item.Owner,
                Type = Util.CheckMeshType ((sbyte)item.AssetType),
                GroupID = item.GroupID,
                GroupOwned = item.GroupOwned,
                GroupMask = item.GroupPermissions,
                CreationDate = item.CreationDate,
                SalePrice = item.SalePrice,
                SaleType = item.SaleType,
                Flags = item.Flags
            };


            newBlock.CRC = Helpers.InventoryCRC (newBlock.CreationDate, newBlock.SaleType,
                                                 newBlock.InvType, newBlock.Type,
                                                 newBlock.AssetID, newBlock.GroupID,
                                                 newBlock.SalePrice,
                                                 newBlock.OwnerID, newBlock.CreatorID,
                                                 newBlock.ItemID, newBlock.FolderID,
                                                 newBlock.EveryoneMask,
                                                 newBlock.Flags, newBlock.OwnerMask,
                                                 newBlock.GroupMask, newBlock.NextOwnerMask);

            return newBlock;
        }

        void AddNullFolderBlockToDecendentsPacket (ref InventoryDescendentsPacket packet)
        {
            packet.FolderData = new InventoryDescendentsPacket.FolderDataBlock [1];
            packet.FolderData [0] = new InventoryDescendentsPacket.FolderDataBlock {
                FolderID = UUID.Zero, ParentID = UUID.Zero, Type = -1, Name = new byte [0]
            };
        }

        void AddNullItemBlockToDescendentsPacket (ref InventoryDescendentsPacket packet)
        {
            packet.ItemData = new InventoryDescendentsPacket.ItemDataBlock [1];
            packet.ItemData [0] = new InventoryDescendentsPacket.ItemDataBlock {
                ItemID = UUID.Zero,
                AssetID = UUID.Zero,
                CreatorID = UUID.Zero,
                BaseMask = 0,
                Description = new byte [0],
                EveryoneMask = 0,
                OwnerMask = 0,
                FolderID = UUID.Zero,
                InvType = 0,
                Name = new byte [0],
                NextOwnerMask = 0,
                OwnerID = UUID.Zero,
                Type = -1,
                GroupID = UUID.Zero,
                GroupOwned = false,
                GroupMask = 0,
                CreationDate = 0,
                SalePrice = 0,
                SaleType = 0,
                Flags = 0
            };

            // No need to add CRC
        }

        InventoryDescendentsPacket CreateInventoryDescendentsPacket (UUID ownerID, UUID folderID, int version,
                                                                     int descendents, int folders, int items)
        {
            InventoryDescendentsPacket descend =
                (InventoryDescendentsPacket)PacketPool.Instance.GetPacket (PacketType.InventoryDescendents);
            descend.Header.Zerocoded = true;
            descend.AgentData.AgentID = AgentId;
            descend.AgentData.OwnerID = ownerID;
            descend.AgentData.FolderID = folderID;
            descend.AgentData.Version = version;
            descend.AgentData.Descendents = descendents;

            if (folders > 0)
                descend.FolderData = new InventoryDescendentsPacket.FolderDataBlock [folders];
            else
                AddNullFolderBlockToDecendentsPacket (ref descend);

            if (items > 0)
                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock [items];
            else
                AddNullItemBlockToDescendentsPacket (ref descend);

            return descend;
        }

        public void SendInventoryItemDetails (UUID ownerID, InventoryItemBase item)
        {
            const uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;

            FetchInventoryReplyPacket inventoryReply =
                (FetchInventoryReplyPacket)PacketPool.Instance.GetPacket (PacketType.FetchInventoryReply);
            // TODO: don't create new blocks if recycling an old packet
            inventoryReply.AgentData.AgentID = AgentId;
            inventoryReply.InventoryData = new FetchInventoryReplyPacket.InventoryDataBlock [1];
            inventoryReply.InventoryData [0] = new FetchInventoryReplyPacket.InventoryDataBlock {
                ItemID = item.ID,
                AssetID = item.AssetID,
                CreatorID = item.CreatorIdAsUuid,
                BaseMask = item.BasePermissions,
                CreationDate = item.CreationDate,
                Description = Util.StringToBytes256 (item.Description),
                EveryoneMask = item.EveryOnePermissions,
                FolderID = item.Folder,
                InvType = (sbyte)item.InvType,
                Name = Util.StringToBytes256 (item.Name),
                NextOwnerMask = item.NextPermissions,
                OwnerID = item.Owner,
                OwnerMask = item.CurrentPermissions,
                Type = Util.CheckMeshType ((sbyte)item.AssetType),
                GroupID = item.GroupID,
                GroupOwned = item.GroupOwned,
                GroupMask = item.GroupPermissions,
                Flags = item.Flags,
                SalePrice = item.SalePrice,
                SaleType = item.SaleType
            };


            inventoryReply.InventoryData [0].CRC =
                Helpers.InventoryCRC (
                    1000, 0, inventoryReply.InventoryData [0].InvType,
                    inventoryReply.InventoryData [0].Type, inventoryReply.InventoryData [0].AssetID,
                    inventoryReply.InventoryData [0].GroupID, 100,
                    inventoryReply.InventoryData [0].OwnerID, inventoryReply.InventoryData [0].CreatorID,
                    inventoryReply.InventoryData [0].ItemID, inventoryReply.InventoryData [0].FolderID,
                    FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                    FULL_MASK_PERMISSIONS);
            inventoryReply.Header.Zerocoded = true;
            OutPacket (inventoryReply, ThrottleOutPacketType.Asset);
        }

        public void SendBulkUpdateInventory (InventoryFolderBase folder)
        {
            // We will use the same transaction id for all the separate packets to be sent out in this update.
            UUID transactionId = UUID.Random ();

            List<BulkUpdateInventoryPacket.FolderDataBlock> folderDataBlocks
                = new List<BulkUpdateInventoryPacket.FolderDataBlock> ();

            SendBulkUpdateInventoryFolderRecursive (folder, ref folderDataBlocks, transactionId);

            if (folderDataBlocks.Count > 0) {
                // We'll end up with some unsent folder blocks if there were some empty folders at the end of the list
                // Send these now
                BulkUpdateInventoryPacket bulkUpdate
                    = (BulkUpdateInventoryPacket)PacketPool.Instance.GetPacket (PacketType.BulkUpdateInventory);
                bulkUpdate.Header.Zerocoded = true;

                bulkUpdate.AgentData.AgentID = AgentId;
                bulkUpdate.AgentData.TransactionID = transactionId;
                bulkUpdate.FolderData = folderDataBlocks.ToArray ();
                List<BulkUpdateInventoryPacket.ItemDataBlock> foo = new List<BulkUpdateInventoryPacket.ItemDataBlock> ();
                bulkUpdate.ItemData = foo.ToArray ();

                //MainConsole.Instance.Debug("SendBulkUpdateInventory :" + bulkUpdate);
                OutPacket (bulkUpdate, ThrottleOutPacketType.Asset);
            }
        }

        /// <summary>
        ///     Recursively construct bulk update packets to send folders and items
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="folderDataBlocks"></param>
        /// <param name="transactionId"></param>
        void SendBulkUpdateInventoryFolderRecursive (InventoryFolderBase folder,
                                                     ref List<BulkUpdateInventoryPacket.FolderDataBlock> folderDataBlocks,
                                                     UUID transactionId)
        {
            folderDataBlocks.Add (GenerateBulkUpdateFolderDataBlock (folder));

            const int MAX_ITEMS_PER_PACKET = 5;

            IInventoryService invService = m_scene.RequestModuleInterface<IInventoryService> ();
            // If there are any items then we have to start sending them off in this packet - the next folder will have
            // to be in its own bulk update packet.  Also, we can only fit 5 items in a packet (at least this was the limit
            // being used on the Linden grid at 20081203).
            InventoryCollection contents = invService.GetFolderContent (AgentId, folder.ID);
            // folder.RequestListOfItems();
            List<InventoryItemBase> items = contents.Items;
            while (items.Count > 0) {
                BulkUpdateInventoryPacket bulkUpdate
                    = (BulkUpdateInventoryPacket)PacketPool.Instance.GetPacket (PacketType.BulkUpdateInventory);
                bulkUpdate.Header.Zerocoded = true;

                bulkUpdate.AgentData.AgentID = AgentId;
                bulkUpdate.AgentData.TransactionID = transactionId;
                bulkUpdate.FolderData = folderDataBlocks.ToArray ();

                int itemsToSend = (items.Count > MAX_ITEMS_PER_PACKET ? MAX_ITEMS_PER_PACKET : items.Count);
                bulkUpdate.ItemData = new BulkUpdateInventoryPacket.ItemDataBlock [itemsToSend];

                for (int i = 0; i < itemsToSend; i++) {
                    // Remove from the end of the list so that we don't incur a performance penalty
                    bulkUpdate.ItemData [i] = GenerateBulkUpdateItemDataBlock (items [items.Count - 1]);
                    items.RemoveAt (items.Count - 1);
                }

                //MainConsole.Instance.Debug("SendBulkUpdateInventoryRecursive :" + bulkUpdate);
                OutPacket (bulkUpdate, ThrottleOutPacketType.Asset);

                folderDataBlocks = new List<BulkUpdateInventoryPacket.FolderDataBlock> ();

                // If we're going to be sending another items packet then it needs to contain just the folder to which those
                // items belong.
                if (items.Count > 0)
                    folderDataBlocks.Add (GenerateBulkUpdateFolderDataBlock (folder));
            }

            List<InventoryFolderBase> subFolders = contents.Folders;
            foreach (InventoryFolderBase subFolder in subFolders) {
                SendBulkUpdateInventoryFolderRecursive (subFolder, ref folderDataBlocks, transactionId);
            }
        }

        /// <summary>
        ///     Generate a bulk update inventory data block for the given folder
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        BulkUpdateInventoryPacket.FolderDataBlock GenerateBulkUpdateFolderDataBlock (InventoryFolderBase folder)
        {
            BulkUpdateInventoryPacket.FolderDataBlock folderBlock = new BulkUpdateInventoryPacket.FolderDataBlock {
                FolderID = folder.ID,
                ParentID = folder.ParentID,
                Type = -1,
                Name = Util.StringToBytes256 (folder.Name)
            };


            return folderBlock;
        }

        /// <summary>
        ///     Generate a bulk update inventory data block for the given item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        BulkUpdateInventoryPacket.ItemDataBlock GenerateBulkUpdateItemDataBlock (InventoryItemBase item)
        {
            BulkUpdateInventoryPacket.ItemDataBlock itemBlock = new BulkUpdateInventoryPacket.ItemDataBlock {
                ItemID = item.ID,
                AssetID = item.AssetID,
                CreatorID = item.CreatorIdAsUuid,
                BaseMask = item.BasePermissions,
                Description = Util.StringToBytes256 (item.Description),
                EveryoneMask = item.EveryOnePermissions,
                FolderID = item.Folder,
                InvType = (sbyte)item.InvType,
                Name = Util.StringToBytes256 (item.Name),
                NextOwnerMask = item.NextPermissions,
                OwnerID = item.Owner,
                OwnerMask = item.CurrentPermissions,
                Type = Util.CheckMeshType ((sbyte)item.AssetType),
                GroupID = item.GroupID,
                GroupOwned = item.GroupOwned,
                GroupMask = item.GroupPermissions,
                Flags = item.Flags,
                SalePrice = item.SalePrice,
                SaleType = item.SaleType,
                CreationDate = item.CreationDate
            };


            itemBlock.CRC =
                Helpers.InventoryCRC (
                    1000, 0, itemBlock.InvType,
                    itemBlock.Type, itemBlock.AssetID,
                    itemBlock.GroupID, 100,
                    itemBlock.OwnerID, itemBlock.CreatorID,
                    itemBlock.ItemID, itemBlock.FolderID,
                    (uint)PermissionMask.All, 1, (uint)PermissionMask.All, (uint)PermissionMask.All,
                    (uint)PermissionMask.All);

            return itemBlock;
        }

        public void SendBulkUpdateInventory (InventoryItemBase item)
        {
            const uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;

            BulkUpdateInventoryPacket bulkUpdate
                = (BulkUpdateInventoryPacket)PacketPool.Instance.GetPacket (PacketType.BulkUpdateInventory);

            bulkUpdate.AgentData.AgentID = AgentId;
            bulkUpdate.AgentData.TransactionID = UUID.Random ();

            bulkUpdate.FolderData = new BulkUpdateInventoryPacket.FolderDataBlock [1];
            bulkUpdate.FolderData [0] = new BulkUpdateInventoryPacket.FolderDataBlock { 
                FolderID = UUID.Zero, ParentID = UUID.Zero, Type = -1, Name = new byte [0] };

            bulkUpdate.ItemData = new BulkUpdateInventoryPacket.ItemDataBlock [1];
            bulkUpdate.ItemData [0] = new BulkUpdateInventoryPacket.ItemDataBlock {
                ItemID = item.ID,
                AssetID = item.AssetID,
                CreatorID = item.CreatorIdAsUuid,
                BaseMask = item.BasePermissions,
                CreationDate = item.CreationDate,
                Description = Util.StringToBytes256 (item.Description),
                EveryoneMask = item.EveryOnePermissions,
                FolderID = item.Folder,
                InvType = (sbyte)item.InvType,
                Name = Util.StringToBytes256 (item.Name),
                NextOwnerMask = item.NextPermissions,
                OwnerID = item.Owner,
                OwnerMask = item.CurrentPermissions,
                Type = Util.CheckMeshType ((sbyte)item.AssetType),
                GroupID = item.GroupID,
                GroupOwned = item.GroupOwned,
                GroupMask = item.GroupPermissions,
                Flags = item.Flags,
                SalePrice = item.SalePrice,
                SaleType = item.SaleType
            };


            bulkUpdate.ItemData [0].CRC =
                Helpers.InventoryCRC (1000, 0, bulkUpdate.ItemData [0].InvType,
                                     bulkUpdate.ItemData [0].Type, bulkUpdate.ItemData [0].AssetID,
                                     bulkUpdate.ItemData [0].GroupID, 100,
                                     bulkUpdate.ItemData [0].OwnerID, bulkUpdate.ItemData [0].CreatorID,
                                     bulkUpdate.ItemData [0].ItemID, bulkUpdate.ItemData [0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);
            bulkUpdate.Header.Zerocoded = true;
            OutPacket (bulkUpdate, ThrottleOutPacketType.Asset);
        }

        /// <see>IClientAPI.SendInventoryItemCreateUpdate(InventoryItemBase)</see>
        public void SendInventoryItemCreateUpdate (InventoryItemBase Item, uint callbackId)
        {
            const uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;

            UpdateCreateInventoryItemPacket InventoryReply
                = (UpdateCreateInventoryItemPacket)PacketPool.Instance.GetPacket (
                    PacketType.UpdateCreateInventoryItem);

            // TODO: don't create new blocks if recycling an old packet
            InventoryReply.AgentData.AgentID = AgentId;
            InventoryReply.AgentData.SimApproved = true;
            InventoryReply.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock [1];
            InventoryReply.InventoryData [0] = new UpdateCreateInventoryItemPacket.InventoryDataBlock {
                ItemID = Item.ID,
                AssetID = Item.AssetID,
                CreatorID = Item.CreatorIdAsUuid,
                BaseMask = Item.BasePermissions,
                Description = Util.StringToBytes256 (Item.Description),
                EveryoneMask = Item.EveryOnePermissions,
                FolderID = Item.Folder,
                InvType = (sbyte)Item.InvType,
                Name = Util.StringToBytes256 (Item.Name),
                NextOwnerMask = Item.NextPermissions,
                OwnerID = Item.Owner,
                OwnerMask = Item.CurrentPermissions,
                Type = Util.CheckMeshType ((sbyte)Item.AssetType),
                CallbackID = callbackId,
                GroupID = Item.GroupID,
                GroupOwned = Item.GroupOwned,
                GroupMask = Item.GroupPermissions,
                Flags = Item.Flags,
                SalePrice = Item.SalePrice,
                SaleType = Item.SaleType,
                CreationDate = Item.CreationDate
            };


            InventoryReply.InventoryData [0].CRC =
                Helpers.InventoryCRC (1000, 0, InventoryReply.InventoryData [0].InvType,
                                     InventoryReply.InventoryData [0].Type, InventoryReply.InventoryData [0].AssetID,
                                     InventoryReply.InventoryData [0].GroupID, 100,
                                     InventoryReply.InventoryData [0].OwnerID, InventoryReply.InventoryData [0].CreatorID,
                                     InventoryReply.InventoryData [0].ItemID, InventoryReply.InventoryData [0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);
            InventoryReply.Header.Zerocoded = true;
            OutPacket (InventoryReply, ThrottleOutPacketType.Asset);
        }

        public void SendRemoveInventoryItem (UUID itemID)
        {
            RemoveInventoryItemPacket remove =
                (RemoveInventoryItemPacket)PacketPool.Instance.GetPacket (PacketType.RemoveInventoryItem);
            // TODO: don't create new blocks if recycling an old packet
            remove.AgentData.AgentID = AgentId;
            remove.AgentData.SessionID = m_sessionId;
            remove.InventoryData = new RemoveInventoryItemPacket.InventoryDataBlock [1];
            remove.InventoryData [0] = new RemoveInventoryItemPacket.InventoryDataBlock { ItemID = itemID };
            remove.Header.Zerocoded = true;
            OutPacket (remove, ThrottleOutPacketType.Asset);
        }

        public void SendTakeControls (int controls, bool passToAgent, bool TakeControls)
        {
            ScriptControlChangePacket scriptcontrol =
                (ScriptControlChangePacket)PacketPool.Instance.GetPacket (PacketType.ScriptControlChange);
            ScriptControlChangePacket.DataBlock [] data = new ScriptControlChangePacket.DataBlock [1];
            ScriptControlChangePacket.DataBlock ddata = new ScriptControlChangePacket.DataBlock {
                Controls = (uint)controls,
                PassToAgent = passToAgent,
                TakeControls = TakeControls
            };
            data [0] = ddata;
            scriptcontrol.Data = data;
            OutPacket (scriptcontrol, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendTaskInventory (UUID taskID, short serial, byte [] fileName)
        {
            ReplyTaskInventoryPacket replytask =
                (ReplyTaskInventoryPacket)PacketPool.Instance.GetPacket (PacketType.ReplyTaskInventory);
            replytask.InventoryData.TaskID = taskID;
            replytask.InventoryData.Serial = serial;
            replytask.InventoryData.Filename = fileName;
            OutPacket (replytask, ThrottleOutPacketType.Transfer);
        }

        public void SendXferPacket (ulong xferID, uint packet, byte [] data)
        {
            SendXferPacketPacket sendXfer =
                (SendXferPacketPacket)PacketPool.Instance.GetPacket (PacketType.SendXferPacket);
            sendXfer.XferID.ID = xferID;
            sendXfer.XferID.Packet = packet;
            sendXfer.DataPacket.Data = data;
            OutPacket (sendXfer, ThrottleOutPacketType.Transfer);
        }

        public void SendEconomyData (float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit,
                                    int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent,
                                    float PriceObjectScaleFactor,
                                    int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent,
                                    int PricePublicObjectDecay,
                                    int PricePublicObjectDelete, int PriceRentLight, int PriceUpload,
                                    int TeleportMinPrice, float TeleportPriceExponent)
        {
            EconomyDataPacket economyData = (EconomyDataPacket)PacketPool.Instance.GetPacket (PacketType.EconomyData);
            economyData.Info.EnergyEfficiency = EnergyEfficiency;
            economyData.Info.ObjectCapacity = ObjectCapacity;
            economyData.Info.ObjectCount = ObjectCount;
            economyData.Info.PriceEnergyUnit = PriceEnergyUnit;
            economyData.Info.PriceGroupCreate = PriceGroupCreate;
            economyData.Info.PriceObjectClaim = PriceObjectClaim;
            economyData.Info.PriceObjectRent = PriceObjectRent;
            economyData.Info.PriceObjectScaleFactor = PriceObjectScaleFactor;
            economyData.Info.PriceParcelClaim = PriceParcelClaim;
            economyData.Info.PriceParcelClaimFactor = PriceParcelClaimFactor;
            economyData.Info.PriceParcelRent = PriceParcelRent;
            economyData.Info.PricePublicObjectDecay = PricePublicObjectDecay;
            economyData.Info.PricePublicObjectDelete = PricePublicObjectDelete;
            economyData.Info.PriceRentLight = PriceRentLight;
            economyData.Info.PriceUpload = PriceUpload;
            economyData.Info.TeleportMinPrice = TeleportMinPrice;
            economyData.Info.TeleportPriceExponent = TeleportPriceExponent;
            economyData.Header.Reliable = true;
            OutPacket (economyData, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAvatarPickerReply (AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data)
        {
            //construct the AvatarPickerReply packet.
            AvatarPickerReplyPacket replyPacket = new AvatarPickerReplyPacket {
                AgentData = { AgentID = AgentData.AgentID, QueryID = AgentData.QueryID },
                Data = Data.Select (arg => new AvatarPickerReplyPacket.DataBlock {
                    AvatarID = arg.AvatarID,
                    FirstName = arg.FirstName,
                    LastName = arg.LastName
                }).ToArray ()
            };

            //int i = 0;
            OutPacket (replyPacket, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAgentDataUpdate (UUID agentid, UUID activegroupid, string name,
                                         ulong grouppowers, string groupname, string grouptitle)
        {
            if (agentid == AgentId) {
                m_activeGroupID = activegroupid;
                m_activeGroupName = groupname;
                m_activeGroupPowers = grouppowers;
            }

            AgentDataUpdatePacket sendAgentDataUpdate =
                (AgentDataUpdatePacket)PacketPool.Instance.GetPacket (PacketType.AgentDataUpdate);
            sendAgentDataUpdate.AgentData.ActiveGroupID = activegroupid;
            sendAgentDataUpdate.AgentData.AgentID = agentid;
            string [] spl = name.Split (' ');
            string first = spl [0], last = (spl.Length == 1 ? "" : Util.CombineParams (spl, 1));
            sendAgentDataUpdate.AgentData.FirstName = Util.StringToBytes256 (first);
            sendAgentDataUpdate.AgentData.GroupName = Util.StringToBytes256 (groupname);
            sendAgentDataUpdate.AgentData.GroupPowers = grouppowers;
            sendAgentDataUpdate.AgentData.GroupTitle = Util.StringToBytes256 (grouptitle);
            sendAgentDataUpdate.AgentData.LastName = Util.StringToBytes256 (last);
            OutPacket (sendAgentDataUpdate, ThrottleOutPacketType.AvatarInfo);
        }


        /// <summary>
        /// A convenience function for sending simple alert messages to the client.
        /// </summary>
        /// <param name="message">Message</param>
        public void SendAlertMessage (string message)
        {
            SendAlertMessage (message, string.Empty, new OSD ());
        }


        /// <summary>
        ///     Send an alert message to the client.  
        ///     This pops up a brief duration information box in the bottom right hand corner.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="infoMessage"></param>
        /// <param name="extraParams"></param>
        public void SendAlertMessage (string message, string infoMessage, OSD extraParams)
        {
            AlertMessagePacket alertPack = (AlertMessagePacket)PacketPool.Instance.GetPacket (PacketType.AlertMessage);
            alertPack.AlertData = new AlertMessagePacket.AlertDataBlock();
            alertPack.AlertData.Message = Util.StringToBytes256 (message);          

            // Info message
            alertPack.AlertInfo = new AlertMessagePacket.AlertInfoBlock [1];        
            alertPack.AlertInfo [0] = new AlertMessagePacket.AlertInfoBlock ();
            alertPack.AlertInfo [0].Message = Util.StringToBytes256 (infoMessage);
            alertPack.AlertInfo [0].ExtraParams = OSDParser.SerializeLLSDXmlBytes (extraParams);

            // 20190301 -greythane-  we now have an agentInfoblock in the OMV libary due to changes to the specifications
            alertPack.AgentInfo = new AlertMessagePacket.AgentInfoBlock [0];         

            OutPacket (alertPack, ThrottleOutPacketType.AvatarInfo);
        }

        /// <summary>
        ///     Send an agent alert message to the client.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="modal">
        ///     On the linden client, if this true then it displays a one button text box placed in the
        ///     middle of the window.  If false, the message is displayed in a brief duration blue information box (as for
        ///     the AlertMessage packet).
        /// </param>
        public void SendAgentAlertMessage (string message, bool modal)
        {
            AgentAlertMessagePacket alertPack =
                (AgentAlertMessagePacket)PacketPool.Instance.GetPacket (PacketType.AgentAlertMessage);
            alertPack.AgentData.AgentID = AgentId;
            alertPack.AlertData.Message = Util.StringToBytes256 (message);
            alertPack.AlertData.Modal = modal;
            OutPacket (alertPack, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendLoadURL (string objectname, UUID objectID, UUID ownerID, bool groupOwned,
                                 string message, string url)
        {
            LoadURLPacket loadURL = (LoadURLPacket)PacketPool.Instance.GetPacket (PacketType.LoadURL);
            loadURL.Data.ObjectName = Util.StringToBytes256 (objectname);
            loadURL.Data.ObjectID = objectID;
            loadURL.Data.OwnerID = ownerID;
            loadURL.Data.OwnerIsGroup = groupOwned;
            loadURL.Data.Message = Util.StringToBytes256 (message);
            loadURL.Data.URL = Util.StringToBytes256 (url);
            OutPacket (loadURL, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDialog (string objectname, UUID objectID, UUID ownerID, string ownerFirstName,
                                string ownerLastName, string msg, UUID textureID, int ch, string [] buttonlabels)
        {
            ScriptDialogPacket dialog = (ScriptDialogPacket)PacketPool.Instance.GetPacket (PacketType.ScriptDialog);
            dialog.Data.ObjectID = objectID;
            dialog.Data.ObjectName = Util.StringToBytes256 (objectname);
            // this is the username of the *owner*
            dialog.Data.FirstName = Util.StringToBytes256 (ownerFirstName);
            dialog.Data.LastName = Util.StringToBytes256 (ownerLastName);
            dialog.Data.Message = Util.StringToBytes1024 (msg);
            dialog.Data.ImageID = textureID;
            dialog.Data.ChatChannel = ch;
            ScriptDialogPacket.ButtonsBlock [] buttons = new ScriptDialogPacket.ButtonsBlock [buttonlabels.Length];
            for (int i = 0; i < buttonlabels.Length; i++) {
                buttons [i] = new ScriptDialogPacket.ButtonsBlock { ButtonLabel = Util.StringToBytes256 (buttonlabels [i]) };
            }
            dialog.Buttons = buttons;
            dialog.OwnerData = new ScriptDialogPacket.OwnerDataBlock [1];
            dialog.OwnerData [0] = new ScriptDialogPacket.OwnerDataBlock { OwnerID = ownerID };
            OutPacket (dialog, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendPreLoadSound (UUID objectID, UUID ownerID, UUID soundID)
        {
            PreloadSoundPacket preSound = (PreloadSoundPacket)PacketPool.Instance.GetPacket (PacketType.PreloadSound);
            // TODO: don't create new blocks if recycling an old packet
            preSound.DataBlock = new PreloadSoundPacket.DataBlockBlock [1];
            preSound.DataBlock [0] = new PreloadSoundPacket.DataBlockBlock { ObjectID = objectID, OwnerID = ownerID, SoundID = soundID };
            preSound.Header.Zerocoded = true;
            OutPacket (preSound, ThrottleOutPacketType.Asset);
        }

        public void SendPlayAttachedSound (UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags)
        {
            AttachedSoundPacket sound = (AttachedSoundPacket)PacketPool.Instance.GetPacket (PacketType.AttachedSound);
            sound.DataBlock.SoundID = soundID;
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.OwnerID = ownerID;
            sound.DataBlock.Gain = gain;
            sound.DataBlock.Flags = flags;

            OutPacket (sound, ThrottleOutPacketType.Asset);
        }

        public void SendTriggeredSound (UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle,
                                       Vector3 position, float gain)
        {
            SoundTriggerPacket sound = (SoundTriggerPacket)PacketPool.Instance.GetPacket (PacketType.SoundTrigger);
            sound.SoundData.SoundID = soundID;
            sound.SoundData.OwnerID = ownerID;
            sound.SoundData.ObjectID = objectID;
            sound.SoundData.ParentID = parentID;
            sound.SoundData.Handle = handle;
            sound.SoundData.Position = position;
            sound.SoundData.Gain = gain;

            OutPacket (sound, ThrottleOutPacketType.Asset);
        }

        public void SendAttachedSoundGainChange (UUID objectID, float gain)
        {
            AttachedSoundGainChangePacket sound =
                (AttachedSoundGainChangePacket)PacketPool.Instance.GetPacket (PacketType.AttachedSoundGainChange);
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.Gain = gain;

            OutPacket (sound, ThrottleOutPacketType.Asset);
        }

        public void SendSunPos (Vector3 Position, Vector3 Velocity, ulong currentTime, uint secondsPerSunCycle,
                               uint secondsPerYear, float orbitalPosition)
        {
            // Viewers based on the Linden viewer code, do wacky things for orbital positions from Midnight to Sunrise
            // So adjust for that
            // Contributed by: Godfrey

            if (orbitalPosition > m_sunPainDaHalfOrbitalCutoff) // things get weird from midnight to sunrise
            {
                orbitalPosition = (orbitalPosition - m_sunPainDaHalfOrbitalCutoff) * 0.6666666667f +
                                  m_sunPainDaHalfOrbitalCutoff;
            }

            SimulatorViewerTimeMessagePacket viewertime =
                (SimulatorViewerTimeMessagePacket)PacketPool.Instance.GetPacket (PacketType.SimulatorViewerTimeMessage);
            viewertime.TimeInfo.SunDirection = Position;
            viewertime.TimeInfo.SunAngVelocity = Velocity;

            // Sun module used to add 6 hours to adjust for linden sun hour, adding here
            // to prevent existing code from breaking if it assumed that 6 hours were included.
            // 21600 == 6 hours * 60 minutes * 60 Seconds
            viewertime.TimeInfo.UsecSinceStart = currentTime + 21600;

            viewertime.TimeInfo.SecPerDay = secondsPerSunCycle;
            viewertime.TimeInfo.SecPerYear = secondsPerYear;
            viewertime.TimeInfo.SunPhase = orbitalPosition;
            viewertime.Header.Reliable = false;
            viewertime.Header.Zerocoded = true;
            OutPacket (viewertime, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendViewerEffect (ViewerEffectPacket.EffectBlock [] effectBlocks)
        {
            ViewerEffectPacket packet = (ViewerEffectPacket)PacketPool.Instance.GetPacket (PacketType.ViewerEffect);
            packet.Header.Reliable = false;
            packet.Header.Zerocoded = true;

            packet.AgentData.AgentID = AgentId;
            packet.AgentData.SessionID = SessionId;

            packet.Effect = effectBlocks;

            OutPacket (packet, ThrottleOutPacketType.State);
        }

        public void SendAvatarProperties (UUID avatarID, string aboutText, string bornOn, byte [] charterMember,
                                         string flAbout, uint flags, UUID flImageID, UUID imageID, string profileURL,
                                         UUID partnerID)
        {
            AvatarPropertiesReplyPacket avatarReply =
                (AvatarPropertiesReplyPacket)PacketPool.Instance.GetPacket (PacketType.AvatarPropertiesReply);
            avatarReply.AgentData.AgentID = AgentId;
            avatarReply.AgentData.AvatarID = avatarID;
            avatarReply.PropertiesData.AboutText = aboutText != null
                                                       ? Util.StringToBytes1024 (aboutText)
                                                       : Utils.EmptyBytes;
            avatarReply.PropertiesData.BornOn = Util.StringToBytes256 (bornOn);
            avatarReply.PropertiesData.CharterMember = charterMember;
            avatarReply.PropertiesData.FLAboutText = flAbout != null ? Util.StringToBytes256 (flAbout) : Utils.EmptyBytes;
            avatarReply.PropertiesData.Flags = flags;
            avatarReply.PropertiesData.FLImageID = flImageID;
            avatarReply.PropertiesData.ImageID = imageID;
            avatarReply.PropertiesData.ProfileURL = Util.StringToBytes256 (profileURL);
            avatarReply.PropertiesData.PartnerID = partnerID;
            OutPacket (avatarReply, ThrottleOutPacketType.AvatarInfo);
        }

        /// <summary>
        ///     Send the client an Estate message blue box pop-down with a single OK button
        /// </summary>
        /// <param name="FromAvatarID"></param>
        /// <param name="FromAvatarName"></param>
        /// <param name="Message"></param>
        public void SendBlueBoxMessage (UUID FromAvatarID, string FromAvatarName, string Message)
        {
            if (!ChildAgentStatus ())
                SendInstantMessage (new GridInstantMessage () {
                    FromAgentID = FromAvatarID,
                    FromAgentName = FromAvatarName,
                    ToAgentID = AgentId,
                    Dialog = (byte)InstantMessageDialog.MessageBox,
                    Message = Message,
                    Offline = 0,
                    Position = new Vector3 (),
                    RegionID = Scene.RegionInfo.RegionID
                });

            //SendInstantMessage(FromAvatarID, fromSessionID, Message, AgentId, SessionId, FromAvatarName, (byte)21,(uint) Util.UnixTimeSinceEpoch());
        }

        public void SendLogoutPacket ()
        {
            // I know this is a bit of a hack, however there are times when you don't
            // want to send this, but still need to do the rest of the shutdown process
            // this method gets called from the packet server..   which makes it practically
            // impossible to do any other way.

            if (m_SendLogoutPacketWhenClosing) {
                LogoutReplyPacket logReply = (LogoutReplyPacket)PacketPool.Instance.GetPacket (PacketType.LogoutReply);
                // TODO: don't create new blocks if recycling an old packet
                logReply.AgentData.AgentID = AgentId;
                logReply.AgentData.SessionID = SessionId;
                logReply.InventoryData = new LogoutReplyPacket.InventoryDataBlock [1];
                logReply.InventoryData [0] = new LogoutReplyPacket.InventoryDataBlock { ItemID = UUID.Zero };

                OutPacket (logReply, ThrottleOutPacketType.OutBand);
            }
        }

        public void SendHealth (float health)
        {
            HealthMessagePacket healthpacket =
                (HealthMessagePacket)PacketPool.Instance.GetPacket (PacketType.HealthMessage);
            healthpacket.HealthData.Health = health;
            OutPacket (healthpacket, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAgentOnline (UUID [] agentIDs)
        {
            OnlineNotificationPacket onp = new OnlineNotificationPacket ();
            OnlineNotificationPacket.AgentBlockBlock [] onpb =
                new OnlineNotificationPacket.AgentBlockBlock [agentIDs.Length];
            for (int i = 0; i < agentIDs.Length; i++) {
                OnlineNotificationPacket.AgentBlockBlock onpbl = new OnlineNotificationPacket.AgentBlockBlock { AgentID = agentIDs [i] };
                onpb [i] = onpbl;
            }
            onp.AgentBlock = onpb;
            onp.Header.Reliable = true;
            OutPacket (onp, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAgentOffline (UUID [] agentIDs)
        {
            OfflineNotificationPacket offp = new OfflineNotificationPacket ();
            OfflineNotificationPacket.AgentBlockBlock [] offpb =
                new OfflineNotificationPacket.AgentBlockBlock [agentIDs.Length];
            for (int i = 0; i < agentIDs.Length; i++) {
                OfflineNotificationPacket.AgentBlockBlock onpbl = new OfflineNotificationPacket.AgentBlockBlock { AgentID = agentIDs [i] };
                offpb [i] = onpbl;
            }
            offp.AgentBlock = offpb;
            offp.Header.Reliable = true;
            OutPacket (offp, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendSitResponse (UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot,
                                     Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
            AvatarSitResponsePacket avatarSitResponse = new AvatarSitResponsePacket { SitObject = { ID = TargetID } };
            if (CameraAtOffset != Vector3.Zero) {
                avatarSitResponse.SitTransform.CameraAtOffset = CameraAtOffset;
                avatarSitResponse.SitTransform.CameraEyeOffset = CameraEyeOffset;
            }
            avatarSitResponse.SitTransform.ForceMouselook = ForceMouseLook;
            avatarSitResponse.SitTransform.AutoPilot = autopilot;
            avatarSitResponse.SitTransform.SitPosition = OffsetPos;
            avatarSitResponse.SitTransform.SitRotation = SitOrientation;

            OutPacket (avatarSitResponse, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAdminResponse (UUID Token, uint AdminLevel)
        {
            GrantGodlikePowersPacket respondPacket = new GrantGodlikePowersPacket ();
            GrantGodlikePowersPacket.GrantDataBlock gdb = new GrantGodlikePowersPacket.GrantDataBlock ();
            GrantGodlikePowersPacket.AgentDataBlock adb = new GrantGodlikePowersPacket.AgentDataBlock { AgentID = AgentId, SessionID = SessionId };

            // More security
            gdb.GodLevel = (byte)AdminLevel;
            gdb.Token = Token;
            respondPacket.AgentData = adb;
            respondPacket.GrantData = gdb;
            OutPacket (respondPacket, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendGroupMembership (GroupMembershipData [] GroupMembership)
        {
            AgentGroupDataUpdatePacket Groupupdate = new AgentGroupDataUpdatePacket ();
            AgentGroupDataUpdatePacket.GroupDataBlock [] Groups =
                new AgentGroupDataUpdatePacket.GroupDataBlock [GroupMembership.Length];
            for (int i = 0; i < GroupMembership.Length; i++) {
                AgentGroupDataUpdatePacket.GroupDataBlock Group = new AgentGroupDataUpdatePacket.GroupDataBlock {
                    AcceptNotices = GroupMembership [i].AcceptNotices,
                    Contribution = GroupMembership [i].Contribution,
                    GroupID = GroupMembership [i].GroupID,
                    GroupInsigniaID = GroupMembership [i].GroupPicture,
                    GroupName = Util.StringToBytes256 (GroupMembership [i].GroupName),
                    GroupPowers = GroupMembership [i].GroupPowers
                };
                Groups [i] = Group;
            }
            Groupupdate.GroupData = Groups;
            Groupupdate.AgentData = new AgentGroupDataUpdatePacket.AgentDataBlock { AgentID = AgentId };
            OutPacket (Groupupdate, ThrottleOutPacketType.AvatarInfo);

            try {
                IEventQueueService eq = Scene.RequestModuleInterface<IEventQueueService> ();
                if (eq != null) {
                    eq.GroupMembership (Groupupdate, AgentId, Scene.RegionInfo.RegionID);
                }
            } catch (Exception ex) {
                MainConsole.Instance.Error ("Unable to send group membership data via eventqueue - exception: " + ex);
                MainConsole.Instance.Warn ("sending group membership data via UDP");
                OutPacket (Groupupdate, ThrottleOutPacketType.AvatarInfo);
            }
        }


        public void SendGroupNameReply (UUID groupLLUID, string GroupName)
        {
            var pack = new UUIDGroupNameReplyPacket ();
            UUIDGroupNameReplyPacket.UUIDNameBlockBlock [] uidnameblock = new UUIDGroupNameReplyPacket.UUIDNameBlockBlock [1];
            var uidnamebloc = new UUIDGroupNameReplyPacket.UUIDNameBlockBlock {
                ID = groupLLUID,
                GroupName = Util.StringToBytes256 (GroupName)
            };
            uidnameblock [0] = uidnamebloc;
            pack.UUIDNameBlock = uidnameblock;
            OutPacket (pack, ThrottleOutPacketType.AvatarInfo);
        }

        static readonly object _lock = new object ();
        static readonly object _landlock = new object();
        public void SendLandStatReply (uint reportType, uint requestFlags, uint resultCount, LandStatReportItem [] lsrpia)
        {
            var message = new LandStatReplyMessage {
                ReportType = reportType,
                RequestFlags = requestFlags,
                TotalObjectCount = resultCount,
                ReportDataBlocks = new LandStatReplyMessage.ReportDataBlock [lsrpia.Length]
            };

            lock (_lock) {
                for (int i = 0; i < lsrpia.Length; i++) {
                    var landItem = lsrpia [i];
                    lock (_landlock)
                    {
                        var block = new LandStatReplyMessage.ReportDataBlock();

                        block.Location = landItem.Location;
                        block.MonoScore = landItem.Score;
                        block.OwnerName = landItem.OwnerName;
                        block.Score = landItem.Score;
                        block.TaskID = landItem.TaskID;
                        block.TaskLocalID = landItem.TaskLocalID;
                        block.TaskName = landItem.TaskName;
                        block.TimeStamp = landItem.TimeModified;

                        message.ReportDataBlocks[i] = block;
                    }
                }
            }
            IEventQueueService eventService = m_scene.RequestModuleInterface<IEventQueueService> ();
            if (eventService != null)
                eventService.LandStatReply (message, AgentId, m_scene.RegionInfo.RegionID);

        }

        public void SendScriptRunningReply (UUID objectID, UUID itemID, bool running)
        {
            ScriptRunningReplyPacket scriptRunningReply = new ScriptRunningReplyPacket {
                Script = { ObjectID = objectID, ItemID = itemID, Running = running }
            };

            OutPacket (scriptRunningReply, ThrottleOutPacketType.AvatarInfo);
        }

        void SendFailedAsset (AssetRequestToClient req, TransferPacketStatus assetErrors)
        {
            TransferInfoPacket TransferPkt = new TransferInfoPacket {
                TransferInfo = {ChannelType = (int) ChannelType.Asset,
                                Status = (int) assetErrors,
                                TargetType = 0,
                                Params = req.Params,
                                Size = 0,
                                TransferID = req.TransferRequestID},
                Header = { Zerocoded = true }
            };
            OutPacket (TransferPkt, ThrottleOutPacketType.Transfer);
        }

        public void SendAsset (AssetRequestToClient req)
        {
            if (req.AssetInf.Data == null) {
                MainConsole.Instance.ErrorFormat ("[Client]: Cannot send asset {0} ({1}), asset data is null",
                                                 req.AssetInf.ID, req.AssetInf.TypeString);
                return;
            }

            //MainConsole.Instance.Debug("sending asset " + req.RequestAssetID);
            TransferInfoPacket Transfer = new TransferInfoPacket {
                TransferInfo = {ChannelType = (int) ChannelType.Asset,
                                Status = (int) TransferPacketStatus.MorePacketsToCome,
                                TargetType = 0}
            };
            if (req.AssetRequestSource == 2) {
                Transfer.TransferInfo.Params = new byte [20];
                Array.Copy (req.RequestAssetID.GetBytes (), 0, Transfer.TransferInfo.Params, 0, 16);
                int assType = req.AssetInf.Type;
                Array.Copy (Utils.IntToBytes (assType), 0, Transfer.TransferInfo.Params, 16, 4);
            } else if (req.AssetRequestSource == 3) {
                Transfer.TransferInfo.Params = req.Params;
                // Transfer.TransferInfo.Params = new byte[100];
                //Array.Copy(req.RequestUser.AgentId.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                //Array.Copy(req.RequestUser.SessionId.GetBytes(), 0, Transfer.TransferInfo.Params, 16, 16);
            }
            Transfer.TransferInfo.Size = req.AssetInf.Data.Length;
            Transfer.TransferInfo.TransferID = req.TransferRequestID;
            Transfer.Header.Zerocoded = true;
            OutPacket (Transfer, ThrottleOutPacketType.Transfer);

            if (req.NumPackets == 1) {
                TransferPacketPacket TransferPacket = new TransferPacketPacket {
                    TransferData = {Packet = 0,
                                    ChannelType = (int) ChannelType.Asset,
                                    TransferID = req.TransferRequestID,
                                    Data = req.AssetInf.Data,
                                    Status = (int) TransferPacketStatus.Done},
                    Header = { Zerocoded = true }
                };
                OutPacket (TransferPacket, ThrottleOutPacketType.Transfer);
            } else {
                int processedLength = 0;
                const int maxChunkSize = 1024;
                int packetNumber = 0;
                const int firstPacketSize = 600;

                while (processedLength < req.AssetInf.Data.Length) {
                    TransferPacketPacket TransferPacket = new TransferPacketPacket {
                        TransferData = {Packet = packetNumber,
                                        ChannelType = (int) ChannelType.Asset,
                                        TransferID = req.TransferRequestID}
                    };

                    int chunkSize = Math.Min (req.AssetInf.Data.Length - processedLength,
                                             packetNumber == 0 ? firstPacketSize : maxChunkSize);

                    byte [] chunk = new byte [chunkSize];
                    Array.Copy (req.AssetInf.Data, processedLength, chunk, 0, chunk.Length);
                    TransferPacket.TransferData.Data = chunk;

                    processedLength += chunkSize;
                    // 0 indicates more packets to come, 1 indicates last packet
                    if (req.AssetInf.Data.Length - processedLength == 0) {
                        TransferPacket.TransferData.Status = (int)TransferPacketStatus.Done;
                    } else {
                        TransferPacket.TransferData.Status = (int)TransferPacketStatus.MorePacketsToCome;
                    }
                    TransferPacket.Header.Zerocoded = true;
                    OutPacket (TransferPacket, ThrottleOutPacketType.Transfer);

                    packetNumber++;
                }
            }
        }

        public void SendRegionHandle (UUID regionID, ulong handle)
        {
            RegionIDAndHandleReplyPacket reply =
                (RegionIDAndHandleReplyPacket)PacketPool.Instance.GetPacket (PacketType.RegionIDAndHandleReply);
            reply.ReplyBlock.RegionID = regionID;
            reply.ReplyBlock.RegionHandle = handle;
            OutPacket (reply, ThrottleOutPacketType.Land);
        }

        public void SendParcelInfo (LandData land, UUID parcelID, uint x, uint y, string SimName)
        {
            ParcelInfoReplyPacket reply =
                (ParcelInfoReplyPacket)PacketPool.Instance.GetPacket (PacketType.ParcelInfoReply);
            reply.AgentData.AgentID = m_agentId;
            reply.Data.ParcelID = parcelID;
            reply.Data.OwnerID = land.OwnerID;
            reply.Data.Name = Utils.StringToBytes (land.Name);
            reply.Data.Desc = Utils.StringToBytes (land.Description);
            reply.Data.ActualArea = land.Area;
            reply.Data.BillableArea = land.Area; // TODO: what is this?

            // Bit 0: Mature, bit 7: on sale, other bits: no idea
            reply.Data.Flags = (byte)(land.Maturity > 0
                                              ? (1 << 0)
                                              : 0 + ((land.Flags & (uint)ParcelFlags.ForSale) != 0 ? (1 << 7) : 0));

            Vector3 pos = land.UserLocation;
            if (pos.Equals (Vector3.Zero)) {
                pos = (land.AABBMax + land.AABBMin) * 0.5f;
            }
            reply.Data.GlobalX = x;
            reply.Data.GlobalY = y;
            reply.Data.GlobalZ = pos.Z;
            reply.Data.SimName = Utils.StringToBytes (SimName);
            reply.Data.SnapshotID = land.SnapshotID;
            reply.Data.Dwell = land.Dwell;
            reply.Data.SalePrice = land.SalePrice;
            reply.Data.AuctionID = (int)land.AuctionID;

            OutPacket (reply, ThrottleOutPacketType.Land);
        }

        public void SendScriptTeleportRequest (string objName, string simName, Vector3 pos, Vector3 lookAt)
        {
            ScriptTeleportRequestPacket packet =
                (ScriptTeleportRequestPacket)PacketPool.Instance.GetPacket (PacketType.ScriptTeleportRequest);

            packet.Data.ObjectName = Utils.StringToBytes (objName);
            packet.Data.SimName = Utils.StringToBytes (simName);
            packet.Data.SimPosition = pos;
            packet.Data.LookAt = lookAt;

            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDirPlacesReply (UUID queryID, DirPlacesReplyData [] data)
        {
            DirPlacesReplyPacket packet =
                (DirPlacesReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirPlacesReply);

            packet.AgentData = new DirPlacesReplyPacket.AgentDataBlock ();

            packet.QueryData = new DirPlacesReplyPacket.QueryDataBlock [1];
            packet.QueryData [0] = new DirPlacesReplyPacket.QueryDataBlock ();

            packet.AgentData.AgentID = AgentId;

            packet.QueryData [0].QueryID = queryID;

            DirPlacesReplyPacket.QueryRepliesBlock [] replies =
                new DirPlacesReplyPacket.QueryRepliesBlock [0];
            DirPlacesReplyPacket.StatusDataBlock [] status =
                new DirPlacesReplyPacket.StatusDataBlock [0];

            packet.QueryReplies = replies;
            packet.StatusData = status;

            foreach (DirPlacesReplyData d in data) {
                int idx = replies.Length;
                Array.Resize (ref replies, idx + 1);
                Array.Resize (ref status, idx + 1);

                replies [idx] = new DirPlacesReplyPacket.QueryRepliesBlock ();
                status [idx] = new DirPlacesReplyPacket.StatusDataBlock ();
                replies [idx].ParcelID = d.parcelID;
                replies [idx].Name = Utils.StringToBytes (d.name);
                replies [idx].ForSale = d.forSale;
                replies [idx].Auction = d.auction;
                replies [idx].Dwell = d.dwell;
                status [idx].Status = d.Status;

                packet.QueryReplies = replies;
                packet.StatusData = status;

                if (packet.Length >= 1000) {
                    OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
                    packet = (DirPlacesReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirPlacesReply);
                    packet.AgentData = new DirPlacesReplyPacket.AgentDataBlock ();
                    packet.QueryData = new DirPlacesReplyPacket.QueryDataBlock [1];
                    packet.QueryData [0] = new DirPlacesReplyPacket.QueryDataBlock ();
                    packet.AgentData.AgentID = AgentId;
                    packet.QueryData [0].QueryID = queryID;
                    replies = new DirPlacesReplyPacket.QueryRepliesBlock [0];
                    status = new DirPlacesReplyPacket.StatusDataBlock [0];
                }
            }

            packet.HasVariableBlocks = false;
            if (replies.Length > 0 || data.Length == 0)
                OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDirPeopleReply (UUID queryID, DirPeopleReplyData [] data)
        {
            DirPeopleReplyPacket packet =
                (DirPeopleReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirPeopleReply);

            packet.AgentData = new DirPeopleReplyPacket.AgentDataBlock { AgentID = AgentId };
            packet.QueryData = new DirPeopleReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirPeopleReplyPacket.QueryRepliesBlock [data.Length];

            int i = 0;
            foreach (DirPeopleReplyData d in data) {
                packet.QueryReplies [i] = new DirPeopleReplyPacket.QueryRepliesBlock {
                    AgentID = d.agentID,
                    FirstName = Utils.StringToBytes (d.firstName),
                    LastName = Utils.StringToBytes (d.lastName),
                    Group = Utils.StringToBytes (d.group),
                    Online = d.online,
                    Reputation = d.reputation
                };
                i++;
            }

            packet.HasVariableBlocks = false;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDirEventsReply (UUID queryID, DirEventsReplyData [] data)
        {
            DirEventsReplyPacket packet =
                (DirEventsReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirEventsReply);

            packet.AgentData = new DirEventsReplyPacket.AgentDataBlock { AgentID = AgentId };
            packet.QueryData = new DirEventsReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirEventsReplyPacket.QueryRepliesBlock [data.Length];
            packet.StatusData = new DirEventsReplyPacket.StatusDataBlock [data.Length];

            int i = 0;
            foreach (DirEventsReplyData d in data) {
                packet.QueryReplies [i] = new DirEventsReplyPacket.QueryRepliesBlock ();
                packet.StatusData [i] = new DirEventsReplyPacket.StatusDataBlock ();
                packet.QueryReplies [i].OwnerID = d.ownerID;
                packet.QueryReplies [i].Name = Utils.StringToBytes (d.name);
                packet.QueryReplies [i].EventID = d.eventID;
                packet.QueryReplies [i].Date = Utils.StringToBytes (d.date);
                packet.QueryReplies [i].UnixTime = d.unixTime;
                packet.QueryReplies [i].EventFlags = d.eventFlags;
                packet.StatusData [i].Status = d.Status;
                i++;
            }

            packet.HasVariableBlocks = false;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDirGroupsReply (UUID queryID, DirGroupsReplyData [] data)
        {
            DirGroupsReplyPacket packet =
                (DirGroupsReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirGroupsReply);

            packet.AgentData = new DirGroupsReplyPacket.AgentDataBlock { AgentID = AgentId };
            packet.QueryData = new DirGroupsReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirGroupsReplyPacket.QueryRepliesBlock [data.Length];

            int i = 0;
            foreach (DirGroupsReplyData d in data) {
                packet.QueryReplies [i] = new DirGroupsReplyPacket.QueryRepliesBlock {
                    GroupID = d.groupID,
                    GroupName = Utils.StringToBytes (d.groupName),
                    Members = d.members,
                    SearchOrder = d.searchOrder
                };
                i++;
            }

            packet.HasVariableBlocks = false;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDirClassifiedReply (UUID queryID, DirClassifiedReplyData [] data)
        {
            DirClassifiedReplyPacket packet =
                (DirClassifiedReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirClassifiedReply);

            packet.AgentData = new DirClassifiedReplyPacket.AgentDataBlock { AgentID = AgentId };
            packet.QueryData = new DirClassifiedReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirClassifiedReplyPacket.QueryRepliesBlock [data.Length];
            packet.StatusData = new DirClassifiedReplyPacket.StatusDataBlock [data.Length];

            int i = 0;
            foreach (DirClassifiedReplyData d in data) {
                packet.QueryReplies [i] = new DirClassifiedReplyPacket.QueryRepliesBlock ();
                packet.StatusData [i] = new DirClassifiedReplyPacket.StatusDataBlock ();
                packet.QueryReplies [i].ClassifiedID = d.classifiedID;
                packet.QueryReplies [i].Name = Utils.StringToBytes (d.name);
                packet.QueryReplies [i].ClassifiedFlags = d.classifiedFlags;
                packet.QueryReplies [i].CreationDate = d.creationDate;
                packet.QueryReplies [i].ExpirationDate = d.expirationDate;
                packet.QueryReplies [i].PriceForListing = d.price;
                packet.StatusData [i].Status = d.Status;
                i++;
            }

            packet.HasVariableBlocks = false;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDirLandReply (UUID queryID, DirLandReplyData [] data)
        {
            DirLandReplyPacket packet = (DirLandReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirLandReply);

            packet.AgentData = new DirLandReplyPacket.AgentDataBlock { AgentID = AgentId };
            packet.QueryData = new DirLandReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirLandReplyPacket.QueryRepliesBlock [data.Length];

            int i = 0;
            foreach (DirLandReplyData d in data) {
                packet.QueryReplies [i] = new DirLandReplyPacket.QueryRepliesBlock {
                    ParcelID = d.parcelID,
                    Name = Utils.StringToBytes (d.name),
                    Auction = d.auction,
                    ForSale = d.forSale,
                    SalePrice = d.salePrice,
                    ActualArea = d.actualArea
                };
                i++;
            }

            packet.HasVariableBlocks = false;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDirPopularReply (UUID queryID, DirPopularReplyData [] data)
        {
            DirPopularReplyPacket packet =
                (DirPopularReplyPacket)PacketPool.Instance.GetPacket (PacketType.DirPopularReply);

            packet.AgentData = new DirPopularReplyPacket.AgentDataBlock { AgentID = AgentId };
            packet.QueryData = new DirPopularReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirPopularReplyPacket.QueryRepliesBlock [data.Length];

            int i = 0;
            foreach (DirPopularReplyData d in data) {
                packet.QueryReplies [i] = new DirPopularReplyPacket.QueryRepliesBlock {
                    ParcelID = d.ParcelID,
                    Name = Utils.StringToBytes (d.Name),
                    Dwell = d.Dwell
                };
                i++;
            }

            packet.HasVariableBlocks = false;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendEventInfoReply (EventData data)
        {
            EventInfoReplyPacket packet =
                (EventInfoReplyPacket)PacketPool.Instance.GetPacket (PacketType.EventInfoReply);

            packet.AgentData = new EventInfoReplyPacket.AgentDataBlock { AgentID = AgentId };

            packet.EventData = new EventInfoReplyPacket.EventDataBlock {
                EventID = data.eventID,
                Creator = Utils.StringToBytes (data.creator),
                Name = Utils.StringToBytes (data.name),
                Category = Utils.StringToBytes (data.category),
                Desc = Utils.StringToBytes (data.description),
                Date = Utils.StringToBytes (data.date),
                DateUTC = data.dateUTC,
                Duration = data.duration,
                Cover = data.cover,
                Amount = data.amount,
                SimName = Utils.StringToBytes (data.simName),
                GlobalPos = new Vector3d (data.globalPos),
                EventFlags = data.eventFlags
            };

            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendMapItemReply (mapItemReply [] replies, uint mapitemtype, uint flags)
        {
            MapItemReplyPacket mirplk = new MapItemReplyPacket {
                AgentData = { AgentID = AgentId },
                RequestData = { ItemType = mapitemtype },
                Data = new MapItemReplyPacket.DataBlock [replies.Length]
            };
            for (int i = 0; i < replies.Length; i++) {
                MapItemReplyPacket.DataBlock mrdata = new MapItemReplyPacket.DataBlock {
                    X = replies [i].x,
                    Y = replies [i].y,
                    ID = replies [i].id,
                    Extra = replies [i].Extra,
                    Extra2 = replies [i].Extra2,
                    Name = Utils.StringToBytes (replies [i].name)
                };
                mirplk.Data [i] = mrdata;
            }
            //MainConsole.Instance.Debug(mirplk.ToString());
            OutPacket (mirplk, ThrottleOutPacketType.Land);
        }

        public void SendOfferCallingCard (UUID srcID, UUID transactionID)
        {
            // a bit special, as this uses AgentID to store the source instead
            // of the destination. The destination (the receiver) goes into destID
            OfferCallingCardPacket p =
                (OfferCallingCardPacket)PacketPool.Instance.GetPacket (PacketType.OfferCallingCard);
            p.AgentData.AgentID = srcID;
            p.AgentData.SessionID = UUID.Zero;
            p.AgentBlock.DestID = AgentId;
            p.AgentBlock.TransactionID = transactionID;
            OutPacket (p, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAcceptCallingCard (UUID transactionID)
        {
            AcceptCallingCardPacket p =
                (AcceptCallingCardPacket)PacketPool.Instance.GetPacket (PacketType.AcceptCallingCard);
            p.AgentData.AgentID = AgentId;
            p.AgentData.SessionID = UUID.Zero;
            p.FolderData = new AcceptCallingCardPacket.FolderDataBlock [1];
            p.FolderData [0] = new AcceptCallingCardPacket.FolderDataBlock { FolderID = UUID.Zero };
            OutPacket (p, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDeclineCallingCard (UUID transactionID)
        {
            DeclineCallingCardPacket p =
                (DeclineCallingCardPacket)PacketPool.Instance.GetPacket (PacketType.DeclineCallingCard);
            p.AgentData.AgentID = AgentId;
            p.AgentData.SessionID = UUID.Zero;
            p.TransactionBlock.TransactionID = transactionID;
            OutPacket (p, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendTerminateFriend (UUID exFriendID)
        {
            TerminateFriendshipPacket p =
                (TerminateFriendshipPacket)PacketPool.Instance.GetPacket (PacketType.TerminateFriendship);
            p.AgentData.AgentID = AgentId;
            p.AgentData.SessionID = SessionId;
            p.ExBlock.OtherID = exFriendID;
            OutPacket (p, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAvatarGroupsReply (UUID avatarID, GroupMembershipData [] data)
        {
            OSDMap llsd = new OSDMap (3);
            OSDArray AgentData = new OSDArray (1);
            OSDMap AgentDataMap = new OSDMap (1)
                                      {{"AgentID", OSD.FromUUID(AgentId)}, {"AvatarID", OSD.FromUUID(avatarID)}};
            AgentData.Add (AgentDataMap);
            llsd.Add ("AgentData", AgentData);
            OSDArray GroupData = new OSDArray (data.Length);
            OSDArray NewGroupData = new OSDArray (data.Length);
            foreach (GroupMembershipData m in data) {
                OSDMap GroupDataMap = new OSDMap (6);
                OSDMap NewGroupDataMap = new OSDMap (1);
                GroupDataMap.Add ("GroupPowers", OSD.FromULong (m.GroupPowers));
                GroupDataMap.Add ("AcceptNotices", OSD.FromBoolean (m.AcceptNotices));
                GroupDataMap.Add ("GroupTitle", OSD.FromString (m.GroupTitle));
                GroupDataMap.Add ("GroupID", OSD.FromUUID (m.GroupID));
                GroupDataMap.Add ("GroupName", OSD.FromString (m.GroupName));
                GroupDataMap.Add ("GroupInsigniaID", OSD.FromUUID (m.GroupPicture));
                NewGroupDataMap.Add ("ListInProfile", OSD.FromBoolean (m.ListInProfile));
                GroupData.Add (GroupDataMap);
                NewGroupData.Add (NewGroupDataMap);
            }
            llsd.Add ("GroupData", GroupData);
            llsd.Add ("NewGroupData", NewGroupData);

            IEventQueueService eq = Scene.RequestModuleInterface<IEventQueueService> ();
            if (eq != null) {
                eq.Enqueue (BuildEvent ("AvatarGroupsReply", llsd), AgentId, Scene.RegionInfo.RegionID);
            }
        }

        public void SendJoinGroupReply (UUID groupID, bool success)
        {
            JoinGroupReplyPacket p = (JoinGroupReplyPacket)PacketPool.Instance.GetPacket (PacketType.JoinGroupReply);

            p.AgentData = new JoinGroupReplyPacket.AgentDataBlock { AgentID = AgentId };
            p.GroupData = new JoinGroupReplyPacket.GroupDataBlock { GroupID = groupID, Success = success };

            OutPacket (p, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendEjectGroupMemberReply (UUID agentID, UUID groupID, bool success)
        {
            EjectGroupMemberReplyPacket p =
                (EjectGroupMemberReplyPacket)PacketPool.Instance.GetPacket (PacketType.EjectGroupMemberReply);

            p.AgentData = new EjectGroupMemberReplyPacket.AgentDataBlock { AgentID = agentID };
            p.GroupData = new EjectGroupMemberReplyPacket.GroupDataBlock { GroupID = groupID };
            p.EjectData = new EjectGroupMemberReplyPacket.EjectDataBlock { Success = success };

            OutPacket (p, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendLeaveGroupReply (UUID groupID, bool success)
        {
            LeaveGroupReplyPacket p = (LeaveGroupReplyPacket)PacketPool.Instance.GetPacket (PacketType.LeaveGroupReply);

            p.AgentData = new LeaveGroupReplyPacket.AgentDataBlock { AgentID = AgentId };
            p.GroupData = new LeaveGroupReplyPacket.GroupDataBlock { GroupID = groupID, Success = success };

            OutPacket (p, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAvatarClassifiedReply (UUID targetID, UUID [] classifiedID, string [] name)
        {
            if (classifiedID.Length != name.Length)
                return;

            AvatarClassifiedReplyPacket ac =
                (AvatarClassifiedReplyPacket)PacketPool.Instance.GetPacket (PacketType.AvatarClassifiedReply);

            ac.AgentData = new AvatarClassifiedReplyPacket.AgentDataBlock { AgentID = AgentId, TargetID = targetID };

            ac.Data = new AvatarClassifiedReplyPacket.DataBlock [classifiedID.Length];
            int i;
            for (i = 0; i < classifiedID.Length; i++) {
                ac.Data [i].ClassifiedID = classifiedID [i];
                ac.Data [i].Name = Utils.StringToBytes (name [i]);
            }

            OutPacket (ac, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendClassifiedInfoReply (UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate,
                                            uint category, string name, string description, UUID parcelID,
                                            uint parentEstate, UUID snapshotID, string simName, Vector3 globalPos,
                                            string parcelName, byte classifiedFlags, int price)
        {
            ClassifiedInfoReplyPacket cr =
                (ClassifiedInfoReplyPacket)PacketPool.Instance.GetPacket (PacketType.ClassifiedInfoReply);

            cr.AgentData = new ClassifiedInfoReplyPacket.AgentDataBlock { AgentID = AgentId };

            cr.Data = new ClassifiedInfoReplyPacket.DataBlock {
                ClassifiedID = classifiedID,
                CreatorID = creatorID,
                CreationDate = creationDate,
                ExpirationDate = expirationDate,
                Category = category,
                Name = Utils.StringToBytes (name),
                Desc = Utils.StringToBytes (description),
                ParcelID = parcelID,
                ParentEstate = parentEstate,
                SnapshotID = snapshotID,
                SimName = Utils.StringToBytes (simName),
                PosGlobal = new Vector3d (globalPos),
                ParcelName = Utils.StringToBytes (parcelName),
                ClassifiedFlags = classifiedFlags,
                PriceForListing = price
            };

            OutPacket (cr, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAgentDropGroup (UUID groupID)
        {
            AgentDropGroupPacket dg =
                (AgentDropGroupPacket)PacketPool.Instance.GetPacket (
                    PacketType.AgentDropGroup);

            dg.AgentData = new AgentDropGroupPacket.AgentDataBlock { AgentID = AgentId, GroupID = groupID };

            OutPacket (dg, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAvatarNotesReply (UUID targetID, string text)
        {
            AvatarNotesReplyPacket an =
                (AvatarNotesReplyPacket)PacketPool.Instance.GetPacket (PacketType.AvatarNotesReply);

            an.AgentData = new AvatarNotesReplyPacket.AgentDataBlock { AgentID = AgentId };
            an.Data = new AvatarNotesReplyPacket.DataBlock { TargetID = targetID, Notes = Utils.StringToBytes (text) };

            OutPacket (an, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAvatarPicksReply (UUID targetID, Dictionary<UUID, string> picks)
        {
            AvatarPicksReplyPacket ap =
                (AvatarPicksReplyPacket)PacketPool.Instance.GetPacket (PacketType.AvatarPicksReply);

            ap.AgentData = new AvatarPicksReplyPacket.AgentDataBlock { AgentID = AgentId, TargetID = targetID };
            ap.Data = new AvatarPicksReplyPacket.DataBlock [picks.Count];

            int i = 0;
            foreach (KeyValuePair<UUID, string> pick in picks) {
                ap.Data [i] = new AvatarPicksReplyPacket.DataBlock { PickID = pick.Key, PickName = Utils.StringToBytes (pick.Value) };
                i++;
            }

            OutPacket (ap, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAvatarClassifiedReply (UUID targetID, Dictionary<UUID, string> classifieds)
        {
            AvatarClassifiedReplyPacket ac =
                (AvatarClassifiedReplyPacket)PacketPool.Instance.GetPacket (PacketType.AvatarClassifiedReply);

            ac.AgentData = new AvatarClassifiedReplyPacket.AgentDataBlock { AgentID = AgentId, TargetID = targetID };
            ac.Data = new AvatarClassifiedReplyPacket.DataBlock [classifieds.Count];

            int i = 0;
            foreach (KeyValuePair<UUID, string> classified in classifieds) {
                ac.Data [i] = new AvatarClassifiedReplyPacket.DataBlock { ClassifiedID = classified.Key, Name = Utils.StringToBytes (classified.Value) };
                i++;
            }

            OutPacket (ac, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendParcelDwellReply (int localID, UUID parcelID, float dwell)
        {
            ParcelDwellReplyPacket pd =
                (ParcelDwellReplyPacket)PacketPool.Instance.GetPacket (PacketType.ParcelDwellReply);

            pd.AgentData = new ParcelDwellReplyPacket.AgentDataBlock { AgentID = AgentId };
            pd.Data = new ParcelDwellReplyPacket.DataBlock { LocalID = localID, ParcelID = parcelID, Dwell = dwell };

            OutPacket (pd, ThrottleOutPacketType.Land);
        }

        public void SendUserInfoReply (bool imViaEmail, bool visible, string email)
        {
            UserInfoReplyPacket ur =
                (UserInfoReplyPacket)PacketPool.Instance.GetPacket (PacketType.UserInfoReply);

            string Visible = "hidden";
            if (visible)
                Visible = "default";

            ur.AgentData = new UserInfoReplyPacket.AgentDataBlock { AgentID = AgentId };
            ur.UserData = new UserInfoReplyPacket.UserDataBlock {
                IMViaEMail = imViaEmail,
                DirectoryVisibility = Utils.StringToBytes (Visible),
                EMail = Utils.StringToBytes (email)
            };

            OutPacket (ur, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendCreateGroupReply (UUID groupID, bool success, string message)
        {
            CreateGroupReplyPacket createGroupReply =
                (CreateGroupReplyPacket)PacketPool.Instance.GetPacket (PacketType.CreateGroupReply);

            createGroupReply.AgentData = new CreateGroupReplyPacket.AgentDataBlock ();
            createGroupReply.ReplyData = new CreateGroupReplyPacket.ReplyDataBlock ();

            createGroupReply.AgentData.AgentID = AgentId;
            createGroupReply.ReplyData.GroupID = groupID;
            createGroupReply.ReplyData.Success = success;
            createGroupReply.ReplyData.Message = Utils.StringToBytes (message);
            OutPacket (createGroupReply, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendUseCachedMuteList ()
        {
            UseCachedMuteListPacket useCachedMuteList =
                (UseCachedMuteListPacket)PacketPool.Instance.GetPacket (PacketType.UseCachedMuteList);

            useCachedMuteList.AgentData = new UseCachedMuteListPacket.AgentDataBlock { AgentID = AgentId };

            OutPacket (useCachedMuteList, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendMuteListUpdate (string filename)
        {
            MuteListUpdatePacket muteListUpdate =
                (MuteListUpdatePacket)PacketPool.Instance.GetPacket (PacketType.MuteListUpdate);

            muteListUpdate.MuteData = new MuteListUpdatePacket.MuteDataBlock { AgentID = AgentId, Filename = Utils.StringToBytes (filename) };

            OutPacket (muteListUpdate, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendPickInfoReply (UUID pickID, UUID creatorID, bool topPick, UUID parcelID, string name, string desc,
                                      UUID snapshotID, string user, string originalName, string simName,
                                      Vector3 posGlobal, int sortOrder, bool enabled)
        {
            PickInfoReplyPacket pickInfoReply =
                (PickInfoReplyPacket)PacketPool.Instance.GetPacket (PacketType.PickInfoReply);

            pickInfoReply.AgentData = new PickInfoReplyPacket.AgentDataBlock { AgentID = AgentId };

            pickInfoReply.Data = new PickInfoReplyPacket.DataBlock {
                PickID = pickID,
                CreatorID = creatorID,
                TopPick = topPick,
                ParcelID = parcelID,
                Name = Utils.StringToBytes (name),
                Desc = Utils.StringToBytes (desc),
                SnapshotID = snapshotID,
                User = Utils.StringToBytes (user),
                OriginalName = Utils.StringToBytes (originalName),
                SimName = Utils.StringToBytes (simName),
                PosGlobal = new Vector3d (posGlobal),
                SortOrder = sortOrder,
                Enabled = enabled
            };

            OutPacket (pickInfoReply, ThrottleOutPacketType.AvatarInfo);
        }

        #endregion Scene/Avatar to Client

        #region Objects/m_sceneObjects

        bool HandleObjectLink (IClientAPI sender, Packet pack)
        {
            var link = (ObjectLinkPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (link.AgentData.SessionID != SessionId ||
                    link.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            uint parentprimid = 0;
            List<uint> childrenprims = new List<uint> ();
            if (link.ObjectData.Length > 1) {
                parentprimid = link.ObjectData [0].ObjectLocalID;

                for (int i = 1; i < link.ObjectData.Length; i++) {
                    childrenprims.Add (link.ObjectData [i].ObjectLocalID);
                }
            }
            LinkObjects handlerLinkObjects = OnLinkObjects;
            if (handlerLinkObjects != null) {
                handlerLinkObjects (this, parentprimid, childrenprims);
            }
            return true;
        }

        bool HandleObjectDelink (IClientAPI sender, Packet pack)
        {
            var delink = (ObjectDelinkPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (delink.AgentData.SessionID != SessionId ||
                    delink.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            // It appears the prim at index 0 is not always the root prim (for
            // instance, when one prim of a link set has been edited independently
            // of the others).  Therefore, we'll pass all the ids onto the delink
            // method for it to decide which is the root.
            List<uint> prims = delink.ObjectData.Select (t => t.ObjectLocalID).ToList ();
            DelinkObjects handlerDelinkObjects = OnDelinkObjects;
            if (handlerDelinkObjects != null) {
                handlerDelinkObjects (prims, this);
            }
            return true;
        }

        bool HandleObjectAdd (IClientAPI sender, Packet pack)
        {
            if (OnAddPrim != null) {
                var addPacket = (ObjectAddPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (addPacket.AgentData.SessionID != SessionId ||
                        addPacket.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                PrimitiveBaseShape shape = GetShapeFromAddPacket (addPacket);
                // MainConsole.Instance.Info("[REZData]: " + addPacket.ToString());
                //BypassRaycast: 1
                //RayStart: <69.79469, 158.2652, 98.40343>
                //RayEnd: <61.97724, 141.995, 92.58341>
                //RayTargetID: 00000000-0000-0000-0000-000000000000

                //Check to see if adding the prim is allowed; useful for any module wanting to restrict the
                //object from rezing initially

                AddNewPrim handlerAddPrim = OnAddPrim;
                if (handlerAddPrim != null)
                    handlerAddPrim (AgentId,
                                    ActiveGroupId,
                                    addPacket.ObjectData.RayEnd,
                                    addPacket.ObjectData.Rotation,
                                    shape,
                                    addPacket.ObjectData.BypassRaycast,
                                    addPacket.ObjectData.RayStart,
                                    addPacket.ObjectData.RayTargetID,
                                    addPacket.ObjectData.RayEndIsIntersection);
            }
            return true;
        }

        bool HandleObjectShape (IClientAPI sender, Packet pack)
        {
            var shapePacket = (ObjectShapePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (shapePacket.AgentData.SessionID != SessionId ||
                    shapePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectShapePacket.ObjectDataBlock t in shapePacket.ObjectData) {
                UpdateShape handlerUpdatePrimShape = OnUpdatePrimShape;
                if (handlerUpdatePrimShape != null) {
                    UpdateShapeArgs shapeData = new UpdateShapeArgs {
                        ObjectLocalID = t.ObjectLocalID,
                        PathBegin = t.PathBegin,
                        PathCurve = t.PathCurve,
                        PathEnd = t.PathEnd,
                        PathRadiusOffset = t.PathRadiusOffset,
                        PathRevolutions = t.PathRevolutions,
                        PathScaleX = t.PathScaleX,
                        PathScaleY = t.PathScaleY,
                        PathShearX = t.PathShearX,
                        PathShearY = t.PathShearY,
                        PathSkew = t.PathSkew,
                        PathTaperX = t.PathTaperX,
                        PathTaperY = t.PathTaperY,
                        PathTwist = t.PathTwist,
                        PathTwistBegin = t.PathTwistBegin,
                        ProfileBegin = t.ProfileBegin,
                        ProfileCurve = t.ProfileCurve,
                        ProfileEnd = t.ProfileEnd,
                        ProfileHollow = t.ProfileHollow
                    };

                    handlerUpdatePrimShape (m_agentId, t.ObjectLocalID, shapeData);
                }
            }
            return true;
        }

        bool HandleObjectExtraParams (IClientAPI sender, Packet pack)
        {
            var extraPar = (ObjectExtraParamsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (extraPar.AgentData.SessionID != SessionId ||
                    extraPar.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ObjectExtraParams handlerUpdateExtraParams = OnUpdateExtraParams;
            if (handlerUpdateExtraParams != null) {
                foreach (ObjectExtraParamsPacket.ObjectDataBlock t in extraPar.ObjectData) {
                    handlerUpdateExtraParams (m_agentId,
                                              t.ObjectLocalID,
                                              t.ParamType,
                                              t.ParamInUse,
                                              t.ParamData);
                }
            }
            return true;
        }

        bool HandleObjectDuplicate (IClientAPI sender, Packet pack)
        {
            var dupe = (ObjectDuplicatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dupe.AgentData.SessionID != SessionId ||
                    dupe.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            //            ObjectDuplicatePacket.AgentDataBlock AgentandGroupData = dupe.AgentData;

            foreach (ObjectDuplicatePacket.ObjectDataBlock t in dupe.ObjectData) {
                ObjectDuplicate handlerObjectDuplicate = OnObjectDuplicate;
                if (handlerObjectDuplicate != null) {
                    handlerObjectDuplicate (t.ObjectLocalID,
                                            dupe.SharedData.Offset,
                                            dupe.SharedData.DuplicateFlags,
                                            AgentId,
                                            m_activeGroupID,
                                            Quaternion.Identity);
                }
            }

            return true;
        }

        bool HandleRequestMultipleObjects (IClientAPI sender, Packet pack)
        {
            var incomingRequest = (RequestMultipleObjectsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (incomingRequest.AgentData.SessionID != SessionId ||
                    incomingRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (RequestMultipleObjectsPacket.ObjectDataBlock t in incomingRequest.ObjectData) {
                ObjectRequest handlerObjectRequest = OnObjectRequest;
                if (handlerObjectRequest != null) {
                    handlerObjectRequest (t.ID, t.CacheMissType, this);
                }
            }
            return true;
        }

        bool HandleObjectSelect (IClientAPI sender, Packet Pack)
        {
            ObjectSelectPacket incomingselect = (ObjectSelectPacket)Pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (incomingselect.AgentData.SessionID != SessionId ||
                    incomingselect.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ObjectSelect handlerObjectSelect = null;

            List<uint> LocalIDs = incomingselect.ObjectData.Select (t => t.ObjectLocalID).ToList ();

            handlerObjectSelect = OnObjectSelect;
            if (handlerObjectSelect != null) {
                handlerObjectSelect (LocalIDs, this);
            }
            return true;
        }

        bool HandleObjectDeselect (IClientAPI sender, Packet pack)
        {
            var incomingdeselect = (ObjectDeselectPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (incomingdeselect.AgentData.SessionID != SessionId ||
                    incomingdeselect.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectDeselectPacket.ObjectDataBlock t in incomingdeselect.ObjectData) {
                ObjectDeselect handlerObjectDeselect = OnObjectDeselect;
                if (handlerObjectDeselect != null) {
                    OnObjectDeselect (t.ObjectLocalID, this);
                }
            }
            return true;
        }

        bool HandleObjectPosition (IClientAPI sender, Packet pack)
        {
            // DEPRECATED: but till libsecondlife removes it, people will use it
            var position = (ObjectPositionPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (position.AgentData.SessionID != SessionId ||
                    position.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectPositionPacket.ObjectDataBlock t in position.ObjectData) {
                UpdateVectorWithUpdate handlerUpdateVector = OnUpdatePrimGroupPosition;
                if (handlerUpdateVector != null)
                    handlerUpdateVector (t.ObjectLocalID, t.Position, this, true);
            }

            return true;
        }

        bool HandleObjectScale (IClientAPI sender, Packet pack)
        {
            // DEPRECATED: but till libsecondlife removes it, people will use it
            var scale = (ObjectScalePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (scale.AgentData.SessionID != SessionId ||
                    scale.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectScalePacket.ObjectDataBlock t in scale.ObjectData) {
                UpdateVector handlerUpdatePrimGroupScale = OnUpdatePrimGroupScale;
                if (handlerUpdatePrimGroupScale != null)
                    handlerUpdatePrimGroupScale (t.ObjectLocalID, t.Scale, this);
            }

            return true;
        }

        bool HandleObjectRotation (IClientAPI sender, Packet pack)
        {
            // DEPRECATED: but till libsecondlife removes it, people will use it
            var rotation = (ObjectRotationPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (rotation.AgentData.SessionID != SessionId ||
                    rotation.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectRotationPacket.ObjectDataBlock t in rotation.ObjectData) {
                UpdatePrimRotation handlerUpdatePrimRotation = OnUpdatePrimGroupRotation;
                if (handlerUpdatePrimRotation != null)
                    handlerUpdatePrimRotation (t.ObjectLocalID, t.Rotation, this);
            }

            return true;
        }

        bool HandleObjectFlagUpdate (IClientAPI sender, Packet pack)
        {
            var flags = (ObjectFlagUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (flags.AgentData.SessionID != SessionId ||
                    flags.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            UpdatePrimFlags handlerUpdatePrimFlags = OnUpdatePrimFlags;

            if (handlerUpdatePrimFlags != null) {
                handlerUpdatePrimFlags (flags.AgentData.ObjectLocalID,
                                        flags.AgentData.UsePhysics,
                                        flags.AgentData.IsTemporary,
                                        flags.AgentData.IsPhantom,
                                        flags.ExtraPhysics,
                                        this);
            }
            return true;
        }

        bool HandleObjectImage (IClientAPI sender, Packet pack)
        {
            var imagePack = (ObjectImagePacket)pack;

            foreach (ObjectImagePacket.ObjectDataBlock t in imagePack.ObjectData) {
                UpdatePrimTexture handlerUpdatePrimTexture = OnUpdatePrimTexture;
                if (handlerUpdatePrimTexture != null) {
                    handlerUpdatePrimTexture (t.ObjectLocalID, t.TextureEntry, this);
                }
            }
            return true;
        }

        bool HandleObjectGrab (IClientAPI sender, Packet pack)
        {
            var grab = (ObjectGrabPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (grab.AgentData.SessionID != SessionId ||
                    grab.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            GrabObject handlerGrabObject = OnGrabObject;

            if (handlerGrabObject != null) {
                List<SurfaceTouchEventArgs> touchArgs = new List<SurfaceTouchEventArgs> ();
                if ((grab.SurfaceInfo != null) && (grab.SurfaceInfo.Length > 0)) {
                    touchArgs.AddRange (grab.SurfaceInfo.Select (surfaceInfo => new SurfaceTouchEventArgs {
                        Binormal = surfaceInfo.Binormal,
                        FaceIndex = surfaceInfo.FaceIndex,
                        Normal = surfaceInfo.Normal,
                        Position = surfaceInfo.Position,
                        STCoord = surfaceInfo.STCoord,
                        UVCoord = surfaceInfo.UVCoord
                    }));
                }
                handlerGrabObject (grab.ObjectData.LocalID, grab.ObjectData.GrabOffset, this, touchArgs);
            }
            return true;
        }

        bool HandleObjectGrabUpdate (IClientAPI sender, Packet pack)
        {
            var grabUpdate = (ObjectGrabUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (grabUpdate.AgentData.SessionID != SessionId ||
                    grabUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            MoveObject handlerGrabUpdate = OnGrabUpdate;

            if (handlerGrabUpdate != null) {
                List<SurfaceTouchEventArgs> touchArgs = new List<SurfaceTouchEventArgs> ();
                if ((grabUpdate.SurfaceInfo != null) && (grabUpdate.SurfaceInfo.Length > 0)) {
                    touchArgs.AddRange (grabUpdate.SurfaceInfo.Select (surfaceInfo => new SurfaceTouchEventArgs {
                        Binormal = surfaceInfo.Binormal,
                        FaceIndex = surfaceInfo.FaceIndex,
                        Normal = surfaceInfo.Normal,
                        Position = surfaceInfo.Position,
                        STCoord = surfaceInfo.STCoord,
                        UVCoord = surfaceInfo.UVCoord
                    }));
                }
                handlerGrabUpdate (grabUpdate.ObjectData.ObjectID,
                                   grabUpdate.ObjectData.GrabOffsetInitial,
                                   grabUpdate.ObjectData.GrabPosition,
                                   this,
                                   touchArgs);
            }
            return true;
        }

        bool HandleObjectDeGrab (IClientAPI sender, Packet pack)
        {
            var deGrab = (ObjectDeGrabPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (deGrab.AgentData.SessionID != SessionId ||
                    deGrab.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DeGrabObject handlerDeGrabObject = OnDeGrabObject;
            if (handlerDeGrabObject != null) {
                List<SurfaceTouchEventArgs> touchArgs = new List<SurfaceTouchEventArgs> ();
                if ((deGrab.SurfaceInfo != null) && (deGrab.SurfaceInfo.Length > 0)) {
                    touchArgs.AddRange (deGrab.SurfaceInfo.Select (surfaceInfo => new SurfaceTouchEventArgs {
                        Binormal = surfaceInfo.Binormal,
                        FaceIndex = surfaceInfo.FaceIndex,
                        Normal = surfaceInfo.Normal,
                        Position = surfaceInfo.Position,
                        STCoord = surfaceInfo.STCoord,
                        UVCoord = surfaceInfo.UVCoord
                    }));
                }
                handlerDeGrabObject (deGrab.ObjectData.LocalID, this, touchArgs);
            }
            return true;
        }

        bool HandleObjectSpinStart (IClientAPI sender, Packet pack)
        {
            //MainConsole.Instance.Warn("[Client]: unhandled ObjectSpinStart packet");
            var spinStart = (ObjectSpinStartPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (spinStart.AgentData.SessionID != SessionId ||
                    spinStart.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SpinStart handlerSpinStart = OnSpinStart;
            if (handlerSpinStart != null) {
                handlerSpinStart (spinStart.ObjectData.ObjectID, this);
            }
            return true;
        }

        bool HandleObjectSpinUpdate (IClientAPI sender, Packet pack)
        {
            //MainConsole.Instance.Warn("[Client]: unhandled ObjectSpinUpdate packet");
            var spinUpdate = (ObjectSpinUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (spinUpdate.AgentData.SessionID != SessionId ||
                    spinUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            Vector3 axis;
            float angle;
            spinUpdate.ObjectData.Rotation.GetAxisAngle (out axis, out angle);
            //MainConsole.Instance.Warn("[Client]: ObjectSpinUpdate packet rot axis:" + axis + " angle:" + angle);

            SpinObject handlerSpinUpdate = OnSpinUpdate;
            if (handlerSpinUpdate != null) {
                handlerSpinUpdate (spinUpdate.ObjectData.ObjectID, spinUpdate.ObjectData.Rotation, this);
            }
            return true;
        }

        bool HandleObjectSpinStop (IClientAPI sender, Packet pack)
        {
            //MainConsole.Instance.Warn("[Client]: unhandled ObjectSpinStop packet");
            var spinStop = (ObjectSpinStopPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (spinStop.AgentData.SessionID != SessionId ||
                    spinStop.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SpinStop handlerSpinStop = OnSpinStop;
            if (handlerSpinStop != null) {
                handlerSpinStop (spinStop.ObjectData.ObjectID, this);
            }
            return true;
        }

        bool HandleObjectDescription (IClientAPI sender, Packet pack)
        {
            var objDes = (ObjectDescriptionPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (objDes.AgentData.SessionID != SessionId ||
                    objDes.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectDescriptionPacket.ObjectDataBlock t in objDes.ObjectData) {
                GenericCall7 handlerObjectDescription = OnObjectDescription;
                if (handlerObjectDescription != null) {
                    handlerObjectDescription (this, t.LocalID, Util.FieldToString (t.Description));
                }
            }
            return true;
        }

        bool HandleObjectName (IClientAPI sender, Packet pack)
        {
            var objName = (ObjectNamePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (objName.AgentData.SessionID != SessionId ||
                    objName.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectNamePacket.ObjectDataBlock t in objName.ObjectData) {
                GenericCall7 handlerObjectName = OnObjectName;
                if (handlerObjectName != null) {
                    handlerObjectName (this, t.LocalID, Util.FieldToString (t.Name));
                }
            }
            return true;
        }

        bool HandleObjectPermissions (IClientAPI sender, Packet pack)
        {
            if (OnObjectPermissions != null) {
                var newobjPerms = (ObjectPermissionsPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (newobjPerms.AgentData.SessionID != SessionId ||
                        newobjPerms.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                UUID AgentID = newobjPerms.AgentData.AgentID;
                UUID SessionID = newobjPerms.AgentData.SessionID;

                foreach (ObjectPermissionsPacket.ObjectDataBlock permChanges in newobjPerms.ObjectData) {
                    byte field = permChanges.Field;
                    uint localID = permChanges.ObjectLocalID;
                    uint mask = permChanges.Mask;
                    byte set = permChanges.Set;

                    ObjectPermissions handlerObjectPermissions = OnObjectPermissions;

                    if (handlerObjectPermissions != null)
                        handlerObjectPermissions (this, AgentID, SessionID, field, localID, mask, set);
                }
            }

            // Here's our data,
            // PermField contains the field the info goes into
            // PermField determines which mask we're changing
            //
            // chmask is the mask of the change
            // setTF is whether we're adding it or taking it away
            //
            // objLocalID is the localID of the object.

            // Unfortunately, we have to pass the event the packet because objData is an array
            // That means multiple object perms may be updated in a single packet.

            return true;
        }

        bool HandleUndo (IClientAPI sender, Packet pack)
        {
            var undoitem = (UndoPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (undoitem.AgentData.SessionID != SessionId ||
                    undoitem.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (undoitem.ObjectData.Length > 0) {
                foreach (UndoPacket.ObjectDataBlock t in undoitem.ObjectData) {
                    UUID objiD = t.ObjectID;
                    AgentSit handlerOnUndo = OnUndo;
                    if (handlerOnUndo != null) {
                        handlerOnUndo (this, objiD);
                    }
                }
            }
            return true;
        }

        bool HandleLandUndo (IClientAPI sender, Packet pack)
        {
            var undolanditem = (UndoLandPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (undolanditem.AgentData.SessionID != SessionId ||
                    undolanditem.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            LandUndo handlerOnUndo = OnLandUndo;
            if (handlerOnUndo != null) {
                handlerOnUndo (this);
            }
            return true;
        }

        bool HandleRedo (IClientAPI sender, Packet pack)
        {
            var redoitem = (RedoPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (redoitem.AgentData.SessionID != SessionId ||
                    redoitem.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (redoitem.ObjectData.Length > 0) {
                foreach (RedoPacket.ObjectDataBlock t in redoitem.ObjectData) {
                    UUID objiD = t.ObjectID;
                    AgentSit handlerOnRedo = OnRedo;
                    if (handlerOnRedo != null) {
                        handlerOnRedo (this, objiD);
                    }
                }
            }
            return true;
        }

        bool HandleObjectDuplicateOnRay (IClientAPI sender, Packet pack)
        {
            var dupeOnRay = (ObjectDuplicateOnRayPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dupeOnRay.AgentData.SessionID != SessionId ||
                    dupeOnRay.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectDuplicateOnRayPacket.ObjectDataBlock t in dupeOnRay.ObjectData) {
                ObjectDuplicateOnRay handlerObjectDuplicateOnRay = OnObjectDuplicateOnRay;
                if (handlerObjectDuplicateOnRay != null) {
                    handlerObjectDuplicateOnRay (t.ObjectLocalID,
                                                 dupeOnRay.AgentData.DuplicateFlags,
                                                 AgentId,
                                                 m_activeGroupID,
                                                 dupeOnRay.AgentData.RayTargetID,
                                                 dupeOnRay.AgentData.RayEnd,
                                                 dupeOnRay.AgentData.RayStart,
                                                 dupeOnRay.AgentData.BypassRaycast,
                                                 dupeOnRay.AgentData.RayEndIsIntersection,
                                                 dupeOnRay.AgentData.CopyCenters,
                                                 dupeOnRay.AgentData.CopyRotates);
                }
            }

            return true;
        }

        bool HandleRequestObjectPropertiesFamily (IClientAPI sender, Packet pack)
        {
            //This powers the little tooltip that appears when you move your mouse over an object
            var packToolTip = (RequestObjectPropertiesFamilyPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (packToolTip.AgentData.SessionID != SessionId ||
                    packToolTip.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RequestObjectPropertiesFamilyPacket.ObjectDataBlock packObjBlock = packToolTip.ObjectData;

            RequestObjectPropertiesFamily handlerRequestObjectPropertiesFamily = OnRequestObjectPropertiesFamily;

            if (handlerRequestObjectPropertiesFamily != null) {
                handlerRequestObjectPropertiesFamily (this, m_agentId, packObjBlock.RequestFlags, packObjBlock.ObjectID);
            }

            return true;
        }

        bool HandleObjectIncludeInSearch (IClientAPI sender, Packet pack)
        {
            //This lets us set objects to appear in search (stuff like DataSnapshot, etc)
            var packInSearch = (ObjectIncludeInSearchPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (packInSearch.AgentData.SessionID != SessionId ||
                    packInSearch.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectIncludeInSearchPacket.ObjectDataBlock objData in packInSearch.ObjectData) {
                bool inSearch = objData.IncludeInSearch;
                uint localID = objData.ObjectLocalID;

                ObjectIncludeInSearch handlerObjectIncludeInSearch = OnObjectIncludeInSearch;

                if (handlerObjectIncludeInSearch != null) {
                    handlerObjectIncludeInSearch (this, inSearch, localID);
                }
            }
            return true;
        }

        bool HandleObjectClickAction (IClientAPI sender, Packet pack)
        {
            var ocpacket = (ObjectClickActionPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (ocpacket.AgentData.SessionID != SessionId ||
                    ocpacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            GenericCall7 handlerObjectClickAction = OnObjectClickAction;
            if (handlerObjectClickAction != null) {
                foreach (ObjectClickActionPacket.ObjectDataBlock odata in ocpacket.ObjectData) {
                    byte action = odata.ClickAction;
                    uint localID = odata.ObjectLocalID;
                    handlerObjectClickAction (this, localID, action.ToString ());
                }
            }
            return true;
        }

        bool HandleObjectMaterial (IClientAPI sender, Packet Pack)
        {
            ObjectMaterialPacket ompacket = (ObjectMaterialPacket)Pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (ompacket.AgentData.SessionID != SessionId ||
                    ompacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            GenericCall7 handlerObjectMaterial = OnObjectMaterial;
            if (handlerObjectMaterial != null) {
                foreach (ObjectMaterialPacket.ObjectDataBlock odata in ompacket.ObjectData) {
                    byte material = odata.Material;
                    uint localID = odata.ObjectLocalID;
                    handlerObjectMaterial (this, localID, material.ToString ());
                }
            }
            return true;
        }

        bool HandleMultipleObjUpdate (IClientAPI simClient, Packet pack)
        {
        	var multipleupdate = (MultipleObjectUpdatePacket)pack;
        	if (multipleupdate.AgentData.SessionID != SessionId) return false;
        	// MainConsole.Instance.Debug("new multi update packet " + multipleupdate.ToString());
        	IScene tScene = m_scene;

        	foreach (MultipleObjectUpdatePacket.ObjectDataBlock block in multipleupdate.ObjectData) {
        		// Can't act on Null Data
        		if (block.Data != null) {
        			uint localId = block.ObjectLocalID;
        			ISceneChildEntity part = tScene.GetSceneObjectPart (localId);

        			if (part == null) {
        				// It's a ghost! tell the client to delete it from view.
        				simClient.SendKillObject (Scene.RegionInfo.RegionHandle, new IEntity [] { null });
        			} else {
        				// UUID partId = part.UUID;

        				switch (block.Type) {
        				case 1:
        					Vector3 pos1 = new Vector3 (block.Data, 0);

        					UpdateVectorWithUpdate handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
        					if (handlerUpdatePrimSinglePosition != null) {
        						// MainConsole.Instance.Debug("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
        						handlerUpdatePrimSinglePosition (localId, pos1, this, true);
        					}
        					break;
        				case 2:
        					Quaternion rot1 = new Quaternion (block.Data, 0, true);

        					UpdatePrimSingleRotation handlerUpdatePrimSingleRotation = OnUpdatePrimSingleRotation;
        					if (handlerUpdatePrimSingleRotation != null) {
        						// MainConsole.Instance.Info("new tab rotation is " + rot1.X + " , " + rot1.Y + " , " + rot1.Z + " , " + rot1.W);
        						handlerUpdatePrimSingleRotation (localId, rot1, this);
        					}
        					break;
        				case 3:
        					Vector3 rotPos = new Vector3 (block.Data, 0);
        					Quaternion rot2 = new Quaternion (block.Data, 12, true);

        					UpdatePrimSingleRotationPosition handlerUpdatePrimSingleRotationPosition =
        						OnUpdatePrimSingleRotationPosition;
        					if (handlerUpdatePrimSingleRotationPosition != null) {
        						// MainConsole.Instance.Debug("new mouse rotation position is " + rotPos.X + " , " + rotPos.Y + " , " + rotPos.Z);
        						// MainConsole.Instance.Info("new mouse rotation is " + rot2.X + " , " + rot2.Y + " , " + rot2.Z + " , " + rot2.W);
        						handlerUpdatePrimSingleRotationPosition (localId, rot2, rotPos, this);
        					}
        					break;
        				case 4:
        				case 20:
        					Vector3 scale4 = new Vector3 (block.Data, 0);

        					UpdateVector handlerUpdatePrimScale = OnUpdatePrimScale;
        					if (handlerUpdatePrimScale != null) {
        						//                                     MainConsole.Instance.Debug("new scale is " + scale4.X + " , " + scale4.Y + " , " + scale4.Z);
        						handlerUpdatePrimScale (localId, scale4, this);
        					}
        					break;
        				case 5:

        					Vector3 scale1 = new Vector3 (block.Data, 12);
        					Vector3 pos11 = new Vector3 (block.Data, 0);

        					handlerUpdatePrimScale = OnUpdatePrimScale;
        					if (handlerUpdatePrimScale != null) {
        						// MainConsole.Instance.Debug("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
        						handlerUpdatePrimScale (localId, scale1, this);

        						handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
        						if (handlerUpdatePrimSinglePosition != null) {
        							handlerUpdatePrimSinglePosition (localId, pos11, this, false);
        						}
        					}
        					break;
        				case 9:
        					Vector3 pos2 = new Vector3 (block.Data, 0);

        					UpdateVectorWithUpdate handlerUpdateVector = OnUpdatePrimGroupPosition;

        					if (handlerUpdateVector != null) {
        						handlerUpdateVector (localId, pos2, this, true);
        					}
        					break;
        				case 10:
        					Quaternion rot3 = new Quaternion (block.Data, 0, true);

        					UpdatePrimRotation handlerUpdatePrimRotation = OnUpdatePrimGroupRotation;
        					if (handlerUpdatePrimRotation != null) {
        						//  Console.WriteLine("new rotation is " + rot3.X + " , " + rot3.Y + " , " + rot3.Z + " , " + rot3.W);
        						handlerUpdatePrimRotation (localId, rot3, this);
        					}
        					break;
        				case 11:
        					Vector3 pos3 = new Vector3 (block.Data, 0);
        					Quaternion rot4 = new Quaternion (block.Data, 12, true);

        					UpdatePrimGroupRotation handlerUpdatePrimGroupRotation = OnUpdatePrimGroupMouseRotation;
        					if (handlerUpdatePrimGroupRotation != null) {
        						//  MainConsole.Instance.Debug("new rotation position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
        						// MainConsole.Instance.Debug("new group mouse rotation is " + rot4.X + " , " + rot4.Y + " , " + rot4.Z + " , " + rot4.W);
        						handlerUpdatePrimGroupRotation (localId, pos3, rot4, this);
        					}
        					break;
        				case 12:
        				case 28:
        					Vector3 scale7 = new Vector3 (block.Data, 0);

        					UpdateVector handlerUpdatePrimGroupScale = OnUpdatePrimGroupScale;
        					if (handlerUpdatePrimGroupScale != null) {
        						//                                     MainConsole.Instance.Debug("new scale is " + scale7.X + " , " + scale7.Y + " , " + scale7.Z);
        						handlerUpdatePrimGroupScale (localId, scale7, this);
        					}
        					break;
        				case 13:
        					Vector3 scale2 = new Vector3 (block.Data, 12);
        					Vector3 pos4 = new Vector3 (block.Data, 0);

        					handlerUpdatePrimScale = OnUpdatePrimScale;
        					if (handlerUpdatePrimScale != null) {
        						//MainConsole.Instance.Debug("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
        						handlerUpdatePrimScale (localId, scale2, this);

        						// Change the position based on scale (for bug number 246)
        						handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
        						// MainConsole.Instance.Debug("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
        						if (handlerUpdatePrimSinglePosition != null) {
        							handlerUpdatePrimSinglePosition (localId, pos4, this, false);
        						}
        					}
        					break;
        				case 29:
        					Vector3 scale5 = new Vector3 (block.Data, 12);
        					Vector3 pos5 = new Vector3 (block.Data, 0);

        					handlerUpdatePrimGroupScale = OnUpdatePrimGroupScale;
        					if (handlerUpdatePrimGroupScale != null) {
        						// MainConsole.Instance.Debug("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
        						handlerUpdatePrimGroupScale (localId, scale5, this);
        						handlerUpdateVector = OnUpdatePrimGroupPosition;

        						if (handlerUpdateVector != null) {
        							handlerUpdateVector (localId, pos5, this, false);
        						}
        					}
        					break;
        				case 21:
        					Vector3 scale6 = new Vector3 (block.Data, 12);
        					Vector3 pos6 = new Vector3 (block.Data, 0);

        					handlerUpdatePrimScale = OnUpdatePrimScale;
        					if (handlerUpdatePrimScale != null) {
        						// MainConsole.Instance.Debug("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
        						handlerUpdatePrimScale (localId, scale6, this);
        						handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
        						if (handlerUpdatePrimSinglePosition != null) {
        							handlerUpdatePrimSinglePosition (localId, pos6, this, false);
        						}
        					}
        					break;
        				default:
        					MainConsole.Instance.Debug (
        						"[Client]: MultipleObjUpdate recieved an unknown packet type: " +
        						(block.Type));
        					break;
        				}
        			}
        		}
        	}
        	return true;
        }


        #endregion Objects/msceneObjects


    }
}
