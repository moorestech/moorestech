using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Common;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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

        [Test]
        public void PlaceTrain_SucceedsAndRegistersTrain()
        {
            // テスト環境と依存を準備
            // Prepare environment and dependencies
            var environment = TrainTestHelper.CreateEnvironment();
            var serviceProvider = environment.ServiceProvider;
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var packetProtocol = new PlaceTrainCarOnRailProtocol(serviceProvider);

            // レールを設置して指定子を生成
            // Place a rail block and build specifier
            var railComponent = TrainTestHelper.PlaceRail(environment, Vector3Int.zero, BlockDirection.North);
            railComponent.FrontNode.ConnectNode(railComponent.BackNode, 200);
            railComponent.BackNode.ConnectNode(railComponent.FrontNode, 200);

            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(Vector3Int.zero);

            // プレイヤーのインベントリに列車アイテムを追加
            // Insert train item into player inventory
            var inventory = inventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            var hotBarSlotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            inventory.SetItem(hotBarSlotIndex, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));

            // リクエストを生成しプロトコルを実行
            // Serialize request and execute protocol
            var request = new PlaceTrainOnRailRequestMessagePack(railSpecifier, 0, PlayerId);
            var payload = MessagePackSerializer.Serialize(request).ToList();
            var response = (PlaceTrainOnRailResponseMessagePack)packetProtocol.GetResponse(payload);

            // レスポンスの検証
            // Validate response content
            Assert.IsTrue(response.IsSuccess, "列車配置に成功フラグが立っていません");
            Assert.IsNotNull(response.TrainId, "レスポンスに列車IDが含まれていません");

            // 列車が登録されていることを確認
            // Ensure the train is registered in TrainUpdateService
            var registeredTrain = TrainUpdateService.Instance
                .GetRegisteredTrains()
                .FirstOrDefault(train => train.TrainId == response.TrainId);
            Assert.IsNotNull(registeredTrain, "生成された列車がTrainUpdateServiceに登録されていません");

            // アイテムが消費されていることを確認
            // Verify the train item is consumed from inventory
            Assert.AreEqual(ServerContext.ItemStackFactory.CreatEmpty(), inventory.GetItem(hotBarSlotIndex));

            // 列車の初期状態を検証
            // Validate the initial state of the created train
            Assert.AreEqual(140, registeredTrain.RailPosition.TrainLength, "列車長が編成データと一致しません");
            Assert.AreEqual(0, registeredTrain.RailPosition.GetDistanceToNextNode(), "初期距離が0ではありません");
            Assert.AreEqual(0d, registeredTrain.CurrentSpeed, "初期速度が0ではありません");
            Assert.IsFalse(registeredTrain.IsAutoRun, "初期状態で自動運転が有効になっています");
            Assert.AreEqual(2, registeredTrain.Cars.Count, "編成の車両数が一致しません");
            Assert.AreEqual(500000, registeredTrain.Cars[0].TractionForce);
            Assert.AreEqual(0, registeredTrain.Cars[0].InventorySlots);
            Assert.AreEqual(80, registeredTrain.Cars[0].Length);
            Assert.AreEqual(0, registeredTrain.Cars[1].TractionForce);
            Assert.AreEqual(8, registeredTrain.Cars[1].InventorySlots);
            Assert.AreEqual(60, registeredTrain.Cars[1].Length);
        }

        [Test]
        public void PlaceTrain_InvalidRail_ReturnsError()
        {
            // テスト環境と依存を準備
            // Prepare environment and dependencies
            var environment = TrainTestHelper.CreateEnvironment();
            var serviceProvider = environment.ServiceProvider;
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var packetProtocol = new PlaceTrainCarOnRailProtocol(serviceProvider);

            // プレイヤーのインベントリに列車アイテムを追加
            // Insert train item into player inventory
            var inventory = inventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            var hotBarSlotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            inventory.SetItem(hotBarSlotIndex, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));

            // 存在しない座標の指定子を生成
            // Build specifier pointing to an empty position
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(new Vector3Int(100, 0, 100));

            var request = new PlaceTrainOnRailRequestMessagePack(railSpecifier, 0, PlayerId);
            var payload = MessagePackSerializer.Serialize(request).ToList();
            var response = (PlaceTrainOnRailResponseMessagePack)packetProtocol.GetResponse(payload);

            // エラーレスポンスを検証
            // Validate the error response
            Assert.IsFalse(response.IsSuccess, "存在しないレールで成功フラグが立っています");
            Assert.AreEqual("指定されたレールが見つかりません", response.ErrorMessage);
            Assert.IsNull(response.TrainId);

            // アイテムが消費されていないことを確認
            // Ensure the train item remains in inventory
            Assert.AreEqual(1, inventory.GetItem(hotBarSlotIndex).Count);
        }

        [Test]
        public void PlaceTrain_InventoryMissing_ReturnsError()
        {
            // テスト環境と依存を準備
            // Prepare environment and dependencies
            var environment = TrainTestHelper.CreateEnvironment();
            var serviceProvider = environment.ServiceProvider;
            var packetProtocol = new PlaceTrainCarOnRailProtocol(serviceProvider);

            // レールを設置して指定子を生成
            // Place a rail and connect nodes for testing
            var railComponent = TrainTestHelper.PlaceRail(environment, Vector3Int.zero, BlockDirection.North);
            railComponent.FrontNode.ConnectNode(railComponent.BackNode, 200);
            railComponent.BackNode.ConnectNode(railComponent.FrontNode, 200);

            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(Vector3Int.zero);

            var request = new PlaceTrainOnRailRequestMessagePack(railSpecifier, 0, PlayerId);
            var payload = MessagePackSerializer.Serialize(request).ToList();
            var response = (PlaceTrainOnRailResponseMessagePack)packetProtocol.GetResponse(payload);

            // エラーレスポンスを検証
            // Validate the error response
            Assert.IsFalse(response.IsSuccess, "インベントリ無しで成功フラグが立っています");
            Assert.AreEqual("列車アイテムがインベントリにありません", response.ErrorMessage);
            Assert.IsNull(response.TrainId);
        }

        [Test]
        public void PlaceTrain_DuplicatePlacement_ReturnsError()
        {
            // テスト環境と依存を準備
            // Prepare environment and dependencies
            var environment = TrainTestHelper.CreateEnvironment();
            var serviceProvider = environment.ServiceProvider;
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var packetProtocol = new PlaceTrainCarOnRailProtocol(serviceProvider);

            // レールを設置してノード距離を設定
            // Place rail and assign node distance for validation
            var railComponent = TrainTestHelper.PlaceRail(environment, Vector3Int.zero, BlockDirection.North);
            railComponent.FrontNode.ConnectNode(railComponent.BackNode, 200);
            railComponent.BackNode.ConnectNode(railComponent.FrontNode, 200);
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(Vector3Int.zero);

            // プレイヤーのインベントリに複数の列車アイテムを追加
            // Grant player two train items for consecutive attempts
            var inventory = inventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            var hotBarSlotIndex0 = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            var hotBarSlotIndex1 = PlayerInventoryConst.HotBarSlotToInventorySlot(1);
            inventory.SetItem(hotBarSlotIndex0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));
            inventory.SetItem(hotBarSlotIndex1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));

            // 初回配置は成功させる
            // First placement should succeed
            var firstRequest = new PlaceTrainOnRailRequestMessagePack(railSpecifier, 0, PlayerId);
            var firstPayload = MessagePackSerializer.Serialize(firstRequest).ToList();
            var firstResponse = (PlaceTrainOnRailResponseMessagePack)packetProtocol.GetResponse(firstPayload);
            Assert.IsTrue(firstResponse.IsSuccess, "初回配置に失敗しました");

            // 二回目の配置は重複エラーとなる
            // Second placement should be rejected due to duplication
            var secondRequest = new PlaceTrainOnRailRequestMessagePack(railSpecifier, 1, PlayerId);
            var secondPayload = MessagePackSerializer.Serialize(secondRequest).ToList();
            var secondResponse = (PlaceTrainOnRailResponseMessagePack)packetProtocol.GetResponse(secondPayload);

            Assert.IsFalse(secondResponse.IsSuccess, "重複配置が許容されています");
            Assert.AreEqual("指定位置に既に列車が存在します", secondResponse.ErrorMessage);
            Assert.IsNull(secondResponse.TrainId);

            // アイテム消費は成功時のみ
            // Ensure only one item was consumed across both attempts
            Assert.AreEqual(ServerContext.ItemStackFactory.CreatEmpty(), inventory.GetItem(hotBarSlotIndex0));
            Assert.AreEqual(1, inventory.GetItem(hotBarSlotIndex1).Count);
        }

        [Test]
        public void PlaceTrain_InvalidTrainItem_ReturnsError()
        {
            // テスト環境と依存を準備
            // Prepare environment and dependencies
            var environment = TrainTestHelper.CreateEnvironment();
            var serviceProvider = environment.ServiceProvider;
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var packetProtocol = new PlaceTrainCarOnRailProtocol(serviceProvider);

            // レールを設置してノード距離を設定
            // Place rail and assign node distance
            var railComponent = TrainTestHelper.PlaceRail(environment, Vector3Int.zero, BlockDirection.North);
            railComponent.FrontNode.ConnectNode(railComponent.BackNode, 200);
            railComponent.BackNode.ConnectNode(railComponent.FrontNode, 200);
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(Vector3Int.zero);

            // 列車用でないアイテムをインベントリに追加
            // Insert a non-train item into inventory
            var inventory = inventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            var hotBarSlotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            inventory.SetItem(hotBarSlotIndex, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));

            var request = new PlaceTrainOnRailRequestMessagePack(railSpecifier, 0, PlayerId);
            var payload = MessagePackSerializer.Serialize(request).ToList();
            var response = (PlaceTrainOnRailResponseMessagePack)packetProtocol.GetResponse(payload);

            // エラーレスポンスを検証
            // Validate the error response
            Assert.IsFalse(response.IsSuccess, "無効なアイテムで成功しています");
            Assert.AreEqual("無効な列車アイテムです", response.ErrorMessage);
            Assert.IsNull(response.TrainId);

            // アイテムが消費されていないことを確認
            // Ensure the item remains in inventory
            Assert.AreEqual(1, inventory.GetItem(hotBarSlotIndex).Count);
        }

        [Test]
        public void PlaceTrain_ViaPacketResponseCreator_Succeeds()
        {
            // テスト環境を生成しレールを設定
            // Create environment and configure rail component
            var environment = TrainTestHelper.CreateEnvironment();
            var railComponent = TrainTestHelper.PlaceRail(environment, Vector3Int.zero, BlockDirection.North);
            railComponent.FrontNode.ConnectNode(railComponent.BackNode, 200);
            railComponent.BackNode.ConnectNode(railComponent.FrontNode, 200);
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(Vector3Int.zero);

            // インベントリに列車アイテムを投入
            // Insert train item into inventory
            var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var hotBarSlotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            inventory.SetItem(hotBarSlotIndex, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));

            // PacketResponseCreator経由でプロトコル呼び出し
            // Invoke protocol via PacketResponseCreator
            var request = new PlaceTrainOnRailRequestMessagePack(railSpecifier, 0, PlayerId);
            var payload = MessagePackSerializer.Serialize(request).ToList();
            var responses = environment.PacketResponseCreator.GetPacketResponse(payload);
            Assert.AreEqual(1, responses.Count);

            var response = MessagePackSerializer.Deserialize<PlaceTrainOnRailResponseMessagePack>(responses[0].ToArray());
            Assert.IsTrue(response.IsSuccess, "PacketResponseCreator経由で成功しませんでした");
            Assert.IsNotNull(response.TrainId);
        }
    }
}

