using WhiteCore.Framework.Modules;
using Nini.Config;

namespace WhiteCore.Framework.Physics
{
    public interface IMeshingPlugin
    {
        string GetName();
        IMesher GetMesher(IConfigSource config, IRegistryCore registry);
    }
}