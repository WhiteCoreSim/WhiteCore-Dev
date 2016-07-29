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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using WhiteCore.Framework.SceneInfo;

namespace WhiteCore.ScriptEngine.DotNetEngine.Runtime
{
    public partial class ScriptBaseClass : MarshalByRefObject, IScript, IDisposable
    {
        readonly ScriptSponsor m_sponser;

        public ISponsor Sponsor
        {
            get { return m_sponser; }
        }

        bool m_stateSaveRequired;

        public bool NeedsStateSaved
        {
            get
            {
                if (m_stateSaveRequired)
                    return true;
                if (!m_useStateSaves)
                    return false;
                if (m_lastStateSaveValues == null)
                    m_lastStateSaveValues = m_InitialValues;
                Dictionary<string, object> vars = GetVars();
                return vars.Any(kvp => m_lastStateSaveValues[kvp.Key].ToString() != kvp.Value.ToString());
            }
            set
            {
                m_stateSaveRequired = value;
                if (!m_useStateSaves)
                    return;
                //Besides setting the value, if we don't need one, save the vars we have for the last state save as well
                if (!value)
                    m_lastStateSaveValues = GetVars();
            }
        }

        Dictionary<string, object> m_lastStateSaveValues;
        public IScene Scene;
        public ISceneChildEntity Object;
        public bool m_useStateSaves = true;

        public void SetSceneRefs(IScene scene, ISceneChildEntity child, bool useStateSaves)
        {
            Scene = scene;
            Object = child;
            m_useStateSaves = useStateSaves;
        }

        public override object InitializeLifetimeService()
        {
            try
            {
                ILease lease = (ILease) base.InitializeLifetimeService();

                if (lease.CurrentState == LeaseState.Initial)
                {
                    // Infinite : lease.InitialLeaseTime = TimeSpan.FromMinutes(0);
                    lease.InitialLeaseTime = TimeSpan.FromMinutes(0);
                    //lease.InitialLeaseTime = TimeSpan.FromMinutes(0);
                    //lease.RenewOnCallTime = TimeSpan.FromMinutes(10.0);
                    //lease.SponsorshipTimeout = TimeSpan.FromMinutes(1.0);
                }
                return lease;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public ScriptBaseClass()
        {
            m_Executor = new Executor(this);

            m_sponser = new ScriptSponsor();
        }

        public Executor m_Executor;

        public virtual long GetStateEventFlags(string state)
        {
            return (long) m_Executor.GetStateEventFlags(state);
        }

        public EnumeratorInfo ExecuteEvent(string state, string FunctionName, object[] args, EnumeratorInfo Start,
                                           out Exception ex)
        {
            return m_Executor.ExecuteEvent(state, FunctionName, args, Start, out ex);
        }

        public bool CheckSlice()
        {
            return m_Executor.CheckSlice();
        }

        Dictionary<string, object> m_InitialValues = new Dictionary<string, object>();

        public Dictionary<string, IScriptApi> m_apis = new Dictionary<string, IScriptApi>();

        public void InitApi(IScriptApi data)
        {
            /*ILease lease = (ILease)RemotingServices.GetLifetimeService(data as MarshalByRefObject);
            if (lease != null)
                lease.Register(m_sponser);*/
            m_apis.Add(data.Name, data);
        }

        public void UpdateInitialValues()
        {
            m_InitialValues = GetVars();
        }

        public virtual void StateChange(string newState)
        {
        }

        public void Close()
        {
            m_sponser.Close();
        }

        public Dictionary<string, object> GetVars()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();

            Type t = GetType();

            FieldInfo[] fields = t.GetFields(BindingFlags.NonPublic |
                                             BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof (LSL_Types.list)) // ref type, copy
                {
                    LSL_Types.list v = (LSL_Types.list) field.GetValue(this);
                    if (((object) v) == null)
                        continue; //Broken... :/
                    object[] data = new object[v.Data.Length];
                    Array.Copy(v.Data, 0, data, 0, v.Data.Length);
                    LSL_Types.list c = new LSL_Types.list {Data = data};
                    vars[field.Name] = c;
                }
                else if (field.FieldType == typeof (LSL_Types.LSLInteger) ||
                         field.FieldType == typeof (LSL_Types.LSLString) ||
                         field.FieldType == typeof (LSL_Types.LSLFloat) ||
                         field.FieldType == typeof (int) ||
                         field.FieldType == typeof (double) ||
                         field.FieldType == typeof (float) ||
                         field.FieldType == typeof (string) ||
                         field.FieldType == typeof (byte) ||
                         field.FieldType == typeof (short) ||
                         field.FieldType == typeof (LSL_Types.Vector3) ||
                         field.FieldType == typeof (LSL_Types.Quaternion))
                {
                    vars[field.Name] = field.GetValue(this);
                }
            }
            fields = null;
            t = null;

            return vars;
        }

        /// <summary>
        ///     Note: this is just used for reset
        /// </summary>
        /// <param name="vars"></param>
        public void SetVars(Dictionary<string, object> vars)
        {
            if (!m_useStateSaves)
                return;
            Type t = GetType();
            FieldInfo[] fields = t.GetFields(BindingFlags.NonPublic |
                                             BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (vars.ContainsKey(field.Name))
                {
                    object newVal = vars[field.Name];
                    if (field.FieldType == typeof (LSL_Types.list))
                    {
                        LSL_Types.list v = (LSL_Types.list) field.GetValue(this);
                        object[] data = ((LSL_Types.list) (newVal)).Data;
                        v.Data = new object[data.Length];
                        Array.Copy(data, 0, v.Data, 0, data.Length);
                        field.SetValue(this, v);
                    }

                    else if (field.FieldType == typeof (LSL_Types.LSLInteger) ||
                             field.FieldType == typeof (LSL_Types.LSLString) ||
                             field.FieldType == typeof (LSL_Types.LSLFloat) ||
                             field.FieldType == typeof (int) ||
                             field.FieldType == typeof (double) ||
                             field.FieldType == typeof (float) ||
                             field.FieldType == typeof (string) ||
                             field.FieldType == typeof (byte) ||
                             field.FieldType == typeof (short) ||
                             field.FieldType == typeof (LSL_Types.Vector3) ||
                             field.FieldType == typeof (LSL_Types.Quaternion)
                        )
                    {
                        field.SetValue(this, newVal);
                    }
                }
            }
            fields = null;
            t = null;
        }

        public string GetShortType(object o)
        {
            string tmp = o.GetType().ToString();
            int i = tmp.LastIndexOf('+');
            string type = tmp.Substring(i + 1);
            return type;
        }

        #region String Escaping for Save/Load States

        public string EscapeString(string str)
        {
            // Must escape string for lists
            // This escapes double quotes first, then backslashes
            return str.Replace("\"", "\\\"").Replace("\\", "\\\\");
        }

        public string UnEscapeString(string str)
        {
            // Must do the opposite of EscapeString
            // This unescapes backslashes first, then quotes
            return str.Replace("\\\\", "\\").Replace("\\\"", "\"");
        }

        #endregion

        public string ListToString(object o)
        {
            string tmp = "";
            string cur = "";
            LSL_Types.list v = (LSL_Types.list) o;
            foreach (object ob in v.Data)
            {
				if (ob is LSL_Types.LSLInteger)
					cur = "i" + ob;
				else if (ob is LSL_Types.LSLFloat)
					cur = "f" + ob;
				else if (ob is LSL_Types.Vector3)
					cur = "v" + ob;
				else if (ob is LSL_Types.Quaternion)
					cur = "q" + ob;
				else if (ob is LSL_Types.LSLString)
					cur = "\"" + EscapeString(ob.ToString()) + "\"";
				else if (o.GetType() == typeof(LSL_Types.list))
					cur = "{" + ListToString(ob) + "}";

                if (tmp == "")
                    tmp = cur;
                else
                    tmp += ", " + cur;
            }
            return tmp;
        }

        public Dictionary<string, object> GetStoreVars()
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            if (!m_useStateSaves)
                return vars;

            Type t = GetType();

            FieldInfo[] fields = t.GetFields(BindingFlags.NonPublic |
                                             BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof (LSL_Types.list)) // ref type, copy
                {
                    string tmp = "";
                    string cur = "";
                    LSL_Types.list v = (LSL_Types.list) field.GetValue(this);
                    foreach (object o in v.Data)
                    {
						if (o is LSL_Types.LSLInteger)
							cur = "i" + o;
						else if (o is LSL_Types.LSLFloat)
							cur = "f" + o;
						else if (o is LSL_Types.Vector3)
							cur = "v" + o;
						else if (o is LSL_Types.Quaternion)
							cur = "q" + o;
						else if (o is LSL_Types.LSLString)
							cur = "\"" + EscapeString(o.ToString()) + "\"";
						else if (o.GetType() == typeof(LSL_Types.list))
							cur = "{" + ListToString(o) + "}";

                        if (tmp == "")
                            tmp = cur;
                        else
                            tmp += ", " + cur;
                    }
                    vars[field.Name] = tmp;
                }
                else if (field.FieldType == typeof (LSL_Types.LSLInteger) ||
                         field.FieldType == typeof (LSL_Types.LSLString) ||
                         field.FieldType == typeof (LSL_Types.LSLFloat) ||
                         field.FieldType == typeof (int) ||
                         field.FieldType == typeof (double) ||
                         field.FieldType == typeof (float) ||
                         field.FieldType == typeof (string) ||
                         field.FieldType == typeof (byte) ||
                         field.FieldType == typeof (short) ||
                         field.FieldType == typeof (LSL_Types.Vector3) ||
                         field.FieldType == typeof (LSL_Types.Quaternion))
                {
                    vars[field.Name] = field.GetValue(this).ToString();
                }
            }
            fields = null;
            t = null;

            return vars;
        }

        public LSL_Types.list ParseValueToList(string inval, int start, out int end)
        {
            LSL_Types.list v = new LSL_Types.list();
            end = -1;
            char c;
            string tr = ",}";
            char[] charany = tr.ToCharArray();
            string param = "";
            int totlen = inval.Length;
            int len;
            DateTime time_limit = DateTime.Now.AddSeconds(5.0);

            while (true)
            {
                try
                {
                    if (inval.Length == 0)
                        v.Add(new LSL_Types.LSLString(""));
                    else
                    {
                        c = inval[start++];
                        switch (c)
                        {
                            case 'i':
                                end = inval.IndexOfAny(charany, start);
                                if (end > 0)
                                    len = end - start;
                                else
                                    len = totlen - start;
                                param = inval.Substring(start, len);
                                v.Add(new LSL_Types.LSLInteger(param));
                                break;
                            case 'f':
                                end = inval.IndexOfAny(charany, start);
                                if (end > 0)
                                    len = end - start;
                                else
                                    len = totlen - start;
                                param = inval.Substring(start, len);
                                v.Add(new LSL_Types.LSLFloat(param));
                                break;
                            case 'v':
                                end = inval.IndexOf('>', start);
                                if (end > 0)
                                    len = end - start;
                                else
                                    len = totlen - start;
                                param = inval.Substring(start, len);
                                v.Add(new LSL_Types.Vector3(param));
                                end++;
                                break;
                            case 'q':
                                end = inval.IndexOf('>', start);
                                if (end > 0)
                                    len = end - start;
                                else
                                    len = totlen - start;
                                param = inval.Substring(start, len);
                                v.Add(new LSL_Types.Quaternion(param));
                                end++;
                                break;
                            case '"':
                                end = inval.IndexOf('"', start);
                                while (inval[end - 1] == '\\')
                                {
                                    int slashes = 2;
                                    while (end - slashes > 0 && inval[end - slashes] == '\\')
                                        slashes++;
                                    if ((slashes - 1)%2 == 0)
                                        end = inval.IndexOf('"', end + 1);
                                    else
                                        break;
                                }
                                if (end > 0)
                                    len = end - start;
                                else
                                    len = totlen - start;
                                param = UnEscapeString(inval.Substring(start, len));
                                v.Add(new LSL_Types.LSLString(param));
                                end++;
                                break;
                            case '{':
                                v.Add(ParseValueToList(inval, start, out end));
                                end++;
                                break;

                            default:
                                break;
                        }
                    }
                    start = end;
                    if (start == -1 || start >= totlen || (inval[start] == '}'))
                        break;
                    else
                        while ((inval[start] == ',' || inval[start] == ' ') && DateTime.Now.CompareTo(time_limit) < 0)
                            start++;
                    if (DateTime.Now.CompareTo(time_limit) > 0)
                        break;
                }
                catch
                {
                }
            }
            return v;
        }

        public void SetStoreVars(Dictionary<string, object> vars)
        {
            if (!m_useStateSaves)
                return;
            m_lastStateSaveValues = vars;
            m_stateSaveRequired = false;
            //If something is setting the vars, we don't need to do a state save, as this came from a state save 
            Type t = GetType();

            FieldInfo[] fields = t.GetFields(BindingFlags.NonPublic |
                                             BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (vars.ContainsKey(field.Name))
                {
                    object var = vars[field.Name];
                    if (field.FieldType == typeof (LSL_Types.list))
                    {
                        string val = var.ToString();
                        int end;
                        LSL_Types.list v = ParseValueToList(val, 0, out end);
                        field.SetValue(this, v);
                    }
                    else if (field.FieldType == typeof (LSL_Types.LSLInteger))
                    {
                        int val = int.Parse(var.ToString());
                        field.SetValue(this, new LSL_Types.LSLInteger(val));
                    }
                    else if (field.FieldType == typeof (LSL_Types.LSLString))
                    {
                        string val = var.ToString();
                        field.SetValue(this, new LSL_Types.LSLString(val));
                    }
                    else if (field.FieldType == typeof (LSL_Types.LSLFloat))
                    {
                        float val = float.Parse(var.ToString());
                        field.SetValue(this, new LSL_Types.LSLFloat(val));
                    }
                    else if (field.FieldType == typeof (int))
                    {
                        int val = int.Parse(var.ToString());
                        field.SetValue(this, val);
                    }
                    else if (field.FieldType == typeof (double))
                    {
                        double val = double.Parse(var.ToString());
                        field.SetValue(this, val);
                    }
                    else if (field.FieldType == typeof (float))
                    {
                        float val = float.Parse(var.ToString());
                        field.SetValue(this, val);
                    }
                    else if (field.FieldType == typeof (string))
                    {
                        string val = var.ToString();
                        field.SetValue(this, val);
                    }
                    else if (field.FieldType == typeof (byte))
                    {
                        byte val = byte.Parse(var.ToString());
                        field.SetValue(this, val);
                    }
                    else if (field.FieldType == typeof (short))
                    {
                        short val = short.Parse(var.ToString());
                        field.SetValue(this, val);
                    }
                    else if (field.FieldType == typeof (LSL_Types.Quaternion))
                    {
                        LSL_Types.Quaternion val = new LSL_Types.Quaternion(var.ToString());
                        field.SetValue(this, val);
                    }
                    else if (field.FieldType == typeof (LSL_Types.Vector3))
                    {
                        LSL_Types.Vector3 val = new LSL_Types.Vector3(var.ToString());
                        field.SetValue(this, val);
                    }
                }
            }
            fields = null;
            t = null;
        }

        public void ResetVars()
        {
            if (!m_useStateSaves)
                return;
            m_Executor.ResetStateEventFlags();
            m_stateSaveRequired = true;
            SetVars(m_InitialValues);
        }

        public void NoOp()
        {
            // Does what is says on the packet. Nowt, nada, nothing.
            // Required for insertion after a jump label to do what it says on the packet!
            // With a bit of luck the compiler may even optimize it out.
        }

        public string Name
        {
            get { return "ScriptBase"; }
        }

        public void Dispose()
        {
        	GC.SuppressFinalize(this);
        }

        Type m_typeCache; //This shouldn't normally be used

        public virtual IEnumerator FireEvent(string evName, object[] parameters)
        {
            if (m_typeCache == null)
                m_typeCache = GetType();
            MethodInfo ev = m_typeCache.GetMethod(evName);
            if (ev != null)
                if (ev.ReturnType == typeof (IEnumerator))
                    return (IEnumerator) ev.Invoke(this, parameters);
                else
                    ev.Invoke(this, parameters);

            return null;
        }
    }
}
