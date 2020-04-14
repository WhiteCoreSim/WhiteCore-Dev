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
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class UserTransactionsPage : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/user/transactions.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                                OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                                ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();
            var transactionsList = new List<Dictionary<string, object>> ();

            var today = DateTime.Now;
            var fromDays = today.AddDays (-7);
            string DateStart = fromDays.ToShortDateString ();
            string DateEnd = today.ToShortDateString ();

            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule> ();
            string noDetails = translator.GetTranslatedString ("NoTransactionsText");

            // Check if we're looking at the standard page or the submitted one
            if (requestParameters.ContainsKey ("Submit")) {
                if (requestParameters.ContainsKey ("date_start"))
                    DateStart = requestParameters ["date_start"].ToString ();
                if (requestParameters.ContainsKey ("date_end"))
                    DateEnd = requestParameters ["date_end"].ToString ();
            }

            UserAccount user = Authenticator.GetAuthentication (httpRequest);
            if (user == null) {
                response = "<h3>Error validating user details</h3>" +
                    "<script language=\"javascript\">" +
                    "setTimeout(function() {window.location.href = \"/?page=user_transactions\";}, 1000);" +
                    "</script>";

                return null;
            }

            // Transaction Logs
            var timeNow = DateTime.Now.ToString ("HH:mm:ss");
            var dateFrom = DateTime.Parse (DateStart + " " + timeNow);
            var dateTo = DateTime.Parse (DateEnd + " " + timeNow);
            TimeSpan period = dateTo.Subtract (dateFrom);

            var transactions = new List<AgentTransfer> ();
            if (user != null && moneyModule != null)
                transactions = moneyModule.GetTransactionHistory (user.PrincipalID, UUID.Zero, dateFrom, dateTo, null, null);

            // data
            if (transactions.Count > 0) {
                noDetails = "";

                foreach (var transaction in transactions) {
                    transactionsList.Add (new Dictionary<string, object> {
                        { "Date", Culture.LocaleDate (transaction.TransferDate.ToLocalTime(), "MMM dd, hh:mm:ss tt") },
                        { "ToAgent", transaction.ToAgentName },
                        { "FromAgent", transaction.FromAgentName },
                        { "Description", transaction.Description },
                        { "Amount",transaction.Amount },
                        { "ToBalance",transaction.ToBalance }

                    });
                }
            }

            if (transactionsList.Count == 0) {
                transactionsList.Add (new Dictionary<string, object> {
                    {"Date", ""},                   //Culture.LocaleDate(today,"MMM dd, hh:mm:ss")},
                    {"ToAgent", ""},
                    {"FromAgent", ""},
                    {"Description", translator.GetTranslatedString ("NoTransactionsText")},
                    {"Amount",""},
                    {"ToBalance",""}

                });
            }

            // always required data
            vars.Add ("DateStart", DateStart);
            vars.Add ("DateEnd", DateEnd);
            vars.Add ("Period", period.TotalDays + " " + translator.GetTranslatedString ("DaysText"));
            vars.Add ("TransactionsList", transactionsList);
            vars.Add ("NoTransactionsText", noDetails);

            // labels
            vars.Add ("UserName", user.Name);
            vars.Add ("TransactionsText", translator.GetTranslatedString ("TransactionsText"));
            vars.Add("DateTimeFormat", Culture.DateTimeFormatInfo);
            vars.Add ("DateInfoText", translator.GetTranslatedString ("DateInfoText"));
            vars.Add ("DateStartText", translator.GetTranslatedString ("DateStartText"));
            vars.Add ("DateEndText", translator.GetTranslatedString ("DateEndText"));
            vars.Add("Search", translator.GetTranslatedString("Search"));

            vars.Add ("TransactionDateText", translator.GetTranslatedString ("TransactionDateText"));
            vars.Add ("TransactionToAgentText", translator.GetTranslatedString ("TransactionToAgentText"));
            vars.Add ("TransactionFromAgentText", translator.GetTranslatedString ("TransactionFromAgentText"));
            // vars.Add("TransactionTimeText", translator.GetTranslatedString("Time"));
            vars.Add ("TransactionDetailText", translator.GetTranslatedString ("TransactionDetailText"));
            vars.Add ("TransactionAmountText", translator.GetTranslatedString ("TransactionAmountText"));
            vars.Add ("TransactionBalanceText", translator.GetTranslatedString ("TransactionBalanceText"));

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
