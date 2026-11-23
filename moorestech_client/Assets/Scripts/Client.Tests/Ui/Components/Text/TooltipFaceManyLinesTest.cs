using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Text
{
    /// <summary>
    /// 行が多い面の折り返し判定。行ごとに超過を数える方式でも、全行を測る方式と同じ結果になることを確かめる
    /// Wrap judgement on a face with many lines; counting over-width lines per line must match measuring every line
    /// </summary>
    public class TooltipFaceManyLinesTest : UiTestHost
    {
        private const int LineCount = 200;

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

        [Test]
        public void 短い行だけなら何行あっても最大幅へ固定しない()
        {
            var face = Mount();
            face.SetLines(Enumerable.Repeat(new UiText(LocalizationKeys.Ui.PauseMenu.Back), LineCount).ToArray());
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            Assert.AreEqual(LineCount, face.Query<Label>("tooltip-line").ToList().Count);
            Assert.IsFalse(face.ClassListContains("tooltip-face--wrapped"));
        }

        [Test]
        public void 長い行が1つ混ざると最大幅へ固定し外すと戻る()
        {
            var face = Mount();
            var lines = Enumerable.Repeat(new UiText(LocalizationKeys.Ui.PauseMenu.Back), LineCount).ToArray();
            lines[LineCount / 2] = new UiText(LocalizationKeys.Ui.Settings.GraphicsRecovered);
            face.SetLines(lines);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsTrue(face.ClassListContains("tooltip-face--wrapped"));
            Assert.AreEqual(320f, face.worldBound.width, 0.5f);

            // 長い行を外すと超過の数が 0 に戻る
            // Dropping the long line brings the over-width count back to zero
            face.SetLines(Enumerable.Repeat(new UiText(LocalizationKeys.Ui.PauseMenu.Back), LineCount).ToArray());
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsFalse(face.ClassListContains("tooltip-face--wrapped"));
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

        private TooltipFace Mount()
        {
            var face = new TooltipFace("tooltip", TooltipFaceLook.Hover);
            face.style.position = Position.Absolute;
            rootVisualElement.Add(face);
            simulate.FrameUpdate();
            return face;
        }
    }
}
