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
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.SQLServices.InventoryService
{
    public class InventoryService : ConnectorBase, IInventoryService, IService
    {
        #region Declares

        protected bool m_AllowDelete = true;

        protected IAssetService m_AssetService;
        protected IInventoryData m_Database;
        protected ILibraryService m_LibraryService;
        protected IUserAccountService m_UserAccountService;
        protected Dictionary<UUID, InventoryItemBase> _tempItemCache = new Dictionary<UUID, InventoryItemBase>();

        #endregion

        #region IService Members

        public virtual string Name
        {
            get { return GetType().Name; }
        }

        public virtual void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("InventoryHandler", "") != Name)
                return;

            IConfig invConfig = config.Configs["InventoryService"];
            if (invConfig != null)
                m_AllowDelete = invConfig.GetBoolean ("AllowDelete", true);
            
            registry.RegisterModuleInterface<IInventoryService> (this);
            Init (registry, Name, serverPath: "/inventory/", serverHandlerName: "InventoryServerURI");

        }

        public virtual void Start(IConfigSource config, IRegistryCore registry)
        {
            m_Database = Framework.Utilities.DataManager.RequestPlugin<IInventoryData>();
            m_UserAccountService = registry.RequestModuleInterface<IUserAccountService>();
            m_LibraryService = registry.RequestModuleInterface<ILibraryService>();
            m_AssetService = registry.RequestModuleInterface<IAssetService>();

            registry.RequestModuleInterface<ISimulationBase>()
                    .EventManager.RegisterEventHandler("DeleteUserInformation", DeleteUserInformation);
        }

        public virtual void FinishedStartup()
        {
            if (IsLocalConnector &&  (MainConsole.Instance != null))
            {
                MainConsole.Instance.Commands.AddCommand (
                    "fix inventory",
                    "fix inventory",
                    "If the user's inventory has been corrupted, this function will attempt to fix it",
                    FixInventory, false, true);

                // Provide correction for existing users for the updated 
                //   FolderType definitions implemented Sept 2015
                // This may be removed for future releases - greythane -
                MainConsole.Instance.Commands.AddCommand (
                    "verify root folders",
                    "verify root folders",
                    "Verify that the users root folder is the correct type",
                    VerifyRootFolders, false, true);
            }

            _addInventoryItemQueue.Start(
                0.5,
                (agentID, itemsToAdd) =>
                {
                    if (itemsToAdd == null)
                        return;

                    foreach (AddInventoryItemStore item in itemsToAdd)
                    {
                        if (UUID.Zero == item.Item.Folder)
                        {
                            InventoryFolderBase f = GetFolderForType( item.Item.Owner, (InventoryType) item.Item.InvType, (FolderType) item.Item.AssetType );

                            if (f != null)
                                item.Item.Folder = f.ID;
                            else
                            {
                                f = GetRootFolder(item.Item.Owner);
                                if (f != null)
                                    item.Item.Folder = f.ID;
                                else
                                {
                                    MainConsole.Instance.WarnFormat(
                                        "[InventorySerivce]: Could not find root folder for {0} when trying to add item {1} with no parent folder specified",
                                        item.Item.Owner, item.Item.Name);
                                    return;
                                }
                            }
                        }

                        AddItem(item.Item);
                        lock (_tempItemCache)
                            _tempItemCache.Remove(item.Item.ID);

                        if (item.Complete != null)
                            item.Complete(item.Item);
                    }
                });

            _moveInventoryItemQueue.Start(
                0.5,
                (agentID, itemsToMove) =>
                {
                    foreach (var item in itemsToMove)
                    {
                        MoveItems(agentID, item.Items);
                        if (item.Complete != null)
                            item.Complete();
                    }
                });
        }

        #endregion

        #region IInventoryService Members

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual bool CreateUserInventory(UUID principalID, bool createDefaultItems)
        {
            /*object remoteValue = DoRemoteByURL("InventoryServerURI", principalID, createDefaultItems);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool)remoteValue;*/

            List<InventoryItemBase> items;
            return CreateUserInventory(principalID, createDefaultItems, out items);
        }

        public virtual bool CreateUserInventory(UUID principalID, bool createDefaultItems,
                                                out List<InventoryItemBase> defaultItems)
        {
            // This is brain-dead. We can't ever communicate that we fixed
            // an existing inventory. Well, just return root folder status,
            // but check sanity anyway.
            //
            bool result = false;

            InventoryFolderBase rootFolder = GetRootFolder(principalID);
            if (rootFolder == null)
            {
                rootFolder = CreateFolder(principalID, UUID.Zero, (int) FolderType.Root, InventoryFolderBase.ROOT_FOLDER_NAME);
                if (rootFolder != null)
                    result = true;
                else {
                    MainConsole.Instance.Error ("Inventory service]: Unable to obtain/create user's root folder!");
                    defaultItems = new List<InventoryItemBase> ();
                    return false;
                }
            }

            InventoryFolderBase[] sysFolders = GetSystemFolders(principalID);

            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Animation) return true;
                    return false;
                }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Animation, "Animations");
            
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.BodyPart) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.BodyPart, "Body Parts");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.CallingCard) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.CallingCard, "Calling Cards");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Clothing) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Clothing, "Clothing");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Gesture) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Gesture, "Gestures");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Landmark) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Landmark, "Landmarks");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                                                  if (f.Type == (short) FolderType.LostAndFound) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.LostAndFound, "Lost And Found");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Notecard) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Notecard, "Notecards");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Object) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Object, "Objects");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Snapshot) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Snapshot, "Photo Album");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.LSLText) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.LSLText, "Scripts");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Sound) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Sound, "Sounds");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Texture) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Texture, "Textures");
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Trash) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Trash, "Trash");

            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Mesh) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Mesh, "Mesh");

            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Inbox) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Inbox, "Received Items");

            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.Outbox) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.Outbox, "Merchant Outbox");

            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.CurrentOutfit) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.CurrentOutfit, "Current Outfit");
            
            // Marketplace related folders, unchecked at the moment
            
            if (!Array.Exists(sysFolders, delegate(InventoryFolderBase f)
                                              {
                    if (f.Type == (short) FolderType.VMMListings) return true;
                                                  return false;
                                              }))
                CreateFolder(principalID, rootFolder.ID, (int) FolderType.VMMListings, "Marketplace Listings");

            if (createDefaultItems && m_LibraryService != null)
            {
                defaultItems = new List<InventoryItemBase>();
                InventoryFolderBase bodypartFolder = GetFolderForType(principalID, InventoryType.Unknown, FolderType.BodyPart);
                InventoryFolderBase clothingFolder = GetFolderForType(principalID, InventoryType.Unknown, FolderType.Clothing);

                // Default items
                InventoryItemBase defaultShape = new InventoryItemBase
                                                     {
                                                         Name = "Default shape",
                                                         Description = "Default shape description",
                                                         AssetType = (int) AssetType.Bodypart,
                                                         InvType = (int) InventoryType.Wearable,
                                                         Flags = (uint) WearableType.Shape,
                                                         ID = UUID.Random()
                                                     };
                //Give a new copy to every person
                AssetBase asset = m_AssetService.Get(AvatarWearable.DEFAULT_SHAPE_ASSET.ToString());
                if (asset != null)
                {
                    asset.ID = UUID.Random();
                    asset.ID = m_AssetService.Store(asset);
                    defaultShape.AssetID = asset.ID;
                    defaultShape.Folder = bodypartFolder.ID;
                    defaultShape.CreatorId = Constants.LibraryOwnerUUID;
                    defaultShape.Owner = principalID;
                    defaultShape.BasePermissions = (uint) PermissionMask.All;
                    defaultShape.CurrentPermissions = (uint) PermissionMask.All;
                    defaultShape.EveryOnePermissions = (uint) PermissionMask.None;
                    defaultShape.NextPermissions = (uint) PermissionMask.All;
                    AddItem(defaultShape, false);
                    defaultItems.Add(defaultShape);
                }

                InventoryItemBase defaultSkin = new InventoryItemBase
                                                    {
                                                        Name = "Default skin",
                                                        Description = "Default skin description",
                                                        AssetType = (int) AssetType.Bodypart,
                                                        InvType = (int) InventoryType.Wearable,
                                                        Flags = (uint) WearableType.Skin,
                                                        ID = UUID.Random()
                                                    };
                //Give a new copy to every person
                asset = m_AssetService.Get(AvatarWearable.DEFAULT_SKIN_ASSET.ToString());
                if (asset != null)
                {
                    asset.ID = UUID.Random();
                    asset.ID = m_AssetService.Store(asset);
                    defaultSkin.AssetID = asset.ID;
                    defaultSkin.Folder = bodypartFolder.ID;
                    defaultSkin.CreatorId = Constants.LibraryOwnerUUID;
                    defaultSkin.Owner = principalID;
                    defaultSkin.BasePermissions = (uint) PermissionMask.All;
                    defaultSkin.CurrentPermissions = (uint) PermissionMask.All;
                    defaultSkin.EveryOnePermissions = (uint) PermissionMask.None;
                    defaultSkin.NextPermissions = (uint) PermissionMask.All;
                    AddItem(defaultSkin, false);
                    defaultItems.Add(defaultSkin);
                }

                InventoryItemBase defaultHair = new InventoryItemBase
                                                    {
                                                        Name = "Default hair",
                                                        Description = "Default hair description",
                                                        AssetType = (int) AssetType.Bodypart,
                                                        InvType = (int) InventoryType.Wearable,
                                                        Flags = (uint) WearableType.Hair,
                                                        ID = UUID.Random()
                                                    };
                //Give a new copy to every person
                asset = m_AssetService.Get(AvatarWearable.DEFAULT_HAIR_ASSET.ToString());
                if (asset != null)
                {
                    asset.ID = UUID.Random();
                    asset.ID = m_AssetService.Store(asset);
                    defaultHair.AssetID = asset.ID;
                    defaultHair.Folder = bodypartFolder.ID;
                    defaultHair.CreatorId = Constants.LibraryOwnerUUID;
                    defaultHair.Owner = principalID;
                    defaultHair.BasePermissions = (uint) PermissionMask.All;
                    defaultHair.CurrentPermissions = (uint) PermissionMask.All;
                    defaultHair.EveryOnePermissions = (uint) PermissionMask.None;
                    defaultHair.NextPermissions = (uint) PermissionMask.All;
                    AddItem(defaultHair, false);
                    defaultItems.Add(defaultHair);
                }

                InventoryItemBase defaultEyes = new InventoryItemBase
                                                    {
                                                        Name = "Default eyes",
                                                        Description = "Default eyes description",
                                                        AssetType = (int) AssetType.Bodypart,
                                                        InvType = (int) InventoryType.Wearable,
                                                        Flags = (uint) WearableType.Eyes,
                                                        ID = UUID.Random()
                                                    };
                //Give a new copy to every person
                asset = m_AssetService.Get(AvatarWearable.DEFAULT_EYES_ASSET.ToString());
                if (asset != null)
                {
                    asset.ID = UUID.Random();
                    asset.ID = m_AssetService.Store(asset);
                    defaultEyes.AssetID = asset.ID;
                    defaultEyes.Folder = bodypartFolder.ID;
                    defaultEyes.CreatorId = Constants.LibraryOwnerUUID;
                    defaultEyes.Owner = principalID;
                    defaultEyes.BasePermissions = (uint) PermissionMask.All;
                    defaultEyes.CurrentPermissions = (uint) PermissionMask.All;
                    defaultEyes.EveryOnePermissions = (uint) PermissionMask.None;
                    defaultEyes.NextPermissions = (uint) PermissionMask.All;
                    AddItem(defaultEyes, false);
                    defaultItems.Add(defaultEyes);
                }

                InventoryItemBase defaultShirt = new InventoryItemBase
                                                     {
                                                         Name = "Default shirt",
                                                         Description = "Default shirt description",
                                                         AssetType = (int) AssetType.Clothing,
                                                         InvType = (int) InventoryType.Wearable,
                                                         Flags = (uint) WearableType.Shirt,
                                                         ID = UUID.Random()
                                                     };
                //Give a new copy to every person
                asset = m_AssetService.Get(AvatarWearable.DEFAULT_SHIRT_ASSET.ToString());
                if (asset != null)
                {
                    asset.ID = UUID.Random();
                    asset.ID = m_AssetService.Store(asset);
                    defaultShirt.AssetID = asset.ID;
                    defaultShirt.Folder = clothingFolder.ID;
                    defaultShirt.CreatorId = Constants.LibraryOwnerUUID;
                    defaultShirt.Owner = principalID;
                    defaultShirt.BasePermissions = (uint) PermissionMask.All;
                    defaultShirt.CurrentPermissions = (uint) PermissionMask.All;
                    defaultShirt.EveryOnePermissions = (uint) PermissionMask.None;
                    defaultShirt.NextPermissions = (uint) PermissionMask.All;
                    AddItem(defaultShirt, false);
                    defaultItems.Add(defaultShirt);
                }

                InventoryItemBase defaultPants = new InventoryItemBase
                                                     {
                                                         Name = "Default pants",
                                                         Description = "Default pants description",
                                                         AssetType = (int) AssetType.Clothing,
                                                         InvType = (int) InventoryType.Wearable,
                                                         Flags = (uint) WearableType.Pants,
                                                         ID = UUID.Random()
                                                     };
                //Give a new copy to every person
                asset = m_AssetService.Get(AvatarWearable.DEFAULT_PANTS_ASSET.ToString());
                if (asset != null)
                {
                    asset.ID = UUID.Random();
                    asset.ID = m_AssetService.Store(asset);
                    defaultPants.AssetID = asset.ID;
                    defaultPants.Folder = clothingFolder.ID;
                    defaultPants.CreatorId = Constants.LibraryOwnerUUID;
                    defaultPants.Owner = principalID;
                    defaultPants.BasePermissions = (uint) PermissionMask.All;
                    defaultPants.CurrentPermissions = (uint) PermissionMask.All;
                    defaultPants.EveryOnePermissions = (uint) PermissionMask.None;
                    defaultPants.NextPermissions = (uint) PermissionMask.All;
                    AddItem(defaultPants, false);
                    defaultItems.Add(defaultPants);
                }
            }
            else
                defaultItems = new List<InventoryItemBase>();

            return result;
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual List<InventoryFolderBase> GetInventorySkeleton(UUID principalID)
        {
            /*object remoteValue = DoRemoteByURL("InventoryServerURI", principalID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<InventoryFolderBase>)remoteValue;*/

            List<InventoryFolderBase> allFolders = m_Database.GetFolders(
                new[] {"agentID"},
                new[] {principalID.ToString()});
            if (allFolders.Count == 0)
                return null;

            return allFolders;
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool FolderExists(UUID folderID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folderID);
                return remoteValue == null ? false : (bool)remoteValue;
            }

            return m_Database.FolderExists(folderID);
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool FolderItemExists(UUID folderID, UUID itemID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folderID, itemID);
                return remoteValue == null ? false : (bool)remoteValue;
            }

            return m_Database.FolderItemExists(folderID, itemID);
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool ItemExists(UUID itemID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", itemID);
                return remoteValue == null ? false : (bool)remoteValue;
            }

            return m_Database.ItemExists(itemID);
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual List<string> GetUserFolderID(UUID principalID, string folderName)
        {
            /*object remoteValue = DoRemoteByURL("InventoryServerURI", principalID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<InventoryFolderBase>)remoteValue;*/

            return m_Database.GetUserFolderID (principalID, folderName);
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual List<InventoryFolderBase> GetRootFolders(UUID principalID)
        {
            /*object remoteValue = DoRemoteByURL("InventoryServerURI", principalID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<InventoryFolderBase>)remoteValue;*/

            return m_Database.GetFolders(
                new[] {"agentID", "parentFolderID"},
                new[] {principalID.ToString(), UUID.Zero.ToString()});
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Medium)]
        public virtual InventoryFolderBase GetRootFolder(UUID principalID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", principalID);
                return remoteValue != null ? (InventoryFolderBase)remoteValue : null;
            }

            List<InventoryFolderBase> folders = m_Database.GetFolders(
                new[] {"agentID", "parentFolderID"},
                new[] {principalID.ToString(), UUID.Zero.ToString()});

            if (folders.Count == 0) {
                // nothing for this user... auto create the root folder
                var rootfolder = CreateFolder (principalID, UUID.Zero, (int)FolderType.Root, InventoryFolderBase.ROOT_FOLDER_NAME);
                return rootfolder;
             }

            // we have the user's folders... find the root
            InventoryFolderBase root = null;
            foreach (InventoryFolderBase folder in folders.Where(folder => folder.Name == InventoryFolderBase.ROOT_FOLDER_NAME))
                root = folder;

            return root;
        }


        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual InventoryFolderBase GetFolderForType(UUID principalID, InventoryType invType, FolderType type)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", principalID, invType, type);
                return remoteValue != null ? (InventoryFolderBase)remoteValue : null;
            }

            if (invType == InventoryType.Snapshot)
                type = FolderType.Snapshot;
            //Fix for snapshots, as they get the texture asset type, but need to get checked as snapshot folder types

            List<InventoryFolderBase> folders = m_Database.GetFolders(
                new[] {"agentID", "type"},
                new[] {principalID.ToString(), ((int) type).ToString()});

            if (folders.Count == 0)
            {
                //                MainConsole.Instance.WarnFormat("[XINVENTORY SERVICE]: Found no folder for type {0} for user {1}", type, principalID);
                return null;
            }

            //            MainConsole.Instance.DebugFormat(
            //                "[XINVENTORY SERVICE]: Found folder {0} {1} for type {2} for user {3}", 
            //                folders[0].folderName, folders[0].folderID, type, principalID);

            return folders[0];
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public virtual InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", userID, folderID);
                return remoteValue != null ? (InventoryCollection)remoteValue : null;
            }

            // This method doesn't receive a valid principal id from the
            // connector. So we disregard the principal and look
            // by ID.
            //
            MainConsole.Instance.DebugFormat("[Inventory Service]: Fetch contents for folder {0}", folderID);
            InventoryCollection inventory = new InventoryCollection ();
            inventory.UserID = userID;
            inventory.FolderID = folderID;
            inventory.Folders = m_Database.GetFolders (new [] { "parentFolderID" }, new [] { folderID.ToString()});
            inventory.Items = m_Database.GetItems (userID, new [] { "parentFolderID" }, new [] { folderID.ToString()});

            return inventory;
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual List<InventoryItemBase> GetFolderItems(UUID principalID, UUID folderID)
        {
            /*object remoteValue = DoRemoteByURL("InventoryServerURI", principalID, folderID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<InventoryItemBase>)remoteValue;*/

            if (principalID != UUID.Zero)
                return m_Database.GetItems(principalID,
                                           new[] {"parentFolderID", "avatarID"},
                                           new[] {folderID.ToString(), principalID.ToString()});
            return m_Database.GetItems(principalID,
                                       new[] {"parentFolderID"},
                                       new[] {folderID.ToString()});
        }


        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual List<InventoryFolderBase> GetFolderFolders(UUID principalID, UUID folderID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", principalID, folderID);
                return remoteValue != null
                    ? (List<InventoryFolderBase>)remoteValue
                    : new List<InventoryFolderBase> ();
            }

            // Since we probably don't get a valid principal here, either ...
            //
            List<InventoryFolderBase> invItems = m_Database.GetFolders(
                new[] {"parentFolderID"},
                new[] {folderID.ToString()});

            return invItems;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool AddFolder(InventoryFolderBase folder)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folder);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            InventoryFolderBase check = GetFolder(folder);
            if (check != null)
                return false;

            return m_Database.StoreFolder(folder);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool UpdateFolder(InventoryFolderBase folder)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folder);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            if (!m_AllowDelete) //Initial item MUST be created as a link folder
                if (folder.Type == (sbyte) AssetType.LinkFolder)
                    return false;

            InventoryFolderBase check = GetFolder(folder);
            if (check == null)
                return AddFolder(folder);

            if (check.Type != (short) FolderType.None || folder.Type != (short) FolderType.None)
            {
                if (folder.Version > check.Version)
                    return false;
                check.Version = folder.Version;
                check.Type = folder.Type;
                check.Version++;
                return m_Database.StoreFolder(check);
            }

            if (folder.Version < check.Version)
                folder.Version = check.Version;
            folder.ID = check.ID;

            folder.Version++;
            return m_Database.StoreFolder(folder);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool MoveFolder(InventoryFolderBase folder)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folder);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            List<InventoryFolderBase> x = m_Database.GetFolders(
                new[] {"folderID"},
                new[] {folder.ID.ToString()});

            if (x.Count == 0)
                return false;

            x[0].ParentID = folder.ParentID;

            return m_Database.StoreFolder(x[0]);
        }

        // We don't check the principal's ID here
        //
        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public virtual bool DeleteFolders(UUID principalID, List<UUID> folderIDs)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", principalID, folderIDs);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            if (!m_AllowDelete)
            {
                foreach (UUID id in folderIDs)
                {
                    if (!ParentIsLinkFolder(id))
                        continue;
                    InventoryFolderBase f = new InventoryFolderBase {ID = id};
                    PurgeFolder(f);
                    m_Database.DeleteFolders("folderID", id.ToString(), true);
                }
                return true;
            }

            // Ignore principal ID, it's bogus at connector level
            //
            foreach (UUID id in folderIDs)
            {
                if (!ParentIsTrash(id))
                    continue;
                InventoryFolderBase f = new InventoryFolderBase {ID = id};
                PurgeFolder(f);
                m_Database.DeleteFolders("folderID", id.ToString(), true);
            }

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public virtual bool PurgeFolder(InventoryFolderBase folder)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folder);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            if (!m_AllowDelete && !ParentIsLinkFolder(folder.ID))
                return false;

            if (!ParentIsTrash(folder.ID))
                return false;

            List<InventoryFolderBase> subFolders = m_Database.GetFolders(
                new[] {"parentFolderID"},
                new[] {folder.ID.ToString()});

            foreach (InventoryFolderBase x in subFolders)
            {
                PurgeFolder(x);
                m_Database.DeleteFolders("folderID", x.ID.ToString(), true);
            }

            m_Database.DeleteItems("parentFolderID", folder.ID.ToString());

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual bool ForcePurgeFolder(InventoryFolderBase folder)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folder);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            List<InventoryFolderBase> subFolders = m_Database.GetFolders(
                new[] {"parentFolderID"},
                new[] {folder.ID.ToString()});

            foreach (InventoryFolderBase x in subFolders)
            {
                ForcePurgeFolder(x);
                m_Database.DeleteFolders("folderID", x.ID.ToString(), false);
            }

            m_Database.DeleteItems("parentFolderID", folder.ID.ToString());
            m_Database.DeleteFolders("folderID", folder.ID.ToString(), false);

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool AddItem(InventoryItemBase item)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", item);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            return AddItem(item, true);
        }

        public virtual bool AddItem(InventoryItemBase item, bool doParentFolderCheck)
        {
            if (doParentFolderCheck)
            {
                InventoryFolderBase folder = GetFolder(new InventoryFolderBase(item.Folder));

                if (folder == null || folder.Owner != item.Owner)
                {
                    MainConsole.Instance.DebugFormat ("[Inventory service]: Aborting adding item as folder {0} does not exist or is not the owner's",folder);
                    return false;
                }
            }
            m_Database.IncrementFolder(item.Folder);
            bool success = m_Database.StoreItem(item);
            if (!success)
                MainConsole.Instance.DebugFormat ("[Inventory service]: Failed to save item {0} in folder {1}",item.Name,item.Folder);
            else
                MainConsole.Instance.DebugFormat ("[Inventory service]: Saved item {0} in folder {1}",item.Name,item.Folder);

            return success;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool UpdateItem(InventoryItemBase item)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", item);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            if (!m_AllowDelete) //Initial item MUST be created as a link or link folder
                if (item.AssetType == (sbyte) AssetType.Link || item.AssetType == (sbyte) AssetType.LinkFolder)
                    return false;
            m_Database.IncrementFolder(item.Folder);
            return m_Database.StoreItem(item);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool UpdateAssetIDForItem(UUID itemID, UUID assetID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", itemID, assetID);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            return m_Database.UpdateAssetIDForItem(itemID, assetID);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool MoveItems(UUID principalID, List<InventoryItemBase> items)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", principalID, items);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            foreach (InventoryItemBase i in items)
            {
                //re-fetch because we don't have Owner filled in properly
                InventoryItemBase item = GetItem(UUID.Zero, i.ID);
                if(item == null) continue;
                // Cannot move this item, its from libraryowner
                if(item.Owner == (UUID)Constants.LibraryOwnerUUID) continue;

                m_Database.IncrementFolder(i.Folder); //Increment the new folder
                m_Database.IncrementFolderByItem(i.ID);
                //And the old folder too (have to use this one because we don't know the old folder)
                m_Database.MoveItem(i.ID.ToString(), i.Folder.ToString());
            }

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.High)]
        public virtual bool DeleteItems(UUID principalID, List<UUID> itemIDs)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", principalID, itemIDs);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            if (!m_AllowDelete)
            {
                foreach (UUID id in itemIDs)
                {
                    InventoryItemBase item = GetItem(principalID, id);
                    if(item == null) continue;
                    m_Database.IncrementFolder(item.Folder);
                    if (!ParentIsLinkFolder(item.Folder))
                        continue;
                    if (item.Owner == (UUID)Constants.LibraryOwnerUUID)
                        continue;
                    m_Database.DeleteItems("inventoryID", id.ToString());
                }
                return true;
            }

            // Just use the ID... *facepalms*
            //
            foreach (UUID id in itemIDs)
            {
                InventoryItemBase item = GetItem(UUID.Zero, id);
                if(item == null) continue;
                if(item.Owner == (UUID)Constants.LibraryOwnerUUID) continue;
                m_Database.DeleteItems("inventoryID", id.ToString());
                m_Database.IncrementFolderByItem(id);
            }

            return true;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual InventoryItemBase GetItem(UUID userID, UUID inventoryID)
        {
            lock (_tempItemCache)
            {
                if (_tempItemCache.ContainsKey(inventoryID))
                    return _tempItemCache[inventoryID];
            }

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", userID, inventoryID);
                return remoteValue != null ? (InventoryItemBase)remoteValue : null;
            }

            string[] fields = userID != UUID.Zero ? new[] {"inventoryID", "avatarID"} : new[] {"inventoryID"};
            string[] vals = userID != UUID.Zero
                                ? new[] {inventoryID.ToString(), userID.ToString()}
                                : new[] {inventoryID.ToString()};
            List<InventoryItemBase> items = m_Database.GetItems(userID, fields, vals);

            if (items.Count == 0)
                return null;

            return items[0];
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID GetItemAssetID(UUID userID, UUID inventoryID)
        {
            lock (_tempItemCache)
            {
                if (_tempItemCache.ContainsKey(inventoryID))
                    return _tempItemCache[inventoryID].AssetID;
            }

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", userID, inventoryID);
                return remoteValue != null ? (UUID)remoteValue : UUID.Zero;
            }

            List<UUID> items = m_Database.GetItemAssetIDs(userID,
                                                          new[] {"inventoryID", "avatarID"},
                                                          new[] {inventoryID.ToString(), userID.ToString()});

            if (items.Count == 0)
                return UUID.Zero;

            return items[0];
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual OSDArray GetOSDItem(UUID avatarID, UUID itemID)
        {
            /*object remoteValue = DoRemoteByURL("InventoryServerURI", avatarID, itemID);
            if (remoteValue != null || m_doRemoteOnly)
                return (OSDArray)remoteValue;*/
            if (avatarID != UUID.Zero)
            {
                return m_Database.GetLLSDItems(
                    new string[2] {"inventoryID", "avatarID"},
                    new string[2] {itemID.ToString(), avatarID.ToString()});
            }
            return m_Database.GetLLSDItems(
                new string[1] {"inventoryID"},
                new string[1] {itemID.ToString()});
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folder);
                return remoteValue != null ? (InventoryFolderBase)remoteValue : null;
            }

            List<InventoryFolderBase> folders = m_Database.GetFolders(
                new[] {"folderID"},
                new[] {folder.ID.ToString()});

            if (folders.Count == 0)
                return null;

            return folders[0];
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual InventoryFolderBase GetFolderByOwnerAndName(UUID folderOwner, string folderName)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("InventoryServerURI", folderOwner, folderName);
                return remoteValue != null ? (InventoryFolderBase)remoteValue : null;
            }

            List<InventoryFolderBase> folders = m_Database.GetFolders(
                new[] {"folderName", "agentID"},
                new[] {folderName, folderOwner.ToString()});

            if (folders.Count == 0)
                return null;

            return folders[0];
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public virtual List<InventoryItemBase> GetActiveGestures(UUID principalID)
        {
            /*object remoteValue = DoRemoteByURL("InventoryServerURI", principalID);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<InventoryItemBase>)remoteValue;*/

            return new List<InventoryItemBase>(m_Database.GetActiveGestures(principalID));
        }

        public object DeleteUserInformation(string name, object param)
        {
            UUID user = (UUID) param;
            var skel = GetInventorySkeleton(user);
            if (skel != null)
            {
                foreach (var folder in skel)
                {
                    var items = GetFolderContent(user, folder.ID);
                    DeleteItems(user, items.Items.ConvertAll<UUID>(item => item.ID));
                    ForcePurgeFolder(folder);
                }
            }
            return null;
        }

        #endregion

        #region Asynchronous Commands

        protected ListCombiningTimedSaving<AddInventoryItemStore> _addInventoryItemQueue =
            new ListCombiningTimedSaving<AddInventoryItemStore>();

        protected ListCombiningTimedSaving<MoveInventoryItemStore> _moveInventoryItemQueue =
            new ListCombiningTimedSaving<MoveInventoryItemStore>();

        public void AddItemAsync(InventoryItemBase item, Action<InventoryItemBase> success)
        {
            if (item == null)
                return;
            lock (_tempItemCache)
            {
                if (!_tempItemCache.ContainsKey(item.ID))
                    _tempItemCache.Add(item.ID, item);
            }
            _addInventoryItemQueue.Add(item.Owner, new AddInventoryItemStore(item, success));
        }

        public void MoveItemsAsync(UUID agentID, List<InventoryItemBase> items, NoParam success)
        {
            _moveInventoryItemQueue.Add(agentID, new MoveInventoryItemStore(items, success));
        }

        public void AddCacheItemAsync(InventoryItemBase item)
        {
            if (item == null)
                return;
            lock (_tempItemCache)
            {
                if (!_tempItemCache.ContainsKey(item.ID))
                    _tempItemCache.Add(item.ID, item);
            }
            //_addInventoryItemQueue.Add(item.Owner, new AddInventoryItemStore(item, success));
        }

        /// <summary>
        ///     Give an entire inventory folder from one user to another.  The entire contents (including all descendent
        ///     folders) is given.
        /// </summary>
        /// <param name="recipientId"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="folderId"></param>
        /// <param name="recipientParentFolderId">
        ///     The id of the recipient folder in which the send folder should be placed.  If UUID.Zero then the
        ///     recipient folder is the root folder
        /// </param>
        /// <param name="success"></param>
        /// <returns>
        ///     The inventory folder copy given, null if the copy was unsuccessful
        /// </returns>
        public void GiveInventoryFolderAsync(
            UUID recipientId, UUID senderId, UUID folderId, UUID recipientParentFolderId, GiveFolderParam success)
        {
            Util.FireAndForget(o =>
                                   {
                                       // Retrieve the folder from the sender
                                       InventoryFolderBase folder = GetFolder(new InventoryFolderBase(folderId));
                                       if (null == folder)
                                       {
                                           MainConsole.Instance.ErrorFormat(
                                               "[Inventory service]: Could not find inventory folder {0} to give",
                                               folderId);
                                           success(null);
                                           return;
                                       }

                                       //Find the folder for the receiver
                                       if (recipientParentFolderId == UUID.Zero)
                                       {
                                           InventoryFolderBase recipientRootFolder = GetRootFolder(recipientId);
                                           if (recipientRootFolder != null)
                                               recipientParentFolderId = recipientRootFolder.ID;
                                           else
                                           {
                                               MainConsole.Instance.WarnFormat(
                                                   "[Inventory service]: Unable to find root folder for receiving agent");
                                               success(null);
                                               return;
                                           }
                                       }

                                       UUID newFolderId = UUID.Random();
                                       InventoryFolderBase newFolder
                                           = new InventoryFolderBase(
                                               newFolderId, folder.Name, recipientId, folder.Type,
                                               recipientParentFolderId, folder.Version);
                                       AddFolder(newFolder);

                                       // Give all the subfolders
                                       InventoryCollection contents = GetFolderContent(senderId, folderId);
                                       foreach (InventoryFolderBase childFolder in contents.Folders)
                                       {
                                           GiveInventoryFolderAsync(recipientId, senderId, childFolder.ID, newFolder.ID,
                                                                    null);
                                       }

                                       // Give all the items
                                       foreach (InventoryItemBase item in contents.Items)
                                       {
                                           InnerGiveInventoryItem(recipientId, senderId, item, newFolder.ID, true, true);
                                       }
                                       success(newFolder);
                                   });
        }

        /// <summary>
        ///     Give an inventory item from one user to another
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="itemId"></param>
        /// <param name="recipientFolderId">
        ///     The id of the folder in which the copy item should go.  If UUID.Zero then the item is placed in the most
        ///     appropriate default folder.
        /// </param>
        /// <param name="doOwnerCheck">This is for when the item is being given away publically, such as when it is posted on a group notice</param>
        /// <param name="success"></param>
        /// <returns>
        ///     The inventory item copy given, null if the give was unsuccessful
        /// </returns>
        public void GiveInventoryItemAsync(UUID recipient, UUID senderId, UUID itemId,
                                           UUID recipientFolderId, bool doOwnerCheck, GiveItemParam success)
        {
            Util.FireAndForget(o =>
                                   {
                                       InventoryItemBase item = GetItem(senderId, itemId);
                                       success(InnerGiveInventoryItem(recipient, senderId,
                                                                      item, recipientFolderId, doOwnerCheck, true));
                                   });
        }

        public InventoryItemBase InnerGiveInventoryItem(
            UUID recipient, UUID senderId, InventoryItemBase item, UUID recipientFolderId, bool doOwnerCheck, bool checkTransferPermission)
        {
            if (item == null)
            {
                MainConsole.Instance.Info("[Inventory service]: Could not find item to give to " + recipient);
                return null;
            }
            if (!doOwnerCheck || item.Owner == senderId)
            {
                if (checkTransferPermission)
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                    {
                        MainConsole.Instance.WarnFormat (
                            "[Inventory service]: Inventory copy of {0} aborted due to permissions: Sender {1}, recipient {2}",
                            item.AssetID, senderId, recipient);
                        return null;
                    }
                }

                // Insert a copy of the item into the recipient
                InventoryItemBase itemCopy = new InventoryItemBase
                                                 {
                                                     Owner = recipient,
                                                     CreatorId = item.CreatorId,
                                                     CreatorData = item.CreatorData,
                                                     ID = UUID.Random(),
                                                     AssetID = item.AssetID,
                                                     Description = item.Description,
                                                     Name = item.Name,
                                                     AssetType = item.AssetType,
                                                     InvType = item.InvType,
                                                     Folder = recipientFolderId
                                                 };

                if (recipient != senderId)
                {
                    // Trying to do this right this time. This is evil. If
                    // you believe in Good, go elsewhere. Vampires and other
                    // evil creators only beyond this point. You have been
                    // warned.

                    // We're going to mask a lot of things by the next perms
                    // Tweak the next perms to be nicer to our data
                    //
                    // In this mask, all the bits we do NOT want to mess
                    // with are set. These are:
                    //
                    // Transfer
                    // Copy
                    // Modify
                    const uint permsMask = ~((uint) PermissionMask.Copy |
                                             (uint) PermissionMask.Transfer |
                                             (uint) PermissionMask.Modify);

                    // Now, reduce the next perms to the mask bits
                    // relevant to the operation
                    uint nextPerms = permsMask | (item.NextPermissions &
                                                  ((uint) PermissionMask.Copy |
                                                   (uint) PermissionMask.Transfer |
                                                   (uint) PermissionMask.Modify));

                    // nextPerms now has all bits set, except for the actual
                    // next permission bits.

                    // This checks for no mod, no copy, no trans.
                    // This indicates an error or messed up item. Do it like
                    // SL and assume trans
                    if (nextPerms == permsMask)
                        nextPerms |= (uint) PermissionMask.Transfer;

                    // Inventory owner perms are the logical AND of the
                    // folded perms and the root prim perms, however, if
                    // the root prim is mod, the inventory perms will be
                    // mod. This happens on "take" and is of little concern
                    // here, save for preventing escalation

                    // This hack ensures that items previously permalocked
                    // get unlocked when they're passed or rezzed
                    uint basePerms = item.BasePermissions |
                                     (uint) PermissionMask.Move;
                    uint ownerPerms = item.CurrentPermissions;

                    // If this is an object, root prim perms may be more
                    // permissive than folded perms. Use folded perms as
                    // a mask
                    if (item.InvType == (int) InventoryType.Object)
                    {
                        // Create a safe mask for the current perms
                        uint foldedPerms = (item.CurrentPermissions & 7) << 13;
                        foldedPerms |= permsMask;

                        bool isRootMod = (item.CurrentPermissions &
                                          (uint) PermissionMask.Modify) != 0;

                        // Mask the owner perms to the folded perms
                        ownerPerms &= foldedPerms;
                        basePerms &= foldedPerms;

                        // If the root was mod, let the mask reflect that
                        // We also need to adjust the base here, because
                        // we should be able to edit in-inventory perms
                        // for the root prim, if it's mod.
                        if (isRootMod)
                        {
                            ownerPerms |= (uint) PermissionMask.Modify;
                            basePerms |= (uint) PermissionMask.Modify;
                        }
                    }

                    // These will be applied to the root prim at next rez.
                    // The slam bit (bit 3) and folded permission (bits 0-2)
                    // are preserved due to the above mangling
                    ownerPerms &= nextPerms;

                    // Mask the base permissions. This is a conservative
                    // approach altering only the three main perms
                    basePerms &= nextPerms;

                    // Assign to the actual item. Make sure the slam bit is
                    // set, if it wasn't set before.
                    itemCopy.BasePermissions = basePerms;
                    itemCopy.CurrentPermissions = ownerPerms | 16; // Slam

                    itemCopy.NextPermissions = item.NextPermissions;

                    // This preserves "everyone can move"
                    itemCopy.EveryOnePermissions = item.EveryOnePermissions &
                                                   nextPerms;

                    // Intentionally killing "share with group" here, as
                    // the recipient will not have the group this is
                    // set to
                    itemCopy.GroupPermissions = 0;

                    MainConsole.Instance.Debug ("[Inventory service]: Updated item permissions for new user");
                }
                else
                {
                    itemCopy.CurrentPermissions = item.CurrentPermissions;
                    itemCopy.NextPermissions = item.NextPermissions;
                    itemCopy.EveryOnePermissions = item.EveryOnePermissions & item.NextPermissions;
                    itemCopy.GroupPermissions = item.GroupPermissions & item.NextPermissions;
                    itemCopy.BasePermissions = item.BasePermissions;
                }

                if (itemCopy.Folder == UUID.Zero)
                {
                    InventoryFolderBase folder = GetFolderForType(recipient,
                                                                  (InventoryType) itemCopy.InvType,
                                                                  (FolderType) itemCopy.AssetType);

                    if (folder != null)
                        itemCopy.Folder = folder.ID;
                }

                itemCopy.GroupID = UUID.Zero;
                itemCopy.GroupOwned = false;
                itemCopy.Flags = item.Flags;
                itemCopy.SalePrice = item.SalePrice;
                itemCopy.SaleType = item.SaleType;

                if (! AddItem(itemCopy))
                    MainConsole.Instance.Warn ("[Inventory service]: Failed to insert inventory item copy into database");


                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                {
                    DeleteItems (senderId, new List<UUID> { item.ID });
                    MainConsole.Instance.Debug ("[Inventory service]: Deleting new item as permissions prevent copying");
                }


                return itemCopy;
            }
            MainConsole.Instance.WarnFormat(
                "[Inventory service]: Failed to give item {0} as item does not belong to giver", item.ID);
            return null;
        }

        #region Internal Classes

        protected class AddInventoryItemStore
        {
            public AddInventoryItemStore(InventoryItemBase item, Action<InventoryItemBase> success)
            {
                Item = item;
                Complete = success;
            }

            public InventoryItemBase Item;
            public Action<InventoryItemBase> Complete;
        }

        protected class MoveInventoryItemStore
        {
            public MoveInventoryItemStore(List<InventoryItemBase> items, NoParam success)
            {
                Items = items;
                Complete = success;
            }

            public List<InventoryItemBase> Items;
            public NoParam Complete;
        }

        #endregion

        #endregion

        #region Console Commands

        public virtual void FixInventory(IScene scene, string[] cmd)
        {
            string userName = MainConsole.Instance.Prompt ("Name of user (First Last)");
            UserAccount account = m_UserAccountService.GetUserAccount (null, userName);
            if (account == null)
            {
                MainConsole.Instance.WarnFormat ("Sorry.. Could not find user '{0}'", userName);
                return;
            }

            MainConsole.Instance.Info ("Verifying inventory for " + account.Name);
            InventoryFolderBase rootFolder = GetRootFolder(account.PrincipalID);

            //Fix having a default root folder
            if (rootFolder == null)
            {
                MainConsole.Instance.Warn ("Fixing default root folder...");
                List<InventoryFolderBase> skel;
                skel = GetInventorySkeleton (account.PrincipalID);
                if (skel == null)
                {
                    MainConsole.Instance.Info ("  .... skipping as user has not logged in yet");
                    return;
                }

                if (skel.Count == 0)
                {
                    CreateUserInventory (account.PrincipalID, false);
                    rootFolder = GetRootFolder (account.PrincipalID);
                }
                // recheck to make sure
                if (rootFolder == null) {
                    rootFolder = new InventoryFolderBase {
                        Name = InventoryFolderBase.ROOT_FOLDER_NAME,
                        Type = (short)FolderType.Root,
                        Version = 1,
                        ID = skel [0].ParentID,
                        Owner = account.PrincipalID,
                        ParentID = UUID.Zero
                    };
                    m_Database.StoreFolder (rootFolder);
                }
            } else
            {
                // Check to make sure we have the correct foldertype (Sep 2015)
                if (rootFolder.Type != (short) FolderType.Root)
                {
                    rootFolder.Type = (short) FolderType.Root;
                    MainConsole.Instance.Warn ("Correcting root folder type");
                    m_Database.StoreFolder (rootFolder);
                }
            }

            //Check against multiple root folders
            List<InventoryFolderBase> rootFolders = GetRootFolders(account.PrincipalID);
            List<UUID> badFolders = new List<UUID>();
            if (rootFolders.Count != 1)
            {
                //No duplicate folders!
                foreach (
                    InventoryFolderBase f in rootFolders.Where(f => !badFolders.Contains(f.ID) && f.ID != rootFolder.ID)
                    )
                {
                    MainConsole.Instance.Warn("Removing duplicate root folder " + f.Name);
                    badFolders.Add(f.ID);
                }
            }
            //Fix any root folders that shouldn't be root folders
            List<InventoryFolderBase> skeleton = GetInventorySkeleton(account.PrincipalID);
            List<UUID> foundFolders = new List<UUID>();
            foreach (InventoryFolderBase f in skeleton)
            {
                if (!foundFolders.Contains(f.ID))
                    foundFolders.Add(f.ID);
                if (f.Name == InventoryFolderBase.ROOT_FOLDER_NAME && f.ParentID != UUID.Zero)
                {
                    //Merge them all together
                    badFolders.Add(f.ID);
                }
            }
            foreach (InventoryFolderBase f in skeleton)
            {
                if ((!foundFolders.Contains(f.ParentID) && f.ParentID != UUID.Zero) ||
                    f.ID == f.ParentID)
                {
                    //The viewer loses the parentID when something goes wrong
                    //it puts it in the top where My Inventory should be
                    //We need to put it back in the root (My Inventory) folder, as the sub folders are right for some reason
                    f.ParentID = rootFolder.ID;
                    m_Database.StoreFolder(f);
                    MainConsole.Instance.WarnFormat("Fixing folder {0}", f.Name);
                }
                else if (badFolders.Contains(f.ParentID))
                {
                    //Put it back in the root (My Inventory) folder
                    f.ParentID = rootFolder.ID;
                    m_Database.StoreFolder(f);
                    MainConsole.Instance.WarnFormat("Fixing folder {0}", f.Name);
                }
                else if (f.Type == (short) FolderType.CurrentOutfit)
                {
                    List<InventoryItemBase> items = GetFolderItems(account.PrincipalID, f.ID);
                    //Check the links!
                    List<UUID> brokenLinks = new List<UUID>();
                    foreach (InventoryItemBase item in items)
                    {
                        InventoryItemBase linkedItem;
                        if ((linkedItem = GetItem(account.PrincipalID, item.AssetID)) == null)
                        {
                            //Broken link...
                            brokenLinks.Add(item.ID);
                        }
                        else if (linkedItem.ID == AvatarWearable.DEFAULT_EYES_ITEM ||
                                 linkedItem.ID == AvatarWearable.DEFAULT_SHAPE_ITEM ||
                                 linkedItem.ID == AvatarWearable.DEFAULT_HAIR_ITEM ||
                                 linkedItem.ID == AvatarWearable.DEFAULT_PANTS_ITEM ||
                                 linkedItem.ID == AvatarWearable.DEFAULT_SHIRT_ITEM ||
                                 linkedItem.ID == AvatarWearable.DEFAULT_SKIN_ITEM)
                        {
                            //Default item link, needs removed
                            brokenLinks.Add(item.ID);
                        }
                    }
                    if (brokenLinks.Count != 0)
                        DeleteItems(account.PrincipalID, brokenLinks);
                }
                else if (f.Type == (short) FolderType.Mesh)
                {
                    ForcePurgeFolder(f);  // Why?
                }
            }
            foreach (UUID id in badFolders)
            {
                m_Database.DeleteFolders("folderID", id.ToString(), false);
            }
            //Make sure that all default folders exist
            CreateUserInventory(account.PrincipalID, false);
            //Re-fetch the skeleton now
            skeleton = GetInventorySkeleton(account.PrincipalID);
            Dictionary<int, UUID> defaultFolders = new Dictionary<int, UUID>();
            Dictionary<UUID, UUID> changedFolders = new Dictionary<UUID, UUID>();
            foreach (InventoryFolderBase folder in skeleton.Where(folder => folder.Type != (short) FolderType.None))
            {
                if (!defaultFolders.ContainsKey(folder.Type))
                    defaultFolders[folder.Type] = folder.ID;
                else
                    changedFolders.Add(folder.ID, defaultFolders[folder.Type]);
            }
            foreach (InventoryFolderBase folder in skeleton)
            {
                if (folder.Type != (short) FolderType.None && defaultFolders[folder.Type] != folder.ID)
                {
                    //Delete the dup
                    ForcePurgeFolder(folder);
                    MainConsole.Instance.Warn("Purging duplicate default inventory type folder " + folder.Name);
                }
                if (changedFolders.ContainsKey(folder.ParentID))
                {
                    folder.ParentID = changedFolders[folder.ParentID];
                    MainConsole.Instance.Warn("Merging child folder of default inventory type " + folder.Name);
                    m_Database.StoreFolder(folder);
                }
            }
            MainConsole.Instance.Warn("Completed the check");
        }

        // update verification for new folder types - Sep 2015
        // This can be removed for future releases - greythane - 
        void VerifyRootFolders(IScene scene, string[] cmd)
        {
            List <UserAccount> userAccounts;
            userAccounts = m_UserAccountService.GetUserAccounts (null, "*");
            if (userAccounts != null)       // unlikely but..
            {
                foreach (var account in userAccounts)
                {
                    if (!Utilities.IsSystemUser (account.PrincipalID))
                    {
                        InventoryFolderBase rootFolder = GetRootFolder (account.PrincipalID);
                        if (rootFolder != null)
                        {
                            // Check to make sure we have the correct foldertype (Sep 2015)
                            if (rootFolder.Type != (short)FolderType.Root)
                            {
                                rootFolder.Type = (short)FolderType.Root;
                                MainConsole.Instance.Warn ("Correcting root folder type for " + account.Name);
                                m_Database.StoreFolder (rootFolder);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Helpers

        protected InventoryFolderBase CreateFolder(UUID principalID, UUID parentID, int type, string name)
        {
            InventoryFolderBase newFolder = new InventoryFolderBase
                                                {
                                                    Name = name,
                                                    Type = (short) type,
                                                    Version = 1,
                                                    ID = UUID.Random(),
                                                    Owner = principalID,
                                                    ParentID = parentID
                                                };


            m_Database.StoreFolder(newFolder);

            return newFolder;
        }

        protected virtual InventoryFolderBase[] GetSystemFolders(UUID principalID)
        {
            //            MainConsole.Instance.DebugFormat("[XINVENTORY SERVICE]: Getting system folders for {0}", principalID);

            InventoryFolderBase[] allFolders = m_Database.GetFolders(
                new[] {"agentID"},
                new[] {principalID.ToString()}).ToArray();

            InventoryFolderBase[] sysFolders = Array.FindAll(
                allFolders,
                delegate(InventoryFolderBase f)
                    {
                        if (f.Type > 0)
                            return true;
                        return false;
                    });

            //            MainConsole.Instance.DebugFormat(
            //                "[XINVENTORY SERVICE]: Found {0} system folders for {1}", sysFolders.Length, principalID);

            return sysFolders;
        }

        bool ParentIsTrash(UUID folderID)
        {
            List<InventoryFolderBase> folder = m_Database.GetFolders(new[] {"folderID"}, new[] {folderID.ToString()});
            if (folder.Count < 1)
                return false;

            if (folder[0].Type == (int) FolderType.Trash ||
                folder[0].Type == (int) FolderType.LostAndFound)
                return true;

            UUID parentFolder = folder[0].ParentID;

            while (parentFolder != UUID.Zero)
            {
                List<InventoryFolderBase> parent = m_Database.GetFolders(new[] {"folderID"},
                                                                         new[] {parentFolder.ToString()});
                if (parent.Count < 1)
                    return false;

                if (parent[0].Type == (int) FolderType.Trash ||
                    parent[0].Type == (int) FolderType.LostAndFound)
                    return true;
                if (parent[0].Type == (int) FolderType.Root)
                    return false;

                parentFolder = parent[0].ParentID;
            }
            return false;
        }

        bool ParentIsLinkFolder(UUID folderID)
        {
            List<InventoryFolderBase> folder = m_Database.GetFolders(new[] {"folderID"}, new[] {folderID.ToString()});
            if (folder.Count < 1)
                return false;

            if (folder[0].Type == (int) AssetType.LinkFolder)
                return true;

            UUID parentFolder = folder[0].ParentID;

            while (parentFolder != UUID.Zero)
            {
                List<InventoryFolderBase> parent = m_Database.GetFolders(new[] {"folderID"},
                                                                         new[] {parentFolder.ToString()});
                if (parent.Count < 1)
                    return false;

                if (parent[0].Type == (int) AssetType.LinkFolder)
                    return true;
                if (parent[0].Type == (int) FolderType.Root)
                    return false;

                parentFolder = parent[0].ParentID;
            }
            return false;
        }

        #endregion
    }
}
