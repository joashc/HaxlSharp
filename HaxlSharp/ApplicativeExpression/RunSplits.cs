using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var type = id.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Identity<>))
            {
                return ((dynamic)id).val;
            }
            return id;
        }

        public static async Task<A> Run<A>(SplitApplicatives<A> splits)
        {
            if (splits.IsIdentity)
            {
                return await Task.FromResult((splits.Expression as Identity<A>).val);
            }

            var rebindTransparent = new RebindTransparent();
            var boundVariables = new Dictionary<string, object>();
            A final = default(A);
            int i = 1;
            foreach (var segment in splits.Segments)
            {
                if (segment.IsProjectGroup)
                {
                    var project = segment.Expressions.First();
                    var rewritten = rebindTransparent.Rewrite(project);
                    var bindTo = splits.NameQueue.Dequeue();
                    var id = rewritten.Compile().DynamicInvoke(boundVariables);
                    var result = FetchId(id);
                    Debug.WriteLine($"Projected {bindTo}: {result}\n");
                    boundVariables[bindTo] = result;
                    continue;
                }
                if (!segment.Expressions.Any()) continue;
                Debug.WriteLine($"=== Batch {i++} ===");
                var segmentTasks = segment.Expressions.Select(exp =>
                {
                    var rewritten = rebindTransparent.Rewrite(exp);
                    var bindTo = splits.NameQueue.Dequeue();
                    return Task.Factory.StartNew(() =>
                    {
                        var id = rewritten.Compile().DynamicInvoke(boundVariables);
                        var result = FetchId(id);
                        Debug.WriteLine($"Fetched {bindTo}: {result}");
                        boundVariables[bindTo] = result;
                    });
                });
                await Task.WhenAll(segmentTasks);
                Debug.WriteLine("");
            }
            var finalProject = rebindTransparent.Rewrite(splits.FinalProject);
            final = (A)finalProject.Compile().DynamicInvoke(boundVariables);

            Debug.WriteLine($"Result: {final}");
            return final;
        }
    }
}
