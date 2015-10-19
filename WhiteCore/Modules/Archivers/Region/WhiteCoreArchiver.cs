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
using System.IO;
using System.IO.Compression;
using System.Threading;
using Nini.Config;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Services;

#if ISWIN
using System.Windows.Forms;
using Microsoft.Win32;
using WhiteCore.Framework.Utilities;
#endif

namespace WhiteCore.Modules.Archivers
{
    public class WhiteCoreArchiver : IService, IWhiteCoreBackupArchiver
    {
        Int64 m_AllowPrompting;

        #region IWhiteCoreBackupArchiver Members

        public bool AllowPrompting
        {
            get { return Interlocked.Read (ref m_AllowPrompting) == 0; }
            set
            {
                if (value)
                    Interlocked.Increment (ref m_AllowPrompting);
                else
                    Interlocked.Decrement (ref m_AllowPrompting);
            }
        }

        public void SaveRegionBackup (TarArchiveWriter writer, IScene scene)
        {
            writer.WriteDir ("assets"); //Used by many, create it by default

            IWhiteCoreBackupModule[] modules = scene.RequestModuleInterfaces<IWhiteCoreBackupModule> ();
            foreach (IWhiteCoreBackupModule module in modules)
                module.SaveModuleToArchive (writer, scene);

            foreach (IWhiteCoreBackupModule module in modules)
            {
                while (module.IsArchiving) //Wait until all are done
                    Thread.Sleep (100);
            }

            writer.Close ();
            GC.Collect ();
            MainConsole.Instance.Info ("[Archive]: Finished saving of archive.");
        }

        public void LoadRegionBackup (TarArchiveReader reader, IScene scene)
        {
            IWhiteCoreBackupModule[] modules = scene.RequestModuleInterfaces<IWhiteCoreBackupModule> ();

            byte[] data;
            string filePath;
            TarArchiveReader.TarEntryType entryType;

            foreach (IWhiteCoreBackupModule module in modules)
                module.BeginLoadModuleFromArchive (scene);

            while ((data = reader.ReadEntry (out filePath, out entryType)) != null)
            {
                if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                    continue;
                foreach (IWhiteCoreBackupModule module in modules)
                    module.LoadModuleFromArchive (data, filePath, entryType, scene);
            }

            reader.Close ();

            foreach (IWhiteCoreBackupModule module in modules)
                module.EndLoadModuleFromArchive (scene);
        }

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "save archive",
                    "save archive",
                    "Saves a WhiteCore '.abackup' archive (deprecated)",
                    SaveWhiteCoreArchive, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "load archive",
                    "load archive",
                    "Loads a WhiteCore '.abackup' archive",
                    LoadWhiteCoreArchive, true, false);
            }

            #if ISWIN
            //Register the extension
            const string ext = ".abackup";
            try
            {
                if (Util.IsWindows ())
                {
                    RegistryKey key = Registry.ClassesRoot.CreateSubKey (ext + "\\DefaultIcon");
                    key.SetValue ("", Application.StartupPath + "\\CrateDownload.ico");
                    key.Close ();
                }
            } catch
            {
            }
            #endif

            //Register the interface
            registry.RegisterModuleInterface<IWhiteCoreBackupArchiver> (this);

        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup ()
        {
        }

        #endregion

        void LoadWhiteCoreArchive (IScene scene, string[] cmd)
        {
            string fileName = MainConsole.Instance.Prompt ("What file name should we load?",
                                  scene.RegionInfo.RegionName + ".abackup");

            // a couple of sanity checks
            string extension = Path.GetExtension (fileName);

            if (extension == string.Empty)
            {
                fileName = fileName + ".abackup";
            }

            if (!File.Exists (fileName))
            {
                MainConsole.Instance.Info ("[Archiver]: Region archive file '" + fileName + "' not found.");
                return;
            }

            var stream = ArchiveHelpers.GetStream (fileName);
            if (stream == null)
            {
                MainConsole.Instance.Warn ("No file found with the specified name.");
                return;
            }
            GZipStream m_loadStream = new GZipStream (stream, CompressionMode.Decompress);
            TarArchiveReader reader = new TarArchiveReader (m_loadStream);

            LoadRegionBackup (reader, scene);
            GC.Collect ();
        }

        void SaveWhiteCoreArchive (IScene scene, string[] cmd)
        {
            string fileName = MainConsole.Instance.Prompt ("What file name will this be saved as?",
                                  scene.RegionInfo.RegionName + ".abackup");

            //some file sanity checks
            string extension = Path.GetExtension (fileName);

            if (extension == string.Empty)
            {
                fileName = fileName + ".abackup";
            }

            string fileDir = Path.GetDirectoryName (fileName);
            if (fileDir == "")
            {
                fileDir = "./";
            }
            if (!Directory.Exists (fileDir))
            {
                MainConsole.Instance.Info ("[Archiver]: The file path specified, '" + fileDir + "' does not exist!");
                return;
            }

            if (File.Exists (fileName))
            {
                if (MainConsole.Instance.Prompt ("[Archiver]: The Region archive file '" + fileName + "' already exists. Overwrite?", "yes") != "yes")
                    return;

                File.Delete (fileName);
            }

            GZipStream m_saveStream = new GZipStream (new FileStream (fileName, FileMode.Create), CompressionMode.Compress);
            TarArchiveWriter writer = new TarArchiveWriter (m_saveStream);

            SaveRegionBackup (writer, scene);
        }
    }
}