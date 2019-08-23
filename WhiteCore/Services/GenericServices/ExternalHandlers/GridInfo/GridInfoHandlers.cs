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
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using Nini.Config;
using Nwc.XmlRpc;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services
{
    public class GridInfoHandlers : IGridInfo
    {
        readonly Hashtable grid_info = new Hashtable();

        public string GridName { get; protected set; }
        public string GridNick { get; protected set; }
        public string GridLoginURI { get; protected set; }
        public string GridWelcomeURI { get; protected set; }
        public string GridEconomyURI { get; protected set; }
        public string GridAboutURI { get; protected set; }
        public string GridHelpURI { get; protected set; }
        public string GridRegisterURI { get; protected set; }
        public string GridForgotPasswordURI { get; protected set; }
        public string GridMapTileURI { get; set; }
        public string AgentAppearanceURI { get; set; }
        public string GridWebProfileURI { get; protected set; }
        public string GridSearchURI { get; protected set; }
        public string GridDestinationURI { get; protected set; }
        public string GridMarketplaceURI { get; protected set; }
        public string GridTutorialURI { get; protected set; }
        public string GridSnapshotConfigURI { get; protected set; }

        protected IConfigSource m_config;
        protected IRegistryCore m_registry;

        /// <summary>
        ///     Instantiate a GridInfoService object.
        /// </summary>
        /// <param name="configSource">path to config path containing grid information</param>
        /// <param name="registry"></param>
        /// <remarks>
        ///     GridInfoService uses the [GridInfo] section of the
        ///     standard WhiteCore.ini file --- which is not optimal, but
        ///     anything else requires a general redesign of the config
        ///     system.
        /// </remarks>
        public GridInfoHandlers(IConfigSource configSource, IRegistryCore registry)
        {
            m_config = configSource;
            m_registry = registry;
            UpdateGridInfo();
        }

        public void UpdateGridInfo()
        {
            IConfig gridCfg = m_config.Configs["GridInfoService"];
            if (gridCfg == null)
                return;
            
            grid_info["platform"] = "WhiteCore";
            try
            {
                IConfig configCfg = m_config.Configs["Handlers"];
                IWebInterfaceModule webInterface = m_registry.RequestModuleInterface<IWebInterfaceModule>();
                IMoneyModule moneyModule = m_registry.RequestModuleInterface<IMoneyModule>();
                IGridServerInfoService serverInfoService = m_registry.RequestModuleInterface<IGridServerInfoService>();

                // grid details
                grid_info["gridname"] = GridName = GetConfig(m_config, "gridname");
                grid_info["gridnick"] = GridNick = GetConfig(m_config, "gridnick");

                // login
                GridLoginURI = GetConfig(m_config, "login");
                if (GridLoginURI == "")
                {
                    GridLoginURI = MainServer.Instance.ServerURI + "/";

                    if (configCfg != null && configCfg.GetString("LLLoginHandlerPort", "") != "")
                    {
                        var port = configCfg.GetString("LLLoginHandlerPort", "");
                        if (port == "" || port == "0")
                            port = MainServer.Instance.Port.ToString();
                        GridLoginURI = MainServer.Instance.FullHostName + ":" + port + "/";
                    }
                }
                grid_info["login"] = GridLoginURI;

                // welcome
                GridWelcomeURI = GetConfig(m_config, "welcome");
                if (GridWelcomeURI == "" && webInterface != null)
                    GridWelcomeURI = webInterface.LoginScreenURL;
                grid_info["welcome"] = CheckServerHost(GridWelcomeURI);

                // registration
                GridRegisterURI = GetConfig(m_config, "register");
                if (GridRegisterURI == "" && webInterface != null)
                    GridRegisterURI = webInterface.RegistrationScreenURL;
                grid_info["register"] = CheckServerHost(GridRegisterURI);

                GridAboutURI = GetConfig(m_config, "about");
                if (GridAboutURI == "" && webInterface != null)
                    GridAboutURI = webInterface.HomeScreenURL;
                grid_info["about"] = CheckServerHost(GridAboutURI);

                GridHelpURI = GetConfig(m_config, "help");
                if (GridHelpURI == "" && webInterface != null)
                    GridHelpURI = webInterface.HelpScreenURL;
                grid_info["help"] = CheckServerHost(GridHelpURI);

                GridForgotPasswordURI = GetConfig(m_config, "forgottenpassword");
                if (GridForgotPasswordURI == "" && webInterface != null)
                    GridForgotPasswordURI = webInterface.ForgotPasswordScreenURL;
                 grid_info["password"] = CheckServerHost(GridForgotPasswordURI);

                // mapping
                GridMapTileURI = GetConfig(m_config, "map");
                if (GridMapTileURI == "" && serverInfoService != null)
                    GridMapTileURI = serverInfoService.GetGridURI("MapService");

                // Agent
                AgentAppearanceURI = GetConfig(m_config, "AgentAppearanceURI");
                if (AgentAppearanceURI == "" && serverInfoService != null)
                    AgentAppearanceURI = serverInfoService.GetGridURI("SSAService");

                // profile
                GridWebProfileURI = GetConfig(m_config, "webprofile");
                if (GridWebProfileURI == "" && webInterface != null)
                    GridWebProfileURI = webInterface.WebProfileURL;

                // economy
                GridEconomyURI = GetConfig(m_config, "economy");
                if (GridEconomyURI == "")
                {

                    GridEconomyURI = MainServer.Instance.ServerURI + "/";           // assume default... 

                    if (moneyModule != null)
                    {
                        int port = moneyModule.ClientPort;
                        if (port == 0)
                            port = (int) MainServer.Instance.Port;

                        GridEconomyURI = MainServer.Instance.FullHostName + ":" + port + "/";
                    }
                }
                grid_info["economy"] = grid_info["helperuri"] = CheckServerHost(GridEconomyURI);


                // misc.. these must be set to be used
                GridSearchURI = GetConfig(m_config, "search");
                grid_info["search"] = CheckServerHost(GridSearchURI);

                GridDestinationURI = GetConfig(m_config, "destination");
                grid_info["destination"] = CheckServerHost(GridDestinationURI);

                GridMarketplaceURI = GetConfig(m_config, "marketplace");
                grid_info["marketplace"] = CheckServerHost(GridMarketplaceURI);

                GridTutorialURI = GetConfig(m_config, "tutorial");
                grid_info["tutorial"] = CheckServerHost(GridTutorialURI);

                GridSnapshotConfigURI = GetConfig(m_config, "snapshotconfig");
                grid_info["snapshotconfig"] = CheckServerHost(GridSnapshotConfigURI);

            }
            catch (Exception)
            {
                MainConsole.Instance.Warn(
                    "[Grid Info Service]: Cannot get grid info from config source, using minimal defaults");
            }

            MainConsole.Instance.DebugFormat("[Grid Info Service]: Grid info service initialized with {0} keys",
                                             grid_info.Count);
        }

        string CheckServerHost(string uri)
        {
            // if uri is in the format http://ServersHostnmae:nnnn/ replace with the current Hostname
            return uri.Replace ("ServersHostname", MainServer.Instance.HostName);
        }

        string GetConfig(IConfigSource config, string p)
        {
            IConfig gridCfg = config.Configs["GridInfoService"];
            return gridCfg.GetString(p, "");
        }

        void IssueWarning()
        {
            MainConsole.Instance.Warn("[Grid Info Service]: Found no [GridInfo] section in your configuration files");
            MainConsole.Instance.Warn(
                "[Grid Info Service]: Trying to guess sensible defaults, you might want to provide better ones:");

            foreach (string key in grid_info.Keys)
            {
                MainConsole.Instance.WarnFormat("[Grid Info Service]: {0}: {1}", key, grid_info[key]);
            }
        }
        
        public XmlRpcResponse XmlRpcGridInfoMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            MainConsole.Instance.Debug("[Grid Info Service]: Request for grid info");
            UpdateGridInfo();

            foreach (string k in grid_info.Keys)
            {
                responseData[k] = grid_info[k];
            }
            response.Value = responseData;

            return response;
        }

        public byte[] RestGetGridInfoMethod(string path, Stream request, OSHttpRequest httpRequest,
                                            OSHttpResponse httpResponse)
        {
            StringBuilder sb = new StringBuilder();
            UpdateGridInfo();

            sb.Append("<gridinfo>\n");
            foreach (string key in grid_info.Keys)
            {
                sb.AppendFormat("<{0}>{1}</{0}>\n", key, grid_info[key]);
            }
            sb.Append("</gridinfo>\n");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }


        public Hashtable GetGridInfoHashtable ()
        {
            UpdateGridInfo ();
            return new Hashtable (grid_info);
        }

    }
}
