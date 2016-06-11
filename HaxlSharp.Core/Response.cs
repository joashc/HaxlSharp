using System;

namespace HaxlSharp
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
