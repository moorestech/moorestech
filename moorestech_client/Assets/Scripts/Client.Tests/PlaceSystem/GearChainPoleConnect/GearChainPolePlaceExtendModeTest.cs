using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Core.Master;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// ポールアイテム手持ちモードDecideの純関数テスト
    /// Pure function tests for the pole-item mode Decide
    /// </summary>
    public class GearChainPolePlaceExtendModeTest
    {
        private static readonly ItemId ChainItemId = new(7);

        [Test]
        // 既存ポールクリックで起点選択され進行中応答が無効化される
        // Clicking an existing pole selects the source and invalidates pending requests
        public void HitPoleClickSelectsSourceTest()
        {
            var hitPole = new FakeGearChainPole(new Vector3Int(1, 0, 1));
            var input = CreateGhostReadyInput(sourcePole: null);
            input.HitPole = hitPole;
            input.Clicked = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(hitPole, result.NextSourcePole);
            Assert.IsTrue(result.InvalidatePendingRequest);
            Assert.IsFalse(result.Preview.GhostVisible);
            Assert.IsFalse(result.ExtendSend.HasValue);
        }

        [Test]
        // ポール命中中の非クリックは起点維持で非表示
        // Hitting a pole without click keeps the source and hides previews
        public void HitPoleWithoutClickKeepsSourceTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateGhostReadyInput(sourcePole);
            input.HitPole = new FakeGearChainPole(new Vector3Int(1, 0, 1));

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(sourcePole, result.NextSourcePole);
            Assert.IsFalse(result.InvalidatePendingRequest);
            Assert.IsFalse(result.Preview.GhostVisible);
        }

        [Test]
        // 起点なしのクリックで孤立設置が送信される
        // Clicking with no source sends an isolated placement
        public void IsolatedPlaceSendTest()
        {
            var input = CreateGhostReadyInput(sourcePole: null);
            input.Clicked = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsTrue(result.ExtendSend.HasValue);
            var send = result.ExtendSend.Value;
            Assert.IsFalse(send.FromPos.HasValue);
            Assert.AreEqual(ItemMaster.EmptyItemId, send.ChainItemId);
            Assert.IsTrue(send.CanContinueExtension);
            Assert.IsNull(result.NextSourcePole);
            Assert.IsFalse(result.Preview.GhostVisible);
        }

        [Test]
        // 応答待ち中はクリックしても送信されない
        // No send happens on click while awaiting a response
        public void AwaitingResponseBlocksSendTest()
        {
            var input = CreateGhostReadyInput(sourcePole: null);
            input.Clicked = true;
            input.IsAwaitingResponse = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.ExtendSend.HasValue);
            Assert.IsTrue(result.Preview.GhostVisible);
            Assert.IsTrue(result.Preview.GhostPlaceable);
        }

        [Test]
        // 地面衝突時は赤ゴーストになり送信されない
        // Ground collision shows a red ghost and blocks sending
        public void GroundBlockedShowsUnplaceableGhostTest()
        {
            var input = CreateGhostReadyInput(sourcePole: null);
            input.Clicked = true;
            input.GhostGroundClear = false;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.ExtendSend.HasValue);
            Assert.IsTrue(result.Preview.GhostVisible);
            Assert.IsFalse(result.Preview.GhostPlaceable);
        }

        [Test]
        // 起点ありのクリックで延長設置が送信され、上限1なら連続延長は打ち切られる
        // Clicking with a source sends an extension; max connection 1 ends continuous extension
        public void ExtendPlaceSendTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateGhostReadyInput(sourcePole);
            input.Clicked = true;
            input.MaxConnectionCount = 1;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsTrue(result.ExtendSend.HasValue);
            var send = result.ExtendSend.Value;
            Assert.AreEqual(sourcePole.GetBlockPosition(), send.FromPos.Value);
            Assert.AreEqual(ChainItemId, send.ChainItemId);
            Assert.IsFalse(send.CanContinueExtension);
            Assert.IsNull(result.NextSourcePole);
        }

        [Test]
        // 延長評価が不可なら赤ゴースト＋赤線で送信されない
        // Unplaceable extension judgement shows red ghost and line without sending
        public void ExtendUnplaceableShowsRedPreviewTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateGhostReadyInput(sourcePole);
            input.Clicked = true;
            input.ExtendPreview = GearChainPoleExtendPreviewData.Invalid;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.ExtendSend.HasValue);
            Assert.AreEqual(sourcePole, result.NextSourcePole);
            Assert.IsTrue(result.Preview.GhostVisible);
            Assert.IsFalse(result.Preview.GhostPlaceable);
            Assert.IsTrue(result.Preview.LineVisible);
            Assert.IsFalse(result.Preview.LinePlaceable);
        }

        private static GearChainPolePlaceExtendInput CreateGhostReadyInput(FakeGearChainPole sourcePole)
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
                PoleInventorySlot = 5,
                OwnedChainItemId = ChainItemId,
                MaxConnectionCount = 4,
            };
            if (sourcePole != null)
            {
                input.SourcePolePos = sourcePole.GetBlockPosition();
                input.SourcePoleCenter = input.SourcePolePos + new Vector3(0.5f, 0.5f, 0.5f);
                input.ExtendPreview = new GearChainPoleExtendPreviewData(input.SourcePoleCenter, input.GhostCenter, true);
            }

            return input;
        }
    }
}
