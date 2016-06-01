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

    public interface FetchVisitor<C, X>
    {
        X Bind<A, B>(IEnumerable<BindProjectPair> expressions, Fetch<A> fetch);
        X Request(Returns<C> request, Type requestType);
        X RequestSequence<B>(IEnumerable<B> list, Func<B, Fetch<C>> bind);
    }


    public class Bind<A, B, C> : Fetch<C>
    {
        public Bind(IEnumerable<BindProjectPair> binds, Fetch<A> expr)
        {
            _binds = binds;
            Fetch = expr;
        }

        private readonly IEnumerable<BindProjectPair> _binds;
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return _binds; } }

        public LambdaExpression Initial
        {
            get { return Fetch.Initial; }
        }

        public readonly Fetch<A> Fetch;

        public C RunFetch(Fetcher fetch)
        {
            var split = Splitter.Split(this);
            return RunSplits.Run(split, fetch).Result;
        }
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

    public class Request<A> : FetchNode<A>
    {
        public readonly Returns<A> request;
        public Request(Returns<A> request)
        {
            this.request = request;
        }

        public Type RequestType { get { return request.GetType(); } }
    }

    public class RequestSequence<A, B> : FetchNode<IEnumerable<B>>
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

    public class Select<A, B> : FetchNode<B>, Fetchable
    {
        public readonly Fetch<A> Fetch;
        public readonly Expression<Func<A, B>> Map;
        public Select(Fetch<A> fetch, Expression<Func<A, B>> map)
        {
            Fetch = fetch;
            Map = map;
        }

        public LambdaExpression MapExpression
        {
            get { return Map; }
        }

        public object RunFetch(Fetcher fetcher)
        {
            var split = Splitter.Split(Fetch);
            return RunSplits.Run(split, fetcher).Result;
        }
    }

    public interface Fetchable
    {
        object RunFetch(Fetcher fetcher);
        LambdaExpression MapExpression { get; }
    }

    public class FetchResult<A> : FetchNode<A>, HoldsObject
    {
        public A Val;

        public object Value
        {
            get
            {
                return Val;
            }
        }

        public FetchResult(A val)
        {
            Val = val;
        }
    }

    public interface HoldsObject
    {
        object Value { get; }
    }

    public static class ExprExt
    {
        public static Fetch<B> Select<A, B>(this Fetch<A> self, Expression<Func<A, B>> f)
        {
            return new Select<A, B>(self, f);
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
