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

using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using Nini.Config;
using System;

namespace WhiteCore.Modules.World.ServerSettingsModule
{
    public class PhysicsServerSettingsModule : INonSharedRegionModule
    {
        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(IScene scene)
        {
        }

        public void RegionLoaded(IScene scene)
        {
            IServerSettings serverSettings = scene.RequestModuleInterface<IServerSettings>();
            ServerSetting gravitySetting = new ServerSetting
                                               {
                                                   Name = "Gravity",
                                                   Comment = "The forces of gravity that are on this sim",
                                                   Type = "Color4" //All arrays are color4
                                               };
            gravitySetting.OnGetSetting += delegate()
                                               {
                                                   return
                                                       string.Format(
                                                           "<array><real>{0}</real><real>{1}</real><real>{2}</real><real>1.0</real></array>",
                                                           scene.PhysicsScene.GetGravityForce()[0],
                                                           scene.PhysicsScene.GetGravityForce()[1],
                                                           scene.PhysicsScene.GetGravityForce()[2]);
                                               };
            gravitySetting.OnUpdatedSetting += delegate(string value) { };

            serverSettings.RegisterSetting(gravitySetting);
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "PhysicsServerSettingsModules"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }
    }
}