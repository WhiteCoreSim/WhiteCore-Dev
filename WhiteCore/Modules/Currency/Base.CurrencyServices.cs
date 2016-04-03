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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Currency
{
    public class BaseCurrencyServiceModule : IMoneyModule, IService
    {
        #region Declares

        BaseCurrencyConfig Config {
            get { return m_connector.GetConfig (); }
        }

        List<IScene> m_scenes = new List<IScene>();
        BaseCurrencyConnector m_connector;
        IRegistryCore m_registry;
        IAgentInfoService m_userInfoService;
        IUserAccountService m_userAccountService;

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            if (config.Configs["Currency"] == null ||
                config.Configs["Currency"].GetString("Module", "") != "BaseCurrency")
                return;

            m_registry = registry;
            m_connector = Framework.Utilities.DataManager.RequestPlugin<IBaseCurrencyConnector>() as BaseCurrencyConnector;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            if (m_registry == null)
                return;
            ISyncMessageRecievedService syncRecievedService =
                registry.RequestModuleInterface<ISyncMessageRecievedService>();
            if (syncRecievedService != null)
                syncRecievedService.OnMessageReceived += syncRecievedService_OnMessageReceived;
        }

        public void FinishedStartup()
        {
            if (m_registry == null)
                return;

            m_registry.RegisterModuleInterface<IMoneyModule>(this);

            ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
            if (manager != null) {
                manager.OnAddedScene += (scene) => {
                    m_scenes.Add (scene);
                    scene.EventManager.OnNewClient += OnNewClient;
                    scene.EventManager.OnClosingClient += OnClosingClient;
                    scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
                    scene.EventManager.OnValidateBuyLand += EventManager_OnValidateBuyLand;
                    scene.RegisterModuleInterface<IMoneyModule> (this);
                };
                manager.OnCloseScene += (scene) => {
                    scene.EventManager.OnNewClient -= OnNewClient;
                    scene.EventManager.OnClosingClient -= OnClosingClient;
                    scene.EventManager.OnMakeRootAgent -= OnMakeRootAgent;
                    scene.EventManager.OnValidateBuyLand -= EventManager_OnValidateBuyLand;
                    scene.RegisterModuleInterface<IMoneyModule> (this);
                    m_scenes.Remove (scene);
                };
            }


            // these are only valid if we are local
            if (m_connector.IsLocalConnector)
            {
                m_userInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
                m_userAccountService = m_registry.RequestModuleInterface<IUserAccountService> ();
                    
                AddCommands ();
                
            }
        }

        bool EventManager_OnValidateBuyLand(EventManager.LandBuyArgs e)
        {
            IParcelManagementModule parcelManagement = GetSceneFor(e.agentId).RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement == null)
                return false;
            ILandObject lob = parcelManagement.GetLandObject(e.parcelLocalID);

            if (lob != null) {
                UUID AuthorizedID = lob.LandData.AuthBuyerID;
                int saleprice = lob.LandData.SalePrice;
                UUID pOwnerID = lob.LandData.OwnerID;

                bool landforsale = ((lob.LandData.Flags & (uint) (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects | ParcelFlags.SellParcelObjects)) != 0);
                if ((AuthorizedID == UUID.Zero || AuthorizedID == e.agentId) && e.parcelPrice >= saleprice &&
                    landforsale) {
                    if (m_connector.UserCurrencyTransfer (lob.LandData.OwnerID, e.agentId, (uint)saleprice, "Land Buy", TransactionType.LandSale, UUID.Zero))
                    {
                        e.parcelOwnerID = pOwnerID;
                        e.landValidated = true;
                        return true;
                    }

                    // not validated
                    e.landValidated = false;

                }
            }
            return false;
        }

        #endregion

        void AddCommands()
        {
            if (MainConsole.Instance != null) {
                MainConsole.Instance.Commands.AddCommand (
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
                    "show user transactions",
                    "Display user transactions for a period.",
                    HandleShowTransactions, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "show user purchases",
                    "show user purchases",
                    "Display user purchases for a period.",
                    HandleShowPurchases, false, true);

            }
        }

        #region IMoneyModule Members

        public string InWorldCurrencySymbol {
            get { return m_connector.InWorldCurrency; }
        }

        public bool IsLocal {
            get { return m_connector.IsLocalConnector; }
        }

        public int UploadCharge {
            get { return Config.PriceUpload; }
        }

        public int GroupCreationCharge {
            get { return Config.PriceGroupCreate; }
        }

        public int DirectoryFeeCharge {
            get { return Config.PriceDirectoryFee; }
        }

        public int ClientPort {
            get  { return Config.ClientPort; }
        }

        public bool ObjectGiveMoney(UUID objectID, string objectName, UUID fromID, UUID toID, int amount)
        {
            return m_connector.UserCurrencyTransfer(toID, fromID, UUID.Zero, "", objectID, objectName, (uint) amount, "Object payment",
                                                    TransactionType.ObjectPays, UUID.Zero);
        }

        public int Balance(UUID agentID)
        {
            return (int) m_connector.GetUserCurrency(agentID).Amount;
        }

        public bool Charge(UUID agentID, int amount, string text, TransactionType type)
        {
            return m_connector.UserCurrencyTransfer(UUID.Zero, agentID, (uint)amount, text,
                                                    type, UUID.Zero);
        }

        public event ObjectPaid OnObjectPaid;

        public void FireObjectPaid(UUID objectID, UUID agentID, int amount)
        {
            if (OnObjectPaid != null)
                OnObjectPaid(objectID, agentID, amount);
        }

        public bool Transfer(UUID toID, UUID fromID, int amount, string description, TransactionType type)
        {
            return m_connector.UserCurrencyTransfer(toID, fromID, (uint) amount, description, type,
                                                    UUID.Zero);
        }

        public bool Transfer(UUID toID, UUID fromID, UUID toObjectID, string toObjectName, UUID fromObjectID, 
            string fromObjectName, int amount, string description, TransactionType type)
        {
            bool result = m_connector.UserCurrencyTransfer (toID, fromID, toObjectID, toObjectName, 
                              fromObjectID, fromObjectName, (uint)amount, description, type, UUID.Zero);
            if (toObjectID != UUID.Zero) {
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                if (manager != null) {
                    foreach (IScene scene in manager.Scenes) {
                        ISceneChildEntity ent = scene.GetSceneObjectPart (toObjectID);
                        if (ent != null)
                            FireObjectPaid(toObjectID, fromID, amount);
                    }
                }
            }
            return result;
        }

        public uint NumberOfTransactions(UUID toAgent, UUID fromAgent)
        {
            return m_connector.NumberOfTransactions(toAgent, fromAgent);
        }

        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetTransactionHistory(toAgentID, fromAgentID, dateStart, dateEnd, start, count);
        }

        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, int period, string periodType)
        {
            return m_connector.GetTransactionHistory (toAgentID, fromAgentID, period, periodType);
        }
            
        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, int period, string periodType)
        {
            return m_connector.GetTransactionHistory(toAgentID, period, periodType);
        }

        public List<AgentTransfer> GetTransactionHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetTransactionHistory(dateStart, dateEnd, start, count);
        }

        public List<AgentTransfer> GetTransactionHistory(int period, string periodType, uint? start, uint? count)
        {
            return m_connector.GetTransactionHistory(period, periodType, start, count);
        }
 

        public uint NumberOfPurchases(UUID UserID)
        {
            return m_connector.NumberOfPurchases(UserID);
        }

        public List<AgentPurchase> GetPurchaseHistory(UUID userID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetPurchaseHistory(userID, dateStart, dateEnd, start, count);
        }

        public List<AgentPurchase> GetPurchaseHistory(UUID toAgentID, int period, string periodType)
        {
            return m_connector.GetPurchaseHistory(toAgentID, period, periodType);
        }

        public List<AgentPurchase> GetPurchaseHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetPurchaseHistory(dateStart, dateEnd, start, count);
        }

        public List<AgentPurchase> GetPurchaseHistory (int period, string periodType, uint? start, uint? count)
        {
            return m_connector.GetPurchaseHistory(period, periodType, start, count);
        }

        public List<GroupAccountHistory> GetGroupTransactions(UUID groupID, UUID agentID, int currentInterval,
            int intervalDays)
        {
            return m_connector.GetGroupTransactions (groupID, agentID, currentInterval, intervalDays);
        }

        public GroupBalance GetGroupBalance(UUID groupID)
        {
            return m_connector.GetGroupBalance(groupID);
        }

        public bool GroupCurrencyTransfer(UUID groupID, UUID userID, bool payUser, string toObjectName, UUID fromObjectID,
            string fromObjectName, int amount, string description, TransactionType type, UUID transactionID)
        {
            return m_connector.GroupCurrencyTransfer(groupID, userID, payUser, toObjectName, fromObjectID,
                fromObjectName, amount, description, type, transactionID);
        }


        #endregion

        #region Client Members

        void OnNewClient(IClientAPI client)
        {
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
        }

        void OnMakeRootAgent(IScenePresence presence)
        {
            presence.ControllingClient.SendMoneyBalance(UUID.Zero, true, new byte[0],
                                                        (int) m_connector.GetUserCurrency(presence.UUID).Amount);
        }

        protected void OnClosingClient(IClientAPI client)
        {
            client.OnEconomyDataRequest -= EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest -= SendMoneyBalance;
            client.OnMoneyTransferRequest -= ProcessMoneyTransferRequest;
        }

        void ProcessMoneyTransferRequest(UUID fromID, UUID toID, int amount, int type, string description)
        {
            if (toID != UUID.Zero) {
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                if (manager != null) {
                    bool paid = false;
                    foreach (IScene scene in manager.Scenes) {
                        ISceneChildEntity ent = scene.GetSceneObjectPart (toID);
                        if (ent != null) {
                            bool success = m_connector.UserCurrencyTransfer (ent.OwnerID, fromID, ent.UUID, ent.Name, UUID.Zero, "",
                                               (uint)amount, description, (TransactionType)type, UUID.Random ());
                            if (success)
                                FireObjectPaid(toID, fromID, amount);
                            paid = true;
                            break;
                        }
                    }
                    if (!paid) {
                        m_connector.UserCurrencyTransfer (toID, fromID, (uint)amount, description,
                            (TransactionType)type, UUID.Random ());
                    }
                }
            }
        }

        bool ValidateLandBuy(EventManager.LandBuyArgs e)
        {
            return m_connector.UserCurrencyTransfer(e.parcelOwnerID, e.agentId,
                                                    (uint) e.parcelPrice, "Land Purchase", TransactionType.LandSale,
                                                    UUID.Random());
        }

        void EconomyDataRequestHandler(IClientAPI remoteClient)
        {
            if (Config == null) {
                remoteClient.SendEconomyData (0, remoteClient.Scene.RegionInfo.ObjectCapacity,
                    remoteClient.Scene.RegionInfo.ObjectCapacity,
                    0, 0,
                    0, 0,
                    0, 0,
                    0,
                    0, 0,
                    0, 0,
                    0,
                    0, 0);
            } else
                remoteClient.SendEconomyData (0, remoteClient.Scene.RegionInfo.ObjectCapacity,
                    remoteClient.Scene.RegionInfo.ObjectCapacity,
                    0, Config.PriceGroupCreate,
                    0, 0,
                    0, 0,
                    0,
                    0, 0,
                    0, 0,
                    Config.PriceUpload,
                    0, 0);
        }

        void SendMoneyBalance(IClientAPI client, UUID agentId, UUID sessionId, UUID transactionId)
        {
            if (client.AgentId == agentId && client.SessionId == sessionId) {
                var cliBal = (int)m_connector.GetUserCurrency (client.AgentId).Amount;   
                client.SendMoneyBalance (transactionId, true, new byte[0], cliBal);
            } else
                client.SendAlertMessage ("Unable to send your money balance to you!");
        }

        #endregion

        #region Service Members

        OSDMap syncRecievedService_OnMessageReceived(OSDMap message)
        {
            string method = message ["Method"];
            if (method == "UpdateMoneyBalance") {
                UUID agentID = message ["AgentID"];
                int Amount = message ["Amount"];
                string Message = message ["Message"];
                UUID TransactionID = message ["TransactionID"];
                IDialogModule dialogModule = GetSceneFor (agentID).RequestModuleInterface<IDialogModule> ();
                IScenePresence sp = GetSceneFor (agentID).GetScenePresence (agentID);
                if (sp != null) {
                    if (dialogModule != null && !string.IsNullOrEmpty (Message))
                        dialogModule.SendAlertToUser (agentID, Message);

                    sp.ControllingClient.SendMoneyBalance(TransactionID, true, Utils.StringToBytes(Message), Amount);
                }
            } else if (method == "GetLandData") {
                MainConsole.Instance.Info (message);

                UUID agentID = message["AgentID"];
                IScene region = GetSceneFor (agentID);
                MainConsole.Instance.Info ("Region: " + region.RegionInfo.RegionName);

                IParcelManagementModule parcelManagement = region.RequestModuleInterface<IParcelManagementModule> ();
                if (parcelManagement != null) {
                    IScenePresence sp = region.GetScenePresence (agentID);
                    if (sp != null) {
                        MainConsole.Instance.DebugFormat ("sp parcel UUID: {0} Pos: {1}, {2}",
                            sp.CurrentParcelUUID, sp.AbsolutePosition.X, sp.AbsolutePosition.Y);
                        
                        ILandObject lo = sp.CurrentParcel;
                        if (lo == null) {
                            // try for a position fix
                            lo = parcelManagement.GetLandObject ((int)sp.AbsolutePosition.X, (int)sp.AbsolutePosition.Y);
                        }

                        if (lo != null) {   
                            if ((lo.LandData.Flags & (uint)ParcelFlags.ForSale) == (uint)ParcelFlags.ForSale) {
                                if (lo.LandData.AuthBuyerID != UUID.Zero && lo.LandData.AuthBuyerID != agentID)
                                    return new OSDMap () { new KeyValuePair<string, OSD> ("Success", false) };
                                OSDMap map = lo.LandData.ToOSD ();
                                map ["Success"] = true;
                                return map;
                            }
                        }
                    }
                }
                return new OSDMap() {new KeyValuePair<string, OSD>("Success", false)};
            }
            return null;
        }

        IScene GetSceneFor(UUID userID)
        {
            foreach (IScene scene in m_scenes) {
                var sp = scene.GetScenePresence (userID);
                if ( sp != null && !sp.IsChildAgent)
                    return scene;
            }
            if (m_scenes.Count == 0) {
                MainConsole.Instance.Debug ("User not present in any regions??");
                return null;
            }

            MainConsole.Instance.Debug ("Returning scene[0]: " + m_scenes [0].RegionInfo.RegionName);
            return m_scenes[0];
        }

        /// <summary>
        ///     All message for money actually go through this function. Which also update the balance
        /// </summary>
        /// <param name="toId"></param>
        /// <param name="message"></param>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        public bool SendGridMessage(UUID toId, string message, UUID transactionId)
        {
            IDialogModule dialogModule = GetSceneFor (toId).RequestModuleInterface<IDialogModule> ();
            if (dialogModule != null) {
                IScenePresence icapiTo = GetSceneFor (toId).GetScenePresence (toId);
                if (icapiTo != null) {
                    icapiTo.ControllingClient.SendMoneyBalance (transactionId, true, Utils.StringToBytes (message),
                        (int)m_connector.GetUserCurrency (icapiTo.UUID).Amount);
                    dialogModule.SendAlertToUser (toId, message);
                }

                return true;
            }
            return false;
        }

        #endregion

        #region Console Methods

        UserAccount GetUserAccount()
        {
            string name = MainConsole.Instance.Prompt("User Name (First Last) ");

            UserAccount account = m_userAccountService.GetUserAccount(new List<UUID> {UUID.Zero}, name);
            if (account == null)
                MainConsole.Instance.Info("Sorry, unable to locate account for " + name);

            return account;
        }

        uint GetAmount(string prompt)
        {
            uint amount = 0;
            string amnt = "";
            do {
                amnt = MainConsole.Instance.Prompt (prompt, "amnt");
                if (amnt == "")     // leave an 'out'
                    return 0;
                
                if (!uint.TryParse (amnt, out amount))
                    MainConsole.Instance.Error ("Bad input, must be a number!");
            } while (amount == 0);
             
            return amount;
        }

        string StrUserBalance(int amount)
        {
            return m_connector.InWorldCurrency + amount;
        }

        protected void AddMoney(IScene scene, string[] cmd)
        {
            UserAccount account = GetUserAccount ();
            if (account == null)
                return;

            uint amount = GetAmount("Amount of " + m_connector.InWorldCurrency + " to add?");
            if (amount == 0)
                return;

            // log the transfer
            m_connector.UserCurrencyTransfer(account.PrincipalID, UUID.Zero, amount, "Money transfer", TransactionType.SystemGenerated, UUID.Zero);

            var currency = m_connector.GetUserCurrency(account.PrincipalID);
            MainConsole.Instance.Info(account.Name + " now has " + StrUserBalance((int)currency.Amount));

            if (m_userInfoService != null) {
                UserInfo toUserInfo = m_userInfoService.GetUserInfo (account.PrincipalID.ToString ());
                if (toUserInfo != null && toUserInfo.IsOnline)
                    m_connector.SendUpdateMoneyBalanceToClient(account.PrincipalID, UUID.Zero, toUserInfo.CurrentRegionURI, (currency.Amount), "");
            }

        }

        protected void SetMoney(IScene scene, string[] cmd)
        {
            UserAccount account = GetUserAccount ();
            if (account == null)
                return;

            uint amount = GetAmount("Set user's balance to " + m_connector.InWorldCurrency + " ?");

            if (amount == 0) {
                string response = MainConsole.Instance.Prompt ("Clear user's balance? (yes, no)", "no").ToLower ();
                if (!response.StartsWith ("y")) {
                    MainConsole.Instance.Info ("[Currency]: User balance not cleared.");
                    return;
                }
            }

            var currency = m_connector.GetUserCurrency(account.PrincipalID);
            var balAdjust = amount - currency.Amount;

            // log the transfer
            m_connector.UserCurrencyTransfer(account.PrincipalID, UUID.Zero, balAdjust, "Set user money", TransactionType.SystemGenerated, UUID.Zero);

            currency = m_connector.GetUserCurrency(account.PrincipalID);
            MainConsole.Instance.Info(account.Name + " now has " + StrUserBalance((int)currency.Amount));

            if (m_userInfoService != null) {
                UserInfo toUserInfo = m_userInfoService.GetUserInfo (account.PrincipalID.ToString ());
                if (toUserInfo != null && toUserInfo.IsOnline)
                    m_connector.SendUpdateMoneyBalanceToClient(account.PrincipalID, UUID.Zero, toUserInfo.CurrentRegionURI, currency.Amount, "");
            }
        }


        protected void GetMoney(IScene scene, string[] cmd)
        {
            UserAccount account = GetUserAccount ();
            if (account == null)
                return;

            var currency = m_connector.GetUserCurrency(account.PrincipalID);
            MainConsole.Instance.Info(account.Name + " has " + StrUserBalance((int)currency.Amount));
        }

/*
        protected void HandleStipendSet(IScene scene, string[] cmd)
        {
            string rawDate = MainConsole.Instance.Prompt("Date to pay next Stipend? (MM/dd/yyyy)");
            if (rawDate == "")
                return;
            
            // Make a new DateTime from rawDate
            DateTime newDate = DateTime.ParseExact(rawDate, "MM/dd/yyyy", CultureInfo.InvariantCulture);
//            GiveStipends.StipendDate = newDate;

            // Code needs to be added to run through the scheduler and change the 
            // RunsNext to the date that the user wants the scheduler to be
            // Fly-Man- 2-5-2015
            MainConsole.Instance.Info("Stipend Date has been set to" + newDate);
        }
*/
        protected void HandleShowTransactions(IScene scene, string [] cmd)
        {
            UserAccount account = GetUserAccount ();
            if (account == null)
                return;

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

            foreach (AgentTransfer transfer in transactions) {
                transInfo = String.Format ("{0, -24}", transfer.TransferDate.ToLocalTime ());   
                transInfo += String.Format ("{0, -25}", transfer.FromAgentName);   
                transInfo += String.Format ("{0, -30}", transfer.Description);
                transInfo += String.Format ("{0, -20}", Utilities.TransactionTypeInfo(transfer.TransferType));
                transInfo += String.Format ("{0, -12}", transfer.Amount);
                transInfo += String.Format ("{0, -12}", transfer.ToBalance);

                MainConsole.Instance.CleanInfo(transInfo);

            }

        }

        protected void HandleShowPurchases(IScene scene, string [] cmd)
        {
            UserAccount account = GetUserAccount ();
            if (account == null)
                return;

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

            foreach (AgentPurchase purchase in purchases) {
                transInfo = String.Format ("{0, -24}", purchase.PurchaseDate.ToLocalTime ());   
                transInfo += String.Format ("{0, -30}", "Purchase");
                transInfo += String.Format ("{0, -20}", m_connector.InWorldCurrency + purchase.Amount);
                transInfo += String.Format ("{0, -12}", m_connector.RealCurrency + ((float)purchase.RealAmount / 100).ToString ("0.00"));

                MainConsole.Instance.CleanInfo (transInfo);

            }
        }
        #endregion
    }
}
