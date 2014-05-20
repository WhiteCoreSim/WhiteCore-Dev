using WhiteCore.Framework.ClientInterfaces;

namespace WhiteCore.Framework.Modules
{
    public interface IEnvironmentSettingsModule
    {
        WindlightDayCycle GetCurrentDayCycle();
        void TriggerWindlightUpdate(int interpolate);

        void SetDayCycle(WindlightDayCycle cycle);
    }
}