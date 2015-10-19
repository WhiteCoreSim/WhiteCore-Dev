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

using Nini.Config;
using Nini.Ini;
using System;
using System.IO;
using System.Reflection;

namespace WhiteCore.Simulation.Base
{
    public class BinMigratorService
    {
        private const int _currentBinVersion = 10;

        public void MigrateBin()
        {
            int currentVersion = GetBinVersion();
            if (currentVersion != _currentBinVersion)
            {
                UpgradeToTarget(currentVersion);
                SetBinVersion(_currentBinVersion);
            }
        }

        public int GetBinVersion()
        {
            if (!File.Exists("WhiteCore.version"))
                return 0;
            string file = File.ReadAllText("WhiteCore.version");
            return int.Parse(file);
        }

        public void SetBinVersion(int version)
        {
            File.WriteAllText("WhiteCore.version", version.ToString());
        }

        public bool UpgradeToTarget(int currentVersion)
        {
            try
            {
                while (currentVersion != _currentBinVersion)
                {
                    MethodInfo info = GetType().GetMethod("RunMigration" + ++currentVersion);
                    if (info != null)
                        info.Invoke(this, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error running bin migration " + currentVersion + ", " + ex.ToString());
                return false;
            }
            return true;
        }

        //Next: 9

        public void RunMigration9()
        {
            if (File.Exists("WhiteCore.UserServer.exe"))
                File.Delete("WhiteCore.UserServer.exe");
        }

        public void RunMigration10()
        {
            foreach (string dir in Directory.GetDirectories("ScriptEngines/"))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    public enum MigratorAction
    {
        Add,
        Remove
    }

    public class IniMigrator
    {
        public static void UpdateIniFile(string fileName, string handler, string[] names, string[] values,
                                         MigratorAction[] actions)
        {
            if (File.Exists(fileName + ".example")) //Update the .example files too if people haven't
                UpdateIniFile(fileName + ".example", handler, names, values, actions);
            if (File.Exists(fileName))
            {
                IniConfigSource doc = new IniConfigSource(fileName, IniFileType.AuroraStyle);
                IConfig section = doc.Configs[handler];
                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    string value = values[i];
                    MigratorAction action = actions[i];
                    if (action == MigratorAction.Add)
                        section.Set(name, value);
                    else
                        section.Remove(name);
                }
                doc.Save();
            }
        }
    }
}