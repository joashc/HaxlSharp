using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    public static partial class Haxl
    {
        /// <summary>
        /// We prefix with "<>" so we can't clash with actual bound variable names. 
        /// </summary>
        public const string HAXL_RESULT_NAME = "<>HAXL_RESULT";

        /// <summary>
        /// All transparent identifiers start with this prefix.
        /// </summary>
        public const string TRANSPARENT_PREFIX = "<>h__Trans";
    }
}
