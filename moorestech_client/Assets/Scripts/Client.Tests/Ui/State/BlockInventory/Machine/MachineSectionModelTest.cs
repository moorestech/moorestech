using System;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.Block;
using Client.Tests.Common;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Source;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.MachineUpgrade;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Gear.Common;

namespace Client.Tests.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画 Model がパネルと追跡ブロックから組み直す
    /// The machine section model rebuilds from the panel and the tracked block
    /// </summary>
    public class MachineSectionModelTest
    {
        private static readonly Vector3Int Position = new(1, 0, 0);
        private TrackedBlockHarness _harness;
        private ReactiveProperty<BlockInventoryPanelState> _panel;
        private MachineSectionModel _model;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _harness = new TrackedBlockHarness();
            _panel = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            // レシピ源は Initialize しない（空の表のまま）
            // The recipe source is not initialized (stays an empty table)
            var recipeSource = new UnlockedMachineRecipeSource(null, null, null);
            _model = new MachineSectionModel(_harness.Tracker, new FakePanelSource(_panel), recipeSource, new FixedStackLimit(), new FakeFluidReplicaReader());
            _model.Initialize();
        }

        [TearDown]
        public void TearDown() => _harness.Dispose();

        [Test]
        public void 状態詳細を差し替えてパネルが組み直されると新しい進捗が配られる()
        {
            var block = MachineBlockWith(MachineTestDetails.MachineBlock, 0.25f);
            _harness.Track(block);
            _panel.Value = MachinePanel(MachineTestDetails.DefaultInstanceId);
            Assert.IsTrue(_model.Current.Value.TryGetPresent(out var before));
            Assert.AreEqual(0.25f, before.Detail.Progress, 1e-6);

            // 本番では alpha の Model が状態変化でパネルを組み直す
            // In production alpha's model rebuilds the panel on state changes
            _harness.ReplaceStates(block, Common(), (MachineBlockStateDetail.BlockStateDetailKey, Machine(0.75f)));
            _harness.NotifyStateChanged();
            _panel.Value = MachinePanel(MachineTestDetails.DefaultInstanceId);

            Assert.IsTrue(_model.Current.Value.TryGetPresent(out var after));
            Assert.AreEqual(0.75f, after.Detail.Progress, 1e-6);
        }

        [Test]
        public void 機械でないブロックとパネルの個体IDが違うブロックはAbsent()
        {
            var notMachine = MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is not IMachineParam);
            _harness.Track(MachineBlockWith(notMachine, 0.5f));
            _panel.Value = MachinePanel(MachineTestDetails.DefaultInstanceId);
            Assert.IsFalse(_model.Current.Value.IsPresent, "IMachineParam でない");

            // 追跡替え直後にパネルが前のブロックのまま届いた場合
            // The panel still names the previous block right after a tracking switch
            _harness.Track(MachineBlockWith(MachineTestDetails.MachineBlock, 0.5f));
            _panel.Value = MachinePanel(new BlockInstanceId(99));
            Assert.IsFalse(_model.Current.Value.IsPresent, "パネルと追跡ブロックの個体 ID が違う");
        }

        [Test]
        public void 歯車の不整合は組み直しを繰り返しても1回で_閉じて開き直すと再び出る()
        {
            var master = MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is IMachineParam and not IGearConsumptionParam);
            var block = MachineBlockWith(master, 0.5f);
            var gear = new GearStateDetail(true, 5f, 2.4f, GearRole.Consumer);
            _harness.ReplaceStates(block, Common(), (MachineBlockStateDetail.BlockStateDetailKey, Machine(0.5f)), (GearStateDetail.BlockStateDetailKey, gear));
            _harness.Track(block);

            LogAssert.Expect(LogType.Error, new Regex("has no IGearConsumptionParam"));
            _panel.Value = MachinePanel(MachineTestDetails.DefaultInstanceId);
            _panel.Value = MachinePanel(MachineTestDetails.DefaultInstanceId);

            // パネルを閉じる間は区画が無く、旗が戻る
            // While the panel is closed there is no section and the flag clears
            _panel.Value = BlockInventoryPanelState.Closed;
            LogAssert.Expect(LogType.Error, new Regex("has no IGearConsumptionParam"));
            _panel.Value = MachinePanel(MachineTestDetails.DefaultInstanceId);
        }

        private BlockGameObject MachineBlockWith(BlockMasterElement master, float progress)
        {
            var block = _harness.CreateBlock(Position, master, Common(), (MachineBlockStateDetail.BlockStateDetailKey, Machine(progress)));
            TestReflection.SetField(block, "<BlockInstanceId>k__BackingField", MachineTestDetails.DefaultInstanceId);
            return block;
        }

        private static OpenBlockInventoryPanel MachinePanel(BlockInstanceId instanceId)
        {
            // 機械の全枠より多い空き枠を渡す
            // Pass more empty slots than any machine layout uses
            var slots = Enumerable.Repeat(new SlotItem(new ItemId(0), 0), 20).ToArray();
            return MachineTestDetails.Panel(instanceId, slots);
        }

        private static (string, object) Common()
        {
            return (CommonMachineBlockStateDetail.BlockStateDetailKey, MachineTestDetails.Common(VanillaMachineBlockStateConst.ProcessingState, 1, 1));
        }

        private static MachineBlockStateDetail Machine(float progress)
        {
            return new MachineBlockStateDetail(progress, Guid.Empty, Guid.Empty, false);
        }

        // 上限は固定で変化しない
        // A fixed limit that never changes
        private sealed class FixedStackLimit : IModuleSlotStackLimitLookup
        {
            public int CurrentLevel => 0;
            public int CurrentStackLimit => 1;
            public IObservable<int> OnStackLimitChanged => Observable.Never<int>();
        }
    }
}
