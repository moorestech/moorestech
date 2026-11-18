using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Slots.Item
{
    /// <summary>
    /// ItemSlotPresentation が Web の ItemSlot の式どおりに面・ゴースト・バッジ・ツールチップを決めることを確かめる（L0）
    /// Verifies ItemSlotPresentation derives face, ghost, badge and tooltip by the Web ItemSlot formulas (L0)
    /// </summary>
    public class ItemSlotPresentationTest
    {
        private static readonly ItemSlotMarks Catalog = new(false, true, false);

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">countがundefinedの時はバッジを表示しない</web-test>
        [Test]
        public void 個数が未指定なら白面で絵を出しバッジを出さない()
        {
            var presentation = ItemSlotPresentation.From(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), null, ItemSlotMarks.None, null, null, null));

            Assert.IsTrue(presentation.Filled);
            Assert.IsNotNull(presentation.ShownIcon);
            Assert.IsFalse(presentation.ShowsBadge);
        }

        [Test]
        public void 個数が0なら空きマスでバッジもツールチップも出さない()
        {
            var presentation = ItemSlotPresentation.From(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 0, ItemSlotMarks.None, null, null, null));

            Assert.IsFalse(presentation.Filled);
            Assert.IsNull(presentation.ShownIcon);
            Assert.IsFalse(presentation.ShowsBadge);
            Assert.IsNull(presentation.Tooltip);
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">countが正の数の時はバッジを表示する</web-test>
        [Test]
        public void 個数が正ならバッジにその数を出す()
        {
            var presentation = ItemSlotPresentation.From(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 7, ItemSlotMarks.None, null, null, null));

            Assert.IsTrue(presentation.ShowsBadge);
            Assert.AreEqual(7, presentation.BadgeCount);
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">countが0の時はバッジを表示しない</web-test>
        [Test]
        public void カタログは未所持なら灰面で絵を出し所持なら白面で個数を出す()
        {
            var unowned = ItemSlotPresentation.From(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 0, Catalog, null, null, null));
            var owned = ItemSlotPresentation.From(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 4, Catalog, null, null, null));

            Assert.IsFalse(unowned.Filled);
            Assert.IsNotNull(unowned.ShownIcon);
            Assert.IsFalse(unowned.ShowsBadge);
            Assert.IsTrue(owned.Filled);
            Assert.AreEqual(4, owned.BadgeCount);
        }

        [Test]
        public void ゴーストは空きマスにだけ出て白面にしない()
        {
            var ghost = new ItemSlotGhost(SlotTestIcons.WithTexture("2", "Copper"), 3);
            var onEmpty = ItemSlotPresentation.From(new ItemSlotContent(null, null, ItemSlotMarks.None, null, ghost, null));
            var onItem = ItemSlotPresentation.From(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 5, ItemSlotMarks.None, null, ghost, null));

            Assert.IsTrue(onEmpty.GhostShown);
            Assert.IsFalse(onEmpty.Filled);
            Assert.AreSame(ghost.Icon, onEmpty.ShownIcon);
            Assert.AreEqual(3, onEmpty.BadgeCount);
            Assert.IsFalse(onItem.GhostShown);
            Assert.AreEqual(5, onItem.BadgeCount);
        }

        [Test]
        public void ツールチップは上書きがあれば上書きを無ければ表示中の名前を使う()
        {
            var icon = SlotTestIcons.WithTexture("1", "Iron");
            var custom = UiText.FromVerbatim("custom");

            Assert.AreSame(icon.Label, ItemSlotPresentation.From(new ItemSlotContent(icon, 1, ItemSlotMarks.None, null, null, null)).Tooltip);
            Assert.AreSame(custom, ItemSlotPresentation.From(new ItemSlotContent(icon, 1, ItemSlotMarks.None, null, null, custom)).Tooltip);
        }

        [Test]
        public void ツールチップは不足の文と上書きの両方があれば不足の文を使う()
        {
            var icon = SlotTestIcons.WithTexture("1", "Iron");
            var custom = UiText.FromVerbatim("custom");
            var shortageText = UiText.FromVerbatim("shortage");
            var shortage = new ItemSlotShortage(1, 3, true, shortageText);

            Assert.AreSame(shortageText, ItemSlotPresentation.From(new ItemSlotContent(icon, 1, ItemSlotMarks.None, shortage, null, custom)).Tooltip);
        }
    }
}
