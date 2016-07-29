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
using System.Drawing.Imaging;
using System.IO;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.ConsoleFramework;

namespace WhiteCore.Modules.Terrain.FileLoaders
{
    public class JPEG : ITerrainLoader
    {
        #region ITerrainLoader Members

        public string FileExtension {
            get { return ".jpg"; }
        }

        public ITerrainChannel LoadFile (string filename, IScene scene)
        {
            return LoadBitmap (new Bitmap (filename), scene);
        }

        public ITerrainChannel LoadFile (string filename, IScene scene, int x, int y, int fileWidth, int fileHeight, int w, int h)
        {
            // [[THEMAJOR]] Some work on tile loading..
            // terrain load-tile Tile.png 5 5 10000 10050
            Bitmap tilemap = new Bitmap (filename);

            // Prevents off-by-one issue
            fileHeight--;

            int xoffset = w * x;
            int yoffset = h * (fileHeight - y);

            MainConsole.Instance.DebugFormat (
                "[Terrain]: Loading tile {0},{1} (offset {2},{3}) from tile-map size of {4},{5}",
                x, y, xoffset, yoffset, fileWidth, fileHeight);

            Rectangle tileRect = new Rectangle (xoffset, yoffset, w, h);
            PixelFormat format = tilemap.PixelFormat;
            Bitmap cloneBitmap = null;
            try {
                cloneBitmap = tilemap.Clone (tileRect, format);
            } catch (OutOfMemoryException e) {
                // This error WILL appear if the number of Y tiles is too high because of how it works from the bottom up
                // However, this still spits out ugly unreferenced object errors on the console
                MainConsole.Instance.ErrorFormat (
                    "[Terrain]: Couldn't load tile {0},{1} (from bitmap coordinates {2},{3}). Number of specified Y tiles may be too high: {4}",
                    x, y, xoffset, yoffset, e);
            } finally {
                // Some attempt at keeping a clean memory
                tilemap.Dispose ();
            }

            return LoadBitmap (cloneBitmap, scene);

        }

        public ITerrainChannel LoadStream (Stream stream, IScene scene)
        {
            //throw new NotImplementedException();
            return LoadBitmap (new Bitmap (stream), scene);

        }

        public void SaveFile (string filename, ITerrainChannel map)
        {
            Bitmap colours = CreateBitmapFromMap (map);
            try {
                colours.Save (filename, ImageFormat.Jpeg);
            } catch {
            }
            colours.Dispose ();
        }

        /// <summary>
        ///     Exports a stream using a System.Drawing exporter.
        /// </summary>
        /// <param name="stream">The target stream</param>
        /// <param name="map">The terrain channel being saved</param>
        public void SaveStream (Stream stream, ITerrainChannel map)
        {
            Bitmap colours = CreateBitmapFromMap (map);
            try {
                colours.Save (stream, ImageFormat.Jpeg);
            } catch {
            }
            colours.Dispose ();
        }

        #endregion

        protected virtual ITerrainChannel LoadBitmap (Bitmap bitmap, IScene scene)
        {
            ITerrainChannel retval = new TerrainChannel (bitmap.Width, bitmap.Height, scene);

            int x;
            int y;

            for (x = 0; x < bitmap.Width; x++) {
                for (y = 0; y < bitmap.Height; y++)
                    retval [x, y] = bitmap.GetPixel (x, bitmap.Height - y - 1).GetBrightness () * 128;
            }

            bitmap.Dispose ();  // not needed anymore
            return retval;
        }

        public override string ToString ()
        {
            return "JPEG";
        }

        static Bitmap CreateBitmapFromMap (ITerrainChannel map)
        {
            Bitmap gradientmapLd = new Bitmap ("defaultstripe.png");

            int pallete = gradientmapLd.Height;

            Bitmap bmp = new Bitmap (map.Width, map.Height);
            Color [] colours = new Color [pallete];

            for (int i = 0; i < pallete; i++) {
                colours [i] = gradientmapLd.GetPixel (0, i);
            }

            for (int y = 0; y < map.Height; y++) {
                for (int x = 0; x < map.Width; x++) {
                    // 512 is the largest possible height before colors clamp
                    int colorindex = (int)(Math.Max (Math.Min (1.0, map [x, y] / 512.0), 0.0) * (pallete - 1));
                    bmp.SetPixel (x, map.Height - y - 1, colours [colorindex]);
                }
            }

            gradientmapLd.Dispose ();
            return bmp;
        }
    }
}
