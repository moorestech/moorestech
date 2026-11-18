using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Client.Tests.Ui.Components.Text;
using Client.Ui.Screens.CursorTooltip;
using Client.Ui.State.CursorTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ツールチップの文言・行・面（L1）
    /// Tooltip text, lines and face (L1)
    /// </summary>
    public class CursorTooltipViewTest : ScreenTestHost
    {
        private CursorTooltipView _view;

        [SetUp]
        public void MountTooltip()
        {
            _view = Mount(NewViewWithFakeScreen());
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">interpolates textParams into the localized template</web-test>
        [Test]
        public void 引数を辞書の文へ差し込む()
        {
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { new LocalizationTextParam("count", "3") })));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wire x3", Find<Label>(_view, "cursor-tooltip-line").text);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">preserves a missing count and warns once across renders</web-test>
        [Test]
        public void 欠けた引数は枠のまま残しエラーを出す()
        {
            // UITK の補間は解くたびに出す（Web は 1 回だけ）。差は判断記録 D-13
            // UITK interpolation logs on every resolve (Web logs once); the difference is decision D-13
            // 引数 0 件でも補間を通り、欠けた count は補間器がエラーログで知らせる
            // Even with zero params interpolation runs, and the interpolator reports the missing count as an error
            LogAssert.Expect(LogType.Error, new Regex(@"no value for placeholder \{count\}"));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost)));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wire x{count}", Find<Label>(_view, "cursor-tooltip-line").text);

            // count=0 は欠けではなく 0 として差し込む（Web の後半）
            // count=0 is not missing and is interpolated as 0 (second half of the Web test)
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { new LocalizationTextParam("count", "0") })));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wire x0", Find<Label>(_view, "cursor-tooltip-line").text);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">shows a loud marker for an unknown localized key</web-test>
        [Test]
        public void 辞書に無いキーは大声の印で出す()
        {
            // LocalizationKey は public ctor で、GameObjectTooltipTarget が Inspector の文字列から作る。綴り違いのキーは [!key] で画面に出る（Web の warn 1 回は写さず、印そのものが気づく経路。D-27）
            // LocalizationKey has a public ctor and GameObjectTooltipTarget builds one from an Inspector string; a misspelt key shows as [!key] (Web's single warn is not ported; the marker itself is the notice, D-27)
            var unknown = new LocalizationKey("ui.tooltip.unknown");
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(unknown)));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "[!ui.tooltip.unknown]", Find<Label>(_view, "cursor-tooltip-line").text);
            _view.Render(CursorTooltipViewModel.Hidden);
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(unknown)));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "[!ui.tooltip.unknown]", Find<Label>(_view, "cursor-tooltip-line").text);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">renders every line in order</web-test>
        /// <web-test file="e2e/tests/system/commonHud.spec.ts">ツールチップは複数行を順序どおり縦積みで表示する</web-test>
        [Test]
        public void 複数行を順に縦へ積む()
        {
            Localize.TrySetChosenLanguage("japanese");
            // 2 行目は名前付きの引数 3 つの差し込み（Web と同じ placeMaterialShortage）
            // The second line interpolates three named params (placeMaterialShortage, as on Web)
            var shortage = new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage, new[] { new LocalizationTextParam("itemName", "Iron Plate"), new LocalizationTextParam("ownedCount", "3"), new LocalizationTextParam("requiredCount", "10") });
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain), shortage));
            simulate.FrameUpdate();
            var lines = _view.Query<Label>("cursor-tooltip-line").ToList();
            Assert.AreEqual(new[] { TooltipFaceTest.LineHeightTag + "地形に埋まっています", TooltipFaceTest.LineHeightTag + "アイテム不足： Iron Plate 3/10" }, new[] { lines[0].text, lines[1].text });
            Assert.Greater(lines[1].worldBound.yMin, lines[0].worldBound.yMax - 0.5f);
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">採掘進捗・クロスヘア・tooltipのtopic eventを表示する</web-test>
        [Test]
        public void ツールチップは18pxの文字と余白6と10と最大幅320で出て拾わない()
        {
            Localize.TrySetChosenLanguage("japanese");
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.IsTrue(IsShown(face));
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "世界の対象", Find<Label>(_view, "cursor-tooltip-line").text);
            Assert.AreEqual(18f, Find<Label>(_view, "cursor-tooltip-line").resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(6f, face.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(10f, face.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(320f, face.resolvedStyle.maxWidth.value, 0.01f);
            _view.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));

            _view.Render(CursorTooltipViewModel.Hidden);
            Assert.IsFalse(IsShown(face));
        }

        private static CursorTooltipView NewViewWithFakeScreen()
        {
            var screen = new FakeCursorTooltipScreen();
            return new CursorTooltipView(screen, screen, screen.StageScale);
        }
    }
}
