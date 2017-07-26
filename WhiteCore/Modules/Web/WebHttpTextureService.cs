/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org
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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;

namespace WhiteCore.Modules.Web
{
    public class WebHttpTextureService : IService, IWebHttpTextureService
    {
        protected IRegistryCore _registry;
        protected string _gridNick;
        protected IHttpServer _server;

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            _registry = registry;
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup ()
        {
            _server = _registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (0);
            if (_server != null) {
                _server.AddStreamHandler (new GenericStreamHandler ("GET", "/index.php?method=GridTexture", OnHTTPGetTextureImage));
                _server.AddStreamHandler (new GenericStreamHandler ("GET", "/index.php?method=AvatarTexture", OnHTTPGetAvatarImage));
                _server.AddStreamHandler (new GenericStreamHandler ("GET", "/WebImage", OnHTTPGetImage));
                _registry.RegisterModuleInterface<IWebHttpTextureService> (this);
            }
            IGridInfo gridInfo = _registry.RequestModuleInterface<IGridInfo> ();
            _gridNick = gridInfo != null
                            ? gridInfo.GridName
                            : "No Grid Name Available, please set this";
        }

        public string GetTextureURL (UUID textureID)
        {
            return _server.ServerURI + "/index.php?method=GridTexture&uuid=" + textureID;
        }

        public string GetRegionWorldViewURL (UUID RegionID)
        {
            return _server.ServerURI + "/worldview/" + RegionID;
        }

        public string GetAvatarImageURL (string imageURL)
        {
            return _server.ServerURI + "/index.php?method=AvatarTexture&imageurl=" + imageURL;
        }

        public string GetImageURL (string imageURL)
        {
            return _server.ServerURI + "/WebImage?imageurl=" + imageURL;
        }

        public byte [] OnHTTPGetTextureImage (string path, Stream request, OSHttpRequest httpRequest,
                                            OSHttpResponse httpResponse)
        {
            byte [] jpeg = new byte [0];
            httpResponse.ContentType = "image/jpeg";
            var imageUUID = httpRequest.QueryString ["uuid"];

            // check for bogies
            if (imageUUID == UUID.Zero.ToString ())
                return jpeg;
            
            IAssetService m_AssetService = _registry.RequestModuleInterface<IAssetService> ();

            using (MemoryStream imgstream = new MemoryStream ()) {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                // non-async because we know we have the asset immediately.
                byte [] mapasset = m_AssetService.GetData (httpRequest.QueryString ["uuid"]);

                if (mapasset != null) {
                    // Decode image to System.Drawing.Image
                    Image image;
                    ManagedImage managedImage;
                    EncoderParameters myEncoderParameters = new EncoderParameters ();
                    myEncoderParameters.Param [0] = new EncoderParameter (Encoder.Quality, 75L);
                    if (OpenJPEG.DecodeToImage (mapasset, out managedImage, out image)) {
                        // Save to bitmap
                        var texture = ResizeBitmap (image, 256, 256);
                        try {
                            var encInfo = GetEncoderInfo ("image/jpeg");
                            if (encInfo != null)
                                texture.Save (imgstream, encInfo, myEncoderParameters);

                            // Write the stream to a byte array for output
                            jpeg = imgstream.ToArray ();
                        } catch {
                        }
                    }
                    myEncoderParameters.Dispose ();
                    if (image != null)
                        image.Dispose ();
                }
            }

            if (jpeg.Length > 0)
                return jpeg;

            // no UUID here so...
            string nouuid = "html/images/icons/no_image.png";
            try {
                return File.ReadAllBytes (nouuid);
            } catch {
            }

            return new byte [0];
        }


        public byte [] OnHTTPGetAvatarImage (string path, Stream request, OSHttpRequest httpRequest,
                                             OSHttpResponse httpResponse)
        {
            httpResponse.ContentType = "image/jpeg";

            string uri = httpRequest.QueryString ["imageurl"];
            string nourl = "html/images/icons/no_avatar.jpg";

            try {
                if (File.Exists (uri)) {
                    return File.ReadAllBytes (uri);
                }
                return File.ReadAllBytes (nourl);
            } catch {
            }
                
            return new byte [0];
        }


        public byte [] OnHTTPGetImage (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
            httpResponse.ContentType = "image/jpeg";

            string uri = httpRequest.QueryString ["imageurl"];
            string nourl = "html/images/noimage.jpg";

            try {
                if (File.Exists (uri)) {
                    return File.ReadAllBytes (uri);
                }
                return File.ReadAllBytes (nourl);
            } catch {
            }

            return new byte [0];
        }

        Bitmap ResizeBitmap (Image b, int nWidth, int nHeight)
        {
            Bitmap newsize = new Bitmap (nWidth, nHeight);
            Graphics temp = Graphics.FromImage (newsize);
            temp.DrawImage (b, 0, 0, nWidth, nHeight);
            temp.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            temp.DrawString (_gridNick, new Font ("Arial", 8, FontStyle.Regular),
                            new SolidBrush (Color.FromArgb (90, 255, 255, 50)), new Point (2, nHeight - 13));

            temp.Dispose ();
            return newsize;
        }

        // From MSDN
        static ImageCodecInfo GetEncoderInfo (string mimeType)
        {
            ImageCodecInfo [] encoders;
            try {
                encoders = ImageCodecInfo.GetImageEncoders ();
            } catch {
                return null;
            }

            for (int j = 0; j < encoders.Length; ++j) {
                if (encoders [j].MimeType == mimeType)
                    return encoders [j];
            }
            return null;
        }
    }
}
