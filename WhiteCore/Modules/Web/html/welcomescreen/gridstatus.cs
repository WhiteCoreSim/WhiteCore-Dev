﻿/*
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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.Modules.Web
{
    public class GridStatusPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/welcomescreen/gridstatus.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return false; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();

            IAgentInfoConnector recentUsers = Framework.Utilities.DataManager.RequestPlugin<IAgentInfoConnector>();
            IGenericsConnector connector = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();
            GridWelcomeScreen welcomeInfo = null;
            if (connector != null)
                welcomeInfo = connector.GetGeneric<GridWelcomeScreen>(UUID.Zero, "GridWelcomeScreen",
                                                                                    "GridWelcomeScreen");

            if (welcomeInfo == null)
                welcomeInfo = GridWelcomeScreen.Default;

            IConfigSource config = webInterface.Registry.RequestModuleInterface<ISimulationBase>().ConfigSource;
            vars.Add("GridStatus", translator.GetTranslatedString("GridStatus"));
            vars.Add("TotalUserCount", translator.GetTranslatedString("TotalUserCount"));
            vars.Add("TotalRegionCount", translator.GetTranslatedString("TotalRegionCount"));
            vars.Add("UniqueVisitors", translator.GetTranslatedString("UniqueVisitors"));
            vars.Add("OnlineNow", translator.GetTranslatedString("OnlineNow"));
            vars.Add("RecentlyOnline", translator.GetTranslatedString("RecentlyOnline"));
            vars.Add("HGActiveText", translator.GetTranslatedString("HyperGrid"));
            vars.Add("VoiceActiveLabel", translator.GetTranslatedString("Voice"));
            vars.Add("CurrencyActiveLabel", translator.GetTranslatedString("Currency"));

            vars.Add("GridOnline",
                     welcomeInfo.GridStatus
                         ? translator.GetTranslatedString("Online")
                         : translator.GetTranslatedString("Offline"));
            vars.Add("UserCount", webInterface.Registry.RequestModuleInterface<IUserAccountService>().
                                               NumberOfUserAccounts(null, "").ToString());
            vars.Add("RegionCount", Framework.Utilities.DataManager.RequestPlugin<IRegionData>().
                                                Count((RegionFlags) 0, (RegionFlags) 0).ToString());
            string disabled = translator.GetTranslatedString("Disabled"),
                   enabled = translator.GetTranslatedString("Enabled");
            vars.Add("HGActive", disabled + "(TODO: FIX)");
            vars.Add("VoiceActive",
                     config.Configs["Voice"] != null &&
                     config.Configs["Voice"].GetString("Module", "GenericVoice") != "GenericVoice"
                         ? enabled
                         : disabled);
            vars.Add("CurrencyActive",
                     webInterface.Registry.RequestModuleInterface<IMoneyModule>() != null ? enabled : disabled);

            if (recentUsers != null)
            {
                vars.Add("UniqueVisitorCount", recentUsers.RecentlyOnline((uint) TimeSpan.FromDays(30).TotalSeconds, false).ToString());
                vars.Add ("OnlineNowCount", recentUsers.RecentlyOnline (5 * 60, true).ToString ());
                vars.Add("RecentlyOnlineCount", recentUsers.RecentlyOnline(10*60, false).ToString());
            }
            else
            {
                vars.Add("UniqueVisitorCount", "");
                vars.Add ("OnlineNowCount", "");
                vars.Add("RecentlyOnlineCount", "");
            }

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}