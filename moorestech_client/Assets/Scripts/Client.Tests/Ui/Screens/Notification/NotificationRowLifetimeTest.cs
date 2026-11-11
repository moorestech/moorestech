using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.Screens.Notification.Parts;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 行の入場と、生存尺の終端から逆算した遅延で始まる退場の 2 本の tween を経過時間で確かめる（Web の notificationAnimationDesign.test.ts の 2 件）
    /// Verifies the two tweens of a row, the enter and the exit delayed back from the lifetime end, by elapsed time (2 tests of Web notificationAnimationDesign.test.ts)
    /// </summary>
    public class NotificationRowLifetimeTest
    {
        private const float Tolerance = 0.001f;
        private static readonly NotificationRowTiming WebTiming = new(160f, 200f, 7000f, 12f);
        private VisualElement _row;

        [SetUp]
        public void CreateRow()
        {
            _row = new VisualElement();
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">入場と退場の2本を持ち、退場は生存尺から逆算した遅延で始まる</web-test>
        [Test]
        public void 入場と退場の2本を持ち退場は生存尺から逆算した遅延で始まる()
        {
            var enter = NotificationRowLifetime.CreateEnter(_row, WebTiming);
            var exit = NotificationRowLifetime.CreateExit(_row, WebTiming);

            // 入場の半ば（CSS ease-out で 0.6846）。退場は遅延中で何も書かない
            // Midway through the enter (0.6846 on CSS ease-out); the exit is still delayed and writes nothing
            Assert.IsFalse(AdvanceBoth(enter, exit, 80f));
            Assert.AreEqual(0.6846f, Opacity(), Tolerance);
            Assert.AreEqual(-12f * (1f - 0.6846f), TranslateX(), 0.01f);

            // 退場は 7000-200=6800ms から CSS ease-in で始まり、生存尺の終端で終わる
            // The exit starts at 7000-200=6800ms on CSS ease-in and ends at the lifetime end
            Assert.IsFalse(AdvanceBoth(enter, exit, 6799f));
            Assert.AreEqual(1f, Opacity(), Tolerance);
            Assert.IsFalse(AdvanceBoth(enter, exit, 6900f));
            Assert.AreEqual(1f - 0.3154f, Opacity(), Tolerance);
            Assert.IsTrue(AdvanceBoth(enter, exit, 7000f));
            Assert.AreEqual(0f, Opacity(), Tolerance);
            Assert.AreEqual(-12f, TranslateX(), Tolerance);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">退場のfill-modeはforwardsで入場を巻き戻さない</web-test>
        [Test]
        public void 保持の間は入場の終わりの姿のまま退場を先取りしない()
        {
            var enter = NotificationRowLifetime.CreateEnter(_row, WebTiming);
            var exit = NotificationRowLifetime.CreateExit(_row, WebTiming);
            foreach (var elapsed in new[] { 160f, 3000f, 6800f })
            {
                Assert.IsFalse(AdvanceBoth(enter, exit, elapsed));
                Assert.AreEqual(1f, Opacity(), Tolerance, $"opacity at {elapsed}");
                Assert.AreEqual(0f, TranslateX(), Tolerance, $"translate at {elapsed}");
            }
        }

        [Test]
        public void 尺が0なら即座に表示し生存尺の終端で消える()
        {
            var zero = new NotificationRowTiming(0f, 0f, 7000f, 0f);
            var enter = NotificationRowLifetime.CreateEnter(_row, zero);
            var exit = NotificationRowLifetime.CreateExit(_row, zero);
            Assert.IsFalse(AdvanceBoth(enter, exit, 0f));
            Assert.AreEqual(1f, Opacity(), Tolerance);
            Assert.IsFalse(AdvanceBoth(enter, exit, 6999f));
            Assert.AreEqual(1f, Opacity(), Tolerance);
            Assert.IsTrue(AdvanceBoth(enter, exit, 7000f));
            Assert.AreEqual(0f, Opacity(), Tolerance);
        }

        // 本番と同じく入場→退場の順に同じ経過時間で進め、退場の終わりを返す
        // Advance enter then exit with the same elapsed time as in production and return the exit's end
        private static bool AdvanceBoth(UiTweenAnimation enter, UiTweenAnimation exit, float elapsedMilliseconds)
        {
            enter.Advance(elapsedMilliseconds);
            return exit.Advance(elapsedMilliseconds);
        }

        private float Opacity()
        {
            return _row.style.opacity.value;
        }

        private float TranslateX()
        {
            return _row.style.translate.value.x.value;
        }
    }
}
