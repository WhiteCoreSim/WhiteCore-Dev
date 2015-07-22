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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Timer = System.Timers.Timer;

namespace WhiteCore.Region
{
    /// <summary>
    ///     Manager for adding, closing, resetting, and restarting scenes.
    /// </summary>
    public class SceneManager : ISceneManager, IApplicationPlugin
    {
        #region Static Constructor

        static SceneManager()
        {
            WhiteCore.Framework.Serialization.SceneEntitySerializer.SceneObjectSerializer =
                new WhiteCore.Region.Serialization.SceneObjectSerializer();
        }

        #endregion

        #region Declares

        public event NewScene OnCloseScene;
        public event NewScene OnAddedScene;
        public event NewScene OnFinishedAddingScene;

        protected ISimulationBase m_SimBase;
        protected List<IScene> m_scenes = new List<IScene>();

        public List<IScene> Scenes { get { return m_scenes; } }

        protected List<ISimulationDataStore> m_simulationDataServices = new List<ISimulationDataStore>();
        protected ISimulationDataStore m_selectedDataService;

        IConfigSource m_config = null;
        DateTime m_startupTime;

        public IConfigSource ConfigSource
        {
            get { return m_config; }
        }

        #endregion

        #region IApplicationPlugin members

        public void PreStartup(ISimulationBase simBase)
        {
            m_SimBase = simBase;
            m_startupTime = simBase.StartupTime;

            IConfig handlerConfig = simBase.ConfigSource.Configs["ApplicationPlugins"];
            if (handlerConfig.GetString("SceneManager", "") != Name)
                return;

            m_config = simBase.ConfigSource;
            //Register us!
            m_SimBase.ApplicationRegistry.RegisterModuleInterface<ISceneManager>(this);
        }

        public void Initialize(ISimulationBase simBase)
        {
            IConfig handlerConfig = simBase.ConfigSource.Configs["ApplicationPlugins"];
            if (handlerConfig.GetString("SceneManager", "") != Name)
                return;

            string name = "FileBasedDatabase";
            // Try reading the [SimulationDataStore] section
            IConfig simConfig = simBase.ConfigSource.Configs["SimulationDataStore"];
            if (simConfig != null)
            {
                name = simConfig.GetString("DatabaseLoaderName", "FileBasedDatabase");
            }

            ISimulationDataStore[] stores = WhiteCoreModuleLoader.PickupModules<ISimulationDataStore>().ToArray();
            
            List<string> storeNames = new List<string>();
            foreach (ISimulationDataStore store in stores)
            {
                if (store.Name.ToLower() == name.ToLower())
                {
                    m_selectedDataService = store;
                    break;
                }
                storeNames.Add(store.Name);
            }

            if (m_selectedDataService == null)
            {
                MainConsole.Instance.ErrorFormat(
                    "[SceneManager]: FAILED TO LOAD THE SIMULATION SERVICE AT '{0}', ONLY OPTIONS ARE {1}, QUITING...",
                    name, string.Join(", ", storeNames.ToArray()));
                Console.Read(); //Wait till they see
                Environment.Exit(0);
            }
            m_selectedDataService.Initialise();

            AddConsoleCommands();

            //Load the startup modules for the region
            m_startupPlugins = WhiteCoreModuleLoader.PickupModules<ISharedRegionStartupModule>();
        }

        public void ReloadConfiguration(IConfigSource config)
        {
            //Update this
            m_config = config;
            foreach (IScene scene in m_scenes)
            {
                scene.Config = config;
                scene.PhysicsScene.PostInitialise(config);
            }
        }

        public void PostInitialise()
        {
        }

        public void Start()
        {
        }

        public void PostStart()
        {
            if (m_selectedDataService == null)
                return;

            bool newRegion = false;
            StartRegions(out newRegion);
            SetRegionPrompt("root");
            if (newRegion) //Save the new info
            {
                foreach (ISimulationDataStore store in m_simulationDataServices)
                    store.ForceBackup();
            }
        }

        public string Name
        {
            get { return "SceneManager"; }
        }

        public void Dispose()
        {
        }

        public void Close()
        {
            if (m_scenes.Count == 0)
                return;
            foreach(IScene scene in new List<IScene>(m_scenes))
                CloseRegion(scene, ShutdownType.Immediate, 0, true);
        }

        public void SetRegionPrompt(string region)
        {
            MainConsole.Instance.DefaultPrompt = region+ ": ";
        }

        #endregion

        #region Startup complete

        public void HandleStartupComplete(List<string> data)
        {
            //Tell modules about it 
            StartupCompleteModules();
            m_SimBase.RunStartupCommands();

            TimeSpan timeTaken;
            if (m_startupTime == m_SimBase.StartupTime)
               timeTaken = DateTime.Now - m_SimBase.StartupTime;    // this is the time since the sim started
            else
               timeTaken = DateTime.Now - m_startupTime;            // time for a restart etc

            MainConsole.Instance.InfoFormat(
                "[SceneManager]: Startup Complete. This took {0}m {1}.{2}s",
                timeTaken.Minutes, timeTaken.Seconds, timeTaken.Milliseconds);

            m_startupTime = m_SimBase.StartupTime;                  // finished this timing period

            WhiteCoreModuleLoader.ClearCache();
            // In 99.9% of cases it is a bad idea to manually force garbage collection. However,
            // this is a rare case where we know we have just went through a long cycle of heap
            // allocations, and there is no more work to be done until someone logs in
            GC.Collect();
        }

        #endregion

        #region Add a region

        public void StartRegions(out bool newRegion)
        {
            List<KeyValuePair<ISimulationDataStore, RegionInfo>> regions = new List<KeyValuePair<ISimulationDataStore, RegionInfo>>();
            List<string> regionFiles = m_selectedDataService.FindRegionInfos(out newRegion, m_SimBase);
            if (newRegion)
            {
                var currentInfo = FindCurrentRegionInfo ();

                ISimulationDataStore store = m_selectedDataService.Copy();
                regions.Add(new KeyValuePair<ISimulationDataStore, RegionInfo>(store, store.CreateNewRegion(m_SimBase, currentInfo)));
            }
            else
            {
                foreach (string fileName in regionFiles)
                {
                    ISimulationDataStore store = m_selectedDataService.Copy();
                    regions.Add(new KeyValuePair<ISimulationDataStore, RegionInfo>(store, 
                        store.LoadRegionInfo(fileName, m_SimBase)));
                }
            }

            foreach (KeyValuePair<ISimulationDataStore, RegionInfo> kvp in regions)
                StartRegion(kvp.Key, kvp.Value);
        }

        public void StartRegion(ISimulationDataStore simData, RegionInfo regionInfo)
        {
            MainConsole.Instance.InfoFormat("[SceneManager]: Starting region \"{0}\" at @ {1},{2}",
                                            regionInfo.RegionName,
                                            regionInfo.RegionLocX/256, regionInfo.RegionLocY/256);
            ISceneLoader sceneLoader = m_SimBase.ApplicationRegistry.RequestModuleInterface<ISceneLoader>();
            if (sceneLoader == null)
                throw new Exception("No Scene Loader Interface!");

            //Get the new scene from the interface
            IScene scene = sceneLoader.CreateScene(simData, regionInfo);
            m_scenes.Add(scene);

            MainConsole.Instance.ConsoleScenes = m_scenes;
            simData.SetRegion(scene);
            m_simulationDataServices.Add(simData);

            if (OnAddedScene != null)
                OnAddedScene(scene);

            StartModules(scene);

            if (OnFinishedAddingScene != null)
                OnFinishedAddingScene(scene);

            //Start the heartbeats
            scene.StartHeartbeat();
            //Tell the scene that the startup is complete 
            // Note: this event is added in the scene constructor
            scene.FinishedStartup("Startup", new List<string>());

        }

        #endregion

        #region Reset a region

        public void ResetRegion(IScene scene)
        {
            IBackupModule backup = scene.RequestModuleInterface<IBackupModule>();
            if (backup != null)
                backup.DeleteAllSceneObjects(); //Remove all the objects from the region
            ITerrainModule module = scene.RequestModuleInterface<ITerrainModule>();
            if (module != null)
                module.ResetTerrain(); //Then remove the terrain
            //Then reset the textures
            scene.RegionInfo.RegionSettings.TerrainTexture1 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_1;
            scene.RegionInfo.RegionSettings.TerrainTexture2 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_2;
            scene.RegionInfo.RegionSettings.TerrainTexture3 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_3;
            scene.RegionInfo.RegionSettings.TerrainTexture4 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_4;
            MainConsole.Instance.Warn("Region " + scene.RegionInfo.RegionName + " was reset");
        }

        public void ClearRegion(IScene scene)
        {
            IBackupModule backup = scene.RequestModuleInterface<IBackupModule>();
            if (backup != null)
                backup.DeleteAllSceneObjects(); //Remove all the objects from the region
            MainConsole.Instance.Warn("Region " + scene.RegionInfo.RegionName + " has been cleared");
        }

        public void RemoveRegion(IScene scene)
        {
            // change back to the root as we are going to trash this one
            MainConsole.Instance.ConsoleScene = null;
            SetRegionPrompt("root");

            m_scenes.Remove (scene);
            MainConsole.Instance.ConsoleScenes = m_scenes;

            scene.SimulationDataService.RemoveRegion();
            IGridRegisterModule gridRegisterModule = scene.RequestModuleInterface<IGridRegisterModule>();
            gridRegisterModule.DeleteRegion(scene);

            MainConsole.Instance.Warn("[SceneManager]: Region " + scene.RegionInfo.RegionName + " was removed\n"+
                "To ensure all data is correct, you should consider restarting the simulator");

            if (MainConsole.Instance.Prompt ("[SceneManager]: Do you wish to shutdown the systemn? (yes/no)", "no") == "yes")
            {
                MainConsole.Instance.Warn ("[SceneManager]: Shutting down in 5 seconds");
                System.Threading.Thread.Sleep (5000);
                Environment.Exit (0);
            }
        }

        #endregion

        #region Restart a region

        public void RestartRegion(IScene scene, bool killAgents)
        {
            m_startupTime = DateTime.Now;                           // for more meaningful startup times
            string regionName = scene.RegionInfo.RegionName;        // save current info for later

            // change back to the root as we are going to trash this one
            MainConsole.Instance.ConsoleScene = null;
            SetRegionPrompt("root");

            // close and clean up a bit
            CloseRegion(scene, ShutdownType.Immediate, 0, killAgents);
            MainConsole.Instance.ConsoleScenes = m_scenes;

            // restart or die?
            IConfig startupConfig = m_config.Configs["Startup"];
            if (startupConfig == null || !startupConfig.GetBoolean ("RegionRestartCausesShutdown", false))
            {
                RegionInfo region = m_selectedDataService.LoadRegionNameInfo (regionName, m_SimBase);

                StartRegion (m_selectedDataService, region);
                MainConsole.Instance.Info ("[SceneManager]: " + regionName + " has been restarted");
            }
            else
            {
                //Kill us now
                MainConsole.Instance.Warn ("[SceneManager]: Shutting down as per [Startup] configuration");
                m_SimBase.Shutdown(true);
            }
        }

        #endregion

        #region Shutdown regions

        /// <summary>
        ///     Shuts down a region and removes it from all running modules
        /// </summary>
        /// <param name="type"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public void CloseRegion(IScene scene, ShutdownType type, int delaySecs, bool killAgents)
        {
            if (type == ShutdownType.Immediate)
            {
                scene.Close(killAgents);
                if (OnCloseScene != null)
                    OnCloseScene(scene);
                CloseModules(scene);
                m_scenes.Remove(scene);
            }
            else
            {
                Timer t = new Timer(delaySecs*1000); //Millisecond conversion
                t.Elapsed += (sender, e) => CloseRegion(scene, ShutdownType.Immediate, 0, killAgents);
                t.AutoReset = false;
                t.Start();
            }
        }

        #endregion

        #region Create Region
        /// <summary>
        /// Creates and adds a region from supplied regioninfo.
        /// </summary>
        /// <param name="regionInfo">Region info.</param>
        public bool CreateRegion (RegionInfo regionInfo)
        {
            if (regionInfo == null)
                return false;

            if (RegionNameExists(regionInfo.RegionName))
            {
                MainConsole.Instance.InfoFormat ("[SceneManager]: A region already exists with the name '{0}'",
                    regionInfo.RegionName);
                return false;
            }

            if ( RegionAtLocationExists(regionInfo.RegionLocX, regionInfo.RegionLocY))
            {
            MainConsole.Instance.InfoFormat ("[SceneManager]: A region at @ {0},{1} already exists",
                regionInfo.RegionLocX / Constants.RegionSize, regionInfo.RegionLocY / Constants.RegionSize);
                return false;
            }

            // we should be ok..
            MainConsole.Instance.InfoFormat ("[SceneManager]: Creating new region \"{0}\" at @ {1},{2}",
                regionInfo.RegionName, regionInfo.RegionLocX / Constants.RegionSize, regionInfo.RegionLocY / Constants.RegionSize);

            var currentInfo = FindCurrentRegionInfo ();
            var regions = new List<KeyValuePair<ISimulationDataStore, RegionInfo>> ();
            ISimulationDataStore store = m_selectedDataService.Copy ();

            regions.Add (new KeyValuePair<ISimulationDataStore, RegionInfo> (store, store.CreateNewRegion (m_SimBase, regionInfo, currentInfo)));
            StartRegion (store, regionInfo);

            return true;
        }
        #endregion

        #region ISharedRegionStartupModule plugins

        protected List<ISharedRegionStartupModule> m_startupPlugins = new List<ISharedRegionStartupModule>();

        protected void StartModules(IScene scene)
        {
            //Run all the initialization
            //First, Initialize the SharedRegionStartupModule
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.Initialise(scene, m_config, m_SimBase);
            }
            //Then do the ISharedRegionModule and INonSharedRegionModules
            MainConsole.Instance.Debug("[Modules]: Loading region modules");
            IRegionModulesController controller;
            if (m_SimBase.ApplicationRegistry.TryRequestModuleInterface(out controller))
            {
                controller.AddRegionToModules(scene);
            }
            else
                MainConsole.Instance.Error("[Modules]: The new RegionModulesController is missing...");
            //Then finish the rest of the SharedRegionStartupModules
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.PostInitialise(scene, m_config, m_SimBase);
            }
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.FinishStartup(scene, m_config, m_SimBase);
            }
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.PostFinishStartup(scene, m_config, m_SimBase);
            }
        }

        protected void StartupCompleteModules()
        {
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                try
                {
                    module.StartupComplete();
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.Warn("[SceneManager]: Exception running StartupComplete, " + ex);
                }
            }
        }

        protected void CloseModules(IScene scene)
        {
            IRegionModulesController controller;
            if (m_SimBase.ApplicationRegistry.TryRequestModuleInterface(out controller))
                controller.RemoveRegionFromModules(scene);

            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.Close(scene);
            }
        }

        public void DeleteRegion(IScene scene)
        {
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.DeleteRegion(scene);
            }
        }

        #endregion

        #region Console Commands

        void AddConsoleCommands()
        {
            if (MainConsole.Instance == null)
                return;
            MainConsole.Instance.Commands.AddCommand("show users",
                "show users [full]",
                "Shows users in the given region (if full is added, child agents are shown as well)",
                HandleShowUsers, true, false);

            MainConsole.Instance.Commands.AddCommand("show maturity",
                "show maturity",
                "Show all region's maturity levels",
                HandleShowMaturity, true, false);

            MainConsole.Instance.Commands.AddCommand("force update",
                "force update",
                "Force the update of all objects on clients",
                HandleForceUpdate, true, false);

            MainConsole.Instance.Commands.AddCommand("debug packet level", 
                "debug packet level [level]",
                "Turn on packet debugging",
                Debug, true, false);

            MainConsole.Instance.Commands.AddCommand("debug packet name",
                "debug packet name [packetname]",
                "Turn on packet debugging for a specific packet",
                Debug, true, false);

            MainConsole.Instance.Commands.AddCommand("debug packet name remove",
                "debug packet name [packetname]",
                "Turn off packet debugging for a specific packet",
                Debug, true, false);

            MainConsole.Instance.Commands.AddCommand("debug scene",
                "debug scene [scripting] [collisions] [physics]",
                "Turn on scene debugging",
                Debug, true, false);

            MainConsole.Instance.Commands.AddCommand("load oar",
                "load oar [OAR filename] [--merge] [--skip-assets] [--skip-terrain] [--OffsetX=#] [--OffsetY=#] [--OffsetZ=#] [--FlipX] [--FlipY] [--UseParcelOwnership] [--CheckOwnership]",
                "Load a region's data from OAR archive.  \n" +
                "--merge will merge the oar with the existing scene (including parcels).  \n" +
                "--skip-assets will load the oar but ignore the assets it contains. \n" +
                "--skip-terrain will skip loading the oar terrain. \n" +
                "--OffsetX will change where the X location of the oar is loaded, and the same for Y and Z.  \n" +
                "--FlipX flips the region on the X axis.  \n" +
                "--FlipY flips the region on the Y axis.  \n" +
                "--UseParcelOwnership changes who the default owner of objects whose owner cannot be found from\n" + 
                "      the Estate Owner to the parcel owner on which the object is found.  \n" +
                "--CheckOwnership asks for each UUID that is not found on the grid what user it should be changed\n" +
                "      to (useful for changing UUIDs from other grids, but very long with many users).  ",
                HandleLoadOar, true, true);

            MainConsole.Instance.Commands.AddCommand("save oar",
                "save oar [<OAR filename>] [--perm=<permissions>] ",
                "Save a region's data to an OAR archive" + Environment.NewLine +
                "<OAR filename> The file name (and optional path) to use when saveing the archive." +
                "  If this is not given then the oar is saved to the 'region name' in the 'Data/Region/OarFiles' folder." + Environment.NewLine +
                "--perm stops objects with insufficient permissions from being saved to the OAR." + Environment.NewLine +
                "  <permissions> can contain one or more of these characters: \"C\" = Copy, \"T\" = Transfer" + Environment.NewLine,
                HandleSaveOar, true, true);

            MainConsole.Instance.Commands.AddCommand("kick user", 
                "kick user [all]",
                "Kick a user off the simulator",
                KickUserCommand, true, true);

            MainConsole.Instance.Commands.AddCommand("restart-instance",
                "restart-instance",
                "Restarts the region(s) (as if you closed and re-opened WhiteCore)",
                RunCommand, true, false);

            MainConsole.Instance.Commands.AddCommand("command-script",
                "command-script [script]",
                "Run a command script from file",
                RunCommand, false, false);

            MainConsole.Instance.Commands.AddCommand("modules list",
                "modules list",
                "Lists all simulator modules",
                HandleModulesList, true, false);

            MainConsole.Instance.Commands.AddCommand("modules unload",
                "modules unload [module]",
                "Unload the given simulator module",
                HandleModulesUnload, true, false);
            
            MainConsole.Instance.Commands.AddCommand("change region",
                "change region [region name]",
                "Changes the region that commands will run on (or root for the commands to run on all regions)",
                HandleChangeRegion, false, true);

            MainConsole.Instance.Commands.AddCommand("show regions",
                "show regions",
                "Show information about all regions in this instance",
                HandleShowRegions, true, false);

            MainConsole.Instance.Commands.AddCommand("reset region",
                "reset region [RegionName]",
                "Reset region to the default terrain, wipe all prims, etc.",
                HandleResetRegion, false, true);

            MainConsole.Instance.Commands.AddCommand("remove region", 
                "remove region [RegionName]",
                "Remove region from the grid, and delete all info associated with it",
                HandleDeleteRegion, false, true);

            MainConsole.Instance.Commands.AddCommand("load region backup", 
                "load region backup [FileName]",
                "load a region from a previous backup file",
                HandleReloadRegion, false, true);

            MainConsole.Instance.Commands.AddCommand("delete region", 
                "delete region  [RegionName] (alias for 'remove region')",
                "Remove region from the grid, and delete all info associated with it",
                HandleDeleteRegion, false, true);

            MainConsole.Instance.Commands.AddCommand("create region", 
                "create region <Region Name>  <--config=filename>",
                "Creates a new region to start\n"+
                "<Region Name> - Use this name for the new region\n"+
                "--config='filename' - Use this file for region configuration",
                HandleCreateNewRegion, false, true);
			
            MainConsole.Instance.Commands.AddCommand("save region config",
                "save region config <filename>",
                "Saves the configuration of the region\n"+
                "<filename> - Use this name for the region configuration (default is region name)",
                HandleSaveRegionConfig, true, false);

            MainConsole.Instance.Commands.AddCommand("resize object",
                "resize object <name> <x> <y> <z>",
                "Change the scale of a named object by x,y,z", 
                HandleResizeObject, true, true);

            MainConsole.Instance.Commands.AddCommand("show objects",
                "show object [name]",
                "shows region objects or if object name is supplied, object info", 
                HandleShowObjects, true, true);

            MainConsole.Instance.Commands.AddCommand("rotate region objects",
                "rotate region objects <degrees> [centerX] [centerY]",
                "Rotates all region objects around centerX, centerY (default center of region)\n" +
                "Rotation is +ve to the right, -ve to the left. (Have you backed up your region?)",
                HandleRotateScene, true, true);

            MainConsole.Instance.Commands.AddCommand("scale region objects",
                "scale region objects <factor>",
                "Scales all region objects by the specified amount (please back up your region before using)",
                HandleScaleScene, true, true);

            MainConsole.Instance.Commands.AddCommand("reposition region objects",
                "reposition region objects <xOffset> <yOffset> <zOffset>",
                "Move region objects by the specified amounts (Have you backed up your region?)",
                HandleTranslateScene, true, true);

            // some region settings for maintenance
            MainConsole.Instance.Commands.AddCommand("set region capacity",
                "set region capacity [prims]",
                "sets the region maximum prim count", 
                HandleSetRegionCapacity, true, true);

            MainConsole.Instance.Commands.AddCommand("set region startup",
                "set region startup [normal/delayed]",
                "set the startup mode of scripts in the region.\n" +
                "   normal - scripts run continuously; delayed - scripts are started when an avatar enters", 
                HandleSetRegionStartup, true, true);

            MainConsole.Instance.Commands.AddCommand("set region infinite",
                "set region infinite [yes/no]",
                "sets the region type as 'infinite':  If 'infinite' this allows an avatar to fly out of the region", 
                HandleSetRegionInfinite, true, true);

            MainConsole.Instance.Commands.AddCommand("set region visibility",
                "set region visibility [yes/no]",
                "sets whether neighbouring regions can 'see into' this region", 
                HandleSetRegionVisibility, true, true);

        }

        #region helpers

        /// <summary>
        /// Gets the available region names.
        /// </summary>
        /// <returns>The region names.</returns>
        public List<string> GetRegionNames()
        {
            var retVals = new List<string>();
            foreach (IScene scene in m_scenes)
                retVals.Add (scene.RegionInfo.RegionName);

            return retVals;
        }

        /// <summary>
        /// Checks if a Region name already exists.
        /// </summary>
        /// <returns><c>true</c>, if region name exists, <c>false</c> otherwise.</returns>
        /// <param name="regionName">Region name to check.</param>
        public bool RegionNameExists(string regionName)
        {
            bool retVal = false;
            var rName = regionName.ToLower ();
            foreach (IScene scene in Scenes)
            {
                if (scene.RegionInfo.RegionName.ToLower() == rName)
                {
                    retVal = true;
                    break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Checks if a Region exists at a location.
        /// </summary>
        /// <returns><c>true</c>, if region exists at location, <c>false</c> otherwise.</returns>
        /// <param name="regionX">Region x.</param>
        /// <param name="regionY">Region y.</param>
        public bool RegionAtLocationExists(int regionX, int regionY)
        {
            bool retVal = false;
            foreach (IScene scene in Scenes)
            {
                if (scene.RegionInfo.RegionLocX == regionX &&
                    scene.RegionInfo.RegionLocY == regionY )
                {
                    retVal = true;
                    break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Finds the current region info.
        /// </summary>
        /// <returns>The current region info.</returns>
        public Dictionary<string, int> FindCurrentRegionInfo()
        {
            var rInfo = new Dictionary<string, int >();

            rInfo["minX"] = 0;
            rInfo["minY"] = 0;
            rInfo["port"] = 0;
            rInfo ["regions"] = 0;

            int regX, regY;
            foreach (IScene scene in Scenes)
            {
                regX = scene.RegionInfo.RegionLocX;
                if (rInfo ["minX"] <= regX)
                    rInfo ["minX"] = regX + scene.RegionInfo.RegionSizeX;

                regY = scene.RegionInfo.RegionLocY;
                if (rInfo ["minY"] < regY)
                    rInfo ["minY"] = regY + scene.RegionInfo.RegionSizeY;

                if (rInfo ["port"] < scene.RegionInfo.RegionPort)
                    rInfo ["port"] = scene.RegionInfo.RegionPort;

                rInfo ["regions"]++;
                }
            return rInfo;
        }

        /// <summary>
        /// Handles the create new region command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleCreateNewRegion(IScene scene, string[] cmd)
        {
            // get some current details
            var currentInfo = FindCurrentRegionInfo ();

            if (cmd.Length > 2)
            {
                CreateNewRegionExtended(scene, cmd);
            }
            else
            {
                ISimulationDataStore store = m_selectedDataService.Copy ();
                var newRegion = store.CreateNewRegion (m_SimBase, currentInfo);

                if (newRegion.RegionName != "abort")
                {
                    //                    StartRegion (store, store.CreateNewRegion (m_SimBase, currentInfo));
                    StartRegion (store, newRegion);

                    foreach (ISimulationDataStore st in m_simulationDataServices)
                        st.ForceBackup ();
                }
            }
        }

        /// <summary>
        /// Creates the new region.
        /// </summary>
        /// <param name="regionName">Region name.</param>
        void CreateNewRegion( string regionName)
        {
            // get some current details
            var currentInfo = FindCurrentRegionInfo ();

            // modified to pass a region name to use
            ISimulationDataStore store = m_selectedDataService.Copy ();
            //StartRegion (store, store.CreateNewRegion (m_SimBase, regionName, currentInfo));

            var newRegion = store.CreateNewRegion (m_SimBase, regionName, currentInfo);
            if (newRegion.RegionName != "abort")
            {
                StartRegion (store, newRegion);

                foreach (ISimulationDataStore st in m_simulationDataServices)
                    st.ForceBackup ();
            }
        }

        #endregion

        /// <summary>
        /// Creates the new region using addition options.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void CreateNewRegionExtended(IScene scene, string[] cmd)
        {

            string defaultDir = "";
            string defaultExt = ".xml";
            string regionName = "";
            string regionFile = "";

            List<string> newParams = new List<string>(cmd);
            foreach (string param in cmd)
            {
                if (param.StartsWith("--config", StringComparison.CurrentCultureIgnoreCase))
                {
                    regionFile = param.Remove(0, 9);
                    newParams.Remove(param);
                }
            }

            if (newParams.Count == 3)
            {
                regionName = newParams [2];
            }

            if (regionFile == "" )
            {
                CreateNewRegion (regionName);
                return;
            }

            // we have a config file and possibly a region name
            IConfig config = m_config.Configs["FileBasedSimulationData"];
            if (config != null)
                defaultDir = PathHelpers.ComputeFullPath (config.GetString ("StoreBackupDirectory", "Regions"));

            regionFile = PathHelpers.VerifyReadFile (regionFile, defaultExt, defaultDir);
            if (regionFile == "")
                return;

            // let's do it...
            MainConsole.Instance.Info ( "[SceneManager]: Loading region definition...." );
            RegionInfo loadRegion = new RegionInfo ();
            loadRegion.LoadRegionConfig( regionFile );

            if (loadRegion.RegionName != regionName)
            {
                if ( MainConsole.Instance.Prompt("You have specified a different name than what is specified in the configuration file\n"+
                    "Do you wish to rename the region to '" + regionName +"'? (yes/no): ") == "yes" )
                {
                    loadRegion.RegionName = regionName;
                }
            }

            //check for an existing Scene
            IScene region = m_scenes.Find((s) => s.RegionInfo.RegionName.ToLower() == regionName);
            if (region != null)
            {
                MainConsole.Instance.InfoFormat(
                    "ERROR: The region '{0}' already exists, please retry", regionName);
                return;
            }

            // indicate this is a new region
            loadRegion.NewRegion = true;

            // get some current details
            var currentInfo = FindCurrentRegionInfo ();

            // let's do it
            ISimulationDataStore store = m_selectedDataService.Copy ();
            //StartRegion (store, store.CreateNewRegion (m_SimBase, newRegion, currentInfo));

            var newRegion = store.CreateNewRegion (m_SimBase, loadRegion, currentInfo);
            if (newRegion.RegionName != "abort")
            {
                StartRegion (store, newRegion);

                // backup all our work
                foreach (ISimulationDataStore st in m_simulationDataServices)
                    st.ForceBackup ();
            }

        }

        /// <summary>
        /// Saves the region configuration.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleSaveRegionConfig(IScene scene, string[] cmd)
        {
 
            if (scene == null)
                return;

            string regionFile = "";
            string regionsDir = "";

            // get region config path in case...
            IConfig config = m_config.Configs["FileBasedSimulationData"];
            if (config != null)
                regionsDir = PathHelpers.ComputeFullPath (config.GetString ("StoreBackupDirectory", "Regions"));
                 
            if (cmd.Count () > 4)
            {
                regionFile = cmd [3];
                regionFile = PathHelpers.VerifyWriteFile (regionFile, ".xml", regionsDir, false);
            }

            // let's do it
            if (regionFile != "")
            {
//                regionFile = Path.Combine( regionsDir, scene.RegionInfo.RegionName + ".xml");

                MainConsole.Instance.InfoFormat ("[SceneManager]: Saving region configuration for {0} to {1} ...", 
                    scene.RegionInfo.RegionName, regionFile);
                scene.RegionInfo.SaveRegionConfig (regionFile);
            }

         }

        /// <summary>
        ///     Kicks users off the region
        /// </summary>
        /// <param name="cmdparams">name of avatar to kick</param>
        void KickUserCommand(IScene scene, string[] cmdparams)
        {
            IList agents = new List<IScenePresence>(scene.GetScenePresences());

            if (cmdparams.Length > 2 && cmdparams[2] == "all")
            {
                string alert = MainConsole.Instance.Prompt("Alert message: ", "");
                foreach (IScenePresence presence in agents)
                {
                    RegionInfo regionInfo = presence.Scene.RegionInfo;

                    MainConsole.Instance.Info(String.Format("Kicking user: {0,-16}{1,-37} in region: {2,-16}",
                                                            presence.Name, presence.UUID, regionInfo.RegionName));

                    // kick client...
                    presence.ControllingClient.Kick(alert ?? "\nThe WhiteCore manager kicked you out.\n");

                    // ...and close on our side
                    IEntityTransferModule transferModule =
                        presence.Scene.RequestModuleInterface<IEntityTransferModule>();
                    if (transferModule != null)
                        transferModule.IncomingCloseAgent(presence.Scene, presence.UUID);
                }
                return;
            }
            else
            {
                string username = MainConsole.Instance.Prompt("User to kick: ", "");
                string alert = MainConsole.Instance.Prompt("Alert message: ", "");

                foreach (IScenePresence presence in agents)
                {
                    if (presence.Name.ToLower().Contains(username.ToLower()))
                    {
                        MainConsole.Instance.Info(String.Format("Kicking user: {0} in region: {1}",
                                                                presence.Name, presence.Scene.RegionInfo.RegionName));

                        // kick client...
                        presence.ControllingClient.Kick(alert ?? "\nThe WhiteCore manager kicked you out.\n");

                        // ...and close on our side
                        IEntityTransferModule transferModule =
                            presence.Scene.RequestModuleInterface<IEntityTransferModule>();
                        if (transferModule != null)
                            transferModule.IncomingCloseAgent(presence.Scene, presence.UUID);
                    }
                }
            }
        }

        /// <summary>
        ///     Force resending of all updates to all clients in active region(s)
        /// </summary>
        /// <param name="args"></param>
        void HandleForceUpdate(IScene scene, string[] args)
        {
            MainConsole.Instance.Info("Updating all clients");
            ISceneEntity[] EntityList = scene.Entities.GetEntities();

            foreach (SceneObjectGroup ent in EntityList.OfType<SceneObjectGroup>())
            {
                (ent).ScheduleGroupUpdate(
                    PrimUpdateFlags.ForcedFullUpdate);
            }
            List<IScenePresence> presences = scene.Entities.GetPresences();

            foreach (IScenePresence presence in presences.Where(presence => !presence.IsChildAgent))
            {
                IScenePresence presence1 = presence;
                scene.ForEachClient(
                    client => client.SendAvatarDataImmediate(presence1));
            }
        }

        /// <summary>
        ///     Load, Unload, and list Region modules in use
        /// </summary>
        /// <param name="cmd"></param>
        void HandleModulesUnload(IScene scene, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] cmdparams = args.ToArray();

            IRegionModulesController controller =
                m_SimBase.ApplicationRegistry.RequestModuleInterface<IRegionModulesController>();
            if (cmdparams.Length > 1)
            {
                foreach (IRegionModuleBase irm in controller.AllModules)
                {
                    if (irm.Name.ToLower() == cmdparams[1].ToLower())
                    {
                        MainConsole.Instance.Info(String.Format("Unloading module: {0}", irm.Name));
                        irm.RemoveRegion(scene);
                        irm.Close();
                    }
                }
            }
        }

        /// <summary>
        ///     Load, Unload, and list Region modules in use
        /// </summary>
        /// <param name="cmd"></param>
        void HandleModulesList(IScene scene, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);

            IRegionModulesController controller =
                m_SimBase.ApplicationRegistry.RequestModuleInterface<IRegionModulesController>();
            foreach (IRegionModuleBase irm in controller.AllModules)
            {
                if (irm is INonSharedRegionModule)
                    MainConsole.Instance.Info(String.Format("Nonshared region module: {0}", irm.Name));
                else
                    MainConsole.Instance.Info(String.Format("Unknown type " + irm.GetType() + " region module: {0}",
                                                            irm.Name));
            }
        }

        /// <summary>
        ///     Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        void RunCommand(IScene scene, string[] cmdparams)
        {
            // TODO: Fix this so that additional commandline details can be passed
            if ( (MainConsole.Instance.ConsoleScene == null) &&
                (m_scenes.IndexOf(scene) == 0) )
            {
                MainConsole.Instance.Info ("[SceneManager]: Operating on the 'root' scene will run this command for all regions");
                //    if (MainConsole.Instance.Prompt ("Are you sure you want to do this? (yes/no)", "no") != "yes")
                //    return;
            }
           
            var regionName = scene.RegionInfo.RegionName;
            List<string> args = new List<string>(cmdparams);
            if (args.Count < 1)
                return;

            string command = args[0];
            args.RemoveAt(0);

            cmdparams = args.ToArray();

            switch (command)
            {
                case "reset":
                    if (cmdparams.Length > 0)
                        if (cmdparams[0] == "region")
                        {
                    if (MainConsole.Instance.Prompt("Are you sure you want to reset " + regionName +"? (yes/no)", "no") !=
                                "yes")
                                return;
                            ResetRegion(scene);
                        }
                    break;
                case "clear":
                    if (cmdparams.Length > 0)
                    if (cmdparams[0] == "region")
                    {
                    if (MainConsole.Instance.Prompt("Are you sure you want to clear all " + regionName +" objects? (yes/no)", "no") !=
                            "yes")
                            return;
                        ClearRegion(scene);
                    }
                break;
            case "remove": case "delete":
                    if (cmdparams.Length > 0)
                        if (cmdparams[0] == "region")
                        {
                    if (MainConsole.Instance.Prompt("Are you sure you want to remove " + regionName +"? (yes/no)", "no") !=
                                "yes")
                                return;
                            RemoveRegion(scene);
                        }
                    break;
                case "command-script":
                    if (cmdparams.Length > 0)
                    {
                        m_SimBase.RunCommandScript(cmdparams[0]);
                    }
                    break;

                case "restart-instance":
                    //This kills the instance and restarts it
                    IRestartModule restartModule = scene.RequestModuleInterface<IRestartModule>();
                    if (restartModule != null)
                        restartModule.RestartScene();
                    break;
            }
        }

        /// <summary>
        ///     Turn on some debugging values for WhiteCore.
        /// </summary>
        /// <param name="args"></param>
        protected void Debug(IScene scene, string[] args)
        {
            if (args.Length != 4)
                return;

            switch (args[1])
            {
                case "packet":
                    switch (args[2])
                    {
                        case "name":
                            bool remove = false;
                            string packetName = args[3];
                            if (packetName == "remove")
                            {
                                packetName = args[4];
                                remove = true;
                            }
                            SetDebugPacketName(scene, packetName, remove);
                            MainConsole.Instance.Info(String.Format("Added packet {0} to debug list", packetName));

                            break;
                        case "level":
                            int newDebug;
                            if (int.TryParse(args[3], out newDebug))
                            {
                                SetDebugPacketLevel(scene, newDebug);
                            }
                            else
                            {
                                MainConsole.Instance.Info("packet debug should be 0..255");
                            }
                            MainConsole.Instance.Info(String.Format("New packet debug: {0}", newDebug));

                            break;
                    }
                    break;
                default:

                    MainConsole.Instance.Info("Unknown debug");
                    break;
            }
        }

        /// <summary>
        ///     Set the debug packet level on the current scene.  This level governs which packets are printed out to the
        ///     console.
        /// </summary>
        /// <param name="newDebug"></param>
        void SetDebugPacketLevel(IScene scene, int newDebug)
        {
            scene.ForEachScenePresence(scenePresence =>
            {
                if (scenePresence.IsChildAgent) return;
                    MainConsole.Instance.DebugFormat(
                        "Packet debug for {0} set to {1}",
                        scenePresence.Name,
                        newDebug);

                scenePresence.ControllingClient.SetDebugPacketLevel(
                    newDebug);
            });
        }

        /// <summary>
        ///     Set the debug packet level on the current scene.  This level governs which packets are printed out to the
        ///     console.
        /// </summary>
        /// <param name="newDebug"></param>
        void SetDebugPacketName(IScene scene, string name, bool remove)
        {
            scene.ForEachScenePresence(scenePresence =>
            {
                if (scenePresence.IsChildAgent) return;
                    MainConsole.Instance.DebugFormat(
                        "Packet debug for {0} {2} to {1}",
                        scenePresence.Name,
                        name, remove ? "removed" : "set");

                    scenePresence.ControllingClient.SetDebugPacketName(name, remove);
            });
        }

        /// <summary>
        /// Handles the change region command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleChangeRegion(IScene scene, string[] cmd)
        {
            string regionName;
            if (cmd.Length < 3) 
            {
                do
                {
                    regionName = MainConsole.Instance.Prompt("Region to change to? (? for list)","");
                    if (regionName == "?")
                    {
                        var regions = GetRegionNames();
                        MainConsole.Instance.CleanInfo (" Available regions are : ");
                        foreach (string name in regions)
                            MainConsole.Instance.CleanInfo ("   " + name);
                    }
                } while (regionName == "?");

                if (regionName == "")
                    return;
            } else
                regionName = Util.CombineParams(cmd, 2); // in case of spaces in the name eg Steam Island

            string rName;
            regionName = regionName.ToLower();
            if (regionName.ToLower () == "root")
            {
                MainConsole.Instance.ConsoleScene = null;
                rName = "root";
            } else
            {
                var newScene = m_scenes.Find ((s) => s.RegionInfo.RegionName.ToLower () == regionName);
                if (newScene == null)
                {
                    MainConsole.Instance.Info (String.Format ("Region '"+ regionName + "' not found?"));
                    if ( MainConsole.Instance.ConsoleScene != null)
                        rName = MainConsole.Instance.ConsoleScene.RegionInfo.RegionName;
                    else
                        rName = "root";
                } else
                {
                    MainConsole.Instance.ConsoleScene = newScene;
                    rName = newScene.RegionInfo.RegionName;
                    MainConsole.Instance.Info ("[SceneManager]: Changed to region " + rName);
                }
            }
            SetRegionPrompt(rName);
        }

        /// <summary>
        /// Handles the show users command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleShowUsers(IScene scene, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] showParams = args.ToArray();

            List<IScenePresence> agents = new List<IScenePresence>();
            agents.AddRange(scene.GetScenePresences());
            if (showParams.Length == 1 || showParams[1] != "full")
                agents.RemoveAll(sp => sp.IsChildAgent);

            MainConsole.Instance.Info(String.Format("\n" + scene.RegionInfo.RegionName +": "+
                (agents.Count == 0 ? "No": agents.Count.ToString()) + " Agents connected\n"));
            if (agents.Count == 0)
                return;

            // we have some details to show...
            MainConsole.Instance.CleanInfo(String.Format("{0,-16}{1,-37}{2,-14}{3,-20}{4,-30}", 
                "Username", "Agent ID", "Root/Child", "Region", "Position"));

            foreach (IScenePresence presence in agents)
            {
                RegionInfo regionInfo = presence.Scene.RegionInfo;

                string regionName = regionInfo == null ? "Unresolvable" : regionInfo.RegionName;

                    MainConsole.Instance.CleanInfo(String.Format("{0,-16}{1,-37}{2,-14}{3,-20}{4,-30}", presence.Name,
                                                        presence.UUID, presence.IsChildAgent ? "Child" : "Root",
                                                        regionName, presence.AbsolutePosition.ToString()));
            }

            MainConsole.Instance.CleanInfo(String.Empty);
            MainConsole.Instance.CleanInfo(String.Empty);
        }

        /// <summary>
        /// Display the current scene (region) details.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleShowRegions(IScene scene, string[] cmd)
        {

            string sceneInfo;
            var regInfo = scene.RegionInfo;
            UserAccount EstateOwner;
            EstateOwner = scene.UserAccountService.GetUserAccount (null, regInfo.EstateSettings.EstateOwner);

            if ((MainConsole.Instance.ConsoleScene == null) &&
                (m_scenes.IndexOf (scene) == 0))
            {
                sceneInfo =  String.Format ("{0, -20}", "Region");
                sceneInfo += String.Format ("{0, -14}", "Startup");
                sceneInfo += String.Format ("{0, -16}", "Location");
                sceneInfo += String.Format ("{0, -12}", "Size");
                sceneInfo += String.Format ("{0, -8}", "Port");
                sceneInfo += String.Format ("{0, -20}", "Estate");
                sceneInfo += String.Format ("{0, -20}", "Estate Owner");

                MainConsole.Instance.CleanInfo(sceneInfo);
                MainConsole.Instance.CleanInfo("--------------------------------------------------------------------------------------------------------");

            }

            // TODO ... change hardcoded field sizes to public constants
            sceneInfo =  String.Format ("{0, -20}", regInfo.RegionName);
            sceneInfo += String.Format ("{0, -14}", regInfo.Startup);
            sceneInfo += String.Format ("{0, -16}", regInfo.RegionLocX / Constants.RegionSize + "," + regInfo.RegionLocY / Constants.RegionSize);
            sceneInfo += String.Format ("{0, -12}", regInfo.RegionSizeX + "x" + regInfo.RegionSizeY);
            sceneInfo += String.Format ("{0, -8}", regInfo.RegionPort);
            sceneInfo += String.Format ("{0, -20}", regInfo.EstateSettings.EstateName);
            sceneInfo += String.Format ("{0, -20}", EstateOwner.Name);

            MainConsole.Instance.CleanInfo(sceneInfo);

        }

        /// <summary>
        /// Handles the show maturity command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleShowMaturity(IScene scene, string[] cmd)
        {
            string rating = "";
            if (scene.RegionInfo.RegionSettings.Maturity == 1)
            {
                rating = "Mature";
            }
            else if (scene.RegionInfo.RegionSettings.Maturity == 2)
            {
                rating = "Adult";
            }
            else
            {
                rating = "PG";
            }
            MainConsole.Instance.Info(String.Format("Region Name: {0}, Region Rating {1}", scene.RegionInfo.RegionName,
                                                    rating));
        }

        public List<string> GetOARFilenames()
        {
            var defaultOarDir = Constants.DEFAULT_OARARCHIVE_DIR;
            var retVals = new List<string>();

            if (Directory.Exists (defaultOarDir))
            {
                var archives = new List<string> (Directory.GetFiles (Constants.DEFAULT_OARARCHIVE_DIR, "*.oar"));
                archives.AddRange (new List<string> (Directory.GetFiles (Constants.DEFAULT_OARARCHIVE_DIR, "*.tgz")));
                foreach (string file in archives)
                    retVals.Add (Path.GetFileNameWithoutExtension (file));
            }
            return retVals;
        }

        /// <summary>
        ///     Load a whole region from an opensimulator archive.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleLoadOar(IScene scene, string[] cmdparams)
        {
            string fileName;

            // a couple of sanity checks
			if (cmdparams.Count() < 3)
            {
                do
                {
                    fileName = MainConsole.Instance.Prompt("OAR to load (? for list)", "");
                    if (fileName == "")
                        return;

                    if (fileName == "?")
                    {
                        var archives = GetOARFilenames();
                        if (archives.Count > 0)
                        {
                            MainConsole.Instance.CleanInfo (" Available archives are : ");
                            foreach (string file in archives)
                                MainConsole.Instance.CleanInfo ("   " + file);
                        } else
                            MainConsole.Instance.CleanInfo (" Sorry!, no archives are currently available.");

                        fileName = "";
                    }
                } while (fileName == "");


                // need to add this filename to the cmdparams
                var newParams = new List<string>(cmdparams);
                newParams.Add(fileName);
                cmdparams = newParams.ToArray();

            } else
                fileName = cmdparams[2];

			if (fileName.StartsWith("--", StringComparison.CurrentCultureIgnoreCase))
			{
				MainConsole.Instance.Info("[Error] Command format is 'load oar Filename [optional switches]'");
				return;
			}

            fileName = PathHelpers.VerifyReadFile (fileName, new List<string>() {".oar","tgz"}, Constants.DEFAULT_OARARCHIVE_DIR);
            if (fileName == "")                 // something wrong...
                return;
            cmdparams [2] = fileName;           // reset passed filename

            // should be good to go...
            string regionName = scene.RegionInfo.RegionName;
            if (MainConsole.Instance.ConsoleScene == null)
            {
                if ( m_scenes.IndexOf(scene) == 0 )
                    MainConsole.Instance.Warn ("[SceneManager]: Operating on the 'root' will load the OAR into all regions");
                if (MainConsole.Instance.Prompt ("[SceneManager]: Do you wish to load this OAR into " + regionName + "? (yes/no)", "no") != "yes")
                    return;
            }

            try
            {
                IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
                if (archiver != null)
                {
                    var success = archiver.HandleLoadOarConsoleCommand(cmdparams);
                    if (!success)
                    {
                        ResetRegion(scene);

                        ISimulationDataStore simStore = scene.SimulationDataService;
                        success = simStore.RestoreLastBackup (scene.RegionInfo.RegionName);
                        if(success)
                        {
                            scene.RegionInfo = m_selectedDataService.LoadRegionNameInfo (regionName, m_SimBase);
                            MainConsole.Instance.Warn ("[SceneManager]: Region has been reloaded from the previous backup");
                        }
                    }

                    // force a map update 
                    var mapGen = scene.RequestModuleInterface<IMapImageGenerator>();
                    mapGen.UpdateWorldMaps();

                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.Error(e.ToString());
            }
        }

        /// <summary>
        ///     Save a region to a file, including all the assets needed to restore it.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveOar(IScene scene, string[] cmdparams)
        {
            string fileName;

            if (MainConsole.Instance.ConsoleScene == null) 
            {
                MainConsole.Instance.Info ("[SceneManager]: This command requires a region to be selected\n          Please change to a region first");
                return;
            }

            // a couple of sanity checks
            if (cmdparams.Count () < 3)
            {
                fileName = MainConsole.Instance.Prompt ("Filename for the save OAR operation.", scene.RegionInfo.RegionName);
                if (fileName == "")
                    return;

                // need to add this to the cmdparams
                var newParams = new List<string>(cmdparams);
                newParams.Add(fileName);
                cmdparams = newParams.ToArray();
            }
            else
                fileName = cmdparams[2];

            fileName = PathHelpers.VerifyWriteFile (fileName, ".oar", Constants.DEFAULT_OARARCHIVE_DIR, true);
            if (fileName == "")                 // something wrong...
                return;
            cmdparams [2] = fileName;           // reset passed filename

            // should be good to go...
            IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
            if (archiver != null)
                archiver.HandleSaveOarConsoleCommand(cmdparams);
        }

        /// <summary>
        /// Resizes the scale of a primative/object with the name specified
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        void HandleResizeObject(IScene scene, string[] cmdparams)
        {
            if (cmdparams.Length < 4)
            {
                MainConsole.Instance.Info("usage: resize object <prim name> <x> <y> <z>");
                return;
            }

            var primName = cmdparams [2];

            // assume overall scaling factor initially
            var xScale = Convert.ToSingle (cmdparams [3]);
            var yScale = xScale;
            var zScale = xScale;

            // individual axis scaling?
            if (cmdparams.Length > 4)
                yScale = Convert.ToSingle (cmdparams [4]);
            if (cmdparams.Length > 5)
                zScale = Convert.ToSingle (cmdparams [5]);

            var newScale = new Vector3 (xScale, yScale, zScale);

            //MainConsole.Instance.DebugFormat("Searching for Object: '{0}'", primName);

            ISceneEntity[] entityList = scene.Entities.GetEntities ();
            foreach (ISceneEntity ent in entityList)
            {
                if (ent is SceneObjectGroup && (ent.Name == primName))
                {
                    MainConsole.Instance.InfoFormat("Object: " + primName + " found, resizing..." );
                    var entParts = ent.ChildrenEntities ();
                    foreach (ISceneChildEntity enp in entParts)
                    {
                        if ( enp != null  )
                        {
                            enp.Resize( enp.Scale * newScale);

                            var curOffset = enp.OffsetPosition;
                            enp.OffsetPosition = (curOffset * newScale);

                            //MainConsole.Instance.Info("    Edited scale of child part: " +  enp.Name);
                        }
                    }
                    MainConsole.Instance.InfoFormat("Object: {0} has been resized", primName);
                    return;          // no need to continue searching as we have done it.
                }
            }
            MainConsole.Instance.WarnFormat("Sorry.. could not find '{0}'", primName);
       
        }

        /// <summary>
        /// Handles object info displays.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        void HandleShowObjects(IScene scene, string[] cmdparams)
        {
            string objectName = null;
            bool found = false;

            if (cmdparams.Length > 2)
            {
                objectName = Util.CombineParams(cmdparams, 2); // in case of spaces in the name eg Steam Island
            }

            ISceneEntity[] entityList = scene.Entities.GetEntities ();
            foreach (ISceneEntity ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if ( objectName == null || (ent.Name.Substring(0,objectName.Length) == objectName))
                    {
                        found = true;

                        var entParts = ent.ChildrenEntities ();
                        MainConsole.Instance.Info("Object: " + ent.Name + " at position " + ent.AbsolutePosition + ", comprised of " +entParts.Count + " parts" );

                        // specific object requested?
                        if (objectName != null)
                        {
                            foreach (ISceneChildEntity enp in entParts)
                            {
                                if (enp != null)
                                {
                                    MainConsole.Instance.Info ("    " + enp.Name + (enp.IsRoot ? "  [ Root prim ]" : "") );
                                }
                            }
                        }
                    }
                }
            }
            if (!found)
                if (objectName == null)
                MainConsole.Instance.Warn (" There does not appear to be any objects in this region");
                else
                MainConsole.Instance.WarnFormat("Sorry.. could not find '{0}'", objectName);

        }

        /// <summary>
        /// Handles rotating an entire scene.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        void HandleRotateScene(IScene scene, string[] cmdparams)
        {
            var usage = "Usage: rotate scene objects <angle in degrees> [centerX centerY]\n"+
                "(centerX and centerY are optional and default to the center of the region";

            if (cmdparams.Length < 4)
            {
                MainConsole.Instance.Info(usage);
                return;
            }

            var centerX = scene.RegionInfo.RegionSizeX * 0.5f;
            var centerY = scene.RegionInfo.RegionSizeY * 0.5f;

            var degrees = Convert.ToSingle(cmdparams[3]);
            var angle = (float) (degrees * (Math.PI/180));

            // normalize rotation angle  -ve, anticlockwise, +ve clockwise
            angle *= -1f;

            Quaternion rot = Quaternion.CreateFromAxisAngle(0, 0, 1, angle);

            // center supplied?
            if (cmdparams.Length > 4)
                centerX = Convert.ToSingle(cmdparams[4]);
            if (cmdparams.Length > 5)
                centerY = Convert.ToSingle(cmdparams[5]);

            var center = new Vector3(centerX, centerY, 0.0f);
            ISceneEntity[] entitlList = scene.Entities.GetEntities ();

            foreach (ISceneEntity ent in entitlList)
            {
                if (!ent.IsAttachment)
                {
                    ent.UpdateGroupRotationR (rot * ent.GroupRotation);
                    Vector3 offset = ent.AbsolutePosition - center;
                    offset *= rot;
                    ent.UpdateGroupPosition (center + offset, true);
                }
            }
            MainConsole.Instance.Info("    Rotation of region objects completed");

        }

        /// <summary>
        /// Handles scaling of a scene.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        void HandleScaleScene(IScene scene, string[] cmdparams)
        {
            string usage = "Usage: scale region objects <factor>";

            if (cmdparams.Length < 4)
            {
                MainConsole.Instance.Info(usage);
                return;
            }

            float factor = Convert.ToSingle(cmdparams[3]);
            var centerX = scene.RegionInfo.RegionSizeX * 0.5f;
            var centerY = scene.RegionInfo.RegionSizeY * 0.5f;

            // center supplied?
            if (cmdparams.Length > 4)
                centerX = Convert.ToSingle(cmdparams[4]);
            if (cmdparams.Length > 5)
                centerY = Convert.ToSingle(cmdparams[5]);

            var center = new Vector3(centerX, centerY, 0.0f);
            ITerrainChannel heightmap = scene.RequestModuleInterface<ITerrainChannel>();
            ISceneEntity[] entitlList = scene.Entities.GetEntities ();

            // let' do some resizing
            foreach (ISceneEntity ent in entitlList)
            {
                if (!ent.IsAttachment)
                {
                    Vector3 offsetPos = ent.AbsolutePosition - center;
                    // offset above/below the current land height
                    var offsetZ = ent.AbsolutePosition.Z - heightmap.GetNormalizedGroundHeight( (int)ent.AbsolutePosition.X, (int)ent.AbsolutePosition.Y );

                    offsetPos.Z = offsetZ;          // only scale theheight offset 
                    offsetPos *= factor;    

                    var entParts = ent.ChildrenEntities ();
                    foreach (ISceneChildEntity enp in entParts)
                    {
                        enp.Resize( enp.Scale * factor);

                        var curOffset = enp.OffsetPosition;
                        enp.OffsetPosition = (curOffset * factor);

                    }

                    // account for terrain height and reposition
                    var newPos = offsetPos + center;
                    newPos.Z += heightmap.GetNormalizedGroundHeight((int)newPos.X, (int)newPos.Y);
                    ent.UpdateGroupPosition (newPos, true);
                }
            }
            MainConsole.Instance.Info("    Rescaling of region objects completed");

        }



        /// <summary>
        /// Handles moving all scene objects.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        void HandleTranslateScene(IScene scene, string[] cmdparams)
        {
             if (cmdparams.Length < 6)
            {
                MainConsole.Instance.Info("Usage: translate scene objects <xOffset> <yOffset> <zOffset>");
                return;
            }

            var xOffset = Convert.ToSingle(cmdparams[3]);
            var yOffset = Convert.ToSingle(cmdparams[4]);
            var zOffset = Convert.ToSingle(cmdparams[5]);

            var offset = new Vector3(xOffset, yOffset, zOffset);
            ISceneEntity[] entitlList = scene.Entities.GetEntities ();

            foreach (ISceneEntity ent in entitlList)
            {
                if (!ent.IsAttachment)
                {
                    ent.UpdateGroupPosition (ent.AbsolutePosition + offset, true);
                }
            }
            MainConsole.Instance.Info("Region objects have been offset");
        }
            
        string GetCmdRegionName(string prompt)
        {
            string regionName;
            regionName = MainConsole.Instance.Prompt (prompt, "");
            if (regionName == "")
                return "";

            regionName = regionName.ToLower();
            return regionName;
        }

        /// <summary>
        /// Handles the delete region command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleDeleteRegion(IScene scene, string[] cmd)
        {
            // command is delete/remove region [regionname]
            string regionName;
            if (cmd.Length < 3)
            {
                regionName = MainConsole.Instance.Prompt ("Region to delete?", "");
                if (regionName == "")
                    return;
            } else
                regionName = Util.CombineParams(cmd, 2); // in case of spaces in the name eg Steam Island

            regionName = regionName.ToLower();
            IScene delScene = m_scenes.Find((s) => s.RegionInfo.RegionName.ToLower() == regionName);

            if (delScene == null)
            {
                MainConsole.Instance.WarnFormat ("[SceneManager]: Sorry, {0} was not found", regionName);
                return;
            }

            // last chance
            if (MainConsole.Instance.Prompt("Are you sure you want to remove " + regionName +"? (yes/no)", "no") != "yes")
                return;

            RemoveRegion(delScene);
            if(delScene != scene)
                MainConsole.Instance.ConsoleScene = scene;
        }

        /// <summary>
        /// Handles the reset region command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleResetRegion(IScene scene, string[] cmd)
        {
            string regionName;
            if (cmd.Length < 3)
            {
                regionName = MainConsole.Instance.Prompt ("Region to reset?", "");
                if (regionName == "")
                    return;
            } else
                regionName = Util.CombineParams(cmd, 2); // in case of spaces in the name eg Steam Island

            regionName = regionName.ToLower();
            IScene resetScene = m_scenes.Find((s) => s.RegionInfo.RegionName.ToLower() == regionName);

            if (resetScene == null)
            {
                MainConsole.Instance.WarnFormat ("[SceneManager]: Sorry, {0} was not found", regionName);
                return;
            }

            // last chance
            if (MainConsole.Instance.Prompt("Are you sure you want to reset " + regionName +"? (yes/no)", "no") != "yes")
                return;

            ResetRegion(resetScene);

        }

        /// <summary>
        /// Handles the clear region command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleClearRegion(IScene scene, string[] cmd)
        {
            string regionName;
            if (cmd.Length < 3)
            {
                regionName = MainConsole.Instance.Prompt ("Region to clear?", "");
                if (regionName == "")
                    return;
            } else
                regionName = Util.CombineParams(cmd, 2); // in case of spaces in the name eg Steam Island

            regionName = regionName.ToLower();
            IScene clearScene = m_scenes.Find((s) => s.RegionInfo.RegionName.ToLower() == regionName);

            if (clearScene == null)
            {
                MainConsole.Instance.WarnFormat ("[SceneManager]: Sorry, {0} was not found", regionName);
                return;
            }

            // last chance
            if (MainConsole.Instance.Prompt("Are you sure you want to clear all " + regionName +" objects? (yes/no)", "no") != "yes")
                return;

            IBackupModule backup = clearScene.RequestModuleInterface<IBackupModule>();

            if (backup != null)
            {
                if (MainConsole.Instance.Prompt ("Would you like to backup before clearing? (yes/no)", "yes") == "yes")
                    clearScene.SimulationDataService.ForceBackup ();

                backup.DeleteAllSceneObjects (); //Remove all the objects from the region
                MainConsole.Instance.Warn (regionName + " has been cleared of objects");
            } else
                MainConsole.Instance.Error ("Unable to locate the backup module for "+ regionName + ". Clear aborted");

        }
            
        /// <summary>
        /// Handles the reload region command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleReloadRegion(IScene scene, string[] cmd)
        {
            IScene loadScene = scene;
            string regionName;

            if (MainConsole.Instance.ConsoleScene == null)
            {
                regionName = MainConsole.Instance.Prompt ("Region to load from backup?", "");
                if (regionName == "")
                    return;

                regionName = regionName.ToLower ();
                loadScene = m_scenes.Find ((s) => s.RegionInfo.RegionName.ToLower () == regionName);

                if (loadScene == null)
                {
                    MainConsole.Instance.WarnFormat ("[SceneManager]: Sorry, {0} was not found", regionName);
                    return;
                }

            } else
                regionName = scene.RegionInfo.RegionName;


            // we have the correct scene
            string backupFileName;
            if (cmd.Length < 4)
            {
                backupFileName = MainConsole.Instance.Prompt ("Backup file to load? (Previous / FileName to load)", "Previous");
                if (backupFileName == "")
                    return;
            } else
                backupFileName = cmd [3];

            if (backupFileName == "Previous")
            {
                ISimulationDataStore simStore = loadScene.SimulationDataService;
                backupFileName = simStore.GetLastBackupFileName (regionName);
                if (backupFileName == "")
                {
                    MainConsole.Instance.Warn ("[SceneManager]: Sorry, unable to find any backups for " + regionName);
                    return;
                }
            } else
            {
                if (!backupFileName.ToLower ().StartsWith (regionName))
                {
                    MainConsole.Instance.Warn ("[SceneManager]: Only backups from the same region should be restored!");
                    return;
                }
            }

            backupFileName = PathHelpers.VerifyReadFile (backupFileName, ".sim", "");
            if (backupFileName == "")
                return;

            //we have verified what we need so... last chance
            if (MainConsole.Instance.Prompt("Are you sure you want load " + regionName +
                " from "+ Path.GetFileName(backupFileName) + "? (yes/no)", "no") != "yes")
                return;

            // let's do it.. 
            if (loadScene.SimulationDataService.RestoreBackupFile(backupFileName, regionName))
            {
                loadScene.RegionInfo = m_selectedDataService.LoadRegionNameInfo (regionName, m_SimBase);
                CloseRegion(loadScene, ShutdownType.Immediate, 0, true);
                MainConsole.Instance.ConsoleScenes = m_scenes;

                RegionInfo region = m_selectedDataService.LoadRegionNameInfo (regionName, m_SimBase);

                StartRegion (m_selectedDataService, region);
                MainConsole.Instance.WarnFormat ("[SceneManager]: {0} has been reloaded from the backup", regionName);
            }
        }


        /// <summary>
        /// Handles set region capacity.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleSetRegionCapacity(IScene scene, string[] cmd)
        {
            int regionCapacity = scene.RegionInfo.ObjectCapacity;
            int newCapacity;
            string regionName = scene.RegionInfo.RegionName;

            if (cmd.Length < 4)
            {
                var response = MainConsole.Instance.Prompt ("[SceneManager]: New prim capacity for " + regionName + "(-1 to cancel)", regionCapacity.ToString());
                int.TryParse (response, out newCapacity);
                if (newCapacity == -1)
                    return;

            } else
                int.TryParse( cmd[3], out newCapacity );

            bool setCapacity = true;
            if (newCapacity == 0)
            {
                var response = MainConsole.Instance.Prompt ("Set region prims to zero. Are you sure? (yes/no)", "no");
                setCapacity = response.ToLower ().StartsWith ("y");
            }

            if (setCapacity)
            {
                scene.RegionInfo.ObjectCapacity = newCapacity;
                MainConsole.Instance.InfoFormat("[SceneManager]: New prim capacity for {0} set to {1}", regionName, newCapacity); 
                scene.SimulationDataService.ForceBackup();
            }
        }

        /// <summary>
        /// Handles set region startup.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleSetRegionStartup(IScene scene, string[] cmd)
        {

            string regionName = scene.RegionInfo.RegionName;
            string response = "No";

            if (cmd.Length < 4)
                response = MainConsole.Instance.Prompt ("[SceneManager]: Delay " + regionName + " script startup? (yes/no)", response);
            else
                response = cmd [3];

            response = response.ToLower ();
            scene.RegionInfo.Startup = response.StartsWith ("n") ? StartupType.Normal : StartupType.Medium;

            MainConsole.Instance.InfoFormat("[SceneManager]: Region has been set for {0} script startup.",
                (scene.RegionInfo.Startup == StartupType.Normal) ? "Normal": "Delayed"); 
            scene.SimulationDataService.ForceBackup();
        }


        /// <summary>
        /// Handles set region infinite.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleSetRegionInfinite(IScene scene, string[] cmd)
        {
            string regionName = scene.RegionInfo.RegionName;
            string response = "No";

            if (cmd.Length < 4)
                response = MainConsole.Instance.Prompt ("[SceneManager]: Set " + regionName + " as infinite? (yes/no)", response);
            else
                response = cmd [3];

            response = response.ToLower ();
            scene.RegionInfo.InfiniteRegion = response.StartsWith ("y");

            MainConsole.Instance.Info("[SceneManager]: Region has been set as " +
                (scene.RegionInfo.InfiniteRegion ? "Infinite": "Finite")); 

            scene.SimulationDataService.ForceBackup();
        }

        /// <summary>
        /// Handles set region neighbour visibility.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void HandleSetRegionVisibility(IScene scene, string[] cmd)
        {
            string regionName = scene.RegionInfo.RegionName;
            string response = "Yes";

            if (cmd.Length < 4)
                response = MainConsole.Instance.Prompt ("[SceneManager]: Allow neighbours to see into " + regionName + "(yes/no)", response);
            else
                response = cmd [3];

            response = response.ToLower ();
            scene.RegionInfo.SeeIntoThisSimFromNeighbor = response.StartsWith ("y");

            MainConsole.Instance.InfoFormat("[SceneManager]: Region has been set to {0} visibility from neighbours.",
                scene.RegionInfo.SeeIntoThisSimFromNeighbor ? " Allow": "Disallow"); 

            scene.SimulationDataService.ForceBackup();
        }

        #endregion
    }
}
