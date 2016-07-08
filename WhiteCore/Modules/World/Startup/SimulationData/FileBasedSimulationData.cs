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
using System.IO;
using System.Linq;
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using ProtoBuf;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Utilities;
using WhiteCore.Region;
using Timer = System.Timers.Timer;

namespace WhiteCore.Modules
{
    /// <summary>
    ///     FileBased DataStore, do not store anything in any databases, instead save .sim files for it
    /// </summary>
    public class FileBasedSimulationData : ISimulationDataStore, IDisposable
    {
        protected object m_saveLock = new object ();

        protected Timer m_backupSaveTimer;
        protected Timer m_saveTimer;

        protected string m_fileName = "";
        protected string m_storeDirectory = "";
        protected bool m_keepOldSave = true;
        protected string m_oldSaveDirectory = "";
        protected bool m_oldSaveHasBeenSaved;
        protected bool m_requiresSave = true;
        protected bool m_displayNotSavingNotice = true;
        protected bool m_saveBackupChanges = true;
        protected bool m_saveBackups;
        protected int m_removeArchiveDays = 30;
        protected bool m_saveChanges = true;
        protected IScene m_scene;
        protected int m_timeBetweenBackupSaves = 1440; //One day
        protected int m_timeBetweenSaves = 5;
        protected bool m_shutdown = false;
        protected IRegionDataLoader _regionLoader;
        protected IRegionDataLoader _oldRegionLoader;
        protected RegionData _regionData;
        protected string [] m_regionNameSeed;

        #region ISimulationDataStore Members

        public bool MapTileNeedsGenerated { get; set; }

        public virtual string Name {
            get { return "FileBasedDatabase"; }
        }

        public bool SaveBackups {
            get { return m_saveBackups; }
            set { m_saveBackups = value; }
        }

        public string BackupFile {
            get { return m_fileName; }
            set { m_fileName = value; }
        }

        public virtual void CacheDispose ()
        {
            _regionData.Dispose ();
            _regionData = null;
        }

        public FileBasedSimulationData ()
        {
            _oldRegionLoader = new TarRegionDataLoader ();
            _regionLoader = new ProtobufRegionDataLoader ();
        }

        public void Initialise ()
        {
            MainConsole.Instance.Commands.AddCommand (
                "update region info",
                "update region info",
                "Updates the region settings",
                UpdateRegionInfo, true, true);

            MainConsole.Instance.Commands.AddCommand (
                "update region prims",
                "update region prims [amount]",
                "Update the region prim capacity",
                UpdateRegionPrims, true, true);

            MainConsole.Instance.Commands.AddCommand (
                "delete sim backups",
                "delete sim backups [days]",
                "Removes old region backup files older than [days] (default: " + m_removeArchiveDays + " days)",
                CleanupRegionBackups,
                false, true);

        }

        public virtual List<string> FindRegionInfos (out bool newRegion, ISimulationBase simBase)
        {
            ReadConfig (simBase);
            MainConsole.Instance.Info ("Retrieving region data from: " + m_storeDirectory);
            if (m_keepOldSave)
                MainConsole.Instance.Info ("Region archives saved to:    " + m_oldSaveDirectory);

            List<string> regions = new List<string> (Directory.GetFiles (m_storeDirectory, "*.sim", SearchOption.TopDirectoryOnly));
            newRegion = regions.Count == 0;
            List<string> retVals = new List<string> ();

            foreach (string r in regions)
                if (Path.GetExtension (r) == ".sim") {
                    MainConsole.Instance.Info ("Found: " + Path.GetFileNameWithoutExtension (r));
                    retVals.Add (Path.GetFileNameWithoutExtension (r));
                }

            return retVals;
        }

        public virtual List<string> FindRegionBackupFiles (string regionName)
        {
            if ((m_oldSaveDirectory == "") || (regionName == null))
                return null;
            regionName += "--";                                 // name & timestamp delimiter

            List<string> regionBaks = new List<string> ();
            List<string> allBackups = FindBackupRegionFiles ();
            if (allBackups != null) {

                foreach (string regBak in allBackups) {
                    if (Path.GetFileName (regBak).StartsWith (regionName, StringComparison.Ordinal)) {
                        //        MainConsole.Instance.Debug ("Found: " + Path.GetFileNameWithoutExtension (regBak));
                        regionBaks.Add (regBak);
                    }
                }
            }
            return regionBaks;
        }

        public virtual List<string> FindBackupRegionFiles ()
        {
            if (m_oldSaveDirectory == "")
                return null;

            List<string> archives = new List<string> (Directory.GetFiles (m_oldSaveDirectory, "*.sim", SearchOption.TopDirectoryOnly));
            return archives;
        }

        public string GetLastBackupFileName (string regionName)
        {
            List<string> backups = FindRegionBackupFiles (regionName);
            if (backups == null)
                return "";

            // we have backups.. find the last one...
            DateTime mostRecent = DateTime.Now.AddDays (-7);

            string lastBackFile = "";
            foreach (string bak in backups) {
                if (File.GetLastWriteTime (bak) > mostRecent)
                    lastBackFile = bak;
            }

            return lastBackFile;
        }

        public virtual RegionInfo CreateNewRegion (ISimulationBase simBase, Dictionary<string, int> currentInfo)
        {
            ReadConfig (simBase);
            _regionData = new RegionData ();
            _regionData.Init ();
            RegionInfo info = CreateRegionFromConsole (null, true, currentInfo);
            if (info == null)
                return CreateNewRegion (simBase, currentInfo);

            BackupFile = info.RegionName;
            return info;
        }

        public virtual RegionInfo CreateNewRegion (ISimulationBase simBase, string regionName, Dictionary<string, int> currentInfo)
        {
            ReadConfig (simBase);
            _regionData = new RegionData ();
            _regionData.Init ();
            RegionInfo info = new RegionInfo ();
            info.RegionName = regionName;
            info.NewRegion = true;

            info = CreateRegionFromConsole (info, true, currentInfo);
            if (info == null)
                return CreateNewRegion (simBase, info, currentInfo);

            BackupFile = info.RegionName;
            return info;
        }

        /// <summary>
        /// Initializes a new region using the passed regioninfo
        /// </summary>
        /// <returns></returns>
        /// <param name="simBase">Sim base.</param>
        /// <param name="regionInfo">Region info.</param>
        /// <param name="currentInfo">Current region info.</param>
        public virtual RegionInfo CreateNewRegion (ISimulationBase simBase, RegionInfo regionInfo, Dictionary<string, int> currentInfo)
        {
            ReadConfig (simBase);
            _regionData = new RegionData ();
            _regionData.Init ();

            // something wrong here, prompt for details
            if (regionInfo == null)
                return CreateNewRegion (simBase, currentInfo);

            BackupFile = regionInfo.RegionName;

            if (m_scene != null) {
                IGridRegisterModule gridRegister = m_scene.RequestModuleInterface<IGridRegisterModule> ();
                //Re-register so that if the position has changed, we get the new neighbors
                gridRegister.RegisterRegionWithGrid (m_scene, true, false, null);

                ForceBackup ();

                MainConsole.Instance.Info ("[FileBasedSimulationData]: Save completed.");
            }

            return regionInfo;
        }

        public virtual RegionInfo LoadRegionInfo (string fileName, ISimulationBase simBase)
        {
            ReadConfig (simBase);
            ReadBackup (fileName);
            BackupFile = fileName;

            return _regionData.RegionInfo;
        }

        public virtual RegionInfo LoadRegionNameInfo (string regionName, ISimulationBase simBase)
        {
            ReadConfig (simBase);
            _regionData = new RegionData ();
            _regionData.Init ();

            string regionFile = Path.Combine (m_storeDirectory, regionName + ".sim");
            if (File.Exists (regionFile)) {
                regionFile = Path.GetFileNameWithoutExtension (regionFile);
                ReadBackup (regionFile);
                BackupFile = regionFile;
            }

            return _regionData.RegionInfo;
        }

        /// <summary>
        /// Creates/updates a region from console.
        /// </summary>
        /// <returns>The region from console.</returns>
        /// <param name="info">Info.</param>
        /// <param name="prompt">If set to <c>true</c> prompt.</param>
        /// <param name="currentInfo">Current info.</param>
        RegionInfo CreateRegionFromConsole (RegionInfo info, bool prompt, Dictionary<string, int> currentInfo)
        {

            if (info == null || info.NewRegion) {
                if (info == null)
                    info = new RegionInfo ();

                info.RegionID = UUID.Random ();

                if (currentInfo != null) {
                    info.RegionLocX = currentInfo ["minX"] > 0 ? currentInfo ["minX"] : 1000 * Constants.RegionSize;
                    info.RegionLocY = currentInfo ["minY"] > 0 ? currentInfo ["minY"] : 1000 * Constants.RegionSize;
                    info.RegionPort = currentInfo ["port"] > 0 ? currentInfo ["port"] + 1 : 9000;
                } else {
                    info.RegionLocX = 1000 * Constants.RegionSize;
                    info.RegionLocY = 1000 * Constants.RegionSize;
                    info.RegionPort = 9000;

                }
                prompt = true;
            }

            // prompt for user input
            if (prompt) {
                GetRegionName (ref info);
                GetRegionLocation (ref info);
                GetRegionSize (ref info);

                bool bigRegion = ((info.RegionSizeX > Constants.MaxRegionSize) || (info.RegionSizeY > Constants.MaxRegionSize));

                // * Mainland / Full Region (Private)
                // * Mainland / Homestead
                // * Mainland / Openspace
                //
                // * Estate / Full Region   (Private)
                // * Estate / Homestead
                // * Estate / Openspace
                //
                // * WhiteCore Home / Full Region (Private)
                //
                info.RegionType = MainConsole.Instance.Prompt ("Region Type (Mainland / Estate / Homes)",
                    (info.RegionType == "" ? "Estate" : info.RegionType));

                // Region presets or advanced setup
                string setupMode;
                string terrainOpen = "Grassland";
                string terrainFull = "Grassland";
                var responses = new List<string> ();
                if (info.RegionType.ToLower ().StartsWith ("m", StringComparison.Ordinal)) {
                    // Mainland regions
                    info.RegionType = "Mainland / ";
                    responses.Add ("Full Region");
                    responses.Add ("Homestead");
                    responses.Add ("Openspace");
                    setupMode = MainConsole.Instance.Prompt ("Mainland region type?", "Full Region", responses).ToLower ();

                    // allow specifying terrain for Openspace
                    if (bigRegion)
                        terrainOpen = "flatland";
                    else if (setupMode.StartsWith ("o", StringComparison.Ordinal))
                        terrainOpen = MainConsole.Instance.Prompt ("Openspace terrain ( Grassland, Swamp, Aquatic)?", terrainOpen).ToLower ();

                } else if (info.RegionType.ToLower ().StartsWith ("e", StringComparison.Ordinal)) {
                    // Estate regions
                    info.RegionType = "Estate / ";
                    responses.Add ("Full Region");
                    responses.Add ("Homestead");
                    responses.Add ("Openspace");
                    setupMode = MainConsole.Instance.Prompt ("Estate region type?", "Full Region", responses).ToLower ();
                } else {
                    info.RegionType = "WhiteCore Homes / ";
                    responses.Add ("Full Region");
                    setupMode = MainConsole.Instance.Prompt ("Estate region type?", "Full Region", responses).ToLower ();
                }

                // terrain can be specified for Full or custom regions
                if (bigRegion)
                    terrainFull = "Flatland";
                if (setupMode.StartsWith ("f", StringComparison.Ordinal)) {
                    // 'Region land types' setup
                    var tresp = new List<string> ();
                    tresp.Add ("Flatland");
                    tresp.Add ("Grassland");
                    tresp.Add ("Hills");
                    tresp.Add ("Mountainous");
                    tresp.Add ("Island");
                    tresp.Add ("Swamp");
                    tresp.Add ("Aquatic");
                    string tscape = MainConsole.Instance.Prompt ("Terrain Type?", terrainFull, tresp);
                    terrainFull = tscape;
                    // TODO: This would be where we allow selection of preset terrain files
                }

                if (setupMode.StartsWith ("o", StringComparison.Ordinal)) {
                    // 'Openspace' setup
                    info.RegionType = info.RegionType + "Openspace";

                    if (terrainOpen.StartsWith ("a", StringComparison.Ordinal))
                        info.RegionTerrain = "Aquatic";
                    else if (terrainOpen.StartsWith ("s", StringComparison.Ordinal))
                        info.RegionTerrain = "Swamp";
                    else if (terrainOpen.StartsWith ("g", StringComparison.Ordinal))
                        info.RegionTerrain = "Grassland";
                    else
                        info.RegionTerrain = "Flatland";

                    info.Startup = StartupType.Medium;
                    info.SeeIntoThisSimFromNeighbor = true;
                    info.InfiniteRegion = false;
                    info.ObjectCapacity = 750;
                    info.RegionSettings.AgentLimit = 10;
                    info.RegionSettings.AllowLandJoinDivide = false;
                    info.RegionSettings.AllowLandResell = false;
                }

                if (setupMode.StartsWith ("h", StringComparison.Ordinal)) {
                    // 'Homestead' setup
                    info.RegionType = info.RegionType + "Homestead";
                    if (bigRegion)
                        info.RegionTerrain = "Flatland";
                    else
                        info.RegionTerrain = "Homestead";

                    info.Startup = StartupType.Medium;
                    info.SeeIntoThisSimFromNeighbor = true;
                    info.InfiniteRegion = false;
                    info.ObjectCapacity = 3750;
                    info.RegionSettings.AgentLimit = 20;
                    info.RegionSettings.AllowLandJoinDivide = false;
                    info.RegionSettings.AllowLandResell = false;
                }

                if (setupMode.StartsWith ("f", StringComparison.Ordinal)) {
                    // 'Full Region' setup
                    info.RegionType = info.RegionType + "Full Region";
                    info.RegionTerrain = terrainFull;
                    info.Startup = StartupType.Normal;
                    info.SeeIntoThisSimFromNeighbor = true;
                    info.InfiniteRegion = false;
                    info.ObjectCapacity = 15000;
                    info.RegionSettings.AgentLimit = 100;
                    if (info.RegionType.StartsWith ("M", StringComparison.Ordinal))                           // defaults are 'true'
                    {
                        info.RegionSettings.AllowLandJoinDivide = false;
                        info.RegionSettings.AllowLandResell = false;
                    } else if (info.RegionType.StartsWith ("H", StringComparison.Ordinal))                    // Homes always have 25000 prims
                      {
                        info.RegionSettings.AllowLandJoinDivide = true;
                        info.RegionSettings.AllowLandResell = true;
                        info.ObjectCapacity = 25000;
                    }
                }

                // re-proportion allocations based on actual region area <> std area
                var regFactor = (info.RegionSizeX * info.RegionSizeY) / (Constants.RegionSize * Constants.RegionSize);
                info.ObjectCapacity = (int)Math.Round ((float)(info.ObjectCapacity * regFactor));
                info.RegionSettings.AgentLimit = (int)Math.Round ((float)(info.RegionSettings.AgentLimit * regFactor));
            }

            // are we updating or adding??
            if (m_scene != null) {
                IGridRegisterModule gridRegister = m_scene.RequestModuleInterface<IGridRegisterModule> ();
                //Re-register so that if the position has changed, we get the new neighbors
                gridRegister.RegisterRegionWithGrid (m_scene, true, false, null);

                // Tell clients about changes
                IEstateModule es = m_scene.RequestModuleInterface<IEstateModule> ();
                if (es != null)
                    es.sendRegionHandshakeToAll ();

                // in case we have changed the name
                if (m_scene.SimulationDataService.BackupFile != info.RegionName) {
                    string oldFile = BuildSaveFileName (m_scene.SimulationDataService.BackupFile);
                    if (File.Exists (oldFile))
                        File.Delete (oldFile);
                    m_scene.SimulationDataService.BackupFile = info.RegionName;
                }

                m_scene.SimulationDataService.ForceBackup ();

                MainConsole.Instance.InfoFormat ("[FileBasedSimulationData]: Save of {0} completed.", info.RegionName);
            }

            return info;
        }


        /// <summary>
        /// Modifies the region settings.
        /// </summary>
        /// <returns>The region settings.</returns>
        /// <param name="regInfo">Reg info.</param>
        /// <param name="advanced">If set to <c>true</c> advanced.</param>
        bool ModifyRegionSettings (ref RegionInfo regInfo, bool advanced)
        {
            bool updated = false;
            if (regInfo == null || regInfo.NewRegion)
                return updated;

            updated |= GetRegionName (ref regInfo);
            updated |= GetRegionLocation (ref regInfo);
            updated |= GetRegionSize (ref regInfo);

            if (advanced) {
                // region type
                var oldType = regInfo.RegionType;
                regInfo.RegionType = MainConsole.Instance.Prompt ("Region Type (Mainland/Estate)",
                    (regInfo.RegionType == "" ? "Estate" : regInfo.RegionType));
                if (regInfo.RegionType != oldType)
                    updated = true;

                updated |= GetRegionOptional (ref regInfo);
            }

            return updated;
        }

        /// <summary>
        /// Gets the name of the region.
        /// </summary>
        /// <returns><c>true</c>, if region name was gotten, <c>false</c> otherwise.</returns>
        /// <param name="regInfo">Reg info.</param>
        bool GetRegionName (ref RegionInfo regInfo)
        {
            var updated = false;

            Utilities.MarkovNameGenerator rNames = new Utilities.MarkovNameGenerator ();
            string regionName = rNames.FirstName (m_regionNameSeed == null ? Utilities.RegionNames : m_regionNameSeed, 3, 7);

            regionName = regInfo.RegionName;
            var oldName = regionName;

            do {
                regInfo.RegionName = MainConsole.Instance.Prompt ("Region Name (? for suggestion)", regionName);
                if (regInfo.RegionName == "" || regInfo.RegionName == "?") {
                    regionName = rNames.NextName;
                    regInfo.RegionName = "";
                    continue;
                }
            } while (regInfo.RegionName == "");
            rNames.Reset ();
            if (regInfo.RegionName != oldName)
                updated = true;

            return updated;
        }

        /// <summary>
        /// Gets the region location.
        /// </summary>
        /// <returns><c>true</c>, if region location was gotten, <c>false</c> otherwise.</returns>
        /// <param name="regInfo">Reg info.</param>
        bool GetRegionLocation (ref RegionInfo regInfo)
        {
            var updated = false;
            var loc = regInfo.RegionLocX;
            regInfo.RegionLocX =
                int.Parse (MainConsole.Instance.Prompt ("Region Location X",
                    ((regInfo.RegionLocX == 0
                        ? 1000
                        : regInfo.RegionLocX / Constants.RegionSize)).ToString ())) * Constants.RegionSize;
            if (regInfo.RegionLocX != loc)
                updated = true;

            loc = regInfo.RegionLocY;
            regInfo.RegionLocY =
                int.Parse (MainConsole.Instance.Prompt ("Region location Y",
                    ((regInfo.RegionLocY == 0
                        ? 1000
                        : regInfo.RegionLocY / Constants.RegionSize)).ToString ())) * Constants.RegionSize;
            if (regInfo.RegionLocY != loc)
                updated = true;

            //loc = regInfo.RegionLocZ;
            //regInfo.RegionLocZ =
            //        int.Parse (MainConsole.Instance.Prompt ("Region location Z",
            //            ((regInfo.RegionLocZ == 0 
            //            ? 0 
            //            : regInfo.RegionLocZ / Constants.RegionSize)).ToString ())) * Constants.RegionSize;
            //if (regInfo.RegionLocZ != loc)
            //    updated = true;

            return updated;
        }

        /// <summary>
        /// Gets the size of the region.
        /// </summary>
        /// <returns><c>true</c>, if region size was gotten, <c>false</c> otherwise.</returns>
        /// <param name="regInfo">Region info.</param>
        bool GetRegionSize (ref RegionInfo regInfo)
        {
            var updated = false;
            var haveSize = true;
            var sizeCheck = "";
            var oldSize = regInfo.RegionSizeX;
            do {
                regInfo.RegionSizeX = int.Parse (MainConsole.Instance.Prompt ("Region size X", regInfo.RegionSizeX.ToString ()));
                if (regInfo.RegionSizeX > Constants.MaxRegionSize) {
                    MainConsole.Instance.CleanInfo ("    The currently recommended maximum size is " + Constants.MaxRegionSize);
                    sizeCheck = MainConsole.Instance.Prompt ("Continue with the X size of " + regInfo.RegionSizeX + "? (yes/no)", "no");
                    haveSize = sizeCheck.ToLower ().StartsWith ("y", StringComparison.Ordinal);
                }
            } while (!haveSize);
            if (regInfo.RegionSizeX != oldSize)
                updated = true;

            // assume square regions
            regInfo.RegionSizeY = regInfo.RegionSizeX;
            oldSize = regInfo.RegionSizeY;
            do {
                regInfo.RegionSizeY = int.Parse (MainConsole.Instance.Prompt ("Region size Y", regInfo.RegionSizeY.ToString ()));
                if ((regInfo.RegionSizeY > regInfo.RegionSizeX) && (regInfo.RegionSizeY > Constants.MaxRegionSize)) {
                    MainConsole.Instance.CleanInfo ("    The currently recommended maximum size is " + Constants.MaxRegionSize);
                    sizeCheck = MainConsole.Instance.Prompt ("Continue with the Y size of " + regInfo.RegionSizeY + "? (yes/no)", "no");
                    haveSize = sizeCheck.ToLower ().StartsWith ("y", StringComparison.Ordinal);
                }
            } while (!haveSize);
            if (regInfo.RegionSizeY != oldSize)
                updated = true;

            return updated;
        }

        /// <summary>
        /// Gets the region optional settings.
        /// </summary>
        /// <returns><c>true</c>, if region optional was gotten, <c>false</c> otherwise.</returns>
        /// <param name="regInfo">Reg info.</param>
        bool GetRegionOptional (ref RegionInfo regInfo)
        {
            var updated = false;

            // allow port selection
            var oldPort = regInfo.RegionPort;
            regInfo.RegionPort = int.Parse (MainConsole.Instance.Prompt ("Region Port (Only change if necessary)",
                                                                         regInfo.RegionPort.ToString ()));
            if (regInfo.RegionPort != oldPort)
                updated = true;

            var oldStart = regInfo.Startup;
            // Startup mode
            string scriptStart = MainConsole.Instance.Prompt (
                "Region Startup - Normal or Delayed startup (normal/delay) : ", "normal").ToLower ();
            regInfo.Startup = scriptStart.StartsWith ("n") ? StartupType.Normal : StartupType.Medium;
            if (regInfo.Startup != oldStart)
                updated = true;

            var oldSwitch = regInfo.SeeIntoThisSimFromNeighbor;
            regInfo.SeeIntoThisSimFromNeighbor = MainConsole.Instance.Prompt (
                "See into this sim from neighbors (yes/no)",
                regInfo.SeeIntoThisSimFromNeighbor ? "yes" : "no").ToLower () == "yes";
            if (regInfo.SeeIntoThisSimFromNeighbor != oldSwitch)
                updated = true;

            oldSwitch = regInfo.InfiniteRegion;
            regInfo.InfiniteRegion = MainConsole.Instance.Prompt (
                "Make an infinite region (yes/no)",
                regInfo.InfiniteRegion ? "yes" : "no").ToLower () == "yes";
            if (regInfo.InfiniteRegion != oldSwitch)
                updated = true;

            var oldCap = regInfo.ObjectCapacity;
            regInfo.ObjectCapacity =
                int.Parse (MainConsole.Instance.Prompt ("Object capacity",
                    regInfo.ObjectCapacity == 0
                    ? "50000"
                    : regInfo.ObjectCapacity.ToString ()));
            if (regInfo.ObjectCapacity != oldCap)
                updated = true;

            return updated;
        }

        public virtual void SetRegion (IScene scene)
        {
            scene.WhiteCoreEventManager.RegisterEventHandler ("Backup", WhiteCoreEventManager_OnGenericEvent);
            m_scene = scene;
        }

        /// <summary>
        /// Updates the region info, allowing for changes etc.
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="cmds">Commands</param>
        public void UpdateRegionInfo (IScene scene, string [] cmds)
        {
            if (MainConsole.Instance.ConsoleScene != null) {
                m_scene = scene;
                var regInfo = scene.RegionInfo;
                string oldFile = BuildSaveFileName (scene.SimulationDataService.BackupFile);

                if (ModifyRegionSettings (ref regInfo, true)) {
                    scene.RegionInfo = regInfo;

                    // save a new backup
                    if (File.Exists (oldFile))
                        File.Delete (oldFile);
                    scene.SimulationDataService.BackupFile = regInfo.RegionName;
                    scene.SimulationDataService.ForceBackup ();

                    MainConsole.Instance.InfoFormat ("[FileBasedSimulationData]: Save of {0} completed.", regInfo.RegionName);

                    // bail out
                    MainConsole.Instance.ConsoleScene = null;
                    MainConsole.Instance.DefaultPrompt = "root: ";

                    // save current region serialized
                    var restart = scene.RequestModuleInterface<IRestartModule> ();
                    restart.SerializeScene ();

                    // shutdown and restart
                    restart.RestartScene ();

                }
            }
        }


        /// <summary>
        /// Sets the region prim capacity.
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="cmds">Commands</param>
        public void UpdateRegionPrims (IScene scene, string [] cmds)
        {
            if (MainConsole.Instance.ConsoleScene == null)
                return;

            m_scene = scene;
            var primCount = scene.RegionInfo.ObjectCapacity;

            if (cmds.Length > 3)
                primCount = int.Parse (cmds [3]);
            else
                while (!int.TryParse (MainConsole.Instance.Prompt ("Region prim capacity: ", primCount.ToString ()), out primCount))
                    MainConsole.Instance.Info ("Bad input, must be a number > 0");

            scene.RegionInfo.ObjectCapacity = primCount;
            MainConsole.Instance.InfoFormat (" The region capacity has been set to {0} prims", primCount);

            scene.SimulationDataService.ForceBackup ();
        }

        /// <summary>
        /// Cleanups the old region backups.
        /// </summary>
        /// <param name="scene">Scene</param>
        /// <param name="cmds">Commands</param>
        public void CleanupRegionBackups (IScene scene, string [] cmds)
        {
            int daysOld = m_removeArchiveDays;
            if (cmds.Count () > 3) {
                if (!int.TryParse (cmds [3], out daysOld))
                    daysOld = m_removeArchiveDays;
            }

            DeleteUpOldArchives (daysOld);

        }

        /// <summary>
        /// Restores the last backup.
        /// </summary>
        /// <returns><c>true</c>, if last backup was restored, <c>false</c> otherwise.</returns>
        /// <param name="regionName">Region name.</param>
        public bool RestoreLastBackup (string regionName)
        {
            string lastBackFile = GetLastBackupFileName (regionName);
            if (lastBackFile != "") {
                string regionFile = (m_storeDirectory == null)
                    ? regionName + ".sim"
                    : Path.Combine (m_storeDirectory, regionName + ".sim");

                if (File.Exists (regionFile))
                    File.Delete (regionFile);

                // now we can copy it over...
                File.Copy (lastBackFile, regionFile);

                return true;
            }

            return false;
        }

        public bool RestoreBackupFile (string fileName, string regionName)
        {
            if (fileName != "") {
                string regionFile = (m_storeDirectory == null)
                    ? regionName + ".sim"
                    : Path.Combine (m_storeDirectory, regionName + ".sim");

                if (File.Exists (regionFile))
                    File.Delete (regionFile);

                // now we can copy it over...
                File.Copy (fileName, regionFile);

                return true;

            }

            return false;
        }


        public virtual List<ISceneEntity> LoadObjects ()
        {
            return _regionData.Groups.ConvertAll<ISceneEntity> (o => o);
        }

        public virtual void LoadTerrain (bool RevertMap, int RegionSizeX, int RegionSizeY)
        {
            ITerrainModule terrainModule = m_scene.RequestModuleInterface<ITerrainModule> ();
            if (RevertMap) {
                terrainModule.TerrainRevertMap = ReadFromData (_regionData.RevertTerrain);
                //Make sure the size is right!
                if (terrainModule.TerrainRevertMap != null &&
                    terrainModule.TerrainRevertMap.Width != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainRevertMap = null;
            } else {
                terrainModule.TerrainMap = ReadFromData (_regionData.Terrain);
                //Make sure the size is right!
                if (terrainModule.TerrainMap != null &&
                    terrainModule.TerrainMap.Width != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainMap = null;
            }
        }

        public virtual void LoadWater (bool RevertMap, int RegionSizeX, int RegionSizeY)
        {
            ITerrainModule terrainModule = m_scene.RequestModuleInterface<ITerrainModule> ();
            if (RevertMap) {
                terrainModule.TerrainWaterRevertMap = ReadFromData (_regionData.RevertWater);
                //Make sure the size is right!
                if (terrainModule.TerrainWaterRevertMap.Width != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainWaterRevertMap = null;
            } else {
                terrainModule.TerrainWaterMap = ReadFromData (_regionData.Water);
                //Make sure the size is right!
                if (terrainModule.TerrainWaterMap.Width != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainWaterMap = null;
            }
        }

        public virtual void Shutdown ()
        {
            //The sim is shutting down, we need to save one last backup
            try {
                lock (m_saveLock) {
                    m_shutdown = true;
                    if (!m_saveChanges || !m_saveBackups)
                        return;
                    SaveBackup (false);
                }
            } catch (Exception ex) {
                MainConsole.Instance.Error ("[FileBasedSimulationData]: Failed to save backup, exception occurred " + ex);
            }
        }

        public virtual void Tainted ()
        {
            m_requiresSave = true;
        }

        public virtual void ForceBackup ()
        {
            if (m_saveTimer != null)
                m_saveTimer.Stop ();
            try {
                lock (m_saveLock) {
                    if (!m_shutdown)
                        SaveBackup (false);
                    m_requiresSave = false;
                }
            } catch (Exception ex) {
                MainConsole.Instance.Error ("[FileBasedSimulationData]: Failed to save backup, exception occurred " + ex);
            }
            if (m_saveTimer != null)
                m_saveTimer.Start (); //Restart it as we just did a backup
        }

        public virtual void RemoveRegion ()
        {
            //Remove the file so that the region is gone
            File.Delete (BuildSaveFileName ());
        }

        /// <summary>
        ///     Around for legacy things
        /// </summary>
        /// <returns></returns>
        public virtual List<LandData> LoadLandObjects ()
        {
            return _regionData.Parcels;
        }

        #endregion

        /// <summary>
        ///     Read the config for the data loader
        /// </summary>
        /// <param name="simBase"></param>
        protected virtual void ReadConfig (ISimulationBase simBase)
        {
            IConfig config = simBase.ConfigSource.Configs ["FileBasedSimulationData"];
            if (config != null) {
                m_saveChanges = config.GetBoolean ("SaveChanges", m_saveChanges);
                m_timeBetweenSaves = config.GetInt ("TimeBetweenSaves", m_timeBetweenSaves);
                m_keepOldSave = config.GetBoolean ("SavePreviousBackup", m_keepOldSave);

                // As of V0.9.2, data is saved in the '../Data' directory relative to the bin dir
                // or as configured

                // Get and save the default Data path
                string defaultDataPath = simBase.DefaultDataPath;

                m_storeDirectory =
                    PathHelpers.ComputeFullPath (config.GetString ("StoreBackupDirectory", m_storeDirectory));
                if (m_storeDirectory == "")
                    m_storeDirectory = Path.Combine (defaultDataPath, "Region");

                m_oldSaveDirectory =
                    PathHelpers.ComputeFullPath (config.GetString ("PreviousBackupDirectory", m_oldSaveDirectory));
                if (m_oldSaveDirectory == "")
                    m_oldSaveDirectory = Path.Combine (defaultDataPath, "RegionBak");

                m_removeArchiveDays = config.GetInt ("ArchiveDays", m_removeArchiveDays);


                // verify the necessary paths exist
                if (!Directory.Exists (m_storeDirectory))
                    Directory.CreateDirectory (m_storeDirectory);
                if (!Directory.Exists (m_oldSaveDirectory))
                    Directory.CreateDirectory (m_oldSaveDirectory);


                string regionNameSeed = config.GetString ("RegionNameSeed", "");
                if (regionNameSeed != "")
                    m_regionNameSeed = regionNameSeed.Split (',');

                m_saveBackupChanges = config.GetBoolean ("SaveTimedPreviousBackup", m_keepOldSave);
                m_timeBetweenBackupSaves = config.GetInt ("TimeBetweenBackupSaves", m_timeBetweenBackupSaves);
            }

            if (m_saveChanges && m_timeBetweenSaves != 0) {
                m_saveTimer = new Timer (m_timeBetweenSaves * 60 * 1000);
                m_saveTimer.Elapsed += m_saveTimer_Elapsed;
                m_saveTimer.Start ();
            }

            if (m_saveChanges && m_timeBetweenBackupSaves != 0) {
                m_backupSaveTimer = new Timer (m_timeBetweenBackupSaves * 60 * 1000 + 5000);
                m_backupSaveTimer.Elapsed += m_backupSaveTimer_Elapsed;
                m_backupSaveTimer.Start ();
            }
        }

        /// <summary>
        ///     Look for the backup event, and if it is there, trigger the backup of the sim
        /// </summary>
        /// <param name="FunctionName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        object WhiteCoreEventManager_OnGenericEvent (string FunctionName, object parameters)
        {
            if (FunctionName == "Backup") {
                ForceBackup ();
            }
            return null;
        }

        /// <summary>
        ///     Save a backup on the timer event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void m_saveTimer_Elapsed (object sender, ElapsedEventArgs e)
        {
            if (m_requiresSave) {
                m_displayNotSavingNotice = true;
                m_requiresSave = false;
                m_saveTimer.Stop ();
                try {
                    lock (m_saveLock) {
                        if (m_saveChanges && m_saveBackups && !m_shutdown) {
                            SaveBackup (false);
                        }
                    }
                } catch (Exception ex) {
                    MainConsole.Instance.Error ("[FileBasedSimulationData]: Failed to save backup, exception occurred " +
                                               ex);
                }
                m_saveTimer.Start (); //Restart it as we just did a backup
            } else if (m_displayNotSavingNotice) {
                m_displayNotSavingNotice = false;
                MainConsole.Instance.Info ("[FileBasedSimulationData]: Not saving backup, not required");
            }
        }

        /// <summary>
        ///     Save a backup into the oldSaveDirectory on the timer event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void m_backupSaveTimer_Elapsed (object sender, ElapsedEventArgs e)
        {
            try {
                lock (m_saveLock) {
                    if (!m_shutdown) {
                        SaveBackup (true);
                        DeleteUpOldArchives (m_removeArchiveDays);
                    }
                }
            } catch (Exception ex) {
                MainConsole.Instance.Error ("[FileBasedSimulationData]: Failed to save archive, exception occurred " + ex);
            }
        }

        public void DeleteUpOldArchives (int daysOld)
        {
            if (m_scene == null)
                return;

            if (daysOld < 0)
                return;

            var simRegion = m_scene.RegionInfo.RegionName;
            if (string.IsNullOrEmpty (simRegion))
                return;

            var regionArchives = FindRegionBackupFiles (simRegion);
            if (regionArchives == null || regionArchives.Count == 0)
                return;

            MainConsole.Instance.InfoFormat ("Found {0} archive files", regionArchives.Count);

            int removed = 0;
            DateTime archiveDate = DateTime.Today.AddDays (-daysOld);

            foreach (string fileName in regionArchives) {
                if (File.Exists (fileName)) {
                    DateTime fileDate = File.GetCreationTime (fileName);
                    if (DateTime.Compare (fileDate, archiveDate) < 0) {
                        File.Delete (fileName);
                        removed++;
                    }
                }
            }
            MainConsole.Instance.InfoFormat (" Removed {0} archive files", removed);
        }

        /// <summary>
        ///     Save a backup of the sim
        /// </summary>
        /// <param name="isOldSave"></param>
        protected virtual void SaveBackup (bool isOldSave)
        {
            if (m_scene == null || m_scene.RegionInfo.HasBeenDeleted)
                return;
            IBackupModule backupModule = m_scene.RequestModuleInterface<IBackupModule> ();
            if (backupModule != null && backupModule.LoadingPrims) //Something is changing lots of prims
            {
                MainConsole.Instance.Info ("[Backup]: Not saving backup because the backup module is loading prims");
                return;
            }

            //Save any script state saves that might be around
            IScriptModule [] engines = m_scene.RequestModuleInterfaces<IScriptModule> ();
            try {
                if (engines != null) {
                    foreach (IScriptModule engine in engines.Where (engine => engine != null)) {
                        engine.SaveStateSaves ();
                    }
                }
            } catch (Exception ex) {
                MainConsole.Instance.WarnFormat ("[Backup]: Exception caught: {0}", ex);
            }

            MainConsole.Instance.Info ("[FileBasedSimulationData]: Backing up " +
                                      m_scene.RegionInfo.RegionName);

            RegionData regiondata = new RegionData ();
            regiondata.Init ();

            regiondata.RegionInfo = m_scene.RegionInfo;
            IParcelManagementModule module = m_scene.RequestModuleInterface<IParcelManagementModule> ();
            if (module != null) {
                List<ILandObject> landObject = module.AllParcels ();
                foreach (ILandObject parcel in landObject)
                    regiondata.Parcels.Add (parcel.LandData);
            }

            ITerrainModule tModule = m_scene.RequestModuleInterface<ITerrainModule> ();
            if (tModule != null) {
                try {
                    regiondata.Terrain = WriteTerrainToStream (tModule.TerrainMap);
                    regiondata.RevertTerrain = WriteTerrainToStream (tModule.TerrainRevertMap);

                    if (tModule.TerrainWaterMap != null) {
                        regiondata.Water = WriteTerrainToStream (tModule.TerrainWaterMap);
                        regiondata.RevertWater = WriteTerrainToStream (tModule.TerrainWaterRevertMap);
                    }
                } catch (Exception ex) {
                    MainConsole.Instance.WarnFormat ("[Backup]: Exception caught: {0}", ex);
                }
            }

            ISceneEntity [] entities = m_scene.Entities.GetEntities ();
            regiondata.Groups = new List<SceneObjectGroup> (
                entities.Cast<SceneObjectGroup> ().Where (
                    (entity) => {
                        return !(entity.IsAttachment ||
               ((entity.RootChild.Flags & PrimFlags.Temporary) == PrimFlags.Temporary) ||
               ((entity.RootChild.Flags & PrimFlags.TemporaryOnRez) == PrimFlags.TemporaryOnRez));
                    }));
            try {
                foreach (ISceneEntity entity in regiondata.Groups.Where (ent => ent.HasGroupChanged))
                    entity.HasGroupChanged = false;
            } catch (Exception ex) {
                MainConsole.Instance.WarnFormat ("[Backup]: Exception caught: {0}", ex);
            }
            string filename = isOldSave ? BuildOldSaveFileName () : BuildSaveFileName ();

            if (File.Exists (filename + (isOldSave ? "" : ".tmp")))
                File.Delete (filename + (isOldSave ? "" : ".tmp")); //Remove old tmp files
            if (!_regionLoader.SaveBackup (filename + (isOldSave ? "" : ".tmp"), regiondata)) {
                if (File.Exists (filename + (isOldSave ? "" : ".tmp")))
                    File.Delete (filename + (isOldSave ? "" : ".tmp")); //Remove old tmp files
                MainConsole.Instance.Error ("[FileBasedSimulationData]: Failed to save backup for region " +
                                           m_scene.RegionInfo.RegionName + "!");
                return;
            }

            //RegionData data = _regionLoader.LoadBackup(filename + ".tmp");
            if (!isOldSave) {
                if (File.Exists (filename))
                    File.Delete (filename);
                File.Move (filename + ".tmp", filename);

                if (m_keepOldSave && !m_oldSaveHasBeenSaved) {
                    //Haven't moved it yet, so make sure the directory exists, then move it
                    m_oldSaveHasBeenSaved = true;
                    if (!Directory.Exists (m_oldSaveDirectory))
                        Directory.CreateDirectory (m_oldSaveDirectory);

                    // need to check if backup file already exists as well (e.g.. save within the minute timeframe)
                    string oldfileName = BuildOldSaveFileName ();
                    if (File.Exists (oldfileName))
                        File.Delete (oldfileName);

                    // now we can copy it over...
                    File.Copy (filename, oldfileName);
                }
            }
            regiondata.Dispose ();
            //Now make it the full file again
            MapTileNeedsGenerated = true;
            MainConsole.Instance.Info ("[FileBasedSimulationData]: Saved Backup for region " +
                                      m_scene.RegionInfo.RegionName);
        }

        string BuildOldSaveFileName ()
        {
            return Path.Combine (m_oldSaveDirectory,
                                m_scene.RegionInfo.RegionName + SerializeDateTime () + ".sim");
        }

        string BuildSaveFileName ()
        {
            //return (m_storeDirectory == "" || m_storeDirectory == "/")
            // the'/' directory is valid an someone might use it to store backups so don't
            // fudge it to mean './' ... as it previously was...

            var name = BackupFile;
            return (m_storeDirectory == "")
                       ? name + ".sim"
                       : Path.Combine (m_storeDirectory, name + ".sim");
        }

        string BuildSaveFileName (string name)
        {
            return (m_storeDirectory == "")
                ? name + ".sim"
                    : Path.Combine (m_storeDirectory, name + ".sim");
        }

        byte [] WriteTerrainToStream (ITerrainChannel tModule)
        {
            int tMapSize = tModule.Width * tModule.Height;
            byte [] sdata = new byte [tMapSize * 2];
            Buffer.BlockCopy (tModule.GetSerialised (), 0, sdata, 0, sdata.Length);
            return sdata;
        }

        protected virtual string SerializeDateTime ()
        {
            return string.Format ("--{0:yyyy-MM-dd-HH-mm}", DateTime.Now);
        }

        protected virtual void ReadBackup (string fileName)
        {
            BackupFile = fileName;
            string simName = Path.GetFileName (fileName);
            MainConsole.Instance.Info ("[FileBasedSimulationData]: Restoring sim backup for region " + simName + "...");

            _regionData = _regionLoader.LoadBackup (BuildSaveFileName ());
            if (_regionData == null)
                _regionData = _oldRegionLoader.LoadBackup (Path.ChangeExtension (BuildSaveFileName (),
                    _oldRegionLoader.FileType));
            if (_regionData == null) {
                _regionData = new RegionData ();
                _regionData.Init ();
            } else {
                //Make sure the region port is set
                if (_regionData.RegionInfo.RegionPort == 0) {
                    _regionData.RegionInfo.RegionPort = int.Parse (MainConsole.Instance.Prompt ("Region Port: ",
                        (9000).ToString ()));
                }
            }
            GC.Collect ();
        }

        ITerrainChannel ReadFromData (byte [] data)
        {
            if (data == null) return null;
            short [] sdata = new short [data.Length / 2];
            Buffer.BlockCopy (data, 0, sdata, 0, data.Length);
            return new TerrainChannel (sdata, m_scene);
        }

        public void Dispose ()
        {
            m_backupSaveTimer.Close ();
            m_saveTimer.Close ();
        }

        public ISimulationDataStore Copy ()
        {
            return new FileBasedSimulationData ();
        }
    }

    public interface IRegionDataLoader
    {
        string FileType { get; }

        RegionData LoadBackup (string file);

        bool SaveBackup (string fileName, RegionData regiondata);
    }

    [Serializable, ProtoContract]
    public class RegionData
    {
        [ProtoMember (1)]
        public List<SceneObjectGroup> Groups;
        [ProtoMember (2)]
        public RegionInfo RegionInfo;
        [ProtoMember (3)]
        public byte [] Terrain;
        [ProtoMember (4)]
        public byte [] RevertTerrain;
        [ProtoMember (5)]
        public byte [] Water;
        [ProtoMember (6)]
        public byte [] RevertWater;
        [ProtoMember (7)]
        public List<LandData> Parcels;

        public void Init ()
        {
            Groups = new List<SceneObjectGroup> ();
            Parcels = new List<LandData> ();
        }

        public void Dispose ()
        {
            Groups = null;
            Parcels = null;
            Water = null;
            RevertWater = null;
            Terrain = null;
            RevertTerrain = null;
            RegionInfo = null;
        }
    }
}
