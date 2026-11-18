using Client.Ui.State.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Text
{
    public class SlotAmountFormatTest
    {
        /// <web-test file="src/shared/ui/slotAmountFormat.test.ts">0 はそのまま</web-test>
        [Test]
        public void 零はそのまま()
        {
            Assert.AreEqual("0", SlotAmountFormat.Format(0));
        }

        /// <web-test file="src/shared/ui/slotAmountFormat.test.ts">千未満は区切らない</web-test>
        [Test]
        public void 千未満は区切らない()
        {
            Assert.AreEqual("500", SlotAmountFormat.Format(500));
        }

        /// <web-test file="src/shared/ui/slotAmountFormat.test.ts">1000 は千区切りになる</web-test>
        [Test]
        public void 千は千区切りになる()
        {
            Assert.AreEqual("1,000", SlotAmountFormat.Format(1000));
        }

        /// <web-test file="src/shared/ui/slotAmountFormat.test.ts">百万超も区切る</web-test>
        [Test]
        public void 百万超も区切る()
        {
            Assert.AreEqual("1,234,567", SlotAmountFormat.Format(1234567));
        }

        /// <web-test file="src/shared/ui/slotAmountFormat.test.ts">小数は丸めずそのまま出す</web-test>
        [Test]
        public void 小数は丸めずそのまま出す()
        {
            Assert.AreEqual("0.1", SlotAmountFormat.Format(0.1));
            Assert.AreEqual("1.4", SlotAmountFormat.Format(1.4));
        }

        /// <web-test file="src/shared/ui/slotAmountFormat.test.ts">千区切りと小数は同時に効く</web-test>
        [Test]
        public void 千区切りと小数は同時に効く()
        {
            Assert.AreEqual("1,234.5", SlotAmountFormat.Format(1234.5));
        }

        /// <web-test file="src/shared/ui/slotAmountFormat.test.ts">実行時残量の長い小数は表示桁で止める</web-test>
        [Test]
        public void 実行時残量の長い小数は表示桁で止める()
        {
            Assert.AreEqual("823.33", SlotAmountFormat.Format(823.3333333333334));
            Assert.AreEqual("0.3", SlotAmountFormat.Format(0.30000000000000004));
        }
    }
}
