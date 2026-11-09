using System;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Toast;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 拡張口のレジストリ（本文・題の操作・列車の本文の引き当てと起動時の検査）の L1
    /// L1 of the extension-port registry (resolving bodies, title actions and the train body, plus startup checks)
    /// </summary>
    public class BlockInventoryPartRegistryTest : BlockInventoryScreenTestHost
    {
        [Test]
        public void SectionStackのKindは標準の本文を返す()
        {
            var registry = NewRegistry(new IBlockInventoryBodyFactory[0]);
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType("Chest");
            Assert.IsInstanceOf<SectionStackBody>(registry.CreateBody(panel, new BlockInventoryBodyMount(false)));
        }

        [Test]
        public void 登録のあるKindはその工場が作る()
        {
            var factory = new FakeBodyFactory(BlockInventoryBodyKind.FilterSplitter);
            var registry = NewRegistry(new IBlockInventoryBodyFactory[] { factory });
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType("FilterSplitter");
            var part = registry.CreateBody(panel, new BlockInventoryBodyMount(true));
            Assert.AreSame(factory.Created, part);
            Assert.IsTrue(factory.LastMount.FillsPanelHeight);
        }

        [Test]
        public void 未登録の本文Kindは標準の本文へ逃げて警告を1回だけ出す()
        {
            var registry = NewRegistry(new IBlockInventoryBodyFactory[0]);
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType("FilterSplitter");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("FilterSplitter"));
            Assert.IsInstanceOf<SectionStackBody>(registry.CreateBody(panel, new BlockInventoryBodyMount(false)));
            Assert.IsInstanceOf<SectionStackBody>(registry.CreateBody(panel, new BlockInventoryBodyMount(false)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 本文の二重登録とSectionStackの外部登録は例外()
        {
            var twice = new IBlockInventoryBodyFactory[] { new FakeBodyFactory(BlockInventoryBodyKind.ElectricPole), new FakeBodyFactory(BlockInventoryBodyKind.ElectricPole) };
            Assert.Throws<InvalidOperationException>(() => NewRegistry(twice));
            var standard = new IBlockInventoryBodyFactory[] { new FakeBodyFactory(BlockInventoryBodyKind.SectionStack) };
            Assert.Throws<InvalidOperationException>(() => NewRegistry(standard));
        }

        [Test]
        public void 列車の本文の2本目は例外()
        {
            var trains = new ITrainInventoryBodyFactory[] { new FakeTrainFactory(), new FakeTrainFactory() };
            Assert.Throws<InvalidOperationException>(() => new BlockInventoryPartRegistry(new IBlockInventoryBodyFactory[0], new IBlockInventoryTitleActionFactory[0], trains, NewStandardBody()));
        }

        private BlockInventoryPartRegistry NewRegistry(IBlockInventoryBodyFactory[] bodies)
        {
            return new BlockInventoryPartRegistry(bodies, new IBlockInventoryTitleActionFactory[0], new ITrainInventoryBodyFactory[0], NewStandardBody());
        }

        private SectionStackBodyFactory NewStandardBody()
        {
            var source = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed));
            var content = new Client.Ui.Content.GameContentPresentation(new Client.Tests.Ui.Content.FakeContentTextureSource(null, null, null));
            var grids = new BlockItemGridFactory(new RecordingBlockSlotCommands(), content, new UiCommandResultToaster(new GameUiToastFeed()));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("区画の登録が無い"));
            return new SectionStackBodyFactory(grids, new SectionStackSlotRegistry(new ISectionStackSectionFactory[0]), source);
        }

        private sealed class FakeBodyFactory : IBlockInventoryBodyFactory
        {
            public BlockInventoryBodyKind Kind { get; private set; }
            public IBlockInventoryPart Created { get; private set; }
            public BlockInventoryBodyMount LastMount { get; private set; }

            public FakeBodyFactory(BlockInventoryBodyKind kind)
            {
                Kind = kind;
            }

            public IBlockInventoryPart Create(BlockInventoryBodyMount mount)
            {
                LastMount = mount;
                Created = new EmptyBlockInventoryPart();
                return Created;
            }
        }

        private sealed class FakeTrainFactory : ITrainInventoryBodyFactory
        {
            public ITrainInventoryPart Create()
            {
                return new EmptyTrainInventoryPart();
            }
        }
    }
}
