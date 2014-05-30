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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Utilities;
using WhiteCore.Region;
using Nini.Config;
using OpenMetaverse;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Timer = System.Timers.Timer;

namespace WhiteCore.Modules
{
    /// <summary>
    ///     FileBased DataStore, do not store anything in any databases, instead save .sim files for it
    /// </summary>
    public class FileBasedSimulationData : ISimulationDataStore, IDisposable
    {
        protected Timer m_backupSaveTimer;

        protected string m_fileName = "";
        protected bool m_keepOldSave = true;
        protected string m_oldSaveDirectory = "";
        protected bool m_oldSaveHasBeenSaved;
        protected bool m_requiresSave = true;
        protected bool m_displayNotSavingNotice = true;
        protected bool m_saveBackupChanges = true;
        protected bool m_saveBackups;
        protected int m_removeArchiveDays = 30;
        protected bool m_saveChanges = true;
        protected string m_storeDirectory = "";
        protected Timer m_saveTimer;
        protected IScene m_scene;
        protected int m_timeBetweenBackupSaves = 1440; //One day
        protected int m_timeBetweenSaves = 5;
        protected bool m_shutdown = false;
        protected IRegionDataLoader _regionLoader;
        protected IRegionDataLoader _oldRegionLoader;
        protected RegionData _regionData;
        protected Object m_saveLock = new Object();

        #region ISimulationDataStore Members

        public bool MapTileNeedsGenerated { get; set; }

        public virtual string Name
        {
            get { return "FileBasedDatabase"; }
        }

        public bool SaveBackups
        {
            get { return m_saveBackups; }
            set { m_saveBackups = value; }
        }

        public virtual void CacheDispose()
        {
            _regionData.Dispose();
            _regionData = null;
        }

        public FileBasedSimulationData()
        {
            _oldRegionLoader = new TarRegionDataLoader();
            _regionLoader = new ProtobufRegionDataLoader();
        }

        public void Initialise() { }

        public virtual List<string> FindRegionInfos(out bool newRegion, ISimulationBase simBase)
        {
//			List<string> regions = new List<string>(Directory.GetFiles(".", "*.sim", SearchOption.TopDirectoryOnly));
			ReadConfig(simBase);
			MainConsole.Instance.Info("Looking for previous sims in: "+ m_storeDirectory);
			List<string> regions = new List<string>(Directory.GetFiles(m_storeDirectory, "*.sim", SearchOption.TopDirectoryOnly));
            newRegion = regions.Count == 0;
            List<string> retVals = new List<string>();
			foreach (string r in regions)
				if (Path.GetExtension (r) == ".sim") {
				MainConsole.Instance.Info ("Found: " + Path.GetFileNameWithoutExtension (r));
					retVals.Add (Path.GetFileNameWithoutExtension (r));
				}
            return retVals;
        }

        public virtual List<string> FindBackupRegionFiles()
        {
            if (m_oldSaveDirectory == "")
                return null;

            MainConsole.Instance.Info("Looking for sim backups in: "+ m_oldSaveDirectory);
            List<string> archives = new List<string>(Directory.GetFiles(m_oldSaveDirectory, "*.sim", SearchOption.TopDirectoryOnly));
            //List<string> retVals = new List<string>();
            //foreach (string r in regions)
            //    if (Path.GetExtension (r) == ".sim") {
            //        MainConsole.Instance.Debug ("Found: " + Path.GetFileNameWithoutExtension (r));
            //        retVals.Add (Path.GetFileNameWithoutExtension (r));
            //    }
            //return retVals;
            MainConsole.Instance.InfoFormat ("Found {0} archive files", archives.Count);
            return archives;
        }

        public virtual RegionInfo CreateNewRegion(ISimulationBase simBase, Dictionary<string, int> currentInfo)
        {
            ReadConfig(simBase);
            _regionData = new RegionData();
            _regionData.Init();
            RegionInfo info = CreateRegionFromConsole(null, true, currentInfo);
            if (info == null)
                return CreateNewRegion(simBase,currentInfo);

            m_fileName = info.RegionName;
            return info;
        }

        public virtual RegionInfo CreateNewRegion(ISimulationBase simBase, string regionName, Dictionary<string, int> currentInfo)
        {
            ReadConfig(simBase);
            _regionData = new RegionData();
            _regionData.Init();
            RegionInfo info = new RegionInfo();
            info.RegionName = regionName;
            info.NewRegion = true;

            info = CreateRegionFromConsole(info, true, currentInfo);
            if (info == null)
                return CreateNewRegion(simBase, info, currentInfo);
        
            m_fileName = info.RegionName;
            return info;
        }

		/// <summary>
        /// Initializes a new region using the passed regioninfo
		/// </summary>
		/// <returns></returns>
		/// <param name="simBase">Sim base.</param>
		/// <param name="regionInfo">Region info.</param>
        /// <param name="currentInfo">Current region info.</param>
        public virtual RegionInfo CreateNewRegion(ISimulationBase simBase, RegionInfo regionInfo, Dictionary<string, int> currentInfo)
        {
            ReadConfig(simBase);
            _regionData = new RegionData();
            _regionData.Init();
			
            // something wrong here, prompt for details
            if (regionInfo == null)
                return CreateNewRegion(simBase, currentInfo );		
            
			m_fileName = regionInfo.RegionName;
            
			if (m_scene != null)
			{
				IGridRegisterModule gridRegister = m_scene.RequestModuleInterface<IGridRegisterModule>();
				//Re-register so that if the position has changed, we get the new neighbors
				gridRegister.RegisterRegionWithGrid(m_scene, true, false, null);

				ForceBackup();

				MainConsole.Instance.Info("[FileBasedSimulationData]: Save completed.");
			}

			return regionInfo;

        }

        public virtual RegionInfo LoadRegionInfo(string fileName, ISimulationBase simBase)
        {
            ReadConfig(simBase);
            ReadBackup(fileName);
            m_fileName = fileName;
            return _regionData.RegionInfo;
        }

        public virtual RegionInfo LoadRegionNameInfo(string regionName, ISimulationBase simBase)
        {
            ReadConfig(simBase);
            _regionData = new RegionData();
            _regionData.Init();

            string regionFile = m_storeDirectory + regionName + ".sim";
            if (File.Exists(regionFile))
            {
                regionFile = Path.GetFileNameWithoutExtension (regionFile);
                ReadBackup (regionFile);
                m_fileName = regionFile;
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
        private RegionInfo CreateRegionFromConsole(RegionInfo info, Boolean prompt, Dictionary<string, int> currentInfo)
        {

            if (info == null || info.NewRegion)
            {
                if (info == null)
                    info = new RegionInfo();

                info.RegionID = UUID.Random();

                if (currentInfo != null)
                {
                    info.RegionLocX = currentInfo ["minX"] > 0 ? currentInfo ["minX"] : 1000 * Constants.RegionSize;
                    info.RegionLocY = currentInfo ["minY"] > 0 ? currentInfo ["minY"] : 1000 * Constants.RegionSize;
                    info.RegionPort = currentInfo ["port"] > 0 ? currentInfo ["port"] + 1 : 9000;
                } else
                {
                    info.RegionLocX = 1000 * Constants.RegionSize;
                    info.RegionLocY = 1000 * Constants.RegionSize;
                    info.RegionPort = 9000;

                }
                prompt = true;
            }

            // prompt for user input
            if (prompt)
            {
                info.RegionName = MainConsole.Instance.Prompt ("Region Name", info.RegionName);

                // Startup mode
                string scriptStart = MainConsole.Instance.Prompt (
                    "Region Startup - Normal or Delay script startup (normal/delay) : ","normal").ToLower();
                if (scriptStart.StartsWith("n"))
                {
                    info.Startup = StartupType.Normal;
                }
                else
                {
                    info.Startup = StartupType.Medium;
                }                
                // END TODO
                info.RegionLocX =
                    int.Parse (MainConsole.Instance.Prompt ("Region Location X",
                    ((info.RegionLocX == 0 
                            ? 1000 
                            : info.RegionLocX / Constants.RegionSize)).ToString ())) * Constants.RegionSize;

                info.RegionLocY =
                    int.Parse (MainConsole.Instance.Prompt ("Region location Y",
                    ((info.RegionLocY == 0 
                            ? 1000 
                            : info.RegionLocY / Constants.RegionSize)).ToString ())) * Constants.RegionSize;
            
                info.RegionSizeX = int.Parse (MainConsole.Instance.Prompt ("Region size X", info.RegionSizeX.ToString ()));
                info.RegionSizeY = int.Parse (MainConsole.Instance.Prompt ("Region size Y", info.RegionSizeY.ToString ()));
            
                info.RegionPort = int.Parse (MainConsole.Instance.Prompt ("Region Port", info.RegionPort.ToString ()));
            
                info.RegionType = MainConsole.Instance.Prompt ("Region Type (Flatland/Mainland/Island)",
                    (info.RegionType == "" ? "Flatland" : info.RegionType));
                    
                info.SeeIntoThisSimFromNeighbor =  MainConsole.Instance.Prompt (
                    "See into this sim from neighbors (yes/no)",
                    info.SeeIntoThisSimFromNeighbor ? "yes" : "no").ToLower() == "yes";

                info.InfiniteRegion = MainConsole.Instance.Prompt (
                    "Make an infinite region (yes/no)",
                    info.InfiniteRegion ? "yes" : "no").ToLower () == "yes";
            
                info.ObjectCapacity =
                    int.Parse (MainConsole.Instance.Prompt ("Object capacity",
                    info.ObjectCapacity == 0
                                           ? "50000"
                                           : info.ObjectCapacity.ToString ()));

            }

            // are we updating or adding??
            if (m_scene != null)
            {
                IGridRegisterModule gridRegister = m_scene.RequestModuleInterface<IGridRegisterModule>();
                //Re-register so that if the position has changed, we get the new neighbors
                gridRegister.RegisterRegionWithGrid(m_scene, true, false, null);

                // Tell clients about changes
                IEstateModule es = m_scene.RequestModuleInterface<IEstateModule> ();
                if (es != null)
                    es.sendRegionHandshakeToAll ();

                ForceBackup();

                MainConsole.Instance.Info("[FileBasedSimulationData]: Save completed.");
            }

            return info;
        }

        public virtual void SetRegion(IScene scene)
        {
            scene.WhiteCoreEventManager.RegisterEventHandler("Backup", WhiteCoreEventManager_OnGenericEvent);
            m_scene = scene;

            if (!MainConsole.Instance.Commands.ContainsCommand("update region info"))
                MainConsole.Instance.Commands.AddCommand("update region info", "update region info",
                    "Updates the region settings",
                    UpdateRegionInfo, true, true);
            if (!MainConsole.Instance.Commands.ContainsCommand("delete sim backups"))
                MainConsole.Instance.Commands.AddCommand(
                    "delete sim backups",
                    "delete sim backups [days]",
                    "Removes old region backup files older than [days] (default: " + m_removeArchiveDays + " days)",
                    CleanupRegionBackups,
                    false, true);

        }

        /// <summary>
        /// Updates the region info, allowing for changes etc.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmds">Cmds.</param>
        public void UpdateRegionInfo(IScene scene, string[] cmds)
        {
            if (MainConsole.Instance.ConsoleScene != null)
            {
                m_scene = scene;
                MainConsole.Instance.ConsoleScene.RegionInfo = CreateRegionFromConsole(MainConsole.Instance.ConsoleScene.RegionInfo, true, null);
            }
        }

        /// <summary>
        /// Cleanups the old region backups.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmds">Cmds.</param>
        public void CleanupRegionBackups(IScene scene, string[] cmds)
        {
            int daysOld = m_removeArchiveDays;
            if (cmds.Count () > 3)
            {
                if (!int.TryParse (cmds [3], out daysOld))
                    daysOld = m_removeArchiveDays;
            }

            DeleteUpOldArchives(daysOld);

        }

        public virtual List<ISceneEntity> LoadObjects()
        {
            return _regionData.Groups.ConvertAll<ISceneEntity>(o => o);
        }

        public virtual void LoadTerrain(bool RevertMap, int RegionSizeX, int RegionSizeY)
        {
            ITerrainModule terrainModule = m_scene.RequestModuleInterface<ITerrainModule>();
            if (RevertMap)
            {
                terrainModule.TerrainRevertMap = ReadFromData(_regionData.RevertTerrain);
                //Make sure the size is right!
                if (terrainModule.TerrainRevertMap != null &&
                    terrainModule.TerrainRevertMap.Height != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainRevertMap = null;
            }
            else
            {
                terrainModule.TerrainMap = ReadFromData(_regionData.Terrain);
                //Make sure the size is right!
                if (terrainModule.TerrainMap != null &&
                    terrainModule.TerrainMap.Height != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainMap = null;
            }
        }

        public virtual void LoadWater(bool RevertMap, int RegionSizeX, int RegionSizeY)
        {
            ITerrainModule terrainModule = m_scene.RequestModuleInterface<ITerrainModule>();
            if (RevertMap)
            {
                terrainModule.TerrainWaterRevertMap = ReadFromData(_regionData.RevertWater);
                //Make sure the size is right!
                if (terrainModule.TerrainWaterRevertMap.Height != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainWaterRevertMap = null;
            }
            else
            {
                terrainModule.TerrainWaterMap = ReadFromData(_regionData.Water);
                //Make sure the size is right!
                if (terrainModule.TerrainWaterMap.Height != m_scene.RegionInfo.RegionSizeX)
                    terrainModule.TerrainWaterMap = null;
            }
        }

        public virtual void Shutdown()
        {
            //The sim is shutting down, we need to save one last backup
            try
            {
                lock (m_saveLock)
                {
                    m_shutdown = true;
                    if (!m_saveChanges || !m_saveBackups)
                        return;
                    SaveBackup(false);
                }
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occurred " + ex);
            }
        }

        public virtual void Tainted()
        {
            m_requiresSave = true;
        }

        public virtual void ForceBackup()
        {
            if (m_saveTimer != null)
                m_saveTimer.Stop();
            try
            {
                lock (m_saveLock)
                {
                    if (!m_shutdown)
                        SaveBackup(false);
                    m_requiresSave = false;
                }
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occurred " + ex);
            }
            if (m_saveTimer != null)
                m_saveTimer.Start(); //Restart it as we just did a backup
        }

        public virtual void RemoveRegion()
        {
            //Remove the file so that the region is gone
            File.Delete(BuildSaveFileName());
        }

        /// <summary>
        ///     Around for legacy things
        /// </summary>
        /// <returns></returns>
        public virtual List<LandData> LoadLandObjects()
        {
            return _regionData.Parcels;
        }

        #endregion

        /// <summary>
        ///     Read the config for the data loader
        /// </summary>
        /// <param name="simBase"></param>
        protected virtual void ReadConfig(ISimulationBase simBase)
        {
            IConfig config = simBase.ConfigSource.Configs["FileBasedSimulationData"];
            if (config != null)
            {
                m_saveChanges = config.GetBoolean("SaveChanges", m_saveChanges);
                m_timeBetweenSaves = config.GetInt("TimeBetweenSaves", m_timeBetweenSaves);
                m_keepOldSave = config.GetBoolean("SavePreviousBackup", m_keepOldSave);

                // directories are referneces from the bin directory
                // As of V0.9.2 the data is saved relative to the bin dirs
                m_oldSaveDirectory =
                    PathHelpers.ComputeFullPath(config.GetString("PreviousBackupDirectory", m_oldSaveDirectory));
                m_storeDirectory =
                    PathHelpers.ComputeFullPath(config.GetString("StoreBackupDirectory", m_storeDirectory));
                if (m_storeDirectory == "")
                    m_storeDirectory = Constants.DEFAULT_DATA_DIR + "/Region";
                if (m_oldSaveDirectory == "")
                    m_oldSaveDirectory = Constants.DEFAULT_DATA_DIR + "/RegionBak";

                m_removeArchiveDays = config.GetInt("ArchiveDays", m_removeArchiveDays);
                               

                // verify the necessary paths exist
                if (!Directory.Exists(m_storeDirectory))
                    Directory.CreateDirectory(m_storeDirectory);
                if (!Directory.Exists(m_oldSaveDirectory))
                    Directory.CreateDirectory(m_oldSaveDirectory);


                m_saveBackupChanges = config.GetBoolean("SaveTimedPreviousBackup", m_keepOldSave);
                m_timeBetweenBackupSaves = config.GetInt("TimeBetweenBackupSaves", m_timeBetweenBackupSaves);
            }

            if (m_saveChanges && m_timeBetweenSaves != 0)
            {
                m_saveTimer = new Timer(m_timeBetweenSaves*60*1000);
                m_saveTimer.Elapsed += m_saveTimer_Elapsed;
                m_saveTimer.Start();
            }

            if (m_saveChanges && m_timeBetweenBackupSaves != 0)
            {
                m_backupSaveTimer = new Timer(m_timeBetweenBackupSaves*60*1000);
                m_backupSaveTimer.Elapsed += m_backupSaveTimer_Elapsed;
                m_backupSaveTimer.Start();
            }
        }

        /// <summary>
        ///     Look for the backup event, and if it is there, trigger the backup of the sim
        /// </summary>
        /// <param name="FunctionName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private object WhiteCoreEventManager_OnGenericEvent(string FunctionName, object parameters)
        {
            if (FunctionName == "Backup")
            {
                ForceBackup();
            }
            return null;
        }

        /// <summary>
        ///     Save a backup on the timer event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (m_requiresSave)
            {
                m_displayNotSavingNotice = true;
                m_requiresSave = false;
                m_saveTimer.Stop();
                try
                {
                    lock (m_saveLock)
                    {
                        if (m_saveChanges && m_saveBackups && !m_shutdown)
                        {
                            SaveBackup(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occurred " +
                                               ex);
                }
                m_saveTimer.Start(); //Restart it as we just did a backup
            }
            else if (m_displayNotSavingNotice)
            {
                m_displayNotSavingNotice = false;
                MainConsole.Instance.Info("[FileBasedSimulationData]: Not saving backup, not required");
            }
        }

        /// <summary>
        ///     Save a backup into the oldSaveDirectory on the timer event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_backupSaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                lock (m_saveLock)
                {
                    if (!m_shutdown)
                    {
                        SaveBackup(true);
                        DeleteUpOldArchives(m_removeArchiveDays);
                    }
                }
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup, exception occurred " + ex);
            }
        }

        public void DeleteUpOldArchives(int daysOld)
        {

            if (daysOld < 0)
                return;

            var regionArchives = FindBackupRegionFiles();
            if (regionArchives.Count == 0)
                return;

            int removed = 0;
            DateTime archiveDate = DateTime.Today.AddDays(-daysOld);

            foreach (string fileName in regionArchives)
            {
                if (File.Exists (fileName))
                { 
                    DateTime fileDate = File.GetCreationTime (fileName);
                    if (DateTime.Compare(fileDate, archiveDate) < 0)
                    {
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
        protected virtual void SaveBackup(bool isOldSave)
        {
            if (m_scene == null || m_scene.RegionInfo.HasBeenDeleted)
                return;
            IBackupModule backupModule = m_scene.RequestModuleInterface<IBackupModule>();
            if (backupModule != null && backupModule.LoadingPrims) //Something is changing lots of prims
            {
                MainConsole.Instance.Info("[Backup]: Not saving backup because the backup module is loading prims");
                return;
            }

            //Save any script state saves that might be around
            IScriptModule[] engines = m_scene.RequestModuleInterfaces<IScriptModule>();
            try
            {
                if (engines != null)
                {
                    foreach (IScriptModule engine in engines.Where(engine => engine != null))
                    {
                        engine.SaveStateSaves();
                    }
                }
            }
            catch (Exception ex)
            {
                MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
            }

            MainConsole.Instance.Info("[FileBasedSimulationData]: Saving backup for region " +
                                      m_scene.RegionInfo.RegionName);

            RegionData regiondata = new RegionData();
            regiondata.Init();

            regiondata.RegionInfo = m_scene.RegionInfo;
            IParcelManagementModule module = m_scene.RequestModuleInterface<IParcelManagementModule>();
            if (module != null)
            {
                List<ILandObject> landObject = module.AllParcels();
                foreach (ILandObject parcel in landObject)
                    regiondata.Parcels.Add(parcel.LandData);
            }

            ITerrainModule tModule = m_scene.RequestModuleInterface<ITerrainModule>();
            if (tModule != null)
            {
                try
                {
                    regiondata.Terrain = WriteTerrainToStream(tModule.TerrainMap);
                    regiondata.RevertTerrain = WriteTerrainToStream(tModule.TerrainRevertMap);

                    if (tModule.TerrainWaterMap != null)
                    {
                        regiondata.Water = WriteTerrainToStream(tModule.TerrainWaterMap);
                        regiondata.RevertWater = WriteTerrainToStream(tModule.TerrainWaterRevertMap);
                    }
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                }
            }

            ISceneEntity[] entities = m_scene.Entities.GetEntities();
            regiondata.Groups = new List<SceneObjectGroup>(
                entities.Cast<SceneObjectGroup>().Where(
                    (entity) => {return !( entity.IsAttachment ||
                        ((entity.RootChild.Flags & PrimFlags.Temporary) == PrimFlags.Temporary) ||
                        ((entity.RootChild.Flags & PrimFlags.TemporaryOnRez) == PrimFlags.TemporaryOnRez));
                }));
            try
            {
                foreach (ISceneEntity entity in regiondata.Groups.Where(ent => ent.HasGroupChanged))
                    entity.HasGroupChanged = false;
            }
            catch (Exception ex)
            {
                MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
            }
            string filename = isOldSave ? BuildOldSaveFileName() : BuildSaveFileName();

            if (File.Exists(filename + (isOldSave ? "" : ".tmp")))
                File.Delete(filename + (isOldSave ? "" : ".tmp")); //Remove old tmp files
            if (!_regionLoader.SaveBackup(filename + (isOldSave ? "" : ".tmp"), regiondata))
            {
                if (File.Exists(filename + (isOldSave ? "" : ".tmp")))
                    File.Delete(filename + (isOldSave ? "" : ".tmp")); //Remove old tmp files
                MainConsole.Instance.Error("[FileBasedSimulationData]: Failed to save backup for region " +
                                           m_scene.RegionInfo.RegionName + "!");
                return;
            }

            //RegionData data = _regionLoader.LoadBackup(filename + ".tmp");
            if (!isOldSave)
            {
                if (File.Exists(filename))
                    File.Delete(filename);
                File.Move(filename + ".tmp", filename);

                if (m_keepOldSave && !m_oldSaveHasBeenSaved)
                {
                    //Havn't moved it yet, so make sure the directory exists, then move it
                    m_oldSaveHasBeenSaved = true;
                    if (!Directory.Exists(m_oldSaveDirectory))
                        Directory.CreateDirectory(m_oldSaveDirectory);
                    File.Copy(filename, BuildOldSaveFileName());
                }
            }
            regiondata.Dispose();
            //Now make it the full file again
            MapTileNeedsGenerated = true;
            MainConsole.Instance.Info("[FileBasedSimulationData]: Saved Backup for region " +
                                      m_scene.RegionInfo.RegionName);
        }

        private string BuildOldSaveFileName()
        {
            return Path.Combine(m_oldSaveDirectory,
                                m_scene.RegionInfo.RegionName + SerializeDateTime() + ".sim");
        }

        private string BuildSaveFileName()
        {
            //return (m_storeDirectory == "" || m_storeDirectory == "/")
            // the'/' diretcory is valid an someone might use it to store backups so don't
            // fudge it to mean './' ... as it previously was...
            return (m_storeDirectory == "")
                       ? m_fileName + ".sim"
                       : Path.Combine(m_storeDirectory, m_fileName + ".sim");
        }

        private byte[] WriteTerrainToStream(ITerrainChannel tModule)
        {
            int tMapSize = tModule.Height*tModule.Height;
            byte[] sdata = new byte[tMapSize*2];
            Buffer.BlockCopy(tModule.GetSerialised(), 0, sdata, 0, sdata.Length);
            return sdata;
        }

        protected virtual string SerializeDateTime()
        {
            return String.Format("--{0:yyyy-MM-dd-HH-mm}", DateTime.Now);
        }

        protected virtual void ReadBackup(string fileName)
        {
            m_fileName = fileName;
            string simName = Path.GetFileName(fileName); 
            MainConsole.Instance.Info("[FileBasedSimulationData]: Restoring sim backup for region " + simName + "...");

            _regionData = _regionLoader.LoadBackup(BuildSaveFileName());
            if (_regionData == null)
                _regionData = _oldRegionLoader.LoadBackup(Path.ChangeExtension(BuildSaveFileName(), 
                    _oldRegionLoader.FileType));
            if (_regionData == null)
            {
                _regionData = new RegionData();
                _regionData.Init();
            }
            else
            {
                //Make sure the region port is set
                if (_regionData.RegionInfo.RegionPort == 0)
                {
                    _regionData.RegionInfo.RegionPort = int.Parse(MainConsole.Instance.Prompt("Region Port: ", 
                        (9000).ToString()));
                }
            }
            GC.Collect();
        }

        private ITerrainChannel ReadFromData(byte[] data)
        {
            if (data == null) return null;
            short[] sdata = new short[data.Length/2];
            Buffer.BlockCopy(data, 0, sdata, 0, data.Length);
            return new TerrainChannel(sdata, m_scene);
        }

        public void Dispose()
        {
            m_backupSaveTimer.Close();
            m_saveTimer.Close();
        }

        public ISimulationDataStore Copy()
        {
            return new FileBasedSimulationData();
        }
    }

    public interface IRegionDataLoader
    {
        string FileType { get; }

        RegionData LoadBackup(string file);

        bool SaveBackup(string m_fileName, RegionData regiondata);
    }

    [Serializable, ProtoBuf.ProtoContract()]
    public class RegionData
    {
        [ProtoMember(1)] public List<SceneObjectGroup> Groups;
        [ProtoMember(2)] public RegionInfo RegionInfo;
        [ProtoMember(3)] public byte[] Terrain;
        [ProtoMember(4)] public byte[] RevertTerrain;
        [ProtoMember(5)] public byte[] Water;
        [ProtoMember(6)] public byte[] RevertWater;
        [ProtoMember(7)] public List<LandData> Parcels;

        public void Init()
        {
            Groups = new List<SceneObjectGroup>();
            Parcels = new List<LandData>();
        }

        public void Dispose()
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
