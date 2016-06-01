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

        public IEnumerable<B> FetchSequence(Fetcher fetcher)
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

        public static Task<A> Fetch<A>(this Fetch<A> expr)
        {
            var split = Splitter.Split(expr);
            return RunSplits.Run(split, new DefaultFetcher(new Dictionary<Type, Func<GenericRequest, Result>>(), new Dictionary<Type, Func<GenericRequest, Task<Result>>>()));
        }

    }

}
