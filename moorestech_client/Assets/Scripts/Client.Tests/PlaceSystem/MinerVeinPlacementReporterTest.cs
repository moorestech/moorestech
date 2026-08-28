using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.Map.MapVein;
using Client.Network.API;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Mooresmaster.Localization.Generated;
using Game.Block.Interface;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;

// namespaceは既存の隣接テスト（ConstructionCostPreviewMarkerTest等）に合わせること
// Match the namespace of sibling tests such as ConstructionCostPreviewMarkerTest
namespace Client.Tests.PlaceSystem
{
    public class MinerVeinPlacementReporterTest
    {
        // ForUnitTest map.jsonに定義済みの鉱脈GUID
        // Vein GUIDs defined in ForUnitTest map.json
        private const string ItemVeinGuid = "11111111-0000-0000-0000-000000000001";
        private const string FluidVeinGuid = "11111111-0000-0000-0000-000000000002";
        private const string UnmineableItemVeinGuid = "11111111-0000-0000-0000-000000000004";

        private static readonly Vector3Int VeinMinCell = new(0, 0, 0);
        private static readonly Vector3Int VeinMaxCell = new(2, 2, 2);
        private static readonly Vector3Int OutsideVeinCell = new(5, 0, 5);
        private static readonly Vector3Int FluidVeinCell = new(20, 0, 20);
        private static readonly Vector3Int UnmineableVeinCell = new(40, 0, 40);

        [Test]
        public void 鉱脈外の採掘機セルをPlaceableFalseにしカーソルセルだけ理由を出す()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(VeinMinCell, BlockDirection.North),
                CreatePlaceInfo(OutsideVeinCell, BlockDirection.North),
                CreatePlaceInfo(OutsideVeinCell + Vector3Int.right, BlockDirection.North),
            };
            var feedback = new PlacementFeedback();

            // カーソルは2番目（鉱脈外）のセル上にある
            // The cursor sits on the second cell, which is outside the vein
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, minerMaster, 1, CreateRegistry(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a miner over the vein was rejected");
            Assert.IsFalse(placeInfos[1].Placeable, "a miner outside the vein stayed placeable");
            Assert.IsFalse(placeInfos[2].Placeable, "a miner outside the vein stayed placeable");

            // 不可セルが2つでも理由行はカーソルセルの1行だけ
            // Two blocked cells still produce exactly one reason line, the cursor cell's
            CollectionAssert.AreEqual(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMinerOutsideVein) }, feedback.Lines);
        }

        [Test]
        public void 底面が1セルでも重なれば向きに関わらず設置可でYは見ない()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.OffsetDrillMinerId);
            var registry = CreateRegistry();

            // 北向き原点(-1,7,-2): x:-1..0 z:-2..0 でAABB角(0,0)に掛かる。Y=7は無視される
            // North at (-1,7,-2) spans x:-1..0 z:-2..0 and touches AABB corner (0,0); Y=7 is ignored
            var corner = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(-1, 7, -2), BlockDirection.North) };
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(corner, minerMaster, -1, registry, new PlacementFeedback());
            Assert.IsTrue(corner[0].Placeable, "a footprint touching the vein corner was rejected");

            // 原点(-2,0,-1)は向きで可否が反転する: 東はAABB角(0,0,0)に掛かり可、北は掛からず不可
            // Origin (-2,0,-1) flips by direction: East touches AABB corner (0,0,0) and is placeable, North misses it
            var eastTouchesCorner = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(-2, 0, -1), BlockDirection.East) };
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(eastTouchesCorner, minerMaster, -1, registry, new PlacementFeedback());
            Assert.IsTrue(eastTouchesCorner[0].Placeable, "an East footprint touching the vein corner was rejected");

            var northMissesVein = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(-2, 0, -1), BlockDirection.North) };
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(northMissesVein, minerMaster, -1, registry, new PlacementFeedback());
            Assert.IsFalse(northMissesVein[0].Placeable, "MarkOutsideVeinCellsAsNotPlaceable ignored PlaceInfo.Direction");

            // 隣接のみ（重ならない）は向きに関係なく不可のまま
            // A merely-adjacent footprint stays not placeable regardless of direction
            var adjacent = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(3, 0, 0), BlockDirection.East) };
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(adjacent, minerMaster, -1, registry, new PlacementFeedback());
            Assert.IsFalse(adjacent[0].Placeable, "an adjacent footprint was accepted");
        }

        /// <summary>
        ///     採掘機が掘れるのはアイテム鉱脈だけなので、流体鉱脈の上は設置可にしない
        ///     A miner can only mine item veins, so a fluid vein must not make the cell placeable
        /// </summary>
        [Test]
        public void 流体鉱脈の上は採掘機を設置可にしない()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(FluidVeinCell, BlockDirection.North) };

            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, minerMaster, -1, CreateRegistry(), new PlacementFeedback());

            Assert.IsFalse(placeInfos[0].Placeable, "a fluid vein made a miner placeable");
        }

        /// <summary>
        ///     置けるのに掘らない採掘機を作らないため、mineSettingsに無いアイテム鉱脈の上も設置不可
        ///     An item vein missing from mineSettings is not placeable either, so a placed miner always mines
        /// </summary>
        [Test]
        public void mineSettingsに無いアイテム鉱脈の上は採掘機を設置可にしない()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(UnmineableVeinCell, BlockDirection.North) };

            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, minerMaster, -1, CreateRegistry(), new PlacementFeedback());

            Assert.IsFalse(placeInfos[0].Placeable, "an unmineable item vein made a miner placeable");
        }

        [Test]
        public void 採掘機以外は鉱脈外でも素通しする()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(OutsideVeinCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateRegistry(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a non-miner block was blocked by the vein check");
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        private static PlaceInfo CreatePlaceInfo(Vector3Int position, BlockDirection direction)
        {
            return new PlaceInfo
            {
                Position = position,
                Direction = direction,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                Placeable = true,
            };
        }

        private static MapVeinAabbRegistry CreateRegistry()
        {
            // 台帳が読むのはMapLayout.MapVeinsだけなので、他の応答はdefaultで埋める
            // The registry only reads MapLayout.MapVeins, so every other response is left at default
            var veinLayouts = new List<VeinLayoutMessagePack>
            {
                new(ItemVeinGuid, VeinMinCell.x, VeinMinCell.y, VeinMinCell.z, VeinMaxCell.x, VeinMaxCell.y, VeinMaxCell.z),
                new(FluidVeinGuid, FluidVeinCell.x, FluidVeinCell.y, FluidVeinCell.z, FluidVeinCell.x, FluidVeinCell.y, FluidVeinCell.z),
                new(UnmineableItemVeinGuid, UnmineableVeinCell.x, UnmineableVeinCell.y, UnmineableVeinCell.z, UnmineableVeinCell.x, UnmineableVeinCell.y, UnmineableVeinCell.z),
            };
            var mapLayout = new GetMapDataProtocol.ResponseMapDataMessagePack(new Vector3MessagePack(Vector3.zero),
                new List<MapObjectLayoutMessagePack>(), veinLayouts, TerrainTransferMeta.CreateWithoutWorldDirectory(), string.Empty);
            var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new Vector3MessagePack(Vector3.zero), null, -1, null, null, null);

            return new MapVeinAabbRegistry(new InitialHandshakeResponse(handshake, (default, default, default, default, default, default, default, mapLayout)));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
