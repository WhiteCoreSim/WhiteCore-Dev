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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using Nini.Config;
using OpenMetaverse.StructuredData;
using RunTimeCompiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace WhiteCore.Modules.Installer
{
    public class ModuleInstaller : IService
    {
        #region IService Members

        public IConfigSource m_config;
        public IRegistryCore m_registry;

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_config = config;
            m_registry = registry;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            MainConsole.Instance.Commands.AddCommand(
                "compile module",
                "compile module [module name] [--gui]",
                "Compiles and adds a given addon-module to WhiteCore, adding the gui parameter opens a file picker in Windows",
                consoleCommand, false, true);
        }

        public void FinishedStartup()
        {
        }

        #endregion

        private void consoleCommand(IScene scene, string[] commands)
        {
            bool useGUI = false;
            string moduleName = "";

            List<string> cmdparams = new List<string>(commands);
            foreach (string param in commands)
            {
                if (param.StartsWith("--gui"))
                {
                    useGUI = true;
                    cmdparams.Remove(param);
                }
            }

            // check for provided user name
            if (cmdparams.Count < 3)
            {
                MainConsole.Instance.Prompt ("Module to compile", moduleName);
                if (moduleName == "")
                    return;
            }


            if (useGUI)
            {
                bool finished = false;
                OpenFileDialog dialog = new OpenFileDialog
                                            {
                                                Filter =
                                                    "Build Files (*.am)|*.am|Xml Files (*.xml)|*.xml|Dll Files (*.dll)|*.dll"
                                            };
                System.Threading.Thread t = new System.Threading.Thread(delegate()
                                                                            {
                                                                                if (dialog.ShowDialog() == DialogResult.OK)
                                                                                {
                                                                                    finished = true;
                                                                                }
                                                                            });
                t.SetApartmentState(System.Threading.ApartmentState.STA);
                t.Start();
                while (!finished)
                    System.Threading.Thread.Sleep(10);
                CompileModule(dialog.FileName);
            }
            else
               CompileModule(moduleName);
        }

        public void CompileModule(string fileName)
        {
            // check for details
            var modulePath = Path.GetDirectoryName (fileName);
            if (!Directory.Exists (modulePath))
            {
                MainConsole.Instance.Error ("Invalid module path: " + modulePath);
                return;
            } else if (!File.Exists (fileName))
            {
                MainConsole.Instance.Error ("Unable to find the module " + fileName);
                return;
            }

            if (Path.GetExtension(fileName) == ".am")
                ReadAMBuildFile(fileName);
            else if (Path.GetExtension(fileName) == ".dll")
                CopyAndInstallDllFile(fileName, Path.GetFileNameWithoutExtension(fileName) + ".dll", null);
                    //Install .dll files
            else
            {
                string tmpFile = Path.Combine(Path.GetDirectoryName(fileName),
                                              Path.GetFileNameWithoutExtension(fileName) + ".tmp.xml");
                if (!File.Exists (tmpFile))
                {
                    MainConsole.Instance.Error ("Unable to find the module prebuild information: " + tmpFile);
                    return;
                }
                ReadFileAndCreatePrebuildFile(tmpFile, fileName);
                BuildCSProj(tmpFile);
                CreateAndCompileCSProj(tmpFile, fileName, null);
            }
        }

        private void ReadAMBuildFile(string fileName)
        {
            OSDMap map = (OSDMap) OSDParser.DeserializeJson(File.ReadAllText(fileName));
            string prebuildFile = Path.Combine(Path.GetDirectoryName(fileName), map["PrebuildFile"]);
            string tmpFile = Path.Combine(Path.GetDirectoryName(fileName), map["TmpFile"]);

            ReadFileAndCreatePrebuildFile(tmpFile, prebuildFile);
            BuildCSProj(tmpFile);
            CreateAndCompileCSProj(tmpFile, prebuildFile, map);
            ConfigureModule(Path.GetDirectoryName(fileName), map);
        }

        private void ConfigureModule(string installationPath, OSDMap map)
        {
            bool standaloneSwitch = map["StandaloneSwitch"];
            bool ConsoleConfiguration = map["ConsoleConfiguration"];
            bool useConfigDirectory = true;
            if (standaloneSwitch)
                useConfigDirectory =
                    MainConsole.Instance.Prompt("Are you running this module in standalone or on WhiteCore.Server?",
                                                "Standalone", new List<string>(new[] {"Standalone", "WhiteCore.Server"})) ==
                    "Standalone";
            string configDir = useConfigDirectory ? map["ConfigDirectory"] : map["ServerConfigDirectory"];
            string configurationFinished = map["ConfigurationFinished"];
            string configPath = Path.Combine(Environment.CurrentDirectory, configDir);
            OSDArray config = (OSDArray) map["Configs"];
            foreach (OSD c in config)
            {
                try
                {
                    File.Copy(Path.Combine(installationPath, c.AsString()), Path.Combine(configPath, c.AsString()));
                }
                catch
                {
                }
            }
            if (ConsoleConfiguration)
            {
                OSDMap ConsoleConfig = (OSDMap) map["ConsoleConfig"];
                foreach (KeyValuePair<string, OSD> kvp in ConsoleConfig)
                {
                    string resp = MainConsole.Instance.Prompt(kvp.Key);
                    OSDMap configMap = (OSDMap) kvp.Value;
                    string file = configMap["File"];
                    string Section = configMap["Section"];
                    string ConfigOption = configMap["ConfigOption"];
                    Nini.Ini.IniDocument doc = new Nini.Ini.IniDocument(Path.Combine(configPath, file));
                    doc.Sections[Section].Set(ConfigOption, resp);
                    doc.Save(Path.Combine(configPath, file));
                }
            }
            MainConsole.Instance.Warn(configurationFinished);
        }

        private static void BuildCSProj(string tmpFile)
        {
            Process p = new Process
                            {
                                StartInfo =
                                    new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "Prebuild.exe"),
                                                         "/target vs2010 /targetframework v4_0 /file " + tmpFile)
                            };
            p.Start();
            p.WaitForExit();
        }

        private void CreateAndCompileCSProj(string tmpFile, string fileName, OSDMap options)
        {
            File.Delete(tmpFile);
            string projFile = FindProjFile(Path.GetDirectoryName(fileName));
            MainConsole.Instance.Warn("Installing " + Path.GetFileNameWithoutExtension(projFile));
            BasicProject project = ProjectReader.Instance.ReadProject(projFile);
            CsprojCompiler compiler = new CsprojCompiler();
            compiler.Compile(project);
            string dllFile = Path.Combine(Path.GetDirectoryName(fileName),
                                          Path.GetFileNameWithoutExtension(projFile) + ".dll");
            string copiedDllFile = Path.GetFileNameWithoutExtension(projFile) + ".dll";
            if (project.BuildOutput == "Project built successfully!")
            {
                if (options != null)
                    MainConsole.Instance.Warn(options["CompileFinished"]);
                CopyAndInstallDllFile(dllFile, copiedDllFile, options);
            }
            else
                MainConsole.Instance.Warn("Failed to compile the module, exiting! (" + project.BuildOutput + ")");

            File.Delete(Path.Combine(Path.GetDirectoryName(tmpFile), "WhiteCore.sln"));
            File.Delete(Path.Combine(Path.GetDirectoryName(tmpFile), projFile));
            File.Delete(Path.Combine(Path.GetDirectoryName(tmpFile), projFile + ".user"));
            File.Delete(Path.Combine(Path.GetDirectoryName(tmpFile), copiedDllFile));
        }

        private void CopyAndInstallDllFile(string dllFile, string copiedDllFile, OSDMap options)
        {
            try
            {
                File.Copy(dllFile, copiedDllFile);
                if (options != null)
                    MainConsole.Instance.Warn(options["CopyFinished"]);
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Warn("Failed to copy the module! (" + ex + ")");
                if (MainConsole.Instance.Prompt("Continue?", "yes", new List<string>(new[] {"yes", "no"})) == "no")
                    return;
            }
            string basePath = Path.Combine(Environment.CurrentDirectory, copiedDllFile);
            LoadModulesFromDllFile(basePath);
            MainConsole.Instance.Warn("Installed the module successfully!");
        }

        private void ReadFileAndCreatePrebuildFile(string tmpFile, string fileName)
        {
            string file = File.ReadAllText(fileName);
            file = file.Replace("<?xml version=\"1.0\" ?>", "<?xml version=\"1.0\" ?>" + Environment.NewLine +
                                                            "<Prebuild version=\"1.7\" xmlns=\"http://dnpb.sourceforge.net/schemas/prebuild-1.7.xsd\">" +
                                                            Environment.NewLine +
                                                            "  <Solution activeConfig=\"Debug\" name=\"WhiteCore\" path=\"\" version=\"0.5.0-$Rev$\">" +
                                                            Environment.NewLine +
                                                            "<Configuration name=\"Debug\" platform=\"x86\">" +
                                                            Environment.NewLine +
                                                            @"<Options>
                    <CompilerDefines>TRACE;DEBUG</CompilerDefines>
                    <OptimizeCode>false</OptimizeCode>
                    <CheckUnderflowOverflow>false</CheckUnderflowOverflow>
                    <AllowUnsafe>true</AllowUnsafe>
                    <WarningLevel>4</WarningLevel>
                    <WarningsAsErrors>false</WarningsAsErrors>
                    <OutputPath>bin</OutputPath>
                    <DebugInformation>true</DebugInformation>
                    <IncrementalBuild>true</IncrementalBuild>
                    <NoStdLib>false</NoStdLib>
                  </Options>
                </Configuration>" + Environment.NewLine +
                                                            "<Configuration name=\"Debug\" platform=\"AnyCPU\">" +
                                                            Environment.NewLine +
                                                            "<Options>" + Environment.NewLine +
                                                            "<target name=\"net-1.1\" description=\"Sets framework to .NET 1.1\">" +
                                                            Environment.NewLine +
                                                            "<property name=\"nant.settings.currentframework\" value=\"net-1.1\" />" +
                                                            Environment.NewLine +
                                                            @"</target>
        <CompilerDefines>TRACE;DEBUG</CompilerDefines>
        <OptimizeCode>false</OptimizeCode>
        <CheckUnderflowOverflow>false</CheckUnderflowOverflow>
        <AllowUnsafe>true</AllowUnsafe>
        <WarningLevel>4</WarningLevel>
        <WarningsAsErrors>false</WarningsAsErrors>
        <OutputPath>bin</OutputPath>
        <DebugInformation>true</DebugInformation>
        <IncrementalBuild>true</IncrementalBuild>
        <NoStdLib>false</NoStdLib>
      </Options>
    </Configuration>" + Environment.NewLine +
                                                            "<Configuration name=\"Debug\" platform=\"x64\">" +
                                                            Environment.NewLine +
                                                            @"<Options>
        <CompilerDefines>TRACE;DEBUG</CompilerDefines>
        <OptimizeCode>false</OptimizeCode>
        <CheckUnderflowOverflow>false</CheckUnderflowOverflow>
        <AllowUnsafe>true</AllowUnsafe>
        <WarningLevel>4</WarningLevel>
        <WarningsAsErrors>false</WarningsAsErrors>
        <OutputPath>bin</OutputPath>
        <DebugInformation>true</DebugInformation>
        <IncrementalBuild>true</IncrementalBuild>
        <NoStdLib>false</NoStdLib>
      </Options>
    </Configuration>" + Environment.NewLine +
                                                            "<Configuration name=\"Release\" platform=\"x86\">" +
                                                            Environment.NewLine +
                                                            @"<Options>
        <CompilerDefines>TRACE;DEBUG</CompilerDefines>
        <OptimizeCode>true</OptimizeCode>
        <CheckUnderflowOverflow>false</CheckUnderflowOverflow>
        <AllowUnsafe>true</AllowUnsafe>
        <WarningLevel>4</WarningLevel>
        <WarningsAsErrors>false</WarningsAsErrors>
        <SuppressWarnings/>
        <OutputPath>bin</OutputPath>
        <DebugInformation>false</DebugInformation>
        <IncrementalBuild>true</IncrementalBuild>
        <NoStdLib>false</NoStdLib>
      </Options>
    </Configuration>" + Environment.NewLine +
                                                            "<Configuration name=\"Release\" platform=\"AnyCPU\">" +
                                                            Environment.NewLine +
                                                            @"<Options>
        <CompilerDefines>TRACE;DEBUG</CompilerDefines>
        <OptimizeCode>true</OptimizeCode>
        <CheckUnderflowOverflow>false</CheckUnderflowOverflow>
        <AllowUnsafe>true</AllowUnsafe>
        <WarningLevel>4</WarningLevel>
        <WarningsAsErrors>false</WarningsAsErrors>
        <SuppressWarnings/>
        <OutputPath>bin</OutputPath>
        <DebugInformation>false</DebugInformation>
        <IncrementalBuild>true</IncrementalBuild>
        <NoStdLib>false</NoStdLib>
      </Options>
    </Configuration>" + Environment.NewLine +
                                                            "<Configuration name=\"Release\" platform=\"x64\">" +
                                                            Environment.NewLine +
                                                            @"<Options>
        <CompilerDefines>TRACE;DEBUG</CompilerDefines>
        <OptimizeCode>true</OptimizeCode>
        <CheckUnderflowOverflow>false</CheckUnderflowOverflow>
        <AllowUnsafe>true</AllowUnsafe>
        <WarningLevel>4</WarningLevel>
        <WarningsAsErrors>false</WarningsAsErrors>
        <SuppressWarnings/>
        <OutputPath>bin</OutputPath>
        <DebugInformation>false</DebugInformation>
        <IncrementalBuild>true</IncrementalBuild>
        <NoStdLib>false</NoStdLib>
      </Options>
    </Configuration>");
            file = file + "</Solution>" + Environment.NewLine + "</Prebuild>";

            file = FixPath(file);
            file = file.Replace("../../../bin/", "../bin");
            file = file.Replace("../../..", "../bin");
            File.WriteAllText(tmpFile, file);
        }

        private void LoadModulesFromDllFile(string copiedDllFile)
        {
            List<IService> services = WhiteCoreModuleLoader.LoadPlugins<IService>(copiedDllFile);
            List<IApplicationPlugin> appPlugins = WhiteCoreModuleLoader.LoadPlugins<IApplicationPlugin>(copiedDllFile);
            List<INonSharedRegionModule> nsregionModule =
                WhiteCoreModuleLoader.LoadPlugins<INonSharedRegionModule>(copiedDllFile);
            foreach (IService service in services)
            {
                service.Initialize(m_config, m_registry);
                service.Start(m_config, m_registry);
                service.FinishedStartup();
            }
            foreach (IApplicationPlugin plugin in appPlugins)
            {
                plugin.PreStartup(m_registry.RequestModuleInterface<ISimulationBase>());
                plugin.Initialize(m_registry.RequestModuleInterface<ISimulationBase>());
                plugin.PostInitialise();
                plugin.Start();
                plugin.PostStart();
            }
            IRegionModulesController rmc = m_registry.RequestModuleInterface<IRegionModulesController>();
            ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager>();
            if (manager != null)
            {
                foreach (INonSharedRegionModule nsrm in nsregionModule)
                {
                    foreach (IScene scene in manager.Scenes)
                    {
                        nsrm.Initialise(m_config);
                        nsrm.AddRegion(scene);
                        nsrm.RegionLoaded(scene);
                        rmc.AllModules.Add(nsrm);
                    }
                }
            }
        }

        private string FindProjFile(string p)
        {
            string[] files = Directory.GetFiles(p, "*.csproj");
            return files[0];
        }

        private string FixPath(string file)
        {
            string f = "";
            foreach (string line in file.Split('\n'))
            {
                string l = line;
                if (line.StartsWith("<Project frameworkVersion="))
                {
                    string[] lines = line.Split(new[] {"path=\""}, StringSplitOptions.RemoveEmptyEntries);
                    string li = "";
                    int i = 0;
                    foreach (string ll in lines[1].Split('"'))
                    {
                        if (i > 0)
                            li += ll + "\"";
                        i++;
                    }
                    l = lines[0] + "path=\"./\" " + li.Remove(li.Length - 1);
                }
                f += l;
            }
            return f;
        }
    }
}