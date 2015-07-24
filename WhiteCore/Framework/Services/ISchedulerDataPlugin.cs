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
using WhiteCore.Framework.Services.ClassHelpers.Other;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Framework.Services
{
    public interface ISchedulerDataPlugin : IWhiteCoreDataPlugin
    {
        /// <summary>
        /// Save a Scheduler item.
        /// </summary>
        /// <returns>The ID of the item.</returns>
        /// <param name="I">Schedule item</param>
        string SchedulerSave(SchedulerItem I);

        /// <summary>
        /// Remove a scheduler item by id.
        /// </summary>
        /// <param name="id">Identifier.</param>
        void SchedulerRemoveID(string id);

        /// <summary>
        /// Remove a scheduler item specifying the function identifier.
        /// </summary>
        /// <param name="identifier">Identifier.</param>
        void SchedulerRemoveFunction(string identifier);

        /// <summary>
        /// Checks if a scheduler item (id) exists.
        /// </summary>
        /// <returns><c>true</c>, if scheduler id exists, <c>false</c> otherwise.</returns>
        /// <param name="id">Identifier.</param>
        bool SchedulerExist(string id);

        /// <summary>
        /// Retrieves the scheduler items that are <c>= the specified DateTime.
        /// </summary>
        /// <returns>Items <c>= timeBefore</returns>
        /// <param name="timeBefore">DateTime before to check.</param>
        List<SchedulerItem> ToRun(DateTime timeBefore);

        /// <summary>
        /// Saves scheuler itm in history.
        /// </summary>
        /// <returns>The history.</returns>
        /// <param name="I">Scheduler item</param>
        SchedulerItem SaveHistory(SchedulerItem I);

        /// <summary>
        /// Saves the history completed status only.
        /// </summary>
        /// <returns>The history scheulder item.</returns>
        /// <param name="I">I.</param>
        SchedulerItem SaveHistoryComplete(SchedulerItem I);

        /// <summary>
        /// Saves a recipt to the history with completed status.
        /// </summary>
        /// <param name="historyID">History ID.</param>
        /// <param name="reciept">Reciept.</param>
        void SaveHistoryCompleteReciept(string historyID, string reciept);

        /// <summary>
        /// Histories the delete old.
        /// </summary>
        /// <param name="I">I.</param>
        void HistoryDeleteOld(SchedulerItem I);

        /// <summary>
        /// Get the specified scheduler id.
        /// </summary>
        /// <param name="id">Identifier.</param>
        SchedulerItem Get(string id);

        /// <summary>
        /// Get the specified scheduleFor and fireFunction. (Obsolete?)
        /// </summary>
        /// <param name="scheduleFor">Schedule for.</param>
        /// <param name="fireFunction">Fire function.</param>
        SchedulerItem Get(string scheduleFor, string fireFunction);

        /// <summary>
        /// Gets the schedule item corresponding to the supplied function name.
        /// </summary>
        /// <returns>The schedule item.</returns>
        /// <param name="fireFunction">Fire function.</param>
        SchedulerItem GetFunctionItem (string fireFunction);

    }

    public interface IScheduleService
    {

        string Save(SchedulerItem I);

        void RemoveID(string scdID);

        void RemoveFireFunction(string identifier);

        bool Exist(string scdID);

        SchedulerItem Get(string ID);

        SchedulerItem Get(string scheduleFor, string fireFunction);

        SchedulerItem GetFunctionItem (string fireFunction);

    }
}