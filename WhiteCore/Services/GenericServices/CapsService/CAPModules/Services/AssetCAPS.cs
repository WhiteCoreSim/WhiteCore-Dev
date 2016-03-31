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
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;
using Encoder = System.Drawing.Imaging.Encoder;

namespace WhiteCore.Services
{
    public class AssetCAPS : IExternalCapsRequestHandler
    {

        protected IAssetService m_assetService;
        protected IJ2KDecoder m_j2kDecoder;
        protected UUID m_AgentID;
        public const string DefaultFormat = "x-j2c";
        // TODO: Change this to a config option
        protected string REDIRECT_URL = null;
        string m_getTextureURI;
        string m_getMeshURI;
        string m_bakedTextureURI;

        public string Name { get { return GetType().Name; } }

        public void IncomingCapsRequest(UUID agentID, WhiteCore.Framework.Services.GridRegion region, ISimulationBase simbase, ref OSDMap capURLs)
        {
            m_AgentID = agentID;
            m_assetService = simbase.ApplicationRegistry.RequestModuleInterface<IAssetService>();
            m_j2kDecoder = simbase.ApplicationRegistry.RequestModuleInterface<IJ2KDecoder>();

            m_getTextureURI = "/CAPS/GetTexture/" + UUID.Random() + "/";
            capURLs["GetTexture"] = MainServer.Instance.ServerURI + m_getTextureURI;
            MainServer.Instance.AddStreamHandler(new GenericStreamHandler("GET", m_getTextureURI, ProcessGetTexture));

            m_bakedTextureURI = "/CAPS/UploadBakedTexture/" + UUID.Random() + "/";
            capURLs["UploadBakedTexture"] = MainServer.Instance.ServerURI + m_bakedTextureURI;
            MainServer.Instance.AddStreamHandler(new GenericStreamHandler("POST", m_bakedTextureURI, UploadBakedTexture));

            m_getMeshURI = "/CAPS/GetMesh/" + UUID.Random() + "/";
            capURLs["GetMesh"] = MainServer.Instance.ServerURI + m_getMeshURI;
            MainServer.Instance.AddStreamHandler(new GenericStreamHandler("GET", m_getMeshURI, ProcessGetMesh));
        }

        public void IncomingCapsDestruction()
        {
            MainServer.Instance.RemoveStreamHandler("GET", m_getTextureURI);
            MainServer.Instance.RemoveStreamHandler("POST", m_bakedTextureURI);
            MainServer.Instance.RemoveStreamHandler("GET", m_getMeshURI);
        }

        #region Get Texture

        byte[] ProcessGetTexture(string path, Stream request, OSHttpRequest httpRequest,
                                         OSHttpResponse httpResponse)
        {
            //MainConsole.Instance.DebugFormat("[GETTEXTURE]: called in {0}", m_scene.RegionInfo.RegionName);

            // Try to parse the texture ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            string textureStr = query.GetOne("texture_id");
            string format = query.GetOne("format");

            if (m_assetService == null)
            {
                httpResponse.StatusCode = (int) System.Net.HttpStatusCode.NotFound;
                return MainServer.BlankResponse;
            }

            UUID textureID;
            if (!String.IsNullOrEmpty(textureStr) && UUID.TryParse(textureStr, out textureID))
            {
                string[] formats;
                if (!string.IsNullOrEmpty(format))
                    formats = new[] {format.ToLower()};
                else
                {
                    formats = WebUtils.GetPreferredImageTypes(httpRequest.Headers.Get("Accept"));
                    if (formats.Length == 0)
                        formats = new[] {DefaultFormat}; // default
                }

                // OK, we have an array with preferred formats, possibly with only one entry
                byte[] response;
                foreach (string f in formats)
                {
                    if (FetchTexture(httpRequest, httpResponse, textureID, f, out response))
                        return response;
                }
            }

            // null or invalid UUID
            MainConsole.Instance.Warn("[AssetCAPS]: Failed to parse a texture_id from GetTexture request: " +
                                          httpRequest.Url);
            httpResponse.StatusCode = (int) System.Net.HttpStatusCode.NotFound;
            return MainServer.BlankResponse;
        }

        /// <summary>
        /// </summary>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <param name="textureID"></param>
        /// <param name="format"></param>
        /// <param name="response"></param>
        /// <returns>False for "caller try another codec"; true otherwise</returns>
        bool FetchTexture(OSHttpRequest httpRequest, OSHttpResponse httpResponse, UUID textureID, string format,
                                  out byte[] response)
        {
            //MainConsole.Instance.DebugFormat("[GETTEXTURE]: {0} with requested format {1}", textureID, format);
            AssetBase texture;

            string fullID = textureID.ToString();
            if (format != DefaultFormat)
                fullID = fullID + "-" + format;

            if (!String.IsNullOrEmpty(REDIRECT_URL))
            {
                // Only try to fetch locally cached textures. Misses are redirected
                texture = m_assetService.GetCached(fullID);

                if (texture != null)
                {
                    if (texture.Type != (sbyte) AssetType.Texture &&        // not actually a texture
                        texture.Type != (sbyte) AssetType.Unknown &&        // .. but valid
                        texture.Type != (sbyte) AssetType.Simstate)     
                    {
                        httpResponse.StatusCode = (int) System.Net.HttpStatusCode.NotFound;
                        response = MainServer.BlankResponse;
                        return true;
                    }

                    response = WriteTextureData(httpRequest, httpResponse, texture, format);
                    return true;
                }
                else
                {
                    string textureUrl = REDIRECT_URL + textureID;
                    MainConsole.Instance.Debug("[AssetCAPS]: Redirecting texture request to " + textureUrl);
                    httpResponse.RedirectLocation = textureUrl;
                    response = MainServer.BlankResponse;
                    return true;
                }
            }

            // no redirect
            // try the cache
            texture = m_assetService.GetCached(fullID);

            if (texture == null)
            {
                //MainConsole.Instance.DebugFormat("[GETTEXTURE]: texture was not in the cache");

                // Fetch locally or remotely. Misses return a 404
                texture = m_assetService.Get(textureID.ToString());

                if (texture != null)
				{
					if (texture.Type != (sbyte)AssetType.Texture && 
                        texture.Type != (sbyte)AssetType.Unknown &&
                        texture.Type != (sbyte)AssetType.Simstate)
					{
					    httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
						response = MainServer.BlankResponse;
						return true;
					}
					
                    if (format == DefaultFormat)
					{
						response = WriteTextureData (httpRequest, httpResponse, texture, format);
						return true;
					}
					
                    AssetBase newTexture = new AssetBase (texture.ID + "-" + format, texture.Name, AssetType.Texture,
							                                         texture.CreatorID)
                                                   { Data = ConvertTextureData (texture, format) };
					
                    if (newTexture.Data.Length == 0)            // unable to convert
					{
						response = MainServer.BlankResponse;
						return false; // !!! Caller try another codec, please!
					}

					newTexture.Flags = AssetFlags.Collectable | AssetFlags.Temporary;
					newTexture.ID = m_assetService.Store (newTexture);
					response = WriteTextureData (httpRequest, httpResponse, newTexture, format);
					return true;
				} 

				// nothing found... replace with the 'missing_texture" texture
				// try the cache first
				texture = m_assetService.GetCached(Constants.MISSING_TEXTURE_ID);

				if (texture == null)
					texture = m_assetService.Get (Constants.MISSING_TEXTURE_ID);		// not in local cache...

                if (texture != null)
                {
                    if (format == DefaultFormat)
                    {
                        MainConsole.Instance.Debug ("[AssetCAPS]: Texture " + textureID + " replaced with default 'missing' texture");
                        response = WriteTextureData (httpRequest, httpResponse, texture, format);
                        return true;
                    }
                }

                // texture not found and we have no 'missing texture'??
                // ... or if all else fails...
                MainConsole.Instance.Warn("[AssetCAPS]: Texture " + textureID + " not found (no default)");
                httpResponse.StatusCode = (int) System.Net.HttpStatusCode.NotFound;
                response = MainServer.BlankResponse;
                return true;

            }

            // found the texture in the cache
            if (texture.Type != (sbyte) AssetType.Texture &&
                texture.Type != (sbyte) AssetType.Unknown &&
                texture.Type != (sbyte) AssetType.Simstate)
            {
                httpResponse.StatusCode = (int) System.Net.HttpStatusCode.NotFound;
                response = MainServer.BlankResponse;
                return true;
            }
            
            // the best result...
            //MainConsole.Instance.DebugFormat("[GETTEXTURE]: texture was in the cache");
            response = WriteTextureData(httpRequest, httpResponse, texture, format);
            return true;

        }

        byte[] WriteTextureData(OSHttpRequest request, OSHttpResponse response, AssetBase texture, string format)
        {
            string range = request.Headers.GetOne("Range");
            //MainConsole.Instance.DebugFormat("[GETTEXTURE]: Range {0}", range);
            if (!String.IsNullOrEmpty(range)) // JP2's only
            {
                // Range request
                int start, end;
                if (TryParseRange(range, out start, out end))
                {
                    // Before clamping start make sure we can satisfy it in order to avoid
                    // sending back the last byte instead of an error status
                    if (start >= texture.Data.Length)
                    {
                        response.StatusCode = (int) System.Net.HttpStatusCode.RequestedRangeNotSatisfiable;
                        return MainServer.BlankResponse;
                    }
                    else
                    {
						// Handle the case where portions of the range are missing.
			            if (start == -1)
							start = 0;
						if (end == -1)
							end = int.MaxValue;

                        end = Utils.Clamp(end, 0, texture.Data.Length - 1);
                        start = Utils.Clamp(start, 0, end);
                        int len = end - start + 1;

                        //MainConsole.Instance.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);

                        if (len < texture.Data.Length)
                            response.StatusCode = (int) System.Net.HttpStatusCode.PartialContent;
                        else
                            response.StatusCode = (int) System.Net.HttpStatusCode.OK;

                        response.ContentType = texture.TypeString;
                        response.AddHeader("Content-Range",
                                           String.Format("bytes {0}-{1}/{2}", start, end, texture.Data.Length));
                        byte[] array = new byte[len];
                        Array.Copy(texture.Data, start, array, 0, len);
                        return array;
                    }
                }
               
                MainConsole.Instance.Warn("[AssetCAPS]: Malformed Range header: " + range);
                response.StatusCode = (int) System.Net.HttpStatusCode.BadRequest;
                return MainServer.BlankResponse;
               
            }
 
            // Full content request
            response.StatusCode = (int) System.Net.HttpStatusCode.OK;
            response.ContentType = texture.TypeString;
            if (format == DefaultFormat)
                response.ContentType = texture.TypeString;
            else
                response.ContentType = "image/" + format;
            return texture.Data;

        }

		/*
 	     * <summary>
 		 * Parse a range header.
 		 * </summary>
     	 * <remarks>
    	 * As per http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html,
     	 * this obeys range headers with two values (e.g. 533-4165) and no second value (e.g. 533-).
     	 * Where there is no value, -1 is returned. Also handles a range like (-4165) where -1 is 
     	 * returned for the starting value.</remarks>
     	 * <returns></returns>
     	 * <param name='header'></param>
     	 * <param name='start'>Undefined if the parse fails.</param>
     	 * <param name='end'>Undefined if the parse fails.</param>
     	*/
		bool TryParseRange(string header, out int start, out int end)
        {
			start = end = -1;
      		if (! header.StartsWith("bytes="))
        		return false;

      		string[] rangeValues = header.Substring(6).Split('-');
      		if (rangeValues.Length != 2)
        		return false;

     		if (rangeValues[0] != "")
      		{
        		if (!Int32.TryParse(rangeValues[0], out start))
          		return false;
      		}

      		if (rangeValues[1] != "")
      		{
        		if (!Int32.TryParse(rangeValues[1], out end))
          		return false;
      		}

      		return true;
		}

        byte[] ConvertTextureData(AssetBase texture, string format)
        {
            MainConsole.Instance.DebugFormat("[AssetCAPS]: Converting texture {0} to {1}", texture.ID, format);
            byte[] data = new byte[0];

            MemoryStream imgstream = new MemoryStream();
            Image image = null;

            try
            {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular data
                image = m_j2kDecoder.DecodeToImage(texture.Data);
                if (image == null)
                    return data;
                // Save to bitmap
                image = new Bitmap(image);

                EncoderParameters myEncoderParameters = new EncoderParameters();
                myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

                // Save bitmap to stream
                ImageCodecInfo codec = GetEncoderInfo("image/" + format);
                if (codec != null)
                {
                    image.Save(imgstream, codec, myEncoderParameters);
                    // Write the stream to a byte array for output
                    data = imgstream.ToArray();
                }
                else
                    MainConsole.Instance.WarnFormat("[AssetCAPS]: No such codec {0}", format);
            }
            catch (Exception e)
            {
                MainConsole.Instance.WarnFormat("[AssetCAPS]: Unable to convert texture {0} to {1}: {2}", texture.ID,
                                                format, e.Message);
            }
            finally
            {
                // Reclaim memory, these are unmanaged resources
                // If we encountered an exception, one or more of these will be null

                if (image != null)
                    image.Dispose();

                imgstream.Close();
            }

            return data;
        }

        // From MSDN
        static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
            return encoders.FirstOrDefault(t => t.MimeType == mimeType);
        }

        #endregion

        #region Baked Textures

        public byte[] UploadBakedTexture(string path, Stream request, OSHttpRequest httpRequest,
                                         OSHttpResponse httpResponse)
        {
            try
            {
                //MainConsole.Instance.Debug("[CAPS]: UploadBakedTexture Request in region: " +
                //        m_regionName);

                string uploadpath = "/CAPS/Upload/" + UUID.Random() + "/";
                BakedTextureUploader uploader = new BakedTextureUploader(uploadpath);
                uploader.OnUpLoad += BakedTextureUploaded;

                MainServer.Instance.AddStreamHandler(new GenericStreamHandler("POST", uploadpath,
                                                                    uploader.UploaderCaps));

                string uploaderURL = MainServer.Instance.ServerURI + uploadpath;
                OSDMap map = new OSDMap();
                map["uploader"] = uploaderURL;
                map["state"] = "upload";
                return OSDParser.SerializeLLSDXmlBytes(map);
            }
            catch (Exception e)
            {
                MainConsole.Instance.Error("[AssetCAPS]: " + e);
            }

            return null;
        }

        public delegate void UploadedBakedTexture(byte[] data, out UUID newAssetID);

        public class BakedTextureUploader
        {
            public event UploadedBakedTexture OnUpLoad;
            UploadedBakedTexture handlerUpLoad = null;

            readonly string uploaderPath = String.Empty;

            public BakedTextureUploader(string path)
            {
                uploaderPath = path;
            }

            /// <summary>
            /// </summary>
            /// <param name="path"></param>
            /// <param name="request"></param>
            /// <param name="httpRequest"></param>
            /// <param name="httpResponse"></param>
            /// <returns></returns>
            public byte[] UploaderCaps(string path, Stream request,
                                       OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                handlerUpLoad = OnUpLoad;
                UUID newAssetID;
                handlerUpLoad(HttpServerHandlerHelpers.ReadFully(request), out newAssetID);

                OSDMap map = new OSDMap();
                map["new_asset"] = newAssetID.ToString();
                map["item_id"] = UUID.Zero;
                map["state"] = "complete";
                MainServer.Instance.RemoveStreamHandler("POST", uploaderPath);

                return OSDParser.SerializeLLSDXmlBytes(map);
            }
        }

        public void BakedTextureUploaded(byte[] data, out UUID newAssetID)
        {
            //MainConsole.Instance.InfoFormat("[AssetCAPS]: Received baked texture {0}", assetID);
            AssetBase asset = new AssetBase(UUID.Random(), "Baked Texture", AssetType.Texture, m_AgentID)
                                  {Data = data, Flags = AssetFlags.Deletable | AssetFlags.Temporary};
            newAssetID = asset.ID = m_assetService.Store(asset);
            MainConsole.Instance.DebugFormat("[AssetCAPS]: Baked texture new id {0}", newAssetID);
        }

        public byte[] ProcessGetMesh(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            httpResponse.ContentType = "text/plain";

            string meshStr = string.Empty;


            if (httpRequest.QueryString["mesh_id"] != null)
                meshStr = httpRequest.QueryString["mesh_id"];


            UUID meshID;
            if (!String.IsNullOrEmpty(meshStr) && UUID.TryParse(meshStr, out meshID))
            {
                if (m_assetService == null)
                    return Encoding.UTF8.GetBytes("The asset service is unavailable.  So is your mesh.");

                // Only try to fetch locally cached textures. Misses are redirected
                AssetBase mesh = m_assetService.GetCached(meshID.ToString());
                if (mesh != null)
                {
                    if (mesh.Type == (SByte) AssetType.Mesh)
                    {
                        httpResponse.StatusCode = 200;
                        httpResponse.ContentType = "application/vnd.ll.mesh";
                        return mesh.Data;
                    }

                    // Optionally add additional mesh types here
                    httpResponse.StatusCode = 404; //501; //410; //404;
                    httpResponse.ContentType = "text/plain";
                    return Encoding.UTF8.GetBytes("Unfortunately, this asset isn't a mesh.");
                }
          
                // not in the cache
                mesh = m_assetService.GetMesh(meshID.ToString());
                if (mesh != null)
                {
                    if (mesh.Type == (SByte) AssetType.Mesh)
                    {
                        httpResponse.StatusCode = 200;
                        httpResponse.ContentType = "application/vnd.ll.mesh";
                        return mesh.Data;
                    }

                    // Optionally add additional mesh types here
                    httpResponse.StatusCode = 404; //501; //410; //404;
                    httpResponse.ContentType = "text/plain";
                    return Encoding.UTF8.GetBytes("Unfortunately, this asset isn't a mesh.");
                }

                // not found
                httpResponse.StatusCode = 404; //501; //410; //404;
                httpResponse.ContentType = "text/plain";
                return Encoding.UTF8.GetBytes("Your Mesh wasn't found.  Sorry!");
            }

            // null or not a UUID
            httpResponse.StatusCode = 404;
            return Encoding.UTF8.GetBytes("Invalid mesh");
        }

        #endregion
    }
}
