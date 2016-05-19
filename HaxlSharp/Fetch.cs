﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetch<A> 
    {
        X Run<X>(Fetcher<A, X> fetcher);
    }

    public interface Result<A> : Fetch<A>
    {
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
        public readonly Task<Fetch<A>> fetch;
        public readonly IEnumerable<Task> blockedRequests;
        public Blocked(Task<Fetch<A>> fetch, IEnumerable<Task> blockedRequests)
        {
            this.fetch = fetch;
            this.blockedRequests = blockedRequests;
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Blocked(fetch, blockedRequests);
        }
    }

    public class Bind<B, A> : Fetch<A>
    {
        public readonly Fetch<B> fetch;
        public readonly Func<B, Fetch<A>> bind;
        public Bind(Fetch<B> fetch, Func<B, Fetch<A>> bind)
        {
            this.fetch = fetch;
            this.bind = bind;
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            return fetcher.Bind(fetch, bind);
        }
    }

    public class Cons<B, A> : Fetch<A>
    {
        public readonly Fetch<B> fetch;
        public readonly Fetch<A> fetchA;
        public Cons(Fetch<B> fetch, Fetch<A> fetchA)
        {
            this.fetch = fetch;
            this.fetchA = fetchA;
        }

        public X Run<X>(Fetcher<A, X> fetcher)
        {
            throw new NotImplementedException();
        }
    }

    public static class FetchExt
    {
        public static Cons<B, A> Cons<A, B, C>(Fetch<A> f1, Fetch<B> f2, Fetch<C> f3)
        {
            return new Cons<B, A>(f2, new Cons<C, A>(f3, f1));
        }
        public static Fetch<B> Select<A, B>(this Fetch<A> fetch, Func<A, B> f)
        {
            return new Bind<A, B>(fetch, a => new Done<B>(f(a)));
        }

        public static Fetch<B> SelectMany<A, B>(this Fetch<A> fetch, Func<A, Fetch<B>> bind)
        {
            return new Bind<A, B>(fetch, bind);
        }

        public static Fetch<C> SelectMany<A, B, C>(this Fetch<A> fetch, Func<A, Fetch<B>> bind, Func<A, B, C> project)
        {
            return new Bind<A, C>(fetch, a =>
                new Bind<B, C>(bind(a), b => 
                    new Done<C>(project(a, b))
                )
            );
        }

        public static Task<A> Fetch<A> (this Fetch<A> fetch)
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
                Haxl.Done<IEnumerable<A>>(new List<A>()),
                (listFetch, aFetch) => from a in aFetch
                                     from list in listFetch
                                     select Haxl.Append(list, a)
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
