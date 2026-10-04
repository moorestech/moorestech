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

using static Client.Tests.PlaceSystem.GearChainPoleConnect.GearChainPoleDecideInputs;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    // 接続可否の表示フィードバックを検証する
    // Verify presentation feedback for connection judgements
    public class GearChainPoleChainConnectModeFeedbackTest
    {
        private static readonly Guid ShortageMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [SetUp]
        public void SetUp()
        {
            // 不足行はアイテム名を表示言語で解決するため、マスタと辞書を実物で通す
            // A shortage line resolves the item name in the display language, so the real master and dictionary are loaded
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

        [Test]
        // ポール間接続の判定失敗は理由キーの行を返す
        // A failed pole-to-pole judgement returns the reason-key line
        public void PoleToPoleFailureReasonReportsFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.PoleToPolePreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.AlreadyConnected), Array.Empty<ConstructionMaterialShortage>());

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
            input.PoleToPolePreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.NoItem), shortages);

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
            input.PoleToPolePreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.NoItem), Array.Empty<ConstructionMaterialShortage>());

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

    }
}
