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

using vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;

namespace WhiteCore.ScriptEngine.DotNetEngine.Runtime
{
    public partial class ScriptBaseClass
    {
        // Constants for osGetRegionStats
        public static readonly LSL_Types.LSLInteger STATS_TIME_DILATION = 0;
        public static readonly LSL_Types.LSLInteger STATS_SIM_FPS = 1;
        public static readonly LSL_Types.LSLInteger STATS_PHYSICS_FPS = 2;
        public static readonly LSL_Types.LSLInteger STATS_AGENT_UPDATES = 3;
        public static readonly LSL_Types.LSLInteger STATS_FRAME_MS = 4;
        public static readonly LSL_Types.LSLInteger STATS_NET_MS = 5;
        public static readonly LSL_Types.LSLInteger STATS_OTHER_MS = 6;
        public static readonly LSL_Types.LSLInteger STATS_PHYSICS_MS = 7;
        public static readonly LSL_Types.LSLInteger STATS_AGENT_MS = 8;
        public static readonly LSL_Types.LSLInteger STATS_IMAGE_MS = 9;
        public static readonly LSL_Types.LSLInteger STATS_SCRIPT_MS = 10;
        public static readonly LSL_Types.LSLInteger STATS_TOTAL_PRIMS = 11;
        public static readonly LSL_Types.LSLInteger STATS_ACTIVE_PRIMS = 12;
        public static readonly LSL_Types.LSLInteger STATS_ROOT_AGENTS = 13;
        public static readonly LSL_Types.LSLInteger STATS_CHILD_AGENTS = 14;
        public static readonly LSL_Types.LSLInteger STATS_ACTIVE_SCRIPTS = 15;
        public static readonly LSL_Types.LSLInteger STATS_SCRIPT_LPS = 16; //Doesn't work
        public static readonly LSL_Types.LSLInteger STATS_SCRIPT_EPS = 31;
        public static readonly LSL_Types.LSLInteger STATS_IN_PACKETS_PER_SECOND = 17;
        public static readonly LSL_Types.LSLInteger STATS_OUT_PACKETS_PER_SECOND = 18;
        public static readonly LSL_Types.LSLInteger STATS_PENDING_DOWNLOADS = 19;
        public static readonly LSL_Types.LSLInteger STATS_PENDING_UPLOADS = 20;
        public static readonly LSL_Types.LSLInteger STATS_UNACKED_BYTES = 24;
        
        // Constants for osNpc* functions
        public static readonly LSL_Types.LSLInteger OS_NPC_FLY = 0;
        public static readonly LSL_Types.LSLInteger OS_NPC_NO_FLY = 1;
        public static readonly LSL_Types.LSLInteger OS_NPC_LAND_AT_TARGET = 2;
        public static readonly LSL_Types.LSLInteger OS_NPC_RUNNING = 4;
        public static readonly LSL_Types.LSLInteger OS_NPC_SIT_NOW = 0;

        public static readonly LSL_Types.LSLInteger OS_NPC_CREATOR_OWNED = 0x1;
        public static readonly LSL_Types.LSLInteger OS_NPC_NOT_OWNED = 0x2;
        public static readonly LSL_Types.LSLInteger OS_NPC_SENSE_AS_AGENT = 0x4;

        /// <summary>
        /// process name parameter as regex
        /// </summary>
        public const int OS_LISTEN_REGEX_NAME = 0x1;

        /// <summary>
        /// process message parameter as regex
        /// </summary>
        public const int OS_LISTEN_REGEX_MESSAGE = 0x2;        
    }
}
