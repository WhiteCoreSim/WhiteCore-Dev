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
using OpenMetaverse;
using OpenMetaverse.Imaging;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services.ClassHelpers.Assets;

namespace WhiteCore.ScriptEngine.DotNetEngine.MiniModule
{
    class Graphics : MarshalByRefObject, IGraphics
    {
        readonly IScene m_scene;

        public Graphics (IScene m_scene)
        {
            this.m_scene = m_scene;
        }

        #region IGraphics Members

        public UUID SaveBitmap (Bitmap data)
        {
            return SaveBitmap (data, false, true);
        }

        public UUID SaveBitmap (Bitmap data, bool lossless, bool temporary)
        {
            AssetBase asset = new AssetBase (
                UUID.Random (),
                "MRMDynamicImage",
                AssetType.Texture,
                m_scene.RegionInfo.RegionID) {
                Data = OpenJPEG.EncodeFromImage (data, lossless),
                Description = "MRM Image",
                Flags = (temporary) ? AssetFlags.Temporary : 0
            };

            var assetID = m_scene.AssetService.Store (asset);
            asset.Dispose ();
            return assetID;
        }

        public Bitmap LoadBitmap (UUID assetID)
        {
            // from AssetCaps
            const string MISSING_TEXTURE_ID = "41fcdbb9-0896-495d-8889-1eb6fad88da3";       // texture to use when all else fails...

            byte[] bmp = m_scene.AssetService.GetData (assetID.ToString ());
            if (bmp == null)
                bmp = m_scene.AssetService.GetData (MISSING_TEXTURE_ID);

            if (bmp == null)    // something reqlly wrong here
                return null;

            Image img = m_scene.RequestModuleInterface<IJ2KDecoder> ().DecodeToImage (bmp);
            if (img == null)
                return null;
            
            var retbmp = new Bitmap (img);
            img.Dispose ();

            return retbmp;
        }

        #endregion
    }
}
