using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// BlockSlot の面・絵・ツールチップを確かめる（L1）
    /// Verifies BlockSlot's face, picture and tooltip (L1)
    /// </summary>
    public class BlockSlotTest : UiTestHost
    {
        private BlockSlot _slot;
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void AddSlot()
        {
            // 言語の保存値を退避し英語に固定
            // Save the stored language and pin English
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            _slot = new BlockSlot("research-block-12");
            rootVisualElement.Add(_slot);
            simulate.FrameUpdate();
        }

        [TearDown]
        public void NoUnexpectedLogs()
        {
            // 言語の復元を先に済ませ、検証が失敗しても英語固定を残さない
            // Restore the language first so a failed assertion never leaves English pinned
            rootVisualElement.Clear();
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/shared/ui/BlockSlot/index.test.ts">従来の暗背景契約を保ちつつブロックアイコンを直下に描画する</web-test>
        [Test]
        public void 暗い面の枠の中にブロックの絵を描く()
        {
            _slot.SetBlock(SlotTestIcons.WithTexture("12", "Assembler"), UiText.FromVerbatim("Assembler"));
            simulate.FrameUpdate();

            var frame = _slot.Q<SlotFrame>("research-block-12");
            Assert.IsFalse(frame.ClassListContains("slot-frame--filled"));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--slot-frame-face"), frame.Q(className: "slot-frame__face").resolvedStyle.backgroundColor, "face");
            Assert.AreSame(SlotTestIcons.SharedTexture, frame.Q(className: "game-icon__image").resolvedStyle.backgroundImage.texture);
        }

        /// <web-test file="src/shared/ui/BlockSlot/index.test.ts">従来と同じアイコンCSSを適用する</web-test>
        [Test]
        public void アイテムと同じ中身の余白を当てる()
        {
            _slot.SetBlock(SlotTestIcons.WithTexture("12", "Assembler"), UiText.FromVerbatim("Assembler"));
            simulate.FrameUpdate();

            var icon = _slot.Q(className: "slot-content__icon");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--icon-pad"), icon.resolvedStyle.paddingLeft, 0.01f);
        }

        [Test]
        public void 名前を枠のツールチップの印に付け絵が欠けたらIDを出す()
        {
            var name = UiText.FromVerbatim("Assembler");
            _slot.SetBlock(SlotTestIcons.Missing("12", "Assembler"), name);
            simulate.FrameUpdate();

            var frame = _slot.Q<SlotFrame>("research-block-12");
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(frame.contentContainer, out var marked, out var text));
            Assert.AreSame(frame, marked);
            Assert.AreSame(name, text);
            Assert.AreEqual("#12", _slot.Q<Label>(className: "game-icon__fallback").text);
        }
    }
}
