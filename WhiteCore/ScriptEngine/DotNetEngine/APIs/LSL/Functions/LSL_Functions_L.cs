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



        public void llLookAt (LSL_Vector target, double strength, double damping)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            LookAt (target, strength, damping, m_host);
        }

        // 04122016 Fly-Man-
        // This function is unknown on the SL Wiki
        public void llLinkLookAt (LSL_Integer link, LSL_Vector target, double strength, double damping)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            List<ISceneChildEntity> parts = GetLinkParts (link);

            foreach (ISceneChildEntity part in parts)
                LookAt (target, strength, damping, part);
        }


        // 04122016 Fly-Man-
        // This function is unknown on the SL Wiki
        public void llLinkRotLookAt (LSL_Integer link, LSL_Rotation target, double strength, double damping)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            Quaternion rot = new Quaternion ((float)target.x, (float)target.y, (float)target.z, (float)target.s);
            List<ISceneChildEntity> parts = GetLinkParts (link);

            foreach (ISceneChildEntity part in parts)
                part.RotLookAt (rot, (float)strength, (float)damping);
        }

        public LSL_List llListSort (LSL_List src, int stride, int ascending)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List ();


            if (stride <= 0) {
                stride = 1;
            }
            return src.Sort (stride, ascending);
        }

        public LSL_Integer llList2Integer (LSL_List src, int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            if (index < 0) {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0) {
                return 0;
            }
            try {
                if (src.Data [index] is LSL_Integer)
                    return (LSL_Integer)src.Data [index];
                if (src.Data [index] is LSL_Float)
                    return Convert.ToInt32 (((LSL_Float)src.Data [index]).value);
                return new LSL_Integer (src.Data [index].ToString ());
            } catch (FormatException) {
                return 0;
            } catch (InvalidCastException) {
                return 0;
            }
        }

        public LSL_Float llList2Float (LSL_List src, int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            if (index < 0) {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0) {
                return 0.0;
            }
            try {
                if (src.Data [index] is LSL_Integer)
                    return Convert.ToDouble (((LSL_Integer)src.Data [index]).value);
                if (src.Data [index] is LSL_Float)
                    return Convert.ToDouble (((LSL_Float)src.Data [index]).value);
                if (src.Data [index] is LSL_String)
                    return Convert.ToDouble (((LSL_String)src.Data [index]).m_string);
                return Convert.ToDouble (src.Data [index]);
            } catch (FormatException) {
                return 0.0;
            } catch (InvalidCastException) {
                return 0.0;
            }
        }

        public LSL_String llList2String (LSL_List src, int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            if (index < 0) {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0) {
                return string.Empty;
            }
            return new LSL_String (src.Data [index].ToString ());
        }

        public LSL_String llList2Key (LSL_List src, int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            if (index < 0) {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0) {
                return "";
            }
            return src.Data [index].ToString ();
        }

        public LSL_Vector llList2Vector (LSL_List src, int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            if (index < 0) {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0) {
                return new LSL_Vector (0, 0, 0);
            }
            if (src.Data [index] is LSL_Vector) {
                return (LSL_Vector)src.Data [index];
            }
            return new LSL_Vector (src.Data [index].ToString ());
        }

        public LSL_Rotation llList2Rot (LSL_List src, int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation ();

            if (index < 0) {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0) {
                return new LSL_Rotation (0, 0, 0, 1);
            }
            if (src.Data [index] is LSL_Rotation) {
                return (LSL_Rotation)src.Data [index];
            }
            return new LSL_Rotation (src.Data [index].ToString ());
        }

        public LSL_List llList2List (LSL_List src, int start, int end)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List ();

            return src.GetSublist (start, end);
        }


        /// <summary>
        ///     Process the supplied list and return the
        ///     content of the list formatted as a comma
        ///     separated list. There is a space after
        ///     each comma.
        /// </summary>
        public LSL_String llList2CSV (LSL_List src)
        {
            string ret = string.Empty;
            int x = 0;

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            if (src.Data.Length > 0) {
                ret = src.Data [x++].ToString ();
                for (; x < src.Data.Length; x++) {
                    ret += ", " + src.Data [x].ToString ();
                }
            }

            return ret;
        }

        /// <summary>
        ///     Randomizes the list, be arbitrarily reordering
        ///     sublists of stride elements. As the stride approaches
        ///     the size of the list, the options become very
        ///     limited.
        /// </summary>
        /// <remarks>
        ///     This could take a while for very large list
        ///     sizes.
        /// </remarks>
        public LSL_List llListRandomize (LSL_List src, int stride)
        {
            LSL_List result;
            Random rand = new Random ();

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List ();


            if (stride <= 0) {
                stride = 1;
            }

            // Stride MUST be a factor of the list length
            // If not, then return the src list. This also
            // traps those cases where stride > length.

            if (src.Length != stride && src.Length % stride == 0) {
                int chunkk = src.Length / stride;

                int [] chunks = new int [chunkk];

                for (int i = 0; i < chunkk; i++)
                    chunks [i] = i;

                // Knuth shuffle the chunkk index
                for (int i = chunkk - 1; i >= 1; i--) {
                    // Elect an unrandomized chunk to swap
                    int index = rand.Next (i + 1);

                    // and swap position with first unrandomized chunk
                    int tmp = chunks [i];
                    chunks [i] = chunks [index];
                    chunks [index] = tmp;
                }

                // Construct the randomized list

                result = new LSL_List ();

                for (int i = 0; i < chunkk; i++) {
                    for (int j = 0; j < stride; j++) {
                        result.Add (src.Data [chunks [i] * stride + j]);
                    }
                }
            } else {
                object [] array = new object [src.Length];
                Array.Copy (src.Data, 0, array, 0, src.Length);
                result = new LSL_List (array);
            }

            return result;
        }

        /// <summary>
        ///     Elements in the source list starting with 0 and then
        ///     every i+stride. If the stride is negative then the scan
        ///     is backwards producing an inverted result.
        ///     Only those elements that are also in the specified
        ///     range are included in the result.
        /// </summary>
        public LSL_List llList2ListStrided (LSL_List src, int start, int end, int stride)
        {
            LSL_List result = new LSL_List ();
            int [] si = new int [2];
            int [] ei = new int [2];
            bool twopass = false;

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List ();


            //  First step is always to deal with negative indices

            if (start < 0)
                start = src.Length + start;
            if (end < 0)
                end = src.Length + end;

            //  Out of bounds indices are OK, just trim them
            //  accordingly

            if (start > src.Length)
                start = src.Length;

            if (end > src.Length)
                end = src.Length;

            if (stride == 0)
                stride = 1;

            //  There may be one or two ranges to be considered

            if (start != end) {
                if (start <= end) {
                    si [0] = start;
                    ei [0] = end;
                } else {
                    si [1] = start;
                    ei [1] = src.Length;
                    si [0] = 0;
                    ei [0] = end;
                    twopass = true;
                }

                //  The scan always starts from the beginning of the
                //  source list, but members are only selected if they
                //  fall within the specified sub-range. The specified
                //  range values are inclusive.
                //  A negative stride reverses the direction of the
                //  scan producing an inverted list as a result.

                if (stride > 0) {
                    for (int i = 0; i < src.Length; i += stride) {
                        if (i <= ei [0] && i >= si [0])
                            result.Add (src.Data [i]);
                        if (twopass && i >= si [1] && i <= ei [1])
                            result.Add (src.Data [i]);
                    }
                } else if (stride < 0) {
                    for (int i = src.Length - 1; i >= 0; i += stride) {
                        if (i <= ei [0] && i >= si [0])
                            result.Add (src.Data [i]);
                        if (twopass && i >= si [1] && i <= ei [1])
                            result.Add (src.Data [i]);
                    }
                }
            } else {
                if (start % stride == 0) {
                    result.Add (src.Data [start]);
                }
            }

            return result;
        }

        /// <summary>
        ///     Insert the list identified by &lt;src&gt; into the
        ///     list designated by &lt;dest&gt; such that the first
        ///     new element has the index specified by &lt;index&gt;
        /// </summary>
        public LSL_List llListInsertList (LSL_List dest, LSL_List src, int index)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List ();

            LSL_List pref = null;
            LSL_List suff = null;


            if (index < 0) {
                index = index + dest.Length;
                if (index < 0) {
                    index = 0;
                }
            }

            if (index != 0) {
                pref = dest.GetSublist (0, index - 1);
                if (index < dest.Length) {
                    suff = dest.GetSublist (index, -1);
                    return pref + src + suff;
                }
                return pref + src;
            }
            if (index < dest.Length) {
                suff = dest.GetSublist (index, -1);
                return src + suff;
            }
            return src;
        }

        /// <summary>
        ///     Returns the index of the first occurrence of test
        ///     in src.
        /// </summary>
        public LSL_Integer llListFindList (LSL_List src, LSL_List test)
        {
            int index = -1;
            int length = src.Length - test.Length + 1;

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            // If either list is empty, do not match
            if (src.Length != 0 && test.Length != 0) {
                for (int i = 0; i < length; i++) {
                    if (src.Data [i].Equals (test.Data [0]) || test.Data [0].Equals (src.Data [i])) {
                        int j;
                        for (j = 1; j < test.Length; j++)
                            if (!(src.Data [i + j].Equals (test.Data [j]) || test.Data [j].Equals (src.Data [i + j])))
                                break;

                        if (j == test.Length) {
                            index = i;
                            break;
                        }
                    }
                }
            }

            return index;
        }

        public void llLinkParticleSystem (int linknumber, LSL_List rules)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            List<ISceneChildEntity> parts = GetLinkParts (linknumber);

            foreach (var part in parts) {
                SetParticleSystem (part, rules);
            }
        }

        public void llLinkSitTarget (LSL_Integer link, LSL_Vector offset, LSL_Rotation rot)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (link == ScriptBaseClass.LINK_ROOT)
                SitTarget (m_host.ParentEntity.RootChild, offset, rot);
            else if (link == ScriptBaseClass.LINK_THIS)
                SitTarget (m_host, offset, rot);
            else {
                var entity = m_host.ParentEntity.GetLinkNumPart (link);
                if (entity != null) {
                    SitTarget ((ISceneChildEntity)entity, offset, rot);
                }
            }
        }


        /// <summary>
        ///     illListReplaceList removes the sub-list defined by the inclusive indices
        ///     start and end and inserts the src list in its place. The inclusive
        ///     nature of the indices means that at least one element must be deleted
        ///     if the indices are within the bounds of the existing list. I.e. 2,2
        ///     will remove the element at index 2 and replace it with the source
        ///     list. Both indices may be negative, with the usual interpretation. An
        ///     interesting case is where end is lower than start. As these indices
        ///     bound the list to be removed, then 0->end, and start->lim are removed
        ///     and the source list is added as a suffix.
        /// </summary>
        public LSL_List llListReplaceList (LSL_List dest, LSL_List src, int start, int end)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_List ();


            // Note that although we have normalized, both
            // indices could still be negative.
            if (start < 0) {
                start = start + dest.Length;
            }

            if (end < 0) {
                end = end + dest.Length;
            }
            // The comventional case, remove a sequence starting with
            // start and ending with end. And then insert the source
            // list.
            if (start <= end) {
                // If greater than zero, then there is going to be a
                // surviving prefix. Otherwise the inclusive nature
                // of the indices mean that we're going to add the
                // source list as a prefix.
                if (start > 0) {
                    LSL_List pref = dest.GetSublist (0, start - 1);
                    // Only add a suffix if there is something
                    // beyond the end index (it's inclusive too).
                    if (end + 1 < dest.Length) {
                        return pref + src + dest.GetSublist (end + 1, -1);
                    }
                    return pref + src;
                }
                // If start is less than or equal to zero, then
                // the new list is simply a prefix. We still need to
                // figure out any necessary surgery to the destination
                // based upon end. Note that if end exceeds the upper
                // bound in this case, the entire destination list
                // is removed.
                if (end + 1 < dest.Length) {
                    return src + dest.GetSublist (end + 1, -1);
                }
                return src;
            }
            // Finally, if start > end, we strip away a prefix and
            // a suffix, to leave the list that sits <between> ens
            // and start, and then tag on the src list. AT least
            // that's my interpretation. We can get sublist to do
            // this for us. Note that one, or both of the indices
            // might have been negative.
            return dest.GetSublist (end + 1, start - 1) + src;
        }

        public LSL_Float llListStatistics (int operation, LSL_List src)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            LSL_List nums = LSL_List.ToDoubleList (src);
            if (operation == ScriptBaseClass.LIST_STAT_RANGE)
                return nums.Range ();
            if (operation == ScriptBaseClass.LIST_STAT_MIN)
                return nums.Min ();
            if (operation == ScriptBaseClass.LIST_STAT_MAX)
                return nums.Max ();
            if (operation == ScriptBaseClass.LIST_STAT_MEAN)
                return nums.Mean ();
            if (operation == ScriptBaseClass.LIST_STAT_MEDIAN)
                return nums.Median ();
            if (operation == ScriptBaseClass.LIST_STAT_NUM_COUNT)
                return nums.NumericLength ();
            if (operation == ScriptBaseClass.LIST_STAT_STD_DEV)
                return nums.StdDev ();
            if (operation == ScriptBaseClass.LIST_STAT_SUM)
                return nums.Sum ();
            if (operation == ScriptBaseClass.LIST_STAT_SUM_SQUARES)
                return nums.SumSqrs ();
            if (operation == ScriptBaseClass.LIST_STAT_GEOMETRIC_MEAN)
                return nums.GeometricMean ();
            if (operation == ScriptBaseClass.LIST_STAT_HARMONIC_MEAN)
                return nums.HarmonicMean ();
            return 0.0;
        }



    }
}
