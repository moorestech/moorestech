using UnityEngine;

namespace Game.Context
{
    public interface IChainSystem
    {
        bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, out string error);
        bool TryDisconnect(Vector3Int posA, Vector3Int posB, out string error);
    }
}
