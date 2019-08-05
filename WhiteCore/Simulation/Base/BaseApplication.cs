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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Nini.Config;
using WhiteCore.Framework.Configuration;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Simulation.Base
{
    /// <summary>
    ///     Starting class for the WhiteCore Server
    /// </summary>
    public static class BaseApplication
    {

        /// <summary>
        ///     Save Crashes in the bin/crashes folder.  Configurable with m_crashDir
        /// </summary>
        public static bool m_saveCrashDumps;

        /// <summary>
        ///     Loader of configuration files
        /// </summary>
        static readonly ConfigurationLoader m_configLoader = new ConfigurationLoader ();

        /// <summary>
        ///     Directory to save crash reports to.  Relative to bin/
        /// </summary>
        public static string m_crashDir = "crashes";

        static bool _IsHandlingException; // Make sure we don't go recursive on ourselves

        public static void BaseMain (string [] args, string defaultIniFile, ISimulationBase simBase)
        {
            // First line, hook the appdomain to the crash reporter
            AppDomain.CurrentDomain.UnhandledException +=
                CurrentDomain_UnhandledException;

            // Add the arguments supplied when running the application to the configuration
            ArgvConfigSource configSource = new ArgvConfigSource (args);

            // check configuration unless specifically requested not to
            if (!args.Contains ("-skipconfig"))
                Configure (false);

            // provide a startup configuration generation override option
            if (args.Contains ("-config"))
                Configure (true);

            // Increase the number of IOCP threads available. Mono defaults to a tragically low number
            int workerThreads, iocpThreads;
            ThreadPool.GetMaxThreads (out workerThreads, out iocpThreads);
            //MainConsole.Instance.InfoFormat("[WhiteCore Main]: Runtime gave us {0} worker threads and {1} IOCP threads", workerThreads, iocpThreads);
            if (workerThreads < 500 || iocpThreads < 1000) {
                workerThreads = 500;
                iocpThreads = 1000;
                //MainConsole.Instance.Info("[WhiteCore Main]: Bumping up to 500 worker threads and 1000 IOCP threads");
                ThreadPool.SetMaxThreads (workerThreads, iocpThreads);
            }

            BinMigratorService service = new BinMigratorService ();
            service.MigrateBin ();

            // Configure nIni aliases and localles
            Culture.SystemCultureInfo = CultureInfo.CurrentCulture;
            Culture.SetCurrentCulture ();
            configSource.Alias.AddAlias ("On", true);
            configSource.Alias.AddAlias ("Off", false);
            configSource.Alias.AddAlias ("True", true);
            configSource.Alias.AddAlias ("False", false);

            //Command line switches
            configSource.AddSwitch ("Startup", "inifile");
            configSource.AddSwitch ("Startup", "inimaster");
            configSource.AddSwitch ("Startup", "inigrid");
            configSource.AddSwitch ("Startup", "inisim");
            configSource.AddSwitch ("Startup", "inidirectory");
            configSource.AddSwitch ("Startup", "oldoptions");
            configSource.AddSwitch ("Startup", "inishowfileloading");
            configSource.AddSwitch ("Startup", "mainIniDirectory");
            configSource.AddSwitch ("Startup", "mainIniFileName");
            configSource.AddSwitch ("Startup", "secondaryIniFileName");
            configSource.AddSwitch ("Startup", "RegionDataFileName");
            configSource.AddSwitch ("Console", "Console");
            configSource.AddSwitch ("Console", "LogAppendName");
            configSource.AddSwitch ("Console", "LogPath");
            configSource.AddSwitch ("Network", "http_listener_port");

            IConfigSource m_configSource = Configuration (configSource, defaultIniFile);
            if (m_configSource == null)
                Environment.Exit (0);            // No configuration.. exit

            // Check if we're saving crashes
            m_saveCrashDumps = m_configSource.Configs ["Startup"].GetBoolean ("save_crashes", m_saveCrashDumps);

            // load Crash directory config
            m_crashDir = m_configSource.Configs ["Startup"].GetString ("crash_dir", m_crashDir);

            //Initialize the sim base now
            Startup (configSource, m_configSource, simBase.Copy (), args);
        }

        public static void Configure (bool requested)
        {
            string WhiteCore_ConfigDir = Constants.DEFAULT_CONFIG_DIR;
            bool isWhiteCoreExe = AppDomain.CurrentDomain.FriendlyName == "WhiteCore.exe" ||
                               AppDomain.CurrentDomain.FriendlyName == "WhiteCore.vshost.exe";
            
            bool existingConfig;
            string systype = "";
            var pmode = "";
            if (isWhiteCoreExe) {
                existingConfig = File.Exists (Path.Combine (WhiteCore_ConfigDir, "WhiteCore.ini"));
                systype = "a standalone system";
                pmode = "region";
            } else {
                existingConfig = File.Exists (Path.Combine (WhiteCore_ConfigDir, "WhiteCore.Server.ini"));
                systype = "the Grid server";
                pmode = "grid";
            }

            if (!requested) {
                string configrun = CheckConfigStamp (isWhiteCoreExe);
                if (configrun != "") {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    if (existingConfig) {
                        Console.WriteLine ("Using the existing configuration of " + configrun);
                        return;
                    }

                    // previously run but no config
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine ("\n************* WhiteCore-Sim re-configure. *************");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine ("It appears that the previous configuration for " + systype);
                    Console.WriteLine ("  has been removed.\nWhiteCore-Sim will rebuild your configuration.\n");
                    Console.ResetColor ();
                    requested = true;
                } else {
                    // not previously run
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine ("\n************* WhiteCore-Sim initial run. *************");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine ("\n   This appears to be your first time running WhiteCore-Sim.\n");
                    if (existingConfig) {
                        Console.WriteLine ("An existing configuration has been found.");
                        Console.WriteLine ("If you have already configured your *.ini files, or are using the pre-configured setup");
                        Console.WriteLine("  for " + systype + "  then please ignore this warning and press enter.");
                        Console.WriteLine ("If you wish to re-configure your settings then type 'yes' and ");
                        Console.WriteLine ("  WhiteCore-Sim will guide you through the configuration process.\n");
                        Console.ResetColor ();
                        Console.WriteLine ("");
                        var reconfig = "no";

                        reconfig = ReadLine ("Do you want to configure WhiteCore-Sim now?  (yes/no)", "no");
                        if (reconfig == "yes")
                            requested = true;
                        else {
                            WriteConfigStamp (pmode, "pre-configured", "preset", 0);
                            return;
                        }
                    } 

                    // no existing, force a reconfigure
                    requested = true;

                }
            }

            if (requested) {    // not required?? // || !existingConfig) {

                Console.ForegroundColor = ConsoleColor.Red;
                if (existingConfig)
                    Console.WriteLine ("\n\n************* WhiteCore-Sim Setup. *************");
                else
                    Console.WriteLine ("\n\n************* WhiteCore-Sim Configuration *************");
                    
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine ("\nIf you wish to configure your *.ini files manually, please press enter to abort!");
                Console.WriteLine ("Otherwise type 'yes' and WhiteCore-Sim will guide you through the configuration process.\n");
                Console.WriteLine ("Remember, these file names are Case Sensitive in Linux and Proper Cased.\n");
                if (isWhiteCoreExe) {
                    Console.WriteLine ("1. " + WhiteCore_ConfigDir + "/WhiteCore.ini\n\t\tand\n" +
                                      "2. " + WhiteCore_ConfigDir + "/Standalone/StandaloneCommon.ini \n\t\tor\n" +
                                      "3. " + WhiteCore_ConfigDir + "/GridRegion/GridCommon.ini\n");
                    Console.WriteLine ("The sim configuration is found in " + WhiteCore_ConfigDir + "/Sim/*");
                } else {
                    Console.WriteLine ("1. " + WhiteCore_ConfigDir + "/WhiteCoreServer.ini\n");
                    Console.WriteLine ("The grid configuration is found in " + WhiteCore_ConfigDir + "/Grid/*");
                }
                Console.WriteLine ("You should examine these files in great detail because only the basic system");
                Console.WriteLine ("  will load by default. WhiteCore-Sim can do a LOT more if you spend a little");
                Console.WriteLine ("  time going through these files.\n\n");

                // Make sure...
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine ("");
                Console.WriteLine (" ## WARNING - WARNING - WARNING - WARNING - WARNING ##\n");
                Console.WriteLine ("This will overwrite any existing configuration files!");
                Console.WriteLine ("It is strongly recommended that you save a backup copy");
                Console.WriteLine ("of any existing configuration files before proceeding!");
                Console.ResetColor ();
                Console.WriteLine ("");
                string resp = "no";
                resp = ReadLine ("Do you want to configure WhiteCore-Sim now?  (yes/no)", resp);

                if (resp != "yes") {
                    Console.WriteLine ("No configuration changes will take place!");
                    WriteConfigStamp (pmode, "pre-configured", "preset", 0);
                    return;
                }

                // Ok we have the 'go' to configure
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine ("\n     Just a moment... getting some details...\n");

                string cfgFolder = WhiteCore_ConfigDir + "/";           // Main Config folder >> "../Config" (default)

                string dbSource = "localhost";
                string dbPasswd = "whitecore";
                string dbSchema = "whitecore";
                string dbUser = "whitecore";
                string dbPort = "3306";
                string gridIPAddress = Utilities.GetExternalIp ();
                string regionIPAddress = gridIPAddress;
                bool isStandalone = true;
                string dbType = "1";
                string assetType = "1";
                string gridName = "WhiteCore-Sim";
                string welcomeMessage = "";
                string allowAnonLogin = "true";
                uint port = 8002;
                uint gridPort = 8002;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine ("====================================================================");
                Console.WriteLine ("======================= WhiteCore-Sim Configurator =================");
                Console.WriteLine ("====================================================================");
                Console.ResetColor ();

                if (isWhiteCoreExe) {
                    Console.WriteLine ("This installation is going to run in in which operation mode?");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("[1] Standalone (grid & region) \n[2] Grid connected region");
                    Console.ResetColor ();
                    isStandalone = ReadLine ("Choose 1 or 2", "1") == "1";

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("The domain name or IP address of this region server");
                    Console.WriteLine (" You may enter 'localip' to use the local IP address of your system");
                    Console.WriteLine (" The default is use your external IP address\n");
                    Console.ResetColor ();
                    regionIPAddress = ReadLine ("Region server address: ", regionIPAddress);

                    Console.WriteLine ("\nHttp Port for the region server");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("Default is 8002");
                    Console.ResetColor ();
                    port = uint.Parse (ReadLine ("Choose the port: ", "8002"));
                }

                if (isStandalone) {
                    Console.WriteLine ("Which database do you want to use?");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("[1] SQLite \n[2] MySQL");
                    Console.ResetColor ();
                    dbType = ReadLine ("Choose 1 or 2", dbType);
                    if (dbType == "2") {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine (
                            "\nNote: this setup does not automatically create a MySQL installation for you.\n" +
                            " This will configure the WhiteCore setting but you must install MySQL as well");
                        Console.ResetColor ();

                        dbSource = ReadLine ("MySQL database IP", dbSource);
                        dbPort = ReadLine ("MySQL database port (if not default)", dbPort);
                        dbSchema = ReadLine ("MySQL database name for your region", dbSchema);
                        dbUser = ReadLine ("MySQL database user account", dbUser);

                        Console.WriteLine ("MySQL database password for that account");
                        dbPasswd = Console.ReadLine ();

                        Console.WriteLine ("");
                    }
                }

                if (isStandalone) {
                    gridName = ReadLine ("Name of your WhiteCore-Sim server", gridName);

                    welcomeMessage = "Welcome to " + gridName + ", <USERNAME>!";
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine ("\nEnter your 'Welcome Message' that each user will see during login\n" +
                    "  (putting <USERNAME> into the welcome message will insert the user's name)");
                    Console.ResetColor ();
                    welcomeMessage = ReadLine ("Welcome Message", welcomeMessage);

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine ("\nAccounts can be created automatically when users log in for the first time.\n" +
                    "  (This means you don't have to create all accounts manually using the console or web interface)");
                    Console.ResetColor ();

                    allowAnonLogin = ReadLine ("Create accounts automatically?", allowAnonLogin);
                }

                if (!isStandalone) {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("The domain name or IP address of the Grid server you wish to connect to");
                    Console.WriteLine (" You may enter 'localip' if the Grid server is running on the same system\n");
                    Console.ResetColor ();
                    gridIPAddress = ReadLine ("Grid server address: ", gridIPAddress);
                }
            
                //Data.ini setup
                if (isStandalone) {
                    string cfgDataFolder = isWhiteCoreExe ? "Sim/" : "Grid/";

                    MakeSureExists (cfgFolder + cfgDataFolder + "Data/Data.ini");
                    var data_ini = new IniConfigSource (
                        cfgFolder + cfgDataFolder + "Data/Data.ini",
                        Nini.Ini.IniFileType.AuroraStyle);

                    IConfig conf = data_ini.AddConfig ("DataFile");

                    // DB include
                    if (dbType == "1")
                        conf.Set ("Include-SQLite", cfgDataFolder + "Data/SQLite.ini");
                    else
                        conf.Set ("Include-MySQL", cfgDataFolder + "Data/MySQL.ini");

                    if (isWhiteCoreExe)
                        conf.Set ("Include-FileBased", "Sim/Data/FileBased.ini");


                    // Asset services
                    conf = data_ini.AddConfig ("Handlers");

                    Console.WriteLine ("Which asset service do you want to use?");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("[1] File based\n[2] SQL");
                    Console.ResetColor ();
                    assetType = ReadLine ("Choose 1 or 2", assetType);
                    if (assetType == "2")
                        conf.Set ("AssetHandler", "AssetService");
                    else
                        conf.Set ("AssetHandler", "FileBasedAssetService");
                    conf.Set ("AssetHandlerUseCache", false);

                    conf = data_ini.AddConfig ("WhiteCoreConnectors");
                    conf.Set ("ValidateTables", true);


                    data_ini.Save ();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine ("Your Data.ini has been successfully configured");
                    Console.ResetColor ();

                    // MySql setup 
                    if (dbType == "2") {
                        MakeSureExists (cfgFolder + cfgDataFolder + "Data/MySQL.ini");
                        var mysql_ini = new IniConfigSource (
                            cfgFolder + cfgDataFolder + "Data/MySQL.ini",
                            Nini.Ini.IniFileType.AuroraStyle);

                        //var mysql_ini_example = new IniConfigSource (
                        //cfgFolder + cfgDataFolder + "Data/MySQL.ini.example",
                        //     Nini.Ini.IniFileType.AuroraStyle);
                       var mysql_ini_example = new IniConfigSource (
                            cfgFolder + "Templates/MySQL.ini.example",
                            Nini.Ini.IniFileType.AuroraStyle);

                        foreach (IConfig config in mysql_ini_example.Configs) {
                            IConfig newConfig = mysql_ini.AddConfig (config.Name);
                            foreach (string key in config.GetKeys ()) {
                                if (key == "ConnectionString")
                                    newConfig.Set (key,
                                                  string.Format (
                                            "\"Data Source={0};Port={1};Database={2};User ID={3};Password={4};Charset=utf8;\"",
                                            dbSource, dbPort, dbSchema, dbUser, dbPasswd));
                                else
                                    newConfig.Set (key, config.Get (key));
                            }
                        }

                        mysql_ini.Save ();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine ("Your MySQL.ini has been successfully configured");
                        Console.ResetColor ();
                    }
                }

                // Region server
                if (isWhiteCoreExe) {
                    MakeSureExists (cfgFolder + "WhiteCore.ini");
                    var whitecore_ini = new IniConfigSource (
                        cfgFolder + "WhiteCore.ini",
                        Nini.Ini.IniFileType.AuroraStyle);
                    var whitecore_ini_example = new IniConfigSource (
                        cfgFolder + "Templates/WhiteCore.ini.example",
                        Nini.Ini.IniFileType.AuroraStyle);

                    bool setIp = false;

                    IConfig conf = whitecore_ini.AddConfig ("Architecture");
                    if (isStandalone)
                        conf.Set ("Include-Standalone", "Standalone/StandaloneCommon.ini");
                    else
                        conf.Set ("Include-Grid", "GridRegion/GridCommon.ini");

                    foreach (IConfig config in whitecore_ini_example.Configs) {
                        IConfig newConfig = whitecore_ini.AddConfig (config.Name);
                        foreach (string key in config.GetKeys ()) {
                            if (key == "http_listener_port")
                                newConfig.Set (key, port);
                            else if (key == "HostName") {
                                setIp = true;
                                newConfig.Set (key, regionIPAddress);
                            } else
                                newConfig.Set (key, config.Get (key));
                        }

                        if ((config.Name == "Network") & !setIp) {
                            setIp = true;
                            newConfig.Set ("HostName", regionIPAddress);
                        }
                    }

                    //conf.Set ("Include-Includes", "Sim/Includes.ini");

                    whitecore_ini.Save ();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine ("Your WhiteCore.ini has been successfully configured");
                    Console.ResetColor ();

                    /* 20190722 -greythane- this config merged into the main whitecore.ini
                    MakeSureExists (cfgFolder + "Sim/Main.ini");
                    var main_ini = new IniConfigSource (
                        cfgFolder + "Sim/Main.ini",
                        Nini.Ini.IniFileType.AuroraStyle);

                    IConfig conf = main_ini.AddConfig ("Architecture");
                    if (isStandalone)
                        conf.Set ("Include-Standalone", "Sim/Standalone/StandaloneCommon.ini");
                    else
                        conf.Set ("Include-Grid", "Sim/GridRegio/GridCommon.ini");
                    conf.Set ("Include-Includes", "Sim/Includes.ini");

                    main_ini.Save ();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine ("Your Main.ini has been successfully configured");
                    Console.ResetColor ();
                    */

                    if (isStandalone) {
                        MakeSureExists (cfgFolder + "Standalone/StandaloneCommon.ini");
                        var standalone_ini = new IniConfigSource (
                            cfgFolder + "Standalone/StandaloneCommon.ini",
                            Nini.Ini.IniFileType.AuroraStyle);
                        //var standalone_ini_example = new IniConfigSource (
                        //    cfgFolder + "Sim/Standalone/StandaloneCommon.ini.example",
                        //    Nini.Ini.IniFileType.AuroraStyle);
                        var standalone_ini_example = new IniConfigSource (
                            cfgFolder + "Templates/StandaloneCommon.ini.example",
                            Nini.Ini.IniFileType.AuroraStyle);

                        foreach (IConfig config in standalone_ini_example.Configs) {
                            IConfig newConfig = standalone_ini.AddConfig (config.Name);
                            if (newConfig.Name == "GridInfoService") {
                                newConfig.Set ("GridInfoInHandlerPort", 0);
                                newConfig.Set ("login", "http://" + gridIPAddress + ":" + port + "/");
                                newConfig.Set ("gridname", gridName);
                                newConfig.Set ("gridnick", gridName);
                            } else {
                                foreach (string key in config.GetKeys ()) {
                                    if (key == "WelcomeMessage")
                                        newConfig.Set (key, welcomeMessage);
                                    else if (key == "AllowAnonymousLogin")
                                        newConfig.Set (key, allowAnonLogin);
                                    else
                                        newConfig.Set (key, config.Get (key));
                                }
                            }
                        }

                        standalone_ini.Save ();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine ("Your StandaloneCommon.ini has been successfully configured");
                        Console.ResetColor ();
                    } else {
                        MakeSureExists (cfgFolder + "GridRegion/GridCommon.ini");
                        var grid_ini = new IniConfigSource (
                            cfgFolder + "GridRegion/GridCommon.ini",
                            Nini.Ini.IniFileType.AuroraStyle);

                        conf = grid_ini.AddConfig ("Includes");
                        conf.Set ("Include-Grid", "GridRegion/Grid.ini");
                        conf = grid_ini.AddConfig ("Configuration");
                        conf.Set ("GridServerURI", "http://" + gridIPAddress + ":8012/grid/");
                        //Lets tell the Configurator to create the missing .ini entrys from the .example file
                        conf.Set("SendGridInfoToViewerOnLogin", "true");
                        conf.Set("CurrencySymbol", "\"WC$\"");
                        conf.Set(";;DISABLED BY DEFAULT GENERATED BY CONFIGURATOR", "");
                        conf.Set(";;welcome", "http://" + gridIPAddress + "/welcome");
                        conf.Set(";;economy", "http://" + gridIPAddress + ":8009/");
                        conf.Set(";;about", "http://" + gridIPAddress + "/about");
                        conf.Set(";;register", "http://" + gridIPAddress + "/register");
                        conf.Set(";;help", "http://" + gridIPAddress + "/help");
                        conf.Set(";;forgottenpassword", "http://" + gridIPAddress + "/password");
                        conf.Set(";;map", "");
                        conf.Set(";;webprofile", "");
                        conf.Set(";;search", "");
                        conf.Set(";;destination", "");
                        conf.Set(";;marketplace", "");
                        conf.Set(";;tutorial", "");
                        conf.Set(";;message", "\"this is a test message\"");
                        conf.Set(";;snapshotconfig", "");
                        conf.Set(";;RealCurrencySymbol", "\"$\"");
                        conf.Set(";;MaxGroups", "50");
                        grid_ini.Save ();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine ("Your Grid.ini has been successfully configured");
                        Console.ResetColor ();
                        Console.WriteLine ("");
                    }
                }

                // Grid server
                if (!isWhiteCoreExe) {
                    MakeSureExists (cfgFolder + "WhiteCore.Server.ini");
                    var whitecore_ini = new IniConfigSource (
                        cfgFolder + "WhiteCore.Server.ini",
                        Nini.Ini.IniFileType.AuroraStyle);
                    //var whitecore_ini_example = new IniConfigSource (
                    //    cfgFolder + "WhiteCore.Server.ini.example",
                    //    Nini.Ini.IniFileType.AuroraStyle);
                    var whitecore_ini_example = new IniConfigSource (
                    	cfgFolder + "Templates/WhiteCore.Server.ini.example",
                    	Nini.Ini.IniFileType.AuroraStyle);

                    gridIPAddress =
                        ReadLine ("\nThe domain name or IP address of the grid server", gridIPAddress);
                    bool ipSet = false;

                    foreach (IConfig config in whitecore_ini_example.Configs) {
                        IConfig newConfig = whitecore_ini.AddConfig (config.Name);
                        foreach (string key in config.GetKeys ()) {
                            if (key == "HostName") {
                                ipSet = true;
                                newConfig.Set (key, gridIPAddress);
                            } else
                                newConfig.Set (key, config.Get (key));
                        }

                        if ((config.Name == "Network") & !ipSet) {
                            ipSet = true;
                            newConfig.Set ("HostName", gridIPAddress);
                        }
                    }

                    whitecore_ini.Save ();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine ("Your WhiteCore.Server.ini has been successfully configured");
                    Console.ResetColor ();

                    MakeSureExists (cfgFolder + "Grid/Login.ini");
                    var login_ini = new IniConfigSource (
                        cfgFolder + "Grid/Login.ini",
                        Nini.Ini.IniFileType.AuroraStyle);
                    //var login_ini_example = new IniConfigSource (
                    //    cfgFolder + "Grid/ServerConfiguration/Login.ini.example",
                    //    Nini.Ini.IniFileType.AuroraStyle);
                    var login_ini_example = new IniConfigSource (
                    	cfgFolder + "Templates/Login.ini.example",
                    	Nini.Ini.IniFileType.AuroraStyle);

                    Console.WriteLine ("\nHttp Port for the grid server");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("Default is 8002");
                    Console.ResetColor ();
                    gridPort = uint.Parse (ReadLine ("Choose the port", "8002"));

                    foreach (IConfig config in login_ini_example.Configs) {
                        IConfig newConfig = login_ini.AddConfig (config.Name);
                        foreach (string key in config.GetKeys ()) {
                            if (key == "WelcomeMessage")
                                newConfig.Set (key, welcomeMessage);
                            else if (key == "AllowAnonymousLogin")
                                newConfig.Set (key, allowAnonLogin);
                            else
                                newConfig.Set (key, config.Get (key));
                        }
                    }

                    login_ini.Save ();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine ("Your Login.ini has been successfully configured");
                    Console.ResetColor ();

                    MakeSureExists (cfgFolder + "Grid/GridInfoService.ini");
                    var grid_info_ini = new IniConfigSource (
                        cfgFolder + "Grid/GridInfoService.ini",
                        Nini.Ini.IniFileType.AuroraStyle);

                    IConfig conf = grid_info_ini.AddConfig ("GridInfoService");
                    conf.Set ("GridInfoInHandlerPort", gridPort);
                    conf.Set ("login", "http://" + gridIPAddress + ":" + gridPort + "/");
                    conf.Set ("gridname", gridName);
                    conf.Set ("gridnick", gridName);

                    grid_info_ini.Save ();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine ("Your GridInfoService.ini has been successfully configured");
                    Console.ResetColor ();
                    Console.WriteLine ("");
                }

                Console.WriteLine ("\n====================================================================\n");
                Console.ResetColor ();
                Console.WriteLine ("Your grid name is ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine (gridName);
                Console.ResetColor ();
                if (isStandalone) {
                    Console.WriteLine ("\nYour loginuri is ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("http://" + (isWhiteCoreExe ? regionIPAddress : gridIPAddress) + ":" + (isWhiteCoreExe ? port : gridPort) + "/");
                    Console.ResetColor ();

                    if (isWhiteCoreExe)
                        WriteConfigStamp ("region", gridName, regionIPAddress, port);
                    else
                        WriteConfigStamp ("grid", gridName, gridIPAddress, gridPort);

                } else {
                    Console.WriteLine ("\nConnected Grid URL: ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine ("http://" + gridIPAddress + ":" + gridPort + "/");
                    Console.ResetColor ();

                    WriteConfigStamp ("regiongrid", gridName, gridIPAddress, gridPort);
                }


                Console.WriteLine ("\n====================================================================\n");
                Console.WriteLine (
                    "To re-run this configurator, enter \"run configurator\" into the console.");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine (" >> WhiteCore will now exit. Restart to use your new configuration. <<");
                Console.ResetColor ();
                Console.WriteLine ("");
                ReadLine ("Press <enter> to exit for the restart","");
                Environment.Exit (0);            // Exit for restart

            }
        }

        static void MakeSureExists (string file)
        {
            if (File.Exists (file))
                File.Delete (file);
            File.Create (file).Close ();
        }

        static string CheckConfigStamp (bool region)
        {
            // check if a config info file exists in the current (bin) directory
            string cfile;
            string msg = "";

            if (region) {
                cfile = ".whitecore.config";
            } else {
                cfile = ".whitecoregrid.config";
            }


            // get some details
            FileInfo file = new FileInfo (Environment.CurrentDirectory + "/" + @cfile);
            if (!file.Exists)           // no configuration done yet
                return msg;

            file.Attributes &= ~FileAttributes.Hidden;

            using (var reader = new StreamReader (cfile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith ("Date", StringComparison.Ordinal)) {
                        msg = line;
                        break;
                    }
                }
            }

            file.Attributes |= FileAttributes.Hidden;
            return msg;
        }
            

        static void WriteConfigStamp (string mode, string name, string ipaddr, uint port)
        {
            // write a config info file to the current (bin) directory
            string cfile;
            string configmsg = "http://" + ipaddr + ":";
            if (port == 0)
                configmsg = configmsg + "preset";
            else
                configmsg = configmsg + port.ToString();
                            

            if (mode == "grid") {
                cfile = ".whitecoregrid.config";
                configmsg = "Grid URI - " + configmsg;

            } else {
                cfile = ".whitecore.config";
                if (mode == "region")
                    configmsg = "Login URI - " + configmsg;
                else configmsg = "Connected Grid URL - " + configmsg;
            }

            using (FileStream fs = new FileStream (cfile, FileMode.OpenOrCreate)) {
                using (TextWriter tw = new StreamWriter (fs)) {
                    // Write selected details...
                    tw.WriteLine ("Date   : " + DateTime.Now.ToString());
                    tw.WriteLine ("Mode   : " + mode);
                    tw.WriteLine ("Name   : " + name);
                    tw.WriteLine ("Config : " + configmsg);

                    // Flush the writer in order to get a correct stream position for truncating
                    tw.Flush ();
                    // Set the stream length to the current position in order to truncate leftover text
                    fs.SetLength (fs.Position);
                }
            }
        }

        static string ReadLine (string log, string defaultReturn)
        {
            Console.WriteLine (log + ": [" + defaultReturn + "]");
            string mode = Console.ReadLine ();
            if (mode == string.Empty)
                mode = defaultReturn;
            if (mode != null)
                mode = mode.Trim ();
            return mode;
        }

        public static void Startup (IConfigSource originalConfigSource, IConfigSource configSource,
                                   ISimulationBase simBase, string [] cmdParameters)
        {
            //Get it ready to run
            simBase.Initialize (originalConfigSource, configSource, cmdParameters, m_configLoader);
            try {
                //Start it. This starts ALL modules and completes the startup of the application
                simBase.Startup ();
                //Run the console now that we are done
                simBase.Run ();
            } catch (Exception ex) {
                UnhandledException (false, ex);
                //Just clean it out as good as we can
                simBase.Shutdown (false);
            }
        }

        /// <summary>
        ///     Load the configuration for the Application
        /// </summary>
        /// <param name="configSource"></param>
        /// <param name="defaultIniFile"></param>
        /// <returns></returns>
        static IConfigSource Configuration (IConfigSource configSource, string defaultIniFile)
        {
            if (defaultIniFile != "")
                m_configLoader.defaultIniFile = defaultIniFile;
            return m_configLoader.LoadConfigSettings (configSource);
        }

        /// <summary>
        ///     Global exception handler -- all unhandled exceptions end up here :)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
        {
            if (_IsHandlingException)
                return;

            _IsHandlingException = true;
            Exception ex = (Exception)e.ExceptionObject;

            UnhandledException (e.IsTerminating, ex);

            _IsHandlingException = false;
        }

        static void UnhandledException (bool isTerminating, Exception ex)
        {
            string msg = string.Empty;
            msg += "\r\n";
            msg += "Application exception detected" + "\r\n";
            msg += "\r\n";

            msg += "Exception: " + ex + "\r\n";
            if (ex.InnerException != null) {
                msg += "InnerException: " + ex.InnerException + "\r\n";
            }

            msg += "\r\n";
            msg += "Application is terminating: " + isTerminating.ToString (CultureInfo.InvariantCulture) + "\r\n";

            MainConsole.Instance.ErrorFormat ("[Application]: {0}", msg);
            if (Util.IsWindows ())
                HandleCrashException (msg, ex);
        }

        /// <summary>
        ///     Deal with sending the error to the error reporting service and saving the dump to the hard-drive if needed
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="ex"></param>
        public static void HandleCrashException (string msg, Exception ex)
        {
            //if (m_saveCrashDumps && Environment.OSVersion.Platform == PlatformID.Win32NT)
            // 20160323 -greythane - not sure why this will not work on *nix as well?
            // 20160408 -EmperorStarfinder - This appears to not work on either Windows or Nix.
            // Maybe consider removing the saveCrashDumps as it appears it is writing it to the
            // logs for WhiteCore.Server and WhiteCore consoles already?
            if (m_saveCrashDumps) {
                // Log exception to disk
                try {
                    if (!Directory.Exists (m_crashDir))
                        Directory.CreateDirectory (m_crashDir);

                    string log = Path.Combine (m_crashDir, Util.GetUniqueFilename ("CrashDump" +
                                                                                 DateTime.Now.Day + DateTime.Now.Month +
                                                                                 DateTime.Now.Year + ".mdmp"));
                    using (FileStream fs = new FileStream (log, FileMode.Create, FileAccess.ReadWrite, FileShare.Write)) {
                        MiniDump.Write (fs.SafeFileHandle,
                                       MiniDump.Option.WithThreadInfo | MiniDump.Option.WithProcessThreadData |
                                       MiniDump.Option.WithUnloadedModules | MiniDump.Option.WithHandleData |
                                       MiniDump.Option.WithDataSegs | MiniDump.Option.WithCodeSegs,
                                       MiniDump.ExceptionInfo.Present);
                    }
                } catch (Exception e2) {
                    MainConsole.Instance.ErrorFormat ("[Crash logger crashed]: {0}", e2);
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
        [DllImport ("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool MiniDumpWriteDump (IntPtr hProcess, uint processId, SafeHandle hFile, uint dumpType,
                                                     ref MiniDumpExceptionInformation expParam, IntPtr userStreamParam,
                                                     IntPtr callbackParam);

        // Overload supporting MiniDumpExceptionInformation == NULL 
        [DllImport ("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool MiniDumpWriteDump (IntPtr hProcess, uint processId, SafeHandle hFile, uint dumpType,
                                                     IntPtr expParam, IntPtr userStreamParam, IntPtr callbackParam);

        [DllImport ("kernel32.dll", EntryPoint = "GetCurrentThreadId", ExactSpelling = true)]
        static extern uint GetCurrentThreadId ();

        public static bool Write (SafeHandle fileHandle, Option options, ExceptionInfo exceptionInfo)
        {
            Process currentProcess = Process.GetCurrentProcess ();
            IntPtr currentProcessHandle = currentProcess.Handle;
            uint currentProcessId = (uint)currentProcess.Id;
            MiniDumpExceptionInformation exp;
            exp.ThreadId = GetCurrentThreadId ();
            exp.ClientPointers = false;
            exp.ExceptionPointers = IntPtr.Zero;
            if (exceptionInfo == ExceptionInfo.Present) {
                exp.ExceptionPointers = Marshal.GetExceptionPointers ();
            }

            bool bRet;
            if (exp.ExceptionPointers == IntPtr.Zero) {
                bRet = MiniDumpWriteDump (currentProcessHandle, currentProcessId, fileHandle, (uint)options, IntPtr.Zero,
                                         IntPtr.Zero, IntPtr.Zero);
            } else {
                bRet = MiniDumpWriteDump (currentProcessHandle, currentProcessId, fileHandle, (uint)options, ref exp,
                                         IntPtr.Zero, IntPtr.Zero);
            }
            return bRet;
        }

        public static bool Write (SafeHandle fileHandle, Option dumpType)
        {
            return Write (fileHandle, dumpType, ExceptionInfo.None);
        }

        #region Nested type: MiniDumpExceptionInformation

        [StructLayout (LayoutKind.Sequential, Pack = 4)] // Pack=4 is important! So it works also for x64! 
        public struct MiniDumpExceptionInformation
        {
            public uint ThreadId;
            public IntPtr ExceptionPointers;
            [MarshalAs (UnmanagedType.Bool)]
            public bool ClientPointers;
        }

        #endregion
    }
}
