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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Archivers
{
    /// <summary>
    ///     This module loads/saves the avatar's appearance from/down into an "Avatar Archive", also known as an AA.
    /// </summary>
    public class WhiteCoreAvatarAppearanceArchiver : IService, IAvatarAppearanceArchiver
    {
        #region Declares

        IAssetService assetService;
        UuidGatherer assetGatherer;
        IAvatarService avatarService;
        IInventoryService inventoryService;
        IUserAccountService userAccountService;
        IRegistryCore m_registry;
        string m_storeDirectory = Constants.DEFAULT_AVATARARCHIVE_DIR;

        #endregion

        #region IAvatarAppearanceArchiver Members

        public AvatarArchive LoadAvatarArchive(string fileName, UUID principalID)
        {
            AvatarArchive archive = new AvatarArchive();
            UserAccount account = userAccountService.GetUserAccount(null, principalID);
            if (account == null)
            {
                MainConsole.Instance.Error("[AvatarArchive]: User not found!");
                return null;
            }

            // need to be smart here...
            fileName = PathHelpers.VerifyReadFile (fileName, ".aa", m_storeDirectory);
            if (!File.Exists(fileName))
            {
                MainConsole.Instance.Error("[AvatarArchive]: Unable to load from file: file does not exist!");
                return null;
            }
            MainConsole.Instance.Info("[AvatarArchive]: Loading archive from " + fileName);

            archive.FromOSD((OSDMap)OSDParser.DeserializeLLSDXml(File.ReadAllText(fileName)));

            AvatarAppearance appearance = ConvertXMLToAvatarAppearance(archive.BodyMap);

            appearance.Owner = principalID;

            InventoryFolderBase AppearanceFolder = inventoryService.GetFolderForType(account.PrincipalID,
                                                                                     InventoryType.Wearable,
                                                                                     AssetType.Clothing);

            if (AppearanceFolder == null)
            {
                AppearanceFolder = new InventoryFolderBase (); // does not exist so...
                AppearanceFolder.Owner = account.PrincipalID;
                AppearanceFolder.ID = UUID.Random ();  
                AppearanceFolder.Type = (short) InventoryType.Wearable;
            }

            List<InventoryItemBase> items;

            InventoryFolderBase folderForAppearance
                = new InventoryFolderBase(
                    UUID.Random(), archive.FolderName, account.PrincipalID,
                    -1, AppearanceFolder.ID, 1);

            inventoryService.AddFolder(folderForAppearance);

            folderForAppearance = inventoryService.GetFolder(folderForAppearance);

            try
            {
                LoadAssets(archive.AssetsMap);
                appearance = CopyWearablesAndAttachments(account.PrincipalID, UUID.Zero, appearance, folderForAppearance,
                                                         account.PrincipalID, archive.ItemsMap, out items);
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Warn("[AvatarArchiver]: Error loading assets and items, " + ex);
            }

            /*  implement fully if we need to
            // inform the client if needed

            ScenePresence SP;
            MainConsole.Instance.ConsoleScenes[0].TryGetScenePresence(account.PrincipalID, out SP);
            if (SP == null)
                return; // nobody home!

            SP.ControllingClient.SendAlertMessage("Appearance loading in progress...");
            SP.ControllingClient.SendBulkUpdateInventory(folderForAppearance);
            */

            MainConsole.Instance.Info("[AvatarArchive]: Loaded archive from " + fileName);
            archive.Appearance = appearance;
            return archive;
        }

        /// <summary>
        /// Saves the avatar archive.
        /// </summary>
        /// <returns><c>true</c>, if avatar archive was saved, <c>false</c> otherwise.</returns>
        /// <param name="fileName">File name.</param>
        /// <param name="principalID">Principal I.</param>
        /// <param name="folderName">Folder name.</param>
        /// <param name="snapshotUUID">Snapshot UUI.</param>
        /// <param name="isPublic">If set to <c>true</c> is public.</param>
        /// <param name="isPortable">If set to <c>true</c> create a portable archive.</param>
        public bool SaveAvatarArchive(string fileName, UUID principalID, string folderName,
            UUID snapshotUUID, bool isPublic, bool isPortable)
        {
            UserAccount account = userAccountService.GetUserAccount(null, principalID);
            if (account == null)
            {
                MainConsole.Instance.Error("[AvatarArchive]: User not found!");
                return false;
            }

            AvatarAppearance appearance = avatarService.GetAppearance(account.PrincipalID);
            if (appearance == null)
            {
                MainConsole.Instance.Error("[AvatarArchive] Appearance not found!");
                return false;
            }

            string archiveName =  Path.GetFileNameWithoutExtension (fileName);
            string filePath = Path.GetDirectoryName(fileName);

            AvatarArchive archive = new AvatarArchive();
            archive.AssetsMap = new OSDMap();
            archive.ItemsMap = new OSDMap();

            int wearCount = 0;
            foreach (AvatarWearable wear in appearance.Wearables)
            {
                for (int i = 0; i < wear.Count; i++)
                {
                    WearableItem w = wear[i];

                    if (w.AssetID != UUID.Zero)
                    {
                        SaveItem(w.ItemID, ref archive);
                        SaveAsset(w.AssetID, ref archive, isPortable);
                        wearCount++;
                    }
                }
            }
            MainConsole.Instance.InfoFormat("[AvatarArchive] Adding {0} wearables to {1}",wearCount, archiveName);

            int attachCount = 0;
            List<AvatarAttachment> attachments = appearance.GetAttachments();
            foreach (AvatarAttachment a in attachments.Where(a => a.AssetID != UUID.Zero))
            {
                SaveItem(a.ItemID, ref archive);
                SaveAsset(a.AssetID, ref archive, isPortable);
                attachCount++;
            }
            MainConsole.Instance.InfoFormat("[AvatarArchive] Adding {0} attachments to {1}", attachCount, archiveName);

            //InventoryFolderBase clothingFolder = inventoryService.GetFolderForType(principalID, AssetType.Clothing);

            // set details
            archive.Appearance = appearance;
            archive.BodyMap = appearance.Pack();
            archive.FolderName = folderName;
            archive.Snapshot = snapshotUUID;
            archive.IsPublic = isPublic;
            archive.IsPortable = isPortable;

            File.WriteAllText(fileName, OSDParser.SerializeLLSDXmlString(archive.ToOSD()));

            if (snapshotUUID != UUID.Zero)
            {

                ExportArchiveImage (snapshotUUID, archiveName, filePath);
                MainConsole.Instance.Info("[AvatarArchive] Saved archive snapshot");
            }

            MainConsole.Instance.Info("[AvatarArchive] Saved archive to " + fileName);

            return true;
        }

        /// <summary>
        /// Gets all public avatar archives
        /// </summary>
        /// <returns></returns>
        public List<AvatarArchive> GetAvatarArchives()
        {
            var archives = new List<AvatarArchive>();

            if (Directory.Exists(m_storeDirectory))
            {
                foreach (string file in Directory.GetFiles(m_storeDirectory, "*.aa"))
                {
                    try
                    {
                        AvatarArchive archive = new AvatarArchive();
                        archive.FromOSD((OSDMap)OSDParser.DeserializeLLSDXml(File.ReadAllText(file)));
                        if (archive.IsPublic)
                            archives.Add(archive);
                    }
                    catch
                    {
                    }
                }
            }

            return archives;
        }

        /// <summary>
        /// Gets the avatar archive filenames.
        /// </summary>
        /// <returns>The avatar archive filenames without extension.</returns>
        public List<string> GetAvatarArchiveFilenames ()
        {
            return GetAvatarArchiveFilenames (false);
        }

        /// <summary>
        /// Gets the avatar archive filenames.
        /// </summary>
        /// <returns>The avatar archive filenames.</returns>
        public List<string> GetAvatarArchiveFilenames(bool fullName)
        {
            var archives = new List <string> ();
            if (Directory.Exists (m_storeDirectory))
                archives = new List<string> (Directory.GetFiles (m_storeDirectory, "*.aa"));
            else
                return archives;

            if (!fullName)
            {
                var archiveNames = new List<string> ();
                foreach (string file in archives)
                    archiveNames.Add (Path.GetFileNameWithoutExtension (file));
             
                return archiveNames;
            }

            return archives;
        }

        /// <summary>
        /// Gets the avatar archive images.
        /// </summary>
        /// <returns>The avatar archive images.</returns>
        public List<string> GetAvatarArchiveImages()
        {
            var archives = new List<string>( Directory.GetFiles (m_storeDirectory, "*.jpg"));
            var retVals = new List<string>();
            foreach (string file in archives)
                retVals.Add (file);

            return retVals;
        }

        #endregion

        #region Console Commands

        /// <summary>
        /// Handles loading of an avatar archive.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleLoadAvatarArchive(IScene scene, string[] cmdparams)
        {
            string userName;
            string fileName;

            if (cmdparams.Length < 5)
            {
                userName = MainConsole.Instance.Prompt ("Avatar name for archive upload (<first> <last>)", "");
                if (userName == "")
                    return;
            } else
            {
                userName = cmdparams [3] + " " + cmdparams [4];
            }

            UserAccount account = userAccountService.GetUserAccount(null, userName);
            if (account == null)
            {
                MainConsole.Instance.Info("[AvatarArchive]: Sorry, unable to find an account for " + userName +"!");
                return;
            }

            // filename to load
            if (cmdparams.Length < 6)
            {
                do
                {
                    fileName = MainConsole.Instance.Prompt ("Avatar archive filename to load (? for list)", "");
                    if (fileName == "?")
                    {
                        var archives = GetAvatarArchiveFilenames();
                        MainConsole.Instance.CleanInfo (" Available archives are : ");
                        foreach (string avatar in archives)
                            MainConsole.Instance.CleanInfo ("   " + avatar);
                    }
                } while (fileName == "?");

                if (fileName == "")
                    return;
            } else
            {
                fileName = cmdparams [5];
            }
            

            //some file sanity checks
            fileName = PathHelpers.VerifyReadFile (fileName, ".aa", m_storeDirectory);
            if (fileName == "")
                return;

            AvatarArchive archive = LoadAvatarArchive(fileName, account.PrincipalID);
            if (archive != null)
                avatarService.SetAppearance(account.PrincipalID, archive.Appearance);
        }

        /// <summary>
        /// Handles saving of an avatar archive.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSaveAvatarArchive(IScene scene, string[] cmdparams)
        {
            string userName;
            string fileName;
            string foldername;
            UUID snapshotUUID = UUID.Zero;
            bool isPublic = true;
            bool isPortable = false;

            // check for switch options
            var parms = new List <string>();
            for (int i = 3; i < cmdparams.Length;)
            {
                if (cmdparams [i].StartsWith ("--portable"))
                {
                    isPortable = true;
                    i++;
                } else if (cmdparams [i].StartsWith ("--private"))
                {
                    isPublic = false;
                    i++;
                } else if (cmdparams [i].StartsWith ("--snapshot"))
                {
                    snapshotUUID = UUID.Parse (cmdparams [i + 1]);
                    i += 2;
                } else if (cmdparams [i].StartsWith ("--"))
                {
                    MainConsole.Instance.WarnFormat ("Unknown parameter: " + cmdparams [i]);
                    i++;
                } else
                {
                    parms.Add (cmdparams [i]);
                    i++;
                }
            }

            if (parms.Count == 0)
            {
                userName = MainConsole.Instance.Prompt (" Avatar appearance to save (<first> <last>)");
                if (userName == "")
                    return;
            } else if (parms.Count > 1)
            {
                userName = parms [0] + " " + parms [1];
            } else
            {
                MainConsole.Instance.Info ("Error in command format.");
                return;
            }

            UserAccount account = userAccountService.GetUserAccount(null, userName);
            if (account == null)
            {
                MainConsole.Instance.Error("[AvatarArchive]: User '" + userName + "' not found!");
                return;
            }

            if (parms.Count > 2)
            {
                fileName = parms [2];
            } else
            {
                fileName = userName.Replace (" ", "");
                fileName = MainConsole.Instance.Prompt (" Avatar archive filename)", fileName);
                if (fileName == "")
                    return;
            }

            //some file sanity checks
            fileName = PathHelpers.VerifyWriteFile (fileName, ".aa", m_storeDirectory, true);
            if (fileName == "")
                return;

            // check options
            foldername =  (Path.GetFileNameWithoutExtension (fileName));             // use the filename as the default folder
            if (parms.Count > 3)
                foldername = OSD.FromString(cmdparams[3]);
            foldername = foldername.Replace (' ', '_');    

            SaveAvatarArchive(fileName, account.PrincipalID, foldername, snapshotUUID, isPublic, isPortable);
        }

        #endregion

        #region Helpers

        InventoryItemBase GiveInventoryItem(UUID senderId, UUID recipient, InventoryItemBase item,
                                                    InventoryFolderBase parentFolder)
        {
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
                Folder = UUID.Zero,
                NextPermissions = (uint)PermissionMask.All,
                GroupPermissions = (uint)PermissionMask.All,
                EveryOnePermissions = (uint)PermissionMask.All,
                CurrentPermissions = (uint)PermissionMask.All
            };

            //Give full permissions for them

            if (parentFolder == null)
            {
                InventoryFolderBase folder = inventoryService.GetFolderForType(recipient, InventoryType.Unknown,
                                                                               (AssetType)itemCopy.AssetType);

                if (folder != null)
                    itemCopy.Folder = folder.ID;
                else
                {
                    InventoryFolderBase root = inventoryService.GetRootFolder(recipient);

                    if (root != null)
                        itemCopy.Folder = root.ID;
                    else
                        return null; // No destination
                }
            }
            else
                itemCopy.Folder = parentFolder.ID; //We already have a folder to put it in

            itemCopy.GroupID = UUID.Zero;
            itemCopy.GroupOwned = false;
            itemCopy.Flags = item.Flags;
            itemCopy.SalePrice = item.SalePrice;
            itemCopy.SaleType = item.SaleType;

            inventoryService.AddItem(itemCopy);
            return itemCopy;
        }

        AvatarAppearance CopyWearablesAndAttachments(UUID destination, UUID source,
                                                             AvatarAppearance avatarAppearance,
                                                             InventoryFolderBase destinationFolder, UUID agentid,
                                                             OSDMap itemsMap,
                                                             out List<InventoryItemBase> items)
        {
            if (destinationFolder == null)
                throw new Exception("Cannot locate folder(s)");
            items = new List<InventoryItemBase>();

            List<InventoryItemBase> litems = new List<InventoryItemBase>();
            foreach (KeyValuePair<string, OSD> kvp in itemsMap)
            {
                InventoryItemBase item = new InventoryItemBase();
                item.FromOSD((OSDMap)kvp.Value);
                MainConsole.Instance.Info("[AvatarArchive]: Loading item " + item.ID);
                litems.Add(item);
            }
            
            // Wearables
            AvatarWearable[] wearables = avatarAppearance.Wearables;
            MainConsole.Instance.InfoFormat("[AvatarArchive] Adding {0} wearables", wearables.Length);

            for (int i = 0; i < wearables.Length; i++)
            {
                AvatarWearable wearable = wearables[i];
                for (int ii = 0; ii < wearable.Count; ii++)
                {
                    if (wearable[ii].ItemID != UUID.Zero)
                    {
                        // Get inventory item and copy it
                        InventoryItemBase item = inventoryService.GetItem(UUID.Zero, wearable[ii].ItemID);

                        if (item == null)
                        {
                            //Attempt to get from the map if it doesn't already exist on the grid
                            item = litems.First((itm) => itm.ID == wearable[ii].ItemID);
                        }
                        if (item != null)
                        {
                            InventoryItemBase destinationItem = inventoryService.InnerGiveInventoryItem(destination,
                                                                                                        destination,
                                                                                                        item,
                                                                                                        destinationFolder
                                                                                                            .ID,
                                                                                                        false, false);
                            items.Add(destinationItem);
                            MainConsole.Instance.DebugFormat("[AvatarArchive]: Added item {0} to folder {1}",
                                                             destinationItem.ID, destinationFolder.ID);

                            // Wear item
                            AvatarWearable newWearable = new AvatarWearable();
                            newWearable.Wear(destinationItem.ID, destinationItem.AssetID);
                            avatarAppearance.SetWearable(i, newWearable);
                        }
                        else
                        {
                            MainConsole.Instance.WarnFormat("[AvatarArchive]: Unable to transfer {0} to folder {1}",
                                                            wearable[ii].ItemID, destinationFolder.ID);
                        }
                    }
                }
            }

            // Attachments
            List<AvatarAttachment> attachments = avatarAppearance.GetAttachments();
            MainConsole.Instance.InfoFormat("[AvatarArchive] Adding {0} attachments",attachments.Count);

            foreach (AvatarAttachment attachment in attachments)
            {
                int attachpoint = attachment.AttachPoint;
                UUID itemID = attachment.ItemID;

                if (itemID != UUID.Zero)
                {

                    // Get inventory item and copy it
                    InventoryItemBase item = inventoryService.GetItem(UUID.Zero, itemID);

                    if (item == null)
                    {
                        //Attempt to get from the map if it doesn't already exist on the grid
                        item = litems.First((itm) => itm.ID == itemID);
                    }

                    if (item != null)
                    {
                        InventoryItemBase destinationItem = inventoryService.InnerGiveInventoryItem(destination,
                                                                                                    destination, item,
                                                                                                    destinationFolder.ID,
                                                                                                    false, false);
                        items.Add(destinationItem);
                        MainConsole.Instance.DebugFormat("[AvatarArchive]: Added item {0} to folder {1}", destinationItem.ID,
                                                         destinationFolder.ID);

                        // Attach item
                        avatarAppearance.SetAttachment(attachpoint, destinationItem.ID, destinationItem.AssetID);
                        MainConsole.Instance.DebugFormat("[AvatarArchive]: Attached {0}", destinationItem.ID);
                    }
                    else
                    {
                        MainConsole.Instance.WarnFormat("[AvatarArchive]: Error transferring {0} to folder {1}", itemID,
                                                        destinationFolder.ID);
                    }
                }
            }
            return avatarAppearance;
        }

        AvatarAppearance ConvertXMLToAvatarAppearance(OSDMap map)
        {
            AvatarAppearance appearance = new AvatarAppearance();
            appearance.Unpack(map);
            return appearance;
        }

        void SaveAsset(UUID AssetID, ref AvatarArchive archive, bool isPortable)
        {
            IDictionary<UUID, AssetType> assetUuids = new Dictionary<UUID, AssetType> (); 

            AssetBase assetBase = assetService.Get(AssetID.ToString());
            if (assetBase == null)
                return;

             if (isPortable)
                assetGatherer.GatherAssetUuids (assetBase.ID, assetBase.TypeAsset, assetUuids);
            else
                // we need this one at least
                assetUuids [assetBase.ID] = assetBase.TypeAsset;
            
            // save the required assets
            foreach (KeyValuePair<UUID, AssetType> kvp in assetUuids)
            {
                var asset = assetService.Get(kvp.Key.ToString());
                if (asset != null)
                {
                    MainConsole.Instance.Debug("[AvatarArchive]: Saving asset " + asset.ID);
                    archive.AssetsMap[asset.ID.ToString()] = asset.ToOSD();
                }
                else
                {
                    MainConsole.Instance.Debug("[AvatarArchive]: Could not find asset to save: " + asset.ID);
                    return;
                }
            }
        }

        AssetBase LoadAssetBase(OSDMap map)
        {
            AssetBase asset = new AssetBase();
            asset.FromOSD(map);
            return asset;
        }

        void SaveItem(UUID ItemID, ref AvatarArchive archive)
        {
            InventoryItemBase saveItem = inventoryService.GetItem(UUID.Zero, ItemID);
            if (saveItem == null)
            {
                MainConsole.Instance.Warn("[AvatarArchive]: Could not find item to save: " + ItemID);
                return;
            }
            MainConsole.Instance.Info("[AvatarArchive]: Saving item " + ItemID);
            archive.ItemsMap[ItemID.ToString()] = saveItem.ToOSD();
        }

        void LoadAssets(OSDMap assets)
        {
            foreach (KeyValuePair<string, OSD> kvp in assets)
            {
                UUID AssetID = UUID.Parse(kvp.Key);
                OSDMap assetMap = (OSDMap) kvp.Value;

                // check if this assets alreasy exists in the database
                AssetBase asset = assetService.Get(AssetID.ToString(), false);
                if (asset == null) // Only save if it does not exist
                {
                    MainConsole.Instance.Info ("[AvatarArchive]: Saving asset " + AssetID);

                    asset = LoadAssetBase (assetMap);
                    asset.ID = assetService.Store (asset);
                } else
                    MainConsole.Instance.Debug ("[Avatararchive]: Asset " + AssetID + " already exists.");
            }
        }

        void ExportArchiveImage(UUID imageUUID, string archiveName, string filePath)
        {
            byte[] jpeg;

            using (MemoryStream imgstream = new MemoryStream())
            {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                // non-async because we know we have the asset immediately.
                byte[] imageAsset = assetService.GetData(imageUUID.ToString());

                if (imageAsset != null)
                {
                    // Decode image to System.Drawing.Image
                    Image image;
                    ManagedImage managedImage;
                    if (OpenJPEG.DecodeToImage(imageAsset, out managedImage, out image))
                    {
                        // Save to bitmap
                        using (Bitmap texture = ResizeBitmap(image, 256, 256, archiveName))
                        {
                            EncoderParameters myEncoderParameters = new EncoderParameters();
                            myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 75L);

                            // Save bitmap to stream
                            texture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);

                            // Write the stream to a byte array for output
                            jpeg = imgstream.ToArray();

                            // save image
                            string fileName = archiveName + ".jpg";
                            string fullPath = Path.Combine(filePath, fileName);
                            File.WriteAllBytes(fullPath, jpeg);

                        }
                        image.Dispose();
                    }
                }
            }
        }

        Bitmap ResizeBitmap(Image b, int nWidth, int nHeight, string name)
        {
            Bitmap newsize = new Bitmap(nWidth, nHeight);
            Graphics temp = Graphics.FromImage(newsize);
            temp.DrawImage(b, 0, 0, nWidth, nHeight);
            temp.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (name != "")
                temp.DrawString(name, new Font("Arial", 8, FontStyle.Regular),
                    new SolidBrush(Color.FromArgb(90, 255, 255, 180)), new Point(2, nHeight - 13));

            return newsize;
        }

        // From MSDN
        static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig avatarConfig = config.Configs["FileBasedSimulationData"];
            if (avatarConfig != null)
            {
                m_storeDirectory =
                    PathHelpers.ComputeFullPath (avatarConfig.GetString ("AvatarArchiveDirectory", m_storeDirectory));
                if (m_storeDirectory == "")
                    m_storeDirectory = Constants.DEFAULT_AVATARARCHIVE_DIR;
            }

            bool remoteCalls = false;
            IConfig connectorConfig = config.Configs ["WhiteCoreConnectors"];
            if ((connectorConfig != null) && connectorConfig.Contains ("DoRemoteCalls"))
                remoteCalls = connectorConfig.GetBoolean ("DoRemoteCalls", false);

            // Lock out if remote 
            if (!remoteCalls)
            {
                if (MainConsole.Instance != null)
                {
                    MainConsole.Instance.Commands.AddCommand (
                        "save avatar archive",
                        "save avatar archive [<First> <Last> [<Filename>]] [FolderNameToSaveInto] (--snapshot <UUID>) (--private) (--portable)",
                        "Saves appearance to an avatar archive (.aa is the recommended file extension)\n" +
                        " Note: Put \"\" around the FolderName if you have spaces. \n" +
                        "     : e.g. \"../Data/MyAvatars/Male Avatar.aa\" \n" +
                    //"  Put all attachments in BodyParts folder before saving the archive) \n" +
                        "   --snapshot --private and --portable are optional.\n" +
                        "   --snapshot sets a picture to display on the web interface if this archive is being used as a default avatar.\n" +
                        "   --private tells any web interfaces that they cannot display this as a default avatar.\n" +
                        "   --portable includes full asset tells any web interfaces that they cannot display this as a default avatar.",
                        HandleSaveAvatarArchive, false, true);

                    MainConsole.Instance.Commands.AddCommand (
                        "load avatar archive",
                        "load avatar archive [<First> <Last> [<Filename>]]",
                        "Loads appearance from an avatar archive",
                        HandleLoadAvatarArchive, false, true);
                }
            }
        }


        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            userAccountService = registry.RequestModuleInterface<IUserAccountService>();
            avatarService = registry.RequestModuleInterface<IAvatarService>();
            assetService = registry.RequestModuleInterface<IAssetService>();
            assetGatherer = new UuidGatherer(assetService);
            inventoryService = registry.RequestModuleInterface<IInventoryService>();
            m_registry.RegisterModuleInterface<IAvatarAppearanceArchiver>(this);
        }

        public void FinishedStartup()
        {
        }

        #endregion
    }
}