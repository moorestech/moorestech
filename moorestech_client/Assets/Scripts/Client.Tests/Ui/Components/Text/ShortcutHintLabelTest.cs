// moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Text/ShortcutHintLabelTest.cs
using System.Linq;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Text;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Text
{
    /// <summary>
    /// ShortcutHintLabel が翻訳の語順を保って KeyCap を差し込み、言語変更で解き直すことを確かめる
    /// Verifies ShortcutHintLabel inserts KeyCap while keeping the translated order and re-resolves on language change
    /// </summary>
    public class ShortcutHintLabelTest : UiTestHost
    {
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void UseEnglish()
        {
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [TearDown]
        public void RestoreLanguage()
        {
            rootVisualElement.Clear();
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void prefixはKeyCapを先頭に置き文を後ろに置く()
        {
            var hint = Mount(ShortcutHintLayout.Prefix);
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Back), "Esc");
            simulate.FrameUpdate();

            var shown = Shown(hint);
            Assert.AreEqual(2, shown.Length);
            Assert.IsInstanceOf<KeyCap>(shown[0]);
            Assert.AreEqual("Esc", ((Label)shown[0]).text);
            Assert.AreEqual("Back", ((Label)shown[1]).text);
            Assert.Less(shown[0].worldBound.x, shown[1].worldBound.x);
        }

        [Test]
        public void inlineは印の位置にKeyCapを差し込む()
        {
            // 印は shortcut 以外の引数から届いてもよい。辞書の {itemId} に印を渡して位置を作る
            // The marker may arrive through any param; pass it to the dictionary's {itemId} to place it
            var hint = Mount(ShortcutHintLayout.Inline);
            var sentence = new UiText(LocalizationKeys.Ui.Common.ItemFallback, new[] { new LocalizationTextParam("itemId", ShortcutHintSegments.Marker) });
            hint.SetHint(sentence, "E");
            simulate.FrameUpdate();

            var shown = Shown(hint);
            Assert.AreEqual(2, shown.Length);
            Assert.AreEqual("Item ", ((Label)shown[0]).text);
            Assert.IsInstanceOf<KeyCap>(shown[1]);
        }

        [Test]
        public void inlineで印が無ければLogErrorを出しprefixで出す()
        {
            var hint = Mount(ShortcutHintLayout.Inline);
            LogAssert.Expect(LogType.Error, new Regex(@"\[ShortcutHintLabel\] hint: .*no shortcut marker"));
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Back), "Esc");
            simulate.FrameUpdate();

            Assert.IsInstanceOf<KeyCap>(Shown(hint)[0]);
        }

        [Test]
        public void 載せた後にSetHintすればLogErrorを出さない()
        {
            var hint = Mount(ShortcutHintLayout.Prefix);
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Back), "Esc");
            simulate.FrameUpdate();

            Assert.AreEqual("Back", ((Label)Shown(hint)[1]).text);
        }

        [Test]
        public void SetHintを呼ばずに載せるとLogErrorを出す()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[LocalizedTextBinding\] attached without a text: hint"));
            Mount(ShortcutHintLayout.Prefix);
            simulate.FrameUpdate();
        }

        [Test]
        public void SetHintを2回呼ぶと2回目の内容だけが残る()
        {
            var hint = Mount(ShortcutHintLayout.Prefix);
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Back), "Esc");
            simulate.FrameUpdate();
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Settings), "F1");
            simulate.FrameUpdate();

            var shown = Shown(hint);
            Assert.AreEqual(2, shown.Length);
            Assert.AreEqual("F1", ((Label)shown[0]).text);
            Assert.AreEqual("Settings", ((Label)shown[1]).text);
        }

        [Test]
        public void 言語を変えると文を解き直す()
        {
            var hint = Mount(ShortcutHintLayout.Prefix);
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Back), "Esc");
            simulate.FrameUpdate();

            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            Assert.AreEqual("戻る", ((Label)Shown(hint)[1]).text);
        }

        [Test]
        public void KeyCapは枠なしで置く側の字の大きさを継ぎ色は高コントラスト()
        {
            var row = new VisualElement();
            row.AddToClassList(UiTypographyClass.KeyHintText);
            row.style.color = Color.red;
            rootVisualElement.Add(row);
            var hint = new ShortcutHintLabel("hint", ShortcutHintLayout.Prefix);
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Back), "Esc");
            row.Add(hint);
            simulate.FrameUpdate();

            var keyCap = hint.Q<KeyCap>();
            Assert.AreEqual(25f, keyCap.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(0f, keyCap.resolvedStyle.borderTopWidth, 0.001f);
            Assert.AreEqual(0f, keyCap.resolvedStyle.paddingLeft, 0.001f);
            Assert.AreEqual(Color.white, keyCap.resolvedStyle.color);
            Assert.AreEqual(Color.red, ((Label)Shown(hint)[1]).resolvedStyle.color);
        }

        [Test]
        public void kbdと文の間は既定で0()
        {
            var hint = Mount(ShortcutHintLayout.Prefix);
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Back), "Esc");
            simulate.FrameUpdate();

            var shown = Shown(hint);
            Assert.AreEqual(0f, shown[1].worldBound.x - shown[0].worldBound.xMax, 0.01f);
        }

        private ShortcutHintLabel Mount(ShortcutHintLayout layout)
        {
            var hint = new ShortcutHintLabel("hint", layout);
            rootVisualElement.Add(hint);
            return hint;
        }

        private static VisualElement[] Shown(ShortcutHintLabel hint)
        {
            return hint.Children().Where(child => child.resolvedStyle.display == DisplayStyle.Flex).ToArray();
        }
    }
}
