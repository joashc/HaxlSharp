using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    public static partial class Base
    {
        public static readonly Func<string, byte[]> StringBytes = new UTF8Encoding().GetBytes;

        public static readonly Func<byte[], string> ToLowerHexString = bs => bs.Aggregate(new StringBuilder(32), (sb, b) => sb.Append(b.ToString("x2"))).ToString();
    }
}
