using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Core.Master;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// チェーンアイテム手持ちモードDecideの純関数テスト
    /// Pure function tests for the chain-item mode Decide
    /// </summary>
    public class GearChainPoleChainConnectModeTest
    {
        private static readonly ItemId HoldingChainItemId = new(7);

        [Test]
        // ポール非命中で起点があればカーソルへ赤線を表示する
        // With a source and no pole hit, show a red line to the cursor
        public void NoHitShowsRedLineToCursorTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.HitPole = default;
            input.HasCursorPoint = true;
            input.CursorPoint = new Vector3(9, 0, 9);

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(sourcePole, result.NextSourcePole);
            Assert.IsTrue(result.Preview.LineVisible);
            Assert.IsFalse(result.Preview.LinePlaceable);
            Assert.AreEqual(input.CursorPoint, result.Preview.LineEnd);
        }

        [Test]
        // 起点もポール命中もなければ何も表示しない
        // Show nothing without a source or a pole hit
        public void NoHitNoSourceShowsNothingTest()
        {
            var input = new GearChainPoleChainConnectInput { HoldingChainItemId = HoldingChainItemId };

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.IsNull(result.NextSourcePole);
            Assert.IsFalse(result.Preview.LineVisible);
            Assert.IsFalse(result.Preview.GhostVisible);
        }

        [Test]
        // 起点未選択のクリックで起点が選択される
        // A click with no source selects the source
        public void ClickSelectsSourceTest()
        {
            var hitPole = new FakeGearChainPole(new Vector3Int(2, 0, 2));
            var input = new GearChainPoleChainConnectInput
            {
                HitPole = hitPole,
                HitPolePos = hitPole.GetBlockPosition(),
                Clicked = true,
                HoldingChainItemId = HoldingChainItemId,
            };

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(hitPole, result.NextSourcePole);
            Assert.IsTrue(result.InvalidatePendingRequest);
        }

        [Test]
        // 起点自身への接続は非表示のまま何も起きない
        // Targeting the source itself shows nothing and does nothing
        public void SamePoleShowsNothingTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.HitPole = sourcePole;
            input.HitPolePos = sourcePole.GetBlockPosition();
            input.Clicked = true;

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(sourcePole, result.NextSourcePole);
            Assert.IsFalse(result.Preview.LineVisible);
            Assert.IsFalse(result.ChainConnectSend.HasValue);
        }

        [Test]
        // 接続可能状態のクリックで接続が送信され起点がクリアされる
        // Clicking in a connectable state sends the connection and clears the source
        public void ConnectableClickSendsConnectTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.Clicked = true;

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.IsTrue(result.ChainConnectSend.HasValue);
            var send = result.ChainConnectSend.Value;
            Assert.AreEqual(input.SourcePolePos, send.FromPos);
            Assert.AreEqual(input.HitPolePos, send.ToPos);
            Assert.AreEqual(HoldingChainItemId, send.ChainItemId);
            Assert.IsNull(result.NextSourcePole);
            Assert.IsTrue(result.InvalidatePendingRequest);
        }

        [Test]
        // 起点情報が解決できない場合はクリックで起点を選び直す
        // When the source cannot be resolved, a click re-selects the source
        public void InvalidPreviewClickReselectsSourceTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.PoleToPolePreview = GearChainPoleExtendPreviewData.Invalid;
            input.Clicked = true;

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(input.HitPole, result.NextSourcePole);
            Assert.IsTrue(result.InvalidatePendingRequest);
            Assert.IsFalse(result.ChainConnectSend.HasValue);
        }

        [Test]
        // 接続可能状態の非クリックは接続線のみ表示する
        // A connectable state without click only shows the connection line
        public void ConnectableWithoutClickShowsLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(sourcePole, result.NextSourcePole);
            Assert.IsTrue(result.Preview.LineVisible);
            Assert.IsTrue(result.Preview.LinePlaceable);
            Assert.IsFalse(result.ChainConnectSend.HasValue);
        }

        private static GearChainPoleChainConnectInput CreateConnectablePairInput(FakeGearChainPole sourcePole)
        {
            // 起点と命中ポールが接続可能な標準入力を作る
            // Build a standard input where the source and hit pole are connectable
            var hitPole = new FakeGearChainPole(new Vector3Int(5, 0, 5));
            var sourcePos = sourcePole.GetBlockPosition();
            var hitPos = hitPole.GetBlockPosition();
            return new GearChainPoleChainConnectInput
            {
                HitPole = hitPole,
                SourcePole = sourcePole,
                HoldingChainItemId = HoldingChainItemId,
                SourcePolePos = sourcePos,
                SourcePoleCenter = sourcePos + new Vector3(0.5f, 0.5f, 0.5f),
                HitPolePos = hitPos,
                PoleToPolePreview = new GearChainPoleExtendPreviewData(sourcePos + new Vector3(0.5f, 0.5f, 0.5f), hitPos + new Vector3(0.5f, 0.5f, 0.5f), true),
            };
        }
    }
}
