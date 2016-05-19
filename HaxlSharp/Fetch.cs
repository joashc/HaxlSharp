using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Haxl;

namespace HaxlSharp
{
    public class Fetch<A>
    {
        public Task<Result<A>> Result { get; }
        public Fetch(Task<Result<A>> result)
        {
            Result = result;
        }
    }

    public interface Result<A>
    {
        X Run<X>(Fetcher<A, X> fetcher);
    }

    public class Done<A> : Result<A>
    {
        public readonly A result;
        public Done(A result)
        {
            this.result = result;
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Done(result);
        }
    }

    public class Blocked<A> : Result<A>
    {
        public readonly Fetch<A> fetch;
        public readonly IEnumerable<Task> blockedRequests;
        public Blocked(Fetch<A> fetch, IEnumerable<Task> blockedRequests)
        {
            this.fetch = fetch;
            this.blockedRequests = blockedRequests;
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Blocked(fetch, blockedRequests);
        }
    }

    public static class FetchExt
    {
        public static Fetch<B> Select<A, B>(this Fetch<A> fetch, Func<A, B> f)
        {
            var task = Task.Run(async () =>
            {
                var resultA = await fetch.Result;
                if (resultA is Done<A>)
                {
                    var doneA = resultA as Done<A>;
                    return Done(f(doneA.result));
                }
                if (resultA is Blocked<A>)
                {
                    var blockedA = resultA as Blocked<A>;
                    var taskB = Task.Run(async () =>
                    {
                        var blockedResult = await blockedA.fetch.Result;
                        var a = await blockedResult.RunFetch();
                        return Done(f(a));
                    });
                    return Blocked(Fetch(taskB), blockedA.blockedRequests);
                }
                throw new ArgumentException();
            });
            return new Fetch<B>(task);
        }

        public static Fetch<B> SelectMany<A, B>(this Fetch<A> fetch, Func<A, Fetch<B>> bind)
        {
            var task = Task.Run(async () =>
            {
                var resultA = await fetch.Result;
                if (resultA is Done<A>)
                {
                    var doneA = resultA as Done<A>;
                    return bind(doneA.result);
                }
                if (resultA is Blocked<A>)
                {
                    var blockedA = resultA as Blocked<A>;
                    var newFetch = JoinFetch(blockedA.fetch.Select(bind));
                    return Fetch(Blocked(newFetch, blockedA.blockedRequests));
                }
                throw new ArgumentException();
            });
            return task.Result;
        }

        public static Fetch<A> JoinFetch<A>(Fetch<Fetch<A>> nested)
        {
            var result = nested.Result.Result.RunFetch().Result;
            return result;
        }

        public static Fetch<C> SelectMany<A, B, C>(this Fetch<A> fetch, Func<A, Fetch<B>> bind, Func<A, B, C> project)
        {
            return JoinFetch(fetch.Select(t => bind(t).Select(u => project(t, u))));
        }

        public static Task<A> RunFetch<A>(this Result<A> fetch)
        {
            return fetch.Run(new RunFetch<A>());
        }

        public static IEnumerable<Fetch<B>> Select<A, B>(this IEnumerable<Fetch<A>> fetches, Func<A, B> f)
        {
            foreach (var fetch in fetches)
            {
                yield return fetch.Select(f);
            }
        }

        public static IEnumerable<Fetch<B>> SelectMany<A, B>(this IEnumerable<Fetch<A>> fetches, Func<A, Fetch<B>> bind)
        {
            foreach (var fetch in fetches)
            {
                yield return fetch.SelectMany(bind);
            }
        }

        public static IEnumerable<Fetch<C>> SelectMany<A, B, C>(this IEnumerable<Fetch<A>> fetches, Func<A, Fetch<B>> bind, Func<A, B, C> project)
        {
            foreach (var fetch in fetches)
            {
                yield return fetch.SelectMany(bind, project);
            }
        }

        /// <summary>
        /// Default to using recursion depth limit of 100
        /// </summary>
        public static Fetch<IEnumerable<A>> Sequence<A>(this IEnumerable<Fetch<A>> dists)
        {
            return SequenceWithDepth(dists, 100);
        }

        /// <summary>
        /// This implementation sort of does trampolining to avoid stack overflows,
        /// but for performance reasons it recursively divides the list
        /// into groups up to a recursion depth, instead of trampolining every iteration.
        ///
        /// This should limit the recursion depth to around 
        /// $$s\log_{s}{n}$$
        /// where s is the specified recursion depth limit
        /// </summary>
        public static Fetch<IEnumerable<A>> SequenceWithDepth<A>(this IEnumerable<Fetch<A>> dists, int recursionDepth)
        {
            var sections = dists.Count() / recursionDepth;
            if (sections <= 1) return RunSequence(dists);
            return from nested in SequenceWithDepth(SequencePartial(dists, recursionDepth), recursionDepth)
                   select nested.SelectMany(a => a);
        }

        /// <summary>
        /// `sequence` can be implemented as
        /// sequence xs = foldr (liftM2 (:)) (return []) xs
        /// </summary>
        private static Fetch<IEnumerable<A>> RunSequence<A>(IEnumerable<Fetch<A>> dists)
        {
            return dists.Aggregate(
                Fetch(Done<IEnumerable<A>>(new List<A>())),
                (listFetch, aFetch) => from a in aFetch
                                       from list in listFetch
                                       select Append(list, a)
            );
        }

        /// <summary>
        /// Divide a list of distributions into groups of given size, then runs sequence on each group
        /// </summary>
        /// <returns>The list of sequenced distribution groups</returns>
        private static IEnumerable<Fetch<IEnumerable<A>>> SequencePartial<A>(IEnumerable<Fetch<A>> dists, int groupSize)
        {
            var numGroups = dists.Count() / groupSize;
            return Enumerable.Range(0, numGroups)
                             .Select(groupNum => RunSequence(dists.Skip(groupNum * groupSize).Take(groupSize)));
        }

    }

}
