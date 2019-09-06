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
        // convert a LSL_Rotation to a Quaternion
        protected Quaternion Rot2Quaternion (LSL_Rotation r)
        {
            Quaternion q = new Quaternion ((float)r.x, (float)r.y, (float)r.z, (float)r.s);
            q.Normalize ();
            return q;
        }

        //These are the implementations of the various ll-functions used by the LSL scripts.
        public LSL_Float llSin (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Sin (f);
        }

        public LSL_Float llCos (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Cos (f);
        }

        public LSL_Float llTan (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Tan (f);
        }

        public LSL_Float llAtan2 (double x, double y)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Atan2 (x, y);
        }

        public LSL_Float llSqrt (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Sqrt (f);
        }

        public LSL_Float llPow (double fbase, double fexponent)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Pow (fbase, fexponent);
        }

        public LSL_Integer llAbs (int i)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            // changed to replicate LSL behaviour whereby minimum int value is returned untouched.
            if (i == int.MinValue)
                return i;
            return Math.Abs (i);
        }

        public LSL_Float llFabs (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Abs (f);
        }

        public LSL_Float llFrand (double mag)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Util.RandomClass.NextDouble () * mag;
        }

        public LSL_Integer llFloor (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            return (int)Math.Floor (f);
        }

        public LSL_Integer llCeil (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            return (int)Math.Ceiling (f);
        }

        // Xantor 01/May/2008 fixed midpointrounding (2.5 becomes 3.0 instead of 2.0, default = ToEven)
        public LSL_Integer llRound (double f)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer ();

            double RoundedNumber = Math.Round (f, MidpointRounding.AwayFromZero);
            //Attempt to fix rounded numbers like -4.5 arounding away from zero
            if (f < 0) {
                if (FloatAlmostEqual (f + 0.5, RoundedNumber) || FloatAlmostEqual (f - 0.5, RoundedNumber)) {
                    RoundedNumber += 1;
                }
            }
            return (int)RoundedNumber;
        }

        //This next group are vector operations involving squaring and square root. ckrinke
        public LSL_Float llVecMag (LSL_Vector v)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return LSL_Vector.Mag (v);
        }

        public LSL_Vector llVecNorm (LSL_Vector v)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();
            return LSL_Vector.Norm (v);
        }

        public LSL_Float llVecDist (LSL_Vector a, LSL_Vector b)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return Math.Sqrt (dx * dx + dy * dy + dz * dz);
        }

        //Now we start getting into quaternions which means sin/cos, matrices and vectors. ckrinke

        // Old implementation of llRot2Euler. Normalization not required as Atan2 function will
        // only return values >= -PI (-180 degrees) and <= PI (180 degrees).

        public LSL_Vector llRot2Euler (LSL_Rotation r)
        {
            //This implementation is from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions. ckrinke
            LSL_Rotation t = new LSL_Rotation (r.x * r.x, r.y * r.y, r.z * r.z, r.s * r.s);
            double m = (t.x + t.y + t.z + t.s);
            if (FloatAlmostEqual (m, 0))
                return new LSL_Vector ();

            double n = 2 * (r.y * r.s + r.x * r.z);
            double p = m * m - n * n;
            if (p > 0)
                return new LSL_Vector (Math.Atan2 (2.0 * (r.x * r.s - r.y * r.z), (-t.x - t.y + t.z + t.s)),
                                      Math.Atan2 (n, Math.Sqrt (p)),
                                      Math.Atan2 (2.0 * (r.z * r.s - r.x * r.y), (t.x - t.y - t.z + t.s)));
            if (n > 0)
                return new LSL_Vector (0.0, Math.PI * 0.5, Math.Atan2 ((r.z * r.s + r.x * r.y), 0.5 - t.x - t.z));
            return new LSL_Vector (0.0, -Math.PI * 0.5, Math.Atan2 ((r.z * r.s + r.x * r.y), 0.5 - t.x - t.z));
        }

        /* From wiki:
        The Euler angle vector (in radians) is converted to a rotation by doing the rotations around the 3 axes
        in Z, Y, X order. So llEuler2Rot(<1.0, 2.0, 3.0> * DEG_TO_RAD) generates a rotation by taking the zero rotation,
        a vector pointing along the X axis, first rotating it 3 degrees around the global Z axis, then rotating the resulting
        vector 2 degrees around the global Y axis, and finally rotating that 1 degree around the global X axis.
        */

        /* How we arrived at this llEuler2Rot
         *
         * Experiment in SL to determine conventions:
         *   llEuler2Rot(<PI,0,0>)=<1,0,0,0>
         *   llEuler2Rot(<0,PI,0>)=<0,1,0,0>
         *   llEuler2Rot(<0,0,PI>)=<0,0,1,0>
         *
         * Important facts about Quaternions
         *  - multiplication is non-commutative (a*b != b*a)
         *  - http://en.wikipedia.org/wiki/Quaternion#Basis_multiplication
         *
         * Above SL experiment gives (c1,c2,c3,s1,s2,s3 as defined in our llEuler2Rot):
         *   Qx = c1+i*s1
         *   Qy = c2+j*s2;
         *   Qz = c3+k*s3;
         *
         * Rotations applied in order (from above) Z, Y, X
         * Q = (Qz * Qy) * Qx
         * ((c1+i*s1)*(c2+j*s2))*(c3+k*s3)
         * (c1*c2+i*s1*c2+j*c1*s2+ij*s1*s2)*(c3+k*s3)
         * (c1*c2+i*s1*c2+j*c1*s2+k*s1*s2)*(c3+k*s3)
         * c1*c2*c3+i*s1*c2*c3+j*c1*s2*c3+k*s1*s2*c3+k*c1*c2*s3+ik*s1*c2*s3+jk*c1*s2*s3+kk*s1*s2*s3
         * c1*c2*c3+i*s1*c2*c3+j*c1*s2*c3+k*s1*s2*c3+k*c1*c2*s3 -j*s1*c2*s3 +i*c1*s2*s3   -s1*s2*s3
         * regroup: x=i*(s1*c2*c3+c1*s2*s3)
         *          y=j*(c1*s2*c3-s1*c2*s3)
         *          z=k*(s1*s2*c3+c1*c2*s3)
         *          s=   c1*c2*c3-s1*s2*s3
         *
         * This implementation agrees with the functions found here:
         * http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions
         * And with the results in SL.
         *
         * It's also possible to calculate llEuler2Rot by direct multiplication of
         * the Qz, Qy, and Qx vectors (as above - and done in the "accurate" function
         * from the wiki).
         * Apparently in some cases this is better from a numerical precision perspective?
         */

        public LSL_Rotation llEuler2Rot (LSL_Vector v)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation ();


            double c1 = Math.Cos (v.x * 0.5);
            double c2 = Math.Cos (v.y * 0.5);
            double c3 = Math.Cos (v.z * 0.5);
            double s1 = Math.Sin (v.x * 0.5);
            double s2 = Math.Sin (v.y * 0.5);
            double s3 = Math.Sin (v.z * 0.5);

            double x = s1 * c2 * c3 + c1 * s2 * s3;
            double y = c1 * s2 * c3 - s1 * c2 * s3;
            double z = s1 * s2 * c3 + c1 * c2 * s3;
            double s = c1 * c2 * c3 - s1 * s2 * s3;

            return new LSL_Rotation (x, y, z, s);
        }

        public LSL_Rotation llAxes2Rot (LSL_Vector fwd, LSL_Vector left, LSL_Vector up)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation ();

            double s;
            double tr = fwd.x + left.y + up.z + 1.0;

            if (tr >= 1.0) {
                s = 0.5 / Math.Sqrt (tr);
                return new LSL_Rotation (
                    (left.z - up.y) * s,
                    (up.x - fwd.z) * s,
                    (fwd.y - left.x) * s,
                    0.25 / s);
            }
            double max = (left.y > up.z) ? left.y : up.z;

            if (max < fwd.x) {
                s = Math.Sqrt (fwd.x - (left.y + up.z) + 1.0);
                double x = s * 0.5;
                s = 0.5 / s;
                return new LSL_Rotation (
                    x,
                    (fwd.y + left.x) * s,
                    (up.x + fwd.z) * s,
                    (left.z - up.y) * s);
            }
            if (FloatAlmostEqual (max, left.y)) {
                s = Math.Sqrt (left.y - (up.z + fwd.x) + 1.0);
                double y = s * 0.5;
                s = 0.5 / s;
                return new LSL_Rotation (
                    (fwd.y + left.x) * s,
                    y,
                    (left.z + up.y) * s,
                    (up.x - fwd.z) * s);
            }
            s = Math.Sqrt (up.z - (fwd.x + left.y) + 1.0);
            double z = s * 0.5;
            s = 0.5 / s;
            return new LSL_Rotation (
                (up.x + fwd.z) * s,
                (left.z + up.y) * s,
                z,
                (fwd.y - left.x) * s);
        }

        public LSL_Vector llRot2Fwd (LSL_Rotation r)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();


            double m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs (1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt (m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            double x = r.x * r.x - r.y * r.y - r.z * r.z + r.s * r.s;
            double y = 2 * (r.x * r.y + r.z * r.s);
            double z = 2 * (r.x * r.z - r.y * r.s);
            return (new LSL_Vector (x, y, z));
        }

        public LSL_Vector llRot2Left (LSL_Rotation r)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();


            double m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs (1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt (m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            double x = 2 * (r.x * r.y - r.z * r.s);
            double y = -r.x * r.x + r.y * r.y - r.z * r.z + r.s * r.s;
            double z = 2 * (r.x * r.s + r.y * r.z);
            return (new LSL_Vector (x, y, z));
        }

        public LSL_Vector llRot2Up (LSL_Rotation r)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            double m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs (1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt (m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            double x = 2 * (r.x * r.z + r.y * r.s);
            double y = 2 * (-r.x * r.s + r.y * r.z);
            double z = -r.x * r.x - r.y * r.y + r.z * r.z + r.s * r.s;
            return (new LSL_Vector (x, y, z));
        }

        public LSL_Rotation llRotBetween (LSL_Vector a, LSL_Vector b)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Rotation ();
            //A and B should both be normalized

            LSL_Rotation rotBetween;
            // Check for zero vectors. If either is zero, return zero rotation. Otherwise,
            // continue calculation.
            if (a == new LSL_Vector (0.0f, 0.0f, 0.0f) || b == new LSL_Vector (0.0f, 0.0f, 0.0f)) {
                rotBetween = new LSL_Rotation (0.0f, 0.0f, 0.0f, 1.0f);
            } else {
                a = LSL_Vector.Norm (a);
                b = LSL_Vector.Norm (b);
                double dotProduct = LSL_Vector.Dot (a, b);
                // There are two degenerate cases possible. These are for vectors 180 or
                // 0 degrees apart. These have to be detected and handled individually.
                //
                // Check for vectors 180 degrees apart.
                // A dot product of -1 would mean the angle between vectors is 180 degrees.
                if (dotProduct < -0.9999999f) {
                    // First assume X axis is orthogonal to the vectors.
                    LSL_Vector orthoVector = new LSL_Vector (1.0f, 0.0f, 0.0f);
                    orthoVector = orthoVector - a * (a.x / LSL_Vector.Dot (a, a));
                    // Check for near zero vector. A very small non-zero number here will create
                    // a rotation in an undesired direction.
                    rotBetween = LSL_Vector.Mag (orthoVector) > 0.0001
                                     ? new LSL_Rotation (orthoVector.x, orthoVector.y, orthoVector.z, 0.0f)
                                     : new LSL_Rotation (0.0f, 0.0f, 1.0f, 0.0f);
                }
                // Check for parallel vectors.
                // A dot product of 1 would mean the angle between vectors is 0 degrees.
                else if (dotProduct > 0.9999999f) {
                    // Set zero rotation.
                    rotBetween = new LSL_Rotation (0.0f, 0.0f, 0.0f, 1.0f);
                } else {
                    // All special checks have been performed so get the axis of rotation.
                    LSL_Vector crossProduct = LSL_Vector.Cross (a, b);
                    // Quarternion s value is the length of the unit vector + dot product.
                    double qs = 1.0 + dotProduct;
                    rotBetween = new LSL_Rotation (crossProduct.x, crossProduct.y, crossProduct.z, qs);
                    // Normalize the rotation.
                    double mag = LSL_Rotation.Mag (rotBetween);
                    // We shouldn't have to worry about a divide by zero here. The qs value will be
                    // non-zero because we already know if we're here, then the dotProduct is not -1 so
                    // qs will not be zero. Also, we've already handled the input vectors being zero so the
                    // crossProduct vector should also not be zero.
                    rotBetween.x = rotBetween.x / mag;
                    rotBetween.y = rotBetween.y / mag;
                    rotBetween.z = rotBetween.z / mag;
                    rotBetween.s = rotBetween.s / mag;
                    // Check for undefined values and set zero rotation if any found. This code might not actually be required
                    // any longer since zero vectors are checked for at the top.
                    if (double.IsNaN (rotBetween.x) || double.IsNaN (rotBetween.y) ||
                        double.IsNaN (rotBetween.z) || double.IsNaN (rotBetween.s)) {
                        rotBetween = new LSL_Rotation (0.0f, 0.0f, 0.0f, 1.0f);
                    }
                }
            }
            return rotBetween;
        }


        // Xantor 29/apr/2008
        // converts a Quaternion to X,Y,Z axis rotations
        public LSL_Vector llRot2Axis (LSL_Rotation rot)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            double x, y, z;

            if (rot.s > 1) // normalization needed
            {
                double length = Math.Sqrt (rot.x * rot.x + rot.y * rot.y +
                                          rot.z * rot.z + rot.s * rot.s);
                if (FloatAlmostEqual (length, 0))
                    return new LSL_Vector (0, 0, 0);
                length = 1 / length;
                rot.x *= length;
                rot.y *= length;
                rot.z *= length;
                rot.s *= length;
            }

            // double angle = 2 * Math.Acos(rot.s);
            double s = Math.Sqrt (1 - rot.s * rot.s);
            if (s < 0.001) {
                x = 1;
                y = z = 0;
            } else {
                s = 1 / s;
                x = rot.x * s; // normalise axis
                y = rot.y * s;
                z = rot.z * s;
            }

            return new LSL_Vector (x, y, z);
        }


        // Returns the angle of a quaternion (see llRot2Axis for the axis)
        public LSL_Float llRot2Angle (LSL_Rotation rot)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();


            if (rot.s > 1) // normalization needed
            {
                double length = Math.Sqrt (rot.x * rot.x + rot.y * rot.y +
                                          rot.z * rot.z + rot.s * rot.s);

                if (FloatAlmostEqual (length, 0))
                    return 0;
                //                rot.x /= length;
                //                rot.y /= length;
                //                rot.z /= length;
                rot.s /= length;
            }

            double angle = 2 * Math.Acos (rot.s);

            return angle;
        }

        public LSL_Float llAcos (double val)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Acos (val);
        }

        public LSL_Float llAsin (double val)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Asin (val);
        }

        public LSL_Float llAngleBetween (LSL_Rotation a, LSL_Rotation b)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();


            double aa = (a.x * a.x + a.y * a.y + a.z * a.z + a.s * a.s);
            double bb = (b.x * b.x + b.y * b.y + b.z * b.z + b.s * b.s);
            double aa_bb = aa * bb;
            if (FloatAlmostEqual (aa_bb, 0))
                return 0.0;
            double ab = (a.x * b.x + a.y * b.y + a.z * b.z + a.s * b.s);
            double quotient = (ab * ab) / aa_bb;
            if (quotient >= 1.0) return 0.0;
            return Math.Acos (2 * quotient - 1);
        }

        public LSL_Float llLog10 (double val)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Log10 (val);
        }

        public LSL_Float llLog (double val)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            return Math.Log (val);
        }


        //  <remarks>
        //  <para>
        //  The .NET definition of base 64 is:
        //  <list>
        //  <item>
        //  Significant: A-Z a-z 0-9 + -
        //  </item>
        //  <item>
        //  Whitespace: \t \n \r ' '
        //  </item>
        //  <item>
        //  Valueless: =
        //  </item>
        //  <item>
        //  End-of-string: \0 or '=='
        //  </item>
        //  </list>
        //  </para>
        //  <para>
        //  Each point in a base-64 string represents a 6 bit value. A 32-bit integer can be
        //  represented using 6 characters (with some redundancy).
        //  </para>
        //  <para>
        //  LSL requires a base64 string to be 8 characters in length. LSL also uses '/'
        //  rather than '-' (MIME compliant).
        //  </para>
        //  <para>
        //  RFC 1341 used as a reference (as specified by the SecondLife Wiki).
        //  </para>
        //  <para>
        //  SL do not record any kind of exception for these functions, so the string to integer
        //  conversion returns '0' if an invalid character is encountered during conversion.
        //  </para>
        //  <para>
        //  References
        //  <list>
        //  <item>
        //  http://lslwiki.net/lslwiki/wakka.php?wakka=Base64
        //  </item>
        //  <item>
        //  </item>
        //  </list>
        //  </para>
        //  </remarks>

        //  <summary>
        //  Table for converting 6-bit integers into
        //  base-64 characters
        //  </summary>

        protected static readonly char [] i2ctable =
            {
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
                'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
                'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
                'Y', 'Z',
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h',
                'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
                'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
                'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7',
                '8', '9',
                '+', '/'
            };

        //  <summary>
        //  Table for converting base-64 characters
        //  into 6-bit integers.
        //  </summary>

        protected static readonly int [] c2itable =
            {
                -1, -1, -1, -1, -1, -1, -1, -1, // 0x
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // 1x
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // 2x
                -1, -1, -1, 63, -1, -1, -1, 64,
                53, 54, 55, 56, 57, 58, 59, 60, // 3x
                61, 62, -1, -1, -1, 0, -1, -1,
                -1, 1, 2, 3, 4, 5, 6, 7, // 4x
                8, 9, 10, 11, 12, 13, 14, 15,
                16, 17, 18, 19, 20, 21, 22, 23, // 5x
                24, 25, 26, -1, -1, -1, -1, -1,
                -1, 27, 28, 29, 30, 31, 32, 33, // 6x
                34, 35, 36, 37, 38, 39, 40, 41,
                42, 43, 44, 45, 46, 47, 48, 49, // 7x
                50, 51, 52, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // 8x
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // 9x
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // Ax
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // Bx
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // Cx
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // Dx
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // Ex
                -1, -1, -1, -1, -1, -1, -1, -1,
                -1, -1, -1, -1, -1, -1, -1, -1, // Fx
                -1, -1, -1, -1, -1, -1, -1, -1
            };

        //  <summary>
        //  Converts a 32-bit integer into a Base64
        //  character string. Base64 character strings
        //  are always 8 characters long. All iinteger
        //  values are acceptable.
        //  </summary>
        //  <param name="number">
        //  32-bit integer to be converted.
        //  </param>
        //  <returns>
        //  8 character string. The 1st six characters
        //  contain the encoded number, the last two
        //  characters are padded with "=".
        //  </returns>

        public LSL_String llIntegerToBase64 (int number)
        {
            // uninitialized string

            char [] imdt = new char [8];

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";


            // Manually unroll the loop

            imdt [7] = '=';
            imdt [6] = '=';
            imdt [5] = i2ctable [number << 4 & 0x3F];
            imdt [4] = i2ctable [number >> 2 & 0x3F];
            imdt [3] = i2ctable [number >> 8 & 0x3F];
            imdt [2] = i2ctable [number >> 14 & 0x3F];
            imdt [1] = i2ctable [number >> 20 & 0x3F];
            imdt [0] = i2ctable [number >> 26 & 0x3F];

            return new string (imdt);
        }

        //  <summary>
        //  Converts an eight character base-64 string into a 32-bit integer.
        //  </summary>
        //  <param name="str">
        //  8 characters string to be converted. Other length strings return zero.
        //  </param>
        //  <returns>
        //  Returns an integer representing the encoded value providedint he 1st 6
        //  characters of the string.
        //  </returns>
        //  <remarks>
        //  This is coded to behave like LSL's implementation (I think), based upon the
        //  information available at the Wiki. If more than 8 characters are supplied,
        //  zero is returned. If a NULL string is supplied, zero will
        //  be returned. If fewer than 6 characters are supplied, then
        //  the answer will reflect a partial accumulation.
        //  <para>
        //  The 6-bit segments are extracted left-to-right in big-endian mode,
        //  which means that segment 6 only contains the
        //  two low-order bits of the 32 bit integer as
        //  its high order 2 bits. A short string therefore
        //  means loss of low-order information. E.g.
        //
        //  |<---------------------- 32-bit integer ----------------------->|<-Pad->|
        //  |<--Byte 0----->|<--Byte 1----->|<--Byte 2----->|<--Byte 3----->|<-Pad->|
        //  |3|3|2|2|2|2|2|2|2|2|2|2|1|1|1|1|1|1|1|1|1|1| | | | | | | | | | |P|P|P|P|
        //  |1|0|9|8|7|6|5|4|3|2|1|0|9|8|7|6|5|4|3|2|1|0|9|8|7|6|5|4|3|2|1|0|P|P|P|P|
        //  |  str[0]   |  str[1]   |  str[2]   |  str[3]   |  str[4]   |  str[6]   |
        //
        //  </para>
        //  </remarks>

        public LSL_Integer llBase64ToInteger (string str)
        {
            int number = 0;
            int digit;

            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;


            //    Require a well-fromed base64 string

            if (str.Length > 8)
                return 0;

            //    The loop is unrolled in the interests
            //    of performance and simple necessity.
            //
            //    MUST find 6 digits to be well formed
            //      -1 == invalid
            //       0 == padding

            if ((digit = c2itable [str [0]]) <= 0) {
                return digit < 0 ? 0 : number;
            }
            number += --digit << 26;

            if ((digit = c2itable [str [1]]) <= 0) {
                return digit < 0 ? 0 : number;
            }
            number += --digit << 20;

            if ((digit = c2itable [str [2]]) <= 0) {
                return digit < 0 ? 0 : number;
            }
            number += --digit << 14;

            if ((digit = c2itable [str [3]]) <= 0) {
                return digit < 0 ? 0 : number;
            }
            number += --digit << 8;

            if ((digit = c2itable [str [4]]) <= 0) {
                return digit < 0 ? 0 : number;
            }
            number += --digit << 2;

            if ((digit = c2itable [str [5]]) <= 0) {
                return digit < 0 ? 0 : number;
            }
            number += --digit >> 4;

            // ignore trailing padding

            return number;
        }

        public LSL_Integer llModPow (int a, int b, int c)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            long tmp = 0;
            Math.DivRem (Convert.ToInt64 (Math.Pow (a, b)), c, out tmp);
            PScriptSleep (m_sleepMsOnModPow);
            return Convert.ToInt32 (tmp);
        }


    }
}
