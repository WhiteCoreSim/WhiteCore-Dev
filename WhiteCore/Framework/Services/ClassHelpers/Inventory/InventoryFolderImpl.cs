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

namespace WhiteCore.Framework.Services.ClassHelpers.Inventory
{
    public sealed class InventoryFolderImpl : InventoryFolderBase
    {
        public static readonly string PATH_DELIMITER = "/";

        /// <summary>
        ///     Items that are contained in this folder
        /// </summary>
        public Dictionary<UUID, InventoryItemBase> Items = new Dictionary<UUID, InventoryItemBase>();

        /// <summary>
        ///     Child folders that are contained in this folder
        /// </summary>
        private Dictionary<UUID, InventoryFolderImpl> m_childFolders = new Dictionary<UUID, InventoryFolderImpl>();

        // Constructors
        public InventoryFolderImpl(InventoryFolderBase folderbase)
        {
            Owner = folderbase.Owner;
            ID = folderbase.ID;
            Name = folderbase.Name;
            ParentID = folderbase.ParentID;
            Type = folderbase.Type;
            Version = folderbase.Version;
        }

        public InventoryFolderImpl()
        {
        }

        /// <value>
        ///     The total number of items in this folder and in the immediate child folders (though not from other
        ///     descendants).
        /// </value>
        public int TotalCount
        {
            get
            {
                return m_childFolders.Values.Aggregate(Items.Count, (current, folder) => current + folder.TotalCount);
            }
        }

        /// <summary>
        ///     Create a new subfolder.
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="folderName"></param>
        /// <param name="type"></param>
        /// <returns>The newly created subfolder.  Returns null if the folder already exists</returns>
        public InventoryFolderImpl CreateChildFolder(UUID folderID, string folderName, ushort type)
        {
            lock (m_childFolders)
            {
                if (!m_childFolders.ContainsKey(folderID))
                {
                    InventoryFolderImpl subFold = new InventoryFolderImpl
                                                      {
                                                          Name = folderName,
                                                          ID = folderID,
                                                          Type = (short) type,
                                                          ParentID = this.ID,
                                                          Owner = Owner
                                                      };
                    m_childFolders.Add(subFold.ID, subFold);

                    return subFold;
                }
            }

            return null;
        }

        /// <summary>
        ///     Add a folder that already exists.
        /// </summary>
        /// <param name="folder"></param>
        public void AddChildFolder(InventoryFolderImpl folder)
        {
            lock (m_childFolders)
            {
                folder.ParentID = ID;
                m_childFolders[folder.ID] = folder;
            }
        }

        /// <summary>
        ///     Does this folder contain the given child folder?
        /// </summary>
        /// <param name="folderID"></param>
        /// <returns></returns>
        public bool ContainsChildFolder(UUID folderID)
        {
            return m_childFolders.ContainsKey(folderID);
        }

        /// <summary>
        ///     Get a child folder
        /// </summary>
        /// <param name="folderID"></param>
        /// <returns>The folder if it exists, null if it doesn't</returns>
        public InventoryFolderImpl GetChildFolder(UUID folderID)
        {
            InventoryFolderImpl folder = null;

            lock (m_childFolders)
            {
                m_childFolders.TryGetValue(folderID, out folder);
            }

            return folder;
        }

        /// <summary>
        ///     Removes the given child subfolder.
        /// </summary>
        /// <param name="folderID"></param>
        /// <returns>
        ///     The folder removed, or null if the folder was not present.
        /// </returns>
        public InventoryFolderImpl RemoveChildFolder(UUID folderID)
        {
            InventoryFolderImpl removedFolder = null;

            lock (m_childFolders)
            {
                if (m_childFolders.ContainsKey(folderID))
                {
                    removedFolder = m_childFolders[folderID];
                    m_childFolders.Remove(folderID);
                }
            }

            return removedFolder;
        }

        /// <summary>
        ///     Delete all the folders and items in this folder.
        /// </summary>
        public void Purge()
        {
            foreach (InventoryFolderImpl folder in m_childFolders.Values)
            {
                folder.Purge();
            }

            m_childFolders.Clear();
            Items.Clear();
        }

        /// <summary>
        ///     Returns the item if it exists in this folder or in any of this folder's descendant folders
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>null if the item is not found</returns>
        public InventoryItemBase FindItem(UUID itemID)
        {
            lock (Items)
            {
                if (Items.ContainsKey(itemID))
                {
                    return Items[itemID];
                }
            }

            lock (m_childFolders)
            {
                foreach (
                    InventoryItemBase item in
                        m_childFolders.Values.Select(folder => folder.FindItem(itemID)).Where(item => item != null))
                {
                    return item;
                }
            }

            return null;
        }

        public InventoryItemBase FindAsset(UUID assetID)
        {
            lock (Items)
            {
                foreach (InventoryItemBase item in Items.Values.Where(item => item.AssetID == assetID))
                {
                    return item;
                }
            }

            lock (m_childFolders)
            {
                foreach (
                    InventoryItemBase item in
                        m_childFolders.Values.Select(folder => folder.FindAsset(assetID)).Where(item => item != null))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        ///     Deletes an item if it exists in this folder or any children
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public bool DeleteItem(UUID itemID)
        {
            bool found = false;

            lock (Items)
            {
                if (Items.ContainsKey(itemID))
                {
                    Items.Remove(itemID);
                    return true;
                }
            }

            lock (m_childFolders)
            {
                foreach (InventoryFolderImpl folder in m_childFolders.Values)
                {
                    found = folder.DeleteItem(itemID);

                    if (found)
                    {
                        break;
                    }
                }
            }

            return found;
        }

        /// <summary>
        ///     Returns the folder requested if it is this folder or is a descendent of this folder.  The search is depth
        ///     first.
        /// </summary>
        /// <returns>The requested folder if it exists, null if it does not.</returns>
        public InventoryFolderImpl FindFolder(UUID folderID)
        {
            if (folderID == ID)
                return this;

            lock (m_childFolders)
            {
                foreach (
                    InventoryFolderImpl returnFolder in
                        m_childFolders.Values.Select(folder => folder.FindFolder(folderID))
                                      .Where(returnFolder => returnFolder != null))
                {
                    return returnFolder;
                }
            }

            return null;
        }

        /// <summary>
        ///     Look through all child subfolders for a folder marked as one for a particular asset type, and return it.
        /// </summary>
        /// <param name="type"></param>
        /// <returns>Returns null if no such folder is found</returns>
        public InventoryFolderImpl FindFolderForType(int type)
        {
            lock (m_childFolders)
            {
                foreach (InventoryFolderImpl f in m_childFolders.Values.Where(f => f.Type == type))
                {
                    return f;
                }
            }

            return null;
        }

        /// <summary>
        ///     Find a folder given a PATH_DELIMITER delimited path starting from this folder
        /// </summary>
        /// This method does not handle paths that contain multiple delimitors
        /// 
        /// FIXME: We do not yet handle situations where folders have the same name.  We could handle this by some
        /// XPath like expression
        /// 
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// <param name="path">
        ///     The path to the required folder.
        ///     It this is empty or consists only of the PATH_DELIMTER then this folder itself is returned.
        /// </param>
        /// <returns>null if the folder is not found</returns>
        public InventoryFolderImpl FindFolderByPath(string path)
        {
            if (path == string.Empty)
                return this;

            path = path.Trim();

            if (path == PATH_DELIMITER)
                return this;

            string[] components = path.Split(new[] {PATH_DELIMITER}, 2, StringSplitOptions.None);

            lock (m_childFolders)
            {
                foreach (
                    InventoryFolderImpl folder in m_childFolders.Values.Where(folder => folder.Name == components[0]))
                {
                    if (components.Length > 1)
                        return folder.FindFolderByPath(components[1]);
                    else
                        return folder;
                }
            }

            // We didn't find a folder with the given name
            return null;
        }

        /// <summary>
        ///     Find an item given a PATH_DELIMITOR delimited path starting from this folder.
        ///     This method does not handle paths that contain multiple delimitors
        ///     FIXME: We do not yet handle situations where folders or items have the same name.  We could handle this by some
        ///     XPath like expression
        ///     FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// </summary>
        /// <param name="path">
        ///     The path to the required item.
        /// </param>
        /// <returns>null if the item is not found</returns>
        public InventoryItemBase FindItemByPath(string path)
        {
            string[] components = path.Split(new[] {PATH_DELIMITER}, 2, StringSplitOptions.None);

            if (components.Length == 1)
            {
                lock (Items)
                {
                    foreach (InventoryItemBase item in Items.Values.Where(item => item.Name == components[0]))
                    {
                        return item;
                    }
                }
            }
            else
            {
                lock (m_childFolders)
                {
                    foreach (
                        InventoryFolderImpl folder in
                            m_childFolders.Values.Where(folder => folder.Name == components[0]))
                    {
                        return folder.FindItemByPath(components[1]);
                    }
                }
            }

            // We didn't find an item or intermediate folder with the given name
            return null;
        }

        /// <summary>
        ///     Return a copy of the list of child items in this folder.  The items themselves are the originals.
        /// </summary>
        public List<InventoryItemBase> RequestListOfItems()
        {
            List<InventoryItemBase> itemList = new List<InventoryItemBase>();

            lock (Items)
            {
                itemList.AddRange(Items.Values);
            }

            //MainConsole.Instance.DebugFormat("[INVENTORY FOLDER IMPL]: Found {0} items", itemList.Count);

            return itemList;
        }

        /// <summary>
        ///     Return a copy of the list of child folders in this folder.  The folders themselves are the originals.
        /// </summary>
        public List<InventoryFolderBase> RequestListOfFolders()
        {
            List<InventoryFolderBase> folderList = new List<InventoryFolderBase>();

            lock (m_childFolders)
            {
                folderList.AddRange(m_childFolders.Values.Cast<InventoryFolderBase>());
            }

            return folderList;
        }

        public List<InventoryFolderImpl> RequestListOfFolderImpls()
        {
            List<InventoryFolderImpl> folderList = new List<InventoryFolderImpl>();

            lock (m_childFolders)
            {
                folderList.AddRange(m_childFolders.Values);
            }

            return folderList;
        }
    }
}