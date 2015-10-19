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
using Nini.Config;

namespace WhiteCore.Framework.SceneInfo
{

    #region Delegates

    public delegate void NewScene(IScene scene);

    public delegate void NoParam();

    #endregion

    public interface ISceneManager
    {
        /// <summary>
        ///     Starts the region
        /// </summary>
        /// <param name="newRegion"></param>
        void StartRegions(out bool newRegion);

        /// <summary>
        ///     Shuts down the given region
        /// </summary>
        /// <param name="shutdownType"></param>
        /// <param name="p"></param>
        void CloseRegion(IScene scene, ShutdownType shutdownType, int delaySecs, bool killAgents);

        /// <summary>
        ///     Removes and resets terrain and objects from the database
        /// </summary>
        void ResetRegion(IScene scene);

        /// <summary>
        ///     Restart the given region
        /// </summary>
        void RestartRegion(IScene scene, bool killAgents);

        /// <summary>
        /// Creates and adds a region from supplied info.
        /// </summary>
        /// <param name="regionInfo">Region info.</param>
        bool CreateRegion (RegionInfo regionInfo);

        /// <summary>
        /// Finds the current region info.
        /// </summary>
        /// <returns>The current region info.</returns>
        Dictionary<string, int> FindCurrentRegionInfo();

        void HandleStartupComplete(List<string> data);

        IConfigSource ConfigSource { get; }

        List<IScene> Scenes { get; }

        event NewScene OnCloseScene;
        event NewScene OnAddedScene;
        event NewScene OnFinishedAddingScene;
    }
}