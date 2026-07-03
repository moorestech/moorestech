using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// Decideテスト用のフェイクポール。座標だけを返す
    /// Fake pole for Decide tests that only returns its position
    /// </summary>
    public class FakeGearChainPole : IGearChainPoleConnectAreaCollider
    {
        private readonly Vector3Int _position;

        public FakeGearChainPole(Vector3Int position)
        {
            _position = position;
        }

        public Vector3Int GetBlockPosition()
        {
            return _position;
        }
    }
}
