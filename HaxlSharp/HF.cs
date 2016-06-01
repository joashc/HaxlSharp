using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public interface Fetch<A>
    {
        IEnumerable<BindProjectPair> CollectedExpressions { get; }
        LambdaExpression Initial { get; }
    }

    public class Bind<A, B, C> : Fetch<C>
    {
        public Bind(IEnumerable<BindProjectPair> binds, Fetch<A> expr)
        {
            _binds = binds;
            Expr = expr;
        }

        private readonly IEnumerable<BindProjectPair> _binds;
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return _binds; } }

        public LambdaExpression Initial
        {
            get { return Expr.Initial; }
        }

        public readonly Fetch<A> Expr;
    }

    public class BindProjectPair
    {
        public BindProjectPair(LambdaExpression bind, LambdaExpression project)
        {
            Bind = bind;
            Project = project;
        }

        public readonly LambdaExpression Bind;
        public readonly LambdaExpression Project;
    }

    public class ApplicativeGroup
    {
        public ApplicativeGroup(bool isProjectGroup = false, List<LambdaExpression> expressions = null, List<string> boundVariables = null)
        {
            Expressions = expressions ?? new List<LambdaExpression>();
            BoundVariables = boundVariables ?? new List<string>();
            IsProjectGroup = isProjectGroup;
        }

        public readonly List<LambdaExpression> Expressions;
        public readonly List<string> BoundVariables;
        public readonly bool IsProjectGroup;
    }

    public abstract class FetchNode<A> : Fetch<A>
    {
        private static readonly IEnumerable<BindProjectPair> emptyList = new List<BindProjectPair>();
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return emptyList; } }
        public LambdaExpression Initial { get { return Expression.Lambda(Expression.Constant(this)); } }
    }

    public class Request<A> : FetchNode<A>, Fetch<A>
    {
        public readonly Returns<A> request;
        public Request(Returns<A> request)
        {
            this.request = request;
        }

        public Type RequestType { get { return request.GetType(); } }
    }

    public class RequestSequence<A, B> : FetchNode<A>, Fetch<IEnumerable<B>>
    {
        public readonly IEnumerable<A> List;
        public readonly Func<A, Fetch<B>> Bind;
        public RequestSequence(IEnumerable<A> list, Func<A, Fetch<B>> bind)
        {
            List = list;
            Bind = bind;
        }

        public IEnumerable<B> FetchMe(Fetcher fetcher)
        {
            var tasks = List.Select(a =>
            {
                var fetch = Bind(a);
                var split = Splitter.Split(fetch);
                return RunSplits.Run(split, fetcher);
            }).ToArray();
            Task.WaitAll(tasks);
            return tasks.Select(t => t.Result).ToList();
        }
    }

    public class FetchResult<A> : FetchNode<A>, Fetch<A>
    {
        public readonly A Val;
        public FetchResult(A val)
        {
            Val = val;
        }
    }

    public static class ExprExt
    {
        public static Fetch<B> Select<A, B>(this Fetch<A> self, Func<A, B> f)
        {
            Expression<Func<A, Fetch<B>>> bind = a => new FetchResult<B>(f(a));
            Expression<Func<A, B, B>> project = (a, b) => b;
            var newBinds = new BindProjectPair(bind, project);
            return new Bind<A, B, B>(self.CollectedExpressions.Append(newBinds), self);
        }

        public static Fetch<C> SelectMany<A, B, C>(this Fetch<A> self, Expression<Func<A, Fetch<B>>> bind, Expression<Func<A, B, C>> project)
        {
            var bindExpression = new BindProjectPair(bind, project);
            var newBinds = self.CollectedExpressions.Append(bindExpression);
            return new Bind<A, B, C>(newBinds, self);
        }

        /// <summary>
        /// Default to using recursion depth limit of 100
        /// </summary>
        public static Fetch<IEnumerable<B>> SelectFetch<A, B>(this IEnumerable<A> list, Func<A, Fetch<B>> bind)
        {
            return new RequestSequence<A, B>(list, bind);
        }

        public static Fetch<IEnumerable<A>> Sequence<A>(this IEnumerable<Fetch<A>> fetches)
        {
            return SequenceWithDepth(fetches, 100);
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
        public static Fetch<IEnumerable<A>> SequenceWithDepth<A>(IEnumerable<Fetch<A>> dists, int recursionDepth)
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
                new FetchResult<IEnumerable<A>>(new List<A>()) as Fetch<IEnumerable<A>>,
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


        public static Task<A> Fetch<A>(this Fetch<A> expr)
        {
            var split = Splitter.Split(expr);
            return RunSplits.Run(split, new DefaultFetcher(new Dictionary<Type, Func<GenericRequest, Result>>(), new Dictionary<Type, Func<GenericRequest, Task<Result>>>()));
        }

    }

}
