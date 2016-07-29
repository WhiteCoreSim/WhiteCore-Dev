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
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;


namespace WhiteCore.Modules.Terrain.PaintBrushes
{
    /// <summary>
    ///     Hydraulic Erosion Brush
    /// </summary>
    public class ErodeSphere : ITerrainPaintableEffect
    {
        const float rainHeight = 0.2f;
        const int rounds = 10;
        const NeighbourSystem type = NeighbourSystem.Moore;
        const float waterSaturation = 0.30f;

        #region Supporting Functions

        static int [] Neighbours (NeighbourSystem neighbourType, int index)
        {
            int [] coord = new int [2];

            index++;

            switch (neighbourType) {
            case NeighbourSystem.Moore:
                switch (index) {
                case 1:
                    coord [0] = -1;
                    coord [1] = -1;
                    break;

                case 2:
                    coord [0] = -0;
                    coord [1] = -1;
                    break;

                case 3:
                    coord [0] = +1;
                    coord [1] = -1;
                    break;

                case 4:
                    coord [0] = -1;
                    coord [1] = -0;
                    break;

                case 5:
                    coord [0] = -0;
                    coord [1] = -0;
                    break;

                case 6:
                    coord [0] = +1;
                    coord [1] = -0;
                    break;

                case 7:
                    coord [0] = -1;
                    coord [1] = +1;
                    break;

                case 8:
                    coord [0] = -0;
                    coord [1] = +1;
                    break;

                case 9:
                    coord [0] = +1;
                    coord [1] = +1;
                    break;

                default:
                    break;
                }
                break;

            case NeighbourSystem.VonNeumann:
                switch (index) {
                case 1:
                    coord [0] = 0;
                    coord [1] = -1;
                    break;

                case 2:
                    coord [0] = -1;
                    coord [1] = 0;
                    break;

                case 3:
                    coord [0] = +1;
                    coord [1] = 0;
                    break;

                case 4:
                    coord [0] = 0;
                    coord [1] = +1;
                    break;

                case 5:
                    coord [0] = -0;
                    coord [1] = -0;
                    break;

                default:
                    break;
                }
                break;
            }

            return coord;
        }

        enum NeighbourSystem
        {
            Moore,
            VonNeumann
        };

        #endregion

        #region ITerrainPaintableEffect Members

        public void PaintEffect (ITerrainChannel map, UUID userID, float rx, float ry, float rz, float strength,
                                float duration, float BrushSize)
        {
            strength = TerrainUtil.MetersToSphericalStrength (strength);

            int x, y;
            // Using one 'rain' round for this, so skipping a useless loop
            // Will need to adapt back in for the Flood brush

            ITerrainChannel water = new TerrainChannel (map.Width, map.Height, null);
            ITerrainChannel sediment = new TerrainChannel (map.Width, map.Height, null);

            // Fill with rain
            for (x = 0; x < water.Width; x++)
                for (y = 0; y < water.Height; y++)
                    water [x, y] =
                        (float)Math.Max (0.0, TerrainUtil.SphericalFactor (x, y, rx, ry, strength) * rainHeight * duration);

            for (int i = 0; i < rounds; i++) {
                // Erode underlying terrain
                for (x = 0; x < water.Width; x++) {
                    for (y = 0; y < water.Height; y++) {
                        const float solConst = (1.0f / rounds);
                        float sedDelta = water [x, y] * solConst;
                        map [x, y] -= sedDelta;
                        sediment [x, y] += sedDelta;
                    }
                }

                // Move water
                for (x = 0; x < water.Width; x++) {
                    for (y = 0; y < water.Height; y++) {
                        if (water [x, y] <= 0)
                            continue;

                        // Step 1. Calculate average of neighbors

                        int neighbours = 0;
                        float altitudeTotal = 0.0f;
                        float altitudeMe = map [x, y] + water [x, y];

                        const int NEIGHBOUR_ME = 4;
                        const int NEIGHBOUR_MAX = 9;

                        for (int j = 0; j < NEIGHBOUR_MAX; j++) {
                            if (j != NEIGHBOUR_ME) {
                                int [] coords = Neighbours (type, j);

                                coords [0] += x;
                                coords [1] += y;

                                if (coords [0] > map.Width - 1)
                                    continue;
                                if (coords [1] > map.Height - 1)
                                    continue;
                                if (coords [0] < 0)
                                    continue;
                                if (coords [1] < 0)
                                    continue;

                                // Calculate total height of this neighbor
                                float altitudeNeighbour = water [coords [0], coords [1]] + map [coords [0], coords [1]];

                                // If it's greater than me...
                                if (altitudeNeighbour - altitudeMe < 0) {
                                    // Add it to our calculations
                                    neighbours++;
                                    altitudeTotal += altitudeNeighbour;
                                }
                            }
                        }

                        if (neighbours == 0)
                            continue;

                        float altitudeAvg = altitudeTotal / neighbours;

                        // Step 2. Allocate water to neighbors.
                        for (int j = 0; j < NEIGHBOUR_MAX; j++) {
                            if (j != NEIGHBOUR_ME) {
                                int [] coords = Neighbours (type, j);

                                coords [0] += x;
                                coords [1] += y;

                                if (coords [0] > map.Width - 1)
                                    continue;
                                if (coords [1] > map.Height - 1)
                                    continue;
                                if (coords [0] < 0)
                                    continue;
                                if (coords [1] < 0)
                                    continue;

                                // Skip if we don't have water to begin with.
                                if (water [x, y] < 0)
                                    continue;

                                // Calculate our delta average
                                float altitudeDelta = altitudeMe - altitudeAvg;

                                if (altitudeDelta < 0)
                                    continue;

                                // Calculate how much water we can move
                                float waterMin = Math.Min (water [x, y], altitudeDelta);
                                float waterDelta = waterMin * ((water [coords [0], coords [1]] + map [coords [0], coords [1]])
                                                             / altitudeTotal);

                                float sedimentDelta = sediment [x, y] * (waterDelta / water [x, y]);

                                if (sedimentDelta > 0) {
                                    sediment [x, y] -= sedimentDelta;
                                    sediment [coords [0], coords [1]] += sedimentDelta;
                                }
                            }
                        }
                    }
                }

                // Evaporate

                for (x = 0; x < water.Width; x++) {
                    for (y = 0; y < water.Height; y++) {
                        water [x, y] *= 1.0f - (rainHeight / rounds);

                        float waterCapacity = waterSaturation * water [x, y];

                        float sedimentDeposit = sediment [x, y] - waterCapacity;
                        if (sedimentDeposit > 0) {
                            sediment [x, y] -= sedimentDeposit;
                            map [x, y] += sedimentDeposit;
                        }
                    }
                }
            }

            // Deposit any remainder (should be minimal)
            for (x = 0; x < water.Width; x++) {
                for (y = 0; y < water.Height; y++) {
                    if (!map.Scene.Permissions.CanTerraformLand (userID, new Vector3 (rx + x, ry + y, 0)))
                        continue;
                    if (sediment [x, y] > 0)
                        map [(int)rx + x, (int)ry + y] += sediment [x, y];
                }
            }
        }

        #endregion
    }
}
