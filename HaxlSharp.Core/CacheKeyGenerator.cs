using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    /// <summary>
    /// Generates a unique key per request.
    /// </summary>
    public interface CacheKeyGenerator
    {
        string ForRequest<A>(Returns<A> request);
    }
}
