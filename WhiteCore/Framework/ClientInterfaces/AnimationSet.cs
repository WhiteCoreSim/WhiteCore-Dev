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

namespace WhiteCore.Framework.ClientInterfaces
{
    [Serializable]
    public class AnimationSet
    {
        public static AvatarAnimations Animations = new AvatarAnimations();

        readonly List<Animation> m_animations = new List<Animation>();
        Animation m_implicitDefaultAnimation = new Animation();
        Animation m_defaultAnimation = new Animation();
        readonly Dictionary<string, UUID> m_defaultAnimationOverrides = new Dictionary<string,UUID>();
        readonly Dictionary<string, string> m_defaultAnimationOverridesName = new Dictionary<string, string>();

        public AnimationSet(AvatarAnimations animations)
        {
            Animations = animations;
            ResetDefaultAnimation();
        }

        public Animation ImplicitDefaultAnimation
        {
            get { return m_implicitDefaultAnimation; }
        }

        public bool HasAnimation(UUID animID)
        {
            if (m_defaultAnimation.AnimID == animID)
                return true;

            return m_animations.Any(t => t.AnimID == animID);
        }

        public bool Add(UUID animID, int sequenceNum, UUID objectID)
        {
            lock (m_animations)
            {
                if (!HasAnimation(animID))
                {
                    m_animations.Add(new Animation(animID, sequenceNum, objectID));
                    return true;
                }
            }
            return false;
        }

        public bool Remove(UUID animID)
        {
            lock (m_animations)
            {
                if (m_defaultAnimation.AnimID == animID)
                {
                    ResetDefaultAnimation();
                }
                else if (HasAnimation(animID))
                {
                    for (int i = 0; i < m_animations.Count; i++)
                    {
                        if (m_animations[i].AnimID == animID)
                        {
                            m_animations.RemoveAt(i);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void Clear()
        {
            ResetDefaultAnimation();
            lock (m_animations) {
                m_animations.Clear ();
            }
        }

        /// <summary>
        ///     The default animation is reserved for "main" animations
        ///     that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        public bool SetDefaultAnimation(UUID animID, int sequenceNum, UUID objectID)
        {
            if (m_defaultAnimation.AnimID != animID)
            {
                m_defaultAnimation = new Animation(animID, sequenceNum, objectID);
                m_implicitDefaultAnimation = m_defaultAnimation;
                return true;
            }
            return false;
        }

        protected bool ResetDefaultAnimation()
        {
            return TrySetDefaultAnimation("STAND", 1, UUID.Zero);
        }

        /// <summary>
        ///     Set the animation as the default animation if it's known
        /// </summary>
        public bool TrySetDefaultAnimation(string anim, int sequenceNum, UUID objectID)
        {
            UUID uuid;
            if (m_defaultAnimationOverrides.TryGetValue(anim, out uuid) ||
                Animations.AnimsUUID.TryGetValue(anim, out uuid))
            {
                return SetDefaultAnimation(uuid, sequenceNum, objectID);
            }
            return false;
        }

        public void SetDefaultAnimationOverride(string anim_state, UUID animID, string animation)
        {
            m_defaultAnimationOverrides[anim_state] = animID;
            m_defaultAnimationOverridesName[anim_state] = animation;
        }

        public void ResetDefaultAnimationOverride(string anim_state)
        {
            if (anim_state == "ALL")
            {
                m_defaultAnimationOverrides.Clear();
                m_defaultAnimationOverridesName.Clear();
            }
            else
            {
                m_defaultAnimationOverrides.Remove(anim_state);
                m_defaultAnimationOverridesName.Remove(anim_state);
            }
        }

        public string GetDefaultAnimationOverride(string anim_state)
        {
            string anim = "";
            if (!m_defaultAnimationOverridesName.TryGetValue(anim_state, out anim))
            {
                UUID animID;
                if (!Animations.AnimsUUID.TryGetValue(anim_state, out animID))
                    anim = "";
                else
                    return anim_state;
            }
            return anim;
        }

        public void GetArrays(out UUID[] animIDs, out int[] sequenceNums, out UUID[] objectIDs)
        {
            lock (m_animations)
            {
                int defaultSize = 0;
                if (m_defaultAnimation.AnimID != UUID.Zero)
                {
                    defaultSize++;
                }
                else if (m_animations.Count == 0)
                {
                    defaultSize++;
                }

                animIDs = new UUID[m_animations.Count + defaultSize];
                sequenceNums = new int[m_animations.Count + defaultSize];
                objectIDs = new UUID[m_animations.Count + defaultSize];

                if (m_defaultAnimation.AnimID != UUID.Zero)
                {
                    animIDs[0] = m_defaultAnimation.AnimID;
                    sequenceNums[0] = m_defaultAnimation.SequenceNum;
                    objectIDs[0] = m_defaultAnimation.ObjectID;
                }
                else if (m_animations.Count == 0)
                {
                    animIDs[0] = m_implicitDefaultAnimation.AnimID;
                    sequenceNums[0] = m_implicitDefaultAnimation.SequenceNum;
                    objectIDs[0] = m_implicitDefaultAnimation.ObjectID;
                }

                for (int i = 0; i < m_animations.Count; ++i)
                {
                    animIDs[i + 1] = m_animations[i].AnimID;
                    sequenceNums[i + 1] = m_animations[i].SequenceNum;
                    objectIDs[i + 1] = m_animations[i].ObjectID;
                }
            }
        }

        public Animation[] ToArray()
        {
            lock (m_animations) {
                Animation [] theArray = new Animation [m_animations.Count];
                uint i = 0;
                try {
                    foreach (Animation anim in m_animations)
                        theArray [i++] = anim;
                } catch {
                    /* S%^t happens. Ignore. */
                }

                return theArray;
            }
        }

        public void FromArray(Animation[] theArray)
        {
            if (theArray == null)
                return;
   
            lock (m_animations) {
                foreach (Animation anim in theArray)
                    m_animations.Add (anim);
            }
        }
    }
}