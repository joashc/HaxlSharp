using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class BindSegments
    {
        public readonly IEnumerable<ApplicativeGroup> Segments;
        public BindSegments(IEnumerable<ApplicativeGroup> segments)
        {
            Segments = segments;
        }
    }
}
