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
using System.Collections.Generic;
using System.Linq;
using System.Security;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;

namespace WhiteCore.ScriptEngine.DotNetEngine.MiniModule
{
    class SPAvatar : MarshalByRefObject, IAvatar
    {
        readonly UUID m_ID;
        readonly IScene m_rootScene;
        readonly ISecurityCredential m_security;

        public SPAvatar(IScene scene, UUID ID, ISecurityCredential security)
        {
            m_rootScene = scene;
            m_security = security;
            m_ID = ID;
        }

        #region IAvatar Members

        public string Name
        {
            get { return GetSP().Name; }
            set { throw new SecurityException("Avatar Names are a read-only property."); }
        }

        public UUID GlobalID
        {
            get { return m_ID; }
        }

        public Vector3 WorldPosition
        {
            get { return GetSP().AbsolutePosition; }
            set { GetSP().TeleportWithMomentum(value); }
        }

        public bool IsChildAgent
        {
            get { return GetSP().IsChildAgent; }
        }

        #endregion

        #region IAvatar implementation

        public IAvatarAttachment[] Attachments
        {
            get
            {
                IAvatarAppearanceModule appearance = GetSP().RequestModuleInterface<IAvatarAppearanceModule>();
                List<AvatarAttachment> internalAttachments = appearance.Appearance.GetAttachments();

                return
                    internalAttachments.Select(
                        attach =>
                        new SPAvatarAttachment(m_rootScene, this, attach.AttachPoint, new UUID(attach.ItemID),
                                               new UUID(attach.AssetID), m_security))
                                       .Cast<IAvatarAttachment>()
                                       .ToArray();
            }
        }

        public void LoadUrl(IObject sender, string message, string url)
        {
            IDialogModule dm = m_rootScene.RequestModuleInterface<IDialogModule>();
            if (dm != null)
                dm.SendUrlToUser(GetSP().UUID, sender.Name, sender.GlobalID, GetSP().UUID, false, message, url);
        }

        #endregion

        private IScenePresence GetSP()
        {
            return m_rootScene.GetScenePresence(m_ID);
        }
    }
}
