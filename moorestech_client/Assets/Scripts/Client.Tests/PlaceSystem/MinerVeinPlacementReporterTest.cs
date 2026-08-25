using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.Map.MapVein;
using Client.Network.API;
using Core.Master;
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
        // ForUnitTest map.jsonに定義済みのアイテム鉱脈GUID
        // Item vein GUID defined in ForUnitTest map.json
        private const string ItemVeinGuid = "11111111-0000-0000-0000-000000000001";

        private static readonly Vector3Int VeinMinCell = new(0, 0, 0);
        private static readonly Vector3Int VeinMaxCell = new(2, 2, 2);
        private static readonly Vector3Int OutsideVeinCell = new(5, 0, 5);

        [Test]
        public void 鉱脈外の採掘機セルをPlaceableFalseにしカーソルセルだけ理由を出す()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(VeinMinCell),
                CreatePlaceInfo(OutsideVeinCell),
                CreatePlaceInfo(OutsideVeinCell + Vector3Int.right),
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
            CollectionAssert.AreEqual(new[] { MinerVeinPlacementReporter.MinerOutsideVeinLine() }, feedback.Lines);
        }

        [Test]
        public void 採掘機以外は鉱脈外でも素通しする()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(OutsideVeinCell) };
            var feedback = new PlacementFeedback();

            MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateRegistry(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a non-miner block was blocked by the vein check");
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        private static PlaceInfo CreatePlaceInfo(Vector3Int position)
        {
            return new PlaceInfo
            {
                Position = position,
                Direction = BlockDirection.North,
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
