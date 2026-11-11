using System;
using System.Linq;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Core.Master;
using Game.PlacementTarget;
using NUnit.Framework;
using static Client.Tests.Ui.State.BuildMenu.BuildMenuTestEntries;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuShortageTest
    {
        private static readonly BuildMenuRequiredItem Lacking = new(new Guid("50000000-0000-4000-8000-000000000001"), 4, 1, true);
        private static readonly BuildMenuRequiredItem Enough = new(new Guid("50000000-0000-4000-8000-000000000002"), 2, 9, false);

        [Test]
        public void 免除なしのエントリでは足りない素材だけが不足になる()
        {
            var entry = EntryWith(false);
            Assert.IsTrue(BuildMenuShortage.IsItemInsufficient(entry, Lacking));
            Assert.IsFalse(BuildMenuShortage.IsItemInsufficient(entry, Enough));
            CollectionAssert.AreEqual(new[] { true, false }, ViewModelWith(new[] { Lacking, Enough }, false).Materials.Select(line => line.IsInsufficient).ToArray());
        }

        [Test]
        public void 支払い免除のエントリは足りない素材があっても不足にしない()
        {
            var entry = EntryWith(true);
            Assert.IsFalse(BuildMenuShortage.IsItemInsufficient(entry, Lacking));
            Assert.IsTrue(ViewModelWith(new[] { Lacking, Enough }, true).Materials.All(line => !line.IsInsufficient));
        }

        [Test]
        public void 足りない素材が無ければ不足の行は無い()
        {
            var entry = new BuildMenuCatalogEntry(Guid.NewGuid(), BuildMenuTestEntries.Mining, MinerSub, "x", new[] { Enough }, false);
            Assert.IsFalse(BuildMenuShortage.IsItemInsufficient(entry, Enough));
            Assert.IsTrue(ViewModelWith(new[] { Enough }, false).Materials.All(line => !line.IsInsufficient));
        }

        private static BuildMenuCatalogEntry EntryWith(bool paymentWaived)
        {
            return new BuildMenuCatalogEntry(Guid.NewGuid(), BuildMenuTestEntries.Mining, MinerSub, "x", new[] { Lacking, Enough }, paymentWaived);
        }

        // VM の ctor が判定した行ごとの不足を見る（画面のツールチップと詳細が読む値）
        // Reads the per-line shortage judged by the VM ctor (the value the tooltip and detail read)
        private static BuildMenuEntryViewModel ViewModelWith(BuildMenuRequiredItem[] items, bool paymentWaived)
        {
            var rows = items.Select(item => new BuildMenuMaterialRow(new ItemId(1), item)).ToList();
            return new BuildMenuEntryViewModel(Guid.NewGuid(), PlacementTargetKind.Block, BuildMenuTestEntries.Mining, MinerSub, "x", rows, paymentWaived, new PlacementTargetBlockIcon(new BlockId(1)), BuildMenuSetPlacementNone.Instance);
        }
    }
}
