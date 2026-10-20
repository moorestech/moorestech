using System.Text.RegularExpressions;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BeltConnectionRemovalRefundPendingTest
    {
        [Test]
        public void CorruptedPendingLogsAndDropsTheWholeOwnerWithoutRefundingStaleItems()
        {
            var world = BeltRemovalRefundTestWorld.CreateServer();
            var merge = world.CreateConnectionMerge(true, true);
            var item = world.RestoreInternal(BeltDirection.Left);
            world.RestoreInternal(BeltDirection.Right);
            var pending = new BeltConveyorSaveJsonObject
            {
                BufferItem = new BeltItemSaveJsonObject(item, 0)
            };
            pending.Items.Add(new BeltItemSaveJsonObject(item, 100) { ItemGuidStr = "invalid-guid" });
            pending.InternalItems.Add(new BeltInternalItemSaveJsonObject(BeltDirection.Left, item, 100));
            pending.InternalItems.Add(new BeltInternalItemSaveJsonObject(BeltDirection.Right, item, 100));
            world.Transport.RegisterLoadedState(merge.BlockInstanceId, pending);
            var hash = world.Transport.ComputeStateHash();

            // 一箇所の破損でも所有者全体を除外し古い実状態へ戻さない
            // Exclude the entire corrupted owner without falling back to its stale live state
            LogAssert.Expect(LogType.Error, new Regex("unreadable saved items"));
            Assert.IsEmpty(world.Transport.GetRefundItems(merge.BlockInstanceId));
            for (var query = 0; query < 3; query++)
                LogAssert.Expect(LogType.Error, new Regex("unreadable saved items"));
            Assert.IsTrue(world.Remove(new Vector3Int(-1, 0, 1)).Success);
            Assert.AreEqual(0, world.Count(item.ItemId));
            Assert.AreEqual(hash, world.Transport.ComputeStateHash());
            Assert.AreSame(pending, world.Transport.CreateSaveState(merge.BlockInstanceId));

            // 次回再構築でも同じ検証契約で全品を落とす
            // The next rebuild drops all owner items under the same validation contract
            LogAssert.Expect(LogType.Error, new Regex("unreadable saved items"));
            world.Transport.RebuildIfDirty();
            Assert.AreEqual(0, world.CarriedCount());
            Assert.IsEmpty(world.Transport.GetRefundItems(merge.BlockInstanceId));
            Assert.AreEqual(0, world.Count(item.ItemId));
        }
    }
}
