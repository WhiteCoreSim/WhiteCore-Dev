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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Amib.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using ProtoBuf;
using ProtoBuf.Meta;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using ReaderWriterLockSlim = System.Threading.ReaderWriterLockSlim;

namespace WhiteCore.Framework.Utilities
{

    /// <summary>
    ///     The method used by Util.FireAndForget for asynchronously firing events
    /// </summary>
    public enum FireAndForgetMethod
    {
        UnsafeQueueUserWorkItem,
        QueueUserWorkItem,
        BeginInvoke,
        SmartThreadPool,
        Thread
    }

    public enum RuntimeEnvironment
    {
        NET,
        WinMono,
        Mono
    }

    /// <summary>
    ///     Miscellaneous utility functions
    /// </summary>
    public static class Util
    {
        static uint nextXferID = 5000;
        static readonly Random randomClass = new ThreadSafeRandom ();

        // Get a list of invalid file characters (OS dependent)
        static readonly string regexInvalidFileChars = "[" + new string (Path.GetInvalidFileNameChars ()) + "]";
        static readonly string regexInvalidPathChars = "[" + new string (Path.GetInvalidPathChars ()) + "]";
        static readonly object XferLock = new object ();

        /// <summary>
        ///     Thread pool used for Util.FireAndForget if
        ///     FireAndForgetMethod.SmartThreadPool is used
        /// </summary>
        static SmartThreadPool m_ThreadPool;

        static volatile bool m_threadPoolRunning;

        // Unix-epoch starts at January 1st 1970, 00:00:00 UTC. And all our times in the server are (or at least should be) in UTC.
        public static readonly DateTime UnixEpoch =
            DateTime.ParseExact ("1970-01-01 00:00:00 +0", "yyyy-MM-dd hh:mm:ss z", DateTimeFormatInfo.InvariantInfo).
                     ToUniversalTime ();

        public static readonly Regex UUIDPattern
            = new Regex ("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

        public static FireAndForgetMethod FireAndForgetMethod = FireAndForgetMethod.SmartThreadPool;

        static Util ()
        {
            RuntimeTypeModel.Default.Add (typeof (UUID), false)
                            .SetSurrogate (typeof (UUIDSurrogate));
            RuntimeTypeModel.Default.Add (typeof (IPEndPoint), false)
                            .SetSurrogate (typeof (IPEndPointSurrogate));
            RuntimeTypeModel.Default.Add (typeof (OSD), false)
                            .SetSurrogate (typeof (OSDSurrogate));
            RuntimeTypeModel.Default.Add (typeof (OSDArray), false)
                            .SetSurrogate (typeof (OSDArraySurrogate));
            RuntimeTypeModel.Default.Add (typeof (OSDMap), false)
                            .SetSurrogate (typeof (OSDMapSurrogate));
            RuntimeTypeModel.Default.Add (typeof (Vector3), false)
                            .SetSurrogate (typeof (Vector3Surrogate));
            RuntimeTypeModel.Default.Add (typeof (Quaternion), false)
                            .SetSurrogate (typeof (QuaternionSurrogate));
            RuntimeTypeModel.Default.Add (typeof (ParcelManager.ParcelAccessEntry), false)
                            .SetSurrogate (typeof (ParcelAccessEntrySurrogate));
            RuntimeTypeModel.Default.Add (typeof (MediaEntry), false)
                            .SetSurrogate (typeof (MediaEntrySurrogate));
            RuntimeTypeModel.Default.Add (typeof (System.Drawing.Color), false)
                            .SetSurrogate (typeof (ColorSurrogate));
        }

        #region Protobuf helpers

        [ProtoContract]
        class UUIDSurrogate
        {
            [ProtoMember (1)]
            public string ID;
            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator UUID (UUIDSurrogate value)
            {
                return UUID.Parse (value.ID);
            }

            public static implicit operator UUIDSurrogate (UUID value)
            {
                return new UUIDSurrogate {
                    ID = value.ToString ()
                };
            }
        }

        [ProtoContract]
        class Vector3Surrogate
        {
            [ProtoMember (1)]
            public float X;
            [ProtoMember (2)]
            public float Y;
            [ProtoMember (3)]
            public float Z;

            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator Vector3 (Vector3Surrogate value)
            {
                return new Vector3 (value.X, value.Y, value.Z);
            }

            public static implicit operator Vector3Surrogate (Vector3 value)
            {
                return new Vector3Surrogate () {
                    X = value.X,
                    Y = value.Y,
                    Z = value.Z
                };
            }
        }

        [ProtoContract]
        class QuaternionSurrogate
        {
            [ProtoMember (1)]
            public float X;
            [ProtoMember (2)]
            public float Y;
            [ProtoMember (3)]
            public float Z;
            [ProtoMember (4)]
            public float W;
            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator Quaternion (QuaternionSurrogate value)
            {
                return new Quaternion (value.X, value.Y, value.Z, value.W);
            }

            public static implicit operator QuaternionSurrogate (Quaternion value)
            {
                return new QuaternionSurrogate () {
                    X = value.X,
                    Y = value.Y,
                    Z = value.Z,
                    W = value.W
                };
            }
        }

        [ProtoContract]
        class IPEndPointSurrogate
        {
            [ProtoMember (1)]
            public string IPAddr;
            [ProtoMember (2)]
            public int Port;
            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator IPEndPoint (IPEndPointSurrogate value)
            {
                return value == null ? null : new IPEndPoint (IPAddress.Parse (value.IPAddr), value.Port);
            }

            public static implicit operator IPEndPointSurrogate (IPEndPoint value)
            {
                return value == null
                           ? null
                           : new IPEndPointSurrogate {
                               IPAddr = value.Address.ToString (),
                               Port = value.Port
                           };
            }
        }

        [ProtoContract]
        class OSDSurrogate
        {
            [ProtoMember (1)]
            public string str;
            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator OSD (OSDSurrogate value)
            {
                return value.str == "" ? null : OSDParser.DeserializeJson (value.str);
            }

            public static implicit operator OSDSurrogate (OSD value)
            {
                return new OSDSurrogate {
                    str = value == null ? "" : OSDParser.SerializeJsonString (value)
                };
            }
        }

        [ProtoContract]
        class OSDMapSurrogate
        {
            [ProtoMember (1)]
            public string str;
            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator OSDMap (OSDMapSurrogate value)
            {
                return value.str == "" ? null : (OSDMap)OSDParser.DeserializeJson (value.str);
            }

            public static implicit operator OSDMapSurrogate (OSDMap value)
            {
                return new OSDMapSurrogate {
                    str = value == null ? "" : OSDParser.SerializeJsonString (value)
                };
            }
        }

        [ProtoContract]
        class OSDArraySurrogate
        {
            [ProtoMember (1)]
            public string str;
            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator OSDArray (OSDArraySurrogate value)
            {
                return value.str == "" ? null : (OSDArray)OSDParser.DeserializeJson (value.str);
            }

            public static implicit operator OSDArraySurrogate (OSDArray value)
            {
                return new OSDArraySurrogate {
                    str = value == null ? "" : OSDParser.SerializeJsonString (value)
                };
            }
        }

        [ProtoContract]
        class ParcelAccessEntrySurrogate
        {
            [ProtoMember (1)]
            public UUID AgentID;
            [ProtoMember (2)]
            public AccessList Flags;
            [ProtoMember (3)]
            public DateTime Time;

            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator ParcelManager.ParcelAccessEntry (ParcelAccessEntrySurrogate value)
            {
                return new ParcelManager.ParcelAccessEntry () {
                    AgentID = value.AgentID,
                    Flags = value.Flags,
                    Time = value.Time
                };
            }

            public static implicit operator ParcelAccessEntrySurrogate (ParcelManager.ParcelAccessEntry value)
            {
                return new ParcelAccessEntrySurrogate {
                    AgentID = value.AgentID,
                    Flags = value.Flags,
                    Time = value.Time
                };
            }
        }

        [ProtoContract]
        class MediaEntrySurrogate
        {
            [ProtoMember (1)]
            public OSD info;

            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator MediaEntry (MediaEntrySurrogate value)
            {
                return value.info == null ? null : MediaEntry.FromOSD (value.info);
            }

            public static implicit operator MediaEntrySurrogate (MediaEntry value)
            {
                return new MediaEntrySurrogate {
                    info = value == null ? null : value.GetOSD ()
                };
            }
        }

        [ProtoContract]
        class ColorSurrogate
        {
            [ProtoMember (1)]
            public int A;
            [ProtoMember (2)]
            public int R;
            [ProtoMember (3)]
            public int G;
            [ProtoMember (4)]
            public int B;

            // protobuf-net wants an implicit or explicit operator between the types
            public static implicit operator System.Drawing.Color (ColorSurrogate value)
            {
                return System.Drawing.Color.FromArgb (value.A, value.R, value.G, value.B);
            }

            public static implicit operator ColorSurrogate (System.Drawing.Color value)
            {
                return new ColorSurrogate {
                    A = value.A,
                    R = value.R,
                    G = value.G,
                    B = value.B
                };
            }
        }

        #endregion

        public static string ConvertToString (List<string> list)
        {
            StringBuilder builder = new StringBuilder ();
            foreach (string val in list) {
                builder.Append (val + ",");
            }
            return builder.ToString ();
        }

        public static string ConvertToString (Dictionary<string, object> list)
        {
            StringBuilder builder = new StringBuilder ();
            foreach (var val in list) {
                builder.Append (val.Key + "=" + val.Value + ",");
            }
            return builder.ToString ();
        }

        //wab - Added for debugging ease
        public static string ConvertToString (OSDMap values, string lineStart)
        {
            if (string.IsNullOrEmpty (lineStart)) lineStart = "\t";
            StringBuilder builder = new StringBuilder ();
            String [] keys = new String [values.Count];
            values.Keys.CopyTo (keys, 0);
            foreach (String key in keys) {
                Object val = values [key];
                if (val == null)
                    builder.AppendFormat ("{0}{1}=null\n", lineStart, key);
                else if (val is OSDMap)
                    builder.AppendFormat ("{0}{1}=...\n{2}", lineStart, key, ConvertToString ((OSDMap)val, "\t\t"));
                else
                    builder.AppendFormat ("{0}{1}={2}\n", lineStart, key, val);
            }
            return builder.ToString ();
        }

        public static List<string> ConvertToList (string listAsString, bool splitSpaces)
        {
            if (listAsString == null) return new List<string> ();
            //Do both , and " " so that it removes any annoying spaces in the string added by users
            List<string> value =
                new List<string> (listAsString.Split ((splitSpaces ? new [] { ",", " " } : new [] { "," }), StringSplitOptions.RemoveEmptyEntries));
            return value;
        }

        public static Dictionary<string, string> ConvertToDictionary (string listAsString)
        {
            //Do both , and " " so that it removes any annoying spaces in the string added by users
            List<string> value =
                new List<string> (listAsString.Split (new [] { "," }, StringSplitOptions.RemoveEmptyEntries));
            Dictionary<string, string> dict = new Dictionary<string, string> ();
            foreach (var v in value) {
                var split = v.Split ('=');
                dict.Add (split [0], split [1]);
            }
            return dict;
        }

        public static string BuildYMDDateString (DateTime time)
        {
            return time.ToString ("yyyy-MM-dd");
        }

        /// <summary>
        ///     Gets the name of the directory where the current running executable
        ///     is located
        /// </summary>
        /// <returns>
        ///     File system path to the directory containing the current
        ///     executable
        /// </returns>
        public static string ExecutingDirectory ()
        {
            return Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
        }

        /// <summary>
        ///     Copy data from one stream to another, leaving the read position of both streams at the beginning.
        /// </summary>
        /// <param name='inputStream'>
        ///     Input stream.  Must be seek-able.
        /// </param>
        /// <exception cref='ArgumentException'>
        ///     Thrown if the input stream is not seek-able.
        /// </exception>
        public static Stream Copy (Stream inputStream)
        {
            if (!inputStream.CanSeek)
                throw new ArgumentException ("Util.Copy(Stream inputStream) must receive an inputStream that can seek");

            const int readSize = 256;
            byte [] buffer = new byte [readSize];
            MemoryStream ms = new MemoryStream ();

            int count = inputStream.Read (buffer, 0, readSize);

            while (count > 0) {
                ms.Write (buffer, 0, count);
                count = inputStream.Read (buffer, 0, readSize);
            }

            ms.Position = 0;
            inputStream.Position = 0;

            return ms;
        }

        /// <summary>
        ///     Linear interpolates B&lt;-&gt;C using percent A
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static double lerp (double a, double b, double c)
        {
            return (b * a) + (c * (1 - a));
        }

        /// <summary>
        ///     Bilinear Interpolate, see Lerp but for 2D using 'percents' X & Y.
        ///     Layout:
        ///     A B
        ///     C D
        ///     A&lt;-&gt;C = Y
        ///     C&lt;-&gt;D = X
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static double lerp2D (double x, double y, double a, double b, double c, double d)
        {
            return lerp (y, lerp (x, a, b), lerp (x, c, d));
        }


        public static Encoding UTF8 = Encoding.UTF8;

        /// <value>
        ///     Well known UUID for the blank texture used in the Linden SL viewer version 1.20 (and hopefully onwards)
        /// </value>
        public static UUID BLANK_TEXTURE_UUID = new UUID ("5748decc-f629-461c-9a36-a35a221fe21f");

        #region General maths helpers
        /// <summary>
        /// Values are equal within float variation.
        /// </summary>
        /// <returns>The equals.</returns>
        /// <param name="valA">Value a.</param>
        /// <param name="valB">Value b.</param>
        public static bool ApproxEqual (float valA, float valB)
        {
            return Math.Abs (valA - valB) <= Constants.FloatDifference;
        }

        /// <summary>
        /// Value is approximately zero (within float difference).
        /// </summary>
        /// <returns>The zero.</returns>
        /// <param name="valA">Value a.</param>
        public static bool ApproxZero (float valA)
        {
            return Math.Abs (valA) <= Constants.FloatDifference;
        }

        // double variations

        /// <summary>
        /// Values are equal within float variation.
        /// </summary>
        /// <returns>The equals.</returns>
        /// <param name="valA">Value a.</param>
        /// <param name="valB">Value b.</param>
        public static bool ApproxEqual (double valA, double valB)
        {
            return Math.Abs (valA - valB) <= Constants.FloatDifference;
        }

        /// <summary>
        /// Value is approximately zero (within float difference).
        /// </summary>
        /// <returns>The zero.</returns>
        /// <param name="valA">Value a.</param>
        public static bool ApproxZero (double valA)
        {
            return Math.Abs (valA) <= Constants.FloatDifference;
        }

        #endregion

        #region Vector Equations

        /// <summary>
        ///     Get the distance between two 3d vectors
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <param name="b">A 3d vector</param>
        /// <returns>The distance between the two vectors</returns>
        public static double GetDistanceTo (Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return Math.Sqrt (dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        ///     Get the distance between two 3d vectors (excluding Z)
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <param name="b">A 3d vector</param>
        /// <returns>The distance between the two vectors</returns>
        public static double GetFlatDistanceTo (Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return Math.Sqrt (dx * dx + dy * dy);
        }

        /// <summary>
        ///     Returns true if the distance between A and B is less than amount. Significantly faster than GetDistanceTo since it eliminates the Sqrt.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool DistanceLessThan (Vector3 a, Vector3 b, double amount)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (dx * dx + dy * dy + dz * dz) < (amount * amount);
        }

        /// <summary>
        ///     Get the magnitude of a 3d vector
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <returns>The magnitude of the vector</returns>
        public static double GetMagnitude (Vector3 a)
        {
            return Math.Sqrt ((a.X * a.X) + (a.Y * a.Y) + (a.Z * a.Z));
        }

        /// <summary>
        ///     Get a normalized form of a 3d vector
        ///     The vector parameter cannot be &lt;0,0,0&gt;
        /// </summary>
        /// <param name="a">A 3d vector</param>
        /// <returns>A new vector which is normalized form of the vector</returns>
        public static Vector3 GetNormalizedVector (Vector3 a)
        {
            if (IsZeroVector (a))
                throw new ArgumentException ("Vector parameter cannot be a zero vector.");

            float Mag = (float)GetMagnitude (a);
            return new Vector3 (a.X / Mag, a.Y / Mag, a.Z / Mag);
        }

        /// <summary>
        ///     Returns if a vector is a zero vector (has all zero components)
        /// </summary>
        /// <returns></returns>
        public static bool IsZeroVector (Vector3 v)
        {
            //if (v.X == 0 && v.Y == 0 && v.Z == 0)
            if (v.X < Constants.FloatDifference &&
                v.Y < Constants.FloatDifference &&
                v.Z < Constants.FloatDifference) {
                return true;
            }

            return false;
        }

        #endregion
       
        #region Quaternion Equations
        public static Quaternion Axes2Rot (Vector3 fwd, Vector3 left, Vector3 up)
        {
            float s;
            float tr = (float)(fwd.X + left.Y + up.Z + 1.0);

            if (tr >= 1.0) {
                s = (float)(0.5 / Math.Sqrt (tr));
                return new Quaternion (
                    (left.Z - up.Y) * s,
                    (up.X - fwd.Z) * s,
                    (fwd.Y - left.X) * s,
                    (float)0.25 / s);
            } else {
                float max = (left.Y > up.Z) ? left.Y : up.Z;

                if (max < fwd.X) {
                    s = (float)(Math.Sqrt (fwd.X - (left.Y + up.Z) + 1.0));
                    float x = (float)(s * 0.5);
                    s = (float)(0.5 / s);
                    return new Quaternion (
                        x,
                        (fwd.Y + left.X) * s,
                        (up.X + fwd.Z) * s,
                        (left.Z - up.Y) * s);
                }
                if (max == left.Y) {
                    s = (float)(Math.Sqrt (left.Y - (up.Z + fwd.X) + 1.0));
                    float y = (float)(s * 0.5);
                    s = (float)(0.5 / s);
                    return new Quaternion (
                        (fwd.Y + left.X) * s,
                        y,
                        (left.Z + up.Y) * s,
                        (up.X - fwd.Z) * s);
                } 

                s = (float)(Math.Sqrt (up.Z - (fwd.X + left.Y) + 1.0));
                float z = (float)(s * 0.5);
                s = (float)(0.5 / s);
                return new Quaternion (
                    (up.X + fwd.Z) * s,
                    (left.Z + up.Y) * s,
                    z,
                    (fwd.Y - left.X) * s);
                
            }
        }

        #endregion

        public static Random RandomClass {
            get { return randomClass; }
        }

        public static T Clamp<T> (T x, T min, T max)
            where T : IComparable<T>
        {
            return x.CompareTo (max) > 0
                       ? max
                       : x.CompareTo (min) < 0
                             ? min
                             : x;
        }

        public static uint GetNextXferID ()
        {
            uint id = 0;
            lock (XferLock) {
                id = nextXferID;
                nextXferID++;
            }
            return id;
        }

        /// <summary>
        ///     Debug utility function to convert unbroken strings of XML into something human readable for occasional debugging purposes.
        ///     Please don't delete me even if I appear currently unused!
        /// </summary>
        /// <param name="rawXml"></param>
        /// <returns></returns>
        public static string GetFormattedXml (string rawXml)
        {
            XmlDocument xd = new XmlDocument ();
            xd.LoadXml (rawXml);

            StringBuilder sb = new StringBuilder ();
            StringWriter sw = new StringWriter (sb);

            XmlTextWriter xtw = new XmlTextWriter (sw) { Formatting = Formatting.Indented };

            try {
                xd.WriteTo (xtw);
            } finally {
                xtw.Close ();
            }

            return sb.ToString ();
        }

        public static bool IsEnvironmentSupported (ref string reason)
        {
            // Must have .NET 2.0 (Generics / libsl)
            if (Environment.Version.Major < 2) {
                reason = ".NET 1.0/1.1 lacks components that are used by WhiteCore";
                return false;
            }

            // Windows 95/98/ME are unsupported
            if (Environment.OSVersion.Platform == PlatformID.Win32Windows ||
                Environment.OSVersion.Platform == PlatformID.Win32S ||
                Environment.OSVersion.Platform == PlatformID.WinCE) {
                reason = "Windows 95/98/ME will not run WhiteCore";
                return false;
            }

            // Windows 2000 / Pre-SP2 XP
            if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                Environment.OSVersion.Version.Major == 5 &&
                Environment.OSVersion.Version.Minor == 0) {
                reason = "Please update to Windows XP Service Pack 2 or Server 2003 with .NET 3.5 installed";
                return false;
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                Environment.Version.Major < 4 &&
                Environment.Version.Build < 50727) //.net 3.5
            {
                reason = ".NET versions before 3.5 lack components that are used by WhiteCore";
                return false;
            }

            return true;
        }

        public static bool IsLinux {
            get {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        /// <summary>
        /// Is the platform Windows?
        /// </summary>
        /// <returns>true if so, false otherwise</returns>
        public static bool IsWindows ()
        {
            PlatformID platformId = Environment.OSVersion.Platform;

            return (platformId == PlatformID.Win32NT
                || platformId == PlatformID.Win32S
                || platformId == PlatformID.Win32Windows
                || platformId == PlatformID.WinCE);
        }

        public static bool LoadArchSpecificWindowsDll (string libraryName)
        {
            // We do this so that WhiteCore on Windows loads the correct native library depending on whether
            // it's running as a 32-bit process or a 64-bit one.  By invoking LoadLibary here, later DLLImports
            // will find it already loaded later on.
            //
            // This isn't necessary for other platforms (e.g. Mac OSX and Linux) since the DLL used can be
            // controlled in config files.
            string nativeLibraryPath;

            if (Is64BitProcess ())
                nativeLibraryPath = "lib64/" + libraryName;
            else
                nativeLibraryPath = "lib32/" + libraryName;

            MainConsole.Instance.DebugFormat ("[Util]: Loading native Windows library at {0}", nativeLibraryPath);

            if (LoadLibrary (nativeLibraryPath) == IntPtr.Zero) {
                MainConsole.Instance.ErrorFormat (
                    "[Util]: Couldn't find native Windows library at {0}", nativeLibraryPath);

                return false;
            }

            return true;

        }

        /// <summary>
        /// Used to trigger an early library load on Windows systems.
        /// </summary>
        /// <remarks>
        /// Required to get 32-bit and 64-bit processes to automatically use the
        /// appropriate native library.
        /// </remarks>
        /// <param name="dllToLoad"></param>
        /// <returns></returns>
        [DllImport ("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary (string dllToLoad);

        /// <summary>
        /// Determine whether the current process is 64 bit
        /// </summary>
        /// <returns>true if so, false if not</returns>
        public static bool Is64BitProcess ()
        {
            return IntPtr.Size == 8;
        }

        public static int UnixTimeSinceEpoch ()
        {
            return ToUnixTime (DateTime.UtcNow);
        }

        public static int ToUnixTime (DateTime stamp)
        {
            TimeSpan t = stamp.ToUniversalTime () - UnixEpoch;
            return (int)t.TotalSeconds;
        }

        public static DateTime ToDateTime (ulong seconds)
        {
            DateTime epoch = UnixEpoch;
            return epoch.AddSeconds (seconds);
        }

        public static DateTime ToDateTime (int seconds)
        {
            DateTime epoch = UnixEpoch;
            return epoch.AddSeconds (seconds);
        }

        /// <summary>
        ///     Return an md5 hash of the given string
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string Md5Hash (string data)
        {
            byte [] dataMd5 = ComputeMD5Hash (data);
            StringBuilder sb = new StringBuilder ();
            foreach (byte t in dataMd5)
                sb.AppendFormat ("{0:x2}", t);
            return sb.ToString ();
        }

        static byte [] ComputeMD5Hash (string data)
        {
            MD5 md5 = MD5.Create ();
            return md5.ComputeHash (Encoding.Default.GetBytes (data));
        }

        /// <summary>
        ///     Return an SHA1 hash of the given string
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string SHA1Hash (string data)
        {
            byte [] hash = ComputeSHA1Hash (data);
            return BitConverter.ToString (hash).Replace ("-", string.Empty);
        }

        static byte [] ComputeSHA1Hash (string src)
        {
            SHA1CryptoServiceProvider SHA1 = new SHA1CryptoServiceProvider ();
            var hash = SHA1.ComputeHash (Encoding.Default.GetBytes (src));
            SHA1.Dispose ();
            return hash;
        }

        public static int fast_distance2d (int x, int y)
        {
            x = Math.Abs (x);
            y = Math.Abs (y);

            int min = Math.Min (x, y);

            return (x + y - (min >> 1) - (min >> 2) + (min >> 4));
        }

        // Inclusive, within range test (true if equal to the endpoints)
        public static bool InRange<T> (T x, T min, T max)
            where T : IComparable<T>
        {
            return x.CompareTo (max) <= 0 && x.CompareTo (min) >= 0;
        }

        // Clamp the maximum magnitude of a vector
        public static Vector3 ClampV (Vector3 x, float max)
        {
            float lenSq = x.LengthSquared ();
            if (lenSq > (max * max)) {
                x = x / x.Length () * max;
            }
            return x;
        }

        public static string FieldToString (byte [] bytes)
        {
            return FieldToString (bytes, string.Empty);
        }

        /// <summary>
        ///     Convert a variable length field (byte array) to a string, with a
        ///     field name prepended to each line of the output
        /// </summary>
        /// <remarks>
        ///     If the byte array has unprintable characters in it, a
        ///     hex dump will be put in the string instead
        /// </remarks>
        /// <param name="bytes">The byte array to convert to a string</param>
        /// <param name="fieldName">A field name to prepend to each line of output</param>
        /// <returns>
        ///     An ASCII string or a string containing a hex dump, minus
        ///     the null terminator
        /// </returns>
        public static string FieldToString (byte [] bytes, string fieldName)
        {
            // Check for a common case
            if (bytes.Length == 0) return string.Empty;

            StringBuilder output = new StringBuilder ();

            bool printable = bytes.All (t => (t >= 0x20 && t <= 0x7E) || t == 0x09 || t == 0x0D || t == 0x0A || t == 0x00);

            if (printable) {
                if (fieldName.Length > 0) {
                    output.Append (fieldName);
                    output.Append (": ");
                }

                output.Append (CleanString (UTF8.GetString (bytes, 0, bytes.Length - 1)));
            } else {
                for (int i = 0; i < bytes.Length; i += 16) {
                    if (i != 0)
                        output.Append (Environment.NewLine);
                    if (fieldName.Length > 0) {
                        output.Append (fieldName);
                        output.Append (": ");
                    }

                    for (int j = 0; j < 16; j++) {
                        output.Append ((i + j) < bytes.Length ? string.Format ("{0:X2} ", bytes [i + j]) : "   ");
                    }

                    for (int j = 0; j < 16 && (i + j) < bytes.Length; j++) {
                        if (bytes [i + j] >= 0x20 && bytes [i + j] < 0x7E)
                            output.Append ((char)bytes [i + j]);
                        else
                            output.Append (".");
                    }
                }
            }

            return output.ToString ();
        }

        /// <summary>
        ///     Removes all invalid path chars (OS dependent)
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>safe path</returns>
        public static string safePath (string path)
        {
            return Regex.Replace (path, regexInvalidPathChars, string.Empty);
        }

        /// <summary>
        ///     Removes all invalid filename chars (OS dependent)
        /// </summary>
        /// <param name="filename">filename</param>
        /// <returns>safe filename</returns>
        public static string safeFileName (string filename)
        {
            return Regex.Replace (filename, regexInvalidFileChars, string.Empty);
        }

        //
        // directory locations
        //

        public static string homeDir ()
        {
            string temp;
            //            string personal=(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
            //            temp = Path.Combine(personal,".WhiteCore");
            temp = ".";
            return temp;
        }

        public static string configDir ()
        {
            return ".";
        }

        public static string dataDir ()
        {
            return ".";
        }

        public static string logDir ()
        {
            return ".";
        }

        // From: http://coercedcode.blogspot.com/2008/03/c-generate-unique-filenames-within.html
        public static string GetUniqueFilename (string FileName)
        {
            int count = 0;
            string Name;

            if (File.Exists (FileName)) {
                FileInfo f = new FileInfo (FileName);

                Name = !string.IsNullOrEmpty (f.Extension)
                           ? f.FullName.Substring (0, f.FullName.LastIndexOf ('.'))
                           : f.FullName;

                while (File.Exists (FileName)) {
                    count++;
                    FileName = Name + count + f.Extension;
                }
            }
            return FileName;
        }

        // Nini (config) related Methods
        public static IConfigSource ConvertDataRowToXMLConfig (DataRow row, string fileName)
        {
            if (!File.Exists (fileName)) {
                //create new file
            }
            XmlConfigSource config = new XmlConfigSource (fileName);
            AddDataRowToConfig (config, row);
            config.Save ();

            return config;
        }

        public static void AddDataRowToConfig (IConfigSource config, DataRow row)
        {
            config.Configs.Add ((string)row [0]);
            for (int i = 0; i < row.Table.Columns.Count; i++) {
                config.Configs [(string)row [0]].Set (row.Table.Columns [i].ColumnName, row [i]);
            }
        }

        public static float Clip (float x, float min, float max)
        {
            return Math.Min (Math.Max (x, min), max);
        }

        public static int Clip (int x, int min, int max)
        {
            return Math.Min (Math.Max (x, min), max);
        }

        /// <summary>
        ///     Convert an UUID to a raw uuid string.  Right now this is a string without hyphens.
        /// </summary>
        /// <param name="UUID"></param>
        /// <returns></returns>
        public static string ToRawUuidString (UUID UUID)
        {
            return UUID.Guid.ToString ("n");
        }

        public static string CleanString (string input)
        {
            if (input.Length == 0)
                return input;

            int clip = input.Length;

            // Test for ++ string terminator
            int pos = input.IndexOf ("\0");
            if (pos != -1 && pos < clip)
                clip = pos;

            // Test for CR
            pos = input.IndexOf ("\r");
            if (pos != -1 && pos < clip)
                clip = pos;

            // Test for LF
            pos = input.IndexOf ("\n");
            if (pos != -1 && pos < clip)
                clip = pos;

            // Truncate string before first end-of-line character found
            return input.Substring (0, clip);
        }

        /// <summary>
        ///     returns the contents of /etc/issue on Unix Systems
        ///     Use this for where it's absolutely necessary to implement platform specific stuff
        /// </summary>
        /// <returns></returns>
        public static string ReadEtcIssue ()
        {
            StreamReader sr = null;
            try {
                sr = new StreamReader ("/etc/issue.net");
                string issue = sr.ReadToEnd ();
                sr.Close ();
                return issue;
            } catch (Exception) {
                if (sr != null)
                    sr.Close ();
                return "";
            }
        }

        public static void Compress7ZipFile (string path, string destination)
        {
            ProcessStartInfo p = new ProcessStartInfo ();
            string pa = Path.GetDirectoryName (Assembly.GetAssembly (typeof (Util)).CodeBase);
            if (pa != null) {
                p.FileName = Path.Combine (pa, "7za.exe");
                p.Arguments = "a -y -tgzip \"" + destination + "\" \"" + path + "\" -mx=9";
                p.WindowStyle = ProcessWindowStyle.Hidden;
                Process x = Process.Start (p);
                x.WaitForExit ();
            }
        }

        public static void UnCompress7ZipFile (string path, string destination)
        {
            ProcessStartInfo p = new ProcessStartInfo ();
            string pa = Path.GetDirectoryName (Assembly.GetAssembly (typeof (Util)).CodeBase);
            if (pa != null) {
                p.FileName = Path.Combine (pa, "7za.exe");
                p.Arguments = "e -y \"" + path + "\" -o\"" + destination + "\" -mx=9";
                p.WindowStyle = ProcessWindowStyle.Hidden;
                Process x = Process.Start (p);
                x.WaitForExit ();
            }
        }

        public static string Compress (string text)
        {
            byte [] buffer = UTF8.GetBytes (text);
            MemoryStream memory = new MemoryStream ();
            using (GZipStream compressor = new GZipStream (memory, CompressionMode.Compress, true)) {
                compressor.Write (buffer, 0, buffer.Length);
            }

            memory.Position = 0;

            byte [] compressed = new byte [memory.Length];
            memory.Read (compressed, 0, compressed.Length);

            byte [] compressedBuffer = new byte [compressed.Length + 4];
            Buffer.BlockCopy (compressed, 0, compressedBuffer, 4, compressed.Length);
            Buffer.BlockCopy (BitConverter.GetBytes (buffer.Length), 0, compressedBuffer, 0, 4);
            return Convert.ToBase64String (compressedBuffer);
        }

        public static byte [] CompressBytes (byte [] buffer)
        {
            MemoryStream memory = new MemoryStream ();
            using (GZipStream compressor = new GZipStream (memory, CompressionMode.Compress, true)) {
                compressor.Write (buffer, 0, buffer.Length);
            }

            memory.Position = 0;

            byte [] compressed = new byte [memory.Length];
            memory.Read (compressed, 0, compressed.Length);

            byte [] compressedBuffer = new byte [compressed.Length + 4];
            Buffer.BlockCopy (compressed, 0, compressedBuffer, 4, compressed.Length);
            Buffer.BlockCopy (BitConverter.GetBytes (buffer.Length), 0, compressedBuffer, 0, 4);
            return compressedBuffer;
        }

        public static string Decompress (string compressedText)
        {
            byte [] compressedBuffer = Convert.FromBase64String (compressedText);
            using (MemoryStream memory = new MemoryStream ()) {
                int msgLength = BitConverter.ToInt32 (compressedBuffer, 0);
                memory.Write (compressedBuffer, 4, compressedBuffer.Length - 4);

                byte [] buffer = new byte [msgLength];

                memory.Position = 0;
                using (GZipStream decompressor = new GZipStream (memory, CompressionMode.Decompress)) {
                    decompressor.Read (buffer, 0, buffer.Length);
                }

                return UTF8.GetString (buffer);
            }
        }

        public static Stream DecompressStream (Stream compressedStream)
        {
            byte [] compressedBuffer = ReadToEnd (compressedStream);
            using (MemoryStream memory = new MemoryStream ()) {
                int msgLength = BitConverter.ToInt32 (compressedBuffer, 0);
                memory.Write (compressedBuffer, 4, compressedBuffer.Length - 4);

                byte [] buffer = new byte [msgLength];

                memory.Position = 0;
                using (GZipStream decompressor = new GZipStream (memory, CompressionMode.Decompress)) {
                    decompressor.Read (buffer, 0, buffer.Length);
                }

                return new MemoryStream (buffer);
            }
        }

        public static byte [] ReadToEnd (Stream stream)
        {
            byte [] readBuffer = new byte [4096];

            int totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = stream.Read (readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0) {
                totalBytesRead += bytesRead;

                if (totalBytesRead == readBuffer.Length) {
                    int nextByte = stream.ReadByte ();
                    if (nextByte != -1) {
                        byte [] temp = new byte [readBuffer.Length * 2];
                        Buffer.BlockCopy (readBuffer, 0, temp, 0, readBuffer.Length);
                        Buffer.SetByte (temp, totalBytesRead, (byte)nextByte);
                        readBuffer = temp;
                        totalBytesRead++;
                    }
                }
            }

            byte [] buffer = readBuffer;
            if (readBuffer.Length != totalBytesRead) {
                buffer = new byte [totalBytesRead];
                Buffer.BlockCopy (readBuffer, 0, buffer, 0, totalBytesRead);
            }
            return buffer;
        }

        /// <summary>
        ///     Converts a byte array in big endian order into an ulong.
        /// </summary>
        /// <param name="bytes">
        ///     The array of bytes
        /// </param>
        /// <returns>
        ///     The extracted ulong
        /// </returns>
        public static ulong BytesToUInt64Big (byte [] bytes)
        {
            if (bytes.Length < 8) return 0;
            return ((ulong)bytes [0] << 56) | ((ulong)bytes [1] << 48) | ((ulong)bytes [2] << 40) |
                   ((ulong)bytes [3] << 32) |
                   ((ulong)bytes [4] << 24) | ((ulong)bytes [5] << 16) | ((ulong)bytes [6] << 8) | bytes [7];
        }

        // used for RemoteParcelRequest (for "About Landmark")
        public static UUID BuildFakeParcelID (ulong regionHandle, uint x, uint y)
        {
            byte [] bytes =
                {
                    (byte) regionHandle, (byte) (regionHandle >> 8), (byte) (regionHandle >> 16),
                    (byte) (regionHandle >> 24),
                    (byte) (regionHandle >> 32), (byte) (regionHandle >> 40), (byte) (regionHandle >> 48),
                    (byte) (regionHandle << 56),
                    (byte) x, (byte) (x >> 8), 0, 0,
                    (byte) y, (byte) (y >> 8), 0, 0
                };
            return new UUID (bytes, 0);
        }

        public static UUID BuildFakeParcelID (ulong regionHandle, uint x, uint y, uint z)
        {
            byte [] bytes =
                {
                    (byte) regionHandle, (byte) (regionHandle >> 8), (byte) (regionHandle >> 16),
                    (byte) (regionHandle >> 24),
                    (byte) (regionHandle >> 32), (byte) (regionHandle >> 40), (byte) (regionHandle >> 48),
                    (byte) (regionHandle << 56),
                    (byte) x, (byte) (x >> 8), (byte) z, (byte) (z >> 8),
                    (byte) y, (byte) (y >> 8), 0, 0
                };
            return new UUID (bytes, 0);
        }

        public static void ParseFakeParcelID (UUID parcelID, out ulong regionHandle, out uint x, out uint y)
        {
            byte [] bytes = parcelID.GetBytes ();
            regionHandle = Utils.BytesToUInt64 (bytes);
            x = Utils.BytesToUInt (bytes, 8) & 0xffff;
            y = Utils.BytesToUInt (bytes, 12) & 0xffff;
        }

        public static void ParseFakeParcelID (UUID parcelID, out ulong regionHandle, out uint x, out uint y, out uint z)
        {
            byte [] bytes = parcelID.GetBytes ();
            regionHandle = Utils.BytesToUInt64 (bytes);
            x = Utils.BytesToUInt (bytes, 8) & 0xffff;
            z = (Utils.BytesToUInt (bytes, 8) & 0xffff0000) >> 16;
            y = Utils.BytesToUInt (bytes, 12) & 0xffff;
        }

        public static void FakeParcelIDToGlobalPosition (UUID parcelID, out uint x, out uint y)
        {
            ulong regionHandle;
            uint rx, ry;

            ParseFakeParcelID (parcelID, out regionHandle, out x, out y);
            Utils.LongToUInts (regionHandle, out rx, out ry);

            x += rx;
            y += ry;
        }

        /// <summary>
        ///     Get operating system information if available.  Returns only the first 45 characters of information
        /// </summary>
        /// <returns>
        ///     Operating system information.  Returns an empty string if none was available.
        /// </returns>
        public static string GetOperatingSystemInformation ()
        {
            string os = string.Empty;

            os = Environment.OSVersion.Platform != PlatformID.Unix ? Environment.OSVersion.ToString () : ReadEtcIssue ();

            if (os.Length > 45) {
                os = os.Substring (0, 45);
            }

            return os;
        }

        public static string GetRuntimeInformation ()
        {
            string ru = string.Empty;

            switch (Environment.OSVersion.Platform) {
            case PlatformID.Unix:
                ru = "Unix/Mono";
                break;
            case PlatformID.MacOSX:
                ru = "OSX/Mono";
                break;
            default:
                ru = Type.GetType ("Mono.Runtime") != null ? "Win/Mono" : "Win/.NET";
                break;
            }

            return ru;
        }

        public static RuntimeEnvironment GetRuntimeEnvironment ()
        {
            switch (Environment.OSVersion.Platform) {
            case PlatformID.Unix:
                return RuntimeEnvironment.Mono;
            case PlatformID.MacOSX:
                return RuntimeEnvironment.Mono;
            default:
                return Type.GetType ("Mono.Runtime") != null
                           ? RuntimeEnvironment.WinMono
                           : RuntimeEnvironment.NET;
            }
        }

        /// <summary>
        ///     Is the given string a UUID?
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool isUUID (string s)
        {
            return UUIDPattern.IsMatch (s);
        }

        public static string GetDisplayConnectionString (string connectionString)
        {
            int passPosition = 0;
            int passEndPosition = 0;
            string displayConnectionString = null;

            // hide the password in the connection string
            passPosition = connectionString.IndexOf ("password", StringComparison.OrdinalIgnoreCase);
            passPosition = connectionString.IndexOf ("=", passPosition, StringComparison.Ordinal);
            if (passPosition < connectionString.Length)
                passPosition += 1;
            passEndPosition = connectionString.IndexOf (";", passPosition, StringComparison.Ordinal);

            displayConnectionString = connectionString.Substring (0, passPosition);
            displayConnectionString += "***";
            displayConnectionString += connectionString.Substring (passEndPosition, connectionString.Length - passEndPosition);

            return displayConnectionString;
        }

        public static T ReadSettingsFromIniFile<T> (IConfig config, T settingsClass)
        {
            Type settingsType = settingsClass.GetType ();

            FieldInfo [] fieldInfos = settingsType.GetFields ();
            foreach (FieldInfo fieldInfo in fieldInfos) {
                if (!fieldInfo.IsStatic) {
                    if (fieldInfo.FieldType == typeof (string)) {
                        fieldInfo.SetValue (settingsClass,
                                           config.Get (fieldInfo.Name, (string)fieldInfo.GetValue (settingsClass)));
                    } else if (fieldInfo.FieldType == typeof (bool)) {
                        fieldInfo.SetValue (settingsClass,
                                           config.GetBoolean (fieldInfo.Name, (bool)fieldInfo.GetValue (settingsClass)));
                    } else if (fieldInfo.FieldType == typeof (int)) {
                        fieldInfo.SetValue (settingsClass,
                                           config.GetInt (fieldInfo.Name, (int)fieldInfo.GetValue (settingsClass)));
                    } else if (fieldInfo.FieldType == typeof (float)) {
                        fieldInfo.SetValue (settingsClass,
                                           config.GetFloat (fieldInfo.Name, (float)fieldInfo.GetValue (settingsClass)));
                    } else if (fieldInfo.FieldType == typeof (uint)) {
                        fieldInfo.SetValue (settingsClass,
                                           Convert.ToUInt32 (config.Get (fieldInfo.Name,
                                                                       ((uint)fieldInfo.GetValue (settingsClass))
                                                                           .ToString ())));
                    }
                }
            }

            PropertyInfo [] propertyInfos = settingsType.GetProperties ();
            foreach (PropertyInfo propInfo in propertyInfos) {
                if ((propInfo.CanRead) && (propInfo.CanWrite)) {
                    if (propInfo.PropertyType == typeof (string)) {
                        propInfo.SetValue (settingsClass,
                                          config.Get (propInfo.Name, (string)propInfo.GetValue (settingsClass, null)),
                                          null);
                    } else if (propInfo.PropertyType == typeof (bool)) {
                        propInfo.SetValue (settingsClass,
                                          config.GetBoolean (propInfo.Name, (bool)propInfo.GetValue (settingsClass, null)),
                                          null);
                    } else if (propInfo.PropertyType == typeof (int)) {
                        propInfo.SetValue (settingsClass,
                                          config.GetInt (propInfo.Name, (int)propInfo.GetValue (settingsClass, null)),
                                          null);
                    } else if (propInfo.PropertyType == typeof (float)) {
                        propInfo.SetValue (settingsClass,
                                          config.GetFloat (propInfo.Name, (float)propInfo.GetValue (settingsClass, null)),
                                          null);
                    }
                    if (propInfo.PropertyType == typeof (uint)) {
                        propInfo.SetValue (settingsClass,
                                          Convert.ToUInt32 (config.Get (propInfo.Name,
                                                                      ((uint)propInfo.GetValue (settingsClass, null))
                                                                          .ToString ())), null);
                    }
                }
            }

            return settingsClass;
        }

        public static Guid GetHashGuid (string data, string salt)
        {
            byte [] hash = ComputeMD5Hash (data + salt);

            //string s = BitConverter.ToString(hash);

            Guid guid = new Guid (hash);

            return guid;
        }

        public static byte ConvertMaturityToAccessLevel (uint maturity)
        {
            byte retVal = 0;
            switch (maturity) {
            case 0: //PG
                retVal = 13;
                break;
            case 1: //Mature
                retVal = 21;
                break;
            case 2: // Adult
                retVal = 42;
                break;
            }

            return retVal;
        }

        public static uint ConvertAccessLevelToMaturity (byte maturity)
        {
            uint retVal = 0;
            switch (maturity) {
            case 13: //PG
                retVal = 0;
                break;
            case 21: //Mature
                retVal = 1;
                break;
            case 42: // Adult
                retVal = 2;
                break;
            }

            return retVal;
        }

        /// <summary>
        ///     Produces an OSDMap from its string representation on a stream
        /// </summary>
        /// <param name="stream">The stream</param>
        /// <param name="length">The size of the data on the stream</param>
        /// <returns>The OSDMap or an exception</returns>
        public static OSDMap GetOSDMap (Stream stream, int length)
        {
            byte [] data = new byte [length];
            stream.Read (data, 0, length);
            string strdata = UTF8.GetString (data);
            OSDMap args = null;
            OSD buffer;
            buffer = OSDParser.DeserializeJson (strdata);
            if (buffer.Type == OSDType.Map) {
                args = (OSDMap)buffer;
                return args;
            }
            return null;
        }

        public static OSDMap GetOSDMap (string data)
        {
            OSDMap args = null;
            try {
                OSD buffer;
                // We should pay attention to the content-type, but let's assume we know it's Json
                buffer = OSDParser.DeserializeJson (data);
                if (buffer.Type == OSDType.Map) {
                    args = (OSDMap)buffer;
                    return args;
                } else {
                    // uh?
                    MainConsole.Instance.Debug (("[Util]: Got OSD of unexpected type " + buffer.Type));
                    return null;
                }
            } catch (Exception ex) {
                MainConsole.Instance.Debug ("[Util]: exception on GetOSDMap " + ex);
                return null;
            }
        }

        public static string [] Glob (string path)
        {
            string vol = string.Empty;

            if (Path.VolumeSeparatorChar != Path.DirectorySeparatorChar) {
                string [] vcomps = path.Split (new [] { Path.VolumeSeparatorChar }, 2, StringSplitOptions.RemoveEmptyEntries);

                if (vcomps.Length > 1) {
                    path = vcomps [1];
                    vol = vcomps [0];
                }
            }

            string [] comps = path.Split (new [] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                                        StringSplitOptions.RemoveEmptyEntries);

            // Glob

            path = vol;
            if (vol != string.Empty)
                path += new string (new [] { Path.VolumeSeparatorChar, Path.DirectorySeparatorChar });
            else
                path = new string (new [] { Path.DirectorySeparatorChar });

            List<string> paths = new List<string> ();
            List<string> found = new List<string> ();
            paths.Add (path);

            int compIndex = -1;
            foreach (string c in comps) {
                compIndex++;

                List<string> addpaths = new List<string> ();
                foreach (string p in paths) {
                    string [] dirs = Directory.GetDirectories (p, c);

                    if (dirs.Length != 0) {
                        addpaths.AddRange (dirs.Select (dir => Path.Combine (path, dir)));
                    }

                    // Only add files if that is the last path component
                    if (compIndex == comps.Length - 1) {
                        string [] files = Directory.GetFiles (p, c);
                        found.AddRange (files);
                    }
                }
                paths = addpaths;
            }

            return found.ToArray ();
        }

        public static string [] GetSubFiles (string path)
        {
            string [] comps = path.Split (new [] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                                        StringSplitOptions.None);
            List<string> paths = new List<string> ();
            string endFind = comps [comps.Length - 1];
            string baseDir = "";
            for (int i = 0; i < comps.Length - 1; i++) {
                if (i == comps.Length - 2)
                    baseDir += comps [i];
                else
                    baseDir += comps [i] + Path.DirectorySeparatorChar;
            }
            paths.Add (baseDir);

            return paths.SelectMany (p => Directory.GetFiles (p, endFind)).ToArray ();
        }

        public static byte [] StringToBytes256 (string str, params object [] args)
        {
            return StringToBytes256 (string.Format (str, args));
        }

        public static byte [] StringToBytes1024 (string str, params object [] args)
        {
            return StringToBytes1024 (string.Format (str, args));
        }

        public static byte [] StringToBytes256 (string str)
        {
            if (string.IsNullOrEmpty (str))
                return Utils.EmptyBytes;

            if (str.Length > 254) str = str.Remove (254);
            if (!str.EndsWith ("\0", StringComparison.Ordinal)) {
                str += "\0";
            }

            // Because this is UTF-8 encoding and not ASCII, it's possible we
            // might have gotten an oversized array even after the string trim
            byte [] data = UTF8.GetBytes (str);
            if (data.Length > 256) {
                Array.Resize (ref data, 256);
                data [255] = 0;
            }

            return data;
        }

        public static byte [] StringToBytes1024 (string str)
        {
            if (string.IsNullOrEmpty (str)) {
                return Utils.EmptyBytes;
            }
            if (str.Length > 1023) str = str.Remove (1023);
            if (!str.EndsWith ("\0", StringComparison.Ordinal)) {
                str += "\0";
            }

            // Because this is UTF-8 encoding and not ASCII, it's possible we
            // might have gotten an oversized array even after the string trim
            byte [] data = UTF8.GetBytes (str);
            if (data.Length > 1024) {
                Array.Resize (ref data, 1024);
                data [1023] = 0;
            }

            return data;
        }

        #region FireAndForget Threading Pattern

        /// <summary>
        ///     Created to work around a limitation in Mono with nested delegates
        /// </summary>
        sealed class FireAndForgetWrapper
        {
            static volatile FireAndForgetWrapper instance;
            static readonly object syncRoot = new object ();

            public static FireAndForgetWrapper Instance {
                get {
                    if (instance == null) {
                        lock (syncRoot) {
                            if (instance == null) {
                                instance = new FireAndForgetWrapper ();
                            }
                        }
                    }

                    return instance;
                }
            }

            public void FireAndForget (WaitCallback callback)
            {
                callback.BeginInvoke (null, EndFireAndForget, callback);
            }

            public void FireAndForget (WaitCallback callback, object obj)
            {
                callback.BeginInvoke (obj, EndFireAndForget, callback);
            }

            static void EndFireAndForget (IAsyncResult ar)
            {
                WaitCallback callback = (WaitCallback)ar.AsyncState;

                try {
                    callback.EndInvoke (ar);
                } catch (Exception ex) {
                    MainConsole.Instance.ErrorFormat ("[Util]: Asynchronous method threw an exception: {0}", ex.ToString ());
                }

                ar.AsyncWaitHandle.Close ();
            }
        }

        public static void FireAndForget (WaitCallback callback)
        {
            FireAndForget (callback, null);
        }

        public static void InitThreadPool (int minThreads, int maxThreads)
        {
            if (m_ThreadPool != null) {
                MainConsole.Instance.Warn ("SmartThreadPool is already initialized");
                return;
            }

            // should not be necessary but you never can tell...
            if (maxThreads < 2) {
                maxThreads = 2;
                MainConsole.Instance.Warn ("[STP]: maxThreads must be greater than 2");
            }

            if (minThreads > maxThreads || minThreads < 2) {
                minThreads = 2;
                MainConsole.Instance.Warn ("[STP]: minThreads must be greater than 2 and <= maxThreads");
            }


            STPStartInfo startInfo = new STPStartInfo ();
            startInfo.ThreadPoolName = "Util";
            startInfo.IdleTimeout = 2000;
            startInfo.MinWorkerThreads = minThreads;
            startInfo.MaxWorkerThreads = maxThreads;

            m_ThreadPool = new SmartThreadPool (startInfo);
            m_threadPoolRunning = true;
            //  old - remove if OK //m_ThreadPool = new SmartThreadPool(2000, maxThreads, 2);

        }

        public static void CloseThreadPool ()
        {
            if (FireAndForgetMethod == FireAndForgetMethod.SmartThreadPool &&
                m_ThreadPool != null) {
                //This stops more tasks and threads from being started
                m_threadPoolRunning = false;
                m_ThreadPool.WaitForIdle (60 * 1000);
                //Wait for the threads to be idle, but don't wait for more than a minute
                //Destroy the threadpool now
                m_ThreadPool.Dispose ();
                m_ThreadPool = null;
            }
        }

        public static int FireAndForgetCount ()
        {
            const int MAX_SYSTEM_THREADS = 200;

            switch (FireAndForgetMethod) {
            case FireAndForgetMethod.UnsafeQueueUserWorkItem:
            case FireAndForgetMethod.QueueUserWorkItem:
            case FireAndForgetMethod.BeginInvoke:
                int workerThreads, iocpThreads;
                ThreadPool.GetAvailableThreads (out workerThreads, out iocpThreads);
                return workerThreads;
            case FireAndForgetMethod.SmartThreadPool:
                if (m_ThreadPool == null || !m_threadPoolRunning)
                    return 0;
                return m_ThreadPool.MaxThreads - m_ThreadPool.InUseThreads;
            case FireAndForgetMethod.Thread:
                return MAX_SYSTEM_THREADS - Process.GetCurrentProcess ().Threads.Count;
            default:
                throw new NotImplementedException ();
            }
        }

        public static void FireAndForget (WaitCallback callback, object obj)
        {
            switch (FireAndForgetMethod) {
            case FireAndForgetMethod.UnsafeQueueUserWorkItem:
                ThreadPool.UnsafeQueueUserWorkItem (callback, obj);
                break;
            case FireAndForgetMethod.QueueUserWorkItem:
                ThreadPool.QueueUserWorkItem (callback, obj);
                break;
            case FireAndForgetMethod.BeginInvoke:
                FireAndForgetWrapper wrapper = FireAndForgetWrapper.Instance;
                wrapper.FireAndForget (callback, obj);
                break;
            case FireAndForgetMethod.SmartThreadPool:
                if (m_ThreadPool == null)
                    InitThreadPool (2, 15);      // this should create the threadpool
                if (m_ThreadPool != null)       // make sure
                    if (m_threadPoolRunning)    // Check if the thread pool should be running
                        m_ThreadPool.QueueWorkItem ((WorkItemCallback)SmartThreadPoolCallback, new [] { callback, obj });
                break;
            case FireAndForgetMethod.Thread:
                Thread thread = new Thread (delegate (object o) {
                    Culture.SetCurrentCulture ();
                    callback (o);
                });
                thread.Start (obj);
                break;
            default:
                throw new NotImplementedException ();
            }
        }

        static object SmartThreadPoolCallback (object o)
        {
            object [] array = (object [])o;
            WaitCallback callback = (WaitCallback)array [0];
            object obj = array [1];

            callback (obj);
            return null;
        }

        #endregion FireAndForget Threading Pattern

        /// <summary>
        ///     Environment.TickCount is an int but it counts all 32 bits so it goes positive
        ///     and negative every 24.9 days. This trims down TickCount so it doesn't wrap
        ///     for the callers.
        ///     This trims it to a 12 day interval so don't let your frame time get too long.
        /// </summary>
        /// <returns></returns>
        public static int EnvironmentTickCount ()
        {
            return Environment.TickCount & EnvironmentTickCountMask;
        }

        public const int EnvironmentTickCountMask = 0x3fffffff;

        /// <summary>
        ///     Environment.TickCount is an int but it counts all 32 bits so it goes positive
        ///     and negative every 24.9 days. Subtracts the passed value (previously fetched by
        ///     'EnvironmentTickCount()') and accounts for any wrapping.
        /// </summary>
        /// <returns>subtraction of passed prevValue from current Environment.TickCount</returns>
        public static int EnvironmentTickCountSubtract (int prevValue)
        {
            int diff = EnvironmentTickCount () - prevValue;
            return (diff >= 0) ? diff : (diff + EnvironmentTickCountMask + 1);
        }

        /// <summary>
        ///     Environment.TickCount is an int but it counts all 32 bits so it goes positive
        ///     and negative every 24.9 days. Adds the passed value (previously fetched by
        ///     'EnvironmentTickCount()') and accounts for any wrapping.
        /// </summary>
        /// <returns>addition of passed prevValue from current Environment.TickCount</returns>
        public static int EnvironmentTickCountAdd (int ms)
        {
            int add = EnvironmentTickCount () + ms;
            return (add >= EnvironmentTickCountMask) ? add - EnvironmentTickCountMask : add;
        }

        /// <summary>
        ///     Prints the call stack at any given point. Useful for debugging.
        /// </summary>
        public static void PrintCallStack ()
        {
            StackTrace stackTrace = new StackTrace (); // get call stack
            StackFrame [] stackFrames = stackTrace.GetFrames (); // get method calls (frames)

            // write call stack method names
            foreach (StackFrame stackFrame in stackFrames) {
                MainConsole.Instance.Debug (stackFrame.GetMethod ().DeclaringType + "." + stackFrame.GetMethod ().Name);
                // write method name
            }
        }

        public static OSDMap DictionaryToOSD (Dictionary<string, object> sendData)
        {
            OSDMap map = new OSDMap ();
            foreach (KeyValuePair<string, object> kvp in sendData) {
                if (kvp.Value is Dictionary<string, object>) {
                    map [kvp.Key] = DictionaryToOSD (kvp.Value as Dictionary<string, object>);
                } else {
                    OSD v = OSD.FromObject (kvp.Value);
                    OSD val = kvp.Value as OSD;
                    map [kvp.Key] = v.Type == OSDType.Unknown ? val : v;
                }
            }
            return map;
        }

        public static Dictionary<string, object> OSDToDictionary (OSDMap map)
        {
            Dictionary<string, object> retVal = new Dictionary<string, object> ();
            foreach (string key in map.Keys) {
                retVal.Add (key, OSDToObject (map [key]));
            }
            return retVal;
        }

        public static OSD MakeOSD (object o, Type t)
        {
            if (o is OSD)
                return (OSD)o;
            if (o is System.Drawing.Image)
                return OSD.FromBinary (ImageToByteArray (o as System.Drawing.Image));
            OSD oo;
            if ((oo = OSD.FromObject (o)).Type != OSDType.Unknown)
                return oo;
            if (o is IDataTransferable)
                return ((IDataTransferable)o).ToOSD ();
            Type [] genericArgs = t.GetGenericArguments ();
            if (IsInstanceOfGenericType (typeof (List<>), t)) {
                OSDArray array = new OSDArray ();
                IList collection = (IList)o;
                foreach (object item in collection) {
                    array.Add (MakeOSD (item, genericArgs [0]));
                }
                return array;
            }

            if (Util.IsInstanceOfGenericType (typeof (Dictionary<,>), t)) {
                OSDMap array = new OSDMap ();
                IDictionary collection = (IDictionary)o;
                foreach (DictionaryEntry item in collection) {
                    array.Add (MakeOSD (item.Key, genericArgs [0]), MakeOSD (item.Value, genericArgs [1]));
                }
                return array;
            }
            if (t.BaseType == typeof (Enum))
                return OSD.FromString (o.ToString ());
            return null;
        }

        public static byte [] ImageToByteArray (System.Drawing.Image imageIn)
        {
            MemoryStream ms = new MemoryStream ();
            imageIn.Save (ms, System.Drawing.Imaging.ImageFormat.Gif);
            return ms.ToArray ();
        }

        public static System.Drawing.Image ByteArrayToImage (byte [] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream (byteArrayIn);
            System.Drawing.Image returnImage = System.Drawing.Image.FromStream (ms);
            return returnImage;
        }

        static object CreateInstance (Type type)
        {
            if (type == typeof (string))
                return string.Empty;

            return Activator.CreateInstance (type);
        }

        public static object OSDToObject (OSD o)
        {
            return OSDToObject (o, o.GetType ());
        }

        public static object OSDToObject (OSD o, Type PossibleArrayType)
        {
            if (o.Type == OSDType.UUID || PossibleArrayType == typeof (UUID))
                return o.AsUUID ();
            if (PossibleArrayType == typeof (string) || PossibleArrayType == typeof (OSDString) ||
                PossibleArrayType.BaseType == typeof (Enum)) {
                if (PossibleArrayType.BaseType == typeof (Enum))
                    return Enum.Parse (PossibleArrayType, o.AsString ());

                return o.AsString ();
            }
            if (o.Type == OSDType.Array && PossibleArrayType == typeof (System.Drawing.Image))
                return ByteArrayToImage (o.AsBinary ());

            if (o.Type == OSDType.Integer && PossibleArrayType == typeof (byte))
                return (byte)o.AsInteger ();
            if (o.Type == OSDType.Integer || PossibleArrayType == typeof (int))
                return o.AsInteger ();
            if (o.Type == OSDType.Binary || PossibleArrayType == typeof (byte []))
                return o.AsBinary ();
            if (o.Type == OSDType.Boolean || PossibleArrayType == typeof (bool))
                return o.AsBoolean ();
            if (PossibleArrayType == typeof (Color4))
                return o.AsColor4 ();
            if (o.Type == OSDType.Date || PossibleArrayType == typeof (DateTime))
                return o.AsDate ();
            if (PossibleArrayType == typeof (long))
                return o.AsLong ();
            if (PossibleArrayType == typeof (Quaternion))
                return o.AsQuaternion ();
            if (PossibleArrayType == typeof (float))
                return (float)o.AsReal ();
            if (o.Type == OSDType.Real || PossibleArrayType == typeof (double))
                return o.AsReal ();
            if (PossibleArrayType == typeof (uint))
                return o.AsUInteger ();
            if (PossibleArrayType == typeof (ulong))
                return o.AsULong ();
            if (o.Type == OSDType.URI || PossibleArrayType == typeof (Uri))
                return o.AsUri ();
            if (PossibleArrayType == typeof (Vector2))
                return o.AsVector2 ();
            if (PossibleArrayType == typeof (Vector3))
                return o.AsVector3 ();
            if (PossibleArrayType == typeof (Vector3d))
                return o.AsVector3d ();
            if (PossibleArrayType == typeof (Vector4))
                return o.AsVector4 ();
            if (PossibleArrayType == typeof (OSDMap))
                return o;
            if (PossibleArrayType == typeof (OSDArray))
                return o;
            if (o.Type == OSDType.Array) {
                OSDArray array = (OSDArray)o;
                var possArrayType = Activator.CreateInstance (PossibleArrayType);
                IList list = (IList)possArrayType;
                Type t = PossibleArrayType.GetGenericArguments () [0];
                if (t == typeof (UInt32))
                    return o.AsUInteger ();

                foreach (OSD oo in array) {
                    list.Add (OSDToObject (oo, t));
                }
                return list;
            }

            var possType = Activator.CreateInstance (PossibleArrayType);
            if (possType is IDataTransferable) {
                IDataTransferable data = (IDataTransferable)possType;
                data.FromOSD ((OSDMap)o);
                return data;
            }

            if (o.Type == OSDType.Map) {
                OSDMap array = (OSDMap)o;
                var possArrayTypeB = Activator.CreateInstance (PossibleArrayType);
                var list = (IDictionary)possArrayTypeB;
                Type t = PossibleArrayType.GetGenericArguments () [1];
                Type tt = PossibleArrayType.GetGenericArguments () [0];
                foreach (KeyValuePair<string, OSD> oo in array) {
                    list.Add (OSDToObject (oo.Key, tt), OSDToObject (oo.Value, t));
                }
                return list;
            }
            return null;
        }

        public static List<T> MakeList<T> (T itemOftype)
        {
            List<T> newList = new List<T> ();
            return newList;
        }

        public static Dictionary<A, B> MakeDictionary<A, B> (A itemOftypeA, B itemOfTypeB)
        {
            Dictionary<A, B> newList = new Dictionary<A, B> ();
            return newList;
        }

        public static void UlongToInts (ulong regionHandle, out int x, out int y)
        {
            uint xx, yy;
            Utils.LongToUInts (regionHandle, out xx, out yy);
            x = (int)xx;
            y = (int)yy;
        }

        public static ulong IntsToUlong (int x, int y)
        {
            return Utils.UIntsToLong ((uint)x, (uint)y);
        }

        public static string CombineParams (string [] commandParams, int pos)
        {
            string result = string.Empty;
            for (int i = pos; i < commandParams.Length; i++) {
                result += commandParams [i] + " ";
            }
            result = result.Remove (result.Length - 1, 1); //Remove the trailing space
            return result;
        }

        public static string CombineParams (string [] commandParams, int pos, int endPos)
        {
            string result = string.Empty;
            for (int i = pos; i < endPos; i++) {
                result += commandParams [i] + " ";
            }

            return result.Substring (0, result.Length - 1);
        }

        public static string BasePathCombine (string p)
        {
            if (p == "")
                return AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine (AppDomain.CurrentDomain.BaseDirectory, p);
        }

        public static void GetReaderLock (ReaderWriterLockSlim l)
        {
            int i = 0;
            while (i < 10) //Only try 10 times... 10s is way too much
            {
                try {
                    l.TryEnterReadLock (100);
                    //Got it
                    return;
                } catch (ApplicationException) {
                    // The reader lock request timed out. Try again
                }
            }
            throw new ApplicationException ("Could not retrieve read lock");
        }

        public static void ReleaseReaderLock (ReaderWriterLockSlim l)
        {
            l.ExitReadLock ();
        }

        public static void GetWriterLock (ReaderWriterLockSlim l)
        {
            int i = 0;
            while (i < 10) //Only try 10 times... 10s is way too much
            {
                try {
                    l.TryEnterWriteLock (100);
                    //Got it
                    return;
                } catch (ApplicationException) {
                    // The reader lock request timed out. Try again
                }
            }
            throw new ApplicationException ("Could not retrieve write lock");
        }

        public static void ReleaseWriterLock (ReaderWriterLockSlim l)
        {
            l.ExitWriteLock ();
        }

        public static sbyte CheckMeshType (sbyte p)
        {
            if (p == (sbyte)AssetType.Mesh)
                return (sbyte)AssetType.Texture;
            return p;
        }

        public static bool IsInstanceOfGenericType (Type genericType, object instance)
        {
            Type type = instance.GetType ();
            return IsInstanceOfGenericType (genericType, type);
        }

        public static bool IsInstanceOfGenericType (Type genericType, Type type)
        {
            while (type != null) {
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition () == genericType) {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }

        // http://social.msdn.microsoft.com/forums/en-US/csharpgeneral/thread/68f7ca38-5cd1-411f-b8d4-e4f7a688bc03
        // By: A Million Lemmings
        public static string ConvertDecString (int dvalue)
        {
            string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            string retVal = string.Empty;

            double value = Convert.ToDouble (dvalue);

            do {
                double remainder = value - (26 * Math.Truncate (value / 26));

                retVal = retVal + CHARS.Substring ((int)remainder, 1);

                value = Math.Truncate (value / 26);
            } while (value > 0);


            return retVal;
        }
    }

    public static class StringUtils
    {
        public static string MySqlEscape (this string usString)
        {
            return MySqlEscape (usString, 0);
        }

        /// <summary>
        ///     Because Escaping the sql might cause it to go over the max length
        ///     DO NOT USE THIS ON JSON STRINGS!!! IT WILL BREAK THE DESERIALIZATION!!!
        /// </summary>
        /// <param name="usString"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string MySqlEscape (this string usString, int maxLength)
        {
            if (usString == null) {
                return null;
            }
            // SQL Encoding for MySQL Recommended here:
            // http://au.php.net/manual/en/function.mysql-real-escape-string.php
            // it escapes \r, \n, \x00, \x1a, baskslash, single quotes, and double quotes
            string returnvalue = Regex.Replace (usString, @"[\r\n\x00\x1a\\'""]", @"\$0");
            if ((maxLength != 0) && (returnvalue.Length > maxLength))
                returnvalue = returnvalue.Substring (0, maxLength);
            return returnvalue;
        }

        /// From http://www.c-sharpcorner.com/UploadFile/mahesh/RandomNumber11232005010428AM/RandomNumber.aspx
        /// <summary>
        ///     Generates a random string with the given length
        /// </summary>
        /// <param name="size">Size of the string</param>
        /// <param name="lowerCase">If true, generate lowercase string</param>
        /// <returns>Random string</returns>
        public static string RandomString (int size, bool lowerCase)
        {
            string builder = "t";
            int off = lowerCase ? 'a' : 'A';
            int j;
            for (int i = 0; i < size; i++) {
                j = Util.RandomClass.Next (25);
                builder += (char)(j + off);
            }

            return builder;
        }

        public static string [] AlphanumericSort (List<string> list)
        {
            string [] nList = list.ToArray ();
            Array.Sort (nList, new AlphanumComparatorFast ());
            return nList;
        }

        public class AlphanumComparatorFast : IComparer
        {
            public int Compare (object x, object y)
            {
                string s1 = x as string;
                if (s1 == null)
                    return 0;

                string s2 = y as string;
                if (s2 == null)
                    return 0;


                int len1 = s1.Length;
                int len2 = s2.Length;
                int marker1 = 0;
                int marker2 = 0;

                // Walk through two the strings with two markers.
                while (marker1 < len1 && marker2 < len2) {
                    char ch1 = s1 [marker1];
                    char ch2 = s2 [marker2];

                    // Some buffers we can build up characters in for each chunk.
                    char [] space1 = new char [len1];
                    int loc1 = 0;
                    char [] space2 = new char [len2];
                    int loc2 = 0;

                    // Walk through all following characters that are digits or
                    // characters in BOTH strings starting at the appropriate marker.
                    // Collect char arrays.
                    do {
                        space1 [loc1++] = ch1;
                        marker1++;

                        if (marker1 < len1) {
                            ch1 = s1 [marker1];
                        } else {
                            break;
                        }
                    } while (char.IsDigit (ch1) == char.IsDigit (space1 [0]));

                    do {
                        space2 [loc2++] = ch2;
                        marker2++;

                        if (marker2 < len2) {
                            ch2 = s2 [marker2];
                        } else {
                            break;
                        }
                    } while (char.IsDigit (ch2) == char.IsDigit (space2 [0]));

                    // If we have collected numbers, compare them numerically.
                    // Otherwise, if we have strings, compare them alphabetically.
                    string str1 = new string (space1);
                    string str2 = new string (space2);

                    int result;

                    if (char.IsDigit (space1 [0]) && char.IsDigit (space2 [0])) {
                        int thisNumericChunk = int.Parse (str1);
                        int thatNumericChunk = int.Parse (str2);
                        result = thisNumericChunk.CompareTo (thatNumericChunk);
                    } else {
                        result = string.Compare (str1, str2, StringComparison.Ordinal);
                    }

                    if (result != 0) {
                        return result;
                    }
                }
                return len1 - len2;
            }
        }

        public static List<string> SizeSort (List<string> functionKeys, bool smallestToLargest)
        {
            functionKeys.Sort ((a, b) => { return a.Length.CompareTo (b.Length); });
            if (!smallestToLargest)
                functionKeys.Reverse (); //Flip the order then
            return functionKeys;
        }
    }

    public static class NetworkUtils
    {
        static bool m_noInternetConnection;
        static int m_nextInternetConnectionCheck;
        //static bool useLocalhostLoopback=false;
        static readonly ExpiringCache<string, IPAddress> m_dnsCache = new ExpiringCache<string, IPAddress> ();

        public static IPEndPoint ResolveEndPoint (string hostName, int port)
        {
            IPEndPoint endpoint = null;
            // Old one defaults to IPv6
            //return new IPEndPoint(Dns.GetHostAddresses(m_externalHostName)[0], m_internalEndPoint.Port);

            IPAddress ia = null;
            // If it is already an IP, don't resolve it - just return directly
            if (IPAddress.TryParse (hostName, out ia)) {
                endpoint = new IPEndPoint (ia, port);
                return endpoint;
            }

            try {
                if (IPAddress.TryParse (hostName.Split (':') [0], out ia)) {
                    endpoint = new IPEndPoint (ia, port);
                    return endpoint;
                }
            } catch {
            }
            // Reset for next check
            ia = null;
            try {
                if (CheckInternetConnection ()) {
                    foreach (IPAddress Adr in Dns.GetHostAddresses (hostName)) {
                        if (ia == null)
                            ia = Adr;

                        if (Adr.AddressFamily == AddressFamily.InterNetwork) {
                            ia = Adr;
                            InternetSuccess ();
                            break;
                        }
                    }
                }
            } catch (SocketException e) {
                InternetFailure ();
                throw new Exception (
                    "Unable to resolve local hostname " + hostName + " innerException of type '" +
                    e + "' attached to this exception", e);
            }
            if (ia != null)
                endpoint = new IPEndPoint (ia, port);
            return endpoint;
        }

        public static bool CheckInternetConnection ()
        {
            if (m_noInternetConnection) {
                if (Util.EnvironmentTickCount () > m_nextInternetConnectionCheck)
                    return true; //Try again

                return false;
            }
            return true; //No issues
        }

        public static void InternetSuccess ()
        {
            m_noInternetConnection = false;
        }

        public static void InternetFailure ()
        {
            m_nextInternetConnectionCheck = Util.EnvironmentTickCountAdd (5 * 60 * 1000); /*5 mins*/
            m_noInternetConnection = true;
        }

        /// <summary>
        ///     Gets the client IP address
        /// </summary>
        /// <param name="xff"></param>
        /// <returns></returns>
        public static IPEndPoint GetClientIPFromXFF (string xff)
        {
            if (xff == string.Empty)
                return null;

            string [] parts = xff.Split (new [] { ',' });
            if (parts.Length > 0) {
                try {
                    return new IPEndPoint (IPAddress.Parse (parts [0]), 0);
                } catch (Exception e) {
                    MainConsole.Instance.WarnFormat ("[Util]: Exception parsing XFF header {0}: {1}", xff, e.Message);
                }
            }

            return null;
        }

        public static string GetCallerIP (Hashtable req)
        {
            if (req.ContainsKey ("headers")) {
                try {
                    Hashtable headers = (Hashtable)req ["headers"];
                    if (headers.ContainsKey ("remote_addr") && headers ["remote_addr"] != null)
                        return headers ["remote_addr"].ToString ();
                    if (headers.ContainsKey ("Host") && headers ["Host"] != null)
                        return headers ["Host"].ToString ().Split (':') [0];
                } catch (Exception e) {
                    MainConsole.Instance.WarnFormat ("[Util]: exception in GetCallerIP: {0}", e.Message);
                }
            }
            return string.Empty;
        }

        /// <summary>
        ///     Attempts to resolve the loopback issue, but only works if this is run on the same network as the iPAddress
        /// </summary>
        /// <param name="iPAddress"></param>
        /// <param name="clientIP"></param>
        /// <returns></returns>
        public static IPAddress ResolveAddressForClient (IPAddress iPAddress, IPEndPoint clientIP)
        {
            /*if (iPAddress == null)
                return clientIP.Address;
            if (iPAddress.Equals(clientIP.Address))
            {
                if (useLocalhostLoopback)
                    return IPAddress.Loopback;
                if (iPAddress == IPAddress.Loopback)
                    return iPAddress; //Don't send something else if it is already on loopback
                if (CheckInternetConnection())
                {
#pragma warning disable 618
                    //The 'bad' way, only works for things on the same machine...
                    try
                    {
                        string hostName = Dns.GetHostName();
                        IPHostEntry ipEntry = Dns.GetHostByName(hostName);
#pragma warning restore 618
                        IPAddress[] addr = ipEntry.AddressList;
                        return addr[0]; //Loopback around! They are on the same connection
                    }
                    catch
                    {
                        InternetFailure(); //Something went wrong
                    }
                }
            }
            return iPAddress;*/
            return iPAddress;
        }

        public static bool IsLanIP (IPAddress address)
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces ();
            foreach (var iface in interfaces) {
                var properties = iface.GetIPProperties ();
                foreach (var ifAddr in properties.UnicastAddresses) {
                    if (ifAddr.IPv4Mask != null &&
                        ifAddr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        CheckMask (ifAddr.Address, ifAddr.IPv4Mask, address))
                        return true;
                }
            }
            return false;
        }

        static bool CheckMask (IPAddress address, IPAddress mask, IPAddress target)
        {
            if (mask == null)
                return false;

            var ba = address.GetAddressBytes ();
            var bm = mask.GetAddressBytes ();
            var bb = target.GetAddressBytes ();

            if (ba.Length != bm.Length || bm.Length != bb.Length)
                return false;

            for (var i = 0; i < ba.Length; i++) {
                int m = bm [i];

                int a = ba [i] & m;
                int b = bb [i] & m;

                if (a != b)
                    return false;
            }

            return true;
        }

        public static IPEndPoint ResolveAddressForClient (IPEndPoint iPAddress, IPEndPoint clientIP)
        {
            iPAddress.Address = ResolveAddressForClient (iPAddress.Address, clientIP);
            return iPAddress;
        }

        /// <summary>
        ///     Returns a IP address from a specified DNS, favouring IPv4 addresses.
        /// </summary>
        /// <param name="dnsAddress">DNS Hostname</param>
        /// <returns>An IP address, or null</returns>
        public static IPAddress GetHostFromDNS (string dnsAddress)
        {
            dnsAddress = dnsAddress.Replace ("http://", "").Replace ("https://", "");
            if (dnsAddress.EndsWith ("/", StringComparison.Ordinal))
                dnsAddress = dnsAddress.Remove (dnsAddress.Length - 1);
            if (dnsAddress.Contains (":"))
                dnsAddress = dnsAddress.Split (':') [0];
            IPAddress ipa;
            if (m_dnsCache.TryGetValue (dnsAddress, out ipa))
                return ipa;
            // Is it already a valid IP? No need to look it up.
            if (IPAddress.TryParse (dnsAddress, out ipa)) {
                m_dnsCache.Add (dnsAddress, ipa, 30 * 60 /*30mins*/);
                return ipa;
            }
            try {
                if (IPAddress.TryParse (dnsAddress.Split (':') [0], out ipa)) {
                    m_dnsCache.Add (dnsAddress, ipa, 30 * 60 /*30mins*/);
                    return ipa;
                }
            } catch {
            }
            IPAddress [] hosts = null;

            // Not an IP, lookup required
            try {
                if (CheckInternetConnection ()) {
                    hosts = Dns.GetHostEntry (dnsAddress).AddressList;
                    if (hosts != null)
                        InternetSuccess ();
                    else
                        InternetFailure ();
                }
            } catch (Exception e) {
                MainConsole.Instance.WarnFormat ("[Util]: An error occurred while resolving host name {0}, {1}",
                                                dnsAddress, e.ToString ());

                InternetFailure ();
                // Still going to throw the exception on for now, since this was what was happening in the first place
                throw e;
            }

            if (hosts != null) {
                foreach (IPAddress host in hosts.Where (host => host.AddressFamily == AddressFamily.InterNetwork)) {
                    m_dnsCache.Add (dnsAddress, host, 30 * 60 /*30mins*/);
                    return host;
                }

                if (hosts.Length > 0) {
                    m_dnsCache.Add (dnsAddress, hosts [0], 30 * 60 /*30mins*/);
                    return hosts [0];
                }
            }

            return null;
        }

        public static Uri GetURI (string protocol, string hostname, int port, string path)
        {
            return new UriBuilder (protocol, hostname, port, path).Uri;
        }

        /// <summary>
        ///     Gets a list of all local system IP addresses
        /// </summary>
        /// <returns></returns>
        public static IPAddress [] GetLocalHosts ()
        {
            return Dns.GetHostAddresses (Dns.GetHostName ());
        }

        public static IPAddress GetLocalHost ()
        {
            IPAddress [] iplist = GetLocalHosts ();

            if (iplist.Length == 0) // No accessible external interfaces
            {
                if (CheckInternetConnection ()) {
                    try {
                        IPAddress [] loopback = Dns.GetHostAddresses ("localhost");
                        IPAddress localhost = loopback [0];

                        InternetSuccess ();
                        return localhost;
                    } catch {
                        InternetFailure ();
                    }
                }
            }

            foreach (
                IPAddress host in
                    iplist.Where (host => !IPAddress.IsLoopback (host) && host.AddressFamily == AddressFamily.InterNetwork)
                ) {
                return host;
            }

            if (iplist.Length > 0) {
                foreach (IPAddress host in iplist.Where (host => host.AddressFamily == AddressFamily.InterNetwork)) {
                    return host;
                }

                // Well all else failed...
                return iplist [0];
            }

            return null;
        }

        #region Nested type: IPAddressRange

        public class IPAddressRange
        {
            readonly AddressFamily addressFamily;
            readonly byte [] lowerBytes;
            readonly byte [] upperBytes;

            public IPAddressRange (IPAddress lower, IPAddress upper)
            {
                // Assert that lower.AddressFamily == upper.AddressFamily

                addressFamily = lower.AddressFamily;
                lowerBytes = lower.GetAddressBytes ();
                upperBytes = upper.GetAddressBytes ();
            }

            public bool IsInRange (IPAddress address)
            {
                if (address.AddressFamily != addressFamily) {
                    return false;
                }

                byte [] addressBytes = address.GetAddressBytes ();

                bool lowerBoundary = true, upperBoundary = true;

                for (int i = 0;
                     i < lowerBytes.Length &&
                     (lowerBoundary || upperBoundary);
                     i++) {
                    if ((lowerBoundary && addressBytes [i] < lowerBytes [i]) ||
                        (upperBoundary && addressBytes [i] > upperBytes [i])) {
                        return false;
                    }

                    lowerBoundary &= (addressBytes [i] == lowerBytes [i]);
                    upperBoundary &= (addressBytes [i] == upperBytes [i]);
                }

                return true;
            }
        }

        #endregion
    }

    public static class Extensions
    {
        public static List<T> ConvertAll<T> (this OSDArray array, Converter<OSD, T> converter)
        {
            List<OSD> list = new List<OSD> ();
            foreach (OSD o in array) {
                list.Add (o);
            }
            return list.ConvertAll<T> (converter);
        }

        public static Dictionary<string, T> ConvertMap<T> (this OSDMap array, Converter<OSD, T> converter)
        {
            Dictionary<string, T> map = new Dictionary<string, T> ();
            foreach (KeyValuePair<string, OSD> o in array) {
                map.Add (o.Key, converter (o.Value));
            }
            return map;
        }

        public static OSDArray ToOSDArray<T> (this List<T> array)
        {
            OSDArray list = new OSDArray ();
            foreach (object o in array) {
                OSD osd = Util.MakeOSD (o, o.GetType ());
                if (osd != null)
                    list.Add (osd);
            }
            return list;
        }

        public static OSDMap ToOSDMap<A, B> (this Dictionary<A, B> array)
        {
            OSDMap list = new OSDMap ();
            foreach (KeyValuePair<A, B> o in array) {
                OSD osd = Util.MakeOSD (o.Value, o.Value.GetType ());
                if (osd != null)
                    list.Add (o.Key.ToString (), osd);
            }
            return list;
        }

        /// <summary>
        ///     Comes from http://www.codeproject.com/script/Articles/ViewDownloads.aspx?aid=14593
        /// </summary>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <param name="invokeClass"></param>
        /// <param name="invokeParameters"></param>
        /// <returns></returns>
        public static object FastInvoke (this MethodInfo method, ParameterInfo [] parameters, object invokeClass,
                                        object [] invokeParameters)
        {
            DynamicMethod dynamicMethod = new DynamicMethod (string.Empty,
                                                            typeof (object), new Type []
                                                                                 {
                                                                                     typeof (object),
                                                                                     typeof (object[])
                                                                                 },
                                                            method.DeclaringType.Module);
            ILGenerator il = dynamicMethod.GetILGenerator ();
            Type [] paramTypes = new Type [parameters.Length];
            for (int i = 0; i < paramTypes.Length; i++) {
                paramTypes [i] = parameters [i].ParameterType;
            }
            LocalBuilder [] locals = new LocalBuilder [paramTypes.Length];
            for (int i = 0; i < paramTypes.Length; i++) {
                locals [i] = il.DeclareLocal (paramTypes [i]);
            }
            for (int i = 0; i < paramTypes.Length; i++) {
                il.Emit (OpCodes.Ldarg_1);
                EmitFastInt (il, i);
                il.Emit (OpCodes.Ldelem_Ref);
                EmitCastToReference (il, paramTypes [i]);
                il.Emit (OpCodes.Stloc, locals [i]);
            }
            il.Emit (OpCodes.Ldarg_0);
            for (int i = 0; i < paramTypes.Length; i++) {
                il.Emit (OpCodes.Ldloc, locals [i]);
            }
            il.EmitCall (OpCodes.Call, method, null);
            if (method.ReturnType == typeof (void))
                il.Emit (OpCodes.Ldnull);
            else
                EmitBoxIfNeeded (il, method.ReturnType);
            il.Emit (OpCodes.Ret);
            return dynamicMethod.Invoke (null, new object [] { invokeClass, invokeParameters });
            /*FastInvokeHandler invoder =
              (FastInvokeHandler)dynamicMethod.CreateDelegate(
              typeof(FastInvokeHandler));
            return invoder;*/
        }

        static void EmitCastToReference (ILGenerator il, Type type)
        {
            if (type.IsValueType) {
                il.Emit (OpCodes.Unbox_Any, type);
            } else {
                il.Emit (OpCodes.Castclass, type);
            }
        }

        static void EmitBoxIfNeeded (ILGenerator il, Type type)
        {
            if (type.IsValueType) {
                il.Emit (OpCodes.Box, type);
            }
        }

        static void EmitFastInt (ILGenerator il, int value)
        {
            switch (value) {
            case -1:
                il.Emit (OpCodes.Ldc_I4_M1);
                return;
            case 0:
                il.Emit (OpCodes.Ldc_I4_0);
                return;
            case 1:
                il.Emit (OpCodes.Ldc_I4_1);
                return;
            case 2:
                il.Emit (OpCodes.Ldc_I4_2);
                return;
            case 3:
                il.Emit (OpCodes.Ldc_I4_3);
                return;
            case 4:
                il.Emit (OpCodes.Ldc_I4_4);
                return;
            case 5:
                il.Emit (OpCodes.Ldc_I4_5);
                return;
            case 6:
                il.Emit (OpCodes.Ldc_I4_6);
                return;
            case 7:
                il.Emit (OpCodes.Ldc_I4_7);
                return;
            case 8:
                il.Emit (OpCodes.Ldc_I4_8);
                return;
            }

            if (value > -129 && value < 128) {
                il.Emit (OpCodes.Ldc_I4_S, (sbyte)value);
            } else {
                il.Emit (OpCodes.Ldc_I4, value);
            }
        }
    }

    [ProtoContract (UseProtoMembersOnly = true)]
    public class AllScopeIDImpl : IDataTransferable
    {
        [ProtoMember (1)]
        public UUID ScopeID = UUID.Zero;

        [ProtoMember (2)]
        public List<UUID> AllScopeIDs {
            get {
                List<UUID> ids = new List<UUID> ();
                if (!ids.Contains (ScopeID))
                    ids.Add (ScopeID);
                return ids;
            }
            set { }
        }

        public static List<T> CheckScopeIDs<T> (List<UUID> scopeIDs, List<T> list) where T : AllScopeIDImpl
        {
            if (scopeIDs == null || scopeIDs.Count == 0 || scopeIDs.Contains (UUID.Zero))
                return list;
            return
                new List<T> (
                    list.Where (r => scopeIDs.Any (s => r.AllScopeIDs.Contains (s)) || r.AllScopeIDs.Contains (UUID.Zero)));
        }

        public static T CheckScopeIDs<T> (List<UUID> scopeIDs, T l) where T : AllScopeIDImpl
        {
            if (l == null || scopeIDs == null || scopeIDs.Count == 0 || scopeIDs.Contains (UUID.Zero))
                return l;
            return (scopeIDs.Any (s => l.AllScopeIDs.Contains (s)) || l.AllScopeIDs.Contains (UUID.Zero)) ? l : null;
        }
    }
}
