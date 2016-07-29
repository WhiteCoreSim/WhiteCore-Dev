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

using OpenMetaverse;
using OpenMetaverse.Assets;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services.ClassHelpers.Assets;

namespace WhiteCore.ScriptEngine.DotNetEngine.MiniModule
{
    public class InventoryItem : IInventoryItem
    {
        readonly TaskInventoryItem m_privateItem;
        readonly IScene m_rootScene;

        public InventoryItem(IScene rootScene, TaskInventoryItem internalItem)
        {
            m_rootScene = rootScene;
            m_privateItem = internalItem;
        }

        // Marked internal, to prevent scripts from accessing the internal type

        #region IInventoryItem Members

        public int Type
        {
            get { return m_privateItem.Type; }
        }

        public UUID AssetID
        {
            get { return m_privateItem.AssetID; }
        }

        // This method exposes OpenSim/OpenMetaverse internals and needs to be replaced with a IAsset specific to MRM.
        public T RetrieveAsset<T> () where T : Asset, new()
        {
            AssetBase asset = m_rootScene.AssetService.Get (AssetID.ToString ());
            if (asset == null)
                return null;
        
            T result = new T();

            if ((sbyte)result.AssetType != asset.Type) {
                MainConsole.Instance.Error ("[MRM] The supplied asset class does not match the found asset");
                asset.Dispose ();
                return null;
            }

            var assetData = new byte [asset.Data.Length];
            asset.Data.CopyTo (assetData, 0);
            asset.Dispose ();

            result.AssetData = assetData;
            result.Decode();
            return result;
        }

        #endregion

        internal TaskInventoryItem ToTaskInventoryItem()
        {
            return m_privateItem;
        }

        /// <summary>
        ///     This will attempt to convert from an IInventoryItem to an InventoryItem object
        /// </summary>
        /// <description>
        ///     In order for this to work the object which implements IInventoryItem must inherit from InventoryItem, otherwise
        ///     an exception is thrown.
        /// </description>
        /// <param name="i">
        ///     The interface to upcast <see cref="IInventoryItem" />
        /// </param>
        /// <returns>
        ///     The object backing the interface implementation <see cref="InventoryItem" />
        /// </returns>
        internal static InventoryItem FromInterface (IInventoryItem i)
        {
            if (typeof (InventoryItem).IsAssignableFrom (i.GetType ())) {
                return (InventoryItem)i;
            }
            MainConsole.Instance.Error ("[MRM] There is no legal conversion from IInventoryItem to InventoryItem");
            return null;
        }
    }
}
