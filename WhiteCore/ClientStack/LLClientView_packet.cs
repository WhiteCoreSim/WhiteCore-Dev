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
using OpenMetaverse.Packets;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.PresenceInfo;
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
        #region Packet Handling

        public void PopulateStats (int inPackets, int outPackets, int unAckedBytes)
        {
            NetworkStats handlerNetworkStatsUpdate = OnNetworkStatsUpdate;
            if (handlerNetworkStatsUpdate != null) {
                handlerNetworkStatsUpdate (inPackets, outPackets, unAckedBytes);
            }
        }

        public static bool AddPacketHandler (PacketType packetType, PacketMethod handler)
        {
            bool result = false;
            lock (PacketHandlers) {
                if (!PacketHandlers.ContainsKey (packetType)) {
                    PacketHandlers.Add (packetType, handler);
                    result = true;
                }
            }
            return result;
        }

        public bool AddLocalPacketHandler (PacketType packetType, PacketMethod handler)
        {
            return AddLocalPacketHandler (packetType, handler, true);
        }

        public bool AddLocalPacketHandler (PacketType packetType, PacketMethod handler, bool runasync)
        {
            bool result = false;
            lock (m_packetHandlers) {
                if (!m_packetHandlers.ContainsKey (packetType)) {
                    m_packetHandlers.Add (packetType, new PacketProcessor { method = handler, Async = runasync });
                    result = true;
                }
            }
            return result;
        }

        public bool AddGenericPacketHandler (string MethodName, GenericMessage handler)
        {
            MethodName = MethodName.ToLower ().Trim ();

            bool result = false;
            lock (m_genericPacketHandlers) {
                if (!m_genericPacketHandlers.ContainsKey (MethodName)) {
                    m_genericPacketHandlers.Add (MethodName, handler);
                    result = true;
                }
            }
            return result;
        }

        public bool RemoveGenericPacketHandler (string MethodName)
        {
            MethodName = MethodName.ToLower ().Trim ();

            bool result = false;
            lock (m_genericPacketHandlers) {
                if (m_genericPacketHandlers.ContainsKey (MethodName)) {
                    m_genericPacketHandlers.Remove (MethodName);
                    result = true;
                }
            }
            return result;
        }

        /// <summary>
        ///     Try to process a packet using registered packet handlers
        /// </summary>
        /// <param name="packet"></param>
        /// <returns>True if a handler was found which successfully processed the packet.</returns>
        bool ProcessPacketMethod (Packet packet)
        {
            bool result = false;
            PacketProcessor pprocessor;

            bool localHandler;
            lock (m_packetHandlers) {
                localHandler = m_packetHandlers.TryGetValue (packet.Type, out pprocessor);
            }
            if (localHandler) {
                //there is a local handler for this packet type
                if (pprocessor.Async) {
                    object obj = new AsyncPacketProcess (this, pprocessor.method, packet);
                    m_udpServer.FireAndForget (ProcessSpecificPacketAsync, obj);
                    result = true;
                } else {
                    result = pprocessor.method (this, packet);
                }
                return result;
            }

            //there is not a local handler so see if there is a Global handler
            PacketMethod method = null;
            bool found;
            lock (PacketHandlers) {
                found = PacketHandlers.TryGetValue (packet.Type, out method);
            }
            if (found) {
                result = method (this, packet);
            }
            return result;
        }

        public void ProcessSpecificPacketAsync (object state)
        {
            AsyncPacketProcess packetObject = (AsyncPacketProcess)state;

            try {
                packetObject.result = packetObject.Method (packetObject.ClientView, packetObject.Pack);
            } catch (Exception e) {
                // Make sure that we see any exception caused by the asynchronous operation.
                MainConsole.Instance.ErrorFormat (
                    "[Client]: Caught exception while processing {0} for {1}, {2} {3}",
                    packetObject.Pack, Name, e.Message, e.StackTrace);
            }
        }

        #endregion Packet Handling

        #region Local Packet Handlers
        /// <summary>
        ///     This is a different way of processing packets then ProcessInPacket
        /// </summary>
        void RegisterLocalPacketHandlers ()
        {
            AddLocalPacketHandler (PacketType.LogoutRequest, HandleLogout);
            AddLocalPacketHandler (PacketType.AgentUpdate, HandleAgentUpdate, false);
            AddLocalPacketHandler (PacketType.ViewerEffect, HandleViewerEffect, true);
            AddLocalPacketHandler (PacketType.AgentCachedTexture, HandleAgentTextureCached, false);
            AddLocalPacketHandler (PacketType.MultipleObjectUpdate, HandleMultipleObjUpdate, false);
            AddLocalPacketHandler (PacketType.MoneyTransferRequest, HandleMoneyTransferRequest, false);
            AddLocalPacketHandler (PacketType.ParcelBuy, HandleParcelBuyRequest, false);
            AddLocalPacketHandler (PacketType.UUIDGroupNameRequest, HandleUUIDGroupNameRequest);
            AddLocalPacketHandler (PacketType.ObjectGroup, HandleObjectGroupRequest);
            AddLocalPacketHandler (PacketType.GenericMessage, HandleGenericMessage);
            AddLocalPacketHandler (PacketType.AvatarPropertiesRequest, HandleAvatarPropertiesRequest);
            AddLocalPacketHandler (PacketType.ChatFromViewer, HandleChatFromViewer);
            AddLocalPacketHandler (PacketType.AvatarPropertiesUpdate, HandlerAvatarPropertiesUpdate);
            AddLocalPacketHandler (PacketType.ScriptDialogReply, HandlerScriptDialogReply);
            AddLocalPacketHandler (PacketType.ImprovedInstantMessage, HandlerImprovedInstantMessage, false);
            AddLocalPacketHandler (PacketType.AcceptFriendship, HandlerAcceptFriendship);
            AddLocalPacketHandler (PacketType.DeclineFriendship, HandlerDeclineFriendship);
            AddLocalPacketHandler (PacketType.TerminateFriendship, HandlerTerminateFrendship);
            AddLocalPacketHandler (PacketType.RezObject, HandlerRezObject);
            AddLocalPacketHandler (PacketType.RezObjectFromNotecard, HandlerRezObjectFromNotecard);
            AddLocalPacketHandler (PacketType.DeRezObject, HandlerDeRezObject);
            AddLocalPacketHandler (PacketType.ModifyLand, HandlerModifyLand);
            AddLocalPacketHandler (PacketType.RegionHandshakeReply, HandlerRegionHandshakeReply);
            AddLocalPacketHandler (PacketType.AgentWearablesRequest, HandlerAgentWearablesRequest);
            AddLocalPacketHandler (PacketType.AgentSetAppearance, HandlerAgentSetAppearance);
            AddLocalPacketHandler (PacketType.AgentIsNowWearing, HandlerAgentIsNowWearing);
            AddLocalPacketHandler (PacketType.RezSingleAttachmentFromInv, HandlerRezSingleAttachmentFromInv);
            AddLocalPacketHandler (PacketType.RezRestoreToWorld, HandlerRezRestoreToWorld);
            AddLocalPacketHandler (PacketType.RezMultipleAttachmentsFromInv, HandleRezMultipleAttachmentsFromInv);
            AddLocalPacketHandler (PacketType.DetachAttachmentIntoInv, HandleDetachAttachmentIntoInv);
            AddLocalPacketHandler (PacketType.ObjectAttach, HandleObjectAttach);
            AddLocalPacketHandler (PacketType.ObjectDetach, HandleObjectDetach);
            AddLocalPacketHandler (PacketType.ObjectDrop, HandleObjectDrop);
            AddLocalPacketHandler (PacketType.SetAlwaysRun, HandleSetAlwaysRun, false);
            AddLocalPacketHandler (PacketType.CompleteAgentMovement, HandleCompleteAgentMovement);
            AddLocalPacketHandler (PacketType.AgentAnimation, HandleAgentAnimation, false);
            AddLocalPacketHandler (PacketType.AgentRequestSit, HandleAgentRequestSit);
            AddLocalPacketHandler (PacketType.AgentSit, HandleAgentSit);
            AddLocalPacketHandler (PacketType.SoundTrigger, HandleSoundTrigger);
            AddLocalPacketHandler (PacketType.AvatarPickerRequest, HandleAvatarPickerRequest);
            AddLocalPacketHandler (PacketType.AgentDataUpdateRequest, HandleAgentDataUpdateRequest);
            AddLocalPacketHandler (PacketType.UserInfoRequest, HandleUserInfoRequest);
            AddLocalPacketHandler (PacketType.UpdateUserInfo, HandleUpdateUserInfo);
            AddLocalPacketHandler (PacketType.SetStartLocationRequest, HandleSetStartLocationRequest);
            AddLocalPacketHandler (PacketType.AgentThrottle, HandleAgentThrottle, false);
            AddLocalPacketHandler (PacketType.AgentPause, HandleAgentPause, false);
            AddLocalPacketHandler (PacketType.AgentResume, HandleAgentResume, false);
            AddLocalPacketHandler (PacketType.ForceScriptControlRelease, HandleForceScriptControlRelease);
            AddLocalPacketHandler (PacketType.ObjectLink, HandleObjectLink, true);
            AddLocalPacketHandler (PacketType.ObjectDelink, HandleObjectDelink, true);
            AddLocalPacketHandler (PacketType.ObjectAdd, HandleObjectAdd);
            AddLocalPacketHandler (PacketType.ObjectShape, HandleObjectShape);
            AddLocalPacketHandler (PacketType.ObjectExtraParams, HandleObjectExtraParams);
            AddLocalPacketHandler (PacketType.ObjectDuplicate, HandleObjectDuplicate);
            AddLocalPacketHandler (PacketType.RequestMultipleObjects, HandleRequestMultipleObjects);
            AddLocalPacketHandler (PacketType.ObjectSelect, HandleObjectSelect);
            AddLocalPacketHandler (PacketType.ObjectDeselect, HandleObjectDeselect);
            AddLocalPacketHandler (PacketType.ObjectPosition, HandleObjectPosition);
            AddLocalPacketHandler (PacketType.ObjectScale, HandleObjectScale);
            AddLocalPacketHandler (PacketType.ObjectRotation, HandleObjectRotation);
            AddLocalPacketHandler (PacketType.ObjectFlagUpdate, HandleObjectFlagUpdate);

            // Handle ObjectImage (TextureEntry) updates synchronously, since when updating multiple prim faces at once,
            // some clients will send out a separate ObjectImage packet for each face
            AddLocalPacketHandler (PacketType.ObjectImage, HandleObjectImage, false);

            AddLocalPacketHandler (PacketType.ObjectGrab, HandleObjectGrab, false);
            AddLocalPacketHandler (PacketType.ObjectGrabUpdate, HandleObjectGrabUpdate, false);
            AddLocalPacketHandler (PacketType.ObjectDeGrab, HandleObjectDeGrab);
            AddLocalPacketHandler (PacketType.ObjectSpinStart, HandleObjectSpinStart, false);
            AddLocalPacketHandler (PacketType.ObjectSpinUpdate, HandleObjectSpinUpdate, false);
            AddLocalPacketHandler (PacketType.ObjectSpinStop, HandleObjectSpinStop, false);
            AddLocalPacketHandler (PacketType.ObjectDescription, HandleObjectDescription, false);
            AddLocalPacketHandler (PacketType.ObjectName, HandleObjectName, false);
            AddLocalPacketHandler (PacketType.ObjectPermissions, HandleObjectPermissions, false);
            AddLocalPacketHandler (PacketType.Undo, HandleUndo, false);
            AddLocalPacketHandler (PacketType.UndoLand, HandleLandUndo, false);
            AddLocalPacketHandler (PacketType.Redo, HandleRedo, false);
            AddLocalPacketHandler (PacketType.ObjectDuplicateOnRay, HandleObjectDuplicateOnRay);
            AddLocalPacketHandler (PacketType.RequestObjectPropertiesFamily, HandleRequestObjectPropertiesFamily, false);
            AddLocalPacketHandler (PacketType.ObjectIncludeInSearch, HandleObjectIncludeInSearch);
            AddLocalPacketHandler (PacketType.ScriptAnswerYes, HandleScriptAnswerYes, false);
            AddLocalPacketHandler (PacketType.ObjectClickAction, HandleObjectClickAction, false);
            AddLocalPacketHandler (PacketType.ObjectMaterial, HandleObjectMaterial, false);
            AddLocalPacketHandler (PacketType.RequestImage, HandleRequestImage);
            AddLocalPacketHandler (PacketType.TransferRequest, HandleTransferRequest);
            AddLocalPacketHandler (PacketType.AssetUploadRequest, HandleAssetUploadRequest);
            AddLocalPacketHandler (PacketType.RequestXfer, HandleRequestXfer);
            AddLocalPacketHandler (PacketType.SendXferPacket, HandleSendXferPacket);
            AddLocalPacketHandler (PacketType.ConfirmXferPacket, HandleConfirmXferPacket);
            AddLocalPacketHandler (PacketType.AbortXfer, HandleAbortXfer);
            AddLocalPacketHandler (PacketType.CreateInventoryFolder, HandleCreateInventoryFolder);
            AddLocalPacketHandler (PacketType.UpdateInventoryFolder, HandleUpdateInventoryFolder);
            AddLocalPacketHandler (PacketType.MoveInventoryFolder, HandleMoveInventoryFolder, true);
            AddLocalPacketHandler (PacketType.CreateInventoryItem, HandleCreateInventoryItem);
            AddLocalPacketHandler (PacketType.LinkInventoryItem, HandleLinkInventoryItem);
            if (m_allowUDPInv) {
                AddLocalPacketHandler (PacketType.FetchInventory, HandleFetchInventory);
                AddLocalPacketHandler (PacketType.FetchInventoryDescendents, HandleFetchInventoryDescendents);
            }
            AddLocalPacketHandler (PacketType.PurgeInventoryDescendents, HandlePurgeInventoryDescendents, true);
            AddLocalPacketHandler (PacketType.UpdateInventoryItem, HandleUpdateInventoryItem, true);
            AddLocalPacketHandler (PacketType.CopyInventoryItem, HandleCopyInventoryItem, true);
            AddLocalPacketHandler (PacketType.MoveInventoryItem, HandleMoveInventoryItem, true);
            AddLocalPacketHandler (PacketType.ChangeInventoryItemFlags, HandleChangeInventoryItemFlags);
            AddLocalPacketHandler (PacketType.RemoveInventoryItem, HandleRemoveInventoryItem, true);
            AddLocalPacketHandler (PacketType.RemoveInventoryFolder, HandleRemoveInventoryFolder, true);
            AddLocalPacketHandler (PacketType.RemoveInventoryObjects, HandleRemoveInventoryObjects, true);
            AddLocalPacketHandler (PacketType.RequestTaskInventory, HandleRequestTaskInventory);
            AddLocalPacketHandler (PacketType.UpdateTaskInventory, HandleUpdateTaskInventory);
            AddLocalPacketHandler (PacketType.RemoveTaskInventory, HandleRemoveTaskInventory);
            AddLocalPacketHandler (PacketType.MoveTaskInventory, HandleMoveTaskInventory);
            AddLocalPacketHandler (PacketType.RezScript, HandleRezScript);
            AddLocalPacketHandler (PacketType.MapLayerRequest, HandleMapLayerRequest, false);
            AddLocalPacketHandler (PacketType.MapBlockRequest, HandleMapBlockRequest, false);
            AddLocalPacketHandler (PacketType.MapNameRequest, HandleMapNameRequest, false);
            AddLocalPacketHandler (PacketType.TeleportLandmarkRequest, HandleTeleportLandmarkRequest);
            AddLocalPacketHandler (PacketType.TeleportLocationRequest, HandleTeleportLocationRequest);
            AddLocalPacketHandler (PacketType.UUIDNameRequest, HandleUUIDNameRequest, false);
            AddLocalPacketHandler (PacketType.RegionHandleRequest, HandleRegionHandleRequest);
            AddLocalPacketHandler (PacketType.ParcelInfoRequest, HandleParcelInfoRequest, false);
            AddLocalPacketHandler (PacketType.ParcelAccessListRequest, HandleParcelAccessListRequest, false);
            AddLocalPacketHandler (PacketType.ParcelAccessListUpdate, HandleParcelAccessListUpdate, false);
            AddLocalPacketHandler (PacketType.ParcelPropertiesRequest, HandleParcelPropertiesRequest, false);
            AddLocalPacketHandler (PacketType.ParcelDivide, HandleParcelDivide);
            AddLocalPacketHandler (PacketType.ParcelJoin, HandleParcelJoin);
            AddLocalPacketHandler (PacketType.ParcelPropertiesUpdate, HandleParcelPropertiesUpdate);
            AddLocalPacketHandler (PacketType.ParcelSelectObjects, HandleParcelSelectObjects);
            AddLocalPacketHandler (PacketType.ParcelObjectOwnersRequest, HandleParcelObjectOwnersRequest);
            AddLocalPacketHandler (PacketType.ParcelGodForceOwner, HandleParcelGodForceOwner);
            AddLocalPacketHandler (PacketType.ParcelRelease, HandleParcelRelease);
            AddLocalPacketHandler (PacketType.ParcelReclaim, HandleParcelReclaim);
            AddLocalPacketHandler (PacketType.ParcelReturnObjects, HandleParcelReturnObjects);
            AddLocalPacketHandler (PacketType.ParcelSetOtherCleanTime, HandleParcelSetOtherCleanTime);
            AddLocalPacketHandler (PacketType.LandStatRequest, HandleLandStatRequest);
            AddLocalPacketHandler (PacketType.ParcelDwellRequest, HandleParcelDwellRequest);
            AddLocalPacketHandler (PacketType.EstateOwnerMessage, HandleEstateOwnerMessage);
            AddLocalPacketHandler (PacketType.RequestRegionInfo, HandleRequestRegionInfo, false);
            AddLocalPacketHandler (PacketType.EstateCovenantRequest, HandleEstateCovenantRequest);
            AddLocalPacketHandler (PacketType.RequestGodlikePowers, HandleRequestGodlikePowers);
            AddLocalPacketHandler (PacketType.GodKickUser, HandleGodKickUser);
            AddLocalPacketHandler (PacketType.MoneyBalanceRequest, HandleMoneyBalanceRequest);
            AddLocalPacketHandler (PacketType.EconomyDataRequest, HandleEconomyDataRequest);
            AddLocalPacketHandler (PacketType.RequestPayPrice, HandleRequestPayPrice);
            AddLocalPacketHandler (PacketType.ObjectSaleInfo, HandleObjectSaleInfo);
            AddLocalPacketHandler (PacketType.ObjectBuy, HandleObjectBuy);
            AddLocalPacketHandler (PacketType.GetScriptRunning, HandleGetScriptRunning);
            AddLocalPacketHandler (PacketType.SetScriptRunning, HandleSetScriptRunning);
            AddLocalPacketHandler (PacketType.ScriptReset, HandleScriptReset);
            AddLocalPacketHandler (PacketType.ActivateGestures, HandleActivateGestures);
            AddLocalPacketHandler (PacketType.DeactivateGestures, HandleDeactivateGestures);
            AddLocalPacketHandler (PacketType.ObjectOwner, HandleObjectOwner);
            AddLocalPacketHandler (PacketType.AgentFOV, HandleAgentFOV, false);
            AddLocalPacketHandler (PacketType.ViewerStats, HandleViewerStats);
            AddLocalPacketHandler (PacketType.MapItemRequest, HandleMapItemRequest, true);
            AddLocalPacketHandler (PacketType.TransferAbort, HandleTransferAbort, false);
            AddLocalPacketHandler (PacketType.MuteListRequest, HandleMuteListRequest, true);
            AddLocalPacketHandler (PacketType.UseCircuitCode, HandleUseCircuitCode);
            AddLocalPacketHandler (PacketType.AgentHeightWidth, HandleAgentHeightWidth, false);
            AddLocalPacketHandler (PacketType.DirPlacesQuery, HandleDirPlacesQuery);
            AddLocalPacketHandler (PacketType.DirFindQuery, HandleDirFindQuery);
            AddLocalPacketHandler (PacketType.DirLandQuery, HandleDirLandQuery);
            AddLocalPacketHandler (PacketType.DirPopularQuery, HandleDirPopularQuery);
            AddLocalPacketHandler (PacketType.DirClassifiedQuery, HandleDirClassifiedQuery);
            AddLocalPacketHandler (PacketType.EventInfoRequest, HandleEventInfoRequest);
            AddLocalPacketHandler (PacketType.OfferCallingCard, HandleOfferCallingCard);
            AddLocalPacketHandler (PacketType.AcceptCallingCard, HandleAcceptCallingCard);
            AddLocalPacketHandler (PacketType.DeclineCallingCard, HandleDeclineCallingCard);
            AddLocalPacketHandler (PacketType.ActivateGroup, HandleActivateGroup);
            AddLocalPacketHandler (PacketType.GroupTitlesRequest, HandleGroupTitlesRequest);
            AddLocalPacketHandler (PacketType.GroupProfileRequest, HandleGroupProfileRequest);
            AddLocalPacketHandler (PacketType.GroupMembersRequest, HandleGroupMembersRequest);
            AddLocalPacketHandler (PacketType.GroupRoleDataRequest, HandleGroupRoleDataRequest);
            AddLocalPacketHandler (PacketType.GroupRoleMembersRequest, HandleGroupRoleMembersRequest);
            AddLocalPacketHandler (PacketType.CreateGroupRequest, HandleCreateGroupRequest);
            AddLocalPacketHandler (PacketType.UpdateGroupInfo, HandleUpdateGroupInfo);
            AddLocalPacketHandler (PacketType.SetGroupAcceptNotices, HandleSetGroupAcceptNotices);
            AddLocalPacketHandler (PacketType.GroupTitleUpdate, HandleGroupTitleUpdate);
            AddLocalPacketHandler (PacketType.ParcelDeedToGroup, HandleParcelDeedToGroup);
            AddLocalPacketHandler (PacketType.GroupNoticesListRequest, HandleGroupNoticesListRequest);
            AddLocalPacketHandler (PacketType.GroupNoticeRequest, HandleGroupNoticeRequest);
            AddLocalPacketHandler (PacketType.GroupRoleUpdate, HandleGroupRoleUpdate);
            AddLocalPacketHandler (PacketType.GroupRoleChanges, HandleGroupRoleChanges);
            AddLocalPacketHandler (PacketType.JoinGroupRequest, HandleJoinGroupRequest);
            AddLocalPacketHandler (PacketType.LeaveGroupRequest, HandleLeaveGroupRequest);
            AddLocalPacketHandler (PacketType.EjectGroupMemberRequest, HandleEjectGroupMemberRequest);
            AddLocalPacketHandler (PacketType.InviteGroupRequest, HandleInviteGroupRequest);
            AddLocalPacketHandler (PacketType.StartLure, HandleStartLure);
            AddLocalPacketHandler (PacketType.TeleportLureRequest, HandleTeleportLureRequest);
            AddLocalPacketHandler (PacketType.ClassifiedInfoRequest, HandleClassifiedInfoRequest);
            AddLocalPacketHandler (PacketType.ClassifiedInfoUpdate, HandleClassifiedInfoUpdate);
            AddLocalPacketHandler (PacketType.ClassifiedDelete, HandleClassifiedDelete);
            AddLocalPacketHandler (PacketType.ClassifiedGodDelete, HandleClassifiedGodDelete);
            AddLocalPacketHandler (PacketType.EventGodDelete, HandleEventGodDelete);
            AddLocalPacketHandler (PacketType.EventNotificationAddRequest, HandleEventNotificationAddRequest);
            AddLocalPacketHandler (PacketType.EventNotificationRemoveRequest, HandleEventNotificationRemoveRequest);
            AddLocalPacketHandler (PacketType.RetrieveInstantMessages, HandleRetrieveInstantMessages);
            AddLocalPacketHandler (PacketType.PickDelete, HandlePickDelete);
            AddLocalPacketHandler (PacketType.PickGodDelete, HandlePickGodDelete);
            AddLocalPacketHandler (PacketType.PickInfoUpdate, HandlePickInfoUpdate);
            AddLocalPacketHandler (PacketType.AvatarNotesUpdate, HandleAvatarNotesUpdate);
            AddLocalPacketHandler (PacketType.AvatarInterestsUpdate, HandleAvatarInterestsUpdate);
            AddLocalPacketHandler (PacketType.GrantUserRights, HandleGrantUserRights);
            AddLocalPacketHandler (PacketType.PlacesQuery, HandlePlacesQuery);
            AddLocalPacketHandler (PacketType.UpdateMuteListEntry, HandleUpdateMuteListEntry);
            AddLocalPacketHandler (PacketType.RemoveMuteListEntry, HandleRemoveMuteListEntry);
            AddLocalPacketHandler (PacketType.UserReport, HandleUserReport);
            AddLocalPacketHandler (PacketType.FindAgent, HandleFindAgent);
            AddLocalPacketHandler (PacketType.TrackAgent, HandleTrackAgent);
            AddLocalPacketHandler (PacketType.GodUpdateRegionInfo, HandleGodUpdateRegionInfoUpdate);
            AddLocalPacketHandler (PacketType.GodlikeMessage, HandleGodlikeMessage);
            AddLocalPacketHandler (PacketType.StateSave, HandleSaveStatePacket);
            AddLocalPacketHandler (PacketType.GroupAccountDetailsRequest, HandleGroupAccountDetailsRequest);
            AddLocalPacketHandler (PacketType.GroupAccountSummaryRequest, HandleGroupAccountSummaryRequest);
            AddLocalPacketHandler (PacketType.GroupAccountTransactionsRequest, HandleGroupTransactionsDetailsRequest);
            AddLocalPacketHandler (PacketType.FreezeUser, HandleFreezeUser);
            AddLocalPacketHandler (PacketType.EjectUser, HandleEjectUser);
            AddLocalPacketHandler (PacketType.ParcelBuyPass, HandleParcelBuyPass);
            AddLocalPacketHandler (PacketType.ParcelGodMarkAsContent, HandleParcelGodMarkAsContent);
            AddLocalPacketHandler (PacketType.GroupActiveProposalsRequest, HandleGroupActiveProposalsRequest);
            AddLocalPacketHandler (PacketType.GroupVoteHistoryRequest, HandleGroupVoteHistoryRequest);
            AddLocalPacketHandler (PacketType.GroupProposalBallot, HandleGroupProposalBallot);
            AddLocalPacketHandler (PacketType.SimWideDeletes, HandleSimWideDeletes);
            AddLocalPacketHandler (PacketType.SendPostcard, HandleSendPostcard);
            AddLocalPacketHandler (PacketType.TeleportCancel, HandleTeleportCancel);
            AddLocalPacketHandler (PacketType.ViewerStartAuction, HandleViewerStartAuction);
            AddLocalPacketHandler (PacketType.ParcelDisableObjects, HandleParcelDisableObjects);
            AddLocalPacketHandler (PacketType.VelocityInterpolateOn, HandleVelocityInterpolate);
            AddLocalPacketHandler (PacketType.VelocityInterpolateOff, HandleVelocityInterpolate);
        }

        #endregion Local Packet Handlers

        #region PriorityQueue

        public struct PacketProcessor
        {
            public bool Async;
            public PacketMethod method;
        }

        public class AsyncPacketProcess
        {
            public readonly LLClientView ClientView;
            public readonly PacketMethod Method;
            public readonly Packet Pack;
            public bool result;

            public AsyncPacketProcess (LLClientView pClientview, PacketMethod pMethod, Packet pPack)
            {
                ClientView = pClientview;
                Method = pMethod;
                Pack = pPack;
            }
        }

        #endregion

        #region Outpacket
        /// <summary>
        ///     Sets the throttles from values supplied by the client
        /// </summary>
        /// <param name="throttles"></param>
        public void SetChildAgentThrottle (byte [] throttles)
        {
            m_udpClient.SetThrottles (throttles);
        }

        /// <summary>
        ///     Get the current throttles for this client as a packed byte array
        /// </summary>
        /// <param name="multiplier">Unused</param>
        /// <returns></returns>
        public byte [] GetThrottlesPacked (float multiplier)
        {
            return m_udpClient.GetThrottlesPacked (multiplier);
        }

        /// <summary>
        ///     This is the starting point for sending a simulator packet out to the client
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <param name="throttlePacketType">Throttling category for the packet</param>
        void OutPacket (Packet packet, ThrottleOutPacketType throttlePacketType)
        {
            #region BinaryStats

            LLUDPServer.LogPacketHeader (false, m_circuitCode, 0, packet.Type, (ushort)packet.Length);

            #endregion BinaryStats

            OutPacket (packet, throttlePacketType, true);
        }

        /// <summary>
        ///     This is the starting point for sending a simulator packet out to the client
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <param name="throttlePacketType">Throttling category for the packet</param>
        /// <param name="doAutomaticSplitting">
        ///     True to automatically split oversized
        ///     packets (the default), or false to disable splitting if the calling code
        ///     handles splitting manually
        /// </param>
        void OutPacket (Packet packet, ThrottleOutPacketType throttlePacketType, bool doAutomaticSplitting)
        {
            OutPacket (packet, throttlePacketType, doAutomaticSplitting, null);
        }

        /// <summary>
        ///     This is the starting point for sending a simulator packet out to the client
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <param name="throttlePacketType">Throttling category for the packet</param>
        /// <param name="doAutomaticSplitting">
        ///     True to automatically split oversized
        ///     packets (the default), or false to disable splitting if the calling code
        ///     handles splitting manually
        /// </param>
        /// <param name="resendMethod">Method that will be called if the packet needs resent</param>
        void OutPacket (Packet packet, ThrottleOutPacketType throttlePacketType, bool doAutomaticSplitting,
                               UnackedPacketMethod resendMethod)
        {
            OutPacket (packet, throttlePacketType, doAutomaticSplitting, resendMethod, null);
        }

        /// <summary>
        ///     This is the starting point for sending a simulator packet out to the client
        /// </summary>
        /// <param name="pack">Packet to send</param>
        /// <param name="throttlePacketType">Throttling category for the packet</param>
        /// <param name="doAutomaticSplitting">
        ///     True to automatically split oversized
        ///     packets (the default), or false to disable splitting if the calling code
        ///     handles splitting manually
        /// </param>
        /// <param name="resendMethod">Method that will be called if the packet needs resent</param>
        /// <param name="finishedMethod">Method that will be called when the packet is sent</param>
        void OutPacket (Packet pack, ThrottleOutPacketType throttlePacketType, bool doAutomaticSplitting,
                               UnackedPacketMethod resendMethod, UnackedPacketMethod finishedMethod)
        {
            if (m_debugPacketLevel > 0 || m_debugPackets.Contains (pack.Type.ToString ())) {
                bool outputPacket = true;

                if (m_debugPacketLevel <= 255
                    && (pack.Type == PacketType.SimStats || pack.Type == PacketType.SimulatorViewerTimeMessage))
                    outputPacket = false;

                if (m_debugPacketLevel <= 200
                    && (pack.Type == PacketType.ImagePacket
                        || pack.Type == PacketType.ImageData
                        || pack.Type == PacketType.LayerData
                        || pack.Type == PacketType.CoarseLocationUpdate))
                    outputPacket = false;

                if (m_debugPacketLevel <= 100
                    && (pack.Type == PacketType.AvatarAnimation
                        || pack.Type == PacketType.ViewerEffect))
                    outputPacket = false;

                if (m_debugPacketLevel <= 50
                    && (pack.Type == PacketType.ImprovedTerseObjectUpdate
                        || pack.Type == PacketType.ObjectUpdate))
                    outputPacket = false;

                if (m_debugPacketLevel <= 25
                    && pack.Type == PacketType.ObjectPropertiesFamily)
                    outputPacket = false;

                if (m_debugPackets.Contains (pack.Type.ToString ()))
                    outputPacket = true;

                if (outputPacket && !m_debugRemovePackets.Contains (pack.Type.ToString ()))
                    MainConsole.Instance.DebugFormat ("[CLIENT ({1})]: Packet OUT {0}", pack.Type, Name);
            }

            m_udpServer.SendPacket (m_udpClient, pack, throttlePacketType, doAutomaticSplitting, resendMethod,
                                   finishedMethod);
        }

        #endregion Outpacket

        #region InPacket
        /// <summary>
        ///     Entryway from the client to the simulator.  All UDP packets from the client will end up here
        /// </summary>
        /// <param name="pack">OpenMetaverse.packet</param>
        public void ProcessInPacket (Packet pack)
        {
            if (m_debugPacketLevel > 0 || m_debugPackets.Contains (pack.Type.ToString ())) {
                bool outputPacket = true;

                if (m_debugPacketLevel <= 255 && pack.Type == PacketType.AgentUpdate)
                    outputPacket = false;

                if (m_debugPacketLevel <= 200 && pack.Type == PacketType.RequestImage)
                    outputPacket = false;

                if (m_debugPacketLevel <= 100 &&
                    (pack.Type == PacketType.ViewerEffect || pack.Type == PacketType.AgentAnimation))
                    outputPacket = false;

                if (m_debugPackets.Contains (pack.Type.ToString ()))
                    outputPacket = true;

                if (outputPacket && !m_debugRemovePackets.Contains (pack.Type.ToString ()))
                    MainConsole.Instance.DebugFormat ("[CLIENT ({1})]: Packet IN {0}", pack.Type, Name);
            }

            if (!ProcessPacketMethod (pack))
                MainConsole.Instance.Warn ("[Client]: unhandled packet " + pack.Type);

            //Give the packet back to the pool now, we've processed it
            PacketPool.Instance.ReturnPacket (pack);
        }

        #endregion
    }

}
