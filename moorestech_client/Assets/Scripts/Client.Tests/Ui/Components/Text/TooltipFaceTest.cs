using System.Linq;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Text
{
    /// <summary>
    /// 面・余白・最大幅の確認
    /// Checks face, padding and max width
    /// </summary>
    public class TooltipFaceTest : UiTestHost
    {
        // 行は行送りの <line-height> を前に付けて出す（TooltipFaceLineHeightTest）
        // Lines are shown with the pitch <line-height> tag prefixed (TooltipFaceLineHeightTest)
        public const string LineHeightTag = "<line-height=27.9px>";

        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void UseEnglish()
        {
            // 言語の保存値を退避して英語に固定する（ScreenTestHost と同じ手順）
            // Save the stored language and pin English (same steps as ScreenTestHost)
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [Test]
        public void 面と余白と文字はWebのツールチップのトークン()
        {
            var face = Mount(TooltipFaceLook.Hover);
            face.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Settings));
            simulate.FrameUpdate();

            AssertColor(Rgba(16, 20, 28, 0.94f), face.resolvedStyle.backgroundColor, "face");
            Assert.AreEqual(1f, face.resolvedStyle.borderTopWidth, 0.001f);
            AssertColor(Rgba(255, 255, 255, 0.22f), face.resolvedStyle.borderTopColor, "border");
            Assert.AreEqual(6f, face.resolvedStyle.paddingTop, 0.001f);
            Assert.AreEqual(10f, face.resolvedStyle.paddingLeft, 0.001f);
            var line = face.Q<Label>("tooltip-line");
            Assert.AreEqual(18f, line.resolvedStyle.fontSize, 0.01f);
            AssertColor(Color.white, line.resolvedStyle.color, "text");
            Assert.GreaterOrEqual(line.worldBound.height, 27.9f - 0.01f);
            Assert.AreEqual(LineHeightTag + "Settings", line.text);
        }

        [Test]
        public void カーソルの面は角丸4pxでホバーの面は角なし()
        {
            Assert.AreEqual(4f, Mount(TooltipFaceLook.Cursor).resolvedStyle.borderTopLeftRadius, 0.001f);
            Assert.AreEqual(0f, Mount(TooltipFaceLook.Hover).resolvedStyle.borderTopLeftRadius, 0.001f);
        }

        [Test]
        public void SetLinesは行を置き換え各行にtestidを付ける()
        {
            var face = Mount(TooltipFaceLook.Cursor);
            face.SetLines(new[] { new UiText(LocalizationKeys.Ui.PauseMenu.Settings), new UiText(LocalizationKeys.Ui.PauseMenu.Back) });
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { LineHeightTag + "Settings", LineHeightTag + "Back" }, face.Query<Label>("tooltip-line").ToList().Select(label => label.text));

            face.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            simulate.FrameUpdate();
            Assert.AreEqual(1, face.Query<Label>("tooltip-line").ToList().Count);
        }

        [Test]
        public void 長い文は最大幅320pxで折り返す()
        {
            var longText = new UiText(LocalizationKeys.Ui.Settings.GraphicsRecovered);
            var face = Mount(TooltipFaceLook.Hover);
            face.SetText(longText);
            // 折り返しの検出と最大幅への固定で 2 回レイアウトが回る
            // Wrap detection then pinning the max width takes two layout passes
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            Assert.AreEqual(320f, face.worldBound.width, 0.5f);
            Assert.Greater(face.Q<Label>("tooltip-line").worldBound.height, 27.9f * 1.5f);
        }

        [Test]
        public void 日本語の長文でも最大幅320pxに広がる()
        {
            Localize.TrySetChosenLanguage("japanese");
            var face = Mount(TooltipFaceLook.Hover);
            face.SetText(new UiText(LocalizationKeys.Ui.Settings.GraphicsRecovered));
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            Assert.IsTrue(face.ClassListContains("tooltip-face--wrapped"));
            Assert.AreEqual(320f, face.worldBound.width, 0.5f);
        }

        [Test]
        public void 短い文は広がらず長文から短文へ戻すと固定が外れる()
        {
            var face = Mount(TooltipFaceLook.Hover);
            face.SetText(new UiText(LocalizationKeys.Ui.Settings.GraphicsRecovered));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsTrue(face.ClassListContains("tooltip-face--wrapped"));

            face.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsFalse(face.ClassListContains("tooltip-face--wrapped"));
            Assert.Less(face.worldBound.width, 200f);
        }

        [Test]
        public void 行を0件にすると最大幅の固定が外れる()
        {
            var face = Mount(TooltipFaceLook.Hover);
            face.SetText(new UiText(LocalizationKeys.Ui.Settings.GraphicsRecovered));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsTrue(face.ClassListContains("tooltip-face--wrapped"));

            face.SetLines(new UiText[0]);
            simulate.FrameUpdate();
            Assert.IsFalse(face.ClassListContains("tooltip-face--wrapped"));
        }

        [Test]
        public void 最大幅が長さで解けないとLogErrorを出す()
        {
            var face = Mount(TooltipFaceLook.Hover);
            LogAssert.Expect(LogType.Error, new Regex(@"\[TooltipFace\] max-width is not a resolved length.*: tooltip"));
            face.style.maxWidth = StyleKeyword.None;
            face.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            simulate.FrameUpdate();
        }

        [Test]
        public void 言語を変えると行を解き直す()
        {
            var face = Mount(TooltipFaceLook.Hover);
            face.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            simulate.FrameUpdate();

            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            Assert.AreEqual(LineHeightTag + "戻る", face.Q<Label>("tooltip-line").text);
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

        private TooltipFace Mount(TooltipFaceLook look)
        {
            var face = new TooltipFace("tooltip", look);
            face.style.position = Position.Absolute;
            rootVisualElement.Add(face);
            simulate.FrameUpdate();
            return face;
        }
    }
}
