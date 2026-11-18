using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Tests.Map.Vein;
using Client.Tests.UIState.Fakes;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module.TestMod;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     設置対象と鉱脈限定の状態から鉱脈範囲表示へ渡る表示状態を検証する
    ///     Verifies the display state pushed to the vein range view from the placement target and the restriction
    /// </summary>
    public class PlacementVeinViewPushTest
    {
        private static readonly Guid TutorialGuid = Guid.Parse("22222222-0000-0000-0000-000000000001");
        private static readonly Guid TargetVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000004");
        private static readonly Guid MinableItemVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");

        // ForUnitTestの採掘機とチェスト。採掘機はアイテム鉱脈を見たがり、チェストはどの鉱脈も見ない
        // ForUnitTest's miner and chest: the miner wants item veins, the chest wants none
        private static readonly Guid MinerBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000006");
        private static readonly Guid ChestBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000007");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 制限対象ブロックを持つ間だけ対象鉱脈だけの表示になる()
        {
            var view = new FakeMapVeinRangeView();
            var registry = CreateRegistry();
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(TutorialGuid, TargetVeinGuid, MasterHolder.BlockMaster.GetBlockId(MinerBlockGuid));

            PlacementVeinViewResolver.PushToView(view, registry, state, new BlockPlacementTarget(MinerBlockGuid, null));
            CollectionAssert.AreEqual(new[] { TargetVeinGuid }, ToVeinTypeGuids(view.DisplayPushes[^1]));
            Assert.IsTrue(view.DisplayPushes[^1].Highlight);

            // 制限対象でないブロックは制限を受けない。制限を無条件に効かせる実装はここで落ちる
            // A block outside the restriction is unaffected; an unconditional restriction fails here
            PlacementVeinViewResolver.PushToView(view, registry, state, new BlockPlacementTarget(ChestBlockGuid, null));
            Assert.IsNull(view.DisplayPushes[^1].Veins);

            // 制限解除後は同じブロックでも単一表示へ戻らない
            // Once cleared, the very same block no longer shows the single vein
            state.Clear(TutorialGuid);
            PlacementVeinViewResolver.PushToView(view, registry, state, new BlockPlacementTarget(MinerBlockGuid, null));
            CollectionAssert.AreEqual(new[] { MinableItemVeinGuid }, ToVeinTypeGuids(view.DisplayPushes[^1]));
            Assert.IsFalse(view.DisplayPushes[^1].Highlight);
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
                new VeinLayoutMessagePack(MinableItemVeinGuid.ToString("D"), 0, 0, 0, 2, 2, 2),
                new VeinLayoutMessagePack(TargetVeinGuid.ToString("D"), 30, 0, 30, 31, 0, 31));
        }
    }
}
