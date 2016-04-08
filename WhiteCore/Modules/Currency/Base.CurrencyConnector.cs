/*
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
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.DatabaseInterfaces;

namespace WhiteCore.Modules.Currency
{
    
    public class BaseCurrencyConnector : ConnectorBase, IBaseCurrencyConnector
    {
        #region Declares
        const string _REALM = "user_currency";
        const string _REALMHISTORY = "user_currency_history";
        const string _REALMPURCHASE = "user_purchased";
        const string _GROUPREALM = "group_currency";
        const string _GROUPREALMHISTORY = "group_currency_history";

        IGenericData GD;
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
            GD = GenericData;
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

        #region groupcurrency

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public GroupBalance GetGroupBalance(UUID groupID)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", groupID);
            if (remoteValue != null || m_doRemoteOnly)
                return (GroupBalance) remoteValue;

            GroupBalance gb = new GroupBalance () {
                GroupFee = 0,
                LandFee = 0,
                ObjectFee = 0,
                ParcelDirectoryFee = 0,
                TotalTierCredits = 0,
                TotalTierDebit = 0,
                Balance = 0,
                StartingDate = DateTime.UtcNow
            };
            Dictionary<string, object> where = new Dictionary<string, object> (1);
            where ["GroupID"] = groupID;

            List<string> queryResults = GD.Query (new [] { "*" }, _GROUPREALM,
                new QueryFilter () { andFilters = where }, null, null, null);

            if (queryResults.Count == 0)
            {
                GroupCurrencyCreate(groupID);
                return gb;
            }

            return ParseGroupBalance(queryResults);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<GroupAccountHistory> GetGroupTransactions(UUID groupID, UUID fromAgentID,
            int currentInterval, int intervalDays)
        {
            //return new List<GroupAccountHistory>();
            object remoteValue = DoRemoteByURL("CurrencyServerURI", groupID, fromAgentID, currentInterval, intervalDays);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<GroupAccountHistory>) remoteValue;

            QueryFilter filter = new QueryFilter();

            if (groupID != UUID.Zero)
                filter.andFilters["GroupID"] = groupID;
            if (fromAgentID != UUID.Zero)
                filter.andFilters["AgentID"] = fromAgentID;

            // calculate interval dates
            var dStart = DateTime.Now.AddDays(-currentInterval*intervalDays);
            var dEnd = dStart.AddDays (intervalDays);

            // back to UTC please...
            var dateStart = dStart.ToUniversalTime ();
            var dateEnd = dEnd.ToUniversalTime ();

            filter.andGreaterThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateStart);    // from...
            filter.andLessThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateEnd);         //...to

            Dictionary<string, bool> sort = new Dictionary<string, bool>(1);
            sort["Created"] = false;        // descending order

            List<string> query = GD.Query (new string[] { "*" }, _GROUPREALMHISTORY, filter, sort, null, null);

            return ParseGroupTransferQuery(query);
        }

        public bool GroupCurrencyTransfer(UUID groupID, UUID userId, bool payUser, string toObjectName, UUID fromObjectID,
            string fromObjectName, int amount, string description, TransactionType type, UUID transactionID)
        {
            GroupBalance gb = new GroupBalance () {
                StartingDate = DateTime.UtcNow
            };
            UserCurrency fromCurrency = userId == UUID.Zero ? null : GetUserCurrency(userId);

            // Groups (legacy) should not receive stipends
            if (type == TransactionType.StipendPayment) 
                return false;

            if (fromCurrency != null)
            {
                // Normal users cannot have a credit balance.. check to see whether they have enough money
                if ((int)fromCurrency.Amount - amount < 0)
                    return false; // Not enough money
            }

            // is thiis a payment to the group or to the user?
            if (payUser)
                amount = -1 * amount;   
            
            // user payment
            fromCurrency.Amount -= (uint) amount;
            UserCurrencyUpdate (fromCurrency, true);

            // track specific group fees
            switch (type)
            {
            case TransactionType.GroupJoin:
                gb.GroupFee += amount;
                break;
            case TransactionType.LandAuction:
                gb.LandFee += amount;
                break;
            case TransactionType.ParcelDirFee:
                gb.ParcelDirectoryFee += amount;
                break;
            }

            if (payUser)
                gb.TotalTierDebit -= amount;          // not sure if this the correct place yet? Are these currency or land credits?
            else
                gb.TotalTierCredits += amount;        // .. or this?

            // update the group balance
            gb.Balance += amount;                
            GroupCurrencyUpdate(groupID, gb, true);

            //Must send out notifications to the users involved so that they get the updates
            if (m_userInfoService == null)
            {
                m_userInfoService = m_registry.RequestModuleInterface<IAgentInfoService>();
                m_userAccountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            }
            if (m_userInfoService != null)
            {
                UserInfo agentInfo = userId == UUID.Zero ? null : m_userInfoService.GetUserInfo(userId.ToString());
                UserAccount agentAccount = m_userAccountService.GetUserAccount(null, userId);
                var groupService = Framework.Utilities.DataManager.RequestPlugin<IGroupsServiceConnector> ();
                var groupInfo = groupService.GetGroupRecord (userId, groupID, null);
                var groupName = "Unknown";

                if (groupInfo != null)
                    groupName = groupInfo.GroupName;

                if (m_config.SaveTransactionLogs)
                    AddGroupTransactionRecord(
                        (transactionID == UUID.Zero ? UUID.Random() : transactionID), 
                        description,
                        groupID,
                        groupName, 
                        userId,
                        (agentAccount == null ? "System" : agentAccount.Name),
                        amount,
                        type,
                        gb.TotalTierCredits,     //assume this it the 'total credit for the group but it may be land tier credit??
                        (int) fromCurrency.Amount,
                        toObjectName,
                        fromObjectName,
                        (agentInfo == null ? UUID.Zero : agentInfo.CurrentRegionID)
                    );

                if (agentInfo != null && agentInfo.IsOnline)
                {
                    SendUpdateMoneyBalanceToClient(userId, transactionID, agentInfo.CurrentRegionURI, fromCurrency.Amount,
                    "You paid " + groupName + " " +InWorldCurrency + amount);
                }
            }
            return true;
        }

        #endregion //group currency

        #region usercurrency

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public UserCurrency GetUserCurrency(UUID agentId)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", agentId);
            if (remoteValue != null || m_doRemoteOnly)
                return (UserCurrency) remoteValue;

            Dictionary<string, object> where = new Dictionary<string, object> (1);
            where ["PrincipalID"] = agentId;
            List<string> query = GD.Query (new [] { "*" }, _REALM, new QueryFilter () {
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
            
            UserCurrencyTransfer(
                agentID,
                UUID.Zero,
                amount,
                "Currency Exchange",
                TransactionType.BuyMoney,
                UUID.Zero
            );

            //Log to the database
            List<object> values = new List<object> {
                UUID.Random (),                         // TransactionID
                agentID.ToString (),                    // PrincipalID
                ep.ToString (),                         // IP
                amount,                                // Amount
                CalculateEstimatedCost(amount),        // Actual cost
                Utils.GetUnixTime(),                   // Created
                Utils.GetUnixTime()                    // Updated
            };

            GD.Insert(_REALMPURCHASE, values.ToArray());
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
            List<string> query = GD.Query(new string[1] { "Amount" }, _REALMPURCHASE, filter, null, null, null);
            if (query == null)
                return new List<uint> ();
            return query.ConvertAll<uint> (s => uint.Parse (s));
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

   
            var transactions = GD.Query (new string[1] {"count(*)"}, _REALMHISTORY, filter, null, null, null);
            if ((transactions == null) || (transactions.Count == 0))
                return 0;
           
            return (uint)int.Parse (transactions[0]);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", toAgentID, fromAgentID, dateStart, dateEnd, start, count);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<AgentTransfer>) remoteValue;

            QueryFilter filter = new QueryFilter();

            if (toAgentID != UUID.Zero)
                filter.andFilters["ToPrincipalID"] = toAgentID;
            if (fromAgentID != UUID.Zero)
                filter.andFilters["FromPrincipalID"] = fromAgentID;

            // back to UTC please...
            dateStart = dateStart.ToUniversalTime ();
            dateEnd = dateEnd.ToUniversalTime ();

            filter.andGreaterThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateStart);    // from...
            filter.andLessThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateEnd);         //...to

            Dictionary<string, bool> sort = new Dictionary<string, bool>(1);
            sort["Created"] = false;        // descending order
            //sort["FromName"] = true;

            List<string> query = GD.Query (new string[] { "*" }, _REALMHISTORY, filter, sort, start, count);

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

            var purchases = GD.Query (new string[1] { "count(*)" }, _REALMPURCHASE, filter, null, null, null);
            if ((purchases == null) || (purchases.Count == 0))
                return 0;
            
            return (uint)int.Parse (purchases [0]);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<AgentPurchase> GetPurchaseHistory(UUID UserID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", UserID, dateStart, dateEnd, start, count);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<AgentPurchase>) remoteValue;

            QueryFilter filter = new QueryFilter();

            if (UserID != UUID.Zero)
                filter.andFilters["PrincipalID"] = UserID;

            // back to UTC please...
            dateStart = dateStart.ToUniversalTime ();
            dateEnd = dateEnd.ToUniversalTime ();

            filter.andGreaterThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateStart);    // from...
            filter.andLessThanEqFilters["Created"] = Utils.DateTimeToUnixTime(dateEnd);         //...to


            Dictionary<string, bool> sort = new Dictionary<string, bool>(1);
            //sort["PrincipalID"] = true;
            sort["Created"] = false;        // descending order

            List<string> query = GD.Query (new string[] { "*" }, _REALMPURCHASE, filter, sort, start, count);

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

        // This is the main entry point for currency transactions
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool UserCurrencyTransfer(UUID toID, UUID fromID, UUID toObjectID, string toObjectName, UUID fromObjectID,
            string fromObjectName, uint amount, string description, TransactionType type, UUID transactionID)
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI", toID, fromID, toObjectID, toObjectName, fromObjectID,
                fromObjectName, amount, description, type, transactionID);
            if (remoteValue != null || m_doRemoteOnly)
                return (bool) remoteValue;

            // check if the 'toID' is a group
            var groupService = Framework.Utilities.DataManager.RequestPlugin<IGroupsServiceConnector> ();
            if (groupService.IsGroup(toID))
                return GroupCurrencyTransfer(toID, fromID, false, toObjectName, fromObjectID,
                    fromObjectName, (int) amount, description, type, transactionID);
                
            // use transfer
            UserCurrency toCurrency = GetUserCurrency(toID);
            UserCurrency fromCurrency = fromID == UUID.Zero ? null : GetUserCurrency(fromID);

            if (toCurrency == null)
                return false;

            // Groups (legacy) should not receive stipends
            if ((type == TransactionType.StipendPayment) && toCurrency.IsGroup)
                return false;
            
            if (fromCurrency != null)
            {
                if (fromID == (UUID)Constants.BankerUUID)
                {
                    // payment from the Banker
                    // 20150730 - greythane - need to fiddle 'the books' as -ve balances are not currently available
                    fromCurrency.Amount += amount;
                } else
                {
                    // Normal users cannot have a credit balance.. check to see whether they have enough money
                    if ((int)fromCurrency.Amount - (int)amount < 0)
                        return false; // Not enough money
                }

                // subtract this payment
                fromCurrency.Amount -= amount;
                UserCurrencyUpdate (fromCurrency, true);
            }

            if (fromID == toID)
                toCurrency = GetUserCurrency (toID);

            //Update the user who is getting paid
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
                    AddTransactionRecord(
                        (transactionID == UUID.Zero ? UUID.Random() : transactionID), 
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
                            toAccount == null ? "" : (toAccount.Name + " paid you " + InWorldCurrency + amount + (description == "" ? "" : ": " + description)));
                } else
                {
                    if (toUserInfo != null && toUserInfo.IsOnline)
                    {
                        SendUpdateMoneyBalanceToClient(toID, transactionID, toUserInfo.CurrentRegionURI, toCurrency.Amount,
                            fromAccount == null ? "" : (fromAccount.Name + " paid you " + InWorldCurrency  + amount + (description == "" ? "" : ": " + description)));
                    }
                    if (fromUserInfo != null && fromUserInfo.IsOnline)
                    {
                        SendUpdateMoneyBalanceToClient(fromID, transactionID, fromUserInfo.CurrentRegionURI, fromCurrency.Amount,
                            "You paid " + (toAccount == null ? "" : toAccount.Name) + " " + InWorldCurrency + amount);
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
        #endregion  // user currnency
        #endregion  // Service Members

        #region Helper Methods

        // Method Added By Alicia Raven
        void AddTransactionRecord(UUID transID, string description, UUID toID, UUID fromID, uint amount,
            TransactionType transType, uint toBalance, uint fromBalance, string toName, string fromName, string toObjectName, string fromObjectName, UUID regionID)
        {
            if(amount > m_config.MaxAmountBeforeLogging)
                GD.Insert(_REALMHISTORY, new object[] {
                    transID,
                    description ?? "",
                    fromID.ToString (),
                    fromName,
                    toID.ToString (),
                    toName,
                    amount,
                    (int)transType,
                    Util.UnixTimeSinceEpoch (),
                    toBalance,
                    fromBalance,
                    toObjectName ?? "",
                    fromObjectName ?? "",
                    regionID 
                });
        }

        void AddGroupTransactionRecord(UUID transID, string description, UUID groupID, string groupName, UUID userID, string userName, int amount,
            TransactionType transType, int groupBalance, int userBalance, string toObjectName, string fromObjectName, UUID regionID)
        {
            if(amount > m_config.MaxAmountBeforeLogging)
                GD.Insert(_GROUPREALMHISTORY, new object[] {
                    transID,
                    description ?? "",
                    groupID.ToString (),
                    groupName,
                    userID.ToString (),
                    userName,
                    amount,
                    (int)transType,
                    Util.UnixTimeSinceEpoch (),
                    groupBalance,
                    userBalance,
                    toObjectName ?? "",             // not used?
                    fromObjectName ?? "",           // not used?
                    regionID 
                });
        }

        void UserCurrencyUpdate (UserCurrency agent, bool full)
        {
            if (full)
                GD.Update (_REALM,
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
                GD.Update (_REALM,
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
                    null);
        }

        void UserCurrencyCreate(UUID agentId)
        {
			// Check if this agent has a user account, if not assume its a bot and exit
			UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(new List<UUID> { UUID.Zero }, agentId);
            if (account != null)
            {
                GD.Insert(_REALM, new object[] {agentId.ToString(), 0, 0, 0, 0, 0});
            }
        }

        void GroupCurrencyCreate(UUID groupID)
        {
            var qryResults = GD.Query (new [] { "*" }, _GROUPREALM,
                new QueryFilter () { andFilters = new Dictionary<string, object>{{"GroupID", groupID}}}, null, null, null);
            
            if (qryResults.Count == 0)
                GD.Insert(_GROUPREALM, new object[] {groupID.ToString(), 0, 0, 0, 0, 0, 0, 0});
        }

        static GroupBalance ParseGroupBalance(List<string> queryResults)
        {
            GroupBalance gb = new GroupBalance ();
           /* ColDef("GroupID", ColumnTypes.String36),
            ColDef("Balance", ColumnTypes.Integer30),
            ColDef("GroupFee", ColumnTypes.Integer30),
            ColDef("LandFee", ColumnTypes.Integer30),
            ColDef("ObjectFee", ColumnTypes.Integer30),
            ColDef("ParcelDirectoryFee", ColumnTypes.Integer30),
            ColDef("TierCredits", ColumnTypes.Integer30),
            ColDef("TierDebits", ColumnTypes.Integer30),
            */

            int.TryParse (queryResults [1], out gb.Balance);
            int.TryParse (queryResults [2], out gb.GroupFee);
            int.TryParse (queryResults [3], out gb.LandFee);
            int.TryParse (queryResults [4], out gb.ObjectFee);
            int.TryParse (queryResults [5], out gb.ParcelDirectoryFee);
            int.TryParse (queryResults [6], out gb.TotalTierCredits);
            int.TryParse (queryResults [7], out gb.TotalTierDebit);

            gb.StartingDate = DateTime.UtcNow;

            return gb;
        }

        static List<GroupAccountHistory> ParseGroupTransferQuery(List<string> query)
        {
            var transferList = new List<GroupAccountHistory>();
/*
        int Amount;
        string Description;
        string TimeString;
        string UserCausingCharge;
        bool Payment
*/
            for (int i = 0; i < query.Count; i += 14)
            {
                GroupAccountHistory transfer = new GroupAccountHistory ();

                /* actual saved details but not all needed for group history
                transfer.ID = UUID.Parse(query[i + 0]);
                transfer.Description = query[i + 1];
                transfer.GroupID = UUID.Parse(query[i + 2]);
                transfer.GroupName = query[i + 3];
                transfer.AgentID = UUID.Parse(query[i + 4]);
                transfer.AgentName = query[i + 5];
                transfer.Amount = Int32.Parse(query[i + 6]);
                transfer.TransferType = (TransactionType) Int32.Parse(query[i + 7]);
                transfer.TransferDate = Utils.UnixTimeToDateTime((uint) Int32.Parse(query[i + 8]));
                transfer.ToBalance = Int32.Parse(query[i + 9]);
                transfer.FromBalance = Int32.Parse(query[i + 10]);
                transfer.FromObjectName = query[i + 11];
                transfer.ToObjectName = query[i + 12];
                transfer.RegionName = query[i + 13];
                */

                transfer.Amount = Int32.Parse(query[i + 6]);
                transfer.Description = query[i + 1];
                transfer.TimeString = Utils.UnixTimeToDateTime((uint) Int32.Parse(query[i + 8])).ToString();
                transfer.UserCausingCharge = query[i + 5];
                transfer.Payment = (TransactionType)Int32.Parse (query [i + 7]) != TransactionType.StipendPayment; // This might need work

                transferList.Add(transfer);
            }

            return transferList;
        }

        void GroupCurrencyUpdate (UUID groupID, GroupBalance gb, bool full)
        {
            if (full)
                GD.Update (_GROUPREALM,
                    new Dictionary<string, object> {
                    { "GroupFee", gb.GroupFee },
                    { "LandFee", gb.LandFee },
                    { "ObjectFee", gb.ObjectFee },
                    { "ParcelDirectoryFee", gb.ParcelDirectoryFee },
                    { "TotalTierCredits", gb.TotalTierCredits },
                    { "TotalTierDebit", gb.TotalTierDebit },
                    { "Balance", gb.Balance }
                },
                    null,
                    new QueryFilter () {
                    andFilters = new Dictionary<string, object> {
                        { "GroupID", groupID }
                    }
                },
                    null,
                    null
                );
            else
                GD.Update (_GROUPREALM,
                    new Dictionary<string, object> {
                    { "TotalTierCredits", gb.TotalTierCredits },
                    { "TotalTierDebit", gb.TotalTierDebit }
                },
                    null,
                    new QueryFilter () {
                    andFilters = new Dictionary<string, object> {
                        { "GroupID", groupID }
                    }
                },
                    null,
                    null);
        }


        DateTime StartTransactionPeriod (int period, string periodType)
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
