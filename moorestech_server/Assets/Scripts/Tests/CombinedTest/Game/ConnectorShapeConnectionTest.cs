using System;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
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

            // 接続されなければ歯車ネットワークは2つに分かれたまま（tickで遅延適用をflushしてから数える）
            // If not connected, the gear networks remain split in two (tick first to flush pending mutations)
            GameUpdater.UpdateOneTick();
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            Assert.AreEqual(2, GearNetworkDatastoreReflectionTestUtil.GetNetworkCountWithoutFlush(gearNetworkDatastore));
        }

        [Test]
        // 90度回転した歯車同士は歯の位置が合っても噛み合わない（回転軸が直交）
        // Gears rotated 90 degrees do not mesh even when teeth positions match (axes orthogonal)
        public void RotatedGearMeshingAxisMismatchDoesNotConnectTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // North歯車（軸=ワールドZ）とUpNorth歯車（軸=ワールドY）を±X面で向かい合わせる
            // Face a North gear (axis world-Z) and an UpNorth gear (axis world-Y) across the ±X faces
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(1, 0, 0), BlockDirection.UpNorth, Array.Empty<BlockCreateParam>(), out _);

            GameUpdater.UpdateOneTick();
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            Assert.AreEqual(2, GearNetworkDatastoreReflectionTestUtil.GetNetworkCountWithoutFlush(gearNetworkDatastore));
        }

        [Test]
        // 軸が平行な歯車同士は従来どおり噛み合う（対照実験）
        // Gears with parallel axes still mesh (control case)
        public void ParallelAxisGearsConnectTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            GameUpdater.UpdateOneTick();
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            Assert.AreEqual(1, GearNetworkDatastoreReflectionTestUtil.GetNetworkCountWithoutFlush(gearNetworkDatastore));
        }
    }
}
