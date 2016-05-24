using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Haxl;

namespace HaxlSharp
{
    /// <summary>
    /// The free monad over Result.
    /// </summary>
    /// <typeparam name="A"></typeparam>
    public interface Fetch<A>
    {
        X Run<X>(FetchRewriter<A, X> rewriter);
    }

    /// <summary>
    /// Monadic bind.
    /// </summary>
    public class Bind<B, C> : Fetch<C>
    {
        public readonly Fetch<B> fetch;
        public readonly Expression<Func<B, Fetch<C>>> bind;
        public Bind(Fetch<B> fetch, Expression<Func<B, Fetch<C>>> bind)
        {
            this.fetch = fetch;
            this.bind = bind;
        }

        public X Run<X>(FetchRewriter<C, X> rewriter)
        {
            return rewriter.Bind(fetch, bind);
        }
    }

    /// <summary>
    /// Applicative functor.
    /// </summary>
    public class Applicative<A, B, C> : Fetch<C>
    {
        public readonly Fetch<A> fetch1;
        public readonly Func<Fetch<B>> fetch2;
        public readonly Func<A, B, C> project;
        public Applicative(Fetch<A> fetch1, Func<Fetch<B>> fetch2, Func<A, B, C> project)
        {
            this.fetch1 = fetch1;
            this.fetch2 = fetch2;
            this.project = project;
        }

        public X Run<X>(FetchRewriter<C, X> rewriter)
        {
            return rewriter.Applicative(fetch1, fetch2, project);
        }
    }

    /// <summary>
    /// Monad instance for Fetch
    /// </summary>
    public static class FetchExt
    {
        public static Fetch<B> Select<A, B>(this Fetch<A> self, Expression<Func<A, B>> f)
        {
            var compiled = f.Compile();
            return Done(() => compiled(self.Rewrite().RunFetch().Result));
        }

        public static Fetch<B> SelectMany<A, B>(this Fetch<A> self, Expression<Func<A, Fetch<B>>> bind)
        {
            return new Bind<A, B>(self, bind);
        }

        public static Fetch<C> SelectMany<A, B, C>(this Fetch<A> self,
            Expression<Func<A, Fetch<B>>> bind, Expression<Func<A, B, C>> project)
        {
            var compiledBind = bind.Compile();
            var compiledProject = project.Compile();

            if (DetectApplicative.IsApplicative(bind)) return new Applicative<A, B, C>(self, () => compiledBind(default(A)), compiledProject);

            return new Bind<A, C>(self, a => new Bind<B, C>(compiledBind(a),
                b => Done(() => compiledProject(a, b))));
        }

        public static Result<A> Rewrite<A>(this Fetch<A> fetch)
        {
            return fetch.Run(new Rewriter<A>());
        }

        /// <summary>
        /// Default to using recursion depth limit of 100
        /// </summary>
        public static Fetch<IEnumerable<A>> Sequence<A>(this IEnumerable<Fetch<A>> dists)
        {
            return SequenceWithDepth(dists, 10);
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
                Done<IEnumerable<A>>(() => new List<A>()) as Fetch<IEnumerable<A>>,
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
