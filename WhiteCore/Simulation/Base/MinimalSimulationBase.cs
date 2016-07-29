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
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.Configuration;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Other;
using WhiteCore.Framework.Utilities;
using WhiteCore.Services.DataService;

namespace WhiteCore.Simulation.Base
{
    public class MinimalSimulationBase : ISimulationBase
    {
        protected string m_startupCommandsFile;
        protected string m_shutdownCommandsFile;
        protected string m_TimerScriptFileName = "disabled";
        protected int m_TimerScriptTime = 20;
        protected IHttpServer m_BaseHTTPServer;
        protected Timer m_TimerScriptTimer;
        protected ConfigurationLoader m_configurationLoader;

        /// <value>
        ///     The config information passed into the WhiteCore server.
        /// </value>
        protected IConfigSource m_config;

        protected IConfigSource m_original_config;

        public IConfigSource ConfigSource
        {
            get { return m_config; }
            set { m_config = value; }
        }

        /// <summary>
        ///     Server version information.  Usually VersionInfo + information about git commit, operating system, etc.
        /// </summary>
        protected string m_version;

        public string Version
        {
            get { return m_version; }
        }

        /// <summary>
        /// Is this instance a grid server.
        /// </summary>
        /// <value>true</value>
        /// <c>false</c>
        public bool IsGridServer
        {
            get { return m_configurationLoader.IsGridServer; }
        }

        protected string m_defaultDataPath = Constants.DEFAULT_DATA_DIR;
        public string DefaultDataPath
        { 
            get { return m_defaultDataPath;}
            set { m_defaultDataPath = value;}
        }

        protected int m_mapcenter_x = Constants.DEFAULT_REGIONSTART_X;
        public int MapCenterX {
            get { return m_mapcenter_x; }
            set { m_mapcenter_x = value; }
        }

        protected int m_mapcenter_y = Constants.DEFAULT_REGIONSTART_Y;
        public int MapCenterY {
            get { return m_mapcenter_y; }
            set { m_mapcenter_y = value; }
        }

        protected IRegistryCore m_applicationRegistry = new RegistryCore();

        public IRegistryCore ApplicationRegistry
        {
            get { return m_applicationRegistry; }
        }

        protected WhiteCoreEventManager m_eventManager = new WhiteCoreEventManager();

        public WhiteCoreEventManager EventManager
        {
            get { return m_eventManager; }
        }

        /// <summary>
        ///     Time at which this server was started
        /// </summary>
        protected DateTime m_StartupTime;

        public DateTime StartupTime
        {
            get { return m_StartupTime; }
        }

        public IHttpServer HttpServer
        {
            get { return m_BaseHTTPServer; }
        }

        protected Dictionary<uint, IHttpServer> m_Servers =
            new Dictionary<uint, IHttpServer>();

        protected uint m_Port;

        public uint DefaultPort
        {
            get { return m_Port; }
        }

        protected string[] m_commandLineParameters = null;

        public string[] CommandLineParameters
        {
            get { return m_commandLineParameters; }
        }

        protected string m_pidFile = string.Empty;

        protected string m_consolePrompt = "";
        protected List<Type> m_dataPlugins;
        protected List<Type> m_servicePlugins;

        public MinimalSimulationBase(string consolePrompt, List<Type> dataPlugins, List<Type> servicePlugins)
        {
            m_consolePrompt = consolePrompt;
            m_dataPlugins = dataPlugins;
            m_servicePlugins = servicePlugins;
        }

        /// <summary>
        ///     Do the initial setup for the application
        /// </summary>
        /// <param name="originalConfig"></param>
        /// <param name="configSource"></param>
        /// <param name="cmdParams"></param>
        /// <param name="configLoader"></param>
        public virtual void Initialize(IConfigSource originalConfig, IConfigSource configSource, string[] cmdParams,
                                       ConfigurationLoader configLoader)
        {
            m_commandLineParameters = cmdParams;
            m_StartupTime = DateTime.Now;
            m_version = VersionInfo.Version;
            m_original_config = originalConfig;
            m_config = configSource;
            m_configurationLoader = configLoader;

            // This thread will go on to become the console listening thread
            System.Threading.Thread.CurrentThread.Name = "ConsoleThread";

            //Register the interface
            ApplicationRegistry.RegisterModuleInterface<ISimulationBase>(this);

            Configuration(configSource);

            InitializeModules();

            RegisterConsoleCommands();
        }

        /// <summary>
        ///     Read the configuration
        /// </summary>
        /// <param name="configSource"></param>
        public virtual void Configuration(IConfigSource configSource)
        {
            IConfig startupConfig = m_config.Configs["Startup"];

            int stpMinThreads = 15;
            int stpMaxThreads = 300;

            if (startupConfig != null)
            {
                m_defaultDataPath = startupConfig.GetString("DataDirectory", Constants.DEFAULT_DATA_DIR);
                if (m_defaultDataPath == "")
                    m_defaultDataPath = Constants.DEFAULT_DATA_DIR;
                
                m_startupCommandsFile = startupConfig.GetString("startup_console_commands_file", "startup_commands.txt");
                m_shutdownCommandsFile = startupConfig.GetString("shutdown_console_commands_file",
                                                                 "shutdown_commands.txt");

                m_TimerScriptFileName = startupConfig.GetString("timer_Script", "disabled");
                m_TimerScriptTime = startupConfig.GetInt("timer_time", m_TimerScriptTime);

                string pidFile = startupConfig.GetString("PIDFile", string.Empty);
                if (pidFile != string.Empty)
                    CreatePIDFile(pidFile);
            }

            IConfig SystemConfig = m_config.Configs["System"];
            if (SystemConfig != null)
            {
                string asyncCallMethodStr = SystemConfig.GetString("AsyncCallMethod", string.Empty);
                FireAndForgetMethod asyncCallMethod;
                if (!string.IsNullOrEmpty(asyncCallMethodStr) &&
                    Utils.EnumTryParse(asyncCallMethodStr, out asyncCallMethod))
                    Util.FireAndForgetMethod = asyncCallMethod;

                stpMinThreads = SystemConfig.GetInt("MinPoolThreads", stpMinThreads);
                stpMaxThreads = SystemConfig.GetInt("MaxPoolThreads", stpMaxThreads);

                if (stpMinThreads < 2)
                    stpMinThreads = 2;
                if (stpMaxThreads < 2)
                    stpMaxThreads = 2;
                if (stpMinThreads > stpMaxThreads)
                    stpMinThreads = stpMaxThreads;           
            }

            var mapConfig = m_config.Configs ["WebInterface"];
            if (mapConfig != null) {
                m_mapcenter_x = mapConfig.GetInt ("mapcenter_x", m_mapcenter_x);
                m_mapcenter_y = mapConfig.GetInt ("mapcenter_y", m_mapcenter_y);
            }

            if (Util.FireAndForgetMethod == FireAndForgetMethod.SmartThreadPool)
                Util.InitThreadPool(stpMinThreads, stpMaxThreads);

            //Set up console forcefully
            Services.BaseService consoleService = new Services.BaseService();
            consoleService.PreStartup(this);

            //Fix the default prompt
             MainConsole.Instance.DefaultPrompt = m_consolePrompt;
        }

        /// <summary>
        ///     Performs initialization of the application, such as loading the HTTP server and modules
        /// </summary>
        public virtual void Startup()
        {
            PrintFileToConsole (Path.Combine(DefaultDataPath,"startuplogo.txt"));

            MainConsole.Instance.InfoFormat ("[Mini WhiteCore-Sim]: Starting Mini WhiteCore-Sim ({0})...",
                                             (IntPtr.Size == 4 ? "x86" : "x64"));
            MainConsole.Instance.Info ("[Mini WhiteCore-Sim]: Version : " + Version + "\n");
            MainConsole.Instance.Info ("[Mini WhiteCore-Sim]: Git Base: " + VersionInfo.GitVersion + "\n");
            MainConsole.Instance.Info ("[Mini WhiteCore-Sim]: Startup completed in " +
                                       (DateTime.Now - StartupTime).TotalSeconds);
        }

        public virtual ISimulationBase Copy()
        {
            return new MinimalSimulationBase(m_consolePrompt, m_dataPlugins, m_servicePlugins);
        }

        /// <summary>
        ///     Run the console now that we are all done with startup
        /// </summary>
        public virtual void Run()
        {
            //Start the prompt
            MainConsole.Instance.ReadConsole();
        }

        /// <summary>
        ///     Get an HTTPServer on the given port. It will create one if one does not exist
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public IHttpServer GetHttpServer(uint port)
        {
            if ((port == m_Port || port == 0) && HttpServer != null)
                return HttpServer;

            bool useHTTPS = m_config.Configs["Network"].GetBoolean("use_https", false);
            IHttpServer server;
            if (m_Servers.TryGetValue(port, out server) && server.Secure == useHTTPS)
                return server;

            uint threadCount = m_config.Configs["Network"].GetUInt("HttpThreadCount", 5);

            // find out where we live
            string hostName;

            // been here before?
            if (Utilities.HostName == "")
            {
                hostName = m_config.Configs ["Network"].GetString ("HostName", "0.0.0.0");

                if ((hostName == "") || (hostName == "0.0.0.0"))
                {
                    MainConsole.Instance.Info ("[Network]: Retrieving the external IP address");
                    hostName = "http" + (useHTTPS ? "s" : "") + "://" + Utilities.GetExternalIp ();
                }
            
                //Clean it up a bit
                if (hostName.StartsWith ("http://", StringComparison.OrdinalIgnoreCase) || hostName.StartsWith ("https://", StringComparison.OrdinalIgnoreCase))
                    hostName = hostName.Replace ("https://", "").Replace ("http://", "");
                if (hostName.EndsWith ("/", StringComparison.Ordinal))
                    hostName = hostName.Remove (hostName.Length - 1, 1);
                
                // save this for posterity in case it is needed
                MainConsole.Instance.Info ("[Network]: Network IP address has been set to " + hostName);
                Utilities.HostName = hostName;
            } else
                hostName = Utilities.HostName;

            server = new BaseHttpServer(port, hostName, useHTTPS, threadCount);

            try
            {
                server.Start();
            }
            catch (Exception)
            {
                //Remove the server from the list
                m_Servers.Remove(port);
                //Then pass the exception upwards
                throw;
            }
            if (m_Servers.Count == 0)
                MainServer.Instance = server;
            return (m_Servers[port] = server);
        }

        public virtual void InitializeModules()
        {
            LocalDataService lds = new LocalDataService();
            lds.Initialise(ConfigSource, ApplicationRegistry, m_dataPlugins);

            List<dynamic> modules = new List<dynamic>();
            foreach (Type t in m_servicePlugins)
            {
                var mods = WhiteCoreModuleLoader.PickupModules(t);
                modules.AddRange(mods);
            }

            foreach (dynamic service in modules)
            {
                if (!(service is IService)) continue;
                ((IService)service).Initialize(ConfigSource, ApplicationRegistry);
            }
            foreach (dynamic service in modules)
            {
                if (!(service is IService)) continue;
                ((IService) service).Start(ConfigSource, ApplicationRegistry);
            }
            foreach (dynamic service in modules)
            {
                if (!(service is IService)) continue;
                ((IService)service).FinishedStartup();
            }
        }

        /// <summary>
        ///     Close all the Application Plugins
        /// </summary>
        public virtual void CloseModules()
        {
        }

        /// <summary>
        ///     Run the commands given now that startup is complete
        /// </summary>
        public void RunStartupCommands()
        {
            //Draw the file on the console
            PrintFileToConsole(m_startupCommandsFile);
            //Run Startup Commands
            if (!string.IsNullOrEmpty(m_startupCommandsFile))
                RunCommandScript(m_startupCommandsFile);

            // Start timer script (run a script every xx seconds)
            if (m_TimerScriptFileName != "disabled")
            {
                m_TimerScriptTimer = new Timer {Enabled = true, Interval = m_TimerScriptTime*60*1000};
                m_TimerScriptTimer.Elapsed += RunAutoTimerScript;
            }
        }

        /// <summary>
        ///     Opens a file and uses it as input to the console command parser.
        /// </summary>
        /// <param name="fileName">name of file to use as input to the console</param>
        void PrintFileToConsole(string fileName)
        {
            if (File.Exists(fileName))
            {
                StreamReader readFile = File.OpenText(fileName);
                string currentLine;
                while ((currentLine = readFile.ReadLine()) != null)
                {
                    MainConsole.Instance.Info("[!]" + currentLine);
                }
            }
        }

        /// <summary>
        ///     Timer to run a specific text file as console commands.
        ///     Configured in in the main .ini file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void RunAutoTimerScript(object sender, EventArgs e)
        {
            RunCommandScript(m_TimerScriptFileName);
        }

        #region Console Commands

        /// <summary>
        ///     Register standard set of region console commands
        /// </summary>
        public virtual void RegisterConsoleCommands()
        {
            if (MainConsole.Instance == null)
                return;
            MainConsole.Instance.Commands.AddCommand(
                "quit", 
                "quit", 
                "Quit the application", 
                HandleQuit, false, true);
            
            MainConsole.Instance.Commands.AddCommand(
                "shutdown",
                "shutdown", 
                "Quit the application", 
                HandleQuit, false, true);
            
            MainConsole.Instance.Commands.AddCommand(
                "show info",
                "show info",
                "Show server information (e.g. startup path)", 
                HandleShowInfo, false, true);
            
            MainConsole.Instance.Commands.AddCommand(
                "show version",
                "show version", 
                "Show server version",
                HandleShowVersion, false, true);
            
            MainConsole.Instance.Commands.AddCommand(
                "reload config",
                "reload config", 
                "Reloads .ini file configuration",
                HandleConfigRefresh, false, true);
            
            MainConsole.Instance.Commands.AddCommand(
                "set timer script interval",
                "set timer script interval",
                "Set the interval for the timer script (in minutes).",
                HandleTimerScriptTime, false, true);
            
            MainConsole.Instance.Commands.AddCommand(
                "force GC",
                "force GC", 
                "Forces garbage collection.", 
                HandleForceGC, false, true);
            
            MainConsole.Instance.Commands.AddCommand(
                "run configurator",
                "run configurator", 
                "Runs WhiteCore.Configurator.",
                runConfig, false, true);
        }

        void HandleQuit(IScene scene, string[] args)
        {
            var ok = MainConsole.Instance.Prompt ("[Console]: Shutdown the simulator. Are you sure? (yes/no)", "no").ToLower();
            if (ok.StartsWith ("y", StringComparison.Ordinal))
                Shutdown(true);
        }

        /// <summary>
        ///     Run an optional startup list of commands
        /// </summary>
        /// <param name="fileName"></param>
        public virtual void RunCommandScript(string fileName)
        {
            if (File.Exists(fileName))
            {
                MainConsole.Instance.Info("[Commandfile]: Running " + fileName);
                List<string> commands = new List<string>();
                using (StreamReader readFile = File.OpenText(fileName))
                {
                    string currentCommand;
                    while ((currentCommand = readFile.ReadLine()) != null)
                    {
                        if ( (currentCommand != string.Empty) &&
                            (!currentCommand.StartsWith (";", StringComparison.Ordinal)) )
                        {
                            commands.Add(currentCommand);
                        }
                    }
                }
                foreach (string currentCommand in commands)
                {
                    MainConsole.Instance.Info("[Command file]: Running '" + currentCommand + "'");
                    MainConsole.Instance.RunCommand(currentCommand);
                }
            }
        }

        public virtual void HandleForceGC(IScene scene, string[] cmd)
        {
            GC.Collect();
            MainConsole.Instance.Warn("[Garbage Collection Service]: Garbage collection finished");
        }

        public virtual void runConfig(IScene scene, string[] cmd)
        {
            BaseApplication.Configure(true);
        }

        public virtual void HandleTimerScriptTime(IScene scene, string[] cmd)
        {
            if (cmd.Length != 5)
            {
                MainConsole.Instance.Warn("[Console]: Timer Interval command did not have enough parameters.");
                return;
            }

            if (int.TryParse (cmd [4], out m_TimerScriptTime))
            {
                m_TimerScriptTimer.Enabled = false;
                m_TimerScriptTimer.Interval = m_TimerScriptTime * 60 * 1000;
                m_TimerScriptTimer.Enabled = true;
                MainConsole.Instance.Warn ("[Console]: Set Timer Interval to " + cmd [4]);
            }
        }

        public virtual void HandleConfigRefresh(IScene scene, string[] cmd)
        {
            //Rebuild the configuration
            m_config = m_configurationLoader.LoadConfigSettings(m_original_config);

            if (m_config != null)
            {
                string hostName = m_config.Configs ["Network"].GetString ("HostName", "127.0.0.1");
                //Clean it up a bit
                // these are doing nothing??
                hostName = hostName.Replace ("http://", "").Replace ("https://", "");
                if (hostName.EndsWith ("/", StringComparison.Ordinal))
                    hostName = hostName.Remove (hostName.Length - 1, 1);
                
                foreach (IHttpServer server in m_Servers.Values)
                {
                    server.HostName = hostName;
                }
                MainConsole.Instance.Info ("[WhiteCore-Sim Configuration]: Finished reloading configuration.");
            }
        }

        public virtual void HandleShowInfo(IScene scene, string[] cmd)
        {
            PrintFileToConsole (Path.Combine (m_defaultDataPath, "/startuplogo.txt"));

            MainConsole.Instance.Info("Version: " + m_version);
            MainConsole.Instance.Info("Startup directory: " + Environment.CurrentDirectory);
        }

        public virtual void HandleShowVersion(IScene scene, string[] cmd)
        {
            MainConsole.Instance.InfoFormat("Version: {0}", m_version);
        }

        #endregion

        /// <summary>
        ///     Should be overridden and referenced by descendents if they need to perform extra shutdown processing
        ///     Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        public virtual void Shutdown(bool close)
        {
            try
            {
                try
                {
                    RemovePIDFile();
                    if (m_shutdownCommandsFile != string.Empty)
                    {
                        RunCommandScript(m_shutdownCommandsFile);
                    }
                }
                catch
                {
                    //It doesn't matter, just shut down
                }
                try
                {
                    //Close out all the modules
                    CloseModules();
                }
                catch
                {
                    //Just shut down already
                }
                try
                {
                    //Close the thread pool
                    Util.CloseThreadPool();
                }
                catch
                {
                    //Just shut down already
                }
                try
                {
                    //Stop the HTTP server(s)
                    foreach (IHttpServer server in m_Servers.Values)
                    {
                        server.Stop();
                    }
                }
                catch
                {
                    //Again, just shut down
                }

                if (close)
                    MainConsole.Instance.Info("[Shut Down]: Terminating");

                MainConsole.Instance.Info("[Shut Down]: Shut down processing on main thread complete. " +
                                          (close ? " Exiting WhiteCore-Sim..." : ""));

                if (close)
                    Environment.Exit(0);
            }
            catch
            {
            }
        }

        /// <summary>
        ///     Write the PID file to the hard drive
        /// </summary>
        /// <param name="path"></param>
        protected void CreatePIDFile(string path)
        {
            FileStream fs = null;
            try
            {
                string pidstring = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
                fs = File.Create(path);
                System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                byte[] buf = enc.GetBytes(pidstring);
                fs.Write(buf, 0, buf.Length);
                fs.Close();
                m_pidFile = path;
            }
            catch (Exception)
            {
                if (fs != null)
                    fs.Close();
            }
        }

        /// <summary>
        ///     Delete the PID file now that we are done running
        /// </summary>
        protected void RemovePIDFile()
        {
            if (m_pidFile != string.Empty)
            {
                try
                {
                    File.Delete(m_pidFile);
                    m_pidFile = string.Empty;
                }
                catch (Exception)
                {
                }
            }
        }
    }
}