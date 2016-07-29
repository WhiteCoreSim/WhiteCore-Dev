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
using System.IO;
using System.IO.Compression;
using System.Xml;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Serialization.External;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;

namespace WhiteCore.Modules.Archivers
{
    public class InventoryArchiveWriteRequest
    {
        /// <value>
        ///     Used to select all inventory nodes in a folder but not the folder itself
        /// </value>
        const string STAR_WILDCARD = "*";

        readonly InventoryArchiverModule m_module;
        readonly bool m_saveAssets;

        // some services
        IInventoryService m_inventoryService;
        IAssetService m_assetService;
        IUserAccountService m_accountService;

        /// <summary>
        /// Determines which items will be included in the archive, according to their permissions.
        /// Default is null, meaning no permission checks.
        /// </summary>
        public string FilterContent { get; set; }

        /// <summary>
        /// Counter for inventory items skipped due to permission filter option for passing to completion event
        /// </summary>
        public int CountFiltered { get; set; }

        /// <value>
        ///     The stream to which the inventory archive will be saved.
        /// </value>
        readonly Stream m_saveStream;

        readonly UserAccount m_userInfo;
        protected TarArchiveWriter m_archiveWriter;
        protected UuidGatherer m_assetGatherer;

        /// <value>
        ///     Used to collect the uuids of the assets that we need to save into the archive
        /// </value>
        protected Dictionary<UUID, AssetType> m_assetUuids = new Dictionary<UUID, AssetType>();

        protected List<AssetBase> m_assetsToAdd;
        protected InventoryFolderBase m_defaultFolderToSave;

        /// <value>
        ///     ID of this request
        /// </value>
        protected Guid m_id;

        string m_invPath;

        /// <value>
        ///     We only use this to request modules
        /// </value>
        protected IRegistryCore m_registry;

        /// <value>
        ///     Used to collect the uuids of the users that we need to save into the archive
        /// </value>
        protected Dictionary<UUID, int> m_userUuids = new Dictionary<UUID, int>();

        /// <summary>
        ///     Constructor
        /// </summary>
        public InventoryArchiveWriteRequest(
            Guid id, InventoryArchiverModule module, IRegistryCore registry,
            UserAccount userInfo, string invPath, string savePath, bool UseAssets, string checkPermissions)
            : this(
                id,
                module,
                registry,
                userInfo,
                invPath,
                new GZipStream(new FileStream(savePath, FileMode.Create), CompressionMode.Compress),
                UseAssets,
                null,
                new List<AssetBase>(),
                checkPermissions)
        {
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        public InventoryArchiveWriteRequest(
            Guid id, InventoryArchiverModule module, IRegistryCore registry,
            UserAccount userInfo, string invPath, Stream saveStream, bool UseAssets, InventoryFolderBase folderBase,
            List<AssetBase> assetsToAdd, string checkPermissions)
        {
            m_id = id;
            m_module = module;
            m_registry = registry;
            m_userInfo = userInfo;
            m_invPath = invPath;
            m_saveStream = saveStream;
            m_saveAssets = UseAssets;
            m_defaultFolderToSave = folderBase;
            m_assetsToAdd = assetsToAdd;

            // Set Permission filter if available
            if (checkPermissions != null)
                FilterContent = checkPermissions.ToUpper();

            // some necessary services
            m_inventoryService = m_registry.RequestModuleInterface<IInventoryService> ();
            m_assetService = m_registry.RequestModuleInterface<IAssetService> ();
            m_accountService = m_registry.RequestModuleInterface<IUserAccountService> ();

            // lastly as it is dependant       
            m_assetGatherer = new UuidGatherer(m_assetService);

        }

        protected void ReceivedAllAssets(ICollection<UUID> assetsFoundUuids, ICollection<UUID> assetsNotFoundUuids)
        {
            Exception reportedException = null;
            bool succeeded = true;

            try
            {
                // We're almost done.  Just need to write out the control file now
                m_archiveWriter.WriteFile(ArchiveConstants.CONTROL_FILE_PATH, CreateControlFile(m_saveAssets));
                MainConsole.Instance.InfoFormat("[Inventory Archiver]: Added control file to archive.");
                m_archiveWriter.Close();
            }
            catch (Exception e)
            {
                reportedException = e;
                succeeded = false;
            }
            finally
            {
                m_saveStream.Close();
            }

            if (m_module != null)
                m_module.TriggerInventoryArchiveSaved(
                    m_id, succeeded, m_userInfo, m_invPath, m_saveStream, reportedException);
        }

        protected void SaveInvItem(InventoryItemBase inventoryItem, string path)
        {

            // Check For Permissions Filter Flags
            if (!CanUserArchiveObject(m_userInfo.PrincipalID, inventoryItem))
            {
                MainConsole.Instance.InfoFormat(
                    "[Inventory Archiver]: Insufficient permissions, skipping inventory item {0} {1} at {2}",
                    inventoryItem.Name, inventoryItem.ID, path);

                // Count Items Excluded
                CountFiltered++;

                return;
            }

            string filename = path + CreateArchiveItemName(inventoryItem);

            // Record the creator of this item for user record purposes (which might go away soon)
            m_userUuids[inventoryItem.CreatorIdAsUuid] = 1;

            InventoryItemBase saveItem = (InventoryItemBase) inventoryItem.Clone();
            saveItem.CreatorId = OspResolver.MakeOspa(saveItem.CreatorIdAsUuid, m_accountService);

            string serialization = UserInventoryItemSerializer.Serialize(saveItem);
            m_archiveWriter.WriteFile(filename, serialization);

    //        m_assetGatherer.GatherAssetUuids(saveItem.AssetID, (AssetType) saveItem.AssetType, m_assetUuids);
            AssetType itemAssetType = (AssetType)inventoryItem.AssetType;

            // Don't chase down link asset items as they actually point to their target item IDs rather than an asset
            if (m_saveAssets && itemAssetType != AssetType.Link && itemAssetType != AssetType.LinkFolder)
                m_assetGatherer.GatherAssetUuids(saveItem.AssetID, (AssetType) inventoryItem.AssetType, m_assetUuids);

        }

        /// <summary>
        ///     Save an inventory folder
        /// </summary>
        /// <param name="inventoryFolder">The inventory folder to save</param>
        /// <param name="path">The path to which the folder should be saved</param>
        /// <param name="saveThisFolderItself">If true, save this folder itself.  If false, only saves contents</param>
        protected void SaveInvFolder(InventoryFolderBase inventoryFolder, string path, bool saveThisFolderItself)
        {
            // ignore viewer folders (special folders?)
            if (inventoryFolder.Name.StartsWith ("#", StringComparison.Ordinal))
                return;


            if (saveThisFolderItself)
            {
                path += CreateArchiveFolderName(inventoryFolder);

                // We need to make sure that we record empty folders
                m_archiveWriter.WriteDir(path);
            }

            InventoryCollection contents
                = m_inventoryService.GetFolderContent(inventoryFolder.Owner, inventoryFolder.ID);

            foreach (InventoryFolderBase childFolder in contents.Folders)
            {
                SaveInvFolder(childFolder, path, true);
            }

            foreach (InventoryItemBase item in contents.Items)
            {
                SaveInvItem(item, path);
            }
        }

        /// <summary>
        /// Checks whether the user has permission to export an inventory item to an IAR.
        /// </summary>
        /// <param name="UserID">The user</param>
        /// <param name="InvItem">The inventory item</param>
        /// <returns>Whether the user is allowed to export the object to an IAR</returns>
        bool CanUserArchiveObject(UUID UserID, InventoryItemBase InvItem)
        {
            if (string.IsNullOrEmpty(FilterContent))
                return true;// Default To Allow Export

            bool permitted = true;

            bool canCopy = (InvItem.CurrentPermissions & (uint)PermissionMask.Copy) != 0;
            bool canTransfer = (InvItem.CurrentPermissions & (uint)PermissionMask.Transfer) != 0;
            bool canMod = (InvItem.CurrentPermissions & (uint)PermissionMask.Modify) != 0;

            if (FilterContent.Contains("C") && !canCopy)
                permitted = false;

            if (FilterContent.Contains("T") && !canTransfer)
                permitted = false;

            if (FilterContent.Contains("M") && !canMod)
                permitted = false;

            return permitted;
        }


        /// <summary>
        ///     Execute the inventory write request
        /// </summary>
        public void Execute()
        {
            try
            {
                InventoryFolderBase inventoryFolder = null;
                InventoryItemBase inventoryItem = null;
                InventoryFolderBase rootFolder = m_inventoryService.GetRootFolder(m_userInfo.PrincipalID);

                if (rootFolder == null) {
                    MainConsole.Instance.ErrorFormat ("[Inventory Archiver]: Unable to fine root folder for {0}",
                                               m_userInfo.PrincipalID);
                    return;
                }

                if (m_defaultFolderToSave != null)
                    rootFolder = m_defaultFolderToSave;

                bool saveFolderContentsOnly = false;

                // Eliminate double slashes and any leading / on the path.
                string[] components
                    = m_invPath.Split(
                        new[] {InventoryFolderImpl.PATH_DELIMITER}, StringSplitOptions.RemoveEmptyEntries);

                int maxComponentIndex = components.Length - 1;

                // If the path terminates with a STAR then later on we want to archive all nodes in the folder but not the
                // folder itself.  This may get more sophisicated later on
                if (maxComponentIndex >= 0 && components[maxComponentIndex] == STAR_WILDCARD)
                {
                    saveFolderContentsOnly = true;
                    maxComponentIndex--;
                } else if (maxComponentIndex == -1)
                {
                    // If the user has just specified "/", then don't save the root "My Inventory" folder.  This is
                    // more intuitive then requiring the user to specify "/*" for this.
                   // 20141119-greythane- This breaks saving default inventory //  saveFolderContentsOnly = true;
                }

                m_invPath = string.Empty;
                for (int i = 0; i <= maxComponentIndex; i++)
                {
                    m_invPath += components[i] + InventoryFolderImpl.PATH_DELIMITER;
                }

                // Annoyingly Split actually returns the original string if the input string consists only of delimiters
                // Therefore if we still start with a / after the split, then we need the root folder
                if (m_invPath.Length == 0)
                {
                    inventoryFolder = rootFolder;
                }
                else
                {
                    m_invPath = m_invPath.Remove(m_invPath.LastIndexOf (InventoryFolderImpl.PATH_DELIMITER, StringComparison.Ordinal));
                    List<InventoryFolderBase> candidateFolders
                        = InventoryArchiveUtils.FindFolderByPath(m_inventoryService, rootFolder, m_invPath);
                    if (candidateFolders.Count > 0)
                        inventoryFolder = candidateFolders[0];
                }

                // The path may point to an item instead
                if (inventoryFolder == null)
                {
                    inventoryItem =
                        InventoryArchiveUtils.FindItemByPath(m_inventoryService, rootFolder, m_invPath);
                    //inventoryItem = m_userInfo.RootFolder.FindItemByPath(m_invPath);
                }

                if (null == inventoryFolder && null == inventoryItem)
                {
                    // We couldn't find the path indicated 
                    string errorMessage = string.Format("Aborted save.  Could not find inventory path {0}", m_invPath);
                    Exception e = new InventoryArchiverException(errorMessage);
                    if (m_module != null)
                        m_module.TriggerInventoryArchiveSaved(m_id, false, m_userInfo, m_invPath, m_saveStream, e);
                    throw e;
                }

                m_archiveWriter = new TarArchiveWriter(m_saveStream);

                if (inventoryFolder != null)
                {
                    MainConsole.Instance.DebugFormat(
                        "[Inventory Archiver]: Found folder {0} {1} at {2}",
                        inventoryFolder.Name,
                        inventoryFolder.ID,
                        m_invPath == string.Empty ? InventoryFolderImpl.PATH_DELIMITER : m_invPath);

                    //recurse through all dirs getting dirs and files
                    SaveInvFolder(inventoryFolder, ArchiveConstants.INVENTORY_PATH, !saveFolderContentsOnly);
                }
                else if (inventoryItem != null)
                {
                    MainConsole.Instance.DebugFormat(
                        "[Inventory Archiver]: Found item {0} {1} at {2}",
                        inventoryItem.Name, inventoryItem.ID, m_invPath);

                    SaveInvItem(inventoryItem, ArchiveConstants.INVENTORY_PATH);
                }

                // Don't put all this profile information into the archive right now.
                //SaveUsers();
            }
            catch (Exception)
            {
                m_saveStream.Close();
                throw;
            }
            if (m_saveAssets)
            {
                foreach (AssetBase asset in m_assetsToAdd)
                {
                    m_assetUuids[asset.ID] = (AssetType) asset.Type;
                }
                new AssetsRequest(
                    new AssetsArchiver(m_archiveWriter), m_assetUuids, m_assetService, ReceivedAllAssets).Execute();
                    
            }
            else
            {
                MainConsole.Instance.Debug("[Inventory Archiver]: Save Complete");
                m_archiveWriter.Close();
            }
        }

        /// <summary>
        ///     Save information for the users that we've collected.
        /// </summary>
        protected void SaveUsers()
        {
            MainConsole.Instance.InfoFormat("[Inventory Archiver]: Saving user information for {0} users",
                                            m_userUuids.Count);

            foreach (UUID creatorId in m_userUuids.Keys)
            {
                // Record the creator of this item
                UserAccount creator = m_accountService.GetUserAccount(null, creatorId);

                if (creator != null)
                {
                    m_archiveWriter.WriteFile(
                        ArchiveConstants.USERS_PATH + creator.FirstName + " " + creator.LastName + ".xml",
                        UserProfileSerializer.Serialize(creator.PrincipalID, creator.FirstName, creator.LastName));
                }
                else
                {
                    MainConsole.Instance.WarnFormat("[Inventory Archiver]: Failed to get creator profile for {0}",
                                                    creatorId);
                }
            }
        }

        /// <summary>
        ///     Create the archive name for a particular folder.
        /// </summary>
        /// These names are prepended with an inventory folder's UUID so that more than one folder can have the
        /// same name
        /// <param name="folder"></param>
        /// <returns></returns>
        public static string CreateArchiveFolderName(InventoryFolderBase folder)
        {
            return CreateArchiveFolderName(folder.Name, folder.ID);
        }

        /// <summary>
        ///     Create the archive name for a particular item.
        /// </summary>
        /// These names are prepended with an inventory item's UUID so that more than one item can have the
        /// same name
        /// <param name="item"></param>
        /// <returns></returns>
        public static string CreateArchiveItemName(InventoryItemBase item)
        {
            return CreateArchiveItemName(item.Name, item.ID);
        }

        /// <summary>
        ///     Create an archive folder name given its constituent components
        /// </summary>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string CreateArchiveFolderName(string name, UUID id)
        {
            return string.Format(
                "{0}{1}{2}/",
                InventoryArchiveUtils.EscapeArchivePath(name),
                ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR,
                id);
        }

        /// <summary>
        ///     Create an archive item name given its constituent components
        /// </summary>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string CreateArchiveItemName(string name, UUID id)
        {
            return string.Format(
                "{0}{1}{2}.xml",
                InventoryArchiveUtils.EscapeArchivePath(name),
                ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR,
                id);
        }

        /// <summary>
        ///     Create the control file for a 0.1 version archive
        /// </summary>
        /// <returns></returns>
        public static string CreateControlFile(bool saveAssets)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw) {Formatting = Formatting.Indented};
            xtw.WriteStartDocument();
            xtw.WriteStartElement("archive");
            xtw.WriteAttributeString("major_version", "0");
            xtw.WriteAttributeString("minor_version", "3");

            var includeAssets = saveAssets ? "True": "False";
            xtw.WriteElementString("assets_included", includeAssets);

            xtw.WriteEndElement();

            xtw.Flush();
            xtw.Close();

            string s = sw.ToString();
            sw.Close();

            return s;
        }
    }
}