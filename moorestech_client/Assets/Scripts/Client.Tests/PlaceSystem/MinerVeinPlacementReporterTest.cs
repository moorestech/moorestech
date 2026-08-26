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

        private static readonly Vector3Int VeinMinCell = new(0, 0, 0);
        private static readonly Vector3Int VeinMaxCell = new(2, 2, 2);
        private static readonly Vector3Int OutsideVeinCell = new(5, 0, 5);
        private static readonly Vector3Int FluidVeinCell = new(20, 0, 20);

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

        /// <summary>
        ///     判定はブロック原点ではなくドリルセルで行う。原点で判定する実装はここで落ちる
        ///     The check runs on the drill cell, not the block origin; an origin-based implementation fails here
        /// </summary>
        [Test]
        public void 判定は原点ではなく回転後のドリルセルで行う()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.OffsetDrillMinerId);

            // 北向き: 原点は鉱脈内だがドリル(1,0,2)は鉱脈外
            // North: the origin is inside the vein while the drill at (1,0,2) is outside it
            var originInsideDrillOutside = new List<PlaceInfo> { CreatePlaceInfo(VeinMaxCell, BlockDirection.North) };
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(originInsideDrillOutside, minerMaster, -1, CreateRegistry(), new PlacementFeedback());
            Assert.IsFalse(originInsideDrillOutside[0].Placeable, "the check used the block origin instead of the drill cell");

            // 北向き: 原点は鉱脈外だがドリルは鉱脈内
            // North: the origin is outside the vein while the drill lands inside it
            var originOutsideDrillInside = new List<PlaceInfo> { CreatePlaceInfo(VeinMinCell - new Vector3Int(1, 0, 2), BlockDirection.North) };
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(originOutsideDrillInside, minerMaster, -1, CreateRegistry(), new PlacementFeedback());
            Assert.IsTrue(originOutsideDrillInside[0].Placeable, "a miner whose drill sits on the vein was rejected");

            // 同じ原点でも向きが変わればドリルセルが動くので判定も変わる
            // The same origin with a different direction moves the drill cell, so the verdict changes with it
            var rotated = new List<PlaceInfo> { CreatePlaceInfo(VeinMinCell - new Vector3Int(1, 0, 2), BlockDirection.East) };
            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(rotated, minerMaster, -1, CreateRegistry(), new PlacementFeedback());
            Assert.IsFalse(rotated[0].Placeable, "rotation did not move the drill cell");
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
