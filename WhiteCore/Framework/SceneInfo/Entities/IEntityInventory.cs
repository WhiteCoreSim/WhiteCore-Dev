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

using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.PresenceInfo;

namespace WhiteCore.Framework.SceneInfo.Entities
{
    /// <summary>
    ///     Interface to an ISceneChildEntity's inventory
    /// </summary>
    /// This is not a finished 1.0 candidate interface
    public interface IEntityInventory
    {
        /// <summary>
        ///     Has the inventory in this object changed since the last backup?
        /// </summary>
        bool HasInventoryChanged { get; set; }

        /// <summary>
        ///     Force the task inventory of this prim to persist at the next update sweep
        /// </summary>
        void ForceInventoryPersistence ();

        /// <summary>
        ///     Reset UUIDs for all the items in the prim's inventory.
        /// </summary>
        /// This involves either generating
        /// new ones or setting existing UUIDs to the correct parent UUIDs.
        /// 
        /// If this method is called and there are inventory items, then we regard the inventory as having changed.
        /// <param name="ChangeScripts">Link number for the part</param>
        void ResetInventoryIDs (bool ChangeScripts);

        /// <summary>
        ///     Reset parent object UUID for all the items in the prim's inventory.
        /// </summary>
        /// If this method is called and there are inventory items, then we regard the inventory as having changed.
        void ResetObjectID ();

        /// <summary>
        ///     Change every item in this inventory to a new owner.
        /// </summary>
        /// <param name="ownerId"></param>
        void ChangeInventoryOwner (UUID ownerId);

        /// <summary>
        ///     Change every item in this inventory to a new group.
        /// </summary>
        /// <param name="groupID"></param>
        void ChangeInventoryGroup (UUID groupID);

        /// <summary>
        ///     Start all the scripts contained in this entity's inventory
        /// </summary>
        void CreateScriptInstances (int startParam, bool postOnRez, StateSource stateSource, UUID RezzedFrom,
                                   bool clearStateSaves);

        ArrayList GetScriptErrors (UUID itemID);
        void ResumeScripts ();

        /// <summary>
        ///     Stop all the scripts in this entity.
        /// </summary>
        /// <param name="sceneObjectBeingDeleted">
        ///     Should be true if these scripts are being removed because the scene
        ///     object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        void RemoveScriptInstances (bool sceneObjectBeingDeleted);

        /// <summary>
        ///     Start a script which is in this entity's inventory.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="stateSource"></param>
        void CreateScriptInstance (
            TaskInventoryItem item, int startParam, bool postOnRez, StateSource stateSource);

        /// <summary>
        ///     Start a script which is in this entity's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="stateSource"></param>
        void CreateScriptInstance (UUID itemId, int startParam, bool postOnRez, StateSource stateSource);

        /// <summary>
        ///     Updates a script instance in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="assetData"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="stateSource"></param>
        void UpdateScriptInstance (UUID itemId, byte [] assetData, int startParam, bool postOnRez, StateSource stateSource);

        /// <summary>
        ///     Stop a script which is in this prim's inventory.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="sceneObjectBeingDeleted">
        ///     Should be true if these scripts are being removed because the scene
        ///     object is being deleted.  This will prevent spurious updates to the client.
        /// </param>
        void RemoveScriptInstance (UUID itemId, bool sceneObjectBeingDeleted);

        /// <summary>
        ///     Add an item to this entity's inventory.  If an item with the same name already exists, then an alternative
        ///     name is chosen.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="allowedDrop"></param>
        void AddInventoryItem (TaskInventoryItem item, bool allowedDrop);

        /// <summary>
        ///     Add an item to this entity's inventory.  If an item with the same name already exists, it is replaced.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="allowedDrop"></param>
        void AddInventoryItemExclusive (TaskInventoryItem item, bool allowedDrop);

        /// <summary>
        ///     Restore a whole collection of items to the entity's inventory at once.
        ///     We assume that the items already have all their fields correctly filled out.
        ///     The items are not flagged for persistence to the database, since they are being restored
        ///     from persistence rather than being newly added.
        /// </summary>
        /// <param name="items"></param>
        void RestoreInventoryItems (ICollection<TaskInventoryItem> items);

        /// <summary>
        ///     Returns an existing inventory item.  Returns the original, so any changes will be live.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>null if the item does not exist</returns>
        TaskInventoryItem GetInventoryItem (UUID itemID);

        /// <summary>
        ///     Get inventory items by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>
        ///     A list of inventory items with that name.
        ///     If no inventory item has that name then an empty list is returned.
        /// </returns>
        IList<TaskInventoryItem> GetInventoryItems (string name);

        /// <summary>
        ///     Get all inventory items
        /// </summary>
        /// <returns>
        ///     A list of inventory items in this object
        /// </returns>
        List<TaskInventoryItem> GetInventoryItems ();

        /// <summary>
        ///     Get the scene object referenced by an inventory item.
        /// </summary>
        /// This is returned in a 'rez ready' state.  That is, name, description, permissions and other details have
        /// been adjusted to reflect the part and item from which it originates.
        /// <param name="item"></param>
        /// <returns>The scene object.  Null if the scene object asset couldn't be found</returns>
        ISceneEntity GetRezReadySceneObject (TaskInventoryItem item);

        /// <summary>
        ///     Update an existing inventory item.
        /// </summary>
        /// <param name="item">
        ///     The updated item.  An item with the same id must already exist
        ///     in this prim's inventory.
        /// </param>
        /// <returns>false if the item did not exist, true if the update occurred successfully</returns>
        bool UpdateInventoryItem (TaskInventoryItem item);

        bool UpdateInventoryItem (TaskInventoryItem item, bool fireScriptEvents);

        /// <summary>
        ///     Remove an item from this entity's inventory
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>
        ///     Numeric asset type of the item removed.  Returns -1 if the item did not exist
        ///     in this prim's inventory.
        /// </returns>
        int RemoveInventoryItem (UUID itemID);

        /// <summary>
        ///     Serialize all the metadata for the items in this prim's inventory ready for sending to the client
        /// </summary>
        /// <param name="client"></param>
        void RequestInventoryFile (IClientAPI client);

        uint MaskEffectivePermissions ();

        /// <summary>
        ///     Applies the next owner permissions
        /// </summary>
        void ApplyNextOwnerPermissions ();

        /// <summary>
        ///     Applies the given permissions (forced)
        /// </summary>
        /// <param name="perms"></param>
        void ApplyGodPermissions (uint perms);

        /// <summary>
        ///     Returns true if this inventory contains any scripts
        /// </summary>
        /// <returns></returns>
        bool ContainsScripts ();

        /// <summary>
        ///     Get the uuids of all items in this inventory
        /// </summary>
        /// <returns></returns>
        List<UUID> GetInventoryList ();

        /// <summary>
        ///     Save all script state saves for this object
        /// </summary>
        void SaveScriptStateSaves ();
    }
}
