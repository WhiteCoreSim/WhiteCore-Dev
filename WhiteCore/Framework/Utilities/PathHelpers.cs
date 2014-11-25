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
using System.Text.RegularExpressions;
using System.IO;
using WhiteCore.Framework.ConsoleFramework;
using System.Collections.Generic;

namespace WhiteCore.Framework.Utilities
{
    public class PathHelpers
    {
        const string usernameVar = "%username%";
        const string HomedriveVar = "%homedrive%";

        public static string PathUsername(string Path) //supports using %username% in place of username
        {
            if (Path.IndexOf(usernameVar, StringComparison.CurrentCultureIgnoreCase) == -1)
                //does not contain username var
            {
                return Path;
            }
            else //contains username var
            {
                string userName = Environment.UserName; //check system for current username
                return Regex.Replace(Path, usernameVar, userName, RegexOptions.IgnoreCase);
                    //return Path with the system username
            }
        }


        public static string PathHomeDrive(string Path) //supports for %homedrive%, gives the drive letter on Windows
        {
            if (Path.IndexOf(HomedriveVar, StringComparison.CurrentCultureIgnoreCase) == -1)
                //does not contain username var
            {
                return Path;
            }
            else
            {
                if (Util.IsLinux)
                {
                    return Path;
                }
                else
                {
                    string DriveLetter = Environment.GetEnvironmentVariable("HOMEDRIVE");

                    return Regex.Replace(Path, HomedriveVar, DriveLetter, RegexOptions.IgnoreCase);
                }
            }
        }

        public static string ComputeFullPath(string Path)
            //single function that calls the functions that help compute a full url Path
        {
            return PathHomeDrive(PathUsername(Path));
        }

        /// <summary>
        /// Verifies a file for writeing setting some defaults if needed.
        /// </summary>
        /// <returns>The write file.</returns>
        /// <param name="fileName">File name.</param>
        /// <param name="defaultExt">Default ext.</param>
        /// <param name="defaultDir">Default dir.</param>
        /// <param name="createPath">If set to <c>true</c> create path.</param>
        public static string VerifyWriteFile(string fileName, string defaultExt, string defaultDir, bool createPath)
        {
            // some file sanity checks when saving 
            string extension = Path.GetExtension (fileName);
            if (!defaultExt.StartsWith ("."))
                defaultExt = "." + defaultExt;

            if (extension == string.Empty)
            {
                fileName = fileName + defaultExt;
            }

            // verify path details
            string filePath = Path.GetDirectoryName (fileName);
            if (filePath == "")
            {
                if (defaultDir == String.Empty)
                    defaultDir = "./";
                fileName = Path.Combine (defaultDir, fileName);

            }
             
            // check if the directory exists
            if (!Directory.Exists (defaultDir))
            {
                if (createPath)
                    Directory.CreateDirectory (defaultDir);
                else
                {
                    MainConsole.Instance.Info ("[Error]: The folder specified, '" + defaultDir + "' does not exist!");
                    return "";
                }
            }
 
            // last check...
            if (File.Exists (fileName))
            {
                if (MainConsole.Instance.Prompt ("[Warning]: The file '" + fileName + "' exists. Overwrite?", "yes") != "yes")
                    return "";

                File.Delete (fileName);
            }

            return fileName;
        }

        /// <summary>
        /// Verifies file for reading, setting some defaults if needed.
        /// </summary>
        /// <returns>The read file.</returns>
        /// <param name="fileName">File name.</param>
        /// <param name="extensions">Extensions.</param>
        /// <param name="defaultDir">Default dir.</param>
        public static string VerifyReadFile(string fileName, List <string> extensions, string defaultDir)
        {
            foreach (var ext in extensions)
            {

                var fName = VerifyReadFile(fileName, ext, defaultDir, false);
                if ( fName != "")
                    return fName;
            }

            MainConsole.Instance.Info("[Error]: The file '" + fileName + "' cannot be found." );
            return "";
        }

        /// <summary>
        /// Verifies file for reading, setting some defaults if needed.
        /// </summary>
        /// <returns>The read file.</returns>
        /// <param name="fileName">File name.</param>
        /// <param name="defaultExt">Default ext.</param>
        /// <param name="defaultDir">Default dir.</param>
        public static string VerifyReadFile(string fileName, string defaultExt, string defaultDir)
        {
            return VerifyReadFile (fileName, defaultExt, defaultDir, true);
        }

        /// <summary>
        /// Verifies file for reading, setting some defaults if needed.
        /// </summary>
        /// <returns>The read file.</returns>
        /// <param name="fileName">File name.</param>
        /// <param name="defaultExt">Default ext.</param>
        /// <param name="defaultDir">Default dir.</param>
        /// <param name="showErrors">Show error messages.</param>
        public static string VerifyReadFile(string fileName, string defaultExt, string defaultDir, bool showErrors)
        {
            // some sanity checks...
            string extension = Path.GetExtension(fileName).ToLower();
            if (!defaultExt.StartsWith ("."))
                defaultExt = "." + defaultExt;
            bool extOK = extension.Equals(defaultExt);

            if (!extOK)
            {
                if ( extension == string.Empty)
                {
                    fileName = fileName + defaultExt;
                } else 
                {
                    if (showErrors)
                        MainConsole.Instance.Info("Usage: the filename should be a '" + defaultExt +"' file");
                    return "";
                }
            }

            string filePath = Path.GetDirectoryName (fileName);
            if (filePath == "")
            {
                if (defaultDir == String.Empty)
                    defaultDir = "./";

                if (!Directory.Exists (defaultDir))
                {
                    if (showErrors)
                        MainConsole.Instance.Info ("[Error]: The folder specified, '" + defaultDir + "' does not exist!");
                    return "";
                }
                fileName = Path.Combine (defaultDir, fileName);
            }
             
            // last check...
            if ( !File.Exists( fileName ) )
            {
                if (showErrors)
                    MainConsole.Instance.Info ( "[Error]: The file '" + fileName + "' cannot be found." );
                return "";
            }

            return fileName;

        }

    }
}