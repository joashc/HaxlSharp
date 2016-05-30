using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class SplitApplicatives<A>
    {
        public readonly Expr<A> Expression;
        public readonly IEnumerable<ApplicativeGroup> Segments;
    }

    public static class Splitter
    {
        public static Tuple<bool, IEnumerable<ApplicativeGroup>> Split<A>(Expr<A> expression)
        {
            var segments = new List<ApplicativeGroup>();

            var currentSegment = new ApplicativeGroup();
            var first = expression.Binds.First();
            var firstApplicativeInfo = DetectApplicative.CheckApplicative(first.Bind);

            currentSegment.Expressions.Add(first);
            currentSegment.BoundVariables.AddRange(firstApplicativeInfo.Free.Select(f => f.Name));
            bool firstSplit = false;
            if (firstApplicativeInfo.Free.Count > 1)
            {
                firstSplit = true;
                currentSegment.BoundVariables.RemoveAll(a => true);
            }
            var rest = expression.Binds.Skip(1);

            foreach (var bind in rest)
            {
                var info = DetectApplicative.CheckApplicative(bind.Bind);
                var split = info.Bound.Any(b => currentSegment.BoundVariables.Contains(b));
                currentSegment.BoundVariables.AddRange(info.Free.Select(f => f.Name));
                if (split)
                {
                    segments.Add(currentSegment);
                    currentSegment = new ApplicativeGroup();
                }
                currentSegment.Expressions.Add(bind);
            }
            segments.Add(currentSegment);
            return new Tuple<bool, IEnumerable<ApplicativeGroup>>(firstSplit, segments);
        }
    }
}
