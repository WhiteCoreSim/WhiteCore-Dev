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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Simple.Currency
{
    
    public class SimpleCurrencyConnector : ConnectorBase, ISimpleCurrencyConnector
    {
        #region Declares
        private const string _REALM = "simple_currency";
        private const string _REALMHISTORY = "simple_currency_history";
        private const string _REALMPURCHASE = "simple_purchased";

        private IGenericData m_gd;
        private SimpleCurrencyConfig m_config;
        private ISyncMessagePosterService m_syncMessagePoster;
        private IAgentInfoService m_userInfoService;
        private string InWorldCurrency = "";
        private string RealCurrency = "";
        
        #endregion

        #region IWhiteCoreDataPlugin Members

        public string Name
        {
            get { return "ISimpleCurrencyConnector"; }
        }

        public void Initialize(IGenericData GenericData, IConfigSource source, IRegistryCore registry,
                               string defaultConnectionString)
        {
            m_gd = GenericData;
            m_registry = registry;

            IConfig config = source.Configs["Currency"];
            if (config == null || source.Configs["Currency"].GetString("Module", "") != "SimpleCurrency")
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
            DataManager.RegisterPlugin(Name, this);

            m_config = new SimpleCurrencyConfig(config);

            Init(m_registry, Name, "", "/currency/", "CurrencyServerURI");

            if (!m_doRemoteCalls)
            {
                MainConsole.Instance.Commands.AddCommand(
                    "money add",
                    "money add",
                    "Adds money to a user's account.",
                    AddMoney, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "money set",
                    "money set",
                    "Sets the amount of money a user has.",
                    SetMoney, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "money get",
                    "money get",
                    "Gets the amount of money a user has.",
                    GetMoney, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "show user transactions",
                    "show user transactionst",
                    "Display user transactions for a period.",
                    HandleShowTransactions, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "show user purchases",
                    "show user purchases",
                    "Display user purchases for a period.",
                    HandleShowPurchases, false, true);
            }
        }

        #endregion

        #region Service Members

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public SimpleCurrencyConfig GetConfig()
        {
            object remoteValue = DoRemoteByURL("CurrencyServerURI");
            if (remoteValue != null || m_doRemoteOnly)
                return (SimpleCurrencyConfig) remoteValue;

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
            List<string> query = m_gd.Query(new string[] {"*"}, _REALM, new QueryFilter()
                                                                            {
                                                                                andFilters = where
                                                                            }, null, null, null);

            UserCurrency currency;

            if (query.Count == 0)
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
            List<string> queryResults = m_gd.Query(new string[] {"*"}, _REALM, new QueryFilter()
                                                                                   {
                                                                                       andFilters = where
                                                                                   }, null, null, null);

            if (queryResults.Count == 0)
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
            if (transactions.Count == 0)
                return 0;
            else
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

            Dictionary<string, bool> sort = new Dictionary<string, bool>(2);
            sort["ToName"] = true;
            sort["FromName"] = true;

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
            if (purchases.Count == 0)
                return 0;
            else
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


            Dictionary<string, bool> sort = new Dictionary<string, bool>(2);
            sort["PrincipalID"] = true;
            sort["Created"] = true;

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
            if (m_syncMessagePoster == null)
            {
                m_syncMessagePoster = m_registry.RequestModuleInterface<ISyncMessagePosterService>();
                m_userInfoService = m_registry.RequestModuleInterface<IAgentInfoService>();
            }
            if (m_syncMessagePoster != null)
            {
                UserInfo toUserInfo = m_userInfoService.GetUserInfo(toID.ToString());
                UserInfo fromUserInfo = fromID == UUID.Zero ? null : m_userInfoService.GetUserInfo(fromID.ToString());
                UserAccount toAccount = m_registry.RequestModuleInterface<IUserAccountService>()
                                                  .GetUserAccount(null, toID);
                UserAccount fromAccount = m_registry.RequestModuleInterface<IUserAccountService>()
                                                    .GetUserAccount(null, fromID);
                if (m_config.SaveTransactionLogs)
                    AddTransactionRecord((transactionID == UUID.Zero ? UUID.Random() : transactionID), 
                        description, toID, fromID, amount, type, (toCurrency == null ? 0 : toCurrency.Amount), 
                        (fromCurrency == null ? 0 : fromCurrency.Amount), (toAccount == null ? "System" : toAccount.Name), 
                        (fromAccount == null ? "System" : fromAccount.Name), toObjectName, fromObjectName, (fromUserInfo == null ? 
                        UUID.Zero : fromUserInfo.CurrentRegionID));

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

        private void SendUpdateMoneyBalanceToClient(UUID toID, UUID transactionID, string serverURI, uint balance, string message)
        {
            OSDMap map = new OSDMap();
            map["Method"] = "UpdateMoneyBalance";
            map["AgentID"] = toID;
            map["Amount"] = balance;
            map["Message"] = message;
            map["TransactionID"] = transactionID;
            m_syncMessagePoster.Post(serverURI, map);
        }

        // Method Added By Alicia Raven
        private void AddTransactionRecord(UUID TransID, string Description, UUID ToID, UUID FromID, uint Amount,
            TransactionType TransType, uint ToBalance, uint FromBalance, string ToName, string FromName, string toObjectName, string fromObjectName, UUID regionID)
        {
            if(Amount > m_config.MaxAmountBeforeLogging)
                m_gd.Insert(_REALMHISTORY, new object[] {
                    TransID,
                    (Description == null ? "" : Description),
                    FromID.ToString(),
                    FromName,
                    ToID.ToString(),
                    ToName,
                    Amount,
                    (int)TransType,
                    Util.UnixTimeSinceEpoch(),
                    ToBalance,
                    FromBalance,
                    toObjectName == null ? "" : toObjectName, fromObjectName == null ? "" : fromObjectName, regionID 
                });
        }

        #endregion

        #region Helper Methods

        private void UserCurrencyUpdate(UserCurrency agent, bool full)
        {
            if (full)
                m_gd.Update(_REALM,
                            new Dictionary<string, object>
                                {
                                    {"LandInUse", agent.LandInUse},
                                    {"Tier", agent.Tier},
                                    {"IsGroup", agent.IsGroup},
                                    {"Amount", agent.Amount},
                                    {"StipendsBalance", agent.StipendsBalance}
                                }, null,
                            new QueryFilter()
                                {
                                    andFilters =
                                        new Dictionary<string, object>
                                            {
                                                {"PrincipalID", agent.PrincipalID}
                                            }
                                }
                            , null, null);
            else
                m_gd.Update(_REALM,
                            new Dictionary<string, object>
                                {
                                    {"LandInUse", agent.LandInUse},
                                    {"Tier", agent.Tier},
                                    {"IsGroup", agent.IsGroup}
                                }, null,
                            new QueryFilter()
                                {
                                    andFilters =
                                        new Dictionary<string, object>
                                            {
                                                {"PrincipalID", agent.PrincipalID}
                                            }
                                }
                            , null, null);
        }

        private void UserCurrencyCreate(UUID agentId)
        {
			// Check if this agent has a user account, if not assume its a bot and exit
			UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(new List<UUID> { UUID.Zero }, agentId);
            if (account != null)
            {
                m_gd.Insert(_REALM, new object[] {agentId.ToString(), 0, 0, 0, 0, 0});
            }
        }

        private void GroupCurrencyCreate(UUID groupID)
        {
            m_gd.Insert(_REALM, new object[] {groupID.ToString(), 0, 0, 0, 1, 0});
        }

        private DateTime StartTransactionPeriod( int period, string periodType)
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

        private static List<AgentTransfer> ParseTransferQuery(List<string> query)
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


        private static List<AgentPurchase> ParsePurchaseQuery(List<string> query)
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
            

        public string TransactionTypeInfo(TransactionType transType)
        {
            switch (transType)
            {
            // One-Time Charges
            case TransactionType.GroupCreate:       return "Group creation fee";
            case TransactionType.GroupJoin:         return "Group joining fee";
            case TransactionType.UploadCharge:      return "Upload charge";
            case TransactionType.LandAuction:       return "Land auction fee";
            case TransactionType.ClassifiedCharge:  return "Classified advert fee";
                // Recurrent Charges
            case TransactionType.ParcelDirFee:      return "Parcel directory fee";
            case TransactionType.ClassifiedRenew:   return "Classified renewal";
            case TransactionType.ScheduledFee:      return "Scheduled fee";
                // Inventory Transactions
            case TransactionType.GiveInventory:     return "Give inventory";
                // Transfers Between Users
            case TransactionType.ObjectSale:        return "Object sale";
            case TransactionType.Gift:              return "Gift";
            case TransactionType.LandSale:          return "Land sale";
            case TransactionType.ReferBonus:        return "Refer bonus";
            case TransactionType.InvntorySale:      return "Inventory sale";
            case TransactionType.RefundPurchase:    return "Purchase refund";
            case TransactionType.LandPassSale:      return "Land parcel sale";
            case TransactionType.DwellBonus:        return "Dwell bonus";
            case TransactionType.PayObject:         return "Pay object";
            case TransactionType.ObjectPays:        return "Object pays";
            case TransactionType.BuyMoney:          return "Money purchase";
            case TransactionType.MoveMoney:         return "Move money";
                // Group Transactions
            case TransactionType.GroupLiability:    return "Group liability";
            case TransactionType.GroupDividend:     return "Group dividend";
                // Event Transactions
            case TransactionType.EventFee:          return "Event fee";
            case TransactionType.EventPrize:        return "Event prize";
                // Stipend Credits
            case TransactionType.StipendPayment:    return "Stipend payment";

            default:                                return "System Generated";
            }
        }

        #endregion

        #region Console Methods

        public void AddMoney(IScene scene, string[] cmd)
        {
            string name = MainConsole.Instance.Prompt("User Name: ");
            uint amount = 0;
            while (!uint.TryParse(MainConsole.Instance.Prompt("Amount: ", "0"), out amount))
                MainConsole.Instance.Info("Bad input, must be a number > 0");

            UserAccount account =
                m_registry.RequestModuleInterface<IUserAccountService>()
                          .GetUserAccount(new List<UUID> {UUID.Zero}, name);
            if (account == null)
            {
                MainConsole.Instance.Info("No account found");
                return;
            }
            var currency = GetUserCurrency(account.PrincipalID);
            m_gd.Update(_REALM, new Dictionary<string, object>
                {
                    {
                        "Amount", currency.Amount + amount
                    }
                }, null, new QueryFilter()
                                         {
                                             andFilters =
                                                 new Dictionary<string, object> {{"PrincipalID", account.PrincipalID}}
                                         }, null, null);
            MainConsole.Instance.Info(account.Name + " now has $" + (currency.Amount + amount));

            if (m_syncMessagePoster == null)
            {
                m_syncMessagePoster = m_registry.RequestModuleInterface<ISyncMessagePosterService>();
                m_userInfoService = m_registry.RequestModuleInterface<IAgentInfoService>();
            }
            if (m_syncMessagePoster != null)
            {
                UserInfo toUserInfo = m_userInfoService.GetUserInfo(account.PrincipalID.ToString());
                if (toUserInfo != null && toUserInfo.IsOnline)
                    SendUpdateMoneyBalanceToClient(account.PrincipalID, UUID.Zero, toUserInfo.CurrentRegionURI, (currency.Amount + amount), "");
            }

            // log the transfer
            UserCurrencyTransfer(account.PrincipalID, UUID.Zero, amount, "Money transfer", TransactionType.SystemGenerated, UUID.Zero);

        }

        public void SetMoney(IScene scene, string[] cmd)
        {
            string name = MainConsole.Instance.Prompt("User Name: ");
            uint amount = 0;
            while (!uint.TryParse(MainConsole.Instance.Prompt("Set User's Money Amount: ", "0"), out amount))
                MainConsole.Instance.Info("Bad input, must be a number > 0");

            UserAccount account =
                m_registry.RequestModuleInterface<IUserAccountService>()
                          .GetUserAccount(new List<UUID> {UUID.Zero}, name);
            if (account == null)
            {
                MainConsole.Instance.Info("No account found");
                return;
            }
            m_gd.Update(_REALM,
                        new Dictionary<string, object>
                            {
                                {
                                    "Amount", amount
                                }
                            }, null, new QueryFilter()
                                         {
                                             andFilters =
                                                 new Dictionary<string, object> {{"PrincipalID", account.PrincipalID}}
                                         }, null, null);
            MainConsole.Instance.Info(account.Name + " now has $" + amount);

            if (m_syncMessagePoster == null)
            {
                m_syncMessagePoster = m_registry.RequestModuleInterface<ISyncMessagePosterService>();
                m_userInfoService = m_registry.RequestModuleInterface<IAgentInfoService>();
            }
            if (m_syncMessagePoster != null)
            {
                UserInfo toUserInfo = m_userInfoService.GetUserInfo(account.PrincipalID.ToString());
                if (toUserInfo != null && toUserInfo.IsOnline)
                    SendUpdateMoneyBalanceToClient(account.PrincipalID, UUID.Zero, toUserInfo.CurrentRegionURI, amount, "");
            }

            // log the transfer
            UserCurrencyTransfer(account.PrincipalID, UUID.Zero, amount, "Set user money", TransactionType.SystemGenerated, UUID.Zero);

        }

        public void GetMoney(IScene scene, string[] cmd)
        {
            string name = MainConsole.Instance.Prompt("User Name: ");
            UserAccount account =
                m_registry.RequestModuleInterface<IUserAccountService>()
                          .GetUserAccount(new List<UUID> {UUID.Zero}, name);
            if (account == null)
            {
                MainConsole.Instance.Info("No account found");
                return;
            }
            var currency = GetUserCurrency(account.PrincipalID);
            if (currency == null)
            {
                MainConsole.Instance.Info("No currency account found");
                return;
            }
            MainConsole.Instance.Info(account.Name + " has $" + currency.Amount);
        }

 
        public void HandleShowTransactions(IScene scene, string [] cmd)
        {
                string name = MainConsole.Instance.Prompt("User Name: ");
                UserAccount account =
                    m_registry.RequestModuleInterface<IUserAccountService>()
                        .GetUserAccount(new List<UUID> {UUID.Zero}, name);
                if (account == null)
                {
                    MainConsole.Instance.Info("No account found");
                    return;
                }

                int period;
                while (!int.TryParse(MainConsole.Instance.Prompt("Number of days to display: ", "7"), out period))
                    MainConsole.Instance.Info("Bad input, must be a number > 0");

                string transInfo;

                transInfo =  String.Format ("{0, -24}", "Date");
                transInfo += String.Format ("{0, -25}", "From");
                transInfo += String.Format ("{0, -30}", "Description");
                transInfo += String.Format ("{0, -20}", "Type");
                transInfo += String.Format ("{0, -12}", "Amount");
                transInfo += String.Format ("{0, -12}", "Balance");

                MainConsole.Instance.CleanInfo(transInfo);

                MainConsole.Instance.CleanInfo(
                    "-------------------------------------------------------------------------------------------------------------------------");

                List<AgentTransfer> transactions =  GetTransactionHistory(account.PrincipalID, period, "day");

                foreach (AgentTransfer transfer in transactions)
                {
                    transInfo =  String.Format ("{0, -24}", transfer.TransferDate.ToLocalTime());   
                    transInfo += String.Format ("{0, -25}", transfer.FromAgentName);   
                    transInfo += String.Format ("{0, -30}", transfer.Description);
                    transInfo += String.Format ("{0, -20}", TransactionTypeInfo(transfer.TransferType));
                    transInfo += String.Format ("{0, -12}", transfer.Amount);
                    transInfo += String.Format ("{0, -12}", transfer.ToBalance);

                    MainConsole.Instance.CleanInfo(transInfo);

                }

        }

        public void HandleShowPurchases(IScene scene, string [] cmd)
        {
            string name = MainConsole.Instance.Prompt ("User Name: ");
            UserAccount account =
                m_registry.RequestModuleInterface<IUserAccountService> ()
                    .GetUserAccount (new List<UUID> { UUID.Zero }, name);
            if (account == null)
            {
                MainConsole.Instance.Info ("No account found");
                return;
            }

            int period;
            while (!int.TryParse (MainConsole.Instance.Prompt ("Number of days to display: ", "7"), out period))
                MainConsole.Instance.Info ("Bad input, must be a number > 0");
             string transInfo;

            transInfo = String.Format ("{0, -24}", "Date");
            transInfo += String.Format ("{0, -30}", "Description");
            transInfo += String.Format ("{0, -20}", "InWorld Amount");
            transInfo += String.Format ("{0, -12}", "Cost");

            MainConsole.Instance.CleanInfo (transInfo);

            MainConsole.Instance.CleanInfo (
                "--------------------------------------------------------------------------------------------");

            List<AgentPurchase> purchases = GetPurchaseHistory (account.PrincipalID, period, "day");

            foreach (AgentPurchase purchase in purchases)
            {
                transInfo = String.Format ("{0, -24}", purchase.PurchaseDate.ToLocalTime());   
                transInfo += String.Format ("{0, -30}", "Purchase");
                transInfo += String.Format ("{0, -20}", InWorldCurrency + purchase.Amount);
                transInfo += String.Format ("{0, -12}", RealCurrency + ((float) purchase.RealAmount/100).ToString("0.00"));

                MainConsole.Instance.CleanInfo (transInfo);

            }
        }
        #endregion
    }
}