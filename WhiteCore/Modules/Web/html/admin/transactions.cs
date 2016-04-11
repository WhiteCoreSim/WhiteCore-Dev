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
    public class AdminUserTransactionsPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/admin/transactions.html"
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
            var transactionsList = new List<Dictionary<string, object>>();

            uint amountPerQuery = 25;
            var today = DateTime.Now;
            var thirtyDays = today.AddDays (-30);
            string DateStart = thirtyDays.ToShortDateString();
            string DateEnd = today.ToShortDateString();
            string UserName = "";
            UUID UserID = UUID.Zero;

            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule>();
            string noDetails = translator.GetTranslatedString ("NoTransactionsText");

            // Check if we're looking at the standard page or the submitted one
            if (requestParameters.ContainsKey ("Submit"))
            {
                if (requestParameters.ContainsKey ("date_start"))
                    DateStart = requestParameters ["date_start"].ToString ();
                if (requestParameters.ContainsKey ("date_end"))
                    DateEnd = requestParameters ["date_end"].ToString ();
                if (requestParameters.ContainsKey ("user_name"))
                    UserName = requestParameters ["user_name"].ToString ();

                if (UserName != "")
                {
                    // TODO: Work out a better way to catch this
                    UserID = (UUID)Constants.LibraryOwner;         // This user should hopefully never have transactions

                    if (UserName.Split (' ').Length == 2)
                    {
                        IUserAccountService accountService = webInterface.Registry.RequestModuleInterface<IUserAccountService> ();
                        var userAccount = accountService.GetUserAccount (null, UserName);
                        if (userAccount != null)
                            UserID = userAccount.PrincipalID;
                    }
                }

                // paginations
                int start = httpRequest.Query.ContainsKey ("Start")
                    ? int.Parse (httpRequest.Query ["Start"].ToString ())
                    : 0;
                int count = (int) moneyModule.NumberOfTransactions(UserID, UUID.Zero);
                int maxPages = (int)(count / amountPerQuery) - 1;

                if (start == -1)
                    start = (int)(maxPages < 0 ? 0 : maxPages);

                vars.Add ("CurrentPage", start);
                vars.Add ("NextOne", start + 1 > maxPages ? start : start + 1);
                vars.Add ("BackOne", start - 1 < 0 ? 0 : start - 1);


                // Transaction Logs
                var timeNow = DateTime.Now.ToString ("HH:mm:ss");
                var dateFrom = DateTime.Parse (DateStart + " " + timeNow);
                var dateTo = DateTime.Parse (DateEnd + " " + timeNow);

                var transactions = new List<AgentTransfer>();
                if (moneyModule != null)
                {
                    if (UserID != UUID.Zero)
                        transactions = moneyModule.GetTransactionHistory (UserID, UUID.Zero, dateFrom, dateTo, (uint)start, amountPerQuery);
                    else
                        transactions = moneyModule.GetTransactionHistory (dateFrom, dateTo, (uint)start, amountPerQuery);
                }

                // data
                if (transactions.Count > 0)
                {
                    noDetails = "";

                    foreach (var transaction in transactions)
                    {
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
            } else
            {
                vars.Add ("CurrentPage", 0 );
                vars.Add ("NextOne", 0);
                vars.Add ("BackOne", 0);
            }

            if (transactionsList.Count == 0)
            {
                transactionsList.Add(new Dictionary<string, object> {
                    {"Date", ""},                   //Culture.LocaleDate(today,"MMM dd, hh:mm:ss")},
                    {"ToAgent", ""},
                    {"FromAgent", ""},
                    {"Description", translator.GetTranslatedString ("NoTransactionsText")},
                    {"Amount",""},
                    {"ToBalance",""}

                });
            }

            // always required data
            vars.Add("DateStart", DateStart );
            vars.Add ("DateEnd", DateEnd );
            vars.Add ("SearchUser", UserName);
            vars.Add("TransactionsList",transactionsList);
            vars.Add ("NoTransactionsText", noDetails);

            // labels
            vars.Add("TransactionsText", translator.GetTranslatedString("TransactionsText"));
            vars.Add("DateInfoText", translator.GetTranslatedString("DateInfoText"));
            vars.Add("DateStartText", translator.GetTranslatedString("DateStartText"));
            vars.Add("DateEndText", translator.GetTranslatedString("DateEndText"));
            vars.Add("SearchUserText", translator.GetTranslatedString("AvatarNameText"));

            vars.Add("TransactionDateText", translator.GetTranslatedString("TransactionDateText"));
            vars.Add("TransactionToAgentText", translator.GetTranslatedString("TransactionToAgentText"));
            vars.Add("TransactionFromAgentText", translator.GetTranslatedString("TransactionFromAgentText"));
            //vars.Add("TransactionTimeText", translator.GetTranslatedString("Time"));
            vars.Add("TransactionDetailText", translator.GetTranslatedString("TransactionDetailText"));
            vars.Add("TransactionAmountText", translator.GetTranslatedString("TransactionAmountText"));
            vars.Add("TransactionBalanceText", translator.GetTranslatedString("TransactionBalanceText"));

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
