using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.UI.Tooltip;
using Client.Tests.Map.Vein;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

// namespaceは既存の隣接テスト（ConstructionCostPreviewMarkerTest等）に合わせること
// Match the namespace of sibling tests such as ConstructionCostPreviewMarkerTest
namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     鉱脈由来の2つの設置制限（採掘機の底面XZ重なり・チュートリアルの鉱脈限定）を検証する
    ///     Verifies both vein-bound placement restrictions: the miner's footprint XZ overlap and the tutorial vein limit
    /// </summary>
    public class VeinPlacementReporterTest
    {
        // ForUnitTest map.jsonに定義済みの鉱脈GUID
        // Vein GUIDs defined in ForUnitTest map.json
        private const string MinableItemVeinGuid = "11111111-0000-0000-0000-000000000001";
        private const string FluidVeinGuid = "11111111-0000-0000-0000-000000000002";

        // 採掘機のmineSettingsに無いアイテム鉱脈。掘れない鉱脈とチュートリアル限定対象の両方を兼ねる
        // An item vein absent from the miner's mineSettings; it serves as both the unmineable vein and the tutorial target
        private const string UnmineableItemVeinGuid = "11111111-0000-0000-0000-000000000004";

        private static readonly Guid RestrictionTutorialGuid = Guid.Parse("22222222-0000-0000-0000-000000000001");

        private static readonly Vector3Int VeinMinCell = new(0, 0, 0);
        private static readonly Vector3Int VeinMaxCell = new(2, 2, 2);
        private static readonly Vector3Int OutsideVeinCell = new(5, 0, 5);
        private static readonly Vector3Int FluidVeinCell = new(20, 0, 20);
        private static readonly Vector3Int UnmineableVeinMinCell = new(30, 0, 30);
        private static readonly Vector3Int UnmineableVeinMaxCell = new(31, 0, 31);

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
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, minerMaster, 1, CreateRegistry(), new VeinRestrictedPlacementState(), feedback);

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
            var noRestriction = new VeinRestrictedPlacementState();

            // 北向き原点(-1,7,-2): x:-1..0 z:-2..0 でAABB角(0,0)に掛かる。Y=7は無視される
            // North at (-1,7,-2) spans x:-1..0 z:-2..0 and touches AABB corner (0,0); Y=7 is ignored
            var corner = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(-1, 7, -2), BlockDirection.North) };
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(corner, minerMaster, -1, registry, noRestriction, new PlacementFeedback());
            Assert.IsTrue(corner[0].Placeable, "a footprint touching the vein corner was rejected");

            // 原点(-2,0,-1)は向きで可否が反転する: 東はAABB角(0,0,0)に掛かり可、北は掛からず不可
            // Origin (-2,0,-1) flips by direction: East touches AABB corner (0,0,0) and is placeable, North misses it
            var eastTouchesCorner = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(-2, 0, -1), BlockDirection.East) };
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(eastTouchesCorner, minerMaster, -1, registry, noRestriction, new PlacementFeedback());
            Assert.IsTrue(eastTouchesCorner[0].Placeable, "an East footprint touching the vein corner was rejected");

            var northMissesVein = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(-2, 0, -1), BlockDirection.North) };
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(northMissesVein, minerMaster, -1, registry, noRestriction, new PlacementFeedback());
            Assert.IsFalse(northMissesVein[0].Placeable, "MarkOutsideVeinCellsAsNotPlaceable ignored PlaceInfo.Direction");

            // 隣接のみ（重ならない）は向きに関係なく不可のまま
            // A merely-adjacent footprint stays not placeable regardless of direction
            var adjacent = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(3, 0, 0), BlockDirection.East) };
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(adjacent, minerMaster, -1, registry, noRestriction, new PlacementFeedback());
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

            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, minerMaster, -1, CreateRegistry(), new VeinRestrictedPlacementState(), new PlacementFeedback());

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
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(UnmineableVeinMinCell, BlockDirection.North) };

            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, minerMaster, -1, CreateRegistry(), new VeinRestrictedPlacementState(), new PlacementFeedback());

            Assert.IsFalse(placeInfos[0].Placeable, "an unmineable item vein made a miner placeable");
        }

        [Test]
        public void 採掘機以外は鉱脈外でも素通しする()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(OutsideVeinCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateRegistry(), new VeinRestrictedPlacementState(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a non-miner block was blocked by the vein check");
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 制限対象ブロックは対象鉱脈外のセルだけ不可にしカーソルセルに理由を出す()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var state = CreateRestrictedState(ForUnitTestModBlockId.ChestId);
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(UnmineableVeinMinCell, BlockDirection.North),
                CreatePlaceInfo(VeinMinCell, BlockDirection.North),
            };
            var feedback = new PlacementFeedback();

            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, chestMaster, 1, CreateRegistry(), state, feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a cell over the target vein was rejected");
            Assert.IsFalse(placeInfos[1].Placeable, "a cell over another vein stayed placeable");
            CollectionAssert.AreEqual(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein) }, feedback.Lines);
        }

        /// <summary>
        ///     チュートリアル限定も採掘機と同じXZ重なり規則で判定する。サーバーのチャレンジ達成判定と同じ規則
        ///     The tutorial limit judges by the same XZ overlap rule as the miner, exactly as the server challenge does
        /// </summary>
        [Test]
        public void 多セルの制限対象は底面が対象鉱脈にXZで重なれば置けYは見ない()
        {
            CreateServer();
            var multiBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MultiBlockGeneratorId);
            var state = CreateRestrictedState(ForUnitTestModBlockId.MultiBlockGeneratorId);
            var registry = CreateRegistry();

            // 3x1x2。原点は鉱脈外だが底面が鉱脈の角へ届く
            // 3x1x2: the origin sits outside while the footprint reaches the corner of the vein
            var originOutsideFootprintInside = new List<PlaceInfo> { CreatePlaceInfo(UnmineableVeinMinCell - new Vector3Int(2, 0, 1), BlockDirection.North) };
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(originOutsideFootprintInside, multiBlockMaster, -1, registry, state, new PlacementFeedback());
            Assert.IsTrue(originOutsideFootprintInside[0].Placeable, "the check looked only at the origin cell instead of the whole footprint");

            // 鉱脈AABBのYから外れても重なりは成立する（斜面での取りこぼしを消すのがADR 0039の趣旨）
            // Falling outside the vein's Y range still overlaps; removing slope dropouts is the point of ADR 0039
            var aboveVein = new List<PlaceInfo> { CreatePlaceInfo(UnmineableVeinMinCell - new Vector3Int(2, -9, 1), BlockDirection.North) };
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(aboveVein, multiBlockMaster, -1, registry, state, new PlacementFeedback());
            Assert.IsTrue(aboveVein[0].Placeable, "the tutorial restriction judged Y despite the XZ-only rule");

            // 底面が1セルも鉱脈に掛からなければ不可
            // Nothing is placeable when the footprint touches no cell of the vein
            var footprintOutside = new List<PlaceInfo> { CreatePlaceInfo(OutsideVeinCell, BlockDirection.North) };
            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(footprintOutside, multiBlockMaster, -1, registry, state, new PlacementFeedback());
            Assert.IsFalse(footprintOutside[0].Placeable, "a footprint entirely off the vein stayed placeable");
        }

        [Test]
        public void 制限対象でないブロックは素通しする()
        {
            CreateServer();
            var chestMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);
            var state = CreateRestrictedState(ForUnitTestModBlockId.MultiBlockGeneratorId);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(OutsideVeinCell, BlockDirection.North) };
            var feedback = new PlacementFeedback();

            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, chestMaster, 0, CreateRegistry(), state, feedback);

            Assert.IsTrue(placeInfos[0].Placeable);
            CollectionAssert.IsEmpty(feedback.Lines);
        }

        /// <summary>
        ///     制限は入れた本人のGUIDでしか落とせない。別チュートリアルのClearで消えると設置が素通しになる
        ///     Only the tutorial that set the restriction may clear it; another tutorial's clear would let placement through
        /// </summary>
        [Test]
        public void 制限は入れた本人のGUIDでしか解除されない()
        {
            CreateServer();
            var state = new VeinRestrictedPlacementState();
            var notified = 0;
            using var subscription = state.OnChanged.Subscribe(_ => notified++);

            state.SetRestriction(RestrictionTutorialGuid, Guid.Parse(UnmineableItemVeinGuid), ForUnitTestModBlockId.ElectricMinerId);
            Assert.IsTrue(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out var veinGuid));
            Assert.AreEqual(Guid.Parse(UnmineableItemVeinGuid), veinGuid);
            Assert.IsFalse(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ChestId, out _));

            state.Clear(Guid.Parse("22222222-0000-0000-0000-000000000009"));
            Assert.IsTrue(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out _), "another tutorial cleared a restriction it never set");

            state.Clear(RestrictionTutorialGuid);
            Assert.IsFalse(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out _));
            Assert.AreEqual(2, notified);
        }

        private static VeinRestrictedPlacementState CreateRestrictedState(BlockId restrictedBlockId)
        {
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(RestrictionTutorialGuid, Guid.Parse(UnmineableItemVeinGuid), restrictedBlockId);
            return state;
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
            return MapVeinAabbRegistryFixture.Create(
                new VeinLayoutMessagePack(MinableItemVeinGuid, VeinMinCell.x, VeinMinCell.y, VeinMinCell.z, VeinMaxCell.x, VeinMaxCell.y, VeinMaxCell.z),
                new VeinLayoutMessagePack(FluidVeinGuid, FluidVeinCell.x, FluidVeinCell.y, FluidVeinCell.z, FluidVeinCell.x, FluidVeinCell.y, FluidVeinCell.z),
                new VeinLayoutMessagePack(UnmineableItemVeinGuid, UnmineableVeinMinCell.x, UnmineableVeinMinCell.y, UnmineableVeinMinCell.z, UnmineableVeinMaxCell.x, UnmineableVeinMaxCell.y, UnmineableVeinMaxCell.z));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
