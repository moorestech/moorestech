using Client.Ui.State.Skit.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Skit
{
    /// <summary>
    /// 会話窓のクリックの帰結と、タイプライタの字数・即時表示の判定を確かめる（L0。Web の skit/interaction.test.ts）
    /// Verifies the dialogue-window click outcome and the typewriter's count and immediate-reveal rules (L0; Web skit/interaction.test.ts)
    /// </summary>
    public class SkitTypewriterRulesTest
    {
        /// <web-test file="src/features/skit/interaction.test.ts">reveals the full body without dispatching advance while typing</web-test>
        [Test]
        public void 打ち出し中のクリックは送らず全文を出す()
        {
            Assert.AreEqual(SkitClickOutcome.Reveal, SkitTypewriterRules.ClickOutcome(3, 10, true));
        }

        /// <web-test file="src/features/skit/interaction.test.ts">dispatches advance only after the full body is visible</web-test>
        [Test]
        public void 全文が出てからのクリックだけが送る()
        {
            Assert.AreEqual(SkitClickOutcome.Advance, SkitTypewriterRules.ClickOutcome(10, 10, true));
        }

        /// <web-test file="src/features/skit/interaction.test.ts">does nothing when advance is not allowed</web-test>
        [Test]
        public void 送りが許可されていなければ何もしない()
        {
            Assert.AreEqual(SkitClickOutcome.None, SkitTypewriterRules.ClickOutcome(10, 10, false));
        }

        /// <web-test file="src/features/skit/interaction.test.ts">reveals one Unicode code point per tick</web-test>
        [Test]
        public void 一刻みにコードポイントを1つずつ出す()
        {
            // 2 刻み目で "A" と絵文字（サロゲートペア 1 つ）が出る
            // At the second tick "A" and the emoji (one surrogate pair) are out
            const string body = "A😊B";
            var count = SkitTypewriterRules.RevealCountAt(100f, 50, SkitTypewriterRules.CountCodePoints(body));
            Assert.AreEqual(2, count);
            Assert.AreEqual("A😊", body.Substring(0, SkitTypewriterRules.PrefixLengthOfCodePoints(body, count)));
            Assert.AreEqual(3, SkitTypewriterRules.CountCodePoints(body));
        }

        /// <web-test file="src/features/skit/interaction.test.ts">reveals reconnect snapshots immediately and animates later events</web-test>
        [Test]
        public void 復元の提示は即座に全文を出しその後の変化は打ち出す()
        {
            Assert.IsTrue(SkitTypewriterRules.ShouldRevealImmediately(true, true, 50));
            Assert.IsFalse(SkitTypewriterRules.ShouldRevealImmediately(false, true, 50));
            Assert.IsTrue(SkitTypewriterRules.ShouldRevealImmediately(false, false, 50));
            Assert.IsTrue(SkitTypewriterRules.ShouldRevealImmediately(false, true, 0));
        }

        [Test]
        public void 経過時間の字数は間隔ごとに1つ増え全文で止まる()
        {
            Assert.AreEqual(0, SkitTypewriterRules.RevealCountAt(49f, 50, 3));
            Assert.AreEqual(1, SkitTypewriterRules.RevealCountAt(50f, 50, 3));
            Assert.AreEqual(3, SkitTypewriterRules.RevealCountAt(1000f, 50, 3));
        }
    }
}
