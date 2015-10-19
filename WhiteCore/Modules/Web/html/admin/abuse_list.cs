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
using System.Collections.Generic;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;

namespace WhiteCore.Modules.Web
{
    public class AdminUserAbuseListPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get {
                return new[] {
                    "html/admin/abuse_list.html"
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

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();
            var abuseReportsList = new List<Dictionary<string, object>> ();

            IAbuseReportsConnector abuseModule = Framework.Utilities.DataManager.RequestPlugin<IAbuseReportsConnector> ();

            string noDetails = translator.GetTranslatedString ("NoDetailsText");
            List<AbuseReport> abuseReports;

            abuseReports = abuseModule.GetAbuseReports (0, 0, true);

            if (abuseReports.Count > 0)
            {
                noDetails = "";

                foreach (var rpt in abuseReports)
                {
                    abuseReportsList.Add (new Dictionary<string, object> {
                        //{ "Date", Culture.LocaleDate (transaction.TransferDate.ToLocalTime(), "MMM dd, hh:mm:ss tt") },
                        { "Category", rpt.Category },
                        { "ReporterName", rpt.ReporterName },
                        { "Abusername", rpt.AbuserName },
                        { "Summary", rpt.AbuseSummary },
                        { "AssignedTo", rpt.AssignedTo },
                        { "Active", rpt.Active ? "Yes" : "No" },
                        { "CardNumber", rpt.Number.ToString () }
                    });
                }
            }

            if (abuseReports == null)
            {
                abuseReportsList.Add (new Dictionary<string, object> {
                    { "Category", "" },
                    { "ReporterName", "" },
                    { "Abusername", "" },
                    { "Summary", "No abuse reports available" },
                    { "AssignedTo", "" },
                    { "Active", "" },
                    { "CardNumber", "" }
                });
            }

            // always required data
            vars.Add ("AbuseReportsList", abuseReportsList);
            vars.Add ("NoDetailsText", noDetails);
            vars.Add ("AbuseReportText", translator.GetTranslatedString ("MenuAbuse"));

//            vars.Add("DateText", translator.GetTranslatedString("DateText"));
            vars.Add ("CategoryText", translator.GetTranslatedString ("CategoryText"));
            vars.Add ("AbuseReporterNameText", translator.GetTranslatedString ("AbuseReporterNameText"));
            vars.Add ("AbuserNameText", translator.GetTranslatedString ("AbuserNameText"));
            vars.Add ("SummaryText", translator.GetTranslatedString ("SummaryText"));
            vars.Add ("AssignedToText", translator.GetTranslatedString ("AssignedToText"));
            vars.Add ("ActiveText", translator.GetTranslatedString ("ActiveText"));
            vars.Add ("MoreInfoText", translator.GetTranslatedString ("MoreInfoText"));

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}

