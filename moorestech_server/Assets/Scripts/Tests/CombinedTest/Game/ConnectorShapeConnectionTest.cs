using System;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class ConnectorShapeConnectionTest
    {
        [Test]
        // 歯車の歯（teeth形状）とシャフト端面（shaft形状）は位置が合っても接続しない
        // Gear teeth and shaft end do not connect even when positions match
        public void GearTeethAndShaftShapeMismatchDoesNotConnectTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // North歯車の歯(+X)に、East回転シャフト（±Zが±X向きになる）の端面を向かい合わせる
            // Face an East-rotated shaft end (local ±Z becomes world ±X) against the gear teeth (+X)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.Shaft, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);

            // 接続されなければ歯車ネットワークは2つに分かれたまま
            // If not connected, the gear networks remain split in two
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            Assert.AreEqual(2, gearNetworkDatastore.GearNetworks.Count);
        }
    }
}
