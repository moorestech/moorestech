using System;
using Client.Ui.State.Research.Logic;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// unlockEntries.ts の写しの並び順と通し番号を固定する（Web にテストが無いため新設。L0）
    /// Pins the order and running index of the unlockEntries.ts port (new, since the Web has no test; L0)
    /// </summary>
    public class UnlockEntriesTest
    {
        [Test]
        public void 解放物を種類順に並べ同じ値の行も通し番号で見分ける()
        {
            var unlocks = new ResearchUnlockSet(
                new[] { new UnlockBlock(new BlockId(1), Guid.NewGuid()) },
                new[] { new UnlockMachineRecipe(Guid.NewGuid(), new[] { new ItemId(2) }, new UnlockFluid[0]) },
                new[] { new ItemId(3), new ItemId(3) },
                new[] { new RewardItem(new ItemId(4), 5) },
                new[] { Guid.NewGuid() },
                new[] { Guid.NewGuid() },
                new[] { 64 },
                new[] { new LevelInheritanceCraftCap(new ItemId(6), 2) });

            var entries = UnlockEntries.ToUnlockEntries(unlocks);

            var expectedTypes = new[]
            {
                typeof(BlockUnlockEntry), typeof(MachineRecipeOutputUnlockEntry), typeof(ItemRecipeViewUnlockEntry), typeof(ItemRecipeViewUnlockEntry),
                typeof(RewardItemUnlockEntry), typeof(ConnectToolUnlockEntry), typeof(TrainCarUnlockEntry), typeof(ModuleSlotStackLimitUnlockEntry), typeof(LevelInheritanceCraftCapUnlockEntry),
            };
            Assert.AreEqual(expectedTypes.Length, entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(expectedTypes[i], entries[i].GetType(), $"entry #{i}");
                Assert.AreEqual(i, entries[i].Index, $"index #{i}");
            }
        }
    }
}
