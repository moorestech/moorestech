using System;
using Client.Network.API;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Handshake;

namespace Client.Starter.Identity
{
    internal static class PlayerStartFailureMessage
    {
        public static LocalizationKey GetKey(Exception exception)
        {
            // 開始拒否の理由を既存のローディング表示へ対応づける
            // Map start refusal reasons to the existing loading display
            return exception switch
            {
                PlayerStartRefusedException refused => refused.LocalizationKey,
                PlayerHandshakeRejectedException { Rejection: HandshakeRejection.AlreadyConnected } => LocalizationKeys.Ui.Loading.PlayerAlreadyConnected,
                _ => LocalizationKeys.Ui.Loading.InitializationFailed,
            };
        }
    }
}
