using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Ui.Components.Gauges;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.BlockInventory.Family;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Logic.Detail;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 採掘機・ポンプ・発電機の区画の見た目の決まり（共通部品・寒色トークン・流体アイコンの枠・行の共有）
    /// Design rules of the miner, pump and generator sections (shared parts, cool tokens, fluid icon box, shared rows)
    /// </summary>
    public class FamilyDesignTest : FamilyScreenTestHost
    {
        private const string FamilyUssPath = "Assets/Scripts/Client.Ui/Screens/BlockInventory/Family/Family.uss";

        private static ElectricPumpDetail PumpWithWater()
        {
            return new ElectricPumpDetail(MachineProcessDisplayState.Processing, 100, 100, new[] { new PumpingFluid(Water, 60) });
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ゲージとモード切替を用途別に共通部品へ置換する</web-test>
        [Test]
        public void 発電機は棒のゲージで採掘機は進捗の帯を使う()
        {
            var body = MountBody();
            Generator.Set(SectionContent<GeneratorDetail>.Present(new GeneratorDetail(1, 2, 1)));
            Miner.Set(SectionContent<MinerDetail>.Present(new MinerDetail(1, 1, 0.5f, false, new MinerMiningItem[0])));
            simulate.FrameUpdate();

            Assert.IsNotNull(body.Root.Q<GaugeBar>("generator-fuel-progress"));
            Assert.IsNotNull(body.Root.Q<ProgressArrowBar>("progress-arrow-bar"));
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ゲージ系部品を寒色トークンで統一する</web-test>
        [Test]
        public void ゲージの色は部品のトークンのままで区画のUSSは色を書かない()
        {
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(FamilyUssPath);
            Assert.IsNotNull(uss, FamilyUssPath);
            var text = System.IO.File.ReadAllText(FamilyUssPath);
            Assert.IsFalse(Regex.IsMatch(text, @"(^|[\s;{])(background-)?color\s*:|#[0-9a-fA-F]{3,8}\b|rgba?\("), "Family.uss must not write colors");

            var body = MountBody();
            Generator.Set(SectionContent<GeneratorDetail>.Present(new GeneratorDetail(1, 2, 1)));
            Miner.Set(SectionContent<MinerDetail>.Present(new MinerDetail(1, 1, 0.5f, false, new MinerMiningItem[0])));
            simulate.FrameUpdate();

            var gauge = body.Root.Q<GaugeBar>("generator-fuel-progress");
            var bar = body.Root.Q<ProgressArrowBar>("progress-arrow-bar");
            AssertCoolTokens(gauge, "gauge-bar__fill", "gauge");
            AssertCoolTokens(bar, "progress-arrow-bar__fill", "bar");
        }

        private static void AssertCoolTokens(VisualElement bar, string fillClass, string label)
        {
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--gauge-track"), bar.resolvedStyle.backgroundColor, $"{label} track");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--gauge-fill"), bar.Q(className: fillClass).resolvedStyle.backgroundColor, $"{label} fill");
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ポンプの流体アイコンを非正方形でも歪ませずに収める</web-test>
        [Test]
        public void ポンプの流体アイコンは枠の大きさの四方で縦横比を保つ()
        {
            var view = new PumpSectionView(Content);
            view.Root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(FamilyUssPath));
            view.Render(PumpWithWater());
            Mount(view.Root);

            var icon = view.Root.Q<GameIcon>(null, "pump-section__fluid-icon");
            var slotSize = ThemeTokenFile.ResolvePixels("--slot-size");
            Assert.AreEqual(slotSize, icon.resolvedStyle.width, 0.5f);
            Assert.AreEqual(slotSize, icon.resolvedStyle.height, 0.5f);
            // 縦横比を保つ枠への収め方は旧 API でしか解決値を読めない
            // The fit mode that keeps the aspect ratio can only be read through the old resolved-style API
#pragma warning disable CS0618
            Assert.AreEqual(ScaleMode.ScaleToFit, icon.Q(className: "game-icon__image").resolvedStyle.unityBackgroundScaleMode.value);
#pragma warning restore CS0618
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">稼働ラベルと分間レートの行を機械・採掘機・ポンプで共有する</web-test>
        [Test]
        public void ポンプは稼働ラベルと分間レートの共有の行を持つ()
        {
            var view = new PumpSectionView(Content);
            view.Render(PumpWithWater());
            Mount(view.Root);

            Assert.IsNotNull(view.Root.Q<Label>("pump-state-label"));
            Assert.IsNotNull(view.Root.Q<PowerRateText>());
            Assert.IsNotNull(view.Root.Q<PerMinuteRateRow>());
        }
    }
}
