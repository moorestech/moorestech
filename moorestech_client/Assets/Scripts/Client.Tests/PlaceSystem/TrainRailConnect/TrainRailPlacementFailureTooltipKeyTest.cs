using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール接続失敗理由→ツールチップキー写像とReportの行順序のテスト
    /// Tests for the rail connection failure reason to tooltip key mapping and Report's line order
    /// </summary>
    public class TrainRailPlacementFailureTooltipKeyTest
    {
        [Test]
        public void 失敗理由ごとにツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailNotEnoughRailItem.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailFailed.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.InvalidNode).Key);
        }

        [Test]
        // 判定失敗理由のみ成立時は理由行だけを積む
        // Only the judgement failure line is pushed when just the judgement fails
        public void 判定失敗のみのとき理由行を1つだけ積む()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded, true);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded.Key, feedback.Lines[0].TextKey);
        }

        [Test]
        // 判定失敗とカーブ半径不足が同時成立するとき、判定理由→カーブ理由の順で行を積む
        // When the judgement failure and the too-tight curve both hold, the judgement reason line comes before the curve line
        public void 判定失敗とカーブ半径不足が同時成立するとき判定理由が先でカーブ理由が後になる()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem, false);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailNotEnoughRailItem.Key, feedback.Lines[0].TextKey);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight.Key, feedback.Lines[1].TextKey);
        }

        [Test]
        // 判定は成功しカーブ半径のみ不足のとき、カーブ理由行だけを積む
        // When only the curve is too tight while the judgement succeeds, only the curve line is pushed
        public void カーブ半径不足のみのときカーブ理由行だけを積む()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, false);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight.Key, feedback.Lines[0].TextKey);
        }

        [Test]
        // どちらも成立しないときは行を積まない
        // No lines are pushed when neither condition holds
        public void どちらも不成立のとき行を積まない()
        {
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, true);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.AreEqual(0, feedback.Lines.Count);
        }

        // テスト用にジャッジメントとカーブ可否からPreviewDataを組み立てる
        // Build a PreviewData from a judgement and curve viability for testing
        private static TrainRailConnectPreviewData CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, bool isCurvePlaceable)
        {
            var judgement = new RailPlacementJudgement(failureReason, Guid.NewGuid(), Array.Empty<ConnectToolMaterialCost>());
            return new TrainRailConnectPreviewData(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, judgement, isCurvePlaceable);
        }
    }
}
