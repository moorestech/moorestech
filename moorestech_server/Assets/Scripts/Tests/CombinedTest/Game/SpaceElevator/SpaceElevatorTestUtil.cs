using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.Orbit;
using Microsoft.Extensions.DependencyInjection;
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

        public static IBlock PlaceBase(Vector3Int position)
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

        public static void RunSeconds(double seconds)
        {
            var ticks = GameUpdater.SecondsToTicks(seconds);
            for (var i = 0; i < ticks; i++) GameUpdater.RunFrames(1);
        }
    }
}
