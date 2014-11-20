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

using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WhiteCore.Modules.Avatar.Groups
{
    public class GroupMoneyModule : INonSharedRegionModule
    {
        private bool m_enabled = false;

        public string Name
        {
            get { return "GroupMoneyModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["GroupMoney"];
            if (config != null)
                m_enabled = config.GetBoolean("Enabled", m_enabled);
        }

        public void AddRegion(IScene scene)
        {
        }

        public void RegionLoaded(IScene scene)
        {
            scene.EventManager.OnNewClient += new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
            scene.EventManager.OnClosingClient += new EventManager.OnNewClientDelegate(EventManager_OnClosingClient);
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void Close()
        {
        }

        private void EventManager_OnClosingClient(IClientAPI client)
        {
            client.OnGroupAccountSummaryRequest -= new GroupAccountSummaryRequest(client_OnGroupAccountSummaryRequest);
            client.OnGroupAccountTransactionsRequest -=
                new GroupAccountTransactionsRequest(client_OnGroupAccountTransactionsRequest);
            client.OnGroupAccountDetailsRequest -= new GroupAccountDetailsRequest(client_OnGroupAccountDetailsRequest);
        }

        private void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnGroupAccountSummaryRequest += new GroupAccountSummaryRequest(client_OnGroupAccountSummaryRequest);
            client.OnGroupAccountTransactionsRequest +=
                new GroupAccountTransactionsRequest(client_OnGroupAccountTransactionsRequest);
            client.OnGroupAccountDetailsRequest += new GroupAccountDetailsRequest(client_OnGroupAccountDetailsRequest);
        }

        /// <summary>
        ///     Sends the details about what
        /// </summary>
        /// <param name="client"></param>
        /// <param name="agentID"></param>
        /// <param name="groupID"></param>
        /// <param name="transactionID"></param>
        /// <param name="sessionID"></param>
        /// <param name="currentInterval"></param>
        /// <param name="intervalDays"></param>
        private void client_OnGroupAccountDetailsRequest(IClientAPI client, UUID agentID, UUID groupID,
                                                         UUID transactionID, UUID sessionID, int currentInterval,
                                                         int intervalDays)
        {
            IGroupsModule groupsModule = client.Scene.RequestModuleInterface<IGroupsModule>();
            if (groupsModule != null && groupsModule.GroupPermissionCheck(agentID, groupID, GroupPowers.Accountable))
            {
                IMoneyModule moneyModule = client.Scene.RequestModuleInterface<IMoneyModule>();
                if (moneyModule != null)
                {
                    int amt = moneyModule.Balance(groupID);
                    List<GroupAccountHistory> history = moneyModule.GetTransactions(groupID, agentID, currentInterval,
                                                                                    intervalDays);
                    history = (from h in history where h.Stipend select h).ToList();
                        //We don't want payments, we only want stipends which we sent to users
                    GroupBalance balance = moneyModule.GetGroupBalance(groupID);
                    client.SendGroupAccountingDetails(client, groupID, transactionID, sessionID, amt, currentInterval,
                                                      intervalDays,
                                                      Util.BuildYMDDateString(
                                                          balance.StartingDate.AddDays(-currentInterval*intervalDays)),
                                                      history.ToArray());
                }
                else
                    client.SendGroupAccountingDetails(client, groupID, transactionID, sessionID, 0, currentInterval,
                                                      intervalDays,
                                                      "Never", new GroupAccountHistory[0]);
            }
        }

        /// <summary>
        ///     Sends the transactions that the group has done over the given time period
        /// </summary>
        /// <param name="client"></param>
        /// <param name="agentID"></param>
        /// <param name="groupID"></param>
        /// <param name="transactionID"></param>
        /// <param name="sessionID"></param>
        /// <param name="currentInterval"></param>
        /// <param name="intervalDays"></param>
        private void client_OnGroupAccountTransactionsRequest(IClientAPI client, UUID agentID, UUID groupID,
                                                              UUID transactionID, UUID sessionID, int currentInterval,
                                                              int intervalDays)
        {
            IGroupsModule groupsModule = client.Scene.RequestModuleInterface<IGroupsModule>();
            if (groupsModule != null && groupsModule.GroupPermissionCheck(agentID, groupID, GroupPowers.Accountable))
            {
                IMoneyModule moneyModule = client.Scene.RequestModuleInterface<IMoneyModule>();
                if (moneyModule != null)
                {
                    List<GroupAccountHistory> history = moneyModule.GetTransactions(groupID, agentID, currentInterval,
                                                                                    intervalDays);
                    history = (from h in history where h.Payment select h).ToList();
                        //We want payments for things only, not stipends
                    GroupBalance balance = moneyModule.GetGroupBalance(groupID);
                    client.SendGroupTransactionsSummaryDetails(client, groupID, transactionID, sessionID,
                                                               currentInterval, intervalDays,
                                                               Util.BuildYMDDateString(
                                                                   balance.StartingDate.AddDays(-currentInterval*
                                                                                                intervalDays)),
                                                               history.ToArray());
                }
                else
                    client.SendGroupTransactionsSummaryDetails(client, groupID, transactionID, sessionID,
                                                               currentInterval, intervalDays,
                                                               "Never", new GroupAccountHistory[0]);
            }
        }

        private void client_OnGroupAccountSummaryRequest(IClientAPI client, UUID agentID, UUID groupID, UUID requestID,
                                                         int currentInterval, int intervalDays)
        {
            IGroupsModule groupsModule = client.Scene.RequestModuleInterface<IGroupsModule>();
            if (groupsModule != null && groupsModule.GroupPermissionCheck(agentID, groupID, GroupPowers.Accountable))
            {
                IMoneyModule moneyModule = client.Scene.RequestModuleInterface<IMoneyModule>();
                if (moneyModule != null)
                {
                    int amt = moneyModule.Balance(groupID);
                    GroupBalance balance = moneyModule.GetGroupBalance(groupID);
                    client.SendGroupAccountingSummary(client, groupID, requestID, amt, balance.TotalTierDebit,
                                                      balance.TotalTierCredits,
                                                      Util.BuildYMDDateString(
                                                          balance.StartingDate.AddDays(-currentInterval*intervalDays)),
                                                      currentInterval, intervalDays,
                                                      Util.BuildYMDDateString(balance.StartingDate.AddDays(intervalDays)),
                                                      Util.BuildYMDDateString(
                                                          balance.StartingDate.AddDays(-(currentInterval + 1)*
                                                                                       intervalDays)),
                                                      balance.ParcelDirectoryFee, balance.LandFee, balance.GroupFee,
                                                      balance.ObjectFee);
                }
                else
                    client.SendGroupAccountingSummary(client, groupID, requestID, 0, 0, 0, "Never",
                                                      currentInterval, intervalDays, "Never",
                                                      "Never", 0, 0, 0, 0);
            }
        }
    }
}