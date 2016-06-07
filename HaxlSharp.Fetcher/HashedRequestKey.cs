using static HaxlSharp.Internal.Base;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace HaxlSharp
{
    public class HashedRequestKey : CacheKeyGenerator
    {
        public string ForRequest<A>(Returns<A> request)
        {
            var json = JsonConvert.SerializeObject(request, request.GetType(), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All, TypeNameAssemblyFormat= System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full});
            return json;
            using (var md5 = new MD5CryptoServiceProvider())
            {
                return compose(ToLowerHexString, md5.ComputeHash, StringBytes)(json);
            }
        }
    }
}
