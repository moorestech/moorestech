using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Game.Block.Interface;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// 源の値からパネルの状態を Web ホストの DTO 工場と同じ規則で組む（L0）
    /// Builds the panel state from source values by the same rules as the Web host DTO factories (L0)
    /// </summary>
    public class BlockInventoryPanelStateFactoryTest
    {
        private BlockMasterElement _chest;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _chest = Array.Find(MasterHolder.BlockMaster.Blocks.Data, b => b.BlockType == BlockMasterElement.BlockTypeConst.Chest);
        }

        [Test]
        public void 取得成功のブロックはエラー無しでスロットと構成を持つ()
        {
            var slots = new[] { new SlotItem(new ItemId(1), 7) };
            var state = BlockInventoryPanelStateFactory.FromBlock(true, slots, _chest, new BlockInstanceId(1), new Vector3Int(1, 2, 3));
            var open = (OpenBlockInventoryPanel)state;
            Assert.AreEqual(BlockInventoryPanelError.None, open.Error);
            Assert.AreEqual(_chest.BlockGuid, open.BlockGuid);
            Assert.AreEqual(BlockMasterElement.BlockTypeConst.Chest, open.BlockType);
            Assert.AreEqual(BlockComponentRegistry.Resolve(open.BlockType).Body, open.Components.Body);
            Assert.AreEqual(7, open.ItemSlots[0].Count);
            Assert.AreEqual(new BlockInstanceId(1), open.BlockInstanceId);
        }

        [Test]
        public void 取得失敗のブロックはOpenFailed()
        {
            var state = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(false, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), Vector3Int.zero);
            Assert.AreEqual(BlockInventoryPanelError.OpenFailed, state.Error);
        }

        [TestCase(null, BlockInventoryPanelError.None)]
        [TestCase(TrainInventoryMessageType.ContainerMissing, BlockInventoryPanelError.ContainerMissing)]
        [TestCase(TrainInventoryMessageType.TrainCarMissing, BlockInventoryPanelError.TrainCarMissing)]
        [TestCase(TrainInventoryMessageType.OpenFailed, BlockInventoryPanelError.OpenFailed)]
        public void 列車の開け失敗をエラーへ写す(TrainInventoryMessageType? message, BlockInventoryPanelError expected)
        {
            var state = (OpenTrainInventoryPanel)BlockInventoryPanelStateFactory.FromTrain(5, Array.Empty<SlotItem>(), message);
            Assert.AreEqual(expected, state.Error);
            Assert.AreEqual(5, state.TrainCarInstanceId);
        }

        [Test]
        public void 同じ位置と同じ個体は同じパネル()
        {
            var a = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), new Vector3Int(1, 0, 0));
            var b = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, new[] { new SlotItem(new ItemId(1), 1) }, _chest, new BlockInstanceId(1), new Vector3Int(1, 0, 0));
            var c = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), new Vector3Int(2, 0, 0));
            var rebuilt = (OpenBlockInventoryPanel)BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(2), new Vector3Int(1, 0, 0));
            Assert.IsTrue(a.IsSamePanel(b));
            Assert.IsFalse(a.IsSamePanel(c));
            Assert.IsFalse(a.IsSamePanel(rebuilt));   // 撤去して同じ型を同じ位置に置き直した別個体（D-59）
            Assert.IsFalse(a.IsSamePanel(BlockInventoryPanelState.Closed));
        }

        [Test]
        public void エラーの有無が変われば別のパネルで列車は車両とエラーで決まる()
        {
            var ok = BlockInventoryPanelStateFactory.FromBlock(true, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), Vector3Int.zero);
            var failed = BlockInventoryPanelStateFactory.FromBlock(false, Array.Empty<SlotItem>(), _chest, new BlockInstanceId(1), Vector3Int.zero);
            Assert.IsFalse(ok.IsSamePanel(failed));
            var train = BlockInventoryPanelStateFactory.FromTrain(5, Array.Empty<SlotItem>(), null);
            Assert.IsTrue(train.IsSamePanel(BlockInventoryPanelStateFactory.FromTrain(5, new[] { new SlotItem(new ItemId(1), 1) }, null)));
            Assert.IsFalse(train.IsSamePanel(BlockInventoryPanelStateFactory.FromTrain(5, Array.Empty<SlotItem>(), TrainInventoryMessageType.ContainerMissing)));
        }
    }
}
