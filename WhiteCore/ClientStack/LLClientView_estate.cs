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
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using RegionFlags = OpenMetaverse.RegionFlags;

namespace WhiteCore.ClientStack
{
    //public delegate bool PacketMethod (IClientAPI simClient, Packet packet);

    /// <summary>
    ///     Handles new client connections
    ///     Constructor takes a single Packet and authenticates everything
    /// </summary>
    public sealed partial class LLClientView : IClientAPI
    {
        #region Estate Data Sending Methods

        static bool convertParamStringToBool (byte [] field)
        {
            string s = Utils.BytesToString (field);
            if (s == "1" || s.ToLower () == "y" || s.ToLower () == "yes" || s.ToLower () == "t" || s.ToLower () == "true") {
                return true;
            }
            return false;
        }

        public void SendEstateList (UUID invoice, int code, List<UUID> Data, uint estateID)

        {
            EstateOwnerMessagePacket packet = new EstateOwnerMessagePacket {
                AgentData = { TransactionID = UUID.Random(), AgentID = AgentId, SessionID = SessionId },
                MethodData = { Invoice = invoice, Method = Utils.StringToBytes ("setaccess") }
            };

            EstateOwnerMessagePacket.ParamListBlock [] returnblock =
                new EstateOwnerMessagePacket.ParamListBlock [6 + Data.Count];

            for (int i = 0; i < (6 + Data.Count); i++) {
                returnblock [i] = new EstateOwnerMessagePacket.ParamListBlock ();
            }
            int j = 0;

            returnblock [j].Parameter = Utils.StringToBytes (estateID.ToString ());
            j++;
            returnblock [j].Parameter = Utils.StringToBytes (code.ToString ());
            j++;
            returnblock [j].Parameter = Utils.StringToBytes ("0");
            j++;
            returnblock [j].Parameter = Utils.StringToBytes ("0");
            j++;
            returnblock [j].Parameter = Utils.StringToBytes ("0");
            j++;
            returnblock [j].Parameter = Utils.StringToBytes ("0");
            j++;

            j = 2; // Agents
            if ((code & 2) != 0)
                j = 3; // Groups
            if ((code & 8) != 0)
                j = 5; // Managers

            returnblock [j].Parameter = Utils.StringToBytes (Data.Count.ToString ());
            j = 6;

            for (int i = 0; i < Data.Count; i++) {
                returnblock [j].Parameter = Data [i].GetBytes ();
                j++;
            }
            packet.ParamList = returnblock;
            packet.Header.Reliable = true;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }


        public void SendBannedUserList (UUID invoice, List<EstateBan> bl, uint estateID)
        {
            List<UUID> BannedUsers =
                (from t in bl where t != null where t.BannedUserID != UUID.Zero select t.BannedUserID).ToList ();

            EstateOwnerMessagePacket packet = new EstateOwnerMessagePacket {
                AgentData = { TransactionID = UUID.Random(),
                              AgentID = AgentId,
                              SessionID = SessionId },
                MethodData = { Invoice = invoice, Method = Utils.StringToBytes ("setaccess") }
            };

            EstateOwnerMessagePacket.ParamListBlock [] returnblock =
                new EstateOwnerMessagePacket.ParamListBlock [6 + BannedUsers.Count];

            for (int i = 0; i < (6 + BannedUsers.Count); i++) {
                returnblock [i] = new EstateOwnerMessagePacket.ParamListBlock ();
            }
            int j = 0;

            returnblock [j].Parameter = Utils.StringToBytes (estateID.ToString ());
            j++;

            returnblock [j].Parameter =
                Utils.StringToBytes (((int)EstateTools.EstateAccessReplyDelta.EstateBans).ToString ());
            j++;
            returnblock [j].Parameter = Utils.StringToBytes ("0");
            j++;
            returnblock [j].Parameter = Utils.StringToBytes ("0");
            j++;
            returnblock [j].Parameter = Utils.StringToBytes (BannedUsers.Count.ToString ());
            j++;
            returnblock [j].Parameter = Utils.StringToBytes ("0");
            j++;

            foreach (UUID banned in BannedUsers) {
                returnblock [j].Parameter = banned.GetBytes ();
                j++;
            }
            packet.ParamList = returnblock;
            packet.Header.Reliable = false;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendRegionInfoToEstateMenu (RegionInfoForEstateMenuArgs args)
        {
            RegionInfoPacket rinfopack = new RegionInfoPacket ();
            RegionInfoPacket.RegionInfoBlock rinfoblk = new RegionInfoPacket.RegionInfoBlock ();
            rinfopack.AgentData.AgentID = AgentId;
            rinfopack.AgentData.SessionID = SessionId;
            rinfoblk.BillableFactor = args.billableFactor;
            rinfoblk.EstateID = args.estateID;
            rinfoblk.MaxAgents = args.maxAgents;
            rinfoblk.ObjectBonusFactor = args.objectBonusFactor;
            rinfoblk.ParentEstateID = args.parentEstateID;
            rinfoblk.PricePerMeter = args.pricePerMeter;
            rinfoblk.RedirectGridX = args.redirectGridX;
            rinfoblk.RedirectGridY = args.redirectGridY;
            rinfoblk.RegionFlags = (uint)args.regionFlags;
            rinfoblk.SimAccess = args.simAccess;
            rinfoblk.SunHour = args.sunHour;
            rinfoblk.TerrainLowerLimit = args.terrainLowerLimit;
            rinfoblk.TerrainRaiseLimit = args.terrainRaiseLimit;
            rinfoblk.UseEstateSun = args.useEstateSun;
            rinfoblk.WaterHeight = args.waterHeight;
            rinfoblk.SimName = Utils.StringToBytes (args.simName);

            rinfopack.RegionInfo2 = new RegionInfoPacket.RegionInfo2Block {
                HardMaxAgents = uint.MaxValue,
                HardMaxObjects = uint.MaxValue,
                MaxAgents32 = args.maxAgents,
                ProductName = Utils.StringToBytes (args.regionType),
                ProductSKU = Utils.EmptyBytes
            };

            rinfopack.HasVariableBlocks = true;
            rinfopack.RegionInfo = rinfoblk;
            rinfopack.AgentData = new RegionInfoPacket.AgentDataBlock { AgentID = AgentId, SessionID = SessionId };
            rinfopack.RegionInfo3 = new RegionInfoPacket.RegionInfo3Block [1]
                                        {
                                            new RegionInfoPacket.RegionInfo3Block()
                                                {
                                                    RegionFlagsExtended = args.regionFlags
                                                }
                                        };

            OutPacket (rinfopack, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendEstateCovenantInformation (UUID covenant, int covenantLastUpdated)
        {
            EstateCovenantReplyPacket einfopack = new EstateCovenantReplyPacket ();
            EstateCovenantReplyPacket.DataBlock edata = new EstateCovenantReplyPacket.DataBlock {
                CovenantID = covenant,
                CovenantTimestamp = (uint)covenantLastUpdated,
                EstateOwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner,
                EstateName = Utils.StringToBytes (m_scene.RegionInfo.EstateSettings.EstateName)
            };
            einfopack.Data = edata;
            OutPacket (einfopack, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendDetailedEstateData (UUID invoice, string estateName, uint estateID, uint parentEstate,
                                            uint estateFlags, uint sunPosition, UUID covenant, int CovenantLastUpdated,
                                            string abuseEmail, UUID estateOwner)
        {
            EstateOwnerMessagePacket packet = new EstateOwnerMessagePacket {
                MethodData = { Invoice = invoice },
                AgentData = { TransactionID = UUID.Random () }
            };
            packet.MethodData.Method = Utils.StringToBytes ("estateupdateinfo");
            EstateOwnerMessagePacket.ParamListBlock [] returnblock = new EstateOwnerMessagePacket.ParamListBlock [10];

            for (int i = 0; i < 10; i++) {
                returnblock [i] = new EstateOwnerMessagePacket.ParamListBlock ();
            }

            //Sending Estate Settings
            returnblock [0].Parameter = Utils.StringToBytes (estateName);
            returnblock [1].Parameter = Utils.StringToBytes (estateOwner.ToString ());
            returnblock [2].Parameter = Utils.StringToBytes (estateID.ToString ());

            returnblock [3].Parameter = Utils.StringToBytes (estateFlags.ToString ());
            returnblock [4].Parameter = Utils.StringToBytes (sunPosition.ToString ());
            returnblock [5].Parameter = Utils.StringToBytes (parentEstate.ToString ());
            returnblock [6].Parameter = Utils.StringToBytes (covenant.ToString ());
            returnblock [7].Parameter = Utils.StringToBytes (CovenantLastUpdated.ToString ());
            returnblock [8].Parameter = Utils.StringToBytes ("1"); // Send to this agent only
            returnblock [9].Parameter = Utils.StringToBytes (abuseEmail);

            packet.ParamList = returnblock;
            packet.Header.Reliable = false;
            //MainConsole.Instance.Debug("[ESTATE]: SIM--->" + packet.ToString());
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        #endregion

        #region Land Data Sending Methods

        public void SendLandParcelOverlay (byte [] data, int sequence_id)
        {
            ParcelOverlayPacket packet = (ParcelOverlayPacket)PacketPool.Instance.GetPacket (PacketType.ParcelOverlay);
            packet.ParcelData.Data = data;
            packet.ParcelData.SequenceID = sequence_id;
            packet.Header.Zerocoded = true;
            OutPacket (packet, ThrottleOutPacketType.Land);
        }

        public void SendLandProperties (int sequence_id, bool snap_selection, int request_result, LandData landData,
                                        float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity,
                                        uint regionFlags)
        {
            ParcelPropertiesMessage updateMessage = new ParcelPropertiesMessage ();

            IPrimCountModule primCountModule = m_scene.RequestModuleInterface<IPrimCountModule> ();
            if (primCountModule != null) {
                IPrimCounts primCounts = primCountModule.GetPrimCounts (landData.GlobalID);
                updateMessage.GroupPrims = primCounts.Group;
                updateMessage.OtherPrims = primCounts.Others;
                updateMessage.OwnerPrims = primCounts.Owner;
                updateMessage.SelectedPrims = primCounts.Selected;
                updateMessage.SimWideTotalPrims = primCounts.Simulator;
                updateMessage.TotalPrims = primCounts.Total;
            }

            updateMessage.AABBMax = landData.AABBMax;
            updateMessage.AABBMin = landData.AABBMin;
            updateMessage.Area = landData.Area;
            updateMessage.AuctionID = landData.AuctionID;
            updateMessage.AuthBuyerID = landData.AuthBuyerID;

            updateMessage.Bitmap = landData.Bitmap;

            updateMessage.Desc = landData.Description;
            updateMessage.Category = landData.Category;
            updateMessage.ClaimDate = Util.ToDateTime (landData.ClaimDate);
            updateMessage.ClaimPrice = landData.ClaimPrice;
            updateMessage.GroupID = landData.GroupID;
            updateMessage.IsGroupOwned = landData.IsGroupOwned;
            updateMessage.LandingType = (LandingType)landData.LandingType;
            updateMessage.LocalID = landData.LocalID;

            updateMessage.MaxPrims = parcelObjectCapacity;

            updateMessage.MediaAutoScale = Convert.ToBoolean (landData.MediaAutoScale);
            updateMessage.MediaID = landData.MediaID;
            updateMessage.MediaURL = landData.MediaURL;
            updateMessage.MusicURL = landData.MusicURL;
            updateMessage.Name = landData.Name;
            updateMessage.OtherCleanTime = landData.OtherCleanTime;
            updateMessage.OtherCount = 0; //TODO: Unimplemented
            updateMessage.OwnerID = landData.OwnerID;
            updateMessage.ParcelFlags = (ParcelFlags)landData.Flags;
            updateMessage.ParcelPrimBonus = simObjectBonusFactor;
            updateMessage.PassHours = landData.PassHours;
            updateMessage.PassPrice = landData.PassPrice;
            updateMessage.PublicCount = 0; //TODO: Unimplemented
            updateMessage.Privacy = landData.Private;

            updateMessage.RegionPushOverride = (regionFlags & (uint)RegionFlags.RestrictPushObject) > 0;
            updateMessage.RegionDenyAnonymous = (regionFlags & (uint)RegionFlags.DenyAnonymous) > 0;

            updateMessage.RegionDenyIdentified = (regionFlags & (uint)RegionFlags.DenyIdentified) > 0;
            updateMessage.RegionDenyTransacted = (regionFlags & (uint)RegionFlags.DenyTransacted) > 0;

            updateMessage.RentPrice = 0;
            updateMessage.RequestResult = (ParcelResult)request_result;
            updateMessage.SalePrice = landData.SalePrice;
            updateMessage.SelfCount = 0; //TODO: Unimplemented
            updateMessage.SequenceID = sequence_id;
            updateMessage.SimWideMaxPrims = simObjectCapacity;
            updateMessage.SnapSelection = snap_selection;
            updateMessage.SnapshotID = landData.SnapshotID;
            updateMessage.Status = landData.Status;
            updateMessage.UserLocation = landData.UserLocation;
            updateMessage.UserLookAt = landData.UserLookAt;

            updateMessage.MediaType = landData.MediaType;
            updateMessage.MediaDesc = landData.MediaDescription;
            updateMessage.MediaWidth = landData.MediaWidth;
            updateMessage.MediaHeight = landData.MediaHeight;
            updateMessage.MediaLoop = landData.MediaLoop;
            updateMessage.ObscureMusic = landData.ObscureMusic;
            updateMessage.ObscureMedia = landData.ObscureMedia;
            updateMessage.SeeAVs = landData.SeeAVS;
            updateMessage.AnyAVSounds = landData.AnyAVSounds;
            updateMessage.GroupAVSounds = landData.GroupAVSounds;

            try {
                IEventQueueService eq = Scene.RequestModuleInterface<IEventQueueService> ();
                if (eq != null) {
                    eq.ParcelProperties (updateMessage, AgentId, Scene.RegionInfo.RegionID);
                } else {
                    MainConsole.Instance.Warn ("[Client]: No EQ Interface when sending parcel data.");
                }
            } catch (Exception ex) {
                MainConsole.Instance.Error ("[Client]: Unable to send parcel data via eventqueue - exception: " + ex);
            }
        }

        public void SendLandAccessListData (List<UUID> avatars, uint accessFlag, int localLandID)
        {
            ParcelAccessListReplyPacket replyPacket =
                (ParcelAccessListReplyPacket)PacketPool.Instance.GetPacket (PacketType.ParcelAccessListReply);
            replyPacket.Data.AgentID = AgentId;
            replyPacket.Data.Flags = accessFlag;
            replyPacket.Data.LocalID = localLandID;
            replyPacket.Data.SequenceID = 0;

            replyPacket.List =
                avatars.Select (
                    avatar => new ParcelAccessListReplyPacket.ListBlock { Flags = accessFlag, ID = avatar, Time = 0 }).
                        ToArray ();

            replyPacket.Header.Zerocoded = true;
            OutPacket (replyPacket, ThrottleOutPacketType.Land);
        }

        public void SendForceClientSelectObjects (List<uint> ObjectIDs)
        {
            bool firstCall = true;
            const int MAX_OBJECTS_PER_PACKET = 251;
            ForceObjectSelectPacket pack =
                (ForceObjectSelectPacket)PacketPool.Instance.GetPacket (PacketType.ForceObjectSelect);
            while (ObjectIDs.Count > 0) {
                if (firstCall) {
                    pack._Header.ResetList = true;
                    firstCall = false;
                } else {
                    pack._Header.ResetList = false;
                }

                ForceObjectSelectPacket.DataBlock [] data = ObjectIDs.Count > MAX_OBJECTS_PER_PACKET 
                                                                ? new ForceObjectSelectPacket.DataBlock [MAX_OBJECTS_PER_PACKET]
                                                                : new ForceObjectSelectPacket.DataBlock [ObjectIDs.Count];

                int i;
                for (i = 0; i < MAX_OBJECTS_PER_PACKET && ObjectIDs.Count > 0; i++) {
                    data [i] = new ForceObjectSelectPacket.DataBlock { LocalID = Convert.ToUInt32 (ObjectIDs [0]) };
                    ObjectIDs.RemoveAt (0);
                }
                pack.Data = data;
                pack.Header.Zerocoded = true;
                OutPacket (pack, ThrottleOutPacketType.State);
            }
        }

        public void SendCameraConstraint (Vector4 ConstraintPlane)
        {
            CameraConstraintPacket cpack =
                (CameraConstraintPacket)PacketPool.Instance.GetPacket (PacketType.CameraConstraint);
            cpack.CameraCollidePlane = new CameraConstraintPacket.CameraCollidePlaneBlock { Plane = ConstraintPlane };
            //MainConsole.Instance.DebugFormat("[Client]: Constraint {0}", ConstraintPlane);
            OutPacket (cpack, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendLandObjectOwners (List<LandObjectOwners> objectOwners)
        {
            int notifyCount = objectOwners.Count;

            if (notifyCount > 32) {
                MainConsole.Instance.InfoFormat (
                    "[Land]: More than {0} avatars own prims on this parcel.  Only sending back details of first {0}"
                    + " - a developer might want to investigate whether this is a hard limit", 32);

                notifyCount = 32;
            }

            ParcelObjectOwnersReplyMessage message = new ParcelObjectOwnersReplyMessage {
                PrimOwnersBlock = new ParcelObjectOwnersReplyMessage.PrimOwner [notifyCount]
            };

            int num = 0;
            foreach (LandObjectOwners owner in objectOwners) {
                message.PrimOwnersBlock [num] = new ParcelObjectOwnersReplyMessage.PrimOwner {
                    Count = owner.Count,
                    IsGroupOwned = owner.GroupOwned,
                    OnlineStatus = owner.Online,
                    OwnerID = owner.OwnerID,
                    TimeStamp = owner.TimeLastRezzed
                };

                num++;

                if (num >= notifyCount)
                    break;
            }

            IEventQueueService eventQueueService = m_scene.RequestModuleInterface<IEventQueueService> ();
            if (eventQueueService != null) {
                eventQueueService.ParcelObjectOwnersReply (message, AgentId, m_scene.RegionInfo.RegionID);
            }
        }

        #endregion

        #region Parcel related packets

        bool HandleRegionHandleRequest (IClientAPI sender, Packet pack)
        {
            var rhrPack = (RegionHandleRequestPacket)pack;

            RegionHandleRequest handlerRegionHandleRequest = OnRegionHandleRequest;
            if (handlerRegionHandleRequest != null) {
                handlerRegionHandleRequest (this, rhrPack.RequestBlock.RegionID);
            }
            return true;
        }

        bool HandleParcelInfoRequest (IClientAPI sender, Packet pack)
        {
            var pirPack = (ParcelInfoRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (pirPack.AgentData.SessionID != SessionId ||
                    pirPack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelInfoRequest handlerParcelInfoRequest = OnParcelInfoRequest;
            if (handlerParcelInfoRequest != null) {
                handlerParcelInfoRequest (this, pirPack.Data.ParcelID);
            }
            return true;
        }

        bool HandleParcelAccessListRequest (IClientAPI sender, Packet pack)
        {
            var requestPacket = (ParcelAccessListRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (requestPacket.AgentData.SessionID != SessionId ||
                    requestPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelAccessListRequest handlerParcelAccessListRequest = OnParcelAccessListRequest;

            if (handlerParcelAccessListRequest != null) {
                handlerParcelAccessListRequest (requestPacket.AgentData.AgentID,
                                                requestPacket.AgentData.SessionID,
                                                requestPacket.Data.Flags,
                                                requestPacket.Data.SequenceID,
                                                requestPacket.Data.LocalID,
                                                this);
            }
            return true;
        }

        bool HandleParcelAccessListUpdate (IClientAPI sender, Packet pack)
        {
            var updatePacket = (ParcelAccessListUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (updatePacket.AgentData.SessionID != SessionId ||
                    updatePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            List<ParcelManager.ParcelAccessEntry> entries =
                updatePacket.List.Select (block => new ParcelManager.ParcelAccessEntry {
                    AgentID = block.ID,
                    Flags = (AccessList)block.Flags,
                    Time = new DateTime ()
                }).ToList ();

            ParcelAccessListUpdateRequest handlerParcelAccessListUpdateRequest = OnParcelAccessListUpdateRequest;
            if (handlerParcelAccessListUpdateRequest != null) {
                handlerParcelAccessListUpdateRequest (updatePacket.AgentData.AgentID,
                                                      updatePacket.AgentData.SessionID,
                                                      updatePacket.Data.Flags,
                                                      updatePacket.Data.LocalID,
                                                      entries,
                                                      this);
            }
            return true;
        }

        bool HandleParcelPropertiesRequest (IClientAPI sender, Packet pack)
        {
            var propertiesRequest = (ParcelPropertiesRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (propertiesRequest.AgentData.SessionID != SessionId ||
                    propertiesRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelPropertiesRequest handlerParcelPropertiesRequest = OnParcelPropertiesRequest;
            if (handlerParcelPropertiesRequest != null) {
                handlerParcelPropertiesRequest ((int)Math.Round (propertiesRequest.ParcelData.West),
                                                (int)Math.Round (propertiesRequest.ParcelData.South),
                                                (int)Math.Round (propertiesRequest.ParcelData.East),
                                                (int)Math.Round (propertiesRequest.ParcelData.North),
                                                propertiesRequest.ParcelData.SequenceID,
                                                propertiesRequest.ParcelData.SnapSelection,
                                                this);
            }
            return true;
        }

        bool HandleParcelDivide (IClientAPI sender, Packet pack)
        {
            var landDivide = (ParcelDividePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (landDivide.AgentData.SessionID != SessionId ||
                    landDivide.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelDivideRequest handlerParcelDivideRequest = OnParcelDivideRequest;
            if (handlerParcelDivideRequest != null) {
                handlerParcelDivideRequest ((int)Math.Round (landDivide.ParcelData.West),
                                            (int)Math.Round (landDivide.ParcelData.South),
                                            (int)Math.Round (landDivide.ParcelData.East),
                                            (int)Math.Round (landDivide.ParcelData.North),
                                            this);
            }
            return true;
        }

        bool HandleParcelJoin (IClientAPI sender, Packet pack)
        {
            var landJoin = (ParcelJoinPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (landJoin.AgentData.SessionID != SessionId ||
                    landJoin.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelJoinRequest handlerParcelJoinRequest = OnParcelJoinRequest;

            if (handlerParcelJoinRequest != null) {
                handlerParcelJoinRequest ((int)Math.Round (landJoin.ParcelData.West),
                                          (int)Math.Round (landJoin.ParcelData.South),
                                          (int)Math.Round (landJoin.ParcelData.East),
                                          (int)Math.Round (landJoin.ParcelData.North),
                                          this);
            }
            return true;
        }

        bool HandleParcelPropertiesUpdate (IClientAPI sender, Packet pack)
        {
            var parcelPropertiesPacket = (ParcelPropertiesUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (parcelPropertiesPacket.AgentData.SessionID != SessionId ||
                    parcelPropertiesPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelPropertiesUpdateRequest handlerParcelPropertiesUpdateRequest = OnParcelPropertiesUpdateRequest;

            if (handlerParcelPropertiesUpdateRequest != null) {
                LandUpdateArgs args = new LandUpdateArgs {
                    AuthBuyerID = parcelPropertiesPacket.ParcelData.AuthBuyerID,
                    Category = (ParcelCategory)parcelPropertiesPacket.ParcelData.Category,
                    Desc = Utils.BytesToString (parcelPropertiesPacket.ParcelData.Desc),
                    GroupID = parcelPropertiesPacket.ParcelData.GroupID,
                    LandingType = parcelPropertiesPacket.ParcelData.LandingType,
                    MediaAutoScale = parcelPropertiesPacket.ParcelData.MediaAutoScale,
                    MediaID = parcelPropertiesPacket.ParcelData.MediaID,
                    MediaURL = Utils.BytesToString (parcelPropertiesPacket.ParcelData.MediaURL),
                    MusicURL = Utils.BytesToString (parcelPropertiesPacket.ParcelData.MusicURL),
                    Name = Utils.BytesToString (parcelPropertiesPacket.ParcelData.Name),
                    ParcelFlags = parcelPropertiesPacket.ParcelData.ParcelFlags,
                    PassHours = parcelPropertiesPacket.ParcelData.PassHours,
                    PassPrice = parcelPropertiesPacket.ParcelData.PassPrice,
                    SalePrice = parcelPropertiesPacket.ParcelData.SalePrice,
                    SnapshotID = parcelPropertiesPacket.ParcelData.SnapshotID,
                    UserLocation = parcelPropertiesPacket.ParcelData.UserLocation,
                    UserLookAt = parcelPropertiesPacket.ParcelData.UserLookAt
                };

                handlerParcelPropertiesUpdateRequest (args, parcelPropertiesPacket.ParcelData.LocalID, this);
            }
            return true;
        }

        public void FireUpdateParcel (LandUpdateArgs args, int LocalID)
        {
            ParcelPropertiesUpdateRequest handlerParcelPropertiesUpdateRequest = OnParcelPropertiesUpdateRequest;

            if (handlerParcelPropertiesUpdateRequest != null) {
                handlerParcelPropertiesUpdateRequest (args, LocalID, this);
            }
        }

        bool HandleParcelSelectObjects (IClientAPI sender, Packet pack)
        {
            var selectPacket = (ParcelSelectObjectsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (selectPacket.AgentData.SessionID != SessionId ||
                    selectPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            List<UUID> returnIDs = selectPacket.ReturnIDs.Select (rb => rb.ReturnID).ToList ();

            ParcelSelectObjects handlerParcelSelectObjects = OnParcelSelectObjects;

            if (handlerParcelSelectObjects != null) {
                handlerParcelSelectObjects (selectPacket.ParcelData.LocalID,
                                            Convert.ToInt32 (selectPacket.ParcelData.ReturnType),
                                            returnIDs,
                                            this);
            }
            return true;
        }

        bool HandleParcelObjectOwnersRequest (IClientAPI sender, Packet pack)
        {
            var reqPacket = (ParcelObjectOwnersRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (reqPacket.AgentData.SessionID != SessionId ||
                    reqPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelObjectOwnerRequest handlerParcelObjectOwnerRequest = OnParcelObjectOwnerRequest;

            if (handlerParcelObjectOwnerRequest != null) {
                handlerParcelObjectOwnerRequest (reqPacket.ParcelData.LocalID, this);
            }
            return true;
        }

        bool HandleParcelGodForceOwner (IClientAPI sender, Packet pack)
        {
            var godForceOwnerPacket = (ParcelGodForceOwnerPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (godForceOwnerPacket.AgentData.SessionID != SessionId ||
                    godForceOwnerPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelGodForceOwner handlerParcelGodForceOwner = OnParcelGodForceOwner;
            if (handlerParcelGodForceOwner != null) {
                handlerParcelGodForceOwner (godForceOwnerPacket.Data.LocalID, godForceOwnerPacket.Data.OwnerID, this);
            }
            return true;
        }

        bool HandleParcelRelease (IClientAPI sender, Packet pack)
        {
            var releasePacket = (ParcelReleasePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (releasePacket.AgentData.SessionID != SessionId ||
                    releasePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelAbandonRequest handlerParcelAbandonRequest = OnParcelAbandonRequest;
            if (handlerParcelAbandonRequest != null) {
                handlerParcelAbandonRequest (releasePacket.Data.LocalID, this);
            }
            return true;
        }

        bool HandleParcelReclaim (IClientAPI sender, Packet pack)
        {
            var reclaimPacket = (ParcelReclaimPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (reclaimPacket.AgentData.SessionID != SessionId ||
                    reclaimPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelReclaim handlerParcelReclaim = OnParcelReclaim;
            if (handlerParcelReclaim != null) {
                handlerParcelReclaim (reclaimPacket.Data.LocalID, this);
            }
            return true;
        }

        bool HandleParcelReturnObjects (IClientAPI sender, Packet pack)
        {
            var parcelReturnObjects = (ParcelReturnObjectsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (parcelReturnObjects.AgentData.SessionID != SessionId ||
                    parcelReturnObjects.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            UUID [] puserselectedOwnerIDs = new UUID [parcelReturnObjects.OwnerIDs.Length];
            for (int parceliterator = 0; parceliterator < parcelReturnObjects.OwnerIDs.Length; parceliterator++)
                puserselectedOwnerIDs [parceliterator] = parcelReturnObjects.OwnerIDs [parceliterator].OwnerID;

            UUID [] puserselectedTaskIDs = new UUID [parcelReturnObjects.TaskIDs.Length];

            for (int parceliterator = 0; parceliterator < parcelReturnObjects.TaskIDs.Length; parceliterator++)
                puserselectedTaskIDs [parceliterator] = parcelReturnObjects.TaskIDs [parceliterator].TaskID;

            ParcelReturnObjectsRequest handlerParcelReturnObjectsRequest = OnParcelReturnObjectsRequest;
            if (handlerParcelReturnObjectsRequest != null) {
                handlerParcelReturnObjectsRequest (parcelReturnObjects.ParcelData.LocalID,
                                                   parcelReturnObjects.ParcelData.ReturnType,
                                                   puserselectedOwnerIDs,
                                                   puserselectedTaskIDs,
                                                   this);
            }
            return true;
        }

        bool HandleParcelSetOtherCleanTime (IClientAPI sender, Packet pack)
        {
            var parcelSetOtherCleanTimePacket = (ParcelSetOtherCleanTimePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (parcelSetOtherCleanTimePacket.AgentData.SessionID != SessionId ||
                    parcelSetOtherCleanTimePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelSetOtherCleanTime handlerParcelSetOtherCleanTime = OnParcelSetOtherCleanTime;
            if (handlerParcelSetOtherCleanTime != null) {
                handlerParcelSetOtherCleanTime (this,
                                                parcelSetOtherCleanTimePacket.ParcelData.LocalID,
                                                parcelSetOtherCleanTimePacket.ParcelData.OtherCleanTime);
            }
            return true;
        }

        bool HandleLandStatRequest (IClientAPI sender, Packet pack)
        {
            var lsrp = (LandStatRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (lsrp.AgentData.SessionID != SessionId ||
                    lsrp.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            GodLandStatRequest handlerLandStatRequest = OnLandStatRequest;
            if (handlerLandStatRequest != null) {
                handlerLandStatRequest (lsrp.RequestData.ParcelLocalID, 
                                        lsrp.RequestData.ReportType,
                                        lsrp.RequestData.RequestFlags,
                                        Utils.BytesToString (lsrp.RequestData.Filter),
                                        this);
            }
            return true;
        }

        bool HandleParcelDwellRequest (IClientAPI sender, Packet pack)
        {
            var dwellrq = (ParcelDwellRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dwellrq.AgentData.SessionID != SessionId ||
                    dwellrq.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelDwellRequest handlerParcelDwellRequest = OnParcelDwellRequest;
            if (handlerParcelDwellRequest != null) {
                handlerParcelDwellRequest (dwellrq.Data.LocalID, this);
            }
            return true;
        }

        #endregion Parcel related packets

        #region Estate Packets

        bool HandleEstateOwnerMessage (IClientAPI sender, Packet pack)
        {
            var messagePacket = (EstateOwnerMessagePacket)pack;
            //MainConsole.Instance.Debug(messagePacket.ToString());
            GodLandStatRequest handlerLandStatRequest;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (messagePacket.AgentData.SessionID != SessionId ||
                    messagePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            string method = Utils.BytesToString (messagePacket.MethodData.Method);

            switch (method) {
            case "getinfo":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    OnDetailedEstateDataRequest (this, messagePacket.MethodData.Invoice);
                }
                return true;
            case "setregioninfo":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    OnSetEstateFlagsRequest (this,
                                             convertParamStringToBool (messagePacket.ParamList [0].Parameter),
                                             convertParamStringToBool (messagePacket.ParamList [1].Parameter),
                                             convertParamStringToBool (messagePacket.ParamList [2].Parameter),
                                             convertParamStringToBool (messagePacket.ParamList [3].Parameter),
                                             Convert.ToInt16 (
                                                 Convert.ToDecimal (
                                                     Utils.BytesToString (messagePacket.ParamList [4].Parameter),
                                                     Culture.NumberFormatInfo)),
                                             (float) Convert.ToDecimal (
                                                 Utils.BytesToString (messagePacket.ParamList [5].Parameter),
                                                 Culture.NumberFormatInfo),
                                             Convert.ToInt16 (Utils.BytesToString (messagePacket.ParamList [6].Parameter)),
                                             convertParamStringToBool (messagePacket.ParamList [7].Parameter),
                                             convertParamStringToBool (messagePacket.ParamList [8].Parameter));
                }
                return true;
            //                            case "texturebase":
            //                                if (((Scene)m_scene).Permissions.CanIssueEstateCommand(AgentId, false))
            //                                {
            //                                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
            //                                    {
            //                                        string s = Utils.BytesToString(block.Parameter);
            //                                        string[] splitField = s.Split(' ');
            //                                        if (splitField.Length == 2)
            //                                        {
            //                                            UUID tempUUID = new UUID(splitField[1]);
            //                                            OnSetEstateTerrainBaseTexture(this, Convert.ToInt16(splitField[0]), tempUUID);
            //                                        }
            //                                    }
            //                                }
            //                                break;
            case "texturedetail":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList) {
                        string s = Utils.BytesToString (block.Parameter);
                        string [] splitField = s.Split (' ');
                        if (splitField.Length == 2) {
                            short corner = Convert.ToInt16 (splitField [0]);
                            UUID textureUUID = new UUID (splitField [1]);

                            OnSetEstateTerrainDetailTexture (this, corner, textureUUID);
                        }
                    }
                }

                return true;
            case "textureheights":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList) {
                        string s = Utils.BytesToString (block.Parameter);
                        string [] splitField = s.Split (' ');
                        if (splitField.Length == 3) {
                            short corner = Convert.ToInt16 (splitField [0]);
                            float lowValue = (float)Convert.ToDecimal (splitField [1], Culture.NumberFormatInfo);
                            float highValue = (float)Convert.ToDecimal (splitField [2], Culture.NumberFormatInfo);

                            OnSetEstateTerrainTextureHeights (this, corner, lowValue, highValue);
                        }
                    }
                }
                return true;
            case "texturecommit":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    OnCommitEstateTerrainTextureRequest (this);
                }
                return true;
            case "setregionterrain":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    if (messagePacket.ParamList.Length != 9) {
                        MainConsole.Instance.Error (
                            "[Client]: EstateOwnerMessage: SetRegionTerrain method has a ParamList of invalid length");
                    } else {
                        try {
                            string tmp = Utils.BytesToString (messagePacket.ParamList [0].Parameter);
                            if (!tmp.Contains (".")) 
                                tmp += ".00";
                            float WaterHeight = (float)Convert.ToDecimal (tmp, Culture.NumberFormatInfo);
                            tmp = Utils.BytesToString (messagePacket.ParamList [1].Parameter);
                            if (!tmp.Contains (".")) 
                                tmp += ".00";
                            float TerrainRaiseLimit = (float)Convert.ToDecimal (tmp, Culture.NumberFormatInfo);
                            tmp = Utils.BytesToString (messagePacket.ParamList [2].Parameter);
                            if (!tmp.Contains (".")) 
                                tmp += ".00";
                            float TerrainLowerLimit = (float)Convert.ToDecimal (tmp, Culture.NumberFormatInfo);
                            bool UseEstateSun = convertParamStringToBool (messagePacket.ParamList [3].Parameter);
                            bool UseFixedSun = convertParamStringToBool (messagePacket.ParamList [4].Parameter);
                            float SunHour =(float) Convert.ToDecimal (Utils.BytesToString (messagePacket.ParamList [5].Parameter),
                                                                      Culture.NumberFormatInfo);
                            bool UseGlobal = convertParamStringToBool (messagePacket.ParamList [6].Parameter);
                            bool EstateFixedSun = convertParamStringToBool (messagePacket.ParamList [7].Parameter);
                            float EstateSunHour = (float)Convert.ToDecimal (Utils.BytesToString (messagePacket.ParamList [8].Parameter),
                                                                            Culture.NumberFormatInfo);

                            OnSetRegionTerrainSettings (AgentId,
                                                        WaterHeight,
                                                        TerrainRaiseLimit,
                                                        TerrainLowerLimit,
                                                        UseEstateSun,
                                                        UseFixedSun,
                                                        SunHour,
                                                        UseGlobal,
                                                        EstateFixedSun,
                                                        EstateSunHour);
                        } catch (Exception ex) {
                            MainConsole.Instance.Error ("[Clinet]: EstateOwnerMessage: Exception while setting terrain settings: \n" +
                                messagePacket + "\n" + ex);
                        }
                    }
                }

                return true;
            case "restart":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    // There's only 1 block in the estateResetSim..   and that's the number of seconds till restart.
                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList) {
                        float timeSeconds;
                        Utils.TryParseSingle (Utils.BytesToString (block.Parameter), out timeSeconds);
                        timeSeconds = (int)timeSeconds;
                        OnEstateRestartSimRequest (this, (int)timeSeconds);
                    }
                }
                return true;
            case "estatechangecovenantid":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    foreach (
                        UUID newCovenantID in
                            messagePacket.ParamList.Select (block => new UUID (Utils.BytesToString (block.Parameter)))) {
                        OnEstateChangeCovenantRequest (this, newCovenantID);
                    }
                }
                return true;
            case "estateaccessdelta": // Estate access delta manages the banlist and allow list too.
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    int estateAccessType = Convert.ToInt16 (Utils.BytesToString (messagePacket.ParamList [1].Parameter));
                    OnUpdateEstateAccessDeltaRequest (this,
                                                      messagePacket.MethodData.Invoice,
                                                      estateAccessType,
                                                      new UUID (Utils.BytesToString (messagePacket.ParamList [2].Parameter)));
                }
                return true;
            case "simulatormessage":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    UUID invoice = messagePacket.MethodData.Invoice;
                    UUID SenderID = new UUID (Utils.BytesToString (messagePacket.ParamList [2].Parameter));
                    string SenderName = Utils.BytesToString (messagePacket.ParamList [3].Parameter);
                    string Message = Utils.BytesToString (messagePacket.ParamList [4].Parameter);
                    UUID sessionID = messagePacket.AgentData.SessionID;
                    OnSimulatorBlueBoxMessageRequest (this, invoice, SenderID, sessionID, SenderName, Message);
                }
                return true;
            case "instantmessage":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    UUID invoice = messagePacket.MethodData.Invoice;
                    UUID sessionID = messagePacket.AgentData.SessionID;
                    string Message = "";
                    string SenderName = "";
                    UUID SenderID = UUID.Zero;
                    if (messagePacket.ParamList.Length < 5) {
                        SenderName = Utils.BytesToString (messagePacket.ParamList [0].Parameter);
                        Message = Utils.BytesToString (messagePacket.ParamList [1].Parameter);
                        SenderID = AgentId;
                    } else {
                        SenderID = new UUID (Utils.BytesToString (messagePacket.ParamList [2].Parameter));
                        SenderName = Utils.BytesToString (messagePacket.ParamList [3].Parameter);
                        Message = Utils.BytesToString (messagePacket.ParamList [4].Parameter);
                    }
                    OnEstateBlueBoxMessageRequest (this, invoice, SenderID, sessionID, SenderName, Message);
                }
                return true;
            case "setregiondebug":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    UUID invoice = messagePacket.MethodData.Invoice;
                    UUID SenderID = messagePacket.AgentData.AgentID;
                    bool scripted = convertParamStringToBool (messagePacket.ParamList [0].Parameter);
                    bool collisionEvents = convertParamStringToBool (messagePacket.ParamList [1].Parameter);
                    bool physics = convertParamStringToBool (messagePacket.ParamList [2].Parameter);

                    OnEstateDebugRegionRequest (this, invoice, SenderID, scripted, collisionEvents, physics);
                }
                return true;
            case "teleporthomeuser":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    UUID invoice = messagePacket.MethodData.Invoice;
                    UUID SenderID = messagePacket.AgentData.AgentID;
                    UUID Prey;

                    UUID.TryParse (Utils.BytesToString (messagePacket.ParamList [1].Parameter), out Prey);

                    OnEstateTeleportOneUserHomeRequest (this, invoice, SenderID, Prey);
                }
                return true;
            case "teleporthomeallusers":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    UUID invoice = messagePacket.MethodData.Invoice;
                    UUID SenderID = messagePacket.AgentData.AgentID;
                    OnEstateTeleportAllUsersHomeRequest (this, invoice, SenderID);
                }
                return true;
            case "colliders":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    handlerLandStatRequest = OnLandStatRequest;
                    if (handlerLandStatRequest != null) {
                        handlerLandStatRequest (0, 1, 0, "", this);
                    }
                }
                return true;
            case "scripts":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    handlerLandStatRequest = OnLandStatRequest;
                    if (handlerLandStatRequest != null) {
                        handlerLandStatRequest (0, 0, 0, "", this);
                    }
                }
                return true;
            case "terrain":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    if (messagePacket.ParamList.Length > 0) {
                        if (Utils.BytesToString (messagePacket.ParamList [0].Parameter) == "bake") {
                            BakeTerrain handlerBakeTerrain = OnBakeTerrain;
                            if (handlerBakeTerrain != null) {
                                handlerBakeTerrain (this);
                            }
                        }
                        if (Utils.BytesToString (messagePacket.ParamList [0].Parameter) == "download filename") {
                            if (messagePacket.ParamList.Length > 1) {
                                RequestTerrain handlerRequestTerrain = OnRequestTerrain;
                                if (handlerRequestTerrain != null) {
                                    handlerRequestTerrain (this, Utils.BytesToString (messagePacket.ParamList [1].Parameter));
                                }
                            }
                        }
                        if (Utils.BytesToString (messagePacket.ParamList [0].Parameter) == "upload filename") {
                            if (messagePacket.ParamList.Length > 1) {
                                RequestTerrain handlerUploadTerrain = OnUploadTerrain;
                                if (handlerUploadTerrain != null) {
                                    handlerUploadTerrain (this, Utils.BytesToString (messagePacket.ParamList [1].Parameter));
                                }
                            }
                        }
                    }
                }
                return true;

            case "estatechangeinfo":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    UUID invoice = messagePacket.MethodData.Invoice;
                    UUID SenderID = messagePacket.AgentData.AgentID;
                    uint param1 = Convert.ToUInt32 (Utils.BytesToString (messagePacket.ParamList [1].Parameter));
                    uint param2 = Convert.ToUInt32 (Utils.BytesToString (messagePacket.ParamList [2].Parameter));

                    EstateChangeInfo handlerEstateChangeInfo = OnEstateChangeInfo;
                    if (handlerEstateChangeInfo != null) {
                        handlerEstateChangeInfo (this, invoice, SenderID, param1, param2);
                    }
                }
                return true;

            case "refreshmapvisibility":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    IMapImageGenerator mapModule = Scene.RequestModuleInterface<IMapImageGenerator> ();
                    if (mapModule != null)
                        mapModule.CreateTerrainTexture (true);
                }
                return true;

            case "kickestate":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    UUID Prey;

                    UUID.TryParse (Utils.BytesToString (messagePacket.ParamList [0].Parameter), out Prey);
                    IClientAPI client;
                    m_scene.ClientManager.TryGetValue (Prey, out client);
                    if (client == null)
                        return true;
                    client.Kick ("The WhiteCore Manager has kicked you");
                    IEntityTransferModule transferModule = Scene.RequestModuleInterface<IEntityTransferModule> ();
                    if (transferModule != null)
                        transferModule.IncomingCloseAgent (Scene, Prey);
                }
                return true;
            case "telehub":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    List<string> Parameters =
                        messagePacket.ParamList.Select (block => Utils.BytesToString (block.Parameter)).ToList ();

                    GodlikeMessage handlerEstateTelehubRequest = OnEstateTelehubRequest;
                    if (handlerEstateTelehubRequest != null) {
                        handlerEstateTelehubRequest (this,
                                                    messagePacket.MethodData.Invoice,
                                                    Utils.BytesToString (messagePacket.MethodData.Method),
                                                    Parameters);
                    }
                }
                return true;
            case "estateobjectreturn":
                if (m_scene.Permissions.CanIssueEstateCommand (AgentId, false)) {
                    SimWideDeletesDelegate handlerSimWideDeletesRequest = OnSimWideDeletes;
                    if (handlerSimWideDeletesRequest != null) {
                        UUID Prey;
                        UUID.TryParse (Utils.BytesToString (messagePacket.ParamList [1].Parameter), out Prey);
                        int flags = int.Parse (Utils.BytesToString (messagePacket.ParamList [0].Parameter));
                        handlerSimWideDeletesRequest (this, flags, Prey);
                        return true;
                    }
                }
                return true;
            default:
                MainConsole.Instance.WarnFormat (
                    "[Client]: EstateOwnerMessage: Unknown method {0} requested for {1}",
                    method, Name);

                for (int i = 0; i < messagePacket.ParamList.Length; i++) {
                    EstateOwnerMessagePacket.ParamListBlock block = messagePacket.ParamList [i];
                    string data = Utils.BytesToString (block.Parameter);
                    MainConsole.Instance.DebugFormat ("[Client]: Param {0}={1}", i, data);
                }

                return true;
            }
        }

        bool HandleRequestRegionInfo (IClientAPI sender, Packet pack)
        {
            RequestRegionInfoPacket.AgentDataBlock mPacket = ((RequestRegionInfoPacket)pack).AgentData;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (mPacket.SessionID != SessionId ||
                    mPacket.AgentID != AgentId)
                    return true;
            }

            #endregion

            RegionInfoRequest handlerRegionInfoRequest = OnRegionInfoRequest;
            if (handlerRegionInfoRequest != null) {
                handlerRegionInfoRequest (this);
            }
            return true;
        }

        bool HandleEstateCovenantRequest (IClientAPI sender, Packet pack)
        {
            //EstateCovenantRequestPacket.AgentDataBlock epack =
            //     ((EstateCovenantRequestPacket)Pack).AgentData;

            EstateCovenantRequest handlerEstateCovenantRequest = OnEstateCovenantRequest;
            if (handlerEstateCovenantRequest != null) {
                handlerEstateCovenantRequest (this);
            }
            return true;
        }

        #endregion Estate Packets


    }
}
