using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Components.Gauges;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.Screens.BlockInventory.Special.FilterSplitter;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の View が色を直書きせず、Web と同じ共通部品・縦積み・専用トークンで組まれていること（Web blockInventoryDesign.test.ts の special の持ち分）
    /// Special views use no hard-coded colors and are built from the same shared parts, vertical stacking and dedicated tokens as the Web (special share of blockInventoryDesign.test.ts)
    /// </summary>
    public class SpecialDesignTest : SpecialScreenTestHost
    {
        private const string SpecialRoot = "Scripts/Client.Ui/Screens/BlockInventory/Special";
        private const string ModeSwitchUss = "Scripts/Client.Ui/Components/Controls/ModeSwitch/ModeSwitch.uss";

        // Web の禁止 Mantine 部品・テーマ色に当たるもの: 素の Unity の操作部品と色の直書き
        // Counterparts of the Web's forbidden Mantine parts and theme colors: raw Unity controls and hard-coded colors
        private static readonly Regex ForbiddenControl = new(@"new\s+(Button|ProgressBar|Slider|SliderInt|RadioButton|RadioButtonGroup|Toggle|DropdownField)\s*[({]");
        private static readonly Regex HardCodedColor = new(@"new\s+Color32?\s*\(|\bColor\.(red|green|blue|white|black|yellow|cyan|magenta|gray|grey|clear)\b|style\.(color|backgroundColor|border\w*Color|unityBackgroundImageTintColor)\s*=|""#[0-9a-fA-F]{3,8}""");

        private static readonly Dictionary<string, string[]> ViewFilesByWebSource = new()
        {
            ["gear"] = new[] { "Energy/GearSectionView.cs" },
            ["filterSplitter"] = new[] { "FilterSplitter/FilterSplitterBodyView.cs", "FilterSplitter/FilterDirectionView.cs" },
            ["electricToGear"] = new[] { "Energy/ElectricToGearBodyView.cs" },
            ["network"] = new[] { "Network/ElectricNetworkSectionView.cs", "Network/GearNetworkSectionView.cs" },
        };

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [TestCase("gear")]
        [TestCase("filterSplitter")]
        [TestCase("electricToGear")]
        [TestCase("network")]
        public void 特殊族のViewは素の操作部品と色の直書きを使わない(string webSource)
        {
            foreach (var file in ViewFilesByWebSource[webSource])
            {
                var source = File.ReadAllText(Path.Combine(Application.dataPath, SpecialRoot, file));
                Assert.IsFalse(ForbiddenControl.IsMatch(source), $"{file} が素の操作部品を作っている");
                Assert.IsFalse(HardCodedColor.IsMatch(source), $"{file} に色の直書きがある");
            }

            // 組んだ木でも、共通部品の外に素の操作部品が無いこと（alpha の同じタグの件と同じ境界。alpha D-46）
            // The built tree must hold no raw controls outside shared components (same boundary as alpha's case under this tag. alpha D-46)
            foreach (var view in MountViewsOf(webSource))
            {
                foreach (var element in OwnTree(view)) Assert.IsFalse(!IsSharedComponent(element) && IsRawControl(element), $"{webSource} の木に素の操作部品 {element.GetType().Name}（{element.name}）がある");
            }
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ゲージとモード切替を用途別に共通部品へ置換する</web-test>
        [Test]
        public void 回転生成機は縦のModeSwitchとGaugeBarをフィルタ分配器は方向ごとのModeSwitchを使う()
        {
            // 回転生成機: 縦の択一と充足率のゲージ（Generator・TrainPlatform の面は S4-machine・S4-train）
            // Electric-to-gear: a vertical switch and the fulfillment gauge (Generator and TrainPlatform belong to S4-machine and S4-train)
            var electricToGear = Mount(new ElectricToGearBodyView(Kit(null)));
            electricToGear.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.ElectricToGear));
            Assert.IsTrue(Find<VisualElement>(electricToGear, "electric-to-gear-modes").ClassListContains("mode-switch--vertical"));
            Assert.IsNotNull(electricToGear.Q<GaugeBar>("electric-to-gear-fulfillment"));

            var filter = RenderFilterSplitter();
            for (var d = 0; d < 3; d++) Assert.IsTrue(Find<VisualElement>(filter, $"filter-mode-{d}").ClassListContains("mode-switch"));
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">フィルタ分岐器の方向を縦積みにする</web-test>
        [Test]
        public void フィルタ分配器の方向は縦に積む()
        {
            var filter = RenderFilterSplitter();
            simulate.FrameUpdate();
            var directions = Enumerable.Range(0, 3).Select(d => Find<VisualElement>(filter, $"filter-direction-{d}").worldBound).ToArray();
            for (var d = 1; d < 3; d++)
            {
                Assert.AreEqual(directions[0].xMin, directions[d].xMin, 0.5f, "方向は左端をそろえる");
                Assert.GreaterOrEqual(directions[d].yMin, directions[d - 1].yMax, "方向は上から下へ積む");
            }
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">モード切替の間隔と選択面を専用トークンで制御する</web-test>
        [Test]
        public void モード切替の間隔と内側の余白と選択面は専用トークンで決まる()
        {
            // Web の --mode-switch-selected-mix は USS に color-mix が無いので DerivedTokens の --mode-switch-selected-face に焼いてある
            // The Web's --mode-switch-selected-mix is baked into DerivedTokens' --mode-switch-selected-face because USS has no color-mix
            var uss = File.ReadAllText(Path.Combine(Application.dataPath, ModeSwitchUss));
            foreach (var token in new[] { "var(--mode-switch-gap)", "var(--mode-switch-padding-block)", "var(--mode-switch-padding-inline)", "var(--mode-switch-selected-face)" }) StringAssert.Contains(token, uss);
        }

        private VisualElement[] MountViewsOf(string webSource)
        {
            switch (webSource)
            {
                case "gear": return new VisualElement[] { RenderInto(new GearSectionView(), SpecialDetailSamples.GearMachine) };
                case "filterSplitter": return new VisualElement[] { RenderFilterSplitter() };
                case "electricToGear": return new VisualElement[] { RenderInto(new ElectricToGearBodyView(Kit(null)), SpecialDetailSamples.Only(SpecialDetailSamples.ElectricToGear)) };
                case "network": return new VisualElement[] { RenderInto(new ElectricNetworkSectionView(), SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork)), RenderInto(new GearNetworkSectionView(), SpecialDetailSamples.GearMachine) };
                default: throw new System.ArgumentOutOfRangeException(nameof(webSource), webSource, "Web の sources の鍵に対応する View が無い");
            }
        }

        private VisualElement RenderInto<TView>(TView view, SpecialBlockDetailViewModel detail) where TView : VisualElement, ISpecialDetailRenderer
        {
            Mount(view);
            view.Render(NoSlots, detail);
            return view;
        }

        // 共通部品（Client.Ui.Components 以下の型）は内部へ降りず、利用側が足す contentContainer の子だけたどる
        // Do not descend into shared components (types under Client.Ui.Components); follow only the children the caller adds to their contentContainer
        private static IEnumerable<VisualElement> OwnTree(VisualElement root)
        {
            foreach (var child in root.hierarchy.Children())
            {
                yield return child;
                var next = IsSharedComponent(child) ? child.contentContainer : child;
                if (IsSharedComponent(child) && next == child) continue;
                foreach (var descendant in OwnTree(next)) yield return descendant;
            }
        }

        private static bool IsSharedComponent(VisualElement element)
        {
            var ns = element.GetType().Namespace ?? string.Empty;
            return ns == "Client.Ui.Components" || ns.StartsWith("Client.Ui.Components.");
        }

        private static bool IsRawControl(VisualElement element)
        {
            return element is Button || element is Toggle || element is RadioButton || element is Slider || element is ProgressBar;
        }

        private FilterSplitterBodyView RenderFilterSplitter()
        {
            var view = Mount(new FilterSplitterBodyView(Kit(LoadContent())));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            return view;
        }
    }
}
