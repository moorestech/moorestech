using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Client.Network.API;
using Core.Master;
using Game.Block.Interface;
using Game.MapGeneration.Transfer;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     チュートリアルが指す鉱脈の外へ対象ブロックを置けなくする制限を検証する
    ///     Verifies the restriction that keeps the target block off everything but the vein the tutorial points at
    /// </summary>
    public class VeinRestrictedPlacementReporterTest
    {
        private const string ItemVeinAGuid = "11111111-0000-0000-0000-000000000001";
        private const string ItemVeinBGuid = "11111111-0000-0000-0000-000000000004";

        private static readonly Vector3Int VeinACell = new(0, 0, 0);
        private static readonly Vector3Int VeinBCell = new(30, 0, 30);
        private static readonly Vector3Int OutsideAnyVeinCell = new(50, 0, 50);

        [Test]
        public void 対象鉱脈外のセルだけ不可にしカーソルセルに理由を出す()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(Guid.Parse(ItemVeinBGuid), ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(VeinBCell, BlockDirection.North),
                CreatePlaceInfo(VeinACell, BlockDirection.North),
            };
            var feedback = new PlacementFeedback();

            VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(placeInfos, minerMaster, 1, CreateRegistry(), state, feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a cell over the target vein was rejected");
            Assert.IsFalse(placeInfos[1].Placeable, "a cell over another vein stayed placeable");
            CollectionAssert.AreEqual(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein) }, feedback.Lines);
        }

        [Test]
        public void 制限対象でないブロックは素通しする()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(Guid.Parse(ItemVeinBGuid), ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(OutsideAnyVeinCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateRegistry(), state, feedback);

            Assert.IsTrue(placeInfos[0].Placeable);
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 制限が無ければ何もしない()
        {
            CreateServer();
            var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(OutsideAnyVeinCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            VeinRestrictedPlacementReporter.MarkOutsideTargetVeinCellsAsNotPlaceable(placeInfos, minerMaster, 0, CreateRegistry(), new VeinRestrictedPlacementState(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable);
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 状態の変更はOnChangedで通知されClearで消える()
        {
            CreateServer();
            var state = new VeinRestrictedPlacementState();
            var notified = 0;
            using var subscription = state.OnChanged.Subscribe(_ => notified++);

            state.SetRestriction(Guid.Parse(ItemVeinBGuid), ForUnitTestModBlockId.ElectricMinerId);
            Assert.IsTrue(state.IsRestrictedBlock(ForUnitTestModBlockId.ElectricMinerId));
            Assert.IsFalse(state.IsRestrictedBlock(ForUnitTestModBlockId.ChestId));

            state.Clear();

            Assert.AreEqual(2, notified);
            Assert.IsNull(state.VeinGuid);
            Assert.IsFalse(state.IsRestrictedBlock(ForUnitTestModBlockId.ElectricMinerId));
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
            var veinLayouts = new List<VeinLayoutMessagePack>
            {
                new(ItemVeinAGuid, VeinACell.x, VeinACell.y, VeinACell.z, 2, 2, 2),
                new(ItemVeinBGuid, VeinBCell.x, VeinBCell.y, VeinBCell.z, 31, 0, 31),
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
