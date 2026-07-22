using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    // tick中に予約されたブロック破壊がtick末尾の一括反映まで持ち越されることを検証する
    // Verifies that block removals reserved mid-tick are held until the batch application at tick end
    public class BlockRemovalReservationTest
    {
        [Test]
        public void 予約だけではブロックは破壊されない()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var pos = new Vector3Int(0, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var reservationService = provider.GetRequiredService<IBlockRemovalReservationService>();
            reservationService.ReserveRemoval(block.BlockInstanceId, BlockRemoveReason.Broken);

            // 予約時点ではブロックが存在し続け、明示的な一括反映で初めて破壊される
            // The block keeps existing at reservation time and is destroyed only by the explicit batch application
            Assert.IsTrue(world.Exists(pos));
            reservationService.ApplyReservedRemovals();
            Assert.IsFalse(world.Exists(pos));
        }

        [Test]
        public void tick進行で予約破壊がtick末尾に確定する()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var pos = new Vector3Int(0, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            // 予約→1tick進行のみで、TickEndUpdates経由の一括反映が破壊を確定させる
            // Reserve, then advance one tick; the batch application via TickEndUpdates commits the destruction
            provider.GetRequiredService<IBlockRemovalReservationService>().ReserveRemoval(block.BlockInstanceId, BlockRemoveReason.Broken);
            GameUpdater.RunFrames(1);

            Assert.IsFalse(world.Exists(pos));
        }
    }
}
