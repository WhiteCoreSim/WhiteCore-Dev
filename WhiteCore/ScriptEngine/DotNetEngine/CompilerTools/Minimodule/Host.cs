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
using WhiteCore.Framework.SceneInfo;

namespace WhiteCore.ScriptEngine.DotNetEngine.MiniModule
{
    class Host : MarshalByRefObject, IHost
    {
        readonly IExtension m_extend;
        readonly IGraphics m_graphics;
        readonly IObject m_obj;
        readonly MicroScheduler m_threader = new MicroScheduler();
        // Scene m_scene;

        public Host(IObject m_obj, IScene m_scene, IExtension m_extend)
        {
            this.m_obj = m_obj;
            this.m_extend = m_extend;

            m_graphics = new Graphics(m_scene);

            m_scene.EventManager.OnFrame += EventManager_OnFrame;
        }

        #region IHost Members

        public IObject Object
        {
            get { return m_obj; }
        }

        public IGraphics Graphics
        {
            get { return m_graphics; }
        }

        public IExtension Extensions
        {
            get { return m_extend; }
        }

        public IMicrothreader Microthreads
        {
            get { return m_threader; }
        }

        #endregion

        void EventManager_OnFrame()
        {
            m_threader.Tick(1000);
        }
    }
}
