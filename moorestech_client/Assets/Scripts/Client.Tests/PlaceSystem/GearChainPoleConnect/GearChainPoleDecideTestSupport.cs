using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
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
    /// 手持ちモードDecide入力を組み立てる共通ビルダー
    /// Shared builder for the held-mode Decide input
    /// </summary>
    public static class GearChainPoleDecideInputs
    {
        public static readonly System.Guid TestConnectToolGuid = System.Guid.NewGuid();

        public static GearChainPolePlaceExtendInput CreateGhostReadyInput(FakeGearChainPole sourcePole)
        {
            // 標準の評価済み入力を作る
            // Builds a standard, already-evaluated input
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
                input.ExtendPreview = new GearChainPoleExtendPreviewData(input.SourcePoleCenter, input.GhostCenter, GearChainPlacementJudgement.Success(default), Array.Empty<ConstructionMaterialShortage>());
            }

            return input;
        }
    }
}
