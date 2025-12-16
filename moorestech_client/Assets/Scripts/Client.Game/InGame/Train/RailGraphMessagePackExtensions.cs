using Game.Train.RailGraph;
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
        // ConnectionDestinationMPをサーバー構造体へ変換
        // Convert ConnectionDestinationMessagePack into server-side ConnectionDestination
        public static ConnectionDestination ToConnectionDestination(this ConnectionDestinationMessagePack message)
        {
            if (message == null || message.ComponentId == null)
            {
                return ConnectionDestination.Default;
            }

            var position = message.ComponentId.Position?.Vector3Int ?? Vector3Int.zero;
            var componentId = new RailComponentID(position, message.ComponentId.ID);
            return new ConnectionDestination(componentId, message.IsFrontSide);
        }

        // Vector3MessagePackを安全にVector3化
        // Safely convert Vector3MessagePack into Vector3
        public static Vector3 ToUnityVector(this Vector3MessagePack message)
        {
            return message?.Vector3 ?? Vector3.zero;
        }
    }
}
