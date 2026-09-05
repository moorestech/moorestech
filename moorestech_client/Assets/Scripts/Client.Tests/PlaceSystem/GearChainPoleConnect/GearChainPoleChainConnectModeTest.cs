using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// チェーンアイテム手持ちモードDecideの純関数テスト
    /// Pure function tests for the chain-item mode Decide
    /// </summary>
    public class GearChainPoleChainConnectModeTest
    {
        private static readonly System.Guid TestConnectToolGuid = System.Guid.NewGuid();
        private static readonly System.Guid ShortageMaterialGuid = System.Guid.Parse("00000000-0000-0000-1234-000000000003");

        [SetUp]
        public void SetUp()
        {
            // 不足行はアイテム名を表示言語で解決するため、マスタと辞書を実物で通す
            // A shortage line resolves the item name in the display language, so the real master and dictionary are loaded
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

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
            var input = new GearChainPoleChainConnectInput { ConnectToolGuid = TestConnectToolGuid };

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
                ConnectToolGuid = TestConnectToolGuid,
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
            Assert.AreEqual(TestConnectToolGuid, send.ConnectToolGuid);
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

        [Test]
        // ポール間接続の判定失敗は理由キーの行を返す
        // A failed pole-to-pole judgement returns the reason-key line
        public void PoleToPoleFailureReasonReportsFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.PoleToPolePreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.AlreadyConnectedError), Array.Empty<ConstructionMaterialShortage>());

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected.Key, result.FeedbackLines[0].Key.Key);
        }

        [Test]
        // 素材不足の判定では不足リストが落とし先キー付きの枠へ素通しされる
        // On a material-shortage judgement the shortage list passes through into the fallback-keyed slot
        public void MaterialShortageIsRoutedToFallbackSlotTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            var shortages = new[] { new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(ShortageMaterialGuid), 1, 4) };
            input.PoleToPolePreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.NoItemError), shortages);

            var feedback = new PlacementFeedback();
            GearChainPoleChainConnectMode.Decide(input).PushFeedback(feedback);

            // 不足はデータのまま関門へ渡り、名指しの不足行1本になる（汎用の不可行には落ちない）
            // The shortage reaches the gate as data and becomes one named line instead of the generic wording
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("4", feedback.Lines[0].TextParams[2]);
        }

        [Test]
        // 不足が算出できなくても落とし先キーは付き、関門が汎用文言へ落とせる
        // Even with no computed shortage the fallback key is attached so the gate can emit the generic wording
        public void EmptyMaterialShortageStillCarriesFallbackKeyTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.PoleToPolePreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.NoItemError), Array.Empty<ConstructionMaterialShortage>());

            var feedback = new PlacementFeedback();
            GearChainPoleChainConnectMode.Decide(input).PushFeedback(feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        // 接続可能なポール間では行を出さない
        // A connectable pole pair reports no line
        public void ConnectablePairReportsNoFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(0, result.FeedbackLines.Count);
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
                ConnectToolGuid = TestConnectToolGuid,
                SourcePolePos = sourcePos,
                SourcePoleCenter = sourcePos + new Vector3(0.5f, 0.5f, 0.5f),
                HitPolePos = hitPos,
                PoleToPolePreview = new GearChainPoleExtendPreviewData(sourcePos + new Vector3(0.5f, 0.5f, 0.5f), hitPos + new Vector3(0.5f, 0.5f, 0.5f), GearChainPlacementJudgement.Success(default), Array.Empty<ConstructionMaterialShortage>()),
            };
        }
    }
}
