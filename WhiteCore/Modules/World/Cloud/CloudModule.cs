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
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using Nini.Config;

namespace WhiteCore.Modules.Cloud
{
    public class CloudModule : ICloudModule
    {
        float [] cloudCover;
        int gridX;
        int gridY;
        readonly Random m_rndnums = new Random (Environment.TickCount);
        float m_cloudDensity = 1.5F;
        bool m_enabled;
        uint m_frame;
        int m_frameUpdateRate = 1000;
        bool m_ready;
        IScene m_scene;

        #region ICloudModule Members

        public void Initialise (IConfigSource config)
        {
            IConfig cloudConfig = config.Configs ["Cloud"];

            if (cloudConfig != null) {
                m_enabled = cloudConfig.GetBoolean ("enabled", false);
                m_cloudDensity = cloudConfig.GetFloat ("density", 0.5F);
                m_frameUpdateRate = cloudConfig.GetInt ("cloud_update_rate", 1000);
            }
        }

        public void AddRegion (IScene scene)
        {
            if (m_enabled) {
                m_scene = scene;

                gridX = (m_scene.RegionInfo.RegionSizeX / 16);
                gridY = (m_scene.RegionInfo.RegionSizeY / 16);
                cloudCover = new float [gridX * gridY];

                scene.EventManager.OnNewClient += CloudsToClient;
                scene.RegisterModuleInterface<ICloudModule> (this);
                scene.EventManager.OnFrame += CloudUpdate;

                GenerateCloudCover ();

                m_ready = true;
            }
        }

        public void RemoveRegion (IScene scene)
        {
            if (m_enabled) {
                m_ready = false;
                //  Remove our hooks
                m_scene.EventManager.OnNewClient -= CloudsToClient;
                m_scene.EventManager.OnFrame -= CloudUpdate;
            }
        }

        public void RegionLoaded (IScene scene)
        {
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public void Close ()
        {
        }

        public string Name {
            get { return "CloudModule"; }
        }

        public void SetCloudDensity (float density)
        {
            m_cloudDensity = density;
            m_scene.ForEachClient (CloudsToClient);
        }

        public float CloudCover (int x, int y, int z)
        {
            float cover = 0f;
            x /= (gridX);
            y /= (gridY);

            // check limits
            if (x < 0) x = 0;
            if (x > (gridX - 1)) x = gridX - 1;
            if (y < 0) y = 0;
            if (y > (gridY - 1)) y = gridY - 1;

            if (cloudCover != null) {
                cover = cloudCover [y * 16 + x];
            }

            return cover;
        }

        #endregion

        void UpdateCloudCover ()
        {
            float [] newCover = new float [gridX * gridY];
            int rowAbove = new int ();
            int rowBelow = new int ();
            int columnLeft = new int ();
            int columnRight = new int ();

            for (int x = 0; x < gridX; x++) {
                if (x == 0) {
                    columnRight = x + 1;
                    columnLeft = gridX - 1;
                } else if (x == gridX - 1) {
                    columnRight = 0;
                    columnLeft = x - 1;
                } else {
                    columnRight = x + 1;
                    columnLeft = x - 1;
                }
                for (int y = 0; y < gridY; y++) {
                    if (y == 0) {
                        rowAbove = y + 1;
                        rowBelow = gridY - 1;
                    } else if (y == gridY - 1) {
                        rowAbove = 0;
                        rowBelow = y - 1;
                    } else {
                        rowAbove = y + 1;
                        rowBelow = y - 1;
                    }
                    float neighborAverage = (cloudCover [rowBelow * 16 + columnLeft] +
                                             cloudCover [y * 16 + columnLeft] +
                                             cloudCover [rowAbove * 16 + columnLeft] +
                                             cloudCover [rowBelow * 16 + x] +
                                             cloudCover [rowAbove * 16 + x] +
                                             cloudCover [rowBelow * 16 + columnRight] +
                                             cloudCover [y * 16 + columnRight] +
                                             cloudCover [rowAbove * 16 + columnRight] +
                                             cloudCover [y * 16 + x]) / 9;
                    newCover [y * 16 + x] = ((neighborAverage / m_cloudDensity) + 0.175f) % 1.0f;
                    newCover [y * 16 + x] *= m_cloudDensity;
                }
            }
            Array.Copy (newCover, cloudCover, gridX * gridY);
        }

        void CloudUpdate ()
        {
            if (((m_frame++ % m_frameUpdateRate) != 0) || !m_ready || (m_cloudDensity <= 0.01)) {
                return;
            }
            UpdateCloudCover ();
        }

        public void CloudsToClient (IClientAPI client)
        {
            if (m_ready) {
                client.SendCloudData (cloudCover);
            }
        }


        /// <summary>
        ///     Calculate the cloud cover over the region.
        /// </summary>
        void GenerateCloudCover ()
        {
            for (int y = 0; y < gridY; y++) {
                for (int x = 0; x < gridX; x++) {
                    cloudCover [y * 16 + x] = (float)(m_rndnums.NextDouble ());        // 0 to 1
                    cloudCover [y * 16 + x] *= m_cloudDensity;                         //  normalize range 0: none < 1: rain > 2: snow
                }
            }
        }
    }
}
