﻿/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
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
using System.Linq;
using System.Net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Currency
{
    
    public class BaseCurrencyConnector : ConnectorBase, IBaseCurrencyConnector
    {
        #region Declares
        const string _REALM = "simple_currency";
        const string _REALMHISTORY = "simple_currency_history";
        const string _REALMPURCHASE = "simple_purchased";

        IGenericData m_gd;
        BaseCurrencyConfig m_config;
        ISyncMessagePosterService m_syncMessagePoster;
        IAgentInfoService m_userInfoService;
        IUserAccountService m_userAccountService;

        public string InWorldCurrency = "";
        public string RealCurrency = "";
        
        #endregion

        #region IWhiteCoreDataPlugin Members

        public string Name
        {
            get { return "IBaseCurrencyConnector"; }
        }

        public void Initialize(IGenericData GenericData, IConfigSource source, IRegistryCore registry,
                               string defaultConnectionString)
        {
            m_gd = GenericData;
            m_registry = registry;

            IConfig config = source.Configs["Currency"];
            if (config == null || source.Configs["Currency"].GetString("Module", "") != "BaseCurrency")
                return;

            IConfig gridInfo = source.Configs["GridInfoService"];
            if (gridInfo != null)
            {
                InWorldCurrency = gridInfo.GetString ("CurrencySymbol", String.Empty) + " ";
                RealCurrency = gridInfo.GetString ("RealCurrencySymbol", String.Empty) + " ";
            }

            if (source.Configs[Name] != null)
                defaultConnectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);

            if (GenericData != null)
                GenericData.ConnectToDatabase(defaultConnectionString, "SimpleCurrency", true);
            Framework.Utilities.DataManager.RegisterPlugin(Name, this);

            m_config = new BaseCurrencyConfig(config);

            Init(m_registry, Name, "", "/currency/", "CurrencyServerURI");

        }

        #endregion

        #region Service Members

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public BaseCurrencyConfig GetConfig()
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI");
            if (remoteValue != null || m_doRemoteOnly)
                return (BaseCurrencyConfig) remoteValue;

            return m_config;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public UserCurrency GetUserCurrency(UUID agentId)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", agentId);
            if (remoteValue != null || m_doRemoteOnly)
                return (UserCurrency) remoteValue;

            Dictionary<string, object> where = new Dictionary<string, object>(1);
            where["PrincipalID"] = agentId;
            List<string> query = m_gd.Query(new [] {"*"}, _REALM, new QueryFilter()
                                                                            {
                                                                                andFilters = where
                                                                            }, null, null, null);
            UserCurrency currency;
            if ((query == null) || (query.Count == 0))
            {
                currency = new UserCurrency(agentId, 0, 0, 0, false, 0);
                UserCurrencyCreate(agentId);
                return currency;
            }
            
            return new UserCurrency(query);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupBalance GetGroupBalance(UUID groupID)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupBalance) remoteValue;

            GroupBalance gb = new GroupBalance()
                                  {
                                      GroupFee = 0,
                                      LandFee = 0,
                                      ObjectFee = 0,
                                      ParcelDirectoryFee = 0,
                                      TotalTierCredits = 0,
                                      TotalTierDebit = 0,
                                      StartingDate = DateTime.UtcNow
                                  };
            Dictionary<string, object> where = new Dictionary<string, object>(1);
            where["PrincipalID"] = groupID;
            List<string> queryResults = m_gd.Query(new [] {"*"}, _REALM, new QueryFilter()
                                                                                   {
                                                                                       andFilters = where
                                                                                   }, null, null, null);

            if ((queryResults == null) || (queryResults.Count == 0))
            {
                GroupCurrencyCreate(groupID);
                return gb;
            }

            int.TryParse(queryResults[1], out gb.TotalTierCredits);
            return gb;
        }

        public int CalculateEstimatedCost(uint amount)
        {
            return Convert.ToInt32(
                Math.Round(((float.Parse(amount.ToString()) /
                            m_config.RealCurrencyConversionFactor) +
                            ((float.Parse(amount.ToString()) /
                            m_config.RealCurrencyConversionFactor) *
                            (m_config.AdditionPercentage / 10000.0)) +
                            (m_config.AdditionAmount / 100.0)) * 100));
        }

        public int CheckMinMaxTransferSettings(UUID agentID, uint amount)
        {
            amount = Math.Max(amount, (uint)m_config.MinAmountPurchasable);
            amount = Math.Min(amount, (uint)m_config.MaxAmountPurchasable);
            List<uint> recentTransactions = GetAgentRecentTransactions(agentID);

            long currentlyBought = recentTransactions.Sum((u) => u);
            return (int)Math.Min(amount, m_config.MaxAmountPurchasableOverTime - currentlyBought);
        }

        public bool InworldCurrencyBuyTransaction(UUID agentID, uint amount, IPEndPoint ep)
        {
            amount = (uint)CheckMinMaxTransferSettings(agentID, amount);
            if (amount == 0)
                return false;
            UserCurrencyTransfer(agentID, UUID.Zero, amount,
                                             "Currency Exchange", TransactionType.SystemGenerated, UUID.Zero);

            //Log to the database
            List<object> values = new List<object>
            {
                UUID.Random(),                         // TransactionID
                agentID.ToString(),                    // PrincipalID
                ep.ToString(),                         // IP
                amount,                                // Amount
                CalculateEstimatedCost(amount),        // Actual cost
                Utils.GetUnixTime(),                   // Created
                Utils.GetUnixTime()                    // Updated
            };
            m_gd.Insert(_REALMPURCHASE, values.ToArray());
            return true;
        }

        public List<uint> GetAgentRecentTransactions(UUID agentID)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["PrincipalID"] = agentID;
            DateTime now = DateTime.Now;
            RepeatType runevertype = (RepeatType)Enum.Parse(typeof(RepeatType), m_config.MaxAmountPurchasableEveryType);
            switch (runevertype)
            {
                case RepeatType.second:
                    now = now.AddSeconds(-m_config.MaxAmountPurchasableEveryAmount);
                    break;
                case RepeatType.minute:
                    now = now.AddMinutes(-m_config.MaxAmountPurchasableEveryAmount);
                    break;
                case RepeatType.hours:
                    now = now.AddHours(-m_config.MaxAmountPurchasableEveryAmount);
                    break;
                case RepeatType.days:
                    now = now.AddDays(-m_config.MaxAmountPurchasableEveryAmount);
                    break;
                case RepeatType.weeks:
                    now = now.AddDays(-m_config.MaxAmountPurchasableEveryAmount * 7);
                    break;
                case RepeatType.months:
                    now = now.AddMonths(-m_config.MaxAmountPurchasableEveryAmount);
                    break;
                case RepeatType.years:
                    now = now.AddYears(-m_config.MaxAmountPurchasableEveryAmount);
                    break;
            }
            filter.andGreaterThanEqFilters["Created"] = Utils.DateTimeToUnixTime(now);//Greater than the time that we are checking against
            filter.andLessThanEqFilters["Created"] = Utils.GetUnixTime();//Less than now
            List<string> query = m_gd.Query(new string[1] { "Amount" }, _REALMPURCHASE, filter, null, null, null);
            if (query == null)
                return new List<uint>();
            return query.ConvertAll<uint>(s=>uint.Parse(s));
        }

        // transactions...
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public uint NumberOfTransactions(UUID toAgentID, UUID fromAgentID)
        {
            QueryFilter filter = new QueryFilter();
            if (toAgentID != UUID.Zero)
                filter.andFilters["ToPrincipalID"] = toAgentID;
            if (fromAgentID != UUID.Zero)
                filter.andFilters["FromPrincipalID"] = fromAgentID;

   
            var transactions = m_gd.Query (new string[1] {"count(*)"}, _REALMHISTORY, filter, null, null, null);
            if ((transactions == null) || (transactions.Count == 0))
                return 0;
           
            return (uint)int.Parse (transactions[0]);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", dateStart, dateEnd, start, count);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<AgentTransfer>) remoteValue;

            QueryFilter filter = new QueryFilter();

            if (toAgentID != UUID.Zero)
                filter.andFilters["ToPrincipalID"] = toAgentID;
            if (fromAgentID != UUID.Zero)
                filter.andFilters["FromPrincipalID"] = fromAgentID;

            // back to utc please...
            dateStart = dateStart.ToUniversalTime ();
            dateEnd = dateEnd.ToUniversalTime ();

            filter.andGreaterThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateStart);    // from...
            filter.andLessThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateEnd);         //...to

            Dictionary<string, bool> sort = new Dictionary<string, bool>(1);
            sort["Created"] = false;        // descending order
            //sort["FromName"] = true;

            List<string> query = m_gd.Query(new string[] {"*"}, _REALMHISTORY, filter, sort, start, count);

            return ParseTransferQuery(query);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, int period, string periodType)
        {
            var dateStart = StartTransactionPeriod(period, periodType);
            var dateEnd = DateTime.Now;

            return GetTransactionHistory (toAgentID, fromAgentID, dateStart, dateEnd, null, null);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, int period, string periodType)
        {
            return GetTransactionHistory (toAgentID, UUID.Zero, period, periodType);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentTransfer> GetTransactionHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return GetTransactionHistory (UUID.Zero, UUID.Zero, dateStart, dateEnd, start, count);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentTransfer> GetTransactionHistory(int period, string periodType, uint? start, uint? count)
        {
            var dateStart = StartTransactionPeriod(period, periodType);
            var dateEnd = DateTime.Now;

            return GetTransactionHistory (dateStart, dateEnd, start, count);
        }

        // Purchases...
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public uint NumberOfPurchases(UUID UserID)
        {
            QueryFilter filter = new QueryFilter();
            if (UserID != UUID.Zero)
                filter.andFilters["PrincipalID"] = UserID;

            var purchases = m_gd.Query (new string[1] { "count(*)" }, _REALMPURCHASE, filter, null, null, null);
            if ((purchases == null) || (purchases.Count == 0))
                return 0;
            
            return (uint)int.Parse (purchases [0]);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentPurchase> GetPurchaseHistory(UUID UserID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", dateStart, dateEnd, start, count);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<AgentPurchase>) remoteValue;

            QueryFilter filter = new QueryFilter();

            if (UserID != UUID.Zero)
                filter.andFilters["PrincipalID"] = UserID;

            // back to utc please...
            dateStart = dateStart.ToUniversalTime ();
            dateEnd = dateEnd.ToUniversalTime ();

            filter.andGreaterThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateStart);    // from...
            filter.andLessThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateEnd);         //...to


            Dictionary<string, bool> sort = new Dictionary<string, bool>(1);
            //sort["PrincipalID"] = true;
            sort["Created"] = false;        // descending order

            List<string> query = m_gd.Query(new string[] {"*"}, _REALMPURCHASE, filter, sort, start, count);

            return ParsePurchaseQuery(query);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentPurchase> GetPurchaseHistory(UUID toAgentID, int period, string periodType)
        {
            var dateStart = StartTransactionPeriod(period, periodType);
            var dateEnd = DateTime.Now;

            return GetPurchaseHistory (toAgentID, dateStart, dateEnd, null, null);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentPurchase> GetPurchaseHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return GetPurchaseHistory (UUID.Zero, dateStart, dateEnd, start, count);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentPurchase> GetPurchaseHistory(int period, string periodType, uint? start, uint? count)
        {
            var dateStart = StartTransactionPeriod(period, periodType);
            var dateEnd = DateTime.Now;

            return GetPurchaseHistory (UUID.Zero, dateStart, dateEnd, start, count);
        }


            
        public bool UserCurrencyTransfer(UUID toID, UUID fromID, uint amount,
                                         string description, TransactionType type, UUID transactionID)
        {
            return UserCurrencyTransfer(toID, fromID, UUID.Zero, "", UUID.Zero, "", amount, description, type, transactionID);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool UserCurrencyTransfer(UUID toID, UUID fromID, UUID toObjectID, string toObjectName, UUID fromObjectID,
            string fromObjectName, uint amount, string description, TransactionType type, UUID transactionID)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", toID, fromID, toObjectID, toObjectName, fromObjectID,
                fromObjectName, amount, description, type, transactionID);
            if (remoteValue != null || m_doRemoteOnly)
                return (bool) remoteValue;

            UserCurrency toCurrency = GetUserCurrency(toID);
            UserCurrency fromCurrency = fromID == UUID.Zero ? null : GetUserCurrency(fromID);
            if (toCurrency == null)
                return false;
            if (fromCurrency != null)
            {
                //Check to see whether they have enough money
                if ((int) fromCurrency.Amount - (int) amount < 0)
                    return false; //Not enough money
                fromCurrency.Amount -= amount;

                UserCurrencyUpdate(fromCurrency, true);
            }
            if (fromID == toID) toCurrency = GetUserCurrency(toID);

            //Update the user whose getting paid
            toCurrency.Amount += amount;
            UserCurrencyUpdate(toCurrency, true);

            //Must send out notifications to the users involved so that they get the updates
            if (m_userInfoService == null)
            {
                m_userInfoService = m_registry.RequestModuleInterface<IAgentInfoService>();
                m_userAccountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            }
            if (m_userInfoService != null)
            {
                UserInfo toUserInfo = m_userInfoService.GetUserInfo(toID.ToString());
                UserInfo fromUserInfo = fromID == UUID.Zero ? null : m_userInfoService.GetUserInfo(fromID.ToString());
                UserAccount toAccount = m_userAccountService.GetUserAccount(null, toID);
                UserAccount fromAccount = m_userAccountService.GetUserAccount(null, fromID);

                if (m_config.SaveTransactionLogs)
                    AddTransactionRecord((
                        transactionID == UUID.Zero ? UUID.Random() : transactionID), 
                        description,
                        toID,
                        fromID,
                        amount,
                        type,
                        (toCurrency == null ? 0 : toCurrency.Amount), 
                        (fromCurrency == null ? 0 : fromCurrency.Amount),
                        (toAccount == null ? "System" : toAccount.Name), 
                        (fromAccount == null ? "System" : fromAccount.Name),
                        toObjectName,
                        fromObjectName,
                        (fromUserInfo == null ? UUID.Zero : fromUserInfo.CurrentRegionID)
                    );

                if (fromID == toID)
                {
                    if (toUserInfo != null && toUserInfo.IsOnline)
                        SendUpdateMoneyBalanceToClient(toID, transactionID, toUserInfo.CurrentRegionURI, toCurrency.Amount,
                            toAccount == null ? "" : (toAccount.Name + " paid you $" + amount + (description == "" ? "" : ": " + description)));
                }
                else
                {
                    if (toUserInfo != null && toUserInfo.IsOnline)
                    {
                        SendUpdateMoneyBalanceToClient(toID, transactionID, toUserInfo.CurrentRegionURI, toCurrency.Amount,
                            fromAccount == null ? "" : (fromAccount.Name + " paid you $" + amount + (description == "" ? "" : ": " + description)));
                    }
                    if (fromUserInfo != null && fromUserInfo.IsOnline)
                    {
                        SendUpdateMoneyBalanceToClient(fromID, transactionID, fromUserInfo.CurrentRegionURI, fromCurrency.Amount,
                            "You paid " + (toAccount == null ? "" : toAccount.Name) + " $" + amount);
                    }
                }
            }
            return true;
        }

        public void SendUpdateMoneyBalanceToClient(UUID toID, UUID transactionID, string serverURI, uint balance, string message)
        {
            if (m_syncMessagePoster == null)
            {
                m_syncMessagePoster = m_registry.RequestModuleInterface<ISyncMessagePosterService>();
            }

            if (m_syncMessagePoster != null)
            {
                OSDMap map = new OSDMap ();
                map ["Method"] = "UpdateMoneyBalance";
                map ["AgentID"] = toID;
                map ["Amount"] = balance;
                map ["Message"] = message;
                map ["TransactionID"] = transactionID;
                m_syncMessagePoster.Post (serverURI, map);
            }
        }

        #endregion

        #region Helper Methods

        // Method Added By Alicia Raven
        void AddTransactionRecord(UUID TransID, string Description, UUID ToID, UUID FromID, uint Amount,
            TransactionType TransType, uint ToBalance, uint FromBalance, string ToName, string FromName, string toObjectName, string fromObjectName, UUID regionID)
        {
            if(Amount > m_config.MaxAmountBeforeLogging)
                m_gd.Insert(_REALMHISTORY, new object[] {
                    TransID,
                    Description ?? "",
                    FromID.ToString (),
                    FromName,
                    ToID.ToString (),
                    ToName,
                    Amount,
                    (int)TransType,
                    Util.UnixTimeSinceEpoch (),
                    ToBalance,
                    FromBalance,
                    toObjectName ?? "",
                    fromObjectName ?? "",
                    regionID 
                });
        }

        void UserCurrencyUpdate (UserCurrency agent, bool full)
        {
            if (full)
                m_gd.Update (_REALM,
                    new Dictionary<string, object> {
                        { "LandInUse", agent.LandInUse },
                        { "Tier", agent.Tier },
                        { "IsGroup", agent.IsGroup },
                        { "Amount", agent.Amount },
                        { "StipendsBalance", agent.StipendsBalance }
                    },
                    null,
                    new QueryFilter () {
                        andFilters = new Dictionary<string, object> {
                            { "PrincipalID", agent.PrincipalID }
                        }
                    },
                    null,
                    null
                );
            else
                m_gd.Update (_REALM,
                    new Dictionary<string, object> {
                        { "LandInUse", agent.LandInUse },
                        { "Tier", agent.Tier },
                        { "IsGroup", agent.IsGroup }
                    },
                    null,
                    new QueryFilter () {
                        andFilters = new Dictionary<string, object> {
                            { "PrincipalID", agent.PrincipalID }
                        }
                    },
                    null,
                    null)
                ;
        }

        void UserCurrencyCreate(UUID agentId)
        {
			// Check if this agent has a user account, if not assume its a bot and exit
			UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(new List<UUID> { UUID.Zero }, agentId);
            if (account != null)
            {
                m_gd.Insert(_REALM, new object[] {agentId.ToString(), 0, 0, 0, 0, 0});
            }
        }

        void GroupCurrencyCreate(UUID groupID)
        {
            m_gd.Insert(_REALM, new object[] {groupID.ToString(), 0, 0, 0, 1, 0});
        }

        DateTime StartTransactionPeriod( int period, string periodType)
        {
            DateTime then = DateTime.Now;
            switch (periodType)
            {
            case "sec":
                then = then.AddSeconds(-period);
                break;
            case "min":
                then = then.AddMinutes(-period);
                break;
            case "hour":
                then = then.AddHours(-period);
                break;
            case "day":
                then = then.AddDays(-period);
                break;
            case "week":
                then = then.AddDays(-period * 7);
                break;
            case "month":
                then = then.AddMonths(-period);
                break;
            case "year":
                then = then.AddYears(-period);
                break;
            }

            return then;
        }

        static List<AgentTransfer> ParseTransferQuery(List<string> query)
        {
           var transferList = new List<AgentTransfer>();

            for (int i = 0; i < query.Count; i += 14)
            {
                AgentTransfer transfer = new AgentTransfer ();

                transfer.ID = UUID.Parse(query[i + 0]);
                transfer.Description = query[i + 1];
                transfer.FromAgent = UUID.Parse(query[i + 2]);
                transfer.FromAgentName = query[i + 3];
                transfer.ToAgent = UUID.Parse(query[i + 4]);
                transfer.ToAgentName = query[i + 5];
                transfer.Amount = Int32.Parse(query[i + 6]);
                transfer.TransferType = (TransactionType) Int32.Parse(query[i + 7]);
                transfer.TransferDate = Utils.UnixTimeToDateTime((uint) Int32.Parse(query[i + 8]));
                transfer.ToBalance = Int32.Parse(query[i + 9]);
                transfer.FromBalance = Int32.Parse(query[i + 10]);
                transfer.FromObjectName = query[i + 11];
                transfer.ToObjectName = query[i + 12];
                transfer.RegionName = query[i + 13];

                transferList.Add(transfer);
            }

            return transferList;
        }


        static List<AgentPurchase> ParsePurchaseQuery(List<string> query)
        {
            var purchaseList = new List<AgentPurchase>();

            for (int i = 0; i < query.Count; i += 14)
            {
                AgentPurchase purchase = new AgentPurchase ();

                purchase.ID = UUID.Parse(query[i + 0]);
                purchase.AgentID = UUID.Parse(query[i + 1]);
                purchase.IP = query[i + 2];
                purchase.Amount = Int32.Parse(query[i + 3]);
                purchase.RealAmount = Int32.Parse(query[i + 4]);
                purchase.PurchaseDate = Utils.UnixTimeToDateTime((uint) Int32.Parse(query[i + 5]));
                purchase.UpdateDate = Utils.UnixTimeToDateTime((uint) Int32.Parse(query[i + 6]));

                purchaseList.Add(purchase);
            }

            return purchaseList;
        }
            
        #endregion

    }
}
