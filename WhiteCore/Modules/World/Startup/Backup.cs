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
using System.Text;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Startup
{
    public class Backup : ISharedRegionStartupModule
    {
        #region Declares

        protected Dictionary<IScene, InternalSceneBackup> m_backup = new Dictionary<IScene, InternalSceneBackup> ();

        #endregion

        #region ISharedRegionStartupModule Members

        public void Initialise (IScene scene, IConfigSource source, ISimulationBase simBase)
        {
            if (MainConsole.Instance != null && m_backup.Count == 0) //Only add them once
            {
                MainConsole.Instance.Commands.AddCommand (
                    "edit scale",
                    "edit scale <name> <X> <Y> <Z>",
                    "Change the scale of a named prim",
                    EditScale, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "offset region prims",
                    "offset region prims <X> <Y> <Z>",
                    "Offset all prims by the same amount",
                    OffsetPrims, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "backup",
                    "backup",
                    "Persist objects to the database now, if [all], will force the persistence of all prims",
                    RunCommand, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "disable backup",
                    "disable backup",
                    "Disables persistence until re-enabled",
                    DisableBackup, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "enable backup",
                    "enable backup",
                    "Enables persistence after 'disable backup' has been run",
                    EnableBackup, true, false);
            }
            //Set up the backup for the scene
            m_backup [scene] = new InternalSceneBackup (scene);
        }

        public void PostInitialise (IScene scene, IConfigSource source, ISimulationBase simBase)
        {
        }

        public void FinishStartup (IScene scene, IConfigSource source, ISimulationBase simBase)
        {
        }

        public void PostFinishStartup (IScene scene, IConfigSource source, ISimulationBase simBase)
        {
            m_backup [scene].FinishStartup ();
        }

        public void StartupComplete ()
        {
            foreach (IScene scene in m_backup.Keys)
                EnableBackup (scene, null);
        }

        public void Close (IScene scene)
        {
            m_backup.Remove (scene);
        }

        public void DeleteRegion (IScene scene)
        {
        }

        #endregion

        #region Console commands

        /// <summary>
        ///     Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="scene">Currently selected scene</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public void RunCommand (IScene scene, string [] cmdparams)
        {
            scene.WhiteCoreEventManager.FireGenericEventHandler ("Backup", null);
        }

        public void EditScale (IScene scene, string [] cmdparams)
        {
            scene.ForEachSceneEntity (delegate (ISceneEntity entity) {
                foreach (ISceneChildEntity child in entity.ChildrenEntities ()) {
                    if (child.Name == cmdparams [2]) {
                        child.Resize (
                            new Vector3 (Convert.ToSingle (cmdparams [3]),
                                        Convert.ToSingle (cmdparams [4]),
                                        Convert.ToSingle (cmdparams [5])));

                        MainConsole.Instance.InfoFormat ("Edited scale of Primitive: {0}", child.Name);
                    }
                }
            });
        }

        public void OffsetPrims (IScene scene, string [] cmdParams)
        {
            if (cmdParams.Length < 6) {
                MainConsole.Instance.Info ("Not enough parameters");
                return;
            }

            Vector3 offset = new Vector3 (float.Parse (cmdParams [3]), float.Parse (cmdParams [4]), float.Parse (cmdParams [5]));
            scene.ForEachSceneEntity (delegate (ISceneEntity entity) {
                entity.AbsolutePosition += offset;
                entity.ScheduleGroupTerseUpdate ();
            });
            MainConsole.Instance.Info ("Region has been offset");
        }

        public void DisableBackup (IScene scene, string [] cmdparams)
        {
            scene.SimulationDataService.SaveBackups = false;
            MainConsole.Instance.Warn ("Disabled backup");
        }

        public void EnableBackup (IScene scene, string [] cmdparams)
        {
            scene.SimulationDataService.SaveBackups = true;
            if (cmdparams != null) //so that it doesn't show on startup
                MainConsole.Instance.Warn ("Enabled backup");
        }

        #endregion

        #region Per region backup class

        protected class InternalSceneBackup : IBackupModule, IWhiteCoreBackupModule
        {
            #region Declares

            protected IScene m_scene;
            protected bool m_LoadingPrims;

            #endregion

            #region Constructor

            public InternalSceneBackup (IScene scene)
            {
                m_scene = scene;
                m_scene.StackModuleInterface<IWhiteCoreBackupModule> (this);
                m_scene.RegisterModuleInterface<IBackupModule> (this);

                if (MainConsole.Instance != null) {
                    MainConsole.Instance.Commands.AddCommand (
                        "delete object owner",
                        "delete object owner <UUID>",
                        "Delete object by owner",
                        HandleDeleteObject, true, false);

                    MainConsole.Instance.Commands.AddCommand (
                        "delete object creator",
                        "delete object creator <UUID>",
                        "Delete object by creator",
                        HandleDeleteObject, true, false);

                    MainConsole.Instance.Commands.AddCommand (
                        "delete object uuid",
                        "delete object uuid <UUID>",
                        "Delete object by uuid",
                        HandleDeleteObject, true, false);

                    MainConsole.Instance.Commands.AddCommand (
                        "delete object name",
                        "delete object name <name>",
                        "Delete object by name",
                        HandleDeleteObject, true, false);
                }
            }

            #endregion

            #region Console Commands

            void HandleDeleteObject (IScene scene, string [] cmd)
            {
                if (cmd.Length < 4)
                    return;

                string mode = cmd [2];
                string o = cmd [3];

                List<ISceneEntity> deletes = new List<ISceneEntity> ();

                UUID match;

                switch (mode) {
                case "owner":
                    if (!UUID.TryParse (o, out match))
                        return;
                    scene.ForEachSceneEntity (delegate (ISceneEntity g) {
                        if (g.OwnerID == match && !g.IsAttachment)
                            deletes.Add (g);
                    });
                    break;
                case "creator":
                    if (!UUID.TryParse (o, out match))
                        return;
                    scene.ForEachSceneEntity (delegate (ISceneEntity g) {
                        if (g.RootChild.CreatorID == match && !g.IsAttachment)
                            deletes.Add (g);
                    });
                    break;
                case "uuid":
                    if (!UUID.TryParse (o, out match))
                        return;
                    scene.ForEachSceneEntity (delegate (ISceneEntity g) {
                        if (g.UUID == match && !g.IsAttachment)
                            deletes.Add (g);
                    });
                    break;
                case "name":
                    scene.ForEachSceneEntity (delegate (ISceneEntity g) {
                        if (g.RootChild.Name == o && !g.IsAttachment)
                            deletes.Add (g);
                    });
                    break;
                }

                MainConsole.Instance.Warn ("Deleting " + deletes.Count + " objects.");
                DeleteSceneObjects (deletes.ToArray (), true, true);
            }

            #endregion

            #region Scene events

            /// <summary>
            ///     Loads the World's objects
            /// </summary>
            public void LoadPrimsFromStorage ()
            {
                LoadingPrims = true;

                MainConsole.Instance.InfoFormat ("[Backup]: Loading objects for {0} from {1}",
                    m_scene.RegionInfo.RegionName, m_scene.SimulationDataService.Name);
                List<ISceneEntity> PrimsFromDB = m_scene.SimulationDataService.LoadObjects ();
                foreach (ISceneEntity group in PrimsFromDB) {
                    try {
                        if (group == null) {
                            MainConsole.Instance.Warn ("[Backup]: Null object while loading objects, ignoring.");
                            continue;
                        }
                        if (group.RootChild.Shape == null) {
                            MainConsole.Instance.Warn ("[Backup]: Broken object (" + group.Name +
                                                      ") found while loading objects, removing it from the database.");
                            //WTF went wrong here? Remove by passing it by on loading
                            continue;
                        }
                        if (group.IsAttachment || (group.RootChild.Shape.State != 0 &&
                                                   (group.RootChild.Shape.PCode == (byte)PCode.None ||
                                                    group.RootChild.Shape.PCode == (byte)PCode.Prim ||
                                                    group.RootChild.Shape.PCode == (byte)PCode.Avatar))) {
                            MainConsole.Instance.Warn ("[Backup]: Broken state for object " + group.Name +
                                                      " while loading objects, removing it from the database.");
                            //WTF went wrong here? Remove by passing it by on loading
                            continue;
                        }
                        if (group.AbsolutePosition.X > m_scene.RegionInfo.RegionSizeX + 10 ||
                            group.AbsolutePosition.X < -10 ||
                            group.AbsolutePosition.Y > m_scene.RegionInfo.RegionSizeY + 10 ||
                            group.AbsolutePosition.Y < -10) {
                            MainConsole.Instance.WarnFormat ("[Backup]: Object outside the region " +
                                "(" + group.Name + ", " + group.AbsolutePosition + ")" +
                                " found while loading objects, removing it from the database.");
                            //WTF went wrong here? Remove by passing it by on loading
                            continue;
                        }
                        m_scene.SceneGraph.CheckAllocationOfLocalIds (group);
                        group.Scene = m_scene;
                        group.FinishedSerializingGenericProperties ();

                        if (group.RootChild == null) {
                            MainConsole.Instance.ErrorFormat (
                                "[Backup] Found a SceneObjectGroup with m_rootPart == null and {0} children",
                                group.ChildrenEntities ().Count);
                            continue;
                        }
                        m_scene.SceneGraph.RestorePrimToScene (group, false);
                    } catch (Exception ex) {
                        MainConsole.Instance.WarnFormat (
                            "[Backup]: Exception attempting to load object from the database, {0}, continuing...", ex);
                    }
                }
                LoadingPrims = false;
                MainConsole.Instance.Info ("[Backup]: Loaded " + PrimsFromDB.Count + " object(s) in " +
                                          m_scene.RegionInfo.RegionName);
                PrimsFromDB.Clear ();
            }

            /// <summary>
            ///     Loads all Parcel data from the datastore for region identified by regionID
            /// </summary>
            public void LoadAllLandObjectsFromStorage ()
            {
                MainConsole.Instance.Debug ("[Backup]: Loading Land Objects from database... ");
                m_scene.EventManager.TriggerIncomingLandDataFromStorage (
                    m_scene.SimulationDataService.LoadLandObjects (), Vector2.Zero);
            }

            public void FinishStartup ()
            {
                //Load the prims from the database now that we are done loading
                LoadPrimsFromStorage ();
                //Then load the land objects
                LoadAllLandObjectsFromStorage ();
                //Load the prims from the database now that we are done loading
                CreateScriptInstances ();
                //Now destroy the local caches as we're all loaded
                m_scene.SimulationDataService.CacheDispose ();
            }

            /// <summary>
            ///     Start all the scripts in the scene which should be started.
            /// </summary>
            public void CreateScriptInstances ()
            {
                var scriptEngines = m_scene.RequestModuleInterfaces<IScriptModule> ();
                var startingScripts = scriptEngines.Where(module => module != null).Sum(module => module.GetActiveScripts());

                MainConsole.Instance.Info ("[Backup]: Starting scripts in " + m_scene.RegionInfo.RegionName);

                //Set loading prims here to block backup
                LoadingPrims = true;
                ISceneEntity [] entities = m_scene.Entities.GetEntities ();
                foreach (ISceneEntity group in entities) {
                    group.CreateScriptInstances (0, false, StateSource.RegionStart, UUID.Zero, false);
                }

                //Now reset it
                LoadingPrims = false;
                var scriptsLoaded = scriptEngines.Where (module => module != null).Sum (module => module.GetActiveScripts ());
                MainConsole.Instance.InfoFormat ("[Backup]: {0} scripts started", scriptsLoaded - startingScripts);
            }

            #endregion

            #region Public members

            /// <summary>
            ///     Are we currently loading prims?
            /// </summary>
            public bool LoadingPrims {
                get { return m_LoadingPrims; }
                set { m_LoadingPrims = value; }
            }

            /// <summary>
            ///     Delete every object from the scene.  This does not include attachments worn by avatars.
            /// </summary>
            public void DeleteAllSceneObjects ()
            {
                try {
                    LoadingPrims = true;
                    List<ISceneEntity> groups = new List<ISceneEntity> ();
                    lock (m_scene.Entities) {
                        ISceneEntity [] entities = m_scene.Entities.GetEntities ();
                        groups.AddRange (entities.Where (entity => !entity.IsAttachment));
                    }
                    //Delete all the groups now
                    DeleteSceneObjects (groups.ToArray (), true, true);

                    //Now remove the entire region at once
                    m_scene.SimulationDataService.RemoveRegion ();
                    LoadingPrims = false;
                } catch {
                }
            }

            public void ResetRegionToStartupDefault ()
            {
                //Add the loading prims piece just to be safe
                LoadingPrims = true;

                try {
                    lock (m_scene.Entities) {
                        ISceneEntity [] entities = m_scene.Entities.GetEntities ();
                        foreach (ISceneEntity entity in entities) {
                            if (!entity.IsAttachment) {
                                List<ISceneChildEntity> parts = new List<ISceneChildEntity> ();
                                parts.AddRange (entity.ChildrenEntities ());
                                DeleteSceneObject (entity, true, false); //Don't remove from the database

                                m_scene.ForEachScenePresence (
                                    avatar =>
                                    avatar.ControllingClient.SendKillObject (m_scene.RegionInfo.RegionHandle,
                                                                            parts.ToArray ()));
                            }
                        }
                    }
                } catch {
                }

                LoadingPrims = false;
            }

            /// <summary>
            ///     Synchronously delete the objects from the scene.
            ///     This does send kill object updates and resets the parcel prim counts.
            /// </summary>
            /// <param name="groups"></param>
            /// <param name="deleteScripts"></param>
            /// <param name="sendKillPackets"></param>
            /// <returns></returns>
            public bool DeleteSceneObjects (ISceneEntity [] groups, bool deleteScripts, bool sendKillPackets)
            {
                List<ISceneChildEntity> parts = new List<ISceneChildEntity> ();
                foreach (ISceneEntity grp in groups) {
                    if (grp == null)
                        continue;
                    //if (group.IsAttachment)
                    //    continue;
                    parts.AddRange (grp.ChildrenEntities ());
                    DeleteSceneObject (grp, true, true);
                }
                if (sendKillPackets) {
                    m_scene.ForEachScenePresence (avatar => avatar.ControllingClient.SendKillObject (
                         m_scene.RegionInfo.RegionHandle, parts.ToArray ()));
                }

                return true;
            }

            /// <summary>
            ///     Add a backup taint to the prim
            /// </summary>
            /// <param name="sceneObjectGroup"></param>
            public void AddPrimBackupTaint (ISceneEntity sceneObjectGroup)
            {
                //Tell the database that something has changed
                m_scene.SimulationDataService.Tainted ();
            }

            #endregion

            #region Per Object Methods

            /// <summary>
            ///     Synchronously delete the given object from the scene.
            /// </summary>
            /// <param name="group">Object Id</param>
            /// <param name="DeleteScripts">Remove the scripts from the ScriptEngine as well</param>
            /// <param name="removeFromDatabase">Remove from the database?</param>
            protected bool DeleteSceneObject (ISceneEntity group, bool DeleteScripts, bool removeFromDatabase)
            {
                //MainConsole.Instance.DebugFormat("[Backup]: Deleting scene object {0} {1}", group.Name, group.UUID);

                if (group.SitTargetAvatar.Count != 0) {
                    foreach (UUID avID in group.SitTargetAvatar) {
                        //Don't screw up avatar's that are sitting on us!
                        IScenePresence SP = m_scene.GetScenePresence (avID);
                        if (SP != null)
                            SP.StandUp ();
                    }
                }

                // Serialise calls to RemoveScriptInstances to avoid
                // deadlocking on m_parts inside SceneObjectGroup
                if (DeleteScripts) {
                    group.RemoveScriptInstances (true);
                }

                foreach (ISceneChildEntity part in group.ChildrenEntities ()) {
                    IScriptControllerModule m = m_scene.RequestModuleInterface<IScriptControllerModule> ();
                    if (m != null)
                        m.RemoveAllScriptControllers (part);
                }
                if (group.RootChild.PhysActor != null) {
                    //Remove us from the physics sim
                    m_scene.PhysicsScene.RemovePrim (group.RootChild.PhysActor);
                    group.RootChild.PhysActor = null;
                }

                if (!group.IsAttachment)
                    m_scene.SimulationDataService.Tainted ();
                if (m_scene.SceneGraph.DeleteEntity (group)) {
                    // We need to keep track of this state in case this group is still queued for backup.
                    group.IsDeleted = true;
                    m_scene.EventManager.TriggerObjectBeingRemovedFromScene (group);
                    return true;
                }

                //MainConsole.Instance.DebugFormat("[Scene]: Exit DeleteSceneObject() for {0} {1}", group.Name, group.UUID);
                return false;
            }

            #endregion

            #region IWhiteCoreBackupModule Methods

            bool m_isArchiving = false;
            readonly List<UUID> m_missingAssets = new List<UUID> ();
            readonly List<LandData> m_parcels = new List<LandData> ();
            bool m_merge = false;
            bool m_loadAssets = false;
            readonly GenericAccountCache<UserAccount> m_cache = new GenericAccountCache<UserAccount> ();
            List<ISceneEntity> m_groups = new List<ISceneEntity> ();

            public bool IsArchiving {
                get { return m_isArchiving; }
            }

            public void SaveModuleToArchive (TarArchiveWriter writer, IScene scene)
            {
                m_isArchiving = true;

                MainConsole.Instance.Info ("[Archive]: Writing parcels to archive");

                writer.WriteDir ("parcels");

                IParcelManagementModule module = scene.RequestModuleInterface<IParcelManagementModule> ();
                if (module != null) {
                    List<ILandObject> landObject = module.AllParcels ();
                    foreach (ILandObject parcel in landObject) {
                        OSDMap parcelMap = parcel.LandData.ToOSD ();
                        writer.WriteFile ("parcels/" + parcel.LandData.GlobalID,
                                         OSDParser.SerializeLLSDBinary (parcelMap));
                    }
                }

                MainConsole.Instance.Info ("[Archive]: Finished writing parcels to archive");
                MainConsole.Instance.Info ("[Archive]: Writing terrain to archive");

                writer.WriteDir ("newstyleterrain");
                writer.WriteDir ("newstylerevertterrain");

                writer.WriteDir ("newstylewater");
                writer.WriteDir ("newstylerevertwater");

                ITerrainModule tModule = scene.RequestModuleInterface<ITerrainModule> ();
                if (tModule != null) {
                    try {
                        byte [] sdata = WriteTerrainToStream (tModule.TerrainMap);
                        writer.WriteFile ("newstyleterrain/" + scene.RegionInfo.RegionID + ".terrain", sdata);

                        sdata = WriteTerrainToStream (tModule.TerrainRevertMap);
                        writer.WriteFile ("newstylerevertterrain/" + scene.RegionInfo.RegionID + ".terrain",
                                         sdata);
                        sdata = null;

                        if (tModule.TerrainWaterMap != null) {
                            sdata = WriteTerrainToStream (tModule.TerrainWaterMap);
                            writer.WriteFile ("newstylewater/" + scene.RegionInfo.RegionID + ".terrain", sdata);

                            sdata = WriteTerrainToStream (tModule.TerrainWaterRevertMap);
                            writer.WriteFile (
                                "newstylerevertwater/" + scene.RegionInfo.RegionID + ".terrain", sdata);
                            sdata = null;
                        }
                    } catch (Exception ex) {
                        MainConsole.Instance.WarnFormat ("[Backup]: Exception caught: {0}", ex);
                    }
                }

                MainConsole.Instance.Info ("[Archive]: Finished writing terrain to archive");
                MainConsole.Instance.Info ("[Archive]: Writing entities to archive");
                ISceneEntity [] entities = scene.Entities.GetEntities ();
                //Get all entities, then start writing them to the database
                writer.WriteDir ("entities");

                IDictionary<UUID, AssetType> assets = new Dictionary<UUID, AssetType> ();
                UuidGatherer assetGatherer = new UuidGatherer (m_scene.AssetService);
                IWhiteCoreBackupArchiver archiver = m_scene.RequestModuleInterface<IWhiteCoreBackupArchiver> ();
                bool saveAssets = false;
                if (archiver.AllowPrompting)
                    saveAssets =
                        MainConsole.Instance.Prompt ("Save assets? (Will not be able to load on other grids if not saved)", "false")
                                   .Equals ("true", StringComparison.CurrentCultureIgnoreCase);

                int count = 0;
                foreach (ISceneEntity entity in entities) {
                    try {
                        if (entity.IsAttachment ||
                            ((entity.RootChild.Flags & PrimFlags.Temporary) == PrimFlags.Temporary)
                            || ((entity.RootChild.Flags & PrimFlags.TemporaryOnRez) == PrimFlags.TemporaryOnRez))
                            continue;

                        //Write all entities
                        byte [] xml = entity.ToBinaryXml2 ();
                        writer.WriteFile ("entities/" + entity.UUID, xml);
                        xml = null;
                        count++;
                        if (count % 3 == 0)
                            Thread.Sleep (5);

                        //Get all the assets too
                        if (saveAssets)
                            assetGatherer.GatherAssetUuids (entity, assets);
                    } catch (Exception ex) {
                        MainConsole.Instance.WarnFormat ("[Backup]: Exception caught: {0}", ex);
                    }
                }
                entities = null;

                MainConsole.Instance.Info ("[Archive]: Finished writing entities to archive");
                MainConsole.Instance.Info ("[Archive]: Writing assets for entities to archive");

                bool foundAllAssets = true;
                foreach (UUID assetID in new List<UUID> (assets.Keys)) {
                    try {
                        foundAllAssets = false; //Not all are cached
                        m_scene.AssetService.Get (assetID.ToString (), writer, RetrievedAsset);
                        m_missingAssets.Add (assetID);
                    } catch (Exception ex) {
                        MainConsole.Instance.WarnFormat ("[Backup]: Exception caught: {0}", ex);
                    }
                }
                if (foundAllAssets)
                    m_isArchiving = false; //We're done if all the assets were found

                MainConsole.Instance.Info ("[Archive]: Finished writing assets for entities to archive");
            }

            static byte [] WriteTerrainToStream (ITerrainChannel tModule)
            {
                int tMapSize = tModule.Width * tModule.Height;
                byte [] sdata = new byte [tMapSize * 2];
                Buffer.BlockCopy (tModule.GetSerialised (), 0, sdata, 0, sdata.Length);

                return sdata;
            }

            void RetrievedAsset (string id, object sender, AssetBase asset)
            {
                TarArchiveWriter writer = (TarArchiveWriter)sender;
                //Add the asset
                WriteAsset (id, asset, writer);
                m_missingAssets.Remove (UUID.Parse (id));
                if (m_missingAssets.Count == 0)
                    m_isArchiving = false;
            }

            void WriteAsset (string id, AssetBase asset, TarArchiveWriter writer)
            {
                if (asset != null)
                    writer.WriteFile ("assets/" + asset.ID, OSDParser.SerializeJsonString (asset.ToOSD ()));
                else
                    MainConsole.Instance.WarnFormat ("Could not find asset {0}", id);
            }

            public void BeginLoadModuleFromArchive (IScene scene)
            {
                IBackupModule backup = scene.RequestModuleInterface<IBackupModule> ();
                IScriptModule [] modules = scene.RequestModuleInterfaces<IScriptModule> ();
                IParcelManagementModule parcelModule = scene.RequestModuleInterface<IParcelManagementModule> ();
                //Disable the script engine so that it doesn't load in the background and kill OAR loading

                foreach (IScriptModule module in modules) {
                    if (module != null)
                        module.Disabled = true;
                }

                //Disable backup for now as well
                if (backup != null) {
                    backup.LoadingPrims = true;
                    m_loadAssets =
                        MainConsole.Instance.Prompt (
                            "Should any stored assets be loaded? (If you got this .abackup from another grid, choose yes",
                            "no").ToLower () == "yes";
                    m_merge =
                        MainConsole.Instance.Prompt (
                            "Should we merge prims together (keep the prims from the old region too)?", "no").ToLower () ==
                        "yes";
                    if (!m_merge) {
                        DateTime before = DateTime.Now;
                        MainConsole.Instance.Info ("[Archiver]: Clearing all existing scene objects");
                        backup.DeleteAllSceneObjects ();
                        MainConsole.Instance.Info ("[Archiver]: Cleared all existing scene objects in " +
                                                  (DateTime.Now - before).Minutes + ":" +
                                                  (DateTime.Now - before).Seconds);
                        if (parcelModule != null)
                            parcelModule.ClearAllParcels ();
                    }
                }
            }

            public void EndLoadModuleFromArchive (IScene scene)
            {
                IBackupModule backup = scene.RequestModuleInterface<IBackupModule> ();
                IScriptModule [] modules = scene.RequestModuleInterfaces<IScriptModule> ();

                //Reeanble now that we are done
                foreach (IScriptModule module in modules) {
                    module.Disabled = false;
                }

                //Reset backup too
                if (backup != null)
                    backup.LoadingPrims = false;

                //Update the database as well!
                IParcelManagementModule parcelManagementModule = scene.RequestModuleInterface<IParcelManagementModule> ();
                if (parcelManagementModule != null && !m_merge) //Only if we are not merging
                {
                    if (m_parcels.Count > 0) {
                        scene.EventManager.TriggerIncomingLandDataFromStorage (m_parcels, Vector2.Zero);
                        //Update the database as well!
                        foreach (LandData parcel in m_parcels) {
                            parcelManagementModule.UpdateLandObject (parcelManagementModule.GetLandObject (parcel.LocalID));
                        }
                    } else
                        parcelManagementModule.ResetSimLandObjects ();

                    m_parcels.Clear ();
                }

                foreach (ISceneEntity sceneObject in m_groups) {
                    foreach (ISceneChildEntity part in sceneObject.ChildrenEntities ()) {
                        if (!ResolveUserUuid (part.CreatorID))
                            part.CreatorID = m_scene.RegionInfo.EstateSettings.EstateOwner;

                        if (!ResolveUserUuid (part.OwnerID))
                            part.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;

                        if (!ResolveUserUuid (part.LastOwnerID))
                            part.LastOwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;

                        // Fix ownership/creator of inventory items
                        // Not doing so results in inventory items
                        // being no copy/no mod for everyone
                        lock (part.TaskInventory) {
                            TaskInventoryDictionary inv = part.TaskInventory;
                            foreach (KeyValuePair<UUID, TaskInventoryItem> kvp in inv) {
                                if (!ResolveUserUuid (kvp.Value.OwnerID)) {
                                    kvp.Value.OwnerID = scene.RegionInfo.EstateSettings.EstateOwner;
                                }
                                if (!ResolveUserUuid (kvp.Value.CreatorID)) {
                                    kvp.Value.CreatorID = scene.RegionInfo.EstateSettings.EstateOwner;
                                }
                            }
                        }
                    }

                    if (scene.SceneGraph.AddPrimToScene (sceneObject)) {
                        sceneObject.HasGroupChanged = true;
                        sceneObject.ScheduleGroupUpdate (PrimUpdateFlags.ForcedFullUpdate);
                        sceneObject.CreateScriptInstances (0, false, StateSource.RegionStart, UUID.Zero, false);
                    }
                }
            }

            public void LoadModuleFromArchive (byte [] data, string filePath, TarArchiveReader.TarEntryType type,
                                              IScene scene)
            {
                if (filePath.StartsWith ("parcels/", StringComparison.Ordinal)) {
                    if (!m_merge) {
                        //Only use if we are not merging
                        LandData parcel = new LandData ();
                        OSD parcelData = OSDParser.DeserializeLLSDBinary (data);
                        parcel.FromOSD ((OSDMap)parcelData);
                        m_parcels.Add (parcel);
                    }
                }
                #region New Style Terrain Loading

                else if (filePath.StartsWith ("newstyleterrain/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();
                    terrainModule.TerrainMap = ReadTerrain (data, scene);
                } else if (filePath.StartsWith ("newstylerevertterrain/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();
                    terrainModule.TerrainRevertMap = ReadTerrain (data, scene);
                } else if (filePath.StartsWith ("newstylewater/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();
                    terrainModule.TerrainWaterMap = ReadTerrain (data, scene);
                } else if (filePath.StartsWith ("newstylerevertwater/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();
                    terrainModule.TerrainWaterRevertMap = ReadTerrain (data, scene);
                }
                #endregion
                #region Old Style Terrain Loading

                  else if (filePath.StartsWith ("terrain/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();

                    MemoryStream ms = new MemoryStream (data);
                    terrainModule.LoadFromStream (filePath, ms, 0, 0);
                    ms.Close ();
                } else if (filePath.StartsWith ("revertterrain/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();

                    MemoryStream ms = new MemoryStream (data);
                    terrainModule.LoadRevertMapFromStream (filePath, ms, 0, 0);
                    ms.Close ();
                } else if (filePath.StartsWith ("water/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();

                    MemoryStream ms = new MemoryStream (data);
                    terrainModule.LoadWaterFromStream (filePath, ms, 0, 0);
                    ms.Close ();
                } else if (filePath.StartsWith ("revertwater/", StringComparison.Ordinal)) {
                    ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule> ();

                    MemoryStream ms = new MemoryStream (data);
                    terrainModule.LoadWaterRevertMapFromStream (filePath, ms, 0, 0);
                    ms.Close ();
                }
                #endregion

                  else if (filePath.StartsWith ("entities/", StringComparison.Ordinal)) {
                    MemoryStream ms = new MemoryStream (data);
                    ISceneEntity sceneObject = SceneEntitySerializer.SceneObjectSerializer.FromXml2Format (ref ms, scene);
                    ms.Close ();
                    m_groups.Add (sceneObject);
                } else if (filePath.StartsWith ("assets/", StringComparison.Ordinal)) {
                    if (m_loadAssets) {
                        AssetBase asset = new AssetBase ();
                        asset.Unpack (OSDParser.DeserializeJson (Encoding.UTF8.GetString (data)));
                        scene.AssetService.Store (asset);
                    }
                }
            }

            ITerrainChannel ReadTerrain (byte [] data, IScene scene)
            {
                short [] sdata = new short [data.Length / 2];
                Buffer.BlockCopy (data, 0, sdata, 0, data.Length);
                return new TerrainChannel (sdata, scene);
            }

            bool ResolveUserUuid (UUID uuid)
            {
                UserAccount acc;
                if (m_cache.Get (uuid, out acc))
                    return acc != null;

                acc = m_scene.UserAccountService.GetUserAccount (m_scene.RegionInfo.AllScopeIDs, uuid);
                m_cache.Cache (uuid, acc);

                return acc != null;
            }

            #endregion
        }

        #endregion
    }
}
