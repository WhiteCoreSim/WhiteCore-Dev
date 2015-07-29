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
    public class GroupPayments : IService
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
                // Register the GroupPayments Engine
            }
        }
        #endregion

        #region IGroupMoneyModule Members

        // Functions:
        //
        // * Group Liability
        // - Check if the group has parcels
        // - Are the parcels in Search ?
        // - Yes, make a GroupLiability Task for each of the Parcels (Amount comes out of INI -> PriceDirectoryFee)
        // - Check if there's money in the Group
        // - If there's money already there, subtract the money by calling the Tasks
        // - If there's no money, check which users have the "Accountability" role task and pull money from their accounts
        // into the group so the payments can be done

        // * Group Payments
        // - If there's money in the group it will have to be divided by the people that have the "Accountability" role task
        // - Check how many users there are with that role task
        // - Divide the amount of money by the amount of users (make sure the amount is a whole number)
        // - Create Task for each user to be payed
        // - If there is a remaining amount of money, leave it in the group balance for the next week

        #endregion
    }
}
