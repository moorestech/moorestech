using System;
using Client.Network.API;
using Client.Network.API.Identity;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Handshake;

namespace Client.Starter.Identity
{
    internal static class PlayerStartFailureMessage
    {
        public static LocalizationKey GetKey(Exception exception)
        {
            // 拒否理由をローディング表示へ対応
            // Map refusal reasons to the loading display
            return exception switch
            {
                PlayerStartRefusedException refused => refused.LocalizationKey,
                PlayerHandshakeRejectedException { Rejection: HandshakeRejection.AlreadyConnected } => LocalizationKeys.Ui.Loading.PlayerAlreadyConnected,
                _ => LocalizationKeys.Ui.Loading.InitializationFailed,
            };
        }
    }
}
