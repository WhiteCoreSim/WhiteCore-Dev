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

namespace WhiteCore.ScriptEngine.DotNetEngine.MiniModule
{
    public interface IHeightmap
    {
        /// <summary>
        ///     Returns [or sets] the heightmap value at specified coordinates.
        /// </summary>
        /// <param name="x">X Coordinate</param>
        /// <param name="y">Y Coordinate</param>
        /// <returns>A value in meters representing height. Can be negative. Value correlates with Z parameter in world coordinates</returns>
        /// <example>
        ///     double heightVal = World.Heightmap[128,128];
        ///     World.Heightmap[128,128] *= 5.0;
        ///     World.Heightmap[128,128] = 25;
        /// </example>
        float this[int x, int y] { get; set; }

        /// <summary>
        ///     The maximum length of the region (Y axis), exclusive. (eg Height = 256, max Y = 255). Minimum is always 0 inclusive.
        /// </summary>
        /// <example>
        ///     Host.Console.Info("The terrain length of this region is " + World.Heightmap.Length);
        /// </example>
        int Length { get; }

        /// <summary>
        ///     The maximum width of the region (X axis), exclusive. (eg Width = 256, max X = 255). Minimum is always 0 inclusive.
        /// </summary>
        /// <example>
        ///     Host.Console.Info("The terrain width of this region is " + World.Heightmap.Width);
        /// </example>
        int Width { get; }
    }
}
