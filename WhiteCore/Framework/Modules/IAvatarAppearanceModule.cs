using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.PresenceInfo;

namespace WhiteCore.Framework.Modules
{
    public interface IAvatarAppearanceModule
    {
        /// <summary>
        ///     The appearance that this agent has
        /// </summary>
        AvatarAppearance Appearance { get; set; }

        bool InitialHasWearablesBeenSent { get; set; }
        void SendAppearanceToAgent(IScenePresence sp);
        void SendAvatarDataToAgent(IScenePresence sp, bool sendAppearance);
        void SendOtherAgentsAppearanceToMe();
        void SendAppearanceToAllOtherAgents();
        void SendAvatarDataToAllAgents(bool sendAppearance);
    }
}