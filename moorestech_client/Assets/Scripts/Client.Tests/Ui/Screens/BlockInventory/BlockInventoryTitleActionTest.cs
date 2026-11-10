using System;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Panels;
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
    /// 題の操作の器の付け替え L1
    /// L1 of the title-action holder swap
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
            Assert.IsNull(shell.View.Q(FakeActionName));
            Assert.IsNull(Holder(shell), "操作の無い blockType では器ごと題の行から外す");
            Assert.IsFalse(shell.View.Q("block-inventory-panel").ClassListContains(GamePanel.TitleActionUssClassName));
        }

        [Test]
        public void 題の操作を持つblockTypeはエラー中も題の右を空ける()
        {
            // Web は TitleAction があれば操作を出さないエラー中も題の span に paddingRight を付ける
            // The Web pads the title span whenever TitleAction exists, even while erroring hides the action
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(Panel(RocketType, BlockInventoryPanelError.OpenFailed)), new FakeTitleActionFactory(), new FakeRocketBodyFactory());
            simulate.FrameUpdate();
            Assert.IsTrue(shell.View.Q("block-inventory-panel").ClassListContains(GamePanel.TitleActionUssClassName));
            Assert.Greater(shell.View.Q(className: "game-panel__title").resolvedStyle.paddingRight, 0f);
        }

        [Test]
        public void 同じパネルの状態更新で器は外れ直さない()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(Panel(RocketType, BlockInventoryPanelError.None));
            var shell = BlockInventoryTestShell.Create(this, state, new FakeTitleActionFactory(), new FakeRocketBodyFactory());
            simulate.FrameUpdate();
            var detachCount = 0;
            Holder(shell).RegisterCallback<DetachFromPanelEvent>(_ => detachCount++);

            state.Value = Panel(RocketType, BlockInventoryPanelError.None);
            simulate.FrameUpdate();
            Assert.AreEqual(0, detachCount);
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
