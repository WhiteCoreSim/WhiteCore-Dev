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

using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CSharp;

//using Microsoft.JScript;

namespace WhiteCore.ScriptEngine.DotNetEngine.CompilerTools
{
    public class LSLConverter : IScriptConverter
    {
        readonly CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();
        CSCodeGenerator LSL_Converter;
        Compiler m_compiler;

        #region IScriptConverter Members

        public string DefaultState
        {
            get { return "default"; }
        }

        public void Initialise(Compiler compiler)
        {
            m_compiler = compiler;
            //? // new CSCodeGenerator(compiler);

            //Add new LSL events that haven't been added into the parser
            LSL2CSCodeTransformer.AddLSLEvent(new EventInfo("experience_permissions", new [] {
                "LSL_Types.LSLString"}));
            LSL2CSCodeTransformer.AddLSLEvent(new EventInfo("experience_permissions_denied", new [] {
				"LSL_Types.LSLString", "LSL_Types.LSLInteger" }));
            LSL2CSCodeTransformer.AddLSLEvent(new EventInfo("path_update", new [] {
                "LSL_Types.LSLInteger", "LSL_Types.list" }));
            LSL2CSCodeTransformer.AddLSLEvent(new EventInfo("transaction_result", new [] {
                "LSL_Types.LSLString", "LSL_Types.LSLInteger", "LSL_Types.LSLString" }));
        }

        public void Convert(string Script, out string CompiledScript,
                            out object PositionMap)
        {
            // Its LSL, convert it to C#
            LSL_Converter = new CSCodeGenerator(m_compiler);
            CompiledScript = LSL_Converter.Convert(Script);
            PositionMap = LSL_Converter.PositionMap;
        }

        public string Name
        {
            get { return "lsl"; }
        }

        public CompilerResults Compile(CompilerParameters parameters, bool isFile, string Script)
        {
            bool complete = false;
            bool retried = false;
            CompilerResults results;
            do
            {
                lock (CScodeProvider)
                {
                    if (isFile)
                        results = CScodeProvider.CompileAssemblyFromFile(
                            parameters, Script);
                    else
                        results = CScodeProvider.CompileAssemblyFromSource(
                            parameters, Script);
                }
                // Deal with an occasional segv in the compiler.
                // Rarely, if ever, occurs twice in succession.
                // Line # == 0 and no file name are indications that
                // this is a native stack trace rather than a normal
                // error log.
                if (results.Errors.Count > 0)
                {
                    if (!retried && string.IsNullOrEmpty(results.Errors[0].FileName) &&
                        results.Errors[0].Line == 0)
                    {
                        // System.Console.WriteLine("retrying failed compilation");
                        retried = true;
                    }
                    else
                    {
                        complete = true;
                    }
                }
                else
                {
                    complete = true;
                }
            } while (!complete);
            return results;
        }

        public void FinishCompile(IScriptModulePlugin plugin, ScriptData data, IScript Script)
        {
        }

        public void FindErrorLine(CompilerError CompErr, object map, string script, out int LineN, out int CharN)
        {
            Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> PositionMap =
                (Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>) map;
            string text = ReplaceTypes(CompErr.ErrorText);
            text = CleanError(text);
            KeyValuePair<int, int> lslPos = FindErrorPosition(CompErr.Line, CompErr.Column, PositionMap);
            LineN = lslPos.Key - 1;
            CharN = lslPos.Value - 1;
            if (LineN <= 0 && CharN != 0)
            {
                string[] lines = script.Split('\n');
                int charCntr = 0;
                int lineCntr = 0;
                foreach (string line in lines)
                {
                    if (charCntr + line.Length > CharN)
                    {
                        //Its in this line
                        CharN -= charCntr;
                        LineN = lineCntr;
                        break;
                    }
                    charCntr += line.Length - 1;
                    lineCntr++;
                }
            }
        }

        public static KeyValuePair<int, int> FindErrorPosition(int line,
                                                               int col, Dictionary<KeyValuePair<int, int>,
                                                                            KeyValuePair<int, int>> positionMap)
        {
            if (positionMap == null || positionMap.Count == 0)
                return new KeyValuePair<int, int>(line, col);

            KeyValuePair<int, int> ret = new KeyValuePair<int, int>();

            if (positionMap.TryGetValue(new KeyValuePair<int, int>(line, col),
                                        out ret))
                return ret;

            List<KeyValuePair<int, int>> sorted =
                new List<KeyValuePair<int, int>>(positionMap.Keys);

            sorted.Sort(new kvpSorter());

            int l = 1;
            int c = 1;

            foreach (KeyValuePair<int, int> cspos in sorted)
            {
                if (cspos.Key >= line)
                {
                    if (cspos.Key > line)
                        return new KeyValuePair<int, int>(l, c);
                    if (cspos.Value > col)
                        return new KeyValuePair<int, int>(l, c);
                    c = cspos.Value;
                    if (c == 0)
                        c++;
                }
                else
                {
                    l = cspos.Key;
                }
            }
            return new KeyValuePair<int, int>(l, c);
        }

        private string ReplaceTypes(string message)
        {
            message = message.Replace(
                "WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString",
                "string");

            message = message.Replace(
                "WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger",
                "integer");

            message = message.Replace(
                "WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat",
                "float");

            message = message.Replace(
                "WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.list",
                "list");

            return message;
        }

        private string CleanError(string message)
        {
            //Remove these long strings
            message = message.Replace(
                "WhiteCore.ScriptEngine.DotNetEngine.Runtime.ScriptBaseClass.",
                "");
            message = message.Replace(
                "WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.",
                "");
            if (message.Contains("The best overloaded method match for"))
            {
                string[] messageSplit = message.Split('\'');
                string Function = messageSplit[1];
                string[] FunctionSplit = Function.Split('(');
                string FunctionName = FunctionSplit[0];
                string Arguments = FunctionSplit[1].Split(')')[0];
                message = "Incorrect argument in " + FunctionName + ", arguments should be " + Arguments + "\n";
            }
            if (message == "Unexpected EOF")
            {
                message = "Missing one or more }." + "\n";
            }
            return message;
        }

        private class kvpSorter : IComparer<KeyValuePair<int, int>>
        {
            #region IComparer<KeyValuePair<int,int>> Members

            public int Compare(KeyValuePair<int, int> a,
                               KeyValuePair<int, int> b)
            {
                return a.Key.CompareTo(b.Key);
            }

            #endregion
        }

        #endregion

        public void Dispose()
        {
        }
    }
}