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
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using WhiteCore.ScriptEngine.DotNetEngine.Plugins;
using WhiteCore.ScriptEngine.DotNetEngine.Runtime;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using LSL_Float = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger;
using LSL_Key = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.list;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {
        public DateTime llAdjustSoundVolume (double volume)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            m_host.AdjustSoundGain (volume);
            return PScriptSleep (m_sleepMsOnAdjustSoundVolume);
        }

        public LSL_String llGetParcelMusicURL ()
        {
            ILandObject parcel =
                m_host.ParentEntity.Scene.RequestModuleInterface<IParcelManagementModule> ()
                      .GetLandObject (m_host.ParentEntity.AbsolutePosition.X, m_host.ParentEntity.AbsolutePosition.Y);
            return new LSL_String (parcel.LandData.MusicURL);
        }

        public LSL_Integer llListen (int channelID, string name, string ID, string msg)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            UUID keyID;
            UUID.TryParse (ID, out keyID);
            if (m_comms != null)
                return m_comms.Listen (m_itemID, m_host.UUID, channelID, name, keyID, msg, 0);
            return -1;
        }

        public void llListenControl (int number, int active)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_comms != null)
                m_comms.ListenControl (m_itemID, number, active);
        }

        public void llListenRemove (int number)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_comms != null)
                m_comms.ListenRemove (m_itemID, number);
        }


        // Xantor 20080528 we should do this differently.
        // 1) apply the sound to the object
        // 2) schedule full update
        // just sending the sound out once doesn't work so well when other avatars come in view later on
        // or when the prim gets moved, changed, sat on, whatever
        // see large number of mantises (mantes?)
        // 20080530 Updated to remove code duplication
        // 20080530 Stop sound if there is one, otherwise volume only changes don't work
        public void llLoopSound (string sound, double volume)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.Sound == KeyOrName (sound, AssetType.Sound, true))
                return;

            if (m_host.Sound != UUID.Zero)
                llStopSound ();

            m_host.Sound = KeyOrName (sound, AssetType.Sound, true);
            m_host.SoundGain = volume;
            m_host.SoundFlags = (byte)SoundFlags.Loop; // looping
            if (FloatAlmostEqual (m_host.SoundRadius, 0))
                m_host.SoundRadius = 20; // Magic number, 20 seems reasonable. Make configurable?

            m_host.ScheduleUpdate (PrimUpdateFlags.FindBest);
        }

        public void llLoopSoundMaster (string sound, double volume)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            lock (m_host.ParentEntity.LoopSoundSlavePrims) {
                m_host.ParentEntity.LoopSoundMasterPrim = m_host.UUID;
                foreach (UUID child in m_host.ParentEntity.LoopSoundSlavePrims) {
                    ISceneChildEntity part = m_host.ParentEntity.GetChildPart (child);
                    if (part == null)
                        continue;
                    part.Sound = KeyOrName (sound, AssetType.Sound, true);
                    part.SoundGain = volume;
                    part.AdjustSoundGain (volume);
                    part.SoundFlags = (byte)SoundFlags.Loop; // looping
                    if (FloatAlmostEqual (part.SoundRadius, 0))
                        part.SoundRadius = 20; // Magic number, 20 seems reasonable. Make configurable?

                    part.ScheduleUpdate (PrimUpdateFlags.FindBest);
                }
            }
            //if (m_host.Sound != UUID.Zero)
            //    llStopSound();

            m_host.AdjustSoundGain (volume);
            m_host.Sound = KeyOrName (sound, AssetType.Sound, true);
            m_host.SoundGain = volume;
            m_host.SoundFlags = (byte)SoundFlags.Loop; // looping
            if (FloatAlmostEqual (m_host.SoundRadius, 0))
                m_host.SoundRadius = 20; // Magic number, 20 seems reasonable. Make configurable?

            m_host.ScheduleUpdate (PrimUpdateFlags.ForcedFullUpdate);
        }

        public void llLoopSoundSlave (string sound, double volume)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            lock (m_host.ParentEntity.LoopSoundSlavePrims) {
                if (m_host.UUID != m_host.ParentEntity.LoopSoundMasterPrim &&
                    !m_host.ParentEntity.LoopSoundSlavePrims.Contains (m_host.UUID))//Can't set the master as a slave
                    m_host.ParentEntity.LoopSoundSlavePrims.Add (m_host.UUID);
            }
        }


        public void llSound (string sound, double volume, int queue, int loop)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            // This function has been deprecated
            // see http://www.lslwiki.net/lslwiki/wakka.php?wakka=llSound
            Deprecated ("llSound", "Use llPlaySound instead");
            if (loop == 1)
                llLoopSound (sound, volume);
            else
                llPlaySound (sound, volume);
            llSetSoundQueueing (queue);
        }

        // Xantor 20080528: Clear prim data of sound instead
        public void llStopSound ()
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.AdjustSoundGain (0);
            lock (m_host.ParentEntity.LoopSoundSlavePrims) {
                if (m_host.ParentEntity.LoopSoundMasterPrim == m_host.UUID) {
                    foreach (UUID child in m_host.ParentEntity.LoopSoundSlavePrims) {
                        ISceneChildEntity part = m_host.ParentEntity.GetChildPart (child);
                        if (part == null)
                            continue;
                        part.Sound = UUID.Zero;
                        part.SoundGain = 0;
                        part.SoundFlags = (byte)SoundFlags.None;
                        part.AdjustSoundGain (0);
                        part.ScheduleUpdate (PrimUpdateFlags.FindBest);
                    }
                    m_host.ParentEntity.LoopSoundMasterPrim = UUID.Zero;
                    m_host.ParentEntity.LoopSoundSlavePrims.Clear ();
                } else {
                    if (m_host.ParentEntity.LoopSoundSlavePrims.Contains (m_host.UUID)) {
                        m_host.Sound = UUID.Zero;
                        m_host.SoundGain = 0;
                        m_host.SoundFlags = (byte)SoundFlags.None;
                        m_host.ScheduleUpdate (PrimUpdateFlags.FindBest);
                    } else {
                        m_host.Sound = UUID.Zero;
                        m_host.SoundGain = 0;
                        m_host.SoundFlags = (byte)SoundFlags.Stop | (byte)SoundFlags.None;
                        m_host.ScheduleUpdate (PrimUpdateFlags.FindBest);
                    }
                }
            }
        }

        /// <summary>
        ///     llSoundPreload is deprecated. In SL this appears to do absolutely nothing
        ///     and is documented to have no delay.
        /// </summary>
        public void llSoundPreload (string sound)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;
            Deprecated ("llSoundPreload", "Use llPreloadSound instead");
        }

        public void llSetSoundQueueing (int queue)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.SetSoundQueueing (queue);
        }

        public void llSetSoundRadius (double radius)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.SoundRadius = radius;
        }

        public void llTriggerSound (string sound, double volume)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            // send the sound, once, to all clients in range
            m_host.SendSound (KeyOrName (sound, AssetType.Sound, true).ToString (), volume, true, 0, 0);
        }

        public void llTriggerSoundLimited (string sound, double volume, LSL_Vector top_north_east,
                          LSL_Vector bottom_south_west)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            double radius1 = (float)llVecDist (llGetPos (), top_north_east);
            double radius2 = (float)llVecDist (llGetPos (), bottom_south_west);
            double radius = Math.Abs (radius1 - radius2);
            m_host.SendSound (KeyOrName (sound, AssetType.Sound, true).ToString (), volume, true, 0, (float)radius);
        }



    }
}
