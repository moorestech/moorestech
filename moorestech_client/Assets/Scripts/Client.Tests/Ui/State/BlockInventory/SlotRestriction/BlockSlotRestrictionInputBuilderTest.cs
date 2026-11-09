using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.Machine;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.BlockInventory.SlotRestriction
{
    /// <summary>
    /// 開いたブロックの状態と寄与から束縛の入力が組まれる（L0）
    /// The binding input is built from the open block state and contributions (L0)
    /// </summary>
    public class BlockSlotRestrictionInputBuilderTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 取得失敗のブロックは取得失敗の入力になる()
        {
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.OpenFailedChest();
            var input = new BlockSlotRestrictionInputBuilder(new[] { new StubContributor("a", true, new List<string>()) }).Build(panel);
            Assert.IsInstanceOf<OpenFailedBlockSlotInput>(input);
        }

        [Test]
        public void 寄与が無いチェストは詳細が取得待ちのままの開いたブロックの入力になる()
        {
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.Chest();
            var input = new BlockSlotRestrictionInputBuilder(new IBlockSlotRestrictionContributor[0]).Build(panel);
            Assert.IsInstanceOf<OpenBlockSlotInput>(input);
            Assert.IsInstanceOf<PendingBlockSlotDetail>(((OpenBlockSlotInput)input).Detail);
        }

        [Test]
        public void 適用対象の寄与だけが順にContributeされる()
        {
            var calls = new List<string>();
            var contributors = new IBlockSlotRestrictionContributor[]
            {
                new StubContributor("first", true, calls),
                new StubContributor("skipped", false, calls),
                new StubContributor("second", true, calls),
            };
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.Chest();
            var input = (OpenBlockSlotInput)new BlockSlotRestrictionInputBuilder(contributors).Build(panel);
            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
            Assert.IsInstanceOf<MachineBlockSlotDetail>(input.Detail);
        }

        // 機械の束縛を足すだけの寄与の代役
        // Stand-in contributor that only adds machine bindings
        private sealed class StubContributor : IBlockSlotRestrictionContributor
        {
            private readonly string _name;
            private readonly bool _applies;
            private readonly List<string> _calls;

            public StubContributor(string name, bool applies, List<string> calls)
            {
                _name = name;
                _applies = applies;
                _calls = calls;
            }

            public bool AppliesTo(OpenBlockInventoryPanel panel) => _applies;

            public OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input)
            {
                _calls.Add(_name);
                return input.WithMachine(MachineSlotGhostFixtures.Unbound());
            }
        }
    }
}
