using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 失敗理由→キー写像と行順序のテスト
    /// Tests the failure-reason-to-key mapping and line order
    /// </summary>
    public class TrainRailPlacementFailureTooltipKeyTest
    {
        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

        private static readonly Guid RailMaterial1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid RailMaterial2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 失敗理由ごとにツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded).Key);
            // 素材不足の期待キーは既定の不可文言になる
            // The material shortage's expected key is the default cannot-place text
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

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

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

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

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

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

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

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.AreEqual(0, feedback.Lines.Count);
        }

        [Test]
        // 素材不足は不足素材ごとの実アイテム名行になり、カーブ理由行はその後に続く
        // A material shortage becomes one real-item-name line per material, followed by the curve reason line
        public void 素材不足は不足素材ごとの行になりカーブ理由行が後に続く()
        {
            var feedback = new PlacementFeedback();
            var shortages = new[]
            {
                new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(RailMaterial1Guid), 1, 12),
                new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(RailMaterial2Guid), 2, 5),
            };
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem, false, shortages);

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.AreEqual(3, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(RailMaterial1Guid)), feedback.Lines[0].TextParams[0]);
            Assert.AreEqual("1", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("12", feedback.Lines[0].TextParams[2]);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[1].Key.Key);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(RailMaterial2Guid)), feedback.Lines[1].TextParams[0]);
            Assert.AreEqual("2", feedback.Lines[1].TextParams[1]);
            Assert.AreEqual("5", feedback.Lines[1].TextParams[2]);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight.Key, feedback.Lines[2].Key.Key);

        }

        [Test]
        // 橋脚自身の建設コスト不足は設置不可にし、レール素材と同じアイテムなら関門が1行へ畳む
        // The pier's own construction cost shortage blocks placement, and the gate folds it with the rail material when the item matches
        public void 橋脚コスト不足は設置不可になり同一アイテムはレール不足と1行に畳まれる()
        {
            var railItemId = MasterHolder.ItemMaster.GetItemId(RailMaterial1Guid);
            var railShortages = new[] { new ConstructionMaterialShortage(railItemId, 1, 12) };
            var pierShortages = new[] { new ConstructionMaterialShortage(railItemId, 1, 4) };
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem, true, railShortages, pierShortages);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.IsFalse(previewData.IsPlaceable);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("12", feedback.Lines[0].TextParams[2]);

        }

        [Test]
        // 判定も曲線も通っていても橋脚コストが足りなければ設置不可になる
        // Even with the judgement and the curve passing, an unaffordable pier makes it unplaceable
        public void 橋脚コスト不足だけでも設置不可になる()
        {
            var pierShortages = new[] { new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(RailMaterial1Guid), 0, 4) };
            var previewData = CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, true, Array.Empty<ConstructionMaterialShortage>(), pierShortages);
            var feedback = new PlacementFeedback();

            TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);

            Assert.IsFalse(previewData.IsPlaceable);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("4", feedback.Lines[0].TextParams[2]);

        }

        // テスト用にPreviewDataを組み立てる
        // Builds a PreviewData for testing
        private static TrainRailConnectPreviewData CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, bool isCurvePlaceable)
        {
            return CreatePreviewData(failureReason, isCurvePlaceable, Array.Empty<ConstructionMaterialShortage>());
        }

        private static TrainRailConnectPreviewData CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, bool isCurvePlaceable, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
        {
            return CreatePreviewData(failureReason, isCurvePlaceable, materialShortages, Array.Empty<ConstructionMaterialShortage>());
        }

        private static TrainRailConnectPreviewData CreatePreviewData(RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, bool isCurvePlaceable, IReadOnlyList<ConstructionMaterialShortage> materialShortages, IReadOnlyList<ConstructionMaterialShortage> pierMaterialShortages)
        {
            var judgement = new RailPlacementJudgement(failureReason, Guid.NewGuid(), Array.Empty<ConnectToolMaterialCost>());
            return new TrainRailConnectPreviewData(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, judgement, isCurvePlaceable, materialShortages, pierMaterialShortages);
        }
    }
}
