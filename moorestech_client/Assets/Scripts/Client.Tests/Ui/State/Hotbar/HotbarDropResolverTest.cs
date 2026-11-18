using System;
using Client.Ui.State.Hotbar.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Hotbar
{
    public class HotbarDropResolverTest
    {
        private static readonly Guid EntryId = new("30000000-0000-4000-8000-0000000000aa");

        /// <web-test file="src/features/hotbar/hotbarDnd.test.ts">ビルドメニューエントリを枠へ落とすとassign</web-test>
        [Test]
        public void ビルドメニューのエントリを枠へ落とすと割当になる()
        {
            var action = HotbarDropResolver.Resolve(new BuildMenuEntryDragSource(EntryId), new HotbarSlotDropTarget(2));
            var assign = (HotbarAssignAction)action;
            Assert.AreEqual(2, assign.SlotIndex);
            Assert.AreEqual(EntryId, assign.TargetId);
        }

        /// <web-test file="src/features/hotbar/hotbarDnd.test.ts">枠から枠へ落とすとswap</web-test>
        [Test]
        public void 枠から枠へ落とすと入替になる()
        {
            var action = HotbarDropResolver.Resolve(new HotbarSlotDragSource(1), new HotbarSlotDropTarget(4));
            var swap = (HotbarSwapAction)action;
            Assert.AreEqual(1, swap.FromSlotIndex);
            Assert.AreEqual(4, swap.ToSlotIndex);
        }

        /// <web-test file="src/features/hotbar/hotbarDnd.test.ts">枠から枠外へ落とすとclear</web-test>
        [Test]
        public void 枠から枠外へ落とすと解除になる()
        {
            var action = HotbarDropResolver.Resolve(new HotbarSlotDragSource(1), OutsideHotbarDropTarget.Instance);
            Assert.AreEqual(1, ((HotbarClearAction)action).SlotIndex);
        }

        [Test]
        public void 枠を自分自身へ落としても何も変わらない()
        {
            var action = HotbarDropResolver.Resolve(new HotbarSlotDragSource(3), new HotbarSlotDropTarget(3));
            Assert.AreSame(HotbarNoDropAction.Instance, action);
        }

        [Test]
        public void ビルドメニューのエントリを枠外へ落としても何も変わらない()
        {
            var action = HotbarDropResolver.Resolve(new BuildMenuEntryDragSource(EntryId), OutsideHotbarDropTarget.Instance);
            Assert.AreSame(HotbarNoDropAction.Instance, action);
        }

        // 列の隙間はまだ HUD の中なので、枠から落としても解除しない（Web useHotbarDragSource の data-hotbar-row）
        // The row gap is still inside the HUD, so a drop from a slot does not clear (Web useHotbarDragSource data-hotbar-row)
        [Test]
        public void 枠を列の隙間へ落としても何も変わらない()
        {
            var action = HotbarDropResolver.Resolve(new HotbarSlotDragSource(1), HotbarRowGapDropTarget.Instance);
            Assert.AreSame(HotbarNoDropAction.Instance, action);
        }

        [Test]
        public void ビルドメニューのエントリを列の隙間へ落としても何も変わらない()
        {
            var action = HotbarDropResolver.Resolve(new BuildMenuEntryDragSource(EntryId), HotbarRowGapDropTarget.Instance);
            Assert.AreSame(HotbarNoDropAction.Instance, action);
        }
    }
}
