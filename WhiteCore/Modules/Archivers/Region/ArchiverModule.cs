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
using System.Linq;
using Nini.Config;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Archivers
{
    /// <summary>
    ///     This module loads and saves WhiteCore-Sim region archives
    /// </summary>
    public class ArchiverModule : INonSharedRegionModule, IRegionArchiverModule
    {
        /// <value>
        ///     The file used to load and save an opensimulator archive if no filename has been specified
        /// </value>
        protected const string DEFAULT_OAR_BACKUP_FILENAME = Constants.DEFAULT_OAR_BACKUP_FILENAME;

        private IScene m_scene;

        #region INonSharedRegionModule Members

        public string Name
        {
            get { return "RegionArchiverModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            //MainConsole.Instance.Debug("[Archiver] Initializing");
        }

        public void AddRegion(IScene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IRegionArchiverModule>(this);
            //MainConsole.Instance.DebugFormat("[Archiver]: Enabled for region {0}", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(IScene scene)
        {
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void Close()
        {
        }

        #endregion

        #region IRegionArchiverModule Members

        /// <summary>
        ///     Load a whole region from an opensimulator archive.
        /// </summary>
        /// <param name="cmdparams"></param>
        public bool HandleLoadOarConsoleCommand(string[] cmdparams)
        {
            bool mergeOar = false;
            bool skipAssets = false;
            bool skipTerrain = false;
            int offsetX = 0;
            int offsetY = 0;
            int offsetZ = 0;
            bool flipX = false;
            bool flipY = false;
            bool useParcelOwnership = false;
            bool checkOwnership = false;

            int i = 0;
            List<string> newParams = new List<string>(cmdparams);
            foreach (string param in cmdparams)
            {
                if (param.StartsWith("--skip-assets", StringComparison.CurrentCultureIgnoreCase))
                {
                    skipAssets = true;
                    newParams.Remove(param);
                }
                if (param.StartsWith("--skip-terrain", StringComparison.CurrentCultureIgnoreCase))
                {
                    skipTerrain = true;
                    newParams.Remove(param);
                }
                if (param.StartsWith("--merge", StringComparison.CurrentCultureIgnoreCase))
                {
                    mergeOar = true;
                    newParams.Remove(param);
                }
                if (param.StartsWith("--OffsetX", StringComparison.CurrentCultureIgnoreCase))
                {
                    string retVal = param.Remove(0, 10);
                    int.TryParse(retVal, out offsetX);
                    newParams.Remove(param);
                }
                if (param.StartsWith("--OffsetY", StringComparison.CurrentCultureIgnoreCase))
                {
                    string retVal = param.Remove(0, 10);
                    int.TryParse(retVal, out offsetY);
                    newParams.Remove(param);
                }
                if (param.StartsWith("--OffsetZ", StringComparison.CurrentCultureIgnoreCase))
                {
                    string retVal = param.Remove(0, 10);
                    int.TryParse(retVal, out offsetZ);
                    newParams.Remove(param);
                }
                if (param.StartsWith("--FlipX", StringComparison.CurrentCultureIgnoreCase))
                {
                    flipX = true;
                    newParams.Remove(param);
                }
                if (param.StartsWith("--FlipY", StringComparison.CurrentCultureIgnoreCase))
                {
                    flipY = true;
                    newParams.Remove(param);
                }
                if (param.StartsWith("--UseParcelOwnership", StringComparison.CurrentCultureIgnoreCase))
                {
                    useParcelOwnership = true;
                    newParams.Remove(param);
                }
                if (param.StartsWith("--CheckOwnership", StringComparison.CurrentCultureIgnoreCase))
                {
                    checkOwnership = true;
                    newParams.Remove(param);
                }
                i++;
            }

            return DearchiveRegion(newParams.Count > 2 ? newParams[2] : DEFAULT_OAR_BACKUP_FILENAME, mergeOar, skipAssets,
                            skipTerrain, offsetX, offsetY, offsetZ, flipX, flipY,
                            useParcelOwnership, checkOwnership);
        }

        /// <summary>
        ///     Save a region to a file, including all the assets needed to restore it.
        /// </summary>
        /// <param name="cmdparams"></param>
        public void HandleSaveOarConsoleCommand(string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                string permissions = null;
                List<string> newParams = new List<string>(cmdparams);

                foreach (
                    string param in
                        cmdparams.Where(param => param.StartsWith("--perm=", StringComparison.CurrentCultureIgnoreCase))
                    )
                {
                    permissions = param.Remove(0, 7);
                    newParams.Remove(param);
                }
                ArchiveRegion(newParams[2], Guid.Empty, permissions);
            }
            else
            {
                ArchiveRegion(DEFAULT_OAR_BACKUP_FILENAME);
            }
        }

        public void ArchiveRegion(string savePath)
        {
            ArchiveRegion(savePath, Guid.Empty, null);
        }

        public void ArchiveRegion(string savePath, Guid requestId, string permissions)
        {
            MainConsole.Instance.InfoFormat(
                "[Archiver]: Writing archive for region {0} to {1}", m_scene.RegionInfo.RegionName, savePath);

            new ArchiveWriteRequestPreparation(m_scene, savePath, requestId, permissions).ArchiveRegion();
        }

        public void ArchiveRegion(Stream saveStream, Guid requestId)
        {
            new ArchiveWriteRequestPreparation(m_scene, saveStream, requestId).ArchiveRegion();
        }

        public bool DearchiveRegion(string loadPath)
        {
             return DearchiveRegion(loadPath, false, false, false, 0, 0, 0, false, false, false, false);
        }

        public bool DearchiveRegion(string loadPath, bool merge, bool skipAssets, bool skipTerrain,
                                    int offsetX, int offsetY, int offsetZ, bool flipX, bool flipY,
                                    bool useParcelOwnership, bool checkOwnership)
        {
            MainConsole.Instance.InfoFormat(
                "[Archiver]: Loading archive to region {0} from {1}", m_scene.RegionInfo.RegionName, loadPath);

            var archiveRead = new ArchiveReadRequest (m_scene, loadPath, merge, skipAssets, skipTerrain, offsetX,
                                  offsetY, offsetZ, flipX, flipY, useParcelOwnership, checkOwnership);
            return archiveRead.DearchiveRegion();
        }

        public bool DearchiveRegion(Stream loadStream)
        {
            return DearchiveRegion(loadStream, false, false, false, 0, 0, 0, false, false, false, false);
        }

        public bool DearchiveRegion(Stream loadStream, bool merge, bool skipAssets, bool skipTerrain,
                                    int offsetX, int offsetY, int offsetZ,
                                    bool flipX, bool flipY, bool useParcelOwnership, bool checkOwnership)
        {
            var archiveRead = new ArchiveReadRequest (m_scene, loadStream, merge, skipAssets, skipTerrain, offsetX, offsetY, offsetZ, flipX, flipY,
                                  useParcelOwnership, checkOwnership);
            return archiveRead.DearchiveRegion();
        }

        #endregion

        public void ArchiveRegion(Stream saveStream)
        {
            ArchiveRegion(saveStream, Guid.Empty);
        }
    }
}