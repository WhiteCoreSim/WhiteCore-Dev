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
using Nini.Config;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class UserPurchasesPage : IWebInterfacePage
    {
        public string[] FilePath {
            get {
                return new[]
                           {
                               "html/user/purchases.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                                OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                                ITranslator translator, out string response) {
            response = null;
            IConfig gridInfo = webInterface.Registry.RequestModuleInterface<ISimulationBase>().ConfigSource.Configs["GridInfoService"];
            var inWorldCurrency = gridInfo.GetString("CurrencySymbol", string.Empty) + " ";
            var realCurrency = gridInfo.GetString("RealCurrencySymbol", string.Empty) + " ";

            var vars = new Dictionary<string, object>();
            var purchasesList = new List<Dictionary<string, object>>();

            var today = DateTime.Now;
            var thirtyDays = today.AddDays(-7);
            string dateStart = thirtyDays.ToShortDateString();
            string dateEnd = today.ToShortDateString();


            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule>();
            string noDetails = translator.GetTranslatedString("NoPurchasesText");

            // Check if we're looking at the standard page or the submitted one
            if (requestParameters.ContainsKey("search")) {
                if (requestParameters.ContainsKey("date_start"))
                    dateStart = requestParameters["date_start"].ToString();
                if (requestParameters.ContainsKey("date_end"))
                    dateEnd = requestParameters["date_end"].ToString();

            }

            UserAccount user = Authenticator.GetAuthentication(httpRequest);
            if (user == null) {
                response = webInterface.UserMsg("!Error validating user", true);
                return null;
            }

            // Purchases Logs
            var timeNow = DateTime.Now.ToString("HH:mm:ss");
            var dateFrom = DateTime.Parse(dateStart + " " + timeNow);
            var dateTo = DateTime.Parse(dateEnd + " " + timeNow);
            TimeSpan period = dateTo.Subtract(dateFrom);

            var purchases = new List<AgentPurchase>();
            if (moneyModule != null)
                purchases = moneyModule.GetPurchaseHistory(user.PrincipalID, dateFrom, dateTo, null, null);

            // data
            if (purchases.Count > 0) {
                noDetails = "";

                foreach (var purchase in purchases) {
                    purchasesList.Add(new Dictionary<string, object> {
                        { "ID", purchase.ID },
                        { "AgentID", purchase.AgentID },
                        { "AgentName", user.Name },
                        { "LoggedIP", purchase.IP },
                        { "Description", "Purchase" },
                        { "Amount",purchase.Amount },
                        { "RealAmount",((float) purchase.RealAmount/100).ToString("0.00") },
                        { "PurchaseDate", Culture.LocaleDate (purchase.PurchaseDate.ToLocalTime(), "MMM dd, hh:mm:ss tt") },
                        { "UpdateDate", Culture.LocaleDate (purchase.UpdateDate.ToLocalTime(), "MMM dd, hh:mm:ss tt") }

                    });
                }
            }

            if (purchasesList.Count == 0) {

                purchasesList.Add(new Dictionary<string, object> {
                    {"ID", ""},
                    {"AgentID", ""},
                    {"AgentName", ""},
                    {"LoggedIP", ""},
                    {"Description",  translator.GetTranslatedString ("NoPurchasesText")},
                    {"Amount",""},
                    {"RealAmount",""},
                    {"PurchaseDate",""},
                    {"UpdateDate", ""}

                });
            }

            // always required data
            vars.Add("DateStart", dateStart);
            vars.Add("DateEnd", dateEnd);
            vars.Add("Period", period.TotalDays + " " + translator.GetTranslatedString("DaysText"));
            vars.Add("PurchasesList", purchasesList);
            vars.Add("NoPurchasesText", noDetails);

            // labels
            vars.Add("UserName", user.Name);
            vars.Add("PurchasesText", translator.GetTranslatedString("PurchasesText"));
            vars.Add("DateTimeFormat", Culture.DateTimeFormatInfo);
            vars.Add("DateInfoText", translator.GetTranslatedString("DateInfoText"));
            vars.Add("DateStartText", translator.GetTranslatedString("DateStartText"));
            vars.Add("DateEndText", translator.GetTranslatedString("DateEndText"));
            vars.Add("DaysText", translator.GetTranslatedString("DateEndText"));
            vars.Add("SearchUserText", translator.GetTranslatedString("AvatarNameText"));
            vars.Add("Search", translator.GetTranslatedString("Search"));

            vars.Add("PurchaseAgentText", translator.GetTranslatedString("TransactionToAgentText"));
            vars.Add("PurchaseDateText", translator.GetTranslatedString("TransactionDateText"));
            vars.Add("PurchaseUpdateDateText", translator.GetTranslatedString("TransactionDateText"));
            // vars.Add("PurchaseTimeText", translator.GetTranslatedString("Time"));
            vars.Add("PurchaseDetailText", translator.GetTranslatedString("TransactionDetailText"));
            vars.Add("LoggedIPText", translator.GetTranslatedString("LoggedIPText"));
            vars.Add("PurchaseAmountText", inWorldCurrency + translator.GetTranslatedString("TransactionAmountText"));
            vars.Add("PurchaseRealAmountText", realCurrency + translator.GetTranslatedString("PurchaseCostText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text) {
            text = "";
            return false;
        }
    }
}
