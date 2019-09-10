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
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.List;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {
        // Xantor 20080528 PlaySound updated so it accepts an objectinventory name -or- a key to a sound
        // 20080530 Updated to remove code duplication
        public void llPlaySound(string sound, double volume) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            // send the sound, once, to all clients in range
            m_host.SendSound(KeyOrName(sound, AssetType.Sound, true).ToString(), volume, false, 0, 0);
        }


        public void llPlaySoundSlave(string sound, double volume) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            // send the sound, once, to all clients in range
            //This kinda works, but I haven't found a way to be able to tell the client to sync it with the looping master sound
            if (m_host.ParentEntity.LoopSoundMasterPrim != UUID.Zero) {
                ISceneChildEntity part = m_host.ParentEntity.GetChildPart(m_host.ParentEntity.LoopSoundMasterPrim);
                if (part != null) {
                    foreach (IScenePresence sp in m_host.ParentEntity.Scene.GetScenePresences()) {
                        //sp.ControllingClient.SendPlayAttachedSound(part.Sound, part.UUID, part.OwnerID, (float)part.SoundGain, (byte)SoundFlags.Stop);
                        sp.ControllingClient.SendPlayAttachedSound(
                            KeyOrName(sound, AssetType.Sound, true),
                            m_host.UUID,
                            m_host.OwnerID,
                            (float)(FloatAlmostEqual(m_host.SoundGain, 0) ? 1.0 : m_host.SoundGain),
                            (byte)(SoundFlags.Queue | SoundFlags.SyncMaster));

                        sp.ControllingClient.SendPlayAttachedSound(
                            part.Sound,
                            part.UUID,
                            part.OwnerID,
                            (float)part.SoundGain,
                            (byte)(SoundFlags.Queue | SoundFlags.Loop | SoundFlags.SyncSlave));
                    }
                    //if (part.Sound != UUID.Zero)
                    //    part.SendSound(part.Sound.ToString(), part.SoundGain, false, (int)(SoundFlags.Loop | SoundFlags.Queue), (float)part.SoundRadius);
                }
            } else
                m_host.SendSound(KeyOrName(sound, AssetType.Sound, true).ToString(), volume, false, 0, 0);
        }

        public DateTime llPreloadSound(string sound) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            m_host.PreloadSound(sound);
            return PScriptSleep(m_sleepMsOnPreloadSound);
        }

        public void llPointAt(LSL_Vector pos) {
        }

        public void llPassTouches(int pass) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.PassTouch = pass;
        }

        public void llPushObject(string target, LSL_Vector impulse, LSL_Vector ang_impulse, int local) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            bool pushAllowed = false;

            bool pusheeIsAvatar = false;
            UUID targetID = UUID.Zero;

            if (!UUID.TryParse(target, out targetID))
                return;

            IScenePresence pusheeav = null;
            Vector3 PusheePos = Vector3.Zero;
            ISceneChildEntity pusheeob = null;

            IScenePresence avatar = World.GetScenePresence(targetID);
            if (avatar != null) {
                pusheeIsAvatar = true;

                // Pushee is in GodMode this pushing object isn't owned by them
                if (avatar.GodLevel > 0 && m_host.OwnerID != targetID)
                    return;

                pusheeav = avatar;

                // Find pushee position
                // Pushee Linked?
                if (pusheeav.ParentID != UUID.Zero) {
                    ISceneChildEntity parentobj = World.GetSceneObjectPart(pusheeav.ParentID);
                    PusheePos = parentobj != null ? parentobj.AbsolutePosition : pusheeav.AbsolutePosition;
                } else {
                    PusheePos = pusheeav.AbsolutePosition;
                }
            }

            if (!pusheeIsAvatar) {
                // not an avatar so push is not affected by parcel flags
                pusheeob = World.GetSceneObjectPart(UUID.Parse(target));

                // We can't find object
                if (pusheeob == null)
                    return;

                // Object not pushable.  Not an attachment and has no physics component
                if (!pusheeob.IsAttachment && pusheeob.PhysActor == null)
                    return;

                PusheePos = pusheeob.AbsolutePosition;
                pushAllowed = true;
            } else {
                IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
                if (World.RegionInfo.RegionSettings.RestrictPushing) {
                    pushAllowed = m_host.OwnerID == targetID ||
                                  m_host.ParentEntity.Scene.Permissions.IsGod(m_host.OwnerID);
                } else {
                    if (parcelManagement != null) {
                        ILandObject targetlandObj = parcelManagement.GetLandObject(PusheePos.X, PusheePos.Y);
                        if (targetlandObj == null)
                            // We didn't find the parcel but region isn't push restricted so assume it's ok
                            pushAllowed = true;
                        else {
                            // Parcel push restriction
                            pushAllowed = (targetlandObj.LandData.Flags & (uint)ParcelFlags.RestrictPushObject) !=
                                          (uint)ParcelFlags.RestrictPushObject ||
                                          m_host.ParentEntity.Scene.Permissions.CanPushObject(m_host.OwnerID,
                                                                                              targetlandObj);
                        }
                    }
                }
            }

            if (pushAllowed) {
                float distance = (PusheePos - m_host.AbsolutePosition).Length();
                float distance_term = distance * distance * distance; // Script Energy
                float pusher_mass = m_host.GetMass();

                const float PUSH_ATTENUATION_DISTANCE = 17f;
                const float PUSH_ATTENUATION_SCALE = 5f;
                float distance_attenuation = 1f;
                if (distance > PUSH_ATTENUATION_DISTANCE) {
                    float normalized_units = 1f + (distance - PUSH_ATTENUATION_DISTANCE) / PUSH_ATTENUATION_SCALE;
                    distance_attenuation = 1f / normalized_units;
                }

                Vector3 applied_linear_impulse = new Vector3((float)impulse.x, (float)impulse.y, (float)impulse.z);
                {
                    float impulse_length = applied_linear_impulse.Length();

                    float desired_energy = impulse_length * pusher_mass;
                    if (desired_energy > 0f)
                        desired_energy += distance_term;

                    float scaling_factor = 1f;
                    scaling_factor *= distance_attenuation;
                    applied_linear_impulse *= scaling_factor;
                }
                if (pusheeIsAvatar) {
                    if (pusheeav != null) {
                        PhysicsActor pa = pusheeav.PhysicsActor;

                        if (pa != null) {
                            if (local != 0) {
                                applied_linear_impulse *= m_host.GetWorldRotation();
                            }
                            //Put a limit on it...
                            int MaxPush = (int)pusheeav.PhysicsActor.Mass * 25;

                            if (applied_linear_impulse.X > 0 &&
                                Math.Abs(applied_linear_impulse.X) > MaxPush)
                                applied_linear_impulse.X = MaxPush;
                            if (applied_linear_impulse.X < 0 &&
                                Math.Abs(applied_linear_impulse.X) > MaxPush)
                                applied_linear_impulse.X = -MaxPush;

                            if (applied_linear_impulse.Y > 0 &&
                                Math.Abs(applied_linear_impulse.X) > MaxPush)
                                applied_linear_impulse.Y = MaxPush;
                            if (applied_linear_impulse.Y < 0 &&
                                Math.Abs(applied_linear_impulse.Y) > MaxPush)
                                applied_linear_impulse.Y = -MaxPush;

                            if (applied_linear_impulse.Z > 0 &&
                                Math.Abs(applied_linear_impulse.X) > MaxPush)
                                applied_linear_impulse.Z = MaxPush;
                            if (applied_linear_impulse.Z < 0 &&
                                Math.Abs(applied_linear_impulse.Z) > MaxPush)
                                applied_linear_impulse.Z = -MaxPush;

                            pa.AddForce(applied_linear_impulse, true);
                        }
                    }
                } else {
                    if (pusheeob.PhysActor != null) {
                        pusheeob.ApplyImpulse(applied_linear_impulse, local != 0);
                    }
                }
            }
        }

        public void llPassCollisions(int pass) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.PassCollisions = pass;
        }

        public void llParticleSystem(LSL_List rules) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            SetParticleSystem(m_host, rules);
        }

        //  <summary>
        //  Scan the string supplied in 'src' and tokenize it based upon two sets of
        //  tokenizers provided in two lists, separators and spacers.
        //  </summary>
        //
        //  <remarks>
        //  Separators demarcate tokens and are elided as they are encountered. Spacers
        //  also demarcate tokens, but are themselves retained as tokens.
        //
        //  Both separators and spacers may be arbitrarily long strings. i.e. ":::".
        //
        //  The function returns an ordered list representing the tokens found in the supplied
        //  sources string. If two successive tokenizers are encountered, then a NULL entry is added
        //  to the list.
        //
        //  It is a precondition that the source and toekizer lisst are non-null. If they are null,
        //  then a null pointer exception will be thrown while their lengths are being determined.
        //
        //  A small amount of working memoryis required of approximately 8*#tokenizers.
        //
        //  There are many ways in which this function can be implemented, this implementation is
        //  fairly naive and assumes that when the function is invooked with a short source
        //  string and/or short lists of tokenizers, then performance will not be an issue.
        //
        //  In order to minimize the perofrmance effects of long strings, or large numbers
        //  of tokeizers, the function skips as far as possible whenever a toekenizer is found,
        //  and eliminates redundant tokenizers as soon as is possible.
        //
        //  The implementation tries to avoid any copying of arrays or other objects.
        //  </remarks>

        LSL_List ParseString(string src, LSL_List separators, LSL_List spacers, bool keepNulls) {
            int beginning = 0;
            int srclen = src.Length;
            int seplen = separators.Length;
            object[] separray = separators.Data;
            int spclen = spacers.Length;
            object[] spcarray = spacers.Data;
            int mlen = seplen + spclen;

            int[] offset = new int[mlen + 1];
            bool[] active = new bool[mlen];

            //    Initial capacity reduces resize cost

            LSL_List tokens = new LSL_List();

            //    All entries are initially valid

            for (int i = 0; i < mlen; i++)
                active[i] = true;

            offset[mlen] = srclen;

            while (beginning < srclen) {
                int best = mlen;

                //    Scan for separators

                int j;
                for (j = 0; j < seplen; j++) {
                    if (separray[j].ToString() == string.Empty)
                        active[j] = false;

                    if (active[j]) {
                        // scan all of the markers
                        if ((offset[j] = src.IndexOf(separray[j].ToString(), beginning, StringComparison.Ordinal)) == -1) {
                            // not present at all
                            active[j] = false;
                        } else {
                            // present and correct
                            if (offset[j] < offset[best]) {
                                // closest so far
                                best = j;
                                if (offset[best] == beginning)
                                    break;
                            }
                        }
                    }
                }

                //    Scan for spacers

                if (offset[best] != beginning) {
                    for (j = seplen; (j < mlen) && (offset[best] > beginning); j++) {
                        if (spcarray[j - seplen].ToString() == string.Empty)
                            active[j] = false;

                        if (active[j]) {
                            // scan all of the markers
                            if ((offset[j] = src.IndexOf(spcarray[j - seplen].ToString(), beginning, StringComparison.Ordinal)) == -1) {
                                // not present at all
                                active[j] = false;
                            } else {
                                // present and correct
                                if (offset[j] < offset[best]) {
                                    // closest so far
                                    best = j;
                                }
                            }
                        }
                    }
                }

                //    This is the normal exit from the scanning loop

                if (best == mlen) {
                    // no markers were found on this pass
                    // so we're pretty much done
                    if ((keepNulls) || ((srclen - beginning) > 0))
                        tokens.Add(new LSL_String(src.Substring(beginning, srclen - beginning)));
                    break;
                }

                //    Otherwise we just add the newly delimited token
                //    and recalculate where the search should continue.
                if ((keepNulls) || ((offset[best] - beginning) > 0))
                    tokens.Add(new LSL_String(src.Substring(beginning, offset[best] - beginning)));

                if (best < seplen) {
                    beginning = offset[best] + (separray[best].ToString()).Length;
                } else {
                    beginning = offset[best] + (spcarray[best - seplen].ToString()).Length;
                    string str = spcarray[best - seplen].ToString();
                    if ((keepNulls) || ((str.Length > 0)))
                        tokens.Add(new LSL_String(str));
                }
            }

            //    This an awkward an not very intuitive boundary case. If the
            //    last substring is a tokenizer, then there is an implied trailing
            //    null list entry. Hopefully the single comparison will not be too
            //    arduous. Alternatively the 'break' could be replced with a return
            //    but that's shabby programming.

            if ((beginning == srclen) && (keepNulls)) {
                if (srclen != 0)
                    tokens.Add(new LSL_String(""));
            }

            return tokens;
        }

        public LSL_List llParseString2List(string src, LSL_List separators, LSL_List spacers) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "llParseString2List", m_host, "LSL", m_itemID))
                return new LSL_List();
            return ParseString(src, separators, spacers, false);
        }

        public LSL_List llParseStringKeepNulls(string src, LSL_List separators, LSL_List spacers) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "llParseStringKeepNulls", m_host, "LSL", m_itemID))
                return new LSL_List();
            return ParseString(src, separators, spacers, true);
        }

        public DateTime llParcelMediaCommandList(LSL_List commandList) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;


            // according to the docs, this command only works if script owner and land owner are the same
            // lets add estate owners and gods, too, and use the generic permission check.
            IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement != null) {
                ILandObject landObject = parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                        m_host.AbsolutePosition.Y);
                if (landObject == null)
                    return DateTime.Now;
                if (!World.Permissions.CanEditParcel(m_host.OwnerID, landObject))
                    return DateTime.Now;

                bool update = false; // send a ParcelMediaUpdate (and possibly change the land's media URL)?
                byte loop = 0;

                LandData landData = landObject.LandData;
                string url = landData.MediaURL;
                string texture = landData.MediaID.ToString();
                bool autoAlign = landData.MediaAutoScale != 0;
                string mediaType = landData.MediaType;
                string description = landData.MediaDescription;
                int width = landData.MediaWidth;
                int height = landData.MediaHeight;
                float mediaLoopSet = landData.MediaLoopSet;

                ParcelMediaCommandEnum? commandToSend = null;
                float time = 0.0f; // default is from start

                IScenePresence presence = null;

                for (int i = 0; i < commandList.Data.Length; i++) {
                    int tmp = ((LSL_Integer)commandList.Data[i]).value;
                    ParcelMediaCommandEnum command = (ParcelMediaCommandEnum)tmp;
                    switch (command) {
                        case ParcelMediaCommandEnum.Agent:
                            // we send only to one agent
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_String) {
                                    UUID agentID;
                                    if (UUID.TryParse((LSL_String)commandList.Data[i + 1], out agentID)) {
                                        presence = World.GetScenePresence(agentID);
                                    }
                                } else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_AGENT must be a key");
                                ++i;
                            }
                            break;

                        case ParcelMediaCommandEnum.Loop:
                            loop = 1;
                            commandToSend = command;
                            update = true; //need to send the media update packet to set looping
                            break;

                        case ParcelMediaCommandEnum.LoopSet:
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_Float) {
                                    mediaLoopSet = (float)((LSL_Float)commandList.Data[i + 1]).value;
                                } else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_LOOP_SET must be a float");
                                ++i;
                            }
                            commandToSend = command;
                            break;

                        case ParcelMediaCommandEnum.Play:
                            loop = 0;
                            commandToSend = command;
                            update = true; //need to send the media update packet to make sure it doesn't loop
                            break;

                        case ParcelMediaCommandEnum.Pause:
                        case ParcelMediaCommandEnum.Stop:
                        case ParcelMediaCommandEnum.Unload:
                            commandToSend = command;
                            break;

                        case ParcelMediaCommandEnum.Url:
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_String) {
                                    url = (LSL_String)commandList.Data[i + 1];
                                    update = true;
                                } else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_URL must be a string.");
                                ++i;
                            }
                            break;

                        case ParcelMediaCommandEnum.Texture:
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_String) {
                                    texture = (LSL_String)commandList.Data[i + 1];
                                    update = true;
                                } else
                                    Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TEXTURE must be a string or key.");
                                ++i;
                            }
                            break;

                        case ParcelMediaCommandEnum.Time:
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_Float) {
                                    time = (float)(LSL_Float)commandList.Data[i + 1];
                                } else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TIME must be a float.");
                                ++i;
                            }
                            commandToSend = command;
                            break;

                        case ParcelMediaCommandEnum.AutoAlign:
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_Integer) {
                                    autoAlign = (LSL_Integer)commandList.Data[i + 1];
                                    update = true;
                                } else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_AUTO_ALIGN must be an integer.");
                                ++i;
                            }
                            break;

                        case ParcelMediaCommandEnum.Type:
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_String) {
                                    mediaType = (LSL_String)commandList.Data[i + 1];
                                    update = true;
                                } else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TYPE must be a string.");
                                ++i;
                            }
                            break;

                        case ParcelMediaCommandEnum.Desc:
                            if ((i + 1) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_String) {
                                    description = (LSL_String)commandList.Data[i + 1];
                                    update = true;
                                } else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_DESC must be a string.");
                                ++i;
                            }
                            break;
                        case ParcelMediaCommandEnum.Size:
                            if ((i + 2) < commandList.Length) {
                                if (commandList.Data[i + 1] is LSL_Integer) {
                                    if (commandList.Data[i + 2] is LSL_Integer) {
                                        width = (LSL_Integer)commandList.Data[i + 1];
                                        height = (LSL_Integer)commandList.Data[i + 2];
                                        update = true;
                                    } else
                                        Error("llParcelMediaCommandList", "The second argument of PARCEL_MEDIA_COMMAND_SIZE must be an integer.");
                                } else Error("llParcelMediaCommandList", "The first argument of PARCEL_MEDIA_COMMAND_SIZE must be an integer.");
                                i += 2;
                            }
                            break;

                        default:
                            NotImplemented("llParcelMediaCommandList", "Parameter not supported yet: " +
                                           Enum.Parse(typeof(ParcelMediaCommandEnum), commandList.Data[i].ToString()));
                            break;
                    } //end switch
                } //end for

                // if we didn't get a presence, we send to all and change the url
                // if we did get a presence, we only send to the agent specified, and *don't change the land settings*!

                // did something important change or do we only start/stop/pause?
                if (update) {
                    if (presence == null) {
                        // we send to all
                        landData.MediaID = new UUID(texture);
                        landData.MediaAutoScale = autoAlign ? (byte)1 : (byte)0;
                        landData.MediaWidth = width;
                        landData.MediaHeight = height;
                        landData.MediaType = mediaType;
                        landData.MediaDescription = description;
                        landData.MediaLoop = loop == 1;
                        landData.MediaLoopSet = mediaLoopSet;

                        // do that one last, it will cause a ParcelPropertiesUpdate
                        landObject.SetMediaUrl(url);

                        // now send to all (non-child) agents
                        World.ForEachScenePresence(delegate (IScenePresence sp) {
                            if (!sp.IsChildAgent &&
                                (sp.CurrentParcelUUID == landData.GlobalID)) {
                                sp.ControllingClient.SendParcelMediaUpdate(
                                    landData.MediaURL,
                                    landData.MediaID,
                                    landData.MediaAutoScale,
                                    mediaType,
                                    description,
                                    width, height,
                                    loop);
                            }
                        });
                    } else if (!presence.IsChildAgent) {
                        // we only send to one (root) agent
                        presence.ControllingClient.SendParcelMediaUpdate(url,
                                                                         new UUID(texture),
                                                                         autoAlign ? (byte)1 : (byte)0,
                                                                         mediaType,
                                                                         description,
                                                                         width, height,
                                                                         loop);
                    }
                }

                if (commandToSend != null) {
                    float ParamToSend = time;
                    if (commandToSend == ParcelMediaCommandEnum.LoopSet)
                        ParamToSend = mediaLoopSet;

                    // the commandList contained a start/stop/... command, too
                    if (presence == null) {
                        // send to all (non-child) agents
                        World.ForEachScenePresence(delegate (IScenePresence sp) {
                            if (!sp.IsChildAgent) {
                                sp.ControllingClient.SendParcelMediaCommand(
                                    landData.Flags,
                                    (ParcelMediaCommandEnum)commandToSend,
                                    ParamToSend);
                            }
                        });
                    } else if (!presence.IsChildAgent) {
                        presence.ControllingClient.SendParcelMediaCommand(landData.Flags,
                                                                          (ParcelMediaCommandEnum)commandToSend,
                                                                          ParamToSend);
                    }
                }
            }
            return PScriptSleep(m_sleepMsOnParcelMediaCommandList);
        }

        public LSL_List llParcelMediaQuery(LSL_List aList) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List();

            LSL_List list = new LSL_List();
            foreach (object t in aList.Data) {
                if (t != null) {
                    IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
                    if (parcelManagement != null) {
                        LSL_Integer tmp = (LSL_Integer)t;
                        switch ((ParcelMediaCommandEnum)tmp.value) {
                            case ParcelMediaCommandEnum.Url:
                                list.Add(
                                    new LSL_String(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y).LandData.MediaURL));
                                break;
                            case ParcelMediaCommandEnum.Desc:
                                list.Add(
                                    new LSL_String(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y)
                                                        .LandData.MediaDescription));
                                break;
                            case ParcelMediaCommandEnum.Texture:
                                list.Add(
                                    new LSL_String(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y)
                                                        .LandData.MediaID.ToString()));
                                break;
                            case ParcelMediaCommandEnum.Type:
                                list.Add(
                                    new LSL_String(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y).LandData.MediaType));
                                break;
                            case ParcelMediaCommandEnum.Loop:
                                list.Add(
                                    new LSL_Integer(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y).LandData.MediaLoop
                                            ? 1
                                            : 0));
                                break;
                            case ParcelMediaCommandEnum.LoopSet:
                                list.Add(
                                    new LSL_Integer(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y).LandData.MediaLoopSet));
                                break;
                            case ParcelMediaCommandEnum.Size:
                                list.Add(
                                    new LSL_String(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y).LandData.MediaHeight));
                                list.Add(
                                    new LSL_String(
                                        parcelManagement.GetLandObject(m_host.AbsolutePosition.X,
                                                                       m_host.AbsolutePosition.Y).LandData.MediaWidth));
                                break;
                            default:
                                const ParcelMediaCommandEnum mediaCommandEnum = ParcelMediaCommandEnum.Url;
                                NotImplemented("llParcelMediaQuery", "Parameter not supported yet: " +
                                               Enum.Parse(mediaCommandEnum.GetType(), t.ToString()));
                                break;
                        }
                    }
                }
            }
            PScriptSleep(m_sleepMsOnParcelMediaQuery);
            return list;
        }


        public void llPursue(LSL_String target, LSL_List options) {
            IBotManager botManager = World.RequestModuleInterface<IBotManager>();
            if (botManager != null) {
                float fuzz = 2;
                Vector3 offset = Vector3.Zero;
                bool requireLOS = false;
                // 20131224 not used                bool intercept;  = false; //Not implemented
                for (int i = 0; i < options.Length; i += 2) {
                    LSL_Integer opt = options.GetLSLIntegerItem(i);
                    if (opt == ScriptBaseClass.PURSUIT_FUZZ_FACTOR)
                        fuzz = (float)options.GetLSLFloatItem(i + 1).value;
                    if (opt == ScriptBaseClass.PURSUIT_OFFSET)
                        offset = options.GetVector3Item(i + 1).ToVector3();
                    if (opt == ScriptBaseClass.REQUIRE_LINE_OF_SIGHT)
                        requireLOS = options.GetLSLIntegerItem(i + 1) == 1;
                    // 20131224 not used                    if (opt == ScriptBaseClass.PURSUIT_INTERCEPT)
                    // 20131224 not used                        intercept = options.GetLSLIntegerItem(i + 1) == 1;
                }
                botManager.FollowAvatar(m_host.ParentEntity.UUID, target.m_string, fuzz, fuzz, requireLOS, offset,
                                        m_host.ParentEntity.OwnerID);
            }
        }





    }
}
