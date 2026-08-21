using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Localization;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     素材不足の必要数が地形干渉・重複を除いたセル数ぶんであり、賄えないセルが設置不可になることを検証
    ///     Verify the required count covers only the cells left after terrain/overlap filtering, and unaffordable cells become not placeable
    /// </summary>
    public class CommonBlockPlaceCostMarkerTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 必要数は設置不可セルを除いたセル数分になる()
        {
            CreateServer();

            // 5セルのうち2セルは地形干渉・重複で既に不可。残り3セル分のみ必要数に数える
            // 2 of the 5 cells are already blocked by terrain/overlap; only the remaining 3 count toward the required amount
            var placeInfos = BuildDragCells(5);
            placeInfos[1].Placeable = false;
            placeInfos[3].Placeable = false;
            var feedback = new PlacementFeedback();

            CommonBlockPlaceCostMarker.MarkInsufficientCellsAsNotPlaceable(placeInfos, ForUnitTestModBlockId.BlockId, BuildInventory(3, 10), feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].TextKey);
            Assert.AreEqual("3", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("6", feedback.Lines[0].TextParams[2]);
        }

        [Test]
        public void 賄えるセル数を超えた設置可セルは不可になる()
        {
            CreateServer();

            var placeInfos = BuildDragCells(5);
            placeInfos[1].Placeable = false;
            placeInfos[3].Placeable = false;

            // 所持3枚ではコスト2のセルを1つしか賄えないため、残る設置可セルは先頭のみ
            // 3 held items afford only one cell costing 2, so the first cell is the only one left placeable
            CommonBlockPlaceCostMarker.MarkInsufficientCellsAsNotPlaceable(placeInfos, ForUnitTestModBlockId.BlockId, BuildInventory(3, 10), new PlacementFeedback());

            CollectionAssert.AreEqual(new[] { true, false, false, false, false }, placeInfos.ConvertAll(info => info.Placeable));
        }

        [Test]
        public void 全素材が足りていれば不足行を出さず全セルを保つ()
        {
            CreateServer();

            var placeInfos = BuildDragCells(3);
            var feedback = new PlacementFeedback();

            CommonBlockPlaceCostMarker.MarkInsufficientCellsAsNotPlaceable(placeInfos, ForUnitTestModBlockId.BlockId, BuildInventory(6, 3), feedback);

            Assert.IsEmpty(feedback.Lines);
            CollectionAssert.AreEqual(new[] { true, true, true }, placeInfos.ConvertAll(info => info.Placeable));
        }

        private static List<PlaceInfo> BuildDragCells(int cellCount)
        {
            var placeInfos = new List<PlaceInfo>(cellCount);
            for (var i = 0; i < cellCount; i++) placeInfos.Add(new PlaceInfo { Position = new Vector3Int(i, 0, 0), Placeable = true });
            return placeInfos;
        }

        private static List<IItemStack> BuildInventory(int material1Count, int material2Count)
        {
            return new List<IItemStack>
            {
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), material1Count),
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), material2Count),
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
