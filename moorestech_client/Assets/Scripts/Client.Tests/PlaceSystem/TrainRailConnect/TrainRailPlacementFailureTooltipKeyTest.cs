using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 失敗理由→キー写像と行順序のテスト
    /// Tests the failure-reason-to-key mapping and line order
    /// </summary>
    public class TrainRailPlacementFailureTooltipKeyTest
    {
        [Test]
        public void 失敗理由ごとにツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded).Key);
            // 素材不足は写像を持たず、名指しの行が作れないときの落とし先と同じ既定文言になる
            // The material shortage has no mapping of its own and lands on the same default used when no named line can be built
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailFailed.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailFailed.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.InvalidNode).Key);
        }

        [Test]
        // 判定失敗理由のみ成立時は理由行だけを積む
        // Only the judgement failure line is pushed when just the judgement fails
        public void 判定失敗のみのとき理由行を1つだけ積む()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded, true);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, Array.Empty<ConstructionMaterialShortage>(), feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        // 素材不足かつ不足素材が空のときは汎用の接続不可行になる
        // A material shortage with no short material becomes the generic cannot-connect line
        // 判定失敗とカーブ半径不足が同時成立するとき、判定理由→カーブ理由の順で行を積む
        // When the judgement failure and the too-tight curve both hold, the judgement reason line comes before the curve line
        public void 判定失敗とカーブ半径不足が同時成立するとき判定理由が先でカーブ理由が後になる()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem, false);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, Array.Empty<ConstructionMaterialShortage>(), feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailFailed.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight.Key, feedback.Lines[1].Key.Key);
        }

        [Test]
        // 判定は成功しカーブ半径のみ不足のとき、カーブ理由行だけを積む
        // When only the curve is too tight while the judgement succeeds, only the curve line is pushed
        public void カーブ半径不足のみのときカーブ理由行だけを積む()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, false);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, Array.Empty<ConstructionMaterialShortage>(), feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        // どちらも成立しないときは行を積まない
        // No lines are pushed when neither condition holds
        public void どちらも不成立のとき行を積まない()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, true);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, Array.Empty<ConstructionMaterialShortage>(), feedback);

            Assert.AreEqual(0, feedback.Lines.Count);
        }

        // テスト用にPreviewDataを組み立てる
        // Builds a PreviewData for testing
        private static TrainRailConnectPreviewData CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, bool isCurvePlaceable)
        {
            var judgement = new RailPlacementJudgement(failureReason, Guid.NewGuid(), Array.Empty<ConnectToolMaterialCost>());
            return new TrainRailConnectPreviewData(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, judgement, isCurvePlaceable);
        }
    }
}
