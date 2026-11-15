using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using System;
using System.Linq;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Screens.Settings;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Settings
{
    /// <summary>
    /// 画質の各選択肢の testid が送る項目と値に対応し、モデルが変わらない間の連続操作でも前の操作を巻き戻さないことを確かめる（L1。Web の GraphicsQualitySettings.test.ts）
    /// Verifies each graphics option testid maps to the item and value it sends, and back-to-back presses on a stale model never undo the previous one (L1; Web GraphicsQualitySettings.test.ts)
    /// </summary>
    public class GraphicsQualityItemCommandsTest : ScreenTestHost
    {
        private static readonly GraphicsQualitySettings Medium = GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium);
        private static readonly GraphicsQualitySettings Low = GraphicsQualityPresets.Get(GraphicsQualityPreset.Low);

        private RecordingSettingsCommands _commands;
        private ScrollView _scroll;
        private GraphicsQualitySettingsView _view;

        [SetUp]
        public void MountGraphicsSettings()
        {
            // 末尾の行が 720px の外へ出ても押せるよう、スクロールに入れて載せる
            // Mount inside a scroller so the last rows stay pressable beyond the 720px panel
            _commands = new RecordingSettingsCommands();
            _view = new GraphicsQualitySettingsView(FakeSettingsModel.Default(), _commands, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.height = 720f;
            _scroll.Add(_view);
            Mount(_scroll);
        }

        /// <web-test file="src/features/settings/GraphicsQualitySettings.test.ts">%sの操作値を送る</web-test>
        [TestCase("graphics-render-scale-75")]
        [TestCase("graphics-terrain-coarse")]
        [TestCase("graphics-ssao-off")]
        [TestCase("graphics-postProcessing-off")]
        [TestCase("graphics-effects-off")]
        [TestCase("graphics-mapObjects-off")]
        [TestCase("graphics-ocean-off")]
        public void 選択肢を押すとtestidに対応する項目だけを差し替えた値を送る(string optionTestid)
        {
            Press(optionTestid);
            var sent = _commands.Issued.Single();
            Assert.AreEqual(SettingsCommandKind.SetGraphicsQuality, sent.Kind);
            Assert.AreEqual(ExpectedAfterPress(Medium, optionTestid), sent.Graphics, $"{optionTestid} が別の項目か別の値を送った");
        }

        /// <web-test file="src/features/settings/GraphicsQualitySettings.test.ts">topicを固定したまま連続操作とpreset直後に%sを送る</web-test>
        [TestCase("graphics-draw-distance-1000")]
        [TestCase("graphics-render-scale-75")]
        [TestCase("graphics-terrain-coarse")]
        [TestCase("graphics-shadows-off")]
        [TestCase("graphics-ssao-off")]
        [TestCase("graphics-postProcessing-off")]
        [TestCase("graphics-effects-off")]
        [TestCase("graphics-mapObjects-off")]
        [TestCase("graphics-ocean-off")]
        public void モデルが変わらない間の連続操作とプリセット直後の操作は前の操作を巻き戻さず今の項目を送る(string optionTestid)
        {
            // 影オフの直後に、モデルを更新しないまま次の項目を押す
            // Press the next item right after shadows-off without updating the model
            Press("graphics-shadows-off");
            Press(optionTestid);
            var afterShadowsOff = Medium.WithShadowsEnabled(false);
            Assert.AreEqual(afterShadowsOff, _commands.Issued[0].Graphics);
            Assert.AreEqual(ExpectedAfterPress(afterShadowsOff, optionTestid), _commands.Issued[1].Graphics, "2 回目の操作が影オフを巻き戻した");

            // 低プリセットの直後に同じ項目を押すと、低の値に今の項目だけを差し替えて送る
            // Pressing the same item right after the low preset sends the low values with only that item replaced
            Press("graphics-preset-low");
            Press(optionTestid);
            Assert.AreEqual(SettingsCommandKind.ApplyGraphicsPreset, _commands.Issued[2].Kind);
            Assert.AreEqual(GraphicsQualityPreset.Low, _commands.Issued[2].Preset);
            Assert.AreEqual(ExpectedAfterPress(Low, optionTestid), _commands.Issued[3].Graphics, "プリセット直後の操作がプリセットを巻き戻した");
        }

        // 選択肢をスクロールで見える位置へ出してから押す
        // Scroll the option into view before pressing it
        private void Press(string optionTestid)
        {
            var option = Find<Button>(_view, optionTestid);
            _scroll.ScrollTo(option);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.Click(option);
        }

        // GraphicsQualityItemRows を通さず、testid から期待の値を独立に組む
        // Build the expected value from the testid independently of GraphicsQualityItemRows
        private static GraphicsQualitySettings ExpectedAfterPress(GraphicsQualitySettings settings, string optionTestid)
        {
            return optionTestid switch
            {
                "graphics-draw-distance-1000" => settings.WithDrawDistance(GraphicsDrawDistance.Meters1000),
                "graphics-render-scale-75" => settings.WithRenderScale(GraphicsRenderScale.Percent75),
                "graphics-terrain-coarse" => settings.WithTerrainDetail(GraphicsTerrainDetail.Coarse),
                "graphics-shadows-off" => settings.WithShadowsEnabled(false),
                "graphics-ssao-off" => settings.WithSsaoEnabled(false),
                "graphics-postProcessing-off" => settings.WithPostProcessingEnabled(false),
                "graphics-effects-off" => settings.WithEffectsEnabled(false),
                "graphics-mapObjects-off" => settings.WithMapObjectsVisible(false),
                "graphics-ocean-off" => settings.WithOceanVisible(false),
                _ => throw new ArgumentOutOfRangeException(nameof(optionTestid), optionTestid, null),
            };
        }
    }
}
