using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core
{
    public class BlockSystemTickUpdateTest
    {
        [Test]
        public void MasterTickUpdaterDrivesBeltTransportWithoutGameUpdaterObservable()
        {
            // チェスト→ベルト1マス→チェストを組み、中央tick入口だけで搬送を進める
            // Build chest -> one belt cell -> chest and advance transport through the central tick entry only
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var masterTickUpdater = serviceProvider.GetRequiredService<MasterTickUpdater>();
            var itemId = new ItemId(1);
            var source = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            var output = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1), BlockDirection.North));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 1));

            // GameUpdaterを使わずMasterTickUpdater.Updateだけで回す。1回目で再構築・チェストの押し込み・ベルト1tickが走る
            // Drive only MasterTickUpdater.Update without GameUpdater; the first call rebuilds, runs the chest push and one belt tick
            // 1マス・速度6: 出口まで255。42回後に残り3、43回目に渡る
            // One cell at speed 6: 255 to the exit; 3 away after 42 updates, handed over on update 43
            for (var i = 0; i < 42; i++) masterTickUpdater.Update();
            Assert.AreEqual(0, source.GetItem(0).Count);
            Assert.AreEqual(0, CountOf(output, itemId));
            masterTickUpdater.Update();
            Assert.AreEqual(1, CountOf(output, itemId));
        }
    }
}
