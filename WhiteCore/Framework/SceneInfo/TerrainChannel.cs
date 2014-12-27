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
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Utilities;
using OpenMetaverse;

namespace WhiteCore.Framework.SceneInfo
{
    /// <summary>
    ///     A new version of the old Channel class, simplified
    /// </summary>
    public class TerrainChannel : ITerrainChannel
    {
        private int m_Width;
        private int m_Height;

        /// <summary>
        ///     NOTE: This is NOT a normal map, it has a resolution of 10x
        /// </summary>
        short[] m_map;

        IScene m_scene;
        bool[,] taint;

        public TerrainChannel(IScene scene)
        {
            m_scene = scene;
            m_Width = m_scene.RegionInfo.RegionSizeX;
            m_Height = m_scene.RegionInfo.RegionSizeY;
			CreateDefaultTerrain(m_scene.RegionInfo.RegionTerrain);
        }

		public TerrainChannel(string terrainType, IScene scene)
		{
			m_scene = scene;
			m_Width = Constants.RegionSize;
            m_Height = m_Width;
			if (scene != null)
			{
				m_Width = scene.RegionInfo.RegionSizeX;
                m_Height = scene.RegionInfo.RegionSizeY;
			}

            CreateDefaultTerrain(terrainType);
		}

		public TerrainChannel(short[] import, IScene scene)
        {
            m_scene = scene;
            m_map = import;
            m_Width = Constants.RegionSize;
            m_Height = m_Width;
			if (scene != null)
			{
                if(scene.RegionInfo.RegionSizeX != int.MaxValue &&
                   scene.RegionInfo.RegionSizeY != int.MaxValue)
                {
                    m_Width = scene.RegionInfo.RegionSizeX;
                    m_Height = scene.RegionInfo.RegionSizeY;
                }
            }
            taint = new bool[m_Width/Constants.TerrainPatchSize,m_Height/Constants.TerrainPatchSize];

            if((import.Length != m_Width * m_Height)) {
                //We need to fix the map then
                CreateDefaultTerrain(m_scene.RegionInfo.RegionTerrain);
            }
        }

        public TerrainChannel(bool createMap, IScene scene)
        {
            m_scene = scene;
            m_Width = Constants.RegionSize;
            m_Height = m_Width;
            if (scene != null)
            {
                m_Width = scene.RegionInfo.RegionSizeX;
                m_Height = scene.RegionInfo.RegionSizeY;
            }
            if (createMap)
            {
                m_map = new short[m_Width*m_Height];
                taint = new bool[m_Width/Constants.TerrainPatchSize,m_Height/Constants.TerrainPatchSize];
            }
        }

        public TerrainChannel(int w, int h, IScene scene)
        {
            m_scene = scene;
            m_Width = w;

			// the basic assumption is that regions are square so make sure...
			if (m_scene != null)
			{
    			w = m_scene.RegionInfo.RegionSizeX;
				h = m_scene.RegionInfo.RegionSizeY;
			}
			else
			{
				w = Constants.RegionSize;
				h = Constants.RegionSize;
			}


            m_map = new short[w*h];
            taint = new bool[w/Constants.TerrainPatchSize,h/Constants.TerrainPatchSize];
        }

        #region ITerrainChannel Members

        public int Width
        {
            get { return m_Width; }
        }

        public int Height
        {
            get { return m_Height; }
        }

        public IScene Scene
        {
            get { return m_scene; }
            set { m_scene = value; }
        }

        public short[] GetSerialised()
        {
            return m_map;
        }

        public float this[int x, int y]
        {
            get
            {
                if (x >= 0 && x < m_Width && y >= 0 && y < m_Width)
                    return (m_map[y*m_Width + x])/Constants.TerrainCompression;
                else
                {
                    //Get the nearest one so that things don't get screwed up near borders
                    int betterX = x < 0 ? 0 : x >= m_Width ? m_Width - 1 : x;
                    int betterY = y < 0 ? 0 : y >= m_Height ? m_Height - 1 : y;
                    return (m_map[betterY*m_Width + betterX])/Constants.TerrainCompression;
                }
            }
            set
            {
                // Will "fix" terrain hole problems. Although not fantastically.
                if (value*Constants.TerrainCompression > short.MaxValue)
                    value = short.MaxValue;
                if (value*Constants.TerrainCompression < short.MinValue)
                    value = short.MinValue;

                if (m_map[y*m_Width + x] != value*Constants.TerrainCompression)
                {
                    taint[x/Constants.TerrainPatchSize, y/Constants.TerrainPatchSize] = true;
                    m_map[y*m_Width + x] = (short) (value*Constants.TerrainCompression);
                }
            }
        }

        public bool Tainted(int x, int y)
        {
            if (taint[x/Constants.TerrainPatchSize, y/Constants.TerrainPatchSize])
            {
                taint[x/Constants.TerrainPatchSize, y/Constants.TerrainPatchSize] = false;
                return true;
            }
            return false;
        }

        public ITerrainChannel MakeCopy()
        {
            TerrainChannel copy = new TerrainChannel(false, m_scene)
                                      {m_map = (short[]) m_map.Clone(), taint = (bool[,]) taint.Clone()};
            return copy;
        }

        /// <summary>
        ///     Gets the average height of the area +2 in both the X and Y directions from the given position
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public float GetNormalizedGroundHeight(int x, int y)
        {
            if (x < 0)
                x = 0;
            if (x >= m_Width)
                x = m_Width - 1;
            if (y < 0)
                y = 0;
            if (y >= m_Height)
                y = m_Height - 1;

            Vector3 p0 = new Vector3(x, y, this[x, y]);
            Vector3 p1 = new Vector3(p0);
            Vector3 p2 = new Vector3(p0);

            p1.X += 1.0f;
            if (p1.X < m_Width)
                p1.Z = this[(int) p1.X, (int) p1.Y];

            p2.Y += 1.0f;
            if (p2.Y < m_Height)
                p2.Z = this[(int) p2.X, (int) p2.Y];

            Vector3 v0 = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);

            v0.Normalize();
            v1.Normalize();

            Vector3 vsn = new Vector3
                              {
                                  X = (v0.Y*v1.Z) - (v0.Z*v1.Y),
                                  Y = (v0.Z*v1.X) - (v0.X*v1.Z),
                                  Z = (v0.X*v1.Y) - (v0.Y*v1.X)
                              };
            vsn.Normalize();

            return ((vsn.X + vsn.Y)/(-1*vsn.Z)) + p0.Z;
        }

        /// <summary>
        /// Gets the average height of land above the waterline at the specified point.
        /// </summary>
        /// <returns>The normalized land height.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public float GetNormalizedLandHeight(int x, int y)
        {
            var groundHeight = GetNormalizedGroundHeight (x, y);
            var waterHeight = m_scene.RegionInfo.RegionSettings.WaterHeight;

            //var landHeight = (groundHeight < waterHeight) ? 0f : groundHeight - waterHeight;
            var landHeight = groundHeight - waterHeight;

            // this is the height above/below the waterline
            return (float) landHeight;
        }

        /// <summary>
        /// Generates  new terrain based upon supplied parameters.
        /// </summary>
        /// <param name="landType">Land type.</param>
        /// <param name="min">Minimum.</param>
        /// <param name="max">Max.</param>
        /// <param name="smoothing">Smoothing.</param>
        /// <param name="scene">Scene.</param>
        public void GenerateTerrain(string terrainType, float min, float max, int smoothing,  IScene scene)
		{
			m_scene = scene;
            m_Width = Constants.RegionSize;
            m_Height = m_Width;
			if (scene != null)
			    m_Width = scene.RegionInfo.RegionSizeX;                 // use the region size
                m_Height = scene.RegionInfo.RegionSizeY;
			
            if (terrainType == null)
			    terrainType = "f";                                      // Flatland then

			// try for the land type
            string tType = terrainType.ToLower ();
            if (tType.StartsWith("f"))                                  // basic flatland
                CreateFlatlandTerrain ();
            else if (tType.StartsWith("i"))                             // island
				CreateIslandTerrain (min, max, smoothing);
            else if (tType.StartsWith("a"))                             // auqatic
                CreateIslandTerrain (min, max, smoothing);              // TODO: fully sort this one out
            else if (tType.StartsWith("n"))                             // null space
                CreateNullSpaceTerrain ();   
			else
                // grassland, (tType.StartsWith("m") || tType.StartsWith("g") || tType.StartsWith("h"))
                    CreateMainlandTerrain (min, max, smoothing);

            CalcLandArea ();
		}

        public void ReCalcLandArea()
        {
            CalcLandArea();
        }

        #endregion

        /// <summary>
        /// Creates the default terrain, default is 'Flatland'
        /// </summary>
		void CreateDefaultTerrain(string terrainType)
		{
            float waterHeight = (float) m_scene.RegionInfo.RegionSettings.WaterHeight;

            if (terrainType == null)
                terrainType = "f";                     // Flatland
			
			// try for the land type
            var lT = terrainType.ToLower ();

            if (lT.Substring(1,2) == "ho")           // homestead
                CreateMainlandTerrain (4);
            else if (lT.StartsWith("m"))            // Mountainous
				CreateMainlandTerrain (2);
            else if (lT.StartsWith("h"))            // Hills
                CreateMainlandTerrain (3);
            else if (lT.StartsWith("g"))            // Grassland
                CreateMainlandTerrain (waterHeight - 0.5f, waterHeight + 2, 5);
			else if (lT.StartsWith("i"))            // Island
				CreateIslandTerrain ();
            else if (lT.StartsWith("a"))            // Aquatic
                CreateIslandTerrain (0, 15, 3);     // TODO: fully sort this one out
            else if (lT.StartsWith("s"))            // Swamp
                CreateMainlandTerrain (waterHeight - 0.5f, waterHeight + 0.75f, 3);
			else
				CreateFlatlandTerrain ();           // we need something

            CalcLandArea ();
		}

        void CreateNullSpaceTerrain()
        {

            m_map = null;
            taint = null;
            m_map = new short[m_scene.RegionInfo.RegionSizeX*m_scene.RegionInfo.RegionSizeY];
            taint =
                new bool[m_scene.RegionInfo.RegionSizeX/Constants.TerrainPatchSize,
                    m_scene.RegionInfo.RegionSizeY/Constants.TerrainPatchSize];
            m_Width = m_scene.RegionInfo.RegionSizeX;
            m_Height = m_scene.RegionInfo.RegionSizeY;

           /* int x;
            for (x = 0; x < m_scene.RegionInfo.RegionSizeX; x++)
            {
                int y;
                for (y = 0; y < m_scene.RegionInfo.RegionSizeY; y++)
                {
//                    this[x, y] = (float) m_scene.RegionInfo.RegionSettings.WaterHeight + .1f;
                    this[x, y] = 0.0f;
                }
            }
            */
            //m_scene.RegionInfo.RegionSettings.WaterHeight = 0.0f;
            //m_scene.RegionInfo.RegionSettings.TerrainTexture1 = UUID.Zero;
        }

        void CreateFlatlandTerrain()
		{

			m_map = null;
		 	taint = null;
            m_map = new short[m_scene.RegionInfo.RegionSizeX*m_scene.RegionInfo.RegionSizeY];
            taint =
                new bool[m_scene.RegionInfo.RegionSizeX/Constants.TerrainPatchSize,
                    m_scene.RegionInfo.RegionSizeY/Constants.TerrainPatchSize];
            m_Width = m_scene.RegionInfo.RegionSizeX;
            m_Height = m_scene.RegionInfo.RegionSizeY;

            int x;
            for (x = 0; x < m_scene.RegionInfo.RegionSizeX; x++)
            {
                int y;
                for (y = 0; y < m_scene.RegionInfo.RegionSizeY; y++)
                {
                    this[x, y] = (float) m_scene.RegionInfo.RegionSettings.WaterHeight + .1f;
                }
            }
        }


        void CreateMainlandTerrain(int smoothing)
		{
			float minHeight = (float) m_scene.RegionInfo.RegionSettings.WaterHeight - 5;
			float maxHeight = 30;

			CreateMainlandTerrain (minHeight, maxHeight,smoothing);
		}

		void CreateMainlandTerrain (float minHeight, float maxHeight, int smoothing)
		{
			m_map = null;
			taint = null;
			m_map = new short[m_scene.RegionInfo.RegionSizeX*m_scene.RegionInfo.RegionSizeY];
			taint =
				new bool[m_scene.RegionInfo.RegionSizeX/Constants.TerrainPatchSize,
				         m_scene.RegionInfo.RegionSizeY/Constants.TerrainPatchSize];

			int rWidth = m_scene.RegionInfo.RegionSizeX;
			int rHeight = m_scene.RegionInfo.RegionSizeY;
			m_Width = rWidth; 
            m_Height = rHeight;
            float waterHeight = (float) m_scene.RegionInfo.RegionSettings.WaterHeight;

			int octaveCount = 8;
			float[][] heightMap = PerlinNoise.GenerateHeightMap(rWidth, rHeight, octaveCount, minHeight, maxHeight, smoothing);
            float[][] blendMap = PerlinNoise.EdgeBlendMainlandMap (heightMap, waterHeight);

            // set the terrain heightmap
            int x;
            int y;
            for (x = 0; x < rWidth; x++)
			{
				for (y = 0; y < rHeight; y++)
				{
//					this[x, y] = heightMap[x][y];
                    this[x, y] = blendMap[x][y];
				}
			}
		}

		void CreateIslandTerrain()
		{
			float minHeight = (float) m_scene.RegionInfo.RegionSettings.WaterHeight - 5;
            float maxHeight = minHeight + 15;

			CreateIslandTerrain (minHeight, maxHeight,2);
		}

		void CreateIslandTerrain(float minHeight, float maxHeight, int smoothing)
		{
			m_map = null;
			taint = null;
			m_map = new short[m_scene.RegionInfo.RegionSizeX*m_scene.RegionInfo.RegionSizeY];
			taint =
				new bool[m_scene.RegionInfo.RegionSizeX/Constants.TerrainPatchSize,
				         m_scene.RegionInfo.RegionSizeY/Constants.TerrainPatchSize];

			int rWidth = m_scene.RegionInfo.RegionSizeX;
			int rHeight = m_scene.RegionInfo.RegionSizeY;
			m_Width = rWidth; 
            m_Height = rHeight;

			int octaveCount = 8;
			float[][] heightMap = PerlinNoise.GenerateIslandMap(rWidth, rHeight, octaveCount, minHeight, maxHeight, smoothing);

			int x;
			for (x = 0; x < rWidth; x++)
			{
				int y;
				for (y = 0; y < rHeight; y++)
				{
					this[x, y] = heightMap[x][y];
				}
			}
		}

		// original island from opensim
		void CreateAtolIslandTerrain()
		{
			m_map = null;
			taint = null;
			m_map = new short[m_scene.RegionInfo.RegionSizeX*m_scene.RegionInfo.RegionSizeY];
			taint =
				new bool[m_scene.RegionInfo.RegionSizeX/Constants.TerrainPatchSize,
				         m_scene.RegionInfo.RegionSizeY/Constants.TerrainPatchSize];
			m_Width = m_scene.RegionInfo.RegionSizeX;
            m_Height = m_scene.RegionInfo.RegionSizeY;

			int x;
			int y;
			float regionDiv = (float) (Constants.RegionSize / 2.0);

			for (x = 0; x < m_scene.RegionInfo.RegionSizeX; x++)
			{
				for (y = 0; y < m_scene.RegionInfo.RegionSizeY; y++)
				{
					this [x, y] = (float)TerrainUtil.PerlinNoise2D (x, y, 2, (float)  0.125) * 10;
					float spherFacA = (float) (TerrainUtil.SphericalFactor ( x, y, regionDiv, regionDiv, 50) * 0.01);
					float spherFacB = (float) (TerrainUtil.SphericalFactor ( x, y, regionDiv, regionDiv, 100) * 0.001);
					if (this [x, y] < spherFacA)
						this [x, y] = spherFacA;
					if (this [x, y] < spherFacB)
						this [x, y] = spherFacB;
				}
			}
		}

        /// <summary>
        /// Calculates the land area.
        /// </summary>
        void CalcLandArea()
        {
            uint regionArea = 0;

            int x;
            for (x = 0; x < m_scene.RegionInfo.RegionSizeX; x++)
            {
                int y;
                for (y = 0; y < m_scene.RegionInfo.RegionSizeY; y++)
                {
                    if (this [x, y] > m_scene.RegionInfo.RegionSettings.WaterHeight)
                        regionArea++;
                                
                }
            }

           m_scene.RegionInfo.RegionArea = regionArea;
        }

	}
}