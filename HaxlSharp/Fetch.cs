using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Haxl;

namespace HaxlSharp
{
    public interface Fetch<A>
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        IEnumerable<BindProjectPair> CollectedExpressions { get; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        LambdaExpression Initial { get; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        HaxlFetch ToHaxlFetch(string bindTo, Scope scope);
    }

    public class Bind<A, B, C> : Fetch<C>
    {
        private readonly IEnumerable<BindProjectPair> _binds;
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return _binds; } }
        public readonly Fetch<A> Fetch;

        public Bind(IEnumerable<BindProjectPair> binds, Fetch<A> expr)
        {
            _binds = binds;
            Fetch = expr;
        }

        public LambdaExpression Initial { get { return Fetch.Initial; } }

        public HaxlFetch ToHaxlFetch(string bindTo, Scope scope)
        {
            var bindSplit = QuerySplitter.Bind(CollectedExpressions, Initial);
            return SplitQuery.ToFetch(bindSplit, bindTo, new Scope(scope));
        }
    }

    public abstract class FetchNode<A> : Fetch<A>
    {
        private static readonly IEnumerable<BindProjectPair> emptyList = new List<BindProjectPair>();
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return emptyList; } }
        public LambdaExpression Initial { get { return Expression.Lambda(Expression.Constant(this)); } }
        public abstract HaxlFetch ToHaxlFetch(string bindTo, Scope scope);
    }

    public class Request<A> : FetchNode<A>
    {
        public readonly Returns<A> request;
        public Request(Returns<A> request)
        {
            this.request = request;
        }

        public override HaxlFetch ToHaxlFetch(string bindTo, Scope scope)
        {
            return HaxlFetch.FromFunc(() =>
            {
                var blocked = new BlockedRequest(request, request.GetType(), bindTo);
                return Blocked.New(
                    new List<BlockedRequest> { blocked },
                    HaxlFetch.FromFunc(() => Done.New(_ =>
                    {
                        var result = blocked.Resolver.Task.Result;
                        return scope.Add(bindTo, result);
                    }))
                );
            });
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

        public override HaxlFetch ToHaxlFetch(string bindTo, Scope parentScope)
        {
            var childScope = new Scope(parentScope);
            var fetches = List.Select(Bind)
                              .Select((f, i) => f.ToHaxlFetch(i.ToString(), childScope));

            var concurrent = fetches.Aggregate((f1, f2) => f1.Applicative(f2));
            return concurrent.Bind(scope => HaxlFetch.FromFunc(() => Done.New(_ =>
            {
                var values = scope.ShallowValues.Select(v => (B)v);
                return scope.WriteParent(bindTo, values);
            }
            )));
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

        public override HaxlFetch ToHaxlFetch(string bindTo, Scope parentScope)
        {
            return Fetch.ToHaxlFetch(bindTo, parentScope).Map(scope =>
            {
                var value = (A)scope.GetValue(bindTo);
                return scope.Add(bindTo, Map.Compile()(value));
            });
        }
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

        public static async Task<A> FetchWith<A>(this Fetch<A> fetch, Fetcher fetcher)
        {
            var run = fetch.ToHaxlFetch(HAXL_RESULT_NAME, Scope.New());
            var scope = await SplitQuery.RunFetch(run, Scope.New(), fetcher);
            return (A)scope.GetValue(HAXL_RESULT_NAME);
        }
    }
}
