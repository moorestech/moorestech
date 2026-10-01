using Client.Network.API.Identity;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Handshake;
using UnityEngine;

namespace Client.Starter.Initialization.Refusal
{
    // サーバーの拒否コードを表示文言へ写す。表示語彙を通信層へ持ち込まないための変換点
    // Maps the server's rejection code to display text; the conversion point that keeps display vocabulary out of the network layer
    internal static class HandshakeRejectionDisplay
    {
        internal static PlayerStartRefusal ToRefusal(InitialHandshakeAttempt attempt)
        {
            return new PlayerStartRefusal(ToDisplayKey(attempt.Rejection), attempt.LogReason);
        }

        private static LocalizationKey ToDisplayKey(HandshakeRejection rejection)
        {
            switch (rejection)
            {
                case HandshakeRejection.AlreadyConnected:
                    return LocalizationKeys.Ui.Loading.PlayerAlreadyConnected;
                case HandshakeRejection.InvalidIdentity:
                case HandshakeRejection.ConnectionClosed:
                    return LocalizationKeys.Ui.Loading.InitializationFailed;

                // 再ハンドシェイクとNoneはどちらもクライアント・サーバー間の手順の食い違い
                // Re-handshaking and None are both mismatches in the client/server sequence
                case HandshakeRejection.AlreadyHandshaked:
                case HandshakeRejection.None:
                    return LocalizationKeys.Ui.Loading.HandshakeProtocolError;
                default:
                    Debug.LogError($"未知のハンドシェイク拒否コードを受信しました: {(int)rejection}");
                    return LocalizationKeys.Ui.Loading.HandshakeProtocolError;
            }
        }
    }
}
