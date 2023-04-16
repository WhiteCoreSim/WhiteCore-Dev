using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static WhiteCore.ScriptEngine.DotNetEngine.LSL_Types;
using LSL_Float = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger;
using LSL_Key = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.List;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {
        public LSLInteger llAgentInExperience(LSL_Key agent)
        {
            NotImplemented("llAgentInExperience", "Not implemented at this moment");
            return 0;
        }
        public void llClearExperiencePermissions(LSL_Key agent)
        {
            NotImplemented("llClearExperiencePermissions", "Not implemented at this moment");
        }
        public LSL_Key llCreateKeyValue(LSL_String key, LSL_String value)
        {
            NotImplemented("llClearExperiencePermissions", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llDataSizeKeyValue()
        {
            NotImplemented("llDataSizeKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llDeleteKeyValue(LSL_String key)
        {
            NotImplemented("llDeleteKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_List llGetExperienceDetails(LSL_Key experience_id)
        {
            NotImplemented("llGetExperienceDetails", "Not implemented at this moment");
            return new LSL_List();
        }

        public LSL_String llGetExperienceErrorMessage(LSL_Integer value)
        {
            NotImplemented("llGetExperienceDetails", "Not implemented at this moment");
            return String.Empty;
        }

        public LSL_List llGetExperienceList(LSL_Key agent)
        {
            NotImplemented("llGetExperienceDetails", "Function was deprecated");
            return new LSL_List();
        }

        public LSL_Key llKeyCountKeyValue()
        {
            NotImplemented("llKeyCountKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llKeysKeyValue(LSL_Integer first, LSL_Integer count)
        {
            NotImplemented("llKeysKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public LSL_Key llReadKeyValue(LSL_String key)
        {
            NotImplemented("llReadKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }

        public void llRequestExperiencePermissions(LSL_Key agent, LSL_String name)
        {
            NotImplemented("llRequestExperiencePermissions", "Not implemented at this moment");
        }

        public LSL_Integer llSitOnLink(LSL_Key agent_id, LSL_Integer link)
        {
            NotImplemented("llSitOnLink", "Not implemented at this moment");
            return 0;
        }

        public LSL_Key llUpdateKeyValue(LSL_Key key, LSL_String value, LSL_Integer check, LSL_String original_value)
        {
            NotImplemented("llUpdateKeyValue", "Not implemented at this moment");
            return UUID.Zero.ToString();
        }
    }
}
