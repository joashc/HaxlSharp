using static HaxlSharp.Internal.Base;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Utilities.Encoders;

namespace HaxlSharp
{
    public class HashedRequestKey : CacheKeyGenerator
    {
        public string ForRequest<A>(Returns<A> request)
        {
            return StaticForRequest(request);
        }

        private static byte[] Md5Hash(byte[] input)
        {
            // Update the input of MD5
            var md5 = new MD5Digest();
            md5.BlockUpdate(input, 0, input.Length);

            // Get the output and hash it
            var output = new byte[md5.GetDigestSize()];
            md5.DoFinal(output, 0);

            return Hex.Encode(output);
        }

        public static string StaticForRequest<A>(Returns<A> request)
        {
            var json = JsonConvert.SerializeObject(request, request.GetType(), new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All, TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full});
            return compose(ToLowerHexString, Md5Hash, StringBytes)(json);
        }
    }
}
