using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Logic.Detail;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 採掘機・ポンプ・発電機の区画の出し入れと中身（本物の工場・偽の Model で標準の本文に組む）
    /// Visibility and contents of the miner, pump and generator sections (real factories and fake models in the standard body)
    /// </summary>
    public class BlockDetailsFamilyTest : FamilyScreenTestHost
    {
        private const string InsufficientClass = "lack-highlight-text--insufficient";
        private static MinerDetail MinerOf(double current, double request, float progress, bool gear, params MinerMiningItem[] items)
        {
            return new MinerDetail(current, request, progress, gear, items);
        }

        private static ElectricPumpDetail ElectricPump(params PumpingFluid[] fluids)
        {
            return new ElectricPumpDetail(MachineProcessDisplayState.Processing, 100, 100, fluids);
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">renders ${type} detail section</web-test>
        [Test]
        public void 種別ごとに対応する区画が出る()
        {
            var cases = new List<(string Name, System.Action Set)>
            {
                ("miner-section", () => Miner.Set(SectionContent<MinerDetail>.Present(MinerOf(100, 100, 0, false)))),
                ("pump-section", () => Pump.Set(SectionContent<PumpDetail>.Present(ElectricPump(new PumpingFluid(Water, 60))))),
                ("generator-section", () => Generator.Set(SectionContent<GeneratorDetail>.Present(new GeneratorDetail(1, 2, 1)))),
            };
            var body = MountBody();
            foreach (var (name, _) in cases) Assert.AreEqual(0, Shown(body.Root, name).Count, $"{name} before");
            foreach (var (name, set) in cases)
            {
                set();
                simulate.FrameUpdate();
                Assert.AreEqual(1, Shown(body.Root, name).Count, name);
            }

            // 歯車ポンプも同じ pump-section に出る
            // A gear pump shows in the same pump-section
            Pump.Set(SectionContent<PumpDetail>.Present(new GearPumpDetail(new[] { new PumpingFluid(Water, 60) })));
            simulate.FrameUpdate();
            Assert.AreEqual(1, Shown(body.Root, "pump-section").Count, "gear pump");
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">電力駆動の採掘機は充足率不足を赤で出す</web-test>
        [Test]
        public void 電力駆動の採掘機は充足率不足を赤で出し歯車駆動は出さない()
        {
            var body = MountBody();
            Miner.Set(SectionContent<MinerDetail>.Present(MinerOf(50, 100, 0, false)));
            simulate.FrameUpdate();
            Assert.IsTrue(body.Root.Q<Label>("miner-power-rate").ClassListContains(InsufficientClass));

            Miner.Set(SectionContent<MinerDetail>.Present(MinerOf(50, 100, 0, true)));
            simulate.FrameUpdate();
            Assert.IsFalse(body.Root.Q<Label>("miner-power-rate").ClassListContains(InsufficientClass));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">油井は電力充足率と公称生成速度を出し鉱脈外なら警告行に切り替わる</web-test>
        [Test]
        public void 油井は電力と毎分を出し鉱脈外なら警告行に切り替わる()
        {
            var body = MountBody();
            Pump.Set(SectionContent<PumpDetail>.Present(ElectricPump(new PumpingFluid(Water, 3600))));
            simulate.FrameUpdate();

            Assert.AreEqual(1, Shown(body.Root, "pump-state-label").Count);
            StringAssert.Contains("100%", body.Root.Q<Label>("pump-power-rate").text);
            Assert.AreEqual(1, Shown(body.Root, "pump-pumping-fluids").Count);
            StringAssert.Contains("3600.0", body.Root.Q("pump-pumping-fluids").Q<Label>().text);
            Assert.AreEqual(0, Shown(body.Root, "pump-no-vein").Count);

            Pump.Set(SectionContent<PumpDetail>.Present(ElectricPump()));
            simulate.FrameUpdate();
            Assert.AreEqual(1, Shown(body.Root, "pump-no-vein").Count);
            Assert.IsTrue(body.Root.Q<Label>("pump-no-vein").ClassListContains(InsufficientClass));
            Assert.AreEqual(0, Shown(body.Root, "pump-pumping-fluids").Count);
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">歯車ポンプは電力行を持たず歯車行を出す</web-test>
        [Test]
        public void 歯車ポンプは電力行を持たない()
        {
            var body = MountBody();
            Pump.Set(SectionContent<PumpDetail>.Present(new GearPumpDetail(new[] { new PumpingFluid(Water, 60) })));
            simulate.FrameUpdate();

            Assert.AreEqual(1, Shown(body.Root, "pump-section").Count);
            Assert.AreEqual(0, Shown(body.Root, "pump-state-label").Count);
            Assert.AreEqual(0, Shown(body.Root, "pump-power-rate").Count);
            Assert.AreEqual(1, Shown(body.Root, "pump-pumping-fluids").Count);
        }

        [Test]
        public void 発電機の燃料の棒は燃料比で稼働率の文は四捨五入で出る()
        {
            var body = MountBody();
            Generator.Set(SectionContent<GeneratorDetail>.Present(new GeneratorDetail(30, 100, 0.456)));
            simulate.FrameUpdate();

            var fill = body.Root.Q<GaugeBar>("generator-fuel-progress").Q(className: "gauge-bar__fill");
            Assert.AreEqual(30f, fill.style.width.value.value, 0.01f);
            Assert.AreEqual("Operating rate 46%", body.Root.Q<Label>("generator-operating-rate").text);
        }

        [Test]
        public void 採掘機の進捗の棒は進捗の幅で出る()
        {
            var body = MountBody();
            Miner.Set(SectionContent<MinerDetail>.Present(MinerOf(100, 100, 0.25f, false)));
            simulate.FrameUpdate();

            var fill = body.Root.Q<ProgressArrowBar>("progress-arrow-bar").Q(className: "progress-arrow-bar__fill");
            Assert.AreEqual(25f, fill.style.width.value.value, 0.01f);
        }

        [Test]
        public void 採掘中の品が同じ再描画では行の要素が同一のまま毎分だけ変わる()
        {
            var body = MountBody();
            Miner.Set(SectionContent<MinerDetail>.Present(MinerOf(100, 100, 0.1f, false, new MinerMiningItem(FirstItem, 30))));
            simulate.FrameUpdate();
            var slotName = $"miner-mining-item-{FirstItem.AsPrimitive()}";
            var before = body.Root.Q(slotName);
            Assert.IsNotNull(before);

            Miner.Set(SectionContent<MinerDetail>.Present(MinerOf(100, 100, 0.6f, false, new MinerMiningItem(FirstItem, 60))));
            simulate.FrameUpdate();
            Assert.AreSame(before, body.Root.Q(slotName));
            StringAssert.Contains("60.0", body.Root.Q("miner-mining-items").Q<Label>(null, "per-minute-rate-row__text").text);

            // 品の並びが変わると行は作り直される
            // A changed item sequence rebuilds the rows
            Miner.Set(SectionContent<MinerDetail>.Present(MinerOf(100, 100, 0.6f, false, new MinerMiningItem(SecondItem, 60))));
            simulate.FrameUpdate();
            Assert.IsNull(body.Root.Q(slotName));
            Assert.IsNotNull(body.Root.Q($"miner-mining-item-{SecondItem.AsPrimitive()}"));
        }
    }
}
