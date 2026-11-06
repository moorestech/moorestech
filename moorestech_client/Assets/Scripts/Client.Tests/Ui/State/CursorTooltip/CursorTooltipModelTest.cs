using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Client.Ui.State.CursorTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの状態を行の列にして配り、同じ行の再表示は流さない（L1）
    /// Publishes the cursor tooltip state as a list of lines and does not republish identical lines (L1)
    /// </summary>
    public class CursorTooltipModelTest
    {
        private bool _hadSavedLanguage;
        private string _savedLanguage;

        [SetUp]
        public void PinEnglish()
        {
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [TearDown]
        public void RestoreLanguage()
        {
            if (_hadSavedLanguage) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void 表示した行を順に配り隠すと空になる()
        {
            var state = new MouseCursorTooltipState();
            var model = new CursorTooltipModel(state);
            model.Initialize();
            var owner = new TooltipOwner();

            state.Show(owner, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain), new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceTooFar) });
            Assert.IsTrue(model.Current.Value.Visible);
            Assert.AreEqual(new[] { "Blocked by terrain", "Too far away" }, new[] { model.Current.Value.Lines[0].Resolve(), model.Current.Value.Lines[1].Resolve() });

            state.Hide(owner);
            Assert.IsFalse(model.Current.Value.Visible);
        }

        [Test]
        public void 同じ行の再表示は再配信しない()
        {
            var state = new MouseCursorTooltipState();
            var model = new CursorTooltipModel(state);
            model.Initialize();
            var owner = new TooltipOwner();
            var published = 0;
            model.Current.Subscribe(_ => published++);

            state.Show(owner, LocalizationKeys.Ui.Tooltip.WorldTarget);
            state.Show(owner, LocalizationKeys.Ui.Tooltip.WorldTarget);
            Assert.AreEqual(2, published, "購読時の初期値 1 回と表示 1 回だけ");
        }
    }
}
