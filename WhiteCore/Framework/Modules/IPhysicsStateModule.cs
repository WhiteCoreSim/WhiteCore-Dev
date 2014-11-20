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

namespace WhiteCore.Framework.Modules
{
    public interface IPhysicsStateModule
    {
        /// <summary>
        ///     Saves a state for all active objects in the region so that it can be reloaded later
        /// </summary>
        void SavePhysicsState();

        /// <summary>
        ///     Reset the physics scene to the last saved physics state (with SavePhysicsState())
        /// </summary>
        void ResetToLastSavedState();

        /// <summary>
        ///     Start saving the states that will be reverted with StartPhysicsTimeReversal()
        /// </summary>
        void StartSavingPhysicsTimeReversalStates();

        /// <summary>
        ///     Stop saving the states that will be reverted with StartPhysicsTimeReversal()
        /// </summary>
        void StopSavingPhysicsTimeReversalStates();

        /// <summary>
        ///     Begin reverting prim velocities and positions backwards in time to what they were previously
        ///     Must have StartSavingPhysicsTimeReversalStates() called before it so that it reads the states
        /// </summary>
        void StartPhysicsTimeReversal();

        /// <summary>
        ///     Stop reverting prim velocities and positions backwards in time and let things happen forwards again
        /// </summary>
        void StopPhysicsTimeReversal();
    }
}