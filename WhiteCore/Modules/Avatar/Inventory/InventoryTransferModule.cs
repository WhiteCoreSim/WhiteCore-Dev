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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;

namespace WhiteCore.Modules.Inventory
{
    public class InventoryTransferModule : INonSharedRegionModule
    {
        readonly List<IScene> m_Scenelist = new List<IScene> ();

        bool m_Enabled = true;
        IMessageTransferModule m_TransferModule;
        IMoneyModule moneyService;

        #region INonSharedRegionModule Members

        public void Initialise (IConfigSource config)
        {
            if (config.Configs ["Messaging"] != null) {
                // Allow disabling this module in config
                //
                if (config.Configs ["Messaging"].GetString ("InventoryTransferModule", "InventoryTransferModule") !=
                    "InventoryTransferModule") {
                    m_Enabled = false;
                    return;
                }
            }
        }

        public void AddRegion (IScene scene)
        {
            if (!m_Enabled)
                return;

            m_Scenelist.Add (scene);

            scene.RegisterModuleInterface (this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

            moneyService = scene.RequestModuleInterface<IMoneyModule> ();

        }

        public void RegionLoaded (IScene scene)
        {
            if (m_TransferModule == null) {
                m_TransferModule = m_Scenelist [0].RequestModuleInterface<IMessageTransferModule> ();
                if (m_TransferModule == null) {
                    MainConsole.Instance.Error (
                        "[Inventory transfer]: No Message transfer module found, transfers will be local only");
                    m_Enabled = false;

                    m_Scenelist.Clear ();
                    scene.EventManager.OnNewClient -= OnNewClient;
                    scene.EventManager.OnClosingClient -= OnClosingClient;
                    scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
                }
            }
        }

        public void RemoveRegion (IScene scene)
        {
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
            scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            m_Scenelist.Remove (scene);
        }

        public void Close ()
        {
        }

        public string Name {
            get { return "InventoryModule"; }
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        #endregion

        void OnNewClient (IClientAPI client)
        {
            // Inventory giving is conducted via instant message
            client.OnInstantMessage += OnInstantMessage;
        }

        void OnClosingClient (IClientAPI client)
        {
            client.OnInstantMessage -= OnInstantMessage;
        }

        IScene FindClientScene (UUID agentId)
        {
            lock (m_Scenelist) {
                foreach (IScene scene in m_Scenelist) {
                    var presence = scene.GetScenePresence (agentId);
                    if (presence != null)
                        return scene;
                }
            }
            return null;
        }

        void OnInstantMessage (IClientAPI client, GridInstantMessage im)
        {
            //MainConsole.Instance.InfoFormat("[Inventory transfer]: OnInstantMessage {0}", im.dialog);
            IScene clientScene = FindClientScene (client.AgentId);
            if (clientScene == null) { 
                // Something seriously wrong here.
                MainConsole.Instance.DebugFormat ("[Inventory transfer]: Cannot find originating user scene");
                return;
            }

            if (im.Dialog == (byte)InstantMessageDialog.InventoryOffered) {
                //MainConsole.Instance.DebugFormat("Asset type {0}", ((AssetType)im.binaryBucket[0]));

                if (im.BinaryBucket.Length < 17) { // Invalid
                    MainConsole.Instance.DebugFormat ("[Inventory transfer]: Invalid length {0} for asset type {1}",
                        im.BinaryBucket.Length, ((AssetType)im.BinaryBucket [0]));
                    return;
                }

                UUID recipientID = im.ToAgentID;
                IScenePresence recipientUser = null;
                IScene recipientUserScene = FindClientScene (recipientID);
                if (recipientUserScene != null)
                    recipientUser = recipientUserScene.GetScenePresence (recipientID);
                
                UUID copyID;

                // give the item to the recipient, assuming they will accept it
                // First byte is the asset type
                AssetType assetType = (AssetType)im.BinaryBucket [0];

                if (assetType == AssetType.Folder) {
                    var folderID = new UUID (im.BinaryBucket, 1);
                    if (im.SessionID == folderID) {
                        // this must be an offline message being processed. just pass it through
                        if (m_TransferModule != null)
                            m_TransferModule.SendInstantMessage (im);
                        return;
                    }

                    MainConsole.Instance.DebugFormat (
                        "[Inventory transfer]: Inserting original folder {0} into agent {1}'s inventory",
                        folderID, im.ToAgentID);


                    clientScene.InventoryService.GiveInventoryFolderAsync (
                        recipientID,
                        client.AgentId,
                        folderID,
                        UUID.Zero,
                        (folder) => {
                            if (folder == null) {
                                client.SendAgentAlertMessage ("Can't find folder to give. Nothing given.", false);
                                return;
                            }

                            // The outgoing binary bucket should contain only the byte which signals an asset folder is
                            // being copied and the following bytes for the copied folder's UUID
                            copyID = folder.ID;
                            byte [] copyIDBytes = copyID.GetBytes ();
                            im.BinaryBucket = new byte [1 + copyIDBytes.Length];
                            im.BinaryBucket [0] = (byte)AssetType.Folder;
                            Array.Copy (copyIDBytes, 0, im.BinaryBucket, 1, copyIDBytes.Length);
                            im.SessionID = copyID;

                            if (moneyService != null)
                                moneyService.Transfer (im.ToAgentID, im.FromAgentID, 0,
                                "Inworld inventory folder transfer", TransactionType.GiveInventory);

                            if (recipientUser != null) {
                                // user is on this region... update them
                                recipientUser.ControllingClient.SendBulkUpdateInventory (folder);
                                recipientUser.ControllingClient.SendInstantMessage (im);
                            } else if (m_TransferModule != null) {
                                // user is not in this region or not online... let them know
                                m_TransferModule.SendInstantMessage (im);
                            }

                        });

                    } else {
                        // Inventory item
                        // First byte of the array is probably the item type
                        // Next 16 bytes are the UUID

                        var itemID = new UUID (im.BinaryBucket, 1);
                        if (im.SessionID == itemID) {
                            // this must be an offline message being processed. just pass it through
                            if (m_TransferModule != null) 
                                m_TransferModule.SendInstantMessage (im);
                            return;
                        }


                        MainConsole.Instance.DebugFormat (
                            "[Inventory transfer]: (giving) Inserting item {0} into agent {1}'s inventory",
                            itemID, im.ToAgentID);
                        
                        clientScene.InventoryService.GiveInventoryItemAsync (
                            im.ToAgentID,
                            im.FromAgentID,
                            itemID,
                            UUID.Zero,
                            false,
                            (itemCopy) => {
                                if (itemCopy == null) {
                                    MainConsole.Instance.DebugFormat (
                                        "[Inventory transfer]: (giving) Unable to find item {0} to give to agent {1}'s inventory",
                                        itemID, im.ToAgentID);
                                    client.SendAgentAlertMessage ("Can't find item to give. Nothing given.", false);
                                    return;
                                }

                            copyID = itemCopy.ID;
                            Array.Copy (copyID.GetBytes (), 0, im.BinaryBucket, 1, 16);
                            im.SessionID = itemCopy.ID;

                            if (moneyService != null)
                                moneyService.Transfer (im.ToAgentID, im.FromAgentID, 0,
                                            "Inworld inventory item transfer", TransactionType.GiveInventory);

                            if (recipientUser != null) {
                                // user is on this region... 
                                recipientUser.ControllingClient.SendBulkUpdateInventory (itemCopy);
                                recipientUser.ControllingClient.SendInstantMessage (im);
                            } else if (m_TransferModule != null) {
                                // user is not present on this region or offline... let them know
                                m_TransferModule.SendInstantMessage (im);
                            }
                        });
                }

             } else if (im.Dialog == (byte)InstantMessageDialog.InventoryAccepted) {
                IScenePresence user = clientScene.GetScenePresence (im.ToAgentID);
                MainConsole.Instance.DebugFormat ("[Inventory transfer]: Acceptance message received");

                if (user != null)  { // Local
                    user.ControllingClient.SendInstantMessage (im);
                } else {
                    if (m_TransferModule != null)
                        m_TransferModule.SendInstantMessage (im);
                }
            } else if (im.Dialog == (byte)InstantMessageDialog.InventoryDeclined) {
                // Here, the recipient is local and we can assume that the
                // inventory is loaded. Courtesy of the above bulk update,
                // It will have been pushed to the client, too
                //
                IInventoryService invService = clientScene.InventoryService;
                MainConsole.Instance.DebugFormat ("[Inventory transfer]: Declined message received");

                InventoryFolderBase trashFolder =
                    invService.GetFolderForType (client.AgentId, InventoryType.Unknown, FolderType.Trash);

                UUID inventoryID = im.SessionID; // The inventory item/folder, back from it's trip

                InventoryItemBase item = invService.GetItem (client.AgentId, inventoryID);
                InventoryFolderBase folder = null;

                // cannot delete if we do not have  somewhere to put it
                if (trashFolder != null) {
                    // Deleting an item
                    if (item != null) {
                        item.Folder = trashFolder.ID;

                        var uuids = new List<UUID> { item.ID };
                        invService.DeleteItems (item.Owner, uuids);         // delete the item from the client's inventory

                        ILLClientInventory inventory = client.Scene.RequestModuleInterface<ILLClientInventory> ();
                        if (inventory != null)
                            inventory.AddInventoryItemAsync (client, item); // send an inventory update to the client
                    } else {
                        // deleting a folder
                        folder = new InventoryFolderBase (inventoryID, client.AgentId);
                        folder = invService.GetFolder (folder);

                        if (folder != null) {
                            folder.ParentID = trashFolder.ID;
                            invService.MoveFolder (folder);
                            client.SendBulkUpdateInventory (folder);
                        }
                    }
                }

                if ((item == null && folder == null) | trashFolder == null) {
                    string reason = string.Empty;

                    if (trashFolder == null)
                        reason += " Trash folder not found.";
                    if (item == null)
                        reason += " Item not found.";
                    if (folder == null)
                        reason += " Folder not found.";

                    client.SendAgentAlertMessage ("Unable to delete received inventory" + reason, false);
                }

                if (moneyService != null)
                    moneyService.Transfer (im.ToAgentID, im.FromAgentID, 0,
                        "Inworld inventory transfer declined", TransactionType.GiveInventory);

                IScenePresence user = clientScene.GetScenePresence (im.ToAgentID);

                if (user != null)  { // Local
                    user.ControllingClient.SendInstantMessage (im);
                } else {
                    if (m_TransferModule != null)
                        m_TransferModule.SendInstantMessage (im);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="msg"></param>
        void OnGridInstantMessage (GridInstantMessage msg)
        {
            // Check if this is ours to handle
            //
            IScene userScene = FindClientScene (msg.ToAgentID);
            if (userScene == null) {
                MainConsole.Instance.DebugFormat ("[Inventory transfer]: Cannot find user scene for instant message");
                return;
            }

            // Find agent to deliver to
            //
            IScenePresence user = userScene.GetScenePresence (msg.ToAgentID);

            // Just forward to local handling
            OnInstantMessage (user.ControllingClient, msg);
        }
    }
}
