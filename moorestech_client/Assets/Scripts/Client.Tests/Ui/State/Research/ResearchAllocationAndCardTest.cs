using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Game.Research;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// Web の researchLogic.test.ts のうち allocateResearchItems・isConsumeItemLacking・deriveNodeCardState・hasEnoughItems の写し（L0）
    /// Port of the allocateResearchItems, isConsumeItemLacking, deriveNodeCardState and hasEnoughItems parts of Web researchLogic.test.ts (L0)
    /// </summary>
    public class ResearchAllocationAndCardTest
    {
        /// <web-test file="src/features/research/researchLogic.test.ts">複数受付同士は受付数で並べ替えず宣言順で配分する</web-test>
        [Test]
        public void 複数受付同士は受付数で並べ替えず宣言順で配分する()
        {
            var items = new[] { Consume(1, 1, new[] { 1, 2, 3 }), Consume(1, 1, new[] { 1, 2 }), Consume(3, 1, new[] { 3 }) };
            var owned = new Dictionary<ItemId, int> { [new ItemId(1)] = 1, [new ItemId(3)] = 1 };
            CollectionAssert.AreEqual(new[] { 1, 0, 1 }, ResearchLogic.AllocateResearchItems(items, owned));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">所持数が判明していれば不足を検出する</web-test>
        [Test]
        public void 所持数が分かっていれば不足を検出する()
        {
            Assert.IsTrue(ResearchLogic.IsConsumeItemLacking(ResearchNodeState.Researchable, 2, 1));
            Assert.IsFalse(ResearchLogic.IsConsumeItemLacking(ResearchNodeState.Researchable, 2, 2));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">所持数未受信(null)の間は不足を出さない(D4)</web-test>
        [Test]
        public void 所持数が未受信の間は不足を出さない()
        {
            // 未受信を所持 0 と読み違えない
            // Unknown counts are never misread as owning zero
            Assert.IsFalse(ResearchLogic.IsConsumeItemLacking(ResearchNodeState.Researchable, 1, null));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">上位レベルを含む受け付けIDの所持を合計して判定する</web-test>
        [Test]
        public void 上位レベルを含む受け付けIDの所持を合計して判定する()
        {
            var items = new[] { Consume(1, 2, new[] { 1, 11 }) };
            var both = ResearchLogic.AllocateResearchItems(items, new Dictionary<ItemId, int> { [new ItemId(1)] = 1, [new ItemId(11)] = 1 });
            Assert.IsFalse(ResearchLogic.IsConsumeItemLacking(ResearchNodeState.Researchable, 2, both[0]));
            var higherOnly = ResearchLogic.AllocateResearchItems(items, new Dictionary<ItemId, int> { [new ItemId(11)] = 1 });
            Assert.IsTrue(ResearchLogic.IsConsumeItemLacking(ResearchNodeState.Researchable, 2, higherOnly[0]));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">完了ノードはcompletedのみ立つ</web-test>
        [Test]
        public void 完了ノードは完了だけが立つ()
        {
            AssertCard(ResearchNodeState.Completed, true, false, false);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">前提未達はlocked</web-test>
        [Test]
        public void 前提未達はロック()
        {
            AssertCard(ResearchNodeState.UnresearchableNotEnoughPreNode, false, false, true);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">アイテム不足はready無しの通常表示</web-test>
        [Test]
        public void アイテム不足は研究可能の印の無い通常表示()
        {
            AssertCard(ResearchNodeState.UnresearchableNotEnoughItem, false, false, false);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">全条件未達はlocked</web-test>
        [Test]
        public void 全条件未達はロック()
        {
            AssertCard(ResearchNodeState.UnresearchableAllReasons, false, false, true);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">researchableはready</web-test>
        [Test]
        public void 研究可能は研究可能の印()
        {
            AssertCard(ResearchNodeState.Researchable, false, true, false);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">checks owned counts against consume items</web-test>
        [Test]
        public void 所持数を消費アイテムの必要数と照らし合わせる()
        {
            // 必要 1x3 に対し所持 3 なら足り、2 なら足りない
            // Owning 3 covers a need of 1x3, owning 2 does not
            var requirements = new[] { new ItemAmount(new ItemId(1), 3) };
            Assert.IsTrue(OwnedItemCounts.HasEnoughItems(requirements, new Dictionary<ItemId, int> { [new ItemId(1)] = 3 }));
            Assert.IsFalse(OwnedItemCounts.HasEnoughItems(requirements, new Dictionary<ItemId, int> { [new ItemId(1)] = 2 }));
        }

        private static ResearchConsumeItem Consume(int itemId, int count, int[] accepted)
        {
            var ids = new ItemId[accepted.Length];
            for (var i = 0; i < accepted.Length; i++) ids[i] = new ItemId(accepted[i]);
            return new ResearchConsumeItem(new ItemId(itemId), count, ids);
        }

        private static void AssertCard(ResearchNodeState state, bool completed, bool ready, bool locked)
        {
            var card = ResearchLogic.DeriveNodeCardState(state);
            Assert.AreEqual(completed, card.Completed, "completed");
            Assert.AreEqual(ready, card.Ready, "ready");
            Assert.AreEqual(locked, card.Locked, "locked");
        }
    }
}
