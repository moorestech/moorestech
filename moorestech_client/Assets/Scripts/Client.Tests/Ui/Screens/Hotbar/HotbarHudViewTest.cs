using System.Globalization;
using Client.Tests.Ui.Components;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Hotbar.Parts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// ホットバーの 9 枠・寸法・番号タブ・選択・減光・スキットで退くこと（L1）
    /// Hotbar nine slots, sizes, number tabs, selection, dimming and retreat during skits (L1)
    /// </summary>
    public class HotbarHudViewTest : ScreenTestHost
    {
        private RecordingHotbarCommands _commands;
        private HotbarHudView _view;

        [SetUp]
        public void MountHotbar()
        {
            _commands = new RecordingHotbarCommands();
            _view = Mount(new HotbarHudView(_commands, HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">ホットバー9枠が常時表示され、fixtureのselectedSlotがハイライトされる</web-test>
        [Test]
        public void ホットバーは9枠を出し由来の枠だけを選択で光らせる()
        {
            Assert.IsTrue(IsShown(_view));
            Assert.IsNotNull(Find<VisualElement>(_view, "hotbar-grid"));
            for (var i = 0; i < 9; i++) Find<SlotFrame>(_view, $"hotbar-slot-{i}");
            Assert.IsTrue(Find<SlotFrame>(_view, "hotbar-slot-0").ClassListContains("slot-frame--selected"));
            Assert.IsFalse(Find<SlotFrame>(_view, "hotbar-slot-1").ClassListContains("slot-frame--selected"));
        }

        [Test]
        public void 枠は下端から2pxの中央に並び寸法と枠間と番号タブがWebの値になる()
        {
            // 51.68px の枠が 5px 間隔で並び、列は中央から 0.78px 右・下端から 2px
            // 51.68px slots 5px apart; the row sits 0.78px right of centre and 2px above the bottom
            var slot0 = Find<SlotFrame>(_view, "hotbar-slot-0");
            var slot1 = Find<SlotFrame>(_view, "hotbar-slot-1");
            var row = Find<VisualElement>(_view, "hotbar-grid");
            Assert.AreEqual(51.68f, slot0.worldBound.width, 0.5f);
            Assert.AreEqual(5f, slot1.worldBound.xMin - slot0.worldBound.xMax, 0.5f);
            Assert.AreEqual(720f - 2f, slot0.worldBound.yMax, 0.5f);
            Assert.AreEqual(640f + 0.782875f, row.worldBound.center.x, 0.5f);

            // 番号タブ: 枠の左から 17・上へ 16 はみ出し・高さ 15・暗い縁の数字
            // Number tab: 17 from the slot's left, 16 above, 15 tall, a dark-outlined digit
            var tab = slot0.parent.Q(className: HotbarNumberTab.ClassName);
            Assert.AreEqual(17f, tab.worldBound.xMin - slot0.worldBound.xMin, 0.5f);
            Assert.AreEqual(-16f, tab.worldBound.yMin - slot0.worldBound.yMin, 0.5f);
            Assert.AreEqual(15f, tab.worldBound.height, 0.5f);
            Assert.GreaterOrEqual(tab.worldBound.width, 15f - 0.5f);
            StringAssert.EndsWith("1", tab.Q<Label>(className: "outlined-count-label--glyph").text);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--hotbar-number-tab-face"), tab.resolvedStyle.backgroundColor, "tab face");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), tab.Q<Label>(className: "outlined-count-label--stroke").resolvedStyle.color, "tab stroke");

            // 空き枠の面は部品の既定（Web の値のまま。sRGB 合成なので上書きしない）、ベベルはホットバーの色
            // The empty face is the part default (the Web value; sRGB blending needs no override); the bevel uses the hotbar colors
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--slot-frame-face"), slot1.resolvedStyle.backgroundColor, "empty face");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--hotbar-bevel-c1"), slot1.Q(className: "slot-frame__band--1").resolvedStyle.borderTopColor, "bevel c1");
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">空き枠のクリックはhotbar.select{index}を送る</web-test>
        [Test]
        public void 空き枠のクリックはその枠の選択を送る()
        {
            var center = Find<SlotFrame>(_view, "hotbar-slot-3").worldBound.center;
            simulate.MouseDown(center, MouseButton.LeftMouse);
            simulate.MouseUp(center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "select:3" }, _commands.Calls);
        }

        [Test]
        public void 選択を受けない画面では列が減光し受理は操作側が決める()
        {
            // Web .hotbarFrame[data-select-disabled] の opacity。タップは操作へ渡し、HotbarCommands が規則で捨てて理由を残す
            // Web .hotbarFrame[data-select-disabled] opacity; the tap reaches the operation and HotbarCommands drops it by the rule with a reason
            _view.Render(HotbarViewModelSamples.WebFixture(false, true));
            simulate.FrameUpdate();
            var row = Find<VisualElement>(_view, "hotbar-grid");
            Assert.IsTrue(row.ClassListContains("hotbar-hud__row--select-disabled"));
            Assert.AreEqual(float.Parse(ThemeTokenFile.Resolve("--hotbar-select-disabled-opacity"), CultureInfo.InvariantCulture), HotbarGroupOpacity.Read(row), 0.001f);
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">blockingスキット中はホットバーHUDだけが退き、会話窓は残る</web-test>
        [Test]
        public void blockingスキットの間は帯ごと退き終われば戻る()
        {
            // 会話窓が残ることは S2-skit が見る（計画 D-15）
            // That the dialogue window stays is checked by S2-skit (plan D-15)
            _view.Render(HotbarViewModelSamples.WebFixture(true, false));
            Assert.IsFalse(IsShown(_view));
            _view.Render(HotbarViewModelSamples.WebFixture());
            Assert.IsTrue(IsShown(_view));
        }
    }
}
