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
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Framework.ClientInterfaces
{
    /// <summary>
    ///     Contains the Avatar's Appearance and methods to manipulate the appearance.
    /// </summary>
    public sealed class AvatarAppearance : IDataTransferable
    {
        public static readonly int VISUALPARAM_COUNT = 218;
        public static readonly int TEXTURE_COUNT = 21;
        public static readonly byte[] BAKE_INDICES = new byte[] {8, 9, 10, 11, 19, 20};
        
        int m_serial = 1;
        byte[] m_visualparams;
        Primitive.TextureEntry m_texture;
        AvatarWearable[] m_wearables;
        Dictionary<int, List<AvatarAttachment>> m_attachments;        
        float m_avatarHeight = 0;
        UUID m_owner;
        Dictionary<string, UUID> m_wearableCache = new Dictionary<string, UUID>();

        public int Serial
        {
            get { return m_serial; }
            set { m_serial = value; }
        }

        public byte[] VisualParams
        {
            get { return m_visualparams; }
            set { m_visualparams = value; }
        }

        public Primitive.TextureEntry Texture
        {
            get { return m_texture; }
            set { m_texture = value; }
        }

        public AvatarWearable[] Wearables
        {
            get { return m_wearables; }
            set { m_wearables = value; }
        }

        public float AvatarHeight
        {
            get { return m_avatarHeight; }
            set { m_avatarHeight = value; }
        }

        public Dictionary<string, UUID> WearableCache
        {
            get { return m_wearableCache; }
        }

        public UUID Owner
        {
            get { return m_owner; }
            set { m_owner = value; }
        }

        static readonly object _attachmentslock = new object ();
        public Dictionary<int, List<AvatarAttachment>> Attachments
        {
            get { lock (_attachmentslock) { return m_attachments; }}
            set { lock (_attachmentslock) { m_attachments = value; }}
        }

        public AvatarAppearance() : this(UUID.Zero)
        {
        }

        public AvatarAppearance(UUID owner)
        {
            m_serial = 1;
            m_owner = owner;

            SetDefaultWearables();
            SetDefaultTexture();
            SetDefaultParams();
            SetHeight();
            m_attachments = new Dictionary<int, List<AvatarAttachment>>();
        }

        public AvatarAppearance(UUID avatarID, OSDMap map)
        {
            m_owner = avatarID;
            Unpack(map);
        }

        public AvatarAppearance(UUID avatarID, AvatarWearable[] wearables, Primitive.TextureEntry textureEntry,
                                byte[] visualParams)
        {
            //            MainConsole.Instance.WarnFormat("[AVATAR APPEARANCE] create initialized appearance for {0}",avatarID);

            m_serial = 1;
            m_owner = avatarID;

            if (wearables != null)
                m_wearables = wearables;
            else
                SetDefaultWearables();

            if (textureEntry != null)
                m_texture = textureEntry;
            else
                SetDefaultTexture();

            if (visualParams != null)
                m_visualparams = visualParams;
            else
                SetDefaultParams();

            SetHeight();

            m_attachments = new Dictionary<int, List<AvatarAttachment>>();
        }

        public AvatarAppearance(AvatarAppearance appearance)
            : this(appearance, true)
        {
        }

        public AvatarAppearance(AvatarAppearance appearance, bool copyWearables)
        {
            //            MainConsole.Instance.WarnFormat("[AVATAR APPEARANCE] create from an existing appearance");

            if (appearance == null)
            {
                m_serial = 1;
                m_owner = UUID.Zero;

                SetDefaultWearables();
                SetDefaultTexture();
                SetDefaultParams();
                SetHeight();

                m_attachments = new Dictionary<int, List<AvatarAttachment>>();

                return;
            }

            m_serial = appearance.Serial;
            m_owner = appearance.Owner;

            m_wearables = new AvatarWearable[AvatarWearable.MAX_WEARABLES];
            for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                m_wearables[i] = new AvatarWearable();
            if (copyWearables && (appearance.Wearables != null))
            {
                for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                    SetWearable(i, appearance.Wearables[i]);
            }

            m_texture = null;
            if (appearance.Texture != null)
            {
                byte[] tbytes = appearance.Texture.GetBytes();
                m_texture = new Primitive.TextureEntry(tbytes, 0, tbytes.Length);
            }

            m_visualparams = null;
            if (appearance.VisualParams != null)
                m_visualparams = (byte [])appearance.VisualParams.Clone ();
            else
                SetDefaultParams ();    // we need something to work with
            
            SetHeight ();

            // Copy the attachment, force append mode since that ensures consistency
            m_attachments = new Dictionary<int, List<AvatarAttachment>>();
            foreach (AvatarAttachment attachment in appearance.GetAttachments())
                AppendAttachment(new AvatarAttachment(attachment));
        }

        public void GetAssetsFrom(AvatarAppearance app)
        {
            for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
            {
                for (int j = 0; j < m_wearables[i].Count; j++)
                {
                    UUID itemID = m_wearables[i][j].ItemID;
                    UUID assetID = app.Wearables[i].GetAsset(itemID);
                    if (assetID != UUID.Zero)
                        m_wearables[i].Add(itemID, assetID);
                }
            }
        }

        public void ClearWearables()
        {
            m_wearables = new AvatarWearable[AvatarWearable.MAX_WEARABLES];
            for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                m_wearables[i] = new AvatarWearable();
        }

        public void SetDefaultWearables()
        {
            m_wearables = AvatarWearable.DefaultWearables;
        }
        
        public void ResetAppearance()
        {
        	m_serial = 1;
            SetDefaultTexture();
        }

        void SetDefaultParams()
        {
            // Initial parameters for Ruth
            // Superseded by the new Avatar details
        	/*
            m_visualparams = new byte[] {
        		33, 61, 85, 23, 58, 127, 63, 85, 63, 42, 0, 85, 63, 36, 85, 95, 153, 63, 34, 0, 63,
        		109, 88, 132, 63, 136, 81, 85, 103, 136, 127, 0, 150, 150, 150, 127, 0, 0, 0, 0, 0,
        		127, 0, 0, 255, 127, 114, 127, 99, 63, 127, 140, 127, 127, 0, 0, 0, 191, 0, 104, 0,
        		0, 0, 0, 0, 0, 0, 0, 0, 145, 216, 133, 0, 127, 0, 127, 170, 0, 0, 127, 127, 109, 85,
        		127, 127, 63, 85, 42, 150, 150, 150, 150, 150, 150, 150, 25, 150, 150, 150, 0, 127, 0, 
        		0, 144, 85, 127, 132, 127, 85, 0, 127, 127, 127, 127, 127, 127, 59, 127, 85, 127, 127,
        		106, 47, 79, 127, 127, 204, 2, 141, 66, 0, 0, 127, 127, 0, 0, 0, 0, 127, 0, 159, 0,
        		0, 178, 127, 36, 85, 131, 127, 127, 127, 153, 95, 0, 140, 75, 27, 127, 127, 0, 150,
        		150, 198, 0, 0, 63, 30, 127, 165, 209, 198, 127, 127, 153, 204, 51, 51, 255, 255, 255,
        		204, 0, 255, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 0, 150, 150, 150, 150,
        		150, 0, 127, 127, 150, 150, 150, 150, 150, 150, 150, 150, 0, 0, 150, 51, 132, 150, 150,
        		150
        	};
        	 */
            // female params
            m_visualparams = new byte[] {
                33, 37, 56, 62, 25, 204, 12, 114, 66, 25, 37, 178, 102, 53, 139, 38, 114, 30, 0, 127,
                102, 122, 76, 66, 63, 76, 38, 63, 122, 102, 158, 0, 203, 255, 0, 127, 0, 0, 255, 0, 28,
                255, 255, 0, 0, 0, 132, 0, 96, 0, 226, 181, 35, 127, 153, 0, 0, 206, 0, 23, 0, 0, 
                0, 0, 0, 0, 0, 0, 0, 145, 204, 178, 0, 43, 0, 165, 91, 0, 0, 102, 76, 76, 85,
                127, 127, 25, 90, 152, 100, 216, 214, 255, 255, 135, 255, 25, 89, 76, 204, 0, 127,
                73, 0, 147, 139, 142, 125, 114, 117, 0, 127, 193, 132, 127, 132, 122, 59, 63, 81, 127,
                147, 183, 76, 79, 81, 127, 249, 63, 0, 0, 0, 0, 127, 127, 0, 0, 0, 0, 127, 0, 159, 0, 0,
                0, 127, 83, 68, 131, 107, 158, 112, 193, 183, 0, 107, 142, 0, 130, 147, 0, 214, 255,
                198, 0, 0, 96, 30, 122, 165, 209, 198, 127, 127, 153, 255, 255, 255, 255, 255, 255, 255,
                0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 255, 255, 255, 255, 255, 
                0, 132, 153, 255, 25, 100, 255, 255, 255, 255, 84, 0, 0, 0, 51, 91, 255, 255, 255, 0,
                0, 25, 0, 25, 23, 51, 0, 25, 23, 51, 0, 0, 25, 0, 25, 23, 51, 0, 0, 25, 0, 25, 23, 51,
                0, 25, 23, 51, 0, 25, 23, 51, 0, 127
            };
        }

        void SetDefaultTexture()
        {
            m_texture = new Primitive.TextureEntry(new UUID("C228D1CF-4B5D-4BA8-84F4-899A0796AA97"));
            for (uint i = 0; i < TEXTURE_COUNT; i++)
                m_texture.CreateFace(i).TextureID = new UUID(AppearanceManager.DEFAULT_AVATAR_TEXTURE);
        }

        /// <summary>
        ///     Set up appearance textures.
        ///     Returns Boolean that indicates whether the new entries actually change the
        ///     existing values.
        /// </summary>
        public bool SetTextureEntries(Primitive.TextureEntry textureEntry, out List<UUID> ChangedTextures)
        {
            ChangedTextures = new List<UUID>();
            if (textureEntry == null)
                return false;

            // There are much simpler versions of this copy that could be
            // made. We determine if any of the textures actually
            // changed to know if the appearance should be saved later
            bool changed = false;
            for (uint i = 0; i < TEXTURE_COUNT; i++)
            {
                Primitive.TextureEntryFace newface = textureEntry.FaceTextures[i];
                Primitive.TextureEntryFace oldface = m_texture.FaceTextures[i];

                if (newface == null)
                {
                    if (oldface == null) continue;
                }
                else
                {
                    if (oldface != null && oldface.TextureID == newface.TextureID) continue;
                }

                //Ignore the 0 based one... as its always null and causes issues
                if (i > 0)
                {
                    changed = true;
                    if (oldface != null)
                        if (!ChangedTextures.Contains(oldface.TextureID))
                            ChangedTextures.Add(oldface.TextureID);

                    //                if (newface != null)
                    //                    MainConsole.Instance.WarnFormat("[AVATAR APPEARANCE]: index {0}, new texture id {1}",i,newface.TextureID);
                }
            }

            m_texture = textureEntry;
            return changed;
        }

        /// <summary>
        ///     Set up visual parameters for the avatar and refresh the avatar height
        ///     Returns Boolean that indicates whether the new entries actually change the
        ///     existing values.
        /// </summary>
        public bool SetVisualParams(byte[] visualParams)
        {
            if (visualParams == null)
                return false;

            // There are much simpler versions of this copy that could be
            // made. We determine if any of the visual parameters actually
            // changed to know if the appearance should be saved later
            bool changed = false;
            
            int newsize = visualParams.Length;
            
            if (newsize != m_visualparams.Length)
            {
                changed = true;
                m_visualparams = (byte[])visualParams.Clone();
            }
            else
            {
            	for (int i = 0; i < newsize; i++)
                {
                    if (visualParams[i] != m_visualparams[i])
                    {
                        m_visualparams[i] = visualParams[i];
                        changed = true;
                    }
                }
            }

            // Reset the height if the visual parameters actually changed
            if (changed)
                SetHeight();

            return changed;
        }

        public void SetAppearance(Primitive.TextureEntry textureEntry, byte[] visualParams)
        {
            List<UUID> Changed = new List<UUID>();
            SetTextureEntries(textureEntry, out Changed);
            SetVisualParams(visualParams);
        }

        public void SetHeight()
        {
            m_avatarHeight = 1.26077f // Shortest possible avatar height
                             + 0.506945f*m_visualparams[(int) VPElement.SHAPE_HEIGHT]/255.0f // Body height
                             + 0.072514f*m_visualparams[(int) VPElement.SHAPE_HEAD_SIZE]/255.0f // Head size
                             + 0.3836f*m_visualparams[(int) VPElement.SHAPE_LEG_LENGTH]/255.0f // Leg length
                             + 0.08f*m_visualparams[(int) VPElement.SHOES_PLATFORM_HEIGHT]/255.0f
                             // Shoe platform height
                             + 0.07f*m_visualparams[(int) VPElement.SHOES_HEEL_HEIGHT]/255.0f // Shoe heel height
                             + 0.076f*m_visualparams[(int) VPElement.SHAPE_NECK_LENGTH]/255.0f; // Neck length
        }

        public void SetWearable(int wearableId, AvatarWearable wearable)
        {
            m_wearables[wearableId].Clear();
            for (int i = 0; i < wearable.Count; i++)
                m_wearables[wearableId].Add(wearable[i].ItemID, wearable[i].AssetID);
        }

        // DEBUG ON
        public override string ToString()
        {
            string s = "";

            s += string.Format("Serial: {0}\n", m_serial);

            for (uint i = 0; i < TEXTURE_COUNT; i++)
                if (m_texture.FaceTextures[i] != null)
                    s += string.Format("Texture: {0} --> {1}\n", i, m_texture.FaceTextures[i].TextureID);

            foreach (AvatarWearable awear in m_wearables)
            {
                for (int i = 0; i < awear.Count; i++)
                    s += string.Format("Wearable: item={0}, asset={1}\n", awear[i].ItemID, awear[i].AssetID);
            }

            s += "Visual Params: ";
            for (uint j = 0; j < m_visualparams.Length; j++)
                s += string.Format("{0},", m_visualparams[j]);
            s += "\n";

            return s;
        }

        // DEBUG OFF

        /// <summary>
        ///     Get a list of the attachments, note that there may be
        ///     duplicate attach points
        /// </summary>
        public List<AvatarAttachment> GetAttachments()
        {
            lock (_attachmentslock)
                return (from kvp in m_attachments from attach in kvp.Value select new AvatarAttachment(attach)).ToList();
        }

        /// <summary>
        ///     Get a list of the attachments, note that there may be
        ///     duplicate attach points
        /// </summary>
        public Dictionary<int, List<AvatarAttachment>> GetAttachmentsDictionary()
        {
            lock (_attachmentslock)
                return new Dictionary<int, List<AvatarAttachment>>(m_attachments);
        }

        internal void AppendAttachment(AvatarAttachment attach)
        {
            lock (_attachmentslock)
            {
                if (!m_attachments.ContainsKey(attach.AttachPoint))
                    m_attachments[attach.AttachPoint] = new List<AvatarAttachment>();

                // 21/07/2014 Added to prevent Attachments to be added more then once
                foreach (AvatarAttachment prev in m_attachments[attach.AttachPoint])
                {
                    if (prev.ItemID == attach.ItemID)
                        return;
                }

                m_attachments[attach.AttachPoint].Add(attach);
            }
        }

        /// <summary>
        ///     Replace the attachment
        /// </summary>
        /// <param name="attach"></param>
        /// <returns>Whether attachments changed</returns>
        internal bool ReplaceAttachment(AvatarAttachment attach)
        {
            lock (_attachmentslock)
            {
                bool result = true;
                if (m_attachments.ContainsKey(attach.AttachPoint))
                {
                    if (m_attachments[attach.AttachPoint].Contains(attach))
                        result = false;
                }
                m_attachments[attach.AttachPoint] = new List<AvatarAttachment> {attach};
                return result;
            }
        }

        /// <summary>
        ///     DEPRECATED: USE SetAttachments now
        ///     Add an attachment, if the attach point has the
        ///     0x80 bit set then we assume this is an append
        ///     operation otherwise we replace whatever is
        ///     currently attached at the attach point
        /// </summary>
        public bool SetAttachment(int attachpoint, UUID item, UUID asset)
        {
            if (attachpoint == 0)
                return false;

            if (item == UUID.Zero)
            {
                lock (_attachmentslock)
                {
                    if (m_attachments.ContainsKey(attachpoint))
                        m_attachments.Remove(attachpoint);
                }
                return true;
            }

            // check if this is an append or a replace, 0x80 marks it as an append
            if ((attachpoint & 0x80) > 0)
            {
                // strip the append bit
                int point = attachpoint & 0x7F;
                AppendAttachment(new AvatarAttachment(point, item, asset));
                //It got added, so it changed
                return true;
            }
            else
            {
                AppendAttachment(new AvatarAttachment(attachpoint, item, asset));
                return true;
            }
        }

        /// <summary>
        ///     Returns whether an attachment will require a save to the avatar service
        /// </summary>
        /// <param name="attPnt"></param>
        /// <param name="item"></param>
        /// <param name="assetID"></param>
        /// <returns></returns>
        public bool CheckWhetherAttachmentChanged(int attPnt, UUID item, UUID assetID)
        {
            if ((attPnt & 0x80) > 0)
                return true;
            lock (_attachmentslock)
            {
                if (m_attachments.ContainsKey(attPnt))
                {
                    foreach (AvatarAttachment a in m_attachments[attPnt].Where(a => a.ItemID == item))
                    {
                        return !(a.AssetID == assetID);
                    }
                }
            }
            return true;
        }

        public void SetAttachments(ISceneEntity[] attachments)
        {
            ClearAttachments();
            foreach (ISceneEntity e in attachments)
            {
                if (e.RootChild.FromUserInventoryItemID == UUID.Zero)
                    continue;
                AvatarAttachment a = new AvatarAttachment(e.GetAttachmentPoint(), e.RootChild.FromUserInventoryItemID,
                                                          e.RootChild.FromUserInventoryAssetID);
                lock (_attachmentslock)
                {
                    if (!m_attachments.ContainsKey(e.GetAttachmentPoint()))
                        m_attachments.Add(e.GetAttachmentPoint(), new List<AvatarAttachment>());
                    m_attachments[e.GetAttachmentPoint()].Add(a);
                }
            }
        }

        public int GetAttachpoint(UUID itemID)
        {
            lock (_attachmentslock)
            {
                return (m_attachments.Select(
                    kvp =>
                    new {kvp, index = kvp.Value.FindIndex(delegate(AvatarAttachment a) { return a.ItemID == itemID; })})
                                     .
                                      Where(@t => @t.index >= 0).Select(@t => @t.kvp.Key)).FirstOrDefault();
            }
        }

        public bool DetachAttachment(UUID itemID)
        {
            lock (_attachmentslock)
            {
                foreach (KeyValuePair<int, List<AvatarAttachment>> kvp in m_attachments)
                {
                    int index = kvp.Value.FindIndex(a => a.ItemID == itemID);

                    if (index >= 0)
                    {
                        // Remove it from the list of attachments at that attach point
                        m_attachments[kvp.Key].RemoveAt(index);

                        // And remove the list if there are no more attachments here
                        if (m_attachments[kvp.Key].Count == 0)
                            m_attachments.Remove(kvp.Key);
                        return true;
                    }
                }
                return false;
            }
        }

        public void ClearAttachments()
        {
            lock (_attachmentslock)
                m_attachments.Clear();
        }

        public void SetCachedWearables(PresenceInfo.WearableCache[] wearables)
        {
            if (wearables.Length == 0)
                return;
            m_wearableCache.Clear();
            foreach (var w in wearables)
                m_wearableCache.Add(w.TextureIndex.ToString(), w.CacheID);
        }

        #region Packing Functions

        public override OSDMap ToOSD()
        {
            return Pack();
        }

        /// <summary>
        ///     Create an OSDMap from the appearance data
        /// </summary>
        public OSDMap Pack()
        {
            OSDMap data = new OSDMap();

            data["serial"] = OSD.FromInteger(m_serial);
            data["owner"] = Owner;
            data["height"] = OSD.FromReal(m_avatarHeight);

            // Wearables
            OSDArray wears = new OSDArray(AvatarWearable.MAX_WEARABLES);
            for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                wears.Add(m_wearables[i].ToOSD());
            data["wearables"] = wears;

            // Avatar Textures
            OSDArray textures = new OSDArray(TEXTURE_COUNT);
            for (uint i = 0; i < TEXTURE_COUNT; i++)
            {
                textures.Add(m_texture.FaceTextures[i] != null
                                 ? OSD.FromUUID(m_texture.FaceTextures[i].TextureID)
                                 : OSD.FromUUID(AppearanceManager.DEFAULT_AVATAR_TEXTURE));
            }
            data["textures"] = textures;

            // Visual Parameters
            OSDBinary visualparams = new OSDBinary(m_visualparams);
            data["visualparams"] = visualparams;

            // Attachments
            int attachCount;
            lock (_attachmentslock) {
                attachCount = m_attachments.Count;
            }
            OSDArray attachs = new OSDArray (attachCount);
            foreach (AvatarAttachment attach in GetAttachments ())
                attachs.Add (attach.Pack ());
            data ["attachments"] = attachs;

            data["wearableCache"] = m_wearableCache.ToOSDMap();

            return data;
        }

        public override void FromOSD(OSDMap map)
        {
            Unpack(map);
        }

        /// <summary>
        ///     Unpack and OSDMap and initialize the appearance
        ///     from it
        /// </summary>
        public void Unpack(OSDMap data)
        {
            if ((data != null) && (data["serial"] != null))
                m_serial = data["serial"].AsInteger();
            if ((data != null) && (data["height"] != null))
                m_avatarHeight = (float) data["height"].AsReal();

            if ((data != null) && (data["owner"] != null))
                m_owner = data["owner"].AsUUID();
            try
            {
                // Wearables
                SetDefaultWearables();
                if ((data != null) && (data["wearables"] != null) && (data["wearables"]).Type == OSDType.Array)
                {
                    OSDArray wears = (OSDArray) (data["wearables"]);
                    for (int i = 0; i < wears.Count; i++)
                        if(wears[i] is OSDArray)
                            m_wearables[i] = new AvatarWearable((OSDArray) wears[i]);
                }
                else
                {
                    MainConsole.Instance.Warn("[AVATAR APPEARANCE]: failed to unpack wearables");
                }

                // Avatar Textures
                SetDefaultTexture();
                if ((data != null) && (data["textures"] != null) && (data["textures"]).Type == OSDType.Array)
                {
                    OSDArray textures = (OSDArray) (data["textures"]);
                    for (int i = 0; i < TEXTURE_COUNT && i < textures.Count; i++)
                    {
                        UUID textureID = AppearanceManager.DEFAULT_AVATAR_TEXTURE;
                        if (textures[i] != null)
                            textureID = textures[i].AsUUID();
                        if (textureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                            m_texture.CreateFace((uint) i).TextureID = new UUID(textureID);
                    }
                }
                else
                {
                    MainConsole.Instance.Warn("[AVATAR APPEARANCE]: failed to unpack textures");
                }

                // Visual Parameters
                SetDefaultParams();
                if ((data != null) && (data["visualparams"] != null))
                {
                    if ((data["visualparams"].Type == OSDType.Binary) || (data["visualparams"].Type == OSDType.Array))
                        m_visualparams = data["visualparams"].AsBinary();
                }
                else
                {
                    MainConsole.Instance.Warn("[AVATAR APPEARANCE]: failed to unpack visual parameters");
                }

                // Attachments
                m_attachments = new Dictionary<int, List<AvatarAttachment>>();
                if ((data != null) && (data["attachments"] != null) && (data["attachments"]).Type == OSDType.Array)
                {
                    OSDArray attachs = (OSDArray) (data["attachments"]);
                    foreach (OSD t in attachs)
                        AppendAttachment(new AvatarAttachment((OSDMap) t));
                }
                if (data != null && data["wearableCache"] != null && data["wearableCache"] is OSDMap)
                    m_wearableCache = ((OSDMap)data["wearableCache"]).ConvertMap<UUID>((o) => o);
                SetHeight();
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[AVATAR APPEARANCE]: unpack failed badly: {0}, {1}", e.ToString(), OSDParser.SerializeJsonString(data));
            }
        }

        #endregion

        #region VPElement

        /// <summary>
        ///     Viewer Params Array Element for AgentSetAppearance
        ///     Generated from LibOMV's Visual Params list
        /// </summary>
        public enum VPElement
        {
            /// <summary>
            ///     Brow Size - Small 0--+255 Large
            /// </summary>
            SHAPE_BIG_BROW = 0,

            /// <summary>
            ///     Nose Size - Small 0--+255 Large
            /// </summary>
            SHAPE_NOSE_BIG_OUT = 1,

            /// <summary>
            ///     Nostril Width - Narrow 0--+255 Broad
            /// </summary>
            SHAPE_BROAD_NOSTRILS = 2,

            /// <summary>
            ///     Chin Cleft - Round 0--+255 Cleft
            /// </summary>
            SHAPE_CLEFT_CHIN = 3,

            /// <summary>
            ///     Nose Tip Shape - Pointy 0--+255 Bulbous
            /// </summary>
            SHAPE_BULBOUS_NOSE_TIP = 4,

            /// <summary>
            ///     Chin Angle - Chin Out 0--+255 Chin In
            /// </summary>
            SHAPE_WEAK_CHIN = 5,

            /// <summary>
            ///     Chin-Neck - Tight Chin 0--+255 Double Chin
            /// </summary>
            SHAPE_DOUBLE_CHIN = 6,

            /// <summary>
            ///     Lower Cheeks - Well-Fed 0--+255 Sunken
            /// </summary>
            SHAPE_SUNKEN_CHEEKS = 7,

            /// <summary>
            ///     Upper Bridge - Low 0--+255 High
            /// </summary>
            SHAPE_NOBLE_NOSE_BRIDGE = 8,

            /// <summary>
            ///     - Less 0--+255 More
            /// </summary>
            SHAPE_JOWLS = 9,

            /// <summary>
            ///     Upper Chin Cleft - Round 0--+255 Cleft
            /// </summary>
            SHAPE_CLEFT_CHIN_UPPER = 10,

            /// <summary>
            ///     Cheek Bones - Low 0--+255 High
            /// </summary>
            SHAPE_HIGH_CHEEK_BONES = 11,

            /// <summary>
            ///     Ear Angle - In 0--+255 Out
            /// </summary>
            SHAPE_EARS_OUT = 12,

            /// <summary>
            ///     Eyebrow Points - Smooth 0--+255 Pointy
            /// </summary>
            HAIR_POINTY_EYEBROWS = 13,

            /// <summary>
            ///     Jaw Shape - Pointy 0--+255 Square
            /// </summary>
            SHAPE_SQUARE_JAW = 14,

            /// <summary>
            ///     Upper Cheeks - Thin 0--+255 Puffy
            /// </summary>
            SHAPE_PUFFY_UPPER_CHEEKS = 15,

            /// <summary>
            ///     Nose Tip Angle - Downturned 0--+255 Upturned
            /// </summary>
            SHAPE_UPTURNED_NOSE_TIP = 16,

            /// <summary>
            ///     Nose Thickness - Thin Nose 0--+255 Bulbous Nose
            /// </summary>
            SHAPE_BULBOUS_NOSE = 17,

            /// <summary>
            ///     Upper Eyelid Fold - Uncreased 0--+255 Creased
            /// </summary>
            SHAPE_UPPER_EYELID_FOLD = 18,

            /// <summary>
            ///     Attached Earlobes - Unattached 0--+255 Attached
            /// </summary>
            SHAPE_ATTACHED_EARLOBES = 19,

            /// <summary>
            ///     Eye Bags - Smooth 0--+255 Baggy
            /// </summary>
            SHAPE_BAGGY_EYES = 20,

            /// <summary>
            ///     Eye Opening - Narrow 0--+255 Wide
            /// </summary>
            SHAPE_WIDE_EYES = 21,

            /// <summary>
            ///     Lip Cleft - Narrow 0--+255 Wide
            /// </summary>
            SHAPE_WIDE_LIP_CLEFT = 22,

            /// <summary>
            ///     Bridge Width - Narrow 0--+255 Wide
            /// </summary>
            SHAPE_WIDE_NOSE_BRIDGE = 23,

            /// <summary>
            ///     Eyebrow Arc - Flat 0--+255 Arced
            /// </summary>
            HAIR_ARCED_EYEBROWS = 24,

            /// <summary>
            ///     Height - Short 0--+255 Tall
            /// </summary>
            SHAPE_HEIGHT = 25,

            /// <summary>
            ///     Body Thickness - Body Thin 0--+255 Body Thick
            /// </summary>
            SHAPE_THICKNESS = 26,

            /// <summary>
            ///     Ear Size - Small 0--+255 Large
            /// </summary>
            SHAPE_BIG_EARS = 27,

            /// <summary>
            ///     Shoulders - Narrow 0--+255 Broad
            /// </summary>
            SHAPE_SHOULDERS = 28,

            /// <summary>
            ///     Hip Width - Narrow 0--+255 Wide
            /// </summary>
            SHAPE_HIP_WIDTH = 29,

            /// <summary>
            ///     - Short Torso 0--+255 Long Torso
            /// </summary>
            SHAPE_TORSO_LENGTH = 30,
            SHAPE_MALE = 31,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            GLOVES_GLOVE_LENGTH = 32,

            /// <summary>
            ///     - Darker 0--+255 Lighter
            /// </summary>
            EYES_EYE_LIGHTNESS = 33,

            /// <summary>
            ///     - Natural 0--+255 Unnatural
            /// </summary>
            EYES_EYE_COLOR = 34,

            /// <summary>
            ///     - Small 0--+255 Large
            /// </summary>
            SHAPE_BREAST_SIZE = 35,

            /// <summary>
            ///     - None 0--+255 Wild
            /// </summary>
            SKIN_RAINBOW_COLOR = 36,

            /// <summary>
            ///     Ruddiness - Pale 0--+255 Ruddy
            /// </summary>
            SKIN_RED_SKIN = 37,

            /// <summary>
            ///     - Light 0--+255 Dark
            /// </summary>
            SKIN_PIGMENT = 38,
            HAIR_RAINBOW_COLOR_39 = 39,

            /// <summary>
            ///     - No Red 0--+255 Very Red
            /// </summary>
            HAIR_RED_HAIR = 40,

            /// <summary>
            ///     - Black 0--+255 Blonde
            /// </summary>
            HAIR_BLONDE_HAIR = 41,

            /// <summary>
            ///     - No White 0--+255 All White
            /// </summary>
            HAIR_WHITE_HAIR = 42,

            /// <summary>
            ///     - Less Rosy 0--+255 More Rosy
            /// </summary>
            SKIN_ROSY_COMPLEXION = 43,

            /// <summary>
            ///     - Darker 0--+255 Pinker
            /// </summary>
            SKIN_LIP_PINKNESS = 44,

            /// <summary>
            ///     - Thin Eyebrows 0--+255 Bushy Eyebrows
            /// </summary>
            HAIR_EYEBROW_SIZE = 45,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            HAIR_FRONT_FRINGE = 46,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            HAIR_SIDE_FRINGE = 47,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            HAIR_BACK_FRINGE = 48,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            HAIR_HAIR_FRONT = 49,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            HAIR_HAIR_SIDES = 50,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            HAIR_HAIR_BACK = 51,

            /// <summary>
            ///     - Sweep Forward 0--+255 Sweep Back
            /// </summary>
            HAIR_HAIR_SWEEP = 52,

            /// <summary>
            ///     - Left 0--+255 Right
            /// </summary>
            HAIR_HAIR_TILT = 53,

            /// <summary>
            ///     Middle Part - No Part 0--+255 Part
            /// </summary>
            HAIR_HAIR_PART_MIDDLE = 54,

            /// <summary>
            ///     Right Part - No Part 0--+255 Part
            /// </summary>
            HAIR_HAIR_PART_RIGHT = 55,

            /// <summary>
            ///     Left Part - No Part 0--+255 Part
            /// </summary>
            HAIR_HAIR_PART_LEFT = 56,

            /// <summary>
            ///     Full Hair Sides - Mowhawk 0--+255 Full Sides
            /// </summary>
            HAIR_HAIR_SIDES_FULL = 57,

            /// <summary>
            ///     - Less 0--+255 More
            /// </summary>
            SKIN_BODY_DEFINITION = 58,

            /// <summary>
            ///     Lip Width - Narrow Lips 0--+255 Wide Lips
            /// </summary>
            SHAPE_LIP_WIDTH = 59,

            /// <summary>
            ///     - Small 0--+255 Big
            /// </summary>
            SHAPE_BELLY_SIZE = 60,

            /// <summary>
            ///     - Less 0--+255 More
            /// </summary>
            SKIN_FACIAL_DEFINITION = 61,

            /// <summary>
            ///     - Less 0--+255 More
            /// </summary>
            SKIN_WRINKLES = 62,

            /// <summary>
            ///     - Less 0--+255 More
            /// </summary>
            SKIN_FRECKLES = 63,

            /// <summary>
            ///     - Short Sideburns 0--+255 Mutton Chops
            /// </summary>
            HAIR_SIDEBURNS = 64,

            /// <summary>
            ///     - Chaplin 0--+255 Handlebars
            /// </summary>
            HAIR_MOUSTACHE = 65,

            /// <summary>
            ///     - Less soul 0--+255 More soul
            /// </summary>
            HAIR_SOULPATCH = 66,

            /// <summary>
            ///     - Less Curtains 0--+255 More Curtains
            /// </summary>
            HAIR_CHIN_CURTAINS = 67,

            /// <summary>
            ///     Rumpled Hair - Smooth Hair 0--+255 Rumpled Hair
            /// </summary>
            HAIR_HAIR_RUMPLED = 68,

            /// <summary>
            ///     Big Hair Front - Less 0--+255 More
            /// </summary>
            HAIR_HAIR_BIG_FRONT = 69,

            /// <summary>
            ///     Big Hair Top - Less 0--+255 More
            /// </summary>
            HAIR_HAIR_BIG_TOP = 70,

            /// <summary>
            ///     Big Hair Back - Less 0--+255 More
            /// </summary>
            HAIR_HAIR_BIG_BACK = 71,

            /// <summary>
            ///     Spiked Hair - No Spikes 0--+255 Big Spikes
            /// </summary>
            HAIR_HAIR_SPIKED = 72,

            /// <summary>
            ///     Chin Depth - Shallow 0--+255 Deep
            /// </summary>
            SHAPE_DEEP_CHIN = 73,

            /// <summary>
            ///     Part Bangs - No Part 0--+255 Part Bangs
            /// </summary>
            HAIR_BANGS_PART_MIDDLE = 74,

            /// <summary>
            ///     Head Shape - More Square 0--+255 More Round
            /// </summary>
            SHAPE_HEAD_SHAPE = 75,

            /// <summary>
            ///     Eye Spacing - Close Set Eyes 0--+255 Far Set Eyes
            /// </summary>
            SHAPE_EYE_SPACING = 76,

            /// <summary>
            ///     - Low Heels 0--+255 High Heels
            /// </summary>
            SHOES_HEEL_HEIGHT = 77,

            /// <summary>
            ///     - Low Platforms 0--+255 High Platforms
            /// </summary>
            SHOES_PLATFORM_HEIGHT = 78,

            /// <summary>
            ///     - Thin Lips 0--+255 Fat Lips
            /// </summary>
            SHAPE_LIP_THICKNESS = 79,

            /// <summary>
            ///     Mouth Position - High 0--+255 Low
            /// </summary>
            SHAPE_MOUTH_HEIGHT = 80,

            /// <summary>
            ///     Breast Buoyancy - Less Gravity 0--+255 More Gravity
            /// </summary>
            SHAPE_BREAST_GRAVITY = 81,

            /// <summary>
            ///     Platform Width - Narrow 0--+255 Wide
            /// </summary>
            SHOES_SHOE_PLATFORM_WIDTH = 82,

            /// <summary>
            ///     - Pointy Heels 0--+255 Thick Heels
            /// </summary>
            SHOES_HEEL_SHAPE = 83,

            /// <summary>
            ///     - Pointy 0--+255 Square
            /// </summary>
            SHOES_TOE_SHAPE = 84,

            /// <summary>
            ///     Foot Size - Small 0--+255 Big
            /// </summary>
            SHAPE_FOOT_SIZE = 85,

            /// <summary>
            ///     Nose Width - Narrow 0--+255 Wide
            /// </summary>
            SHAPE_WIDE_NOSE = 86,

            /// <summary>
            ///     Eyelash Length - Short 0--+255 Long
            /// </summary>
            SHAPE_EYELASHES_LONG = 87,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            UNDERSHIRT_SLEEVE_LENGTH = 88,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            UNDERSHIRT_BOTTOM = 89,

            /// <summary>
            ///     - Low 0--+255 High
            /// </summary>
            UNDERSHIRT_COLLAR_FRONT = 90,
            JACKET_SLEEVE_LENGTH_91 = 91,
            JACKET_COLLAR_FRONT_92 = 92,

            /// <summary>
            ///     Jacket Length - Short 0--+255 Long
            /// </summary>
            JACKET_BOTTOM_LENGTH_LOWER = 93,

            /// <summary>
            ///     Open Front - Open 0--+255 Closed
            /// </summary>
            JACKET_OPEN_JACKET = 94,

            /// <summary>
            ///     - Short 0--+255 Tall
            /// </summary>
            SHOES_SHOE_HEIGHT = 95,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            SOCKS_SOCKS_LENGTH = 96,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            UNDERPANTS_PANTS_LENGTH = 97,

            /// <summary>
            ///     - Low 0--+255 High
            /// </summary>
            UNDERPANTS_PANTS_WAIST = 98,

            /// <summary>
            ///     Cuff Flare - Tight Cuffs 0--+255 Flared Cuffs
            /// </summary>
            PANTS_LEG_PANTFLAIR = 99,

            /// <summary>
            ///     - More Vertical 0--+255 More Sloped
            /// </summary>
            SHAPE_FOREHEAD_ANGLE = 100,

            /// <summary>
            ///     - Less Body Fat 0--+255 More Body Fat
            /// </summary>
            SHAPE_BODY_FAT = 101,

            /// <summary>
            ///     Pants Crotch - High and Tight 0--+255 Low and Loose
            /// </summary>
            PANTS_LOW_CROTCH = 102,

            /// <summary>
            ///     Egg Head - Chin Heavy 0--+255 Forehead Heavy
            /// </summary>
            SHAPE_EGG_HEAD = 103,

            /// <summary>
            ///     Head Stretch - Squash Head 0--+255 Stretch Head
            /// </summary>
            SHAPE_SQUASH_STRETCH_HEAD = 104,

            /// <summary>
            ///     Torso Muscles - Less Muscular 0--+255 More Muscular
            /// </summary>
            SHAPE_TORSO_MUSCLES = 105,

            /// <summary>
            ///     Outer Eye Corner - Corner Down 0--+255 Corner Up
            /// </summary>
            SHAPE_EYELID_CORNER_UP = 106,

            /// <summary>
            ///     - Less Muscular 0--+255 More Muscular
            /// </summary>
            SHAPE_LEG_MUSCLES = 107,

            /// <summary>
            ///     Lip Fullness - Less Full 0--+255 More Full
            /// </summary>
            SHAPE_TALL_LIPS = 108,

            /// <summary>
            ///     Toe Thickness - Flat Toe 0--+255 Thick Toe
            /// </summary>
            SHOES_SHOE_TOE_THICK = 109,

            /// <summary>
            ///     Crooked Nose - Nose Left 0--+255 Nose Right
            /// </summary>
            SHAPE_CROOKED_NOSE = 110,

            /// <summary>
            ///     - Corner Down 0--+255 Corner Up
            /// </summary>
            SHAPE_MOUTH_CORNER = 111,

            /// <summary>
            ///     - Shear Right Up 0--+255 Shear Left Up
            /// </summary>
            SHAPE_FACE_SHEAR = 112,

            /// <summary>
            ///     Shift Mouth - Shift Left 0--+255 Shift Right
            /// </summary>
            SHAPE_SHIFT_MOUTH = 113,

            /// <summary>
            ///     Eye Pop - Pop Right Eye 0--+255 Pop Left Eye
            /// </summary>
            SHAPE_POP_EYE = 114,

            /// <summary>
            ///     Jaw Jut - Overbite 0--+255 Underbite
            /// </summary>
            SHAPE_JAW_JUT = 115,

            /// <summary>
            ///     Shear Back - Full Back 0--+255 Sheared Back
            /// </summary>
            HAIR_HAIR_SHEAR_BACK = 116,

            /// <summary>
            ///     - Small Hands 0--+255 Large Hands
            /// </summary>
            SHAPE_HAND_SIZE = 117,

            /// <summary>
            ///     Love Handles - Less Love 0--+255 More Love
            /// </summary>
            SHAPE_LOVE_HANDLES = 118,
            SHAPE_TORSO_MUSCLES_119 = 119,

            /// <summary>
            ///     Head Size - Small Head 0--+255 Big Head
            /// </summary>
            SHAPE_HEAD_SIZE = 120,

            /// <summary>
            ///     - Skinny Neck 0--+255 Thick Neck
            /// </summary>
            SHAPE_NECK_THICKNESS = 121,

            /// <summary>
            ///     Breast Cleavage - Separate 0--+255 Join
            /// </summary>
            SHAPE_BREAST_FEMALE_CLEAVAGE = 122,

            /// <summary>
            ///     Pectorals - Big Pectorals 0--+255 Sunken Chest
            /// </summary>
            SHAPE_CHEST_MALE_NO_PECS = 123,

            /// <summary>
            ///     Eye Size - Beady Eyes 0--+255 Anime Eyes
            /// </summary>
            SHAPE_EYE_SIZE = 124,

            /// <summary>
            ///     - Short Legs 0--+255 Long Legs
            /// </summary>
            SHAPE_LEG_LENGTH = 125,

            /// <summary>
            ///     - Short Arms 0--+255 Long arms
            /// </summary>
            SHAPE_ARM_LENGTH = 126,

            /// <summary>
            ///     - Pink 0--+255 Black
            /// </summary>
            SKIN_LIPSTICK_COLOR = 127,

            /// <summary>
            ///     - No Lipstick 0--+255 More Lipstick
            /// </summary>
            SKIN_LIPSTICK = 128,

            /// <summary>
            ///     - No Lipgloss 0--+255 Glossy
            /// </summary>
            SKIN_LIPGLOSS = 129,

            /// <summary>
            ///     - No Eyeliner 0--+255 Full Eyeliner
            /// </summary>
            SKIN_EYELINER = 130,

            /// <summary>
            ///     - No Blush 0--+255 More Blush
            /// </summary>
            SKIN_BLUSH = 131,

            /// <summary>
            ///     - Pink 0--+255 Orange
            /// </summary>
            SKIN_BLUSH_COLOR = 132,

            /// <summary>
            ///     - Clear 0--+255 Opaque
            /// </summary>
            SKIN_OUT_SHDW_OPACITY = 133,

            /// <summary>
            ///     - No Eyeshadow 0--+255 More Eyeshadow
            /// </summary>
            SKIN_OUTER_SHADOW = 134,

            /// <summary>
            ///     - Light 0--+255 Dark
            /// </summary>
            SKIN_OUT_SHDW_COLOR = 135,

            /// <summary>
            ///     - No Eyeshadow 0--+255 More Eyeshadow
            /// </summary>
            SKIN_INNER_SHADOW = 136,

            /// <summary>
            ///     - No Polish 0--+255 Painted Nails
            /// </summary>
            SKIN_NAIL_POLISH = 137,

            /// <summary>
            ///     - Clear 0--+255 Opaque
            /// </summary>
            SKIN_BLUSH_OPACITY = 138,

            /// <summary>
            ///     - Light 0--+255 Dark
            /// </summary>
            SKIN_IN_SHDW_COLOR = 139,

            /// <summary>
            ///     - Clear 0--+255 Opaque
            /// </summary>
            SKIN_IN_SHDW_OPACITY = 140,

            /// <summary>
            ///     - Dark Green 0--+255 Black
            /// </summary>
            SKIN_EYELINER_COLOR = 141,

            /// <summary>
            ///     - Pink 0--+255 Black
            /// </summary>
            SKIN_NAIL_POLISH_COLOR = 142,

            /// <summary>
            ///     - Sparse 0--+255 Dense
            /// </summary>
            HAIR_EYEBROW_DENSITY = 143,

            /// <summary>
            ///     - 5 O'Clock Shadow 0--+255 Bushy Hair
            /// </summary>
            HAIR_HAIR_THICKNESS = 144,

            /// <summary>
            ///     Saddle Bags - Less Saddle 0--+255 More Saddle
            /// </summary>
            SHAPE_SADDLEBAGS = 145,

            /// <summary>
            ///     Taper Back - Wide Back 0--+255 Narrow Back
            /// </summary>
            HAIR_HAIR_TAPER_BACK = 146,

            /// <summary>
            ///     Taper Front - Wide Front 0--+255 Narrow Front
            /// </summary>
            HAIR_HAIR_TAPER_FRONT = 147,

            /// <summary>
            ///     - Short Neck 0--+255 Long Neck
            /// </summary>
            SHAPE_NECK_LENGTH = 148,

            /// <summary>
            ///     Eyebrow Height - Higher 0--+255 Lower
            /// </summary>
            HAIR_LOWER_EYEBROWS = 149,

            /// <summary>
            ///     Lower Bridge - Low 0--+255 High
            /// </summary>
            SHAPE_LOWER_BRIDGE_NOSE = 150,

            /// <summary>
            ///     Nostril Division - High 0--+255 Low
            /// </summary>
            SHAPE_LOW_SEPTUM_NOSE = 151,

            /// <summary>
            ///     Jaw Angle - Low Jaw 0--+255 High Jaw
            /// </summary>
            SHAPE_JAW_ANGLE = 152,

            /// <summary>
            ///     Shear Front - Full Front 0--+255 Sheared Front
            /// </summary>
            HAIR_HAIR_SHEAR_FRONT = 153,

            /// <summary>
            ///     - Less Volume 0--+255 More Volume
            /// </summary>
            HAIR_HAIR_VOLUME = 154,

            /// <summary>
            ///     Lip Cleft Depth - Shallow 0--+255 Deep
            /// </summary>
            SHAPE_LIP_CLEFT_DEEP = 155,

            /// <summary>
            ///     Puffy Eyelids - Flat 0--+255 Puffy
            /// </summary>
            SHAPE_PUFFY_LOWER_LIDS = 156,

            /// <summary>
            ///     - Sunken Eyes 0--+255 Bugged Eyes
            /// </summary>
            SHAPE_EYE_DEPTH = 157,

            /// <summary>
            ///     - Flat Head 0--+255 Long Head
            /// </summary>
            SHAPE_HEAD_LENGTH = 158,

            /// <summary>
            ///     - Less Freckles 0--+255 More Freckles
            /// </summary>
            SKIN_BODY_FRECKLES = 159,

            /// <summary>
            ///     - Low 0--+255 High
            /// </summary>
            UNDERSHIRT_COLLAR_BACK = 160,
            JACKET_COLLAR_BACK_161 = 161,
            SHIRT_COLLAR_BACK_162 = 162,

            /// <summary>
            ///     - Short Pigtails 0--+255 Long Pigtails
            /// </summary>
            HAIR_PIGTAILS = 163,

            /// <summary>
            ///     - Short Ponytail 0--+255 Long Ponytail
            /// </summary>
            HAIR_PONYTAIL = 164,

            /// <summary>
            ///     Butt Size - Flat Butt 0--+255 Big Butt
            /// </summary>
            SHAPE_BUTT_SIZE = 165,

            /// <summary>
            ///     Ear Tips - Flat 0--+255 Pointy
            /// </summary>
            SHAPE_POINTY_EARS = 166,

            /// <summary>
            ///     Lip Ratio - More Upper Lip 0--+255 More Lower Lip
            /// </summary>
            SHAPE_LIP_RATIO = 167,
            SHIRT_SLEEVE_LENGTH_168 = 168,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            SHIRT_SHIRT_BOTTOM = 169,
            SHIRT_COLLAR_FRONT_170 = 170,
            SHIRT_SHIRT_RED = 171,
            SHIRT_SHIRT_GREEN = 172,
            SHIRT_SHIRT_BLUE = 173,
            PANTS_PANTS_RED = 174,
            PANTS_PANTS_GREEN = 175,
            PANTS_PANTS_BLUE = 176,
            SHOES_SHOES_RED = 177,
            SHOES_SHOES_GREEN = 178,

            /// <summary>
            ///     - Low 0--+255 High
            /// </summary>
            PANTS_WAIST_HEIGHT = 179,
            PANTS_PANTS_LENGTH_180 = 180,

            /// <summary>
            ///     Pants Fit - Tight Pants 0--+255 Loose Pants
            /// </summary>
            PANTS_LOOSE_LOWER_CLOTHING = 181,
            SHOES_SHOES_BLUE = 182,
            SOCKS_SOCKS_RED = 183,
            SOCKS_SOCKS_GREEN = 184,
            SOCKS_SOCKS_BLUE = 185,
            UNDERSHIRT_UNDERSHIRT_RED = 186,
            UNDERSHIRT_UNDERSHIRT_GREEN = 187,
            UNDERSHIRT_UNDERSHIRT_BLUE = 188,
            UNDERPANTS_UNDERPANTS_RED = 189,
            UNDERPANTS_UNDERPANTS_GREEN = 190,
            UNDERPANTS_UNDERPANTS_BLUE = 191,
            GLOVES_GLOVES_RED = 192,

            /// <summary>
            ///     Shirt Fit - Tight Shirt 0--+255 Loose Shirt
            /// </summary>
            SHIRT_LOOSE_UPPER_CLOTHING = 193,
            GLOVES_GLOVES_GREEN = 194,
            GLOVES_GLOVES_BLUE = 195,
            JACKET_JACKET_RED = 196,
            JACKET_JACKET_GREEN = 197,
            JACKET_JACKET_BLUE = 198,

            /// <summary>
            ///     Sleeve Looseness - Tight Sleeves 0--+255 Loose Sleeves
            /// </summary>
            SHIRT_SHIRTSLEEVE_FLAIR = 199,

            /// <summary>
            ///     Knee Angle - Knock Kneed 0--+255 Bow Legged
            /// </summary>
            SHAPE_BOWED_LEGS = 200,

            /// <summary>
            ///     - Short hips 0--+255 Long Hips
            /// </summary>
            SHAPE_HIP_LENGTH = 201,

            /// <summary>
            ///     - Fingerless 0--+255 Fingers
            /// </summary>
            GLOVES_GLOVE_FINGERS = 202,

            /// <summary>
            ///     bustle skirt - no bustle 0--+255 more bustle
            /// </summary>
            SKIRT_SKIRT_BUSTLE = 203,

            /// <summary>
            ///     - Short 0--+255 Long
            /// </summary>
            SKIRT_SKIRT_LENGTH = 204,

            /// <summary>
            ///     - Open Front 0--+255 Closed Front
            /// </summary>
            SKIRT_SLIT_FRONT = 205,

            /// <summary>
            ///     - Open Back 0--+255 Closed Back
            /// </summary>
            SKIRT_SLIT_BACK = 206,

            /// <summary>
            ///     - Open Left 0--+255 Closed Left
            /// </summary>
            SKIRT_SLIT_LEFT = 207,

            /// <summary>
            ///     - Open Right 0--+255 Closed Right
            /// </summary>
            SKIRT_SLIT_RIGHT = 208,

            /// <summary>
            ///     Skirt Fit - Tight Skirt 0--+255 Poofy Skirt
            /// </summary>
            SKIRT_SKIRT_LOOSENESS = 209,
            SHIRT_SHIRT_WRINKLES = 210,
            PANTS_PANTS_WRINKLES = 211,

            /// <summary>
            ///     Jacket Wrinkles - No Wrinkles 0--+255 Wrinkles
            /// </summary>
            JACKET_JACKET_WRINKLES = 212,

            /// <summary>
            ///     Package - Coin Purse 0--+255 Duffle Bag
            /// </summary>
            SHAPE_MALE_PACKAGE = 213,

            /// <summary>
            ///     Inner Eye Corner - Corner Down 0--+255 Corner Up
            /// </summary>
            SHAPE_EYELID_INNER_CORNER_UP = 214,
            SKIRT_SKIRT_RED = 215,
            SKIRT_SKIRT_GREEN = 216,
            SKIRT_SKIRT_BLUE = 217,

            /// <summary>
            /// Avatar Physics section.  These are 0 type visual params which get transmitted.
            /// </summary>

            /// <summary>
            /// Breast Part 1 
            /// </summary>
            BREAST_PHYSICS_MASS = 218,
            BREAST_PHYSICS_GRAVITY = 219,
            BREAST_PHYSICS_DRAG = 220,
            BREAST_PHYSICS_UPDOWN_MAX_EFFECT = 221,
            BREAST_PHYSICS_UPDOWN_SPRING = 222,
            BREAST_PHYSICS_UPDOWN_GAIN = 223,
            BREAST_PHYSICS_UPDOWN_DAMPING = 224,
            BREAST_PHYSICS_INOUT_MAX_EFFECT = 225,
            BREAST_PHYSICS_INOUT_SPRING = 226,
            BREAST_PHYSICS_INOUT_GAIN = 227,
            BREAST_PHYSICS_INOUT_DAMPING = 228,
            
            /// <summary>
            /// Belly
            /// </summary>
            BELLY_PHYISCS_MASS = 229,
            BELLY_PHYSICS_GRAVITY = 230,
            BELLY_PHYSICS_DRAG = 231,
            BELLY_PHYISCS_UPDOWN_MAX_EFFECT = 232,
            BELLY_PHYSICS_UPDOWN_SPRING = 233,
            BELLY_PHYSICS_UPDOWN_GAIN = 234,
            BELLY_PHYSICS_UPDOWN_DAMPING = 235,

            /// <summary>
            /// Butt
            /// </summary>
            BUTT_PHYSICS_MASS = 236,
            BUTT_PHYSICS_GRAVITY = 237,
            BUTT_PHYSICS_DRAG = 238,
            BUTT_PHYSICS_UPDOWN_MAX_EFFECT = 239,
            BUTT_PHYSICS_UPDOWN_SPRING = 240,
            BUTT_PHYSICS_UPDOWN_GAIN = 241,
            BUTT_PHYSICS_UPDOWN_DAMPING = 242,
            BUTT_PHYSICS_LEFTRIGHT_MAX_EFFECT = 243,
            BUTT_PHYSICS_LEFTRIGHT_SPRING = 244,
            BUTT_PHYSICS_LEFTRIGHT_GAIN = 245,
            BUTT_PHYSICS_LEFTRIGHT_DAMPING = 246,
            
            /// <summary>
            /// Breast Part 2
            /// </summary>
            BREAST_PHYSICS_LEFTRIGHT_MAX_EFFECT = 247,
            BREAST_PHYSICS_LEFTRIGHT_SPRING= 248,
            BREAST_PHYSICS_LEFTRIGHT_GAIN = 249,
            BREAST_PHYSICS_LEFTRIGHT_DAMPING = 250
        }
        #endregion
    }
}
