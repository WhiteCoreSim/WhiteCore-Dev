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
using System.IO;

namespace WhiteCore.Framework.Modules
{
    /// <summary>
    ///     Interface to region archive functionality
    /// </summary>
    public interface IRegionArchiverModule
    {
        bool HandleLoadOarConsoleCommand(string[] cmdparams);
        void HandleSaveOarConsoleCommand(string[] cmdparams);

        /// <summary>
        ///     Archive the region to the given path
        /// </summary>
        /// This method occurs asynchronously.  If you want notification of when it has completed then subscribe to
        /// the EventManager.OnOarFileSaved event.
        /// <param name="savePath"></param>
        void ArchiveRegion(string savePath);

        /// <summary>
        ///     Archive the region to the given path
        /// </summary>
        /// This method occurs asynchronously.  If you want notification of when it has completed then subscribe to
        /// the EventManager.OnOarFileSaved event.
        /// <param name="savePath"></param>
        /// <param name="requestId">If supplied, this request Id is later returned in the saved event</param>
        /// <param name="permissions">Permission string, see the 'save oar' help</param>
        void ArchiveRegion(string savePath, Guid requestId, string permissions);

        /// <summary>
        ///     Archive the region to a stream.
        /// </summary>
        /// This method occurs asynchronously.  If you want notification of when it has completed then subscribe to
        /// the EventManager.OnOarFileSaved event.
        /// <param name="saveStream"></param>
        /// <param name="requestId">If supplied, this request Id is later returned in the saved event</param>
        void ArchiveRegion(Stream saveStream, Guid requestId);

        /// <summary>
        ///     De-archive the given region archive.  This replaces the existing scene.
        /// </summary>
        /// If you want notification of when it has completed then subscribe to the EventManager.OnOarFileLoaded event.
        /// <param name="loadPath"></param>
        bool DearchiveRegion(string loadPath);

        /// <summary>
        ///     De-archive the given region archive.  This replaces the existing scene.
        /// </summary>
        /// If you want notification of when it has completed then subscribe to the EventManager.OnOarFileLoaded event.
        /// <param name="loadPath"></param>
        /// <param name="merge">
        ///     If true, the loaded region merges with the existing one rather than replacing it.  Any terrain or region
        ///     settings in the archive will be ignored.
        /// </param>
        /// <param name="skipAssets">
        ///     If true, the archive is loaded without loading any assets contained within it.  This is useful if the
        ///     assets are already known to be present in the grid's asset service.
        /// </param>
        /// <param name="skipTerrain">
        ///     If true, the archive is loaded without loading any terrain contained within it. 
        ///     This is useful if assets need to be transferred to another existing region.
        /// </param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="offsetZ"></param>
        /// <param name="flipX"></param>
        /// <param name="flipY"></param>
        /// <param name="useParcelOwnership"></param>
        /// <param name="checkOwnership"></param>
        bool DearchiveRegion(string loadPath, bool merge, bool skipAssets, bool skipTerrain, 
                             int offsetX, int offsetY, int offsetZ,
                             bool flipX, bool flipY, bool useParcelOwnership, bool checkOwnership);

        /// <summary>
        ///     De-archive a region from a stream.  This replaces the existing scene.
        /// </summary>
        /// If you want notification of when it has completed then subscribe to the EventManager.OnOarFileLoaded event.
        /// <param name="loadStream"></param>
        bool DearchiveRegion(Stream loadStream);

        /// <summary>
        ///     De-archive a region from a stream.  This replaces the existing scene.
        /// </summary>
        /// If you want notification of when it has completed then subscribe to the EventManager.OnOarFileLoaded event.
        /// <param name="loadStream"></param>
        /// <param name="merge">
        ///     If true, the loaded region merges with the existing one rather than replacing it.  Any terrain or region
        ///     settings in the archive will be ignored.
        /// </param>
        /// <param name="skipAssets">
        ///     If true, the archive is loaded without loading any assets contained within it.  This is useful if the
        ///     assets are already known to be present in the grid's asset service.
        /// </param>
        /// <param name="skipTerrain">
        ///     If true, the archive is loaded without loading any terrain contained within it. 
        ///     This is useful if assets need to be transferred to another existing region.
        /// </param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="offsetZ"></param>
        /// <param name="flipX"></param>
        /// <param name="flipY"></param>
        /// <param name="useParcelOwnership"></param>
        /// <param name="checkOwnership"></param>
        bool DearchiveRegion(Stream loadStream, bool merge, bool skipAssets, bool skipTerrain,
                             int offsetX, int offsetY, int offsetZ,
                             bool flipX, bool flipY, bool useParcelOwnership, bool checkOwnership);
    }
}