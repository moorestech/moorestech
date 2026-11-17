using Client.Game.InGame.GraphicsQuality;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    /// <summary>
    /// 画質の項目の変更は最新の設定に 1 項目を重ね、続けて変えてもプリセットの直後でも他の値が残ることを確かめる（L0）
    /// Verifies each graphics change layers one item on the latest settings, keeping other values across rapid changes and right after a preset (L0)
    /// </summary>
    public class SettingsModelGraphicsQualityTest : GraphicsQualityTestScope
    {
        [Test]
        public void 項目を続けて変えると全部残りプリセットの直後に1項目を戻しても他はプリセットのまま()
        {
            var model = new SettingsModel();
            model.Initialize();
            var commands = new SettingsCommands();
            var medium = model.Current.Value.Graphics;

            // 画面の行と同じく、毎回いまの設定に 1 項目だけ重ねて 9 項目を変える
            // Like the screen rows, change nine items by layering one item on the current settings each time
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithDrawDistance(GraphicsDrawDistance.Meters1000));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithShadowsEnabled(false));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithRenderScale(GraphicsRenderScale.Percent75));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithSsaoEnabled(false));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithPostProcessingEnabled(false));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithEffectsEnabled(false));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithMapObjectsVisible(false));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithTerrainDetail(GraphicsTerrainDetail.Coarse));
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithOceanVisible(false));

            var expected = new GraphicsQualitySettings(GraphicsDrawDistance.Meters1000, false, GraphicsRenderScale.Percent75, false, false, false, false, GraphicsTerrainDetail.Coarse, false);
            Assert.AreEqual(expected, model.Current.Value.Graphics);
            Assert.AreEqual(GraphicsQualityPresetChoice.Custom, model.Current.Value.Preset);
            Assert.AreEqual(GraphicsQualityPreference.Serialize(model.Current.Value.Graphics), PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));

            // 低のプリセットの直後に解像度だけ中の値へ戻しても、他は低のまま
            // Right after the low preset, restoring only the render scale to medium keeps the rest at low
            commands.ApplyGraphicsPreset(GraphicsQualityPreset.Low);
            commands.SetGraphicsQuality(model.Current.Value.Graphics.WithRenderScale(medium.RenderScale));

            var low = GraphicsQualityPresets.Get(GraphicsQualityPreset.Low);
            Assert.AreNotEqual(low.RenderScale, medium.RenderScale, "テスト前提: 低と中で解像度が違う");
            Assert.AreEqual(low.WithRenderScale(medium.RenderScale), model.Current.Value.Graphics);
            Assert.AreEqual(GraphicsQualityPreference.Serialize(model.Current.Value.Graphics), PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));
        }
    }
}
