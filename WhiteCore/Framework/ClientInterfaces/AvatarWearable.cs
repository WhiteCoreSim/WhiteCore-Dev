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

using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace WhiteCore.Framework.ClientInterfaces
{
    public struct WearableItem
    {
        public UUID AssetID;
        public UUID ItemID;

        public WearableItem(UUID itemID, UUID assetID)
        {
            ItemID = itemID;
            AssetID = assetID;
        }
    }

    public class AvatarWearable
    {
        // these are guessed at by the list here -
        // http://wiki.secondlife.com/wiki/Avatar_Appearance.  We'll
        // correct them over time for when were are wrong.
        //public static readonly int BODY;
        public static readonly int SHAPE = 0;
        public static readonly int SKIN = 1;
        public static readonly int HAIR = 2;
        public static readonly int EYES = 3;
        public static readonly int SHIRT = 4;
        public static readonly int PANTS = 5;
        public static readonly int SHOES = 6;
        public static readonly int SOCKS = 7;
        public static readonly int JACKET = 8;
        public static readonly int GLOVES = 9;
        public static readonly int UNDERSHIRT = 10;
        public static readonly int UNDERPANTS = 11;
        public static readonly int SKIRT = 12;
        public static readonly int ALPHA = 13;
        public static readonly int TATTOO = 14;
        public static readonly int PHYSICS = 15;

        public static readonly int MAX_WEARABLES = 16;

        public static readonly UUID DEFAULT_SHAPE_ITEM = new UUID("66c41e39-38f9-f75a-024e-585989bfaba9");
        public static readonly UUID DEFAULT_SHAPE_ASSET = new UUID("66c41e39-38f9-f75a-024e-585989bfab73");

        public static readonly UUID DEFAULT_HAIR_ITEM = new UUID("d342e6c1-b9d2-11dc-95ff-0800200c9a66");
        public static readonly UUID DEFAULT_HAIR_ASSET = new UUID("d342e6c0-b9d2-11dc-95ff-0800200c9a66");

        public static readonly UUID DEFAULT_SKIN_ITEM = new UUID("77c41e39-38f9-f75a-024e-585989bfabc9");
        public static readonly UUID DEFAULT_SKIN_ASSET = new UUID("77c41e39-38f9-f75a-024e-585989bbabbb");

        public static readonly UUID DEFAULT_EYES_ITEM = new UUID("4d3af499-976b-400a-a04a-f13df0babc0b");
        public static readonly UUID DEFAULT_EYES_ASSET = new UUID("4bb6fa4d-1cd2-498a-a84c-95c1a0e745a7");

        public static readonly UUID DEFAULT_SHIRT_ITEM = new UUID("77c41e39-38f9-f75a-0000-585989bf0000");
        public static readonly UUID DEFAULT_SHIRT_ASSET = new UUID("00000000-38f9-1111-024e-222222111110");

        public static readonly UUID DEFAULT_PANTS_ITEM = new UUID("77c41e39-38f9-f75a-0000-5859892f1111");
        public static readonly UUID DEFAULT_PANTS_ASSET = new UUID("00000000-38f9-1111-024e-222222111120");

        public static readonly UUID DEFAULT_ALPHA_ITEM = new UUID("bfb9923c-4838-4d2d-bf07-608c5b1165c8");
        public static readonly UUID DEFAULT_ALPHA_ASSET = new UUID("1578a2b1-5179-4b53-b618-fe00ca5a5594");

        public static readonly UUID DEFAULT_TATTOO_ITEM = new UUID("c47e22bd-3021-4ba4-82aa-2b5cb34d35e1");
        public static readonly UUID DEFAULT_TATTOO_ASSET = new UUID("00000000-0000-2222-3333-100000001007");

        public int MaxItems = 5;
        protected List<UUID> m_ids = new List<UUID>();
        protected Dictionary<UUID, UUID> m_items = new Dictionary<UUID, UUID>();

        public AvatarWearable()
        {
        }

        public AvatarWearable(UUID itemID, UUID assetID)
        {
            Wear(itemID, assetID);
        }

        public AvatarWearable(OSDArray args)
        {
            FromOSD(args);
        }

        public int Count
        {
            get { return m_ids.Count; }
        }

        public WearableItem this[int idx]
        {
            get
            {
                if (idx >= m_ids.Count || idx < 0)
                    return new WearableItem(UUID.Zero, UUID.Zero);

                return new WearableItem(m_ids[idx], m_items[m_ids[idx]]);
            }
        }

        public static AvatarWearable[] DefaultWearables
        {
            get
            {
                AvatarWearable[] defaultWearables = new AvatarWearable[MAX_WEARABLES]; //should be 16 of these
                for (int i = 0; i < MAX_WEARABLES; i++)
                {
                    defaultWearables[i] = new AvatarWearable();
                }

                // Body
                defaultWearables[SHAPE].Add(DEFAULT_SHAPE_ITEM, DEFAULT_SHAPE_ASSET);

                // Hair
                defaultWearables[HAIR].Add(DEFAULT_HAIR_ITEM, DEFAULT_HAIR_ASSET);

                // Skin
                defaultWearables[SKIN].Add(DEFAULT_SKIN_ITEM, DEFAULT_SKIN_ASSET);

                // Eyes
                defaultWearables[EYES].Add(DEFAULT_EYES_ITEM, DEFAULT_EYES_ASSET);

                // Shirt
                defaultWearables[SHIRT].Add(DEFAULT_SHIRT_ITEM, DEFAULT_SHIRT_ASSET);

                // Pants
                defaultWearables[PANTS].Add(DEFAULT_PANTS_ITEM, DEFAULT_PANTS_ASSET);

                // Eyes
                defaultWearables[EYES].Add(DEFAULT_EYES_ITEM, DEFAULT_EYES_ASSET);

                // Alpha
                defaultWearables[ALPHA].Add(DEFAULT_ALPHA_ITEM, DEFAULT_ALPHA_ASSET);

                // Tattoo
                defaultWearables[TATTOO].Add(DEFAULT_TATTOO_ITEM, DEFAULT_TATTOO_ASSET);

                return defaultWearables;
            }
        }

        public Dictionary<UUID, UUID> GetItems()
        {
            return m_items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public OSDArray ToOSD()
        {
            OSDArray wearlist = new OSDArray();

            foreach (UUID id in m_ids)
            {
                OSDMap weardata = new OSDMap();
                weardata["item"] = OSD.FromUUID(id);
                weardata["asset"] = OSD.FromUUID(m_items[id]);
                wearlist.Add(weardata);
            }

            return wearlist;
        }

        public void FromOSD(OSDArray args)
        {
            Clear();

            foreach (OSDMap weardata in args)
            {
                Add(weardata["item"].AsUUID(), weardata["asset"].AsUUID());
            }
        }

        public void Add(UUID itemID, UUID assetID)
        {
            if (itemID == UUID.Zero)
                return;
            if (m_items.ContainsKey(itemID))
            {
                m_items[itemID] = assetID;
                return;
            }
            if (MaxItems != 0 && m_ids.Count >= MaxItems)
                return;

            m_ids.Add(itemID);
            m_items[itemID] = assetID;
        }

        public void Wear(WearableItem item)
        {
            Wear(item.ItemID, item.AssetID);
        }

        public void Wear(UUID itemID, UUID assetID)
        {
            Clear();
            Add(itemID, assetID);
        }

        public void Clear()
        {
            m_ids.Clear();
            m_items.Clear();
        }

        public void RemoveItem(UUID itemID)
        {
            if (m_items.ContainsKey(itemID))
            {
                m_ids.Remove(itemID);
                m_items.Remove(itemID);
            }
        }

        public void RemoveAsset(UUID assetID)
        {
            UUID itemID = UUID.Zero;

            foreach (KeyValuePair<UUID, UUID> kvp in m_items.Where(kvp => kvp.Value == assetID))
            {
                itemID = kvp.Key;
                break;
            }

            if (itemID != UUID.Zero)
            {
                m_ids.Remove(itemID);
                m_items.Remove(itemID);
            }
        }

        public UUID GetAsset(UUID itemID)
        {
            if (!m_items.ContainsKey(itemID))
                return UUID.Zero;
            return m_items[itemID];
        }

        public UUID GetItem(UUID assetID)
        {
            if (!m_items.ContainsValue(assetID))
                return UUID.Zero;

            foreach (KeyValuePair<UUID, UUID> kvp in m_items.Where(kvp => kvp.Value == assetID))
            {
                return kvp.Key;
            }

            return UUID.Zero;
        }
    }
}