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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;

namespace WhiteCore.Modules.Currency
{
    /* This module provides the necessary economy functionality for the viewer
       but with all values being $0
    */
    public class CurrencyModule : IMoneyModule, INonSharedRegionModule
    {
        bool m_enabled;
        IConfigSource m_config;

        #region IMoneyModule Members

		public string InWorldCurrencySymbol
        {
            get { return "$"; }
        }

        public bool IsLocal
        {
            get { return !m_config.Configs ["WhiteCoreConnectors"].GetBoolean("DoRemoteCalls", false); }
        }

        public int UploadCharge
        {
            get { return 0; }
        }

        public int GroupCreationCharge
        {
            get { return 0; }
        }

        public int DirectoryFeeCharge
        {
            get { return 0; }
        }

        public int ClientPort
        {
            get { return m_config.Configs["Handlers"].GetInt("LLLoginHandlerPort", (int) MainServer.Instance.Port); }
        }

#pragma warning disable 67

        public event ObjectPaid OnObjectPaid;

        public bool Transfer(UUID toID, UUID fromID, int amount, string description, TransactionType type)
        {
            return true;
        }

        public bool Transfer(UUID toID, UUID fromID, UUID toObjectID, string toObjectName, UUID fromObjectID,
                             string fromObjectName, int amount, string description,
                             TransactionType type)
        {
            if ((type == TransactionType.PayObject) && (OnObjectPaid != null))
                OnObjectPaid((fromObjectID == UUID.Zero) ? toObjectID : fromObjectID, fromID, amount);
            return true;
        }

        public void Transfer(UUID objectID, UUID agentID, int amount)
        {
        }

#pragma warning restore 67

        #region INonSharedRegionModule Members

        /// <summary>
        ///     Startup
        /// </summary>
        /// <param name="config"></param>
        public void Initialise(IConfigSource config)
        {
            m_config = config;
            IConfig currencyConfig = config.Configs["Currency"];
            if (currencyConfig != null)
            {
                m_enabled = currencyConfig.GetString("Module", "") == Name;
            }
        }

        public void AddRegion(IScene scene)
        {
            if (!m_enabled)
                return;
            // Send ObjectCapacity to Scene..  Which sends it to the SimStatsReporter.
            scene.RegisterModuleInterface<IMoneyModule>(this);

            // XMLRPCHandler = scene;
            // To use the following you need to add:
            // -helperuri <ADDRESS TO HERE OR grid MONEY SERVER>
            // to the command line parameters you use to start up your client
            // This commonly looks like -helperuri http://127.0.0.1:9000/
            MainServer.Instance.AddXmlRPCHandler("getCurrencyQuote", quote_func);
            MainServer.Instance.AddXmlRPCHandler("buyCurrency", buy_func);
            MainServer.Instance.AddXmlRPCHandler("preflightBuyLandPrep", preflightBuyLandPrep_func);
            MainServer.Instance.AddXmlRPCHandler("buyLandPrep", landBuy_func);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void RegionLoaded(IScene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "ZeroCurrency"; }
        }

        #endregion

        public int Balance(UUID agentID)
        {
            return 0;
        }

        public bool Charge(UUID agentID, int amount, string description, TransactionType type)
        {
            return true;
        }

        public bool ObjectGiveMoney(UUID objectID, string objectName, UUID fromID, UUID toID, int amount)
        {
            return true;
        }

        public void ProcessMoneyTransferRequest(UUID source, UUID destination, int amount,
                                                int transactiontype, string description)
        {
        }

        #endregion

        /// <summary>
        ///     New Client Event Handler
        /// </summary>
        /// <param name="client"></param>
        protected void OnNewClient(IClientAPI client)
        {
            // Subscribe to Money messages
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
        }

        protected void OnClosingClient(IClientAPI client)
        {
            // Subscribe to Money messages
            client.OnEconomyDataRequest -= EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest -= SendMoneyBalance;
            client.OnMoneyTransferRequest -= ProcessMoneyTransferRequest;
        }

        /// <summary>
        ///     Sends the stored money balance to the client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="agentID"></param>
        /// <param name="SessionID"></param>
        /// <param name="transactionID"></param>
        protected void SendMoneyBalance(IClientAPI client, UUID agentID, UUID SessionID, UUID transactionID)
        {
            client.SendMoneyBalance(transactionID, true, new byte[0], 0);
        }

        #region Buy Currency and Land

        protected XmlRpcResponse quote_func(XmlRpcRequest request, IPEndPoint ep)
        {
            Hashtable requestData = (Hashtable) request.Params[0];
            UUID agentId = UUID.Zero;
            int amount = 0;
            Hashtable quoteResponse = new Hashtable();
            XmlRpcResponse returnval = new XmlRpcResponse();

            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID.TryParse((string) requestData["agentId"], out agentId);
                try
                {
                    amount = (int) requestData["currencyBuy"];
                }
                catch (InvalidCastException)
                {
                }
                Hashtable currencyResponse = new Hashtable {{"estimatedCost", 0}, {"currencyBuy", amount}};

                quoteResponse.Add("success", true);
                quoteResponse.Add("currency", currencyResponse);
                quoteResponse.Add("confirm", "asdfad9fj39ma9fj");

                returnval.Value = quoteResponse;
                return returnval;
            }

            quoteResponse.Add("success", false);
            quoteResponse.Add("errorMessage", "Invalid parameters passed to the quote box");
			quoteResponse.Add("errorURI", "http://whitecore-sim.org/wiki");
            returnval.Value = quoteResponse;
            return returnval;
        }

        protected XmlRpcResponse buy_func(XmlRpcRequest request, IPEndPoint ep)
        {
            /*Hashtable requestData = (Hashtable)request.Params[0];
            UUID agentId = UUID.Zero;
            int amount = 0;
            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID.TryParse((string)requestData["agentId"], out agentId);
                try
                {
                    amount = (Int32)requestData["currencyBuy"];
                }
                catch (InvalidCastException)
                {
                }
                if (agentId != UUID.Zero)
                {
                    uint buyer = CheckExistAndRefreshFunds(agentId);
                    buyer += (uint)amount;
                    UpdateBalance(agentId,buyer);
					
                    IClientAPI client = LocateClientObject(agentId);
                    if (client != null)
                    {
                        SendMoneyBalance(client, agentId, client.SessionId, UUID.Zero);
                    }
                }
            }*/
            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable returnresp = new Hashtable {{"success", true}};
            returnval.Value = returnresp;
            return returnval;
        }

        protected XmlRpcResponse preflightBuyLandPrep_func(XmlRpcRequest request, IPEndPoint ep)
        {
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();

            Hashtable membershiplevels = new Hashtable();
            membershiplevels.Add("levels", membershiplevels);

            Hashtable landuse = new Hashtable();

            Hashtable level = new Hashtable {{"id", "00000000-0000-0000-0000-000000000000"}, {"", "Premium Membership"}};

            Hashtable currencytable = new Hashtable {{"estimatedCost", 0}};

            retparam.Add("success", true);
            retparam.Add("currency", currencytable);
            retparam.Add("membership", level);
            retparam.Add("landuse", landuse);
            retparam.Add("confirm", "asdfajsdkfjasdkfjalsdfjasdf");
            ret.Value = retparam;
            return ret;
        }

        protected XmlRpcResponse landBuy_func(XmlRpcRequest request, IPEndPoint ep)
        {
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable {{"success", true}};
            ret.Value = retparam;
            return ret;
        }

        #endregion

        #region event Handlers

        /// <summary>
        ///     Event called Economy Data Request handler.
        /// </summary>
        /// <param name="remoteClient"></param>
        public void EconomyDataRequestHandler(IClientAPI remoteClient)
        {
            remoteClient.SendEconomyData(0, remoteClient.Scene.RegionInfo.ObjectCapacity, 0, 0, 0,
                                         0, 0, 0, 0, 0,
                                         0, 0, 0, 0, 0,
                                         0, 0);
        }

        #endregion

        public uint NumberOfTransactions(UUID toAgent, UUID fromAgent)
        {
            return 0;
        }

        public List<AgentTransfer> GetTransactionHistory(UUID UserID, UUID fromAgentID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return new List<AgentTransfer> ();
        }

        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, int period, string periodType)
        {
            return new List<AgentTransfer> ();
        }

        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, int period, string periodType)
        {
            return new List<AgentTransfer> ();
        }
            
        public List<AgentTransfer> GetTransactionHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return new List<AgentTransfer> ();
        }

        public List<AgentTransfer> GetTransactionHistory(int period, string periodType, uint? start, uint? count)
        {
            return new List<AgentTransfer> ();
        }

        public uint NumberOfPurchases(UUID UserID)
        {
            return 0;
        }

        public List<AgentPurchase> GetPurchaseHistory(UUID UserID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return new List<AgentPurchase> ();
        }

        public List<AgentPurchase> GetPurchaseHistory (UUID toAgentID, int period, string periodType)
        {
            return new List<AgentPurchase> ();
        }

        public List<AgentPurchase> GetPurchaseHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return new List<AgentPurchase> ();
        }

        public List<AgentPurchase> GetPurchaseHistory (int period, string periodType, uint? start, uint? count)
        {
            return new List<AgentPurchase> ();
        }

        public List<GroupAccountHistory> GetGroupTransactions(UUID groupID, UUID agentID, int currentInterval,
            int intervalDays)
        {
            return new List<GroupAccountHistory>();
        }

        public GroupBalance GetGroupBalance(UUID groupID)
        {
            return new GroupBalance {StartingDate = DateTime.Now.AddDays(-4)};
        }

        public bool GroupCurrencyTransfer(UUID groupID, UUID fromID, bool payUser, string toObjectName, UUID fromObjectID,
            string fromObjectName, int amount, string description, TransactionType type, UUID transactionID)
        {
            return true;
        }
    }
}
