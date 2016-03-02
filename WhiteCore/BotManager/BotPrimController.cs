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

using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;

namespace WhiteCore.BotManager
{
    public class BotPrimController : IBotController
    {
        ISceneEntity m_object;
        Bot m_bot;
        bool m_run;
        float m_speed = 1;
        bool m_hasStoppedMoving;

        public BotPrimController(ISceneEntity obj, Bot bot)
        {
            m_object = obj;
            m_bot = bot;
        }

        public string Name
        {
            get { return m_object.Name; }
        }

        public UUID UUID
        {
            get { return m_object.UUID; }
        }

        public bool SetAlwaysRun
        {
            get { return m_run; }
            set { m_run = value; }
        }

        public bool ForceFly
        {
            get { return false; }
            set { }
        }

        public PhysicsActor PhysicsActor
        {
            get { return m_object.RootChild.PhysActor; }
        }

        public bool CanMove
        {
            get { return true; }
        }

        public Vector3 AbsolutePosition
        {
            get { return m_object.AbsolutePosition; }
        }

        public void SendChatMessage(int sayType, string message, int channel)
        {
            IChatModule chatModule = m_object.Scene.RequestModuleInterface<IChatModule>();
            if (chatModule != null)
                chatModule.SimChat(message, (ChatTypeEnum) sayType, channel,
                                   m_object.RootChild.AbsolutePosition, m_object.Name, m_object.UUID, false,
                                   m_object.Scene);
        }

        public void SendInstantMessage(GridInstantMessage im)
        {
            IMessageTransferModule m_TransferModule =
                m_object.Scene.RequestModuleInterface<IMessageTransferModule>();
            if (m_TransferModule != null)
                m_TransferModule.SendInstantMessage(im);
        }

        public void Close()
        {
        }

        public void OnBotAgentUpdate(Vector3 toward, uint controlFlag, Quaternion bodyRotation)
        {
            OnBotAgentUpdate(toward, controlFlag, bodyRotation, true);
        }

        public void OnBotAgentUpdate(Vector3 toward, uint controlFlag, Quaternion bodyRotation, bool isMoving)
        {
            if (isMoving)
                m_hasStoppedMoving = false;

            m_object.AbsolutePosition += toward * (m_speed * (1f/45f));
            m_object.ScheduleGroupTerseUpdate();
        }

        public void UpdateMovementAnimations(bool sendTerseUpdate)
        {
            if (sendTerseUpdate)
                m_object.ScheduleGroupTerseUpdate();
        }

        public void Teleport(Vector3 pos)
        {
            m_object.AbsolutePosition = pos;
        }

        public IScene GetScene()
        {
            return m_object.Scene;
        }

        public void StopMoving(bool fly, bool clearPath)
        {
            if (m_hasStoppedMoving)
                return;
            m_hasStoppedMoving = true;
            m_bot.State = BotState.Idle;

            //Clear out any nodes
            if (clearPath)
                m_bot.m_nodeGraph.Clear();

            //Send the stop message
            m_bot.m_movementFlag = (uint) AgentManager.ControlFlags.NONE;
            if (fly)
                m_bot.m_movementFlag |= (uint) AgentManager.ControlFlags.AGENT_CONTROL_FLY;

            OnBotAgentUpdate(Vector3.Zero, m_bot.m_movementFlag, m_bot.m_bodyDirection, false);

            if (m_object.RootChild.PhysActor != null)
                m_object.RootChild.PhysActor.ForceSetVelocity(Vector3.Zero);
        }

        public void SetSpeedModifier(float speed)
        {
            if (speed > 4)
                speed = 4;
            m_speed = speed;
        }

        public void SetDrawDistance(float draw)
        {
        }

        public void StandUp()
        {
        }

        public void Jump()
        {
            m_bot.m_nodeGraph.Clear();
            m_bot.m_nodeGraph.FollowIndefinitely = false;
            m_bot.m_nodeGraph.Add(m_object.AbsolutePosition + new Vector3(0, 0, 1.5f), TravelMode.Walk);
            m_bot.m_nodeGraph.Add(m_object.AbsolutePosition, TravelMode.Walk);
            m_bot.ForceCloseToPoint = true;
            m_bot.m_closeToPoint = 0.1f;
            m_bot.GetNextDestination();
        }
    }
}