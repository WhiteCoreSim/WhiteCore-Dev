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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Currency
{
    public class ScheduledPayments : IService, IScheduledMoneyModule
    {
        #region Declares

        public IRegistryCore m_registry;

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            IMoneyModule moneyModule = m_registry.RequestModuleInterface<IMoneyModule>();
            if (moneyModule != null) //Only register if money is enabled
            {
                m_registry.RegisterModuleInterface<IScheduledMoneyModule>(this);
                m_registry.RequestModuleInterface<ISimulationBase>()
                          .EventManager.RegisterEventHandler("ScheduledPayment", ChargeNext);
            }
        }

        #endregion

        #region IScheduledMoneyModule Members

        public event UserDidNotPay OnUserDidNotPay;
        public event CheckWhetherUserShouldPay OnCheckWhetherUserShouldPay;

        public bool Charge(UUID agentID, int amount, string text, int daysUntilNextCharge, TransactionType type, string identifer, bool chargeImmediately)
        {
            IMoneyModule moneyModule = m_registry.RequestModuleInterface<IMoneyModule>();
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
    }
}
