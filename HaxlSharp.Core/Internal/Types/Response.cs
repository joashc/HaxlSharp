using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// The result of a primitive request. 
    /// </summary>
    public class Response
    {
        public readonly object Value;
        public readonly Type ResultType;
        public Response(object value, Type resultType)
        {
            Value = value;
            ResultType = resultType;
        }
    }
}
