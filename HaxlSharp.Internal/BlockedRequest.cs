using System;
using System.Threading.Tasks;

namespace HaxlSharp
{
    /// <summary>
    /// A request that's blocking the completion of a fetch.
    /// </summary>
    /// <remarks>
    /// We simulate existential types by packaging the request with its type information.
    /// </remarks>
    public class BlockedRequest
    {
        public readonly object TypedRequest;
        public readonly Type RequestType;
        public readonly string BindName;
        public readonly TaskCompletionSource<object> Resolver;

        public BlockedRequest(object typedRequest, Type requestType, string bindName)
        {
            TypedRequest = typedRequest;
            RequestType = requestType;
            BindName = bindName;
            Resolver = new TaskCompletionSource<object>();
        }
    }
}
