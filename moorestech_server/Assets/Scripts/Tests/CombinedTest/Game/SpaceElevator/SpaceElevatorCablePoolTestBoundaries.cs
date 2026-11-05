using System;
using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using Game.SpaceElevator.Save;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorCablePoolTestBoundaries
    {
        [Test]
        public void 到着加算が上限を超えると残量も保存値も通知数も変えず拒否する()
        {
            var provider = SpaceElevatorCablePoolTestFixture.CreateServer();
            var pool = provider.GetRequiredService<SpaceElevatorCablePool>();
            var orbital = provider.GetRequiredService<IOrbitalArrivalService>();
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue - 50 });
            orbital.Arrive(ForUnitTestItemId.TestElevatorCable, 50);
            var changes = 0;
            pool.OnAmountChanged.Subscribe(_ => changes++);

            // 通知中の失敗でも、確定前の荷物は呼び出し元に残す
            // Even a failure during notification must leave the uncommitted payload with the caller
            LogAssert.Expect(LogType.Error, new Regex("ケーブルプール.*範囲.*"));
            Assert.Throws<InvalidOperationException>(() => orbital.Arrive(ForUnitTestItemId.TestElevatorCable, 50));
            Assert.AreEqual(int.MaxValue, pool.Amount);
            Assert.AreEqual(int.MaxValue, pool.GetSaveData().CableCount);
            Assert.AreEqual(0, changes);

            // 拒否後も購読が生き、容量を空ければ同じ到着を受理する
            // The subscription survives rejection and accepts the same arrival after capacity is freed
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue - 50 });
            orbital.Arrive(ForUnitTestItemId.TestElevatorCable, 50);
            Assert.AreEqual(int.MaxValue, pool.Amount);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void 撤去返却が上限を超えるとブロックと追跡を保ち再試行で一度だけ返却する()
        {
            var provider = SpaceElevatorCablePoolTestFixture.CreateServer();
            var pool = provider.GetRequiredService<SpaceElevatorCablePool>();
            var service = provider.GetRequiredService<SpaceElevatorConnectionService>();
            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestSpaceElevatorBase, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            var component = block.ComponentManager.GetComponent<ISpaceElevatorBase>();
            component.MarkConnected();
            var cables = ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).CablesPerElevator;
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue });
            var changes = 0;
            pool.OnAmountChanged.Subscribe(_ => changes++);

            LogAssert.Expect(LogType.Error, new Regex("ケーブルプール.*範囲.*"));
            Assert.Throws<InvalidOperationException>(() => world.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove));
            Assert.AreSame(block, world.GetBlock(Vector3Int.zero));
            Assert.IsFalse(component.IsDestroy);
            Assert.AreEqual(int.MaxValue, pool.GetSaveData().CableCount);
            Assert.AreEqual(0, changes);
            LogAssert.Expect(LogType.Log, new Regex("AlreadyConnected.*instance="));
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, service.Connect(block));

            // 返却容量を空けた同じブロックの撤去で、追跡を失っていないことも確かめる
            // Removing the same block after freeing capacity also verifies that tracking was retained
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue - cables });
            Assert.IsTrue(world.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove));
            Assert.IsTrue(component.IsDestroy);
            Assert.AreEqual(int.MaxValue, pool.Amount);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void 接続の拒否は対象と理由をログに残す()
        {
            var provider = SpaceElevatorCablePoolTestFixture.CreateServer();
            var service = provider.GetRequiredService<SpaceElevatorConnectionService>();
            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest));
            LogAssert.Expect(LogType.Log, new Regex("NotSpaceElevatorBase.*instance=" + chest.BlockInstanceId.AsPrimitive()));
            Assert.AreEqual(SpaceElevatorConnectResult.NotSpaceElevatorBase, service.Connect(chest));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.TestSpaceElevatorBase, new Vector3Int(10, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));

            LogAssert.Expect(LogType.Log, new Regex("InsufficientCable.*instance=" + block.BlockInstanceId.AsPrimitive()));
            Assert.AreEqual(SpaceElevatorConnectResult.InsufficientCable, service.Connect(block));
            var pool = provider.GetRequiredService<SpaceElevatorCablePool>();
            Assert.AreEqual(0, pool.Amount);
            var cables = ((SpaceElevatorBaseBlockParam)block.BlockMasterElement.BlockParam).CablesPerElevator;
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = cables });
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(block));
            LogAssert.Expect(LogType.Log, new Regex("AlreadyConnected.*instance=" + block.BlockInstanceId.AsPrimitive()));
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, service.Connect(block));
            Assert.AreEqual(0, pool.Amount);
        }
    }
}
