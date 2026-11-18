using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// PlacementTargetFace の絵か名前の一方を確かめる（L1）
    /// Verifies PlacementTargetFace shows the picture or the name (L1)
    /// </summary>
    public class PlacementTargetFaceTest : UiTestHost
    {
        private PlacementTargetFace _face;
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void AddFace()
        {
            // 言語の保存値を退避し英語に固定
            // Save the stored language and pin English
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            var frame = new SlotFrame("slot");
            _face = new PlacementTargetFace();
            frame.Add(_face);
            rootVisualElement.Add(frame);
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

        [Test]
        public void 絵があれば絵をicon_padの余白で出し名前を作らない()
        {
            _face.SetIconTarget(SlotTestIcons.WithTexture("3", "Chest"), UiText.FromVerbatim("Chest"));
            simulate.FrameUpdate();

            var icon = _face.Q(className: "placement-target-face__icon");
            Assert.AreEqual(DisplayStyle.Flex, icon.resolvedStyle.display);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--icon-pad"), icon.resolvedStyle.paddingTop, 0.01f);
            Assert.IsNull(_face.Q(className: "placement-target-face__label"));
        }

        [Test]
        public void 絵の無い対象は名前を10pxで中央寄せに出す()
        {
            _face.SetNameTarget(UiText.FromVerbatim("Blueprint Copy"));
            simulate.FrameUpdate();

            var label = _face.Q<Label>(className: "placement-target-face__label");
            Assert.AreEqual("Blueprint Copy", label.text);
            Assert.AreEqual(10f, label.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(TextAnchor.UpperCenter, label.resolvedStyle.unityTextAlign);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-default"), label.resolvedStyle.color, "label");
            Assert.AreEqual(DisplayStyle.None, _face.Q(className: "placement-target-face__icon").resolvedStyle.display);
        }

        [Test]
        public void テクスチャが欠けた対象も名前で出す()
        {
            _face.SetIconTarget(SlotTestIcons.Missing("3", "Chest"), UiText.FromVerbatim("Chest"));
            simulate.FrameUpdate();

            Assert.AreEqual("Chest", _face.Q<Label>(className: "placement-target-face__label").text);
        }

        [Test]
        public void 名前から絵へ切り替えると名前を隠す()
        {
            _face.SetNameTarget(UiText.FromVerbatim("Blueprint Copy"));
            _face.SetIconTarget(SlotTestIcons.WithTexture("3", "Chest"), UiText.FromVerbatim("Chest"));
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, _face.Q(className: "placement-target-face__label").resolvedStyle.display);
        }
    }
}
