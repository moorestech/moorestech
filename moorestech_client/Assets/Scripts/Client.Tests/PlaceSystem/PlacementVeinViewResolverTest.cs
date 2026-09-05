using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Tests.Map.Vein;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     設置対象ごとに表示される鉱脈が切り替わることを検証する
    ///     Verifies which veins are displayed switches per placement target
    /// </summary>
    public class PlacementVeinViewResolverTest
    {
        private const string MinableItemVeinGuid = "11111111-0000-0000-0000-000000000001";
        private const string FluidVeinGuid = "11111111-0000-0000-0000-000000000002";
        private const string UnmineableItemVeinGuid = "11111111-0000-0000-0000-000000000004";
        private const string SteamVeinGuid = "11111111-0000-0000-0000-000000000003";

        /// <summary>
        ///     表示は設置判定と同じ絞り込みでなければならない。掘れない鉱脈にボックスが出ると「見えるのに置けない」が再発する
        ///     The display must filter exactly as the placement check does; a box over an unmineable vein brings back "visible but refused"
        /// </summary>
        [Test]
        public void 採掘機は掘れるアイテム鉱脈だけを出しそれ以外のブロックは非表示になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var minerGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId).BlockGuid;
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;

            var registry = CreateRegistry();
            var noRestriction = new VeinRestrictedPlacementState();

            var minerDisplay = PlacementVeinViewResolver.Resolve(registry, noRestriction, new BlockPlacementTarget(minerGuid, null));
            CollectionAssert.AreEqual(new[] { Guid.Parse(MinableItemVeinGuid) }, ToVeinTypeGuids(minerDisplay));
            Assert.IsFalse(minerDisplay.Highlight);

            Assert.IsNull(PlacementVeinViewResolver.Resolve(registry, noRestriction, new BlockPlacementTarget(chestGuid, null)).Veins);

            // 対象未選択でも落ちずに非表示を返す。滞在中の非表示プッシュがここを通る
            // An absent target must return "hidden" instead of throwing; the hide push during the stay goes through here
            Assert.AreEqual(VeinDisplay.Hidden, PlacementVeinViewResolver.Resolve(registry, noRestriction, null));
        }

        [Test]
        public void 制限対象ブロックの間は対象鉱脈だけを強調表示する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var minerGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId).BlockGuid;
            var registry = CreateRegistry();
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(Guid.Parse("22222222-0000-0000-0000-000000000001"), Guid.Parse(UnmineableItemVeinGuid), ForUnitTestModBlockId.ElectricMinerId);

            var display = PlacementVeinViewResolver.Resolve(registry, state, new BlockPlacementTarget(minerGuid, null));

            CollectionAssert.AreEqual(new[] { Guid.Parse(UnmineableItemVeinGuid) }, ToVeinTypeGuids(display));
            Assert.IsTrue(display.Highlight, "the tutorial restriction must draw in the highlight color");
        }

        /// <summary>
        ///     ポンプ判定はIPumpParamで行う。具象クラス列挙に戻すとポンプ追加時に流体鉱脈が出なくなる
        ///     The pump check goes through IPumpParam; enumerating concrete params again would hide fluid veins for a newly added pump
        /// </summary>
        [Test]
        public void ポンプは汲み上げられる流体鉱脈だけを出す()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var gearPumpGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearPump).BlockGuid;
            var registry = CreateRegistry();
            var noRestriction = new VeinRestrictedPlacementState();

            var display = PlacementVeinViewResolver.Resolve(registry, noRestriction, new BlockPlacementTarget(gearPumpGuid, null));

            CollectionAssert.AreEqual(new[] { Guid.Parse(FluidVeinGuid) }, ToVeinTypeGuids(display));
            Assert.IsFalse(display.Highlight);
        }

        [Test]
        public void ポンプはgenerateFluidに無い流体の鉱脈を出さない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var pumpGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPump).BlockGuid;
            var registry = MapVeinAabbRegistryFixture.Create(
                new VeinLayoutMessagePack(FluidVeinGuid, 20, 0, 20, 20, 0, 20),
                new VeinLayoutMessagePack(SteamVeinGuid, 40, 0, 40, 40, 0, 40));

            var display = PlacementVeinViewResolver.Resolve(registry, new VeinRestrictedPlacementState(), new BlockPlacementTarget(pumpGuid, null));

            CollectionAssert.AreEqual(new[] { Guid.Parse(FluidVeinGuid) }, ToVeinTypeGuids(display));
        }

        private static List<Guid> ToVeinTypeGuids(VeinDisplay display)
        {
            var guids = new List<Guid>();
            foreach (var vein in display.Veins) guids.Add(vein.VeinTypeGuid);
            return guids;
        }

        private static MapVeinAabbRegistry CreateRegistry()
        {
            return MapVeinAabbRegistryFixture.Create(
                new VeinLayoutMessagePack(MinableItemVeinGuid, 0, 0, 0, 2, 2, 2),
                new VeinLayoutMessagePack(FluidVeinGuid, 20, 0, 20, 20, 0, 20),
                new VeinLayoutMessagePack(UnmineableItemVeinGuid, 30, 0, 30, 31, 0, 31));
        }
    }
}
