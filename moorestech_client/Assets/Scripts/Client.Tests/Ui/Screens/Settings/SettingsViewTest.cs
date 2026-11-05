using System.Linq;
using Client.Game.InGame.GraphicsQuality;
using Client.Input;
using Client.Ui.Screens.Settings;
using Client.Ui.State.Commands;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Settings
{
    /// <summary>
    /// 言語・走り・画質の選択がコマンドになり、現在値が選択表示になることを確かめる（L1。Web の features/settings の vitest）
    /// Verifies language, sprint and graphics choices become commands and current values show as selected (L1; Web features/settings vitest)
    /// </summary>
    public class SettingsViewTest : ScreenTestHost
    {
        private RecordingSettingsCommands _commands;

        [SetUp]
        public void CreateCommands()
        {
            _commands = new RecordingSettingsCommands();
        }

        [Test]
        public void 言語は配信順のoptionsを縦に並べ現在値を選択する()
        {
            var view = Mount(new LanguageSelectView(FakeSettingsModel.Default(), _commands, Toasts, Catalog));
            var expected = LanguageCatalog.Languages.Select(language => $"language-select-option-{language.Code}").ToArray();
            CollectionAssert.AreEqual(expected, Find<VisualElement>(view, "language-select").Query<Button>().ToList().Select(button => button.name));
            Assert.IsTrue(Find<VisualElement>(view, "language-select").ClassListContains("mode-switch--vertical"));
            Assert.IsTrue(IsSelected(view, "language-select-option-english"));
            Assert.AreEqual(1, SelectedCount(Find<VisualElement>(view, "language-select")));
        }

        [Test]
        public void 言語を選ぶとSetLanguageを出し拒否ならエラーのトーストを出す()
        {
            var view = Mount(new LanguageSelectView(FakeSettingsModel.Default(), _commands, Toasts, Catalog));
            simulate.Click(Find<Button>(view, "language-select-option-japanese"));
            Assert.AreEqual("japanese", _commands.Issued.Single().LanguageCode);
            Assert.IsEmpty(ShownToasts);

            _commands.SetLanguageResult(new UiCommandRejected(UiCommandRejectReason.UnknownLanguage, ""));
            simulate.Click(Find<Button>(view, "language-select-option-japanese"));
            Assert.AreEqual("localization.setLocale failed: unknown_locale", ShownToasts.Single().Text);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
        }

        [Test]
        public void 走りはtopicの現在値を選択しホールド選択を設定actionへ送る()
        {
            var view = Mount(new SprintModeSelectView(FakeSettingsModel.Default(), _commands, Catalog));
            Assert.IsTrue(IsSelected(view, "sprint-mode-option-toggle"));
            simulate.Click(Find<Button>(view, "sprint-mode-option-hold"));
            Assert.AreEqual(SprintInputMode.Hold, _commands.Issued.Single().SprintMode);

            // toggle から hold への更新を表示する
            // Shows the update from toggle to hold
            view.Apply(Settings(GraphicsQualityPresetChoice.Medium, false, SprintInputMode.Hold, false));
            Assert.IsTrue(IsSelected(view, "sprint-mode-option-hold"));
            Assert.IsFalse(IsSelected(view, "sprint-mode-option-toggle"));
        }

        [Test]
        public void 走りの復旧記録を表示し解除のtopicで消す()
        {
            var view = Mount(new SprintModeSelectView(Settings(GraphicsQualityPresetChoice.Medium, false, SprintInputMode.Toggle, true), _commands, Catalog));
            Assert.IsTrue(IsShown(Find<Label>(view, "sprint-mode-recovered")));
            view.Apply(FakeSettingsModel.Default());
            Assert.IsFalse(IsShown(Find<Label>(view, "sprint-mode-recovered")));
        }

        [Test]
        public void プリセットと一致するときはその値を選択しカスタム表示を出さない()
        {
            var view = Mount(new GraphicsQualitySettingsView(FakeSettingsModel.Default(), _commands, Catalog));
            Assert.IsTrue(IsSelected(view, "graphics-preset-medium"));
            Assert.AreEqual(1, SelectedCount(Find<VisualElement>(view, "graphics-preset")));
            Assert.IsFalse(IsShown(Find<Label>(view, "graphics-preset-custom")));
            Assert.IsFalse(IsShown(Find<Label>(view, "graphics-recovered")));
        }

        [Test]
        public void カスタムのときは無選択にしてカスタム表示を1件出す()
        {
            var view = Mount(new GraphicsQualitySettingsView(Settings(GraphicsQualityPresetChoice.Custom, false, SprintInputMode.Toggle, false), _commands, Catalog));
            Assert.AreEqual(0, SelectedCount(Find<VisualElement>(view, "graphics-preset")));
            Assert.IsTrue(IsShown(Find<Label>(view, "graphics-preset-custom")));
            Assert.AreEqual(1, view.Query<Label>("graphics-preset-custom").ToList().Count);
        }

        [Test]
        public void 項目変更は今の設定値の1項目だけを差し替えプリセット選択はプリセット名を送る()
        {
            var current = GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium);
            var view = Mount(new GraphicsQualitySettingsView(FakeSettingsModel.Default(), _commands, Catalog));
            Assert.IsTrue(IsSelected(view, current.ShadowsEnabled ? "graphics-shadows-on" : "graphics-shadows-off"));

            simulate.Click(Find<Button>(view, current.ShadowsEnabled ? "graphics-shadows-off" : "graphics-shadows-on"));
            var sent = _commands.Issued.Single().Graphics;
            Assert.AreEqual(!current.ShadowsEnabled, sent.ShadowsEnabled);
            Assert.AreEqual(current.DrawDistance, sent.DrawDistance);
            Assert.AreEqual(current.OceanVisible, sent.OceanVisible);

            simulate.Click(Find<Button>(view, "graphics-preset-high"));
            Assert.AreEqual(SettingsCommandKind.ApplyGraphicsPreset, _commands.Issued.Last().Kind);
            Assert.AreEqual(GraphicsQualityPreset.High, _commands.Issued.Last().Preset);
        }

        [Test]
        public void 数値項目と地形は対応する値で送る()
        {
            var view = Mount(new GraphicsQualitySettingsView(FakeSettingsModel.Default(), _commands, Catalog));
            Assert.AreEqual("5000m", Find<Button>(view, "graphics-draw-distance-5000").text);
            simulate.Click(Find<Button>(view, "graphics-draw-distance-5000"));
            simulate.Click(Find<Button>(view, "graphics-render-scale-50"));
            simulate.Click(Find<Button>(view, "graphics-terrain-coarse"));
            Assert.AreEqual(GraphicsDrawDistance.Meters5000, _commands.Issued[0].Graphics.DrawDistance);
            Assert.AreEqual(GraphicsRenderScale.Percent50, _commands.Issued[1].Graphics.RenderScale);
            Assert.AreEqual(GraphicsTerrainDetail.Coarse, _commands.Issued[2].Graphics.TerrainDetail);
        }

        [Test]
        public void 画質の復旧表示を解除通知まで保持し同値選択も送る()
        {
            var view = Mount(new GraphicsQualitySettingsView(Settings(GraphicsQualityPresetChoice.Medium, true, SprintInputMode.Toggle, false), _commands, Catalog));
            Assert.IsTrue(IsShown(Find<Label>(view, "graphics-recovered")));

            // 選択中の中を押しても送る（復旧の確認になる）
            // Pressing the already-selected Medium still sends (it confirms the recovery)
            simulate.Click(Find<Button>(view, "graphics-preset-medium"));
            Assert.AreEqual(GraphicsQualityPreset.Medium, _commands.Issued.Single().Preset);
            Assert.IsTrue(IsShown(Find<Label>(view, "graphics-recovered")));

            view.Apply(FakeSettingsModel.Default());
            Assert.IsFalse(IsShown(Find<Label>(view, "graphics-recovered")));
        }

        [Test]
        public void 画質の項目の並びとtestidはWebと同じ()
        {
            var view = Mount(new GraphicsQualitySettingsView(FakeSettingsModel.Default(), _commands, Catalog));
            var switches = view.Query<VisualElement>(className: "mode-switch").ToList().Select(element => element.name);
            CollectionAssert.AreEqual(new[]
            {
                "graphics-preset", "graphics-draw-distance", "graphics-shadows", "graphics-render-scale", "graphics-ssao",
                "graphics-postProcessing", "graphics-effects", "graphics-mapObjects", "graphics-terrain", "graphics-ocean",
            }, switches);
            Assert.AreEqual("Shadows", view.Query<Label>(className: "settings-section__caption").ToList()[2].text);
        }

        private static SettingsViewModel Settings(GraphicsQualityPresetChoice preset, bool graphicsRecovered, SprintInputMode sprintMode, bool sprintRecovered)
        {
            var graphics = preset == GraphicsQualityPresetChoice.Custom ? GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium).WithOceanVisible(false) : GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium);
            return new SettingsViewModel(graphics, preset, graphicsRecovered, sprintMode, sprintRecovered, "english");
        }
    }
}
