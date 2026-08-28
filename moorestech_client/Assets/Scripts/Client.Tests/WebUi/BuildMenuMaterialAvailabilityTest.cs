using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Core.Master;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// 必要素材の合算と不足判定の回帰試験
    /// Regression tests for required-item aggregation and the shortage decision
    /// </summary>
    public class BuildMenuMaterialAvailabilityTest
    {
        [Test]
        public void 同一アイテムの必要数は合算してから所持と突き合わせる()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemGuid = MasterHolder.ItemMaster.Items.Data[0].ItemGuid;
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var target = new DuplicatedRequiredItemTarget(new[] { (itemGuid, 3), (itemGuid, 3) });

            // 行ごとなら4/3で足りるが、合算6に対しては不足する
            // Per row 4/3 suffices, but against the summed 6 it falls short
            var dtos = BuildMenuMaterialAvailability.CreateRequiredItemDtos(target, false, new Dictionary<ItemId, int> { { itemId, 4 } });

            Assert.AreEqual(1, dtos.Count);
            Assert.AreEqual(6, dtos[0].Count);
            Assert.AreEqual(4, dtos[0].Held);
            Assert.IsTrue(dtos[0].Lacking);
        }

        [Test]
        public void 支払いが免除される局面では所持ゼロでも不足にしない()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemGuid = MasterHolder.ItemMaster.Items.Data[0].ItemGuid;
            var target = new DuplicatedRequiredItemTarget(new[] { (itemGuid, 5) });

            var dtos = BuildMenuMaterialAvailability.CreateRequiredItemDtos(target, true, new Dictionary<ItemId, int>());

            Assert.AreEqual(0, dtos[0].Held);
            Assert.IsFalse(dtos[0].Lacking);
        }

        /// <summary>
        /// 必要素材を任意に並べるスタブ。実マスタは重複itemGuidを持たない
        /// Stub that lists arbitrary required items; the real master holds no duplicate itemGuid
        /// </summary>
        private class DuplicatedRequiredItemTarget : IPlacementTarget
        {
            private readonly IReadOnlyList<(Guid itemGuid, int count)> _requiredItems;

            public DuplicatedRequiredItemTarget(IReadOnlyList<(Guid itemGuid, int count)> requiredItems)
            {
                _requiredItems = requiredItems;
            }

            public Guid Id { get; } = Guid.NewGuid();
            public PlacementTargetKind Kind => PlacementTargetKind.Block;
            public string DisplayName => "stub";

            public IReadOnlyList<(Guid itemGuid, int count)> CreateRequiredItems() => _requiredItems;

            public bool Equals(IPlacementTarget other) => other != null && other.Id == Id;
        }
    }
}
