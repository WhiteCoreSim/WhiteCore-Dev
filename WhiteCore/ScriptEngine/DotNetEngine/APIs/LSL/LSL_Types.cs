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
using System.Globalization;
using System.Text.RegularExpressions;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.ScriptEngine.DotNetEngine
{
    [Serializable]
	public class LSL_Types
    {
        // Types are kept is separate .dll to avoid having to add whatever .dll it is in it to script AppDomain
        // Define the tolerance for variation in their values 
        const double DoubleDifference = .0000005;
        static bool FloatAlmostEqual (LSLFloat valA, LSLFloat valB)
        {
            return Math.Abs (valA - valB) <= DoubleDifference;
        }

        [Serializable]
        public struct Vector3
        {
            public LSLFloat x;
            public LSLFloat y;
            public LSLFloat z;

            #region Constructors

            public Vector3(Vector3 vector)
            {
                x = (float) vector.x;
                y = (float) vector.y;
                z = (float) vector.z;
            }

            public Vector3(OpenMetaverse.Vector3 vector)
            {
                x = vector.X;
                y = vector.Y;
                z = vector.Z;
            }

            public Vector3(double X, double Y, double Z)
            {
                x = X;
                y = Y;
                z = Z;
            }

            public Vector3(string str)
            {
                str = str.Replace('<', ' ');
                str = str.Replace('>', ' ');
                string[] tmps = str.Split(new char [] {',', '<', '>'});
                if (tmps.Length < 3)
                {
                    x = y = z = 0;
                    return;
                }
                //bool res;
                double xx, yy, zz;
                double.TryParse(tmps[0], NumberStyles.Float, Culture.NumberFormatInfo, out xx);
                double.TryParse(tmps[1], NumberStyles.Float, Culture.NumberFormatInfo, out yy);
                double.TryParse(tmps[2], NumberStyles.Float, Culture.NumberFormatInfo, out zz);
                x = new LSLFloat(xx);
                y = new LSLFloat(yy);
                z = new LSLFloat(zz);
            }

            #endregion

            #region Overriders

            public override string ToString()
            {
                string s = string.Format("<{0:0.000000},{1:0.000000},{2:0.000000}>", x, y, z);
                return s;
            }

            public static explicit operator LSLString(Vector3 vec)
            {
                string s = string.Format("<{0:0.000000},{1:0.000000},{2:0.000000}>", vec.x, vec.y, vec.z);
                return new LSLString(s);
            }

            public static explicit operator string(Vector3 vec)
            {
                string s = string.Format("<{0:0.000000},{1:0.000000},{2:0.000000}>", vec.x, vec.y, vec.z);
                return s;
            }

            public static explicit operator Vector3(string s)
            {
                return new Vector3(s);
            }

            public static implicit operator bool(Vector3 s)
            {
                return s.x != 0 ||
                       s.y != 0 ||
                       s.z != 0;
            }

            public static implicit operator list(Vector3 vec)
            {
                return new list(new object[] {vec});
            }

            public static bool operator ==(Vector3 lhs, Vector3 rhs)
            {
                // compare like values for equality withing the difference range
                bool xEq = FloatAlmostEqual (lhs.x, rhs.x);
                bool yEq = FloatAlmostEqual (lhs.y, rhs.y);
                bool zEq = FloatAlmostEqual (lhs.z, rhs.z);
                return (xEq && yEq && zEq);
            }

            public static bool operator !=(Vector3 lhs, Vector3 rhs)
            {
                return !(lhs == rhs);
            }

            public override int GetHashCode()
            {
                return (x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode());
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Vector3)) return false;

                var vector = (Vector3) obj;

                // compare like values for equality withing the difference range
                bool xEq = FloatAlmostEqual (x, vector.x);
                bool yEq = FloatAlmostEqual (y, vector.y);
                bool zEq = FloatAlmostEqual (z, vector.z);

                return (xEq && yEq && zEq);
            }

            public static Vector3 operator -(Vector3 vector)
            {
                return new Vector3(-vector.x, -vector.y, -vector.z);
            }

            #endregion

            #region Vector & Vector Math

            // Vector-Vector Math
            public static Vector3 operator +(Vector3 lhs, Vector3 rhs)
            {
                return new Vector3(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
            }

            public static Vector3 operator -(Vector3 lhs, Vector3 rhs)
            {
                return new Vector3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
            }

            public static LSLFloat operator *(Vector3 lhs, Vector3 rhs)
            {
                return Dot(lhs, rhs);
            }

            public static Vector3 operator %(Vector3 v1, Vector3 v2)
            {
                //Cross product
                Vector3 tv;
                tv.x = (v1.y*v2.z) - (v1.z*v2.y);
                tv.y = (v1.z*v2.x) - (v1.x*v2.z);
                tv.z = (v1.x*v2.y) - (v1.y*v2.x);
                return tv;
            }

            #endregion

            #region Vector & Float Math

            // Vector-Float and Float-Vector Math
            public static Vector3 operator *(Vector3 vec, float val)
            {
                return new Vector3(vec.x*val, vec.y*val, vec.z*val);
            }

            public static Vector3 operator *(float val, Vector3 vec)
            {
                return new Vector3(vec.x*val, vec.y*val, vec.z*val);
            }

            public static Vector3 operator /(Vector3 v, float f)
            {
                v.x = v.x/f;
                v.y = v.y/f;
                v.z = v.z/f;
                return v;
            }

            #endregion

            #region Vector & Double Math

            public static Vector3 operator *(Vector3 vec, double val)
            {
                return new Vector3(vec.x*val, vec.y*val, vec.z*val);
            }

            public static Vector3 operator *(double val, Vector3 vec)
            {
                return new Vector3(vec.x*val, vec.y*val, vec.z*val);
            }

            public static Vector3 operator /(Vector3 v, double f)
            {
                v.x = v.x/f;
                v.y = v.y/f;
                v.z = v.z/f;
                return v;
            }

            #endregion

            #region Vector & Rotation Math

            // Vector-Rotation Math
            public static Vector3 operator *(Vector3 v, Quaternion r)
            {
                Quaternion vq = new Quaternion(v.x, v.y, v.z, 0);
                Quaternion nq = new Quaternion(-r.x, -r.y, -r.z, r.s);

                // adapted for operator * computing "b * a"
                Quaternion result = nq*(vq*r);

                return new Vector3(result.x, result.y, result.z);
            }

            public static Vector3 operator /(Vector3 v, Quaternion r)
            {
                r.s = -r.s;
                return v*r;
            }

            #endregion

            #region Static Helper Functions

            public static double Dot(Vector3 v1, Vector3 v2)
            {
                return (v1.x*v2.x) + (v1.y*v2.y) + (v1.z*v2.z);
            }

            public static Vector3 Cross(Vector3 v1, Vector3 v2)
            {
                return new Vector3
                    (
                    v1.y*v2.z - v1.z*v2.y,
                    v1.z*v2.x - v1.x*v2.z,
                    v1.x*v2.y - v1.y*v2.x
                    );
            }

            public static double Mag(Vector3 v)
            {
                return Math.Sqrt(v.x*v.x + v.y*v.y + v.z*v.z);
            }

            public static Vector3 Norm(Vector3 vector)
            {
                double mag = Mag(vector);
                if (mag > 0.0)
                {
                    double invMag = 1.0/mag;
                    return vector*invMag;
                }
                return new Vector3(0, 0, 0);
            }

            public OpenMetaverse.Vector3 ToVector3()
            {
                return new OpenMetaverse.Vector3((float) x.value, (float) y.value, (float) z.value);
            }

            #endregion
        }

        [Serializable]
        public struct Quaternion
        {
            public double x;
            public double y;
            public double z;
            public double s;

            #region Constructors

            public Quaternion(Quaternion Quat)
            {
                x = (float) Quat.x;
                y = (float) Quat.y;
                z = (float) Quat.z;
                s = (float) Quat.s;
                if (FloatAlmostEqual(x, 0) &&
                    FloatAlmostEqual(y, 0) &&
                    FloatAlmostEqual(z, 0) && 
                    FloatAlmostEqual(s, 0))
                    s = 1;
            }

            public Quaternion(double X, double Y, double Z, double S)
            {
                x = X;
                y = Y;
                z = Z;
                s = S;
                if (FloatAlmostEqual (x, 0) &&
                    FloatAlmostEqual (y, 0) &&
                    FloatAlmostEqual (z, 0) &&
                    FloatAlmostEqual (s, 0))
                    s = 1;
            }

            public Quaternion(string str)
            {
                str = str.Replace('<', ' ');
                str = str.Replace('>', ' ');
                string[] tmps = str.Split(new char [] {',', '<', '>'});
                if (tmps.Length < 4)
                {
                    x = y = z = s = 0;
                    return;
                }
                bool res = double.TryParse(tmps[0], NumberStyles.Float, Culture.NumberFormatInfo, out x);
                res = res & double.TryParse(tmps[1], NumberStyles.Float, Culture.NumberFormatInfo, out y);
                res = res & double.TryParse(tmps[2], NumberStyles.Float, Culture.NumberFormatInfo, out z);
                res = res & double.TryParse(tmps[3], NumberStyles.Float, Culture.NumberFormatInfo, out s);
                if (FloatAlmostEqual (x, 0) &&
                    FloatAlmostEqual (y, 0) &&
                    FloatAlmostEqual (z, 0) &&
                    FloatAlmostEqual (s, 0))
                    s = 1;
            }

            public Quaternion(OpenMetaverse.Quaternion rot)
            {
                x = rot.X;
                y = rot.Y;
                z = rot.Z;
                s = rot.W;
            }

            #endregion

            #region Overriders

            public override int GetHashCode()
            {
                return (x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ s.GetHashCode());
            }

            public override bool Equals(object o)
            {
                if (!(o is Quaternion)) return false;

                Quaternion quaternion = (Quaternion) o;

                var result = 
                    FloatAlmostEqual (x, quaternion.x) &&
                    FloatAlmostEqual (y, quaternion.y) &&
                    FloatAlmostEqual (z, quaternion.z) &&
                    FloatAlmostEqual (s, quaternion.s);
                return result;
            }

            public override string ToString()
            {
                string st = string.Format(Culture.FormatProvider,
                                          "<{0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000}>", x, y, z, s);
                return st;
            }

            public static explicit operator string(Quaternion r)
            {
                string s = string.Format("<{0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000}>", r.x, r.y, r.z, r.s);
                return s;
            }

            public static explicit operator LSLString(Quaternion r)
            {
                string s = string.Format("<{0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000}>", r.x, r.y, r.z, r.s);
                return new LSLString(s);
            }

            public static explicit operator Quaternion(string s)
            {
                return new Quaternion(s);
            }

            public static implicit operator bool(Quaternion s)
            {
                var result = 
                    !FloatAlmostEqual (s.x, 0) ||
                    !FloatAlmostEqual (s.y, 0) ||
                    !FloatAlmostEqual (s.z, 0) ||
                    !FloatAlmostEqual (s.s, 1);
                return result;
            }

            public static implicit operator list(Quaternion r)
            {
                return new list(new object[] {r});
            }

            public static bool operator ==(Quaternion lhs, Quaternion rhs)
            {
                // Return true if the fields match:
                var result =
                    FloatAlmostEqual (lhs.x, rhs.x) &&
                    FloatAlmostEqual (lhs.y, rhs.y) &&
                    FloatAlmostEqual (lhs.z, rhs.z) &&
                    FloatAlmostEqual (lhs.s, rhs.s);
                return result;
            }

            public static bool operator !=(Quaternion lhs, Quaternion rhs)
            {
                return !(lhs == rhs);
            }

            public static double Mag(Quaternion q)
            {
                return Math.Sqrt(q.x*q.x + q.y*q.y + q.z*q.z + q.s*q.s);
            }

            #endregion

            public static Quaternion operator +(Quaternion a, Quaternion b)
            {
                return new Quaternion(a.x + b.x, a.y + b.y, a.z + b.z, a.s + b.s);
            }

            public static Quaternion operator /(Quaternion a, Quaternion b)
            {
                b.s = -b.s;
                return a*b;
            }

            public static Quaternion operator -(Quaternion a, Quaternion b)
            {
                return new Quaternion(a.x - b.x, a.y - b.y, a.z - b.z, a.s - b.s);
            }

            // using the equations below, we need to do "b * a" to be compatible with LSL
            public static Quaternion operator *(Quaternion b, Quaternion a)
            {
                Quaternion c;
                c.x = a.s*b.x + a.x*b.s + a.y*b.z - a.z*b.y;
                c.y = a.s*b.y + a.y*b.s + a.z*b.x - a.x*b.z;
                c.z = a.s*b.z + a.z*b.s + a.x*b.y - a.y*b.x;
                c.s = a.s*b.s - a.x*b.x - a.y*b.y - a.z*b.z;
                return c;
            }

            public OpenMetaverse.Quaternion ToQuaternion()
            {
                return new OpenMetaverse.Quaternion((float) x, (float) y, (float) z, (float) s);
            }

            public OpenMetaverse.Vector4 ToVector4()
            {
                return new OpenMetaverse.Vector4((float) x, (float) y, (float) z, (float) s);
            }
        }

        [Serializable]
        public class list : IEnumerator
        {
            object[] m_data;

            public list(params object[] args)
            {
                m_data = args;
            }

            public int Length
            {
                get
                {
                    if (m_data == null)
                        m_data = new object[0];
                    return m_data.Length;
                }
            }

            public int Size
            {
                get
                {
                    if (m_data == null)
                        m_data = new object[0];

                    int size = 0;

                    foreach (object o in m_data)
                    {
                        if (o is LSLInteger)
                            size += 4;
                        else if (o is LSLFloat)
                            size += 8;
                        else if (o is LSLString)
                            size += ((LSLString) o).m_string.Length;
                        else if (o is Vector3)
                            size += 32;
                        else if (o is Quaternion)
                            size += 64;
                        else if (o is int)
                            size += 4;
                        else if (o is string)
                            size += ((string) o).Length;
                        else if (o is float)
                            size += 8;
                        else if (o is double)
                            size += 16;
                        else
                            throw new Exception("Unknown type in List.Size: " + o.GetType());
                    }
                    return size;
                }
            }

            public object[] Data
            {
                get
                {
                    if (m_data == null)
                        m_data = new object[0];
                    return m_data;
                }

                set { m_data = value; }
            }

            // Function to obtain LSL type from an index. This is needed
            // because LSL lists allow for multiple types, and safely
            // iterating in them requires a type check.
            public Type GetLSLListItemType(int itemIndex)
            {
                return m_data[itemIndex].GetType();
            }

            // Member functions to obtain item as specific types.
            // For cases where implicit conversions would apply if items
            // were not in a list (e.g. integer to float, but not float
            // to integer) functions check for alternate types so as to
            // down-cast from Object to the correct type.
            // Note: no checks for item index being valid are performed

            public LSLFloat GetLSLFloatItem (int itemIndex)
            {
                if (m_data[itemIndex] is LSLInteger)
                {
                    return (LSLInteger) m_data[itemIndex];
                }
                else if (m_data [itemIndex] is int)
                {
                    return new LSLFloat ((int) m_data[itemIndex]);
                }
                else if (m_data[itemIndex] is float)
                {
                    return new LSLFloat((float) m_data[itemIndex]);
                }
                else if (m_data[itemIndex] is double)
                {
                    return new LSLFloat((double) m_data[itemIndex]);
                }
                else if (m_data[itemIndex] is LSLString)
                {
                    return new LSLFloat(m_data[itemIndex].ToString());
                }
                else
                {
                    return (LSLFloat) m_data[itemIndex];
                }
            }

            public LSLString GetLSLStringItem(int itemIndex)
            {
                if (m_data[itemIndex] is string)
                {
                    return new LSLString((string) m_data[itemIndex]);
                }
                if (m_data[itemIndex] is LSLFloat)
                {
                    return new LSLString((LSLFloat) m_data[itemIndex]);
                }
                if (m_data[itemIndex] is LSLInteger)
                {
                    return new LSLString((LSLInteger) m_data[itemIndex]);
                }

                return (LSLString) m_data[itemIndex];

            }

            public LSLInteger GetLSLIntegerItem(int itemIndex)
            {
                if (m_data[itemIndex] is LSLInteger)
                    return (LSLInteger) m_data[itemIndex];
                if (m_data[itemIndex] is LSLFloat)
                    return new LSLInteger((int) m_data[itemIndex]);
                if (m_data [itemIndex] is int)
                    return new LSLInteger((int) m_data[itemIndex]);
                if (m_data[itemIndex] is LSLString)
                    return new LSLInteger(((LSLString) m_data[itemIndex]).m_string);
                
                throw new InvalidCastException(string.Format(
                        "{0} expected but {1} given",
                        typeof (LSLInteger).Name,
                        m_data[itemIndex] != null
                            ? m_data[itemIndex].GetType().Name
                            : "null"));
            }

            public Vector3 GetVector3Item(int itemIndex)
            {
                if (m_data[itemIndex] is Vector3)
                {
                    return (Vector3) m_data[itemIndex];
                }
                if (m_data [itemIndex] is OpenMetaverse.Vector3) {
                    return new Vector3 (
                        (OpenMetaverse.Vector3)m_data [itemIndex]);
                }

                throw new InvalidCastException (string.Format (
                    "{0} expected but {1} given",
                    typeof (Vector3).Name,
                    m_data [itemIndex] != null
                        ? m_data [itemIndex].GetType ().Name
                        : "null"));
            }

            public Quaternion GetQuaternionItem(int itemIndex)
            {
                if (m_data[itemIndex] is Quaternion)
                {
                    return (Quaternion) m_data[itemIndex];
                }
                if (m_data [itemIndex] is OpenMetaverse.Quaternion) {
                    return new Quaternion (
                        (OpenMetaverse.Quaternion)m_data [itemIndex]);
                }

                throw new InvalidCastException (string.Format (
                    "{0} expected but {1} given",
                    typeof (Quaternion).Name,
                    m_data [itemIndex] != null
                        ? m_data [itemIndex].GetType ().Name
                        : "null"));
            }

            public static list operator +(list a, list b)
            {
                object[] tmp;
                tmp = new object[a.Length + b.Length];
                a.Data.CopyTo(tmp, 0);
                b.Data.CopyTo(tmp, a.Length);
                return new list(tmp);
            }

            void ExtendAndAdd(object o)
            {
                Array.Resize(ref m_data, Length + 1);
                m_data.SetValue(o, Length - 1);
            }

            public static implicit operator bool(list s)
            {
                return s.Length != 0;
            }

            public static list operator +(list a, LSLString s)
            {
                a.ExtendAndAdd(s);
                return a;
            }

            public static list operator +(list a, LSLInteger i)
            {
                a.ExtendAndAdd(i);
                return a;
            }

            public static list operator +(list a, LSLFloat d)
            {
                a.ExtendAndAdd(d);
                return a;
            }

            public static bool operator ==(list a, list b)
            {
                int la = -1;
                int lb = -1;
                try
                {
                    la = a.Length;
                }
                catch (NullReferenceException)
                {
                }
                try
                {
                    lb = b.Length;
                }
                catch (NullReferenceException)
                {
                }

                return la == lb;
            }

            public static bool operator !=(list a, list b)
            {
                int la = -1;
                int lb = -1;
                try
                {
                    la = a.Length;
                }
                catch (NullReferenceException)
                {
                }
                try
                {
                    lb = b.Length;
                }
                catch (NullReferenceException)
                {
                }

                return la != lb;
            }

            public void Add(object o)
            {
                object[] tmp;
                tmp = new object[m_data.Length + 1];
                m_data.CopyTo(tmp, 0);
                tmp[m_data.Length] = o;
                m_data = tmp;
            }

            public bool Contains(object o)
            {
                bool ret = false;
                foreach (object dobj in Data)
                {
                    if (dobj == o)
                    {
                        ret = true;
                        break;
                    }
                }
                return ret;
            }

            public list DeleteSublist(int start, int end)
            {
                // Not an easy one
                // If start <= end, remove that part
                // if either is negative, count from the end of the array
                // if the resulting start > end, remove all BUT that part

                object [] ret;

                if (start < 0)
                    start = m_data.Length + start;

                if (start < 0)
                    start = 0;

                if (end < 0)
                    end = m_data.Length + end;
                if (end < 0)
                    end = 0;

                if (start > end)
                {
                    if (end >= m_data.Length)
                        return new list(new object [0]);

                    if (start >= m_data.Length)
                        start = m_data.Length - 1;

                    return GetSublist(end, start);
                }

                // start >= 0 && end >= 0 here
                if (start >= m_data.Length)
                {
                    ret = new object[m_data.Length];
                    Array.Copy(m_data, 0, ret, 0, m_data.Length);

                    return new list(ret);
                }

                if (end >= m_data.Length)
                    end = m_data.Length - 1;

                // now, this makes the math easier
                int remove = end + 1 - start;

                ret = new object [m_data.Length - remove];
                if (ret.Length == 0)
                    return new list(ret);

                int src;
                int dest = 0;

                for (src = 0; src < m_data.Length; src++)
                {
                    if (src < start || src > end)
                        ret[dest++] = m_data[src];
                }

                return new list(ret);
            }

            public list GetSublist(int start, int end)
            {
                object[] ret;

                // Take care of neg start or end's
                // NOTE that either index may still be negative after
                // adding the length, so we must take additional
                // measures to protect against this. Note also that
                // after normalisation the negative indices are no
                // longer relative to the end of the list.

                if (start < 0)
                {
                    start = m_data.Length + start;
                }

                if (end < 0)
                {
                    end = m_data.Length + end;
                }

                // The conventional case is start <= end
                // NOTE that the case of an empty list is
                // dealt with by the initial test. Start
                // less than end is taken to be the most
                // common case.

                if (start <= end) {
                    // Start sublist beyond length
                    // Also deals with start AND end still negative
                    if (start >= m_data.Length || end < 0) {
                        return new list ();
                    }

                    // Sublist extends beyond the end of the supplied list
                    if (end >= m_data.Length) {
                        end = m_data.Length - 1;
                    }

                    // Sublist still starts before the beginning of the list
                    if (start < 0) {
                        start = 0;
                    }

                    ret = new object [end - start + 1];

                    Array.Copy (m_data, start, ret, 0, end - start + 1);

                    return new list (ret);
                }
                list result = null;

                // If end is negative, then prefix list is empty
                if (end < 0) {
                    result = new list ();
                    // If start is still negative, then the whole of
                    // the existing list is returned. This case is
                    // only admitted if end is also still negative.
                    if (start < 0) {
                        return this;
                    }
                } else {
                    result = GetSublist (0, end);
                }

                // If start is outside of list, then just return
                // the prefix, whatever it is.
                if (start >= m_data.Length) {
                    return result;
                }

                return result + GetSublist (start, Data.Length);
            }

            static int compare(object left, object right, int ascending)
            {
                if (!left.GetType().Equals(right.GetType()))
                {
                    // unequal types are always "equal" for comparison purposes.
                    // this way, the bubble sort will never swap them, and we'll
                    // get that feathered effect we're looking for
                    return 0;
                }

                int ret = 0;

                if (left is LSLString)
                {
                    LSLString l = (LSLString) left;
                    LSLString r = (LSLString) right;
                    ret = string.CompareOrdinal(l.m_string, r.m_string);
                }
                else if (left is LSLInteger)
                {
                    LSLInteger l = (LSLInteger) left;
                    LSLInteger r = (LSLInteger) right;
                    ret = Math.Sign(l.value - r.value);
                }
                else if (left is LSLFloat)
                {
                    LSLFloat l = (LSLFloat) left;
                    LSLFloat r = (LSLFloat) right;
                    ret = Math.Sign(l.value - r.value);
                }
                else if (left is Vector3)
                {
                    Vector3 l = (Vector3) left;
                    Vector3 r = (Vector3) right;
                    ret = Math.Sign(Vector3.Mag(l) - Vector3.Mag(r));
                }
                else if (left is Quaternion)
                {
                    Quaternion l = (Quaternion) left;
                    Quaternion r = (Quaternion) right;
                    ret = Math.Sign(Quaternion.Mag(l) - Quaternion.Mag(r));
                }

                if (ascending == 0)
                {
                    ret = 0 - ret;
                }

                return ret;
            }

            class HomogeneousComparer : IComparer
            {
                public HomogeneousComparer()
                {
                }

                public int Compare(object lhs, object rhs)
                {
                    return compare(lhs, rhs, 1);
                }
            }

            public list Sort(int stride, int ascending)
            {
                if (Data.Length == 0)
                    return new list(); // Don't even bother

                object[] ret = new object[Data.Length];
                Array.Copy(Data, 0, ret, 0, Data.Length);

                if (stride <= 0)
                {
                    stride = 1;
                }

                // we can optimize here in the case where stride == 1 and the list
                // consists of homogeneous types

                if (stride == 1)
                {
                    bool homogeneous = true;
                    int index;
                    for (index = 1; index < Data.Length; index++)
                    {
                        if (!Data[0].GetType().Equals(Data[index].GetType()))
                        {
                            homogeneous = false;
                            break;
                        }
                    }

                    if (homogeneous)
                    {
                        Array.Sort(ret, new HomogeneousComparer());
                        if (ascending == 0)
                        {
                            Array.Reverse(ret);
                        }
                        return new list(ret);
                    }
                }

                // Because of the desired type specific feathered sorting behavior
                // requried by the spec, we MUST use a non-optimized bubble sort here.
                // Anything else will give you the incorrect behavior.

                // begin bubble sort...
                int ix;
                int jx;
                int kx;
                int n = Data.Length;

                for (ix = 0; ix < (n - stride); ix += stride)
                {
                    for (jx = ix + stride; jx < n; jx += stride)
                    {
                        if (compare(ret[ix], ret[jx], ascending) > 0)
                        {
                            for (kx = 0; kx < stride; kx++)
                            {
                                object tmp = ret[ix + kx];
                                ret[ix + kx] = ret[jx + kx];
                                ret[jx + kx] = tmp;
                            }
                        }
                    }
                }

                // end bubble sort

                return new list(ret);
            }

            #region CSV Methods

            public static list FromCSV(string csv)
            {
                return new list(csv.Split(','));
            }

            public string ToCSV()
            {
                string ret = "";
                foreach (object o in Data)
                {
                    if (ret == "")
                    {
                        ret = o.ToString();
                    }
                    else
                    {
                        ret = ret + ", " + o;
                    }
                }
                return ret;
            }

            string ToSoup()
            {
                string output;
                output = string.Empty;
                if (m_data.Length == 0)
                {
                    return string.Empty; 
                }
                foreach (object o in m_data)
                {
                    output = output + o;
                }
                return output;
            }

            public static explicit operator string(list l)
            {
                return l.ToSoup();
            }

            public static explicit operator LSLString(list l)
            {
                return new LSLString(l.ToSoup());
            }

            public override string ToString()
            {
                return ToSoup();
            }

            #endregion

            #region Statistic Methods

            public double Min()
            {
                double minimum = double.PositiveInfinity;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        if (entry < minimum) minimum = entry;
                    }
                }
                return minimum;
            }

            public double Max()
            {
                double maximum = double.NegativeInfinity;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        if (entry > maximum) maximum = entry;
                    }
                }
                return maximum;
            }

            public double Range()
            {
                return (Max()/Min());
            }

            public int NumericLength()
            {
                int count = 0;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        count++;
                    }
                }
                return count;
            }

            public static list ToDoubleList(list src)
            {
                list ret = new list();
                double entry;
                for (int i = 0; i < src.Data.Length - 1; i++)
                {
                    if (double.TryParse(src.Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        ret.Add(entry);
                    }
                }
                return ret;
            }

            public double Sum()
            {
                double sum = 0;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        sum = sum + entry;
                    }
                }
                return sum;
            }

            public double SumSqrs()
            {
                double sum = 0;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        sum = sum + Math.Pow(entry, 2);
                    }
                }
                return sum;
            }

            public double Mean()
            {
                var numLen = NumericLength ();
                if (numLen != 0)
                    return (Sum()/numLen);

                return 0f;
            }

            public void NumericSort()
            {
                IComparer Numeric = new NumericComparer();
                Array.Sort(Data, Numeric);
            }

            public void AlphaSort()
            {
                IComparer Alpha = new AlphaCompare();
                Array.Sort(Data, Alpha);
            }

            public double Median()
            {
                return Qi(0.5);
            }

            public double GeometricMean()
            {
                double ret = 1.0;
                list nums = ToDoubleList(this);
                for (int i = 0; i < nums.Data.Length; i++)
                {
                    ret *= (double) nums.Data[i];
                }
                return Math.Exp(Math.Log(ret) / nums.Data.Length);
            }

            public double HarmonicMean()
            {
                double ret = 0.0;
                list nums = ToDoubleList(this);
                for (int i = 0; i < nums.Data.Length; i++)
                {
                    ret += 1.0/(double) nums.Data[i];
                }
                return (nums.Data.Length/ret);
            }

            public double Variance()
            {
                double s = 0;
                list num = ToDoubleList(this);
                for (int i = 0; i < num.Data.Length; i++)
                {
                    s += Math.Pow((double) num.Data[i], 2);
                }
                return (s - num.Data.Length*Math.Pow(num.Mean(), 2))/(num.Data.Length - 1);
            }

            public double StdDev()
            {
                return Math.Sqrt(Variance());
            }

            public double Qi(double i)
            {
                list j = this;
                j.NumericSort();

                if (FloatAlmostEqual (Math.Ceiling (Length * i), Length * i)) {
                    return ((double)j.Data [(int)(Length * i - 1)] + (double)j.Data [(int)(Length * i)]) / 2;
                }
                return (double)j.Data [((int)(Math.Ceiling (Length * i))) - 1];
            }

            #endregion

            public string ToPrettyString()
            {
                string output;
                if (m_data.Length == 0)
                {
                    return "[]";
                }
                output = "[";
                foreach (object o in m_data)
                {
                    if (o is string)
                    {
                        output = output + "\"" + o + "\", ";
                    }
                    else
                    {
                        output = output + o + ", ";
                    }
                }
                output = output.Substring(0, output.Length - 2);
                output = output + "]";
                return output;
            }

            public class AlphaCompare : IComparer
            {
                int IComparer.Compare(object x, object y)
                {
                    return string.Compare(x.ToString(), y.ToString());
                }
            }

            public class NumericComparer : IComparer
            {
                int IComparer.Compare(object x, object y)
                {
                    double a;
                    double b;
                    if (!double.TryParse(x.ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out a))
                    {
                        a = 0.0;
                    }
                    if (!double.TryParse(y.ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out b))
                    {
                        b = 0.0;
                    }
                    if (a < b) {
                        return -1;
                    }
                    if (FloatAlmostEqual (a, b)) {
                        return 0;
                    }
                    return 1;
                }
            }

            public override bool Equals(object obj)
            {
                if (!(obj is list))
                    return false;

                return Data.Length == ((list) obj).Data.Length;
            }

            public override int GetHashCode()
            {
                return Data.GetHashCode();
            }

            #region IEnumerator Members

            int enumIx = 0;

            public object Current
            {
                get { return m_data[enumIx]; }
            }

            public bool MoveNext()
            {
                enumIx++;
                if (m_data.Length == enumIx)
                    return false;
                return true;
            }

            public void Reset()
            {
                enumIx = 0;
            }

            #endregion
        }

        [Serializable]
        public struct LSLString
        {
            public string m_string;

            #region Constructors

            public LSLString(string s)
            {
                m_string = s;
            }

            public LSLString(OpenMetaverse.UUID s)
            {
                m_string = s.ToString();
            }

            public LSLString(double d)
            {
                string s = string.Format(Culture.FormatProvider, "{0:0.000000}", d);
                m_string = s;
            }

            public LSLString(LSLFloat f)
            {
                string s = string.Format(Culture.FormatProvider, "{0:0.000000}", f.value);
                m_string = s;
            }

            public LSLString(LSLInteger i)
            {
                string s = string.Format("{0}", i);
                m_string = s;
            }

            #endregion

            #region Operators

            #region Implicit

            public static implicit operator bool (LSLString s)
            {
                if (s.m_string.Length == 0) {
                    return false;
                }
                if (s.m_string == OpenMetaverse.UUID.Zero.ToString ()) {
                    return false;
                }
                return true;
            }

            public static implicit operator OpenMetaverse.UUID(LSLString s)
            {
                return OpenMetaverse.UUID.Parse(s.m_string);
            }

            public static implicit operator string(LSLString s)
            {
                return s.m_string;
            }

            public static implicit operator LSLString(string s)
            {
                return new LSLString(s);
            }

            public static implicit operator Vector3(LSLString s)
            {
                return new Vector3(s.m_string);
            }

            public static implicit operator Quaternion(LSLString s)
            {
                return new Quaternion(s.m_string);
            }

            public static implicit operator LSLFloat(LSLString s)
            {
                return new LSLFloat(s);
            }

            public static implicit operator list(LSLString s)
            {
                return new list(new object[] {s});
            }

            #endregion

            #region Explicit

            public static explicit operator double(LSLString s)
            {
                return new LSLFloat(s).value;
            }

            public static explicit operator LSLInteger(LSLString s)
            {
                return new LSLInteger(s.m_string);
            }

            public static explicit operator LSLString(LSLFloat f)
            {
                return new LSLString(f);
            }

            public static explicit operator LSLString(double d)
            {
                return new LSLString(d);
            }

            public static explicit operator LSLString(int i)
            {
                return new LSLString(i);
            }

            public static explicit operator LSLString(bool b)
            {
                return new LSLString(b ? "1" : "0");
            }

            #endregion

            public static string ToString(LSLString s)
            {
                return s.m_string;
            }

            public override string ToString()
            {
                return m_string;
            }

            public static bool operator ==(LSLString s1, string s2)
            {
                return s1.m_string == s2;
            }

            public static bool operator !=(LSLString s1, string s2)
            {
                return s1.m_string != s2;
            }

            public static LSLString operator +(LSLString s1, LSLString s2)
            {
                return new LSLString(s1.m_string + s2.m_string);
            }

            public static LSLString operator +(LSLString s1, LSLFloat s2)
            {
                return new LSLString(s1.m_string + s2);
            }

            public static LSLString operator +(LSLString s1, LSLInteger s2)
            {
                return new LSLString(s1.m_string + s2);
            }

            public static LSLString operator +(LSLString s1, Quaternion s2)
            {
                return new LSLString(s1.m_string + s2);
            }

            public static LSLString operator +(LSLString s1, Vector3 s2)
            {
                return new LSLString(s1.m_string + s2);
            }

            public static LSLString operator +(Vector3 s1, LSLString s2)
            {
                return new LSLString(s1 + s2.m_string);
            }

            public static LSLString operator +(Quaternion s1, LSLString s2)
            {
                return new LSLString(s1 + s2.m_string);
            }

            public static LSLString operator +(LSLInteger s1, LSLString s2)
            {
                return new LSLString(s1 + s2.m_string);
            }

            public static LSLString operator +(LSLFloat s1, LSLString s2)
            {
                return new LSLString(s1 + s2.m_string);
            }

            #endregion

            #region Overriders

            public override bool Equals(object obj)
            {
                return m_string == obj.ToString();
            }

            public override int GetHashCode()
            {
                return m_string.GetHashCode();
            }

            #endregion

            #region " Standard string functions "

            //Clone,CompareTo,Contains
            //CopyTo,EndsWith,Equals,GetEnumerator,GetHashCode,GetType,GetTypeCode
            //IndexOf,IndexOfAny,Insert,IsNormalized,LastIndexOf,LastIndexOfAny
            //Length,Normalize,PadLeft,PadRight,Remove,Replace,Split,StartsWith,Substring,ToCharArray,ToLowerInvariant
            //ToString,ToUpper,ToUpperInvariant,Trim,TrimEnd,TrimStart
            public bool Contains(string value)
            {
                return m_string.Contains(value);
            }

            public int IndexOf(string value)
            {
                return m_string.IndexOf (value, StringComparison.Ordinal);
            }

            public int Length
            {
                get { return m_string.Length; }
            }

            #endregion
        }

        [Serializable]
        public struct LSLInteger
        {
            public int value;
            public static LSLInteger TRUE = new LSLInteger(1);
            public static LSLInteger FALSE = new LSLInteger(0);

            static readonly Regex castRegex =
                new Regex(@"(^[ ]*0[xX][0-9A-Fa-f][0-9A-Fa-f]*)|(^[ ]*(-?|\+?)[0-9][0-9]*)");

            #region Constructors

            public LSLInteger(int i)
            {
                value = i;
            }

            public LSLInteger(uint i)
            {
                value = (int) i;
            }

            public LSLInteger(double d)
            {
                value = (int) d;
            }

            public LSLInteger(string s)
            {
                if (int.TryParse(s, out value))
                    return;
                Match m = castRegex.Match(s);
                string v = m.Groups[0].Value;
                // Leading plus sign is allowed, but ignored
                v = v.Replace("+", "");
                if (s == "TRUE")
                    value = 1;
                else if (s == "FALSE")
                    value = 0;
                else if (v == string.Empty)
                    value = 0;
                else
                {
                    try
                    {
                        if (v.Contains("x") || v.Contains("X"))
                        {
                            value = int.Parse(v.Substring(2), NumberStyles.HexNumber);
                        }
                        else
                        {
                            value = int.Parse(v, NumberStyles.Integer);
                        }
                    }
                    catch (OverflowException)
                    {
                        value = -1;
                    }
                }
            }

            #endregion

            #region Operators

            public static implicit operator int(LSLInteger i)
            {
                return i.value;
            }

            public static explicit operator uint(LSLInteger i)
            {
                return (uint) i.value;
            }

            public static explicit operator LSLString(LSLInteger i)
            {
                return new LSLString(i.ToString());
            }

            public static implicit operator list(LSLInteger i)
            {
                return new list(new object[] {i});
            }

            public static implicit operator bool (LSLInteger i)
            {
                if (i.value == 0) {
                    return false;
                }
                return true;
            }

            public static implicit operator LSLInteger(int i)
            {
                return new LSLInteger(i);
            }

            public static explicit operator LSLInteger(string s)
            {
                return new LSLInteger(s);
            }

            public static implicit operator LSLInteger(uint u)
            {
                return new LSLInteger(u);
            }

            public static explicit operator LSLInteger(double d)
            {
                return new LSLInteger(d);
            }

            public static explicit operator LSLInteger(LSLFloat f)
            {
                return new LSLInteger(f.value);
            }

            public static implicit operator LSLInteger(bool b)
            {
                if (b)
                    return new LSLInteger (1);
                return new LSLInteger (0);
            }

            public static LSLInteger operator ==(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value == i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            public static LSLInteger operator !=(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value != i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            public static LSLInteger operator <(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value < i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            public static LSLInteger operator <=(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value <= i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            public static LSLInteger operator >(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value > i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            public static LSLInteger operator >=(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value >= i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            public static LSLInteger operator +(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value + i2);
            }

            public static LSLInteger operator -(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value - i2);
            }

            public static LSLInteger operator *(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value*i2);
            }

            public static LSLInteger operator /(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value/i2);
            }

            //            static public LSLFloat operator +(LSLInteger i1, double f)
            //            {
            //                return new LSLFloat((double)i1.value + f);
            //            }
            //
            //            static public LSLFloat operator -(LSLInteger i1, double f)
            //            {
            //                return new LSLFloat((double)i1.value - f);
            //            }
            //
            //            static public LSLFloat operator *(LSLInteger i1, double f)
            //            {
            //                return new LSLFloat((double)i1.value * f);
            //            }
            //
            //            static public LSLFloat operator /(LSLInteger i1, double f)
            //            {
            //                return new LSLFloat((double)i1.value / f);
            //            }

            public static LSLInteger operator -(LSLInteger i)
            {
                return new LSLInteger(-i.value);
            }

            public static LSLInteger operator ~(LSLInteger i)
            {
                return new LSLInteger(~i.value);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is LSLInteger))
                    return false;
                return value == ((LSLInteger) obj).value;
            }

            public override int GetHashCode()
            {
                return value;
            }

            public static LSLInteger operator &(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value & i2.value;
                return ret;
            }

            public static LSLInteger operator %(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value%i2.value;
                return ret;
            }

            public static LSLInteger operator |(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value | i2.value;
                return ret;
            }

            public static LSLInteger operator ^(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value ^ i2.value;
                return ret;
            }

            public static LSLInteger operator !(LSLInteger i1)
            {
                return i1.value == 0 ? 1 : 0;
            }

            public static LSLInteger operator ++(LSLInteger i)
            {
                i.value++;
                return i;
            }


            public static LSLInteger operator --(LSLInteger i)
            {
                i.value--;
                return i;
            }

            public static LSLInteger operator <<(LSLInteger i, int s)
            {
                return i.value << s;
            }

            public static LSLInteger operator >>(LSLInteger i, int s)
            {
                return i.value >> s;
            }

            public static implicit operator double(LSLInteger i)
            {
                return i.value;
            }

            public static bool operator true(LSLInteger i)
            {
                return i.value != 0;
            }

            public static bool operator false(LSLInteger i)
            {
                return i.value == 0;
            }

            #endregion

            #region Overriders

            public override string ToString()
            {
                return value.ToString();
            }

            #endregion
        }

        [Serializable]
        public struct LSLFloat
        {
            public double value;

            #region Constructors

            public LSLFloat(int i)
            {
                value = i;
            }

            public LSLFloat(double d)
            {
                value = d;
            }

            public LSLFloat(string s)
            {
                if (double.TryParse(s, out value))
                    return;
                Regex r = new Regex("^ *(\\+|-)?([0-9]+\\.?[0-9]*|\\.[0-9]+)([eE](\\+|-)?[0-9]+)?");
                Match m = r.Match(s);
                string v = m.Groups[0].Value;

                v = v.Trim();

                if (s == "TRUE")
                    value = 1.0;
                else if (s == "FALSE")
                    value = 0.0;
                else if (string.IsNullOrEmpty (v))
                    v = "0.0";
                else if (!v.Contains(".") && !v.ToLower().Contains("e"))
                    v = v + ".0";
                else if (v.EndsWith (".", StringComparison.Ordinal))
                    v = v + "0";
                value = double.Parse(v, NumberStyles.Float, Culture.NumberFormatInfo);
            }

            #endregion

            #region Operators

            public static explicit operator float(LSLFloat f)
            {
                return (float) f.value;
            }

            public static explicit operator byte(LSLFloat f)
            {
                return (byte) f.value;
            }

            public static explicit operator ushort(LSLFloat f)
            {
                return (ushort) f.value;
            }

            public static explicit operator int(LSLFloat f)
            {
                return (int) f.value;
            }

            public static explicit operator uint(LSLFloat f)
            {
                return (uint) Math.Abs(f.value);
            }

            public static explicit operator string(LSLFloat f)
            {
                return f.value.ToString();
            }

            public static implicit operator bool (LSLFloat f)
            {

                //if (f.value == 0.0)
                if (Math.Abs (f.value) <= DoubleDifference) {
                    return false;
                }
                return true;
            }

            public static implicit operator LSLFloat(int i)
            {
                return new LSLFloat(i);
            }

            public static implicit operator LSLFloat(LSLInteger i)
            {
                return new LSLFloat(i.value);
            }

            public static explicit operator LSLFloat(string s)
            {
                return new LSLFloat(s);
            }

            public static implicit operator list(LSLFloat f)
            {
                return new list(new object[] {f});
            }

            public static implicit operator LSLFloat(double d)
            {
                return new LSLFloat(d);
            }

            public static implicit operator LSLFloat(bool b)
            {
                if (b)
                    return new LSLFloat (1.0);
                return new LSLFloat (0.0);
            }
 
            public static bool operator ==(LSLFloat f1, LSLFloat f2)
            {
                // compare like values for equality withing the difference range
                return ( Math.Abs(f1.value) - Math.Abs(f2.value) ) <= DoubleDifference;
                //return f1.value == f2.value;
            }

            public static bool operator !=(LSLFloat f1, LSLFloat f2)
            {
                return !(f1 == f2);
                //return f1.value != f2.value;
            }


            public static LSLFloat operator ++(LSLFloat f)
            {
                f.value++;
                return f;
            }

            public static LSLFloat operator --(LSLFloat f)
            {
                f.value--;
                return f;
            }

            public static LSLFloat operator +(LSLFloat f, int i)
            {
                return new LSLFloat(f.value + i);
            }

            public static LSLFloat operator -(LSLFloat f, int i)
            {
                return new LSLFloat(f.value - i);
            }

            public static LSLFloat operator *(LSLFloat f, int i)
            {
                return new LSLFloat(f.value * i);
            }

            public static LSLFloat operator /(LSLFloat f, int i)
            {
                return new LSLFloat(f.value / i);
            }

            public static LSLFloat operator +(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value + rhs.value);
            }

            public static LSLFloat operator -(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value - rhs.value);
            }

            public static LSLFloat operator *(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value*rhs.value);
            }

            public static LSLFloat operator /(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value/rhs.value);
            }

            public static LSLFloat operator -(LSLFloat f)
            {
                return new LSLFloat(-f.value);
            }

            public static implicit operator double(LSLFloat f)
            {
                return f.value;
            }

            #endregion

            #region Overriders

            public override string ToString()
            {
                return string.Format(Culture.FormatProvider, "{0:0.000000}", value);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is LSLFloat))
                    return false;
                return FloatAlmostEqual (value, ((LSLFloat) obj).value);
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            #endregion
        }
    }
}
