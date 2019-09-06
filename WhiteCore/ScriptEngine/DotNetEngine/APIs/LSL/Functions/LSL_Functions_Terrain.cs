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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using WhiteCore.ScriptEngine.DotNetEngine.Plugins;
using WhiteCore.ScriptEngine.DotNetEngine.Runtime;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using LSL_Float = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger;
using LSL_Key = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.list;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {
        public LSL_Float llCloud (LSL_Vector offset)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            float cloudCover = 0f;
            ICloudModule module = World.RequestModuleInterface<ICloudModule> ();
            if (module != null) {
                Vector3 pos = m_host.GetWorldPosition ();
                int x = (int)(pos.X + offset.x);
                int y = (int)(pos.Y + offset.y);

                cloudCover = module.CloudCover (x, y, 0);
            }
            return cloudCover;
        }

        public LSL_Float llGround (LSL_Vector offset)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Float ();

            Vector3 pos = m_host.GetWorldPosition () + new Vector3 ((float)offset.x,
                                                                  (float)offset.y,
                                                                  (float)offset.z);

            //Get the slope normal.  This gives us the equation of the plane tangent to the slope.
            LSL_Vector vsn = llGroundNormal (offset);
            ITerrainChannel heightmap = World.RequestModuleInterface<ITerrainChannel> ();
            // Clamp to valid position
            if (pos.X < 0)
                pos.X = 0;
            else if (pos.X >= heightmap.Width)
                pos.X = heightmap.Width - 1;
            if (pos.Y < 0)
                pos.Y = 0;
            else if (pos.Y >= heightmap.Height)
                pos.Y = heightmap.Height - 1;

            //Get the height for the integer coordinates from the Heightmap
            float baseheight = heightmap [(int)pos.X, (int)pos.Y];

            //Calculate the difference between the actual coordinates and the integer coordinates
            float xdiff = pos.X - (int)pos.X;
            float ydiff = pos.Y - (int)pos.Y;

            //Use the equation of the tangent plane to adjust the height to account for slope

            return (((vsn.x * xdiff) + (vsn.y * ydiff)) / (-1 * vsn.z)) + baseheight;
        }

        public LSL_Vector llGetSunDirection ()
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();


            LSL_Vector SunDoubleVector3;

            // have to convert from Vector3 (float) to LSL_Vector (double)
            Vector3 SunFloatVector3 = World.RegionInfo.RegionSettings.SunVector;
            SunDoubleVector3.x = SunFloatVector3.X;
            SunDoubleVector3.y = SunFloatVector3.Y;
            SunDoubleVector3.z = SunFloatVector3.Z;

            return SunDoubleVector3;
        }

        public LSL_Vector llGroundSlope (LSL_Vector offset)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            //Get the slope normal.  This gives us the equation of the plane tangent to the slope.
            LSL_Vector vsn = llGroundNormal (offset);

            //Plug the x,y coordinates of the slope normal into the equation of the plane to get
            //the height of that point on the plane.  The resulting vector gives the slope.
            Vector3 vsl = new Vector3 {
                X = (float)vsn.x,
                Y = (float)vsn.y,
                Z = (float)(((vsn.x * vsn.x) + (vsn.y * vsn.y)) / (-1 * vsn.z))
            };
            vsl.Normalize ();
            //Normalization might be overkill here

            return new LSL_Vector (vsl.X, vsl.Y, vsl.Z);
        }

        public LSL_Vector llGroundNormal (LSL_Vector offset)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            Vector3 pos = m_host.GetWorldPosition () + new Vector3 ((float)offset.x,
                                                                  (float)offset.y,
                                                                  (float)offset.z);
            ITerrainChannel heightmap = World.RequestModuleInterface<ITerrainChannel> ();
            // Clamp to valid position
            if (pos.X < 0)
                pos.X = 0;
            else if (pos.X >= heightmap.Width)
                pos.X = heightmap.Width - 1;
            if (pos.Y < 0)
                pos.Y = 0;
            else if (pos.Y >= heightmap.Height)
                pos.Y = heightmap.Height - 1;

            //Find two points in addition to the position to define a plane
            Vector3 p0 = new Vector3 (pos.X, pos.Y,
                                     heightmap [(int)pos.X, (int)pos.Y]);
            Vector3 p1 = new Vector3 ();
            Vector3 p2 = new Vector3 ();
            if ((pos.X + 1.0f) >= heightmap.Width)
                p1 = new Vector3 (pos.X + 1.0f, pos.Y,
                                 heightmap [(int)pos.X, (int)pos.Y]);
            else
                p1 = new Vector3 (pos.X + 1.0f, pos.Y,
                                 heightmap [(int)(pos.X + 1.0f), (int)pos.Y]);
            if ((pos.Y + 1.0f) >= heightmap.Height)
                p2 = new Vector3 (pos.X, pos.Y + 1.0f,
                                 heightmap [(int)pos.X, (int)pos.Y]);
            else
                p2 = new Vector3 (pos.X, pos.Y + 1.0f,
                                 heightmap [(int)pos.X, (int)(pos.Y + 1.0f)]);

            //Find normalized vectors from p0 to p1 and p0 to p2
            Vector3 v0 = new Vector3 (p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3 (p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);
            //            v0.Normalize();
            //            v1.Normalize();

            //Find the cross product of the vectors (the slope normal).
            Vector3 vsn = new Vector3 {
                X = (v0.Y * v1.Z) - (v0.Z * v1.Y),
                Y = (v0.Z * v1.X) - (v0.X * v1.Z),
                Z = (v0.X * v1.Y) - (v0.Y * v1.X)
            };
            vsn.Normalize ();
            //I believe the crossproduct of two normalized vectors is a normalized vector so
            //this normalization may be overkill
            // then don't normalize them just the result

            return new LSL_Vector (vsn.X, vsn.Y, vsn.Z);
        }

        public LSL_Vector llGroundContour (LSL_Vector offset)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Vector ();

            LSL_Vector x = llGroundSlope (offset);
            return new LSL_Vector (-x.y, x.x, 0.0);
        }

        public void llGroundRepel (double height, int water, double tau)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            if (m_host.PhysActor != null) {
                float ground = (float)llGround (new LSL_Vector (0, 0, 0));
                float waterLevel = (float)llWater (new LSL_Vector (0, 0, 0));
                PIDHoverType hoverType = PIDHoverType.Ground;
                if (water != 0) {
                    hoverType = PIDHoverType.GroundAndWater;
                    if (ground < waterLevel)
                        height += waterLevel;
                    else
                        height += ground;
                } else {
                    height += ground;
                }

                m_host.SetHoverHeight ((float)height, hoverType, (float)tau);
            }
        }

        public void llModifyLand (int action, int brush)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            ITerrainModule tm = World.RequestModuleInterface<ITerrainModule> ();
            if (tm != null) {
                tm.ModifyTerrain (m_host.OwnerID, m_host.AbsolutePosition, (byte)brush, (byte)action, m_host.OwnerID);
            }
        }






    }
}
