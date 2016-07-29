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
using System.IO;
using System.Reflection;

namespace WhiteCore.ScriptEngine.DotNetEngine
{
    [Serializable]
    public class AssemblyResolver : MarshalByRefObject
    {
        readonly string PathToSearch = "";

        public AssemblyResolver(string pathToSearch)
        {
            PathToSearch = pathToSearch;
        }

        public Assembly OnAssemblyResolve(object sender,
                                          ResolveEventArgs args)
        {
            if (!(sender is AppDomain))
                return null;

            string[] pathList = {
                Path.Combine(Directory.GetCurrentDirectory(), "bin"),
                Path.Combine(Directory.GetCurrentDirectory(), PathToSearch),
                Path.Combine(Directory.GetCurrentDirectory(), 
                             Path.Combine(PathToSearch, "Scripts")),
                Directory.GetCurrentDirectory()
            };

            string assemblyName = args.Name;
            if (assemblyName.IndexOf (",", StringComparison.Ordinal) != -1)
                assemblyName = args.Name.Substring(0, args.Name.IndexOf (",", StringComparison.Ordinal));

            foreach (string s in pathList)
            {
                string path = Path.Combine(s, assemblyName) + ".dll";

                if (File.Exists(path))
                    return Assembly.Load(AssemblyName.GetAssemblyName(path));
            }
            return null;
        }
    }
}
