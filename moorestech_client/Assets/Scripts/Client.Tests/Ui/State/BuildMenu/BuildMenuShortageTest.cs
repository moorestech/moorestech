using System;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;
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
            CollectionAssert.AreEqual(new[] { Lacking }, BuildMenuShortage.InsufficientItems(entry).ToArray());
        }

        [Test]
        public void 支払い免除のエントリは足りない素材があっても不足にしない()
        {
            var entry = EntryWith(true);
            Assert.IsFalse(BuildMenuShortage.IsItemInsufficient(entry, Lacking));
            Assert.AreEqual(0, BuildMenuShortage.InsufficientItems(entry).Count);
        }

        [Test]
        public void 足りない素材が無ければ不足の行は空()
        {
            var entry = new BuildMenuCatalogEntry(Guid.NewGuid(), BuildMenuTestEntries.Mining, MinerSub, "x", new[] { Enough }, false);
            Assert.AreEqual(0, BuildMenuShortage.InsufficientItems(entry).Count);
        }

        private static BuildMenuCatalogEntry EntryWith(bool paymentWaived)
        {
            return new BuildMenuCatalogEntry(Guid.NewGuid(), BuildMenuTestEntries.Mining, MinerSub, "x", new[] { Lacking, Enough }, paymentWaived);
        }
    }
}
