/*
 * Copyright (c) Contributors, http://whitecore-sim.org/
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
using System.IO;
using System.Linq;

namespace WhiteCore.Modules.Web.Translators
{
    public static class TranslatorSerialization
    {
        static void WriteHeader (StreamWriter outFile, string fullLanguageName, string languageName)
        {
            var header = "#\n# " + fullLanguageName + " translation for WhiteCore.\n" +
                "# This file is distributed under the same license as the WhiteCore package.\n" +
                "# greythane <greythane@gmail.com>, 2016.\n#";
            outFile.WriteLine (header);
            outFile.WriteLine ("");
            outFile.WriteLine ("\nmsgid \"\"\nmsgstr \"\"");
            outFile.WriteLine ("");

            var fileinfo = "\"Project-Id-Version: WhiteCore Version 0.9.5\\n\"\n" +
                "\"Report-Msgid-Bugs-To: greythane <greythane@gmail.com>\\n\"\n";
            outFile.WriteLine (fileinfo);

            var timenow = DateTime.Now;
            var filestamp = "\"POT-Creation-Date: " + timenow.ToString ("u") + "\\n\"\n" +
                "\"PO-Revision-Date: " + timenow.ToString ("u") + "\\n\"";
            outFile.WriteLine (filestamp);

            var xtrainfo = "\"Last-Translator: greythane <greythane@gmail.com>\\n\"\n" +
                "\"Language-Team: WhiteCore Development <greythane@gmail.com>\\n\"\n" +
                "\"MIME-Version: 1.0\\n\"\n" +
                "\"Content-Type: text/plain; charset=UTF-8\\n\"\n" +
                "\"Content-Transfer-Encoding: 8bit\\n\"\n" +
                "\"Language: " + languageName + "\\n\"\n" +
                "\"Plural-Forms: nplurals=2; plural=(n != 1);\\n\"\n" +
                "\"X-Generator: WhiteCore V0.9.5 Dev\\n\"\n";
            outFile.WriteLine (xtrainfo);
        }

        /// <summary>
        /// Serialize the specified translation dictionary into a poEdit format file.
        /// </summary>
        /// <param name="basePath">Base path to save file.</param>
        /// <param name="fullLanguageName">Full language name.</param>
        /// <param name="languageName">Language name.</param>
        /// <param name="dictionary">Dictionary.</param>
        public static void Serialize (string basePath, string fullLanguageName, string languageName, Dictionary<string, string> dictionary)
        {
            var outPath = Path.Combine (basePath, "translations");
            if (!Directory.Exists (outPath))
                Directory.CreateDirectory (outPath);

            var outFile = Path.Combine (outPath, languageName + ".po");
            var fileout = new StreamWriter (outFile);

            var wordList = dictionary.Keys.ToList ();
            wordList.Sort ();

            try {
                WriteHeader (fileout, fullLanguageName, languageName);

                foreach (var element in wordList) {
                    fileout.Write ("msgid \"");
                    fileout.WriteLine (element + "\"");
                    fileout.Write ("msgstr \"");
                    fileout.WriteLine (dictionary [element] + "\"");
                    fileout.WriteLine ("");
                }
            } finally {
                fileout.Close ();
            }

            return;
        }

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
