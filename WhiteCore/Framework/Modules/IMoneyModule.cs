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
using WhiteCore.Framework.PresenceInfo;
using OpenMetaverse;
using WhiteCore.Framework.Services;

namespace WhiteCore.Framework.Modules
{
    public delegate bool ObjectPaid(UUID objectID, UUID agentID, int amount);

    public enum TransactionType
    {
        SystemGenerated = 0,
        // One-Time Charges
        GroupCreate    	= 1002,
        GroupJoin      	= 1004,
        UploadCharge   	= 1101,
        LandAuction    	= 1102,
        ClassifiedCharge= 1103,
        // Recurrent Charges
        ParcelDirFee  	= 2003,
        ClassifiedRenew = 2005,
        ScheduledFee    = 2900,
        // Inventory Transactions
        GiveInventory   = 3000,
        // Transfers Between Users
        ObjectSale     	= 5000,
        Gift           	= 5001,
        LandSale       	= 5002,
        ReferBonus     	= 5003,
        InvntorySale   	= 5004,
        RefundPurchase 	= 5005,
        LandPassSale   	= 5006,
        DwellBonus     	= 5007,
        PayObject      	= 5008,
        ObjectPays     	= 5009,
        BuyMoney       	= 5010,
        MoveMoney      	= 5011,
        // Group Transactions
        GroupLiability 	= 6003,
        GroupDividend  	= 6004,
        // Event Transactions
        EventFee        = 9003,
        EventPrize      = 9004,
        // Stipend Credits
        StipendPayment 	= 10000
    }
            
    public class GroupBalance : IDataTransferable
    {
        public int TotalTierDebit = 0;
        public int TotalTierCredits = 0;
        public int ParcelDirectoryFee = 0;
        public int LandFee = 0;
        public int ObjectFee = 0;
        public int GroupFee = 0;
        public DateTime StartingDate;

        public override void FromOSD(OpenMetaverse.StructuredData.OSDMap map)
        {
            TotalTierDebit = map["TotalTierDebit"];
            TotalTierCredits = map["TotalTierCredits"];
            ParcelDirectoryFee = map["ParcelDirectoryFee"];
            LandFee = map["LandFee"];
            ObjectFee = map["ObjectFee"];
            GroupFee = map["GroupFee"];
            StartingDate = map["StartingDate"];
        }

        public override OpenMetaverse.StructuredData.OSDMap ToOSD()
        {
            OpenMetaverse.StructuredData.OSDMap map = new OpenMetaverse.StructuredData.OSDMap();

            map["TotalTierDebit"] = TotalTierDebit;
            map["TotalTierCredits"] = TotalTierCredits;
            map["ParcelDirectoryFee"] = ParcelDirectoryFee;
            map["LandFee"] = LandFee;
            map["ObjectFee"] = ObjectFee;
            map["GroupFee"] = GroupFee;
            map["StartingDate"] = StartingDate;

            return map;
        }
    }

    public class AgentTransfer : IDataTransferable
    {
        public UUID ID;
        public string Description = "";
        public UUID FromAgent;
        public string FromAgentName = "";
        public UUID ToAgent;
        public string ToAgentName = "";
        public int Amount = 0;
        public TransactionType TransferType = 0;
        public DateTime TransferDate;
        public int ToBalance = 0;
        public int FromBalance = 0;
        public string FromObjectName = "";
        public string ToObjectName = "";
        public string RegionName = "";

        public override void FromOSD(OpenMetaverse.StructuredData.OSDMap map)
        {
            ID = map["ID"];
            Description = map["Description"];
            FromAgent = map["FromAgent"];
            FromAgentName = map["FromAgentName"];
            ToAgent = map["ToAgent"];
            ToAgentName = map["ToAgentName"];
            Amount = map["Amount"];
            TransferType = (TransactionType) Int32.Parse( map["TransferType"]);
            TransferDate = map["TransferDate"];
            ToBalance = map["ToBalance"];
            FromBalance = map["FromBalance"];
            FromObjectName = map["FromObjectName"];
            ToObjectName = map["ToObjectName"];
            RegionName = map["RegionName"];

        }

        public override OpenMetaverse.StructuredData.OSDMap ToOSD()
        {
            OpenMetaverse.StructuredData.OSDMap map = new OpenMetaverse.StructuredData.OSDMap();

            map["ID"] = ID;
            map["Description"] = Description;
            map["FromAgent"] = FromAgent;
            map["FromAgentName"] = FromAgentName;
            map["ToAgent"] = ToAgent;
            map["ToAgentName"] = ToAgentName;
            map["Amount"] = Amount;
            map["TransferType"] = TransferType.ToString();
            map["TransferDate"] = TransferDate;
            map["ToBalance"] = ToBalance;
            map["FromObjectName"] = FromObjectName;
            map["ToObjectName"] = ToObjectName;
            map["RegionName"] = RegionName;

            return map;
        }
    }

        public class AgentPurchase : IDataTransferable
    {
        public UUID ID;
        public UUID AgentID;
        public string IP = "";
        public int Amount = 0;
        public int RealAmount = 0;
        public DateTime PurchaseDate;
        public DateTime UpdateDate;

        public override void FromOSD(OpenMetaverse.StructuredData.OSDMap map)
        {
            ID = map["ID"];
            AgentID = map["AgentID"];
            IP = map["IP"];
            Amount = map["Amount"];
            RealAmount = map["RealAmount"];
            PurchaseDate = map["PurchaseDate"];
            UpdateDate = map["UpdateDate"];
        }

        public override OpenMetaverse.StructuredData.OSDMap ToOSD()
        {
            OpenMetaverse.StructuredData.OSDMap map = new OpenMetaverse.StructuredData.OSDMap();

            map["ID"] = ID;
            map["AgentID"] = AgentID;
            map["IP"] = IP;
            map["Amount"] = Amount;
            map["RealAmount"] = RealAmount;
            map["PurchaseDate"] = PurchaseDate;
            map["UpdateDate"] = UpdateDate;

            return map;
        }
    }

    public interface IMoneyModule
    {
        int UploadCharge { get; }
        int GroupCreationCharge { get; }
        int DirectoryFeeCharge { get; }
        int ClientPort { get; }

        bool ObjectGiveMoney(UUID objectID, string objectName, UUID fromID, UUID toID, int amount);

        int Balance(UUID agentID);
        bool Charge(UUID agentID, int amount, string text, TransactionType type);

        event ObjectPaid OnObjectPaid;

        bool Transfer(UUID toID, UUID fromID, int amount, string description, TransactionType type);

        bool Transfer(UUID toID, UUID fromID, UUID toObjectID, string toObjectName, UUID fromObjectID, string fromObjectName, int amount, string description,
                      TransactionType type);

        /// <summary>
        ///     Get a list of transactions that have occurred over the given interval (0 is this period of interval days, positive #s go back previous sets)
        /// </summary>
        /// <param name="groupID"></param>
        /// <param name="agentID">Requesting agentID (must be checked whether they can call this)</param>
        /// <param name="currentInterval"></param>
        /// <param name="intervalDays"></param>
        List<GroupAccountHistory> GetTransactions(UUID groupID, UUID agentID, int currentInterval, int intervalDays);

        GroupBalance GetGroupBalance(UUID groupID);

        List<AgentTransfer> GetTransactionHistory (UUID toAgentID, UUID fromAgentID, DateTime dateStart, DateTime dateEnd, uint start, uint count);
        List<AgentTransfer> GetTransactionHistory (UUID toAgentID, UUID fromAgentID, int period, string periodType);
        List<AgentTransfer> GetTransactionHistory (UUID toAgentID, int period, string periodType);
        List<AgentTransfer> GetTransactionHistory (DateTime dateStart, DateTime dateEnd, uint start, uint count);
        List<AgentTransfer> GetTransactionHistory (int period, string periodType, uint start, uint count);

        List<AgentPurchase> GetPurchaseHistory (UUID UserID, DateTime dateStart, DateTime dateEnd, uint start, uint count);
        List<AgentPurchase> GetPurchaseHistory (UUID toAgentID, int period, string periodType);
        List<AgentPurchase> GetPurchaseHistory (DateTime dateStart, DateTime dateEnd, uint start, uint count);
        List<AgentPurchase> GetPurchaseHistory (int period, string periodType, uint start, uint count);

    }

    public delegate void UserDidNotPay(UUID agentID, string identifier, string paymentTextThatFailed);

    public delegate bool CheckWhetherUserShouldPay(UUID agentID, string paymentTextThatFailed);

    public interface IScheduledMoneyModule
    {
        event UserDidNotPay OnUserDidNotPay;
        event CheckWhetherUserShouldPay OnCheckWhetherUserShouldPay;
        bool Charge(UUID agentID, int amount, string text, int daysUntilNextCharge, TransactionType type, string identifier, bool chargeImmediately);
        void RemoveFromScheduledCharge(string identifier);
    }

    public interface ISimpleCurrencyConnector : IWhiteCoreDataPlugin
    {
        /*SimpleCurrencyConfig GetConfig();
        UserCurrency GetUserCurrency(UUID agentId);
        bool UserCurrencyUpdate(UserCurrency agent);
        GroupBalance GetGroupBalance(UUID groupID);

        bool UserCurrencyTransfer(UUID toID, UUID fromID, UUID toObjectID, UUID fromObjectID, uint amount,
                                  string description, TransactionType type, UUID transactionID);
        */                          
    }
}