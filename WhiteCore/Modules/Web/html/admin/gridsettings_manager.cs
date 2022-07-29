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

using System.Collections.Generic;
using WhiteCore.Framework.Servers.HttpServer.Implementation;

namespace WhiteCore.Modules.Web
{
    public class GridSettingsManagerPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/admin/gridsettings_manager.html"
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
            var settings = webInterface.GetGridSettings();

            if (requestParameters.ContainsKey("update"))
            {
                settings.Gridname = requestParameters["Gridname"].ToString();
                settings.Gridnick = requestParameters["Gridnick"].ToString();
                settings.WelcomeMessage = requestParameters["WelcomeMessage"].ToString();
                settings.GovernorName = requestParameters["GovernorName"].ToString();
                settings.RealEstateOwnerName = requestParameters["RealEstateOwnerName"].ToString();
                settings.BankerName = requestParameters["BankerName"].ToString();
                settings.MarketplaceOwnerName = requestParameters["MarketplaceOwnerName"].ToString();
                settings.MainlandEstateName = requestParameters["MainlandEstateName"].ToString();
                settings.SystemEstateName = requestParameters["SystemEstateName"].ToString();

                // update main grid setup
                webInterface.SaveGridSettings(settings);
                response = webInterface.UserMsg("Successfully updated grid settings.", true);

                return null;
            }

            vars.Add("Gridname", settings.Gridname);
            vars.Add("Gridnick", settings.Gridnick);
            vars.Add("WelcomeMessage", settings.WelcomeMessage);
            vars.Add("GovernorName", settings.GovernorName);
            vars.Add("RealEstateOwnerName", settings.RealEstateOwnerName);
            vars.Add("BankerName", settings.BankerName);
            vars.Add("MarketPlaceOwnerName", settings.MarketplaceOwnerName);
            vars.Add("MainlandEstateName", settings.MainlandEstateName);
            vars.Add("SystemEstateName", settings.SystemEstateName);


            vars.Add("GridSettingsManager", translator.GetTranslatedString("GridSettingsManager"));
            vars.Add("GridnameText", translator.GetTranslatedString("GridnameText"));
            vars.Add("GridnickText", translator.GetTranslatedString("GridnickText"));
            vars.Add("WelcomeMessageText", translator.GetTranslatedString("WelcomeMessageText"));
            vars.Add("GovernorNameText", translator.GetTranslatedString("GovernorNameText"));
            vars.Add("RealEstateOwnerNameText", translator.GetTranslatedString("RealEstateOwnerNameText"));
            vars.Add("BankerNameText", translator.GetTranslatedString("BankerNameText"));
            vars.Add("MarketPlaceOwnerNameText", translator.GetTranslatedString("MarketPlaceOwnerNameText"));
            vars.Add("MainlandEstateNameText", translator.GetTranslatedString("MainlandEstateNameText"));
            vars.Add("SystemEstateNameText", translator.GetTranslatedString("SystemEstateNameText"));


            vars.Add("Cancel", translator.GetTranslatedString("Cancel"));
            vars.Add("Save", translator.GetTranslatedString("Save"));
            // vars.Add("No", translator.GetTranslatedString("No"));
            // vars.Add("Yes", translator.GetTranslatedString("Yes"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
