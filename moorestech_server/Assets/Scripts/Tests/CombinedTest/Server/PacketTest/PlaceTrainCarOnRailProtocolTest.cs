using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;

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
            var (environment, railPosition) = SetupEnvironment();

            // プロトコルを実行
            // Execute protocol
            ExecuteProtocol(environment, railPosition);

            // 結果を検証
            // Verify result
            ValidateResult();

            #region Internal

            (TrainTestEnvironment Environment, RailPositionSnapshotMessagePack RailPosition) SetupEnvironment()
            {
                // レールとインベントリを準備
                // Prepare rails and inventory
                var environment = TrainTestHelper.CreateEnvironment();
                
                var railPos1 = new Vector3Int(0, 0, 0);
                var rail1Component = TrainTestHelper.PlaceRail(environment, railPos1, BlockDirection.North, out _);
                var rail2Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(1000, 0, 0), BlockDirection.North, out _);

                // レール同士を接続する
                // Connect rails together
                //rail1Component.ConnectRailComponent(rail2Component, useFrontSideOfThis: true, useFrontSideOfTarget: true);
                rail1Component.FrontNode.ConnectNode(rail2Component.FrontNode);
                rail2Component.BackNode.ConnectNode(rail1Component.BackNode);

                // インベントリに列車アイテムを設定する
                // Put train item in inventory
                var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
                inventory.MainOpenableInventory.SetItem(InventorySlot, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TrainCarItem, 1));

                // レール位置スナップショットを生成
                // Create rail position snapshot
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(ForUnitTestItemId.TrainCarItem, out var trainCarMasterElement))
                {
                    Assert.Fail("テスト用車両マスターが見つかりません / Missing train car master for test");
                    return default;
                }
                var trainLength = TrainLengthConverter.ToRailUnits(trainCarMasterElement.Length);
                var railNodes = new List<IRailNode> { rail1Component.BackNode, rail2Component.BackNode };
                var railPosition = new RailPosition(railNodes, trainLength, 0);
                var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition.CreateSaveSnapshot());
                
                return (environment, railPositionSnapshot);
            }

            void ExecuteProtocol(TrainTestEnvironment environment, RailPositionSnapshotMessagePack railPosition)
            {
                Assert.AreEqual(0, environment.GetTrainUpdateService().GetRegisteredTrains().Count(), "初期状態では列車が存在しないべき / No trains should exist initially");
                
                // プロトコルで列車を配置
                // Place train through protocol
                var packet = CreatePlaceTrainPacket(railPosition, HotBarSlot, PlayerId);
                environment.PacketResponseCreator.GetPacketResponse(packet);
            }

            void ValidateResult()
            {
                // 列車生成とアイテム消費を検証
                // Validate train creation and item consumption
                Assert.AreEqual(1, environment.GetTrainUpdateService().GetRegisteredTrains().Count(), "列車が1つ生成されるべき / One train should be created");
                
                var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
                Assert.AreEqual(0, inventory.MainOpenableInventory.GetItem(InventorySlot).Count, "列車アイテムが消費されるべき / Train item should be consumed");
                
                var createdTrain = environment.GetTrainUpdateService().GetRegisteredTrains().Last();
                Assert.Greater(createdTrain.Cars.Count, 0, "列車は1両以上の車両を持つべき / Train should have at least one car");
            }

            #endregion
        }

        private List<byte> CreatePlaceTrainPacket(RailPositionSnapshotMessagePack railPosition, int hotBarSlot, int playerId)
        {
            return MessagePackSerializer
                .Serialize(new PlaceTrainOnRailRequestMessagePack(railPosition, hotBarSlot, playerId))
                .ToList();
        }
    }
}
