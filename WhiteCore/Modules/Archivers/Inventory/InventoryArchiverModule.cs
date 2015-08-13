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
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Archivers
{
    /// <summary>
    ///     This module loads and saves WhiteCore inventory archives
    /// </summary>
    public class InventoryArchiverModule : IService, IInventoryArchiverModule
    {
        /// <summary>
        /// The default save/load archive directory.
        /// </summary>
        string m_archiveDirectory = "";

        /// <value>
        ///     All scenes that this module knows about
        /// </value>
        readonly Dictionary<UUID, IScene> m_scenes = new Dictionary<UUID, IScene>();

        /// <value>
        ///     Pending save completions initiated from the console
        /// </value>
        protected List<Guid> m_pendingConsoleSaves = new List<Guid>();

        IRegistryCore m_registry;

        public string Name
        {
            get { return "Inventory Archiver Module"; }
        }

        #region IInventoryArchiverModule Members

        public event InventoryArchiveSaved OnInventoryArchiveSaved;

        public bool ArchiveInventory(
            Guid id, string firstName, string lastName, string invPath, Stream saveStream)
        {
            return ArchiveInventory(id, firstName, lastName, invPath, saveStream, new Dictionary<string, object>());
        }

        public bool ArchiveInventory(
            Guid id, string firstName, string lastName, string invPath, Stream saveStream,
            Dictionary<string, object> options)
        {
            UserAccount userInfo = m_registry.RequestModuleInterface<IUserAccountService>()
                                             .GetUserAccount(null, firstName, lastName);

            if (userInfo != null)
            {
                try
                {
                    bool UseAssets = true;
                    if (options.ContainsKey("assets"))
                    {
                        object Assets;
                        options.TryGetValue("assets", out Assets);
                        bool.TryParse(Assets.ToString(), out UseAssets);
                    }

                    string checkPermissions = "";
                    if (options.ContainsKey("checkPermissions"))
                    {
                        Object temp;
                        if (options.TryGetValue("checkPermissions", out temp))
                            checkPermissions = temp.ToString().ToUpper();
                    }

                    var saveArchive = new InventoryArchiveWriteRequest(
                        id,
                        this,
                        m_registry,
                        userInfo,
                        invPath,
                        saveStream,
                        UseAssets,
                        null,
                        new List<AssetBase>(),
                        checkPermissions);

                    saveArchive.Execute();
                }
                catch (EntryPointNotFoundException e)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                    MainConsole.Instance.Error(e);

                    return false;
                }

                return true;
            }

            return false;
        }

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            m_registry.RegisterModuleInterface<IInventoryArchiverModule>(this);
            if (m_scenes.Count == 0)
            {
                // set default path to user archives
                var defpath = registry.RequestModuleInterface<ISimulationBase>().DefaultDataPath;
                m_archiveDirectory = Path.Combine (defpath, Constants.DEFAULT_USERINVENTORY_DIR);

                OnInventoryArchiveSaved += SaveIARConsoleCommandCompleted;

                if (MainConsole.Instance != null)
                {
                    MainConsole.Instance.Commands.AddCommand(
                        "load iar",
                        "load iar <first> <last> [<IAR path> [<inventory path>]]",
                        "Load user inventory archive (IAR).\n " +
                        "--merge is an option which merges the loaded IAR with existing inventory folders where possible, rather than always creating new ones\n"
                        + "<first> is user's first name." + Environment.NewLine
                        + "<last> is user's last name." + Environment.NewLine
                        + "<IAR path> is the filesystem path or URI from which to load the IAR." + Environment.NewLine
                        + "           If this is not given then 'UserArchives' in the "+ m_archiveDirectory + " directory is used\n"
                        + "<inventory path> is the path inside the user's inventory where the IAR should be loaded." 
                        + "                 (Default is '/iar_import')",
                        HandleLoadIARConsoleCommand, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "save iar",
                        "save iar <first> <last> [<IAR path> [<inventory path>]] [--noassets]",
                        "Save user inventory archive (IAR). <first> is the user's first name." + Environment.NewLine
                        + "<last> is the user's last name." + Environment.NewLine
                        + "<IAR path> is the filesystem path at which to save the IAR." + Environment.NewLine
                        + "           If this is not given then the IAR will be saved in " + m_archiveDirectory + "/UserArchives\n"
                        + "<inventory path> is the path inside the user's inventory for the folder/item to be saved.\n"
                        + "                 (Default is all folders)\n"
                        + " --noassets : if present, save withOUT assets.\n"
                        +"               This version will NOT load on another grid/standalone other than the current grid/standalone!"
                        + "--perm=<permissions> : If present, verify asset permissions before saving.\n"
                        + "   <permissions> can include 'C' (Copy), 'M' (Modify, 'T' (Transfer)",
                        HandleSaveIARConsoleCommand, false, true);

                }
            }
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
        }

        #endregion

        /// <summary>
        ///     Trigger the inventory archive saved event.
        /// </summary>
        protected internal void TriggerInventoryArchiveSaved(
            Guid id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException)
        {
            InventoryArchiveSaved handlerInventoryArchiveSaved = OnInventoryArchiveSaved;
            if (handlerInventoryArchiveSaved != null)
                handlerInventoryArchiveSaved(id, succeeded, userInfo, invPath, saveStream, reportedException);
        }

        public bool ArchiveInventory(
            Guid id, string firstName, string lastName, string invPath, string savePath,
            Dictionary<string, object> options)
        {
            UserAccount userInfo = m_registry.RequestModuleInterface<IUserAccountService>()
                                             .GetUserAccount(null, firstName, lastName);

            if (userInfo != null)
            {
                try
                {
                    bool UseAssets = true;
                    if (options.ContainsKey("assets"))
                    {
                        object Assets;
                        options.TryGetValue("assets", out Assets);
                        bool.TryParse(Assets.ToString(), out UseAssets);
                    }

                    string checkPermissions = null;
                    if (options.ContainsKey("checkPermissions"))
                    {
                        Object temp;
                        if (options.TryGetValue("checkPermissions", out temp))
                            checkPermissions = temp.ToString().ToUpper();
                    }

                    new InventoryArchiveWriteRequest(id, this, m_registry, userInfo, invPath, savePath, UseAssets, checkPermissions).
                        Execute();
                }
                catch (EntryPointNotFoundException e)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream.\n"
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                    MainConsole.Instance.Error(e);

                    return false;
                }

                return true;
            }

            return false;
        }

        public bool DearchiveInventory(
            string firstName, string lastName, string invPath, string loadPath,
            Dictionary<string, object> options)
        {
            UserAccount userInfo = m_registry.RequestModuleInterface<IUserAccountService>()
                                             .GetUserAccount(null, firstName, lastName);

            if (userInfo != null)
            {
                InventoryArchiveReadRequest request;
                bool merge = (options.ContainsKey("merge") && (bool) options["merge"]);

                try
                {
                    request = new InventoryArchiveReadRequest(m_registry, userInfo, invPath, loadPath, merge, UUID.Zero);
                }
                catch (EntryPointNotFoundException e)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream.\n"
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                    MainConsole.Instance.Error(e);

                    return false;
                }

                var loadArchive = request.Execute(false);
                if (loadArchive == null)                         // nothing loaded ??
                    return false;

                return true;
            }

            return false;
        }

        public List<string> GetIARFilenames()
        {
            var retVals = new List<string>();

            if (Directory.Exists (m_archiveDirectory))
            {
                var archives = new List<string> (Directory.GetFiles (m_archiveDirectory, "*.iar"));
                archives.AddRange (new List<string> (Directory.GetFiles (m_archiveDirectory, "*.tgz")));
                foreach (string file in archives)
                    retVals.Add (Path.GetFileNameWithoutExtension (file));
            }

            return retVals;
        }

        /// <summary>
        ///     Load inventory from an inventory file archive
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="cmdparams"></param>
        protected void HandleLoadIARConsoleCommand(IScene scene, string[] cmdparams)
        {
            try
            {
                string iarPath = "IAR Import";             // default path to load IAR

                Dictionary<string, object> options = new Dictionary<string, object>();
                List<string> newParams = new List<string>(cmdparams);
                foreach (string param in cmdparams)
                {
                    if (param.StartsWith("--skip-assets", StringComparison.CurrentCultureIgnoreCase))
                    {
                        options["skip-assets"] = true;
                        newParams.Remove(param);
                    } 

                    if (param.StartsWith("--merge", StringComparison.CurrentCultureIgnoreCase))
                    {
                        options["merge"] = true;
                        iarPath = "/";
                        newParams.Remove(param);
                    }
                }

                string firstName;
                string lastName;

                if (newParams.Count < 3)
                {
                    string[] names;
                    string name = "";

                    do
                    {
                        name = MainConsole.Instance.Prompt("User Name <first last>: ", name);
                        names = name.Split(' ');
                        if (name.ToLower() == "cancel")
                            return;
                    } while (names.Length < 2);
                    firstName = names[0];
                    lastName = names[1];
                } else
                {
                    firstName = newParams[2];
                    lastName = newParams[3];
                }
                string archiveFileName = firstName+"_"+lastName+".iar";         // assume this is the IAR to load initially

                // optional...
                if (newParams.Count > 4)
                    archiveFileName = newParams[4];
                else
                {
                    // confirm iar to load
                    do
                    {
                        archiveFileName = MainConsole.Instance.Prompt("IAR file to load (? for list): ", archiveFileName);
                        if (archiveFileName == "")
                            return;

                        if (archiveFileName == "?")
                        {
                            var archives = GetIARFilenames();
                            if (archives.Count > 0)
                            {
                                MainConsole.Instance.CleanInfo (" Available archives are : ");
                                foreach (string file in archives)
                                    MainConsole.Instance.CleanInfo ("   " + file);
                            } else
                                MainConsole.Instance.CleanInfo ("Sorry, no archives are available.");

                            archiveFileName = "";    
                        }
                    } while (archiveFileName == "");
                }

                // sanity checks...
                var loadPath = PathHelpers.VerifyReadFile(archiveFileName, new List<string>{".iar",".tgz"}, m_archiveDirectory);
                if (loadPath == "")
                {
                    MainConsole.Instance.InfoFormat("   Sorry, IAR file '{0}' not found!", archiveFileName);
                    return;
                }

                if (cmdparams.Length > 5)
                    iarPath = newParams[5];
                if (iarPath == "/")
                    options["merge"] = true;                // always merge if using the root folder

                if (loadPath != "")
                {

                    MainConsole.Instance.InfoFormat(
                        "[Inventory Archiver]: Loading archive {0} to inventory path {1} for {2} {3}",
                        loadPath, iarPath, firstName, lastName);

                    if (DearchiveInventory(firstName, lastName, iarPath, loadPath, options))
                        MainConsole.Instance.InfoFormat(
                            "[Inventory Archiver]: Loaded archive {0} for {1} {2}",
                            loadPath, firstName, lastName);
                }
            }
            catch (InventoryArchiverException e)
            {
                MainConsole.Instance.ErrorFormat("[Inventory Archiver]: {0}", e.Message);
            }
        }

        /// <summary>
        ///     Save inventory to a file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveIARConsoleCommand(IScene scene, string[] cmdparams)
        {
            Dictionary<string, object> options = new Dictionary<string, object> {{"Assets", true}};
            List<string> newParams = new List<string>(cmdparams);
            foreach (string param in cmdparams)
            {
                if (param.StartsWith("--noassets", StringComparison.CurrentCultureIgnoreCase))
                {
                    options["Assets"] = false;
                    newParams.Remove(param);
                }
                if (param.StartsWith("--perm=", StringComparison.CurrentCultureIgnoreCase))
                {
                    options["CheckPermissions"] = param.Substring(7);
                    newParams.Remove(param);
                }

            }

            string firstName;
            string lastName;
            try
            {
                if (newParams.Count < 3)
                {
                    string[] names;
                    string name = "";

                    do
                    {
                        name = MainConsole.Instance.Prompt("User Name <first last>: ", name);
                        names = name.Split(' ');
                        if (name.ToLower() == "cancel")
                            return;
                    } while (names.Length < 2);
                    firstName = names[0];
                    lastName = names[1];
                } else
                {
                    firstName = newParams[2];
                    lastName = newParams[3];
                }


                // optional...
                string iarPath = "/*";
                if (newParams.Count > 5)
                    iarPath = newParams[5];

                // archive name
                string archiveFileName;
                if (newParams.Count < 4)
                {
                    archiveFileName = firstName+"_"+lastName;
                    archiveFileName = MainConsole.Instance.Prompt("IAR file to save: ", archiveFileName);
                } else
                    archiveFileName = newParams[4];
                

                //some file sanity checks
                string savePath;
                savePath = PathHelpers.VerifyWriteFile (archiveFileName, ".iar", m_archiveDirectory, true);

                MainConsole.Instance.InfoFormat(
                    "[Inventory Archiver]: Saving archive {0} using inventory path {1} for {2} {3}",
                    savePath, iarPath, firstName, lastName);

                Guid id = Guid.NewGuid();
                ArchiveInventory(id, firstName, lastName, iarPath, savePath, options);

                lock (m_pendingConsoleSaves)
                    m_pendingConsoleSaves.Add(id);
            }
            catch (InventoryArchiverException e)
            {
                MainConsole.Instance.ErrorFormat("[Inventory Archiver]: {0}", e.Message);
            }
        }

        void SaveIARConsoleCommandCompleted(
            Guid id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException)

        {
            lock (m_pendingConsoleSaves)
            {
                if (m_pendingConsoleSaves.Contains(id))
                    m_pendingConsoleSaves.Remove(id);
                else
                    return;
            }

            if (succeeded)
            {
                MainConsole.Instance.InfoFormat("[Inventory Archiver]: Saved archive for {0} {1}", userInfo.FirstName,
                                                userInfo.LastName);
            }
            else
            {
                MainConsole.Instance.ErrorFormat(
                    "[Inventory Archiver]: Archive save for {0} {1} failed - {2}",
                    userInfo.FirstName, userInfo.LastName, reportedException.Message);
            }
        }
    }
}
