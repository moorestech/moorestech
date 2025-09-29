using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class ChestLogicTest
    {
        private IBlockFactory _blockFactory;

        [SetUp]
        public void SetUp()
        {
            _ = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            _blockFactory = ServerContext.BlockFactory;
        }

        //ベルトコンベアからアイテムを搬入する
        [Test]
        public void BeltConveyorInsertChestLogicTest()
        {
            var item = ServerContext.ItemStackFactory.Create(new ItemId(5), 1);

            // ベルトコンベアにアイテムをセット
            var (chest, chestComponent) = CreateChest(new BlockInstanceId(0));
            var (beltConveyor, beltConveyorComponent) = CreateBeltConveyor(new BlockInstanceId(int.MaxValue));
            beltConveyorComponent.InsertItem(item);

            // ベルトコンベアとチェストを接続
            ConnectInventory(beltConveyor, chestComponent);
            WaitUntilChestHasItem(chestComponent, item);

            Assert.True(chestComponent.GetItem(0).Equals(item));
        }
        
        [Test]
        // チェストからベルトコンベアへアイテムを搬出する
        public void BeltConveyorOutputChestLogicTest()
        {
            // チェストにアイテムをセット
            var (chest, chestComponent) = CreateChest(new BlockInstanceId(0));
            var (beltConveyor, beltConveyorComponent) = CreateBeltConveyor(new BlockInstanceId(0));

            // ベルトコンベアとチェストを接続
            chestComponent.SetItem(0, new ItemId(1), 1);
            ConnectInventory(chest, beltConveyorComponent);
            GameUpdater.UpdateWithWait();

            Assert.AreEqual(chestComponent.GetItem(0).Count, 0);
        }

        #region Internal

        private (IBlock block, VanillaChestComponent component) CreateChest(BlockInstanceId blockInstanceId)
        {
            var block = _blockFactory.Create(ForUnitTestModBlockId.ChestId, blockInstanceId, CreateDefaultPosition());
            return (block, block.GetComponent<VanillaChestComponent>());
        }

        private (IBlock block, VanillaBeltConveyorComponent component) CreateBeltConveyor(BlockInstanceId blockInstanceId)
        {
            var block = _blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, blockInstanceId, CreateDefaultPosition());
            return (block, block.GetComponent<VanillaBeltConveyorComponent>());
        }

        private static void ConnectInventory(IBlock sourceBlock, IBlockInventory targetInventory)
        {
            var connections = (Dictionary<IBlockInventory, ConnectedInfo>)sourceBlock.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            connections[targetInventory] = new ConnectedInfo();
        }

        private static void WaitUntilChestHasItem(VanillaChestComponent chestComponent, IItemStack expected)
        {
            while (!chestComponent.GetItem(0).Equals(expected)) GameUpdater.UpdateWithWait();
        }

        private static BlockPositionInfo CreateDefaultPosition()
        {
            return new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
        }

        #endregion
    }
}
