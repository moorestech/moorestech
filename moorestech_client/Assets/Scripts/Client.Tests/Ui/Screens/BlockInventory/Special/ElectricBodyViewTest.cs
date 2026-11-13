using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.State.BlockInventory.Special.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 電柱の本文（電力網の集約）と回転生成機の本文（出力モード・充足率・消費電力・選択の送信）（Web の e2e を L1 で）
    /// Electric pole body (network aggregate) and electric-to-gear body (output modes, fulfillment, consumed power, selection) (Web e2e as L1)
    /// </summary>
    public class ElectricBodyViewTest : SpecialScreenTestHost
    {
        /// <web-test file="e2e/tests/block/electric/electricPole.spec.ts">電柱専用ビューに発電量・需要量・消費者数・供給率を表示する</web-test>
        [Test]
        public void 電柱の本文に発電量と需要量と消費者数と供給率を出す()
        {
            var view = Mount(new ElectricPoleBodyView());
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));

            Assert.IsTrue(IsShown(Find<VisualElement>(view, "electric-pole-view")));
            var summary = TextOf(view, "electric-network-summary");
            foreach (var part in new[] { "240", "180", "3", "100%" }) StringAssert.Contains(part, summary);
        }

        /// <web-test file="e2e/tests/block/electric/electricToGear.spec.ts">出力モード行とStateDetail値を表示し、選択indexをaction送信する</web-test>
        [Test]
        public void 出力モードの行と状態の値を出し選んだ添字を送る()
        {
            var view = Mount(new ElectricToGearBodyView(Kit(null)));
            var detail = SpecialDetailSamples.ElectricToGear;
            view.Render(NoSlots, SpecialDetailSamples.Only(detail));
            simulate.FrameUpdate();

            Assert.IsTrue(IsShown(Find<VisualElement>(view, "electric-to-gear-view")));
            Assert.AreEqual(Resolved(SpecialDetailTexts.ElectricToGearOutputMode(detail.OutputModes[0])), ModeLabel(view, "electric-to-gear-mode-0"));
            StringAssert.Contains("10 rpm / 10 trq / 10 W", ModeLabel(view, "electric-to-gear-mode-0"));
            StringAssert.Contains("10 W", TextOf(view, "electric-to-gear-consumed-power"));
            Assert.IsTrue(IsSelected(view, "electric-to-gear-mode-1"));
            Assert.AreEqual(0.75f, GaugeValue(view, "electric-to-gear-fulfillment"), 0.001f);

            simulate.Click(Find<Button>(view, "electric-to-gear-mode-0"));
            CollectionAssert.AreEqual(new[] { "electric_to_gear.set_output_mode 0" }, Commands.Issued);
        }

        [Test]
        public void 同じモードの一覧の更新では切り替えを作り直さない()
        {
            var view = Mount(new ElectricToGearBodyView(Kit(null)));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.ElectricToGear));
            var before = view.Q<ModeSwitch<int>>();

            view.Render(NoSlots, SpecialDetailSamples.Only(new Client.Ui.State.BlockInventory.Special.Details.ElectricToGearDetail(SpecialDetailSamples.ElectricToGear.OutputModes, 0, 0.5, 20)));
            Assert.AreSame(before, view.Q<ModeSwitch<int>>());
            Assert.IsTrue(IsSelected(view, "electric-to-gear-mode-0"));
            Assert.AreEqual(0.5f, GaugeValue(view, "electric-to-gear-fulfillment"), 0.001f);
        }

        // 択一の押し面は Button の中の LocalizedLabel に文を載せる（Button.text は空）
        // A mode option carries its text on the LocalizedLabel inside the Button (Button.text stays empty)
        private static string ModeLabel(VisualElement root, string optionTestid)
        {
            return Find<Button>(root, optionTestid).Q<Label>().PlainText();
        }
    }
}
