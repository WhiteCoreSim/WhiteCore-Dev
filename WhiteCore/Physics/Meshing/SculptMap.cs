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

// to build without references to System.Drawing, comment this out

#define SYSTEM_DRAWING
#undef FASTBMP                     // expiremental. Advise if problems are seen <greythane@gmail.com>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using WhiteCore.Framework.ConsoleFramework;
#if FASTBMP
using WhiteCore.Framework.Utilities;
#endif

#if SYSTEM_DRAWING

namespace WhiteCore.Physics.PrimMesher
{
    public class SculptMap
    {
        public int width;
        public int height;
        public byte[] redBytes;
        public byte[] greenBytes;
        public byte[] blueBytes;

        public SculptMap ()
        {
        }

        public SculptMap (Bitmap bm, int lod)
        {
            int bmW = bm.Width;
            int bmH = bm.Height;

            if (bmW == 0 || bmH == 0)
                MainConsole.Instance.Error("[Sculptmap]: Bitmap has no data");

            int numLodPixels = lod * 2 * lod * 2; // (32 * 2)^2  = 64^2 pixels for default sculpt map image

            bool needsScaling = false;

            bool smallMap = bmW * bmH <= lod * lod;

            width = bmW;
            height = bmH;
            while (width * height > numLodPixels)
            {
                width >>= 1;
                height >>= 1;
                needsScaling = true;
            }


            try
            {
                if (needsScaling)
                    bm = ScaleImage (bm, width, height, InterpolationMode.NearestNeighbor);
            } catch (Exception e)
            {
                MainConsole.Instance.Error ("[Sculptmap]: Exception in ScaleImage(): e: " + e);
            }

            if (width * height > lod * lod)
            {
                width >>= 1;
                height >>= 1;
            }

            int numBytes = (width + 1) * (height + 1);
            redBytes = new byte[numBytes];
            greenBytes = new byte[numBytes];
            blueBytes = new byte[numBytes];
#if FASTBMP
            FastBitmap unsafeBMP = new FastBitmap (bm);
            unsafeBMP.LockBitmap (); //Lock the bitmap for the unsafe operation
#endif
            int byteNdx = 0;

            try
            {
                for (int y = 0; y <= height; y++)
                {
                    for (int x = 0; x <= width; x++)
                    {
                        Color pixel;
                        if (smallMap)
                        {
#if FASTBMP
                            pixel = unsafeBMP.GetPixel (x < width ? x : x - 1,
#else
                            pixel = bm.GetPixel (x < width ? x : x - 1,
#endif
                                y < height ? y : y - 1);
                        } else
                        {
#if FASTBMP
                            pixel = unsafeBMP.GetPixel (x < width ? x * 2 : x * 2 - 1,
#else
                            pixel = bm.GetPixel (x < width ? x * 2 : x * 2 - 1,
#endif
                                y < height ? y * 2 : y * 2 - 1);
                        }

                        redBytes [byteNdx] = pixel.R;
                        greenBytes [byteNdx] = pixel.G;
                        blueBytes [byteNdx] = pixel.B;

                        ++byteNdx;
                    }
                }
            } catch (Exception e)
            {
                MainConsole.Instance.Error("[SculptMap]: Caught exception processing byte arrays in SculptMap(): e: " + e);
            }
#if FASTBMP
            //All done, unlock
            unsafeBMP.UnlockBitmap ();
#endif
            bm.Dispose ();
            width++;
            height++;
        }

        public List<List<Coord>> ToRows (bool mirror)
        {
            int numRows = height;
            int numCols = width;

            List<List<Coord>> rows = new List<List<Coord>> (numRows);

            float pixScale = 1.0f / 255;

            int rowNdx, colNdx;
            int smNdx = 0;

            for (rowNdx = 0; rowNdx < numRows; rowNdx++)
            {
                List<Coord> row = new List<Coord> (numCols);
                for (colNdx = 0; colNdx < numCols; colNdx++)
                {
                    if (mirror)
                        row.Add (new Coord (-(redBytes [smNdx] * pixScale - 0.5f), (greenBytes [smNdx] * pixScale - 0.5f),
                            blueBytes [smNdx] * pixScale - 0.5f));
                    else
                        row.Add (new Coord (redBytes [smNdx] * pixScale - 0.5f, greenBytes [smNdx] * pixScale - 0.5f,
                            blueBytes [smNdx] * pixScale - 0.5f));

                    ++smNdx;
                }
                rows.Add (row);
            }
            return rows;
        }

        Bitmap ScaleImage (Bitmap srcImage, int destWidth, int destHeight, InterpolationMode interpMode)
        {
            // just in case of furfies  :)
            if (destWidth == 0 || destHeight == 0)
                return srcImage;
            
            Bitmap scaledImage = new Bitmap (destWidth, destHeight, PixelFormat.Format24bppRgb);

            Color c;
            float xscale = srcImage.Width / (float)destWidth;
            float yscale = srcImage.Height / (float) destHeight;

            float sy = 0.5f;
            for (int y = 0; y < destHeight; y++)
            {
                float sx = 0.5f;
                for (int x = 0; x < destWidth; x++)
                {
                    try
                    {
                        c = srcImage.GetPixel ((int)(sx), (int)(sy));
                        scaledImage.SetPixel (x, y, Color.FromArgb (c.R, c.G, c.B));
                    } catch // not sure why this one specifically?? //(IndexOutOfRangeException)
                    {
                    }

                    sx += xscale;
                }
                sy += yscale;
            }
            srcImage.Dispose ();
            return scaledImage;

            /*
            Bitmap scaledImage = new Bitmap(srcImage, destWidth, destHeight);
            scaledImage.SetResolution(96.0f, 96.0f);

            Graphics grPhoto = Graphics.FromImage(scaledImage);
            grPhoto.InterpolationMode = interpMode;

            grPhoto.DrawImage(srcImage,
                              new Rectangle(0, 0, destWidth, destHeight),
                              new Rectangle(0, 0, srcImage.Width, srcImage.Height),
                              GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return scaledImage;
             */
        }
    }
}

#endif