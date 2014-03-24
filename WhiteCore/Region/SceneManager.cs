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
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Timer = System.Timers.Timer;
using System.IO;

namespace WhiteCore.Region
{
    /// <summary>
    ///     Manager for adding, closing, reseting, and restarting scenes.
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

        protected ISimulationBase m_OpenSimBase;
        protected List<IScene> m_scenes = new List<IScene>();

        public List<IScene> Scenes { get { return m_scenes; } }

        protected List<ISimulationDataStore> m_simulationDataServices = new List<ISimulationDataStore>();
        protected ISimulationDataStore m_selectedDataService;

        private IConfigSource m_config = null;

        public IConfigSource ConfigSource
        {
            get { return m_config; }
        }

        #endregion

        #region IApplicationPlugin members

        public void PreStartup(ISimulationBase simBase)
        {
            m_OpenSimBase = simBase;

            IConfig handlerConfig = simBase.ConfigSource.Configs["ApplicationPlugins"];
            if (handlerConfig.GetString("SceneManager", "") != Name)
                return;

            m_config = simBase.ConfigSource;
            //Register us!
            m_OpenSimBase.ApplicationRegistry.RegisterModuleInterface<ISceneManager>(this);
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
            MainConsole.Instance.DefaultPrompt = "Region [root]";
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
                CloseRegion(scene, ShutdownType.Immediate, 0);
        }

        #endregion

        #region Startup complete

        public void HandleStartupComplete(List<string> data)
        {
            //Tell modules about it 
            StartupCompleteModules();
            m_OpenSimBase.RunStartupCommands();

            TimeSpan timeTaken = DateTime.Now - m_OpenSimBase.StartupTime;

            MainConsole.Instance.InfoFormat(
                "[SceneManager]: Startup Complete. This took {0}m {1}.{2}s",
                timeTaken.Minutes, timeTaken.Seconds, timeTaken.Milliseconds);

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
            List<string> regionFiles = m_selectedDataService.FindRegionInfos(out newRegion, m_OpenSimBase);
            if (newRegion)
            {
                ISimulationDataStore store = m_selectedDataService.Copy();
                regions.Add(new KeyValuePair<ISimulationDataStore, RegionInfo>(store, store.CreateNewRegion(m_OpenSimBase)));
            }
            else
            {
                foreach (string fileName in regionFiles)
                {
                    ISimulationDataStore store = m_selectedDataService.Copy();
                    regions.Add(new KeyValuePair<ISimulationDataStore, RegionInfo>(store, 
                        store.LoadRegionInfo(fileName, m_OpenSimBase)));
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
            ISceneLoader sceneLoader = m_OpenSimBase.ApplicationRegistry.RequestModuleInterface<ISceneLoader>();
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

        public void RemoveRegion(IScene scene)
        {
            scene.SimulationDataService.RemoveRegion();
            IGridRegisterModule gridRegisterModule = scene.RequestModuleInterface<IGridRegisterModule>();
            gridRegisterModule.DeleteRegion(scene);
            MainConsole.Instance.Warn("Region " + scene.RegionInfo.RegionName + " was removed, restarting the instance in 10 seconds");
            System.Threading.Thread.Sleep(10000);
            Environment.Exit(0);
        }

        #endregion

        #region Restart a region

        public void RestartRegion(IScene scene)
        {
            CloseRegion(scene, ShutdownType.Immediate, 0);

            IConfig startupConfig = m_config.Configs["Startup"];
            if (startupConfig == null || !startupConfig.GetBoolean("RegionRestartCausesShutdown", false))
                StartRegion(scene.SimulationDataService, scene.RegionInfo);
            else
            {
                //Kill us now
                m_OpenSimBase.Shutdown(true);
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
        public void CloseRegion(IScene scene, ShutdownType type, int seconds)
        {
            if (type == ShutdownType.Immediate)
            {
                scene.Close(true);
                if (OnCloseScene != null)
                    OnCloseScene(scene);
                CloseModules(scene);
                m_scenes.Remove(scene);
            }
            else
            {
                Timer t = new Timer(seconds*1000); //Millisecond conversion
                t.Elapsed += (sender, e) => CloseRegion(scene, ShutdownType.Immediate, 0);
                t.AutoReset = false;
                t.Start();
            }
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
                module.Initialise(scene, m_config, m_OpenSimBase);
            }
            //Then do the ISharedRegionModule and INonSharedRegionModules
            MainConsole.Instance.Debug("[Modules]: Loading region modules");
            IRegionModulesController controller;
            if (m_OpenSimBase.ApplicationRegistry.TryRequestModuleInterface(out controller))
            {
                controller.AddRegionToModules(scene);
            }
            else
                MainConsole.Instance.Error("[Modules]: The new RegionModulesController is missing...");
            //Then finish the rest of the SharedRegionStartupModules
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.PostInitialise(scene, m_config, m_OpenSimBase);
            }
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.FinishStartup(scene, m_config, m_OpenSimBase);
            }
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.PostFinishStartup(scene, m_config, m_OpenSimBase);
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
            if (m_OpenSimBase.ApplicationRegistry.TryRequestModuleInterface(out controller))
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

        private void AddConsoleCommands()
        {
            if (MainConsole.Instance == null)
                return;
            MainConsole.Instance.Commands.AddCommand("show users", "show users [full]",
                                                     "Shows users in the given region (if full is added, child agents are shown as well)",
                                                     HandleShowUsers, true, false);
            MainConsole.Instance.Commands.AddCommand("change region", "change region [region name]",
                                                     "Changes the region that commands will run on (or root for the commands to run on all regions)",
                                                     HandleChangeRegion, false, true);
            MainConsole.Instance.Commands.AddCommand("show regions", "show regions",
                                                     "Show information about all regions in this instance",
                                                     HandleShowRegions, true, false);
            MainConsole.Instance.Commands.AddCommand("show maturity", "show maturity",
                                                     "Show all region's maturity levels", HandleShowMaturity, true, false);

            MainConsole.Instance.Commands.AddCommand("force update", "force update",
                                                     "Force the update of all objects on clients", HandleForceUpdate, true, false);

            MainConsole.Instance.Commands.AddCommand("debug packet level", "debug packet level [level]", "Turn on packet debugging",
                                                     Debug, true, false);
            MainConsole.Instance.Commands.AddCommand("debug packet name", "debug packet name [packetname]", "Turn on packet debugging for a specific packet",
                                                     Debug, true, false);
            MainConsole.Instance.Commands.AddCommand("debug packet name remove", "debug packet name [packetname]", "Turn off packet debugging for a specific packet",
                                                     Debug, true, false);
            MainConsole.Instance.Commands.AddCommand("debug scene", "debug scene [scripting] [collisions] [physics]",
                                                     "Turn on scene debugging", Debug, true, false);

            MainConsole.Instance.Commands.AddCommand("load oar",
                                                     "load oar [oar name] [--merge] [--skip-assets] [--OffsetX=#] [--OffsetY=#] [--OffsetZ=#] [--FlipX] [--FlipY] [--UseParcelOwnership] [--CheckOwnership]",
                                                     "Load a region's data from OAR archive.  \n" +
                                                     "--merge will merge the oar with the existing scene (including parcels).  \n" +
                                                     "--skip-assets will load the oar but ignore the assets it contains. \n" +
                                                     "--OffsetX will change where the X location of the oar is loaded, and the same for Y and Z.  \n" +
                                                     "--FlipX flips the region on the X axis.  \n" +
                                                     "--FlipY flips the region on the Y axis.  \n" +
				                                     "--UseParcelOwnership changes who the default owner of objects whose owner cannot be found from the Estate Owner to the parcel owner on which the object is found.  \n" +
                                                     "--CheckOwnership asks for each UUID that is not found on the grid what user it should be changed to (useful for changing UUIDs from other grids, but very long with many users).  ",
                                                     LoadOar, true, false);

            MainConsole.Instance.Commands.AddCommand("save oar", "save oar [<OAR path>] [--perm=<permissions>] ",
                                                     "Save a region's data to an OAR archive" + Environment.NewLine
                                                     + "<OAR path> The OAR path must be a filesystem path."
                                                     +
                                                     "  If this is not given then the oar is saved to region.oar in the current directory." +
                                                     Environment.NewLine
                                                     +
                                                     "--perm stops objects with insufficient permissions from being saved to the OAR." +
                                                     Environment.NewLine
                                                     +
                                                     "  <permissions> can contain one or more of these characters: \"C\" = Copy, \"T\" = Transfer" +
                                                     Environment.NewLine, SaveOar, true, false);

            MainConsole.Instance.Commands.AddCommand("kick user", "kick user [all]",
                                                     "Kick a user off the simulator", KickUserCommand, true, false);

            MainConsole.Instance.Commands.AddCommand("reset region", "reset region",
                                                     "Reset region to the default terrain, wipe all prims, etc.",
                                                     RunCommand, true, false);

            MainConsole.Instance.Commands.AddCommand("remove region", "remove region",
                                                     "Remove region from the grid, and delete all info associated with it",
                                                     RunCommand, true, false);

            MainConsole.Instance.Commands.AddCommand("restart-instance", "restart-instance",
                                                     "Restarts the instance (as if you closed and re-opened WhiteCore)",
                                                     RunCommand, true, false);

            MainConsole.Instance.Commands.AddCommand("command-script", "command-script [script]",
                                                     "Run a command script from file", RunCommand, false, false);

            MainConsole.Instance.Commands.AddCommand("modules list", "modules list", "Lists all simulator modules",
                                                     HandleModulesList, true, false);

            MainConsole.Instance.Commands.AddCommand("modules unload", "modules unload [module]",
                                                     "Unload the given simulator module", HandleModulesUnload, true, false);
            
            MainConsole.Instance.Commands.AddCommand("create region", "create region <Region Name>  <--config=filename>",
                "Creates a new region to start\n"+
                "<Region Name> - Use this name for the new region\n"+
                "--config='filename' - Use this file for region configuration",
                                                         CreateNewRegion, false, true);
			
            MainConsole.Instance.Commands.AddCommand("save region config", "save region config <filename>",
                "Saves the configuration of the region\n"+
                "<filename> - Use this name for the region configuration",
                SaveRegionConfig, true, false);
                
        }

        private void CreateNewRegion(IScene scene, string[] cmd)
        {

            if (cmd.Length > 2)
            {
                CreateNewRegionExtended(scene, cmd);
            }
            else
            {
                // original 'no paramters' command
                ISimulationDataStore store = m_selectedDataService.Copy ();
                StartRegion (store, store.CreateNewRegion (m_OpenSimBase));

                foreach (ISimulationDataStore st in m_simulationDataServices)
                    st.ForceBackup ();
            }
        }

        private void CreateNewRegion( string regionName)
        {
            // modified to pass a region name to use
            ISimulationDataStore store = m_selectedDataService.Copy ();
            StartRegion (store, store.CreateNewRegion (m_OpenSimBase, regionName));

            foreach (ISimulationDataStore st in m_simulationDataServices)
                st.ForceBackup ();
        }

        /// <summary>
        /// Creates the new region using addition options.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        private void CreateNewRegionExtended(IScene scene, string[] cmd)
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
            RegionInfo newRegion = new RegionInfo ();
            newRegion.LoadRegionConfig( regionFile );

            if (newRegion.RegionName != regionName)
            {
                if ( MainConsole.Instance.Prompt("You have specified a different name than what is specified in the configuration file\n"+
                    "Do you wish to rename the region to '" + regionName +"'? (yes/no): ") == "yes" )
                {
                    newRegion.RegionName = regionName;
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
            newRegion.NewRegion = true;

            // let's do it
            ISimulationDataStore store = m_selectedDataService.Copy ();
            StartRegion (store, store.CreateNewRegion (m_OpenSimBase, newRegion));

            // backup all our work
            foreach (ISimulationDataStore st in m_simulationDataServices)
                st.ForceBackup ();

        }

        /// <summary>
        /// Saves the region configuration.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        private void SaveRegionConfig(IScene scene, string[] cmd)
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

                if (PathHelpers.VerifySaveFile (regionFile, ".xml", regionsDir) == "")        // filename is not kocher
                  return;

                // verify path details
                if (!Path.IsPathRooted (regionFile))
                    regionFile = Path.Combine (regionsDir, regionFile);
            }

            // let's do it
            if (regionFile == "")
                regionFile = Path.Combine( regionsDir, scene.RegionInfo.RegionName + ".xml");

            MainConsole.Instance.InfoFormat("[SceneManager]: Saving region configuration for {0} to {1} ...", 
                                                scene.RegionInfo.RegionName, regionFile);
            scene.RegionInfo.SaveRegionConfig( regionFile );

         }

        /// <summary>
        ///     Kicks users off the region
        /// </summary>
        /// <param name="cmdparams">name of avatar to kick</param>
        private void KickUserCommand(IScene scene, string[] cmdparams)
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
        private void HandleForceUpdate(IScene scene, string[] args)
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
        private void HandleModulesUnload(IScene scene, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] cmdparams = args.ToArray();

            IRegionModulesController controller =
                m_OpenSimBase.ApplicationRegistry.RequestModuleInterface<IRegionModulesController>();
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
        private void HandleModulesList(IScene scene, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);

            IRegionModulesController controller =
                m_OpenSimBase.ApplicationRegistry.RequestModuleInterface<IRegionModulesController>();
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
        private void RunCommand(IScene scene, string[] cmdparams)
        {
            if (MainConsole.Instance.ConsoleScene == null) 
            {
                MainConsole.Instance.Info ("[SceneManager]: Operating on the 'root' scene will run this command for all regions");
                if (MainConsole.Instance.Prompt ("Are you sure you want to do this? (yes/no)", "no") != "yes")
                    return;
            }

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
                    if (MainConsole.Instance.Prompt("Are you sure you want to reset the region? (yes/no)", "no") !=
                                "yes")
                                return;
                            ResetRegion(scene);
                        }
                    break;
                case "remove":
                    if (cmdparams.Length > 0)
                        if (cmdparams[0] == "region")
                        {
                    if (MainConsole.Instance.Prompt("Are you sure you want to remove the region? (yes/no)", "no") !=
                                "yes")
                                return;
                            RemoveRegion(scene);
                        }
                    break;
                case "command-script":
                    if (cmdparams.Length > 0)
                    {
                        m_OpenSimBase.RunCommandScript(cmdparams[0]);
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
        private void SetDebugPacketLevel(IScene scene, int newDebug)
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
        private void SetDebugPacketName(IScene scene, string name, bool remove)
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

        private void HandleChangeRegion(IScene scene, string[] cmd)
        {
            if (cmd.Length <= 2) 
            {
                MainConsole.Instance.Warn("You need to specify a region name.");
                return;
            }
            string regionName = Util.CombineParams(cmd, 2); // in case of spaces in the name eg Steam Island
            regionName = regionName.ToLower();

            MainConsole.Instance.ConsoleScene = m_scenes.Find((s) => s.RegionInfo.RegionName.ToLower() == regionName);
	
            MainConsole.Instance.InfoFormat("[SceneManager]: Changed to region {0}",
                MainConsole.Instance.ConsoleScene == null ? "root" : MainConsole.Instance.ConsoleScene.RegionInfo.RegionName);

			string rName;
			if (MainConsole.Instance.ConsoleScene == null) {
				if (regionName.ToLower() != "root")
				{
					MainConsole.Instance.Info(String.Format(regionName+" not found? (Case is important)"));
				}
				rName = "root";
			} else {
				rName = MainConsole.Instance.ConsoleScene.RegionInfo.RegionName;
			}
			MainConsole.Instance.DefaultPrompt = "Region ["+rName+"]";

        }

        private void HandleShowUsers(IScene scene, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] showParams = args.ToArray();

            List<IScenePresence> agents = new List<IScenePresence>();
            agents.AddRange(scene.GetScenePresences());
            if (showParams.Length == 1 || showParams[1] != "full")
                agents.RemoveAll(sp => sp.IsChildAgent);

            MainConsole.Instance.Info(String.Format("\nAgents connected: {0}\n", agents.Count));

            MainConsole.Instance.Info(String.Format("{0,-16}{1,-16}{2,-37}{3,-11}{4,-16}{5,-30}", "Firstname",
                                                    "Lastname", "Agent ID", "Root/Child", "Region", "Position"));

            foreach (IScenePresence presence in agents)
            {
                RegionInfo regionInfo = presence.Scene.RegionInfo;

                string regionName = regionInfo == null ? "Unresolvable" : regionInfo.RegionName;

                MainConsole.Instance.Info(String.Format("{0,-16}{1,-37}{2,-11}{3,-16}{4,-30}", presence.Name,
                                                        presence.UUID, presence.IsChildAgent ? "Child" : "Root",
                                                        regionName, presence.AbsolutePosition.ToString()));
            }

            MainConsole.Instance.Info(String.Empty);
            MainConsole.Instance.Info(String.Empty);
        }

        /// <summary>
        /// Display the current scene (region) details.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        private void HandleShowRegions(IScene scene, string[] cmd)
        {
            //  MainConsole.Instance.Info(scene.ToString());

            string sceneInfo;
            var regInfo = scene.RegionInfo;
            UserAccount EstateOwner;
            EstateOwner = scene.UserAccountService.GetUserAccount (null, regInfo.EstateSettings.EstateOwner);

            // todo ... change hardcoded field sizes to public constants
            sceneInfo =  String.Format ("{0, -20}", regInfo.RegionName);
            sceneInfo += String.Format ("{0, -16}", regInfo.RegionLocX / Constants.RegionSize + "," + regInfo.RegionLocY / Constants.RegionSize);
            sceneInfo += String.Format ("{0, -12}", regInfo.RegionSizeX + "x" + regInfo.RegionSizeY);
            sceneInfo += String.Format ("{0, -8}", regInfo.RegionPort);
            sceneInfo += String.Format ("{0, -16}", regInfo.EstateSettings.EstateName);
            sceneInfo += String.Format ("{0, -20}", EstateOwner.Name);

            MainConsole.Instance.Info(sceneInfo);

        }

        private void HandleShowMaturity(IScene scene, string[] cmd)
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

        /// <summary>
        ///     Load a whole region from an opensimulator archive.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void LoadOar(IScene scene, string[] cmdparams)
        {
            if (MainConsole.Instance.ConsoleScene == null) 
            {
                MainConsole.Instance.Info ("[SceneManager]: Operating on the 'root' will load the OAR into all regions");
                if (MainConsole.Instance.Prompt ("Are you sure you want to do this? (yes/no)", "no") != "yes")
                    return;
            }

            // a couple of sanity checks
			if (cmdparams.Count() < 3)
            {
                MainConsole.Instance.Info(
                    "You need to specify a filename to load.");
                return;
            }

			string fileName = cmdparams[2];
			if (fileName.StartsWith("--", StringComparison.CurrentCultureIgnoreCase))
			{
				MainConsole.Instance.Info("[Error] Command format is 'load oar Filename [optional switches]'");
				return;
			}

            string extension = Path.GetExtension (fileName);

            if (extension == string.Empty)
            {
                fileName = fileName + ".oar";
                cmdparams [2] = fileName;
            }

            if (!File.Exists(fileName)) {
                MainConsole.Instance.Info ("OAR archive file '"+fileName+"' not found.");
                return;
            }

            try
            {
                IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
                if (archiver != null)
                    archiver.HandleLoadOarConsoleCommand(cmdparams);
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
        protected void SaveOar(IScene scene, string[] cmdparams)
        {
            if (MainConsole.Instance.ConsoleScene == null) 
            {
                MainConsole.Instance.Info ("[SceneManager]: This command requires a region to be selected\n          Please change to a region first");
                return;
            }

            // a couple of sanity checkes
            if (cmdparams.Count() < 3)
            {
                MainConsole.Instance.Info("You need to specify a filename for the save operation.");
                return;
            }

            string fileName = cmdparams[2];
            string extension = Path.GetExtension (fileName);

            if (extension == string.Empty)
            {
                fileName = fileName + ".oar";
                cmdparams [2] = fileName;
            }

            string fileDir = Path.GetDirectoryName(fileName);
            if (fileDir == "") { fileDir = "./"; }
            if (!Directory.Exists(fileDir))
            {
                MainConsole.Instance.Info ( "[SceneManager]: The folder specified, '" + fileDir + "' does not exist!" );
                return;
            }

            if (File.Exists(fileName)) {
                if (MainConsole.Instance.Prompt ("[SceneManager]: The OAR archive file '"+fileName+"' already exists. Overwrite?", "yes" ) != "yes")
                    return;

                File.Delete (fileName);
            }

            // should be good to go...
            IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
            if (archiver != null)
                archiver.HandleSaveOarConsoleCommand(cmdparams);
        }
            
        #endregion
    }
}