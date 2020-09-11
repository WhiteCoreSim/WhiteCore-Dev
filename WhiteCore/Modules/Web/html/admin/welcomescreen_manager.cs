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
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;

namespace WhiteCore.Modules.Web
{
    public class WelcomeScreenManagerPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/admin/welcomescreen_manager.html"
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

            if (requestParameters.ContainsKey("update"))
            {
                GridWelcomeScreen submittedInfo = new GridWelcomeScreen();
                submittedInfo.SpecialWindowMessageTitle = requestParameters["SpecialWindowTitle"].ToString();
                submittedInfo.SpecialWindowMessageText = requestParameters["SpecialWindowText"].ToString();
                submittedInfo.SpecialWindowMessageColor = requestParameters["SpecialWindowColor"].ToString();
                submittedInfo.SpecialWindowActive = requestParameters["SpecialWindowStatus"].ToString() == "1";
                submittedInfo.GridStatus = requestParameters["GridStatus"].ToString() == "1";

                connector.AddGeneric(UUID.Zero, "GridWelcomeScreen", "GridWelcomeScreen", submittedInfo.ToOSD());

                response = webInterface.UserMsg("Successfully saved data");
                return null;
            }

            GridWelcomeScreen welcomeInfo = connector.GetGeneric<GridWelcomeScreen>(UUID.Zero, "GridWelcomeScreen",
                                                                                    "GridWelcomeScreen");
            if (welcomeInfo == null)
                welcomeInfo = GridWelcomeScreen.Default;

            vars.Add("OpenNewsManager", translator.GetTranslatedString("OpenNewsManager"));
            vars.Add("SpecialWindowTitleText", translator.GetTranslatedString("SpecialWindowTitleText"));
            vars.Add("SpecialWindowTextText", translator.GetTranslatedString("SpecialWindowTextText"));
            vars.Add("SpecialWindowColorText", translator.GetTranslatedString("SpecialWindowColorText"));
            vars.Add("SpecialWindowStatusText", translator.GetTranslatedString("SpecialWindowStatusText"));
            vars.Add("WelcomeScreenManagerFor", translator.GetTranslatedString("WelcomeScreenManagerFor"));
            vars.Add("GridStatus", translator.GetTranslatedString("GridStatus"));
            vars.Add("Online", translator.GetTranslatedString("Online"));
            vars.Add("Offline", translator.GetTranslatedString("Offline"));
            vars.Add("Enabled", translator.GetTranslatedString("Enabled"));
            vars.Add("Disabled", translator.GetTranslatedString("Disabled"));

            vars.Add("SpecialWindowTitle", welcomeInfo.SpecialWindowMessageTitle);
            vars.Add("SpecialWindowMessage", welcomeInfo.SpecialWindowMessageText);
            vars.Add("SpecialWindowActive", welcomeInfo.SpecialWindowActive ? "selected" : "");
            vars.Add("SpecialWindowInactive", welcomeInfo.SpecialWindowActive ? "" : "selected");
            vars.Add("GridActive", welcomeInfo.GridStatus ? "selected" : "");
            vars.Add("GridInactive", welcomeInfo.GridStatus ? "" : "selected");
            vars.Add("SpecialWindowColorRed", welcomeInfo.SpecialWindowMessageColor == "red" ? "selected" : "");
            vars.Add("SpecialWindowColorYellow", welcomeInfo.SpecialWindowMessageColor == "yellow" ? "selected" : "");
            vars.Add("SpecialWindowColorGreen", welcomeInfo.SpecialWindowMessageColor == "green" ? "selected" : "");
            vars.Add("SpecialWindowColorWhite", welcomeInfo.SpecialWindowMessageColor == "white" ? "selected" : "");
            vars.Add("Submit", translator.GetTranslatedString("Submit"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}