using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Ui.Screens.Tutorial.Pulse;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.Tutorial.Design
{
    public class TutorialAttentionTokenTest
    {
        private static readonly string[] UssFiles =
        {
            "Overlay/TutorialOverlay.uss", "KeyControlHint/KeyControlHint.uss", "WorldPin/WorldPin.uss", "DragGuide/TutorialDragGuide.uss",
        };

        private static readonly string[] PulseUserFiles =
        {
            "Overlay/TutorialOverlayView.cs", "Overlay/TutorialOutlineView.cs", "KeyControlHint/KeyControlHintView.cs",
            "WorldPin/WorldPinOverlayView.cs", "WorldPin/WorldPinEdgeArrowView.cs",
        };

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">原色赤と周期はtokensが唯一の正</web-test>
        [Test]
        public void 原色赤と周期はトークンだけが持つ()
        {
            Assert.AreEqual("#ff0000", ThemeTokenFile.Resolve("--tutorial-attention-red"));
            Assert.AreEqual("1200ms", ThemeTokenFile.Resolve("--tutorial-pulse-duration"));
            foreach (var path in UssFiles)
            {
                var uss = TutorialDesignSource.ReadScreenFile(path);
                StringAssert.DoesNotContain("#ff0000", uss.ToLowerInvariant(), path);
                StringAssert.DoesNotContain("1200ms", uss, path);
            }
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">グローは赤から導出し、色の決定を二重に持たない</web-test>
        [Test]
        public void グローは赤と同じRGBで値の写しを持たない()
        {
            var red = ThemeTokenFile.ResolveColor("--tutorial-attention-red");
            var glow = ThemeTokenFile.ResolveColor("--tutorial-attention-glow");
            Assert.AreEqual((red.r, red.g, red.b), (glow.r, glow.g, glow.b));
            Assert.AreEqual(1f, glow.r, 0.001f);
            // 世界の地の上のグロー（LX-38）も赤と同じ RGB で、α だけが違う
            // The over-world glow (LX-38) shares the red RGB and differs only in alpha
            var glowOverWorld = ThemeTokenFile.ResolveColor("--tutorial-attention-glow-over-world");
            Assert.AreEqual((red.r, red.g, red.b), (glowOverWorld.r, glowOverWorld.g, glowOverWorld.b));
            foreach (var path in UssFiles.Concat(TutorialDesignSource.ListScreenFiles("", "*.cs")))
            {
                var text = TutorialDesignSource.ReadScreenFile(path);
                var withoutReference = text.Replace("var(--tutorial-attention-glow)", "").Replace("var(--tutorial-attention-glow-over-world)", "");
                StringAssert.DoesNotContain("--tutorial-attention-glow", withoutReference, path);
                Assert.IsFalse(Regex.IsMatch(text, @"0\.3981|0\.24"), path);
            }
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">脈動キーフレームは振幅を実数値で焼き、var()参照を残さない</web-test>
        [Test]
        public void 脈動の振幅は定数で持つ()
        {
            Assert.AreEqual(1.08f, TutorialAttentionPulse.StrongScale);
            Assert.AreEqual(1.03f, TutorialAttentionPulse.SubtleScale);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">脈動キーフレームはtokens.cssにだけ存在し、機能側へ複製されない</web-test>
        [Test]
        public void 振幅の数値はTutorialAttentionPulseだけにある()
        {
            foreach (var path in TutorialDesignSource.ListScreenFiles("", "*.cs"))
            {
                var hasLiteral = TutorialDesignSource.ReadScreenFile(path).Contains("1.08f") || TutorialDesignSource.ReadScreenFile(path).Contains("1.03f");
                Assert.AreEqual(path == "Pulse/TutorialAttentionPulse.cs", hasLiteral, path);
            }
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">利用側は素名のanimation直書きを復活させない（CSS Modulesがハッシュ化しキーフレームに届かなくなる）</web-test>
        [Test]
        public void USSは遷移も拡縮も書かず脈動は1経路だけ()
        {
            foreach (var path in UssFiles)
            {
                var uss = TutorialDesignSource.ReadScreenFile(path);
                StringAssert.DoesNotContain("transition", uss, path);
                Assert.IsFalse(Regex.IsMatch(uss, @"(?<![\w-])scale\s*:"), path);
            }
            var bindingUsers = TutorialDesignSource.ListScreenFiles("", "*.cs")
                .Where(path => TutorialDesignSource.ReadScreenFile(path).Contains("TutorialAttentionPulse.Start(")).ToArray();
            CollectionAssert.AreEqual(new[] { "Pulse/TutorialPulseBinding.cs" }, bindingUsers);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">利用側は振幅を持たず、tokensの名前トークンだけを参照する</web-test>
        [Test]
        public void 利用側は振幅の定数と周期の変数だけを参照する()
        {
            foreach (var path in PulseUserFiles)
            {
                var text = TutorialDesignSource.ReadScreenFile(path);
                foreach (Match call in Regex.Matches(text, @"\.Attach\(([^;]*)\);"))
                    StringAssert.IsMatch(@"TutorialAttentionPulse\.(Strong|Subtle)Scale", call.Groups[1].Value, path);
            }
            foreach (var path in new[] { "Overlay/TutorialOverlay.uss", "KeyControlHint/KeyControlHint.uss", "WorldPin/WorldPin.uss" })
                StringAssert.Contains("--tutorial-attention-pulse-period: var(--tutorial-pulse-duration);", TutorialDesignSource.ReadScreenFile(path), path);
        }
    }
}
