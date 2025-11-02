using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Common;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceTrainCarOnRailProtocolTest
    {
        private const int PlayerId = 1;
        private const int HotBarSlot = 0;

        [Test]
        public void PlaceTrainOnRail_ValidRailAndItem_CreatesTrainUnit()
        {
            // テスト環境を構築
            // Build test environment
            var (environment, inventory, inventorySlot, railSpecifier) = SetupEnvironment();

            // プロトコルを実行
            // Execute protocol
            var (trainCountBefore, trainCountAfter, itemAfter) = ExecuteProtocol(environment, inventory, inventorySlot, railSpecifier);

            // 結果を検証
            // Verify result
            ValidateResult(trainCountBefore, trainCountAfter, itemAfter);

            #region Internal

            (TrainTestEnvironment Environment, PlayerInventoryData Inventory, int InventorySlot, RailComponentSpecifier RailSpecifier) SetupEnvironment()
            {
                // レールとインベントリを準備
                // Prepare rails and inventory
                var environment = TrainTestHelper.CreateEnvironment();
                var itemStackFactory = ServerContext.ItemStackFactory;
                var railPos1 = new Vector3Int(0, 0, 0);
                var railPos2 = new Vector3Int(1, 0, 0);
                var rail1Component = TrainTestHelper.PlaceRail(environment, railPos1, BlockDirection.North, out _);
                var rail2Component = TrainTestHelper.PlaceRail(environment, railPos2, BlockDirection.North, out _);
                rail1Component.ConnectRailComponent(rail2Component, useFrontSideOfThis: true, useFrontSideOfTarget: true);
                // デバッグログで接続状況を確認
                // Debug log to capture rail connections
                Debug.Log($"[PlaceTrainValid][Setup] frontConnections={rail1Component.FrontNode.ConnectedNodes.Count()} backConnections={rail1Component.BackNode.ConnectedNodes.Count()}");
                var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
                var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
                inventory.MainOpenableInventory.SetItem(slot, itemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));
                var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(railPos1);
                return (environment, inventory, slot, railSpecifier);
            }

            (int TrainCountBefore, int TrainCountAfter, IItemStack ItemAfter) ExecuteProtocol(TrainTestEnvironment environment, PlayerInventoryData inventory, int inventorySlot, RailComponentSpecifier railSpecifier)
            {
                // プロトコルで列車を配置
                // Place train through protocol
                var trainCountBefore = TrainUpdateService.Instance.GetRegisteredTrains().Count();
                var packet = CreatePlaceTrainPacket(railSpecifier, HotBarSlot, PlayerId);
                environment.PacketResponseCreator.GetPacketResponse(packet);
                var trainCountAfter = TrainUpdateService.Instance.GetRegisteredTrains().Count();
                var itemAfter = inventory.MainOpenableInventory.GetItem(inventorySlot);
                // デバッグログで結果を確認
                // Debug log to capture execution result
                Debug.Log($"[PlaceTrainValid][Execute] before={trainCountBefore} after={trainCountAfter} itemAfter={itemAfter.Count}");
                return (trainCountBefore, trainCountAfter, itemAfter);
            }

            void ValidateResult(int beforeCount, int afterCount, IItemStack itemAfter)
            {
                // 列車生成とアイテム消費を検証
                // Validate train creation and item consumption
                Assert.AreEqual(beforeCount + 1, afterCount, "列車が1つ生成されるべき / One train should be created");
                Assert.AreEqual(0, itemAfter.Count, "列車アイテムが消費されるべき / Train item should be consumed");
                var createdTrain = TrainUpdateService.Instance.GetRegisteredTrains().Last();
                Assert.IsNotNull(createdTrain, "列車が生成されているべき / Train should be created");
                Assert.Greater(createdTrain.Cars.Count, 0, "列車は1両以上の車両を持つべき / Train should have at least one car");
            }

            #endregion
        }

        [Test]
        public void PlaceTrainOnRail_EmptyInventorySlot_DoesNotCreateTrain()
        {
            // テスト環境を構築
            // Build test environment
            var (environment, railSpecifier) = SetupEnvironment();

            // プロトコルを実行
            // Execute protocol
            var (trainCountBefore, trainCountAfter) = ExecuteProtocol(environment, railSpecifier);

            // 結果を検証
            // Verify result
            ValidateResult(trainCountBefore, trainCountAfter);

            #region Internal

            (TrainTestEnvironment Environment, RailComponentSpecifier RailSpecifier) SetupEnvironment()
            {
                // レールのみを配置
                // Place only rails
                var environment = TrainTestHelper.CreateEnvironment();
                var railPos = new Vector3Int(0, 0, 0);
                TrainTestHelper.PlaceRail(environment, railPos, BlockDirection.North);
                var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(railPos);
                return (environment, railSpecifier);
            }

            (int TrainCountBefore, int TrainCountAfter) ExecuteProtocol(TrainTestEnvironment environment, RailComponentSpecifier railSpecifier)
            {
                // 空スロットで配置を試行
                // Attempt placement with empty slot
                var trainCountBefore = TrainUpdateService.Instance.GetRegisteredTrains().Count();
                var packet = CreatePlaceTrainPacket(railSpecifier, HotBarSlot, PlayerId);
                environment.PacketResponseCreator.GetPacketResponse(packet);
                var trainCountAfter = TrainUpdateService.Instance.GetRegisteredTrains().Count();
                return (trainCountBefore, trainCountAfter);
            }

            void ValidateResult(int beforeCount, int afterCount)
            {
                // 列車未生成を確認
                // Confirm no train was created
                Assert.AreEqual(beforeCount, afterCount, "列車が生成されないべき / No train should be created");
            }

            #endregion
        }

        [Test]
        public void PlaceTrainOnRail_InvalidRail_DoesNotCreateTrain()
        {
            // テスト環境を構築
            // Build test environment
            var (environment, inventory, inventorySlot, railSpecifier) = SetupEnvironment();

            // プロトコルを実行
            // Execute protocol
            var (trainCountBefore, trainCountAfter, itemAfter) = ExecuteProtocol(environment, inventory, inventorySlot, railSpecifier);

            // 結果を検証
            // Verify result
            ValidateResult(trainCountBefore, trainCountAfter, itemAfter);

            #region Internal

            (TrainTestEnvironment Environment, PlayerInventoryData Inventory, int InventorySlot, RailComponentSpecifier RailSpecifier) SetupEnvironment()
            {
                // アイテムのみ追加
                // Provide only item
                var environment = TrainTestHelper.CreateEnvironment();
                var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
                var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
                inventory.MainOpenableInventory.SetItem(slot, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));
                var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(new Vector3Int(99, 99, 99));
                return (environment, inventory, slot, railSpecifier);
            }

            (int TrainCountBefore, int TrainCountAfter, IItemStack ItemAfter) ExecuteProtocol(TrainTestEnvironment environment, PlayerInventoryData inventory, int inventorySlot, RailComponentSpecifier railSpecifier)
            {
                // 存在しないレールに送信
                // Send request for missing rail
                var trainCountBefore = TrainUpdateService.Instance.GetRegisteredTrains().Count();
                var packet = CreatePlaceTrainPacket(railSpecifier, HotBarSlot, PlayerId);
                environment.PacketResponseCreator.GetPacketResponse(packet);
                var trainCountAfter = TrainUpdateService.Instance.GetRegisteredTrains().Count();
                var itemAfter = inventory.MainOpenableInventory.GetItem(inventorySlot);
                return (trainCountBefore, trainCountAfter, itemAfter);
            }

            void ValidateResult(int beforeCount, int afterCount, IItemStack itemAfter)
            {
                // 列車未生成とアイテム維持を検証
                // Verify absence of train and item retention
                Assert.AreEqual(beforeCount, afterCount, "列車が生成されないべき / No train should be created");
                Assert.AreEqual(1, itemAfter.Count, "列車アイテムが消費されないべき / Train item should not be consumed");
            }

            #endregion
        }

        private List<byte> CreatePlaceTrainPacket(RailComponentSpecifier railSpecifier, int hotBarSlot, int playerId)
        {
            return MessagePackSerializer
                .Serialize(new PlaceTrainOnRailRequestMessagePack(railSpecifier, hotBarSlot, playerId))
                .ToList();
        }
    }
}
