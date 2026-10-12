using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    // インスタンスIDから端点座標を読み取る
    // Read endpoint positions by block instance id
    public interface IConnectionLineEndpointQuery
    {
        bool TryGetPosition(BlockInstanceId instanceId, out Vector3Int position);
    }
}
