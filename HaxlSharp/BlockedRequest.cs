using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    /// <summary>
    /// Simulate existential types by packaging the request with its type information. 
    /// </summary>
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
