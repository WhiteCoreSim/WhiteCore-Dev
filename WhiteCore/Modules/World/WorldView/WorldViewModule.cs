/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Utilities;
using System.Collections.Generic;

namespace WhiteCore.Modules.WorldView
{
    public class WorldViewModule : INonSharedRegionModule
    {

        bool m_Enabled = true;
        IMapImageGenerator m_Generator;
        string m_assetCacheDir = Constants.DEFAULT_ASSETCACHE_DIR;
        string m_worldviewCacheDir;
        bool m_cacheEnabled = true;
        float m_cacheExpires = 24;

        public void Initialise(IConfigSource config)
        {
         
            IConfig moduleConfig = config.Configs ["WorldViewModule"];
            if (moduleConfig != null)
            {
                // enabled by default but allow disabling
                m_Enabled = moduleConfig.GetBoolean ("Enabled", m_Enabled);
                m_cacheEnabled = moduleConfig.GetBoolean ("EnableCache", true);
                m_cacheExpires = moduleConfig.GetFloat("CacheExpires", m_cacheExpires);
            }
             
            if (m_Enabled)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "save worldview",
                    "save worldview [filename]< --fov degrees >",
                    "Save a view of the region to a file",
                    HandleSaveWorldview, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "save worldmaptile",
                    "save worldmaptile [filename] < --size pixels >",
                    "Save a maptile view of the region to a file",
                    HandleSaveWorldTile, true, true);


                if (m_cacheEnabled)
                {
                    m_assetCacheDir = config.Configs ["AssetCache"].GetString ("CacheDirectory",m_assetCacheDir);
                    CreateCacheDirectories (m_assetCacheDir);
                }

            }
        }

        public void AddRegion (IScene scene)
        {
        }

        public void RegionLoaded (IScene scene)
        {
            if (!m_Enabled)
                return;
            m_Generator = scene.RequestModuleInterface<IMapImageGenerator>();
            if (m_Generator == null)
            {
                m_Enabled = false;
                return;
            }

            ISimulationBase simulationBase = scene.RequestModuleInterface<ISimulationBase>();
            if (simulationBase != null)
            {
                IHttpServer server = simulationBase.GetHttpServer(0);
                server.AddStreamHandler(new WorldViewRequestHandler(this,
                        scene.RegionInfo.RegionID.ToString()));
                MainConsole.Instance.Info("[WORLDVIEW]: Configured and enabled for " + scene.RegionInfo.RegionName);
                MainConsole.Instance.Info("[WORLDVIEW]: RegionID " + scene.RegionInfo.RegionID);
            }
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public string Name
        {
            get { return "WorldViewModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public bool CacheEnabled
        {
            get { return m_cacheEnabled; }
        }

        public float CacheExpires
        {
            get { return m_cacheExpires; }
        }

        public string CacheDir
        {
            get { return m_worldviewCacheDir; }
        }

        void CreateCacheDirectories(string cacheDir)
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            m_worldviewCacheDir = cacheDir + "/worldview";
            if (!Directory.Exists (m_worldviewCacheDir))
                Directory.CreateDirectory (m_worldviewCacheDir);
        }

        public byte[] GenerateWorldView(Vector3 pos, Vector3 rot, float fov,
                int width, int height, bool usetex)
        {
            if (!m_Enabled)
                return new Byte[0];

            Bitmap bmp = m_Generator.CreateViewImage(pos, rot, fov, width, height, usetex);

            MemoryStream str = new MemoryStream();

            bmp.Save(str, ImageFormat.Jpeg);

            return str.ToArray();
        }

        public void SaveRegionWorldView (IScene scene, string fileName, float fieldOfView)
        {
            m_Generator = scene.RequestModuleInterface<IMapImageGenerator>();
            if (m_Generator == null)
                return;

            // set some basic defaults
            Vector3 camPos = new Vector3 ();
            //camPos.Y = scene.RegionInfo.RegionSizeY / 2 - 0.5f;
            //camPos.X = scene.RegionInfo.RegionSizeX / 2 - 0.5f;
            //camPos.Z = 221.7025033688163f);

            camPos.X = 1.25f;
            camPos.Y = 1.25f;
            camPos.Z = 61.0f;

            Vector3 camDir = new Vector3 ();
            camDir.X = .687462f;                        // -1  -< y/x > 1
            camDir.Y = .687462f;
            camDir.Z = -0.23536f;                       // -1 (up) < Z > (down) 1

            float fov = 89f;                            // degrees
            if (fieldOfView > 0)
                fov = fieldOfView;

            int width = 1280;           
            int height = 720;  

            byte[] jpeg = ExportWorldView(camPos, camDir, fov, width, height, true); 

            // save image
            var savePath = fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = scene.RegionInfo.RegionName + ".jpg";
                savePath = PathHelpers.VerifyWriteFile (fileName, ".jpg", Constants.DEFAULT_DATA_DIR + "/worldview", true);
            }
            File.WriteAllBytes(savePath, jpeg);

        }

        public void SaveRegionWorldMapTile (IScene scene, string fileName, int size)
        {
            m_Generator = scene.RequestModuleInterface<IMapImageGenerator>();
            if (m_Generator == null)
                return;


            byte[] jpeg = ExportWorldMapTile(size); 

            // save image
            var savePath = fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = scene.RegionInfo.RegionName + "_maptile.jpg";
                savePath = PathHelpers.VerifyWriteFile (fileName, ".jpg", Constants.DEFAULT_DATA_DIR + "/worldview", true);
            }
            File.WriteAllBytes(savePath, jpeg);

        }

        public byte[] ExportWorldView(Vector3 camPos, Vector3 camDir, float fov,
            int width, int height, bool usetex)
        {
           // String background = @"html/images/sky_bg.jpg";

            Bitmap bmp = m_Generator.CreateViewImage(camPos, camDir, fov, width, height, usetex);

            /*
            Color bgColor = Color.FromArgb( 0xFF, 0x8B, 0xC4, 0xEC);
            bmp.MakeTransparent (bgColor);

            //this does not crash but probably needs transparency set correctly
            var bgBmp = Bitmap.FromFile(background);
            Bitmap outbmp = ImageUtils.ResizeImage(bgBmp, width, height);

            //create a bitmap to hold the combined image
            var finalImage = new System.Drawing.Bitmap(width, height);

            //get a graphics object from the image so we can draw on it
            using (Graphics g = Graphics.FromImage(finalImage))
            {
                //set background color
                //g.Clear(Color.Black);

                //go through each image and draw it on the final image
                g.DrawImage(outbmp,  new Rectangle(0, 0, width, height));
                g.DrawImage(bmp,  new Rectangle(0, 0, width, height));
            }
            if (finalImage != null)
*/
            if (bmp != null)
            {
                MemoryStream str = new MemoryStream ();

                bmp.Save (str, ImageFormat.Jpeg);

                return str.ToArray ();
            } 

            return null;

        }

        public byte[] ExportWorldMapTile(int size)
        {


            Bitmap bmp = m_Generator.CreateViewTileImage(size);

            if (bmp != null)
            {
                MemoryStream str = new MemoryStream ();

                bmp.Save (str, ImageFormat.Jpeg);

                return str.ToArray ();
            } else
                return null;

        }

        protected void HandleSaveWorldview(IScene scene, string[] cmdparams)
        {
            string fileName = "";
            float fieldOfView = 0f;

            // check for switch options
            var cmds = new List <string>();
            for (int i = 2; i < cmdparams.Length;)
            {
                if (cmdparams [i].StartsWith ("--fov"))
                {
                    fieldOfView = float.Parse(cmdparams [i + 1]);
                    i +=2;
                } else
                {
                    cmds.Add (cmdparams [i]);
                    i++;
                }
            }


            if (cmds.Count > 0)
                fileName = cmds [0];
            else
            {
                fileName = scene.RegionInfo.RegionName;
                fileName = MainConsole.Instance.Prompt (" Worldview filename", fileName);
                if (fileName == "")
                    return;
            }

            //some file sanity checks
            var savePath = PathHelpers.VerifyWriteFile (fileName, ".jpg", Constants.DEFAULT_DATA_DIR + "/worldview", true);

            MainConsole.Instance.InfoFormat (
                "[Worldview]: Saving worldview for {0} to {1}", scene.RegionInfo.RegionName, savePath);
        
            SaveRegionWorldView (scene, savePath, fieldOfView);
        
        }

        protected void HandleSaveWorldTile(IScene scene, string[] cmdparams)
        {
            string fileName = "";
            int size = 256;

            // check for switch options
            var cmds = new List <string>();
            for (int i = 2; i < cmdparams.Length;)
            {
                if (cmdparams [i].StartsWith ("--size"))
                {
                    size = int.Parse(cmdparams [i + 1]);
                    i +=2;
                } else
                {
                    cmds.Add (cmdparams [i]);
                    i++;
                }
            }

            if (cmds.Count > 0)
                fileName = cmds [0];
            else
            {
                fileName = scene.RegionInfo.RegionName;
                fileName = MainConsole.Instance.Prompt (" World maptile filename", fileName);
                if (fileName == "")
                    return;
            }

            //some file sanity checks
            var savePath = PathHelpers.VerifyWriteFile (fileName+"_maptile", ".jpg", Constants.DEFAULT_DATA_DIR + "/worldview", true);

            MainConsole.Instance.InfoFormat (
                "[Worldview]: Saving world maptile for {0} to {1}", scene.RegionInfo.RegionName, savePath);

            SaveRegionWorldMapTile (scene, savePath, size);

        }

        /*
         private void ExportArchiveImage(UUID imageUUID, string archiveName, string filePath)
        {
            byte[] jpeg = new byte[0];

            using (MemoryStream imgstream = new MemoryStream())
            {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                // non-async because we know we have the asset immediately.
                byte[] imageAsset = AssetService.GetData(imageUUID.ToString());

                if (imageAsset != null)
                {
                    // Decode image to System.Drawing.Image
                    Image image = null;
                    ManagedImage managedImage;
                    if (OpenJPEG.DecodeToImage(imageAsset, out managedImage, out image))
                    {
                        // Save to bitmap
                        using (Bitmap texture = ResizeBitmap(image, 256, 256, archiveName))
                        {
                            EncoderParameters myEncoderParameters = new EncoderParameters();
                            myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,
                                75L);

                            // Save bitmap to stream
                            texture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);

                            // Write the stream to a byte array for output
                            jpeg = imgstream.ToArray();

                            // save image
                            string fileName = archiveName + ".jpg";
                            string fullPath = Path.Combine(filePath, fileName);
                            File.WriteAllBytes(fullPath, jpeg);

                        }
                        image.Dispose();
                    }
                }
            }
        }


        private Bitmap ResizeBitmap(Image b, int nWidth, int nHeight, string name)
        {
            Bitmap newsize = new Bitmap(nWidth, nHeight);
            Graphics temp = Graphics.FromImage(newsize);
            temp.DrawImage(b, 0, 0, nWidth, nHeight);
            temp.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (name != "")
                temp.DrawString(name, new Font("Arial", 8, FontStyle.Regular),
                    new SolidBrush(Color.FromArgb(90, 255, 255, 180)), new Point(2, nHeight - 13));

            return newsize;
        }

        // From msdn
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
           ImageCodecInfo[] encoders;
           encoders = ImageCodecInfo.GetImageEncoders();
            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
*/
    }
}
