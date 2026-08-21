using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Core.Master;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.GearChain;
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

    /// <summary>
    /// ポールアイテム手持ちモードDecideの入力スナップショットを組み立てる、テスト共通のビルダー
    /// Shared test builder for the pole-item mode Decide input snapshot
    /// </summary>
    public static class GearChainPoleDecideInputs
    {
        public static readonly System.Guid TestConnectToolGuid = System.Guid.NewGuid();

        public static GearChainPolePlaceExtendInput CreateGhostReadyInput(FakeGearChainPole sourcePole)
        {
            // ゴースト有効・地面クリア・設置可評価済みの標準入力を作る
            // Build a standard input with a valid ghost, clear ground and placeable judgement
            var placePos = new Vector3Int(3, 0, 3);
            var input = new GearChainPolePlaceExtendInput
            {
                SourcePole = sourcePole,
                HasGhost = true,
                GhostPlaceInfo = new PlaceInfo { Position = placePos, Placeable = true },
                GhostGroundClear = true,
                GhostCenter = placePos + new Vector3(0.5f, 0.5f, 0.5f),
                PoleBlockId = new BlockId(5),
                ConnectToolGuid = TestConnectToolGuid,
                MaxConnectionCount = 4,
            };
            if (sourcePole != null)
            {
                input.SourcePolePos = sourcePole.GetBlockPosition();
                input.SourcePoleCenter = input.SourcePolePos + new Vector3(0.5f, 0.5f, 0.5f);
                input.ExtendPreview = new GearChainPoleExtendPreviewData(input.SourcePoleCenter, input.GhostCenter, GearChainPlacementJudgement.Success(default));
            }

            return input;
        }
    }
}
