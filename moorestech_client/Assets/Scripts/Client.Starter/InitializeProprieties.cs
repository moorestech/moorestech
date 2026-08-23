using System;
using Client.Common;

namespace Client.Starter
{
    public class InitializeProprieties
    {
        // 未指定時の既定プレイヤー。既定の解決はこのクラスだけが持つ
        // The default player when unspecified; only this class resolves it
        private const int DefaultPlayerId = 1;

        public readonly bool IsRemoteConnection;
        public readonly string ServerIp;

        // リモート接続専用の宛先ポート。ローカルは宛先を持たないためnull
        // Destination port for remote connections only; null for local, which has no destination
        public readonly int? RemoteServerPort;
        public readonly int PlayerId;

        public string[] CreateLocalServerArgs { get; set; } = Array.Empty<string>();

        private InitializeProprieties(bool isRemoteConnection, string serverIp, int? remoteServerPort, int playerId)
        {
            IsRemoteConnection = isRemoteConnection;
            ServerIp = serverIp;
            RemoteServerPort = remoteServerPort;
            PlayerId = playerId;
        }

        // ローカルプレイは接続試行なしで内蔵サーバーを必ず起動する（ADR 0013）
        // Local play always boots the embedded server without probing (ADR 0013)
        public static InitializeProprieties CreateLocalServer(int? playerId)
        {
            return new InitializeProprieties(false, ServerConst.LocalServerIp, null, playerId ?? DefaultPlayerId);
        }

        // 明示IP:ポート指定のみ。フォールバック無し
        // Explicit IP:port only; no fallback
        public static InitializeProprieties CreateRemoteConnection(string serverIp, int serverPort, int playerId)
        {
            return new InitializeProprieties(true, serverIp, serverPort, playerId);
        }
    }
}
