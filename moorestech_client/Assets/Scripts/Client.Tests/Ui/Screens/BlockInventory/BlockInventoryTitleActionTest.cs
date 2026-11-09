using System;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 題の操作の器の付け替え（D-17。器は 1 回だけ渡し、中身を器の中で差し替える）の L1
    /// L1 of swapping the title-action holder contents (D-17; the holder goes in once and its contents are swapped inside)
    /// </summary>
    public class BlockInventoryTitleActionTest : BlockInventoryScreenTestHost
    {
        private const string FakeActionName = "fake-title-action";
        private static readonly string RocketType = BlockMasterElement.BlockTypeConst.RocketLaunchPad;

        [Test]
        public void 題の操作を持つblockTypeでは器に操作が入る()
        {
            Assert.AreEqual(BlockInventoryTitleActionKind.RocketLaunchPadCommand, BlockComponentRegistry.Resolve(RocketType).TitleAction);
            var actions = new FakeTitleActionFactory();
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(Panel(RocketType, BlockInventoryPanelError.None)), actions, new FakeRocketBodyFactory());
            simulate.FrameUpdate();
            Assert.IsNotNull(Holder(shell).Q(FakeActionName));
            Assert.AreEqual(1, actions.AttachCount);
        }

        [Test]
        public void エラー中は器が空()
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(Panel(RocketType, BlockInventoryPanelError.OpenFailed)), new FakeTitleActionFactory(), new FakeRocketBodyFactory());
            simulate.FrameUpdate();
            Assert.AreEqual(0, Holder(shell).childCount);
        }

        [Test]
        public void 操作の無いblockTypeへ替えると前の操作を外して器が空の部分だけになる()
        {
            var actions = new FakeTitleActionFactory();
            var state = new ReactiveProperty<BlockInventoryPanelState>(Panel(RocketType, BlockInventoryPanelError.None));
            var shell = BlockInventoryTestShell.Create(this, state, actions, new FakeRocketBodyFactory());
            simulate.FrameUpdate();

            state.Value = BlockInventoryTestPanels.OfType(BlockMasterElement.BlockTypeConst.Chest);
            simulate.FrameUpdate();
            Assert.AreEqual(1, actions.DetachCount);
            Assert.IsNull(Holder(shell).Q(FakeActionName));
            Assert.AreEqual(1, Holder(shell).childCount);
            Assert.AreEqual(0, Holder(shell)[0].childCount);
        }

        private static VisualElement Holder(BlockInventoryTestShell shell)
        {
            return shell.View.Q("block-inventory-title-action");
        }

        private static BlockInventoryPanelState Panel(string blockType, BlockInventoryPanelError error)
        {
            return new OpenBlockInventoryPanel(blockType, Guid.Empty, default, new BlockInstanceId(2), new Vector3Int(2, 0, 0), Array.Empty<SlotItem>(), error);
        }

        private sealed class FakeTitleActionFactory : IBlockInventoryTitleActionFactory, IBlockInventoryPart
        {
            public BlockInventoryTitleActionKind Kind => BlockInventoryTitleActionKind.RocketLaunchPadCommand;
            public VisualElement Root { get; private set; } = new() { name = FakeActionName };
            public int AttachCount { get; private set; }
            public int DetachCount { get; private set; }

            public IBlockInventoryPart Create()
            {
                return this;
            }

            public void Attach(OpenBlockInventoryPanel panel)
            {
                AttachCount++;
            }

            public void Detach()
            {
                DetachCount++;
            }
        }

        // 本文の未登録の警告を出さないための空の本文
        // Empty body so the unregistered-body warning is not raised
        private sealed class FakeRocketBodyFactory : IBlockInventoryBodyFactory
        {
            public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.RocketLaunchPad;

            public IBlockInventoryPart Create(BlockInventoryBodyMount mount)
            {
                return new EmptyBlockInventoryPart();
            }
        }
    }
}
