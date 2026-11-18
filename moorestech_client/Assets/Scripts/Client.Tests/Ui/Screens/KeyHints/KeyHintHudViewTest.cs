using Client.Game.InGame.UI.UIState.State;
using Client.Localization;
using Client.Ui.Components.Text;
using Client.Ui.Screens.KeyHints;
using Client.Ui.State.KeyHints;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.KeyHints
{
    /// <summary>
    /// 左下のキーヒントが kbd と文言の行を積み、空・blocking スキット中は描かず、辞書に無いキーを placeholder へ落とすことを確かめる（L1）
    /// Verifies the bottom-left key hints stack kbd-and-text rows, stay hidden when empty or during a blocking skit, and fall back to a placeholder for unknown keys (L1)
    /// </summary>
    public class KeyHintHudViewTest : ScreenTestHost
    {
        private static readonly KeyHint TabInventory = new(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory);
        private static readonly KeyHint GExit = new(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.ExitDeleteMode);

        /// <web-test file="src/features/keyHint/KeyHintHud.test.ts">ヒントをkbd+文言の順で描く</web-test>
        [Test]
        public void ヒントをkbdと文言の順で描く()
        {
            var view = Mount(new KeyHintHudView());
            view.Render(KeyHintHudViewModel.Create(new[] { TabInventory, GExit }, false, false));
            Assert.IsTrue(IsShown(view));

            // 行は受け取り順、各行は kbd が先で文言が後
            // Rows keep the received order; each row has the kbd first and the text after
            var rows = view.Query(className: "key-hint-row").ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.IsInstanceOf<KeyCap>(rows[0][0]);
            Assert.AreEqual(Localize.Get(TabInventory.KeyNameKey), ((KeyCap)rows[0][0]).text);
            Assert.AreEqual(Localize.Get(TabInventory.TextKey), ((Label)rows[0][1]).text);
            Assert.AreEqual(Localize.Get(GExit.KeyNameKey), ((KeyCap)rows[1][0]).text);
            Assert.IsTrue(view.ClassListContains("key-hint-text"));
            Assert.AreEqual("key-hints", view.name);
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/features/keyHint/KeyHintHud.test.ts">ヒントが空ならHUD自体を描かない</web-test>
        [Test]
        public void ヒントが空ならHUD自体を描かない()
        {
            var view = Mount(new KeyHintHudView());
            view.Render(KeyHintHudViewModel.Create(new[] { TabInventory }, false, false));
            Assert.IsTrue(IsShown(view));
            view.Render(KeyHintHudViewModel.Create(new KeyHint[0], false, false));
            Assert.IsFalse(IsShown(view));
        }

        /// <web-test file="src/features/keyHint/KeyHintHud.test.ts">blockingスキット中は描かない</web-test>
        [Test]
        public void blockingスキット中は描かない()
        {
            var view = Mount(new KeyHintHudView());
            view.Render(KeyHintHudViewModel.Create(new[] { TabInventory }, false, true));
            Assert.IsFalse(IsShown(view));
        }

        /// <web-test file="src/features/keyHint/KeyHintHud.test.ts">辞書に無いキーは声高なplaceholderへ落とす</web-test>
        [Test]
        public void 辞書に無いキーは声高なplaceholderへ落とす()
        {
            var missing = new KeyHint(new LocalizationKey("ui.keyHint.key.missingForTest"), new LocalizationKey("ui.keyHint.text.missingForTest"));
            var view = Mount(new KeyHintHudView());
            view.Render(KeyHintHudViewModel.Create(new[] { missing }, false, false));
            simulate.FrameUpdate();

            // C# の Localize は辞書に無いキーを [!key] で返す（Web の translateExternalKey の placeholder）
            // C# Localize returns [!key] for a missing key (the Web translateExternalKey placeholder)
            var row = view.Q(className: "key-hint-row");
            Assert.AreEqual("[!ui.keyHint.key.missingForTest]", ((KeyCap)row[0]).text);
            Assert.AreEqual("[!ui.keyHint.text.missingForTest]", ((Label)row[1]).text);
        }
    }
}
