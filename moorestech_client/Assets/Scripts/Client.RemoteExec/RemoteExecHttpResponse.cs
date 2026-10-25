using Client.RemoteExec.Run;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Client.RemoteExec
{
    // 入口が返す応答。拒否も成功も同じRemoteExecResultの形で返す
    // The entry's response; refusals and successes share the RemoteExecResult shape
    internal sealed class RemoteExecHttpResponse
    {
        private static readonly JsonSerializerSettings BodySettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        public int StatusCode { get; }
        public RemoteExecResult Result { get; }

        internal RemoteExecHttpResponse(int statusCode, RemoteExecResult result)
        {
            StatusCode = statusCode;
            Result = result;
        }

        internal string SerializeBody()
        {
            return JsonConvert.SerializeObject(Result, BodySettings);
        }
    }
}
