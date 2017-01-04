/*
 * Copyright (c) http://whitecore-sim.org/
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

using System.Collections.Generic;
using System.IO;

namespace WhiteCore.Modules.Web.Translators
{
    public static class TranslatorSerialization
    {
        /// <summary>
        /// Deserialize the specified <languageName>.po file
        /// </summary>
        /// <param name="basePath">Base path.</param>
        /// <param name="languageName">Language name.</param>
        public static Dictionary<string, string> Deserialize (string basePath, string languageName)
        {
            var newdict = new Dictionary<string, string> ();
            var outPath = Path.Combine (basePath, "translations");
            var fileName = Path.Combine (outPath, languageName + ".po");

            if (!File.Exists (fileName))
                return newdict;             // no translation available

            string inputline;
            char [] delim = { ' ' };

            using (StreamReader reader = new StreamReader (fileName)) {
                string key = "";

                while ((inputline = reader.ReadLine ()) != null) {
                    var bits = inputline.Split (delim, 2);
                    if (bits.Length == 2) {
                        if (bits [0] == "msgid")
                            key = bits [1].Replace ("\"", "");
                        if (key != "" & bits [0] == "msgstr") {
                            newdict.Add (key, bits [1].Replace ("\"", ""));
                            key = "";
                        }
                    }
                }
            }
            return newdict;
        }
    }
}
