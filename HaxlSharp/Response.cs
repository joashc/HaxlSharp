using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp
{
    public class Response
    {
        public readonly object Value;
        public readonly string BindName;
        public readonly Type ResultType;
        public Response(object value, Type resultType, string bindName)
        {
            Value = value;
            ResultType = resultType;
            BindName = bindName;
        }
    }
}
