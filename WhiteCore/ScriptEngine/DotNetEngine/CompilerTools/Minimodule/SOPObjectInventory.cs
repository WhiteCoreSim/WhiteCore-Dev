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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WhiteCore.Framework.SceneInfo;
using OpenMetaverse;

namespace WhiteCore.ScriptEngine.DotNetEngine.MiniModule
{
    public class SOPObjectInventory : IObjectInventory
    {
        readonly TaskInventoryDictionary m_privateInventory;

        /// WhiteCore's task inventory
        readonly Dictionary<UUID, IInventoryItem> m_publicInventory;

        /// MRM's inventory
        readonly IScene m_rootScene;

        public SOPObjectInventory(IScene rootScene, TaskInventoryDictionary taskInventory)
        {
            m_rootScene = rootScene;
            m_privateInventory = taskInventory;
            m_publicInventory = new Dictionary<UUID, IInventoryItem>();
        }

        #region IObjectInventory Members

        //note: it looks to me this function is not doing anything, no return value, always throws exception
        public IInventoryItem this[string name]
        {
            get
            {
                foreach (TaskInventoryItem i in m_privateInventory.Values.Where(i => i.Name == name))
                    if (!m_publicInventory.ContainsKey(i.ItemID))
                        m_publicInventory.Add(i.ItemID, new InventoryItem(m_rootScene, i));

                throw new KeyNotFoundException();
            }
        }

        #endregion

        /// <summary>
        ///     Fully populate the public dictionary with the contents of the private dictionary
        /// </summary>
        /// <description>
        ///     This will only convert those items which hasn't already been converted. ensuring that
        ///     no items are converted twice, and that any references already in use are maintained.
        /// </description>
        void SynchronizeDictionaries()
        {
            foreach (
                TaskInventoryItem privateItem in
                    m_privateInventory.Values.Where(privateItem => !m_publicInventory.ContainsKey(privateItem.ItemID)))
                m_publicInventory.Add(privateItem.ItemID, new InventoryItem(m_rootScene, privateItem));
        }

        #region IDictionary<UUID, IInventoryItem> implementation

        public void Add(UUID key, IInventoryItem value)
        {
            m_publicInventory.Add(key, value);
            m_privateInventory.Add(key, InventoryItem.FromInterface(value).ToTaskInventoryItem());
        }

        public bool ContainsKey(UUID key)
        {
            return m_privateInventory.ContainsKey(key);
        }

        public bool Remove(UUID key)
        {
            m_publicInventory.Remove(key);
            return m_privateInventory.Remove(key);
        }

        public bool TryGetValue(UUID key, out IInventoryItem value)
        {
            value = null;

            bool result = false;
            if (!m_publicInventory.TryGetValue(key, out value))
            {
                // wasn't found in the public inventory
                TaskInventoryItem privateItem;

                result = m_privateInventory.TryGetValue(key, out privateItem);
                if (result)
                {
                    value = new InventoryItem(m_rootScene, privateItem);
                    m_publicInventory.Add(key, value); // add item, so we don't convert again
                }
            }
            else
                return true;

            return result;
        }

        public ICollection<UUID> Keys
        {
            get { return m_privateInventory.Keys; }
        }

        public ICollection<IInventoryItem> Values
        {
            get
            {
                SynchronizeDictionaries();
                return m_publicInventory.Values;
            }
        }

        #endregion

        #region IEnumerable<KeyValuePair<UUID, IInventoryItem>> implementation

        public IEnumerator<KeyValuePair<UUID, IInventoryItem>> GetEnumerator()
        {
            SynchronizeDictionaries();
            return m_publicInventory.GetEnumerator();
        }

        #endregion

        #region IEnumerable implementation

        IEnumerator IEnumerable.GetEnumerator()
        {
            SynchronizeDictionaries();
            return m_publicInventory.GetEnumerator();
        }

        #endregion

        #region ICollection<KeyValuePair<UUID, IInventoryItem>> implementation

        public void Add(KeyValuePair<UUID, IInventoryItem> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            m_publicInventory.Clear();
            m_privateInventory.Clear();
        }

        public bool Contains(KeyValuePair<UUID, IInventoryItem> item)
        {
            return m_privateInventory.ContainsKey(item.Key);
        }

        public bool Remove(KeyValuePair<UUID, IInventoryItem> item)
        {
            return Remove(item.Key);
        }

        public int Count
        {
            get { return m_privateInventory.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void CopyTo(KeyValuePair<UUID, IInventoryItem>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Explicit implementations

        IInventoryItem IDictionary<UUID, IInventoryItem>.this[UUID key]
        {
            get
            {
                IInventoryItem result;
                if (TryGetValue(key, out result))
                    return result;

                throw new KeyNotFoundException("[MRM] The requrested item ID could not be found");
            }
            set
            {
                m_publicInventory[key] = value;
                m_privateInventory[key] = InventoryItem.FromInterface(value).ToTaskInventoryItem();
            }
        }

        void ICollection<KeyValuePair<UUID, IInventoryItem>>.CopyTo(KeyValuePair<UUID, IInventoryItem>[] array,
                                                                    int offset)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}