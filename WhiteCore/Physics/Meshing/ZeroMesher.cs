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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.SceneInfo;

/*
 * This is the zero mesher.
 * Whatever you want him to mesh, he can't, telling you that by responding with a null pointer.
 * Effectivly this is for switching off meshing and for testing as each physics machine should deal
 * with the null pointer situation.
 * But it's also a convenience thing, as physics machines can rely on having a mesher in any situation, even
 * if it's a dump one like this.
 * Note, that this mesher is *not* living in a module but in the manager itself, so
 * it's always availabe and thus the default in case of configuration errors
*/

namespace WhiteCore.Physics.Meshing
{
    public class ZeroMesherPlugin : IMeshingPlugin
    {
        #region IMeshingPlugin Members

        public string GetName()
        {
            return "ZeroMesher";
        }

        public IMesher GetMesher(IConfigSource config, IRegistryCore registry)
        {
            return new ZeroMesher();
        }

        #endregion
    }

    public class ZeroMesher : IMesher
    {
        #region IMesher Members

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical)
        {
            return null;
        }

        public void RemoveMesh(ulong key)
        {
        }

        #endregion
    }
}