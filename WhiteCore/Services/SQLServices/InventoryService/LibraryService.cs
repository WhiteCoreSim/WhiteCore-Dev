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
using Nini.Config;
using Nini.Ini;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.SQLServices.InventoryService
{
    /// <summary>
    ///     Basically a hack to give us a Inventory library while we don't have a inventory server
    ///     once the server is fully implemented then should read the data from that
    /// </summary>
    public class LibraryService : ILibraryService, IService
    {
        // moved to Constants to allow for easier comparison from the WebUI
        // readonly UUID libOwner = new UUID("11111111-1111-0000-0000-000100bba000");
        readonly UUID libOwner = new UUID (Constants.LibraryOwner);

        public UUID LibraryRootFolderID {
            // similarly placed in Constants
            //get { return new UUID("00000112-000f-0000-0000-000100bba000"); }
            get { return new UUID (Constants.LibraryRootFolderID); }
        }

        string libOwnerName = "Library Owner";
        bool m_enabled;
        IRegistryCore m_registry;
        string pLibName = "WhiteCore Library";
        protected IInventoryService m_inventoryService;

        #region ILibraryService Members

        public UUID LibraryOwner {
            get { return libOwner; }
        }

        public string LibraryOwnerName {
            get { return libOwnerName; }
        }

        public string LibraryName {
            get { return pLibName; }
        }

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            string pLibOwnerName = "Library Owner";

            IConfig libConfig = config.Configs ["LibraryService"];
            if (libConfig != null) {
                m_enabled = true;
                pLibName = libConfig.GetString ("LibraryName", pLibName);
                libOwnerName = libConfig.GetString ("LibraryOwnerName", pLibOwnerName);
            }

            //MainConsole.Instance.Debug("[LIBRARY]: Starting library service...");

            registry.RegisterModuleInterface<ILibraryService> (this);
            m_registry = registry;
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            if (m_enabled) {
                if (MainConsole.Instance != null)
                    MainConsole.Instance.Commands.AddCommand (
                        "clear default inventory",
                        "clear default inventory",
                        "Clears the Default Inventory stored for this grid",
                        ClearDefaultInventory, false, true);
            }
        }

        public void FinishedStartup ()
        {
            m_inventoryService = m_registry.RequestModuleInterface<IInventoryService> ();
            LoadLibraries ();
        }

        #endregion

        public void LoadLibraries ()
        {
            if (!m_enabled) {
                return;
            }

            if (!File.Exists ("DefaultInventory/Inventory.ini") &&
                !File.Exists ("DefaultInventory/Inventory.ini.example")) {
                MainConsole.Instance.Error (
                    "Could not find DefaultInventory/Inventory.ini or DefaultInventory/Inventory.ini.example");
                return;
            }

            List<IDefaultLibraryLoader> Loaders = WhiteCoreModuleLoader.PickupModules<IDefaultLibraryLoader> ();
            try {
                if (!File.Exists ("DefaultInventory/Inventory.ini")) {
                    File.Copy ("DefaultInventory/Inventory.ini.example", "DefaultInventory/Inventory.ini");
                }
                IniConfigSource iniSource = new IniConfigSource ("DefaultInventory/Inventory.ini",
                                                                IniFileType.AuroraStyle);
                if (iniSource != null) {
                    foreach (IDefaultLibraryLoader loader in Loaders) {
                        loader.LoadLibrary (this, iniSource, m_registry);
                    }
                }
            } catch {
            }
        }

        void ClearDefaultInventory (IScene scene, string [] cmd)
        {
            string sure = MainConsole.Instance.Prompt ("Are you sure you want to delete the default inventory? (yes/no)", "no");
            if (!sure.Equals ("yes", StringComparison.CurrentCultureIgnoreCase))
                return;
            ClearDefaultInventory ();
        }

        public void ClearDefaultInventory ()
        {

            // get root folders
            List<InventoryFolderBase> rootFolders = m_inventoryService.GetRootFolders (LibraryOwner);

            //Delete the root folder's folders
            foreach (var rFF in rootFolders) {
                List<InventoryFolderBase> rootFolderFolders = m_inventoryService.GetFolderFolders (LibraryOwner, rFF.ID);

                // delete root folders
                foreach (InventoryFolderBase rFolder in rootFolderFolders) {
                    MainConsole.Instance.Info ("Removing folder " + rFolder.Name);
                    m_inventoryService.ForcePurgeFolder (rFolder);
                }
            }

            // remove top level folders
            foreach (InventoryFolderBase rFolder in rootFolders) {
                MainConsole.Instance.Info ("Removing folder " + rFolder.Name);
                m_inventoryService.ForcePurgeFolder (rFolder);
            }

            MainConsole.Instance.Info ("Finished removing default inventory");
            MainConsole.Instance.Info ("[Library]: If a new default inventory is to be loaded, please restart WhiteCore");
        }
    }
}
