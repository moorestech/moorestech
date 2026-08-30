using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    ///     地形→重複→素材の順で行になる
    ///     Verify lines follow terrain→overlap→material order
    ///     不可セルは素材行なし
    ///     A blocked cell has no material line
    /// </summary>
    public class ElectricWirePoleGhostEvaluationTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 地形干渉だけのときは地形行だけを積み素材行を出さない()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(false, true, BuildShortages()).PushBlockReasons(feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 重複だけのときは重複行だけを積み素材行を出さない()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(true, false, BuildShortages()).PushBlockReasons(feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 地形干渉と重複が両方成立すると地形行の次に重複行を積む()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(false, false, BuildShortages()).PushBlockReasons(feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[1].Key.Key);
        }

        [Test]
        public void 設置可能なセルでは不足素材ごとに所持と必要の行を積む()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(true, true, BuildShortages()).PushBlockReasons(feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("1", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("2", feedback.Lines[0].TextParams[2]);
            Assert.AreEqual("0", feedback.Lines[1].TextParams[1]);
            Assert.AreEqual("3", feedback.Lines[1].TextParams[2]);
        }

        [Test]
        public void 不可理由も不足素材も無ければ行を積まない()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(true, true, new List<ConstructionMaterialShortage>()).PushBlockReasons(feedback);

            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        // 電柱の建設コストと電線コストが同じアイテムなら、2つの出所から積まれても1行に畳む
        // When the pole's construction cost and the wire cost are the same item, the two sources still produce one line
        public void 電柱の建設コスト行と電線の不足行が同一アイテムなら1行に畳まれる()
        {
            CreateServer();
            var itemId = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var feedback = new PlacementFeedback();

            // 電柱ゴーストが建設コスト不足(0/10)を積んだ後、電線判定が予約込みの不足(0/11)を積む
            // The pole ghost pushes its construction shortage (0/10), then the wire judgement pushes the reservation-inclusive one (0/11)
            BuildEvaluation(true, true, new List<ConstructionMaterialShortage> { new(itemId, 0, 10) }).PushBlockReasons(feedback);
            var judgement = ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.NoWireItem);
            var wireShortages = new List<ConstructionMaterialShortage> { new(itemId, 0, 11) };
            ElectricWirePlacementFailureTooltipKey.Report(new ElectricWireExtendPreviewData(judgement, wireShortages, 1), feedback);

            // 素材行は1本だけで、必要数は予約分を含む合計側になる
            // Only one material line remains and its required count is the reservation-inclusive total
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("0", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("11", feedback.Lines[0].TextParams[2]);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireCost.Key, feedback.Lines[1].Key.Key);
            Assert.AreEqual(2, feedback.Lines.Count);
        }

        private static ElectricWirePoleGhostEvaluation BuildEvaluation(bool isGroundClear, bool isPositionFree, IReadOnlyList<ConstructionMaterialShortage> shortages)
        {
            var poleBlockId = ForUnitTestModBlockId.ElectricPoleId;
            var placeInfos = new List<PlaceInfo> { new() { Position = Vector3Int.zero, Placeable = isGroundClear && isPositionFree } };
            return new ElectricWirePoleGhostEvaluation(placeInfos, MasterHolder.BlockMaster.GetBlockMaster(poleBlockId), poleBlockId, isGroundClear, isPositionFree, shortages, Array.Empty<(ItemId itemId, int count)>());
        }

        private static List<ConstructionMaterialShortage> BuildShortages()
        {
            return new List<ConstructionMaterialShortage>
            {
                new(MasterHolder.ItemMaster.GetItemId(Material1Guid), 1, 2),
                new(MasterHolder.ItemMaster.GetItemId(Material2Guid), 0, 3),
            };
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 不足素材行はアイテム名を表示言語で解決するため実辞書を通す
            // The shortage line resolves the item name in the display language, so go through the real dictionary
            Localize.Initialize();
        }
    }
}
