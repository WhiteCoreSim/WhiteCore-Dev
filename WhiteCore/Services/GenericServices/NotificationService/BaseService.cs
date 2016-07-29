﻿/*
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
using Nini.Config;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services
{
    public class BaseService : IApplicationPlugin
    {
        #region IApplicationPlugin Members

        public string Name {
            get { return "BaseNotificationService"; }
        }

        public void PreStartup (ISimulationBase simBase)
        {
            SetUpConsole (simBase.ConfigSource, simBase);
        }

        public void Initialize (ISimulationBase simBase)
        {
        }

        void SetUpConsole (IConfigSource config, ISimulationBase simbase)
        {
            List<ICommandConsole> Plugins = WhiteCoreModuleLoader.PickupModules<ICommandConsole> ();
            foreach (ICommandConsole plugin in Plugins) {
                plugin.Initialize (config, simbase);
            }

            if (MainConsole.Instance == null) {
                Console.WriteLine ("[Console]: No Console configured, falling back to 'LocalConsole'");
                new LocalConsole ().LocalInitialize (config, simbase);
            }

            MainConsole.Instance.Threshold = Level.Info;

            MainConsole.Instance.Fatal (string.Format ("[Console]: Console log level is {0}", MainConsole.Instance.Threshold));

            MainConsole.Instance.Commands.AddCommand (
                "set log level",
                "set log level [level]",
                "Set the console logging level",
                HandleLogLevel, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "get log level",
                "get log level",
                "Returns the current console logging level",
                HandleGetLogLevel, false, true);
        }

        public void PostInitialise ()
        {
        }

        public void Start ()
        {
        }

        public void PostStart ()
        {
        }

        public void Close ()
        {
        }

        public void ReloadConfiguration (IConfigSource m_config)
        {
        }

        #endregion

        #region Console Commands

        static void HandleGetLogLevel (IScene scene, string [] cmd)
        {
            MainConsole.Instance.Fatal (string.Format ("Console log level is {0}", MainConsole.Instance.Threshold));
        }

        static void HandleLogLevel (IScene scene, string [] cmd)
        {
            string rawLevel = cmd [3];

            try {
                MainConsole.Instance.Threshold = (Level)Enum.Parse (typeof (Level), rawLevel, true);
            } catch { }
            MainConsole.Instance.Format (Level.Off, "Console log level is {0}", MainConsole.Instance.Threshold);
        }

        #endregion
    }
}
