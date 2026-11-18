using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.SectionStack
{
    /// <summary>
    /// 先頭の区画がパネルの組み直しの外で後から出る・消えるとき、後続の区画の余白まで Web の Stack gap="sm" と同じに組み直される
    /// When the leading section appears or disappears outside panel rebuilds, the following sections' gaps are re-laid like the Web Stack gap="sm"
    /// </summary>
    public class SpecialSectionStackSpacingTest : SpecialScreenTestHost
    {
        private static readonly string[] SpecialSectionNames = { "gear-to-electric-section", "belt-throughput-section", "gear-section", "electric-network-section", "gear-network-section" };

        [Test]
        public void 先頭の区画が後から届くと後続の区画に余白が付き消えると詰まる()
        {
            // 歯車と歯車網だけの詳細で開く（回転発電機の状態は未着）
            // Open with only gear and gear-network details (the generator state has not arrived)
            var root = MountStack("GearToElectricGenerator", WithGearToElectric(null));
            AssertLeadingGap(root, "gear-section", "gear-network-section");

            // 回転発電機の状態が組み直しの外で届く → 先頭が入れ替わり、歯車の区画に余白が付く
            // The generator state arrives outside a rebuild; the leader changes and the gear section gains a gap
            Model.Set(WithGearToElectric(SpecialDetailSamples.GearToElectricGenerating));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-to-electric-section", "gear-section", "gear-network-section");

            // 先頭が消える → 歯車の区画が先頭に戻り、余白が 0 に詰まる
            // The leader disappears; the gear section leads again and its gap collapses to 0
            Model.Set(WithGearToElectric(null));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-section", "gear-network-section");
        }

        [Test]
        public void 先頭の歯車の区画が消えると歯車網が先頭で余白0になり網が消えても先頭は変わらない()
        {
            var root = MountStack("GearMachine", SpecialDetailSamples.GearMachine);
            AssertLeadingGap(root, "gear-section", "gear-network-section");

            // 先頭が消える → 歯車網が先頭になり余白 0
            // The leader vanishes; the gear network leads with a zero gap
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.GearMachineNetwork));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-network-section");

            // 網の集約が消える（末尾の消失）→ 歯車の区画だけが余白 0 で残る
            // The network aggregate vanishes (tail removal); only the gear section remains with a zero gap
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.GearMachineGear));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-section");
        }

        private VisualElement MountStack(string blockType, SpecialBlockDetailViewModel detail)
        {
            Model.Set(detail);
            var panel = OpenPanel(blockType);
            var body = BlockInventoryTestBodies.SectionStack(Panels, blockType, SectionFactories());
            Mount(body.Root);
            body.Attach(panel);
            simulate.FrameUpdate();
            return body.Root;
        }

        private static SpecialBlockDetailViewModel WithGearToElectric(GearToElectricDetail generator)
        {
            return new SpecialBlockDetailViewModel(null, generator, GearDetail.Generator(20, 5), null, null, new GearNetworkDetail(20, 30, GearNetworkStopReasonKind.None), null, null, null);
        }

        // 表示中の特殊族の区画が期待の順に並び、先頭だけ余白 0・他は正（UiGap.Sm の実値）
        // Shown special sections appear in the expected order; only the leader has a 0 gap, the others positive (the UiGap.Sm value)
        private static void AssertLeadingGap(VisualElement root, params string[] expected)
        {
            var shown = root.Children().Where(e => e.resolvedStyle.display == DisplayStyle.Flex).ToList();
            CollectionAssert.AreEqual(expected, shown.Select(e => e.name).Where(SpecialSectionNames.Contains).ToArray());
            Assert.AreEqual(0f, shown[0].resolvedStyle.marginTop, "表示中の先頭の区画は余白 0");
            foreach (var following in shown.Skip(1)) Assert.Less(0f, following.resolvedStyle.marginTop, $"{following.name} に区画の間の余白がある");
        }
    }
}
