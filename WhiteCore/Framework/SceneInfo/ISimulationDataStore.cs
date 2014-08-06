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

using System.Collections.Generic;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo.Entities;

namespace WhiteCore.Framework.SceneInfo
{
    public interface ISimulationDataStore
    {
        /// <summary>
        ///     The name of the plugin
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     A new map tile needs generated
        /// </summary>
        bool MapTileNeedsGenerated { get; set; }

        /// <summary>
        ///     Whether we should save backups currently or not
        /// </summary>
        bool SaveBackups { get; set; }

        /// <summary>
        /// Gets or sets the backup filename.
        /// </summary>
        /// <value>The backup file.</value>
        string BackupFile { get; set; }

        /// <summary>
        ///     Initialises the data storage engine
        /// </summary>
        void Initialise();

        /// <summary>
        ///     Entirely removes the region, this includes everything about the region
        /// </summary>
        /// <returns></returns>
        void RemoveRegion();

        /// <summary>
        ///     Something has changed in the region, just alerting us to the change if we need to do anything
        /// </summary>
        void Tainted();

        /// <summary>
        ///     Load persisted objects from region storage.
        /// </summary>
        /// <returns>List of loaded groups</returns>
        List<ISceneEntity> LoadObjects();

        /// <summary>
        ///     Load the latest terrain revision from region storage
        /// </summary>
        /// <param name="RevertMap"></param>
        /// <param name="RegionSizeX"></param>
        /// <param name="RegionSizeY"></param>
        /// <returns>Heightfield data</returns>
        void LoadTerrain(bool RevertMap, int RegionSizeX, int RegionSizeY);

        /// <summary>
        ///     Load the latest water revision from region storage
        /// </summary>
        /// <param name="RevertMap"></param>
        /// <param name="RegionSizeX"></param>
        /// <param name="RegionSizeY"></param>
        /// <returns>Heightfield data</returns>
        void LoadWater(bool RevertMap, int RegionSizeX, int RegionSizeY);

        /// <summary>
        ///     Load all parcels from the database
        /// </summary>
        /// <returns></returns>
        List<LandData> LoadLandObjects();

        /// <summary>
        ///     Shutdown and exit the module
        /// </summary>
        void Shutdown();

        /// <summary>
        ///     Clears out all references of the backup stream and dumps local caches
        /// </summary>
        void CacheDispose();

        /// <summary>
        /// Finds all available regions to load
        /// </summary>
        /// <param name="newRegion"></param>
        /// <returns></returns>
		List<string> FindRegionInfos(out bool newRegion, ISimulationBase simBase);

        /// <summary>
        /// Creates a new region
        /// </summary>
        /// <returns></returns>
        RegionInfo CreateNewRegion(ISimulationBase simBase, Dictionary<string, int> currentInfo);

        /// <summary>
        /// Creates a new region with the passed name
        /// </summary>
        /// <returns></returns>
        RegionInfo CreateNewRegion(ISimulationBase simBase, string regionName, Dictionary<string, int> currentInfo);

        /// <summary>
        /// Creates a new region using the passed regInfo object
        /// </summary>
        /// <returns></returns>
        RegionInfo CreateNewRegion(ISimulationBase simBase, RegionInfo regionInfo, Dictionary<string, int> currentInfo);

        /// <summary>
        ///     Load all region infos available
        /// </summary>
        /// <param name="simBase"></param>
        /// <param name="newRegion"></param>
        /// <returns></returns>
        RegionInfo LoadRegionInfo(string fileName, ISimulationBase simBase);

        /// <summary>
        ///     Load the region info for a specified region
        /// </summary>
        /// <param name="simBase"></param>
        /// <param name="newRegion"></param>
        /// <returns></returns>
        RegionInfo LoadRegionNameInfo(string regionName, ISimulationBase simBase);

        /// <summary>
        ///     Set the region ref
        /// </summary>
        /// <param name="scene"></param>
        void SetRegion(IScene scene);

        /// <summary>
        ///     Forces the datastore to backup the region
        /// </summary>
        void ForceBackup();

        /// <summary>
        /// Gets the  filename of the last region backup file.
        /// </summary>
        /// <returns>The last backup file name.</returns>
        /// <param name="regionName">Region name.</param>
        string GetLastBackupFileName (string regionName);

        /// <summary>
        /// Restores the last backup.
        /// </summary>
        /// <returns><c>true</c>, if last backup was restored, <c>false</c> otherwise.</returns>
        /// <param name="regionName">Region name.</param>
        bool RestoreLastBackup (string regionName);

        /// <summary>
        /// Restores a backup file to a region.
        /// </summary>
        /// <returns><c>true</c>, if backup file was restored, <c>false</c> otherwise.</returns>
        /// <param name="fileName">File name.</param>
        /// <param name="regionName">Region name.</param>
        bool RestoreBackupFile (string fileName, string regionName);

        /// <summary>
        /// Copies the instance
        /// </summary>
        /// <returns></returns>
        ISimulationDataStore Copy();
    }
}