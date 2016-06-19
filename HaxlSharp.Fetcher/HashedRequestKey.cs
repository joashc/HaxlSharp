using static HaxlSharp.Internal.Base;
using Newtonsoft.Json;
using xBrainLab.Security.Cryptography;

namespace HaxlSharp
{
    public class HashedRequestKey : CacheKeyGenerator
    {
        public string ForRequest<A>(Returns<A> request)
        {
            return StaticForRequest(request);
        }

        public static string StaticForRequest<A>(Returns<A> request)
        {
            var json = JsonConvert.SerializeObject(request, request.GetType(), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All, TypeNameAssemblyFormat= System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full});
            return compose(ToLowerHexString, MD5.GetHash, StringBytes)(json);
        }
    }
}
