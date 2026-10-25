using System.IO;

namespace Client.RemoteExec
{
    // 待受の種類に依らない要求の形。入口の判定はこの値だけを見る
    // A listener-independent request shape; the entry decides from these values alone
    internal sealed class RemoteExecHttpRequest
    {
        public string Path { get; }
        public string Method { get; }
        public bool HasOriginHeader { get; }

        // トークンヘッダが無い要求は null
        // null when the request carries no token header
        public string TokenHeader { get; }
        public Stream Body { get; }

        internal RemoteExecHttpRequest(string path, string method, bool hasOriginHeader, string tokenHeader, Stream body)
        {
            Path = path;
            Method = method;
            HasOriginHeader = hasOriginHeader;
            TokenHeader = tokenHeader;
            Body = body;
        }
    }
}
