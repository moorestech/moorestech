using System;
using Client.Common;

namespace Client.Starter
{
    public class InitializeProprieties
    {
        public readonly bool IsRemoteConnection;
        public readonly string ServerIp;
        public readonly int ServerPort;
        public readonly int PlayerId;

        public string[] CreateLocalServerArgs { get; set; } = Array.Empty<string>();

        private InitializeProprieties(bool isRemoteConnection, string serverIp, int serverPort, int playerId)
        {
            IsRemoteConnection = isRemoteConnection;
            ServerIp = serverIp;
            ServerPort = serverPort;
            PlayerId = playerId;
        }

        // ローカルプレイは接続試行なしで内蔵サーバーを必ず起動する（ADR 0013）
        // Local play always boots the embedded server without probing (ADR 0013)
        public static InitializeProprieties CreateLocalServer(int playerId)
        {
            return new InitializeProprieties(false, ServerConst.LocalServerIp, 0, playerId);
        }

        // 明示IP:ポート指定のみ。フォールバック無し
        // Explicit IP:port only; no fallback
        public static InitializeProprieties CreateRemoteConnection(string serverIp, int serverPort, int playerId)
        {
            return new InitializeProprieties(true, serverIp, serverPort, playerId);
        }
    }
}
