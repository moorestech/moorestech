using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     Railグラフ関連MessagePackのクライアント変換ヘルパー
    ///     Helper extensions to convert rail graph MessagePack models into client structures
    /// </summary>
    internal static class RailGraphMessagePackExtensions
    {
        // ConnectionDestinationMPをクライアント構造体へ変換
        // Convert ConnectionDestinationMessagePack into client-side ConnectionDestination
        public static ConnectionDestination ToClientDestination(this ConnectionDestinationMessagePack message)
        {
            if (message == null || message.ComponentId == null)
            {
                return ConnectionDestination.Default;
            }

            var position = message.ComponentId.Position?.Vector3Int ?? Vector3Int.zero;
            return new ConnectionDestination(position, message.ComponentId.ID, message.IsFrontSide);
        }

        // Vector3MessagePackを安全にVector3化
        // Safely convert Vector3MessagePack into Vector3
        public static Vector3 ToUnityVector(this Vector3MessagePack message)
        {
            return message?.Vector3 ?? Vector3.zero;
        }
    }
}
