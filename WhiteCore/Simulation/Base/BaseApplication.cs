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

//#define BlockUnsupportedVersions

using WhiteCore.Framework.Configuration;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace WhiteCore.Simulation.Base
{
    /// <summary>
    ///     Starting class for the WhiteCore Server
    /// </summary>
    public class BaseApplication
    {

        /// <summary>
        ///     Save Crashes in the bin/crashes folder.  Configurable with m_crashDir
        /// </summary>
        public static bool m_saveCrashDumps;

        /// <summary>
        ///     Loader of configuration files
        /// </summary>
        static readonly ConfigurationLoader m_configLoader = new ConfigurationLoader();

        /// <summary>
        ///     Directory to save crash reports to.  Relative to bin/
        /// </summary>
        public static string m_crashDir = "crashes";

        static bool _IsHandlingException; // Make sure we don't go recursive on ourself

        //could move our main function into OpenSimMain and kill this class
        public static void BaseMain(string[] args, string defaultIniFile, ISimulationBase simBase)
        {
            // First line, hook the appdomain to the crash reporter
            AppDomain.CurrentDomain.UnhandledException +=
                CurrentDomain_UnhandledException;

            // Add the arguments supplied when running the application to the configuration
            ArgvConfigSource configSource = new ArgvConfigSource(args);

            if (!args.Contains("-skipconfig"))
                Configure(false);

            // Increase the number of IOCP threads available. Mono defaults to a tragically low number
            int workerThreads, iocpThreads;
            ThreadPool.GetMaxThreads(out workerThreads, out iocpThreads);
            //MainConsole.Instance.InfoFormat("[WHiteCore MAIN]: Runtime gave us {0} worker threads and {1} IOCP threads", workerThreads, iocpThreads);
            if (workerThreads < 500 || iocpThreads < 1000)
            {
                workerThreads = 500;
                iocpThreads = 1000;
                //MainConsole.Instance.Info("[WHiteCore MAIN]: Bumping up to 500 worker threads and 1000 IOCP threads");
                ThreadPool.SetMaxThreads(workerThreads, iocpThreads);
            }

            BinMigratorService service = new BinMigratorService();
            service.MigrateBin();
            // Configure nIni aliases and localles
            Culture.SystemCultureInfo = CultureInfo.CurrentCulture;
            Culture.SetCurrentCulture();
            configSource.Alias.AddAlias("On", true);
            configSource.Alias.AddAlias("Off", false);
            configSource.Alias.AddAlias("True", true);
            configSource.Alias.AddAlias("False", false);

            //Command line switches
            configSource.AddSwitch("Startup", "inifile");
            configSource.AddSwitch("Startup", "inimaster");
            configSource.AddSwitch("Startup", "inigrid");
            configSource.AddSwitch("Startup", "inisim");
            configSource.AddSwitch("Startup", "inidirectory");
            configSource.AddSwitch("Startup", "oldoptions");
            configSource.AddSwitch("Startup", "inishowfileloading");
            configSource.AddSwitch("Startup", "mainIniDirectory");
            configSource.AddSwitch("Startup", "mainIniFileName");
            configSource.AddSwitch("Startup", "secondaryIniFileName");
            configSource.AddSwitch("Startup", "RegionDataFileName");
            configSource.AddSwitch("Console", "Console");
            configSource.AddSwitch("Console", "LogAppendName");
            configSource.AddSwitch("Console", "LogPath");
            configSource.AddSwitch("Network", "http_listener_port");

            IConfigSource m_configSource = Configuration(configSource, defaultIniFile);

            // Check if we're saving crashes
            m_saveCrashDumps = m_configSource.Configs["Startup"].GetBoolean("save_crashes", m_saveCrashDumps);

            // load Crash directory config
            m_crashDir = m_configSource.Configs["Startup"].GetString("crash_dir", m_crashDir);

            //Initialize the sim base now
            Startup(configSource, m_configSource, simBase.Copy(), args);
        }

        public static void Configure(bool requested)
        {
            string WhiteCore_ConfigDir = Constants.DEFAULT_CONFIG_DIR;
            bool isWhiteCoreExe = AppDomain.CurrentDomain.FriendlyName == "WhiteCore.exe" ||
                               AppDomain.CurrentDomain.FriendlyName == "WhiteCore.vshost.exe";

             bool existingConfig = (
                File.Exists(Path.Combine(WhiteCore_ConfigDir,"MyWorld.ini")) ||
                File.Exists(Path.Combine(WhiteCore_ConfigDir,"WhiteCore.ini")) ||
                File.Exists(Path.Combine(WhiteCore_ConfigDir,"WhiteCore.Server.ini"))
                );

            if ( requested || !existingConfig )
            {
                string resp = "no";
                if (!requested)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("\n\n************* WhiteCore initial run. *************");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(
                        "\n\n   This appears to be your first time running WhiteCore.\n"+
                        "If you have already configured your *.ini files, please ignore this warning and press enter;\n" +
                        "Otherwise type 'yes' and WhiteCore will guide you through the configuration process.\n\n"+
                        "Remember, these file names are Case Sensitive in Linux and Proper Cased.\n"+
                        "1. " + WhiteCore_ConfigDir + "/WhiteCore.ini\nand\n" +
                        "2. " + WhiteCore_ConfigDir + "/Sim/Standalone/StandaloneCommon.ini \nor\n" +
                        "3. " + WhiteCore_ConfigDir + "/Grid/GridCommon.ini\n" +
                        "\nAlso, you will want to examine these files in great detail because only the basic system will " +
                        "load by default. WhiteCore can do a LOT more if you spend a little time going through these files.\n\n");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This will overwrite any existing configuration files for your sim");
                    Console.ResetColor();
                    resp = ReadLine("Do you want to configure WhiteCore now", resp);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("This will overwrite any existing configuration files for your sim");
                    Console.ResetColor();
                    resp = ReadLine("Do you want to configure WhiteCore now", resp);
                }
                if (resp == "yes")
                {
                    string dbSource = "localhost";
					string dbPasswd = "whitecore";
					string dbSchema = "whitecore";
					string dbUser = "whitecore";
                    string dbPort = "3306";
                    string gridIPAddress = Utilities.GetExternalIp();
                    bool isStandalone = true;
                    string dbType = "1";
                    string gridName = "WhiteCore-Sim Grid";
                    string welcomeMessage = "";
                    string allowAnonLogin = "true";
                    uint port = 9000;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("====================================================================");
					Console.WriteLine("======================= WhiteCore CONFIGURATOR =====================");
                    Console.WriteLine("====================================================================");
                    Console.ResetColor();

                    if (isWhiteCoreExe)
                    {
                        Console.WriteLine("This installation is going to run in");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("[1] Standalone Mode \n[2] Grid Mode");
                        Console.ResetColor();
                        isStandalone = ReadLine("Choose 1 or 2", "1") == "1";


                        Console.WriteLine("Http Port for the server");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Default is 9000");
                        Console.ResetColor();
                        port = uint.Parse(ReadLine("Choose the port", "9000"));
                    }

                    if (isStandalone)
                    {
                        Console.WriteLine("Which database do you want to use?");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("[1] SQLite \n[2] MySQL");
                        Console.ResetColor();
                        dbType = ReadLine("Choose 1 or 2", dbType);
                        if (dbType == "2")
                        {
                            Console.WriteLine(
                                "Note: this setup does not automatically create a MySQL installation for you.\n" +
                                " This will configure the WhiteCore setting but you must install MySQL as well");

                            dbSource = ReadLine("MySQL database IP", dbSource);
                            dbPort = ReadLine("MySQL database port (if not default)", dbPort);
                            dbSchema = ReadLine("MySQL database name for your region", dbSchema);
                            dbUser = ReadLine("MySQL database user account", dbUser);

                            Console.Write("MySQL database password for that account: ");
                            dbPasswd = Console.ReadLine();
                        }
                    }

                    if (isStandalone)
                    {
                        gridName = ReadLine("Name of your WhiteCore-Sim Grid", gridName);

                        welcomeMessage = "Welcome to " + gridName + ", <USERNAME>!";
                        welcomeMessage = ReadLine("Welcome Message to show during login\n" +
                            "  (putting <USERNAME> into the welcome message will insert the user's name)", welcomeMessage);

                        allowAnonLogin = ReadLine("Create accounts automatically when users log in\n" +
                            "  (This means you don't have to create all accounts manually\n" +
                            "   using the console or web interface): ",
                            allowAnonLogin);
                    }

                    if (!isStandalone)
                        gridIPAddress =
                            ReadLine("The external domain name or IP address of the grid server you wish to connect to",
                                     gridIPAddress);

                    //Data.ini setup
                    if (isStandalone)
                    {
                        string folder = isWhiteCoreExe ? WhiteCore_ConfigDir + "/Sim/" : WhiteCore_ConfigDir + "/Grid/ServerConfiguration/";
                        MakeSureExists(folder + "Data/Data.ini");
                        IniConfigSource data_ini = new IniConfigSource(folder + "Data/Data.ini",
                                                                       Nini.Ini.IniFileType.AuroraStyle);
                        IConfig conf = data_ini.AddConfig("DataFile");
                        if (dbType == "1")
                            conf.Set("Include-SQLite", folder + "Data/SQLite.ini");
                        else
                            conf.Set("Include-MySQL", folder + "Data/MySQL.ini");

                        if (isWhiteCoreExe)
                            conf.Set("Include-FileBased", "Sim/Data/FileBased.ini");

                        conf = data_ini.AddConfig("WhiteCoreConnectors");
                        conf.Set("ValidateTables", true);

                        data_ini.Save();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Your Data.ini has been successfully configured");
                        Console.ResetColor();

                        if (dbType == "2") //MySQL setup
                        {
                            MakeSureExists(folder + "Data/MySQL.ini");
                            IniConfigSource mysql_ini = new IniConfigSource(folder + "Data/MySQL.ini",
                                                                            Nini.Ini.IniFileType.AuroraStyle);
                            IniConfigSource mysql_ini_example = new IniConfigSource(folder + "Data/MySQL.ini.example",
                                                                                    Nini.Ini.IniFileType.AuroraStyle);
                            foreach (IConfig config in mysql_ini_example.Configs)
                            {
                                IConfig newConfig = mysql_ini.AddConfig(config.Name);
                                foreach (string key in config.GetKeys())
                                {
                                    if (key == "ConnectionString")
                                        newConfig.Set(key,
                                                      string.Format(
                                                "\"Data Source={0};Port={1};Database={2};User ID={3};Password={4};\"",
                                                dbSource, dbPort, dbSchema, dbUser, dbPasswd));
                                    else
                                        newConfig.Set(key, config.Get(key));
                                }
                            }
                            mysql_ini.Save();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Your MySQL.ini has been successfully configured");
                            Console.ResetColor();
                        }
                    }

                    if (isWhiteCoreExe)
                    {
						string folder = WhiteCore_ConfigDir;
						MakeSureExists(folder + "/WhiteCore.ini");

						IniConfigSource whitecore_ini = new IniConfigSource(folder 
							+ "/WhiteCore.ini", Nini.Ini.IniFileType.AuroraStyle);
						IniConfigSource whitecore_ini_example = new IniConfigSource(folder 
							+ "/WhiteCore.ini.example", Nini.Ini.IniFileType.AuroraStyle);

						foreach (IConfig config in whitecore_ini_example.Configs)
                        {
							IConfig newConfig = whitecore_ini.AddConfig(config.Name);
                            foreach (string key in config.GetKeys())
                            {
                                if (key == "http_listener_port")
                                    newConfig.Set(key, port);
                                else
                                    newConfig.Set(key, config.Get(key));
                            }
                        }

						whitecore_ini.Save();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Your WhiteCore.ini has been successfully configured");
                        Console.ResetColor();

						MakeSureExists(folder + "/Sim/Main.ini");

						IniConfigSource main_ini = new IniConfigSource(folder + "/Sim/Main.ini", 
							Nini.Ini.IniFileType.AuroraStyle);
						//IniConfigSource main_ini_example = new IniConfigSource(folder 
						//	+ "/Sim/Main.ini.example", Nini.Ini.IniFileType.AuroraStyle);

                        IConfig conf = main_ini.AddConfig("Architecture");
                        if (isStandalone)
                            conf.Set("Include-Standalone", "Sim/Standalone/StandaloneCommon.ini");
                        else
                            conf.Set("Include-Grid", "Sim/Grid/WhiteCoreGridCommon.ini");
                        conf.Set("Include-Includes", "Sim/Includes.ini");

                        main_ini.Save();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Your Main.ini has been successfully configured");
                        Console.ResetColor();

                        if (isStandalone)
                        {
							MakeSureExists(folder + "/Sim/Standalone/StandaloneCommon.ini");

							IniConfigSource standalone_ini = new IniConfigSource(folder 
								+ "/Sim/Standalone/StandaloneCommon.ini", Nini.Ini.IniFileType.AuroraStyle);
							IniConfigSource standalone_ini_example = new IniConfigSource(folder 
								+ "/Sim/Standalone/StandaloneCommon.ini.example", Nini.Ini.IniFileType.AuroraStyle);

                            foreach (IConfig config in standalone_ini_example.Configs)
                            {
                                IConfig newConfig = standalone_ini.AddConfig(config.Name);
                                if (newConfig.Name == "GridInfoService")
                                {
                                    newConfig.Set("GridInfoInHandlerPort", 0);
                                    newConfig.Set("login", "http://" + gridIPAddress + ":9000/");
                                    newConfig.Set("gridname", gridName);
                                    newConfig.Set("gridnick", gridName);
                                }
                                else
                                {
                                    foreach (string key in config.GetKeys())
                                    {
                                        if (key == "WelcomeMessage")
                                            newConfig.Set(key, welcomeMessage);
                                        else if (key == "AllowAnonymousLogin")
                                            newConfig.Set(key, allowAnonLogin);
                                        else
                                            newConfig.Set(key, config.Get(key));
                                    }
                                }
                            }

                            standalone_ini.Save();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Your StandaloneCommon.ini has been successfully configured");
                            Console.ResetColor();
                        }
                        else
                        {
                            MakeSureExists("Sim/Grid/WhiteCoreGridCommon.ini");
                            IniConfigSource grid_ini = new IniConfigSource("Sim/Grid/WhiteCoreGridCommon.ini",
                                                                           Nini.Ini.IniFileType.AuroraStyle);

                            conf = grid_ini.AddConfig("Includes");
                            conf.Set("Include-Grid", "Sim/Grid/Grid.ini");
                            conf = grid_ini.AddConfig("Configuration");
                            conf.Set("GridServerURI", "http://" + gridIPAddress + ":8012/grid/");

                            grid_ini.Save();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Your Grid.ini has been successfully configured");
                            Console.ResetColor();
                        }
                    }
                    if (!isWhiteCoreExe)
                    {
                        string folder = WhiteCore_ConfigDir;
                        MakeSureExists(folder + "Grid/ServerConfiguration/Login.ini");
                        IniConfigSource login_ini = new IniConfigSource(folder + "Grid/ServerConfiguration/Login.ini",
                                                                        Nini.Ini.IniFileType.AuroraStyle);
                        IniConfigSource login_ini_example =
                            new IniConfigSource(folder + "Grid/ServerConfiguration/Login.ini.example",
                                                Nini.Ini.IniFileType.AuroraStyle);
                        foreach (IConfig config in login_ini_example.Configs)
                        {
                            IConfig newConfig = login_ini.AddConfig(config.Name);
                            foreach (string key in config.GetKeys())
                            {
                                if (key == "WelcomeMessage")
                                    newConfig.Set(key, welcomeMessage);
                                else if (key == "AllowAnonymousLogin")
                                    newConfig.Set(key, allowAnonLogin);
                                else
                                    newConfig.Set(key, config.Get(key));
                            }
                        }
                        login_ini.Save();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Your Login.ini has been successfully configured");
                        Console.ResetColor();

                        MakeSureExists(folder + "Grid/ServerConfiguration/GridInfoService.ini");
                        IniConfigSource grid_info_ini =
                            new IniConfigSource(folder + "Grid/ServerConfiguration/GridInfoService.ini",
                                                Nini.Ini.IniFileType.AuroraStyle);
                        IConfig conf = grid_info_ini.AddConfig("GridInfoService");
                        conf.Set("GridInfoInHandlerPort", 8002);
                        conf.Set("login", "http://" + gridIPAddress + ":8002");
                        conf.Set("gridname", gridName);
                        conf.Set("gridnick", gridName);

                        grid_info_ini.Save();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Your GridInfoService.ini has been successfully configured");
                        Console.ResetColor();
                    }

                    Console.WriteLine("\n====================================================================\n");
                    Console.ResetColor();
                    Console.WriteLine("Your grid name is ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(gridName);
                    Console.ResetColor();
                    if (isStandalone)
                    {
                        Console.WriteLine("\nYour loginuri is ");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("http://" + gridIPAddress + (isWhiteCoreExe ? ":9000/" : ":8002/"));
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine("\nConnected Grid URL: ");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("http://" + gridIPAddress + ":8002/");
                        Console.ResetColor();
                    }
                    Console.WriteLine("\n====================================================================\n");
                    Console.WriteLine(
                        "If you ever want to rerun this configurator, you can type \"run configurator\" into the console to bring this prompt back up.");
                }
            }
        }

        static void MakeSureExists(string file)
        {
            if (File.Exists(file))
                File.Delete(file);
            File.Create(file).Close();
        }

        static string ReadLine(string log, string defaultReturn)
        {
            Console.WriteLine(log + ": [" + defaultReturn + "]");
            string mode = Console.ReadLine();
            if (mode == string.Empty)
                mode = defaultReturn;
            if (mode != null)
                mode = mode.Trim();
            return mode;
        }

        public static void Startup(IConfigSource originalConfigSource, IConfigSource configSource,
                                   ISimulationBase simBase, string[] cmdParameters)
        {
            //Get it ready to run
            simBase.Initialize(originalConfigSource, configSource, cmdParameters, m_configLoader);
            try
            {
                //Start it. This starts ALL modules and completes the startup of the application
                simBase.Startup();
                //Run the console now that we are done
                simBase.Run();
            }
            catch (Exception ex)
            {
                UnhandledException(false, ex);
                //Just clean it out as good as we can
                simBase.Shutdown(false);
            }
        }

        /// <summary>
        ///     Load the configuration for the Application
        /// </summary>
        /// <param name="configSource"></param>
        /// <param name="defaultIniFile"></param>
        /// <returns></returns>
        static IConfigSource Configuration(IConfigSource configSource, string defaultIniFile)
        {
            if (defaultIniFile != "")
                m_configLoader.defaultIniFile = defaultIniFile;
            return m_configLoader.LoadConfigSettings(configSource);
        }

        /// <summary>
        ///     Global exception handler -- all unhandled exceptions end up here :)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (_IsHandlingException)
                return;

            _IsHandlingException = true;
            Exception ex = (Exception) e.ExceptionObject;

            UnhandledException(e.IsTerminating, ex);

            _IsHandlingException = false;
        }

        static void UnhandledException(bool isTerminating, Exception ex)
        {
            string msg = String.Empty;
            msg += "\r\n";
            msg += "APPLICATION EXCEPTION DETECTED" + "\r\n";
            msg += "\r\n";

            msg += "Exception: " + ex + "\r\n";
            if (ex.InnerException != null)
            {
                msg += "InnerException: " + ex.InnerException + "\r\n";
            }

            msg += "\r\n";
            msg += "Application is terminating: " + isTerminating.ToString(CultureInfo.InvariantCulture) + "\r\n";

            MainConsole.Instance.ErrorFormat("[APPLICATION]: {0}", msg);

            handleException(msg, ex);
        }

        /// <summary>
        ///     Deal with sending the error to the error reporting service and saving the dump to the harddrive if needed
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="ex"></param>
        public static void handleException(string msg, Exception ex)
        {
            if (m_saveCrashDumps && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Log exception to disk
                try
                {
                    if (!Directory.Exists(m_crashDir))
                        Directory.CreateDirectory(m_crashDir);

                    string log = Path.Combine(m_crashDir, Util.GetUniqueFilename("crashDump" +
                                                                                 DateTime.Now.Day + DateTime.Now.Month +
                                                                                 DateTime.Now.Year + ".mdmp"));
                    using (FileStream fs = new FileStream(log, FileMode.Create, FileAccess.ReadWrite, FileShare.Write))
                    {
                        MiniDump.Write(fs.SafeFileHandle,
                                       MiniDump.Option.WithThreadInfo | MiniDump.Option.WithProcessThreadData |
                                       MiniDump.Option.WithUnloadedModules | MiniDump.Option.WithHandleData |
                                       MiniDump.Option.WithDataSegs | MiniDump.Option.WithCodeSegs,
                                       MiniDump.ExceptionInfo.Present);
                    }
                }
                catch (Exception e2)
                {
                    MainConsole.Instance.ErrorFormat("[CRASH LOGGER CRASHED]: {0}", e2);
                }
            }
        }
    }

    public static class MiniDump
    {
        // Taken almost verbatim from http://blog.kalmbach-software.de/2008/12/13/writing-minidumps-in-c/ 

        #region ExceptionInfo enum

        public enum ExceptionInfo
        {
            None,
            Present
        }

        #endregion

        #region Option enum

        [Flags]
        public enum Option : uint
        {
            // From dbghelp.h: 
            Normal = 0x00000000,
            WithDataSegs = 0x00000001,
            WithFullMemory = 0x00000002,
            WithHandleData = 0x00000004,
            FilterMemory = 0x00000008,
            ScanMemory = 0x00000010,
            WithUnloadedModules = 0x00000020,
            WithIndirectlyReferencedMemory = 0x00000040,
            FilterModulePaths = 0x00000080,
            WithProcessThreadData = 0x00000100,
            WithPrivateReadWriteMemory = 0x00000200,
            WithoutOptionalData = 0x00000400,
            WithFullMemoryInfo = 0x00000800,
            WithThreadInfo = 0x00001000,
            WithCodeSegs = 0x00002000,
            WithoutAuxiliaryState = 0x00004000,
            WithFullAuxiliaryState = 0x00008000,
            WithPrivateWriteCopyMemory = 0x00010000,
            IgnoreInaccessibleMemory = 0x00020000,
            ValidTypeFlags = 0x0003ffff,
        };

        #endregion

        //typedef struct _MINIDUMP_EXCEPTION_INFORMATION { 
        //    DWORD ThreadId; 
        //    PEXCEPTION_POINTERS ExceptionPointers; 
        //    BOOL ClientPointers; 
        //} MINIDUMP_EXCEPTION_INFORMATION, *PMINIDUMP_EXCEPTION_INFORMATION; 

        //BOOL 
        //WINAPI 
        //MiniDumpWriteDump( 
        //    __in HANDLE hProcess, 
        //    __in DWORD ProcessId, 
        //    __in HANDLE hFile, 
        //    __in MINIDUMP_TYPE DumpType, 
        //    __in_opt PMINIDUMP_EXCEPTION_INFORMATION ExceptionParam, 
        //    __in_opt PMINIDUMP_USER_STREAM_INFORMATION UserStreamParam, 
        //    __in_opt PMINIDUMP_CALLBACK_INFORMATION CallbackParam 
        //    ); 

        // Overload requiring MiniDumpExceptionInformation 
        [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, SafeHandle hFile, uint dumpType,
                                                     ref MiniDumpExceptionInformation expParam, IntPtr userStreamParam,
                                                     IntPtr callbackParam);

        // Overload supporting MiniDumpExceptionInformation == NULL 
        [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, SafeHandle hFile, uint dumpType,
                                                     IntPtr expParam, IntPtr userStreamParam, IntPtr callbackParam);

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId", ExactSpelling = true)]
        static extern uint GetCurrentThreadId();

        public static bool Write(SafeHandle fileHandle, Option options, ExceptionInfo exceptionInfo)
        {
            Process currentProcess = Process.GetCurrentProcess();
            IntPtr currentProcessHandle = currentProcess.Handle;
            uint currentProcessId = (uint) currentProcess.Id;
            MiniDumpExceptionInformation exp;
            exp.ThreadId = GetCurrentThreadId();
            exp.ClientPointers = false;
            exp.ExceptionPointers = IntPtr.Zero;
            if (exceptionInfo == ExceptionInfo.Present)
            {
                exp.ExceptionPointers = Marshal.GetExceptionPointers();
            }
            bool bRet = false;
            if (exp.ExceptionPointers == IntPtr.Zero)
            {
                bRet = MiniDumpWriteDump(currentProcessHandle, currentProcessId, fileHandle, (uint) options, IntPtr.Zero,
                                         IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                bRet = MiniDumpWriteDump(currentProcessHandle, currentProcessId, fileHandle, (uint) options, ref exp,
                                         IntPtr.Zero, IntPtr.Zero);
            }
            return bRet;
        }

        public static bool Write(SafeHandle fileHandle, Option dumpType)
        {
            return Write(fileHandle, dumpType, ExceptionInfo.None);
        }

        #region Nested type: MiniDumpExceptionInformation

        [StructLayout(LayoutKind.Sequential, Pack = 4)] // Pack=4 is important! So it works also for x64! 
        public struct MiniDumpExceptionInformation
        {
            public uint ThreadId;
            public IntPtr ExceptionPointers;
            [MarshalAs(UnmanagedType.Bool)] public bool ClientPointers;
        }

        #endregion
    }
}