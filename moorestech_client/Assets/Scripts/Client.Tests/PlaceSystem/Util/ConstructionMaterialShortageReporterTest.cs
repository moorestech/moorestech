using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Construction;
using Client.Localization;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Util
{
    /// <summary>
    ///     不足行は不可セルを除いた設置予定セル分
    ///     財布の残りで賄える分は不足に数えない
    ///     Shortage lines cover only the cells left after blocked cells are excluded
    ///     What the wallet remainder covers is not counted as short
    /// </summary>
    public class ConstructionMaterialShortageReporterTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 必要数は設置不可セルを除いたセル数分になる()
        {
            CreateServer();

            // 5セル中2セルは既に不可、残り3セル分のみ算入
            // 2 of 5 cells are already blocked; only the remaining 3 count
            var placeInfos = BuildDragCells(ForUnitTestModBlockId.BlockId, 5);
            placeInfos[1].Placeable = false;
            placeInfos[3].Placeable = false;
            var feedback = new PlacementFeedback();

            ConstructionMaterialShortageReporter.ReportShortages(placeInfos, ForUnitTestModBlockId.BlockId, BuildWalletQuery(), BuildInventory(3, 10), feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("3", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("6", feedback.Lines[0].TextParams[2]);
        }

        [Test]
        public void 全素材が足りていれば不足行を出さない()
        {
            CreateServer();

            var placeInfos = BuildDragCells(ForUnitTestModBlockId.BlockId, 3);
            var feedback = new PlacementFeedback();

            ConstructionMaterialShortageReporter.ReportShortages(placeInfos, ForUnitTestModBlockId.BlockId, BuildWalletQuery(), BuildInventory(6, 3), feedback);

            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 財布の残りで賄えるセルは不足行に数えない()
        {
            CreateServer();

            // PlacementsPerCost=3のブロックを3セル。残り3なら1セットも払わない
            // Three cells of a PlacementsPerCost=3 block; a remainder of 3 pays no cost set at all
            var blockId = ForUnitTestModBlockId.GearBeltConveyor;
            var placeInfos = BuildDragCells(blockId, 3);
            var feedback = new PlacementFeedback();
            var walletQuery = BuildWalletQuery();
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.ApplyAll(new Dictionary<BlockId, int> { { ConstructionWalletUtil.ResolveWalletBlockId(blockId), 3 } });

            ConstructionMaterialShortageReporter.ReportShortages(placeInfos, blockId, new ConstructionWalletQuery(datastore), BuildInventory(0, 0), feedback);
            Assert.IsEmpty(feedback.Lines);

            // 残り0なら3セルで1セット分の不足が出る
            // With an empty wallet the same three cells fall one cost set short
            ConstructionMaterialShortageReporter.ReportShortages(placeInfos, blockId, walletQuery, BuildInventory(0, 0), feedback);
            Assert.IsNotEmpty(feedback.Lines);
        }

        private static ConstructionWalletQuery BuildWalletQuery()
        {
            return new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
        }

        private static List<PlaceInfo> BuildDragCells(BlockId blockId, int cellCount)
        {
            var placeInfos = new List<PlaceInfo>(cellCount);
            for (var i = 0; i < cellCount; i++) placeInfos.Add(new PlaceInfo { Position = new Vector3Int(i, 0, 0), Placeable = true, BlockId = blockId });
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
