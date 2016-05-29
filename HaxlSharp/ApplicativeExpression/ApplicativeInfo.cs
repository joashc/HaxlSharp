using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class ApplicativeInfo
    {
        public readonly bool IsApplicative;
        public readonly IEnumerable<string> BoundVariables;
        public readonly string LastBound;
        public readonly string lastBound; public ApplicativeInfo(bool isApplicative, IEnumerable<string> boundVariables, string lastBound)
        {
            LastBound = lastBound;
            IsApplicative = isApplicative;
            BoundVariables = boundVariables;
        }
    }
}
