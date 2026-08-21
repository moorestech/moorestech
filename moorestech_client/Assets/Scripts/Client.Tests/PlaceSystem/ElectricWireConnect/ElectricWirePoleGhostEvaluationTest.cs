using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    ///     電柱ゴーストの不可理由が地形→重複→素材の順で個別行になり、不可セルでは素材行を出さないことを検証
    ///     Verify the pole ghost pushes terrain/overlap/material as separate lines in order, with no material line on an already blocked cell
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
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].TextKey);
        }

        [Test]
        public void 重複だけのときは重複行だけを積み素材行を出さない()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(true, false, BuildShortages()).PushBlockReasons(feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].TextKey);
        }

        [Test]
        public void 地形干渉と重複が両方成立すると地形行の次に重複行を積む()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(false, false, BuildShortages()).PushBlockReasons(feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].TextKey);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[1].TextKey);
        }

        [Test]
        public void 設置可能なセルでは不足素材ごとに所持と必要の行を積む()
        {
            CreateServer();
            var feedback = new PlacementFeedback();

            BuildEvaluation(true, true, BuildShortages()).PushBlockReasons(feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].TextKey);
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

        private static ElectricWirePoleGhostEvaluation BuildEvaluation(bool isGroundClear, bool isPositionFree, IReadOnlyList<ConstructionMaterialShortage> shortages)
        {
            var poleBlockId = ForUnitTestModBlockId.ElectricPoleId;
            var placeInfos = new List<PlaceInfo> { new() { Position = Vector3Int.zero, Placeable = isGroundClear && isPositionFree } };
            return new ElectricWirePoleGhostEvaluation(placeInfos, MasterHolder.BlockMaster.GetBlockMaster(poleBlockId), poleBlockId, isGroundClear, isPositionFree, shortages);
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
