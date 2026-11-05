using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorSaveBoundaryTest
    {
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.01)]
        public void 不正な残り時間は衛星を保持して最初から昇る(double remaining)
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var param = (SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam;
            var save = Save(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1), remaining);

            // JSON読込の境界を通し、無限値も負値も即時到着にしない
            // Exercise JSON loading so infinite and negative values never cause immediate arrival
            LogAssert.Expect(LogType.Error, new Regex("最初から昇り直します"));
            var climber = Load(save, inventory, param);
            Assert.AreEqual(0f, Detail(climber).ClimbProgress);
            Assert.AreEqual(1, climber.GetRefundItems()[0].Count);
            Assert.Greater(climber.RequestPower, 0f);
            climber.Destroy();
        }

        [Test]
        public void 衛星なしの残り時間はエラーログとともに取り消す()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            LogAssert.Expect(LogType.Error, new Regex("衛星が無いのに残り"));
            var climber = Load(Save(ServerContext.ItemStackFactory.CreatEmpty(), 1),
                block.GetComponent<SpaceElevatorBaseInventoryComponent>(), (SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, Detail(climber).Status);
            Assert.AreEqual(0f, climber.RequestPower);
            Assert.AreEqual(0, climber.GetRefundItems().Count);
            climber.Destroy();
        }

        [Test]
        public void セーブの非空超過スロットは原本を保ってロードを止める()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            var param = (SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam;
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            var items = (List<ItemStackSaveJsonObject>)inventory.GetSaveState();
            items[0] = new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 2));
            items.Add(new ItemStackSaveJsonObject(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1)));

            // 品物が残る末尾は切り捨てず明示ロード失敗にする
            // Fail loading explicitly instead of truncating a nonempty tail
            LogAssert.Expect(LogType.Error, new Regex("出口縮小.*ロードを中断"));
            Assert.Throws<InvalidOperationException>(() => new SpaceElevatorBaseInventoryComponent(new Dictionary<string, object> { { inventory.SaveKey, items } },
                block.BlockInstanceId, param, new ConnectingInventoryListPriorityInsertItemService(block.BlockInstanceId,
                    block.GetComponent<BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext>>()), ServerContext.GetService<global::Game.Orbit.IOrbitalArrivalService>()));
            Assert.AreEqual(3, items.Count);
            Assert.AreEqual(1, items[2].Count);
        }

        [Test]
        public void 残りゼロの衛星は頂上の出口待ちとして復元する()
        {
            CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(1, ForUnitTestItemId.ItemId1, 1);
            var climber = Load(Save(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1), 0),
                inventory, (SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam);
            climber.Update();
            Assert.AreEqual(1f, Detail(climber).ClimbProgress);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, Detail(climber).Status);
            Assert.AreEqual(0f, climber.RequestPower);
            Assert.AreEqual(1, climber.GetRefundItems()[0].Count);

            // 在庫変更の購読で頂上待ちから再開する
            // Resume waiting at the top through the inventory-change subscription
            inventory.SetItem(1, ServerContext.ItemStackFactory.CreatEmpty());
            climber.Update();
            Assert.AreEqual(5, inventory.GetItem(1).Count);
            Assert.AreEqual(0, climber.GetRefundItems().Count);
            climber.Destroy();
        }

        private static SpaceElevatorClimberSaveJsonObject Save(IItemStack satellite, double remaining)
        {
            return new SpaceElevatorClimberSaveJsonObject
            {
                IsConnected = true,
                ClimbingSatellite = new ItemStackSaveJsonObject(satellite),
                RemainingClimbSeconds = remaining,
            };
        }

        private static SpaceElevatorClimberComponent Load(SpaceElevatorClimberSaveJsonObject save,
            SpaceElevatorBaseInventoryComponent inventory, SpaceElevatorBaseBlockParam param)
        {
            return new SpaceElevatorClimberComponent(new Dictionary<string, object> { { "spaceElevatorClimber", Newtonsoft.Json.Linq.JObject.FromObject(save) } }, param, inventory);
        }

        private static SpaceElevatorBaseStateDetail Detail(SpaceElevatorClimberComponent climber)
        {
            return MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(climber.GetBlockStateDetails()[0].Value);
        }
    }
}
