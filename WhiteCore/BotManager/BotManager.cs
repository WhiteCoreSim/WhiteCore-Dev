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
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;
using WhiteCore.ScriptEngine.DotNetEngine.Runtime;


namespace WhiteCore.BotManager
{
    public class BotManager : INonSharedRegionModule, IBotManager
    {
        readonly Dictionary<UUID, Bot> m_bots = new Dictionary<UUID, Bot>();
        IScene m_scene;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(IScene scene)
        {
            m_scene = scene;
            scene.RegisterModuleInterface<IBotManager>(this);
            scene.RegisterModuleInterface(this);
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void RegionLoaded(IScene scene)
        {
        }

        public void Close()
        {
            m_bots.Clear();
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return GetType().AssemblyQualifiedName; }
        }

        #endregion

        #region IBotManager

        /// <summary>
        ///     Creates a new bot in world
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="scene"></param>
        /// <param name="cloneAppearanceFrom">UUID of the avatar who's appearance will be copied to give this bot an appearance</param>
        /// <param name="creatorID"></param>
        /// <param name="startPos"></param>
        /// <returns>ID of the bot</returns>
        public UUID CreateAvatar(string firstName, string lastName, IScene scene, UUID cloneAppearanceFrom,
            UUID creatorID, Vector3 startPos)
        {
            AvatarAppearance avatarApp = GetAppearance(cloneAppearanceFrom, scene) ?? new AvatarAppearance { Wearables = AvatarWearable.DefaultWearables };
            return CreateAvatar (firstName, lastName, scene, avatarApp, creatorID, startPos);

        }

        public UUID CreateAvatar(string firstName, string lastName, IScene scene, AvatarAppearance avatarApp,
            UUID creatorID, Vector3 startPos)
        {
            //Add the circuit data so they can login
            AgentCircuitData m_aCircuitData = new AgentCircuitData
            {
                IsChildAgent = false,
                CircuitCode = (uint) Util.RandomClass.Next()
            };
                    
            //Create the new bot data
            BotClientAPI m_character = new BotClientAPI(scene, m_aCircuitData);
            m_character.Name = firstName + " " + lastName;
            m_aCircuitData.AgentID = m_character.AgentId;

            //Set up appearance
            var origOwner = avatarApp.Owner;
            avatarApp.Owner = m_character.AgentId;
            List<AvatarAttachment> attachments = avatarApp.GetAttachments();

            avatarApp.ClearAttachments();
            // get original attachments
            foreach (AvatarAttachment t in attachments)
            {
                InventoryItemBase item = scene.InventoryService.GetItem(origOwner, t.ItemID);
                if (item != null)
                {
                    item.ID = UUID.Random();
                    item.Owner = m_character.AgentId;
                    item.Folder = UUID.Zero;
                    scene.InventoryService.AddCacheItemAsync(item);
                    //Now fix the ItemID
                    avatarApp.SetAttachment(t.AttachPoint, item.ID, t.AssetID);
                }
            }

            scene.AuthenticateHandler.AgentCircuits.Add(m_character.CircuitCode, m_aCircuitData);
            //This adds them to the scene and sets them in world
            AddAndWaitUntilAgentIsAdded(scene, m_character);

            IScenePresence SP = scene.GetScenePresence(m_character.AgentId);
            if (SP == null)
                return UUID.Zero; //Failed!

            IAvatarAppearanceModule appearance = SP.RequestModuleInterface<IAvatarAppearanceModule>();
            appearance.Appearance = avatarApp;
            appearance.InitialHasWearablesBeenSent = true;
            Bot bot = new Bot();
            bot.Initialize(SP, creatorID);
            SP.MakeRootAgent(startPos, false, true);
            //Move them
            SP.Teleport(startPos);

            foreach (var presence in scene.GetScenePresences())
                presence.SceneViewer.QueuePresenceForUpdate(SP, PrimUpdateFlags.ForcedFullUpdate);
            IAttachmentsModule attModule = SP.Scene.RequestModuleInterface<IAttachmentsModule>();
            if (attModule != null)
                foreach (AvatarAttachment att in attachments)
                    attModule.RezSingleAttachmentFromInventory(SP.ControllingClient, att.ItemID, att.AssetID, 0, true);

            //Save them in the bots list
            m_bots.Add(m_character.AgentId, bot);
            AddTagToBot(m_character.AgentId, "AllBots", bot.AvatarCreatorID);

            MainConsole.Instance.InfoFormat("[BotManager]: Added bot {0} to region {1}",
                m_character.Name, scene.RegionInfo.RegionName);

            //Return their UUID
            return m_character.AgentId;
        }
            
        static void AddAndWaitUntilAgentIsAdded(IScene scene, BotClientAPI m_character)
        {
            bool done = false;
            scene.AddNewClient(m_character, delegate { done = true; });
            while (!done)
                Thread.Sleep(3);
        }

        public void RemoveAvatar(UUID avatarID, IScene scene, UUID userAttempting)
        {
            IEntity sp = scene.GetScenePresence(avatarID);
            if (sp == null)
            {
                sp = scene.GetSceneObjectPart(avatarID);
                if (sp == null)
                    return;
                sp = ((ISceneChildEntity) sp).ParentEntity;
            }
            if (!CheckPermission(sp, userAttempting))
                return;

            RemoveAllTagsFromBot(avatarID, userAttempting);

            if (!m_bots.Remove(avatarID))
                return;

            //Kill the agent
            IEntityTransferModule module = scene.RequestModuleInterface<IEntityTransferModule>();
            module.IncomingCloseAgent(scene, avatarID);

            // clean up leftovers...
            var avService = scene.AvatarService;
            avService.ResetAvatar (avatarID);

            var rootFolder = scene.InventoryService.GetRootFolder(avatarID);
            if (rootFolder != null)
                scene.InventoryService.ForcePurgeFolder (rootFolder);

            MainConsole.Instance.InfoFormat("[BotManager]: Removed bot {0} from region {1}",
                sp.Name, scene.RegionInfo.RegionName);

        }

        public bool SetAvatarAppearance(UUID botID, AvatarAppearance avatarApp, IScene scene)
        {
            Bot bot;
            //Find the bot
            if (!m_bots.TryGetValue (botID, out bot))
                return false;

            var origOwner = avatarApp.Owner;
            avatarApp.Owner = botID;

            List<AvatarAttachment> attachments = avatarApp.GetAttachments();

            avatarApp.ClearAttachments();
            // get original attachments
            foreach (AvatarAttachment t in attachments)
            {
                InventoryItemBase item = scene.InventoryService.GetItem(origOwner, t.ItemID);
                if (item != null)
                {
                    item.ID = UUID.Random();
                    item.Owner = botID;
                    item.Folder = UUID.Zero;
                    scene.InventoryService.AddCacheItemAsync(item);
                    //Now fix the ItemID
                    avatarApp.SetAttachment(t.AttachPoint, item.ID, t.AssetID);
                }
            }

            IScenePresence SP = scene.GetScenePresence(botID);
            if (SP == null)
                return false;   // Failed! bot not found??

            IAvatarAppearanceModule appearance = SP.RequestModuleInterface<IAvatarAppearanceModule>();
            appearance.Appearance = avatarApp;
            appearance.InitialHasWearablesBeenSent = true;

            return true;
        }

        public void PauseMovement(UUID botID, UUID userAttempting)
        {
            Bot bot;
            //Find the bot
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.PauseMovement();
            }
        }

        public void ResumeMovement(UUID botID, UUID userAttempting)
        {
            Bot bot;
            //Find the bot
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.ResumeMovement();
            }
        }

        /// <summary>
        ///     Sets up where the bot should be walking
        /// </summary>
        /// <param name="botID">ID of the bot</param>
        /// <param name="positions">List of positions the bot will move to</param>
        /// <param name="mode">List of what the bot should be doing in between the positions</param>
        /// <param name="flags"></param>
        /// <param name="userAttempting"></param>
        public void SetBotMap(UUID botID, List<Vector3> positions, List<TravelMode> mode, int flags, UUID userAttempting)
        {
            Bot bot;
            //Find the bot
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.SetPath(positions, mode, flags);
            }
        }

        /// <summary>
        ///     Speed up or slow down the bot
        /// </summary>
        /// <param name="botID"></param>
        /// <param name="modifier"></param>
        /// <param name="userAttempting"></param>
        public void SetMovementSpeedMod(UUID botID, float modifier, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.SetMovementSpeedMod(modifier);
            }
        }

        public void SetBotShouldFly(UUID botID, bool shouldFly, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                if (shouldFly)
                    bot.DisableWalk();
                else
                    bot.EnableWalk();
            }
        }

        #region Tag/Remove bots

        readonly Dictionary<string, List<UUID>> m_botTags = new Dictionary<string, List<UUID>>();

        public void AddTagToBot(UUID Bot, string tag, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(Bot, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
            }
            if (!m_botTags.ContainsKey(tag))
                m_botTags.Add(tag, new List<UUID>());
            m_botTags[tag].Add(Bot);
        }

        public List<UUID> GetBotsWithTag(string tag)
        {
            if (!m_botTags.ContainsKey(tag))
                return new List<UUID>();
            return new List<UUID>(m_botTags[tag]);
        }

        public void RemoveBots(string tag, UUID userAttempting)
        {
            List<UUID> bots = GetBotsWithTag(tag);
            foreach (UUID bot in bots)
            {
                Bot Bot;
                if (m_bots.TryGetValue(bot, out Bot))
                {
                    if (!CheckPermission(Bot, userAttempting))
                        continue;
                    RemoveTagFromBot(bot, tag, userAttempting);
                    RemoveAvatar(bot, Bot.Controller.GetScene(), userAttempting);
                }
            }
        }

        public void RemoveTagFromBot(UUID Bot, string tag, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(Bot, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
            }
            if (m_botTags.ContainsKey(tag))
                m_botTags[tag].Remove(Bot);
        }

        public void RemoveAllTagsFromBot(UUID Bot, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(Bot, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
            }
            List<string> tagsToRemove = new List<string>();
            foreach (KeyValuePair<string, List<UUID>> kvp in m_botTags)
            {
                if (kvp.Value.Contains(Bot))
                    tagsToRemove.Add(kvp.Key);
            }
            foreach (string tag in tagsToRemove)
                m_botTags[tag].Remove(Bot);
        }

        #endregion

        /// <summary>
        ///     Finds the given users appearance
        /// </summary>
        /// <param name="target"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        AvatarAppearance GetAppearance(UUID target, IScene scene)
        {
            IScenePresence sp = scene.GetScenePresence(target);
            if (sp != null)
            {
                IAvatarAppearanceModule aa = sp.RequestModuleInterface<IAvatarAppearanceModule>();
                if (aa != null)
                    return new AvatarAppearance(aa.Appearance);
            }
            return scene.AvatarService.GetAppearance(target);
        }

        bool CheckPermission(IEntity sp, UUID userAttempting)
        {
            foreach (Bot bot in m_bots.Values)
            {
                if (bot.Controller.UUID == sp.UUID)
                    return bot.AvatarCreatorID == userAttempting;
            }
            return false;
        }

        bool CheckPermission(Bot bot, UUID userAttempting)
        {
            if (userAttempting == UUID.Zero)        // override for system bots
                return true; 
            if (bot != null)
            {
                if (bot.AvatarCreatorID == userAttempting)      // bot owner
                    return true;
                //else
                    //throw new Exception("Bot permission error, you cannot control this bot");

            }
            return false;
        }

        /// <summary>
        /// Checks for permission to command a bot.
        /// </summary>
        /// <returns><c>true</c>, if allowed, <c>false</c> otherwise.</returns>
        /// <param name="botID">BotID.</param>
        /// <param name="userAttempting">User attempting.</param>
        public bool CheckPermission(UUID botID, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                return CheckPermission (bot, userAttempting);
            }
            return false;
        }
            
        #endregion

        #region IBotManager

        /// <summary>
        ///     Begins to follow the given user
        /// </summary>
        /// <param name="botID"></param>
        /// <param name="avatarName"></param>
        /// <param name="startFollowDistance"></param>
        /// <param name="endFollowDistance"></param>
        /// <param name="requireLOS"></param>
        /// <param name="offsetFromAvatar"></param>
        /// <param name="userAttempting"></param>
        public void FollowAvatar(UUID botID, string avatarName, float startFollowDistance, float endFollowDistance,
                                 bool requireLOS, Vector3 offsetFromAvatar, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.FollowAvatar(avatarName, startFollowDistance, endFollowDistance, offsetFromAvatar, requireLOS);
            }
        }
            
        public void SetSpeed(UUID botID, UUID userAttempting, float speedModifier)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;

                IScenePresence avatar = m_scene.GetScenePresence(botID);
                if (avatar != null)
                    avatar.SpeedModifier = speedModifier;
            }
        }

        public void MoveToTarget(UUID botID, Vector3 destination, int options, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;

                bot.m_nodeGraph.Clear ();

                if ((options & ScriptBaseClass.OS_NPC_NO_FLY) != 0)
                {
                    bot.m_nodeGraph.Add (destination, TravelMode.Walk);
                    bot.WalkTo (destination);
                } else
                {
                    bot.m_nodeGraph.Add (destination, TravelMode.Fly);
                    bot.FlyTo (destination);
                }
            }
        }

        public void StopMoving(UUID botID, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;

                var flying = bot.lastFlying;
                bot.Controller.StopMoving (flying, false);

            }
        }

        public void WalkTo(UUID botID, Vector3 destination, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;

                bot.m_nodeGraph.Clear ();
                bot.m_nodeGraph.Add (destination, TravelMode.Walk);

                bot.WalkTo (destination);
            }
        }

        /// <summary>
        ///     Stops following the given user
        /// </summary>
        /// <param name="botID"></param>
        /// <param name="userAttempting"></param>
        public void StopFollowAvatar(UUID botID, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.StopFollowAvatar();
            }
        }

        /// <summary>
        ///     Sends a chat message to all clients
        /// </summary>
        /// <param name="botID"></param>
        /// <param name="message"></param>
        /// <param name="sayType"></param>
        /// <param name="channel"></param>
        /// <param name="userAttempting"></param>
        public void SendChatMessage(UUID botID, string message, int sayType, int channel, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.SendChatMessage(sayType, message, channel);
            }
        }

        /// <summary>
        ///     Sends a chat message to all clients
        /// </summary>
        /// <param name="botID"></param>
        /// <param name="toUser"></param>
        /// <param name="message"></param>
        /// <param name="userAttempting"></param>
        public void SendIM(UUID botID, UUID toUser, string message, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return;
                bot.SendInstantMessage(new GridInstantMessage
                                           {
                                               BinaryBucket = new byte[0],
                                               Dialog = (byte) InstantMessageDialog.MessageFromAgent,
                                               Message = message,
                                               FromAgentID = botID,
                                               FromAgentName = bot.Controller.Name,
                                               FromGroup = false,
                                               SessionID = UUID.Random(),
                                               Offline = 0,
                                               ParentEstateID = 0,
                                               RegionID = bot.Controller.GetScene().RegionInfo.RegionID,
                                               Timestamp = (uint) Util.UnixTimeSinceEpoch(),
                                               ToAgentID = toUser
                                           });
            }
        }


        #endregion

        #region helpers

        /// <summary>
        /// Gets the owner.
        /// </summary>
        /// <returns>The owner ID.</returns>
        /// <param name="botID">BotID.</param>
        public UUID GetOwner(UUID botID)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                return bot.AvatarCreatorID;
            }

            return UUID.Zero;
        }

        /// <summary>
        /// Gets the bot position.
        /// </summary>
        /// <returns>The position.</returns>
        /// <param name="botID">Bot ID.</param>
        /// <param name="userAttempting">User attempting.</param>
        public Vector3 GetPosition(UUID botID, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return new Vector3(0,0,0);

                return bot.Controller.AbsolutePosition;
            }
            return new Vector3(0,0,0);
        }

        public Quaternion GetRotation(UUID botID, UUID userAttempting)
        {
            Bot bot;
            if (m_bots.TryGetValue(botID, out bot))
            {
                if (!CheckPermission(bot, userAttempting))
                    return new Quaternion(0,0,0);

                return bot.Controller.PhysicsActor.Orientation;

            }
            return new Quaternion(0,0,0);
        }

        #endregion

        #region Character Management

        public void CreateCharacter(UUID primID, IScene scene)
        {
            RemoveCharacter(primID);
            ISceneEntity entity = scene.GetSceneObjectPart(primID).ParentEntity;
            Bot bot = new Bot();
            bot.Initialize(entity);

            m_bots.Add(primID, bot);
            AddTagToBot(primID, "AllBots", bot.AvatarCreatorID);
        }

        public IBotController GetCharacterManager(UUID primID)
        {
            foreach (Bot bot in m_bots.Values)
            {
                if (bot.Controller.UUID == primID)
                    return bot.Controller;
            }
            return null;
        }

        public void RemoveCharacter(UUID primID)
        {
            if (m_bots.ContainsKey(primID))
            {
                Bot b = m_bots[primID];
                b.Close(true);
                RemoveAllTagsFromBot(primID, UUID.Zero);
                m_bots.Remove(primID);
            }
        }

        #endregion
    }
}