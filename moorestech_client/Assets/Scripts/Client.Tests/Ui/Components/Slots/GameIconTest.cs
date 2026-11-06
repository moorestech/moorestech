using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// GameIcon の絵と 3 種の代わりを本番テーマのパネルで確かめる（L1）
    /// Verifies the GameIcon picture and its three fallbacks on a production-theme panel (L1)
    /// </summary>
    public class GameIconTest : UiTestHost
    {
        private GameIcon _icon;
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void AddIcon()
        {
            // 言語の保存値を退避して英語に固定する（TooltipFaceTest と同じ手順）
            // Save the stored language and pin English (same steps as TooltipFaceTest)
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            _icon = new GameIcon();
            _icon.style.width = 48;
            _icon.style.height = 48;
            rootVisualElement.Add(_icon);
            simulate.FrameUpdate();
        }

        [TearDown]
        public void NoUnexpectedLogs()
        {
            // 言語の復元を先に済ませ、検証が失敗しても次のテストへ英語固定を残さない
            // Restore the language first so a failed assertion never leaves English pinned for the next test
            rootVisualElement.Clear();
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void テクスチャがあれば絵だけを出し代わりの文字を作らない()
        {
            _icon.SetIcon(SlotTestIcons.WithTexture("12", "Iron"), GameIconFallback.IdText);
            simulate.FrameUpdate();

            var image = _icon.Q(className: "game-icon__image");
            Assert.AreEqual(DisplayStyle.Flex, image.resolvedStyle.display);
            Assert.AreSame(SlotTestIcons.SharedTexture, image.resolvedStyle.backgroundImage.texture);
            Assert.IsNull(_icon.Q(className: "game-icon__fallback"));
            Assert.AreEqual(PickingMode.Ignore, _icon.pickingMode);
        }

        [Test]
        public void テクスチャが無くIdTextならIDに井桁を付けて出す()
        {
            _icon.SetIcon(SlotTestIcons.Missing("12", "Iron"), GameIconFallback.IdText);
            simulate.FrameUpdate();

            var fallback = _icon.Q<Label>(className: "game-icon__fallback");
            Assert.AreEqual("#12", fallback.text);
            Assert.AreEqual(DisplayStyle.Flex, fallback.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, _icon.Q(className: "game-icon__image").resolvedStyle.display);
        }

        [Test]
        public void テクスチャが無くLabelなら名前を出す()
        {
            _icon.SetIcon(SlotTestIcons.Missing("7", "Water"), GameIconFallback.Label);
            simulate.FrameUpdate();

            Assert.AreEqual("Water", _icon.Q<Label>(className: "game-icon__fallback").text);
        }

        [Test]
        public void テクスチャが無くNoneなら何も出さない()
        {
            _icon.SetIcon(SlotTestIcons.Missing("7", "Water"), GameIconFallback.None);
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, _icon.Q(className: "game-icon__image").resolvedStyle.display);
            Assert.IsNull(_icon.Q(className: "game-icon__fallback"));
        }

        [Test]
        public void 代わりの文字は10pxで暗い灰色の1行省略()
        {
            _icon.SetIcon(SlotTestIcons.Missing("123456789012", "Iron"), GameIconFallback.IdText);
            simulate.FrameUpdate();

            var fallback = _icon.Q<Label>(className: "game-icon__fallback");
            Assert.AreEqual(10f, fallback.resolvedStyle.fontSize, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-dark-2"), fallback.resolvedStyle.color, "fallback");
            Assert.AreEqual(WhiteSpace.NoWrap, fallback.resolvedStyle.whiteSpace);
            Assert.AreEqual(TextOverflow.Ellipsis, fallback.resolvedStyle.textOverflow);
        }

        [Test]
        public void 絵が来たら代わりの文字を隠し消すと両方隠れる()
        {
            _icon.SetIcon(SlotTestIcons.Missing("12", "Iron"), GameIconFallback.IdText);
            _icon.SetIcon(SlotTestIcons.WithTexture("12", "Iron"), GameIconFallback.IdText);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, _icon.Q(className: "game-icon__fallback").resolvedStyle.display);

            _icon.ClearIcon();
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, _icon.Q(className: "game-icon__image").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, _icon.Q(className: "game-icon__fallback").resolvedStyle.display);
        }
    }
}
