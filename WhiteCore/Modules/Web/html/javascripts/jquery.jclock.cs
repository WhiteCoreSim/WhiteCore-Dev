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
    public class JQueryClickPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/static/js/jquery.jclock.js"
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
            vars.Add("Sun", translator.GetTranslatedString("Sun"));
            vars.Add("Mon", translator.GetTranslatedString("Mon"));
            vars.Add("Tue", translator.GetTranslatedString("Tue"));
            vars.Add("Wed", translator.GetTranslatedString("Wed"));
            vars.Add("Thu", translator.GetTranslatedString("Thu"));
            vars.Add("Fri", translator.GetTranslatedString("Fri"));
            vars.Add("Sat", translator.GetTranslatedString("Sat"));
            vars.Add("Sunday", translator.GetTranslatedString("Sunday"));
            vars.Add("Monday", translator.GetTranslatedString("Monday"));
            vars.Add("Tuesday", translator.GetTranslatedString("Tuesday"));
            vars.Add("Wednesday", translator.GetTranslatedString("Wednesday"));
            vars.Add("Thursday", translator.GetTranslatedString("Thursday"));
            vars.Add("Friday", translator.GetTranslatedString("Friday"));
            vars.Add("Saturday", translator.GetTranslatedString("Saturday"));
            vars.Add("Jan", translator.GetTranslatedString("Jan"));
            vars.Add("Feb", translator.GetTranslatedString("Feb"));
            vars.Add("Mar", translator.GetTranslatedString("Mar"));
            vars.Add("Apr", translator.GetTranslatedString("Apr"));
            vars.Add("May", translator.GetTranslatedString("May"));
            vars.Add("Jun", translator.GetTranslatedString("Jun"));
            vars.Add("Jul", translator.GetTranslatedString("Jul"));
            vars.Add("Aug", translator.GetTranslatedString("Aug"));
            vars.Add("Sep", translator.GetTranslatedString("Sep"));
            vars.Add("Oct", translator.GetTranslatedString("Oct"));
            vars.Add("Nov", translator.GetTranslatedString("Nov"));
            vars.Add("Dec", translator.GetTranslatedString("Dec"));
            vars.Add("January", translator.GetTranslatedString("January"));
            vars.Add("February", translator.GetTranslatedString("February"));
            vars.Add("March", translator.GetTranslatedString("March"));
            vars.Add("April", translator.GetTranslatedString("April"));
            vars.Add("June", translator.GetTranslatedString("June"));
            vars.Add("July", translator.GetTranslatedString("July"));
            vars.Add("August", translator.GetTranslatedString("August"));
            vars.Add("September", translator.GetTranslatedString("September"));
            vars.Add("October", translator.GetTranslatedString("October"));
            vars.Add("November", translator.GetTranslatedString("November"));
            vars.Add("December", translator.GetTranslatedString("December"));
            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
