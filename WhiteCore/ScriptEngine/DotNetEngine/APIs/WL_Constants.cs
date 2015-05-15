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

using vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;

namespace WhiteCore.ScriptEngine.DotNetEngine.Runtime
{
    public partial class ScriptBaseClass
    {
        // Windlight Settings
        public const int WL_OK = -1;
        public const int WL_ERROR = -2;
        public const int WL_ERROR_NO_SCENE_SET = -3;
        public const int WL_ERROR_SCENE_MUST_BE_STATIC = -4;
        public const int WL_ERROR_SCENE_MUST_NOT_BE_STATIC = -5;
        public const int WL_ERROR_BAD_SETTING = -6;
        public const int WL_ERROR_NO_PRESET_FOUND = -7;

        // Windlight Sky Settings
        public const int WL_AMBIENT = 0;
        public const int WL_SKY_BLUE_DENSITY = 1;
        public const int WL_SKY_BLUR_HORIZON = 2;
        public const int WL_CLOUD_COLOR = 3;
        public const int WL_CLOUD_POS_DENSITY1 = 4;
        public const int WL_CLOUD_POS_DENSITY2 = 5;
        public const int WL_CLOUD_SCALE = 6;
        public const int WL_CLOUD_SCROLL_X = 7;
        public const int WL_CLOUD_SCROLL_Y = 8;
        public const int WL_CLOUD_SCROLL_X_LOCK = 9;
        public const int WL_CLOUD_SCROLL_Y_LOCK = 10;
        public const int WL_CLOUD_SHADOW = 11;
        public const int WL_SKY_DENSITY_MULTIPLIER = 12;
        public const int WL_SKY_DISTANCE_MULTIPLIER = 13;
        public const int WL_SKY_GAMMA = 14;
        public const int WL_SKY_GLOW = 15;
        public const int WL_SKY_HAZE_DENSITY = 16;
        public const int WL_SKY_HAZE_HORIZON = 17;
        public const int WL_SKY_LIGHT_NORMALS = 18;
        public const int WL_SKY_MAX_ALTITUDE = 19;
        public const int WL_SKY_STAR_BRIGHTNESS = 20;
        public const int WL_SKY_SUNLIGHT_COLOR = 21;

        // Windlight Water Settings
        public const int WL_WATER_BLUR_MULTIPLIER = 22;
        public const int WL_WATER_FRESNEL_OFFSET = 23;
        public const int WL_WATER_FRESNEL_SCALE = 24;
        public const int WL_WATER_NORMAL_MAP = 25;
        public const int WL_WATER_NORMAL_SCALE = 26;
        public const int WL_WATER_SCALE_ABOVE = 27;
        public const int WL_WATER_SCALE_BELOW = 28;
        public const int WL_WATER_UNDERWATER_FOG_MODIFIER = 29;
        public const int WL_WATER_FOG_COLOR = 30;
        public const int WL_WATER_FOG_DENSITY = 31;
        public const int WL_WATER_BIG_WAVE_DIRECTION = 32;
        public const int WL_WATER_LITTLE_WAVE_DIRECTION = 33;
    }
}