using Server.Util.MessagePack;
using UnityEngine;
using Game.Train.SaveLoad;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     Railグラフ関連MessagePackのクライアント変換ヘルパー
    ///     Helper extensions to convert rail graph MessagePack models into client structures
    /// </summary>
    internal static class RailGraphMessagePackExtensions
    {
        // ConnectionDestinationMPをサーバー構造体へ変換
        // Convert ConnectionDestinationMessagePack into server-side ConnectionDestination
        public static ConnectionDestination ToConnectionDestination(this ConnectionDestinationMessagePack message)
        {
            if (message == null || message.BlockPosition == null)
            {
                return ConnectionDestination.Default;
            }

            var position = message.BlockPosition.Vector3Int;
            return new ConnectionDestination(position, message.ComponentIndex, message.IsFrontSide);
        }

        // Vector3MessagePackを安全にVector3化
        // Safely convert Vector3MessagePack into Vector3
        public static Vector3 ToUnityVector(this Vector3MessagePack message)
        {
            return message?.Vector3 ?? Vector3.zero;
        }
    }
}
