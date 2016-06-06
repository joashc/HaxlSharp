using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public abstract class CachableRequest<A> : Returns<A>
    {
        string CacheKey { get; }
    }

    /// <summary>
    /// Constructs a fetcher from request handlers.
    /// </summary>
    public class FetcherBuilder
    {
        private readonly Dictionary<Type, Func<BlockedRequest, Response>> _fetchFunctions;
        private readonly Dictionary<Type, Func<BlockedRequest, Task<Response>>> _asyncFetchFunctions;
        public FetcherBuilder()
        {
            _fetchFunctions = new Dictionary<Type, Func<BlockedRequest, Response>>();
            _asyncFetchFunctions = new Dictionary<Type, Func<BlockedRequest, Task<Response>>>();
        }

        public static FetcherBuilder New()
        {
            return new FetcherBuilder();
        }

        /// <summary>
        /// Creates untyped fetch function from typed fetch function.
        /// </summary>
        private Func<BlockedRequest, Response> CreateFetchFunc<Req, Res>(Func<Req, Res> fetchFunc) where Req : Returns<Res>
        {
            var resultType = typeof(Res);
            var requestType = typeof(Req);
            Func<BlockedRequest, Response> untypedFetchFunc = request =>
            {
                if (request.RequestType != requestType) throw new ArgumentException("Invalid request type");
                var typedRequest = (Req)request.TypedRequest;
                var result = fetchFunc(typedRequest);
                return new Response(result, typeof(Res), request.BindName);
            };
            return untypedFetchFunc;
        }

        /// <summary>
        /// Creates untyped async fetch function from typed async fetch function.
        /// </summary>
        private Func<BlockedRequest, Task<Response>> CreateAsyncFetchFunc<Req, Res>(Func<Req, Task<Res>> fetchFunc) where Req : Returns<Res>
        {
            var resultType = typeof(Res);
            var requestType = typeof(Req);
            Func<BlockedRequest, Task<Response>> untypedFetchFunc = async blockedRequest =>
            {
                if (blockedRequest.RequestType != requestType) throw new ArgumentException($"Request type mismatch: expected '{requestType}', got '{blockedRequest.RequestType}'");
                var typedRequest = (Req)blockedRequest.TypedRequest;
                var result = await fetchFunc(typedRequest);
                return new Response(result, typeof(Res), blockedRequest.BindName);
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

