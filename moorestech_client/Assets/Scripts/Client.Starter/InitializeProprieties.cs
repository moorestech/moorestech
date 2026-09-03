using System;
using System.Net;
using Client.Common;
using Mooresmaster.Localization.Generated;

namespace Client.Starter
{
    public class InitializeProprieties
    {
        // 未指定時の既定プレイヤー。既定の解決はこのクラスだけが持つ
        // The default player when unspecified; only this class resolves it
        private const int DefaultPlayerId = 1;

        // 許容ポート範囲。文言へは{p0}で供給する
        // The allowed port range; the wording receives it through {p0}
        private const int MinExclusivePort = 1024;
        private const int MaxPort = 65535;

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

        // 入力欄の文字列を検証し、通ったときだけ接続プロパティを作る。拒否は理由だけで表す
        // Validates the raw input fields and builds the properties only when they pass; a refusal is expressed by the reason alone
        public static bool TryCreateRemoteConnection(string serverIpText, string serverPortText, int playerId, out InitializeProprieties properties, out RemoteConnectionDenyReason denyReason)
        {
            properties = null;
            denyReason = default;

            if (!IPAddress.TryParse(serverIpText, out var address))
            {
                denyReason = new RemoteConnectionDenyReason(LocalizationKeys.Ui.MainMenu.ConnectInvalidIp);
                return false;
            }

            if (!int.TryParse(serverPortText, out var port))
            {
                denyReason = new RemoteConnectionDenyReason(LocalizationKeys.Ui.MainMenu.ConnectInvalidPort);
                return false;
            }

            if (MaxPort < port)
            {
                denyReason = new RemoteConnectionDenyReason(LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge, MaxPort.ToString());
                return false;
            }

            if (port <= MinExclusivePort)
            {
                denyReason = new RemoteConnectionDenyReason(LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall, MinExclusivePort.ToString());
                return false;
            }

            // 表記ゆれを正規化した文字列で持たせる
            // Carry the address as its normalized textual form
            properties = CreateRemoteConnection(address.ToString(), port, playerId);
            return true;
        }
    }
}
