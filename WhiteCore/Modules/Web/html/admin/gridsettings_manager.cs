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

using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using OpenMetaverse;
using System.Collections.Generic;

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

            if (requestParameters.ContainsKey("Submit"))
            {
                settings.Gridname = requestParameters["Gridname"].ToString();
                settings.Gridnick = requestParameters["Gridnick"].ToString();
                settings.WelcomeMessage = requestParameters["WelcomeMessage"].ToString();
                settings.SystemEstateOwnerName = requestParameters["SystemEstateOwnerName"].ToString();
                settings.SystemEstateName = requestParameters["SystemEstateName"].ToString();




                // update main grid setup
                webInterface.SaveGridSettings (settings);
                response = "Successfully updated grid settings.";

                return null;
            }

            vars.Add("Gridname", settings.Gridname);
            vars.Add("Gridnick", settings.Gridnick);
            vars.Add("WelcomeMessage", settings.WelcomeMessage);
            vars.Add("SystemEstateOwnerName", settings.SystemEstateOwnerName);
            vars.Add("SystemEstateName", settings.SystemEstateName);


 

            vars.Add("GridSettingsManager", translator.GetTranslatedString("GridSettingsManager"));
            vars.Add("GridnameText", translator.GetTranslatedString("GridnameText"));
            vars.Add("GridnickText", translator.GetTranslatedString("GridnickText"));
            vars.Add("WelcomeMessageText", translator.GetTranslatedString("WelcomeMessageText"));
            vars.Add("SystemEstateNameText", translator.GetTranslatedString("SystemEstateNameText"));
            vars.Add("SystemEstateOwnerText", translator.GetTranslatedString("SystemEstateOwnerText"));



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