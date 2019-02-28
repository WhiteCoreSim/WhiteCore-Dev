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
using OpenMetaverse.Packets;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
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

        #region Inventory/Asset/Other related packets

        bool HandleRequestImage (IClientAPI sender, Packet pack)
        {
            var imageRequest = (RequestImagePacket)pack;
            //MainConsole.Instance.Debug("image request: " + Pack.ToString());

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (imageRequest.AgentData.SessionID != SessionId ||
                    imageRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            //handlerTextureRequest = null;
            foreach (RequestImagePacket.RequestImageBlock t in imageRequest.RequestImage) {
                TextureRequestArgs args = new TextureRequestArgs ();

                RequestImagePacket.RequestImageBlock block = t;

                args.RequestedAssetID = block.Image;
                args.DiscardLevel = block.DiscardLevel;
                args.PacketNumber = block.Packet;
                args.Priority = block.DownloadPriority;
                args.requestSequence = imageRequest.Header.Sequence;

                // NOTE: This is not a built in part of the LLUDP protocol, but we double the
                // priority of avatar textures to get avatars rezzing in faster than the
                // surrounding scene
                if ((ImageType)block.Type == ImageType.Baked)
                    args.Priority *= 2.0f;

                // in the end, we null this, so we have to check if it's null
                if (m_imageManager != null) {
                    m_imageManager.EnqueueReq (args);
                }
            }
            return true;
        }

        /// <summary>
        ///     This is the entry point for the UDP route by which the client can retrieve asset data.  If the request
        ///     is successful then a TransferInfo packet will be sent back, followed by one or more TransferPackets
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pack"></param>
        /// <returns>This parameter may be ignored since we appear to return true whatever happens</returns>
        bool HandleTransferRequest (IClientAPI sender, Packet pack)
        {
            //MainConsole.Instance.Debug("ClientView.ProcessPackets.cs:ProcessInPacket() - Got transfer request");

            var transfer = (TransferRequestPacket)pack;
            //MainConsole.Instance.Debug("Transfer Request: " + transfer.ToString());
            // Validate inventory transfers
            // Has to be done here, because AssetCache can't do it
            //
            UUID taskID = UUID.Zero;
            if (transfer.TransferInfo.SourceType == (int)SourceType.SimInventoryItem) {
                taskID = new UUID (transfer.TransferInfo.Params, 48);
                UUID itemID = new UUID (transfer.TransferInfo.Params, 64);
                UUID requestID = new UUID (transfer.TransferInfo.Params, 80);

                //                MainConsole.Instance.DebugFormat(
                //                    "[Client]: Got request for asset {0} from item {1} in prim {2} by {3}",
                //                    requestID, itemID, taskID, Name);

                if (!m_scene.Permissions.BypassPermissions ()) {
                    if (taskID != UUID.Zero) // Prim
                    {
                        ISceneChildEntity part = m_scene.GetSceneObjectPart (taskID);

                        if (part == null) {
                            MainConsole.Instance.WarnFormat (
                                "[Client]: {0} requested asset {1} from item {2} in prim {3} but prim does not exist",
                                Name, requestID, itemID, taskID);
                            return true;
                        }

                        TaskInventoryItem tii = part.Inventory.GetInventoryItem (itemID);
                        if (tii == null) {
                            MainConsole.Instance.WarnFormat (
                                "[Client]: {0} requested asset {1} from item {2} in prim {3} but item does not exist",
                                Name, requestID, itemID, taskID);
                            return true;
                        }

                        if (tii.Type == (int)AssetType.LSLText) {
                            if (!m_scene.Permissions.CanEditScript (itemID, taskID, AgentId))
                                return true;
                        } else if (tii.Type == (int)AssetType.Notecard) {
                            if (!m_scene.Permissions.CanEditNotecard (itemID, taskID, AgentId))
                                return true;
                        } else {
                            if (!m_scene.Permissions.CanEditObjectInventory (part.UUID, AgentId)) {
                                MainConsole.Instance.Warn (
                                    "[Client]: Permissions check for CanEditObjectInventory fell through to standard code!");

                                if (part.OwnerID != AgentId) {
                                    MainConsole.Instance.WarnFormat (
                                        "[Client]: {0} requested asset {1} from item {2} in prim {3} but the prim is owned by {4}",
                                        Name, requestID, itemID, taskID, part.OwnerID);
                                    return true;
                                }

                                if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0) {
                                    MainConsole.Instance.WarnFormat (
                                        "[Client]: {0} requested asset {1} from item {2} in prim {3} but modify permissions are not set",
                                        Name, requestID, itemID, taskID);
                                    return true;
                                }

                                if (tii.OwnerID != AgentId) {
                                    MainConsole.Instance.WarnFormat (
                                        "[Client]: {0} requested asset {1} from item {2} in prim {3} but the item is owned by {4}",
                                        Name, requestID, itemID, taskID, tii.OwnerID);
                                    return true;
                                }

                                if ((tii.CurrentPermissions &
                                    ((uint)PermissionMask.Modify |
                                     (uint)PermissionMask.Copy |
                                     (uint)PermissionMask.Transfer)) != ((uint)PermissionMask.Modify |
                                                                         (uint)PermissionMask.Copy |
                                                                         (uint)PermissionMask.Transfer)) {
                                    MainConsole.Instance.WarnFormat (
                                        "[Client]: {0} requested asset {1} from item {2} in prim {3} but item permissions are not modify/copy/transfer",
                                        Name, requestID, itemID, taskID);
                                    return true;
                                }

                                if (tii.AssetID != requestID) {
                                    MainConsole.Instance.WarnFormat (
                                        "[Client]: {0} requested asset {1} from item {2} in prim {3} but this does not match item's asset {4}",
                                        Name, requestID, itemID, taskID, tii.AssetID);
                                    return true;
                                }
                            }
                        }
                    } else // Agent
                      {
                        IInventoryAccessModule invAccess = m_scene.RequestModuleInterface<IInventoryAccessModule> ();
                        if (invAccess != null) {
                            if (!invAccess.GetAgentInventoryItem (this, itemID, requestID))
                                return false;
                        } else
                            return false;
                    }
                }
            }

            MakeAssetRequest (transfer, taskID);

            return true;
        }

        bool HandleAssetUploadRequest (IClientAPI sender, Packet pack)
        {
            var request = (AssetUploadRequestPacket)pack;


            // MainConsole.Instance.Debug("upload request " + request.ToString());
            // MainConsole.Instance.Debug("upload request was for assetid: " + request.AssetBlock.TransactionID.Combine(this.SecureSessionId).ToString());
            UUID temp = UUID.Combine (request.AssetBlock.TransactionID, SecureSessionId);

            UDPAssetUploadRequest handlerAssetUploadRequest = OnAssetUploadRequest;

            if (handlerAssetUploadRequest != null) {
                handlerAssetUploadRequest (this,
                                           temp,
                                           request.AssetBlock.TransactionID,
                                           request.AssetBlock.Type,
                                           request.AssetBlock.AssetData,
                                           request.AssetBlock.StoreLocal,
                                           request.AssetBlock.Tempfile);
            }
            return true;
        }

        bool HandleRequestXfer (IClientAPI sender, Packet pack)
        {
            var xferReq = (RequestXferPacket)pack;

            RequestXfer handlerRequestXfer = OnRequestXfer;

            if (handlerRequestXfer != null) {
                handlerRequestXfer (this, xferReq.XferID.ID, Util.FieldToString (xferReq.XferID.Filename));
            }
            return true;
        }

        bool HandleSendXferPacket (IClientAPI sender, Packet pack)
        {
            var xferRec = (SendXferPacketPacket)pack;

            XferReceive handlerXferReceive = OnXferReceive;
            if (handlerXferReceive != null) {
                handlerXferReceive (this, xferRec.XferID.ID, xferRec.XferID.Packet, xferRec.DataPacket.Data);
            }
            return true;
        }

        bool HandleConfirmXferPacket (IClientAPI sender, Packet pack)
        {
            var confirmXfer = (ConfirmXferPacketPacket)pack;

            ConfirmXfer handlerConfirmXfer = OnConfirmXfer;
            if (handlerConfirmXfer != null) {
                handlerConfirmXfer (this, confirmXfer.XferID.ID, confirmXfer.XferID.Packet);
            }
            return true;
        }

        bool HandleAbortXfer (IClientAPI sender, Packet pack)
        {
            var abortXfer = (AbortXferPacket)pack;
            AbortXfer handlerAbortXfer = OnAbortXfer;
            if (handlerAbortXfer != null) {
                handlerAbortXfer (this, abortXfer.XferID.ID);
            }

            return true;
        }

        public void SendAbortXferPacket (ulong xferID)
        {
            AbortXferPacket xferItem = (AbortXferPacket)PacketPool.Instance.GetPacket (PacketType.AbortXfer);
            xferItem.XferID.ID = xferID;
            OutPacket (xferItem, ThrottleOutPacketType.Transfer);
        }

        bool HandleCreateInventoryFolder (IClientAPI sender, Packet pack)
        {
            var invFolder = (CreateInventoryFolderPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (invFolder.AgentData.SessionID != SessionId ||
                    invFolder.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            CreateInventoryFolder handlerCreateInventoryFolder = OnCreateNewInventoryFolder;
            if (handlerCreateInventoryFolder != null) {
                handlerCreateInventoryFolder (this,
                                              invFolder.FolderData.FolderID,
                                             (ushort)invFolder.FolderData.Type,
                                             Util.FieldToString (invFolder.FolderData.Name),
                                             invFolder.FolderData.ParentID);
            }

            return true;
        }

        bool HandleUpdateInventoryFolder (IClientAPI sender, Packet pack)
        {
            if (OnUpdateInventoryFolder != null) {
                var invFolderx = (UpdateInventoryFolderPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (invFolderx.AgentData.SessionID != SessionId ||
                        invFolderx.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                foreach (UpdateInventoryFolderPacket.FolderDataBlock t in from t in invFolderx.FolderData
                                                                          let handlerUpdateInventoryFolder = OnUpdateInventoryFolder
                                                                          where handlerUpdateInventoryFolder != null
                                                                          select t) {
                    OnUpdateInventoryFolder (this,
                                             t.FolderID,
                                             (ushort)t.Type,
                                             Util.FieldToString (t.Name),
                                             t.ParentID);
                }
            }
            return true;
        }

        bool HandleMoveInventoryFolder (IClientAPI sender, Packet pack)
        {
            if (OnMoveInventoryFolder != null) {
                var invFoldery = (MoveInventoryFolderPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (invFoldery.AgentData.SessionID != SessionId ||
                        invFoldery.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                foreach (MoveInventoryFolderPacket.InventoryDataBlock t in from t in invFoldery.InventoryData
                                                                           let handlerMoveInventoryFolder = OnMoveInventoryFolder
                                                                           where handlerMoveInventoryFolder != null
                                                                           select t) {
                    OnMoveInventoryFolder (this, t.FolderID, t.ParentID);
                }
            }
            return true;
        }

        bool HandleCreateInventoryItem (IClientAPI sender, Packet pack)
        {
            var createItem = (CreateInventoryItemPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (createItem.AgentData.SessionID != SessionId ||
                    createItem.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            CreateNewInventoryItem handlerCreateNewInventoryItem = OnCreateNewInventoryItem;
            if (handlerCreateNewInventoryItem != null) {
                handlerCreateNewInventoryItem (this,
                                               createItem.InventoryBlock.TransactionID,
                                               createItem.InventoryBlock.FolderID,
                                               createItem.InventoryBlock.CallbackID,
                                               Util.FieldToString (createItem.InventoryBlock.Description),
                                               Util.FieldToString (createItem.InventoryBlock.Name),
                                               createItem.InventoryBlock.InvType,
                                               createItem.InventoryBlock.Type,
                                               createItem.InventoryBlock.WearableType,
                                               createItem.InventoryBlock.NextOwnerMask,
                                               Util.UnixTimeSinceEpoch ());
            }

            return true;
        }

        bool HandleLinkInventoryItem (IClientAPI sender, Packet pack)
        {
            var createLink = (LinkInventoryItemPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (createLink.AgentData.SessionID != SessionId ||
                    createLink.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            LinkInventoryItem linkInventoryItem = OnLinkInventoryItem;

            if (linkInventoryItem != null) {
                linkInventoryItem (
                    this,
                    createLink.InventoryBlock.TransactionID,
                    createLink.InventoryBlock.FolderID,
                    createLink.InventoryBlock.CallbackID,
                    Util.FieldToString (createLink.InventoryBlock.Description),
                    Util.FieldToString (createLink.InventoryBlock.Name),
                    createLink.InventoryBlock.InvType,
                    createLink.InventoryBlock.Type,
                    createLink.InventoryBlock.OldItemID);
            }

            return true;
        }

        bool HandleFetchInventory (IClientAPI sender, Packet pack)
        {
            if (OnFetchInventory != null) {
                var FetchInventoryx = (FetchInventoryPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (FetchInventoryx.AgentData.SessionID != SessionId ||
                        FetchInventoryx.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                foreach (FetchInventoryPacket.InventoryDataBlock t in FetchInventoryx.InventoryData) {
                    FetchInventory handlerFetchInventory = OnFetchInventory;

                    if (handlerFetchInventory != null) {
                        OnFetchInventory (this, t.ItemID, t.OwnerID);
                    }
                }
            }
            return true;
        }

        bool HandleFetchInventoryDescendents (IClientAPI sender, Packet pack)
        {
            var Fetch = (FetchInventoryDescendentsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (Fetch.AgentData.SessionID != SessionId || 
                    Fetch.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            FetchInventoryDescendents handlerFetchInventoryDescendents = OnFetchInventoryDescendents;
            if (handlerFetchInventoryDescendents != null) {
                handlerFetchInventoryDescendents (this,
                                                  Fetch.InventoryData.FolderID,
                                                  Fetch.InventoryData.OwnerID,
                                                  Fetch.InventoryData.FetchFolders,
                                                  Fetch.InventoryData.FetchItems,
                                                  Fetch.InventoryData.SortOrder);
            }
            return true;
        }

        bool HandlePurgeInventoryDescendents (IClientAPI sender, Packet pack)
        {
            var Purge = (PurgeInventoryDescendentsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (Purge.AgentData.SessionID != SessionId ||
                    Purge.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            PurgeInventoryDescendents handlerPurgeInventoryDescendents = OnPurgeInventoryDescendents;
            if (handlerPurgeInventoryDescendents != null) {
                handlerPurgeInventoryDescendents (this, Purge.InventoryData.FolderID);
            }

            return true;
        }

        bool HandleUpdateInventoryItem (IClientAPI sender, Packet pack)
        {
            var inventoryItemUpdate = (UpdateInventoryItemPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (inventoryItemUpdate.AgentData.SessionID != SessionId ||
                    inventoryItemUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnUpdateInventoryItem != null) {
                foreach (UpdateInventoryItemPacket.InventoryDataBlock t in inventoryItemUpdate.InventoryData) {
                    UpdateInventoryItem handlerUpdateInventoryItem = OnUpdateInventoryItem;

                    if (handlerUpdateInventoryItem == null) continue;
                    InventoryItemBase itemUpd = new InventoryItemBase {
                        ID = t.ItemID,
                        Name = Util.FieldToString (t.Name),
                        Description = Util.FieldToString (t.Description),
                        GroupID = t.GroupID,
                        GroupOwned = t.GroupOwned,
                        GroupPermissions = t.GroupMask,
                        NextPermissions = t.NextOwnerMask,
                        EveryOnePermissions = t.EveryoneMask,
                        CreationDate = t.CreationDate,
                        Folder = t.FolderID,
                        InvType = t.InvType,
                        SalePrice = t.SalePrice,
                        SaleType = t.SaleType,
                        Flags = t.Flags
                    };

                    OnUpdateInventoryItem (this, t.TransactionID, t.ItemID, itemUpd);
                }
            }

            return true;
        }

        bool HandleCopyInventoryItem (IClientAPI sender, Packet pack)
        {
            var copyitem = (CopyInventoryItemPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (copyitem.AgentData.SessionID != SessionId || 
                    copyitem.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnCopyInventoryItem != null) {
                foreach (CopyInventoryItemPacket.InventoryDataBlock datablock in copyitem.InventoryData) {
                    CopyInventoryItem handlerCopyInventoryItem = OnCopyInventoryItem;
                    if (handlerCopyInventoryItem != null) {
                        handlerCopyInventoryItem (this,
                                                  datablock.CallbackID,
                                                  datablock.OldAgentID,
                                                  datablock.OldItemID,
                                                  datablock.NewFolderID,
                                                  Util.FieldToString (datablock.NewName));
                    }
                }
            }

            return true;
        }

        bool HandleMoveInventoryItem (IClientAPI sender, Packet pack)
        {
            var moveitem = (MoveInventoryItemPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (moveitem.AgentData.SessionID != SessionId || 
                    moveitem.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnMoveInventoryItem != null) {
                MoveInventoryItem handlerMoveInventoryItem = null;
                List<InventoryItemBase> items =
                    moveitem.InventoryData.Select (
                        datablock =>
                        new InventoryItemBase (datablock.ItemID, AgentId) {
                            Folder = datablock.FolderID,
                            Name = Util.FieldToString (datablock.NewName)
                        }).ToList ();
                
                handlerMoveInventoryItem = OnMoveInventoryItem;
                if (handlerMoveInventoryItem != null) {
                    handlerMoveInventoryItem (this, items);
                }
            }

            return true;
        }

        bool HandleChangeInventoryItemFlags (IClientAPI sender, Packet pack)
        {
            var inventoryItemUpdate = (ChangeInventoryItemFlagsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (inventoryItemUpdate.AgentData.SessionID != SessionId ||
                    inventoryItemUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnChangeInventoryItemFlags != null) {
                foreach (
                    ChangeInventoryItemFlagsPacket.InventoryDataBlock t in from t in inventoryItemUpdate.InventoryData
                                                                           let handlerUpdateInventoryItem =
                                                                               OnChangeInventoryItemFlags
                                                                           where handlerUpdateInventoryItem != null
                                                                           select t) {
                    OnChangeInventoryItemFlags (this, t.ItemID, t.Flags);
                }
            }

            return true;
        }

        bool HandleRemoveInventoryItem (IClientAPI sender, Packet pack)
        {
            var removeItem = (RemoveInventoryItemPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (removeItem.AgentData.SessionID != SessionId || 
                    removeItem.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnRemoveInventoryItem != null) {
                RemoveInventoryItem handlerRemoveInventoryItem = null;
                List<UUID> uuids = removeItem.InventoryData.Select (datablock => datablock.ItemID).ToList ();
                handlerRemoveInventoryItem = OnRemoveInventoryItem;
                if (handlerRemoveInventoryItem != null) {
                    handlerRemoveInventoryItem (this, uuids);
                }
            }
            return true;
        }

        bool HandleRemoveInventoryFolder (IClientAPI sender, Packet pack)
        {
            var removeFolder = (RemoveInventoryFolderPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (removeFolder.AgentData.SessionID != SessionId ||
                    removeFolder.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnRemoveInventoryFolder != null) {
                RemoveInventoryFolder handlerRemoveInventoryFolder = null;
                List<UUID> uuids = removeFolder.FolderData.Select (datablock => datablock.FolderID).ToList ();
                handlerRemoveInventoryFolder = OnRemoveInventoryFolder;
                if (handlerRemoveInventoryFolder != null) {
                    handlerRemoveInventoryFolder (this, uuids);
                }
            }

            return true;
        }

        bool HandleRemoveInventoryObjects (IClientAPI sender, Packet pack)
        {
            var removeObject = (RemoveInventoryObjectsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (removeObject.AgentData.SessionID != SessionId || 
                    removeObject.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnRemoveInventoryFolder != null) {
                RemoveInventoryFolder handlerRemoveInventoryFolder = null;
                List<UUID> uuids = removeObject.FolderData.Select (datablock => datablock.FolderID).ToList ();
                handlerRemoveInventoryFolder = OnRemoveInventoryFolder;
                if (handlerRemoveInventoryFolder != null) {
                    handlerRemoveInventoryFolder (this, uuids);
                }
            }

            if (OnRemoveInventoryItem != null) {
                RemoveInventoryItem handlerRemoveInventoryItem = null;
                List<UUID> uuids = removeObject.ItemData.Select (datablock => datablock.ItemID).ToList ();
                handlerRemoveInventoryItem = OnRemoveInventoryItem;
                if (handlerRemoveInventoryItem != null) {
                    handlerRemoveInventoryItem (this, uuids);
                }
            }

            return true;
        }

        bool HandleRequestTaskInventory (IClientAPI sender, Packet pack)
        {
            var requesttask = (RequestTaskInventoryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (requesttask.AgentData.SessionID != SessionId || 
                    requesttask.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RequestTaskInventory handlerRequestTaskInventory = OnRequestTaskInventory;
            if (handlerRequestTaskInventory != null) {
                handlerRequestTaskInventory (this, requesttask.InventoryData.LocalID);
            }
            return true;
        }

        bool HandleUpdateTaskInventory (IClientAPI sender, Packet pack)
        {
            var updatetask = (UpdateTaskInventoryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (updatetask.AgentData.SessionID != SessionId ||
                    updatetask.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnUpdateTaskInventory != null) {
                if (updatetask.UpdateData.Key == 0) {
                    UpdateTaskInventory handlerUpdateTaskInventory = OnUpdateTaskInventory;
                    if (handlerUpdateTaskInventory != null) {
                        TaskInventoryItem newTaskItem = new TaskInventoryItem {
                            ItemID = updatetask.InventoryData.ItemID,
                            ParentID = updatetask.InventoryData.FolderID,
                            CreatorID = updatetask.InventoryData.CreatorID,
                            OwnerID = updatetask.InventoryData.OwnerID,
                            GroupID = updatetask.InventoryData.GroupID,
                            BasePermissions = updatetask.InventoryData.BaseMask,
                            CurrentPermissions = updatetask.InventoryData.OwnerMask,
                            GroupPermissions = updatetask.InventoryData.GroupMask,
                            EveryonePermissions = updatetask.InventoryData.EveryoneMask,
                            NextPermissions = updatetask.InventoryData.NextOwnerMask,
                            Type = updatetask.InventoryData.Type,
                            InvType = updatetask.InventoryData.InvType,
                            Flags = updatetask.InventoryData.Flags,
                            SaleType = updatetask.InventoryData.SaleType,
                            SalePrice = updatetask.InventoryData.SalePrice,
                            Name = Util.FieldToString (updatetask.InventoryData.Name),
                            Description = Util.FieldToString (updatetask.InventoryData.Description),
                            CreationDate = (uint)updatetask.InventoryData.CreationDate
                        };

                        // Unused?  Clicking share with group sets GroupPermissions instead, so perhaps this is something
                        // different
                        //newTaskItem.GroupOwned=updatetask.InventoryData.GroupOwned;
                        handlerUpdateTaskInventory (this,
                                                    updatetask.InventoryData.TransactionID,
                                                    newTaskItem,
                                                    updatetask.UpdateData.LocalID);
                    }
                }
            }

            return true;
        }

        bool HandleRemoveTaskInventory (IClientAPI sender, Packet pack)
        {
            var removeTask = (RemoveTaskInventoryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (removeTask.AgentData.SessionID != SessionId ||
                    removeTask.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RemoveTaskInventory handlerRemoveTaskItem = OnRemoveTaskItem;

            if (handlerRemoveTaskItem != null) {
                handlerRemoveTaskItem (this, removeTask.InventoryData.ItemID, removeTask.InventoryData.LocalID);
            }

            return true;
        }

        bool HandleMoveTaskInventory (IClientAPI sender, Packet pack)
        {
            var moveTaskInventoryPacket = (MoveTaskInventoryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (moveTaskInventoryPacket.AgentData.SessionID != SessionId ||
                    moveTaskInventoryPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            MoveTaskInventory handlerMoveTaskItem = OnMoveTaskItem;

            if (handlerMoveTaskItem != null) {
                handlerMoveTaskItem (this,
                                     moveTaskInventoryPacket.AgentData.FolderID,
                                     moveTaskInventoryPacket.InventoryData.LocalID,
                                     moveTaskInventoryPacket.InventoryData.ItemID);
            }

            return true;
        }

        bool HandleRezScript (IClientAPI sender, Packet pack)
        {
            //MainConsole.Instance.Debug(Pack.ToString());
            var rezScriptx = (RezScriptPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (rezScriptx.AgentData.SessionID != SessionId ||
                    rezScriptx.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RezScript handlerRezScript = OnRezScript;
            InventoryItemBase item = new InventoryItemBase {
                ID = rezScriptx.InventoryBlock.ItemID,
                Folder = rezScriptx.InventoryBlock.FolderID,
                CreatorId = rezScriptx.InventoryBlock.CreatorID.ToString (),
                Owner = rezScriptx.InventoryBlock.OwnerID,
                BasePermissions = rezScriptx.InventoryBlock.BaseMask,
                CurrentPermissions = rezScriptx.InventoryBlock.OwnerMask,
                EveryOnePermissions = rezScriptx.InventoryBlock.EveryoneMask,
                NextPermissions = rezScriptx.InventoryBlock.NextOwnerMask,
                GroupPermissions = rezScriptx.InventoryBlock.GroupMask,
                GroupOwned = rezScriptx.InventoryBlock.GroupOwned,
                GroupID = rezScriptx.InventoryBlock.GroupID,
                AssetType = rezScriptx.InventoryBlock.Type,
                InvType = rezScriptx.InventoryBlock.InvType,
                Flags = rezScriptx.InventoryBlock.Flags,
                SaleType = rezScriptx.InventoryBlock.SaleType,
                SalePrice = rezScriptx.InventoryBlock.SalePrice,
                Name = Util.FieldToString (rezScriptx.InventoryBlock.Name),
                Description = Util.FieldToString (rezScriptx.InventoryBlock.Description),
                CreationDate = rezScriptx.InventoryBlock.CreationDate
            };

            if (handlerRezScript != null) {
                handlerRezScript (this,
                                  item,
                                  rezScriptx.InventoryBlock.TransactionID,
                                  rezScriptx.UpdateBlock.ObjectLocalID);
            }
            return true;
        }

        bool HandleMapLayerRequest (IClientAPI sender, Packet pack)
        {
            #region Packet Session and User Check

            if (m_checkPackets) {
                var mapLayerRequestPacket = (MapLayerRequestPacket)pack;
                if (mapLayerRequestPacket != null &&
                    (mapLayerRequestPacket.AgentData.SessionID != SessionId || 
                     mapLayerRequestPacket.AgentData.AgentID != AgentId))
                    return true;
            }

            #endregion

            return true;
        }

        bool HandleMapBlockRequest (IClientAPI sender, Packet pack)
        {
            var MapRequest = (MapBlockRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (MapRequest.AgentData.SessionID != SessionId || 
                    MapRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RequestMapBlocks handlerRequestMapBlocks = OnRequestMapBlocks;
            if (handlerRequestMapBlocks != null) {
                handlerRequestMapBlocks (this,
                                         MapRequest.PositionData.MinX,
                                         MapRequest.PositionData.MinY,
                                         MapRequest.PositionData.MaxX,
                                         MapRequest.PositionData.MaxY,
                                         MapRequest.AgentData.Flags);
            }
            return true;
        }

        bool HandleMapNameRequest (IClientAPI sender, Packet pack)
        {
            var map = (MapNameRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (map.AgentData.SessionID != SessionId ||
                    map.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            string mapName = Util.UTF8.GetString (map.NameData.Name, 0, map.NameData.Name.Length - 1);
            RequestMapName handlerMapNameRequest = OnMapNameRequest;
            if (handlerMapNameRequest != null) {
                handlerMapNameRequest (this, mapName, map.AgentData.Flags);
            }
            return true;
        }

        bool HandleTeleportLandmarkRequest (IClientAPI sender, Packet pack)
        {
            var tpReq = (TeleportLandmarkRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (tpReq.Info.SessionID != SessionId ||
                    tpReq.Info.AgentID != AgentId)
                    return true;
            }

            #endregion

            UUID lmid = tpReq.Info.LandmarkID;
            if (lmid != UUID.Zero) {
                m_assetService.Get (lmid.ToString (), null, (id, s, lma) => {
                    AssetLandmark lm = null;
                    try {
                        if (lma != null)
                            lm = new AssetLandmark (lma);
                    } catch (NullReferenceException) {
                        // asset not found generates null ref inside the assetlandmark constructor.
                    }
                    if (lm == null) {
                        var tpCancel = (TeleportCancelPacket)PacketPool.Instance.GetPacket (PacketType.TeleportCancel);
                        tpCancel.Info.SessionID = tpReq.Info.SessionID;
                        tpCancel.Info.AgentID = tpReq.Info.AgentID;
                        OutPacket (tpCancel, ThrottleOutPacketType.Asset);
                    } else {
                        TeleportLandmarkRequest handlerTeleportLandmarkRequest = OnTeleportLandmarkRequest;
                        if (handlerTeleportLandmarkRequest != null) {
                            handlerTeleportLandmarkRequest (this, lm.RegionID, lm.Position);
                        } else {
                            //no event handler so cancel request
                            var tpCancel = (TeleportCancelPacket)PacketPool.Instance.GetPacket (PacketType.TeleportCancel);
                            tpCancel.Info.AgentID = tpReq.Info.AgentID;
                            tpCancel.Info.SessionID = tpReq.Info.SessionID;
                            OutPacket (tpCancel, ThrottleOutPacketType.Asset);
                        }
                    }
                });
            } else {
                // Teleport home request
                UUIDNameRequest handlerTeleportHomeRequest = OnTeleportHomeRequest;
                if (handlerTeleportHomeRequest != null) {
                    handlerTeleportHomeRequest (AgentId, this);
                }
                return true;
            }

            return true;
        }

        bool HandleTeleportLocationRequest (IClientAPI sender, Packet pack)
        {
            var tpLocReq = (TeleportLocationRequestPacket)pack;
            // MainConsole.Instance.Debug(tpLocReq.ToString());

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (tpLocReq.AgentData.SessionID != SessionId || tpLocReq.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            TeleportLocationRequest handlerTeleportLocationRequest = OnTeleportLocationRequest;
            if (handlerTeleportLocationRequest != null) {
                handlerTeleportLocationRequest (this,
                                                tpLocReq.Info.RegionHandle,
                                                tpLocReq.Info.Position,
                                                tpLocReq.Info.LookAt,
                                                16);
            } else {
                //no event handler so cancel request
                TeleportCancelPacket tpCancel =
                    (TeleportCancelPacket)PacketPool.Instance.GetPacket (PacketType.TeleportCancel);
                tpCancel.Info.SessionID = tpLocReq.AgentData.SessionID;
                tpCancel.Info.AgentID = tpLocReq.AgentData.AgentID;
                OutPacket (tpCancel, ThrottleOutPacketType.Asset);
            }
            return true;
        }

        #endregion Inventory/Asset/Other related packets

        bool HandleMapItemRequest (IClientAPI sender, Packet pack)
        {
            var mirpk = (MapItemRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (mirpk.AgentData.SessionID != SessionId ||
                    mirpk.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            //MainConsole.Instance.Debug(mirpk.ToString());
            MapItemRequest handlerMapItemRequest = OnMapItemRequest;
            if (handlerMapItemRequest != null) {
                handlerMapItemRequest (this,
                                       mirpk.AgentData.Flags,
                                       mirpk.AgentData.EstateID,
                                       mirpk.AgentData.Godlike,
                                       mirpk.RequestData.ItemType,
                                       mirpk.RequestData.RegionHandle);
            }
            return true;
        }



        readonly List<UUID> m_transfersToAbort = new List<UUID> ();

        bool HandleTransferAbort (IClientAPI sender, Packet pack)
        {
            var transferAbort = (TransferAbortPacket)pack;
            m_transfersToAbort.Add (transferAbort.TransferInfo.TransferID);
            return true;
        }

        /// <summary>
        ///     Make an asset request to the asset service in response to a client request.
        /// </summary>
        /// <param name="transferRequest"></param>
        /// <param name="taskID"></param>
        void MakeAssetRequest (TransferRequestPacket transferRequest, UUID taskID)
        {
            UUID requestID = UUID.Zero;
            switch (transferRequest.TransferInfo.SourceType) {
            case (int)SourceType.Asset:
                requestID = new UUID (transferRequest.TransferInfo.Params, 0);
                break;
            case (int)SourceType.SimInventoryItem:
                requestID = new UUID (transferRequest.TransferInfo.Params, 80);
                break;
            }

            //MainConsole.Instance.InfoFormat("[Client]: {0} requesting asset {1}", Name, requestID);

            m_assetService.Get (requestID.ToString (), transferRequest, AssetReceived);
        }

        /// <summary>
        ///     When we get a reply back from the asset service in response to a client request, send back the data.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sender"></param>
        /// <param name="asset"></param>
        void AssetReceived (string id, object sender, AssetBase asset)
        {
            //MainConsole.Instance.InfoFormat("[Client]: {0} found requested asset", Name);

            TransferRequestPacket transferRequest = (TransferRequestPacket)sender;

            UUID requestID = UUID.Zero;
            byte source = (byte)SourceType.Asset;
            if (transferRequest.TransferInfo.SourceType == (int)SourceType.Asset) {
                requestID = new UUID (transferRequest.TransferInfo.Params, 0);
            } else if (transferRequest.TransferInfo.SourceType == (int)SourceType.SimInventoryItem) {
                requestID = new UUID (transferRequest.TransferInfo.Params, 80);
                source = (byte)SourceType.SimInventoryItem;
            }

            if (m_transfersToAbort.Contains (requestID))
                return; //They wanted to cancel it

            // The asset is known to exist and is in our cache, so add it to the AssetRequests list
            AssetRequestToClient req = new AssetRequestToClient {
                AssetInf = asset,
                AssetRequestSource = source,
                IsTextureRequest = false,
                NumPackets = asset == null ? 0 : CalculateNumPackets (asset.Data),
                Params = transferRequest.TransferInfo.Params,
                RequestAssetID = requestID,
                TransferRequestID = transferRequest.TransferInfo.TransferID
            };


            if (asset == null) {
                SendFailedAsset (req, TransferPacketStatus.AssetUnknownSource);
                return;
            }
            // Scripts cannot be retrieved by direct request
            if (transferRequest.TransferInfo.SourceType == (int)SourceType.Asset && asset.Type == 10) {
                SendFailedAsset (req, TransferPacketStatus.InsufficientPermissions);
                return;
            }

            SendAsset (req);
        }

    }
}
