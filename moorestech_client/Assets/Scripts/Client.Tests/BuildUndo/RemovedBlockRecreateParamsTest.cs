using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Train.RailGraph;
using Client.Tests.UIState.Fakes;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     撤去時の生成値保持とUndo再設置への受け渡しを検証
    ///     Verifies removal captures creation params and passes them to undo
    /// </summary>
    public class RemovedBlockRecreateParamsTest
    {
        private GameObject _blockObject;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            if (_blockObject != null) Object.DestroyImmediate(_blockObject);
        }

        [Test]
        public void CapturedPierStateIsSentAsSameCreateParamOnUndo()
        {
            var block = CreateBlock();
            var processor = _blockObject.AddComponent<TrainRailStateChangeProcessor>();
            typeof(TrainRailStateChangeProcessor).GetField("railModel", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(processor, _blockObject.transform);
            var key = RailBridgePierComponentStateDetail.StateDetailKey;
            var bytes = MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(Vector3.right));
            processor.OnChangeState(new BlockStateMessagePack
            {
                CurrentStateDetail = new Dictionary<string, byte[]> { { key, bytes } },
            });
            var collector = new RemovedObjectCollector();

            // 撤去前の状態を採取し設置情報まで追跡
            // Capture state before removal and trace it to the undo request
            RemovedBlock.Capture(block, collector);
            Assert.AreEqual(0, collector.UnrecordableCount);
            Assert.AreEqual(1, collector.Objects.Count);
            var target = new FakeDeleteTarget { RemovedObjects = { collector.Objects[0] } };
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new[] { target }, sender);
            record.UndoAsync(new EmptyOccupancy()).GetAwaiter().GetResult();

            Assert.AreEqual(1, sender.PlacedBatches.Count);
            var createParams = sender.PlacedBatches[0][0].CreateParams;
            Assert.AreEqual(1, createParams.Length);
            Assert.AreEqual(key, createParams[0].Key);
            CollectionAssert.AreEqual(bytes, createParams[0].Value);
        }

        [Test]
        public void PierWithoutInitialStateIsCountedAsUnrecordable()
        {
            var block = CreateBlock();
            _blockObject.AddComponent<TrainRailStateChangeProcessor>();
            var collector = new RemovedObjectCollector();
            var reason = $"[RemovalRestore] unrecordable: block at {Vector3Int.zero}: TrainRailStateChangeProcessor has no recreate params";

            LogAssert.Expect(LogType.Warning, reason);
            RemovedBlock.Capture(block, collector);

            Assert.IsEmpty(collector.Objects);
            Assert.AreEqual(1, collector.GetUnrecordableBlocks().Count);
        }

        private BlockGameObject CreateBlock()
        {
            _blockObject = new GameObject("RemovedBlockWithParams");
            var block = _blockObject.AddComponent<BlockGameObject>();
            var blockId = ForUnitTestModBlockId.MachineId;
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockId)).GetSetMethod(true)
                .Invoke(block, new object[] { blockId });
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true)
                .Invoke(block, new object[] { new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, blockSize) });
            return block;
        }

        private sealed class EmptyOccupancy : IBlockOccupancyQuery
        {
            public BlockFootprintOccupancy GetOccupancy(Vector3Int origin, BlockDirection direction, BlockId blockId)
            {
                return BlockFootprintOccupancy.Free;
            }
        }
    }
}
