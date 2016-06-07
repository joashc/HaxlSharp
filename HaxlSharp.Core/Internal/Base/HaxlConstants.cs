
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
        /// Combines variable names with block numbers.
        /// </summary>
        public static string PrefixedVariable(int blockNumber, string variableName)
        {
            return $"({blockNumber}) {variableName}";
        }
    }
}
