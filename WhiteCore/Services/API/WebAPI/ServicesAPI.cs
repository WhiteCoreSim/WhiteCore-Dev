/*
 * Copyright (c) Contributors, http://whitecore-sim.org/
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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
	{

        OSDMap GetAvatarArchives (OSDMap map)
        {
            var resp = new OSDMap ();
            var temp = m_registry.RequestModuleInterface<IAvatarAppearanceArchiver> ().GetAvatarArchives ();
            var names = new OSDArray ();
            var snapshot = new OSDArray ();

            MainConsole.Instance.DebugFormat ("[API] {0} avatar archives found", temp.Count);

            foreach (AvatarArchive a in temp) {
                names.Add (OSD.FromString (a.FolderName));
                //names.Add(OSD.FromString(a.FileName));
                snapshot.Add (OSD.FromUUID (a.Snapshot));
            }

            resp ["names"] = names;
            resp ["snapshot"] = snapshot;

            return resp;
        }

        OSDMap GridWideAlert (OSDMap map)
        {
            var resp = new OSDMap ();
            resp ["Finished"] = OSD.FromBoolean (true);

            var messageModule = m_registry.RequestModuleInterface<IGridWideMessageModule> ();
            if (messageModule != null)
                messageModule.SendAlert (map ["Message"].AsString ());

            return resp;
        }

        OSDMap MessageUser (OSDMap map)
        {
            var resp = new OSDMap ();
            resp ["Finished"] = OSD.FromBoolean (true);

            var agentID = map ["UserID"].AsUUID ();

            var messageModule = m_registry.RequestModuleInterface<IGridWideMessageModule> ();
            if (messageModule != null)
                messageModule.MessageUser (agentID, map ["Message"].AsString ());

            return resp;
        }

		#region textures

/*		public byte[] OnHTTPGetTextureImage(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
		{
			if (httpRequest.QueryString.Get("method") != "GridTexture")
				return MainServer.BlankResponse;

			MainConsole.Instance.Debug("[API]: Sending image jpeg");
			byte[] jpeg = new byte[0];
			IAssetService m_AssetService = m_registry.RequestModuleInterface<IAssetService>();
			IJ2KDecoder m_j2kDecoder = m_registry.RequestModuleInterface<IJ2KDecoder>();

			MemoryStream imgstream = new MemoryStream();
			Image mapTexture = null;

			try
			{
				// Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data
				AssetBase mapasset = m_AssetService.Get(httpRequest.QueryString.Get("uuid"));

				// Decode image to System.Drawing.Image
				mapTexture = m_j2kDecoder.DecodeToImage(mapasset.Data);
				if (mapTexture == null)
					return jpeg;
				// Save to bitmap

				mapTexture = ResizeBitmap(mapTexture, 128, 128);
				EncoderParameters myEncoderParameters = new EncoderParameters();
				myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 75L);

				// Save bitmap to stream
				mapTexture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);



				// Write the stream to a byte array for output
				jpeg = imgstream.ToArray();
			}
			catch (Exception)
			{
				// Dummy!
				MainConsole.Instance.Warn("[API]: Unable to post image.");
			}
			finally
			{
				// Reclaim memory, these are unmanaged resources
				// If we encountered an exception, one or more of these will be null
				if (mapTexture != null)
					mapTexture.Dispose();

				if (imgstream != null)
				{
					imgstream.Close();
					imgstream.Dispose();
				}
			}

			httpResponse.ContentType = "image/jpeg";
			return jpeg;
		}

		Bitmap ResizeBitmap(Image b, int nWidth, int nHeight)
		{
			Bitmap newsize = new Bitmap(nWidth, nHeight);
			Graphics temp = Graphics.FromImage(newsize);
			temp.DrawImage(b, 0, 0, nWidth, nHeight);
			temp.SmoothingMode = SmoothingMode.AntiAlias;
			temp.DrawString(m_gridnick, new Font("Arial", 8, FontStyle.Regular), new SolidBrush(Color.FromArgb(90, 255, 255, 50)), new Point(2, 115));

			return newsize;
		}

		// From msdn
		static ImageCodecInfo GetEncoderInfo(String mimeType)
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
		#endregion

	}
}