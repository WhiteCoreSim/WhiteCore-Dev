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

using Nini.Config;
using WhiteCore.Framework.Modules;

namespace WhiteCore.Framework.ConsoleFramework
{
    /// <summary>
    ///     This is a Fake console that's used when setting up the Scene in Unit Tests
    ///     Don't use this except for Unit Testing or you're in for a world of hurt when the
    ///     sim gets to ReadLine
    /// </summary>
    public class MockConsole : CommandConsole
    {
        public override void Initialize (IConfigSource source, ISimulationBase simBase)
        {
            if (source.Configs ["Console"] == null ||
                source.Configs ["Console"].GetString ("Console", string.Empty) != "MockConsole") {
                return;
            }

            simBase.ApplicationRegistry.RegisterModuleInterface<ICommandConsole> (this);
            MainConsole.Instance = this;

            m_Commands.AddCommand ("help", "help", "Get a general command list", Help, false, true);
        }

        public override void Output (string text, Level level)
        {
        }

        public override string ReadLine (string p, bool isCommand, bool e)
        {
            //Thread.CurrentThread.Join(1000);
            return string.Empty;
        }

        public override void UnlockOutput ()
        {
        }

        public override void LockOutput ()
        {
        }
    }
}