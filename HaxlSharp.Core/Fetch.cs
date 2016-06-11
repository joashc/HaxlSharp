using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static HaxlSharp.Internal.Base;
using HaxlSharp.Internal;
using System.Diagnostics;

namespace HaxlSharp
{
    /// <summary>
    /// Fetch a result.
    /// </summary>
    /// <fetch>
    /// This is a free monad that leaves its expression tree open for inspection.
    /// </fetch>
    public interface Fetch<A> : Fetchable
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        IEnumerable<BindProjectPair> CollectedExpressions { get; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        LambdaExpression Initial { get; }
    }

    public interface Fetchable
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        Haxl ToHaxlFetch(string bindTo, Scope scope);
    }

    /// <summary>
    /// Monadic bind that just collects the query expression tree for inspection.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class Bind<A, B, C> : Fetch<C>
    {
        public IEnumerable<BindProjectPair> CollectedExpressions { get; }

        public readonly Fetch<A> Fetch;

        public Bind(IEnumerable<BindProjectPair> binds, Fetch<A> expr)
        {
            CollectedExpressions = binds;
            Fetch = expr;
        }

        public LambdaExpression Initial => Fetch.Initial;

        public Haxl ToHaxlFetch(string bindTo, Scope scope)
        {
            var bindSplit = SplitApplicative.SplitBind(CollectedExpressions, Initial);
            return HaxlApplicative.ToFetch(bindSplit, bindTo, new Scope(scope));
        }
    }

    /// <summary>
    /// All binds must terminate in a FetchNode.
    /// </summary>
    public abstract class FetchNode<A> : Fetch<A>
    {
        private static readonly IEnumerable<BindProjectPair> emptyList = new List<BindProjectPair>();
        public IEnumerable<BindProjectPair> CollectedExpressions => emptyList;
        public LambdaExpression Initial => Expression.Lambda(Expression.Constant(this));
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

        public object WarnIfNull(object result, Action<HaxlLogEntry> logger)
        {
            if (result == null) logger(Warn($"The request type '{request.GetType().Name}' returned a null."));
            return result;
        }

        public override Haxl ToHaxlFetch(string bindTo, Scope scope)
        {
            Func<Task<object>, Haxl> DoneFromTask =
                t => Haxl.FromFunc((c, l) => Done.New(_ => scope.Add(bindTo, WarnIfNull(t.Result, l))));

            return Haxl.FromFunc((cache, logger) =>
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
                            if (task.IsCompleted)
                                return Done.New(_ =>
                                {
                                    var result = task.Result;
                                    return scope.Add(bindTo, WarnIfNull(result, logger));
                                });
                            return Blocked.New(
                                new List<BlockedRequest>(),
                                DoneFromTask(task)
                            );
                        }
                    );
            });
        }

        public Type RequestType => request.GetType();
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
            var binds = List.Select(Bind).ToList();
            var fetches = binds.Select((f, i) => f.ToHaxlFetch($"{bindTo}[{i}]", childScope)).ToList();
            var concurrent = fetches.Aggregate((f1, f2) => f1.Applicative(f2));
            return concurrent.Bind(scope => Haxl.FromFunc((cache, logger) => Done.New(_ =>
            {
                var values = scope.ShallowValues.Select(v => (B)v).ToList();
                return scope.WriteParent(bindTo, values);
            }
            )));
        }
    }

    /// <summary>
    /// Wraps the value in a Fetch monad.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class FetchResult<A> : FetchNode<A>
    {
        public readonly A Value;

        public FetchResult(A value)
        {
            Value = value;
        }

        public override Haxl ToHaxlFetch(string bindTo, Scope scope)
        {
            return Haxl.FromFunc((cache, logger) => Done.New(_ => scope.Add(bindTo, Value)));
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
                var newScope = scope;
                if (scope.InScope(bindTo))
                {
                    var value = scope.GetValue(bindTo);
                    newScope = new SelectScope(value, scope);
                }

                var blockNumber = newScope.GetLatestBlockNumber();
                var rebinder = new RebindToScope() { BlockCount = blockNumber };
                var rewritten = rebinder.Rebind(Map);
                return scope.Add(bindTo, rewritten.Compile().DynamicInvoke(newScope));
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
            var isLet = LetExpression.IsLetExpression(f);
            if (!isLet) return new Select<A, B>(self, f);

            Expression<Func<A, Fetch<A>>> letBind = _ => self;
            var letProject = LetExpression.RewriteLetExpression(f);
            var letPair = new BindProjectPair(letBind, letProject);
            return new Bind<A, B, B>(self.CollectedExpressions.Append(letPair), self);
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

        public static async Task<A> FetchWith<A>(this Fetch<A> fetch, Fetcher fetcher, HaxlCache cache, Action<HaxlLogEntry> logger)
        {
            var run = fetch.ToHaxlFetch(HAXL_RESULT_NAME, Scope.New());
            var scope = await RunFetch.Run(run, Scope.New(), fetcher.FetchBatch, cache, logger);
            var result = (A)scope.GetValue(HAXL_RESULT_NAME);
            logger(Info("==== Result ===="));
            logger(Info($"{result}"));
            return result;
        }
    }
}
