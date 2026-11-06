using System;
using Client.Ui.State.BuildMenu.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuScrollSpyTest
    {
        private static readonly Guid A = new("00000000-0000-4000-8000-00000000000a");
        private static readonly Guid B = new("00000000-0000-4000-8000-00000000000b");
        private static readonly Guid C = new("00000000-0000-4000-8000-00000000000c");
        private static readonly CategoryHeadingOffset[] Offsets = { new(A, 0f), new(B, 300f), new(C, 720f) };

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">先頭より上は先頭カテゴリ</web-test>
        [Test]
        public void 先頭より上は先頭カテゴリ()
        {
            Assert.AreEqual(A, BuildMenuScrollSpy.ActiveCategoryAtScroll(Offsets, 0f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">見出しの間は直前の見出しのカテゴリ</web-test>
        [Test]
        public void 見出しの間は直前の見出しのカテゴリ()
        {
            Assert.AreEqual(A, BuildMenuScrollSpy.ActiveCategoryAtScroll(Offsets, 298f));
            Assert.AreEqual(B, BuildMenuScrollSpy.ActiveCategoryAtScroll(Offsets, 500f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">見出し上端ちょうど（±1px）はその見出しのカテゴリ</web-test>
        [Test]
        public void 見出しの上端ちょうどはその見出しのカテゴリ()
        {
            Assert.AreEqual(B, BuildMenuScrollSpy.ActiveCategoryAtScroll(Offsets, 300f));
            Assert.AreEqual(C, BuildMenuScrollSpy.ActiveCategoryAtScroll(Offsets, 719f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">許容は全見出しに一律適用される</web-test>
        [Test]
        public void 許容は全見出しに一律に効く()
        {
            Assert.AreEqual(B, BuildMenuScrollSpy.ActiveCategoryAtScroll(Offsets, 299f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">末尾を越えても末尾カテゴリ</web-test>
        [Test]
        public void 末尾を越えても末尾カテゴリ()
        {
            Assert.AreEqual(C, BuildMenuScrollSpy.ActiveCategoryAtScroll(Offsets, 5000f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">見出しが無ければnull</web-test>
        [Test]
        public void 見出しが無ければ現在地も無い()
        {
            Assert.IsNull(BuildMenuScrollSpy.ActiveCategoryAtScroll(Array.Empty<CategoryHeadingOffset>(), 10f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">先頭見出しのtopが0でなくても先頭より上は先頭カテゴリ</web-test>
        [Test]
        public void 先頭の見出しが0でなくても先頭より上は先頭カテゴリ()
        {
            var padded = new[] { new CategoryHeadingOffset(A, 24f), new CategoryHeadingOffset(B, 300f) };
            Assert.AreEqual(A, BuildMenuScrollSpy.ActiveCategoryAtScroll(padded, 0f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">top昇順でない配列でも視口位置から正しいカテゴリを選ぶ</web-test>
        [Test]
        public void 上端の昇順でない並びでも視口の位置から選ぶ()
        {
            // わざと降順で渡し、配列順ではなく上端の大小で選んでいることを確かめる
            // Deliberately pass descending tops to verify selection uses magnitude, not array order
            var unordered = new[] { new CategoryHeadingOffset(C, 720f), new CategoryHeadingOffset(A, 0f), new CategoryHeadingOffset(B, 300f) };
            Assert.AreEqual(A, BuildMenuScrollSpy.ActiveCategoryAtScroll(unordered, 0f));
            Assert.AreEqual(B, BuildMenuScrollSpy.ActiveCategoryAtScroll(unordered, 500f));
            Assert.AreEqual(C, BuildMenuScrollSpy.ActiveCategoryAtScroll(unordered, 5000f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">目標±1px以内で到達</web-test>
        [Test]
        public void 目標の1px以内なら到達()
        {
            Assert.IsTrue(BuildMenuScrollSpy.IsJumpSettled(299.4f, 300f));
            Assert.IsTrue(BuildMenuScrollSpy.IsJumpSettled(301f, 300f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">それ以上離れていれば未到達</web-test>
        [Test]
        public void それより離れていれば未到達()
        {
            Assert.IsFalse(BuildMenuScrollSpy.IsJumpSettled(297f, 300f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">末尾群が視口より短ければ差分を返す</web-test>
        [Test]
        public void 末尾群が視口より短ければ差分を返す()
        {
            Assert.AreEqual(380f, BuildMenuScrollSpy.TrailingSpacerHeight(600f, 220f));
        }

        /// <web-test file="src/features/buildMenu/logic/buildMenuScrollSpy.test.ts">末尾群が視口以上なら0</web-test>
        [Test]
        public void 末尾群が視口以上なら0()
        {
            Assert.AreEqual(0f, BuildMenuScrollSpy.TrailingSpacerHeight(600f, 600f));
            Assert.AreEqual(0f, BuildMenuScrollSpy.TrailingSpacerHeight(600f, 900f));
        }
    }
}
