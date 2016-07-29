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
using System.Text;
using ScriptEngineParser;
using Tools;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.ScriptEngine.DotNetEngine.CompilerTools
{
    public static class Extension
    {
        public static bool CompareWildcards(this string WildString, string Mask, bool IgnoreCase)
        {
            int i = 0;

            if (string.IsNullOrEmpty(Mask))
                return false;
            if (Mask == "*")
                return true;

            while (i != Mask.Length)
            {
                if (CompareWildcard(WildString, Mask.Substring(i), IgnoreCase))
                    return true;

                while (i != Mask.Length && Mask[i] != ';')
                    i += 1;

                if (i != Mask.Length && Mask[i] == ';')
                {
                    i += 1;

                    while (i != Mask.Length && Mask[i] == ' ')
                        i += 1;
                }
            }

            return false;
        }

        public static bool CompareWildcard(this string WildString, string Mask, bool IgnoreCase)
        {
            int i = 0, k = 0;

            while (k != WildString.Length)
            {
                if ((i + 1) > Mask.Length)
                    return true;
                switch (Mask[i])
                {
                    case '*':

                        if ((i + 1) >= Mask.Length)
                            return true;

                        while (k != WildString.Length)
                        {
                            if (CompareWildcard(WildString.Substring(k + 1), Mask.Substring(i + 1), IgnoreCase))
                                return true;

                            k += 1;
                        }

                        return false;

                    case '?':

                        break;

                    default:

                        if (IgnoreCase == false && WildString[k] != Mask[i])
                            return false;
                        if (IgnoreCase && char.ToLower(WildString[k]) != char.ToLower(Mask[i]))
                            return false;

                        break;
                }

                i += 1;
                k += 1;
            }

            if (k == WildString.Length)
            {
                if (i == Mask.Length || Mask[i] == ';' || Mask[i] == '*')
                    return true;
            }

            return false;
        }
    }

    public sealed class CSCodeGenerator : IDisposable
    {
        readonly List<string> AfterFuncCalls = new List<string>();
        readonly HashSet<string> DTFunctions = new HashSet<string>();
        readonly List<string> FuncCalls = new List<string>();
        //        public Dictionary<string, string> IenFunctions = new Dictionary<string, string>();
        readonly Dictionary<string, GlobalVar> GlobalVariables = new Dictionary<string, GlobalVar>();
        Dictionary<string, SYMBOL> DuplicatedGlobalVariables = new Dictionary<string, SYMBOL>();

        Dictionary<string, Dictionary<string, SYMBOL>> DuplicatedLocalVariables =
            new Dictionary<string, Dictionary<string, SYMBOL>>();

        /// <summary>
        ///     This saves the variables in methods so that we can make sure multiple variables do not have the same name, and if they do, rename/assign them to the correct variable name
        /// </summary>
        readonly Dictionary<string, GlobalVar> MethodVariables = new Dictionary<string, GlobalVar>();

        readonly List<string> MethodsToAdd = new List<string>();

        /// <summary>
        ///     This contains a list of variables that we need to rename because of some constraint
        /// </summary>
        readonly Dictionary<string, VarRename> VariablesToRename = new Dictionary<string, VarRename>();

        readonly Dictionary<string, List<ArgumentDeclarationList>> m_allMethods =
            new Dictionary<string, List<ArgumentDeclarationList>>();

        /// <summary>
        ///     Param 1 - the API function name, Param 2 - the API name
        /// </summary>
        static Dictionary<string, IScriptApi> m_apiFunctions = null;

        readonly Compiler m_compiler;

        bool FuncCntr;
        bool IsParentEnumerable;
        bool IsaGlobalVar;
        string OriginalScript = "";
        bool isAdditionExpression;
        int m_CSharpCol; // the current column of generated C# code
        int m_CSharpLine; // the current line of generated C# code
        int m_braceCount; // for indentation
        int m_indentWidth = 4; // for indentation
        bool m_isInEnumeratedDeclaration;
        Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> m_positionMap;
        Parser p;
        static yyLSLSyntax _LSLSyntax = new yyLSLSyntax();

        public Dictionary<string, ObjectList> LocalMethodArguements = new Dictionary<string, ObjectList> ();
        public Dictionary<string, string> LocalMethods = new Dictionary<string, string> ();

        /// <summary>
        ///     Creates an 'empty' CSCodeGenerator instance.
        /// </summary>
        public CSCodeGenerator(Compiler compiler)
        {
            #region DTFunctions

            // api funtions that can return a time delay only
            // must be type DateTime in stub files and implementation
            DTFunctions.Add("llAddToLandBanList");
            DTFunctions.Add("llAddToLandPassList");
            DTFunctions.Add("llAdjustSoundVolume");
            DTFunctions.Add("llCloseRemoteDataChannel");
            DTFunctions.Add("llCreateLink");
            DTFunctions.Add("llDialog");
            DTFunctions.Add("llEjectFromLand");
            DTFunctions.Add("llEmail");
            DTFunctions.Add("llGiveInventory");
            DTFunctions.Add("llInstantMessage");
            DTFunctions.Add("llLoadURL");
            DTFunctions.Add("llMakeExplosion");
            DTFunctions.Add("llMakeFire");
            DTFunctions.Add("llMakeFountain");
            DTFunctions.Add("llMakeSmoke");
            DTFunctions.Add("llMapDestination");
            DTFunctions.Add("llOffsetTexture");
            DTFunctions.Add("llOpenRemoteDataChannel");
            DTFunctions.Add("llParcelMediaCommandList");
            DTFunctions.Add("llPreloadSound");
            DTFunctions.Add("llRefreshPrimURL");
            DTFunctions.Add("llRemoteDataReply");
            DTFunctions.Add("llRemoteLoadScript");
            DTFunctions.Add("llRemoteLoadScriptPin");
            DTFunctions.Add("llRemoveFromLandBanList");
            DTFunctions.Add("llRemoveFromLandPassList");
            DTFunctions.Add("llResetLandBanList");
            DTFunctions.Add("llResetLandPassList");
            DTFunctions.Add("llRezAtRoot");
            DTFunctions.Add("llRezObject");
            DTFunctions.Add("llRotateTexture");
            DTFunctions.Add("llScaleTexture");
            DTFunctions.Add("llSetLinkTexture");
            DTFunctions.Add("llSetLocalRot");
            DTFunctions.Add("llSetParcelMusicURL");
            DTFunctions.Add("llSetPos");
            DTFunctions.Add("llSetPrimURL");
            DTFunctions.Add("llSetRot");
            DTFunctions.Add("llSetTexture");
            DTFunctions.Add("llSleep");
            DTFunctions.Add("llTeleportAgentHome");
            DTFunctions.Add("llTextBox");
            DTFunctions.Add("osTeleportAgent");
            DTFunctions.Add("osTeleportOwner");


            /* suspended:  some API functions are not compatible with IEnum
                        // Api functions that can return a delay, a value or breakable in timeslices
                        // must be IEnumerator in stub, interface and implementation files           
 
                        IenFunctions.Add("llRequestAgentData", "LSL_Types.LSLString");
                        IenFunctions.Add("llRequestInventoryData", "LSL_Types.LSLString");
                        IenFunctions.Add("llSendRemoteData", "LSL_Types.LSLString");
                        IenFunctions.Add("llXorBase64Strings", "LSL_Types.LSLString");
                        IenFunctions.Add("llRequestSimulatorData", "LSL_Types.LSLString");
                        IenFunctions.Add("llParcelMediaQuery", "LSL_Types.list");
                        IenFunctions.Add("llGetPrimMediaParams", "LSL_Types.list");
                        IenFunctions.Add("llSetPrimMediaParams", "LSL_Types.LSLInteger");
                        IenFunctions.Add("llClearPrimMedia", "LSL_Types.LSLInteger");
                        IenFunctions.Add("llModPow", "LSL_Types.LSLInteger");
                        IenFunctions.Add("llGetNumberOfNotecardLines", "LSL_Types.LSLString");
                        IenFunctions.Add("llGetParcelPrimOwners", "LSL_Types.list");
                        IenFunctions.Add("llGetNotecardLine", "LSL_Types.LSLString");
            */


            // the rest will be directly called

            #endregion

            ResetCounters();
            m_compiler = compiler;

            if(m_apiFunctions == null)
                m_apiFunctions = compiler.ScriptEngine.GetAllFunctionNamesAPIs();
        }

        /// <summary>
        ///     Get the mapping between LSL and C# line/column number.
        /// </summary>
        /// <returns>
        ///     Dictionary&lt;KeyValuePair&lt;int, int&gt;, KeyValuePair&lt;int, int&gt;&gt;
        /// </returns>
        public Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> PositionMap
        {
            get { return m_positionMap; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            ResetCounters();
        }

        #endregion

        /// <summary>
        ///     Resets various counters and metadata.
        /// </summary>
        void ResetCounters()
        {
            //NOTE: This takes a VERY long time to rebuild. Ideally, this should be reset, but interesting errors are happening when it is reset..
            p = new LSLSyntax(_LSLSyntax, new ErrorHandler(true));
            MethodVariables.Clear();
            VariablesToRename.Clear();
            GlobalVariables.Clear();
            m_braceCount = 0;
            m_CSharpLine = 15;
            m_CSharpCol = 1;
            m_positionMap = new Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>();
            LocalMethods.Clear();
            IsParentEnumerable = false;
            OriginalScript = "";
            lock (FuncCalls)
                FuncCalls.Clear();
            FuncCntr = false;
            IsaGlobalVar = false;
            MethodsToAdd.Clear();
            LocalMethodArguements.Clear();
            m_allMethods.Clear();
        }

        public static int GetHeaderCount(Compiler compiler)
        {
            int i = 0;
            foreach (IScriptApi api in compiler.ScriptEngine.GetAPIs())
                foreach (string nameSpace in api.NamespaceAdditions)
                    i++;
            return 13 + i;
        }

        public static string CreateCompilerScript(Compiler compiler,
                                                  List<string> MethodsToAdd, string ScriptClass)
        {
            StringBuilder sb = new StringBuilder();
            foreach (IScriptApi api in compiler.ScriptEngine.GetAPIs())
                foreach (string nameSpace in api.NamespaceAdditions)
                    sb.AppendFormat("using {0};\n", nameSpace);
            sb.AppendLine(
@"using LSL_Types = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types;
using System;
namespace Script
{
[Serializable]
public class ScriptClass : WhiteCore.ScriptEngine.DotNetEngine.Runtime.ScriptBaseClass
{");

            sb.AppendLine(ScriptClass);
            sb.AppendLine(string.Join("\n", MethodsToAdd));

            sb.AppendLine("}}");

            return sb.ToString();
        }

        /// <summary>
        ///     Generate the code from the AST we have.
        /// </summary>
        /// <param name="script">The LSL source as a string.</param>
        /// <returns>String containing the generated C# code.</returns>
        public string Convert(string script)
        {
            //Unless we are using the same LSL_Converter instance for all scripts, we don't need to reset this
            //ResetCounters();

            LSL2CSCodeTransformer codeTransformer;
            try
            {
                //               lock (p)
                {
                    codeTransformer = new LSL2CSCodeTransformer(p.Parse(FixAdditionalEvents(script)), script);
                    //                    p.m_lexer.Reset();
                }
            }
            catch (CSToolsException e)
            {
                string message;

                // LL start numbering lines at 0 - geeks!
                // Also need to subtract one line we prepend!
                //
                string emessage = e.Message;
                string slinfo = e.slInfo.ToString();

                // Remove wrong line number info
                //
                if (emessage.StartsWith (slinfo + ": ", StringComparison.Ordinal))
                    emessage = emessage.Substring(slinfo.Length + 2);

                if (e.slInfo.lineNumber - 1 <= 0)
                    e.slInfo.lineNumber = 2;
                if (e.slInfo.charPosition - 1 <= 0)
                    e.slInfo.charPosition = 2;

                message = string.Format("({0},{1}) {2}",
                                        e.slInfo.lineNumber - 1,
                                        e.slInfo.charPosition - 1, emessage);

                m_compiler.AddError(message);
                //                p.m_lexer.Reset();
                ResetCounters();
                return "Error parsing the script. " + message;
            }

            SYMBOL root = codeTransformer.Transform(LocalMethods, LocalMethodArguements);
            DuplicatedGlobalVariables = codeTransformer.DuplicatedGlobalVars;
            DuplicatedLocalVariables = codeTransformer.DuplicatedLocalVars;
            OriginalScript = script;
            StringBuilder retVal = new StringBuilder();

            // line number
            //m_CSharpLine += 3;

            // here's the payload
            retVal.Append(GenerateLine());
            foreach (SYMBOL s in root.kids)
                retVal.Append(GenerateNode(s));

            retVal.Append(GenerateFireEventMethod());

            // Removes all carriage return characters which may be generated in Windows platform. 
            //Is there a cleaner way of doing this?
            string returnstring = retVal.ToString().Replace("\r", "");

            try
            {
                CheckEventCasts(returnstring);
            }
            catch (InvalidOperationException ex)
            {
                m_compiler.AddError(ex.Message);
                return ex.Message;
            }
            return CreateCompilerScript(m_compiler, MethodsToAdd, returnstring);
        }

        string FixAdditionalEvents(string script)
        {
            string retVal = script;
            foreach(EventInfo ev in LSL2CSCodeTransformer.GetNewLSLEvents())
                retVal = retVal.Replace(ev.Name, "remote_data");
            return retVal;
        }

        string GenerateFireEventMethod()
        {
            StringBuilder retVal = new StringBuilder();
            retVal.AppendLine(
                "public override System.Collections.IEnumerator FireEvent (string evName, object[] parameters)");
            retVal.AppendLine("{");
            foreach (KeyValuePair<string, List<ArgumentDeclarationList>> method in m_allMethods)
            {
                retVal.AppendLine("if(evName == \"" + method.Key + "\")"); // if(evName == "state_entry")
                retVal.Append("return " + method.Key + " (");
                foreach (ArgumentDeclarationList t in method.Value)
                {
                    for (int b = 0; b < t.kids.Count; b++)
                    {
                        retVal.Append("(" + ((Declaration) t.kids[b]).Datatype + ")");
                        retVal.Append("parameters[" + b + "]");
                        if (b != t.kids.Count - 1)
                            retVal.Append(", ");
                    }
                }
                retVal.AppendLine(");");
            }
            retVal.AppendLine("return null;");
            retVal.AppendLine("}");
            return retVal.ToString();
        }

        string CheckFloatExponent(string script)
        {
            string[] SplitScript = script.Split('\n');
            List<string> ReconstructableScript = new List<string>();

            foreach (string line in SplitScript)
            {
                string AddLine = line;
                if (AddLine.Replace(" ", "") == "default")
                    break; //We only check above default

                if (AddLine.Contains("float"))
                {
                    if (line.Contains("e") && !line.Contains("(") && !line.Contains(")"))
                        // Looking for exponents, but not for functions that have ()
                    {
                        //Should have this - float *** = 151e9;
                        string[] SplitBeforeE = AddLine.Split('='); // Split at the e so we can look at the syntax
                        if (SplitBeforeE.Length != 1)
                        {
                            string[] SplitBeforeESpace = SplitBeforeE[1].Split(';');
                            //Should have something like this now - 151
                            if (SplitBeforeESpace[0].Contains("e"))
                            {
                                if (!SplitBeforeESpace[0].Contains("."))
                                {
                                    //Needs one then
                                    string[] Split = SplitBeforeESpace[0].Split('e');
                                    // Split at the e so we can look at the syntax
                                    Split[0] += ".";
                                    string TempString = "";
                                    foreach (string tempLine in Split)
                                    {
                                        TempString += tempLine + "e";
                                    }
                                    TempString = TempString.Remove(TempString.Length - 1, 1);
                                    SplitBeforeESpace[0] = TempString;
                                    TempString = "";
                                    foreach (string tempLine in SplitBeforeESpace)
                                    {
                                        TempString += tempLine + ";";
                                    }
                                    //Remove the last ;
                                    TempString = TempString.Remove(TempString.Length - 1, 1);
                                    SplitBeforeE[1] = TempString;
                                    foreach (string tempLine in SplitBeforeE)
                                    {
                                        AddLine += tempLine + "=";
                                    }
                                    //Remove the last e
                                    AddLine = AddLine.Remove(AddLine.Length - 1, 1);
                                }
                            }
                        }
                    }
                }
                ReconstructableScript.Add(AddLine);
            }
            string RetVal = "";
            foreach (string line in ReconstructableScript)
            {
                RetVal += line + "\n";
            }
            return RetVal;
        }

        string CheckForInlineVectors(string script)
        {
            string[] SplitScript = script.Split('\n');
            List<string> ReconstructableScript = new List<string>();
            foreach (string line in SplitScript)
            {
                string AddLine = line;
                if (AddLine.Contains("<") && AddLine.Contains(">"))
                {
                    if (AddLine.Contains("\"")) // if it contains ", we need to be more careful
                    {
                        string[] SplitByParLine = AddLine.Split('\"');
                        int lineNumber = 0;
                        List<string> ReconstructableLine = new List<string>();
                        foreach (string parline in SplitByParLine)
                        {
                            string AddInsideLine = parline;
                            //throw out all odd numbered lines as they are inside ""
                            if (lineNumber%2 != 1)
                            {
                                string[] SplitLineA = AddLine.Split('<');
                                if (SplitLineA.Length > 1)
                                {
                                    string SplitLineB = SplitLineA[1].Split('>')[0];
                                    if (SplitLineB.CompareWildcard("*,*,*", true))
                                    {
                                        AddInsideLine = AddInsideLine.Replace("<", "(<");
                                        AddInsideLine = AddInsideLine.Replace(">", ">)");
                                    }
                                }
                            }
                            ReconstructableLine.Add(AddInsideLine);
                            lineNumber++;
                        }
                        AddLine = "";
                        foreach (string insideline in ReconstructableLine)
                        {
                            AddLine += insideline + "\"";
                        }
                        AddLine = AddLine.Remove(AddLine.Length - 1, 1);
                    }
                    else
                    {
                        string[] SplitLineA = AddLine.Split('<');
                        if (SplitLineA.Length > 1)
                        {
                            string SplitLineB = SplitLineA[1].Split('>')[0];
                            if (SplitLineB.CompareWildcard("*,*,*", true))
                            {
                                AddLine = AddLine.Replace("<", "(<");
                                AddLine = AddLine.Replace(">", ">)");
                            }
                        }
                    }
                }
                ReconstructableScript.Add(AddLine);
            }
            string RetVal = "";
            foreach (string line in ReconstructableScript)
            {
                RetVal += line + "\n";
            }
            return RetVal;
        }

        /// <summary>
        ///     Checks the C# script for the correct casts in events
        ///     This stops errors from misformed events ex. 'touch(vector3 position)' instead of 'touch(int touch)'
        /// </summary>
        /// <param name="script"></param>
        void CheckEventCasts(string script)
        {
            CheckEvent(script, "default");
            string[] scriptStates = OriginalScript.Split(new string[] {"state "}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string state in scriptStates)
            {
                string stateName = state.Split(' ')[0].Split('\n')[0];
                if (!stateName.Contains("default"))
                    CheckEvent(script, stateName);
            }
        }

        void CheckEvent(string script, string state)
        {
            foreach (EventInfo evInfo in LSL2CSCodeTransformer.GetAllLSLEvents())
            {
                string evName = string.Format("{0}_event_{1}(", state, evInfo.Name);
                if (script.Contains(evName))
                {
                    int charNum = script.IndexOf (evName, StringComparison.Ordinal);
                    string splitScript = script.Remove(0, charNum);
                    charNum = splitScript.IndexOf('\n');
                    splitScript = splitScript.Remove(charNum, splitScript.Length - charNum);

                    string arguments = splitScript.Split('(')[1];
                    arguments = arguments.Split(')')[0];

                    string[] AllArguments = arguments.Split(',');
                    if (evInfo.ArgumentTypes == null)
                    {
                        if (arguments != "")
                            FindWrongParameterCountLineNumbers(evInfo.Name, 0, AllArguments.Length);
                    }
                    else
                    {
                        for (int i = 0; i < AllArguments.Length; i++)
                        {
                            if (!AllArguments[i].Contains(evInfo.ArgumentTypes[i]))
                                FindLineNumbers(evInfo.Name, "Invalid argument");
                        }
                        if (AllArguments.Length != evInfo.ArgumentTypes.Length)
                            FindWrongParameterCountLineNumbers(evInfo.Name, evInfo.ArgumentTypes.Length, AllArguments.Length);
                    }
                }
            }
        }

        void FindWrongParameterCountLineNumbers(string EventName, int correct, int i)
        {
            if (i > correct)
                FindLineNumbers(EventName, "Too many arguments, " + i + " arguments given, " + correct + " expected");
            else
                FindLineNumbers(EventName, "Too few arguments, " + i + " arguments given, " + correct + " expected");
        }

        void FindLineNumbers(string EventName, string Problem)
        {
            //string testScript = OriginalScript.Replace(" ", "");
            int lineNumber = 0;
            int charNumber = 0;
            int i = 0;
            foreach (string str in OriginalScript.Split('\n'))
            {
                if (str.Contains(EventName + "("))
                {
                    lineNumber = i;
                    charNumber = str.IndexOf (EventName, StringComparison.Ordinal);
                    break;
                }
                i++;
            }
            throw new InvalidOperationException(string.Format("({0},{1}) {2}",
                                                              lineNumber,
                                                              charNumber, Problem + " in '" + EventName + "'\n"));
        }

        void AddWarning(string warning)
        {
            m_compiler.AddWarning(warning);
        }

        /// <summary>
        ///     Recursively called to generate each type of node. Will generate this
        ///     node, then all it's children.
        /// </summary>
        /// <param name="s">The current node to generate code for.</param>
        /// <returns>String containing C# code for SYMBOL s.</returns>
        string GenerateNode(SYMBOL s)
        {
            // make sure to put type lower in the inheritance hierarchy first
            // ie: since IdentArgument and ExpressionArgument inherit from
            // Argument, put IdentArgument and ExpressionArgument before Argument
            if (s is GlobalFunctionDefinition)
            {
                return GenerateGlobalFunctionDefinition((GlobalFunctionDefinition) s);
            }
            else if (s is GlobalVariableDeclaration)
            {
                return GenerateGlobalVariableDeclaration((GlobalVariableDeclaration) s);
            }
            else if (s is State)
            {
                return GenerateState((State) s);
            }
            else if (s is CompoundStatement)
            {
                return GenerateCompoundStatement((CompoundStatement) s);
            }
            else if (s is Declaration)
            {
                return GenerateDeclaration((Declaration) s);
            }
            else if (s is Statement)
            {
                return GenerateStatement((Statement) s);
            }
            else if (s is ReturnStatement)
            {
                return GenerateReturnStatement((ReturnStatement) s);
            }
            else if (s is JumpLabel)
            {
                return GenerateJumpLabel((JumpLabel) s);
            }
            else if (s is JumpStatement)
            {
                return GenerateJumpStatement((JumpStatement) s);
            }
            else if (s is StateChange)
            {
                return GenerateStateChange((StateChange) s);
            }
            else if (s is IfStatement)
            {
                return GenerateIfStatement((IfStatement) s);
            }
            else if (s is WhileStatement)
            {
                return GenerateWhileStatement((WhileStatement) s);
            }
            else if (s is DoWhileStatement)
            {
                return GenerateDoWhileStatement((DoWhileStatement) s);
            }
            else if (s is ForLoop)
            {
                return GenerateForLoop((ForLoop) s);
            }
            else if (s is ArgumentList)
            {
                return GenerateArgumentList((ArgumentList) s);
            }
            else if (s is Assignment)
            {
                return GenerateAssignment((Assignment) s);
            }
            else if (s is BinaryExpression)
            {
                return GenerateBinaryExpression((BinaryExpression) s, false, "");
            }
            else if (s is ParenthesisExpression)
            {
                return GenerateParenthesisExpression((ParenthesisExpression) s);
            }
            else if (s is UnaryExpression)
            {
                return GenerateUnaryExpression((UnaryExpression) s);
            }
            else if (s is IncrementDecrementExpression)
            {
                return GenerateIncrementDecrementExpression((IncrementDecrementExpression) s);
            }
            else if (s is TypecastExpression)
            {
                return GenerateTypecastExpression((TypecastExpression) s);
            }
            else if (s is FunctionCall)
            {
                return GenerateFunctionCall((FunctionCall) s, true);
            }
            else if (s is VectorConstant)
            {
                return GenerateVectorConstant((VectorConstant) s);
            }
            else if (s is RotationConstant)
            {
                return GenerateRotationConstant((RotationConstant) s);
            }
            else if (s is ListConstant)
            {
                return GenerateListConstant((ListConstant)s);
            }
            else if (s is Constant)
            {
                return GenerateConstant((Constant)s);
            }
            else if (s is IdentDotExpression)
            {
                return Generate(CheckName(((IdentDotExpression) s).Name) + "." + ((IdentDotExpression) s).Member, s);
            }
            else if (s is IdentExpression)
            {
                return Generate(CheckName(((IdentExpression)s).Name), s);
            }
            else if (s is IDENT)
            {
                return Generate(CheckName(((TOKEN) s).yytext), s);
            }
            else
            {
                StringBuilder retVal = new StringBuilder();
                foreach (SYMBOL kid in s.kids)
                    retVal.Append(GenerateNode(kid));
                return retVal.ToString();
            }
        }

        GlobalFunctionDefinition _currentGlobalFunctionDeclaration = null;
        StateEvent _currentLocalFunctionDeclaration = null;
        State _currentLocalStateDeclaration = null;

        /// <summary>
        ///     Generates the code for a GlobalFunctionDefinition node.
        /// </summary>
        /// <param name="gf">The GlobalFunctionDefinition node.</param>
        /// <returns>String containing C# code for GlobalFunctionDefinition gf.</returns>
        string GenerateGlobalFunctionDefinition(GlobalFunctionDefinition gf)
        {
            MethodVariables.Clear();
            VariablesToRename.Clear();
            StringBuilder retstr = new StringBuilder();
            _currentGlobalFunctionDeclaration = gf;

            // we need to separate the argument declaration list from other kids
            List<SYMBOL> argumentDeclarationListKids = new List<SYMBOL>();
            List<SYMBOL> remainingKids = new List<SYMBOL>();


            foreach (SYMBOL kid in gf.kids)
                if (kid is ArgumentDeclarationList)
                    argumentDeclarationListKids.Add(kid);
                else
                    remainingKids.Add(kid);

            retstr.Append(
                GenerateIndented(string.Format("public System.Collections.IEnumerator {0}(", CheckName(gf.Name)), gf));

            IsParentEnumerable = true;

            // print the state arguments, if any
            List<ArgumentDeclarationList> args = new List<ArgumentDeclarationList>();
            foreach (SYMBOL kid in argumentDeclarationListKids)
            {
                ArgumentDeclarationList ADL = (ArgumentDeclarationList) kid;
                args.Add(ADL);
                retstr.Append(GenerateArgumentDeclarationList(ADL));
            }
            m_allMethods.Add(CheckName(gf.Name), args);

            retstr.Append(GenerateLine(")"));


            foreach (SYMBOL kid in remainingKids)
                retstr.Append(GenerateNode(kid));

            if (gf.ReturnType == "void")
            {
                int i;
                for (i = 1; i < 5; i++)
                {
                    if (retstr[retstr.Length - i] == '}')
                    {
                        retstr.Insert(retstr.Length - i, GenerateLine(" yield break;"));
                        break;
                    }
                }
            }

            IsParentEnumerable = false;
            _currentGlobalFunctionDeclaration = null;
            return retstr.ToString();
        }

        /// <summary>
        ///     Generates the code for a GlobalVariableDeclaration node.
        /// </summary>
        /// <param name="gv">The GlobalVariableDeclaration node.</param>
        /// <returns>String containing C# code for GlobalVariableDeclaration gv.</returns>
        string GenerateGlobalVariableDeclaration(GlobalVariableDeclaration gv)
        {
			// ## TO DO ##  
			// Does this do anything as some of the assignments are never used??
			StringBuilder retVal = new StringBuilder();

            foreach (SYMBOL s in gv.kids)
            {
                retVal.Append(Indent());
                retVal.Append("public ");
                if (s is Assignment)
                {
                    Assignment a = s as Assignment;
                    List<string> identifiers = new List<string>();

                    checkForMultipleAssignments(identifiers, a);

                    IsaGlobalVar = true;
                    SYMBOL variableName = (SYMBOL) a.kids.Pop();
                    string VarName = GenerateNode(variableName);
                    retVal.Append(VarName);
                    IsaGlobalVar = false;

                    #region Find the var name and type

                    Declaration dec = variableName as Declaration;
                    string type = dec.Datatype;
                    string varName = dec.Id;

                    #endregion

                    if (DuplicatedGlobalVariables.ContainsKey(((Declaration) variableName).Id))
                    {
                        if (a.kids.Count == 1)
                        {
                            SYMBOL assignmentChild = (SYMBOL) a.kids[0];
                            if (assignmentChild is IdentExpression)
                            {
// 20131224 not used                                IdentExpression identEx = (IdentExpression) assignmentChild;
                            }
                            else if (assignmentChild is ListConstant)
                            {
                                ListConstant listConst = (ListConstant) assignmentChild;
                                foreach (SYMBOL listChild in listConst.kids)
                                {
                                    if (listChild is ArgumentList)
                                    {
                                        ArgumentList argList = (ArgumentList) listChild;
                                        int i = 0;
                                        //bool changed = false;
                                        object[] pObj = new object[argList.kids.Count];
                                        foreach (SYMBOL objChild in argList.kids)
                                        {
                                            pObj[i] = objChild;
                                            if (objChild is IdentExpression)
                                            {
// 20131224 not used                                                IdentExpression identEx = (IdentExpression) objChild;
                                            }
                                            i++;
                                        }
                                        // TODO: 20160607 -greythane- This is never executed, check implmentation
                                        /*if (changed)
                                        {
                                            argList.kids = new ObjectList();
                                            foreach (object o in pObj)
                                                argList.kids.Add(o);
                                        }
                                        */
                                    }
                                }
                            }
                            else if (assignmentChild is Constant)
                            {
                                Constant identEx = (Constant) assignmentChild;
                                string value = GetValue(identEx);
                                Constant dupConstant = (Constant) DuplicatedGlobalVariables[dec.Id];
                                dupConstant.Value = dupConstant.Value == null
                                                        ? GetValue(dupConstant)
                                                        : dupConstant.Value;
                                if (value != dupConstant.Value)
                                {
                                    return "";
                                }
                            }
                        }
                    }

                    retVal.Append(Generate(string.Format(" {0} ", a.AssignmentType), a));
                    foreach (SYMBOL kid in a.kids)
                    {
                        retVal.Append(CheckIfGlobalVariable(varName, type, kid));
                    }
                }
                else
                    retVal.Append(GenerateNode(s));

                retVal.Append(GenerateLine(";"));
            }

            return retVal.ToString();
        }

        string GetValue(Constant identEx)
        {
            if (identEx == null) return null;
            if (identEx.Value != null)
                return identEx.Value;
            StringBuilder retVal = new StringBuilder();
            if (identEx is VectorConstant)
            {
                VectorConstant vc = (VectorConstant) identEx;

                retVal.Append(Generate(string.Format("new {0}(", vc.Type), vc));
                retVal.Append(GenerateNode((SYMBOL) vc.kids[0]));
                retVal.Append(Generate(", "));
                retVal.Append(GenerateNode((SYMBOL) vc.kids[1]));
                retVal.Append(Generate(", "));
                retVal.Append(GenerateNode((SYMBOL) vc.kids[2]));
                retVal.Append(Generate(")"));

                return retVal.ToString();
            }
            if (identEx is RotationConstant)
            {
                RotationConstant rc = (RotationConstant) identEx;

                retVal.Append(Generate(string.Format("new {0}(", rc.Type), rc));
                retVal.Append(GenerateNode((SYMBOL) rc.kids[0]));
                retVal.Append(Generate(", "));
                retVal.Append(GenerateNode((SYMBOL) rc.kids[1]));
                retVal.Append(Generate(", "));
                retVal.Append(GenerateNode((SYMBOL) rc.kids[2]));
                retVal.Append(Generate(", "));
                retVal.Append(GenerateNode((SYMBOL) rc.kids[3]));
                retVal.Append(Generate(")"));

                return retVal.ToString();
            }
            if (identEx is ListConstant)
                return GenerateListConstant((ListConstant) identEx);
            return null;
        }

        string CheckIfGlobalVariable(string varName, string type, SYMBOL kid)
        {
            string globalVarValue = "";
            if (kid is Constant)
            {
                Constant c = kid as Constant;
                // Supprt LSL's weird acceptance of floats with no trailing digits
                // after the period. Turn float x = 10.; into float x = 10.0;
                if ("LSL_Types.LSLFloat" == c.Type)
                {
                    int dotIndex = c.Value.IndexOf('.') + 1;
                    if (0 < dotIndex && (dotIndex == c.Value.Length || !char.IsDigit(c.Value[dotIndex])))
                        c.Value = c.Value.Insert(dotIndex, "0");
                    globalVarValue = "new LSL_Types.LSLFloat(" + c.Value + ") ";
                }
                else if ("LSL_Types.LSLInteger" == c.Type)
                {
                    globalVarValue = "new LSL_Types.LSLInteger(" + c.Value + ") ";
                }
                else if ("LSL_Types.LSLString" == c.Type)
                {
                    globalVarValue = "new LSL_Types.LSLString(\"" + c.Value + "\") ";
                }
                if (globalVarValue == "")
                    globalVarValue = c.Value;

                if (globalVarValue == null)
                    globalVarValue = GenerateNode(c);
                if (GlobalVariables.ContainsKey(globalVarValue))
                {
                    //Its an assignment to another global var!
                    //reset the global value to the other's value
                    GlobalVar var;
                    GlobalVariables.TryGetValue(globalVarValue, out var);
                    //Do one last additional test before we set it.
                    if (type == var.Type)
                    {
                        globalVarValue = var.Value;
                    }
                    c.Value = globalVarValue;
                }
                else if (GlobalVariables.ContainsKey(varName))
                {
                }
                else
                    GlobalVariables.Add(varName, new GlobalVar
                                                     {
                                                         Type = type,
                                                         Value = globalVarValue
                                                     });
                return globalVarValue;
            }
            else if (kid is IdentExpression)
            {
                IdentExpression c = kid as IdentExpression;
                globalVarValue = c.Name;

                if (GlobalVariables.ContainsKey(globalVarValue))
                {
                    //Its an assignment to another global var!
                    //reset the global value to the other's value
                    GlobalVar var;
                    GlobalVariables.TryGetValue(globalVarValue, out var);
                    //Do one last additional test before we set it.
                    if (type == var.Type)
                    {
                        globalVarValue = var.Value;
                    }
                }
                GlobalVariables.Add(varName, new GlobalVar
                                                 {
                                                     Type = type,
                                                     Value = globalVarValue
                                                 });

                return globalVarValue;
            }
            else if (kid is UnaryExpression)
            {
                UnaryExpression c = kid as UnaryExpression;
                globalVarValue = Generate(c.UnarySymbol, c);
                foreach (SYMBOL k in c.kids)
                {
                    globalVarValue += CheckIfGlobalVariable(varName, type, k);
                }
                return globalVarValue;
            }
            else
                return GenerateNode(kid);
        }

        /// <summary>
        ///     Generates the code for a State node.
        /// </summary>
        /// <param name="s">The State node.</param>
        /// <returns>String containing C# code for State s.</returns>
        string GenerateState(State s)
        {
            StringBuilder retVal = new StringBuilder();
            _currentLocalStateDeclaration = s;

            foreach (SYMBOL kid in s.kids)
                if (kid is StateEvent)
                    retVal.Append(GenerateStateEvent((StateEvent)kid, s.Name));

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a StateEvent node.
        /// </summary>
        /// <param name="se">The StateEvent node.</param>
        /// <param name="parentStateName">The name of the parent state.</param>
        /// <returns>String containing C# code for StateEvent se.</returns>
        string GenerateStateEvent(StateEvent se, string parentStateName)
        {
            StringBuilder retstr = new StringBuilder();

            // we need to separate the argument declaration list from other kids
            List<SYMBOL> argumentDeclarationListKids = new List<SYMBOL>();
            List<SYMBOL> remainingKids = new List<SYMBOL>();
            LSL2CSCodeTransformer.FixEventName(OriginalScript, ref se);

            _currentLocalFunctionDeclaration = se;

            MethodVariables.Clear();
            VariablesToRename.Clear();

            foreach (SYMBOL kid in se.kids)
                if (kid is ArgumentDeclarationList)
                    argumentDeclarationListKids.Add(kid);
                else
                    remainingKids.Add(kid);

            // "state" (function) declaration
            retstr.Append(GenerateIndented(
                string.Format("public System.Collections.IEnumerator {0}_event_{1}(", parentStateName, se.Name), se));

            IsParentEnumerable = true;

            // print the state arguments, if any
            List<ArgumentDeclarationList> args = new List<ArgumentDeclarationList>();
            foreach (SYMBOL kid in argumentDeclarationListKids)
            {
                args.Add(((ArgumentDeclarationList) kid));
                retstr.Append(GenerateArgumentDeclarationList((ArgumentDeclarationList) kid));
            }

            m_allMethods.Add(string.Format("{0}_event_{1}", parentStateName, se.Name), args);
            retstr.Append(GenerateLine(")"));


            foreach (SYMBOL kid in remainingKids)
                retstr.Append(GenerateNode(kid));


            if (retstr[retstr.Length - 2] == '}')
                retstr.Insert(retstr.Length - 2, GenerateLine("    yield break;"));

            return retstr.ToString();
        }

        /// <summary>
        ///     Generates the code for an ArgumentDeclarationList node.
        /// </summary>
        /// <param name="adl">The ArgumentDeclarationList node.</param>
        /// <returns>String containing C# code for ArgumentDeclarationList adl.</returns>
        string GenerateArgumentDeclarationList(ArgumentDeclarationList adl)
        {
            StringBuilder retVal = new StringBuilder();
            int comma = adl.kids.Count - 1; // tells us whether to print a comma

            foreach (Declaration d in adl.kids)
            {
                retVal.Append(GenerateDeclaration(d));
                //                retstr += Generate(String.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);
                if (0 < comma--)
                    retVal.Append(Generate(", "));
            }

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for an ArgumentList node.
        /// </summary>
        /// <param name="al">The ArgumentList node.</param>
        /// <returns>String containing C# code for ArgumentList al.</returns>
        string GenerateArgumentList(ArgumentList al)
        {
            StringBuilder retVal = new StringBuilder();
            int comma = al.kids.Count - 1; // tells us whether to print a comma

            foreach (SYMBOL s in al.kids)
            {
                retVal.Append(GenerateNode(s));
                if (0 < comma--)
                    retVal.Append(Generate(", "));
            }

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a CompoundStatement node.
        /// </summary>
        /// <param name="cs">The CompoundStatement node.</param>
        /// <returns>String containing C# code for CompoundStatement cs.</returns>
        string GenerateCompoundStatement(CompoundStatement cs)
        {
            StringBuilder retVal = new StringBuilder();
            // opening brace
            retVal.Append(GenerateIndentedLine("{"));
            //            if (IsParentEnumerable)
            //                retstr += GenerateLine("if (CheckSlice()) yield return null;");
            m_braceCount++;

            foreach (SYMBOL kid in cs.kids)
                if (kid is Statement && kid.kids.Top is BinaryExpression &&
                    ((BinaryExpression) kid.kids.Top).ExpressionSymbol == "==")
                    continue;
                else
                    retVal.Append(GenerateNode(kid));

            // closing brace
            m_braceCount--;

            retVal.Append(GenerateIndentedLine("}"));

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a Declaration node.
        /// </summary>
        /// <param name="d">The Declaration node.</param>
        /// <returns>String containing C# code for Declaration d.</returns>
        string GenerateDeclaration(Declaration d)
        {
            //        return Generate(String.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);

            GlobalVar var;
            if (IsaGlobalVar)
                return Generate(string.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);

            if (GlobalVariables.TryGetValue(d.Id, out var))
                return Generate(string.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);

            //Commented out because we can't handle the same var name in different if/else statements
            /*if (MethodVariables.TryGetValue(d.Id, out var))
            {
            if (var.Type != d.Datatype)
                {
                Console.WriteLine("[CSCodeGenerator]: found var needing renamed!");
                string NewVariableName = RandomString(10, true);
                VarRename r = new VarRename();
                r.OldVarName = d.Id;
                r.HasBeenAssigned = false;
                r.NewVarName = NewVariableName;
                VariablesToRename.Add(d.Id, r);
                d.Id = NewVariableName;
                MethodVariables.Add(d.Id, new GlobalVar() { Type = d.Datatype, Value = "" });
                return Generate(String.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);
                }
            else
                return Generate(String.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);
            }
        else
            {
            MethodVariables[d.Id] = new GlobalVar() { Type = d.Datatype, Value = "" };
            return Generate(String.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);
            }  
         * */

            return Generate(string.Format("{0} {1}", d.Datatype, CheckName(d.Id)), d);
        }

        /// <summary>
        ///     Generates the code for a Statement node.
        /// </summary>
        /// <param name="s">The Statement node.</param>
        /// <returns>String containing C# code for Statement s.</returns>
        string GenerateStatement(Statement s)
        {
            StringBuilder retVal = new StringBuilder();
            bool printSemicolon = true;

            bool marc = FuncCallsMarc();
            retVal.Append(Indent());

            if (0 < s.kids.Count)
            {
                // Jump label prints its own colon, we don't need a semicolon.
                printSemicolon = !(s.kids.Top is JumpLabel);

                // If we encounter a lone Ident, we skip it, since that's a C#
                // (MONO) error.
                if (!(s.kids.Top is IdentExpression && 1 == s.kids.Count))
                {
                    foreach (SYMBOL kid in s.kids)
                    {
                        //                        if (kid is Assignment && m_SLCompatabilityMode)
                        if (kid is Assignment)
                        {
                            Assignment a = kid as Assignment;
                            List<string> identifiers = new List<string>();
                            checkForMultipleAssignments(identifiers, a);

                            SYMBOL firstChild = (SYMBOL) a.kids[0];
                            bool retStrChanged = false;
                            if (firstChild is Declaration &&
                                DuplicatedLocalVariables[GetLocalDeclarationKey()].ContainsKey(
                                    ((Declaration) firstChild).Id))
                            {
                                Declaration dec = ((Declaration) firstChild);
                                if (a.kids.Count == 2)
                                {
                                    SYMBOL assignmentChild = (SYMBOL) a.kids[1];
                                tryAgain:
                                    if (assignmentChild is TypecastExpression)
                                    {
                                        TypecastExpression typecast = (TypecastExpression)assignmentChild;
                                        assignmentChild = (SYMBOL)typecast.kids[0];
                                        goto tryAgain;
                                    }
                                    else if (assignmentChild is FunctionCall || assignmentChild is FunctionCallExpression)
                                    {
                                        retStrChanged = true;
                                        retVal.Append(dec.Id);
                                        a.kids.Pop();
                                    }
                                    else if (assignmentChild is IdentExpression)
                                    {
// 20131224 not used                                        IdentExpression identEx = (IdentExpression) assignmentChild;
                                    }
                                    else if (assignmentChild is ListConstant)
                                    {
                                        ListConstant listConst = (ListConstant) assignmentChild;
                                        foreach (SYMBOL listChild in listConst.kids)
                                        {
                                            if (listChild is ArgumentList)
                                            {
                                                ArgumentList argList = (ArgumentList) listChild;
                                                int i = 0;
                                                //bool changed = false;
                                                object[] pObj = new object[argList.kids.Count];
                                                foreach (SYMBOL objChild in argList.kids)
                                                {
                                                    pObj[i] = objChild;
                                                    if (objChild is IdentExpression)
                                                    {
// 20131224 not used                                                        IdentExpression identEx = (IdentExpression) objChild;
                                                    }
                                                    i++;
                                                }
                                                // TODO: 20160607 -greythane- This is never executed, check implmentation
                                                /*if (changed)
                                                {
                                                    argList.kids = new ObjectList();
                                                    foreach (object o in pObj)
                                                        argList.kids.Add(o);
                                                }
                                                */
                                            }
                                        }
                                    }
                                    else if (assignmentChild is Constant)
                                    {
                                        Constant identEx = (Constant) assignmentChild;
                                        string value = GetValue(identEx);
                                        Constant dupConstant =
                                            (Constant) DuplicatedLocalVariables[GetLocalDeclarationKey()][dec.Id];
                                        if(dupConstant != null)
                                            dupConstant.Value = (dupConstant == null || dupConstant.Value == null)
                                                                    ? GetValue(dupConstant)
                                                                    : dupConstant.Value;
                                        if (dupConstant == null || value != dupConstant.Value)
                                        {
                                            retStrChanged = true;
                                            retVal.Append(dec.Id);
                                            a.kids.Pop();
                                        }
                                    }
                                }
                            }
                            if (!retStrChanged)
                                retVal.Append(GenerateNode((SYMBOL) a.kids.Pop()));
                            retVal.Append(Generate(string.Format(" {0} ", a.AssignmentType), a));
                            foreach (SYMBOL akid in a.kids)
                            {
                                if (akid is BinaryExpression)
                                {
                                    BinaryExpression be = akid as BinaryExpression;
                                    if (be.ExpressionSymbol.Equals("&&") || be.ExpressionSymbol.Equals("||"))
                                    {
                                        // special case handling for logical and/or, see Mantis 3174
                                        retVal.Append("((bool)(");
                                        retVal.Append(GenerateNode((SYMBOL) be.kids.Pop()));
                                        retVal.Append("))");
                                        retVal.Append(Generate(string.Format(" {0} ", be.ExpressionSymbol.Substring(0, 1)), be));
                                        retVal.Append("((bool)(");
                                        foreach (SYMBOL bkid in be.kids)
                                            retVal.Append(GenerateNode(bkid));
                                        retVal.Append("))");
                                    }
                                    else
                                    {
                                        retVal.Append(GenerateNode((SYMBOL) be.kids.Pop()));
                                        retVal.Append(Generate(string.Format(" {0} ", be.ExpressionSymbol), be));
                                        foreach (SYMBOL kidb in be.kids)
                                        {
                                            //                                            if (kidb is FunctionCallExpression)
                                            {
                                                retVal.Append(GenerateNode(kidb));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    retVal.Append(GenerateNode(akid));
                                }
                            }
                        }
                        else
                        {
                            if (kid is FunctionCallExpression)
                            {
                                foreach (SYMBOL akid in kid.kids)
                                {
                                    if (akid is FunctionCall)
                                        retVal.Append(GenerateFunctionCall(akid as FunctionCall, false));
                                    else
                                        retVal.Append(GenerateNode(akid));
                                }
                            }
                            else
                            {
                                // this kids will not need to dump in current string position
                                // so save what we have in dump and let kids have their own then take it again
                                retVal.Append(DumpFunc(marc));
                                retVal.Append(GenerateNode(kid));
                                marc = FuncCallsMarc();
                            }
                        }
                    }
                }
            }

            //Nasty hack to fix if statements with yield return and yield break;
            if (retVal[retVal.Length - 1] == '}')
                printSemicolon = false;

            if (printSemicolon)
                retVal.Append(GenerateLine(";"));

            return DumpFunc(marc) + retVal + DumpAfterFunc(marc);
        }

        string GetLocalDeclarationKey()
        {
            if (_currentLocalStateDeclaration == null)
            {
                if (_currentGlobalFunctionDeclaration == null)
                    return null;
                
                return "global_function_" + _currentGlobalFunctionDeclaration.Name;
            }
            return _currentLocalStateDeclaration.Name + "_" + _currentLocalFunctionDeclaration.Name;
        }

        /// <summary>
        ///     Generates the code for an Assignment node.
        /// </summary>
        /// <param name="a">The Assignment node.</param>
        /// <returns>String containing C# code for Assignment a.</returns>
        string GenerateAssignment(Assignment a)
        {
            StringBuilder retVal = new StringBuilder();
            List<string> identifiers = new List<string>();

            bool marc = FuncCallsMarc();
            checkForMultipleAssignments(identifiers, a);

            if (a.kids[a.kids.Count - 1] is ListConstant && isAdditionExpression) //Deal with the list memory hack
            {
                a.kids.Pop(); //Get rid of the first one
                foreach (SYMBOL kid in a.kids)
                    retVal.Append(GenerateNode(kid));
                return retVal.ToString(); //If it is a list, and we are in an addition expression, we drop the assignment
            }

            retVal.Append(GenerateNode((SYMBOL) a.kids.Pop()));
            retVal.Append(Generate(string.Format(" {0} ", a.AssignmentType), a));
            foreach (SYMBOL kid in a.kids)
                retVal.Append(GenerateNode(kid));
            //fCalls += ";";//Add a ; at the end.
            //lock (AfterFuncCalls)
            //    AfterFuncCalls.Add (fCalls);

            return DumpFunc(marc) + retVal.ToString() + DumpAfterFunc(marc);
        }

        // This code checks for LSL of the following forms, and generates a
        // warning if it finds them.
        //
        // list l = [ "foo" ]; 
        // l = (l=[]) + l + ["bar"];
        // (produces l=["foo","bar"] in SL but l=["bar"] in OS)
        //
        // integer i;
        // integer j;
        // i = (j = 3) + (j = 4) + (j = 5);
        // (produces j=3 in SL but j=5 in OS)
        //
        // Without this check, that code passes compilation, but does not do what
        // the end user expects, because LSL in SL evaluates right to left instead
        // of left to right.
        //
        // The theory here is that producing an error and alerting the end user that
        // something needs to change is better than silently generating incorrect code.
        void checkForMultipleAssignments(List<string> identifiers, SYMBOL s)
        {
            if (s is Assignment)
            {
                Assignment a = (Assignment) s;
                string newident = null;

                if (a.kids[0] is Declaration)
                {
                    newident = ((Declaration) a.kids[0]).Id;
                }
                else if (a.kids[0] is IDENT)
                {
                    newident = ((IDENT) a.kids[0]).yytext;
                }
                else if (a.kids[0] is IdentDotExpression)
                {
                    newident = ((IdentDotExpression) a.kids[0]).Name; // +"." + ((IdentDotExpression)a.kids[0]).Member;
                }
                else
                {
                    AddWarning(string.Format(
                        "Multiple assignments checker internal error '{0}' at line {1} column {2}.", a.kids[0].GetType(),
                        ((SYMBOL) a.kids[0]).Line - 1, ((SYMBOL) a.kids[0]).Position));
                }

                if (identifiers.Contains(newident))
                {
                    AddWarning(
                        string.Format(
                            "Multiple assignments to '{0}' at line {1} column {2}; results may differ between LSL and OSSL.",
                            newident, ((SYMBOL) a.kids[0]).Line - 1, ((SYMBOL) a.kids[0]).Position));
                }
                identifiers.Add(newident);
            }

            int index;
            for (index = 0; index < s.kids.Count; index++)
            {
                checkForMultipleAssignments(identifiers, (SYMBOL) s.kids[index]);
            }
        }

        /// <summary>
        ///     Generates the code for a ReturnStatement node.
        /// </summary>
        /// <param name="rs">The ReturnStatement node.</param>
        /// <returns>String containing C# code for ReturnStatement rs.</returns>
        string GenerateReturnStatement(ReturnStatement rs)
        {
            StringBuilder retVal = new StringBuilder();

            bool dump = FuncCallsMarc();

            if (IsParentEnumerable)
            {
                retVal.Append(GenerateLine("{ "));
                if (rs.kids.Count == 0)
                    retVal.Append(GenerateLine("yield break;", rs));
                else
                {
                    retVal.Append(Generate(string.Format("yield return ({0})(", _currentGlobalFunctionDeclaration.ReturnType), rs));
                    foreach (SYMBOL kid in rs.kids)
                        retVal.Append(GenerateNode(kid));
                    retVal.Append(GenerateLine(");", null));
                    retVal.Append(GenerateLine("yield break;", null));
                }
                retVal.Append(GenerateLine("}"));
            }
            else
            {
                retVal.Append(Generate(string.Format("return ({0})", _currentGlobalFunctionDeclaration.ReturnType), rs));

                foreach (SYMBOL kid in rs.kids)
                    retVal.Append(GenerateNode(kid));
            }
            if (dump)
                return DumpFunc(dump) + retVal + DumpAfterFunc(dump);
            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a JumpLabel node.
        /// </summary>
        /// <param name="jl">The JumpLabel node.</param>
        /// <returns>String containing C# code for JumpLabel jl.</returns>
        string GenerateJumpLabel(JumpLabel jl)
        {
            return GenerateLine(Generate(string.Format("{0}:", CheckName(jl.LabelName)), jl) + " NoOp();");
        }

        /// <summary>
        ///     Generates the code for a JumpStatement node.
        /// </summary>
        /// <param name="js">The JumpStatement node.</param>
        /// <returns>String containing C# code for JumpStatement js.</returns>
        string GenerateJumpStatement(JumpStatement js)
        {
            return Generate(string.Format("goto {0}", CheckName(js.TargetName)), js);
        }

        /// <summary>
        ///     Generates the code for an IfStatement node.
        /// </summary>
        /// <param name="ifs">The IfStatement node.</param>
        /// <returns>String containing C# code for IfStatement ifs.</returns>
        string GenerateIfStatement(IfStatement ifs)
        {
            /*
             * Test script that was used to make sure that if statements do not fail
              integer a = 0;
integer test()
{
    a++;
    return a;
}
default
{
    state_entry()
    {
        if(test() == 0) //1 gets returned here
        {
            llSay(0, "Script running. 0");
        }
        else if(test() == 1) //2 gets returned here
        {
            llSay(0, "Script running. 2");
        }
        else
        {
            // 3 gets returned here
            if(test() == 4)
            {
                llSay(0, "Script running. 4");
            }
            else if(test() == 4)
            {
                // It should hit this path
                llSay(0, "Script running. 2 4");
            }
            else
            {
              // 5 would be returned here
                llSay(0, "Script running. else " + test());
            }
        }
    }
}*/
            StringBuilder retVal = new StringBuilder(), tmpVal = new StringBuilder();
            bool DoBrace = false;
            bool marc = FuncCallsMarc();
            tmpVal.Append(GenerateIndented("if (", ifs));
            tmpVal.Append(GenerateNode((SYMBOL)ifs.kids.Pop()));
            tmpVal.Append(GenerateLine(")"));

            retVal.Append(DumpFunc(marc));
            retVal.Append(tmpVal.ToString());

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.

            // bool indentHere = ifs.kids.Top is Statement;
            // if (indentHere) m_braceCount++;
            DoBrace = !(ifs.kids.Top is CompoundStatement);
            if (DoBrace)
                retVal.Append(GenerateLine("{"));

            retVal.Append(GenerateNode((SYMBOL)ifs.kids.Pop()));
            // if (indentHere) m_braceCount--;
            if (DoBrace)
                retVal.Append(GenerateLine("}"));


            if (0 < ifs.kids.Count) // do it again for an else
            {
                retVal.Append(GenerateIndentedLine("else", ifs));

                // indentHere = ifs.kids.Top is Statement;
                // if (indentHere) m_braceCount++;
                DoBrace = !(ifs.kids.Top is CompoundStatement);
                if (DoBrace)
                    retVal.Append(GenerateLine("{"));

                retVal.Append(GenerateNode((SYMBOL)ifs.kids.Pop()));

                if (DoBrace)
                    retVal.Append(GenerateLine("}"));


                // if (indentHere) m_braceCount--;
            }

            retVal.Append(DumpAfterFunc(marc));
            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a StateChange node.
        /// </summary>
        /// <param name="sc">The StateChange node.</param>
        /// <returns>String containing C# code for StateChange sc.</returns>
        string GenerateStateChange(StateChange sc)
        {
            //State is in the LSL_Api because it requires a ref to the ScriptEngine, which we can't have in the ScriptBase
            StringBuilder retVal = new StringBuilder();
            retVal.Append(GenerateLine("try", null));
            retVal.Append(GenerateLine("{", null));
            retVal.Append(Generate(string.Format("((dynamic)m_apis[\"ll\"]).state(\"{0}\");", sc.NewState), sc));
            retVal.Append(GenerateLine("}", null));
            retVal.Append(GenerateLine("catch", null));
            retVal.Append(GenerateLine("{", null));
            retVal.Append(GenerateLine("yield break;", null));
            retVal.Append(GenerateLine("}", null));
            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a WhileStatement node.
        /// </summary>
        /// <param name="ws">The WhileStatement node.</param>
        /// <returns>String containing C# code for WhileStatement ws.</returns>
        string GenerateWhileStatement(WhileStatement ws)
        {
            StringBuilder retVal = new StringBuilder();
            StringBuilder tmpVal = new StringBuilder();

            bool marc = FuncCallsMarc();

            tmpVal.Append(GenerateIndented("while (", ws));
            tmpVal.Append(GenerateNode((SYMBOL) ws.kids.Pop()));
            tmpVal.Append(GenerateLine(")"));

            //Forces all functions to use MoveNext() instead of .Current, as it never changes otherwise, and the loop runs infinitely
            m_isInEnumeratedDeclaration = true;
            retVal.Append(DumpFunc(marc));
            retVal.Append(tmpVal.ToString());
            m_isInEnumeratedDeclaration = false; //End above

            if (IsParentEnumerable)
            {
                retVal.Append(GenerateLine("{")); // SLAM! No 'while(true) doThis(); ' statements for you!
                retVal.Append(GenerateLine("if (CheckSlice()) yield return null;"));
            }

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.
            bool indentHere = ws.kids.Top is Statement;
            if (indentHere) m_braceCount++;
            retVal.Append(GenerateNode((SYMBOL) ws.kids.Pop()));
            if (indentHere) m_braceCount--;

            if (IsParentEnumerable)
                retVal.Append(GenerateLine("}"));

            return retVal + DumpAfterFunc(marc);
        }

        /// <summary>
        ///     Generates the code for a DoWhileStatement node.
        /// </summary>
        /// <param name="dws">The DoWhileStatement node.</param>
        /// <returns>String containing C# code for DoWhileStatement dws.</returns>
        string GenerateDoWhileStatement(DoWhileStatement dws)
        {
            StringBuilder retVal = new StringBuilder();

            retVal.Append(GenerateIndentedLine("do", dws));
            if (IsParentEnumerable)
            {
                retVal.Append(GenerateLine("{")); // SLAM!
                retVal.Append(GenerateLine("if (CheckSlice()) yield return null;"));
            }

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.
            bool indentHere = dws.kids.Top is Statement;
            if (indentHere) m_braceCount++;
            retVal.Append(GenerateNode((SYMBOL) dws.kids.Pop()));
            if (indentHere) m_braceCount--;

            if (IsParentEnumerable)
                retVal.Append(GenerateLine("}"));

            bool marc = FuncCallsMarc();

            //Forces all functions to use MoveNext() instead of .Current, as it never changes otherwise, and the loop runs infinitely

            m_isInEnumeratedDeclaration = true;

            retVal.Append(GenerateIndented("while (", dws));
            retVal.Append(GenerateNode((SYMBOL) dws.kids.Pop()));
            retVal.Append(GenerateLine(");"));

            m_isInEnumeratedDeclaration = false; //End above

            return DumpFunc(marc) + retVal + DumpAfterFunc(marc);
        }

        /// <summary>
        ///     Generates the code for a ForLoop node.
        /// </summary>
        /// <param name="fl">The ForLoop node.</param>
        /// <returns>String containing C# code for ForLoop fl.</returns>
        string GenerateForLoop(ForLoop fl)
        {
            StringBuilder retVal = new StringBuilder();
            StringBuilder tmpVal = new StringBuilder();

            bool marc = FuncCallsMarc();

            tmpVal.Append(GenerateIndented("for (", fl));

            // It's possible that we don't have an assignment, in which case
            // the child will be null and we only print the semicolon.
            // for (x = 0; x < 10; x++)
            //      ^^^^^
            ForLoopStatement s = (ForLoopStatement) fl.kids.Pop();
            if (null != s)
            {
                tmpVal.Append(GenerateForLoopStatement(s));
            }
            tmpVal.Append(Generate("; "));
            // for (x = 0; x < 10; x++)
            //             ^^^^^^
            tmpVal.Append(GenerateNode((SYMBOL) fl.kids.Pop()));
            tmpVal.Append(Generate("; "));
            // for (x = 0; x < 10; x++)
            //                     ^^^
            tmpVal.Append(GenerateForLoopStatement((ForLoopStatement) fl.kids.Pop()));
            tmpVal.Append(GenerateLine(")"));

            retVal.Append(DumpFunc(marc));
            retVal.Append(tmpVal.ToString());

            if (IsParentEnumerable)
            {
                retVal.Append(GenerateLine("{")); // SLAM! No 'for(i = 0; i < 1; i = 0) doSomething();' statements for you
                retVal.Append(GenerateLine("if (CheckSlice()) yield return null;"));
            }

            // CompoundStatement handles indentation itself but we need to do it
            // otherwise.
            bool indentHere = fl.kids.Top is Statement;
            if (indentHere) m_braceCount++;
            retVal.Append(GenerateNode((SYMBOL) fl.kids.Pop()));
            if (indentHere) m_braceCount--;

            if (IsParentEnumerable)
                retVal.Append(GenerateLine("}"));
            
            retVal.Append(DumpAfterFunc(marc));
            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a ForLoopStatement node.
        /// </summary>
        /// <param name="fls">The ForLoopStatement node.</param>
        /// <returns>String containing C# code for ForLoopStatement fls.</returns>
        string GenerateForLoopStatement(ForLoopStatement fls)
        {
            StringBuilder retVal = new StringBuilder();
            int comma = fls.kids.Count - 1; // tells us whether to print a comma

            // It's possible that all we have is an empty Ident, for example:
            //
            //     for (x; x < 10; x++) { ... }
            //
            // Which is illegal in C# (MONO). We'll skip it.
            if (fls.kids.Top is IdentExpression && 1 == fls.kids.Count)
                return "";

            for (int i = 0; i < fls.kids.Count; i++)
            {
                SYMBOL s = (SYMBOL) fls.kids[i];

                // Statements surrounded by parentheses in for loops
                //
                // e.g.  for ((i = 0), (j = 7); (i < 10); (++i))
                //
                // are legal in LSL but not in C# so we need to discard the parentheses
                //
                // The following, however, does not appear to be legal in LLS
                //
                // for ((i = 0, j = 7); (i < 10); (++i))
                //
                // As of Friday 20th November 2009, the Linden Lab simulators appear simply never to compile or run this
                // script but with no debug or warnings at all!  Therefore, we won't deal with this yet (which looks
                // like it would be considerably more complicated to handle).
                while (s is ParenthesisExpression)
                    s = (SYMBOL) s.kids.Pop();

                retVal.Append(GenerateNode(s));
                if (0 < comma--)
                    retVal.Append(Generate(", "));
            }

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a BinaryExpression node.
        /// </summary>
        /// <param name="be">The BinaryExpression node.</param>
        /// <param name="isUnaryExpression"></param>
        /// <param name="addition"></param>
        /// <returns>String containing C# code for BinaryExpression be.</returns>
        string GenerateBinaryExpression(BinaryExpression be, bool isUnaryExpression, string addition)
        {
            StringBuilder retVal = new StringBuilder();
            bool marc = FuncCallsMarc();

            if (be.ExpressionSymbol.Equals("&&") || be.ExpressionSymbol.Equals("||"))
            {
                // special case handling for logical and/or, see Mantis 3174
                retVal.Append("((LSL_Types.LSLInteger)( " +
                          (isUnaryExpression ? addition : "") +
                          "(bool)(");
                retVal.Append(GenerateNode((SYMBOL) be.kids.Pop()));
                retVal.Append("))");
                retVal.Append(Generate(string.Format(" {0} ", be.ExpressionSymbol.Substring(0, 1)), be));
                retVal.Append("((LSL_Types.LSLInteger)((bool)(");
                foreach (SYMBOL kid in be.kids)
                    retVal.Append(GenerateNode(kid));
                retVal.Append("))))");
            }
            else if (be.ExpressionSymbol.Equals("!=") || be.ExpressionSymbol.Equals("=="))
            {
                retVal.Append("((LSL_Types.LSLInteger)(");
                retVal.Append(GenerateNode((SYMBOL) be.kids.Pop()));
                retVal.Append(Generate(string.Format(" {0} ", be.ExpressionSymbol), be));
                foreach (SYMBOL kid in be.kids)
                    retVal.Append(GenerateNode(kid));
                retVal.Append("))");
            }
            else
            {
                /*ObjectList kids = new ObjectList ();
                for (int i = be.kids.Count-1; i >= 0; i--)
                {
                    kids.Add(be.kids[i]);
                }*/
                bool weSetTheAdditionExpression = false;
                if (be.ExpressionSymbol == "+" && !isAdditionExpression)
                {
                    weSetTheAdditionExpression = true;
                    isAdditionExpression = true;
                }
                retVal.Append(GenerateNode((SYMBOL) be.kids.Pop()));
                if (weSetTheAdditionExpression)
                    isAdditionExpression = false;
                if (!(retVal.ToString() == "()" || retVal.ToString() == ""))
                    retVal.Append(Generate(string.Format(" {0} ", be.ExpressionSymbol), be));
                else
                    //Something was removed, we need to remove the operator here!
                    retVal.Clear();
                foreach (SYMBOL kid in be.kids)
                    retVal.Append(GenerateNode(kid));
            }

            return DumpFunc(marc) + retVal + DumpAfterFunc(marc);
        }

        /// <summary>
        ///     Generates the code for a UnaryExpression node.
        /// </summary>
        /// <param name="ue">The UnaryExpression node.</param>
        /// <returns>String containing C# code for UnaryExpression ue.</returns>
        string GenerateUnaryExpression(UnaryExpression ue)
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append(Generate(ue.UnarySymbol, ue));
            SYMBOL kid = (SYMBOL) ue.kids.Pop();
            if (kid is BinaryExpression)
            {
                string tempretstr = retVal.ToString();
                retVal.Clear();
                retVal.Append(GenerateBinaryExpression((BinaryExpression)kid, true, tempretstr));
            }
            else
                retVal.Append(GenerateNode(kid));

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a ParenthesisExpression node.
        /// </summary>
        /// <param name="pe">The ParenthesisExpression node.</param>
        /// <returns>String containing C# code for ParenthesisExpression pe.</returns>
        private string GenerateParenthesisExpression(ParenthesisExpression pe)
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append(Generate("("));
            foreach (SYMBOL kid in pe.kids)
                retVal.Append(GenerateNode(kid));
            retVal.Append(Generate(")"));

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a IncrementDecrementExpression node.
        /// </summary>
        /// <param name="ide">The IncrementDecrementExpression node.</param>
        /// <returns>String containing C# code for IncrementDecrementExpression ide.</returns>
        string GenerateIncrementDecrementExpression(IncrementDecrementExpression ide)
        {
            StringBuilder retVal = new StringBuilder();
            if (0 < ide.kids.Count)
            {
                IdentDotExpression dot = (IdentDotExpression) ide.kids.Top;
                retVal.Append(Generate(
                        string.Format("{0}",
                                      ide.PostOperation
                                          ? CheckName(dot.Name) + "." + dot.Member + ide.Operation
                                          : ide.Operation + CheckName(dot.Name) + "." + dot.Member), ide));
            }
            else
                retVal.Append(Generate(
                        string.Format("{0}",
                                      ide.PostOperation
                                          ? CheckName(ide.Name) + ide.Operation
                                          : ide.Operation + CheckName(ide.Name)), ide));

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a TypecastExpression node.
        /// </summary>
        /// <param name="te">The TypecastExpression node.</param>
        /// <returns>String containing C# code for TypecastExpression te.</returns>
        string GenerateTypecastExpression(TypecastExpression te)
        {
            StringBuilder retVal = new StringBuilder();
            // we wrap all typecasted statements in parentheses
            retVal.Append(Generate(string.Format("({0}) (", te.TypecastType), te));
            retVal.Append(GenerateNode((SYMBOL) te.kids.Pop()));
            retVal.Append(Generate(")"));
            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a FunctionCall node.
        /// </summary>
        /// <param name="fc">The FunctionCall node.</param>
        /// <param name="NeedRetVal"></param>
        /// <returns>String containing C# code for FunctionCall fc.</returns>
        string GenerateFunctionCall(FunctionCall fc, bool NeedRetVal)
        {
            StringBuilder retVal = new StringBuilder(), tmpVal = new StringBuilder();
            bool marc = FuncCallsMarc();

            string Mname = "";
            bool isEnumerable = false;

            //int NeedCloseParent = 0;

            foreach (SYMBOL kid in fc.kids)
            {
                //                if (kid is ArgumentList && m_SLCompatabilityMode)
                if (kid is ArgumentList)
                {
                    ArgumentList al = kid as ArgumentList;
                    int comma = al.kids.Count - 1; // tells us whether to print a comma

                    foreach (SYMBOL s in al.kids)
                    {
                        if (s is BinaryExpression)
                        {
                            BinaryExpression be = s as BinaryExpression;
                            //FunctionCalls += GenerateNode(s);
                            if (be.ExpressionSymbol.Equals("&&") || be.ExpressionSymbol.Equals("||"))
                            {
                                // special case handling for logical and/or, see Mantis 3174
                                tmpVal.Append("((bool)(");
                                tmpVal.Append(GenerateNode((SYMBOL) be.kids.Pop()));
                                tmpVal.Append("))");
                                tmpVal.Append(Generate(string.Format(" {0} ", be.ExpressionSymbol.Substring(0, 1)), be));
                                tmpVal.Append("((bool)(");
                                foreach (SYMBOL kidb in be.kids)
                                    retVal.Append(GenerateNode(kidb));
                                tmpVal.Append("))");
                            }
                            else
                            {
                                tmpVal.Append(GenerateNode((SYMBOL) be.kids.Pop()));
                                tmpVal.Append(Generate(string.Format(" {0} ", be.ExpressionSymbol), be));
                                foreach (SYMBOL kidb in be.kids)
                                {
                                    if (kidb is FunctionCallExpression)
                                    {
                                        tmpVal.Append(GenerateNode(kidb));
                                    }
                                    else if (kidb is TypecastExpression)
                                    {
                                        tmpVal.Append(Generate(string.Format("({0}) (", ((TypecastExpression) kidb).TypecastType)));
                                        tmpVal.Append(GenerateNode((SYMBOL) kidb.kids.Pop()));
                                        tmpVal.Append(Generate(")"));
                                    }

                                    else
                                        tmpVal.Append(GenerateNode(kidb));
                                }
                            }
                        }
                        else if (s is TypecastExpression)
                        {
                            tmpVal.Append(Generate(string.Format("({0}) (", ((TypecastExpression) s).TypecastType)));
                            tmpVal.Append(GenerateNode((SYMBOL) s.kids.Pop()));
                            tmpVal.Append(Generate(")"));
                        }
                        else
                        {
                            tmpVal.Append(GenerateNode(s));
                        }

                        if (0 < comma--)
                            tmpVal.Append(Generate(", "));
                    }
                }
                else
                {
                    tmpVal.Append(GenerateNode(kid));
                }
            }

            isEnumerable = false;
            bool DTFunction = false;

            string rettype = "void";
            if (LocalMethods.TryGetValue(fc.Id, out rettype))
                isEnumerable = true;
                /* suspended.. API fails with IEnums
                        else if (IenFunctions.TryGetValue(fc.Id, out rettype))
                            isEnumerable = true;
            */
            else if (DTFunctions.Contains(fc.Id))
            {
                DTFunction = true;
            }

            //Check whether this function is an API function
            if (m_apiFunctions.ContainsKey(CheckName(fc.Id)))
            {
                //Add the m_apis link
                fc.Id = string.Format("((dynamic)m_apis[\"{0}\"]).{1}",
                                      m_apiFunctions[CheckName(fc.Id)].Name, fc.Id);
            }

            if (DTFunction)
            {
                retVal.Append(Generate("yield return "));
                retVal.Append(Generate(string.Format("{0}(", CheckName(fc.Id)), fc));
                retVal.Append(tmpVal.ToString());
                retVal.Append(Generate(")"));
            }
            else if (isEnumerable)
            {
                if (m_isInEnumeratedDeclaration && NeedRetVal) //Got to have a retVal for do/while
                {
                    //This is for things like the do/while statement, where a function is in the 
                    // while() part and can't be dumped in front of the do/while
                    string MethodName = StringUtils.RandomString(10, true);
                    string typeDefs = "";
                    ObjectList arguements = null;
                    if (LocalMethodArguements.TryGetValue(fc.Id, out arguements))
                    {
                        // print the state arguments, if any
                        foreach (SYMBOL kid in arguements)
                        {
                            if (kid is ArgumentDeclarationList)
                            {
                                ArgumentDeclarationList ADL = (ArgumentDeclarationList) kid;
                                typeDefs += (GenerateArgumentDeclarationList(ADL)) + ",";
                            }
                        }
                    }
                    if (typeDefs.Length != 0)
                        typeDefs = typeDefs.Remove(typeDefs.Length - 1);

                    string newMethod = string.Format("private {0} {1}({2}, out bool ahwowuerogng)", rettype, MethodName,
                                                     typeDefs);
                    newMethod += (Generate("{"));
                    newMethod += (Generate("ahwowuerogng = true;"));
                    Mname = StringUtils.RandomString(10, true);
                    newMethod += (Generate("System.Collections.IEnumerator " + Mname + " = "));
                    newMethod += (Generate(string.Format("{0}(", CheckName(fc.Id)), fc));
                    newMethod += (tmpVal.ToString());
                    newMethod += (Generate(");"));

                    newMethod += (Generate(" try {"));
                    newMethod += (Generate(Mname + ".MoveNext();"));
                    newMethod += (Generate("  if(" + Mname + ".Current != null)"));
                    newMethod += (Generate("   return (" + rettype + ")" + Mname + ".Current;"));
                    newMethod += (Generate("  }")); //End of try
                    newMethod += (Generate(" catch(Exception ex) "));
                    newMethod += (Generate("  {"));
                    newMethod += (Generate("  }")); //End of catch
                    newMethod += (Generate("ahwowuerogng = true;"));
                    newMethod += (Generate("return default(" + rettype + ");")); //End while
                    newMethod += "}";
                    MethodsToAdd.Add(newMethod);

                    List<string> fCalls = new List<string>();
                    string boolname = StringUtils.RandomString(10, true);
                    fCalls.Add(Generate("bool " + boolname + " = true;"));
                    retVal.Append(MethodName + "(" + tmpVal + ", out " + boolname + ")");
                    lock (FuncCalls)
                        FuncCalls.AddRange(fCalls);
                }
                else
                {
                    //Function calls are added to the DumpFunc command, and will be dumped safely before the
                    // statement that occurs here, so we don't have to deal with the issues behind having 
                    // { and } in this area.
                    Mname = StringUtils.RandomString(10, true);
                    string Exname = StringUtils.RandomString(10, true);
                    List<string> fCalls = new List<string>
                                              {
                                                  Generate("string " + Exname + " =  \"\";"),
                                                  Generate("System.Collections.IEnumerator " + Mname + " = "),
                                                  Generate(string.Format("{0}(", CheckName(fc.Id)), fc),
                                                  tmpVal.ToString(),
                                                  Generate(");"),
                                                  Generate("while (true) {"),
                                                  Generate(" try {"),
                                                  Generate("  if(!" + Mname + ".MoveNext())"),
                                                  Generate("   break;"),
                                                  Generate("  }"),
                                                  Generate(" catch(Exception ex) "),
                                                  Generate("  {"),
                                                  Generate("  " + Exname + " = ex.Message;"),
                                                  Generate("  }"),
                                                  Generate(" if(" + Exname + " != \"\")"),
                                                  Generate("   yield return " + Exname + ";"),
                                                  Generate(" else if(" + Mname + ".Current == null || " + Mname +
                                                           ".Current is DateTime)"),
                                                  Generate("   yield return " + Mname + ".Current;"),
                                                  Generate(" else break;"),
                                                  Generate(" }")
                                              };

                    //Let the other things process for a bit here at the end of each enumeration
                    //Let the other things process for a bit here at the end of each enumeration
                    if (NeedRetVal && rettype != "void")
                    {
                        retVal.Append(" (" + rettype + ") " + Mname + ".Current");
                    }
                    lock (FuncCalls)
                        FuncCalls.AddRange(fCalls);
                }
            }
            else
            {
                retVal.Append(Generate(string.Format("{0}(", CheckName(fc.Id)), fc));
                retVal.Append(tmpVal.ToString());

                retVal.Append(Generate(")"));
            }

            //Function calls are first if needed
            return DumpFunc(marc) + retVal + DumpAfterFunc(marc);
        }

        /// <summary>
        ///     Generates the code for a Constant node.
        /// </summary>
        /// <param name="c">The Constant node.</param>
        /// <returns>String containing C# code for Constant c.</returns>
        string GenerateConstant(Constant c)
        {
            // Supprt LSL's weird acceptance of floats with no trailing digits
            // after the period. Turn float x = 10.; into float x = 10.0;
            if ("LSL_Types.LSLFloat" == c.Type)
            {
                int dotIndex = c.Value.IndexOf('.') + 1;
                if (0 < dotIndex && (dotIndex == c.Value.Length || !char.IsDigit(c.Value[dotIndex])))
                    c.Value = c.Value.Insert(dotIndex, "0");
                c.Value = "new LSL_Types.LSLFloat(" + c.Value + ")";
            }
            else if ("LSL_Types.LSLInteger" == c.Type)
            {
                c.Value = "new LSL_Types.LSLInteger(" + c.Value + ")";
            }
            else if ("LSL_Types.LSLString" == c.Type)
            {
                c.Value = "new LSL_Types.LSLString(\"" + c.Value + "\")";
            }

            return Generate(c.Value, c);
        }

        /// <summary>
        ///     Generates the code for a VectorConstant node.
        /// </summary>
        /// <param name="vc">The VectorConstant node.</param>
        /// <returns>String containing C# code for VectorConstant vc.</returns>
        string GenerateVectorConstant(VectorConstant vc)
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append(Generate(string.Format("new {0}(", vc.Type), vc));
            retVal.Append(GenerateNode((SYMBOL) vc.kids.Pop()));
            retVal.Append(Generate(", "));
            retVal.Append(GenerateNode((SYMBOL) vc.kids.Pop()));
            retVal.Append(Generate(", "));
            retVal.Append(GenerateNode((SYMBOL) vc.kids.Pop()));
            retVal.Append(Generate(")"));

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a RotationConstant node.
        /// </summary>
        /// <param name="rc">The RotationConstant node.</param>
        /// <returns>String containing C# code for RotationConstant rc.</returns>
        string GenerateRotationConstant(RotationConstant rc)
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append(Generate(string.Format("new {0}(", rc.Type), rc));
            retVal.Append(GenerateNode((SYMBOL) rc.kids.Pop()));
            retVal.Append(Generate(", "));
            retVal.Append(GenerateNode((SYMBOL) rc.kids.Pop()));
            retVal.Append(Generate(", "));
            retVal.Append(GenerateNode((SYMBOL) rc.kids.Pop()));
            retVal.Append(Generate(", "));
            retVal.Append(GenerateNode((SYMBOL) rc.kids.Pop()));
            retVal.Append(Generate(")"));

            return retVal.ToString();
        }

        /// <summary>
        ///     Generates the code for a ListConstant node.
        /// </summary>
        /// <param name="lc">The ListConstant node.</param>
        /// <returns>String containing C# code for ListConstant lc.</returns>
        string GenerateListConstant(ListConstant lc)
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append(Generate(string.Format("new {0}(", lc.Type), lc));

            foreach (SYMBOL kid in lc.kids)
                retVal.Append(GenerateNode(kid));

            retVal.Append(Generate(")"));

            return retVal.ToString();
        }

        /// <summary>
        ///     Prints a newline.
        /// </summary>
        /// <returns>A newline.</returns>
        string GenerateLine()
        {
            return GenerateLine("");
        }

        /// <summary>
        ///     Prints text, followed by a newline.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <returns>String s followed by newline.</returns>
        string GenerateLine(string s)
        {
            return GenerateLine(s, null);
        }

        /// <summary>
        ///     Prints text, followed by a newline.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <param name="sym">
        ///     Symbol being generated to extract original line
        ///     number and column from.
        /// </param>
        /// <returns>String s followed by newline.</returns>
        string GenerateLine(string s, SYMBOL sym)
        {
            string retstr = Generate(s, sym) + "\n";

            m_CSharpLine++;
            m_CSharpCol = 1;

            return retstr;
        }

        /// <summary>
        ///     Prints text.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <returns>String s.</returns>
        string Generate(string s)
        {
            return Generate(s, null);
        }

        /// <summary>
        ///     Prints text.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <param name="sym">
        ///     Symbol being generated to extract original line
        ///     number and column from.
        /// </param>
        /// <returns>String s.</returns>
        string Generate(string s, SYMBOL sym)
        {
            if (null != sym)
                m_positionMap.Add(new KeyValuePair<int, int>(m_CSharpLine, m_CSharpCol),
                                  new KeyValuePair<int, int>(sym.Line, sym.Position));

            m_CSharpCol += s.Length;

            return s;
        }

        /// <summary>
        ///     Prints text correctly indented, followed by a newline.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <returns>Properly indented string s followed by newline.</returns>
        string GenerateIndentedLine(string s)
        {
            return GenerateIndentedLine(s, null);
        }

        /// <summary>
        ///     Prints text correctly indented, followed by a newline.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <param name="sym">
        ///     Symbol being generated to extract original line
        ///     number and column from.
        /// </param>
        /// <returns>Properly indented string s followed by newline.</returns>
        string GenerateIndentedLine(string s, SYMBOL sym)
        {
            string retstr = GenerateIndented(s, sym) + "\n";

            m_CSharpLine++;
            m_CSharpCol = 1;

            return retstr;
        }

        /// <summary>
        ///     Prints text correctly indented.
        /// </summary>
        /// <param name="s">String of text to print.</param>
        /// <param name="sym">
        ///     Symbol being generated to extract original line
        ///     number and column from.
        /// </param>
        /// <returns>Properly indented string s.</returns>
        string GenerateIndented(string s, SYMBOL sym)
        {
            string retstr = Indent() + s;

            if (null != sym)
                m_positionMap.Add(new KeyValuePair<int, int>(m_CSharpLine, m_CSharpCol),
                                  new KeyValuePair<int, int>(sym.Line, sym.Position));

            m_CSharpCol += s.Length;

            return retstr;
        }

        /// <summary>
        ///     Prints correct indentation.
        /// </summary>
        /// <returns>Indentation based on brace count.</returns>
        string Indent()
        {
            string retstr = string.Empty;

            for (int i = 0; i < m_braceCount; i++)
                for (int j = 0; j < m_indentWidth; j++)
                {
                    retstr += " ";
                    m_CSharpCol++;
                }

            return retstr;
        }

        /// <summary>
        ///     Returns the passed name with an underscore prepended if that name is a reserved word in C#
        ///     and not reserved in LSL otherwise it just returns the passed name.
        ///     This makes no attempt to cache the results to minimise future lookups. For a non trivial
        ///     scripts the number of unique identifiers could easily grow to the size of the reserved word
        ///     list so maintaining a list or dictionary and doing the lookup there firstwould probably not
        ///     give any real speed advantage.
        ///     I believe there is a class Microsoft.CSharp.CSharpCodeProvider that has a function
        ///     CreateValidIdentifier(str) that will return either the value of str if it is not a C#
        ///     key word or "_"+str if it is. But availability under Mono?
        /// </summary>
        string CheckName(string s)
        {
            if (CSReservedWords.IsReservedWord(s))
                return "@" + s;
            else
            {
                /*VarRename var;
                if(VariablesToRename.TryGetValue(s, out var))
                {
                    Console.WriteLine("[CSCodeGenerator]: found var needing renamed!");
                    if (var.HasBeenAssigned)
                        s = var.NewVarName;
                    else
                    {
                        s = var.OldVarName;
                        var.HasBeenAssigned = true;
                        VariablesToRename[s] = var;
                    }
                }*/
                return s;
            }
        }

        string DumpFunc(bool marc)
        {
            string ret = "";

            if (!marc)
                return ret;

            FuncCntr = false;

            lock (FuncCalls) {
                if (FuncCalls.Count == 0)
                    return ret;
            }

            lock (FuncCalls)
            {
                foreach (string s in FuncCalls)
                    ret += GenerateIndentedLine(s);
                FuncCalls.Clear();
            }
            return ret;
        }

        string DumpAfterFunc(bool marc)
        {
            string ret = "";

            if (!marc)
                return ret;

            FuncCntr = false;

            lock (FuncCalls) {
                if (AfterFuncCalls.Count == 0)
                    return ret;
            }

            lock (FuncCalls)
            {
                foreach (string s in AfterFuncCalls)
                    ret += GenerateIndentedLine(s);
                AfterFuncCalls.Clear();
            }
            return ret;
        }

        bool FuncCallsMarc()
        {
            if (FuncCntr)
                return false;
            FuncCntr = true;
            return true;
        }

        #region Nested type: GlobalVar

        class GlobalVar
        {
            public string Type;
            public string Value;
        }

        #endregion

        #region Nested type: VarRename

        class VarRename
        {
            //public string NewVarName;
            //public bool HasBeenAssigned;
            //public string OldVarName;
        }

        #endregion
    }
}
