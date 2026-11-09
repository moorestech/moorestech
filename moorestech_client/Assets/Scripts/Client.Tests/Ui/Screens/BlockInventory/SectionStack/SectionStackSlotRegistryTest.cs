using System;
using Client.Ui.Screens.BlockInventory.SectionStack;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 区画の差し込み口の起動時の検査（二重登録・ItemGrid の外部登録・空き Slot の情報ログ）
    /// Startup checks of the section slots (duplicates, an external ItemGrid, the info log of empty slots)
    /// </summary>
    public class SectionStackSlotRegistryTest
    {
        [Test]
        public void 同じSlotの二重登録は例外()
        {
            var factories = new ISectionStackSectionFactory[] { new FakeSectionFactory(SectionStackSlot.Machine, true), new FakeSectionFactory(SectionStackSlot.Machine, true) };
            Assert.Throws<InvalidOperationException>(() => new SectionStackSlotRegistry(factories));
        }

        [Test]
        public void ItemGridの外部登録は例外()
        {
            var factories = new ISectionStackSectionFactory[] { new FakeSectionFactory(SectionStackSlot.ItemGrid, true) };
            Assert.Throws<InvalidOperationException>(() => new SectionStackSlotRegistry(factories));
        }

        [Test]
        public void 空きのSlotは起動時に1回だけ情報ログに並ぶ()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Context.*Miner"));
            var registry = new SectionStackSlotRegistry(new ISectionStackSectionFactory[] { new FakeSectionFactory(SectionStackSlot.Machine, true) });
            Assert.IsNull(registry.CreateOrNull(SectionStackSlot.Miner, new SectionStackSectionMount(null, false)));
            Assert.IsNotNull(registry.CreateOrNull(SectionStackSlot.Machine, new SectionStackSectionMount(null, false)));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
