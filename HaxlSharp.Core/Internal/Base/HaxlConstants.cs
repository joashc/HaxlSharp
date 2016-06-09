
using System;
using System.Text.RegularExpressions;

namespace HaxlSharp.Internal
{
    public static partial class Base
    {
        /// <summary>
        /// We prefix with "<>" so we can't clash with actual bound variable names. 
        /// </summary>
        public const string HAXL_RESULT_NAME = "<>HAXL_RESULT";

        /// <summary>
        /// All transparent identifiers start with this prefix.
        /// </summary>
        public const string TRANSPARENT_PREFIX = "<>h__Trans";

        /// <summary>
        /// We mark let expressions with this prefix.
        /// </summary>
        public const string LET_PREFIX = "<>HAXL_LET";

        /// <summary>
        /// Annotate let arguments with the let prefix.
        /// </summary>
        public static string PrefixLet(string letVarName)
        {
            return $"{LET_PREFIX}{letVarName}";
        }

        /// <summary>
        /// Combines variable names with block numbers.
        /// </summary>
        public static string PrefixedVariable(int blockNumber, string variableName)
        {
            return $"({blockNumber}) {variableName}";
        }

        public static int GetBlockNumber(string bindTo)
        {
            if (bindTo == HAXL_RESULT_NAME) return 0;
            var regex = @"^\((\d+)\).*$";
            var match = Regex.Match(bindTo, regex);
            if (match.Groups.Count < 2)  throw new ArgumentException("Invalid bind variable name");
            return int.Parse(match.Groups[1].Value);
        }
    }
}
