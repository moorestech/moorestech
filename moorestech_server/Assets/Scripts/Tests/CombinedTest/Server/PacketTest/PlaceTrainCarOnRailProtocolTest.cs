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
using Game.Train.RailGraph;
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
        
        private static int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);

        [Test]
        public void PlaceTrainOnRail_ValidRailAndItem_CreatesTrainUnit()
        {
            // テスト環境を構築
            // Build test environment
            var (environment, railSpecifier) = SetupEnvironment();

            // プロトコルを実行
            // Execute protocol
            ExecuteProtocol(environment, railSpecifier);

            // 結果を検証
            // Verify result
            ValidateResult();

            #region Internal

            (TrainTestEnvironment Environment, RailComponentSpecifier RailSpecifier) SetupEnvironment()
            {
                // レールとインベントリを準備
                // Prepare rails and inventory
                var environment = TrainTestHelper.CreateEnvironment();
                
                var railPos1 = new Vector3Int(0, 0, 0);
                var rail1Component = TrainTestHelper.PlaceRail(environment, railPos1, BlockDirection.North, out _);
                var rail2Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(1000, 0, 0), BlockDirection.North, out _);
                
                rail1Component.ConnectRailComponent(rail2Component, useFrontSideOfThis: true, useFrontSideOfTarget: true);
                
                var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
                inventory.MainOpenableInventory.SetItem(InventorySlot, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));
                
                return (environment, RailComponentSpecifier.CreateRailSpecifier(railPos1));
            }

            void ExecuteProtocol(TrainTestEnvironment environment, RailComponentSpecifier railSpecifier)
            {
                Assert.AreEqual(0, TrainUpdateService.Instance.GetRegisteredTrains().Count(), "初期状態では列車が存在しないべき / No trains should exist initially");
                
                // プロトコルで列車を配置
                // Place train through protocol
                var packet = CreatePlaceTrainPacket(railSpecifier, HotBarSlot, PlayerId);
                environment.PacketResponseCreator.GetPacketResponse(packet);
            }

            void ValidateResult()
            {
                // 列車生成とアイテム消費を検証
                // Validate train creation and item consumption
                Assert.AreEqual(1, TrainUpdateService.Instance.GetRegisteredTrains().Count(), "列車が1つ生成されるべき / One train should be created");
                
                var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
                Assert.AreEqual(0, inventory.MainOpenableInventory.GetItem(InventorySlot).Count, "列車アイテムが消費されるべき / Train item should be consumed");
                
                var createdTrain = TrainUpdateService.Instance.GetRegisteredTrains().Last();
                Assert.Greater(createdTrain.Cars.Count, 0, "列車は1両以上の車両を持つべき / Train should have at least one car");
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
