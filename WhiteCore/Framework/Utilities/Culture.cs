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
using System.Globalization;
using System.Threading;

namespace WhiteCore.Framework.Utilities
{
    public class Culture
    {
        static readonly CultureInfo m_cultureInfo = new CultureInfo("en-US", false);

        public static NumberFormatInfo NumberFormatInfo
        {
            get { return m_cultureInfo.NumberFormat; }
        }

        public static IFormatProvider FormatProvider
        {
            get { return m_cultureInfo; }
        }

        /// <summary>
        ///     Set Culture to en-US to make string processing of numbers simpler.
        /// </summary>
        public static void SetCurrentCulture()
        {
            Thread.CurrentThread.CurrentCulture = m_cultureInfo;
        }

        /// <summary>
        /// The base system culture info before it is locked to "en_US".
        ///   Used for log date/time formatting
        /// </summary>
        public static CultureInfo SystemCultureInfo
        { get; set; }

        /// <summary>
        /// Returns a formatted date string depending upon the system Locale.
        /// </summary>
        /// <returns>Local date string.</returns>
        public static string LocaleDate(DateTime userDateTime)
        {
            return LocaleDate (userDateTime, null);
        }

        /// <summary>
        /// Returns a formatted date string depending upon the system Locale.
        /// </summary>
        /// <returns>The localized date.</returns>
        /// <param name="userDateTime">User date time.</param>
        /// <param name="dtFormat">DateTime format if required.</param>
        public static string LocaleDate(DateTime userDateTime, string dtFormat )
        {
            const string defFormat = "MMM dd, yyyy";
            //string dt = Culture.SystemCultureInfo.DateTimeFormat.ShortDatePattern;
            //string dt = DateTime.Now.ToString (df);
            if (dtFormat == null)
                dtFormat = defFormat;

            string dt;
            if (userDateTime > DateTime.MinValue)
                dt = userDateTime.ToString (dtFormat,SystemCultureInfo);
            else
                dt = DateTime.Now.ToString (dtFormat,SystemCultureInfo);
            return dt;
        }

        /// <summary>
        /// Returns a formatted date time string depending upon the system Locale.
        /// </summary>
        /// <returns>Local time and date string.</returns>
        public static string LocaleTimeDate()
        {
            //string dt = Culture.SystemCultureInfo.DateTimeFormat.ShortDatePattern;
            string dt = DateTime.Now.ToString ("hh:mm:ss MMM dd",SystemCultureInfo);
            return dt;
        }

        public static string LocaleShortDateTime (DateTime userDateTime)
        {
            const string defFormat = "MMM dd, hh:mm tt";
            string ts = userDateTime.ToString (defFormat, SystemCultureInfo);
            return ts;
        }

        /// <summary>
        /// Returns a formatted date time string depending upon the system Locale.
        /// Used for logging
        /// </summary>
        /// <returns>Local date time string.</returns>
        public static string LocaleLogStamp()
        {
            string ts = DateTime.Now.ToString ("MMM dd hh:mm:ss",SystemCultureInfo);
            return ts;
        }

    }
}
