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
        SplitFetch<A> Split(FetchSplitter<A> splitter);
        Task<A> FetchWith(Fetcher fetcher, int nestLevel = 0);
    }

    public interface SplitFetch<A>
    {
        X Run<X>(SplitHandler<A, X> handler);
        IEnumerable<FetchResult> CollectRequests(string bindTo);
    }

    public interface SplitHandler<A, X>
    {
        X Bind(SplitBind<A> splits);
        X Request(Returns<A> request, Type requestType);
        X RequestSequence<B, Item>(IEnumerable<B> list, Func<B, Fetch<Item>> bind);
        X Select<B>(Fetch<B> fetch, Expression<Func<B, A>> fmap);
        X Result(A result);
    }

    public interface FetchSplitter<C>
    {
        SplitFetch<C> Bind<A, B>(Fetch<C> bind);
        SplitFetch<C> Pass(Fetch<C> unsplittable);
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

        public SplitFetch<C> Split(FetchSplitter<C> splitter)
        {
            return splitter.Bind<A, B>(this);
        }

        public Task<C> FetchWith(Fetcher fetcher, int nestLevel)
        {
            var split = Split(new Splitta<C>());
            var runner = new SplitRunner<C>(fetcher, nestLevel);
            return split.Run(runner);
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

    public abstract class FetchNode<A> : Fetch<A>, SplitFetch<A>
    {
        private static readonly IEnumerable<BindProjectPair> emptyList = new List<BindProjectPair>();
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return emptyList; } }
        public LambdaExpression Initial { get { return Expression.Lambda(Expression.Constant(this)); } }

        public abstract X Run<X>(SplitHandler<A, X> handler);

        public SplitFetch<A> Split(FetchSplitter<A> splitter)
        {
            return splitter.Pass(this);
        }

        public Task<A> FetchWith(Fetcher fetcher, int nestLevel)
        {
            var split = Split(new Splitta<A>());
            var runner = new SplitRunner<A>(fetcher, nestLevel);
            return split.Run(runner);
        }

        public IEnumerable<FetchResult> CollectRequests(string bindTo)
        {
            return Run(new RequestCollector<A>(bindTo));
        }
    }

    public class Request<A> : FetchNode<A>
    {
        public readonly Returns<A> request;
        public Request(Returns<A> request)
        {
            this.request = request;
        }

        public Type RequestType { get { return request.GetType(); } }

        public override X Run<X>(SplitHandler<A, X> handler)
        {
            return handler.Request(request, RequestType);
        }
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

        public override X Run<X>(SplitHandler<IEnumerable<B>, X> handler)
        {
            return handler.RequestSequence(List, Bind);
        }
    }

    public class Select<A, B> : FetchNode<B>
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

        public override X Run<X>(SplitHandler<B, X> handler)
        {
            return handler.Select(Fetch, Map);
        }
    }

    public class FetchResult<A> : FetchNode<A>
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

        public override X Run<X>(SplitHandler<A, X> handler)
        {
            return handler.Result(Val);
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

        public static SplitFetch<A> Split<A>(this Fetch<A> expr)
        {
            return expr.Split(new Splitta<A>());
        }

    }

}
