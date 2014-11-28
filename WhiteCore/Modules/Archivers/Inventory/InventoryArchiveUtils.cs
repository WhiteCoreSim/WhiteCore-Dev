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

using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using OpenMetaverse;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WhiteCore.Modules.Archivers
{
    /// <summary>
    ///     Utility methods for inventory archiving
    /// </summary>
    public static class InventoryArchiveUtils
    {

        // Character used for escaping the path delimter ("\/") and itself ("\\") in human escaped strings
        public static readonly char ESCAPE_CHARACTER = '\\';

        // The character used to separate inventory path components (different folders and items)
        public static readonly char PATH_DELIMITER = '/';

        /// <summary>
        ///     Find a folder given a PATH_DELIMITER delimited path starting from a user's root folder
        /// </summary>
        /// This method does not handle paths that contain multiple delimitors
        /// 
        /// FIXME: We have no way of distinguishing folders with the same path
        /// 
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// <param name="inventoryService">
        ///     Inventory service to query
        /// </param>
        /// <param name="userId">
        ///     User id to search
        /// </param>
        /// <param name="path">
        ///     The path to the required folder.
        ///     It this is empty or consists only of the PATH_DELIMTER then this folder itself is returned.
        /// </param>
        /// <returns>An empty list if the folder is not found, otherwise a list of all folders that match the name</returns>
        public static List<InventoryFolderBase> FindFolderByPath(
            IInventoryService inventoryService, UUID userId, string path)
        {
            InventoryFolderBase rootFolder = inventoryService.GetRootFolder(userId);

            if (rootFolder == null)
            {
                // we don't appear to have any inventory setup yet
                if (!inventoryService.CreateUserInventory (userId, true))
                    return new List<InventoryFolderBase> ();
            }

            return FindFolderByPath(inventoryService, rootFolder, path);
        }

        /// <summary>
        ///     Find a folder given a PATH_DELIMITER delimited path starting from this folder
        /// </summary>
        /// This method does not handle paths that contain multiple delimitors
        /// 
        /// FIXME: We have no way of distinguishing folders with the same path.
        /// 
        /// FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// <param name="inventoryService">
        ///     Inventory service to query
        /// </param>
        /// <param name="startFolder">
        ///     The folder from which the path starts
        /// </param>
        /// <param name="path">
        ///     The path to the required folder.
        ///     It this is empty or consists only of the PATH_DELIMTER then this folder itself is returned.
        /// </param>
        /// <returns>An empty list if the folder is not found, otherwise a list of all folders that match the name</returns>
        public static List<InventoryFolderBase> FindFolderByPath(
            IInventoryService inventoryService, InventoryFolderBase startFolder, string path)
        {
            List<InventoryFolderBase> foundFolders = new List<InventoryFolderBase>();

            if (path == string.Empty)
            {
                foundFolders.Add(startFolder);
                return foundFolders;
            }

            path = path.Trim();

            if (path == PATH_DELIMITER.ToString())
            {
                foundFolders.Add(startFolder);
                return foundFolders;
            }

            // If the path isn't just / then trim any starting extraneous slashes
            path = path.TrimStart(new[] {PATH_DELIMITER});

//            MainConsole.Instance.DebugFormat("[INVENTORY ARCHIVE UTILS]: Adjusted path in FindFolderByPath() is [{0}]", path);

            string[] components = SplitEscapedPath(path);
            components[0] = UnescapePath(components[0]);

            //string[] components = path.Split(new string[] { PATH_DELIMITER.ToString() }, 2, StringSplitOptions.None);

            InventoryCollection contents = inventoryService.GetFolderContent(startFolder.Owner, startFolder.ID);

            foreach (InventoryFolderBase folder in contents.Folders.Where(folder => folder.Name == components[0]))
            {
                if (components.Length > 1)
                    foundFolders.AddRange(FindFolderByPath(inventoryService, folder, components[1]));
                else
                    foundFolders.Add(folder);
            }
            return foundFolders;
        }

        /// <summary>
        ///     Find an item given a PATH_DELIMITOR delimited path starting from the user's root folder.
        ///     This method does not handle paths that contain multiple delimitors
        ///     FIXME: We do not yet handle situations where folders or items have the same name.  We could handle this by some
        ///     XPath like expression
        ///     FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// </summary>
        /// <param name="inventoryService">
        ///     Inventory service to query
        /// </param>
        /// <param name="userId">
        ///     The user to search
        /// </param>
        /// <param name="path">
        ///     The path to the required item.
        /// </param>
        /// <returns>null if the item is not found</returns>
        public static InventoryItemBase FindItemByPath(
            IInventoryService inventoryService, UUID userId, string path)
        {
            InventoryFolderBase rootFolder = inventoryService.GetRootFolder(userId);

            if (rootFolder == null)
            {
                // we don't appear to have any inventory setup yet
                if (!inventoryService.CreateUserInventory (userId, true))
                    return null;                                                // something really wrong
            }
            return FindItemByPath(inventoryService, rootFolder, path);
        }

        /// <summary>
        ///     Find an item given a PATH_DELIMITOR delimited path starting from this folder.
        ///     This method does not handle paths that contain multiple delimitors
        ///     FIXME: We do not yet handle situations where folders or items have the same name.  We could handle this by some
        ///     XPath like expression
        ///     FIXME: Delimitors which occur in names themselves are not currently escapable.
        /// </summary>
        /// <param name="inventoryService">
        ///     Inventory service to query
        /// </param>
        /// <param name="startFolder">
        ///     The folder from which the path starts
        /// </param>
        /// <param name="path">
        ///     The path to the required item.
        /// </param>
        /// <returns>null if the item is not found</returns>
        public static InventoryItemBase FindItemByPath(
            IInventoryService inventoryService, InventoryFolderBase startFolder, string path)
        {
            // If the path isn't just / then trim any starting extraneous slashes
            path = path.TrimStart(new[] {PATH_DELIMITER});

            string[] components = SplitEscapedPath(path);
            components[0] = UnescapePath(components[0]);

            //string[] components = path.Split(new string[] { PATH_DELIMITER }, 2, StringSplitOptions.None);

            if (components.Length == 1)
            {
//                MainConsole.Instance.DebugFormat(
//                    "FOUND SINGLE COMPONENT [{0}].  Looking for this in [{1}] {2}", 
//                    components[0], startFolder.Name, startFolder.ID);

                List<InventoryItemBase> items = inventoryService.GetFolderItems(startFolder.Owner, startFolder.ID);

//                MainConsole.Instance.DebugFormat("[INVENTORY ARCHIVE UTILS]: Found {0} items in FindItemByPath()", items.Count);

                return items.FirstOrDefault(item => item.Name == components[0]);
            }
            else
            {
//                MainConsole.Instance.DebugFormat("FOUND COMPONENTS [{0}] and [{1}]", components[0], components[1]);

                InventoryCollection contents = inventoryService.GetFolderContent(startFolder.Owner, startFolder.ID);

                return (from folder in contents.Folders
                        where folder.Name == components[0]
                        select FindItemByPath(inventoryService, folder, components[1])).FirstOrDefault();
            }
        }

        /// <summary>
        ///     Split a human escaped path into two components if it contains an unescaped path delimiter, or one component
        ///     if no delimiter is present
        /// </summary>
        /// <param name="path"></param>
        /// <returns>
        ///     The split path.  We leave the components in their originally unescaped state (though we remove the delimiter
        ///     which originally split them if applicable).
        /// </returns>
        public static string[] SplitEscapedPath(string path)
        {
//            MainConsole.Instance.DebugFormat("SPLITTING PATH {0}", path);

            bool singleEscapeChar = false;

            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == ESCAPE_CHARACTER && !singleEscapeChar)
                {
                    singleEscapeChar = true;
                }
                else
                {
                    if (PATH_DELIMITER == path[i] && !singleEscapeChar)
                        return new string[2] {path.Remove(i), path.Substring(i + 1)};
                    else
                        singleEscapeChar = false;
                }
            }

            // We didn't find a delimiter
            return new string[1] {path};
        }

        /// <summary>
        ///     Unescapes a human escaped path.  This means that "\\" goes to "\", and "\/" goes to "/"
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string UnescapePath(string path)
        {
//            MainConsole.Instance.DebugFormat("ESCAPING PATH {0}", path);

            StringBuilder sb = new StringBuilder();

            bool singleEscapeChar = false;
            foreach (char t in path)
            {
                if (t == ESCAPE_CHARACTER && !singleEscapeChar)
                    singleEscapeChar = true;
                else
                    singleEscapeChar = false;

                if (singleEscapeChar)
                {
                    if (PATH_DELIMITER == t)
                        sb.Append(PATH_DELIMITER);
                }
                else
                {
                    sb.Append(t);
                }
            }

//            MainConsole.Instance.DebugFormat("ESCAPED PATH TO {0}", sb);

            return sb.ToString();
        }

        /// <summary>
        ///     Escape an archive path.
        /// </summary>
        /// This has to be done differently from human paths because we can't leave in any "/" characters (due to
        /// problems if the archive is built from or extracted to a filesystem
        /// <param name="path"></param>
        /// <returns></returns>
        public static string EscapeArchivePath(string path)
        {
            // Only encode ampersands (for escaping anything) and / (since this is used as general dir separator).
            return path.Replace("&", "&amp;").Replace("/", "&#47;");
        }

        /// <summary>
        ///     Unescape an archive path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string UnescapeArchivePath(string path)
        {
            return path.Replace("&#47;", "/").Replace("&amp;", "&");
        }
    }
}