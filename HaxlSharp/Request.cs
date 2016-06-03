using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HaxlSharp.Haxl;

namespace HaxlSharp
{
    public interface FetchResult
    {

        bool isBlocked { get; }

    }


    /// <summary>
    /// Simulate existential types by packaging the request with its type information. 
    /// </summary>
    public class BlockedRequest : FetchResult
    {
        public readonly object TypedRequest;
        public readonly Type RequestType;
        public readonly string BindName;

        public bool isBlocked
        {
            get
            {
                return true;
            }
        }

        public BlockedRequest(object typedRequest, Type requestType, string bindName)
        {
            TypedRequest = typedRequest;
            RequestType = requestType;
            BindName = bindName;
        }
    }

    public class ProjectResult : FetchResult
    {
        public readonly Action<Scope> PutResult;
        public ProjectResult(Action<Scope> putResult)
        {
            PutResult = putResult;
        }

        public bool isBlocked
        {
            get
            {
                return false;
            }
        }
    }

    public class Result
    {
        public readonly object Value;
        public readonly string BindName;
        public readonly Type ResultType;
        public Result(object value, Type resultType, string bindName)
        {
            Value = value;
            ResultType = resultType;
            BindName = bindName;
        }
    }

    public interface Returns<A> { }

    public interface CachableRequest<A> : Returns<A>
    {
        string CacheKey { get; }
    }

    public class FetcherBuilder
    {
        private readonly Dictionary<Type, Func<BlockedRequest, Result>> _fetchFunctions;
        private readonly Dictionary<Type, Func<BlockedRequest, Task<Result>>> _asyncFetchFunctions;
        public FetcherBuilder()
        {
            _fetchFunctions = new Dictionary<Type, Func<BlockedRequest, Result>>();
            _asyncFetchFunctions = new Dictionary<Type, Func<BlockedRequest, Task<Result>>>();
        }

        public static FetcherBuilder New()
        {
            return new FetcherBuilder();
        }

        /// <summary>
        /// Creates untyped fetch function from typed fetch function.
        /// </summary>
        private Func<BlockedRequest, Result> CreateFetchFunc<Req, Res>(Func<Req, Res> fetchFunc) where Req : Returns<Res>
        {
            var resultType = typeof(Res);
            var requestType = typeof(Req);
            Func<BlockedRequest, Result> untypedFetchFunc = request =>
            {
                if (request.RequestType != requestType) throw new ArgumentException("Invalid request type");
                var typedRequest = (Req)request.TypedRequest;
                var result = fetchFunc(typedRequest);
                return new Result(result, typeof(Res), request.BindName);
            };
            return untypedFetchFunc;
        }

        /// <summary>
        /// Creates untyped async fetch function from typed async fetch function.
        /// </summary>
        private Func<BlockedRequest, Task<Result>> CreateAsyncFetchFunc<Req, Res>(Func<Req, Task<Res>> fetchFunc) where Req : Returns<Res>
        {
            var resultType = typeof(Res);
            var requestType = typeof(Req);
            Func<BlockedRequest, Task<Result>> untypedFetchFunc = async blockedRequest =>
            {
                if (blockedRequest.RequestType != requestType) throw new ArgumentException($"Request type mismatch: expected '{requestType}', got '{blockedRequest.RequestType}'");
                var typedRequest = (Req)blockedRequest.TypedRequest;
                var result = await fetchFunc(typedRequest);
                return new Result(result, typeof(Res), blockedRequest.BindName);
            };
            return untypedFetchFunc;
        }

        /// <summary>
        /// Throws exception if there's already a handler registered for given type. 
        /// </summary>
        private void ThrowIfRegistered(Type requestType)
        {
            if (!_fetchFunctions.ContainsKey(requestType) && !_asyncFetchFunctions.ContainsKey(requestType)) return;
            throw new ArgumentException($"Attempted to register multiple handlers for request type '{requestType}'");
        }

        /// <summary>
        /// Adds a request handler to the fetcher.
        /// </summary>
        public FetcherBuilder FetchRequest<Req, Res>(Func<Req, Res> fetchFunction) where Req : Returns<Res>
        {
            var requestType = typeof(Req);
            ThrowIfRegistered(requestType);
            _fetchFunctions.Add(requestType, CreateFetchFunc(fetchFunction));
            return this;
        }

        /// <summary>
        /// Adds an async request handler to the fetcher.
        /// </summary>
        public FetcherBuilder HandleRequest<Req, Res>(Func<Req, Task<Res>> fetchFunction) where Req : Returns<Res>
        {
            var requestType = typeof(Req);
            ThrowIfRegistered(requestType);
            _asyncFetchFunctions.Add(requestType, CreateAsyncFetchFunc(fetchFunction));
            return this;
        }

        public Fetcher Create()
        {
            return new DefaultFetcher(_fetchFunctions, _asyncFetchFunctions);
        }
    }
}

