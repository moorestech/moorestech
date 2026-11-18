using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Game.Research;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// Web の researchAllocation.test.ts の写し（L0）
    /// Port of Web researchAllocation.test.ts (L0)
    /// </summary>
    public class ResearchAllocationTest
    {
        /// <web-test file="src/features/research/researchAllocation.test.ts">reserves exact variants regardless of requirement order: %s</web-test>
        [TestCase(false)]
        [TestCase(true)]
        public void 要求の並び順によらず完全一致の素材を先に確保する(bool reverse)
        {
            // 汎用受付と完全一致の 2 要求を並びだけ入れ替える
            // Two requirements, one generic and one exact, swapped only in order
            var baseItem = Consume(1, 1, new[] { 1, 2, 3 });
            var exactItem = Consume(2, 1, new[] { 2 });
            var items = reverse ? new[] { exactItem, baseItem } : new[] { baseItem, exactItem };
            var owned = new Dictionary<ItemId, int> { [new ItemId(2)] = 1 };

            // 完全一致が先に確保され、呼び出し側の所持数は変わらない
            // Exact is reserved first and the caller's owned counts stay untouched
            CollectionAssert.AreEqual(reverse ? new[] { 1, 0 } : new[] { 0, 1 }, ResearchLogic.AllocateResearchItems(items, owned));
            Assert.AreEqual(1, owned.Count);
            Assert.AreEqual(1, owned[new ItemId(2)]);

            // 3 を足すと汎用側も満たされる
            // Adding item 3 lets the generic requirement be satisfied too
            owned[new ItemId(3)] = 1;
            CollectionAssert.AreEqual(new[] { 1, 1 }, ResearchLogic.AllocateResearchItems(items, owned));
        }

        private static ResearchConsumeItem Consume(int itemId, int count, int[] accepted)
        {
            var ids = new ItemId[accepted.Length];
            for (var i = 0; i < accepted.Length; i++) ids[i] = new ItemId(accepted[i]);
            return new ResearchConsumeItem(new ItemId(itemId), count, ids);
        }
    }
}
