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


namespace WhiteCore.Modules.Terrain.PaintBrushes
{
    public class RaiseSphere : ITerrainPaintableEffect
    {
        #region ITerrainPaintableEffect Members

        public void PaintEffect (ITerrainChannel map, UUID userID, float rx, float ry, float rz, float strength,
                                float duration, float BrushSize)
        {
            int x;
            int xFrom = (int)(rx - BrushSize + 0.5);
            int xTo = (int)(rx + BrushSize + 0.5) + 1;
            int yFrom = (int)(ry - BrushSize + 0.5);
            int yTo = (int)(ry + BrushSize + 0.5) + 1;

            if (xFrom < 0)
                xFrom = 0;

            if (yFrom < 0)
                yFrom = 0;

            if (xTo > map.Width)
                xTo = map.Width;

            if (yTo > map.Height)
                yTo = map.Height;

            for (x = xFrom; x < xTo; x++) {
                int y;
                for (y = yFrom; y < yTo; y++) {
                    if (!map.Scene.Permissions.CanTerraformLand (userID, new Vector3 (x, y, 0)))
                        continue;

                    // Calculate a cos-sphere and add it to the heightmap
                    double r = Math.Sqrt ((x - rx) * (x - rx) + ((y - ry) * (y - ry)));
                    double z = Math.Cos (r * Math.PI / (BrushSize * 2));
                    if (z > 0.0)
                        map [x, y] += (float)(z * duration);
                }
            }
        }

        #endregion
    }
}
