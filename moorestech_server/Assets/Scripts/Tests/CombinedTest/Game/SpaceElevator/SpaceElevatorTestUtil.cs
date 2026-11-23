using System;
using System.Collections.Generic;
using Core.Master;
using Tests.Util.OrbitalCargo;
using Core.Update;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Orbit;
using Game.SaveLoad.Json;
using Game.SpaceElevator;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using Server.Boot;
using Server.Protocol;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util.EnergySystem;
using NUnit.Framework;
using UnityEngine;
using static Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    // 宇宙エレベーターのテスト用の組み立て。電力は電柱1本＋テスト発電機で与える
    // Test assembly for the space elevator; power comes from one pole and a test generator
    public static class SpaceElevatorTestUtil
    {
        public static (PacketResponseCreator Packet, ServiceProvider Provider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 積荷を選んだ拠点を置く。選ばない拠点が要るテストは PlaceUnselectedBase を使う
        // Place a base with its cargo selected; tests that need an unselected base use PlaceUnselectedBase
        public static IBlock PlaceBase(Vector3Int position, ItemId selectedCargo)
        {
            var block = PlaceUnselectedBase(position);
            OrbitalCargoTestUtil.SelectCargo(block, selectedCargo);
            return block;
        }

        public static IBlock PlaceUnselectedBase(Vector3Int position)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestSpaceElevatorBase, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }

        // 拠点に電柱をつなぎ、指定電力の発電機をセグメントへ足す
        // Wire a pole to the base and add a generator of the given power to the segment
        public static void PowerBase(IBlock elevatorBase, Vector3Int basePosition, float power)
        {
            var polePosition = basePosition + new Vector3Int(2, 0, 0);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            ElectricWireTestUtil.Connect(basePosition, polePosition);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ServerContext.GetService<IElectricWireNetworkLookup>().TryGetEnergySegment(elevatorBase.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(power), BlockInstanceId.Create()));
        }

        public static void LaunchCables(int count)
        {
            // テストmodのケーブルは1便50本
            // The test mod's cable launches carry 50 per launch
            var orbital = ServerContext.GetService<IOrbitalArrivalService>();
            for (var launched = 0; launched < count; launched += 50) orbital.Arrive(ForUnitTestItemId.TestElevatorCable, 50);
        }

        public static IBlock SetupConnected(float power, ItemId selectedCargo)
        {
            CreateServer();
            return PlaceConnectedBase(power, selectedCargo);
        }

        // 電力とケーブルを与え、積荷を選んで接続済みにした拠点を置く
        // Place a base that is powered, supplied with cables, has its cargo selected and is already connected
        public static IBlock PlaceConnectedBase(float power, ItemId selectedCargo)
        {
            var elevatorBase = PlaceBase(Vector3Int.zero, selectedCargo);
            PowerBase(elevatorBase, Vector3Int.zero, power);
            LaunchCables(100);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase);
            return elevatorBase;
        }

        public static SpaceElevatorBaseInventoryComponent Inventory(IBlock elevatorBase)
        {
            return elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
        }

        // セーブの昇降状態から昇降機コンポーネントを組み立てる
        // Build a climber component from a saved climbing state
        public static SpaceElevatorClimberComponent LoadClimber(IBlock elevatorBase, SpaceElevatorClimberSaveJsonObject save)
        {
            var param = (SpaceElevatorBaseBlockParam)elevatorBase.BlockMasterElement.BlockParam;
            var states = new Dictionary<string, object> { { "spaceElevatorClimber", save } };
            return new SpaceElevatorClimberComponent(states, param, elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>());
        }

        public static void RunSeconds(double seconds)
        {
            var ticks = GameUpdater.SecondsToTicks(seconds);
            for (var i = 0; i < ticks; i++) GameUpdater.RunFrames(1);
        }
    }
}
