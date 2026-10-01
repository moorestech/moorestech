using Server.Protocol.PacketResponse.Handshake;

namespace Client.Network.API.Identity
{
    // ハンドシェイクの結果。成功なら応答、拒否ならサーバーの理由コードと開発者向け理由を運ぶ
    // The handshake outcome: the response on success, or the server's reason code plus a developer-facing reason on refusal
    // 表示文言は上位層が決める。通信層は表示語彙を持たない
    // The display text is decided upstairs; the network layer owns no display vocabulary
    public readonly struct InitialHandshakeAttempt
    {
        public readonly InitialHandshakeResponse Response;
        public readonly HandshakeRejection Rejection;
        public readonly string LogReason;

        private InitialHandshakeAttempt(InitialHandshakeResponse response, HandshakeRejection rejection, string logReason)
        {
            Response = response;
            Rejection = rejection;
            LogReason = logReason;
        }

        public static InitialHandshakeAttempt Succeeded(InitialHandshakeResponse response)
        {
            return new InitialHandshakeAttempt(response, HandshakeRejection.None, null);
        }

        // 拒否コードがNoneの拒否は、サーバーが成功と答えたのに応答が使えなかったプロトコル不整合を表す
        // A refusal carrying None means the server answered success but its payload was unusable: a protocol mismatch
        public static InitialHandshakeAttempt Refused(HandshakeRejection rejection, string logReason)
        {
            return new InitialHandshakeAttempt(null, rejection, logReason);
        }

        public bool IsRefused => LogReason != null;
    }
}
