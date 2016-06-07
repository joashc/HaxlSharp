using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static HaxlSharp.Internal.Base;
using HaxlSharp.Internal;

namespace HaxlSharp
{
    /// <summary>
    /// Fetch a result.
    /// </summary>
    /// <fetch>
    /// This is a free monad that leaves its expression tree open for inspection.
    /// </fetch>
    public interface Fetch<A>
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        IEnumerable<BindProjectPair> CollectedExpressions { get; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        LambdaExpression Initial { get; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        Haxl ToHaxlFetch(string bindTo, Scope scope);
    }

    /// <summary>
    /// Monadic bind that just collects the query expression tree for inspection.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
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

        public Haxl ToHaxlFetch(string bindTo, Scope scope)
        {
            var bindSplit = QuerySplitter.Bind(CollectedExpressions, Initial);
            return SplitQuery.ToFetch(bindSplit, bindTo, new Scope(scope));
        }
    }

    /// <summary>
    /// All binds must terminate in a FetchNode.
    /// </summary>
    public abstract class FetchNode<A> : Fetch<A>
    {
        private static readonly IEnumerable<BindProjectPair> emptyList = new List<BindProjectPair>();
        public IEnumerable<BindProjectPair> CollectedExpressions { get { return emptyList; } }
        public LambdaExpression Initial { get { return Expression.Lambda(Expression.Constant(this)); } }
        public abstract Haxl ToHaxlFetch(string bindTo, Scope scope);
    }

    /// <summary>
    /// Wraps a primitive request type.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class Request<A> : FetchNode<A>
    {
        public readonly Returns<A> request;
        public Request(Returns<A> request)
        {
            this.request = request;
        }

        public override Haxl ToHaxlFetch(string bindTo, Scope scope)
        {
            Func<Task<object>, Haxl> DoneFromTask =
                t => Haxl.FromFunc(
                    c => Done.New(_ => scope.Add(bindTo, t.Result))
                );

            return Haxl.FromFunc(cache =>
            {
                var cacheResult = cache.Lookup(request);
                return cacheResult.Match<Result>
                (
                    notFound =>
                    {
                        var blocked = new BlockedRequest(request, request.GetType(), bindTo);
                        cache.Insert(request, blocked);
                        return Blocked.New(
                            new List<BlockedRequest> { blocked },
                            DoneFromTask(blocked.Resolver.Task)
                        );
                    },
                    found =>
                    {
                        var task = found.Resolver.Task;
                        if (task.IsCompleted) return Done.New(_ =>
                            {
                                var result = task.Result;
                                return scope.Add(bindTo, result);
                            });
                        else return Blocked.New(
                            new List<BlockedRequest>(),
                            DoneFromTask(task)
                        );
                    }
               );
            });
        }

        public Type RequestType { get { return request.GetType(); } }
    }

    /// <summary>
    /// Applicative sequence.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RequestSequence<A, B> : FetchNode<IEnumerable<B>>
    {
        public readonly IEnumerable<A> List;
        public readonly Func<A, Fetch<B>> Bind;
        public RequestSequence(IEnumerable<A> list, Func<A, Fetch<B>> bind)
        {
            List = list;
            Bind = bind;
        }

        public override Haxl ToHaxlFetch(string bindTo, Scope parentScope)
        {
            var childScope = new Scope(parentScope);
            var fetches = List.Select(Bind)
                              .Select((f, i) => f.ToHaxlFetch(i.ToString(), childScope));

            var concurrent = fetches.Aggregate((f1, f2) => f1.Applicative(f2));
            return concurrent.Bind(scope => Haxl.FromFunc(cache => Done.New(_ =>
            {
                var values = scope.ShallowValues.Select(v => (B)v);
                return scope.WriteParent(bindTo, values);
            }
            )));
        }
    }

    /// <summary>
    /// Maps the result of the fetch with given function.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class Select<A, B> : FetchNode<B>
    {
        public readonly Fetch<A> Fetch;
        public readonly Expression<Func<A, B>> Map;
        public Select(Fetch<A> fetch, Expression<Func<A, B>> map)
        {
            Fetch = fetch;
            Map = map;
        }

        public override Haxl ToHaxlFetch(string bindTo, Scope parentScope)
        {
            return Fetch.ToHaxlFetch(bindTo, parentScope).Map(scope =>
            {
                var value = (A)scope.GetValue(bindTo);
                return scope.WriteParent(bindTo, Map.Compile()(value));
            });
        }
    }

    /// <summary>
    /// Monad instance for Fetch.
    /// </summary>
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

        public static Fetch<IEnumerable<B>> SelectFetch<A, B>(this IEnumerable<A> list, Func<A, Fetch<B>> bind)
        {
            return new RequestSequence<A, B>(list, bind);
        }

        public static async Task<A> FetchWith<A>(this Fetch<A> fetch, Fetcher fetcher, HaxlCache cache)
        {
            var run = fetch.ToHaxlFetch(HAXL_RESULT_NAME, Scope.New());
            var scope = await RunFetch.Run(run, Scope.New(), fetcher.FetchBatch, cache);
            return (A)scope.GetValue(HAXL_RESULT_NAME);
        }
    }
}
