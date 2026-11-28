using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Panel;
using Game.Block.Interface;
using UniRx;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.SectionStack
{
    /// <summary>
    /// 回転発電機の 4 区画が本物の区画で Web と同じ順と中身で出る（Web SectionStackView.test.tsx の special の持ち分。順だけは alpha が偽の区画で見る）
    /// The generator's four sections render with real sections in the Web order and content (special share of SectionStackView.test.tsx; alpha checks the order alone with fake sections)
    /// </summary>
    public class SpecialSectionStackOrderTest : SpecialScreenTestHost
    {
        private static readonly string[] ExpectedIds =
        {
            "gear-to-electric-section", "gear-to-electric-state", "gear-to-electric-supplied-power",
            "gear-section", "gear-torque", "gear-rpm", "electric-network-section", "gear-network-section",
        };

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">親から状態・供給・歯車・電力網・歯車網を順に描画する</web-test>
        [Test]
        public void 回転発電機は状態と供給と歯車と電力網と歯車網を順に中身つきで出す()
        {
            // Web と同じ値: 発電中 72.4/100・歯車 120rpm 100trq（基準 120）・電力網 100/72.4・歯車網 100/72
            // Same values as the Web: generating 72.4/100, gear 120rpm 100trq (base 120), electric 100/72.4, gear network 100/72
            var detail = new SpecialBlockDetailViewModel(null, new GearToElectricDetail(GearToElectricDisplayState.Generating, 72.4, 100), GearDetail.Consumer(120, 100, 120), null, new ElectricNetworkDetail(100, 72.4, 1, 1), new GearNetworkDetail(100, 72, GearNetworkStopReasonKind.None), null, null, null, null, BlockInventoryTestPanels.SampleInstanceId);
            var root = MountStack("GearToElectricGenerator", S.GearToElectricGenerator, detail);

            // 表示中の要素の testid を木の順に読む（Web は data-testid を持つ要素を木の順で並べる）
            // Read the testids of shown elements in tree order (the Web lists data-testid elements in tree order)
            var ids = root.Query<VisualElement>().ToList().Where(e => IsSectionOrRowId(e.name) && IsShownInTree(e, root)).Select(e => e.name).ToArray();
            CollectionAssert.AreEqual(ExpectedIds, ids, "余計な区画・行・項目グリッドが出ていない");
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.GearToElectricStateGenerating).Resolve(), TextOf(root, "gear-to-electric-state"));
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.GearToElectricSuppliedPower, new[] { new LocalizationTextParam("current", "72"), new LocalizationTextParam("max", "100") }).Resolve(), TextOf(root, "gear-to-electric-supplied-power"));
        }

        [Test]
        public void 別個体の詳細を持つパネル購読者には空の詳細が届く()
        {
            var panel = BlockInventoryTestPanels.OpenBlock("GearMachine", S.Machine, default, default, new BlockInstanceId(2), default, NoSlots, BlockInventoryPanelError.None);
            Panels.State.Value = panel;
            Model.Set(SpecialDetailSamples.GearMachine);

            SpecialBlockDetailViewModel received = null;
            using var subscription = SpecialPanelDetailStream.Of(Panels, Model, panel).Subscribe(value => received = value.detail);
            Assert.AreSame(SpecialBlockDetailViewModel.Empty, received);
        }

        // 区画（-section）・項目グリッド（-grid）と、Web が並べる区画の行の id
        // Section (-section) and item-grid (-grid) ids plus the section-row ids the Web lists
        private static bool IsSectionOrRowId(string name)
        {
            return !string.IsNullOrEmpty(name) && (name.EndsWith("-section") || name.EndsWith("-grid") || ExpectedIds.Contains(name));
        }

        // 自分と根までの祖先がどれも隠れていない
        // Neither the element nor any ancestor up to the root is hidden
        private static bool IsShownInTree(VisualElement element, VisualElement root)
        {
            for (var e = element; e != null && e != root.parent; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }
    }
}
