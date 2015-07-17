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
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using WhiteCore.Framework.DatabaseInterfaces;

namespace WhiteCore.Modules.Currency
{
    public class ScheduledPayments : IService, IScheduledMoneyModule
    {
        #region Declares

        IRegistryCore m_registry;
        IConfigSource m_config;
        IMoneyModule moneyModule;

        readonly Timer taskTimer = new Timer ();
        DateTime nextStipendPayment;
        DateTime nextGroupPayment;

		bool payStipends;
        bool payGroups;

        int directoryFee;

		string stipendMessage =	"Stipend payment";
        int stipendAmount = 0;          // How much??
        string stipendPeriod;           // period for payments
        int stipendInterval;            // period * interval between payments
        string stipendPayDay;           // the good day
        string stipendPayTime;          // the time to start work
        bool stipendsPremiumOnly;       // Premium members only
        bool stipendsLoadOldUsers;      //  ?? not sure if needed
        bool stipendsLoginRequired;     // login required in the last week
        int schedulerInterval = 0;      // seconds

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            m_config = config;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            moneyModule = m_registry.RequestModuleInterface<IMoneyModule>();
            if (moneyModule != null) //Only register if money is enabled
            {
                m_registry.RegisterModuleInterface<IScheduledMoneyModule>(this);
                m_registry.RequestModuleInterface<ISimulationBase>()
                          .EventManager.RegisterEventHandler("ScheduledPayment", ChargeNext);

 				getStipendConfig ();
                        
                AddCommands ();

                // Set up Timer in case it is needed
                taskTimer.Enabled = false;
                taskTimer.Elapsed += SchedulerTimerElapsed;

                if (payStipends || payGroups)
                {
                    InitializeScheduleTimer ();
                    MainConsole.Instance.Info ("[Currency]: Stipend paymenst enabled. Next payment: " + nextStipendPayment.ToLongDateString ());
                }
            }
        }

        void AddCommands()
        {
			MainConsole.Instance.Commands.AddCommand(
				"stipend enable",
				"stipend enable",
				"Enables stipend payments cycle",
				HandleStipendEnable, false, true);

			MainConsole.Instance.Commands.AddCommand(
				"stipend disable",
				"stipend disable",
				"Disables stipend payments",
				HandleStipendDisable, false, true);

            MainConsole.Instance.Commands.AddCommand(
                "stipend info",
                "stipend info",
                "Displays details about the current stipend payment cycle",
                HandleStipendInfo, false, true);

            MainConsole.Instance.Commands.AddCommand(
                "stipend paynow",
                "stipend paynow",
                "Pay stipends to users immediately",
                HandleStipendPayNow, false, true);

            MainConsole.Instance.Commands.AddCommand(
                "stipend reset",
                "stipend reset",
                "Reset the stipend payment cycle details to the system configurations",
                HandleStipendReset, false, true);

            MainConsole.Instance.Commands.AddCommand(
                "grouppay enable",
                "grouppay enable",
                "Enables payments to groups and group memeners",
                HandleGrouppayEnable, false, true);

            MainConsole.Instance.Commands.AddCommand(
                "grouppay disable",
                "grouppay disable",
                "Disables group payments",
                HandleGrouppayDisable, false, true);

            MainConsole.Instance.Commands.AddCommand(
                "grouppay info",
                "grouppay info",
                "Displays details about the current group payments",
                HandleGrouppayInfo, false, true);

            MainConsole.Instance.Commands.AddCommand(
                "grouppay paynow",
                "grouppay paynow",
                "Process group payments immediately",
                HandleGrouppayPayNow, false, true);


        }

		void getStipendConfig()
		{
			payStipends = false;

			var currCfg = m_config.Configs ["Currency"];
			if ( currCfg != null )
			{
				payStipends = currCfg.GetBoolean("PayStipends",false);
				stipendAmount = currCfg.GetInt("Stipend",0);
                stipendPeriod = currCfg.GetString("StipendsPeriod",Constants.STIPEND_PAY_PERIOD);
				stipendInterval = currCfg.GetInt("StipendInterval",1);
                stipendPayDay = currCfg.GetString("StipendPayDay",Constants.STIPEND_PAY_DAY);
                stipendPayTime = currCfg.GetString("StipendPayTime",Constants.STIPEND_PAY_TIME);
				stipendsPremiumOnly = currCfg.GetBoolean("StipendsPremiumOnly",false);
				stipendsLoadOldUsers = currCfg.GetBoolean ("StipendsLoadOldUsers", false);
				stipendsLoginRequired = currCfg.GetBoolean ("StipendsLoginRequired", false);
                schedulerInterval = currCfg.GetInt ("SchedulerInterval", Constants.SCHEDULER_INTERVAL);

                payGroups = currCfg.GetBoolean("GroupPayments",false);
                directoryFee = currCfg.GetInt ("PriceDirectoryFee", 0);

            }

            // some checks
            payStipends &= stipendAmount != 0;
            payGroups &= directoryFee != 0;

		}

        #endregion

        #region IScheduledMoneyModule Members

        public event UserDidNotPay OnUserDidNotPay;
        public event CheckWhetherUserShouldPay OnCheckWhetherUserShouldPay;

        public bool Charge(UUID agentID, int amount, string text, int daysUntilNextCharge, TransactionType type, string identifer, bool chargeImmediately)
        {
            if (moneyModule != null)
            {
                if (chargeImmediately)
                {
                    bool success = moneyModule.Charge(agentID, amount, text, type);
                    if (!success)
                        return false;
                }
                IScheduleService scheduler = m_registry.RequestModuleInterface<IScheduleService>();
                if (scheduler != null)
                {
                    OSDMap itemInfo = new OSDMap();
                    itemInfo.Add("AgentID", agentID);
                    itemInfo.Add("Amount", amount);
                    itemInfo.Add("Text", text);
                    itemInfo.Add("Type", (int)type);
                    SchedulerItem item = new SchedulerItem("ScheduledPayment " + identifer,
                                                           OSDParser.SerializeJsonString(itemInfo), false,
                                                           DateTime.UtcNow, daysUntilNextCharge, RepeatType.days, agentID);
                    itemInfo.Add("SchedulerID", item.id);
                    scheduler.Save(item);
                }
            }
            return true;
        }

        public void RemoveFromScheduledCharge(string identifier)
        {
            IScheduleService scheduler = m_registry.RequestModuleInterface<IScheduleService>();
            if (scheduler != null)
                scheduler.Remove("ScheduledPayment " + identifier);
        }

        object ChargeNext(string functionName, object parameters)
        {
            if (functionName.StartsWith("ScheduledPayment"))
            {
                OSDMap itemInfo = (OSDMap)OSDParser.DeserializeJson(parameters.ToString());
                IMoneyModule moneyModule = m_registry.RequestModuleInterface<IMoneyModule>();
                UUID agentID = itemInfo["AgentID"];
                string scdID = itemInfo["SchedulerID"];
                string text = itemInfo["Text"];
                int amount = itemInfo["Amount"];
                TransactionType type = !itemInfo.ContainsKey("Type") ? TransactionType.SystemGenerated : (TransactionType)itemInfo["Type"].AsInteger();
                if (CheckWhetherUserShouldPay(agentID, text))
                {
                    MainConsole.Instance.Info("[MONEY MODULE] Scheduled Payment for " + agentID + " is now running");
                    bool success = moneyModule.Charge(agentID, amount, text, type);
                    if (!success)
                    {
                        if (OnUserDidNotPay != null)
                            OnUserDidNotPay(agentID, functionName.Replace("ScheduledPayment ", ""), text);
                    }
                }
                else
                {
                    IScheduleService scheduler = m_registry.RequestModuleInterface<IScheduleService>();
                    if (scheduler != null)
                        scheduler.Remove(scdID);
                }
            }
            return null;
        }

        bool CheckWhetherUserShouldPay(UUID agentID, string text)
        {
            if (OnCheckWhetherUserShouldPay == null)
                return true;
            foreach (CheckWhetherUserShouldPay d in OnCheckWhetherUserShouldPay.GetInvocationList())
            {
                if (!d(agentID, text))
                    return false;
            }
            return true;
        }

        #endregion

        #region scheduler

        void InitializeScheduleTimer()
        {
            if (! (payStipends || payGroups))
                return;

            nextStipendPayment = GetStipendPaytime ();
            nextGroupPayment = nextStipendPayment + new TimeSpan(0, 0, Constants.GROUP_PAYMENTS_DELAY);  

            taskTimer.Interval = schedulerInterval * 1000;         // seconds 
            taskTimer.Enabled = true;
        }

        void SchedulerTimerElapsed (object sender, ElapsedEventArgs elapsedEventArgs)
        {
            // ok time for some payments
            taskTimer.Enabled = false;

            // Stipend payments needed yet?
            if (DateTime.Now > nextStipendPayment)
                ProcessStipendPayments ();

            // Group payments needed yet?
            if (DateTime.Now > nextGroupPayment)
                ProcessGroupPayments ();

            // reset for the next cycle
            taskTimer.Interval = schedulerInterval * 1000;     // reset in case it has been 'fiddled with'
            taskTimer.Enabled = true;
        }

        /// <summary>
        /// Calculate the number of days between payments.
        /// </summary>
        /// <returns>The cycle days.</returns>
        int PaymentCycleDays()
        {
            var payPeriod = stipendPeriod.Substring (0, 1);
            int periodMult;
            switch (payPeriod)
            {
            case "d":
                periodMult = 1;
                break;
            case "w":
                periodMult = 7;
                break;
            case "m":
                periodMult = 30;         // I know... :) 
                break;
            case "y":
                periodMult = 365;        // a bit on the long side
                break;
            default:
                periodMult =  7;         // week 
                break;
            }

            if (stipendInterval < 1)
                stipendInterval = 1;

            return periodMult * stipendInterval;

        }

        /// <summary>
        /// Convert day to DdoyOfWeek number.
        /// </summary>
        /// <returns>The day of week.</returns>
        /// <param name="payday">Payday.</param>
        int PayDayOfWeek(string payday)
        {
            int dow;
            var payDay = payday.Substring (0, 2);
            switch (payDay)
            {
            case "su":
                dow = 0;
                break;
            case "mo":
                dow = 1;
                break;
            case "tu":
                dow = 2;
                break;
            case "we":
                dow = 3;
                break;
            case "th":
                dow = 4;
                break;
            case "fr":
                dow = 5;
                break;
            case "sa":
                dow = 6;
                break;
            case "in":          // interval period specified rather than a particular day
                dow = -1;
                break;
            default:
                dow = 2;
                break;
            }

            return dow;
        }

        /// <summary>
        /// Gets the date and time for the newt stipend payment.
        /// </summary>
        /// <returns>The stipend paytime.</returns>
        DateTime GetStipendPaytime()
        {

            int paydayDow;
            if (stipendPayDay != "")            
            {
                // we have a pay day
                paydayDow = PayDayOfWeek( stipendPayDay);
            } else
                paydayDow = (int) DateTime.Now.DayOfWeek;           

            // time to start processing
            int stipHour;
            int.TryParse( stipendPayTime.Substring (0, 2), out stipHour);
            int stipMin;
            int.TryParse( stipendPayTime.Substring (3, 2), out stipMin);

            var today = DateTime.Now;
            int todayDow = (int) today.DayOfWeek;
            if (paydayDow < todayDow)
                paydayDow += PaymentCycleDays();                           

            double dayOffset = (paydayDow - todayDow);              // # days to payday

            DateTime nxtPayTime = (today.Date + new TimeSpan(stipHour, stipMin, 0)).AddDays (dayOffset);

            return nxtPayTime;  

        }

        #endregion

        #region stipends

        void StipendInfo()
        {
            if (!payStipends)
            {
                MainConsole.Instance.Info ("[Currency]: Stipend payments are not enabled.");
                return;
            }

            TimeSpan nextSched = nextStipendPayment - DateTime.Now;

            MainConsole.Instance.InfoFormat ("[Currency]: The next stipend payment is scheduled for {0} at {1}",
                nextStipendPayment.ToLongDateString(), stipendPayTime);
            MainConsole.Instance.InfoFormat ("            Time to next payment schedule: {0} day{1} {2} hour{3} {4} minute{5}",
                nextSched.Days,
                nextSched.Days == 1 ? "" : "s",
                nextSched.Hours,
                nextSched.Hours == 1 ? "" : "s",
                nextSched.Minutes,
                nextSched.Minutes == 1 ? "" :"s"
            );
            MainConsole.Instance.InfoFormat ("            Stipend : {0} {1}",
                moneyModule.InWorldCurrencySymbol, stipendAmount);
            MainConsole.Instance.InfoFormat ("            Cycle   : {0} {1}{2}",
                stipendInterval, stipendPeriod, stipendInterval == 1 ? "" : "s");
        }

        void ProcessStipendPayments()
        {
            if (stipendAmount == 0)
            {
                MainConsole.Instance.Warn ("[Currency]: Stipend payments enabled but amount is not set");
                return;
            }
            
            MainConsole.Instance.Warn ("[Currency]: Processing of Stipend payments commenced");

            var rightNow = DateTime.Now.ToUniversalTime();
            var userService = m_registry.RequestModuleInterface<IUserAccountService> ();
            var agentInfo = Framework.Utilities.DataManager.RequestPlugin<IAgentInfoConnector> ();
            List<UserAccount> users;
            int payments = 0;
            int payValue = 0;
            bool xfrd;
                
            users = userService.GetUserAccounts (new List<UUID> { UUID.Zero }, 0, stipendsPremiumOnly ? 600 : 0);
            foreach (UserAccount user in users)
            {
                if (Utilities.IsSystemUser (user.PrincipalID))
                    continue;

                if (!stipendsPremiumOnly && stipendsLoginRequired)
                {
                    bool status;
					UserInfo usrInfo = agentInfo.Get (user.PrincipalID.ToString(), true, out status);
					DateTime loginWin = usrInfo.LastLogin.AddSeconds(Constants.STIPEND_RECENT_LOGIN_PERIOD);
					if (rightNow > loginWin)
                        continue;
                }

                // pay them...
                xfrd = moneyModule.Transfer(
                    user.PrincipalID,
					(UUID) Constants.BankerUUID,
					stipendAmount,
					stipendMessage,
					TransactionType.SystemGenerated
				);

                // keep track
                if (xfrd)
                {
                    payments ++;
                    payValue += stipendAmount;
                }
            }

            // remove the payments from the banker account 
            if (payValue > 0)
            {
                moneyModule.Transfer(
                    (UUID) Constants.BankerUUID,
                    UUID.Zero,
                    payValue,
                    "Stipends reset",
                    TransactionType.SystemGenerated
                );
            }

            var elapsed = DateTime.Now - rightNow;
            MainConsole.Instance.InfoFormat ("[Currency]: Processed {0} stipend payments for {1} users in {2} secs",
                payments, payValue, elapsed.Seconds);

            // reset for the next payment
            nextStipendPayment = GetStipendPaytime ();
            MainConsole.Instance.InfoFormat ("[Currency]: The next stipend payment is scheduled for {0}", nextStipendPayment.ToLongDateString());

        }

        #endregion

        #region group payments

        void GroupPaymentsInfo()
        {
            if (!payGroups)
            {
                MainConsole.Instance.Info ("[Currency]: Group payments are not enabled.");
                return;
            }

            IScene scene = MainConsole.Instance.ConsoleScenes [0];
            IGroupsModule groupsModule = scene.RequestModuleInterface<IGroupsModule>();
            IDirectoryServiceConnector dir_service = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();

            int searchFee = 0;
            int liableGroups = 0;
            var groups = groupsModule.GetAllGroups ((UUID) Constants.BankerUUID);
            if (groups != null)
            {

               // check each group
                foreach (UUID groupID in groups)
                {
                    var grpParcels = dir_service.GetParcelByOwner (groupID);
                    foreach (var parcel in grpParcels)
                    {
                        if (parcel.LandData.SalePrice > 0)
                        {
                            searchFee += directoryFee;
                            liableGroups ++;
                        }
                    }
                }
            }
                
            TimeSpan nextSched = nextStipendPayment - DateTime.Now;

            MainConsole.Instance.InfoFormat ("[Currency]: The next Group payment cycle is scheduled for {0} at {1}",
                nextStipendPayment.ToLongDateString(), stipendPayTime);
            MainConsole.Instance.InfoFormat ("            Time to next payment schedule: {0} day{1} {2} hour{3} {4} minute{5}",
                nextSched.Days,
                nextSched.Days == 1 ? "" : "s",
                nextSched.Hours,
                nextSched.Hours == 1 ? "" : "s",
                nextSched.Minutes,
                nextSched.Minutes == 1 ? "" :"s"
            );
            MainConsole.Instance.InfoFormat ("            Cycle   : {0} {1}{2}",
                stipendInterval, stipendPeriod, stipendInterval == 1 ? "" : "s");
            MainConsole.Instance.InfoFormat ("            Groups  : {0}", liableGroups);
            MainConsole.Instance.InfoFormat ("            Fee     : {0}{1}", moneyModule.InWorldCurrencySymbol, searchFee);
        }

        void ProcessGroupPayments()
        {
            if (directoryFee == 0)
                return;

            var startGroups = DateTime.Now;
            MainConsole.Instance.Warn ("[Currency]: Processing of Group liabilities and payments commenced");

            int grpMembersLiable;
            int liablePayments;
            int grpsPayments;
            int grpDividends;

            ProcessGroupLiability (out grpMembersLiable, out liablePayments);
            ProcessGroupDividends (out grpsPayments, out grpDividends);

            // reset for the next payment
            nextGroupPayment = GetStipendPaytime () +  + new TimeSpan(0, 0, Constants.GROUP_PAYMENTS_DELAY);

            MainConsole.Instance.InfoFormat ("[Currency]: Processed {0} group liability payments for {1}{2}",
                grpMembersLiable, liablePayments, moneyModule.InWorldCurrencySymbol);
            
            MainConsole.Instance.InfoFormat ("[Currency]: Processed {0} group dividend payments for {1}{2}",
                grpsPayments, grpDividends, moneyModule.InWorldCurrencySymbol);
            
            var elapsed = DateTime.Now - startGroups;
            MainConsole.Instance.InfoFormat ("[Currency]: Group processing completed in {0} secs", elapsed);
            MainConsole.Instance.InfoFormat ("[Currency]: The next Group payment is scheduled for {0}",
                nextGroupPayment.ToLongDateString ());
        }


        void ProcessGroupLiability(out int grpMembersLiable, out int liablePayments)
        {
            grpMembersLiable = 0;
            liablePayments = 0;

            if (!payGroups || directoryFee == 0)                 
                return;

            // - Check if the group has parcels
            // - Are the parcels in Search ?
            // - Yes, make a GroupLiability Task for each of the Parcels (Amount comes out of INI -> PriceDirectoryFee)
            // - Check if there's money in the Group
            // - If there's money already there, substract the money by calling the Tasks
            // - If there's no money, check which users have the "Accountability" role task and pull money from their accounts
            // into the group so the payments can be done

            IScene scene = MainConsole.Instance.ConsoleScenes [0];
            IGroupsModule groupsModule = scene.RequestModuleInterface<IGroupsModule>();
            IDirectoryServiceConnector dir_service = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();

            var groups = groupsModule.GetAllGroups ((UUID) Constants.BankerUUID);

            if (groups == null | groups.Count == 0)
                return;
            
            // check each group
            GroupBalance grpBalance;
            int searchFee = 0;
            bool xfrd;

            foreach (UUID groupID in groups)
            {

                var grpParcels = dir_service.GetParcelByOwner (groupID);
                foreach (var parcel in grpParcels)
                {
                    if (parcel.LandData.SalePrice > 0)
                        searchFee += directoryFee;
                }
                        
                grpBalance = moneyModule.GetGroupBalance(groupID);
                // This does not appear to be set anywhere else so use it as the total group liability for land sales
                grpBalance.ParcelDirectoryFee = searchFee;    

                //TODO: Add a groupTranfer() process to provide for actually saving group monies !!
                // moneyModule.UpdateGroupBalance(groupID, grpBalance);

                // a bit of optimisation - no need to continue if there are no fees to be paid
                if (searchFee == 0)
                    continue;
                
                // find how many members are accountable for fees
                var grpMembers = groupsModule.GetGroupMembers((UUID) Constants.BankerUUID, groupID);
                List<UUID> payMembers = new List<UUID>();
                foreach (var member in grpMembers)
                {
                    if (Utilities.IsSystemUser (member.AgentID))
                        continue;

                    // Is member accountable for fees?
                    if (((GroupPowers) member.AgentPowers & GroupPowers.Accountable) == GroupPowers.Accountable)
                        payMembers.Add (member.AgentID);
                }
                if (payMembers.Count == 0)      // no one to pay??
                    continue;
                
                int memberShare = grpBalance.ParcelDirectoryFee / payMembers.Count;         // this should be integer division so truncated (5 /4 = 1)
                if (memberShare == 0)                                                       // share of fee < 1 per user
                    memberShare = 1;

                foreach( var memberID in payMembers)
                {
                    // check user balance
                    var userBalance = moneyModule.Balance(memberID);
                    if (userBalance <= 0)
                        continue;                                                           // let them off the hook (for now - could still charge and go credit balance)

                    // pay the man...
                    xfrd = moneyModule.Transfer(
                        (UUID) Constants.BankerUUID,
                        memberID,
                        memberShare,
                        "Group directory fee share",
                        TransactionType.SystemGenerated
                    );

                    // keep track
                    if (xfrd)
                    {
                        grpMembersLiable ++;
                        liablePayments += directoryFee;
                    }
                }
            }
            return;

        }

        void ProcessGroupDividends (out int grpsPayments, out int grpDividends)
        {
            grpsPayments = 0;
            grpDividends = 0;

            // * Group Payments
            // - If there's money in the group it will have to be divided by the people that have the "Accountability" role task
            // - Check how many users there are with that role task
            // - Divide the amount of money by the amount of users (make sure the amount is a whole number)
            // - Create Task for each user to be payed
            // - If there is a remaining amount of money, leave it in the group balance for the next week

            IScene scene = MainConsole.Instance.ConsoleScenes [0];
            IGroupsModule groupsModule = scene.RequestModuleInterface<IGroupsModule>();

            var groups = groupsModule.GetAllGroups ((UUID) Constants.BankerUUID);
            if (groups == null | groups.Count == 0)
                return;

            // check each group
            GroupBalance grpBalance;
            bool xfrd;

            foreach (UUID groupID in groups)
            {
                grpBalance = moneyModule.GetGroupBalance(groupID);
                if (grpBalance.ParcelDirectoryFee <= 0)
                    continue;

                // find how many members are accountable for fees and pay them dividends
                var grpMembers = groupsModule.GetGroupMembers((UUID) Constants.BankerUUID, groupID);
                List<UUID> payMembers = new List<UUID>();
                foreach (var member in grpMembers)
                {
                    if (Utilities.IsSystemUser (member.AgentID))
                        continue;

                    // Is member accountable for fees?
                    if (((GroupPowers) member.AgentPowers & GroupPowers.Accountable) == GroupPowers.Accountable)
                        payMembers.Add (member.AgentID);
                 }
                if (payMembers.Count == 0)      // no one to pay??
                    continue;

                int dividend = grpBalance.ParcelDirectoryFee / payMembers.Count;    // this should be integer division so truncated (5 /4 = 1)
                if (dividend == 0)                                                  // insufficient funds < 1 per user
                    continue;
                
                foreach( var memberID in payMembers)
                {

                    // pay them...
                    xfrd = moneyModule.Transfer(
                            memberID,
                            (UUID) Constants.BankerUUID,
                            dividend,
                            "Group dividend",
                            TransactionType.SystemGenerated
                        );

                    // keep track
                    if (xfrd)
                    {
                        grpsPayments ++;
                        grpDividends += dividend;
                    }
                }
            }
            return;
        }

        #endregion

        #region console commands
		protected void HandleStipendEnable(IScene scene, string[] cmd)
		{
			if ( payStipends) 
			{
				MainConsole.Instance.Info ("[Currency]: Stipend payments are already enabled");
				return;
			}

			getStipendConfig ();

			var okConfig = payStipends;
			bool promptUser = false;

            if (!okConfig)
            {
                var pu = MainConsole.Instance.Prompt (
                    "The Stipend configuration may be invalid. Do you wish to verify details? (yes, no)", "no").ToLower ();
                if (pu.StartsWith ("y"))
                    promptUser = true;
                else
                    return;
            } else
            {
                var pmtu = MainConsole.Instance.Prompt ("Do you wish to revise the configuration? (yes, no)", "no").ToLower ();
                promptUser = pmtu.StartsWith ("y");
            }
				
			if(promptUser)
			{

			    // prompt for details...");
                stipendAmount = int.Parse (MainConsole.Instance.Prompt ("Stipend amount ?", "0"));
                if (stipendAmount == 0)
                    return;

                var respDay = new List<string>();
                respDay.Add ("sunday");    
                respDay.Add ("monday");
                respDay.Add ("tuesday");
                respDay.Add ("wednesday");
                respDay.Add ("thursday");  
                respDay.Add ("friday");    
                respDay.Add ("saturday");  
                respDay.Add ("interval");    

                var pday = MainConsole.Instance.Prompt("Pay day? (Assumes weekly period)\n (sun, mon, tue, wed, thu, fri, sat, interval)", Constants.STIPEND_PAY_DAY).ToLower ();
                stipendPayDay = respDay [PayDayOfWeek (pday)];
                if (stipendPayDay.StartsWith("i"))
                {
                    // get a time period then
                    var respPeriod = new List<string>();
                    respPeriod.Add ("month");
                    respPeriod.Add ("year");  
                    respPeriod.Add ("none");    

                    stipendPeriod = MainConsole.Instance.Prompt("Time period between payments?", Constants.STIPEND_PAY_PERIOD, respPeriod).ToLower ();
                    if (stipendPeriod.StartsWith("n"))
                        return;
                        
                    stipendPayDay = "";
                }

                stipendInterval = int.Parse(MainConsole.Instance.Prompt (
                        "Number of time periods between payments? (1 > Every period 2 > every two periods etc.)",
                        Constants.STIPEND_PAY_INTERVAL.ToString()));
                if (stipendInterval == 0)
                    return;
                
                stipendPayTime = MainConsole.Instance.Prompt("Payment time? (hh:mm)", Constants.STIPEND_PAY_TIME);

                stipendsPremiumOnly = MainConsole.Instance.Prompt ("Pay premium users only? (yes/no)", "no").ToLower() == "yes";
                if (!stipendsPremiumOnly)
                    stipendsLoginRequired = MainConsole.Instance.Prompt ("Require a recent login for Free members? (yes/no)", "no").ToLower() == "yes";
                // not sure about this one??  //StipendsLoadOldUsers = currCfg.GetBoolean ("StipendsLoadOldUsers", false);

            }

			// ensure we are enabled
			payStipends = true;
            InitializeScheduleTimer();

            MainConsole.Instance.Info ("[Currency]; Stipend payments have been enabled");
            MainConsole.Instance.CleanInfoFormat ("          The next stipend payment of {0}{1} is scheduled for {2}",
                moneyModule.InWorldCurrencySymbol, stipendAmount, nextStipendPayment.ToLongDateString());

		}

		protected void HandleStipendDisable(IScene scene, string[] cmd)
		{
			if ( !payStipends) 
			{
				MainConsole.Instance.Info ("[Currency]: Stipend payments are already disabled");
				return;
			}

			payStipends = false;
			MainConsole.Instance.Info ("[Currency]: Stipend payments have been disabled");
		}

		protected void HandleStipendInfo(IScene scene, string[] cmd)
        {
            StipendInfo ();
        }

        protected void HandleStipendPayNow(IScene scene, string[] cmd)
        {
            if ( !payStipends) 
            {
                MainConsole.Instance.Info ("[Currency]: Please enable Stipend payments first!");
                return;
            }

            nextStipendPayment = DateTime.Now; 
            taskTimer.Enabled = false;
            taskTimer.Interval = 10 * 1000;
            taskTimer.Enabled = true;
            MainConsole.Instance.InfoFormat ("[Currency]: Stipend payments will commence in {0} seconds.", 10);

        }


        protected void HandleStipendReset(IScene scene, string[] cmd)
        {
            getStipendConfig ();
            InitializeScheduleTimer();

            MainConsole.Instance.Info ("[Currency]; Stipend configuration reloaded");
            StipendInfo ();

        }


        protected void HandleGrouppayEnable(IScene scene, string[] cmd)
        {
            if ( payGroups) 
            {
                MainConsole.Instance.Info ("[Currency]: Group payments are already enabled");
                return;
            }

            // ensure we have schedule details
            getStipendConfig ();

            // ensure we are enabled
            payGroups = true;
            InitializeScheduleTimer();

            MainConsole.Instance.Info ("[Currency]; Group payments have been enabled");
            MainConsole.Instance.CleanInfoFormat ("          The next group payment cycle is scheduled for {0}",
                  nextGroupPayment.ToLongDateString());

        }

        protected void HandleGrouppayDisable(IScene scene, string[] cmd)
        {
            if ( !payGroups) 
            {
                MainConsole.Instance.Info ("[Currency]: Group payments are already disabled");
                return;
            }

            payGroups = false;
            MainConsole.Instance.Info ("[Currency]: Group payments have been disabled");
        }

        protected void HandleGrouppayInfo(IScene scene, string[] cmd)
        {
            GroupPaymentsInfo ();
        }

        protected void HandleGrouppayPayNow(IScene scene, string[] cmd)
        {
            if ( !payGroups) 
            {
                MainConsole.Instance.Info ("[Currency]: Please enable Group payments first!");
                return;
            }

            nextGroupPayment = DateTime.Now; 
            taskTimer.Enabled = false;
            taskTimer.Interval = 10 * 1000;
            taskTimer.Enabled = true;
            MainConsole.Instance.InfoFormat ("[Currency]: Group payments will commence in {0} seconds.", 10);

        }

        #endregion
    }
}
