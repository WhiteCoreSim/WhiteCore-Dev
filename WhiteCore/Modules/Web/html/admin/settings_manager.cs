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
    public class SettingsManagerPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/admin/settings_manager.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return true; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return true; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();
            IGenericsConnector connector = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector>();
            var settings = connector.GetGeneric<GridSettings>(UUID.Zero, "WebSettings", "Settings");

            if (requestParameters.ContainsKey("Submit"))
            {
                settings.WebRegistration = requestParameters["WebRegistration"].ToString() == "1";
                settings.MapCenter.X = int.Parse(requestParameters["GridCenterX"].ToString());
                settings.MapCenter.Y = int.Parse(requestParameters["GridCenterY"].ToString());
                settings.HideLanguageTranslatorBar = requestParameters["HideLanguageBar"].ToString() == "1";
                settings.HideStyleBar = requestParameters["HideStyleBar"].ToString() == "1";
                connector.AddGeneric(UUID.Zero, "WebSettings", "Settings", settings.ToOSD());

                response = "Successfully updated settings.";

                return null;
            }
            else if (requestParameters.ContainsKey("IgnorePagesUpdates"))
            {
                settings.LastPagesVersionUpdateIgnored = PagesMigrator.CurrentVersion;
                connector.AddGeneric(UUID.Zero, "WebSettings", "Settings", settings.ToOSD());
            }
            else if (requestParameters.ContainsKey("IgnoreSettingsUpdates"))
            {
                settings.LastSettingsVersionUpdateIgnored = PagesMigrator.CurrentVersion;
                connector.AddGeneric(UUID.Zero, "WebSettings", "Settings", settings.ToOSD());
            }
            vars.Add("WebRegistrationNo", !settings.WebRegistration ? "selected=\"selected\"" : "");
            vars.Add("WebRegistrationYes", settings.WebRegistration ? "selected=\"selected\"" : "");
            vars.Add("GridCenterX", settings.MapCenter.X);
            vars.Add("GridCenterY", settings.MapCenter.Y);
            vars.Add("HideLanguageBarNo", !settings.HideLanguageTranslatorBar ? "selected=\"selected\"" : "");
            vars.Add("HideLanguageBarYes", settings.HideLanguageTranslatorBar ? "selected=\"selected\"" : "");
            vars.Add("HideStyleBarNo", !settings.HideStyleBar ? "selected=\"selected\"" : "");
            vars.Add("HideStyleBarYes", settings.HideStyleBar ? "selected=\"selected\"" : "");
            vars.Add("IgnorePagesUpdates",
                     PagesMigrator.CheckWhetherIgnoredVersionUpdate(settings.LastPagesVersionUpdateIgnored)
                         ? ""
                         : "checked");
            vars.Add("IgnoreSettingsUpdates",
                     settings.LastSettingsVersionUpdateIgnored != SettingsMigrator.CurrentVersion ? "" : "checked");

            vars.Add("SettingsManager", translator.GetTranslatedString("SettingsManager"));
            vars.Add("IgnorePagesUpdatesText", translator.GetTranslatedString("IgnorePagesUpdatesText"));
            vars.Add("IgnoreSettingsUpdatesText", translator.GetTranslatedString("IgnoreSettingsUpdatesText"));
            vars.Add("WebRegistrationText", translator.GetTranslatedString("WebRegistrationText"));
            vars.Add("GridCenterXText", translator.GetTranslatedString("GridCenterXText"));
            vars.Add("GridCenterYText", translator.GetTranslatedString("GridCenterYText"));
            vars.Add("HideLanguageBarText", translator.GetTranslatedString("HideLanguageBarText"));
            vars.Add("HideStyleBarText", translator.GetTranslatedString("HideStyleBarText"));
            vars.Add("Save", translator.GetTranslatedString("Save"));
            vars.Add("No", translator.GetTranslatedString("No"));
            vars.Add("Yes", translator.GetTranslatedString("Yes"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}