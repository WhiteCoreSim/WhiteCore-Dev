﻿/*
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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;


namespace WhiteCore.Modules.WorldShader
{
    public class WorldShader : INonSharedRegionModule
    {
        readonly Dictionary<UUID, UUID> m_previouslyConverted = new Dictionary<UUID, UUID> ();
        readonly Dictionary<UUID, UUID> m_revertConverted = new Dictionary<UUID, UUID> ();
        bool initialized;

        #region ISharedRegionModule Members

        public void Initialise (IConfigSource source)
        {
        }

        public void AddRegion (IScene scene)
        {
        }

        public void RegionLoaded (IScene scene)
        {
            if (MainConsole.Instance != null && !initialized) {
                MainConsole.Instance.Commands.AddCommand (
                    "revert shade world",
                    "revert shade world",
                    "Reverts the shading of the world",
                    RevertShadeWorld, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "shade world",
                    "shade world",
                    "Shades the world with a color",
                    ShadeWorld, true, false);
            }
            initialized = true;
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public void Close ()
        {
        }

        public string Name {
            get { return "WorldShader"; }
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        #endregion

        public void RevertShadeWorld (IScene scene, string [] cmd)
        {
            ISceneEntity [] entities = scene.Entities.GetEntities ();
            foreach (ISceneEntity entity in entities) {
                foreach (ISceneChildEntity child in entity.ChildrenEntities ()) {
                    UUID [] textures = GetTextures (child.Shape.Textures);
                    foreach (UUID t in textures) {
                        UUID oldID = t;
                        AssetBase oldAsset = null;
                        while (m_revertConverted.ContainsKey (oldID)) {
                            child.Shape.Textures = SetTexture (child.Shape, m_revertConverted [oldID], oldID);
                            oldID = m_revertConverted [oldID];
                        }

                        UUID newID;
                        while ((oldAsset = scene.AssetService.Get (oldID.ToString ())) != null && UUID.TryParse (oldAsset.Description, out newID)) {
                            child.Shape.Textures = SetTexture (child.Shape, newID, oldID);
                        }
                        if (oldAsset != null)
                            oldAsset.Dispose ();
                    }
                }
            }
            m_revertConverted.Clear ();
            m_previouslyConverted.Clear ();
        }

        public void ShadeWorld (IScene scene, string [] cmd)
        {
            bool greyScale = MainConsole.Instance.Prompt ("Greyscale (yes or no)?").ToLower () == "yes";
            int R = 0;
            int G = 0;
            int B = 0;
            float percent = 0;
            if (!greyScale) {
                R = int.Parse (MainConsole.Instance.Prompt ("R color (0 - 255)"));
                G = int.Parse (MainConsole.Instance.Prompt ("G color (0 - 255)"));
                B = int.Parse (MainConsole.Instance.Prompt ("B color (0 - 255)"));
                percent = float.Parse (MainConsole.Instance.Prompt ("Percent to merge in the shade (0 - 100)"));
            }
            if (percent > 1)
                percent /= 100;
            Color shader = Color.FromArgb (R, G, B);

            IJ2KDecoder j2kDecoder = scene.RequestModuleInterface<IJ2KDecoder> ();
            ISceneEntity [] entities = scene.Entities.GetEntities ();
            foreach (ISceneEntity entity in entities) {
                foreach (ISceneChildEntity child in entity.ChildrenEntities ()) {
                    UUID [] textures = GetTextures (child.Shape.Textures);
                    foreach (UUID t in textures) {
                        if (m_previouslyConverted.ContainsKey (t)) {
                            child.Shape.Textures = SetTexture (child.Shape, m_previouslyConverted [t], t);
                        } else {
                            AssetBase asset = scene.AssetService.Get (t.ToString ());
                            if (asset != null) {
                                Bitmap texture = null;
                                try {
                                    texture = (Bitmap)j2kDecoder.DecodeToImage (asset.Data);
                                } catch {
                                }
                                if (texture == null) {
                                    asset.Dispose ();
                                    continue;
                                }

                                asset.ID = UUID.Random ();
                                try {
                                    texture = Shade (texture, shader, percent, greyScale);
                                } catch {
                                    asset.Dispose ();
                                    continue;   // cannot convert this one...
                                }

                                asset.Data = OpenJPEG.EncodeFromImage (texture, false);
                                asset.Description = t.ToString ();
                                texture.Dispose ();
                                asset.ID = scene.AssetService.Store (asset);
                                child.Shape.Textures = SetTexture (child.Shape, asset.ID, t);
                                m_previouslyConverted.Add (t, asset.ID);
                                m_revertConverted.Add (asset.ID, t);

                                asset.Dispose ();
                            }
                        }
                    }
                }
            }
            MainConsole.Instance.Warn ("[Shader]: WARNING: You may not be able to revert this once you restart the instance");
        }

        Primitive.TextureEntry SetTexture (PrimitiveBaseShape shape, UUID newID, UUID oldID)
        {
            Primitive.TextureEntry oldShape = shape.Textures;
            Primitive.TextureEntry newShape;
            newShape = shape.Textures.DefaultTexture.TextureID == oldID
                           ? Copy (shape.Textures, newID)
                           : Copy (shape.Textures, shape.Textures.DefaultTexture.TextureID);

            int i = 0;
            foreach (Primitive.TextureEntryFace face in shape.Textures.FaceTextures) {
                if (face != null)
                    if (face.TextureID == oldID) {
                        Primitive.TextureEntryFace f = newShape.CreateFace ((uint)i);
                        CopyFace (oldShape.FaceTextures [i], f);
                        f.TextureID = newID;
                        newShape.FaceTextures [i] = f;
                    } else {
                        Primitive.TextureEntryFace f = newShape.CreateFace ((uint)i);
                        CopyFace (oldShape.FaceTextures [i], f);
                        f.TextureID = oldShape.FaceTextures [i].TextureID;
                        newShape.FaceTextures [i] = f;
                    }
                i++;
            }

            return newShape;
        }

        Primitive.TextureEntry Copy (Primitive.TextureEntry c, UUID id)
        {
            Primitive.TextureEntry Textures = new Primitive.TextureEntry (id);
            Textures.DefaultTexture = CopyFace (c.DefaultTexture, Textures.DefaultTexture);
            //for(int i = 0; i < c.FaceTextures.Length; i++)
            //{
            //    Textures.FaceTextures[i] = c.FaceTextures[i];
            //}
            return Textures;
        }

        Primitive.TextureEntryFace CopyFace (Primitive.TextureEntryFace old, Primitive.TextureEntryFace face)
        {
            face.Bump = old.Bump;
            face.Fullbright = old.Fullbright;
            face.Glow = old.Glow;
            face.MediaFlags = old.MediaFlags;
            face.OffsetU = old.OffsetU;
            face.OffsetV = old.OffsetV;
            face.RepeatU = old.RepeatU;
            face.RepeatV = old.RepeatV;
            face.RGBA = old.RGBA;
            face.Rotation = old.Rotation;
            face.Shiny = old.Shiny;
            face.TexMapType = old.TexMapType;

            return face;
        }

        UUID [] GetTextures (Primitive.TextureEntry textureEntry)
        {
            List<UUID> textures =
                (from face in textureEntry.FaceTextures where face != null select face.TextureID).ToList ();
            textures.Add (textureEntry.DefaultTexture.TextureID);
            return textures.ToArray ();
        }

        public Bitmap Shade (Bitmap source, Color shade, float percent, bool greyScale)
        {
            FastBitmap bmp = new FastBitmap (source);
            bmp.LockBitmap ();
            for (int y = 0; y < source.Height; y++) {
                for (int x = 0; x < source.Width; x++) {
                    Color c = bmp.GetPixel (x, y);
                    if (greyScale) {
                        int luma = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                        bmp.SetPixel (x, y, Color.FromArgb (c.A, luma, luma, luma));
                    } else {
                        float amtFrom = 1 - percent;
                        int lumaR = (int)(c.R * amtFrom + shade.R * percent);
                        int lumaG = (int)(c.G * amtFrom + shade.G * percent);
                        int lumaB = (int)(c.B * amtFrom + shade.B * percent);
                        bmp.SetPixel (x, y, Color.FromArgb (c.A, lumaR, lumaG, lumaB));
                    }
                }
            }
            bmp.UnlockBitmap ();
            return bmp.Bitmap ();
        }
    }
}
