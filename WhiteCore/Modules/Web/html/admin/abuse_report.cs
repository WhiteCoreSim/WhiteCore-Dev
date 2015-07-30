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
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.DatabaseInterfaces;

namespace WhiteCore.Modules.Web
{
    public class AdminUserAbuseReportPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                {
                    "html/admin/abuse_report.html"
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
            //var today = DateTime.Now;

            IAbuseReportsConnector abuseModule = Framework.Utilities.DataManager.RequestPlugin<IAbuseReportsConnector>();
            //IUserAccountService accountService = webInterface.Registry.RequestModuleInterface<IUserAccountService> ();
            IWebHttpTextureService webTextureService = webInterface.Registry.RequestModuleInterface<IWebHttpTextureService> ();
            
            string noDetails = translator.GetTranslatedString ("NoTransactionsText");
            AbuseReport rpt = new AbuseReport ();;
            var snapshotURL = "../images/icons/no_screenshot.jpg";

            if (httpRequest.Query.ContainsKey("cardid"))
            {

                int cardID = int.Parse(httpRequest.Query["cardid"].ToString());
                rpt = abuseModule.GetAbuseReport (cardID);

                noDetails = "";
                if (rpt.ScreenshotID != UUID.Zero)
                    snapshotURL = webTextureService.GetTextureURL (rpt.ScreenshotID);

                // notes?
                if (requestParameters.ContainsKey("Submit") &&
                    requestParameters["Submit"].ToString() == "SubmitUpdates")
                {
                    string newNote = requestParameters["AbuseNoteText"].ToString();
                    if (newNote != "")
                        rpt.Notes += "\n" + newNote;

                    rpt.Checked = (requestParameters["Checked"].ToString().ToLower() == "yes");
                    rpt.Active = (requestParameters["Active"].ToString().ToLower() == "yes");
                    rpt.AssignedTo = requestParameters["AssignedTo"].ToString();

                    abuseModule.UpdateAbuseReport (rpt);

                }

            }

            // details
            vars.Add( "CardNumber", rpt.Number);
            //vars.Add("Date"), Culture.LocaleDate (transaction.TransferDate.ToLocalTime(), "MMM dd, hh:mm:ss tt");
            vars.Add( "Details", rpt.AbuseDetails );
            vars.Add( "AbuseLocation", rpt.AbuseLocation );
            vars.Add( "Summary", rpt.AbuseSummary );
            vars.Add( "AbuserName", rpt.AbuserName );
            vars.Add( "Active", rpt.Active ? "Yes" : "No" );
            vars.Add( "AssignedTo", rpt.AssignedTo );
            vars.Add( "Category", rpt.Category );
            vars.Add( "Checked", rpt.Checked ? "Yes" : "No");
            vars.Add( "Notes", rpt.Notes );
            vars.Add( "ObjectName", rpt.ObjectName );
            vars.Add( "ObjectPosition", rpt.ObjectPosition );
            vars.Add( "ObjectUUID", rpt.ObjectUUID);
            vars.Add( "RegionName", rpt.RegionName );
            vars.Add( "ReporterName", rpt.ReporterName );
            vars.Add( "ScreenshotURL", snapshotURL);
            
            vars.Add ("NoDetailsText", noDetails);
            vars.Add ("ErrorMessage", "");
            // labels
            vars.Add("AbuseReportText", translator.GetTranslatedString("AbuseReportText"));

            vars.Add("DateText", translator.GetTranslatedString("DateText"));
            vars.Add("CategoryText", translator.GetTranslatedString("AbuseCategoryText"));
            vars.Add("AbuseSummaryText", translator.GetTranslatedString("AbuseSummaryText"));
            vars.Add("AbuserNameText", translator.GetTranslatedString("AbuserNameText"));
            vars.Add("AbuseReporterNameText", translator.GetTranslatedString("AbuseReporterNameText"));
            vars.Add("RegionNameText", translator.GetTranslatedString("RegionNameText"));
            vars.Add("ObjectNameText", translator.GetTranslatedString("ObjectNameText"));
            vars.Add("LocationText", translator.GetTranslatedString("LocationText"));
            vars.Add("UUIDText", translator.GetTranslatedString("UUIDText"));
            vars.Add("DetailsText", translator.GetTranslatedString("DetailsText"));
            vars.Add("NotesText", translator.GetTranslatedString("NotesText"));
            vars.Add("AddNotesText", translator.GetTranslatedString("AddNotesText"));
            vars.Add("ActiveText", translator.GetTranslatedString("ActiveText"));
            vars.Add("CheckedText", translator.GetTranslatedString("CheckedText"));
            vars.Add("AssignedToText", translator.GetTranslatedString("AssignedToText"));
            vars.Add ("Submit", translator.GetTranslatedString("SaveUpdates"));

            vars.Add("FirstText", translator.GetTranslatedString("FirstText"));
            vars.Add("BackText", translator.GetTranslatedString("BackText"));
            vars.Add("NextText", translator.GetTranslatedString("NextText"));
            vars.Add("LastText", translator.GetTranslatedString("LastText"));
            vars.Add("CurrentPageText", translator.GetTranslatedString("CurrentPageText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}

