using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Map.MapVein;
using Client.Tests.UIState.Fakes;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
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
            var state = new VeinRestrictedPlacementState();
            state.SetRestriction(TutorialGuid, TargetVeinGuid, MasterHolder.BlockMaster.GetBlockId(MinerBlockGuid));

            PlacementVeinViewKindResolver.PushToView(view, state, new BlockPlacementTarget(MinerBlockGuid, null));
            Assert.AreEqual(TargetVeinGuid, view.DisplayPushes[^1].VeinTypeGuid);

            // 制限対象でないブロックは種別表示のまま。制限を無条件に効かせる実装はここで落ちる
            // A block outside the restriction keeps the kind view; an unconditional restriction fails here
            PlacementVeinViewKindResolver.PushToView(view, state, new BlockPlacementTarget(ChestBlockGuid, null));
            Assert.IsNull(view.DisplayPushes[^1].VeinTypeGuid);
            Assert.IsNull(view.DisplayPushes[^1].Kind);

            // 制限解除後は同じブロックでも単一表示へ戻らない
            // Once cleared, the very same block no longer shows the single vein
            state.Clear(TutorialGuid);
            PlacementVeinViewKindResolver.PushToView(view, state, new BlockPlacementTarget(MinerBlockGuid, null));
            Assert.IsNull(view.DisplayPushes[^1].VeinTypeGuid);
            Assert.AreEqual(MapVeinKind.Item, view.DisplayPushes[^1].Kind);
        }
    }
}
