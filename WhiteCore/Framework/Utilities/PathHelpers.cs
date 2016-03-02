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
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using WhiteCore.Framework.ConsoleFramework;

namespace WhiteCore.Framework.Utilities
{
    public class PathHelpers
    {
        const string usernameVar = "%username%";
        const string winHomedriveVar = "%homedrive%";           // should the be  "%HOMEDRIVE%%HOMEPATH%" ?
        const string nixHomeVar = "~/";

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


        public static string PathHomeDrive(string fName) 
        {
            // supports for %homedrive%, gives the drive letter on Windows
            // ~/ on *nix
             
            if ( fName.IndexOf (winHomedriveVar, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                string DriveLetter = Environment.GetEnvironmentVariable ("HOMEDRIVE");
                fName =  Regex.Replace (fName, winHomedriveVar, DriveLetter, RegexOptions.IgnoreCase);
            }
    
            // *nix then ?
            if ( fName.IndexOf (nixHomeVar, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                string homePath = Environment.GetEnvironmentVariable("HOME") + "/";
                fName = Regex.Replace (fName, nixHomeVar, homePath, RegexOptions.IgnoreCase);
            }

            return fName;        
        }

        public static string ComputeFullPath(string Path)
            //single function that calls the functions that help compute a full url Path
        {
            return PathHomeDrive(PathUsername(Path));
        }

        /// <summary>
        /// Verifies and corrects the OS path.
        /// </summary>
        /// <returns>The OS path.</returns>
        /// <param name="filename">Filename.</param>
        public static string VerifyOSPath(string filename)
        {
            return Utilities.IsLinuxOs ? filename.Replace ('\\', '/') : filename.Replace ('/', '\\');
        }

        /// <summary>
        /// Verifies a file for writing setting some defaults if needed.
        /// </summary>
        /// <returns>The write file.</returns>
        /// <param name="fileName">File name.</param>
        /// <param name="defaultExt">Default ext.</param>
        /// <param name="defaultDir">Default dir.</param>
        /// <param name="createPath">If set to <c>true</c> create path.</param>
        public static string VerifyWriteFile(string fileName, string defaultExt, string defaultDir, bool createPath)
        {
            // some file sanity checks when saving 
            if (fileName == "")
                return "";
            
            string extension = Path.GetExtension (fileName);
            if (!defaultExt.StartsWith ("."))
                defaultExt = "." + defaultExt;

            if (extension == string.Empty)
            {
                fileName = fileName + defaultExt;
            }

            // check for user directories
            fileName = ComputeFullPath(fileName);

            string filePath = Path.GetDirectoryName (fileName);
            if (filePath == "")
            {
                if (defaultDir == String.Empty)
                    defaultDir = "./";

                fileName = VerifyOSPath(Path.Combine (defaultDir, fileName));

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
            if (fileName == "")
                return "";
            
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

            // check for user directories
            fileName = ComputeFullPath(fileName);

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

                fileName = VerifyOSPath(Path.Combine (defaultDir, fileName));
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

        public static List<string> GetFilenames(string defaultDir, string extension, bool showextension)
        {
            var retVals = new List<string>();

            if (string.IsNullOrEmpty(extension))
                extension = ".*";
            if (!extension.StartsWith ("."))
                extension = "." + extension;
            
            if (defaultDir == String.Empty)
                defaultDir = "./";
                       
            if (Directory.Exists (defaultDir))
            {
                var archives = new List<string> (Directory.GetFiles (defaultDir, "*" + extension));
                foreach (string file in archives)
                    if (showextension)
                        retVals.Add (Path.GetFileName (file));
                    else
                        retVals.Add (Path.GetFileNameWithoutExtension (file));
            }

            return retVals;
        }

        /// <summary>
        /// Gets file for reading and verifies that it exists.
        /// </summary>
        /// <returns>The read filename.</returns>
        /// <param name="prompt">Prompt.</param>
        /// <param name="defaultDir">Default dir.</param>
        /// <param name="extensions">Extensions.</param>
        /// <param name="showextension">If set to <c>true</c> showextension.</param>
        public static string GetReadFilename(string prompt, string defaultDir, List <string> extensions, bool showextension)
        {
            // get a file and verify that it exists etc
            string loadFileName = "";
            do
            {
                loadFileName = MainConsole.Instance.Prompt(prompt + " (? for list) ", loadFileName);
                if (loadFileName == "")
                    return "";

                if (loadFileName == "?")
                {
                    var availfiles = new List<string>();
                    foreach (var ext in extensions)
                    {
                        var fnames = GetFilenames(defaultDir, ext, showextension);
                        if ( fnames.Count > 0)
                            availfiles.AddRange(fnames);
                    }

                    if (availfiles.Count > 0)
                    {
                        MainConsole.Instance.CleanInfo (" Available files are : ");
                        foreach (string file in availfiles)
                            MainConsole.Instance.CleanInfo ("   " + file);
                    } else
                        MainConsole.Instance.CleanInfo ("Sorry, no files are available.");

                    loadFileName = "";    
                }
            } while (loadFileName == "");

            // check for full pathing
            var fileName = VerifyOSPath(Path.Combine (defaultDir, loadFileName));
            if ( !File.Exists( fileName ) )
            {
                MainConsole.Instance.Info ( "[Error]: The file '" + loadFileName + "' cannot be found." );
                return "";
            }

            return fileName;
        }

        /// <summary>
        /// Gets file for reading and verifies that it exists.
        /// </summary>
        /// <returns>The read filename.</returns>
        /// <param name="prompt">Prompt.</param>
        /// <param name="defaultDir">Defaultdir.</param>
        /// <param name="extension">Extension.</param>
        public static string GetReadFilename(string prompt, string defaultDir, string extension, bool showextension)
        {
            // get a file and verify that it exists etc
            string loadFileName = "";
            do
            {
                loadFileName = MainConsole.Instance.Prompt(prompt + " (? for list) ", loadFileName);
                if (loadFileName == "")
                    return "";

                if (loadFileName == "?")
                {
                    var availfiles = GetFilenames(defaultDir, extension, showextension);
                    if (availfiles.Count > 0)
                    {
                        MainConsole.Instance.CleanInfo (" Available files are : ");
                        foreach (string file in availfiles)
                            MainConsole.Instance.CleanInfo ("   " + file);
                    } else
                        MainConsole.Instance.CleanInfo ("Sorry, no files are available.");

                    loadFileName = "";    
                }
            } while (loadFileName == "");

            // check for full pathing
            var fileName = VerifyOSPath(Path.Combine (defaultDir, loadFileName));
            if ( !File.Exists( fileName ) )
            {
                MainConsole.Instance.Info ( "[Error]: The file '" + loadFileName + "' cannot be found." );
                return "";
            }

            return fileName;
        }

    }
}
