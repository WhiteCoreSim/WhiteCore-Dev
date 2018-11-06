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
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Other;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Currency
{
    public class ScheduledPayments : IService, IScheduledMoneyModule
    {
        #region Declares

        public WhiteCoreEventManager eventManager = new WhiteCoreEventManager ();

        IRegistryCore m_registry;
        IConfigSource m_config;
        IMoneyModule moneyModule;
        IScheduleService scheduler;
        ISchedulerDataPlugin sched_database;

        string currencySymbol = "";

        readonly Timer taskTimer = new Timer ();
        DateTime nextStipendPayment;
        DateTime nextScheduledPayment;
        DateTime nextGroupPayment;
        DateTime nextGroupDividend;

        bool payStipends;
        bool payGroups;

        int directoryFee;

        string stipendMessage = "Stipend payment";
        int stipendAmount = 0;          // How much??
        string stipendPeriod;           // period for payments
        int stipendInterval;            // period * interval between payments
        string stipendPayDay;           // the good day
        string stipendPayTime;          // the time to start work
        bool stipendsPremiumOnly;       // Premium members only
        // bool stipendsLoadOldUsers;      //  only needed for old system for reloa
        bool stipendsLoginRequired;     // login required in the last week
        int schedulerInterval = 300;    // default to 5 mins
        bool showSchedulerTick = false;

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            m_config = config;
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup ()
        {
            moneyModule = m_registry.RequestModuleInterface<IMoneyModule> ();
            if ((moneyModule != null) && moneyModule.IsLocal) //Only register if money is enabled and is local
            {
                m_registry.RegisterModuleInterface<IScheduledMoneyModule> (this);
                eventManager.RegisterEventHandler ("ScheduledPayment", ChargeNext);

                scheduler = m_registry.RequestModuleInterface<IScheduleService> ();
                sched_database = Framework.Utilities.DataManager.RequestPlugin<ISchedulerDataPlugin> ();

                currencySymbol = moneyModule.InWorldCurrencySymbol;

                getStipendConfig ();

                AddCommands ();

                // Set up Timer
                taskTimer.Enabled = false;
                taskTimer.Elapsed += SchedulerTimerElapsed;

                InitializeScheduleTimer ();
            }
        }

        void AddCommands ()
        {
            MainConsole.Instance.Commands.AddCommand (
                "stipend enable",
                "stipend enable",
                "Enables stipend payments cycle",
                HandleStipendEnable, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "stipend disable",
                "stipend disable",
                "Disables stipend payments",
                HandleStipendDisable, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "stipend info",
                "stipend info",
                "Displays details about the current stipend payment cycle",
                HandleStipendInfo, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "stipend paynow",
                "stipend paynow",
                "Pay stipends to users immediately",
                HandleStipendPayNow, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "stipend reset",
                "stipend reset",
                "Reset the stipend payment cycle details to the system configurations",
                HandleStipendReset, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "grouppay enable",
                "grouppay enable",
                "Enables payments to groups and group members",
                HandleGrouppayEnable, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "grouppay disable",
                "grouppay disable",
                "Disables group payments",
                HandleGrouppayDisable, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "grouppay info",
                "grouppay info",
                "Displays details about the current group payments",
                HandleGrouppayInfo, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "grouppay paynow",
                "grouppay paynow",
                "Process group payments immediately",
                HandleGrouppayPayNow, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "grouppay pay dividends",
                "grouppay pay dividends",
                "Process group dividends immediately",
                HandleGrouppayPayDividends, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "scheduled info",
                "scheduled info",
                "Displays details about any pending scheduled payments",
                HandleScheduledPayInfo, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "scheduled paynow",
                "scheduled paynow",
                "Process scheduled payments immediately",
                HandleScheduledPayNow, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "show scheduler tick",
                "show scheduler tick",
                "Show scheduler activity in logs",
                HandleShowSchedulerTick, false, true);

        }

        void getStipendConfig ()
        {
            payStipends = false;

            var currCfg = m_config.Configs ["Currency"];
            if (currCfg != null) {
                payStipends = currCfg.GetBoolean ("PayStipends", false);
                stipendAmount = currCfg.GetInt ("Stipend", 0);
                stipendPeriod = currCfg.GetString ("StipendPeriod", Constants.STIPEND_PAY_PERIOD);
                stipendInterval = currCfg.GetInt ("StipendInterval", 1);
                stipendPayDay = currCfg.GetString ("StipendPayDay", Constants.STIPEND_PAY_DAY);
                stipendPayTime = currCfg.GetString ("StipendPayTime", Constants.STIPEND_PAY_TIME);
                stipendsPremiumOnly = currCfg.GetBoolean ("StipendsPremiumOnly", false);
                // stipendsLoadOldUsers = currCfg.GetBoolean ("StipendsLoadOldUsers", false);
                stipendsLoginRequired = currCfg.GetBoolean ("StipendsLoginRequired", false);
                schedulerInterval = currCfg.GetInt ("SchedulerInterval", Constants.SCHEDULER_INTERVAL);

                payGroups = currCfg.GetBoolean ("GroupPayments", false);
                directoryFee = currCfg.GetInt ("PriceDirectoryFee", 0);

            }

            // some sanity checks
            payStipends &= stipendAmount != 0;
            payGroups &= directoryFee != 0;

        }

        #endregion

        #region IScheduledMoneyModule Members

        // alternatives for the MoneyModule
        public int UploadCharge { get { return moneyModule.UploadCharge; } }

        public int GroupCreationCharge { get { return moneyModule.GroupCreationCharge; } }

        public int DirectoryFeeCharge { get { return moneyModule.DirectoryFeeCharge; } }

        public event UserDidNotPay OnUserDidNotPay;
        public event CheckWhetherUserShouldPay OnCheckWhetherUserShouldPay;

        public bool Charge (UUID agentID, int amount, string description, TransactionType transType,
            string identifer, bool chargeImmediately, bool runOnce)
        {
            var userService = m_registry.RequestModuleInterface<IUserAccountService> ();
            var user = userService.GetUserAccount (null, agentID);

            if (moneyModule != null) {
                if (chargeImmediately) {
                    bool success = moneyModule.Transfer (
                        (UUID)Constants.BankerUUID,            // pay the Banker
                        agentID,
                        amount,
                        description,
                        transType
                    );
                    if (!success) {
                        MainConsole.Instance.WarnFormat ("[Currency]: Unable to process {0} payment of {1}{2} from {3}",
                             description, currencySymbol, amount, user.Name);
                        return false;
                    }

                    MainConsole.Instance.WarnFormat ("[Currency]: Payment for {0} of {1}{2} from {3} has been paid",
                        description, currencySymbol, amount, user.Name);

                }

                if (!runOnce) {
                    // add a re-occurring scheduled payment
                    if (scheduler != null) {
                        string scid = UUID.Random ().ToString ();

                        OSDMap itemInfo = new OSDMap ();
                        itemInfo.Add ("AgentID", agentID);
                        itemInfo.Add ("Amount", amount);
                        itemInfo.Add ("Text", description);
                        itemInfo.Add ("Type", (int)transType);
                        itemInfo.Add ("SchedulerID", scid);

                        SchedulerItem item = new SchedulerItem (
                                             "ScheduledPayment " + identifer,                         // name
                                             OSDParser.SerializeJsonString (itemInfo),                // scheduled payment details
                                             false,                                                   // run once
                                             GetStipendPaytime (Constants.SCHEDULED_PAYMENTS_DELAY),  // next cycle + delay
                                             agentID);                                                // user to charge

                        // we need to use our own id here
                        item.id = scid;
                        scheduler.Save (item);
                    } else
                        MainConsole.Instance.WarnFormat ("[Currency]: Unable to add a new scheduled {0} payment of {1}{2} for {3}",
                            description, currencySymbol, amount, user.Name);
                }
            }
            return true;
        }

        public void RemoveFromScheduledCharge (string identifier)
        {
            // NOTE:  THe identifier is actually the 'fire_function' in the database
            // format is "ScheduledPayment [ShowInDirectory: 44089279-b5b0-49f2-a92b-ff2c2ab063e7]" (UUID is the Landdata GlobalID) 
            if (scheduler != null)
                scheduler.RemoveFireFunction ("ScheduledPayment " + identifier);
        }

        public void RemoveDirFeeScheduledCharge (string identifier)
        {
            // NOTE:  THe identifier is actually the 'fire_function' in the database
            if (scheduler == null)
                return;

            var fireFunction = "ScheduledPayment " + identifier;
            var schItem = scheduler.GetFunctionItem (fireFunction);
            if (schItem == null)
                return;

            // is this an 'oops' setting?
            DateTime gracePeriod = schItem.StartTime.AddHours (Constants.DIRECTORYFEE_GRACE_PERIOD);
            if (DateTime.Now > gracePeriod) {
                // Check if the fee has been charged at least once
                var firstCharge = schItem.TimeToRun.AddDays (-PaymentCycleDays ());
                if (firstCharge <= schItem.StartTime) {
                    // We have not been through at least one cycle
                    schItem.RunOnce = true;
                    scheduler.Save (schItem);

                    return;    // the schedule item will be removed once it is paid
                }
            }

            // all good.. just clear it
            scheduler.RemoveFireFunction (fireFunction);

        }

        object ChargeNext (string functionName, object parameters)
        {
            if (functionName.StartsWith ("ScheduledPayment", StringComparison.Ordinal)) {
                OSDMap itemInfo = (OSDMap)OSDParser.DeserializeJson (parameters.ToString ());
                UUID agentID = itemInfo ["AgentID"];
                string scdID = itemInfo ["SchedulerID"];
                string description = itemInfo ["Text"];
                int amount = itemInfo ["Amount"];
                TransactionType transType = !itemInfo.ContainsKey ("Type") ? TransactionType.SystemGenerated : (TransactionType)itemInfo ["Type"].AsInteger ();

                // allow for delayed start before charge commences
                SchedulerItem schItem = null;
                if (scheduler != null) {
                    schItem = scheduler.Get (scdID);
                    if (schItem == null || schItem.StartTime >= DateTime.Now)
                        return null;
                }

                var userService = m_registry.RequestModuleInterface<IUserAccountService> ();
                var user = userService.GetUserAccount (null, agentID);

                if (CheckWhetherUserShouldPay (agentID, description)) {
                    bool success = moneyModule.Transfer (
                        (UUID)Constants.BankerUUID,            // pay the Banker
                        agentID,
                        amount,
                        description,
                        transType
                    );
                    if (!success) {
                        MainConsole.Instance.WarnFormat ("[Currency]: Unable to process {0} payment of {1}{2} from {3}",
                            description, currencySymbol, amount, user.Name);
                        if (OnUserDidNotPay != null)
                            OnUserDidNotPay (agentID, functionName.Replace ("ScheduledPayment ", ""), description);
                    }

                    MainConsole.Instance.InfoFormat ("[Currency]: Scheduled payment for {0} of {1}{2} from {3} has been paid",
                        description, currencySymbol, amount, user.Name);

                    // check for a 'runOnce' charge
                    if ((schItem != null) && schItem.RunOnce)
                        scheduler.RemoveID (scdID);

                } else {
                    if (scheduler != null)
                        scheduler.RemoveID (scdID);
                }
            }
            return null;
        }


        bool CheckWhetherUserShouldPay (UUID agentID, string text)
        {
            if (OnCheckWhetherUserShouldPay == null)
                return true;

            bool foundParcel = false;
            foreach (CheckWhetherUserShouldPay d in OnCheckWhetherUserShouldPay.GetInvocationList ()) {
                if (d (agentID, text))
                    foundParcel = true;
            }
            return foundParcel;
        }

        #endregion

        #region scheduler

        string ElapsedTime (TimeSpan elapsed)
        {
            string strElapsed = "";

            if (elapsed.Days > 0)
                strElapsed += string.Format ("{0} day{1} ", elapsed.Days, elapsed.Days == 1 ? "" : "s");

            strElapsed += string.Format ("{0} hour{1} {2} minute{3} {4} second{5}",
                elapsed.Hours,
                elapsed.Hours == 1 ? "" : "s",
                elapsed.Minutes,
                elapsed.Minutes == 1 ? "" : "s",
                elapsed.Seconds,
                elapsed.Seconds == 1 ? "" : "s"
            );

            return strElapsed;
        }

        void InitializeScheduleTimer ()
        {
            if (payStipends) {
                nextStipendPayment = GetStipendPaytime (0);
                MainConsole.Instance.Info ("[Currency]: Stipend payments enabled. Next payment: " +
                                           string.Format ("{0:f}", nextStipendPayment));
            }
            if (payGroups) {
                nextGroupPayment = GetGroupPaytime (0);
                nextGroupDividend = GetGroupPaytime (Constants.GROUP_DISBURSMENTS_DELAY);
                MainConsole.Instance.Info ("[Currency]: Group payments enabled.   Next payment: " +
                                           string.Format ("{0:f}", nextGroupPayment));
            }

            // scheduled payments are always processed
            //nextScheduledPayment = GetStipendPaytime(Constants.SCHEDULED_PAYMENTS_DELAY);  
            nextScheduledPayment = DateTime.Now.AddSeconds (Constants.SCHEDULER_INTERVAL);

            taskTimer.Interval = schedulerInterval * 1000;         // seconds 
            taskTimer.Enabled = true;
            if (showSchedulerTick)
                MainConsole.Instance.Info ("[Scheduler]: Timer enabled");
        }

        void SchedulerTimerElapsed (object sender, ElapsedEventArgs elapsedEventArgs)
        {
            // check if time for some payments
            taskTimer.Enabled = false;

            var checkTime = DateTime.Now.AddSeconds (schedulerInterval / 2);

            // Stipend payments needed yet?
            if (payStipends && (checkTime > nextStipendPayment))
                ProcessStipendPayments ();

            // What about Groups?
            if (payGroups) {
                // liabilities?
                if (checkTime > nextGroupPayment)
                    ProcessGroupLiability ();

                // dividend?
                if (checkTime > nextGroupDividend)
                    ProcessGroupDividends ();
            }

            // Scheduled payments then?
            if (checkTime > nextScheduledPayment)
                ProccessScheduledPayments ();

            // reset for the next cycle
            taskTimer.Interval = schedulerInterval * 1000;     // reset in case it has been 'fiddled with' (manual paynow)
            taskTimer.Enabled = true;

            if (showSchedulerTick)
                MainConsole.Instance.Info ("[Scheduler]: tick");
        }

        /// <summary>
        /// Calculate the number of days between payments.
        /// </summary>
        /// <returns>The cycle days.</returns>
        int PaymentCycleDays ()
        {
            var payPeriod = stipendPeriod.Substring (0, 1);
            int periodMult;
            switch (payPeriod) {
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
                periodMult = 7;         // week 
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
        int PayDayOfWeek (string payday)
        {
            int dow;
            var payDay = payday.Substring (0, 2);
            switch (payDay) {
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
        /// Gets the date and time for the next stipend payment.
        /// </summary>
        /// <returns>The stipend paytime.</returns>
        public DateTime GetStipendPaytime (int minsOffset)
        {

            int paydayDow;
            if (stipendPayDay != "") {
                // we have a pay day
                paydayDow = PayDayOfWeek (stipendPayDay);
            } else
                paydayDow = (int)DateTime.Now.DayOfWeek;

            // time to start processing
            int stipHour;
            int.TryParse (stipendPayTime.Substring (0, 2), out stipHour);
            int stipMin;
            int.TryParse (stipendPayTime.Substring (3, 2), out stipMin);

            var today = DateTime.Now;
            int todayDow = (int)today.DayOfWeek;
            if (paydayDow < todayDow)
                paydayDow += PaymentCycleDays ();

            double dayOffset = (paydayDow - todayDow);              // # days to payday

            DateTime nxtPayTime = (today.Date + new TimeSpan (stipHour, stipMin + minsOffset, 0)).AddDays (dayOffset);
            var cycleDays = PaymentCycleDays ();
            while (nxtPayTime < DateTime.Now) {
                // process time was earlier than today 
                nxtPayTime = nxtPayTime.AddDays ((double)cycleDays);
            }

            return nxtPayTime;

        }

        /// <summary>
        /// Gets the date and time for the next group charges/dividend payment.
        /// </summary>
        /// <returns>The group paytime.</returns>
        public DateTime GetGroupPaytime (int minsOffset)
        {
            // group payments/disbursments are processed daily 
            int stipHour;
            int.TryParse (stipendPayTime.Substring (0, 2), out stipHour);
            int stipMin;
            int.TryParse (stipendPayTime.Substring (3, 2), out stipMin);

            var today = DateTime.Now;

            // offset group payments from normal stipend processing time
            var groupOffset = Constants.GROUP_PAYMENTS_DELAY + minsOffset;

            DateTime nxtPayTime = (today.Date + new TimeSpan (stipHour, stipMin + groupOffset, 0));
            nxtPayTime = nxtPayTime.AddDays (1);

            return nxtPayTime;
        }

        #endregion

        #region stipends

        void StipendInfo ()
        {
            if (!payStipends) {
                MainConsole.Instance.Info ("[Currency]: Stipend payments are not enabled.");
                return;
            }

            TimeSpan nextSched = nextStipendPayment - DateTime.Now;

            MainConsole.Instance.InfoFormat ("[Currency]: The next stipend payment is scheduled for {0}",
                string.Format ("{0:f}", nextStipendPayment));
            MainConsole.Instance.InfoFormat ("            Time to next payment schedule: {0} day{1} {2} hour{3} {4} minute{5}",
                nextSched.Days,
                nextSched.Days == 1 ? "" : "s",
                nextSched.Hours,
                nextSched.Hours == 1 ? "" : "s",
                nextSched.Minutes,
                nextSched.Minutes == 1 ? "" : "s"
            );
            MainConsole.Instance.InfoFormat ("            Stipend : {0} {1}",
                currencySymbol, stipendAmount);
            MainConsole.Instance.InfoFormat ("            Cycle   : {0} {1}{2}",
                stipendInterval, stipendPeriod, stipendInterval == 1 ? "" : "s");
        }

        void ProcessStipendPayments ()
        {
            if (stipendAmount == 0) {
                MainConsole.Instance.Warn ("[Currency]: Stipend payments enabled but amount is not set");
                return;
            }

            MainConsole.Instance.Warn ("[Currency]: Processing of Stipend payments commenced");

            var startTime = DateTime.Now;
            var rightNow = startTime.ToUniversalTime ();
            var userService = m_registry.RequestModuleInterface<IUserAccountService> ();
            var agentInfo = Framework.Utilities.DataManager.RequestPlugin<IAgentInfoConnector> ();
            List<UserAccount> users;
            int payments = 0;
            int payValue = 0;
            bool xfrd;

            users = userService.GetUserAccounts (new List<UUID> { UUID.Zero }, 0, stipendsPremiumOnly ? 600 : 0);
            foreach (UserAccount user in users) {
                if (Utilities.IsSystemUser (user.PrincipalID))
                    continue;

                if (!stipendsPremiumOnly && stipendsLoginRequired) {
                    bool status;
                    UserInfo usrInfo = agentInfo.Get (user.PrincipalID.ToString (), true, out status);
                    if (usrInfo == null)
                        continue;

                    DateTime loginWin = usrInfo.LastLogin.AddSeconds (Constants.STIPEND_RECENT_LOGIN_PERIOD);
                    if (rightNow > loginWin)
                        continue;
                }

                // pay them...
                xfrd = moneyModule.Transfer (
                    user.PrincipalID,
                    (UUID)Constants.BankerUUID,
                    stipendAmount,
                    stipendMessage,
                    TransactionType.StipendPayment
                );

                // keep track
                if (xfrd) {
                    MainConsole.Instance.InfoFormat ("[Currency]: Stipend Payment of {0}{1} for {2} processed.",
                        currencySymbol, stipendAmount, user.Name);
                    payments++;
                    payValue += stipendAmount;
                }
            }

            // 20150730 - greythane - need to fiddle 'the books' as -ve balances are not currently available
            // ignore for now
            /*

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
            */

            var elapsed = DateTime.Now - startTime;
            MainConsole.Instance.InfoFormat ("[Currency]: Processed {0} user stipend payments for {1}{2} in {3} secs",
                payments, currencySymbol, payValue, (int)elapsed.TotalSeconds);

            // reset for the next payment
            nextStipendPayment = GetStipendPaytime (0);
            MainConsole.Instance.InfoFormat ("[Currency]: The next stipend payment is scheduled for {0}",
                string.Format ("{0:f}", nextStipendPayment));

        }

        #endregion

        #region scheduled charges

        void ScheduledPaymentsInfo ()
        {

            var userService = m_registry.RequestModuleInterface<IUserAccountService> ();

            int payments = 0;
            int payValue = 0;

            string paymentInfo;

            paymentInfo = string.Format ("{0, -20}", "User");
            // paymentInfo += Strstringing.Format ("{0, -34}", "Description");
            paymentInfo += string.Format ("{0, -30}", "Transaction");
            paymentInfo += string.Format ("{0, -10}", "Amount");
            paymentInfo += string.Format ("{0, -10}", "Scheduled");

            MainConsole.Instance.CleanInfo (paymentInfo);

            MainConsole.Instance.CleanInfo (
                "----------------------------------------------------------------------------------------------------");

            List<SchedulerItem> CurrentSchedule = sched_database.ToRun (nextScheduledPayment);
            foreach (SchedulerItem I in CurrentSchedule) {

                OSDMap itemInfo = (OSDMap)OSDParser.DeserializeJson (I.FireParams);
                UUID agentID = itemInfo ["AgentID"];
                //string scdID = itemInfo ["SchedulerID"];
                //string description = itemInfo ["Text"];
                int amount = itemInfo ["Amount"];
                DateTime chargeTime = itemInfo ["StartTime"];
                TransactionType transType = !itemInfo.ContainsKey ("Type") ? TransactionType.SystemGenerated : (TransactionType)itemInfo ["Type"].AsInteger ();

                var user = userService.GetUserAccount (null, agentID);

                paymentInfo = string.Format ("{0, -20}", user.Name);
                // paymentInfo += string.Format ("{0, -34}", description.Substring (0, 32));   
                paymentInfo += string.Format ("{0, -30}", Utilities.TransactionTypeInfo (transType));
                paymentInfo += string.Format ("{0, -10}", amount);
                paymentInfo += string.Format ("{0:f}", chargeTime);

                MainConsole.Instance.CleanInfo (paymentInfo);
                payments++;
                payValue += amount;
            }

            MainConsole.Instance.CleanInfo ("");

            TimeSpan nextSched = nextScheduledPayment - DateTime.Now;

            MainConsole.Instance.InfoFormat ("[Currency]: The next payment check is scheduled for {0}",
                string.Format ("{0:f}", nextScheduledPayment));
            MainConsole.Instance.InfoFormat ("            Time to next payment schedule: {0} day{1} {2} hour{3} {4} minute{5}",
                nextSched.Days,
                nextSched.Days == 1 ? "" : "s",
                nextSched.Hours,
                nextSched.Hours == 1 ? "" : "s",
                nextSched.Minutes,
                nextSched.Minutes == 1 ? "" : "s"
            );
            // MainConsole.Instance.InfoFormat ("             Cycle  : {0} {1}{2}",
            //    stipendInterval, stipendPeriod, stipendInterval == 1 ? "" : "s");
            MainConsole.Instance.InfoFormat ("          Payments  : {0}", payments);
            MainConsole.Instance.InfoFormat ("              Fees  : {0}{1}", currencySymbol, payValue);
        }

        void ProccessScheduledPayments ()
        {
            if (showSchedulerTick)
                MainConsole.Instance.Warn ("[Currency]: Processing of Scheduled payments commenced");

            var startScheduled = DateTime.Now;
            var schPayments = 0;


            List<SchedulerItem> CurrentSchedule = sched_database.ToRun (nextScheduledPayment);
            foreach (SchedulerItem I in CurrentSchedule) {
                FireScheduleEvent (I, nextScheduledPayment);
                schPayments++;
            }

            if (schPayments > 0) {
                var elapsed = DateTime.Now - startScheduled;
                MainConsole.Instance.InfoFormat ("[Currency]: {0} Scheduled payments: processing completed in {1}",
                    schPayments, (int)elapsed.TotalSeconds);
            } else
                if (showSchedulerTick)
                MainConsole.Instance.Info ("[Currency]: No scheduled payments at this time.");

            // reset in case this is a manual 'paynow'
            //nextScheduledPayment = GetStipendPaytime (Constants.SCHEDULED_PAYMENTS_DELAY);  
            //MainConsole.Instance.InfoFormat ("[Currency]: The next scheduled payment cycle is scheduled for {0}",
            //    String.Format("{0:f}",nextScheduledPayment));

            nextScheduledPayment = DateTime.Now.AddSeconds (Constants.SCHEDULER_INTERVAL);

        }

        void FireScheduleEvent (SchedulerItem I, DateTime nextPayTime)
        {
            if (I.FireFunction.StartsWith ("ScheduledPayment", StringComparison.Ordinal)) {
                try {
                    // save changes before it fires in case its changed during the fire
                    I = sched_database.SaveHistory (I);

                    if (I.RunOnce)
                        I.Enabled = false;

                    if (I.Enabled)
                        I.TimeToRun = nextPayTime;      // next stipend payment cycle + delay

                    if (!I.HistoryKeep)
                        sched_database.HistoryDeleteOld (I);

                    // save the new schedule item
                    sched_database.SchedulerSave (I);

                    // now fire
                    List<object> reciept = eventManager.FireGenericEventHandler ("ScheduledPayment", I.FireParams);
                    if (!I.HistoryReceipt)
                        I = sched_database.SaveHistoryComplete (I);
                    else {
                        foreach (string results in reciept.Cast<string> ().Where (results => results != "")) {
                            sched_database.SaveHistoryCompleteReciept (I.HistoryLastID, results);
                        }
                    }
                } catch (Exception e) {
                    MainConsole.Instance.ErrorFormat ("[Scheduler]: FireEvent Error {0}: {1}", I.id, e);
                }
            }

        }

        #endregion

        #region group payments

        void GroupPaymentsInfo ()
        {
            if (!payGroups) {
                MainConsole.Instance.Info ("[Currency]: Group payments are not enabled.");
                return;
            }

            var groupsModule = m_registry.RequestModuleInterface<IGroupsServiceConnector> ();
            var dir_service = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();

            List<UUID> groups = null;
            int dirFees = 0;
            int liableGroups = 0;

            if (groupsModule != null)
                groups = groupsModule.GetAllGroups ((UUID)Constants.BankerUUID);

            if (groups != null) {

                // check each group
                foreach (UUID groupID in groups) {
                    var grpParcels = dir_service.GetParcelByOwner (groupID);
                    foreach (var parcel in grpParcels) {
                        if (parcel.LandData.SalePrice > 0) {
                            dirFees += directoryFee;
                            liableGroups++;
                        }
                    }
                }
            }

            TimeSpan nextSched = nextGroupPayment - DateTime.Now;

            MainConsole.Instance.InfoFormat ("[Currency]: The next Group dividend is scheduled for {0}",
                string.Format ("{0:f}", nextGroupDividend));
            MainConsole.Instance.InfoFormat ("[Currency]: The next Group payment cycle is scheduled for {0}",
                        string.Format ("{0:f}", nextGroupPayment));
            MainConsole.Instance.InfoFormat ("            Time to next payment schedule: {0} day{1} {2} hour{3} {4} minute{5}",
                nextSched.Days,
                nextSched.Days == 1 ? "" : "s",
                nextSched.Hours,
                nextSched.Hours == 1 ? "" : "s",
                nextSched.Minutes,
                nextSched.Minutes == 1 ? "" : "s"
            );
            MainConsole.Instance.InfoFormat ("            Cycle   : {0} {1}{2}",
                stipendInterval, stipendPeriod, stipendInterval == 1 ? "" : "s");
            MainConsole.Instance.InfoFormat ("            Groups  : {0}", liableGroups);
            MainConsole.Instance.InfoFormat ("     Directory fee  : {0}{1}", currencySymbol, directoryFee);
            MainConsole.Instance.InfoFormat ("      Fees payable  : {0}{1}", currencySymbol, dirFees);


        }

        void ProcessGroupLiability ()
        {
            if (!payGroups || directoryFee == 0)
                return;

            List<UUID> groups = null;
            int grpMembersLiable = 0;
            int liablePayments = 0;

            var startGroups = DateTime.Now;
            MainConsole.Instance.Warn ("[Currency]: Processing of Group liabilities commenced");

            // - Check if the group has parcels
            // - Are the parcels in Search ?
            // - Yes, make a GroupLiability Task for each of the Parcels (Amount comes out of INI -> PriceDirectoryFee)
            // - Check if there's money in the Group
            // - If there's money already there, substract the money by calling the Tasks
            // - If there's no money, check which users have the "Accountability" role task and pull money from their accounts
            // into the group so the payments can be done

            var groupsModule = m_registry.RequestModuleInterface<IGroupsServiceConnector> ();
            var dir_service = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
            var userService = m_registry.RequestModuleInterface<IUserAccountService> ();

            if (groupsModule != null)
                groups = groupsModule.GetAllGroups ((UUID)Constants.BankerUUID);

            if (groups != null) {
                // check each group
                GroupBalance grpBalance;
                int searchFee = 0;
                bool xfrd;

                foreach (UUID groupID in groups) {

                    var groupRec = groupsModule.GetGroupRecord ((UUID)Constants.BankerUUID, groupID, null);
                    var groupName = groupRec.GroupName;

                    var grpParcels = dir_service.GetParcelByOwner (groupID);
                    foreach (var parcel in grpParcels) {
                        if (parcel.LandData.SalePrice > 0)
                            searchFee += directoryFee;
                    }

                    grpBalance = moneyModule.GetGroupBalance (groupID);
                    // This does not appear to be set anywhere else so use it as the total group liability for land sales
                    grpBalance.ParcelDirectoryFee = searchFee;

                    //TODO: Add a groupTranfer() process to provide for actually saving group monies !!
                    // moneyModule.UpdateGroupBalance(groupID, grpBalance);

                    // a bit of optimisation - no need to continue if there are no fees to be paid
                    if (searchFee == 0)
                        continue;

                    // find how many members are accountable for fees
                    var grpMembers = groupsModule.GetGroupMembers ((UUID)Constants.BankerUUID, groupID);
                    List<UUID> payMembers = new List<UUID> ();
                    foreach (var member in grpMembers) {
                        if (Utilities.IsSystemUser (member.AgentID))
                            continue;

                        // Is member accountable for fees?
                        if (((GroupPowers)member.AgentPowers & GroupPowers.Accountable) == GroupPowers.Accountable)
                            payMembers.Add (member.AgentID);
                    }
                    if (payMembers.Count == 0)      // no one to pay??
                        continue;

                    int memberShare = grpBalance.ParcelDirectoryFee / payMembers.Count;         // this should be integer division so truncated (5 /4 = 1)
                    if (memberShare == 0)                                                       // share of fee < 1 per user
                        memberShare = 1;

                    foreach (var memberID in payMembers) {
                        // check user balance
                        var userBalance = moneyModule.Balance (memberID);
                        if (userBalance <= 0)
                            continue;                                                           // let them off the hook (for now - could still charge and go credit balance)

                        // pay the man...
                        xfrd = moneyModule.Transfer (
                            (UUID)Constants.BankerUUID,
                            memberID,
                            memberShare,
                            "Group directory fee share payment",
                            TransactionType.SystemGenerated
                        );

                        // keep track
                        if (xfrd) {
                            var user = userService.GetUserAccount (null, memberID);
                            MainConsole.Instance.InfoFormat ("[Currency]: Directory fee payment for {0} of {1}{2} from {3} processed.",
                                groupName, currencySymbol, memberShare, user.Name);

                            grpMembersLiable++;
                            liablePayments += directoryFee;
                        }
                    }
                }
            }

            // reset for the next payment
            nextGroupPayment = GetGroupPaytime (0);

            MainConsole.Instance.InfoFormat ("[Currency]: Processed {0} group liability payments for {1}{2}",
                grpMembersLiable, currencySymbol, liablePayments);

            var elapsed = DateTime.Now - startGroups;
            MainConsole.Instance.InfoFormat ("[Currency]: Group processing completed in {0} secs", (int)elapsed.TotalSeconds);
            MainConsole.Instance.InfoFormat ("[Currency]: The next Group payment is scheduled for {0}",
                string.Format ("{0:f}", nextGroupPayment));

            return;

        }

        void ProcessGroupDividends ()
        {
            if (!payGroups)
                return;

            List<UUID> groups = null;
            int grpsPayments = 0;
            int grpDividends = 0;
            var startGroups = DateTime.Now;
            MainConsole.Instance.Warn ("[Currency]: Processing of Group dividends commenced");

            // * Group Disbursments
            // - If there's money in the group after any payments (parcel directory fees)it will have to be
            //    divided by the people that have the "Accountability" role task
            // - Check how many users there are with that role task
            // - Divide the amount of money by the amount of users (make sure the amount is a whole number)
            // - Create Task for each user to be payed
            // - If there is a remaining amount of money, leave it in the group balance for the next week

            var groupsModule = m_registry.RequestModuleInterface<IGroupsServiceConnector> ();
            var userService = m_registry.RequestModuleInterface<IUserAccountService> ();

            if (groupsModule != null)
                groups = groupsModule.GetAllGroups ((UUID)Constants.BankerUUID);

            if (groups != null) {

                // check each group
                GroupBalance grpBalance;
                bool xfrd;

                foreach (UUID groupID in groups) {
                    var groupRec = groupsModule.GetGroupRecord ((UUID)Constants.BankerUUID, groupID, null);
                    var groupName = groupRec.GroupName;

                    grpBalance = moneyModule.GetGroupBalance (groupID);
                    if (grpBalance.Balance <= 0)
                        continue;

                    // find how many members are accountable for fees and pay them dividends
                    var grpMembers = groupsModule.GetGroupMembers ((UUID)Constants.BankerUUID, groupID);
                    List<UUID> payMembers = new List<UUID> ();
                    foreach (var member in grpMembers) {
                        if (Utilities.IsSystemUser (member.AgentID))
                            continue;

                        // Is member accountable for fees?
                        if (((GroupPowers)member.AgentPowers & GroupPowers.Accountable) == GroupPowers.Accountable)
                            payMembers.Add (member.AgentID);
                    }
                    if (payMembers.Count == 0)      // no one to pay??
                        continue;

                    int dividend = grpBalance.Balance / payMembers.Count;    // this should be integer division so truncated (5 /4 = 1)
                    if (dividend == 0)                                                  // insufficient funds < 1 per user
                        continue;

                    foreach (var memberID in payMembers) {

                        // pay them...
                        xfrd = moneyModule.Transfer (
                            memberID,
                            (UUID)Constants.BankerUUID,
                            dividend,
                            "Group dividend",
                            TransactionType.SystemGenerated
                        );


                        // keep track
                        if (xfrd) {
                            var user = userService.GetUserAccount (null, memberID);
                            MainConsole.Instance.InfoFormat ("[Currency]: Dividend payment from {0} of {1}{2} from {3} processed.",
                                groupName, currencySymbol, dividend, user.Name);

                            grpsPayments++;
                            grpDividends += dividend;
                        }
                    }
                }
            }

            // reset for the next payment
            nextGroupDividend = GetGroupPaytime (Constants.GROUP_DISBURSMENTS_DELAY);

            MainConsole.Instance.InfoFormat ("[Currency]: Processed {0} group dividend payments for {1}{2}",
                grpsPayments, currencySymbol, grpDividends);

            var elapsed = DateTime.Now - startGroups;
            MainConsole.Instance.InfoFormat ("[Currency]: Group processing completed in {0} secs", (int)elapsed.TotalSeconds);
            MainConsole.Instance.InfoFormat ("[Currency]: The next Group disbursment is scheduled for {0}",
                string.Format ("{0:f}", nextGroupDividend));

            return;
        }

        #endregion

        #region console commands
        void SetSchedTimer (int seconds)
        {
            taskTimer.Enabled = false;
            taskTimer.Interval = seconds * 1000;
            taskTimer.Enabled = true;
        }

        protected void HandleStipendEnable (IScene scene, string [] cmd)
        {
            if (payStipends) {
                MainConsole.Instance.Info ("[Currency]: Stipend payments are already enabled");
                return;
            }

            getStipendConfig ();

            var okConfig = payStipends;
            bool promptUser = false;

            if (!okConfig) {
                var pu = MainConsole.Instance.Prompt (
                    "The Stipend configuration may be invalid. Do you wish to verify details? (yes, no)", "no").ToLower ();
                if (pu.StartsWith ("y", StringComparison.Ordinal))
                    promptUser = true;
                else
                    return;
            } else {
                var pmtu = MainConsole.Instance.Prompt ("Do you wish to revise the configuration? (yes, no)", "no").ToLower ();
                promptUser = pmtu.StartsWith ("y", StringComparison.Ordinal);
            }

            if (promptUser) {
                MainConsole.Instance.CleanInfo ("");
                MainConsole.Instance.CleanInfo ("Note: These settings are valid only for the current session.\n" +
                    "Please edit your Economy.ini file to make these permanent");
                MainConsole.Instance.CleanInfo ("");

                // prompt for details...");
                int amnt;
                int.TryParse (MainConsole.Instance.Prompt ("Stipend amount ?", stipendAmount.ToString ()), out amnt);
                stipendAmount = amnt;
                if (stipendAmount <= 0) {
                    payStipends = false;
                    return;
                }

                // get a time period then
                var respPeriod = new List<string> ();
                respPeriod.Add ("day");
                respPeriod.Add ("week");
                respPeriod.Add ("month");
                respPeriod.Add ("year");
                respPeriod.Add ("none");

                stipendPeriod = MainConsole.Instance.Prompt ("Time period between payments?", stipendPeriod, respPeriod).ToLower ();
                if (stipendPeriod.StartsWith ("n", StringComparison.Ordinal))
                    return;

                if (!stipendPeriod.StartsWith ("d", StringComparison.Ordinal)) {
                    var respDay = new List<string> ();
                    respDay.Add ("sunday");
                    respDay.Add ("monday");
                    respDay.Add ("tuesday");
                    respDay.Add ("wednesday");
                    respDay.Add ("thursday");
                    respDay.Add ("friday");
                    respDay.Add ("saturday");

                    MainConsole.Instance.Info ("Day of the week for payments can be : sun, mon, tue, wed, thu, fri, sat");
                    MainConsole.Instance.Info ("For non weekly periods, payments will be the first day of the selected period");

                    var pday = MainConsole.Instance.Prompt ("Pay day?", stipendPayDay).ToLower ();
                    stipendPayDay = respDay [PayDayOfWeek (pday)];
                }

                int intvl;
                int.TryParse (MainConsole.Instance.Prompt (
                    "Number of time periods between payments? (1 > Every period 2 > every two periods etc.)", stipendInterval.ToString ()), out intvl);
                stipendInterval = intvl;
                if (stipendInterval <= 0) {
                    payStipends = false;
                    return;
                }

                stipendPayTime = MainConsole.Instance.Prompt ("Payment time? (hh:mm)", stipendPayTime);

                stipendsPremiumOnly = MainConsole.Instance.Prompt ("Pay premium users only? (yes/no)", (stipendsPremiumOnly ? "yes" : "no")).ToLower () == "yes";
                if (!stipendsPremiumOnly)
                    stipendsLoginRequired = MainConsole.Instance.Prompt ("Require a recent login for Free members? (yes/no)",
                        (stipendsLoginRequired ? "yesy" : "no")).ToLower () == "yes";
                // not sure about this one??  //StipendsLoadOldUsers = currCfg.GetBoolean ("StipendsLoadOldUsers", false);

            }

            // ensure we are enabled
            MainConsole.Instance.InfoFormat ("[Currency]; Enabling stipend payment of {0}{1}", currencySymbol, stipendAmount);

            payStipends = true;
            InitializeScheduleTimer ();

        }

        protected void HandleStipendDisable (IScene scene, string [] cmd)
        {
            if (!payStipends) {
                MainConsole.Instance.Info ("[Currency]: Stipend payments are already disabled");
                return;
            }

            payStipends = false;
            MainConsole.Instance.Info ("[Currency]: Stipend payments have been disabled");
        }

        protected void HandleStipendInfo (IScene scene, string [] cmd)
        {
            StipendInfo ();
        }

        protected void HandleStipendPayNow (IScene scene, string [] cmd)
        {
            if (!payStipends) {
                MainConsole.Instance.Info ("[Currency]: Please enable Stipend payments first!");
                return;
            }

            nextStipendPayment = DateTime.Now;
            SetSchedTimer (10);
            MainConsole.Instance.InfoFormat ("[Currency]: Stipend payments will commence in {0} seconds.", 10);

        }


        protected void HandleStipendReset (IScene scene, string [] cmd)
        {
            getStipendConfig ();
            InitializeScheduleTimer ();

            MainConsole.Instance.Info ("[Currency]; Stipend configuration reloaded");
            StipendInfo ();

        }


        protected void HandleGrouppayEnable (IScene scene, string [] cmd)
        {
            if (payGroups) {
                MainConsole.Instance.Info ("[Currency]: Group payments are already enabled");
                return;
            }

            // ensure we have schedule details
            getStipendConfig ();

            // ensure we are enabled
            payGroups = true;
            InitializeScheduleTimer ();

            MainConsole.Instance.Info ("[Currency]; Group payments have been enabled");
            MainConsole.Instance.CleanInfoFormat ("          The next group payment cycle is scheduled for {0}",
                  nextGroupPayment.ToLongDateString ());

        }

        protected void HandleGrouppayDisable (IScene scene, string [] cmd)
        {
            if (!payGroups) {
                MainConsole.Instance.Info ("[Currency]: Group payments are already disabled");
                return;
            }

            payGroups = false;
            MainConsole.Instance.Info ("[Currency]: Group payments have been disabled");
        }

        protected void HandleGrouppayInfo (IScene scene, string [] cmd)
        {
            GroupPaymentsInfo ();
        }

        protected void HandleGrouppayPayNow (IScene scene, string [] cmd)
        {
            if (!payGroups) {
                MainConsole.Instance.Info ("[Currency]: Please enable Group payments first!");
                return;
            }

            nextGroupPayment = DateTime.Now;
            SetSchedTimer (10);
            MainConsole.Instance.InfoFormat ("[Currency]: Group payments will commence in {0} seconds.", 10);

        }

        protected void HandleGrouppayPayDividends (IScene scene, string [] cmd)
        {

            nextGroupDividend = DateTime.Now;
            SetSchedTimer (10);
            MainConsole.Instance.InfoFormat ("[Currency]: Group dividend payments will commence in {0} seconds.", 10);

        }

        protected void HandleScheduledPayInfo (IScene scene, string [] cmd)
        {
            ScheduledPaymentsInfo ();
        }

        protected void HandleScheduledPayNow (IScene scene, string [] cmd)
        {

            nextScheduledPayment = DateTime.Now;
            SetSchedTimer (10);
            MainConsole.Instance.InfoFormat ("[Currency]: Scheduled payments will commence in {0} seconds.", 10);

        }

        protected void HandleShowSchedulerTick (IScene scene, string [] cmd)
        {
            var activity = MainConsole.Instance.Prompt ("Enable scheduler activity tracking? (y/n)", showSchedulerTick ? "yes" : "no");
            showSchedulerTick = activity.ToLower ().StartsWith ("y", StringComparison.Ordinal);

            MainConsole.Instance.Info ("[Scheduler]: Activity tracking " + (showSchedulerTick ? "enabled" : "disabled"));

        }
        #endregion
    }
}
