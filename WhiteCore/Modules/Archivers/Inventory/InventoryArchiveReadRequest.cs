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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Serialization.External;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Text;

namespace WhiteCore.Modules.Archivers
{
    public class InventoryArchiveReadRequest
    {
        readonly string m_invPath;
        readonly List<InventoryItemBase> itemsSavedOff = new List<InventoryItemBase>();
        readonly Queue<UUID> assets2Save = new Queue<UUID>();
        protected bool m_assetsIncluded = true;

        // services...
        IAssetService m_assetService;
        IAssetDataPlugin m_assetData;
        IInventoryService m_inventoryService;
        IUserAccountService m_accountService;

        const string sPattern =
            @"(\{{0,1}([0-9a-fA-F]){8}-([0-9a-f]){4}-([0-9a-f]){4}-([0-9a-f]){4}-([0-9a-f]){12}\}{0,1})";

        /// <value>
        ///     The stream from which the inventory archive will be loaded.
        /// </value>
        readonly Stream m_loadStream;

        readonly UserAccount m_userInfo;
        protected TarArchiveReader archive;

        /// <summary>
        ///     Record the creator id that should be associated with an asset.  This is used to adjust asset creator ids
        ///     after OSP resolution (since OSP creators are only stored in the item
        /// </summary>
        protected Dictionary<UUID, UUID> m_creatorIdForAssetId = new Dictionary<UUID, UUID>();

        /// <summary>
        ///     Do we want to merge this load with existing inventory?
        /// </summary>
        protected bool m_merge;

        /// <value>
        ///     We only use this to request modules
        /// </value>
        protected IRegistryCore m_registry;

        UUID m_overridecreator = UUID.Zero;

        static InventoryArchiveReadRequest()
        {
            if (SceneEntitySerializer.SceneObjectSerializer == null)
                SceneEntitySerializer.SceneObjectSerializer =
                    new WhiteCore.Region.Serialization.SceneObjectSerializer();
        }

        public bool ReplaceAssets { get; set; }

        public InventoryArchiveReadRequest(
            IRegistryCore registry, UserAccount userInfo, string invPath, string loadPath, bool merge,
            UUID overwriteCreator)
        {
            Stream str = ArchiveHelpers.GetStream(loadPath);
            if (str == null)
                return;

            m_registry = registry;
            m_merge = merge;
            m_userInfo = userInfo;
            m_invPath = invPath.StartsWith("/") ? invPath.Remove(0, 1) : invPath;
            m_loadStream = new GZipStream(str, CompressionMode.Decompress);
            m_overridecreator = overwriteCreator;

            // we will need thse at some time
            m_assetService = m_registry.RequestModuleInterface<IAssetService>();
            m_assetData = Framework.Utilities.DataManager.RequestPlugin<IAssetDataPlugin>();
            m_inventoryService = m_registry.RequestModuleInterface<IInventoryService> ();
            m_accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
        }

        /// <summary>
        ///     Execute the request
        /// </summary>
        /// <returns>
        ///     A list of the inventory nodes loaded.  If folders were loaded then only the root folders are
        ///     returned
        /// </returns>
        public HashSet<InventoryNodeBase> Execute(bool loadAll)
        {
            if (m_loadStream == null)
                return new HashSet<InventoryNodeBase>();
            try
            {
                string filePath = "ERROR";
                int successfulAssetRestores = 0;
                int failedAssetRestores = 0;
                int successfulItemRestores = 0;

                HashSet<InventoryNodeBase> loadedNodes = loadAll ? new HashSet<InventoryNodeBase>() : null;

                List<InventoryFolderBase> folderCandidates
                    = InventoryArchiveUtils.FindFolderByPath(m_inventoryService, m_userInfo.PrincipalID, m_invPath);

                if (folderCandidates.Count == 0)
                {
                    // try and create requested folder
                    var rootFolder = m_inventoryService.GetRootFolder(m_userInfo.PrincipalID);

                    InventoryFolderBase iarImportFolder = new InventoryFolderBase();

                    iarImportFolder.ID = UUID.Random();
                    iarImportFolder.Name = m_invPath;                       // the path
                    iarImportFolder.Owner = m_userInfo.PrincipalID;         // owner
                    iarImportFolder.ParentID = rootFolder.ID;               // the root folder 
                    iarImportFolder.Type = -1;                              // user defined folder
                    iarImportFolder.Version = 1;                            // initial version

                    m_inventoryService.AddFolder(iarImportFolder);

                    // ensure that it now exists...
                    folderCandidates = InventoryArchiveUtils.FindFolderByPath(m_inventoryService, m_userInfo.PrincipalID, m_invPath);
                    if (folderCandidates.Count == 0)
                    {
                        MainConsole.Instance.ErrorFormat("[INVENTORY ARCHIVER]: Unable to create Inventory path {0}",
                                                     m_invPath);
                        return loadedNodes;
                    }
                }

                // we have the base folder... do it...
                InventoryFolderBase rootDestinationFolder = folderCandidates[0];
                archive = new TarArchiveReader(m_loadStream);

                // In order to load identically named folders, we need to keep track of the folders that we have already
                // resolved
                Dictionary<string, InventoryFolderBase> resolvedFolders = new Dictionary<string, InventoryFolderBase>();

                MainConsole.Instance.Info("[ARCHIVER]: Commencing load from archive");
                int ticker = 0;

                byte[] data;
                TarArchiveReader.TarEntryType entryType;

                while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
                {
                    if (TarArchiveReader.TarEntryType.TYPE_NORMAL_FILE == entryType)
                    {
                        var fName = Path.GetFileName (filePath);
                        if (fName.StartsWith ("."))                 // ignore hidden files
                            continue;
                    }

                    ticker ++;
                    if (ticker % 5 == 0)
                        MainConsole.Instance.Ticker();

                    if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                    {
                        if (LoadAsset(filePath, data))
                            successfulAssetRestores++;
                        else
                            failedAssetRestores++;

                        if ((successfulAssetRestores)%50 == 0)
                            MainConsole.Instance.InfoFormat(
                                " [INVENTORY ARCHIVER]: Loaded {0} assets...",
                                successfulAssetRestores);
                    }
                    else if (filePath.StartsWith(ArchiveConstants.INVENTORY_PATH))
                    {
                        filePath = filePath.Substring(ArchiveConstants.INVENTORY_PATH.Length);

                        // Trim off the file portion if we aren't already dealing with a directory path
                        if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY != entryType)
                            filePath = filePath.Remove(filePath.LastIndexOf("/") + 1);

                        InventoryFolderBase foundFolder
                            = ReplicateArchivePathToUserInventory(
                                filePath, rootDestinationFolder, ref resolvedFolders, ref loadedNodes);

                        if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY != entryType)
                        {
                            InventoryItemBase item = LoadItem(data, foundFolder);

                            if (item != null)
                            {
                                successfulItemRestores++;

                                if ((successfulItemRestores)%50 == 0)
                                    MainConsole.Instance.InfoFormat(
                                        "[INVENTORY ARCHIVER]: Restored {0} items...",successfulItemRestores);

                                // If we aren't loading the folder containing the item then well need to update the 
                                // viewer separately for that item.
                                if (loadAll && !loadedNodes.Contains(foundFolder))
                                    loadedNodes.Add(item);
                            }
                            item = null;
                        }
                    } else if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                    {
                        LoadControlFile(data);
                    }

                    data = null;
                }
 
                MainConsole.Instance.CleanInfo("");
                MainConsole.Instance.Info("[INVENTORY ARCHIVER]: Saving loaded inventory items");
                ticker = 0;

                int successfulItemLoaded = 0;
                foreach (InventoryItemBase item in itemsSavedOff)
                {
                    ticker++;
                    if (ticker % 5 == 0)
                        MainConsole.Instance.Ticker();

                    AddInventoryItem(item);
                    successfulItemLoaded++;

                    if ((successfulItemLoaded)%50 == 0)
                        MainConsole.Instance.InfoFormat(
                            "[INVENTORY ARCHIVER]: Loaded {0} items of {1}...",
                            successfulItemLoaded, itemsSavedOff.Count);
                }
                itemsSavedOff.Clear();
                assets2Save.Clear();

                MainConsole.Instance.CleanInfo("");
                MainConsole.Instance.InfoFormat(
                    "[INVENTORY ARCHIVER]: Successfully loaded {0} assets with {1} failures",
                    successfulAssetRestores, failedAssetRestores);
                MainConsole.Instance.InfoFormat("[INVENTORY ARCHIVER]: Successfully loaded {0} items",
                                                successfulItemRestores);

                return loadedNodes;
            }
            finally
            {
                m_loadStream.Close();
            }
        }

        public void Close()
        {
            if (m_loadStream != null)
                m_loadStream.Close();
        }

        /// <summary>
        ///     Replicate the inventory paths in the archive to the user's inventory as necessary.
        /// </summary>
        /// <param name="iarPath">The item archive path to replicate</param>
        /// <param name="rootDestFolder">The root folder for the inventory load</param>
        /// <param name="resolvedFolders">
        ///     The folders that we have resolved so far for a given archive path.
        ///     This method will add more folders if necessary
        /// </param>
        /// <param name="loadedNodes">
        ///     Track the inventory nodes created.
        /// </param>
        /// <returns>The last user inventory folder created or found for the archive path</returns>
        public InventoryFolderBase ReplicateArchivePathToUserInventory(
            string iarPath,
            InventoryFolderBase rootDestFolder,
            ref Dictionary<string, InventoryFolderBase> resolvedFolders,
            ref HashSet<InventoryNodeBase> loadedNodes)
        {
            string iarPathExisting = iarPath;

            //            MainConsole.Instance.DebugFormat(
            //                "[INVENTORY ARCHIVER]: Loading folder {0} {1}", rootDestFolder.Name, rootDestFolder.ID);

            InventoryFolderBase destFolder
                = ResolveDestinationFolder(rootDestFolder, ref iarPathExisting, ref resolvedFolders);

            //            MainConsole.Instance.DebugFormat(
            //                "[INVENTORY ARCHIVER]: originalArchivePath [{0}], section already loaded [{1}]", 
            //                iarPath, iarPathExisting);

            string iarPathToCreate = iarPath.Substring(iarPathExisting.Length);
            CreateFoldersForPath(destFolder, iarPathExisting, iarPathToCreate, ref resolvedFolders, ref loadedNodes);

            return destFolder;
        }

        /// <summary>
        ///     Resolve a destination folder
        /// </summary>
        /// We require here a root destination folder (usually the root of the user's inventory) and the archive
        /// path.  We also pass in a list of previously resolved folders in case we've found this one previously.
        /// <param name="archivePath">
        ///     The item archive path to resolve.  The portion of the path passed back is that
        ///     which corresponds to the resolved destination folder.
        /// </param>
        /// <param name="rootDestFolder">
        ///     The root folder for the inventory load
        /// </param>
        /// <param name="resolvedFolders">
        ///     The folders that we have resolved so far for a given archive path.
        /// </param>
        /// <returns>
        ///     The folder in the user's inventory that matches best the archive path given.  If no such folder was found
        ///     then the passed in root destination folder is returned.
        /// </returns>
        protected InventoryFolderBase ResolveDestinationFolder(
            InventoryFolderBase rootDestFolder,
            ref string archivePath,
            ref Dictionary<string, InventoryFolderBase> resolvedFolders)
        {
            //            string originalArchivePath = archivePath;

            while (archivePath.Length > 0)
            {
                //                MainConsole.Instance.DebugFormat("[INVENTORY ARCHIVER]: Trying to resolve destination folder {0}", archivePath);

                if (resolvedFolders.ContainsKey(archivePath))
                {
                    //                    MainConsole.Instance.DebugFormat(
                    //                        "[INVENTORY ARCHIVER]: Found previously created folder from archive path {0}", archivePath);
                    return resolvedFolders[archivePath];
                }
                if (m_merge)
                {
                    // TODO: Using m_invPath is totally wrong - what we need to do is strip the uuid from the 
                    // iar name and try to find that instead.
                    string plainPath = ArchiveConstants.ExtractPlainPathFromIarPath(archivePath);
                    List<InventoryFolderBase> folderCandidates
                    = InventoryArchiveUtils.FindFolderByPath(m_inventoryService, m_userInfo.PrincipalID, plainPath);

                    if (folderCandidates.Count != 0)
                    {
                        InventoryFolderBase destFolder = folderCandidates[0];
                        resolvedFolders[archivePath] = destFolder;
                        return destFolder;
                    }
                }

                // Don't include the last slash so find the penultimate one
                int penultimateSlashIndex = archivePath.LastIndexOf("/", archivePath.Length - 2);

                if (penultimateSlashIndex >= 0)
                {
                    // Remove the last section of path so that we can see if we've already resolved the parent
                    archivePath = archivePath.Remove(penultimateSlashIndex + 1);
                }
                else
                {
                    //                        MainConsole.Instance.DebugFormat(
                    //                            "[INVENTORY ARCHIVER]: Found no previously created folder for archive path {0}",
                    //                            originalArchivePath);
                    archivePath = string.Empty;
                    return rootDestFolder;
                }
            }

            return rootDestFolder;
        }

        /// <summary>
        ///     Create a set of folders for the given path.
        /// </summary>
        /// <param name="destFolder">
        ///     The root folder from which the creation will take place.
        /// </param>
        /// <param name="iarPathExisting">
        ///     the part of the iar path that already exists
        /// </param>
        /// <param name="iarPathToReplicate">
        ///     The path to replicate in the user's inventory from iar
        /// </param>
        /// <param name="resolvedFolders">
        ///     The folders that we have resolved so far for a given archive path.
        /// </param>
        /// <param name="loadedNodes">
        ///     Track the inventory nodes created.
        /// </param>
        protected void CreateFoldersForPath(
            InventoryFolderBase destFolder,
            string iarPathExisting,
            string iarPathToReplicate,
            ref Dictionary<string, InventoryFolderBase> resolvedFolders,
            ref HashSet<InventoryNodeBase> loadedNodes)
        {
            string[] rawDirsToCreate = iarPathToReplicate.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < rawDirsToCreate.Length; i++)
            {
                //                MainConsole.Instance.DebugFormat("[INVENTORY ARCHIVER]: Creating folder {0} from IAR", rawDirsToCreate[i]);

                if (!rawDirsToCreate[i].Contains(ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR))
                    continue;

                int identicalNameIdentifierIndex
                    = rawDirsToCreate[i].LastIndexOf(
                        ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR);

                string newFolderName = rawDirsToCreate[i].Remove(identicalNameIdentifierIndex);

                newFolderName = InventoryArchiveUtils.UnescapeArchivePath(newFolderName);
                UUID newFolderId = UUID.Random ();              // assume we need a new ID

                // Asset type has to be Unknown here rather than Folder, otherwise the created folder can't be
                // deleted once the client has relogged.
                // The root folder appears to be labelled AssetType.Folder (shows up as "Category" in the client)
                // even though there is a AssetType.RootCategory
                destFolder
                    = new InventoryFolderBase(
                        newFolderId, newFolderName, m_userInfo.PrincipalID,
                        (short) AssetType.Unknown, destFolder.ID, 1);

                var existingFolder = m_inventoryService.GetUserFolderID (m_userInfo.PrincipalID, newFolderName);
                if (existingFolder == null)
                    m_inventoryService.AddFolder (destFolder);      // add the folder
                else
                    destFolder.ID = (UUID)existingFolder [0];       // use the existing ID

                // Record that we have now created this folder
                iarPathExisting += rawDirsToCreate[i] + "/";
                MainConsole.Instance.DebugFormat("[INVENTORY ARCHIVER]: Created folder {0} from IAR", iarPathExisting);
                resolvedFolders[iarPathExisting] = destFolder;

                if (0 == i && loadedNodes != null)
                    loadedNodes.Add(destFolder);
            }
        }

        /// <summary>
        ///     Load an item from the archive
        /// </summary>
        /// <param name="data">The raw item data</param>
        /// <param name="loadFolder"> </param>
        protected InventoryItemBase LoadItem(byte[] data, InventoryFolderBase loadFolder)
        {
            InventoryItemBase item = UserInventoryItemSerializer.Deserialize(data);


            UUID ospResolvedId = OspResolver.ResolveOspa(item.CreatorId, m_accountService);
            if (UUID.Zero != ospResolvedId)
            {
                item.CreatorIdAsUuid = ospResolvedId;

                // Don't preserve the OSPA in the creator id (which actually gets persisted to the
                // database).  Instead, replace with the UUID that we found.
                item.CreatorId = ospResolvedId.ToString();

                item.CreatorData = string.Empty;
            }
            else if (string.IsNullOrEmpty(item.CreatorData))
            {
                item.CreatorId = m_userInfo.PrincipalID.ToString();
                item.CreatorIdAsUuid = new UUID(item.CreatorId);
            }

            // Don't use the item ID that's in the file, this could be a local user's folder
            //item.ID = UUID.Random();
            item.Owner = m_userInfo.PrincipalID;

        
            // Record the creator id for the item's asset so that we can use it later, if necessary, when the asset
            // is loaded.
            // FIXME: This relies on the items coming before the assets in the TAR file.  Need to create stronger
            // checks for this, and maybe even an external tool for creating OARs which enforces this, rather than
            // relying on native tar tools.
            m_creatorIdForAssetId[item.AssetID] = item.CreatorIdAsUuid;

            // Reset folder ID to the one in which we want to load it
            item.Folder = loadFolder.ID;

            itemsSavedOff.Add(item);

            return item;
        }

        public bool AddInventoryItem(InventoryItemBase item)
        {
            if (UUID.Zero == item.Folder)
            {
                InventoryFolderBase f = m_inventoryService.GetFolderForType(
                    item.Owner,
                    (InventoryType) item.InvType,
                    (AssetType) item.AssetType);

                if (f != null)
                {
                    //                    MainConsole.Instance.DebugFormat(
                    //                        "[LOCAL INVENTORY SERVICES CONNECTOR]: Found folder {0} type {1} for item {2}", 
                    //                        f.Name, (AssetType)f.Type, item.Name);

                    item.Folder = f.ID;
                }
                else
                {
                    f = m_inventoryService.GetRootFolder(item.Owner);
                    if (f != null)
                    {
                        item.Folder = f.ID;
                    }
                    else
                    {
                        MainConsole.Instance.WarnFormat(
                            "[AGENT INVENTORY]: Could not find root folder for {0} when trying to add item {1} with no parent folder specified",
                            item.Owner, item.Name);
                        return false;
                    }
                }
            }

            // check if the folder item exists
            if (!m_inventoryService.FolderItemExists (item.Folder, item.ID))
            {
                if (m_inventoryService.ItemExists (item.ID))
                {
                    // Don't use this item ID as it probably belongs to another local user's folder
                    item.ID = UUID.Random();
                }

                if (!m_inventoryService.AddItem (item))
                {
                    MainConsole.Instance.WarnFormat (
                        "[AGENT INVENTORY]: Agent {0} could not add item {1} {2}",
                        item.Owner, item.Name, item.ID);
                    return false;
                }
            } 
            return true;
        }

        /// <summary>
        ///     Load an asset
        /// </summary>
        /// <param name="assetPath"> </param>
        /// <param name="data"></param>
        /// <returns>true if asset was successfully loaded, false otherwise</returns>
        private bool LoadAsset(string assetPath, byte[] data)
        {
            //IRegionSerialiser serialiser = scene.RequestModuleInterface<IRegionSerialiser>();
            // Right now we're nastily obtaining the UUID from the filename
            string filename = assetPath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);

            int i = filename.LastIndexOf(ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

            if (i == -1)
            {
                MainConsole.Instance.ErrorFormat(
                    "[INVENTORY ARCHIVER]: Could not find extension information in asset path {0} since it's missing the separator {1}.  Skipping",
                    assetPath, ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

                return false;
            }

            string extension = filename.Substring(i);
            string uuid = filename.Remove(filename.Length - extension.Length);
            UUID assetID = UUID.Parse (uuid);

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                AssetType assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                if (assetType == AssetType.Unknown)
                    MainConsole.Instance.WarnFormat(
                        "[INVENTORY ARCHIVER]: Importing {0} byte asset {1} with unknown type", data.Length,
                        uuid);
                else if (assetType == AssetType.Object)
                {
                    string xmlData = Utils.BytesToString(data);
                    ISceneEntity sceneObject = SceneEntitySerializer.SceneObjectSerializer.FromOriginalXmlFormat(
                        xmlData, m_registry);
                    if (sceneObject != null)
                    {
                        if (m_creatorIdForAssetId.ContainsKey(assetID))
                        {
                            foreach (
                                ISceneChildEntity sop in
                                    from sop in sceneObject.ChildrenEntities()
                                    where string.IsNullOrEmpty(sop.CreatorData)
                                    select sop)
                                sop.CreatorID = m_creatorIdForAssetId[assetID];
                        }

                        foreach (ISceneChildEntity sop in sceneObject.ChildrenEntities())
                        {
                            //Fix ownerIDs and perms
                            sop.Inventory.ApplyGodPermissions((uint) PermissionMask.All);
                            sceneObject.ApplyPermissions((uint) PermissionMask.All);
                            foreach (TaskInventoryItem item in sop.Inventory.GetInventoryItems())
                                item.OwnerID = m_userInfo.PrincipalID;
                            sop.OwnerID = m_userInfo.PrincipalID;
                        }

                        data =
                            Utils.StringToBytes(
                                SceneEntitySerializer.SceneObjectSerializer.ToOriginalXmlFormat(sceneObject));
                    }
                }
                //MainConsole.Instance.DebugFormat("[INVENTORY ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(assetID, "From IAR", assetType, m_overridecreator)
                                      {
                                          Data = data,
                                          Flags = AssetFlags.Normal
                                      };
 
                if (m_assetData != null && ReplaceAssets)
                    m_assetData.Delete(asset.ID, true);

                // check if this asset already exists in the database
                if (!m_assetService.GetExists(asset.ID.ToString()))
                    m_assetService.Store(asset);

                return true;
            }
            MainConsole.Instance.ErrorFormat(
                "[INVENTORY ARCHIVER]: Tried to dearchive data with path {0} with an unknown type extension {1}",
                assetPath, extension);

            return false;
        }


        /// <summary>
        /// Loads the archive.xml control file.
        /// </summary>
        /// <param name="data">Data.</param>
        public void LoadControlFile(byte[] data)
        {
            //Create the XmlNamespaceManager.
            ASCIIEncoding m_asciiEncoding = new ASCIIEncoding();
            NameTable nt = new NameTable();
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(nt);

            // Create the XmlParserContext.
            XmlParserContext context = new XmlParserContext(null, nsmgr, null, XmlSpace.None);

            XmlTextReader xtr = new XmlTextReader(m_asciiEncoding.GetString(data), XmlNodeType.Document, context);

             while (xtr.Read())
            {
                if (xtr.NodeType == XmlNodeType.Element)
                {
                    if (xtr.Name == "archive")
                    {
                        int majorVersion = int.Parse (xtr.GetAttribute(0));
                        int minorVersion = int.Parse (xtr.GetAttribute(1));
                        string version = string.Format ("{0}.{1}", majorVersion, minorVersion);

                        MainConsole.Instance.InfoFormat("[INVENTORY ARCHIVER]: Loading version {0} IAR", version);                        

                    }
                    if (xtr.Name == "assets_included")
                    {
                        bool value;
                        if (bool.TryParse(xtr.ReadElementContentAsString(), out value))
                            m_assetsIncluded = value;
                    }
                }
            }
        }

    }
}