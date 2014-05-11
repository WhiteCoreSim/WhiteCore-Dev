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

using WhiteCore.Framework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using OpenMetaverse;
using System.Collections.Generic;

namespace WhiteCore.Modules.Web
{
    public class HomeMain : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/home.html"
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

            // Tooltips Urls
            vars.Add("TooltipsWelcomeScreen", translator.GetTranslatedString("TooltipsWelcomeScreen"));
            vars.Add("TooltipsWorldMap", translator.GetTranslatedString("TooltipsWorldMap"));

            // Index Page
            vars.Add("HomeText", translator.GetTranslatedString("HomeText"));
            vars.Add("HomeTextWelcome", translator.GetTranslatedString("HomeTextWelcome"));
            vars.Add("HomeTextTips", translator.GetTranslatedString("HomeTextTips"));
            vars.Add("WelcomeScreen", translator.GetTranslatedString("WelcomeScreen"));
            vars.Add("WelcomeToText", translator.GetTranslatedString("WelcomeToText"));

            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();
            var settings = generics.GetGeneric<GridSettings>(UUID.Zero, "WebSettings", "Settings");
            if (PagesMigrator.RequiresUpdate() &&
                PagesMigrator.CheckWhetherIgnoredVersionUpdate(settings.LastPagesVersionUpdateIgnored))
                vars.Add("PagesUpdateRequired",
                         translator.GetTranslatedString("Pages") + " " +
                         translator.GetTranslatedString("DefaultsUpdated"));
            else
                vars.Add("PagesUpdateRequired", "");
            if (SettingsMigrator.RequiresUpdate() &&
                SettingsMigrator.CheckWhetherIgnoredVersionUpdate(settings.LastSettingsVersionUpdateIgnored))
                vars.Add("SettingsUpdateRequired",
                         translator.GetTranslatedString("Settings") + " " +
                         translator.GetTranslatedString("DefaultsUpdated"));
            else
                vars.Add("SettingsUpdateRequired", "");
            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}