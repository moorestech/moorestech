using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class BlockSystemTickUpdateTest
    {
        [Test]
        public void TickUpdateDrivesBeltComponentWithoutGameUpdaterObservable()
        {
            // 中央tick駆動と同じ経路でベルトブロックを構築する
            // Build a belt block through the same path used by central tick driving
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var belt = ServerContext.BlockFactory.Create(
                ForUnitTestModBlockId.BeltConveyorId,
                new BlockInstanceId(int.MaxValue),
                new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            var beltComponent = belt.GetComponent<VanillaBeltConveyorComponent>();
            var output = new DummyBlockInventory();

            // 搬出先を接続して1アイテムをベルトへ投入する
            // Connect an output and insert one item into the belt
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)belt
                .GetComponent<BlockConnectorComponent<IBlockInventory, DefaultContext>>()
                .ConnectedTargets;
            connectedTargets.Add(output, new ConnectedInfo());
            var item = ServerContext.ItemStackFactory.Create(new ItemId(1), 1);
            var remainder = beltComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainder.Id);

            // GameUpdaterを使わずBlockSystemの公開tick入口だけで搬送を進める
            // Advance transport only through BlockSystem's public tick entry without GameUpdater
            var beltParam = (BeltConveyorBlockParam)MasterHolder.BlockMaster
                .GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId)
                .BlockParam;
            var maxTicks = (int)GameUpdater.SecondsToTicks(beltParam.TimeOfItemEnterToExit) + 2;
            for (var tick = 0; tick < maxTicks && !output.IsItemExists; tick++)
            {
                belt.TickUpdate();
            }

            Assert.True(output.IsItemExists);
        }
    }
}
