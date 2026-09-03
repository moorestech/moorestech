using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePlacementFailureTooltipKeyTest
    {
        // lengthPerUnit=1、単一素材×1
        // TestElectricWire: lengthPerUnit=1 with a single material x1
        private static readonly Guid WireConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid WireMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 失敗理由ごとに個別のツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRange.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.OutOfRange).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireAlreadyConnected.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.AlreadyConnected).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireConnectionLimit.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.ConnectionLimit).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InvalidTarget).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.PositionOccupied).Key);
        }

        [Test]
        public void クライアント判定が返さない理由は既定キーにフォールバックする()
        {
            // Extend由来: OutOfRange/InvalidTarget
            // Evaluator由来: AlreadyConnected/ConnectionLimit/NoWireItem
            // From Extend: OutOfRange/InvalidTarget
            // From Evaluator: AlreadyConnected/ConnectionLimit/NoWireItem
            // PositionOccupied はサーバー応答用の写像で、電線ツールの重複理由は電柱ゴーストのIsPositionFree行が担う
            // PositionOccupied is mapped for server responses; the wire tool's overlap reason comes from the pole ghost's IsPositionFree line
            // 素材不足の期待キーは既定の不可文言になる
            // The material shortage's expected key is the default cannot-place text
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.NoWireItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InvalidMode).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.None).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.NoPoleItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InsufficientItems).Key);
        }

        [Test]
        // 素材不足の判定は不足行(実アイテム名+所持/必要)に続けてコスト行を積む
        // A material shortage judgement pushes the real-item-name shortage line followed by the cost line
        public void 素材不足の判定は不足行とコスト行を積む()
        {
            CreateServer();
            var judgement = ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.NoWireItem);
            var inventoryItems = new List<IItemStack> { ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(WireMaterialGuid), 0) };

            // 判定側が算出する不足素材と消費電線数を、判定と同じ入力から組み立てて渡す
            // Build the shortages and wire cost from the same inputs the judgement side uses, then hand them over
            var shortages = ConnectToolMaterialShortageCalculator.Calculate(WireConnectToolGuid, 1f, inventoryItems, null);
            var preview = new ElectricWireExtendPreviewData(judgement, shortages, 1);
            var feedback = new PlacementFeedback();

            ElectricWirePlacementFailureTooltipKey.Report(preview, feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(WireMaterialGuid)), feedback.Lines[0].TextParams[0]);
            Assert.AreEqual("0", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("1", feedback.Lines[0].TextParams[2]);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireCost.Key, feedback.Lines[1].Key.Key);
            CollectionAssert.AreEqual(new[] { "1" }, feedback.Lines[1].TextParams);

            #region Internal

            void CreateServer()
            {
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                Localize.Initialize();
            }

            #endregion
        }

        [Test]
        // 建設コストの予約分が電線の必要数へ上乗せされ、不足行の必要数として出る
        // The construction cost reservation is added on top of the wire requirement and shows up as the required count
        public void 電柱の建設コスト予約は電線不足行の必要数へ上乗せされる()
        {
            CreateServer();
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireMaterialGuid);
            var judgement = ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.NoWireItem);

            // 電線1本分(距離1)に対し、同じアイテムを2個使う電柱の建設コストを予約として渡す
            // Against one wire unit (distance 1), reserve a pole construction cost using two of the very same item
            var inventoryItems = new List<IItemStack> { ServerContext.ItemStackFactory.Create(wireItemId, 2) };
            var reserved = ConnectToolMaterialConsumer.ToMaterials(new[] { (itemId: wireItemId, count: 2) });
            var shortages = ConnectToolMaterialShortageCalculator.Calculate(WireConnectToolGuid, 1f, inventoryItems, reserved);
            var feedback = new PlacementFeedback();

            ElectricWirePlacementFailureTooltipKey.Report(new ElectricWireExtendPreviewData(judgement, shortages, 1), feedback);

            // 所持2でも電線1+予約2=3が必要なため不足行が出る
            // Holding 2 still falls short of 1 wire + 2 reserved = 3, so the shortage line appears
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("2", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("3", feedback.Lines[0].TextParams[2]);

            #region Internal

            void CreateServer()
            {
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                Localize.Initialize();
            }

            #endregion
        }
    }
}
