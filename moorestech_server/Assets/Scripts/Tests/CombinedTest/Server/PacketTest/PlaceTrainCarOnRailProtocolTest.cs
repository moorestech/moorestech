using System;
using System.Collections.Generic;
using System.Linq;
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
            // テスト環境をセットアップ
            // Setup test environment
            var environment = TrainTestHelper.CreateEnvironment();
            var itemStackFactory = ServerContext.ItemStackFactory;

            // レールブロックを2つ配置
            // Place two rail blocks
            var railPos1 = new Vector3Int(0, 0, 0);
            var railPos2 = new Vector3Int(1, 0, 0);

            var rail1Component = TrainTestHelper.PlaceRail(environment, railPos1, BlockDirection.North, out var railBlock1);
            var rail2Component = TrainTestHelper.PlaceRail(environment, railPos2, BlockDirection.North, out var railBlock2);

            // レールを接続
            // Connect rails
            rail1Component.ConnectRailComponent(rail2Component, useFrontSideOfThis: true, useFrontSideOfTarget: true);

            // プレイヤーインベントリに列車アイテムを追加
            // Add train item to player inventory
            var trainItemId = ForUnitTestItemId.TrainCarItem;
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            inventory.MainOpenableInventory.SetItem(slot, itemStackFactory.Create(trainItemId, 1));

            // 列車配置前の列車数を確認
            // Check train count before placement
            var trainCountBefore = TrainUpdateService.Instance.GetRegisteredTrains().Count();

            // 列車を配置
            // Place train on rail
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(railPos1);
            var placeTrainPacket = CreatePlaceTrainPacket(railSpecifier, HotBarSlot, PlayerId);
            environment.PacketResponseCreator.GetPacketResponse(placeTrainPacket);

            // 列車が生成されたことを確認
            // Verify train was created
            var trainCountAfter = TrainUpdateService.Instance.GetRegisteredTrains().Count();
            Assert.AreEqual(trainCountBefore + 1, trainCountAfter, "列車が1つ生成されるべき / One train should be created");

            // アイテムが消費されたことを確認
            // Verify item was consumed
            var itemAfter = inventory.MainOpenableInventory.GetItem(slot);
            Assert.AreEqual(0, itemAfter.Count, "列車アイテムが消費されるべき / Train item should be consumed");

            // 生成された列車の検証
            // Verify created train
            var createdTrain = TrainUpdateService.Instance.GetRegisteredTrains().Last();
            Assert.IsNotNull(createdTrain, "列車が生成されているべき / Train should be created");
            Assert.Greater(createdTrain.Cars.Count, 0, "列車は1両以上の車両を持つべき / Train should have at least one car");
        }

        [Test]
        public void PlaceTrainOnRail_EmptyInventorySlot_DoesNotCreateTrain()
        {
            // テスト環境をセットアップ
            // Setup test environment
            var environment = TrainTestHelper.CreateEnvironment();

            // レールブロックを配置
            // Place rail blocks
            var railPos = new Vector3Int(0, 0, 0);
            TrainTestHelper.PlaceRail(environment, railPos, BlockDirection.North);

            // プレイヤーインベントリは空のまま
            // Keep player inventory empty
            var trainCountBefore = TrainUpdateService.Instance.GetRegisteredTrains().Count();

            // 列車を配置しようとする
            // Attempt to place train
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(railPos);
            var placeTrainPacket = CreatePlaceTrainPacket(railSpecifier, HotBarSlot, PlayerId);
            environment.PacketResponseCreator.GetPacketResponse(placeTrainPacket);

            // 列車が生成されていないことを確認
            // Verify no train was created
            var trainCountAfter = TrainUpdateService.Instance.GetRegisteredTrains().Count();
            Assert.AreEqual(trainCountBefore, trainCountAfter, "列車が生成されないべき / No train should be created");
        }

        [Test]
        public void PlaceTrainOnRail_InvalidRail_DoesNotCreateTrain()
        {
            // テスト環境をセットアップ
            // Setup test environment
            var environment = TrainTestHelper.CreateEnvironment();
            var itemStackFactory = ServerContext.ItemStackFactory;

            // プレイヤーインベントリに列車アイテムを追加
            // Add train item to player inventory
            var trainItemId = ForUnitTestItemId.TrainCarItem;
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            inventory.MainOpenableInventory.SetItem(slot, itemStackFactory.Create(trainItemId, 1));

            var trainCountBefore = TrainUpdateService.Instance.GetRegisteredTrains().Count();

            // 存在しないレールに列車を配置しようとする
            // Attempt to place train on non-existent rail
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(new Vector3Int(99, 99, 99));
            var placeTrainPacket = CreatePlaceTrainPacket(railSpecifier, HotBarSlot, PlayerId);
            environment.PacketResponseCreator.GetPacketResponse(placeTrainPacket);

            // 列車が生成されていないことを確認
            // Verify no train was created
            var trainCountAfter = TrainUpdateService.Instance.GetRegisteredTrains().Count();
            Assert.AreEqual(trainCountBefore, trainCountAfter, "列車が生成されないべき / No train should be created");

            // アイテムが消費されていないことを確認
            // Verify item was not consumed
            var itemAfter = inventory.MainOpenableInventory.GetItem(slot);
            Assert.AreEqual(1, itemAfter.Count, "列車アイテムが消費されないべき / Train item should not be consumed");
        }

        private List<byte> CreatePlaceTrainPacket(RailComponentSpecifier railSpecifier, int hotBarSlot, int playerId)
        {
            return MessagePackSerializer
                .Serialize(new PlaceTrainOnRailRequestMessagePack(railSpecifier, hotBarSlot, playerId))
                .ToList();
        }
    }
}
