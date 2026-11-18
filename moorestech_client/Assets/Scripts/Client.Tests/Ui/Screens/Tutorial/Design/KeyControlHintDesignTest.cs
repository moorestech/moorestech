using System.Linq;
using Client.Tests.Ui.Components;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.Tutorial.Design
{
    public class KeyControlHintDesignTest
    {
        private static string Uss => TutorialDesignSource.ReadScreenFile("KeyControlHint/KeyControlHint.uss");

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">文字色は原色赤トークンを指す</web-test>
        [Test]
        public void 文字色は原色赤トークンを指す()
        {
            var item = TutorialDesignSource.Rule(Uss, ".key-control-hint .key-control-hint__item");
            StringAssert.Contains("color: var(--tutorial-key-hint-color)", item);
            Assert.AreEqual("#ff0000", ThemeTokenFile.Resolve("--tutorial-key-hint-color"));
            var root = TutorialDesignSource.Rule(Uss, ".key-control-hint");
            StringAssert.Contains("--key-cap-color: var(--tutorial-key-hint-color)", root);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">1.08の拡縮ループを共有キーフレームで持つ</web-test>
        [Test]
        public void 拡縮は共有の脈動の強い振幅で流す()
        {
            var view = TutorialDesignSource.ReadScreenFile("KeyControlHint/KeyControlHintView.cs");
            StringAssert.Contains("TutorialAttentionPulse.StrongScale", view);
            StringAssert.Contains("new TutorialPulseBinding(", view);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">機能側CSSに色と秒数を直書きしない</web-test>
        [Test]
        public void ヒントのUSSに色と秒の直書きが無い()
        {
            Assert.IsFalse(TutorialDesignSource.ColorLiteral.IsMatch(Uss), Uss);
            Assert.IsFalse(TutorialDesignSource.MillisecondLiteral.IsMatch(Uss), Uss);
        }

        /// <web-test file="src/features/tutorial/tutorialAttentionDesign.test.ts">共有様式keyHintTextの文字色は白のままで、インベントリ左下・研究左下を巻き込まない</web-test>
        [Test]
        public void 共有のkeyHintTextは白のままで画面は共有の色を受け直さない()
        {
            var typography = System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "Assets/Scripts/Client.Ui/Foundation/Theme/Typography.uss"));
            var rule = TutorialDesignSource.Rule(typography, ".key-hint-text");
            StringAssert.Contains("color: var(--text-high-contrast)", rule);
            Assert.AreEqual("#fff", ThemeTokenFile.Resolve("--text-high-contrast"));
            foreach (var path in TutorialDesignSource.ListScreenFiles("", "*.uss"))
                StringAssert.DoesNotContain("--text-high-contrast:", TutorialDesignSource.ReadScreenFile(path), path);
        }
    }
}
