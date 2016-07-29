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
using System.Drawing;
using Nini.Config;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;

namespace WhiteCore.Modules.WorldMap
{
    public class ShadedMapTileRenderer : IMapTileTerrainRenderer
    {
        static readonly Color WATER_COLOR = Color.FromArgb (29, 71, 95);

        IScene m_scene;
        // IConfigSource m_config; // not used currently

        #region IMapTileTerrainRenderer Members

        public void Initialise (IScene scene, IConfigSource config)
        {
            m_scene = scene;
            // m_config = config; // not used currently
        }

        public Bitmap TerrainToBitmap (Bitmap mapbmp)
        {
            //MainConsole.Instance.Info("[ShadedMap generator]: Generating Maptile Step 1: Terrain");

            ITerrainChannel heightmap = m_scene.RequestModuleInterface<ITerrainChannel> ();
            bool ShadowDebugContinue = true;

            bool terraincorruptedwarningsaid = false;

            float low = 255;
            float high = 0;
            for (int x = 0; x < m_scene.RegionInfo.RegionSizeX; x++) {
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY; y++) {
                    float hmval = heightmap [x, y];
                    if (hmval < low)
                        low = hmval;
                    if (hmval > high)
                        high = hmval;
                }
            }

            float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;

            for (int x = 0; x < m_scene.RegionInfo.RegionSizeX; x++) {
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY; y++) {
                    // Y flip the coordinates for the bitmap: hf origin is lower left, bm origin is upper left
                    int yr = (m_scene.RegionInfo.RegionSizeY - 1) - y;

                    float heightvalue = heightmap [x, y];

                    if (heightvalue > waterHeight) {
                        // scale height value
                        // No, that doesn't scale it:
                        // heightvalue = low + mid * (heightvalue - low) / mid; => low + (heightvalue - low) * mid / mid = low + (heightvalue - low) * 1 = low + heightvalue - low = heightvalue

                        if (float.IsInfinity (heightvalue) || float.IsNaN (heightvalue))
                            heightvalue = 0;
                        else if (heightvalue > 255f)
                            heightvalue = 255f;
                        else if (heightvalue < 0f)
                            heightvalue = 0f;

                        Color color = Color.FromArgb ((int)heightvalue, 100, (int)heightvalue);

                        mapbmp.SetPixel (x, yr, color);

                        try {
                            //X
                            // .
                            //
                            // Shade the terrain for shadows
                            if (x < (m_scene.RegionInfo.RegionSizeX - 1) && yr < (m_scene.RegionInfo.RegionSizeY - 1)) {
                                float hfvalue = heightmap [x, y];
                                float hfvaluecompare = 0f;

                                if ((x + 1 < m_scene.RegionInfo.RegionSizeX) && (y + 1 < m_scene.RegionInfo.RegionSizeY)) {
                                    hfvaluecompare = heightmap [x + 1, y + 1];
                                    // light from north-east => look at land height there
                                }
                                if (float.IsInfinity (hfvalue) || float.IsNaN (hfvalue))
                                    hfvalue = 0f;

                                if (float.IsInfinity (hfvaluecompare) || float.IsNaN (hfvaluecompare))
                                    hfvaluecompare = 0f;

                                float hfdiff = hfvalue - hfvaluecompare;
                                // => positive if NE is lower, negative if here is lower

                                int hfdiffi = 0;
                                int hfdiffihighlight = 0;
                                float highlightfactor = 0.18f;

                                try {
                                    // hfdiffi = Math.Abs((int)((hfdiff * 4) + (hfdiff * 0.5))) + 1;
                                    hfdiffi = Math.Abs ((int)(hfdiff * 4.5f)) + 1;
                                    if (hfdiff % 1f > 0) {
                                        // hfdiffi = hfdiffi + Math.Abs((int)(((hfdiff % 1) * 0.5f) * 10f) - 1);
                                        hfdiffi = hfdiffi + Math.Abs ((int)((hfdiff % 1f) * 5f) - 1);
                                    }

                                    hfdiffihighlight = Math.Abs ((int)((hfdiff * highlightfactor) * 4.5f)) + 1;
                                    if (hfdiff % 1f > 0) {
                                        // hfdiffi = hfdiffi + Math.Abs((int)(((hfdiff % 1) * 0.5f) * 10f) - 1);
                                        hfdiffihighlight = hfdiffihighlight +
                                                           Math.Abs ((int)(((hfdiff * highlightfactor) % 1f) * 5f) - 1);
                                    }
                                } catch (OverflowException) {
                                    MainConsole.Instance.Debug ("[ShadedMap genertor]: Shadow failed at value: " + hfdiff);
                                    ShadowDebugContinue = false;
                                }

                                if (hfdiff > 0.3f) {
                                    // NE is lower than here
                                    // We have to desaturate and lighten the land at the same time
                                    // we use floats, colors use bytes, so shrink are space down to
                                    // 0-255

                                    if (ShadowDebugContinue) {
                                        int r = color.R;
                                        int g = color.G;
                                        int b = color.B;
                                        color = Color.FromArgb (
                                            (r + hfdiffihighlight < 255) ? r + hfdiffihighlight : 255,
                                            (g + hfdiffihighlight < 255) ? g + hfdiffihighlight : 255,
                                            (b + hfdiffihighlight < 255) ? b + hfdiffihighlight : 255);
                                    }
                                } else if (hfdiff < -0.3f) {
                                    // here is lower than NE:
                                    // We have to desaturate and blacken the land at the same time
                                    // we use floats, colors use bytes, so shrink are space down to
                                    // 0-255

                                    if (ShadowDebugContinue) {
                                        if ((x - 1 > 0) && (yr + 1 < m_scene.RegionInfo.RegionSizeY)) {
                                            color = mapbmp.GetPixel (x - 1, yr + 1);
                                            int r = color.R;
                                            int g = color.G;
                                            int b = color.B;
                                            color = Color.FromArgb ((r - hfdiffi > 0) ? r - hfdiffi : 0,
                                                                   (g - hfdiffi > 0) ? g - hfdiffi : 0,
                                                                   (b - hfdiffi > 0) ? b - hfdiffi : 0);

                                            mapbmp.SetPixel (x - 1, yr + 1, color);
                                        }
                                    }
                                }
                            }
                        } catch (ArgumentException) {
                            if (!terraincorruptedwarningsaid) {
                                MainConsole.Instance.WarnFormat (
                                    "[ShadedMap generator]: Your terrain is corrupted in region {0}.\n" +
                                    " It may take a few minutes to generate the map image depending on the corruption level",
                                    m_scene.RegionInfo.RegionName);
                                terraincorruptedwarningsaid = true;
                            }
                            color = Color.Black;
                            mapbmp.SetPixel (x, yr, color);
                        }
                    } else {
                        // We're under the water level with the terrain, so paint water instead of land

                        // Y flip the cordinates
                        heightvalue = waterHeight - heightvalue;
                        if (float.IsInfinity (heightvalue) || float.IsNaN (heightvalue))
                            heightvalue = 0f;
                        else if (heightvalue > 19f)
                            heightvalue = 19f;
                        else if (heightvalue < 0f)
                            heightvalue = 0f;

                        heightvalue = 100f - (heightvalue * 100f) / 19f;

                        try {
                            mapbmp.SetPixel (x, yr, WATER_COLOR);
                        } catch (ArgumentException) {
                            if (!terraincorruptedwarningsaid) {
                                MainConsole.Instance.WarnFormat (
                                    "[ShadedMap generator]: Your terrain is corrupted in region {0}.\n" +
                                    "It may take a few minutes to generate the map image depending on the corruption level",
                                    m_scene.RegionInfo.RegionName);
                                terraincorruptedwarningsaid = true;
                            }
                            Color black = Color.Black;
                            mapbmp.SetPixel (x, (m_scene.RegionInfo.RegionSizeY - y) - 1, black);
                        }
                    }
                }
            }
            // MainConsole.Instance.Info("[ShadedMap generator]: Generating Maptile Step 1: Done in " + (Environment.TickCount - tc) + " ms");
            return mapbmp;
        }

        #endregion
    }
}
