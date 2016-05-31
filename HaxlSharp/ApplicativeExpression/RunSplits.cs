using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{

    public static class RunSplits
    {
        public static A FetchId<A>(Identity<A> id)
        {
            return id.val;
        }

        public static object FetchId(object id)
        {
            return ((dynamic)id).val;
        }

        public static async Task<A> Run<A>(SplitApplicatives<A> splits)
        {
            var rebindTransparent = new RebindTransparent();
            var boundVariables = new Dictionary<string, object>();
            A final = default(A);
            foreach (var segment in splits.Segments)
            {
                var segmentTasks = segment.Expressions.Select(exp =>
                {
                    var rewritten = rebindTransparent.Rewrite(exp);
                    var bindTo = splits.NameQueue.Dequeue();
                    return Task.Factory.StartNew(() =>
                    {
                        var result = rewritten.Compile().DynamicInvoke(boundVariables);
                        boundVariables[bindTo] = FetchId(result);
                    });
                });
                await Task.WhenAll(segmentTasks);
            }
            var finalProject = rebindTransparent.Rewrite(splits.FinalProject);
            final = (A)finalProject.Compile().DynamicInvoke(boundVariables);
            return final;
        }
    }
}
